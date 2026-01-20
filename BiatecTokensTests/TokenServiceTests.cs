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

        #region Validation Tests

        [Test]
        public void ValidateRequest_ValidMintableRequest_DoesNotThrow()
        {
            // Arrange - use the valid request from Setup

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_SymbolTooLong_ThrowsArgumentException()
        {
            // Arrange
            _validRequest.Symbol = "VERYLONGSYM"; // 11 characters

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Symbol").And.Contain("10 characters"));
        }

        [Test]
        public void ValidateRequest_NameTooLong_ThrowsArgumentException()
        {
            // Arrange
            _validRequest.Name = new string('A', 51); // 51 characters

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Name").And.Contain("50 characters"));
        }

        [Test]
        public void ValidateRequest_InitialSupplyZero_ThrowsArgumentException()
        {
            // Arrange
            _validRequest.InitialSupply = 0;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply").And.Contain("non-negative"));
        }

        [Test]
        public void ValidateRequest_InitialSupplyNegative_ThrowsArgumentException()
        {
            // Arrange
            _validRequest.InitialSupply = -100;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply"));
        }

        [Test]
        [TestCase(-1, Description = "Decimals below 0")]
        [TestCase(19, Description = "Decimals above 18")]
        public void ValidateRequest_InvalidDecimals_ThrowsArgumentException(int decimals)
        {
            // Arrange
            _validRequest.Decimals = decimals;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Decimals").And.Contain("between 0 and 18"));
        }

        [Test]
        public void ValidateRequest_EmptyReceiverAddress_ThrowsArgumentException()
        {
            // Arrange
            _validRequest.InitialSupplyReceiver = string.Empty;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply receiver"));
        }

        [Test]
        public void ValidateRequest_NullReceiverAddress_ThrowsArgumentException()
        {
            // Arrange
            _validRequest.InitialSupplyReceiver = null!;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply receiver"));
        }

        [Test]
        public void ValidateRequest_CapLessThanInitialSupply_ThrowsArgumentException()
        {
            // Arrange
            _validRequest.InitialSupply = 10000000;
            _validRequest.Cap = 5000000; // Cap less than initial supply

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Cap").And.Contain("at least the initial supply"));
        }

        [Test]
        public void ValidateRequest_WrongRequestTypeForMintable_ThrowsArgumentException()
        {
            // Arrange
            var wrongRequest = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TST",
                InitialSupply = 1000,
                Decimals = 18,
                ChainId = 31337,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(wrongRequest, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("ERC20MintableTokenDeploymentRequest"));
        }

        #endregion
    }
}