using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Request to generate a compliance evidence bundle (ZIP) for auditors
    /// </summary>
    public class GenerateComplianceEvidenceBundleRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which to generate the evidence bundle
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// Optional start date filter for audit logs (ISO 8601 format)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter for audit logs (ISO 8601 format)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Whether to include whitelist history in the bundle (default: true)
        /// </summary>
        public bool IncludeWhitelistHistory { get; set; } = true;

        /// <summary>
        /// Whether to include transfer approvals in the bundle (default: true)
        /// </summary>
        public bool IncludeTransferApprovals { get; set; } = true;

        /// <summary>
        /// Whether to include audit logs in the bundle (default: true)
        /// </summary>
        public bool IncludeAuditLogs { get; set; } = true;

        /// <summary>
        /// Whether to include policy metadata in the bundle (default: true)
        /// </summary>
        public bool IncludePolicyMetadata { get; set; } = true;

        /// <summary>
        /// Whether to include token metadata in the bundle (default: true)
        /// </summary>
        public bool IncludeTokenMetadata { get; set; } = true;
    }

    /// <summary>
    /// Response for compliance evidence bundle generation
    /// </summary>
    public class ComplianceEvidenceBundleResponse : BaseResponse
    {
        /// <summary>
        /// The generated bundle metadata
        /// </summary>
        public ComplianceEvidenceBundleMetadata? BundleMetadata { get; set; }

        /// <summary>
        /// The base64-encoded ZIP file content (only populated when downloading)
        /// </summary>
        public byte[]? ZipContent { get; set; }

        /// <summary>
        /// The filename for the ZIP bundle
        /// </summary>
        public string? FileName { get; set; }
    }

    /// <summary>
    /// Metadata for a compliance evidence bundle
    /// </summary>
    public class ComplianceEvidenceBundleMetadata
    {
        /// <summary>
        /// Unique identifier for the bundle
        /// </summary>
        public string BundleId { get; set; } = string.Empty;

        /// <summary>
        /// Asset ID (token ID) for which the bundle was generated
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Timestamp when the bundle was generated (UTC)
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Algorand address of the user who generated the bundle
        /// </summary>
        public string GeneratedBy { get; set; } = string.Empty;

        /// <summary>
        /// Start date for audit log filtering (if specified)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// End date for audit log filtering (if specified)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Network on which the token is deployed
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// List of files included in the bundle
        /// </summary>
        public List<BundleFile> Files { get; set; } = new();

        /// <summary>
        /// SHA256 checksum of the entire bundle
        /// </summary>
        public string BundleSha256 { get; set; } = string.Empty;

        /// <summary>
        /// MICA compliance framework version
        /// </summary>
        public string ComplianceFramework { get; set; } = "MICA 2024";

        /// <summary>
        /// Retention period in years as per MICA requirements
        /// </summary>
        public int RetentionPeriodYears { get; set; } = 7;

        /// <summary>
        /// Summary of included data
        /// </summary>
        public BundleSummary Summary { get; set; } = new();
    }

    /// <summary>
    /// Represents a file included in the compliance evidence bundle
    /// </summary>
    public class BundleFile
    {
        /// <summary>
        /// File path within the ZIP archive
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Description of the file content
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 checksum of the file content
        /// </summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// File format (JSON, CSV, TXT, etc.)
        /// </summary>
        public string Format { get; set; } = string.Empty;
    }

    /// <summary>
    /// Summary statistics for the compliance evidence bundle
    /// </summary>
    public class BundleSummary
    {
        /// <summary>
        /// Total number of audit log entries included
        /// </summary>
        public int AuditLogCount { get; set; }

        /// <summary>
        /// Total number of whitelist entries included
        /// </summary>
        public int WhitelistEntriesCount { get; set; }

        /// <summary>
        /// Total number of whitelist rule audit logs included
        /// </summary>
        public int WhitelistRuleAuditCount { get; set; }

        /// <summary>
        /// Total number of transfer validation records included
        /// </summary>
        public int TransferValidationCount { get; set; }

        /// <summary>
        /// Date range of the oldest record
        /// </summary>
        public DateTime? OldestRecordDate { get; set; }

        /// <summary>
        /// Date range of the newest record
        /// </summary>
        public DateTime? NewestRecordDate { get; set; }

        /// <summary>
        /// Whether compliance metadata is included
        /// </summary>
        public bool HasComplianceMetadata { get; set; }

        /// <summary>
        /// Whether token metadata is included
        /// </summary>
        public bool HasTokenMetadata { get; set; }

        /// <summary>
        /// List of event categories included in audit logs
        /// </summary>
        public List<string> IncludedCategories { get; set; } = new();
    }
}
