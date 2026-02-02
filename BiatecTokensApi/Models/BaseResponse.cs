namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Base response model for token deployment operations
    /// </summary>
    public class BaseResponse
    {
        /// <summary>
        /// Status of the deployment
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Unique identifier for tracking the deployment progress
        /// </summary>
        /// <remarks>
        /// Use this ID with the /api/v1/token/deployments/{deploymentId} endpoint
        /// to track deployment status in real-time.
        /// </remarks>
        public string? DeploymentId { get; set; }

        /// <summary>
        /// Error message if deployment failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error code for programmatic error handling
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Additional error details (optional, for debugging)
        /// </summary>
        public Dictionary<string, object>? ErrorDetails { get; set; }

        /// <summary>
        /// Timestamp of when the response was created (set at instantiation)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
