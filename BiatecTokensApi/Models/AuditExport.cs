namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Export format for audit trails
    /// </summary>
    public enum AuditExportFormat
    {
        /// <summary>
        /// JSON format with full details
        /// </summary>
        Json = 0,

        /// <summary>
        /// CSV format for spreadsheet import
        /// </summary>
        Csv = 1
    }

    /// <summary>
    /// Complete audit trail for a deployment
    /// </summary>
    /// <remarks>
    /// Provides comprehensive deployment information for compliance reporting,
    /// including all status transitions, compliance checks, and timing information.
    /// </remarks>
    public class DeploymentAuditTrail
    {
        /// <summary>
        /// Unique identifier for the deployment
        /// </summary>
        public string DeploymentId { get; set; } = string.Empty;

        /// <summary>
        /// Token type being deployed
        /// </summary>
        public string TokenType { get; set; } = string.Empty;

        /// <summary>
        /// Token name
        /// </summary>
        public string? TokenName { get; set; }

        /// <summary>
        /// Token symbol
        /// </summary>
        public string? TokenSymbol { get; set; }

        /// <summary>
        /// Network where the token is deployed
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Address of the user who initiated the deployment
        /// </summary>
        public string DeployedBy { get; set; } = string.Empty;

        /// <summary>
        /// Asset ID or contract address (if deployment succeeded)
        /// </summary>
        public string? AssetIdentifier { get; set; }

        /// <summary>
        /// Transaction hash (if submitted to blockchain)
        /// </summary>
        public string? TransactionHash { get; set; }

        /// <summary>
        /// Current status of the deployment
        /// </summary>
        public DeploymentStatus CurrentStatus { get; set; }

        /// <summary>
        /// Timestamp when deployment was initiated (UTC)
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Timestamp when deployment was last updated (UTC)
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Complete history of status transitions
        /// </summary>
        public List<DeploymentStatusEntry> StatusHistory { get; set; } = new();

        /// <summary>
        /// Summary of compliance checks performed
        /// </summary>
        public string? ComplianceSummary { get; set; }

        /// <summary>
        /// Total duration of deployment in milliseconds
        /// </summary>
        public long TotalDurationMs { get; set; }

        /// <summary>
        /// Error summary if deployment failed
        /// </summary>
        public string? ErrorSummary { get; set; }
    }

    /// <summary>
    /// Request to export audit trails
    /// </summary>
    public class AuditExportRequest
    {
        /// <summary>
        /// Export format (JSON or CSV)
        /// </summary>
        public AuditExportFormat Format { get; set; } = AuditExportFormat.Json;

        /// <summary>
        /// Optional filter by deployed by address
        /// </summary>
        public string? DeployedBy { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by token type
        /// </summary>
        public string? TokenType { get; set; }

        /// <summary>
        /// Optional filter by current status
        /// </summary>
        public DeploymentStatus? Status { get; set; }

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
        /// Page size for pagination (default: 100, max: 1000)
        /// </summary>
        public int PageSize { get; set; } = 100;
    }

    /// <summary>
    /// Result of an audit export operation
    /// </summary>
    public class AuditExportResult
    {
        /// <summary>
        /// Whether the export was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Exported data (JSON or CSV)
        /// </summary>
        public string? Data { get; set; }

        /// <summary>
        /// Format of the exported data
        /// </summary>
        public AuditExportFormat Format { get; set; }

        /// <summary>
        /// Number of records included in the export
        /// </summary>
        public int RecordCount { get; set; }

        /// <summary>
        /// Whether this result was returned from cache
        /// </summary>
        public bool IsCached { get; set; }

        /// <summary>
        /// Timestamp when the export was generated (UTC)
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Error message if the export failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
