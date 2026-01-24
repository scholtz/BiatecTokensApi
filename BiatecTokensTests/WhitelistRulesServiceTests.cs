using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    [TestFixture]
    public class WhitelistRulesServiceTests
    {
        private Mock<IWhitelistRulesRepository> _rulesRepositoryMock = null!;
        private Mock<IWhitelistRepository> _whitelistRepositoryMock = null!;
        private Mock<ILogger<WhitelistRulesService>> _loggerMock = null!;
        private WhitelistRulesService _service = null!;

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

        #region CreateRule Tests

        [Test]
        public async Task CreateRuleAsync_ValidKycRule_ShouldCreateSuccessfully()
        {
            // Arrange
            var request = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "KYC Required",
                RuleType = WhitelistRuleType.KycRequired,
                Configuration = new WhitelistRuleConfiguration
                {
                    KycMandatory = true
                }
            };

            _rulesRepositoryMock
                .Setup(r => r.CreateRuleAsync(It.IsAny<WhitelistRule>()))
                .ReturnsAsync((WhitelistRule r) => r);

            _rulesRepositoryMock
                .Setup(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()))
                .ReturnsAsync((WhitelistRuleAuditLog log) => log);

            // Act
            var result = await _service.CreateRuleAsync(request, "TESTCREATOR");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rule, Is.Not.Null);
            Assert.That(result.Rule!.Name, Is.EqualTo("KYC Required"));
            _rulesRepositoryMock.Verify(r => r.CreateRuleAsync(It.IsAny<WhitelistRule>()), Times.Once);
            _rulesRepositoryMock.Verify(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()), Times.Once);
        }

        [Test]
        public async Task CreateRuleAsync_KycRuleWithoutMandatoryOrProviders_ShouldFail()
        {
            // Arrange
            var request = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "Invalid KYC Rule",
                RuleType = WhitelistRuleType.KycRequired,
                Configuration = new WhitelistRuleConfiguration()
            };

            // Act
            var result = await _service.CreateRuleAsync(request, "TESTCREATOR");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("KycMandatory"));
        }

        [Test]
        public async Task CreateRuleAsync_RoleRuleWithoutMinimumRole_ShouldFail()
        {
            // Arrange
            var request = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "Invalid Role Rule",
                RuleType = WhitelistRuleType.RoleBasedAccess,
                Configuration = new WhitelistRuleConfiguration()
            };

            // Act
            var result = await _service.CreateRuleAsync(request, "TESTCREATOR");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("MinimumRole"));
        }

        #endregion

        #region UpdateRule Tests

        [Test]
        public async Task UpdateRuleAsync_ExistingRule_ShouldUpdateSuccessfully()
        {
            // Arrange
            var existingRule = new WhitelistRule
            {
                Id = "rule-123",
                AssetId = 12345,
                Name = "Original Name",
                RuleType = WhitelistRuleType.KycRequired,
                Configuration = new WhitelistRuleConfiguration { KycMandatory = true }
            };

            var request = new UpdateWhitelistRuleRequest
            {
                RuleId = "rule-123",
                Name = "Updated Name",
                Priority = 150
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("rule-123")).ReturnsAsync(existingRule);
            _rulesRepositoryMock.Setup(r => r.UpdateRuleAsync(It.IsAny<WhitelistRule>())).ReturnsAsync(true);
            _rulesRepositoryMock.Setup(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()))
                .ReturnsAsync((WhitelistRuleAuditLog log) => log);

            // Act
            var result = await _service.UpdateRuleAsync(request, "TESTUPDATER");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rule!.Name, Is.EqualTo("Updated Name"));
            Assert.That(result.Rule.Priority, Is.EqualTo(150));
        }

        [Test]
        public async Task UpdateRuleAsync_NonExistingRule_ShouldFail()
        {
            // Arrange
            var request = new UpdateWhitelistRuleRequest
            {
                RuleId = "non-existing",
                Name = "Test"
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("non-existing")).ReturnsAsync((WhitelistRule?)null);

            // Act
            var result = await _service.UpdateRuleAsync(request, "TESTUPDATER");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        #endregion

        #region ValidateEntryAgainstRule Tests

        [Test]
        public async Task ValidateEntryAgainstRuleAsync_KycRule_EntryWithoutKyc_ShouldFail()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                Address = "TESTADDRESS",
                AssetId = 12345,
                KycVerified = false
            };

            var rule = new WhitelistRule
            {
                RuleType = WhitelistRuleType.KycRequired,
                Name = "KYC Required",
                Configuration = new WhitelistRuleConfiguration
                {
                    KycMandatory = true
                }
            };

            // Act
            var error = await _service.ValidateEntryAgainstRuleAsync(entry, rule);

            // Assert
            Assert.That(error, Is.Not.Null);
            Assert.That(error!.ErrorMessage, Does.Contain("KYC"));
        }

        [Test]
        public async Task ValidateEntryAgainstRuleAsync_KycRule_EntryWithKyc_ShouldPass()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                Address = "TESTADDRESS",
                AssetId = 12345,
                KycVerified = true,
                KycProvider = "Sumsub"
            };

            var rule = new WhitelistRule
            {
                RuleType = WhitelistRuleType.KycRequired,
                Name = "KYC Required",
                Configuration = new WhitelistRuleConfiguration
                {
                    KycMandatory = true,
                    ApprovedKycProviders = new List<string> { "Sumsub", "Onfido" }
                }
            };

            // Act
            var error = await _service.ValidateEntryAgainstRuleAsync(entry, rule);

            // Assert
            Assert.That(error, Is.Null);
        }

        [Test]
        public async Task ValidateEntryAgainstRuleAsync_RoleRule_InsufficientRole_ShouldFail()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                Address = "TESTADDRESS",
                AssetId = 12345,
                Role = WhitelistRole.Operator // Lower privilege
            };

            var rule = new WhitelistRule
            {
                RuleType = WhitelistRuleType.RoleBasedAccess,
                Name = "Admin Required",
                Configuration = new WhitelistRuleConfiguration
                {
                    MinimumRole = WhitelistRole.Admin
                }
            };

            // Act
            var error = await _service.ValidateEntryAgainstRuleAsync(entry, rule);

            // Assert
            Assert.That(error, Is.Not.Null);
            Assert.That(error!.ErrorMessage, Does.Contain("role"));
        }

        [Test]
        public async Task ValidateEntryAgainstRuleAsync_ExpirationRule_MissingExpiration_ShouldFail()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                Address = "TESTADDRESS",
                AssetId = 12345,
                ExpirationDate = null
            };

            var rule = new WhitelistRule
            {
                RuleType = WhitelistRuleType.ExpirationRequired,
                Name = "Expiration Required",
                Configuration = new WhitelistRuleConfiguration
                {
                    ExpirationMandatory = true
                }
            };

            // Act
            var error = await _service.ValidateEntryAgainstRuleAsync(entry, rule);

            // Assert
            Assert.That(error, Is.Not.Null);
            Assert.That(error!.ErrorMessage, Does.Contain("Expiration"));
        }

        [Test]
        public async Task ValidateEntryAgainstRuleAsync_NetworkRule_DifferentNetwork_ShouldNotApply()
        {
            // Arrange
            var entry = new WhitelistEntry
            {
                Address = "TESTADDRESS",
                AssetId = 12345,
                Network = "voimain-v1.0"
            };

            var rule = new WhitelistRule
            {
                RuleType = WhitelistRuleType.NetworkSpecific,
                Name = "Aramid Only",
                Network = "aramidmain-v1.0",
                Configuration = new WhitelistRuleConfiguration
                {
                    NetworkRequirement = "aramidmain-v1.0"
                }
            };

            // Act
            var error = await _service.ValidateEntryAgainstRuleAsync(entry, rule);

            // Assert - Rule should not apply to different network
            Assert.That(error, Is.Null);
        }

        #endregion

        #region ApplyRule Tests

        [Test]
        public async Task ApplyRuleAsync_ValidRule_ShouldValidateExistingEntries()
        {
            // Arrange
            var rule = new WhitelistRule
            {
                Id = "rule-123",
                AssetId = 12345,
                RuleType = WhitelistRuleType.KycRequired,
                IsEnabled = true,
                Configuration = new WhitelistRuleConfiguration { KycMandatory = true }
            };

            var entries = new List<WhitelistEntry>
            {
                new WhitelistEntry { Address = "ADDR1", AssetId = 12345, KycVerified = true },
                new WhitelistEntry { Address = "ADDR2", AssetId = 12345, KycVerified = false }
            };

            _rulesRepositoryMock.Setup(r => r.GetRuleAsync("rule-123")).ReturnsAsync(rule);
            _whitelistRepositoryMock.Setup(r => r.GetEntriesByAssetIdAsync(12345, null)).ReturnsAsync(entries);
            _rulesRepositoryMock.Setup(r => r.AddAuditLogAsync(It.IsAny<WhitelistRuleAuditLog>()))
                .ReturnsAsync((WhitelistRuleAuditLog log) => log);

            var request = new ApplyWhitelistRuleRequest
            {
                RuleId = "rule-123",
                ApplyToExisting = true
            };

            // Act
            var result = await _service.ApplyRuleAsync(request, "TESTUSER");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.EntriesEvaluated, Is.EqualTo(2));
            Assert.That(result.EntriesPassed, Is.EqualTo(1));
            Assert.That(result.EntriesFailed, Is.EqualTo(1));
        }

        #endregion

        #region ListRules Tests

        [Test]
        public async Task ListRulesAsync_WithPagination_ShouldReturnPagedResults()
        {
            // Arrange
            var rules = Enumerable.Range(1, 25).Select(i => new WhitelistRule
            {
                AssetId = 12345,
                Name = $"Rule {i}",
                RuleType = WhitelistRuleType.KycRequired,
                Priority = i
            }).ToList();

            _rulesRepositoryMock.Setup(r => r.GetRulesForAssetAsync(12345, null, null, null))
                .ReturnsAsync(rules);

            var request = new ListWhitelistRulesRequest
            {
                AssetId = 12345,
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await _service.ListRulesAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rules.Count, Is.EqualTo(10));
            Assert.That(result.TotalCount, Is.EqualTo(25));
            Assert.That(result.TotalPages, Is.EqualTo(3));
        }

        #endregion
    }
}
