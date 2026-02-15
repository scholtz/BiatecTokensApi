namespace BiatecTokensApi.Models.DecisionIntelligence
{
    /// <summary>
    /// Metadata about metric calculation for data quality and trustworthiness
    /// </summary>
    /// <remarks>
    /// Provides context about metric freshness, confidence, and calculation lineage
    /// to enable users to make informed decisions based on data quality signals.
    /// </remarks>
    public class MetricMetadata
    {
        /// <summary>
        /// Timestamp when the metric was generated (UTC)
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Time window for which the metric was calculated
        /// </summary>
        public DataWindow DataWindow { get; set; } = new();

        /// <summary>
        /// Indicator of data freshness (Fresh, Stale, Delayed)
        /// </summary>
        public FreshnessIndicator FreshnessIndicator { get; set; } = FreshnessIndicator.Fresh;

        /// <summary>
        /// Confidence level in the metric calculation (0.0 - 1.0)
        /// </summary>
        /// <remarks>
        /// 1.0 = High confidence (complete data, no anomalies detected)
        /// 0.5 - 0.99 = Medium confidence (minor data gaps or anomalies)
        /// Below 0.5 = Low confidence (significant data quality issues)
        /// </remarks>
        public double ConfidenceHint { get; set; } = 1.0;

        /// <summary>
        /// Version of the calculation algorithm used
        /// </summary>
        /// <remarks>
        /// Format: "v{major}.{minor}" e.g., "v1.0"
        /// Enables tracking of calculation changes over time
        /// </remarks>
        public string CalculationVersion { get; set; } = "v1.0";

        /// <summary>
        /// Data quality caveats or warnings
        /// </summary>
        /// <remarks>
        /// Examples: "Partial data availability", "Anomaly detected in time series",
        /// "Insufficient historical data for confidence calculation"
        /// </remarks>
        public List<string> Caveats { get; set; } = new();

        /// <summary>
        /// Number of data points used in calculation
        /// </summary>
        public int DataPointCount { get; set; }

        /// <summary>
        /// Whether data is complete for the requested window
        /// </summary>
        public bool IsDataComplete { get; set; } = true;

        /// <summary>
        /// Percentage of expected data points that are available (0-100)
        /// </summary>
        public double DataCompleteness { get; set; } = 100.0;
    }

    /// <summary>
    /// Time window for metric calculation
    /// </summary>
    public class DataWindow
    {
        /// <summary>
        /// Start of the data window (UTC)
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End of the data window (UTC)
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Duration of the window in hours
        /// </summary>
        public double DurationHours => (EndTime - StartTime).TotalHours;

        /// <summary>
        /// Human-readable description of the window
        /// </summary>
        /// <remarks>
        /// Examples: "Last 24 hours", "Last 7 days", "Custom: 2026-01-01 to 2026-01-31"
        /// </remarks>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Freshness indicator for metric data
    /// </summary>
    public enum FreshnessIndicator
    {
        /// <summary>
        /// Data is current and up-to-date (within SLA)
        /// </summary>
        Fresh,

        /// <summary>
        /// Data is slightly delayed but usable (exceeds SLA warning threshold)
        /// </summary>
        Delayed,

        /// <summary>
        /// Data is significantly outdated (exceeds SLA critical threshold)
        /// </summary>
        Stale
    }
}
