using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceEvents;
using BiatecTokensApi.Models.OperatorNotification;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of <see cref="IOperatorNotificationCenterService"/>.
    /// </summary>
    /// <remarks>
    /// This service wraps <see cref="IComplianceEventBackboneService"/> to produce operator notifications
    /// and maintains per-operator lifecycle state (Unread / Read / Acknowledged / Dismissed / Resolved / Reopened) in memory.
    ///
    /// Key design decisions:
    /// <list type="bullet">
    ///   <item><description>Lifecycle state is per-operator: dismissing a notification in one operator's inbox does not affect another operator's view.</description></item>
    ///   <item><description>The underlying compliance event is never modified; only the overlay is updated.</description></item>
    ///   <item><description>All state transitions are timestamped and recorded in an audit trail to preserve auditability for regulated environments.</description></item>
    ///   <item><description>In a production deployment the in-memory state would be backed by a durable store.</description></item>
    /// </list>
    /// </remarks>
    public class OperatorNotificationCenterService : IOperatorNotificationCenterService
    {
        private readonly IComplianceEventBackboneService _backbone;
        private readonly ILogger<OperatorNotificationCenterService> _logger;
        private readonly TimeProvider _timeProvider;

        // keyed by "{operatorId}|{eventId}"
        private readonly Dictionary<string, OperatorNotificationState> _states = new();
        private readonly object _lock = new();

        /// <summary>Initializes a new instance of <see cref="OperatorNotificationCenterService"/>.</summary>
        public OperatorNotificationCenterService(
            IComplianceEventBackboneService backbone,
            ILogger<OperatorNotificationCenterService> logger,
            TimeProvider? timeProvider = null)
        {
            _backbone = backbone;
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        // ── GetNotificationsAsync ─────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<OperatorNotificationListResponse> GetNotificationsAsync(
            OperatorNotificationQueryRequest request,
            string operatorId)
        {
            ArgumentNullException.ThrowIfNull(request);

            var page = Math.Max(request.Page, 1);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            // Pull the full compliance event set matching coarse filters
            var backboneRequest = new ComplianceEventQueryRequest
            {
                CaseId = request.CaseId,
                SubjectId = request.SubjectId,
                EntityId = request.EntityId,
                HeadRef = request.HeadRef,
                EventType = request.EventType,
                Severity = request.Severity,
                EntityKind = request.EntityKind,
                Page = 1,
                PageSize = 100
            };

            ComplianceEventListResponse eventsResult = await _backbone.GetEventsAsync(backboneRequest, operatorId);

            if (!eventsResult.Success)
            {
                return new OperatorNotificationListResponse
                {
                    Success = false,
                    ErrorCode = eventsResult.ErrorCode ?? "BACKBONE_ERROR",
                    ErrorMessage = eventsResult.ErrorMessage ?? "Could not retrieve compliance events from the backbone.",
                    IsDegradedState = true,
                    DegradedReason = eventsResult.ErrorMessage ?? "Backbone service returned a failure response."
                };
            }

            var now = _timeProvider.GetUtcNow();

            // Build notification envelopes with per-operator lifecycle state
            var notifications = eventsResult.Events.Select(evt =>
            {
                var state = GetOrCreateState(operatorId, evt.EventId, evt.Timestamp, now);
                return BuildEnvelope(evt, state, now);
            }).ToList();

            // Apply notification-specific filters
            IEnumerable<OperatorNotificationEnvelope> filtered = notifications;

            if (request.UnreadOnly == true)
                filtered = filtered.Where(n => n.LifecycleState == NotificationLifecycleState.Unread);

            if (request.ExcludeDismissed == true)
                filtered = filtered.Where(n => n.LifecycleState != NotificationLifecycleState.Dismissed);

            if (request.LifecycleState.HasValue)
                filtered = filtered.Where(n => n.LifecycleState == request.LifecycleState.Value);

            if (request.FromDate.HasValue)
                filtered = filtered.Where(n => n.CreatedAt >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                filtered = filtered.Where(n => n.CreatedAt <= request.ToDate.Value);

            // New role-aware, workflow-area, and aged-only filters
            if (request.Role.HasValue)
                filtered = filtered.Where(n => n.AudienceRoles.Contains(request.Role.Value));

            if (request.WorkflowArea.HasValue)
                filtered = filtered.Where(n => n.WorkflowArea == request.WorkflowArea.Value);

            if (request.AgedOnly == true)
                filtered = filtered.Where(n =>
                    n.EscalationMetadata.AgeBucket == NotificationAgeBucket.Stale
                    || n.EscalationMetadata.AgeBucket == NotificationAgeBucket.Overdue);

            var filteredList = filtered.OrderByDescending(n => n.CreatedAt).ToList();
            var totalCount = filteredList.Count;

            var paged = filteredList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var inboxSummary = BuildInboxSummary(notifications, now);

            _logger.LogInformation(
                "OperatorNotificationCenter.GetNotifications Operator={OperatorId} TotalFiltered={Total} Page={Page} PageSize={PageSize}",
                LoggingHelper.SanitizeLogInput(operatorId),
                totalCount,
                page,
                pageSize);

            // Set degraded-state fields if backbone returned error or partial data
            bool isDegraded = eventsResult.ErrorCode != null;
            string? degradedReason = isDegraded ? (eventsResult.ErrorMessage ?? "Backbone service returned a failure response.") : null;

            return new OperatorNotificationListResponse
            {
                Success = true,
                Notifications = paged,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                InboxSummary = inboxSummary,
                IsDegradedState = isDegraded,
                DegradedReason = degradedReason
            };
        }

        // ── MarkAsReadAsync ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<NotificationLifecycleResponse> MarkAsReadAsync(
            MarkNotificationsReadRequest request,
            string operatorId)
        {
            ArgumentNullException.ThrowIfNull(request);

            var now = _timeProvider.GetUtcNow();
            var notificationIds = await ResolveNotificationIdsAsync(
                request.NotificationIds,
                request.CaseId,
                operatorId,
                applyState: s => s.LifecycleState == NotificationLifecycleState.Unread);

            int affected = 0;
            lock (_lock)
            {
                foreach (var id in notificationIds)
                {
                    var key = StateKey(operatorId, id);
                    if (!_states.TryGetValue(key, out var state))
                        continue;

                    if (state.LifecycleState == NotificationLifecycleState.Unread)
                    {
                        var prev = state.LifecycleState;
                        state.LifecycleState = NotificationLifecycleState.Read;
                        state.ReadAt = now;
                        state.LastActorId = operatorId;
                        state.AuditTrail.Add(new NotificationAuditEntry
                        {
                            ChangedAt = now,
                            PreviousState = prev,
                            NewState = NotificationLifecycleState.Read,
                            ActorId = operatorId
                        });
                        affected++;
                    }
                }
            }

            _logger.LogInformation(
                "OperatorNotificationCenter.MarkAsRead Operator={OperatorId} Affected={Affected} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(operatorId),
                affected,
                LoggingHelper.SanitizeLogInput(request.CorrelationId));

            return new NotificationLifecycleResponse
            {
                Success = true,
                AffectedCount = affected,
                AppliedState = NotificationLifecycleState.Read,
                ActionedAt = now
            };
        }

        // ── AcknowledgeAsync ──────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<NotificationLifecycleResponse> AcknowledgeAsync(
            AcknowledgeNotificationsRequest request,
            string operatorId)
        {
            ArgumentNullException.ThrowIfNull(request);

            var now = _timeProvider.GetUtcNow();
            var notificationIds = await ResolveNotificationIdsAsync(
                request.NotificationIds,
                request.CaseId,
                operatorId,
                applyState: s => s.LifecycleState is NotificationLifecycleState.Unread or NotificationLifecycleState.Read);

            int affected = 0;
            lock (_lock)
            {
                foreach (var id in notificationIds)
                {
                    var key = StateKey(operatorId, id);
                    if (!_states.TryGetValue(key, out var state))
                        continue;

                    if (state.LifecycleState is NotificationLifecycleState.Unread or NotificationLifecycleState.Read)
                    {
                        var prev = state.LifecycleState;
                        state.LifecycleState = NotificationLifecycleState.Acknowledged;
                        state.ReadAt ??= now;
                        state.AcknowledgedAt = now;
                        state.LastActorId = operatorId;
                        if (!string.IsNullOrWhiteSpace(request.OperatorNote))
                            state.OperatorNote = request.OperatorNote;
                        state.AuditTrail.Add(new NotificationAuditEntry
                        {
                            ChangedAt = now,
                            PreviousState = prev,
                            NewState = NotificationLifecycleState.Acknowledged,
                            ActorId = operatorId,
                            Note = request.OperatorNote
                        });
                        affected++;
                    }
                }
            }

            _logger.LogInformation(
                "OperatorNotificationCenter.Acknowledge Operator={OperatorId} Affected={Affected} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(operatorId),
                affected,
                LoggingHelper.SanitizeLogInput(request.CorrelationId));

            return new NotificationLifecycleResponse
            {
                Success = true,
                AffectedCount = affected,
                AppliedState = NotificationLifecycleState.Acknowledged,
                ActionedAt = now
            };
        }

        // ── DismissAsync ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<NotificationLifecycleResponse> DismissAsync(
            DismissNotificationsRequest request,
            string operatorId)
        {
            ArgumentNullException.ThrowIfNull(request);

            var now = _timeProvider.GetUtcNow();
            var notificationIds = await ResolveNotificationIdsAsync(
                request.NotificationIds,
                request.CaseId,
                operatorId,
                applyState: s => s.LifecycleState != NotificationLifecycleState.Dismissed);

            int affected = 0;
            lock (_lock)
            {
                foreach (var id in notificationIds)
                {
                    var key = StateKey(operatorId, id);
                    if (!_states.TryGetValue(key, out var state))
                        continue;

                    if (state.LifecycleState != NotificationLifecycleState.Dismissed)
                    {
                        var prev = state.LifecycleState;
                        state.ReadAt ??= now;
                        state.AcknowledgedAt ??= now;
                        state.LifecycleState = NotificationLifecycleState.Dismissed;
                        state.DismissedAt = now;
                        state.LastActorId = operatorId;
                        if (!string.IsNullOrWhiteSpace(request.OperatorNote))
                            state.OperatorNote = request.OperatorNote;
                        state.AuditTrail.Add(new NotificationAuditEntry
                        {
                            ChangedAt = now,
                            PreviousState = prev,
                            NewState = NotificationLifecycleState.Dismissed,
                            ActorId = operatorId,
                            Note = request.OperatorNote
                        });
                        affected++;
                    }
                }
            }

            _logger.LogInformation(
                "OperatorNotificationCenter.Dismiss Operator={OperatorId} Affected={Affected} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(operatorId),
                affected,
                LoggingHelper.SanitizeLogInput(request.CorrelationId));

            return new NotificationLifecycleResponse
            {
                Success = true,
                AffectedCount = affected,
                AppliedState = NotificationLifecycleState.Dismissed,
                ActionedAt = now
            };
        }

        // ── ResolveAsync ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<NotificationLifecycleResponse> ResolveAsync(
            ResolveNotificationsRequest request,
            string operatorId)
        {
            ArgumentNullException.ThrowIfNull(request);

            var now = _timeProvider.GetUtcNow();
            var notificationIds = await ResolveNotificationIdsAsync(
                request.NotificationIds,
                request.CaseId,
                operatorId,
                applyState: s => s.LifecycleState != NotificationLifecycleState.Resolved);

            int affected = 0;
            lock (_lock)
            {
                foreach (var id in notificationIds)
                {
                    var key = StateKey(operatorId, id);
                    if (!_states.TryGetValue(key, out var state))
                        continue;

                    if (state.LifecycleState != NotificationLifecycleState.Resolved)
                    {
                        var prev = state.LifecycleState;
                        state.ReadAt ??= now;
                        state.AcknowledgedAt ??= now;
                        state.LifecycleState = NotificationLifecycleState.Resolved;
                        state.ResolvedAt = now;
                        state.LastActorId = operatorId;
                        if (!string.IsNullOrWhiteSpace(request.OperatorNote))
                            state.OperatorNote = request.OperatorNote;
                        state.AuditTrail.Add(new NotificationAuditEntry
                        {
                            ChangedAt = now,
                            PreviousState = prev,
                            NewState = NotificationLifecycleState.Resolved,
                            ActorId = operatorId,
                            Note = request.OperatorNote
                        });
                        affected++;
                    }
                }
            }

            _logger.LogInformation(
                "OperatorNotificationCenter.Resolve Operator={OperatorId} Affected={Affected} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(operatorId),
                affected,
                LoggingHelper.SanitizeLogInput(request.CorrelationId));

            return new NotificationLifecycleResponse
            {
                Success = true,
                AffectedCount = affected,
                AppliedState = NotificationLifecycleState.Resolved,
                ActionedAt = now
            };
        }

        // ── ReopenAsync ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<NotificationLifecycleResponse> ReopenAsync(
            ReopenNotificationsRequest request,
            string operatorId)
        {
            ArgumentNullException.ThrowIfNull(request);

            var now = _timeProvider.GetUtcNow();
            var notificationIds = await ResolveNotificationIdsAsync(
                request.NotificationIds,
                request.CaseId,
                operatorId,
                applyState: s =>
                    s.LifecycleState == NotificationLifecycleState.Resolved
                    || s.LifecycleState == NotificationLifecycleState.Dismissed);

            int affected = 0;
            lock (_lock)
            {
                foreach (var id in notificationIds)
                {
                    var key = StateKey(operatorId, id);
                    if (!_states.TryGetValue(key, out var state))
                        continue;

                    if (state.LifecycleState == NotificationLifecycleState.Resolved
                        || state.LifecycleState == NotificationLifecycleState.Dismissed)
                    {
                        var prev = state.LifecycleState;
                        state.LifecycleState = NotificationLifecycleState.Reopened;
                        state.ReopenedAt = now;
                        state.LastActorId = operatorId;
                        if (!string.IsNullOrWhiteSpace(request.OperatorNote))
                            state.OperatorNote = request.OperatorNote;
                        state.AuditTrail.Add(new NotificationAuditEntry
                        {
                            ChangedAt = now,
                            PreviousState = prev,
                            NewState = NotificationLifecycleState.Reopened,
                            ActorId = operatorId,
                            Note = request.OperatorNote
                        });
                        affected++;
                    }
                }
            }

            _logger.LogInformation(
                "OperatorNotificationCenter.Reopen Operator={OperatorId} Affected={Affected} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(operatorId),
                affected,
                LoggingHelper.SanitizeLogInput(request.CorrelationId));

            return new NotificationLifecycleResponse
            {
                Success = true,
                AffectedCount = affected,
                AppliedState = NotificationLifecycleState.Reopened,
                ActionedAt = now
            };
        }

        // ── GetUnreadCountAsync ───────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<NotificationUnreadCountResponse> GetUnreadCountAsync(string operatorId)
        {
            var now = _timeProvider.GetUtcNow();

            // Pull all events to ensure states are seeded
            var eventsResult = await _backbone.GetEventsAsync(new ComplianceEventQueryRequest { Page = 1, PageSize = 100 }, operatorId);

            if (!eventsResult.Success)
            {
                return new NotificationUnreadCountResponse
                {
                    Success = false,
                    ErrorCode = "BACKBONE_ERROR",
                    EvaluatedAt = now
                };
            }

            int unread = 0;
            int criticalUnread = 0;

            foreach (var evt in eventsResult.Events)
            {
                var state = GetOrCreateState(operatorId, evt.EventId, evt.Timestamp, now);
                if (state.LifecycleState == NotificationLifecycleState.Unread)
                {
                    unread++;
                    if (evt.Severity == ComplianceEventSeverity.Critical)
                        criticalUnread++;
                }
            }

            return new NotificationUnreadCountResponse
            {
                Success = true,
                UnreadCount = unread,
                CriticalUnreadCount = criticalUnread,
                EvaluatedAt = now
            };
        }

        // ── GetDigestSummaryAsync ─────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<NotificationDigestResponse> GetDigestSummaryAsync(
            NotificationDigestRequest request,
            string operatorId)
        {
            ArgumentNullException.ThrowIfNull(request);

            var eventsResult = await _backbone.GetEventsAsync(
                new ComplianceEventQueryRequest { Page = 1, PageSize = 100 }, operatorId);

            if (!eventsResult.Success)
            {
                return new NotificationDigestResponse
                {
                    Success = false,
                    ErrorCode = "BACKBONE_ERROR",
                    ErrorMessage = eventsResult.ErrorMessage ?? "Could not retrieve compliance events from the backbone.",
                    ComputedAt = _timeProvider.GetUtcNow()
                };
            }

            var now = _timeProvider.GetUtcNow();

            var notifications = eventsResult.Events.Select(evt =>
            {
                var state = GetOrCreateState(operatorId, evt.EventId, evt.Timestamp, now);
                return BuildEnvelope(evt, state, now);
            }).ToList();

            // Apply digest filters
            IEnumerable<OperatorNotificationEnvelope> filtered = notifications;

            if (request.WorkflowArea.HasValue)
                filtered = filtered.Where(n => n.WorkflowArea == request.WorkflowArea.Value);

            if (request.Role.HasValue)
                filtered = filtered.Where(n => n.AudienceRoles.Contains(request.Role.Value));

            if (request.FromDate.HasValue)
                filtered = filtered.Where(n => n.CreatedAt >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                filtered = filtered.Where(n => n.CreatedAt <= request.ToDate.Value);

            if (request.AgedOnly == true)
                filtered = filtered.Where(n =>
                    n.EscalationMetadata.AgeBucket == NotificationAgeBucket.Stale
                    || n.EscalationMetadata.AgeBucket == NotificationAgeBucket.Overdue);

            var filteredList = filtered.ToList();

            // Group by workflow area
            var groups = filteredList
                .GroupBy(n => n.WorkflowArea)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(n => n.CreatedAt).FirstOrDefault();
                    return new NotificationDigestSummary
                    {
                        WorkflowArea = g.Key,
                        TotalCount = g.Count(),
                        UnreadCount = g.Count(n => n.LifecycleState == NotificationLifecycleState.Unread),
                        CriticalCount = g.Count(n => n.Event.Severity == ComplianceEventSeverity.Critical),
                        EscalatedCount = g.Count(n => n.EscalationMetadata.IsEscalated),
                        LatestAt = latest?.CreatedAt,
                        RecommendedAction = BuildRecommendedAction(g.Key, g.Count(n => n.EscalationMetadata.IsEscalated))
                    };
                })
                .OrderBy(g => g.WorkflowArea)
                .ToList();

            var overallSummary = BuildInboxSummary(notifications, now);

            return new NotificationDigestResponse
            {
                Success = true,
                DigestGroups = groups,
                OverallSummary = overallSummary,
                ComputedAt = now
            };
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static string StateKey(string operatorId, string eventId) =>
            $"{operatorId}|{eventId}";

        private OperatorNotificationState GetOrCreateState(
            string operatorId,
            string eventId,
            DateTimeOffset eventTimestamp,
            DateTimeOffset now)
        {
            var key = StateKey(operatorId, eventId);
            lock (_lock)
            {
                if (!_states.TryGetValue(key, out var state))
                {
                    state = new OperatorNotificationState
                    {
                        OperatorId = operatorId,
                        EventId = eventId,
                        LifecycleState = NotificationLifecycleState.Unread,
                        CreatedAt = eventTimestamp == default ? now : eventTimestamp
                    };
                    _states[key] = state;
                }

                return state;
            }
        }

        private static OperatorNotificationEnvelope BuildEnvelope(
            ComplianceEventEnvelope evt,
            OperatorNotificationState state,
            DateTimeOffset now)
        {
            var workflowArea = MapWorkflowArea(evt.EventType);
            var audienceRoles = ComputeAudienceRoles(workflowArea, evt.Severity);
            var escalation = ComputeEscalationMetadata(state, evt.Severity, now);

            return new OperatorNotificationEnvelope
            {
                NotificationId = evt.EventId,
                Event = evt,
                LifecycleState = state.LifecycleState,
                CreatedAt = state.CreatedAt,
                ReadAt = state.ReadAt,
                AcknowledgedAt = state.AcknowledgedAt,
                DismissedAt = state.DismissedAt,
                ResolvedAt = state.ResolvedAt,
                ReopenedAt = state.ReopenedAt,
                LastActorId = state.LastActorId,
                OperatorNote = state.OperatorNote,
                WorkflowArea = workflowArea,
                AudienceRoles = audienceRoles,
                EscalationMetadata = escalation,
                AuditTrail = state.AuditTrail.ToList(),
                IsActionable = IsActionableEvent(evt.EventType, evt.Severity),
                RemediationGuidance = BuildRemediationGuidance(workflowArea, evt.Severity)
            };
        }

        private static NotificationWorkflowArea MapWorkflowArea(ComplianceEventType eventType) =>
            eventType switch
            {
                ComplianceEventType.OnboardingCaseCreated => NotificationWorkflowArea.KycOnboarding,
                ComplianceEventType.OnboardingProviderChecksInitiated => NotificationWorkflowArea.KycOnboarding,
                ComplianceEventType.OnboardingReviewerActionRecorded => NotificationWorkflowArea.KycOnboarding,
                ComplianceEventType.OnboardingNotConfigured => NotificationWorkflowArea.KycOnboarding,
                ComplianceEventType.OnboardingProviderUnavailable => NotificationWorkflowArea.KycOnboarding,
                ComplianceEventType.ComplianceCaseCreated => NotificationWorkflowArea.ComplianceCase,
                ComplianceEventType.ComplianceCaseStateChanged => NotificationWorkflowArea.ComplianceCase,
                ComplianceEventType.ComplianceCaseEvidenceUpdated => NotificationWorkflowArea.ComplianceCase,
                ComplianceEventType.ComplianceCaseEvidenceStale => NotificationWorkflowArea.ComplianceCase,
                ComplianceEventType.ComplianceEscalationRaised => NotificationWorkflowArea.ComplianceCase,
                ComplianceEventType.ComplianceEscalationResolved => NotificationWorkflowArea.ComplianceCase,
                ComplianceEventType.ComplianceDecisionRecorded => NotificationWorkflowArea.ComplianceCase,
                ComplianceEventType.ComplianceEvidenceGenerated => NotificationWorkflowArea.ComplianceCase,
                ComplianceEventType.ProtectedSignOffApprovalWebhookRecorded => NotificationWorkflowArea.ProtectedSignOff,
                ComplianceEventType.ProtectedSignOffEvidenceCaptured => NotificationWorkflowArea.ProtectedSignOff,
                ComplianceEventType.ReleaseReadinessEvaluated => NotificationWorkflowArea.ReleaseReadiness,
                ComplianceEventType.ComplianceAuditExportGenerated => NotificationWorkflowArea.ExportAudit,
                _ => NotificationWorkflowArea.General
            };

        private static List<OperatorRole> ComputeAudienceRoles(
            NotificationWorkflowArea area,
            ComplianceEventSeverity severity)
        {
            var roles = area switch
            {
                NotificationWorkflowArea.KycOnboarding => new List<OperatorRole>
                {
                    OperatorRole.ComplianceReviewer,
                    OperatorRole.OnboardingOperator,
                    OperatorRole.Manager
                },
                NotificationWorkflowArea.ComplianceCase => new List<OperatorRole>
                {
                    OperatorRole.ComplianceReviewer,
                    OperatorRole.Manager
                },
                NotificationWorkflowArea.ProtectedSignOff => new List<OperatorRole>
                {
                    OperatorRole.ComplianceReviewer,
                    OperatorRole.EnterpriseAdministrator
                },
                NotificationWorkflowArea.ReleaseReadiness => new List<OperatorRole>
                {
                    OperatorRole.ComplianceReviewer,
                    OperatorRole.EnterpriseAdministrator,
                    OperatorRole.Manager
                },
                NotificationWorkflowArea.Reporting => new List<OperatorRole>
                {
                    OperatorRole.ComplianceReviewer,
                    OperatorRole.Manager
                },
                NotificationWorkflowArea.ExportAudit => new List<OperatorRole>
                {
                    OperatorRole.ComplianceReviewer,
                    OperatorRole.SystemAuditor,
                    OperatorRole.EnterpriseAdministrator
                },
                _ => new List<OperatorRole>
                {
                    OperatorRole.ComplianceReviewer,
                    OperatorRole.OnboardingOperator,
                    OperatorRole.Manager,
                    OperatorRole.EnterpriseAdministrator,
                    OperatorRole.SystemAuditor
                }
            };

            // Critical events always include Manager for immediate escalation awareness
            if (severity == ComplianceEventSeverity.Critical && !roles.Contains(OperatorRole.Manager))
                roles.Add(OperatorRole.Manager);

            return roles;
        }

        private static NotificationEscalationMetadata ComputeEscalationMetadata(
            OperatorNotificationState state,
            ComplianceEventSeverity severity,
            DateTimeOffset now)
        {
            var ageHours = (int)(now - state.CreatedAt).TotalHours;

            var ageBucket = ageHours switch
            {
                < 1 => NotificationAgeBucket.Fresh,
                < 24 => NotificationAgeBucket.Aging,
                < 168 => NotificationAgeBucket.Stale,   // < 7 days
                _ => NotificationAgeBucket.Overdue
            };

            bool isActivelyPending = state.LifecycleState is
                NotificationLifecycleState.Unread or NotificationLifecycleState.Read;

            bool isEscalated = isActivelyPending && ageHours >= 24;
            bool isSlaBreached = isActivelyPending && severity == ComplianceEventSeverity.Critical && ageHours >= 24;

            string? hint = null;
            if (isEscalated)
            {
                hint = severity == ComplianceEventSeverity.Critical
                    ? $"Critical notification unacknowledged for {ageHours}h"
                    : $"Notification unacknowledged for {ageHours}h";
            }

            return new NotificationEscalationMetadata
            {
                AgeBucket = ageBucket,
                AgeHours = ageHours,
                IsEscalated = isEscalated,
                IsSlaBreached = isSlaBreached,
                EscalationHint = hint
            };
        }

        private static bool IsActionableEvent(ComplianceEventType eventType, ComplianceEventSeverity severity) =>
            severity == ComplianceEventSeverity.Critical
            || eventType is ComplianceEventType.ComplianceEscalationRaised
                or ComplianceEventType.ComplianceCaseEvidenceStale
                or ComplianceEventType.ProtectedSignOffApprovalWebhookRecorded
                or ComplianceEventType.OnboardingNotConfigured
                or ComplianceEventType.OnboardingProviderUnavailable;

        private static string? BuildRemediationGuidance(NotificationWorkflowArea area, ComplianceEventSeverity severity) =>
            area switch
            {
                NotificationWorkflowArea.KycOnboarding when severity == ComplianceEventSeverity.Critical =>
                    "Review KYC evidence and record a compliance decision immediately.",
                NotificationWorkflowArea.ComplianceCase when severity == ComplianceEventSeverity.Critical =>
                    "Review the compliance case blocker and take remediation action.",
                NotificationWorkflowArea.ProtectedSignOff =>
                    "Review the sign-off evidence pack and approve or reject.",
                NotificationWorkflowArea.ReleaseReadiness =>
                    "Evaluate release readiness evidence and confirm gating decision.",
                _ => null
            };

        private static string? BuildRecommendedAction(NotificationWorkflowArea area, int escalatedCount) =>
            escalatedCount > 0
                ? $"Review {escalatedCount} escalated notification(s) in {area}."
                : null;

        /// <summary>
        /// Resolves the list of notification IDs to act on.
        /// When <paramref name="explicitIds"/> is non-empty, returns those IDs (after seeding state).
        /// When empty, fetches all events scoped by optional <paramref name="caseId"/> and returns IDs
        /// that match <paramref name="applyState"/>.
        /// </summary>
        private async Task<IReadOnlyList<string>> ResolveNotificationIdsAsync(
            IReadOnlyList<string> explicitIds,
            string? caseId,
            string operatorId,
            Func<OperatorNotificationState, bool> applyState)
        {
            if (explicitIds.Count > 0)
            {
                var now = _timeProvider.GetUtcNow();

                // Ensure state entries exist for explicitly requested IDs
                lock (_lock)
                {
                    foreach (var id in explicitIds)
                    {
                        var key = StateKey(operatorId, id);
                        if (!_states.ContainsKey(key))
                        {
                            _states[key] = new OperatorNotificationState
                            {
                                OperatorId = operatorId,
                                EventId = id,
                                LifecycleState = NotificationLifecycleState.Unread,
                                CreatedAt = now
                            };
                        }
                    }
                }

                return explicitIds;
            }

            // Bulk mode: fetch all events in scope and collect IDs matching applyState
            var eventsResult = await _backbone.GetEventsAsync(new ComplianceEventQueryRequest
            {
                CaseId = caseId,
                Page = 1,
                PageSize = 100
            }, operatorId);

            if (!eventsResult.Success)
                return Array.Empty<string>();

            var now2 = _timeProvider.GetUtcNow();
            var ids = new List<string>();

            foreach (var evt in eventsResult.Events)
            {
                var state = GetOrCreateState(operatorId, evt.EventId, evt.Timestamp, now2);
                if (applyState(state))
                    ids.Add(evt.EventId);
            }

            return ids;
        }

        private static OperatorNotificationInboxSummary BuildInboxSummary(
            IReadOnlyList<OperatorNotificationEnvelope> all,
            DateTimeOffset now)
        {
            int unread = 0, read = 0, acked = 0, dismissed = 0, resolved = 0;
            int blockers = 0, warnings = 0;
            int escalated = 0, slaBreached = 0, degraded = 0;

            foreach (var n in all)
            {
                switch (n.LifecycleState)
                {
                    case NotificationLifecycleState.Unread: unread++; break;
                    case NotificationLifecycleState.Read: read++; break;
                    case NotificationLifecycleState.Acknowledged: acked++; break;
                    case NotificationLifecycleState.Dismissed: dismissed++; break;
                    case NotificationLifecycleState.Resolved: resolved++; break;
                    case NotificationLifecycleState.Reopened: unread++; break;
                }

                if (n.LifecycleState is NotificationLifecycleState.Unread or NotificationLifecycleState.Read)
                {
                    if (n.Event.Severity == ComplianceEventSeverity.Critical) blockers++;
                    else if (n.Event.Severity == ComplianceEventSeverity.Warning) warnings++;
                }

                if (n.EscalationMetadata.IsEscalated) escalated++;
                if (n.EscalationMetadata.IsSlaBreached) slaBreached++;

                if (n.Event.Freshness is ComplianceEventFreshness.Stale
                    or ComplianceEventFreshness.Unavailable
                    or ComplianceEventFreshness.NotConfigured
                    or ComplianceEventFreshness.PartialEvidence)
                {
                    degraded++;
                }
            }

            return new OperatorNotificationInboxSummary
            {
                UnreadCount = unread,
                ReadCount = read,
                AcknowledgedCount = acked,
                DismissedCount = dismissed,
                ActiveBlockerCount = blockers,
                ActiveWarningCount = warnings,
                TotalActiveCount = unread + read + acked,
                ComputedAt = now,
                EscalatedCount = escalated,
                SlaBreachedCount = slaBreached,
                DegradedStateCount = degraded,
                ResolvedCount = resolved
            };
        }

        // ── Inner record ─────────────────────────────────────────────────────

        private sealed class OperatorNotificationState
        {
            public string OperatorId { get; init; } = string.Empty;
            public string EventId { get; init; } = string.Empty;
            public NotificationLifecycleState LifecycleState { get; set; }
            public DateTimeOffset CreatedAt { get; init; }
            public DateTimeOffset? ReadAt { get; set; }
            public DateTimeOffset? AcknowledgedAt { get; set; }
            public DateTimeOffset? DismissedAt { get; set; }
            public DateTimeOffset? ResolvedAt { get; set; }
            public DateTimeOffset? ReopenedAt { get; set; }
            public string? LastActorId { get; set; }
            public string? OperatorNote { get; set; }
            public List<NotificationAuditEntry> AuditTrail { get; } = new();
        }
    }
}
