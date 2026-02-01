using BiatecTokensApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Reflection;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides health check and status endpoints for monitoring API health and dependencies
    /// </summary>
    [ApiController]
    [Route("api/v1")]
    public class StatusController : ControllerBase
    {
        private readonly HealthCheckService _healthCheckService;
        private readonly ILogger<StatusController> _logger;
        private readonly IHostEnvironment _env;
        private static readonly DateTime _startTime = DateTime.UtcNow;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatusController"/> class.
        /// </summary>
        /// <param name="healthCheckService">Health check service</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="env">Host environment</param>
        public StatusController(
            HealthCheckService healthCheckService,
            ILogger<StatusController> logger,
            IHostEnvironment env)
        {
            _healthCheckService = healthCheckService;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Gets detailed API status including component health
        /// </summary>
        /// <returns>Comprehensive API status information</returns>
        /// <remarks>
        /// This endpoint provides detailed information about:
        /// - Overall API health status
        /// - Version and build information
        /// - Uptime
        /// - Individual component health (IPFS, Algorand networks, EVM chains)
        /// - Environment information
        /// 
        /// **Use Cases:**
        /// - Monitoring dashboards
        /// - Health checks for orchestration systems
        /// - Troubleshooting connectivity issues
        /// - Verifying service configuration
        /// </remarks>
        [HttpGet("status")]
        [ProducesResponseType(typeof(ApiStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiStatusResponse), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var healthReport = await _healthCheckService.CheckHealthAsync();
                
                var response = new ApiStatusResponse
                {
                    Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                    BuildTime = GetBuildTime(),
                    Timestamp = DateTime.UtcNow,
                    Uptime = DateTime.UtcNow - _startTime,
                    Environment = _env.EnvironmentName,
                    Status = healthReport.Status.ToString()
                };

                // Add component status
                foreach (var entry in healthReport.Entries)
                {
                    response.Components[entry.Key] = new ComponentStatus
                    {
                        Status = entry.Value.Status.ToString(),
                        Message = entry.Value.Description,
                        Details = entry.Value.Data?.Count > 0 ? entry.Value.Data.ToDictionary(k => k.Key, v => v.Value) : null
                    };
                }

                _logger.LogInformation("Status check completed. Overall status: {Status}", response.Status);

                // Return 503 if any critical components are unhealthy
                if (healthReport.Status == HealthStatus.Unhealthy)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking API status");
                
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiStatusResponse
                {
                    Status = "Error",
                    Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                    Timestamp = DateTime.UtcNow,
                    Uptime = DateTime.UtcNow - _startTime,
                    Environment = _env.EnvironmentName,
                    Components = new Dictionary<string, ComponentStatus>
                    {
                        ["error"] = new ComponentStatus
                        {
                            Status = "Error",
                            Message = "Failed to retrieve status information",
                            Details = _env.IsDevelopment() ? new Dictionary<string, object>
                            {
                                { "error", ex.Message }
                            } : null
                        }
                    }
                });
            }
        }

        private string? GetBuildTime()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var buildAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return buildAttribute?.InformationalVersion;
            }
            catch
            {
                return null;
            }
        }
    }
}
