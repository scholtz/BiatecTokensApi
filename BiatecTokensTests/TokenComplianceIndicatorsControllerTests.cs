using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class TokenComplianceIndicatorsControllerTests
    {
        private Mock<IERC20TokenService> _erc20TokenServiceMock;
        private Mock<IARC3TokenService> _arc3TokenServiceMock;
        private Mock<IASATokenService> _asaTokenServiceMock;
        private Mock<IARC200TokenService> _arc200TokenServiceMock;
        private Mock<IARC1400TokenService> _arc1400TokenServiceMock;
        private Mock<IComplianceService> _complianceServiceMock;
        private Mock<ILogger<TokenController>> _loggerMock;
        private TokenController _controller;

        private const ulong TestAssetId = 12345;

        [SetUp]
        public void Setup()
        {
            _erc20TokenServiceMock = new Mock<IERC20TokenService>();
            _arc3TokenServiceMock = new Mock<IARC3TokenService>();
            _asaTokenServiceMock = new Mock<IASATokenService>();
            _arc200TokenServiceMock = new Mock<IARC200TokenService>();
            _arc1400TokenServiceMock = new Mock<IARC1400TokenService>();
            _complianceServiceMock = new Mock<IComplianceService>();
            _loggerMock = new Mock<ILogger<TokenController>>();
            var envMock = new Mock<IHostEnvironment>();

            _controller = new TokenController(
                _erc20TokenServiceMock.Object,
                _arc3TokenServiceMock.Object,
                _asaTokenServiceMock.Object,
                _arc200TokenServiceMock.Object,
                _arc1400TokenServiceMock.Object,
                _complianceServiceMock.Object,
                _loggerMock.Object,
                envMock.Object);
        }

        [Test]
        public async Task GetComplianceIndicators_Success_ReturnsOkWithIndicators()
        {
            // Arrange
            var indicators = new TokenComplianceIndicators
            {
                AssetId = TestAssetId,
                IsMicaReady = true,
                WhitelistingEnabled = true,
                WhitelistedAddressCount = 50,
                HasTransferRestrictions = true,
                TransferRestrictions = "KYC required",
                RequiresAccreditedInvestors = true,
                ComplianceStatus = "Compliant",
                VerificationStatus = "Verified",
                RegulatoryFramework = "MICA",
                Jurisdiction = "EU",
                MaxHolders = 100,
                EnterpriseReadinessScore = 100,
                Network = "voimain-v1.0",
                HasComplianceMetadata = true
            };

            var response = new TokenComplianceIndicatorsResponse
            {
                Success = true,
                Indicators = indicators
            };

            _complianceServiceMock.Setup(s => s.GetComplianceIndicatorsAsync(TestAssetId))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetComplianceIndicators(TestAssetId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            Assert.That(okResult!.Value, Is.InstanceOf<TokenComplianceIndicatorsResponse>());
            
            var actualResponse = okResult.Value as TokenComplianceIndicatorsResponse;
            Assert.That(actualResponse!.Success, Is.True);
            Assert.That(actualResponse.Indicators, Is.Not.Null);
            Assert.That(actualResponse.Indicators!.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(actualResponse.Indicators.IsMicaReady, Is.True);
            Assert.That(actualResponse.Indicators.WhitelistingEnabled, Is.True);
            Assert.That(actualResponse.Indicators.EnterpriseReadinessScore, Is.EqualTo(100));
        }

        [Test]
        public async Task GetComplianceIndicators_ServiceFailure_ReturnsInternalServerError()
        {
            // Arrange
            var response = new TokenComplianceIndicatorsResponse
            {
                Success = false,
                ErrorMessage = "Internal service error"
            };

            _complianceServiceMock.Setup(s => s.GetComplianceIndicatorsAsync(TestAssetId))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetComplianceIndicators(TestAssetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            
            var actualResponse = objectResult.Value as TokenComplianceIndicatorsResponse;
            Assert.That(actualResponse!.Success, Is.False);
            Assert.That(actualResponse.ErrorMessage, Does.Contain("Internal service error"));
        }

        [Test]
        public async Task GetComplianceIndicators_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            _complianceServiceMock.Setup(s => s.GetComplianceIndicatorsAsync(TestAssetId))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.GetComplianceIndicators(TestAssetId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
            
            var actualResponse = objectResult.Value as TokenComplianceIndicatorsResponse;
            Assert.That(actualResponse!.Success, Is.False);
            Assert.That(actualResponse.ErrorMessage, Does.Contain("Internal error"));
        }

        [Test]
        public async Task GetComplianceIndicators_NoMetadata_ReturnsBasicIndicators()
        {
            // Arrange
            var indicators = new TokenComplianceIndicators
            {
                AssetId = TestAssetId,
                IsMicaReady = false,
                WhitelistingEnabled = false,
                WhitelistedAddressCount = 0,
                HasTransferRestrictions = false,
                RequiresAccreditedInvestors = false,
                EnterpriseReadinessScore = 0,
                HasComplianceMetadata = false
            };

            var response = new TokenComplianceIndicatorsResponse
            {
                Success = true,
                Indicators = indicators
            };

            _complianceServiceMock.Setup(s => s.GetComplianceIndicatorsAsync(TestAssetId))
                .ReturnsAsync(response);

            // Act
            var result = await _controller.GetComplianceIndicators(TestAssetId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var actualResponse = okResult!.Value as TokenComplianceIndicatorsResponse;
            
            Assert.That(actualResponse!.Indicators!.HasComplianceMetadata, Is.False);
            Assert.That(actualResponse.Indicators.IsMicaReady, Is.False);
            Assert.That(actualResponse.Indicators.EnterpriseReadinessScore, Is.EqualTo(0));
        }

        [Test]
        public async Task GetComplianceIndicators_VerifiesLogging_OnSuccess()
        {
            // Arrange
            var response = new TokenComplianceIndicatorsResponse
            {
                Success = true,
                Indicators = new TokenComplianceIndicators { AssetId = TestAssetId }
            };

            _complianceServiceMock.Setup(s => s.GetComplianceIndicatorsAsync(TestAssetId))
                .ReturnsAsync(response);

            // Act
            await _controller.GetComplianceIndicators(TestAssetId);

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Retrieved compliance indicators for asset {TestAssetId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task GetComplianceIndicators_VerifiesLogging_OnFailure()
        {
            // Arrange
            var response = new TokenComplianceIndicatorsResponse
            {
                Success = false,
                ErrorMessage = "Test error"
            };

            _complianceServiceMock.Setup(s => s.GetComplianceIndicatorsAsync(TestAssetId))
                .ReturnsAsync(response);

            // Act
            await _controller.GetComplianceIndicators(TestAssetId);

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Failed to retrieve compliance indicators for asset {TestAssetId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Test]
        public async Task GetComplianceIndicators_VerifiesLogging_OnException()
        {
            // Arrange
            _complianceServiceMock.Setup(s => s.GetComplianceIndicatorsAsync(TestAssetId))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            await _controller.GetComplianceIndicators(TestAssetId);

            // Assert
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error retrieving compliance indicators for asset {TestAssetId}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
