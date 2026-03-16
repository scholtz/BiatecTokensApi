namespace BiatecTokensApi.Models.KycAmlDecisionIngestion
{
    // ── Enums ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Identifies the compliance check provider that originated a decision.
    /// Keeping the ingestion layer provider-agnostic means internal logic
    /// never branches on provider identity for business rules.
    /// </summary>
    public enum IngestionProviderType
    {
        /// <summary>Decision was submitted via a manual reviewer workflow.</summary>
        Manual = 0,
        /// <summary>Decision originated from a generic or unknown external system.</summary>
        Generic = 1,
        /// <summary>Onfido identity and document verification platform.</summary>
        Onfido = 2,
        /// <summary>Jumio identity and biometric verification platform.</summary>
        Jumio = 3,
        /// <summary>Stripe Identity KYC verification service.</summary>
        StripeIdentity = 4,
        /// <summary>ComplyAdvantage AML and sanctions screening service.</summary>
        ComplyAdvantage = 5,
        /// <summary>Refinitiv (now LSEG) World-Check screening database.</summary>
        WorldCheck = 6,
        /// <summary>Chainalysis blockchain analytics for AML screening.</summary>
        Chainalysis = 7,
        /// <summary>Elliptic blockchain analytics for AML screening.</summary>
        Elliptic = 8,
        /// <summary>Sumsub all-in-one compliance verification platform.</summary>
        Sumsub = 9,
        /// <summary>Internal compliance evaluation performed by this platform.</summary>
        Internal = 10
    }

    /// <summary>
    /// Classifies the kind of compliance check represented by a decision record.
    /// </summary>
    public enum IngestionDecisionKind
    {
        /// <summary>Know Your Customer / identity verification.</summary>
        IdentityKyc = 0,
        /// <summary>Anti-Money Laundering / sanctions screening.</summary>
        AmlSanctions = 1,
        /// <summary>Jurisdiction eligibility check.</summary>
        JurisdictionCheck = 2,
        /// <summary>Document review and authenticity check.</summary>
        DocumentReview = 3,
        /// <summary>Risk scoring or risk profiling assessment.</summary>
        RiskScoring = 4,
        /// <summary>Manual compliance review by a designated reviewer.</summary>
        ManualReview = 5,
        /// <summary>Combined KYC + AML in a single provider call.</summary>
        Combined = 6,
        /// <summary>Adverse media and negative news screening.</summary>
        AdverseMedia = 7,
        /// <summary>Politically exposed person screening.</summary>
        PepScreening = 8
    }

    /// <summary>
    /// Normalised outcome status for any ingested compliance decision,
    /// independent of provider-specific status terminology.
    /// </summary>
    public enum NormalizedIngestionStatus
    {
        /// <summary>Check initiated; result not yet available.</summary>
        Pending = 0,
        /// <summary>Subject passed the compliance check.</summary>
        Approved = 1,
        /// <summary>Subject failed the compliance check.</summary>
        Rejected = 2,
        /// <summary>Manual review is required before a final decision can be made.</summary>
        NeedsReview = 3,
        /// <summary>A previously-approved decision has passed its validity window.</summary>
        Expired = 4,
        /// <summary>Insufficient subject data to complete the check.</summary>
        InsufficientData = 5,
        /// <summary>Provider was unreachable or returned an unusable response; fail-closed.</summary>
        ProviderUnavailable = 6,
        /// <summary>Two or more decisions for the same check type contradict each other.</summary>
        Contradiction = 7,
        /// <summary>An internal or provider-side error prevented the check from completing.</summary>
        Error = 8
    }

    /// <summary>
    /// Aggregated readiness state derived from all compliance decisions for a subject or cohort.
    /// </summary>
    public enum IngestionReadinessState
    {
        /// <summary>All required checks are approved; launch is permitted.</summary>
        Ready = 0,
        /// <summary>One or more hard blockers prevent launch.</summary>
        Blocked = 1,
        /// <summary>Manual review is in progress; launch cannot be confirmed yet.</summary>
        PendingReview = 2,
        /// <summary>Non-critical issues exist that warrant attention but do not block launch.</summary>
        AtRisk = 3,
        /// <summary>One or more evidence items are stale; remediation is required.</summary>
        Stale = 4,
        /// <summary>Required evidence has not yet been submitted.</summary>
        EvidenceMissing = 5,
        /// <summary>Readiness has not yet been evaluated.</summary>
        Unknown = 6
    }

    /// <summary>
    /// Classifies the kind of evidence artifact stored alongside a decision.
    /// </summary>
    public enum EvidenceArtifactKind
    {
        /// <summary>Identity document (passport, driving licence, national ID).</summary>
        IdentityDocument = 0,
        /// <summary>Sanctions or watchlist screening report.</summary>
        ScreeningReport = 1,
        /// <summary>Proof of address document.</summary>
        ProofOfAddress = 2,
        /// <summary>Numeric risk score or risk rating from a scoring provider.</summary>
        RiskScore = 3,
        /// <summary>Jurisdiction eligibility determination artefact.</summary>
        JurisdictionCheckResult = 4,
        /// <summary>Adverse media or negative news hit record.</summary>
        AdverseMediaHit = 5,
        /// <summary>Watchlist or sanctions list entry match record.</summary>
        SanctionsListHit = 6,
        /// <summary>Free-text note recorded by a human reviewer.</summary>
        ManualReviewNote = 7,
        /// <summary>External reference link or URI pointing to off-platform evidence.</summary>
        ExternalReference = 8,
        /// <summary>Biometric verification result.</summary>
        BiometricResult = 9,
        /// <summary>Accreditation or professional qualification document.</summary>
        AccreditationDocument = 10,
        /// <summary>Raw provider payload preserved for audit purposes.</summary>
        ProviderRawPayload = 11
    }

    /// <summary>
    /// Severity of a launch blocker or remediation requirement.
    /// </summary>
    public enum IngestionBlockerSeverity
    {
        /// <summary>Critical blocker; launch is prohibited.</summary>
        Critical = 0,
        /// <summary>High-severity issue; launch is strongly discouraged without resolution.</summary>
        High = 1,
        /// <summary>Medium severity advisory.</summary>
        Medium = 2,
        /// <summary>Low-severity note; launch may proceed with acknowledged risk.</summary>
        Low = 3
    }

    // ── Evidence artefact ─────────────────────────────────────────────────────

    /// <summary>
    /// A piece of compliance evidence retained alongside an ingested decision.
    /// Records provenance so every artefact can be traced back to its source.
    /// </summary>
    public class IngestionEvidenceArtifact
    {
        /// <summary>Unique identifier for this artefact.</summary>
        public string ArtifactId { get; set; } = string.Empty;

        /// <summary>Classification of this artefact.</summary>
        public EvidenceArtifactKind Kind { get; set; }

        /// <summary>
        /// Human-readable label for the artefact
        /// (e.g. "Passport scan 2025-Q4", "ComplyAdvantage report #R-8821").
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Provider-assigned reference identifier for this artefact.
        /// Allows cross-referencing back to the originating provider system.
        /// </summary>
        public string? ProviderArtifactId { get; set; }

        /// <summary>URI or pointer to the artefact content (optional; may be absent for privacy).</summary>
        public string? ContentUri { get; set; }

        /// <summary>SHA-256 content hash for integrity verification, if available.</summary>
        public string? ContentHash { get; set; }

        /// <summary>Timestamp when this artefact was received or recorded.</summary>
        public DateTimeOffset ReceivedAt { get; set; }

        /// <summary>Timestamp when this artefact's validity expires, if applicable.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>True when the artefact has passed its expiry date.</summary>
        public bool IsExpired { get; set; }

        /// <summary>
        /// Additional key-value metadata about the artefact
        /// (e.g. document type codes, issuing country, confidence level).
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    // ── Decision record ───────────────────────────────────────────────────────

    /// <summary>
    /// Normalised compliance decision record, provider-agnostic.
    /// The ingestion layer translates provider-specific status codes into
    /// <see cref="NormalizedIngestionStatus"/> so downstream logic is stable.
    /// </summary>
    public class IngestionDecisionRecord
    {
        /// <summary>Stable unique identifier for this decision record.</summary>
        public string DecisionId { get; set; } = string.Empty;

        /// <summary>Identifier of the subject being evaluated (investor, entity, address).</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Issuer or context scope for this decision.</summary>
        public string ContextId { get; set; } = string.Empty;

        /// <summary>Kind of compliance check this decision represents.</summary>
        public IngestionDecisionKind Kind { get; set; }

        /// <summary>Provider that originated this decision.</summary>
        public IngestionProviderType Provider { get; set; }

        /// <summary>
        /// Provider-assigned identifier for the underlying check or case.
        /// Preserved for cross-referencing but never used in business-rule logic.
        /// </summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>
        /// Raw status string exactly as received from the provider before normalisation.
        /// Stored for audit/support purposes; not used for readiness logic.
        /// </summary>
        public string? ProviderRawStatus { get; set; }

        /// <summary>Normalised decision outcome.</summary>
        public NormalizedIngestionStatus Status { get; set; }

        /// <summary>Actor (user or system account) who submitted or triggered this ingestion.</summary>
        public string IngestedBy { get; set; } = string.Empty;

        /// <summary>Timestamp when the decision was ingested into the platform.</summary>
        public DateTimeOffset IngestedAt { get; set; }

        /// <summary>
        /// Timestamp when the provider completed the underlying check,
        /// if reported (may differ from <see cref="IngestedAt"/>).
        /// </summary>
        public DateTimeOffset? ProviderCompletedAt { get; set; }

        /// <summary>Timestamp when this decision's evidence window expires.</summary>
        public DateTimeOffset? EvidenceExpiresAt { get; set; }

        /// <summary>True when the evidence validity window has been exceeded.</summary>
        public bool IsEvidenceExpired { get; set; }

        /// <summary>
        /// Human-readable reason code capturing why a check was rejected or flagged
        /// (e.g., "SANCTIONS_MATCH", "DOCUMENT_EXPIRED", "JURISDICTION_RESTRICTED").
        /// </summary>
        public string? ReasonCode { get; set; }

        /// <summary>Extended human-readable explanation of the decision.</summary>
        public string? Rationale { get; set; }

        /// <summary>Confidence score reported by the provider, if available (0–100).</summary>
        public double? ConfidenceScore { get; set; }

        /// <summary>Jurisdiction code relevant to this decision (ISO 3166-1 alpha-2).</summary>
        public string? Jurisdiction { get; set; }

        /// <summary>Evidence artefacts retained alongside this decision.</summary>
        public List<IngestionEvidenceArtifact> EvidenceArtifacts { get; set; } = new();

        /// <summary>Reviewer notes appended by human operators.</summary>
        public List<IngestionReviewerNote> ReviewerNotes { get; set; } = new();

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Whether this record was the result of idempotent replay.</summary>
        public bool IsIdempotentReplay { get; set; }

        /// <summary>
        /// Idempotency key supplied at ingestion time.
        /// If the same key is submitted again, the original record is returned.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>Immutable event timeline for this decision record.</summary>
        public List<IngestionTimelineEvent> Timeline { get; set; } = new();
    }

    // ── Timeline event ────────────────────────────────────────────────────────

    /// <summary>
    /// A single auditable event in the lifecycle of an ingested decision.
    /// </summary>
    public class IngestionTimelineEvent
    {
        /// <summary>UTC timestamp when the event occurred.</summary>
        public DateTimeOffset OccurredAt { get; set; }

        /// <summary>Short machine-readable event name (e.g. "DecisionIngested", "StatusChanged", "EvidenceExpired").</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>Human-readable description of the event.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Decision status at the time of the event.</summary>
        public NormalizedIngestionStatus StatusSnapshot { get; set; }

        /// <summary>Actor who triggered the event (user ID or system name).</summary>
        public string Actor { get; set; } = string.Empty;

        /// <summary>Correlation ID propagated from the originating request.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Optional structured metadata for the event.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    // ── Reviewer note ────────────────────────────────────────────────────────

    /// <summary>
    /// A reviewer note appended to an ingested decision record by a human operator.
    /// </summary>
    public class IngestionReviewerNote
    {
        /// <summary>Unique identifier for this note.</summary>
        public string NoteId { get; set; } = string.Empty;

        /// <summary>Actor who submitted the note.</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Free-text content of the note.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the note was appended.</summary>
        public DateTimeOffset AppendedAt { get; set; }

        /// <summary>Optional structured evidence references (label → external reference).</summary>
        public Dictionary<string, string> EvidenceReferences { get; set; } = new();

        /// <summary>Correlation ID for this note's request.</summary>
        public string CorrelationId { get; set; } = string.Empty;
    }

    // ── Blocker ──────────────────────────────────────────────────────────────

    /// <summary>
    /// An explicit issue that prevents or qualifies a subject's launch readiness.
    /// Making blockers explicit means the frontend never needs to infer them.
    /// </summary>
    public class IngestionBlocker
    {
        /// <summary>Machine-readable blocker code (e.g., "EVIDENCE_EXPIRED", "SANCTIONS_MATCH").</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Human-readable title.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Human-readable explanation of what is blocking readiness.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Severity of this blocker.</summary>
        public IngestionBlockerSeverity Severity { get; set; }

        /// <summary>Decision record ID that produced this blocker, if applicable.</summary>
        public string? SourceDecisionId { get; set; }

        /// <summary>Kind of check that produced this blocker, if applicable.</summary>
        public IngestionDecisionKind? SourceDecisionKind { get; set; }

        /// <summary>Suggested remediation step for the operator or subject.</summary>
        public string? RemediationHint { get; set; }

        /// <summary>Whether this blocker prevents launch entirely (true) or is advisory only (false).</summary>
        public bool IsHardBlocker { get; set; }
    }

    // ── Readiness summary ─────────────────────────────────────────────────────

    /// <summary>
    /// Aggregated launch readiness for a single subject across all their ingested decisions.
    /// </summary>
    public class SubjectIngestionReadiness
    {
        /// <summary>Subject identifier.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Derived readiness state.</summary>
        public IngestionReadinessState ReadinessState { get; set; }

        /// <summary>Human-readable summary of the readiness state.</summary>
        public string ReadinessSummary { get; set; } = string.Empty;

        /// <summary>Explicit hard blockers preventing launch.</summary>
        public List<IngestionBlocker> Blockers { get; set; } = new();

        /// <summary>Advisory (non-blocking) issues.</summary>
        public List<IngestionBlocker> Advisories { get; set; } = new();

        /// <summary>Snapshot of the most recent decision per check kind.</summary>
        public Dictionary<IngestionDecisionKind, NormalizedIngestionStatus> CheckSummary { get; set; } = new();

        /// <summary>Earliest evidence expiry date across all decisions for this subject.</summary>
        public DateTimeOffset? EarliestEvidenceExpiry { get; set; }

        /// <summary>True when any evidence item is currently expired.</summary>
        public bool HasExpiredEvidence { get; set; }

        /// <summary>Timestamp when this readiness summary was computed.</summary>
        public DateTimeOffset ComputedAt { get; set; }
    }

    /// <summary>
    /// Aggregated launch readiness summary for a cohort of subjects.
    /// A cohort represents a logical group (e.g., all investors for a token launch).
    /// </summary>
    public class CohortIngestionReadiness
    {
        /// <summary>Cohort identifier.</summary>
        public string CohortId { get; set; } = string.Empty;

        /// <summary>Optional human-readable cohort name.</summary>
        public string? CohortName { get; set; }

        /// <summary>Overall cohort readiness state (most severe across all members).</summary>
        public IngestionReadinessState OverallReadinessState { get; set; }

        /// <summary>Human-readable cohort-level summary.</summary>
        public string ReadinessSummary { get; set; } = string.Empty;

        /// <summary>Total subjects in the cohort.</summary>
        public int TotalSubjects { get; set; }

        /// <summary>Number of subjects in each readiness state.</summary>
        public Dictionary<IngestionReadinessState, int> SubjectCountByState { get; set; } = new();

        /// <summary>Per-subject readiness summaries.</summary>
        public List<SubjectIngestionReadiness> SubjectReadiness { get; set; } = new();

        /// <summary>Cohort-level blockers (at least one subject is hard-blocked).</summary>
        public List<IngestionBlocker> CohortBlockers { get; set; } = new();

        /// <summary>Timestamp when this readiness summary was computed.</summary>
        public DateTimeOffset ComputedAt { get; set; }
    }

    // ── Cohort membership ─────────────────────────────────────────────────────

    /// <summary>Internal record associating a subject with a cohort.</summary>
    public class CohortMembership
    {
        /// <summary>Cohort identifier.</summary>
        public string CohortId { get; set; } = string.Empty;

        /// <summary>Optional cohort display name.</summary>
        public string? CohortName { get; set; }

        /// <summary>Subject identifiers belonging to this cohort.</summary>
        public HashSet<string> SubjectIds { get; set; } = new();
    }

    // ── Request / Response models ─────────────────────────────────────────────

    /// <summary>
    /// Request to ingest a compliance decision from a provider into the normalised store.
    /// The caller is responsible for mapping provider-specific fields to the normalised model.
    /// </summary>
    public class IngestProviderDecisionRequest
    {
        /// <summary>
        /// Identifier of the subject (investor, entity, wallet address).
        /// Required.
        /// </summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>
        /// Issuer or context scope (e.g., token launch ID, organisation ID).
        /// Required.
        /// </summary>
        public string ContextId { get; set; } = string.Empty;

        /// <summary>Kind of compliance check this decision represents. Required.</summary>
        public IngestionDecisionKind Kind { get; set; }

        /// <summary>Provider that originated this decision. Required.</summary>
        public IngestionProviderType Provider { get; set; }

        /// <summary>Provider-assigned reference ID for the underlying check or case.</summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>
        /// Raw status string from the provider, exactly as received.
        /// Preserved for audit; not used in business logic.
        /// </summary>
        public string? ProviderRawStatus { get; set; }

        /// <summary>Normalised decision outcome. Required.</summary>
        public NormalizedIngestionStatus Status { get; set; }

        /// <summary>Timestamp when the provider completed the check.</summary>
        public DateTimeOffset? ProviderCompletedAt { get; set; }

        /// <summary>
        /// How long (in hours) the evidence remains valid.
        /// Zero or null means no expiry is tracked.
        /// </summary>
        public int? EvidenceValidityHours { get; set; }

        /// <summary>
        /// Machine-readable reason code for rejections or flags
        /// (e.g., "SANCTIONS_MATCH", "DOCUMENT_EXPIRED").
        /// </summary>
        public string? ReasonCode { get; set; }

        /// <summary>Human-readable explanation of the decision.</summary>
        public string? Rationale { get; set; }

        /// <summary>Provider-reported confidence score (0–100).</summary>
        public double? ConfidenceScore { get; set; }

        /// <summary>Jurisdiction code (ISO 3166-1 alpha-2), if relevant.</summary>
        public string? Jurisdiction { get; set; }

        /// <summary>Evidence artefacts to retain alongside this decision.</summary>
        public List<IngestEvidenceArtifactRequest>? EvidenceArtifacts { get; set; }

        /// <summary>
        /// Optional idempotency key.
        /// If the same key is submitted again, the original decision record is returned.
        /// </summary>
        public string? IdempotencyKey { get; set; }
    }

    /// <summary>
    /// Inline evidence artefact supplied within an ingestion request.
    /// </summary>
    public class IngestEvidenceArtifactRequest
    {
        /// <summary>Classification of this artefact.</summary>
        public EvidenceArtifactKind Kind { get; set; }

        /// <summary>Human-readable label for the artefact.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Provider-assigned identifier for this artefact.</summary>
        public string? ProviderArtifactId { get; set; }

        /// <summary>URI or pointer to the artefact content.</summary>
        public string? ContentUri { get; set; }

        /// <summary>SHA-256 content hash for integrity verification.</summary>
        public string? ContentHash { get; set; }

        /// <summary>When this artefact's validity expires, if applicable.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        /// <summary>Additional metadata key-value pairs.</summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>Response returned after a successful or failed ingestion attempt.</summary>
    public class IngestProviderDecisionResponse
    {
        /// <summary>True when the ingestion succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The created or replayed decision record.</summary>
        public IngestionDecisionRecord? Decision { get; set; }

        /// <summary>True when the response was produced by idempotent replay.</summary>
        public bool WasIdempotentReplay { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response for retrieving a single ingested decision record.</summary>
    public class GetIngestionDecisionResponse
    {
        /// <summary>True when the record was found.</summary>
        public bool Success { get; set; }

        /// <summary>The retrieved decision record.</summary>
        public IngestionDecisionRecord? Decision { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response for the subject timeline endpoint.</summary>
    public class GetSubjectTimelineResponse
    {
        /// <summary>True when the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Subject identifier.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>
        /// Ordered list of timeline events across all decisions for the subject,
        /// most recent first.
        /// </summary>
        public List<IngestionTimelineEvent> Timeline { get; set; } = new();

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response for the subject blockers endpoint.</summary>
    public class GetSubjectBlockersResponse
    {
        /// <summary>True when the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Subject identifier.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Current hard blockers.</summary>
        public List<IngestionBlocker> HardBlockers { get; set; } = new();

        /// <summary>Current advisory issues.</summary>
        public List<IngestionBlocker> Advisories { get; set; } = new();

        /// <summary>Aggregate readiness state derived from blockers.</summary>
        public IngestionReadinessState ReadinessState { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response for the subject readiness endpoint.</summary>
    public class GetSubjectReadinessResponse
    {
        /// <summary>True when the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Computed readiness summary for the subject.</summary>
        public SubjectIngestionReadiness? Readiness { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response for the cohort readiness endpoint.</summary>
    public class GetCohortReadinessResponse
    {
        /// <summary>True when the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Computed cohort-level readiness summary.</summary>
        public CohortIngestionReadiness? CohortReadiness { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Request to add or update a cohort (upsert).</summary>
    public class UpsertCohortRequest
    {
        /// <summary>Cohort identifier. Required.</summary>
        public string CohortId { get; set; } = string.Empty;

        /// <summary>Optional human-readable cohort name.</summary>
        public string? CohortName { get; set; }

        /// <summary>Subject IDs to add to this cohort.</summary>
        public List<string> SubjectIds { get; set; } = new();
    }

    /// <summary>Response after upserting a cohort.</summary>
    public class UpsertCohortResponse
    {
        /// <summary>True when the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Cohort identifier.</summary>
        public string? CohortId { get; set; }

        /// <summary>Total subject count after the upsert.</summary>
        public int SubjectCount { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response for listing all ingested decisions for a subject.</summary>
    public class ListSubjectDecisionsResponse
    {
        /// <summary>True when the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Subject identifier.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>All ingested decision records for the subject, most recent first.</summary>
        public List<IngestionDecisionRecord> Decisions { get; set; } = new();

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Request to append a reviewer note to an ingested decision.</summary>
    public class AppendIngestionReviewerNoteRequest
    {
        /// <summary>Free-text content of the note. Required.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Optional structured evidence references (label → external reference).</summary>
        public Dictionary<string, string>? EvidenceReferences { get; set; }
    }

    /// <summary>Response after appending a reviewer note.</summary>
    public class AppendIngestionReviewerNoteResponse
    {
        /// <summary>True when the note was appended successfully.</summary>
        public bool Success { get; set; }

        /// <summary>The created reviewer note.</summary>
        public IngestionReviewerNote? Note { get; set; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing.</summary>
        public string? CorrelationId { get; set; }
    }
}
