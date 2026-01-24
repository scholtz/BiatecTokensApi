using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    [TestFixture]
    [Category("Integration")]
    public class WhitelistRulesIntegrationTests
    {
        private WhitelistRulesRepository _rulesRepository = null!;
        private WhitelistRepository _whitelistRepository = null!;
        private WhitelistRulesService _service = null!;
        private Mock<ILogger<WhitelistRulesService>> _loggerMock = null!;
        private Mock<ILogger<WhitelistRepository>> _whitelistLoggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _rulesRepository = new WhitelistRulesRepository();
            _whitelistLoggerMock = new Mock<ILogger<WhitelistRepository>>();
            _whitelistRepository = new WhitelistRepository(_whitelistLoggerMock.Object);
            _loggerMock = new Mock<ILogger<WhitelistRulesService>>();
            
            _service = new WhitelistRulesService(
                _rulesRepository,
                _whitelistRepository,
                _loggerMock.Object);
        }

        #region VOI Network Tests

        [Test]
        public async Task VOINetwork_KycRule_ShouldEnforceForVOIEntries()
        {
            // Arrange - Create KYC rule for VOI network
            var createRuleRequest = new CreateWhitelistRuleRequest
            {
                AssetId = 12345,
                Name = "VOI KYC Required",
                Description = "KYC verification required for VOI network entries",
                RuleType = WhitelistRuleType.KycRequired,
                Network = "voimain-v1.0",
                Priority = 100,
                Configuration = new WhitelistRuleConfiguration
                {
                    KycMandatory = true,
                    ApprovedKycProviders = new List<string> { "Sumsub", "Onfido" },
                    ValidationMessage = "KYC verification is mandatory for VOI network"
                }
            };

            var ruleResult = await _service.CreateRuleAsync(createRuleRequest, "TESTCREATOR");
            Assert.That(ruleResult.Success, Is.True);

            // Add whitelist entries for VOI network
            var entryWithKyc = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "VOIADDRESSWITHKYC12345678901234567890123456789012345678",
                Network = "voimain-v1.0",
                KycVerified = true,
                KycProvider = "Sumsub",
                Status = WhitelistStatus.Active
            };

            var entryWithoutKyc = new WhitelistEntry
            {
                AssetId = 12345,
                Address = "VOIADDRESSNOKC123456789012345678901234567890123456789",
                Network = "voimain-v1.0",
                KycVerified = false,
                Status = WhitelistStatus.Active
            };

            await _whitelistRepository.AddEntryAsync(entryWithKyc);
            await _whitelistRepository.AddEntryAsync(entryWithoutKyc);

            // Act - Apply rule to validate entries
            var applyRequest = new ApplyWhitelistRuleRequest
            {
                RuleId = ruleResult.Rule!.Id,
                ApplyToExisting = true,
                FailOnError = false
            };

            var applyResult = await _service.ApplyRuleAsync(applyRequest, "TESTUSER");

            // Assert
            Assert.That(applyResult.Success, Is.True);
            Assert.That(applyResult.EntriesEvaluated, Is.EqualTo(2));
            Assert.That(applyResult.EntriesPassed, Is.EqualTo(1), "Entry with KYC should pass");
            Assert.That(applyResult.EntriesFailed, Is.EqualTo(1), "Entry without KYC should fail");
        }

        #endregion

        #region Aramid Network Tests

        [Test]
        public async Task AramidNetwork_KycRule_ShouldEnforceStrictKyc()
        {
            // Arrange - Create strict KYC rule for Aramid network
            var createRuleRequest = new CreateWhitelistRuleRequest
            {
                AssetId = 54321,
                Name = "Aramid Strict KYC",
                Description = "Aramid network requires verified KYC with approved providers",
                RuleType = WhitelistRuleType.KycRequired,
                Network = "aramidmain-v1.0",
                Priority = 200,
                Configuration = new WhitelistRuleConfiguration
                {
                    KycMandatory = true,
                    ApprovedKycProviders = new List<string> { "Sumsub" }, // Only Sumsub approved
                    ValidationMessage = "Aramid network requires Sumsub KYC verification"
                }
            };

            var ruleResult = await _service.CreateRuleAsync(createRuleRequest, "TESTCREATOR");
            Assert.That(ruleResult.Success, Is.True);

            // Add entries with different KYC providers
            var entryApprovedProvider = new WhitelistEntry
            {
                AssetId = 54321,
                Address = "ARAMIDADDRESS1234567890123456789012345678901234567890",
                Network = "aramidmain-v1.0",
                KycVerified = true,
                KycProvider = "Sumsub", // Approved
                Status = WhitelistStatus.Active
            };

            var entryUnapprovedProvider = new WhitelistEntry
            {
                AssetId = 54321,
                Address = "ARAMIDADDRESS2567890123456789012345678901234567890123",
                Network = "aramidmain-v1.0",
                KycVerified = true,
                KycProvider = "Onfido", // Not approved for Aramid
                Status = WhitelistStatus.Active
            };

            await _whitelistRepository.AddEntryAsync(entryApprovedProvider);
            await _whitelistRepository.AddEntryAsync(entryUnapprovedProvider);

            // Act
            var applyRequest = new ApplyWhitelistRuleRequest
            {
                RuleId = ruleResult.Rule!.Id,
                ApplyToExisting = true
            };

            var applyResult = await _service.ApplyRuleAsync(applyRequest, "TESTUSER");

            // Assert
            Assert.That(applyResult.EntriesPassed, Is.EqualTo(1), "Only Sumsub verified entry should pass");
            Assert.That(applyResult.EntriesFailed, Is.EqualTo(1), "Onfido entry should fail for Aramid");
        }

        [Test]
        public async Task AramidNetwork_CompositeRule_ShouldEnforceMultipleRequirements()
        {
            // Arrange - Create KYC rule
            var kycRuleRequest = new CreateWhitelistRuleRequest
            {
                AssetId = 99999,
                Name = "Aramid KYC",
                RuleType = WhitelistRuleType.KycRequired,
                Network = "aramidmain-v1.0",
                Configuration = new WhitelistRuleConfiguration
                {
                    KycMandatory = true
                }
            };
            var kycRuleResult = await _service.CreateRuleAsync(kycRuleRequest, "TESTCREATOR");

            // Create Role rule
            var roleRuleRequest = new CreateWhitelistRuleRequest
            {
                AssetId = 99999,
                Name = "Aramid Admin Role",
                RuleType = WhitelistRuleType.RoleBasedAccess,
                Network = "aramidmain-v1.0",
                Configuration = new WhitelistRuleConfiguration
                {
                    MinimumRole = WhitelistRole.Admin
                }
            };
            var roleRuleResult = await _service.CreateRuleAsync(roleRuleRequest, "TESTCREATOR");

            // Create Composite rule
            var compositeRuleRequest = new CreateWhitelistRuleRequest
            {
                AssetId = 99999,
                Name = "Aramid Complete Compliance",
                RuleType = WhitelistRuleType.Composite,
                Network = "aramidmain-v1.0",
                Priority = 300,
                Configuration = new WhitelistRuleConfiguration
                {
                    CompositeRuleIds = new List<string> 
                    { 
                        kycRuleResult.Rule!.Id, 
                        roleRuleResult.Rule!.Id 
                    },
                    ValidationMessage = "Aramid requires KYC and Admin role"
                }
            };
            var compositeResult = await _service.CreateRuleAsync(compositeRuleRequest, "TESTCREATOR");

            // Add entry that meets all requirements
            var compliantEntry = new WhitelistEntry
            {
                AssetId = 99999,
                Address = "COMPLIANTADDR123456789012345678901234567890123456789",
                Network = "aramidmain-v1.0",
                KycVerified = true,
                KycProvider = "Sumsub",
                Role = WhitelistRole.Admin,
                Status = WhitelistStatus.Active
            };

            // Add entry that only has KYC but not admin role
            var partialEntry = new WhitelistEntry
            {
                AssetId = 99999,
                Address = "PARTIALADDR1234567890123456789012345678901234567890",
                Network = "aramidmain-v1.0",
                KycVerified = true,
                KycProvider = "Sumsub",
                Role = WhitelistRole.Operator, // Not admin
                Status = WhitelistStatus.Active
            };

            await _whitelistRepository.AddEntryAsync(compliantEntry);
            await _whitelistRepository.AddEntryAsync(partialEntry);

            // Act
            var applyRequest = new ApplyWhitelistRuleRequest
            {
                RuleId = compositeResult.Rule!.Id,
                ApplyToExisting = true
            };
            var applyResult = await _service.ApplyRuleAsync(applyRequest, "TESTUSER");

            // Assert
            Assert.That(applyResult.EntriesPassed, Is.EqualTo(1), "Only fully compliant entry should pass");
            Assert.That(applyResult.EntriesFailed, Is.EqualTo(1), "Partial entry should fail composite rule");
        }

        #endregion

        #region Cross-Network Tests

        [Test]
        public async Task GlobalRule_ShouldApplyToAllNetworks()
        {
            // Arrange - Create global KYC rule (no network specified)
            var globalRuleRequest = new CreateWhitelistRuleRequest
            {
                AssetId = 77777,
                Name = "Global KYC Rule",
                RuleType = WhitelistRuleType.KycRequired,
                Network = null, // Applies to all networks
                Configuration = new WhitelistRuleConfiguration
                {
                    KycMandatory = true
                }
            };
            var globalRuleResult = await _service.CreateRuleAsync(globalRuleRequest, "TESTCREATOR");

            // Add entries from different networks
            var voiEntry = new WhitelistEntry
            {
                AssetId = 77777,
                Address = "VOIADDRESS12345678901234567890123456789012345678901",
                Network = "voimain-v1.0",
                KycVerified = true,
                Status = WhitelistStatus.Active
            };

            var aramidEntry = new WhitelistEntry
            {
                AssetId = 77777,
                Address = "ARAMIDADDRESS678901234567890123456789012345678901234",
                Network = "aramidmain-v1.0",
                KycVerified = false,
                Status = WhitelistStatus.Active
            };

            await _whitelistRepository.AddEntryAsync(voiEntry);
            await _whitelistRepository.AddEntryAsync(aramidEntry);

            // Act
            var applyRequest = new ApplyWhitelistRuleRequest
            {
                RuleId = globalRuleResult.Rule!.Id,
                ApplyToExisting = true
            };
            var applyResult = await _service.ApplyRuleAsync(applyRequest, "TESTUSER");

            // Assert
            Assert.That(applyResult.EntriesEvaluated, Is.EqualTo(2));
            Assert.That(applyResult.EntriesPassed, Is.EqualTo(1), "VOI entry with KYC should pass");
            Assert.That(applyResult.EntriesFailed, Is.EqualTo(1), "Aramid entry without KYC should fail");
        }

        #endregion

        #region Audit Trail Tests

        [Test]
        public async Task RuleOperations_ShouldCreateCompleteAuditTrail()
        {
            // Arrange & Act - Create rule
            var createRequest = new CreateWhitelistRuleRequest
            {
                AssetId = 11111,
                Name = "Test Rule for Audit",
                RuleType = WhitelistRuleType.KycRequired,
                Configuration = new WhitelistRuleConfiguration { KycMandatory = true }
            };
            var createResult = await _service.CreateRuleAsync(createRequest, "CREATOR");
            var ruleId = createResult.Rule!.Id;

            // Update rule
            var updateRequest = new UpdateWhitelistRuleRequest
            {
                RuleId = ruleId,
                Name = "Updated Rule Name"
            };
            await _service.UpdateRuleAsync(updateRequest, "UPDATER");

            // Apply rule
            var applyRequest = new ApplyWhitelistRuleRequest { RuleId = ruleId };
            await _service.ApplyRuleAsync(applyRequest, "APPLIER");

            // Delete rule
            await _service.DeleteRuleAsync(ruleId, "DELETER");

            // Assert - Check audit log
            var auditLog = await _rulesRepository.GetAuditLogAsync(ruleId);
            
            Assert.That(auditLog.Count, Is.EqualTo(4), "Should have 4 audit entries");
            Assert.That(auditLog.Any(log => log.ActionType == "Create"), Is.True);
            Assert.That(auditLog.Any(log => log.ActionType == "Update"), Is.True);
            Assert.That(auditLog.Any(log => log.ActionType == "Apply"), Is.True);
            Assert.That(auditLog.Any(log => log.ActionType == "Delete"), Is.True);
        }

        #endregion
    }
}
