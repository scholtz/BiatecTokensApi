namespace BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence
{
    // ══════════════════════════════════════════════════════════════════════════
    // Enumerations
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Describes the freshness state of a protected sign-off evidence package
    /// relative to the current release head.
    /// </summary>
    public enum SignOffEvidenceFreshnessStatus
    {
        /// <summary>
        /// Evidence exists for the current head commit, all checks passed, and
        /// nothing has expired. The evidence is authoritative for release decisions.
        /// </summary>
        Complete,

        /// <summary>
        /// Evidence exists but one or more evidence items are missing, incomplete,
        /// or pending. The evidence pack is not yet sufficient for release gating.
        /// </summary>
        Partial,

        /// <summary>
        /// Evidence was captured for the current head but has since passed its
        /// freshness window. The evidence must be refreshed before it can gate release.
        /// </summary>
        Stale,

        /// <summary>
        /// No evidence record exists for the current head. Sign-off has never been
        /// attempted or was never persisted for this head.
        /// </summary>
        Unavailable,

        /// <summary>
        /// Evidence exists but was captured against a different head commit than the
        /// one currently being evaluated. The evidence may be valid for a prior head
        /// but cannot gate the current head.
        /// </summary>
        HeadMismatch
    }

    /// <summary>
    /// Outcome of an approval webhook received for a protected sign-off flow.
    /// </summary>
    public enum ApprovalWebhookOutcome
    {
        /// <summary>Approval webhook delivered and processed successfully.</summary>
        Approved,

        /// <summary>
        /// Escalation webhook delivered — the case has been escalated to a higher
        /// reviewer before a final approval or denial decision.
        /// </summary>
        Escalated,

        /// <summary>Approval was denied; the release is blocked.</summary>
        Denied,

        /// <summary>
        /// Webhook payload arrived but was malformed, missing required fields,
        /// or could not be deserialized. The outcome is treated as unknown.
        /// </summary>
        Malformed,

        /// <summary>
        /// A delivery was attempted but the webhook never arrived within the
        /// expected window.
        /// </summary>
        TimedOut,

        /// <summary>
        /// The webhook delivery encountered an error that was not recoverable.
        /// </summary>
        DeliveryError
    }

    /// <summary>
    /// Aggregated release-readiness status for the current head.
    /// </summary>
    public enum SignOffReleaseReadinessStatus
    {
        /// <summary>
        /// All required approvals received, evidence is complete and fresh, and no
        /// blockers are outstanding. The release can proceed.
        /// </summary>
        Ready,

        /// <summary>
        /// One or more required inputs are missing or incomplete. The system is
        /// actively awaiting evidence, approvals, or remediation.
        /// </summary>
        Pending,

        /// <summary>
        /// A hard blocker prevents release. Evidence is missing, approval was denied,
        /// or a protected-environment check failed. Release is not permitted.
        /// </summary>
        Blocked,

        /// <summary>
        /// Evidence exists but is stale or was produced for a mismatched head.
        /// Re-execution of the sign-off workflow is required.
        /// </summary>
        Stale,

        /// <summary>
        /// The system cannot determine readiness because required backend services
        /// or evidence records are unavailable.
        /// </summary>
        Indeterminate,

        /// <summary>
        /// Release is blocked because required protected-environment configuration
        /// or credentials are missing. The workflow infrastructure cannot verify
        /// sign-off readiness without valid configuration.
        /// </summary>
        BlockedMissingConfiguration,

        /// <summary>
        /// Release is blocked because the compliance or KYC/AML provider is
        /// unavailable or unreachable. Evidence cannot be produced until the
        /// provider connection is restored.
        /// </summary>
        BlockedProviderUnavailable,

        /// <summary>
        /// Release is blocked because no evidence pack exists for the current
        /// head ref. A protected sign-off run must be executed to produce evidence.
        /// </summary>
        BlockedMissingEvidence,

        /// <summary>
        /// Evidence pack exists but is stale (past its freshness window) or was
        /// produced for a different head. The workflow must be re-executed to
        /// produce fresh evidence before release can proceed.
        /// </summary>
        DegradedStaleEvidence,

        /// <summary>
        /// An evidence pack was recorded but does not qualify as release evidence
        /// because protected configuration, live-provider credentials, or required
        /// proof artifacts were missing when it was captured. The workflow ran but
        /// produced a non-release-evidence outcome.
        /// </summary>
        NotReleaseEvidence
    }

    /// <summary>
    /// Operational mode of the strict sign-off artifact, giving downstream consumers
    /// a single, authoritative signal about whether the protected environment is
    /// configured before they need to inspect individual status fields.
    ///
    /// <para>
    /// This field is the key discriminator that closes the roadmap's documented
    /// backend-side MVP blocker. When the protected-sign-off lane is not set up,
    /// Mode is <see cref="NotConfigured"/> and the artifact must not be used as
    /// release evidence. When configuration is present and passes all checks,
    /// Mode advances through <see cref="Configured"/> to
    /// <see cref="ReadyReleaseGrade"/>.
    /// </para>
    /// </summary>
    public enum StrictArtifactMode
    {
        /// <summary>
        /// Required protected-environment configuration or credentials are absent.
        /// Evidence produced in this mode is never release-grade.  Operator must
        /// provision the required secrets and re-run the workflow before release
        /// sign-off can proceed.
        /// </summary>
        NotConfigured,

        /// <summary>
        /// The strict sign-off lane is configured with all required secrets and
        /// environment prerequisites. Evidence can be evaluated against release-grade
        /// criteria. Check <see cref="SignOffReleaseReadinessStatus"/> and the
        /// <c>Blockers</c> collection for the detailed verdict.
        /// </summary>
        Configured,

        /// <summary>
        /// Configuration is present but one or more optional components are
        /// unavailable or operating with reduced confidence (e.g., the KYC/AML
        /// provider is unreachable). Evidence may be produced but cannot be treated
        /// as full release-grade proof until the degraded component is restored.
        /// </summary>
        Degraded,

        /// <summary>
        /// Evidence exists but is past its freshness window or was captured for a
        /// different head commit. The strict lane must be re-executed against the
        /// current head before evidence can gate release.
        /// </summary>
        StaleEvidence,

        /// <summary>
        /// The strict lane is configured, all evidence is fresh and complete, all
        /// release-grade criteria are satisfied, and an approved webhook has been
        /// received. This is the highest-confidence state for product-owner and
        /// regulator review.
        /// </summary>
        ReadyReleaseGrade
    }

    /// <summary>
    /// Category of a release-readiness blocker, enabling the frontend to group
    /// and prioritise operator actions.
    /// </summary>
    public enum SignOffReleaseBlockerCategory
    {
        /// <summary>
        /// Sentinel value — a blocker must never carry this category.
        /// Its presence indicates that category assignment was omitted in the service layer.
        /// </summary>
        Unspecified = 0,

        /// <summary>Required approval webhook has not been received.</summary>
        MissingApproval = 1,

        /// <summary>Approval was explicitly denied by a reviewer.</summary>
        ApprovalDenied = 2,

        /// <summary>Evidence pack is absent for the current head.</summary>
        MissingEvidence = 3,

        /// <summary>Evidence pack is present but has expired or is beyond freshness window.</summary>
        StaleEvidence = 4,

        /// <summary>
        /// Evidence was produced for a different head than the current evaluation target.
        /// </summary>
        HeadMismatch = 5,

        /// <summary>Protected-environment checks have not passed.</summary>
        EnvironmentNotReady = 6,

        /// <summary>A compliance case required for sign-off is not in an approved state.</summary>
        CaseNotApproved = 7,

        /// <summary>
        /// An escalation is outstanding and must be resolved before approval can
        /// be finalised.
        /// </summary>
        UnresolvedEscalation = 8,

        /// <summary>The approval webhook arrived but contained malformed or missing data.</summary>
        MalformedWebhook = 9,

        /// <summary>A catch-all for blockers that do not fit other categories.</summary>
        Other = 10
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Core Domain Models
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A persisted record of an approval or escalation webhook outcome that
    /// arrived for a protected sign-off flow.
    /// </summary>
    public class ApprovalWebhookRecord
    {
        /// <summary>Unique identifier for this webhook record.</summary>
        public string RecordId { get; set; } = string.Empty;

        /// <summary>Identifier of the compliance case this webhook relates to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>
        /// The head commit SHA or release tag against which this webhook was recorded.
        /// </summary>
        public string HeadRef { get; set; } = string.Empty;

        /// <summary>The outcome carried by this webhook payload.</summary>
        public ApprovalWebhookOutcome Outcome { get; set; }

        /// <summary>UTC timestamp when this webhook was received and persisted.</summary>
        public DateTimeOffset ReceivedAt { get; set; }

        /// <summary>
        /// The actor (reviewer, system, or service principal) that originated the webhook.
        /// </summary>
        public string? ActorId { get; set; }

        /// <summary>
        /// Optional reason provided with the approval, denial, or escalation decision.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Correlation ID propagated from the originating workflow run, if available.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// True when the payload was validated as structurally correct and
        /// semantically meaningful.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Validation error message when <see cref="IsValid"/> is false.
        /// </summary>
        public string? ValidationError { get; set; }

        /// <summary>Raw payload hash (SHA-256 hex) for integrity verification.</summary>
        public string? PayloadHash { get; set; }

        /// <summary>Additional metadata key/value pairs from the webhook payload.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// A persisted evidence pack for a protected sign-off against a specific head.
    /// </summary>
    public class ProtectedSignOffEvidencePack
    {
        /// <summary>Unique identifier for this evidence pack.</summary>
        public string PackId { get; set; } = string.Empty;

        /// <summary>The head commit SHA or release tag this evidence was captured against.</summary>
        public string HeadRef { get; set; } = string.Empty;

        /// <summary>Identifier of the associated compliance case, if any.</summary>
        public string? CaseId { get; set; }

        /// <summary>Freshness status of this evidence pack.</summary>
        public SignOffEvidenceFreshnessStatus FreshnessStatus { get; set; }

        /// <summary>UTC timestamp when the evidence pack was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>UTC timestamp when the evidence pack was last evaluated for freshness.</summary>
        public DateTimeOffset LastEvaluatedAt { get; set; }

        /// <summary>UTC timestamp after which the evidence is considered stale.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>
        /// True when the evidence pack was produced by a live or sandbox provider
        /// (not simulated).
        /// </summary>
        public bool IsProviderBacked { get; set; }

        /// <summary>
        /// True when the evidence pack meets release-grade criteria: provider-backed,
        /// not expired, and all required checks passed.
        /// </summary>
        public bool IsReleaseGrade { get; set; }

        /// <summary>
        /// True when this evidence pack constitutes genuine release evidence — i.e.
        /// it was produced in a protected environment with live or sandbox provider
        /// credentials, all required proof artifacts are present, and no configuration
        /// blockers were encountered. Equivalent to <see cref="IsReleaseGrade"/>.
        ///
        /// This field provides an explicit <c>is_release_evidence</c> contract for
        /// frontend release-evidence center and product-owner tooling.
        /// </summary>
        public bool IsReleaseEvidence => IsReleaseGrade;

        /// <summary>
        /// The approval webhook record associated with this evidence pack, if received.
        /// </summary>
        public ApprovalWebhookRecord? ApprovalWebhook { get; set; }

        /// <summary>
        /// List of individual evidence items included in this pack (KYC, AML, sanctions,
        /// compliance case approval, etc.).
        /// </summary>
        public List<EvidencePackItem> Items { get; set; } = new();

        /// <summary>
        /// SHA-256 content hash of the serialised evidence pack for integrity verification.
        /// </summary>
        public string? ContentHash { get; set; }

        /// <summary>
        /// Actor who triggered the evidence pack creation.
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Label of the protected environment that produced this evidence pack
        /// (e.g., "protected-ci", "staging", "release-candidate").
        ///
        /// Sourced from <see cref="PersistSignOffEvidenceRequest.EnvironmentLabel"/>
        /// at capture time.  Null when the caller did not supply a label.
        ///
        /// Allows audit-export and reporting consumers to trace each evidence pack
        /// back to the exact protected environment without comparing external metadata.
        /// </summary>
        public string? EnvironmentLabel { get; set; }
    }

    /// <summary>
    /// A single item within a <see cref="ProtectedSignOffEvidencePack"/>.
    /// </summary>
    public class EvidencePackItem
    {
        /// <summary>Type identifier for this evidence item (e.g. KYC_DOCUMENT, AML_CHECK, APPROVAL_WEBHOOK).</summary>
        public string EvidenceType { get; set; } = string.Empty;

        /// <summary>Source service or system that produced this evidence item.</summary>
        public string? SourceService { get; set; }

        /// <summary>True when this evidence item is complete and valid.</summary>
        public bool IsPresent { get; set; }

        /// <summary>UTC timestamp when this evidence item was captured.</summary>
        public DateTimeOffset? CapturedAt { get; set; }

        /// <summary>UTC timestamp after which this evidence item is stale, if applicable.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>True when this item is past its expiry timestamp.</summary>
        public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;

        /// <summary>Optional reference to an external record (e.g. a compliance case ID).</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Detail note for operator visibility.</summary>
        public string? Detail { get; set; }
    }

    /// <summary>
    /// A blocker preventing release readiness for the current head.
    /// </summary>
    public class SignOffReleaseBlocker
    {
        /// <summary>Category of this blocker.</summary>
        public SignOffReleaseBlockerCategory Category { get; set; }

        /// <summary>Machine-readable error code for this blocker.</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Human-readable description of the blocker for operator visibility.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Concise, action-oriented guidance for the operator to resolve this blocker.
        /// </summary>
        public string RemediationHint { get; set; } = string.Empty;

        /// <summary>True when this blocker is critical and prevents any release promotion.</summary>
        public bool IsCritical { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // API Request / Response Models
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Request to record an incoming approval or escalation webhook outcome.
    /// </summary>
    public class RecordApprovalWebhookRequest
    {
        /// <summary>Identifier of the compliance case this webhook relates to.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>
        /// The head commit SHA or release tag against which this approval was granted.
        /// </summary>
        public string HeadRef { get; set; } = string.Empty;

        /// <summary>The outcome carried by the webhook payload.</summary>
        public ApprovalWebhookOutcome Outcome { get; set; }

        /// <summary>
        /// The actor (reviewer or service principal) that originated the webhook.
        /// </summary>
        public string? ActorId { get; set; }

        /// <summary>Reason provided with the decision.</summary>
        public string? Reason { get; set; }

        /// <summary>Correlation ID propagated from the originating workflow.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Optional raw payload for hash computation and audit.</summary>
        public string? RawPayload { get; set; }

        /// <summary>Additional metadata key/value pairs from the webhook payload.</summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Response to a <see cref="RecordApprovalWebhookRequest"/>.
    /// </summary>
    public class RecordApprovalWebhookResponse
    {
        /// <summary>True when the webhook was persisted successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The persisted webhook record.</summary>
        public ApprovalWebhookRecord? Record { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Actionable guidance when the record was not persisted.</summary>
        public string? RemediationHint { get; set; }
    }

    /// <summary>
    /// Request to persist or refresh a protected sign-off evidence pack.
    /// </summary>
    public class PersistSignOffEvidenceRequest
    {
        /// <summary>The head commit SHA or release tag to capture evidence against.</summary>
        public string HeadRef { get; set; } = string.Empty;

        /// <summary>Compliance case to include in the evidence pack.</summary>
        public string? CaseId { get; set; }

        /// <summary>
        /// Freshness window in hours. Evidence captured within this window is
        /// considered not stale. Defaults to 24 hours.
        /// </summary>
        public int FreshnessWindowHours { get; set; } = 24;

        /// <summary>
        /// When true, the call fails if the evidence pack cannot be marked as
        /// release-grade (i.e., all items must be provider-backed).
        /// </summary>
        public bool RequireReleaseGrade { get; set; }

        /// <summary>
        /// When true, the call fails if a required approval webhook has not been received
        /// for this head ref.
        /// </summary>
        public bool RequireApprovalWebhook { get; set; }

        /// <summary>
        /// Optional label identifying the protected environment that is producing this
        /// evidence pack (e.g., "protected-ci", "staging", "release-candidate").
        ///
        /// When set, the label is stored on the resulting <see cref="ProtectedSignOffEvidencePack"/>
        /// so that audit and reporting consumers can trace evidence back to its source
        /// environment without relying on external metadata.
        /// </summary>
        public string? EnvironmentLabel { get; set; }
    }

    /// <summary>
    /// Response to a <see cref="PersistSignOffEvidenceRequest"/>.
    /// </summary>
    public class PersistSignOffEvidenceResponse
    {
        /// <summary>True when the evidence pack was persisted successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The persisted evidence pack.</summary>
        public ProtectedSignOffEvidencePack? Pack { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Actionable guidance when the pack was not persisted.</summary>
        public string? RemediationHint { get; set; }
    }

    /// <summary>
    /// Request to evaluate the release readiness for a given head ref.
    /// </summary>
    public class GetSignOffReleaseReadinessRequest
    {
        /// <summary>The head commit SHA or release tag to evaluate.</summary>
        public string HeadRef { get; set; } = string.Empty;

        /// <summary>Compliance case to include in the evaluation, if applicable.</summary>
        public string? CaseId { get; set; }

        /// <summary>
        /// Freshness window in hours used to classify stale evidence.
        /// Defaults to 24 hours.
        /// </summary>
        public int FreshnessWindowHours { get; set; } = 24;
    }

    /// <summary>
    /// Response describing the aggregated release readiness for the current head.
    /// Provides a single authoritative object for operator dashboards and the
    /// release evidence center.
    /// </summary>
    public class GetSignOffReleaseReadinessResponse
    {
        /// <summary>True when release is fully ready (no blockers).</summary>
        public bool Success { get; set; }

        /// <summary>Aggregated readiness status.</summary>
        public SignOffReleaseReadinessStatus Status { get; set; }

        /// <summary>The head ref that was evaluated.</summary>
        public string HeadRef { get; set; } = string.Empty;

        /// <summary>
        /// True when the latest evidence pack for this head qualifies as genuine
        /// release evidence — produced in a protected environment with live or
        /// sandbox provider credentials, all required proof artifacts present,
        /// and no configuration blockers encountered.
        ///
        /// This field is false when no evidence exists, when evidence is non-release-grade
        /// (simulated or unconfigured), or when any blocker prevents release.
        ///
        /// Consumers MUST NOT infer readiness solely from this field; use
        /// <see cref="Status"/> and <see cref="Blockers"/> for the authoritative
        /// release decision.
        /// </summary>
        public bool IsReleaseEvidence { get; set; }

        /// <summary>
        /// Evidence freshness status for the evaluated head ref.
        /// </summary>
        public SignOffEvidenceFreshnessStatus EvidenceFreshness { get; set; }

        /// <summary>True when a valid approval webhook has been received for this head.</summary>
        public bool HasApprovalWebhook { get; set; }

        /// <summary>The most recently received approval webhook for this head, if any.</summary>
        public ApprovalWebhookRecord? LatestApprovalWebhook { get; set; }

        /// <summary>The most recent evidence pack for this head, if any.</summary>
        public ProtectedSignOffEvidencePack? LatestEvidencePack { get; set; }

        /// <summary>
        /// Ordered list of blockers preventing release. Empty when <see cref="Status"/> is Ready.
        /// </summary>
        public List<SignOffReleaseBlocker> Blockers { get; set; } = new();

        /// <summary>
        /// Top-level operator guidance summarising next actions required before release.
        /// Null when status is Ready.
        /// </summary>
        public string? OperatorGuidance { get; set; }

        /// <summary>UTC timestamp when this readiness evaluation was performed.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }

        /// <summary>
        /// Machine-readable error code on error.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message on error.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Operational mode of the strict sign-off artifact.
        ///
        /// <para>
        /// This is the primary field for distinguishing a correctly configured
        /// protected lane from an unconfigured or degraded one, without needing to
        /// inspect <see cref="Status"/> or <see cref="Blockers"/>.
        /// </para>
        ///
        /// <list type="bullet">
        ///   <item>
        ///     <see cref="StrictArtifactMode.NotConfigured"/> — required secrets or
        ///     environment configuration are absent; this artifact must not be used
        ///     as release evidence (<see cref="IsReleaseEvidence"/> is always false).
        ///   </item>
        ///   <item>
        ///     <see cref="StrictArtifactMode.Configured"/> — environment is configured;
        ///     check <see cref="Status"/> and <see cref="Blockers"/> for the full verdict.
        ///   </item>
        ///   <item>
        ///     <see cref="StrictArtifactMode.Degraded"/> — configuration present but an
        ///     optional provider is unavailable; evidence may still be produced but
        ///     cannot be treated as full release-grade proof.
        ///   </item>
        ///   <item>
        ///     <see cref="StrictArtifactMode.StaleEvidence"/> — evidence exists but has
        ///     passed its freshness window or was captured for a different head; the
        ///     workflow must be re-run before release can proceed.
        ///   </item>
        ///   <item>
        ///     <see cref="StrictArtifactMode.ReadyReleaseGrade"/> — fully configured,
        ///     fresh, complete, and release-grade. Safe to use as authoritative release
        ///     proof; <see cref="IsReleaseEvidence"/> is true.
        ///   </item>
        /// </list>
        /// </summary>
        public StrictArtifactMode Mode { get; set; }

        /// <summary>
        /// Label of the protected environment that produced the evaluated evidence pack,
        /// if available (e.g., "protected-ci", "staging", "release-candidate").
        ///
        /// Sourced from the latest <see cref="ProtectedSignOffEvidencePack.EnvironmentLabel"/>
        /// for this head ref.  Null when no pack exists or the pack carried no label.
        ///
        /// Enables audit and reporting consumers to confirm evidence originates from
        /// the expected protected environment without comparing external metadata.
        /// </summary>
        public string? EnvironmentLabel { get; set; }
    }

    /// <summary>
    /// Request to retrieve the approval webhook history for a given case and head ref.
    /// </summary>
    public class GetApprovalWebhookHistoryRequest
    {
        /// <summary>Compliance case ID to filter by.</summary>
        public string? CaseId { get; set; }

        /// <summary>Head ref to filter by. If null, returns history for all head refs.</summary>
        public string? HeadRef { get; set; }

        /// <summary>Maximum number of records to return. Defaults to 50.</summary>
        public int MaxRecords { get; set; } = 50;
    }

    /// <summary>
    /// Response containing approval webhook history records.
    /// </summary>
    public class GetApprovalWebhookHistoryResponse
    {
        /// <summary>True when the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Ordered list of approval webhook records (newest first).</summary>
        public List<ApprovalWebhookRecord> Records { get; set; } = new();

        /// <summary>Total number of records in the store matching the filter.</summary>
        public int TotalCount { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request to retrieve evidence pack history for a given head ref.
    /// </summary>
    public class GetEvidencePackHistoryRequest
    {
        /// <summary>Head ref to filter by. If null, returns all packs.</summary>
        public string? HeadRef { get; set; }

        /// <summary>Case ID to filter by. If null, returns packs for all cases.</summary>
        public string? CaseId { get; set; }

        /// <summary>Maximum number of records to return. Defaults to 50.</summary>
        public int MaxRecords { get; set; } = 50;
    }

    /// <summary>
    /// Response containing evidence pack history.
    /// </summary>
    public class GetEvidencePackHistoryResponse
    {
        /// <summary>True when the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Ordered list of evidence packs (newest first).</summary>
        public List<ProtectedSignOffEvidencePack> Packs { get; set; } = new();

        /// <summary>Total number of packs in the store matching the filter.</summary>
        public int TotalCount { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }
}
