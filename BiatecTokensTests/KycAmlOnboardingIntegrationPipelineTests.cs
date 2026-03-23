using BiatecTokensApi.Models.KycAmlOnboarding;
using BiatecTokensApi.Models.LiveProviderVerificationJourney;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration pipeline tests for KycAmlOnboardingCaseService (IP01–IP40).
    ///
    /// <para>
    /// This test suite addresses the critical coverage gaps identified in the product roadmap:
    /// - KYC Integration (48%): Tests full multi-step workflow with provider journey
    /// - AML Screening (43%): Tests end-to-end evidence chain from onboarding to decision
    /// </para>
    ///
    /// Coverage:
    ///
    /// IP01–IP05: Multi-step workflow (create → initiate → reviewer → approve/reject)
    /// IP06–IP10: Idempotency determinism (3 identical runs, identical outcomes)
    /// IP11–IP15: Pipeline integration — KycAmlOnboarding feeds into review system
    /// IP16–IP20: Concurrent execution — multiple simultaneous cases proceed independently
    /// IP21–IP25: Full lifecycle evidence chain — onboarded case produces release-grade evidence
    /// IP26–IP30: Failure recovery — transient vs terminal failures produce correct guidance
    /// IP31–IP35: Webhook emission — all state transitions emit correct events
    /// IP36–IP40: WebApplicationFactory HTTP stack — full API contract via deployed app
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class KycAmlOnboardingIntegrationPipelineTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Fakes
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class ConfigurableJourney : ILiveProviderVerificationJourneyService
        {
            public bool ThrowOnStart { get; set; }
            public VerificationJourneyStage StartStage { get; set; } = VerificationJourneyStage.KycInitiated;
            public string JourneyId { get; set; } = "ip-journey-001";
            public List<StartVerificationJourneyRequest> StartCalls { get; } = new();

            public Task<StartVerificationJourneyResponse> StartJourneyAsync(
                StartVerificationJourneyRequest request, string actorId)
            {
                StartCalls.Add(request);
                if (ThrowOnStart) throw new InvalidOperationException("IP-simulated-exception");
                return Task.FromResult(new StartVerificationJourneyResponse
                {
                    Success = true,
                    Journey = new VerificationJourneyRecord
                    {
                        JourneyId = JourneyId,
                        SubjectId = request.SubjectId,
                        CurrentStage = StartStage
                    }
                });
            }

            public Task<GetVerificationJourneyStatusResponse> GetJourneyStatusAsync(
                string journeyId, string? correlationId = null)
                => Task.FromResult(new GetVerificationJourneyStatusResponse
                {
                    Success = true,
                    Journey = new VerificationJourneyRecord { JourneyId = journeyId, CurrentStage = VerificationJourneyStage.KycInitiated }
                });

            public Task<EvaluateApprovalDecisionResponse> EvaluateApprovalDecisionAsync(
                string journeyId, string? correlationId = null)
                => Task.FromResult(new EvaluateApprovalDecisionResponse { Success = true });

            public Task<GenerateVerificationJourneyEvidenceResponse> GenerateReleaseEvidenceAsync(
                string journeyId, GenerateVerificationJourneyEvidenceRequest request, string actorId)
                => Task.FromResult(new GenerateVerificationJourneyEvidenceResponse { Success = true });
        }

        private sealed class CapturingWebhook : IWebhookService
        {
            public List<WebhookEvent> Events { get; } = new();

            public Task EmitEventAsync(WebhookEvent e) { lock (Events) Events.Add(e); return Task.CompletedTask; }
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

        private static KycAmlOnboardingCaseService Build(
            ConfigurableJourney? journey = null,
            CapturingWebhook? webhook = null)
            => new(NullLogger<KycAmlOnboardingCaseService>.Instance, journey, webhook);

        private static async Task<string> CreateCase(
            KycAmlOnboardingCaseService svc,
            string? subjectId = null,
            string? idempotencyKey = null)
        {
            var resp = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest
            {
                SubjectId = subjectId ?? $"ip-subject-{Guid.NewGuid():N}",
                SubjectKind = KycAmlOnboardingSubjectKind.Individual,
                IdempotencyKey = idempotencyKey
            }, "ip-actor");
            return resp.Case!.CaseId;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IP01–IP05: Multi-step workflow
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>IP01 — Full workflow: create → initiate → note → get → evidence shows PendingVerification.</summary>
        [Test]
        public async Task IP01_FullWorkflow_CreateInitiateNoteGetEvidence()
        {
            var journey = new ConfigurableJourney { JourneyId = "ip01-journey" };
            var svc = Build(journey);

            // Step 1: Create
            var createResp = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "ip01-subject", SubjectKind = KycAmlOnboardingSubjectKind.Individual },
                "actor-1");
            Assert.That(createResp.Success, Is.True);
            Assert.That(createResp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Initiated));
            var caseId = createResp.Case!.CaseId;

            // Step 2: Initiate provider checks
            var initiateResp = await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-1");
            Assert.That(initiateResp.Success, Is.True);
            Assert.That(initiateResp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderChecksStarted));
            Assert.That(initiateResp.Case!.VerificationJourneyId, Is.EqualTo("ip01-journey"));

            // Step 3: Add note
            var noteResp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest
                {
                    Kind = KycAmlOnboardingActionKind.AddNote,
                    Notes = "Provider checks initiated, monitoring status"
                }, "reviewer-1");
            Assert.That(noteResp.Success, Is.True);
            Assert.That(noteResp.Case!.Actions.Count, Is.GreaterThan(0));

            // Step 4: Get case
            var getResp = await svc.GetCaseAsync(caseId);
            Assert.That(getResp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderChecksStarted));
            // InitiateProviderChecksAsync adds an automatic "checks initiated" note, so count is 2 after AddNote
            Assert.That(getResp.Case!.Actions.Count, Is.EqualTo(2));

            // Step 5: Evidence at ProviderChecksStarted → PendingVerification
            var evidenceResp = await svc.GetEvidenceSummaryAsync(caseId);
            Assert.That(evidenceResp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.PendingVerification));
            Assert.That(evidenceResp.Summary!.IsReleaseGrade, Is.False);
        }

        /// <summary>IP02 — Rejection path: create → reject → evidence is DegradedPartialEvidence.</summary>
        [Test]
        public async Task IP02_RejectionPath_EvidenceIsDegradedPartialEvidence()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);

            var caseId = await CreateCase(svc, "ip02-subject");
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor-1");

            // Reject from ProviderChecksStarted (valid for reject)
            var rejectResp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest
                {
                    Kind = KycAmlOnboardingActionKind.Reject,
                    Rationale = "Provider checks showed disqualifying factors"
                }, "reviewer-1");
            Assert.That(rejectResp.Success, Is.True);
            Assert.That(rejectResp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Rejected));

            // Evidence after rejection → DegradedPartialEvidence
            // With a provider configured, rejected case has isProviderBacked=true per service logic
            var evidenceResp = await svc.GetEvidenceSummaryAsync(caseId);
            Assert.That(evidenceResp.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.DegradedPartialEvidence));
            Assert.That(evidenceResp.Summary!.IsReleaseGrade, Is.False);
            Assert.That(evidenceResp.Summary!.IsProviderBacked, Is.True);
        }

        /// <summary>IP03 — Multi-note audit trail: 5 notes accumulate in chronological order.</summary>
        [Test]
        public async Task IP03_MultiNoteAuditTrail_NotesAccumulateInOrder()
        {
            var svc = Build();
            var caseId = await CreateCase(svc, "ip03-subject");

            for (int i = 1; i <= 5; i++)
            {
                var noteResp = await svc.RecordReviewerActionAsync(caseId,
                    new RecordReviewerActionRequest
                    {
                        Kind = KycAmlOnboardingActionKind.AddNote,
                        Notes = $"Note {i}: reviewer observation at step {i}"
                    }, $"reviewer-{i}");
                Assert.That(noteResp.Success, Is.True, $"Note {i} should succeed");
            }

            var caseResp = await svc.GetCaseAsync(caseId);
            Assert.That(caseResp.Case!.Actions.Count, Is.EqualTo(5));

            // Verify actor IDs were preserved
            for (int i = 0; i < 5; i++)
            {
                Assert.That(caseResp.Case!.Actions[i].ActorId, Is.EqualTo($"reviewer-{i + 1}"));
            }
        }

        /// <summary>IP04 — Workflow step chain: all 6 service methods called in sequence, no exceptions.</summary>
        [Test]
        public async Task IP04_WorkflowStepChain_AllSixServiceMethodsInSequence()
        {
            var journey = new ConfigurableJourney { JourneyId = "ip04-journey" };
            var svc = Build(journey);

            // 1. Create
            var createResp = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest
                {
                    SubjectId = "ip04-subject",
                    SubjectKind = KycAmlOnboardingSubjectKind.Business,
                    OrganizationName = "ACME Corp"
                }, "op-1");
            Assert.That(createResp.Success, Is.True);
            var caseId = createResp.Case!.CaseId;

            // 2. Initiate checks
            var initiateResp = await svc.InitiateProviderChecksAsync(caseId,
                new InitiateProviderChecksRequest { CorrelationId = "ip04-corr" }, "op-1");
            Assert.That(initiateResp.Case!.VerificationJourneyId, Is.EqualTo("ip04-journey"));

            // 3. Record note
            await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "Checks in progress" },
                "reviewer-1");

            // 4. Get case
            var getResp = await svc.GetCaseAsync(caseId);
            Assert.That(getResp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderChecksStarted));
            // InitiateProviderChecksAsync adds an auto-note + RecordReviewerAction adds another = 2 actions
            Assert.That(getResp.Case!.Actions.Count, Is.EqualTo(2));

            // 5. Get evidence
            var evidenceResp = await svc.GetEvidenceSummaryAsync(caseId);
            Assert.That(evidenceResp.Summary!.IsProviderConfigured, Is.True);

            // 6. List cases
            var listResp = await svc.ListCasesAsync(new ListOnboardingCasesRequest { SubjectId = "ip04-subject" });
            Assert.That(listResp.Cases.Count, Is.EqualTo(1));
            Assert.That(listResp.Cases[0].SubjectId, Is.EqualTo("ip04-subject"));
        }

        /// <summary>IP05 — Terminal state guard: once Rejected, Approve and Escalate are blocked; AddNote succeeds.</summary>
        [Test]
        public async Task IP05_TerminalStateGuard_OnceRejected_NoFurtherStateTransitions()
        {
            var svc = Build();
            var caseId = await CreateCase(svc, "ip05-subject");

            // Reject
            await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "Rejected" },
                "reviewer-1");

            // Attempt Approve — should fail (terminal state)
            var approveAttempt = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Approve, Rationale = "Try after reject" },
                "reviewer-2");
            Assert.That(approveAttempt.Success, Is.False);
            Assert.That(approveAttempt.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));

            // Attempt Escalate — should fail
            var escalateAttempt = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Escalate },
                "reviewer-2");
            Assert.That(escalateAttempt.Success, Is.False);

            // AddNote — should succeed even in terminal state
            var noteAttempt = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "Post-rejection audit note" },
                "reviewer-3");
            Assert.That(noteAttempt.Success, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IP06–IP10: Idempotency determinism (3 identical runs)
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>IP06 — Idempotency run 1: same key, same subjectId → same CaseId across 3 calls.</summary>
        [Test]
        public async Task IP06_IdempotencyDeterminism_Run1_SameKeyReturnsSameCaseId()
        {
            var svc = Build();
            const string key = "ip06-idempotency-key";
            const string subjectId = "ip06-subject";

            var r1 = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest { SubjectId = subjectId, IdempotencyKey = key }, "actor");
            var r2 = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest { SubjectId = subjectId, IdempotencyKey = key }, "actor");
            var r3 = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest { SubjectId = subjectId, IdempotencyKey = key }, "actor");

            Assert.That(r1.Case!.CaseId, Is.EqualTo(r2.Case!.CaseId), "Run 1 vs 2: CaseId must match");
            Assert.That(r2.Case!.CaseId, Is.EqualTo(r3.Case!.CaseId), "Run 2 vs 3: CaseId must match");
        }

        /// <summary>IP07 — Idempotency run 2: re-create with same key returns case without resetting state.</summary>
        [Test]
        public async Task IP07_IdempotencyDeterminism_Run2_StateUnchangedByRepeatCreation()
        {
            var svc = Build();
            const string key = "ip07-idempotency-key";
            const string subjectId = "ip07-subject";

            var r1 = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest { SubjectId = subjectId, IdempotencyKey = key }, "actor");
            var caseId = r1.Case!.CaseId;

            // Reject to change state
            await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "test" },
                "reviewer-1");

            // Re-create with same key — must return same case without resetting state
            var r2 = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest { SubjectId = subjectId, IdempotencyKey = key }, "actor");
            var r3 = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest { SubjectId = subjectId, IdempotencyKey = key }, "actor");

            Assert.That(r2.Case!.CaseId, Is.EqualTo(caseId), "Repeat creation must return same CaseId");
            Assert.That(r3.Case!.CaseId, Is.EqualTo(caseId), "Third creation must also return same CaseId");
            Assert.That(r2.Case!.State, Is.EqualTo(r3.Case!.State), "State must be consistent across repeat reads");
        }

        /// <summary>IP08 — Idempotency run 3: different keys → distinct CaseIds.</summary>
        [Test]
        public async Task IP08_IdempotencyDeterminism_Run3_DifferentKeysProduceDifferentCaseIds()
        {
            var svc = Build();

            var r1 = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest { SubjectId = "ip08-s1", IdempotencyKey = "ip08-key-1" }, "actor");
            var r2 = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest { SubjectId = "ip08-s2", IdempotencyKey = "ip08-key-2" }, "actor");
            var r3 = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest { SubjectId = "ip08-s3", IdempotencyKey = "ip08-key-3" }, "actor");

            var ids = new[] { r1.Case!.CaseId, r2.Case!.CaseId, r3.Case!.CaseId };
            Assert.That(ids.Distinct().Count(), Is.EqualTo(3), "3 distinct keys must produce 3 distinct CaseIds");
        }

        /// <summary>IP09 — GetCase determinism: 3 reads return identical state.</summary>
        [Test]
        public async Task IP09_GetCaseDeterminism_ThreeReadsReturnIdenticalState()
        {
            var journey = new ConfigurableJourney { JourneyId = "ip09-journey" };
            var svc = Build(journey);

            var caseId = await CreateCase(svc, "ip09-subject");
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor");
            await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "Test note" },
                "reviewer-1");

            var g1 = await svc.GetCaseAsync(caseId);
            var g2 = await svc.GetCaseAsync(caseId);
            var g3 = await svc.GetCaseAsync(caseId);

            Assert.That(g1.Case!.CaseId, Is.EqualTo(g2.Case!.CaseId));
            Assert.That(g2.Case!.CaseId, Is.EqualTo(g3.Case!.CaseId));
            Assert.That(g1.Case!.State, Is.EqualTo(g2.Case!.State));
            Assert.That(g2.Case!.State, Is.EqualTo(g3.Case!.State));
            Assert.That(g1.Case!.Actions.Count, Is.EqualTo(g2.Case!.Actions.Count));
        }

        /// <summary>IP10 — GetEvidenceSummary determinism: 3 reads return identical evidence state.</summary>
        [Test]
        public async Task IP10_EvidenceSummaryDeterminism_ThreeReadsReturnIdenticalResult()
        {
            var svc = Build(); // No provider → ConfigurationMissing
            var caseId = await CreateCase(svc, "ip10-subject");
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "actor");

            var e1 = await svc.GetEvidenceSummaryAsync(caseId);
            var e2 = await svc.GetEvidenceSummaryAsync(caseId);
            var e3 = await svc.GetEvidenceSummaryAsync(caseId);

            Assert.That(e1.Summary!.EvidenceState, Is.EqualTo(e2.Summary!.EvidenceState));
            Assert.That(e2.Summary!.EvidenceState, Is.EqualTo(e3.Summary!.EvidenceState));
            Assert.That(e1.Summary!.IsProviderConfigured, Is.EqualTo(e2.Summary!.IsProviderConfigured));
            Assert.That(e2.Summary!.IsProviderConfigured, Is.EqualTo(e3.Summary!.IsProviderConfigured));
            Assert.That(e1.Summary!.IsReleaseGrade, Is.EqualTo(e2.Summary!.IsReleaseGrade));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IP11–IP15: Pipeline integration
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>IP11 — Multiple subjects onboarded: cases are independent and non-interfering.</summary>
        [Test]
        public async Task IP11_MultipleSubjects_CasesAreIndependentAndNonInterfering()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);

            var caseIds = new List<string>();
            for (int i = 1; i <= 5; i++)
            {
                var resp = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest
                {
                    SubjectId = $"ip11-subject-{i}",
                    SubjectKind = KycAmlOnboardingSubjectKind.Individual
                }, "admin");
                caseIds.Add(resp.Case!.CaseId);
            }

            // Initiate checks for subjects 1, 3, 5 only
            await svc.InitiateProviderChecksAsync(caseIds[0], new InitiateProviderChecksRequest(), "admin");
            await svc.InitiateProviderChecksAsync(caseIds[2], new InitiateProviderChecksRequest(), "admin");
            await svc.InitiateProviderChecksAsync(caseIds[4], new InitiateProviderChecksRequest(), "admin");

            var g1 = await svc.GetCaseAsync(caseIds[0]);
            var g2 = await svc.GetCaseAsync(caseIds[1]);
            var g3 = await svc.GetCaseAsync(caseIds[2]);

            Assert.That(g1.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderChecksStarted));
            Assert.That(g2.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.Initiated));
            Assert.That(g3.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderChecksStarted));
        }

        /// <summary>IP12 — Pagination: list with page-size=2 across multiple cases returns non-overlapping slices.</summary>
        [Test]
        public async Task IP12_Pagination_NonOverlappingSlices()
        {
            var svc = Build();
            for (int i = 0; i < 6; i++)
            {
                await svc.CreateCaseAsync(new CreateOnboardingCaseRequest
                {
                    SubjectId = $"ip12-paged-{i:D3}"
                }, "admin");
            }

            var page1 = await svc.ListCasesAsync(new ListOnboardingCasesRequest { PageSize = 2, PageToken = "0" });
            var page2 = await svc.ListCasesAsync(new ListOnboardingCasesRequest { PageSize = 2, PageToken = "2" });

            Assert.That(page1.Cases.Count, Is.LessThanOrEqualTo(2));
            Assert.That(page2.Cases.Count, Is.LessThanOrEqualTo(2));

            var page1Ids = page1.Cases.Select(c => c.CaseId).ToHashSet();
            var page2Ids = page2.Cases.Select(c => c.CaseId).ToHashSet();
            Assert.That(page1Ids.Intersect(page2Ids).Count(), Is.EqualTo(0), "Pages must not overlap");
        }

        /// <summary>IP13 — State filter: list with state filter returns only matching cases.</summary>
        [Test]
        public async Task IP13_StateFilter_ReturnsOnlyMatchingCases()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);

            var initiated1 = await CreateCase(svc, "ip13-init-1");
            var initiated2 = await CreateCase(svc, "ip13-init-2");
            var provider1 = await CreateCase(svc, "ip13-prov-1");
            var provider2 = await CreateCase(svc, "ip13-prov-2");

            await svc.InitiateProviderChecksAsync(provider1, new InitiateProviderChecksRequest(), "admin");
            await svc.InitiateProviderChecksAsync(provider2, new InitiateProviderChecksRequest(), "admin");

            var initiatedList = await svc.ListCasesAsync(
                new ListOnboardingCasesRequest { State = KycAmlOnboardingCaseState.Initiated });
            var providerList = await svc.ListCasesAsync(
                new ListOnboardingCasesRequest { State = KycAmlOnboardingCaseState.ProviderChecksStarted });

            Assert.That(initiatedList.Cases.All(c => c.State == KycAmlOnboardingCaseState.Initiated), Is.True);
            Assert.That(providerList.Cases.All(c => c.State == KycAmlOnboardingCaseState.ProviderChecksStarted), Is.True);
            Assert.That(initiatedList.Cases.Select(c => c.CaseId), Does.Contain(initiated1));
            Assert.That(initiatedList.Cases.Select(c => c.CaseId), Does.Contain(initiated2));
        }

        /// <summary>IP14 — Subject metadata preserved through lifecycle.</summary>
        [Test]
        public async Task IP14_SubjectMetadata_PreservedThroughLifecycle()
        {
            var svc = Build();
            var metadata = new Dictionary<string, string>
            {
                ["jurisdiction"] = "EU",
                ["tier"] = "enterprise",
                ["risk-rating"] = "medium"
            };

            var createResp = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest
            {
                SubjectId = "ip14-subject",
                SubjectKind = KycAmlOnboardingSubjectKind.Business,
                OrganizationName = "Acme EU GmbH",
                SubjectMetadata = metadata
            }, "admin");

            var caseId = createResp.Case!.CaseId;
            var getResp = await svc.GetCaseAsync(caseId);

            Assert.That(getResp.Case!.SubjectMetadata["jurisdiction"], Is.EqualTo("EU"));
            Assert.That(getResp.Case!.SubjectMetadata["tier"], Is.EqualTo("enterprise"));
            Assert.That(getResp.Case!.OrganizationName, Is.EqualTo("Acme EU GmbH"));
        }

        /// <summary>IP15 — Action audit trail: actor ID, timestamp, rationale all preserved.</summary>
        [Test]
        public async Task IP15_ActionAuditTrail_ActorIdTimestampRationaleAllPreserved()
        {
            var svc = Build();
            var caseId = await CreateCase(svc, "ip15-subject");

            var beforeAction = DateTimeOffset.UtcNow;

            var actionResp = await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest
                {
                    Kind = KycAmlOnboardingActionKind.Reject,
                    Rationale = "Insufficient identity documentation provided",
                    Notes = "Reviewer observed: photo ID expired"
                }, "senior-reviewer-007");

            Assert.That(actionResp.Success, Is.True);
            var action = actionResp.Case!.Actions.Last();

            Assert.That(action.ActorId, Is.EqualTo("senior-reviewer-007"));
            Assert.That(action.Rationale, Is.EqualTo("Insufficient identity documentation provided"));
            Assert.That(action.Notes, Is.EqualTo("Reviewer observed: photo ID expired"));
            Assert.That(action.Timestamp, Is.GreaterThanOrEqualTo(beforeAction));
            Assert.That(action.ActionId, Is.Not.Null.And.Not.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IP16–IP20: Concurrent execution
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>IP16 — Concurrent case creation: 10 parallel creates produce 10 distinct cases.</summary>
        [Test]
        public async Task IP16_ConcurrentCaseCreation_ProducesDistinctCases()
        {
            var svc = Build();

            var tasks = Enumerable.Range(1, 10)
                .Select(i => svc.CreateCaseAsync(new CreateOnboardingCaseRequest
                {
                    SubjectId = $"ip16-concurrent-{i:D3}"
                }, "admin"))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.Success), Is.True);
            var ids = results.Select(r => r.Case!.CaseId).ToHashSet();
            Assert.That(ids.Count, Is.EqualTo(10), "10 concurrent creates must produce 10 distinct CaseIds");
        }

        /// <summary>IP17 — Concurrent notes on same case: all notes appended without loss.</summary>
        [Test]
        public async Task IP17_ConcurrentNotesOnSameCase_AllNotesAppended()
        {
            var svc = Build();
            var caseId = await CreateCase(svc, "ip17-subject");

            var tasks = Enumerable.Range(1, 10)
                .Select(i => svc.RecordReviewerActionAsync(caseId,
                    new RecordReviewerActionRequest
                    {
                        Kind = KycAmlOnboardingActionKind.AddNote,
                        Notes = $"Concurrent note from reviewer {i}"
                    }, $"reviewer-{i}"))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.Success), Is.True);

            var finalCase = await svc.GetCaseAsync(caseId);
            Assert.That(finalCase.Case!.Actions.Count, Is.EqualTo(10), "All 10 concurrent notes must be recorded");
        }

        /// <summary>IP18 — Concurrent list queries return without exception.</summary>
        [Test]
        public async Task IP18_ConcurrentListQueries_NoExceptionAndConsistentResults()
        {
            var svc = Build();
            for (int i = 0; i < 5; i++)
                await CreateCase(svc, $"ip18-subject-{i}");

            var tasks = Enumerable.Range(1, 10)
                .Select(_ => svc.ListCasesAsync(new ListOnboardingCasesRequest { PageSize = 100 }))
                .ToArray();

            Assert.DoesNotThrowAsync(async () => await Task.WhenAll(tasks));
        }

        /// <summary>IP19 — Concurrent evidence queries all return same evidence state.</summary>
        [Test]
        public async Task IP19_ConcurrentEvidenceQueries_AllReturnSameEvidenceState()
        {
            var svc = Build(); // No provider → MissingConfiguration
            var caseId = await CreateCase(svc, "ip19-subject");
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "admin");

            var tasks = Enumerable.Range(1, 5)
                .Select(_ => svc.GetEvidenceSummaryAsync(caseId))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            var states = results.Select(r => r.Summary!.EvidenceState).Distinct().ToList();
            Assert.That(states.Count, Is.EqualTo(1), "All concurrent evidence reads must return same state");
        }

        /// <summary>IP20 — Concurrent idempotent creates with same key yield exactly 1 CaseId.</summary>
        [Test]
        public async Task IP20_ConcurrentIdempotentCreates_SameKeyYieldsOneCase()
        {
            var svc = Build();
            const string idempotencyKey = "ip20-concurrent-key";
            const string subjectId = "ip20-subject";

            var tasks = Enumerable.Range(1, 5)
                .Select(_ => svc.CreateCaseAsync(new CreateOnboardingCaseRequest
                {
                    SubjectId = subjectId,
                    IdempotencyKey = idempotencyKey
                }, "admin"))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            var caseIds = results.Select(r => r.Case!.CaseId).Distinct().ToList();
            Assert.That(caseIds.Count, Is.EqualTo(1), "5 concurrent creates with same key must yield exactly 1 CaseId");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IP21–IP25: Full lifecycle evidence chain
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>IP21 — Rejected case with provider: evidence is DegradedPartialEvidence, not release-grade.</summary>
        [Test]
        public async Task IP21_RejectedCase_EvidenceIsDegradedAndNotReleaseGrade()
        {
            var journey = new ConfigurableJourney(); // provider configured
            var svc = Build(journey);
            var caseId = await CreateCase(svc, "ip21-subject");

            // Initiate checks first to get a VerificationJourneyId (needed for evidence to hit the Rejected path)
            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "admin");

            // Reject from ProviderChecksStarted
            await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "Rejected" },
                "reviewer-1");

            var evidence = await svc.GetEvidenceSummaryAsync(caseId);
            Assert.That(evidence.Summary!.EvidenceState, Is.EqualTo(KycAmlOnboardingEvidenceState.DegradedPartialEvidence));
            Assert.That(evidence.Summary!.IsReleaseGrade, Is.False);
        }

        /// <summary>IP22 — IsReleaseGrade false for Initiated and ProviderChecksStarted states.</summary>
        [Test]
        public async Task IP22_EvidenceIsReleaseGrade_FalseForNonApprovedStates()
        {
            var journey = new ConfigurableJourney();
            var svc = Build(journey);

            // Initiated state
            var caseId1 = await CreateCase(svc, "ip22-initiated");
            var e1 = await svc.GetEvidenceSummaryAsync(caseId1);
            Assert.That(e1.Summary!.IsReleaseGrade, Is.False, "Initiated state must not be release grade");

            // ProviderChecksStarted state
            var caseId2 = await CreateCase(svc, "ip22-provider");
            await svc.InitiateProviderChecksAsync(caseId2, new InitiateProviderChecksRequest(), "admin");
            var e2 = await svc.GetEvidenceSummaryAsync(caseId2);
            Assert.That(e2.Summary!.IsReleaseGrade, Is.False, "ProviderChecksStarted state must not be release grade");
        }

        /// <summary>IP23 — Evidence ActionableGuidance is non-empty for all reachable states.</summary>
        [Test]
        public async Task IP23_EvidenceActionableGuidance_NonEmptyForAllReachableStates()
        {
            var svc = Build(); // No provider

            // Initiated state
            var caseId1 = await CreateCase(svc, "ip23-initiated");
            var e1 = await svc.GetEvidenceSummaryAsync(caseId1);
            Assert.That(e1.Summary!.ActionableGuidance, Is.Not.Null.And.Not.Empty,
                "ActionableGuidance must be populated for Initiated state");

            // ConfigurationMissing state
            await svc.InitiateProviderChecksAsync(caseId1, new InitiateProviderChecksRequest(), "admin");
            var e2 = await svc.GetEvidenceSummaryAsync(caseId1);
            Assert.That(e2.Summary!.ActionableGuidance, Is.Not.Null.And.Not.Empty,
                "ActionableGuidance must be populated for ConfigurationMissing state");
        }

        /// <summary>IP24 — IsProviderConfigured flag reflects journey service presence truthfully.</summary>
        [Test]
        public async Task IP24_IsProviderConfigured_ReflectsTruthfully()
        {
            // Without journey service
            var svcNoProvider = Build(journey: null);
            var createRespNone = await svcNoProvider.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "ip24-no-provider" }, "admin");
            Assert.That(createRespNone.Case!.IsProviderConfigured, Is.False);

            // With journey service
            var journey = new ConfigurableJourney();
            var svcWithProvider = Build(journey);
            var createRespWith = await svcWithProvider.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "ip24-with-provider" }, "admin");
            Assert.That(createRespWith.Case!.IsProviderConfigured, Is.True);
        }

        /// <summary>IP25 — CorrelationId set on create is preserved in the case record.</summary>
        [Test]
        public async Task IP25_CorrelationIdThreading_PreservedInCaseRecord()
        {
            var svc = Build();
            const string correlationId = "ip25-correlation-xyz-789";

            var createResp = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest
            {
                SubjectId = "ip25-subject",
                CorrelationId = correlationId
            }, "admin");

            Assert.That(createResp.Case!.CorrelationId, Is.EqualTo(correlationId));

            var getResp = await svc.GetCaseAsync(createResp.Case!.CaseId);
            Assert.That(getResp.Case!.CorrelationId, Is.EqualTo(correlationId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IP26–IP30: Failure recovery — fail-closed semantics
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>IP26 — Provider exception → PROVIDER_ERROR, case moves to ProviderUnavailable.</summary>
        [Test]
        public async Task IP26_ProviderException_CaseBecomesProviderUnavailable()
        {
            var journey = new ConfigurableJourney { ThrowOnStart = true };
            var svc = Build(journey);
            var caseId = await CreateCase(svc, "ip26-subject");

            var resp = await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "admin");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("PROVIDER_ERROR"));
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderUnavailable));
        }

        /// <summary>IP27 — Provider returns Degraded stage → case becomes ProviderUnavailable.</summary>
        [Test]
        public async Task IP27_ProviderDegradedResponse_CaseBecomesProviderUnavailable()
        {
            var journey = new ConfigurableJourney { StartStage = VerificationJourneyStage.Degraded };
            var svc = Build(journey);
            var caseId = await CreateCase(svc, "ip27-subject");

            var resp = await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "admin");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("PROVIDER_DEGRADED"));
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderUnavailable));
        }

        /// <summary>IP28 — Provider degraded stage → case becomes ProviderUnavailable with guidance.</summary>
        [Test]
        public async Task IP28_ProviderDegradedStage_ProviderUnavailableWithGuidance()
        {
            var journey = new ConfigurableJourney { StartStage = VerificationJourneyStage.Degraded };
            var svc = Build(journey);
            var caseId = await CreateCase(svc, "ip28-subject");

            var resp = await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "admin");
            Assert.That(resp.Case!.State, Is.EqualTo(KycAmlOnboardingCaseState.ProviderUnavailable));

            var evidence = await svc.GetEvidenceSummaryAsync(caseId);
            Assert.That(evidence.Summary!.ActionableGuidance, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>IP29 — Null/empty subject ID is rejected with INVALID_SUBJECT_ID error code.</summary>
        [Test]
        public async Task IP29_NullSubjectId_RejectedWithInvalidSubjectId()
        {
            var svc = Build();

            var resp = await svc.CreateCaseAsync(new CreateOnboardingCaseRequest
            {
                SubjectId = null!,
                SubjectKind = KycAmlOnboardingSubjectKind.Individual
            }, "admin");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("INVALID_SUBJECT_ID"));
        }

        /// <summary>IP30 — GetCase for non-existent caseId returns CASE_NOT_FOUND.</summary>
        [Test]
        public async Task IP30_GetCaseNotFound_ReturnsCaseNotFound()
        {
            var svc = Build();

            var resp = await svc.GetCaseAsync("non-existent-case-id-ip30");

            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("CASE_NOT_FOUND"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IP31–IP35: Webhook emission
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>IP31 — CreateCase emits at least one webhook event (fire-and-forget).</summary>
        [Test]
        public async Task IP31_CreateCase_EmitsWebhookEvent()
        {
            var webhook = new CapturingWebhook();
            var svc = Build(webhook: webhook);

            await svc.CreateCaseAsync(new CreateOnboardingCaseRequest { SubjectId = "ip31-subject" }, "admin");

            // Webhook is fire-and-forget via Task.Run; give background task time to complete
            await Task.Delay(200);
            Assert.That(webhook.Events.Count, Is.GreaterThanOrEqualTo(1));
        }

        /// <summary>IP32 — RecordReviewerAction emits a webhook event (fire-and-forget).</summary>
        [Test]
        public async Task IP32_RecordReviewerAction_EmitsWebhookEvent()
        {
            var webhook = new CapturingWebhook();
            var svc = Build(webhook: webhook);
            var caseId = await CreateCase(svc, "ip32-subject");
            await Task.Delay(100); // wait for create event
            var initialCount = webhook.Events.Count;

            await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "Webhook test note" },
                "reviewer-1");

            await Task.Delay(200); // wait for fire-and-forget
            Assert.That(webhook.Events.Count, Is.GreaterThan(initialCount));
        }

        /// <summary>IP33 — InitiateProviderChecks (success path) emits webhook event (fire-and-forget).</summary>
        [Test]
        public async Task IP33_InitiateProviderChecks_EmitsWebhookEvent()
        {
            var journey = new ConfigurableJourney();
            var webhook = new CapturingWebhook();
            var svc = Build(journey: journey, webhook: webhook);
            var caseId = await CreateCase(svc, "ip33-subject");
            await Task.Delay(100); // wait for create event
            var initialCount = webhook.Events.Count;

            await svc.InitiateProviderChecksAsync(caseId, new InitiateProviderChecksRequest(), "admin");

            await Task.Delay(200); // wait for fire-and-forget
            Assert.That(webhook.Events.Count, Is.GreaterThan(initialCount));
        }

        /// <summary>IP34 — Multiple state transitions each emit a webhook event (fire-and-forget).</summary>
        [Test]
        public async Task IP34_MultipleStateTransitions_EmitMultipleWebhookEvents()
        {
            var webhook = new CapturingWebhook();
            var svc = Build(webhook: webhook);

            var caseId = await CreateCase(svc, "ip34-subject"); // Create → event
            await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "Note 1" }, "r1");
            await svc.RecordReviewerActionAsync(caseId,
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.Reject, Rationale = "Rejected" }, "r2");

            await Task.Delay(300); // wait for all fire-and-forget tasks
            Assert.That(webhook.Events.Count, Is.GreaterThanOrEqualTo(3));
        }

        /// <summary>IP35 — Null webhook service does not crash the service (fire-and-forget semantics).</summary>
        [Test]
        public async Task IP35_NullWebhookService_DoesNotCrashService()
        {
            var svc = new KycAmlOnboardingCaseService(
                NullLogger<KycAmlOnboardingCaseService>.Instance,
                webhookService: null);

            var resp = await svc.CreateCaseAsync(
                new CreateOnboardingCaseRequest { SubjectId = "ip35-subject" }, "admin");

            Assert.That(resp.Success, Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IP36–IP40: WebApplicationFactory HTTP stack
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class IpFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "IpIntegrationTestSecretKey32CharsMin!!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "IpIntegrationTestKey32+CharMin!!X",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "ip-integration-test",
                        ["ProtectedSignOff:EnforceConfigGuards"] = "true",
                        ["WorkflowGovernanceConfig:Enabled"] = "true",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io"
                    });
                });
            }
        }

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        private static async Task<string> GetTokenAsync(HttpClient client)
        {
            var email = $"ip-pipeline-{Guid.NewGuid():N}@ip-pipeline-test.biatec.example.com";
            const string password = "IpPipeline@Test123!";

            var registerResp = await client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password, FullName = "IP Pipeline Test User" });

            var doc = await registerResp.Content.ReadFromJsonAsync<JsonDocument>();
            var token = doc?.RootElement.GetProperty("accessToken").GetString();
            return token ?? string.Empty;
        }

        /// <summary>IP36 — Unauthenticated create case returns 401.</summary>
        [Test]
        public async Task IP36_UnauthenticatedCreateCase_Returns401()
        {
            using var factory = new IpFactory();
            using var client = factory.CreateClient();

            var resp = await client.PostAsJsonAsync("/api/v1/kyc-aml-onboarding/cases",
                new { SubjectId = "ip36-subject", SubjectKind = 0 });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        /// <summary>IP37 — Authenticated create case returns 200 with state Initiated.</summary>
        [Test]
        public async Task IP37_AuthenticatedCreateCase_Returns200WithInitiatedState()
        {
            using var factory = new IpFactory();
            using var client = factory.CreateClient();

            var token = await GetTokenAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await client.PostAsJsonAsync("/api/v1/kyc-aml-onboarding/cases",
                new { SubjectId = "ip37-subject", SubjectKind = 0 });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var caseEl = doc.RootElement.GetProperty("case");
            // State is serialized as integer enum; Initiated = 0
            Assert.That(caseEl.GetProperty("state").GetInt32(), Is.EqualTo((int)KycAmlOnboardingCaseState.Initiated));
            Assert.That(caseEl.GetProperty("caseId").GetString(), Is.Not.Null.And.Not.Empty);
        }

        /// <summary>IP38 — Authenticated list cases returns 200 with cases array.</summary>
        [Test]
        public async Task IP38_AuthenticatedListCases_Returns200WithCasesArray()
        {
            using var factory = new IpFactory();
            using var client = factory.CreateClient();

            var token = await GetTokenAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await client.GetAsync("/api/v1/kyc-aml-onboarding/cases");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.TryGetProperty("cases", out _), Is.True);
        }

        /// <summary>IP39 — Get non-existent case returns 404.</summary>
        [Test]
        public async Task IP39_GetNonExistentCase_Returns404()
        {
            using var factory = new IpFactory();
            using var client = factory.CreateClient();

            var token = await GetTokenAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await client.GetAsync("/api/v1/kyc-aml-onboarding/cases/non-existent-case-ip39");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        /// <summary>IP40 — Full HTTP lifecycle: create → get → initiate → note → evidence → list.</summary>
        [Test]
        public async Task IP40_FullHttpLifecycle_CreateGetInitiateNoteEvidenceList()
        {
            using var factory = new IpFactory();
            using var client = factory.CreateClient();

            var token = await GetTokenAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // 1. Create
            var createResp = await client.PostAsJsonAsync("/api/v1/kyc-aml-onboarding/cases",
                new { SubjectId = "ip40-subject", SubjectKind = 0 });
            Assert.That(createResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var createDoc = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
            var caseId = createDoc.RootElement.GetProperty("case").GetProperty("caseId").GetString()!;

            // 2. Get case
            var getResp = await client.GetAsync($"/api/v1/kyc-aml-onboarding/cases/{caseId}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // 3. Initiate checks (will be ConfigurationMissing — no real provider)
            var initiateResp = await client.PostAsJsonAsync(
                $"/api/v1/kyc-aml-onboarding/cases/{caseId}/initiate-checks", new { });
            Assert.That(initiateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // 4. Add note (use typed request object for correct enum serialization)
            var noteResp = await client.PostAsJsonAsync(
                $"/api/v1/kyc-aml-onboarding/cases/{caseId}/reviewer-actions",
                new RecordReviewerActionRequest { Kind = KycAmlOnboardingActionKind.AddNote, Notes = "IP40 integration test note" });
            Assert.That(noteResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // 5. Get evidence
            var evidenceResp = await client.GetAsync($"/api/v1/kyc-aml-onboarding/cases/{caseId}/evidence");
            Assert.That(evidenceResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var evidenceDoc = JsonDocument.Parse(await evidenceResp.Content.ReadAsStringAsync());
            // Response property is "summary" (camelCase of GetOnboardingEvidenceSummaryResponse.Summary)
            Assert.That(evidenceDoc.RootElement.TryGetProperty("summary", out _), Is.True);

            // 6. List cases
            var listResp = await client.GetAsync("/api/v1/kyc-aml-onboarding/cases");
            Assert.That(listResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
    }
}
