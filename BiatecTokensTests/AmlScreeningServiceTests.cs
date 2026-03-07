using BiatecTokensApi.Models.Aml;
using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for <see cref="AmlService"/>.
    /// Covers ScreenUserAsync, GetStatusAsync, HandleWebhookAsync, GenerateReportAsync,
    /// IsUserClearedAsync, and AnonymizeUserAmlDataAsync across normal, warning, and critical conditions.
    /// </summary>
    [TestFixture]
    public class AmlScreeningServiceTests
    {
        private Mock<IAmlRepository> _repositoryMock = null!;
        private Mock<IAmlProvider> _providerMock = null!;
        private Mock<ILogger<AmlService>> _loggerMock = null!;
        private AmlService _service = null!;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IAmlRepository>();
            _providerMock = new Mock<IAmlProvider>();
            _loggerMock = new Mock<ILogger<AmlService>>();
            _service = new AmlService(_repositoryMock.Object, _providerMock.Object, _loggerMock.Object);
        }

        // =========================================================
        // ScreenUserAsync – happy path
        // =========================================================

        [Test]
        public async Task ScreenUserAsync_NewUser_Cleared_ShouldCreateRecord()
        {
            // Arrange
            var userId = "user-aml-001";
            var correlationId = "corr-aml-001";
            var providerRefId = "AML-MOCK-abc123";
            var metadata = new Dictionary<string, string>();

            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, metadata, correlationId))
                .ReturnsAsync((providerRefId, ComplianceDecisionState.Approved, (string?)null, (string?)null));

            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync((AmlRecord?)null);

            _repositoryMock.Setup(r => r.CreateAmlRecordAsync(It.IsAny<AmlRecord>()))
                .ReturnsAsync((AmlRecord r) => r);

            // Act
            var result = await _service.ScreenUserAsync(userId, metadata, correlationId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Status, Is.EqualTo(AmlScreeningStatus.Cleared));
            Assert.That(result.RiskLevel, Is.EqualTo(AmlRiskLevel.Low));
            Assert.That(result.ProviderReferenceId, Is.EqualTo(providerRefId));
            _repositoryMock.Verify(r => r.CreateAmlRecordAsync(It.Is<AmlRecord>(
                a => a.Status == AmlScreeningStatus.Cleared && a.RiskLevel == AmlRiskLevel.Low)), Times.Once);
        }

        [Test]
        public async Task ScreenUserAsync_ExistingUser_ShouldUpdateRecord()
        {
            // Arrange
            var userId = "user-aml-002";
            var correlationId = "corr-aml-002";
            var providerRefId = "AML-MOCK-def456";
            var metadata = new Dictionary<string, string>();

            var existingRecord = new AmlRecord
            {
                AmlId = "aml-existing-001",
                UserId = userId,
                Status = AmlScreeningStatus.Cleared
            };

            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, metadata, correlationId))
                .ReturnsAsync((providerRefId, ComplianceDecisionState.Approved, (string?)null, (string?)null));

            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync(existingRecord);

            _repositoryMock.Setup(r => r.UpdateAmlRecordAsync(It.IsAny<AmlRecord>()))
                .ReturnsAsync((AmlRecord r) => r);

            // Act
            var result = await _service.ScreenUserAsync(userId, metadata, correlationId);

            // Assert
            Assert.That(result.Success, Is.True);
            _repositoryMock.Verify(r => r.UpdateAmlRecordAsync(It.IsAny<AmlRecord>()), Times.Once);
            _repositoryMock.Verify(r => r.CreateAmlRecordAsync(It.IsAny<AmlRecord>()), Times.Never);
        }

        // =========================================================
        // ScreenUserAsync – sanctions / PEP / review scenarios
        // =========================================================

        [Test]
        public async Task ScreenUserAsync_SanctionsMatch_ShouldReturnHighRisk()
        {
            // Arrange
            var userId = "user-sanctioned";
            var correlationId = "corr-sanctioned";
            var metadata = new Dictionary<string, string>();

            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, metadata, correlationId))
                .ReturnsAsync(("AML-SANC-001", ComplianceDecisionState.Rejected, "SANCTIONS_MATCH", (string?)null));

            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync((AmlRecord?)null);

            _repositoryMock.Setup(r => r.CreateAmlRecordAsync(It.IsAny<AmlRecord>()))
                .ReturnsAsync((AmlRecord r) => r);

            // Act
            var result = await _service.ScreenUserAsync(userId, metadata, correlationId);

            // Assert
            Assert.That(result.Status, Is.EqualTo(AmlScreeningStatus.SanctionsMatch));
            Assert.That(result.RiskLevel, Is.EqualTo(AmlRiskLevel.High));
        }

        [Test]
        public async Task ScreenUserAsync_PepMatch_ShouldReturnHighRisk()
        {
            // Arrange
            var userId = "user-pep";
            var correlationId = "corr-pep";
            var metadata = new Dictionary<string, string>();

            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, metadata, correlationId))
                .ReturnsAsync(("AML-PEP-001", ComplianceDecisionState.Rejected, "PEP_MATCH", (string?)null));

            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync((AmlRecord?)null);

            _repositoryMock.Setup(r => r.CreateAmlRecordAsync(It.IsAny<AmlRecord>()))
                .ReturnsAsync((AmlRecord r) => r);

            // Act
            var result = await _service.ScreenUserAsync(userId, metadata, correlationId);

            // Assert
            Assert.That(result.Status, Is.EqualTo(AmlScreeningStatus.PepMatch));
            Assert.That(result.RiskLevel, Is.EqualTo(AmlRiskLevel.High));
        }

        [Test]
        public async Task ScreenUserAsync_NeedsReview_ShouldReturnMediumRisk()
        {
            // Arrange
            var userId = "user-review";
            var correlationId = "corr-review";
            var metadata = new Dictionary<string, string>();

            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, metadata, correlationId))
                .ReturnsAsync(("AML-REV-001", ComplianceDecisionState.NeedsReview, "REVIEW_REQUIRED", (string?)null));

            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync((AmlRecord?)null);

            _repositoryMock.Setup(r => r.CreateAmlRecordAsync(It.IsAny<AmlRecord>()))
                .ReturnsAsync((AmlRecord r) => r);

            // Act
            var result = await _service.ScreenUserAsync(userId, metadata, correlationId);

            // Assert
            Assert.That(result.Status, Is.EqualTo(AmlScreeningStatus.NeedsReview));
            Assert.That(result.RiskLevel, Is.EqualTo(AmlRiskLevel.Medium));
        }

        [Test]
        public async Task ScreenUserAsync_ProviderError_ShouldReturnErrorStatus()
        {
            // Arrange
            var userId = "user-error";
            var correlationId = "corr-error";
            var metadata = new Dictionary<string, string>();

            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, metadata, correlationId))
                .ReturnsAsync(("AML-ERR-001", ComplianceDecisionState.Error, "PROVIDER_TIMEOUT", "Simulated timeout"));

            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync((AmlRecord?)null);

            _repositoryMock.Setup(r => r.CreateAmlRecordAsync(It.IsAny<AmlRecord>()))
                .ReturnsAsync((AmlRecord r) => r);

            // Act
            var result = await _service.ScreenUserAsync(userId, metadata, correlationId);

            // Assert
            Assert.That(result.Status, Is.EqualTo(AmlScreeningStatus.Error));
            Assert.That(result.RiskLevel, Is.EqualTo(AmlRiskLevel.Unknown));
        }

        [Test]
        public async Task ScreenUserAsync_ClearedUser_ShouldSetNextScreeningDate()
        {
            // Arrange
            var userId = "user-next-screen";
            var correlationId = "corr-next";
            var metadata = new Dictionary<string, string>();

            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, metadata, correlationId))
                .ReturnsAsync(("AML-NS-001", ComplianceDecisionState.Approved, (string?)null, (string?)null));

            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync((AmlRecord?)null);

            AmlRecord? createdRecord = null;
            _repositoryMock.Setup(r => r.CreateAmlRecordAsync(It.IsAny<AmlRecord>()))
                .Callback<AmlRecord>(r => createdRecord = r)
                .ReturnsAsync((AmlRecord r) => r);

            // Act
            await _service.ScreenUserAsync(userId, metadata, correlationId);

            // Assert
            Assert.That(createdRecord, Is.Not.Null);
            Assert.That(createdRecord!.NextScreeningDue, Is.Not.Null);
            Assert.That(createdRecord.NextScreeningDue!.Value, Is.GreaterThan(DateTime.UtcNow));
        }

        // =========================================================
        // ScreenUserAsync – exception handling
        // =========================================================

        [Test]
        public async Task ScreenUserAsync_ProviderThrows_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-throw";
            var correlationId = "corr-throw";
            var metadata = new Dictionary<string, string>();

            _providerMock.Setup(p => p.ScreenSubjectAsync(userId, metadata, correlationId))
                .ThrowsAsync(new Exception("Provider connection failed"));

            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync((AmlRecord?)null);

            // Act
            var result = await _service.ScreenUserAsync(userId, metadata, correlationId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null);
        }

        // =========================================================
        // GetStatusAsync
        // =========================================================

        [Test]
        public async Task GetStatusAsync_NoRecord_ShouldReturnNotScreened()
        {
            // Arrange
            var userId = "user-no-record";
            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync((AmlRecord?)null);

            // Act
            var result = await _service.GetStatusAsync(userId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Status, Is.EqualTo(AmlScreeningStatus.NotScreened));
            Assert.That(result.RiskLevel, Is.EqualTo(AmlRiskLevel.Unknown));
        }

        [Test]
        public async Task GetStatusAsync_WithRecord_ShouldReturnRecord()
        {
            // Arrange
            var userId = "user-with-record";
            var record = new AmlRecord
            {
                AmlId = "aml-001",
                UserId = userId,
                Status = AmlScreeningStatus.Cleared,
                RiskLevel = AmlRiskLevel.Low,
                ProviderReferenceId = "AML-MOCK-001",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-5),
                NextScreeningDue = DateTime.UtcNow.AddDays(25)
            };

            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync(record);

            // Act
            var result = await _service.GetStatusAsync(userId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.AmlId, Is.EqualTo("aml-001"));
            Assert.That(result.Status, Is.EqualTo(AmlScreeningStatus.Cleared));
            Assert.That(result.RiskLevel, Is.EqualTo(AmlRiskLevel.Low));
            Assert.That(result.NextScreeningDue, Is.Not.Null);
        }

        [Test]
        public async Task GetStatusAsync_RepositoryThrows_ShouldReturnFailure()
        {
            // Arrange
            var userId = "user-repo-throw";
            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _service.GetStatusAsync(userId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null);
        }

        // =========================================================
        // HandleWebhookAsync
        // =========================================================

        [Test]
        public async Task HandleWebhookAsync_ValidPayload_ShouldUpdateRecord()
        {
            // Arrange
            var providerRefId = "AML-MOCK-wh001";
            var record = new AmlRecord
            {
                AmlId = "aml-wh-001",
                UserId = "user-wh-001",
                Status = AmlScreeningStatus.Cleared,
                ProviderReferenceId = providerRefId
            };

            _repositoryMock.Setup(r => r.GetAmlRecordByProviderReferenceIdAsync(providerRefId))
                .ReturnsAsync(record);

            _repositoryMock.Setup(r => r.UpdateAmlRecordAsync(It.IsAny<AmlRecord>()))
                .ReturnsAsync((AmlRecord r) => r);

            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = providerRefId,
                AlertType = "SANCTIONS_MATCH",
                Status = "SANCTIONS_MATCH",
                RiskLevel = "HIGH",
                Timestamp = DateTime.UtcNow,
                ReasonCode = "OFAC_SDN"
            };

            // Act
            var result = await _service.HandleWebhookAsync(payload, null);

            // Assert
            Assert.That(result, Is.True);
            _repositoryMock.Verify(r => r.UpdateAmlRecordAsync(It.Is<AmlRecord>(
                a => a.Status == AmlScreeningStatus.SanctionsMatch && a.RiskLevel == AmlRiskLevel.High)), Times.Once);
        }

        [Test]
        public async Task HandleWebhookAsync_RecordNotFound_ShouldReturnFalse()
        {
            // Arrange
            var providerRefId = "AML-UNKNOWN-001";
            _repositoryMock.Setup(r => r.GetAmlRecordByProviderReferenceIdAsync(providerRefId))
                .ReturnsAsync((AmlRecord?)null);

            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = providerRefId,
                AlertType = "REVIEW_REQUIRED",
                Status = "NEEDS_REVIEW",
                RiskLevel = "MEDIUM",
                Timestamp = DateTime.UtcNow
            };

            // Act
            var result = await _service.HandleWebhookAsync(payload, null);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task HandleWebhookAsync_ClearedWebhook_ShouldSetNextScreeningDate()
        {
            // Arrange
            var providerRefId = "AML-MOCK-wh002";
            var record = new AmlRecord
            {
                AmlId = "aml-wh-002",
                UserId = "user-wh-002",
                Status = AmlScreeningStatus.NeedsReview,
                ProviderReferenceId = providerRefId
            };

            _repositoryMock.Setup(r => r.GetAmlRecordByProviderReferenceIdAsync(providerRefId))
                .ReturnsAsync(record);

            AmlRecord? updatedRecord = null;
            _repositoryMock.Setup(r => r.UpdateAmlRecordAsync(It.IsAny<AmlRecord>()))
                .Callback<AmlRecord>(r => updatedRecord = r)
                .ReturnsAsync((AmlRecord r) => r);

            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = providerRefId,
                AlertType = "CLEARED",
                Status = "CLEARED",
                RiskLevel = "LOW",
                Timestamp = DateTime.UtcNow
            };

            // Act
            await _service.HandleWebhookAsync(payload, null);

            // Assert
            Assert.That(updatedRecord, Is.Not.Null);
            Assert.That(updatedRecord!.NextScreeningDue, Is.Not.Null);
        }

        [Test]
        public async Task HandleWebhookAsync_PepMatchWebhook_ShouldUpdateToPepStatus()
        {
            // Arrange
            var providerRefId = "AML-MOCK-pep001";
            var record = new AmlRecord
            {
                AmlId = "aml-pep-001",
                UserId = "user-pep-wh-001",
                Status = AmlScreeningStatus.Cleared,
                ProviderReferenceId = providerRefId
            };

            _repositoryMock.Setup(r => r.GetAmlRecordByProviderReferenceIdAsync(providerRefId))
                .ReturnsAsync(record);

            _repositoryMock.Setup(r => r.UpdateAmlRecordAsync(It.IsAny<AmlRecord>()))
                .ReturnsAsync((AmlRecord r) => r);

            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = providerRefId,
                AlertType = "PEP_MATCH",
                Status = "PEP_MATCH",
                RiskLevel = "HIGH",
                Timestamp = DateTime.UtcNow,
                ReasonCode = "PEP_DATABASE"
            };

            // Act
            var result = await _service.HandleWebhookAsync(payload, null);

            // Assert
            Assert.That(result, Is.True);
            _repositoryMock.Verify(r => r.UpdateAmlRecordAsync(It.Is<AmlRecord>(
                a => a.Status == AmlScreeningStatus.PepMatch)), Times.Once);
        }

        // =========================================================
        // GenerateReportAsync
        // =========================================================

        [Test]
        public async Task GenerateReportAsync_NoHistory_ShouldReturnEmptyReport()
        {
            // Arrange
            var userId = "user-no-history";
            _repositoryMock.Setup(r => r.GetAmlRecordsByUserIdAsync(userId))
                .ReturnsAsync(new List<AmlRecord>());

            // Act
            var result = await _service.GenerateReportAsync(userId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.UserId, Is.EqualTo(userId));
            Assert.That(result.ScreeningHistory, Is.Empty);
            Assert.That(result.LatestRecord, Is.Null);
            Assert.That(result.ComplianceSummary, Does.Contain("No AML screening"));
        }

        [Test]
        public async Task GenerateReportAsync_WithHistory_ShouldReturnFullReport()
        {
            // Arrange
            var userId = "user-with-history";
            var records = new List<AmlRecord>
            {
                new AmlRecord
                {
                    AmlId = "aml-h-001",
                    UserId = userId,
                    Status = AmlScreeningStatus.Cleared,
                    RiskLevel = AmlRiskLevel.Low,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    UpdatedAt = DateTime.UtcNow.AddDays(-30)
                },
                new AmlRecord
                {
                    AmlId = "aml-h-002",
                    UserId = userId,
                    Status = AmlScreeningStatus.Cleared,
                    RiskLevel = AmlRiskLevel.Low,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedAt = DateTime.UtcNow.AddDays(-5),
                    NextScreeningDue = DateTime.UtcNow.AddDays(25)
                }
            };

            _repositoryMock.Setup(r => r.GetAmlRecordsByUserIdAsync(userId))
                .ReturnsAsync(records);

            // Act
            var result = await _service.GenerateReportAsync(userId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.ScreeningHistory.Count, Is.EqualTo(2));
            Assert.That(result.LatestRecord, Is.Not.Null);
        }

        [Test]
        public async Task GenerateReportAsync_SanctionsMatch_ShouldIncludeBlockedSummary()
        {
            // Arrange
            var userId = "user-sanctioned-report";
            var records = new List<AmlRecord>
            {
                new AmlRecord
                {
                    AmlId = "aml-s-001",
                    UserId = userId,
                    Status = AmlScreeningStatus.SanctionsMatch,
                    RiskLevel = AmlRiskLevel.High,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _repositoryMock.Setup(r => r.GetAmlRecordsByUserIdAsync(userId))
                .ReturnsAsync(records);

            // Act
            var result = await _service.GenerateReportAsync(userId);

            // Assert
            Assert.That(result.ComplianceSummary, Does.Contain("sanctions"));
        }

        // =========================================================
        // IsUserClearedAsync
        // =========================================================

        [Test]
        public async Task IsUserClearedAsync_ClearedRecord_ShouldReturnTrue()
        {
            // Arrange
            var userId = "user-cleared";
            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync(new AmlRecord { UserId = userId, Status = AmlScreeningStatus.Cleared });

            // Act
            var result = await _service.IsUserClearedAsync(userId);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task IsUserClearedAsync_SanctionsMatch_ShouldReturnFalse()
        {
            // Arrange
            var userId = "user-blocked";
            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync(new AmlRecord { UserId = userId, Status = AmlScreeningStatus.SanctionsMatch });

            // Act
            var result = await _service.IsUserClearedAsync(userId);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task IsUserClearedAsync_NoRecord_ShouldReturnFalse()
        {
            // Arrange
            var userId = "user-not-screened";
            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ReturnsAsync((AmlRecord?)null);

            // Act
            var result = await _service.IsUserClearedAsync(userId);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task IsUserClearedAsync_RepositoryThrows_ShouldReturnFalse()
        {
            // Arrange
            var userId = "user-throw";
            _repositoryMock.Setup(r => r.GetAmlRecordByUserIdAsync(userId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _service.IsUserClearedAsync(userId);

            // Assert
            Assert.That(result, Is.False);
        }

        // =========================================================
        // AnonymizeUserAmlDataAsync
        // =========================================================

        [Test]
        public async Task AnonymizeUserAmlDataAsync_WithRecords_ShouldAnonymizeAll()
        {
            // Arrange
            var userId = "user-gdpr";
            var records = new List<AmlRecord>
            {
                new AmlRecord { AmlId = "aml-g-001", UserId = userId, Notes = "Private note", ReasonCode = "REASON" },
                new AmlRecord { AmlId = "aml-g-002", UserId = userId, Notes = "Another note", ReasonCode = null }
            };

            _repositoryMock.Setup(r => r.GetAmlRecordsByUserIdAsync(userId))
                .ReturnsAsync(records);

            _repositoryMock.Setup(r => r.UpdateAmlRecordAsync(It.IsAny<AmlRecord>()))
                .ReturnsAsync((AmlRecord r) => r);

            // Act
            var count = await _service.AnonymizeUserAmlDataAsync(userId);

            // Assert
            Assert.That(count, Is.EqualTo(2));
            _repositoryMock.Verify(r => r.UpdateAmlRecordAsync(It.Is<AmlRecord>(
                a => a.Notes == "[GDPR_ANONYMIZED]")), Times.Exactly(2));
        }

        [Test]
        public async Task AnonymizeUserAmlDataAsync_NoRecords_ShouldReturnZero()
        {
            // Arrange
            var userId = "user-no-gdpr";
            _repositoryMock.Setup(r => r.GetAmlRecordsByUserIdAsync(userId))
                .ReturnsAsync(new List<AmlRecord>());

            // Act
            var count = await _service.AnonymizeUserAmlDataAsync(userId);

            // Assert
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public async Task AnonymizeUserAmlDataAsync_RepositoryThrows_ShouldReturnZero()
        {
            // Arrange
            var userId = "user-throw-gdpr";
            _repositoryMock.Setup(r => r.GetAmlRecordsByUserIdAsync(userId))
                .ThrowsAsync(new Exception("Storage error"));

            // Act
            var count = await _service.AnonymizeUserAmlDataAsync(userId);

            // Assert
            Assert.That(count, Is.EqualTo(0));
        }
    }
}
