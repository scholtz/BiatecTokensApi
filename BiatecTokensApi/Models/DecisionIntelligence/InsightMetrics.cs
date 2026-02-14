namespace BiatecTokensApi.Models.DecisionIntelligence
{
    /// <summary>
    /// Request for token insight metrics
    /// </summary>
    public class GetInsightMetricsRequest
    {
        /// <summary>
        /// Asset ID to analyze
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Network identifier (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, etc.)
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Time window start (UTC). Defaults to 30 days ago.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Time window end (UTC). Defaults to now.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Metrics to include (if empty, returns all)
        /// </summary>
        /// <remarks>
        /// Valid values: Adoption, Retention, TransactionQuality, LiquidityHealth, ConcentrationRisk
        /// </remarks>
        public List<string> RequestedMetrics { get; set; } = new();
    }

    /// <summary>
    /// Response containing token insight metrics
    /// </summary>
    public class InsightMetricsResponse : BaseResponse
    {
        /// <summary>
        /// Asset ID analyzed
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Network identifier
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Adoption metrics
        /// </summary>
        public AdoptionMetrics? Adoption { get; set; }

        /// <summary>
        /// Retention metrics
        /// </summary>
        public RetentionMetrics? Retention { get; set; }

        /// <summary>
        /// Transaction quality metrics
        /// </summary>
        public TransactionQualityMetrics? TransactionQuality { get; set; }

        /// <summary>
        /// Liquidity health metrics
        /// </summary>
        public LiquidityHealthMetrics? LiquidityHealth { get; set; }

        /// <summary>
        /// Concentration risk metrics
        /// </summary>
        public ConcentrationRiskMetrics? ConcentrationRisk { get; set; }

        /// <summary>
        /// Metadata about the metric calculation
        /// </summary>
        public MetricMetadata Metadata { get; set; } = new();
    }

    /// <summary>
    /// Adoption metrics for token ecosystem growth
    /// </summary>
    public class AdoptionMetrics
    {
        /// <summary>
        /// Total unique holder addresses
        /// </summary>
        public int UniqueHolders { get; set; }

        /// <summary>
        /// New holders in the time window
        /// </summary>
        public int NewHolders { get; set; }

        /// <summary>
        /// Growth rate percentage compared to previous period
        /// </summary>
        public double GrowthRate { get; set; }

        /// <summary>
        /// Average new holders per day
        /// </summary>
        public double AverageNewHoldersPerDay { get; set; }

        /// <summary>
        /// Active addresses (made transactions) in the window
        /// </summary>
        public int ActiveAddresses { get; set; }

        /// <summary>
        /// Activity rate: active addresses / total holders (0-100)
        /// </summary>
        public double ActivityRate { get; set; }

        /// <summary>
        /// Adoption trend direction
        /// </summary>
        public TrendDirection Trend { get; set; }
    }

    /// <summary>
    /// Retention metrics for user engagement
    /// </summary>
    public class RetentionMetrics
    {
        /// <summary>
        /// Holders at start of period
        /// </summary>
        public int InitialHolders { get; set; }

        /// <summary>
        /// Holders at end of period
        /// </summary>
        public int CurrentHolders { get; set; }

        /// <summary>
        /// Holders who left (zero balance) during period
        /// </summary>
        public int LostHolders { get; set; }

        /// <summary>
        /// Retention rate percentage (0-100)
        /// </summary>
        public double RetentionRate { get; set; }

        /// <summary>
        /// Churn rate percentage (0-100)
        /// </summary>
        public double ChurnRate { get; set; }

        /// <summary>
        /// Average holding period in days
        /// </summary>
        public double AverageHoldingPeriodDays { get; set; }

        /// <summary>
        /// Median holding period in days
        /// </summary>
        public double MedianHoldingPeriodDays { get; set; }

        /// <summary>
        /// Retention trend direction
        /// </summary>
        public TrendDirection Trend { get; set; }
    }

    /// <summary>
    /// Transaction quality metrics for ecosystem health
    /// </summary>
    public class TransactionQualityMetrics
    {
        /// <summary>
        /// Total transactions in the window
        /// </summary>
        public int TotalTransactions { get; set; }

        /// <summary>
        /// Successful transactions
        /// </summary>
        public int SuccessfulTransactions { get; set; }

        /// <summary>
        /// Failed transactions
        /// </summary>
        public int FailedTransactions { get; set; }

        /// <summary>
        /// Success rate percentage (0-100)
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Average transaction value (in token units)
        /// </summary>
        public double AverageTransactionValue { get; set; }

        /// <summary>
        /// Median transaction value (in token units)
        /// </summary>
        public double MedianTransactionValue { get; set; }

        /// <summary>
        /// Transaction volume (total value transferred)
        /// </summary>
        public double TotalVolume { get; set; }

        /// <summary>
        /// Average transactions per day
        /// </summary>
        public double AverageTransactionsPerDay { get; set; }

        /// <summary>
        /// Transaction quality trend direction
        /// </summary>
        public TrendDirection Trend { get; set; }
    }

    /// <summary>
    /// Liquidity health metrics for market depth
    /// </summary>
    public class LiquidityHealthMetrics
    {
        /// <summary>
        /// Total supply of the token
        /// </summary>
        public double TotalSupply { get; set; }

        /// <summary>
        /// Circulating supply (non-locked, non-burned)
        /// </summary>
        public double CirculatingSupply { get; set; }

        /// <summary>
        /// Locked supply (in escrow, staking, etc.)
        /// </summary>
        public double LockedSupply { get; set; }

        /// <summary>
        /// Circulating supply percentage (0-100)
        /// </summary>
        public double CirculatingSupplyPercentage { get; set; }

        /// <summary>
        /// Trading volume in the window (for tokens with market data)
        /// </summary>
        public double TradingVolume { get; set; }

        /// <summary>
        /// Volume to circulating supply ratio
        /// </summary>
        public double VolumeToCirculatingRatio { get; set; }

        /// <summary>
        /// Liquidity score (0-100, higher is better)
        /// </summary>
        /// <remarks>
        /// Composite score based on trading volume, circulating supply, and holder distribution
        /// </remarks>
        public double LiquidityScore { get; set; }

        /// <summary>
        /// Liquidity health status
        /// </summary>
        public LiquidityStatus Status { get; set; }
    }

    /// <summary>
    /// Concentration risk metrics for distribution analysis
    /// </summary>
    public class ConcentrationRiskMetrics
    {
        /// <summary>
        /// Percentage held by top 10 holders (0-100)
        /// </summary>
        public double Top10HoldersPercentage { get; set; }

        /// <summary>
        /// Percentage held by top 50 holders (0-100)
        /// </summary>
        public double Top50HoldersPercentage { get; set; }

        /// <summary>
        /// Percentage held by top 100 holders (0-100)
        /// </summary>
        public double Top100HoldersPercentage { get; set; }

        /// <summary>
        /// Gini coefficient for wealth distribution (0-1, higher = more concentrated)
        /// </summary>
        public double GiniCoefficient { get; set; }

        /// <summary>
        /// Herfindahl-Hirschman Index (HHI) for concentration
        /// </summary>
        /// <remarks>
        /// Values: 0-1500 = Low concentration, 1500-2500 = Moderate, >2500 = High
        /// </remarks>
        public double HerfindahlIndex { get; set; }

        /// <summary>
        /// Number of addresses holding >1% of supply
        /// </summary>
        public int WhaleCount { get; set; }

        /// <summary>
        /// Concentration risk level
        /// </summary>
        public ConcentrationRisk RiskLevel { get; set; }

        /// <summary>
        /// Concentration trend direction
        /// </summary>
        public TrendDirection Trend { get; set; }
    }

    /// <summary>
    /// Trend direction indicator
    /// </summary>
    public enum TrendDirection
    {
        /// <summary>
        /// Improving/Growing trend
        /// </summary>
        Improving,

        /// <summary>
        /// Stable/Unchanged trend
        /// </summary>
        Stable,

        /// <summary>
        /// Declining/Worsening trend
        /// </summary>
        Declining
    }

    /// <summary>
    /// Liquidity status indicator
    /// </summary>
    public enum LiquidityStatus
    {
        /// <summary>
        /// Excellent liquidity (high volume, good distribution)
        /// </summary>
        Excellent,

        /// <summary>
        /// Good liquidity (adequate volume)
        /// </summary>
        Good,

        /// <summary>
        /// Fair liquidity (moderate volume)
        /// </summary>
        Fair,

        /// <summary>
        /// Poor liquidity (low volume, concerns)
        /// </summary>
        Poor,

        /// <summary>
        /// Insufficient data to determine liquidity
        /// </summary>
        Insufficient
    }

    /// <summary>
    /// Concentration risk level
    /// </summary>
    public enum ConcentrationRisk
    {
        /// <summary>
        /// Low concentration risk (well distributed)
        /// </summary>
        Low,

        /// <summary>
        /// Moderate concentration risk
        /// </summary>
        Moderate,

        /// <summary>
        /// High concentration risk (centralized holdings)
        /// </summary>
        High,

        /// <summary>
        /// Extreme concentration risk
        /// </summary>
        Extreme
    }
}
