using BiatecTokensApi.Models.ComplianceHardening;
using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// ~25 E2E workflow tests for Issue #488: ComplianceOrchestrationHardeningService.
    /// Validates full evaluate→enforce workflows, determinism, correlation propagation,
    /// and explicit "not integrated" semantics.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceOrchestrationHardeningIssue488E2EWorkflowTests
    {
        private Mock<IComplianceOrchestrationService> _orchestrationMock = null!;
        private ComplianceOrchestrationHardeningService _svc = null!;
        private const string CorrelationId = "e2e-corr-488";

        [SetUp]
        public void Setup()
        {
            _orchestrationMock = new Mock<IComplianceOrchestrationService>();
            _orchestrationMock
                .Setup(o => o.InitiateCheckAsync(
                    It.IsAny<InitiateComplianceCheckRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ComplianceCheckResponse
                {
                    Success = true,
                    State = ComplianceDecisionState.Approved,
                    DecisionId = "test-id"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);
        }

        // ── Full evaluate-readiness → enforce-launch-gate workflow ────────────────

        [Test]
        public async Task E2E_EvaluateThenEnforce_CleanPath_BothPermitted()
        {
            var evalReq = new ComplianceHardeningRequest
            {
                SubjectId = "e2e-sub-clean",
                TokenId = "e2e-tok-clean",
                JurisdictionCode = "DE"
            };

            var evalResult = await _svc.EvaluateLaunchReadinessAsync(evalReq, CorrelationId);
            Assert.That(evalResult.LaunchGate, Is.EqualTo(LaunchGateStatus.Permitted));

            var gateReq = new LaunchGateRequest
            {
                TokenId = "e2e-tok-clean-gate",
                SubjectId = "e2e-sub-clean-gate"
            };

            var gateResult = await _svc.EnforceLaunchGateAsync(gateReq, CorrelationId);
            Assert.That(gateResult.IsLaunchPermitted, Is.True);
        }

        [Test]
        public async Task E2E_EvaluateThenEnforce_CorrelationId_ConsistentThroughWorkflow()
        {
            var evalReq = new ComplianceHardeningRequest
            {
                SubjectId = "e2e-sub-corr",
                TokenId = "e2e-tok-corr"
            };

            var evalResult = await _svc.EvaluateLaunchReadinessAsync(evalReq, CorrelationId);
            Assert.That(evalResult.CorrelationId, Is.EqualTo(CorrelationId));

            var gateReq = new LaunchGateRequest
            {
                TokenId = "e2e-tok-corr-gate",
                SubjectId = "e2e-sub-corr-gate"
            };

            var gateResult = await _svc.EnforceLaunchGateAsync(gateReq, CorrelationId);
            Assert.That(gateResult.CorrelationId, Is.EqualTo(CorrelationId));
        }

        // ── Blocked jurisdiction stops at gate enforcement ────────────────────────

        [Test]
        public async Task E2E_BlockedJurisdiction_EvalIsBlocked()
        {
            var evalReq = new ComplianceHardeningRequest
            {
                SubjectId = "e2e-sub-kp",
                TokenId = "e2e-tok-kp",
                JurisdictionCode = "KP"
            };

            var evalResult = await _svc.EvaluateLaunchReadinessAsync(evalReq, CorrelationId);

            Assert.That(evalResult.LaunchGate, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
            Assert.That(evalResult.Success, Is.True); // operation succeeded; content is blocked
        }

        [Test]
        public async Task E2E_BlockedJurisdiction_GateEnforcement_IsNotPermitted()
        {
            // Use a sanctioned subject: mock returns Rejected so both eval and gate see a blocked result
            _orchestrationMock
                .Setup(o => o.InitiateCheckAsync(
                    It.IsAny<InitiateComplianceCheckRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ComplianceCheckResponse
                {
                    Success = true,
                    State = ComplianceDecisionState.Rejected,
                    DecisionId = "kp2-blocked"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var gateReq = new LaunchGateRequest
            {
                TokenId = "e2e-tok-kp2-gate",
                SubjectId = "e2e-sub-kp2-gate"
            };

            var gateResult = await _svc.EnforceLaunchGateAsync(gateReq, CorrelationId);

            Assert.That(gateResult.IsLaunchPermitted, Is.False);
        }

        // ── Provider unavailable propagates NotReady ──────────────────────────────

        [Test]
        public async Task E2E_ProviderUnavailable_EvalReturnsNotReady()
        {
            _orchestrationMock
                .Setup(o => o.InitiateCheckAsync(
                    It.IsAny<InitiateComplianceCheckRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ComplianceCheckResponse
                {
                    Success = false,
                    State = ComplianceDecisionState.Error,
                    DecisionId = "unavail-id"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new ComplianceHardeningRequest
            {
                SubjectId = "e2e-sub-unavail",
                TokenId = "e2e-tok-unavail"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.NotReady));
        }

        [Test]
        public async Task E2E_ProviderUnavailable_SanctionsStatus_IsProviderUnavailable()
        {
            _orchestrationMock
                .Setup(o => o.InitiateCheckAsync(
                    It.IsAny<InitiateComplianceCheckRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ComplianceCheckResponse
                {
                    Success = false,
                    State = ComplianceDecisionState.Error,
                    DecisionId = "unavail-id-2"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new ComplianceHardeningRequest
            {
                SubjectId = "e2e-sub-unavail-2",
                TokenId = "e2e-tok-unavail-2"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.SanctionsResult!.Status, Is.EqualTo(SanctionsStatus.ProviderUnavailable));
        }

        // ── Determinism: same inputs → same outputs on 3 consecutive calls ────────

        [Test]
        public async Task E2E_Determinism_SameInputs_3Runs_SameGateStatus()
        {
            var idempKey = "det-key-" + Guid.NewGuid().ToString("N");

            var req = new ComplianceHardeningRequest
            {
                SubjectId = "det-sub",
                TokenId = "det-tok",
                JurisdictionCode = "AU",
                IdempotencyKey = idempKey
            };

            var r1 = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);
            var r2 = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);
            var r3 = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(r1.LaunchGate, Is.EqualTo(r2.LaunchGate));
            Assert.That(r2.LaunchGate, Is.EqualTo(r3.LaunchGate));
        }

        [Test]
        public async Task E2E_Determinism_SameInputs_3Runs_SameEvaluationId()
        {
            var idempKey = "det-key2-" + Guid.NewGuid().ToString("N");

            var req = new ComplianceHardeningRequest
            {
                SubjectId = "det-sub-2",
                TokenId = "det-tok-2",
                IdempotencyKey = idempKey
            };

            var r1 = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);
            var r2 = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);
            var r3 = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(r1.EvaluationId, Is.EqualTo(r2.EvaluationId));
            Assert.That(r2.EvaluationId, Is.EqualTo(r3.EvaluationId));
        }

        // ── Error category differentiation ────────────────────────────────────────

        [Test]
        public async Task E2E_InvalidInput_MissingSubjectId_ErrorCategory_IsInvalidInput()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "", TokenId = "tok-e2e" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        [Test]
        public async Task E2E_InvalidInput_MissingTokenId_ErrorCategory_IsInvalidInput()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "sub-e2e", TokenId = "" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        [Test]
        public async Task E2E_SuccessfulEval_ErrorCategory_IsNone()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "sub-ok", TokenId = "tok-ok" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.None));
        }

        // ── ProviderStatusListResponse always includes explicit "not integrated" ──

        [Test]
        public async Task E2E_ProviderStatus_AlwaysHasExplicitNotIntegratedEntries()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            var notIntegrated = result.Providers.Where(p =>
                p.Status == ProviderIntegrationStatus.NotIntegrated).ToList();

            Assert.That(notIntegrated, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(notIntegrated.All(p => !string.IsNullOrEmpty(p.StatusMessage)), Is.True);
        }

        // ── LaunchGate is BlockedByCompliance (not silently permitted) when flagged

        [Test]
        public async Task E2E_SanctionsFlagged_LaunchGate_IsBlockedByCompliance_NotPermitted()
        {
            _orchestrationMock
                .Setup(o => o.InitiateCheckAsync(
                    It.IsAny<InitiateComplianceCheckRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ComplianceCheckResponse
                {
                    Success = true,
                    State = ComplianceDecisionState.Rejected,
                    DecisionId = "flagged"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new ComplianceHardeningRequest { SubjectId = "flagged-sub", TokenId = "flagged-tok" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.Not.EqualTo(LaunchGateStatus.Permitted),
                "Flagged sanctions must not silently yield Permitted gate");
            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
        }

        [Test]
        public async Task E2E_SanctionsFlagged_GateEnforce_IsNotPermitted()
        {
            _orchestrationMock
                .Setup(o => o.InitiateCheckAsync(
                    It.IsAny<InitiateComplianceCheckRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ComplianceCheckResponse
                {
                    Success = true,
                    State = ComplianceDecisionState.Rejected,
                    DecisionId = "flagged-gate"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new LaunchGateRequest { TokenId = "flagged-gate-tok", SubjectId = "flagged-gate-sub" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.IsLaunchPermitted, Is.False);
            Assert.That(result.GateStatus, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
        }

        // ── Provider statuses included in eval response ───────────────────────────

        [Test]
        public async Task E2E_EvalResponse_IncludesProviderStatuses()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "prov-sub", TokenId = "prov-tok" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ProviderStatuses, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ProviderStatuses, Has.Count.EqualTo(4));
        }

        [Test]
        public async Task E2E_EvalResponse_ProviderStatuses_ContainNotIntegrated()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "prov-sub-2", TokenId = "prov-tok-2" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ProviderStatuses.Any(p =>
                p.Status == ProviderIntegrationStatus.NotIntegrated), Is.True);
        }
    }
}
