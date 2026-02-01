using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Controller tests for compliance analytics endpoints
    /// Tests HTTP layer, input validation, authentication, and response handling
    /// </summary>
    [TestFixture]
    public class ComplianceAnalyticsControllerTests
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

        #region Regulatory Reporting Analytics Tests

        [Test]
        public async Task GetRegulatoryReportingAnalytics_ValidRequest_ReturnsOk()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.GetRegulatoryReportingAnalyticsAsync(
                It.IsAny<GetRegulatoryReportingAnalyticsRequest>(),
                TestUserAddress))
                .ReturnsAsync(new RegulatoryReportingAnalyticsResponse
                {
                    Success = true,
                    ComplianceSummary = new RegulatoryComplianceSummary
                    {
                        TotalAssets = 10,
                        MicaCompliantAssets = 8
                    }
                });

            // Act
            var result = await _controller.GetRegulatoryReportingAnalytics(
                network: "voimain-v1.0",
                tokenStandard: null,
                fromDate: fromDate,
                toDate: toDate,
                includeAssetDetails: false);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as RegulatoryReportingAnalyticsResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.ComplianceSummary.TotalAssets, Is.EqualTo(10));
        }

        [Test]
        public async Task GetRegulatoryReportingAnalytics_MissingFromDate_ReturnsBadRequest()
        {
            // Arrange
            DateTime? fromDate = null;
            var toDate = DateTime.UtcNow;

            // Act
            var result = await _controller.GetRegulatoryReportingAnalytics(
                network: null,
                tokenStandard: null,
                fromDate: fromDate,
                toDate: toDate,
                includeAssetDetails: false);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badRequest = result as BadRequestObjectResult;
            var response = badRequest!.Value as RegulatoryReportingAnalyticsResponse;
            Assert.That(response!.Success, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("fromDate and toDate are required"));
        }

        [Test]
        public async Task GetRegulatoryReportingAnalytics_MissingToDate_ReturnsBadRequest()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            DateTime? toDate = null;

            // Act
            var result = await _controller.GetRegulatoryReportingAnalytics(
                network: null,
                tokenStandard: null,
                fromDate: fromDate,
                toDate: toDate,
                includeAssetDetails: false);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GetRegulatoryReportingAnalytics_InvalidDateRange_ReturnsBadRequest()
        {
            // Arrange
            var fromDate = DateTime.UtcNow;
            var toDate = DateTime.UtcNow.AddDays(-30); // toDate before fromDate

            // Act
            var result = await _controller.GetRegulatoryReportingAnalytics(
                network: null,
                tokenStandard: null,
                fromDate: fromDate,
                toDate: toDate,
                includeAssetDetails: false);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badRequest = result as BadRequestObjectResult;
            var response = badRequest!.Value as RegulatoryReportingAnalyticsResponse;
            Assert.That(response!.ErrorMessage, Does.Contain("fromDate must be before or equal to toDate"));
        }

        [Test]
        public async Task GetRegulatoryReportingAnalytics_ServiceFailure_ReturnsInternalServerError()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.GetRegulatoryReportingAnalyticsAsync(
                It.IsAny<GetRegulatoryReportingAnalyticsRequest>(),
                TestUserAddress))
                .ReturnsAsync(new RegulatoryReportingAnalyticsResponse
                {
                    Success = false,
                    ErrorMessage = "Database error"
                });

            // Act
            var result = await _controller.GetRegulatoryReportingAnalytics(
                network: null,
                tokenStandard: null,
                fromDate: fromDate,
                toDate: toDate,
                includeAssetDetails: false);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region Audit Summary Aggregates Tests

        [Test]
        public async Task GetAuditSummaryAggregates_ValidRequest_ReturnsOk()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.GetAuditSummaryAggregatesAsync(
                It.IsAny<GetAuditSummaryAggregatesRequest>(),
                TestUserAddress))
                .ReturnsAsync(new AuditSummaryAggregatesResponse
                {
                    Success = true,
                    Summary = new AuditSummaryStatistics
                    {
                        TotalEvents = 50,
                        SuccessRate = 95.0
                    }
                });

            // Act
            var result = await _controller.GetAuditSummaryAggregates(
                assetId: null,
                network: "voimain-v1.0",
                fromDate: fromDate,
                toDate: toDate,
                period: AggregationPeriod.Daily);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as AuditSummaryAggregatesResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Summary.TotalEvents, Is.EqualTo(50));
        }

        [Test]
        public async Task GetAuditSummaryAggregates_MissingDateRange_ReturnsBadRequest()
        {
            // Arrange
            DateTime? fromDate = null;
            DateTime? toDate = null;

            // Act
            var result = await _controller.GetAuditSummaryAggregates(
                assetId: null,
                network: null,
                fromDate: fromDate,
                toDate: toDate,
                period: AggregationPeriod.Daily);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GetAuditSummaryAggregates_WithAssetIdFilter_PassesFilterToService()
        {
            // Arrange
            var assetId = 12345ul;
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.GetAuditSummaryAggregatesAsync(
                It.Is<GetAuditSummaryAggregatesRequest>(r => r.AssetId == assetId),
                TestUserAddress))
                .ReturnsAsync(new AuditSummaryAggregatesResponse
                {
                    Success = true,
                    Summary = new AuditSummaryStatistics()
                });

            // Act
            var result = await _controller.GetAuditSummaryAggregates(
                assetId: assetId,
                network: null,
                fromDate: fromDate,
                toDate: toDate,
                period: AggregationPeriod.Daily);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _serviceMock.Verify(s => s.GetAuditSummaryAggregatesAsync(
                It.Is<GetAuditSummaryAggregatesRequest>(r => r.AssetId == assetId),
                TestUserAddress), Times.Once);
        }

        [Test]
        public async Task GetAuditSummaryAggregates_WeeklyPeriod_PassesCorrectPeriodToService()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.GetAuditSummaryAggregatesAsync(
                It.Is<GetAuditSummaryAggregatesRequest>(r => r.Period == AggregationPeriod.Weekly),
                TestUserAddress))
                .ReturnsAsync(new AuditSummaryAggregatesResponse
                {
                    Success = true,
                    AggregationPeriod = AggregationPeriod.Weekly,
                    Summary = new AuditSummaryStatistics()
                });

            // Act
            var result = await _controller.GetAuditSummaryAggregates(
                assetId: null,
                network: null,
                fromDate: fromDate,
                toDate: toDate,
                period: AggregationPeriod.Weekly);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        #endregion

        #region Compliance Trends Tests

        [Test]
        public async Task GetComplianceTrends_ValidRequest_ReturnsOk()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.GetComplianceTrendsAsync(
                It.IsAny<GetComplianceTrendsRequest>(),
                TestUserAddress))
                .ReturnsAsync(new ComplianceTrendsResponse
                {
                    Success = true,
                    StatusTrends = new List<ComplianceStatusTrend>(),
                    MicaTrends = new List<MicaReadinessTrend>(),
                    WhitelistTrends = new List<WhitelistAdoptionTrend>(),
                    TrendDirection = "Improving"
                });

            // Act
            var result = await _controller.GetComplianceTrends(
                network: "voimain-v1.0",
                tokenStandard: null,
                fromDate: fromDate,
                toDate: toDate,
                period: AggregationPeriod.Weekly);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as ComplianceTrendsResponse;
            Assert.That(response!.Success, Is.True);
            Assert.That(response.TrendDirection, Is.EqualTo("Improving"));
        }

        [Test]
        public async Task GetComplianceTrends_MissingDateRange_ReturnsBadRequest()
        {
            // Arrange
            DateTime? fromDate = null;
            DateTime? toDate = null;

            // Act
            var result = await _controller.GetComplianceTrends(
                network: null,
                tokenStandard: null,
                fromDate: fromDate,
                toDate: toDate,
                period: AggregationPeriod.Weekly);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GetComplianceTrends_InvalidDateRange_ReturnsBadRequest()
        {
            // Arrange
            var fromDate = DateTime.UtcNow;
            var toDate = DateTime.UtcNow.AddDays(-30);

            // Act
            var result = await _controller.GetComplianceTrends(
                network: null,
                tokenStandard: null,
                fromDate: fromDate,
                toDate: toDate,
                period: AggregationPeriod.Weekly);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task GetComplianceTrends_WithNetworkAndTokenStandardFilters_PassesToService()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;
            var network = "aramidmain-v1.0";
            var tokenStandard = "ARC200";

            _serviceMock.Setup(s => s.GetComplianceTrendsAsync(
                It.Is<GetComplianceTrendsRequest>(r => 
                    r.Network == network && 
                    r.TokenStandard == tokenStandard),
                TestUserAddress))
                .ReturnsAsync(new ComplianceTrendsResponse
                {
                    Success = true,
                    TrendDirection = "Stable"
                });

            // Act
            var result = await _controller.GetComplianceTrends(
                network: network,
                tokenStandard: tokenStandard,
                fromDate: fromDate,
                toDate: toDate,
                period: AggregationPeriod.Weekly);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            _serviceMock.Verify(s => s.GetComplianceTrendsAsync(
                It.Is<GetComplianceTrendsRequest>(r => 
                    r.Network == network && 
                    r.TokenStandard == tokenStandard),
                TestUserAddress), Times.Once);
        }

        [Test]
        public async Task GetComplianceTrends_MonthlyPeriod_PassesCorrectPeriodToService()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddMonths(-6);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.GetComplianceTrendsAsync(
                It.Is<GetComplianceTrendsRequest>(r => r.Period == AggregationPeriod.Monthly),
                TestUserAddress))
                .ReturnsAsync(new ComplianceTrendsResponse
                {
                    Success = true,
                    AggregationPeriod = AggregationPeriod.Monthly,
                    TrendDirection = "Stable"
                });

            // Act
            var result = await _controller.GetComplianceTrends(
                network: null,
                tokenStandard: null,
                fromDate: fromDate,
                toDate: toDate,
                period: AggregationPeriod.Monthly);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        #endregion

        #region Exception Handling Tests

        [Test]
        public async Task GetRegulatoryReportingAnalytics_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.GetRegulatoryReportingAnalyticsAsync(
                It.IsAny<GetRegulatoryReportingAnalyticsRequest>(),
                TestUserAddress))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.GetRegulatoryReportingAnalytics(
                network: null,
                tokenStandard: null,
                fromDate: fromDate,
                toDate: toDate,
                includeAssetDetails: false);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task GetAuditSummaryAggregates_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.GetAuditSummaryAggregatesAsync(
                It.IsAny<GetAuditSummaryAggregatesRequest>(),
                TestUserAddress))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.GetAuditSummaryAggregates(
                assetId: null,
                network: null,
                fromDate: fromDate,
                toDate: toDate,
                period: AggregationPeriod.Daily);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        [Test]
        public async Task GetComplianceTrends_ServiceThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;

            _serviceMock.Setup(s => s.GetComplianceTrendsAsync(
                It.IsAny<GetComplianceTrendsRequest>(),
                TestUserAddress))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.GetComplianceTrends(
                network: null,
                tokenStandard: null,
                fromDate: fromDate,
                toDate: toDate,
                period: AggregationPeriod.Weekly);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion
    }
}
