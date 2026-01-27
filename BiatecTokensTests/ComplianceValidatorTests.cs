using BiatecTokensApi.Models;
using BiatecTokensApi.Services;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for ComplianceValidator
    /// </summary>
    [TestFixture]
    public class ComplianceValidatorTests
    {
        [Test]
        public void ValidateComplianceMetadata_UtilityToken_NoMetadata_ShouldPass()
        {
            // Arrange
            TokenDeploymentComplianceMetadata? metadata = null;
            bool isRwaToken = false;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.True, "Utility tokens should not require compliance metadata");
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void ValidateComplianceMetadata_UtilityToken_WithMetadata_ShouldPass()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Optional Issuer",
                Notes = "Optional notes"
            };
            bool isRwaToken = false;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.True, "Utility tokens with optional metadata should pass");
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_NoMetadata_ShouldFail()
        {
            // Arrange
            TokenDeploymentComplianceMetadata? metadata = null;
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.False, "RWA tokens require compliance metadata");
            Assert.That(errors, Is.Not.Empty);
            Assert.That(errors[0], Does.Contain("required for RWA tokens"));
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_MissingIssuerName_ShouldFail()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                // IssuerName missing
                Jurisdiction = "US,EU",
                AssetType = "Security Token",
                RegulatoryFramework = "SEC Reg D",
                DisclosureUrl = "https://example.com/disclosure"
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(errors, Has.Some.Contains("IssuerName"));
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_MissingJurisdiction_ShouldFail()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Test Issuer",
                // Jurisdiction missing
                AssetType = "Security Token",
                RegulatoryFramework = "SEC Reg D",
                DisclosureUrl = "https://example.com/disclosure"
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(errors, Has.Some.Contains("Jurisdiction"));
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_MissingAssetType_ShouldFail()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Test Issuer",
                Jurisdiction = "US,EU",
                // AssetType missing
                RegulatoryFramework = "SEC Reg D",
                DisclosureUrl = "https://example.com/disclosure"
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(errors, Has.Some.Contains("AssetType"));
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_MissingRegulatoryFramework_ShouldFail()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Test Issuer",
                Jurisdiction = "US,EU",
                AssetType = "Security Token",
                // RegulatoryFramework missing
                DisclosureUrl = "https://example.com/disclosure"
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(errors, Has.Some.Contains("RegulatoryFramework"));
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_MissingDisclosureUrl_ShouldFail()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Test Issuer",
                Jurisdiction = "US,EU",
                AssetType = "Security Token",
                RegulatoryFramework = "SEC Reg D"
                // DisclosureUrl missing
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(errors, Has.Some.Contains("DisclosureUrl"));
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_AllRequiredFields_ShouldPass()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Test Real Estate LLC",
                Jurisdiction = "US,EU,GB",
                AssetType = "Real Estate Security Token",
                RegulatoryFramework = "SEC Reg D, MiFID II",
                DisclosureUrl = "https://example.com/disclosure/token123",
                RequiresWhitelist = true,
                RequiresAccreditedInvestors = true,
                MaxHolders = 500,
                KycProvider = "Sumsub"
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.True, "RWA token with all required fields should pass");
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_MultipleErrors_ShouldListAll()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                // All required fields missing
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(errors.Count, Is.EqualTo(5), "Should report all 5 missing required fields");
            Assert.That(errors, Has.Some.Contains("IssuerName"));
            Assert.That(errors, Has.Some.Contains("Jurisdiction"));
            Assert.That(errors, Has.Some.Contains("AssetType"));
            Assert.That(errors, Has.Some.Contains("RegulatoryFramework"));
            Assert.That(errors, Has.Some.Contains("DisclosureUrl"));
        }

        [Test]
        public void IsRwaToken_NoMetadata_ShouldReturnFalse()
        {
            // Arrange
            TokenDeploymentComplianceMetadata? metadata = null;

            // Act
            var result = ComplianceValidator.IsRwaToken(metadata);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsRwaToken_EmptyMetadata_ShouldReturnFalse()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata();

            // Act
            var result = ComplianceValidator.IsRwaToken(metadata);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsRwaToken_WithIssuerName_ShouldReturnTrue()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Test Issuer"
            };

            // Act
            var result = ComplianceValidator.IsRwaToken(metadata);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsRwaToken_WithRegulatoryFramework_ShouldReturnTrue()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                RegulatoryFramework = "SEC Reg D"
            };

            // Act
            var result = ComplianceValidator.IsRwaToken(metadata);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsRwaToken_WithAccreditedInvestorsFlag_ShouldReturnTrue()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                RequiresAccreditedInvestors = true
            };

            // Act
            var result = ComplianceValidator.IsRwaToken(metadata);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsRwaToken_WithWhitelistFlag_ShouldReturnTrue()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                RequiresWhitelist = true
            };

            // Act
            var result = ComplianceValidator.IsRwaToken(metadata);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsRwaToken_WithMaxHolders_ShouldReturnTrue()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                MaxHolders = 500
            };

            // Act
            var result = ComplianceValidator.IsRwaToken(metadata);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsRwaToken_OnlyOptionalFields_ShouldReturnFalse()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                Notes = "Just some notes",
                KycProvider = "SomeProvider"
            };

            // Act
            var result = ComplianceValidator.IsRwaToken(metadata);

            // Assert
            Assert.That(result, Is.False, "Optional fields alone should not classify as RWA");
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_EmptyStrings_ShouldFail()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "",
                Jurisdiction = "",
                AssetType = "",
                RegulatoryFramework = "",
                DisclosureUrl = ""
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.False, "Empty strings should be treated as missing fields");
            Assert.That(errors.Count, Is.EqualTo(5), "All 5 required fields should be reported as missing");
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_ValidUrls_ShouldPass()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Test Issuer",
                Jurisdiction = "US,EU,GB",
                AssetType = "Real Estate Token",
                RegulatoryFramework = "SEC Reg D, MiFID II, MICA",
                DisclosureUrl = "https://example.com/disclosure.pdf"
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.True, "Valid URLs should pass validation");
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_WithOptionalFields_ShouldPass()
        {
            // Arrange - RWA with all required + optional fields
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Complete Issuer LLC",
                Jurisdiction = "US",
                AssetType = "Security Token",
                RegulatoryFramework = "SEC Reg D",
                DisclosureUrl = "https://example.com/disclosure",
                RequiresWhitelist = true,
                RequiresAccreditedInvestors = true,
                MaxHolders = 500,
                TransferRestrictions = "Lock-up period of 12 months",
                KycProvider = "Sumsub",
                Notes = "Additional compliance notes"
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.True, "RWA with optional fields should pass");
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void IsRwaToken_BoundaryConditions_MaxHoldersZero_ShouldReturnTrue()
        {
            // Arrange - MaxHolders set to 0 should still be treated as RWA indicator
            var metadata = new TokenDeploymentComplianceMetadata
            {
                MaxHolders = 0
            };

            // Act
            var result = ComplianceValidator.IsRwaToken(metadata);

            // Assert
            Assert.That(result, Is.True, "MaxHolders with value 0 should still indicate RWA token");
        }

        [Test]
        public void ValidateComplianceMetadata_UtilityToken_WithAllFields_ShouldPass()
        {
            // Arrange - Utility token with full compliance metadata (optional)
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Utility Issuer",
                Jurisdiction = "GLOBAL",
                AssetType = "Utility Token",
                RegulatoryFramework = "N/A",
                DisclosureUrl = "https://example.com/terms",
                Notes = "Utility token for platform access"
            };
            bool isRwaToken = false; // Explicitly utility

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.True, "Utility tokens can have optional compliance metadata");
            Assert.That(errors, Is.Empty);
        }
    }
}
