using BiatecTokensApi.Models.ARC76;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for checking ARC76 account readiness for backend-managed token operations
    /// </summary>
    public interface IARC76AccountReadinessService
    {
        /// <summary>
        /// Checks if an ARC76 account is ready for operations
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="correlationId">Correlation ID for tracking</param>
        /// <returns>Account readiness result</returns>
        Task<ARC76AccountReadinessResult> CheckAccountReadinessAsync(string userId, string? correlationId = null);

        /// <summary>
        /// Initializes an ARC76 account (if not already initialized)
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="correlationId">Correlation ID for tracking</param>
        /// <returns>True if initialization was successful or already complete</returns>
        Task<bool> InitializeAccountAsync(string userId, string? correlationId = null);

        /// <summary>
        /// Validates account metadata and key accessibility
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <returns>True if validation passes</returns>
        Task<bool> ValidateAccountIntegrityAsync(string userId);

        /// <summary>
        /// Gets the current readiness state for a user
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <returns>Current readiness state</returns>
        Task<ARC76ReadinessState> GetReadinessStateAsync(string userId);
    }
}
