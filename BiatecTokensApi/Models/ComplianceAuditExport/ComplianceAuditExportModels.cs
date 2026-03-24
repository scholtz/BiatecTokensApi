using BiatecTokensApi.Models.RegulatoryEvidencePackage;

namespace BiatecTokensApi.Models.ComplianceAuditExport
{
    // ── Scenario type ─────────────────────────────────────────────────────────

    /// <summary>
    /// The scenario type that drives evidence assembly for a compliance audit export package.
    /// Each scenario collects and weights evidence differently.
    /// </summary>
    public enum AuditScenario
    {
        /// <summary>
        /// Assembles evidence for a release-readiness sign-off decision.
        /// Focuses on protected sign-off records, KYC/AML posture, and launch-grade evidence.
        /// </summary>
        ReleaseReadinessSignOff,

        /// <summary>
        /// Assembles evidence for a KYC/AML onboarding case review.
        /// Focuses on case lifecycle, provider-check outcomes, and reviewer actions.
        /// </summary>
        OnboardingCaseReview,

        /// <summary>
        /// Assembles a compliance blocker review showing open and resolved blockers with
        /// severity distribution and remediation hints.
        /// </summary>
        ComplianceBlockerReview,

        /// <summary>
        /// Exports the complete approval workflow history for audit trail purposes.
        /// Focuses on approval stage decisions, decision actors, and workflow completion status.
        /// </summary>
        ApprovalHistoryExport
    }

    // ── Export readiness ──────────────────────────────────────────────────────

    /// <summary>
    /// Top-level readiness classification for a compliance audit export package.
    /// Readiness is fail-closed: any missing, stale, provider-unavailable, or unverified evidence
    /// downgrades the status explicitly rather than silently passing incomplete packages.
    /// </summary>
    public enum AuditExportReadiness
    {
        /// <summary>
        /// Package is complete, evidence is current, and all scenario-specific checks passed.
        /// Suitable for regulator-grade submission.
        /// </summary>
        Ready,

        /// <summary>One or more hard blockers prevent a positive readiness determination.</summary>
        Blocked,

        /// <summary>Evidence is present but manual review is required before final determination.</summary>
        RequiresReview,

        /// <summary>One or more required evidence sources are missing.</summary>
        Incomplete,

        /// <summary>Evidence was previously sufficient but has since become stale or expired.</summary>
        Stale,

        /// <summary>
        /// Evidence is partially available but does not satisfy regulator-grade requirements.
        /// </summary>
        PartiallyAvailable,

        /// <summary>
        /// A provider or data source was unreachable; evidence could not be fully assembled.
        /// </summary>
        DegradedProviderUnavailable
    }

    // ── Blocker severity ──────────────────────────────────────────────────────

    /// <summary>Severity of a compliance audit export blocker item.</summary>
    public enum AuditBlockerSeverity
    {
        /// <summary>Informational; does not prevent readiness.</summary>
        Advisory,

        /// <summary>Significant; may affect readiness under stricter rules.</summary>
        Warning,

        /// <summary>Critical; directly prevents regulator-ready or release-grade classification.</summary>
        Critical
    }

    // ── Evidence freshness ────────────────────────────────────────────────────

    /// <summary>Freshness state of a specific evidence record in a compliance audit export.</summary>
    public enum AuditEvidenceFreshness
    {
        /// <summary>Evidence is current and within the required freshness window.</summary>
        Fresh,

        /// <summary>Evidence is approaching its expiry window (within 7 days).</summary>
        NearingExpiry,

        /// <summary>Evidence has exceeded its validity window and is stale.</summary>
        Stale,

        /// <summary>Evidence is absent; could not be located for this subject.</summary>
        Missing,

        /// <summary>Evidence exists but failed validation checks.</summary>
        Invalid,

        /// <summary>Source system was unreachable at export assembly time.</summary>
        ProviderUnavailable
    }

    // ── Provenance record ─────────────────────────────────────────────────────

    /// <summary>
    /// Provenance record identifying the origin and freshness state of a single piece of evidence
    /// included in a compliance audit export package.
    /// Enables downstream consumers to trace each evidence item back to its authoritative source.
    /// </summary>
    public class AuditEvidenceProvenance
    {
        /// <summary>Stable identifier for this provenance record.</summary>
        public string ProvenanceId { get; set; } = string.Empty;

        /// <summary>Source system or provider that originated this evidence record.</summary>
        public string SourceSystem { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable category of evidence (e.g., "KYC Identity Verification", "AML Screening",
        /// "Compliance Case", "Approval Workflow", "Launch Decision").
        /// </summary>
        public string EvidenceCategory { get; set; } = string.Empty;

        /// <summary>UTC timestamp when this evidence was captured or generated.</summary>
        public DateTime CapturedAt { get; set; }

        /// <summary>UTC timestamp when this evidence expires; null if it does not expire.</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Freshness state of this evidence at export assembly time.</summary>
        public AuditEvidenceFreshness FreshnessState { get; set; }

        /// <summary>Whether this evidence source is required for the scenario readiness determination.</summary>
        public bool IsRequired { get; set; }

        /// <summary>SHA-256 integrity hash of the evidence payload (null if not computable).</summary>
        public string? IntegrityHash { get; set; }

        /// <summary>
        /// External reference ID of the source record (e.g., KYC case ID, compliance case ID).
        /// </summary>
        public string? ExternalReferenceId { get; set; }

        /// <summary>Brief description of what this evidence represents.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Environment label where this evidence was collected (e.g., "production", "staging").</summary>
        public string? EnvironmentLabel { get; set; }
    }

    // ── Blocker item ──────────────────────────────────────────────────────────

    /// <summary>
    /// A single blocker item preventing regulator-ready or release-grade classification.
    /// Critical-severity blockers must all be resolved before the package can be considered Ready.
    /// Clients must never suppress or override blocker items when presenting export status.
    /// </summary>
    public class AuditExportBlocker
    {
        /// <summary>Stable blocker identifier.</summary>
        public string BlockerId { get; set; } = string.Empty;

        /// <summary>Short title of the blocker.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed description of the blocker and its impact on readiness.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Severity of this blocker.</summary>
        public AuditBlockerSeverity Severity { get; set; }

        /// <summary>
        /// Category of blocker (e.g., "MissingEvidence", "StaleEvidence",
        /// "UnresolvedContradiction", "ProviderUnavailable", "ApprovalPending").
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Provenance IDs of evidence records contributing to this blocker.</summary>
        public List<string> RelatedProvenanceIds { get; set; } = new();

        /// <summary>Ordered remediation hints to resolve this blocker.</summary>
        public List<string> RemediationHints { get; set; } = new();

        /// <summary>Suggested owner team (e.g., "compliance", "operations", "legal").</summary>
        public string? OwnerTeam { get; set; }

        /// <summary>Whether this blocker has been resolved.</summary>
        public bool IsResolved { get; set; }

        /// <summary>UTC timestamp when this blocker was resolved (null if still open).</summary>
        public DateTime? ResolvedAt { get; set; }
    }

    // ── Scenario-specific sections ────────────────────────────────────────────

    /// <summary>
    /// Release-readiness section of a compliance audit export package.
    /// Summarises protected sign-off evidence, release-grade determination, and any
    /// deployment blockers that must be resolved before a production release can proceed.
    /// </summary>
    public class AuditReleaseReadinessSection
    {
        /// <summary>Head reference or version being evaluated (e.g., git commit SHA, release tag).</summary>
        public string? HeadRef { get; set; }

        /// <summary>Environment label under evaluation (e.g., "production", "staging").</summary>
        public string? EnvironmentLabel { get; set; }

        /// <summary>Whether the protected sign-off evidence is present and release-grade.</summary>
        public bool HasReleaseGradeSignOff { get; set; }

        /// <summary>UTC timestamp of the most recent sign-off record.</summary>
        public DateTime? SignOffCapturedAt { get; set; }

        /// <summary>
        /// Status of the most recent sign-off evaluation
        /// (e.g., "Ready", "Blocked", "Stale", "NotReleaseEvidence").
        /// </summary>
        public string? SignOffStatus { get; set; }

        /// <summary>Whether sign-off evidence is within the freshness window.</summary>
        public bool IsSignOffFresh { get; set; }

        /// <summary>
        /// Whether sign-off evidence is confirmed as release-grade
        /// (not a test artifact or non-grade record).
        /// </summary>
        public bool IsReleaseGradeEvidence { get; set; }

        /// <summary>
        /// List of release blocker descriptions that must be resolved before sign-off can be granted.
        /// Empty when <see cref="HasReleaseGradeSignOff"/> is true.
        /// </summary>
        public List<string> ReleaseBlockers { get; set; } = new();

        /// <summary>List of approval webhook event references confirming release approval (if any).</summary>
        public List<string> ApprovalWebhookEvents { get; set; } = new();

        /// <summary>
        /// Operator guidance for next steps when sign-off is not yet ready.
        /// Null when sign-off is Ready.
        /// </summary>
        public string? OperatorGuidance { get; set; }

        /// <summary>KYC/AML posture summary at release evaluation time.</summary>
        public string? KycAmlPostureSummary { get; set; }

        /// <summary>Whether KYC/AML checks have passed for this subject.</summary>
        public bool KycAmlChecksPassed { get; set; }
    }

    /// <summary>
    /// Onboarding case section of a compliance audit export package.
    /// Summarises the KYC/AML onboarding case lifecycle, provider-check outcomes,
    /// and reviewer actions for a subject's most relevant onboarding case.
    /// </summary>
    public class AuditOnboardingCaseSection
    {
        /// <summary>Onboarding case identifier.</summary>
        public string? CaseId { get; set; }

        /// <summary>
        /// Current state of the onboarding case
        /// (e.g., "Approved", "UnderReview", "ProviderUnavailable", "ConfigurationMissing").
        /// </summary>
        public string? CaseState { get; set; }

        /// <summary>UTC timestamp when the case was initiated.</summary>
        public DateTime? CaseInitiatedAt { get; set; }

        /// <summary>UTC timestamp of the most recent case state transition.</summary>
        public DateTime? LastTransitionAt { get; set; }

        /// <summary>Whether provider-backed KYC/AML checks have been completed.</summary>
        public bool ProviderChecksCompleted { get; set; }

        /// <summary>
        /// Provider availability status at evidence assembly time
        /// (e.g., "Available", "Unavailable", "ConfigurationMissing").
        /// </summary>
        public string? ProviderAvailabilityStatus { get; set; }

        /// <summary>
        /// Human-readable summaries of evidence items submitted to the case.
        /// Each entry describes a single evidence record (type, captured date, status).
        /// </summary>
        public List<string> EvidenceSummaries { get; set; } = new();

        /// <summary>Whether the case is in a terminal state (Approved, Rejected, Expired).</summary>
        public bool IsInTerminalState { get; set; }

        /// <summary>Whether the case outcome supports a positive compliance determination.</summary>
        public bool SupportsPositiveDetermination { get; set; }

        /// <summary>Human-readable summary of the case posture for this subject.</summary>
        public string? CaseSummary { get; set; }

        /// <summary>
        /// List of reviewer action descriptions that have been taken on this case
        /// (e.g., "Approved by compliance-reviewer-001 on 2025-03-01").
        /// </summary>
        public List<string> ReviewerActions { get; set; } = new();
    }

    /// <summary>
    /// Compliance blocker review section of a compliance audit export package.
    /// Provides a severity-stratified view of all open and recently resolved compliance blockers,
    /// enabling operators and auditors to assess remediation progress.
    /// </summary>
    public class AuditBlockerReviewSection
    {
        /// <summary>Total number of open (unresolved) blockers.</summary>
        public int OpenBlockerCount { get; set; }

        /// <summary>Total number of resolved blockers included in this snapshot.</summary>
        public int ResolvedBlockerCount { get; set; }

        /// <summary>Number of Critical severity blockers currently open.</summary>
        public int CriticalOpenCount { get; set; }

        /// <summary>Number of Warning severity blockers currently open.</summary>
        public int WarningOpenCount { get; set; }

        /// <summary>Number of Advisory blockers currently open.</summary>
        public int AdvisoryOpenCount { get; set; }

        /// <summary>Blocker category distribution (category name → count of open blockers).</summary>
        public Dictionary<string, int> BlockersByCategory { get; set; } = new();

        /// <summary>List of open blocker items with full detail.</summary>
        public List<AuditExportBlocker> OpenBlockers { get; set; } = new();

        /// <summary>
        /// List of recently resolved blocker items for audit trail completeness.
        /// Present when the export request included resolved blockers.
        /// </summary>
        public List<AuditExportBlocker> RecentlyResolvedBlockers { get; set; } = new();

        /// <summary>Whether any critical blockers remain unresolved.</summary>
        public bool HasUnresolvedCriticalBlockers { get; set; }

        /// <summary>Governance summary explaining the blocker posture.</summary>
        public string? BlockerPostureSummary { get; set; }
    }

    /// <summary>
    /// Approval history section of a compliance audit export package.
    /// Captures the complete approval workflow history for audit-trail purposes,
    /// including stage decisions, decision actors, rationale, and workflow completion status.
    /// </summary>
    public class AuditApprovalHistorySection
    {
        /// <summary>Total number of approval decisions recorded.</summary>
        public int TotalDecisionCount { get; set; }

        /// <summary>UTC timestamp of the most recent approval decision.</summary>
        public DateTime? LatestDecisionAt { get; set; }

        /// <summary>
        /// Decision outcome of the most recent stage
        /// (e.g., "Approved", "Rejected", "NeedsMoreEvidence", "Escalated").
        /// </summary>
        public string? LatestDecisionOutcome { get; set; }

        /// <summary>Actor (user or system) who made the most recent decision.</summary>
        public string? LatestDecisionBy { get; set; }

        /// <summary>Whether the approval workflow has been fully completed (no pending stages).</summary>
        public bool IsWorkflowCompleted { get; set; }

        /// <summary>Whether any approval stage is pending manual review.</summary>
        public bool HasPendingReviewStage { get; set; }

        /// <summary>List of approval stages with their current status.</summary>
        public List<AuditApprovalStageEntry> Stages { get; set; } = new();

        /// <summary>Chronological list of all approval decisions (oldest first).</summary>
        public List<AuditApprovalDecisionEntry> DecisionHistory { get; set; } = new();

        /// <summary>Human-readable workflow posture summary for this subject.</summary>
        public string? WorkflowSummary { get; set; }
    }

    /// <summary>A single approval stage and its current outcome state.</summary>
    public class AuditApprovalStageEntry
    {
        /// <summary>Stage name or identifier (e.g., "ComplianceReview", "ExecutiveSignOff").</summary>
        public string StageName { get; set; } = string.Empty;

        /// <summary>Current decision outcome for this stage (null if not yet decided).</summary>
        public string? Outcome { get; set; }

        /// <summary>Whether this stage is the most recently active.</summary>
        public bool IsLatest { get; set; }

        /// <summary>UTC timestamp of the most recent decision for this stage (null if not yet decided).</summary>
        public DateTime? LastDecisionAt { get; set; }

        /// <summary>Actor who made the most recent decision for this stage (null if not yet decided).</summary>
        public string? LastDecisionBy { get; set; }
    }

    /// <summary>A single approval decision event in the workflow history.</summary>
    public class AuditApprovalDecisionEntry
    {
        /// <summary>Stable entry identifier.</summary>
        public string EntryId { get; set; } = string.Empty;

        /// <summary>Stage name (e.g., "ComplianceReview", "ExecutiveSignOff").</summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>
        /// Decision outcome
        /// (e.g., "Approved", "Rejected", "NeedsMoreEvidence", "Escalated").
        /// </summary>
        public string Decision { get; set; } = string.Empty;

        /// <summary>Actor (user or system) who made the decision.</summary>
        public string DecidedBy { get; set; } = string.Empty;

        /// <summary>UTC timestamp of the decision.</summary>
        public DateTime DecidedAt { get; set; }

        /// <summary>Human-readable rationale for the decision (null if not provided).</summary>
        public string? Rationale { get; set; }
    }

    // ── Canonical audit export package ────────────────────────────────────────

    /// <summary>
    /// Canonical compliance audit export package: a durable, scenario-specific evidence bundle
    /// assembled from compliance, KYC/AML, sign-off, and approval-history sources.
    ///
    /// Each package is:
    /// <list type="bullet">
    ///   <item><description>Identified by a stable <see cref="ExportId"/> (UUID) for re-identification and comparison.</description></item>
    ///   <item><description>Scenario-classified so downstream clients can route by use case.</description></item>
    ///   <item><description>Audience-aware so framing matches the target reviewer.</description></item>
    ///   <item><description>Fail-closed: missing, stale, or unverified evidence downgrades <see cref="Readiness"/> explicitly.</description></item>
    ///   <item><description>Integrity-protected by a <see cref="ContentHash"/> (SHA-256).</description></item>
    ///   <item><description>Traceable via <see cref="TrackerHistory"/> for append-only comparison over time.</description></item>
    /// </list>
    /// </summary>
    public class ComplianceAuditExportPackage
    {
        /// <summary>Stable export package identifier (UUID).</summary>
        public string ExportId { get; set; } = string.Empty;

        /// <summary>Subject or issuer identifier for whom this package was assembled.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Scenario type that drove evidence assembly.</summary>
        public AuditScenario Scenario { get; set; }

        /// <summary>Audience profile applied during assembly.</summary>
        public RegulatoryAudienceProfile AudienceProfile { get; set; }

        /// <summary>Readiness determination for this package (fail-closed).</summary>
        public AuditExportReadiness Readiness { get; set; }

        /// <summary>Single-sentence headline explaining the readiness determination.</summary>
        public string ReadinessHeadline { get; set; } = string.Empty;

        /// <summary>Detailed, paragraph-level explanation of the readiness determination.</summary>
        public string ReadinessDetail { get; set; } = string.Empty;

        /// <summary>UTC timestamp when this package was assembled.</summary>
        public DateTime AssembledAt { get; set; }

        /// <summary>
        /// UTC timestamp when this package expires or should be regenerated.
        /// Null indicates no expiry.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Environment label (e.g., "production", "staging") captured at assembly time.</summary>
        public string? EnvironmentLabel { get; set; }

        /// <summary>
        /// Head reference or version alignment string (e.g., git commit SHA, release tag)
        /// captured at assembly time.
        /// </summary>
        public string? HeadReference { get; set; }

        /// <summary>Schema version of this package format for contract stability.</summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>Policy version applied during readiness evaluation.</summary>
        public string PolicyVersion { get; set; } = string.Empty;

        /// <summary>Provenance records for each evidence source considered during assembly.</summary>
        public List<AuditEvidenceProvenance> ProvenanceRecords { get; set; } = new();

        /// <summary>
        /// All blocker items (open and resolved) detected during package assembly.
        /// Clients must never suppress or hide blocker items when presenting export status.
        /// </summary>
        public List<AuditExportBlocker> Blockers { get; set; } = new();

        /// <summary>Open (unresolved) blockers only — convenience view of <see cref="Blockers"/>.</summary>
        public List<AuditExportBlocker> OpenBlockers => Blockers.Where(b => !b.IsResolved).ToList();

        /// <summary>Number of open critical blockers. Non-zero → package is not regulator-ready.</summary>
        public int CriticalBlockerCount => Blockers.Count(b => !b.IsResolved && b.Severity == AuditBlockerSeverity.Critical);

        /// <summary>
        /// Whether this package is suitable for regulator-grade submission.
        /// True only when <see cref="Readiness"/> is <see cref="AuditExportReadiness.Ready"/>.
        /// </summary>
        public bool IsRegulatorReady => Readiness == AuditExportReadiness.Ready;

        /// <summary>
        /// Whether this package is release-grade.
        /// True when Readiness is Ready or RequiresReview (human sign-off pending).
        /// False for all other readiness states.
        /// </summary>
        public bool IsReleaseGrade =>
            Readiness == AuditExportReadiness.Ready ||
            Readiness == AuditExportReadiness.RequiresReview;

        /// <summary>Correlation ID for end-to-end request tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Optional requestor notes included in the package.</summary>
        public string? RequestorNotes { get; set; }

        /// <summary>
        /// SHA-256 integrity hash of the canonical package content.
        /// Computed at assembly time; changes if evidence content changes between assemblies.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Ordered list of export IDs that preceded this package for the same subject and scenario.
        /// Enables append-only history comparison of evidence posture over time.
        /// The most recent prior export ID is first; the oldest is last.
        /// </summary>
        public List<string> TrackerHistory { get; set; } = new();

        /// <summary>
        /// Release-readiness section.
        /// Populated when <see cref="Scenario"/> is <see cref="AuditScenario.ReleaseReadinessSignOff"/>.
        /// </summary>
        public AuditReleaseReadinessSection? ReleaseReadiness { get; set; }

        /// <summary>
        /// Onboarding case section.
        /// Populated when <see cref="Scenario"/> is <see cref="AuditScenario.OnboardingCaseReview"/>.
        /// </summary>
        public AuditOnboardingCaseSection? OnboardingCase { get; set; }

        /// <summary>
        /// Compliance blocker review section.
        /// Populated when <see cref="Scenario"/> is <see cref="AuditScenario.ComplianceBlockerReview"/>.
        /// </summary>
        public AuditBlockerReviewSection? BlockerReview { get; set; }

        /// <summary>
        /// Approval history section.
        /// Populated when <see cref="Scenario"/> is <see cref="AuditScenario.ApprovalHistoryExport"/>.
        /// </summary>
        public AuditApprovalHistorySection? ApprovalHistory { get; set; }
    }

    // ── Request models ────────────────────────────────────────────────────────

    /// <summary>Base request for assembling a compliance audit export package.</summary>
    public class ComplianceAuditExportBaseRequest
    {
        /// <summary>Subject or issuer identifier for whom the package is being assembled.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>
        /// Audience profile governing framing and detail level.
        /// Defaults to <see cref="RegulatoryAudienceProfile.InternalCompliance"/>.
        /// </summary>
        public RegulatoryAudienceProfile AudienceProfile { get; set; } =
            RegulatoryAudienceProfile.InternalCompliance;

        /// <summary>Optional idempotency key to prevent duplicate package generation.</summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>Optional: limit evidence to records collected after this timestamp.</summary>
        public DateTime? EvidenceFromTimestamp { get; set; }

        /// <summary>Optional requestor notes to include in the package.</summary>
        public string? RequestorNotes { get; set; }

        /// <summary>Correlation ID for end-to-end request tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>When true, forces regeneration even if a cached result exists for the idempotency key.</summary>
        public bool ForceRegenerate { get; set; }

        /// <summary>Optional environment label to record in the package (e.g., "production", "staging").</summary>
        public string? EnvironmentLabel { get; set; }
    }

    /// <summary>Request to assemble a release-readiness sign-off audit export.</summary>
    public class ReleaseReadinessExportRequest : ComplianceAuditExportBaseRequest
    {
        /// <summary>Head reference or version being evaluated (e.g., git commit SHA, release tag).</summary>
        public string? HeadRef { get; set; }

        /// <summary>Environment label to evaluate against (e.g., "production", "staging").</summary>
        public string? EnvironmentLabel { get; set; }
    }

    /// <summary>Request to assemble an onboarding case review audit export.</summary>
    public class OnboardingCaseReviewExportRequest : ComplianceAuditExportBaseRequest
    {
        /// <summary>
        /// Optional specific case ID to focus the review on.
        /// If omitted, the most recent onboarding case for the subject is used.
        /// </summary>
        public string? CaseId { get; set; }
    }

    /// <summary>Request to assemble a compliance blocker review audit export.</summary>
    public class ComplianceBlockerReviewExportRequest : ComplianceAuditExportBaseRequest
    {
        /// <summary>
        /// When true (default), includes recently resolved blockers for historical completeness.
        /// </summary>
        public bool IncludeResolvedBlockers { get; set; } = true;
    }

    /// <summary>Request to assemble an approval-history audit export.</summary>
    public class ApprovalHistoryExportRequest : ComplianceAuditExportBaseRequest
    {
        /// <summary>Maximum number of approval decisions to include (1–200, default 100).</summary>
        public int DecisionLimit { get; set; } = 100;
    }

    // ── Response models ───────────────────────────────────────────────────────

    /// <summary>Response from a compliance audit export package assembly request.</summary>
    public class ComplianceAuditExportResponse
    {
        /// <summary>Whether the package was successfully assembled.</summary>
        public bool Success { get; set; } = true;

        /// <summary>The assembled compliance audit export package.</summary>
        public ComplianceAuditExportPackage? Package { get; set; }

        /// <summary>Whether this result was served from an idempotency cache (not regenerated).</summary>
        public bool IsIdempotentReplay { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-safe error message when <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response from a compliance audit export package retrieval request.</summary>
    public class GetComplianceAuditExportResponse
    {
        /// <summary>Whether the retrieval succeeded.</summary>
        public bool Success { get; set; } = true;

        /// <summary>The retrieved compliance audit export package.</summary>
        public ComplianceAuditExportPackage? Package { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-safe error message when <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response from a list compliance audit export packages request.</summary>
    public class ListComplianceAuditExportsResponse
    {
        /// <summary>Whether the retrieval succeeded.</summary>
        public bool Success { get; set; } = true;

        /// <summary>List of export package summaries ordered by AssembledAt descending.</summary>
        public List<ComplianceAuditExportSummary> Exports { get; set; } = new();

        /// <summary>Total number of exports available for this subject/scenario (before limit).</summary>
        public int TotalCount { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-safe error message when <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Lightweight summary of a compliance audit export package for list and history views.
    /// Does not include the full scenario-specific section payload.
    /// </summary>
    public class ComplianceAuditExportSummary
    {
        /// <summary>Stable export identifier.</summary>
        public string ExportId { get; set; } = string.Empty;

        /// <summary>Subject identifier.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Scenario type.</summary>
        public AuditScenario Scenario { get; set; }

        /// <summary>Audience profile applied.</summary>
        public RegulatoryAudienceProfile AudienceProfile { get; set; }

        /// <summary>Readiness determination at assembly time.</summary>
        public AuditExportReadiness Readiness { get; set; }

        /// <summary>Readiness headline.</summary>
        public string ReadinessHeadline { get; set; } = string.Empty;

        /// <summary>UTC timestamp when assembled.</summary>
        public DateTime AssembledAt { get; set; }

        /// <summary>UTC timestamp when this export expires (null = no expiry).</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Number of open critical blockers.</summary>
        public int CriticalBlockerCount { get; set; }

        /// <summary>Total number of open blockers (any severity).</summary>
        public int TotalOpenBlockerCount { get; set; }

        /// <summary>Number of provenance records included in the full package.</summary>
        public int ProvenanceRecordCount { get; set; }

        /// <summary>Whether this package is regulator-ready.</summary>
        public bool IsRegulatorReady { get; set; }

        /// <summary>Content hash for integrity verification.</summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>Correlation ID from the originating request.</summary>
        public string? CorrelationId { get; set; }
    }
}
