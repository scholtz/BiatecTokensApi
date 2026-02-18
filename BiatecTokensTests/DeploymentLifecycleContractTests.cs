using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Contract tests for deployment lifecycle state machine
    /// 
    /// Validates that deployment status transitions follow strict rules:
    /// 1. Only valid state transitions are allowed
    /// 2. Invalid transitions return proper error indicators
    /// 3. Status history maintains chronological ordering
    /// 4. Response schemas are consistent across states
    /// 5. Idempotency - setting same status twice is harmless
    /// 
    /// Business Value: Ensures deployment lifecycle is predictable and auditable,
    /// supporting compliance requirements and preventing state corruption.
    /// 
    /// Risk Mitigation: Strict state machine prevents invalid operations that could
    /// lead to inconsistent deployment records or lost transaction tracking.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class DeploymentLifecycleContractTests
    {
        private IDeploymentStatusService _service = null!;
        private Mock<IWebhookService> _webhookServiceMock = null!;

        [SetUp]
        public void Setup()
        {
            var repositoryLogger = new Mock<ILogger<DeploymentStatusRepository>>();
            var repository = new DeploymentStatusRepository(repositoryLogger.Object);

            _webhookServiceMock = new Mock<IWebhookService>();
            var serviceLogger = new Mock<ILogger<DeploymentStatusService>>();

            _service = new DeploymentStatusService(
                repository,
                _webhookServiceMock.Object,
                serviceLogger.Object);
        }

        #region State Transition Contract Tests

        [Test]
        public async Task StateTransition_QueuedToSubmitted_ShouldSucceed()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable", "base-mainnet", "0xCreator", "Token", "TKN");

            // Act
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "Transaction submitted",
                transactionHash: "0xtxhash");

            // Assert
            Assert.That(result, Is.True, "Queued → Submitted should be valid transition");
            
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Submitted));
            Assert.That(deployment.TransactionHash, Is.EqualTo("0xtxhash"));
        }

        [Test]
        public async Task StateTransition_SubmittedToPending_ShouldSucceed()
        {
            // Arrange
            var deploymentId = await CreateDeploymentInState(DeploymentStatus.Submitted);

            // Act
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Pending,
                "Transaction pending");

            // Assert
            Assert.That(result, Is.True, "Submitted → Pending should be valid transition");
            
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Pending));
        }

        [Test]
        public async Task StateTransition_PendingToConfirmed_ShouldSucceed()
        {
            // Arrange
            var deploymentId = await CreateDeploymentInState(DeploymentStatus.Pending);

            // Act
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Confirmed,
                "Transaction confirmed",
                confirmedRound: 12345);

            // Assert
            Assert.That(result, Is.True, "Pending → Confirmed should be valid transition");
            
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Confirmed));
            // ConfirmedRound is stored in status history entry, not on deployment directly
            var confirmedEntry = deployment.StatusHistory.FirstOrDefault(h => h.Status == DeploymentStatus.Confirmed);
            Assert.That(confirmedEntry, Is.Not.Null);
            Assert.That(confirmedEntry!.ConfirmedRound, Is.EqualTo(12345));
        }

        [Test]
        public async Task StateTransition_ConfirmedToIndexed_ShouldSucceed()
        {
            // Arrange
            var deploymentId = await CreateDeploymentInState(DeploymentStatus.Confirmed);

            // Act
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Indexed,
                "Transaction indexed");

            // Assert
            Assert.That(result, Is.True, "Confirmed → Indexed should be valid transition");
            
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Indexed));
        }

        [Test]
        public async Task StateTransition_IndexedToCompleted_ShouldSucceed()
        {
            // Arrange
            var deploymentId = await CreateDeploymentInState(DeploymentStatus.Indexed);

            // Act
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Completed,
                "Deployment completed");

            // Assert
            Assert.That(result, Is.True, "Indexed → Completed should be valid transition");
            
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed));
        }

        [Test]
        public async Task StateTransition_AnyStateToFailed_ShouldSucceed()
        {
            // Arrange - Test from multiple starting states
            var testStates = new[]
            {
                DeploymentStatus.Queued,
                DeploymentStatus.Submitted,
                DeploymentStatus.Pending,
                DeploymentStatus.Confirmed,
                DeploymentStatus.Indexed
            };

            foreach (var startState in testStates)
            {
                var deploymentId = await CreateDeploymentInState(startState);

                // Act
                var result = await _service.UpdateDeploymentStatusAsync(
                    deploymentId,
                    DeploymentStatus.Failed,
                    $"Failed from {startState}",
                    errorMessage: "Network timeout");

                // Assert
                Assert.That(result, Is.True, 
                    $"{startState} → Failed should be valid transition");
                
                var deployment = await _service.GetDeploymentAsync(deploymentId);
                Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Failed));
                Assert.That(deployment.ErrorMessage, Is.EqualTo("Network timeout"));
            }
        }

        [Test]
        public async Task StateTransition_FailedToQueued_Retry_ShouldSucceed()
        {
            // Arrange
            var deploymentId = await CreateDeploymentInState(DeploymentStatus.Failed);

            // Act - Retry by moving back to Queued
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Queued,
                "Retrying deployment");

            // Assert
            Assert.That(result, Is.True, "Failed → Queued (retry) should be valid transition");
            
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued));
        }

        [Test]
        public async Task StateTransition_QueuedToCancelled_ShouldSucceed()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable", "base-mainnet", "0xCreator", "Token", "TKN");

            // Act
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Cancelled,
                "User cancelled deployment");

            // Assert
            Assert.That(result, Is.True, "Queued → Cancelled should be valid transition");
            
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Cancelled));
        }

        #endregion

        #region Invalid Transition Tests

        [Test]
        public async Task StateTransition_CompletedToSubmitted_ShouldFail()
        {
            // Arrange
            var deploymentId = await CreateDeploymentInState(DeploymentStatus.Completed);

            // Act
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "Invalid backward transition");

            // Assert
            Assert.That(result, Is.False, 
                "Completed → Submitted should be invalid transition");
            
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed),
                "Status should remain unchanged on invalid transition");
        }

        [Test]
        public async Task StateTransition_QueuedToCompleted_SkippingStates_ShouldFail()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable", "base-mainnet", "0xCreator", "Token", "TKN");

            // Act - Try to jump directly to Completed
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Completed,
                "Invalid jump to completed");

            // Assert
            Assert.That(result, Is.False, 
                "Queued → Completed (skipping states) should be invalid");
            
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued),
                "Status should remain Queued on invalid transition");
        }

        [Test]
        public async Task StateTransition_CancelledToAnyOtherState_ShouldFail()
        {
            // Arrange
            var deploymentId = await CreateDeploymentInState(DeploymentStatus.Cancelled);

            // Act - Try to transition from Cancelled to Submitted
            var result = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "Invalid transition from cancelled");

            // Assert
            Assert.That(result, Is.False, 
                "Cancelled → any other state should be invalid (terminal state)");
            
            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Cancelled),
                "Cancelled is terminal state");
        }

        #endregion

        #region Idempotency Tests

        [Test]
        public async Task StateTransition_SetSameStatusTwice_ShouldBeIdempotent()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable", "base-mainnet", "0xCreator", "Token", "TKN");

            // Act - Transition to Submitted twice
            var result1 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "First submission",
                transactionHash: "0xtxhash");

            var result2 = await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Submitted,
                "Duplicate submission",
                transactionHash: "0xtxhash");

            // Assert
            Assert.That(result1, Is.True, "First transition should succeed");
            Assert.That(result2, Is.True, "Idempotent: setting same status should succeed");

            var deployment = await _service.GetDeploymentAsync(deploymentId);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Submitted));

            // History should still show transition only once or twice (implementation dependent)
            var history = await _service.GetStatusHistoryAsync(deploymentId);
            Assert.That(history, Is.Not.Null);
            // Either implementation is acceptable: dedupe or record both
            Assert.That(history.Count, Is.GreaterThanOrEqualTo(2)); // Queued + Submitted
        }

        #endregion

        #region Status History Contract Tests

        [Test]
        public async Task StatusHistory_ShouldMaintainChronologicalOrder()
        {
            // Arrange - Create deployment and transition through states
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable", "base-mainnet", "0xCreator", "Token", "TKN");

            // Act - Perform multiple transitions
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Step 1");
            await Task.Delay(100); // Ensure timestamps differ
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending, "Step 2");
            await Task.Delay(100);
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed, "Step 3");

            // Assert
            var history = await _service.GetStatusHistoryAsync(deploymentId);
            Assert.That(history.Count, Is.GreaterThanOrEqualTo(4)); // Queued + 3 updates

            // Verify chronological ordering
            for (int i = 1; i < history.Count; i++)
            {
                Assert.That(history[i].Timestamp, Is.GreaterThanOrEqualTo(history[i - 1].Timestamp),
                    $"History entry {i} should have timestamp >= entry {i-1}");
            }

            // Verify status progression
            Assert.That(history[0].Status, Is.EqualTo(DeploymentStatus.Queued));
            Assert.That(history[1].Status, Is.EqualTo(DeploymentStatus.Submitted));
            Assert.That(history[2].Status, Is.EqualTo(DeploymentStatus.Pending));
            Assert.That(history[3].Status, Is.EqualTo(DeploymentStatus.Confirmed));
        }

        [Test]
        public async Task StatusHistory_ShouldPreserveAllFields()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable", "base-mainnet", "0xCreator", "Token", "TKN");

            // Act - Transition through proper states to reach Confirmed
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Submitted", transactionHash: "0xtxhash123");
            await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending, "Pending");
            await _service.UpdateDeploymentStatusAsync(
                deploymentId,
                DeploymentStatus.Confirmed,
                "Transaction confirmed with full details",
                transactionHash: "0xtxhash123",
                confirmedRound: 54321,
                errorMessage: null);

            // Assert
            var history = await _service.GetStatusHistoryAsync(deploymentId);
            var confirmedEntry = history.FirstOrDefault(h => h.Status == DeploymentStatus.Confirmed);
            
            Assert.That(confirmedEntry, Is.Not.Null, "Confirmed status should be in history");
            Assert.That(confirmedEntry!.TransactionHash, Is.EqualTo("0xtxhash123"));
            Assert.That(confirmedEntry.ConfirmedRound, Is.EqualTo(54321));
            Assert.That(confirmedEntry.Message, Contains.Substring("confirmed"));
        }

        #endregion

        #region Response Schema Contract Tests

        [Test]
        public async Task GetDeployment_ShouldReturnConsistentSchema()
        {
            // Arrange
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable", "base-mainnet", "0xCreatorAddress", "TestToken", "TST");

            // Act
            var deployment = await _service.GetDeploymentAsync(deploymentId);

            // Assert - Verify all expected fields are present
            Assert.That(deployment, Is.Not.Null, "Deployment should be retrievable");
            Assert.That(deployment!.DeploymentId, Is.EqualTo(deploymentId));
            Assert.That(deployment.TokenType, Is.EqualTo("ERC20_Mintable"));
            Assert.That(deployment.Network, Is.EqualTo("base-mainnet"));
            Assert.That(deployment.DeployedBy, Is.EqualTo("0xCreatorAddress"));
            Assert.That(deployment.TokenName, Is.EqualTo("TestToken"));
            Assert.That(deployment.TokenSymbol, Is.EqualTo("TST"));
            Assert.That(deployment.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued));
            Assert.That(deployment.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(deployment.UpdatedAt, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public async Task ListDeployments_Comment_NotImplemented()
        {
            // Note: ListDeploymentsAsync is not part of IDeploymentStatusService interface
            // This test is omitted as the repository pattern doesn't expose list operations through service
            Assert.Pass("ListDeploymentsAsync not available in service interface - list operations handled by controller/repository");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Creates a deployment and transitions it to the specified state
        /// </summary>
        private async Task<string> CreateDeploymentInState(DeploymentStatus targetState)
        {
            var deploymentId = await _service.CreateDeploymentAsync(
                "ERC20_Mintable",
                "base-mainnet",
                "0xCreator",
                "Token",
                "TKN");

            // Transition through valid states to reach target
            if (targetState == DeploymentStatus.Queued)
                return deploymentId; // Already queued

            if (targetState == DeploymentStatus.Submitted)
            {
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Submitted", transactionHash: "0xtx");
                return deploymentId;
            }

            if (targetState == DeploymentStatus.Pending)
            {
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Submitted", transactionHash: "0xtx");
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending, "Pending");
                return deploymentId;
            }

            if (targetState == DeploymentStatus.Confirmed)
            {
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Submitted", transactionHash: "0xtx");
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending, "Pending");
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed, "Confirmed", confirmedRound: 123);
                return deploymentId;
            }

            if (targetState == DeploymentStatus.Indexed)
            {
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Submitted", transactionHash: "0xtx");
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending, "Pending");
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed, "Confirmed", confirmedRound: 123);
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Indexed, "Indexed");
                return deploymentId;
            }

            if (targetState == DeploymentStatus.Completed)
            {
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Submitted, "Submitted", transactionHash: "0xtx");
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Pending, "Pending");
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Confirmed, "Confirmed", confirmedRound: 123);
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Indexed, "Indexed");
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Completed, "Completed");
                return deploymentId;
            }

            if (targetState == DeploymentStatus.Failed)
            {
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Failed, "Failed", errorMessage: "Test failure");
                return deploymentId;
            }

            if (targetState == DeploymentStatus.Cancelled)
            {
                await _service.UpdateDeploymentStatusAsync(deploymentId, DeploymentStatus.Cancelled, "Cancelled");
                return deploymentId;
            }

            throw new ArgumentException($"Unknown target state: {targetState}");
        }

        #endregion
    }
}
