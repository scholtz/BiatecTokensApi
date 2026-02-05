namespace BiatecTokensApi.Models.Subscription
{
    /// <summary>
    /// Represents the entitlements (feature access) for a subscription tier
    /// </summary>
    public class SubscriptionEntitlements
    {
        /// <summary>
        /// Subscription tier
        /// </summary>
        public SubscriptionTier Tier { get; set; }

        /// <summary>
        /// Maximum number of tokens that can be deployed per month
        /// </summary>
        public int MaxTokenDeployments { get; set; }

        /// <summary>
        /// Maximum number of whitelisted addresses per token
        /// </summary>
        public int MaxWhitelistedAddresses { get; set; }

        /// <summary>
        /// Maximum number of compliance reports per month
        /// </summary>
        public int MaxComplianceReports { get; set; }

        /// <summary>
        /// Whether advanced compliance features are enabled
        /// </summary>
        public bool AdvancedComplianceEnabled { get; set; }

        /// <summary>
        /// Whether multi-jurisdiction support is enabled
        /// </summary>
        public bool MultiJurisdictionEnabled { get; set; }

        /// <summary>
        /// Whether custom branding is enabled
        /// </summary>
        public bool CustomBrandingEnabled { get; set; }

        /// <summary>
        /// Whether priority support is included
        /// </summary>
        public bool PrioritySupportEnabled { get; set; }

        /// <summary>
        /// Whether API access is enabled
        /// </summary>
        public bool ApiAccessEnabled { get; set; }

        /// <summary>
        /// Whether webhook support is enabled
        /// </summary>
        public bool WebhooksEnabled { get; set; }

        /// <summary>
        /// Whether audit exports are enabled
        /// </summary>
        public bool AuditExportsEnabled { get; set; }

        /// <summary>
        /// Maximum number of audit exports per month
        /// </summary>
        public int MaxAuditExports { get; set; }

        /// <summary>
        /// Whether SLA guarantees apply
        /// </summary>
        public bool SlaEnabled { get; set; }

        /// <summary>
        /// SLA uptime guarantee percentage (e.g., 99.9)
        /// </summary>
        public double? SlaUptimePercentage { get; set; }
    }

    /// <summary>
    /// Response containing subscription entitlements
    /// </summary>
    public class SubscriptionEntitlementsResponse
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Subscription entitlements
        /// </summary>
        public SubscriptionEntitlements? Entitlements { get; set; }

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
