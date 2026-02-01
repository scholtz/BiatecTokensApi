using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for issuer profile management functionality
    /// </summary>
    [TestFixture]
    public class IssuerProfileServiceTests
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
                _mockMeteringService.Object,
                Mock.Of<IWebhookService>());
        }

        [Test]
        public async Task UpsertIssuerProfile_NewProfile_ShouldSucceed()
        {
            // Arrange
            var issuerAddress = "TESTISSUERADDRESS123456789012345678901234567890AB";
            var request = new UpsertIssuerProfileRequest
            {
                LegalName = "Test Corporation",
                CountryOfIncorporation = "US",
                EntityType = "Corporation",
                RegistrationNumber = "REG123456"
            };

            _mockRepository.Setup(r => r.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync((IssuerProfile?)null);
            _mockRepository.Setup(r => r.UpsertIssuerProfileAsync(It.IsAny<IssuerProfile>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpsertIssuerProfileAsync(request, issuerAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Profile, Is.Not.Null);
            Assert.That(result.Profile!.LegalName, Is.EqualTo("Test Corporation"));
            Assert.That(result.Profile.IssuerAddress, Is.EqualTo(issuerAddress));
            Assert.That(result.Profile.Status, Is.EqualTo(IssuerProfileStatus.Draft));
            _mockRepository.Verify(r => r.UpsertIssuerProfileAsync(It.IsAny<IssuerProfile>()), Times.Once);
        }

        [Test]
        public async Task UpsertIssuerProfile_UpdateExisting_ShouldPreserveCreationInfo()
        {
            // Arrange
            var issuerAddress = "TESTISSUERADDRESS123456789012345678901234567890AB";
            var existingProfile = new IssuerProfile
            {
                IssuerAddress = issuerAddress,
                LegalName = "Old Name",
                CreatedBy = issuerAddress,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                KybStatus = VerificationStatus.Verified,
                MicaLicenseStatus = MicaLicenseStatus.Approved,
                Status = IssuerProfileStatus.Verified
            };

            var request = new UpsertIssuerProfileRequest
            {
                LegalName = "Updated Corporation",
                CountryOfIncorporation = "DE"
            };

            _mockRepository.Setup(r => r.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync(existingProfile);
            _mockRepository.Setup(r => r.UpsertIssuerProfileAsync(It.IsAny<IssuerProfile>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpsertIssuerProfileAsync(request, issuerAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Profile!.LegalName, Is.EqualTo("Updated Corporation"));
            Assert.That(result.Profile.CreatedBy, Is.EqualTo(issuerAddress)); // Preserved
            Assert.That(result.Profile.KybStatus, Is.EqualTo(VerificationStatus.Verified)); // Preserved
        }

        [Test]
        public async Task GetIssuerProfile_ExistingProfile_ShouldReturnProfile()
        {
            // Arrange
            var issuerAddress = "TESTISSUERADDRESS123456789012345678901234567890AB";
            var profile = new IssuerProfile
            {
                IssuerAddress = issuerAddress,
                LegalName = "Test Corporation",
                KybStatus = VerificationStatus.Verified
            };

            _mockRepository.Setup(r => r.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync(profile);

            // Act
            var result = await _service.GetIssuerProfileAsync(issuerAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Profile, Is.Not.Null);
            Assert.That(result.Profile!.IssuerAddress, Is.EqualTo(issuerAddress));
        }

        [Test]
        public async Task GetIssuerProfile_NonExistent_ShouldReturnNotFound()
        {
            // Arrange
            var issuerAddress = "NONEXISTENT123456789012345678901234567890ABCDEF";

            _mockRepository.Setup(r => r.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync((IssuerProfile?)null);

            // Act
            var result = await _service.GetIssuerProfileAsync(issuerAddress);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Profile, Is.Null);
            Assert.That(result.ErrorMessage, Is.EqualTo("Issuer profile not found"));
        }

        [Test]
        public async Task GetIssuerVerification_FullyCompleteProfile_ShouldScore100()
        {
            // Arrange
            var issuerAddress = "TESTISSUERADDRESS123456789012345678901234567890AB";
            var profile = new IssuerProfile
            {
                IssuerAddress = issuerAddress,
                LegalName = "Test Corporation",
                CountryOfIncorporation = "US",
                RegistrationNumber = "REG123",
                RegisteredAddress = new IssuerAddress { AddressLine1 = "123 Main St", City = "New York" },
                PrimaryContact = new IssuerContact { Name = "John Doe", Email = "john@test.com" },
                KybStatus = VerificationStatus.Verified,
                MicaLicenseStatus = MicaLicenseStatus.Approved
            };

            _mockRepository.Setup(r => r.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync(profile);

            // Act
            var result = await _service.GetIssuerVerificationAsync(issuerAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.VerificationScore, Is.EqualTo(100));
            Assert.That(result.OverallStatus, Is.EqualTo(IssuerVerificationStatus.FullyVerified));
            Assert.That(result.IsProfileComplete, Is.True);
            Assert.That(result.MissingFields, Is.Empty);
        }

        [Test]
        public async Task GetIssuerVerification_PartialProfile_ShouldScorePartially()
        {
            // Arrange
            var issuerAddress = "TESTISSUERADDRESS123456789012345678901234567890AB";
            var profile = new IssuerProfile
            {
                IssuerAddress = issuerAddress,
                LegalName = "Test Corporation",
                CountryOfIncorporation = "US",
                KybStatus = VerificationStatus.InProgress,
                MicaLicenseStatus = MicaLicenseStatus.None
            };

            _mockRepository.Setup(r => r.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync(profile);

            // Act
            var result = await _service.GetIssuerVerificationAsync(issuerAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.VerificationScore, Is.LessThan(100));
            Assert.That(result.VerificationScore, Is.GreaterThan(0));
            Assert.That(result.MissingFields, Is.Not.Empty);
            Assert.That(result.MissingFields, Contains.Item("RegisteredAddress"));
            Assert.That(result.MissingFields, Contains.Item("MICA License"));
        }

        [Test]
        public async Task ListIssuerAssets_WithFilters_ShouldReturnFilteredAssets()
        {
            // Arrange
            var issuerAddress = "TESTISSUERADDRESS123456789012345678901234567890AB";
            var request = new ListIssuerAssetsRequest
            {
                Network = "voimain-v1.0",
                ComplianceStatus = ComplianceStatus.Compliant,
                Page = 1,
                PageSize = 20
            };

            var assetIds = new List<ulong> { 12345, 67890 };
            _mockRepository.Setup(r => r.ListIssuerAssetsAsync(issuerAddress, request))
                .ReturnsAsync(assetIds);
            _mockRepository.Setup(r => r.GetIssuerAssetCountAsync(issuerAddress, request))
                .ReturnsAsync(2);

            // Act
            var result = await _service.ListIssuerAssetsAsync(issuerAddress, request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.AssetIds, Has.Count.EqualTo(2));
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.IssuerAddress, Is.EqualTo(issuerAddress));
        }
    }
}
