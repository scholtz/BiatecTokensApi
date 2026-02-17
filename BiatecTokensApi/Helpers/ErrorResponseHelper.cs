using BiatecTokensApi.Models;

namespace BiatecTokensApi.Helpers
{
    /// <summary>
    /// Helper class for creating standardized error responses with remediation hints
    /// </summary>
    public static class ErrorResponseHelper
    {
        /// <summary>
        /// Creates a standardized error response with correlation ID and remediation hint
        /// </summary>
        public static ApiErrorResponse CreateErrorResponse(
            string errorCode,
            string errorMessage,
            string? remediationHint = null,
            string? correlationId = null,
            string? path = null,
            Dictionary<string, object>? details = null)
        {
            return new ApiErrorResponse
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = LoggingHelper.SanitizeLogInput(errorMessage),
                RemediationHint = remediationHint != null ? LoggingHelper.SanitizeLogInput(remediationHint) : null,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                Path = path,
                Details = details,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Creates error response for authentication failures
        /// </summary>
        public static ApiErrorResponse CreateAuthenticationError(
            string errorCode,
            string errorMessage,
            string? correlationId = null)
        {
            return CreateErrorResponse(
                errorCode,
                errorMessage,
                remediationHint: "Please verify your credentials and try again. Contact support if the problem persists.",
                correlationId: correlationId
            );
        }

        /// <summary>
        /// Creates error response for key management failures
        /// </summary>
        public static ApiErrorResponse CreateKeyManagementError(
            string technicalMessage,
            string? correlationId = null)
        {
            return CreateErrorResponse(
                ErrorCodes.CONFIGURATION_ERROR,
                "Unable to access encryption keys. Please try again later.",
                remediationHint: "This is a system configuration issue. Please contact support with the correlation ID.",
                correlationId: correlationId,
                details: new Dictionary<string, object>
                {
                    { "technicalDetails", LoggingHelper.SanitizeLogInput(technicalMessage) }
                }
            );
        }

        /// <summary>
        /// Creates error response for account lockout
        /// </summary>
        public static ApiErrorResponse CreateAccountLockedError(
            DateTime? lockoutEnd,
            string? correlationId = null)
        {
            var minutesRemaining = lockoutEnd.HasValue
                ? Math.Max(0, (int)(lockoutEnd.Value - DateTime.UtcNow).TotalMinutes)
                : 30;

            return CreateErrorResponse(
                ErrorCodes.ACCOUNT_LOCKED,
                $"Account is locked due to too many failed login attempts.",
                remediationHint: $"Your account will be unlocked in approximately {minutesRemaining} minutes. Please try again later or contact support to unlock your account immediately.",
                correlationId: correlationId,
                details: new Dictionary<string, object>
                {
                    { "lockoutEnd", lockoutEnd ?? DateTime.UtcNow.AddMinutes(30) },
                    { "minutesRemaining", minutesRemaining }
                }
            );
        }

        /// <summary>
        /// Creates error response for ARC76 account readiness issues
        /// </summary>
        public static ApiErrorResponse CreateAccountReadinessError(
            string state,
            string? reason = null,
            string? correlationId = null)
        {
            var remediationHint = state.ToLowerInvariant() switch
            {
                "initializing" => "Your account is being set up. Please wait a moment and try again.",
                "degraded" => "Your account is experiencing issues. Please contact support for assistance.",
                "failed" => "Account initialization failed. Please contact support to resolve this issue.",
                _ => "Your account is not ready for this operation. Please contact support."
            };

            return CreateErrorResponse(
                ErrorCodes.ACCOUNT_NOT_READY,
                $"Account is not ready: {state}",
                remediationHint: remediationHint,
                correlationId: correlationId,
                details: reason != null ? new Dictionary<string, object> { { "reason", LoggingHelper.SanitizeLogInput(reason) } } : null
            );
        }

        /// <summary>
        /// Creates error response for blockchain network issues
        /// </summary>
        public static ApiErrorResponse CreateNetworkError(
            string errorMessage,
            bool isRetryable = true,
            int? retryAfterSeconds = null,
            string? correlationId = null)
        {
            var remediationHint = isRetryable
                ? $"Please try again in a few moments. {(retryAfterSeconds.HasValue ? $"Recommended retry delay: {retryAfterSeconds.Value} seconds." : "")}"
                : "This issue requires intervention. Please contact support.";

            return CreateErrorResponse(
                ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR,
                errorMessage,
                remediationHint: remediationHint,
                correlationId: correlationId,
                details: new Dictionary<string, object>
                {
                    { "isRetryable", isRetryable },
                    { "suggestedRetryDelaySeconds", retryAfterSeconds ?? 30 }
                }
            );
        }

        /// <summary>
        /// Creates error response for validation failures
        /// </summary>
        public static ApiErrorResponse CreateValidationError(
            string errorMessage,
            Dictionary<string, string>? fieldErrors = null,
            string? correlationId = null)
        {
            var details = new Dictionary<string, object>();
            if (fieldErrors != null && fieldErrors.Any())
            {
                details["fieldErrors"] = fieldErrors.ToDictionary(
                    kvp => LoggingHelper.SanitizeLogInput(kvp.Key),
                    kvp => (object)LoggingHelper.SanitizeLogInput(kvp.Value)
                );
            }

            return CreateErrorResponse(
                ErrorCodes.INVALID_REQUEST,
                errorMessage,
                remediationHint: "Please check the request parameters and try again.",
                correlationId: correlationId,
                details: details.Any() ? details : null
            );
        }

        /// <summary>
        /// Creates error response for insufficient funds
        /// </summary>
        public static ApiErrorResponse CreateInsufficientFundsError(
            string requiredAmount,
            string availableAmount,
            string? correlationId = null)
        {
            return CreateErrorResponse(
                ErrorCodes.INSUFFICIENT_FUNDS,
                "Insufficient funds to complete the operation.",
                remediationHint: "Please add funds to your account and try again.",
                correlationId: correlationId,
                details: new Dictionary<string, object>
                {
                    { "required", LoggingHelper.SanitizeLogInput(requiredAmount) },
                    { "available", LoggingHelper.SanitizeLogInput(availableAmount) }
                }
            );
        }

        /// <summary>
        /// Creates error response for rate limiting
        /// </summary>
        public static ApiErrorResponse CreateRateLimitError(
            int retryAfterSeconds,
            string? correlationId = null)
        {
            return CreateErrorResponse(
                ErrorCodes.RATE_LIMIT_EXCEEDED,
                "Too many requests. Please slow down.",
                remediationHint: $"Please wait {retryAfterSeconds} seconds before trying again.",
                correlationId: correlationId,
                details: new Dictionary<string, object>
                {
                    { "retryAfterSeconds", retryAfterSeconds }
                }
            );
        }

        /// <summary>
        /// Creates error response for internal server errors with safe messaging
        /// </summary>
        public static ApiErrorResponse CreateInternalServerError(
            string? correlationId = null,
            string? technicalDetails = null)
        {
            var details = technicalDetails != null
                ? new Dictionary<string, object> { { "technicalDetails", LoggingHelper.SanitizeLogInput(technicalDetails) } }
                : null;

            return CreateErrorResponse(
                ErrorCodes.INTERNAL_SERVER_ERROR,
                "An unexpected error occurred. Our team has been notified.",
                remediationHint: "Please try again later. If the problem persists, contact support with the correlation ID.",
                correlationId: correlationId,
                details: details
            );
        }
    }
}
