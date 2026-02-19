using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
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
        private Mock<ITokenIssuanceRepository> _tokenIssuanceRepositoryMock;
        private Mock<IComplianceRepository> _complianceRepositoryMock;
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
            _tokenIssuanceRepositoryMock = new Mock<ITokenIssuanceRepository>();
            _complianceRepositoryMock = new Mock<IComplianceRepository>();
            
            var deploymentStatusServiceMock = new Mock<IDeploymentStatusService>();
            deploymentStatusServiceMock.Setup(x => x.CreateDeploymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Guid.NewGuid().ToString());
            deploymentStatusServiceMock.Setup(x => x.UpdateDeploymentStatusAsync(
                It.IsAny<string>(), It.IsAny<DeploymentStatus>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>()))
                .ReturnsAsync(true);

            var mockAuthService = Mock.Of<IAuthenticationService>();
            var mockUserRepo = Mock.Of<IUserRepository>();
            var mockHttpContextAccessor = Mock.Of<IHttpContextAccessor>();

            _tokenService = new ERC20TokenService(_blockchainConfigMock.Object, _appConfigMock.Object, _loggerMock.Object, _tokenIssuanceRepositoryMock.Object, _complianceRepositoryMock.Object, deploymentStatusServiceMock.Object, mockAuthService, mockUserRepo, mockHttpContextAccessor);

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
            Assert.That(ex.Message, Does.Contain("Initial supply").And.Contain("positive"));
        }

        [Test]
        public void ValidateRequest_InitialSupplyNegative_ThrowsArgumentException()
        {
            // Arrange
            _validRequest.InitialSupply = -100;

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
            Assert.That(ex.Message, Does.Contain("Initial supply").And.Contain("positive"));
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

        #region ERC20 Preminted Token Validation Tests

        [Test]
        public void ValidateRequest_ValidPremintedRequest_DoesNotThrow()
        {
            // Arrange
            var premintedRequest = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(premintedRequest, TokenType.ARC200_Preminted));
        }

        [Test]
        public void ValidateRequest_PremintedSymbolTooLong_ThrowsArgumentException()
        {
            // Arrange
            var premintedRequest = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "VERYLONGSYM", // 11 characters
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(premintedRequest, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("Symbol").And.Contain("10 characters"));
        }

        [Test]
        public void ValidateRequest_PremintedZeroInitialSupply_ThrowsArgumentException()
        {
            // Arrange
            var premintedRequest = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 0,
                Decimals = 18,
                ChainId = 31337,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(premintedRequest, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("non-negative"));
        }

        [Test]
        public void ValidateRequest_WrongRequestTypeForPreminted_ThrowsArgumentException()
        {
            // Arrange
            var wrongRequest = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TST",
                InitialSupply = 1000,
                Decimals = 18,
                ChainId = 31337,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79",
                Cap = 10000
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                _tokenService.ValidateRequest(wrongRequest, TokenType.ARC200_Preminted));
            Assert.That(ex.Message, Does.Contain("ERC20PremintedTokenDeploymentRequest"));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void ValidateRequest_MaximumDecimals_DoesNotThrow()
        {
            // Arrange
            _validRequest.Decimals = 18; // Maximum

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_ZeroDecimals_DoesNotThrow()
        {
            // Arrange
            _validRequest.Decimals = 0; // Minimum

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_CapEqualsInitialSupply_DoesNotThrow()
        {
            // Arrange
            _validRequest.InitialSupply = 1000000;
            _validRequest.Cap = 1000000; // Same as initial supply

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_MaxSymbolLength_DoesNotThrow()
        {
            // Arrange
            _validRequest.Symbol = "ABCDEFGHIJ"; // Exactly 10 characters

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_MaxNameLength_DoesNotThrow()
        {
            // Arrange
            _validRequest.Name = new string('A', 50); // Exactly 50 characters

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_SingleCharacterSymbol_DoesNotThrow()
        {
            // Arrange
            _validRequest.Symbol = "T"; // Single character

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ValidateRequest_SingleCharacterName_DoesNotThrow()
        {
            // Arrange
            _validRequest.Name = "T"; // Single character

            // Act & Assert
            Assert.DoesNotThrow(() => _tokenService.ValidateRequest(_validRequest, TokenType.ERC20_Mintable));
        }

        #endregion

        #region Unsupported Token Type Tests

        [Test]
        public void ValidateRequest_UnsupportedTokenType_ThrowsArgumentOutOfRangeException()
        {
            // Arrange - use a valid request but wrong token type

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => 
                _tokenService.ValidateRequest(_validRequest, TokenType.ASA_FT));
            Assert.That(ex.Message, Does.Contain("Unsupported token type"));
        }

        #endregion

        #region GetBlockchainConfig Tests

        [Test]
        public void GetBlockchainConfig_ValidChainId_ReturnsConfig()
        {
            // Arrange
            var chainId = 31337;

            // Act
            var method = typeof(ERC20TokenService).GetMethod("GetBlockchainConfig",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = method?.Invoke(_tokenService, new object[] { chainId });

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.InstanceOf<EVMBlockchainConfig>());
            var config = (EVMBlockchainConfig)result!;
            Assert.That(config.ChainId, Is.EqualTo(chainId));
        }

        [Test]
        public void GetBlockchainConfig_InvalidChainId_ThrowsInvalidOperationException()
        {
            // Arrange
            var invalidChainId = 99999;

            // Act & Assert
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            {
                var method = typeof(ERC20TokenService).GetMethod("GetBlockchainConfig",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(_tokenService, new object[] { invalidChainId });
            });
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ex.InnerException?.Message, Does.Contain("No configuration found"));
        }

        #endregion
    }
}