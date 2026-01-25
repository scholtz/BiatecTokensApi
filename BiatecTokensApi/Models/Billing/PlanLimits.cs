namespace BiatecTokensApi.Models.Billing
{
    /// <summary>
    /// Defines plan limits for a subscription tier
    /// </summary>
    /// <remarks>
    /// These limits control usage across token issuance, transfers, exports, and storage
    /// to enable subscription-based billing and enterprise governance.
    /// </remarks>
    public class PlanLimits
    {
        /// <summary>
        /// Maximum number of tokens that can be issued per period (-1 for unlimited)
        /// </summary>
        public int MaxTokenIssuance { get; set; } = -1;

        /// <summary>
        /// Maximum number of transfer validations per period (-1 for unlimited)
        /// </summary>
        public int MaxTransferValidations { get; set; } = -1;

        /// <summary>
        /// Maximum number of audit exports per period (-1 for unlimited)
        /// </summary>
        public int MaxAuditExports { get; set; } = -1;

        /// <summary>
        /// Maximum storage items allowed (-1 for unlimited)
        /// </summary>
        public int MaxStorageItems { get; set; } = -1;

        /// <summary>
        /// Maximum compliance operations per period (-1 for unlimited)
        /// </summary>
        public int MaxComplianceOperations { get; set; } = -1;

        /// <summary>
        /// Maximum whitelist operations per period (-1 for unlimited)
        /// </summary>
        public int MaxWhitelistOperations { get; set; } = -1;
    }

    /// <summary>
    /// Request model for updating plan limits (admin only)
    /// </summary>
    public class UpdatePlanLimitsRequest
    {
        /// <summary>
        /// The tenant's Algorand address
        /// </summary>
        public string TenantAddress { get; set; } = string.Empty;

        /// <summary>
        /// New plan limits to set
        /// </summary>
        public PlanLimits Limits { get; set; } = new();

        /// <summary>
        /// Optional notes about why limits were changed
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Response model for plan limits operations
    /// </summary>
    public class PlanLimitsResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The updated plan limits
        /// </summary>
        public PlanLimits? Limits { get; set; }

        /// <summary>
        /// Error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
