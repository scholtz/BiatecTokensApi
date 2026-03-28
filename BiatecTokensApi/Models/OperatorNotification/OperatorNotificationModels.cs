using BiatecTokensApi.Models.ComplianceEvents;

namespace BiatecTokensApi.Models.OperatorNotification
{
    /// <summary>
    /// Per-operator lifecycle state for a compliance notification.
    /// Lifecycle transitions: Unread → Read → Acknowledged → Dismissed (or any step can transition to Dismissed).
    /// </summary>
    public enum NotificationLifecycleState
    {
        /// <summary>The notification has not yet been seen by the operator.</summary>
        Unread,

        /// <summary>The operator has opened or explicitly marked the notification as read.</summary>
        Read,

        /// <summary>The operator has acknowledged the notification, confirming awareness.</summary>
        Acknowledged,

        /// <summary>The operator has dismissed the notification from their active queue.</summary>
        Dismissed
    }

    /// <summary>
    /// Operator-enriched notification envelope that wraps a canonical compliance event
    /// with per-operator lifecycle state and audit metadata.
    /// </summary>
    public class OperatorNotificationEnvelope
    {
        /// <summary>Stable identifier for this notification (matches the underlying event ID).</summary>
        public string NotificationId { get; set; } = string.Empty;

        /// <summary>The canonical compliance event this notification is based on.</summary>
        public ComplianceEventEnvelope Event { get; set; } = new();

        /// <summary>Current lifecycle state for the requesting operator.</summary>
        public NotificationLifecycleState LifecycleState { get; set; } = NotificationLifecycleState.Unread;

        /// <summary>UTC timestamp when this notification was first generated.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>UTC timestamp when the operator first marked the notification as read, if applicable.</summary>
        public DateTimeOffset? ReadAt { get; set; }

        /// <summary>UTC timestamp when the operator acknowledged the notification, if applicable.</summary>
        public DateTimeOffset? AcknowledgedAt { get; set; }

        /// <summary>UTC timestamp when the operator dismissed the notification, if applicable.</summary>
        public DateTimeOffset? DismissedAt { get; set; }

        /// <summary>Identifier of the operator who last changed the lifecycle state.</summary>
        public string? LastActorId { get; set; }

        /// <summary>
        /// Optional operator-supplied note attached when acknowledging or dismissing.
        /// Supports auditability for regulated environments.
        /// </summary>
        public string? OperatorNote { get; set; }
    }

    /// <summary>
    /// Filter and pagination options for the operator notification center.
    /// Extends the base compliance event query with notification-specific filters.
    /// </summary>
    public class OperatorNotificationQueryRequest
    {
        /// <summary>Filter by case ID across onboarding, compliance, and sign-off domains.</summary>
        public string? CaseId { get; set; }

        /// <summary>Filter by subject or investor identifier.</summary>
        public string? SubjectId { get; set; }

        /// <summary>Filter by a specific entity identifier.</summary>
        public string? EntityId { get; set; }

        /// <summary>Filter by release head ref.</summary>
        public string? HeadRef { get; set; }

        /// <summary>Filter by event severity.</summary>
        public ComplianceEventSeverity? Severity { get; set; }

        /// <summary>Filter by event type.</summary>
        public ComplianceEventType? EventType { get; set; }

        /// <summary>Filter by entity kind.</summary>
        public ComplianceEventEntityKind? EntityKind { get; set; }

        /// <summary>Filter by notification lifecycle state.</summary>
        public NotificationLifecycleState? LifecycleState { get; set; }

        /// <summary>
        /// When true, only returns notifications not yet dismissed (Unread, Read, Acknowledged).
        /// Equivalent to filtering out Dismissed state.
        /// </summary>
        public bool? ExcludeDismissed { get; set; }

        /// <summary>
        /// When true, only returns unread notifications.
        /// Equivalent to filtering LifecycleState = Unread.
        /// </summary>
        public bool? UnreadOnly { get; set; }

        /// <summary>Filter to events created on or after this UTC timestamp.</summary>
        public DateTimeOffset? FromDate { get; set; }

        /// <summary>Filter to events created on or before this UTC timestamp.</summary>
        public DateTimeOffset? ToDate { get; set; }

        /// <summary>Page number (1-based). Defaults to 1.</summary>
        public int Page { get; set; } = 1;

        /// <summary>Page size capped at 100. Defaults to 50.</summary>
        public int PageSize { get; set; } = 50;
    }

    /// <summary>Request to mark one or more notifications as read.</summary>
    public class MarkNotificationsReadRequest
    {
        /// <summary>
        /// Specific notification IDs to mark as read.
        /// When empty, all Unread notifications visible to the operator are marked as read.
        /// </summary>
        public List<string> NotificationIds { get; set; } = new();

        /// <summary>Optional scoping filter so only notifications matching a case are affected.</summary>
        public string? CaseId { get; set; }

        /// <summary>Optional correlation ID for audit-trail linkage.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Request to acknowledge one or more notifications.</summary>
    public class AcknowledgeNotificationsRequest
    {
        /// <summary>
        /// Specific notification IDs to acknowledge.
        /// When empty, all Read or Unread notifications visible to the operator are acknowledged.
        /// </summary>
        public List<string> NotificationIds { get; set; } = new();

        /// <summary>Optional scoping filter so only notifications matching a case are affected.</summary>
        public string? CaseId { get; set; }

        /// <summary>Optional operator note attached as audit evidence of the acknowledgement decision.</summary>
        public string? OperatorNote { get; set; }

        /// <summary>Optional correlation ID for audit-trail linkage.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Request to dismiss one or more notifications from the active queue.</summary>
    public class DismissNotificationsRequest
    {
        /// <summary>
        /// Specific notification IDs to dismiss.
        /// When empty, all non-Dismissed notifications visible to the operator are dismissed.
        /// </summary>
        public List<string> NotificationIds { get; set; } = new();

        /// <summary>Optional scoping filter so only notifications matching a case are affected.</summary>
        public string? CaseId { get; set; }

        /// <summary>Optional operator note explaining the dismissal for audit purposes.</summary>
        public string? OperatorNote { get; set; }

        /// <summary>Optional correlation ID for audit-trail linkage.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Response containing paginated operator notifications with lifecycle state and summary.
    /// </summary>
    public class OperatorNotificationListResponse
    {
        /// <summary>True when the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Paginated list of operator notifications with per-operator lifecycle state.</summary>
        public List<OperatorNotificationEnvelope> Notifications { get; set; } = new();

        /// <summary>Total matching notifications before pagination.</summary>
        public int TotalCount { get; set; }

        /// <summary>Requested page number.</summary>
        public int Page { get; set; }

        /// <summary>Requested page size after server-side clamping.</summary>
        public int PageSize { get; set; }

        /// <summary>Summary counts across the unfiltered inbox for the requesting operator.</summary>
        public OperatorNotificationInboxSummary InboxSummary { get; set; } = new();

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Lifecycle action response for mark-read, acknowledge, or dismiss operations.
    /// </summary>
    public class NotificationLifecycleResponse
    {
        /// <summary>True when the lifecycle action was applied successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Number of notifications affected by the action.</summary>
        public int AffectedCount { get; set; }

        /// <summary>New lifecycle state applied to affected notifications.</summary>
        public NotificationLifecycleState AppliedState { get; set; }

        /// <summary>UTC timestamp when the lifecycle action was recorded.</summary>
        public DateTimeOffset ActionedAt { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Lightweight inbox summary providing badge counts for the operator notification center.
    /// </summary>
    public class OperatorNotificationInboxSummary
    {
        /// <summary>Number of notifications that have not yet been read.</summary>
        public int UnreadCount { get; set; }

        /// <summary>Number of notifications that have been read but not yet acknowledged.</summary>
        public int ReadCount { get; set; }

        /// <summary>Number of notifications that have been acknowledged but not dismissed.</summary>
        public int AcknowledgedCount { get; set; }

        /// <summary>Number of dismissed notifications (excluded from the active queue).</summary>
        public int DismissedCount { get; set; }

        /// <summary>Number of unread or read notifications with Critical severity requiring immediate attention.</summary>
        public int ActiveBlockerCount { get; set; }

        /// <summary>Number of unread or read notifications with Warning severity requiring operator attention.</summary>
        public int ActiveWarningCount { get; set; }

        /// <summary>Total active notifications (Unread + Read + Acknowledged, excludes Dismissed).</summary>
        public int TotalActiveCount { get; set; }

        /// <summary>UTC timestamp when this summary was computed.</summary>
        public DateTimeOffset ComputedAt { get; set; }
    }

    /// <summary>
    /// Response from the unread-count endpoint, optimised for notification badge rendering.
    /// </summary>
    public class NotificationUnreadCountResponse
    {
        /// <summary>True when the count was retrieved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Total number of unread notifications for the authenticated operator.</summary>
        public int UnreadCount { get; set; }

        /// <summary>Number of unread Critical notifications requiring immediate action.</summary>
        public int CriticalUnreadCount { get; set; }

        /// <summary>UTC timestamp when this count was evaluated.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }
    }
}
