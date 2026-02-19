namespace BiatecTokensApi.Models.TokenOperationsIntelligence
{
    /// <summary>
    /// API contract version metadata for schema evolution tracking
    /// </summary>
    public class OperationsContractVersion
    {
        /// <summary>
        /// API version string (e.g., "v1.0")
        /// </summary>
        public string ApiVersion { get; set; } = "v1.0";

        /// <summary>
        /// Schema version for this response structure
        /// </summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Minimum client version required to consume this response
        /// </summary>
        public string MinClientVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Whether this response contains any breaking changes from previous versions
        /// </summary>
        public bool BackwardCompatible { get; set; } = true;

        /// <summary>
        /// List of deprecated fields in this response (for migration guidance)
        /// </summary>
        public List<string> DeprecatedFields { get; set; } = new();

        /// <summary>
        /// UTC timestamp when this contract version was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Consolidated token operations intelligence response
    /// </summary>
    public class TokenOperationsIntelligenceResponse
    {
        /// <summary>
        /// Whether the request was processed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Asset ID that was evaluated
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Network identifier
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Correlation ID for tracing
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// API contract version metadata
        /// </summary>
        public OperationsContractVersion ContractVersion { get; set; } = new();

        /// <summary>
        /// Aggregated token health assessment from all policy dimensions
        /// </summary>
        public TokenHealthAssessment? Health { get; set; }

        /// <summary>
        /// Ordered lifecycle recommendations (highest priority first)
        /// </summary>
        public List<LifecycleRecommendation> Recommendations { get; set; } = new();

        /// <summary>
        /// Recent normalized token-affecting events
        /// </summary>
        public List<NormalizedTokenEvent> Events { get; set; } = new();

        /// <summary>
        /// Whether this response is in degraded mode due to partial upstream failures
        /// </summary>
        public bool IsDegraded { get; set; }

        /// <summary>
        /// List of upstream data sources that failed (when IsDegraded = true)
        /// </summary>
        public List<string> DegradedSources { get; set; } = new();

        /// <summary>
        /// UTC timestamp when this response was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether health data is from cache
        /// </summary>
        public bool HealthFromCache { get; set; }

        /// <summary>
        /// Machine-readable error code (populated on failure)
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Human-readable error message with remediation hint (populated on failure)
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
