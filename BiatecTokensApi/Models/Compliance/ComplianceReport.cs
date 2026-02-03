namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Type of compliance report
    /// </summary>
    public enum ReportType
    {
        /// <summary>
        /// MICA readiness assessment report (Articles 17-35)
        /// </summary>
        MicaReadiness,

        /// <summary>
        /// Chronological audit trail snapshot
        /// </summary>
        AuditTrail,

        /// <summary>
        /// Compliance badge evidence collection
        /// </summary>
        ComplianceBadge
    }

    /// <summary>
    /// Status of a compliance report
    /// </summary>
    public enum ReportStatus
    {
        /// <summary>
        /// Report generation is pending
        /// </summary>
        Pending,

        /// <summary>
        /// Report is currently being generated
        /// </summary>
        Processing,

        /// <summary>
        /// Report generation completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Report generation failed
        /// </summary>
        Failed
    }

    /// <summary>
    /// Compliance report metadata and storage information
    /// </summary>
    public class ComplianceReport
    {
        /// <summary>
        /// Unique identifier for the report
        /// </summary>
        public string ReportId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of report
        /// </summary>
        public ReportType ReportType { get; set; }

        /// <summary>
        /// Issuer/creator of the report (Algorand address)
        /// </summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>
        /// Optional asset ID filter applied to this report
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional network filter applied to this report
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Start date of the reporting period (UTC)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// End date of the reporting period (UTC)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Current status of the report
        /// </summary>
        public ReportStatus Status { get; set; } = ReportStatus.Pending;

        /// <summary>
        /// Report schema version for compatibility tracking
        /// </summary>
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>
        /// Report generation timestamp (UTC)
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Report completion timestamp (UTC), null if not completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Error message if report generation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Number of audit events included in the report
        /// </summary>
        public int EventCount { get; set; }

        /// <summary>
        /// SHA-256 checksum of the report content for tamper evidence
        /// </summary>
        public string? Checksum { get; set; }

        /// <summary>
        /// Structured warnings about missing data or compliance gaps
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Report content as JSON (stored for completed reports)
        /// </summary>
        public string? ContentJson { get; set; }
    }

    /// <summary>
    /// Request to create a new compliance report
    /// </summary>
    public class CreateComplianceReportRequest
    {
        /// <summary>
        /// Type of report to generate
        /// </summary>
        public ReportType ReportType { get; set; }

        /// <summary>
        /// Optional filter by asset ID
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional start date for the reporting period
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date for the reporting period
        /// </summary>
        public DateTime? ToDate { get; set; }
    }

    /// <summary>
    /// Response after creating a compliance report
    /// </summary>
    public class CreateComplianceReportResponse : BaseResponse
    {
        /// <summary>
        /// The generated report ID
        /// </summary>
        public string? ReportId { get; set; }

        /// <summary>
        /// Current status of the report
        /// </summary>
        public ReportStatus? Status { get; set; }

        /// <summary>
        /// Report creation timestamp
        /// </summary>
        public DateTime? CreatedAt { get; set; }
    }

    /// <summary>
    /// Request to list compliance reports
    /// </summary>
    public class ListComplianceReportsRequest
    {
        /// <summary>
        /// Optional filter by report type
        /// </summary>
        public ReportType? ReportType { get; set; }

        /// <summary>
        /// Optional filter by asset ID
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by status
        /// </summary>
        public ReportStatus? Status { get; set; }

        /// <summary>
        /// Optional filter by creation date (from)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional filter by creation date (to)
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
    /// Response containing list of compliance reports
    /// </summary>
    public class ListComplianceReportsResponse : BaseResponse
    {
        /// <summary>
        /// List of reports
        /// </summary>
        public List<ComplianceReportSummary> Reports { get; set; } = new();

        /// <summary>
        /// Total number of reports matching the filter
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
    }

    /// <summary>
    /// Summary information for a compliance report (used in listings)
    /// </summary>
    public class ComplianceReportSummary
    {
        /// <summary>
        /// Unique identifier for the report
        /// </summary>
        public string ReportId { get; set; } = string.Empty;

        /// <summary>
        /// Type of report
        /// </summary>
        public ReportType ReportType { get; set; }

        /// <summary>
        /// Current status of the report
        /// </summary>
        public ReportStatus Status { get; set; }

        /// <summary>
        /// Optional asset ID filter
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional network filter
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Number of events in the report
        /// </summary>
        public int EventCount { get; set; }

        /// <summary>
        /// Report creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Report completion timestamp (if completed)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Number of warnings in the report
        /// </summary>
        public int WarningCount { get; set; }
    }

    /// <summary>
    /// Response containing full compliance report details
    /// </summary>
    public class GetComplianceReportResponse : BaseResponse
    {
        /// <summary>
        /// Report metadata
        /// </summary>
        public ComplianceReport? Report { get; set; }
    }

    /// <summary>
    /// MICA readiness report content
    /// </summary>
    public class MicaReadinessReportContent
    {
        /// <summary>
        /// Report metadata
        /// </summary>
        public MicaReadinessReportMetadata Metadata { get; set; } = new();

        /// <summary>
        /// MICA compliance checks results
        /// </summary>
        public List<MicaComplianceCheck> ComplianceChecks { get; set; } = new();

        /// <summary>
        /// Missing evidence or data warnings
        /// </summary>
        public List<string> MissingEvidence { get; set; } = new();

        /// <summary>
        /// Overall MICA readiness score (0-100)
        /// </summary>
        public int ReadinessScore { get; set; }

        /// <summary>
        /// Summary of readiness status
        /// </summary>
        public string ReadinessSummary { get; set; } = string.Empty;
    }

    /// <summary>
    /// MICA readiness report metadata
    /// </summary>
    public class MicaReadinessReportMetadata
    {
        /// <summary>
        /// Report ID
        /// </summary>
        public string ReportId { get; set; } = string.Empty;

        /// <summary>
        /// Schema version
        /// </summary>
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>
        /// Generation timestamp
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Asset ID (if filtered)
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Network (if filtered)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Report period start
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Report period end
        /// </summary>
        public DateTime? ToDate { get; set; }
    }

    /// <summary>
    /// Individual MICA compliance check result
    /// </summary>
    public class MicaComplianceCheck
    {
        /// <summary>
        /// MICA article reference (e.g., "Article 17")
        /// </summary>
        public string Article { get; set; } = string.Empty;

        /// <summary>
        /// Description of the requirement
        /// </summary>
        public string Requirement { get; set; } = string.Empty;

        /// <summary>
        /// Check result (Pass, Fail, Partial)
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Evidence supporting the status
        /// </summary>
        public string? Evidence { get; set; }

        /// <summary>
        /// Recommendations for addressing gaps
        /// </summary>
        public string? Recommendation { get; set; }
    }

    /// <summary>
    /// Audit trail report content
    /// </summary>
    public class AuditTrailReportContent
    {
        /// <summary>
        /// Report metadata
        /// </summary>
        public AuditTrailReportMetadata Metadata { get; set; } = new();

        /// <summary>
        /// Audit events in chronological order
        /// </summary>
        public List<EnterpriseAuditLogEntry> Events { get; set; } = new();

        /// <summary>
        /// Summary statistics
        /// </summary>
        public AuditLogSummary Summary { get; set; } = new();
    }

    /// <summary>
    /// Audit trail report metadata
    /// </summary>
    public class AuditTrailReportMetadata
    {
        /// <summary>
        /// Report ID
        /// </summary>
        public string ReportId { get; set; } = string.Empty;

        /// <summary>
        /// Schema version
        /// </summary>
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>
        /// Generation timestamp
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Asset ID (if filtered)
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Network (if filtered)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Report period start
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Report period end
        /// </summary>
        public DateTime? ToDate { get; set; }
    }

    /// <summary>
    /// Compliance badge evidence report content
    /// </summary>
    public class ComplianceBadgeReportContent
    {
        /// <summary>
        /// Report metadata
        /// </summary>
        public ComplianceBadgeReportMetadata Metadata { get; set; } = new();

        /// <summary>
        /// Evidence items collected
        /// </summary>
        public List<ComplianceEvidenceItem> Evidence { get; set; } = new();

        /// <summary>
        /// Badge eligibility status
        /// </summary>
        public string BadgeStatus { get; set; } = string.Empty;

        /// <summary>
        /// Missing requirements for badge
        /// </summary>
        public List<string> MissingRequirements { get; set; } = new();
    }

    /// <summary>
    /// Compliance badge report metadata
    /// </summary>
    public class ComplianceBadgeReportMetadata
    {
        /// <summary>
        /// Report ID
        /// </summary>
        public string ReportId { get; set; } = string.Empty;

        /// <summary>
        /// Schema version
        /// </summary>
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>
        /// Generation timestamp
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Asset ID (if filtered)
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Network (if filtered)
        /// </summary>
        public string? Network { get; set; }
    }

    /// <summary>
    /// Individual compliance evidence item
    /// </summary>
    public class ComplianceEvidenceItem
    {
        /// <summary>
        /// Type of evidence
        /// </summary>
        public string EvidenceType { get; set; } = string.Empty;

        /// <summary>
        /// Evidence description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Source of the evidence
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of the evidence
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Verification status
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }
}
