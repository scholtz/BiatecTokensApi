using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for Deployment Status Service
    /// </summary>
    [TestFixture]
    public class DeploymentStatusServiceTests
    {
        private IDeploymentStatusService _service = null!;
        private Mock<IDeploymentStatusRepository> _repositoryMock = null!;
        private Mock<IWebhookService> _webhookServiceMock = null!;
        private Mock<ILogger<DeploymentStatusService>> _loggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _repositoryMock = new Mock<IDeploymentStatusRepository>();
            _webhookServiceMock = new Mock<IWebhookService>();
            _loggerMock = new Mock<ILogger<DeploymentStatusService>>();

            _service = new DeploymentStatusService(
                _repositoryMock.Object,
                _webhookServiceMock.Object,
                _loggerMock.Object);
        }

        [Test]
        public async Task CreateDeploymentAsync_ShouldCreateDeploymentWithQueuedStatus()
        {
            // Arrange
            string? capturedDeploymentId = null;
            _repositoryMock.Setup(r => r.CreateDeploymentAsync(It.IsAny<TokenDeployment>()))
                .Callback<TokenDeployment>(d => capturedDeploymentId = d.DeploymentId)
                .Returns(Task.CompletedTask);

            // Act
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable",
                "base-mainnet",
                "0x1234",
                "Test Token",
                "TST");

            // Assert
            Assert.That(deploymentId, Is.Not.Null);
            Assert.That(deploymentId, Is.EqualTo(capturedDeploymentId));
            _repositoryMock.Verify(r => r.CreateDeploymentAsync(
                It.Is<TokenDeployment>(d =>
                    d.CurrentStatus == DeploymentStatus.Queued &&
                    d.TokenType == "ERC20_Mintable" &&
                    d.Network == "base-mainnet")), Times.Once);
        }

        [Test]
        public async Task UpdateDeploymentStatusAsync_WithValidTransition_ShouldSucceed()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();
            var deployment = new TokenDeployment
            {
                DeploymentId = deploymentId,
                CurrentStatus = DeploymentStatus.Queued
            };

            _repositoryMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId))
                .ReturnsAsync(deployment);

            _repositoryMock.Setup(r => r.AddStatusEntryAsync(deploymentId, It.IsAny<DeploymentStatusEntry>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "Transaction submitted");

            // Assert
            Assert.That(result, Is.True);
            _repositoryMock.Verify(r => r.AddStatusEntryAsync(
                deploymentId,
                It.Is<DeploymentStatusEntry>(e => e.Status == DeploymentStatus.Submitted)), Times.Once);
        }

        [Test]
        public async Task UpdateDeploymentStatusAsync_WithInvalidTransition_ShouldFail()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();
            var deployment = new TokenDeployment
            {
                DeploymentId = deploymentId,
                CurrentStatus = DeploymentStatus.Queued
            };

            _repositoryMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId))
                .ReturnsAsync(deployment);

            // Act - Try to go from Queued directly to Completed (invalid)
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Completed);

            // Assert
            Assert.That(result, Is.False);
            _repositoryMock.Verify(r => r.AddStatusEntryAsync(
                It.IsAny<string>(),
                It.IsAny<DeploymentStatusEntry>()), Times.Never);
        }

        [Test]
        public async Task UpdateDeploymentStatusAsync_WithSameStatus_ShouldSucceed()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();
            var deployment = new TokenDeployment
            {
                DeploymentId = deploymentId,
                CurrentStatus = DeploymentStatus.Submitted
            };

            _repositoryMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId))
                .ReturnsAsync(deployment);

            // Act - Idempotency check
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task UpdateDeploymentStatusAsync_WithNonExistentDeployment_ShouldFail()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();
            _repositoryMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId))
                .ReturnsAsync((TokenDeployment?)null);

            // Act
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted);

            // Assert
            Assert.That(result, Is.False);
        }

        [TestCase(DeploymentStatus.Queued, DeploymentStatus.Submitted, true)]
        [TestCase(DeploymentStatus.Queued, DeploymentStatus.Failed, true)]
        [TestCase(DeploymentStatus.Submitted, DeploymentStatus.Pending, true)]
        [TestCase(DeploymentStatus.Pending, DeploymentStatus.Confirmed, true)]
        [TestCase(DeploymentStatus.Confirmed, DeploymentStatus.Completed, true)]
        [TestCase(DeploymentStatus.Failed, DeploymentStatus.Queued, true)] // Retry
        [TestCase(DeploymentStatus.Completed, DeploymentStatus.Failed, false)] // Invalid
        [TestCase(DeploymentStatus.Queued, DeploymentStatus.Completed, false)] // Invalid
        [TestCase(DeploymentStatus.Submitted, DeploymentStatus.Confirmed, false)] // Skip state
        public void IsValidStatusTransition_ShouldValidateCorrectly(
            DeploymentStatus current, DeploymentStatus next, bool expectedResult)
        {
            // Act
            var result = _service.IsValidStatusTransition(current, next);

            // Assert
            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [Test]
        public async Task MarkDeploymentFailedAsync_ShouldSetFailedStatus()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();
            var deployment = new TokenDeployment
            {
                DeploymentId = deploymentId,
                CurrentStatus = DeploymentStatus.Pending
            };

            _repositoryMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId))
                .ReturnsAsync(deployment);

            _repositoryMock.Setup(r => r.AddStatusEntryAsync(deploymentId, It.IsAny<DeploymentStatusEntry>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.MarkDeploymentFailedAsync(
                deploymentId,
                "Network timeout",
                isRetryable: true);

            // Assert
            _repositoryMock.Verify(r => r.AddStatusEntryAsync(
                deploymentId,
                It.Is<DeploymentStatusEntry>(e =>
                    e.Status == DeploymentStatus.Failed &&
                    e.ErrorMessage == "Network timeout")), Times.Once);
        }

        [Test]
        public async Task UpdateAssetIdentifierAsync_ShouldUpdateDeployment()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();
            var deployment = new TokenDeployment
            {
                DeploymentId = deploymentId,
                CurrentStatus = DeploymentStatus.Confirmed
            };

            _repositoryMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId))
                .ReturnsAsync(deployment);

            _repositoryMock.Setup(r => r.UpdateDeploymentAsync(It.IsAny<TokenDeployment>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpdateAssetIdentifierAsync(deploymentId, "0xabcdef123456");

            // Assert
            _repositoryMock.Verify(r => r.UpdateDeploymentAsync(
                It.Is<TokenDeployment>(d => d.AssetIdentifier == "0xabcdef123456")), Times.Once);
        }

        [Test]
        public async Task GetDeploymentsAsync_ShouldReturnPaginatedResults()
        {
            // Arrange
            var deployments = new List<TokenDeployment>
            {
                new() { DeploymentId = "1", TokenType = "ERC20" },
                new() { DeploymentId = "2", TokenType = "ERC20" }
            };

            _repositoryMock.Setup(r => r.GetDeploymentsAsync(It.IsAny<ListDeploymentsRequest>()))
                .ReturnsAsync(deployments);

            _repositoryMock.Setup(r => r.GetDeploymentsCountAsync(It.IsAny<ListDeploymentsRequest>()))
                .ReturnsAsync(10);

            var request = new ListDeploymentsRequest
            {
                Page = 1,
                PageSize = 5
            };

            // Act
            var response = await _service.GetDeploymentsAsync(request);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Deployments.Count, Is.EqualTo(2));
            Assert.That(response.TotalCount, Is.EqualTo(10));
            Assert.That(response.TotalPages, Is.EqualTo(2));
        }

        [Test]
        public async Task CreateDeploymentAsync_ShouldTriggerWebhook()
        {
            // Arrange
            _repositoryMock.Setup(r => r.CreateDeploymentAsync(It.IsAny<TokenDeployment>()))
                .Returns(Task.CompletedTask);

            _webhookServiceMock.Setup(w => w.EmitEventAsync(It.IsAny<WebhookEvent>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.CreateDeploymentAsync(
                "ERC20_Mintable",
                "base-mainnet",
                "0x1234",
                "Test Token",
                "TST");

            // Assert
            _webhookServiceMock.Verify(w => w.EmitEventAsync(
                It.Is<WebhookEvent>(e => e.EventType == WebhookEventType.TokenDeploymentStarted)), Times.Once);
        }

        [Test]
        public async Task UpdateDeploymentStatusAsync_ToCompleted_ShouldTriggerCompletedWebhook()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();
            var deployment = new TokenDeployment
            {
                DeploymentId = deploymentId,
                CurrentStatus = DeploymentStatus.Confirmed,
                Network = "base-mainnet",
                DeployedBy = "0x1234"
            };

            _repositoryMock.Setup(r => r.GetDeploymentByIdAsync(deploymentId))
                .ReturnsAsync(deployment);

            _repositoryMock.Setup(r => r.AddStatusEntryAsync(deploymentId, It.IsAny<DeploymentStatusEntry>()))
                .Returns(Task.CompletedTask);

            _webhookServiceMock.Setup(w => w.EmitEventAsync(It.IsAny<WebhookEvent>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Completed);

            // Assert
            _webhookServiceMock.Verify(w => w.EmitEventAsync(
                It.Is<WebhookEvent>(e => e.EventType == WebhookEventType.TokenDeploymentCompleted)), Times.Once);
        }
    }
}
