using System.Text.Json;

namespace BiatecTokensApi.Models.LifecycleIntelligence
{
    /// <summary>
    /// Post-launch risk signal for operational monitoring
    /// </summary>
    public class RiskSignal
    {
        /// <summary>
        /// Unique signal identifier
        /// </summary>
        public string SignalId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Risk signal type
        /// </summary>
        public RiskSignalType Type { get; set; }

        /// <summary>
        /// Signal severity level
        /// </summary>
        public RiskSeverity Severity { get; set; }

        /// <summary>
        /// Current signal value or score
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Signal trend direction
        /// </summary>
        public TrendDirection Trend { get; set; }

        /// <summary>
        /// Human-readable signal description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Asset ID this signal applies to
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Network identifier
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// When this signal was last evaluated
        /// </summary>
        public DateTime LastEvaluatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Next scheduled evaluation time
        /// </summary>
        public DateTime? NextEvaluationAt { get; set; }

        /// <summary>
        /// Historical trend data points (timestamp, value pairs)
        /// </summary>
        public List<DataPoint> TrendHistory { get; set; } = new();

        /// <summary>
        /// Threshold values for this signal
        /// </summary>
        public RiskThresholds? Thresholds { get; set; }

        /// <summary>
        /// Recommended actions to address this signal
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new();

        /// <summary>
        /// Related evidence references
        /// </summary>
        public List<string> EvidenceReferences { get; set; } = new();

        /// <summary>
        /// Additional signal metadata
        /// </summary>
        public JsonElement? Metadata { get; set; }

        /// <summary>
        /// Whether this signal requires immediate attention
        /// </summary>
        public bool RequiresAttention => Severity >= RiskSeverity.High;

        /// <summary>
        /// Confidence in this signal (0.0-1.0)
        /// </summary>
        public double Confidence { get; set; } = 1.0;
    }

    /// <summary>
    /// Type of risk signal
    /// </summary>
    public enum RiskSignalType
    {
        /// <summary>
        /// High concentration of token holdings
        /// </summary>
        HolderConcentration,

        /// <summary>
        /// Low or declining transaction activity
        /// </summary>
        InactivityRisk,

        /// <summary>
        /// Unusual transaction patterns detected
        /// </summary>
        AnomalousActivity,

        /// <summary>
        /// Liquidity below healthy thresholds
        /// </summary>
        LiquidityRisk,

        /// <summary>
        /// High holder churn rate
        /// </summary>
        ChurnRisk,

        /// <summary>
        /// Compliance or regulatory concern
        /// </summary>
        ComplianceRisk,

        /// <summary>
        /// Smart contract security concern
        /// </summary>
        SecurityRisk,

        /// <summary>
        /// Market volatility or price instability
        /// </summary>
        VolatilityRisk,

        /// <summary>
        /// Integration or technical health issue
        /// </summary>
        TechnicalRisk,

        /// <summary>
        /// Unusual whale activity
        /// </summary>
        WhaleMovement
    }

    /// <summary>
    /// Risk severity level
    /// </summary>
    public enum RiskSeverity
    {
        /// <summary>
        /// Informational, no action required
        /// </summary>
        Info = 0,

        /// <summary>
        /// Low priority, monitoring recommended
        /// </summary>
        Low = 1,

        /// <summary>
        /// Medium priority, action recommended
        /// </summary>
        Medium = 2,

        /// <summary>
        /// High priority, action needed soon
        /// </summary>
        High = 3,

        /// <summary>
        /// Critical priority, immediate action required
        /// </summary>
        Critical = 4
    }

    /// <summary>
    /// Trend direction for risk signals
    /// </summary>
    public enum TrendDirection
    {
        /// <summary>
        /// Risk is improving (trend going down)
        /// </summary>
        Improving,

        /// <summary>
        /// Risk is stable (no significant change)
        /// </summary>
        Stable,

        /// <summary>
        /// Risk is worsening (trend going up)
        /// </summary>
        Worsening,

        /// <summary>
        /// Insufficient data to determine trend
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Threshold values for risk signals
    /// </summary>
    public class RiskThresholds
    {
        /// <summary>
        /// Informational threshold
        /// </summary>
        public double? InfoThreshold { get; set; }

        /// <summary>
        /// Low severity threshold
        /// </summary>
        public double? LowThreshold { get; set; }

        /// <summary>
        /// Medium severity threshold
        /// </summary>
        public double? MediumThreshold { get; set; }

        /// <summary>
        /// High severity threshold
        /// </summary>
        public double? HighThreshold { get; set; }

        /// <summary>
        /// Critical severity threshold
        /// </summary>
        public double? CriticalThreshold { get; set; }
    }

    /// <summary>
    /// Data point for trend history
    /// </summary>
    public class DataPoint
    {
        /// <summary>
        /// Timestamp of this data point
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Value at this timestamp
        /// </summary>
        public double Value { get; set; }
    }

    /// <summary>
    /// Request to retrieve risk signals
    /// </summary>
    public class RiskSignalsRequest
    {
        /// <summary>
        /// Asset ID to retrieve signals for
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Network identifier
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Filter by signal types
        /// </summary>
        public List<RiskSignalType>? SignalTypes { get; set; }

        /// <summary>
        /// Minimum severity to include
        /// </summary>
        public RiskSeverity? MinimumSeverity { get; set; }

        /// <summary>
        /// Maximum number of signals to return
        /// </summary>
        public int Limit { get; set; } = 50;

        /// <summary>
        /// Whether to include historical trend data
        /// </summary>
        public bool IncludeTrendHistory { get; set; } = true;
    }

    /// <summary>
    /// Response containing risk signals
    /// </summary>
    public class RiskSignalsResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// List of risk signals
        /// </summary>
        public List<RiskSignal> Signals { get; set; } = new();

        /// <summary>
        /// Total number of signals available (may exceed returned count)
        /// </summary>
        public int TotalSignals { get; set; }

        /// <summary>
        /// Highest severity level among all signals
        /// </summary>
        public RiskSeverity? MaxSeverity { get; set; }

        /// <summary>
        /// Number of signals requiring attention
        /// </summary>
        public int SignalsRequiringAttention { get; set; }

        /// <summary>
        /// Asset ID these signals apply to
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Network identifier
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// When these signals were evaluated
        /// </summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Error message if request failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Overall risk summary
        /// </summary>
        public string Summary { get; set; } = string.Empty;
    }
}
