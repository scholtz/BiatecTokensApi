using BiatecTokensApi.Models.ARC76MVPPipeline;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for ARC76 MVP deployment pipeline with ARC76 readiness enforcement,
    /// idempotency guarantees, compliance traceability, and failure classification.
    /// </summary>
    public interface IARC76MVPDeploymentPipelineService
    {
        /// <summary>Initiates a new pipeline (idempotent via IdempotencyKey).</summary>
        Task<PipelineInitiateResponse> InitiateAsync(PipelineInitiateRequest request);

        /// <summary>Returns the current status of a pipeline, or null if not found.</summary>
        Task<PipelineStatusResponse?> GetStatusAsync(string pipelineId, string? correlationId);

        /// <summary>Advances a pipeline to its next lifecycle stage.</summary>
        Task<PipelineAdvanceResponse> AdvanceAsync(PipelineAdvanceRequest request);

        /// <summary>Cancels a non-terminal pipeline.</summary>
        Task<PipelineCancelResponse> CancelAsync(PipelineCancelRequest request);

        /// <summary>Retries a failed pipeline.</summary>
        Task<PipelineRetryResponse> RetryAsync(PipelineRetryRequest request);

        /// <summary>Returns the structured audit response for a pipeline.</summary>
        Task<PipelineAuditResponse> GetAuditAsync(string pipelineId, string? correlationId);

        /// <summary>Returns audit events filtered by pipeline ID and/or correlation ID.</summary>
        IReadOnlyList<PipelineAuditEntry> GetAuditEvents(string? pipelineId = null, string? correlationId = null);
    }
}
