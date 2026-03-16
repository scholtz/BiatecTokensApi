namespace BiatecTokensApi.Models.KycAmlSignOff
{
    // ── Enums ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Distinguishes how the KYC/AML check was executed:
    /// via a live external provider, a protected sandbox, or a local simulation.
    /// This field is used by product leadership and operators to determine whether
    /// sign-off evidence qualifies as release-grade.
    /// </summary>
    public enum KycAmlSignOffExecutionMode
    {
        /// <summary>
        /// The check was executed against a live, production-grade external provider
        /// using real API credentials. Evidence produced here is release-grade.
        /// </summary>
        LiveProvider,

        /// <summary>
        /// The check was executed against a provider-operated sandbox or protected
        /// test environment using protected (non-production) API keys. Evidence is
        /// production-like but not live. Suitable for sign-off validation.
        /// </summary>
        ProtectedSandbox,

        /// <summary>
        /// The check was executed using an internal simulation / mock provider.
        /// Evidence is not release-grade. Suitable for unit and integration tests only.
        /// </summary>
        Simulated
    }

    /// <summary>
    /// The normalized outcome of a KYC/AML sign-off check after provider processing.
    /// </summary>
    public enum KycAmlSignOffOutcome
    {
        /// <summary>
        /// The provider has not yet returned a result; awaiting callback or polling.
        /// </summary>
        Pending,

        /// <summary>
        /// All required checks passed. Subject is approved for the next workflow step.
        /// </summary>
        Approved,

        /// <summary>
        /// One or more checks failed definitively (e.g., sanctions list match).
        /// Subject is rejected.
        /// </summary>
        Rejected,

        /// <summary>
        /// The provider result requires manual analyst review before a final decision
        /// can be made.
        /// </summary>
        NeedsManualReview,

        /// <summary>
        /// Adverse findings were detected (e.g., PEP match, adverse media, watchlist
        /// hit). Remediation or further investigation is required.
        /// </summary>
        AdverseFindings,

        /// <summary>
        /// The check is blocked because of a pre-existing or dependent condition
        /// (e.g., unresolved prior adverse finding, incomplete remediation).
        /// </summary>
        Blocked,

        /// <summary>
        /// Evidence was produced but has since expired. A new check must be initiated.
        /// </summary>
        Stale,

        /// <summary>
        /// The external provider was unavailable or returned a transient error.
        /// The system has failed closed; no approval should be issued.
        /// </summary>
        ProviderUnavailable,

        /// <summary>
        /// A provider error or unrecognised response was received.
        /// </summary>
        Error,

        /// <summary>
        /// The callback or polling data was malformed or contradictory.
        /// </summary>
        MalformedCallback
    }

    /// <summary>
    /// Readiness state derived from all KYC/AML sign-off evidence for a subject.
    /// Used by approval-gating logic and the enterprise frontend.
    /// </summary>
    public enum KycAmlSignOffReadinessState
    {
        /// <summary>
        /// All required checks have been completed, passed, and evidence is fresh.
        /// Subject is ready for approval.
        /// </summary>
        Ready,

        /// <summary>
        /// One or more checks are blocked due to adverse findings, provider failures,
        /// or unresolved remediation. Approval cannot proceed.
        /// </summary>
        Blocked,

        /// <summary>
        /// Evidence is present but requires manual review before a readiness decision.
        /// </summary>
        RequiresReview,

        /// <summary>
        /// One or more checks are still awaiting a provider response.
        /// </summary>
        AwaitingProvider,

        /// <summary>
        /// Evidence was previously ready but has since expired. A new check must be
        /// initiated.
        /// </summary>
        Stale,

        /// <summary>
        /// Required evidence has not yet been submitted or no checks have been
        /// initiated.
        /// </summary>
        IncompleteEvidence
    }

    /// <summary>
    /// The kind of KYC/AML check within a sign-off record.
    /// </summary>
    public enum KycAmlSignOffCheckKind
    {
        /// <summary>Identity / KYC check.</summary>
        IdentityKyc,

        /// <summary>AML / sanctions / watchlist screening.</summary>
        AmlScreening,

        /// <summary>Combined KYC identity check and AML screening.</summary>
        Combined
    }

    // ── Core records ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A single audit event appended to the evidence record when the state or
    /// outcome of a KYC/AML sign-off changes.
    /// </summary>
    public class KycAmlSignOffAuditEvent
    {
        /// <summary>UTC timestamp of the event.</summary>
        public DateTimeOffset OccurredAt { get; set; }

        /// <summary>Short label describing what happened, e.g. "ProviderCallbackReceived".</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>Previous outcome before this event, if applicable.</summary>
        public KycAmlSignOffOutcome? PreviousOutcome { get; set; }

        /// <summary>New outcome after this event.</summary>
        public KycAmlSignOffOutcome NewOutcome { get; set; }

        /// <summary>Plain-language description of the event suitable for operator display.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Opaque provider reference or correlation ID attached to this event.</summary>
        public string? ProviderReference { get; set; }

        /// <summary>Actor who triggered the event (system, webhook, or operator ID).</summary>
        public string Actor { get; set; } = "system";
    }

    /// <summary>
    /// A durable evidence artifact attached to a KYC/AML sign-off record.
    /// Artifacts distinguish real provider evidence from simulated test output.
    /// </summary>
    public class KycAmlSignOffEvidenceArtifact
    {
        /// <summary>Unique artifact identifier.</summary>
        public string ArtifactId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Kind of artifact: "ProviderInitiationRecord", "ProviderCallbackPayload",
        /// "StateTransitionRecord", "ReadinessAssessment", or "BlockerSummary".
        /// </summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>When the artifact was created (UTC).</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Execution mode at the time this artifact was created.
        /// Operators use this to distinguish live from simulated evidence.
        /// </summary>
        public KycAmlSignOffExecutionMode ExecutionMode { get; set; }

        /// <summary>
        /// True when this artifact was produced by a live or protected-sandbox
        /// provider. False for simulated/mock evidence.
        /// </summary>
        public bool IsProviderBacked => ExecutionMode != KycAmlSignOffExecutionMode.Simulated;

        /// <summary>Provider name that produced this artifact, if applicable.</summary>
        public string? ProviderName { get; set; }

        /// <summary>Provider reference ID associated with this artifact.</summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>
        /// Structured summary of the artifact content. Does not contain raw provider
        /// secrets or sensitive personal data.
        /// </summary>
        public Dictionary<string, string> Summary { get; set; } = new();

        /// <summary>
        /// Plain-language explanation of what this artifact represents, suitable for
        /// display in enterprise operator interfaces.
        /// </summary>
        public string ExplanationText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Core record that tracks the full lifecycle of a KYC/AML sign-off evidence
    /// flow for a subject.
    /// </summary>
    public class KycAmlSignOffRecord
    {
        /// <summary>Unique record identifier.</summary>
        public string RecordId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Subject being evaluated (user ID, investor ID, etc.).</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Optional context identifier (e.g., token issuance context).</summary>
        public string? ContextId { get; set; }

        /// <summary>Kind of check this record tracks.</summary>
        public KycAmlSignOffCheckKind CheckKind { get; set; }

        /// <summary>
        /// Execution mode. Operators and product leadership use this to determine
        /// whether evidence is release-grade.
        /// </summary>
        public KycAmlSignOffExecutionMode ExecutionMode { get; set; }

        /// <summary>
        /// True when this record was produced via a live or protected-sandbox provider.
        /// Product leadership can use this as a quick signal for release-grade evidence.
        /// </summary>
        public bool IsProviderBacked => ExecutionMode != KycAmlSignOffExecutionMode.Simulated;

        /// <summary>Current outcome of the sign-off flow.</summary>
        public KycAmlSignOffOutcome Outcome { get; set; } = KycAmlSignOffOutcome.Pending;

        /// <summary>Current readiness state for approval gating.</summary>
        public KycAmlSignOffReadinessState ReadinessState { get; set; } = KycAmlSignOffReadinessState.AwaitingProvider;

        /// <summary>
        /// Plain-language explanation of the current state, suitable for enterprise
        /// frontend display (e.g., "Identity verification awaiting provider callback").
        /// </summary>
        public string ReadinessExplanation { get; set; } = string.Empty;

        /// <summary>Name of the KYC provider used for this record, if applicable.</summary>
        public string? KycProviderName { get; set; }

        /// <summary>Provider reference ID for the KYC check, if applicable.</summary>
        public string? KycProviderReferenceId { get; set; }

        /// <summary>Name of the AML provider used for this record, if applicable.</summary>
        public string? AmlProviderName { get; set; }

        /// <summary>Provider reference ID for the AML screening, if applicable.</summary>
        public string? AmlProviderReferenceId { get; set; }

        /// <summary>Optional reason code from the provider (e.g., "SANCTIONS_MATCH").</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Whether the evidence has expired.</summary>
        public bool IsEvidenceExpired { get; set; }

        /// <summary>When the evidence expires (UTC), if applicable.</summary>
        public DateTimeOffset? EvidenceExpiresAt { get; set; }

        /// <summary>When the record was created (UTC).</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>When the record was last updated (UTC).</summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Actor who initiated the sign-off (user ID or system).</summary>
        public string InitiatedBy { get; set; } = string.Empty;

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Chronological audit trail of all state transitions.</summary>
        public List<KycAmlSignOffAuditEvent> AuditTrail { get; set; } = new();

        /// <summary>Durable evidence artifacts for this record.</summary>
        public List<KycAmlSignOffEvidenceArtifact> EvidenceArtifacts { get; set; } = new();

        /// <summary>
        /// Blockers preventing approval, if any. Each blocker contains a code and
        /// an operator-friendly description.
        /// </summary>
        public List<KycAmlSignOffBlocker> Blockers { get; set; } = new();
    }

    /// <summary>
    /// A blocker associated with a KYC/AML sign-off record that prevents approval.
    /// </summary>
    public class KycAmlSignOffBlocker
    {
        /// <summary>Machine-readable blocker code (e.g., "SANCTIONS_MATCH", "PROVIDER_UNAVAILABLE").</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Plain-language description of the blocker suitable for enterprise operator
        /// display (e.g., "Sanctions hit requires analyst review").
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Whether this blocker is remediable (true) or terminal (false).</summary>
        public bool IsRemediable { get; set; }

        /// <summary>UTC timestamp when this blocker was first recorded.</summary>
        public DateTimeOffset RecordedAt { get; set; }
    }

    // ── Request / Response types ─────────────────────────────────────────────────

    /// <summary>
    /// Request to initiate a new KYC/AML sign-off evidence flow for a subject.
    /// </summary>
    public class InitiateKycAmlSignOffRequest
    {
        /// <summary>Subject ID being evaluated (required).</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Optional context ID (e.g., token issuance context).</summary>
        public string? ContextId { get; set; }

        /// <summary>Kind of check to perform.</summary>
        public KycAmlSignOffCheckKind CheckKind { get; set; } = KycAmlSignOffCheckKind.Combined;

        /// <summary>
        /// Requested execution mode. Defaults to LiveProvider.
        /// If the provider is not configured, the service will fail closed rather
        /// than silently downgrading to simulation.
        /// </summary>
        public KycAmlSignOffExecutionMode RequestedExecutionMode { get; set; } = KycAmlSignOffExecutionMode.LiveProvider;

        /// <summary>
        /// Additional metadata about the subject for provider screening
        /// (e.g., full_name, date_of_birth, country).
        /// </summary>
        public Dictionary<string, string> SubjectMetadata { get; set; } = new();

        /// <summary>
        /// Optional idempotency key. If provided, a second call with the same key
        /// returns the existing record without re-initiating the provider check.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>
        /// Optional evidence validity hours. When the provider result arrives, the
        /// evidence expiry will be set this many hours in the future.
        /// </summary>
        public int? EvidenceValidityHours { get; set; }
    }

    /// <summary>
    /// Response from initiating a KYC/AML sign-off evidence flow.
    /// </summary>
    public class InitiateKycAmlSignOffResponse
    {
        /// <summary>True when the record was successfully created or retrieved.</summary>
        public bool Success { get; set; }

        /// <summary>The newly created (or existing) sign-off record.</summary>
        public KycAmlSignOffRecord? Record { get; set; }

        /// <summary>True when an existing record was returned due to idempotency key match.</summary>
        public bool WasIdempotent { get; set; }

        /// <summary>Error code if Success is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Error message if Success is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Request to process an inbound provider callback for an existing sign-off record.
    /// </summary>
    public class ProcessKycAmlSignOffCallbackRequest
    {
        /// <summary>Provider reference ID in the callback payload (required).</summary>
        public string ProviderReferenceId { get; set; } = string.Empty;

        /// <summary>Normalized outcome status from the provider (required).</summary>
        public string OutcomeStatus { get; set; } = string.Empty;

        /// <summary>Optional reason code from the provider.</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Optional event type label from the provider (e.g., "identity.verified").</summary>
        public string? EventType { get; set; }

        /// <summary>Optional raw payload for audit purposes (must not contain secrets).</summary>
        public string? RawPayloadSummary { get; set; }

        /// <summary>Optional HMAC signature for webhook verification.</summary>
        public string? Signature { get; set; }

        /// <summary>When the provider completed its processing (UTC).</summary>
        public DateTimeOffset? ProviderCompletedAt { get; set; }
    }

    /// <summary>
    /// Response from processing a provider callback.
    /// </summary>
    public class ProcessKycAmlSignOffCallbackResponse
    {
        /// <summary>True when the callback was accepted and processed.</summary>
        public bool Success { get; set; }

        /// <summary>Updated record after callback processing.</summary>
        public KycAmlSignOffRecord? Record { get; set; }

        /// <summary>Error code if Success is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Error message if Success is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Response containing a single sign-off record.
    /// </summary>
    public class GetKycAmlSignOffRecordResponse
    {
        /// <summary>True when the record was found.</summary>
        public bool Success { get; set; }

        /// <summary>The sign-off record.</summary>
        public KycAmlSignOffRecord? Record { get; set; }

        /// <summary>Error code if Success is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Error message if Success is false.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response containing the readiness evaluation for a sign-off record.
    /// </summary>
    public class KycAmlSignOffReadinessResponse
    {
        /// <summary>The sign-off record ID.</summary>
        public string RecordId { get; set; } = string.Empty;

        /// <summary>The subject ID.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Current readiness state.</summary>
        public KycAmlSignOffReadinessState ReadinessState { get; set; }

        /// <summary>Current outcome.</summary>
        public KycAmlSignOffOutcome Outcome { get; set; }

        /// <summary>
        /// True when the subject is fully ready for approval. Only true when
        /// ReadinessState == Ready and execution was via a live or protected-sandbox
        /// provider.
        /// </summary>
        public bool IsApprovalReady { get; set; }

        /// <summary>
        /// True when this readiness assessment is based on provider-backed evidence.
        /// False for simulated/mock evidence.
        /// </summary>
        public bool IsProviderBacked { get; set; }

        /// <summary>Execution mode of the underlying evidence.</summary>
        public KycAmlSignOffExecutionMode ExecutionMode { get; set; }

        /// <summary>
        /// Plain-language explanation of the readiness state for enterprise operator
        /// display.
        /// </summary>
        public string ExplanationText { get; set; } = string.Empty;

        /// <summary>Active blockers preventing approval, if any.</summary>
        public List<KycAmlSignOffBlocker> Blockers { get; set; } = new();

        /// <summary>When this readiness assessment was computed (UTC).</summary>
        public DateTimeOffset EvaluatedAt { get; set; }
    }

    /// <summary>
    /// Response containing the evidence artifacts for a sign-off record.
    /// </summary>
    public class GetKycAmlSignOffArtifactsResponse
    {
        /// <summary>The sign-off record ID.</summary>
        public string RecordId { get; set; } = string.Empty;

        /// <summary>The subject ID.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>All evidence artifacts for this record.</summary>
        public List<KycAmlSignOffEvidenceArtifact> Artifacts { get; set; } = new();

        /// <summary>
        /// True when at least one artifact was produced by a live or protected-sandbox
        /// provider.
        /// </summary>
        public bool HasProviderBackedArtifacts { get; set; }

        /// <summary>Error code if retrieval failed.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Error message if retrieval failed.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response containing all sign-off records for a subject.
    /// </summary>
    public class ListKycAmlSignOffRecordsResponse
    {
        /// <summary>Subject ID.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>All sign-off records for the subject, ordered by creation time descending.</summary>
        public List<KycAmlSignOffRecord> Records { get; set; } = new();

        /// <summary>Total count of records.</summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// The most recent record's readiness state, for convenience.
        /// </summary>
        public KycAmlSignOffReadinessState? LatestReadinessState { get; set; }
    }

    /// <summary>
    /// Response from polling for an updated provider status.
    /// </summary>
    public class PollKycAmlSignOffStatusResponse
    {
        /// <summary>True when polling succeeded (even if the outcome hasn't changed).</summary>
        public bool Success { get; set; }

        /// <summary>Updated record after polling.</summary>
        public KycAmlSignOffRecord? Record { get; set; }

        /// <summary>True when the outcome changed as a result of this poll.</summary>
        public bool OutcomeChanged { get; set; }

        /// <summary>Error code if Success is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Error message if Success is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }
}
