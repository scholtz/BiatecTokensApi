namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Request to get compliance monitoring metrics
    /// </summary>
    public class GetComplianceMonitoringMetricsRequest
    {
        /// <summary>
        /// Optional filter by network (voimain-v1.0, aramidmain-v1.0, etc.)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by asset ID
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional start date for metrics calculation
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date for metrics calculation
        /// </summary>
        public DateTime? ToDate { get; set; }
    }

    /// <summary>
    /// Response containing compliance monitoring metrics
    /// </summary>
    public class ComplianceMonitoringMetricsResponse : BaseResponse
    {
        /// <summary>
        /// Whitelist enforcement metrics
        /// </summary>
        public WhitelistEnforcementMetrics WhitelistEnforcement { get; set; } = new();

        /// <summary>
        /// Audit log health status
        /// </summary>
        public AuditLogHealth AuditHealth { get; set; } = new();

        /// <summary>
        /// Retention status per network
        /// </summary>
        public List<NetworkRetentionStatus> NetworkRetentionStatus { get; set; } = new();

        /// <summary>
        /// Overall compliance health score (0-100)
        /// </summary>
        public int OverallHealthScore { get; set; }

        /// <summary>
        /// Timestamp when metrics were calculated
        /// </summary>
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Whitelist enforcement metrics for transfer validations
    /// </summary>
    public class WhitelistEnforcementMetrics
    {
        /// <summary>
        /// Total number of transfer validations performed
        /// </summary>
        public int TotalValidations { get; set; }

        /// <summary>
        /// Number of transfers that were allowed
        /// </summary>
        public int AllowedTransfers { get; set; }

        /// <summary>
        /// Number of transfers that were denied
        /// </summary>
        public int DeniedTransfers { get; set; }

        /// <summary>
        /// Percentage of transfers allowed (0-100)
        /// </summary>
        public decimal AllowedPercentage { get; set; }

        /// <summary>
        /// Top denial reasons with occurrence counts
        /// </summary>
        public List<DenialReasonCount> TopDenialReasons { get; set; } = new();

        /// <summary>
        /// Number of unique assets with enforcement enabled
        /// </summary>
        public int AssetsWithEnforcement { get; set; }

        /// <summary>
        /// Breakdown by network
        /// </summary>
        public List<NetworkEnforcementMetrics> NetworkBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Enforcement metrics per network
    /// </summary>
    public class NetworkEnforcementMetrics
    {
        /// <summary>
        /// Network name (voimain-v1.0, aramidmain-v1.0, etc.)
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Total validations on this network
        /// </summary>
        public int TotalValidations { get; set; }

        /// <summary>
        /// Allowed transfers on this network
        /// </summary>
        public int AllowedTransfers { get; set; }

        /// <summary>
        /// Denied transfers on this network
        /// </summary>
        public int DeniedTransfers { get; set; }

        /// <summary>
        /// Number of assets on this network
        /// </summary>
        public int AssetCount { get; set; }
    }

    /// <summary>
    /// Denial reason with occurrence count
    /// </summary>
    public class DenialReasonCount
    {
        /// <summary>
        /// Reason for denial
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Number of times this reason occurred
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Audit log health status
    /// </summary>
    public class AuditLogHealth
    {
        /// <summary>
        /// Total number of audit log entries
        /// </summary>
        public int TotalEntries { get; set; }

        /// <summary>
        /// Number of compliance audit entries
        /// </summary>
        public int ComplianceEntries { get; set; }

        /// <summary>
        /// Number of whitelist audit entries
        /// </summary>
        public int WhitelistEntries { get; set; }

        /// <summary>
        /// Oldest audit entry timestamp
        /// </summary>
        public DateTime? OldestEntry { get; set; }

        /// <summary>
        /// Most recent audit entry timestamp
        /// </summary>
        public DateTime? NewestEntry { get; set; }

        /// <summary>
        /// Whether audit logs meet MICA retention requirements
        /// </summary>
        public bool MeetsRetentionRequirements { get; set; }

        /// <summary>
        /// Health status (Healthy, Warning, Critical)
        /// </summary>
        public AuditHealthStatus Status { get; set; }

        /// <summary>
        /// Health issues if any
        /// </summary>
        public List<string> HealthIssues { get; set; } = new();

        /// <summary>
        /// Audit coverage percentage (0-100)
        /// </summary>
        public decimal CoveragePercentage { get; set; }
    }

    /// <summary>
    /// Audit log health status enum
    /// </summary>
    public enum AuditHealthStatus
    {
        /// <summary>
        /// All audit logs are healthy and meet requirements
        /// </summary>
        Healthy,

        /// <summary>
        /// Some audit logs have minor issues
        /// </summary>
        Warning,

        /// <summary>
        /// Critical audit log issues detected
        /// </summary>
        Critical
    }

    /// <summary>
    /// Network retention status for compliance
    /// </summary>
    public class NetworkRetentionStatus
    {
        /// <summary>
        /// Network name (voimain-v1.0, aramidmain-v1.0, etc.)
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Whether this network requires MICA compliance
        /// </summary>
        public bool RequiresMicaCompliance { get; set; }

        /// <summary>
        /// Total audit entries for this network
        /// </summary>
        public int TotalAuditEntries { get; set; }

        /// <summary>
        /// Oldest audit entry for this network
        /// </summary>
        public DateTime? OldestEntry { get; set; }

        /// <summary>
        /// Retention period in years
        /// </summary>
        public int RetentionYears { get; set; } = 7;

        /// <summary>
        /// Whether retention requirements are met
        /// </summary>
        public bool MeetsRetentionRequirements { get; set; }

        /// <summary>
        /// Number of assets on this network
        /// </summary>
        public int AssetCount { get; set; }

        /// <summary>
        /// Number of assets with compliance metadata
        /// </summary>
        public int AssetsWithCompliance { get; set; }

        /// <summary>
        /// Compliance coverage percentage (0-100)
        /// </summary>
        public decimal ComplianceCoverage { get; set; }

        /// <summary>
        /// Retention status (Active, Warning, Critical)
        /// </summary>
        public RetentionStatus Status { get; set; }

        /// <summary>
        /// Status message
        /// </summary>
        public string? StatusMessage { get; set; }
    }

    /// <summary>
    /// Retention status enum
    /// </summary>
    public enum RetentionStatus
    {
        /// <summary>
        /// Retention requirements are met
        /// </summary>
        Active,

        /// <summary>
        /// Approaching retention limits
        /// </summary>
        Warning,

        /// <summary>
        /// Retention requirements not met
        /// </summary>
        Critical
    }

    /// <summary>
    /// Request to get audit log health status
    /// </summary>
    public class GetAuditHealthRequest
    {
        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by asset ID
        /// </summary>
        public ulong? AssetId { get; set; }
    }

    /// <summary>
    /// Response containing audit log health status
    /// </summary>
    public class AuditHealthResponse : BaseResponse
    {
        /// <summary>
        /// Audit log health status
        /// </summary>
        public AuditLogHealth AuditHealth { get; set; } = new();
    }

    /// <summary>
    /// Request to get retention status per network
    /// </summary>
    public class GetRetentionStatusRequest
    {
        /// <summary>
        /// Optional filter by specific network
        /// </summary>
        public string? Network { get; set; }
    }

    /// <summary>
    /// Response containing retention status per network
    /// </summary>
    public class RetentionStatusResponse : BaseResponse
    {
        /// <summary>
        /// Retention status for each network
        /// </summary>
        public List<NetworkRetentionStatus> Networks { get; set; } = new();

        /// <summary>
        /// Overall retention health score (0-100)
        /// </summary>
        public int OverallRetentionScore { get; set; }
    }
}
