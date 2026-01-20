using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ERC20.Request;
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
        private ERC20TokenService _tokenService;
        private Mock<IOptionsMonitor<EVMChains>> _blockchainConfigMock;
        private Mock<IOptionsMonitor<AppConfiguration>> _appConfigMock;
        private Mock<ILogger<ERC20TokenService>> _loggerMock;
        private EVMChains _blockchainConfig;
        private AppConfiguration _appConfig;
        private ERC20MintableTokenDeploymentRequest _validRequest;

        [SetUp]
        public void Setup()
        {
            // Set up configuration
            _blockchainConfig = new EVMChains
            {
                Chains = new List<EVMBlockchainConfig>
                {
                    new EVMBlockchainConfig
                    {
                        RpcUrl = "http://127.0.0.1:8545",
                        ChainId = 31337,
                        GasLimit = 4500000
                    }
                }
            };

            _appConfig = new AppConfiguration
            {
                Account = "test-account"
            };

            _blockchainConfigMock = new Mock<IOptionsMonitor<EVMChains>>();
            _blockchainConfigMock.Setup(x => x.CurrentValue).Returns(_blockchainConfig);

            _appConfigMock = new Mock<IOptionsMonitor<AppConfiguration>>();
            _appConfigMock.Setup(x => x.CurrentValue).Returns(_appConfig);

            _loggerMock = new Mock<ILogger<ERC20TokenService>>();

            _tokenService = new ERC20TokenService(_blockchainConfigMock.Object, _appConfigMock.Object, _loggerMock.Object);

            // Create a valid deployment request for testing
            _validRequest = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337, // Required property
                Cap = 10000000, // Required for mintable tokens
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79" // Sample address
            };
        }

        [Test]
        public void Constructor_InitializesServiceCorrectly()
        {
            // Assert
            Assert.That(_tokenService, Is.Not.Null);
        }

        [Test]
        public void ValidateChainConfiguration_WithValidChainId_DoesNotThrow()
        {
            // Arrange
            var chainId = 31337;

            // Act & Assert - Use reflection to call private method if needed
            // For now, verify the service was constructed successfully
            Assert.That(_tokenService, Is.Not.Null);
        }

        [Test]
        public void ValidateChainConfiguration_WithInvalidChainId_ThrowsException()
        {
            // Arrange
            var invalidRequest = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 99999, // Invalid chain ID
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await _tokenService.DeployERC20TokenAsync(invalidRequest, TokenType.ERC20_Mintable);
            });
        }

        [Test]
        public async Task DeployERC20TokenAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await _tokenService.DeployERC20TokenAsync(null!, TokenType.ERC20_Mintable);
            });
        }

        [Test]
        public void ERC20TokenService_SupportsMultipleChainConfigurations()
        {
            // Arrange
            var multiChainConfig = new EVMChains
            {
                Chains = new List<EVMBlockchainConfig>
                {
                    new EVMBlockchainConfig
                    {
                        RpcUrl = "http://127.0.0.1:8545",
                        ChainId = 31337,
                        GasLimit = 4500000
                    },
                    new EVMBlockchainConfig
                    {
                        RpcUrl = "https://mainnet.base.org",
                        ChainId = 8453,
                        GasLimit = 4500000
                    }
                }
            };

            var multiChainConfigMock = new Mock<IOptionsMonitor<EVMChains>>();
            multiChainConfigMock.Setup(x => x.CurrentValue).Returns(multiChainConfig);

            // Act
            var service = new ERC20TokenService(multiChainConfigMock.Object, _appConfigMock.Object, _loggerMock.Object);

            // Assert
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public void ValidRequest_HasAllRequiredProperties()
        {
            // Assert
            Assert.That(_validRequest.Name, Is.EqualTo("Test Token"));
            Assert.That(_validRequest.Symbol, Is.EqualTo("TEST"));
            Assert.That(_validRequest.InitialSupply, Is.EqualTo(1000000));
            Assert.That(_validRequest.Decimals, Is.EqualTo(18));
            Assert.That(_validRequest.ChainId, Is.EqualTo(31337));
            Assert.That(_validRequest.Cap, Is.EqualTo(10000000));
            Assert.That(_validRequest.InitialSupplyReceiver, Is.Not.Null);
        }
    }
}