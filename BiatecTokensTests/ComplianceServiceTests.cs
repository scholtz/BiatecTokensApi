using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ComplianceServiceTests
    {
        private Mock<IComplianceRepository> _repositoryMock;
        private Mock<ILogger<ComplianceService>> _loggerMock;
        private ComplianceService _service;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IComplianceRepository>();
            _loggerMock = new Mock<ILogger<ComplianceService>>();
            _service = new ComplianceService(_repositoryMock.Object, _loggerMock.Object);
        }

        #region UpsertMetadataAsync Tests

        [Test]
        public async Task UpsertMetadataAsync_ValidRequest_ShouldSucceed()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                KycProvider = "Sumsub",
                KycVerificationDate = DateTime.UtcNow,
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "US,EU",
                RegulatoryFramework = "SEC Reg D",
                ComplianceStatus = ComplianceStatus.Compliant
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.AssetId))
                .ReturnsAsync((ComplianceMetadata?)null);
            _repositoryMock.Setup(r => r.UpsertMetadataAsync(It.IsAny<ComplianceMetadata>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpsertMetadataAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata!.AssetId, Is.EqualTo(request.AssetId));
            Assert.That(result.Metadata.CreatedBy, Is.EqualTo(createdBy));
            Assert.That(result.Metadata.KycProvider, Is.EqualTo(request.KycProvider));
        }

        [Test]
        public async Task UpsertMetadataAsync_UpdateExisting_ShouldPreserveCreationInfo()
        {
            // Arrange
            var originalCreatedBy = "CREATOR1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
            var originalCreatedAt = DateTime.UtcNow.AddDays(-10);
            var existingMetadata = new ComplianceMetadata
            {
                AssetId = 12345,
                CreatedBy = originalCreatedBy,
                CreatedAt = originalCreatedAt,
                ComplianceStatus = ComplianceStatus.UnderReview
            };

            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = "SEC Reg D"
            };
            var updatedBy = "UPDATER1AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(request.AssetId))
                .ReturnsAsync(existingMetadata);
            _repositoryMock.Setup(r => r.UpsertMetadataAsync(It.IsAny<ComplianceMetadata>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpsertMetadataAsync(request, updatedBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metadata!.CreatedBy, Is.EqualTo(originalCreatedBy));
            Assert.That(result.Metadata.CreatedAt, Is.EqualTo(originalCreatedAt));
            Assert.That(result.Metadata.UpdatedBy, Is.EqualTo(updatedBy));
            Assert.That(result.Metadata.UpdatedAt, Is.Not.Null);
        }

        #endregion

        #region GetMetadataAsync Tests

        [Test]
        public async Task GetMetadataAsync_ExistingMetadata_ShouldReturnSuccess()
        {
            // Arrange
            var assetId = 12345ul;
            var metadata = new ComplianceMetadata
            {
                AssetId = assetId,
                ComplianceStatus = ComplianceStatus.Compliant
            };

            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync(metadata);

            // Act
            var result = await _service.GetMetadataAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata!.AssetId, Is.EqualTo(assetId));
        }

        [Test]
        public async Task GetMetadataAsync_NonExistingMetadata_ShouldReturnFailure()
        {
            // Arrange
            var assetId = 99999ul;
            _repositoryMock.Setup(r => r.GetMetadataByAssetIdAsync(assetId))
                .ReturnsAsync((ComplianceMetadata?)null);

            // Act
            var result = await _service.GetMetadataAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        #endregion

        #region DeleteMetadataAsync Tests

        [Test]
        public async Task DeleteMetadataAsync_ExistingMetadata_ShouldSucceed()
        {
            // Arrange
            var assetId = 12345ul;
            _repositoryMock.Setup(r => r.DeleteMetadataAsync(assetId))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteMetadataAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task DeleteMetadataAsync_NonExistingMetadata_ShouldReturnFailure()
        {
            // Arrange
            var assetId = 99999ul;
            _repositoryMock.Setup(r => r.DeleteMetadataAsync(assetId))
                .ReturnsAsync(false);

            // Act
            var result = await _service.DeleteMetadataAsync(assetId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        #endregion

        #region ListMetadataAsync Tests

        [Test]
        public async Task ListMetadataAsync_WithoutFilters_ShouldReturnAllMetadata()
        {
            // Arrange
            var request = new ListComplianceMetadataRequest
            {
                Page = 1,
                PageSize = 20
            };

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata { AssetId = 1 },
                new ComplianceMetadata { AssetId = 2 },
                new ComplianceMetadata { AssetId = 3 }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(request))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(request))
                .ReturnsAsync(metadataList.Count);

            // Act
            var result = await _service.ListMetadataAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Metadata.Count, Is.EqualTo(3));
            Assert.That(result.TotalCount, Is.EqualTo(3));
            Assert.That(result.TotalPages, Is.EqualTo(1));
        }

        [Test]
        public async Task ListMetadataAsync_WithPagination_ShouldCalculateCorrectPages()
        {
            // Arrange
            var request = new ListComplianceMetadataRequest
            {
                Page = 1,
                PageSize = 2
            };

            var metadataList = new List<ComplianceMetadata>
            {
                new ComplianceMetadata { AssetId = 1 },
                new ComplianceMetadata { AssetId = 2 }
            };

            _repositoryMock.Setup(r => r.ListMetadataAsync(request))
                .ReturnsAsync(metadataList);
            _repositoryMock.Setup(r => r.GetMetadataCountAsync(request))
                .ReturnsAsync(5); // Total 5 items

            // Act
            var result = await _service.ListMetadataAsync(request);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(5));
            Assert.That(result.TotalPages, Is.EqualTo(3)); // 5 items / 2 per page = 3 pages
        }

        #endregion

        #region Network Validation Tests - VOI Network

        [Test]
        public void ValidateNetworkRules_VoiWithAccreditedInvestorsNotVerified_ShouldReturnError()
        {
            // Arrange
            var network = "voimain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                RequiresAccreditedInvestors = true,
                VerificationStatus = VerificationStatus.Pending,
                Jurisdiction = "US"
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Does.Contain("VOI").And.Contains("KYC verification"));
        }

        [Test]
        public void ValidateNetworkRules_VoiWithVerifiedAccreditedInvestors_ShouldPass()
        {
            // Arrange
            var network = "voimain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                RequiresAccreditedInvestors = true,
                VerificationStatus = VerificationStatus.Verified,
                Jurisdiction = "US"
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ValidateNetworkRules_VoiWithoutJurisdiction_ShouldReturnError()
        {
            // Arrange
            var network = "voimain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                Jurisdiction = null
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Does.Contain("VOI").And.Contains("jurisdiction"));
        }

        [Test]
        public void ValidateNetworkRules_VoiWithJurisdiction_ShouldPass()
        {
            // Arrange
            var network = "voimain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                Jurisdiction = "US",
                VerificationStatus = VerificationStatus.Pending,
                RequiresAccreditedInvestors = false
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        #endregion

        #region Network Validation Tests - Aramid Network

        [Test]
        public void ValidateNetworkRules_AramidCompliantWithoutFramework_ShouldReturnError()
        {
            // Arrange
            var network = "aramidmain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = null
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Does.Contain("Aramid").And.Contains("RegulatoryFramework"));
        }

        [Test]
        public void ValidateNetworkRules_AramidCompliantWithFramework_ShouldPass()
        {
            // Arrange
            var network = "aramidmain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = "MiFID II"
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ValidateNetworkRules_AramidSecurityTokenWithoutMaxHolders_ShouldReturnError()
        {
            // Arrange
            var network = "aramidmain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                AssetType = "Security Token",
                MaxHolders = null
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Not.Null);
            Assert.That(error, Does.Contain("Aramid").And.Contains("MaxHolders"));
        }

        [Test]
        public void ValidateNetworkRules_AramidSecurityTokenWithMaxHolders_ShouldPass()
        {
            // Arrange
            var network = "aramidmain-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network,
                AssetType = "Security Token",
                MaxHolders = 500,
                ComplianceStatus = ComplianceStatus.UnderReview
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        #endregion

        #region Network Validation Tests - General

        [Test]
        public void ValidateNetworkRules_NoNetworkSpecified_ShouldPass()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = null
            };

            // Act
            var error = _service.ValidateNetworkRules(null, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ValidateNetworkRules_EmptyNetwork_ShouldPass()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = ""
            };

            // Act
            var error = _service.ValidateNetworkRules("", request);

            // Assert
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ValidateNetworkRules_OtherNetwork_ShouldPass()
        {
            // Arrange
            var network = "mainnet-v1.0";
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = network
            };

            // Act
            var error = _service.ValidateNetworkRules(network, request);

            // Assert
            Assert.That(error, Is.Null);
        }

        #endregion

        #region Network Validation with Upsert

        [Test]
        public async Task UpsertMetadataAsync_VoiNetworkViolation_ShouldReturnError()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                RequiresAccreditedInvestors = true,
                VerificationStatus = VerificationStatus.Pending
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Act
            var result = await _service.UpsertMetadataAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("VOI"));
        }

        [Test]
        public async Task UpsertMetadataAsync_AramidNetworkViolation_ShouldReturnError()
        {
            // Arrange
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = "aramidmain-v1.0",
                ComplianceStatus = ComplianceStatus.Compliant
                // Missing RegulatoryFramework
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Act
            var result = await _service.UpsertMetadataAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Aramid"));
        }

        #endregion
    }
}
