using BiatecTokensApi.Configuration;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.HealthChecks
{
    /// <summary>
    /// Health check for key management system connectivity and configuration
    /// </summary>
    public class KeyManagementHealthCheck : IHealthCheck
    {
        private readonly KeyProviderFactory _keyProviderFactory;
        private readonly KeyManagementConfig _config;
        private readonly ILogger<KeyManagementHealthCheck> _logger;
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyManagementHealthCheck"/> class.
        /// </summary>
        /// <param name="keyProviderFactory">Key provider factory</param>
        /// <param name="config">Key management configuration</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="environment">Web host environment</param>
        public KeyManagementHealthCheck(
            KeyProviderFactory keyProviderFactory,
            IOptions<KeyManagementConfig> config,
            ILogger<KeyManagementHealthCheck> logger,
            IWebHostEnvironment environment)
        {
            _keyProviderFactory = keyProviderFactory;
            _config = config.Value;
            _logger = logger;
            _environment = environment;
        }

        /// <summary>
        /// Checks the health of the key management system
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
                var provider = _keyProviderFactory.CreateProvider();
                var providerType = provider.ProviderType;

                // In production, fail if using insecure providers
                if (!_environment.IsDevelopment())
                {
                    if (providerType == "EnvironmentVariable" || providerType == "Hardcoded")
                    {
                        _logger.LogError("Insecure key provider '{ProviderType}' is not allowed in production environment", providerType);
                        return HealthCheckResult.Unhealthy(
                            $"Insecure key provider '{providerType}' detected in production. Use AzureKeyVault or AwsKms.",
                            null,
                            new Dictionary<string, object>
                            {
                                { "providerType", providerType },
                                { "environment", _environment.EnvironmentName },
                                { "errorCode", "KMS_INSECURE_PROVIDER_IN_PRODUCTION" }
                            });
                    }
                }

                // Validate provider configuration
                var isValid = await provider.ValidateConfigurationAsync();
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                if (!isValid)
                {
                    _logger.LogError("Key provider configuration validation failed: ProviderType={ProviderType}", providerType);
                    return HealthCheckResult.Unhealthy(
                        $"Key provider '{providerType}' configuration is invalid",
                        null,
                        new Dictionary<string, object>
                        {
                            { "providerType", providerType },
                            { "environment", _environment.EnvironmentName },
                            { "responseTimeMs", Math.Round(responseTime, 2) },
                            { "errorCode", "KMS_INVALID_CONFIGURATION" }
                        });
                }

                // For production KMS providers, test connectivity by attempting to retrieve the key
                if (!_environment.IsDevelopment() && (providerType == "AzureKeyVault" || providerType == "AwsKms"))
                {
                    try
                    {
                        var key = await provider.GetEncryptionKeyAsync();
                        if (string.IsNullOrEmpty(key) || key.Length < 32)
                        {
                            _logger.LogError("Key provider returned invalid key: ProviderType={ProviderType}", providerType);
                            return HealthCheckResult.Unhealthy(
                                $"Key provider '{providerType}' returned invalid encryption key",
                                null,
                                new Dictionary<string, object>
                                {
                                    { "providerType", providerType },
                                    { "environment", _environment.EnvironmentName },
                                    { "responseTimeMs", Math.Round(responseTime, 2) },
                                    { "errorCode", "KMS_INVALID_KEY" }
                                });
                        }

                        responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                        _logger.LogInformation("Key provider health check passed: ProviderType={ProviderType}, ResponseTimeMs={ResponseTime}", 
                            providerType, Math.Round(responseTime, 2));

                        return HealthCheckResult.Healthy(
                            $"Key provider '{providerType}' is healthy and connectivity verified",
                            new Dictionary<string, object>
                            {
                                { "providerType", providerType },
                                { "environment", _environment.EnvironmentName },
                                { "responseTimeMs", Math.Round(responseTime, 2) }
                            });
                    }
                    catch (Exception ex)
                    {
                        responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                        _logger.LogError(ex, "Key provider connectivity test failed: ProviderType={ProviderType}, ResponseTimeMs={ResponseTime}", 
                            providerType, Math.Round(responseTime, 2));
                        return HealthCheckResult.Unhealthy(
                            $"Key provider '{providerType}' connectivity test failed: {ex.Message}",
                            ex,
                            new Dictionary<string, object>
                            {
                                { "providerType", providerType },
                                { "environment", _environment.EnvironmentName },
                                { "responseTimeMs", Math.Round(responseTime, 2) },
                                { "error", ex.Message },
                                { "errorCode", "KMS_CONNECTIVITY_FAILED" }
                            });
                    }
                }

                // In development, just validate configuration
                responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation("Key provider configuration validated: ProviderType={ProviderType}, ResponseTimeMs={ResponseTime}", 
                    providerType, Math.Round(responseTime, 2));

                return HealthCheckResult.Healthy(
                    $"Key provider '{providerType}' is configured correctly",
                    new Dictionary<string, object>
                    {
                        { "providerType", providerType },
                        { "environment", _environment.EnvironmentName },
                        { "responseTimeMs", Math.Round(responseTime, 2) }
                    });
            }
            catch (Exception ex)
            {
                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, "Key management health check failed after {ResponseTime}ms", Math.Round(responseTime, 2));
                return HealthCheckResult.Unhealthy(
                    "Key management system health check failed",
                    ex,
                    new Dictionary<string, object>
                    {
                        { "environment", _environment.EnvironmentName },
                        { "error", ex.Message },
                        { "responseTimeMs", Math.Round(responseTime, 2) },
                        { "errorCode", "KMS_HEALTH_CHECK_FAILED" }
                    });
            }
        }
    }
}
