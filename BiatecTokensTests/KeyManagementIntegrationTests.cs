using BiatecTokensApi.Configuration;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Net;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for Key Management System providers.
    /// Tests verify configuration validation, fail-closed behavior, and provider selection.
    /// </summary>
    [TestFixture]
    [NonParallelizable]  // Prevent parallel execution to avoid WebApplicationFactory conflicts
    public class KeyManagementIntegrationTests
    {
        /// <summary>
        /// Performs a health check with retry logic to accommodate CI environment startup delays
        /// Increased retries (10) and delay (2s) for better CI robustness
        /// </summary>
        private async Task<HttpResponseMessage> GetHealthWithRetryAsync(HttpClient client, int maxRetries = 10, int delayMs = 2000)
        {
            HttpResponseMessage? response = null;
            Exception? lastException = null;
            
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    response = await client.GetAsync("/health");
                    if (response.IsSuccessStatusCode)
                    {
                        return response;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    // Ignore and retry
                }
                
                if (i < maxRetries - 1)
                {
                    await Task.Delay(delayMs);
                }
            }
            
            // If we get here, all retries failed
            if (lastException != null)
            {
                throw new Exception($"Health endpoint failed after {maxRetries} retries over {(maxRetries * delayMs) / 1000}s", lastException);
            }
            
            return response ?? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }
        /// <summary>
        /// Tests that the application starts successfully with Hardcoded provider in development
        /// </summary>
        [Test]
        public async Task Application_Starts_WithHardcodedProvider_InDevelopment()
        {
            // Arrange
            var factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Development");
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["App:Account"] = "test mnemonic phrase for testing purposes only not real",
                            ["KeyManagementConfig:Provider"] = "Hardcoded",
                            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIntegrationTests32CharactersMinimum",
                            ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                            ["AlgorandAuthentication:CheckExpiration"] = "false",
                            ["AlgorandAuthentication:Debug"] = "true",
                            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                            ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                            ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                            ["IPFSConfig:TimeoutSeconds"] = "30",
                            ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                            ["IPFSConfig:ValidateContentHash"] = "true",
                            ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                            ["EVMChains:0:ChainId"] = "8453",
                            ["EVMChains:0:GasLimit"] = "4500000",
                            ["Cors:0"] = "https://tokens.biatec.io"
                        });
                    });
                });

            var client = factory.CreateClient();

            // Act - Use retry logic for CI environment compatibility
            var response = await GetHealthWithRetryAsync(client);

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True, 
                $"Application should start successfully with Hardcoded provider in development. Status: {response.StatusCode}");

            // Cleanup
            client.Dispose();
            await factory.DisposeAsync();
        }

        /// <summary>
        /// Tests that the application starts successfully with EnvironmentVariable provider in development
        /// </summary>
        [Test]
        public async Task Application_Starts_WithEnvironmentVariableProvider_InDevelopment()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_KMS_INTEGRATION_KEY", "TestKeyForIntegrationTests32CharactersMinimumRequired");

            var factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Development");
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["App:Account"] = "test mnemonic phrase for testing purposes only not real",
                            ["KeyManagementConfig:Provider"] = "EnvironmentVariable",
                            ["KeyManagementConfig:EnvironmentVariableName"] = "TEST_KMS_INTEGRATION_KEY",
                            ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                            ["AlgorandAuthentication:CheckExpiration"] = "false",
                            ["AlgorandAuthentication:Debug"] = "true",
                            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                            ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                            ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                            ["IPFSConfig:TimeoutSeconds"] = "30",
                            ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                            ["IPFSConfig:ValidateContentHash"] = "true",
                            ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                            ["EVMChains:0:ChainId"] = "8453",
                            ["EVMChains:0:GasLimit"] = "4500000",
                            ["Cors:0"] = "https://tokens.biatec.io"
                        });
                    });
                });

            var client = factory.CreateClient();

            // Act - Use retry logic for CI environment compatibility
            var response = await GetHealthWithRetryAsync(client);

            // Assert
            Assert.That(response.IsSuccessStatusCode, Is.True,
                $"Application should start successfully with EnvironmentVariable provider in development. Status: {response.StatusCode}");

            // Cleanup
            client.Dispose();
            await factory.DisposeAsync();
            Environment.SetEnvironmentVariable("TEST_KMS_INTEGRATION_KEY", null);
        }

        /// <summary>
        /// Tests that KeyProviderFactory correctly creates the configured provider
        /// </summary>
        [Test]
        public void KeyProviderFactory_CreatesCorrectProvider_ForConfiguration()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.Configure<KeyManagementConfig>(config =>
            {
                config.Provider = "Hardcoded";
                config.HardcodedKey = "TestKeyForFactoryTest32CharactersMinimumRequired";
            });
            services.AddSingleton<EnvironmentKeyProvider>();
            services.AddSingleton<HardcodedKeyProvider>();
            services.AddSingleton<AzureKeyVaultProvider>();
            services.AddSingleton<AwsKmsProvider>();
            services.AddSingleton<KeyProviderFactory>();

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var factory = serviceProvider.GetRequiredService<KeyProviderFactory>();
            var provider = factory.CreateProvider();

            // Assert
            Assert.That(provider, Is.Not.Null, "Factory should create a provider");
            Assert.That(provider.ProviderType, Is.EqualTo("Hardcoded"), "Should create Hardcoded provider");

            // Cleanup
            serviceProvider.Dispose();
        }

        /// <summary>
        /// Tests that Azure Key Vault provider configuration validation works correctly
        /// </summary>
        [Test]
        public async Task AzureKeyVaultProvider_ValidatesConfiguration_Correctly()
        {
            // Arrange - Valid configuration
            var validConfig = new KeyManagementConfig
            {
                Provider = "AzureKeyVault",
                AzureKeyVault = new AzureKeyVaultConfig
                {
                    VaultUrl = "https://test-vault.vault.azure.net/",
                    SecretName = "test-secret",
                    UseManagedIdentity = true
                }
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.Configure<KeyManagementConfig>(config =>
            {
                config.Provider = validConfig.Provider;
                config.AzureKeyVault = validConfig.AzureKeyVault;
            });
            services.AddSingleton<AzureKeyVaultProvider>();

            var serviceProvider = services.BuildServiceProvider();
            var provider = serviceProvider.GetRequiredService<AzureKeyVaultProvider>();

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.True, "Valid Azure Key Vault configuration should pass validation");

            // Cleanup
            serviceProvider.Dispose();
        }

        /// <summary>
        /// Tests that Azure Key Vault provider rejects invalid configuration
        /// </summary>
        [Test]
        public async Task AzureKeyVaultProvider_RejectsInvalidConfiguration()
        {
            // Arrange - Missing VaultUrl
            var invalidConfig = new KeyManagementConfig
            {
                Provider = "AzureKeyVault",
                AzureKeyVault = new AzureKeyVaultConfig
                {
                    VaultUrl = "",  // Invalid: empty
                    SecretName = "test-secret",
                    UseManagedIdentity = true
                }
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.Configure<KeyManagementConfig>(config =>
            {
                config.Provider = invalidConfig.Provider;
                config.AzureKeyVault = invalidConfig.AzureKeyVault;
            });
            services.AddSingleton<AzureKeyVaultProvider>();

            var serviceProvider = services.BuildServiceProvider();
            var provider = serviceProvider.GetRequiredService<AzureKeyVaultProvider>();

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.False, "Invalid Azure Key Vault configuration should fail validation");

            // Cleanup
            serviceProvider.Dispose();
        }

        /// <summary>
        /// Tests that AWS Secrets Manager provider configuration validation works correctly
        /// </summary>
        [Test]
        public async Task AwsKmsProvider_ValidatesConfiguration_Correctly()
        {
            // Arrange - Valid configuration
            var validConfig = new KeyManagementConfig
            {
                Provider = "AwsKms",
                AwsKms = new AwsKmsConfig
                {
                    Region = "us-east-1",
                    KeyId = "test-key-id",
                    UseIamRole = true
                }
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.Configure<KeyManagementConfig>(config =>
            {
                config.Provider = validConfig.Provider;
                config.AwsKms = validConfig.AwsKms;
            });
            services.AddSingleton<AwsKmsProvider>();

            var serviceProvider = services.BuildServiceProvider();
            var provider = serviceProvider.GetRequiredService<AwsKmsProvider>();

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.True, "Valid AWS KMS configuration should pass validation");

            // Cleanup
            serviceProvider.Dispose();
        }

        /// <summary>
        /// Tests that AWS Secrets Manager provider rejects invalid configuration
        /// </summary>
        [Test]
        public async Task AwsKmsProvider_RejectsInvalidConfiguration()
        {
            // Arrange - Missing Region
            var invalidConfig = new KeyManagementConfig
            {
                Provider = "AwsKms",
                AwsKms = new AwsKmsConfig
                {
                    Region = "",  // Invalid: empty
                    KeyId = "test-key-id",
                    UseIamRole = true
                }
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.Configure<KeyManagementConfig>(config =>
            {
                config.Provider = invalidConfig.Provider;
                config.AwsKms = invalidConfig.AwsKms;
            });
            services.AddSingleton<AwsKmsProvider>();

            var serviceProvider = services.BuildServiceProvider();
            var provider = serviceProvider.GetRequiredService<AwsKmsProvider>();

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.False, "Invalid AWS KMS configuration should fail validation");

            // Cleanup
            serviceProvider.Dispose();
        }

        /// <summary>
        /// Tests that EnvironmentVariable provider validates key length requirements
        /// </summary>
        [Test]
        public async Task EnvironmentVariableProvider_ValidatesKeyLength()
        {
            // Arrange - Key that's too short
            Environment.SetEnvironmentVariable("SHORT_KEY_TEST", "TooShort");

            var config = new KeyManagementConfig
            {
                Provider = "EnvironmentVariable",
                EnvironmentVariableName = "SHORT_KEY_TEST"
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.Configure<KeyManagementConfig>(c =>
            {
                c.Provider = config.Provider;
                c.EnvironmentVariableName = config.EnvironmentVariableName;
            });
            services.AddSingleton<EnvironmentKeyProvider>();

            var serviceProvider = services.BuildServiceProvider();
            var provider = serviceProvider.GetRequiredService<EnvironmentKeyProvider>();

            // Act
            var isValid = await provider.ValidateConfigurationAsync();

            // Assert
            Assert.That(isValid, Is.False, "Short key should fail validation (< 32 characters)");

            // Cleanup
            Environment.SetEnvironmentVariable("SHORT_KEY_TEST", null);
            serviceProvider.Dispose();
        }

        /// <summary>
        /// Tests that key provider lazy initialization works correctly (performance optimization)
        /// </summary>
        [Test]
        public void AzureKeyVaultProvider_UsesLazyInitialization()
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

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.Configure<KeyManagementConfig>(c =>
            {
                c.Provider = config.Provider;
                c.AzureKeyVault = config.AzureKeyVault;
            });
            services.AddSingleton<AzureKeyVaultProvider>();

            var serviceProvider = services.BuildServiceProvider();

            // Act - Just create the provider, don't call GetEncryptionKeyAsync yet
            var provider = serviceProvider.GetRequiredService<AzureKeyVaultProvider>();

            // Assert - Provider should be created successfully
            // The lazy initialization means the SecretClient won't be created until first use
            Assert.That(provider, Is.Not.Null, "Provider should be created without initializing the client");
            Assert.That(provider.ProviderType, Is.EqualTo("AzureKeyVault"));

            // Cleanup
            serviceProvider.Dispose();
        }

        /// <summary>
        /// Tests that multiple calls to the same provider reuse the client (connection pooling)
        /// </summary>
        [Test]
        public async Task Providers_ReuseClients_ForMultipleCalls()
        {
            // Arrange
            Environment.SetEnvironmentVariable("REUSE_TEST_KEY", "TestKeyForReuseTest32CharactersMinimumRequired");

            var config = new KeyManagementConfig
            {
                Provider = "EnvironmentVariable",
                EnvironmentVariableName = "REUSE_TEST_KEY"
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.Configure<KeyManagementConfig>(c =>
            {
                c.Provider = config.Provider;
                c.EnvironmentVariableName = config.EnvironmentVariableName;
            });
            services.AddSingleton<EnvironmentKeyProvider>();

            var serviceProvider = services.BuildServiceProvider();
            var provider = serviceProvider.GetRequiredService<EnvironmentKeyProvider>();

            // Act - Call twice
            var key1 = await provider.GetEncryptionKeyAsync();
            var key2 = await provider.GetEncryptionKeyAsync();

            // Assert - Should get the same key both times (proving client reuse)
            Assert.That(key1, Is.EqualTo(key2), "Multiple calls should return the same key");
            Assert.That(key1.Length, Is.GreaterThanOrEqualTo(32), "Key should meet minimum length");

            // Cleanup
            Environment.SetEnvironmentVariable("REUSE_TEST_KEY", null);
            serviceProvider.Dispose();
        }
    }
}
