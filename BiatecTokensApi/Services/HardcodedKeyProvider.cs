using BiatecTokensApi.Configuration;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Hardcoded key provider for development and testing only
    /// WARNING: NEVER use this in production!
    /// </summary>
    public class HardcodedKeyProvider : IKeyProvider
    {
        private readonly KeyManagementConfig _config;
        private readonly ILogger<HardcodedKeyProvider> _logger;

        public string ProviderType => "Hardcoded";

        public HardcodedKeyProvider(
            IOptions<KeyManagementConfig> config,
            ILogger<HardcodedKeyProvider> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public Task<string> GetEncryptionKeyAsync()
        {
            _logger.LogWarning("Using hardcoded encryption key - THIS IS NOT SECURE FOR PRODUCTION!");
            
            var key = _config.HardcodedKey ?? "SYSTEM_KEY_FOR_MVP_REPLACE_IN_PRODUCTION";
            
            if (key.Length < 32)
            {
                _logger.LogError("Hardcoded encryption key is too short (minimum 32 characters required)");
                throw new InvalidOperationException(
                    "Encryption key must be at least 32 characters long for AES-256 encryption.");
            }

            return Task.FromResult(key);
        }

        public Task<bool> ValidateConfigurationAsync()
        {
            try
            {
                var key = _config.HardcodedKey ?? "SYSTEM_KEY_FOR_MVP_REPLACE_IN_PRODUCTION";
                bool isValid = key.Length >= 32;
                
                if (!isValid)
                {
                    _logger.LogWarning("Hardcoded key provider validation failed: KeyLength={Length}", key.Length);
                }
                else
                {
                    _logger.LogWarning("Hardcoded key provider is configured - NOT RECOMMENDED FOR PRODUCTION");
                }
                
                return Task.FromResult(isValid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating hardcoded key provider");
                return Task.FromResult(false);
            }
        }
    }
}
