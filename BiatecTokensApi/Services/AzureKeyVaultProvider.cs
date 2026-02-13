using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Services.Interface;
using BiatecTokensApi.Helpers;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Azure Key Vault key provider for production encryption key management
    /// Uses Azure.Security.KeyVault.Secrets SDK for secure key retrieval
    /// </summary>
    public class AzureKeyVaultProvider : IKeyProvider
    {
        private readonly KeyManagementConfig _config;
        private readonly ILogger<AzureKeyVaultProvider> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Lazy<SecretClient> _lazyClient;

        public string ProviderType => "AzureKeyVault";

        public AzureKeyVaultProvider(
            IOptions<KeyManagementConfig> config,
            ILogger<AzureKeyVaultProvider> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _config = config.Value;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            
            // Lazy initialization of client for connection pooling and token caching
            _lazyClient = new Lazy<SecretClient>(() => CreateClient());
        }

        private SecretClient CreateClient()
        {
            if (_config.AzureKeyVault == null)
            {
                throw new InvalidOperationException("Azure Key Vault configuration is missing");
            }

            TokenCredential credential;
            if (_config.AzureKeyVault.UseManagedIdentity)
            {
                credential = new DefaultAzureCredential();
            }
            else
            {
                credential = new ClientSecretCredential(
                    _config.AzureKeyVault.TenantId, 
                    _config.AzureKeyVault.ClientId, 
                    _config.AzureKeyVault.ClientSecret);
            }

            return new SecretClient(new Uri(_config.AzureKeyVault.VaultUrl), credential);
        }

        public async Task<string> GetEncryptionKeyAsync()
        {
            if (_config.AzureKeyVault == null)
            {
                throw new InvalidOperationException("Azure Key Vault configuration is missing");
            }

            var correlationId = _httpContextAccessor?.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation("Retrieving encryption key from Azure Key Vault: VaultUrl={VaultUrl}, SecretName={SecretName}, CorrelationId={CorrelationId}", 
                    LoggingHelper.SanitizeLogInput(_config.AzureKeyVault.VaultUrl),
                    LoggingHelper.SanitizeLogInput(_config.AzureKeyVault.SecretName),
                    correlationId);

                var secret = await _lazyClient.Value.GetSecretAsync(_config.AzureKeyVault.SecretName);

                if (string.IsNullOrEmpty(secret.Value.Value))
                {
                    _logger.LogError("Azure Key Vault returned empty secret value: CorrelationId={CorrelationId}", correlationId);
                    throw new InvalidOperationException("Azure Key Vault returned an empty secret value");
                }

                if (secret.Value.Value.Length < 32)
                {
                    _logger.LogError("Azure Key Vault secret is too short (minimum 32 characters required): CorrelationId={CorrelationId}", correlationId);
                    throw new InvalidOperationException("Encryption key must be at least 32 characters long for AES-256 encryption");
                }

                _logger.LogInformation("Successfully retrieved encryption key from Azure Key Vault: CorrelationId={CorrelationId}", correlationId);
                return secret.Value.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve encryption key from Azure Key Vault: VaultUrl={VaultUrl}, SecretName={SecretName}, CorrelationId={CorrelationId}, ErrorCode=KMS_AZURE_RETRIEVAL_FAILED", 
                    LoggingHelper.SanitizeLogInput(_config.AzureKeyVault.VaultUrl),
                    LoggingHelper.SanitizeLogInput(_config.AzureKeyVault.SecretName),
                    correlationId);
                throw new InvalidOperationException(
                    $"Failed to retrieve encryption key from Azure Key Vault. ErrorCode: KMS_AZURE_RETRIEVAL_FAILED. CorrelationId: {correlationId}. " +
                    "Verify: (1) VaultUrl is correct, (2) SecretName exists, (3) Application has 'Get' permission, (4) Network connectivity to Azure.", ex);
            }
        }

        public async Task<bool> ValidateConfigurationAsync()
        {
            try
            {
                if (_config.AzureKeyVault == null)
                {
                    _logger.LogError("Azure Key Vault configuration is missing");
                    return false;
                }

                if (string.IsNullOrEmpty(_config.AzureKeyVault.VaultUrl))
                {
                    _logger.LogError("Azure Key Vault URL is not configured");
                    return false;
                }

                if (string.IsNullOrEmpty(_config.AzureKeyVault.SecretName))
                {
                    _logger.LogError("Azure Key Vault secret name is not configured");
                    return false;
                }

                if (!_config.AzureKeyVault.UseManagedIdentity)
                {
                    if (string.IsNullOrEmpty(_config.AzureKeyVault.TenantId) ||
                        string.IsNullOrEmpty(_config.AzureKeyVault.ClientId) ||
                        string.IsNullOrEmpty(_config.AzureKeyVault.ClientSecret))
                    {
                        _logger.LogError("Azure Key Vault client credentials are incomplete");
                        return false;
                    }
                }

                _logger.LogInformation("Azure Key Vault configuration is valid");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Azure Key Vault configuration");
                return false;
            }
        }
    }
}
