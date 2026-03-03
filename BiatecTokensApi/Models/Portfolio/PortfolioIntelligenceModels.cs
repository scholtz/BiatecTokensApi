using System.Text.Json.Serialization;

namespace BiatecTokensApi.Models.Portfolio
{
    // ── Enumerations ─────────────────────────────────────────────────────────────

    /// <summary>Overall readiness status of a wallet/token holding for an action.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ActionReadiness
    {
        /// <summary>Ready to execute the action.</summary>
        Ready,
        /// <summary>Conditionally ready; some warnings present.</summary>
        ConditionallyReady,
        /// <summary>Not ready; one or more blockers must be resolved first.</summary>
        NotReady
    }

    /// <summary>Risk level of a portfolio holding or a token.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HoldingRiskLevel
    {
        /// <summary>Low risk – token is well-established and compliant.</summary>
        Low,
        /// <summary>Medium risk – some risk signals detected; review before acting.</summary>
        Medium,
        /// <summary>High risk – significant risk signals; strong caution advised.</summary>
        High,
        /// <summary>Risk could not be determined (missing data).</summary>
        Unknown
    }

    /// <summary>Confidence level for a portfolio intelligence signal.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ConfidenceLevel
    {
        /// <summary>High confidence – data is complete and recently verified.</summary>
        High,
        /// <summary>Medium confidence – data is present but not fully verified.</summary>
        Medium,
        /// <summary>Low confidence – data is partial or unverified.</summary>
        Low
    }

    /// <summary>Category of a portfolio-level opportunity or signal.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OpportunityCategory
    {
        /// <summary>Governance or voting action available.</summary>
        Governance,
        /// <summary>Staking or yield opportunity available.</summary>
        Staking,
        /// <summary>Token compliance action required.</summary>
        ComplianceAction,
        /// <summary>Metadata or profile improvement suggested.</summary>
        MetadataImprovement,
        /// <summary>Network upgrade or migration available.</summary>
        NetworkMigration,
        /// <summary>General information or insight.</summary>
        GeneralInsight
    }

    /// <summary>Status of the wallet connection relative to the requested network.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WalletCompatibilityStatus
    {
        /// <summary>Wallet is connected and compatible with the token network.</summary>
        Compatible,
        /// <summary>Wallet is connected to a different network than the token.</summary>
        NetworkMismatch,
        /// <summary>Wallet is not connected.</summary>
        NotConnected,
        /// <summary>Wallet type is not supported for this token standard.</summary>
        UnsupportedWalletType
    }

    // ── Request / Response models ─────────────────────────────────────────────────

    /// <summary>Request for portfolio intelligence for a specific wallet.</summary>
    public class PortfolioIntelligenceRequest
    {
        /// <summary>Wallet address to evaluate.</summary>
        public string WalletAddress { get; set; } = string.Empty;

        /// <summary>Network identifier (e.g., "algorand-mainnet", "base-mainnet").</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Optional list of asset IDs to restrict analysis to a subset of holdings.</summary>
        public List<ulong>? AssetFilter { get; set; }

        /// <summary>Whether to include detailed risk breakdown per holding.</summary>
        public bool IncludeRiskDetails { get; set; } = true;

        /// <summary>Whether to include discovered portfolio opportunities.</summary>
        public bool IncludeOpportunities { get; set; } = true;

        /// <summary>Optional correlation ID for tracing requests across systems.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Portfolio intelligence response for a wallet.</summary>
    public class PortfolioIntelligenceResponse
    {
        /// <summary>Wallet address this response covers.</summary>
        public string WalletAddress { get; set; } = string.Empty;

        /// <summary>Network this response applies to.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Whether the response is fully populated or partially degraded.</summary>
        public bool IsDegraded { get; set; }

        /// <summary>Which data sources degraded (empty when <see cref="IsDegraded"/> is false).</summary>
        public List<string> DegradedSources { get; set; } = new();

        /// <summary>Aggregated portfolio-level risk level across all holdings.</summary>
        public HoldingRiskLevel AggregateRiskLevel { get; set; } = HoldingRiskLevel.Unknown;

        /// <summary>Confidence level in the aggregate risk assessment.</summary>
        public ConfidenceLevel RiskConfidence { get; set; } = ConfidenceLevel.Low;

        /// <summary>Wallet compatibility with the requested network.</summary>
        public WalletCompatibilityStatus WalletCompatibility { get; set; } = WalletCompatibilityStatus.NotConnected;

        /// <summary>Human-readable description of wallet compatibility state.</summary>
        public string WalletCompatibilityMessage { get; set; } = string.Empty;

        /// <summary>Action readiness for the primary portfolio action.</summary>
        public ActionReadiness ActionReadiness { get; set; } = ActionReadiness.NotReady;

        /// <summary>Summary of portfolio holdings.</summary>
        public PortfolioSummary Summary { get; set; } = new();

        /// <summary>Per-holding intelligence entries (may be empty when degraded).</summary>
        public List<HoldingIntelligence> Holdings { get; set; } = new();

        /// <summary>Discovered opportunities surfaced for user attention.</summary>
        public List<PortfolioOpportunity> Opportunities { get; set; } = new();

        /// <summary>Correlation ID propagated from the request (or generated if absent).</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Timestamp of the evaluation (UTC).</summary>
        public DateTimeOffset EvaluatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>API schema version for contract evolution tracking.</summary>
        public string SchemaVersion { get; set; } = "1.0.0";
    }

    /// <summary>Aggregated summary of a portfolio.</summary>
    public class PortfolioSummary
    {
        /// <summary>Total number of holdings in the portfolio.</summary>
        public int TotalHoldings { get; set; }

        /// <summary>Number of holdings flagged as high risk.</summary>
        public int HighRiskCount { get; set; }

        /// <summary>Number of holdings flagged as medium risk.</summary>
        public int MediumRiskCount { get; set; }

        /// <summary>Number of holdings flagged as low risk.</summary>
        public int LowRiskCount { get; set; }

        /// <summary>Number of holdings with unknown risk.</summary>
        public int UnknownRiskCount { get; set; }

        /// <summary>Number of holdings that are action-ready.</summary>
        public int ActionReadyCount { get; set; }

        /// <summary>Number of holdings with active opportunities.</summary>
        public int WithOpportunitiesCount { get; set; }
    }

    /// <summary>Intelligence entry for a single portfolio holding.</summary>
    public class HoldingIntelligence
    {
        /// <summary>Asset/token identifier.</summary>
        public ulong AssetId { get; set; }

        /// <summary>Token name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Token symbol.</summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>Token standard (e.g., "ASA", "ARC3", "ERC20").</summary>
        public string Standard { get; set; } = string.Empty;

        /// <summary>Risk level for this holding.</summary>
        public HoldingRiskLevel RiskLevel { get; set; } = HoldingRiskLevel.Unknown;

        /// <summary>Confidence in the risk assessment for this holding.</summary>
        public ConfidenceLevel RiskConfidence { get; set; } = ConfidenceLevel.Low;

        /// <summary>Action readiness for this specific holding.</summary>
        public ActionReadiness ActionReadiness { get; set; } = ActionReadiness.NotReady;

        /// <summary>Risk signals contributing to the risk level.</summary>
        public List<RiskSignal> RiskSignals { get; set; } = new();

        /// <summary>Confidence indicators that drove the risk assessment.</summary>
        public List<ConfidenceIndicator> ConfidenceIndicators { get; set; } = new();

        /// <summary>Human-readable summary of this holding's status.</summary>
        public string StatusSummary { get; set; } = string.Empty;

        /// <summary>Recommended user action for this holding (null when no action needed).</summary>
        public string? RecommendedAction { get; set; }
    }

    /// <summary>A single risk signal contributing to a holding's risk level.</summary>
    public class RiskSignal
    {
        /// <summary>Machine-readable signal code (e.g., "MINT_AUTHORITY_ACTIVE").</summary>
        public string SignalCode { get; set; } = string.Empty;

        /// <summary>Human-readable description of the signal.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Severity of this signal.</summary>
        public HoldingRiskLevel Severity { get; set; } = HoldingRiskLevel.Low;
    }

    /// <summary>A confidence indicator that influences the risk confidence level.</summary>
    public class ConfidenceIndicator
    {
        /// <summary>Indicator key (e.g., "METADATA_VERIFIED", "ONCHAIN_DATA_PRESENT").</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Whether this indicator contributes positively to confidence.</summary>
        public bool IsPositive { get; set; }

        /// <summary>Human-readable description.</summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>A portfolio-level opportunity surfaced for user attention.</summary>
    public class PortfolioOpportunity
    {
        /// <summary>Category of this opportunity.</summary>
        public OpportunityCategory Category { get; set; }

        /// <summary>Asset ID this opportunity relates to (0 for portfolio-level).</summary>
        public ulong AssetId { get; set; }

        /// <summary>Title of the opportunity.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Detailed description of the opportunity.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Suggested action text for display.</summary>
        public string CallToAction { get; set; } = string.Empty;

        /// <summary>Priority score (higher = more important; 0-100).</summary>
        public int Priority { get; set; }
    }
}
