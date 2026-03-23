using BiatecTokensApi.Models.KycAmlOnboarding;
using BiatecTokensApi.Models.LiveProviderVerificationJourney;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive tests for KycAmlOnboardingCaseService covering:
    ///   - Case creation: valid, null SubjectId, idempotency, empty metadata
    ///   - Provider check initiation: success (mocked journey), no provider configured, invalid state
    ///   - Reviewer actions: all 5 kinds, invalid transitions, terminal state guard
    ///   - Evidence summary: provider not configured, journey degraded, approved/rejected states
    ///   - List cases: empty, filter by subject, filter by state, pagination
    ///   - Concurrency: multiple threads creating cases for same subject
    ///   - Schema contract: required response fields always populated
    ///   - Webhook emission: fire-and-forget with null service (no throw)
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class KycAmlOnboardingCaseTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Fakes
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class CapturingWebhook : IWebhookService
        {
            public List<WebhookEvent> Events { get; } = new();

            public Task EmitEventAsync(WebhookEvent e) { lock (Events) Events.Add(e); return Task.CompletedTask; }
            public Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest r, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string id, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = false });
            public Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string u) => Task.FromResult(new WebhookSubscriptionListResponse { Success = true });
            public Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest r, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string id, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest r, string u) => Task.FromResult(new WebhookDeliveryHistoryResponse { Success = true });
        }

        /// <summary>Configurable fake for ILiveProviderVerificationJourneyService.</summary>
        private sealed class FakeJourneyService : ILiveProviderVerificationJourneyService
        {
            public bool StartSucceeds { get; set; } = true;
            public VerificationJourneyStage StartStage { get; set; } = VerificationJourneyStage.KycInitiated;
            public bool ThrowOnStart { get; set; }
            public string JourneyId { get; set; } = "journey-test-001";
            public VerificationJourneyStage StatusStage { get; set; } = VerificationJourneyStage.KycInitiated;

            public Task<StartVerificationJourneyResponse> StartJourneyAsync(StartVerificationJourneyRequest request, string actorId)
            {
                if (ThrowOnStart) throw new InvalidOperationException("Simulated provider exception");

                if (!StartSucceeds)
                    return Task.FromResult(new StartVerificationJourneyResponse { Success = false, ErrorCode = "PROVIDER_ERROR" });

                var journey = new VerificationJourneyRecord
                {
                    JourneyId = JourneyId,
                    SubjectId = request.SubjectId,
                    CurrentStage = StartStage
                };
                return Task.FromResult(new StartVerificationJourneyResponse { Success = true, Journey = journey });
            }

            public Task<GetVerificationJourneyStatusResponse> GetJourneyStatusAsync(string journeyId, string? correlationId = null)
            {
                var journey = new VerificationJourneyRecord
                {
                    JourneyId = journeyId,
                    CurrentStage = StatusStage
                };
                return Task.FromResult(new GetVerificationJourneyStatusResponse { Success = true, Journey = journey });
            }

            public Task<EvaluateApprovalDecisionResponse> EvaluateApprovalDecisionAsync(string journeyId, string? correlationId = null)
                => Task.FromResult(new EvaluateApprovalDecisionResponse { Success = true });

            public Task<GenerateVerificationJourneyEvidenceResponse> GenerateReleaseEvidenceAsync(
                string journeyId, GenerateVerificationJourneyEvidenceRequest request, string actorId)
                => Task.FromResult(new GenerateVerificationJourneyEvidenceResponse { Success = true });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static KycAmlOnboardingCaseService CreateService(
            FakeJourneyService? journey = null,
            CapturingWebhook? webhook = null)
        {
            return new KycAmlOnboardingCaseService(
                NullLogger<KycAmlOnboardingCaseService>.Instance,
                journeyService: journey,
                webhookService: webhook);
        }

        private static CreateOnboardingCaseRequest ValidCreateRequest(string? subjectId = null, string? idempotencyKey = null)
            => new CreateOnboardingCaseRequest
            {
                SubjectId = subjectId ?? $"subject-{Guid.NewGuid():N}",
                SubjectKind = KycAmlOnboardingSubjectKind.Individual,
                IdempotencyKey = idempotencyKey,
                CorrelationId = Guid.NewGuid().ToString()
            };

        // ═══════════════════════════════════════════════════════════════════════
        // Case Creation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateCase_ValidRequest_ReturnsSuccess()
        {
            var svc = CreateService();
            var req = ValidCreateRequest();
            var resp = await svc.CreateCaseAsync(req, "actor-1");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case, Is.Not.Null);
            Assert.That(resp.Case!.CaseId, Is.Not.Empty);
            Assert.That(resp.Case.SubjectId, Is.EqualTo(req.SubjectId));
            Assert.That(resp.Case.State, Is.EqualTo(KycAmlOnboardingCaseState.Initiated));
        }

        [Test]
        public async Task CreateCase_NullSubjectId_ReturnsFail()
        {
            var svc = CreateService();
            var req = new CreateOnboardingCaseRequest { SubjectId = "" };
            var resp = await svc.CreateCaseAsync(req, "actor-1");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_SUBJECT_ID"));
        }

        [Test]
        public async Task CreateCase_WhitespaceSubjectId_ReturnsFail()
        {
            var svc = CreateService();
            var req = new CreateOnboardingCaseRequest { SubjectId = "   " };
            var resp = await svc.CreateCaseAsync(req, "actor-1");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_SUBJECT_ID"));
        }

        [Test]
        public async Task CreateCase_WithIdempotencyKey_SecondCallReturnsSameCase()
        {
            var svc = CreateService();
            var req = ValidCreateRequest("subj-idem", "key-001");

            var r1 = await svc.CreateCaseAsync(req, "actor-1");
            var r2 = await svc.CreateCaseAsync(req, "actor-1");

            Assert.That(r1.Success, Is.True);
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.Case!.CaseId, Is.EqualTo(r1.Case!.CaseId));
        }

        [Test]
        public async Task CreateCase_SameSubjectDifferentIdempotencyKey_CreatesTwoCases()
        {
            var svc = CreateService();
            var subjectId = "subj-multi";
            var r1 = await svc.CreateCaseAsync(ValidCreateRequest(subjectId, "key-A"), "actor");
            var r2 = await svc.CreateCaseAsync(ValidCreateRequest(subjectId, "key-B"), "actor");

            Assert.That(r1.Case!.CaseId, Is.Not.EqualTo(r2.Case!.CaseId));
        }

        [Test]
        public async Task CreateCase_WithEmptyMetadata_ReturnsEmptyDictionary()
        {
            var svc = CreateService();
            var req = new CreateOnboardingCaseRequest { SubjectId = "subj-meta", SubjectMetadata = new() };
            var resp = await svc.CreateCaseAsync(req, "actor");

            Assert.That(resp.Case!.SubjectMetadata, Is.Not.Null);
            Assert.That(resp.Case.SubjectMetadata, Is.Empty);
        }

        [Test]
        public async Task CreateCase_WithMetadata_PreservesMetadata()
        {
            var svc = CreateService();
            var req = new CreateOnboardingCaseRequest
            {
                SubjectId = "subj-meta-2",
                SubjectMetadata = new() { ["country"] = "DE", ["tier"] = "retail" }
            };
            var resp = await svc.CreateCaseAsync(req, "actor");

            Assert.That(resp.Case!.SubjectMetadata["country"], Is.EqualTo("DE"));
            Assert.That(resp.Case.SubjectMetadata["tier"], Is.EqualTo("retail"));
        }

        [Test]
        public async Task CreateCase_NoProviderConfigured_IsProviderConfiguredFalse()
        {
            var svc = CreateService(journey: null);
            var resp = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            Assert.That(resp.Case!.IsProviderConfigured, Is.False);
        }

        [Test]
        public async Task CreateCase_ProviderConfigured_IsProviderConfiguredTrue()
        {
            var svc = CreateService(journey: new FakeJourneyService());
            var resp = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            Assert.That(resp.Case!.IsProviderConfigured, Is.True);
        }

        [Test]
        public async Task CreateCase_WithOrganizationName_PreservesName()
        {
            var svc = CreateService();
            var req = new CreateOnboardingCaseRequest
            {
                SubjectId = "subj-org",
                SubjectKind = KycAmlOnboardingSubjectKind.Business,
                OrganizationName = "Acme Corp"
            };
            var resp = await svc.CreateCaseAsync(req, "actor");

            Assert.That(resp.Case!.OrganizationName, Is.EqualTo("Acme Corp"));
            Assert.That(resp.Case.SubjectKind, Is.EqualTo(KycAmlOnboardingSubjectKind.Business));
        }

        [Test]
        public async Task CreateCase_CorrelationIdPropagated()
        {
            var svc = CreateService();
            var corrId = "corr-xyz-123";
            var req = new CreateOnboardingCaseRequest { SubjectId = "subj-corr", CorrelationId = corrId };
            var resp = await svc.CreateCaseAsync(req, "actor");

            Assert.That(resp.CorrelationId, Is.EqualTo(corrId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Get Case
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetCase_ExistingCase_ReturnsCase()
        {
            var svc = CreateService();
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            var got = await svc.GetCaseAsync(created.Case!.CaseId);

            Assert.That(got.Success, Is.True);
            Assert.That(got.Case!.CaseId, Is.EqualTo(created.Case.CaseId));
        }

        [Test]
        public async Task GetCase_NotFound_ReturnsError()
        {
            var svc = CreateService();
            var resp = await svc.GetCaseAsync("nonexistent-case-id");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Initiate Provider Checks
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateProviderChecks_Success_StateBecomesProviderChecksStarted()
        {
            var journey = new FakeJourneyService { StartStage = VerificationJourneyStage.KycInitiated };
            var svc = CreateService(journey: journey);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.InitiateProviderChecksAsync(
                created.Case!.CaseId,
                new InitiateProviderChecksRequest { ExecutionMode = KycAmlOnboardingExecutionMode.LiveProvider },
                "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderChecksStarted));
            Assert.That(resp.VerificationJourneyId, Is.EqualTo(journey.JourneyId));
        }

        [Test]
        public async Task InitiateProviderChecks_NoProvider_ReturnsConfigurationMissing()
        {
            var svc = CreateService(journey: null);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.InitiateProviderChecksAsync(
                created.Case!.CaseId,
                new InitiateProviderChecksRequest(),
                "actor");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("PROVIDER_NOT_CONFIGURED"));
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ConfigurationMissing));
        }

        [Test]
        public async Task InitiateProviderChecks_CaseNotFound_ReturnsError()
        {
            var svc = CreateService(journey: new FakeJourneyService());
            var resp = await svc.InitiateProviderChecksAsync(
                "no-such-case",
                new InitiateProviderChecksRequest(),
                "actor");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"));
        }

        [Test]
        public async Task InitiateProviderChecks_InvalidState_ReturnsError()
        {
            var journey = new FakeJourneyService();
            var svc = CreateService(journey: journey);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            // First call succeeds and moves to ProviderChecksStarted
            await svc.InitiateProviderChecksAsync(created.Case!.CaseId, new InitiateProviderChecksRequest(), "actor");

            // Second call should fail due to invalid state
            var resp = await svc.InitiateProviderChecksAsync(
                created.Case.CaseId,
                new InitiateProviderChecksRequest(),
                "actor");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE"));
        }

        [Test]
        public async Task InitiateProviderChecks_DegradedJourney_StateBecomesProviderUnavailable()
        {
            var journey = new FakeJourneyService { StartStage = VerificationJourneyStage.Degraded };
            var svc = CreateService(journey: journey);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.InitiateProviderChecksAsync(
                created.Case!.CaseId,
                new InitiateProviderChecksRequest(),
                "actor");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderUnavailable));
            Assert.That(resp.ErrorCode, Is.EqualTo("PROVIDER_DEGRADED"));
        }

        [Test]
        public async Task InitiateProviderChecks_ProviderThrows_StateBecomesProviderUnavailable()
        {
            var journey = new FakeJourneyService { ThrowOnStart = true };
            var svc = CreateService(journey: journey);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.InitiateProviderChecksAsync(
                created.Case!.CaseId,
                new InitiateProviderChecksRequest(),
                "actor");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderUnavailable));
            Assert.That(resp.ErrorCode, Is.EqualTo("PROVIDER_ERROR"));
        }

        [Test]
        public async Task InitiateProviderChecks_ProtectedSandboxMode_Succeeds()
        {
            var journey = new FakeJourneyService { StartStage = VerificationJourneyStage.KycInitiated };
            var svc = CreateService(journey: journey);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.InitiateProviderChecksAsync(
                created.Case!.CaseId,
                new InitiateProviderChecksRequest { ExecutionMode = KycAmlOnboardingExecutionMode.ProtectedSandbox },
                "actor");

            Assert.That(resp.Success, Is.True);
        }

        [Test]
        public async Task InitiateProviderChecks_SimulatedMode_Succeeds()
        {
            var journey = new FakeJourneyService { StartStage = VerificationJourneyStage.KycInitiated };
            var svc = CreateService(journey: journey);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.InitiateProviderChecksAsync(
                created.Case!.CaseId,
                new InitiateProviderChecksRequest { ExecutionMode = KycAmlOnboardingExecutionMode.Simulated },
                "actor");

            Assert.That(resp.Success, Is.True);
        }

        [Test]
        public async Task InitiateProviderChecks_RecordsActionInHistory()
        {
            var journey = new FakeJourneyService { StartStage = VerificationJourneyStage.KycInitiated };
            var svc = CreateService(journey: journey);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            await svc.InitiateProviderChecksAsync(created.Case!.CaseId, new InitiateProviderChecksRequest(), "actor-init");

            var got = await svc.GetCaseAsync(created.Case.CaseId);
            Assert.That(got.Case!.Actions, Is.Not.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Reviewer Actions
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a case then directly mutates its state to PendingReview, bypassing
        /// the normal initiate-checks flow. This is intentional: it lets reviewer-action
        /// tests target the state machine without depending on a full journey setup.
        ///
        /// WARNING: Direct state mutation is ONLY acceptable in tests. Production code
        /// must always use the service methods (InitiateProviderChecksAsync,
        /// RecordReviewerActionAsync) to transition case states so business rules
        /// and audit-trail integrity are preserved.
        /// </summary>
        private async Task<KycAmlOnboardingCase> CreateCaseInPendingReview(KycAmlOnboardingCaseService svc)
        {
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            var kycCase = created.Case!;
            // Manually advance to PendingReview for state-transition tests
            kycCase.State = KycAmlOnboardingCaseState.PendingReview;
            return kycCase;
        }

        [Test]
        public async Task RecordReviewerAction_Approve_FromPendingReview_StateBecomesApproved()
        {
            var svc = CreateService();
            var kycCase = await CreateCaseInPendingReview(svc);

            var resp = await svc.RecordReviewerActionAsync(
                kycCase.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve, Rationale = "All checks passed" },
                "reviewer-1");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Approved));
            Assert.That(resp.Action!.Kind, Is.EqualTo(KycAmlOnboardingActionKind.Approve));
            Assert.That(resp.Action.ActorId, Is.EqualTo("reviewer-1"));
        }

        [Test]
        public async Task RecordReviewerAction_Approve_FromUnderReview_StateBecomesApproved()
        {
            var svc = CreateService();
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            created.Case!.State = KycAmlOnboardingCaseState.UnderReview;

            var resp = await svc.RecordReviewerActionAsync(
                created.Case.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve, Rationale = "Approved" },
                "reviewer");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Approved));
        }

        [Test]
        public async Task RecordReviewerAction_Approve_FromInitiated_FailsInvalidTransition()
        {
            var svc = CreateService();
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.RecordReviewerActionAsync(
                created.Case!.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve },
                "reviewer");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task RecordReviewerAction_Reject_FromAnyState_StateBecomesRejected()
        {
            var svc = CreateService();
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.RecordReviewerActionAsync(
                created.Case!.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "Fraudulent" },
                "reviewer");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Rejected));
        }

        [Test]
        public async Task RecordReviewerAction_Escalate_FromPendingReview_StateBecomesEscalated()
        {
            var svc = CreateService();
            var kycCase = await CreateCaseInPendingReview(svc);

            var resp = await svc.RecordReviewerActionAsync(
                kycCase.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Escalate, Rationale = "Needs senior review" },
                "reviewer");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Escalated));
        }

        [Test]
        public async Task RecordReviewerAction_Escalate_FromInitiated_FailsInvalidTransition()
        {
            var svc = CreateService();
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.RecordReviewerActionAsync(
                created.Case!.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Escalate },
                "reviewer");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task RecordReviewerAction_RequestAdditionalInfo_FromPendingReview_StateTransitions()
        {
            var svc = CreateService();
            var kycCase = await CreateCaseInPendingReview(svc);

            var resp = await svc.RecordReviewerActionAsync(
                kycCase.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.RequestAdditionalInfo, Rationale = "Need passport" },
                "reviewer");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.RequiresAdditionalInfo));
        }

        [Test]
        public async Task RecordReviewerAction_RequestAdditionalInfo_FromInitiated_FailsInvalidTransition()
        {
            var svc = CreateService();
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.RecordReviewerActionAsync(
                created.Case!.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.RequestAdditionalInfo },
                "reviewer");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task RecordReviewerAction_AddNote_FromAnyState_NoStateChange()
        {
            var svc = CreateService();
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.RecordReviewerActionAsync(
                created.Case!.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "Subject contacted" },
                "reviewer");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Initiated)); // unchanged
            Assert.That(resp.Action!.Notes, Is.EqualTo("Subject contacted"));
        }

        [Test]
        public async Task RecordReviewerAction_AddNote_OnApprovedCase_Succeeds()
        {
            var svc = CreateService();
            var kycCase = await CreateCaseInPendingReview(svc);
            await svc.RecordReviewerActionAsync(kycCase.CaseId, new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve }, "r1");

            var resp = await svc.RecordReviewerActionAsync(
                kycCase.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "Post-approval note" },
                "auditor");

            Assert.That(resp.Success, Is.True);
        }

        [Test]
        public async Task RecordReviewerAction_ApproveOnTerminalApproved_FailsInvalidTransition()
        {
            var svc = CreateService();
            var kycCase = await CreateCaseInPendingReview(svc);
            await svc.RecordReviewerActionAsync(kycCase.CaseId, new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve, Rationale = "ok" }, "r1");

            var resp = await svc.RecordReviewerActionAsync(
                kycCase.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve },
                "r2");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task RecordReviewerAction_RejectOnTerminalRejected_FailsInvalidTransition()
        {
            var svc = CreateService();
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            await svc.RecordReviewerActionAsync(created.Case!.CaseId, new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "no" }, "r1");

            var resp = await svc.RecordReviewerActionAsync(
                created.Case.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject },
                "r2");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task RecordReviewerAction_CaseNotFound_ReturnsError()
        {
            var svc = CreateService();
            var resp = await svc.RecordReviewerActionAsync(
                "missing-case",
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote },
                "actor");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"));
        }

        [Test]
        public async Task RecordReviewerAction_NullRationale_ActionStillRecorded()
        {
            var svc = CreateService();
            var kycCase = await CreateCaseInPendingReview(svc);

            var resp = await svc.RecordReviewerActionAsync(
                kycCase.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve, Rationale = null },
                "reviewer");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Action!.Rationale, Is.Null);
        }

        [Test]
        public async Task RecordReviewerAction_PersistsActorIdAndTimestamp()
        {
            var svc = CreateService();
            var kycCase = await CreateCaseInPendingReview(svc);

            var before = DateTimeOffset.UtcNow.AddSeconds(-1);
            var resp = await svc.RecordReviewerActionAsync(
                kycCase.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve, Rationale = "ok" },
                "reviewer-xyz");

            Assert.That(resp.Action!.ActorId, Is.EqualTo("reviewer-xyz"));
            Assert.That(resp.Action.Timestamp, Is.GreaterThanOrEqualTo(before));
            Assert.That(resp.Action.ActionId, Is.Not.Empty);
        }

        [Test]
        public async Task RecordReviewerAction_MultipleActions_AllPersisted()
        {
            var svc = CreateService();
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            created.Case!.State = KycAmlOnboardingCaseState.PendingReview;

            await svc.RecordReviewerActionAsync(created.Case.CaseId, new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "Note 1" }, "r1");
            await svc.RecordReviewerActionAsync(created.Case.CaseId, new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "Note 2" }, "r2");
            var last = await svc.RecordReviewerActionAsync(created.Case.CaseId, new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve, Rationale = "ok" }, "r3");

            Assert.That(last.Case!.Actions.Count, Is.GreaterThanOrEqualTo(3));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Evidence Summary
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetEvidenceSummary_NoProvider_MissingConfiguration()
        {
            var svc = CreateService(journey: null);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.GetEvidenceSummaryAsync(created.Case!.CaseId);

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.MissingConfiguration));
            Assert.That(resp.Summary.IsProviderBacked, Is.False);
            Assert.That(resp.Summary.IsReleaseGrade, Is.False);
            Assert.That(resp.Summary.IsProviderConfigured, Is.False);
        }

        [Test]
        public async Task GetEvidenceSummary_NoProvider_HasActionableGuidance()
        {
            var svc = CreateService(journey: null);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.GetEvidenceSummaryAsync(created.Case!.CaseId);

            Assert.That(resp.Summary!.ActionableGuidance, Is.Not.Empty);
        }

        [Test]
        public async Task GetEvidenceSummary_InitiatedWithProvider_PendingVerification()
        {
            var svc = CreateService(journey: new FakeJourneyService());
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            var resp = await svc.GetEvidenceSummaryAsync(created.Case!.CaseId);

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.PendingVerification));
            Assert.That(resp.Summary.IsProviderConfigured, Is.True);
        }

        [Test]
        public async Task GetEvidenceSummary_ApprovedCase_AuthoritativeProviderBacked()
        {
            var journey = new FakeJourneyService
            {
                StartStage = VerificationJourneyStage.KycInitiated,
                StatusStage = VerificationJourneyStage.KycInitiated
            };
            var svc = CreateService(journey: journey);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            created.Case!.State = KycAmlOnboardingCaseState.PendingReview;
            created.Case.VerificationJourneyId = "j-001";

            await svc.RecordReviewerActionAsync(created.Case.CaseId, new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve, Rationale = "ok" }, "r");

            var resp = await svc.GetEvidenceSummaryAsync(created.Case.CaseId);

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.AuthoritativeProviderBacked));
            Assert.That(resp.Summary.IsProviderBacked, Is.True);
            Assert.That(resp.Summary.IsReleaseGrade, Is.True);
        }

        [Test]
        public async Task GetEvidenceSummary_RejectedCase_DegradedPartialEvidence()
        {
            var journey = new FakeJourneyService { StatusStage = VerificationJourneyStage.KycInitiated };
            var svc = CreateService(journey: journey);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            created.Case!.State = KycAmlOnboardingCaseState.PendingReview;
            created.Case.VerificationJourneyId = "j-002";

            await svc.RecordReviewerActionAsync(created.Case.CaseId, new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "failed" }, "r");

            var resp = await svc.GetEvidenceSummaryAsync(created.Case.CaseId);

            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.DegradedPartialEvidence));
            Assert.That(resp.Summary.IsReleaseGrade, Is.False);
        }

        [Test]
        public async Task GetEvidenceSummary_DegradedJourney_ProviderUnavailable()
        {
            var journey = new FakeJourneyService { StatusStage = VerificationJourneyStage.Degraded };
            var svc = CreateService(journey: journey);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            created.Case!.VerificationJourneyId = "j-degraded";

            var resp = await svc.GetEvidenceSummaryAsync(created.Case.CaseId);

            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.ProviderUnavailable));
            Assert.That(resp.Summary.IsProviderBacked, Is.False);
        }

        [Test]
        public async Task GetEvidenceSummary_FailedJourney_ProviderUnavailable()
        {
            var journey = new FakeJourneyService { StatusStage = VerificationJourneyStage.Failed };
            var svc = CreateService(journey: journey);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            created.Case!.VerificationJourneyId = "j-failed";

            var resp = await svc.GetEvidenceSummaryAsync(created.Case.CaseId);

            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.ProviderUnavailable));
        }

        [Test]
        public async Task GetEvidenceSummary_CaseNotFound_ReturnsError()
        {
            var svc = CreateService();
            var resp = await svc.GetEvidenceSummaryAsync("missing-case");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"));
        }

        [Test]
        public async Task GetEvidenceSummary_ConfigurationMissingState_MissingConfiguration()
        {
            var svc = CreateService(journey: new FakeJourneyService());
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            created.Case!.State = KycAmlOnboardingCaseState.ConfigurationMissing;

            var resp = await svc.GetEvidenceSummaryAsync(created.Case.CaseId);

            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.MissingConfiguration));
        }

        [Test]
        public async Task GetEvidenceSummary_ProviderUnavailableState_ProviderUnavailableEvidence()
        {
            var svc = CreateService(journey: new FakeJourneyService());
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            created.Case!.State = KycAmlOnboardingCaseState.ProviderUnavailable;

            var resp = await svc.GetEvidenceSummaryAsync(created.Case.CaseId);

            Assert.That(resp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.ProviderUnavailable));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // List Cases
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ListCases_Empty_ReturnsEmpty()
        {
            var svc = CreateService();
            var resp = await svc.ListCasesAsync();

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Cases, Is.Empty);
            Assert.That(resp.TotalCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ListCases_NullRequest_ReturnsAll()
        {
            var svc = CreateService();
            await svc.CreateCaseAsync(ValidCreateRequest("subj-a"), "actor");
            await svc.CreateCaseAsync(ValidCreateRequest("subj-b"), "actor");

            var resp = await svc.ListCasesAsync(null);

            Assert.That(resp.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public async Task ListCases_FilterBySubjectId_ReturnsMatching()
        {
            var svc = CreateService();
            await svc.CreateCaseAsync(ValidCreateRequest("subj-filter"), "actor");
            await svc.CreateCaseAsync(ValidCreateRequest("subj-other"), "actor");

            var resp = await svc.ListCasesAsync(new ListOnboardingCasesRequest { SubjectId = "subj-filter" });

            Assert.That(resp.TotalCount, Is.EqualTo(1));
            Assert.That(resp.Cases[0].SubjectId, Is.EqualTo("subj-filter"));
        }

        [Test]
        public async Task ListCases_FilterByState_ReturnsMatching()
        {
            var svc = CreateService();
            var c1 = await svc.CreateCaseAsync(ValidCreateRequest("subj-state-1"), "actor");
            var c2 = await svc.CreateCaseAsync(ValidCreateRequest("subj-state-2"), "actor");

            // Reject one
            await svc.RecordReviewerActionAsync(
                c1.Case!.CaseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "no" },
                "r");

            var resp = await svc.ListCasesAsync(new ListOnboardingCasesRequest
            {
                State = KycAmlOnboardingCaseState.Rejected
            });

            Assert.That(resp.TotalCount, Is.EqualTo(1));
            Assert.That(resp.Cases[0].State, Is.EqualTo(KycAmlOnboardingCaseState.Rejected));
        }

        [Test]
        public async Task ListCases_Pagination_ReturnsCorrectPage()
        {
            var svc = CreateService();
            for (int i = 0; i < 5; i++)
                await svc.CreateCaseAsync(ValidCreateRequest($"subj-page-{i}"), "actor");

            var resp = await svc.ListCasesAsync(new ListOnboardingCasesRequest { PageSize = 2 });
            Assert.That(resp.Cases.Count, Is.EqualTo(2));
            Assert.That(resp.TotalCount, Is.EqualTo(5));
        }

        [Test]
        public async Task ListCases_PaginationWithToken_ReturnsNextPage()
        {
            var svc = CreateService();
            for (int i = 0; i < 5; i++)
                await svc.CreateCaseAsync(ValidCreateRequest($"subj-pg2-{i}"), "actor");

            var page1 = await svc.ListCasesAsync(new ListOnboardingCasesRequest { PageSize = 2, PageToken = "0" });
            var page2 = await svc.ListCasesAsync(new ListOnboardingCasesRequest { PageSize = 2, PageToken = "2" });

            Assert.That(page1.Cases.Count, Is.EqualTo(2));
            Assert.That(page2.Cases.Count, Is.EqualTo(2));
            Assert.That(page2.Cases[0].CaseId, Is.Not.EqualTo(page1.Cases[0].CaseId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Concurrency
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateCase_ConcurrentCreations_AllSucceed()
        {
            var svc = CreateService();
            var tasks = Enumerable.Range(0, 20).Select(i =>
                svc.CreateCaseAsync(ValidCreateRequest($"concurrent-subj-{i}"), "actor"));

            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.Success), Is.True);
            Assert.That(results.Select(r => r.Case!.CaseId).Distinct().Count(), Is.EqualTo(20));
        }

        [Test]
        public async Task CreateCase_ConcurrentWithSameIdempotencyKey_ExactlyOneCase()
        {
            var svc = CreateService();
            var req = ValidCreateRequest("concurrent-idem-subj", "shared-key");

            var tasks = Enumerable.Range(0, 10).Select(_ => svc.CreateCaseAsync(req, "actor"));
            var results = await Task.WhenAll(tasks);

            var caseIds = results.Select(r => r.Case!.CaseId).Distinct().ToList();
            Assert.That(caseIds, Has.Count.EqualTo(1));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Schema contract
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateCase_ResponseAlwaysHasCorrelationId()
        {
            var svc = CreateService();
            var resp = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            Assert.That(resp.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ListCases_ResponseAlwaysHasCorrelationId()
        {
            var svc = CreateService();
            var resp = await svc.ListCasesAsync();

            Assert.That(resp.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task CreateCase_CaseIdAlwaysUnique()
        {
            var svc = CreateService();
            var r1 = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            var r2 = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            Assert.That(r1.Case!.CaseId, Is.Not.EqualTo(r2.Case!.CaseId));
        }

        [Test]
        public async Task GetEvidenceSummary_SummaryHasCaseId()
        {
            var svc = CreateService(journey: new FakeJourneyService());
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");
            var resp = await svc.GetEvidenceSummaryAsync(created.Case!.CaseId);

            Assert.That(resp.Summary!.CaseId, Is.EqualTo(created.Case.CaseId));
        }

        [Test]
        public async Task RecordReviewerAction_ActionHasUniqueId()
        {
            var svc = CreateService();
            var kycCase = await CreateCaseInPendingReview(svc);

            var r1 = await svc.RecordReviewerActionAsync(kycCase.CaseId, new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote }, "a");
            var r2 = await svc.RecordReviewerActionAsync(kycCase.CaseId, new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote }, "a");

            Assert.That(r1.Action!.ActionId, Is.Not.EqualTo(r2.Action!.ActionId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Webhook
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateCase_NullWebhook_DoesNotThrow()
        {
            var svc = CreateService(journey: null, webhook: null);
            Assert.DoesNotThrowAsync(async () => await svc.CreateCaseAsync(ValidCreateRequest(), "actor"));
        }

        [Test]
        public async Task InitiateProviderChecks_NullWebhook_DoesNotThrow()
        {
            var journey = new FakeJourneyService { StartStage = VerificationJourneyStage.KycInitiated };
            var svc = CreateService(journey: journey, webhook: null);
            var created = await svc.CreateCaseAsync(ValidCreateRequest(), "actor");

            Assert.DoesNotThrowAsync(async () =>
                await svc.InitiateProviderChecksAsync(created.Case!.CaseId, new InitiateProviderChecksRequest(), "actor"));
        }
    }
}
