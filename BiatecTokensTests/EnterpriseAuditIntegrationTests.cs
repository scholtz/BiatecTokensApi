using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for enterprise audit export API
    /// Tests unified audit log access across whitelist/blacklist and compliance systems
    /// with focus on MICA reporting requirements and VOI/Aramid network support
    /// </summary>
    [TestFixture]
    public class EnterpriseAuditIntegrationTests
    {
        private WhitelistRepository _whitelistRepository;
        private ComplianceRepository _complianceRepository;
        private EnterpriseAuditRepository _enterpriseAuditRepository;
        private EnterpriseAuditService _enterpriseAuditService;
        private Mock<ILogger<WhitelistRepository>> _whitelistLoggerMock;
        private Mock<ILogger<ComplianceRepository>> _complianceLoggerMock;
        private Mock<ILogger<EnterpriseAuditRepository>> _enterpriseLoggerMock;
        private Mock<ILogger<EnterpriseAuditService>> _serviceLoggerMock;
        private Mock<ITokenIssuanceRepository> _tokenIssuanceRepositoryMock;

        private const string TestPerformedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string ValidAddress1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string ValidAddress2 = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBY5HFKQ";
        private const string ValidAddress3 = "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCY5HFKQ";

        [SetUp]
        public void Setup()
        {
            _whitelistLoggerMock = new Mock<ILogger<WhitelistRepository>>();
            _complianceLoggerMock = new Mock<ILogger<ComplianceRepository>>();
            _enterpriseLoggerMock = new Mock<ILogger<EnterpriseAuditRepository>>();
            _serviceLoggerMock = new Mock<ILogger<EnterpriseAuditService>>();
            _tokenIssuanceRepositoryMock = new Mock<ITokenIssuanceRepository>();

            _whitelistRepository = new WhitelistRepository(_whitelistLoggerMock.Object);
            _complianceRepository = new ComplianceRepository(_complianceLoggerMock.Object);
            _enterpriseAuditRepository = new EnterpriseAuditRepository(
                _whitelistRepository,
                _complianceRepository,
                _tokenIssuanceRepositoryMock.Object,
                _enterpriseLoggerMock.Object);
            var webhookService = Mock.Of<IWebhookService>();
            _enterpriseAuditService = new EnterpriseAuditService(
                _enterpriseAuditRepository,
                _serviceLoggerMock.Object,
                webhookService);
        }

        [Test]
        public async Task GetAuditLogAsync_UnifiedView_ShouldReturnBothWhitelistAndComplianceEvents()
        {
            // Arrange - Create whitelist and compliance audit entries
            var assetId = (ulong)12345;

            // Add whitelist event
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                NewStatus = WhitelistStatus.Active,
                Network = "voimain-v1.0"
            });

            // Add compliance event
            await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
            {
                AssetId = assetId,
                ActionType = ComplianceActionType.Create,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                Success = true,
                NewComplianceStatus = ComplianceStatus.Compliant,
                Network = "voimain-v1.0"
            });

            // Act
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = assetId
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Entries.Any(e => e.Category == AuditEventCategory.Whitelist), Is.True);
            Assert.That(result.Entries.Any(e => e.Category == AuditEventCategory.Compliance), Is.True);
            Assert.That(result.RetentionPolicy, Is.Not.Null);
            Assert.That(result.RetentionPolicy!.MinimumRetentionYears, Is.EqualTo(7));
            Assert.That(result.RetentionPolicy.RegulatoryFramework, Is.EqualTo("MICA"));
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.WhitelistEvents, Is.EqualTo(1));
            Assert.That(result.Summary.ComplianceEvents, Is.EqualTo(1));
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByVOINetwork_ShouldReturnOnlyVOIEvents()
        {
            // Arrange - Create events on different networks
            var assetId = (ulong)12345;

            // VOI network events
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-3),
                Network = "voimain-v1.0"
            });

            await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
            {
                AssetId = assetId,
                ActionType = ComplianceActionType.Create,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                Success = true,
                Network = "voimain-v1.0"
            });

            // Aramid network event
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                Network = "aramidmain-v1.0"
            });

            // Act - Filter by VOI network
            var request = new GetEnterpriseAuditLogRequest
            {
                Network = "voimain-v1.0"
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Entries.All(e => e.Network == "voimain-v1.0"), Is.True);
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.Networks, Contains.Item("voimain-v1.0"));
            Assert.That(result.Summary.Networks.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByAramidNetwork_ShouldReturnOnlyAramidEvents()
        {
            // Arrange - Create events on different networks
            var assetId = (ulong)12345;

            // Aramid network events
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                Network = "aramidmain-v1.0"
            });

            await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
            {
                AssetId = assetId,
                ActionType = ComplianceActionType.Update,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                Success = true,
                Network = "aramidmain-v1.0"
            });

            // VOI network event
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                Network = "voimain-v1.0"
            });

            // Act - Filter by Aramid network
            var request = new GetEnterpriseAuditLogRequest
            {
                Network = "aramidmain-v1.0"
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Entries.All(e => e.Network == "aramidmain-v1.0"), Is.True);
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByAssetId_ShouldReturnOnlyMatchingAsset()
        {
            // Arrange - Create events for different assets
            var assetId1 = (ulong)100;
            var assetId2 = (ulong)200;

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId1,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                Network = "voimain-v1.0"
            });

            await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
            {
                AssetId = assetId2,
                ActionType = ComplianceActionType.Create,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                Success = true,
                Network = "voimain-v1.0"
            });

            // Act - Filter by specific asset
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = assetId1
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].AssetId, Is.EqualTo(assetId1));
            Assert.That(result.Summary!.Assets, Contains.Item(assetId1));
            Assert.That(result.Summary.Assets.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByCategory_ShouldReturnOnlyMatchingCategory()
        {
            // Arrange - Create events of different categories
            var assetId = (ulong)12345;

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                Network = "voimain-v1.0"
            });

            await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
            {
                AssetId = assetId,
                ActionType = ComplianceActionType.Create,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                Success = true,
                Network = "voimain-v1.0"
            });

            // Act - Filter by Whitelist category only
            var request = new GetEnterpriseAuditLogRequest
            {
                Category = AuditEventCategory.Whitelist
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].Category, Is.EqualTo(AuditEventCategory.Whitelist));
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByDateRange_ShouldReturnOnlyEventsInRange()
        {
            // Arrange - Create events at different times
            var assetId = (ulong)12345;
            var now = DateTime.UtcNow;

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddDays(-10), // Outside range
                Network = "voimain-v1.0"
            });

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddDays(-3), // Inside range
                Network = "voimain-v1.0"
            });

            await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
            {
                AssetId = assetId,
                ActionType = ComplianceActionType.Create,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddDays(-1), // Inside range
                Success = true,
                Network = "voimain-v1.0"
            });

            // Act - Filter by date range (last 7 days)
            var request = new GetEnterpriseAuditLogRequest
            {
                FromDate = now.AddDays(-7),
                ToDate = now
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Entries.All(e => e.PerformedAt >= now.AddDays(-7) && e.PerformedAt <= now), Is.True);
            Assert.That(result.Summary!.DateRange, Is.Not.Null);
            Assert.That(result.Summary.DateRange!.EarliestEvent, Is.Not.Null);
            Assert.That(result.Summary.DateRange.LatestEvent, Is.Not.Null);
        }

        [Test]
        public async Task GetAuditLogAsync_Pagination_ShouldReturnCorrectPage()
        {
            // Arrange - Create multiple events
            var assetId = (ulong)12345;

            for (int i = 0; i < 10; i++)
            {
                await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                {
                    AssetId = assetId,
                    Address = ValidAddress1,
                    ActionType = WhitelistActionType.Add,
                    PerformedBy = TestPerformedBy,
                    PerformedAt = DateTime.UtcNow.AddMinutes(-i),
                    Network = "voimain-v1.0"
                });
            }

            // Act - Get page 2 with 3 items per page
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = assetId,
                Page = 2,
                PageSize = 3
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(3));
            Assert.That(result.Page, Is.EqualTo(2));
            Assert.That(result.PageSize, Is.EqualTo(3));
            Assert.That(result.TotalCount, Is.EqualTo(10));
            Assert.That(result.TotalPages, Is.EqualTo(4));
        }

        [Test]
        public async Task GetAuditLogAsync_OrderByMostRecentFirst_ShouldBeDescending()
        {
            // Arrange - Create events at different times
            var assetId = (ulong)12345;
            var now = DateTime.UtcNow;

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddHours(-3),
                Network = "voimain-v1.0"
            });

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddHours(-1),
                Network = "voimain-v1.0"
            });

            // Act
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = assetId
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Entries[0].PerformedAt, Is.GreaterThan(result.Entries[1].PerformedAt));
        }

        [Test]
        public async Task GetAuditLogAsync_CombinedFilters_ShouldApplyAllFilters()
        {
            // Arrange - Create various events
            var assetId = (ulong)12345;

            // Matching all criteria
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                Network = "voimain-v1.0"
            });

            // Different network
            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                Network = "aramidmain-v1.0"
            });

            // Different category
            await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
            {
                AssetId = assetId,
                ActionType = ComplianceActionType.Create,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                Success = true,
                Network = "voimain-v1.0"
            });

            // Act - Apply multiple filters
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = assetId,
                Network = "voimain-v1.0",
                Category = AuditEventCategory.Whitelist
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].AssetId, Is.EqualTo(assetId));
            Assert.That(result.Entries[0].Network, Is.EqualTo("voimain-v1.0"));
            Assert.That(result.Entries[0].Category, Is.EqualTo(AuditEventCategory.Whitelist));
        }

        [Test]
        public async Task ExportAuditLogCsvAsync_ShouldReturnValidCsv()
        {
            // Arrange
            var assetId = (ulong)12345;

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                NewStatus = WhitelistStatus.Active,
                Network = "voimain-v1.0",
                Notes = "Test entry"
            });

            // Act
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = assetId
            };

            var csv = await _enterpriseAuditService.ExportAuditLogCsvAsync(request);

            // Assert
            Assert.That(csv, Is.Not.Null);
            Assert.That(csv, Does.Contain("Id,AssetId,Network,Category")); // Header
            Assert.That(csv, Does.Contain("voimain-v1.0"));
            Assert.That(csv, Does.Contain("Whitelist"));
            Assert.That(csv, Does.Contain(assetId.ToString()));
        }

        [Test]
        public async Task ExportAuditLogJsonAsync_ShouldReturnValidJson()
        {
            // Arrange
            var assetId = (ulong)12345;

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                Network = "voimain-v1.0"
            });

            // Act
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = assetId
            };

            var json = await _enterpriseAuditService.ExportAuditLogJsonAsync(request);

            // Assert
            Assert.That(json, Is.Not.Null);
            Assert.That(json, Does.Contain("\"success\": true"));
            Assert.That(json, Does.Contain("\"entries\""));
            Assert.That(json, Does.Contain("\"retentionPolicy\""));
            Assert.That(json, Does.Contain("voimain-v1.0"));
        }

        [Test]
        public void GetRetentionPolicy_ShouldReturn7YearMICAPolicy()
        {
            // Act
            var policy = _enterpriseAuditService.GetRetentionPolicy();

            // Assert
            Assert.That(policy, Is.Not.Null);
            Assert.That(policy.MinimumRetentionYears, Is.EqualTo(7));
            Assert.That(policy.RegulatoryFramework, Is.EqualTo("MICA"));
            Assert.That(policy.ImmutableEntries, Is.True);
            Assert.That(policy.Description, Does.Contain("7 years"));
            Assert.That(policy.Description, Does.Contain("VOI"));
            Assert.That(policy.Description, Does.Contain("Aramid"));
        }

        [Test]
        public async Task GetAuditLogAsync_TransferValidation_ShouldMapToCorrectCategory()
        {
            // Arrange - Create transfer validation event
            var assetId = (ulong)12345;

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Amount = 1000,
                Network = "voimain-v1.0"
            });

            // Act
            var request = new GetEnterpriseAuditLogRequest
            {
                Category = AuditEventCategory.TransferValidation
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].Category, Is.EqualTo(AuditEventCategory.TransferValidation));
            Assert.That(result.Entries[0].ToAddress, Is.EqualTo(ValidAddress2));
            Assert.That(result.Entries[0].TransferAllowed, Is.True);
            Assert.That(result.Entries[0].Amount, Is.EqualTo(1000));
        }

        [Test]
        public async Task GetAuditLogAsync_MultipleNetworks_ShouldIncludeInSummary()
        {
            // Arrange - Create events on multiple networks
            var assetId = (ulong)12345;

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                Network = "voimain-v1.0"
            });

            await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                Network = "aramidmain-v1.0"
            });

            await _complianceRepository.AddAuditLogEntryAsync(new ComplianceAuditLogEntry
            {
                AssetId = assetId,
                ActionType = ComplianceActionType.Create,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                Success = true,
                Network = "mainnet-v1.0"
            });

            // Act
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = assetId
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.Networks.Count, Is.EqualTo(3));
            Assert.That(result.Summary.Networks, Contains.Item("voimain-v1.0"));
            Assert.That(result.Summary.Networks, Contains.Item("aramidmain-v1.0"));
            Assert.That(result.Summary.Networks, Contains.Item("mainnet-v1.0"));
        }

        [Test]
        public async Task GetAuditLogAsync_MaxPageSize_ShouldCapAt100()
        {
            // Arrange - Create many events
            var assetId = (ulong)12345;

            for (int i = 0; i < 150; i++)
            {
                await _whitelistRepository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                {
                    AssetId = assetId,
                    Address = ValidAddress1,
                    ActionType = WhitelistActionType.Add,
                    PerformedBy = TestPerformedBy,
                    PerformedAt = DateTime.UtcNow.AddMinutes(-i),
                    Network = "voimain-v1.0"
                });
            }

            // Act - Request more than max page size
            var request = new GetEnterpriseAuditLogRequest
            {
                AssetId = assetId,
                PageSize = 150
            };

            var result = await _enterpriseAuditService.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.PageSize, Is.EqualTo(100)); // Should be capped at 100
            Assert.That(result.Entries.Count, Is.LessThanOrEqualTo(100));
        }
    }
}
