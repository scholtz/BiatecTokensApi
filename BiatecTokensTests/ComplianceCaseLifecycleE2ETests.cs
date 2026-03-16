using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive end-to-end lifecycle tests for the compliance case management,
    /// approval workflow, and webhook orchestration surface.
    ///
    /// Valid state machine transitions:
    ///   Intake → EvidencePending | Blocked
    ///   EvidencePending → UnderReview | Stale | Blocked
    ///   UnderReview → Approved | Rejected | Escalated | Remediating | Blocked
    ///   Escalated → UnderReview | Rejected | Blocked
    ///   Remediating → UnderReview | Approved | Rejected | Blocked
    ///   Stale → EvidencePending | Rejected | Blocked
    ///   Blocked → Intake
    ///   Approved / Rejected → (terminal — no outbound transitions)
    ///
    /// These tests validate the complete enterprise compliance lifecycle:
    ///   - Case creation → evidence pending → review → escalation → remediation/rework → approval/rejection
    ///   - Semantic webhook events emitted at each critical milestone
    ///   - Fail-closed behavior for invalid state transitions
    ///   - Rework path: reviewer requests changes before re-evaluation
    ///   - Approval and denial with audit trail
    ///   - Evidence-requested event on entry to EvidencePending state
    ///   - Decision lineage for KYC, AML, and approval outcomes
    ///   - Handoff status tracking after approval
    ///   - Case blockers and SLA urgency bands
    ///   - Idempotency and re-opening behavior after terminal states
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceCaseLifecycleE2ETests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Shared fakes
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class FakeTime : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTime(DateTimeOffset start) => _now = start;
            public void Advance(TimeSpan d) => _now = _now.Add(d);
            public override DateTimeOffset GetUtcNow() => _now;
        }

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

        private static ComplianceCaseManagementService CreateService(
            CapturingWebhook? webhook = null,
            IComplianceCaseRepository? repo = null,
            FakeTime? time = null) =>
            new(NullLogger<ComplianceCaseManagementService>.Instance,
                timeProvider: time,
                defaultEvidenceValidity: null,
                webhookService: webhook,
                repository: repo);

        private static CreateComplianceCaseRequest NewCase(
            string issuerId = "issuer-1",
            string subjectId = "subject-1",
            CaseType type = CaseType.InvestorEligibility) =>
            new() { IssuerId = issuerId, SubjectId = subjectId, Type = type };

        private static async Task<string> CreateCaseAndGetId(
            ComplianceCaseManagementService svc,
            CreateComplianceCaseRequest? req = null)
        {
            var r = await svc.CreateCaseAsync(req ?? NewCase(), "actor-1");
            Assert.That(r.Success, Is.True, r.ErrorMessage);
            return r.Case!.CaseId;
        }

        /// <summary>Helper: advance case to UnderReview via the valid path (Intake → EvidencePending → UnderReview).</summary>
        private static async Task AdvanceToUnderReview(ComplianceCaseManagementService svc, string caseId)
        {
            var r1 = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "actor-system");
            Assert.That(r1.Success, Is.True, $"Intake→EvidencePending failed: {r1.ErrorMessage}");

            var r2 = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "actor-system");
            Assert.That(r2.Success, Is.True, $"EvidencePending→UnderReview failed: {r2.ErrorMessage}");
        }

        private static async Task PollForEvent(
            CapturingWebhook webhook,
            WebhookEventType type,
            int count = 1,
            int maxWaitMs = 3000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            while (DateTime.UtcNow < deadline)
            {
                int actual;
                lock (webhook.Events) actual = webhook.Events.Count(e => e.EventType == type);
                if (actual >= count) return;
                await Task.Delay(20);
            }
            int final;
            lock (webhook.Events) final = webhook.Events.Count(e => e.EventType == type);
            Assert.Fail($"Expected {count} event(s) of type {type} but found {final} after {maxWaitMs}ms.");
        }

        private static string SerializeData(Dictionary<string, object>? data) =>
            data == null ? "{}" : JsonSerializer.Serialize(data);

        // ═══════════════════════════════════════════════════════════════════════
        // 1. Full happy-path lifecycle: creation → evidence → review → approval
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task FullLifecycle_CreateToApproval_EmitsAllMilestoneEvents()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            // Create
            var caseId = await CreateCaseAndGetId(svc);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseCreated);

            // Transition Intake → EvidencePending
            var r1 = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending, Reason = "Need ID docs" },
                "actor-1");
            Assert.That(r1.Success, Is.True, r1.ErrorMessage);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseStateTransitioned);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseEvidenceRequested);

            // Transition EvidencePending → UnderReview
            var r2 = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview, Reason = "Docs received" },
                "reviewer-1");
            Assert.That(r2.Success, Is.True, r2.ErrorMessage);

            // Transition UnderReview → Approved
            var r3 = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved, Reason = "All checks passed" },
                "approver-1");
            Assert.That(r3.Success, Is.True, r3.ErrorMessage);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalReady);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalGranted);

            lock (wh.Events)
            {
                Assert.That(wh.Events.Any(e => e.EventType == WebhookEventType.ComplianceCaseCreated), Is.True);
                Assert.That(wh.Events.Any(e => e.EventType == WebhookEventType.ComplianceCaseEvidenceRequested), Is.True);
                Assert.That(wh.Events.Any(e => e.EventType == WebhookEventType.ComplianceCaseApprovalGranted), Is.True);
            }
        }

        [Test]
        public async Task FullLifecycle_CreateToRejection_EmitsApprovalDeniedEvent()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Rejected, Reason = "Sanctions hit confirmed" },
                "approver-1");
            Assert.That(r.Success, Is.True, r.ErrorMessage);

            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalDenied);

            lock (wh.Events)
            {
                var denied = wh.Events.Single(e => e.EventType == WebhookEventType.ComplianceCaseApprovalDenied);
                Assert.That(denied.Data, Is.Not.Null);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 2. Rework (Remediating) path
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ReworkPath_UnderReviewToRemediating_EmitsReworkRequestedEvent()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest
                {
                    NewState = ComplianceCaseState.Remediating,
                    Reason = "Address discrepancy in KYC document"
                },
                "reviewer-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.Remediating));

            await PollForEvent(wh, WebhookEventType.ComplianceCaseReworkRequested);

            lock (wh.Events)
            {
                var rework = wh.Events.Single(e => e.EventType == WebhookEventType.ComplianceCaseReworkRequested);
                Assert.That(rework.Data, Is.Not.Null);
            }
        }

        [Test]
        public async Task ReworkPath_RemediatingToUnderReview_AllowsResubmission()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var toRework = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Remediating, Reason = "Rework needed" },
                "reviewer-1");
            Assert.That(toRework.Success, Is.True, toRework.ErrorMessage);

            // After rework, resubmit for review
            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview, Reason = "Rework complete" },
                "subject-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.UnderReview));
        }

        [Test]
        public async Task ReworkPath_RemediatingToApproved_EmitsBothReworkAndApprovalEvents()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Remediating, Reason = "Rework needed" },
                "reviewer-1");

            // Direct approval from Remediating (allowed by state machine)
            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved, Reason = "Rework satisfied" },
                "approver-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseReworkRequested);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalGranted);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 3. Evidence-requested event semantics
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvidenceRequested_TransitionToEvidencePending_EmitsSemanticEvent()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            var caseId = await CreateCaseAndGetId(svc);

            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest
                {
                    NewState = ComplianceCaseState.EvidencePending,
                    Reason = "Passport and proof of address required"
                },
                "compliance-officer-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseEvidenceRequested);

            lock (wh.Events)
            {
                var evt = wh.Events.Single(e => e.EventType == WebhookEventType.ComplianceCaseEvidenceRequested);
                Assert.That(evt.Data, Is.Not.Null);
                // Generic state transition event also emitted alongside semantic event
                Assert.That(wh.Events.Any(e => e.EventType == WebhookEventType.ComplianceCaseStateTransitioned), Is.True);
            }
        }

        [Test]
        public async Task EvidenceRequested_OnlyEmittedForEvidencePendingTransition()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(webhook: wh);

            var caseId = await CreateCaseAndGetId(svc);
            // Transition to EvidencePending (should emit EvidenceRequested), then to UnderReview (should NOT)
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "actor-1");
            await PollForEvent(wh, WebhookEventType.ComplianceCaseEvidenceRequested, count: 1);

            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "actor-1");

            // Poll for the generic StateTransitioned event from UnderReview transition to know delivery settled
            await PollForEvent(wh, WebhookEventType.ComplianceCaseStateTransitioned, count: 2);

            lock (wh.Events)
            {
                // Only one EvidenceRequested should exist (from EvidencePending, not UnderReview)
                Assert.That(wh.Events.Count(e => e.EventType == WebhookEventType.ComplianceCaseEvidenceRequested), Is.EqualTo(1));
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4. Fail-closed: invalid transitions rejected with clear errors
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InvalidTransition_ApprovedCase_CannotTransitionFurther()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "approver-1");

            // Attempt transition from terminal Approved state
            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "actor-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task InvalidTransition_RejectedToApproved_FailsClosed()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Rejected, Reason = "Denied" }, "approver-1");

            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "actor-2");

            Assert.That(r.Success, Is.False);
        }

        [Test]
        public async Task InvalidTransition_NonExistentCase_ReturnsNotFoundError()
        {
            var svc = CreateService();
            var r = await svc.TransitionStateAsync("no-such-case",
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "actor-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        [Test]
        public async Task InvalidTransition_IntakeToApproved_NotInAllowedSet_FailsClosed()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);

            // Intake → Approved is not in the allowed transitions
            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "actor-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task InvalidTransition_IntakeToUnderReview_NotInAllowedSet()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);

            // Intake → UnderReview is not allowed; must go through EvidencePending first
            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "actor-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 5. Decision lineage (KYC, AML, approval decisions)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DecisionLineage_KycApproval_RecordedAndRetrievable()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            var caseId = await CreateCaseAndGetId(svc);

            var addR = await svc.AddDecisionRecordAsync(caseId,
                new AddDecisionRecordRequest
                {
                    Kind = CaseDecisionKind.KycApproval,
                    DecisionSummary = "KYC passed — all documents verified",
                    Outcome = "Pass",
                    ProviderName = "CompliantID",
                    ProviderReference = "KYC-REF-001",
                    IsAdverse = false
                },
                "kyc-reviewer-1");

            Assert.That(addR.Success, Is.True, addR.ErrorMessage);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseDecisionRecorded);

            var histR = await svc.GetDecisionHistoryAsync(caseId, "actor-1");
            Assert.That(histR.Success, Is.True);
            Assert.That(histR.Decisions, Has.Count.EqualTo(1));
            Assert.That(histR.Decisions[0].Kind, Is.EqualTo(CaseDecisionKind.KycApproval));
        }

        [Test]
        public async Task DecisionLineage_AmlHit_RecordedAsAdverse()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);

            var r = await svc.AddDecisionRecordAsync(caseId,
                new AddDecisionRecordRequest
                {
                    Kind = CaseDecisionKind.AmlHit,
                    DecisionSummary = "AML screening returned a watchlist match",
                    Outcome = "Hit",
                    ProviderName = "AMLGuard",
                    IsAdverse = true
                },
                "aml-reviewer-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);

            var histR = await svc.GetDecisionHistoryAsync(caseId, "actor-1");
            Assert.That(histR.Decisions[0].IsAdverse, Is.True);
        }

        [Test]
        public async Task DecisionLineage_MultipleDecisions_AllRetained()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);

            await svc.AddDecisionRecordAsync(caseId,
                new AddDecisionRecordRequest { Kind = CaseDecisionKind.KycApproval, DecisionSummary = "KYC OK", IsAdverse = false },
                "actor-1");
            await svc.AddDecisionRecordAsync(caseId,
                new AddDecisionRecordRequest { Kind = CaseDecisionKind.AmlClear, DecisionSummary = "AML clear", IsAdverse = false },
                "actor-1");
            await svc.AddDecisionRecordAsync(caseId,
                new AddDecisionRecordRequest { Kind = CaseDecisionKind.ManualReviewDecision, DecisionSummary = "Manual override approved", IsAdverse = false },
                "actor-1");

            var histR = await svc.GetDecisionHistoryAsync(caseId, "actor-1");
            Assert.That(histR.Decisions, Has.Count.EqualTo(3));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 6. Escalation path
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EscalationPath_RaisedAndResolved_WebhooksFired()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);

            // Raise escalation
            var addR = await svc.AddEscalationAsync(caseId,
                new AddEscalationRequest
                {
                    Type = EscalationType.SanctionsHit,
                    Description = "Possible OFAC match",
                    RequiresManualReview = true
                },
                "reviewer-1");

            Assert.That(addR.Success, Is.True, addR.ErrorMessage);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseEscalationRaised);

            // Resolve escalation
            var escalationId = addR.Case!.Escalations.First().EscalationId;
            var resolveR = await svc.ResolveEscalationAsync(caseId, escalationId,
                new ResolveEscalationRequest { ResolutionNotes = "No match — false positive" },
                "sanctions-team");

            Assert.That(resolveR.Success, Is.True, resolveR.ErrorMessage);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseEscalationResolved);
        }

        [Test]
        public async Task EscalationPath_StateTransitionsToEscalated_ThenBackToReview()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r1 = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Escalated, Reason = "Senior review needed" },
                "reviewer-1");
            Assert.That(r1.Success, Is.True, r1.ErrorMessage);
            Assert.That(r1.Case!.State, Is.EqualTo(ComplianceCaseState.Escalated));

            var r2 = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview, Reason = "Escalation resolved" },
                "senior-reviewer-1");
            Assert.That(r2.Success, Is.True, r2.ErrorMessage);
            Assert.That(r2.Case!.State, Is.EqualTo(ComplianceCaseState.UnderReview));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 7. Approval workflow events carry required payload fields
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ApprovalGrantedEvent_ContainsIssuerId_SubjectId()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            var req = NewCase(issuerId: "issuer-xyz", subjectId: "subject-abc");
            var caseId = await CreateCaseAndGetId(svc, req);
            await AdvanceToUnderReview(svc, caseId);

            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved, Reason = "Full review passed" },
                "approver-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalGranted);

            lock (wh.Events)
            {
                var granted = wh.Events.Single(e => e.EventType == WebhookEventType.ComplianceCaseApprovalGranted);
                var payload = SerializeData(granted.Data);
                Assert.That(payload, Does.Contain("issuer-xyz"));
                Assert.That(payload, Does.Contain("subject-abc"));
            }
        }

        [Test]
        public async Task ApprovalDeniedEvent_ContainsRejectionReason()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Rejected, Reason = "AML hit confirmed - denied" },
                "approver-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalDenied);

            lock (wh.Events)
            {
                var denied = wh.Events.Single(e => e.EventType == WebhookEventType.ComplianceCaseApprovalDenied);
                var payload = SerializeData(denied.Data);
                Assert.That(payload, Does.Contain("AML hit"));
            }
        }

        [Test]
        public async Task ApprovalGrantedEvent_AlsoEmitsApprovalReadyEvent()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "approver-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalReady);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalGranted);

            lock (wh.Events)
            {
                // Both ApprovalReady AND the new ApprovalGranted event must fire
                Assert.That(wh.Events.Count(e => e.EventType == WebhookEventType.ComplianceCaseApprovalReady), Is.EqualTo(1));
                Assert.That(wh.Events.Count(e => e.EventType == WebhookEventType.ComplianceCaseApprovalGranted), Is.EqualTo(1));
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 8. Handoff status tracking post-approval
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task HandoffStatus_SetAndRetrieved_ReflectsDownstreamProgress()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "approver-1");

            var updateR = await svc.UpdateHandoffStatusAsync(caseId,
                new UpdateHandoffStatusRequest
                {
                    Stage = CaseHandoffStage.RegulatoryPackagePending,
                    BlockingReason = "Awaiting regulatory package sign-off",
                    HandoffNotes = "Regulatory team notified"
                },
                "ops-1");

            Assert.That(updateR.Success, Is.True, updateR.ErrorMessage);
            // IsHandoffReady is derived from Stage (only Completed → true)
            Assert.That(updateR.HandoffStatus!.IsHandoffReady, Is.False);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseHandoffStatusChanged);

            var getR = await svc.GetHandoffStatusAsync(caseId, "ops-1");
            Assert.That(getR.Success, Is.True);
            Assert.That(getR.HandoffStatus!.Stage, Is.EqualTo(CaseHandoffStage.RegulatoryPackagePending));
            Assert.That(getR.HandoffStatus.IsHandoffReady, Is.False);
        }

        [Test]
        public async Task HandoffStatus_CompletedStage_MarksHandoffReady()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);

            var r = await svc.UpdateHandoffStatusAsync(caseId,
                new UpdateHandoffStatusRequest
                {
                    Stage = CaseHandoffStage.Completed,
                    HandoffNotes = "All downstream steps complete"
                },
                "ops-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.HandoffStatus!.IsHandoffReady, Is.True);
            Assert.That(r.HandoffStatus.Stage, Is.EqualTo(CaseHandoffStage.Completed));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 9. Case timeline records every transition
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Timeline_FullLifecycle_ContainsAllTransitions()
        {
            var time = new FakeTime(DateTimeOffset.UtcNow);
            var svc = CreateService(time: time);

            var caseId = await CreateCaseAndGetId(svc);

            time.Advance(TimeSpan.FromMinutes(1));
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending, Reason = "Docs required" },
                "actor-1");

            time.Advance(TimeSpan.FromMinutes(1));
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview, Reason = "Docs received" },
                "reviewer-1");

            time.Advance(TimeSpan.FromMinutes(1));
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved, Reason = "Review complete" },
                "approver-1");

            var timelineR = await svc.GetTimelineAsync(caseId, "actor-1");
            Assert.That(timelineR.Success, Is.True);
            // CaseCreated + 3 StateTransitions = 4 entries minimum
            Assert.That(timelineR.Entries, Has.Count.GreaterThanOrEqualTo(4));

            var transitionEntries = timelineR.Entries
                .Where(t => t.EventType == CaseTimelineEventType.StateTransition)
                .ToList();
            Assert.That(transitionEntries, Has.Count.EqualTo(3));
        }

        [Test]
        public async Task Timeline_Immutable_NewTransitionAppendsEntry()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);

            var t1 = await svc.GetTimelineAsync(caseId, "actor-1");
            int count1 = t1.Entries.Count; // Should be 1 (CaseCreated)

            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "actor-1");

            var t2 = await svc.GetTimelineAsync(caseId, "actor-1");
            Assert.That(t2.Entries.Count, Is.GreaterThan(count1));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 10. Idempotency and re-open after terminal states
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Idempotency_SameSubjectCase_ReturnsSameCaseId()
        {
            var svc = CreateService();
            var req = NewCase(issuerId: "i1", subjectId: "s-idempotent");

            var r1 = await svc.CreateCaseAsync(req, "actor-1");
            var r2 = await svc.CreateCaseAsync(req, "actor-2");

            Assert.That(r1.Success, Is.True);
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.Case!.CaseId, Is.EqualTo(r1.Case!.CaseId));
        }

        [Test]
        public async Task Idempotency_AfterTerminalApproval_NewCaseCanBeCreatedForSameSubject()
        {
            var svc = CreateService();
            var req = NewCase(issuerId: "i1", subjectId: "s-reopen");

            var r1 = await svc.CreateCaseAsync(req, "actor-1");
            var caseId = r1.Case!.CaseId;

            await AdvanceToUnderReview(svc, caseId);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "approver-1");

            // After approval (terminal), a new case for same subject should be allowed
            var r2 = await svc.CreateCaseAsync(req, "actor-1");
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.Case!.CaseId, Is.Not.EqualTo(caseId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 11. Case summary and blockers for queue/SLA views
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CaseSummary_ReflectsCurrentOpenEscalations()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);

            await svc.AddEscalationAsync(caseId,
                new AddEscalationRequest
                {
                    Type = EscalationType.ManualEscalation,
                    Description = "Needs senior review",
                    RequiresManualReview = true
                },
                "actor-1");

            var summaryR = await svc.GetCaseSummaryAsync(caseId, "actor-1");
            Assert.That(summaryR.Success, Is.True);
            Assert.That(summaryR.Summary, Is.Not.Null);
            Assert.That(summaryR.Summary!.OpenEscalations, Is.EqualTo(1));
        }

        [Test]
        public async Task CaseBlockers_SLABreached_AppearInBlockerList()
        {
            var time = new FakeTime(DateTimeOffset.UtcNow);
            var svc = CreateService(time: time);

            var caseId = await CreateCaseAndGetId(svc);

            // Set SLA that has already passed
            await svc.SetSlaMetadataAsync(caseId,
                new SetSlaMetadataRequest
                {
                    ReviewDueAt = time.GetUtcNow().AddDays(-1)  // overdue
                },
                "ops-1");

            var blockersR = await svc.EvaluateBlockersAsync(caseId, "actor-1");
            Assert.That(blockersR.Success, Is.True);
            Assert.That(blockersR.Blockers.Any(b => b.Category == CaseBlockerCategory.SlaBreached), Is.True);
        }

        [Test]
        public async Task ListCaseSummaries_FiltersByIssuerId()
        {
            var svc = CreateService();

            // Create multiple cases with different issuers
            await svc.CreateCaseAsync(NewCase(issuerId: "i1", subjectId: "s1"), "actor-1");
            await svc.CreateCaseAsync(NewCase(issuerId: "i1", subjectId: "s2"), "actor-1");
            await svc.CreateCaseAsync(NewCase(issuerId: "i2", subjectId: "s3"), "actor-1");

            var r = await svc.ListCaseSummariesAsync(
                new ListComplianceCasesRequest { IssuerId = "i1" },
                "actor-1");

            Assert.That(r.Success, Is.True);
            Assert.That(r.Summaries, Has.Count.EqualTo(2));
            Assert.That(r.Summaries.All(s => s.IssuerId == "i1"), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 12. Assignment tracking for queue governance
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Assignment_AssignedToReviewer_RecordedInHistory()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            var caseId = await CreateCaseAndGetId(svc);

            var r = await svc.AssignCaseAsync(caseId,
                new AssignCaseRequest { ReviewerId = "reviewer-42", TeamId = "kyc-team", Reason = "Initial assignment" },
                "supervisor-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.AssignedReviewerId, Is.EqualTo("reviewer-42"));
            await PollForEvent(wh, WebhookEventType.ComplianceCaseAssignmentChanged);

            var histR = await svc.GetAssignmentHistoryAsync(caseId, "actor-1");
            Assert.That(histR.Success, Is.True);
            Assert.That(histR.History, Has.Count.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task Assignment_Reassignment_AddsNewHistoryEntry()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);

            await svc.AssignCaseAsync(caseId,
                new AssignCaseRequest { ReviewerId = "reviewer-1", Reason = "Initial" }, "supervisor-1");
            await svc.AssignCaseAsync(caseId,
                new AssignCaseRequest { ReviewerId = "reviewer-2", Reason = "Reassigned due to leave" }, "supervisor-1");

            var histR = await svc.GetAssignmentHistoryAsync(caseId, "actor-1");
            Assert.That(histR.History, Has.Count.EqualTo(2));
            Assert.That(histR.History.Last().NewReviewerId, Is.EqualTo("reviewer-2"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 13. Evidence stale detection in blockers
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Blockers_StaleEvidence_AppearInBlockerList()
        {
            var time = new FakeTime(DateTimeOffset.UtcNow);
            var svc = CreateService(time: time);

            var caseId = await CreateCaseAndGetId(svc);

            // Add evidence that expires in 1 day
            await svc.AddEvidenceAsync(caseId,
                new AddEvidenceRequest
                {
                    EvidenceType = "Passport",
                    Status = CaseEvidenceStatus.Valid,
                    CapturedAt = time.GetUtcNow().AddDays(-10),
                    ExpiresAt = time.GetUtcNow().AddDays(1)   // expires tomorrow
                },
                "actor-1");

            // Advance time so evidence is now expired
            time.Advance(TimeSpan.FromDays(2));

            var blockersR = await svc.EvaluateBlockersAsync(caseId, "actor-1");
            Assert.That(blockersR.Success, Is.True);
            Assert.That(blockersR.Blockers.Any(b => b.Category == CaseBlockerCategory.StaleEvidence), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 14. Terminal state semantics
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TerminalState_ApprovedCase_HasClosedAt()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved }, "approver-1");

            var caseR = await svc.GetCaseAsync(caseId, "actor-1");
            Assert.That(caseR.Success, Is.True);
            Assert.That(caseR.Case!.ClosedAt, Is.Not.Null);
        }

        [Test]
        public async Task TerminalState_RejectedCase_HasClosureReason()
        {
            var svc = CreateService();
            var caseId = await CreateCaseAndGetId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest
                {
                    NewState = ComplianceCaseState.Rejected,
                    Reason = "Confirmed sanctions hit permanent rejection"
                },
                "approver-1");

            var caseR = await svc.GetCaseAsync(caseId, "actor-1");
            Assert.That(caseR.Case!.ClosureReason, Is.EqualTo("Confirmed sanctions hit permanent rejection"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 15. All four new semantic events are distinct from StateTransitioned
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task NewSemanticEvents_EachEmittedOnce_WithDistinctPayloads()
        {
            var wh = new CapturingWebhook();
            var svc = CreateService(wh);

            // Case 1: trigger EvidenceRequested → ReworkRequested → ApprovalGranted
            var caseId1 = await CreateCaseAndGetId(svc, NewCase(subjectId: "subject-approve"));

            // Trigger EvidenceRequested
            await svc.TransitionStateAsync(caseId1,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "actor-1");
            await PollForEvent(wh, WebhookEventType.ComplianceCaseEvidenceRequested);

            // Advance to UnderReview
            await svc.TransitionStateAsync(caseId1,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "actor-1");

            // Trigger ReworkRequested
            await svc.TransitionStateAsync(caseId1,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Remediating, Reason = "Needs rework" }, "reviewer-1");
            await PollForEvent(wh, WebhookEventType.ComplianceCaseReworkRequested);

            // Trigger ApprovalGranted (Remediating → Approved is valid)
            await svc.TransitionStateAsync(caseId1,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Approved, Reason = "Approved after rework" }, "approver-1");
            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalGranted);

            // Case 2: trigger ApprovalDenied on a fresh case
            var caseId2 = await CreateCaseAndGetId(svc, NewCase(subjectId: "subject-reject"));
            await AdvanceToUnderReview(svc, caseId2);
            await svc.TransitionStateAsync(caseId2,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Rejected, Reason = "Denied" }, "approver-1");
            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalDenied);

            lock (wh.Events)
            {
                // EvidenceRequested fires once per EvidencePending transition — 2 cases each go through it
                // (case1 explicit + case2 via AdvanceToUnderReview)
                Assert.That(wh.Events.Count(e => e.EventType == WebhookEventType.ComplianceCaseEvidenceRequested), Is.EqualTo(2));
                Assert.That(wh.Events.Count(e => e.EventType == WebhookEventType.ComplianceCaseReworkRequested), Is.EqualTo(1));
                Assert.That(wh.Events.Count(e => e.EventType == WebhookEventType.ComplianceCaseApprovalGranted), Is.EqualTo(1));
                Assert.That(wh.Events.Count(e => e.EventType == WebhookEventType.ComplianceCaseApprovalDenied), Is.EqualTo(1));

                // Each semantic event should also accompany a generic StateTransitioned event
                int stateTransitioned = wh.Events.Count(e => e.EventType == WebhookEventType.ComplianceCaseStateTransitioned);
                Assert.That(stateTransitioned, Is.GreaterThanOrEqualTo(4),
                    "At least 4 generic StateTransitioned events should have fired");
            }
        }
    }
}
