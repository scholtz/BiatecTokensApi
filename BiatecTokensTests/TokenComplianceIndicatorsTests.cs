using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TokenComplianceIndicatorsTests
    {
        private Mock<IComplianceRepository> _repositoryMock;
        private Mock<IWhitelistService> _whitelistServiceMock;
        private Mock<ILogger<ComplianceService>> _loggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private ComplianceService _service;

        private const ulong TestAssetId = 12345;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IComplianceRepository>();
            _whitelistServiceMock = new Mock<IWhitelistService>();
            _loggerMock = new Mock<ILogger<ComplianceService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _service = new ComplianceService(_repositoryMock.Object, _whitelistServiceMock.Object, _loggerMock.Object, _meteringServiceMock.Object, Mock.Of<IWebhookService>());
        }

        [Test]
        public async Task GetComplianceIndicators_WithCompleteMetadata_ShouldReturnFullIndicators()
        {
            // Arrange
            var complianceMetadata = new ComplianceMetadata
            {
                AssetId = TestAssetId,
                ComplianceStatus = ComplianceStatus.Compliant,
                VerificationStatus = VerificationStatus.Verified,
                RegulatoryFramework = "MICA",
                Jurisdiction = "EU",
                TransferRestrictions = "KYC required",
                RequiresAccreditedInvestors = true,
                MaxHolders = 100,
                Network = "voimain-v1.0",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(TestAssetId))
                .ReturnsAsync(complianceMetadata);

            var whitelistResponse = new WhitelistListResponse
            {
                Success = true,
                TotalCount = 50,
                Entries = new List<WhitelistEntry>()
            };

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(whitelistResponse);

            // Act
            var result = await _service.GetComplianceIndicatorsAsync(TestAssetId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Indicators, Is.Not.Null);
            Assert.That(result.Indicators!.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(result.Indicators.IsMicaReady, Is.True);
            Assert.That(result.Indicators.WhitelistingEnabled, Is.True);
            Assert.That(result.Indicators.WhitelistedAddressCount, Is.EqualTo(50));
            Assert.That(result.Indicators.HasTransferRestrictions, Is.True);
            Assert.That(result.Indicators.TransferRestrictions, Is.EqualTo("KYC required"));
            Assert.That(result.Indicators.RequiresAccreditedInvestors, Is.True);
            Assert.That(result.Indicators.EnterpriseReadinessScore, Is.EqualTo(100)); // All factors met
            Assert.That(result.Indicators.ComplianceStatus, Is.EqualTo("Compliant"));
            Assert.That(result.Indicators.VerificationStatus, Is.EqualTo("Verified"));
            Assert.That(result.Indicators.RegulatoryFramework, Is.EqualTo("MICA"));
            Assert.That(result.Indicators.Jurisdiction, Is.EqualTo("EU"));
            Assert.That(result.Indicators.MaxHolders, Is.EqualTo(100));
            Assert.That(result.Indicators.Network, Is.EqualTo("voimain-v1.0"));
            Assert.That(result.Indicators.HasComplianceMetadata, Is.True);
        }

        [Test]
        public async Task GetComplianceIndicators_WithoutMetadata_ShouldReturnBasicIndicators()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(TestAssetId))
                .ReturnsAsync((ComplianceMetadata?)null);

            var whitelistResponse = new WhitelistListResponse
            {
                Success = true,
                TotalCount = 0,
                Entries = new List<WhitelistEntry>()
            };

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(whitelistResponse);

            // Act
            var result = await _service.GetComplianceIndicatorsAsync(TestAssetId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Indicators, Is.Not.Null);
            Assert.That(result.Indicators!.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(result.Indicators.IsMicaReady, Is.False);
            Assert.That(result.Indicators.WhitelistingEnabled, Is.False);
            Assert.That(result.Indicators.WhitelistedAddressCount, Is.EqualTo(0));
            Assert.That(result.Indicators.HasTransferRestrictions, Is.False);
            Assert.That(result.Indicators.RequiresAccreditedInvestors, Is.False);
            Assert.That(result.Indicators.EnterpriseReadinessScore, Is.EqualTo(0));
            Assert.That(result.Indicators.HasComplianceMetadata, Is.False);
        }

        [Test]
        public async Task GetComplianceIndicators_MicaReadyRequirements_MustHaveCompliantStatus()
        {
            // Arrange - Non-compliant status
            var complianceMetadata = new ComplianceMetadata
            {
                AssetId = TestAssetId,
                ComplianceStatus = ComplianceStatus.NonCompliant,
                RegulatoryFramework = "MICA",
                Jurisdiction = "EU"
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(TestAssetId))
                .ReturnsAsync(complianceMetadata);

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse { Success = true, TotalCount = 0 });

            // Act
            var result = await _service.GetComplianceIndicatorsAsync(TestAssetId);

            // Assert
            Assert.That(result.Indicators!.IsMicaReady, Is.False, "Should not be MICA ready with NonCompliant status");
        }

        [Test]
        public async Task GetComplianceIndicators_MicaReadyRequirements_MustHaveRegulatoryFramework()
        {
            // Arrange - Missing regulatory framework
            var complianceMetadata = new ComplianceMetadata
            {
                AssetId = TestAssetId,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = null,
                Jurisdiction = "EU"
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(TestAssetId))
                .ReturnsAsync(complianceMetadata);

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse { Success = true, TotalCount = 0 });

            // Act
            var result = await _service.GetComplianceIndicatorsAsync(TestAssetId);

            // Assert
            Assert.That(result.Indicators!.IsMicaReady, Is.False, "Should not be MICA ready without regulatory framework");
        }

        [Test]
        public async Task GetComplianceIndicators_MicaReadyRequirements_MustHaveJurisdiction()
        {
            // Arrange - Missing jurisdiction
            var complianceMetadata = new ComplianceMetadata
            {
                AssetId = TestAssetId,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = "MICA",
                Jurisdiction = null
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(TestAssetId))
                .ReturnsAsync(complianceMetadata);

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse { Success = true, TotalCount = 0 });

            // Act
            var result = await _service.GetComplianceIndicatorsAsync(TestAssetId);

            // Assert
            Assert.That(result.Indicators!.IsMicaReady, Is.False, "Should not be MICA ready without jurisdiction");
        }

        [Test]
        public async Task GetComplianceIndicators_EnterpriseScore_CalculatesCorrectly()
        {
            // Arrange - Partial compliance
            var complianceMetadata = new ComplianceMetadata
            {
                AssetId = TestAssetId,
                ComplianceStatus = ComplianceStatus.UnderReview,
                VerificationStatus = VerificationStatus.Pending,
                RegulatoryFramework = "MICA",
                Jurisdiction = null  // Missing jurisdiction
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(TestAssetId))
                .ReturnsAsync(complianceMetadata);

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse { Success = true, TotalCount = 10 });

            // Act
            var result = await _service.GetComplianceIndicatorsAsync(TestAssetId);

            // Assert
            // Score breakdown:
            // - Has metadata: 30 points
            // - Has whitelist: 25 points
            // - Verified KYC: 0 points (Pending, not Verified)
            // - Has regulatory framework: 15 points
            // - Has jurisdiction: 0 points (missing)
            // Total: 70 points
            Assert.That(result.Indicators!.EnterpriseReadinessScore, Is.EqualTo(70));
        }

        [Test]
        public async Task GetComplianceIndicators_WithWhitelistOnly_ShouldIncludeWhitelistData()
        {
            // Arrange - No compliance metadata, but has whitelist
            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(TestAssetId))
                .ReturnsAsync((ComplianceMetadata?)null);

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse { Success = true, TotalCount = 25 });

            // Act
            var result = await _service.GetComplianceIndicatorsAsync(TestAssetId);

            // Assert
            Assert.That(result.Indicators!.WhitelistingEnabled, Is.True);
            Assert.That(result.Indicators.WhitelistedAddressCount, Is.EqualTo(25));
            Assert.That(result.Indicators.EnterpriseReadinessScore, Is.EqualTo(25)); // Only whitelist points
        }

        [Test]
        public async Task GetComplianceIndicators_ExemptStatus_ShouldBeMicaReady()
        {
            // Arrange - Exempt status should also qualify for MICA readiness
            var complianceMetadata = new ComplianceMetadata
            {
                AssetId = TestAssetId,
                ComplianceStatus = ComplianceStatus.Exempt,
                RegulatoryFramework = "MICA",
                Jurisdiction = "EU"
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(TestAssetId))
                .ReturnsAsync(complianceMetadata);

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse { Success = true, TotalCount = 0 });

            // Act
            var result = await _service.GetComplianceIndicatorsAsync(TestAssetId);

            // Assert
            Assert.That(result.Indicators!.IsMicaReady, Is.True, "Exempt status should qualify for MICA readiness");
        }

        [Test]
        public async Task GetComplianceIndicators_ErrorInWhitelistService_ShouldReturnError()
        {
            // Arrange
            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(TestAssetId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _service.GetComplianceIndicatorsAsync(TestAssetId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Failed to retrieve compliance indicators"));
        }
    }
}
