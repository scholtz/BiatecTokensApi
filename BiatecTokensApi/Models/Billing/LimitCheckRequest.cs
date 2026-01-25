namespace BiatecTokensApi.Models.Billing
{
    /// <summary>
    /// Request model for checking if an operation is allowed (preflight check)
    /// </summary>
    /// <remarks>
    /// This model is used to verify if a planned operation would exceed plan limits
    /// before actually performing the operation.
    /// </remarks>
    public class LimitCheckRequest
    {
        /// <summary>
        /// Type of operation to check
        /// </summary>
        public OperationType OperationType { get; set; }

        /// <summary>
        /// Number of operations to check (default: 1)
        /// </summary>
        public int OperationCount { get; set; } = 1;

        /// <summary>
        /// Optional asset ID for context
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional network for context
        /// </summary>
        public string? Network { get; set; }
    }

    /// <summary>
    /// Types of operations that can be limited
    /// </summary>
    public enum OperationType
    {
        /// <summary>
        /// Token issuance operation
        /// </summary>
        TokenIssuance,

        /// <summary>
        /// Transfer validation operation
        /// </summary>
        TransferValidation,

        /// <summary>
        /// Audit export operation
        /// </summary>
        AuditExport,

        /// <summary>
        /// Storage operation
        /// </summary>
        Storage,

        /// <summary>
        /// Compliance metadata operation
        /// </summary>
        ComplianceOperation,

        /// <summary>
        /// Whitelist operation
        /// </summary>
        WhitelistOperation
    }

    /// <summary>
    /// Response model for limit check endpoint
    /// </summary>
    public class LimitCheckResponse
    {
        /// <summary>
        /// Whether the operation is allowed
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// Current usage for this operation type
        /// </summary>
        public int CurrentUsage { get; set; }

        /// <summary>
        /// Maximum allowed for this operation type (-1 for unlimited)
        /// </summary>
        public int MaxAllowed { get; set; }

        /// <summary>
        /// Remaining capacity (-1 for unlimited)
        /// </summary>
        public int RemainingCapacity { get; set; }

        /// <summary>
        /// Reason if the operation is denied
        /// </summary>
        public string? DenialReason { get; set; }

        /// <summary>
        /// Error code for programmatic handling
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Subscription tier of the tenant
        /// </summary>
        public string? SubscriptionTier { get; set; }
    }
}
