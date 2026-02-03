using BiatecTokensApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Helpers
{
    /// <summary>
    /// Helper class for building consistent error responses across the API
    /// </summary>
    public static class ErrorResponseBuilder
    {
        /// <summary>
        /// Creates a standardized error response for validation failures
        /// </summary>
        /// <param name="errorMessage">Human-readable error message</param>
        /// <param name="details">Optional additional details</param>
        /// <param name="remediationHint">Optional hint to help resolve the error</param>
        /// <returns>BadRequest result with standardized error response</returns>
        public static IActionResult ValidationError(string errorMessage, Dictionary<string, object>? details = null, string? remediationHint = null)
        {
            return new BadRequestObjectResult(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.INVALID_REQUEST,
                ErrorMessage = errorMessage,
                Details = details,
                RemediationHint = remediationHint,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Creates a standardized error response for blockchain connection failures
        /// </summary>
        /// <param name="network">Network that failed to connect</param>
        /// <param name="details">Optional additional details</param>
        /// <returns>BadGateway result with standardized error response</returns>
        public static IActionResult BlockchainConnectionError(string network, Dictionary<string, object>? details = null)
        {
            return new ObjectResult(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR,
                ErrorMessage = $"Failed to connect to {network} blockchain network. Please try again later.",
                Details = details,
                RemediationHint = "Check network status and availability. If the problem persists, contact support.",
                Timestamp = DateTime.UtcNow
            })
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
        }

        /// <summary>
        /// Creates a standardized error response for transaction failures
        /// </summary>
        /// <param name="errorMessage">Human-readable error message</param>
        /// <param name="details">Optional additional details</param>
        /// <param name="remediationHint">Optional hint to help resolve the error</param>
        /// <returns>UnprocessableEntity result with standardized error response</returns>
        public static IActionResult TransactionError(string errorMessage, Dictionary<string, object>? details = null, string? remediationHint = null)
        {
            return new UnprocessableEntityObjectResult(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.TRANSACTION_FAILED,
                ErrorMessage = errorMessage,
                Details = details,
                RemediationHint = remediationHint ?? "Verify your account balance and transaction parameters, then try again.",
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Creates a standardized error response for IPFS service failures
        /// </summary>
        /// <param name="errorMessage">Human-readable error message</param>
        /// <param name="details">Optional additional details</param>
        /// <returns>BadGateway result with standardized error response</returns>
        public static IActionResult IPFSServiceError(string errorMessage, Dictionary<string, object>? details = null)
        {
            return new ObjectResult(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.IPFS_SERVICE_ERROR,
                ErrorMessage = errorMessage,
                Details = details,
                RemediationHint = "IPFS service is temporarily unavailable. Please try again in a few moments.",
                Timestamp = DateTime.UtcNow
            })
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
        }

        /// <summary>
        /// Creates a standardized error response for timeout errors
        /// </summary>
        /// <param name="operation">Operation that timed out</param>
        /// <param name="details">Optional additional details</param>
        /// <returns>RequestTimeout result with standardized error response</returns>
        public static IActionResult TimeoutError(string operation, Dictionary<string, object>? details = null)
        {
            return new ObjectResult(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.TIMEOUT,
                ErrorMessage = $"The {operation} operation timed out. Please try again.",
                Details = details,
                RemediationHint = "The operation took too long to complete. This may be temporary - please retry your request.",
                Timestamp = DateTime.UtcNow
            })
            {
                StatusCode = StatusCodes.Status408RequestTimeout
            };
        }

        /// <summary>
        /// Creates a standardized error response for internal server errors
        /// </summary>
        /// <param name="errorMessage">Human-readable error message</param>
        /// <param name="isDevelopment">Whether running in development mode</param>
        /// <param name="exception">Optional exception for development details</param>
        /// <returns>InternalServerError result with standardized error response</returns>
        /// <remarks>
        /// Stack traces are only included in Development environment. 
        /// Production environments will never expose stack trace details.
        /// </remarks>
        public static IActionResult InternalServerError(string errorMessage, bool isDevelopment = false, Exception? exception = null)
        {
            var response = new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };

            // Only include exception details in development environment
            // SECURITY: Never expose stack traces in production
            if (isDevelopment && exception != null)
            {
                response.Details = new Dictionary<string, object>
                {
                    { "exceptionType", exception.GetType().Name },
                    { "exceptionMessage", exception.Message },
                    { "stackTrace", exception.StackTrace ?? "No stack trace available" }
                };
            }

            return new ObjectResult(response)
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }

        /// <summary>
        /// Creates a standardized error response for external service failures
        /// </summary>
        /// <param name="serviceName">Name of the external service</param>
        /// <param name="details">Optional additional details</param>
        /// <returns>BadGateway result with standardized error response</returns>
        public static IActionResult ExternalServiceError(string serviceName, Dictionary<string, object>? details = null)
        {
            return new ObjectResult(new ApiErrorResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.EXTERNAL_SERVICE_ERROR,
                ErrorMessage = $"Failed to communicate with {serviceName}. Please try again later.",
                Details = details,
                RemediationHint = "The external service is currently unavailable. Please retry in a few moments.",
                Timestamp = DateTime.UtcNow
            })
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
        }

        /// <summary>
        /// Creates a deployment response with error details
        /// </summary>
        /// <typeparam name="T">Type of response (must inherit from BaseResponse)</typeparam>
        /// <param name="errorCode">Error code</param>
        /// <param name="errorMessage">Human-readable error message</param>
        /// <param name="details">Optional additional details</param>
        /// <param name="remediationHint">Optional hint to help resolve the error</param>
        /// <param name="correlationId">Optional correlation ID for request tracking</param>
        /// <returns>Response object with error information</returns>
        public static T CreateErrorResponse<T>(string errorCode, string errorMessage, Dictionary<string, object>? details = null, string? remediationHint = null, string? correlationId = null) 
            where T : BaseResponse, new()
        {
            return new T
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                ErrorDetails = details,
                RemediationHint = remediationHint,
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId
            };
        }
    }
}
