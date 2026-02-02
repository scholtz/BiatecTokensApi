using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for deployment status pipeline from creation to completion
    /// </summary>
    [TestFixture]
    public class DeploymentStatusIntegrationTests
    {
        private IDeploymentStatusService _service = null!;
        private IDeploymentStatusRepository _repository = null!;
        private Mock<IWebhookService> _webhookServiceMock = null!;

        [SetUp]
        public void Setup()
        {
            var repositoryLogger = new Mock<ILogger<DeploymentStatusRepository>>();
            _repository = new DeploymentStatusRepository(repositoryLogger.Object);

            _webhookServiceMock = new Mock<IWebhookService>();
            var serviceLogger = new Mock<ILogger<DeploymentStatusService>>();

            _service = new DeploymentStatusService(
                _repository,
                _webhookServiceMock.Object,
                serviceLogger.Object);
        }

        [Test]
        public async Task CompleteDeploymentFlow_FromQueuedToCompleted_ShouldSucceed()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable",
                "base-mainnet",
                "0x1234567890abcdef",
                "Test Token",
                "TST");

            // Act & Assert - Transition through all states
            
            // Queued -> Submitted
            var result1 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "Transaction submitted to blockchain",
                transactionHash: "0xtxhash123");
            Assert.That(result1, Is.True);

            // Submitted -> Pending (implicit - typically blockchain does this)
            var result2 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Pending,
                "Transaction pending confirmation");
            Assert.That(result2, Is.True);

            // Pending -> Confirmed
            var result3 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Confirmed,
                "Transaction confirmed",
                confirmedRound: 12345);
            Assert.That(result3, Is.True);

            // Update asset identifier
            await _service.UpdateAssetIdentifierAsync(deploymentId, "0xcontractaddress");

            // Confirmed -> Completed
            var result4 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Completed,
                "Deployment completed successfully");
            Assert.That(result4, Is.True);

            // Verify final state
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment, Is.Not.Null);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed));
            Assert.That(deployment.AssetIdentifier, Is.EqualTo("0xcontractaddress"));
            Assert.That(deployment.TransactionHash, Is.EqualTo("0xtxhash123"));

            // Verify history has all transitions
            var history = await _service.GetStatusHistoryAsync(deploymentId);
            Assert.That(history.Count, Is.EqualTo(5)); // Queued, Submitted, Pending, Confirmed, Completed
            Assert.That(history[0].Status, Is.EqualTo(DeploymentStatus.Queued));
            Assert.That(history[4].Status, Is.EqualTo(DeploymentStatus.Completed));

            // Verify webhooks were triggered
            _webhookServiceMock.Verify(w => w.EmitEventAsync(It.IsAny<WebhookEvent>()), Times.AtLeast(5));
        }

        [Test]
        public async Task FailedDeployment_ShouldTrackFailureCorrectly()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable",
                "base-mainnet",
                "0x1234",
                "Failed Token",
                "FAIL");

            // Act - Simulate failure during submission
            await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "Transaction submitted");

            await _service.MarkDeploymentFailedAsync(
                deploymentId,
                "Insufficient gas",
                isRetryable: true);

            // Assert
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment, Is.Not.Null);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Failed));
            Assert.That(deployment.ErrorMessage, Is.EqualTo("Insufficient gas"));

            var history = await _service.GetStatusHistoryAsync(deploymentId);
            Assert.That(history.Count, Is.EqualTo(3)); // Queued, Submitted, Failed
            Assert.That(history[2].Status, Is.EqualTo(DeploymentStatus.Failed));
            Assert.That(history[2].Metadata, Is.Not.Null);
            Assert.That(history[2].Metadata!.ContainsKey("isRetryable"), Is.True);
        }

        [Test]
        public async Task RetryFailedDeployment_ShouldAllowQueuedTransition()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable",
                "base-mainnet",
                "0x1234",
                "Retry Token",
                "RETRY");

            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted);
            await _service.MarkDeploymentFailedAsync(deploymentId, "Network timeout", isRetryable: true);

            // Act - Retry from failed state
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Queued,
                "Retrying deployment");

            // Assert
            Assert.That(result, Is.True);
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued));
        }

        [Test]
        public async Task IdempotentStatusUpdates_ShouldNotCreateDuplicateEntries()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable",
                "base-mainnet",
                "0x1234",
                "Idempotent Token",
                "IDEM");

            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted);

            // Act - Try to update to same status multiple times
            var result1 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "Duplicate update 1");

            var result2 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "Duplicate update 2");

            // Assert - All should succeed but no new entries should be created
            Assert.That(result1, Is.True);
            Assert.That(result2, Is.True);

            var history = await _service.GetStatusHistoryAsync(deploymentId);
            // Should only have Queued and one Submitted entry
            Assert.That(history.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task ConcurrentDeployments_ShouldBeIndependent()
        {
            // Arrange & Act - Create multiple deployments
            var deploymentId1 = await _service.CreateDeploymentAsync(
                "ERC20_Mintable", "base-mainnet", "0x1111", "Token 1", "TK1");
            var deploymentId2 = await _service.CreateDeploymentAsync(
                "ARC200_Mintable", "voimain-v1.0", "0x2222", "Token 2", "TK2");
            var deploymentId3 = await _service.CreateDeploymentAsync(
                "ASA_FT", "aramidmain-v1.0", "0x3333", "Token 3", "TK3");

            // Update each to different states
            await _service.UpdateDeploymentStatusAsync(deploymentId1, DeploymentStatus.Submitted);
            await _service.UpdateDeploymentStatusAsync(deploymentId2, DeploymentStatus.Submitted);
            await _service.UpdateDeploymentStatusAsync(deploymentId1, DeploymentStatus.Pending);
            await _service.UpdateDeploymentStatusAsync(deploymentId2, DeploymentStatus.Pending);
            await _service.UpdateDeploymentStatusAsync(deploymentId1, DeploymentStatus.Confirmed);
            await _service.UpdateDeploymentStatusAsync(deploymentId2, DeploymentStatus.Confirmed);
            await _service.UpdateDeploymentStatusAsync(deploymentId1, DeploymentStatus.Completed);
            await _service.MarkDeploymentFailedAsync(deploymentId2, "Test failure");

            // Assert - Each should have independent state
            var deployment1 = await _service.GetDeploymentAsync(deploymentId1);
            var deployment2 = await _service.GetDeploymentAsync(deploymentId2);
            var deployment3 = await _service.GetDeploymentAsync(deploymentId3);

            Assert.That(deployment1!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed));
            Assert.That(deployment2!.CurrentStatus, Is.EqualTo(DeploymentStatus.Failed));
            Assert.That(deployment3!.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued));

            // List all deployments
            var response = await _service.GetDeploymentsAsync(new ListDeploymentsRequest { PageSize = 10 });
            Assert.That(response.Success, Is.True);
            Assert.That(response.Deployments.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public async Task PaginatedDeploymentList_ShouldReturnCorrectPages()
        {
            // Arrange - Create 15 deployments
            for (int i = 0; i < 15; i++)
            {
                await _service.CreateDeploymentAsync(
                    "ERC20_Mintable",
                    "base-mainnet",
                    $"0x{i:X4}",
                    $"Token {i}",
                    $"TK{i}");
            }

            // Act - Get first page
            var page1 = await _service.GetDeploymentsAsync(new ListDeploymentsRequest
            {
                Page = 1,
                PageSize = 5
            });

            var page2 = await _service.GetDeploymentsAsync(new ListDeploymentsRequest
            {
                Page = 2,
                PageSize = 5
            });

            // Assert
            Assert.That(page1.Success, Is.True);
            Assert.That(page1.Deployments.Count, Is.EqualTo(5));
            Assert.That(page1.TotalCount, Is.GreaterThanOrEqualTo(15));
            Assert.That(page1.TotalPages, Is.GreaterThanOrEqualTo(3));

            Assert.That(page2.Success, Is.True);
            Assert.That(page2.Deployments.Count, Is.EqualTo(5));
            Assert.That(page2.Page, Is.EqualTo(2));

            // Ensure different deployments on each page
            Assert.That(page1.Deployments[0].DeploymentId, Is.Not.EqualTo(page2.Deployments[0].DeploymentId));
        }

        [Test]
        public async Task FilterDeploymentsByNetwork_ShouldReturnOnlyMatchingDeployments()
        {
            // Arrange
            await _service.CreateDeploymentAsync("ERC20", "base-mainnet", "0x1", "Token 1", "TK1");
            await _service.CreateDeploymentAsync("ARC200", "voimain-v1.0", "0x2", "Token 2", "TK2");
            await _service.CreateDeploymentAsync("ERC20", "base-mainnet", "0x3", "Token 3", "TK3");
            await _service.CreateDeploymentAsync("ASA", "aramidmain-v1.0", "0x4", "Token 4", "TK4");

            // Act
            var baseDeployments = await _service.GetDeploymentsAsync(new ListDeploymentsRequest
            {
                Network = "base-mainnet"
            });

            var voiDeployments = await _service.GetDeploymentsAsync(new ListDeploymentsRequest
            {
                Network = "voimain-v1.0"
            });

            // Assert
            Assert.That(baseDeployments.Success, Is.True);
            Assert.That(baseDeployments.Deployments.Count, Is.EqualTo(2));
            Assert.That(baseDeployments.Deployments.All(d => d.Network == "base-mainnet"), Is.True);

            Assert.That(voiDeployments.Success, Is.True);
            Assert.That(voiDeployments.Deployments.Count, Is.EqualTo(1));
            Assert.That(voiDeployments.Deployments[0].Network, Is.EqualTo("voimain-v1.0"));
        }

        [Test]
        public async Task FilterDeploymentsByStatus_ShouldReturnOnlyMatchingDeployments()
        {
            // Arrange
            var id1 = await _service.CreateDeploymentAsync("ERC20", "base-mainnet", "0x1", "Token 1", "TK1");
            var id2 = await _service.CreateDeploymentAsync("ERC20", "base-mainnet", "0x2", "Token 2", "TK2");
            var id3 = await _service.CreateDeploymentAsync("ERC20", "base-mainnet", "0x3", "Token 3", "TK3");

            await _service.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Submitted);
            await _service.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Pending);
            await _service.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Confirmed);
            await _service.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Completed);

            await _service.UpdateDeploymentStatusAsync(id2, DeploymentStatus.Submitted);
            await _service.MarkDeploymentFailedAsync(id2, "Test failure");

            // Act
            var completedDeployments = await _service.GetDeploymentsAsync(new ListDeploymentsRequest
            {
                Status = DeploymentStatus.Completed
            });

            var failedDeployments = await _service.GetDeploymentsAsync(new ListDeploymentsRequest
            {
                Status = DeploymentStatus.Failed
            });

            var queuedDeployments = await _service.GetDeploymentsAsync(new ListDeploymentsRequest
            {
                Status = DeploymentStatus.Queued
            });

            // Assert
            Assert.That(completedDeployments.Deployments.Count, Is.EqualTo(1));
            Assert.That(completedDeployments.Deployments[0].DeploymentId, Is.EqualTo(id1));

            Assert.That(failedDeployments.Deployments.Count, Is.EqualTo(1));
            Assert.That(failedDeployments.Deployments[0].DeploymentId, Is.EqualTo(id2));

            Assert.That(queuedDeployments.Deployments.Count, Is.EqualTo(1));
            Assert.That(queuedDeployments.Deployments[0].DeploymentId, Is.EqualTo(id3));
        }
    }
}
