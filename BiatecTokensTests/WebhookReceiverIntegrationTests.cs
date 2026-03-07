using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Aml;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for webhook receivers in both KycController and AmlController.
    /// Covers valid provider payloads, invalid signatures, unknown reference IDs, and all
    /// KYC status transition payloads per the issue acceptance criteria.
    /// </summary>
    [TestFixture]
    public class WebhookReceiverIntegrationTests
    {
        // =========================================================
        // KYC Webhook
        // =========================================================

        private Mock<IKycService> _kycServiceMock = null!;
        private Mock<IAmlService> _amlServiceMock = null!;
        private Mock<ILogger<KycController>> _kycLoggerMock = null!;
        private Mock<ILogger<AmlController>> _amlLoggerMock = null!;
        private KycController _kycController = null!;
        private AmlController _amlController = null!;

        [SetUp]
        public void Setup()
        {
            _kycServiceMock = new Mock<IKycService>();
            _amlServiceMock = new Mock<IAmlService>();
            _kycLoggerMock = new Mock<ILogger<KycController>>();
            _amlLoggerMock = new Mock<ILogger<AmlController>>();

            _kycController = new KycController(_kycServiceMock.Object, _kycLoggerMock.Object);
            _kycController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            _amlController = new AmlController(_amlServiceMock.Object, _amlLoggerMock.Object);
            _amlController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        // ----------------------------------------------------------
        // KYC webhook — status transitions
        // ----------------------------------------------------------

        [Test]
        public async Task KycWebhook_ApprovedPayload_ShouldReturn200()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-001",
                EventType = "verification.completed",
                Status = "approved",
                Timestamp = DateTime.UtcNow
            };

            _kycServiceMock.Setup(s => s.HandleWebhookAsync(payload, null))
                .ReturnsAsync(true);

            // Act
            var result = await _kycController.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task KycWebhook_RejectedPayload_ShouldReturn200()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-002",
                EventType = "verification.rejected",
                Status = "rejected",
                Reason = "DOCUMENT_EXPIRED",
                Timestamp = DateTime.UtcNow
            };

            _kycServiceMock.Setup(s => s.HandleWebhookAsync(payload, null))
                .ReturnsAsync(true);

            // Act
            var result = await _kycController.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task KycWebhook_PendingPayload_ShouldReturn200()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-003",
                EventType = "verification.created",
                Status = "pending",
                Timestamp = DateTime.UtcNow
            };

            _kycServiceMock.Setup(s => s.HandleWebhookAsync(payload, null))
                .ReturnsAsync(true);

            // Act
            var result = await _kycController.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task KycWebhook_NeedsReviewPayload_ShouldReturn200()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-004",
                EventType = "verification.needs_review",
                Status = "needs_review",
                Timestamp = DateTime.UtcNow
            };

            _kycServiceMock.Setup(s => s.HandleWebhookAsync(payload, null))
                .ReturnsAsync(true);

            // Act
            var result = await _kycController.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task KycWebhook_InvalidSignatureOrRecordNotFound_ShouldReturn400()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "UNKNOWN-999",
                EventType = "verification.completed",
                Status = "approved",
                Timestamp = DateTime.UtcNow
            };

            _kycServiceMock.Setup(s => s.HandleWebhookAsync(payload, It.IsAny<string?>()))
                .ReturnsAsync(false);

            // Act
            var result = await _kycController.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task KycWebhook_WithValidSignatureHeader_ShouldPassSignatureToService()
        {
            // Arrange
            var expectedSignature = "hmac-sha256-test-signature";
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-005",
                EventType = "verification.completed",
                Status = "approved",
                Timestamp = DateTime.UtcNow
            };

            _kycController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _kycController.HttpContext.Request.Headers["X-KYC-Signature"] = expectedSignature;

            _kycServiceMock.Setup(s => s.HandleWebhookAsync(payload, expectedSignature))
                .ReturnsAsync(true);

            // Act
            var result = await _kycController.Webhook(payload);

            // Assert
            _kycServiceMock.Verify(s => s.HandleWebhookAsync(payload, expectedSignature), Times.Once);
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task KycWebhook_ServiceThrows_ShouldReturn500()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-006",
                EventType = "verification.completed",
                Status = "approved",
                Timestamp = DateTime.UtcNow
            };

            _kycServiceMock.Setup(s => s.HandleWebhookAsync(payload, It.IsAny<string?>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _kycController.Webhook(payload);

            // Assert
            var statusResult = result as ObjectResult;
            Assert.That(statusResult, Is.Not.Null);
            Assert.That(statusResult!.StatusCode, Is.EqualTo(500));
        }

        // ----------------------------------------------------------
        // AML webhook — alert types
        // ----------------------------------------------------------

        [Test]
        public async Task AmlWebhook_ClearedAlert_ShouldReturn200()
        {
            // Arrange
            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = "AML-MOCK-001",
                AlertType = "CLEARED",
                Status = "CLEARED",
                RiskLevel = "LOW",
                Timestamp = DateTime.UtcNow
            };

            _amlServiceMock.Setup(s => s.HandleWebhookAsync(payload, null))
                .ReturnsAsync(true);

            // Act
            var result = await _amlController.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task AmlWebhook_SanctionsMatchAlert_ShouldReturn200()
        {
            // Arrange
            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = "AML-MOCK-002",
                AlertType = "SANCTIONS_MATCH",
                Status = "SANCTIONS_MATCH",
                RiskLevel = "HIGH",
                Timestamp = DateTime.UtcNow,
                ReasonCode = "OFAC_SDN"
            };

            _amlServiceMock.Setup(s => s.HandleWebhookAsync(payload, null))
                .ReturnsAsync(true);

            // Act
            var result = await _amlController.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task AmlWebhook_PepMatchAlert_ShouldReturn200()
        {
            // Arrange
            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = "AML-MOCK-003",
                AlertType = "PEP_MATCH",
                Status = "PEP_MATCH",
                RiskLevel = "HIGH",
                Timestamp = DateTime.UtcNow,
                ReasonCode = "PEP_DATABASE"
            };

            _amlServiceMock.Setup(s => s.HandleWebhookAsync(payload, null))
                .ReturnsAsync(true);

            // Act
            var result = await _amlController.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task AmlWebhook_ReviewRequiredAlert_ShouldReturn200()
        {
            // Arrange
            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = "AML-MOCK-004",
                AlertType = "REVIEW_REQUIRED",
                Status = "NEEDS_REVIEW",
                RiskLevel = "MEDIUM",
                Timestamp = DateTime.UtcNow
            };

            _amlServiceMock.Setup(s => s.HandleWebhookAsync(payload, null))
                .ReturnsAsync(true);

            // Act
            var result = await _amlController.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task AmlWebhook_UnknownProviderRef_ShouldReturn400()
        {
            // Arrange
            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = "AML-UNKNOWN-999",
                AlertType = "SANCTIONS_MATCH",
                Status = "SANCTIONS_MATCH",
                RiskLevel = "HIGH",
                Timestamp = DateTime.UtcNow
            };

            _amlServiceMock.Setup(s => s.HandleWebhookAsync(payload, It.IsAny<string?>()))
                .ReturnsAsync(false);

            // Act
            var result = await _amlController.Webhook(payload);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task AmlWebhook_WithSignatureHeader_ShouldPassSignatureToService()
        {
            // Arrange
            var expectedSignature = "aml-hmac-sig-test";
            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = "AML-MOCK-005",
                AlertType = "CLEARED",
                Status = "CLEARED",
                RiskLevel = "LOW",
                Timestamp = DateTime.UtcNow
            };

            _amlController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            _amlController.HttpContext.Request.Headers["X-AML-Signature"] = expectedSignature;

            _amlServiceMock.Setup(s => s.HandleWebhookAsync(payload, expectedSignature))
                .ReturnsAsync(true);

            // Act
            var result = await _amlController.Webhook(payload);

            // Assert
            _amlServiceMock.Verify(s => s.HandleWebhookAsync(payload, expectedSignature), Times.Once);
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task AmlWebhook_ServiceThrows_ShouldReturn500()
        {
            // Arrange
            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = "AML-MOCK-006",
                AlertType = "CLEARED",
                Status = "CLEARED",
                RiskLevel = "LOW",
                Timestamp = DateTime.UtcNow
            };

            _amlServiceMock.Setup(s => s.HandleWebhookAsync(payload, It.IsAny<string?>()))
                .ThrowsAsync(new Exception("Unexpected webhook error"));

            // Act
            var result = await _amlController.Webhook(payload);

            // Assert
            var statusResult = result as ObjectResult;
            Assert.That(statusResult, Is.Not.Null);
            Assert.That(statusResult!.StatusCode, Is.EqualTo(500));
        }

        // ----------------------------------------------------------
        // KYC webhook — HMAC signature validation via MockKycProvider
        // ----------------------------------------------------------

        [Test]
        public void KycWebhookSignature_ValidHmac_ShouldReturnTrue()
        {
            // Arrange
            var secret = "test-webhook-secret-key";
            var payload = "{\"providerReferenceId\":\"MOCK-001\",\"eventType\":\"verified\",\"status\":\"approved\"}";

            using var hmac = new System.Security.Cryptography.HMACSHA256(
                System.Text.Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
            var validSignature = Convert.ToBase64String(hash);

            var config = Microsoft.Extensions.Options.Options.Create(new BiatecTokensApi.Configuration.KycConfig
            {
                WebhookSecret = secret
            });
            var mockProvider = new BiatecTokensApi.Services.MockKycProvider(
                config,
                new Mock<ILogger<BiatecTokensApi.Services.MockKycProvider>>().Object);

            // Act
            var result = mockProvider.ValidateWebhookSignature(payload, validSignature, secret);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void KycWebhookSignature_InvalidHmac_ShouldReturnFalse()
        {
            // Arrange
            var secret = "test-webhook-secret-key";
            var payload = "{\"providerReferenceId\":\"MOCK-001\",\"eventType\":\"verified\",\"status\":\"approved\"}";
            var invalidSignature = "invalid-signature-value";

            var config = Microsoft.Extensions.Options.Options.Create(new BiatecTokensApi.Configuration.KycConfig
            {
                WebhookSecret = secret
            });
            var mockProvider = new BiatecTokensApi.Services.MockKycProvider(
                config,
                new Mock<ILogger<BiatecTokensApi.Services.MockKycProvider>>().Object);

            // Act
            var result = mockProvider.ValidateWebhookSignature(payload, invalidSignature, secret);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void KycWebhookSignature_TamperedPayload_ShouldReturnFalse()
        {
            // Arrange
            var secret = "test-webhook-secret-key";
            var originalPayload = "{\"providerReferenceId\":\"MOCK-001\",\"status\":\"approved\"}";
            var tamperedPayload = "{\"providerReferenceId\":\"MOCK-001\",\"status\":\"rejected\"}";

            using var hmac = new System.Security.Cryptography.HMACSHA256(
                System.Text.Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(originalPayload));
            var originalSignature = Convert.ToBase64String(hash);

            var config = Microsoft.Extensions.Options.Options.Create(new BiatecTokensApi.Configuration.KycConfig
            {
                WebhookSecret = secret
            });
            var mockProvider = new BiatecTokensApi.Services.MockKycProvider(
                config,
                new Mock<ILogger<BiatecTokensApi.Services.MockKycProvider>>().Object);

            // Act — verify tampered payload against original signature
            var result = mockProvider.ValidateWebhookSignature(tamperedPayload, originalSignature, secret);

            // Assert
            Assert.That(result, Is.False);
        }
    }
}
