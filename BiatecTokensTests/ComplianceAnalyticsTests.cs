using BiatecTokensApi.Models;
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
    [TestFixture]
    public class ComplianceAnalyticsTests
    {
        private Mock<IComplianceRepository> _repositoryMock;
        private Mock<IWhitelistService> _whitelistServiceMock;
        private Mock<ILogger<ComplianceService>> _loggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private Mock<IWebhookService> _webhookServiceMock;
        private ComplianceService _service;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IComplianceRepository>();
            _whitelistServiceMock = new Mock<IWhitelistService>();
            _loggerMock = new Mock<ILogger<ComplianceService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _webhookServiceMock = new Mock<IWebhookService>();
            _service = new ComplianceService(
                _repositoryMock.Object,
                _whitelistServiceMock.Object,
                _loggerMock.Object,
                _meteringServiceMock.Object,
                _webhookServiceMock.Object);
        }

        #region Regulatory Reporting Analytics Tests

        [Test]
        public async Task GetRegulatoryReportingAnalyticsAsync_WithValidRequest_ShouldReturnSuccess()
        {
            // Arrange
            var request = new GetRegulatoryReportingAnalyticsRequest
            {
                Network = "voimain-v1.0",
                FromDate = DateTime.UtcNow.AddDays(-30),
                ToDate = DateTime.UtcNow,
                IncludeAssetDetails = false
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var metadata = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 1,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    IssuerName = "Test Issuer",
                    Jurisdiction = "US",
                    RegulatoryFramework = "MICA",
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                }
            };

            var auditLogs = new List<ComplianceAuditLogEntry>
            {
                new ComplianceAuditLogEntry
                {
                    AssetId = 1,
                    ActionType = ComplianceActionType.Create,
                    Success = true,
                    PerformedAt = DateTime.UtcNow.AddDays(-10)
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(auditLogs);

            // Act
            var result = await _service.GetRegulatoryReportingAnalyticsAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.ComplianceSummary, Is.Not.Null);
            Assert.That(result.ComplianceSummary.TotalAssets, Is.EqualTo(1));
            Assert.That(result.ComplianceSummary.MicaCompliantAssets, Is.EqualTo(1));
            Assert.That(result.ComplianceSummary.TotalComplianceEvents, Is.EqualTo(1));
            Assert.That(result.Period.DurationDays, Is.EqualTo(30));
        }

        [Test]
        public async Task GetRegulatoryReportingAnalyticsAsync_WithNoData_ShouldReturnEmptyMetrics()
        {
            // Arrange
            var request = new GetRegulatoryReportingAnalyticsRequest
            {
                FromDate = DateTime.UtcNow.AddDays(-30),
                ToDate = DateTime.UtcNow
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(new List<ComplianceMetadata>());

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetRegulatoryReportingAnalyticsAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.ComplianceSummary.TotalAssets, Is.EqualTo(0));
            Assert.That(result.ComplianceSummary.TotalComplianceEvents, Is.EqualTo(0));
        }

        [Test]
        public async Task GetRegulatoryReportingAnalyticsAsync_WithMultipleNetworks_ShouldAggregateByNetwork()
        {
            // Arrange
            var request = new GetRegulatoryReportingAnalyticsRequest
            {
                FromDate = DateTime.UtcNow.AddDays(-30),
                ToDate = DateTime.UtcNow
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var metadata = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 1,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                },
                new ComplianceMetadata
                {
                    AssetId = 2,
                    Network = "aramidmain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetRegulatoryReportingAnalyticsAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.ComplianceSummary.NetworkDistribution.Count, Is.EqualTo(2));
            Assert.That(result.ComplianceSummary.NetworkDistribution["voimain-v1.0"], Is.EqualTo(1));
            Assert.That(result.ComplianceSummary.NetworkDistribution["aramidmain-v1.0"], Is.EqualTo(1));
        }

        [Test]
        public async Task GetRegulatoryReportingAnalyticsAsync_WithJurisdictions_ShouldAggregateByJurisdiction()
        {
            // Arrange
            var request = new GetRegulatoryReportingAnalyticsRequest
            {
                FromDate = DateTime.UtcNow.AddDays(-30),
                ToDate = DateTime.UtcNow
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var metadata = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 1,
                    Network = "voimain-v1.0",
                    Jurisdiction = "US,EU",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                },
                new ComplianceMetadata
                {
                    AssetId = 2,
                    Network = "voimain-v1.0",
                    Jurisdiction = "EU",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(new List<ComplianceAuditLogEntry>());

            // Act
            var result = await _service.GetRegulatoryReportingAnalyticsAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.ComplianceSummary.JurisdictionDistribution.Count, Is.EqualTo(2));
            Assert.That(result.ComplianceSummary.JurisdictionDistribution["EU"], Is.EqualTo(2));
            Assert.That(result.ComplianceSummary.JurisdictionDistribution["US"], Is.EqualTo(1));
        }

        #endregion

        #region Audit Summary Aggregates Tests

        [Test]
        public async Task GetAuditSummaryAggregatesAsync_WithDailyPeriod_ShouldReturnDailyAggregates()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;
            var request = new GetAuditSummaryAggregatesRequest
            {
                FromDate = fromDate,
                ToDate = toDate,
                Period = AggregationPeriod.Daily
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var auditLogs = new List<ComplianceAuditLogEntry>();
            for (int i = 0; i < 7; i++)
            {
                auditLogs.Add(new ComplianceAuditLogEntry
                {
                    AssetId = 1,
                    ActionType = ComplianceActionType.Update,
                    Success = true,
                    PerformedAt = fromDate.AddDays(i),
                    PerformedBy = "User1"
                });
            }

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(auditLogs);

            // Act
            var result = await _service.GetAuditSummaryAggregatesAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.TimeSeries.Count, Is.GreaterThan(0));
            Assert.That(result.Summary.TotalEvents, Is.EqualTo(7));
            Assert.That(result.Summary.SuccessRate, Is.EqualTo(100));
        }

        [Test]
        public async Task GetAuditSummaryAggregatesAsync_WithWeeklyPeriod_ShouldReturnWeeklyAggregates()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;
            var request = new GetAuditSummaryAggregatesRequest
            {
                FromDate = fromDate,
                ToDate = toDate,
                Period = AggregationPeriod.Weekly
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var auditLogs = new List<ComplianceAuditLogEntry>
            {
                new ComplianceAuditLogEntry
                {
                    AssetId = 1,
                    ActionType = ComplianceActionType.Create,
                    Success = true,
                    PerformedAt = fromDate.AddDays(5),
                    PerformedBy = "User1"
                }
            };

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(auditLogs);

            // Act
            var result = await _service.GetAuditSummaryAggregatesAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.AggregationPeriod, Is.EqualTo(AggregationPeriod.Weekly));
            Assert.That(result.Summary.TotalEvents, Is.EqualTo(1));
        }

        [Test]
        public async Task GetAuditSummaryAggregatesAsync_WithFailedEvents_ShouldCalculateSuccessRate()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;
            var request = new GetAuditSummaryAggregatesRequest
            {
                FromDate = fromDate,
                ToDate = toDate,
                Period = AggregationPeriod.Daily
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var auditLogs = new List<ComplianceAuditLogEntry>
            {
                new ComplianceAuditLogEntry
                {
                    AssetId = 1,
                    ActionType = ComplianceActionType.Update,
                    Success = true,
                    PerformedAt = fromDate.AddDays(1),
                    PerformedBy = "User1"
                },
                new ComplianceAuditLogEntry
                {
                    AssetId = 1,
                    ActionType = ComplianceActionType.Update,
                    Success = false,
                    PerformedAt = fromDate.AddDays(2),
                    PerformedBy = "User1"
                }
            };

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(auditLogs);

            // Act
            var result = await _service.GetAuditSummaryAggregatesAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Summary.TotalEvents, Is.EqualTo(2));
            Assert.That(result.Summary.SuccessRate, Is.EqualTo(50));
        }

        [Test]
        public async Task GetAuditSummaryAggregatesAsync_ShouldIdentifyPeakPeriod()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-10);
            var toDate = DateTime.UtcNow;
            var request = new GetAuditSummaryAggregatesRequest
            {
                FromDate = fromDate,
                ToDate = toDate,
                Period = AggregationPeriod.Daily
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var peakDate = fromDate.AddDays(5);
            var auditLogs = new List<ComplianceAuditLogEntry>();

            // Add more events on the peak day
            for (int i = 0; i < 10; i++)
            {
                auditLogs.Add(new ComplianceAuditLogEntry
                {
                    AssetId = 1,
                    ActionType = ComplianceActionType.Update,
                    Success = true,
                    PerformedAt = peakDate,
                    PerformedBy = "User1"
                });
            }

            // Add fewer events on other days
            auditLogs.Add(new ComplianceAuditLogEntry
            {
                AssetId = 1,
                ActionType = ComplianceActionType.Create,
                Success = true,
                PerformedAt = fromDate.AddDays(1),
                PerformedBy = "User1"
            });

            _repositoryMock.Setup(r => r.GetAuditLogAsync(It.IsAny<GetComplianceAuditLogRequest>()))
                .ReturnsAsync(auditLogs);

            // Act
            var result = await _service.GetAuditSummaryAggregatesAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Summary.PeakEventCount, Is.EqualTo(10));
            Assert.That(result.Summary.PeakPeriod, Is.Not.Null);
        }

        #endregion

        #region Compliance Trends Tests

        [Test]
        public async Task GetComplianceTrendsAsync_ShouldReturnTrendData()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;
            var request = new GetComplianceTrendsRequest
            {
                FromDate = fromDate,
                ToDate = toDate,
                Period = AggregationPeriod.Weekly
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var metadata = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 1,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    CreatedAt = fromDate.AddDays(5)
                },
                new ComplianceMetadata
                {
                    AssetId = 2,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.UnderReview,
                    CreatedAt = fromDate.AddDays(10)
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry>()
                });

            // Mock MICA checklist responses
            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(It.IsAny<ulong>()))
                .ReturnsAsync((ulong assetId) => metadata.FirstOrDefault(m => m.AssetId == assetId));

            // Act
            var result = await _service.GetComplianceTrendsAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.StatusTrends, Is.Not.Null);
            Assert.That(result.MicaTrends, Is.Not.Null);
            Assert.That(result.WhitelistTrends, Is.Not.Null);
            Assert.That(result.TrendDirection, Is.Not.Null);
        }

        [Test]
        public async Task GetComplianceTrendsAsync_WithImprovingCompliance_ShouldIndicateImproving()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-60);
            var toDate = DateTime.UtcNow;
            var request = new GetComplianceTrendsRequest
            {
                FromDate = fromDate,
                ToDate = toDate,
                Period = AggregationPeriod.Monthly
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            // Create metadata showing improvement over time
            var metadata = new List<ComplianceMetadata>
            {
                // Early period - non-compliant
                new ComplianceMetadata
                {
                    AssetId = 1,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.NonCompliant,
                    CreatedAt = fromDate.AddDays(5)
                },
                // Later period - compliant
                new ComplianceMetadata
                {
                    AssetId = 2,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    CreatedAt = fromDate.AddDays(40)
                },
                new ComplianceMetadata
                {
                    AssetId = 3,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    CreatedAt = fromDate.AddDays(45)
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry>()
                });

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(It.IsAny<ulong>()))
                .ReturnsAsync((ulong assetId) => metadata.FirstOrDefault(m => m.AssetId == assetId));

            // Act
            var result = await _service.GetComplianceTrendsAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.TrendDirection, Is.EqualTo("Improving"));
        }

        #endregion
    }
}
