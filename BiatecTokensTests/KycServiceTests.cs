using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class KycServiceTests
    {
        private Mock<IKycRepository> _repositoryMock;
        private Mock<IKycProvider> _providerMock;
        private Mock<ILogger<KycService>> _loggerMock;
        private IOptions<KycConfig> _config;
        private KycService _service;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IKycRepository>();
            _providerMock = new Mock<IKycProvider>();
            _loggerMock = new Mock<ILogger<KycService>>();
            _config = Options.Create(new KycConfig
            {
                EnforcementEnabled = true,
                Provider = "Mock",
                ExpirationDays = 365
            });
            _service = new KycService(
                _repositoryMock.Object,
                _providerMock.Object,
                _config,
                _loggerMock.Object);
        }

        #region StartVerificationAsync Tests

        [Test]
        public async Task StartVerificationAsync_NewUser_ShouldCreateRecord()
        {
            // Arrange
            var userId = "test-user-123";
            var request = new StartKycVerificationRequest
            {
                FullName = "John Doe",
                Country = "US"
            };
            var correlationId = "corr-123";
            var providerRefId = "MOCK-12345";

            _repositoryMock.Setup(r => r.GetKycRecordByUserIdAsync(userId))
                .ReturnsAsync((KycRecord?)null);
            
            _providerMock.Setup(p => p.StartVerificationAsync(userId, request, correlationId))
                .ReturnsAsync((providerRefId, KycStatus.Pending, (string?)null));

            _repositoryMock.Setup(r => r.CreateKycRecordAsync(It.IsAny<KycRecord>()))
                .ReturnsAsync((KycRecord r) => r);

            // Act
            var result = await _service.StartVerificationAsync(userId, request, correlationId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.ProviderReferenceId, Is.EqualTo(providerRefId));
            Assert.That(result.Status, Is.EqualTo(KycStatus.Pending));
            _repositoryMock.Verify(r => r.CreateKycRecordAsync(It.IsAny<KycRecord>()), Times.Once);
        }

        [Test]
        public async Task StartVerificationAsync_UserWithPendingVerification_ShouldReturnError()
        {
            // Arrange
            var userId = "test-user-123";
            var request = new StartKycVerificationRequest { FullName = "John Doe" };
            var correlationId = "corr-123";

            var existingRecord = new KycRecord
            {
                UserId = userId,
                Status = KycStatus.Pending
            };

            _repositoryMock.Setup(r => r.GetKycRecordByUserIdAsync(userId))
                .ReturnsAsync(existingRecord);

            // Act
            var result = await _service.StartVerificationAsync(userId, request, correlationId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.KYC_VERIFICATION_ALREADY_PENDING));
            _providerMock.Verify(p => p.StartVerificationAsync(It.IsAny<string>(), It.IsAny<StartKycVerificationRequest>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task StartVerificationAsync_ProviderError_ShouldReturnError()
        {
            // Arrange
            var userId = "test-user-123";
            var request = new StartKycVerificationRequest { FullName = "John Doe" };
            var correlationId = "corr-123";
            var errorMessage = "Provider unavailable";

            _repositoryMock.Setup(r => r.GetKycRecordByUserIdAsync(userId))
                .ReturnsAsync((KycRecord?)null);
            
            _providerMock.Setup(p => p.StartVerificationAsync(userId, request, correlationId))
                .ReturnsAsync(("", KycStatus.NotStarted, errorMessage));

            // Act
            var result = await _service.StartVerificationAsync(userId, request, correlationId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(ErrorCodes.KYC_PROVIDER_ERROR));
            Assert.That(result.ErrorMessage, Is.EqualTo(errorMessage));
        }

        [Test]
        public async Task StartVerificationAsync_WithAutoApprove_ShouldSetExpirationDate()
        {
            // Arrange
            var userId = "test-user-123";
            var request = new StartKycVerificationRequest { FullName = "John Doe" };
            var correlationId = "corr-123";
            var providerRefId = "MOCK-12345";

            _repositoryMock.Setup(r => r.GetKycRecordByUserIdAsync(userId))
                .ReturnsAsync((KycRecord?)null);
            
            _providerMock.Setup(p => p.StartVerificationAsync(userId, request, correlationId))
                .ReturnsAsync((providerRefId, KycStatus.Approved, (string?)null));

            KycRecord? createdRecord = null;
            _repositoryMock.Setup(r => r.CreateKycRecordAsync(It.IsAny<KycRecord>()))
                .Callback<KycRecord>(r => createdRecord = r)
                .ReturnsAsync((KycRecord r) => r);

            // Act
            var result = await _service.StartVerificationAsync(userId, request, correlationId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Status, Is.EqualTo(KycStatus.Approved));
            Assert.That(createdRecord, Is.Not.Null);
            Assert.That(createdRecord!.CompletedAt, Is.Not.Null);
            Assert.That(createdRecord.ExpiresAt, Is.Not.Null);
        }

        #endregion

        #region GetStatusAsync Tests

        [Test]
        public async Task GetStatusAsync_NoRecord_ShouldReturnNotStarted()
        {
            // Arrange
            var userId = "test-user-123";
            _repositoryMock.Setup(r => r.GetKycRecordByUserIdAsync(userId))
                .ReturnsAsync((KycRecord?)null);

            // Act
            var result = await _service.GetStatusAsync(userId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Status, Is.EqualTo(KycStatus.NotStarted));
        }

        [Test]
        public async Task GetStatusAsync_WithRecord_ShouldReturnStatus()
        {
            // Arrange
            var userId = "test-user-123";
            var record = new KycRecord
            {
                KycId = "kyc-123",
                UserId = userId,
                Status = KycStatus.Pending,
                Provider = KycProvider.Mock,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            _repositoryMock.Setup(r => r.GetKycRecordByUserIdAsync(userId))
                .ReturnsAsync(record);

            // Act
            var result = await _service.GetStatusAsync(userId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.KycId, Is.EqualTo(record.KycId));
            Assert.That(result.Status, Is.EqualTo(KycStatus.Pending));
            Assert.That(result.Provider, Is.EqualTo(KycProvider.Mock));
        }

        [Test]
        public async Task GetStatusAsync_ExpiredRecord_ShouldMarkAsExpired()
        {
            // Arrange
            var userId = "test-user-123";
            var record = new KycRecord
            {
                KycId = "kyc-123",
                UserId = userId,
                Status = KycStatus.Approved,
                ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired
            };

            _repositoryMock.Setup(r => r.GetKycRecordByUserIdAsync(userId))
                .ReturnsAsync(record);

            _repositoryMock.Setup(r => r.UpdateKycRecordAsync(It.IsAny<KycRecord>()))
                .ReturnsAsync((KycRecord r) => r);

            // Act
            var result = await _service.GetStatusAsync(userId);

            // Assert
            Assert.That(result.Status, Is.EqualTo(KycStatus.Expired));
            _repositoryMock.Verify(r => r.UpdateKycRecordAsync(It.Is<KycRecord>(kr => kr.Status == KycStatus.Expired)), Times.Once);
        }

        #endregion

        #region HandleWebhookAsync Tests

        [Test]
        public async Task HandleWebhookAsync_ValidWebhook_ShouldUpdateStatus()
        {
            // Arrange
            var providerRefId = "MOCK-12345";
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = providerRefId,
                EventType = "verification.completed",
                Status = "approved"
            };

            var record = new KycRecord
            {
                KycId = "kyc-123",
                UserId = "user-123",
                Status = KycStatus.Pending,
                ProviderReferenceId = providerRefId
            };

            _providerMock.Setup(p => p.ParseWebhookAsync(payload))
                .ReturnsAsync((providerRefId, KycStatus.Approved, "Verification successful"));

            _repositoryMock.Setup(r => r.GetKycRecordByProviderReferenceIdAsync(providerRefId))
                .ReturnsAsync(record);

            _repositoryMock.Setup(r => r.UpdateKycRecordAsync(It.IsAny<KycRecord>()))
                .ReturnsAsync((KycRecord r) => r);

            // Act
            var result = await _service.HandleWebhookAsync(payload, null);

            // Assert
            Assert.That(result, Is.True);
            _repositoryMock.Verify(r => r.UpdateKycRecordAsync(It.Is<KycRecord>(kr => 
                kr.Status == KycStatus.Approved && kr.CompletedAt != null)), Times.Once);
        }

        [Test]
        public async Task HandleWebhookAsync_RecordNotFound_ShouldReturnFalse()
        {
            // Arrange
            var payload = new KycWebhookPayload
            {
                ProviderReferenceId = "MOCK-12345",
                EventType = "verification.completed",
                Status = "approved"
            };

            _providerMock.Setup(p => p.ParseWebhookAsync(payload))
                .ReturnsAsync(("MOCK-12345", KycStatus.Approved, (string?)null));

            _repositoryMock.Setup(r => r.GetKycRecordByProviderReferenceIdAsync(It.IsAny<string>()))
                .ReturnsAsync((KycRecord?)null);

            // Act
            var result = await _service.HandleWebhookAsync(payload, null);

            // Assert
            Assert.That(result, Is.False);
            _repositoryMock.Verify(r => r.UpdateKycRecordAsync(It.IsAny<KycRecord>()), Times.Never);
        }

        [Test]
        public async Task HandleWebhookAsync_InvalidSignature_ShouldReturnFalse()
        {
            // Arrange
            var payload = new KycWebhookPayload { ProviderReferenceId = "MOCK-12345" };
            var signature = "invalid-signature";
            var secret = "test-secret";

            _config = Options.Create(new KycConfig
            {
                WebhookSecret = secret
            });
            _service = new KycService(_repositoryMock.Object, _providerMock.Object, _config, _loggerMock.Object);

            _providerMock.Setup(p => p.ValidateWebhookSignature(It.IsAny<string>(), signature, secret))
                .Returns(false);

            // Act
            var result = await _service.HandleWebhookAsync(payload, signature);

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region IsUserVerifiedAsync Tests

        [Test]
        public async Task IsUserVerifiedAsync_NoRecord_ShouldReturnFalse()
        {
            // Arrange
            var userId = "test-user-123";
            _repositoryMock.Setup(r => r.GetKycRecordByUserIdAsync(userId))
                .ReturnsAsync((KycRecord?)null);

            // Act
            var result = await _service.IsUserVerifiedAsync(userId);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task IsUserVerifiedAsync_ApprovedNotExpired_ShouldReturnTrue()
        {
            // Arrange
            var userId = "test-user-123";
            var record = new KycRecord
            {
                UserId = userId,
                Status = KycStatus.Approved,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };

            _repositoryMock.Setup(r => r.GetKycRecordByUserIdAsync(userId))
                .ReturnsAsync(record);

            // Act
            var result = await _service.IsUserVerifiedAsync(userId);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task IsUserVerifiedAsync_Pending_ShouldReturnFalse()
        {
            // Arrange
            var userId = "test-user-123";
            var record = new KycRecord
            {
                UserId = userId,
                Status = KycStatus.Pending
            };

            _repositoryMock.Setup(r => r.GetKycRecordByUserIdAsync(userId))
                .ReturnsAsync(record);

            // Act
            var result = await _service.IsUserVerifiedAsync(userId);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task IsUserVerifiedAsync_Expired_ShouldReturnFalse()
        {
            // Arrange
            var userId = "test-user-123";
            var record = new KycRecord
            {
                UserId = userId,
                Status = KycStatus.Approved,
                ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired
            };

            _repositoryMock.Setup(r => r.GetKycRecordByUserIdAsync(userId))
                .ReturnsAsync(record);

            _repositoryMock.Setup(r => r.UpdateKycRecordAsync(It.IsAny<KycRecord>()))
                .ReturnsAsync((KycRecord r) => r);

            // Act
            var result = await _service.IsUserVerifiedAsync(userId);

            // Assert
            Assert.That(result, Is.False);
            _repositoryMock.Verify(r => r.UpdateKycRecordAsync(It.Is<KycRecord>(kr => kr.Status == KycStatus.Expired)), Times.Once);
        }

        #endregion

        #region IsEnforcementEnabled Tests

        [Test]
        public void IsEnforcementEnabled_WhenEnabled_ShouldReturnTrue()
        {
            // Act
            var result = _service.IsEnforcementEnabled();

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsEnforcementEnabled_WhenDisabled_ShouldReturnFalse()
        {
            // Arrange
            _config = Options.Create(new KycConfig { EnforcementEnabled = false });
            _service = new KycService(_repositoryMock.Object, _providerMock.Object, _config, _loggerMock.Object);

            // Act
            var result = _service.IsEnforcementEnabled();

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion
    }
}
