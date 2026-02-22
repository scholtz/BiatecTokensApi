namespace BiatecTokensApi.Models.Orchestration
{
    /// <summary>
    /// Result produced by the orchestration pipeline for a single token workflow execution
    /// </summary>
    public class OrchestrationResult<TPayload>
    {
        /// <summary>
        /// Indicates whether the entire pipeline completed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The stage at which the pipeline completed or failed
        /// </summary>
        public OrchestrationStage CompletedAtStage { get; set; }

        /// <summary>
        /// Correlation ID from the originating request – propagated to response headers
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Idempotency key used for this execution (null if not provided by caller)
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>
        /// Whether this result was served from the idempotency cache (duplicate request)
        /// </summary>
        public bool IsIdempotentReplay { get; set; }

        /// <summary>
        /// Structured error code when the pipeline fails (maps to <see cref="ErrorCodes"/> constants)
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Human-readable error message for the caller
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Actionable hint to help callers resolve failures
        /// </summary>
        public string? RemediationHint { get; set; }

        /// <summary>
        /// Failure category used to determine retry guidance
        /// </summary>
        public OrchestrationFailureCategory FailureCategory { get; set; }

        /// <summary>
        /// The actual operation result when the pipeline succeeds
        /// </summary>
        public TPayload? Payload { get; set; }

        /// <summary>
        /// Ordered list of stage markers recorded during pipeline execution
        /// </summary>
        public List<OrchestrationStageMarker> StageMarkers { get; set; } = new();

        /// <summary>
        /// Policy decisions made during the pipeline run
        /// </summary>
        public List<OrchestrationPolicyDecision> PolicyDecisions { get; set; } = new();

        /// <summary>
        /// Audit summary for compliance evidence – always populated regardless of success/failure
        /// </summary>
        public OrchestrationAuditSummary AuditSummary { get; set; } = new();

        /// <summary>
        /// UTC timestamp when the pipeline completed
        /// </summary>
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total elapsed time of the pipeline in milliseconds
        /// </summary>
        public long TotalDurationMs { get; set; }
    }

    /// <summary>
    /// Classifies the reason for a pipeline failure to drive retry guidance and user messaging
    /// </summary>
    public enum OrchestrationFailureCategory
    {
        /// <summary>No failure – pipeline succeeded</summary>
        None = 0,

        /// <summary>
        /// Request did not pass schema or invariant validation.
        /// Not retryable – caller must fix input.
        /// </summary>
        ValidationFailure = 1,

        /// <summary>
        /// A precondition (KYC, subscription, compliance) was not met.
        /// Retryable after user remediation.
        /// </summary>
        PreconditionFailure = 2,

        /// <summary>
        /// A transient infrastructure error occurred during execution (RPC timeout, IPFS unavailable).
        /// Retryable with exponential back-off.
        /// </summary>
        TransientInfrastructureFailure = 3,

        /// <summary>
        /// The operation could not be executed due to a permanent business rule violation.
        /// Not retryable without policy change.
        /// </summary>
        PolicyFailure = 4,

        /// <summary>
        /// Post-commit verification failed – the operation was submitted but the expected
        /// outcome was not confirmed on-chain or in storage.
        /// Retryable after operator investigation.
        /// </summary>
        PostCommitVerificationFailure = 5,

        /// <summary>
        /// An unclassified terminal error occurred.
        /// Not retryable – requires operator investigation.
        /// </summary>
        TerminalExecutionFailure = 6
    }

    /// <summary>
    /// Audit summary generated for each orchestration run – used as compliance evidence
    /// </summary>
    public class OrchestrationAuditSummary
    {
        /// <summary>Correlation ID of the request</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Operation type that was executed</summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>User who initiated the workflow</summary>
        public string? InitiatedBy { get; set; }

        /// <summary>UTC timestamp when the workflow was initiated</summary>
        public DateTime InitiatedAt { get; set; }

        /// <summary>UTC timestamp when the workflow completed (success or failure)</summary>
        public DateTime CompletedAt { get; set; }

        /// <summary>Final outcome: Succeeded or Failed</summary>
        public string Outcome { get; set; } = string.Empty;

        /// <summary>Stage at which the pipeline ended</summary>
        public string CompletedAtStage { get; set; } = string.Empty;

        /// <summary>Error code if the pipeline failed</summary>
        public string? FailureCode { get; set; }

        /// <summary>Count of stages successfully completed</summary>
        public int StagesCompleted { get; set; }

        /// <summary>Count of policy decisions made</summary>
        public int PolicyDecisionCount { get; set; }

        /// <summary>Whether idempotency key was present</summary>
        public bool HasIdempotencyKey { get; set; }

        /// <summary>Whether this was a replay of a previously seen idempotency key</summary>
        public bool WasIdempotentReplay { get; set; }
    }
}
