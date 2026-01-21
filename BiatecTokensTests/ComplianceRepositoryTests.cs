using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class ComplianceRepositoryTests
    {
        private Mock<ILogger<ComplianceRepository>> _loggerMock;
        private ComplianceRepository _repository;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<ComplianceRepository>>();
            _repository = new ComplianceRepository(_loggerMock.Object);
        }

        #region UpsertMetadataAsync Tests

        [Test]
        public async Task UpsertMetadataAsync_NewMetadata_ShouldAdd()
        {
            // Arrange
            var metadata = new ComplianceMetadata
            {
                AssetId = 12345,
                KycProvider = "Sumsub",
                ComplianceStatus = ComplianceStatus.Compliant
            };

            // Act
            var result = await _repository.UpsertMetadataAsync(metadata);

            // Assert
            Assert.That(result, Is.True);
            
            // Verify it was added
            var retrieved = await _repository.GetMetadataByAssetIdAsync(metadata.AssetId);
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.AssetId, Is.EqualTo(metadata.AssetId));
            Assert.That(retrieved.KycProvider, Is.EqualTo(metadata.KycProvider));
        }

        [Test]
        public async Task UpsertMetadataAsync_ExistingMetadata_ShouldUpdate()
        {
            // Arrange
            var metadata = new ComplianceMetadata
            {
                AssetId = 12345,
                KycProvider = "Sumsub",
                ComplianceStatus = ComplianceStatus.UnderReview,
                CreatedBy = "CREATOR1",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            await _repository.UpsertMetadataAsync(metadata);

            var updatedMetadata = new ComplianceMetadata
            {
                AssetId = 12345,
                KycProvider = "Onfido",
                ComplianceStatus = ComplianceStatus.Compliant,
                CreatedBy = "UPDATER1", // This should be preserved
                CreatedAt = DateTime.UtcNow // This should be preserved
            };

            // Act
            var result = await _repository.UpsertMetadataAsync(updatedMetadata);

            // Assert
            Assert.That(result, Is.True);
            
            var retrieved = await _repository.GetMetadataByAssetIdAsync(metadata.AssetId);
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.KycProvider, Is.EqualTo("Onfido"));
            Assert.That(retrieved.ComplianceStatus, Is.EqualTo(ComplianceStatus.Compliant));
            Assert.That(retrieved.CreatedBy, Is.EqualTo("CREATOR1")); // Preserved
            Assert.That(retrieved.UpdatedAt, Is.Not.Null);
        }

        #endregion

        #region GetMetadataByAssetIdAsync Tests

        [Test]
        public async Task GetMetadataByAssetIdAsync_ExistingMetadata_ShouldReturn()
        {
            // Arrange
            var metadata = new ComplianceMetadata
            {
                AssetId = 12345,
                KycProvider = "Sumsub"
            };
            await _repository.UpsertMetadataAsync(metadata);

            // Act
            var result = await _repository.GetMetadataByAssetIdAsync(12345);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.AssetId, Is.EqualTo(12345));
            Assert.That(result.KycProvider, Is.EqualTo("Sumsub"));
        }

        [Test]
        public async Task GetMetadataByAssetIdAsync_NonExistingMetadata_ShouldReturnNull()
        {
            // Act
            var result = await _repository.GetMetadataByAssetIdAsync(99999);

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region DeleteMetadataAsync Tests

        [Test]
        public async Task DeleteMetadataAsync_ExistingMetadata_ShouldDelete()
        {
            // Arrange
            var metadata = new ComplianceMetadata
            {
                AssetId = 12345,
                KycProvider = "Sumsub"
            };
            await _repository.UpsertMetadataAsync(metadata);

            // Act
            var result = await _repository.DeleteMetadataAsync(12345);

            // Assert
            Assert.That(result, Is.True);
            
            // Verify it was deleted
            var retrieved = await _repository.GetMetadataByAssetIdAsync(12345);
            Assert.That(retrieved, Is.Null);
        }

        [Test]
        public async Task DeleteMetadataAsync_NonExistingMetadata_ShouldReturnFalse()
        {
            // Act
            var result = await _repository.DeleteMetadataAsync(99999);

            // Assert
            Assert.That(result, Is.False);
        }

        #endregion

        #region ListMetadataAsync Tests

        [Test]
        public async Task ListMetadataAsync_NoFilters_ShouldReturnAll()
        {
            // Arrange
            await _repository.UpsertMetadataAsync(new ComplianceMetadata { AssetId = 1 });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata { AssetId = 2 });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata { AssetId = 3 });

            var request = new ListComplianceMetadataRequest
            {
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await _repository.ListMetadataAsync(request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task ListMetadataAsync_FilterByComplianceStatus_ShouldReturnFiltered()
        {
            // Arrange
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 1, 
                ComplianceStatus = ComplianceStatus.Compliant 
            });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 2, 
                ComplianceStatus = ComplianceStatus.UnderReview 
            });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 3, 
                ComplianceStatus = ComplianceStatus.Compliant 
            });

            var request = new ListComplianceMetadataRequest
            {
                ComplianceStatus = ComplianceStatus.Compliant,
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await _repository.ListMetadataAsync(request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.All(m => m.ComplianceStatus == ComplianceStatus.Compliant), Is.True);
        }

        [Test]
        public async Task ListMetadataAsync_FilterByVerificationStatus_ShouldReturnFiltered()
        {
            // Arrange
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 1, 
                VerificationStatus = VerificationStatus.Verified 
            });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 2, 
                VerificationStatus = VerificationStatus.Pending 
            });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 3, 
                VerificationStatus = VerificationStatus.Verified 
            });

            var request = new ListComplianceMetadataRequest
            {
                VerificationStatus = VerificationStatus.Verified,
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await _repository.ListMetadataAsync(request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.All(m => m.VerificationStatus == VerificationStatus.Verified), Is.True);
        }

        [Test]
        public async Task ListMetadataAsync_FilterByNetwork_ShouldReturnFiltered()
        {
            // Arrange
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 1, 
                Network = "voimain-v1.0" 
            });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 2, 
                Network = "aramidmain-v1.0" 
            });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 3, 
                Network = "voimain-v1.0" 
            });

            var request = new ListComplianceMetadataRequest
            {
                Network = "voimain-v1.0",
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await _repository.ListMetadataAsync(request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.All(m => m.Network == "voimain-v1.0"), Is.True);
        }

        [Test]
        public async Task ListMetadataAsync_WithPagination_ShouldReturnCorrectPage()
        {
            // Arrange
            for (ulong i = 1; i <= 10; i++)
            {
                await _repository.UpsertMetadataAsync(new ComplianceMetadata { AssetId = i });
            }

            var request = new ListComplianceMetadataRequest
            {
                Page = 2,
                PageSize = 3
            };

            // Act
            var result = await _repository.ListMetadataAsync(request);

            // Assert
            Assert.That(result.Count, Is.EqualTo(3)); // Page 2 should have 3 items
        }

        #endregion

        #region GetMetadataCountAsync Tests

        [Test]
        public async Task GetMetadataCountAsync_NoFilters_ShouldReturnTotalCount()
        {
            // Arrange
            await _repository.UpsertMetadataAsync(new ComplianceMetadata { AssetId = 1 });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata { AssetId = 2 });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata { AssetId = 3 });

            var request = new ListComplianceMetadataRequest();

            // Act
            var count = await _repository.GetMetadataCountAsync(request);

            // Assert
            Assert.That(count, Is.EqualTo(3));
        }

        [Test]
        public async Task GetMetadataCountAsync_WithFilters_ShouldReturnFilteredCount()
        {
            // Arrange
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 1, 
                ComplianceStatus = ComplianceStatus.Compliant 
            });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 2, 
                ComplianceStatus = ComplianceStatus.UnderReview 
            });
            await _repository.UpsertMetadataAsync(new ComplianceMetadata 
            { 
                AssetId = 3, 
                ComplianceStatus = ComplianceStatus.Compliant 
            });

            var request = new ListComplianceMetadataRequest
            {
                ComplianceStatus = ComplianceStatus.Compliant
            };

            // Act
            var count = await _repository.GetMetadataCountAsync(request);

            // Assert
            Assert.That(count, Is.EqualTo(2));
        }

        #endregion

        #region Concurrency Tests

        [Test]
        public async Task UpsertMetadataAsync_ConcurrentUpdates_ShouldHandleCorrectly()
        {
            // Arrange
            var metadata = new ComplianceMetadata
            {
                AssetId = 12345,
                KycProvider = "Initial"
            };
            await _repository.UpsertMetadataAsync(metadata);

            // Act - Concurrent updates
            var tasks = Enumerable.Range(1, 10).Select(i => 
                _repository.UpsertMetadataAsync(new ComplianceMetadata
                {
                    AssetId = 12345,
                    KycProvider = $"Provider{i}"
                })
            );

            await Task.WhenAll(tasks);

            // Assert
            var result = await _repository.GetMetadataByAssetIdAsync(12345);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.KycProvider, Does.StartWith("Provider"));
        }

        #endregion
    }
}
