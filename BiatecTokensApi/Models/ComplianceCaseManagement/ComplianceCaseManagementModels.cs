using BiatecTokensApi.Models.Webhook;

namespace BiatecTokensApi.Models.ComplianceCaseManagement
{
    // ── Enums ──────────────────────────────────────────────────────────────────

    /// <summary>Lifecycle state of a compliance case.</summary>
    public enum ComplianceCaseState
    {
        /// <summary>Case has been created and is awaiting initial processing.</summary>
        Intake,
        /// <summary>Case is waiting for required evidence to be submitted.</summary>
        EvidencePending,
        /// <summary>Case is under active review.</summary>
        UnderReview,
        /// <summary>Case has been escalated due to a compliance concern.</summary>
        Escalated,
        /// <summary>Case has open remediation tasks that must be resolved.</summary>
        Remediating,
        /// <summary>Case has been approved — terminal state.</summary>
        Approved,
        /// <summary>Case has been rejected — terminal state.</summary>
        Rejected,
        /// <summary>Case evidence has expired and is no longer valid.</summary>
        Stale,
        /// <summary>Case is blocked pending manual intervention.</summary>
        Blocked
    }

    /// <summary>Priority level for a compliance case.</summary>
    public enum CasePriority { Low, Medium, High, Critical }

    /// <summary>Classification of what a compliance case is evaluating.</summary>
    public enum CaseType { InvestorEligibility, LaunchPackage, OngoingMonitoring }

    /// <summary>Status of an individual remediation task within a case.</summary>
    public enum RemediationTaskStatus { Open, InProgress, Resolved, Dismissed }

    /// <summary>Type of escalation raised against a case.</summary>
    public enum EscalationType
    {
        SanctionsHit,
        WatchlistMatch,
        JurisdictionConflict,
        AdverseMedia,
        ManualEscalation
    }

    /// <summary>Review status of a case escalation.</summary>
    public enum EscalationStatus { Open, UnderReview, Resolved, Escalated }

    /// <summary>Type of event recorded in the case timeline.</summary>
    public enum CaseTimelineEventType
    {
        CaseCreated,
        StateTransition,
        EvidenceAdded,
        EvidenceStale,
        EscalationRaised,
        EscalationResolved,
        RemediationTaskAdded,
        RemediationTaskResolved,
        ReviewerAssigned,
        ReviewerNoteAdded,
        ReadinessChanged,
        CaseExported,
        MonitoringScheduleSet,
        MonitoringReviewRecorded,
        MonitoringFollowUpCreated
    }

    /// <summary>Outcome of a periodic monitoring review.</summary>
    public enum MonitoringReviewOutcome
    {
        /// <summary>No concerns found; subject remains in good standing.</summary>
        Clear,
        /// <summary>Minor changes observed; continue monitoring on schedule.</summary>
        AdvisoryNote,
        /// <summary>A concern was identified that requires attention but not immediate action.</summary>
        ConcernIdentified,
        /// <summary>A critical finding requiring immediate escalation and follow-up case.</summary>
        EscalationRequired
    }

    /// <summary>Frequency preset for monitoring schedules.</summary>
    public enum MonitoringFrequency
    {
        /// <summary>Review every 30 days.</summary>
        Monthly,
        /// <summary>Review every 90 days.</summary>
        Quarterly,
        /// <summary>Review every 180 days.</summary>
        SemiAnnual,
        /// <summary>Review every 365 days.</summary>
        Annual,
        /// <summary>Custom interval specified in <see cref="MonitoringSchedule.IntervalDays"/>.</summary>
        Custom
    }

    /// <summary>Validity status of a piece of evidence attached to a case.</summary>
    public enum CaseEvidenceStatus { Valid, Pending, Stale, Missing, Rejected }

    /// <summary>Severity level for a remediation task blocker.</summary>
    public enum EvidenceIssueSeverityLevel { Low, Medium, High, Critical }

    /// <summary>Urgency classification derived from SLA due dates.</summary>
    public enum CaseUrgencyBand
    {
        /// <summary>No SLA defined or review is not due soon.</summary>
        Normal,
        /// <summary>Review is due within 7 days.</summary>
        Warning,
        /// <summary>Review is due within 3 days or is already overdue.</summary>
        Critical,
        /// <summary>Case has been explicitly deferred.</summary>
        Deferred
    }

    /// <summary>Outcome of a webhook delivery attempt for a case-related event.</summary>
    public enum CaseDeliveryOutcome
    {
        /// <summary>Delivery not yet attempted.</summary>
        Pending,
        /// <summary>Delivery succeeded (HTTP 2xx received).</summary>
        Success,
        /// <summary>Delivery failed and will not be retried (terminal).</summary>
        Failure,
        /// <summary>Delivery failed but a retry has been scheduled.</summary>
        RetryScheduled,
        /// <summary>All retry attempts have been exhausted without success.</summary>
        RetryExhausted
    }

    // ── Aggregates ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Immutable record capturing a single assignment or reassignment event for a compliance case.
    /// Provides full traceability of ownership changes including previous owner, new owner, reason, and timestamp.
    /// </summary>
    public class CaseAssignmentRecord
    {
        /// <summary>Unique identifier for this assignment event.</summary>
        public string AssignmentId { get; set; } = string.Empty;

        /// <summary>Case this record belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>User ID of the reviewer before this assignment change (null for first assignment).</summary>
        public string? PreviousReviewerId { get; set; }

        /// <summary>User ID of the reviewer after this assignment change.</summary>
        public string? NewReviewerId { get; set; }

        /// <summary>Team ID before this assignment change (null if team assignment not used).</summary>
        public string? PreviousTeamId { get; set; }

        /// <summary>Team ID after this assignment change.</summary>
        public string? NewTeamId { get; set; }

        /// <summary>Human-readable reason for this assignment change.</summary>
        public string? Reason { get; set; }

        /// <summary>Actor who performed this assignment.</summary>
        public string AssignedBy { get; set; } = string.Empty;

        /// <summary>When this assignment took effect.</summary>
        public DateTimeOffset AssignedAt { get; set; }
    }

    /// <summary>
    /// SLA metadata for a compliance case, specifying review deadlines, overdue status,
    /// and the derived urgency band used for worklist prioritisation.
    /// </summary>
    public class CaseSlaMetadata
    {
        /// <summary>When the case review is due.</summary>
        public DateTimeOffset? ReviewDueAt { get; set; }

        /// <summary>When a mandatory escalation is due if the case has not progressed.</summary>
        public DateTimeOffset? EscalationDueAt { get; set; }

        /// <summary>True when the review due date has passed without resolution.</summary>
        public bool IsOverdue { get; set; }

        /// <summary>When the case became overdue (null if not yet overdue).</summary>
        public DateTimeOffset? OverdueSince { get; set; }

        /// <summary>Urgency classification derived from due dates and time remaining.</summary>
        public CaseUrgencyBand UrgencyBand { get; set; } = CaseUrgencyBand.Normal;

        /// <summary>Actor who last configured the SLA for this case.</summary>
        public string? SetBy { get; set; }

        /// <summary>When the SLA was last configured.</summary>
        public DateTimeOffset? SetAt { get; set; }

        /// <summary>Optional notes about the SLA rationale.</summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// A persisted record of a webhook delivery attempt for a case-related event.
    /// Supports operational troubleshooting and delivery auditability.
    /// </summary>
    public class CaseDeliveryRecord
    {
        /// <summary>Unique identifier for this delivery record.</summary>
        public string DeliveryId { get; set; } = string.Empty;

        /// <summary>Case this delivery is associated with.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Webhook event ID that was delivered.</summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>Type of event delivered.</summary>
        public WebhookEventType EventType { get; set; }

        /// <summary>Outcome of the delivery attempt.</summary>
        public CaseDeliveryOutcome Outcome { get; set; }

        /// <summary>HTTP status code received (null if no HTTP response).</summary>
        public int? HttpStatusCode { get; set; }

        /// <summary>When the delivery was attempted.</summary>
        public DateTimeOffset AttemptedAt { get; set; }

        /// <summary>Number of delivery attempts made so far.</summary>
        public int AttemptCount { get; set; }

        /// <summary>When the next retry is scheduled (null if no retry pending).</summary>
        public DateTimeOffset? NextRetryAt { get; set; }

        /// <summary>Summary of the last error (null on success).</summary>
        public string? LastErrorSummary { get; set; }

        /// <summary>True when this is a transient failure that may succeed on retry.</summary>
        public bool IsTransientFailure { get; set; }

        /// <summary>
        /// Next recommended operator action when the delivery is in a failed or exhausted state.
        /// Null when no action is required.
        /// </summary>
        public string? RecommendedAction { get; set; }
    }

    /// <summary>
    /// Root aggregate representing a compliance case tracking all evidence,
    /// escalations, remediation tasks and timeline for a subject.
    /// </summary>
    public class ComplianceCase
    {
        /// <summary>Unique identifier for this case.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Issuer scoping this case.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Identifier of the subject being evaluated (investor, token, etc.).</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Classification of this case.</summary>
        public CaseType Type { get; set; }

        /// <summary>Current lifecycle state.</summary>
        public ComplianceCaseState State { get; set; }

        /// <summary>Priority assigned to this case.</summary>
        public CasePriority Priority { get; set; }

        /// <summary>User ID of the reviewer currently assigned.</summary>
        public string? AssignedReviewerId { get; set; }

        /// <summary>Team ID currently assigned to this case.</summary>
        public string? AssignedTeamId { get; set; }

        /// <summary>Actor who created this case.</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>Timestamp when the case was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Timestamp when the case was last modified.</summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Jurisdiction code relevant to this case (e.g., "US", "EU").</summary>
        public string? Jurisdiction { get; set; }

        /// <summary>Links to ComplianceOrchestration decisionIds associated with this case.</summary>
        public List<string> LinkedDecisionIds { get; set; } = new();

        /// <summary>Human-readable reason the case was closed.</summary>
        public string? ClosureReason { get; set; }

        /// <summary>Timestamp when the case was closed (Approved or Rejected).</summary>
        public DateTimeOffset? ClosedAt { get; set; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Optional external reference (e.g., third-party case ID).</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Timestamp when the evidence bundle expires.</summary>
        public DateTimeOffset? EvidenceExpiresAt { get; set; }

        /// <summary>True when the evidence bundle has passed its expiry date.</summary>
        public bool IsEvidenceStale { get; set; }

        /// <summary>Evidence summaries attached to this case.</summary>
        public List<CaseEvidenceSummary> EvidenceSummaries { get; set; } = new();

        /// <summary>Remediation tasks open against this case.</summary>
        public List<RemediationTask> RemediationTasks { get; set; } = new();

        /// <summary>Escalations raised against this case.</summary>
        public List<CaseEscalation> Escalations { get; set; } = new();

        /// <summary>Immutable audit trail of events for this case.</summary>
        public List<CaseTimelineEntry> Timeline { get; set; } = new();

        /// <summary>Optional ongoing monitoring schedule. Set when the case transitions to a monitoring program.</summary>
        public MonitoringSchedule? MonitoringSchedule { get; set; }

        /// <summary>History of periodic monitoring reviews recorded against this case.</summary>
        public List<MonitoringReview> MonitoringReviews { get; set; } = new();

        /// <summary>Immutable history of assignment and reassignment events for this case.</summary>
        public List<CaseAssignmentRecord> AssignmentHistory { get; set; } = new();

        /// <summary>SLA metadata defining review deadlines and urgency classification.</summary>
        public CaseSlaMetadata? SlaMetadata { get; set; }

        /// <summary>Persisted webhook delivery records for events emitted on this case.</summary>
        public List<CaseDeliveryRecord> DeliveryRecords { get; set; } = new();
    }

    /// <summary>
    /// Normalized summary of a piece of evidence attached to a case.
    /// Does not contain raw provider payloads.
    /// </summary>
    public class CaseEvidenceSummary
    {
        /// <summary>Unique identifier for this evidence record.</summary>
        public string EvidenceId { get; set; } = string.Empty;

        /// <summary>Case this evidence belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Category of evidence: "KYC", "AML", "Jurisdiction", "Identity", etc.</summary>
        public string EvidenceType { get; set; } = string.Empty;

        /// <summary>Validity status of this evidence.</summary>
        public CaseEvidenceStatus Status { get; set; }

        /// <summary>Name of the provider that supplied this evidence.</summary>
        public string? ProviderName { get; set; }

        /// <summary>Provider-side reference ID for this evidence record.</summary>
        public string? ProviderReference { get; set; }

        /// <summary>When the evidence was captured by the provider.</summary>
        public DateTimeOffset? CapturedAt { get; set; }

        /// <summary>When this evidence expires.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>True when the evidence has passed its expiry date.</summary>
        public bool IsExpired { get; set; }

        /// <summary>Human-readable summary of the evidence result.</summary>
        public string? Summary { get; set; }

        /// <summary>Normalized key-value attributes from the evidence (no raw payloads).</summary>
        public Dictionary<string, string> NormalizedAttributes { get; set; } = new();

        /// <summary>True when this evidence is blocking case readiness.</summary>
        public bool IsBlockingReadiness { get; set; }

        /// <summary>Explanation of why this evidence is blocking readiness.</summary>
        public string? BlockingReason { get; set; }

        /// <summary>Actor who added this evidence.</summary>
        public string AddedBy { get; set; } = string.Empty;

        /// <summary>Timestamp when this evidence was added to the case.</summary>
        public DateTimeOffset AddedAt { get; set; }
    }

    /// <summary>An actionable remediation task that must be resolved before a case can progress.</summary>
    public class RemediationTask
    {
        /// <summary>Unique identifier for this task.</summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>Case this task belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Short title of the remediation action required.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed description of what needs to be done.</summary>
        public string? Description { get; set; }

        /// <summary>User ID of the person responsible for resolving this task.</summary>
        public string? OwnerId { get; set; }

        /// <summary>Deadline for completing this task.</summary>
        public DateTimeOffset? DueAt { get; set; }

        /// <summary>Current status of this task.</summary>
        public RemediationTaskStatus Status { get; set; }

        /// <summary>True when this task must be resolved before the case can be approved.</summary>
        public bool IsBlockingCase { get; set; }

        /// <summary>Notes provided when the task was resolved or dismissed.</summary>
        public string? ResolutionNotes { get; set; }

        /// <summary>Actor who created this task.</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>Timestamp when this task was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Timestamp when this task was resolved or dismissed.</summary>
        public DateTimeOffset? ResolvedAt { get; set; }

        /// <summary>Severity level indicating urgency of the blocker.</summary>
        public EvidenceIssueSeverityLevel BlockerSeverity { get; set; }
    }

    /// <summary>An escalation raised against a case requiring compliance review.</summary>
    public class CaseEscalation
    {
        /// <summary>Unique identifier for this escalation.</summary>
        public string EscalationId { get; set; } = string.Empty;

        /// <summary>Case this escalation belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Type/reason for this escalation.</summary>
        public EscalationType Type { get; set; }

        /// <summary>Current review status of this escalation.</summary>
        public EscalationStatus Status { get; set; }

        /// <summary>Human-readable description of the escalation reason.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Name of the screening provider that surfaced this hit.</summary>
        public string? ScreeningSource { get; set; }

        /// <summary>Categories matched by the screening hit (e.g., "Sanctions", "PEP").</summary>
        public List<string> MatchedCategories { get; set; } = new();

        /// <summary>Provider-reported confidence score (0.0–1.0) for the match.</summary>
        public double? ConfidenceScore { get; set; }

        /// <summary>Actor who raised this escalation.</summary>
        public string RaisedBy { get; set; } = string.Empty;

        /// <summary>Timestamp when this escalation was raised.</summary>
        public DateTimeOffset RaisedAt { get; set; }

        /// <summary>Actor who reviewed or resolved this escalation.</summary>
        public string? ReviewedBy { get; set; }

        /// <summary>Timestamp when this escalation was reviewed.</summary>
        public DateTimeOffset? ReviewedAt { get; set; }

        /// <summary>Notes provided when this escalation was resolved.</summary>
        public string? ResolutionNotes { get; set; }

        /// <summary>True when this escalation requires a manual compliance review.</summary>
        public bool RequiresManualReview { get; set; }
    }

    /// <summary>An immutable audit trail entry recording a significant event in a case's lifecycle.</summary>
    public class CaseTimelineEntry
    {
        /// <summary>Unique identifier for this timeline entry.</summary>
        public string EntryId { get; set; } = string.Empty;

        /// <summary>Case this entry belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Type of event recorded.</summary>
        public CaseTimelineEventType EventType { get; set; }

        /// <summary>When the event occurred.</summary>
        public DateTimeOffset OccurredAt { get; set; }

        /// <summary>Actor who caused this event.</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Display name of the actor.</summary>
        public string? ActorDisplayName { get; set; }

        /// <summary>State before the transition (only for StateTransition events).</summary>
        public ComplianceCaseState? FromState { get; set; }

        /// <summary>State after the transition (only for StateTransition events).</summary>
        public ComplianceCaseState? ToState { get; set; }

        /// <summary>Human-readable description of this event.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Additional structured metadata about the event.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>Evaluated readiness summary for a compliance case (fail-closed semantics).</summary>
    public class CaseReadinessSummary
    {
        /// <summary>Case identifier.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>True when the case has no blocking issues and is ready to proceed.</summary>
        public bool IsReady { get; set; }

        /// <summary>True when required data is absent and readiness is failed-closed.</summary>
        public bool FailedClosed { get; set; }

        /// <summary>Issues that must be resolved before the case can be approved.</summary>
        public List<string> BlockingIssues { get; set; } = new();

        /// <summary>Non-blocking warnings that should be reviewed.</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>Evidence types that are missing from the case.</summary>
        public List<string> MissingEvidence { get; set; } = new();

        /// <summary>Count of open remediation tasks on the case.</summary>
        public int OpenRemediationTasks { get; set; }

        /// <summary>Count of open escalations that require manual review.</summary>
        public int CriticalEscalations { get; set; }

        /// <summary>True when any evidence on the case has become stale.</summary>
        public bool HasStaleEvidence { get; set; }

        /// <summary>Human-readable explanation of the readiness evaluation result.</summary>
        public string? ReadinessExplanation { get; set; }

        /// <summary>Timestamp when this readiness summary was evaluated.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }
    }

    // ── Request / Response types ───────────────────────────────────────────────

    /// <summary>Request to create a new compliance case.</summary>
    public class CreateComplianceCaseRequest
    {
        /// <summary>Issuer scoping this case.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Identifier of the subject being evaluated.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Classification of this case.</summary>
        public CaseType Type { get; set; }

        /// <summary>Initial priority for this case.</summary>
        public CasePriority Priority { get; set; } = CasePriority.Medium;

        /// <summary>Jurisdiction relevant to this case.</summary>
        public string? Jurisdiction { get; set; }

        /// <summary>Optional external reference for cross-system linking.</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Optional links to existing compliance decision IDs.</summary>
        public List<string>? LinkedDecisionIds { get; set; }
    }

    /// <summary>Response returned when a compliance case is created.</summary>
    public class CreateComplianceCaseResponse
    {
        /// <summary>True when the case was successfully created or already existed (idempotent).</summary>
        public bool Success { get; set; }

        /// <summary>The created or existing case.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>True when an existing active case was returned instead of creating a new one.</summary>
        public bool WasIdempotent { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Request to update mutable fields on a compliance case.</summary>
    public class UpdateComplianceCaseRequest
    {
        /// <summary>New priority for this case.</summary>
        public CasePriority? Priority { get; set; }

        /// <summary>Reviewer to assign to this case.</summary>
        public string? AssignedReviewerId { get; set; }

        /// <summary>Updated jurisdiction for this case.</summary>
        public string? Jurisdiction { get; set; }

        /// <summary>Updated external reference.</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Additional decision IDs to link to this case.</summary>
        public List<string>? AdditionalLinkedDecisionIds { get; set; }
    }

    /// <summary>Response returned when a compliance case is updated or transitioned.</summary>
    public class UpdateComplianceCaseResponse
    {
        /// <summary>True when the update succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The updated case.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Request to transition a case to a new lifecycle state.</summary>
    public class TransitionCaseStateRequest
    {
        /// <summary>The target state to transition the case to.</summary>
        public ComplianceCaseState NewState { get; set; }

        /// <summary>Optional reason for this state transition.</summary>
        public string? Reason { get; set; }
    }

    /// <summary>Filter and pagination parameters for listing compliance cases.</summary>
    public class ListComplianceCasesRequest
    {
        /// <summary>Filter by lifecycle state.</summary>
        public ComplianceCaseState? State { get; set; }

        /// <summary>Filter by priority.</summary>
        public CasePriority? Priority { get; set; }

        /// <summary>Filter by assigned reviewer.</summary>
        public string? AssignedReviewerId { get; set; }

        /// <summary>Filter by issuer.</summary>
        public string? IssuerId { get; set; }

        /// <summary>Filter by jurisdiction.</summary>
        public string? Jurisdiction { get; set; }

        /// <summary>When true, return only cases with stale evidence.</summary>
        public bool? HasStaleEvidence { get; set; }

        /// <summary>Filter by case type.</summary>
        public CaseType? Type { get; set; }

        /// <summary>1-based page number.</summary>
        public int Page { get; set; } = 1;

        /// <summary>Number of results per page (max 100).</summary>
        public int PageSize { get; set; } = 20;
    }

    /// <summary>Paginated list of compliance cases.</summary>
    public class ListComplianceCasesResponse
    {
        /// <summary>True when the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Matched cases for the current page.</summary>
        public List<ComplianceCase> Cases { get; set; } = new();

        /// <summary>Total number of cases matching the filter.</summary>
        public int TotalCount { get; set; }

        /// <summary>Current page number.</summary>
        public int Page { get; set; }

        /// <summary>Page size used for this response.</summary>
        public int PageSize { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response returned when retrieving a single compliance case.</summary>
    public class GetComplianceCaseResponse
    {
        /// <summary>True when the case was found and returned.</summary>
        public bool Success { get; set; }

        /// <summary>The retrieved case.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Request to add evidence to a compliance case.</summary>
    public class AddEvidenceRequest
    {
        /// <summary>Category of evidence being added (e.g., "KYC", "AML").</summary>
        public string EvidenceType { get; set; } = string.Empty;

        /// <summary>Initial status of this evidence.</summary>
        public CaseEvidenceStatus Status { get; set; } = CaseEvidenceStatus.Valid;

        /// <summary>Name of the provider that supplied this evidence.</summary>
        public string? ProviderName { get; set; }

        /// <summary>Provider-side reference ID.</summary>
        public string? ProviderReference { get; set; }

        /// <summary>When the evidence was captured by the provider.</summary>
        public DateTimeOffset? CapturedAt { get; set; }

        /// <summary>When this evidence expires.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>Human-readable summary of the evidence result.</summary>
        public string? Summary { get; set; }

        /// <summary>Normalized key-value attributes.</summary>
        public Dictionary<string, string>? NormalizedAttributes { get; set; }

        /// <summary>True when this evidence is blocking case readiness.</summary>
        public bool IsBlockingReadiness { get; set; }

        /// <summary>Explanation of why this evidence is blocking readiness.</summary>
        public string? BlockingReason { get; set; }
    }

    /// <summary>Request to add a remediation task to a compliance case.</summary>
    public class AddRemediationTaskRequest
    {
        /// <summary>Short title of the action required.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed description of what needs to be done.</summary>
        public string? Description { get; set; }

        /// <summary>User ID of the person responsible.</summary>
        public string? OwnerId { get; set; }

        /// <summary>Deadline for completing this task.</summary>
        public DateTimeOffset? DueAt { get; set; }

        /// <summary>True when this task must be resolved before the case can be approved.</summary>
        public bool IsBlockingCase { get; set; }

        /// <summary>Severity level of this blocker.</summary>
        public EvidenceIssueSeverityLevel BlockerSeverity { get; set; } = EvidenceIssueSeverityLevel.Medium;
    }

    /// <summary>Request to resolve or dismiss a remediation task.</summary>
    public class ResolveRemediationTaskRequest
    {
        /// <summary>Resolution status (Resolved or Dismissed).</summary>
        public RemediationTaskStatus Status { get; set; } = RemediationTaskStatus.Resolved;

        /// <summary>Notes about how the task was resolved.</summary>
        public string? ResolutionNotes { get; set; }
    }

    /// <summary>Request to raise an escalation on a compliance case.</summary>
    public class AddEscalationRequest
    {
        /// <summary>Type of escalation.</summary>
        public EscalationType Type { get; set; }

        /// <summary>Human-readable description of the escalation reason.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Name of the screening provider that surfaced this hit.</summary>
        public string? ScreeningSource { get; set; }

        /// <summary>Categories matched by the screening hit.</summary>
        public List<string>? MatchedCategories { get; set; }

        /// <summary>Provider-reported confidence score (0.0–1.0).</summary>
        public double? ConfidenceScore { get; set; }

        /// <summary>True when this escalation requires manual compliance review.</summary>
        public bool RequiresManualReview { get; set; } = true;
    }

    /// <summary>Request to resolve an escalation on a compliance case.</summary>
    public class ResolveEscalationRequest
    {
        /// <summary>Notes about how the escalation was resolved.</summary>
        public string? ResolutionNotes { get; set; }
    }

    /// <summary>Chronological timeline of events for a compliance case.</summary>
    public class CaseTimelineResponse
    {
        /// <summary>True when the timeline was retrieved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Case identifier.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Timeline entries in chronological order.</summary>
        public List<CaseTimelineEntry> Entries { get; set; } = new();

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response wrapping a <see cref="CaseReadinessSummary"/>.</summary>
    public class CaseReadinessSummaryResponse
    {
        /// <summary>True when the readiness summary was evaluated successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The evaluated readiness summary.</summary>
        public CaseReadinessSummary? Summary { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ── Monitoring aggregates ────────────────────────────────────────────────────

    /// <summary>
    /// Monitoring schedule attached to a case, defining when periodic reviews should occur.
    /// May be applied to cases in any state to enroll the subject in a periodic review program.
    /// Typically configured after a case reaches <see cref="ComplianceCaseState.Approved"/> for
    /// post-onboarding ongoing monitoring.
    /// </summary>
    public class MonitoringSchedule
    {
        /// <summary>Unique identifier for this schedule.</summary>
        public string ScheduleId { get; set; } = string.Empty;

        /// <summary>Case this schedule belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Frequency preset controlling how often reviews occur.</summary>
        public MonitoringFrequency Frequency { get; set; } = MonitoringFrequency.Annual;

        /// <summary>
        /// Custom review interval in days.
        /// Populated when <see cref="Frequency"/> is <see cref="MonitoringFrequency.Custom"/>.
        /// </summary>
        public int IntervalDays { get; set; }

        /// <summary>Timestamp of the most recent monitoring review (null if none yet).</summary>
        public DateTimeOffset? LastReviewAt { get; set; }

        /// <summary>Timestamp when the next periodic review is due.</summary>
        public DateTimeOffset NextReviewDueAt { get; set; }

        /// <summary>True when the next review date has passed and no review has been recorded.</summary>
        public bool IsOverdue { get; set; }

        /// <summary>Actor who configured this schedule.</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>When the schedule was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Optional notes about the monitoring rationale or scope.</summary>
        public string? Notes { get; set; }

        /// <summary>True when this schedule is still active.</summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// A recorded outcome of a periodic monitoring review performed against a case.
    /// Each review creates an immutable record and may generate a follow-up case.
    /// </summary>
    public class MonitoringReview
    {
        /// <summary>Unique identifier for this review.</summary>
        public string ReviewId { get; set; } = string.Empty;

        /// <summary>Case this review belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Outcome of the review.</summary>
        public MonitoringReviewOutcome Outcome { get; set; }

        /// <summary>Structured notes captured by the reviewer.</summary>
        public string ReviewNotes { get; set; } = string.Empty;

        /// <summary>True when a new follow-up compliance case was created as a result of this review.</summary>
        public bool FollowUpCaseCreated { get; set; }

        /// <summary>Case ID of any follow-up case created.</summary>
        public string? FollowUpCaseId { get; set; }

        /// <summary>Actor who performed this review.</summary>
        public string ReviewedBy { get; set; } = string.Empty;

        /// <summary>When the review was recorded.</summary>
        public DateTimeOffset ReviewedAt { get; set; }

        /// <summary>Structured attributes captured during the review (e.g., re-screening results).</summary>
        public Dictionary<string, string> Attributes { get; set; } = new();
    }

    // ── Monitoring request / response types ─────────────────────────────────────

    /// <summary>Request to configure a monitoring schedule for a compliance case.</summary>
    public class SetMonitoringScheduleRequest
    {
        /// <summary>Frequency at which the case should be reviewed.</summary>
        public MonitoringFrequency Frequency { get; set; } = MonitoringFrequency.Annual;

        /// <summary>
        /// Custom review interval in days. Required when <see cref="Frequency"/> is
        /// <see cref="MonitoringFrequency.Custom"/>; ignored otherwise.
        /// </summary>
        public int? CustomIntervalDays { get; set; }

        /// <summary>Optional notes about the monitoring scope or trigger.</summary>
        public string? Notes { get; set; }
    }

    /// <summary>Response returned after setting or updating a monitoring schedule.</summary>
    public class SetMonitoringScheduleResponse
    {
        /// <summary>True when the schedule was saved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The persisted schedule.</summary>
        public MonitoringSchedule? Schedule { get; set; }

        /// <summary>The case after the schedule was applied.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Request to record the outcome of a periodic monitoring review.</summary>
    public class RecordMonitoringReviewRequest
    {
        /// <summary>Outcome of the review.</summary>
        public MonitoringReviewOutcome Outcome { get; set; } = MonitoringReviewOutcome.Clear;

        /// <summary>Structured review notes (required).</summary>
        public string ReviewNotes { get; set; } = string.Empty;

        /// <summary>
        /// When true and <see cref="Outcome"/> is <see cref="MonitoringReviewOutcome.EscalationRequired"/>,
        /// a new follow-up <see cref="CaseType.OngoingMonitoring"/> case is automatically created.
        /// </summary>
        public bool CreateFollowUpCase { get; set; }

        /// <summary>Optional structured attributes (e.g., re-screening result keys).</summary>
        public Dictionary<string, string>? Attributes { get; set; }
    }

    /// <summary>Response returned after recording a monitoring review.</summary>
    public class RecordMonitoringReviewResponse
    {
        /// <summary>True when the review was recorded successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The recorded review.</summary>
        public MonitoringReview? Review { get; set; }

        /// <summary>The case after the review was applied (schedule updated).</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>Follow-up case if one was created.</summary>
        public ComplianceCase? FollowUpCase { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response from triggering a periodic review check across all monitored cases.</summary>
    public class TriggerPeriodicReviewCheckResponse
    {
        /// <summary>True when the check completed without errors.</summary>
        public bool Success { get; set; }

        /// <summary>Total number of cases inspected.</summary>
        public int CasesInspected { get; set; }

        /// <summary>Number of cases whose monitoring review is overdue.</summary>
        public int OverdueCasesFound { get; set; }

        /// <summary>IDs of cases now marked as overdue.</summary>
        public List<string> OverdueCaseIds { get; set; } = new();

        /// <summary>When the check was run.</summary>
        public DateTimeOffset CheckedAt { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Metadata describing how and when a case evidence bundle was exported.</summary>
    public class CaseExportMetadata
    {
        /// <summary>Unique identifier for this export operation.</summary>
        public string ExportId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>When this export was generated (UTC).</summary>
        public DateTimeOffset ExportedAt { get; set; }

        /// <summary>Identity of the actor who requested the export.</summary>
        public string ExportedBy { get; set; } = string.Empty;

        /// <summary>Export format: "JSON" (default).</summary>
        public string Format { get; set; } = "JSON";

        /// <summary>Schema version for forward-compatibility signalling.</summary>
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>SHA-256 hex digest of the serialised <see cref="ComplianceCaseEvidenceBundle.CaseSnapshot"/> payload.</summary>
        public string ContentHash { get; set; } = string.Empty;
    }

    /// <summary>
    /// Regulator/audit-ready evidence bundle for a single compliance case.
    /// Contains a point-in-time snapshot of the case, its full timeline, and export metadata.
    /// </summary>
    public class ComplianceCaseEvidenceBundle
    {
        /// <summary>ID of the exported case.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Full case snapshot at export time.</summary>
        public ComplianceCase? CaseSnapshot { get; set; }

        /// <summary>Chronological audit trail entries.</summary>
        public List<CaseTimelineEntry> Timeline { get; set; } = new();

        /// <summary>Export metadata (id, timestamp, actor, hash).</summary>
        public CaseExportMetadata Metadata { get; set; } = new();
    }

    /// <summary>Request to export a compliance case evidence bundle.</summary>
    public class ExportComplianceCaseRequest
    {
        /// <summary>Optional: actor requesting the export (for audit logging).</summary>
        public string? RequestedBy { get; set; }

        /// <summary>Optional: export format (default "JSON").</summary>
        public string Format { get; set; } = "JSON";
    }

    /// <summary>Response from a compliance case export operation.</summary>
    public class ExportComplianceCaseResponse
    {
        /// <summary>True when the export was generated successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The structured evidence bundle (populated on success).</summary>
        public ComplianceCaseEvidenceBundle? Bundle { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ── Assignment request / response types ──────────────────────────────────────

    /// <summary>
    /// Request to assign or reassign a compliance case to a specific reviewer and/or team.
    /// Captures structured history including the reason for transfer.
    /// </summary>
    public class AssignCaseRequest
    {
        /// <summary>User ID of the reviewer to assign (null to clear reviewer assignment).</summary>
        public string? ReviewerId { get; set; }

        /// <summary>Team ID to assign to this case (null to clear team assignment).</summary>
        public string? TeamId { get; set; }

        /// <summary>Human-readable reason for this assignment or reassignment.</summary>
        public string? Reason { get; set; }
    }

    /// <summary>Response returned after a case assignment operation.</summary>
    public class AssignCaseResponse
    {
        /// <summary>True when the assignment was applied successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The updated case.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>The assignment record that was created.</summary>
        public CaseAssignmentRecord? AssignmentRecord { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response containing the full assignment history for a case.</summary>
    public class GetAssignmentHistoryResponse
    {
        /// <summary>True when the history was retrieved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Case identifier.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Assignment records in chronological order (oldest first).</summary>
        public List<CaseAssignmentRecord> History { get; set; } = new();

        /// <summary>Total number of assignment events recorded.</summary>
        public int TotalCount { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ── SLA request / response types ─────────────────────────────────────────────

    /// <summary>Request to configure SLA metadata for a compliance case.</summary>
    public class SetSlaMetadataRequest
    {
        /// <summary>When the case review must be completed.</summary>
        public DateTimeOffset? ReviewDueAt { get; set; }

        /// <summary>When an escalation is required if the case has not been resolved.</summary>
        public DateTimeOffset? EscalationDueAt { get; set; }

        /// <summary>Optional notes explaining the SLA rationale.</summary>
        public string? Notes { get; set; }
    }

    /// <summary>Response returned after setting SLA metadata on a case.</summary>
    public class SetSlaMetadataResponse
    {
        /// <summary>True when the SLA metadata was saved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The persisted SLA metadata.</summary>
        public CaseSlaMetadata? SlaMetadata { get; set; }

        /// <summary>The updated case.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ── Escalation history response ───────────────────────────────────────────────

    /// <summary>Response containing the structured escalation history for a case.</summary>
    public class GetEscalationHistoryResponse
    {
        /// <summary>True when the history was retrieved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Case identifier.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>All escalations raised against this case in chronological order.</summary>
        public List<CaseEscalation> Escalations { get; set; } = new();

        /// <summary>Count of currently open escalations.</summary>
        public int OpenCount { get; set; }

        /// <summary>Count of resolved escalations.</summary>
        public int ResolvedCount { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ── Delivery status response ──────────────────────────────────────────────────

    /// <summary>
    /// Response containing the persisted webhook delivery records for a case.
    /// Provides operational visibility into delivery outcomes, retry state, and failure details.
    /// </summary>
    public class GetDeliveryStatusResponse
    {
        /// <summary>True when the delivery status was retrieved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Case identifier.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Delivery records in reverse-chronological order (newest first).</summary>
        public List<CaseDeliveryRecord> Records { get; set; } = new();

        /// <summary>Total number of delivery records for this case.</summary>
        public int TotalCount { get; set; }

        /// <summary>Number of records with a successful outcome.</summary>
        public int SuccessCount { get; set; }

        /// <summary>Number of records with a failed or exhausted outcome.</summary>
        public int FailureCount { get; set; }

        /// <summary>Number of records currently in a retry-scheduled state.</summary>
        public int PendingRetryCount { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }
}
