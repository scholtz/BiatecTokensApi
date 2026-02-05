using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for managing compliance capability matrix
    /// </summary>
    public interface ICapabilityMatrixService
    {
        /// <summary>
        /// Gets the complete capability matrix with optional filtering
        /// </summary>
        /// <param name="request">Optional filters for jurisdiction, wallet type, token standard, and KYC tier</param>
        /// <returns>Response with the capability matrix data</returns>
        Task<CapabilityMatrixResponse> GetCapabilityMatrixAsync(GetCapabilityMatrixRequest? request = null);

        /// <summary>
        /// Checks if a specific action is allowed based on capability rules
        /// </summary>
        /// <param name="request">The capability check request</param>
        /// <returns>Response indicating if the action is allowed and required checks</returns>
        Task<CapabilityCheckResponse> CheckCapabilityAsync(CapabilityCheckRequest request);

        /// <summary>
        /// Reloads the capability matrix configuration from file
        /// </summary>
        /// <returns>True if reload was successful</returns>
        Task<bool> ReloadConfigurationAsync();

        /// <summary>
        /// Gets the current configuration version
        /// </summary>
        /// <returns>The version string</returns>
        string GetVersion();
    }
}
