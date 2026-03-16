namespace BiatecTokensApi.Models.OngoingMonitoring
{
    // ── Enums ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lifecycle status of an ongoing monitoring task, covering the full range of
    /// operational states from healthy through suspended or restricted.
    /// </summary>
    public enum MonitoringTaskStatus
    {
        /// <summary>No review is due; subject is in good standing.</summary>
        Healthy,
        /// <summary>A review is approaching — due within the configured lead-time window.</summary>
        DueSoon,
        /// <summary>The review due date has passed without a recorded review.</summary>
        Overdue,
        /// <summary>A reassessment has been started and is actively in progress.</summary>
        InProgress,
        /// <summary>Waiting for required evidence before the reassessment can proceed.</summary>
        AwaitingEvidence,
        /// <summary>Escalated to a senior reviewer or compliance officer.</summary>
        Escalated,
        /// <summary>Blocked pending manual intervention or remediation.</summary>
        Blocked,
        /// <summary>Deferred to a future date with a recorded rationale.</summary>
        Deferred,
        /// <summary>Review completed; outcome recorded — terminal for this task cycle.</summary>
        Resolved,
        /// <summary>Subject suspended pending investigation — elevated-risk terminal state.</summary>
        Suspended,
        /// <summary>Subject restricted from ongoing token activities — highest-risk terminal state.</summary>
        Restricted
    }

    /// <summary>
    /// The reason why a monitoring task was created, allowing operators to distinguish
    /// between routine scheduled reviews and risk-triggered reassessments.
    /// </summary>
    public enum ReassessmentReason
    {
        /// <summary>Regular periodic review based on the configured monitoring schedule.</summary>
        PeriodicSchedule,
        /// <summary>A KYC or identity document is expiring or has expired.</summary>
        DocumentExpiry,
        /// <summary>A sanctions list update requires the subject to be rescreened.</summary>
        SanctionsRefresh,
        /// <summary>An AML provider has flagged a potential match requiring rescreening.</summary>
        AmlRescreening,
        /// <summary>The subject's risk score has elevated and requires review.</summary>
        RiskSignalElevated,
        /// <summary>An analyst has manually requested a reassessment.</summary>
        ManualAnalystRequest,
        /// <summary>A follow-up task triggered by a prior escalation.</summary>
        EscalationFollowUp,
        /// <summary>An external webhook signal triggered the reassessment.</summary>
        WebhookSignal,
        /// <summary>Regulatory jurisdiction rules have changed, requiring re-evaluation.</summary>
        JurisdictionChange
    }

    /// <summary>Severity level assigned to a monitoring task, used for prioritisation.</summary>
    public enum MonitoringTaskSeverity
    {
        /// <summary>Routine review; no urgent risk indicators.</summary>
        Low,
        /// <summary>Elevated attention warranted but not time-critical.</summary>
        Medium,
        /// <summary>Significant risk or regulatory concern requiring prompt action.</summary>
        High,
        /// <summary>Critical risk requiring immediate action; overrides normal queuing.</summary>
        Critical
    }

    /// <summary>
    /// Resolution outcome when a monitoring task is closed, providing an auditable
    /// record of the final determination.
    /// </summary>
    public enum MonitoringTaskResolution
    {
        /// <summary>No issues found; subject remains in good standing.</summary>
        Clear,
        /// <summary>Issues identified and remediated before closure.</summary>
        ActionTaken,
        /// <summary>Task deferred — will resurface at a later date.</summary>
        Deferred,
        /// <summary>Escalated to a higher authority; not resolved at this tier.</summary>
        EscalatedToHigherAuthority,
        /// <summary>Subject suspended pending further investigation.</summary>
        SubjectSuspended,
        /// <summary>Subject restricted from token issuance activities.</summary>
        SubjectRestricted
    }

    /// <summary>Type of event recorded in the monitoring task timeline.</summary>
    public enum MonitoringTaskEventType
    {
        /// <summary>Task was created.</summary>
        TaskCreated,
        /// <summary>Reassessment was started by an analyst.</summary>
        ReassessmentStarted,
        /// <summary>Required evidence was requested from the subject.</summary>
        EvidenceRequested,
        /// <summary>Required evidence was received.</summary>
        EvidenceReceived,
        /// <summary>Task was deferred to a later date.</summary>
        TaskDeferred,
        /// <summary>Task was escalated.</summary>
        TaskEscalated,
        /// <summary>Task was resolved with an outcome.</summary>
        TaskResolved,
        /// <summary>Subject was suspended.</summary>
        SubjectSuspended,
        /// <summary>Subject was restricted.</summary>
        SubjectRestricted,
        /// <summary>A note was added by a reviewer.</summary>
        ReviewNoteAdded,
        /// <summary>Task severity was changed.</summary>
        SeverityChanged,
        /// <summary>Task status was changed.</summary>
        StatusChanged,
        /// <summary>Task was re-opened after a deferral period elapsed.</summary>
        DeferralExpired
    }

    // ── Aggregates ────────────────────────────────────────────────────────────

    /// <summary>
    /// An ongoing compliance monitoring task representing a single review cycle
    /// for a subject linked to a compliance case.  Monitoring tasks are the
    /// primary operational unit for compliance analysts — each task tracks a
    /// discrete review obligation with its own state machine, evidence requirements,
    /// due date, and auditable event trail.
    /// </summary>
    public class MonitoringTask
    {
        /// <summary>Unique identifier for this monitoring task.</summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>Linked compliance case identifier (required).</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Issuer scoping this monitoring task.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Subject under review (investor, issuer, etc.).</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Current lifecycle status of this task.</summary>
        public MonitoringTaskStatus Status { get; set; } = MonitoringTaskStatus.Healthy;

        /// <summary>Why this monitoring task was created.</summary>
        public ReassessmentReason Reason { get; set; }

        /// <summary>Severity level driving prioritisation.</summary>
        public MonitoringTaskSeverity Severity { get; set; } = MonitoringTaskSeverity.Low;

        /// <summary>When this task was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>When this task was last updated.</summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>When the review is due.</summary>
        public DateTimeOffset DueAt { get; set; }

        /// <summary>When this task was completed (resolved/suspended/restricted).</summary>
        public DateTimeOffset? CompletedAt { get; set; }

        /// <summary>Actor who created this task.</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>Analyst currently assigned to this task.</summary>
        public string? AssignedTo { get; set; }

        /// <summary>
        /// When the task is deferred, the date from which it should resurface.
        /// Null when not deferred.
        /// </summary>
        public DateTimeOffset? DeferredUntil { get; set; }

        /// <summary>Rationale provided when the task was deferred.</summary>
        public string? DeferralRationale { get; set; }

        /// <summary>
        /// Evidence types required before this reassessment can be completed.
        /// Empty list means no specific evidence is required upfront.
        /// </summary>
        public List<string> RequiredEvidenceTypes { get; set; } = new();

        /// <summary>Reason provided when this task was escalated.</summary>
        public string? EscalationReason { get; set; }

        /// <summary>Final resolution notes provided when the task was closed.</summary>
        public string? ResolutionNotes { get; set; }

        /// <summary>
        /// Outcome recorded when the task is resolved. Null while the task is open.
        /// </summary>
        public MonitoringTaskResolution? ResolutionOutcome { get; set; }

        /// <summary>Optional correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Optional free-text notes captured at task creation.</summary>
        public string? Notes { get; set; }

        /// <summary>Immutable ordered audit trail of events for this task.</summary>
        public List<MonitoringTaskEvent> Timeline { get; set; } = new();

        /// <summary>Structured attributes for provider-specific or integration metadata.</summary>
        public Dictionary<string, string> Attributes { get; set; } = new();
    }

    /// <summary>
    /// An immutable audit trail entry recording a state change or analyst action
    /// on a monitoring task.
    /// </summary>
    public class MonitoringTaskEvent
    {
        /// <summary>Unique identifier for this event.</summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>Monitoring task this event belongs to.</summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>Type of event recorded.</summary>
        public MonitoringTaskEventType EventType { get; set; }

        /// <summary>When the event occurred (UTC).</summary>
        public DateTimeOffset OccurredAt { get; set; }

        /// <summary>Actor who caused this event.</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Human-readable description of the event.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Status the task transitioned from (when applicable).</summary>
        public MonitoringTaskStatus? FromStatus { get; set; }

        /// <summary>Status the task transitioned to (when applicable).</summary>
        public MonitoringTaskStatus? ToStatus { get; set; }

        /// <summary>Structured metadata for this event.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    // ── Request / Response types ──────────────────────────────────────────────

    /// <summary>Request to create a new monitoring task for a compliance case.</summary>
    public class CreateMonitoringTaskRequest
    {
        /// <summary>Linked compliance case identifier (required).</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Issuer scoping this task (required).</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Subject under review (required).</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Why this monitoring task is being created (required).</summary>
        public ReassessmentReason Reason { get; set; }

        /// <summary>Initial severity level.</summary>
        public MonitoringTaskSeverity Severity { get; set; } = MonitoringTaskSeverity.Low;

        /// <summary>
        /// When the review is due.  If not specified, defaults to the configured
        /// review window based on the linked case's monitoring schedule or a
        /// platform-default of 30 days.
        /// </summary>
        public DateTimeOffset? DueAt { get; set; }

        /// <summary>
        /// Evidence types required before this reassessment can be completed.
        /// </summary>
        public List<string>? RequiredEvidenceTypes { get; set; }

        /// <summary>Optional free-text notes explaining the monitoring context.</summary>
        public string? Notes { get; set; }

        /// <summary>Optional analyst to assign the task to.</summary>
        public string? AssignedTo { get; set; }

        /// <summary>Optional correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Optional structured attributes for provider or integration metadata.</summary>
        public Dictionary<string, string>? Attributes { get; set; }
    }

    /// <summary>Response from creating a monitoring task.</summary>
    public class CreateMonitoringTaskResponse
    {
        /// <summary>True when the task was created successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The created monitoring task.</summary>
        public MonitoringTask? Task { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Filters and pagination parameters for listing monitoring tasks.</summary>
    public class ListMonitoringTasksRequest
    {
        /// <summary>Filter by issuer identifier.</summary>
        public string? IssuerId { get; set; }

        /// <summary>Filter by subject identifier.</summary>
        public string? SubjectId { get; set; }

        /// <summary>Filter by linked compliance case identifier.</summary>
        public string? CaseId { get; set; }

        /// <summary>Filter by task status.</summary>
        public MonitoringTaskStatus? Status { get; set; }

        /// <summary>Filter by reason.</summary>
        public ReassessmentReason? Reason { get; set; }

        /// <summary>Filter by severity.</summary>
        public MonitoringTaskSeverity? Severity { get; set; }

        /// <summary>
        /// When true, include only tasks whose <see cref="MonitoringTask.DueAt"/> is in the past.
        /// </summary>
        public bool? OverdueOnly { get; set; }

        /// <summary>Page number (1-based).</summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>Page size (1–100).</summary>
        public int PageSize { get; set; } = 20;
    }

    /// <summary>Response from listing monitoring tasks.</summary>
    public class ListMonitoringTasksResponse
    {
        /// <summary>True when the request succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Matching monitoring tasks (current page).</summary>
        public List<MonitoringTask> Tasks { get; set; } = new();

        /// <summary>Total number of tasks matching the filter across all pages.</summary>
        public int TotalCount { get; set; }

        /// <summary>Current page number.</summary>
        public int PageNumber { get; set; }

        /// <summary>Page size used for this response.</summary>
        public int PageSize { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response wrapping a single monitoring task retrieval.</summary>
    public class GetMonitoringTaskResponse
    {
        /// <summary>True when the task was found.</summary>
        public bool Success { get; set; }

        /// <summary>The monitoring task.</summary>
        public MonitoringTask? Task { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Request to start a reassessment for an existing monitoring task.</summary>
    public class StartReassessmentRequest
    {
        /// <summary>Optional notes from the analyst starting the reassessment.</summary>
        public string? Notes { get; set; }

        /// <summary>Optional analyst to assign.</summary>
        public string? AssignedTo { get; set; }

        /// <summary>
        /// Evidence types required before this reassessment can complete.
        /// Replaces the task's existing required evidence list if provided.
        /// </summary>
        public List<string>? RequiredEvidenceTypes { get; set; }
    }

    /// <summary>Response from starting a reassessment.</summary>
    public class StartReassessmentResponse
    {
        /// <summary>True when the reassessment was started successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The updated monitoring task.</summary>
        public MonitoringTask? Task { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Request to defer a monitoring task to a future date.</summary>
    public class DeferMonitoringTaskRequest
    {
        /// <summary>Date until which the task is deferred (required, must be in the future).</summary>
        public DateTimeOffset DeferUntil { get; set; }

        /// <summary>Rationale for the deferral (required; must be non-empty).</summary>
        public string Rationale { get; set; } = string.Empty;
    }

    /// <summary>Response from deferring a monitoring task.</summary>
    public class DeferMonitoringTaskResponse
    {
        /// <summary>True when the task was deferred successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The updated monitoring task.</summary>
        public MonitoringTask? Task { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Request to escalate a monitoring task.</summary>
    public class EscalateMonitoringTaskRequest
    {
        /// <summary>Reason for the escalation (required; must be non-empty).</summary>
        public string EscalationReason { get; set; } = string.Empty;

        /// <summary>Optionally raise the severity when escalating.</summary>
        public MonitoringTaskSeverity? Severity { get; set; }
    }

    /// <summary>Response from escalating a monitoring task.</summary>
    public class EscalateMonitoringTaskResponse
    {
        /// <summary>True when the task was escalated successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The updated monitoring task.</summary>
        public MonitoringTask? Task { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Request to close a monitoring task with an auditable resolution.</summary>
    public class CloseMonitoringTaskRequest
    {
        /// <summary>Final outcome of the monitoring review (required).</summary>
        public MonitoringTaskResolution Resolution { get; set; }

        /// <summary>Notes documenting the basis for this resolution (required).</summary>
        public string ResolutionNotes { get; set; } = string.Empty;
    }

    /// <summary>Response from closing a monitoring task.</summary>
    public class CloseMonitoringTaskResponse
    {
        /// <summary>True when the task was closed successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The updated monitoring task.</summary>
        public MonitoringTask? Task { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }
}
