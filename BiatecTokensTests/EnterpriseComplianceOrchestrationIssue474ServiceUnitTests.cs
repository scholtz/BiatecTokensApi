using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// ~50 unit tests for Issue #474: Enterprise compliance foundation –
    /// KYC/AML orchestration and auditable decision APIs.
    /// Tests directly instantiate ComplianceOrchestrationService with mocked providers.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class EnterpriseComplianceOrchestrationIssue474ServiceUnitTests
    {
        private Mock<IKycProvider> _kycMock = null!;
        private Mock<IAmlProvider> _amlMock = null!;
        private ComplianceOrchestrationService _svc = null!;
        private const string CorrelationId = "test-corr-474";
        private const string ActorId = "test-actor";

        [SetUp]
        public void Setup()
        {
            _kycMock = new Mock<IKycProvider>();
            _amlMock = new Mock<IAmlProvider>();

            // Default KYC mock: Approved
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-REF-001", KycStatus.Approved, (string?)null));

            // Default AML mock: Approved
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-REF-001", ComplianceDecisionState.Approved, (string?)null, (string?)null));

            _svc = new ComplianceOrchestrationService(
                _kycMock.Object,
                _amlMock.Object,
                new Mock<ILogger<ComplianceOrchestrationService>>().Object);
        }

        // ─── AC1: Provider abstraction ────────────────────────────────────────────

        [Test]
        public async Task AC1_KycProvider_Exists_And_ReturnsNormalizedDecision()
        {
            var req = MakeRequest(ComplianceCheckType.Kyc);
            var result = await _svc.InitiateCheckAsync(req, ActorId, CorrelationId);
            Assert.That(result.Success, Is.True);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task AC1_AmlProvider_Exists_And_ReturnsNormalizedDecision()
        {
            var req = MakeRequest(ComplianceCheckType.Aml);
            var result = await _svc.InitiateCheckAsync(req, ActorId, CorrelationId);
            Assert.That(result.Success, Is.True);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        // ─── AC2: Orchestration logic ──────────────────────────────────────────────

        [Test]
        public async Task AC2_KycOnly_Approved_Path()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-OK", KycStatus.Approved, null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Kyc), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task AC2_KycOnly_Rejected_Path()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-REJ", KycStatus.Rejected, null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Kyc), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Rejected));
        }

        [Test]
        public async Task AC2_KycOnly_NeedsReview_Path()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-NR", KycStatus.NeedsReview, null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Kyc), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.NeedsReview));
        }

        [Test]
        public async Task AC2_AmlOnly_Approved_Path()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-OK", ComplianceDecisionState.Approved, null, null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Aml), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task AC2_AmlOnly_Rejected_SanctionsFlag_Path()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-REJ", ComplianceDecisionState.Rejected, "SANCTIONS_MATCH", null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Aml), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Rejected));
        }

        [Test]
        public async Task AC2_AmlOnly_NeedsReview_Path()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-NR", ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED", null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Aml), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.NeedsReview));
        }

        [Test]
        public async Task AC2_Combined_BothApproved_Equals_Approved()
        {
            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Combined), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task AC2_Combined_KycRejected_AmlNotCalled_Equals_Rejected()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-REJ", KycStatus.Rejected, null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Combined), ActorId, CorrelationId);

            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Rejected));
            _amlMock.Verify(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task AC2_Combined_KycApproved_AmlRejected_Equals_Rejected()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-REJ", ComplianceDecisionState.Rejected, "SANCTIONS_MATCH", null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Combined), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Rejected));
        }

        [Test]
        public async Task AC2_Combined_KycApproved_AmlNeedsReview_Equals_NeedsReview()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-NR", ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED", null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Combined), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.NeedsReview));
        }

        [Test]
        public async Task AC2_Combined_KycNeedsReview_AmlApproved_Equals_NeedsReview()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-NR", KycStatus.NeedsReview, null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Combined), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.NeedsReview));
        }

        [Test]
        public async Task AC2_Combined_KycNeedsReview_AmlNeedsReview_Equals_NeedsReview()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-NR", KycStatus.NeedsReview, null));
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-NR", ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED", null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Combined), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.NeedsReview));
        }

        [Test]
        public async Task AC2_Combined_KycNeedsReview_AmlRejected_Equals_Rejected()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-NR", KycStatus.NeedsReview, null));
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-REJ", ComplianceDecisionState.Rejected, "SANCTIONS_MATCH", null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Combined), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Rejected));
        }

        // ─── AC3: Decision normalization / error taxonomy ─────────────────────────

        [Test]
        public async Task AC3_ProviderTimeout_Returns_Error_NotApproved()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-ERR", KycStatus.NotStarted, "timeout"));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Kyc), ActorId, CorrelationId);
            Assert.That(result.State, Is.Not.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task AC3_AmlTimeout_Returns_Error_NotApproved()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-ERR", ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "Simulated timeout"));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Aml), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(result.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.Timeout));
        }

        [Test]
        public async Task AC3_ProviderUnavailable_Returns_Error_NotApproved()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-ERR", ComplianceDecisionState.Error, "PROVIDER_UNAVAILABLE", "Unavailable"));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Aml), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(result.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.ProviderUnavailable));
        }

        [Test]
        public async Task AC3_MalformedResponse_Returns_Error_NotApproved()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-ERR", ComplianceDecisionState.Error, "MALFORMED_RESPONSE", "Parse error"));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Aml), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(result.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.MalformedResponse));
        }

        [Test]
        public async Task AC3_InternalError_Returns_Error_NotApproved()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ThrowsAsync(new Exception("Simulated internal error"));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Kyc), ActorId, CorrelationId);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(result.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.InternalError));
        }

        // ─── AC4: Persistence ─────────────────────────────────────────────────────

        [Test]
        public async Task AC4_Decision_HasTimestamp_InitiatedAt()
        {
            var before = DateTimeOffset.UtcNow;
            var result = await _svc.InitiateCheckAsync(MakeRequest(), ActorId, CorrelationId);
            var after = DateTimeOffset.UtcNow;

            Assert.That(result.InitiatedAt, Is.GreaterThanOrEqualTo(before));
            Assert.That(result.InitiatedAt, Is.LessThanOrEqualTo(after));
        }

        [Test]
        public async Task AC4_Decision_CompletedAt_Set_WhenTerminal()
        {
            var result = await _svc.InitiateCheckAsync(MakeRequest(), ActorId, CorrelationId);
            Assert.That(result.CompletedAt, Is.Not.Null);
        }

        [Test]
        public async Task AC4_AuditEvents_Recorded_WithTimestamps()
        {
            var result = await _svc.InitiateCheckAsync(MakeRequest(), ActorId, CorrelationId);
            Assert.That(result.AuditTrail, Is.Not.Empty);
            Assert.That(result.AuditTrail.All(e => e.OccurredAt > DateTimeOffset.MinValue), Is.True);
        }

        [Test]
        public async Task AC4_AuditEvents_ContainCorrelationId()
        {
            var result = await _svc.InitiateCheckAsync(MakeRequest(), ActorId, CorrelationId);
            Assert.That(result.AuditTrail.All(e => e.CorrelationId == CorrelationId), Is.True);
        }

        [Test]
        public async Task AC4_AuditEvents_InitiatedEvent_RecordedFirst()
        {
            var result = await _svc.InitiateCheckAsync(MakeRequest(), ActorId, CorrelationId);
            Assert.That(result.AuditTrail.First().EventType, Is.EqualTo("CheckInitiated"));
        }

        [Test]
        public async Task AC4_AuditEvents_CompletedEvent_RecordedLast()
        {
            var result = await _svc.InitiateCheckAsync(MakeRequest(), ActorId, CorrelationId);
            var last = result.AuditTrail.Last();
            Assert.That(last.EventType, Is.Not.EqualTo("CheckInitiated"));
        }

        [Test]
        public async Task AC4_DecisionId_IsNonNull_ForNewDecision()
        {
            var result = await _svc.InitiateCheckAsync(MakeRequest(), ActorId, CorrelationId);
            Assert.That(result.DecisionId, Is.Not.Null.And.Not.Empty);
        }

        // ─── AC5: Error taxonomy ──────────────────────────────────────────────────

        [Test]
        public async Task AC5_ReasonCode_Set_ForRejection()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-REJ", KycStatus.Rejected, null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Kyc), ActorId, CorrelationId);
            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AC5_ReasonCode_Set_ForNeedsReview()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-NR", ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED", null));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Aml), ActorId, CorrelationId);
            Assert.That(result.ReasonCode, Is.EqualTo("REVIEW_REQUIRED"));
        }

        [Test]
        public async Task AC5_ReasonCode_Set_ForError()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-ERR", ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "timeout"));

            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Aml), ActorId, CorrelationId);
            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty);
        }

        // ─── AC6: Idempotency ─────────────────────────────────────────────────────

        [Test]
        public async Task AC6_Idempotency_SameKey_ReturnsSameDecision()
        {
            var req = MakeRequest();
            var r1 = await _svc.InitiateCheckAsync(req, ActorId, CorrelationId);
            var r2 = await _svc.InitiateCheckAsync(req, ActorId, CorrelationId);
            Assert.That(r1.DecisionId, Is.EqualTo(r2.DecisionId));
        }

        [Test]
        public async Task AC6_Idempotency_SameKey_SetsIsIdempotentReplay()
        {
            var req = MakeRequest();
            await _svc.InitiateCheckAsync(req, ActorId, CorrelationId);
            var r2 = await _svc.InitiateCheckAsync(req, ActorId, CorrelationId);
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task AC6_Idempotency_SameKey_NoDuplicateProviderCalls()
        {
            var req = MakeRequest(ComplianceCheckType.Aml);
            await _svc.InitiateCheckAsync(req, ActorId, CorrelationId);
            await _svc.InitiateCheckAsync(req, ActorId, CorrelationId);
            _amlMock.Verify(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task AC6_Idempotency_DifferentContext_CreatesNewDecision()
        {
            var r1 = await _svc.InitiateCheckAsync(
                new InitiateComplianceCheckRequest { SubjectId = "subject-A", ContextId = "ctx-1", CheckType = ComplianceCheckType.Aml },
                ActorId, CorrelationId);
            var r2 = await _svc.InitiateCheckAsync(
                new InitiateComplianceCheckRequest { SubjectId = "subject-A", ContextId = "ctx-2", CheckType = ComplianceCheckType.Aml },
                ActorId, CorrelationId);
            Assert.That(r1.DecisionId, Is.Not.EqualTo(r2.DecisionId));
        }

        // ─── AC6: CorrelationId propagation ──────────────────────────────────────

        [Test]
        public async Task AC6_CorrelationId_Propagated_ToResponse()
        {
            var result = await _svc.InitiateCheckAsync(MakeRequest(), ActorId, CorrelationId);
            Assert.That(result.CorrelationId, Is.EqualTo(CorrelationId));
        }

        // ─── AC8: GetStatus / GetHistory ─────────────────────────────────────────

        [Test]
        public async Task AC8_GetStatus_ExistingDecision_Returns_Decision()
        {
            var r1 = await _svc.InitiateCheckAsync(MakeRequest(), ActorId, CorrelationId);
            var r2 = await _svc.GetCheckStatusAsync(r1.DecisionId!);
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.DecisionId, Is.EqualTo(r1.DecisionId));
        }

        [Test]
        public async Task AC8_GetStatus_UnknownId_Returns_ErrorResponse()
        {
            var r = await _svc.GetCheckStatusAsync("unknown-id-xyz");
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("COMPLIANCE_CHECK_NOT_FOUND"));
        }

        [Test]
        public async Task AC8_GetHistory_SubjectId_Returns_AllDecisions()
        {
            const string subjectId = "history-subject";
            await _svc.InitiateCheckAsync(new InitiateComplianceCheckRequest { SubjectId = subjectId, ContextId = "ctx-A", CheckType = ComplianceCheckType.Kyc }, ActorId, CorrelationId);
            await _svc.InitiateCheckAsync(new InitiateComplianceCheckRequest { SubjectId = subjectId, ContextId = "ctx-B", CheckType = ComplianceCheckType.Aml }, ActorId, CorrelationId);

            var history = await _svc.GetDecisionHistoryAsync(subjectId);
            Assert.That(history.Success, Is.True);
            Assert.That(history.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public async Task AC8_GetHistory_EmptySubject_Returns_EmptyList()
        {
            var history = await _svc.GetDecisionHistoryAsync("no-decisions-subject");
            Assert.That(history.Success, Is.True);
            Assert.That(history.Decisions, Is.Empty);
        }

        // ─── Validation ───────────────────────────────────────────────────────────

        [Test]
        public async Task Validation_NullSubjectId_Returns_ValidationError()
        {
            var req = new InitiateComplianceCheckRequest { SubjectId = "", ContextId = "ctx", CheckType = ComplianceCheckType.Kyc };
            var result = await _svc.InitiateCheckAsync(req, ActorId, CorrelationId);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null);
        }

        [Test]
        public async Task Validation_EmptyContextId_Returns_ValidationError()
        {
            var req = new InitiateComplianceCheckRequest { SubjectId = "subject", ContextId = "", CheckType = ComplianceCheckType.Kyc };
            var result = await _svc.InitiateCheckAsync(req, ActorId, CorrelationId);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task Validation_NullMetadata_HandledGracefully()
        {
            var req = new InitiateComplianceCheckRequest { SubjectId = "sub", ContextId = "ctx", CheckType = ComplianceCheckType.Kyc, SubjectMetadata = new() };
            var result = await _svc.InitiateCheckAsync(req, ActorId, CorrelationId);
            Assert.That(result.Success, Is.True);
        }

        // ─── MockAmlProvider direct tests ─────────────────────────────────────────

        [Test]
        public async Task MockAmlProvider_Approved_Default()
        {
            var provider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var (_, state, _, _) = await provider.ScreenSubjectAsync("subject", new Dictionary<string, string>(), "corr");
            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task MockAmlProvider_Rejected_SanctionsFlag()
        {
            var provider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var meta = new Dictionary<string, string> { ["sanctions_flag"] = "true" };
            var (_, state, _, _) = await provider.ScreenSubjectAsync("subject", meta, "corr");
            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Rejected));
        }

        [Test]
        public async Task MockAmlProvider_NeedsReview_ReviewFlag()
        {
            var provider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var meta = new Dictionary<string, string> { ["review_flag"] = "true" };
            var (_, state, _, _) = await provider.ScreenSubjectAsync("subject", meta, "corr");
            Assert.That(state, Is.EqualTo(ComplianceDecisionState.NeedsReview));
        }

        [Test]
        public async Task MockAmlProvider_Error_SimulateTimeout()
        {
            var provider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var meta = new Dictionary<string, string> { ["simulate_timeout"] = "true" };
            var (_, state, _, _) = await provider.ScreenSubjectAsync("subject", meta, "corr");
            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
        }

        [Test]
        public async Task MockAmlProvider_Error_SimulateUnavailable()
        {
            var provider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var meta = new Dictionary<string, string> { ["simulate_unavailable"] = "true" };
            var (_, state, _, _) = await provider.ScreenSubjectAsync("subject", meta, "corr");
            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
        }

        [Test]
        public async Task MockAmlProvider_Error_SimulateMalformed()
        {
            var provider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var meta = new Dictionary<string, string> { ["simulate_malformed"] = "true" };
            var (_, state, _, _) = await provider.ScreenSubjectAsync("subject", meta, "corr");
            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
        }

        [Test]
        public void MockAmlProvider_ProviderName_NonEmpty()
        {
            var provider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            Assert.That(provider.ProviderName, Is.Not.Null.And.Not.Empty);
        }

        // ─── AC4: Combined – AML not called when KYC fails ────────────────────────

        [Test]
        public async Task AC4_CombinedCheck_AmlNotCalled_WhenKycFails()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-REJ", KycStatus.Rejected, null));

            await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Combined), ActorId, CorrelationId);
            _amlMock.Verify(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task AC4_CombinedCheck_AmlCalled_WhenKycApproves()
        {
            await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Combined), ActorId, CorrelationId);
            _amlMock.Verify(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task AC4_MultipleDecisions_SameSubject_DifferentContexts()
        {
            await _svc.InitiateCheckAsync(new InitiateComplianceCheckRequest { SubjectId = "sub-multi", ContextId = "c1", CheckType = ComplianceCheckType.Kyc }, ActorId, "corr-1");
            await _svc.InitiateCheckAsync(new InitiateComplianceCheckRequest { SubjectId = "sub-multi", ContextId = "c2", CheckType = ComplianceCheckType.Kyc }, ActorId, "corr-2");

            var history = await _svc.GetDecisionHistoryAsync("sub-multi");
            Assert.That(history.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public async Task AC4_AuditTrail_IncludesProviderReference()
        {
            var result = await _svc.InitiateCheckAsync(MakeRequest(ComplianceCheckType.Kyc), ActorId, CorrelationId);
            var kycEvent = result.AuditTrail.FirstOrDefault(e => e.EventType == "KycCompleted");
            Assert.That(kycEvent, Is.Not.Null);
            Assert.That(kycEvent!.ProviderReferenceId, Is.Not.Null.And.Not.Empty);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static InitiateComplianceCheckRequest MakeRequest(ComplianceCheckType type = ComplianceCheckType.Combined)
            => new()
            {
                SubjectId = $"subject-{Guid.NewGuid():N}",
                ContextId = $"ctx-{Guid.NewGuid():N}",
                CheckType = type
            };
    }
}
