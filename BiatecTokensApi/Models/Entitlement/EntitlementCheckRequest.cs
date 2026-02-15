namespace BiatecTokensApi.Models.Entitlement
{
    /// <summary>
    /// Request model for checking user entitlements for a specific operation
    /// </summary>
    public class EntitlementCheckRequest
    {
        /// <summary>
        /// The user's identifier (email or Algorand address)
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// The operation being requested
        /// </summary>
        public EntitlementOperation Operation { get; set; }

        /// <summary>
        /// Additional context for the operation (e.g., quantity, asset ID)
        /// </summary>
        public Dictionary<string, object>? OperationContext { get; set; }

        /// <summary>
        /// Correlation ID for tracking across systems
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Result of an entitlement check
    /// </summary>
    public class EntitlementCheckResult
    {
        /// <summary>
        /// Whether the operation is allowed
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// The user's subscription tier
        /// </summary>
        public string SubscriptionTier { get; set; } = string.Empty;

        /// <summary>
        /// Policy version used for evaluation
        /// </summary>
        public string PolicyVersion { get; set; } = string.Empty;

        /// <summary>
        /// Reason for denial (if IsAllowed is false)
        /// </summary>
        public string? DenialReason { get; set; }

        /// <summary>
        /// Structured error code for denial
        /// </summary>
        public string? DenialCode { get; set; }

        /// <summary>
        /// Upgrade recommendation if denial is due to plan constraints
        /// </summary>
        public UpgradeRecommendation? UpgradeRecommendation { get; set; }

        /// <summary>
        /// Current usage statistics relevant to the operation
        /// </summary>
        public Dictionary<string, object>? CurrentUsage { get; set; }

        /// <summary>
        /// Maximum allowed for the operation in current tier
        /// </summary>
        public Dictionary<string, object>? MaxAllowed { get; set; }

        /// <summary>
        /// Timestamp of the check
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Correlation ID for tracking
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Types of operations that require entitlement checks
    /// </summary>
    public enum EntitlementOperation
    {
        /// <summary>
        /// Token deployment operation
        /// </summary>
        TokenDeployment,

        /// <summary>
        /// Whitelist address addition
        /// </summary>
        WhitelistAddition,

        /// <summary>
        /// Compliance report generation
        /// </summary>
        ComplianceReport,

        /// <summary>
        /// Audit export operation
        /// </summary>
        AuditExport,

        /// <summary>
        /// Advanced compliance feature access
        /// </summary>
        AdvancedCompliance,

        /// <summary>
        /// Multi-jurisdiction support
        /// </summary>
        MultiJurisdiction,

        /// <summary>
        /// Custom branding
        /// </summary>
        CustomBranding,

        /// <summary>
        /// API access
        /// </summary>
        ApiAccess,

        /// <summary>
        /// Webhook configuration
        /// </summary>
        WebhookAccess,

        /// <summary>
        /// Bulk operations
        /// </summary>
        BulkOperation
    }
}
