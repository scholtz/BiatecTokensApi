namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Unified enterprise audit log entry for MICA reporting
    /// </summary>
    /// <remarks>
    /// This model provides a consolidated view of all audit events across whitelist/blacklist
    /// and compliance operations for enterprise-grade MICA compliance reporting.
    /// Supports 7-year retention requirements and comprehensive filtering.
    /// </remarks>
    public class EnterpriseAuditLogEntry
    {
        /// <summary>
        /// Unique identifier for the audit log entry
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The asset ID (token ID) associated with this audit event
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Network on which the event occurred (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, etc.)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// The category of audit event (Whitelist, Blacklist, Compliance)
        /// </summary>
        public AuditEventCategory Category { get; set; }

        /// <summary>
        /// The type of action performed (Add, Update, Remove, Validate, etc.)
        /// </summary>
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// The address of the user who performed the action
        /// </summary>
        public string PerformedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the action was performed (UTC)
        /// </summary>
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the operation completed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// The Algorand address affected by this event (for whitelist/blacklist operations)
        /// </summary>
        public string? AffectedAddress { get; set; }

        /// <summary>
        /// Status before the change (e.g., "Active", "Suspended", "Compliant")
        /// </summary>
        public string? OldStatus { get; set; }

        /// <summary>
        /// Status after the change
        /// </summary>
        public string? NewStatus { get; set; }

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
        /// For transfer validations or token operations: the amount involved
        /// </summary>
        public ulong? Amount { get; set; }

        /// <summary>
        /// Role of the user who performed the action
        /// </summary>
        public string? Role { get; set; }

        /// <summary>
        /// For list operations: number of items returned
        /// </summary>
        public int? ItemCount { get; set; }

        /// <summary>
        /// Source system that generated this audit entry
        /// </summary>
        public string SourceSystem { get; set; } = "BiatecTokensApi";

        /// <summary>
        /// Correlation ID for related events
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// SHA-256 hash of the audit event payload for integrity verification
        /// </summary>
        /// <remarks>
        /// Provides cryptographic proof of event data integrity for MICA compliance.
        /// Hash is computed from key event fields (AssetId, Network, Category, ActionType, PerformedBy, PerformedAt, Success).
        /// </remarks>
        public string PayloadHash { get; set; } = string.Empty;
    }

    /// <summary>
    /// Category of audit event for enterprise reporting
    /// </summary>
    public enum AuditEventCategory
    {
        /// <summary>
        /// Whitelist management event
        /// </summary>
        Whitelist,

        /// <summary>
        /// Blacklist management event
        /// </summary>
        Blacklist,

        /// <summary>
        /// Compliance metadata event
        /// </summary>
        Compliance,

        /// <summary>
        /// Whitelist rule configuration event
        /// </summary>
        WhitelistRules,

        /// <summary>
        /// Transfer validation event
        /// </summary>
        TransferValidation,

        /// <summary>
        /// Token issuance/deployment event
        /// </summary>
        TokenIssuance
    }

    /// <summary>
    /// Request to retrieve enterprise audit logs with comprehensive filtering
    /// </summary>
    public class GetEnterpriseAuditLogRequest
    {
        /// <summary>
        /// Optional filter by asset ID (token ID)
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional filter by network (voimain-v1.0, aramidmain-v1.0, etc.)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by event category
        /// </summary>
        public AuditEventCategory? Category { get; set; }

        /// <summary>
        /// Optional filter by action type
        /// </summary>
        public string? ActionType { get; set; }

        /// <summary>
        /// Optional filter by user who performed the action
        /// </summary>
        public string? PerformedBy { get; set; }

        /// <summary>
        /// Optional filter by affected address (for whitelist/blacklist operations)
        /// </summary>
        public string? AffectedAddress { get; set; }

        /// <summary>
        /// Optional filter by operation result (success/failure)
        /// </summary>
        public bool? Success { get; set; }

        /// <summary>
        /// Optional start date filter (ISO 8601)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter (ISO 8601)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination (default: 50, max: 100)
        /// </summary>
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Response containing enterprise audit log entries
    /// </summary>
    public class EnterpriseAuditLogResponse : BaseResponse
    {
        /// <summary>
        /// List of audit log entries
        /// </summary>
        public List<EnterpriseAuditLogEntry> Entries { get; set; } = new();

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

        /// <summary>
        /// Summary statistics for the returned audit logs
        /// </summary>
        public AuditLogSummary? Summary { get; set; }
    }

    /// <summary>
    /// Summary statistics for audit log exports
    /// </summary>
    public class AuditLogSummary
    {
        /// <summary>
        /// Number of whitelist events
        /// </summary>
        public int WhitelistEvents { get; set; }

        /// <summary>
        /// Number of blacklist events
        /// </summary>
        public int BlacklistEvents { get; set; }

        /// <summary>
        /// Number of compliance events
        /// </summary>
        public int ComplianceEvents { get; set; }

        /// <summary>
        /// Number of token issuance events
        /// </summary>
        public int TokenIssuanceEvents { get; set; }

        /// <summary>
        /// Number of successful operations
        /// </summary>
        public int SuccessfulOperations { get; set; }

        /// <summary>
        /// Number of failed operations
        /// </summary>
        public int FailedOperations { get; set; }

        /// <summary>
        /// Date range covered by the export
        /// </summary>
        public AuditDateRange? DateRange { get; set; }

        /// <summary>
        /// Networks included in the export
        /// </summary>
        public List<string> Networks { get; set; } = new();

        /// <summary>
        /// Assets included in the export
        /// </summary>
        public List<ulong> Assets { get; set; } = new();
    }

    /// <summary>
    /// Date range information for audit log exports
    /// </summary>
    public class AuditDateRange
    {
        /// <summary>
        /// Earliest event timestamp
        /// </summary>
        public DateTime? EarliestEvent { get; set; }

        /// <summary>
        /// Latest event timestamp
        /// </summary>
        public DateTime? LatestEvent { get; set; }
    }
}
