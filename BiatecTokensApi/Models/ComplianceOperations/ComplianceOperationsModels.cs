using BiatecTokensApi.Models.Webhook;

namespace BiatecTokensApi.Models.ComplianceOperations
{
    // ── Enums ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// SLA status of a compliance operations queue item.
    /// Encodes urgency from best to worst so numeric comparison is meaningful.
    /// </summary>
    public enum ComplianceOpsSlaStatus
    {
        /// <summary>Work item is on schedule with no imminent deadline risk.</summary>
        OnTrack = 0,
        /// <summary>Deadline is approaching within the warning window (≤ 3 days).</summary>
        DueSoon = 1,
        /// <summary>Deadline has passed and the item is overdue.</summary>
        Overdue = 2,
        /// <summary>Item cannot progress due to an unresolved blocker.</summary>
        Blocked = 3
    }

    /// <summary>
    /// Categorises the type of workflow that generated a queue item.
    /// Used for routing and aggregation across the operations cockpit.
    /// </summary>
    public enum ComplianceOpsWorkflowType
    {
        /// <summary>Item originates from a scheduled compliance report run.</summary>
        ScheduledReporting,
        /// <summary>Item originates from a multi-stage approval workflow.</summary>
        ApprovalWorkflow,
        /// <summary>Item originates from a compliance case (onboarding/review).</summary>
        ComplianceCase,
        /// <summary>Item originates from an ongoing monitoring task.</summary>
        OngoingMonitoring,
        /// <summary>Item originates from a regulatory evidence package review.</summary>
        RegulatoryEvidence
    }

    /// <summary>
    /// Role-based audience for an operations summary or queue view.
    /// Determines which items are surfaced and how urgency is weighted.
    /// </summary>
    public enum ComplianceOpsRole
    {
        /// <summary>Day-to-day compliance manager responsible for case and evidence reviews.</summary>
        ComplianceManager,
        /// <summary>Operations lead responsible for SLA management and workflow coordination.</summary>
        OperationsLead,
        /// <summary>Executive or board-level audience requiring a high-level risk summary.</summary>
        ExecutiveSummary
    }

    /// <summary>
    /// Categorises the reason a queue item is blocked or at risk.
    /// Enables the frontend to render plain-language explanations.
    /// </summary>
    public enum ComplianceOpsBlockerCategory
    {
        /// <summary>No blocker present.</summary>
        None,
        /// <summary>Required compliance evidence is missing.</summary>
        MissingEvidence,
        /// <summary>Required evidence exists but has expired or is stale.</summary>
        StaleEvidence,
        /// <summary>An approval stage is pending a decision.</summary>
        PendingApproval,
        /// <summary>A sanctions or watchlist review is unresolved.</summary>
        UnresolvedSanctionsReview,
        /// <summary>KYC review has not been completed or has failed.</summary>
        KycIncomplete,
        /// <summary>A downstream delivery to a regulator or system has failed.</summary>
        DeliveryFailure,
        /// <summary>The item is escalated and awaiting senior-reviewer action.</summary>
        EscalationPending,
        /// <summary>The item is waiting for a prior workflow to complete.</summary>
        UpstreamDependency,
        /// <summary>The item is blocked by a policy or business-rule conflict.</summary>
        PolicyConflict
    }

    // ── Domain objects ────────────────────────────────────────────────────────

    /// <summary>
    /// A single item in the compliance operations queue.
    /// Carries enough information for the frontend to render title, SLA status,
    /// owner, deep-link, and plain-language explanation without additional calls.
    /// </summary>
    public class ComplianceOpsQueueItem
    {
        /// <summary>Stable identifier for this queue item (suitable for deep links).</summary>
        public string ItemId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Short business-language title of the work item.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Optional additional description for context panels.</summary>
        public string? Description { get; set; }

        /// <summary>Type of workflow that generated this item.</summary>
        public ComplianceOpsWorkflowType WorkflowType { get; set; }

        /// <summary>ID of the source record (e.g. case ID, report run ID, workflow ID).</summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>Current SLA status.</summary>
        public ComplianceOpsSlaStatus SlaStatus { get; set; }

        /// <summary>UTC timestamp when the item becomes due or overdue (null = no fixed deadline).</summary>
        public DateTime? DueAt { get; set; }

        /// <summary>Number of hours remaining until due (negative = overdue). Null if no deadline.</summary>
        public double? HoursUntilDue { get; set; }

        /// <summary>Blocking category if the item cannot progress.</summary>
        public ComplianceOpsBlockerCategory BlockerCategory { get; set; } = ComplianceOpsBlockerCategory.None;

        /// <summary>Human-readable explanation of why the item is blocked or at risk.</summary>
        public string? BlockerReason { get; set; }

        /// <summary>Role that should own and action this item.</summary>
        public ComplianceOpsRole OwnerRole { get; set; }

        /// <summary>Identity of the assigned reviewer or owner (null = unassigned).</summary>
        public string? AssignedTo { get; set; }

        /// <summary>UTC timestamp when this item was first created or entered the queue.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp of the most recent state change.</summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Prioritisation score (higher = more urgent). Used for queue ordering.</summary>
        public int PriorityScore { get; set; }

        /// <summary>
        /// True when this item is fail-closed: missing evidence, failed delivery, or
        /// unresolved approval means the downstream flow cannot proceed.
        /// </summary>
        public bool IsFailClosed { get; set; }

        /// <summary>
        /// Evidence freshness indicator. False when required evidence is missing or stale.
        /// </summary>
        public bool EvidenceFresh { get; set; } = true;

        /// <summary>Optional list of lineage tags for tracing across workflows.</summary>
        public List<string> LineageTags { get; set; } = new();
    }

    /// <summary>
    /// Role-specific summary of the compliance operations workload.
    /// Suitable for rendering a compact cockpit tile or card.
    /// </summary>
    public class ComplianceOpsRoleSummary
    {
        /// <summary>Audience for this summary.</summary>
        public ComplianceOpsRole Role { get; set; }

        /// <summary>Total queue items visible to this role.</summary>
        public int TotalItems { get; set; }

        /// <summary>Items that are on track with no urgent action needed.</summary>
        public int OnTrackCount { get; set; }

        /// <summary>Items due within the warning window.</summary>
        public int DueSoonCount { get; set; }

        /// <summary>Items that are overdue.</summary>
        public int OverdueCount { get; set; }

        /// <summary>Items that are blocked and cannot progress without intervention.</summary>
        public int BlockedCount { get; set; }

        /// <summary>Highest SLA status across all items (worst-case indicator).</summary>
        public ComplianceOpsSlaStatus OverallStatus { get; set; }

        /// <summary>
        /// True if any item is fail-closed, meaning a downstream workflow is gated
        /// on resolution of at least one blocked item.
        /// </summary>
        public bool HasFailClosedBlockers { get; set; }
    }

    /// <summary>
    /// Cross-workflow compliance operations overview aggregating signals from
    /// scheduled reporting, approval workflows, compliance cases, and monitoring tasks.
    /// </summary>
    public class ComplianceOperationsOverview
    {
        /// <summary>UTC timestamp when this overview was computed.</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Total number of active queue items across all workflows.</summary>
        public int TotalQueueItems { get; set; }

        /// <summary>Breakdown by SLA status across all items.</summary>
        public int OnTrackCount { get; set; }

        /// <summary>Items due soon (within warning window).</summary>
        public int DueSoonCount { get; set; }

        /// <summary>Items overdue.</summary>
        public int OverdueCount { get; set; }

        /// <summary>Items blocked.</summary>
        public int BlockedCount { get; set; }

        /// <summary>Count of fail-closed items where downstream flows are gated.</summary>
        public int FailClosedCount { get; set; }

        /// <summary>Breakdown by workflow type.</summary>
        public Dictionary<ComplianceOpsWorkflowType, int> CountByWorkflowType { get; set; } = new();

        /// <summary>Breakdown by blocker category (only non-None entries).</summary>
        public Dictionary<ComplianceOpsBlockerCategory, int> CountByBlockerCategory { get; set; } = new();

        /// <summary>Per-role summaries for quick cockpit tile rendering.</summary>
        public List<ComplianceOpsRoleSummary> RoleSummaries { get; set; } = new();

        /// <summary>
        /// Overall health status derived from the worst-case SLA state across all items.
        /// Healthy = all OnTrack; AtRisk = any DueSoon; Critical = any Overdue or Blocked.
        /// </summary>
        public string OverallHealthStatus { get; set; } = "Healthy";

        /// <summary>Plain-language explanation of the current health state.</summary>
        public string HealthSummaryMessage { get; set; } = string.Empty;
    }

    // ── Request / response DTOs ───────────────────────────────────────────────

    /// <summary>Request parameters for retrieving a role-filtered compliance operations queue.</summary>
    public class ComplianceOpsQueueRequest
    {
        /// <summary>Optional role filter. Null returns items for all roles.</summary>
        public ComplianceOpsRole? Role { get; set; }

        /// <summary>Optional workflow type filter.</summary>
        public ComplianceOpsWorkflowType? WorkflowType { get; set; }

        /// <summary>Optional SLA status filter.</summary>
        public ComplianceOpsSlaStatus? SlaStatus { get; set; }

        /// <summary>When true, returns only fail-closed items.</summary>
        public bool FailClosedOnly { get; set; }

        /// <summary>Maximum number of items to return (default 50, max 200).</summary>
        public int PageSize { get; set; } = 50;

        /// <summary>Zero-based page number for pagination.</summary>
        public int Page { get; set; } = 0;
    }

    /// <summary>Response containing a prioritised compliance operations queue.</summary>
    public class ComplianceOpsQueueResponse
    {
        /// <summary>Whether the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Prioritised queue items ordered by PriorityScore descending.</summary>
        public List<ComplianceOpsQueueItem> Items { get; set; } = new();

        /// <summary>Total matching items before pagination.</summary>
        public int TotalCount { get; set; }

        /// <summary>Applied role filter (null = all roles).</summary>
        public ComplianceOpsRole? AppliedRoleFilter { get; set; }

        /// <summary>UTC timestamp when this response was generated.</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Error code, populated when Success is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message, populated when Success is false.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response wrapping the compliance operations overview.</summary>
    public class ComplianceOperationsOverviewResponse
    {
        /// <summary>Whether the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The computed overview. Null when Success is false.</summary>
        public ComplianceOperationsOverview? Overview { get; set; }

        /// <summary>Error code, populated when Success is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message, populated when Success is false.</summary>
        public string? ErrorMessage { get; set; }
    }

    // ── Webhook / audit payload ────────────────────────────────────────────────

    /// <summary>
    /// Payload emitted when an operations queue item transitions to a new SLA state.
    /// Allows enterprise subscribers to react to overdue or blocked items in real time.
    /// </summary>
    public class ComplianceOpsStateTransitionEvent
    {
        /// <summary>Stable ID of the affected queue item.</summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>Source record identifier (case ID, report ID, etc.).</summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>Type of workflow that owns this item.</summary>
        public ComplianceOpsWorkflowType WorkflowType { get; set; }

        /// <summary>Previous SLA status.</summary>
        public ComplianceOpsSlaStatus FromStatus { get; set; }

        /// <summary>New SLA status after transition.</summary>
        public ComplianceOpsSlaStatus ToStatus { get; set; }

        /// <summary>Blocker category at the time of transition.</summary>
        public ComplianceOpsBlockerCategory BlockerCategory { get; set; }

        /// <summary>Actor who triggered or was assigned the transition (may be system).</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>UTC timestamp of the transition.</summary>
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string CorrelationId { get; set; } = string.Empty;
    }
}
