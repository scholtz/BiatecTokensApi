using BiatecTokensApi.Configuration;
using BiatecTokensApi.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for KeyProviderFactory
    /// </summary>
    [TestFixture]
    public class KeyProviderFactoryTests
    {
        private ServiceProvider _serviceProvider = null!;

        [SetUp]
        public void Setup()
        {
            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder => builder.AddConsole());

            // Register all key providers
            services.AddSingleton<EnvironmentKeyProvider>();
            services.AddSingleton<HardcodedKeyProvider>();
            services.AddSingleton<AzureKeyVaultProvider>();
            services.AddSingleton<AwsKmsProvider>();

            _serviceProvider = services.BuildServiceProvider();
        }

        [TearDown]
        public void TearDown()
        {
            _serviceProvider?.Dispose();
        }

        [Test]
        public void Factory_CreatesEnvironmentKeyProvider_WhenConfigured()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "EnvironmentVariable"
            };

            var factory = new KeyProviderFactory(
                _serviceProvider,
                Options.Create(config),
                _serviceProvider.GetRequiredService<ILogger<KeyProviderFactory>>());

            // Act
            var provider = factory.CreateProvider();

            // Assert
            Assert.That(provider, Is.InstanceOf<EnvironmentKeyProvider>());
            Assert.That(provider.ProviderType, Is.EqualTo("EnvironmentVariable"));
        }

        [Test]
        public void Factory_CreatesHardcodedKeyProvider_WhenConfigured()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "Hardcoded"
            };

            var factory = new KeyProviderFactory(
                _serviceProvider,
                Options.Create(config),
                _serviceProvider.GetRequiredService<ILogger<KeyProviderFactory>>());

            // Act
            var provider = factory.CreateProvider();

            // Assert
            Assert.That(provider, Is.InstanceOf<HardcodedKeyProvider>());
            Assert.That(provider.ProviderType, Is.EqualTo("Hardcoded"));
        }

        [Test]
        public void Factory_CreatesAzureKeyVaultProvider_WhenConfigured()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "AzureKeyVault"
            };

            var factory = new KeyProviderFactory(
                _serviceProvider,
                Options.Create(config),
                _serviceProvider.GetRequiredService<ILogger<KeyProviderFactory>>());

            // Act
            var provider = factory.CreateProvider();

            // Assert
            Assert.That(provider, Is.InstanceOf<AzureKeyVaultProvider>());
            Assert.That(provider.ProviderType, Is.EqualTo("AzureKeyVault"));
        }

        [Test]
        public void Factory_CreatesAwsKmsProvider_WhenConfigured()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "AwsKms"
            };

            var factory = new KeyProviderFactory(
                _serviceProvider,
                Options.Create(config),
                _serviceProvider.GetRequiredService<ILogger<KeyProviderFactory>>());

            // Act
            var provider = factory.CreateProvider();

            // Assert
            Assert.That(provider, Is.InstanceOf<AwsKmsProvider>());
            Assert.That(provider.ProviderType, Is.EqualTo("AwsKms"));
        }

        [Test]
        public void Factory_IsCaseInsensitive()
        {
            // Arrange - test with lowercase
            var config = new KeyManagementConfig
            {
                Provider = "environmentvariable"
            };

            var factory = new KeyProviderFactory(
                _serviceProvider,
                Options.Create(config),
                _serviceProvider.GetRequiredService<ILogger<KeyProviderFactory>>());

            // Act
            var provider = factory.CreateProvider();

            // Assert
            Assert.That(provider, Is.InstanceOf<EnvironmentKeyProvider>());
        }

        [Test]
        public void Factory_ThrowsException_WithInvalidProvider()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = "InvalidProvider"
            };

            var factory = new KeyProviderFactory(
                _serviceProvider,
                Options.Create(config),
                _serviceProvider.GetRequiredService<ILogger<KeyProviderFactory>>());

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => factory.CreateProvider());
        }

        [Test]
        public void Factory_ThrowsException_WithNullProvider()
        {
            // Arrange
            var config = new KeyManagementConfig
            {
                Provider = null!
            };

            var factory = new KeyProviderFactory(
                _serviceProvider,
                Options.Create(config),
                _serviceProvider.GetRequiredService<ILogger<KeyProviderFactory>>());

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => factory.CreateProvider());
        }
    }
}
