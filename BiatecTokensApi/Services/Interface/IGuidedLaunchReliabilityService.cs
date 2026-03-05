using BiatecTokensApi.Models.GuidedLaunchReliability;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>Service for guided token launch reliability, compliance UX, and CI determinism.</summary>
    public interface IGuidedLaunchReliabilityService
    {
        /// <summary>Initiates a new guided launch with idempotency support.</summary>
        Task<GuidedLaunchInitiateResponse> InitiateLaunchAsync(GuidedLaunchInitiateRequest request);

        /// <summary>Returns the current status of a guided launch, or null if not found.</summary>
        Task<GuidedLaunchStatusResponse?> GetLaunchStatusAsync(string launchId, string? correlationId);

        /// <summary>Advances the guided launch to the next stage.</summary>
        Task<GuidedLaunchAdvanceResponse> AdvanceLaunchAsync(GuidedLaunchAdvanceRequest request);

        /// <summary>Validates a specific step's inputs before advancing.</summary>
        Task<GuidedLaunchValidateStepResponse> ValidateStepAsync(GuidedLaunchValidateStepRequest request);

        /// <summary>Cancels an in-progress guided launch.</summary>
        Task<GuidedLaunchCancelResponse> CancelLaunchAsync(GuidedLaunchCancelRequest request);

        /// <summary>Returns audit events, optionally filtered by correlationId.</summary>
        IReadOnlyList<GuidedLaunchAuditEvent> GetAuditEvents(string? correlationId = null);
    }
}
