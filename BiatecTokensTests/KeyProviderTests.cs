using BiatecTokensApi.Configuration;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for key provider implementations
    /// </summary>
    [TestFixture]
    public class KeyProviderTests
    {
        private ILogger<EnvironmentKeyProvider> _envLogger = null!;
        private ILogger<HardcodedKeyProvider> _hardcodedLogger = null!;
        private ILogger<AzureKeyVaultProvider> _azureLogger = null!;
        private ILogger<AwsKmsProvider> _awsLogger = null!;

        [SetUp]
        public void Setup()
        {
            _envLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<EnvironmentKeyProvider>();
            _hardcodedLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<HardcodedKeyProvider>();
            _azureLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<AzureKeyVaultProvider>();
            _awsLogger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<AwsKmsProvider>();
        }

        [Test]
        public async Task EnvironmentKeyProvider_ReturnsKey_WhenEnvironmentVariableIsSet()
        {
            // Arrange
            var testKey = "ThisIsATest32CharacterKeyForAES256Encryption";
            Environment.SetEnvironmentVariable("TEST_ENCRYPTION_KEY", testKey);

            var config = new KeyManagementConfig
            {
                Provider = "EnvironmentVariable",
                EnvironmentVariableName = "TEST_ENCRYPTION_KEY"
            };

            var provider = new EnvironmentKeyProvider(
                Options.Create(config),
                _envLogger);

            // Act
            var key = await provider.GetEncryptionKeyAsync();

            // Assert
            Assert.That(key, Is.EqualTo(testKey));

            // Cleanup
            Environment.SetEnvironmentVariable("TEST_ENCRYPTION_KEY", null);
        }

        [Test]
        public void EnvironmentKeyProvider_ThrowsException_WhenEnvironmentVariableNotSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("MISSING_KEY", null);

            var config = new KeyManagementConfig
            {
                Provider = "EnvironmentVariable",
                EnvironmentVariableName = "MISSING_KEY"
            };

            var provider = new EnvironmentKeyProvider(
                Options.Create(config),
                _envLogger);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await provider.GetEncryptionKeyAsync());
        }

        [Test]
        public void EnvironmentKeyProvider_ThrowsException_WhenKeyTooShort()
        {
            // Arrange
            var shortKey = "ShortKey";
            Environment.SetEnvironmentVariable("SHORT_KEY", shortKey);

            var config = new KeyManagementConfig
            {
                Provider = "EnvironmentVariable",
                EnvironmentVariableName = "SHORT_KEY"
            };

            var provider = new EnvironmentKeyProvider(
                Options.Create(config),
                _envLogger);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await provider.GetEncryptionKeyAsync());

            // Cleanup
            Environment.SetEnvironmentVariable("SHORT_KEY", null);
        }

        [Test]
        public async Task EnvironmentKeyProvider_ValidateConfiguration_ReturnsFalse_WhenVariableNotSet()
        {
            // Arrange
            Environment.SetEnvironmentVariable("MISSING_VALIDATION_KEY", null);

            var config = new KeyManagementConfig
            {
                Provider = "EnvironmentVariable",
                EnvironmentVariableName = "MISSING_VALIDATION_KEY"
            };

            var provider = new EnvironmentKeyProvider(
                Options.Create(config),
                _envLogger);

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.False);
        }

        [Test]
        public async Task EnvironmentKeyProvider_ValidateConfiguration_ReturnsTrue_WhenVariableSet()
        {
            // Arrange
            var testKey = "ThisIsATest32CharacterKeyForAES256Encryption";
            Environment.SetEnvironmentVariable("VALID_KEY", testKey);

            var config = new KeyManagementConfig
            {
                Provider = "EnvironmentVariable",
                EnvironmentVariableName = "VALID_KEY"
            };

            var provider = new EnvironmentKeyProvider(
                Options.Create(config),
                _envLogger);

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.True);

            // Cleanup
            Environment.SetEnvironmentVariable("VALID_KEY", null);
        }

        [Test]
        public async Task HardcodedKeyProvider_ReturnsKey()
        {
            // Arrange
            var testKey = "ThisIsATest32CharacterKeyForAES256Encryption";
            var config = new KeyManagementConfig
            {
                Provider = "Hardcoded",
                HardcodedKey = testKey
            };

            var provider = new HardcodedKeyProvider(
                Options.Create(config),
                _hardcodedLogger);

            // Act
            var key = await provider.GetEncryptionKeyAsync();

            // Assert
            Assert.That(key, Is.EqualTo(testKey));
        }

        [Test]
        public async Task HardcodedKeyProvider_ReturnsDefaultKey_WhenNotConfigured()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "Hardcoded",
                HardcodedKey = null
            };

            var provider = new HardcodedKeyProvider(
                Options.Create(config),
                _hardcodedLogger);

            // Act
            var key = await provider.GetEncryptionKeyAsync();

            // Assert
            Assert.That(key, Is.EqualTo("SYSTEM_KEY_FOR_MVP_REPLACE_IN_PRODUCTION"));
        }

        [Test]
        public async Task HardcodedKeyProvider_ValidateConfiguration_ReturnsTrue_WithValidKey()
        {
            // Arrange
            var testKey = "ThisIsATest32CharacterKeyForAES256Encryption";
            var config = new KeyManagementConfig
            {
                Provider = "Hardcoded",
                HardcodedKey = testKey
            };

            var provider = new HardcodedKeyProvider(
                Options.Create(config),
                _hardcodedLogger);

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void HardcodedKeyProvider_ThrowsException_WhenKeyTooShort()
        {
            // Arrange
            var shortKey = "Short";
            var config = new KeyManagementConfig
            {
                Provider = "Hardcoded",
                HardcodedKey = shortKey
            };

            var provider = new HardcodedKeyProvider(
                Options.Create(config),
                _hardcodedLogger);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await provider.GetEncryptionKeyAsync());
        }

        [Test]
        public void AzureKeyVaultProvider_ThrowsNotImplemented()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "AzureKeyVault",
                AzureKeyVault = new AzureKeyVaultConfig
                {
                    VaultUrl = "https://test-vault.vault.azure.net/",
                    SecretName = "test-secret",
                    UseManagedIdentity = true
                }
            };

            var provider = new AzureKeyVaultProvider(
                Options.Create(config),
                _azureLogger);

            // Act & Assert
            Assert.ThrowsAsync<NotImplementedException>(async () => 
                await provider.GetEncryptionKeyAsync());
        }

        [Test]
        public async Task AzureKeyVaultProvider_ValidateConfiguration_ReturnsFalse_WhenConfigMissing()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "AzureKeyVault",
                AzureKeyVault = null
            };

            var provider = new AzureKeyVaultProvider(
                Options.Create(config),
                _azureLogger);

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.False);
        }

        [Test]
        public async Task AzureKeyVaultProvider_ValidateConfiguration_ReturnsTrue_WithManagedIdentity()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "AzureKeyVault",
                AzureKeyVault = new AzureKeyVaultConfig
                {
                    VaultUrl = "https://test-vault.vault.azure.net/",
                    SecretName = "test-secret",
                    UseManagedIdentity = true
                }
            };

            var provider = new AzureKeyVaultProvider(
                Options.Create(config),
                _azureLogger);

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public void AwsKmsProvider_ThrowsNotImplemented()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "AwsKms",
                AwsKms = new AwsKmsConfig
                {
                    Region = "us-east-1",
                    KeyId = "test-key-id",
                    UseIamRole = true
                }
            };

            var provider = new AwsKmsProvider(
                Options.Create(config),
                _awsLogger);

            // Act & Assert
            Assert.ThrowsAsync<NotImplementedException>(async () => 
                await provider.GetEncryptionKeyAsync());
        }

        [Test]
        public async Task AwsKmsProvider_ValidateConfiguration_ReturnsFalse_WhenConfigMissing()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "AwsKms",
                AwsKms = null
            };

            var provider = new AwsKmsProvider(
                Options.Create(config),
                _awsLogger);

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.False);
        }

        [Test]
        public async Task AwsKmsProvider_ValidateConfiguration_ReturnsTrue_WithIamRole()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "AwsKms",
                AwsKms = new AwsKmsConfig
                {
                    Region = "us-east-1",
                    KeyId = "test-key-id",
                    UseIamRole = true
                }
            };

            var provider = new AwsKmsProvider(
                Options.Create(config),
                _awsLogger);

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.True);
        }

        [Test]
        public async Task ProviderType_ReturnsCorrectValue_ForEachProvider()
        {
            // Arrange & Act
            var envProvider = new EnvironmentKeyProvider(
                Options.Create(new KeyManagementConfig()), _envLogger);
            var hardcodedProvider = new HardcodedKeyProvider(
                Options.Create(new KeyManagementConfig()), _hardcodedLogger);
            var azureProvider = new AzureKeyVaultProvider(
                Options.Create(new KeyManagementConfig()), _azureLogger);
            var awsProvider = new AwsKmsProvider(
                Options.Create(new KeyManagementConfig()), _awsLogger);

            // Assert
            Assert.That(envProvider.ProviderType, Is.EqualTo("EnvironmentVariable"));
            Assert.That(hardcodedProvider.ProviderType, Is.EqualTo("Hardcoded"));
            Assert.That(azureProvider.ProviderType, Is.EqualTo("AzureKeyVault"));
            Assert.That(awsProvider.ProviderType, Is.EqualTo("AwsKms"));
        }
    }
}
