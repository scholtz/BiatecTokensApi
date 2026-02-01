namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Standardized error response structure for API errors
    /// </summary>
    public class ApiErrorResponse
    {
        /// <summary>
        /// Indicates if the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error code for programmatic error handling
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable error message
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Additional details about the error (optional)
        /// </summary>
        public Dictionary<string, object>? Details { get; set; }

        /// <summary>
        /// Timestamp when the error occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Request path that caused the error
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Correlation ID for tracing requests across services
        /// </summary>
        public string? CorrelationId { get; set; }
    }
}
