using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Models.ARC3.Request;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Models.ARC1400.Request;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TokenControllerTests
    {
        private Mock<IERC20TokenService> _tokenServiceMock;
        private Mock<IARC3TokenService> _arc3TokenServiceMock;
        private Mock<IASATokenService> _asaTokenServiceMock;
        private Mock<IARC200TokenService> _arc200TokenServiceMock;
        private Mock<IARC1400TokenService> _arc1400TokenServiceMock;
        private Mock<ILogger<TokenController>> _loggerMock;
        private TokenController _controller;
        private ERC20MintableTokenDeploymentRequest _validDeploymentRequest;

        [SetUp]
        public void Setup()
        {
            _tokenServiceMock = new Mock<IERC20TokenService>();
            _arc3TokenServiceMock = new Mock<IARC3TokenService>();
            _asaTokenServiceMock = new Mock<IASATokenService>();
            _arc200TokenServiceMock = new Mock<IARC200TokenService>();
            _arc1400TokenServiceMock = new Mock<IARC1400TokenService>();
            var complianceServiceMock = new Mock<IComplianceService>();
            _loggerMock = new Mock<ILogger<TokenController>>();
            _controller = new TokenController(_tokenServiceMock.Object, _arc3TokenServiceMock.Object, _asaTokenServiceMock.Object, _arc200TokenServiceMock.Object, _arc1400TokenServiceMock.Object, complianceServiceMock.Object, _loggerMock.Object);

            // Set up a valid token deployment request for testing
            _validDeploymentRequest = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Decimals = 18,
                ChainId = 31337, // Required property
                Cap = 10000000, // Required for mintable tokens
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79" // Sample address
            };

            // Set up controller for testing validation
            _controller.ModelState.Clear();
        }

        #region ERC20 Mintable Token Tests

        [Test]
        public async Task ERC20MintableTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Required");

            // Act
            var result = await _controller.ERC20MintableTokenCreate(_validDeploymentRequest);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task ERC20MintableTokenCreate_ExceptionThrown_ReturnsInternalServerError()
        {
            // Arrange
            _tokenServiceMock.Setup(x => x.DeployERC20TokenAsync(It.IsAny<ERC20MintableTokenDeploymentRequest>(), It.IsAny<TokenType>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.ERC20MintableTokenCreate(_validDeploymentRequest);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region ERC20 Preminted Token Tests

        [Test]
        public async Task ERC20PremintedTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Required");
            var request = new ERC20PremintedTokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TEST",
                InitialSupply = 1000,
                Decimals = 18,
                ChainId = 31337,
                InitialSupplyReceiver = "0x742d35Cc6634C0532925a3b8D162000dDba02C79"
            };

            // Act
            var result = await _controller.ERC20PremnitedTokenCreate(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region ASA Fungible Token Tests

        [Test]
        public async Task ASAFungibleTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Required");
            var request = new ASAFungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TEST",
                TotalSupply = 1000,
                Network = "testnet-v1.0"
            };

            // Act
            var result = await _controller.CreateASAToken(request);

            // Assert
            var actionResult = result.Result;
            Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region ASA NFT Tests

        [Test]
        public async Task ASANonFungibleTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Required");
            var request = new ASANonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "NFT",
                Network = "testnet-v1.0"
            };

            // Act
            var result = await _controller.CreateASANFT(request);

            // Assert
            var actionResult = result.Result;
            Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region ASA Fractional NFT Tests

        [Test]
        public async Task ASAFractionalNonFungibleTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Required");
            var request = new ASAFractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test FNFT",
                UnitName = "FNFT",
                TotalSupply = 100,
                Network = "testnet-v1.0"
            };

            // Act
            var result = await _controller.CreateASAFNFT(request);

            // Assert
            var actionResult = result.Result;
            Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region ARC3 Fungible Token Tests

        [Test]
        public async Task ARC3FungibleTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Required");
            var request = new ARC3FungibleTokenDeploymentRequest
            {
                Name = "Test",
                UnitName = "TEST",
                TotalSupply = 1000,
                Network = "testnet-v1.0",
                Metadata = new BiatecTokensApi.Models.ARC3.ARC3TokenMetadata { Name = "Test" }
            };

            // Act
            var result = await _controller.CreateARC3FungibleToken(request);

            // Assert
            var actionResult = result.Result;
            Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region ARC3 NFT Tests

        [Test]
        public async Task ARC3NonFungibleTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Required");
            var request = new ARC3NonFungibleTokenDeploymentRequest
            {
                Name = "Test NFT",
                UnitName = "NFT",
                Network = "testnet-v1.0",
                Metadata = new BiatecTokensApi.Models.ARC3.ARC3TokenMetadata { Name = "Test NFT" }
            };

            // Act
            var result = await _controller.CreateARC3NFT(request);

            // Assert
            var actionResult = result.Result;
            Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region ARC3 Fractional NFT Tests

        [Test]
        public async Task ARC3FractionalNonFungibleTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Required");
            var request = new ARC3FractionalNonFungibleTokenDeploymentRequest
            {
                Name = "Test FNFT",
                UnitName = "FNFT",
                TotalSupply = 100,
                Network = "testnet-v1.0",
                Metadata = new BiatecTokensApi.Models.ARC3.ARC3TokenMetadata { Name = "Test FNFT" }
            };

            // Act
            var result = await _controller.CreateARC3FractionalNFT(request);

            // Assert
            var actionResult = result.Result;
            Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region ARC200 Mintable Token Tests

        [Test]
        public async Task ARC200MintableTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Required");
            var request = new ARC200MintableTokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TEST",
                InitialSupply = 1000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000,
                Network = "testnet-v1.0"
            };

            // Act
            var result = await _controller.ARC200MintableTokenDeploymentRequest(request);

            // Assert
            var actionResult = result.Result;
            Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region ARC200 Preminted Token Tests

        [Test]
        public async Task ARC200PremintedTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Required");
            var request = new ARC200PremintedTokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TEST",
                InitialSupply = 1000,
                Decimals = 6,
                Network = "testnet-v1.0"
            };

            // Act
            var result = await _controller.CreateARC200Preminted(request);

            // Assert
            var actionResult = result.Result;
            Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion

        #region ARC1400 Mintable Token Tests

        [Test]
        public async Task ARC1400MintableTokenCreate_InvalidModelState_ReturnsBadRequest()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Required");
            var request = new ARC1400MintableTokenDeploymentRequest
            {
                Name = "Test",
                Symbol = "TEST",
                InitialSupply = 1000,
                Decimals = 6,
                InitialSupplyReceiver = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ",
                Cap = 10000,
                Network = "testnet-v1.0"
            };

            // Act
            var result = await _controller.ARC1400MintableTokenDeploymentRequest(request);

            // Assert
            var actionResult = result.Result;
            Assert.That(actionResult, Is.InstanceOf<BadRequestObjectResult>());
        }

        #endregion
    }
}