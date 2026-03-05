using BiatecTokensApi.Models.DeterministicOrchestration;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for deterministic deployment orchestration with idempotency,
    /// compliance validation pipeline, structured error payloads, and audit logging.
    /// </summary>
    public interface IDeterministicOrchestrationService
    {
        /// <summary>Starts a new deterministic orchestration (idempotent under duplicate keys).</summary>
        Task<OrchestrationResponse> OrchestrateAsync(OrchestrationRequest request);

        /// <summary>Returns the current status of an orchestration, or null when not found.</summary>
        Task<OrchestrationStatusResponse?> GetStatusAsync(string orchestrationId, string? correlationId);

        /// <summary>Advances the orchestration to its next valid lifecycle stage.</summary>
        Task<OrchestrationAdvanceResponse> AdvanceAsync(OrchestrationAdvanceRequest request);

        /// <summary>Runs the compliance-check pipeline for the specified orchestration.</summary>
        Task<ComplianceCheckResponse> RunComplianceCheckAsync(ComplianceCheckRequest request);

        /// <summary>Cancels an in-progress orchestration.</summary>
        Task<OrchestrationCancelResponse> CancelAsync(OrchestrationCancelRequest request);

        /// <summary>Returns audit events, optionally filtered by orchestration ID or correlation ID.</summary>
        IReadOnlyList<OrchestrationAuditEntry> GetAuditEvents(string? orchestrationId = null, string? correlationId = null);
    }
}
