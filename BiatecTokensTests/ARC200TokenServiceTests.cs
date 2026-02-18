using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Services;
using BiatecTokensApi.Repositories.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using AlgorandAuthenticationV2;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ARC200TokenServiceTests
    {
        private Mock<IOptionsMonitor<AlgorandAuthenticationOptionsV2>> _configMock;
        private Mock<IOptionsMonitor<AppConfiguration>> _appConfigMock;
        private Mock<ILogger<ARC200TokenService>> _loggerMock;
        private Mock<ITokenIssuanceRepository> _tokenIssuanceRepositoryMock;
        private Mock<IComplianceRepository> _complianceRepositoryMock;
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

            _loggerMock = new Mock<ILogger<ARC200TokenService>>();
            _tokenIssuanceRepositoryMock = new Mock<ITokenIssuanceRepository>();
            _complianceRepositoryMock = new Mock<IComplianceRepository>();
        }

        #region ARC200 Mintable Token Validation Tests

        [Test]
        public void ValidateRequest_ValidMintableToken_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC200_Mintable));
        }

        [Test]
        public void ValidateRequest_SymbolTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
                service.ValidateRequest(request, TokenType.ARC200_Mintable));
            Assert.That(ex.Message, Does.Contain("Symbol").And.Contain("10 characters"));
        }

        [Test]
        public void ValidateRequest_NameTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
                service.ValidateRequest(request, TokenType.ARC200_Mintable));
            Assert.That(ex.Message, Does.Contain("Name").And.Contain("50 characters"));
        }

        [Test]
        public void ValidateRequest_NegativeInitialSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
                service.ValidateRequest(request, TokenType.ARC200_Mintable));
            Assert.That(ex.Message, Does.Contain("non-negative"));
        }

        [Test]
        [TestCase(-1, Description = "Decimals below 0")]
        [TestCase(19, Description = "Decimals above 18")]
        public void ValidateRequest_InvalidDecimals_ThrowsArgumentException(int decimals)
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
                service.ValidateRequest(request, TokenType.ARC200_Mintable));
            Assert.That(ex.Message, Does.Contain("Decimals").And.Contain("between 0 and 18"));
        }

        [Test]
        public void ValidateRequest_EmptyReceiverAddress_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
                service.ValidateRequest(request, TokenType.ARC200_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply receiver"));
        }

        [Test]
        public void ValidateRequest_CapLessThanInitialSupply_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
                service.ValidateRequest(request, TokenType.ARC200_Mintable));
            Assert.That(ex.Message, Does.Contain("Cap").And.Contain("at least the initial supply"));
        }

        [Test]
        public void ValidateRequest_WrongRequestTypeForMintable_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TST",
                InitialSupply = 1000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                service.ValidateRequest(request, TokenType.ARC200_Mintable));
            Assert.That(ex.Message, Does.Contain("ARC200MintableTokenDeploymentRequest"));
        }

        #endregion

        #region ARC200 Preminted Token Validation Tests

        [Test]
        public void ValidateRequest_ValidPremintedToken_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC200_Preminted));
        }

        [Test]
        public void ValidateRequest_PremintedSymbolTooLong_ThrowsArgumentException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "VERYLONGSYM", // 11 characters
                InitialSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
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
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Name = new string('A', 51),
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 6,
                Network = "testnet-v1.0"
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
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 0,
                Decimals = 6,
                Network = "testnet-v1.0"
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
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 20, // Above 18
                Network = "testnet-v1.0"
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
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
            var ex = Assert.Throws<ArgumentException>(() => 
                service.ValidateRequest(request, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("ARC200PremintedTokenDeploymentRequest"));
        }

        #endregion

        #region Unsupported Token Type Tests

        [Test]
        public void ValidateRequest_UnsupportedTokenType_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
            var request = new ARC200MintableTokenDeploymentRequest
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
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC200_Mintable));
        }

        [Test]
        public void ValidateRequest_ZeroDecimals_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC200_Mintable));
        }

        [Test]
        public void ValidateRequest_CapEqualsInitialSupply_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC200_Mintable));
        }

        [Test]
        public void ValidateRequest_MaxSymbolLength_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC200_Mintable));
        }

        [Test]
        public void ValidateRequest_MaxNameLength_DoesNotThrow()
        {
            // Arrange
            var service = CreateServiceWithoutNetworkValidation();
            var request = new ARC200MintableTokenDeploymentRequest
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
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ARC200_Mintable));
        }

        #endregion

        #region Helper Methods

        private ARC200TokenService CreateServiceWithoutNetworkValidation()
        {
            try
            {
                var mockHttpContextAccessor = Mock.Of<IHttpContextAccessor>();
                return new ARC200TokenService(_configMock.Object, _appConfigMock.Object, _loggerMock.Object, _tokenIssuanceRepositoryMock.Object, _complianceRepositoryMock.Object, mockHttpContextAccessor);
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
