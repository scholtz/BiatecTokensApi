using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for validating subscription tier limits for whitelist operations
    /// </summary>
    /// <remarks>
    /// This service manages subscription tier validation and enforcement for RWA compliance features.
    /// In the current implementation, tier assignments are stored in-memory and can be migrated to
    /// a persistent storage backend without API changes.
    /// </remarks>
    public class SubscriptionTierService : ISubscriptionTierService
    {
        private readonly ILogger<SubscriptionTierService> _logger;
        
        // In-memory storage for user tier assignments
        // Key: user address, Value: subscription tier
        private readonly ConcurrentDictionary<string, SubscriptionTier> _userTiers;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionTierService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public SubscriptionTierService(ILogger<SubscriptionTierService> logger)
        {
            _logger = logger;
            _userTiers = new ConcurrentDictionary<string, SubscriptionTier>(StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public Task<SubscriptionTier> GetUserTierAsync(string userAddress)
        {
            if (string.IsNullOrWhiteSpace(userAddress))
            {
                _logger.LogWarning("GetUserTierAsync called with null or empty userAddress, defaulting to Free tier");
                return Task.FromResult(SubscriptionTier.Free);
            }

            // Get tier from storage, default to Free if not found
            var tier = _userTiers.GetOrAdd(userAddress.ToUpperInvariant(), _ => SubscriptionTier.Free);
            
            _logger.LogDebug("Retrieved tier {Tier} for user {UserAddress}", tier, userAddress);
            
            return Task.FromResult(tier);
        }

        /// <inheritdoc/>
        public async Task<SubscriptionTierValidationResult> ValidateOperationAsync(
            string userAddress, 
            ulong assetId, 
            int currentCount, 
            int additionalCount = 1)
        {
            var tier = await GetUserTierAsync(userAddress);
            var limits = SubscriptionTierConfiguration.GetTierLimits(tier);
            var isAllowed = SubscriptionTierConfiguration.IsOperationAllowed(tier, currentCount, additionalCount);
            var remainingCapacity = SubscriptionTierConfiguration.GetRemainingCapacity(tier, currentCount);

            var result = new SubscriptionTierValidationResult
            {
                IsAllowed = isAllowed,
                Tier = tier,
                CurrentCount = currentCount,
                MaxAllowed = limits.MaxAddressesPerAsset,
                RemainingCapacity = remainingCapacity
            };

            if (!isAllowed)
            {
                result.DenialReason = limits.MaxAddressesPerAsset == -1
                    ? "Operation not allowed" // Should never happen for unlimited tier
                    : $"Subscription tier '{limits.TierName}' limit exceeded. " +
                      $"Current: {currentCount}, Attempting to add: {additionalCount}, " +
                      $"Max allowed: {limits.MaxAddressesPerAsset}. " +
                      $"Please upgrade your subscription to add more addresses.";

                _logger.LogWarning(
                    "Operation denied for user {UserAddress} on asset {AssetId}: {Reason}",
                    userAddress, assetId, result.DenialReason);
            }
            else
            {
                _logger.LogDebug(
                    "Operation allowed for user {UserAddress} on asset {AssetId}. " +
                    "Tier: {Tier}, Current: {Current}, Adding: {Adding}, Max: {Max}",
                    userAddress, assetId, tier, currentCount, additionalCount, limits.MaxAddressesPerAsset);
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task<bool> IsBulkOperationEnabledAsync(string userAddress)
        {
            var tier = await GetUserTierAsync(userAddress);
            var limits = SubscriptionTierConfiguration.GetTierLimits(tier);
            
            _logger.LogDebug(
                "Bulk operation enabled check for user {UserAddress}: {Enabled} (Tier: {Tier})",
                userAddress, limits.BulkOperationsEnabled, tier);
            
            return limits.BulkOperationsEnabled;
        }

        /// <inheritdoc/>
        public async Task<bool> IsAuditLogEnabledAsync(string userAddress)
        {
            var tier = await GetUserTierAsync(userAddress);
            var limits = SubscriptionTierConfiguration.GetTierLimits(tier);
            
            _logger.LogDebug(
                "Audit log enabled check for user {UserAddress}: {Enabled} (Tier: {Tier})",
                userAddress, limits.AuditLogEnabled, tier);
            
            return limits.AuditLogEnabled;
        }

        /// <inheritdoc/>
        public SubscriptionTierLimits GetTierLimits(SubscriptionTier tier)
        {
            return SubscriptionTierConfiguration.GetTierLimits(tier);
        }

        /// <inheritdoc/>
        public async Task<int> GetRemainingCapacityAsync(string userAddress, int currentCount)
        {
            var tier = await GetUserTierAsync(userAddress);
            var remaining = SubscriptionTierConfiguration.GetRemainingCapacity(tier, currentCount);
            
            _logger.LogDebug(
                "Remaining capacity for user {UserAddress}: {Remaining} (Tier: {Tier}, Current: {Current})",
                userAddress, remaining == -1 ? "unlimited" : remaining.ToString(), tier, currentCount);
            
            return remaining;
        }

        /// <summary>
        /// Sets the subscription tier for a user (for testing and administration)
        /// </summary>
        /// <param name="userAddress">The user's Algorand address</param>
        /// <param name="tier">The subscription tier to assign</param>
        /// <remarks>
        /// This method is used for testing and can be used by administrators to manage user tiers.
        /// In a production environment, this would typically be managed through a separate admin API
        /// or integrated with a billing/subscription management system.
        /// </remarks>
        public void SetUserTier(string userAddress, SubscriptionTier tier)
        {
            if (string.IsNullOrWhiteSpace(userAddress))
            {
                _logger.LogWarning("SetUserTier called with null or empty userAddress");
                return;
            }

            _userTiers[userAddress.ToUpperInvariant()] = tier;
            
            _logger.LogInformation(
                "Set subscription tier for user {UserAddress} to {Tier}",
                userAddress, tier);
        }
    }
}
