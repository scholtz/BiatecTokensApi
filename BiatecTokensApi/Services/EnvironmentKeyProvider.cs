using BiatecTokensApi.Configuration;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Environment variable-based key provider for encryption keys
    /// </summary>
    public class EnvironmentKeyProvider : IKeyProvider
    {
        private readonly KeyManagementConfig _config;
        private readonly ILogger<EnvironmentKeyProvider> _logger;

        public string ProviderType => "EnvironmentVariable";

        public EnvironmentKeyProvider(
            IOptions<KeyManagementConfig> config,
            ILogger<EnvironmentKeyProvider> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public Task<string> GetEncryptionKeyAsync()
        {
            var key = Environment.GetEnvironmentVariable(_config.EnvironmentVariableName);
            
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogError("Encryption key not found in environment variable: {VariableName}", 
                    _config.EnvironmentVariableName);
                throw new InvalidOperationException(
                    $"Encryption key not found in environment variable '{_config.EnvironmentVariableName}'. " +
                    "Please set this environment variable or configure a different key provider.");
            }

            if (key.Length < 32)
            {
                _logger.LogError("Encryption key from environment variable is too short (minimum 32 characters required)");
                throw new InvalidOperationException(
                    "Encryption key must be at least 32 characters long for AES-256 encryption.");
            }

            _logger.LogDebug("Successfully retrieved encryption key from environment variable");
            return Task.FromResult(key);
        }

        public Task<bool> ValidateConfigurationAsync()
        {
            try
            {
                var key = Environment.GetEnvironmentVariable(_config.EnvironmentVariableName);
                bool isValid = !string.IsNullOrEmpty(key) && key.Length >= 32;
                
                if (!isValid)
                {
                    _logger.LogWarning("Environment variable key provider validation failed: Variable={VariableName}, KeyLength={Length}",
                        _config.EnvironmentVariableName, key?.Length ?? 0);
                }
                
                return Task.FromResult(isValid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating environment variable key provider");
                return Task.FromResult(false);
            }
        }
    }
}
