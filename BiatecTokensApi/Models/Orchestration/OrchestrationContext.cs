namespace BiatecTokensApi.Models.Orchestration
{
    /// <summary>
    /// Context object passed through all stages of the orchestration pipeline
    /// </summary>
    public class OrchestrationContext
    {
        /// <summary>
        /// Unique correlation ID for end-to-end tracing across services and logs.
        /// Taken from the X-Correlation-ID request header or auto-generated.
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Idempotency key supplied by the caller to prevent duplicate operations.
        /// When present, the pipeline enforces deterministic duplicate-response semantics.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>
        /// The token workflow operation type (e.g. "ERC20_MINTABLE_CREATE", "ARC3_CREATE")
        /// </summary>
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Authenticated user identifier – populated from JWT claims or ARC-0014 address
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// UTC timestamp when the pipeline was initiated
        /// </summary>
        public DateTime InitiatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Stage-level telemetry markers recorded as the pipeline progresses
        /// </summary>
        public List<OrchestrationStageMarker> StageMarkers { get; } = new();

        /// <summary>
        /// Policy decisions recorded during the pipeline execution
        /// </summary>
        public List<OrchestrationPolicyDecision> PolicyDecisions { get; } = new();

        /// <summary>
        /// The stage currently executing
        /// </summary>
        public OrchestrationStage CurrentStage { get; set; } = OrchestrationStage.NotStarted;

        /// <summary>
        /// Additional metadata attached by pipeline stages
        /// </summary>
        public Dictionary<string, object> Metadata { get; } = new();
    }

    /// <summary>
    /// Represents a timestamped marker emitted when a pipeline stage starts or finishes
    /// </summary>
    public class OrchestrationStageMarker
    {
        /// <summary>Stage that was entered</summary>
        public OrchestrationStage Stage { get; set; }

        /// <summary>UTC timestamp of the marker</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Whether the stage completed successfully</summary>
        public bool Success { get; set; }

        /// <summary>Optional message associated with the marker</summary>
        public string? Message { get; set; }

        /// <summary>Duration of this stage in milliseconds</summary>
        public long DurationMs { get; set; }
    }

    /// <summary>
    /// Represents a policy decision recorded during orchestration
    /// </summary>
    public class OrchestrationPolicyDecision
    {
        /// <summary>Name of the policy dimension evaluated</summary>
        public string PolicyName { get; set; } = string.Empty;

        /// <summary>Outcome of the policy evaluation: Pass, Warning, or Fail</summary>
        public string Outcome { get; set; } = string.Empty;

        /// <summary>Reason for the policy decision</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>UTC timestamp of the decision</summary>
        public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
    }
}
