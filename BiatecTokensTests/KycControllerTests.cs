using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BiatecTokensTests
{
    [TestFixture]
    public class KycControllerTests
    {
        private Mock<IKycService> _serviceMock;
        private Mock<ILogger<KycController>> _loggerMock;
        private KycController _controller;
        private const string TestUserId = "test-user-123";

        [SetUp]
        public void Setup()
        {
            _serviceMock = new Mock<IKycService>();
            _loggerMock = new Mock<ILogger<KycController>>();
            _controller = new KycController(_serviceMock.Object, _loggerMock.Object);

            // Mock authenticated user context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserId)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
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

        #region StartVerification Tests

        [Test]
        public async Task StartVerification_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new StartKycVerificationRequest
            {
                FullName = "John Doe",
                Country = "US",
                DateOfBirth = "1990-01-01"
            };

            var expectedResponse = new StartKycVerificationResponse
            {
                Success = true,
                KycId = "kyc-123",
                ProviderReferenceId = "MOCK-12345",
                Status = KycStatus.Pending,
                CorrelationId = "trace-123"
            };

            _serviceMock.Setup(s => s.StartVerificationAsync(TestUserId, request, It.IsAny<string>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.StartVerification(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as StartKycVerificationResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.KycId, Is.EqualTo("kyc-123"));
            Assert.That(response.Status, Is.EqualTo(KycStatus.Pending));
        }

        [Test]
        public async Task StartVerification_AlreadyPending_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new StartKycVerificationRequest { FullName = "John Doe" };

            var expectedResponse = new StartKycVerificationResponse
            {
                Success = false,
                ErrorCode = ErrorCodes.KYC_VERIFICATION_ALREADY_PENDING,
                ErrorMessage = "A KYC verification is already in progress for this user"
            };

            _serviceMock.Setup(s => s.StartVerificationAsync(TestUserId, request, It.IsAny<string>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.StartVerification(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badRequestResult = result as BadRequestObjectResult;
            var response = badRequestResult!.Value as StartKycVerificationResponse;
            Assert.That(response!.Success, Is.False);
            Assert.That(response.ErrorCode, Is.EqualTo(ErrorCodes.KYC_VERIFICATION_ALREADY_PENDING));
        }

        [Test]
        public async Task StartVerification_NoUserId_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new StartKycVerificationRequest { FullName = "John Doe" };
            
            // Create controller with no user claims
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal()
                }
            };

            // Act
            var result = await _controller.StartVerification(request);

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        #endregion

        #region GetStatus Tests

        [Test]
        public async Task GetStatus_WithRecord_ShouldReturnOk()
        {
            // Arrange
            var expectedResponse = new KycStatusResponse
            {
                Success = true,
                KycId = "kyc-123",
                Status = KycStatus.Pending,
                Provider = KycProvider.Mock,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            _serviceMock.Setup(s => s.GetStatusAsync(TestUserId))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetStatus();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as KycStatusResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.KycId, Is.EqualTo("kyc-123"));
            Assert.That(response.Status, Is.EqualTo(KycStatus.Pending));
        }

        [Test]
        public async Task GetStatus_NoRecord_ShouldReturnNotStarted()
        {
            // Arrange
            var expectedResponse = new KycStatusResponse
            {
                Success = true,
                Status = KycStatus.NotStarted
            };

            _serviceMock.Setup(s => s.GetStatusAsync(TestUserId))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetStatus();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as KycStatusResponse;
            Assert.That(response!.Status, Is.EqualTo(KycStatus.NotStarted));
        }

        [Test]
        public async Task GetStatus_NoUserId_ShouldReturnUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal()
                }
            };

            // Act
            var result = await _controller.GetStatus();

            // Assert
            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        #endregion

        #region Webhook Tests

        [Test]
        public async Task Webhook_ValidPayload_ShouldReturnOk()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-12345",
                EventType = "verification.completed",
                Status = "approved",
                Timestamp = DateTime.UtcNow
            };

            _serviceMock.Setup(s => s.HandleWebhookAsync(payload, It.IsAny<string?>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task Webhook_InvalidSignature_ShouldReturnBadRequest()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-12345",
                EventType = "verification.completed",
                Status = "approved",
                Timestamp = DateTime.UtcNow
            };

            _serviceMock.Setup(s => s.HandleWebhookAsync(payload, It.IsAny<string?>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task Webhook_WithSignatureHeader_ShouldPassToService()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-12345",
                EventType = "verification.completed",
                Status = "approved"
            };
            var signature = "test-signature";

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-KYC-Signature"] = signature;
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _serviceMock.Setup(s => s.HandleWebhookAsync(payload, signature))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _serviceMock.Verify(s => s.HandleWebhookAsync(payload, signature), Times.Once);
        }

        #endregion
    }
}
