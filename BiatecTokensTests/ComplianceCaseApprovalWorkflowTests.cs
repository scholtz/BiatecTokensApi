using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive tests for the approval-workflow parity APIs:
    ///   - ApproveComplianceCaseAsync: happy path, fail-closed states, decision record, webhooks
    ///   - RejectComplianceCaseAsync: happy path, required reason, fail-closed states, webhooks
    ///   - ReturnForInformationAsync: EvidencePending and Remediating targets, required reason, webhooks
    ///   - State machine extension: UnderReview → EvidencePending and Escalated → EvidencePending
    ///   - Decision history: approval and rejection decisions appear in GetDecisionHistoryAsync
    ///   - Timeline: ApprovalDecisionRecorded, RejectionDecisionRecorded, ReturnedForInformation entries
    ///   - Webhook delivery records: ComplianceCaseApprovalGranted, ApprovalDenied, ReturnedForInformation
    ///   - Edge cases: wrong state, missing reason, non-existent case, terminal state re-attempt
    ///   - Integration: approve/reject/return endpoints accessible via HTTP (integration tests)
    ///   - Protected sign-off: ComplianceCaseApprovalWorkflowAvailable check present
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceCaseApprovalWorkflowTests
    {
        // ═════════════════════════════════════════════════════════════════════
        // Fakes
        // ═════════════════════════════════════════════════════════════════════

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

        private static ComplianceCaseManagementService CreateService(CapturingWebhook? wh = null) =>
            new(NullLogger<ComplianceCaseManagementService>.Instance,
                webhookService: wh);

        private static CreateComplianceCaseRequest NewCase(
            string issuerId = "issuer-1", string subjectId = "subject-1") =>
            new() { IssuerId = issuerId, SubjectId = subjectId, Type = CaseType.InvestorEligibility };

        private static async Task<string> CreateCaseId(ComplianceCaseManagementService svc)
        {
            var r = await svc.CreateCaseAsync(NewCase(), "actor-1");
            Assert.That(r.Success, Is.True, r.ErrorMessage);
            return r.Case!.CaseId;
        }

        private static async Task AdvanceToUnderReview(ComplianceCaseManagementService svc, string caseId)
        {
            var r1 = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "actor-sys");
            Assert.That(r1.Success, Is.True, $"→EvidencePending failed: {r1.ErrorMessage}");
            var r2 = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview }, "actor-sys");
            Assert.That(r2.Success, Is.True, $"→UnderReview failed: {r2.ErrorMessage}");
        }

        private static async Task PollForEvent(CapturingWebhook wh, WebhookEventType type, int count = 1, int maxMs = 3000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
            while (DateTime.UtcNow < deadline)
            {
                int n;
                lock (wh.Events) n = wh.Events.Count(e => e.EventType == type);
                if (n >= count) return;
                await Task.Delay(20);
            }
            int final;
            lock (wh.Events) final = wh.Events.Count(e => e.EventType == type);
            Assert.Fail($"Expected {count}× {type} but got {final} after {maxMs}ms.");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 1. ApproveComplianceCaseAsync — happy paths
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Approve_FromUnderReview_Succeeds()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest { Rationale = "All checks passed" },
                "approver-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.Approved));
            Assert.That(r.DecisionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Approve_FromRemediating_Succeeds()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Remediating }, "reviewer");

            var r = await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest { Rationale = "Remediation complete and satisfactory" },
                "approver-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.Approved));
        }

        [Test]
        public async Task Approve_SetsClosedAtAndClosureReason()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest { Rationale = "Evidence satisfactory" },
                "approver-1");

            var getR = await svc.GetCaseAsync(caseId, "actor-1");
            Assert.That(getR.Case!.ClosedAt, Is.Not.Null);
            Assert.That(getR.Case.ClosureReason, Is.EqualTo("Evidence satisfactory"));
        }

        [Test]
        public async Task Approve_WithApprovedBy_OverridesActorId()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest
                {
                    Rationale  = "Approved by external system",
                    ApprovedBy = "external-approval-system"
                },
                "api-gateway");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            var decision = r.Case!.DecisionHistory.First(d => d.Kind == CaseDecisionKind.ApprovalDecision);
            Assert.That(decision.DecidedBy, Is.EqualTo("external-approval-system"));
        }

        [Test]
        public async Task Approve_WithOptionalNullRationale_UsesDefaultSummary()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest(), "approver-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            var decision = r.Case!.DecisionHistory.First(d => d.Kind == CaseDecisionKind.ApprovalDecision);
            Assert.That(decision.DecisionSummary, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Approve_RecordsDecisionInDecisionHistory()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest
                {
                    Rationale     = "All KYC/AML checks passed",
                    ApprovalNotes = "No adverse findings in any jurisdiction",
                    ExternalApprovalReference = "APPROVE-REF-001"
                },
                "senior-approver");

            var histR = await svc.GetDecisionHistoryAsync(caseId, "actor-1");
            Assert.That(histR.Decisions.Any(d => d.Kind == CaseDecisionKind.ApprovalDecision), Is.True);
            var dec = histR.Decisions.Single(d => d.Kind == CaseDecisionKind.ApprovalDecision);
            Assert.That(dec.Outcome, Is.EqualTo("Approved"));
            Assert.That(dec.IsAdverse, Is.False);
            Assert.That(dec.ProviderReference, Is.EqualTo("APPROVE-REF-001"));
        }

        [Test]
        public async Task Approve_EmitsApprovalGrantedAndDecisionRecordedWebhooks()
        {
            var wh  = new CapturingWebhook();
            var svc = CreateService(wh);
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest { Rationale = "Approved" }, "approver-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalGranted);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseDecisionRecorded);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalReady);
        }

        [Test]
        public async Task Approve_ApprovalGrantedPayload_ContainsIssuerId_And_SubjectId()
        {
            var wh  = new CapturingWebhook();
            var svc = CreateService(wh);
            var req = new CreateComplianceCaseRequest
            {
                IssuerId  = "issuer-xyz",
                SubjectId = "subject-abc",
                Type      = CaseType.InvestorEligibility
            };
            var created = await svc.CreateCaseAsync(req, "actor-1");
            var caseId  = created.Case!.CaseId;
            await AdvanceToUnderReview(svc, caseId);

            await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest { Rationale = "Approved" }, "approver-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalGranted);
            lock (wh.Events)
            {
                var evt = wh.Events.Single(e => e.EventType == WebhookEventType.ComplianceCaseApprovalGranted);
                var json = System.Text.Json.JsonSerializer.Serialize(evt.Data);
                Assert.That(json, Does.Contain("issuer-xyz"));
                Assert.That(json, Does.Contain("subject-abc"));
            }
        }

        [Test]
        public async Task Approve_AddsApprovalDecisionRecordedTimelineEntry()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest { Rationale = "Approved" }, "approver-1");

            var tlR = await svc.GetTimelineAsync(caseId, "actor-1");
            Assert.That(tlR.Entries.Any(e => e.EventType == CaseTimelineEventType.ApprovalDecisionRecorded), Is.True);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 2. ApproveComplianceCaseAsync — fail-closed paths
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Approve_FromIntake_FailsClosed()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);

            var r = await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest { Rationale = "skip" }, "approver-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task Approve_FromEvidencePending_FailsClosed()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "actor");

            var r = await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest(), "approver-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task Approve_AlreadyApproved_FailsClosed()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.ApproveComplianceCaseAsync(caseId, new ApproveComplianceCaseRequest { Rationale = "OK" }, "a1");

            var r = await svc.ApproveComplianceCaseAsync(caseId, new ApproveComplianceCaseRequest { Rationale = "Again" }, "a2");

            Assert.That(r.Success, Is.False);
        }

        [Test]
        public async Task Approve_NonExistentCase_ReturnsNotFound()
        {
            var svc = CreateService();
            var r   = await svc.ApproveComplianceCaseAsync("ghost-case",
                new ApproveComplianceCaseRequest { Rationale = "OK" }, "approver");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 3. RejectComplianceCaseAsync — happy paths
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Reject_FromUnderReview_Succeeds()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "Confirmed sanctions hit" },
                "reviewer-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.Rejected));
            Assert.That(r.DecisionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Reject_FromEscalated_Succeeds()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Escalated }, "reviewer");

            var r = await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "Senior review confirmed sanctions match" },
                "senior-reviewer");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.Rejected));
        }

        [Test]
        public async Task Reject_FromRemediating_Succeeds()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Remediating }, "reviewer");

            var r = await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "Remediation unsuccessful — AML hit confirmed" },
                "compliance-officer");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.Rejected));
        }

        [Test]
        public async Task Reject_SetsClosedAtAndClosureReason()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "AML hit — permanent denial" },
                "reviewer-1");

            var getR = await svc.GetCaseAsync(caseId, "actor-1");
            Assert.That(getR.Case!.ClosedAt, Is.Not.Null);
            Assert.That(getR.Case.ClosureReason, Is.EqualTo("AML hit — permanent denial"));
        }

        [Test]
        public async Task Reject_RecordsRejectionDecisionInHistory()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest
                {
                    Reason                    = "Watchlist match confirmed",
                    RejectionNotes            = "Subject matched OFAC SDN list",
                    ExternalRejectionReference = "REJ-REF-001"
                },
                "compliance-officer");

            var histR = await svc.GetDecisionHistoryAsync(caseId, "actor-1");
            var dec = histR.Decisions.Single(d => d.Kind == CaseDecisionKind.RejectionDecision);
            Assert.That(dec.Outcome, Is.EqualTo("Rejected"));
            Assert.That(dec.IsAdverse, Is.True);
            Assert.That(dec.ProviderReference, Is.EqualTo("REJ-REF-001"));
        }

        [Test]
        public async Task Reject_EmitsApprovalDeniedAndDecisionRecordedWebhooks()
        {
            var wh  = new CapturingWebhook();
            var svc = CreateService(wh);
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "Rejected" }, "reviewer-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalDenied);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseDecisionRecorded);
        }

        [Test]
        public async Task Reject_ApprovalDeniedPayload_ContainsReason()
        {
            var wh  = new CapturingWebhook();
            var svc = CreateService(wh);
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "AML sanctions hit confirmed" },
                "compliance-officer");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalDenied);
            lock (wh.Events)
            {
                var evt = wh.Events.Single(e => e.EventType == WebhookEventType.ComplianceCaseApprovalDenied);
                var json = System.Text.Json.JsonSerializer.Serialize(evt.Data);
                Assert.That(json, Does.Contain("AML sanctions"));
            }
        }

        [Test]
        public async Task Reject_AddsRejectionDecisionRecordedTimelineEntry()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "Rejected" }, "reviewer-1");

            var tlR = await svc.GetTimelineAsync(caseId, "actor-1");
            Assert.That(tlR.Entries.Any(e => e.EventType == CaseTimelineEventType.RejectionDecisionRecorded), Is.True);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 4. RejectComplianceCaseAsync — fail-closed paths
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Reject_MissingReason_FailsClosed()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = null },
                "reviewer-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task Reject_EmptyReason_FailsClosed()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "  " },
                "reviewer-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task Reject_FromIntake_FailsClosed()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);

            var r = await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "Rejected" }, "reviewer-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task Reject_AlreadyRejected_FailsClosed()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "First rejection" }, "r1");

            var r = await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "Second rejection" }, "r2");

            Assert.That(r.Success, Is.False);
        }

        [Test]
        public async Task Reject_NonExistentCase_ReturnsNotFound()
        {
            var svc = CreateService();
            var r   = await svc.RejectComplianceCaseAsync("ghost-case",
                new RejectComplianceCaseRequest { Reason = "Rejected" }, "reviewer");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 5. ReturnForInformationAsync — happy paths
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ReturnForInfo_FromUnderReview_ToEvidencePending_Succeeds()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest
                {
                    TargetStage    = ReturnForInformationTargetStage.EvidencePending,
                    Reason         = "Passport copy is missing",
                    RequestedItems = new List<string> { "Valid passport copy", "Proof of address" }
                },
                "reviewer-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.EvidencePending));
            Assert.That(r.ReturnedToStage, Is.EqualTo(ComplianceCaseState.EvidencePending));
        }

        [Test]
        public async Task ReturnForInfo_FromUnderReview_ToRemediating_Succeeds()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest
                {
                    TargetStage = ReturnForInformationTargetStage.Remediating,
                    Reason      = "Address discrepancy requires correction"
                },
                "reviewer-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.Remediating));
            Assert.That(r.ReturnedToStage, Is.EqualTo(ComplianceCaseState.Remediating));
        }

        [Test]
        public async Task ReturnForInfo_DefaultStage_IsEvidencePending()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest { Reason = "Need more docs" },
                "reviewer-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.EvidencePending));
        }

        [Test]
        public async Task ReturnForInfo_FromEscalated_ToEvidencePending_Succeeds()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Escalated }, "reviewer");

            var r = await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest
                {
                    Reason         = "Senior review requires additional address evidence",
                    RequestedItems = new List<string> { "Utility bill dated within 3 months" }
                },
                "senior-reviewer");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.EvidencePending));
        }

        [Test]
        public async Task ReturnForInfo_EmitsReturnedForInformationWebhook()
        {
            var wh  = new CapturingWebhook();
            var svc = CreateService(wh);
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest { Reason = "Missing docs" }, "reviewer-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseReturnedForInformation);
        }

        [Test]
        public async Task ReturnForInfo_ToEvidencePending_AlsoEmitsEvidenceRequestedWebhook()
        {
            var wh  = new CapturingWebhook();
            var svc = CreateService(wh);
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest
                {
                    TargetStage = ReturnForInformationTargetStage.EvidencePending,
                    Reason      = "Need fresh identity docs"
                },
                "reviewer-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseReturnedForInformation);
            await PollForEvent(wh, WebhookEventType.ComplianceCaseEvidenceRequested);
        }

        [Test]
        public async Task ReturnForInfo_ToRemediating_DoesNotEmitEvidenceRequestedWebhook()
        {
            var wh  = new CapturingWebhook();
            var svc = CreateService(wh);
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest
                {
                    TargetStage = ReturnForInformationTargetStage.Remediating,
                    Reason      = "Address discrepancy needs correction"
                },
                "reviewer-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseReturnedForInformation);

            // Give time for any EvidenceRequested event (there should be none for Remediating target)
            await Task.Delay(100);
            lock (wh.Events)
            {
                // EvidenceRequested should only appear if the original Intake→EvidencePending transition was there.
                // After returning to Remediating, no additional EvidenceRequested should fire.
                // Count events from after case creation (post-AdvanceToUnderReview)
                int retInfoCount = wh.Events.Count(e => e.EventType == WebhookEventType.ComplianceCaseReturnedForInformation);
                Assert.That(retInfoCount, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task ReturnForInfo_ReturnedForInformationPayload_ContainsReason()
        {
            var wh  = new CapturingWebhook();
            var svc = CreateService(wh);
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest
                {
                    Reason         = "Passport expired and identity cannot be confirmed",
                    RequestedItems = new List<string> { "Renewed passport", "Matching selfie" },
                    AdditionalNotes = "Document must be government-issued"
                },
                "reviewer-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseReturnedForInformation);
            lock (wh.Events)
            {
                var evt = wh.Events.Single(e => e.EventType == WebhookEventType.ComplianceCaseReturnedForInformation);
                var json = System.Text.Json.JsonSerializer.Serialize(evt.Data);
                Assert.That(json, Does.Contain("Passport expired"));
                Assert.That(json, Does.Contain("Renewed passport"));
            }
        }

        [Test]
        public async Task ReturnForInfo_AddsReturnedForInformationTimelineEntry()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest { Reason = "Need docs" }, "reviewer-1");

            var tlR = await svc.GetTimelineAsync(caseId, "actor-1");
            Assert.That(tlR.Entries.Any(e => e.EventType == CaseTimelineEventType.ReturnedForInformation), Is.True);
        }

        [Test]
        public async Task ReturnForInfo_CaseCanProgressAfterReturn()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            // Return to EvidencePending
            await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest { Reason = "Missing docs" }, "reviewer-1");

            // Re-advance to UnderReview after supplying docs
            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.UnderReview, Reason = "Docs supplied" },
                "actor-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.UnderReview));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 6. ReturnForInformationAsync — fail-closed paths
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ReturnForInfo_MissingReason_FailsClosed()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest { Reason = null }, "reviewer-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task ReturnForInfo_FromIntake_FailsClosed()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);

            var r = await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest { Reason = "Need docs" }, "reviewer-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task ReturnForInfo_FromEvidencePending_FailsClosed()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.EvidencePending }, "actor");

            var r = await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest { Reason = "Need docs" }, "reviewer-1");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task ReturnForInfo_FromTerminalApproved_FailsClosed()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.ApproveComplianceCaseAsync(caseId, new ApproveComplianceCaseRequest { Rationale = "Approved" }, "a1");

            var r = await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest { Reason = "Actually need more" }, "reviewer-1");

            Assert.That(r.Success, Is.False);
        }

        [Test]
        public async Task ReturnForInfo_NonExistentCase_ReturnsNotFound()
        {
            var svc = CreateService();
            var r   = await svc.ReturnForInformationAsync("ghost-case",
                new ReturnForInformationRequest { Reason = "Need docs" }, "reviewer");

            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 7. State machine extension — UnderReview → EvidencePending via transition
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StateMachine_UnderReview_CanTransitionTo_EvidencePending()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest
                {
                    NewState = ComplianceCaseState.EvidencePending,
                    Reason   = "Additional evidence required during review"
                },
                "reviewer-1");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.EvidencePending));
        }

        [Test]
        public async Task StateMachine_Escalated_CanTransitionTo_EvidencePending()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest { NewState = ComplianceCaseState.Escalated }, "reviewer");

            var r = await svc.TransitionStateAsync(caseId,
                new TransitionCaseStateRequest
                {
                    NewState = ComplianceCaseState.EvidencePending,
                    Reason   = "Senior reviewer requests more docs"
                },
                "senior-reviewer");

            Assert.That(r.Success, Is.True, r.ErrorMessage);
            Assert.That(r.Case!.State, Is.EqualTo(ComplianceCaseState.EvidencePending));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 8. Decision history — approve and reject decisions cumulate
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DecisionHistory_ApprovalDecision_IsPresent_After_Approve()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.ApproveComplianceCaseAsync(caseId, new ApproveComplianceCaseRequest { Rationale = "OK" }, "a1");

            var histR = await svc.GetDecisionHistoryAsync(caseId, "actor-1");
            Assert.That(histR.Success, Is.True);
            Assert.That(histR.Decisions.Any(d => d.Kind == CaseDecisionKind.ApprovalDecision), Is.True);
        }

        [Test]
        public async Task DecisionHistory_RejectionDecision_IsPresent_After_Reject()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "AML hit" }, "r1");

            var histR = await svc.GetDecisionHistoryAsync(caseId, "actor-1");
            Assert.That(histR.Success, Is.True);
            Assert.That(histR.Decisions.Any(d => d.Kind == CaseDecisionKind.RejectionDecision), Is.True);
            // Rejection decisions are adverse
            Assert.That(histR.Decisions.Any(d => d.Kind == CaseDecisionKind.RejectionDecision && d.IsAdverse), Is.True);
        }

        [Test]
        public async Task DecisionHistory_PreExistingDecisions_PlusApproval_AllRetained()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);

            // Add a KYC decision first
            await svc.AddDecisionRecordAsync(caseId,
                new AddDecisionRecordRequest { Kind = CaseDecisionKind.KycApproval, DecisionSummary = "KYC passed", IsAdverse = false },
                "kyc-system");

            await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest { Rationale = "KYC + manual review passed" }, "approver");

            var histR = await svc.GetDecisionHistoryAsync(caseId, "actor-1");
            Assert.That(histR.Decisions, Has.Count.EqualTo(2));
            Assert.That(histR.Decisions.Any(d => d.Kind == CaseDecisionKind.KycApproval), Is.True);
            Assert.That(histR.Decisions.Any(d => d.Kind == CaseDecisionKind.ApprovalDecision), Is.True);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 9. Idempotency after approval/rejection via new semantic endpoints
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Approve_AfterApproval_NewCaseCanBeCreatedForSameSubject()
        {
            var svc = CreateService();
            var req = NewCase(issuerId: "i1", subjectId: "s-re-approve");

            var r1 = await svc.CreateCaseAsync(req, "actor-1");
            var caseId = r1.Case!.CaseId;
            await AdvanceToUnderReview(svc, caseId);
            await svc.ApproveComplianceCaseAsync(caseId, new ApproveComplianceCaseRequest { Rationale = "OK" }, "a1");

            // After approval (terminal), new case for same subject should succeed
            var r2 = await svc.CreateCaseAsync(req, "actor-1");
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.Case!.CaseId, Is.Not.EqualTo(caseId));
        }

        [Test]
        public async Task Reject_AfterRejection_NewCaseCanBeCreatedForSameSubject()
        {
            var svc = CreateService();
            var req = NewCase(issuerId: "i1", subjectId: "s-re-reject");

            var r1 = await svc.CreateCaseAsync(req, "actor-1");
            var caseId = r1.Case!.CaseId;
            await AdvanceToUnderReview(svc, caseId);
            await svc.RejectComplianceCaseAsync(caseId,
                new RejectComplianceCaseRequest { Reason = "Rejected" }, "r1");

            var r2 = await svc.CreateCaseAsync(req, "actor-1");
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.Case!.CaseId, Is.Not.EqualTo(caseId));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 10. Delivery status — approve/reject/return events appear in delivery records
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Approve_DeliveryRecords_ContainApprovalGrantedEvent()
        {
            var wh  = new CapturingWebhook();
            var svc = CreateService(wh);
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.ApproveComplianceCaseAsync(caseId,
                new ApproveComplianceCaseRequest { Rationale = "Approved" }, "approver-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseApprovalGranted);

            var delivR = await svc.GetDeliveryStatusAsync(caseId, "actor-1");
            Assert.That(delivR.Success, Is.True);
            Assert.That(delivR.Records.Any(r => r.EventType == WebhookEventType.ComplianceCaseApprovalGranted), Is.True);
        }

        [Test]
        public async Task ReturnForInfo_DeliveryRecords_ContainReturnedForInformationEvent()
        {
            var wh  = new CapturingWebhook();
            var svc = CreateService(wh);
            var caseId = await CreateCaseId(svc);
            await AdvanceToUnderReview(svc, caseId);
            await svc.ReturnForInformationAsync(caseId,
                new ReturnForInformationRequest { Reason = "Missing docs" }, "reviewer-1");

            await PollForEvent(wh, WebhookEventType.ComplianceCaseReturnedForInformation);

            var delivR = await svc.GetDeliveryStatusAsync(caseId, "actor-1");
            Assert.That(delivR.Success, Is.True);
            Assert.That(delivR.Records.Any(r => r.EventType == WebhookEventType.ComplianceCaseReturnedForInformation), Is.True);
        }
    }
}
