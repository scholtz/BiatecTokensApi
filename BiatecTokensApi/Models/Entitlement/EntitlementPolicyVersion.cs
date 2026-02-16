namespace BiatecTokensApi.Models.Entitlement
{
    /// <summary>
    /// Represents a versioned entitlement policy configuration
    /// </summary>
    public class EntitlementPolicyVersion
    {
        /// <summary>
        /// Policy version identifier (e.g., "2025.02.15.1")
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// When this policy version became effective
        /// </summary>
        public DateTime EffectiveDate { get; set; }

        /// <summary>
        /// Human-readable description of changes in this version
        /// </summary>
        public string? ChangeDescription { get; set; }

        /// <summary>
        /// Whether this is the currently active policy
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Tier configurations for this policy version
        /// </summary>
        public Dictionary<string, TierPolicyConfiguration> TierConfigurations { get; set; } = new();
    }

    /// <summary>
    /// Configuration for a specific tier in a policy version
    /// </summary>
    public class TierPolicyConfiguration
    {
        /// <summary>
        /// Tier name
        /// </summary>
        public string TierName { get; set; } = string.Empty;

        /// <summary>
        /// Monthly token deployment limit (-1 for unlimited)
        /// </summary>
        public int MonthlyTokenDeployments { get; set; }

        /// <summary>
        /// Concurrent draft projects limit (-1 for unlimited)
        /// </summary>
        public int ConcurrentDrafts { get; set; }

        /// <summary>
        /// Monthly compliance reports limit (-1 for unlimited)
        /// </summary>
        public int MonthlyComplianceReports { get; set; }

        /// <summary>
        /// Monthly audit exports limit (-1 for unlimited)
        /// </summary>
        public int MonthlyAuditExports { get; set; }

        /// <summary>
        /// Whitelisted addresses per asset limit (-1 for unlimited)
        /// </summary>
        public int WhitelistedAddressesPerAsset { get; set; }

        /// <summary>
        /// Feature toggles for advanced capabilities
        /// </summary>
        public TierFeatureToggles Features { get; set; } = new();
    }

    /// <summary>
    /// Feature toggle configuration for a tier
    /// </summary>
    public class TierFeatureToggles
    {
        /// <summary>
        /// Advanced compliance features enabled
        /// </summary>
        public bool AdvancedComplianceEnabled { get; set; }

        /// <summary>
        /// Multi-jurisdiction support enabled
        /// </summary>
        public bool MultiJurisdictionEnabled { get; set; }

        /// <summary>
        /// Custom branding enabled
        /// </summary>
        public bool CustomBrandingEnabled { get; set; }

        /// <summary>
        /// API access enabled
        /// </summary>
        public bool ApiAccessEnabled { get; set; }

        /// <summary>
        /// Webhook support enabled
        /// </summary>
        public bool WebhooksEnabled { get; set; }

        /// <summary>
        /// Priority support enabled
        /// </summary>
        public bool PrioritySupportEnabled { get; set; }

        /// <summary>
        /// SLA guarantees apply
        /// </summary>
        public bool SlaEnabled { get; set; }

        /// <summary>
        /// Bulk operations enabled
        /// </summary>
        public bool BulkOperationsEnabled { get; set; }

        /// <summary>
        /// Audit log access enabled
        /// </summary>
        public bool AuditLogEnabled { get; set; }
    }
}
