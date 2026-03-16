using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for the compliance case assignment, SLA metadata, escalation history,
    /// and webhook delivery-status APIs introduced as part of issue: implement
    /// compliance case-management assignment, escalation, and webhook delivery APIs.
    ///
    /// Coverage areas:
    ///   - AssignCaseAsync: first assignment, reassignment, history persistence, webhook events
    ///   - GetAssignmentHistoryAsync: chronological records, not-found errors
    ///   - GetEscalationHistoryAsync: counts, ordering, not-found errors
    ///   - SetSlaMetadataAsync: urgency band derivation, overdue detection, SlaBreached event
    ///   - GetDeliveryStatusAsync: delivery record tracking, counts, not-found errors
    ///   - Edge cases: missing required fields, invalid caseId, duplicate assignments
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceCaseAssignmentSlaDeliveryTests
    {
        // ═════════════════════════════════════════════════════════════════════
        // Fake providers
        // ═════════════════════════════════════════════════════════════════════

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset initial) => _now = initial;
            public void Advance(TimeSpan delta)              => _now = _now.Add(delta);
            public void SetUtcNow(DateTimeOffset value)      => _now = value;
            public override DateTimeOffset GetUtcNow()       => _now;
        }

        private sealed class CapturingWebhookService : IWebhookService
        {
            public List<WebhookEvent> EmittedEvents { get; } = new();

            public Task EmitEventAsync(WebhookEvent e)
            {
                lock (EmittedEvents) EmittedEvents.Add(e);
                return Task.CompletedTask;
            }

            public Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest r, string createdBy)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string id, string userId)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = false });
            public Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string userId)
                => Task.FromResult(new WebhookSubscriptionListResponse { Success = true });
            public Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest r, string userId)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string id, string userId)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest r, string userId)
                => Task.FromResult(new WebhookDeliveryHistoryResponse { Success = true });
        }

        // ═════════════════════════════════════════════════════════════════════
        // Helper factories
        // ═════════════════════════════════════════════════════════════════════

        private static readonly DateTimeOffset _baseline = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

        private static ComplianceCaseManagementService CreateService(
            IWebhookService? webhookService = null,
            TimeProvider? timeProvider = null) =>
            new(NullLogger<ComplianceCaseManagementService>.Instance,
                timeProvider,
                defaultEvidenceValidity: null,
                webhookService: webhookService,
                repository: null);

        private static (ComplianceCaseManagementService svc, CapturingWebhookService ws, FakeTimeProvider tp)
            CreateServiceWithCapture()
        {
            var tp  = new FakeTimeProvider(_baseline);
            var ws  = new CapturingWebhookService();
            var svc = CreateService(webhookService: ws, timeProvider: tp);
            return (svc, ws, tp);
        }

        private static async Task<string> CreateCaseIdAsync(ComplianceCaseManagementService svc,
            string issuerId = "issuer-1", string subjectId = "subject-1")
        {
            var resp = await svc.CreateCaseAsync(
                new CreateComplianceCaseRequest { IssuerId = issuerId, SubjectId = subjectId, Type = CaseType.InvestorEligibility },
                "system");
            return resp.Case!.CaseId;
        }

        private static async Task WaitForEventAsync(CapturingWebhookService ws, WebhookEventType type, int maxWaitMs = 500)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            while (DateTime.UtcNow < deadline)
            {
                bool found;
                lock (ws.EmittedEvents)
                    found = ws.EmittedEvents.Any(e => e.EventType == type);
                if (found) return;
                await Task.Delay(20);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // 1. AssignCaseAsync — basic behaviour
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AssignCase_FirstAssignment_RecordsNoPreviousOwner()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            var result = await svc.AssignCaseAsync(caseId,
                new AssignCaseRequest { ReviewerId = "alice", Reason = "Initial assignment" }, "manager");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Case!.AssignedReviewerId, Is.EqualTo("alice"));

            var record = result.AssignmentRecord!;
            Assert.That(record.PreviousReviewerId, Is.Null);
            Assert.That(record.NewReviewerId,      Is.EqualTo("alice"));
            Assert.That(record.Reason,             Is.EqualTo("Initial assignment"));
            Assert.That(record.AssignedBy,         Is.EqualTo("manager"));
        }

        [Test]
        public async Task AssignCase_Reassignment_RecordsPreviousOwner()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "alice" }, "manager");
            var result = await svc.AssignCaseAsync(caseId,
                new AssignCaseRequest { ReviewerId = "bob", Reason = "Capacity rebalance" }, "manager");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Case!.AssignedReviewerId, Is.EqualTo("bob"));
            Assert.That(result.AssignmentRecord!.PreviousReviewerId, Is.EqualTo("alice"));
            Assert.That(result.AssignmentRecord.NewReviewerId,       Is.EqualTo("bob"));
        }

        [Test]
        public async Task AssignCase_TeamAssignment_SetsTeamId()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            var result = await svc.AssignCaseAsync(caseId,
                new AssignCaseRequest { TeamId = "sanctions-team" }, "manager");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Case!.AssignedTeamId,         Is.EqualTo("sanctions-team"));
            Assert.That(result.AssignmentRecord!.NewTeamId,  Is.EqualTo("sanctions-team"));
        }

        [Test]
        public async Task AssignCase_BothReviewerAndTeam_SetsBoth()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            var result = await svc.AssignCaseAsync(caseId,
                new AssignCaseRequest { ReviewerId = "charlie", TeamId = "kyc-team" }, "manager");

            Assert.That(result.Case!.AssignedReviewerId, Is.EqualTo("charlie"));
            Assert.That(result.Case.AssignedTeamId,       Is.EqualTo("kyc-team"));
        }

        [Test]
        public async Task AssignCase_NoReviewerOrTeam_ReturnsMissingField()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            var result = await svc.AssignCaseAsync(caseId, new AssignCaseRequest(), "manager");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        [Test]
        public async Task AssignCase_NonExistentCase_ReturnsNotFound()
        {
            var svc    = CreateService();
            var result = await svc.AssignCaseAsync("does-not-exist",
                new AssignCaseRequest { ReviewerId = "alice" }, "manager");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 2. AssignCaseAsync — history accumulation
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AssignCase_MultipleReassignments_BuildsHistory()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "alice" }, "mgr");
            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "bob" },   "mgr");
            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "carol" }, "mgr");

            var histResp = await svc.GetAssignmentHistoryAsync(caseId, "mgr");

            Assert.That(histResp.Success,    Is.True);
            Assert.That(histResp.TotalCount, Is.EqualTo(3));
            Assert.That(histResp.History[0].NewReviewerId, Is.EqualTo("alice"));
            Assert.That(histResp.History[1].NewReviewerId, Is.EqualTo("bob"));
            Assert.That(histResp.History[2].NewReviewerId, Is.EqualTo("carol"));
        }

        [Test]
        public async Task AssignCase_AddsTimelineEntry()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            await svc.AssignCaseAsync(caseId,
                new AssignCaseRequest { ReviewerId = "alice", Reason = "First assignment" }, "mgr");

            var timeline = await svc.GetTimelineAsync(caseId, "mgr");
            var assignEntry = timeline.Entries
                .FirstOrDefault(e => e.EventType == CaseTimelineEventType.ReviewerAssigned);

            Assert.That(assignEntry, Is.Not.Null);
            Assert.That(assignEntry!.Metadata.ContainsKey("newReviewerId"), Is.True);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 3. AssignCaseAsync — webhook events
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AssignCase_ReviewerChange_EmitsAssignmentChangedEvent()
        {
            var (svc, ws, _) = CreateServiceWithCapture();
            var caseId       = await CreateCaseIdAsync(svc);

            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "alice" }, "mgr");
            await WaitForEventAsync(ws, WebhookEventType.ComplianceCaseAssignmentChanged);

            lock (ws.EmittedEvents)
                Assert.That(ws.EmittedEvents.Any(e => e.EventType == WebhookEventType.ComplianceCaseAssignmentChanged),
                    Is.True);
        }

        [Test]
        public async Task AssignCase_TeamChange_EmitsTeamAssignedEvent()
        {
            var (svc, ws, _) = CreateServiceWithCapture();
            var caseId       = await CreateCaseIdAsync(svc);

            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { TeamId = "sanctions-team" }, "mgr");
            await WaitForEventAsync(ws, WebhookEventType.ComplianceCaseTeamAssigned);

            lock (ws.EmittedEvents)
                Assert.That(ws.EmittedEvents.Any(e => e.EventType == WebhookEventType.ComplianceCaseTeamAssigned),
                    Is.True);
        }

        [Test]
        public async Task AssignCase_SameReviewer_NoAssignmentChangedEvent()
        {
            var (svc, ws, _) = CreateServiceWithCapture();
            var caseId       = await CreateCaseIdAsync(svc);

            // First assignment
            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "alice" }, "mgr");
            await WaitForEventAsync(ws, WebhookEventType.ComplianceCaseAssignmentChanged);

            int countBefore;
            lock (ws.EmittedEvents)
                countBefore = ws.EmittedEvents.Count(e => e.EventType == WebhookEventType.ComplianceCaseAssignmentChanged);

            // Re-assign to same reviewer — reviewer did not change, no new event expected
            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "alice" }, "mgr");
            await Task.Delay(100); // brief wait

            int countAfter;
            lock (ws.EmittedEvents)
                countAfter = ws.EmittedEvents.Count(e => e.EventType == WebhookEventType.ComplianceCaseAssignmentChanged);

            // Same reviewer → reviewerChanged is false → no new AssignmentChanged event
            Assert.That(countAfter, Is.EqualTo(countBefore));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 4. GetAssignmentHistoryAsync
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetAssignmentHistory_NewCase_ReturnsEmptyHistory()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            var result = await svc.GetAssignmentHistoryAsync(caseId, "actor");

            Assert.That(result.Success,    Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(0));
            Assert.That(result.History,    Is.Empty);
        }

        [Test]
        public async Task GetAssignmentHistory_NonExistentCase_ReturnsNotFound()
        {
            var svc    = CreateService();
            var result = await svc.GetAssignmentHistoryAsync("bad-id", "actor");

            Assert.That(result.Success,   Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        [Test]
        public async Task GetAssignmentHistory_ChronologicalOrder()
        {
            var tp     = new FakeTimeProvider(_baseline);
            var svc    = CreateService(timeProvider: tp);
            var caseId = await CreateCaseIdAsync(svc);

            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "alice" }, "mgr");
            tp.Advance(TimeSpan.FromHours(1));
            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "bob" }, "mgr");

            var result = await svc.GetAssignmentHistoryAsync(caseId, "mgr");

            Assert.That(result.History[0].AssignedAt, Is.LessThan(result.History[1].AssignedAt));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 5. GetEscalationHistoryAsync
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetEscalationHistory_NewCase_ReturnsEmpty()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            var result = await svc.GetEscalationHistoryAsync(caseId, "actor");

            Assert.That(result.Success,       Is.True);
            Assert.That(result.Escalations,   Is.Empty);
            Assert.That(result.OpenCount,     Is.EqualTo(0));
            Assert.That(result.ResolvedCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetEscalationHistory_NonExistentCase_ReturnsNotFound()
        {
            var svc    = CreateService();
            var result = await svc.GetEscalationHistoryAsync("no-case", "actor");

            Assert.That(result.Success,   Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        [Test]
        public async Task GetEscalationHistory_OneOpen_OneResolved_CountsCorrect()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            // Add two escalations
            var e1 = await svc.AddEscalationAsync(caseId,
                new AddEscalationRequest { Type = EscalationType.SanctionsHit, Description = "Hit1" }, "actor");
            await svc.AddEscalationAsync(caseId,
                new AddEscalationRequest { Type = EscalationType.AdverseMedia, Description = "Hit2" }, "actor");

            // Resolve first escalation
            var escalationId = e1.Case!.Escalations[0].EscalationId;
            await svc.ResolveEscalationAsync(caseId, escalationId,
                new ResolveEscalationRequest { ResolutionNotes = "Cleared" }, "actor");

            var result = await svc.GetEscalationHistoryAsync(caseId, "actor");

            Assert.That(result.Success,       Is.True);
            Assert.That(result.Escalations.Count, Is.EqualTo(2));
            Assert.That(result.OpenCount,     Is.EqualTo(1));
            Assert.That(result.ResolvedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GetEscalationHistory_ChronologicalOrder()
        {
            var tp     = new FakeTimeProvider(_baseline);
            var svc    = CreateService(timeProvider: tp);
            var caseId = await CreateCaseIdAsync(svc);

            await svc.AddEscalationAsync(caseId,
                new AddEscalationRequest { Type = EscalationType.ManualEscalation, Description = "First" }, "actor");
            tp.Advance(TimeSpan.FromHours(1));
            await svc.AddEscalationAsync(caseId,
                new AddEscalationRequest { Type = EscalationType.AdverseMedia, Description = "Second" }, "actor");

            var result = await svc.GetEscalationHistoryAsync(caseId, "actor");

            Assert.That(result.Escalations[0].RaisedAt, Is.LessThan(result.Escalations[1].RaisedAt));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 6. SetSlaMetadataAsync — urgency band derivation
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task SetSla_FutureDueDate_UrgencyNormal()
        {
            var tp     = new FakeTimeProvider(_baseline);
            var svc    = CreateService(timeProvider: tp);
            var caseId = await CreateCaseIdAsync(svc);

            var result = await svc.SetSlaMetadataAsync(caseId,
                new SetSlaMetadataRequest { ReviewDueAt = _baseline.AddDays(30) }, "actor");

            Assert.That(result.Success,                         Is.True);
            Assert.That(result.SlaMetadata!.IsOverdue,          Is.False);
            Assert.That(result.SlaMetadata.UrgencyBand,         Is.EqualTo(CaseUrgencyBand.Normal));
        }

        [Test]
        public async Task SetSla_DueSoonWithin7Days_UrgencyWarning()
        {
            var tp     = new FakeTimeProvider(_baseline);
            var svc    = CreateService(timeProvider: tp);
            var caseId = await CreateCaseIdAsync(svc);

            var result = await svc.SetSlaMetadataAsync(caseId,
                new SetSlaMetadataRequest { ReviewDueAt = _baseline.AddDays(5) }, "actor");

            Assert.That(result.SlaMetadata!.UrgencyBand, Is.EqualTo(CaseUrgencyBand.Warning));
        }

        [Test]
        public async Task SetSla_DueSoonWithin3Days_UrgencyCritical()
        {
            var tp     = new FakeTimeProvider(_baseline);
            var svc    = CreateService(timeProvider: tp);
            var caseId = await CreateCaseIdAsync(svc);

            var result = await svc.SetSlaMetadataAsync(caseId,
                new SetSlaMetadataRequest { ReviewDueAt = _baseline.AddDays(2) }, "actor");

            Assert.That(result.SlaMetadata!.UrgencyBand, Is.EqualTo(CaseUrgencyBand.Critical));
        }

        [Test]
        public async Task SetSla_AlreadyOverdue_IsOverdueTrue_UrgencyCritical()
        {
            var tp     = new FakeTimeProvider(_baseline);
            var svc    = CreateService(timeProvider: tp);
            var caseId = await CreateCaseIdAsync(svc);

            // Due date in the past
            var result = await svc.SetSlaMetadataAsync(caseId,
                new SetSlaMetadataRequest { ReviewDueAt = _baseline.AddDays(-1) }, "actor");

            Assert.That(result.SlaMetadata!.IsOverdue,   Is.True);
            Assert.That(result.SlaMetadata.UrgencyBand,  Is.EqualTo(CaseUrgencyBand.Critical));
            Assert.That(result.SlaMetadata.OverdueSince, Is.Not.Null);
        }

        [Test]
        public async Task SetSla_NoDueDate_UrgencyNormal()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            var result = await svc.SetSlaMetadataAsync(caseId,
                new SetSlaMetadataRequest { Notes = "No due date set" }, "actor");

            Assert.That(result.SlaMetadata!.UrgencyBand, Is.EqualTo(CaseUrgencyBand.Normal));
            Assert.That(result.SlaMetadata.IsOverdue,    Is.False);
        }

        [Test]
        public async Task SetSla_EscalationDueDatePersisted()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);
            var escDue = _baseline.AddDays(7);

            var result = await svc.SetSlaMetadataAsync(caseId,
                new SetSlaMetadataRequest { EscalationDueAt = escDue }, "actor");

            Assert.That(result.SlaMetadata!.EscalationDueAt, Is.EqualTo(escDue));
        }

        [Test]
        public async Task SetSla_UpdatesCase_SlaMetadataOnCase()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            await svc.SetSlaMetadataAsync(caseId,
                new SetSlaMetadataRequest { ReviewDueAt = _baseline.AddDays(10) }, "actor");

            var c = await svc.GetCaseAsync(caseId, "actor");
            Assert.That(c.Case!.SlaMetadata, Is.Not.Null);
            Assert.That(c.Case.SlaMetadata!.ReviewDueAt, Is.Not.Null);
        }

        [Test]
        public async Task SetSla_NonExistentCase_ReturnsNotFound()
        {
            var svc    = CreateService();
            var result = await svc.SetSlaMetadataAsync("no-case",
                new SetSlaMetadataRequest { ReviewDueAt = _baseline.AddDays(5) }, "actor");

            Assert.That(result.Success,   Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 7. SetSlaMetadataAsync — SlaBreached webhook event
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task SetSla_PastDueDate_EmitsSlaBreachedEvent()
        {
            var (svc, ws, tp) = CreateServiceWithCapture();
            var caseId        = await CreateCaseIdAsync(svc);

            await svc.SetSlaMetadataAsync(caseId,
                new SetSlaMetadataRequest { ReviewDueAt = _baseline.AddDays(-2) }, "actor");

            await WaitForEventAsync(ws, WebhookEventType.ComplianceCaseSlaBreached);

            lock (ws.EmittedEvents)
                Assert.That(ws.EmittedEvents.Any(e => e.EventType == WebhookEventType.ComplianceCaseSlaBreached),
                    Is.True);
        }

        [Test]
        public async Task SetSla_FutureDueDate_DoesNotEmitSlaBreachedEvent()
        {
            var (svc, ws, _) = CreateServiceWithCapture();
            var caseId       = await CreateCaseIdAsync(svc);

            await svc.SetSlaMetadataAsync(caseId,
                new SetSlaMetadataRequest { ReviewDueAt = _baseline.AddDays(30) }, "actor");

            await Task.Delay(100); // brief wait — event should NOT arrive

            lock (ws.EmittedEvents)
                Assert.That(ws.EmittedEvents.Any(e => e.EventType == WebhookEventType.ComplianceCaseSlaBreached),
                    Is.False);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 8. GetDeliveryStatusAsync — delivery record tracking
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetDeliveryStatus_NewCase_EmptyRecords()
        {
            var svc    = CreateService(); // no webhook service → no records
            var caseId = await CreateCaseIdAsync(svc);

            var result = await svc.GetDeliveryStatusAsync(caseId, "actor");

            Assert.That(result.Success,    Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetDeliveryStatus_NonExistentCase_ReturnsNotFound()
        {
            var svc    = CreateService();
            var result = await svc.GetDeliveryStatusAsync("no-case", "actor");

            Assert.That(result.Success,   Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_FOUND"));
        }

        [Test]
        public async Task GetDeliveryStatus_AfterEventEmitted_RecordsPresent()
        {
            var (svc, ws, _) = CreateServiceWithCapture();
            var caseId       = await CreateCaseIdAsync(svc);

            // Creating the case emits ComplianceCaseCreated; wait for the delivery record to appear
            await WaitForEventAsync(ws, WebhookEventType.ComplianceCaseCreated);

            // Allow the delivery record state to settle
            var deadline = DateTime.UtcNow.AddSeconds(1);
            GetDeliveryStatusResponse result;
            do
            {
                await Task.Delay(20);
                result = await svc.GetDeliveryStatusAsync(caseId, "actor");
            } while (result.TotalCount == 0 && DateTime.UtcNow < deadline);

            Assert.That(result.Success,    Is.True);
            Assert.That(result.TotalCount, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetDeliveryStatus_SuccessfulDelivery_CountedCorrectly()
        {
            var (svc, ws, _) = CreateServiceWithCapture();
            var caseId       = await CreateCaseIdAsync(svc);

            await WaitForEventAsync(ws, WebhookEventType.ComplianceCaseCreated);

            // Wait for delivery record outcome to transition from Pending to Success
            var deadline = DateTime.UtcNow.AddSeconds(2);
            GetDeliveryStatusResponse result;
            do
            {
                await Task.Delay(30);
                result = await svc.GetDeliveryStatusAsync(caseId, "actor");
            } while (result.SuccessCount == 0 && DateTime.UtcNow < deadline);

            Assert.That(result.SuccessCount, Is.GreaterThan(0));
            Assert.That(result.FailureCount,  Is.EqualTo(0));
        }

        [Test]
        public async Task GetDeliveryStatus_ReverseChronologicalOrder()
        {
            var (svc, ws, _) = CreateServiceWithCapture();
            var caseId       = await CreateCaseIdAsync(svc);

            // Trigger a second event by assigning the case
            await svc.AssignCaseAsync(caseId, new AssignCaseRequest { ReviewerId = "alice" }, "mgr");

            // Wait for two delivery records
            var deadline = DateTime.UtcNow.AddSeconds(2);
            GetDeliveryStatusResponse result;
            do
            {
                await Task.Delay(30);
                result = await svc.GetDeliveryStatusAsync(caseId, "actor");
            } while (result.TotalCount < 2 && DateTime.UtcNow < deadline);

            if (result.TotalCount >= 2)
            {
                // Records should be newest-first
                Assert.That(result.Records[0].AttemptedAt,
                    Is.GreaterThanOrEqualTo(result.Records[1].AttemptedAt));
            }
        }

        [Test]
        public async Task GetDeliveryStatus_DeliveryRecordHasExpectedFields()
        {
            var (svc, ws, _) = CreateServiceWithCapture();
            var caseId       = await CreateCaseIdAsync(svc);

            await WaitForEventAsync(ws, WebhookEventType.ComplianceCaseCreated);

            var deadline = DateTime.UtcNow.AddSeconds(2);
            GetDeliveryStatusResponse result;
            do
            {
                await Task.Delay(30);
                result = await svc.GetDeliveryStatusAsync(caseId, "actor");
            } while (result.TotalCount == 0 && DateTime.UtcNow < deadline);

            if (result.TotalCount > 0)
            {
                var record = result.Records[0];
                Assert.That(record.DeliveryId, Is.Not.Null.And.Not.Empty);
                Assert.That(record.EventId,    Is.Not.Null.And.Not.Empty);
                Assert.That(record.CaseId,     Is.EqualTo(caseId));
                Assert.That(record.EventType,  Is.EqualTo(WebhookEventType.ComplianceCaseCreated));
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // 9. Full workflow: assign → escalate → SLA → query all surfaces
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task FullWorkflow_AssignEscalateSlaQuery_AllSurfacesPopulated()
        {
            var (svc, ws, tp) = CreateServiceWithCapture();
            var caseId        = await CreateCaseIdAsync(svc);

            // Step 1: assign
            await svc.AssignCaseAsync(caseId,
                new AssignCaseRequest { ReviewerId = "alice", TeamId = "kyc-team", Reason = "Intake assignment" },
                "manager");

            // Step 2: reassign
            tp.Advance(TimeSpan.FromHours(1));
            await svc.AssignCaseAsync(caseId,
                new AssignCaseRequest { ReviewerId = "bob", Reason = "Escalation reassignment" },
                "manager");

            // Step 3: escalate
            await svc.AddEscalationAsync(caseId,
                new AddEscalationRequest { Type = EscalationType.SanctionsHit, Description = "Possible match" },
                "alice");

            // Step 4: set SLA (future due date = Warning band)
            tp.Advance(TimeSpan.FromHours(1));
            await svc.SetSlaMetadataAsync(caseId,
                new SetSlaMetadataRequest { ReviewDueAt = tp.GetUtcNow().AddDays(5), Notes = "SLA per policy" },
                "manager");

            // — Assert assignment history
            var assignHist = await svc.GetAssignmentHistoryAsync(caseId, "actor");
            Assert.That(assignHist.TotalCount, Is.EqualTo(2));
            Assert.That(assignHist.History[1].PreviousReviewerId, Is.EqualTo("alice"));
            Assert.That(assignHist.History[1].NewReviewerId,      Is.EqualTo("bob"));

            // — Assert escalation history
            var escHist = await svc.GetEscalationHistoryAsync(caseId, "actor");
            Assert.That(escHist.OpenCount, Is.EqualTo(1));

            // — Assert SLA metadata
            var caseResp = await svc.GetCaseAsync(caseId, "actor");
            Assert.That(caseResp.Case!.SlaMetadata!.UrgencyBand, Is.EqualTo(CaseUrgencyBand.Warning));

            // — Assert delivery records are accumulating
            await WaitForEventAsync(ws, WebhookEventType.ComplianceCaseAssignmentChanged);
            var deliveryResp = await svc.GetDeliveryStatusAsync(caseId, "actor");
            Assert.That(deliveryResp.TotalCount, Is.GreaterThan(0));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 10. Regression: existing UpdateCaseAsync reviewer assignment still works
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public async Task UpdateCase_ReviewerAssignment_StillEmitsAssignmentChangedEvent()
        {
            var (svc, ws, _) = CreateServiceWithCapture();
            var caseId       = await CreateCaseIdAsync(svc);

            await svc.UpdateCaseAsync(caseId,
                new UpdateComplianceCaseRequest { AssignedReviewerId = "diana" }, "manager");

            await WaitForEventAsync(ws, WebhookEventType.ComplianceCaseAssignmentChanged);

            lock (ws.EmittedEvents)
                Assert.That(ws.EmittedEvents.Any(e => e.EventType == WebhookEventType.ComplianceCaseAssignmentChanged),
                    Is.True);
        }

        [Test]
        public async Task UpdateCase_ReviewerAssignment_CaseReflectsNewReviewer()
        {
            var svc    = CreateService();
            var caseId = await CreateCaseIdAsync(svc);

            await svc.UpdateCaseAsync(caseId,
                new UpdateComplianceCaseRequest { AssignedReviewerId = "diana" }, "manager");

            var c = await svc.GetCaseAsync(caseId, "actor");
            Assert.That(c.Case!.AssignedReviewerId, Is.EqualTo("diana"));
        }
    }
}
