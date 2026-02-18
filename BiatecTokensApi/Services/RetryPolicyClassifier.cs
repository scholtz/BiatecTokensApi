using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for classifying errors into retry policies with deterministic rules
    /// </summary>
    /// <remarks>
    /// Implements enterprise-grade retry logic that classifies errors into categories
    /// with explicit retry semantics. This enables:
    /// 1. Deterministic retry behavior across the platform
    /// 2. Clear user guidance on error remediation
    /// 3. Optimal balance between availability and avoiding cascade failures
    /// 
    /// Business Value: Reduces user abandonment by automatically retrying transient
    /// failures while providing clear guidance for permanent errors. Improves platform
    /// reliability perception and reduces support burden.
    /// </remarks>
    public class RetryPolicyClassifier : IRetryPolicyClassifier
    {
        private readonly ILogger<RetryPolicyClassifier> _logger;

        // Maximum retry attempts by policy type
        private const int MAX_IMMEDIATE_RETRIES = 3;
        private const int MAX_DELAY_RETRIES = 5;
        private const int MAX_COOLDOWN_RETRIES = 3;

        // Base delays in seconds
        private const int BASE_DELAY_SECONDS = 10;
        private const int BASE_COOLDOWN_SECONDS = 60;

        // Maximum total retry duration (10 minutes)
        private const int MAX_RETRY_DURATION_SECONDS = 600;

        public RetryPolicyClassifier(ILogger<RetryPolicyClassifier> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Classifies an error code into a retry policy
        /// </summary>
        public RetryPolicyDecision ClassifyError(
            string errorCode,
            DeploymentErrorCategory? errorCategory = null,
            Dictionary<string, object>? context = null)
        {
            _logger.LogDebug("Classifying error: Code={ErrorCode}, Category={Category}",
                LoggingHelper.SanitizeLogInput(errorCode),
                errorCategory);

            // Classification based on error code
            var decision = errorCode switch
            {
                // Not retryable - validation errors
                ErrorCodes.INVALID_REQUEST => NotRetryable("Invalid request parameters - user must correct inputs"),
                ErrorCodes.MISSING_REQUIRED_FIELD => NotRetryable("Required field missing - user must provide value"),
                ErrorCodes.INVALID_NETWORK => NotRetryable("Invalid network - user must select valid network"),
                ErrorCodes.INVALID_TOKEN_PARAMETERS => NotRetryable("Invalid token parameters - user must correct values"),
                ErrorCodes.METADATA_VALIDATION_FAILED => NotRetryable("Metadata validation failed - user must fix metadata"),
                ErrorCodes.INVALID_TOKEN_STANDARD => NotRetryable("Invalid token standard - user must select valid standard"),

                // Not retryable - authorization errors
                ErrorCodes.UNAUTHORIZED => NotRetryable("Unauthorized - user must authenticate"),
                ErrorCodes.FORBIDDEN => NotRetryable("Forbidden - user lacks permissions"),
                ErrorCodes.INVALID_AUTH_TOKEN => NotRetryable("Invalid auth token - user must re-authenticate"),

                // Not retryable - resource conflicts
                ErrorCodes.ALREADY_EXISTS => NotRetryable("Resource already exists - user must use different identifier"),
                ErrorCodes.CONFLICT => NotRetryable("Conflict detected - user must resolve conflict"),

                // Retryable with delay - network errors
                ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR => RetryableWithDelay("Blockchain network temporarily unavailable", 30, maxRetries: 5),
                ErrorCodes.IPFS_SERVICE_ERROR => RetryableWithDelay("IPFS service temporarily unavailable", 20, maxRetries: 4),
                ErrorCodes.EXTERNAL_SERVICE_ERROR => RetryableWithDelay("External service temporarily unavailable", 15, maxRetries: 3),
                ErrorCodes.TIMEOUT => RetryableWithDelay("Request timeout - retrying with backoff", 10, maxRetries: 4),

                // Retryable with cooldown - rate limits and circuit breakers
                ErrorCodes.CIRCUIT_BREAKER_OPEN => RetryableWithCooldown("Circuit breaker open - service recovering", 120, maxRetries: 3),
                ErrorCodes.RATE_LIMIT_EXCEEDED => RetryableWithCooldown("Rate limit exceeded - wait before retry", 60, maxRetries: 2, remediationGuidance: "Wait 60 seconds or upgrade subscription tier"),
                ErrorCodes.SUBSCRIPTION_LIMIT_REACHED => RetryableWithCooldown("Subscription limit reached", 300, maxRetries: 1, remediationGuidance: "Upgrade subscription tier or wait for quota reset"),

                // Retryable after remediation - user actions required
                ErrorCodes.INSUFFICIENT_FUNDS => RetryableAfterRemediation("Add funds to account before retry", remediationGuidance: "Add sufficient funds to your account to cover transaction fees"),
                ErrorCodes.KYC_REQUIRED => RetryableAfterRemediation("Complete KYC verification before retry", remediationGuidance: "Complete KYC verification in your account settings"),
                ErrorCodes.KYC_NOT_VERIFIED => RetryableAfterRemediation("Complete KYC verification before retry", remediationGuidance: "Your KYC verification is pending or incomplete"),
                ErrorCodes.FEATURE_NOT_AVAILABLE => RetryableAfterRemediation("Upgrade subscription tier", remediationGuidance: "This feature requires a higher subscription tier"),
                ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED => RetryableAfterRemediation("Upgrade subscription or wait for quota reset", remediationGuidance: "Upgrade to a higher tier or wait for your monthly quota to reset"),

                // Retryable after configuration - admin action required
                ErrorCodes.CONFIGURATION_ERROR => RetryableAfterConfiguration("System configuration error - contact support"),
                ErrorCodes.PRICE_NOT_CONFIGURED => RetryableAfterConfiguration("Price configuration missing - contact support"),

                // Retryable with delay - transaction failures (may be transient)
                ErrorCodes.TRANSACTION_FAILED => RetryableWithDelay("Transaction failed - may be due to network congestion", 45, maxRetries: 3),
                ErrorCodes.GAS_ESTIMATION_FAILED => RetryableWithDelay("Gas estimation failed - retrying", 20, maxRetries: 2),
                ErrorCodes.TRANSACTION_REJECTED => RetryableWithDelay("Transaction rejected by network - retrying", 30, maxRetries: 3),

                // Default: classify by category if provided
                _ => errorCategory.HasValue ? ClassifyByCategory(errorCategory.Value, errorCode) : UnknownError(errorCode)
            };

            _logger.LogInformation("Error classified: Code={ErrorCode}, Policy={Policy}, MaxRetries={MaxRetries}",
                LoggingHelper.SanitizeLogInput(errorCode),
                decision.Policy,
                decision.MaxRetryAttempts);

            return decision;
        }

        /// <summary>
        /// Determines if a retry should be attempted
        /// </summary>
        public bool ShouldRetry(RetryPolicy policy, int attemptCount, DateTime firstAttemptTime)
        {
            // Check if max retry duration exceeded
            var elapsedSeconds = (DateTime.UtcNow - firstAttemptTime).TotalSeconds;
            if (elapsedSeconds > MAX_RETRY_DURATION_SECONDS)
            {
                _logger.LogWarning("Max retry duration exceeded: ElapsedSeconds={ElapsedSeconds}, MaxDuration={MaxDuration}",
                    elapsedSeconds, MAX_RETRY_DURATION_SECONDS);
                return false;
            }

            // Check attempt count against policy-specific maximums
            var maxAttempts = policy switch
            {
                RetryPolicy.NotRetryable => 0,
                RetryPolicy.RetryableImmediate => MAX_IMMEDIATE_RETRIES,
                RetryPolicy.RetryableWithDelay => MAX_DELAY_RETRIES,
                RetryPolicy.RetryableWithCooldown => MAX_COOLDOWN_RETRIES,
                RetryPolicy.RetryableAfterRemediation => 0, // Requires user action, no auto-retry
                RetryPolicy.RetryableAfterConfiguration => 0, // Requires admin action, no auto-retry
                _ => 0
            };

            var shouldRetry = attemptCount < maxAttempts;

            _logger.LogDebug("Retry decision: Policy={Policy}, Attempt={Attempt}, Max={Max}, ShouldRetry={ShouldRetry}",
                policy, attemptCount, maxAttempts, shouldRetry);

            return shouldRetry;
        }

        /// <summary>
        /// Calculates the recommended delay before next retry
        /// </summary>
        public int CalculateRetryDelay(RetryPolicy policy, int attemptCount, bool useExponentialBackoff)
        {
            var baseDelay = policy switch
            {
                RetryPolicy.RetryableImmediate => 0,
                RetryPolicy.RetryableWithDelay => BASE_DELAY_SECONDS,
                RetryPolicy.RetryableWithCooldown => BASE_COOLDOWN_SECONDS,
                _ => 0
            };

            if (useExponentialBackoff && attemptCount > 0)
            {
                // Exponential backoff: delay = baseDelay * 2^(attemptCount - 1)
                // Cap at 5 minutes
                var exponentialDelay = baseDelay * (int)Math.Pow(2, attemptCount - 1);
                return Math.Min(exponentialDelay, 300);
            }

            return baseDelay;
        }

        #region Private Helper Methods

        private RetryPolicyDecision NotRetryable(string explanation)
        {
            return new RetryPolicyDecision
            {
                Policy = RetryPolicy.NotRetryable,
                ReasonCode = "NOT_RETRYABLE",
                Explanation = explanation,
                MaxRetryAttempts = 0,
                UseExponentialBackoff = false
            };
        }

        private RetryPolicyDecision RetryableWithDelay(string explanation, int delaySeconds, int maxRetries, string? remediationGuidance = null)
        {
            return new RetryPolicyDecision
            {
                Policy = RetryPolicy.RetryableWithDelay,
                ReasonCode = "RETRYABLE_WITH_DELAY",
                Explanation = explanation,
                SuggestedDelaySeconds = delaySeconds,
                MaxRetryAttempts = maxRetries,
                UseExponentialBackoff = true,
                RemediationGuidance = remediationGuidance
            };
        }

        private RetryPolicyDecision RetryableWithCooldown(string explanation, int cooldownSeconds, int maxRetries, string? remediationGuidance = null)
        {
            return new RetryPolicyDecision
            {
                Policy = RetryPolicy.RetryableWithCooldown,
                ReasonCode = "RETRYABLE_WITH_COOLDOWN",
                Explanation = explanation,
                SuggestedDelaySeconds = cooldownSeconds,
                MaxRetryAttempts = maxRetries,
                UseExponentialBackoff = false,
                RemediationGuidance = remediationGuidance
            };
        }

        private RetryPolicyDecision RetryableAfterRemediation(string explanation, string remediationGuidance)
        {
            return new RetryPolicyDecision
            {
                Policy = RetryPolicy.RetryableAfterRemediation,
                ReasonCode = "RETRYABLE_AFTER_REMEDIATION",
                Explanation = explanation,
                MaxRetryAttempts = 0,
                UseExponentialBackoff = false,
                RemediationGuidance = remediationGuidance
            };
        }

        private RetryPolicyDecision RetryableAfterConfiguration(string explanation)
        {
            return new RetryPolicyDecision
            {
                Policy = RetryPolicy.RetryableAfterConfiguration,
                ReasonCode = "RETRYABLE_AFTER_CONFIGURATION",
                Explanation = explanation,
                MaxRetryAttempts = 0,
                UseExponentialBackoff = false,
                RemediationGuidance = "Contact system administrator or support for assistance"
            };
        }

        private RetryPolicyDecision ClassifyByCategory(DeploymentErrorCategory category, string errorCode)
        {
            return category switch
            {
                DeploymentErrorCategory.NetworkError => RetryableWithDelay("Network error - retrying with backoff", 20, maxRetries: 4),
                DeploymentErrorCategory.ValidationError => NotRetryable("Validation error - user must correct inputs"),
                DeploymentErrorCategory.ComplianceError => NotRetryable("Compliance check failed - user must meet requirements"),
                DeploymentErrorCategory.UserRejection => NotRetryable("User cancelled operation"),
                DeploymentErrorCategory.InsufficientFunds => RetryableAfterRemediation("Insufficient funds - add funds to account", "Add funds to your account"),
                DeploymentErrorCategory.TransactionFailure => RetryableWithDelay("Transaction failed - retrying", 30, maxRetries: 3),
                DeploymentErrorCategory.ConfigurationError => RetryableAfterConfiguration("Configuration error - contact support"),
                DeploymentErrorCategory.RateLimitExceeded => RetryableWithCooldown("Rate limit exceeded", 60, maxRetries: 2),
                DeploymentErrorCategory.InternalError => RetryableWithDelay("Internal error - retrying", 30, maxRetries: 3),
                _ => UnknownError(errorCode)
            };
        }

        private RetryPolicyDecision UnknownError(string errorCode)
        {
            _logger.LogWarning("Unknown error code encountered: {ErrorCode}", LoggingHelper.SanitizeLogInput(errorCode));

            return new RetryPolicyDecision
            {
                Policy = RetryPolicy.RetryableWithDelay,
                ReasonCode = "UNKNOWN_ERROR",
                Explanation = "Unknown error - attempting cautious retry",
                SuggestedDelaySeconds = 30,
                MaxRetryAttempts = 2,
                UseExponentialBackoff = true,
                RemediationGuidance = "If error persists, contact support"
            };
        }

        #endregion
    }
}
