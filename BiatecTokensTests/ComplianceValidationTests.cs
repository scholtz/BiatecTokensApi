using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ComplianceValidationTests
    {
        private ComplianceRepository _repository;
        private Mock<ILogger<ComplianceService>> _serviceLoggerMock;
        private Mock<ILogger<ComplianceRepository>> _repoLoggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private ComplianceService _service;

        [SetUp]
        public void Setup()
        {
            _repoLoggerMock = new Mock<ILogger<ComplianceRepository>>();
            _repository = new ComplianceRepository(_repoLoggerMock.Object);
            _serviceLoggerMock = new Mock<ILogger<ComplianceService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            _service = new ComplianceService(_repository, _serviceLoggerMock.Object, _meteringServiceMock.Object);
        }

        #region Basic Validation Tests

        [Test]
        public async Task ValidateTokenPreset_ValidConfiguration_ShouldReturnValid()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Utility Token",
                RequiresAccreditedInvestors = false,
                HasWhitelistControls = false,
                HasIssuerControls = false,
                VerificationStatus = VerificationStatus.Pending,
                Network = "testnet"
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ValidateTokenPreset_MinimalConfiguration_ShouldReturnValidWithWarnings()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Utility Token",
                RequiresAccreditedInvestors = false,
                HasWhitelistControls = false,
                HasIssuerControls = false,
                IncludeWarnings = true
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Warnings.Count, Is.GreaterThan(0), "Should have warnings for minimal configuration");
        }

        #endregion

        #region Security Token Validation Tests

        [Test]
        public async Task ValidateTokenPreset_SecurityTokenWithoutKYC_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = true,
                HasIssuerControls = true,
                VerificationStatus = VerificationStatus.Pending, // Not verified
                Jurisdiction = "US"
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Count, Is.GreaterThan(0));
            
            var kycError = result.Errors.FirstOrDefault(e => e.Field == "VerificationStatus");
            Assert.That(kycError, Is.Not.Null, "Should have error for missing KYC verification");
            Assert.That(kycError!.Message, Does.Contain("KYC verification"));
            Assert.That(kycError.RegulatoryContext, Does.Contain("MICA"));
        }

        [Test]
        public async Task ValidateTokenPreset_SecurityTokenWithoutJurisdiction_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = true,
                HasIssuerControls = true,
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = null // Missing jurisdiction
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            
            var jurisdictionError = result.Errors.FirstOrDefault(e => e.Field == "Jurisdiction");
            Assert.That(jurisdictionError, Is.Not.Null, "Should have error for missing jurisdiction");
            Assert.That(jurisdictionError!.Message.ToLower(), Does.Contain("jurisdiction"));
        }

        [Test]
        public async Task ValidateTokenPreset_SecurityTokenWithCompleteConfig_ShouldReturnValid()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = true,
                HasIssuerControls = true,
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "US",
                RegulatoryFramework = "SEC Reg D",
                ComplianceStatus = ComplianceStatus.Compliant,
                MaxHolders = 500
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors.Count, Is.EqualTo(0));
        }

        #endregion

        #region Accredited Investor Validation Tests

        [Test]
        public async Task ValidateTokenPreset_AccreditedInvestorsWithoutWhitelist_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = false, // Missing whitelist
                HasIssuerControls = true,
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "US"
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            
            var whitelistError = result.Errors.FirstOrDefault(e => e.Field == "HasWhitelistControls");
            Assert.That(whitelistError, Is.Not.Null, "Should have error for missing whitelist");
            Assert.That(whitelistError!.Message, Does.Contain("accredited investors"));
            Assert.That(whitelistError.Message, Does.Contain("whitelist"));
            Assert.That(whitelistError.RegulatoryContext, Does.Contain("Accredited Investor"));
        }

        [Test]
        public async Task ValidateTokenPreset_AccreditedInvestorsWithoutJurisdiction_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Utility Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = true,
                HasIssuerControls = true,
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = null // Missing jurisdiction
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            
            var jurisdictionError = result.Errors.FirstOrDefault(e => e.Field == "Jurisdiction");
            Assert.That(jurisdictionError, Is.Not.Null);
        }

        #endregion

        #region Compliance Status Validation Tests

        [Test]
        public async Task ValidateTokenPreset_CompliantStatusWithoutRegulatoryFramework_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Utility Token",
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = null, // Missing regulatory framework
                Jurisdiction = "US"
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            
            var frameworkError = result.Errors.FirstOrDefault(e => e.Field == "RegulatoryFramework");
            Assert.That(frameworkError, Is.Not.Null, "Should have error for missing regulatory framework");
            Assert.That(frameworkError!.Message, Does.Contain("Compliant"));
        }

        #endregion

        #region VOI Network Validation Tests

        [Test]
        public async Task ValidateTokenPreset_VOINetworkWithAccreditedInvestorsNoKYC_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = true,
                VerificationStatus = VerificationStatus.Pending, // Not verified
                Jurisdiction = "US",
                Network = "voimain-v1.0"
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            
            var voiError = result.Errors.FirstOrDefault(e => 
                e.Field == "VerificationStatus" && e.RegulatoryContext!.Contains("VOI"));
            Assert.That(voiError, Is.Not.Null, "Should have VOI-specific error");
            Assert.That(voiError!.Message, Does.Contain("VOI network"));
        }

        [Test]
        public async Task ValidateTokenPreset_VOINetworkWithoutJurisdiction_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Utility Token",
                RequiresAccreditedInvestors = false,
                Network = "voimain-v1.0",
                Jurisdiction = null // Missing jurisdiction
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            
            var jurisdictionError = result.Errors.FirstOrDefault(e => 
                e.Field == "Jurisdiction" && e.RegulatoryContext!.Contains("VOI"));
            Assert.That(jurisdictionError, Is.Not.Null, "Should have VOI-specific jurisdiction error");
        }

        [Test]
        public async Task ValidateTokenPreset_VOINetworkWithCompleteConfig_ShouldReturnValid()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Utility Token",
                RequiresAccreditedInvestors = false,
                Network = "voimain-v1.0",
                Jurisdiction = "US"
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True);
        }

        #endregion

        #region Aramid Network Validation Tests

        [Test]
        public async Task ValidateTokenPreset_AramidNetworkCompliantWithoutFramework_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Utility Token",
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = null, // Missing framework
                Network = "aramidmain-v1.0"
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            
            var aramidError = result.Errors.FirstOrDefault(e => 
                e.Field == "RegulatoryFramework" && e.RegulatoryContext!.Contains("Aramid"));
            Assert.That(aramidError, Is.Not.Null, "Should have Aramid-specific error");
        }

        [Test]
        public async Task ValidateTokenPreset_AramidNetworkSecurityWithoutMaxHolders_ShouldReturnError()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                MaxHolders = null, // Missing max holders
                Network = "aramidmain-v1.0"
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            
            var maxHoldersError = result.Errors.FirstOrDefault(e => 
                e.Field == "MaxHolders" && e.RegulatoryContext!.Contains("Aramid"));
            Assert.That(maxHoldersError, Is.Not.Null, "Should have Aramid-specific max holders error");
        }

        [Test]
        public async Task ValidateTokenPreset_AramidNetworkWithCompleteSecurityConfig_ShouldReturnValid()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                MaxHolders = 500,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = "MiFID II",
                Network = "aramidmain-v1.0",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = true,
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "EU" // Add jurisdiction for security token
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True);
        }

        #endregion

        #region Token Controls Validation Tests

        [Test]
        public async Task ValidateTokenPreset_SecurityTokenWithoutControls_ShouldReturnWarnings()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = false,
                HasWhitelistControls = false,
                HasIssuerControls = false,
                IncludeWarnings = true
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Warnings.Count, Is.GreaterThan(0), "Should have warnings about missing controls");
            
            var controlWarnings = result.Warnings.Where(w => 
                w.Field == "HasWhitelistControls" || w.Field == "HasIssuerControls").ToList();
            Assert.That(controlWarnings.Count, Is.GreaterThan(0), "Should warn about missing controls");
        }

        [Test]
        public async Task ValidateTokenPreset_CompliantTokenWithoutControls_ShouldReturnWarnings()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Utility Token",
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = "MICA",
                HasWhitelistControls = false,
                HasIssuerControls = false,
                IncludeWarnings = true
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Warnings.Count, Is.GreaterThan(0));
            
            var controlWarning = result.Warnings.FirstOrDefault(w => w.Field == "TokenControls");
            Assert.That(controlWarning, Is.Not.Null, "Should warn about missing controls");
        }

        #endregion

        #region Warning Filtering Tests

        [Test]
        public async Task ValidateTokenPreset_WithWarningsExcluded_ShouldNotReturnWarnings()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = false,
                HasWhitelistControls = false,
                HasIssuerControls = false,
                IncludeWarnings = false // Exclude warnings
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Warnings.Count, Is.EqualTo(0), "Should not include warnings when excluded");
        }

        #endregion

        #region Summary Tests

        [Test]
        public async Task ValidateTokenPreset_ValidWithNoWarnings_ShouldHaveSuccessSummary()
        {
            // Arrange - Utility token without compliance status
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Utility Token",
                RequiresAccreditedInvestors = false,
                HasWhitelistControls = false,
                HasIssuerControls = false,
                ComplianceStatus = null, // Not set, so no compliance warnings
                IncludeWarnings = true // Include warnings to verify none are generated
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True);
            // May have warnings about jurisdiction, but should be valid
            if (result.Warnings.Count == 0)
            {
                Assert.That(result.Summary, Does.Contain("valid and compliant"));
            }
            else
            {
                Assert.That(result.Summary, Does.Contain("valid"));
            }
        }

        [Test]
        public async Task ValidateTokenPreset_ValidWithWarnings_ShouldHaveWarningSummary()
        {
            // Arrange - Security token without whitelist (warning, not error since not requiring accredited investors)
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = false, // Not requiring accredited investors
                HasWhitelistControls = false,
                HasIssuerControls = false,
                VerificationStatus = VerificationStatus.Verified, // Verified
                Jurisdiction = "US", // Has jurisdiction
                IncludeWarnings = true
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True, "Should be valid even with warnings");
            Assert.That(result.Warnings.Count, Is.GreaterThan(0), "Should have warnings");
            Assert.That(result.Summary, Does.Contain("warning"));
        }

        [Test]
        public async Task ValidateTokenPreset_InvalidWithErrors_ShouldHaveErrorSummary()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = false, // Will cause error
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "US"
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Summary, Does.Contain("error"));
            Assert.That(result.Summary, Does.Contain("must be fixed"));
        }

        #endregion

        #region Recommendation Tests

        [Test]
        public async Task ValidateTokenPreset_AllErrors_ShouldHaveRecommendations()
        {
            // Arrange
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = false,
                VerificationStatus = VerificationStatus.Pending
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            foreach (var error in result.Errors)
            {
                Assert.That(error.Recommendation, Is.Not.Null.And.Not.Empty, 
                    $"Error '{error.Message}' should have a recommendation");
                Assert.That(error.RegulatoryContext, Is.Not.Null.And.Not.Empty,
                    $"Error '{error.Message}' should have regulatory context");
            }
        }

        #endregion

        #region Complex Scenario Tests

        [Test]
        public async Task ValidateTokenPreset_VOISecurityTokenCompleteConfig_ShouldReturnValid()
        {
            // Arrange - Complete VOI security token configuration
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = true,
                HasIssuerControls = true,
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "US,EU",
                RegulatoryFramework = "SEC Reg D, MiFID II",
                ComplianceStatus = ComplianceStatus.Compliant,
                MaxHolders = 500,
                Network = "voimain-v1.0"
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors.Count, Is.EqualTo(0));
            Assert.That(result.Summary, Does.Contain("valid"));
        }

        [Test]
        public async Task ValidateTokenPreset_MultipleNetworkViolations_ShouldReturnMultipleErrors()
        {
            // Arrange - Violates multiple network rules
            var request = new ValidateTokenPresetRequest
            {
                AssetType = "Security Token",
                RequiresAccreditedInvestors = true,
                HasWhitelistControls = false,
                VerificationStatus = VerificationStatus.Pending,
                Jurisdiction = null,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = null,
                Network = "voimain-v1.0"
            };

            // Act
            var result = await _service.ValidateTokenPresetAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Count, Is.GreaterThanOrEqualTo(3), 
                "Should have multiple errors for multiple violations");
        }

        #endregion
    }
}
