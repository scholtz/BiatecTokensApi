using System.ComponentModel.DataAnnotations;
using BiatecTokensApi.Models.IssuerWorkflow;

namespace BiatecTokensApi.Models.EnterpriseComplianceReview
{
    // ── Role-Capability Enumerations ──────────────────────────────────────────

    /// <summary>
    /// Actions available to a team member based on their role for a given review item.
    /// Used by the frontend to determine which buttons/actions to expose.
    /// </summary>
    public enum ReviewCapability
    {
        /// <summary>Actor can view this item.</summary>
        View = 0,

        /// <summary>Actor can submit this item for review.</summary>
        Submit = 1,

        /// <summary>Actor can approve this item.</summary>
        Approve = 2,

        /// <summary>Actor can reject this item.</summary>
        Reject = 3,

        /// <summary>Actor can request changes on this item.</summary>
        RequestChanges = 4,

        /// <summary>Actor can resubmit this item after changes.</summary>
        Resubmit = 5,

        /// <summary>Actor can reassign this item.</summary>
        Reassign = 6,

        /// <summary>Actor can complete this item.</summary>
        Complete = 7
    }

    /// <summary>
    /// Severity level for evidence contradictions, warnings, or missing evidence indicators.
    /// </summary>
    public enum EvidenceIssueSeverity
    {
        /// <summary>Informational only; does not block approval.</summary>
        Info = 0,

        /// <summary>A notable inconsistency that reviewers should be aware of.</summary>
        Warning = 1,

        /// <summary>A contradiction or critical gap that should be resolved before approval.</summary>
        Critical = 2
    }

    /// <summary>
    /// Validation status of a compliance evidence item.
    /// </summary>
    public enum ReviewEvidenceValidationStatus
    {
        /// <summary>Evidence has been validated and is trusted.</summary>
        Valid = 0,

        /// <summary>Evidence is awaiting validation.</summary>
        Pending = 1,

        /// <summary>Evidence failed validation checks.</summary>
        Invalid = 2,

        /// <summary>Evidence is stale and may no longer reflect current state.</summary>
        Stale = 3,

        /// <summary>Evidence source is unavailable; review is incomplete.</summary>
        Unavailable = 4
    }

    /// <summary>
    /// Category of compliance evidence item.
    /// </summary>
    public enum ReviewEvidenceCategory
    {
        /// <summary>Identity verification (KYC/AML) evidence.</summary>
        Identity = 0,

        /// <summary>Policy rule evaluation evidence.</summary>
        Policy = 1,

        /// <summary>Jurisdiction compliance evidence.</summary>
        Jurisdiction = 2,

        /// <summary>Workflow prerequisite evidence.</summary>
        Workflow = 3,

        /// <summary>Subscription or entitlement evidence.</summary>
        Entitlement = 4,

        /// <summary>Audit trail or historical record evidence.</summary>
        AuditTrail = 5,

        /// <summary>Integration or infrastructure health evidence.</summary>
        Integration = 6
    }

    // ── Evidence Models ───────────────────────────────────────────────────────

    /// <summary>
    /// A single piece of compliance evidence supporting a review decision.
    /// Structured for both human review and machine-readable audit export.
    /// </summary>
    public class ReviewEvidenceItem
    {
        /// <summary>Unique identifier for this evidence item.</summary>
        public string EvidenceId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Human-readable title for the evidence item.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Category of the evidence.</summary>
        public ReviewEvidenceCategory Category { get; set; }

        /// <summary>Source system or service that produced this evidence.</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Validation status of the evidence.</summary>
        public ReviewEvidenceValidationStatus ValidationStatus { get; set; }

        /// <summary>Human-readable explanation of what this evidence shows.</summary>
        public string Rationale { get; set; } = string.Empty;

        /// <summary>Optional SHA-256 hash of the evidence payload for integrity verification.</summary>
        public string? DataHash { get; set; }

        /// <summary>When this evidence was collected.</summary>
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Optional structured metadata for export or frontend rendering.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Describes a detected contradiction between two or more evidence items.
    /// Surfaced explicitly so reviewers can investigate before approving.
    /// </summary>
    public class ContradictionDetail
    {
        /// <summary>Unique identifier for this contradiction record.</summary>
        public string ContradictionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Severity of the contradiction.</summary>
        public EvidenceIssueSeverity Severity { get; set; }

        /// <summary>Human-readable description of the contradiction.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Evidence items involved in the contradiction (by EvidenceId).</summary>
        public List<string> InvolvedEvidenceIds { get; set; } = new();

        /// <summary>Recommended action for resolving the contradiction.</summary>
        public string RecommendedAction { get; set; } = string.Empty;

        /// <summary>When this contradiction was detected.</summary>
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Indicates a piece of evidence that is missing or incomplete for a review decision.
    /// Helps reviewers understand what must be collected before approval can proceed.
    /// </summary>
    public class MissingEvidenceIndicator
    {
        /// <summary>Category of the missing evidence.</summary>
        public ReviewEvidenceCategory Category { get; set; }

        /// <summary>Human-readable description of what is missing.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Whether this missing evidence is blocking (approval cannot proceed without it).</summary>
        public bool IsBlocking { get; set; }

        /// <summary>Suggested action to obtain the missing evidence.</summary>
        public string SuggestedAction { get; set; } = string.Empty;
    }

    /// <summary>
    /// A comprehensive bundle of compliance evidence for a review decision.
    /// Includes supporting evidence, detected contradictions, and missing evidence indicators.
    /// Designed for both interactive reviewer use and structured audit export.
    /// </summary>
    public class ReviewEvidenceBundle
    {
        /// <summary>Workflow item this evidence bundle is associated with.</summary>
        public string WorkflowId { get; set; } = string.Empty;

        /// <summary>Issuer scope for this evidence.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>When this bundle was assembled.</summary>
        public DateTime AssembledAt { get; set; } = DateTime.UtcNow;

        /// <summary>Actor who requested the evidence bundle assembly.</summary>
        public string RequestedBy { get; set; } = string.Empty;

        /// <summary>Supporting evidence items.</summary>
        public List<ReviewEvidenceItem> EvidenceItems { get; set; } = new();

        /// <summary>Detected contradictions between evidence items.</summary>
        public List<ContradictionDetail> Contradictions { get; set; } = new();

        /// <summary>Indicators of missing evidence that may be required for approval.</summary>
        public List<MissingEvidenceIndicator> MissingEvidence { get; set; } = new();

        /// <summary>
        /// Whether the evidence bundle is complete enough for a review decision.
        /// False when blocking missing evidence or critical contradictions exist.
        /// </summary>
        public bool IsReviewReady { get; set; }

        /// <summary>
        /// Human-readable summary of the evidence bundle review readiness,
        /// suitable for display in a compliance review interface.
        /// </summary>
        public string ReviewReadinessSummary { get; set; } = string.Empty;

        /// <summary>Overall count of evidence issues by severity.</summary>
        public EvidenceIssueSummary IssueSummary { get; set; } = new();
    }

    /// <summary>
    /// Summary count of evidence issues by severity.
    /// </summary>
    public class EvidenceIssueSummary
    {
        /// <summary>Number of critical issues (blocking).</summary>
        public int CriticalCount { get; set; }

        /// <summary>Number of warnings.</summary>
        public int WarningCount { get; set; }

        /// <summary>Number of informational issues.</summary>
        public int InfoCount { get; set; }

        /// <summary>Total issue count.</summary>
        public int TotalCount => CriticalCount + WarningCount + InfoCount;
    }

    // ── Review Queue Models ───────────────────────────────────────────────────

    /// <summary>
    /// A workflow item enriched with role-capability context and evidence readiness summary,
    /// suitable for display in a role-aware reviewer queue.
    /// </summary>
    public class ReviewQueueItem
    {
        /// <summary>The underlying workflow item.</summary>
        public WorkflowItem WorkflowItem { get; set; } = new();

        /// <summary>Capabilities available to the requesting actor for this item.</summary>
        public List<ReviewCapability> AvailableCapabilities { get; set; } = new();

        /// <summary>Whether review-ready evidence is available for this item.</summary>
        public bool HasEvidenceBundle { get; set; }

        /// <summary>Summary of evidence issues (without loading the full bundle).</summary>
        public EvidenceIssueSummary EvidenceIssueSummary { get; set; } = new();

        /// <summary>
        /// Whether this item is currently blocked from progressing due to missing or
        /// contradictory evidence.
        /// </summary>
        public bool IsEvidenceBlocked { get; set; }

        /// <summary>
        /// Short description of why the item is evidence-blocked, if applicable.
        /// Suitable for tooltip or inline display in the frontend.
        /// </summary>
        public string? EvidenceBlockReason { get; set; }
    }

    /// <summary>
    /// Response containing the role-filtered reviewer queue for an issuer.
    /// </summary>
    public class ReviewQueueResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error code if unsuccessful.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The actor whose queue is being returned.</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>The actor's role in the issuer team.</summary>
        public IssuerTeamRole? ActorRole { get; set; }

        /// <summary>Queue items visible and actionable by this actor.</summary>
        public List<ReviewQueueItem> Items { get; set; } = new();

        /// <summary>Total count of items in the queue.</summary>
        public int TotalCount { get; set; }

        /// <summary>Number of items blocked by missing or contradictory evidence.</summary>
        public int EvidenceBlockedCount { get; set; }

        /// <summary>Number of items immediately actionable (not blocked).</summary>
        public int ActionableCount { get; set; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Response wrapping a compliance evidence bundle.
    /// </summary>
    public class ReviewEvidenceBundleResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error code if unsuccessful.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The assembled evidence bundle.</summary>
        public ReviewEvidenceBundle? Bundle { get; set; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    // ── Decision Models ───────────────────────────────────────────────────────

    /// <summary>
    /// The type of review decision being submitted.
    /// </summary>
    public enum ReviewDecisionType
    {
        /// <summary>Approves the workflow item.</summary>
        Approve = 0,

        /// <summary>Rejects the workflow item with a reason.</summary>
        Reject = 1,

        /// <summary>Returns the item requesting specific changes.</summary>
        RequestChanges = 2
    }

    /// <summary>
    /// Request to submit a compliance review decision with structured rationale.
    /// </summary>
    public class SubmitReviewDecisionRequest
    {
        /// <summary>The type of decision being submitted.</summary>
        [Required]
        public ReviewDecisionType DecisionType { get; set; }

        /// <summary>
        /// Structured rationale for the decision.
        /// Required for Reject and RequestChanges decisions.
        /// </summary>
        [StringLength(2000)]
        public string? Rationale { get; set; }

        /// <summary>
        /// Optional references to specific evidence items that influenced the decision.
        /// </summary>
        public List<string> EvidenceReferences { get; set; } = new();

        /// <summary>
        /// Whether the reviewer acknowledges any open contradictions or warnings
        /// and proceeds anyway (required if contradictions exist and decision is Approve).
        /// </summary>
        public bool AcknowledgesOpenIssues { get; set; }

        /// <summary>Optional note from the reviewer for the audit trail.</summary>
        [StringLength(1000)]
        public string? ReviewNote { get; set; }
    }

    /// <summary>
    /// Result of submitting a review decision.
    /// </summary>
    public class SubmitReviewDecisionResponse
    {
        /// <summary>Whether the decision was recorded successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Error code if unsuccessful.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The updated workflow item after the decision.</summary>
        public WorkflowItem? UpdatedWorkflowItem { get; set; }

        /// <summary>The recorded decision type.</summary>
        public ReviewDecisionType? DecisionType { get; set; }

        /// <summary>Unique ID for the decision record (for audit trail linkage).</summary>
        public string? DecisionId { get; set; }

        /// <summary>When the decision was recorded.</summary>
        public DateTime? DecisionTimestamp { get; set; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    // ── Audit History Models ──────────────────────────────────────────────────

    /// <summary>
    /// An enriched audit entry for a workflow item, including review decision context.
    /// Designed for audit export and compliance reporting.
    /// </summary>
    public class ReviewAuditEntry
    {
        /// <summary>Entry identifier.</summary>
        public string EntryId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Workflow item this entry belongs to.</summary>
        public string WorkflowId { get; set; } = string.Empty;

        /// <summary>State before the action.</summary>
        public WorkflowApprovalState FromState { get; set; }

        /// <summary>State after the action.</summary>
        public WorkflowApprovalState ToState { get; set; }

        /// <summary>Actor who performed the action.</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Display name of the actor (if available).</summary>
        public string? ActorDisplayName { get; set; }

        /// <summary>Role of the actor at the time of the action.</summary>
        public IssuerTeamRole? ActorRole { get; set; }

        /// <summary>Human-readable description of the action taken.</summary>
        public string ActionDescription { get; set; } = string.Empty;

        /// <summary>Rationale or note provided by the actor.</summary>
        public string? Rationale { get; set; }

        /// <summary>Evidence references cited for this decision.</summary>
        public List<string> EvidenceReferences { get; set; } = new();

        /// <summary>When this entry was recorded.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Response containing the full audit history for a workflow item.
    /// </summary>
    public class ReviewAuditHistoryResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error code if unsuccessful.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Workflow item this history belongs to.</summary>
        public string WorkflowId { get; set; } = string.Empty;

        /// <summary>Audit entries in chronological order.</summary>
        public List<ReviewAuditEntry> Entries { get; set; } = new();

        /// <summary>Total entry count.</summary>
        public int TotalCount { get; set; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    // ── Audit Export Models ───────────────────────────────────────────────────

    /// <summary>
    /// Request to export workflow review decisions for an issuer.
    /// Supports structured output suitable for regulators, auditors, or internal compliance review.
    /// </summary>
    public class ReviewAuditExportRequest
    {
        /// <summary>Optional start of the time range (UTC) for the export.</summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>Optional end of the time range (UTC) for the export.</summary>
        public DateTime? ToUtc { get; set; }

        /// <summary>Optional filter by workflow state.</summary>
        public WorkflowApprovalState? StateFilter { get; set; }

        /// <summary>Optional filter by workflow item type.</summary>
        public WorkflowItemType? ItemTypeFilter { get; set; }

        /// <summary>Optional filter by actor who performed actions.</summary>
        public string? ActorFilter { get; set; }

        /// <summary>Maximum number of items to include (default 500).</summary>
        public int MaxItems { get; set; } = 500;
    }

    /// <summary>
    /// A single record in the audit export, representing a workflow item and its decisions.
    /// Structured for CSV/JSON export and regulatory review.
    /// </summary>
    public class ReviewAuditExportRecord
    {
        /// <summary>Workflow item identifier.</summary>
        public string WorkflowId { get; set; } = string.Empty;

        /// <summary>Issuer identifier.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Item type.</summary>
        public WorkflowItemType ItemType { get; set; }

        /// <summary>Short title of the workflow item.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Current state.</summary>
        public WorkflowApprovalState CurrentState { get; set; }

        /// <summary>Actor who created the item.</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>When the item was created.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Actor who approved, if applicable.</summary>
        public string? ApprovedBy { get; set; }

        /// <summary>When the item was approved, if applicable.</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>Actor who rejected, if applicable.</summary>
        public string? RejectedBy { get; set; }

        /// <summary>When the item was rejected, if applicable.</summary>
        public DateTime? RejectedAt { get; set; }

        /// <summary>Reason for rejection or change request, if applicable.</summary>
        public string? RejectionOrChangeReason { get; set; }

        /// <summary>Number of audit history entries for this item.</summary>
        public int AuditEntryCount { get; set; }

        /// <summary>Optional external reference (e.g. policyId, assetId).</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Last updated timestamp.</summary>
        public DateTime LastUpdatedAt { get; set; }
    }

    /// <summary>
    /// Response containing the audit export data.
    /// </summary>
    public class ReviewAuditExportResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error code if unsuccessful.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Issuer identifier this export covers.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Export records.</summary>
        public List<ReviewAuditExportRecord> Records { get; set; } = new();

        /// <summary>Total number of records in the export.</summary>
        public int TotalCount { get; set; }

        /// <summary>Export criteria used to generate this export.</summary>
        public ReviewAuditExportRequest? ExportCriteria { get; set; }

        /// <summary>When this export was generated.</summary>
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Actor who requested the export.</summary>
        public string ExportedBy { get; set; } = string.Empty;

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    // ── Persistence Models ────────────────────────────────────────────────────

    /// <summary>
    /// Durable record of a compliance review decision.
    /// Stored by <see cref="IComplianceReviewRepository"/> and used to reconstruct
    /// enriched audit history after application restart.
    /// </summary>
    public class PersistedReviewDecision
    {
        /// <summary>Unique decision identifier.</summary>
        public string DecisionId { get; init; } = Guid.NewGuid().ToString();

        /// <summary>Issuer scope.</summary>
        public string IssuerId { get; init; } = string.Empty;

        /// <summary>Workflow item this decision applies to.</summary>
        public string WorkflowId { get; init; } = string.Empty;

        /// <summary>Actor who submitted the decision.</summary>
        public string ActorId { get; init; } = string.Empty;

        /// <summary>Actor's role at the time of the decision (for future audit reconstruction).</summary>
        public IssuerTeamRole? ActorRole { get; init; }

        /// <summary>Type of decision submitted.</summary>
        public ReviewDecisionType DecisionType { get; init; }

        /// <summary>Structured rationale provided by the reviewer.</summary>
        public string? Rationale { get; init; }

        /// <summary>Evidence item references cited by the reviewer.</summary>
        public List<string> EvidenceReferences { get; init; } = new();

        /// <summary>Whether the reviewer acknowledged open evidence issues.</summary>
        public bool AcknowledgesOpenIssues { get; init; }

        /// <summary>Optional reviewer note for the audit trail.</summary>
        public string? ReviewNote { get; init; }

        /// <summary>Correlation ID for distributed tracing and log correlation.</summary>
        public string? CorrelationId { get; init; }

        /// <summary>UTC timestamp when the decision was recorded.</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    // ── Diagnostics Models ────────────────────────────────────────────────────

    /// <summary>
    /// Category of a diagnostics event.
    /// </summary>
    public enum ReviewDiagnosticsEventCategory
    {
        /// <summary>Authorization was denied for a role-gated action.</summary>
        AuthorizationDenial = 0,

        /// <summary>A workflow state transition was attempted but invalid.</summary>
        InvalidTransition = 1,

        /// <summary>Evidence is missing or incomplete for a review decision.</summary>
        MissingEvidence = 2,

        /// <summary>Contradictory evidence was detected.</summary>
        EvidenceContradiction = 3,

        /// <summary>A workflow item has been in a blocked state for an extended period.</summary>
        StaleItem = 4
    }

    /// <summary>
    /// A single diagnostics event record.
    /// </summary>
    public class ReviewDiagnosticsEvent
    {
        /// <summary>Unique identifier for this diagnostics event.</summary>
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Category of the diagnostics event.</summary>
        public ReviewDiagnosticsEventCategory Category { get; set; }

        /// <summary>Severity of the event.</summary>
        public EvidenceIssueSeverity Severity { get; set; }

        /// <summary>Human-readable description of what happened.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Actionable guidance for operators to resolve the issue.</summary>
        public string OperatorGuidance { get; set; } = string.Empty;

        /// <summary>Associated workflow item ID, if applicable.</summary>
        public string? WorkflowId { get; set; }

        /// <summary>Actor involved, if applicable.</summary>
        public string? ActorId { get; set; }

        /// <summary>When the event was recorded.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Additional structured metadata.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Response containing structured diagnostics for an issuer's compliance review operations.
    /// Designed for operator visibility into authorization failures, evidence gaps, and workflow health.
    /// </summary>
    public class ReviewDiagnosticsResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error code if unsuccessful.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Issuer scope for these diagnostics.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Total number of active team members.</summary>
        public int ActiveMemberCount { get; set; }

        /// <summary>Total number of workflow items in all states.</summary>
        public int TotalWorkflowItems { get; set; }

        /// <summary>Number of items currently awaiting review.</summary>
        public int PendingReviewCount { get; set; }

        /// <summary>Number of items blocked by evidence issues.</summary>
        public int EvidenceBlockedCount { get; set; }

        /// <summary>Number of items in NeedsChanges state.</summary>
        public int NeedsChangesCount { get; set; }

        /// <summary>Recent diagnostics events (last 50).</summary>
        public List<ReviewDiagnosticsEvent> RecentEvents { get; set; } = new();

        /// <summary>Summary of event counts by category.</summary>
        public Dictionary<string, int> EventCategoryCounts { get; set; } = new();

        /// <summary>When diagnostics were collected.</summary>
        public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }
    }
}
