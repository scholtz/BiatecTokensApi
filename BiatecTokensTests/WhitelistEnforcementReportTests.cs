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
    /// Tests for whitelist enforcement audit report endpoints
    /// </summary>
    [TestFixture]
    public class WhitelistEnforcementReportTests
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
            
            var webhookServiceMock = Mock.Of<IWebhookService>();
            _service = new WhitelistService(_repository, _loggerMock.Object, _meteringServiceMock.Object, _tierServiceMock.Object, webhookServiceMock);
        }

        [Test]
        public async Task GetEnforcementReportAsync_ShouldReturnOnlyTransferValidationEvents()
        {
            // Arrange - Create mixed audit entries (enforcement and non-enforcement)
            var assetId = (ulong)100;
            
            // Add whitelist entry (should NOT appear in enforcement report)
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.Add,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-3),
                NewStatus = WhitelistStatus.Active,
                Network = "voimain-v1.0"
            });

            // Add transfer validation (should appear)
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Amount = 1000,
                Network = "voimain-v1.0"
            });

            // Add another transfer validation (denied - should appear)
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                ToAddress = ValidAddress3,
                TransferAllowed = false,
                DenialReason = "Receiver not whitelisted",
                Amount = 500,
                Network = "voimain-v1.0"
            });

            // Act
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(2), "Should only return TransferValidation events");
            Assert.That(result.Entries.All(e => e.ActionType == WhitelistActionType.TransferValidation), Is.True);
            Assert.That(result.Entries.Any(e => e.TransferAllowed == true), Is.True, "Should include allowed transfer");
            Assert.That(result.Entries.Any(e => e.TransferAllowed == false), Is.True, "Should include denied transfer");
        }

        [Test]
        public async Task GetEnforcementReportAsync_ShouldIncludeSummaryStatistics()
        {
            // Arrange
            var assetId = (ulong)100;
            
            // Add 3 allowed and 2 denied transfers
            for (int i = 0; i < 3; i++)
            {
                await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                {
                    AssetId = assetId,
                    Address = ValidAddress1,
                    ActionType = WhitelistActionType.TransferValidation,
                    PerformedBy = TestPerformedBy,
                    PerformedAt = DateTime.UtcNow.AddMinutes(-i),
                    ToAddress = ValidAddress2,
                    TransferAllowed = true,
                    Amount = 1000,
                    Network = "voimain-v1.0"
                });
            }

            for (int i = 0; i < 2; i++)
            {
                await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                {
                    AssetId = assetId,
                    Address = ValidAddress2,
                    ActionType = WhitelistActionType.TransferValidation,
                    PerformedBy = TestPerformedBy,
                    PerformedAt = DateTime.UtcNow.AddMinutes(-10 - i),
                    ToAddress = ValidAddress3,
                    TransferAllowed = false,
                    DenialReason = "Receiver not whitelisted",
                    Amount = 500,
                    Network = "voimain-v1.0"
                });
            }

            // Act
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.TotalValidations, Is.EqualTo(5));
            Assert.That(result.Summary.AllowedTransfers, Is.EqualTo(3));
            Assert.That(result.Summary.DeniedTransfers, Is.EqualTo(2));
            Assert.That(result.Summary.AllowedPercentage, Is.EqualTo(60.0));
            Assert.That(result.Summary.DeniedPercentage, Is.EqualTo(40.0));
        }

        [Test]
        public async Task GetEnforcementReportAsync_ShouldIncludeDenialReasons()
        {
            // Arrange
            var assetId = (ulong)100;
            
            // Add denied transfers with different reasons
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-3),
                ToAddress = ValidAddress2,
                TransferAllowed = false,
                DenialReason = "Receiver not whitelisted",
                Network = "voimain-v1.0"
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                ToAddress = ValidAddress3,
                TransferAllowed = false,
                DenialReason = "Receiver not whitelisted",
                Network = "voimain-v1.0"
            });

            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                ToAddress = ValidAddress1,
                TransferAllowed = false,
                DenialReason = "Sender entry expired",
                Network = "voimain-v1.0"
            });

            // Act
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.DenialReasons.Count, Is.EqualTo(2));
            Assert.That(result.Summary.DenialReasons["Receiver not whitelisted"], Is.EqualTo(2));
            Assert.That(result.Summary.DenialReasons["Sender entry expired"], Is.EqualTo(1));
        }

        [Test]
        public async Task GetEnforcementReportAsync_FilterByTransferAllowed_ShouldReturnOnlyAllowedTransfers()
        {
            // Arrange
            var assetId = (ulong)100;
            
            // Add allowed transfer
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Amount = 1000,
                Network = "voimain-v1.0"
            });

            // Add denied transfer
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                ToAddress = ValidAddress3,
                TransferAllowed = false,
                DenialReason = "Receiver not whitelisted",
                Network = "voimain-v1.0"
            });

            // Act - Filter for allowed only
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId,
                TransferAllowed = true
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].TransferAllowed, Is.True);
        }

        [Test]
        public async Task GetEnforcementReportAsync_FilterByTransferDenied_ShouldReturnOnlyDeniedTransfers()
        {
            // Arrange
            var assetId = (ulong)100;
            
            // Add allowed transfer
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Amount = 1000,
                Network = "voimain-v1.0"
            });

            // Add denied transfer
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                ToAddress = ValidAddress3,
                TransferAllowed = false,
                DenialReason = "Receiver not whitelisted",
                Network = "voimain-v1.0"
            });

            // Act - Filter for denied only
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId,
                TransferAllowed = false
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].TransferAllowed, Is.False);
            Assert.That(result.Entries[0].DenialReason, Is.Not.Null);
        }

        [Test]
        public async Task GetEnforcementReportAsync_FilterByFromAddress_ShouldReturnMatchingEntries()
        {
            // Arrange
            var assetId = (ulong)100;
            
            // Add transfer from ValidAddress1
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Network = "voimain-v1.0"
            });

            // Add transfer from ValidAddress2
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                ToAddress = ValidAddress3,
                TransferAllowed = false,
                DenialReason = "Receiver not whitelisted",
                Network = "voimain-v1.0"
            });

            // Act
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId,
                FromAddress = ValidAddress1
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].Address, Is.EqualTo(ValidAddress1));
        }

        [Test]
        public async Task GetEnforcementReportAsync_FilterByToAddress_ShouldReturnMatchingEntries()
        {
            // Arrange
            var assetId = (ulong)100;
            
            // Add transfer to ValidAddress2
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Network = "voimain-v1.0"
            });

            // Add transfer to ValidAddress3
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                ToAddress = ValidAddress3,
                TransferAllowed = false,
                DenialReason = "Receiver not whitelisted",
                Network = "voimain-v1.0"
            });

            // Act
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId,
                ToAddress = ValidAddress2
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].ToAddress, Is.EqualTo(ValidAddress2));
        }

        [Test]
        public async Task GetEnforcementReportAsync_FilterByNetwork_ShouldReturnMatchingEntries()
        {
            // Arrange
            var assetId = (ulong)100;
            
            // Add VOI network transfer
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Network = "voimain-v1.0"
            });

            // Add Aramid network transfer
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                ToAddress = ValidAddress3,
                TransferAllowed = false,
                DenialReason = "Receiver not whitelisted",
                Network = "aramidmain-v1.0"
            });

            // Act
            var request = new GetWhitelistEnforcementReportRequest
            {
                Network = "voimain-v1.0"
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(1));
            Assert.That(result.Entries[0].Network, Is.EqualTo("voimain-v1.0"));
        }

        [Test]
        public async Task GetEnforcementReportAsync_MultipleAssets_ShouldIncludeInSummary()
        {
            // Arrange
            var assetId1 = (ulong)100;
            var assetId2 = (ulong)200;
            
            // Add transfer for asset 1
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId1,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Network = "voimain-v1.0"
            });

            // Add transfer for asset 2
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId2,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                ToAddress = ValidAddress3,
                TransferAllowed = false,
                DenialReason = "Receiver not whitelisted",
                Network = "aramidmain-v1.0"
            });

            // Act - Query without asset ID (all assets)
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = null
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.UniqueAssets.Count, Is.EqualTo(2));
            Assert.That(result.Summary.UniqueAssets.Contains(assetId1), Is.True);
            Assert.That(result.Summary.UniqueAssets.Contains(assetId2), Is.True);
        }

        [Test]
        public async Task GetEnforcementReportAsync_MultipleNetworks_ShouldIncludeInSummary()
        {
            // Arrange
            var assetId = (ulong)100;
            
            // Add VOI network transfer
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-2),
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Network = "voimain-v1.0"
            });

            // Add Aramid network transfer
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow.AddHours(-1),
                ToAddress = ValidAddress3,
                TransferAllowed = false,
                DenialReason = "Receiver not whitelisted",
                Network = "aramidmain-v1.0"
            });

            // Act
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.UniqueNetworks.Count, Is.EqualTo(2));
            Assert.That(result.Summary.UniqueNetworks.Contains("voimain-v1.0"), Is.True);
            Assert.That(result.Summary.UniqueNetworks.Contains("aramidmain-v1.0"), Is.True);
        }

        [Test]
        public async Task GetEnforcementReportAsync_ShouldIncludeDateRange()
        {
            // Arrange
            var assetId = (ulong)100;
            var earliestDate = DateTime.UtcNow.AddHours(-5);
            var latestDate = DateTime.UtcNow.AddHours(-1);
            
            // Add transfer at earliest time
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = earliestDate,
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Network = "voimain-v1.0"
            });

            // Add transfer at latest time
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress2,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = latestDate,
                ToAddress = ValidAddress3,
                TransferAllowed = false,
                DenialReason = "Receiver not whitelisted",
                Network = "voimain-v1.0"
            });

            // Act
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.DateRange, Is.Not.Null);
            Assert.That(result.Summary.DateRange!.EarliestEvent, Is.Not.Null);
            Assert.That(result.Summary.DateRange.LatestEvent, Is.Not.Null);
            // Allow small time difference due to test execution time
            Assert.That(result.Summary.DateRange.EarliestEvent!.Value, Is.EqualTo(earliestDate).Within(TimeSpan.FromSeconds(1)));
            Assert.That(result.Summary.DateRange.LatestEvent!.Value, Is.EqualTo(latestDate).Within(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public async Task GetEnforcementReportAsync_ShouldIncludeRetentionPolicy()
        {
            // Arrange
            var assetId = (ulong)100;
            
            await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
            {
                AssetId = assetId,
                Address = ValidAddress1,
                ActionType = WhitelistActionType.TransferValidation,
                PerformedBy = TestPerformedBy,
                PerformedAt = DateTime.UtcNow,
                ToAddress = ValidAddress2,
                TransferAllowed = true,
                Network = "voimain-v1.0"
            });

            // Act
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.RetentionPolicy, Is.Not.Null);
            Assert.That(result.RetentionPolicy!.MinimumRetentionYears, Is.EqualTo(7));
            Assert.That(result.RetentionPolicy.RegulatoryFramework, Is.EqualTo("MICA"));
            Assert.That(result.RetentionPolicy.ImmutableEntries, Is.True);
        }

        [Test]
        public async Task GetEnforcementReportAsync_Pagination_ShouldWorkCorrectly()
        {
            // Arrange
            var assetId = (ulong)100;
            
            // Add 5 enforcement events
            for (int i = 0; i < 5; i++)
            {
                await _repository.AddAuditLogEntryAsync(new WhitelistAuditLogEntry
                {
                    AssetId = assetId,
                    Address = ValidAddress1,
                    ActionType = WhitelistActionType.TransferValidation,
                    PerformedBy = TestPerformedBy,
                    PerformedAt = DateTime.UtcNow.AddMinutes(-i),
                    ToAddress = ValidAddress2,
                    TransferAllowed = true,
                    Network = "voimain-v1.0"
                });
            }

            // Act - Request page 1 with 2 items
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = assetId,
                Page = 1,
                PageSize = 2
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.LessThanOrEqualTo(2));
            Assert.That(result.TotalCount, Is.EqualTo(5));
            Assert.That(result.TotalPages, Is.EqualTo(3));
            Assert.That(result.Page, Is.EqualTo(1));
        }

        [Test]
        public async Task GetEnforcementReportAsync_EmptyResults_ShouldReturnEmptyList()
        {
            // Arrange - No enforcement events

            // Act
            var request = new GetWhitelistEnforcementReportRequest
            {
                AssetId = 999
            };

            var result = await _service.GetEnforcementReportAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(0));
            Assert.That(result.TotalCount, Is.EqualTo(0));
            Assert.That(result.Summary, Is.Not.Null);
            Assert.That(result.Summary!.TotalValidations, Is.EqualTo(0));
        }
    }
}
