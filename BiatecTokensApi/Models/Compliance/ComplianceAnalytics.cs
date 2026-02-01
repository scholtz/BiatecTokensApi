namespace BiatecTokensApi.Models.Compliance
{
    // ==================== Regulatory Reporting Analytics ====================
    
    /// <summary>
    /// Request for regulatory reporting analytics
    /// </summary>
    /// <remarks>
    /// Generates aggregated compliance analytics for regulatory submissions.
    /// Designed for MICA/RWA compliance reporting requirements.
    /// </remarks>
    public class GetRegulatoryReportingAnalyticsRequest
    {
        /// <summary>
        /// Optional filter by network (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, etc.)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by token standard (ASA, ARC3, ARC200, ARC1400, ERC20)
        /// </summary>
        public string? TokenStandard { get; set; }

        /// <summary>
        /// Start date for reporting period (ISO 8601 format)
        /// </summary>
        public DateTime FromDate { get; set; }

        /// <summary>
        /// End date for reporting period (ISO 8601 format)
        /// </summary>
        public DateTime ToDate { get; set; }

        /// <summary>
        /// Include detailed asset-level data
        /// </summary>
        public bool IncludeAssetDetails { get; set; } = false;
    }

    /// <summary>
    /// Response for regulatory reporting analytics
    /// </summary>
    public class RegulatoryReportingAnalyticsResponse : BaseResponse
    {
        /// <summary>
        /// Reporting period
        /// </summary>
        public ReportingPeriod Period { get; set; } = new();

        /// <summary>
        /// Compliance summary metrics
        /// </summary>
        public RegulatoryComplianceSummary ComplianceSummary { get; set; } = new();

        /// <summary>
        /// Asset-level details (if requested)
        /// </summary>
        public List<AssetRegulatoryMetrics>? AssetDetails { get; set; }

        /// <summary>
        /// Timestamp when report was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Reporting period information
    /// </summary>
    public class ReportingPeriod
    {
        /// <summary>
        /// Start date of reporting period
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// End date of reporting period
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Number of days in reporting period
        /// </summary>
        public int DurationDays { get; set; }
    }

    /// <summary>
    /// Regulatory compliance summary metrics
    /// </summary>
    public class RegulatoryComplianceSummary
    {
        /// <summary>
        /// Total assets in scope
        /// </summary>
        public int TotalAssets { get; set; }

        /// <summary>
        /// Assets with complete compliance metadata
        /// </summary>
        public int AssetsWithCompleteMetadata { get; set; }

        /// <summary>
        /// Assets meeting MICA requirements
        /// </summary>
        public int MicaCompliantAssets { get; set; }

        /// <summary>
        /// Assets with active whitelist enforcement
        /// </summary>
        public int AssetsWithWhitelistEnforcement { get; set; }

        /// <summary>
        /// Total compliance events in period
        /// </summary>
        public int TotalComplianceEvents { get; set; }

        /// <summary>
        /// Whitelist operations in period
        /// </summary>
        public int WhitelistOperations { get; set; }

        /// <summary>
        /// Blacklist operations in period
        /// </summary>
        public int BlacklistOperations { get; set; }

        /// <summary>
        /// Transfer validations performed
        /// </summary>
        public int TransferValidations { get; set; }

        /// <summary>
        /// Transfers denied due to compliance rules
        /// </summary>
        public int TransfersDenied { get; set; }

        /// <summary>
        /// Network distribution
        /// </summary>
        public Dictionary<string, int> NetworkDistribution { get; set; } = new();

        /// <summary>
        /// Jurisdiction distribution
        /// </summary>
        public Dictionary<string, int> JurisdictionDistribution { get; set; } = new();
    }

    /// <summary>
    /// Regulatory metrics for a single asset
    /// </summary>
    public class AssetRegulatoryMetrics
    {
        /// <summary>
        /// Asset ID
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Token standard
        /// </summary>
        public string? TokenStandard { get; set; }

        /// <summary>
        /// Compliance status
        /// </summary>
        public string? ComplianceStatus { get; set; }

        /// <summary>
        /// MICA compliance percentage
        /// </summary>
        public double MicaCompliancePercentage { get; set; }

        /// <summary>
        /// Number of whitelisted addresses
        /// </summary>
        public int WhitelistedAddresses { get; set; }

        /// <summary>
        /// Compliance events in period
        /// </summary>
        public int ComplianceEventsCount { get; set; }

        /// <summary>
        /// Jurisdictions covered
        /// </summary>
        public string? Jurisdictions { get; set; }
    }

    // ==================== Audit Summary Aggregates ====================

    /// <summary>
    /// Request for audit summary aggregates
    /// </summary>
    /// <remarks>
    /// Generates time-series analytics of compliance audit events.
    /// Supports daily, weekly, and monthly aggregation periods.
    /// </remarks>
    public class GetAuditSummaryAggregatesRequest
    {
        /// <summary>
        /// Optional filter by asset ID
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Start date for analysis period
        /// </summary>
        public DateTime FromDate { get; set; }

        /// <summary>
        /// End date for analysis period
        /// </summary>
        public DateTime ToDate { get; set; }

        /// <summary>
        /// Aggregation period (Daily, Weekly, Monthly)
        /// </summary>
        public AggregationPeriod Period { get; set; } = AggregationPeriod.Daily;
    }

    /// <summary>
    /// Aggregation period for time-series data
    /// </summary>
    public enum AggregationPeriod
    {
        /// <summary>
        /// Daily aggregation
        /// </summary>
        Daily,

        /// <summary>
        /// Weekly aggregation
        /// </summary>
        Weekly,

        /// <summary>
        /// Monthly aggregation
        /// </summary>
        Monthly
    }

    /// <summary>
    /// Response for audit summary aggregates
    /// </summary>
    public class AuditSummaryAggregatesResponse : BaseResponse
    {
        /// <summary>
        /// Analysis period
        /// </summary>
        public ReportingPeriod Period { get; set; } = new();

        /// <summary>
        /// Aggregation period used
        /// </summary>
        public AggregationPeriod AggregationPeriod { get; set; }

        /// <summary>
        /// Time-series data points
        /// </summary>
        public List<AuditTimeSeriesDataPoint> TimeSeries { get; set; } = new();

        /// <summary>
        /// Summary statistics across entire period
        /// </summary>
        public AuditSummaryStatistics Summary { get; set; } = new();

        /// <summary>
        /// Filters applied to the analysis
        /// </summary>
        public AuditAggregateFilters Filters { get; set; } = new();
    }

    /// <summary>
    /// Time-series data point for audit aggregation
    /// </summary>
    public class AuditTimeSeriesDataPoint
    {
        /// <summary>
        /// Period start date
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// Period end date
        /// </summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>
        /// Total events in period
        /// </summary>
        public int TotalEvents { get; set; }

        /// <summary>
        /// Successful events
        /// </summary>
        public int SuccessfulEvents { get; set; }

        /// <summary>
        /// Failed events
        /// </summary>
        public int FailedEvents { get; set; }

        /// <summary>
        /// Events by category
        /// </summary>
        public Dictionary<string, int> EventsByCategory { get; set; } = new();

        /// <summary>
        /// Unique assets involved
        /// </summary>
        public int UniqueAssets { get; set; }

        /// <summary>
        /// Unique users involved
        /// </summary>
        public int UniqueUsers { get; set; }
    }

    /// <summary>
    /// Summary statistics for audit aggregates
    /// </summary>
    public class AuditSummaryStatistics
    {
        /// <summary>
        /// Total events across all periods
        /// </summary>
        public int TotalEvents { get; set; }

        /// <summary>
        /// Average events per period
        /// </summary>
        public double AverageEventsPerPeriod { get; set; }

        /// <summary>
        /// Peak period (most events)
        /// </summary>
        public DateTime? PeakPeriod { get; set; }

        /// <summary>
        /// Peak event count
        /// </summary>
        public int PeakEventCount { get; set; }

        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Most common event category
        /// </summary>
        public string? MostCommonCategory { get; set; }
    }

    /// <summary>
    /// Filters applied to audit aggregation
    /// </summary>
    public class AuditAggregateFilters
    {
        /// <summary>
        /// Asset ID filter
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Network filter
        /// </summary>
        public string? Network { get; set; }
    }

    // ==================== Compliance Trends Analytics ====================

    /// <summary>
    /// Request for compliance trends analytics
    /// </summary>
    /// <remarks>
    /// Analyzes historical compliance status changes over time.
    /// Provides trend analysis for compliance posture tracking.
    /// </remarks>
    public class GetComplianceTrendsRequest
    {
        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by token standard
        /// </summary>
        public string? TokenStandard { get; set; }

        /// <summary>
        /// Start date for trend analysis
        /// </summary>
        public DateTime FromDate { get; set; }

        /// <summary>
        /// End date for trend analysis
        /// </summary>
        public DateTime ToDate { get; set; }

        /// <summary>
        /// Aggregation period for trend analysis
        /// </summary>
        public AggregationPeriod Period { get; set; } = AggregationPeriod.Weekly;
    }

    /// <summary>
    /// Response for compliance trends analytics
    /// </summary>
    public class ComplianceTrendsResponse : BaseResponse
    {
        /// <summary>
        /// Analysis period
        /// </summary>
        public ReportingPeriod Period { get; set; } = new();

        /// <summary>
        /// Aggregation period used
        /// </summary>
        public AggregationPeriod AggregationPeriod { get; set; }

        /// <summary>
        /// Compliance status trends over time
        /// </summary>
        public List<ComplianceStatusTrend> StatusTrends { get; set; } = new();

        /// <summary>
        /// MICA readiness trends over time
        /// </summary>
        public List<MicaReadinessTrend> MicaTrends { get; set; } = new();

        /// <summary>
        /// Whitelist adoption trends over time
        /// </summary>
        public List<WhitelistAdoptionTrend> WhitelistTrends { get; set; } = new();

        /// <summary>
        /// Overall trend direction (Improving, Stable, Declining)
        /// </summary>
        public string TrendDirection { get; set; } = "Stable";
    }

    /// <summary>
    /// Compliance status trend data point
    /// </summary>
    public class ComplianceStatusTrend
    {
        /// <summary>
        /// Period start date
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// Compliant assets count
        /// </summary>
        public int CompliantCount { get; set; }

        /// <summary>
        /// Non-compliant assets count
        /// </summary>
        public int NonCompliantCount { get; set; }

        /// <summary>
        /// Under review assets count
        /// </summary>
        public int UnderReviewCount { get; set; }

        /// <summary>
        /// Compliance rate percentage
        /// </summary>
        public double ComplianceRate { get; set; }
    }

    /// <summary>
    /// MICA readiness trend data point
    /// </summary>
    public class MicaReadinessTrend
    {
        /// <summary>
        /// Period start date
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// Fully compliant assets
        /// </summary>
        public int FullyCompliant { get; set; }

        /// <summary>
        /// Nearly compliant assets
        /// </summary>
        public int NearlyCompliant { get; set; }

        /// <summary>
        /// In progress assets
        /// </summary>
        public int InProgress { get; set; }

        /// <summary>
        /// Average MICA compliance percentage
        /// </summary>
        public double AverageCompliancePercentage { get; set; }
    }

    /// <summary>
    /// Whitelist adoption trend data point
    /// </summary>
    public class WhitelistAdoptionTrend
    {
        /// <summary>
        /// Period start date
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// Assets with whitelist enabled
        /// </summary>
        public int AssetsWithWhitelist { get; set; }

        /// <summary>
        /// Total whitelisted addresses
        /// </summary>
        public int TotalWhitelistedAddresses { get; set; }

        /// <summary>
        /// Whitelist adoption rate percentage
        /// </summary>
        public double AdoptionRate { get; set; }
    }
}
