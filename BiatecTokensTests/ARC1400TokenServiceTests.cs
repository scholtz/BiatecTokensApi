using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC1400.Request;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AlgorandAuthenticationV2;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ARC1400TokenServiceTests
    {
        private Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>> _configMock;
        private Mock<IOptionsMonitor<AppConfiguration>> _appConfigMock;
        private Mock<ILogger<ARC1400TokenService>> _loggerMock;
        private AlgorandAuthenticationOptionsV2 _algoConfig;
        private AppConfiguration _appConfig;

        [SetUp]
        public void Setup()
        {
            _algoConfig = new AlgorandAuthenticationOptionsV2
            {
                AllowedNetworks = new AllowedNetworks()
            };

            _appConfig = new AppConfiguration
            {
                Account = "test-account"
            };

            _configMock = new Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>>();
            _configMock.Setup(x => x.CurrentValue).Returns(_algoConfig);

            _appConfigMock = new Mock<IOptionsMonitor<AppConfiguration>>();
            _appConfigMock.Setup(x => x.CurrentValue).Returns(_appConfig);

            _loggerMock = new Mock<ILogger<ARC1400TokenService>>();
        }

        #region ARC1400 Mintable Token Validation Tests

        [Test]
        public void ValidateRequest_ValidMintableToken_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC1400_Mintable));
        }

        [Test]
        public void ValidateRequest_SymbolTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "VERYLONGSYM", // 11 characters
                InitialSupply = 1000000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC1400_Mintable));
            Assert.That(ex.Message, Does.Contain("Symbol").And.Contain("10 characters"));
        }

        [Test]
        public void ValidateRequest_NameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = new string('A', 51), // 51 characters
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC1400_Mintable));
            Assert.That(ex.Message, Does.Contain("Name").And.Contain("50 characters"));
        }

        [Test]
        public void ValidateRequest_NegativeInitialSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = -100,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC1400_Mintable));
            Assert.That(ex.Message, Does.Contain("non-negative"));
        }

        [Test]
        [TestCase(-1, Description = "Decimals below 0")]
        [TestCase(19, Description = "Decimals above 18")]
        public void ValidateRequest_InvalidDecimals_ThrowsArgumentException(int decimals)
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = decimals,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC1400_Mintable));
            Assert.That(ex.Message, Does.Contain("Decimals").And.Contain("between 0 and 18"));
        }

        [Test]
        public void ValidateRequest_EmptyReceiverAddress_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 6,
                InitialSupplyReceiver = string.Empty,
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC1400_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply receiver"));
        }

        [Test]
        public void ValidateRequest_NullReceiverAddress_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 6,
                InitialSupplyReceiver = null,
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC1400_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply receiver"));
        }

        [Test]
        public void ValidateRequest_CapLessThanInitialSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 10000000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 5000000, // Cap less than initial supply
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC1400_Mintable));
            Assert.That(ex.Message, Does.Contain("Cap").And.Contain("at least the initial supply"));
        }

        [Test]
        public void ValidateRequest_WrongRequestType_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400TokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TST",
                InitialSupply = 1000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                service.ValidateRequest(request, TokenType.ARC1400_Mintable));
            Assert.That(ex.Message, Does.Contain("ARC1400MintableTokenDeploymentRequest"));
        }

        #endregion

        #region Unsupported Token Type Tests

        [Test]
        public void ValidateRequest_UnsupportedTokenType_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TST",
                InitialSupply = 1000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                service.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Unsupported token type"));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ValidateRequest_MaximumDecimals_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18, // Maximum
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC1400_Mintable));
        }

        [Test]
        public void ValidateRequest_ZeroDecimals_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 0, // Minimum
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC1400_Mintable));
        }

        [Test]
        public void ValidateRequest_CapEqualsInitialSupply_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 1000000, // Same as initial supply
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC1400_Mintable));
        }

        [Test]
        public void ValidateRequest_MaxSymbolLength_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "ABCDEFGHIJ", // Exactly 10 characters
                InitialSupply = 1000000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC1400_Mintable));
        }

        [Test]
        public void ValidateRequest_MaxNameLength_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = new string('A', 50), // Exactly 50 characters
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC1400_Mintable));
        }

        [Test]
        public void ValidateRequest_ZeroInitialSupply_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 0, // Zero is allowed
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC1400_Mintable));
        }

        [Test]
        public void ValidateRequest_SingleCharacterSymbol_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "T", // Single character
                InitialSupply = 1000000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC1400_Mintable));
        }

        [Test]
        public void ValidateRequest_SingleCharacterName_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "T", // Single character
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000000,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC1400_Mintable));
        }

        [Test]
        public void ValidateRequest_LargeInitialSupply_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = long.MaxValue, // Very large supply
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = long.MaxValue,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC1400_Mintable));
        }

        #endregion

        #region Helper Methods

        private ARC1400TokenService CreateServiceWithoutNetworkValidation()
        {
            try
            {
                return new ARC1400TokenService(_configMock.Object, _appConfigMock.Object, _loggerMock.Object);
            }
            catch
            {
                // Return null if constructor fails, tests will handle
                return null!;
            }
        }

        #endregion
    }
}
