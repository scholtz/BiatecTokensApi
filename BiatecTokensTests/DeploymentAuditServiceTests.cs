using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for Deployment Audit Service
    /// </summary>
    [TestFixture]
    public class DeploymentAuditServiceTests
    {
        private DeploymentAuditService _service = null!;
        private Mock<IDeploymentStatusRepository> _repositoryMock = null!;
        private Mock<ILogger<DeploymentAuditService>> _loggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IDeploymentStatusRepository>();
            _loggerMock = new Mock<ILogger<DeploymentAuditService>>();

            _service = new DeploymentAuditService(
                _repositoryMock.Object,
                _loggerMock.Object);
        }

        [Test]
        public async Task ExportAuditTrailAsJsonAsync_ShouldReturnJsonString()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();
            var deployment = CreateTestDeployment(deploymentId);
            var history = CreateTestHistory(deploymentId);

            _repositoryMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId))
                .ReturnsAsync(deployment);
            _repositoryMock.Setup(r => r.GetStatusHistoryAsync(deploymentId))
                .ReturnsAsync(history);

            // Act
            var json = await _service.ExportAuditTrailAsJsonAsync(deploymentId);

            // Assert
            Assert.That(json, Is.Not.Null);
            Assert.That(json, Does.Contain("deploymentId"));
            Assert.That(json, Does.Contain(deploymentId));
            Assert.That(json, Does.Contain("statusHistory"));
        }

        [Test]
        public async Task ExportAuditTrailAsCsvAsync_ShouldReturnCsvString()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();
            var deployment = CreateTestDeployment(deploymentId);
            var history = CreateTestHistory(deploymentId);

            _repositoryMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId))
                .ReturnsAsync(deployment);
            _repositoryMock.Setup(r => r.GetStatusHistoryAsync(deploymentId))
                .ReturnsAsync(history);

            // Act
            var csv = await _service.ExportAuditTrailAsCsvAsync(deploymentId);

            // Assert
            Assert.That(csv, Is.Not.Null);
            Assert.That(csv, Does.Contain("DeploymentId"));
            Assert.That(csv, Does.Contain("Status"));
            Assert.That(csv, Does.Contain("Timestamp"));
            Assert.That(csv, Does.Contain(deploymentId));
        }

        [Test]
        public void ExportAuditTrailAsJsonAsync_WithNonExistentDeployment_ShouldThrowException()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();
            _repositoryMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId))
                .ReturnsAsync((TokenDeployment?)null);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _service.ExportAuditTrailAsJsonAsync(deploymentId));
        }

        [Test]
        public async Task ExportAuditTrailsAsync_WithValidRequest_ShouldReturnResult()
        {
            // Arrange
            var request = new AuditExportRequest
            {
                Format = AuditExportFormat.Json,
                Page = 1,
                PageSize = 10
            };

            var deployments = new List<TokenDeployment>
            {
                CreateTestDeployment(Guid.NewGuid().ToString()),
                CreateTestDeployment(Guid.NewGuid().ToString())
            };

            _repositoryMock.Setup(r => r.GetDeploymentsAsync(It.IsAny<ListDeploymentsRequest>()))
                .ReturnsAsync(deployments);

            foreach (var deployment in deployments)
            {
                _repositoryMock.Setup(r => r.GetStatusHistoryAsync(deployment.DeploymentId))
                    .ReturnsAsync(CreateTestHistory(deployment.DeploymentId));
            }

            // Act
            var result = await _service.ExportAuditTrailsAsync(request);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.Not.Null);
                Assert.That(result.RecordCount, Is.EqualTo(2));
                Assert.That(result.IsCached, Is.False);
                Assert.That(result.Format, Is.EqualTo(AuditExportFormat.Json));
            });
        }

        [Test]
        public async Task ExportAuditTrailsAsync_WithIdempotencyKey_ShouldCacheResult()
        {
            // Arrange
            var request = new AuditExportRequest
            {
                Format = AuditExportFormat.Json,
                Page = 1,
                PageSize = 10
            };
            var idempotencyKey = "test-key-123";

            var deployments = new List<TokenDeployment>
            {
                CreateTestDeployment(Guid.NewGuid().ToString())
            };

            _repositoryMock.Setup(r => r.GetDeploymentsAsync(It.IsAny<ListDeploymentsRequest>()))
                .ReturnsAsync(deployments);

            _repositoryMock.Setup(r => r.GetStatusHistoryAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestHistory(deployments[0].DeploymentId));

            // Act - First call
            var result1 = await _service.ExportAuditTrailsAsync(request, idempotencyKey);
            
            // Act - Second call with same key
            var result2 = await _service.ExportAuditTrailsAsync(request, idempotencyKey);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result1.Success, Is.True);
                Assert.That(result1.IsCached, Is.False);
                
                Assert.That(result2.Success, Is.True);
                Assert.That(result2.IsCached, Is.True);
                Assert.That(result2.Data, Is.EqualTo(result1.Data));
            });

            // Verify repository was only called once (second call used cache)
            _repositoryMock.Verify(r => r.GetDeploymentsAsync(It.IsAny<ListDeploymentsRequest>()), Times.Once);
        }

        [Test]
        public async Task ExportAuditTrailsAsync_WithSameKeyDifferentRequest_ShouldReturnError()
        {
            // Arrange
            var request1 = new AuditExportRequest
            {
                Format = AuditExportFormat.Json,
                Page = 1,
                PageSize = 10,
                Network = "testnet"
            };

            var request2 = new AuditExportRequest
            {
                Format = AuditExportFormat.Json,
                Page = 1,
                PageSize = 10,
                Network = "mainnet" // Different network
            };

            var idempotencyKey = "test-key-123";

            var deployments = new List<TokenDeployment>
            {
                CreateTestDeployment(Guid.NewGuid().ToString())
            };

            _repositoryMock.Setup(r => r.GetDeploymentsAsync(It.IsAny<ListDeploymentsRequest>()))
                .ReturnsAsync(deployments);

            _repositoryMock.Setup(r => r.GetStatusHistoryAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestHistory(deployments[0].DeploymentId));

            // Act
            var result1 = await _service.ExportAuditTrailsAsync(request1, idempotencyKey);
            var result2 = await _service.ExportAuditTrailsAsync(request2, idempotencyKey);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result1.Success, Is.True);
                Assert.That(result2.Success, Is.False);
                Assert.That(result2.ErrorMessage, Does.Contain("different request parameters"));
            });
        }

        [Test]
        public async Task ExportAuditTrailsAsync_WithInvalidPageSize_ShouldReturnError()
        {
            // Arrange
            var request = new AuditExportRequest
            {
                Format = AuditExportFormat.Json,
                Page = 1,
                PageSize = 2000 // Too large
            };

            // Act
            var result = await _service.ExportAuditTrailsAsync(request);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("Page size"));
            });
        }

        [Test]
        public async Task ExportAuditTrailsAsync_AsCsv_ShouldReturnCsvData()
        {
            // Arrange
            var request = new AuditExportRequest
            {
                Format = AuditExportFormat.Csv,
                Page = 1,
                PageSize = 10
            };

            var deployments = new List<TokenDeployment>
            {
                CreateTestDeployment(Guid.NewGuid().ToString())
            };

            _repositoryMock.Setup(r => r.GetDeploymentsAsync(It.IsAny<ListDeploymentsRequest>()))
                .ReturnsAsync(deployments);

            _repositoryMock.Setup(r => r.GetStatusHistoryAsync(It.IsAny<string>()))
                .ReturnsAsync(CreateTestHistory(deployments[0].DeploymentId));

            // Act
            var result = await _service.ExportAuditTrailsAsync(request);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Format, Is.EqualTo(AuditExportFormat.Csv));
                Assert.That(result.Data, Does.Contain("DeploymentId"));
                Assert.That(result.Data, Does.Contain("Status"));
            });
        }

        private TokenDeployment CreateTestDeployment(string deploymentId)
        {
            return new TokenDeployment
            {
                DeploymentId = deploymentId,
                TokenType = "ERC20_Mintable",
                TokenName = "Test Token",
                TokenSymbol = "TST",
                Network = "testnet",
                DeployedBy = "0x1234",
                CurrentStatus = DeploymentStatus.Completed,
                AssetIdentifier = "12345",
                TransactionHash = "0xabc123",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt = DateTime.UtcNow
            };
        }

        private List<DeploymentStatusEntry> CreateTestHistory(string deploymentId)
        {
            var baseTime = DateTime.UtcNow.AddMinutes(-10);
            
            return new List<DeploymentStatusEntry>
            {
                new DeploymentStatusEntry
                {
                    DeploymentId = deploymentId,
                    Status = DeploymentStatus.Queued,
                    Timestamp = baseTime,
                    Message = "Queued for processing"
                },
                new DeploymentStatusEntry
                {
                    DeploymentId = deploymentId,
                    Status = DeploymentStatus.Submitted,
                    Timestamp = baseTime.AddMinutes(1),
                    Message = "Submitted to blockchain",
                    TransactionHash = "0xabc123"
                },
                new DeploymentStatusEntry
                {
                    DeploymentId = deploymentId,
                    Status = DeploymentStatus.Confirmed,
                    Timestamp = baseTime.AddMinutes(5),
                    Message = "Transaction confirmed",
                    ConfirmedRound = 12345
                },
                new DeploymentStatusEntry
                {
                    DeploymentId = deploymentId,
                    Status = DeploymentStatus.Completed,
                    Timestamp = baseTime.AddMinutes(10),
                    Message = "Deployment completed"
                }
            };
        }
    }
}
