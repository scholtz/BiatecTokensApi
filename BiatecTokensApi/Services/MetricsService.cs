using BiatecTokensApi.Models.Metrics;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Implementation of metrics collection service
    /// </summary>
    public class MetricsService : IMetricsService
    {
        private readonly ApiMetrics _metrics = new();
        private readonly ILogger<MetricsService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public MetricsService(ILogger<MetricsService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public void RecordRequest(string endpoint, string method, double durationMs)
        {
            _metrics.IncrementCounter($"http_requests_total.{method}.{SanitizeEndpoint(endpoint)}");
            _metrics.RecordHistogram($"http_request_duration_ms.{method}.{SanitizeEndpoint(endpoint)}", durationMs);
        }

        /// <inheritdoc/>
        public void RecordError(string endpoint, string method, string errorCode)
        {
            _metrics.IncrementCounter($"http_errors_total.{method}.{SanitizeEndpoint(endpoint)}.{errorCode}");
            _metrics.IncrementCounter($"http_errors_by_code.{errorCode}");
        }

        /// <inheritdoc/>
        public void RecordDeployment(string tokenType, bool success, double durationMs)
        {
            var status = success ? "success" : "failure";
            _metrics.IncrementCounter($"token_deployments_total.{tokenType}.{status}");
            _metrics.RecordHistogram($"token_deployment_duration_ms.{tokenType}", durationMs);
            
            // Update success rate gauge
            UpdateDeploymentSuccessRate(tokenType);
        }

        /// <inheritdoc/>
        public void RecordRpcCall(string network, string operation, bool success, double durationMs)
        {
            var status = success ? "success" : "failure";
            _metrics.IncrementCounter($"rpc_calls_total.{network}.{operation}.{status}");
            _metrics.RecordHistogram($"rpc_call_duration_ms.{network}.{operation}", durationMs);
            
            // Update failure rate gauge for monitoring
            UpdateRpcFailureRate(network);
        }

        /// <inheritdoc/>
        public void RecordAuditWrite(string category, bool success)
        {
            var status = success ? "success" : "failure";
            _metrics.IncrementCounter($"audit_writes_total.{category}.{status}");
            
            if (!success)
            {
                _logger.LogWarning("Audit write failed for category {Category}", category);
            }
        }

        /// <inheritdoc/>
        public void IncrementCounter(string name, long increment = 1)
        {
            _metrics.IncrementCounter(name, increment);
        }

        /// <inheritdoc/>
        public void RecordHistogram(string name, double value)
        {
            _metrics.RecordHistogram(name, value);
        }

        /// <inheritdoc/>
        public void SetGauge(string name, double value)
        {
            _metrics.SetGauge(name, value);
        }

        /// <inheritdoc/>
        public Dictionary<string, object> GetMetrics()
        {
            return new Dictionary<string, object>
            {
                { "counters", _metrics.GetCounters() },
                { "histograms", _metrics.GetHistograms() },
                { "gauges", _metrics.GetGauges() }
            };
        }

        private void UpdateDeploymentSuccessRate(string tokenType)
        {
            var successKey = $"token_deployments_total.{tokenType}.success";
            var failureKey = $"token_deployments_total.{tokenType}.failure";
            
            var counters = _metrics.GetCounters();
            var successCount = counters.GetValueOrDefault(successKey, 0);
            var failureCount = counters.GetValueOrDefault(failureKey, 0);
            var total = successCount + failureCount;
            
            if (total > 0)
            {
                var successRate = (double)successCount / total;
                _metrics.SetGauge($"token_deployment_success_rate.{tokenType}", successRate);
            }
        }

        private void UpdateRpcFailureRate(string network)
        {
            var counters = _metrics.GetCounters();
            
            // Calculate failure rate across all operations for this network
            var totalCalls = 0L;
            var failedCalls = 0L;
            
            foreach (var kvp in counters)
            {
                if (kvp.Key.StartsWith($"rpc_calls_total.{network}."))
                {
                    totalCalls += kvp.Value;
                    if (kvp.Key.EndsWith(".failure"))
                    {
                        failedCalls += kvp.Value;
                    }
                }
            }
            
            if (totalCalls > 0)
            {
                var failureRate = (double)failedCalls / totalCalls;
                _metrics.SetGauge($"rpc_failure_rate.{network}", failureRate);
            }
        }

        private static string SanitizeEndpoint(string endpoint)
        {
            // Remove leading slash and replace slashes with dots for metric names
            return endpoint.TrimStart('/').Replace('/', '.').Replace('-', '_');
        }
    }
}
