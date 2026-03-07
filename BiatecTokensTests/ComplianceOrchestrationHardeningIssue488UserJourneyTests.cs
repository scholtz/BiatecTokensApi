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
    /// ~35 user journey tests for Issue #488: ComplianceOrchestrationHardeningService.
    /// Tests simulate realistic operator workflows end-to-end through the service.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceOrchestrationHardeningIssue488UserJourneyTests
    {
        private Mock<IComplianceOrchestrationService> _orchestrationMock = null!;
        private ComplianceOrchestrationHardeningService _svc = null!;
        private const string CorrelationId = "journey-corr-488";

        [SetUp]
        public void Setup()
        {
            _orchestrationMock = new Mock<IComplianceOrchestrationService>();

            // Default: approved AML check
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

        // ── Journey 1: Enterprise operator, DE jurisdiction, approved subject → Permitted ─

        [Test]
        public async Task Journey1_EnterpriseOperator_DE_Clean_ReturnsPermitted()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "enterprise-issuer-001",
                TokenId = "security-token-de-001",
                JurisdictionCode = "DE",
                TenantId = "enterprise-tenant-a"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.Success, Is.True);
            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.Permitted));
        }

        [Test]
        public async Task Journey1_EnterpriseOperator_DE_JurisdictionResult_IsPermitted()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "enterprise-issuer-001",
                TokenId = "security-token-de-002",
                JurisdictionCode = "DE"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.JurisdictionResult, Is.Not.Null);
            Assert.That(result.JurisdictionResult!.Status, Is.EqualTo(JurisdictionStatus.Permitted));
        }

        [Test]
        public async Task Journey1_EnterpriseOperator_DE_SanctionsResult_IsClear()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "enterprise-issuer-003",
                TokenId = "security-token-de-003",
                JurisdictionCode = "DE"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.SanctionsResult, Is.Not.Null);
            Assert.That(result.SanctionsResult!.Status, Is.EqualTo(SanctionsStatus.Clear));
        }

        [Test]
        public async Task Journey1_EnterpriseOperator_DE_ProviderStatuses_Included()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "enterprise-issuer-004",
                TokenId = "security-token-de-004",
                JurisdictionCode = "DE"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ProviderStatuses, Has.Count.EqualTo(4));
        }

        // ── Journey 2: Operator launches for US jurisdiction → PendingReview ─────

        [Test]
        public async Task Journey2_US_Jurisdiction_ReturnsPendingReview()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "us-issuer-001",
                TokenId = "us-token-001",
                JurisdictionCode = "US"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.PendingReview));
        }

        [Test]
        public async Task Journey2_US_Jurisdiction_JurisdictionResult_IsRestrictedWithConditions()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "us-issuer-002",
                TokenId = "us-token-002",
                JurisdictionCode = "US"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.JurisdictionResult!.Status, Is.EqualTo(JurisdictionStatus.RestrictedWithConditions));
        }

        [Test]
        public async Task Journey2_US_Jurisdiction_HasConditions()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "us-issuer-003",
                TokenId = "us-token-003",
                JurisdictionCode = "US"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.JurisdictionResult!.Conditions, Is.Not.Empty);
            Assert.That(result.JurisdictionResult.Conditions.Any(c => c.Contains("SEC")), Is.True);
        }

        [Test]
        public async Task Journey2_US_Jurisdiction_HasRemediationHints()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "us-issuer-004",
                TokenId = "us-token-004",
                JurisdictionCode = "US"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Empty);
        }

        // ── Journey 3: OFAC-blocked jurisdiction (KP) → BlockedByCompliance ──────

        [Test]
        public async Task Journey3_OFAC_KP_BlockedByCompliance()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "kp-issuer-001",
                TokenId = "kp-token-001",
                JurisdictionCode = "KP"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
        }

        [Test]
        public async Task Journey3_OFAC_KP_HasRemediationHints()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "kp-issuer-002",
                TokenId = "kp-token-002",
                JurisdictionCode = "KP"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Empty);
        }

        [Test]
        public async Task Journey3_OFAC_KP_JurisdictionResult_IsBlocked()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "kp-issuer-003",
                TokenId = "kp-token-003",
                JurisdictionCode = "KP"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.JurisdictionResult!.Status, Is.EqualTo(JurisdictionStatus.Blocked));
        }

        [Test]
        public async Task Journey3_OFAC_KP_ReasonCode_IsBlockedByJurisdiction()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "kp-issuer-004",
                TokenId = "kp-token-004",
                JurisdictionCode = "KP"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.EqualTo("BLOCKED_BY_JURISDICTION"));
        }

        // ── Journey 4: Operator launches with flagged subject → BlockedByCompliance

        [Test]
        public async Task Journey4_FlaggedSubject_BlockedByCompliance()
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
                    DecisionId = "flagged-decision"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new ComplianceHardeningRequest
            {
                SubjectId = "flagged-issuer-001",
                TokenId = "flagged-token-001",
                JurisdictionCode = "DE"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
        }

        [Test]
        public async Task Journey4_FlaggedSubject_SanctionsResult_IsFlagged()
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
                    DecisionId = "flagged-decision-2"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new ComplianceHardeningRequest
            {
                SubjectId = "flagged-issuer-002",
                TokenId = "flagged-token-002"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.SanctionsResult!.Status, Is.EqualTo(SanctionsStatus.Flagged));
        }

        [Test]
        public async Task Journey4_FlaggedSubject_HasRemediationHints()
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
                    DecisionId = "flagged-decision-3"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new ComplianceHardeningRequest
            {
                SubjectId = "flagged-issuer-003",
                TokenId = "flagged-token-003"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Empty);
        }

        // ── Journey 5: Idempotency ────────────────────────────────────────────────

        [Test]
        public async Task Journey5_Idempotency_SameRequestTwice_ReturnsSameResult()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "idem-issuer-j5",
                TokenId = "idem-token-j5",
                JurisdictionCode = "FR"
            };

            var first = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);
            var second = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(second.EvaluationId, Is.EqualTo(first.EvaluationId));
            Assert.That(second.LaunchGate, Is.EqualTo(first.LaunchGate));
        }

        [Test]
        public async Task Journey5_Idempotency_SecondCall_IsIdempotentReplay()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "idem-issuer-j5b",
                TokenId = "idem-token-j5b"
            };

            await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);
            var second = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(second.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task Journey5_Idempotency_FirstCall_IsNotReplay()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "idem-issuer-j5c",
                TokenId = "idem-token-j5c"
            };

            var first = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(first.IsIdempotentReplay, Is.False);
        }

        // ── Journey 6: Provider status check reveals "not integrated" providers ──

        [Test]
        public async Task Journey6_ProviderStatus_ExplicitNotIntegrated_JurisdictionEngine()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            var jre = result.Providers.Single(p => p.ProviderName == "JurisdictionRulesEngine");

            Assert.That(jre.Status, Is.EqualTo(ProviderIntegrationStatus.NotIntegrated));
            Assert.That(jre.StatusMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Journey6_ProviderStatus_ExplicitNotIntegrated_DedicatedSanctions()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            var sp = result.Providers.Single(p => p.ProviderName == "DedicatedSanctionsProvider");

            Assert.That(sp.Status, Is.EqualTo(ProviderIntegrationStatus.NotIntegrated));
            Assert.That(sp.StatusMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Journey6_ProviderStatus_TwoActiveProviders()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            var activeCount = result.Providers.Count(p => p.Status == ProviderIntegrationStatus.Active);

            Assert.That(activeCount, Is.EqualTo(2));
        }

        [Test]
        public async Task Journey6_ProviderStatus_TwoNotIntegratedProviders()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            var notIntCount = result.Providers.Count(p => p.Status == ProviderIntegrationStatus.NotIntegrated);

            Assert.That(notIntCount, Is.EqualTo(2));
        }

        // ── Journey 7: Launch gate enforcement blocks non-cleared compliance ──────

        [Test]
        public async Task Journey7_GateEnforcement_BlockedSubject_IsNotPermitted()
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
                    DecisionId = "gate-blocked"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new LaunchGateRequest { TokenId = "tok-gate-j7", SubjectId = "sub-gate-j7" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.IsLaunchPermitted, Is.False);
        }

        [Test]
        public async Task Journey7_GateEnforcement_BlockedSubject_GateStatus_IsBlockedByCompliance()
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
                    DecisionId = "gate-blocked-2"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new LaunchGateRequest { TokenId = "tok-gate-j7b", SubjectId = "sub-gate-j7b" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.GateStatus, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
        }

        [Test]
        public async Task Journey7_GateEnforcement_CleanSubject_IsPermitted()
        {
            var req = new LaunchGateRequest { TokenId = "tok-gate-j7c", SubjectId = "sub-gate-j7c" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.IsLaunchPermitted, Is.True);
        }
    }
}
