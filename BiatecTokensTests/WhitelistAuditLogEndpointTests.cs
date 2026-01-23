using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for whitelist audit log query and export endpoints
    /// </summary>
    [TestFixture]
    public class WhitelistAuditLogEndpointTests
    {
        private WhitelistRepository _repository;
        private Mock<ILogger<WhitelistService>> _loggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private Mock<ISubscriptionTierService> _tierServiceMock;
        private Mock<ILogger<WhitelistRepository>> _repoLoggerMock;
        private WhitelistService _service;
        private const string TestPerformedBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string ValidAddress1 = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string ValidAddress2 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string ValidAddress3 = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBY5HFKQ";

        [SetUp]
        public void Setup()
        {
            _repoLoggerMock = new Mock<ILogger<WhitelistRepository>>();
            _repository = new WhitelistRepository(_repoLoggerMock.Object);
            _loggerMock = new Mock<ILogger<WhitelistService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _tierServiceMock = new Mock<ISubscriptionTierService>();
            
            // Setup default tier service behavior - Enterprise tier (no limits)
            _tierServiceMock.Setup(t => t.GetUserTierAsync(It.IsAny<string>()))
                .ReturnsAsync(BiatecTokensApi.Models.Subscription.SubscriptionTier.Enterprise);
            _tierServiceMock.Setup(t => t.ValidateOperationAsync(It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new SubscriptionTierValidationResult { IsAllowed = true, Tier = BiatecTokensApi.Models.Subscription.SubscriptionTier.Enterprise });
            _tierServiceMock.Setup(t => t.IsBulkOperationEnabledAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
            
            _service = new WhitelistService(_repository, _loggerMock.Object, _meteringServiceMock.Object, _tierServiceMock.Object);
        }

        [Test]
        public async Task GetAuditLogAsync_WithOptionalAssetId_ShouldReturnAllAssets()
        {
            // Arrange - Create audit entries for multiple assets
            var assetId1 = (ulong)100;
            var assetId2 = (ulong)200;
            
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId1,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                NewStatus = WhitelistStatus.Active,
                Network = "voimain-v1.0"
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId2,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                NewStatus = WhitelistStatus.Active,
                Network = "aramidmain-v1.0"
            });

            // Act - Query without asset ID (should return all)
            var request = new GetWhitelistAuditLogRequest
            {
                AssetId = null // Query all assets
            };

            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Entries.Any(e => e.AssetId == assetId1), Is.True);
            Assert.That(result.Entries.Any(e => e.AssetId == assetId2), Is.True);
            Assert.That(result.RetentionPolicy, Is.Not.Null);
            Assert.That(result.RetentionPolicy!.MinimumRetentionYears, Is.EqualTo(7));
            Assert.That(result.RetentionPolicy.RegulatoryFramework, Is.EqualTo("MICA"));
            Assert.That(result.RetentionPolicy.ImmutableEntries, Is.True);
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByNetwork_ShouldReturnMatchingEntries()
        {
            // Arrange
            var assetId1 = (ulong)100;
            var assetId2 = (ulong)200;
            
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId1,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                Network = "voimain-v1.0"
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId2,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                Network = "aramidmain-v1.0"
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId2,
                Address = ValidAddress3,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                Network = "voimain-v1.0"
            });

            // Act - Filter by network
            var request = new GetWhitelistAuditLogRequest
            {
                Network = "voimain-v1.0"
            };

            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Entries.All(e => e.Network == "voimain-v1.0"), Is.True);
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByActor_ShouldReturnMatchingEntries()
        {
            // Arrange
            var assetId = (ulong)100;
            var actor1 = "ACTOR1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            var actor2 = "ACTOR2AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = actor1,
                PerformedAt = DateTime.UtcNow
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = actor2,
                PerformedAt = DateTime.UtcNow
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress3,
                ActionType = WhitelistActionType.Remove,
                PerformedBy = actor1,
                PerformedAt = DateTime.UtcNow
            });

            // Act
            var request = new GetWhitelistAuditLogRequest
            {
                PerformedBy = actor1
            };

            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(2));
            Assert.That(result.Entries.All(e => e.PerformedBy == actor1), Is.True);
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByDateRange_ShouldReturnMatchingEntries()
        {
            // Arrange
            var assetId = (ulong)100;
            var now = DateTime.UtcNow;
            
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddDays(-10)
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddDays(-5)
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress3,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddDays(-1)
            });

            // Act - Filter by date range (last 7 days)
            var request = new GetWhitelistAuditLogRequest
            {
                FromDate = now.AddDays(-7),
                ToDate = now
            };

            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(2), "Should return only entries from last 7 days");
            Assert.That(result.Entries.All(e => e.PerformedAt >= now.AddDays(-7) && e.PerformedAt <= now), Is.True);
        }

        [Test]
        public async Task GetAuditLogAsync_FilterByActionType_ShouldReturnMatchingEntries()
        {
            // Arrange
            var assetId = (ulong)100;
            
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Remove,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                ToAddress = ValidAddress2,
                TransferAllowed = true
            });

            // Act - Filter by Add action type
            var request = new GetWhitelistAuditLogRequest
            {
                ActionType = WhitelistActionType.Add
            };

            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].ActionType, Is.EqualTo(WhitelistActionType.Add));
        }

        [Test]
        public async Task GetAuditLogAsync_CombinedFilters_ShouldReturnMatchingEntries()
        {
            // Arrange
            var assetId1 = (ulong)100;
            var assetId2 = (ulong)200;
            var now = DateTime.UtcNow;
            
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId1,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddHours(-1),
                Network = "voimain-v1.0"
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId1,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Remove,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddHours(-2),
                Network = "voimain-v1.0"
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId2,
                Address = ValidAddress3,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddHours(-1),
                Network = "aramidmain-v1.0"
            });

            // Act - Filter by asset ID + action type + network
            var request = new GetWhitelistAuditLogRequest
            {
                AssetId = assetId1,
                ActionType = WhitelistActionType.Add,
                Network = "voimain-v1.0"
            };

            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].AssetId, Is.EqualTo(assetId1));
            Assert.That(result.Entries[0].ActionType, Is.EqualTo(WhitelistActionType.Add));
            Assert.That(result.Entries[0].Network, Is.EqualTo("voimain-v1.0"));
        }

        [Test]
        public async Task GetAuditLogAsync_Pagination_ShouldReturnCorrectPage()
        {
            // Arrange - Create 10 entries
            var assetId = (ulong)100;
            for (int i = 0; i < 10; i++)
            {
                await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                {
                    AssetId = assetId,
                    Address = ValidAddress1,
                    ActionType = WhitelistActionType.Add,
                    PerformedBy = TestPerformedBy,
                    PerformedAt = DateTime.UtcNow.AddMinutes(-i),
                    Notes = $"Entry {i}"
                });
            }

            // Act - Get page 2 with page size 3
            var request = new GetWhitelistAuditLogRequest
            {
                AssetId = assetId,
                Page = 2,
                PageSize = 3
            };

            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(3), "Should return 3 entries for page 2");
            Assert.That(result.TotalCount, Is.EqualTo(10), "Total count should be 10");
            Assert.That(result.Page, Is.EqualTo(2));
            Assert.That(result.PageSize, Is.EqualTo(3));
            Assert.That(result.TotalPages, Is.EqualTo(4), "Should have 4 pages total (10 entries / 3 per page)");
        }

        [Test]
        public async Task GetAuditLogAsync_ShouldIncludeRetentionPolicy()
        {
            // Arrange
            var assetId = (ulong)100;
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow
            });

            // Act
            var request = new GetWhitelistAuditLogRequest
            {
                AssetId = assetId
            };

            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.RetentionPolicy, Is.Not.Null, "Retention policy should be included");
            Assert.That(result.RetentionPolicy!.MinimumRetentionYears, Is.EqualTo(7));
            Assert.That(result.RetentionPolicy.RegulatoryFramework, Is.EqualTo("MICA"));
            Assert.That(result.RetentionPolicy.ImmutableEntries, Is.True);
            Assert.That(result.RetentionPolicy.Description, Does.Contain("7 years"));
            Assert.That(result.RetentionPolicy.Description, Does.Contain("MICA"));
            Assert.That(result.RetentionPolicy.Description, Does.Contain("immutable"));
        }

        [Test]
        public async Task GetAuditLogAsync_EntriesShouldBeOrderedByMostRecentFirst()
        {
            // Arrange
            var assetId = (ulong)100;
            var now = DateTime.UtcNow;
            
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddHours(-3),
                Notes = "Oldest"
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddHours(-1),
                Notes = "Newest"
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress3,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = now.AddHours(-2),
                Notes = "Middle"
            });

            // Act
            var request = new GetWhitelistAuditLogRequest
            {
                AssetId = assetId
            };

            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(3));
            Assert.That(result.Entries[0].Notes, Is.EqualTo("Newest"), "First entry should be most recent");
            Assert.That(result.Entries[1].Notes, Is.EqualTo("Middle"));
            Assert.That(result.Entries[2].Notes, Is.EqualTo("Oldest"), "Last entry should be oldest");
        }

        [Test]
        public async Task GetAuditLogAsync_EmptyResults_ShouldReturnEmptyList()
        {
            // Act - Query for non-existent asset
            var request = new GetWhitelistAuditLogRequest
            {
                AssetId = 99999
            };

            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(0));
            Assert.That(result.TotalCount, Is.EqualTo(0));
            Assert.That(result.TotalPages, Is.EqualTo(0));
            Assert.That(result.RetentionPolicy, Is.Not.Null, "Retention policy should still be included");
        }

        [Test]
        public async Task GetAuditLogAsync_MaxPageSize_ShouldCapAt100()
        {
            // Arrange
            var assetId = (ulong)100;
            for (int i = 0; i < 150; i++)
            {
                await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                {
                    AssetId = assetId,
                    Address = ValidAddress1,
                    ActionType = WhitelistActionType.Add,
                    PerformedBy = TestPerformedBy,
                    PerformedAt = DateTime.UtcNow.AddMinutes(-i)
                });
            }

            // Act - Request with page size > 100
            var request = new GetWhitelistAuditLogRequest
            {
                AssetId = assetId,
                PageSize = 200 // Should be capped at 100
            };

            var result = await _service.GetAuditLogAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.LessThanOrEqualTo(100), "Should return max 100 entries per page");
            Assert.That(result.PageSize, Is.EqualTo(100), "PageSize should be capped at 100");
        }
    }
}
