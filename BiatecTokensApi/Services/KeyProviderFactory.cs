using BiatecTokensApi.Configuration;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Factory for creating key provider instances based on configuration
    /// </summary>
    public class KeyProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly KeyManagementConfig _config;
        private readonly ILogger<KeyProviderFactory> _logger;

        public KeyProviderFactory(
            IServiceProvider serviceProvider,
            IOptions<KeyManagementConfig> config,
            ILogger<KeyProviderFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _config = config.Value;
            _logger = logger;
        }

        /// <summary>
        /// Creates a key provider instance based on the configured provider type
        /// </summary>
        /// <returns>Key provider instance</returns>
        /// <exception cref="InvalidOperationException">Thrown when provider type is invalid</exception>
        public IKeyProvider CreateProvider()
        {
            _logger.LogInformation("Creating key provider: Type={ProviderType}", _config.Provider);

            IKeyProvider provider = _config.Provider?.ToLowerInvariant() switch
            {
                "azurekeyvault" => _serviceProvider.GetRequiredService<AzureKeyVaultProvider>(),
                "awskms" => _serviceProvider.GetRequiredService<AwsKmsProvider>(),
                "environmentvariable" => _serviceProvider.GetRequiredService<EnvironmentKeyProvider>(),
                "hardcoded" => _serviceProvider.GetRequiredService<HardcodedKeyProvider>(),
                _ => throw new InvalidOperationException(
                    $"Invalid key provider type '{_config.Provider}'. " +
                    "Supported types: AzureKeyVault, AwsKms, EnvironmentVariable, Hardcoded")
            };

            _logger.LogInformation("Key provider created successfully: Type={ProviderType}", provider.ProviderType);
            return provider;
        }
    }
}
