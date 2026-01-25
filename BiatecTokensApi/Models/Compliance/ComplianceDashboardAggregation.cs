namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Request for compliance dashboard aggregation export
    /// </summary>
    /// <remarks>
    /// This request supports filtering aggregated compliance metrics for enterprise reporting
    /// and scheduled compliance exports. Designed for compliance dashboard data feeds.
    /// </remarks>
    public class GetComplianceDashboardAggregationRequest
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
        /// Optional start date filter for compliance data
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter for compliance data
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Include detailed asset breakdown
        /// </summary>
        public bool IncludeAssetBreakdown { get; set; } = false;

        /// <summary>
        /// Maximum number of top restriction reasons to include
        /// </summary>
        public int TopRestrictionsCount { get; set; } = 10;
    }

    /// <summary>
    /// Compliance dashboard aggregation response with export formats
    /// </summary>
    /// <remarks>
    /// This response provides aggregated compliance metrics suitable for
    /// enterprise dashboards, scheduled reports, and compliance posture tracking.
    /// Includes MICA readiness, whitelist status, jurisdiction coverage, and restriction analysis.
    /// </remarks>
    public class ComplianceDashboardAggregationResponse : BaseResponse
    {
        /// <summary>
        /// Aggregated compliance metrics
        /// </summary>
        public ComplianceDashboardMetrics Metrics { get; set; } = new();

        /// <summary>
        /// Network filter applied (if any)
        /// </summary>
        public string? NetworkFilter { get; set; }

        /// <summary>
        /// Token standard filter applied (if any)
        /// </summary>
        public string? TokenStandardFilter { get; set; }

        /// <summary>
        /// Date range filter applied
        /// </summary>
        public AuditDateRange? DateRangeFilter { get; set; }

        /// <summary>
        /// Timestamp when aggregation was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Detailed asset breakdown (if requested)
        /// </summary>
        public List<AssetComplianceSummary>? AssetBreakdown { get; set; }
    }

    /// <summary>
    /// Aggregated compliance metrics for dashboard
    /// </summary>
    public class ComplianceDashboardMetrics
    {
        /// <summary>
        /// Total number of assets in the dataset
        /// </summary>
        public int TotalAssets { get; set; }

        /// <summary>
        /// MICA readiness metrics
        /// </summary>
        public MicaReadinessMetrics MicaReadiness { get; set; } = new();

        /// <summary>
        /// Whitelist status metrics
        /// </summary>
        public WhitelistStatusMetrics WhitelistStatus { get; set; } = new();

        /// <summary>
        /// Jurisdiction coverage metrics
        /// </summary>
        public JurisdictionMetrics Jurisdictions { get; set; } = new();

        /// <summary>
        /// Compliant vs restricted asset counts
        /// </summary>
        public ComplianceCountMetrics ComplianceCounts { get; set; } = new();

        /// <summary>
        /// Top restriction reasons with counts
        /// </summary>
        public List<RestrictionReasonCount> TopRestrictionReasons { get; set; } = new();

        /// <summary>
        /// Token standard distribution
        /// </summary>
        public Dictionary<string, int> TokenStandardDistribution { get; set; } = new();

        /// <summary>
        /// Network distribution
        /// </summary>
        public Dictionary<string, int> NetworkDistribution { get; set; } = new();
    }

    /// <summary>
    /// MICA readiness aggregation metrics
    /// </summary>
    public class MicaReadinessMetrics
    {
        /// <summary>
        /// Number of assets with MICA compliance metadata
        /// </summary>
        public int AssetsWithMetadata { get; set; }

        /// <summary>
        /// Number of assets without MICA compliance metadata
        /// </summary>
        public int AssetsWithoutMetadata { get; set; }

        /// <summary>
        /// Number of fully compliant assets
        /// </summary>
        public int FullyCompliantAssets { get; set; }

        /// <summary>
        /// Number of nearly compliant assets
        /// </summary>
        public int NearlyCompliantAssets { get; set; }

        /// <summary>
        /// Number of assets in progress
        /// </summary>
        public int InProgressAssets { get; set; }

        /// <summary>
        /// Number of non-compliant assets
        /// </summary>
        public int NonCompliantAssets { get; set; }

        /// <summary>
        /// Number of assets not started
        /// </summary>
        public int NotStartedAssets { get; set; }

        /// <summary>
        /// Average MICA compliance percentage across all assets
        /// </summary>
        public double AverageCompliancePercentage { get; set; }
    }

    /// <summary>
    /// Whitelist status aggregation metrics
    /// </summary>
    public class WhitelistStatusMetrics
    {
        /// <summary>
        /// Number of assets with whitelist enabled
        /// </summary>
        public int AssetsWithWhitelist { get; set; }

        /// <summary>
        /// Number of assets without whitelist
        /// </summary>
        public int AssetsWithoutWhitelist { get; set; }

        /// <summary>
        /// Total whitelisted addresses across all assets
        /// </summary>
        public int TotalWhitelistedAddresses { get; set; }

        /// <summary>
        /// Number of active whitelisted addresses
        /// </summary>
        public int ActiveWhitelistedAddresses { get; set; }

        /// <summary>
        /// Number of revoked whitelisted addresses
        /// </summary>
        public int RevokedWhitelistedAddresses { get; set; }

        /// <summary>
        /// Number of suspended whitelisted addresses
        /// </summary>
        public int SuspendedWhitelistedAddresses { get; set; }

        /// <summary>
        /// Average number of whitelisted addresses per asset
        /// </summary>
        public double AverageWhitelistedAddressesPerAsset { get; set; }
    }

    /// <summary>
    /// Jurisdiction coverage aggregation metrics
    /// </summary>
    public class JurisdictionMetrics
    {
        /// <summary>
        /// Number of assets with jurisdiction information
        /// </summary>
        public int AssetsWithJurisdiction { get; set; }

        /// <summary>
        /// Number of assets without jurisdiction information
        /// </summary>
        public int AssetsWithoutJurisdiction { get; set; }

        /// <summary>
        /// Distribution of assets by jurisdiction (country code -> count)
        /// </summary>
        public Dictionary<string, int> JurisdictionDistribution { get; set; } = new();

        /// <summary>
        /// Number of unique jurisdictions covered
        /// </summary>
        public int UniqueJurisdictions { get; set; }

        /// <summary>
        /// Most common jurisdiction
        /// </summary>
        public string? MostCommonJurisdiction { get; set; }
    }

    /// <summary>
    /// Compliant vs restricted asset count metrics
    /// </summary>
    public class ComplianceCountMetrics
    {
        /// <summary>
        /// Number of compliant assets
        /// </summary>
        public int CompliantAssets { get; set; }

        /// <summary>
        /// Number of restricted assets
        /// </summary>
        public int RestrictedAssets { get; set; }

        /// <summary>
        /// Number of assets under review
        /// </summary>
        public int UnderReviewAssets { get; set; }

        /// <summary>
        /// Number of suspended assets
        /// </summary>
        public int SuspendedAssets { get; set; }

        /// <summary>
        /// Number of exempt assets
        /// </summary>
        public int ExemptAssets { get; set; }

        /// <summary>
        /// Compliance rate as percentage (0-100)
        /// </summary>
        public double ComplianceRate { get; set; }
    }

    /// <summary>
    /// Restriction reason with occurrence count
    /// </summary>
    public class RestrictionReasonCount
    {
        /// <summary>
        /// The restriction reason
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Number of assets with this restriction
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Percentage of total restricted assets
        /// </summary>
        public double Percentage { get; set; }
    }

    /// <summary>
    /// Summary of compliance status for a single asset
    /// </summary>
    public class AssetComplianceSummary
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
        /// Token standard (ASA, ARC3, ARC200, ARC1400, ERC20)
        /// </summary>
        public string? TokenStandard { get; set; }

        /// <summary>
        /// MICA compliance status
        /// </summary>
        public string? MicaComplianceStatus { get; set; }

        /// <summary>
        /// Compliance status
        /// </summary>
        public string? ComplianceStatus { get; set; }

        /// <summary>
        /// Whether whitelist is enabled
        /// </summary>
        public bool HasWhitelist { get; set; }

        /// <summary>
        /// Number of whitelisted addresses
        /// </summary>
        public int WhitelistedAddressCount { get; set; }

        /// <summary>
        /// Jurisdiction(s)
        /// </summary>
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Transfer restrictions
        /// </summary>
        public string? TransferRestrictions { get; set; }

        /// <summary>
        /// Last compliance review date
        /// </summary>
        public DateTime? LastComplianceReview { get; set; }
    }
}
