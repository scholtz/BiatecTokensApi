using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides metrics endpoints for monitoring and observability
    /// </summary>
    [ApiController]
    [Route("api/v1/metrics")]
    public class MetricsController : ControllerBase
    {
        private readonly IMetricsService _metricsService;
        private readonly ILogger<MetricsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsController"/> class.
        /// </summary>
        /// <param name="metricsService">Metrics service</param>
        /// <param name="logger">Logger instance</param>
        public MetricsController(IMetricsService metricsService, ILogger<MetricsController> logger)
        {
            _metricsService = metricsService;
            _logger = logger;
        }

        /// <summary>
        /// Gets current API metrics
        /// </summary>
        /// <returns>Dictionary of metrics organized by category</returns>
        /// <remarks>
        /// This endpoint provides comprehensive metrics for monitoring:
        /// 
        /// **Counters:**
        /// - http_requests_total: Total requests by method and endpoint
        /// - http_errors_total: Total errors by method, endpoint, and error code
        /// - token_deployments_total: Deployments by token type and status
        /// - rpc_calls_total: RPC calls by network, operation, and status
        /// - audit_writes_total: Audit writes by category and status
        /// 
        /// **Histograms:**
        /// - http_request_duration_ms: Request latency distribution
        /// - token_deployment_duration_ms: Deployment duration distribution
        /// - rpc_call_duration_ms: RPC call latency distribution
        /// 
        /// **Gauges:**
        /// - token_deployment_success_rate: Success rate by token type
        /// - rpc_failure_rate: Failure rate by network
        /// 
        /// **Use Cases:**
        /// - Prometheus scraping for alerting
        /// - Performance monitoring dashboards
        /// - Capacity planning
        /// - Incident investigation
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
        public IActionResult GetMetrics()
        {
            try
            {
                var metrics = _metricsService.GetMetrics();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving metrics");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Failed to retrieve metrics",
                    message = ex.Message
                });
            }
        }
    }
}
