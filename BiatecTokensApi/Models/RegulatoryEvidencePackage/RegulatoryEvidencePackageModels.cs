namespace BiatecTokensApi.Models.RegulatoryEvidencePackage
{
    // ── Audience profile enum ─────────────────────────────────────────────────

    /// <summary>
    /// Audience profile that governs framing and redaction rules for the evidence package.
    /// Selection influences summary framing only; it never alters authoritative record integrity.
    /// </summary>
    public enum RegulatoryAudienceProfile
    {
        /// <summary>Full operational detail, including remediation steps and internal reviewer notes.</summary>
        InternalCompliance,
        /// <summary>Executive-level summary with key status indicators; redacts low-level technical detail.</summary>
        ExecutiveSignOff,
        /// <summary>Evidence detail without internal operational notes; suitable for third-party auditors.</summary>
        ExternalAuditor,
        /// <summary>Strict canonical package with complete evidence, source provenance, and rationale; no redactions.</summary>
        RegulatorReview
    }

    // ── Readiness and package status enums ───────────────────────────────────

    /// <summary>Top-level readiness determination for a regulatory evidence package.</summary>
    public enum RegPackageReadinessStatus
    {
        /// <summary>All required evidence is present, current, and passes validation. Package is regulator-ready.</summary>
        Ready,
        /// <summary>One or more hard blockers prevent the package from being considered compliant.</summary>
        Blocked,
        /// <summary>Evidence is complete but requires manual review before a final determination.</summary>
        RequiresReview,
        /// <summary>One or more required evidence sources are missing; readiness cannot be established.</summary>
        Incomplete,
        /// <summary>Evidence was previously sufficient but has since become stale or expired.</summary>
        Stale
    }

    /// <summary>Lifecycle status of the evidence package itself.</summary>
    public enum RegPackageStatus
    {
        /// <summary>Package creation is in progress; not yet available for retrieval.</summary>
        Pending,
        /// <summary>Package has been fully assembled and is available for retrieval.</summary>
        Assembled,
        /// <summary>Package has been archived; still retrievable but no longer current.</summary>
        Archived,
        /// <summary>Package has been invalidated due to evidence changes; requires regeneration.</summary>
        Invalidated
    }

    // ── Evidence source kind enum ─────────────────────────────────────────────

    /// <summary>Category of source record included in an evidence package manifest.</summary>
    public enum RegEvidenceSourceKind
    {
        /// <summary>KYC identity verification decision record.</summary>
        KycDecision,
        /// <summary>AML / sanctions screening decision record.</summary>
        AmlDecision,
        /// <summary>Compliance case management record.</summary>
        ComplianceCase,
        /// <summary>Approval workflow stage decision.</summary>
        ApprovalWorkflow,
        /// <summary>Manual attestation or reviewer note.</summary>
        ManualAttestation,
        /// <summary>External document or regulatory reference.</summary>
        ExternalDocument,
        /// <summary>Launch readiness or compliance evidence launch decision.</summary>
        LaunchDecision
    }

    /// <summary>Availability / freshness status of a specific evidence source in the manifest.</summary>
    public enum RegSourceAvailability
    {
        /// <summary>Source record is present and current.</summary>
        Available,
        /// <summary>Source record is present but approaching its expiry window.</summary>
        NearingExpiry,
        /// <summary>Source record has exceeded its validity window and is stale.</summary>
        Stale,
        /// <summary>Source record is required but was not found.</summary>
        Missing,
        /// <summary>Source record exists but failed validation checks.</summary>
        Invalid,
        /// <summary>Source system was unreachable at package assembly time.</summary>
        ProviderUnavailable
    }

    /// <summary>Severity of a missing or stale data gap in the evidence package.</summary>
    public enum RegMissingDataSeverity
    {
        /// <summary>Gap is informational; does not affect readiness.</summary>
        Advisory,
        /// <summary>Gap is significant; may affect readiness under stricter rules.</summary>
        Warning,
        /// <summary>Gap is critical; directly downgrades package readiness.</summary>
        Blocker
    }

    // ── Manifest entry ────────────────────────────────────────────────────────

    /// <summary>
    /// A single entry in the package evidence manifest describing one source record.
    /// </summary>
    public class RegEvidenceSourceEntry
    {
        /// <summary>Stable identifier of this source record.</summary>
        public string SourceId { get; set; } = string.Empty;

        /// <summary>Kind of source record.</summary>
        public RegEvidenceSourceKind Kind { get; set; }

        /// <summary>Human-readable display name for this source.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>System or provider that originated this record.</summary>
        public string OriginSystem { get; set; } = string.Empty;

        /// <summary>UTC timestamp when this source record was collected or generated.</summary>
        public DateTime CollectedAt { get; set; }

        /// <summary>UTC timestamp when this source record expires (null = no expiry).</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Availability / freshness status of this source.</summary>
        public RegSourceAvailability Availability { get; set; }

        /// <summary>
        /// Whether this source is required for package completeness.
        /// Missing required sources downgrade readiness to Incomplete or Blocked.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>Brief description of what this source record represents.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>SHA-256 data integrity hash of the source record payload (where computable).</summary>
        public string? DataHash { get; set; }

        /// <summary>Additional machine-readable metadata (non-sensitive key-value pairs).</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    // ── KYC/AML summary ───────────────────────────────────────────────────────

    /// <summary>
    /// Compact summary of KYC identity and AML sanctions decisions for a subject,
    /// as evaluated at package generation time.
    /// </summary>
    public class RegKycAmlSummary
    {
        /// <summary>Subject identifier to which this summary applies.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Overall KYC decision status at summary time.</summary>
        public string KycStatus { get; set; } = string.Empty;

        /// <summary>UTC timestamp of the most recent KYC decision.</summary>
        public DateTime? KycDecidedAt { get; set; }

        /// <summary>Provider or system that issued the KYC decision.</summary>
        public string? KycProvider { get; set; }

        /// <summary>UTC timestamp when the KYC decision expires (null = no expiry).</summary>
        public DateTime? KycExpiresAt { get; set; }

        /// <summary>Overall AML/sanctions screening status at summary time.</summary>
        public string AmlStatus { get; set; } = string.Empty;

        /// <summary>UTC timestamp of the most recent AML decision.</summary>
        public DateTime? AmlDecidedAt { get; set; }

        /// <summary>Provider or system that issued the AML decision.</summary>
        public string? AmlProvider { get; set; }

        /// <summary>UTC timestamp when the AML decision expires (null = no expiry).</summary>
        public DateTime? AmlExpiresAt { get; set; }

        /// <summary>Whether the subject passed all identity and sanctions checks.</summary>
        public bool PassedAllChecks { get; set; }

        /// <summary>Human-readable summary of the KYC/AML posture for this subject.</summary>
        public string PostureSummary { get; set; } = string.Empty;

        /// <summary>Confidence score from the most recent AML screening (0–100, null if unavailable).</summary>
        public double? AmlConfidenceScore { get; set; }

        /// <summary>Watchlist categories matched during AML screening (empty if no hits).</summary>
        public List<string> MatchedWatchlistCategories { get; set; } = new();
    }

    // ── Contradiction summary ─────────────────────────────────────────────────

    /// <summary>
    /// A single contradiction between two or more compliance decisions for the same subject and check type.
    /// Unresolved contradictions downgrade readiness to Blocked.
    /// </summary>
    public class RegContradictionItem
    {
        /// <summary>Stable contradiction identifier.</summary>
        public string ContradictionId { get; set; } = string.Empty;

        /// <summary>The check type where the contradiction was detected (e.g., "IdentityKyc", "AmlSanctions").</summary>
        public string CheckType { get; set; } = string.Empty;

        /// <summary>IDs of the source records that contradict each other.</summary>
        public List<string> ConflictingSourceIds { get; set; } = new();

        /// <summary>Human-readable description of the contradiction.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the contradiction was detected.</summary>
        public DateTime DetectedAt { get; set; }

        /// <summary>Whether the contradiction has been reviewed and resolved.</summary>
        public bool IsResolved { get; set; }

        /// <summary>UTC timestamp when the contradiction was resolved (null if unresolved).</summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>Reviewer who resolved the contradiction (null if unresolved).</summary>
        public string? ResolvedBy { get; set; }

        /// <summary>Resolution note or outcome explanation (null if unresolved).</summary>
        public string? ResolutionNote { get; set; }
    }

    // ── Remediation summary ───────────────────────────────────────────────────

    /// <summary>A single remediation item required to achieve or restore package readiness.</summary>
    public class RegRemediationItem
    {
        /// <summary>Stable remediation item identifier.</summary>
        public string RemediationId { get; set; } = string.Empty;

        /// <summary>Short title of the required action.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed description of the required remediation.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Severity: how critically this item affects readiness.</summary>
        public RegMissingDataSeverity Severity { get; set; }

        /// <summary>IDs of source records or package sections related to this item.</summary>
        public List<string> RelatedSourceIds { get; set; } = new();

        /// <summary>Ordered step-by-step instructions to resolve this item.</summary>
        public List<string> RemediationSteps { get; set; } = new();

        /// <summary>Suggested owner team (e.g., "compliance", "operations").</summary>
        public string? OwnerHint { get; set; }

        /// <summary>Whether this item has been resolved.</summary>
        public bool IsResolved { get; set; }

        /// <summary>UTC timestamp when resolved (null if open).</summary>
        public DateTime? ResolvedAt { get; set; }
    }

    // ── Approval history ──────────────────────────────────────────────────────

    /// <summary>A single approval or rejection event from the approval workflow history.</summary>
    public class RegApprovalHistoryEntry
    {
        /// <summary>Stable entry identifier.</summary>
        public string EntryId { get; set; } = string.Empty;

        /// <summary>Stage name or identifier (e.g., "ComplianceReview", "ExecutiveSignOff").</summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>Decision outcome: Approved, Rejected, Escalated, NeedsMoreEvidence, etc.</summary>
        public string Decision { get; set; } = string.Empty;

        /// <summary>Actor (user or system) who made the decision.</summary>
        public string DecidedBy { get; set; } = string.Empty;

        /// <summary>UTC timestamp of the decision.</summary>
        public DateTime DecidedAt { get; set; }

        /// <summary>Human-readable rationale for the decision.</summary>
        public string Rationale { get; set; } = string.Empty;

        /// <summary>Whether this was the most recent decision for this stage.</summary>
        public bool IsLatestForStage { get; set; }
    }

    // ── Readiness posture transitions ─────────────────────────────────────────

    /// <summary>A single transition in the package's readiness posture over time.</summary>
    public class RegReadinessPostureTransition
    {
        /// <summary>Readiness status before the transition.</summary>
        public RegPackageReadinessStatus FromStatus { get; set; }

        /// <summary>Readiness status after the transition.</summary>
        public RegPackageReadinessStatus ToStatus { get; set; }

        /// <summary>UTC timestamp of the transition.</summary>
        public DateTime TransitionedAt { get; set; }

        /// <summary>Human-readable reason for the transition.</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>The source event that triggered the transition.</summary>
        public string TriggerEvent { get; set; } = string.Empty;
    }

    // ── Readiness rationale ───────────────────────────────────────────────────

    /// <summary>
    /// Authoritative, human-readable explanation of why the package has its current readiness status.
    /// Clients can answer "Is this regulator-ready? If not, why not?" from this structure alone.
    /// </summary>
    public class RegReadinessRationale
    {
        /// <summary>Single-sentence headline explanation.</summary>
        public string Headline { get; set; } = string.Empty;

        /// <summary>Detailed paragraph-level explanation.</summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>IDs of blocking sources or remediation items driving this status.</summary>
        public List<string> BlockingSourceIds { get; set; } = new();

        /// <summary>IDs of missing required sources that caused readiness downgrade.</summary>
        public List<string> MissingRequiredSourceIds { get; set; } = new();

        /// <summary>IDs of stale sources that caused readiness downgrade.</summary>
        public List<string> StaleSourceIds { get; set; } = new();

        /// <summary>IDs of unresolved contradictions contributing to the status.</summary>
        public List<string> UnresolvedContradictionIds { get; set; } = new();

        /// <summary>Ordered list of next actions to reach Ready status.</summary>
        public List<string> RecommendedNextSteps { get; set; } = new();
    }

    // ── Package manifest ──────────────────────────────────────────────────────

    /// <summary>
    /// Manifest describing the composition of a regulatory evidence package:
    /// what was included, what was omitted, and why.
    /// </summary>
    public class RegPackageManifest
    {
        /// <summary>Total number of source records included in this package.</summary>
        public int TotalSourceRecords { get; set; }

        /// <summary>Number of source records that are available and current.</summary>
        public int AvailableSourceCount { get; set; }

        /// <summary>Number of required source records that are missing.</summary>
        public int MissingRequiredCount { get; set; }

        /// <summary>Number of source records that are stale or expired.</summary>
        public int StaleSourceCount { get; set; }

        /// <summary>Number of provider-unavailable source records.</summary>
        public int UnavailableSourceCount { get; set; }

        /// <summary>All source entries (present, stale, missing, or unavailable).</summary>
        public List<RegEvidenceSourceEntry> Sources { get; set; } = new();

        /// <summary>Audience profile applied during package assembly.</summary>
        public RegulatoryAudienceProfile AudienceProfile { get; set; }

        /// <summary>Rules applied to this audience profile (e.g., "InternalNotes=Included", "TechnicalDetail=Redacted").</summary>
        public List<string> AppliedAudienceRules { get; set; } = new();

        /// <summary>Schema version of this manifest format.</summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>UTC timestamp when this manifest was assembled.</summary>
        public DateTime AssembledAt { get; set; }

        /// <summary>
        /// SHA-256 hash of the canonical package payload for downstream integrity verification.
        /// </summary>
        public string? PayloadHash { get; set; }
    }

    // ── Request models ────────────────────────────────────────────────────────

    /// <summary>Request to create (assemble) a new regulatory evidence package.</summary>
    public class CreateRegulatoryEvidencePackageRequest
    {
        /// <summary>Subject or issuer identifier for whom the package is being assembled.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Audience profile that governs framing and detail level.</summary>
        public RegulatoryAudienceProfile AudienceProfile { get; set; } = RegulatoryAudienceProfile.InternalCompliance;

        /// <summary>
        /// Optional idempotency key. If an existing package with the same key exists for this
        /// subject/audience pair, the cached package is returned without regeneration.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>Optional: limit evidence assembly to records created after this timestamp.</summary>
        public DateTime? EvidenceFromTimestamp { get; set; }

        /// <summary>Optional: free-text notes from the requestor (included in package for regulator profiles).</summary>
        public string? RequestorNotes { get; set; }

        /// <summary>Correlation ID for end-to-end request tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>When true, forces regeneration of the package even if a cached result exists.</summary>
        public bool ForceRegenerate { get; set; }
    }

    // ── Summary model (lightweight, UI-facing) ────────────────────────────────

    /// <summary>
    /// Lightweight package summary for UI workflows (evidence package preview, approval dashboards).
    /// Does not include detailed evidence payloads; see <see cref="RegulatoryEvidencePackageDetail"/>
    /// for canonical export-grade content.
    /// </summary>
    public class RegulatoryEvidencePackageSummary
    {
        /// <summary>Stable package identifier; use this for canonical retrieval.</summary>
        public string PackageId { get; set; } = string.Empty;

        /// <summary>Subject identifier.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Audience profile applied during assembly.</summary>
        public RegulatoryAudienceProfile AudienceProfile { get; set; }

        /// <summary>Lifecycle status of the package.</summary>
        public RegPackageStatus PackageStatus { get; set; }

        /// <summary>Readiness determination at package generation time.</summary>
        public RegPackageReadinessStatus ReadinessStatus { get; set; }

        /// <summary>Single-sentence readiness headline (from <see cref="RegReadinessRationale.Headline"/>).</summary>
        public string ReadinessHeadline { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the package was generated.</summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>UTC timestamp when the package expires or should be regenerated (null = no expiry).</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Total number of source records included.</summary>
        public int TotalSourceRecords { get; set; }

        /// <summary>Number of missing required source records (0 = complete).</summary>
        public int MissingRequiredCount { get; set; }

        /// <summary>Number of stale source records.</summary>
        public int StaleSourceCount { get; set; }

        /// <summary>Number of open (unresolved) contradictions.</summary>
        public int OpenContradictionCount { get; set; }

        /// <summary>Number of open remediation items.</summary>
        public int OpenRemediationCount { get; set; }

        /// <summary>Whether the package has approval history entries.</summary>
        public bool HasApprovalHistory { get; set; }

        /// <summary>Schema version.</summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>Correlation ID from the originating request.</summary>
        public string? CorrelationId { get; set; }
    }

    // ── Canonical detail model (export/archival) ──────────────────────────────

    /// <summary>
    /// Canonical regulatory evidence package with full detail for export, archival, and regulator submission.
    /// Distinguishes authoritative records from derived summaries, and explicitly identifies missing or stale data.
    /// </summary>
    public class RegulatoryEvidencePackageDetail
    {
        /// <summary>Stable package identifier.</summary>
        public string PackageId { get; set; } = string.Empty;

        /// <summary>Subject identifier.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Audience profile applied during assembly.</summary>
        public RegulatoryAudienceProfile AudienceProfile { get; set; }

        /// <summary>Lifecycle status of the package.</summary>
        public RegPackageStatus PackageStatus { get; set; }

        /// <summary>Readiness determination.</summary>
        public RegPackageReadinessStatus ReadinessStatus { get; set; }

        /// <summary>Authoritative rationale for the readiness determination.</summary>
        public RegReadinessRationale ReadinessRationale { get; set; } = new();

        /// <summary>UTC timestamp when the package was generated.</summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>UTC timestamp when the package expires (null = no expiry).</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Manifest describing all source records included or identified as absent.</summary>
        public RegPackageManifest Manifest { get; set; } = new();

        /// <summary>KYC/AML decision summary for the subject.</summary>
        public RegKycAmlSummary KycAmlSummary { get; set; } = new();

        /// <summary>All detected contradictions (resolved and unresolved).</summary>
        public List<RegContradictionItem> Contradictions { get; set; } = new();

        /// <summary>Open and resolved remediation items.</summary>
        public List<RegRemediationItem> RemediationItems { get; set; } = new();

        /// <summary>Chronological approval workflow history.</summary>
        public List<RegApprovalHistoryEntry> ApprovalHistory { get; set; } = new();

        /// <summary>Chronological log of readiness posture transitions.</summary>
        public List<RegReadinessPostureTransition> PostureTransitions { get; set; } = new();

        /// <summary>Optional requestor notes included in the package.</summary>
        public string? RequestorNotes { get; set; }

        /// <summary>Schema version for response contract stability.</summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>Correlation ID from the originating request.</summary>
        public string? CorrelationId { get; set; }
    }

    // ── Response models ───────────────────────────────────────────────────────

    /// <summary>Response from a package creation request.</summary>
    public class CreateRegulatoryEvidencePackageResponse
    {
        /// <summary>Whether the package was successfully created or retrieved from cache.</summary>
        public bool Success { get; set; } = true;

        /// <summary>The assembled package summary.</summary>
        public RegulatoryEvidencePackageSummary? Package { get; set; }

        /// <summary>Whether this result was served from an idempotency cache (not regenerated).</summary>
        public bool IsIdempotentReplay { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-safe error message when <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response from a package summary retrieval request.</summary>
    public class GetPackageSummaryResponse
    {
        /// <summary>Whether the retrieval succeeded.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Package summary payload.</summary>
        public RegulatoryEvidencePackageSummary? Package { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-safe error message when <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response from a canonical package detail retrieval request.</summary>
    public class GetPackageDetailResponse
    {
        /// <summary>Whether the retrieval succeeded.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Canonical package detail payload.</summary>
        public RegulatoryEvidencePackageDetail? Package { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-safe error message when <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Response from a list packages request.</summary>
    public class ListEvidencePackagesResponse
    {
        /// <summary>Whether the retrieval succeeded.</summary>
        public bool Success { get; set; } = true;

        /// <summary>List of package summaries ordered by GeneratedAt descending.</summary>
        public List<RegulatoryEvidencePackageSummary> Packages { get; set; } = new();

        /// <summary>Total number of packages available for this subject (before limit).</summary>
        public int TotalCount { get; set; }

        /// <summary>Error code when <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-safe error message when <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }
    }
}
