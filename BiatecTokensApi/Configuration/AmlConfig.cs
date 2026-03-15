namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// Configuration for AML (Anti-Money Laundering / sanctions screening) provider integration.
    /// </summary>
    public class AmlConfig
    {
        /// <summary>
        /// Provider identifier. Supported values:
        /// <list type="bullet">
        ///   <item><c>Mock</c> – in-memory mock for development and testing (default)</item>
        ///   <item><c>ComplyAdvantage</c> – ComplyAdvantage API screening</item>
        /// </list>
        /// </summary>
        public string Provider { get; set; } = "Mock";

        /// <summary>
        /// Whether AML enforcement is enabled.
        /// When false, checks run but do not block token operations.
        /// </summary>
        public bool EnforcementEnabled { get; set; } = false;

        /// <summary>
        /// Base API endpoint for the AML provider.
        /// Example: <c>https://api.complyadvantage.com</c>
        /// </summary>
        public string? ApiEndpoint { get; set; }

        /// <summary>
        /// API key for authenticating with the AML provider.
        /// Required when Provider is not <c>Mock</c>.
        /// Should be supplied via environment variable or secrets manager, not committed to source.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Webhook secret used to validate incoming provider webhooks.
        /// Required for async callback support.
        /// </summary>
        public string? WebhookSecret { get; set; }

        /// <summary>
        /// Evidence validity window in hours. Approved decisions expire after this period
        /// and must be re-screened. Default is 720 hours (30 days).
        /// Set to 0 to disable expiry.
        /// </summary>
        public int EvidenceValidityHours { get; set; } = 720;

        /// <summary>
        /// Maximum number of retry attempts for transient provider failures.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Initial delay in milliseconds between retry attempts.
        /// Subsequent retries use exponential backoff.
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Request timeout in seconds for individual provider API calls.
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Minimum confidence score (0.0–1.0) above which a "no match" result is trusted.
        /// Results with confidence below this threshold are treated as NeedsReview.
        /// Default: 0.8.
        /// </summary>
        public decimal MinApprovalConfidence { get; set; } = 0.8m;

        /// <summary>
        /// Whether to monitor PEP (Politically Exposed Persons) watchlists in addition to sanctions lists.
        /// Default: true.
        /// </summary>
        public bool IncludePepScreening { get; set; } = true;

        /// <summary>
        /// Whether to monitor adverse media sources.
        /// Default: false (opt-in to avoid false positives).
        /// </summary>
        public bool IncludeAdverseMedia { get; set; } = false;

        /// <summary>
        /// Fuzziness threshold for name matching (0–100).
        /// Higher values return more candidates; lower values are stricter.
        /// Provider-specific interpretation.
        /// Default: 0 (exact match only).
        /// </summary>
        public int FuzzinessThreshold { get; set; } = 0;
    }
}
