using System.Text.Json;
using BiatecTokensApi.Models.TokenLaunch;

namespace BiatecTokensApi.Models.LifecycleIntelligence
{
    /// <summary>
    /// Enhanced readiness response with v2 capabilities (factor breakdown, confidence, benchmarking)
    /// </summary>
    public class TokenLaunchReadinessResponseV2
    {
        /// <summary>
        /// API version identifier
        /// </summary>
        public string ApiVersion { get; set; } = "v2.0";

        /// <summary>
        /// Unique evaluation identifier
        /// </summary>
        public string EvaluationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Overall readiness status
        /// </summary>
        public ReadinessStatus Status { get; set; }

        /// <summary>
        /// User-facing summary of readiness state
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Whether token launch can proceed
        /// </summary>
        public bool CanProceed { get; set; }

        /// <summary>
        /// Detailed readiness score with factor breakdown
        /// </summary>
        public ReadinessScore? ReadinessScore { get; set; }

        /// <summary>
        /// List of blocking conditions preventing launch
        /// </summary>
        public List<BlockingCondition> BlockingConditions { get; set; } = new();

        /// <summary>
        /// Ordered list of remediation tasks (if not ready)
        /// </summary>
        public List<RemediationTask> RemediationTasks { get; set; } = new();

        /// <summary>
        /// Detailed evaluation results by category
        /// </summary>
        public ReadinessEvaluationDetails Details { get; set; } = new();

        /// <summary>
        /// Confidence metadata for the overall evaluation
        /// </summary>
        public ConfidenceMetadata Confidence { get; set; } = new();

        /// <summary>
        /// Evidence references supporting this evaluation
        /// </summary>
        public List<EvidenceReference> EvidenceReferences { get; set; } = new();

        /// <summary>
        /// Comparative benchmark data (reserved for future use)
        /// </summary>
        public BenchmarkComparison? BenchmarkComparison { get; set; }

        /// <summary>
        /// Policy version used for evaluation
        /// </summary>
        public string PolicyVersion { get; set; } = string.Empty;

        /// <summary>
        /// Evaluation timestamp (UTC)
        /// </summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Correlation ID for tracking
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Evaluation latency in milliseconds
        /// </summary>
        public long EvaluationTimeMs { get; set; }

        /// <summary>
        /// Caveats or important notes about this evaluation
        /// </summary>
        public List<string> Caveats { get; set; } = new();
    }

    /// <summary>
    /// Blocking condition preventing token launch
    /// </summary>
    public class BlockingCondition
    {
        /// <summary>
        /// Unique condition identifier
        /// </summary>
        public string ConditionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Condition type
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Error code associated with this condition
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Category this condition belongs to
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Whether this condition must be resolved before launch
        /// </summary>
        public bool IsMandatory { get; set; } = true;

        /// <summary>
        /// Required resolution steps
        /// </summary>
        public List<string> ResolutionSteps { get; set; } = new();

        /// <summary>
        /// Evidence reference supporting this blocking condition
        /// </summary>
        public string? EvidenceReference { get; set; }

        /// <summary>
        /// Estimated time to resolve (hours)
        /// </summary>
        public int? EstimatedResolutionHours { get; set; }
    }

    /// <summary>
    /// Confidence metadata for evaluation quality
    /// </summary>
    public class ConfidenceMetadata
    {
        /// <summary>
        /// Overall confidence score (0.0-1.0)
        /// </summary>
        public double OverallConfidence { get; set; } = 1.0;

        /// <summary>
        /// Data completeness percentage (0-100)
        /// </summary>
        public double DataCompleteness { get; set; } = 100.0;

        /// <summary>
        /// Freshness of evaluation data
        /// </summary>
        public DataFreshness Freshness { get; set; } = DataFreshness.Fresh;

        /// <summary>
        /// Number of factors evaluated
        /// </summary>
        public int FactorsEvaluated { get; set; }

        /// <summary>
        /// Number of factors with high confidence
        /// </summary>
        public int HighConfidenceFactors { get; set; }

        /// <summary>
        /// Number of factors with low confidence
        /// </summary>
        public int LowConfidenceFactors { get; set; }

        /// <summary>
        /// Factors that could not be evaluated
        /// </summary>
        public List<string> MissingFactors { get; set; } = new();

        /// <summary>
        /// Quality issues or warnings
        /// </summary>
        public List<string> QualityWarnings { get; set; } = new();
    }

    /// <summary>
    /// Data freshness indicator
    /// </summary>
    public enum DataFreshness
    {
        /// <summary>
        /// Data is fresh (less than 5 minutes old)
        /// </summary>
        Fresh,

        /// <summary>
        /// Data is slightly delayed (5-30 minutes old)
        /// </summary>
        Delayed,

        /// <summary>
        /// Data is stale (more than 30 minutes old)
        /// </summary>
        Stale,

        /// <summary>
        /// Data freshness unknown
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Placeholder for future benchmark comparison capabilities
    /// </summary>
    public class BenchmarkComparison
    {
        /// <summary>
        /// Benchmark dataset version
        /// </summary>
        public string DatasetVersion { get; set; } = string.Empty;

        /// <summary>
        /// Percentile ranking (0-100) compared to similar tokens
        /// </summary>
        public double? PercentileRanking { get; set; }

        /// <summary>
        /// Comparison category
        /// </summary>
        public string? ComparisonCategory { get; set; }

        /// <summary>
        /// Number of tokens in comparison set
        /// </summary>
        public int? ComparisonSetSize { get; set; }

        /// <summary>
        /// Reserved for future benchmark data
        /// </summary>
        public JsonElement? BenchmarkData { get; set; }
    }
}
