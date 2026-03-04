using BiatecTokensApi.Models;
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
    /// Advanced coverage tests for Issue #474: Enterprise compliance foundation.
    /// Focuses on deterministic error response behaviour, audit log completeness for
    /// compliance reporting, enterprise operator actionability, and all provider
    /// error-code path coverage.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class EnterpriseComplianceOrchestrationIssue474AdvancedCoverageTests
    {
        private Mock<IKycProvider> _kycMock = null!;
        private Mock<IAmlProvider> _amlMock = null!;
        private ComplianceOrchestrationService _svc = null!;
        private const string Actor = "operator-474-adv";

        [SetUp]
        public void Setup()
        {
            _kycMock = new Mock<IKycProvider>();
            _amlMock = new Mock<IAmlProvider>();

            // Default: both approved
            _kycMock.Setup(k => k.StartVerificationAsync(
                    It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                .ReturnsAsync(("KYC-REF-ADV", KycStatus.Approved, (string?)null));

            _amlMock.Setup(a => a.ScreenSubjectAsync(
                    It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-REF-ADV", ComplianceDecisionState.Approved, (string?)null, (string?)null));

            _svc = new ComplianceOrchestrationService(
                _kycMock.Object,
                _amlMock.Object,
                new Mock<ILogger<ComplianceOrchestrationService>>().Object);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static InitiateComplianceCheckRequest MakeRequest(
            string subjectId, ComplianceCheckType type,
            Dictionary<string, string>? meta = null,
            string? idempotencyKey = null) =>
            new()
            {
                SubjectId = subjectId,
                ContextId = $"ctx-{Guid.NewGuid():N}",
                CheckType = type,
                SubjectMetadata = meta ?? new Dictionary<string, string>(),
                IdempotencyKey = idempotencyKey
            };

        // ─── DET-1–6: Deterministic error responses (same condition → identical codes) ─

        [Test]
        public async Task DET1_AmlTimeout_ThreeRuns_AllHaveIdenticalErrorCode()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(
                    It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-TIMEOUT", ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "timeout"));

            var results = new List<ComplianceCheckResponse>();
            for (var i = 0; i < 3; i++)
            {
                var r = await _svc.InitiateCheckAsync(
                    MakeRequest($"det1-subject-{i}", ComplianceCheckType.Aml),
                    Actor, $"corr-det1-{i}");
                results.Add(r);
            }

            foreach (var r in results)
            {
                Assert.That(r.State, Is.EqualTo(ComplianceDecisionState.Error));
                Assert.That(r.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.Timeout));
                Assert.That(r.ReasonCode, Is.EqualTo("PROVIDER_TIMEOUT"));
            }
        }

        [Test]
        public async Task DET2_AmlUnavailable_ThreeRuns_AllHaveIdenticalErrorCode()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(
                    It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-UNAVAIL", ComplianceDecisionState.Error, "PROVIDER_UNAVAILABLE", "unavailable"));

            var results = new List<ComplianceCheckResponse>();
            for (var i = 0; i < 3; i++)
            {
                var r = await _svc.InitiateCheckAsync(
                    MakeRequest($"det2-subject-{i}", ComplianceCheckType.Aml),
                    Actor, $"corr-det2-{i}");
                results.Add(r);
            }

            foreach (var r in results)
            {
                Assert.That(r.State, Is.EqualTo(ComplianceDecisionState.Error));
                Assert.That(r.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.ProviderUnavailable));
                Assert.That(r.ReasonCode, Is.EqualTo("PROVIDER_UNAVAILABLE"));
            }
        }

        [Test]
        public async Task DET3_AmlMalformed_ThreeRuns_AllHaveIdenticalErrorCode()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(
                    It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-MALFORMED", ComplianceDecisionState.Error, "MALFORMED_RESPONSE", "malformed"));

            var results = new List<ComplianceCheckResponse>();
            for (var i = 0; i < 3; i++)
            {
                var r = await _svc.InitiateCheckAsync(
                    MakeRequest($"det3-subject-{i}", ComplianceCheckType.Aml),
                    Actor, $"corr-det3-{i}");
                results.Add(r);
            }

            foreach (var r in results)
            {
                Assert.That(r.State, Is.EqualTo(ComplianceDecisionState.Error));
                Assert.That(r.ProviderErrorCode, Is.EqualTo(ComplianceProviderErrorCode.MalformedResponse));
                Assert.That(r.ReasonCode, Is.EqualTo("MALFORMED_RESPONSE"));
            }
        }

        [Test]
        public async Task DET4_SanctionsMatch_ThreeRuns_AllReturnRejected_WithSanctionsMatchCode()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(
                    It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-SANCTIONS", ComplianceDecisionState.Rejected, "SANCTIONS_MATCH", null));

            var results = new List<ComplianceCheckResponse>();
            for (var i = 0; i < 3; i++)
            {
                var r = await _svc.InitiateCheckAsync(
                    MakeRequest($"det4-subject-{i}", ComplianceCheckType.Aml),
                    Actor, $"corr-det4-{i}");
                results.Add(r);
            }

            foreach (var r in results)
            {
                Assert.That(r.State, Is.EqualTo(ComplianceDecisionState.Rejected));
                Assert.That(r.ReasonCode, Is.EqualTo("SANCTIONS_MATCH"));
                Assert.That(r.Success, Is.True, "Operation success is independent of subject pass/fail");
            }
        }

        [Test]
        public async Task DET5_KycRejection_ThreeRuns_AmlNeverCalled_AllReturnRejected()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(
                    It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                .ReturnsAsync(("KYC-REF-REJECTED", KycStatus.Rejected, (string?)null));

            var results = new List<ComplianceCheckResponse>();
            for (var i = 0; i < 3; i++)
            {
                var r = await _svc.InitiateCheckAsync(
                    MakeRequest($"det5-subject-{i}", ComplianceCheckType.Combined),
                    Actor, $"corr-det5-{i}");
                results.Add(r);
            }

            foreach (var r in results)
                Assert.That(r.State, Is.EqualTo(ComplianceDecisionState.Rejected));

            // AML must never be called when KYC rejects
            _amlMock.Verify(
                a => a.ScreenSubjectAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()),
                Times.Never);
        }

        [Test]
        public async Task DET6_ErrorState_NeverProducesApproved_AllErrorPaths()
        {
            var errorReturnValues = new[]
            {
                ("AML-ERR-1", ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "timeout"),
                ("AML-ERR-2", ComplianceDecisionState.Error, "PROVIDER_UNAVAILABLE", "unavailable"),
                ("AML-ERR-3", ComplianceDecisionState.Error, "MALFORMED_RESPONSE", "malformed"),
            };

            foreach (var (refId, state, reason, msg) in errorReturnValues)
            {
                _amlMock.Setup(a => a.ScreenSubjectAsync(
                        It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                    .ReturnsAsync((refId, state, reason, msg));

                var r = await _svc.InitiateCheckAsync(
                    MakeRequest($"det6-{reason}", ComplianceCheckType.Aml),
                    Actor, $"corr-det6-{reason}");

                Assert.That(r.State, Is.Not.EqualTo(ComplianceDecisionState.Approved),
                    $"Error path '{reason}' must never resolve to Approved");
                Assert.That(r.State, Is.EqualTo(ComplianceDecisionState.Error),
                    $"Error path '{reason}' must resolve to Error");
            }
        }

        // ─── AUD-1–8: Audit log completeness for compliance reporting ─────────────

        [Test]
        public async Task AUD1_AuditEvent_AllRequiredFieldsPresent()
        {
            var r = await _svc.InitiateCheckAsync(
                MakeRequest("aud1-sub", ComplianceCheckType.Combined),
                Actor, "corr-aud1");

            Assert.That(r.AuditTrail, Is.Not.Empty);
            foreach (var evt in r.AuditTrail)
            {
                Assert.That(evt.OccurredAt, Is.Not.EqualTo(default(DateTimeOffset)),
                    $"Event {evt.EventType} must have a non-default OccurredAt timestamp");
                Assert.That(evt.EventType, Is.Not.Null.And.Not.Empty,
                    "EventType must be non-empty");
                Assert.That(evt.CorrelationId, Is.Not.Null.And.Not.Empty,
                    $"Event {evt.EventType} must carry CorrelationId for tracing");
            }
        }

        [Test]
        public async Task AUD2_CombinedApproved_AuditTrail_HasThreeExpectedEvents()
        {
            var r = await _svc.InitiateCheckAsync(
                MakeRequest("aud2-sub", ComplianceCheckType.Combined),
                Actor, "corr-aud2");

            var eventTypes = r.AuditTrail.Select(e => e.EventType).ToList();
            Assert.That(eventTypes, Contains.Item("CheckInitiated"));
            Assert.That(eventTypes, Contains.Item("KycCompleted"));
            Assert.That(eventTypes, Contains.Item("AmlCompleted"));
        }

        [Test]
        public async Task AUD3_CombinedKycReject_AuditTrail_HasAmlSkippedEvent()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(
                    It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                .ReturnsAsync(("KYC-REJECT", KycStatus.Rejected, (string?)null));

            var r = await _svc.InitiateCheckAsync(
                MakeRequest("aud3-sub", ComplianceCheckType.Combined),
                Actor, "corr-aud3");

            var eventTypes = r.AuditTrail.Select(e => e.EventType).ToList();
            Assert.That(eventTypes, Contains.Item("AmlSkipped"),
                "When KYC rejects, AML must be logged as skipped for compliance tracing");
        }

        [Test]
        public async Task AUD4_ErrorPath_AuditTrail_HasCheckErrorEvent_WithMessage()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(
                    It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-ERR", ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "timeout message"));

            var r = await _svc.InitiateCheckAsync(
                MakeRequest("aud4-sub", ComplianceCheckType.Aml),
                Actor, "corr-aud4");

            Assert.That(r.AuditTrail.Any(e => e.EventType == "AmlCompleted"), Is.True);
            var errorEvent = r.AuditTrail.FirstOrDefault(e => e.State == ComplianceDecisionState.Error);
            Assert.That(errorEvent, Is.Not.Null, "At least one audit event must record the Error state");
        }

        [Test]
        public async Task AUD5_AllAuditEvents_CorrelationId_MatchesRequestCorrelationId()
        {
            const string expectedCorr = "corr-aud5-exact-match";
            var r = await _svc.InitiateCheckAsync(
                MakeRequest("aud5-sub", ComplianceCheckType.Combined),
                Actor, expectedCorr);

            foreach (var evt in r.AuditTrail)
            {
                Assert.That(evt.CorrelationId, Is.EqualTo(expectedCorr),
                    $"Event {evt.EventType} CorrelationId must equal the originating request CorrelationId");
            }
        }

        [Test]
        public async Task AUD6_AuditEvents_ChronologicallyOrdered()
        {
            var r = await _svc.InitiateCheckAsync(
                MakeRequest("aud6-sub", ComplianceCheckType.Combined),
                Actor, "corr-aud6");

            var timestamps = r.AuditTrail.Select(e => e.OccurredAt).ToList();
            for (var i = 1; i < timestamps.Count; i++)
            {
                Assert.That(timestamps[i], Is.GreaterThanOrEqualTo(timestamps[i - 1]),
                    $"Audit event at index {i} must not be earlier than event at index {i - 1}");
            }
        }

        [Test]
        public async Task AUD7_HistoryResponse_AllDecisions_HaveNonEmptyAuditTrails()
        {
            const string subjectId = "aud7-compliance-subject";
            // Issue 3 independent checks
            for (var i = 0; i < 3; i++)
            {
                await _svc.InitiateCheckAsync(
                    new InitiateComplianceCheckRequest
                    {
                        SubjectId = subjectId,
                        ContextId = $"ctx-aud7-{i}",
                        CheckType = ComplianceCheckType.Combined
                    }, Actor, $"corr-aud7-{i}");
            }

            var history = await _svc.GetDecisionHistoryAsync(subjectId);
            Assert.That(history.TotalCount, Is.EqualTo(3));
            foreach (var d in history.Decisions)
            {
                Assert.That(d.AuditTrail, Is.Not.Empty,
                    "Each historical decision must carry its full audit trail for compliance review");
            }
        }

        [Test]
        public async Task AUD8_AuditTrail_KycCompleted_ReferencesProviderRefId()
        {
            _kycMock.Setup(k => k.StartVerificationAsync(
                    It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()))
                .ReturnsAsync(("KYC-KNOWN-REF-123", KycStatus.Approved, (string?)null));

            var r = await _svc.InitiateCheckAsync(
                MakeRequest("aud8-sub", ComplianceCheckType.Kyc),
                Actor, "corr-aud8");

            var kycEvent = r.AuditTrail.FirstOrDefault(e => e.EventType == "KycCompleted");
            Assert.That(kycEvent, Is.Not.Null);
            Assert.That(kycEvent!.ProviderReferenceId, Is.EqualTo("KYC-KNOWN-REF-123"),
                "KycCompleted event must embed the provider reference ID for compliance audit linkage");
        }

        // ─── ENT-1–8: Enterprise operator actionability ────────────────────────────

        [Test]
        public void ENT1_ErrorCode_Timeout_Constant_Is_Stable()
        {
            // Error codes must be stable strings (never change between releases)
            Assert.That(ErrorCodes.AML_PROVIDER_TIMEOUT, Is.EqualTo("AML_PROVIDER_TIMEOUT"));
        }

        [Test]
        public void ENT2_ErrorCode_Unavailable_Constant_Is_Stable()
        {
            Assert.That(ErrorCodes.AML_PROVIDER_UNAVAILABLE, Is.EqualTo("AML_PROVIDER_UNAVAILABLE"));
        }

        [Test]
        public void ENT3_ErrorCode_Malformed_Constant_Is_Stable()
        {
            Assert.That(ErrorCodes.AML_MALFORMED_RESPONSE, Is.EqualTo("AML_MALFORMED_RESPONSE"));
        }

        [Test]
        public void ENT4_ErrorCode_SanctionsRejected_Constant_Is_Stable()
        {
            Assert.That(ErrorCodes.AML_SCREENING_REJECTED, Is.EqualTo("AML_SCREENING_REJECTED"));
        }

        [Test]
        public void ENT5_ErrorCode_NeedsReview_Constant_Is_Stable()
        {
            Assert.That(ErrorCodes.AML_SCREENING_NEEDS_REVIEW, Is.EqualTo("AML_SCREENING_NEEDS_REVIEW"));
        }

        [Test]
        public void ENT6_ErrorCode_DecisionNotFound_Constant_Is_Stable()
        {
            Assert.That(ErrorCodes.COMPLIANCE_CHECK_NOT_FOUND, Is.EqualTo("COMPLIANCE_CHECK_NOT_FOUND"));
        }

        [Test]
        public void ENT7_ErrorCode_IdempotencyMismatch_Constant_Is_Stable()
        {
            Assert.That(ErrorCodes.COMPLIANCE_IDEMPOTENCY_KEY_MISMATCH, Is.EqualTo("COMPLIANCE_IDEMPOTENCY_KEY_MISMATCH"));
        }

        [Test]
        public async Task ENT8_GetStatus_UnknownDecisionId_Returns_ActionableErrorCode()
        {
            var r = await _svc.GetCheckStatusAsync("unknown-decision-id-xyz");
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo(ErrorCodes.COMPLIANCE_CHECK_NOT_FOUND),
                "Unknown decision must return the stable COMPLIANCE_CHECK_NOT_FOUND error code so operators can distinguish 'not found' from server error");
        }

        // ─── COMP-1–6: Compliance reporting workflow completeness ─────────────────

        [Test]
        public async Task COMP1_Decision_SubjectId_Preserved_In_History()
        {
            const string subjectId = "comp1-regulated-entity";
            await _svc.InitiateCheckAsync(
                MakeRequest(subjectId, ComplianceCheckType.Combined),
                Actor, "corr-comp1");

            var history = await _svc.GetDecisionHistoryAsync(subjectId);
            Assert.That(history.SubjectId, Is.EqualTo(subjectId));
            Assert.That(history.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public async Task COMP2_Decision_InitiatedAt_IsUtcAndRecent()
        {
            var before = DateTimeOffset.UtcNow;
            var r = await _svc.InitiateCheckAsync(
                MakeRequest("comp2-sub", ComplianceCheckType.Combined),
                Actor, "corr-comp2");
            var after = DateTimeOffset.UtcNow;

            Assert.That(r.InitiatedAt, Is.Not.Null);
            Assert.That(r.InitiatedAt!.Value, Is.GreaterThanOrEqualTo(before).And.LessThanOrEqualTo(after));
        }

        [Test]
        public async Task COMP3_Decision_CompletedAt_Set_ForApproved()
        {
            var r = await _svc.InitiateCheckAsync(
                MakeRequest("comp3-sub", ComplianceCheckType.Combined),
                Actor, "corr-comp3");

            Assert.That(r.State, Is.EqualTo(ComplianceDecisionState.Approved));
            Assert.That(r.CompletedAt, Is.Not.Null, "Approved decisions must have CompletedAt for SLA reporting");
            Assert.That(r.CompletedAt!.Value, Is.GreaterThanOrEqualTo(r.InitiatedAt!.Value));
        }

        [Test]
        public async Task COMP4_Decision_CompletedAt_Set_ForRejected()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(
                    It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-REJ", ComplianceDecisionState.Rejected, "SANCTIONS_MATCH", null));

            var r = await _svc.InitiateCheckAsync(
                MakeRequest("comp4-sub", ComplianceCheckType.Aml),
                Actor, "corr-comp4");

            Assert.That(r.State, Is.EqualTo(ComplianceDecisionState.Rejected));
            Assert.That(r.CompletedAt, Is.Not.Null, "Rejected decisions must have CompletedAt for compliance timeline");
        }

        [Test]
        public async Task COMP5_Decision_CompletedAt_Set_ForError()
        {
            _amlMock.Setup(a => a.ScreenSubjectAsync(
                    It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-ERR", ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "timeout"));

            var r = await _svc.InitiateCheckAsync(
                MakeRequest("comp5-sub", ComplianceCheckType.Aml),
                Actor, "corr-comp5");

            Assert.That(r.State, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(r.CompletedAt, Is.Not.Null, "Error decisions must have CompletedAt for incident response timeline");
        }

        [Test]
        public async Task COMP6_MultipleChecks_SameSubject_HistoryPreservesAllDecisions()
        {
            const string subjectId = "comp6-repeat-subject";
            // Simulate a re-check after provider error: first fails, second succeeds
            _amlMock.SetupSequence(a => a.ScreenSubjectAsync(
                    It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<string>()))
                .ReturnsAsync(("AML-ERR", ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "timeout"))
                .ReturnsAsync(("AML-OK", ComplianceDecisionState.Approved, null, null));

            // First check (fails)
            await _svc.InitiateCheckAsync(new InitiateComplianceCheckRequest
            {
                SubjectId = subjectId,
                ContextId = "ctx-comp6-first",
                CheckType = ComplianceCheckType.Aml
            }, Actor, "corr-comp6-first");

            // Second check (succeeds, different context = new decision)
            await _svc.InitiateCheckAsync(new InitiateComplianceCheckRequest
            {
                SubjectId = subjectId,
                ContextId = "ctx-comp6-second",
                CheckType = ComplianceCheckType.Aml
            }, Actor, "corr-comp6-second");

            var history = await _svc.GetDecisionHistoryAsync(subjectId);
            Assert.That(history.TotalCount, Is.EqualTo(2),
                "Both decisions (error and retry) must appear in history for compliance audit trail");

            var states = history.Decisions.Select(d => d.State).ToHashSet();
            Assert.That(states, Contains.Item(ComplianceDecisionState.Error));
            Assert.That(states, Contains.Item(ComplianceDecisionState.Approved));
        }

        // ─── MOCK-1–4: MockAmlProvider flag-driven path coverage ─────────────────

        [Test]
        public async Task MOCK1_MockAmlProvider_SanctionsFlag_Returns_Rejected_WithSanctionsMatch()
        {
            var provider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "mock1-sub",
                new Dictionary<string, string> { ["sanctions_flag"] = "true" },
                "corr-mock1");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Rejected));
            Assert.That(reasonCode, Is.EqualTo("SANCTIONS_MATCH"));
        }

        [Test]
        public async Task MOCK2_MockAmlProvider_ReviewFlag_Returns_NeedsReview_WithReviewRequired()
        {
            var provider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var (_, state, reasonCode, _) = await provider.ScreenSubjectAsync(
                "mock2-sub",
                new Dictionary<string, string> { ["review_flag"] = "true" },
                "corr-mock2");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.NeedsReview));
            Assert.That(reasonCode, Is.EqualTo("REVIEW_REQUIRED"));
        }

        [Test]
        public async Task MOCK3_MockAmlProvider_TimeoutFlag_Returns_Error_WithProviderTimeout()
        {
            var provider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var (_, state, reasonCode, errorMsg) = await provider.ScreenSubjectAsync(
                "mock3-sub",
                new Dictionary<string, string> { ["simulate_timeout"] = "true" },
                "corr-mock3");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(reasonCode, Is.EqualTo("PROVIDER_TIMEOUT"));
            Assert.That(errorMsg, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task MOCK4_MockAmlProvider_UnavailableFlag_Returns_Error_WithProviderUnavailable()
        {
            var provider = new MockAmlProvider(new Mock<ILogger<MockAmlProvider>>().Object);
            var (_, state, reasonCode, errorMsg) = await provider.ScreenSubjectAsync(
                "mock4-sub",
                new Dictionary<string, string> { ["simulate_unavailable"] = "true" },
                "corr-mock4");

            Assert.That(state, Is.EqualTo(ComplianceDecisionState.Error));
            Assert.That(reasonCode, Is.EqualTo("PROVIDER_UNAVAILABLE"));
            Assert.That(errorMsg, Is.Not.Null.And.Not.Empty);
        }

        // ─── IDEM-1–3: Idempotency edge cases ────────────────────────────────────

        [Test]
        public async Task IDEM1_ThreeReplays_AllReturnIdenticalDecisionId()
        {
            const string key = "idem1-unique-key-xyz";
            var req = MakeRequest("idem1-sub", ComplianceCheckType.Kyc, idempotencyKey: key);

            var r1 = await _svc.InitiateCheckAsync(req, Actor, "corr-idem1-1");
            // Override key for subsequent calls (same idempotency key, different auto-generated ContextId would differ)
            var r2 = await _svc.InitiateCheckAsync(
                MakeRequest("idem1-sub", ComplianceCheckType.Kyc, idempotencyKey: key),
                Actor, "corr-idem1-2");
            var r3 = await _svc.InitiateCheckAsync(
                MakeRequest("idem1-sub", ComplianceCheckType.Kyc, idempotencyKey: key),
                Actor, "corr-idem1-3");

            Assert.That(r2.DecisionId, Is.EqualTo(r1.DecisionId));
            Assert.That(r3.DecisionId, Is.EqualTo(r1.DecisionId));
        }

        [Test]
        public async Task IDEM2_SecondReplay_MarkedIsIdempotentReplay()
        {
            const string key = "idem2-unique-key-xyz";
            await _svc.InitiateCheckAsync(
                MakeRequest("idem2-sub", ComplianceCheckType.Kyc, idempotencyKey: key),
                Actor, "corr-idem2-1");

            var r2 = await _svc.InitiateCheckAsync(
                MakeRequest("idem2-sub", ComplianceCheckType.Kyc, idempotencyKey: key),
                Actor, "corr-idem2-2");

            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task IDEM3_IdempotencyKey_ProviderCalledOnceAcrossThreeReplays()
        {
            const string key = "idem3-unique-key-xyz";

            for (var i = 0; i < 3; i++)
            {
                await _svc.InitiateCheckAsync(
                    MakeRequest("idem3-sub", ComplianceCheckType.Kyc, idempotencyKey: key),
                    Actor, $"corr-idem3-{i}");
            }

            // KYC provider should be called exactly once regardless of replay count
            _kycMock.Verify(
                k => k.StartVerificationAsync(
                    It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()),
                Times.Once,
                "KYC provider must be called exactly once for idempotent replays — no duplicate charges");
        }
    }
}
