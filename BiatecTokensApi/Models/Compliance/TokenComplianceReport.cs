namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Request for generating a consolidated token compliance report for VOI/Aramid networks
    /// </summary>
    /// <remarks>
    /// This request supports filtering compliance reports by network, asset, and time range
    /// for enterprise reporting and MICA dashboards. Designed for paid subscription tiers.
    /// </remarks>
    public class GetTokenComplianceReportRequest
    {
        /// <summary>
        /// Optional filter by specific asset ID
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional filter by network (voimain-v1.0, aramidmain-v1.0)
        /// </summary>
        /// <remarks>
        /// When specified, only returns compliance data for tokens on the specified network.
        /// Recommended to filter by VOI or Aramid networks for targeted compliance reporting.
        /// </remarks>
        public string? Network { get; set; }

        /// <summary>
        /// Optional start date filter for audit events
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter for audit events
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Include detailed whitelist information in the report
        /// </summary>
        public bool IncludeWhitelistDetails { get; set; } = true;

        /// <summary>
        /// Include recent transfer validation audit events
        /// </summary>
        public bool IncludeTransferAudits { get; set; } = true;

        /// <summary>
        /// Include compliance metadata changes audit log
        /// </summary>
        public bool IncludeComplianceAudits { get; set; } = true;

        /// <summary>
        /// Maximum number of audit entries to include per category
        /// </summary>
        public int MaxAuditEntriesPerCategory { get; set; } = 100;

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
    /// Comprehensive compliance report response for a token
    /// </summary>
    /// <remarks>
    /// This response aggregates compliance metadata, whitelist status, and audit logs
    /// to provide a complete compliance picture for VOI/Aramid tokens. Supports
    /// enterprise dashboards and MICA regulatory reporting requirements.
    /// </remarks>
    public class TokenComplianceReportResponse : BaseResponse
    {
        /// <summary>
        /// List of token compliance status entries
        /// </summary>
        public List<TokenComplianceStatus> Tokens { get; set; } = new();

        /// <summary>
        /// Total number of tokens matching the filter
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
        /// Report generation timestamp
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Network filter applied (if any)
        /// </summary>
        public string? NetworkFilter { get; set; }

        /// <summary>
        /// Subscription tier information for this report
        /// </summary>
        public ReportSubscriptionInfo? SubscriptionInfo { get; set; }
    }

    /// <summary>
    /// Complete compliance status for a single token
    /// </summary>
    /// <remarks>
    /// Combines compliance metadata, whitelist statistics, and recent audit activity
    /// to provide a comprehensive view of token compliance status.
    /// </remarks>
    public class TokenComplianceStatus
    {
        /// <summary>
        /// Asset ID of the token
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Network on which the token is deployed
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Compliance metadata for the token
        /// </summary>
        public ComplianceMetadata? ComplianceMetadata { get; set; }

        /// <summary>
        /// Whitelist summary statistics
        /// </summary>
        public WhitelistSummary? WhitelistSummary { get; set; }

        /// <summary>
        /// Recent compliance metadata audit entries
        /// </summary>
        public List<ComplianceAuditLogEntry>? ComplianceAuditEntries { get; set; }

        /// <summary>
        /// Recent whitelist change audit entries
        /// </summary>
        public List<Whitelist.WhitelistAuditLogEntry>? WhitelistAuditEntries { get; set; }

        /// <summary>
        /// Recent transfer validation audit entries
        /// </summary>
        public List<Whitelist.WhitelistAuditLogEntry>? TransferValidationEntries { get; set; }

        /// <summary>
        /// Overall compliance health score (0-100)
        /// </summary>
        /// <remarks>
        /// Calculated based on:
        /// - Compliance status (Compliant = higher score)
        /// - Verification status (Verified = higher score)
        /// - Whitelist configuration (Active entries = higher score)
        /// - Recent audit activity (No failed validations = higher score)
        /// - VOI/Aramid specific rule compliance
        /// </remarks>
        public int ComplianceHealthScore { get; set; }

        /// <summary>
        /// Compliance warnings or issues identified
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// VOI/Aramid specific compliance status
        /// </summary>
        public NetworkComplianceStatus? NetworkSpecificStatus { get; set; }
    }

    /// <summary>
    /// Summary statistics for token whitelist
    /// </summary>
    public class WhitelistSummary
    {
        /// <summary>
        /// Total number of whitelisted addresses
        /// </summary>
        public int TotalAddresses { get; set; }

        /// <summary>
        /// Number of addresses with Active status
        /// </summary>
        public int ActiveAddresses { get; set; }

        /// <summary>
        /// Number of addresses with Revoked status
        /// </summary>
        public int RevokedAddresses { get; set; }

        /// <summary>
        /// Number of addresses with Suspended status
        /// </summary>
        public int SuspendedAddresses { get; set; }

        /// <summary>
        /// Number of addresses with KYC verification
        /// </summary>
        public int KycVerifiedAddresses { get; set; }

        /// <summary>
        /// Most recent whitelist change timestamp
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// Number of transfer validations in the report period
        /// </summary>
        public int TransferValidationsCount { get; set; }

        /// <summary>
        /// Number of denied transfers in the report period
        /// </summary>
        public int DeniedTransfersCount { get; set; }
    }

    /// <summary>
    /// VOI/Aramid network-specific compliance status
    /// </summary>
    /// <remarks>
    /// Evaluates token compliance against VOI and Aramid network-specific rules
    /// as defined in the compliance service validation logic.
    /// </remarks>
    public class NetworkComplianceStatus
    {
        /// <summary>
        /// Whether the token meets VOI/Aramid network requirements
        /// </summary>
        public bool MeetsNetworkRequirements { get; set; }

        /// <summary>
        /// Specific network rules that are satisfied
        /// </summary>
        public List<string> SatisfiedRules { get; set; } = new();

        /// <summary>
        /// Specific network rules that are violated
        /// </summary>
        public List<string> ViolatedRules { get; set; } = new();

        /// <summary>
        /// Network-specific recommendations
        /// </summary>
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Subscription tier information included in compliance reports
    /// </summary>
    /// <remarks>
    /// Provides transparency about subscription limits and usage for enterprise customers.
    /// Enables customers to understand their tier and upgrade if needed.
    /// </remarks>
    public class ReportSubscriptionInfo
    {
        /// <summary>
        /// Current subscription tier
        /// </summary>
        public string TierName { get; set; } = "Free";

        /// <summary>
        /// Whether audit log access is enabled in this tier
        /// </summary>
        public bool AuditLogEnabled { get; set; }

        /// <summary>
        /// Maximum number of assets that can be included in a single report
        /// </summary>
        public int MaxAssetsPerReport { get; set; }

        /// <summary>
        /// Whether detailed compliance reports are available
        /// </summary>
        public bool DetailedReportsEnabled { get; set; }

        /// <summary>
        /// Message about tier limitations (if any)
        /// </summary>
        public string? LimitationMessage { get; set; }

        /// <summary>
        /// Whether this report was metered for billing
        /// </summary>
        public bool Metered { get; set; }
    }
}
