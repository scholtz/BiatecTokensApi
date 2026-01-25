namespace BiatecTokensApi.Models.Billing
{
    /// <summary>
    /// Summary of usage metrics for a tenant
    /// </summary>
    /// <remarks>
    /// This model aggregates metering events to provide billing-relevant usage statistics
    /// for subscription-funded platform operations.
    /// </remarks>
    public class UsageSummary
    {
        /// <summary>
        /// The tenant's Algorand address
        /// </summary>
        public string TenantAddress { get; set; } = string.Empty;

        /// <summary>
        /// Current subscription tier
        /// </summary>
        public string SubscriptionTier { get; set; } = string.Empty;

        /// <summary>
        /// Start date of the usage period (UTC)
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// End date of the usage period (UTC)
        /// </summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>
        /// Total number of tokens issued
        /// </summary>
        public int TokenIssuanceCount { get; set; }

        /// <summary>
        /// Total number of transfer validations
        /// </summary>
        public int TransferValidationCount { get; set; }

        /// <summary>
        /// Total number of audit exports performed
        /// </summary>
        public int AuditExportCount { get; set; }

        /// <summary>
        /// Total storage used (in items/records)
        /// </summary>
        public int StorageItemsCount { get; set; }

        /// <summary>
        /// Number of compliance metadata operations
        /// </summary>
        public int ComplianceOperationCount { get; set; }

        /// <summary>
        /// Number of whitelist operations
        /// </summary>
        public int WhitelistOperationCount { get; set; }

        /// <summary>
        /// Current plan limits for this tenant
        /// </summary>
        public PlanLimits CurrentLimits { get; set; } = new();

        /// <summary>
        /// Whether any limits have been exceeded
        /// </summary>
        public bool HasExceededLimits { get; set; }

        /// <summary>
        /// List of limit violations if any
        /// </summary>
        public List<string> LimitViolations { get; set; } = new();
    }

    /// <summary>
    /// Response model for usage summary endpoint
    /// </summary>
    public class UsageSummaryResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Usage summary data
        /// </summary>
        public UsageSummary? Data { get; set; }

        /// <summary>
        /// Error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
