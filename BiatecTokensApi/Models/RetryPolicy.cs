namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Classification of error retryability for provider resilience
    /// </summary>
    /// <remarks>
    /// This enum enables deterministic retry behavior across the platform.
    /// Frontend and backend can make consistent decisions about whether to retry,
    /// wait, or fail terminal based on this classification.
    /// 
    /// Business Value: Reduces user confusion and support burden by providing
    /// clear guidance on error remediation. Improves conversion by retrying
    /// transient failures automatically while failing fast on permanent errors.
    /// </remarks>
    public enum RetryPolicy
    {
        /// <summary>
        /// Error is not retryable - operation cannot succeed with same inputs
        /// </summary>
        /// <remarks>
        /// Examples: Validation errors, insufficient permissions, user rejections.
        /// User must change inputs or permissions before retry can succeed.
        /// </remarks>
        NotRetryable = 0,

        /// <summary>
        /// Error is retryable immediately without delay
        /// </summary>
        /// <remarks>
        /// Examples: Idempotent operations, state queries.
        /// Safe to retry immediately as operation has no side effects.
        /// </remarks>
        RetryableImmediate = 1,

        /// <summary>
        /// Error is retryable after a short delay (5-30 seconds)
        /// </summary>
        /// <remarks>
        /// Examples: Network timeouts, temporary RPC unavailability, IPFS delays.
        /// Provider may recover quickly. Exponential backoff recommended.
        /// </remarks>
        RetryableWithDelay = 2,

        /// <summary>
        /// Error is retryable after a longer delay (1-5 minutes)
        /// </summary>
        /// <remarks>
        /// Examples: Rate limiting, blockchain congestion, circuit breaker open.
        /// Requires cooling period before retry can succeed.
        /// </remarks>
        RetryableWithCooldown = 3,

        /// <summary>
        /// Error is retryable only after user remediation
        /// </summary>
        /// <remarks>
        /// Examples: Insufficient funds, KYC required, whitelist violation.
        /// User must take action (add funds, complete KYC) before retry.
        /// </remarks>
        RetryableAfterRemediation = 4,

        /// <summary>
        /// Error is retryable only after configuration change
        /// </summary>
        /// <remarks>
        /// Examples: Missing API keys, invalid network config, service endpoint errors.
        /// System administrator must fix configuration before retry can succeed.
        /// </remarks>
        RetryableAfterConfiguration = 5
    }

    /// <summary>
    /// Metadata about a retry policy decision
    /// </summary>
    /// <remarks>
    /// Provides context for why a particular retry policy was assigned,
    /// supporting debugging and operational visibility.
    /// </remarks>
    public class RetryPolicyDecision
    {
        /// <summary>
        /// The assigned retry policy
        /// </summary>
        public RetryPolicy Policy { get; set; }

        /// <summary>
        /// Reason code for the policy assignment
        /// </summary>
        public string ReasonCode { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable explanation of the decision
        /// </summary>
        public string Explanation { get; set; } = string.Empty;

        /// <summary>
        /// Suggested delay in seconds before retry (if applicable)
        /// </summary>
        public int? SuggestedDelaySeconds { get; set; }

        /// <summary>
        /// Maximum number of retry attempts recommended
        /// </summary>
        public int? MaxRetryAttempts { get; set; }

        /// <summary>
        /// Whether exponential backoff is recommended
        /// </summary>
        public bool UseExponentialBackoff { get; set; }

        /// <summary>
        /// User-facing remediation guidance (if applicable)
        /// </summary>
        public string? RemediationGuidance { get; set; }

        /// <summary>
        /// Timestamp when decision was made (UTC)
        /// </summary>
        public DateTime DecisionTimestamp { get; set; } = DateTime.UtcNow;
    }
}
