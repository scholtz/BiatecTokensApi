using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using BiatecTokensApi.Configuration;
using BiatecTokensApi.Services.Interface;
using BiatecTokensApi.Helpers;
using Microsoft.Extensions.Options;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// AWS Secrets Manager provider for production encryption key management
    /// Uses AWSSDK.SecretsManager for secure key retrieval
    /// </summary>
    public class AwsKmsProvider : IKeyProvider
    {
        private readonly KeyManagementConfig _config;
        private readonly ILogger<AwsKmsProvider> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public string ProviderType => "AwsKms";

        public AwsKmsProvider(
            IOptions<KeyManagementConfig> config,
            ILogger<AwsKmsProvider> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _config = config.Value;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string> GetEncryptionKeyAsync()
        {
            if (_config.AwsKms == null)
            {
                throw new InvalidOperationException("AWS KMS configuration is missing");
            }

            var correlationId = _httpContextAccessor?.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            try
            {
                _logger.LogInformation("Retrieving encryption key from AWS Secrets Manager: Region={Region}, SecretId={SecretId}, CorrelationId={CorrelationId}", 
                    LoggingHelper.SanitizeLogInput(_config.AwsKms.Region),
                    LoggingHelper.SanitizeLogInput(_config.AwsKms.KeyId),
                    correlationId);

                var config = new AmazonSecretsManagerConfig 
                { 
                    RegionEndpoint = RegionEndpoint.GetBySystemName(_config.AwsKms.Region) 
                };

                AmazonSecretsManagerClient client;
                if (_config.AwsKms.UseIamRole)
                {
                    client = new AmazonSecretsManagerClient(config);
                }
                else
                {
                    client = new AmazonSecretsManagerClient(
                        _config.AwsKms.AccessKeyId,
                        _config.AwsKms.SecretAccessKey,
                        config);
                }

                var request = new GetSecretValueRequest 
                { 
                    SecretId = _config.AwsKms.KeyId 
                };

                var response = await client.GetSecretValueAsync(request);

                if (string.IsNullOrEmpty(response.SecretString))
                {
                    _logger.LogError("AWS Secrets Manager returned empty secret value: CorrelationId={CorrelationId}", correlationId);
                    throw new InvalidOperationException("AWS Secrets Manager returned an empty secret value");
                }

                if (response.SecretString.Length < 32)
                {
                    _logger.LogError("AWS Secrets Manager secret is too short (minimum 32 characters required): CorrelationId={CorrelationId}", correlationId);
                    throw new InvalidOperationException("Encryption key must be at least 32 characters long for AES-256 encryption");
                }

                _logger.LogInformation("Successfully retrieved encryption key from AWS Secrets Manager: CorrelationId={CorrelationId}", correlationId);
                return response.SecretString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve encryption key from AWS Secrets Manager: Region={Region}, SecretId={SecretId}, CorrelationId={CorrelationId}, ErrorCode=KMS_AWS_RETRIEVAL_FAILED", 
                    LoggingHelper.SanitizeLogInput(_config.AwsKms.Region),
                    LoggingHelper.SanitizeLogInput(_config.AwsKms.KeyId),
                    correlationId);
                throw new InvalidOperationException(
                    $"Failed to retrieve encryption key from AWS Secrets Manager. ErrorCode: KMS_AWS_RETRIEVAL_FAILED. CorrelationId: {correlationId}. " +
                    "Verify: (1) Region is correct, (2) SecretId exists, (3) IAM permissions include secretsmanager:GetSecretValue, (4) Network connectivity to AWS.", ex);
            }
        }

        public async Task<bool> ValidateConfigurationAsync()
        {
            try
            {
                if (_config.AwsKms == null)
                {
                    _logger.LogError("AWS KMS configuration is missing");
                    return false;
                }

                if (string.IsNullOrEmpty(_config.AwsKms.Region))
                {
                    _logger.LogError("AWS KMS region is not configured");
                    return false;
                }

                if (string.IsNullOrEmpty(_config.AwsKms.KeyId))
                {
                    _logger.LogError("AWS KMS key ID is not configured");
                    return false;
                }

                if (!_config.AwsKms.UseIamRole)
                {
                    if (string.IsNullOrEmpty(_config.AwsKms.AccessKeyId) ||
                        string.IsNullOrEmpty(_config.AwsKms.SecretAccessKey))
                    {
                        _logger.LogError("AWS KMS access credentials are incomplete");
                        return false;
                    }
                }

                _logger.LogInformation("AWS KMS configuration is valid");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating AWS KMS configuration");
                return false;
            }
        }
    }
}
