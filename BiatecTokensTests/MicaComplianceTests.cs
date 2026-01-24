using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for MICA compliance checklist and health functionality
    /// </summary>
    [TestFixture]
    public class MicaComplianceTests
    {
        private Mock<IComplianceRepository> _mockRepository;
        private Mock<IWhitelistService> _mockWhitelistService;
        private Mock<ILogger<ComplianceService>> _mockLogger;
        private Mock<ISubscriptionMeteringService> _mockMeteringService;
        private ComplianceService _service;

        [SetUp]
        public void Setup()
        {
            _mockRepository = new Mock<IComplianceRepository>();
            _mockWhitelistService = new Mock<IWhitelistService>();
            _mockLogger = new Mock<ILogger<ComplianceService>>();
            _mockMeteringService = new Mock<ISubscriptionMeteringService>();
            _service = new ComplianceService(
                _mockRepository.Object,
                _mockWhitelistService.Object,
                _mockLogger.Object,
                _mockMeteringService.Object);
        }

        [Test]
        public async Task GetMicaComplianceChecklist_FullyCompliant_ShouldReturn100Percent()
        {
            // Arrange
            var assetId = 12345ul;
            var metadata = new ComplianceMetadata
            {
                AssetId = assetId,
                CreatedBy = "ISSUER123456789012345678901234567890ABCDEFGH",
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = "MICA",
                Jurisdiction = "EU",
                VerificationStatus = VerificationStatus.Verified,
                KycProvider = "Sumsub",
                KycVerificationDate = DateTime.UtcNow.AddDays(-30),
                Notes = "https://whitepaper.example.com",
                LastComplianceReview = DateTime.UtcNow.AddDays(-15)
            };

            var whitelistEntries = new List<WhitelistEntry>
            {
                new WhitelistEntry { Address = "ADDR1", Status = WhitelistStatus.Active }
            };

            _mockRepository.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(metadata);

            _mockWhitelistService.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = whitelistEntries
                });

            // Act
            var result = await _service.GetMicaComplianceChecklistAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Checklist, Is.Not.Null);
            Assert.That(result.Checklist!.CompliancePercentage, Is.EqualTo(100));
            Assert.That(result.Checklist.OverallStatus, Is.EqualTo(MicaComplianceStatus.FullyCompliant));
            Assert.That(result.Checklist.Requirements, Has.Count.EqualTo(6));
            Assert.That(result.Checklist.Requirements.All(r => r.IsMet), Is.True);
        }

        [Test]
        public async Task GetMicaComplianceChecklist_PartialCompliance_ShouldCalculatePercentage()
        {
            // Arrange
            var assetId = 12345ul;
            var metadata = new ComplianceMetadata
            {
                AssetId = assetId,
                CreatedBy = "ISSUER123456789012345678901234567890ABCDEFGH",
                ComplianceStatus = ComplianceStatus.UnderReview
                // Missing regulatory framework, verification, etc.
            };

            _mockRepository.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(metadata);

            _mockWhitelistService.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry>()
                });

            // Act
            var result = await _service.GetMicaComplianceChecklistAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Checklist, Is.Not.Null);
            Assert.That(result.Checklist!.CompliancePercentage, Is.LessThan(100));
            Assert.That(result.Checklist.CompliancePercentage, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.Checklist.OverallStatus, Is.Not.EqualTo(MicaComplianceStatus.FullyCompliant));
        }

        [Test]
        public async Task GetMicaComplianceChecklist_ShouldIncludeAllSixRequirements()
        {
            // Arrange
            var assetId = 12345ul;

            _mockRepository.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync((ComplianceMetadata?)null);

            _mockWhitelistService.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry>()
                });

            // Act
            var result = await _service.GetMicaComplianceChecklistAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Checklist!.Requirements, Has.Count.EqualTo(6));
            
            var requirementIds = result.Checklist.Requirements.Select(r => r.Id).ToList();
            Assert.That(requirementIds, Contains.Item("MICA-ART35")); // Issuer identification
            Assert.That(requirementIds, Contains.Item("MICA-ART36")); // White paper
            Assert.That(requirementIds, Contains.Item("MICA-ART41")); // Prudential safeguards
            Assert.That(requirementIds, Contains.Item("MICA-ART45")); // Transfer restrictions
            Assert.That(requirementIds, Contains.Item("MICA-ART59")); // AML/CTF
            Assert.That(requirementIds, Contains.Item("MICA-ART60")); // Record keeping
        }

        [Test]
        public async Task GetComplianceHealth_MultipleTokens_ShouldAggregateMetrics()
        {
            // Arrange
            var issuerAddress = "ISSUER123456789012345678901234567890ABCDEFGH";
            var assetIds = new List<ulong> { 12345, 67890, 11111 };

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 12345,
                    CreatedBy = issuerAddress,
                    ComplianceStatus = ComplianceStatus.Compliant,
                    RegulatoryFramework = "MICA",
                    Jurisdiction = "EU"
                },
                new ComplianceMetadata
                {
                    AssetId = 67890,
                    CreatedBy = issuerAddress,
                    ComplianceStatus = ComplianceStatus.UnderReview
                },
                new ComplianceMetadata
                {
                    AssetId = 11111,
                    CreatedBy = issuerAddress,
                    ComplianceStatus = ComplianceStatus.NonCompliant
                }
            };

            var request = new ListIssuerAssetsRequest { Page = 1, PageSize = 100 };

            _mockRepository.Setup(r => r.ListIssuerAssetsAsync(issuerAddress, It.IsAny<ListIssuerAssetsRequest>()))
                .ReturnsAsync(assetIds);

            foreach (var metadata in metadataList)
            {
                _mockRepository.Setup(r => r.GetMetadataByAssetIdAsync(metadata.AssetId))
                    .ReturnsAsync(metadata);
            }

            _mockWhitelistService.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry> { new WhitelistEntry() }
                });

            _mockRepository.Setup(r => r.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync(new IssuerProfile
                {
                    IssuerAddress = issuerAddress,
                    KybStatus = VerificationStatus.Verified
                });

            // Act
            var result = await _service.GetComplianceHealthAsync(issuerAddress, null);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalTokens, Is.EqualTo(3));
            Assert.That(result.CompliantTokens, Is.EqualTo(1));
            Assert.That(result.UnderReviewTokens, Is.EqualTo(1));
            Assert.That(result.NonCompliantTokens, Is.EqualTo(1));
            Assert.That(result.IssuerVerified, Is.True);
        }

        [Test]
        public async Task GetComplianceHealth_NonCompliantTokens_ShouldGenerateAlerts()
        {
            // Arrange
            var issuerAddress = "ISSUER123456789012345678901234567890ABCDEFGH";
            var assetIds = new List<ulong> { 12345 };

            var metadata = new ComplianceMetadata
            {
                AssetId = 12345,
                CreatedBy = issuerAddress,
                ComplianceStatus = ComplianceStatus.NonCompliant
            };

            _mockRepository.Setup(r => r.ListIssuerAssetsAsync(issuerAddress, It.IsAny<ListIssuerAssetsRequest>()))
                .ReturnsAsync(assetIds);

            _mockRepository.Setup(r => r.GetMetadataByAssetIdAsync(12345))
                .ReturnsAsync(metadata);

            _mockWhitelistService.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry>()
                });

            _mockRepository.Setup(r => r.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync((IssuerProfile?)null);

            // Act
            var result = await _service.GetComplianceHealthAsync(issuerAddress, null);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Alerts, Is.Not.Empty);
            Assert.That(result.Alerts.Any(a => a.Severity == "Error"), Is.True);
            Assert.That(result.Recommendations, Is.Not.Empty);
        }

        [Test]
        public async Task GetComplianceHealth_NoTokens_ShouldReturnZeroScore()
        {
            // Arrange
            var issuerAddress = "ISSUER123456789012345678901234567890ABCDEFGH";

            _mockRepository.Setup(r => r.ListIssuerAssetsAsync(issuerAddress, It.IsAny<ListIssuerAssetsRequest>()))
                .ReturnsAsync(new List<ulong>());

            _mockRepository.Setup(r => r.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync((IssuerProfile?)null);

            // Act
            var result = await _service.GetComplianceHealthAsync(issuerAddress, null);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalTokens, Is.EqualTo(0));
            Assert.That(result.OverallHealthScore, Is.EqualTo(0));
        }

        [Test]
        public async Task GetComplianceHealth_VerifiedIssuerCompliantTokens_ShouldScoreHigh()
        {
            // Arrange
            var issuerAddress = "ISSUER123456789012345678901234567890ABCDEFGH";
            var assetIds = new List<ulong> { 12345 };

            var metadata = new ComplianceMetadata
            {
                AssetId = 12345,
                CreatedBy = issuerAddress,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = "MICA",
                Jurisdiction = "EU"
            };

            _mockRepository.Setup(r => r.ListIssuerAssetsAsync(issuerAddress, It.IsAny<ListIssuerAssetsRequest>()))
                .ReturnsAsync(assetIds);

            _mockRepository.Setup(r => r.GetMetadataByAssetIdAsync(12345))
                .ReturnsAsync(metadata);

            _mockWhitelistService.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry> { new WhitelistEntry() }
                });

            _mockRepository.Setup(r => r.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync(new IssuerProfile
                {
                    IssuerAddress = issuerAddress,
                    KybStatus = VerificationStatus.Verified
                });

            // Act
            var result = await _service.GetComplianceHealthAsync(issuerAddress, null);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.OverallHealthScore, Is.GreaterThan(80));
            Assert.That(result.IssuerVerified, Is.True);
            Assert.That(result.MicaReadyTokens, Is.EqualTo(1));
        }
    }
}
