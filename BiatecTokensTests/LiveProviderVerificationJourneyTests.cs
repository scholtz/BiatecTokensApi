using BiatecTokensApi.Models.KycAmlSignOff;
using BiatecTokensApi.Models.LiveProviderVerificationJourney;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive tests for LiveProviderVerificationJourneyService covering:
    ///   - StartJourneyAsync: happy paths for LiveProvider, ProtectedSandbox, Simulated modes
    ///   - Fail-closed behaviour: RequireProviderBacked+Simulated, missing KycAmlService
    ///   - Provider-mode distinction: live vs sandbox vs simulated evidence qualification
    ///   - Degraded states: provider unavailable, malformed callback, error outcomes
    ///   - Rejection/adverse-findings paths
    ///   - Approval-decision observability: structured rationale for all journey stages
    ///   - Release evidence generation: RequireProviderBacked guard, content hash, release tag
    ///   - Idempotency: same key returns existing journey
    ///   - Invalid inputs: empty SubjectId
    ///   - GetJourneyStatusAsync: not found, existing journey
    ///   - EvaluateApprovalDecisionAsync: all relevant stage outcomes
    ///   - Webhook emission: VerificationJourneyStarted, Degraded, ApprovalReady, Failed
    ///   - Concurrent journeys: independent records per subject
    ///   - Schema contract: required response fields always populated
    ///   - Determinism: same inputs produce stable evidence shapes
    ///   - Release-grade qualification: provider-backed + all checks passed required
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LiveProviderVerificationJourneyTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Fakes
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class CapturingWebhook : IWebhookService
        {
            public List<WebhookEvent> Events { get; } = new();

            public Task EmitEventAsync(WebhookEvent e)
            {
                lock (Events) Events.Add(e);
                return Task.CompletedTask;
            }

            public Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest r, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string id, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = false });
            public Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string u)
                => Task.FromResult(new WebhookSubscriptionListResponse { Success = true });
            public Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest r, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string id, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest r, string u)
                => Task.FromResult(new WebhookDeliveryHistoryResponse { Success = true });
        }

        /// <summary>
        /// Configurable fake for IKycAmlSignOffEvidenceService.
        /// Controls outcome, provider backing, and approval-readiness for testing.
        /// </summary>
        private sealed class FakeKycAmlService : IKycAmlSignOffEvidenceService
        {
            public bool InitiateSucceeds { get; set; } = true;
            public string? InitiateErrorCode { get; set; }
            public string? InitiateErrorMessage { get; set; }

            public KycAmlSignOffOutcome Outcome { get; set; } = KycAmlSignOffOutcome.Approved;
            public bool IsApprovalReady { get; set; } = true;
            public bool IsProviderBacked { get; set; } = true;
            public KycAmlSignOffExecutionMode ExecutionMode { get; set; } = KycAmlSignOffExecutionMode.ProtectedSandbox;
            public bool ThrowOnInitiate { get; set; }
            public string? KycProviderName { get; set; } = "TestProvider";
            public string? KycProviderReferenceId { get; set; } = "ref-kyc-001";
            public string? ReasonCode { get; set; }

            public Task<InitiateKycAmlSignOffResponse> InitiateSignOffAsync(
                InitiateKycAmlSignOffRequest r, string a, string c)
            {
                if (ThrowOnInitiate)
                    throw new InvalidOperationException("Simulated provider exception");

                if (!InitiateSucceeds)
                    return Task.FromResult(new InitiateKycAmlSignOffResponse
                    {
                        Success = false,
                        ErrorCode = InitiateErrorCode ?? "PROVIDER_ERROR",
                        ErrorMessage = InitiateErrorMessage ?? "Provider initiation failed"
                    });

                var record = new KycAmlSignOffRecord
                {
                    RecordId = $"rec-{Guid.NewGuid():N}",
                    SubjectId = r.SubjectId,
                    ExecutionMode = ExecutionMode,
                    CheckKind = r.CheckKind,
                    Outcome = Outcome,
                    KycProviderName = KycProviderName,
                    KycProviderReferenceId = KycProviderReferenceId,
                    AmlProviderName = KycProviderName,
                    AmlProviderReferenceId = KycProviderReferenceId,
                    ReasonCode = ReasonCode
                };

                return Task.FromResult(new InitiateKycAmlSignOffResponse
                {
                    Success = true,
                    Record = record
                });
            }

            public Task<KycAmlSignOffReadinessResponse> GetReadinessAsync(string recordId) =>
                Task.FromResult(new KycAmlSignOffReadinessResponse
                {
                    RecordId = recordId,
                    IsApprovalReady = IsApprovalReady,
                    IsProviderBacked = IsProviderBacked,
                    ExecutionMode = ExecutionMode,
                    ReadinessState = IsApprovalReady
                        ? KycAmlSignOffReadinessState.Ready
                        : KycAmlSignOffReadinessState.IncompleteEvidence
                });

            public Task<ProcessKycAmlSignOffCallbackResponse> ProcessCallbackAsync(
                string id, ProcessKycAmlSignOffCallbackRequest r, string c) =>
                Task.FromResult(new ProcessKycAmlSignOffCallbackResponse { Success = true });

            public Task<GetKycAmlSignOffRecordResponse> GetRecordAsync(string id) =>
                Task.FromResult(new GetKycAmlSignOffRecordResponse { Success = true });

            public Task<GetKycAmlSignOffArtifactsResponse> GetArtifactsAsync(string id) =>
                Task.FromResult(new GetKycAmlSignOffArtifactsResponse());

            public Task<ListKycAmlSignOffRecordsResponse> ListRecordsForSubjectAsync(string subjectId) =>
                Task.FromResult(new ListKycAmlSignOffRecordsResponse { SubjectId = subjectId });

            public Task<PollKycAmlSignOffStatusResponse> PollProviderStatusAsync(string id, string c) =>
                Task.FromResult(new PollKycAmlSignOffStatusResponse { Success = true });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static LiveProviderVerificationJourneyService CreateService(
            FakeKycAmlService? kycAml = null,
            CapturingWebhook? webhook = null,
            TimeProvider? timeProvider = null) =>
            new(NullLogger<LiveProviderVerificationJourneyService>.Instance,
                kycAml,
                webhook,
                timeProvider);

        private static StartVerificationJourneyRequest MakeRequest(
            string subjectId = "subject-001",
            VerificationJourneyExecutionMode mode = VerificationJourneyExecutionMode.ProtectedSandbox,
            bool requireProviderBacked = false,
            string? idempotencyKey = null) =>
            new()
            {
                SubjectId = subjectId,
                RequestedExecutionMode = mode,
                RequireProviderBacked = requireProviderBacked,
                SubjectMetadata = new Dictionary<string, string>
                {
                    ["full_name"] = "Jane Doe",
                    ["date_of_birth"] = "1985-03-15",
                    ["country"] = "DE"
                },
                IdempotencyKey = idempotencyKey,
                CorrelationId = Guid.NewGuid().ToString()
            };

        // ═══════════════════════════════════════════════════════════════════════
        // 1. Happy path — ApprovalReady
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_LiveProvider_AllChecksPassed_ReturnsApprovalReady()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true,
                ExecutionMode = KycAmlSignOffExecutionMode.LiveProvider
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(
                MakeRequest(mode: VerificationJourneyExecutionMode.LiveProvider), "actor");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Journey, Is.Not.Null);
            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.ApprovalReady));
            Assert.That(r.Journey.IsProviderBacked, Is.True);
        }

        [Test]
        public async Task StartJourney_ProtectedSandbox_AllChecksPassed_ReturnsApprovalReady()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true,
                ExecutionMode = KycAmlSignOffExecutionMode.ProtectedSandbox
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.ApprovalReady));
            Assert.That(r.Journey.ExecutionMode, Is.EqualTo(VerificationJourneyExecutionMode.ProtectedSandbox));
        }

        [Test]
        public async Task StartJourney_Simulated_AllChecksPassed_ReturnsApprovalReady_NotReleaseGrade()
        {
            // Simulated journeys can reach ApprovalReady but must NOT be release-grade
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = false,     // simulated provider is not provider-backed
                ExecutionMode = KycAmlSignOffExecutionMode.Simulated
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(
                MakeRequest(mode: VerificationJourneyExecutionMode.Simulated), "actor");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.ApprovalReady));
            Assert.That(r.Journey.IsProviderBacked, Is.False, "Simulated journey must not be provider-backed");
            Assert.That(r.Journey.IsReleaseGrade, Is.False, "Simulated journey must not be release-grade");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 2. Release-grade qualification
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_LiveProvider_ApprovalReady_IsReleaseGrade()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true,
                ExecutionMode = KycAmlSignOffExecutionMode.LiveProvider
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(
                MakeRequest(mode: VerificationJourneyExecutionMode.LiveProvider), "actor");

            Assert.That(r.Journey!.IsReleaseGrade, Is.True,
                "LiveProvider + approval-ready must be release-grade");
            Assert.That(r.Journey.IsProviderBacked, Is.True);
        }

        [Test]
        public async Task StartJourney_NotApprovalReady_IsNotReleaseGrade()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Pending,
                IsApprovalReady = false,
                IsProviderBacked = true
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Journey!.IsReleaseGrade, Is.False,
                "Non-approval-ready journey must not be release-grade");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 3. Fail-closed: RequireProviderBacked + Simulated
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_RequireProviderBacked_SimulatedMode_Fails()
        {
            var svc = CreateService(new FakeKycAmlService());

            var r = await svc.StartJourneyAsync(
                MakeRequest(mode: VerificationJourneyExecutionMode.Simulated, requireProviderBacked: true),
                "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("SIMULATED_PROVIDER_REJECTED"));
            Assert.That(r.NextAction, Is.Not.Null.And.Not.Empty,
                "Fail-closed response must include actionable guidance");
        }

        [Test]
        public async Task StartJourney_RequireProviderBacked_SimulatedMode_NoJourneyCreated()
        {
            var svc = CreateService(new FakeKycAmlService());

            var r = await svc.StartJourneyAsync(
                MakeRequest(mode: VerificationJourneyExecutionMode.Simulated, requireProviderBacked: true),
                "actor");

            // Journey should NOT be created for a pre-validation failure
            Assert.That(r.Journey, Is.Null,
                "No journey should be created when fail-closed guard fires before DI resolution");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4. Fail-closed: KycAmlService not registered (missing configuration)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_NoKycAmlService_JourneyDegraded_WithDiagnostics()
        {
            // Service created WITHOUT IKycAmlSignOffEvidenceService
            var svc = CreateService(kycAml: null);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Success, Is.False,
                "Journey must fail when KYC/AML service is not registered");
            Assert.That(r.ErrorCode, Is.EqualTo("CONFIGURATION_MISSING"));
            Assert.That(r.Journey, Is.Not.Null, "Degraded journey record must be created");
            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.Degraded));
            Assert.That(r.Diagnostics, Is.Not.Null);
            Assert.That(r.Diagnostics!.IsConfigurationValid, Is.False);
            Assert.That(r.NextAction, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task StartJourney_NoKycAmlService_DiagnosticsHaveActionableGuidance()
        {
            var svc = CreateService(kycAml: null);
            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Diagnostics!.ActionableGuidance, Is.Not.Null.And.Not.Empty,
                "Configuration-missing degraded state must include actionable guidance");
            Assert.That(r.Diagnostics.DegradedStateReason, Is.Not.Null.And.Not.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 5. Degraded states — provider failures
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_ProviderInitiationFails_JourneyDegraded()
        {
            var kycAml = new FakeKycAmlService
            {
                InitiateSucceeds = false,
                InitiateErrorCode = "PROVIDER_TIMEOUT",
                InitiateErrorMessage = "Provider timed out after 30 seconds"
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.Degraded));
            Assert.That(r.Journey.LatestProviderError, Is.Not.Null.And.Not.Empty);
            Assert.That(r.NextAction, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task StartJourney_ProviderUnavailableOutcome_JourneyDegraded()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.ProviderUnavailable,
                IsApprovalReady = false,
                IsProviderBacked = false
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.Degraded),
                "ProviderUnavailable outcome must degrade the journey");
        }

        [Test]
        public async Task StartJourney_MalformedCallbackOutcome_JourneyDegraded()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.MalformedCallback,
                IsApprovalReady = false
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.Degraded));
        }

        [Test]
        public async Task StartJourney_ErrorOutcome_JourneyDegraded()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Error,
                IsApprovalReady = false
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.Degraded));
        }

        [Test]
        public async Task StartJourney_ProviderThrowsException_JourneyDegraded()
        {
            var kycAml = new FakeKycAmlService { ThrowOnInitiate = true };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.Degraded));
            Assert.That(r.ErrorCode, Is.EqualTo("UNEXPECTED_ERROR"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 6. Rejection and adverse-findings paths
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_ProviderRejectsSubject_JourneyRejected()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Rejected,
                IsApprovalReady = false,
                ReasonCode = "IDENTITY_MISMATCH"
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.Rejected));
            Assert.That(r.Journey.IsReleaseGrade, Is.False);
        }

        [Test]
        public async Task StartJourney_AdverseFindings_JourneySanctionsReview()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.AdverseFindings,
                IsApprovalReady = false
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.SanctionsReview),
                "AdverseFindings must route to SanctionsReview for compliance analyst action");
        }

        [Test]
        public async Task StartJourney_BlockedOutcome_JourneySanctionsReview()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Blocked,
                IsApprovalReady = false
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.SanctionsReview));
        }

        [Test]
        public async Task StartJourney_NeedsManualReview_JourneyUnderReview()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.NeedsManualReview,
                IsApprovalReady = false
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.UnderReview),
                "NeedsManualReview must route to UnderReview for analyst action");
        }

        [Test]
        public async Task StartJourney_PendingOutcome_JourneyKycPending()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Pending,
                IsApprovalReady = false
            };
            var svc = CreateService(kycAml);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Journey!.CurrentStage, Is.EqualTo(VerificationJourneyStage.KycPending));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 7. Input validation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_EmptySubjectId_Fails()
        {
            var svc = CreateService(new FakeKycAmlService());

            var r = await svc.StartJourneyAsync(
                MakeRequest(subjectId: ""), "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_SUBJECT_ID"));
        }

        [Test]
        public async Task StartJourney_WhitespaceSubjectId_Fails()
        {
            var svc = CreateService(new FakeKycAmlService());

            var r = await svc.StartJourneyAsync(
                MakeRequest(subjectId: "   "), "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_SUBJECT_ID"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 8. Idempotency
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_SameIdempotencyKey_ReturnsExistingJourney()
        {
            var svc = CreateService(new FakeKycAmlService());
            const string idemKey = "idem-key-001";

            var r1 = await svc.StartJourneyAsync(
                MakeRequest(idempotencyKey: idemKey), "actor");
            var r2 = await svc.StartJourneyAsync(
                MakeRequest(idempotencyKey: idemKey), "actor");

            Assert.That(r1.Journey!.JourneyId, Is.EqualTo(r2.Journey!.JourneyId),
                "Same idempotency key must return the same journey");
        }

        [Test]
        public async Task StartJourney_DifferentIdempotencyKeys_CreateSeparateJourneys()
        {
            var svc = CreateService(new FakeKycAmlService());

            var r1 = await svc.StartJourneyAsync(
                MakeRequest(idempotencyKey: "key-A"), "actor");
            var r2 = await svc.StartJourneyAsync(
                MakeRequest(idempotencyKey: "key-B"), "actor");

            Assert.That(r1.Journey!.JourneyId, Is.Not.EqualTo(r2.Journey!.JourneyId),
                "Different idempotency keys must create separate journeys");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 9. GetJourneyStatusAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetJourneyStatus_UnknownJourneyId_ReturnsFalse()
        {
            var svc = CreateService(new FakeKycAmlService());

            var r = await svc.GetJourneyStatusAsync("nonexistent-id");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorMessage, Is.Not.Null.And.Contains("not found"));
        }

        [Test]
        public async Task GetJourneyStatus_ExistingJourney_ReturnsJourneyAndDecision()
        {
            var svc = CreateService(new FakeKycAmlService());
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.GetJourneyStatusAsync(started.Journey!.JourneyId);

            Assert.That(r.Success, Is.True);
            Assert.That(r.Journey, Is.Not.Null);
            Assert.That(r.ApprovalDecision, Is.Not.Null,
                "GetJourneyStatus must always include an approval decision explanation");
        }

        [Test]
        public async Task GetJourneyStatus_ApprovalReady_DecisionIsApprovalReady()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true
            });
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.GetJourneyStatusAsync(started.Journey!.JourneyId);

            Assert.That(r.ApprovalDecision!.IsApprovalReady, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 10. EvaluateApprovalDecisionAsync — structured rationale
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluateApprovalDecision_UnknownJourneyId_ReturnsFalse()
        {
            var svc = CreateService(new FakeKycAmlService());

            var r = await svc.EvaluateApprovalDecisionAsync("ghost-id");

            Assert.That(r.Success, Is.False);
        }

        [Test]
        public async Task EvaluateApprovalDecision_ApprovalReady_HasApprovalRationale()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true
            });
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.EvaluateApprovalDecisionAsync(started.Journey!.JourneyId);

            Assert.That(r.Success, Is.True);
            Assert.That(r.Decision!.IsApprovalReady, Is.True);
            Assert.That(r.Decision.ApprovalRationale, Is.Not.Null.And.Not.Empty,
                "Approval-ready decision must include rationale for operator display");
            Assert.That(r.Decision.RequiredNextAction, Is.Null,
                "Approval-ready decision must not require further action");
        }

        [Test]
        public async Task EvaluateApprovalDecision_Rejected_HasRejectionReason()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Rejected,
                IsApprovalReady = false,
                ReasonCode = "SANCTIONS_MATCH"
            });
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.EvaluateApprovalDecisionAsync(started.Journey!.JourneyId);

            Assert.That(r.Decision!.RejectionReason, Is.Not.Null.And.Not.Empty,
                "Rejected journey must include rejection reason for operator display");
        }

        [Test]
        public async Task EvaluateApprovalDecision_Degraded_HasRequiredNextAction()
        {
            var svc = CreateService(kycAml: null); // no provider = degraded
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.EvaluateApprovalDecisionAsync(started.Journey!.JourneyId);

            Assert.That(r.Decision!.RequiredNextAction, Is.Not.Null.And.Not.Empty,
                "Degraded journey must include actionable next-step guidance");
        }

        [Test]
        public async Task EvaluateApprovalDecision_SanctionsReview_HasRequiredNextAction()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.AdverseFindings,
                IsApprovalReady = false
            });
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.EvaluateApprovalDecisionAsync(started.Journey!.JourneyId);

            Assert.That(r.Decision!.RequiredNextAction, Is.Not.Null.And.Not.Empty,
                "SanctionsReview journey must include next-action guidance for compliance analyst");
        }

        [Test]
        public async Task EvaluateApprovalDecision_UnderReview_HasRequiredNextAction()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.NeedsManualReview,
                IsApprovalReady = false
            });
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.EvaluateApprovalDecisionAsync(started.Journey!.JourneyId);

            Assert.That(r.Decision!.RequiredNextAction, Is.Not.Null.And.Not.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 11. ApprovalDecision — checks passed/failed/pending lists
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvaluateApprovalDecision_ApprovalReady_HasChecksPassed()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true
            });
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.EvaluateApprovalDecisionAsync(started.Journey!.JourneyId);

            Assert.That(r.Decision!.ChecksPassed, Is.Not.Empty,
                "Approval-ready journey must have at least one passed check");
        }

        [Test]
        public async Task EvaluateApprovalDecision_Rejected_HasChecksFailed()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Rejected,
                IsApprovalReady = false
            });
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.EvaluateApprovalDecisionAsync(started.Journey!.JourneyId);

            Assert.That(r.Decision!.ChecksFailed, Is.Not.Empty,
                "Rejected journey must have at least one failed check");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 12. GenerateReleaseEvidenceAsync — happy paths
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GenerateReleaseEvidence_ProviderBacked_ReturnsReleaseGradeEvidence()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true,
                ExecutionMode = KycAmlSignOffExecutionMode.ProtectedSandbox
            });

            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");
            var journeyId = started.Journey!.JourneyId;

            var r = await svc.GenerateReleaseEvidenceAsync(journeyId,
                new GenerateVerificationJourneyEvidenceRequest
                {
                    RequireProviderBacked = false,
                    ReleaseTag = "v1.2.0-rc1",
                    WorkflowRunReference = "run-12345",
                    CorrelationId = "corr-001"
                }, "actor");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Evidence, Is.Not.Null);
            Assert.That(r.Evidence!.ReleaseTag, Is.EqualTo("v1.2.0-rc1"));
            Assert.That(r.Evidence.WorkflowRunReference, Is.EqualTo("run-12345"));
            Assert.That(r.Evidence.ContentHash, Is.Not.Null.And.Not.Empty,
                "Release evidence must include a content hash for integrity verification");
            Assert.That(r.Evidence.IsProviderBacked, Is.True);
        }

        [Test]
        public async Task GenerateReleaseEvidence_ContentHashIsStable_SameInputsSameHash()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true
            });
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");
            var journeyId = started.Journey!.JourneyId;

            var r1 = await svc.GenerateReleaseEvidenceAsync(journeyId,
                new GenerateVerificationJourneyEvidenceRequest { ReleaseTag = "v1" }, "actor");
            var r2 = await svc.GenerateReleaseEvidenceAsync(journeyId,
                new GenerateVerificationJourneyEvidenceRequest { ReleaseTag = "v1" }, "actor");

            Assert.That(r1.Evidence!.ContentHash, Is.EqualTo(r2.Evidence!.ContentHash),
                "Content hash must be deterministic for the same journey state");
        }

        [Test]
        public async Task GenerateReleaseEvidence_IncludesJourneyStepsAndApprovalDecision()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true
            });
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.GenerateReleaseEvidenceAsync(started.Journey!.JourneyId,
                new GenerateVerificationJourneyEvidenceRequest(), "actor");

            Assert.That(r.Evidence!.Steps, Is.Not.Empty,
                "Release evidence must bundle the journey step audit trail");
            Assert.That(r.Evidence.ApprovalDecision, Is.Not.Null,
                "Release evidence must include the approval decision explanation");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 13. GenerateReleaseEvidenceAsync — fail-closed guard
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GenerateReleaseEvidence_RequireProviderBacked_SimulatedJourney_Fails()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = false,
                ExecutionMode = KycAmlSignOffExecutionMode.Simulated
            };
            var svc = CreateService(kycAml);

            var started = await svc.StartJourneyAsync(
                MakeRequest(mode: VerificationJourneyExecutionMode.Simulated), "actor");

            var r = await svc.GenerateReleaseEvidenceAsync(started.Journey!.JourneyId,
                new GenerateVerificationJourneyEvidenceRequest { RequireProviderBacked = true },
                "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("SIMULATED_EVIDENCE_REJECTED"),
                "RequireProviderBacked must reject simulated evidence for protected release paths");
        }

        [Test]
        public async Task GenerateReleaseEvidence_RequireProviderBacked_ProviderBackedJourney_Succeeds()
        {
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true,
                ExecutionMode = KycAmlSignOffExecutionMode.ProtectedSandbox
            };
            var svc = CreateService(kycAml);

            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.GenerateReleaseEvidenceAsync(started.Journey!.JourneyId,
                new GenerateVerificationJourneyEvidenceRequest { RequireProviderBacked = true },
                "actor");

            Assert.That(r.Success, Is.True,
                "Provider-backed journey must pass RequireProviderBacked guard");
        }

        [Test]
        public async Task GenerateReleaseEvidence_UnknownJourneyId_ReturnsJourneyNotFound()
        {
            var svc = CreateService(new FakeKycAmlService());

            var r = await svc.GenerateReleaseEvidenceAsync("unknown-id",
                new GenerateVerificationJourneyEvidenceRequest(), "actor");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("JOURNEY_NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 14. Schema contract — all required fields populated
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_Success_SchemaContract_RequiredFieldsPopulated()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true
            });

            var r = await svc.StartJourneyAsync(MakeRequest(subjectId: "subject-schema-test"), "actor");

            Assert.That(r.Journey!.JourneyId, Is.Not.Null.And.Not.Empty, "JourneyId must be populated");
            Assert.That(r.Journey.SubjectId, Is.EqualTo("subject-schema-test"), "SubjectId must be echoed");
            Assert.That(r.Journey.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)), "CreatedAt must be set");
            Assert.That(r.Journey.InitiatedBy, Is.Not.Null.And.Not.Empty, "InitiatedBy must be populated");
            Assert.That(r.Journey.CorrelationId, Is.Not.Null.And.Not.Empty, "CorrelationId must be populated");
            Assert.That(r.Journey.Steps, Is.Not.Empty, "Steps audit trail must be populated");
            Assert.That(r.Journey.StageExplanation, Is.Not.Null.And.Not.Empty, "StageExplanation must be populated");
        }

        [Test]
        public async Task StartJourney_Failure_SchemaContract_DiagnosticsAlwaysPopulated()
        {
            var svc = CreateService(kycAml: null);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Diagnostics, Is.Not.Null,
                "Diagnostics must be populated even on failure for operator triage");
            Assert.That(r.ErrorCode, Is.Not.Null.And.Not.Empty, "ErrorCode must be populated on failure");
            Assert.That(r.ErrorMessage, Is.Not.Null.And.Not.Empty, "ErrorMessage must be populated on failure");
        }

        [Test]
        public async Task GenerateReleaseEvidence_Success_SchemaContract_ContentHashPresent()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true
            });
            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");

            var r = await svc.GenerateReleaseEvidenceAsync(started.Journey!.JourneyId,
                new GenerateVerificationJourneyEvidenceRequest(), "actor");

            var e = r.Evidence!;
            Assert.That(e.EvidenceId, Is.Not.Null.And.Not.Empty, "EvidenceId must be populated");
            Assert.That(e.JourneyId, Is.Not.Null.And.Not.Empty, "JourneyId must be populated");
            Assert.That(e.SubjectId, Is.Not.Null.And.Not.Empty, "SubjectId must be populated");
            Assert.That(e.ContentHash, Is.Not.Null.And.Not.Empty, "ContentHash must be populated");
            Assert.That(e.GeneratedAt, Is.Not.EqualTo(default(DateTimeOffset)), "GeneratedAt must be set");
            Assert.That(e.Diagnostics, Is.Not.Null, "Diagnostics must be populated in evidence artifact");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 15. Webhook emission
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_ApprovalReady_EmitsJourneyStartedAndApprovalReadyEvents()
        {
            var webhook = new CapturingWebhook();
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true
            }, webhook);

            await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(webhook.Events.Any(e => e.EventType == WebhookEventType.VerificationJourneyStarted),
                Is.True, "VerificationJourneyStarted must be emitted on successful journey start");
            Assert.That(webhook.Events.Any(e => e.EventType == WebhookEventType.VerificationJourneyApprovalReady),
                Is.True, "VerificationJourneyApprovalReady must be emitted when journey reaches ApprovalReady");
        }

        [Test]
        public async Task StartJourney_NoKycAmlService_EmitsVerificationJourneyDegraded()
        {
            var webhook = new CapturingWebhook();
            var svc = CreateService(kycAml: null, webhook: webhook);

            await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(webhook.Events.Any(e => e.EventType == WebhookEventType.VerificationJourneyDegraded),
                Is.True, "VerificationJourneyDegraded must be emitted on configuration-missing degraded state");
        }

        [Test]
        public async Task StartJourney_ProviderThrows_EmitsVerificationJourneyDegraded()
        {
            var webhook = new CapturingWebhook();
            var svc = CreateService(new FakeKycAmlService { ThrowOnInitiate = true }, webhook);

            await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(webhook.Events.Any(e => e.EventType == WebhookEventType.VerificationJourneyDegraded),
                Is.True);
        }

        [Test]
        public async Task StartJourney_Rejected_EmitsVerificationJourneyFailed()
        {
            var webhook = new CapturingWebhook();
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Rejected,
                IsApprovalReady = false
            }, webhook);

            await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(webhook.Events.Any(e => e.EventType == WebhookEventType.VerificationJourneyFailed),
                Is.True);
        }

        [Test]
        public async Task StartJourney_SanctionsReview_EmitsVerificationJourneyStageAdvanced()
        {
            var webhook = new CapturingWebhook();
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.AdverseFindings,
                IsApprovalReady = false
            }, webhook);

            await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(webhook.Events.Any(e => e.EventType == WebhookEventType.VerificationJourneyStageAdvanced),
                Is.True);
        }

        [Test]
        public async Task StartJourney_WithoutWebhookService_DoesNotThrow()
        {
            // Should silently skip event emission when no webhook service is provided
            var svc = CreateService(new FakeKycAmlService(), webhook: null);

            Assert.DoesNotThrowAsync(async () =>
                await svc.StartJourneyAsync(MakeRequest(), "actor"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 16. Concurrent journeys — independent records per subject
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_ConcurrentDifferentSubjects_AllJourneysIndependent()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true
            });

            const int count = 10;
            var tasks = Enumerable.Range(0, count)
                .Select(i => svc.StartJourneyAsync(MakeRequest(subjectId: $"subject-{i:D3}"), $"actor-{i}"))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.That(results.All(r => r.Success), Is.True,
                "All concurrent journey starts must succeed");

            var journeyIds = results.Select(r => r.Journey!.JourneyId).Distinct().ToList();
            Assert.That(journeyIds, Has.Count.EqualTo(count),
                "Each subject must receive its own independent journey record");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 17. Journey audit trail — steps are recorded
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_ApprovalReady_HasMultipleStepsInAuditTrail()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true
            });

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Journey!.Steps.Count, Is.GreaterThanOrEqualTo(2),
                "Journey audit trail must record at least KYC initiation and completion steps");
        }

        [Test]
        public async Task StartJourney_Steps_OccurredAtIsPopulated()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true
            });

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            foreach (var step in r.Journey!.Steps)
            {
                Assert.That(step.OccurredAt, Is.Not.EqualTo(default(DateTimeOffset)),
                    $"Step '{step.StepName}' must have OccurredAt populated");
                Assert.That(step.StepName, Is.Not.Null.And.Not.Empty,
                    "Each step must have a descriptive name");
                Assert.That(step.Description, Is.Not.Null.And.Not.Empty,
                    "Each step must have a human-readable description");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 18. Diagnostics — active blockers on degraded state
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_Degraded_DiagnosticsHasActiveBlockers()
        {
            var svc = CreateService(kycAml: null);

            var r = await svc.StartJourneyAsync(MakeRequest(), "actor");

            Assert.That(r.Diagnostics!.ActiveBlockers, Is.Not.Empty,
                "Degraded journey diagnostics must include at least one active blocker");
            Assert.That(r.Diagnostics.ActiveBlockers[0].Code, Is.Not.Null.And.Not.Empty);
            Assert.That(r.Diagnostics.ActiveBlockers[0].Description, Is.Not.Null.And.Not.Empty);
            Assert.That(r.Diagnostics.ActiveBlockers[0].Severity, Is.EqualTo(JourneyBlockerSeverity.Critical));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 19. ExecutionMode propagation
        // ═══════════════════════════════════════════════════════════════════════

        [TestCase(VerificationJourneyExecutionMode.LiveProvider)]
        [TestCase(VerificationJourneyExecutionMode.ProtectedSandbox)]
        [TestCase(VerificationJourneyExecutionMode.Simulated)]
        public async Task StartJourney_ExecutionMode_PropagatedToJourneyRecord(
            VerificationJourneyExecutionMode mode)
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = mode != VerificationJourneyExecutionMode.Simulated
            });

            var r = await svc.StartJourneyAsync(MakeRequest(mode: mode), "actor");

            Assert.That(r.Journey!.ExecutionMode, Is.EqualTo(mode),
                $"ExecutionMode must be propagated correctly for {mode}");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 20. Release evidence — provider references
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GenerateReleaseEvidence_ProviderReferenceId_CapturedInEvidence()
        {
            const string providerRef = "sumsub-kyc-ref-abc123";
            var kycAml = new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = true,
                IsProviderBacked = true,
                KycProviderReferenceId = providerRef,
                KycProviderName = "Sumsub"
            };
            var svc = CreateService(kycAml);

            var started = await svc.StartJourneyAsync(MakeRequest(), "actor");
            var r = await svc.GenerateReleaseEvidenceAsync(started.Journey!.JourneyId,
                new GenerateVerificationJourneyEvidenceRequest(), "actor");

            // Provider reference should be traceable in the evidence
            Assert.That(r.Evidence!.ProviderReferences, Is.Not.Null);
            // At least one step should carry the provider name
            Assert.That(r.Evidence.Steps.Any(s => s.ProviderName == "Sumsub"), Is.True,
                "Release evidence steps must include provider name for traceability");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 21. Unknown execution mode maps to Simulated
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartJourney_UnknownExecutionMode_MapsToSimulated()
        {
            var svc = CreateService(new FakeKycAmlService
            {
                Outcome = KycAmlSignOffOutcome.Approved,
                IsApprovalReady = false,  // simulated doesn't produce provider-backed readiness
                IsProviderBacked = false
            });

            var r = await svc.StartJourneyAsync(
                MakeRequest(mode: VerificationJourneyExecutionMode.Unknown), "actor");

            Assert.That(r.Journey!.ExecutionMode, Is.EqualTo(VerificationJourneyExecutionMode.Simulated),
                "Unknown execution mode must resolve to Simulated as the safe default");
        }
    }
}
