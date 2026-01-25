using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ERC20TokenServiceTests
    {
        private Mock<IOptionsMonitor<EVMChains>> _configMock;
        private Mock<IOptionsMonitor<AppConfiguration>> _appConfigMock;
        private Mock<ILogger<ERC20TokenService>> _loggerMock;
        private Mock<ITokenIssuanceRepository> _tokenIssuanceRepositoryMock;
        private EVMChains _evmConfig;
        private AppConfiguration _appConfig;

        [SetUp]
        public void Setup()
        {
            _evmConfig = new EVMChains
            {
                Chains = new List<EVMBlockchainConfig>
                {
                    new EVMBlockchainConfig
                    {
                        ChainId = 8453, // Base
                        RpcUrl = "https://mainnet.base.org",
                        GasLimit = 4500000
                    },
                    new EVMBlockchainConfig
                    {
                        ChainId = 1, // Ethereum Mainnet
                        RpcUrl = "https://eth.public-rpc.com",
                        GasLimit = 3000000
                    }
                }
            };

            _appConfig = new AppConfiguration
            {
                Account = "test-account"
            };

            _configMock = new Mock<IOptionsMonitor<EVMChains>>();
            _configMock.Setup(x => x.CurrentValue).Returns(_evmConfig);

            _appConfigMock = new Mock<IOptionsMonitor<AppConfiguration>>();
            _appConfigMock.Setup(x => x.CurrentValue).Returns(_appConfig);

            _loggerMock = new Mock<ILogger<ERC20TokenService>>();
            _tokenIssuanceRepositoryMock = new Mock<ITokenIssuanceRepository>();
        }

        #region ERC20 Mintable Token Validation Tests

        [Test]
        public void ValidateRequest_ValidMintableToken_DoesNotThrow()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000000
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_SymbolTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "VERYLONGSYM", // 11 characters
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000000
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Symbol").And.Contain("10 characters"));
        }

        [Test]
        public void ValidateRequest_NameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = new string('A', 51), // 51 characters
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000000
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Name").And.Contain("50 characters"));
        }

        [Test]
        public void ValidateRequest_ZeroInitialSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 0,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000000
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply").And.Contain("positive value"));
        }

        [Test]
        public void ValidateRequest_NegativeInitialSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = -100,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000000
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply").And.Contain("positive value"));
        }

        [Test]
        [TestCase(-1, Description = "Decimals below 0")]
        [TestCase(19, Description = "Decimals above 18")]
        public void ValidateRequest_InvalidDecimals_ThrowsArgumentException(int decimals)
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = decimals,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000000
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Decimals").And.Contain("between 0 and 18"));
        }

        [Test]
        public void ValidateRequest_EmptyReceiverAddress_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = string.Empty,
                Cap = 10000000
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply receiver"));
        }

        [Test]
        public void ValidateRequest_CapLessThanInitialSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 10000000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 5000000 // Cap less than initial supply
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Cap").And.Contain("at least the initial supply"));
        }

        [Test]
        public void ValidateRequest_WrongRequestTypeForMintable_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TST",
                InitialSupply = 1000,
                Decimals = 18,
                ChainId = 8453
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("ERC20MintableTokenDeploymentRequest"));
        }

        #endregion

        #region ERC20 Preminted Token Validation Tests

        [Test]
        public void ValidateRequest_ValidPremintedToken_DoesNotThrow()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC200_Preminted));
        }

        [Test]
        public void ValidateRequest_PremintedSymbolTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "VERYLONGSYM", // 11 characters
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("Symbol").And.Contain("10 characters"));
        }

        [Test]
        public void ValidateRequest_PremintedNameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = new string('A', 51),
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("Name").And.Contain("50 characters"));
        }

        [Test]
        public void ValidateRequest_PremintedZeroInitialSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 0,
                Decimals = 18,
                ChainId = 8453
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("non-negative"));
        }

        [Test]
        public void ValidateRequest_PremintedInvalidDecimals_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 20, // Above 18
                ChainId = 8453
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("Decimals").And.Contain("between 0 and 18"));
        }

        [Test]
        public void ValidateRequest_WrongRequestTypeForPreminted_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TST",
                InitialSupply = 1000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("ERC20PremintedTokenDeploymentRequest"));
        }

        #endregion

        #region Unsupported Token Type Tests

        [Test]
        public void ValidateRequest_UnsupportedTokenType_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TST",
                InitialSupply = 1000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                service.ValidateRequest(request, TokenType.ARC200_Mintable));
            Assert.That(ex.Message, Does.Contain("Unsupported token type"));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ValidateRequest_MaximumDecimals_DoesNotThrow()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18, // Maximum
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000000
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_ZeroDecimals_DoesNotThrow()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 0, // Minimum
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000000
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_CapEqualsInitialSupply_DoesNotThrow()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 1000000 // Same as initial supply
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_MaxSymbolLength_DoesNotThrow()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "ABCDEFGHIJ", // Exactly 10 characters
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000000
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_MaxNameLength_DoesNotThrow()
        {
            // Arrange
            var service = CreateService();
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = new string('A', 50), // Exactly 50 characters
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                Cap = 10000000
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        [Test]
        public void GetBlockchainConfig_InvalidChainId_ThrowsInvalidOperationException()
        {
            // Arrange
            var service = CreateService();
            var method = typeof(ERC20TokenService).GetMethod("GetBlockchainConfig",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act & Assert
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method?.Invoke(service, new object[] { 99999 }));
            
            Assert.That(ex?.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ex?.InnerException?.Message, Does.Contain("No configuration found for chain ID"));
        }

        [Test]
        public void GetBlockchainConfig_ValidChainId_ReturnsConfig()
        {
            // Arrange
            var service = CreateService();
            var method = typeof(ERC20TokenService).GetMethod("GetBlockchainConfig",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = method?.Invoke(service, new object[] { 8453 });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<EVMBlockchainConfig>());
            var config = result as EVMBlockchainConfig;
            Assert.That(config?.ChainId, Is.EqualTo(8453));
        }

        #endregion

        #region Helper Methods

        private ERC20TokenService CreateService()
        {
            return new ERC20TokenService(_configMock.Object, _appConfigMock.Object, _loggerMock.Object, _tokenIssuanceRepositoryMock.Object);
        }

        #endregion
    }
}
