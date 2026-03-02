using System.Text.Json.Serialization;

namespace BiatecTokensApi.Models.TokenLaunch
{
    /// <summary>
    /// Response containing the computed trust score for a token
    /// </summary>
    public class TokenTrustScoreResponse
    {
        /// <summary>
        /// Token identifier (asset ID or contract address)
        /// </summary>
        public string TokenIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Overall trust score (0-100)
        /// </summary>
        public int TrustScore { get; set; }

        /// <summary>
        /// Trust level category derived from the score
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TokenTrustLevel TrustLevel { get; set; }

        /// <summary>
        /// Human-readable trust summary for buyers
        /// </summary>
        public string TrustSummary { get; set; } = string.Empty;

        /// <summary>
        /// Breakdown of score contributions by dimension
        /// </summary>
        public TokenTrustScoreBreakdown Breakdown { get; set; } = new();

        /// <summary>
        /// Trust signals that are present (positive indicators)
        /// </summary>
        public List<TrustSignal> PositiveSignals { get; set; } = new();

        /// <summary>
        /// Trust signals that are missing or negative (risk indicators)
        /// </summary>
        public List<TrustSignal> RiskSignals { get; set; } = new();

        /// <summary>
        /// When this score was computed
        /// </summary>
        public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Score version for reproducibility
        /// </summary>
        public string ScoreVersion { get; set; } = "2026.03.1";
    }

    /// <summary>
    /// Trust level categories for token evaluation
    /// </summary>
    public enum TokenTrustLevel
    {
        /// <summary>
        /// Score 0-24: Very limited trust signals
        /// </summary>
        Minimal,

        /// <summary>
        /// Score 25-49: Basic trust signals present
        /// </summary>
        Low,

        /// <summary>
        /// Score 50-69: Reasonable trust posture for most buyers
        /// </summary>
        Medium,

        /// <summary>
        /// Score 70-89: Strong trust signals, suitable for institutional buyers
        /// </summary>
        High,

        /// <summary>
        /// Score 90-100: Exceptional trust posture with all compliance signals
        /// </summary>
        Exceptional
    }

    /// <summary>
    /// Breakdown of trust score contributions by dimension
    /// </summary>
    public class TokenTrustScoreBreakdown
    {
        /// <summary>
        /// Metadata completeness score contribution (0-25)
        /// </summary>
        public int MetadataScore { get; set; }

        /// <summary>
        /// Compliance and regulatory signal score (0-25)
        /// </summary>
        public int ComplianceScore { get; set; }

        /// <summary>
        /// Deployment quality score (0-25): verified on-chain, confirmed round, etc.
        /// </summary>
        public int DeploymentQualityScore { get; set; }

        /// <summary>
        /// Creator reputation score (0-25): history, account age, prior tokens
        /// </summary>
        public int CreatorReputationScore { get; set; }
    }

    /// <summary>
    /// A specific trust signal (positive or negative) for a token
    /// </summary>
    public class TrustSignal
    {
        /// <summary>
        /// Signal category
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Signal label for display
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of what this signal means
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Score impact (+positive or -negative)
        /// </summary>
        public int ScoreImpact { get; set; }
    }
}
