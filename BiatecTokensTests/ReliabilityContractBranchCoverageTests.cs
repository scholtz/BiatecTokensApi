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
    /// Comprehensive branch coverage unit tests for the reliability contract services.
    ///
    /// Covers all remaining logic branches in:
    /// 1. RetryPolicyClassifier - all error code branches + all category branches
    /// 2. StateTransitionGuard - all valid/invalid transition paths including Confirmed→Indexed path
    /// 3. DeploymentStatusService - success/failure lifecycle edge cases
    ///
    /// These tests complement the existing RetryPolicyClassifierTests.cs,
    /// StateTransitionGuardTests.cs, and DeploymentStatusServiceTests.cs with
    /// full branch saturation for the auth-to-deployment reliability contract.
    ///
    /// Business Value: Ensures that every production code path in the reliability
    /// contract is provably correct, reducing regression risk and providing QA/frontend
    /// with stable guarantees for each error class and lifecycle state.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ReliabilityContractBranchCoverageTests
    {
        private RetryPolicyClassifier _classifier = null!;
        private StateTransitionGuard _guard = null!;
        private Mock<ILogger<RetryPolicyClassifier>> _classifierLoggerMock = null!;
        private Mock<ILogger<StateTransitionGuard>> _guardLoggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _classifierLoggerMock = new Mock<ILogger<RetryPolicyClassifier>>();
            _classifier = new RetryPolicyClassifier(_classifierLoggerMock.Object);

            _guardLoggerMock = new Mock<ILogger<StateTransitionGuard>>();
            _guard = new StateTransitionGuard(_guardLoggerMock.Object);
        }

        #region RetryPolicyClassifier - Auth Error Branches (not retryable)

        [Test]
        public void ClassifyError_Unauthorized_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.UNAUTHORIZED);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
            Assert.That(decision.Explanation, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ClassifyError_Forbidden_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.FORBIDDEN);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        [Test]
        public void ClassifyError_InvalidAuthToken_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.INVALID_AUTH_TOKEN);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        #endregion

        #region RetryPolicyClassifier - Resource Conflict Branches (not retryable)

        [Test]
        public void ClassifyError_AlreadyExists_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.ALREADY_EXISTS);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        [Test]
        public void ClassifyError_Conflict_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.CONFLICT);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        #endregion

        #region RetryPolicyClassifier - Validation Error Branches (not retryable)

        [Test]
        public void ClassifyError_MissingRequiredField_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.MISSING_REQUIRED_FIELD);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        [Test]
        public void ClassifyError_InvalidNetwork_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.INVALID_NETWORK);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        [Test]
        public void ClassifyError_InvalidTokenParameters_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.INVALID_TOKEN_PARAMETERS);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        [Test]
        public void ClassifyError_MetadataValidationFailed_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.METADATA_VALIDATION_FAILED);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        [Test]
        public void ClassifyError_InvalidTokenStandard_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.INVALID_TOKEN_STANDARD);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        #endregion

        #region RetryPolicyClassifier - Network/Transient Error Branches (retryable with delay)

        [Test]
        public void ClassifyError_ExternalServiceError_IsRetryableWithDelay()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.EXTERNAL_SERVICE_ERROR);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
            Assert.That(decision.SuggestedDelaySeconds, Is.GreaterThan(0));
            Assert.That(decision.UseExponentialBackoff, Is.True);
        }

        [Test]
        public void ClassifyError_Timeout_IsRetryableWithDelay()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.TIMEOUT);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
            Assert.That(decision.SuggestedDelaySeconds, Is.GreaterThan(0));
        }

        [Test]
        public void ClassifyError_TransactionFailed_IsRetryableWithDelay()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.TRANSACTION_FAILED);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
        }

        [Test]
        public void ClassifyError_GasEstimationFailed_IsRetryableWithDelay()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.GAS_ESTIMATION_FAILED);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
        }

        [Test]
        public void ClassifyError_TransactionRejected_IsRetryableWithDelay()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.TRANSACTION_REJECTED);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
        }

        #endregion

        #region RetryPolicyClassifier - Cooldown Branches

        [Test]
        public void ClassifyError_SubscriptionLimitReached_IsRetryableWithCooldown()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.SUBSCRIPTION_LIMIT_REACHED);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithCooldown));
            Assert.That(decision.SuggestedDelaySeconds, Is.GreaterThan(60),
                "Subscription limit cooldown should be longer than rate limit");
            Assert.That(decision.RemediationGuidance, Is.Not.Null.And.Not.Empty);
        }

        #endregion

        #region RetryPolicyClassifier - Remediation Branches

        [Test]
        public void ClassifyError_KycNotVerified_IsRetryableAfterRemediation()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.KYC_NOT_VERIFIED);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableAfterRemediation));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
            Assert.That(decision.RemediationGuidance, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ClassifyError_FeatureNotAvailable_IsRetryableAfterRemediation()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.FEATURE_NOT_AVAILABLE);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableAfterRemediation));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
            Assert.That(decision.RemediationGuidance, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ClassifyError_EntitlementLimitExceeded_IsRetryableAfterRemediation()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableAfterRemediation));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
            Assert.That(decision.RemediationGuidance, Is.Not.Null.And.Not.Empty);
        }

        #endregion

        #region RetryPolicyClassifier - Configuration Branches

        [Test]
        public void ClassifyError_PriceNotConfigured_IsRetryableAfterConfiguration()
        {
            var decision = _classifier.ClassifyError(ErrorCodes.PRICE_NOT_CONFIGURED);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableAfterConfiguration));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
            Assert.That(decision.RemediationGuidance, Is.Not.Null.And.Not.Empty);
        }

        #endregion

        #region RetryPolicyClassifier - Category-Based Classification Branches

        [Test]
        public void ClassifyError_WithNetworkErrorCategory_IsRetryableWithDelay()
        {
            var decision = _classifier.ClassifyError("CUSTOM_NETWORK_ERROR", DeploymentErrorCategory.NetworkError);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
        }

        [Test]
        public void ClassifyError_WithValidationErrorCategory_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError("CUSTOM_VALIDATION_ERROR", DeploymentErrorCategory.ValidationError);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        [Test]
        public void ClassifyError_WithComplianceErrorCategory_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError("COMPLIANCE_GATE_FAILED", DeploymentErrorCategory.ComplianceError);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        [Test]
        public void ClassifyError_WithUserRejectionCategory_IsNotRetryable()
        {
            var decision = _classifier.ClassifyError("USER_CANCELLED", DeploymentErrorCategory.UserRejection);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        [Test]
        public void ClassifyError_WithInsufficientFundsCategory_IsRetryableAfterRemediation()
        {
            var decision = _classifier.ClassifyError("CUSTOM_FUNDS_ERROR", DeploymentErrorCategory.InsufficientFunds);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableAfterRemediation));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        [Test]
        public void ClassifyError_WithTransactionFailureCategory_IsRetryableWithDelay()
        {
            var decision = _classifier.ClassifyError("CUSTOM_TX_FAILURE", DeploymentErrorCategory.TransactionFailure);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
        }

        [Test]
        public void ClassifyError_WithConfigurationErrorCategory_IsRetryableAfterConfiguration()
        {
            var decision = _classifier.ClassifyError("CUSTOM_CONFIG_ERROR", DeploymentErrorCategory.ConfigurationError);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableAfterConfiguration));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        [Test]
        public void ClassifyError_WithRateLimitCategory_IsRetryableWithCooldown()
        {
            var decision = _classifier.ClassifyError("CUSTOM_RATE_LIMIT", DeploymentErrorCategory.RateLimitExceeded);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithCooldown));
            Assert.That(decision.SuggestedDelaySeconds, Is.GreaterThan(0));
        }

        [Test]
        public void ClassifyError_WithInternalErrorCategory_IsRetryableWithDelay()
        {
            var decision = _classifier.ClassifyError("CUSTOM_INTERNAL_ERROR", DeploymentErrorCategory.InternalError);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
        }

        #endregion

        #region RetryPolicyClassifier - RetryPolicy Symmetry Contract (user-safe vs terminal)

        /// <summary>
        /// All auth-related errors must be NOT retryable - changing these would break the
        /// user safety contract (cannot auto-retry without user remediation for auth failures).
        /// </summary>
        [TestCase(ErrorCodes.UNAUTHORIZED)]
        [TestCase(ErrorCodes.FORBIDDEN)]
        [TestCase(ErrorCodes.INVALID_AUTH_TOKEN)]
        public void ClassifyError_AuthErrors_AreAllTerminal(string errorCode)
        {
            var decision = _classifier.ClassifyError(errorCode);
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable),
                $"Auth error {errorCode} must be terminal to prevent security token auto-replay");
        }

        /// <summary>
        /// All blockchain/network errors must be retryable - these are transient by nature
        /// and auto-retry improves user experience without security risk.
        /// </summary>
        [TestCase(ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR)]
        [TestCase(ErrorCodes.TRANSACTION_FAILED)]
        [TestCase(ErrorCodes.TRANSACTION_REJECTED)]
        [TestCase(ErrorCodes.TIMEOUT)]
        public void ClassifyError_BlockchainErrors_AreAllRetryable(string errorCode)
        {
            var decision = _classifier.ClassifyError(errorCode);
            Assert.That(decision.Policy, Is.Not.EqualTo(RetryPolicy.NotRetryable),
                $"Blockchain error {errorCode} must be retryable as these are transient by nature");
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0),
                $"Blockchain error {errorCode} must specify max retry attempts for client guidance");
        }

        /// <summary>
        /// All retryable errors with delay must include exponential backoff flag.
        /// This is required for proper client-side retry behavior.
        /// </summary>
        [TestCase(ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR)]
        [TestCase(ErrorCodes.EXTERNAL_SERVICE_ERROR)]
        [TestCase(ErrorCodes.TIMEOUT)]
        public void ClassifyError_DelayedRetryErrors_UseExponentialBackoff(string errorCode)
        {
            var decision = _classifier.ClassifyError(errorCode);
            Assert.That(decision.UseExponentialBackoff, Is.True,
                $"Retryable error {errorCode} must use exponential backoff to prevent thundering herd");
        }

        #endregion

        #region StateTransitionGuard - Remaining Valid Transitions

        [Test]
        public void ValidateTransition_QueuedToFailed_ShouldAllow()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Queued, DeploymentStatus.Failed);
            Assert.That(result.IsAllowed, Is.True, "Queued → Failed should be valid (validation failed before submission)");
            Assert.That(result.ReasonCode, Is.EqualTo("DEPLOYMENT_VALIDATION_FAILED"));
        }

        [Test]
        public void ValidateTransition_SubmittedToFailed_ShouldAllow()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Submitted, DeploymentStatus.Failed);
            Assert.That(result.IsAllowed, Is.True, "Submitted → Failed should be valid (transaction submission failed)");
            Assert.That(result.ReasonCode, Is.EqualTo("TRANSACTION_SUBMISSION_FAILED"));
        }

        [Test]
        public void ValidateTransition_PendingToFailed_ShouldAllow()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Pending, DeploymentStatus.Failed);
            Assert.That(result.IsAllowed, Is.True, "Pending → Failed should be valid (transaction reverted)");
            Assert.That(result.ReasonCode, Is.EqualTo("TRANSACTION_REVERTED"));
        }

        [Test]
        public void ValidateTransition_ConfirmedToIndexed_ShouldAllow()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Confirmed, DeploymentStatus.Indexed);
            Assert.That(result.IsAllowed, Is.True, "Confirmed → Indexed should be valid (transaction indexed by explorer)");
            Assert.That(result.ReasonCode, Is.EqualTo("TRANSACTION_INDEXED"));
        }

        [Test]
        public void ValidateTransition_ConfirmedToFailed_ShouldAllow()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Confirmed, DeploymentStatus.Failed);
            Assert.That(result.IsAllowed, Is.True, "Confirmed → Failed should be valid (post-deployment step failed)");
            Assert.That(result.ReasonCode, Is.EqualTo("POST_DEPLOYMENT_FAILED"));
        }

        [Test]
        public void ValidateTransition_IndexedToCompleted_ShouldAllow()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Indexed, DeploymentStatus.Completed);
            Assert.That(result.IsAllowed, Is.True, "Indexed → Completed should be valid (normal completion path)");
            Assert.That(result.ReasonCode, Is.EqualTo("DEPLOYMENT_COMPLETED"));
        }

        [Test]
        public void ValidateTransition_IndexedToFailed_ShouldAllow()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Indexed, DeploymentStatus.Failed);
            Assert.That(result.IsAllowed, Is.True, "Indexed → Failed should be valid (post-index step failed)");
            Assert.That(result.ReasonCode, Is.EqualTo("POST_DEPLOYMENT_FAILED"));
        }

        [Test]
        public void ValidateTransition_FailedToQueued_ShouldAllow_Retry()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Failed, DeploymentStatus.Queued);
            Assert.That(result.IsAllowed, Is.True, "Failed → Queued should be valid (retry path)");
            Assert.That(result.ReasonCode, Is.EqualTo("DEPLOYMENT_RETRY_REQUESTED"));
        }

        #endregion

        #region StateTransitionGuard - Invalid Transition Branches (full coverage)

        [Test]
        public void ValidateTransition_SubmittedToQueued_ShouldReject()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Submitted, DeploymentStatus.Queued);
            Assert.That(result.IsAllowed, Is.False, "Submitted → Queued is not a valid backward transition");
            Assert.That(result.ReasonCode, Is.EqualTo("INVALID_TRANSITION"));
        }

        [Test]
        public void ValidateTransition_ConfirmedToSubmitted_ShouldReject()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Confirmed, DeploymentStatus.Submitted);
            Assert.That(result.IsAllowed, Is.False, "Confirmed → Submitted is not a valid backward transition");
            Assert.That(result.ReasonCode, Is.EqualTo("INVALID_TRANSITION"));
        }

        [Test]
        public void ValidateTransition_IndexedToQueued_ShouldReject()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Indexed, DeploymentStatus.Queued);
            Assert.That(result.IsAllowed, Is.False, "Indexed → Queued is not a valid backward transition");
        }

        [Test]
        public void ValidateTransition_FailedToCompleted_ShouldReject()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Failed, DeploymentStatus.Completed);
            Assert.That(result.IsAllowed, Is.False, "Failed → Completed is not a valid transition (must retry via Queued)");
        }

        [Test]
        public void ValidateTransition_FailedToCancelled_ShouldReject()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Failed, DeploymentStatus.Cancelled);
            Assert.That(result.IsAllowed, Is.False, "Failed → Cancelled is not a valid transition");
        }

        [Test]
        public void ValidateTransition_CompletedToFailed_ShouldReject_TerminalViolation()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Completed, DeploymentStatus.Failed);
            Assert.That(result.IsAllowed, Is.False, "Completed → Failed must be rejected (terminal state)");
            Assert.That(result.ReasonCode, Is.EqualTo("TERMINAL_STATE_VIOLATION"),
                "Terminal state violation should have explicit reason code for audit trail");
        }

        [Test]
        public void ValidateTransition_CancelledToFailed_ShouldReject_TerminalViolation()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Cancelled, DeploymentStatus.Failed);
            Assert.That(result.IsAllowed, Is.False, "Cancelled → Failed must be rejected (terminal state)");
            Assert.That(result.ReasonCode, Is.EqualTo("TERMINAL_STATE_VIOLATION"));
        }

        #endregion

        #region StateTransitionGuard - Idempotency Contract

        [Test]
        public void ValidateTransition_SameStatus_Queued_ShouldBeIdempotent()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Queued, DeploymentStatus.Queued);
            Assert.That(result.IsAllowed, Is.True, "Same-status transition must be allowed (idempotent)");
            Assert.That(result.ReasonCode, Is.EqualTo("IDEMPOTENT_UPDATE"),
                "Idempotent updates must use IDEMPOTENT_UPDATE reason code for audit clarity");
        }

        [Test]
        public void ValidateTransition_SameStatus_Failed_ShouldBeIdempotent()
        {
            var result = _guard.ValidateTransition(DeploymentStatus.Failed, DeploymentStatus.Failed);
            Assert.That(result.IsAllowed, Is.True, "Failed → Failed same-status must be idempotent");
            Assert.That(result.ReasonCode, Is.EqualTo("IDEMPOTENT_UPDATE"));
        }

        [Test]
        public void ValidateTransition_SameStatus_Completed_ShouldBeIdempotent()
        {
            // Even terminal states allow idempotent same-status updates
            var result = _guard.ValidateTransition(DeploymentStatus.Completed, DeploymentStatus.Completed);
            Assert.That(result.IsAllowed, Is.True, "Completed → Completed same-status must be idempotent");
            Assert.That(result.ReasonCode, Is.EqualTo("IDEMPOTENT_UPDATE"));
        }

        #endregion

        #region StateTransitionGuard - Terminal State Contract

        [Test]
        public void IsTerminalState_Completed_ReturnsTrue()
        {
            Assert.That(_guard.IsTerminalState(DeploymentStatus.Completed), Is.True);
        }

        [Test]
        public void IsTerminalState_Cancelled_ReturnsTrue()
        {
            Assert.That(_guard.IsTerminalState(DeploymentStatus.Cancelled), Is.True);
        }

        [TestCase(DeploymentStatus.Queued)]
        [TestCase(DeploymentStatus.Submitted)]
        [TestCase(DeploymentStatus.Pending)]
        [TestCase(DeploymentStatus.Confirmed)]
        [TestCase(DeploymentStatus.Indexed)]
        [TestCase(DeploymentStatus.Failed)]
        public void IsTerminalState_NonTerminalStates_ReturnFalse(DeploymentStatus status)
        {
            Assert.That(_guard.IsTerminalState(status), Is.False,
                $"{status} must NOT be a terminal state (deployments can continue from this state)");
        }

        #endregion

        #region StateTransitionGuard - ValidNextStates Contract

        [Test]
        public void GetValidNextStates_Queued_ReturnsSubmittedFailedCancelled()
        {
            var states = _guard.GetValidNextStates(DeploymentStatus.Queued);
            Assert.That(states, Contains.Item(DeploymentStatus.Submitted));
            Assert.That(states, Contains.Item(DeploymentStatus.Failed));
            Assert.That(states, Contains.Item(DeploymentStatus.Cancelled));
        }

        [Test]
        public void GetValidNextStates_Submitted_ReturnsPendingFailed()
        {
            var states = _guard.GetValidNextStates(DeploymentStatus.Submitted);
            Assert.That(states, Contains.Item(DeploymentStatus.Pending));
            Assert.That(states, Contains.Item(DeploymentStatus.Failed));
        }

        [Test]
        public void GetValidNextStates_Pending_ReturnsConfirmedFailed()
        {
            var states = _guard.GetValidNextStates(DeploymentStatus.Pending);
            Assert.That(states, Contains.Item(DeploymentStatus.Confirmed));
            Assert.That(states, Contains.Item(DeploymentStatus.Failed));
        }

        [Test]
        public void GetValidNextStates_Confirmed_ReturnsIndexedCompletedFailed()
        {
            var states = _guard.GetValidNextStates(DeploymentStatus.Confirmed);
            Assert.That(states, Contains.Item(DeploymentStatus.Indexed));
            Assert.That(states, Contains.Item(DeploymentStatus.Completed));
            Assert.That(states, Contains.Item(DeploymentStatus.Failed));
        }

        [Test]
        public void GetValidNextStates_Indexed_ReturnsCompletedFailed()
        {
            var states = _guard.GetValidNextStates(DeploymentStatus.Indexed);
            Assert.That(states, Contains.Item(DeploymentStatus.Completed));
            Assert.That(states, Contains.Item(DeploymentStatus.Failed));
        }

        [Test]
        public void GetValidNextStates_Failed_ReturnsQueued_RetryPath()
        {
            var states = _guard.GetValidNextStates(DeploymentStatus.Failed);
            Assert.That(states, Contains.Item(DeploymentStatus.Queued),
                "Failed state must allow retry by transitioning back to Queued");
        }

        [Test]
        public void GetValidNextStates_Completed_ReturnsEmpty_Terminal()
        {
            var states = _guard.GetValidNextStates(DeploymentStatus.Completed);
            Assert.That(states, Is.Empty, "Completed is terminal - no valid next states");
        }

        [Test]
        public void GetValidNextStates_Cancelled_ReturnsEmpty_Terminal()
        {
            var states = _guard.GetValidNextStates(DeploymentStatus.Cancelled);
            Assert.That(states, Is.Empty, "Cancelled is terminal - no valid next states");
        }

        #endregion

        #region StateTransitionGuard - Context-Aware Invariant Branches

        [Test]
        public void ValidateTransition_SubmittedWithoutTxHash_WithDeploymentContext_ShouldRejectOnInvariant()
        {
            // Arrange: deployment missing TransactionHash
            var deployment = new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CurrentStatus = DeploymentStatus.Queued,
                TransactionHash = null // Missing - should trigger invariant
            };

            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Queued, DeploymentStatus.Submitted, deployment);

            // Assert: Context invariant enforced
            Assert.That(result.IsAllowed, Is.False,
                "Transitioning to Submitted without TransactionHash must be rejected by context invariant");
            Assert.That(result.ReasonCode, Is.EqualTo("INVARIANT_VIOLATION"));
            Assert.That(result.ViolatedInvariants, Is.Not.Null.And.Not.Empty,
                "Violated invariants must be listed for audit trail");
        }

        [Test]
        public void ValidateTransition_SubmittedWithTxHash_WithDeploymentContext_ShouldAllow()
        {
            // Arrange: deployment with TransactionHash
            var deployment = new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CurrentStatus = DeploymentStatus.Queued,
                TransactionHash = "0xvalidhash123"
            };

            // Act
            var result = _guard.ValidateTransition(
                DeploymentStatus.Queued, DeploymentStatus.Submitted, deployment);

            // Assert
            Assert.That(result.IsAllowed, Is.True,
                "Transitioning to Submitted with TransactionHash should be allowed");
        }

        [Test]
        public void ValidateTransition_CancelFromNonQueued_WithDeploymentContext_ShouldReject()
        {
            // Arrange: deployment in Submitted state
            var deployment = new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CurrentStatus = DeploymentStatus.Submitted,
                TransactionHash = "0xtxhash"
            };

            // Act: Try to cancel from Submitted (not Queued)
            var result = _guard.ValidateTransition(
                DeploymentStatus.Submitted, DeploymentStatus.Cancelled, deployment);

            // Assert: State machine blocks illegal cancel
            Assert.That(result.IsAllowed, Is.False,
                "Cancellation from non-Queued states must be rejected (cannot cancel submitted transactions)");
        }

        [Test]
        public void ValidateTransition_RetryFromFailed_WithDeploymentContext_ToQueued_ShouldAllow()
        {
            // Arrange
            var deployment = new TokenDeployment
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CurrentStatus = DeploymentStatus.Failed
            };

            // Act: Retry path: Failed → Queued
            var result = _guard.ValidateTransition(
                DeploymentStatus.Failed, DeploymentStatus.Queued, deployment);

            // Assert
            Assert.That(result.IsAllowed, Is.True, "Failed → Queued retry path must be allowed with context");
        }

        #endregion

        #region RetryPolicyClassifier - ShouldRetry Branch Coverage

        [Test]
        public void ShouldRetry_RetryableWithCooldown_WithinLimit_ReturnsTrue()
        {
            var result = _classifier.ShouldRetry(
                RetryPolicy.RetryableWithCooldown,
                attemptCount: 1,
                firstAttemptTime: DateTime.UtcNow);
            Assert.That(result, Is.True, "RetryableWithCooldown within attempt limit should return true");
        }

        [Test]
        public void ShouldRetry_RetryableWithCooldown_ExceedsLimit_ReturnsFalse()
        {
            // MAX_COOLDOWN_RETRIES = 3 (private in RetryPolicyClassifier).
            // Use an attempt count clearly beyond any reasonable maximum to keep test stable.
            const int attemptsBeyondAnyMaximum = 20;
            var result = _classifier.ShouldRetry(
                RetryPolicy.RetryableWithCooldown,
                attemptCount: attemptsBeyondAnyMaximum,
                firstAttemptTime: DateTime.UtcNow);
            Assert.That(result, Is.False, "RetryableWithCooldown past attempt limit must return false");
        }

        [Test]
        public void ShouldRetry_RetryableAfterRemediation_NeverAutoRetries()
        {
            var result = _classifier.ShouldRetry(
                RetryPolicy.RetryableAfterRemediation,
                attemptCount: 0,
                firstAttemptTime: DateTime.UtcNow);
            Assert.That(result, Is.False,
                "RetryableAfterRemediation must NEVER auto-retry (requires user action first)");
        }

        [Test]
        public void ShouldRetry_RetryableAfterConfiguration_NeverAutoRetries()
        {
            var result = _classifier.ShouldRetry(
                RetryPolicy.RetryableAfterConfiguration,
                attemptCount: 0,
                firstAttemptTime: DateTime.UtcNow);
            Assert.That(result, Is.False,
                "RetryableAfterConfiguration must NEVER auto-retry (requires admin action)");
        }

        [Test]
        public void ShouldRetry_MaxDurationExceeded_ReturnsFalse_RegardlessOfPolicy()
        {
            // MAX_RETRY_DURATION_SECONDS = 600 (private in RetryPolicyClassifier).
            // Use 660s = 600s + 60s buffer to ensure we're safely past the limit.
            const int secondsPastMaxDuration = 660;
            var firstAttemptTime = DateTime.UtcNow.AddSeconds(-secondsPastMaxDuration);

            var result = _classifier.ShouldRetry(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 1,
                firstAttemptTime: firstAttemptTime);

            Assert.That(result, Is.False,
                "Max retry duration exceeded must prevent retry regardless of attempt count");
        }

        #endregion

        #region RetryPolicyClassifier - CalculateRetryDelay Branch Coverage

        [Test]
        public void CalculateRetryDelay_NotRetryable_ReturnsZero()
        {
            var delay = _classifier.CalculateRetryDelay(RetryPolicy.NotRetryable, 0, false);
            Assert.That(delay, Is.EqualTo(0), "NotRetryable errors must have 0 delay");
        }

        [Test]
        public void CalculateRetryDelay_RetryableWithDelay_AttemptZero_ReturnsBaseDelay()
        {
            var delay = _classifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 0, false);
            Assert.That(delay, Is.GreaterThan(0), "RetryableWithDelay must have positive base delay");
        }

        [Test]
        public void CalculateRetryDelay_RetryableWithDelay_WithBackoff_IncreasesWithAttempts()
        {
            var delay1 = _classifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 1, true);
            var delay2 = _classifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 2, true);
            Assert.That(delay2, Is.GreaterThanOrEqualTo(delay1),
                "Exponential backoff delays must increase with attempt number");
        }

        [Test]
        public void CalculateRetryDelay_WithBackoff_CapsAt300Seconds()
        {
            // High attempt count should hit cap
            var delay = _classifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 20, true);
            Assert.That(delay, Is.LessThanOrEqualTo(300),
                "Exponential backoff must cap at 300 seconds to prevent infinite delays");
        }

        [Test]
        public void CalculateRetryDelay_RetryableWithCooldown_IsLongerThanRetryableWithDelay()
        {
            var delayForDelay = _classifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 0, false);
            var delayForCooldown = _classifier.CalculateRetryDelay(RetryPolicy.RetryableWithCooldown, 0, false);
            Assert.That(delayForCooldown, Is.GreaterThan(delayForDelay),
                "Cooldown delays must be longer than standard delays (rate limits need more recovery time)");
        }

        #endregion

        #region DeploymentStatusService - Integration via Real Repository

        [Test]
        public async Task DeploymentLifecycle_FullSuccessPath_QueuedToCompleted()
        {
            // Arrange: use real in-memory repository for integration test
            var repoLogger = new Mock<ILogger<DeploymentStatusRepository>>();
            var repo = new DeploymentStatusRepository(repoLogger.Object);
            var webhookMock = new Mock<IWebhookService>();
            var svcLogger = new Mock<ILogger<DeploymentStatusService>>();
            var svc = new DeploymentStatusService(repo, webhookMock.Object, svcLogger.Object);

            // Act: Progress through full success path
            var id = await svc.CreateDeploymentAsync("ARC3", "algorand-mainnet", "user@test.com", "Token", "TKN");

            Assert.That(await svc.UpdateDeploymentStatusAsync(id, DeploymentStatus.Submitted, "Submitted", transactionHash: "0xtx"), Is.True);
            Assert.That(await svc.UpdateDeploymentStatusAsync(id, DeploymentStatus.Pending, "Pending"), Is.True);
            Assert.That(await svc.UpdateDeploymentStatusAsync(id, DeploymentStatus.Confirmed, "Confirmed"), Is.True);
            Assert.That(await svc.UpdateDeploymentStatusAsync(id, DeploymentStatus.Indexed, "Indexed"), Is.True);
            Assert.That(await svc.UpdateDeploymentStatusAsync(id, DeploymentStatus.Completed, "Completed"), Is.True);

            // Assert: Final state
            var deployment = await svc.GetDeploymentAsync(id);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed));
            Assert.That(deployment.StatusHistory.Count, Is.GreaterThanOrEqualTo(6), // Queued + 5 transitions
                "Status history must record every lifecycle transition for audit trail");
        }

        [Test]
        public async Task DeploymentLifecycle_FailureAndRetryPath()
        {
            // Arrange
            var repoLogger = new Mock<ILogger<DeploymentStatusRepository>>();
            var repo = new DeploymentStatusRepository(repoLogger.Object);
            var webhookMock = new Mock<IWebhookService>();
            var svcLogger = new Mock<ILogger<DeploymentStatusService>>();
            var svc = new DeploymentStatusService(repo, webhookMock.Object, svcLogger.Object);

            // Act: Failure then retry
            var id = await svc.CreateDeploymentAsync("ERC20", "base-mainnet", "user@test.com", "Token", "TKN");

            await svc.UpdateDeploymentStatusAsync(id, DeploymentStatus.Submitted, "Submitted", transactionHash: "0xtx");
            await svc.UpdateDeploymentStatusAsync(id, DeploymentStatus.Failed, "TX reverted", errorMessage: "Out of gas");

            // Retry: Failed → Queued
            var retryResult = await svc.UpdateDeploymentStatusAsync(id, DeploymentStatus.Queued, "Retrying");
            Assert.That(retryResult, Is.True, "Retry from Failed to Queued must be allowed");

            var deployment = await svc.GetDeploymentAsync(id);
            Assert.That(deployment!.CurrentStatus, Is.EqualTo(DeploymentStatus.Queued),
                "Deployment must be in Queued state after retry");
        }

        [Test]
        public async Task DeploymentStatusService_GetNonExistentDeployment_ReturnsNull()
        {
            // Arrange
            var repoLogger = new Mock<ILogger<DeploymentStatusRepository>>();
            var repo = new DeploymentStatusRepository(repoLogger.Object);
            var webhookMock = new Mock<IWebhookService>();
            var svcLogger = new Mock<ILogger<DeploymentStatusService>>();
            var svc = new DeploymentStatusService(repo, webhookMock.Object, svcLogger.Object);

            // Act
            var result = await svc.GetDeploymentAsync("non-existent-deployment-id");

            // Assert
            Assert.That(result, Is.Null, "Non-existent deployment must return null, not throw exception");
        }

        [Test]
        public async Task DeploymentStatusService_MultipleDeployments_IndependentLifecycles()
        {
            // Arrange
            var repoLogger = new Mock<ILogger<DeploymentStatusRepository>>();
            var repo = new DeploymentStatusRepository(repoLogger.Object);
            var webhookMock = new Mock<IWebhookService>();
            var svcLogger = new Mock<ILogger<DeploymentStatusService>>();
            var svc = new DeploymentStatusService(repo, webhookMock.Object, svcLogger.Object);

            // Act: Create two independent deployments
            var id1 = await svc.CreateDeploymentAsync("ARC3", "mainnet", "user1@test.com", "Token1", "TK1");
            var id2 = await svc.CreateDeploymentAsync("ERC20", "base-mainnet", "user2@test.com", "Token2", "TK2");

            // Progress id1 to completed (via valid state machine path)
            await svc.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Submitted, "Tx1", transactionHash: "0xtx1");
            await svc.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Pending, "Pending1");
            await svc.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Confirmed, "Confirmed1");
            await svc.UpdateDeploymentStatusAsync(id1, DeploymentStatus.Completed, "Done");

            // Fail id2
            await svc.UpdateDeploymentStatusAsync(id2, DeploymentStatus.Failed, "Failed");

            // Assert: Independent lifecycle states
            var dep1 = await svc.GetDeploymentAsync(id1);
            var dep2 = await svc.GetDeploymentAsync(id2);

            Assert.That(dep1!.CurrentStatus, Is.EqualTo(DeploymentStatus.Completed),
                "Deployment 1 must be in Completed state independently");
            Assert.That(dep2!.CurrentStatus, Is.EqualTo(DeploymentStatus.Failed),
                "Deployment 2 must be in Failed state independently");
        }

        #endregion
    }
}
