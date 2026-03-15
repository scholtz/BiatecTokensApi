namespace BiatecTokensApi.Models.ApprovalWorkflow
{
    // ── Enumerations ──────────────────────────────────────────────────────────

    /// <summary>
    /// Represents the category of an enterprise approval stage in the release pipeline.
    /// </summary>
    public enum ApprovalStageType
    {
        /// <summary>Compliance team sign-off (KYC/AML, regulatory).</summary>
        Compliance = 0,

        /// <summary>Legal team sign-off (contracts, IP, jurisdiction).</summary>
        Legal = 1,

        /// <summary>Procurement team sign-off (vendor, cost approval).</summary>
        Procurement = 2,

        /// <summary>Executive sponsor sign-off.</summary>
        Executive = 3,

        /// <summary>Shared Operations team sign-off (infrastructure, security).</summary>
        SharedOperations = 4
    }

    /// <summary>
    /// Decision status of a single approval stage.
    /// </summary>
    public enum ApprovalDecisionStatus
    {
        /// <summary>Stage has not yet been acted on.</summary>
        Pending = 0,

        /// <summary>Stage has been approved.</summary>
        Approved = 1,

        /// <summary>Stage has been rejected. Release is blocked.</summary>
        Rejected = 2,

        /// <summary>Stage is explicitly blocked pending external resolution.</summary>
        Blocked = 3,

        /// <summary>Stage requires follow-up action from the requestor before progressing.</summary>
        NeedsFollowUp = 4
    }

    /// <summary>
    /// Overall launch readiness posture derived from stage decisions and evidence readiness.
    /// </summary>
    public enum ReleasePosture
    {
        /// <summary>All stages approved and all evidence is fresh. Release may proceed.</summary>
        LaunchReady = 0,

        /// <summary>One or more stages are Rejected, Blocked, Pending, or NeedsFollowUp.</summary>
        BlockedByStageDecision = 1,

        /// <summary>All stages approved but one or more evidence items are stale.</summary>
        BlockedByStaleEvidence = 2,

        /// <summary>One or more required evidence items are missing.</summary>
        BlockedByMissingEvidence = 3,

        /// <summary>Evidence source cannot be evaluated due to configuration issues.</summary>
        ConfigurationBlocked = 4
    }

    /// <summary>
    /// Readiness category of a release evidence item.
    /// </summary>
    public enum EvidenceReadinessCategory
    {
        /// <summary>Evidence is present and current.</summary>
        Fresh = 0,

        /// <summary>Evidence exists but is older than the acceptable freshness window.</summary>
        Stale = 1,

        /// <summary>Required evidence has not been collected.</summary>
        Missing = 2,

        /// <summary>Evidence source is not reachable or not configured.</summary>
        ConfigurationBlocked = 3
    }

    /// <summary>
    /// Domain that owns resolution of an approval blocker or pending stage.
    /// </summary>
    public enum ApprovalOwnerDomain
    {
        /// <summary>No owner — all stages approved.</summary>
        None = 0,

        /// <summary>Compliance team owns the next action.</summary>
        Compliance = 1,

        /// <summary>Legal team owns the next action.</summary>
        Legal = 2,

        /// <summary>Procurement team owns the next action.</summary>
        Procurement = 3,

        /// <summary>Executive sponsor owns the next action.</summary>
        Executive = 4,

        /// <summary>Shared Operations team owns the next action.</summary>
        SharedOperations = 5,

        /// <summary>Requestor must supply additional information or corrections.</summary>
        Requestor = 6,

        /// <summary>Platform team must resolve a configuration or infrastructure issue.</summary>
        Platform = 7
    }

    // ── Core Domain Models ─────────────────────────────────────────────────────

    /// <summary>
    /// A structured blocker preventing release from proceeding.
    /// </summary>
    public class ApprovalBlocker
    {
        /// <summary>Unique identifier for this blocker.</summary>
        public string BlockerId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Human-readable description of why this blocker exists.</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>Severity of this blocker (Critical, High, Medium, Low).</summary>
        public string Severity { get; set; } = "High";

        /// <summary>The approval stage type this blocker is associated with, if any.</summary>
        public ApprovalStageType? LinkedStageType { get; set; }

        /// <summary>The evidence item ID this blocker references, if any.</summary>
        public string? LinkedEvidenceId { get; set; }

        /// <summary>The domain responsible for resolving this blocker.</summary>
        public ApprovalOwnerDomain Attribution { get; set; } = ApprovalOwnerDomain.None;

        /// <summary>UTC timestamp when this blocker was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp when this blocker was resolved, if resolved.</summary>
        public DateTime? ResolvedAt { get; set; }
    }

    /// <summary>
    /// Immutable audit event recording a state change or significant action in the approval workflow.
    /// </summary>
    public class ApprovalAuditEvent
    {
        /// <summary>Unique event identifier.</summary>
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Structured event type identifier (e.g., "StageDecisionSubmitted", "WorkflowStateQueried").</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>The release package this event belongs to.</summary>
        public string ReleasePackageId { get; set; } = string.Empty;

        /// <summary>The approval stage this event affects, if applicable.</summary>
        public ApprovalStageType? StageType { get; set; }

        /// <summary>Identity of the actor who triggered this event.</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Display-friendly name of the actor.</summary>
        public string ActorDisplayName { get; set; } = string.Empty;

        /// <summary>UTC timestamp when this event occurred.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Human-readable description of what happened.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Status before this event, if a transition occurred.</summary>
        public ApprovalDecisionStatus? PreviousStatus { get; set; }

        /// <summary>Status after this event, if a transition occurred.</summary>
        public ApprovalDecisionStatus? NewStatus { get; set; }

        /// <summary>Actor-supplied note or rationale.</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>Distributed tracing correlation identifier.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Additional structured metadata for this event.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Readiness status of a single piece of release evidence.
    /// </summary>
    public class EvidenceReadinessItem
    {
        /// <summary>Unique identifier for this evidence item.</summary>
        public string EvidenceId { get; set; } = string.Empty;

        /// <summary>Human-readable name of the evidence item.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Category label (e.g., "KYC", "Legal", "Security").</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Current readiness assessment.</summary>
        public EvidenceReadinessCategory ReadinessCategory { get; set; } = EvidenceReadinessCategory.Missing;

        /// <summary>UTC timestamp when this evidence was last evaluated.</summary>
        public DateTime? LastCheckedAt { get; set; }

        /// <summary>UTC timestamp after which this evidence is considered stale.</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Human-readable description of what this evidence covers.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Whether this evidence item blocks release when not Fresh.</summary>
        public bool IsReleaseBlocking { get; set; } = true;

        /// <summary>Explanation of why evidence is ConfigurationBlocked, if applicable.</summary>
        public string? ConfigurationNote { get; set; }
    }

    /// <summary>
    /// Current state of a single approval stage within a release package.
    /// </summary>
    public class ApprovalStageRecord
    {
        /// <summary>Which stage this record represents.</summary>
        public ApprovalStageType StageType { get; set; }

        /// <summary>Current decision status.</summary>
        public ApprovalDecisionStatus Status { get; set; } = ApprovalDecisionStatus.Pending;

        /// <summary>Domain that owns this stage.</summary>
        public ApprovalOwnerDomain OwnerDomain { get; set; }

        /// <summary>Actor ID who recorded the last decision, if any.</summary>
        public string? DecidedBy { get; set; }

        /// <summary>UTC timestamp when the last decision was recorded.</summary>
        public DateTime? DecidedAt { get; set; }

        /// <summary>Rationale or note supplied with the last decision.</summary>
        public string? Note { get; set; }

        /// <summary>IDs of active blockers associated with this stage.</summary>
        public List<string> BlockerIds { get; set; } = new();

        /// <summary>UTC timestamp when this record was last modified.</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Flat persistence record for a stage decision, suitable for in-memory or database storage.
    /// </summary>
    public class PersistedApprovalStageDecision
    {
        /// <summary>Release package this decision belongs to.</summary>
        public string PackageId { get; set; } = string.Empty;

        /// <summary>Stage that was decided on.</summary>
        public ApprovalStageType StageType { get; set; }

        /// <summary>Decision recorded.</summary>
        public ApprovalDecisionStatus Status { get; set; }

        /// <summary>Actor who submitted this decision.</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Rationale or note supplied with the decision.</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>UTC timestamp of this decision.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Distributed tracing correlation identifier.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Unique identifier for this decision record.</summary>
        public string DecisionId { get; set; } = Guid.NewGuid().ToString();
    }

    // ── Request / Response DTOs ────────────────────────────────────────────────

    /// <summary>
    /// Request to submit an approval stage decision for a release package.
    /// </summary>
    public class SubmitStageDecisionRequest
    {
        /// <summary>Stage being decided on.</summary>
        public ApprovalStageType StageType { get; set; }

        /// <summary>
        /// Decision to record. Must not be Pending.
        /// </summary>
        public ApprovalDecisionStatus Decision { get; set; }

        /// <summary>
        /// Rationale or note. Required when Decision is Rejected, Blocked, or NeedsFollowUp.
        /// </summary>
        public string? Note { get; set; }

        /// <summary>
        /// Optional list of evidence item IDs the actor explicitly acknowledges,
        /// e.g., knowingly accepting a stale evidence item.
        /// </summary>
        public List<string>? EvidenceAcknowledgements { get; set; }
    }

    /// <summary>
    /// Response to a stage decision submission.
    /// </summary>
    public class SubmitStageDecisionResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The newly created decision record identifier.</summary>
        public string? DecisionId { get; set; }

        /// <summary>Updated stage state after the decision was applied.</summary>
        public ApprovalStageRecord? UpdatedStage { get; set; }

        /// <summary>New overall release posture after the decision was applied.</summary>
        public ReleasePosture? NewReleasePosture { get; set; }

        /// <summary>Structured error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Operator-facing guidance to resolve the error.</summary>
        public string? OperatorGuidance { get; set; }

        /// <summary>Distributed tracing correlation identifier.</summary>
        public string CorrelationId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Full approval workflow state for a release package.
    /// </summary>
    public class ApprovalWorkflowStateResponse
    {
        /// <summary>Whether the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Release package identifier.</summary>
        public string ReleasePackageId { get; set; } = string.Empty;

        /// <summary>Current state of each approval stage.</summary>
        public List<ApprovalStageRecord> Stages { get; set; } = new();

        /// <summary>Current overall release posture.</summary>
        public ReleasePosture ReleasePosture { get; set; }

        /// <summary>Domain that currently owns the next required action.</summary>
        public ApprovalOwnerDomain ActiveOwnerDomain { get; set; }

        /// <summary>Active blockers preventing release.</summary>
        public List<ApprovalBlocker> ActiveBlockers { get; set; } = new();

        /// <summary>Evidence readiness summary for all evidence items.</summary>
        public List<EvidenceReadinessItem> EvidenceSummary { get; set; } = new();

        /// <summary>UTC timestamp when the posture was last calculated.</summary>
        public DateTime PostureCalculatedAt { get; set; }

        /// <summary>Structured error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Distributed tracing correlation identifier.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Human-readable explanation of why the current posture was assigned.</summary>
        public string PostureRationale { get; set; } = string.Empty;
    }

    /// <summary>
    /// Summary of release evidence readiness for a release package.
    /// </summary>
    public class ReleaseEvidenceSummaryResponse
    {
        /// <summary>Whether the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Release package identifier.</summary>
        public string ReleasePackageId { get; set; } = string.Empty;

        /// <summary>Individual evidence items with their readiness status.</summary>
        public List<EvidenceReadinessItem> EvidenceItems { get; set; } = new();

        /// <summary>Worst-case overall readiness across all evidence items.</summary>
        public EvidenceReadinessCategory OverallReadiness { get; set; }

        /// <summary>Number of evidence items in Fresh state.</summary>
        public int FreshCount { get; set; }

        /// <summary>Number of evidence items in Stale state.</summary>
        public int StaleCount { get; set; }

        /// <summary>Number of evidence items in Missing state.</summary>
        public int MissingCount { get; set; }

        /// <summary>Number of evidence items in ConfigurationBlocked state.</summary>
        public int ConfigurationBlockedCount { get; set; }

        /// <summary>Structured error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Distributed tracing correlation identifier.</summary>
        public string CorrelationId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Approval workflow audit history for a release package.
    /// </summary>
    public class ApprovalAuditHistoryResponse
    {
        /// <summary>Whether the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Release package identifier.</summary>
        public string ReleasePackageId { get; set; } = string.Empty;

        /// <summary>Audit events in chronological order.</summary>
        public List<ApprovalAuditEvent> Events { get; set; } = new();

        /// <summary>Total number of events returned.</summary>
        public int TotalCount { get; set; }

        /// <summary>Structured error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Distributed tracing correlation identifier.</summary>
        public string CorrelationId { get; set; } = string.Empty;
    }
}
