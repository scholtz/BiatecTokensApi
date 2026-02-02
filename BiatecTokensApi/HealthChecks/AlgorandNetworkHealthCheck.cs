using AlgorandAuthenticationV2;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.HealthChecks
{
    /// <summary>
    /// Health check for Algorand network connectivity
    /// </summary>
    public class AlgorandNetworkHealthCheck : IHealthCheck
    {
        private readonly AlgorandAuthenticationOptionsV2 _authOptions;
        private readonly ILogger<AlgorandNetworkHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AlgorandNetworkHealthCheck"/> class.
        /// </summary>
        /// <param name="authOptions">Algorand authentication options</param>
        /// <param name="logger">Logger instance</param>
        public AlgorandNetworkHealthCheck(
            IOptions<AlgorandAuthenticationOptionsV2> authOptions,
            ILogger<AlgorandNetworkHealthCheck> logger)
        {
            _authOptions = authOptions.Value;
            _logger = logger;
        }

        /// <summary>
        /// Checks the health of configured Algorand networks
        /// </summary>
        /// <param name="context">Health check context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            if (_authOptions?.AllowedNetworks == null || !_authOptions.AllowedNetworks.Any())
            {
                return HealthCheckResult.Degraded("No Algorand networks configured");
            }

            var healthyNetworks = new List<string>();
            var unhealthyNetworks = new List<string>();
            var networkDetails = new Dictionary<string, object>();

            foreach (var network in _authOptions.AllowedNetworks)
            {
                var networkStartTime = DateTime.UtcNow;
                try
                {
                    if (string.IsNullOrEmpty(network.Value?.Server))
                    {
                        unhealthyNetworks.Add($"{network.Key.Substring(0, 8)}... (no server configured)");
                        continue;
                    }

                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    
                    // Add authentication header if token is provided
                    if (!string.IsNullOrEmpty(network.Value.Token) && !string.IsNullOrEmpty(network.Value.Header))
                    {
                        httpClient.DefaultRequestHeaders.Add(network.Value.Header, network.Value.Token);
                    }

                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    // Try to get node status
                    var response = await httpClient.GetAsync($"{network.Value.Server.TrimEnd('/')}/v2/status", linkedCts.Token);
                    var responseTime = (DateTime.UtcNow - networkStartTime).TotalMilliseconds;

                    if (response.IsSuccessStatusCode)
                    {
                        healthyNetworks.Add(network.Key.Substring(0, 8) + "...");
                        networkDetails[$"network_{network.Key.Substring(0, 8)}"] = new
                        {
                            status = "healthy",
                            server = network.Value.Server,
                            responseTimeMs = Math.Round(responseTime, 2)
                        };
                    }
                    else
                    {
                        unhealthyNetworks.Add($"{network.Key.Substring(0, 8)}... ({response.StatusCode})");
                        networkDetails[$"network_{network.Key.Substring(0, 8)}"] = new
                        {
                            status = "degraded",
                            server = network.Value.Server,
                            statusCode = (int)response.StatusCode,
                            responseTimeMs = Math.Round(responseTime, 2)
                        };
                    }
                }
                catch (OperationCanceledException)
                {
                    var responseTime = (DateTime.UtcNow - networkStartTime).TotalMilliseconds;
                    unhealthyNetworks.Add($"{network.Key.Substring(0, 8)}... (timeout)");
                    networkDetails[$"network_{network.Key.Substring(0, 8)}"] = new
                    {
                        status = "timeout",
                        server = network.Value?.Server ?? "not configured",
                        responseTimeMs = Math.Round(responseTime, 2)
                    };
                }
                catch (Exception ex)
                {
                    var responseTime = (DateTime.UtcNow - networkStartTime).TotalMilliseconds;
                    _logger.LogWarning(ex, "Health check failed for Algorand network {NetworkId} after {ResponseTime}ms", 
                        network.Key.Substring(0, 8), responseTime);
                    unhealthyNetworks.Add($"{network.Key.Substring(0, 8)}... (error)");
                    networkDetails[$"network_{network.Key.Substring(0, 8)}"] = new
                    {
                        status = "error",
                        server = network.Value?.Server ?? "not configured",
                        error = ex.Message,
                        responseTimeMs = Math.Round(responseTime, 2)
                    };
                }
            }

            var data = new Dictionary<string, object>
            {
                { "totalNetworks", _authOptions.AllowedNetworks.Count },
                { "healthyNetworks", healthyNetworks.Count },
                { "unhealthyNetworks", unhealthyNetworks.Count }
            };

            // Merge network details
            foreach (var detail in networkDetails)
            {
                data[detail.Key] = detail.Value;
            }

            if (unhealthyNetworks.Count == _authOptions.AllowedNetworks.Count)
            {
                return HealthCheckResult.Unhealthy(
                    $"All {unhealthyNetworks.Count} Algorand networks are unreachable",
                    null,
                    data);
            }

            if (unhealthyNetworks.Any())
            {
                return HealthCheckResult.Degraded(
                    $"{healthyNetworks.Count}/{_authOptions.AllowedNetworks.Count} Algorand networks are healthy",
                    null,
                    data);
            }

            return HealthCheckResult.Healthy(
                $"All {healthyNetworks.Count} Algorand networks are healthy",
                data);
        }
    }
}
