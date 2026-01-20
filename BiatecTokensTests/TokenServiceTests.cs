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
        private ERC20MintableTokenDeploymentRequest _validMintableRequest;
        private ERC20PremintedTokenDeploymentRequest _validPremintedRequest;

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

            // Create a valid mintable deployment request for testing
            _validMintableRequest = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Create a valid preminted deployment request for testing
            _validPremintedRequest = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Preminted Token",
                Symbol = "PRE",
                InitialSupply = 5000000,
                Decimals = 6,
                ChainId = 31337
            };
        }

        #region Validation Tests - Mintable

        [Test]
        public void ValidateRequest_ValidMintableRequest_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(_validMintableRequest, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_MintableWithSymbolTooLong_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "VERYLONGSYMBOL123",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Symbol").And.Contains("10 characters"));
        }

        [Test]
        public void ValidateRequest_MintableWithNameTooLong_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = new string('A', 51),
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Name").And.Contains("50 characters"));
        }

        [Test]
        public void ValidateRequest_MintableWithZeroInitialSupply_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 0,
                Decimals = 18,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply").And.Contains("non-negative"));
        }

        [Test]
        public void ValidateRequest_MintableWithDecimalsTooHigh_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 19,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Decimals").And.Contains("between 0 and 18"));
        }

        [Test]
        public void ValidateRequest_MintableWithNegativeDecimals_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = -1,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Decimals").And.Contains("between 0 and 18"));
        }

        [Test]
        public void ValidateRequest_MintableWithEmptyReceiver_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = ""
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply receiver"));
        }

        [Test]
        public void ValidateRequest_MintableWithCapLessThanInitialSupply_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 10000000,
                Decimals = 18,
                ChainId = 31337,
                Cap = 1000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Cap").And.Contains("at least the initial supply"));
        }

        [Test]
        public void ValidateRequest_MintableWithWrongRequestType_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("ERC20MintableTokenDeploymentRequest"));
        }

        #endregion

        #region Validation Tests - Preminted

        [Test]
        public void ValidateRequest_ValidPremintedRequest_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(_validPremintedRequest, TokenType.ARC200_Preminted));
        }

        [Test]
        public void ValidateRequest_PremintedWithSymbolTooLong_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "VERYLONGSYMBOL123",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("Symbol").And.Contains("10 characters"));
        }

        [Test]
        public void ValidateRequest_PremintedWithNameTooLong_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = new string('A', 51),
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("Name").And.Contains("50 characters"));
        }

        [Test]
        public void ValidateRequest_PremintedWithZeroInitialSupply_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 0,
                Decimals = 18,
                ChainId = 31337
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("Initial supply").And.Contains("non-negative"));
        }

        [Test]
        public void ValidateRequest_PremintedWithWrongRequestType_ShouldThrowArgumentException()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _tokenService.ValidateRequest(request, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("ERC20PremintedTokenDeploymentRequest"));
        }

        [Test]
        public void ValidateRequest_UnsupportedTokenType_ShouldThrowArgumentOutOfRangeException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => 
                _tokenService.ValidateRequest(_validMintableRequest, TokenType.ARC3_FT));
            Assert.That(ex.Message, Does.Contain("Unsupported token type"));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ValidateRequest_MintableWithMaxValidSymbolLength_ShouldNotThrow()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "ABCDEFGHIJ", // Exactly 10 characters
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_MintableWithMaxValidNameLength_ShouldNotThrow()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = new string('A', 50), // Exactly 50 characters
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_MintableWithZeroDecimals_ShouldNotThrow()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 0,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_MintableWithMaxDecimals_ShouldNotThrow()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337,
                Cap = 10000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_MintableWithCapEqualsInitialSupply_ShouldNotThrow()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337,
                Cap = 1000000,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        #endregion
    }
}