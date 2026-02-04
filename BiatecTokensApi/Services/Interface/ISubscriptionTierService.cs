using BiatecTokensApi.Models.Subscription;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for validating subscription tier limits for whitelist operations
    /// </summary>
    public interface ISubscriptionTierService
    {
        /// <summary>
        /// Gets the subscription tier for a user/address
        /// </summary>
        /// <param name="userAddress">The user's Algorand address</param>
        /// <returns>The subscription tier</returns>
        Task<SubscriptionTier> GetUserTierAsync(string userAddress);

        /// <summary>
        /// Validates if a whitelist operation is allowed for the user's subscription tier
        /// </summary>
        /// <param name="userAddress">The user's Algorand address</param>
        /// <param name="assetId">The asset ID</param>
        /// <param name="currentCount">Current number of whitelisted addresses</param>
        /// <param name="additionalCount">Number of addresses to add</param>
        /// <returns>Validation result with allowed status and reason if denied</returns>
        Task<SubscriptionTierValidationResult> ValidateOperationAsync(
            string userAddress, 
            ulong assetId, 
            int currentCount, 
            int additionalCount = 1);

        /// <summary>
        /// Checks if bulk operations are enabled for the user's tier
        /// </summary>
        /// <param name="userAddress">The user's Algorand address</param>
        /// <returns>True if bulk operations are enabled</returns>
        Task<bool> IsBulkOperationEnabledAsync(string userAddress);

        /// <summary>
        /// Checks if audit log access is enabled for the user's tier
        /// </summary>
        /// <param name="userAddress">The user's Algorand address</param>
        /// <returns>True if audit log access is enabled</returns>
        Task<bool> IsAuditLogEnabledAsync(string userAddress);

        /// <summary>
        /// Gets tier limits for a specific tier
        /// </summary>
        /// <param name="tier">The subscription tier</param>
        /// <returns>Tier limits configuration</returns>
        SubscriptionTierLimits GetTierLimits(SubscriptionTier tier);

        /// <summary>
        /// Gets the remaining capacity for a user on a specific asset
        /// </summary>
        /// <param name="userAddress">The user's Algorand address</param>
        /// <param name="currentCount">Current number of whitelisted addresses</param>
        /// <returns>Remaining capacity (-1 for unlimited)</returns>
        Task<int> GetRemainingCapacityAsync(string userAddress, int currentCount);

        /// <summary>
        /// Checks if token deployment is allowed for the user's tier
        /// </summary>
        /// <param name="userAddress">The user's Algorand address</param>
        /// <returns>True if token deployment is allowed</returns>
        Task<bool> CanDeployTokenAsync(string userAddress);

        /// <summary>
        /// Records a token deployment for the user
        /// </summary>
        /// <param name="userAddress">The user's Algorand address</param>
        /// <returns>True if deployment was recorded successfully</returns>
        Task<bool> RecordTokenDeploymentAsync(string userAddress);

        /// <summary>
        /// Gets the token deployment count for the user
        /// </summary>
        /// <param name="userAddress">The user's Algorand address</param>
        /// <returns>Number of token deployments</returns>
        Task<int> GetTokenDeploymentCountAsync(string userAddress);
    }

    /// <summary>
    /// Result of subscription tier validation
    /// </summary>
    public class SubscriptionTierValidationResult
    {
        /// <summary>
        /// Whether the operation is allowed
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// The user's subscription tier
        /// </summary>
        public SubscriptionTier Tier { get; set; }

        /// <summary>
        /// Reason if the operation is denied
        /// </summary>
        public string? DenialReason { get; set; }

        /// <summary>
        /// Current count of addresses
        /// </summary>
        public int CurrentCount { get; set; }

        /// <summary>
        /// Maximum allowed addresses for the tier (-1 for unlimited)
        /// </summary>
        public int MaxAllowed { get; set; }

        /// <summary>
        /// Remaining capacity (-1 for unlimited)
        /// </summary>
        public int RemainingCapacity { get; set; }
    }
}
