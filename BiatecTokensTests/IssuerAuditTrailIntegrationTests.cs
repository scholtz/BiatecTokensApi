using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// End-to-end integration tests for RWA issuer audit trail export
    /// Tests the complete flow from audit log creation to CSV/JSON export
    /// </summary>
    [TestFixture]
    public class IssuerAuditTrailIntegrationTests
    {
        private WhitelistRepository _whitelistRepository;
        private ComplianceRepository _complianceRepository;
        private TokenIssuanceRepository _tokenIssuanceRepository;
        private EnterpriseAuditRepository _enterpriseAuditRepository;
        private EnterpriseAuditService _enterpriseAuditService;
        private ComplianceService _complianceService;
        private Mock<ILogger<WhitelistRepository>> _whitelistLoggerMock;
        private Mock<ILogger<ComplianceRepository>> _complianceLoggerMock;
        private Mock<ILogger<TokenIssuanceRepository>> _tokenIssuanceLoggerMock;
        private Mock<ILogger<EnterpriseAuditRepository>> _enterpriseLoggerMock;
        private Mock<ILogger<EnterpriseAuditService>> _serviceLoggerMock;
        private Mock<ILogger<ComplianceService>> _complianceServiceLoggerMock;

        private const string TestIssuerAddress = "ISSUER1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string OtherIssuerAddress = "OTHER12345678901234567890123456789012345678";
        private const string ValidAddress1 = "ADDR1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string ValidAddress2 = "ADDR2BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBY5HFKQ";
        private const ulong TestAssetId = 12345;

        [SetUp]
        public void Setup()
        {
            _whitelistLoggerMock = new Mock<ILogger<WhitelistRepository>>();
            _complianceLoggerMock = new Mock<ILogger<ComplianceRepository>>();
            _tokenIssuanceLoggerMock = new Mock<ILogger<TokenIssuanceRepository>>();
            _enterpriseLoggerMock = new Mock<ILogger<EnterpriseAuditRepository>>();
            _serviceLoggerMock = new Mock<ILogger<EnterpriseAuditService>>();
            _complianceServiceLoggerMock = new Mock<ILogger<ComplianceService>>();

            _whitelistRepository = new WhitelistRepository(_whitelistLoggerMock.Object);
            _complianceRepository = new ComplianceRepository(_complianceLoggerMock.Object);
            _tokenIssuanceRepository = new TokenIssuanceRepository(_tokenIssuanceLoggerMock.Object);
            _enterpriseAuditRepository = new EnterpriseAuditRepository(
                _whitelistRepository,
                _complianceRepository,
                _tokenIssuanceRepository,
                _enterpriseLoggerMock.Object);

            var webhookService = Mock.Of<IWebhookService>();
            _enterpriseAuditService = new EnterpriseAuditService(
                _enterpriseAuditRepository,
                _serviceLoggerMock.Object,
                webhookService);

            var whitelistService = Mock.Of<IWhitelistService>();
            var meteringService = Mock.Of<ISubscriptionMeteringService>();
            _complianceService = new ComplianceService(
                _complianceRepository,
                whitelistService,
                _complianceServiceLoggerMock.Object,
                meteringService,
                Mock.Of<IWebhookService>());
        }

        [Test]
        public async Task EndToEnd_IssuerAuditTrail_CSV_Export()
        {
            // Arrange - Create a complete audit trail for an RWA token
            var now = DateTime.UtcNow;

            // 1. Token issuance event
            await _tokenIssuanceRepository.AddAuditLogEntryAsync(new TokenIssuanceAuditLogEntry
            {
                AssetId = TestAssetId,
                TokenType = "ARC1400",
                TokenName = "RWA Security Token",
                Network = "voimain-v1.0",
                DeployedBy = TestIssuerAddress,
                DeployedAt = now.AddDays(-30),
                Success = true
            });

            // 2. Compliance metadata creation
            await _complianceRepository.UpsertMetadataAsync(new ComplianceMetadata
            {
                AssetId = TestAssetId,
                Network = "voimain-v1.0",
                RegulatoryFramework = "EU-MiCA",
                TransferRestrictions = "Whitelisted addresses only",
                CreatedBy = TestIssuerAddress,
                CreatedAt = now.AddDays(-29)
            });

            await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
            {
                AssetId = TestAssetId,
                ActionType = ComplianceActionType.Create,
                PerformedBy = TestIssuerAddress,
                PerformedAt = now.AddDays(-29),
                Success = true,
                Network = "voimain-v1.0",
                NewComplianceStatus = ComplianceStatus.Compliant
            });

            // 3. Whitelist additions
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestIssuerAddress,
                PerformedAt = now.AddDays(-28),
                NewStatus = WhitelistStatus.Active,
                Network = "voimain-v1.0",
                Notes = "KYC verified investor"
            });

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestIssuerAddress,
                PerformedAt = now.AddDays(-27),
                NewStatus = WhitelistStatus.Active,
                Network = "voimain-v1.0",
                Notes = "Institutional investor"
            });

            // 4. Whitelist enforcement events (transfer validations)
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = "SYSTEM",
                PerformedAt = now.AddDays(-20),
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Amount = 1000,
                Network = "voimain-v1.0"
            });

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = "SYSTEM",
                PerformedAt = now.AddDays(-15),
                ToAddress = "NONWHITELISTED123456789012345678901234567890",
                TransferAllowed = false,
                DenialReason = "Receiver not on whitelist",
                Amount = 500,
                Network = "voimain-v1.0"
            });

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = "SYSTEM",
                PerformedAt = now.AddDays(-10),
                ToAddress = ValidAddress1,
                TransferAllowed = true,
                Amount = 250,
                Network = "voimain-v1.0"
            });

            // Act - Export audit trail as CSV
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = TestAssetId,
                Page = 1,
                PageSize = 100
            };

            var csv = await _enterpriseAuditService.ExportAuditLogCsvAsync(request);

            // Assert - Verify CSV contains all events
            Assert.That(csv, Is.Not.Null);
            Assert.That(csv, Does.Contain("AssetId,Network,Category"));
            Assert.That(csv, Does.Contain(TestAssetId.ToString()));
            Assert.That(csv, Does.Contain("voimain-v1.0"));
            Assert.That(csv, Does.Contain("TokenIssuance"));
            Assert.That(csv, Does.Contain("Compliance"));
            Assert.That(csv, Does.Contain("Whitelist"));
            Assert.That(csv, Does.Contain("TransferValidation"));
            Assert.That(csv, Does.Contain("Receiver not on whitelist"));
            Assert.That(csv, Does.Contain(ValidAddress1));
            Assert.That(csv, Does.Contain(ValidAddress2));

            // Verify CSV has multiple rows
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines.Length, Is.GreaterThan(6)); // Header + at least 6 events
        }

        [Test]
        public async Task EndToEnd_IssuerAuditTrail_JSON_Export()
        {
            // Arrange - Create audit trail
            var now = DateTime.UtcNow;

            await _tokenIssuanceRepository.AddAuditLogEntryAsync(new TokenIssuanceAuditLogEntry
            {
                AssetId = TestAssetId,
                TokenType = "ARC1400",
                TokenName = "RWA Bond Token",
                Network = "aramidmain-v1.0",
                DeployedBy = TestIssuerAddress,
                DeployedAt = now.AddDays(-60),
                Success = true
            });

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestIssuerAddress,
                PerformedAt = now.AddDays(-59),
                NewStatus = WhitelistStatus.Active,
                Network = "aramidmain-v1.0"
            });

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = "SYSTEM",
                PerformedAt = now.AddDays(-50),
                ToAddress = ValidAddress2,
                TransferAllowed = false,
                DenialReason = "Receiver suspended",
                Amount = 1000,
                Network = "aramidmain-v1.0"
            });

            // Act - Export as JSON
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = TestAssetId,
                Page = 1,
                PageSize = 100
            };

            var json = await _enterpriseAuditService.ExportAuditLogJsonAsync(request);

            // Assert
            Assert.That(json, Is.Not.Null);
            Assert.That(json, Does.Contain("\"success\": true"));
            Assert.That(json, Does.Contain("\"entries\""));
            Assert.That(json, Does.Contain("\"retentionPolicy\""));
            Assert.That(json, Does.Contain("aramidmain-v1.0"));
            Assert.That(json, Does.Contain("TransferValidation"));
            Assert.That(json, Does.Contain("Receiver suspended"));
            Assert.That(json, Does.Contain("\"transferAllowed\": false"));
        }

        [Test]
        public async Task EndToEnd_IssuerOwnership_Verification()
        {
            // Arrange - Create compliance metadata for the asset
            await _complianceRepository.UpsertMetadataAsync(new ComplianceMetadata
            {
                AssetId = TestAssetId,
                Network = "voimain-v1.0",
                CreatedBy = TestIssuerAddress,
                CreatedAt = DateTime.UtcNow
            });

            // Act & Assert - Verify issuer owns the asset
            var isOwner = await _complianceService.VerifyIssuerOwnsAssetAsync(TestIssuerAddress, TestAssetId);
            Assert.That(isOwner, Is.True);

            // Verify other issuer does not own the asset
            var isOtherOwner = await _complianceService.VerifyIssuerOwnsAssetAsync(OtherIssuerAddress, TestAssetId);
            Assert.That(isOtherOwner, Is.False);
        }

        [Test]
        public async Task EndToEnd_FilterByDateRange()
        {
            // Arrange - Create events at different times
            var now = DateTime.UtcNow;
            var fromDate = now.AddDays(-20);
            var toDate = now.AddDays(-10);

            // Event outside range (too old)
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestIssuerAddress,
                PerformedAt = now.AddDays(-30),
                Network = "voimain-v1.0"
            });

            // Event inside range
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = "SYSTEM",
                PerformedAt = now.AddDays(-15),
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Network = "voimain-v1.0"
            });

            // Event outside range (too recent)
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Update,
                PerformedBy = TestIssuerAddress,
                PerformedAt = now.AddDays(-5),
                Network = "voimain-v1.0"
            });

            // Act - Filter by date range
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = TestAssetId,
                FromDate = fromDate,
                ToDate = toDate
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert - Only event inside range should be returned
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Has.Count.EqualTo(1));
            Assert.That(result.Entries[0].Category, Is.EqualTo(AuditEventCategory.TransferValidation));
            Assert.That(result.Entries[0].PerformedAt, Is.GreaterThanOrEqualTo(fromDate));
            Assert.That(result.Entries[0].PerformedAt, Is.LessThanOrEqualTo(toDate));
        }

        [Test]
        public async Task EndToEnd_FilterByActionType()
        {
            // Arrange - Create different action types
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestIssuerAddress,
                PerformedAt = DateTime.UtcNow.AddDays(-5),
                Network = "voimain-v1.0"
            });

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = "SYSTEM",
                PerformedAt = DateTime.UtcNow.AddDays(-4),
                ToAddress = ValidAddress2,
                TransferAllowed = false,
                DenialReason = "Test denial",
                Network = "voimain-v1.0"
            });

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = TestAssetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Update,
                PerformedBy = TestIssuerAddress,
                PerformedAt = DateTime.UtcNow.AddDays(-3),
                Network = "voimain-v1.0"
            });

            // Act - Filter by TransferValidation action type
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = TestAssetId,
                Category = AuditEventCategory.TransferValidation
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert - Only TransferValidation events should be returned
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Has.Count.EqualTo(1));
            Assert.That(result.Entries[0].Category, Is.EqualTo(AuditEventCategory.TransferValidation));
            Assert.That(result.Entries[0].TransferAllowed, Is.False);
            Assert.That(result.Entries[0].DenialReason, Is.EqualTo("Test denial"));
        }
    }
}
