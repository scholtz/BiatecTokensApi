using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC1400.Request;
using BiatecTokensApi.Models.ARC1400.Response;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Models.ARC200.Response;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ARC3.Response;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Models.ERC20.Response;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TokenControllerTests
    {
        private Mock<IERC20TokenService> _erc20ServiceMock;
        private Mock<IARC3TokenService> _arc3ServiceMock;
        private Mock<IASATokenService> _asaServiceMock;
        private Mock<IARC200TokenService> _arc200ServiceMock;
        private Mock<IARC1400TokenService> _arc1400ServiceMock;
        private Mock<ILogger<TokenController>> _loggerMock;
        private TokenController _controller;

        [SetUp]
        public void Setup()
        {
            _erc20ServiceMock = new Mock<IERC20TokenService>();
            _arc3ServiceMock = new Mock<IARC3TokenService>();
            _asaServiceMock = new Mock<IASATokenService>();
            _arc200ServiceMock = new Mock<IARC200TokenService>();
            _arc1400ServiceMock = new Mock<IARC1400TokenService>();
            _loggerMock = new Mock<ILogger<TokenController>>();
            
            _controller = new TokenController(
                _erc20ServiceMock.Object,
                _arc3ServiceMock.Object,
                _asaServiceMock.Object,
                _arc200ServiceMock.Object,
                _arc1400ServiceMock.Object,
                _loggerMock.Object);

            _controller.ModelState.Clear();
        }

        #region ERC20 Mintable Token Tests

        [Test]
        public async Task ERC20MintableTokenCreate_ValidRequest_ReturnsOkResult()
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

            var response = new ERC20TokenDeploymentResponse
            {
                Success = true,
                ContractAddress = "0x1234567890123456789012345678901234567890",
                TransactionHash = "0xabcdef"
            };

            _erc20ServiceMock.Setup(s => s.DeployERC20TokenAsync(It.IsAny<ERC20TokenDeploymentRequest>(), TokenType.ERC20_Mintable))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ERC20MintableTokenCreate(request);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.That(okResult.Value, Is.EqualTo(response));
            _erc20ServiceMock.Verify(s => s.DeployERC20TokenAsync(request, TokenType.ERC20_Mintable), Times.Once);
        }

        [Test]
        public async Task ERC20MintableTokenCreate_ServiceFails_ReturnsInternalServerError()
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

            var response = new ERC20TokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "Deployment failed",
                ContractAddress = "",
                TransactionHash = ""
            };

            _erc20ServiceMock.Setup(s => s.DeployERC20TokenAsync(It.IsAny<ERC20TokenDeploymentRequest>(), TokenType.ERC20_Mintable))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ERC20MintableTokenCreate(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task ERC20MintableTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest();
            _controller.ModelState.AddModelError("Name", "Name is required");

            // Act
            var result = await _controller.ERC20MintableTokenCreate(request);

            // Assert
            Assert.IsInstanceOf<BadRequestObjectResult>(result);
        }

        [Test]
        public async Task ERC20MintableTokenCreate_ExceptionThrown_ReturnsInternalServerError()
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

            _erc20ServiceMock.Setup(s => s.DeployERC20TokenAsync(It.IsAny<ERC20TokenDeploymentRequest>(), TokenType.ERC20_Mintable))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.ERC20MintableTokenCreate(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region ERC20 Preminted Token Tests

        [Test]
        public async Task ERC20PremintedTokenCreate_ValidRequest_ReturnsOkResult()
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

            var response = new ERC20TokenDeploymentResponse
            {
                Success = true,
                ContractAddress = "0x1234567890123456789012345678901234567890",
                TransactionHash = "0xabcdef"
            };

            _erc20ServiceMock.Setup(s => s.DeployERC20TokenAsync(It.IsAny<ERC20TokenDeploymentRequest>(), TokenType.ERC20_Preminted))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ERC20PremnitedTokenCreate(request);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            Assert.That(okResult.Value, Is.EqualTo(response));
        }

        [Test]
        public async Task ERC20PremintedTokenCreate_ServiceFails_ReturnsInternalServerError()
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

            var response = new ERC20TokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "Deployment failed",
                ContractAddress = "",
                TransactionHash = ""
            };

            _erc20ServiceMock.Setup(s => s.DeployERC20TokenAsync(It.IsAny<ERC20TokenDeploymentRequest>(), TokenType.ERC20_Preminted))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ERC20PremnitedTokenCreate(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result);
            var objectResult = result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task ERC20PremintedTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var request = new ERC20PremintedTokenDeploymentRequest();
            _controller.ModelState.AddModelError("Name", "Name is required");

            // Act
            var result = await _controller.ERC20PremnitedTokenCreate(request);

            // Assert
            Assert.IsInstanceOf<BadRequestObjectResult>(result);
        }

        #endregion

        #region ASA FT Tests

        [Test]
        public async Task CreateASAToken_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ASA",
                UnitName = "TASA",
                TotalSupply = 1000000,
                Decimals = 6
            };

            var response = new ASATokenDeploymentResponse
            {
                Success = true,
                AssetId = 12345,
                TransactionId = "TXID123"
            };

            _asaServiceMock.Setup(s => s.CreateASATokenAsync(It.IsAny<ASABaseTokenDeploymentRequest>(), TokenType.ASA_FT))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateASAToken(request);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result.Result);
            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult.Value, Is.EqualTo(response));
        }

        [Test]
        public async Task CreateASAToken_ServiceFails_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ASA",
                UnitName = "TASA",
                TotalSupply = 1000000,
                Decimals = 6
            };

            var response = new ASATokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "Creation failed"
            };

            _asaServiceMock.Setup(s => s.CreateASATokenAsync(It.IsAny<ASABaseTokenDeploymentRequest>(), TokenType.ASA_FT))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateASAToken(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result.Result);
            var objectResult = result.Result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task CreateASAToken_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ASA",
                UnitName = "TASA",
                TotalSupply = 1000000,
                Decimals = 6
            };
            _controller.ModelState.AddModelError("AssetName", "AssetName is required");

            // Act
            var result = await _controller.CreateASAToken(request);

            // Assert
            Assert.IsInstanceOf<BadRequestObjectResult>(result.Result);
        }

        #endregion

        #region ASA NFT Tests

        [Test]
        public async Task CreateASANFT_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                AssetName = "Test NFT",
                UnitName = "TNFT"
            };

            var response = new ASATokenDeploymentResponse
            {
                Success = true,
                AssetId = 12345,
                TransactionId = "TXID123"
            };

            _asaServiceMock.Setup(s => s.CreateASATokenAsync(It.IsAny<ASABaseTokenDeploymentRequest>(), TokenType.ASA_NFT))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateASANFT(request);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result.Result);
        }

        [Test]
        public async Task CreateASANFT_ServiceFails_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                AssetName = "Test NFT",
                UnitName = "TNFT"
            };

            var response = new ASATokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "NFT creation failed"
            };

            _asaServiceMock.Setup(s => s.CreateASATokenAsync(It.IsAny<ASABaseTokenDeploymentRequest>(), TokenType.ASA_NFT))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateASANFT(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result.Result);
            var objectResult = result.Result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region ASA FNFT Tests

        [Test]
        public async Task CreateASAFNFT_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test FNFT",
                UnitName = "TFNFT",
                TotalSupply = 100
            };

            var response = new ASATokenDeploymentResponse
            {
                Success = true,
                AssetId = 12345,
                TransactionId = "TXID123"
            };

            _asaServiceMock.Setup(s => s.CreateASATokenAsync(It.IsAny<ASABaseTokenDeploymentRequest>(), TokenType.ASA_FNFT))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateASAFNFT(request);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result.Result);
        }

        [Test]
        public async Task CreateASAFNFT_ServiceFails_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test FNFT",
                UnitName = "TFNFT",
                TotalSupply = 100
            };

            var response = new ASATokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "FNFT creation failed"
            };

            _asaServiceMock.Setup(s => s.CreateASATokenAsync(It.IsAny<ASABaseTokenDeploymentRequest>(), TokenType.ASA_FNFT))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateASAFNFT(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result.Result);
            var objectResult = result.Result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region ARC3 FT Tests

        [Test]
        public async Task CreateARC3FungibleToken_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ARC3FungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC3",
                UnitName = "TARC3",
                TotalSupply = 1000000,
                Metadata = new BiatecTokensApi.Models.ARC3.ARC3TokenMetadata 
                { 
                    Name = "Test Metadata" 
                }
            };

            var response = new ARC3TokenDeploymentResponse
            {
                Success = true,
                AssetId = 12345,
                TransactionId = "TXID123"
            };

            _arc3ServiceMock.Setup(s => s.CreateARC3TokenAsync(It.IsAny<IARC3TokenDeploymentRequest>(), TokenType.ARC3_FT))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateARC3FungibleToken(request);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result.Result);
        }

        [Test]
        public async Task CreateARC3FungibleToken_ServiceFails_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ARC3FungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC3",
                UnitName = "TARC3",
                TotalSupply = 1000000,
                Metadata = new BiatecTokensApi.Models.ARC3.ARC3TokenMetadata 
                { 
                    Name = "Test Metadata" 
                }
            };

            var response = new ARC3TokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "ARC3 creation failed"
            };

            _arc3ServiceMock.Setup(s => s.CreateARC3TokenAsync(It.IsAny<IARC3TokenDeploymentRequest>(), TokenType.ARC3_FT))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateARC3FungibleToken(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result.Result);
            var objectResult = result.Result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region ARC3 NFT Tests

        [Test]
        public async Task CreateARC3NFT_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ARC3NonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                AssetName = "Test ARC3 NFT",
                UnitName = "ARC3NFT"
            };

            var response = new ARC3TokenDeploymentResponse
            {
                Success = true,
                AssetId = 12345,
                TransactionId = "TXID123"
            };

            _arc3ServiceMock.Setup(s => s.CreateARC3TokenAsync(It.IsAny<IARC3TokenDeploymentRequest>(), TokenType.ARC3_NFT))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateARC3NFT(request);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result.Result);
        }

        [Test]
        public async Task CreateARC3NFT_ServiceFails_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ARC3NonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                AssetName = "Test ARC3 NFT",
                UnitName = "ARC3NFT"
            };

            var response = new ARC3TokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "ARC3 NFT creation failed"
            };

            _arc3ServiceMock.Setup(s => s.CreateARC3TokenAsync(It.IsAny<IARC3TokenDeploymentRequest>(), TokenType.ARC3_NFT))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateARC3NFT(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result.Result);
            var objectResult = result.Result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region ARC3 FNFT Tests

        [Test]
        public async Task CreateARC3FractionalNFT_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC3 FNFT",
                UnitName = "ARC3FNFT",
                TotalSupply = 100,
                Metadata = new BiatecTokensApi.Models.ARC3.ARC3TokenMetadata 
                { 
                    Name = "Test Metadata" 
                }
            };

            var response = new ARC3TokenDeploymentResponse
            {
                Success = true,
                AssetId = 12345,
                TransactionId = "TXID123"
            };

            _arc3ServiceMock.Setup(s => s.CreateARC3TokenAsync(It.IsAny<IARC3TokenDeploymentRequest>(), TokenType.ARC3_FNFT))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateARC3FractionalNFT(request);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result.Result);
        }

        [Test]
        public async Task CreateARC3FractionalNFT_ExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC3 FNFT",
                UnitName = "ARC3FNFT",
                TotalSupply = 100,
                Metadata = new BiatecTokensApi.Models.ARC3.ARC3TokenMetadata 
                { 
                    Name = "Test Metadata" 
                }
            };

            _arc3ServiceMock.Setup(s => s.CreateARC3TokenAsync(It.IsAny<IARC3TokenDeploymentRequest>(), TokenType.ARC3_FNFT))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.CreateARC3FractionalNFT(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result.Result);
            var objectResult = result.Result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region ARC200 Mintable Tests

        [Test]
        public async Task ARC200MintableTokenDeploymentRequest_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200",
                Symbol = "TARC200",
                Decimals = 6
            };

            var response = new ARC200TokenDeploymentResponse
            {
                Success = true,
                AssetId = 12345,
                TransactionId = "TXID123"
            };

            _arc200ServiceMock.Setup(s => s.CreateARC200TokenAsync(It.IsAny<ARC200TokenDeploymentRequest>(), TokenType.ARC200_Mintable))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ARC200MintableTokenDeploymentRequest(request);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result.Result);
        }

        [Test]
        public async Task ARC200MintableTokenDeploymentRequest_ServiceFails_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200",
                Symbol = "TARC200",
                Decimals = 6
            };

            var response = new ARC200TokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "ARC200 creation failed"
            };

            _arc200ServiceMock.Setup(s => s.CreateARC200TokenAsync(It.IsAny<ARC200TokenDeploymentRequest>(), TokenType.ARC200_Mintable))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ARC200MintableTokenDeploymentRequest(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result.Result);
            var objectResult = result.Result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region ARC200 Preminted Tests

        [Test]
        public async Task CreateARC200Preminted_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200 Preminted",
                Symbol = "TARC200P",
                Decimals = 6,
                InitialSupply = 1000000
            };

            var response = new ARC200TokenDeploymentResponse
            {
                Success = true,
                AssetId = 12345,
                TransactionId = "TXID123"
            };

            _arc200ServiceMock.Setup(s => s.CreateARC200TokenAsync(It.IsAny<ARC200TokenDeploymentRequest>(), TokenType.ARC200_Preminted))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateARC200Preminted(request);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result.Result);
        }

        [Test]
        public async Task CreateARC200Preminted_ServiceFails_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC200 Preminted",
                Symbol = "TARC200P",
                Decimals = 6,
                InitialSupply = 1000000
            };

            var response = new ARC200TokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "ARC200 preminted creation failed"
            };

            _arc200ServiceMock.Setup(s => s.CreateARC200TokenAsync(It.IsAny<ARC200TokenDeploymentRequest>(), TokenType.ARC200_Preminted))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.CreateARC200Preminted(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result.Result);
            var objectResult = result.Result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region ARC1400 Tests

        [Test]
        public async Task ARC1400MintableTokenDeploymentRequest_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC1400",
                Symbol = "TARC1400",
                InitialSupply = 1000000,
                Cap = 10000000
            };

            var response = new ARC1400TokenDeploymentResponse
            {
                Success = true,
                AssetId = 12345,
                TransactionId = "TXID123"
            };

            _arc1400ServiceMock.Setup(s => s.CreateARC1400TokenAsync(It.IsAny<ARC1400TokenDeploymentRequest>(), TokenType.ARC1400_Mintable))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ARC1400MintableTokenDeploymentRequest(request);

            // Assert
            Assert.IsInstanceOf<OkObjectResult>(result.Result);
        }

        [Test]
        public async Task ARC1400MintableTokenDeploymentRequest_ServiceFails_ReturnsInternalServerError()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC1400",
                Symbol = "TARC1400",
                InitialSupply = 1000000,
                Cap = 10000000
            };

            var response = new ARC1400TokenDeploymentResponse
            {
                Success = false,
                ErrorMessage = "ARC1400 creation failed"
            };

            _arc1400ServiceMock.Setup(s => s.CreateARC1400TokenAsync(It.IsAny<ARC1400TokenDeploymentRequest>(), TokenType.ARC1400_Mintable))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.ARC1400MintableTokenDeploymentRequest(request);

            // Assert
            Assert.IsInstanceOf<ObjectResult>(result.Result);
            var objectResult = result.Result as ObjectResult;
            Assert.That(objectResult.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task ARC1400MintableTokenDeploymentRequest_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Network = "testnet",
                Name = "Test ARC1400",
                Symbol = "TARC1400",
                InitialSupply = 1000000,
                Cap = 10000000
            };
            _controller.ModelState.AddModelError("Name", "Name is required");

            // Act
            var result = await _controller.ARC1400MintableTokenDeploymentRequest(request);

            // Assert
            Assert.IsInstanceOf<BadRequestObjectResult>(result.Result);
        }

        #endregion
    }
}