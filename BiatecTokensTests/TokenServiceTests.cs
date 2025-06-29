using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TokenServiceTests
    {
        private TokenService _tokenService;
        private Mock<IOptions<BlockchainConfig>> _blockchainConfigMock;
        private Mock<ILogger<TokenService>> _loggerMock;
        private BlockchainConfig _blockchainConfig;
        private TokenDeploymentRequest _validRequest;

        [SetUp]
        public void Setup()
        {
            // Set up configuration
            _blockchainConfig = new BlockchainConfig
            {
                BaseRpcUrl = "https://mainnet.base.org",
                ChainId = 8453,
                GasLimit = 4500000
            };

            _blockchainConfigMock = new Mock<IOptions<BlockchainConfig>>();
            _blockchainConfigMock.Setup(x => x.Value).Returns(_blockchainConfig);

            _loggerMock = new Mock<ILogger<TokenService>>();

            _tokenService = new TokenService(_blockchainConfigMock.Object, _loggerMock.Object);

            // Create a valid deployment request for testing
            _validRequest = new TokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                DeployerPrivateKey = "0xabc123def456abc123def456abc123def456abc123def456abc123def456abcd"
            };
        }

        [Test]
        public void Constructor_SetsConfiguration()
        {
            // Act - done in Setup

            // Assert - Verify that configuration is set via reflection (testing private field)
            var configField = typeof(TokenService)
                .GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(configField, Is.Not.Null);

            var configValue = configField!.GetValue(_tokenService);
            Assert.That(configValue, Is.EqualTo(_blockchainConfig));
        }

        [Test]
        public async Task DeployTokenAsync_HandlesExceptions()
        {
            // We can't easily test the successful deployment without mocking the Nethereum library,
            // but we can test exception handling

            // Arrange
            var invalidRequest = new TokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                // Invalid private key that will cause an exception
                DeployerPrivateKey = "invalid-key"
            };

            // Act
            var result = await _tokenService.DeployTokenAsync(invalidRequest);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.Or.Empty);
            
            // Verify logging occurred
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public void TokenService_UsesConfiguredChainId()
        {
            // Arrange
            const int expectedChainId = 84532; // Base Sepolia testnet
            
            var config = new BlockchainConfig
            {
                BaseRpcUrl = "https://sepolia.base.org",
                ChainId = expectedChainId,
                GasLimit = 4500000
            };

            var configMock = new Mock<IOptions<BlockchainConfig>>();
            configMock.Setup(x => x.Value).Returns(config);

            // Act
            var service = new TokenService(configMock.Object, _loggerMock.Object);
            
            // Assert - Verify that chain ID is set correctly via reflection
            var configField = typeof(TokenService)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => f.Name == "_config");
                
            Assert.That(configField, Is.Not.Null);
            
            var configValue = configField!.GetValue(service) as BlockchainConfig;
            Assert.That(configValue, Is.Not.Null);
            Assert.That(configValue!.ChainId, Is.EqualTo(expectedChainId));
        }
    }
}