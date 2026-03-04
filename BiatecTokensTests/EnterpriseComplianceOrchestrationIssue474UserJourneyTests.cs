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
    /// ~29 user journey tests for Issue #474: Enterprise compliance foundation –
    /// KYC/AML orchestration and auditable decision APIs.
    ///
    /// Tests exercise user journeys via the service directly (no HTTP):
    ///  HP – Happy path
    ///  II – Idempotency invariants
    ///  BD – Business decision correctness
    ///  FR – Failure recovery / error taxonomy
    ///  NX – Negative / boundary scenarios
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class EnterpriseComplianceOrchestrationIssue474UserJourneyTests
    {
        private Mock<IKycProvider> _kycMock = null!;
        private Mock<IAmlProvider> _amlMock = null!;
        private ComplianceOrchestrationService _svc = null!;

        [SetUp]
        public void Setup()
        {
            _kycMock = new Mock<IKycProvider>();
            _amlMock = new Mock<IAmlProvider>();

            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-REF", KycStatus.Approved, null));

            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-REF", ComplianceDecisionState.Approved, null, null));

            _svc = new ComplianceOrchestrationService(
                _kycMock.Object, _amlMock.Object,
                new Mock<ILogger<ComplianceOrchestrationService>>().Object);
        }

        // ─── HP: Happy path ───────────────────────────────────────────────────────

        [Test]
        public async Task HP1_NewUser_KycOnly_ApprovedOnFirstAttempt()
        {
            var result = await InitiateAsync(ComplianceCheckType.Kyc);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(result.DecisionId, Is.Not.Null);
        }

        [Test]
        public async Task HP2_NewUser_AmlOnly_ApprovedOnFirstAttempt()
        {
            var result = await InitiateAsync(ComplianceCheckType.Aml);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task HP3_NewUser_Combined_ApprovedWhenBothPass()
        {
            var result = await InitiateAsync(ComplianceCheckType.Combined);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task HP4_AuditTrail_Contains_At_Least_Two_Events()
        {
            var result = await InitiateAsync(ComplianceCheckType.Combined);
            Assert.That(result.AuditTrail.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task HP5_DecisionHistory_Accessible_Immediately_After_Check()
        {
            var subjectId = $"hp5-{Guid.NewGuid():N}";
            await InitiateAsync(ComplianceCheckType.Aml, subjectId: subjectId);
            var hist = await _svc.GetDecisionHistoryAsync(subjectId);
            Assert.That(hist.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public async Task HP6_GetStatus_Returns_Same_State_As_Initiate()
        {
            var r1 = await InitiateAsync(ComplianceCheckType.Aml);
            var r2 = await _svc.GetCheckStatusAsync(r1.DecisionId!);
            Assert.That(r2.State, Is.EqualTo(r1.State));
        }

        // ─── II: Idempotency invariants ───────────────────────────────────────────

        [Test]
        public async Task II1_SameSubjectContextType_ReturnsSameDecisionId()
        {
            var req = MakeReq(ComplianceCheckType.Aml, "ii1-sub", "ii1-ctx");
            var r1 = await _svc.InitiateCheckAsync(req, "actor", "corr");
            var r2 = await _svc.InitiateCheckAsync(req, "actor", "corr");
            Assert.That(r1.DecisionId, Is.EqualTo(r2.DecisionId));
        }

        [Test]
        public async Task II2_SecondCall_SetsIsIdempotentReplay_True()
        {
            var req = MakeReq(ComplianceCheckType.Aml, "ii2-sub", "ii2-ctx");
            await _svc.InitiateCheckAsync(req, "actor", "corr");
            var r2 = await _svc.InitiateCheckAsync(req, "actor", "corr");
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task II3_FirstCall_IsIdempotentReplay_False()
        {
            var req = MakeReq(ComplianceCheckType.Aml, "ii3-sub", "ii3-ctx");
            var r1 = await _svc.InitiateCheckAsync(req, "actor", "corr");
            Assert.That(r1.IsIdempotentReplay, Is.False);
        }

        [Test]
        public async Task II4_ExplicitIdempotencyKey_Honored()
        {
            var req1 = MakeReq(ComplianceCheckType.Aml, "ii4a-sub", "ii4a-ctx");
            req1.IdempotencyKey = "my-custom-key-ii4";
            var req2 = MakeReq(ComplianceCheckType.Aml, "different-sub", "different-ctx");
            req2.IdempotencyKey = "my-custom-key-ii4";

            var r1 = await _svc.InitiateCheckAsync(req1, "actor", "corr");
            var r2 = await _svc.InitiateCheckAsync(req2, "actor", "corr");
            Assert.That(r1.DecisionId, Is.EqualTo(r2.DecisionId));
        }

        [Test]
        public async Task II5_ProviderCalledOnce_ForThreeReplays()
        {
            var req = MakeReq(ComplianceCheckType.Aml, "ii5-sub", "ii5-ctx");
            for (int i = 0; i < 3; i++)
                await _svc.InitiateCheckAsync(req, "actor", "corr");
            _amlMock.Verify(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()), Times.Once);
        }

        // ─── BD: Business decision correctness ───────────────────────────────────

        [Test]
        public async Task BD1_Rejection_Beats_NeedsReview_In_Combined()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ReturnsAsync(("KYC-NR", KycStatus.NeedsReview, null));
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-REJ", ComplianceDecisionState.Rejected, "SANCTIONS_MATCH", null));

            var result = await InitiateAsync(ComplianceCheckType.Combined);
            Assert.That(result.State, Is.EqualTo(ComplianceDecisionState.Rejected));
        }

        [Test]
        public async Task BD2_KycError_Stops_Pipeline_Does_Not_Approve()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                    .ThrowsAsync(new Exception("KYC provider crashed"));

            var result = await InitiateAsync(ComplianceCheckType.Combined);
            Assert.That(result.State, Is.Not.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task BD3_AmlError_Does_Not_Approve()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("ERR", ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "timeout"));

            var result = await InitiateAsync(ComplianceCheckType.Aml);
            Assert.That(result.State, Is.Not.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task BD4_AuditTrail_First_Event_Is_Initiated()
        {
            var result = await InitiateAsync(ComplianceCheckType.Aml);
            Assert.That(result.AuditTrail[0].EventType, Is.EqualTo("CheckInitiated"));
        }

        // ─── FR: Failure recovery / error taxonomy ────────────────────────────────

        [Test]
        public async Task FR1_Timeout_Maps_To_ComplianceProviderErrorCode_Timeout()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-TO", ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "timeout"));

            var result = await InitiateAsync(ComplianceCheckType.Aml);
            Assert.That(result.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.Timeout));
        }

        [Test]
        public async Task FR2_Unavailable_Maps_To_ComplianceProviderErrorCode_Unavailable()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-UA", ComplianceDecisionState.Error, "PROVIDER_UNAVAILABLE", "unavail"));

            var result = await InitiateAsync(ComplianceCheckType.Aml);
            Assert.That(result.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.ProviderUnavailable));
        }

        [Test]
        public async Task FR3_Malformed_Maps_To_ComplianceProviderErrorCode_Malformed()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync(("AML-MF", ComplianceDecisionState.Error, "MALFORMED_RESPONSE", "parse"));

            var result = await InitiateAsync(ComplianceCheckType.Aml);
            Assert.That(result.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.MalformedResponse));
        }

        [Test]
        public async Task FR4_Exception_Maps_To_ComplianceProviderErrorCode_InternalError()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ThrowsAsync(new InvalidOperationException("boom"));

            var result = await InitiateAsync(ComplianceCheckType.Aml);
            Assert.That(result.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.InternalError));
        }

        [Test]
        public async Task FR5_Error_State_Has_AuditEvent()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ThrowsAsync(new Exception("boom"));

            var result = await InitiateAsync(ComplianceCheckType.Aml);
            Assert.That(result.AuditTrail.Any(e => e.State == ComplianceDecisionState.Error), Is.True);
        }

        // ─── NX: Negative / boundary scenarios ────────────────────────────────────

        [Test]
        public async Task NX1_EmptySubjectId_Returns_Failure()
        {
            var req = new InitiateComplianceCheckRequest { SubjectId = "  ", ContextId = "ctx", CheckType = ComplianceCheckType.Aml };
            var result = await _svc.InitiateCheckAsync(req, "actor", "corr");
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task NX2_EmptyContextId_Returns_Failure()
        {
            var req = new InitiateComplianceCheckRequest { SubjectId = "sub", ContextId = " ", CheckType = ComplianceCheckType.Aml };
            var result = await _svc.InitiateCheckAsync(req, "actor", "corr");
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task NX3_GetStatus_UnknownId_Returns_Failure()
        {
            var result = await _svc.GetCheckStatusAsync("ghost-id-xyz");
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task NX4_GetHistory_NoDecisions_Returns_EmptyList()
        {
            var hist = await _svc.GetDecisionHistoryAsync("no-decisions-ever");
            Assert.That(hist.Decisions, Is.Empty);
            Assert.That(hist.Success, Is.True);
        }

        [Test]
        public async Task NX5_LargeMetadata_Does_Not_Throw()
        {
            var meta = Enumerable.Range(0, 100)
                .ToDictionary(i => $"key_{i}", i => $"value_{i}");
            var req = new InitiateComplianceCheckRequest { SubjectId = $"s-{Guid.NewGuid():N}", ContextId = "ctx-large", CheckType = ComplianceCheckType.Aml, SubjectMetadata = meta };
            var result = await _svc.InitiateCheckAsync(req, "actor", "corr");
            Assert.That(result.Success, Is.True);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private Task<ComplianceCheckResponse> InitiateAsync(
            ComplianceCheckType type,
            string? subjectId = null,
            string? contextId = null)
            => _svc.InitiateCheckAsync(MakeReq(type, subjectId, contextId), "actor", "corr-uj");

        private static InitiateComplianceCheckRequest MakeReq(
            ComplianceCheckType type,
            string? subjectId = null,
            string? contextId = null)
            => new()
            {
                SubjectId = subjectId ?? $"uj-{Guid.NewGuid():N}",
                ContextId = contextId ?? $"ctx-{Guid.NewGuid():N}",
                CheckType = type
            };
    }
}
