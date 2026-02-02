using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for Deployment Status Repository
    /// </summary>
    [TestFixture]
    public class DeploymentStatusRepositoryTests
    {
        private IDeploymentStatusRepository _repository = null!;
        private Mock<ILogger<DeploymentStatusRepository>> _loggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<DeploymentStatusRepository>>();
            _repository = new DeploymentStatusRepository(_loggerMock.Object);
        }

        [Test]
        public async Task CreateDeploymentAsync_ShouldCreateDeployment()
        {
            // Arrange
            var deployment = new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                TokenType = "ERC20_Mintable",
                Network = "base-mainnet",
                DeployedBy = "0x1234567890abcdef",
                TokenName = "Test Token",
                TokenSymbol = "TST",
                CurrentStatus = DeploymentStatus.Queued
            };

            // Act
            await _repository.CreateDeploymentAsync(deployment);
            var retrieved = await _repository.GetDeploymentByIdAsync(deployment.DeploymentId);

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.DeploymentId, Is.EqualTo(deployment.DeploymentId));
            Assert.That(retrieved.TokenType, Is.EqualTo("ERC20_Mintable"));
            Assert.That(retrieved.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued));
        }

        [Test]
        public void CreateDeploymentAsync_WithDuplicateId_ShouldThrowException()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();
            var deployment1 = new TokenDeployment
            {
                DeploymentId = deploymentId,
                TokenType = "ERC20_Mintable",
                Network = "base-mainnet",
                DeployedBy = "0x1234"
            };

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await _repository.CreateDeploymentAsync(deployment1));
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _repository.CreateDeploymentAsync(deployment1));
        }

        [Test]
        public async Task UpdateDeploymentAsync_ShouldUpdateExistingDeployment()
        {
            // Arrange
            var deployment = new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                TokenType = "ERC20_Mintable",
                Network = "base-mainnet",
                DeployedBy = "0x1234",
                CurrentStatus = DeploymentStatus.Queued
            };

            await _repository.CreateDeploymentAsync(deployment);

            // Act
            deployment.CurrentStatus = DeploymentStatus.Completed;
            deployment.AssetIdentifier = "0xabcdef";
            await _repository.UpdateDeploymentAsync(deployment);

            var retrieved = await _repository.GetDeploymentByIdAsync(deployment.DeploymentId);

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed));
            Assert.That(retrieved.AssetIdentifier, Is.EqualTo("0xabcdef"));
        }

        [Test]
        public async Task GetDeploymentsAsync_WithFilters_ShouldReturnMatchingDeployments()
        {
            // Arrange
            await _repository.CreateDeploymentAsync(new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                TokenType = "ERC20_Mintable",
                Network = "base-mainnet",
                DeployedBy = "0x1234",
                CurrentStatus = DeploymentStatus.Completed
            });

            await _repository.CreateDeploymentAsync(new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                TokenType = "ARC200_Mintable",
                Network = "voimain-v1.0",
                DeployedBy = "0x5678",
                CurrentStatus = DeploymentStatus.Failed
            });

            await _repository.CreateDeploymentAsync(new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                TokenType = "ERC20_Mintable",
                Network = "base-mainnet",
                DeployedBy = "0x1234",
                CurrentStatus = DeploymentStatus.Queued
            });

            // Act - Filter by network
            var request1 = new ListDeploymentsRequest { Network = "base-mainnet" };
            var result1 = await _repository.GetDeploymentsAsync(request1);

            // Assert
            Assert.That(result1.Count, Is.EqualTo(2));
            Assert.That(result1.All(d => d.Network == "base-mainnet"), Is.True);

            // Act - Filter by status
            var request2 = new ListDeploymentsRequest { Status = DeploymentStatus.Failed };
            var result2 = await _repository.GetDeploymentsAsync(request2);

            // Assert
            Assert.That(result2.Count, Is.EqualTo(1));
            Assert.That(result2[0].CurrentStatus, Is.EqualTo(DeploymentStatus.Failed));
        }

        [Test]
        public async Task AddStatusEntryAsync_ShouldAddEntryToHistory()
        {
            // Arrange
            var deployment = new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                TokenType = "ERC20_Mintable",
                Network = "base-mainnet",
                DeployedBy = "0x1234",
                CurrentStatus = DeploymentStatus.Queued
            };

            await _repository.CreateDeploymentAsync(deployment);

            var statusEntry = new DeploymentStatusEntry
            {
                Status = DeploymentStatus.Submitted,
                Message = "Transaction submitted",
                TransactionHash = "0xtxhash"
            };

            // Act
            await _repository.AddStatusEntryAsync(deployment.DeploymentId, statusEntry);
            var history = await _repository.GetStatusHistoryAsync(deployment.DeploymentId);

            // Assert
            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(history[0].Status, Is.EqualTo(DeploymentStatus.Submitted));
            Assert.That(history[0].Message, Is.EqualTo("Transaction submitted"));
            Assert.That(history[0].TransactionHash, Is.EqualTo("0xtxhash"));
        }

        [Test]
        public async Task GetStatusHistoryAsync_ShouldReturnChronologicalOrder()
        {
            // Arrange
            var deployment = new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                TokenType = "ERC20_Mintable",
                Network = "base-mainnet",
                DeployedBy = "0x1234"
            };

            await _repository.CreateDeploymentAsync(deployment);

            // Add multiple status entries
            await _repository.AddStatusEntryAsync(deployment.DeploymentId, new DeploymentStatusEntry
            {
                Status = DeploymentStatus.Queued,
                Timestamp = DateTime.UtcNow.AddMinutes(-3)
            });

            await _repository.AddStatusEntryAsync(deployment.DeploymentId, new DeploymentStatusEntry
            {
                Status = DeploymentStatus.Submitted,
                Timestamp = DateTime.UtcNow.AddMinutes(-2)
            });

            await _repository.AddStatusEntryAsync(deployment.DeploymentId, new DeploymentStatusEntry
            {
                Status = DeploymentStatus.Confirmed,
                Timestamp = DateTime.UtcNow.AddMinutes(-1)
            });

            // Act
            var history = await _repository.GetStatusHistoryAsync(deployment.DeploymentId);

            // Assert
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history[0].Status, Is.EqualTo(DeploymentStatus.Queued));
            Assert.That(history[1].Status, Is.EqualTo(DeploymentStatus.Submitted));
            Assert.That(history[2].Status, Is.EqualTo(DeploymentStatus.Confirmed));
        }

        [Test]
        public async Task GetDeploymentsCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                await _repository.CreateDeploymentAsync(new TokenDeployment
                {
                    DeploymentId = Guid.NewGuid().ToString(),
                    TokenType = "ERC20_Mintable",
                    Network = "base-mainnet",
                    DeployedBy = "0x1234"
                });
            }

            // Act
            var count = await _repository.GetDeploymentsCountAsync(new ListDeploymentsRequest());

            // Assert
            Assert.That(count, Is.EqualTo(5));
        }

        [Test]
        public async Task GetDeploymentsAsync_WithPagination_ShouldReturnPagedResults()
        {
            // Arrange
            for (int i = 0; i < 10; i++)
            {
                await _repository.CreateDeploymentAsync(new TokenDeployment
                {
                    DeploymentId = Guid.NewGuid().ToString(),
                    TokenType = "ERC20_Mintable",
                    Network = "base-mainnet",
                    DeployedBy = "0x1234"
                });
            }

            // Act
            var page1 = await _repository.GetDeploymentsAsync(new ListDeploymentsRequest
            {
                Page = 1,
                PageSize = 5
            });

            var page2 = await _repository.GetDeploymentsAsync(new ListDeploymentsRequest
            {
                Page = 2,
                PageSize = 5
            });

            // Assert
            Assert.That(page1.Count, Is.EqualTo(5));
            Assert.That(page2.Count, Is.EqualTo(5));
            Assert.That(page1[0].DeploymentId, Is.Not.EqualTo(page2[0].DeploymentId));
        }
    }
}
