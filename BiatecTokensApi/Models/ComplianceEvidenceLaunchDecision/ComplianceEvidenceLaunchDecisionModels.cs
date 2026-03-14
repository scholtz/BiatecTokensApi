namespace BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision
{
    // ── Export format enum ────────────────────────────────────────────────────

    /// <summary>Requested artifact format for evidence export.</summary>
    public enum EvidenceExportFormat
    {
        /// <summary>Structured JSON export (default).</summary>
        Json,
        /// <summary>Tabular CSV export.</summary>
        Csv
    }

    // ── Evidence freshness enum ───────────────────────────────────────────────

    /// <summary>Freshness classification of an evidence bundle or item.</summary>
    public enum EvidenceFreshnessStatus
    {
        /// <summary>Evidence is current (collected within the policy freshness window).</summary>
        Fresh,
        /// <summary>Evidence is approaching staleness.</summary>
        NearingExpiry,
        /// <summary>Evidence has exceeded the policy freshness window and is stale.</summary>
        Stale,
        /// <summary>Evidence freshness cannot be determined (timestamp absent).</summary>
        Unknown
    }

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

    /// <summary>Severity of a launch blocker or warning item.</summary>
    public enum LaunchBlockerSeverity
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
    /// Request to export a compliance evidence bundle as a downloadable artifact.
    /// </summary>
    public class EvidenceExportRequest
    {
        /// <summary>Owner identifier whose evidence should be exported.</summary>
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>Optional: filter to a specific launch decision ID.</summary>
        public string? DecisionId { get; set; }

        /// <summary>Optional: filter by evidence category.</summary>
        public EvidenceCategory? Category { get; set; }

        /// <summary>Optional: include only evidence items after this timestamp.</summary>
        public DateTime? FromTimestamp { get; set; }

        /// <summary>Maximum number of evidence items to include (1–500, default 100).</summary>
        public int Limit { get; set; } = 100;

        /// <summary>Correlation ID for request tracing.</summary>
        public string? CorrelationId { get; set; }
    }

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

        /// <summary>
        /// Whether this decision constitutes release-grade evidence.
        /// True only when all rules pass or produce only low-severity warnings,
        /// the policy version is current, and no mandatory blockers are outstanding.
        /// </summary>
        public bool IsReleaseGradeEvidence { get; set; }

        /// <summary>
        /// Human-readable explanation of the release-grade determination.
        /// For example: "Release-grade: all mandatory checks passed on policy 2026.03.07.1."
        /// Or: "Not release-grade: entitlement check failed (ARC1400 requires Premium tier)."
        /// </summary>
        public string ReleaseGradeNote { get; set; } = string.Empty;

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
        public LaunchBlockerSeverity Severity { get; set; }

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
        public LaunchBlockerSeverity Severity { get; set; }

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
    /// Snapshot of the active compliance policy at bundle generation time.
    /// </summary>
    public class PolicySnapshot
    {
        /// <summary>Policy version identifier (e.g., "2026.03.07.1").</summary>
        public string PolicyVersion { get; set; } = string.Empty;

        /// <summary>Human-readable policy name.</summary>
        public string PolicyName { get; set; } = string.Empty;

        /// <summary>UTC timestamp when this policy version took effect.</summary>
        public DateTime EffectiveAt { get; set; }

        /// <summary>UTC timestamp when this policy expires or is superseded (null = indefinite).</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Whether this is the currently active policy version.</summary>
        public bool IsCurrent { get; set; }

        /// <summary>Short description of the policy's compliance scope.</summary>
        public string Scope { get; set; } = string.Empty;

        /// <summary>Hash of the policy document for integrity verification.</summary>
        public string? PolicyHash { get; set; }
    }

    /// <summary>
    /// Attestation record linked to a compliance evidence bundle.
    /// </summary>
    public class AttestationRecord
    {
        /// <summary>Stable attestation identifier.</summary>
        public string AttestationId { get; set; } = string.Empty;

        /// <summary>Type of attestation (e.g., "KYC_PASSED", "AML_CLEARED", "MANUAL_REVIEW").</summary>
        public string AttestationType { get; set; } = string.Empty;

        /// <summary>Actor who issued the attestation (system or human).</summary>
        public string IssuedBy { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the attestation was issued.</summary>
        public DateTime IssuedAt { get; set; }

        /// <summary>UTC timestamp when the attestation expires (null = no expiry).</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Whether the attestation is still valid.</summary>
        public bool IsValid { get; set; }

        /// <summary>Human-readable attestation description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Integrity hash of the attested data.</summary>
        public string? DataHash { get; set; }
    }

    /// <summary>
    /// Reference to an audit trail entry relevant to this evidence bundle.
    /// </summary>
    public class AuditTrailReference
    {
        /// <summary>Audit trail entry identifier.</summary>
        public string AuditId { get; set; } = string.Empty;

        /// <summary>Event type (e.g., "LAUNCH_DECISION_CREATED", "POLICY_UPDATED").</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>UTC timestamp of the audit event.</summary>
        public DateTime OccurredAt { get; set; }

        /// <summary>Actor who triggered the event.</summary>
        public string PerformedBy { get; set; } = string.Empty;

        /// <summary>Brief description of the event.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Category of audit event for filtering.</summary>
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// Export manifest describing what is included in a downloadable evidence artifact.
    /// </summary>
    public class ExportManifest
    {
        /// <summary>Unique export artifact identifier.</summary>
        public string ExportId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>UTC timestamp when the export was generated.</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Schema version of the export format.</summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>Number of evidence items included.</summary>
        public int EvidenceItemCount { get; set; }

        /// <summary>Number of attestation records included.</summary>
        public int AttestationCount { get; set; }

        /// <summary>Number of audit trail references included.</summary>
        public int AuditTrailReferenceCount { get; set; }

        /// <summary>Whether this export is release-grade evidence.</summary>
        public bool IsReleaseGradeEvidence { get; set; }

        /// <summary>Human-readable note about release-grade eligibility.</summary>
        public string ReleaseGradeNote { get; set; } = string.Empty;

        /// <summary>Overall evidence freshness at export time.</summary>
        public EvidenceFreshnessStatus FreshnessStatus { get; set; }

        /// <summary>The policy version active at export time.</summary>
        public string ActivePolicyVersion { get; set; } = string.Empty;

        /// <summary>
        /// SHA-256 hash of the serialized evidence payload,
        /// enabling downstream integrity verification.
        /// </summary>
        public string? PayloadHash { get; set; }
    }

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

        /// <summary>
        /// Whether this bundle contains release-grade evidence.
        /// Release-grade evidence requires: all evidence Valid (no Invalid/Stale),
        /// current policy version, and no mandatory blockers outstanding.
        /// Set to false when evidence is incomplete, permissive-only, or stale.
        /// </summary>
        public bool IsReleaseGradeEvidence { get; set; }

        /// <summary>
        /// Human-readable explanation of release-grade eligibility.
        /// For example: "All evidence is current, policy version is 2026.03.07.1, and no blockers are outstanding."
        /// Or: "Evidence is not release-grade: 2 items are stale (age > 90 days) and ARC1400 requires Premium entitlement."
        /// </summary>
        public string ReleaseGradeNote { get; set; } = string.Empty;

        /// <summary>
        /// Overall freshness status of the evidence bundle.
        /// Stale evidence cannot be release-grade.
        /// </summary>
        public EvidenceFreshnessStatus FreshnessStatus { get; set; }

        /// <summary>
        /// Snapshot of the compliance policy active when the bundle was assembled.
        /// </summary>
        public PolicySnapshot? PolicySnapshot { get; set; }

        /// <summary>
        /// Attestation records providing additional assurance for the evidence.
        /// </summary>
        public List<AttestationRecord> AttestationRecords { get; set; } = new();

        /// <summary>
        /// References to relevant audit trail entries.
        /// </summary>
        public List<AuditTrailReference> AuditTrailReferences { get; set; } = new();

        /// <summary>
        /// Export manifest describing this bundle's contents and integrity metadata.
        /// </summary>
        public ExportManifest? ExportManifest { get; set; }

        /// <summary>
        /// Ordered remediation steps when the bundle is not release-grade.
        /// Empty when <see cref="IsReleaseGradeEvidence"/> is true.
        /// </summary>
        public List<string> RemediationGuidance { get; set; } = new();

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

    // ── Export result model ───────────────────────────────────────────────────

    /// <summary>
    /// Result of a compliance evidence export operation.
    /// Contains the serialized artifact bytes, suggested filename, and MIME type.
    /// </summary>
    public class EvidenceExportResult
    {
        /// <summary>Whether the export was generated successfully.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Serialized artifact content (UTF-8 for JSON/CSV).</summary>
        public byte[]? Content { get; set; }

        /// <summary>Suggested download filename (e.g., "evidence-bundle-20260314.json").</summary>
        public string? FileName { get; set; }

        /// <summary>MIME type of the content (application/json or text/csv).</summary>
        public string? ContentType { get; set; }

        /// <summary>Export manifest describing the artifact's contents.</summary>
        public ExportManifest? Manifest { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-safe error message when <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }
    }
}
