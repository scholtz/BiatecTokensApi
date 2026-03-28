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
    /// and maintains per-operator lifecycle state (Unread / Read / Acknowledged / Dismissed) in memory.
    ///
    /// Key design decisions:
    /// <list type="bullet">
    ///   <item><description>Lifecycle state is per-operator: dismissing a notification in one operator's inbox does not affect another operator's view.</description></item>
    ///   <item><description>The underlying compliance event is never modified; only the overlay is updated.</description></item>
    ///   <item><description>All state transitions are timestamped to preserve auditability for regulated environments.</description></item>
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
                    ErrorMessage = eventsResult.ErrorMessage ?? "Could not retrieve compliance events from the backbone."
                };
            }

            var now = _timeProvider.GetUtcNow();

            // Build notification envelopes with per-operator lifecycle state
            var notifications = eventsResult.Events.Select(evt =>
            {
                var state = GetOrCreateState(operatorId, evt.EventId, evt.Timestamp, now);
                return BuildEnvelope(evt, state);
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

            return new OperatorNotificationListResponse
            {
                Success = true,
                Notifications = paged,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                InboxSummary = inboxSummary
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
                        state.LifecycleState = NotificationLifecycleState.Read;
                        state.ReadAt = now;
                        state.LastActorId = operatorId;
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
                        state.LifecycleState = NotificationLifecycleState.Acknowledged;
                        state.ReadAt ??= now;
                        state.AcknowledgedAt = now;
                        state.LastActorId = operatorId;
                        if (!string.IsNullOrWhiteSpace(request.OperatorNote))
                            state.OperatorNote = request.OperatorNote;
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
                        state.ReadAt ??= now;
                        state.AcknowledgedAt ??= now;
                        state.LifecycleState = NotificationLifecycleState.Dismissed;
                        state.DismissedAt = now;
                        state.LastActorId = operatorId;
                        if (!string.IsNullOrWhiteSpace(request.OperatorNote))
                            state.OperatorNote = request.OperatorNote;
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
            OperatorNotificationState state) =>
            new()
            {
                NotificationId = evt.EventId,
                Event = evt,
                LifecycleState = state.LifecycleState,
                CreatedAt = state.CreatedAt,
                ReadAt = state.ReadAt,
                AcknowledgedAt = state.AcknowledgedAt,
                DismissedAt = state.DismissedAt,
                LastActorId = state.LastActorId,
                OperatorNote = state.OperatorNote
            };

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
            int unread = 0, read = 0, acked = 0, dismissed = 0;
            int blockers = 0, warnings = 0;

            foreach (var n in all)
            {
                switch (n.LifecycleState)
                {
                    case NotificationLifecycleState.Unread: unread++; break;
                    case NotificationLifecycleState.Read: read++; break;
                    case NotificationLifecycleState.Acknowledged: acked++; break;
                    case NotificationLifecycleState.Dismissed: dismissed++; break;
                }

                if (n.LifecycleState is NotificationLifecycleState.Unread or NotificationLifecycleState.Read)
                {
                    if (n.Event.Severity == ComplianceEventSeverity.Critical) blockers++;
                    else if (n.Event.Severity == ComplianceEventSeverity.Warning) warnings++;
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
                ComputedAt = now
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
            public string? LastActorId { get; set; }
            public string? OperatorNote { get; set; }
        }
    }
}
