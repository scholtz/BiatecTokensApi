using BiatecTokensApi.Models.TokenDeploymentLifecycle;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for the deterministic token deployment lifecycle.
    ///
    /// Provides idempotent deployment initiation, progress tracking, retry handling,
    /// telemetry emission, input validation, and reliability guardrail evaluation.
    ///
    /// All operations are deterministic: identical inputs always produce identical results.
    /// Retried operations are safe and do not create duplicate resources.
    /// </summary>
    public interface ITokenDeploymentLifecycleService
    {
        /// <summary>
        /// Initiates a new token deployment or returns a cached result for an idempotent replay.
        /// Validates inputs, evaluates reliability guardrails, and transitions the deployment
        /// through its lifecycle stages.
        /// </summary>
        /// <param name="request">Deployment request including token parameters and idempotency key.</param>
        /// <returns>Deployment lifecycle response with stage, idempotency status, telemetry, and guardrails.</returns>
        Task<TokenDeploymentLifecycleResponse> InitiateDeploymentAsync(TokenDeploymentLifecycleRequest request);

        /// <summary>
        /// Returns the current status of a deployment by its deployment ID.
        /// </summary>
        /// <param name="deploymentId">Deployment ID returned by <see cref="InitiateDeploymentAsync"/>.</param>
        /// <param name="correlationId">Optional correlation ID for tracing.</param>
        /// <returns>Current lifecycle response, or a not-found response when the ID is unknown.</returns>
        Task<TokenDeploymentLifecycleResponse> GetDeploymentStatusAsync(
            string deploymentId,
            string? correlationId = null);

        /// <summary>
        /// Retries a previously failed or timed-out deployment.
        /// Honours the configured MaxRetryAttempts limit and returns a terminal failure
        /// response when the limit has been exceeded.
        /// </summary>
        /// <param name="request">Retry request including the original idempotency key.</param>
        /// <returns>Updated deployment lifecycle response.</returns>
        Task<TokenDeploymentLifecycleResponse> RetryDeploymentAsync(DeploymentRetryRequest request);

        /// <summary>
        /// Returns all telemetry events emitted during the lifecycle of a deployment.
        /// Events include stage transitions, guardrail triggers, idempotency hits, and dependency failures.
        /// </summary>
        /// <param name="deploymentId">Deployment ID to retrieve telemetry for.</param>
        /// <param name="correlationId">Optional correlation ID for tracing.</param>
        /// <returns>Ordered list of telemetry events.</returns>
        Task<List<DeploymentTelemetryEvent>> GetTelemetryEventsAsync(
            string deploymentId,
            string? correlationId = null);

        /// <summary>
        /// Validates deployment inputs before initiating the deployment pipeline.
        /// Returns deterministic validation results and reliability guardrail findings
        /// without committing any state or submitting any transactions.
        /// </summary>
        /// <param name="request">Validation-only request.</param>
        /// <returns>Validation response with field-level results and guardrail findings.</returns>
        Task<DeploymentValidationResponse> ValidateDeploymentInputsAsync(DeploymentValidationRequest request);

        /// <summary>
        /// Evaluates reliability guardrails for the supplied context.
        /// Returns an ordered list of findings from Info through Error severity.
        /// </summary>
        /// <param name="context">Evaluation context describing environment and deployment parameters.</param>
        /// <returns>Ordered list of guardrail findings.</returns>
        List<ReliabilityGuardrail> EvaluateReliabilityGuardrails(GuardrailEvaluationContext context);

        /// <summary>
        /// Derives a deterministic deployment ID from the supplied idempotency key and network.
        /// </summary>
        /// <param name="idempotencyKey">Caller-supplied or derived idempotency key.</param>
        /// <param name="network">Target network identifier.</param>
        /// <returns>Deterministic deployment ID string.</returns>
        string DeriveDeploymentId(string idempotencyKey, string network);

        /// <summary>
        /// Builds a deployment progress snapshot for the current stage.
        /// </summary>
        /// <param name="stage">Current deployment stage.</param>
        /// <param name="retryCount">Number of retries consumed.</param>
        /// <returns>Progress snapshot with percentage and stage statuses.</returns>
        DeploymentProgress BuildProgress(DeploymentStage stage, int retryCount);
    }
}
