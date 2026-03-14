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
        /// <summary>An error occurred during the compliance check</summary>
        Error = 4
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
        /// </summary>
        public Dictionary<string, string> SubjectMetadata { get; set; } = new();

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
}
