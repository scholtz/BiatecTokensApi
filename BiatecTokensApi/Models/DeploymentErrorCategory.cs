namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Categories of deployment errors for structured error handling and reporting
    /// </summary>
    /// <remarks>
    /// Error categories help the frontend provide appropriate user feedback and
    /// enable better analytics for failure analysis. Each category maps to specific
    /// remediation strategies and user-facing messages.
    /// </remarks>
    public enum DeploymentErrorCategory
    {
        /// <summary>
        /// Error category is unknown or not yet categorized
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Network connectivity or blockchain RPC error
        /// </summary>
        /// <remarks>
        /// Includes timeouts, connection failures, and blockchain node unavailability.
        /// These errors are typically retryable after a delay.
        /// </remarks>
        NetworkError = 1,

        /// <summary>
        /// Input validation error (invalid parameters, missing fields)
        /// </summary>
        /// <remarks>
        /// User must correct the request parameters. Not retryable without changes.
        /// </remarks>
        ValidationError = 2,

        /// <summary>
        /// Compliance or regulatory check failure
        /// </summary>
        /// <remarks>
        /// Includes KYC failures, whitelist violations, and regulatory constraint violations.
        /// Requires compliance remediation before retry.
        /// </remarks>
        ComplianceError = 3,

        /// <summary>
        /// User rejected the transaction or cancelled the operation
        /// </summary>
        /// <remarks>
        /// User-initiated cancellation. May be retried if user chooses to proceed.
        /// </remarks>
        UserRejection = 4,

        /// <summary>
        /// Insufficient funds for transaction fees or gas
        /// </summary>
        /// <remarks>
        /// User needs to add funds to their account before retry.
        /// </remarks>
        InsufficientFunds = 5,

        /// <summary>
        /// Transaction failed on the blockchain (reverted, rejected by network)
        /// </summary>
        /// <remarks>
        /// Could be due to smart contract logic, network congestion, or invalid transaction.
        /// May be retryable after investigation.
        /// </remarks>
        TransactionFailure = 6,

        /// <summary>
        /// Configuration error (missing settings, invalid network configuration)
        /// </summary>
        /// <remarks>
        /// System administrator must fix configuration. User cannot remediate.
        /// </remarks>
        ConfigurationError = 7,

        /// <summary>
        /// Rate limit or quota exceeded
        /// </summary>
        /// <remarks>
        /// User must wait or upgrade subscription tier. Retryable after cooldown.
        /// </remarks>
        RateLimitExceeded = 8,

        /// <summary>
        /// Internal system error
        /// </summary>
        /// <remarks>
        /// Unexpected error requiring engineering investigation. May be retryable.
        /// </remarks>
        InternalError = 9
    }

    /// <summary>
    /// Detailed deployment error information
    /// </summary>
    /// <remarks>
    /// Provides structured error information that can be used for debugging,
    /// user feedback, and analytics. Includes both technical and user-friendly messages.
    /// </remarks>
    public class DeploymentError
    {
        /// <summary>
        /// Error category for classification and handling
        /// </summary>
        public DeploymentErrorCategory Category { get; set; }

        /// <summary>
        /// Standardized error code from ErrorCodes class
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Technical error message for logging and debugging
        /// </summary>
        public string TechnicalMessage { get; set; } = string.Empty;

        /// <summary>
        /// User-friendly error message suitable for display in UI
        /// </summary>
        public string UserMessage { get; set; } = string.Empty;

        /// <summary>
        /// Whether this error is retryable
        /// </summary>
        public bool IsRetryable { get; set; }

        /// <summary>
        /// Suggested retry delay in seconds (if retryable)
        /// </summary>
        public int? SuggestedRetryDelaySeconds { get; set; }

        /// <summary>
        /// Additional context for debugging
        /// </summary>
        public Dictionary<string, object>? Context { get; set; }

        /// <summary>
        /// Timestamp when the error occurred (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Helper class for creating deployment errors with consistent categorization
    /// </summary>
    public static class DeploymentErrorFactory
    {
        /// <summary>
        /// Creates a network error
        /// </summary>
        public static DeploymentError NetworkError(string technicalMessage, string? context = null)
        {
            return new DeploymentError
            {
                Category = DeploymentErrorCategory.NetworkError,
                ErrorCode = ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR,
                TechnicalMessage = technicalMessage,
                UserMessage = "Unable to connect to the blockchain network. Please try again in a few moments.",
                IsRetryable = true,
                SuggestedRetryDelaySeconds = 30,
                Context = context != null ? new Dictionary<string, object> { { "details", context } } : null
            };
        }

        /// <summary>
        /// Creates a validation error
        /// </summary>
        public static DeploymentError ValidationError(string technicalMessage, string userMessage)
        {
            return new DeploymentError
            {
                Category = DeploymentErrorCategory.ValidationError,
                ErrorCode = ErrorCodes.INVALID_REQUEST,
                TechnicalMessage = technicalMessage,
                UserMessage = userMessage,
                IsRetryable = false
            };
        }

        /// <summary>
        /// Creates a compliance error
        /// </summary>
        public static DeploymentError ComplianceError(string technicalMessage, string userMessage)
        {
            return new DeploymentError
            {
                Category = DeploymentErrorCategory.ComplianceError,
                ErrorCode = ErrorCodes.FORBIDDEN,
                TechnicalMessage = technicalMessage,
                UserMessage = userMessage,
                IsRetryable = false
            };
        }

        /// <summary>
        /// Creates a user rejection error
        /// </summary>
        public static DeploymentError UserRejection(string reason)
        {
            return new DeploymentError
            {
                Category = DeploymentErrorCategory.UserRejection,
                ErrorCode = ErrorCodes.TRANSACTION_REJECTED,
                TechnicalMessage = $"User rejected deployment: {reason}",
                UserMessage = "Deployment cancelled by user.",
                IsRetryable = true
            };
        }

        /// <summary>
        /// Creates an insufficient funds error
        /// </summary>
        public static DeploymentError InsufficientFunds(string requiredAmount, string availableAmount)
        {
            return new DeploymentError
            {
                Category = DeploymentErrorCategory.InsufficientFunds,
                ErrorCode = ErrorCodes.INSUFFICIENT_FUNDS,
                TechnicalMessage = $"Insufficient funds: required {requiredAmount}, available {availableAmount}",
                UserMessage = "Insufficient funds to complete the deployment. Please add funds to your account.",
                IsRetryable = true,
                Context = new Dictionary<string, object>
                {
                    { "required", requiredAmount },
                    { "available", availableAmount }
                }
            };
        }

        /// <summary>
        /// Creates a transaction failure error
        /// </summary>
        public static DeploymentError TransactionFailure(string technicalMessage, string? txHash = null)
        {
            var context = txHash != null
                ? new Dictionary<string, object> { { "transactionHash", txHash } }
                : null;

            return new DeploymentError
            {
                Category = DeploymentErrorCategory.TransactionFailure,
                ErrorCode = ErrorCodes.TRANSACTION_FAILED,
                TechnicalMessage = technicalMessage,
                UserMessage = "The transaction failed on the blockchain. Please check the transaction details and try again.",
                IsRetryable = true,
                SuggestedRetryDelaySeconds = 60,
                Context = context
            };
        }

        /// <summary>
        /// Creates a configuration error
        /// </summary>
        public static DeploymentError ConfigurationError(string technicalMessage)
        {
            return new DeploymentError
            {
                Category = DeploymentErrorCategory.ConfigurationError,
                ErrorCode = ErrorCodes.CONFIGURATION_ERROR,
                TechnicalMessage = technicalMessage,
                UserMessage = "A configuration error occurred. Please contact support.",
                IsRetryable = false
            };
        }

        /// <summary>
        /// Creates a rate limit error
        /// </summary>
        public static DeploymentError RateLimitError(int retryAfterSeconds)
        {
            return new DeploymentError
            {
                Category = DeploymentErrorCategory.RateLimitExceeded,
                ErrorCode = ErrorCodes.RATE_LIMIT_EXCEEDED,
                TechnicalMessage = "Rate limit exceeded",
                UserMessage = $"Too many requests. Please wait {retryAfterSeconds} seconds before trying again.",
                IsRetryable = true,
                SuggestedRetryDelaySeconds = retryAfterSeconds
            };
        }

        /// <summary>
        /// Creates an internal error
        /// </summary>
        public static DeploymentError InternalError(string technicalMessage)
        {
            return new DeploymentError
            {
                Category = DeploymentErrorCategory.InternalError,
                ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                TechnicalMessage = technicalMessage,
                UserMessage = "An unexpected error occurred. Our team has been notified.",
                IsRetryable = true,
                SuggestedRetryDelaySeconds = 120
            };
        }
    }
}
