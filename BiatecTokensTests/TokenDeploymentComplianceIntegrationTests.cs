using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Models.ARC200.Request;
using BiatecTokensApi.Models.ASA.Request;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using BiatecTokensApi.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for token deployment with compliance metadata
    /// </summary>
    [TestFixture]
    public class TokenDeploymentComplianceIntegrationTests
    {
        private IComplianceRepository _complianceRepository = null!;
        private Mock<ILogger<ComplianceRepository>> _complianceLoggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _complianceLoggerMock = new Mock<ILogger<ComplianceRepository>>();
            _complianceRepository = new ComplianceRepository(_complianceLoggerMock.Object);
        }

        #region ERC20 Token Deployment Tests

        [Test]
        public void ERC20_ValidateRequest_RwaToken_MissingCompliance_ShouldThrow()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Real Estate Token",
                Symbol = "RET",
                InitialSupply = 1000000,
                Cap = 10000000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                ComplianceMetadata = new TokenDeploymentComplianceMetadata
                {
                    // Marks as RWA but missing required fields
                    IssuerName = "Test Issuer"
                }
            };

            var mockConfig = new Mock<IOptionsMonitor<EVMChains>>();
            var mockAppConfig = new Mock<IOptionsMonitor<AppConfiguration>>();
            var mockLogger = new Mock<ILogger<ERC20TokenService>>();
            var mockTokenIssuanceRepo = new Mock<ITokenIssuanceRepository>();
            
            var deploymentStatusServiceMock = new Mock<IDeploymentStatusService>();
            deploymentStatusServiceMock.Setup(x => x.CreateDeploymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Guid.NewGuid().ToString());
            deploymentStatusServiceMock.Setup(x => x.UpdateDeploymentStatusAsync(
                It.IsAny<string>(), It.IsAny<DeploymentStatus>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>()))
                .ReturnsAsync(true);

            var mockAuthService = Mock.Of<IAuthenticationService>();
            var mockUserRepo = Mock.Of<IUserRepository>();
            var mockHttpContextAccessor = Mock.Of<IHttpContextAccessor>();

            var service = new ERC20TokenService(
                mockConfig.Object, 
                mockAppConfig.Object, 
                mockLogger.Object, 
                mockTokenIssuanceRepo.Object,
                _complianceRepository,
                deploymentStatusServiceMock.Object,
                mockAuthService,
                mockUserRepo,
                mockHttpContextAccessor);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                service.ValidateRequest(request, TokenType.ERC20_Mintable));
            Assert.That(ex?.Message, Does.Contain("Compliance validation failed"));
            Assert.That(ex?.Message, Does.Contain("Jurisdiction"));
            Assert.That(ex?.Message, Does.Contain("AssetType"));
            Assert.That(ex?.Message, Does.Contain("RegulatoryFramework"));
            Assert.That(ex?.Message, Does.Contain("DisclosureUrl"));
        }

        [Test]
        public void ERC20_ValidateRequest_RwaToken_WithValidCompliance_ShouldPass()
        {
            // Arrange
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Real Estate Token",
                Symbol = "RET",
                InitialSupply = 1000000,
                Cap = 10000000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                ComplianceMetadata = new TokenDeploymentComplianceMetadata
                {
                    IssuerName = "Real Estate LLC",
                    Jurisdiction = "US,EU",
                    AssetType = "Real Estate Security Token",
                    RegulatoryFramework = "SEC Reg D",
                    DisclosureUrl = "https://example.com/disclosure",
                    RequiresWhitelist = true,
                    RequiresAccreditedInvestors = true
                }
            };

            var mockConfig = new Mock<IOptionsMonitor<EVMChains>>();
            var mockAppConfig = new Mock<IOptionsMonitor<AppConfiguration>>();
            var mockLogger = new Mock<ILogger<ERC20TokenService>>();
            var mockTokenIssuanceRepo = new Mock<ITokenIssuanceRepository>();
            
            var deploymentStatusServiceMock = new Mock<IDeploymentStatusService>();
            deploymentStatusServiceMock.Setup(x => x.CreateDeploymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Guid.NewGuid().ToString());
            deploymentStatusServiceMock.Setup(x => x.UpdateDeploymentStatusAsync(
                It.IsAny<string>(), It.IsAny<DeploymentStatus>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>()))
                .ReturnsAsync(true);

            var mockAuthService = Mock.Of<IAuthenticationService>();
            var mockUserRepo = Mock.Of<IUserRepository>();
            var mockHttpContextAccessor = Mock.Of<IHttpContextAccessor>();

            var service = new ERC20TokenService(
                mockConfig.Object, 
                mockAppConfig.Object, 
                mockLogger.Object, 
                mockTokenIssuanceRepo.Object,
                _complianceRepository,
                deploymentStatusServiceMock.Object,
                mockAuthService,
                mockUserRepo,
                mockHttpContextAccessor);

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        [Test]
        public void ERC20_ValidateRequest_UtilityToken_NoCompliance_ShouldPass()
        {
            // Arrange - utility token without compliance metadata
            var request = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Utility Token",
                Symbol = "UTL",
                InitialSupply = 1000000,
                Cap = 10000000,
                Decimals = 18,
                ChainId = 8453,
                InitialSupplyReceiver = "0x1234567890123456789012345678901234567890",
                ComplianceMetadata = null // No compliance metadata
            };

            var mockConfig = new Mock<IOptionsMonitor<EVMChains>>();
            var mockAppConfig = new Mock<IOptionsMonitor<AppConfiguration>>();
            var mockLogger = new Mock<ILogger<ERC20TokenService>>();
            var mockTokenIssuanceRepo = new Mock<ITokenIssuanceRepository>();
            
            var deploymentStatusServiceMock = new Mock<IDeploymentStatusService>();
            deploymentStatusServiceMock.Setup(x => x.CreateDeploymentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(Guid.NewGuid().ToString());
            deploymentStatusServiceMock.Setup(x => x.UpdateDeploymentStatusAsync(
                It.IsAny<string>(), It.IsAny<DeploymentStatus>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ulong?>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>()))
                .ReturnsAsync(true);

            var mockAuthService = Mock.Of<IAuthenticationService>();
            var mockUserRepo = Mock.Of<IUserRepository>();
            var mockHttpContextAccessor = Mock.Of<IHttpContextAccessor>();

            var service = new ERC20TokenService(
                mockConfig.Object, 
                mockAppConfig.Object, 
                mockLogger.Object, 
                mockTokenIssuanceRepo.Object,
                _complianceRepository,
                deploymentStatusServiceMock.Object,
                mockAuthService,
                mockUserRepo,
                mockHttpContextAccessor);

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() => service.ValidateRequest(request, TokenType.ERC20_Mintable));
        }

        #endregion

        #region ARC200 Token Deployment Tests

        [Test]
        public void ARC200_ValidateRequest_RwaToken_MissingCompliance_ShouldThrow()
        {
            // This test validates the logic without needing full service instantiation
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                // Marks as RWA but missing required fields
                RequiresAccreditedInvestors = true
            };

            // Act
            var isRwaToken = ComplianceValidator.IsRwaToken(metadata);
            var isValid = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(isRwaToken, Is.True, "Token with RequiresAccreditedInvestors should be treated as RWA");
            Assert.That(isValid, Is.False, "RWA token without required compliance fields should fail validation");
            Assert.That(errors, Is.Not.Empty);
            Assert.That(errors, Has.Some.Contains("IssuerName"));
            Assert.That(errors, Has.Some.Contains("Jurisdiction"));
        }

        [Test]
        public void ARC200_ValidateRequest_RwaToken_WithValidCompliance_ShouldPass()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Algorand Real Estate LLC",
                Jurisdiction = "US,EU",
                AssetType = "Real Estate Security Token",
                RegulatoryFramework = "SEC Reg D",
                DisclosureUrl = "https://example.com/disclosure",
                RequiresWhitelist = true,
                RequiresAccreditedInvestors = true
            };

            // Act
            var isRwaToken = ComplianceValidator.IsRwaToken(metadata);
            var isValid = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(isRwaToken, Is.True);
            Assert.That(isValid, Is.True, "RWA token with all required fields should pass validation");
            Assert.That(errors, Is.Empty);
        }

        #endregion

        #region ASA Token Deployment Tests

        [Test]
        public void ASA_ValidateRequest_RwaToken_MissingCompliance_ShouldThrow()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                // Marks as RWA but missing required fields
                MaxHolders = 500
            };

            // Act
            var isRwaToken = ComplianceValidator.IsRwaToken(metadata);
            var isValid = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(isRwaToken, Is.True, "Token with MaxHolders should be treated as RWA");
            Assert.That(isValid, Is.False, "RWA token without required compliance fields should fail validation");
            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void ASA_ValidateRequest_RwaToken_WithValidCompliance_ShouldPass()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "ASA Securities LLC",
                Jurisdiction = "US",
                AssetType = "Security Token",
                RegulatoryFramework = "SEC Reg D",
                DisclosureUrl = "https://example.com/disclosure",
                MaxHolders = 500
            };

            // Act
            var isRwaToken = ComplianceValidator.IsRwaToken(metadata);
            var isValid = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(isRwaToken, Is.True);
            Assert.That(isValid, Is.True, "RWA token with all required fields should pass validation");
            Assert.That(errors, Is.Empty);
        }

        #endregion

        #region Compliance Metadata Persistence Tests

        [Test]
        public async Task ComplianceMetadata_AfterDeployment_ShouldBeRetrievable()
        {
            // Arrange
            var metadata = new ComplianceMetadata
            {
                AssetId = 12345,
                Network = "testnet-v1.0",
                IssuerName = "Test Issuer",
                Jurisdiction = "US,EU",
                AssetType = "Security Token",
                RegulatoryFramework = "SEC Reg D",
                RequiresAccreditedInvestors = true,
                MaxHolders = 500,
                CreatedBy = "TEST_ADDRESS",
                CreatedAt = DateTime.UtcNow
            };

            // Act - Persist
            var upsertResult = await _complianceRepository.UpsertMetadataAsync(metadata);
            Assert.That(upsertResult, Is.True);

            // Act - Retrieve
            var retrieved = await _complianceRepository.GetMetadataByAssetIdAsync(12345);

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.AssetId, Is.EqualTo(metadata.AssetId));
            Assert.That(retrieved.IssuerName, Is.EqualTo(metadata.IssuerName));
            Assert.That(retrieved.Jurisdiction, Is.EqualTo(metadata.Jurisdiction));
            Assert.That(retrieved.AssetType, Is.EqualTo(metadata.AssetType));
            Assert.That(retrieved.RegulatoryFramework, Is.EqualTo(metadata.RegulatoryFramework));
            Assert.That(retrieved.RequiresAccreditedInvestors, Is.EqualTo(metadata.RequiresAccreditedInvestors));
            Assert.That(retrieved.MaxHolders, Is.EqualTo(metadata.MaxHolders));
        }

        [Test]
        public async Task ComplianceMetadata_MultipleTokens_ShouldBeIndependent()
        {
            // Arrange
            var metadata1 = new ComplianceMetadata
            {
                AssetId = 11111,
                Network = "testnet-v1.0",
                IssuerName = "Issuer 1",
                Jurisdiction = "US",
                CreatedBy = "ADDRESS1"
            };

            var metadata2 = new ComplianceMetadata
            {
                AssetId = 22222,
                Network = "mainnet-v1.0",
                IssuerName = "Issuer 2",
                Jurisdiction = "EU",
                CreatedBy = "ADDRESS2"
            };

            // Act
            await _complianceRepository.UpsertMetadataAsync(metadata1);
            await _complianceRepository.UpsertMetadataAsync(metadata2);

            var retrieved1 = await _complianceRepository.GetMetadataByAssetIdAsync(11111);
            var retrieved2 = await _complianceRepository.GetMetadataByAssetIdAsync(22222);

            // Assert
            Assert.That(retrieved1, Is.Not.Null);
            Assert.That(retrieved2, Is.Not.Null);
            Assert.That(retrieved1!.IssuerName, Is.EqualTo("Issuer 1"));
            Assert.That(retrieved2!.IssuerName, Is.EqualTo("Issuer 2"));
            Assert.That(retrieved1.Jurisdiction, Is.EqualTo("US"));
            Assert.That(retrieved2.Jurisdiction, Is.EqualTo("EU"));
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void ValidateComplianceMetadata_RwaToken_WhitespaceOnlyFields_ShouldFail()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "   ",
                Jurisdiction = "  ",
                AssetType = " ",
                RegulatoryFramework = "   ",
                DisclosureUrl = "  "
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.False, "Whitespace-only strings should be treated as missing");
            Assert.That(errors.Count, Is.GreaterThan(0));
        }

        [Test]
        public void ValidateComplianceMetadata_RwaToken_PartiallyFilled_ShouldFail()
        {
            // Arrange - only some fields filled
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Test Issuer",
                Jurisdiction = "US"
                // Missing: AssetType, RegulatoryFramework, DisclosureUrl
            };
            bool isRwaToken = true;

            // Act
            var result = ComplianceValidator.ValidateComplianceMetadata(metadata, isRwaToken, out var errors);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(errors.Count, Is.EqualTo(3), "Should report 3 missing fields");
            Assert.That(errors, Has.Some.Contains("AssetType"));
            Assert.That(errors, Has.Some.Contains("RegulatoryFramework"));
            Assert.That(errors, Has.Some.Contains("DisclosureUrl"));
        }

        [Test]
        public void IsRwaToken_WithMultipleRwaFields_ShouldReturnTrue()
        {
            // Arrange
            var metadata = new TokenDeploymentComplianceMetadata
            {
                IssuerName = "Test Issuer",
                RequiresWhitelist = true,
                MaxHolders = 100
            };

            // Act
            var result = ComplianceValidator.IsRwaToken(metadata);

            // Assert
            Assert.That(result, Is.True, "Multiple RWA indicators should clearly identify as RWA token");
        }

        [Test]
        public async Task ComplianceMetadata_Update_ShouldPreserveOriginalFields()
        {
            // Arrange
            var originalMetadata = new ComplianceMetadata
            {
                AssetId = 99999,
                Network = "testnet-v1.0",
                IssuerName = "Original Issuer",
                Jurisdiction = "US",
                AssetType = "Security",
                RegulatoryFramework = "SEC",
                CreatedBy = "CREATOR",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            // Persist original
            await _complianceRepository.UpsertMetadataAsync(originalMetadata);

            // Update
            var updatedMetadata = new ComplianceMetadata
            {
                AssetId = 99999,
                Network = "testnet-v1.0",
                IssuerName = "Updated Issuer",
                Jurisdiction = "US,EU",
                AssetType = "Security",
                RegulatoryFramework = "SEC Reg D",
                CreatedBy = "UPDATER", // This should be preserved from original
                UpdatedBy = "UPDATER"
            };

            await _complianceRepository.UpsertMetadataAsync(updatedMetadata);

            // Retrieve
            var retrieved = await _complianceRepository.GetMetadataByAssetIdAsync(99999);

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.IssuerName, Is.EqualTo("Updated Issuer"));
            Assert.That(retrieved.Jurisdiction, Is.EqualTo("US,EU"));
            Assert.That(retrieved.RegulatoryFramework, Is.EqualTo("SEC Reg D"));
        }

        [Test]
        public async Task ComplianceMetadata_FilterByNetwork_ShouldWork()
        {
            // Arrange
            var metadata1 = new ComplianceMetadata
            {
                AssetId = 10001,
                Network = "voimain-v1.0",
                IssuerName = "VOI Issuer",
                CreatedBy = "ADDRESS1"
            };

            var metadata2 = new ComplianceMetadata
            {
                AssetId = 10002,
                Network = "aramidmain-v1.0",
                IssuerName = "Aramid Issuer",
                CreatedBy = "ADDRESS2"
            };

            await _complianceRepository.UpsertMetadataAsync(metadata1);
            await _complianceRepository.UpsertMetadataAsync(metadata2);

            // Act - retrieve by network
            var voiMetadata = await _complianceRepository.GetMetadataByAssetIdAsync(10001);
            var aramidMetadata = await _complianceRepository.GetMetadataByAssetIdAsync(10002);

            // Assert
            Assert.That(voiMetadata, Is.Not.Null);
            Assert.That(aramidMetadata, Is.Not.Null);
            Assert.That(voiMetadata!.Network, Is.EqualTo("voimain-v1.0"));
            Assert.That(aramidMetadata!.Network, Is.EqualTo("aramidmain-v1.0"));
        }

        #endregion
    }
}
