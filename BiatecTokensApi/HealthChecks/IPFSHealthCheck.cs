using BiatecTokensApi.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.HealthChecks
{
    /// <summary>
    /// Health check for IPFS API availability
    /// </summary>
    public class IPFSHealthCheck : IHealthCheck
    {
        private readonly IPFSConfig _config;
        private readonly HttpClient _httpClient;
        private readonly ILogger<IPFSHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="IPFSHealthCheck"/> class.
        /// </summary>
        /// <param name="config">IPFS configuration</param>
        /// <param name="httpClient">HTTP client for health checks</param>
        /// <param name="logger">Logger instance</param>
        public IPFSHealthCheck(
            IOptions<IPFSConfig> config,
            HttpClient httpClient,
            ILogger<IPFSHealthCheck> logger)
        {
            _config = config.Value;
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Checks the health of the IPFS API
        /// </summary>
        /// <param name="context">Health check context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Try to reach the IPFS API endpoint with a short timeout
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var response = await _httpClient.GetAsync(_config.ApiUrl, linkedCts.Token);

                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 200 or 404 both indicate the service is reachable
                    return HealthCheckResult.Healthy("IPFS API is reachable", new Dictionary<string, object>
                    {
                        { "apiUrl", _config.ApiUrl },
                        { "statusCode", (int)response.StatusCode }
                    });
                }

                _logger.LogWarning("IPFS API returned unexpected status code: {StatusCode}", response.StatusCode);
                return HealthCheckResult.Degraded($"IPFS API returned status code {response.StatusCode}", null, new Dictionary<string, object>
                {
                    { "apiUrl", _config.ApiUrl },
                    { "statusCode", (int)response.StatusCode }
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("IPFS health check timed out");
                return HealthCheckResult.Degraded("IPFS API health check timed out", null, new Dictionary<string, object>
                {
                    { "apiUrl", _config.ApiUrl }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IPFS health check failed");
                return HealthCheckResult.Unhealthy("IPFS API is not reachable", ex, new Dictionary<string, object>
                {
                    { "apiUrl", _config.ApiUrl },
                    { "error", ex.Message }
                });
            }
        }
    }
}