namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Represents an audit log entry for whitelist changes and transfer validations
    /// </summary>
    /// <remarks>
    /// This model tracks all changes to the whitelist for compliance and regulatory reporting.
    /// Each entry represents a single action (add, update, remove, or transfer validation) performed on a whitelist entry.
    /// </remarks>
    public class WhitelistAuditLogEntry
    {
        /// <summary>
        /// Unique identifier for the audit log entry
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The asset ID (token ID) for which the whitelist change occurred
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// The Algorand address affected by this change (sender address for transfer validations)
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// The type of action performed (Add, Update, Remove, TransferValidation)
        /// </summary>
        public WhitelistActionType ActionType { get; set; }

        /// <summary>
        /// The address of the user who performed the action
        /// </summary>
        public string PerformedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the action was performed
        /// </summary>
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The status before the change (null for new entries)
        /// </summary>
        public WhitelistStatus? OldStatus { get; set; }

        /// <summary>
        /// The status after the change (null for removed entries)
        /// </summary>
        public WhitelistStatus? NewStatus { get; set; }

        /// <summary>
        /// Additional notes or context about the change
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// For transfer validations: the receiver's address
        /// </summary>
        public string? ToAddress { get; set; }

        /// <summary>
        /// For transfer validations: whether the transfer was allowed
        /// </summary>
        public bool? TransferAllowed { get; set; }

        /// <summary>
        /// For transfer validations: the reason if transfer was denied
        /// </summary>
        public string? DenialReason { get; set; }

        /// <summary>
        /// For transfer validations: the amount being transferred (optional)
        /// </summary>
        public ulong? Amount { get; set; }

        /// <summary>
        /// Network on which the token is deployed (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, testnet-v1.0, etc.)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Role of the user who performed the action (Admin or Operator)
        /// </summary>
        public WhitelistRole Role { get; set; } = WhitelistRole.Admin;
    }

    /// <summary>
    /// Type of action performed on a whitelist entry
    /// </summary>
    public enum WhitelistActionType
    {
        /// <summary>
        /// Address was added to the whitelist
        /// </summary>
        Add,

        /// <summary>
        /// Whitelist entry was updated (e.g., status change)
        /// </summary>
        Update,

        /// <summary>
        /// Address was removed from the whitelist
        /// </summary>
        Remove,

        /// <summary>
        /// Transfer validation was performed (enforcement check)
        /// </summary>
        TransferValidation
    }

    /// <summary>
    /// Request to retrieve audit log for a token's whitelist
    /// </summary>
    public class GetWhitelistAuditLogRequest
    {
        /// <summary>
        /// Optional asset ID (token ID) for which to retrieve audit log. If not provided, returns audit logs for all assets.
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional filter by address
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Optional filter by action type
        /// </summary>
        public WhitelistActionType? ActionType { get; set; }

        /// <summary>
        /// Optional filter by user who performed the action
        /// </summary>
        public string? PerformedBy { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional start date filter
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination
        /// </summary>
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Response containing whitelist audit log entries
    /// </summary>
    public class WhitelistAuditLogResponse : BaseResponse
    {
        /// <summary>
        /// List of audit log entries
        /// </summary>
        public List<WhitelistAuditLogEntry> Entries { get; set; } = new();

        /// <summary>
        /// Total number of entries matching the filter
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Audit log retention policy metadata
        /// </summary>
        public Compliance.AuditRetentionPolicy? RetentionPolicy { get; set; }
    }
}
