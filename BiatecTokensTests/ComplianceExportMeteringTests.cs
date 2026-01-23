using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for compliance export endpoint usage metering functionality
    /// </summary>
    [TestFixture]
    public class ComplianceExportMeteringTests
    {
        private Mock<IComplianceService> _serviceMock;
        private Mock<ILogger<ComplianceController>> _loggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private ComplianceController _controller;
        private const string TestUserAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

        [SetUp]
        public void Setup()
        {
            _serviceMock = new Mock<IComplianceService>();
            _loggerMock = new Mock<ILogger<ComplianceController>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _controller = new ComplianceController(_serviceMock.Object, _loggerMock.Object, _meteringServiceMock.Object);

            // Mock authenticated user context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, TestUserAddress)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = claimsPrincipal
                }
            };
        }

        #region ExportAuditLogCsv Metering Tests

        [Test]
        public async Task ExportAuditLogCsv_ShouldEmitMeteringEventWithCorrectFormat()
        {
            // Arrange
            var entries = new List<ComplianceAuditLogEntry>
            {
                new ComplianceAuditLogEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    AssetId = 12345,
                    Network = "voimain",
                    ActionType = ComplianceActionType.Create,
                    PerformedBy = TestUserAddress,
                    PerformedAt = DateTime.UtcNow,
                    Success = true
                }
            };

            _serviceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new ComplianceAuditLogResponse
                {
                    Success = true,
                    Entries = entries
                });

            // Act
            var result = await _controller.ExportAuditLogCsv(
                assetId: 12345,
                network: "voimain",
                fromDate: DateTime.UtcNow.AddDays(-7),
                toDate: DateTime.UtcNow
            );

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());

            // Verify metering event was emitted
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(e =>
                e.Category == MeteringCategory.Compliance &&
                e.OperationType == MeteringOperationType.Export &&
                e.AssetId == 12345 &&
                e.Network == "voimain" &&
                e.PerformedBy == TestUserAddress &&
                e.ItemCount == 1 &&
                e.Metadata != null &&
                e.Metadata["exportFormat"] == "csv" &&
                e.Metadata["exportType"] == "auditLog" &&
                e.Metadata["rowCount"] == "1"
            )), Times.Once);
        }

        [Test]
        public async Task ExportAuditLogCsv_WithoutFilters_ShouldUseDefaultsInMetadata()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new ComplianceAuditLogResponse
                {
                    Success = true,
                    Entries = new List<ComplianceAuditLogEntry>()
                });

            // Act
            await _controller.ExportAuditLogCsv();

            // Assert - verify metadata defaults
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(e =>
                e.Network == "all" &&
                e.AssetId == 0 &&
                e.Metadata != null &&
                e.Metadata["fromDate"] == "none" &&
                e.Metadata["toDate"] == "none" &&
                e.Metadata["actionType"] == "all"
            )), Times.Once);
        }

        [Test]
        public async Task ExportAuditLogCsv_MultipleRecords_ShouldIncludeCorrectRowCount()
        {
            // Arrange
            var entries = Enumerable.Range(1, 50).Select(i => new ComplianceAuditLogEntry
            {
                Id = Guid.NewGuid().ToString(),
                AssetId = 12345,
                Network = "voimain",
                ActionType = ComplianceActionType.Create,
                PerformedBy = TestUserAddress,
                PerformedAt = DateTime.UtcNow,
                Success = true
            }).ToList();

            _serviceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new ComplianceAuditLogResponse
                {
                    Success = true,
                    Entries = entries
                });

            // Act
            await _controller.ExportAuditLogCsv();

            // Assert
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(e =>
                e.ItemCount == 50 &&
                e.Metadata != null &&
                e.Metadata["rowCount"] == "50"
            )), Times.Once);
        }

        #endregion

        #region ExportAuditLogJson Metering Tests

        [Test]
        public async Task ExportAuditLogJson_ShouldEmitMeteringEventWithJsonFormat()
        {
            // Arrange
            var entries = new List<ComplianceAuditLogEntry>
            {
                new ComplianceAuditLogEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    AssetId = 67890,
                    Network = "aramidmain",
                    ActionType = ComplianceActionType.Update,
                    PerformedBy = TestUserAddress,
                    PerformedAt = DateTime.UtcNow,
                    Success = true
                }
            };

            _serviceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new ComplianceAuditLogResponse
                {
                    Success = true,
                    Entries = entries
                });

            // Act
            var result = await _controller.ExportAuditLogJson(assetId: 67890, network: "aramidmain");

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());

            // Verify metering event with JSON format
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(e =>
                e.Category == MeteringCategory.Compliance &&
                e.OperationType == MeteringOperationType.Export &&
                e.Metadata != null &&
                e.Metadata["exportFormat"] == "json" &&
                e.Metadata["exportType"] == "auditLog"
            )), Times.Once);
        }

        #endregion

        #region ExportAttestationsJson Metering Tests

        [Test]
        public async Task ExportAttestationsJson_ShouldEmitMeteringEventWithAttestationMetadata()
        {
            // Arrange
            var attestations = new List<ComplianceAttestation>
            {
                new ComplianceAttestation
                {
                    Id = "test-id-1",
                    WalletAddress = TestUserAddress,
                    AssetId = 11111,
                    IssuerAddress = "ISSUER1",
                    AttestationType = "KYC",
                    Network = "testnet",
                    VerificationStatus = AttestationVerificationStatus.Verified,
                    IssuedAt = DateTime.UtcNow
                },
                new ComplianceAttestation
                {
                    Id = "test-id-2",
                    WalletAddress = TestUserAddress,
                    AssetId = 11111,
                    IssuerAddress = "ISSUER1",
                    AttestationType = "AML",
                    Network = "testnet",
                    VerificationStatus = AttestationVerificationStatus.Verified,
                    IssuedAt = DateTime.UtcNow
                }
            };

            _serviceMock.Setup(s => s.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = attestations
                });

            // Act
            await _controller.ExportAttestationsJson(
                assetId: 11111,
                network: "testnet",
                walletAddress: TestUserAddress,
                verificationStatus: AttestationVerificationStatus.Verified,
                attestationType: "KYC"
            );

            // Assert
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(e =>
                e.Category == MeteringCategory.Compliance &&
                e.OperationType == MeteringOperationType.Export &&
                e.AssetId == 11111 &&
                e.Network == "testnet" &&
                e.ItemCount == 2 &&
                e.Metadata != null &&
                e.Metadata["exportFormat"] == "json" &&
                e.Metadata["exportType"] == "attestations" &&
                e.Metadata["rowCount"] == "2" &&
                e.Metadata["walletAddress"] == TestUserAddress &&
                e.Metadata["verificationStatus"] == "Verified" &&
                e.Metadata["attestationType"] == "KYC"
            )), Times.Once);
        }

        #endregion

        #region ExportAttestationsCsv Metering Tests

        [Test]
        public async Task ExportAttestationsCsv_ShouldEmitMeteringEventWithCsvFormat()
        {
            // Arrange
            var attestations = new List<ComplianceAttestation>
            {
                new ComplianceAttestation
                {
                    Id = "test-id",
                    WalletAddress = TestUserAddress,
                    AssetId = 22222,
                    IssuerAddress = "ISSUER2",
                    AttestationType = "Accredited",
                    Network = "voimain",
                    VerificationStatus = AttestationVerificationStatus.Verified,
                    IssuedAt = DateTime.UtcNow
                }
            };

            _serviceMock.Setup(s => s.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = attestations
                });

            // Act
            await _controller.ExportAttestationsCsv(assetId: 22222);

            // Assert
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(e =>
                e.Category == MeteringCategory.Compliance &&
                e.OperationType == MeteringOperationType.Export &&
                e.Metadata != null &&
                e.Metadata["exportFormat"] == "csv" &&
                e.Metadata["exportType"] == "attestations"
            )), Times.Once);
        }

        [Test]
        public async Task ExportAttestationsCsv_WithDateRange_ShouldIncludeDateRangeInMetadata()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddMonths(-1);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = new List<ComplianceAttestation>()
                });

            // Act
            await _controller.ExportAttestationsCsv(fromDate: fromDate, toDate: toDate);

            // Assert
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(e =>
                e.Metadata != null &&
                e.Metadata["fromDate"] == fromDate.ToString("O") &&
                e.Metadata["toDate"] == toDate.ToString("O")
            )), Times.Once);
        }

        #endregion

        #region Metering Event Payload Tests

        [Test]
        public async Task AllExportEndpoints_ShouldGenerateUniqueEventIds()
        {
            // Arrange
            var capturedEvents = new List<SubscriptionMeteringEvent>();
            _meteringServiceMock.Setup(m => m.EmitMeteringEvent(It.IsAny<SubscriptionMeteringEvent>()))
                .Callback<SubscriptionMeteringEvent>(e => capturedEvents.Add(e));

            _serviceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new ComplianceAuditLogResponse
                {
                    Success = true,
                    Entries = new List<ComplianceAuditLogEntry>()
                });

            _serviceMock.Setup(s => s.ListAttestationsAsync(It.IsAny<ListComplianceAttestationsRequest>()))
                .ReturnsAsync(new ComplianceAttestationListResponse
                {
                    Success = true,
                    Attestations = new List<ComplianceAttestation>()
                });

            // Act - call all export endpoints
            await _controller.ExportAuditLogCsv();
            await _controller.ExportAuditLogJson();
            await _controller.ExportAttestationsJson();
            await _controller.ExportAttestationsCsv();

            // Assert - all events should have unique IDs
            Assert.That(capturedEvents.Count, Is.EqualTo(4));
            var eventIds = capturedEvents.Select(e => e.EventId).ToList();
            Assert.That(eventIds.Distinct().Count(), Is.EqualTo(4), "All event IDs should be unique");
        }

        [Test]
        public async Task ExportMetering_ShouldIncludeTimestamp()
        {
            // Arrange
            var beforeExport = DateTime.UtcNow;

            _serviceMock.Setup(s => s.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new ComplianceAuditLogResponse
                {
                    Success = true,
                    Entries = new List<ComplianceAuditLogEntry>()
                });

            // Act
            await _controller.ExportAuditLogCsv();

            var afterExport = DateTime.UtcNow;

            // Assert
            _meteringServiceMock.Verify(m => m.EmitMeteringEvent(It.Is<SubscriptionMeteringEvent>(e =>
                e.Timestamp >= beforeExport && e.Timestamp <= afterExport
            )), Times.Once);
        }

        #endregion
    }
}
