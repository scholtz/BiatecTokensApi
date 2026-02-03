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
    [TestFixture]
    public class ComplianceReportControllerTests
    {
        private Mock<IComplianceReportService> _serviceMock;
        private Mock<ILogger<ComplianceReportController>> _loggerMock;
        private ComplianceReportController _controller;

        private const string TestIssuerId = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const ulong TestAssetId = 12345;
        private const string TestNetwork = "voimain-v1.0";

        [SetUp]
        public void Setup()
        {
            _serviceMock = new Mock<IComplianceReportService>();
            _loggerMock = new Mock<ILogger<ComplianceReportController>>();
            _controller = new ComplianceReportController(_serviceMock.Object, _loggerMock.Object);

            // Set up user context for authentication
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestIssuerId)
            }, "TestAuth"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        #region CreateReport Tests

        [Test]
        public async Task CreateReport_ValidRequest_ShouldReturnOk()
        {
            // Arrange
            var request = new CreateComplianceReportRequest
            {
                ReportType = ReportType.MicaReadiness,
                AssetId = TestAssetId,
                Network = TestNetwork
            };

            var reportId = Guid.NewGuid().ToString();
            _serviceMock.Setup(s => s.CreateReportAsync(request, TestIssuerId))
                .ReturnsAsync(new CreateComplianceReportResponse
                {
                    Success = true,
                    ReportId = reportId,
                    Status = ReportStatus.Completed,
                    CreatedAt = DateTime.UtcNow
                });

            // Act
            var result = await _controller.CreateReport(request);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as CreateComplianceReportResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.True);
            Assert.That(response.ReportId, Is.EqualTo(reportId));
        }

        [Test]
        public async Task CreateReport_ServiceFailure_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CreateComplianceReportRequest
            {
                ReportType = ReportType.MicaReadiness
            };

            _serviceMock.Setup(s => s.CreateReportAsync(request, TestIssuerId))
                .ReturnsAsync(new CreateComplianceReportResponse
                {
                    Success = false,
                    ErrorMessage = "Failed to create report"
                });

            // Act
            var result = await _controller.CreateReport(request);

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task CreateReport_Exception_ShouldReturnInternalServerError()
        {
            // Arrange
            var request = new CreateComplianceReportRequest
            {
                ReportType = ReportType.AuditTrail
            };

            _serviceMock.Setup(s => s.CreateReportAsync(request, TestIssuerId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.CreateReport(request);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region ListReports Tests

        [Test]
        public async Task ListReports_NoFilters_ShouldReturnOk()
        {
            // Arrange
            var reports = new List<ComplianceReportSummary>
            {
                new ComplianceReportSummary
                {
                    ReportId = Guid.NewGuid().ToString(),
                    ReportType = ReportType.MicaReadiness,
                    Status = ReportStatus.Completed
                }
            };

            _serviceMock.Setup(s => s.ListReportsAsync(It.IsAny<ListComplianceReportsRequest>(), TestIssuerId))
                .ReturnsAsync(new ListComplianceReportsResponse
                {
                    Success = true,
                    Reports = reports,
                    TotalCount = 1,
                    Page = 1,
                    PageSize = 50,
                    TotalPages = 1
                });

            // Act
            var result = await _controller.ListReports();

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as ListComplianceReportsResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Reports.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task ListReports_WithFilters_ShouldPassFiltersToService()
        {
            // Arrange
            ListComplianceReportsRequest? capturedRequest = null;
            _serviceMock.Setup(s => s.ListReportsAsync(It.IsAny<ListComplianceReportsRequest>(), TestIssuerId))
                .Callback<ListComplianceReportsRequest, string>((req, issuer) => capturedRequest = req)
                .ReturnsAsync(new ListComplianceReportsResponse
                {
                    Success = true,
                    Reports = new List<ComplianceReportSummary>()
                });

            // Act
            await _controller.ListReports(
                reportType: ReportType.MicaReadiness,
                assetId: TestAssetId,
                network: TestNetwork,
                status: ReportStatus.Completed,
                page: 2,
                pageSize: 25);

            // Assert
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.ReportType, Is.EqualTo(ReportType.MicaReadiness));
            Assert.That(capturedRequest.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(capturedRequest.Network, Is.EqualTo(TestNetwork));
            Assert.That(capturedRequest.Status, Is.EqualTo(ReportStatus.Completed));
            Assert.That(capturedRequest.Page, Is.EqualTo(2));
            Assert.That(capturedRequest.PageSize, Is.EqualTo(25));
        }

        [Test]
        public async Task ListReports_ServiceFailure_ShouldReturnInternalServerError()
        {
            // Arrange
            _serviceMock.Setup(s => s.ListReportsAsync(It.IsAny<ListComplianceReportsRequest>(), TestIssuerId))
                .ReturnsAsync(new ListComplianceReportsResponse
                {
                    Success = false,
                    ErrorMessage = "Service unavailable"
                });

            // Act
            var result = await _controller.ListReports();

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region GetReport Tests

        [Test]
        public async Task GetReport_ExistingReport_ShouldReturnOk()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();
            var report = new ComplianceReport
            {
                ReportId = reportId,
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness,
                Status = ReportStatus.Completed
            };

            _serviceMock.Setup(s => s.GetReportAsync(reportId, TestIssuerId))
                .ReturnsAsync(new GetComplianceReportResponse
                {
                    Success = true,
                    Report = report
                });

            // Act
            var result = await _controller.GetReport(reportId);

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var okResult = result as OkObjectResult;
            var response = okResult!.Value as GetComplianceReportResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Report!.ReportId, Is.EqualTo(reportId));
        }

        [Test]
        public async Task GetReport_NonExistingReport_ShouldReturnNotFound()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();

            _serviceMock.Setup(s => s.GetReportAsync(reportId, TestIssuerId))
                .ReturnsAsync(new GetComplianceReportResponse
                {
                    Success = false,
                    ErrorMessage = "Report not found"
                });

            // Act
            var result = await _controller.GetReport(reportId);

            // Assert
            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task GetReport_Exception_ShouldReturnInternalServerError()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();

            _serviceMock.Setup(s => s.GetReportAsync(reportId, TestIssuerId))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetReport(reportId);

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region DownloadReport Tests

        [Test]
        public async Task DownloadReport_JsonFormat_ShouldReturnFile()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();
            var jsonContent = "{\"test\":\"data\"}";

            _serviceMock.Setup(s => s.DownloadReportAsync(reportId, TestIssuerId, "json"))
                .ReturnsAsync(jsonContent);

            // Act
            var result = await _controller.DownloadReport(reportId, "json");

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());
            var fileResult = result as FileContentResult;
            Assert.That(fileResult!.ContentType, Is.EqualTo("application/json"));
            Assert.That(fileResult.FileDownloadName, Does.Contain(reportId));
            Assert.That(fileResult.FileDownloadName, Does.EndWith(".json"));
        }

        [Test]
        public async Task DownloadReport_CsvFormat_ShouldReturnFile()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();
            var csvContent = "Column1,Column2\nValue1,Value2";

            _serviceMock.Setup(s => s.DownloadReportAsync(reportId, TestIssuerId, "csv"))
                .ReturnsAsync(csvContent);

            // Act
            var result = await _controller.DownloadReport(reportId, "csv");

            // Assert
            Assert.That(result, Is.InstanceOf<FileContentResult>());
            var fileResult = result as FileContentResult;
            Assert.That(fileResult!.ContentType, Is.EqualTo("text/csv"));
            Assert.That(fileResult.FileDownloadName, Does.Contain(reportId));
            Assert.That(fileResult.FileDownloadName, Does.EndWith(".csv"));
        }

        [Test]
        public async Task DownloadReport_ReportNotReady_ShouldReturnBadRequest()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();

            _serviceMock.Setup(s => s.DownloadReportAsync(reportId, TestIssuerId, "json"))
                .ThrowsAsync(new InvalidOperationException("Report is not ready"));

            // Act
            var result = await _controller.DownloadReport(reportId, "json");

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task DownloadReport_UnsupportedFormat_ShouldReturnBadRequest()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();

            _serviceMock.Setup(s => s.DownloadReportAsync(reportId, TestIssuerId, "xml"))
                .ThrowsAsync(new ArgumentException("Unsupported format"));

            // Act
            var result = await _controller.DownloadReport(reportId, "xml");

            // Assert
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task DownloadReport_Exception_ShouldReturnInternalServerError()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();

            _serviceMock.Setup(s => s.DownloadReportAsync(reportId, TestIssuerId, "json"))
                .ThrowsAsync(new Exception("Unexpected error"));

            // Act
            var result = await _controller.DownloadReport(reportId, "json");

            // Assert
            Assert.That(result, Is.InstanceOf<ObjectResult>());
            var objectResult = result as ObjectResult;
            Assert.That(objectResult!.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }

        #endregion

        #region Authentication Tests

        [Test]
        public async Task CreateReport_WithoutAuthentication_ShouldUseUnknownUser()
        {
            // Arrange
            var request = new CreateComplianceReportRequest
            {
                ReportType = ReportType.MicaReadiness
            };

            // Remove authentication
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            string? capturedIssuerId = null;
            _serviceMock.Setup(s => s.CreateReportAsync(It.IsAny<CreateComplianceReportRequest>(), It.IsAny<string>()))
                .Callback<CreateComplianceReportRequest, string>((req, issuer) => capturedIssuerId = issuer)
                .ReturnsAsync(new CreateComplianceReportResponse { Success = true });

            // Act
            await _controller.CreateReport(request);

            // Assert
            Assert.That(capturedIssuerId, Is.EqualTo("Unknown"));
        }

        #endregion
    }
}
