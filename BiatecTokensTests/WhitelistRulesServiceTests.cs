using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class WhitelistRulesServiceTests
    {
        private Mock<IWhitelistRulesRepository> _rulesRepositoryMock;
        private Mock<IWhitelistRepository> _whitelistRepositoryMock;
        private Mock<ILogger<WhitelistRulesService>> _loggerMock;
        private WhitelistRulesService _service;

        [SetUp]
        public void Setup()
        {
            _rulesRepositoryMock = new Mock<IWhitelistRulesRepository>();
            _whitelistRepositoryMock = new Mock<IWhitelistRepository>();
            _loggerMock = new Mock<ILogger<WhitelistRulesService>>();
            _service = new WhitelistRulesService(
                _rulesRepositoryMock.Object,
                _whitelistRepositoryMock.Object,
                _loggerMock.Object);
        }

        #region Create Rule Tests

        [Test]
        public async Task CreateRuleAsync_ValidRequest_ShouldSucceed()
        {
            // Arrange
            var request = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "Test Rule",
                Description = "Test Description",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                IsActive = true,
                Priority = 100
            };

            _rulesRepositoryMock.Setup(r => r.AddRuleAsync(It.IsAny<WhitelistRule>()))
                .ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.CreateRuleAsync(request, "TESTADDRESS123456789012345678901234567890123456789012");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rule, Is.Not.Null);
            Assert.That(result.Rule?.Name, Is.EqualTo("Test Rule"));
            Assert.That(result.Rule?.AssetId, Is.EqualTo(12345ul));

            _rulesRepositoryMock.Verify(r => r.AddRuleAsync(It.IsAny<WhitelistRule>()), Times.Once);
            _rulesRepositoryMock.Verify(r => r.AddAuditLogAsync(It.Is<WhitelistRuleAuditLog>(
                a => a.ActionType == RuleAuditActionType.Create)), Times.Once);
        }

        [Test]
        public async Task CreateRuleAsync_RepositoryFailure_ShouldReturnFailure()
        {
            // Arrange
            var request = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.AutoRevokeExpired
            };

            _rulesRepositoryMock.Setup(r => r.AddRuleAsync(It.IsAny<WhitelistRule>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.CreateRuleAsync(request, "TESTADDRESS123456789012345678901234567890123456789012");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null);
        }

        [Test]
        public async Task CreateRuleAsync_WithNetworkAndConfiguration_ShouldSucceed()
        {
            // Arrange
            var request = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "Network Rule",
                RuleType = WhitelistRuleType.NetworkKycRequirement,
                Network = "voimain-v1.0",
                Configuration = "{\"requireKyc\": true}"
            };

            _rulesRepositoryMock.Setup(r => r.AddRuleAsync(It.IsAny<WhitelistRule>()))
                .ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.CreateRuleAsync(request, "TESTADDRESS123456789012345678901234567890123456789012");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rule?.Network, Is.EqualTo("voimain-v1.0"));
            Assert.That(result.Rule?.Configuration, Is.EqualTo("{\"requireKyc\": true}"));
        }

        #endregion

        #region Update Rule Tests

        [Test]
        public async Task UpdateRuleAsync_ExistingRule_ShouldSucceed()
        {
            // Arrange
            var existingRule = new WhitelistRule
            {
                Id = "rule-123",
                AssetId = 12345,
                Name = "Original Name",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                IsActive = true,
                CreatedBy = "CREATOR"
            };

            var updateRequest = new UpdateWhitelistRuleRequest
            {
                RuleId = "rule-123",
                Name = "Updated Name",
                IsActive = false
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("rule-123"))
                .ReturnsAsync(existingRule);
            _rulesRepositoryMock.Setup(r => r.UpdateRuleAsync(It.IsAny<WhitelistRule>()))
                .ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateRuleAsync(updateRequest, "UPDATER");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rule?.Name, Is.EqualTo("Updated Name"));
            Assert.That(result.Rule?.IsActive, Is.False);

            _rulesRepositoryMock.Verify(r => r.UpdateRuleAsync(It.IsAny<WhitelistRule>()), Times.Once);
            _rulesRepositoryMock.Verify(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task UpdateRuleAsync_NonExistingRule_ShouldReturnNotFound()
        {
            // Arrange
            var updateRequest = new UpdateWhitelistRuleRequest
            {
                RuleId = "non-existing-id",
                Name = "Updated Name"
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("non-existing-id"))
                .ReturnsAsync((WhitelistRule?)null);

            // Act
            var result = await _service.UpdateRuleAsync(updateRequest, "UPDATER");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task UpdateRuleAsync_ActivationChange_ShouldLogActivateOrDeactivate()
        {
            // Arrange
            var existingRule = new WhitelistRule
            {
                Id = "rule-123",
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                IsActive = true,
                CreatedBy = "CREATOR"
            };

            var updateRequest = new UpdateWhitelistRuleRequest
            {
                RuleId = "rule-123",
                IsActive = false
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("rule-123"))
                .ReturnsAsync(existingRule);
            _rulesRepositoryMock.Setup(r => r.UpdateRuleAsync(It.IsAny<WhitelistRule>()))
                .ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateRuleAsync(updateRequest, "UPDATER");

            // Assert
            Assert.That(result.Success, Is.True);
            _rulesRepositoryMock.Verify(r => r.AddAuditLogAsync(It.Is<WhitelistRuleAuditLog>(
                a => a.ActionType == RuleAuditActionType.Deactivate)), Times.Once);
        }

        #endregion

        #region List Rules Tests

        [Test]
        public async Task ListRulesAsync_NoFilters_ShouldReturnAllRules()
        {
            // Arrange
            var rules = new List<WhitelistRule>
            {
                new WhitelistRule { AssetId = 12345, Name = "Rule 1", RuleType = WhitelistRuleType.AutoRevokeExpired, Priority = 1 },
                new WhitelistRule { AssetId = 12345, Name = "Rule 2", RuleType = WhitelistRuleType.RequireKycForActive, Priority = 2 },
                new WhitelistRule { AssetId = 12345, Name = "Rule 3", RuleType = WhitelistRuleType.NetworkKycRequirement, Priority = 3 }
            };

            _rulesRepositoryMock.Setup(r => r.GetRulesByAssetIdAsync(12345, null, null, null))
                .ReturnsAsync(rules);

            var request = new ListWhitelistRulesRequest { AssetId = 12345, Page = 1, PageSize = 20 };

            // Act
            var result = await _service.ListRulesAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rules.Count, Is.EqualTo(3));
            Assert.That(result.TotalCount, Is.EqualTo(3));
        }

        [Test]
        public async Task ListRulesAsync_WithPagination_ShouldReturnCorrectPage()
        {
            // Arrange
            var rules = Enumerable.Range(1, 50).Select(i => new WhitelistRule
            {
                AssetId = 12345,
                Name = $"Rule {i}",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                Priority = i
            }).ToList();

            _rulesRepositoryMock.Setup(r => r.GetRulesByAssetIdAsync(12345, null, null, null))
                .ReturnsAsync(rules);

            var request = new ListWhitelistRulesRequest { AssetId = 12345, Page = 2, PageSize = 20 };

            // Act
            var result = await _service.ListRulesAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rules.Count, Is.EqualTo(20));
            Assert.That(result.TotalCount, Is.EqualTo(50));
            Assert.That(result.TotalPages, Is.EqualTo(3));
            Assert.That(result.Page, Is.EqualTo(2));
        }

        #endregion

        #region Apply Rule Tests

        [Test]
        public async Task ApplyRuleAsync_AutoRevokeExpired_ShouldRevokeExpiredEntries()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                Id = "rule-123",
                AssetId = 12345,
                Name = "Auto-Revoke Expired",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                IsActive = true
            };

            var entries = new List<WhitelistEntry>
            {
                new WhitelistEntry
                {
                    AssetId = 12345,
                    Address = "ADDR1",
                    Status = WhitelistStatus.Active,
                    ExpirationDate = DateTime.UtcNow.AddDays(-1) // Expired
                },
                new WhitelistEntry
                {
                    AssetId = 12345,
                    Address = "ADDR2",
                    Status = WhitelistStatus.Active,
                    ExpirationDate = DateTime.UtcNow.AddDays(1) // Not expired
                }
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("rule-123"))
                .ReturnsAsync(rule);
            _whitelistRepositoryMock.Setup(r => r.GetEntriesByAssetIdAsync(12345, null))
                .ReturnsAsync(entries);
            _whitelistRepositoryMock.Setup(r => r.UpdateEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.RecordRuleApplicationAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()))
                .ReturnsAsync(true);

            var request = new ApplyWhitelistRuleRequest { RuleId = "rule-123", DryRun = false };

            // Act
            var result = await _service.ApplyRuleAsync(request, "APPLIER");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Result?.AffectedEntriesCount, Is.EqualTo(1));
            Assert.That(result.Result?.AffectedAddresses, Contains.Item("ADDR1"));

            _whitelistRepositoryMock.Verify(r => r.UpdateEntryAsync(It.Is<WhitelistEntry>(
                e => e.Address == "ADDR1" && e.Status == WhitelistStatus.Revoked)), Times.Once);
        }

        [Test]
        public async Task ApplyRuleAsync_RequireKycForActive_ShouldDeactivateEntriesWithoutKyc()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                Id = "rule-123",
                AssetId = 12345,
                Name = "Require KYC for Active",
                RuleType = WhitelistRuleType.RequireKycForActive,
                IsActive = true
            };

            var entries = new List<WhitelistEntry>
            {
                new WhitelistEntry
                {
                    AssetId = 12345,
                    Address = "ADDR1",
                    Status = WhitelistStatus.Active,
                    KycVerified = false
                },
                new WhitelistEntry
                {
                    AssetId = 12345,
                    Address = "ADDR2",
                    Status = WhitelistStatus.Active,
                    KycVerified = true
                }
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("rule-123"))
                .ReturnsAsync(rule);
            _whitelistRepositoryMock.Setup(r => r.GetEntriesByAssetIdAsync(12345, null))
                .ReturnsAsync(entries);
            _whitelistRepositoryMock.Setup(r => r.UpdateEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.RecordRuleApplicationAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()))
                .ReturnsAsync(true);

            var request = new ApplyWhitelistRuleRequest { RuleId = "rule-123", DryRun = false };

            // Act
            var result = await _service.ApplyRuleAsync(request, "APPLIER");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Result?.AffectedEntriesCount, Is.EqualTo(1));
            Assert.That(result.Result?.AffectedAddresses, Contains.Item("ADDR1"));

            _whitelistRepositoryMock.Verify(r => r.UpdateEntryAsync(It.Is<WhitelistEntry>(
                e => e.Address == "ADDR1" && e.Status == WhitelistStatus.Inactive)), Times.Once);
        }

        [Test]
        public async Task ApplyRuleAsync_DryRun_ShouldNotMakeChanges()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                Id = "rule-123",
                AssetId = 12345,
                Name = "Auto-Revoke Expired",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                IsActive = true
            };

            var entries = new List<WhitelistEntry>
            {
                new WhitelistEntry
                {
                    AssetId = 12345,
                    Address = "ADDR1",
                    Status = WhitelistStatus.Active,
                    ExpirationDate = DateTime.UtcNow.AddDays(-1) // Expired
                }
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("rule-123"))
                .ReturnsAsync(rule);
            _whitelistRepositoryMock.Setup(r => r.GetEntriesByAssetIdAsync(12345, null))
                .ReturnsAsync(entries);

            var request = new ApplyWhitelistRuleRequest { RuleId = "rule-123", DryRun = true };

            // Act
            var result = await _service.ApplyRuleAsync(request, "APPLIER");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Result?.AffectedEntriesCount, Is.EqualTo(1));

            // Verify no updates were made
            _whitelistRepositoryMock.Verify(r => r.UpdateEntryAsync(It.IsAny<WhitelistEntry>()), Times.Never);
            _rulesRepositoryMock.Verify(r => r.RecordRuleApplicationAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Test]
        public async Task ApplyRuleAsync_InactiveRule_ShouldReturnError()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                Id = "rule-123",
                AssetId = 12345,
                Name = "Inactive Rule",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                IsActive = false
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("rule-123"))
                .ReturnsAsync(rule);

            var request = new ApplyWhitelistRuleRequest { RuleId = "rule-123", DryRun = false };

            // Act
            var result = await _service.ApplyRuleAsync(request, "APPLIER");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("inactive"));
        }

        [Test]
        public async Task ApplyRuleAsync_WithTargetAddresses_ShouldOnlyAffectSpecifiedAddresses()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                Id = "rule-123",
                AssetId = 12345,
                Name = "Auto-Revoke Expired",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                IsActive = true
            };

            var entries = new List<WhitelistEntry>
            {
                new WhitelistEntry
                {
                    AssetId = 12345,
                    Address = "ADDR1",
                    Status = WhitelistStatus.Active,
                    ExpirationDate = DateTime.UtcNow.AddDays(-1) // Expired
                },
                new WhitelistEntry
                {
                    AssetId = 12345,
                    Address = "ADDR2",
                    Status = WhitelistStatus.Active,
                    ExpirationDate = DateTime.UtcNow.AddDays(-1) // Expired
                }
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("rule-123"))
                .ReturnsAsync(rule);
            _whitelistRepositoryMock.Setup(r => r.GetEntriesByAssetIdAsync(12345, null))
                .ReturnsAsync(entries);
            _whitelistRepositoryMock.Setup(r => r.UpdateEntryAsync(It.IsAny<WhitelistEntry>()))
                .ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.RecordRuleApplicationAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()))
                .ReturnsAsync(true);

            var request = new ApplyWhitelistRuleRequest
            {
                RuleId = "rule-123",
                TargetAddresses = new List<string> { "ADDR1" },
                DryRun = false
            };

            // Act
            var result = await _service.ApplyRuleAsync(request, "APPLIER");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Result?.AffectedEntriesCount, Is.EqualTo(1));
            Assert.That(result.Result?.AffectedAddresses, Contains.Item("ADDR1"));
            Assert.That(result.Result?.AffectedAddresses, Does.Not.Contain("ADDR2"));
        }

        #endregion

        #region Delete Rule Tests

        [Test]
        public async Task DeleteRuleAsync_ExistingRule_ShouldSucceed()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                Id = "rule-123",
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.AutoRevokeExpired
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("rule-123"))
                .ReturnsAsync(rule);
            _rulesRepositoryMock.Setup(r => r.DeleteRuleAsync("rule-123"))
                .ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()))
                .ReturnsAsync(true);

            var request = new DeleteWhitelistRuleRequest { RuleId = "rule-123" };

            // Act
            var result = await _service.DeleteRuleAsync(request, "DELETER");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.RuleId, Is.EqualTo("rule-123"));

            _rulesRepositoryMock.Verify(r => r.DeleteRuleAsync("rule-123"), Times.Once);
            _rulesRepositoryMock.Verify(r => r.AddAuditLogAsync(It.Is<WhitelistRuleAuditLog>(
                a => a.ActionType == RuleAuditActionType.Delete)), Times.Once);
        }

        [Test]
        public async Task DeleteRuleAsync_NonExistingRule_ShouldReturnNotFound()
        {
            // Arrange
            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("non-existing-id"))
                .ReturnsAsync((WhitelistRule?)null);

            var request = new DeleteWhitelistRuleRequest { RuleId = "non-existing-id" };

            // Act
            var result = await _service.DeleteRuleAsync(request, "DELETER");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        #endregion

        #region Get Audit Logs Tests

        [Test]
        public async Task GetAuditLogsAsync_ShouldReturnPaginatedResults()
        {
            // Arrange
            var auditLogs = Enumerable.Range(1, 100).Select(i => new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-123",
                RuleName = "Test Rule",
                ActionType = RuleAuditActionType.Create,
                PerformedBy = "USER",
                PerformedAt = DateTime.UtcNow.AddMinutes(-i)
            }).ToList();

            _rulesRepositoryMock.Setup(r => r.GetAuditLogsAsync(12345, null, null, null, null))
                .ReturnsAsync(auditLogs);

            // Act
            var result = await _service.GetAuditLogsAsync(12345, page: 2, pageSize: 50);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.EqualTo(50));
            Assert.That(result.TotalCount, Is.EqualTo(100));
            Assert.That(result.TotalPages, Is.EqualTo(2));
            Assert.That(result.Page, Is.EqualTo(2));
        }

        [Test]
        public async Task GetAuditLogsAsync_WithFilters_ShouldPassFiltersToRepository()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var toDate = DateTime.UtcNow;

            _rulesRepositoryMock.Setup(r => r.GetAuditLogsAsync(
                12345,
                "rule-123",
                RuleAuditActionType.Apply,
                fromDate,
                toDate))
                .ReturnsAsync(new List<WhitelistRuleAuditLog>());

            // Act
            await _service.GetAuditLogsAsync(
                12345,
                "rule-123",
                RuleAuditActionType.Apply,
                fromDate,
                toDate);

            // Assert
            _rulesRepositoryMock.Verify(r => r.GetAuditLogsAsync(
                12345,
                "rule-123",
                RuleAuditActionType.Apply,
                fromDate,
                toDate), Times.Once);
        }

        #endregion
    }
}
