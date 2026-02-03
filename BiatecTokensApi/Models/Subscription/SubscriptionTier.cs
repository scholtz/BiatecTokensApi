namespace BiatecTokensApi.Models.Subscription
{
    /// <summary>
    /// Represents subscription tier levels for RWA token compliance operations
    /// </summary>
    /// <remarks>
    /// Different subscription tiers provide different limits on whitelist operations,
    /// enabling tiered pricing and usage-based billing for compliance features.
    /// </remarks>
    public enum SubscriptionTier
    {
        /// <summary>
        /// Free tier - limited whitelist capacity for testing and small deployments
        /// </summary>
        /// <remarks>
        /// Limits: Up to 10 whitelisted addresses per asset
        /// </remarks>
        Free = 0,

        /// <summary>
        /// Basic tier - suitable for small to medium RWA token deployments
        /// </summary>
        /// <remarks>
        /// Limits: Up to 100 whitelisted addresses per asset
        /// </remarks>
        Basic = 1,

        /// <summary>
        /// Premium tier - suitable for larger institutional deployments
        /// </summary>
        /// <remarks>
        /// Limits: Up to 1,000 whitelisted addresses per asset
        /// </remarks>
        Premium = 2,

        /// <summary>
        /// Enterprise tier - unlimited capacity for large-scale deployments
        /// </summary>
        /// <remarks>
        /// Limits: Unlimited whitelisted addresses per asset
        /// </remarks>
        Enterprise = 3
    }

    /// <summary>
    /// Defines subscription tier limits and capabilities
    /// </summary>
    public class SubscriptionTierLimits
    {
        /// <summary>
        /// Subscription tier level
        /// </summary>
        public SubscriptionTier Tier { get; set; }

        /// <summary>
        /// Maximum number of whitelisted addresses per asset (-1 for unlimited)
        /// </summary>
        public int MaxAddressesPerAsset { get; set; }

        /// <summary>
        /// Maximum number of token deployments allowed (-1 for unlimited)
        /// </summary>
        public int MaxTokenDeployments { get; set; } = -1;

        /// <summary>
        /// Human-readable tier name
        /// </summary>
        public string TierName { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of tier limits
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether transfer validation is available in this tier
        /// </summary>
        public bool TransferValidationEnabled { get; set; } = true;

        /// <summary>
        /// Whether audit log access is available in this tier
        /// </summary>
        public bool AuditLogEnabled { get; set; } = true;

        /// <summary>
        /// Whether bulk operations are available in this tier
        /// </summary>
        public bool BulkOperationsEnabled { get; set; } = true;

        /// <summary>
        /// Whether token deployment is enabled in this tier
        /// </summary>
        public bool TokenDeploymentEnabled { get; set; } = true;
    }

    /// <summary>
    /// Configuration for subscription tier settings
    /// </summary>
    public class SubscriptionTierConfiguration
    {
        /// <summary>
        /// Gets predefined tier limits for all subscription tiers
        /// </summary>
        public static readonly Dictionary<SubscriptionTier, SubscriptionTierLimits> TierLimits = new()
        {
            {
                SubscriptionTier.Free,
                new SubscriptionTierLimits
                {
                    Tier = SubscriptionTier.Free,
                    MaxAddressesPerAsset = 10,
                    MaxTokenDeployments = 3, // Free tier: limited token deployments for testing
                    TierName = "Free",
                    Description = "Free tier with up to 10 whitelisted addresses per asset and 3 token deployments",
                    TransferValidationEnabled = true,
                    AuditLogEnabled = false,
                    BulkOperationsEnabled = false,
                    TokenDeploymentEnabled = true
                }
            },
            {
                SubscriptionTier.Basic,
                new SubscriptionTierLimits
                {
                    Tier = SubscriptionTier.Basic,
                    MaxAddressesPerAsset = 100,
                    MaxTokenDeployments = 10, // Basic tier: moderate token deployments
                    TierName = "Basic",
                    Description = "Basic tier with up to 100 whitelisted addresses per asset and 10 token deployments",
                    TransferValidationEnabled = true,
                    AuditLogEnabled = true,
                    BulkOperationsEnabled = false,
                    TokenDeploymentEnabled = true
                }
            },
            {
                SubscriptionTier.Premium,
                new SubscriptionTierLimits
                {
                    Tier = SubscriptionTier.Premium,
                    MaxAddressesPerAsset = 1000,
                    MaxTokenDeployments = 50, // Premium tier: generous token deployments
                    TierName = "Premium",
                    Description = "Premium tier with up to 1,000 whitelisted addresses per asset and 50 token deployments",
                    TransferValidationEnabled = true,
                    AuditLogEnabled = true,
                    BulkOperationsEnabled = true,
                    TokenDeploymentEnabled = true
                }
            },
            {
                SubscriptionTier.Enterprise,
                new SubscriptionTierLimits
                {
                    Tier = SubscriptionTier.Enterprise,
                    MaxAddressesPerAsset = -1, // Unlimited
                    MaxTokenDeployments = -1, // Unlimited
                    TierName = "Enterprise",
                    Description = "Enterprise tier with unlimited whitelisted addresses and token deployments",
                    TransferValidationEnabled = true,
                    AuditLogEnabled = true,
                    BulkOperationsEnabled = true,
                    TokenDeploymentEnabled = true
                }
            }
        };

        /// <summary>
        /// Gets tier limits for a specific subscription tier
        /// </summary>
        /// <param name="tier">The subscription tier</param>
        /// <returns>Tier limits configuration</returns>
        public static SubscriptionTierLimits GetTierLimits(SubscriptionTier tier)
        {
            return TierLimits.TryGetValue(tier, out var limits) 
                ? limits 
                : TierLimits[SubscriptionTier.Free]; // Default to Free tier
        }

        /// <summary>
        /// Checks if an operation is allowed for the given tier and current count
        /// </summary>
        /// <param name="tier">The subscription tier</param>
        /// <param name="currentCount">Current number of addresses</param>
        /// <param name="additionalCount">Number of addresses to add</param>
        /// <returns>True if the operation is allowed, false otherwise</returns>
        public static bool IsOperationAllowed(SubscriptionTier tier, int currentCount, int additionalCount = 1)
        {
            var limits = GetTierLimits(tier);
            
            // Unlimited tier (-1) always allows operations
            if (limits.MaxAddressesPerAsset == -1)
            {
                return true;
            }
            
            // Check if adding the additional addresses would exceed the limit
            return (currentCount + additionalCount) <= limits.MaxAddressesPerAsset;
        }

        /// <summary>
        /// Gets the remaining capacity for a tier
        /// </summary>
        /// <param name="tier">The subscription tier</param>
        /// <param name="currentCount">Current number of addresses</param>
        /// <returns>Remaining capacity (-1 for unlimited)</returns>
        public static int GetRemainingCapacity(SubscriptionTier tier, int currentCount)
        {
            var limits = GetTierLimits(tier);
            
            if (limits.MaxAddressesPerAsset == -1)
            {
                return -1; // Unlimited
            }
            
            return Math.Max(0, limits.MaxAddressesPerAsset - currentCount);
        }
    }
}
