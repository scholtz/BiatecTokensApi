using System.Text.Json;

namespace BiatecTokensApi.Models.LifecycleIntelligence
{
    /// <summary>
    /// Detailed breakdown of readiness factors with weighted scoring
    /// </summary>
    public class ReadinessFactorBreakdown
    {
        /// <summary>
        /// Unique factor identifier
        /// </summary>
        public string FactorId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable factor name
        /// </summary>
        public string FactorName { get; set; } = string.Empty;

        /// <summary>
        /// Category this factor belongs to
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Weight of this factor in overall score (0.0-1.0)
        /// </summary>
        public double Weight { get; set; }

        /// <summary>
        /// Raw score for this factor (0.0-1.0)
        /// </summary>
        public double RawScore { get; set; }

        /// <summary>
        /// Weighted score (RawScore * Weight)
        /// </summary>
        public double WeightedScore { get; set; }

        /// <summary>
        /// Whether this factor passed evaluation
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Confidence level in this factor's evaluation (0.0-1.0)
        /// </summary>
        public double Confidence { get; set; } = 1.0;

        /// <summary>
        /// Whether this factor blocks token launch
        /// </summary>
        public bool IsBlocking { get; set; }

        /// <summary>
        /// Explanation of the factor's evaluation
        /// </summary>
        public string Explanation { get; set; } = string.Empty;

        /// <summary>
        /// Evidence reference supporting this evaluation
        /// </summary>
        public string? EvidenceReference { get; set; }

        /// <summary>
        /// Additional metadata for this factor
        /// </summary>
        public JsonElement? Metadata { get; set; }

        /// <summary>
        /// Timestamp when this factor was evaluated
        /// </summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Complete readiness scoring with factor-level breakdown
    /// </summary>
    public class ReadinessScore
    {
        /// <summary>
        /// Overall readiness score (0.0-1.0, where 1.0 is fully ready)
        /// </summary>
        public double OverallScore { get; set; }

        /// <summary>
        /// Composite confidence in the overall score (0.0-1.0)
        /// </summary>
        public double OverallConfidence { get; set; } = 1.0;

        /// <summary>
        /// Individual factor breakdowns
        /// </summary>
        public List<ReadinessFactorBreakdown> Factors { get; set; } = new();

        /// <summary>
        /// List of blocking factors preventing launch
        /// </summary>
        public List<string> BlockingFactors { get; set; } = new();

        /// <summary>
        /// Scoring algorithm version
        /// </summary>
        public string ScoringVersion { get; set; } = "v1.0";

        /// <summary>
        /// Timestamp of score calculation
        /// </summary>
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Minimum score threshold to be considered ready (typically 0.8)
        /// </summary>
        public double ReadinessThreshold { get; set; } = 0.8;

        /// <summary>
        /// Whether the overall score meets the readiness threshold
        /// </summary>
        public bool MeetsThreshold => OverallScore >= ReadinessThreshold;
    }
}
