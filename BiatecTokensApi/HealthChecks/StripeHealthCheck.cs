using BiatecTokensApi.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Stripe;

namespace BiatecTokensApi.HealthChecks
{
    /// <summary>
    /// Health check for Stripe API availability and connectivity
    /// </summary>
    public class StripeHealthCheck : IHealthCheck
    {
        private readonly StripeConfig _config;
        private readonly ILogger<StripeHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StripeHealthCheck"/> class.
        /// </summary>
        /// <param name="config">Stripe configuration</param>
        /// <param name="logger">Logger instance</param>
        public StripeHealthCheck(
            IOptions<StripeConfig> config,
            ILogger<StripeHealthCheck> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        /// <summary>
        /// Checks the health of the Stripe API connection
        /// </summary>
        /// <param name="context">Health check context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Health check result</returns>
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                // Check if Stripe is configured
                if (string.IsNullOrWhiteSpace(_config.SecretKey))
                {
                    _logger.LogWarning("Stripe API key is not configured");
                    return HealthCheckResult.Degraded("Stripe API key is not configured", null, new Dictionary<string, object>
                    {
                        { "configured", false },
                        { "message", "Stripe integration is not configured" }
                    });
                }

                // If using a test/placeholder key, mark as degraded rather than failing
                if (_config.SecretKey.Contains("test_key") || _config.SecretKey.Contains("placeholder"))
                {
                    _logger.LogInformation("Stripe using test/placeholder key, marking as degraded");
                    return HealthCheckResult.Degraded("Stripe using test/placeholder API key", null, new Dictionary<string, object>
                    {
                        { "configured", true },
                        { "mode", "test-placeholder" },
                        { "message", "Using test or placeholder API key" }
                    });
                }

                // Set the API key for this health check
                StripeConfiguration.ApiKey = _config.SecretKey;

                // Try to make a simple API call to verify connectivity
                // Using Balance.Get as it's a lightweight operation that verifies authentication
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var balanceService = new BalanceService();
                var balance = await balanceService.GetAsync(cancellationToken: linkedCts.Token);

                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                return HealthCheckResult.Healthy("Stripe API is reachable and authenticated", new Dictionary<string, object>
                {
                    { "configured", true },
                    { "authenticated", true },
                    { "mode", balance.Livemode ? "live" : "test" },
                    { "responseTimeMs", Math.Round(responseTime, 2) }
                });
            }
            catch (OperationCanceledException)
            {
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogWarning("Stripe health check timed out after {ResponseTime}ms", responseTime);
                return HealthCheckResult.Degraded("Stripe API health check timed out", null, new Dictionary<string, object>
                {
                    { "configured", !string.IsNullOrWhiteSpace(_config.SecretKey) },
                    { "responseTimeMs", Math.Round(responseTime, 2) }
                });
            }
            catch (StripeException ex)
            {
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, "Stripe API error during health check: {Message}", ex.Message);
                
                // Authentication errors indicate misconfiguration
                if (ex.StripeError?.Type == "authentication_error" || ex.StripeError?.Type == "invalid_request_error")
                {
                    return HealthCheckResult.Unhealthy("Stripe API authentication failed", ex, new Dictionary<string, object>
                    {
                        { "configured", true },
                        { "authenticated", false },
                        { "error", ex.StripeError?.Message ?? ex.Message },
                        { "errorType", ex.StripeError?.Type ?? "unknown" },
                        { "responseTimeMs", Math.Round(responseTime, 2) }
                    });
                }

                // Other Stripe errors may be temporary
                return HealthCheckResult.Degraded($"Stripe API returned error: {ex.StripeError?.Message ?? ex.Message}", ex, new Dictionary<string, object>
                {
                    { "configured", true },
                    { "error", ex.StripeError?.Message ?? ex.Message },
                    { "errorType", ex.StripeError?.Type ?? "unknown" },
                    { "responseTimeMs", Math.Round(responseTime, 2) }
                });
            }
            catch (Exception ex)
            {
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, "Stripe health check failed after {ResponseTime}ms", responseTime);
                return HealthCheckResult.Unhealthy("Stripe API is not reachable", ex, new Dictionary<string, object>
                {
                    { "configured", !string.IsNullOrWhiteSpace(_config.SecretKey) },
                    { "error", ex.Message },
                    { "responseTimeMs", Math.Round(responseTime, 2) }
                });
            }
        }
    }
}
