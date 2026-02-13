using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class MockKycProviderTests
    {
        private Mock<ILogger<MockKycProvider>> _loggerMock;
        private IOptions<KycConfig> _config;
        private MockKycProvider _provider;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<MockKycProvider>>();
            _config = Options.Create(new KycConfig
            {
                MockAutoApprove = false,
                WebhookSecret = "test-secret"
            });
            _provider = new MockKycProvider(_config, _loggerMock.Object);
        }

        #region StartVerificationAsync Tests

        [Test]
        public async Task StartVerificationAsync_ShouldReturnProviderReferenceId()
        {
            // Arrange
            var userId = "test-user-123";
            var request = new StartKycVerificationRequest
            {
                FullName = "John Doe",
                Country = "US"
            };
            var correlationId = "corr-123";

            // Act
            var result = await _provider.StartVerificationAsync(userId, request, correlationId);

            // Assert
            Assert.That(result.providerReferenceId, Is.Not.Null);
            Assert.That(result.providerReferenceId, Does.StartWith("MOCK-"));
            Assert.That(result.status, Is.EqualTo(KycStatus.Pending));
            Assert.That(result.errorMessage, Is.Null);
        }

        [Test]
        public async Task StartVerificationAsync_WithAutoApprove_ShouldReturnApproved()
        {
            // Arrange
            _config = Options.Create(new KycConfig { MockAutoApprove = true });
            _provider = new MockKycProvider(_config, _loggerMock.Object);
            
            var userId = "test-user-123";
            var request = new StartKycVerificationRequest { FullName = "John Doe" };
            var correlationId = "corr-123";

            // Act
            var result = await _provider.StartVerificationAsync(userId, request, correlationId);

            // Assert
            Assert.That(result.status, Is.EqualTo(KycStatus.Approved));
        }

        #endregion

        #region FetchStatusAsync Tests

        [Test]
        public async Task FetchStatusAsync_ShouldReturnStatus()
        {
            // Arrange
            var providerReferenceId = "MOCK-12345";

            // Act
            var result = await _provider.FetchStatusAsync(providerReferenceId);

            // Assert
            Assert.That(result.status, Is.Not.EqualTo(KycStatus.NotStarted));
            Assert.That(result.reason, Is.Not.Null);
            Assert.That(result.errorMessage, Is.Null);
        }

        [Test]
        public async Task FetchStatusAsync_WithAutoApprove_ShouldReturnApproved()
        {
            // Arrange
            _config = Options.Create(new KycConfig { MockAutoApprove = true });
            _provider = new MockKycProvider(_config, _loggerMock.Object);
            var providerReferenceId = "MOCK-12345";

            // Act
            var result = await _provider.FetchStatusAsync(providerReferenceId);

            // Assert
            Assert.That(result.status, Is.EqualTo(KycStatus.Approved));
        }

        #endregion

        #region ValidateWebhookSignature Tests

        [Test]
        public void ValidateWebhookSignature_WithValidSignature_ShouldReturnTrue()
        {
            // Arrange
            var payload = "{\"test\":\"data\"}";
            var secret = "test-secret";
            
            // Compute expected signature
            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
            var signature = Convert.ToBase64String(hash);

            // Act
            var result = _provider.ValidateWebhookSignature(payload, signature, secret);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void ValidateWebhookSignature_WithInvalidSignature_ShouldReturnFalse()
        {
            // Arrange
            var payload = "{\"test\":\"data\"}";
            var secret = "test-secret";
            var invalidSignature = "invalid-signature";

            // Act
            var result = _provider.ValidateWebhookSignature(payload, invalidSignature, secret);

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region ParseWebhookAsync Tests

        [Test]
        public async Task ParseWebhookAsync_WithApprovedStatus_ShouldReturnApproved()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-12345",
                EventType = "verification.completed",
                Status = "approved",
                Reason = "Verification successful"
            };

            // Act
            var result = await _provider.ParseWebhookAsync(payload);

            // Assert
            Assert.That(result.providerReferenceId, Is.EqualTo(payload.ProviderReferenceId));
            Assert.That(result.status, Is.EqualTo(KycStatus.Approved));
            Assert.That(result.reason, Is.EqualTo(payload.Reason));
        }

        [Test]
        public async Task ParseWebhookAsync_WithRejectedStatus_ShouldReturnRejected()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-12345",
                EventType = "verification.failed",
                Status = "rejected",
                Reason = "Document verification failed"
            };

            // Act
            var result = await _provider.ParseWebhookAsync(payload);

            // Assert
            Assert.That(result.status, Is.EqualTo(KycStatus.Rejected));
        }

        [Test]
        public async Task ParseWebhookAsync_WithPendingStatus_ShouldReturnPending()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-12345",
                EventType = "verification.pending",
                Status = "pending"
            };

            // Act
            var result = await _provider.ParseWebhookAsync(payload);

            // Assert
            Assert.That(result.status, Is.EqualTo(KycStatus.Pending));
        }

        [Test]
        public async Task ParseWebhookAsync_WithNeedsReviewStatus_ShouldReturnNeedsReview()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-12345",
                EventType = "verification.review",
                Status = "needs_review"
            };

            // Act
            var result = await _provider.ParseWebhookAsync(payload);

            // Assert
            Assert.That(result.status, Is.EqualTo(KycStatus.NeedsReview));
        }

        #endregion
    }
}
