using BiatecTokensApi.Models.Aml;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for <see cref="GdprErasureService"/>.
    /// Verifies that PII is anonymized while audit references and timestamps are preserved,
    /// satisfying GDPR Article 17 and AMLD5 5-year retention requirements.
    /// </summary>
    [TestFixture]
    public class GdprErasureServiceTests
    {
        private Mock<IKycRepository> _kycRepositoryMock = null!;
        private Mock<IAmlService> _amlServiceMock = null!;
        private Mock<ILogger<GdprErasureService>> _loggerMock = null!;
        private GdprErasureService _service = null!;

        [SetUp]
        public void Setup()
        {
            _kycRepositoryMock = new Mock<IKycRepository>();
            _amlServiceMock = new Mock<IAmlService>();
            _loggerMock = new Mock<ILogger<GdprErasureService>>();
            _service = new GdprErasureService(
                _kycRepositoryMock.Object,
                _amlServiceMock.Object,
                _loggerMock.Object);
        }

        // =========================================================
        // EraseUserDataAsync – happy paths
        // =========================================================

        [Test]
        public async Task EraseUserDataAsync_BothKycAndAml_ShouldAnonymizeBoth()
        {
            // Arrange
            var userId = "user-gdpr-001";
            var correlationId = "corr-gdpr-001";
            var request = new GdprErasureRequest { UserId = userId, Reason = "User request" };

            var kycRecords = new List<KycRecord>
            {
                new KycRecord { KycId = "kyc-001", UserId = userId, Reason = "Passport verified" },
                new KycRecord { KycId = "kyc-002", UserId = userId, EncryptedData = "PII_DATA" }
            };

            _kycRepositoryMock.Setup(r => r.GetKycRecordsByUserIdAsync(userId))
                .ReturnsAsync(kycRecords);

            _kycRepositoryMock.Setup(r => r.UpdateKycRecordAsync(It.IsAny<KycRecord>()))
                .ReturnsAsync((KycRecord k) => k);

            _amlServiceMock.Setup(a => a.AnonymizeUserAmlDataAsync(userId))
                .ReturnsAsync(1);

            // Act
            var result = await _service.EraseUserDataAsync(request, correlationId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.KycRecordsAnonymized, Is.EqualTo(2));
            Assert.That(result.AmlRecordsAnonymized, Is.EqualTo(1));
            Assert.That(result.AnonymizationReference, Is.Not.Null);
            Assert.That(result.AnonymizationReference, Does.StartWith("GDPR-"));
            Assert.That(result.ErasedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
        }

        [Test]
        public async Task EraseUserDataAsync_NoKycRecords_ShouldReturnZeroKycCount()
        {
            // Arrange
            var userId = "user-no-kyc";
            var correlationId = "corr-no-kyc";
            var request = new GdprErasureRequest { UserId = userId, Reason = "GDPR request" };

            _kycRepositoryMock.Setup(r => r.GetKycRecordsByUserIdAsync(userId))
                .ReturnsAsync(new List<KycRecord>());

            _amlServiceMock.Setup(a => a.AnonymizeUserAmlDataAsync(userId))
                .ReturnsAsync(0);

            // Act
            var result = await _service.EraseUserDataAsync(request, correlationId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.KycRecordsAnonymized, Is.EqualTo(0));
            Assert.That(result.AmlRecordsAnonymized, Is.EqualTo(0));
        }

        [Test]
        public async Task EraseUserDataAsync_ShouldPreserveKycAuditReference()
        {
            // Arrange
            var userId = "user-audit-ref";
            var correlationId = "corr-audit-ref";
            var request = new GdprErasureRequest { UserId = userId, Reason = "Right to erasure" };

            var kycRecord = new KycRecord
            {
                KycId = "kyc-audit-001",
                UserId = userId,
                Reason = "Original PII data",
                EncryptedData = "SENSITIVE_DATA"
            };

            _kycRepositoryMock.Setup(r => r.GetKycRecordsByUserIdAsync(userId))
                .ReturnsAsync(new List<KycRecord> { kycRecord });

            KycRecord? updatedKycRecord = null;
            _kycRepositoryMock.Setup(r => r.UpdateKycRecordAsync(It.IsAny<KycRecord>()))
                .Callback<KycRecord>(k => updatedKycRecord = k)
                .ReturnsAsync((KycRecord k) => k);

            _amlServiceMock.Setup(a => a.AnonymizeUserAmlDataAsync(userId))
                .ReturnsAsync(0);

            // Act
            var result = await _service.EraseUserDataAsync(request, correlationId);

            // Assert — PII erased, audit reference retained
            Assert.That(updatedKycRecord, Is.Not.Null);
            Assert.That(updatedKycRecord!.Reason, Does.Contain("GDPR_ANONYMIZED"));
            Assert.That(updatedKycRecord.EncryptedData, Is.Null, "Encrypted data should be erased");
            // KycId (audit reference) MUST be preserved
            Assert.That(updatedKycRecord.KycId, Is.EqualTo("kyc-audit-001"));
        }

        [Test]
        public async Task EraseUserDataAsync_ShouldIncludeAnonymizationRefInKycRecord()
        {
            // Arrange
            var userId = "user-ref-check";
            var correlationId = "corr-ref-check";
            var request = new GdprErasureRequest { UserId = userId, Reason = "User deletion" };

            var kycRecord = new KycRecord { KycId = "kyc-ref-001", UserId = userId };

            _kycRepositoryMock.Setup(r => r.GetKycRecordsByUserIdAsync(userId))
                .ReturnsAsync(new List<KycRecord> { kycRecord });

            KycRecord? updatedRecord = null;
            _kycRepositoryMock.Setup(r => r.UpdateKycRecordAsync(It.IsAny<KycRecord>()))
                .Callback<KycRecord>(k => updatedRecord = k)
                .ReturnsAsync((KycRecord k) => k);

            _amlServiceMock.Setup(a => a.AnonymizeUserAmlDataAsync(userId))
                .ReturnsAsync(0);

            // Act
            var result = await _service.EraseUserDataAsync(request, correlationId);

            // Assert — metadata includes anonymization reference for AMLD5 audit trail
            Assert.That(updatedRecord, Is.Not.Null);
            Assert.That(updatedRecord!.Metadata.ContainsKey("anonymization_ref"), Is.True);
            Assert.That(updatedRecord.Metadata["anonymization_ref"], Is.EqualTo(result.AnonymizationReference));
        }

        [Test]
        public async Task EraseUserDataAsync_MultipleKycRecords_ShouldAnonymizeAll()
        {
            // Arrange
            var userId = "user-multi-kyc";
            var correlationId = "corr-multi-kyc";
            var request = new GdprErasureRequest { UserId = userId, Reason = "Erasure test" };

            var kycRecords = new List<KycRecord>
            {
                new KycRecord { KycId = "kyc-m-001", UserId = userId },
                new KycRecord { KycId = "kyc-m-002", UserId = userId },
                new KycRecord { KycId = "kyc-m-003", UserId = userId }
            };

            _kycRepositoryMock.Setup(r => r.GetKycRecordsByUserIdAsync(userId))
                .ReturnsAsync(kycRecords);

            _kycRepositoryMock.Setup(r => r.UpdateKycRecordAsync(It.IsAny<KycRecord>()))
                .ReturnsAsync((KycRecord k) => k);

            _amlServiceMock.Setup(a => a.AnonymizeUserAmlDataAsync(userId))
                .ReturnsAsync(2);

            // Act
            var result = await _service.EraseUserDataAsync(request, correlationId);

            // Assert
            Assert.That(result.KycRecordsAnonymized, Is.EqualTo(3));
            Assert.That(result.AmlRecordsAnonymized, Is.EqualTo(2));
            _kycRepositoryMock.Verify(r => r.UpdateKycRecordAsync(It.IsAny<KycRecord>()), Times.Exactly(3));
        }

        // =========================================================
        // EraseUserDataAsync – error handling
        // =========================================================

        [Test]
        public async Task EraseUserDataAsync_KycRepositoryThrows_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-kyc-throw";
            var correlationId = "corr-kyc-throw";
            var request = new GdprErasureRequest { UserId = userId, Reason = "Test" };

            _kycRepositoryMock.Setup(r => r.GetKycRecordsByUserIdAsync(userId))
                .ThrowsAsync(new Exception("KYC repository error"));

            // Act
            var result = await _service.EraseUserDataAsync(request, correlationId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null);
            Assert.That(result.ErrorMessage, Does.Contain("internal error").IgnoreCase);
        }

        [Test]
        public async Task EraseUserDataAsync_AmlServiceThrows_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-aml-throw";
            var correlationId = "corr-aml-throw";
            var request = new GdprErasureRequest { UserId = userId, Reason = "Test" };

            _kycRepositoryMock.Setup(r => r.GetKycRecordsByUserIdAsync(userId))
                .ReturnsAsync(new List<KycRecord>());

            _amlServiceMock.Setup(a => a.AnonymizeUserAmlDataAsync(userId))
                .ThrowsAsync(new Exception("AML service error"));

            // Act
            var result = await _service.EraseUserDataAsync(request, correlationId);

            // Assert
            Assert.That(result.Success, Is.False);
        }

        // =========================================================
        // Anonymization reference uniqueness
        // =========================================================

        [Test]
        public async Task EraseUserDataAsync_TwoRequests_ShouldGenerateUniqueReferences()
        {
            // Arrange
            var userId1 = "user-unique-1";
            var userId2 = "user-unique-2";
            var request1 = new GdprErasureRequest { UserId = userId1, Reason = "Test" };
            var request2 = new GdprErasureRequest { UserId = userId2, Reason = "Test" };

            foreach (var uid in new[] { userId1, userId2 })
            {
                _kycRepositoryMock.Setup(r => r.GetKycRecordsByUserIdAsync(uid))
                    .ReturnsAsync(new List<KycRecord>());
                _amlServiceMock.Setup(a => a.AnonymizeUserAmlDataAsync(uid))
                    .ReturnsAsync(0);
            }

            // Act
            var result1 = await _service.EraseUserDataAsync(request1, "corr-1");
            var result2 = await _service.EraseUserDataAsync(request2, "corr-2");

            // Assert
            Assert.That(result1.AnonymizationReference, Is.Not.EqualTo(result2.AnonymizationReference));
        }
    }
}
