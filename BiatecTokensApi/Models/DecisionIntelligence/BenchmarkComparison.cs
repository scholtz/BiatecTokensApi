namespace BiatecTokensApi.Models.DecisionIntelligence
{
    /// <summary>
    /// Request for benchmark comparison analysis
    /// </summary>
    public class GetBenchmarkComparisonRequest
    {
        /// <summary>
        /// Primary asset to analyze
        /// </summary>
        public AssetIdentifier PrimaryAsset { get; set; } = new();

        /// <summary>
        /// Comparison assets (competitors/peers)
        /// </summary>
        public List<AssetIdentifier> ComparisonAssets { get; set; } = new();

        /// <summary>
        /// Time window start (UTC). Defaults to 30 days ago.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Time window end (UTC). Defaults to now.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Metrics to compare
        /// </summary>
        /// <remarks>
        /// Valid values: Adoption, Retention, TransactionQuality, LiquidityHealth, ConcentrationRisk
        /// </remarks>
        public List<string> MetricsToCompare { get; set; } = new();

        /// <summary>
        /// Normalization method to use
        /// </summary>
        public NormalizationMethod NormalizationMethod { get; set; } = NormalizationMethod.ZScore;
    }

    /// <summary>
    /// Asset identifier for benchmark comparison
    /// </summary>
    public class AssetIdentifier
    {
        /// <summary>
        /// Asset ID
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Network identifier
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Optional label for display purposes
        /// </summary>
        public string? Label { get; set; }
    }

    /// <summary>
    /// Normalization method for benchmark comparison
    /// </summary>
    public enum NormalizationMethod
    {
        /// <summary>
        /// Z-score normalization (standardization)
        /// </summary>
        /// <remarks>
        /// Converts values to standard deviations from mean: (value - mean) / stddev
        /// Best for normally distributed data
        /// </remarks>
        ZScore,

        /// <summary>
        /// Min-max normalization (scales to 0-100 range)
        /// </summary>
        /// <remarks>
        /// Scales values to 0-100: (value - min) / (max - min) * 100
        /// Best for comparing relative positions
        /// </remarks>
        MinMax,

        /// <summary>
        /// Percentile rank (0-100)
        /// </summary>
        /// <remarks>
        /// Ranks assets by percentile (0 = worst, 100 = best)
        /// Best for understanding relative position regardless of distribution
        /// </remarks>
        Percentile
    }

    /// <summary>
    /// Response containing normalized benchmark comparison
    /// </summary>
    public class BenchmarkComparisonResponse : BaseResponse
    {
        /// <summary>
        /// Primary asset identifier
        /// </summary>
        public AssetIdentifier PrimaryAsset { get; set; } = new();

        /// <summary>
        /// Benchmark comparisons by metric type
        /// </summary>
        public List<MetricBenchmark> Benchmarks { get; set; } = new();

        /// <summary>
        /// Normalization context
        /// </summary>
        public NormalizationContext NormalizationContext { get; set; } = new();

        /// <summary>
        /// Overall benchmark summary
        /// </summary>
        public BenchmarkSummary Summary { get; set; } = new();

        /// <summary>
        /// Metadata about the benchmark calculation
        /// </summary>
        public MetricMetadata Metadata { get; set; } = new();
    }

    /// <summary>
    /// Benchmark comparison for a specific metric
    /// </summary>
    public class MetricBenchmark
    {
        /// <summary>
        /// Metric name (Adoption, Retention, etc.)
        /// </summary>
        public string MetricName { get; set; } = string.Empty;

        /// <summary>
        /// Comparison data points for all assets
        /// </summary>
        public List<BenchmarkDataPoint> DataPoints { get; set; } = new();

        /// <summary>
        /// Statistical summary for this metric
        /// </summary>
        public BenchmarkStatistics Statistics { get; set; } = new();
    }

    /// <summary>
    /// Data point for benchmark comparison
    /// </summary>
    public class BenchmarkDataPoint
    {
        /// <summary>
        /// Asset identifier
        /// </summary>
        public AssetIdentifier Asset { get; set; } = new();

        /// <summary>
        /// Raw value before normalization
        /// </summary>
        public double RawValue { get; set; }

        /// <summary>
        /// Normalized value (scale depends on normalization method)
        /// </summary>
        public double NormalizedValue { get; set; }

        /// <summary>
        /// Percentile rank (0-100)
        /// </summary>
        public double PercentileRank { get; set; }

        /// <summary>
        /// Whether this is the primary asset
        /// </summary>
        public bool IsPrimary { get; set; }

        /// <summary>
        /// Comparison to primary asset
        /// </summary>
        /// <remarks>
        /// Positive = better than primary, Negative = worse than primary, 0 = equal
        /// </remarks>
        public double? DeltaFromPrimary { get; set; }

        /// <summary>
        /// Performance category relative to peers
        /// </summary>
        public PerformanceCategory Category { get; set; }
    }

    /// <summary>
    /// Performance category for benchmark comparison
    /// </summary>
    public enum PerformanceCategory
    {
        /// <summary>
        /// Top performer (>75th percentile)
        /// </summary>
        TopPerformer,

        /// <summary>
        /// Above average (50th-75th percentile)
        /// </summary>
        AboveAverage,

        /// <summary>
        /// Average (25th-50th percentile)
        /// </summary>
        Average,

        /// <summary>
        /// Below average (10th-25th percentile)
        /// </summary>
        BelowAverage,

        /// <summary>
        /// Underperformer (<10th percentile)
        /// </summary>
        Underperformer
    }

    /// <summary>
    /// Statistical summary for benchmark metric
    /// </summary>
    public class BenchmarkStatistics
    {
        /// <summary>
        /// Mean (average) value
        /// </summary>
        public double Mean { get; set; }

        /// <summary>
        /// Median value
        /// </summary>
        public double Median { get; set; }

        /// <summary>
        /// Standard deviation
        /// </summary>
        public double StandardDeviation { get; set; }

        /// <summary>
        /// Minimum value
        /// </summary>
        public double Min { get; set; }

        /// <summary>
        /// Maximum value
        /// </summary>
        public double Max { get; set; }

        /// <summary>
        /// 25th percentile value
        /// </summary>
        public double P25 { get; set; }

        /// <summary>
        /// 75th percentile value
        /// </summary>
        public double P75 { get; set; }

        /// <summary>
        /// Number of data points
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Context about normalization applied to benchmarks
    /// </summary>
    public class NormalizationContext
    {
        /// <summary>
        /// Normalization method used
        /// </summary>
        public NormalizationMethod Method { get; set; }

        /// <summary>
        /// Time window aligned across all comparisons
        /// </summary>
        public DataWindow AlignedWindow { get; set; } = new();

        /// <summary>
        /// Sampling assumptions
        /// </summary>
        public string SamplingAssumptions { get; set; } = "All assets measured over same time period with consistent methodology";

        /// <summary>
        /// Unit consistency notes
        /// </summary>
        public string UnitConsistencyNotes { get; set; } = "All metrics normalized to common scale for valid comparison";

        /// <summary>
        /// Number of assets in comparison
        /// </summary>
        public int AssetCount { get; set; }

        /// <summary>
        /// Caveats about data alignment
        /// </summary>
        public List<string> AlignmentCaveats { get; set; } = new();
    }

    /// <summary>
    /// Overall benchmark summary
    /// </summary>
    public class BenchmarkSummary
    {
        /// <summary>
        /// Primary asset's overall performance rank (1 = best)
        /// </summary>
        public int OverallRank { get; set; }

        /// <summary>
        /// Total assets compared
        /// </summary>
        public int TotalAssetsCompared { get; set; }

        /// <summary>
        /// Primary asset's average percentile across all metrics
        /// </summary>
        public double AveragePercentile { get; set; }

        /// <summary>
        /// Metrics where primary asset is a top performer
        /// </summary>
        public List<string> StrengthMetrics { get; set; } = new();

        /// <summary>
        /// Metrics where primary asset is underperforming
        /// </summary>
        public List<string> WeaknessMetrics { get; set; } = new();

        /// <summary>
        /// Overall competitive position
        /// </summary>
        public string CompetitivePosition { get; set; } = string.Empty;
    }
}
