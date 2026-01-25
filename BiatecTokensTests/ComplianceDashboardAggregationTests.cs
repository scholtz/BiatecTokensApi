using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ComplianceDashboardAggregationTests
    {
        private Mock<IComplianceRepository> _repositoryMock;
        private Mock<IWhitelistService> _whitelistServiceMock;
        private Mock<ILogger<ComplianceService>> _loggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private ComplianceService _service;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IComplianceRepository>();
            _whitelistServiceMock = new Mock<IWhitelistService>();
            _loggerMock = new Mock<ILogger<ComplianceService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _service = new ComplianceService(_repositoryMock.Object, _whitelistServiceMock.Object, _loggerMock.Object, _meteringServiceMock.Object);
        }

        #region GetDashboardAggregationAsync Tests

        [Test]
        public async Task GetDashboardAggregationAsync_WithNoMetadata_ShouldReturnEmptyMetrics()
        {
            // Arrange
            var request = new GetComplianceDashboardAggregationRequest
            {
                Network = "voimain-v1.0"
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(new List<ComplianceMetadata>());

            // Act
            var result = await _service.GetDashboardAggregationAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metrics, Is.Not.Null);
            Assert.That(result.Metrics.TotalAssets, Is.EqualTo(0));
            Assert.That(result.NetworkFilter, Is.EqualTo("voimain-v1.0"));
        }

        [Test]
        public async Task GetDashboardAggregationAsync_WithMultipleAssets_ShouldCalculateCorrectMetrics()
        {
            // Arrange
            var request = new GetComplianceDashboardAggregationRequest();
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var metadata = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 1,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    Jurisdiction = "US",
                    TransferRestrictions = "Accredited investors only"
                },
                new ComplianceMetadata
                {
                    AssetId = 2,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.NonCompliant,
                    Jurisdiction = "EU",
                    TransferRestrictions = "Geographic restrictions"
                },
                new ComplianceMetadata
                {
                    AssetId = 3,
                    Network = "aramidmain-v1.0",
                    ComplianceStatus = ComplianceStatus.UnderReview,
                    Jurisdiction = "US,EU"
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            // Mock MICA checklist responses
            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(It.IsAny<ulong>()))
                .ReturnsAsync((ulong assetId) => metadata.FirstOrDefault(m => m.AssetId == assetId));

            // Mock whitelist responses
            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry>()
                });

            // Act
            var result = await _service.GetDashboardAggregationAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metrics.TotalAssets, Is.EqualTo(3));
            Assert.That(result.Metrics.ComplianceCounts.CompliantAssets, Is.EqualTo(1));
            Assert.That(result.Metrics.ComplianceCounts.RestrictedAssets, Is.EqualTo(1));
            Assert.That(result.Metrics.ComplianceCounts.UnderReviewAssets, Is.EqualTo(1));
            Assert.That(result.Metrics.Jurisdictions.AssetsWithJurisdiction, Is.EqualTo(3));
            Assert.That(result.Metrics.Jurisdictions.UniqueJurisdictions, Is.EqualTo(2));
            Assert.That(result.Metrics.NetworkDistribution.Count, Is.EqualTo(2));
            Assert.That(result.Metrics.TopRestrictionReasons.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task GetDashboardAggregationAsync_WithNetworkFilter_ShouldFilterCorrectly()
        {
            // Arrange
            var request = new GetComplianceDashboardAggregationRequest
            {
                Network = "voimain-v1.0"
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var metadata = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 1,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant
                },
                new ComplianceMetadata
                {
                    AssetId = 2,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.NonCompliant
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(It.IsAny<ulong>()))
                .ReturnsAsync((ulong assetId) => metadata.FirstOrDefault(m => m.AssetId == assetId));

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry>()
                });

            // Act
            var result = await _service.GetDashboardAggregationAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.NetworkFilter, Is.EqualTo("voimain-v1.0"));
            Assert.That(result.Metrics.TotalAssets, Is.EqualTo(2));
        }

        [Test]
        public async Task GetDashboardAggregationAsync_WithWhitelistData_ShouldCalculateWhitelistMetrics()
        {
            // Arrange
            var request = new GetComplianceDashboardAggregationRequest();
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var metadata = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 1,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant
                }
            };

            var whitelistEntries = new List<WhitelistEntry>
            {
                new WhitelistEntry { AssetId = 1, Status = WhitelistStatus.Active },
                new WhitelistEntry { AssetId = 1, Status = WhitelistStatus.Active },
                new WhitelistEntry { AssetId = 1, Status = WhitelistStatus.Revoked }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(It.IsAny<ulong>()))
                .ReturnsAsync((ulong assetId) => metadata.FirstOrDefault(m => m.AssetId == assetId));

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = whitelistEntries
                });

            // Act
            var result = await _service.GetDashboardAggregationAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metrics.WhitelistStatus.AssetsWithWhitelist, Is.EqualTo(1));
            Assert.That(result.Metrics.WhitelistStatus.TotalWhitelistedAddresses, Is.EqualTo(3));
            Assert.That(result.Metrics.WhitelistStatus.ActiveWhitelistedAddresses, Is.EqualTo(2));
            Assert.That(result.Metrics.WhitelistStatus.RevokedWhitelistedAddresses, Is.EqualTo(1));
        }

        [Test]
        public async Task GetDashboardAggregationAsync_WithDateRangeFilter_ShouldApplyFilter()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-10);
            var toDate = DateTime.UtcNow;
            var request = new GetComplianceDashboardAggregationRequest
            {
                FromDate = fromDate,
                ToDate = toDate
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var metadata = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 1,
                    CreatedAt = fromDate.AddDays(5),
                    ComplianceStatus = ComplianceStatus.Compliant
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(It.IsAny<ulong>()))
                .ReturnsAsync((ulong assetId) => metadata.FirstOrDefault(m => m.AssetId == assetId));

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry>()
                });

            // Act
            var result = await _service.GetDashboardAggregationAsync(request, requestedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.DateRangeFilter, Is.Not.Null);
            Assert.That(result.DateRangeFilter!.EarliestEvent, Is.EqualTo(fromDate));
            Assert.That(result.DateRangeFilter.LatestEvent, Is.EqualTo(toDate));
        }

        #endregion

        #region ExportDashboardAggregationCsvAsync Tests

        [Test]
        public async Task ExportDashboardAggregationCsvAsync_ShouldGenerateValidCsv()
        {
            // Arrange
            var request = new GetComplianceDashboardAggregationRequest();
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var metadata = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 1,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant,
                    Jurisdiction = "US"
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(It.IsAny<ulong>()))
                .ReturnsAsync((ulong assetId) => metadata.FirstOrDefault(m => m.AssetId == assetId));

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry>()
                });

            // Act
            var csv = await _service.ExportDashboardAggregationCsvAsync(request, requestedBy);

            // Assert
            Assert.That(csv, Is.Not.Null);
            Assert.That(csv, Does.Contain("Compliance Dashboard Aggregation Export"));
            Assert.That(csv, Does.Contain("SUMMARY METRICS"));
            Assert.That(csv, Does.Contain("MICA READINESS"));
            Assert.That(csv, Does.Contain("WHITELIST STATUS"));
            Assert.That(csv, Does.Contain("JURISDICTION COVERAGE"));
            Assert.That(csv, Does.Contain("COMPLIANCE STATUS COUNTS"));
        }

        [Test]
        public async Task ExportDashboardAggregationCsvAsync_WithAssetBreakdown_ShouldIncludeDetails()
        {
            // Arrange
            var request = new GetComplianceDashboardAggregationRequest
            {
                IncludeAssetBreakdown = true
            };
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var metadata = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 1,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(It.IsAny<ulong>()))
                .ReturnsAsync((ulong assetId) => metadata.FirstOrDefault(m => m.AssetId == assetId));

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry>()
                });

            // Act
            var csv = await _service.ExportDashboardAggregationCsvAsync(request, requestedBy);

            // Assert
            Assert.That(csv, Does.Contain("DETAILED ASSET BREAKDOWN"));
            Assert.That(csv, Does.Contain("Asset ID"));
        }

        #endregion

        #region ExportDashboardAggregationJsonAsync Tests

        [Test]
        public async Task ExportDashboardAggregationJsonAsync_ShouldGenerateValidJson()
        {
            // Arrange
            var request = new GetComplianceDashboardAggregationRequest();
            var requestedBy = "TESTUSER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            var metadata = new List<ComplianceMetadata>
            {
                new ComplianceMetadata
                {
                    AssetId = 1,
                    Network = "voimain-v1.0",
                    ComplianceStatus = ComplianceStatus.Compliant
                }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(It.IsAny<ListComplianceMetadataRequest>()))
                .ReturnsAsync(metadata);

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(It.IsAny<ulong>()))
                .ReturnsAsync((ulong assetId) => metadata.FirstOrDefault(m => m.AssetId == assetId));

            _whitelistServiceMock.Setup(w => w.ListEntriesAsync(It.IsAny<ListWhitelistRequest>()))
                .ReturnsAsync(new WhitelistListResponse
                {
                    Success = true,
                    Entries = new List<WhitelistEntry>()
                });

            // Act
            var json = await _service.ExportDashboardAggregationJsonAsync(request, requestedBy);

            // Assert
            Assert.That(json, Is.Not.Null);
            Assert.That(json, Does.Contain("success"));
            Assert.That(json, Does.Contain("metrics"));
            Assert.That(json, Does.Contain("totalAssets"));
            Assert.That(json, Does.Contain("micaReadiness"));
            Assert.That(json, Does.Contain("whitelistStatus"));
        }

        #endregion
    }
}
