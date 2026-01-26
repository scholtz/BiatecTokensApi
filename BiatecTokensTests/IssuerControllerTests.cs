using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for IssuerController endpoints
    /// </summary>
    [TestFixture]
    public class IssuerControllerTests
    {
        private Mock<IComplianceService> _mockService;
        private Mock<IEnterpriseAuditService> _mockAuditService;
        private Mock<ILogger<IssuerController>> _mockLogger;
        private IssuerController _controller;

        [SetUp]
        public void Setup()
        {
            _mockService = new Mock<IComplianceService>();
            _mockAuditService = new Mock<IEnterpriseAuditService>();
            _mockLogger = new Mock<ILogger<IssuerController>>();
            _controller = new IssuerController(_mockService.Object, _mockAuditService.Object, _mockLogger.Object);

            // Setup authentication context
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "TESTUSER123456789012345678901234567890ABCDEF")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };
        }

        [Test]
        public async Task GetIssuerProfile_ExistingProfile_ReturnsOk()
        {
            // Arrange
            var issuerAddress = "ISSUER123456789012345678901234567890ABCDEFGH";
            var profile = new IssuerProfile
            {
                IssuerAddress = issuerAddress,
                LegalName = "Test Corporation"
            };

            _mockService.Setup(s => s.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync(new IssuerProfileResponse
                {
                    Success = true,
                    Profile = profile
                });

            // Act
            var result = await _controller.GetIssuerProfile(issuerAddress);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult!.Value, Is.InstanceOf<IssuerProfileResponse>());
            var response = okResult.Value as IssuerProfileResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Profile!.IssuerAddress, Is.EqualTo(issuerAddress));
        }

        [Test]
        public async Task GetIssuerProfile_NotFound_ReturnsNotFound()
        {
            // Arrange
            var issuerAddress = "NONEXISTENT1234567890123456789012345678901234";

            _mockService.Setup(s => s.GetIssuerProfileAsync(issuerAddress))
                .ReturnsAsync(new IssuerProfileResponse
                {
                    Success = false,
                    ErrorMessage = "Issuer profile not found"
                });

            // Act
            var result = await _controller.GetIssuerProfile(issuerAddress);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task UpsertIssuerProfile_ValidRequest_ReturnsOk()
        {
            // Arrange
            var request = new UpsertIssuerProfileRequest
            {
                LegalName = "Test Corporation",
                CountryOfIncorporation = "US",
                EntityType = "Corporation"
            };

            var profile = new IssuerProfile
            {
                IssuerAddress = "TESTUSER123456789012345678901234567890ABCDEF",
                LegalName = "Test Corporation"
            };

            _mockService.Setup(s => s.UpsertIssuerProfileAsync(request, It.IsAny<string>()))
                .ReturnsAsync(new IssuerProfileResponse
                {
                    Success = true,
                    Profile = profile
                });

            // Act
            var result = await _controller.UpsertIssuerProfile(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as IssuerProfileResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Profile, Is.Not.Null);
        }

        [Test]
        public async Task GetIssuerVerification_ExistingProfile_ReturnsOk()
        {
            // Arrange
            var issuerAddress = "ISSUER123456789012345678901234567890ABCDEFGH";

            _mockService.Setup(s => s.GetIssuerVerificationAsync(issuerAddress))
                .ReturnsAsync(new IssuerVerificationResponse
                {
                    Success = true,
                    IssuerAddress = issuerAddress,
                    VerificationScore = 85,
                    OverallStatus = IssuerVerificationStatus.PartiallyVerified
                });

            // Act
            var result = await _controller.GetIssuerVerification(issuerAddress);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as IssuerVerificationResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.VerificationScore, Is.EqualTo(85));
        }

        [Test]
        public async Task ListIssuerAssets_WithFilters_ReturnsOk()
        {
            // Arrange
            var issuerAddress = "ISSUER123456789012345678901234567890ABCDEFGH";
            var assetIds = new List<ulong> { 12345, 67890 };

            _mockService.Setup(s => s.ListIssuerAssetsAsync(issuerAddress, It.IsAny<ListIssuerAssetsRequest>()))
                .ReturnsAsync(new IssuerAssetsResponse
                {
                    Success = true,
                    IssuerAddress = issuerAddress,
                    AssetIds = assetIds,
                    TotalCount = 2
                });

            // Act
            var result = await _controller.ListIssuerAssets(issuerAddress);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as IssuerAssetsResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.AssetIds, Has.Count.EqualTo(2));
        }
    }
}
