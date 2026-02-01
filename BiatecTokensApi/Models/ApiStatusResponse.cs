namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Response model for the API status endpoint
    /// </summary>
    public class ApiStatusResponse
    {
        /// <summary>
        /// Overall API status
        /// </summary>
        public string Status { get; set; } = "Healthy";

        /// <summary>
        /// API version
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// API build timestamp
        /// </summary>
        public string? BuildTime { get; set; }

        /// <summary>
        /// Current server timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Application uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Environment name (Development, Production, etc.)
        /// </summary>
        public string Environment { get; set; } = "Unknown";

        /// <summary>
        /// Status of individual components
        /// </summary>
        public Dictionary<string, ComponentStatus> Components { get; set; } = new Dictionary<string, ComponentStatus>();
    }

    /// <summary>
    /// Status information for an individual component
    /// </summary>
    public class ComponentStatus
    {
        /// <summary>
        /// Component status: Healthy, Degraded, or Unhealthy
        /// </summary>
        public string Status { get; set; } = "Unknown";

        /// <summary>
        /// Status description or error message
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Additional component details
        /// </summary>
        public Dictionary<string, object>? Details { get; set; }
    }
}
