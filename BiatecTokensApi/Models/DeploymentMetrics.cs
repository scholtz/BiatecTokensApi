namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Deployment metrics for monitoring and analytics
    /// </summary>
    /// <remarks>
    /// Provides comprehensive metrics about deployment performance, success rates,
    /// and operational health. Useful for monitoring dashboards and SLA tracking.
    /// </remarks>
    public class DeploymentMetrics
    {
        /// <summary>
        /// Total number of deployments
        /// </summary>
        public int TotalDeployments { get; set; }

        /// <summary>
        /// Number of successful deployments
        /// </summary>
        public int SuccessfulDeployments { get; set; }

        /// <summary>
        /// Number of failed deployments
        /// </summary>
        public int FailedDeployments { get; set; }

        /// <summary>
        /// Number of pending deployments
        /// </summary>
        public int PendingDeployments { get; set; }

        /// <summary>
        /// Number of cancelled deployments
        /// </summary>
        public int CancelledDeployments { get; set; }

        /// <summary>
        /// Success rate as a percentage (0-100)
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Failure rate as a percentage (0-100)
        /// </summary>
        public double FailureRate { get; set; }

        /// <summary>
        /// Average deployment duration in milliseconds
        /// </summary>
        public long AverageDurationMs { get; set; }

        /// <summary>
        /// Median deployment duration in milliseconds
        /// </summary>
        public long MedianDurationMs { get; set; }

        /// <summary>
        /// 95th percentile deployment duration in milliseconds
        /// </summary>
        public long P95DurationMs { get; set; }

        /// <summary>
        /// Fastest deployment duration in milliseconds
        /// </summary>
        public long FastestDurationMs { get; set; }

        /// <summary>
        /// Slowest deployment duration in milliseconds
        /// </summary>
        public long SlowestDurationMs { get; set; }

        /// <summary>
        /// Failure breakdown by error category
        /// </summary>
        public Dictionary<string, int> FailuresByCategory { get; set; } = new();

        /// <summary>
        /// Deployment counts by network
        /// </summary>
        public Dictionary<string, int> DeploymentsByNetwork { get; set; } = new();

        /// <summary>
        /// Deployment counts by token type
        /// </summary>
        public Dictionary<string, int> DeploymentsByTokenType { get; set; } = new();

        /// <summary>
        /// Average duration by status transition (e.g., "Submitted->Confirmed")
        /// </summary>
        public Dictionary<string, long> AverageDurationByTransition { get; set; } = new();

        /// <summary>
        /// Number of retried deployments
        /// </summary>
        public int RetriedDeployments { get; set; }

        /// <summary>
        /// Start of the time period for these metrics (UTC)
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>
        /// End of the time period for these metrics (UTC)
        /// </summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>
        /// Timestamp when these metrics were calculated (UTC)
        /// </summary>
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Request to get deployment metrics
    /// </summary>
    public class GetDeploymentMetricsRequest
    {
        /// <summary>
        /// Optional start date for metrics calculation (default: 24 hours ago)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date for metrics calculation (default: now)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by token type
        /// </summary>
        public string? TokenType { get; set; }

        /// <summary>
        /// Optional filter by deployer address
        /// </summary>
        public string? DeployedBy { get; set; }
    }

    /// <summary>
    /// Response containing deployment metrics
    /// </summary>
    public class DeploymentMetricsResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The calculated metrics
        /// </summary>
        public DeploymentMetrics? Metrics { get; set; }

        /// <summary>
        /// Error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
