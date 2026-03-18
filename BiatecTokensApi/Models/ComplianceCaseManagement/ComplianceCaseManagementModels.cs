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
        MonitoringFollowUpCreated,
        /// <summary>A KYC, AML, sanctions, or approval decision was recorded against the case.</summary>
        DecisionRecorded,
        /// <summary>The downstream handoff status was updated.</summary>
        HandoffStatusChanged,
        /// <summary>An explicit approval decision was recorded via the approve endpoint.</summary>
        ApprovalDecisionRecorded,
        /// <summary>An explicit rejection decision was recorded via the reject endpoint.</summary>
        RejectionDecisionRecorded,
        /// <summary>The case was returned to an earlier stage requesting additional information.</summary>
        ReturnedForInformation
    }

    // ── Blocker taxonomy ───────────────────────────────────────────────────────

    /// <summary>Structured category classifying the root cause of a compliance case blocker.</summary>
    public enum CaseBlockerCategory
    {
        /// <summary>Evidence attached to the case has passed its expiry date.</summary>
        StaleEvidence,
        /// <summary>Required evidence is absent from the case.</summary>
        MissingEvidence,
        /// <summary>An open sanctions screening hit requires a manual analyst decision.</summary>
        UnresolvedSanctions,
        /// <summary>KYC review has not yet been completed or returned a non-clear outcome.</summary>
        PendingKycDecision,
        /// <summary>AML screening has not yet been completed or returned a non-clear outcome.</summary>
        PendingAmlDecision,
        /// <summary>An approval workflow stage is pending; the case cannot proceed until approved.</summary>
        IncompleteApproval,
        /// <summary>A downstream delivery or distribution step has failed and requires intervention.</summary>
        DownstreamDeliveryFailure,
        /// <summary>One or more open remediation tasks are blocking case progression.</summary>
        OpenRemediationTask,
        /// <summary>An active escalation requiring manual resolution is blocking the case.</summary>
        OpenEscalation,
        /// <summary>No reviewer or team has been assigned to the case.</summary>
        MissingAssignment,
        /// <summary>The SLA review deadline has passed without resolution.</summary>
        SlaBreached,
        /// <summary>An operator has explicitly blocked the case pending manual intervention.</summary>
        ManualBlock
    }

    // ── Decision lineage ───────────────────────────────────────────────────────

    /// <summary>Kind of decision tracked in the case decision history.</summary>
    public enum CaseDecisionKind
    {
        /// <summary>KYC identity verification approved.</summary>
        KycApproval,
        /// <summary>KYC identity verification rejected.</summary>
        KycRejection,
        /// <summary>AML screening returned a clear result.</summary>
        AmlClear,
        /// <summary>AML screening surfaced a potential or confirmed hit.</summary>
        AmlHit,
        /// <summary>Sanctions list screening review outcome recorded.</summary>
        SanctionsReview,
        /// <summary>An approval workflow stage outcome was recorded.</summary>
        ApprovalWorkflowOutcome,
        /// <summary>A manual compliance review decision was recorded.</summary>
        ManualReviewDecision,
        /// <summary>An escalation review decision was recorded.</summary>
        EscalationDecision,
        /// <summary>A formal case approval decision recorded via the approve endpoint.</summary>
        ApprovalDecision,
        /// <summary>A formal case rejection decision recorded via the reject endpoint.</summary>
        RejectionDecision
    }

    // ── Handoff stage ──────────────────────────────────────────────────────────

    /// <summary>Stage of the downstream handoff workflow for a compliance case.</summary>
    public enum CaseHandoffStage
    {
        /// <summary>No handoff process has been initiated yet.</summary>
        NotStarted,
        /// <summary>Waiting for an approval workflow to complete before handoff.</summary>
        ApprovalWorkflowPending,
        /// <summary>Waiting for the regulatory evidence package to be prepared.</summary>
        RegulatoryPackagePending,
        /// <summary>Waiting for the distribution step to complete.</summary>
        DistributionPending,
        /// <summary>All downstream handoff steps have completed successfully.</summary>
        Completed,
        /// <summary>A downstream handoff step has failed and requires intervention.</summary>
        Failed
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
    /// A structured, typed blocker representing a specific reason why a compliance case
    /// cannot proceed. Provides plain-language titles, descriptions, remediation hints,
    /// and fail-closed severity so the frontend can render enterprise-grade operator copy
    /// without inferring meaning from low-level flags.
    /// </summary>
    public class CaseBlocker
    {
        /// <summary>Unique identifier for this blocker instance.</summary>
        public string BlockerId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>Structural category identifying the root cause of this blocker.</summary>
        public CaseBlockerCategory Category { get; set; }

        /// <summary>Severity of this blocker.</summary>
        public EvidenceIssueSeverityLevel Severity { get; set; } = EvidenceIssueSeverityLevel.High;

        /// <summary>Short, plain-language title suitable for worklist display.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Full explanation of why this blocker exists.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Operator-facing hint describing how to resolve or unblock this issue.
        /// Null when no specific remediation is known.
        /// </summary>
        public string? RemediationHint { get; set; }

        /// <summary>
        /// ID of the entity causing this block (e.g., evidence ID, escalation ID, task ID).
        /// Null for high-level state blockers.
        /// </summary>
        public string? LinkedEntityId { get; set; }

        /// <summary>When this blocker was detected.</summary>
        public DateTimeOffset DetectedAt { get; set; }

        /// <summary>True when this blocker causes a fail-closed state (vs. an advisory warning).</summary>
        public bool IsFailClosed { get; set; } = true;
    }

    /// <summary>
    /// An immutable record of a KYC, AML, sanctions, or approval workflow decision
    /// linked to a compliance case. Provides structured decision lineage for audit
    /// and for computing case readiness.
    /// </summary>
    public class CaseDecisionRecord
    {
        /// <summary>Unique identifier for this decision record.</summary>
        public string DecisionId { get; set; } = string.Empty;

        /// <summary>Case this decision belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Kind of decision being recorded.</summary>
        public CaseDecisionKind Kind { get; set; }

        /// <summary>Short human-readable summary of the decision outcome.</summary>
        public string DecisionSummary { get; set; } = string.Empty;

        /// <summary>
        /// Normalised outcome string: "Approved", "Rejected", "Clear", "Hit",
        /// "Pending", "Inconclusive", etc.
        /// </summary>
        public string? Outcome { get; set; }

        /// <summary>External provider or system that produced this decision (null for manual).</summary>
        public string? ProviderName { get; set; }

        /// <summary>Provider-side reference ID for this decision (null if manual).</summary>
        public string? ProviderReference { get; set; }

        /// <summary>Plain-language explanation of why this outcome was reached.</summary>
        public string? Explanation { get; set; }

        /// <summary>True when this decision is considered adverse (blocks or warns on the case).</summary>
        public bool IsAdverse { get; set; }

        /// <summary>Actor who recorded this decision.</summary>
        public string DecidedBy { get; set; } = string.Empty;

        /// <summary>When this decision was recorded.</summary>
        public DateTimeOffset DecidedAt { get; set; }

        /// <summary>Structured attributes from the decision source (e.g., match scores).</summary>
        public Dictionary<string, string> Attributes { get; set; } = new();
    }

    /// <summary>
    /// Downstream handoff status tracking whether a compliance case has fulfilled all
    /// post-approval obligations (regulatory package, approval routing, distribution).
    /// Updated explicitly via the handoff API; fail-closed until marked completed.
    /// </summary>
    public class CaseHandoffStatus
    {
        /// <summary>Case this handoff status belongs to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Current stage of the downstream handoff workflow.</summary>
        public CaseHandoffStage Stage { get; set; } = CaseHandoffStage.NotStarted;

        /// <summary>
        /// True when all downstream handoff obligations are fulfilled and the case
        /// can be considered operationally complete.
        /// </summary>
        public bool IsHandoffReady { get; set; }

        /// <summary>Plain-language reason the handoff is not yet ready (null when ready).</summary>
        public string? BlockingReason { get; set; }

        /// <summary>Identifiers of unresolved downstream dependencies (e.g., approvalId, reportId).</summary>
        public List<string> UnresolvedDependencies { get; set; } = new();

        /// <summary>When the handoff must be completed (null if no deadline).</summary>
        public DateTimeOffset? HandoffDueAt { get; set; }

        /// <summary>When all handoff steps were completed (null if incomplete).</summary>
        public DateTimeOffset? HandoffCompletedAt { get; set; }

        /// <summary>Free-text notes about this handoff stage.</summary>
        public string? HandoffNotes { get; set; }

        /// <summary>Actor who last updated the handoff status.</summary>
        public string? UpdatedBy { get; set; }

        /// <summary>When the handoff status was last updated.</summary>
        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>
    /// Lightweight, scannable summary of a compliance case.
    /// Designed for worklist rendering, operations cockpit cards, and role-friendly display.
    /// Does not include full evidence payloads, timeline, or detailed remediation notes.
    /// </summary>
    public class CaseSummary
    {
        /// <summary>Unique case identifier.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>Issuer scoping this case.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Identifier of the subject being evaluated.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Classification of this case.</summary>
        public CaseType Type { get; set; }

        /// <summary>Current lifecycle state.</summary>
        public ComplianceCaseState State { get; set; }

        /// <summary>Priority assigned to this case.</summary>
        public CasePriority Priority { get; set; }

        /// <summary>User ID of the reviewer currently assigned (null if unassigned).</summary>
        public string? AssignedReviewerId { get; set; }

        /// <summary>Team ID currently assigned (null if unassigned).</summary>
        public string? AssignedTeamId { get; set; }

        /// <summary>SLA urgency classification (derived from SLA metadata or Normal if no SLA set).</summary>
        public CaseUrgencyBand UrgencyBand { get; set; }

        /// <summary>When the case was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>When the case was last modified.</summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Total number of active (fail-closed) blockers on this case.</summary>
        public int BlockerCount { get; set; }

        /// <summary>Number of open remediation tasks.</summary>
        public int OpenRemediationTasks { get; set; }

        /// <summary>Number of open escalations.</summary>
        public int OpenEscalations { get; set; }

        /// <summary>True when any evidence on the case has become stale.</summary>
        public bool HasStaleEvidence { get; set; }

        /// <summary>True when downstream handoff obligations are fulfilled (or no handoff is required).</summary>
        public bool IsHandoffReady { get; set; }

        /// <summary>Title of the highest-severity active blocker (null if no blockers).</summary>
        public string? TopBlockerTitle { get; set; }

        /// <summary>Plain-language description of the next required action for an operator.</summary>
        public string? NextActionDescription { get; set; }

        /// <summary>Jurisdiction code relevant to this case.</summary>
        public string? Jurisdiction { get; set; }

        /// <summary>Number of decision records linked to this case.</summary>
        public int DecisionCount { get; set; }

        /// <summary>Current handoff stage (NotStarted when no handoff has been initiated).</summary>
        public CaseHandoffStage HandoffStage { get; set; } = CaseHandoffStage.NotStarted;
    }

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

        /// <summary>
        /// Structured, typed blockers currently active on this case.
        /// Computed on-demand and stored here for snapshot visibility.
        /// Fail-closed blockers prevent case readiness.
        /// </summary>
        public List<CaseBlocker> Blockers { get; set; } = new();

        /// <summary>
        /// Chronological history of KYC, AML, sanctions, and approval decisions
        /// explicitly linked to this case for decision lineage and audit.
        /// </summary>
        public List<CaseDecisionRecord> DecisionHistory { get; set; } = new();

        /// <summary>
        /// Downstream handoff status tracking post-approval obligations.
        /// Null until the first handoff status update is recorded.
        /// </summary>
        public CaseHandoffStatus? HandoffStatus { get; set; }
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

    // ── Case summary request / response types ─────────────────────────────────

    /// <summary>Response wrapping a lightweight <see cref="CaseSummary"/>.</summary>
    public class CaseSummaryResponse
    {
        /// <summary>True when the summary was retrieved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The lightweight case summary.</summary>
        public CaseSummary? Summary { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Paginated list of lightweight compliance case summaries.</summary>
    public class ListCaseSummariesResponse
    {
        /// <summary>True when the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Matched case summaries for the current page.</summary>
        public List<CaseSummary> Summaries { get; set; } = new();

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

    // ── Blockers response ─────────────────────────────────────────────────────

    /// <summary>Response containing the evaluated structured blockers for a compliance case.</summary>
    public class EvaluateBlockersResponse
    {
        /// <summary>True when the blocker evaluation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Case identifier.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>All active blockers evaluated for this case.</summary>
        public List<CaseBlocker> Blockers { get; set; } = new();

        /// <summary>Fail-closed blockers (subset of Blockers where IsFailClosed = true).</summary>
        public List<CaseBlocker> FailClosedBlockers { get; set; } = new();

        /// <summary>Advisory warnings (subset of Blockers where IsFailClosed = false).</summary>
        public List<CaseBlocker> Warnings { get; set; } = new();

        /// <summary>True when there are no fail-closed blockers.</summary>
        public bool CanProceed { get; set; }

        /// <summary>When this evaluation was computed.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ── Decision record request / response types ──────────────────────────────

    /// <summary>Request to add a KYC, AML, sanctions, or approval decision record to a compliance case.</summary>
    public class AddDecisionRecordRequest
    {
        /// <summary>Kind of decision being recorded.</summary>
        public CaseDecisionKind Kind { get; set; }

        /// <summary>Short human-readable summary of the decision outcome.</summary>
        public string DecisionSummary { get; set; } = string.Empty;

        /// <summary>Normalised outcome string (e.g., "Approved", "Rejected", "Clear", "Hit").</summary>
        public string? Outcome { get; set; }

        /// <summary>External provider or system that produced this decision.</summary>
        public string? ProviderName { get; set; }

        /// <summary>Provider-side reference ID for this decision.</summary>
        public string? ProviderReference { get; set; }

        /// <summary>Plain-language explanation of why this outcome was reached.</summary>
        public string? Explanation { get; set; }

        /// <summary>True when this decision is considered adverse (blocks or warns on the case).</summary>
        public bool IsAdverse { get; set; }

        /// <summary>Structured attributes from the decision source (e.g., match scores).</summary>
        public Dictionary<string, string>? Attributes { get; set; }
    }

    /// <summary>Response returned after adding a decision record to a compliance case.</summary>
    public class AddDecisionRecordResponse
    {
        /// <summary>True when the decision record was saved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The decision record that was created.</summary>
        public CaseDecisionRecord? DecisionRecord { get; set; }

        /// <summary>The updated case (after appending the decision).</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response containing the full decision history for a compliance case.</summary>
    public class GetDecisionHistoryResponse
    {
        /// <summary>True when the history was retrieved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Case identifier.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>All decision records in chronological order (oldest first).</summary>
        public List<CaseDecisionRecord> Decisions { get; set; } = new();

        /// <summary>Total number of decisions recorded against this case.</summary>
        public int TotalCount { get; set; }

        /// <summary>Number of adverse decisions in the history.</summary>
        public int AdverseCount { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ── Handoff status request / response types ───────────────────────────────

    /// <summary>Request to update the downstream handoff status for a compliance case.</summary>
    public class UpdateHandoffStatusRequest
    {
        /// <summary>The new handoff stage.</summary>
        public CaseHandoffStage Stage { get; set; }

        /// <summary>Plain-language reason the handoff is not ready (required unless Stage is Completed).</summary>
        public string? BlockingReason { get; set; }

        /// <summary>IDs of unresolved downstream dependencies (e.g., approvalId, reportId).</summary>
        public List<string>? UnresolvedDependencies { get; set; }

        /// <summary>When the handoff must be completed.</summary>
        public DateTimeOffset? HandoffDueAt { get; set; }

        /// <summary>Free-text notes about this handoff stage.</summary>
        public string? HandoffNotes { get; set; }
    }

    /// <summary>Response returned after updating the handoff status for a compliance case.</summary>
    public class UpdateHandoffStatusResponse
    {
        /// <summary>True when the handoff status was saved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The persisted handoff status.</summary>
        public CaseHandoffStatus? HandoffStatus { get; set; }

        /// <summary>The updated case.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response containing the handoff status for a compliance case.</summary>
    public class GetHandoffStatusResponse
    {
        /// <summary>True when the handoff status was retrieved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The handoff status (null when no handoff has been initiated).</summary>
        public CaseHandoffStatus? HandoffStatus { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ── Approval / rejection / return-for-information request and response types ──

    /// <summary>
    /// Request to formally approve a compliance case.
    /// Transitions the case to <see cref="ComplianceCaseState.Approved"/>, records an
    /// <see cref="CaseDecisionKind.ApprovalDecision"/> audit record, and emits
    /// <see cref="BiatecTokensApi.Models.Webhook.WebhookEventType.ComplianceCaseApprovalGranted"/>.
    /// </summary>
    public class ApproveComplianceCaseRequest
    {
        /// <summary>Plain-language rationale for the approval (strongly recommended for audit trail).</summary>
        public string? Rationale { get; set; }

        /// <summary>Optional structured notes capturing reviewer observations or evidence references.</summary>
        public string? ApprovalNotes { get; set; }

        /// <summary>
        /// Identity of the approver (overrides the HTTP actor when provided, e.g. for system-originated approvals).
        /// When null, the authenticated actor ID is used.
        /// </summary>
        public string? ApprovedBy { get; set; }

        /// <summary>Optional reference to the external approval workflow ticket or document ID.</summary>
        public string? ExternalApprovalReference { get; set; }
    }

    /// <summary>Response returned after a successful approval action.</summary>
    public class ApproveComplianceCaseResponse
    {
        /// <summary>True when the case was approved successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The updated case snapshot after approval.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>ID of the approval decision record created for audit purposes.</summary>
        public string? DecisionId { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request to formally reject a compliance case.
    /// Transitions the case to <see cref="ComplianceCaseState.Rejected"/>, records a
    /// <see cref="CaseDecisionKind.RejectionDecision"/> audit record, and emits
    /// <see cref="BiatecTokensApi.Models.Webhook.WebhookEventType.ComplianceCaseApprovalDenied"/>.
    /// </summary>
    public class RejectComplianceCaseRequest
    {
        /// <summary>Required plain-language reason for the rejection.</summary>
        public string? Reason { get; set; }

        /// <summary>Optional structured notes capturing reviewer observations, adverse findings, or evidence references.</summary>
        public string? RejectionNotes { get; set; }

        /// <summary>
        /// Identity of the rejecting reviewer (overrides the HTTP actor when provided).
        /// When null, the authenticated actor ID is used.
        /// </summary>
        public string? RejectedBy { get; set; }

        /// <summary>Optional reference to the external document or workflow ticket that triggered the rejection.</summary>
        public string? ExternalRejectionReference { get; set; }
    }

    /// <summary>Response returned after a successful rejection action.</summary>
    public class RejectComplianceCaseResponse
    {
        /// <summary>True when the case was rejected successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The updated case snapshot after rejection.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>ID of the rejection decision record created for audit purposes.</summary>
        public string? DecisionId { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Stage to which a case is returned when the reviewer requests additional information.
    /// </summary>
    public enum ReturnForInformationTargetStage
    {
        /// <summary>
        /// Return to <see cref="ComplianceCaseState.EvidencePending"/> — reviewer needs additional evidence
        /// documents before the review can continue.
        /// </summary>
        EvidencePending,

        /// <summary>
        /// Return to <see cref="ComplianceCaseState.Remediating"/> — reviewer has identified corrections
        /// or outstanding remediation tasks that must be completed before re-evaluation.
        /// </summary>
        Remediating
    }

    /// <summary>
    /// Request to return a compliance case to an earlier lifecycle stage.
    /// Transitions the case to <see cref="ComplianceCaseState.EvidencePending"/> or
    /// <see cref="ComplianceCaseState.Remediating"/> and emits
    /// <see cref="BiatecTokensApi.Models.Webhook.WebhookEventType.ComplianceCaseReturnedForInformation"/>.
    /// </summary>
    public class ReturnForInformationRequest
    {
        /// <summary>
        /// The stage to which the case should be returned.
        /// Defaults to <see cref="ReturnForInformationTargetStage.EvidencePending"/>.
        /// </summary>
        public ReturnForInformationTargetStage TargetStage { get; set; } = ReturnForInformationTargetStage.EvidencePending;

        /// <summary>Required plain-language reason explaining what information or correction is needed.</summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Structured list of the specific items, documents, or evidence fields that must be provided
        /// before the review can continue (e.g., "Valid passport copy", "Updated proof of address").
        /// </summary>
        public List<string>? RequestedItems { get; set; }

        /// <summary>Optional notes providing further context to the subject or compliance team.</summary>
        public string? AdditionalNotes { get; set; }
    }

    /// <summary>Response returned after a successful return-for-information action.</summary>
    public class ReturnForInformationResponse
    {
        /// <summary>True when the case was returned successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The updated case snapshot after the return.</summary>
        public ComplianceCase? Case { get; set; }

        /// <summary>The stage the case was transitioned to.</summary>
        public ComplianceCaseState? ReturnedToStage { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }
    }
}
