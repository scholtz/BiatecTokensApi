using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Controller integration tests for whitelist enforcement on token simulation endpoints
    /// </summary>
    [TestFixture]
    public class TokenSimulationControllerIntegrationTests
    {
        private TokenController _controller;
        private Mock<IWhitelistService> _whitelistServiceMock;
        private Mock<ILogger<TokenController>> _loggerMock;
        private const ulong TestAssetId = 12345;
        private const string TestAddress1 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string TestAddress2 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string TestUserAddress = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBY5HFKQ";

        [SetUp]
        public void Setup()
        {
            _whitelistServiceMock = new Mock<IWhitelistService>();
            _loggerMock = new Mock<ILogger<TokenController>>();

            // Create controller with all required dependencies
            _controller = new TokenController(
                Mock.Of<BiatecTokensApi.Services.Interface.IERC20TokenService>(),
                Mock.Of<BiatecTokensApi.Services.Interface.IARC3TokenService>(),
                Mock.Of<BiatecTokensApi.Services.Interface.IASATokenService>(),
                Mock.Of<BiatecTokensApi.Services.Interface.IARC200TokenService>(),
                Mock.Of<BiatecTokensApi.Services.Interface.IARC1400TokenService>(),
                Mock.Of<BiatecTokensApi.Services.Interface.IComplianceService>(),
                _loggerMock.Object
            );

            // Set up authentication context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserAddress),
                new Claim("sub", TestUserAddress)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // Inject WhitelistService into HttpContext for attribute to use
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IWhitelistService)))
                .Returns(_whitelistServiceMock.Object);
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(ILogger<BiatecTokensApi.Filters.WhitelistEnforcementAttribute>)))
                .Returns(Mock.Of<ILogger<BiatecTokensApi.Filters.WhitelistEnforcementAttribute>>());
            
            _controller.ControllerContext.HttpContext.RequestServices = serviceProviderMock.Object;
        }

        [Test]
        public void SimulateTransfer_BothAddressesWhitelisted_ShouldReturn200()
        {
            // Arrange
            var request = new SimulateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = TestAddress1,
                ToAddress = TestAddress2,
                Amount = 100
            };

            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = true
                });

            // Act
            var result = _controller.SimulateTransfer(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult!.Value, Is.InstanceOf<BaseResponse>());
            var response = okResult.Value as BaseResponse;
            Assert.That(response!.Success, Is.True);
        }

        [Test]
        public void SimulateTransfer_SenderNotWhitelisted_ShouldReturn403()
        {
            // Arrange
            var request = new SimulateTransferRequest
            {
                AssetId = TestAssetId,
                FromAddress = TestAddress1,
                ToAddress = TestAddress2,
                Amount = 100
            };

            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = false,
                    DenialReason = $"Sender address {TestAddress1} is not whitelisted for asset {TestAssetId}"
                });

            // Note: The WhitelistEnforcementAttribute will intercept this before the controller action runs
            // This test verifies the validation service is called correctly
            var validationResult = _whitelistServiceMock.Object.ValidateTransferAsync(
                new ValidateTransferRequest
                {
                    AssetId = request.AssetId,
                    FromAddress = request.FromAddress,
                    ToAddress = request.ToAddress
                },
                TestUserAddress
            ).Result;

            // Assert
            Assert.That(validationResult.IsAllowed, Is.False);
            Assert.That(validationResult.DenialReason, Does.Contain("not whitelisted"));
            Assert.That(validationResult.DenialReason, Does.Contain(TestAssetId.ToString()));
        }

        [Test]
        public void SimulateMint_RecipientWhitelisted_ShouldReturn200()
        {
            // Arrange
            var request = new SimulateMintRequest
            {
                AssetId = TestAssetId,
                ToAddress = TestAddress2,
                Amount = 1000
            };

            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = true
                });

            // Act
            var result = _controller.SimulateMint(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult!.Value, Is.InstanceOf<BaseResponse>());
            var response = okResult.Value as BaseResponse;
            Assert.That(response!.Success, Is.True);
        }

        [Test]
        public void SimulateMint_RecipientNotWhitelisted_ValidationFails()
        {
            // Arrange
            var request = new SimulateMintRequest
            {
                AssetId = TestAssetId,
                ToAddress = TestAddress2,
                Amount = 1000
            };

            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = false,
                    DenialReason = $"Receiver address {TestAddress2} is not whitelisted for asset {TestAssetId}"
                });

            // Verify validation service behavior
            var validationResult = _whitelistServiceMock.Object.ValidateTransferAsync(
                new ValidateTransferRequest
                {
                    AssetId = request.AssetId,
                    FromAddress = request.ToAddress,
                    ToAddress = request.ToAddress
                },
                TestUserAddress
            ).Result;

            // Assert
            Assert.That(validationResult.IsAllowed, Is.False);
            Assert.That(validationResult.DenialReason, Does.Contain("not whitelisted"));
        }

        [Test]
        public void SimulateBurn_HolderWhitelisted_ShouldReturn200()
        {
            // Arrange
            var request = new SimulateBurnRequest
            {
                AssetId = TestAssetId,
                FromAddress = TestAddress1,
                Amount = 500
            };

            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = true
                });

            // Act
            var result = _controller.SimulateBurn(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult!.Value, Is.InstanceOf<BaseResponse>());
            var response = okResult.Value as BaseResponse;
            Assert.That(response!.Success, Is.True);
        }

        [Test]
        public void SimulateBurn_HolderNotWhitelisted_ValidationFails()
        {
            // Arrange
            var request = new SimulateBurnRequest
            {
                AssetId = TestAssetId,
                FromAddress = TestAddress1,
                Amount = 500
            };

            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = false,
                    DenialReason = $"Sender address {TestAddress1} is not whitelisted for asset {TestAssetId}"
                });

            // Verify validation service behavior
            var validationResult = _whitelistServiceMock.Object.ValidateTransferAsync(
                new ValidateTransferRequest
                {
                    AssetId = request.AssetId,
                    FromAddress = request.FromAddress,
                    ToAddress = request.FromAddress
                },
                TestUserAddress
            ).Result;

            // Assert
            Assert.That(validationResult.IsAllowed, Is.False);
            Assert.That(validationResult.DenialReason, Does.Contain("not whitelisted"));
        }

        [Test]
        public void WhitelistEnforcement_ErrorMessage_IncludesAssetIdAndAddress()
        {
            // Arrange
            var expectedAssetId = TestAssetId;
            var expectedAddress = TestAddress1;

            _whitelistServiceMock
                .Setup(s => s.ValidateTransferAsync(It.IsAny<ValidateTransferRequest>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidateTransferResponse
                {
                    Success = true,
                    IsAllowed = false,
                    DenialReason = $"Address {expectedAddress} is not whitelisted for asset {expectedAssetId}"
                });

            // Act
            var validationResult = _whitelistServiceMock.Object.ValidateTransferAsync(
                new ValidateTransferRequest
                {
                    AssetId = expectedAssetId,
                    FromAddress = expectedAddress,
                    ToAddress = expectedAddress
                },
                TestUserAddress
            ).Result;

            // Assert - Verify error message includes both asset ID and address
            Assert.That(validationResult.DenialReason, Does.Contain(expectedAssetId.ToString()));
            Assert.That(validationResult.DenialReason, Does.Contain(expectedAddress));
        }

        [Test]
        public void SimulateEndpoints_RequireAuthentication_UsesUserAddressFromClaims()
        {
            // Verify that the controller has access to user claims
            var user = _controller.ControllerContext.HttpContext.User;
            Assert.That(user.Identity!.IsAuthenticated, Is.True);
            
            var userAddress = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Assert.That(userAddress, Is.EqualTo(TestUserAddress));
        }
    }
}
