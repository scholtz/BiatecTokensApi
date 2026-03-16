using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.OngoingMonitoring;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of <see cref="IOngoingMonitoringService"/>.
    /// Manages the lifecycle of ongoing compliance monitoring tasks with an explicit
    /// state machine, fail-closed validation, and webhook event emission.
    /// </summary>
    public class OngoingMonitoringService : IOngoingMonitoringService
    {
        private readonly ILogger<OngoingMonitoringService> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly IWebhookService? _webhookService;

        // Primary store: taskId → task
        private readonly ConcurrentDictionary<string, MonitoringTask> _tasks = new();

        // ── Terminal states — no further transitions allowed ──────────────────
        private static readonly IReadOnlySet<MonitoringTaskStatus> _terminalStatuses =
            new HashSet<MonitoringTaskStatus>
            {
                MonitoringTaskStatus.Resolved,
                MonitoringTaskStatus.Suspended,
                MonitoringTaskStatus.Restricted
            };

        // ── Due-soon lead-time window ─────────────────────────────────────────
        private static readonly TimeSpan DueSoonWindow = TimeSpan.FromDays(7);

        /// <summary>
        /// Initialises the service.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="timeProvider">Time provider (inject a fake for tests).</param>
        /// <param name="webhookService">Optional webhook service for event emission.</param>
        public OngoingMonitoringService(
            ILogger<OngoingMonitoringService> logger,
            TimeProvider? timeProvider = null,
            IWebhookService? webhookService = null)
        {
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _webhookService = webhookService;
        }

        // ── CreateTaskAsync ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<CreateMonitoringTaskResponse> CreateTaskAsync(
            CreateMonitoringTaskRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(request.CaseId))
                return Task.FromResult(Fail<CreateMonitoringTaskResponse>(
                    "CaseId is required.", ErrorCodes.MISSING_REQUIRED_FIELD));

            if (string.IsNullOrWhiteSpace(request.IssuerId))
                return Task.FromResult(Fail<CreateMonitoringTaskResponse>(
                    "IssuerId is required.", ErrorCodes.MISSING_REQUIRED_FIELD));

            if (string.IsNullOrWhiteSpace(request.SubjectId))
                return Task.FromResult(Fail<CreateMonitoringTaskResponse>(
                    "SubjectId is required.", ErrorCodes.MISSING_REQUIRED_FIELD));

            var now    = _timeProvider.GetUtcNow();
            var dueAt  = request.DueAt ?? now.AddDays(30);

            if (dueAt <= now)
                return Task.FromResult(Fail<CreateMonitoringTaskResponse>(
                    "DueAt must be in the future.", ErrorCodes.INVALID_FIELD_VALUE));

            var taskId = Guid.NewGuid().ToString("N");

            // Determine initial status based on proximity to due date
            var initialStatus = DetermineInitialStatus(now, dueAt);

            var task = new MonitoringTask
            {
                TaskId          = taskId,
                CaseId          = request.CaseId,
                IssuerId        = request.IssuerId,
                SubjectId       = request.SubjectId,
                Status          = initialStatus,
                Reason          = request.Reason,
                Severity        = request.Severity,
                CreatedAt       = now,
                UpdatedAt       = now,
                DueAt           = dueAt,
                CreatedBy       = actorId,
                AssignedTo      = request.AssignedTo,
                Notes           = request.Notes,
                CorrelationId   = request.CorrelationId,
                RequiredEvidenceTypes = request.RequiredEvidenceTypes ?? new List<string>(),
                Attributes      = request.Attributes ?? new Dictionary<string, string>()
            };

            task.Timeline.Add(BuildEvent(
                taskId, MonitoringTaskEventType.TaskCreated, actorId, now,
                toStatus: initialStatus,
                description: $"Monitoring task created. Reason: {request.Reason}. Due: {dueAt:yyyy-MM-dd}."));

            _tasks[taskId] = task;

            _logger.LogInformation(
                "MonitoringTaskCreated. TaskId={TaskId} CaseId={CaseId} Reason={Reason} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(taskId),
                LoggingHelper.SanitizeLogInput(request.CaseId),
                request.Reason,
                LoggingHelper.SanitizeLogInput(actorId));

            EmitEventFireAndForget(
                WebhookEventType.MonitoringTaskCreated, actorId, taskId,
                new { taskId, caseId = request.CaseId, reason = request.Reason.ToString(), dueAt });

            return Task.FromResult(new CreateMonitoringTaskResponse { Success = true, Task = task });
        }

        // ── ListTasksAsync ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ListMonitoringTasksResponse> ListTasksAsync(
            ListMonitoringTasksRequest request, string actorId)
        {
            var pageNumber = Math.Max(1, request.PageNumber);
            var pageSize   = Math.Clamp(request.PageSize, 1, 100);

            IEnumerable<MonitoringTask> query = _tasks.Values;

            if (!string.IsNullOrWhiteSpace(request.IssuerId))
                query = query.Where(t => t.IssuerId == request.IssuerId);
            if (!string.IsNullOrWhiteSpace(request.SubjectId))
                query = query.Where(t => t.SubjectId == request.SubjectId);
            if (!string.IsNullOrWhiteSpace(request.CaseId))
                query = query.Where(t => t.CaseId == request.CaseId);
            if (request.Status.HasValue)
                query = query.Where(t => t.Status == request.Status.Value);
            if (request.Reason.HasValue)
                query = query.Where(t => t.Reason == request.Reason.Value);
            if (request.Severity.HasValue)
                query = query.Where(t => t.Severity == request.Severity.Value);
            if (request.OverdueOnly == true)
            {
                var now = _timeProvider.GetUtcNow();
                query = query.Where(t => t.DueAt < now && !_terminalStatuses.Contains(t.Status));
            }

            var all = query.OrderByDescending(t => t.CreatedAt).ToList();

            var paged = all
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(new ListMonitoringTasksResponse
            {
                Success    = true,
                Tasks      = paged,
                TotalCount = all.Count,
                PageNumber = pageNumber,
                PageSize   = pageSize
            });
        }

        // ── GetTaskAsync ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetMonitoringTaskResponse> GetTaskAsync(string taskId, string actorId)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
                return Task.FromResult(Fail<GetMonitoringTaskResponse>(
                    $"Monitoring task '{taskId}' not found.", ErrorCodes.NOT_FOUND));

            return Task.FromResult(new GetMonitoringTaskResponse { Success = true, Task = task });
        }

        // ── StartReassessmentAsync ────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<StartReassessmentResponse> StartReassessmentAsync(
            string taskId, StartReassessmentRequest request, string actorId)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
                return Task.FromResult(Fail<StartReassessmentResponse>(
                    $"Monitoring task '{taskId}' not found.", ErrorCodes.NOT_FOUND));

            if (_terminalStatuses.Contains(task.Status))
                return Task.FromResult(Fail<StartReassessmentResponse>(
                    $"Task is in terminal status '{task.Status}' and cannot be reassessed.",
                    ErrorCodes.INVALID_STATE_TRANSITION));

            if (task.Status == MonitoringTaskStatus.InProgress)
                return Task.FromResult(Fail<StartReassessmentResponse>(
                    "Task reassessment is already in progress.",
                    ErrorCodes.PRECONDITION_FAILED));

            var now = _timeProvider.GetUtcNow();
            var prevStatus = task.Status;

            task.Status    = MonitoringTaskStatus.InProgress;
            task.UpdatedAt = now;

            if (request.AssignedTo != null)
                task.AssignedTo = request.AssignedTo;

            if (request.RequiredEvidenceTypes != null)
                task.RequiredEvidenceTypes = request.RequiredEvidenceTypes;

            task.Timeline.Add(BuildEvent(
                taskId, MonitoringTaskEventType.ReassessmentStarted, actorId, now,
                fromStatus: prevStatus, toStatus: MonitoringTaskStatus.InProgress,
                description: $"Reassessment started.{(request.Notes != null ? " Notes: " + request.Notes : string.Empty)}",
                metadata: request.Notes != null
                    ? new Dictionary<string, string> { ["notes"] = request.Notes }
                    : null));

            _logger.LogInformation(
                "MonitoringTaskReassessmentStarted. TaskId={TaskId} PrevStatus={Prev} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(taskId),
                prevStatus,
                LoggingHelper.SanitizeLogInput(actorId));

            EmitEventFireAndForget(
                WebhookEventType.MonitoringTaskReassessmentStarted, actorId, taskId,
                new { taskId, caseId = task.CaseId, previousStatus = prevStatus.ToString() });

            return Task.FromResult(new StartReassessmentResponse { Success = true, Task = task });
        }

        // ── DeferTaskAsync ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<DeferMonitoringTaskResponse> DeferTaskAsync(
            string taskId, DeferMonitoringTaskRequest request, string actorId)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
                return Task.FromResult(Fail<DeferMonitoringTaskResponse>(
                    $"Monitoring task '{taskId}' not found.", ErrorCodes.NOT_FOUND));

            if (_terminalStatuses.Contains(task.Status))
                return Task.FromResult(Fail<DeferMonitoringTaskResponse>(
                    $"Task is in terminal status '{task.Status}' and cannot be deferred.",
                    ErrorCodes.INVALID_STATE_TRANSITION));

            if (string.IsNullOrWhiteSpace(request.Rationale))
                return Task.FromResult(Fail<DeferMonitoringTaskResponse>(
                    "Rationale is required when deferring a task.",
                    ErrorCodes.MISSING_REQUIRED_FIELD));

            var now = _timeProvider.GetUtcNow();

            if (request.DeferUntil <= now)
                return Task.FromResult(Fail<DeferMonitoringTaskResponse>(
                    "DeferUntil must be a future date.", ErrorCodes.INVALID_FIELD_VALUE));

            var prevStatus = task.Status;

            task.Status           = MonitoringTaskStatus.Deferred;
            task.DeferredUntil    = request.DeferUntil;
            task.DeferralRationale = request.Rationale;
            task.UpdatedAt        = now;

            task.Timeline.Add(BuildEvent(
                taskId, MonitoringTaskEventType.TaskDeferred, actorId, now,
                fromStatus: prevStatus, toStatus: MonitoringTaskStatus.Deferred,
                description: $"Task deferred until {request.DeferUntil:yyyy-MM-dd}. Rationale: {request.Rationale}",
                metadata: new Dictionary<string, string>
                {
                    ["deferUntil"] = request.DeferUntil.ToString("O"),
                    ["rationale"]  = request.Rationale
                }));

            _logger.LogInformation(
                "MonitoringTaskDeferred. TaskId={TaskId} DeferUntil={DeferUntil} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(taskId),
                request.DeferUntil.ToString("O"),
                LoggingHelper.SanitizeLogInput(actorId));

            EmitEventFireAndForget(
                WebhookEventType.MonitoringTaskDeferred, actorId, taskId,
                new { taskId, caseId = task.CaseId, deferUntil = request.DeferUntil, rationale = request.Rationale });

            return Task.FromResult(new DeferMonitoringTaskResponse { Success = true, Task = task });
        }

        // ── EscalateTaskAsync ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<EscalateMonitoringTaskResponse> EscalateTaskAsync(
            string taskId, EscalateMonitoringTaskRequest request, string actorId)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
                return Task.FromResult(Fail<EscalateMonitoringTaskResponse>(
                    $"Monitoring task '{taskId}' not found.", ErrorCodes.NOT_FOUND));

            if (_terminalStatuses.Contains(task.Status))
                return Task.FromResult(Fail<EscalateMonitoringTaskResponse>(
                    $"Task is in terminal status '{task.Status}' and cannot be escalated.",
                    ErrorCodes.INVALID_STATE_TRANSITION));

            if (string.IsNullOrWhiteSpace(request.EscalationReason))
                return Task.FromResult(Fail<EscalateMonitoringTaskResponse>(
                    "EscalationReason is required.", ErrorCodes.MISSING_REQUIRED_FIELD));

            var now        = _timeProvider.GetUtcNow();
            var prevStatus = task.Status;

            task.Status           = MonitoringTaskStatus.Escalated;
            task.EscalationReason = request.EscalationReason;
            task.UpdatedAt        = now;

            if (request.Severity.HasValue)
                task.Severity = request.Severity.Value;

            task.Timeline.Add(BuildEvent(
                taskId, MonitoringTaskEventType.TaskEscalated, actorId, now,
                fromStatus: prevStatus, toStatus: MonitoringTaskStatus.Escalated,
                description: $"Task escalated. Reason: {request.EscalationReason}",
                metadata: new Dictionary<string, string>
                {
                    ["escalationReason"] = request.EscalationReason,
                    ["severity"]         = task.Severity.ToString()
                }));

            _logger.LogWarning(
                "MonitoringTaskEscalated. TaskId={TaskId} Reason={Reason} Severity={Severity} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(taskId),
                LoggingHelper.SanitizeLogInput(request.EscalationReason),
                task.Severity,
                LoggingHelper.SanitizeLogInput(actorId));

            EmitEventFireAndForget(
                WebhookEventType.MonitoringTaskEscalated, actorId, taskId,
                new { taskId, caseId = task.CaseId, escalationReason = request.EscalationReason, severity = task.Severity.ToString() });

            return Task.FromResult(new EscalateMonitoringTaskResponse { Success = true, Task = task });
        }

        // ── CloseTaskAsync ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<CloseMonitoringTaskResponse> CloseTaskAsync(
            string taskId, CloseMonitoringTaskRequest request, string actorId)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
                return Task.FromResult(Fail<CloseMonitoringTaskResponse>(
                    $"Monitoring task '{taskId}' not found.", ErrorCodes.NOT_FOUND));

            if (_terminalStatuses.Contains(task.Status))
                return Task.FromResult(Fail<CloseMonitoringTaskResponse>(
                    $"Task is already in terminal status '{task.Status}' and cannot be closed again.",
                    ErrorCodes.INVALID_STATE_TRANSITION));

            if (string.IsNullOrWhiteSpace(request.ResolutionNotes))
                return Task.FromResult(Fail<CloseMonitoringTaskResponse>(
                    "ResolutionNotes is required when closing a monitoring task.",
                    ErrorCodes.MISSING_REQUIRED_FIELD));

            var now        = _timeProvider.GetUtcNow();
            var prevStatus = task.Status;

            // Determine terminal status from resolution outcome
            var terminalStatus = request.Resolution switch
            {
                MonitoringTaskResolution.SubjectSuspended   => MonitoringTaskStatus.Suspended,
                MonitoringTaskResolution.SubjectRestricted  => MonitoringTaskStatus.Restricted,
                _                                           => MonitoringTaskStatus.Resolved
            };

            task.Status            = terminalStatus;
            task.ResolutionOutcome = request.Resolution;
            task.ResolutionNotes   = request.ResolutionNotes;
            task.CompletedAt       = now;
            task.UpdatedAt         = now;

            var eventType = terminalStatus switch
            {
                MonitoringTaskStatus.Suspended  => MonitoringTaskEventType.SubjectSuspended,
                MonitoringTaskStatus.Restricted => MonitoringTaskEventType.SubjectRestricted,
                _                               => MonitoringTaskEventType.TaskResolved
            };

            task.Timeline.Add(BuildEvent(
                taskId, eventType, actorId, now,
                fromStatus: prevStatus, toStatus: terminalStatus,
                description: $"Task closed. Resolution: {request.Resolution}. Notes: {request.ResolutionNotes}",
                metadata: new Dictionary<string, string>
                {
                    ["resolution"]      = request.Resolution.ToString(),
                    ["resolutionNotes"] = request.ResolutionNotes
                }));

            // Choose the appropriate webhook event
            var webhookEvent = terminalStatus switch
            {
                MonitoringTaskStatus.Suspended  => WebhookEventType.MonitoringTaskSubjectSuspended,
                MonitoringTaskStatus.Restricted => WebhookEventType.MonitoringTaskSubjectRestricted,
                _                               => WebhookEventType.MonitoringTaskResolved
            };

            _logger.LogInformation(
                "MonitoringTaskClosed. TaskId={TaskId} Resolution={Resolution} FinalStatus={Status} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(taskId),
                request.Resolution,
                terminalStatus,
                LoggingHelper.SanitizeLogInput(actorId));

            EmitEventFireAndForget(
                webhookEvent, actorId, taskId,
                new { taskId, caseId = task.CaseId, resolution = request.Resolution.ToString(), terminalStatus = terminalStatus.ToString() });

            return Task.FromResult(new CloseMonitoringTaskResponse { Success = true, Task = task });
        }

        // ── RunDueDateCheckAsync ──────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<int> RunDueDateCheckAsync()
        {
            var now   = _timeProvider.GetUtcNow();
            int count = 0;

            foreach (var (_, task) in _tasks)
            {
                // Skip terminal or in-progress tasks
                if (_terminalStatuses.Contains(task.Status) ||
                    task.Status == MonitoringTaskStatus.InProgress)
                    continue;

                bool updated = false;

                // Advance deferred tasks whose deferral period has elapsed
                if (task.Status == MonitoringTaskStatus.Deferred &&
                    task.DeferredUntil.HasValue && now >= task.DeferredUntil.Value)
                {
                    var newStatus = now >= task.DueAt
                        ? MonitoringTaskStatus.Overdue
                        : (task.DueAt - now) <= DueSoonWindow
                            ? MonitoringTaskStatus.DueSoon
                            : MonitoringTaskStatus.Healthy;

                    var prevStatus = task.Status;
                    task.Status    = newStatus;
                    task.UpdatedAt = now;
                    task.Timeline.Add(BuildEvent(
                        task.TaskId, MonitoringTaskEventType.DeferralExpired, "system", now,
                        fromStatus: prevStatus, toStatus: newStatus,
                        description: $"Deferral period elapsed. Status advanced to {newStatus}."));
                    updated = true;
                }
                else if (task.Status != MonitoringTaskStatus.Deferred)
                {
                    // Evaluate due-date proximity for non-deferred tasks
                    MonitoringTaskStatus desiredStatus;

                    if (now >= task.DueAt)
                        desiredStatus = MonitoringTaskStatus.Overdue;
                    else if ((task.DueAt - now) <= DueSoonWindow)
                        desiredStatus = MonitoringTaskStatus.DueSoon;
                    else
                        desiredStatus = MonitoringTaskStatus.Healthy;

                    if (desiredStatus != task.Status &&
                        task.Status != MonitoringTaskStatus.Escalated &&
                        task.Status != MonitoringTaskStatus.Blocked &&
                        task.Status != MonitoringTaskStatus.AwaitingEvidence)
                    {
                        var prevStatus = task.Status;
                        task.Status    = desiredStatus;
                        task.UpdatedAt = now;
                        task.Timeline.Add(BuildEvent(
                            task.TaskId, MonitoringTaskEventType.StatusChanged, "system", now,
                            fromStatus: prevStatus, toStatus: desiredStatus,
                            description: $"Status advanced to {desiredStatus} by due-date check."));
                        updated = true;

                        if (desiredStatus == MonitoringTaskStatus.Overdue)
                        {
                            _logger.LogWarning(
                                "MonitoringTaskOverdue. TaskId={TaskId} DueAt={DueAt}",
                                LoggingHelper.SanitizeLogInput(task.TaskId),
                                task.DueAt.ToString("O"));

                            EmitEventFireAndForget(
                                WebhookEventType.MonitoringTaskOverdue, "system", task.TaskId,
                                new { taskId = task.TaskId, caseId = task.CaseId, dueAt = task.DueAt });
                        }
                        else if (desiredStatus == MonitoringTaskStatus.DueSoon)
                        {
                            EmitEventFireAndForget(
                                WebhookEventType.MonitoringTaskDueSoon, "system", task.TaskId,
                                new { taskId = task.TaskId, caseId = task.CaseId, dueAt = task.DueAt });
                        }
                    }
                }

                if (updated)
                    count++;
            }

            _logger.LogInformation(
                "MonitoringDueDateCheck. TasksUpdated={Count}", count);

            return Task.FromResult(count);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static MonitoringTaskStatus DetermineInitialStatus(
            DateTimeOffset now, DateTimeOffset dueAt)
        {
            if (now >= dueAt) return MonitoringTaskStatus.Overdue;
            if ((dueAt - now) <= DueSoonWindow) return MonitoringTaskStatus.DueSoon;
            return MonitoringTaskStatus.Healthy;
        }

        private static MonitoringTaskEvent BuildEvent(
            string taskId,
            MonitoringTaskEventType eventType,
            string actorId,
            DateTimeOffset occurredAt,
            string description = "",
            MonitoringTaskStatus? fromStatus = null,
            MonitoringTaskStatus? toStatus = null,
            Dictionary<string, string>? metadata = null)
        {
            return new MonitoringTaskEvent
            {
                EventId     = Guid.NewGuid().ToString("N"),
                TaskId      = taskId,
                EventType   = eventType,
                OccurredAt  = occurredAt,
                ActorId     = actorId,
                Description = description,
                FromStatus  = fromStatus,
                ToStatus    = toStatus,
                Metadata    = metadata ?? new Dictionary<string, string>()
            };
        }

        private static T Fail<T>(string message, string errorCode) where T : class, new()
        {
            if (typeof(T) == typeof(CreateMonitoringTaskResponse))
                return (new CreateMonitoringTaskResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(ListMonitoringTasksResponse))
                return (new ListMonitoringTasksResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(GetMonitoringTaskResponse))
                return (new GetMonitoringTaskResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(StartReassessmentResponse))
                return (new StartReassessmentResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(DeferMonitoringTaskResponse))
                return (new DeferMonitoringTaskResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(EscalateMonitoringTaskResponse))
                return (new EscalateMonitoringTaskResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(CloseMonitoringTaskResponse))
                return (new CloseMonitoringTaskResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;

            throw new InvalidOperationException($"Unsupported response type {typeof(T).Name}");
        }

        private void EmitEventFireAndForget(
            WebhookEventType eventType, string actorId, string taskId, object payloadObj)
        {
            if (_webhookService == null) return;

            Dictionary<string, object>? data;
            try
            {
                string json = JsonSerializer.Serialize(payloadObj);
                data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            }
            catch
            {
                data = new Dictionary<string, object> { ["taskId"] = taskId };
            }

            var ev = new WebhookEvent
            {
                Id        = Guid.NewGuid().ToString("N"),
                EventType = eventType,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                Actor     = actorId,
                Data      = data
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    await _webhookService.EmitEventAsync(ev);
                }
                catch (Exception ex)
                {
                    try
                    {
                        _logger.LogError(ex,
                            "OngoingMonitoring: webhook emission failed. EventType={EventType} TaskId={TaskId}",
                            eventType, LoggingHelper.SanitizeLogInput(taskId));
                    }
                    catch
                    {
                        // Swallow any logger exception to prevent the background task from faulting
                    }
                }
            });
        }
    }
}
