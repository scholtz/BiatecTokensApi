using BiatecTokensApi.Models;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for StateTransitionGuard service
    /// 
    /// Validates that state transition guards enforce business invariants:
    /// 1. Only valid state transitions are allowed
    /// 2. Terminal states cannot transition
    /// 3. Context-aware invariants are enforced
    /// 4. Transition reason codes are assigned correctly
    /// 
    /// Business Value: Prevents state corruption in deployment tracking,
    /// ensuring audit trail integrity and preventing billing errors.
    /// </summary>
    [TestFixture]
    public class StateTransitionGuardTests
    {
        private StateTransitionGuard _guard = null!;
        private Mock<ILogger<StateTransitionGuard>> _loggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<StateTransitionGuard>>();
            _guard = new StateTransitionGuard(_loggerMock.Object);
        }

        #region Valid Transition Tests

        [Test]
        public void ValidateTransition_QueuedToSubmitted_ShouldAllow()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Queued,
                DeploymentStatus.Submitted);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.ReasonCode, Is.EqualTo("DEPLOYMENT_SUBMITTED"));
        }

        [Test]
        public void ValidateTransition_SubmittedToPending_ShouldAllow()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Submitted,
                DeploymentStatus.Pending);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.ReasonCode, Is.EqualTo("TRANSACTION_BROADCAST"));
        }

        [Test]
        public void ValidateTransition_PendingToConfirmed_ShouldAllow()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Pending,
                DeploymentStatus.Confirmed);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.ReasonCode, Is.EqualTo("TRANSACTION_CONFIRMED"));
        }

        [Test]
        public void ValidateTransition_ConfirmedToCompleted_ShouldAllow()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Confirmed,
                DeploymentStatus.Completed);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.ReasonCode, Is.EqualTo("DEPLOYMENT_COMPLETED"));
        }

        [Test]
        public void ValidateTransition_FailedToQueued_ShouldAllowRetry()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Failed,
                DeploymentStatus.Queued);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.ReasonCode, Is.EqualTo("DEPLOYMENT_RETRY_REQUESTED"));
        }

        [Test]
        public void ValidateTransition_QueuedToCancelled_ShouldAllow()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Queued,
                DeploymentStatus.Cancelled);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.ReasonCode, Is.EqualTo("USER_CANCELLED"));
        }

        #endregion

        #region Invalid Transition Tests

        [Test]
        public void ValidateTransition_QueuedToCompleted_ShouldReject()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Queued,
                DeploymentStatus.Completed);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("INVALID_TRANSITION"));
            Assert.That(result.ValidAlternatives, Is.Not.Null);
            Assert.That(result.ValidAlternatives, Contains.Item(DeploymentStatus.Submitted));
        }

        [Test]
        public void ValidateTransition_SubmittedToCompleted_ShouldReject()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Submitted,
                DeploymentStatus.Completed);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("INVALID_TRANSITION"));
            Assert.That(result.ViolatedInvariants, Is.Not.Empty);
        }

        [Test]
        public void ValidateTransition_PendingToQueued_ShouldReject()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Pending,
                DeploymentStatus.Queued);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("INVALID_TRANSITION"));
        }

        #endregion

        #region Terminal State Tests

        [Test]
        public void ValidateTransition_CompletedToAnyState_ShouldReject()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Completed,
                DeploymentStatus.Queued);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("TERMINAL_STATE_VIOLATION"));
            Assert.That(result.Explanation, Does.Contain("terminal"));
        }

        [Test]
        public void ValidateTransition_CancelledToAnyState_ShouldReject()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Cancelled,
                DeploymentStatus.Failed);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("TERMINAL_STATE_VIOLATION"));
        }

        [Test]
        public void IsTerminalState_Completed_ShouldReturnTrue()
        {
            // Act
            var isTerminal = _guard.IsTerminalState(DeploymentStatus.Completed);

            // Assert
            Assert.That(isTerminal, Is.True);
        }

        [Test]
        public void IsTerminalState_Cancelled_ShouldReturnTrue()
        {
            // Act
            var isTerminal = _guard.IsTerminalState(DeploymentStatus.Cancelled);

            // Assert
            Assert.That(isTerminal, Is.True);
        }

        [Test]
        public void IsTerminalState_Queued_ShouldReturnFalse()
        {
            // Act
            var isTerminal = _guard.IsTerminalState(DeploymentStatus.Queued);

            // Assert
            Assert.That(isTerminal, Is.False);
        }

        #endregion

        #region Idempotency Tests

        [Test]
        public void ValidateTransition_SameStatus_ShouldAllowIdempotent()
        {
            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Pending,
                DeploymentStatus.Pending);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.ReasonCode, Is.EqualTo("IDEMPOTENT_UPDATE"));
        }

        #endregion

        #region Valid Next States Tests

        [Test]
        public void GetValidNextStates_Queued_ShouldReturnCorrectStates()
        {
            // Act
            var nextStates = _guard.GetValidNextStates(DeploymentStatus.Queued);

            // Assert
            Assert.That(nextStates, Contains.Item(DeploymentStatus.Submitted));
            Assert.That(nextStates, Contains.Item(DeploymentStatus.Failed));
            Assert.That(nextStates, Contains.Item(DeploymentStatus.Cancelled));
            Assert.That(nextStates.Count, Is.EqualTo(3));
        }

        [Test]
        public void GetValidNextStates_Completed_ShouldReturnEmpty()
        {
            // Act
            var nextStates = _guard.GetValidNextStates(DeploymentStatus.Completed);

            // Assert
            Assert.That(nextStates, Is.Empty);
        }

        [Test]
        public void GetValidNextStates_Confirmed_ShouldReturnMultiple()
        {
            // Act
            var nextStates = _guard.GetValidNextStates(DeploymentStatus.Confirmed);

            // Assert
            Assert.That(nextStates, Contains.Item(DeploymentStatus.Indexed));
            Assert.That(nextStates, Contains.Item(DeploymentStatus.Completed));
            Assert.That(nextStates, Contains.Item(DeploymentStatus.Failed));
            Assert.That(nextStates.Count, Is.EqualTo(3));
        }

        #endregion

        #region Transition Reason Code Tests

        [Test]
        public void GetTransitionReasonCode_QueuedToSubmitted_ShouldReturnCorrectCode()
        {
            // Act
            var reasonCode = _guard.GetTransitionReasonCode(
                DeploymentStatus.Queued,
                DeploymentStatus.Submitted);

            // Assert
            Assert.That(reasonCode, Is.EqualTo("DEPLOYMENT_SUBMITTED"));
        }

        [Test]
        public void GetTransitionReasonCode_PendingToConfirmed_ShouldReturnCorrectCode()
        {
            // Act
            var reasonCode = _guard.GetTransitionReasonCode(
                DeploymentStatus.Pending,
                DeploymentStatus.Confirmed);

            // Assert
            Assert.That(reasonCode, Is.EqualTo("TRANSACTION_CONFIRMED"));
        }

        [Test]
        public void GetTransitionReasonCode_UnknownTransition_ShouldReturnDefaultCode()
        {
            // Act - Invalid transition but should still return a code
            var reasonCode = _guard.GetTransitionReasonCode(
                DeploymentStatus.Completed,
                DeploymentStatus.Queued);

            // Assert
            Assert.That(reasonCode, Does.StartWith("TRANSITION_"));
            Assert.That(reasonCode, Does.Contain("COMPLETED"));
            Assert.That(reasonCode, Does.Contain("QUEUED"));
        }

        #endregion

        #region Failure Path Tests

        [Test]
        public void ValidateTransition_AnyStatusToFailed_ShouldAllow()
        {
            // Test that any non-terminal status can transition to Failed
            var nonTerminalStatuses = new[]
            {
                DeploymentStatus.Queued,
                DeploymentStatus.Submitted,
                DeploymentStatus.Pending,
                DeploymentStatus.Confirmed,
                DeploymentStatus.Indexed
            };

            foreach (var status in nonTerminalStatuses)
            {
                var result = _guard.ValidateTransition(status, DeploymentStatus.Failed);
                Assert.That(result.IsAllowed, Is.True,
                    $"{status} should be able to transition to Failed");
            }
        }

        #endregion

        #region Context-Aware Invariant Tests

        [Test]
        public void ValidateTransition_SubmittedWithoutTxHash_ShouldReject()
        {
            // Arrange - Deployment without transaction hash
            var deployment = new TokenDeployment
            {
                DeploymentId = "test-123",
                CurrentStatus = DeploymentStatus.Queued,
                TransactionHash = null // Missing required field
            };

            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Queued,
                DeploymentStatus.Submitted,
                deployment);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("INVARIANT_VIOLATION"));
            Assert.That(result.ViolatedInvariants, Is.Not.Empty);
            Assert.That(result.ViolatedInvariants!.Any(m => m.Contains("TransactionHash")), Is.True);
        }

        [Test]
        public void ValidateTransition_CancelFromNonQueued_ShouldReject()
        {
            // Arrange
            var deployment = new TokenDeployment
            {
                DeploymentId = "test-123",
                CurrentStatus = DeploymentStatus.Pending
            };

            // Act - Try to cancel from Pending (not allowed)
            var result = _guard.ValidateTransition(
                DeploymentStatus.Pending,
                DeploymentStatus.Cancelled,
                deployment);

            // Assert - Should be invalid transition, not invariant violation
            // because Cancelled is not in valid transitions from Pending
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.ReasonCode, Is.EqualTo("INVALID_TRANSITION"));
        }

        #endregion
    }
}
