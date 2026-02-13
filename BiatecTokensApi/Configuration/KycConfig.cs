namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// Configuration for KYC provider integration
    /// </summary>
    public class KycConfig
    {
        /// <summary>
        /// Whether KYC enforcement is enabled
        /// </summary>
        public bool EnforcementEnabled { get; set; } = false;

        /// <summary>
        /// Default KYC provider to use
        /// </summary>
        public string Provider { get; set; } = "Mock";

        /// <summary>
        /// API endpoint for the KYC provider
        /// </summary>
        public string? ApiEndpoint { get; set; }

        /// <summary>
        /// API key for authentication with provider
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Webhook secret for signature verification
        /// </summary>
        public string? WebhookSecret { get; set; }

        /// <summary>
        /// Verification expiration period in days
        /// </summary>
        public int ExpirationDays { get; set; } = 365;

        /// <summary>
        /// Maximum retry attempts for provider API calls
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Initial retry delay in milliseconds
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to enable mock auto-approval for testing
        /// </summary>
        public bool MockAutoApprove { get; set; } = false;

        /// <summary>
        /// Mock approval delay in seconds (for testing async workflows)
        /// </summary>
        public int MockApprovalDelaySeconds { get; set; } = 5;
    }
}
