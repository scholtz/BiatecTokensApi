namespace BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision
{
    // ── Enumerations ──────────────────────────────────────────────────────────

    /// <summary>Top-level launch decision status.</summary>
    public enum LaunchDecisionStatus
    {
        /// <summary>All prerequisites met; launch is permitted.</summary>
        Ready,
        /// <summary>One or more hard blockers prevent launch.</summary>
        Blocked,
        /// <summary>Soft warnings present; launch may proceed with acknowledged risk.</summary>
        Warning,
        /// <summary>Requires manual compliance review before proceeding.</summary>
        NeedsReview
    }

    /// <summary>Outcome of a single decision rule evaluation.</summary>
    public enum RuleOutcome
    {
        /// <summary>Rule passed.</summary>
        Pass,
        /// <summary>Rule raised an advisory warning.</summary>
        Warning,
        /// <summary>Rule failed.</summary>
        Fail,
        /// <summary>Rule was skipped (prerequisite not available).</summary>
        Skipped
    }

    /// <summary>Severity of a blocker or warning item.</summary>
    public enum BlockerSeverity
    {
        /// <summary>Informational advisory.</summary>
        Info,
        /// <summary>Low severity; does not block launch.</summary>
        Low,
        /// <summary>Medium severity; should be resolved.</summary>
        Medium,
        /// <summary>High severity; blocks launch.</summary>
        High,
        /// <summary>Critical severity; immediate action required.</summary>
        Critical
    }

    /// <summary>Category of compliance evidence.</summary>
    public enum EvidenceCategory
    {
        /// <summary>Identity or KYC/AML verification evidence.</summary>
        Identity,
        /// <summary>Policy rule evaluation evidence.</summary>
        Policy,
        /// <summary>Subscription or entitlement evidence.</summary>
        Entitlement,
        /// <summary>Jurisdiction compliance evidence.</summary>
        Jurisdiction,
        /// <summary>Integration or infrastructure health evidence.</summary>
        Integration,
        /// <summary>Audit trail or historical record evidence.</summary>
        AuditTrail,
        /// <summary>Workflow or process prerequisite evidence.</summary>
        Workflow
    }

    /// <summary>Validation status of an evidence item.</summary>
    public enum EvidenceValidationStatus
    {
        /// <summary>Evidence was validated successfully.</summary>
        Valid,
        /// <summary>Evidence is pending validation.</summary>
        Pending,
        /// <summary>Evidence failed validation.</summary>
        Invalid,
        /// <summary>Evidence is stale or expired.</summary>
        Stale,
        /// <summary>Evidence source is unavailable.</summary>
        Unavailable
    }

    // ── Request models ────────────────────────────────────────────────────────

    /// <summary>
    /// Request to evaluate launch readiness and produce a decision record.
    /// </summary>
    public class LaunchDecisionRequest
    {
        /// <summary>Requesting user or organization identifier.</summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>Token standard being launched (ASA, ARC3, ARC200, ERC20, ARC1400).</summary>
        public string TokenStandard { get; set; } = string.Empty;

        /// <summary>Target blockchain network.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Optional token name for context.</summary>
        public string? TokenName { get; set; }

        /// <summary>
        /// Idempotency key.  Repeated calls with the same key return the cached result
        /// without re-running evaluation.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>Correlation ID for request tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Policy version to evaluate against (defaults to latest).</summary>
        public string? PolicyVersion { get; set; }

        /// <summary>When true, forces a fresh evaluation even if a cached result exists.</summary>
        public bool ForceRefresh { get; set; }
    }

    /// <summary>
    /// Request to retrieve a compliance evidence bundle.
    /// </summary>
    public class EvidenceBundleRequest
    {
        /// <summary>Owner identifier to retrieve evidence for.</summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>Optional: filter to a specific launch decision ID.</summary>
        public string? DecisionId { get; set; }

        /// <summary>Optional: filter by evidence category.</summary>
        public EvidenceCategory? Category { get; set; }

        /// <summary>Optional: include only evidence items after this timestamp.</summary>
        public DateTime? FromTimestamp { get; set; }

        /// <summary>Maximum number of evidence items to return (1–100, default 50).</summary>
        public int Limit { get; set; } = 50;

        /// <summary>Correlation ID for request tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Request to retrieve the decision trace for a specific evaluation.
    /// </summary>
    public class DecisionTraceRequest
    {
        /// <summary>Decision ID to retrieve the trace for.</summary>
        public string DecisionId { get; set; } = string.Empty;

        /// <summary>Correlation ID for request tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    // ── Core response models ──────────────────────────────────────────────────

    /// <summary>
    /// Comprehensive launch decision response.  Implements AC1-AC8 of the issue.
    /// </summary>
    public class LaunchDecisionResponse
    {
        /// <summary>Unique decision identifier (stable for idempotent replays).</summary>
        public string DecisionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Top-level launch decision status.</summary>
        public LaunchDecisionStatus Status { get; set; }

        /// <summary>Whether launch is permitted based on current evaluation.</summary>
        public bool CanLaunch { get; set; }

        /// <summary>Human-readable summary of the decision.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Ordered list of blockers preventing launch.</summary>
        public List<LaunchBlocker> Blockers { get; set; } = new();

        /// <summary>Non-critical warnings that should be acknowledged.</summary>
        public List<LaunchWarning> Warnings { get; set; } = new();

        /// <summary>Recommended actions to achieve a Ready state.</summary>
        public List<RecommendedAction> RecommendedActions { get; set; } = new();

        /// <summary>Summary of evidence evaluated during this decision.</summary>
        public List<EvidenceSummaryItem> EvidenceSummary { get; set; } = new();

        /// <summary>Policy version used for evaluation.</summary>
        public string PolicyVersion { get; set; } = string.Empty;

        /// <summary>Schema version for response contract stability.</summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>Correlation ID propagated from the request.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>UTC timestamp when the decision was made.</summary>
        public DateTime DecidedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Evaluation duration in milliseconds (observability).</summary>
        public long EvaluationTimeMs { get; set; }

        /// <summary>Whether this result was served from an idempotent cache.</summary>
        public bool IsIdempotentReplay { get; set; }

        /// <summary>Whether this decision requires further review and is not final.</summary>
        public bool IsProvisional { get; set; }

        /// <summary>Operation success flag.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Error code when Success = false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-safe error message when Success = false.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>A hard blocker preventing launch.</summary>
    public class LaunchBlocker
    {
        /// <summary>Stable identifier for this blocker type.</summary>
        public string BlockerId { get; set; } = string.Empty;

        /// <summary>Human-readable title.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed description of the blocker.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Severity level.</summary>
        public BlockerSeverity Severity { get; set; }

        /// <summary>Category of the blocker.</summary>
        public EvidenceCategory Category { get; set; }

        /// <summary>Ordered remediation steps.</summary>
        public List<string> RemediationSteps { get; set; } = new();

        /// <summary>Suggested owner team (e.g., "compliance", "engineering").</summary>
        public string? OwnerHint { get; set; }

        /// <summary>Estimated hours to resolve.</summary>
        public int? EstimatedResolutionHours { get; set; }

        /// <summary>IDs of other blockers that should be resolved first.</summary>
        public List<string> DependsOn { get; set; } = new();

        /// <summary>Rule ID that generated this blocker.</summary>
        public string? RuleId { get; set; }
    }

    /// <summary>A non-critical warning.</summary>
    public class LaunchWarning
    {
        /// <summary>Stable identifier for this warning type.</summary>
        public string WarningId { get; set; } = string.Empty;

        /// <summary>Human-readable title.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Severity level (typically Info or Low).</summary>
        public BlockerSeverity Severity { get; set; }

        /// <summary>Category of the warning.</summary>
        public EvidenceCategory Category { get; set; }

        /// <summary>Suggested actions to address the warning.</summary>
        public List<string> SuggestedActions { get; set; } = new();

        /// <summary>Rule ID that generated this warning.</summary>
        public string? RuleId { get; set; }
    }

    /// <summary>A recommended action to progress toward a Ready state.</summary>
    public class RecommendedAction
    {
        /// <summary>Stable action identifier.</summary>
        public string ActionId { get; set; } = string.Empty;

        /// <summary>Priority order (lower = higher priority).</summary>
        public int Priority { get; set; }

        /// <summary>Short action title.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed action description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Category of the action.</summary>
        public EvidenceCategory Category { get; set; }

        /// <summary>Whether this action is mandatory to unblock launch.</summary>
        public bool IsMandatory { get; set; }
    }

    /// <summary>Brief summary of a single evidence item included in a decision.</summary>
    public class EvidenceSummaryItem
    {
        /// <summary>Evidence item identifier.</summary>
        public string EvidenceId { get; set; } = string.Empty;

        /// <summary>Evidence category.</summary>
        public EvidenceCategory Category { get; set; }

        /// <summary>Validation status of this evidence.</summary>
        public EvidenceValidationStatus ValidationStatus { get; set; }

        /// <summary>Brief rationale or description.</summary>
        public string Rationale { get; set; } = string.Empty;

        /// <summary>When the evidence was collected.</summary>
        public DateTime CollectedAt { get; set; }
    }

    // ── Evidence bundle models ────────────────────────────────────────────────

    /// <summary>
    /// Full compliance evidence bundle for an owner.
    /// </summary>
    public class EvidenceBundleResponse
    {
        /// <summary>Unique bundle identifier.</summary>
        public string BundleId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Owner identifier.</summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>Evidence items in this bundle.</summary>
        public List<ComplianceEvidenceItem> Items { get; set; } = new();

        /// <summary>Total evidence items available (before Limit).</summary>
        public int TotalCount { get; set; }

        /// <summary>Correlation ID from the request.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>UTC timestamp when this bundle was assembled.</summary>
        public DateTime AssembledAt { get; set; } = DateTime.UtcNow;

        /// <summary>Schema version.</summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>Operation success flag.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Error code when Success = false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-safe error message when Success = false.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// A single compliance evidence item with provenance metadata.
    /// </summary>
    public class ComplianceEvidenceItem
    {
        /// <summary>Unique evidence item identifier.</summary>
        public string EvidenceId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Related decision ID (if any).</summary>
        public string? DecisionId { get; set; }

        /// <summary>Category of evidence.</summary>
        public EvidenceCategory Category { get; set; }

        /// <summary>Source system or provider that generated this evidence.</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>UTC timestamp when this evidence was collected.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Validation result for this evidence item.</summary>
        public EvidenceValidationStatus ValidationStatus { get; set; }

        /// <summary>Human-readable rationale or description of the evidence.</summary>
        public string Rationale { get; set; } = string.Empty;

        /// <summary>Data integrity hash (SHA-256 of evidence payload).</summary>
        public string? DataHash { get; set; }

        /// <summary>Optional: reference to external source document or record.</summary>
        public string? ReferenceId { get; set; }

        /// <summary>Optional: additional machine-readable metadata.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>When this evidence expires (UTC), if applicable.</summary>
        public DateTime? ExpiresAt { get; set; }
    }

    // ── Decision trace models ─────────────────────────────────────────────────

    /// <summary>
    /// Structured decision trace showing which rules fired and why.
    /// </summary>
    public class DecisionTraceResponse
    {
        /// <summary>Decision ID this trace belongs to.</summary>
        public string DecisionId { get; set; } = string.Empty;

        /// <summary>Policy version evaluated.</summary>
        public string PolicyVersion { get; set; } = string.Empty;

        /// <summary>Ordered list of rule evaluations (deterministic).</summary>
        public List<RuleEvaluationRecord> Rules { get; set; } = new();

        /// <summary>Overall outcome of the trace.</summary>
        public LaunchDecisionStatus OverallOutcome { get; set; }

        /// <summary>Total evaluation duration in milliseconds.</summary>
        public long EvaluationTimeMs { get; set; }

        /// <summary>UTC timestamp of the evaluation.</summary>
        public DateTime EvaluatedAt { get; set; }

        /// <summary>Correlation ID.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Schema version.</summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>Operation success flag.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Error code when Success = false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-safe error message when Success = false.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Record of a single rule evaluation within a decision trace.
    /// </summary>
    public class RuleEvaluationRecord
    {
        /// <summary>Stable rule identifier.</summary>
        public string RuleId { get; set; } = string.Empty;

        /// <summary>Human-readable rule name.</summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>Category this rule belongs to.</summary>
        public EvidenceCategory Category { get; set; }

        /// <summary>Outcome of this rule evaluation.</summary>
        public RuleOutcome Outcome { get; set; }

        /// <summary>Input values that were evaluated (non-sensitive).</summary>
        public Dictionary<string, string> InputSnapshot { get; set; } = new();

        /// <summary>Reason the rule produced this outcome.</summary>
        public string Rationale { get; set; } = string.Empty;

        /// <summary>Remediation guidance if outcome is Fail or Warning.</summary>
        public string? RemediationGuidance { get; set; }

        /// <summary>Evaluation order (1-based, deterministic).</summary>
        public int EvaluationOrder { get; set; }

        /// <summary>Duration of this individual rule evaluation in milliseconds.</summary>
        public long DurationMs { get; set; }

        /// <summary>Evidence item IDs referenced by this rule.</summary>
        public List<string> EvidenceIds { get; set; } = new();
    }
}
