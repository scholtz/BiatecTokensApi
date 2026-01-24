using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using NUnit.Framework;

namespace BiatecTokensTests
{
    [TestFixture]
    public class WhitelistRulesRepositoryTests
    {
        private WhitelistRulesRepository _repository = null!;

        [SetUp]
        public void Setup()
        {
            _repository = new WhitelistRulesRepository();
        }

        #region Create Rule Tests

        [Test]
        public async Task CreateRuleAsync_ValidRule_ShouldCreateSuccessfully()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Test Rule",
                Description = "Test Description",
                RuleType = WhitelistRuleType.KycRequired,
                CreatedBy = "TESTCREATOR",
                Configuration = new WhitelistRuleConfiguration
                {
                    KycMandatory = true
                }
            };

            // Act
            var created = await _repository.CreateRuleAsync(rule);

            // Assert
            Assert.That(created, Is.Not.Null);
            Assert.That(created.Id, Is.EqualTo(rule.Id));
            Assert.That(created.Name, Is.EqualTo("Test Rule"));
        }

        [Test]
        public void CreateRuleAsync_DuplicateId_ShouldThrowException()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                Id = "duplicate-id",
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.KycRequired,
                CreatedBy = "TESTCREATOR"
            };

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await _repository.CreateRuleAsync(rule));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _repository.CreateRuleAsync(rule));
        }

        #endregion

        #region Get Rule Tests

        [Test]
        public async Task GetRuleAsync_ExistingRule_ShouldReturnRule()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.KycRequired,
                CreatedBy = "TESTCREATOR"
            };
            await _repository.CreateRuleAsync(rule);

            // Act
            var retrieved = await _repository.GetRuleAsync(rule.Id);

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.Id, Is.EqualTo(rule.Id));
            Assert.That(retrieved.Name, Is.EqualTo("Test Rule"));
        }

        [Test]
        public async Task GetRuleAsync_NonExistingRule_ShouldReturnNull()
        {
            // Act
            var retrieved = await _repository.GetRuleAsync("non-existing-id");

            // Assert
            Assert.That(retrieved, Is.Null);
        }

        #endregion

        #region Update and Delete Tests

        [Test]
        public async Task UpdateRuleAsync_ExistingRule_ShouldUpdateSuccessfully()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Original Name",
                RuleType = WhitelistRuleType.KycRequired,
                CreatedBy = "TESTCREATOR"
            };
            await _repository.CreateRuleAsync(rule);

            // Act
            rule.Name = "Updated Name";
            var updated = await _repository.UpdateRuleAsync(rule);
            var retrieved = await _repository.GetRuleAsync(rule.Id);

            // Assert
            Assert.That(updated, Is.True);
            Assert.That(retrieved!.Name, Is.EqualTo("Updated Name"));
        }

        [Test]
        public async Task DeleteRuleAsync_ExistingRule_ShouldDeleteSuccessfully()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Test Rule",
                RuleType = WhitelistRuleType.KycRequired,
                CreatedBy = "TESTCREATOR"
            };
            await _repository.CreateRuleAsync(rule);

            // Act
            var deleted = await _repository.DeleteRuleAsync(rule.Id);
            var retrieved = await _repository.GetRuleAsync(rule.Id);

            // Assert
            Assert.That(deleted, Is.True);
            Assert.That(retrieved, Is.Null);
        }

        #endregion

        #region Get Rules For Asset Tests

        [Test]
        public async Task GetRulesForAssetAsync_MultipleRules_ShouldReturnSortedByPriority()
        {
            // Arrange
            var rule1 = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Rule 1",
                RuleType = WhitelistRuleType.KycRequired,
                Priority = 100,
                CreatedBy = "TESTCREATOR"
            };
            var rule2 = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Rule 2",
                RuleType = WhitelistRuleType.RoleBasedAccess,
                Priority = 200,
                CreatedBy = "TESTCREATOR"
            };

            await _repository.CreateRuleAsync(rule1);
            await _repository.CreateRuleAsync(rule2);

            // Act
            var rules = await _repository.GetRulesForAssetAsync(12345);

            // Assert
            Assert.That(rules.Count, Is.EqualTo(2));
            Assert.That(rules[0].Priority, Is.EqualTo(200)); // Higher priority first
            Assert.That(rules[1].Priority, Is.EqualTo(100));
        }

        [Test]
        public async Task GetRulesForAssetAsync_WithNetworkFilter_ShouldIncludeGlobalRules()
        {
            // Arrange
            var rule1 = new WhitelistRule
            {
                AssetId = 12345,
                Name = "VOI Rule",
                RuleType = WhitelistRuleType.NetworkSpecific,
                Network = "voimain-v1.0",
                CreatedBy = "TESTCREATOR"
            };
            var rule2 = new WhitelistRule
            {
                AssetId = 12345,
                Name = "Global Rule",
                RuleType = WhitelistRuleType.KycRequired,
                Network = null,
                CreatedBy = "TESTCREATOR"
            };

            await _repository.CreateRuleAsync(rule1);
            await _repository.CreateRuleAsync(rule2);

            // Act
            var rules = await _repository.GetRulesForAssetAsync(12345, network: "voimain-v1.0");

            // Assert
            Assert.That(rules.Count, Is.EqualTo(2)); // VOI rule + global rule
        }

        #endregion

        #region Audit Log Tests

        [Test]
        public async Task AddAuditLogAsync_ShouldAddSuccessfully()
        {
            // Arrange
            var auditLog = new WhitelistRuleAuditLog
            {
                RuleId = "test-rule-id",
                AssetId = 12345,
                ActionType = "Create",
                PerformedBy = "TESTUSER"
            };

            // Act
            var added = await _repository.AddAuditLogAsync(auditLog);

            // Assert
            Assert.That(added, Is.Not.Null);
            Assert.That(added.Id, Is.EqualTo(auditLog.Id));
        }

        [Test]
        public async Task GetAuditLogAsync_ShouldReturnSortedByDate()
        {
            // Arrange
            var ruleId = "test-rule-id";
            var auditLog1 = new WhitelistRuleAuditLog
            {
                RuleId = ruleId,
                AssetId = 12345,
                ActionType = "Create",
                PerformedBy = "TESTUSER",
                PerformedAt = DateTime.UtcNow.AddHours(-2)
            };
            var auditLog2 = new WhitelistRuleAuditLog
            {
                RuleId = ruleId,
                AssetId = 12345,
                ActionType = "Update",
                PerformedBy = "TESTUSER",
                PerformedAt = DateTime.UtcNow.AddHours(-1)
            };

            await _repository.AddAuditLogAsync(auditLog1);
            await _repository.AddAuditLogAsync(auditLog2);

            // Act
            var logs = await _repository.GetAuditLogAsync(ruleId);

            // Assert
            Assert.That(logs.Count, Is.EqualTo(2));
            Assert.That(logs[0].ActionType, Is.EqualTo("Update")); // Most recent first
        }

        #endregion
    }
}
