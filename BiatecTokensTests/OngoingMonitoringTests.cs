using BiatecTokensApi.Models;
using BiatecTokensApi.Models.OngoingMonitoring;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive tests for <see cref="OngoingMonitoringService"/> and
    /// <see cref="BiatecTokensApi.Controllers.OngoingMonitoringController"/>.
    ///
    /// Coverage:
    ///   - Unit tests: all service methods, validation, state transitions
    ///   - Due-date status calculation (Healthy, DueSoon, Overdue)
    ///   - Lifecycle transitions: create → start → defer → escalate → close
    ///   - Webhook event emission for each lifecycle event
    ///   - Fail-closed validation: missing required fields, invalid dates, terminal states
    ///   - RunDueDateCheck: overdue detection, DueSoon detection, deferral expiry
    ///   - Negative paths: terminal state re-transitions, missing rationale, past dates
    ///   - Integration tests: full HTTP pipeline via WebApplicationFactory
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class OngoingMonitoringTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // FakeTimeProvider
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset initial) => _now = initial;
            public void Advance(TimeSpan delta)            => _now = _now.Add(delta);
            public void SetUtcNow(DateTimeOffset value)    => _now = value;
            public override DateTimeOffset GetUtcNow()     => _now;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Capturing webhook service
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class CapturingWebhookService : IWebhookService
        {
            public List<WebhookEvent> EmittedEvents { get; } = new();

            public Task EmitEventAsync(WebhookEvent webhookEvent)
            {
                lock (EmittedEvents) EmittedEvents.Add(webhookEvent);
                return Task.CompletedTask;
            }

            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> CreateSubscriptionAsync(
                BiatecTokensApi.Models.Webhook.CreateWebhookSubscriptionRequest request, string createdBy)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse { Success = true });

            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> GetSubscriptionAsync(
                string subscriptionId, string userId)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse { Success = false });

            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionListResponse> ListSubscriptionsAsync(string userId)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionListResponse { Success = true });

            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> UpdateSubscriptionAsync(
                BiatecTokensApi.Models.Webhook.UpdateWebhookSubscriptionRequest request, string userId)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse { Success = true });

            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> DeleteSubscriptionAsync(
                string subscriptionId, string userId)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse { Success = true });

            public Task<BiatecTokensApi.Models.Webhook.WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(
                BiatecTokensApi.Models.Webhook.GetWebhookDeliveryHistoryRequest request, string userId)
                => Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookDeliveryHistoryResponse { Success = true });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helper factories
        // ═══════════════════════════════════════════════════════════════════════

        private static OngoingMonitoringService CreateService(
            TimeProvider? timeProvider = null,
            IWebhookService? webhookService = null)
        {
            return new OngoingMonitoringService(
                NullLogger<OngoingMonitoringService>.Instance,
                timeProvider,
                webhookService);
        }

        private static CreateMonitoringTaskRequest BuildCreateRequest(
            string? caseId = null,
            string? issuerId = null,
            string? subjectId = null,
            ReassessmentReason reason = ReassessmentReason.PeriodicSchedule,
            MonitoringTaskSeverity severity = MonitoringTaskSeverity.Low,
            DateTimeOffset? dueAt = null)
        {
            return new CreateMonitoringTaskRequest
            {
                CaseId    = caseId    ?? "case-" + Guid.NewGuid().ToString("N")[..8],
                IssuerId  = issuerId  ?? "issuer-" + Guid.NewGuid().ToString("N")[..8],
                SubjectId = subjectId ?? "subject-" + Guid.NewGuid().ToString("N")[..8],
                Reason    = reason,
                Severity  = severity,
                DueAt     = dueAt ?? DateTimeOffset.UtcNow.AddDays(30)
            };
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: CreateTaskAsync — validation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateTask_ValidRequest_ReturnsSuccess()
        {
            var svc = CreateService();
            var req = BuildCreateRequest();
            var result = await svc.CreateTaskAsync(req, "analyst@example.com");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Task, Is.Not.Null);
            Assert.That(result.Task!.TaskId, Is.Not.Empty);
            Assert.That(result.Task.Status, Is.EqualTo(MonitoringTaskStatus.Healthy));
            Assert.That(result.Task.CaseId, Is.EqualTo(req.CaseId));
            Assert.That(result.Task.IssuerId, Is.EqualTo(req.IssuerId));
            Assert.That(result.Task.Reason, Is.EqualTo(ReassessmentReason.PeriodicSchedule));
            Assert.That(result.Task.Timeline.Count, Is.EqualTo(1));
            Assert.That(result.Task.Timeline[0].EventType, Is.EqualTo(MonitoringTaskEventType.TaskCreated));
        }

        [Test]
        public async Task CreateTask_MissingCaseId_ReturnsError()
        {
            var svc = CreateService();
            var req = BuildCreateRequest(caseId: "");
            var result = await svc.CreateTaskAsync(req, "analyst");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.MISSING_REQUIRED_FIELD));
        }

        [Test]
        public async Task CreateTask_MissingIssuerId_ReturnsError()
        {
            var svc = CreateService();
            var req = BuildCreateRequest(issuerId: "");
            var result = await svc.CreateTaskAsync(req, "analyst");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.MISSING_REQUIRED_FIELD));
        }

        [Test]
        public async Task CreateTask_MissingSubjectId_ReturnsError()
        {
            var svc = CreateService();
            var req = BuildCreateRequest(subjectId: "");
            var result = await svc.CreateTaskAsync(req, "analyst");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.MISSING_REQUIRED_FIELD));
        }

        [Test]
        public async Task CreateTask_DueAtInPast_ReturnsError()
        {
            var svc = CreateService();
            var req = BuildCreateRequest(dueAt: DateTimeOffset.UtcNow.AddDays(-1));
            var result = await svc.CreateTaskAsync(req, "analyst");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_FIELD_VALUE));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: Initial status determination based on DueAt
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateTask_DueMoreThan7DaysAway_IsHealthy()
        {
            var tp  = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: tp);
            var req = BuildCreateRequest(dueAt: tp.GetUtcNow().AddDays(30));
            var result = await svc.CreateTaskAsync(req, "a");

            Assert.That(result.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Healthy));
        }

        [Test]
        public async Task CreateTask_DueWithin7Days_IsDueSoon()
        {
            var tp  = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: tp);
            var req = BuildCreateRequest(dueAt: tp.GetUtcNow().AddDays(5));
            var result = await svc.CreateTaskAsync(req, "a");

            Assert.That(result.Task!.Status, Is.EqualTo(MonitoringTaskStatus.DueSoon));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: GetTaskAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetTask_ExistingTask_ReturnsTask()
        {
            var svc    = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.GetTaskAsync(taskId, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Task!.TaskId, Is.EqualTo(taskId));
        }

        [Test]
        public async Task GetTask_NonExistentTask_ReturnsNotFound()
        {
            var svc    = CreateService();
            var result = await svc.GetTaskAsync("non-existent-id", "a");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.NOT_FOUND));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: ListTasksAsync — filtering and pagination
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ListTasks_NoFilter_ReturnsAllTasks()
        {
            var svc = CreateService();
            var issuerId = "issuer-list-test-" + Guid.NewGuid().ToString("N")[..6];

            await svc.CreateTaskAsync(BuildCreateRequest(issuerId: issuerId), "a");
            await svc.CreateTaskAsync(BuildCreateRequest(issuerId: issuerId), "a");

            var result = await svc.ListTasksAsync(new ListMonitoringTasksRequest
            {
                IssuerId = issuerId
            }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.Tasks.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task ListTasks_FilterByStatus_ReturnsMatchingTasks()
        {
            var tp       = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc      = CreateService(timeProvider: tp);
            var issuerId = "issuer-" + Guid.NewGuid().ToString("N")[..6];

            // Create one healthy and one due-soon task
            await svc.CreateTaskAsync(BuildCreateRequest(
                issuerId: issuerId, dueAt: tp.GetUtcNow().AddDays(30)), "a"); // Healthy
            await svc.CreateTaskAsync(BuildCreateRequest(
                issuerId: issuerId, dueAt: tp.GetUtcNow().AddDays(3)), "a");  // DueSoon

            var result = await svc.ListTasksAsync(new ListMonitoringTasksRequest
            {
                IssuerId = issuerId,
                Status   = MonitoringTaskStatus.DueSoon
            }, "a");

            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Tasks[0].Status, Is.EqualTo(MonitoringTaskStatus.DueSoon));
        }

        [Test]
        public async Task ListTasks_FilterByCaseId_ReturnsMatchingTasks()
        {
            var svc    = CreateService();
            var caseId = "case-" + Guid.NewGuid().ToString("N")[..8];

            await svc.CreateTaskAsync(BuildCreateRequest(caseId: caseId), "a");
            await svc.CreateTaskAsync(BuildCreateRequest(caseId: caseId), "a");
            await svc.CreateTaskAsync(BuildCreateRequest(), "a"); // different case

            var result = await svc.ListTasksAsync(new ListMonitoringTasksRequest { CaseId = caseId }, "a");

            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.Tasks.All(t => t.CaseId == caseId), Is.True);
        }

        [Test]
        public async Task ListTasks_Pagination_ReturnsCorrectPage()
        {
            var svc      = CreateService();
            var issuerId = "issuer-page-" + Guid.NewGuid().ToString("N")[..6];

            for (int i = 0; i < 5; i++)
                await svc.CreateTaskAsync(BuildCreateRequest(issuerId: issuerId), "a");

            var page1 = await svc.ListTasksAsync(new ListMonitoringTasksRequest
            {
                IssuerId   = issuerId,
                PageNumber = 1,
                PageSize   = 2
            }, "a");

            var page2 = await svc.ListTasksAsync(new ListMonitoringTasksRequest
            {
                IssuerId   = issuerId,
                PageNumber = 2,
                PageSize   = 2
            }, "a");

            Assert.That(page1.TotalCount, Is.EqualTo(5));
            Assert.That(page1.Tasks.Count, Is.EqualTo(2));
            Assert.That(page2.Tasks.Count, Is.EqualTo(2));
            // Pages should return distinct tasks
            Assert.That(page1.Tasks.Select(t => t.TaskId).Intersect(page2.Tasks.Select(t => t.TaskId)).Any(), Is.False);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: StartReassessmentAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task StartReassessment_HealthyTask_TransitionsToInProgress()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.StartReassessmentAsync(taskId,
                new StartReassessmentRequest { Notes = "Starting annual review" }, "analyst");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Task!.Status, Is.EqualTo(MonitoringTaskStatus.InProgress));
            Assert.That(result.Task.Timeline.Any(e => e.EventType == MonitoringTaskEventType.ReassessmentStarted), Is.True);
        }

        [Test]
        public async Task StartReassessment_NonExistentTask_ReturnsNotFound()
        {
            var svc    = CreateService();
            var result = await svc.StartReassessmentAsync("bad-id",
                new StartReassessmentRequest(), "analyst");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.NOT_FOUND));
        }

        [Test]
        public async Task StartReassessment_AlreadyInProgress_ReturnsPreconditionFailed()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.StartReassessmentAsync(taskId, new StartReassessmentRequest(), "a");
            var result = await svc.StartReassessmentAsync(taskId, new StartReassessmentRequest(), "a");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.PRECONDITION_FAILED));
        }

        [Test]
        public async Task StartReassessment_ResolvedTask_ReturnsInvalidStateTransition()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            // Close the task first
            await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = "All clear"
                }, "a");

            var result = await svc.StartReassessmentAsync(taskId, new StartReassessmentRequest(), "a");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_STATE_TRANSITION));
        }

        [Test]
        public async Task StartReassessment_SetsAssignedTo_WhenProvided()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.StartReassessmentAsync(taskId,
                new StartReassessmentRequest { AssignedTo = "analyst@firm.com" }, "a");

            Assert.That(result.Task!.AssignedTo, Is.EqualTo("analyst@firm.com"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: DeferTaskAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DeferTask_ValidRequest_TransitionsToDeferred()
        {
            var tp      = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc     = CreateService(timeProvider: tp);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(dueAt: tp.GetUtcNow().AddDays(30)), "a");
            var taskId  = created.Task!.TaskId;

            var deferUntil = tp.GetUtcNow().AddDays(14);
            var result = await svc.DeferTaskAsync(taskId,
                new DeferMonitoringTaskRequest
                {
                    DeferUntil = deferUntil,
                    Rationale  = "Subject travelling abroad; documents unavailable until return."
                }, "analyst");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Deferred));
            Assert.That(result.Task.DeferredUntil, Is.EqualTo(deferUntil));
            Assert.That(result.Task.DeferralRationale, Is.Not.Empty);
            Assert.That(result.Task.Timeline.Any(e => e.EventType == MonitoringTaskEventType.TaskDeferred), Is.True);
        }

        [Test]
        public async Task DeferTask_MissingRationale_ReturnsError()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.DeferTaskAsync(taskId,
                new DeferMonitoringTaskRequest
                {
                    DeferUntil = DateTimeOffset.UtcNow.AddDays(14),
                    Rationale  = ""
                }, "analyst");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.MISSING_REQUIRED_FIELD));
        }

        [Test]
        public async Task DeferTask_PastDeferUntil_ReturnsError()
        {
            var tp      = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc     = CreateService(timeProvider: tp);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(dueAt: tp.GetUtcNow().AddDays(30)), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.DeferTaskAsync(taskId,
                new DeferMonitoringTaskRequest
                {
                    DeferUntil = tp.GetUtcNow().AddDays(-1),
                    Rationale  = "Should fail"
                }, "analyst");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_FIELD_VALUE));
        }

        [Test]
        public async Task DeferTask_ResolvedTask_ReturnsInvalidStateTransition()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = "All clear"
                }, "a");

            var result = await svc.DeferTaskAsync(taskId,
                new DeferMonitoringTaskRequest
                {
                    DeferUntil = DateTimeOffset.UtcNow.AddDays(7),
                    Rationale  = "Should fail"
                }, "analyst");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_STATE_TRANSITION));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: EscalateTaskAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EscalateTask_ValidRequest_TransitionsToEscalated()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.EscalateTaskAsync(taskId,
                new EscalateMonitoringTaskRequest
                {
                    EscalationReason = "Potential sanctions match detected during rescreening.",
                    Severity         = MonitoringTaskSeverity.Critical
                }, "senior-analyst");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Escalated));
            Assert.That(result.Task.EscalationReason, Is.EqualTo("Potential sanctions match detected during rescreening."));
            Assert.That(result.Task.Severity, Is.EqualTo(MonitoringTaskSeverity.Critical));
            Assert.That(result.Task.Timeline.Any(e => e.EventType == MonitoringTaskEventType.TaskEscalated), Is.True);
        }

        [Test]
        public async Task EscalateTask_MissingReason_ReturnsError()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.EscalateTaskAsync(taskId,
                new EscalateMonitoringTaskRequest { EscalationReason = "" }, "analyst");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.MISSING_REQUIRED_FIELD));
        }

        [Test]
        public async Task EscalateTask_ResolvedTask_ReturnsInvalidStateTransition()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = "All clear"
                }, "a");

            var result = await svc.EscalateTaskAsync(taskId,
                new EscalateMonitoringTaskRequest { EscalationReason = "New info" }, "analyst");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_STATE_TRANSITION));
        }

        [Test]
        public async Task EscalateTask_CanEscalateInProgressTask()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.StartReassessmentAsync(taskId, new StartReassessmentRequest(), "a");

            var result = await svc.EscalateTaskAsync(taskId,
                new EscalateMonitoringTaskRequest { EscalationReason = "Critical finding" }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Escalated));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: CloseTaskAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CloseTask_WithClearResolution_TransitionsToResolved()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = "Subject reviewed; no adverse findings."
                }, "analyst");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Resolved));
            Assert.That(result.Task.ResolutionOutcome, Is.EqualTo(MonitoringTaskResolution.Clear));
            Assert.That(result.Task.CompletedAt, Is.Not.Null);
            Assert.That(result.Task.Timeline.Any(e => e.EventType == MonitoringTaskEventType.TaskResolved), Is.True);
        }

        [Test]
        public async Task CloseTask_WithSubjectSuspended_TransitionsToSuspended()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.SubjectSuspended,
                    ResolutionNotes = "Confirmed sanctions match requires immediate suspension."
                }, "compliance-officer");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Suspended));
            Assert.That(result.Task.Timeline.Any(e => e.EventType == MonitoringTaskEventType.SubjectSuspended), Is.True);
        }

        [Test]
        public async Task CloseTask_WithSubjectRestricted_TransitionsToRestricted()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.SubjectRestricted,
                    ResolutionNotes = "Irreconcilable compliance breach; subject restricted."
                }, "compliance-officer");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Restricted));
            Assert.That(result.Task.Timeline.Any(e => e.EventType == MonitoringTaskEventType.SubjectRestricted), Is.True);
        }

        [Test]
        public async Task CloseTask_MissingResolutionNotes_ReturnsError()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = ""
                }, "analyst");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.MISSING_REQUIRED_FIELD));
        }

        [Test]
        public async Task CloseTask_AlreadyResolved_ReturnsInvalidStateTransition()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = "First close"
                }, "a");

            var result = await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = "Second close — should fail"
                }, "a");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_STATE_TRANSITION));
        }

        [Test]
        public async Task CloseTask_SuspendedTask_CannotBeClosedAgain()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.SubjectSuspended,
                    ResolutionNotes = "Suspended"
                }, "a");

            var result = await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = "Should fail"
                }, "a");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_STATE_TRANSITION));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: Full lifecycle — create → start → escalate → close
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task FullLifecycle_CreateStartEscalateClose_ProducesAuditTrail()
        {
            var svc = CreateService();

            // 1. Create
            var created = await svc.CreateTaskAsync(BuildCreateRequest(
                reason: ReassessmentReason.SanctionsRefresh,
                severity: MonitoringTaskSeverity.High), "system");
            var taskId = created.Task!.TaskId;

            Assert.That(created.Task.Status, Is.EqualTo(MonitoringTaskStatus.Healthy));

            // 2. Start reassessment
            var started = await svc.StartReassessmentAsync(taskId,
                new StartReassessmentRequest { Notes = "Sanctions list updated" }, "analyst");
            Assert.That(started.Task!.Status, Is.EqualTo(MonitoringTaskStatus.InProgress));

            // 3. Escalate
            var escalated = await svc.EscalateTaskAsync(taskId,
                new EscalateMonitoringTaskRequest
                {
                    EscalationReason = "Name match on OFAC list",
                    Severity         = MonitoringTaskSeverity.Critical
                }, "senior-analyst");
            Assert.That(escalated.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Escalated));

            // 4. Close
            var closed = await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.SubjectSuspended,
                    ResolutionNotes = "OFAC match confirmed; subject suspended pending investigation."
                }, "compliance-officer");

            Assert.That(closed.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Suspended));
            Assert.That(closed.Task.CompletedAt, Is.Not.Null);

            // Audit trail must be chronological and complete
            var timeline = closed.Task.Timeline;
            Assert.That(timeline.Count, Is.EqualTo(4));
            Assert.That(timeline[0].EventType, Is.EqualTo(MonitoringTaskEventType.TaskCreated));
            Assert.That(timeline[1].EventType, Is.EqualTo(MonitoringTaskEventType.ReassessmentStarted));
            Assert.That(timeline[2].EventType, Is.EqualTo(MonitoringTaskEventType.TaskEscalated));
            Assert.That(timeline[3].EventType, Is.EqualTo(MonitoringTaskEventType.SubjectSuspended));
        }

        [Test]
        public async Task FullLifecycle_CreateDeferResurface_Works()
        {
            var tp  = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: tp);

            var created = await svc.CreateTaskAsync(BuildCreateRequest(
                dueAt: tp.GetUtcNow().AddDays(60)), "system");
            var taskId = created.Task!.TaskId;

            // Defer
            var deferred = await svc.DeferTaskAsync(taskId,
                new DeferMonitoringTaskRequest
                {
                    DeferUntil = tp.GetUtcNow().AddDays(15),
                    Rationale  = "Subject out of jurisdiction temporarily."
                }, "analyst");
            Assert.That(deferred.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Deferred));

            // Advance time past the deferral date
            tp.Advance(TimeSpan.FromDays(16));

            // Run due-date check to resurface
            var updated = await svc.RunDueDateCheckAsync();
            Assert.That(updated, Is.EqualTo(1));

            var retrieved = await svc.GetTaskAsync(taskId, "a");
            // DueAt is still 44 days away (60 - 16), so status should be Healthy
            Assert.That(retrieved.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Healthy));
            Assert.That(retrieved.Task.Timeline.Any(e => e.EventType == MonitoringTaskEventType.DeferralExpired), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: RunDueDateCheckAsync
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task RunDueDateCheck_OverdueTask_MarksAsOverdue()
        {
            var tp      = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc     = CreateService(timeProvider: tp);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(
                dueAt: tp.GetUtcNow().AddDays(30)), "a");
            var taskId = created.Task!.TaskId;

            // Advance time past due date
            tp.Advance(TimeSpan.FromDays(31));

            var count = await svc.RunDueDateCheckAsync();
            Assert.That(count, Is.EqualTo(1));

            var task = (await svc.GetTaskAsync(taskId, "a")).Task!;
            Assert.That(task.Status, Is.EqualTo(MonitoringTaskStatus.Overdue));
        }

        [Test]
        public async Task RunDueDateCheck_DueSoonTask_MarksAsDueSoon()
        {
            var tp      = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc     = CreateService(timeProvider: tp);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(
                dueAt: tp.GetUtcNow().AddDays(30)), "a");
            var taskId = created.Task!.TaskId;

            // Advance time to within 7 days of due date
            tp.Advance(TimeSpan.FromDays(24));

            var count = await svc.RunDueDateCheckAsync();
            Assert.That(count, Is.EqualTo(1));

            var task = (await svc.GetTaskAsync(taskId, "a")).Task!;
            Assert.That(task.Status, Is.EqualTo(MonitoringTaskStatus.DueSoon));
        }

        [Test]
        public async Task RunDueDateCheck_TerminalTasks_AreNotUpdated()
        {
            var tp      = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc     = CreateService(timeProvider: tp);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(
                dueAt: tp.GetUtcNow().AddDays(30)), "a");
            var taskId = created.Task!.TaskId;

            await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = "Closed"
                }, "a");

            // Advance time past due date
            tp.Advance(TimeSpan.FromDays(35));

            var count = await svc.RunDueDateCheckAsync();
            Assert.That(count, Is.EqualTo(0)); // Terminal tasks not updated

            var task = (await svc.GetTaskAsync(taskId, "a")).Task!;
            Assert.That(task.Status, Is.EqualTo(MonitoringTaskStatus.Resolved)); // Unchanged
        }

        [Test]
        public async Task RunDueDateCheck_EscalatedTasks_AreNotDowngraded()
        {
            var tp      = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc     = CreateService(timeProvider: tp);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(
                dueAt: tp.GetUtcNow().AddDays(30)), "a");
            var taskId = created.Task!.TaskId;

            await svc.EscalateTaskAsync(taskId,
                new EscalateMonitoringTaskRequest { EscalationReason = "Risk signal" }, "a");

            // Advance time past due date
            tp.Advance(TimeSpan.FromDays(35));

            await svc.RunDueDateCheckAsync();

            var task = (await svc.GetTaskAsync(taskId, "a")).Task!;
            // Escalated status must not be overwritten by due-date check
            Assert.That(task.Status, Is.EqualTo(MonitoringTaskStatus.Escalated));
        }

        [Test]
        public async Task RunDueDateCheck_DeferredTaskWithElapsedDeferral_Resurfaces()
        {
            var tp      = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc     = CreateService(timeProvider: tp);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(
                dueAt: tp.GetUtcNow().AddDays(50)), "a");
            var taskId = created.Task!.TaskId;

            await svc.DeferTaskAsync(taskId,
                new DeferMonitoringTaskRequest
                {
                    DeferUntil = tp.GetUtcNow().AddDays(10),
                    Rationale  = "Temporary deferral"
                }, "a");

            tp.Advance(TimeSpan.FromDays(11)); // past deferral date, still before DueAt

            var count = await svc.RunDueDateCheckAsync();
            Assert.That(count, Is.EqualTo(1));

            var task = (await svc.GetTaskAsync(taskId, "a")).Task!;
            // DueAt is still 39 days away; should resurface as Healthy
            Assert.That(task.Status, Is.EqualTo(MonitoringTaskStatus.Healthy));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: Reassessment reasons — all enum values can be created
        // ═══════════════════════════════════════════════════════════════════════

        [TestCase(ReassessmentReason.PeriodicSchedule)]
        [TestCase(ReassessmentReason.DocumentExpiry)]
        [TestCase(ReassessmentReason.SanctionsRefresh)]
        [TestCase(ReassessmentReason.AmlRescreening)]
        [TestCase(ReassessmentReason.RiskSignalElevated)]
        [TestCase(ReassessmentReason.ManualAnalystRequest)]
        [TestCase(ReassessmentReason.EscalationFollowUp)]
        [TestCase(ReassessmentReason.WebhookSignal)]
        [TestCase(ReassessmentReason.JurisdictionChange)]
        public async Task CreateTask_AllReassessmentReasons_Succeed(ReassessmentReason reason)
        {
            var svc    = CreateService();
            var req    = BuildCreateRequest(reason: reason);
            var result = await svc.CreateTaskAsync(req, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Task!.Reason, Is.EqualTo(reason));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: Resolution outcomes — all enum values can close a task
        // ═══════════════════════════════════════════════════════════════════════

        [TestCase(MonitoringTaskResolution.Clear,                      MonitoringTaskStatus.Resolved)]
        [TestCase(MonitoringTaskResolution.ActionTaken,                MonitoringTaskStatus.Resolved)]
        [TestCase(MonitoringTaskResolution.Deferred,                   MonitoringTaskStatus.Resolved)]
        [TestCase(MonitoringTaskResolution.EscalatedToHigherAuthority, MonitoringTaskStatus.Resolved)]
        [TestCase(MonitoringTaskResolution.SubjectSuspended,           MonitoringTaskStatus.Suspended)]
        [TestCase(MonitoringTaskResolution.SubjectRestricted,          MonitoringTaskStatus.Restricted)]
        public async Task CloseTask_AllResolutionOutcomes_ProduceCorrectStatus(
            MonitoringTaskResolution resolution, MonitoringTaskStatus expectedStatus)
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = resolution,
                    ResolutionNotes = $"Closed with {resolution}"
                }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Task!.Status, Is.EqualTo(expectedStatus));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: Webhook event emission
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateTask_EmitsMonitoringTaskCreatedEvent()
        {
            var ws  = new CapturingWebhookService();
            var svc = CreateService(webhookService: ws);

            await svc.CreateTaskAsync(BuildCreateRequest(), "a");

            // Allow fire-and-forget to complete
            await Task.Delay(100);

            Assert.That(ws.EmittedEvents.Any(e => e.EventType == WebhookEventType.MonitoringTaskCreated), Is.True);
        }

        [Test]
        public async Task StartReassessment_EmitsReassessmentStartedEvent()
        {
            var ws      = new CapturingWebhookService();
            var svc     = CreateService(webhookService: ws);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.StartReassessmentAsync(taskId, new StartReassessmentRequest(), "a");
            await Task.Delay(100);

            Assert.That(ws.EmittedEvents.Any(e =>
                e.EventType == WebhookEventType.MonitoringTaskReassessmentStarted), Is.True);
        }

        [Test]
        public async Task EscalateTask_EmitsEscalatedEvent()
        {
            var ws      = new CapturingWebhookService();
            var svc     = CreateService(webhookService: ws);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.EscalateTaskAsync(taskId,
                new EscalateMonitoringTaskRequest { EscalationReason = "Test" }, "a");
            await Task.Delay(100);

            Assert.That(ws.EmittedEvents.Any(e =>
                e.EventType == WebhookEventType.MonitoringTaskEscalated), Is.True);
        }

        [Test]
        public async Task DeferTask_EmitsDeferredEvent()
        {
            var ws      = new CapturingWebhookService();
            var svc     = CreateService(webhookService: ws);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.DeferTaskAsync(taskId,
                new DeferMonitoringTaskRequest
                {
                    DeferUntil = DateTimeOffset.UtcNow.AddDays(10),
                    Rationale  = "Deferred for test"
                }, "a");
            await Task.Delay(100);

            Assert.That(ws.EmittedEvents.Any(e =>
                e.EventType == WebhookEventType.MonitoringTaskDeferred), Is.True);
        }

        [Test]
        public async Task CloseTask_Clear_EmitsResolvedEvent()
        {
            var ws      = new CapturingWebhookService();
            var svc     = CreateService(webhookService: ws);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = "Clear"
                }, "a");
            await Task.Delay(100);

            Assert.That(ws.EmittedEvents.Any(e =>
                e.EventType == WebhookEventType.MonitoringTaskResolved), Is.True);
        }

        [Test]
        public async Task CloseTask_SubjectSuspended_EmitsSuspendedEvent()
        {
            var ws      = new CapturingWebhookService();
            var svc     = CreateService(webhookService: ws);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.SubjectSuspended,
                    ResolutionNotes = "Suspended"
                }, "a");
            await Task.Delay(100);

            Assert.That(ws.EmittedEvents.Any(e =>
                e.EventType == WebhookEventType.MonitoringTaskSubjectSuspended), Is.True);
        }

        [Test]
        public async Task CloseTask_SubjectRestricted_EmitsRestrictedEvent()
        {
            var ws      = new CapturingWebhookService();
            var svc     = CreateService(webhookService: ws);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(), "a");
            var taskId  = created.Task!.TaskId;

            await svc.CloseTaskAsync(taskId,
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.SubjectRestricted,
                    ResolutionNotes = "Restricted"
                }, "a");
            await Task.Delay(100);

            Assert.That(ws.EmittedEvents.Any(e =>
                e.EventType == WebhookEventType.MonitoringTaskSubjectRestricted), Is.True);
        }

        [Test]
        public async Task RunDueDateCheck_Overdue_EmitsOverdueEvent()
        {
            var tp      = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var ws      = new CapturingWebhookService();
            var svc     = CreateService(timeProvider: tp, webhookService: ws);

            await svc.CreateTaskAsync(BuildCreateRequest(dueAt: tp.GetUtcNow().AddDays(1)), "a");
            tp.Advance(TimeSpan.FromDays(2));

            await svc.RunDueDateCheckAsync();
            await Task.Delay(100);

            Assert.That(ws.EmittedEvents.Any(e =>
                e.EventType == WebhookEventType.MonitoringTaskOverdue), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: RequiredEvidenceTypes
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CreateTask_WithRequiredEvidenceTypes_PersistedOnTask()
        {
            var svc = CreateService();
            var req = BuildCreateRequest();
            req.RequiredEvidenceTypes = new List<string> { "KYC", "AML", "Sanctions" };

            var result = await svc.CreateTaskAsync(req, "a");

            Assert.That(result.Task!.RequiredEvidenceTypes, Has.Count.EqualTo(3));
            Assert.That(result.Task.RequiredEvidenceTypes, Contains.Item("KYC"));
        }

        [Test]
        public async Task StartReassessment_OverridesRequiredEvidenceTypes_WhenProvided()
        {
            var svc = CreateService();
            var req = BuildCreateRequest();
            req.RequiredEvidenceTypes = new List<string> { "KYC" };

            var created = await svc.CreateTaskAsync(req, "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.StartReassessmentAsync(taskId,
                new StartReassessmentRequest
                {
                    RequiredEvidenceTypes = new List<string> { "KYC", "AML", "Enhanced-Due-Diligence" }
                }, "a");

            Assert.That(result.Task!.RequiredEvidenceTypes, Has.Count.EqualTo(3));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: Severity escalation path
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EscalateTask_WithSeverityOverride_RaisesSeverity()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(
                BuildCreateRequest(severity: MonitoringTaskSeverity.Low), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.EscalateTaskAsync(taskId,
                new EscalateMonitoringTaskRequest
                {
                    EscalationReason = "Elevated risk",
                    Severity         = MonitoringTaskSeverity.Critical
                }, "a");

            Assert.That(result.Task!.Severity, Is.EqualTo(MonitoringTaskSeverity.Critical));
        }

        [Test]
        public async Task EscalateTask_WithoutSeverityOverride_PreservesExistingSeverity()
        {
            var svc     = CreateService();
            var created = await svc.CreateTaskAsync(
                BuildCreateRequest(severity: MonitoringTaskSeverity.Medium), "a");
            var taskId  = created.Task!.TaskId;

            var result = await svc.EscalateTaskAsync(taskId,
                new EscalateMonitoringTaskRequest { EscalationReason = "Elevated risk" }, "a");

            Assert.That(result.Task!.Severity, Is.EqualTo(MonitoringTaskSeverity.Medium));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UNIT: Edge cases
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ListTasks_EmptyStore_ReturnsEmptyList()
        {
            var svc    = CreateService();
            var result = await svc.ListTasksAsync(
                new ListMonitoringTasksRequest { IssuerId = "non-existent" }, "a");

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(0));
            Assert.That(result.Tasks, Is.Empty);
        }

        [Test]
        public async Task RunDueDateCheck_EmptyStore_ReturnsZero()
        {
            var svc   = CreateService();
            var count = await svc.RunDueDateCheckAsync();

            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public async Task RunDueDateCheck_InProgressTask_IsNotUpdated()
        {
            var tp      = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc     = CreateService(timeProvider: tp);
            var created = await svc.CreateTaskAsync(BuildCreateRequest(
                dueAt: tp.GetUtcNow().AddDays(30)), "a");
            var taskId = created.Task!.TaskId;

            await svc.StartReassessmentAsync(taskId, new StartReassessmentRequest(), "a");

            tp.Advance(TimeSpan.FromDays(31));

            var count = await svc.RunDueDateCheckAsync();
            Assert.That(count, Is.EqualTo(0));

            var task = (await svc.GetTaskAsync(taskId, "a")).Task!;
            // InProgress tasks must not be auto-advanced to Overdue
            Assert.That(task.Status, Is.EqualTo(MonitoringTaskStatus.InProgress));
        }

        [Test]
        public async Task CreateTask_WithAttributes_PersistedOnTask()
        {
            var svc = CreateService();
            var req = BuildCreateRequest();
            req.Attributes = new Dictionary<string, string>
            {
                ["providerRef"] = "ext-12345",
                ["riskScore"]   = "72"
            };

            var result = await svc.CreateTaskAsync(req, "a");

            Assert.That(result.Task!.Attributes["providerRef"], Is.EqualTo("ext-12345"));
            Assert.That(result.Task.Attributes["riskScore"], Is.EqualTo("72"));
        }

        [Test]
        public async Task CreateTask_DefaultDueAt_Is30DaysFromNow()
        {
            var tp  = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(timeProvider: tp);
            var req = new CreateMonitoringTaskRequest
            {
                CaseId    = "case-abc",
                IssuerId  = "issuer-abc",
                SubjectId = "subject-abc",
                Reason    = ReassessmentReason.PeriodicSchedule
                // DueAt intentionally omitted
            };

            var result = await svc.CreateTaskAsync(req, "a");

            var expected = tp.GetUtcNow().AddDays(30);
            Assert.That(result.Task!.DueAt, Is.EqualTo(expected).Within(TimeSpan.FromSeconds(1)));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // INTEGRATION: HTTP pipeline via WebApplicationFactory
        // ═══════════════════════════════════════════════════════════════════════

        private OngoingMonitoringWebApplicationFactory? _factory;
        private HttpClient? _client;
        private HttpClient? _unauthClient;

        [OneTimeSetUp]
        public async Task IntegrationSetUp()
        {
            _factory    = new OngoingMonitoringWebApplicationFactory();
            _unauthClient = _factory.CreateClient();

            var email  = $"monitoring-test-{Guid.NewGuid():N}@biatec-test.example.com";
            var regReq = new BiatecTokensApi.Models.Auth.RegisterRequest
            {
                Email           = email,
                Password        = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName        = "Monitoring Test User"
            };
            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            var regBody = await regResp.Content.ReadFromJsonAsync<BiatecTokensApi.Models.Auth.RegisterResponse>();
            var token   = regBody?.AccessToken ?? string.Empty;

            _client = _factory.CreateClient();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        [OneTimeTearDown]
        public void IntegrationTearDown()
        {
            _client?.Dispose();
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

        [Test]
        [Order(10)]
        public async Task Integration_CreateTask_Unauthenticated_Returns401()
        {
            var req  = BuildCreateRequest();
            var resp = await _unauthClient!.PostAsJsonAsync("/api/v1/ongoing-monitoring", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        [Order(11)]
        public async Task Integration_CreateTask_ValidRequest_Returns200()
        {
            var req = BuildCreateRequest(dueAt: DateTimeOffset.UtcNow.AddDays(30));
            var resp = await _client!.PostAsJsonAsync("/api/v1/ongoing-monitoring", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await resp.Content.ReadFromJsonAsync<CreateMonitoringTaskResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Task, Is.Not.Null);
            Assert.That(body.Task!.TaskId, Is.Not.Empty);
        }

        [Test]
        [Order(12)]
        public async Task Integration_CreateTask_MissingCaseId_Returns400()
        {
            var req = BuildCreateRequest(caseId: "");
            var resp = await _client!.PostAsJsonAsync("/api/v1/ongoing-monitoring", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        [Order(13)]
        public async Task Integration_GetTask_NonExistent_Returns404()
        {
            var resp = await _client!.GetAsync("/api/v1/ongoing-monitoring/non-existent-task-id");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        [Order(14)]
        public async Task Integration_FullLifecycle_CreateGetStartEscalateClose()
        {
            // Create
            var createReq = BuildCreateRequest(
                reason: ReassessmentReason.SanctionsRefresh,
                dueAt: DateTimeOffset.UtcNow.AddDays(60));
            var createResp = await _client!.PostAsJsonAsync("/api/v1/ongoing-monitoring", createReq);
            Assert.That(createResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var created = await createResp.Content.ReadFromJsonAsync<CreateMonitoringTaskResponse>();
            var taskId  = created!.Task!.TaskId;

            // Get
            var getResp = await _client.GetAsync($"/api/v1/ongoing-monitoring/{taskId}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var gotten = await getResp.Content.ReadFromJsonAsync<GetMonitoringTaskResponse>();
            Assert.That(gotten!.Task!.TaskId, Is.EqualTo(taskId));

            // Start reassessment
            var startResp = await _client.PostAsJsonAsync(
                $"/api/v1/ongoing-monitoring/{taskId}/start-reassessment",
                new StartReassessmentRequest { Notes = "Integration test reassessment" });
            Assert.That(startResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Escalate
            var escResp = await _client.PostAsJsonAsync(
                $"/api/v1/ongoing-monitoring/{taskId}/escalate",
                new EscalateMonitoringTaskRequest
                {
                    EscalationReason = "Integration test escalation",
                    Severity         = MonitoringTaskSeverity.High
                });
            Assert.That(escResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Close
            var closeResp = await _client.PostAsJsonAsync(
                $"/api/v1/ongoing-monitoring/{taskId}/close",
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.ActionTaken,
                    ResolutionNotes = "Integration test: action taken, risk mitigated."
                });
            Assert.That(closeResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var closed = await closeResp.Content.ReadFromJsonAsync<CloseMonitoringTaskResponse>();
            Assert.That(closed!.Task!.Status.ToString(), Is.EqualTo("Resolved"));
        }

        [Test]
        [Order(15)]
        public async Task Integration_DeferAndRunDueDateCheck_Works()
        {
            // Create task
            var createReq = BuildCreateRequest(dueAt: DateTimeOffset.UtcNow.AddDays(45));
            var createResp = await _client!.PostAsJsonAsync("/api/v1/ongoing-monitoring", createReq);
            var created = await createResp.Content.ReadFromJsonAsync<CreateMonitoringTaskResponse>();
            var taskId  = created!.Task!.TaskId;

            // Defer
            var deferResp = await _client.PostAsJsonAsync(
                $"/api/v1/ongoing-monitoring/{taskId}/defer",
                new DeferMonitoringTaskRequest
                {
                    DeferUntil = DateTimeOffset.UtcNow.AddDays(7),
                    Rationale  = "Integration test deferral"
                });
            Assert.That(deferResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var deferred = await deferResp.Content.ReadFromJsonAsync<DeferMonitoringTaskResponse>();
            Assert.That(deferred!.Task!.Status.ToString(), Is.EqualTo("Deferred"));

            // Run due-date check (system endpoint; status won't change as times are real/not fake)
            var checkResp = await _client.PostAsJsonAsync("/api/v1/ongoing-monitoring/due-date-check", (object?)null);
            Assert.That(checkResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        [Order(16)]
        public async Task Integration_ListTasks_ReturnsCreatedTasks()
        {
            var issuerId = "issuer-list-int-" + Guid.NewGuid().ToString("N")[..6];

            await _client!.PostAsJsonAsync("/api/v1/ongoing-monitoring", BuildCreateRequest(issuerId: issuerId, dueAt: DateTimeOffset.UtcNow.AddDays(30)));
            await _client.PostAsJsonAsync("/api/v1/ongoing-monitoring", BuildCreateRequest(issuerId: issuerId, dueAt: DateTimeOffset.UtcNow.AddDays(30)));

            var listResp = await _client.GetAsync(
                $"/api/v1/ongoing-monitoring?issuerId={issuerId}&pageNumber=1&pageSize=10");
            Assert.That(listResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var result = await listResp.Content.ReadFromJsonAsync<ListMonitoringTasksResponse>();
            Assert.That(result!.TotalCount, Is.EqualTo(2));
        }

        [Test]
        [Order(17)]
        public async Task Integration_StartReassessment_ThenDefer_ThenEscalate_ThenClose_AuditTrailIntact()
        {
            var createReq = BuildCreateRequest(dueAt: DateTimeOffset.UtcNow.AddDays(90));
            var createResp = await _client!.PostAsJsonAsync("/api/v1/ongoing-monitoring", createReq);
            var created    = await createResp.Content.ReadFromJsonAsync<CreateMonitoringTaskResponse>();
            var taskId     = created!.Task!.TaskId;

            // Start → Defer → Escalate → Close
            await _client.PostAsJsonAsync($"/api/v1/ongoing-monitoring/{taskId}/start-reassessment",
                new StartReassessmentRequest());
            await _client.PostAsJsonAsync($"/api/v1/ongoing-monitoring/{taskId}/defer",
                new DeferMonitoringTaskRequest
                {
                    DeferUntil = DateTimeOffset.UtcNow.AddDays(3),
                    Rationale  = "Awaiting documents"
                });
            await _client.PostAsJsonAsync($"/api/v1/ongoing-monitoring/{taskId}/escalate",
                new EscalateMonitoringTaskRequest { EscalationReason = "Documents not received" });
            var closeResp = await _client.PostAsJsonAsync($"/api/v1/ongoing-monitoring/{taskId}/close",
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.SubjectSuspended,
                    ResolutionNotes = "Suspended for non-compliance."
                });

            Assert.That(closeResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var closed = await closeResp.Content.ReadFromJsonAsync<CloseMonitoringTaskResponse>();
            Assert.That(closed!.Task!.Status.ToString(), Is.EqualTo("Suspended"));
            // Timeline should have entries for: Create, Start, Defer, Escalate, Suspend
            Assert.That(closed.Task.Timeline.Count, Is.GreaterThanOrEqualTo(5));
        }

        // ── WebApplicationFactory ─────────────────────────────────────────────

        private sealed class OngoingMonitoringWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"]     = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForMonitoringIntegrationTests32Chars!",
                        ["JwtConfig:SecretKey"]              = "TestSecretKeyForMonitoringIntegrationTests32Chars!",
                        ["JwtConfig:Issuer"]                 = "BiatecTokensApi",
                        ["JwtConfig:Audience"]               = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"]   = "30",
                        ["JwtConfig:ValidateIssuer"]          = "true",
                        ["JwtConfig:ValidateAudience"]        = "true",
                        ["JwtConfig:ValidateLifetime"]        = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["AlgorandAuthentication:Realm"]          = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"]           = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"]  = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["IPFSConfig:ApiUrl"]       = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"]   = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"]    = "30",
                        ["IPFSConfig:MaxFileSizeBytes"]  = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"]   = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"]  = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io",
                        ["KycConfig:MockAutoApprove"] = "true",
                        ["StripeConfig:SecretKey"]     = "sk_test_placeholder",
                        ["StripeConfig:PublishableKey"] = "pk_test_placeholder",
                        ["StripeConfig:WebhookSecret"]  = "whsec_placeholder",
                    });
                });
            }
        }
    }
}
