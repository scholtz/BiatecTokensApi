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
    /// ~50 advanced coverage tests for Issue #488: ComplianceOrchestrationHardeningService.
    /// Tests branch saturation across all enum values, edge cases, concurrency, and
    /// schema contract invariants.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceOrchestrationHardeningIssue488AdvancedCoverageTests
    {
        private Mock<IComplianceOrchestrationService> _orchestrationMock = null!;
        private ComplianceOrchestrationHardeningService _svc = null!;
        private const string CorrelationId = "adv-corr-488";

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

        // ── All ComplianceErrorCategory values reachable ──────────────────────────

        [Test]
        public async Task ErrorCategory_None_Reachable_SuccessfulEval()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "ec-sub-1", TokenId = "ec-tok-1" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.None));
        }

        [Test]
        public async Task ErrorCategory_InvalidInput_Reachable_MissingSubjectId()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "", TokenId = "ec-tok-2" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        [Test]
        public async Task ErrorCategory_InvalidInput_Reachable_MissingTokenId()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "ec-sub-3", TokenId = "" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        [Test]
        public async Task ErrorCategory_InvalidInput_Reachable_GateMissingTokenId()
        {
            var req = new LaunchGateRequest { TokenId = "", SubjectId = "ec-sub-gate" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        [Test]
        public async Task ErrorCategory_InvalidInput_Reachable_GateMissingSubjectId()
        {
            var req = new LaunchGateRequest { TokenId = "ec-tok-gate", SubjectId = "" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        // ── All LaunchGateStatus values reachable ─────────────────────────────────

        [Test]
        public async Task LaunchGateStatus_Permitted_Reachable()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "lgs-sub-p", TokenId = "lgs-tok-p" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.Permitted));
        }

        [Test]
        public async Task LaunchGateStatus_BlockedByCompliance_Reachable_BlockedJurisdiction()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "lgs-sub-b",
                TokenId = "lgs-tok-b",
                JurisdictionCode = "SY"
            };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
        }

        [Test]
        public async Task LaunchGateStatus_BlockedByCompliance_Reachable_SanctionsFlagged()
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
                    DecisionId = "flag-id"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new ComplianceHardeningRequest { SubjectId = "lgs-sub-bf", TokenId = "lgs-tok-bf" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
        }

        [Test]
        public async Task LaunchGateStatus_PendingReview_Reachable_ConditionalJurisdiction()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "lgs-sub-pr",
                TokenId = "lgs-tok-pr",
                JurisdictionCode = "SG"
            };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.PendingReview));
        }

        [Test]
        public async Task LaunchGateStatus_NotReady_Reachable_ProviderUnavailable()
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
                    DecisionId = "err-id"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new ComplianceHardeningRequest { SubjectId = "lgs-sub-nr", TokenId = "lgs-tok-nr" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.NotReady));
        }

        // ── All JurisdictionStatus values reachable ───────────────────────────────

        [Test]
        public async Task JurisdictionStatus_Permitted_Reachable()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "js-sub", JurisdictionCode = "GB" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Permitted));
        }

        [Test]
        public async Task JurisdictionStatus_Blocked_Reachable()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "js-sub", JurisdictionCode = "IR" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Blocked));
        }

        [Test]
        public async Task JurisdictionStatus_RestrictedWithConditions_Reachable()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "js-sub", JurisdictionCode = "US" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.RestrictedWithConditions));
        }

        [Test]
        public async Task JurisdictionStatus_NotConfigured_Reachable()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "js-sub", JurisdictionCode = "" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.NotConfigured));
        }

        // ── All SanctionsStatus values reachable ──────────────────────────────────

        [Test]
        public async Task SanctionsStatus_Clear_Reachable_ApprovedSubject()
        {
            var req = new SanctionsReadinessRequest { SubjectId = "ss-sub-clear" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.Clear));
        }

        [Test]
        public async Task SanctionsStatus_Flagged_Reachable_RejectedSubject()
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
                    DecisionId = "rej"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new SanctionsReadinessRequest { SubjectId = "ss-sub-flagged" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.Flagged));
        }

        [Test]
        public async Task SanctionsStatus_PendingReview_Reachable_NeedsReview()
        {
            _orchestrationMock
                .Setup(o => o.InitiateCheckAsync(
                    It.IsAny<InitiateComplianceCheckRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ComplianceCheckResponse
                {
                    Success = true,
                    State = ComplianceDecisionState.NeedsReview,
                    DecisionId = "review"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new SanctionsReadinessRequest { SubjectId = "ss-sub-pending" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.PendingReview));
        }

        [Test]
        public async Task SanctionsStatus_NotConfigured_Reachable_EmptySubjectId()
        {
            var req = new SanctionsReadinessRequest { SubjectId = "" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.NotConfigured));
        }

        [Test]
        public async Task SanctionsStatus_ProviderUnavailable_Reachable_ProviderError()
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
                    DecisionId = "err"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new SanctionsReadinessRequest { SubjectId = "ss-sub-unavail" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.ProviderUnavailable));
        }

        // ── All ProviderIntegrationStatus values: Active and NotIntegrated confirmed

        [Test]
        public async Task ProviderIntegrationStatus_Active_Reachable()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            Assert.That(result.Providers.Any(p => p.Status == ProviderIntegrationStatus.Active), Is.True);
        }

        [Test]
        public async Task ProviderIntegrationStatus_NotIntegrated_Reachable()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            Assert.That(result.Providers.Any(p => p.Status == ProviderIntegrationStatus.NotIntegrated), Is.True);
        }

        // ── Concurrent calls produce independent results ───────────────────────────

        [Test]
        public async Task Concurrency_MultipleSimultaneousCalls_ProduceIndependentResults()
        {
            var tasks = Enumerable.Range(1, 10).Select(i =>
                _svc.EvaluateLaunchReadinessAsync(
                    new ComplianceHardeningRequest
                    {
                        SubjectId = $"conc-sub-{i}",
                        TokenId = $"conc-tok-{i}"
                    },
                    $"conc-corr-{i}")
            );

            var results = await Task.WhenAll(tasks);

            // All should succeed
            Assert.That(results.All(r => r.Success), Is.True);
            // All should have distinct EvaluationIds (each is new instance → new Guid)
            var ids = results.Select(r => r.EvaluationId).Distinct().ToList();
            Assert.That(ids, Has.Count.EqualTo(10));
        }

        [Test]
        public async Task Concurrency_MultipleGateEnforcements_ProduceIndependentResults()
        {
            var tasks = Enumerable.Range(1, 5).Select(i =>
                _svc.EnforceLaunchGateAsync(
                    new LaunchGateRequest
                    {
                        TokenId = $"conc-gate-tok-{i}",
                        SubjectId = $"conc-gate-sub-{i}"
                    },
                    $"conc-gate-corr-{i}")
            );

            var results = await Task.WhenAll(tasks);

            Assert.That(results.All(r => r.Success), Is.True);
            Assert.That(results.All(r => r.IsLaunchPermitted), Is.True);
        }

        // ── Null/empty/whitespace input handling ──────────────────────────────────

        [Test]
        public async Task NullInput_SubjectId_HandledGracefully()
        {
            var req = new ComplianceHardeningRequest { SubjectId = null!, TokenId = "tok-null" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        [Test]
        public async Task NullInput_TokenId_HandledGracefully()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "sub-null", TokenId = null! };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        [Test]
        public async Task EmptyString_JurisdictionCode_HandledGracefully()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-1", JurisdictionCode = "" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.NotConfigured));
        }

        [Test]
        public async Task EmptyString_SanctionsSubjectId_HandledGracefully()
        {
            var req = new SanctionsReadinessRequest { SubjectId = "" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.NotConfigured));
        }

        // ── Very long subject IDs handled without exception ───────────────────────

        [Test]
        public async Task LongSubjectId_HandledWithoutException()
        {
            var longId = new string('x', 2000);
            var req = new ComplianceHardeningRequest { SubjectId = longId, TokenId = "tok-long" };

            Assert.DoesNotThrowAsync(async () =>
                await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId));
        }

        [Test]
        public async Task LongTokenId_HandledWithoutException()
        {
            var longId = new string('y', 2000);
            var req = new ComplianceHardeningRequest { SubjectId = "sub-long", TokenId = longId };

            Assert.DoesNotThrowAsync(async () =>
                await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId));
        }

        // ── Special characters in SubjectId handled safely ────────────────────────

        [Test]
        public async Task SpecialChars_SubjectId_HandledSafely()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "sub\n\r\t<script>alert('xss')</script>",
                TokenId = "tok-special"
            };

            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result, Is.Not.Null);
            // Should succeed (special chars in subject ID are valid values)
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task SpecialChars_JurisdictionCode_UnknownCode_ReturnsPermitted()
        {
            // Special/unknown code not in blocked or conditional list → Permitted
            var req = new JurisdictionConstraintRequest
            {
                SubjectId = "sub-spec",
                JurisdictionCode = "ZZ"
            };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Permitted));
        }

        // ── Multiple subjects produce independent evaluations ─────────────────────

        [Test]
        public async Task MultipleSubjects_ProduceIndependentEvaluationIds()
        {
            var r1 = await _svc.EvaluateLaunchReadinessAsync(
                new ComplianceHardeningRequest { SubjectId = "ind-sub-1", TokenId = "ind-tok-1" },
                CorrelationId);

            var r2 = await _svc.EvaluateLaunchReadinessAsync(
                new ComplianceHardeningRequest { SubjectId = "ind-sub-2", TokenId = "ind-tok-2" },
                CorrelationId);

            Assert.That(r1.EvaluationId, Is.Not.EqualTo(r2.EvaluationId));
        }

        [Test]
        public async Task MultipleSubjects_SameTokenDifferentSubject_ProduceIndependentResults()
        {
            var r1 = await _svc.EvaluateLaunchReadinessAsync(
                new ComplianceHardeningRequest { SubjectId = "multi-sub-a", TokenId = "multi-tok-shared" },
                CorrelationId);

            var r2 = await _svc.EvaluateLaunchReadinessAsync(
                new ComplianceHardeningRequest { SubjectId = "multi-sub-b", TokenId = "multi-tok-shared" },
                CorrelationId);

            Assert.That(r1.EvaluationId, Is.Not.EqualTo(r2.EvaluationId));
        }

        // ── ReasonCode is always a non-empty stable string ────────────────────────

        [Test]
        public async Task ReasonCode_AlwaysNonEmpty_Permitted()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "rc-sub-1", TokenId = "rc-tok-1" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ReasonCode_AlwaysNonEmpty_BlockedJurisdiction()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "rc-sub-kp",
                TokenId = "rc-tok-kp",
                JurisdictionCode = "KP"
            };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ReasonCode_AlwaysNonEmpty_InvalidInput()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "", TokenId = "rc-tok-inv" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty);
        }

        // ── RemediationHints: non-null lists (can be empty for Permitted) ─────────

        [Test]
        public async Task RemediationHints_NonNull_ForPermitted()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "rh-sub-p", TokenId = "rh-tok-p" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Null);
        }

        [Test]
        public async Task RemediationHints_NonEmpty_ForBlocked()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "rh-sub-b",
                TokenId = "rh-tok-b",
                JurisdictionCode = "CU"
            };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Null.And.Not.Empty);
        }

        // ── SchemaVersion is "1.0" ────────────────────────────────────────────────

        [Test]
        public async Task SchemaVersion_Is1_0_ForSuccess()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "sv-sub", TokenId = "sv-tok" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.SchemaVersion, Is.EqualTo("1.0"));
        }

        [Test]
        public async Task SchemaVersion_Is1_0_ForInvalidInput()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "", TokenId = "sv-tok-inv" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.SchemaVersion, Is.EqualTo("1.0"));
        }

        // ── BlockingReasons populated when gate is blocked ────────────────────────

        [Test]
        public async Task BlockingReasons_NonEmpty_WhenSanctionsFlagged()
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
                    DecisionId = "br-flagged"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new LaunchGateRequest { TokenId = "br-tok", SubjectId = "br-sub" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.BlockingReasons, Is.Not.Empty);
        }

        [Test]
        public async Task BlockingReasons_Empty_WhenPermitted()
        {
            var req = new LaunchGateRequest { TokenId = "br-tok-ok", SubjectId = "br-sub-ok" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.BlockingReasons, Is.Empty);
        }

        // ── Explicit "not integrated" providers never silently return success ──────

        [Test]
        public async Task NotIntegratedProviders_NeverReturnActiveStatus()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            var notIntegrated = result.Providers
                .Where(p => p.ProviderName is "JurisdictionRulesEngine" or "DedicatedSanctionsProvider")
                .ToList();

            Assert.That(notIntegrated, Has.Count.EqualTo(2));
            Assert.That(notIntegrated.All(p => p.Status != ProviderIntegrationStatus.Active), Is.True);
        }

        [Test]
        public async Task NotIntegratedProviders_HaveNonNullStatusMessages()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            var notIntegrated = result.Providers
                .Where(p => p.Status == ProviderIntegrationStatus.NotIntegrated)
                .ToList();

            Assert.That(notIntegrated.All(p => !string.IsNullOrEmpty(p.StatusMessage)), Is.True);
        }

        // ── SanctionsStatus.PendingReview from Pending state ─────────────────────

        [Test]
        public async Task SanctionsStatus_PendingReview_Reachable_PendingState()
        {
            _orchestrationMock
                .Setup(o => o.InitiateCheckAsync(
                    It.IsAny<InitiateComplianceCheckRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ComplianceCheckResponse
                {
                    Success = true,
                    State = ComplianceDecisionState.Pending,
                    DecisionId = "pending"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new SanctionsReadinessRequest { SubjectId = "pend-sub" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.PendingReview));
        }

        // ── ProviderReferenceId set from DecisionId ───────────────────────────────

        [Test]
        public async Task SanctionsResult_ProviderReferenceId_SetFromDecisionId()
        {
            var expectedDecisionId = "decision-ref-12345";
            _orchestrationMock
                .Setup(o => o.InitiateCheckAsync(
                    It.IsAny<InitiateComplianceCheckRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ComplianceCheckResponse
                {
                    Success = true,
                    State = ComplianceDecisionState.Approved,
                    DecisionId = expectedDecisionId
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new SanctionsReadinessRequest { SubjectId = "ref-sub" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.ProviderReferenceId, Is.EqualTo(expectedDecisionId));
        }

        // ── Jurisdiction code is case-insensitive ─────────────────────────────────

        [Test]
        public async Task JurisdictionCode_CaseInsensitive_LowercaseKp_Blocked()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "ci-sub", JurisdictionCode = "kp" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Blocked));
        }

        [Test]
        public async Task JurisdictionCode_CaseInsensitive_LowercaseUs_Conditional()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "ci-sub", JurisdictionCode = "us" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.RestrictedWithConditions));
        }

        [Test]
        public async Task JurisdictionCode_CaseInsensitive_LowercaseDe_Permitted()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "ci-sub", JurisdictionCode = "de" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Permitted));
        }
    }
}
