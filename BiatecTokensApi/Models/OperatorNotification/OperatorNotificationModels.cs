using BiatecTokensApi.Models.ComplianceEvents;

namespace BiatecTokensApi.Models.OperatorNotification
{
    /// <summary>Per-operator lifecycle state for a notification.</summary>
    public enum NotificationLifecycleState
    {
        /// <summary>Not yet seen by the operator.</summary>
        Unread = 0,
        /// <summary>Opened or viewed by the operator.</summary>
        Read = 1,
        /// <summary>Operator explicitly confirmed awareness.</summary>
        Acknowledged = 2,
        /// <summary>Operator archived the notification.</summary>
        Dismissed = 3,
        /// <summary>Operator marked the underlying workflow item as complete.</summary>
        Resolved = 4,
        /// <summary>Operator reopened a previously resolved or dismissed notification.</summary>
        Reopened = 5
    }

    /// <summary>Operator roles for role-aware notification targeting.</summary>
    public enum OperatorRole
    {
        /// <summary>Compliance reviewer responsible for KYC/AML decisions.</summary>
        ComplianceReviewer = 0,
        /// <summary>Onboarding operator managing investor onboarding flows.</summary>
        OnboardingOperator = 1,
        /// <summary>Operations manager with oversight across workflow areas.</summary>
        Manager = 2,
        /// <summary>Enterprise administrator with platform-wide configuration access.</summary>
        EnterpriseAdministrator = 3,
        /// <summary>System auditor with read-only access for audit and regulatory review.</summary>
        SystemAuditor = 4
    }

    /// <summary>Workflow area classification for notification grouping and digest semantics.</summary>
    public enum NotificationWorkflowArea
    {
        /// <summary>General or uncategorized notifications.</summary>
        General = 0,
        /// <summary>KYC/AML onboarding workflow notifications.</summary>
        KycOnboarding = 1,
        /// <summary>Compliance case management notifications.</summary>
        ComplianceCase = 2,
        /// <summary>Token deployment and operations notifications.</summary>
        TokenOperations = 3,
        /// <summary>Protected sign-off and release evidence notifications.</summary>
        ProtectedSignOff = 4,
        /// <summary>Release readiness evaluations and gating notifications.</summary>
        ReleaseReadiness = 5,
        /// <summary>Regulatory reporting and export notifications.</summary>
        Reporting = 6,
        /// <summary>Audit export and compliance evidence notifications.</summary>
        ExportAudit = 7,
        /// <summary>System health and infrastructure notifications.</summary>
        SystemHealth = 8
    }

    /// <summary>Age bucket for escalation threshold computation.</summary>
    public enum NotificationAgeBucket
    {
        /// <summary>Under 1 hour old.</summary>
        Fresh = 0,
        /// <summary>1–24 hours old.</summary>
        Aging = 1,
        /// <summary>1–7 days old.</summary>
        Stale = 2,
        /// <summary>Over 7 days old without action.</summary>
        Overdue = 3
    }

    /// <summary>
    /// Escalation metadata computed for each notification envelope.
    /// </summary>
    public class NotificationEscalationMetadata
    {
        /// <summary>Age bucket relative to creation time.</summary>
        public NotificationAgeBucket AgeBucket { get; set; }

        /// <summary>Hours elapsed since creation.</summary>
        public double AgeHours { get; set; }

        /// <summary>True when operator SLA has been exceeded.</summary>
        public bool IsSlaBreached { get; set; }

        /// <summary>True when escalation is required based on age and severity.</summary>
        public bool IsEscalated { get; set; }

        /// <summary>Human-readable escalation hint describing urgency.</summary>
        public string? EscalationHint { get; set; }

        /// <summary>Recommended operator action based on lifecycle state and escalation.</summary>
        public string? RecommendedAction { get; set; }
    }

    /// <summary>Immutable audit entry recording a lifecycle state change.</summary>
    public class NotificationAuditEntry
    {
        /// <summary>UTC timestamp when the state change was recorded.</summary>
        public DateTimeOffset ChangedAt { get; set; }

        /// <summary>Lifecycle state before this transition.</summary>
        public NotificationLifecycleState PreviousState { get; set; }

        /// <summary>Lifecycle state applied by this action.</summary>
        public NotificationLifecycleState NewState { get; set; }

        /// <summary>Operator identifier who performed the action.</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Optional operator note at time of state change.</summary>
        public string? Note { get; set; }
    }

    /// <summary>
    /// Notification envelope enriching a compliance event with per-operator lifecycle state,
    /// escalation metadata, workflow area, audience roles, and audit trail.
    /// </summary>
    public class OperatorNotificationEnvelope
    {
        /// <summary>Unique notification identifier (mirrors the compliance event ID).</summary>
        public string NotificationId { get; set; } = string.Empty;

        /// <summary>The underlying compliance event.</summary>
        public ComplianceEventEnvelope Event { get; set; } = new();

        /// <summary>UTC creation time derived from the compliance event.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Current per-operator lifecycle state.</summary>
        public NotificationLifecycleState LifecycleState { get; set; }

        /// <summary>UTC timestamp when first read by the operator.</summary>
        public DateTimeOffset? ReadAt { get; set; }

        /// <summary>UTC timestamp when acknowledged by the operator.</summary>
        public DateTimeOffset? AcknowledgedAt { get; set; }

        /// <summary>UTC timestamp when dismissed by the operator.</summary>
        public DateTimeOffset? DismissedAt { get; set; }

        /// <summary>UTC timestamp when resolved by the operator.</summary>
        public DateTimeOffset? ResolvedAt { get; set; }

        /// <summary>UTC timestamp when most recently reopened.</summary>
        public DateTimeOffset? ReopenedAt { get; set; }

        /// <summary>Identifier of the last operator who changed the lifecycle state.</summary>
        public string? LastActorId { get; set; }

        /// <summary>Optional operator note from the most recent lifecycle action.</summary>
        public string? OperatorNote { get; set; }

        /// <summary>True when this notification requires operator response.</summary>
        public bool IsActionable { get; set; }

        /// <summary>Workflow area classification for grouping and digest semantics.</summary>
        public NotificationWorkflowArea WorkflowArea { get; set; }

        /// <summary>Operator roles that should receive this notification.</summary>
        public List<OperatorRole> AudienceRoles { get; set; } = new();

        /// <summary>Escalation metadata computed at query time.</summary>
        public NotificationEscalationMetadata EscalationMetadata { get; set; } = new();

        /// <summary>Immutable audit trail of lifecycle state changes.</summary>
        public List<NotificationAuditEntry> AuditTrail { get; set; } = new();

        /// <summary>Recommended remediation guidance for actionable notifications.</summary>
        public string? RemediationGuidance { get; set; }
    }

    /// <summary>Filter and pagination options for the operator notification center.</summary>
    public class OperatorNotificationQueryRequest
    {
        /// <summary>Filter by case ID.</summary>
        public string? CaseId { get; set; }

        /// <summary>Filter by subject or investor identifier.</summary>
        public string? SubjectId { get; set; }

        /// <summary>Filter by entity identifier.</summary>
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

        /// <summary>When true, dismissed notifications are excluded.</summary>
        public bool? ExcludeDismissed { get; set; }

        /// <summary>When true, only unread notifications are returned.</summary>
        public bool? UnreadOnly { get; set; }

        /// <summary>Filter by operator role.</summary>
        public OperatorRole? Role { get; set; }

        /// <summary>Filter by workflow area.</summary>
        public NotificationWorkflowArea? WorkflowArea { get; set; }

        /// <summary>When true, only notifications in Stale or Overdue age buckets are returned.</summary>
        public bool? AgedOnly { get; set; }

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
        /// <summary>IDs to mark as read. When empty, all Unread notifications are marked.</summary>
        public List<string> NotificationIds { get; set; } = new();

        /// <summary>Optional case scoping filter.</summary>
        public string? CaseId { get; set; }

        /// <summary>Optional correlation ID for audit tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Request to acknowledge one or more notifications.</summary>
    public class AcknowledgeNotificationsRequest
    {
        /// <summary>IDs to acknowledge. When empty, all Unread or Read notifications are acknowledged.</summary>
        public List<string> NotificationIds { get; set; } = new();

        /// <summary>Optional operator note recorded as audit evidence.</summary>
        public string? OperatorNote { get; set; }

        /// <summary>Optional case scoping filter.</summary>
        public string? CaseId { get; set; }

        /// <summary>Optional correlation ID for audit tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Request to dismiss one or more notifications from the active queue.</summary>
    public class DismissNotificationsRequest
    {
        /// <summary>IDs to dismiss. When empty, all non-Dismissed notifications are dismissed.</summary>
        public List<string> NotificationIds { get; set; } = new();

        /// <summary>Optional operator note recorded as audit evidence.</summary>
        public string? OperatorNote { get; set; }

        /// <summary>Optional case scoping filter.</summary>
        public string? CaseId { get; set; }

        /// <summary>Optional correlation ID for audit tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Request to resolve one or more notifications.</summary>
    public class ResolveNotificationsRequest
    {
        /// <summary>IDs to resolve. When empty, all acknowledged notifications are resolved.</summary>
        public List<string> NotificationIds { get; set; } = new();

        /// <summary>Optional operator note recorded as audit evidence.</summary>
        public string? OperatorNote { get; set; }

        /// <summary>Optional case scoping filter.</summary>
        public string? CaseId { get; set; }

        /// <summary>Optional correlation ID for audit tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Request to reopen one or more previously resolved or dismissed notifications.</summary>
    public class ReopenNotificationsRequest
    {
        /// <summary>IDs to reopen. When empty, all resolved notifications are reopened.</summary>
        public List<string> NotificationIds { get; set; } = new();

        /// <summary>Optional operator note recorded as audit evidence.</summary>
        public string? OperatorNote { get; set; }

        /// <summary>Optional case scoping filter.</summary>
        public string? CaseId { get; set; }

        /// <summary>Optional correlation ID for audit tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Paginated notification list response with inbox summary and degraded-state flags.</summary>
    public class OperatorNotificationListResponse
    {
        /// <summary>True when the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Paginated notification envelopes.</summary>
        public List<OperatorNotificationEnvelope> Notifications { get; set; } = new();

        /// <summary>Total matching notifications before pagination.</summary>
        public int TotalCount { get; set; }

        /// <summary>Page number.</summary>
        public int Page { get; set; }

        /// <summary>Page size after server-side clamping.</summary>
        public int PageSize { get; set; }

        /// <summary>Inbox summary counts for the requesting operator.</summary>
        public OperatorNotificationInboxSummary InboxSummary { get; set; } = new();

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>True when any upstream source is degraded, reducing completeness.</summary>
        public bool IsDegradedState { get; set; }

        /// <summary>Explanation of the degraded state, if applicable.</summary>
        public string? DegradedReason { get; set; }
    }

    /// <summary>Lifecycle action response for mark-read, acknowledge, dismiss, resolve, or reopen.</summary>
    public class NotificationLifecycleResponse
    {
        /// <summary>True when the action succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Number of notifications affected.</summary>
        public int AffectedCount { get; set; }

        /// <summary>New lifecycle state applied to affected notifications.</summary>
        public NotificationLifecycleState AppliedState { get; set; }

        /// <summary>UTC timestamp when the action was recorded.</summary>
        public DateTimeOffset ActionedAt { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Inbox summary providing badge counts for the operator notification center.</summary>
    public class OperatorNotificationInboxSummary
    {
        /// <summary>Unread notifications.</summary>
        public int UnreadCount { get; set; }

        /// <summary>Read but not yet acknowledged.</summary>
        public int ReadCount { get; set; }

        /// <summary>Acknowledged but not yet resolved.</summary>
        public int AcknowledgedCount { get; set; }

        /// <summary>Dismissed notifications retained in audit history.</summary>
        public int DismissedCount { get; set; }

        /// <summary>Resolved notifications retained for audit.</summary>
        public int ResolvedCount { get; set; }

        /// <summary>Active critical-severity notifications (blockers).</summary>
        public int ActiveBlockerCount { get; set; }

        /// <summary>Active warning-severity notifications.</summary>
        public int ActiveWarningCount { get; set; }

        /// <summary>Total active notifications (Unread + Read + Acknowledged).</summary>
        public int TotalActiveCount { get; set; }

        /// <summary>Escalated notifications.</summary>
        public int EscalatedCount { get; set; }

        /// <summary>SLA-breached notifications.</summary>
        public int SlaBreachedCount { get; set; }

        /// <summary>Notifications with degraded upstream data.</summary>
        public int DegradedStateCount { get; set; }

        /// <summary>UTC timestamp when this summary was computed.</summary>
        public DateTimeOffset ComputedAt { get; set; }
    }

    /// <summary>Unread count response for lightweight badge polling.</summary>
    public class NotificationUnreadCountResponse
    {
        /// <summary>True when the count was retrieved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Total unread notification count.</summary>
        public int UnreadCount { get; set; }

        /// <summary>Unread notifications with Critical severity.</summary>
        public int CriticalUnreadCount { get; set; }

        /// <summary>UTC timestamp when this count was evaluated.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Audience target descriptor defining role applicability for notifications.</summary>
    public class NotificationAudienceTarget
    {
        /// <summary>Operator roles this notification is relevant to.</summary>
        public List<OperatorRole> ApplicableRoles { get; set; } = new();

        /// <summary>Workflow area this audience target belongs to.</summary>
        public NotificationWorkflowArea WorkflowArea { get; set; }

        /// <summary>True when all operators in the audience must explicitly acknowledge.</summary>
        public bool RequiresAcknowledgement { get; set; }

        /// <summary>Optional maximum hours before this notification is considered overdue.</summary>
        public double? SlaHours { get; set; }
    }

    /// <summary>Digest summary for a single workflow area.</summary>
    public class NotificationDigestSummary
    {
        /// <summary>Workflow area this digest group covers.</summary>
        public NotificationWorkflowArea WorkflowArea { get; set; }

        /// <summary>Total notifications in this area for the operator.</summary>
        public int TotalCount { get; set; }

        /// <summary>Unread notifications in this area.</summary>
        public int UnreadCount { get; set; }

        /// <summary>Critical-severity notifications in this area.</summary>
        public int CriticalCount { get; set; }

        /// <summary>Escalated notifications in this area.</summary>
        public int EscalatedCount { get; set; }

        /// <summary>SLA-breached notifications in this area.</summary>
        public int SlaBreachedCount { get; set; }

        /// <summary>Resolved notifications in this area.</summary>
        public int ResolvedCount { get; set; }

        /// <summary>True when this area has degraded or partial upstream data.</summary>
        public bool HasDegradedState { get; set; }

        /// <summary>Most recent notification creation time in this area, or null if empty.</summary>
        public DateTimeOffset? MostRecentAt { get; set; }

        /// <summary>Latest notification creation time (alias for MostRecentAt).</summary>
        public DateTimeOffset? LatestAt { get; set; }

        /// <summary>Recommended next action for this workflow area.</summary>
        public string? RecommendedAction { get; set; }
    }

    /// <summary>Request for a digest summary grouped by workflow area.</summary>
    public class NotificationDigestRequest
    {
        /// <summary>Optional workflow area filter; when null, all areas are included.</summary>
        public NotificationWorkflowArea? WorkflowArea { get; set; }

        /// <summary>Optional operator role filter.</summary>
        public OperatorRole? Role { get; set; }

        /// <summary>Optional earliest creation date filter (UTC).</summary>
        public DateTimeOffset? FromDate { get; set; }

        /// <summary>Optional latest creation date filter (UTC).</summary>
        public DateTimeOffset? ToDate { get; set; }

        /// <summary>When true, only includes notifications in Stale or Overdue age buckets.</summary>
        public bool? AgedOnly { get; set; }
    }

    /// <summary>Response containing digest-grouped notification summaries by workflow area.</summary>
    public class NotificationDigestResponse
    {
        /// <summary>True when the digest was computed successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Digest summaries grouped by workflow area.</summary>
        public List<NotificationDigestSummary> DigestGroups { get; set; } = new();

        /// <summary>Overall totals across all digest groups.</summary>
        public OperatorNotificationInboxSummary OverallSummary { get; set; } = new();

        /// <summary>UTC timestamp when this digest was computed.</summary>
        public DateTimeOffset ComputedAt { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }
}
