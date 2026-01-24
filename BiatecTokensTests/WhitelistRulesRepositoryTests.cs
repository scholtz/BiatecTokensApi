using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;

namespace BiatecTokensTests
{
    [TestFixture]
    public class WhitelistRulesRepositoryTests
    {
        private WhitelistRulesRepository _repository;

        [SetUp]
        public void Setup()
        {
            _repository = new WhitelistRulesRepository();
        }

        [Test]
        public async Task AddRuleAsync_NewRule_ShouldSucceed()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Test Rule",
                Description = "Test Description",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                IsActive = true,
                Priority = 100,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            };

            // Act
            var result = await _repository.AddRuleAsync(rule);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task AddRuleAsync_DuplicateRule_ShouldFail()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                Id = "test-rule-id",
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.RequireKycForActive,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            };

            // Act
            var firstAdd = await _repository.AddRuleAsync(rule);
            var secondAdd = await _repository.AddRuleAsync(rule);

            // Assert
            Assert.That(firstAdd, Is.True);
            Assert.That(secondAdd, Is.False);
        }

        [Test]
        public async Task GetRuleAsync_ExistingRule_ShouldReturnRule()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.NetworkKycRequirement,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            };
            await _repository.AddRuleAsync(rule);

            // Act
            var result = await _repository.GetRuleAsync(rule.Id);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.AssetId, Is.EqualTo(12345));
            Assert.That(result?.Name, Is.EqualTo("Test Rule"));
        }

        [Test]
        public async Task GetRuleAsync_NonExistingRule_ShouldReturnNull()
        {
            // Act
            var result = await _repository.GetRuleAsync("non-existing-id");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetRulesByAssetIdAsync_MultipleRules_ShouldReturnOrderedByPriority()
        {
            // Arrange
            var rule1 = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Rule 1",
                Priority = 200,
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            };
            var rule2 = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Rule 2",
                Priority = 50,
                RuleType = WhitelistRuleType.RequireKycForActive,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            };
            var rule3 = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Rule 3",
                Priority = 100,
                RuleType = WhitelistRuleType.NetworkKycRequirement,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            };

            await _repository.AddRuleAsync(rule1);
            await _repository.AddRuleAsync(rule2);
            await _repository.AddRuleAsync(rule3);

            // Act
            var results = await _repository.GetRulesByAssetIdAsync(12345);

            // Assert
            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[0].Priority, Is.EqualTo(50)); // Lowest priority first
            Assert.That(results[1].Priority, Is.EqualTo(100));
            Assert.That(results[2].Priority, Is.EqualTo(200));
        }

        [Test]
        public async Task GetRulesByAssetIdAsync_FilterByRuleType_ShouldReturnMatchingRules()
        {
            // Arrange
            await _repository.AddRuleAsync(new WhitelistRule
            {
                AssetId = 12345,
                Name = "Rule 1",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            });
            await _repository.AddRuleAsync(new WhitelistRule
            {
                AssetId = 12345,
                Name = "Rule 2",
                RuleType = WhitelistRuleType.RequireKycForActive,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            });

            // Act
            var results = await _repository.GetRulesByAssetIdAsync(12345, WhitelistRuleType.AutoRevokeExpired);

            // Assert
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].RuleType, Is.EqualTo(WhitelistRuleType.AutoRevokeExpired));
        }

        [Test]
        public async Task GetRulesByAssetIdAsync_FilterByActiveStatus_ShouldReturnMatchingRules()
        {
            // Arrange
            await _repository.AddRuleAsync(new WhitelistRule
            {
                AssetId = 12345,
                Name = "Active Rule",
                IsActive = true,
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            });
            await _repository.AddRuleAsync(new WhitelistRule
            {
                AssetId = 12345,
                Name = "Inactive Rule",
                IsActive = false,
                RuleType = WhitelistRuleType.RequireKycForActive,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            });

            // Act
            var results = await _repository.GetRulesByAssetIdAsync(12345, isActive: true);

            // Assert
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].IsActive, Is.True);
        }

        [Test]
        public async Task GetRulesByAssetIdAsync_FilterByNetwork_ShouldReturnMatchingRules()
        {
            // Arrange
            await _repository.AddRuleAsync(new WhitelistRule
            {
                AssetId = 12345,
                Name = "VOI Rule",
                Network = "voimain-v1.0",
                RuleType = WhitelistRuleType.NetworkKycRequirement,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            });
            await _repository.AddRuleAsync(new WhitelistRule
            {
                AssetId = 12345,
                Name = "Aramid Rule",
                Network = "aramidmain-v1.0",
                RuleType = WhitelistRuleType.NetworkKycRequirement,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            });
            await _repository.AddRuleAsync(new WhitelistRule
            {
                AssetId = 12345,
                Name = "Global Rule",
                Network = null,
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            });

            // Act
            var results = await _repository.GetRulesByAssetIdAsync(12345, network: "voimain-v1.0");

            // Assert
            Assert.That(results.Count, Is.EqualTo(2)); // VOI rule + global rule
            Assert.That(results.Any(r => r.Network == "voimain-v1.0"), Is.True);
            Assert.That(results.Any(r => r.Network == null), Is.True);
        }

        [Test]
        public async Task UpdateRuleAsync_ExistingRule_ShouldSucceed()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Original Name",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            };
            await _repository.AddRuleAsync(rule);

            // Act
            rule.Name = "Updated Name";
            rule.IsActive = false;
            var result = await _repository.UpdateRuleAsync(rule);

            // Assert
            Assert.That(result, Is.True);
            var updated = await _repository.GetRuleAsync(rule.Id);
            Assert.That(updated?.Name, Is.EqualTo("Updated Name"));
            Assert.That(updated?.IsActive, Is.False);
        }

        [Test]
        public async Task UpdateRuleAsync_NonExistingRule_ShouldFail()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                Id = "non-existing-id",
                AssetId = 12345,
                Name = "Test",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            };

            // Act
            var result = await _repository.UpdateRuleAsync(rule);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task DeleteRuleAsync_ExistingRule_ShouldSucceed()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            };
            await _repository.AddRuleAsync(rule);

            // Act
            var result = await _repository.DeleteRuleAsync(rule.Id);

            // Assert
            Assert.That(result, Is.True);
            var deleted = await _repository.GetRuleAsync(rule.Id);
            Assert.That(deleted, Is.Null);
        }

        [Test]
        public async Task DeleteRuleAsync_NonExistingRule_ShouldFail()
        {
            // Act
            var result = await _repository.DeleteRuleAsync("non-existing-id");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task RecordRuleApplicationAsync_ExistingRule_ShouldUpdateCountAndTimestamp()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.AutoRevokeExpired,
                ApplicationCount = 0,
                CreatedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            };
            await _repository.AddRuleAsync(rule);

            // Act
            var appliedAt = DateTime.UtcNow;
            var result = await _repository.RecordRuleApplicationAsync(rule.Id, appliedAt);

            // Assert
            Assert.That(result, Is.True);
            var updated = await _repository.GetRuleAsync(rule.Id);
            Assert.That(updated?.ApplicationCount, Is.EqualTo(1));
            Assert.That(updated?.LastAppliedAt, Is.EqualTo(appliedAt));
        }

        [Test]
        public async Task AddAuditLogAsync_NewLog_ShouldSucceed()
        {
            // Arrange
            var auditLog = new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-id-123",
                RuleName = "Test Rule",
                ActionType = RuleAuditActionType.Create,
                PerformedBy = "TESTADDRESS123456789012345678901234567890123456789012"
            };

            // Act
            var result = await _repository.AddAuditLogAsync(auditLog);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task GetAuditLogsAsync_FilterByAssetId_ShouldReturnMatchingLogs()
        {
            // Arrange
            await _repository.AddAuditLogAsync(new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-1",
                RuleName = "Rule 1",
                ActionType = RuleAuditActionType.Create,
                PerformedBy = "USER1"
            });
            await _repository.AddAuditLogAsync(new WhitelistRuleAuditLog
            {
                AssetId = 67890,
                RuleId = "rule-2",
                RuleName = "Rule 2",
                ActionType = RuleAuditActionType.Create,
                PerformedBy = "USER2"
            });

            // Act
            var results = await _repository.GetAuditLogsAsync(12345);

            // Assert
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].AssetId, Is.EqualTo(12345ul));
        }

        [Test]
        public async Task GetAuditLogsAsync_FilterByRuleId_ShouldReturnMatchingLogs()
        {
            // Arrange
            await _repository.AddAuditLogAsync(new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-1",
                RuleName = "Rule 1",
                ActionType = RuleAuditActionType.Create,
                PerformedBy = "USER1"
            });
            await _repository.AddAuditLogAsync(new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-2",
                RuleName = "Rule 2",
                ActionType = RuleAuditActionType.Update,
                PerformedBy = "USER1"
            });

            // Act
            var results = await _repository.GetAuditLogsAsync(12345, ruleId: "rule-1");

            // Assert
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].RuleId, Is.EqualTo("rule-1"));
        }

        [Test]
        public async Task GetAuditLogsAsync_FilterByActionType_ShouldReturnMatchingLogs()
        {
            // Arrange
            await _repository.AddAuditLogAsync(new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-1",
                RuleName = "Rule 1",
                ActionType = RuleAuditActionType.Create,
                PerformedBy = "USER1"
            });
            await _repository.AddAuditLogAsync(new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-1",
                RuleName = "Rule 1",
                ActionType = RuleAuditActionType.Apply,
                PerformedBy = "USER1"
            });

            // Act
            var results = await _repository.GetAuditLogsAsync(12345, actionType: RuleAuditActionType.Apply);

            // Assert
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].ActionType, Is.EqualTo(RuleAuditActionType.Apply));
        }

        [Test]
        public async Task GetAuditLogsAsync_FilterByDateRange_ShouldReturnMatchingLogs()
        {
            // Arrange
            var oldDate = DateTime.UtcNow.AddDays(-10);
            var recentDate = DateTime.UtcNow.AddDays(-1);
            
            await _repository.AddAuditLogAsync(new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-1",
                RuleName = "Rule 1",
                ActionType = RuleAuditActionType.Create,
                PerformedBy = "USER1",
                PerformedAt = oldDate
            });
            await _repository.AddAuditLogAsync(new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-1",
                RuleName = "Rule 1",
                ActionType = RuleAuditActionType.Update,
                PerformedBy = "USER1",
                PerformedAt = recentDate
            });

            // Act
            var results = await _repository.GetAuditLogsAsync(
                12345,
                fromDate: DateTime.UtcNow.AddDays(-5),
                toDate: DateTime.UtcNow);

            // Assert
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].ActionType, Is.EqualTo(RuleAuditActionType.Update));
        }

        [Test]
        public async Task GetAuditLogsAsync_ShouldReturnOrderedByMostRecentFirst()
        {
            // Arrange
            var time1 = DateTime.UtcNow.AddHours(-2);
            var time2 = DateTime.UtcNow.AddHours(-1);
            var time3 = DateTime.UtcNow;

            await _repository.AddAuditLogAsync(new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-1",
                RuleName = "Rule 1",
                ActionType = RuleAuditActionType.Create,
                PerformedBy = "USER1",
                PerformedAt = time1
            });
            await _repository.AddAuditLogAsync(new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-1",
                RuleName = "Rule 1",
                ActionType = RuleAuditActionType.Update,
                PerformedBy = "USER1",
                PerformedAt = time2
            });
            await _repository.AddAuditLogAsync(new WhitelistRuleAuditLog
            {
                AssetId = 12345,
                RuleId = "rule-1",
                RuleName = "Rule 1",
                ActionType = RuleAuditActionType.Apply,
                PerformedBy = "USER1",
                PerformedAt = time3
            });

            // Act
            var results = await _repository.GetAuditLogsAsync(12345);

            // Assert
            Assert.That(results.Count, Is.EqualTo(3));
            Assert.That(results[0].PerformedAt, Is.EqualTo(time3)); // Most recent first
            Assert.That(results[1].PerformedAt, Is.EqualTo(time2));
            Assert.That(results[2].PerformedAt, Is.EqualTo(time1));
        }
    }
}
