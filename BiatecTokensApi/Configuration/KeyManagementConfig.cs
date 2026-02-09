namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// Configuration for key management system used for mnemonic encryption
    /// </summary>
    public class KeyManagementConfig
    {
        /// <summary>
        /// Key provider type: "AzureKeyVault", "AwsKms", "EnvironmentVariable", or "Hardcoded" (dev only)
        /// </summary>
        public string Provider { get; set; } = "EnvironmentVariable";

        /// <summary>
        /// Azure Key Vault configuration
        /// </summary>
        public AzureKeyVaultConfig? AzureKeyVault { get; set; }

        /// <summary>
        /// AWS KMS configuration
        /// </summary>
        public AwsKmsConfig? AwsKms { get; set; }

        /// <summary>
        /// Environment variable name for encryption key (fallback option)
        /// </summary>
        public string EnvironmentVariableName { get; set; } = "BIATEC_ENCRYPTION_KEY";

        /// <summary>
        /// For development only - hardcoded key (NEVER use in production)
        /// </summary>
        public string? HardcodedKey { get; set; }
    }

    /// <summary>
    /// Azure Key Vault configuration
    /// </summary>
    public class AzureKeyVaultConfig
    {
        /// <summary>
        /// Key Vault URL (e.g., "https://myvault.vault.azure.net/")
        /// </summary>
        public string VaultUrl { get; set; } = string.Empty;

        /// <summary>
        /// Name of the secret containing the encryption key
        /// </summary>
        public string SecretName { get; set; } = "biatec-encryption-key";

        /// <summary>
        /// Use managed identity for authentication (recommended for production)
        /// </summary>
        public bool UseManagedIdentity { get; set; } = true;

        /// <summary>
        /// Tenant ID (required if not using managed identity)
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Client ID (required if not using managed identity)
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Client secret (required if not using managed identity)
        /// </summary>
        public string? ClientSecret { get; set; }
    }

    /// <summary>
    /// AWS KMS configuration
    /// </summary>
    public class AwsKmsConfig
    {
        /// <summary>
        /// AWS region (e.g., "us-east-1")
        /// </summary>
        public string Region { get; set; } = "us-east-1";

        /// <summary>
        /// KMS key ID or ARN
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// Use IAM role for authentication (recommended for production)
        /// </summary>
        public bool UseIamRole { get; set; } = true;

        /// <summary>
        /// AWS access key ID (required if not using IAM role)
        /// </summary>
        public string? AccessKeyId { get; set; }

        /// <summary>
        /// AWS secret access key (required if not using IAM role)
        /// </summary>
        public string? SecretAccessKey { get; set; }
    }
}
