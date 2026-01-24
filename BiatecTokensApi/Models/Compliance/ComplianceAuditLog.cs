namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents an audit log entry for compliance metadata changes and access
    /// </summary>
    /// <remarks>
    /// This model tracks all operations on compliance metadata for MICA/RWA regulatory reporting.
    /// Each entry represents a single action (create, update, delete, read, list) performed on compliance metadata.
    /// Entries are immutable and retained for at least 7 years for regulatory compliance.
    /// </remarks>
    public class ComplianceAuditLogEntry
    {
        /// <summary>
        /// Unique identifier for the audit log entry
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceAuditLogEntry"/> class.
        /// </summary>
        public ComplianceAuditLogEntry()
        {
            Id = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// The asset ID (token ID) for which the compliance operation occurred
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// The network on which the token is deployed
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// The type of action performed (Create, Update, Delete, Read, List)
        /// </summary>
        public ComplianceActionType ActionType { get; set; }

        /// <summary>
        /// The address of the user who performed the action
        /// </summary>
        public string PerformedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the action was performed
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
        /// The compliance status before the change (for updates)
        /// </summary>
        public ComplianceStatus? OldComplianceStatus { get; set; }

        /// <summary>
        /// The compliance status after the change (for creates/updates)
        /// </summary>
        public ComplianceStatus? NewComplianceStatus { get; set; }

        /// <summary>
        /// The verification status before the change (for updates)
        /// </summary>
        public VerificationStatus? OldVerificationStatus { get; set; }

        /// <summary>
        /// The verification status after the change (for creates/updates)
        /// </summary>
        public VerificationStatus? NewVerificationStatus { get; set; }

        /// <summary>
        /// Additional notes or context about the change
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// For list operations: number of items returned
        /// </summary>
        public int? ItemCount { get; set; }

        /// <summary>
        /// For list operations: filter criteria applied
        /// </summary>
        public string? FilterCriteria { get; set; }
    }

    /// <summary>
    /// Type of action performed on compliance metadata
    /// </summary>
    public enum ComplianceActionType
    {
        /// <summary>
        /// Compliance metadata was created
        /// </summary>
        Create,

        /// <summary>
        /// Compliance metadata was updated
        /// </summary>
        Update,

        /// <summary>
        /// Compliance metadata was deleted
        /// </summary>
        Delete,

        /// <summary>
        /// Compliance metadata was read/retrieved
        /// </summary>
        Read,

        /// <summary>
        /// Compliance metadata was listed with filters
        /// </summary>
        List,

        /// <summary>
        /// Compliance evidence bundle was exported
        /// </summary>
        Export
    }

    /// <summary>
    /// Request to retrieve audit log for compliance operations
    /// </summary>
    public class GetComplianceAuditLogRequest
    {
        /// <summary>
        /// Optional filter by asset ID (token ID)
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by action type
        /// </summary>
        public ComplianceActionType? ActionType { get; set; }

        /// <summary>
        /// Optional filter by user who performed the action
        /// </summary>
        public string? PerformedBy { get; set; }

        /// <summary>
        /// Optional filter by operation result (success/failure)
        /// </summary>
        public bool? Success { get; set; }

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
    /// Response containing compliance audit log entries
    /// </summary>
    public class ComplianceAuditLogResponse : BaseResponse
    {
        /// <summary>
        /// List of audit log entries
        /// </summary>
        public List<ComplianceAuditLogEntry> Entries { get; set; } = new();

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
        public AuditRetentionPolicy? RetentionPolicy { get; set; }
    }

    /// <summary>
    /// Audit log retention policy information for regulatory compliance
    /// </summary>
    public class AuditRetentionPolicy
    {
        /// <summary>
        /// Minimum retention period in years
        /// </summary>
        public int MinimumRetentionYears { get; set; } = 7;

        /// <summary>
        /// Regulatory framework requiring retention (e.g., "MICA", "SEC")
        /// </summary>
        public string RegulatoryFramework { get; set; } = "MICA";

        /// <summary>
        /// Whether audit logs are immutable
        /// </summary>
        public bool ImmutableEntries { get; set; } = true;

        /// <summary>
        /// Description of retention policy
        /// </summary>
        public string Description { get; set; } = "Audit logs are retained for a minimum of 7 years to comply with MICA and other regulatory requirements. All entries are immutable and cannot be modified or deleted.";
    }
}
