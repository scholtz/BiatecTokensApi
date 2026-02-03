using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Text.Json;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ComplianceReportServiceTests
    {
        private Mock<IComplianceReportRepository> _reportRepositoryMock;
        private Mock<IEnterpriseAuditService> _auditServiceMock;
        private Mock<IComplianceService> _complianceServiceMock;
        private Mock<ILogger<ComplianceReportService>> _loggerMock;
        private ComplianceReportService _service;

        private const string TestIssuerId = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const ulong TestAssetId = 12345;
        private const string TestNetwork = "voimain-v1.0";

        [SetUp]
        public void Setup()
        {
            _reportRepositoryMock = new Mock<IComplianceReportRepository>();
            _auditServiceMock = new Mock<IEnterpriseAuditService>();
            _complianceServiceMock = new Mock<IComplianceService>();
            _loggerMock = new Mock<ILogger<ComplianceReportService>>();

            _service = new ComplianceReportService(
                _reportRepositoryMock.Object,
                _auditServiceMock.Object,
                _complianceServiceMock.Object,
                _loggerMock.Object);
        }

        #region CreateReportAsync Tests

        [Test]
        public async Task CreateReportAsync_MicaReadiness_ShouldCreateReportSuccessfully()
        {
            // Arrange
            var request = new CreateComplianceReportRequest
            {
                ReportType = ReportType.MicaReadiness,
                AssetId = TestAssetId,
                Network = TestNetwork
            };

            var savedReport = new ComplianceReport();
            _reportRepositoryMock.Setup(r => r.CreateReportAsync(It.IsAny<ComplianceReport>()))
                .ReturnsAsync((ComplianceReport r) =>
                {
                    savedReport = r;
                    return r;
                });

            _reportRepositoryMock.Setup(r => r.UpdateReportAsync(It.IsAny<ComplianceReport>()))
                .ReturnsAsync((ComplianceReport r) =>
                {
                    savedReport = r;
                    return r;
                });

            // Mock audit service to return empty results
            _auditServiceMock.Setup(a => a.GetAuditLogAsync(It.IsAny<GetEnterpriseAuditLogRequest>()))
                .ReturnsAsync(new EnterpriseAuditLogResponse
                {
                    Success = true,
                    Entries = new List<EnterpriseAuditLogEntry>()
                });

            // Act
            var result = await _service.CreateReportAsync(request, TestIssuerId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.ReportId, Is.Not.Null);
            Assert.That(result.Status, Is.EqualTo(ReportStatus.Completed));
            Assert.That(savedReport.IssuerId, Is.EqualTo(TestIssuerId));
            Assert.That(savedReport.ReportType, Is.EqualTo(ReportType.MicaReadiness));
            Assert.That(savedReport.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(savedReport.Network, Is.EqualTo(TestNetwork));
            Assert.That(savedReport.Checksum, Is.Not.Null);
            Assert.That(savedReport.ContentJson, Is.Not.Null);
        }

        [Test]
        public async Task CreateReportAsync_AuditTrail_ShouldIncludeAuditEvents()
        {
            // Arrange
            var request = new CreateComplianceReportRequest
            {
                ReportType = ReportType.AuditTrail,
                AssetId = TestAssetId,
                Network = TestNetwork
            };

            var testEvents = new List<EnterpriseAuditLogEntry>
            {
                new EnterpriseAuditLogEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    AssetId = TestAssetId,
                    Network = TestNetwork,
                    Category = AuditEventCategory.TokenIssuance,
                    ActionType = "Deploy",
                    PerformedBy = TestIssuerId,
                    PerformedAt = DateTime.UtcNow,
                    Success = true
                },
                new EnterpriseAuditLogEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    AssetId = TestAssetId,
                    Network = TestNetwork,
                    Category = AuditEventCategory.Compliance,
                    ActionType = "Update",
                    PerformedBy = TestIssuerId,
                    PerformedAt = DateTime.UtcNow.AddHours(-1),
                    Success = true
                }
            };

            var savedReport = new ComplianceReport();
            _reportRepositoryMock.Setup(r => r.CreateReportAsync(It.IsAny<ComplianceReport>()))
                .ReturnsAsync((ComplianceReport r) =>
                {
                    savedReport = r;
                    return r;
                });

            _reportRepositoryMock.Setup(r => r.UpdateReportAsync(It.IsAny<ComplianceReport>()))
                .ReturnsAsync((ComplianceReport r) =>
                {
                    savedReport = r;
                    return r;
                });

            _auditServiceMock.Setup(a => a.GetAuditLogAsync(It.IsAny<GetEnterpriseAuditLogRequest>()))
                .ReturnsAsync(new EnterpriseAuditLogResponse
                {
                    Success = true,
                    Entries = testEvents,
                    Summary = new AuditLogSummary
                    {
                        TokenIssuanceEvents = 1,
                        ComplianceEvents = 1,
                        SuccessfulOperations = 2
                    }
                });

            // Act
            var result = await _service.CreateReportAsync(request, TestIssuerId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(savedReport.EventCount, Is.EqualTo(2));
            Assert.That(savedReport.ContentJson, Is.Not.Null);

            // Verify content structure
            var content = JsonSerializer.Deserialize<AuditTrailReportContent>(savedReport.ContentJson!);
            Assert.That(content, Is.Not.Null);
            Assert.That(content.Events.Count, Is.EqualTo(2));
            Assert.That(content.Summary, Is.Not.Null);
        }

        [Test]
        public async Task CreateReportAsync_ComplianceBadge_ShouldCollectEvidence()
        {
            // Arrange
            var request = new CreateComplianceReportRequest
            {
                ReportType = ReportType.ComplianceBadge,
                AssetId = TestAssetId,
                Network = TestNetwork
            };

            var savedReport = new ComplianceReport();
            _reportRepositoryMock.Setup(r => r.CreateReportAsync(It.IsAny<ComplianceReport>()))
                .ReturnsAsync((ComplianceReport r) =>
                {
                    savedReport = r;
                    return r;
                });

            _reportRepositoryMock.Setup(r => r.UpdateReportAsync(It.IsAny<ComplianceReport>()))
                .ReturnsAsync((ComplianceReport r) =>
                {
                    savedReport = r;
                    return r;
                });

            // Mock audit events
            _auditServiceMock.Setup(a => a.GetAuditLogAsync(It.IsAny<GetEnterpriseAuditLogRequest>()))
                .ReturnsAsync(new EnterpriseAuditLogResponse
                {
                    Success = true,
                    Entries = new List<EnterpriseAuditLogEntry>
                    {
                        new EnterpriseAuditLogEntry
                        {
                            AssetId = TestAssetId,
                            Category = AuditEventCategory.TokenIssuance,
                            ActionType = "Deploy",
                            PerformedBy = TestIssuerId,
                            Success = true
                        }
                    }
                });

            // Mock compliance metadata
            _complianceServiceMock.Setup(c => c.GetMetadataAsync(TestAssetId))
                .ReturnsAsync(new ComplianceMetadataResponse
                {
                    Success = true,
                    Metadata = new ComplianceMetadata
                    {
                        AssetId = TestAssetId,
                        ComplianceStatus = ComplianceStatus.Compliant,
                        VerificationStatus = VerificationStatus.Verified,
                        KycProvider = "TestProvider",
                        KycVerificationDate = DateTime.UtcNow.AddDays(-30)
                    }
                });

            // Act
            var result = await _service.CreateReportAsync(request, TestIssuerId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(savedReport.ContentJson, Is.Not.Null);

            var content = JsonSerializer.Deserialize<ComplianceBadgeReportContent>(savedReport.ContentJson!);
            Assert.That(content, Is.Not.Null);
            Assert.That(content.Evidence.Count, Is.GreaterThan(0));
            Assert.That(content.Evidence.Any(e => e.EvidenceType == "Audit Trail"), Is.True);
            Assert.That(content.Evidence.Any(e => e.EvidenceType == "Compliance Metadata"), Is.True);
            Assert.That(content.Evidence.Any(e => e.EvidenceType == "KYC Verification"), Is.True);
        }

        [Test]
        public async Task CreateReportAsync_WithDateFilter_ShouldPassToAuditService()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;

            var request = new CreateComplianceReportRequest
            {
                ReportType = ReportType.AuditTrail,
                AssetId = TestAssetId,
                FromDate = fromDate,
                ToDate = toDate
            };

            GetEnterpriseAuditLogRequest? capturedRequest = null;

            _reportRepositoryMock.Setup(r => r.CreateReportAsync(It.IsAny<ComplianceReport>()))
                .ReturnsAsync((ComplianceReport r) => r);
            _reportRepositoryMock.Setup(r => r.UpdateReportAsync(It.IsAny<ComplianceReport>()))
                .ReturnsAsync((ComplianceReport r) => r);

            _auditServiceMock.Setup(a => a.GetAuditLogAsync(It.IsAny<GetEnterpriseAuditLogRequest>()))
                .Callback<GetEnterpriseAuditLogRequest>(req => capturedRequest = req)
                .ReturnsAsync(new EnterpriseAuditLogResponse
                {
                    Success = true,
                    Entries = new List<EnterpriseAuditLogEntry>()
                });

            // Act
            await _service.CreateReportAsync(request, TestIssuerId);

            // Assert
            Assert.That(capturedRequest, Is.Not.Null);
            Assert.That(capturedRequest!.FromDate, Is.EqualTo(fromDate));
            Assert.That(capturedRequest.ToDate, Is.EqualTo(toDate));
        }

        [Test]
        public async Task CreateReportAsync_AuditServiceFailure_ShouldMarkReportAsFailed()
        {
            // Arrange
            var request = new CreateComplianceReportRequest
            {
                ReportType = ReportType.AuditTrail,
                AssetId = TestAssetId
            };

            ComplianceReport? savedReport = null;
            _reportRepositoryMock.Setup(r => r.CreateReportAsync(It.IsAny<ComplianceReport>()))
                .ReturnsAsync((ComplianceReport r) => r);
            _reportRepositoryMock.Setup(r => r.UpdateReportAsync(It.IsAny<ComplianceReport>()))
                .Callback<ComplianceReport>(r => savedReport = r)
                .ReturnsAsync((ComplianceReport r) => r);

            _auditServiceMock.Setup(a => a.GetAuditLogAsync(It.IsAny<GetEnterpriseAuditLogRequest>()))
                .ReturnsAsync(new EnterpriseAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = "Audit service unavailable"
                });

            // Act
            var result = await _service.CreateReportAsync(request, TestIssuerId);

            // Assert
            Assert.That(result.Success, Is.True); // Initial creation succeeds
            Assert.That(savedReport, Is.Not.Null);
            Assert.That(savedReport!.Status, Is.EqualTo(ReportStatus.Failed));
            Assert.That(savedReport.ErrorMessage, Is.Not.Null);
        }

        #endregion

        #region GetReportAsync Tests

        [Test]
        public async Task GetReportAsync_ExistingReport_ShouldReturnReport()
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

            _reportRepositoryMock.Setup(r => r.GetReportAsync(reportId, TestIssuerId))
                .ReturnsAsync(report);

            // Act
            var result = await _service.GetReportAsync(reportId, TestIssuerId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Report, Is.Not.Null);
            Assert.That(result.Report!.ReportId, Is.EqualTo(reportId));
        }

        [Test]
        public async Task GetReportAsync_NonExistingReport_ShouldReturnError()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();

            _reportRepositoryMock.Setup(r => r.GetReportAsync(reportId, TestIssuerId))
                .ReturnsAsync((ComplianceReport?)null);

            // Act
            var result = await _service.GetReportAsync(reportId, TestIssuerId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null);
        }

        [Test]
        public async Task GetReportAsync_DifferentIssuer_ShouldReturnError()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();
            var differentIssuerId = "DIFFERENT1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            _reportRepositoryMock.Setup(r => r.GetReportAsync(reportId, differentIssuerId))
                .ReturnsAsync((ComplianceReport?)null); // Repository enforces access control

            // Act
            var result = await _service.GetReportAsync(reportId, differentIssuerId);

            // Assert
            Assert.That(result.Success, Is.False);
        }

        #endregion

        #region ListReportsAsync Tests

        [Test]
        public async Task ListReportsAsync_WithoutFilters_ShouldReturnAllReports()
        {
            // Arrange
            var request = new ListComplianceReportsRequest
            {
                Page = 1,
                PageSize = 50
            };

            var reports = new List<ComplianceReport>
            {
                new ComplianceReport { ReportId = Guid.NewGuid().ToString(), ReportType = ReportType.MicaReadiness },
                new ComplianceReport { ReportId = Guid.NewGuid().ToString(), ReportType = ReportType.AuditTrail }
            };

            _reportRepositoryMock.Setup(r => r.ListReportsAsync(TestIssuerId, request))
                .ReturnsAsync(reports);
            _reportRepositoryMock.Setup(r => r.GetReportCountAsync(TestIssuerId, request))
                .ReturnsAsync(reports.Count);

            // Act
            var result = await _service.ListReportsAsync(request, TestIssuerId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Reports.Count, Is.EqualTo(2));
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.TotalPages, Is.EqualTo(1));
        }

        [Test]
        public async Task ListReportsAsync_WithPagination_ShouldCalculatePagesCorrectly()
        {
            // Arrange
            var request = new ListComplianceReportsRequest
            {
                Page = 2,
                PageSize = 10
            };

            _reportRepositoryMock.Setup(r => r.ListReportsAsync(TestIssuerId, request))
                .ReturnsAsync(new List<ComplianceReport>());
            _reportRepositoryMock.Setup(r => r.GetReportCountAsync(TestIssuerId, request))
                .ReturnsAsync(25); // Total 25 records

            // Act
            var result = await _service.ListReportsAsync(request, TestIssuerId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalPages, Is.EqualTo(3)); // 25 records / 10 per page = 3 pages
            Assert.That(result.Page, Is.EqualTo(2));
        }

        [Test]
        public async Task ListReportsAsync_WithFilters_ShouldPassToRepository()
        {
            // Arrange
            var request = new ListComplianceReportsRequest
            {
                ReportType = ReportType.MicaReadiness,
                AssetId = TestAssetId,
                Network = TestNetwork,
                Status = ReportStatus.Completed
            };

            _reportRepositoryMock.Setup(r => r.ListReportsAsync(TestIssuerId, request))
                .ReturnsAsync(new List<ComplianceReport>());
            _reportRepositoryMock.Setup(r => r.GetReportCountAsync(TestIssuerId, request))
                .ReturnsAsync(0);

            // Act
            await _service.ListReportsAsync(request, TestIssuerId);

            // Assert
            _reportRepositoryMock.Verify(r => r.ListReportsAsync(
                TestIssuerId,
                It.Is<ListComplianceReportsRequest>(req =>
                    req.ReportType == ReportType.MicaReadiness &&
                    req.AssetId == TestAssetId &&
                    req.Network == TestNetwork &&
                    req.Status == ReportStatus.Completed)),
                Times.Once);
        }

        #endregion

        #region DownloadReportAsync Tests

        [Test]
        public async Task DownloadReportAsync_CompletedReportJson_ShouldReturnContent()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();
            var contentJson = "{\"test\":\"data\"}";
            var report = new ComplianceReport
            {
                ReportId = reportId,
                IssuerId = TestIssuerId,
                Status = ReportStatus.Completed,
                ContentJson = contentJson,
                Checksum = "abc123"
            };

            _reportRepositoryMock.Setup(r => r.GetReportAsync(reportId, TestIssuerId))
                .ReturnsAsync(report);

            // Act
            var result = await _service.DownloadReportAsync(reportId, TestIssuerId, "json");

            // Assert
            Assert.That(result, Is.EqualTo(contentJson));
        }

        [Test]
        public async Task DownloadReportAsync_CompletedReportCsv_ShouldReturnCsvFormat()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();
            var micaContent = new MicaReadinessReportContent
            {
                Metadata = new MicaReadinessReportMetadata
                {
                    ReportId = reportId,
                    SchemaVersion = "1.0",
                    GeneratedAt = DateTime.UtcNow
                },
                ComplianceChecks = new List<MicaComplianceCheck>
                {
                    new MicaComplianceCheck
                    {
                        Article = "Article 17",
                        Requirement = "Authorization",
                        Status = "Pass",
                        Evidence = "Evidence text",
                        Recommendation = null
                    }
                },
                ReadinessScore = 85,
                ReadinessSummary = "Good compliance"
            };

            var report = new ComplianceReport
            {
                ReportId = reportId,
                IssuerId = TestIssuerId,
                ReportType = ReportType.MicaReadiness,
                Status = ReportStatus.Completed,
                ContentJson = JsonSerializer.Serialize(micaContent),
                Checksum = "abc123"
            };

            _reportRepositoryMock.Setup(r => r.GetReportAsync(reportId, TestIssuerId))
                .ReturnsAsync(report);

            // Act
            var result = await _service.DownloadReportAsync(reportId, TestIssuerId, "csv");

            // Assert
            Assert.That(result, Does.Contain("Article,Requirement,Status,Evidence,Recommendation"));
            Assert.That(result, Does.Contain("Article 17"));
            Assert.That(result, Does.Contain("Authorization"));
            Assert.That(result, Does.Contain("Pass"));
        }

        [Test]
        public void DownloadReportAsync_PendingReport_ShouldThrowException()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();
            var report = new ComplianceReport
            {
                ReportId = reportId,
                IssuerId = TestIssuerId,
                Status = ReportStatus.Processing,
                ContentJson = null
            };

            _reportRepositoryMock.Setup(r => r.GetReportAsync(reportId, TestIssuerId))
                .ReturnsAsync(report);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.DownloadReportAsync(reportId, TestIssuerId, "json"));
        }

        [Test]
        public void DownloadReportAsync_NonExistingReport_ShouldThrowException()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();

            _reportRepositoryMock.Setup(r => r.GetReportAsync(reportId, TestIssuerId))
                .ReturnsAsync((ComplianceReport?)null);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.DownloadReportAsync(reportId, TestIssuerId, "json"));
        }

        [Test]
        public void DownloadReportAsync_UnsupportedFormat_ShouldThrowException()
        {
            // Arrange
            var reportId = Guid.NewGuid().ToString();
            var report = new ComplianceReport
            {
                ReportId = reportId,
                IssuerId = TestIssuerId,
                Status = ReportStatus.Completed,
                ContentJson = "{}",
                Checksum = "abc123"
            };

            _reportRepositoryMock.Setup(r => r.GetReportAsync(reportId, TestIssuerId))
                .ReturnsAsync(report);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _service.DownloadReportAsync(reportId, TestIssuerId, "xml"));
        }

        #endregion

        #region Checksum Tests

        [Test]
        public async Task CreateReportAsync_ShouldGenerateValidChecksum()
        {
            // Arrange
            var request = new CreateComplianceReportRequest
            {
                ReportType = ReportType.MicaReadiness,
                AssetId = TestAssetId
            };

            ComplianceReport? savedReport = null;
            _reportRepositoryMock.Setup(r => r.CreateReportAsync(It.IsAny<ComplianceReport>()))
                .ReturnsAsync((ComplianceReport r) => r);
            _reportRepositoryMock.Setup(r => r.UpdateReportAsync(It.IsAny<ComplianceReport>()))
                .Callback<ComplianceReport>(r => savedReport = r)
                .ReturnsAsync((ComplianceReport r) => r);

            _auditServiceMock.Setup(a => a.GetAuditLogAsync(It.IsAny<GetEnterpriseAuditLogRequest>()))
                .ReturnsAsync(new EnterpriseAuditLogResponse
                {
                    Success = true,
                    Entries = new List<EnterpriseAuditLogEntry>()
                });

            // Act
            await _service.CreateReportAsync(request, TestIssuerId);

            // Assert
            Assert.That(savedReport, Is.Not.Null);
            Assert.That(savedReport!.Checksum, Is.Not.Null);
            Assert.That(savedReport.Checksum!.Length, Is.EqualTo(64)); // SHA-256 hex string length
            Assert.That(savedReport.Checksum, Does.Match("^[0-9a-f]{64}$")); // Hex format
        }

        #endregion
    }
}
