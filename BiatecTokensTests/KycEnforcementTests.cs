using BiatecTokensApi.Configuration;
using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Models.ERC20.Response;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BiatecTokensTests
{
    [TestFixture]
    public class KycEnforcementTests
    {
        private Mock<IERC20TokenService> _erc20ServiceMock;
        private Mock<IARC3TokenService> _arc3ServiceMock;
        private Mock<IASATokenService> _asaServiceMock;
        private Mock<IARC200TokenService> _arc200ServiceMock;
        private Mock<IARC1400TokenService> _arc1400ServiceMock;
        private Mock<IComplianceService> _complianceServiceMock;
        private Mock<IKycService> _kycServiceMock;
        private Mock<ILogger<TokenController>> _loggerMock;
        private Mock<IWebHostEnvironment> _envMock;
        private TokenController _controller;
        private const string TestUserId = "test-user-123";

        [SetUp]
        public void Setup()
        {
            _erc20ServiceMock = new Mock<IERC20TokenService>();
            _arc3ServiceMock = new Mock<IARC3TokenService>();
            _asaServiceMock = new Mock<IASATokenService>();
            _arc200ServiceMock = new Mock<IARC200TokenService>();
            _arc1400ServiceMock = new Mock<IARC1400TokenService>();
            _complianceServiceMock = new Mock<IComplianceService>();
            _kycServiceMock = new Mock<IKycService>();
            _loggerMock = new Mock<ILogger<TokenController>>();
            _envMock = new Mock<IWebHostEnvironment>();

            _controller = new TokenController(
                _erc20ServiceMock.Object,
                _arc3ServiceMock.Object,
                _asaServiceMock.Object,
                _arc200ServiceMock.Object,
                _arc1400ServiceMock.Object,
                _complianceServiceMock.Object,
                _kycServiceMock.Object,
                _loggerMock.Object,
                _envMock.Object);

            // Mock authenticated user context with JWT
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId)
            };
            var identity = new ClaimsIdentity(claims, "Bearer");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = claimsPrincipal,
                    TraceIdentifier = "trace-123"
                }
            };
        }

        #region KYC Enforcement - Verified User Tests

        [Test]
        public async Task ERC20MintableTokenCreate_VerifiedUser_ShouldAllowDeployment()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                InitialSupply = 1000000,
                Cap = 10000000,
                Decimals = 18,
                ChainId = 8453
            };

            // KYC enforcement enabled and user is verified
            _kycServiceMock.Setup(s => s.IsEnforcementEnabled()).Returns(true);
            _kycServiceMock.Setup(s => s.IsUserVerifiedAsync(TestUserId)).ReturnsAsync(true);

            var expectedResponse = new ERC20TokenDeploymentResponse
            {
                Success = true,
                ContractAddress = "0x1234567890123456789012345678901234567890",
                TransactionHash = "0xabcdef"
            };

            _erc20ServiceMock.Setup(s => s.DeployERC20TokenAsync(
                    request,
                    It.IsAny<TokenType>(),
                    TestUserId))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ERC20MintableTokenCreate(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as ERC20TokenDeploymentResponse;
            Assert.That(response!.Success, Is.True);
            _erc20ServiceMock.Verify(s => s.DeployERC20TokenAsync(It.IsAny<ERC20MintableTokenDeploymentRequest>(), It.IsAny<TokenType>(), TestUserId), Times.Once);
        }

        #endregion

        #region KYC Enforcement - Not Started Tests

        [Test]
        public async Task ERC20MintableTokenCreate_KycNotStarted_ShouldBlockDeployment()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                InitialSupply = 1000000,
                Cap = 10000000,
                ChainId = 8453,
                Symbol = "TEST"
            };

            // KYC enforcement enabled but user not verified
            _kycServiceMock.Setup(s => s.IsEnforcementEnabled()).Returns(true);
            _kycServiceMock.Setup(s => s.IsUserVerifiedAsync(TestUserId)).ReturnsAsync(false);
            _kycServiceMock.Setup(s => s.GetStatusAsync(TestUserId))
                .ReturnsAsync(new KycStatusResponse
                {
                    Success = true,
                    Status = KycStatus.NotStarted
                });

            // Act
            var result = await _controller.ERC20MintableTokenCreate(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            _erc20ServiceMock.Verify(s => s.DeployERC20TokenAsync(It.IsAny<ERC20MintableTokenDeploymentRequest>(), It.IsAny<TokenType>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region KYC Enforcement - Pending Tests

        [Test]
        public async Task ERC20MintableTokenCreate_KycPending_ShouldBlockDeployment()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                InitialSupply = 1000000,
                Cap = 10000000,
                ChainId = 8453,
                Symbol = "TEST"
            };

            _kycServiceMock.Setup(s => s.IsEnforcementEnabled()).Returns(true);
            _kycServiceMock.Setup(s => s.IsUserVerifiedAsync(TestUserId)).ReturnsAsync(false);
            _kycServiceMock.Setup(s => s.GetStatusAsync(TestUserId))
                .ReturnsAsync(new KycStatusResponse
                {
                    Success = true,
                    Status = KycStatus.Pending
                });

            // Act
            var result = await _controller.ERC20MintableTokenCreate(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        #endregion

        #region KYC Enforcement - Rejected Tests

        [Test]
        public async Task ERC20MintableTokenCreate_KycRejected_ShouldBlockDeployment()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                InitialSupply = 1000000,
                Cap = 10000000,
                ChainId = 8453,
                Symbol = "TEST"
            };

            _kycServiceMock.Setup(s => s.IsEnforcementEnabled()).Returns(true);
            _kycServiceMock.Setup(s => s.IsUserVerifiedAsync(TestUserId)).ReturnsAsync(false);
            _kycServiceMock.Setup(s => s.GetStatusAsync(TestUserId))
                .ReturnsAsync(new KycStatusResponse
                {
                    Success = true,
                    Status = KycStatus.Rejected,
                    Reason = "Document verification failed"
                });

            // Act
            var result = await _controller.ERC20MintableTokenCreate(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        #endregion

        #region KYC Enforcement - Expired Tests

        [Test]
        public async Task ERC20MintableTokenCreate_KycExpired_ShouldBlockDeployment()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                InitialSupply = 1000000,
                Cap = 10000000,
                ChainId = 8453,
                Symbol = "TEST"
            };

            _kycServiceMock.Setup(s => s.IsEnforcementEnabled()).Returns(true);
            _kycServiceMock.Setup(s => s.IsUserVerifiedAsync(TestUserId)).ReturnsAsync(false);
            _kycServiceMock.Setup(s => s.GetStatusAsync(TestUserId))
                .ReturnsAsync(new KycStatusResponse
                {
                    Success = true,
                    Status = KycStatus.Expired
                });

            // Act
            var result = await _controller.ERC20MintableTokenCreate(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        }

        #endregion

        #region KYC Enforcement - Disabled Tests

        [Test]
        public async Task ERC20MintableTokenCreate_KycEnforcementDisabled_ShouldAllowDeployment()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                InitialSupply = 1000000,
                Cap = 10000000,
                ChainId = 8453,
                Symbol = "TEST",
                Decimals = 18,
            };

            // KYC enforcement disabled
            _kycServiceMock.Setup(s => s.IsEnforcementEnabled()).Returns(false);

            var expectedResponse = new ERC20TokenDeploymentResponse
            {
                Success = true,
                ContractAddress = "0x1234567890123456789012345678901234567890",
                TransactionHash = "0xabcdef"
            };

            _erc20ServiceMock.Setup(s => s.DeployERC20TokenAsync(
                    request,
                    It.IsAny<TokenType>(),
                    TestUserId))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ERC20MintableTokenCreate(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _erc20ServiceMock.Verify(s => s.DeployERC20TokenAsync(It.IsAny<ERC20MintableTokenDeploymentRequest>(), It.IsAny<TokenType>(), TestUserId), Times.Once);
        }

        #endregion

        #region KYC Enforcement - Non-JWT Authentication Tests

        [Test]
        public async Task ERC20MintableTokenCreate_ARC0014Auth_ShouldSkipKycCheck()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                InitialSupply = 1000000,
                Cap = 10000000,
                ChainId = 8453,
                Symbol = "TEST",
                Decimals = 18,
            };

            // Setup ARC-0014 authentication (no JWT userId claim)
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(), // No claims
                    TraceIdentifier = "trace-123"
                }
            };

            // KYC enforcement enabled but should be skipped for non-JWT auth
            _kycServiceMock.Setup(s => s.IsEnforcementEnabled()).Returns(true);

            var expectedResponse = new ERC20TokenDeploymentResponse
            {
                Success = true,
                ContractAddress = "0x1234567890123456789012345678901234567890",
                TransactionHash = "0xabcdef"
            };

            _erc20ServiceMock.Setup(s => s.DeployERC20TokenAsync(
                    request,
                    It.IsAny<TokenType>(),
                    null))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ERC20MintableTokenCreate(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            // Verify KYC check was not performed
            _kycServiceMock.Verify(s => s.IsUserVerifiedAsync(It.IsAny<string>()), Times.Never);
            _erc20ServiceMock.Verify(s => s.DeployERC20TokenAsync(It.IsAny<ERC20MintableTokenDeploymentRequest>(), It.IsAny<TokenType>(), null), Times.Once);
        }

        #endregion
    }
}
