using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class CapabilityMatrixServiceTests
    {
        private Mock<ILogger<CapabilityMatrixService>> _loggerMock;
        private Mock<IOptions<CapabilityMatrixConfig>> _configMock;
        private CapabilityMatrixService _service;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<CapabilityMatrixService>>();
            _configMock = new Mock<IOptions<CapabilityMatrixConfig>>();
            
            // Setup default configuration
            var config = new CapabilityMatrixConfig
            {
                ConfigFilePath = "compliance-capabilities.json",
                Version = "2026-02-05",
                StrictMode = true,
                EnableCaching = true,
                CacheDurationSeconds = 3600
            };
            
            _configMock.Setup(c => c.Value).Returns(config);
        }

        [Test]
        public async Task GetCapabilityMatrixAsync_NoFilters_ShouldReturnCompleteMatrix()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);

            // Act
            var result = await _service.GetCapabilityMatrixAsync();

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.Jurisdictions, Is.Not.Empty);
            Assert.That(result.Data.Version, Is.Not.Null.Or.Empty);
            Assert.That(result.ErrorMessage, Is.Null);
        }

        [Test]
        public async Task GetCapabilityMatrixAsync_FilterByJurisdiction_ShouldReturnFilteredData()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new GetCapabilityMatrixRequest
            {
                Jurisdiction = "CH"
            };

            // Act
            var result = await _service.GetCapabilityMatrixAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.Jurisdictions, Has.Count.EqualTo(1));
            Assert.That(result.Data.Jurisdictions[0].Code, Is.EqualTo("CH"));
        }

        [Test]
        public async Task GetCapabilityMatrixAsync_FilterByMultipleCriteria_ShouldReturnFilteredData()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new GetCapabilityMatrixRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                KycTier = "2"
            };

            // Act
            var result = await _service.GetCapabilityMatrixAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.Jurisdictions, Has.Count.EqualTo(1));
            Assert.That(result.Data.Jurisdictions[0].Code, Is.EqualTo("CH"));
            Assert.That(result.Data.Jurisdictions[0].WalletTypes, Has.Count.EqualTo(1));
            Assert.That(result.Data.Jurisdictions[0].WalletTypes[0].Type, Is.EqualTo("custodial"));
            Assert.That(result.Data.Jurisdictions[0].WalletTypes[0].KycTiers, Has.Count.EqualTo(1));
            Assert.That(result.Data.Jurisdictions[0].WalletTypes[0].KycTiers[0].Tier, Is.EqualTo("2"));
        }

        [Test]
        public async Task GetCapabilityMatrixAsync_InvalidJurisdiction_ShouldReturnNoMatchingCapabilities()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new GetCapabilityMatrixRequest
            {
                Jurisdiction = "ZZ"
            };

            // Act
            var result = await _service.GetCapabilityMatrixAsync(request);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("No matching capabilities"));
            Assert.That(result.ErrorDetails, Is.Not.Null);
            Assert.That(result.ErrorDetails!.Error, Is.EqualTo("no_matching_capabilities"));
            Assert.That(result.ErrorDetails.Jurisdiction, Is.EqualTo("ZZ"));
        }

        [Test]
        public async Task CheckCapabilityAsync_AllowedAction_ShouldReturnAllowed()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "mint"
            };

            // Act
            var result = await _service.CheckCapabilityAsync(request);

            // Assert
            Assert.That(result.Allowed, Is.True);
            Assert.That(result.RequiredChecks, Is.Not.Null);
            Assert.That(result.RequiredChecks, Contains.Item("sanctions"));
            Assert.That(result.RequiredChecks, Contains.Item("accreditation"));
        }

        [Test]
        public async Task CheckCapabilityAsync_DisallowedAction_ShouldReturnNotAllowed()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "freeze" // freeze is not allowed for tier 2
            };

            // Act
            var result = await _service.CheckCapabilityAsync(request);

            // Assert
            Assert.That(result.Allowed, Is.False);
            Assert.That(result.Reason, Does.Contain("not allowed"));
            Assert.That(result.ErrorDetails, Is.Not.Null);
            Assert.That(result.ErrorDetails!.Error, Is.EqualTo("capability_not_allowed"));
            Assert.That(result.ErrorDetails.RuleId, Is.EqualTo("action_not_allowed"));
        }

        [Test]
        public async Task CheckCapabilityAsync_InvalidJurisdiction_ShouldReturnNotAllowed()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "ZZ",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "mint"
            };

            // Act
            var result = await _service.CheckCapabilityAsync(request);

            // Assert
            Assert.That(result.Allowed, Is.False);
            Assert.That(result.Reason, Does.Contain("not found"));
            Assert.That(result.ErrorDetails, Is.Not.Null);
            Assert.That(result.ErrorDetails!.RuleId, Is.EqualTo("jurisdiction_not_found"));
        }

        [Test]
        public async Task CheckCapabilityAsync_InvalidWalletType_ShouldReturnNotAllowed()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "invalid-wallet",
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "mint"
            };

            // Act
            var result = await _service.CheckCapabilityAsync(request);

            // Assert
            Assert.That(result.Allowed, Is.False);
            Assert.That(result.Reason, Does.Contain("not supported"));
            Assert.That(result.ErrorDetails, Is.Not.Null);
            Assert.That(result.ErrorDetails!.RuleId, Is.EqualTo("wallet_type_not_supported"));
        }

        [Test]
        public async Task CheckCapabilityAsync_InvalidKycTier_ShouldReturnNotAllowed()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "99",
                Action = "mint"
            };

            // Act
            var result = await _service.CheckCapabilityAsync(request);

            // Assert
            Assert.That(result.Allowed, Is.False);
            Assert.That(result.Reason, Does.Contain("not supported"));
            Assert.That(result.ErrorDetails, Is.Not.Null);
            Assert.That(result.ErrorDetails!.RuleId, Is.EqualTo("kyc_tier_not_supported"));
        }

        [Test]
        public async Task CheckCapabilityAsync_InvalidTokenStandard_ShouldReturnNotAllowed()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "INVALID-STANDARD",
                KycTier = "2",
                Action = "mint"
            };

            // Act
            var result = await _service.CheckCapabilityAsync(request);

            // Assert
            Assert.That(result.Allowed, Is.False);
            Assert.That(result.Reason, Does.Contain("not supported"));
            Assert.That(result.ErrorDetails, Is.Not.Null);
            Assert.That(result.ErrorDetails!.RuleId, Is.EqualTo("token_standard_not_supported"));
        }

        [Test]
        public async Task CheckCapabilityAsync_USJurisdictionWithSECChecks_ShouldReturnRequiredChecks()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "US",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "mint"
            };

            // Act
            var result = await _service.CheckCapabilityAsync(request);

            // Assert
            Assert.That(result.Allowed, Is.True);
            Assert.That(result.RequiredChecks, Is.Not.Null);
            Assert.That(result.RequiredChecks, Contains.Item("sanctions"));
            Assert.That(result.RequiredChecks, Contains.Item("accreditation"));
            Assert.That(result.RequiredChecks, Contains.Item("sec_regulation_d"));
        }

        [Test]
        public async Task CheckCapabilityAsync_EUJurisdictionWithMiCAChecks_ShouldReturnRequiredChecks()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "EU",
                WalletType = "custodial",
                TokenStandard = "ARC-3",
                KycTier = "2",
                Action = "mint"
            };

            // Act
            var result = await _service.CheckCapabilityAsync(request);

            // Assert
            Assert.That(result.Allowed, Is.True);
            Assert.That(result.RequiredChecks, Is.Not.Null);
            Assert.That(result.RequiredChecks, Contains.Item("sanctions"));
            Assert.That(result.RequiredChecks, Contains.Item("mica_compliance"));
        }

        [Test]
        public async Task CheckCapabilityAsync_HigherKycTierMoreActions_ShouldAllowMoreActions()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            
            // Tier 2 - should not allow freeze
            var tier2Request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "2",
                Action = "freeze"
            };

            // Tier 3 - should allow freeze
            var tier3Request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                TokenStandard = "ARC-19",
                KycTier = "3",
                Action = "freeze"
            };

            // Act
            var tier2Result = await _service.CheckCapabilityAsync(tier2Request);
            var tier3Result = await _service.CheckCapabilityAsync(tier3Request);

            // Assert
            Assert.That(tier2Result.Allowed, Is.False, "Tier 2 should not allow freeze");
            Assert.That(tier3Result.Allowed, Is.True, "Tier 3 should allow freeze");
        }

        [Test]
        public async Task GetVersion_ShouldReturnConfiguredVersion()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);

            // Act
            var version = _service.GetVersion();

            // Assert
            Assert.That(version, Is.Not.Null.Or.Empty);
            Assert.That(version, Does.Contain("2026"));
        }

        [Test]
        public async Task CheckCapabilityAsync_NonCustodialWalletLimitedActions_ShouldRestrictActions()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            
            // Non-custodial wallet should not allow mint
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "CH",
                WalletType = "non-custodial",
                TokenStandard = "ARC-3",
                KycTier = "2",
                Action = "mint"
            };

            // Act
            var result = await _service.CheckCapabilityAsync(request);

            // Assert
            Assert.That(result.Allowed, Is.False);
            Assert.That(result.Reason, Does.Contain("not allowed"));
        }

        [Test]
        public async Task CheckCapabilityAsync_CaseInsensitiveMatching_ShouldWork()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "ch", // lowercase
                WalletType = "CUSTODIAL", // uppercase
                TokenStandard = "Arc-19", // mixed case
                KycTier = "2",
                Action = "MINT" // uppercase
            };

            // Act
            var result = await _service.CheckCapabilityAsync(request);

            // Assert
            Assert.That(result.Allowed, Is.True, "Case-insensitive matching should work");
        }

        [Test]
        public async Task GetCapabilityMatrixAsync_FilterByTokenStandard_ShouldReturnOnlyMatchingStandards()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new GetCapabilityMatrixRequest
            {
                Jurisdiction = "CH",
                WalletType = "custodial",
                KycTier = "2",
                TokenStandard = "ARC-3"
            };

            // Act
            var result = await _service.GetCapabilityMatrixAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            var tokenStandards = result.Data!.Jurisdictions[0].WalletTypes[0].KycTiers[0].TokenStandards;
            Assert.That(tokenStandards, Has.Count.EqualTo(1));
            Assert.That(tokenStandards[0].Standard, Is.EqualTo("ARC-3"));
        }

        [Test]
        public async Task CheckCapabilityAsync_SingaporeJurisdiction_ShouldHaveMASChecks()
        {
            // Arrange
            _service = new CapabilityMatrixService(_loggerMock.Object, _configMock.Object);
            var request = new CapabilityCheckRequest
            {
                Jurisdiction = "SG",
                WalletType = "custodial",
                TokenStandard = "ARC-200",
                KycTier = "2",
                Action = "mint"
            };

            // Act
            var result = await _service.CheckCapabilityAsync(request);

            // Assert
            Assert.That(result.Allowed, Is.True);
            Assert.That(result.RequiredChecks, Is.Not.Null);
            Assert.That(result.RequiredChecks, Contains.Item("mas_accreditation"));
        }
    }
}
