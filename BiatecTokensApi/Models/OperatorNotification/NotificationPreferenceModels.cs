using BiatecTokensApi.Models.ComplianceEvents;

namespace BiatecTokensApi.Models.OperatorNotification
{
    /// <summary>
    /// Minimum severity for notifications to surface in the operator's inbox.
    /// </summary>
    public enum NotificationSeverityThreshold
    {
        /// <summary>All notifications regardless of severity are surfaced.</summary>
        All,
        /// <summary>Only Warning and Critical notifications are surfaced.</summary>
        WarningAndAbove,
        /// <summary>Only Critical notifications are surfaced.</summary>
        CriticalOnly
    }

    /// <summary>
    /// How frequently digest summaries are delivered to the operator.
    /// </summary>
    public enum DigestFrequency
    {
        /// <summary>Notifications are delivered immediately as they arrive.</summary>
        Immediate,
        /// <summary>Notifications are batched into a daily digest summary.</summary>
        DailySummary,
        /// <summary>Notifications are batched into a weekly digest summary.</summary>
        WeeklySummary,
        /// <summary>Digest delivery is disabled; operators rely on inbox directly.</summary>
        Disabled
    }

    /// <summary>
    /// Configures how notification digests are generated and delivered for an operator.
    /// </summary>
    public class NotificationDigestPolicy
    {
        /// <summary>How frequently digest summaries are delivered.</summary>
        public DigestFrequency Frequency { get; set; } = DigestFrequency.DailySummary;

        /// <summary>Whether escalated notifications are included in the digest.</summary>
        public bool IncludeEscalated { get; set; } = true;

        /// <summary>Whether the digest includes only critical-severity notifications.</summary>
        public bool IncludeCriticalOnly { get; set; } = false;

        /// <summary>
        /// Hours after which items are included in an aging digest summary.
        /// Defaults to 24 hours.
        /// </summary>
        public int AgingThresholdHours { get; set; } = 24;

        /// <summary>
        /// When true, blocker-level (critical) events are always surfaced immediately
        /// regardless of the configured digest frequency.
        /// </summary>
        public bool AlwaysImmediateForCritical { get; set; } = true;
    }

    /// <summary>
    /// Per-operator notification preference controlling severity thresholds, subscriptions,
    /// digest policy, muting, and fail-closed escalation behaviour.
    /// </summary>
    public class NotificationPreference
    {
        /// <summary>Unique identifier for the operator who owns these preferences.</summary>
        public string OperatorId { get; set; } = string.Empty;

        /// <summary>Optional tenant scope. Null means the preference applies globally for the operator.</summary>
        public string? TenantId { get; set; }

        /// <summary>Optional role context. Null means preferences apply regardless of role.</summary>
        public OperatorRole? Role { get; set; }

        /// <summary>Minimum severity level for notifications to surface in the operator's inbox.</summary>
        public NotificationSeverityThreshold SeverityThreshold { get; set; } = NotificationSeverityThreshold.All;

        /// <summary>
        /// Workflow areas the operator is subscribed to. Null means subscribed to all areas.
        /// </summary>
        public List<NotificationWorkflowArea>? WorkflowAreaSubscriptions { get; set; }

        /// <summary>Whether digest summaries are enabled for this operator.</summary>
        public bool DigestEnabled { get; set; } = true;

        /// <summary>Digest policy controlling frequency, aging thresholds, and critical escalation.</summary>
        public NotificationDigestPolicy DigestPolicy { get; set; } = new();

        /// <summary>Whether SLA escalation is enabled for this operator.</summary>
        public bool EscalationEnabled { get; set; } = true;

        /// <summary>
        /// Workflow areas that are muted for this operator.
        /// Muted notifications are still received but are marked as muted in the UI.
        /// </summary>
        public List<NotificationWorkflowArea>? MutedWorkflowAreas { get; set; }

        /// <summary>
        /// When false (the default), critical/blocker events cannot be suppressed (fail-closed).
        /// When true, the operator has explicitly opted to allow blockers to be suppressed.
        /// </summary>
        public bool AllowBlockerSuppression { get; set; } = false;

        /// <summary>UTC timestamp when the preference record was first created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>UTC timestamp of the most recent update to this preference record.</summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Immutable audit trail of all preference changes.</summary>
        public List<NotificationPreferenceAuditEntry> AuditTrail { get; set; } = new();
    }

    /// <summary>
    /// Audit entry recording a single change to an operator notification preference.
    /// </summary>
    public class NotificationPreferenceAuditEntry
    {
        /// <summary>Unique identifier for this audit entry.</summary>
        public string EntryId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>UTC timestamp when the change was applied.</summary>
        public DateTimeOffset ChangedAt { get; set; }

        /// <summary>Identifier of the actor who applied the change.</summary>
        public string ChangedBy { get; set; } = string.Empty;

        /// <summary>Name of the field that was changed.</summary>
        public string FieldChanged { get; set; } = string.Empty;

        /// <summary>Previous value of the field before the change.</summary>
        public string? PreviousValue { get; set; }

        /// <summary>New value of the field after the change.</summary>
        public string? NewValue { get; set; }

        /// <summary>Optional operator note explaining the reason for the change.</summary>
        public string? Note { get; set; }
    }

    /// <summary>
    /// Request to update one or more fields of an operator notification preference.
    /// Only non-null fields are applied; omitted fields retain their current values.
    /// </summary>
    public class UpdateNotificationPreferenceRequest
    {
        /// <summary>New minimum severity threshold, or null to leave unchanged.</summary>
        public NotificationSeverityThreshold? SeverityThreshold { get; set; }

        /// <summary>New workflow area subscriptions, or null to leave unchanged.</summary>
        public List<NotificationWorkflowArea>? WorkflowAreaSubscriptions { get; set; }

        /// <summary>Enable or disable digest delivery, or null to leave unchanged.</summary>
        public bool? DigestEnabled { get; set; }

        /// <summary>Updated digest policy, or null to leave unchanged.</summary>
        public NotificationDigestPolicy? DigestPolicy { get; set; }

        /// <summary>Enable or disable SLA escalation, or null to leave unchanged.</summary>
        public bool? EscalationEnabled { get; set; }

        /// <summary>Workflow areas to mute, or null to leave unchanged.</summary>
        public List<NotificationWorkflowArea>? MutedWorkflowAreas { get; set; }

        /// <summary>Allow or deny blocker suppression, or null to leave unchanged.</summary>
        public bool? AllowBlockerSuppression { get; set; }

        /// <summary>Optional audit note explaining the reason for this preference update.</summary>
        public string? Note { get; set; }
    }

    /// <summary>
    /// Response containing an operator's notification preference record.
    /// </summary>
    public class NotificationPreferenceResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The operator preference record, populated on success.</summary>
        public NotificationPreference? Preference { get; set; }

        /// <summary>Machine-readable error code when Success is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message when Success is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Whether the service is operating in a degraded state.</summary>
        public bool IsDegradedState { get; set; }

        /// <summary>Description of the degraded state, if applicable.</summary>
        public string? DegradedReason { get; set; }
    }

    /// <summary>
    /// Routing metadata added to notification envelopes indicating how the notification
    /// should be surfaced for a specific operator based on their preferences.
    /// </summary>
    public class NotificationRoutingMetadata
    {
        /// <summary>
        /// Whether this notification should be delivered immediately,
        /// bypassing any configured digest schedule.
        /// </summary>
        public bool ImmediateDelivery { get; set; }

        /// <summary>
        /// Whether this workflow area is muted for the requesting operator.
        /// Muted notifications are still received but flagged for suppressed UI rendering.
        /// </summary>
        public bool IsMuted { get; set; }

        /// <summary>Whether this notification's severity meets the operator's configured threshold.</summary>
        public bool PassesSeverityThreshold { get; set; }

        /// <summary>Whether this notification falls within a subscribed workflow area.</summary>
        public bool IsInSubscribedArea { get; set; }

        /// <summary>
        /// Suggested digest window label when immediate delivery is not required.
        /// For example, "DailySummary" or "WeeklySummary".
        /// </summary>
        public string? SuggestedDigestWindow { get; set; }
    }
}
