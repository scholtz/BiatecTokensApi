namespace BiatecTokensApi.Models.PricingReliability
{
    /// <summary>Status of a price quote response.</summary>
    public enum QuoteStatus
    {
        Success,
        Fallback,
        Stale,
        Failed
    }

    /// <summary>Type of pricing source used for a quote.</summary>
    public enum PricingSourceType
    {
        Primary,
        Fallback,
        Synthetic,
        Cached
    }

    /// <summary>Error codes for pricing reliability operations.</summary>
    public enum PricingErrorCode
    {
        None,
        QuoteUnavailable,
        StaleSource,
        PrimarySourceFailed,
        AllSourcesFailed,
        MalformedSymbol,
        UnsupportedAsset,
        PolicyViolation,
        ChainMismatch,
        RateLimitExceeded
    }

    /// <summary>Decision made during the precedence evaluation chain.</summary>
    public enum PrecedenceDecision
    {
        PrimaryUsed,
        FallbackUsed,
        SyntheticUsed,
        ExplicitFailure
    }

    /// <summary>Request model for a reliable price quote.</summary>
    public class PricingReliabilityRequest
    {
        /// <summary>The on-chain asset identifier.</summary>
        public ulong AssetId { get; set; }

        /// <summary>The blockchain network identifier.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Base currency for the quote (default: USD).</summary>
        public string BaseCurrency { get; set; } = "USD";

        /// <summary>Whether to include source provenance in the response.</summary>
        public bool IncludeProvenance { get; set; }

        /// <summary>Whether to include the full fallback chain trace in the response.</summary>
        public bool IncludeFallbackChain { get; set; }

        /// <summary>Optional caller-supplied correlation ID for audit tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>Information about the source used to obtain a price quote.</summary>
    public class QuoteSourceInfo
    {
        /// <summary>The type of pricing source.</summary>
        public PricingSourceType SourceType { get; set; }

        /// <summary>Human-readable name of the source.</summary>
        public string SourceName { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the quote was retrieved.</summary>
        public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Round-trip latency in milliseconds to retrieve the quote.</summary>
        public long LatencyMs { get; set; }

        /// <summary>Whether the quote data is currently stale.</summary>
        public bool IsStale { get; set; }

        /// <summary>Freshness window in seconds before the quote is considered stale.</summary>
        public int FreshnessWindowSeconds { get; set; }

        /// <summary>Confidence score between 0 and 1 for this quote.</summary>
        public decimal ConfidenceScore { get; set; }
    }

    /// <summary>A single entry in the precedence decision trace.</summary>
    public class PrecedenceTraceEntry
    {
        /// <summary>Step index in the precedence chain (1-based).</summary>
        public int Step { get; set; }

        /// <summary>The source type attempted at this step.</summary>
        public PricingSourceType SourceType { get; set; }

        /// <summary>The decision made at this step.</summary>
        public PrecedenceDecision Decision { get; set; }

        /// <summary>Human-readable reason for the decision.</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>UTC timestamp when this step was executed.</summary>
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Deterministic pricing reliability response with source provenance
    /// and full fallback chain trace.
    /// </summary>
    public class PricingReliabilityResponse
    {
        /// <summary>Whether the request succeeded (includes Fallback/Stale as partial success).</summary>
        public bool Success { get; set; }

        /// <summary>The on-chain asset identifier.</summary>
        public ulong AssetId { get; set; }

        /// <summary>The blockchain network identifier.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Correlation ID for audit tracing.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Base currency used for the quote.</summary>
        public string BaseCurrency { get; set; } = "USD";

        /// <summary>Status of the returned quote.</summary>
        public QuoteStatus QuoteStatus { get; set; }

        /// <summary>Current price of the asset in the base currency; null when all sources failed.</summary>
        public decimal? Price { get; set; }

        /// <summary>24-hour price change percentage; null when unavailable.</summary>
        public decimal? PriceChangePercent24h { get; set; }

        /// <summary>24-hour trading volume; null when unavailable.</summary>
        public decimal? Volume24h { get; set; }

        /// <summary>Market capitalisation; null when unavailable.</summary>
        public decimal? MarketCap { get; set; }

        /// <summary>UTC timestamp of the underlying quote data; null when unavailable.</summary>
        public DateTime? LastUpdated { get; set; }

        /// <summary>Information about the source used for this quote; null when all sources failed.</summary>
        public QuoteSourceInfo? SourceInfo { get; set; }

        /// <summary>Full precedence decision trace.</summary>
        public List<PrecedenceTraceEntry> PrecedenceTrace { get; set; } = new();

        /// <summary>Bounded error code; None on success.</summary>
        public PricingErrorCode ErrorCode { get; set; } = PricingErrorCode.None;

        /// <summary>Human-readable error message; null on success.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Actionable remediation hint when an error occurs.</summary>
        public string? RemediationHint { get; set; }

        /// <summary>UTC timestamp when this response was generated.</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Total end-to-end latency in milliseconds for this request.</summary>
        public long LatencyMs { get; set; }
    }

    /// <summary>Health summary for all configured pricing sources.</summary>
    public class PricingSourceHealthSummary
    {
        /// <summary>Whether at least one pricing source is healthy.</summary>
        public bool IsHealthy { get; set; }

        /// <summary>Number of currently available pricing sources.</summary>
        public int AvailableSources { get; set; }

        /// <summary>Total number of configured pricing sources.</summary>
        public int TotalSources { get; set; }

        /// <summary>Names of currently available pricing sources.</summary>
        public List<string> AvailableSourceNames { get; set; } = new();

        /// <summary>Names of currently unavailable pricing sources.</summary>
        public List<string> UnavailableSourceNames { get; set; } = new();

        /// <summary>UTC timestamp when this health check was performed.</summary>
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }
}
