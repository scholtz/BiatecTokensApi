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
    /// ~55 unit tests for Issue #488: ComplianceOrchestrationHardeningService.
    /// Tests directly instantiate the service with a mocked IComplianceOrchestrationService.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceOrchestrationHardeningIssue488ServiceUnitTests
    {
        private Mock<IComplianceOrchestrationService> _orchestrationMock = null!;
        private ComplianceOrchestrationHardeningService _svc = null!;
        private const string CorrelationId = "test-corr-488";

        [SetUp]
        public void Setup()
        {
            _orchestrationMock = new Mock<IComplianceOrchestrationService>();

            // Default: approved
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

        // ── EvaluateLaunchReadinessAsync ──────────────────────────────────────────

        [Test]
        public async Task EvaluateLaunchReadiness_ValidRequest_ReturnsPermittedGate()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "subject-1", TokenId = "token-1" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.Success, Is.True);
            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.Permitted));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_ValidRequest_ReturnsNonEmptyEvaluationId()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "subject-2", TokenId = "token-2" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.EvaluationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvaluateLaunchReadiness_ValidRequest_CorrelationIdPropagated()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "subject-3", TokenId = "token-3" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.CorrelationId, Is.EqualTo(CorrelationId));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_MissingSubjectId_ReturnsInvalidInput()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "", TokenId = "token-4" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_WhitespaceSubjectId_ReturnsInvalidInput()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "   ", TokenId = "token-5" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
            Assert.That(result.ReasonCode, Is.EqualTo("MISSING_SUBJECT_ID"));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_MissingTokenId_ReturnsInvalidInput()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "subject-5", TokenId = "" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_WhitespaceTokenId_ReturnsInvalidInput()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "subject-6", TokenId = "  " };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
            Assert.That(result.ReasonCode, Is.EqualTo("MISSING_TOKEN_ID"));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_IdempotencyReplay_ReturnsCachedResult()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "idem-sub", TokenId = "idem-tok" };
            var first = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);
            var second = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(first.IsIdempotentReplay, Is.False);
            Assert.That(second.IsIdempotentReplay, Is.True);
            Assert.That(second.EvaluationId, Is.EqualTo(first.EvaluationId));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_IdempotencyKey_UsedForCaching()
        {
            var key = "custom-idem-key-" + Guid.NewGuid().ToString("N");
            var req1 = new ComplianceHardeningRequest { SubjectId = "s1", TokenId = "t1", IdempotencyKey = key };
            var req2 = new ComplianceHardeningRequest { SubjectId = "s2", TokenId = "t2", IdempotencyKey = key };

            var first = await _svc.EvaluateLaunchReadinessAsync(req1, CorrelationId);
            var second = await _svc.EvaluateLaunchReadinessAsync(req2, CorrelationId);

            Assert.That(second.IsIdempotentReplay, Is.True);
            Assert.That(second.EvaluationId, Is.EqualTo(first.EvaluationId));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_BlockedJurisdiction_ReturnsBlockedByCompliance()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "sub-blocked",
                TokenId = "tok-blocked",
                JurisdictionCode = "KP"
            };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_BlockedJurisdiction_IR_ReturnsBlockedByCompliance()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "sub-ir",
                TokenId = "tok-ir",
                JurisdictionCode = "IR"
            };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_BlockedJurisdiction_CU_ReturnsBlockedByCompliance()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "sub-cu",
                TokenId = "tok-cu",
                JurisdictionCode = "CU"
            };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_SanctionedSubject_ReturnsBlockedByCompliance()
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
                    DecisionId = "rejected-id"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new ComplianceHardeningRequest { SubjectId = "sanctioned-sub", TokenId = "tok-sanc" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.BlockedByCompliance));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_ConditionalJurisdiction_US_ReturnsPendingReview()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "sub-us",
                TokenId = "tok-us",
                JurisdictionCode = "US"
            };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.PendingReview));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_ConditionalJurisdiction_CN_ReturnsPendingReview()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "sub-cn",
                TokenId = "tok-cn",
                JurisdictionCode = "CN"
            };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.PendingReview));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_ConditionalJurisdiction_SG_ReturnsPendingReview()
        {
            var req = new ComplianceHardeningRequest
            {
                SubjectId = "sub-sg",
                TokenId = "tok-sg",
                JurisdictionCode = "SG"
            };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.LaunchGate, Is.EqualTo(LaunchGateStatus.PendingReview));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_SchemaVersionIs1_0()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "sub-schema", TokenId = "tok-schema" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.SchemaVersion, Is.EqualTo("1.0"));
        }

        [Test]
        public async Task EvaluateLaunchReadiness_RemediationHints_NonNull()
        {
            var req = new ComplianceHardeningRequest { SubjectId = "sub-hints", TokenId = "tok-hints" };
            var result = await _svc.EvaluateLaunchReadinessAsync(req, CorrelationId);

            Assert.That(result.RemediationHints, Is.Not.Null);
        }

        // ── GetJurisdictionConstraintAsync ────────────────────────────────────────

        [Test]
        public async Task GetJurisdictionConstraint_KP_ReturnsBlocked()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "KP" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Blocked));
        }

        [Test]
        public async Task GetJurisdictionConstraint_IR_ReturnsBlocked()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "IR" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Blocked));
        }

        [Test]
        public async Task GetJurisdictionConstraint_CU_ReturnsBlocked()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "CU" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Blocked));
        }

        [Test]
        public async Task GetJurisdictionConstraint_SY_ReturnsBlocked()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "SY" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Blocked));
        }

        [Test]
        public async Task GetJurisdictionConstraint_Blocked_ReasonCode_IsSanctions()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "KP" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.EqualTo("JURISDICTION_BLOCKED_SANCTIONS"));
        }

        [Test]
        public async Task GetJurisdictionConstraint_US_ReturnsRestrictedWithConditions()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "US" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.RestrictedWithConditions));
        }

        [Test]
        public async Task GetJurisdictionConstraint_CN_ReturnsRestrictedWithConditions()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "CN" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.RestrictedWithConditions));
        }

        [Test]
        public async Task GetJurisdictionConstraint_SG_ReturnsRestrictedWithConditions()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "SG" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.RestrictedWithConditions));
        }

        [Test]
        public async Task GetJurisdictionConstraint_Conditional_HasNonEmptyConditions()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "US" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Conditions, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetJurisdictionConstraint_DE_ReturnsPermitted()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "DE" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Permitted));
        }

        [Test]
        public async Task GetJurisdictionConstraint_FR_ReturnsPermitted()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "FR" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Permitted));
        }

        [Test]
        public async Task GetJurisdictionConstraint_AU_ReturnsPermitted()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "AU" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.Permitted));
        }

        [Test]
        public async Task GetJurisdictionConstraint_Permitted_ReasonCodeIsJurisdictionPermitted()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "DE" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.EqualTo("JURISDICTION_PERMITTED"));
        }

        [Test]
        public async Task GetJurisdictionConstraint_EmptyCode_ReturnsNotConfigured()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "" };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.NotConfigured));
        }

        [Test]
        public async Task GetJurisdictionConstraint_WhitespaceCode_ReturnsNotConfigured()
        {
            var req = new JurisdictionConstraintRequest { SubjectId = "sub-x", JurisdictionCode = "   " };
            var result = await _svc.GetJurisdictionConstraintAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(JurisdictionStatus.NotConfigured));
        }

        // ── GetSanctionsReadinessAsync ────────────────────────────────────────────

        [Test]
        public async Task GetSanctionsReadiness_ApprovedSubject_ReturnsClear()
        {
            var req = new SanctionsReadinessRequest { SubjectId = "approved-sub" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.Clear));
        }

        [Test]
        public async Task GetSanctionsReadiness_ApprovedSubject_ReasonCode_IsSanctionsClear()
        {
            var req = new SanctionsReadinessRequest { SubjectId = "approved-sub-2" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.EqualTo("SANCTIONS_CLEAR"));
        }

        [Test]
        public async Task GetSanctionsReadiness_EmptySubjectId_ReturnsNotConfigured()
        {
            var req = new SanctionsReadinessRequest { SubjectId = "" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.NotConfigured));
        }

        [Test]
        public async Task GetSanctionsReadiness_WhitespaceSubjectId_ReturnsNotConfigured()
        {
            var req = new SanctionsReadinessRequest { SubjectId = "   " };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.NotConfigured));
        }

        [Test]
        public async Task GetSanctionsReadiness_ProviderError_ReturnsProviderUnavailable()
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
                    DecisionId = "error-id"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new SanctionsReadinessRequest { SubjectId = "sub-error" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.ProviderUnavailable));
        }

        [Test]
        public async Task GetSanctionsReadiness_RejectedSubject_ReturnsFlagged()
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
                    DecisionId = "rejected-id"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new SanctionsReadinessRequest { SubjectId = "flagged-sub" };
            var result = await _svc.GetSanctionsReadinessAsync(req, CorrelationId);

            Assert.That(result.Status, Is.EqualTo(SanctionsStatus.Flagged));
        }

        // ── EnforceLaunchGateAsync ────────────────────────────────────────────────

        [Test]
        public async Task EnforceLaunchGate_CleanSubject_IsLaunchPermitted_True()
        {
            var req = new LaunchGateRequest { TokenId = "tok-clean", SubjectId = "sub-clean" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.Success, Is.True);
            Assert.That(result.IsLaunchPermitted, Is.True);
        }

        [Test]
        public async Task EnforceLaunchGate_CleanSubject_GateStatus_IsPermitted()
        {
            var req = new LaunchGateRequest { TokenId = "tok-gate-clean", SubjectId = "sub-gate-clean" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.GateStatus, Is.EqualTo(LaunchGateStatus.Permitted));
        }

        [Test]
        public async Task EnforceLaunchGate_BlockedSubject_IsLaunchPermitted_False()
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
                    DecisionId = "blocked-id"
                });

            _svc = new ComplianceOrchestrationHardeningService(
                _orchestrationMock.Object,
                new Mock<ILogger<ComplianceOrchestrationHardeningService>>().Object);

            var req = new LaunchGateRequest { TokenId = "tok-blocked", SubjectId = "sub-blocked" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.IsLaunchPermitted, Is.False);
        }

        [Test]
        public async Task EnforceLaunchGate_MissingTokenId_ReturnsInvalidInput()
        {
            var req = new LaunchGateRequest { TokenId = "", SubjectId = "sub-ok" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        [Test]
        public async Task EnforceLaunchGate_MissingSubjectId_ReturnsInvalidInput()
        {
            var req = new LaunchGateRequest { TokenId = "tok-ok", SubjectId = "" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCategory, Is.EqualTo(ComplianceErrorCategory.InvalidInput));
        }

        [Test]
        public async Task EnforceLaunchGate_MissingTokenId_ReasonCode_Correct()
        {
            var req = new LaunchGateRequest { TokenId = "  ", SubjectId = "sub-ok" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.EqualTo("GATE_MISSING_TOKEN_ID"));
        }

        [Test]
        public async Task EnforceLaunchGate_MissingSubjectId_ReasonCode_Correct()
        {
            var req = new LaunchGateRequest { TokenId = "tok-ok", SubjectId = "  " };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.ReasonCode, Is.EqualTo("GATE_MISSING_SUBJECT_ID"));
        }

        [Test]
        public async Task EnforceLaunchGate_CorrelationId_Propagated()
        {
            var req = new LaunchGateRequest { TokenId = "tok-corr", SubjectId = "sub-corr" };
            var result = await _svc.EnforceLaunchGateAsync(req, CorrelationId);

            Assert.That(result.CorrelationId, Is.EqualTo(CorrelationId));
        }

        // ── GetProviderStatusAsync ────────────────────────────────────────────────

        [Test]
        public async Task GetProviderStatus_Returns4Providers()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);

            Assert.That(result.Providers, Has.Count.EqualTo(4));
        }

        [Test]
        public async Task GetProviderStatus_KycProvider_IsActive()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            var kyc = result.Providers.FirstOrDefault(p => p.ProviderType == "KYC");

            Assert.That(kyc, Is.Not.Null);
            Assert.That(kyc!.Status, Is.EqualTo(ProviderIntegrationStatus.Active));
        }

        [Test]
        public async Task GetProviderStatus_AmlProvider_IsActive()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            var aml = result.Providers.FirstOrDefault(p => p.ProviderType == "AML");

            Assert.That(aml, Is.Not.Null);
            Assert.That(aml!.Status, Is.EqualTo(ProviderIntegrationStatus.Active));
        }

        [Test]
        public async Task GetProviderStatus_JurisdictionRulesEngine_IsNotIntegrated()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            var jre = result.Providers.FirstOrDefault(p => p.ProviderName == "JurisdictionRulesEngine");

            Assert.That(jre, Is.Not.Null);
            Assert.That(jre!.Status, Is.EqualTo(ProviderIntegrationStatus.NotIntegrated));
        }

        [Test]
        public async Task GetProviderStatus_DedicatedSanctionsProvider_IsNotIntegrated()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);
            var sp = result.Providers.FirstOrDefault(p => p.ProviderName == "DedicatedSanctionsProvider");

            Assert.That(sp, Is.Not.Null);
            Assert.That(sp!.Status, Is.EqualTo(ProviderIntegrationStatus.NotIntegrated));
        }

        [Test]
        public async Task GetProviderStatus_Success_IsTrue()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);

            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task GetProviderStatus_CorrelationId_Propagated()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);

            Assert.That(result.CorrelationId, Is.EqualTo(CorrelationId));
        }

        [Test]
        public async Task GetProviderStatus_All4ProvidersHaveNonEmptyNames()
        {
            var result = await _svc.GetProviderStatusAsync(CorrelationId);

            Assert.That(result.Providers.All(p => !string.IsNullOrEmpty(p.ProviderName)), Is.True);
        }

        [Test]
        public async Task GetProviderStatus_ReportedAt_IsRecent()
        {
            var before = DateTimeOffset.UtcNow.AddSeconds(-5);
            var result = await _svc.GetProviderStatusAsync(CorrelationId);

            Assert.That(result.ReportedAt, Is.GreaterThan(before));
        }
    }
}
