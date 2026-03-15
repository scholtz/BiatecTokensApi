namespace BiatecTokensApi.Models.ComplianceOrchestration
{
    /// <summary>
    /// Represents the state of a compliance decision
    /// </summary>
    public enum ComplianceDecisionState
    {
        /// <summary>Check initiated but not yet complete</summary>
        Pending = 0,
        /// <summary>Subject passed all compliance checks</summary>
        Approved = 1,
        /// <summary>Subject failed one or more compliance checks</summary>
        Rejected = 2,
        /// <summary>Manual review required before a final decision</summary>
        NeedsReview = 3,
        /// <summary>An internal error occurred during the compliance check</summary>
        Error = 4,
        /// <summary>The screening provider was unavailable; the check could not be completed (fail-closed)</summary>
        ProviderUnavailable = 5,
        /// <summary>A previously-approved decision has exceeded its validity window and must be renewed</summary>
        Expired = 6,
        /// <summary>Insufficient subject data was provided to complete the check</summary>
        InsufficientData = 7
    }

    /// <summary>
    /// Classifies the screening subject as an individual person or a business/legal entity.
    /// </summary>
    public enum ScreeningSubjectType
    {
        /// <summary>Natural person (default)</summary>
        Individual = 0,
        /// <summary>Legal entity, company, or business organisation</summary>
        BusinessEntity = 1
    }

    /// <summary>
    /// Type of compliance check to perform
    /// </summary>
    public enum ComplianceCheckType
    {
        /// <summary>KYC (Know Your Customer) check only</summary>
        Kyc = 0,
        /// <summary>AML (Anti-Money Laundering / sanctions screening) check only</summary>
        Aml = 1,
        /// <summary>Combined KYC + AML check</summary>
        Combined = 2
    }

    /// <summary>
    /// Standardized error codes returned by compliance providers
    /// </summary>
    public enum ComplianceProviderErrorCode
    {
        /// <summary>No error</summary>
        None = 0,
        /// <summary>Provider call timed out</summary>
        Timeout = 1,
        /// <summary>Provider service is unavailable</summary>
        ProviderUnavailable = 2,
        /// <summary>Provider returned a response that could not be parsed</summary>
        MalformedResponse = 3,
        /// <summary>Internal error within the orchestration service</summary>
        InternalError = 4,
        /// <summary>The request sent to the provider was invalid</summary>
        InvalidRequest = 5
    }

    /// <summary>
    /// A reviewer note or evidence reference appended to a compliance decision by an operator.
    /// </summary>
    public class ComplianceReviewerNote
    {
        /// <summary>Unique identifier for this note</summary>
        public string NoteId { get; set; } = string.Empty;

        /// <summary>The decision ID this note belongs to</summary>
        public string DecisionId { get; set; } = string.Empty;

        /// <summary>Actor (user/operator) who submitted the note</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Free-text content of the note</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Optional structured evidence references (e.g. document IDs, external links)</summary>
        public Dictionary<string, string> EvidenceReferences { get; set; } = new();

        /// <summary>UTC timestamp when the note was appended</summary>
        public DateTimeOffset AppendedAt { get; set; }

        /// <summary>Correlation ID propagated from the originating request</summary>
        public string CorrelationId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to append a reviewer note or evidence reference to an existing compliance decision.
    /// </summary>
    public class AppendReviewerNoteRequest
    {
        /// <summary>
        /// Free-text note from the reviewer/operator.
        /// Required; must be non-empty.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Optional structured evidence references keyed by label
        /// (e.g. "passport_scan" → "doc-id-xyz", "sanction_list_version" → "2026-Q1").
        /// </summary>
        public Dictionary<string, string>? EvidenceReferences { get; set; }
    }

    /// <summary>
    /// Response after successfully appending a reviewer note to a compliance decision.
    /// </summary>
    public class AppendReviewerNoteResponse
    {
        /// <summary>Whether the operation succeeded</summary>
        public bool Success { get; set; }

        /// <summary>The newly created note (populated on success)</summary>
        public ComplianceReviewerNote? Note { get; set; }

        /// <summary>Error message when Success is false</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code when Success is false</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Represents a single auditable event in the compliance decision lifecycle
    /// </summary>
    public class ComplianceAuditEvent
    {
        /// <summary>UTC timestamp of the event</summary>
        public DateTimeOffset OccurredAt { get; set; }

        /// <summary>Type/name of the event (e.g. "CheckInitiated", "KycCompleted", "AmlCompleted")</summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>State of the decision at the time of the event</summary>
        public ComplianceDecisionState State { get; set; }

        /// <summary>Correlation ID propagated from the originating request</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Optional provider reference identifier</summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>Optional reason or message associated with the event</summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// Normalized representation of a compliance decision stored in-memory
    /// </summary>
    public class NormalizedComplianceDecision
    {
        /// <summary>Unique identifier for this decision</summary>
        public string DecisionId { get; set; } = string.Empty;

        /// <summary>Subject identifier (e.g. user ID or wallet address)</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Context identifier provided by the caller (e.g. token issuance ID)</summary>
        public string ContextId { get; set; } = string.Empty;

        /// <summary>Type of check that was performed</summary>
        public ComplianceCheckType CheckType { get; set; }

        /// <summary>Current state of the decision</summary>
        public ComplianceDecisionState State { get; set; }

        /// <summary>Reason code from the provider (if any)</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Provider error code when state is Error</summary>
        public ComplianceProviderErrorCode ProviderErrorCode { get; set; }

        /// <summary>KYC provider reference ID (if KYC was performed)</summary>
        public string? KycProviderReferenceId { get; set; }

        /// <summary>AML provider reference ID (if AML was performed)</summary>
        public string? AmlProviderReferenceId { get; set; }

        /// <summary>Correlation ID from the originating request</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>When the check was initiated</summary>
        public DateTimeOffset InitiatedAt { get; set; }

        /// <summary>When the check reached a terminal state (null if still pending)</summary>
        public DateTimeOffset? CompletedAt { get; set; }

        /// <summary>
        /// UTC timestamp after which this decision evidence is considered stale and the
        /// decision transitions to <see cref="ComplianceDecisionState.Expired"/> on next retrieval.
        /// Null means the decision does not expire.
        /// </summary>
        public DateTimeOffset? EvidenceExpiresAt { get; set; }

        /// <summary>Classifies the screened subject as an individual or a business entity</summary>
        public ScreeningSubjectType SubjectType { get; set; }

        /// <summary>
        /// Watchlist or sanctions categories that were matched during AML screening
        /// (e.g. "OFAC_SDN", "EU_SANCTIONS", "PEP_WATCHLIST"). Empty when no matches found.
        /// </summary>
        public List<string> MatchedWatchlistCategories { get; set; } = new();

        /// <summary>
        /// Provider-reported confidence score for the screening result (0.0–1.0).
        /// Null when the provider does not supply a score.
        /// </summary>
        public decimal? ConfidenceScore { get; set; }

        /// <summary>Ordered list of audit events for this decision</summary>
        public List<ComplianceAuditEvent> AuditTrail { get; set; } = new();

        /// <summary>Ordered list of reviewer notes appended by operators</summary>
        public List<ComplianceReviewerNote> ReviewerNotes { get; set; } = new();

        /// <summary>Whether this response was served from the idempotency cache</summary>
        public bool IsIdempotentReplay { get; set; }
    }

    /// <summary>
    /// Request to initiate a compliance check
    /// </summary>
    public class InitiateComplianceCheckRequest
    {
        /// <summary>
        /// Identifier of the subject being checked (e.g. user ID, wallet address).
        /// Required.
        /// </summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>
        /// Caller-supplied context identifier used for idempotency (e.g. token issuance ID).
        /// Required.
        /// </summary>
        public string ContextId { get; set; } = string.Empty;

        /// <summary>
        /// Type of compliance check to perform.
        /// </summary>
        public ComplianceCheckType CheckType { get; set; } = ComplianceCheckType.Combined;

        /// <summary>
        /// Optional subject metadata forwarded to providers (e.g. full_name, country).
        /// For business entities, include keys such as <c>legal_name</c>, <c>registration_number</c>,
        /// <c>jurisdiction</c>, and <c>ultimate_beneficial_owner</c>.
        /// </summary>
        public Dictionary<string, string> SubjectMetadata { get; set; } = new();

        /// <summary>
        /// Classifies the subject as an individual person or a business/legal entity.
        /// Defaults to <see cref="ScreeningSubjectType.Individual"/>.
        /// </summary>
        public ScreeningSubjectType SubjectType { get; set; } = ScreeningSubjectType.Individual;

        /// <summary>
        /// Optional evidence validity window in hours. When set, the completed decision will
        /// carry an <c>EvidenceExpiresAt</c> timestamp that many hours in the future. When that
        /// timestamp is reached the decision transitions to
        /// <see cref="ComplianceDecisionState.Expired"/> on next retrieval.
        /// Set to <c>0</c> or omit to disable expiry.
        /// </summary>
        public int? EvidenceValidityHours { get; set; }

        /// <summary>
        /// Optional explicit idempotency key. When not provided, a key is derived from
        /// SubjectId + ContextId + CheckType.
        /// </summary>
        public string? IdempotencyKey { get; set; }
    }

    /// <summary>
    /// Response from a compliance check operation
    /// </summary>
    public class ComplianceCheckResponse
    {
        /// <summary>Whether the operation itself succeeded (not whether the subject passed)</summary>
        public bool Success { get; set; }

        /// <summary>Unique decision identifier</summary>
        public string? DecisionId { get; set; }

        /// <summary>Current state of the compliance decision</summary>
        public ComplianceDecisionState State { get; set; }

        /// <summary>Reason code from the provider (if any)</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Provider error code when state is Error</summary>
        public ComplianceProviderErrorCode ProviderErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Error message when Success is false</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code when Success is false</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Whether this response was served from the idempotency cache</summary>
        public bool IsIdempotentReplay { get; set; }

        /// <summary>When the check was initiated</summary>
        public DateTimeOffset? InitiatedAt { get; set; }

        /// <summary>When the check reached a terminal state</summary>
        public DateTimeOffset? CompletedAt { get; set; }

        /// <summary>
        /// UTC timestamp after which this evidence is considered stale.
        /// Null means the decision does not expire.
        /// </summary>
        public DateTimeOffset? EvidenceExpiresAt { get; set; }

        /// <summary>Classifies the screened subject as an individual or a business entity</summary>
        public ScreeningSubjectType SubjectType { get; set; }

        /// <summary>
        /// Watchlist or sanctions categories that were matched during AML screening.
        /// Empty list when no matches were found.
        /// </summary>
        public List<string> MatchedWatchlistCategories { get; set; } = new();

        /// <summary>
        /// Provider-reported confidence score for the screening result (0.0–1.0).
        /// Null when the provider does not supply a score.
        /// </summary>
        public decimal? ConfidenceScore { get; set; }

        /// <summary>Audit trail events for this decision</summary>
        public List<ComplianceAuditEvent> AuditTrail { get; set; } = new();

        /// <summary>Reviewer notes appended by operators for this decision</summary>
        public List<ComplianceReviewerNote> ReviewerNotes { get; set; } = new();
    }

    /// <summary>
    /// Response for a status query on a specific decision
    /// </summary>
    public class ComplianceCheckStatusResponse
    {
        /// <summary>Whether the status retrieval succeeded</summary>
        public bool Success { get; set; }

        /// <summary>The decision (null if not found)</summary>
        public ComplianceCheckResponse? Decision { get; set; }

        /// <summary>Error message when Success is false</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code when Success is false</summary>
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Response for a decision history query for a given subject
    /// </summary>
    public class ComplianceDecisionHistoryResponse
    {
        /// <summary>Whether the history retrieval succeeded</summary>
        public bool Success { get; set; }

        /// <summary>Subject identifier that was queried</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>All decisions for this subject (ordered by InitiatedAt descending)</summary>
        public List<ComplianceCheckResponse> Decisions { get; set; } = new();

        /// <summary>Total number of decisions found</summary>
        public int TotalCount { get; set; }

        /// <summary>Error message when Success is false</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request to rescreen a subject whose evidence is stale or expired.
    /// A rescreen creates a new compliance decision linked to the original via <see cref="PreviousDecisionId"/>.
    /// </summary>
    public class RescreenRequest
    {
        /// <summary>
        /// Optional override for the check type. When omitted, the same check type as the
        /// original decision is used.
        /// </summary>
        public ComplianceCheckType? CheckType { get; set; }

        /// <summary>
        /// Optional updated subject metadata for the new check (e.g. updated name or document).
        /// When omitted, the metadata from the original decision is reused.
        /// </summary>
        public Dictionary<string, string>? SubjectMetadata { get; set; }

        /// <summary>
        /// Optional evidence validity window in hours for the new decision.
        /// When omitted, the same window as the original is used.
        /// </summary>
        public int? EvidenceValidityHours { get; set; }

        /// <summary>
        /// Reason the rescreen was triggered (e.g. "EvidenceExpired", "OperatorRequested").
        /// Used for audit trail annotation.
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Response from a rescreen operation.
    /// </summary>
    public class RescreenResponse
    {
        /// <summary>Whether the rescreen was successfully initiated</summary>
        public bool Success { get; set; }

        /// <summary>The new compliance check response (populated on success)</summary>
        public ComplianceCheckResponse? NewDecision { get; set; }

        /// <summary>The original decision ID that triggered this rescreen</summary>
        public string? PreviousDecisionId { get; set; }

        /// <summary>Error message when Success is false</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code when Success is false</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Inbound provider webhook/callback payload for updating a compliance decision.
    /// The orchestration layer accepts normalised callbacks from any registered provider
    /// and maps them to the decision identified by <see cref="ProviderReferenceId"/>.
    /// </summary>
    public class ProviderCallbackRequest
    {
        /// <summary>
        /// Name of the provider sending the callback (e.g. "StripeIdentity", "ComplyAdvantage",
        /// "Mock"). Used to select the correct signature-validation logic.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// The provider-issued reference ID that links this callback to a compliance decision.
        /// Must match the <c>KycProviderReferenceId</c> or <c>AmlProviderReferenceId</c>
        /// stored on the decision.
        /// </summary>
        public string ProviderReferenceId { get; set; } = string.Empty;

        /// <summary>
        /// Provider-specific event type string (e.g. "verification.session.verified",
        /// "alert.created"). Used to determine the resulting compliance state.
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Normalised outcome from the provider:
        /// "approved", "rejected", "needs_review", "error", or "pending".
        /// The orchestration layer maps this to <see cref="ComplianceDecisionState"/>.
        /// </summary>
        public string OutcomeStatus { get; set; } = string.Empty;

        /// <summary>
        /// Optional reason code accompanying the outcome.
        /// </summary>
        public string? ReasonCode { get; set; }

        /// <summary>
        /// Optional free-text message from the provider about the outcome.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Optional HMAC-SHA256 signature for request authenticity validation.
        /// Pass the raw signature string (hex or base64) as provided by the provider.
        /// </summary>
        public string? Signature { get; set; }

        /// <summary>
        /// Optional idempotency key for the callback event.
        /// When the same key is seen twice, the second call is accepted without re-processing.
        /// </summary>
        public string? IdempotencyKey { get; set; }
    }

    /// <summary>
    /// Response from processing a provider webhook/callback.
    /// </summary>
    public class ProviderCallbackResponse
    {
        /// <summary>Whether the callback was successfully processed</summary>
        public bool Success { get; set; }

        /// <summary>Decision ID that was updated (populated on success)</summary>
        public string? DecisionId { get; set; }

        /// <summary>New state of the decision after the callback was applied</summary>
        public ComplianceDecisionState? NewState { get; set; }

        /// <summary>
        /// True when the callback was valid but had already been processed (idempotent replay).
        /// The decision is unchanged.
        /// </summary>
        public bool IsIdempotentReplay { get; set; }

        /// <summary>Error message when Success is false</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code when Success is false</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Correlation ID for end-to-end tracing</summary>
        public string? CorrelationId { get; set; }
    }
}
