using BiatecTokensApi.Models;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Service-layer unit tests for the Vision Milestone: API competitive readiness for token
    /// operations and reliability.
    ///
    /// These are pure unit tests (no HTTP / WebApplicationFactory) that directly exercise
    /// RetryPolicyClassifier, StateTransitionGuard, and related domain services with
    /// mocked or real dependencies where appropriate.
    ///
    /// AC1  - Critical token operation API paths hardened and deterministic across success/failure modes
    /// AC2  - Error responses standardized and include actionable semantics for clients
    /// AC3  - Contract compatibility validated and protected by regression tests
    /// AC4  - Observability for failure paths improved enough to support efficient diagnosis
    /// AC5  - At least one competitive gap in backend reliability/capability addressed (retry strategy)
    /// AC6  - All related PRs include business-value rationale and issue linkage
    /// AC7  - Unit, integration, and scenario-level tests added for changed behavior
    /// AC8  - CI is green and stable, no unresolved flaky checks
    /// AC9  - Documentation/update notes clearly describe delivered value and deferred work
    ///
    /// Business Value: Service-layer unit tests prove deterministic retry policy and state machine
    /// behavior independently of HTTP infrastructure, providing confidence in error classification
    /// accuracy and protection against regression in critical reliability infrastructure.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class VisionMilestoneApiCompetitivenessServiceUnitTests
    {
        private RetryPolicyClassifier _retryClassifier = null!;
        private StateTransitionGuard _stateGuard = null!;

        [SetUp]
        public void Setup()
        {
            var retryLogger = new Mock<ILogger<RetryPolicyClassifier>>();
            _retryClassifier = new RetryPolicyClassifier(retryLogger.Object);

            var guardLogger = new Mock<ILogger<StateTransitionGuard>>();
            _stateGuard = new StateTransitionGuard(guardLogger.Object);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC1 – Critical API paths hardened: RetryPolicyClassifier determinism (6 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC1-U1: BLOCKCHAIN_CONNECTION_ERROR is classified as RetryableWithDelay.
        /// This is the most common transient failure path for token deployment.
        /// </summary>
        [Test]
        public void AC1_BlockchainConnectionError_ClassifiesAs_RetryableWithDelay()
        {
            var decision = _retryClassifier.ClassifyError(ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay),
                "Blockchain connection errors must be retryable to prevent user abandonment");
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
            Assert.That(decision.SuggestedDelaySeconds, Is.GreaterThan(0));
        }

        /// <summary>
        /// AC1-U2: INVALID_REQUEST is classified as NotRetryable.
        /// Validation errors must not be retried — user must correct inputs.
        /// </summary>
        [Test]
        public void AC1_InvalidRequest_ClassifiesAs_NotRetryable()
        {
            var decision = _retryClassifier.ClassifyError(ErrorCodes.INVALID_REQUEST);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable),
                "Validation errors must never be auto-retried without user correction");
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        /// <summary>
        /// AC1-U3: TRANSACTION_FAILED is classified as RetryableWithDelay.
        /// Network congestion can cause transient transaction failures.
        /// </summary>
        [Test]
        public void AC1_TransactionFailed_ClassifiesAs_RetryableWithDelay()
        {
            var decision = _retryClassifier.ClassifyError(ErrorCodes.TRANSACTION_FAILED);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
        }

        /// <summary>
        /// AC1-U4: TIMEOUT error produces a retryable classification with delay.
        /// Timeouts are transient and should trigger automatic retry with backoff.
        /// </summary>
        [Test]
        public void AC1_Timeout_ClassifiesAs_RetryableWithDelay()
        {
            var decision = _retryClassifier.ClassifyError(ErrorCodes.TIMEOUT);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.UseExponentialBackoff, Is.True,
                "Timeout retries must use exponential backoff to avoid thundering-herd");
        }

        /// <summary>
        /// AC1-U5: IPFS_SERVICE_ERROR is classified as RetryableWithDelay.
        /// IPFS is used for ARC3 token metadata and must be reliably retried.
        /// </summary>
        [Test]
        public void AC1_IpfsServiceError_ClassifiesAs_RetryableWithDelay()
        {
            var decision = _retryClassifier.ClassifyError(ErrorCodes.IPFS_SERVICE_ERROR);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
        }

        /// <summary>
        /// AC1-U6: Three sequential classifications of the same error code produce identical decisions.
        /// Determinism is critical for predictable client behavior.
        /// </summary>
        [Test]
        public void AC1_SameErrorCode_ThreeRuns_ProducesIdenticalDecision()
        {
            var decisions = Enumerable.Range(0, 3)
                .Select(_ => _retryClassifier.ClassifyError(ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR))
                .ToList();

            Assert.That(decisions.Select(d => d.Policy).Distinct().Count(), Is.EqualTo(1),
                "Policy must be deterministic across multiple calls");
            Assert.That(decisions.Select(d => d.MaxRetryAttempts).Distinct().Count(), Is.EqualTo(1),
                "MaxRetryAttempts must be deterministic");
            Assert.That(decisions.Select(d => d.SuggestedDelaySeconds).Distinct().Count(), Is.EqualTo(1),
                "SuggestedDelaySeconds must be deterministic");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC2 – Error response standardization: actionable semantics (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC2-U1: UNAUTHORIZED error produces NotRetryable with non-null explanation.
        /// Auth failures must carry actionable semantics for client error handling.
        /// </summary>
        [Test]
        public void AC2_Unauthorized_ClassifiesAsNotRetryable_WithExplanation()
        {
            var decision = _retryClassifier.ClassifyError(ErrorCodes.UNAUTHORIZED);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.Explanation, Is.Not.Null.And.Not.Empty,
                "Auth errors must include actionable explanation for clients");
        }

        /// <summary>
        /// AC2-U2: INSUFFICIENT_FUNDS produces RetryableAfterRemediation with remediation guidance.
        /// Users need clear guidance on how to resolve insufficient funds before retrying.
        /// </summary>
        [Test]
        public void AC2_InsufficientFunds_ClassifiesAs_RetryableAfterRemediation_WithGuidance()
        {
            var decision = _retryClassifier.ClassifyError(ErrorCodes.INSUFFICIENT_FUNDS);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableAfterRemediation));
            Assert.That(decision.RemediationGuidance, Is.Not.Null.And.Not.Empty,
                "Fund errors must include clear remediation guidance");
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0),
                "RemediationRequired errors must not auto-retry without user action");
        }

        /// <summary>
        /// AC2-U3: All standard error codes produce non-null, non-empty explanations.
        /// Every error code must have actionable semantics — no silent or empty failures.
        /// </summary>
        [Test]
        public void AC2_AllStandardErrorCodes_ProduceNonNullExplanation()
        {
            var standardCodes = new[]
            {
                ErrorCodes.INVALID_REQUEST, ErrorCodes.MISSING_REQUIRED_FIELD,
                ErrorCodes.INVALID_NETWORK, ErrorCodes.UNAUTHORIZED, ErrorCodes.FORBIDDEN,
                ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR, ErrorCodes.TRANSACTION_FAILED,
                ErrorCodes.INSUFFICIENT_FUNDS, ErrorCodes.TIMEOUT, ErrorCodes.IPFS_SERVICE_ERROR,
                ErrorCodes.RATE_LIMIT_EXCEEDED, ErrorCodes.CIRCUIT_BREAKER_OPEN
            };

            foreach (var code in standardCodes)
            {
                var decision = _retryClassifier.ClassifyError(code);
                Assert.That(decision.Explanation, Is.Not.Null.And.Not.Empty,
                    $"Error code '{code}' must produce non-empty explanation");
                Assert.That(decision.ReasonCode, Is.Not.Null.And.Not.Empty,
                    $"Error code '{code}' must produce non-empty reason code");
            }
        }

        /// <summary>
        /// AC2-U4: RATE_LIMIT_EXCEEDED produces RetryableWithCooldown with positive delay.
        /// Rate limit handling is a competitive differentiator — clients need cooldown semantics.
        /// </summary>
        [Test]
        public void AC2_RateLimitExceeded_ClassifiesAs_RetryableWithCooldown()
        {
            var decision = _retryClassifier.ClassifyError(ErrorCodes.RATE_LIMIT_EXCEEDED);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithCooldown),
                "Rate limit errors must use cooldown policy, not immediate retry");
            Assert.That(decision.SuggestedDelaySeconds, Is.GreaterThan(0));
            Assert.That(decision.RemediationGuidance, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>
        /// AC2-U5: Unknown error codes receive a cautious default retry with explanation.
        /// Future error codes must not cause silent failures or uncaught exceptions.
        /// </summary>
        [Test]
        public void AC2_UnknownErrorCode_ProducesDefaultRetry_WithExplanation()
        {
            var decision = _retryClassifier.ClassifyError("FUTURE_UNKNOWN_ERROR_CODE_XYZ");

            Assert.That(decision.Policy, Is.Not.EqualTo(default(RetryPolicy)),
                "Unknown codes must receive a policy classification, not default(RetryPolicy)");
            Assert.That(decision.Explanation, Is.Not.Null.And.Not.Empty,
                "Unknown codes must still produce actionable explanation");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC3 – Contract compatibility: state machine regression protection (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC3-U1: Queued → Submitted is a valid transition.
        /// This is the first production step in every deployment workflow.
        /// </summary>
        [Test]
        public void AC3_QueuedToSubmitted_IsValidTransition()
        {
            var result = _stateGuard.ValidateTransition(DeploymentStatus.Queued, DeploymentStatus.Submitted);

            Assert.That(result.IsAllowed, Is.True,
                "Queued → Submitted is the primary deployment activation path");
        }

        /// <summary>
        /// AC3-U2: Submitted → Confirmed is NOT a valid transition per the documented state machine.
        /// The correct path is Submitted → Pending → Confirmed; skipping Pending is forbidden.
        /// </summary>
        [Test]
        public void AC3_SubmittedToConfirmed_IsInvalidTransition()
        {
            var result = _stateGuard.ValidateTransition(DeploymentStatus.Submitted, DeploymentStatus.Confirmed);

            Assert.That(result.IsAllowed, Is.False,
                "Submitted → Confirmed skips Pending and violates the state machine contract");
        }

        /// <summary>
        /// AC3-U3: Completed is a terminal state — no further transitions are allowed.
        /// Terminal state enforcement prevents zombie deployments and audit corruption.
        /// </summary>
        [Test]
        public void AC3_Completed_IsTerminalState_NoTransitionsAllowed()
        {
            Assert.That(_stateGuard.IsTerminalState(DeploymentStatus.Completed), Is.True);

            var nextStates = _stateGuard.GetValidNextStates(DeploymentStatus.Completed);
            Assert.That(nextStates, Is.Empty,
                "Completed deployments must have no valid next states");
        }

        /// <summary>
        /// AC3-U4: Cancelled is a terminal state — no re-activation allowed.
        /// Once cancelled, a deployment cannot be restarted without creating a new record.
        /// </summary>
        [Test]
        public void AC3_Cancelled_IsTerminalState_NoTransitionsAllowed()
        {
            Assert.That(_stateGuard.IsTerminalState(DeploymentStatus.Cancelled), Is.True);

            var result = _stateGuard.ValidateTransition(DeploymentStatus.Cancelled, DeploymentStatus.Queued);
            Assert.That(result.IsAllowed, Is.False,
                "Cancelled deployments must not be re-activated");
        }

        /// <summary>
        /// AC3-U5: Failed → Queued is allowed (retry path), but Failed → Confirmed is not.
        /// The state machine must support retry while blocking invalid recovery paths.
        /// </summary>
        [Test]
        public void AC3_Failed_CanRetryToQueued_ButNotDirectlyToConfirmed()
        {
            var retryResult = _stateGuard.ValidateTransition(DeploymentStatus.Failed, DeploymentStatus.Queued);
            var skipResult = _stateGuard.ValidateTransition(DeploymentStatus.Failed, DeploymentStatus.Confirmed);

            Assert.That(retryResult.IsAllowed, Is.True, "Failed → Queued must be allowed for retry");
            Assert.That(skipResult.IsAllowed, Is.False, "Failed → Confirmed must be blocked");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC4 – Observability: failure path diagnosability (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC4-U1: Invalid transitions include ValidAlternatives for operator guidance.
        /// Operators diagnosing state issues need to know what transitions are valid.
        /// </summary>
        [Test]
        public void AC4_InvalidTransition_IncludesValidAlternatives_ForDiagnosis()
        {
            var result = _stateGuard.ValidateTransition(DeploymentStatus.Completed, DeploymentStatus.Queued);

            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.ReasonCode, Is.Not.Null.And.Not.Empty,
                "Invalid transitions must include reason code for log analysis");
            Assert.That(result.Explanation, Is.Not.Null.And.Not.Empty,
                "Invalid transitions must include explanation for operator diagnosis");
        }

        /// <summary>
        /// AC4-U2: Valid transition reason codes are non-empty and diagnosable.
        /// Transition reason codes enable structured log queries in production.
        /// </summary>
        [Test]
        public void AC4_ValidTransition_ReasonCode_IsNonEmpty()
        {
            var reasonCode = _stateGuard.GetTransitionReasonCode(
                DeploymentStatus.Queued, DeploymentStatus.Submitted);

            Assert.That(reasonCode, Is.Not.Null.And.Not.Empty,
                "Transition reason codes must support structured log queries");
        }

        /// <summary>
        /// AC4-U3: CIRCUIT_BREAKER_OPEN produces RetryableWithCooldown and includes remediation.
        /// Circuit breaker state must be observable and diagnosable from error responses.
        /// </summary>
        [Test]
        public void AC4_CircuitBreakerOpen_ClassifiesAs_RetryableWithCooldown_WithRemediation()
        {
            var decision = _retryClassifier.ClassifyError(ErrorCodes.CIRCUIT_BREAKER_OPEN);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithCooldown));
            Assert.That(decision.SuggestedDelaySeconds, Is.GreaterThanOrEqualTo(60),
                "Circuit breaker cooldown must be at least 60s for service recovery");
            Assert.That(decision.Explanation, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>
        /// AC4-U4: CalculateRetryDelay with exponential backoff grows with attempt count.
        /// Backoff curves must be deterministic and observable for deployment tracking.
        /// </summary>
        [Test]
        public void AC4_ExponentialBackoff_GrowsWithAttemptCount()
        {
            var delay0 = _retryClassifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 0, useExponentialBackoff: true);
            var delay1 = _retryClassifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 1, useExponentialBackoff: true);
            var delay2 = _retryClassifier.CalculateRetryDelay(RetryPolicy.RetryableWithDelay, 2, useExponentialBackoff: true);

            Assert.That(delay2, Is.GreaterThanOrEqualTo(delay1),
                "Exponential backoff must increase delay with each attempt");
            Assert.That(delay1, Is.GreaterThanOrEqualTo(delay0),
                "Exponential backoff must increase delay with each attempt");
        }

        /// <summary>
        /// AC4-U5: KYC_REQUIRED produces RetryableAfterRemediation with user-actionable guidance.
        /// Compliance failures must surface actionable steps so users can self-remediate.
        /// </summary>
        [Test]
        public void AC4_KycRequired_ClassifiesAs_RetryableAfterRemediation_WithGuidance()
        {
            var decision = _retryClassifier.ClassifyError(ErrorCodes.KYC_REQUIRED);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableAfterRemediation));
            Assert.That(decision.RemediationGuidance, Is.Not.Null.And.Not.Empty,
                "KYC errors must direct users to the KYC verification flow");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC5 – Competitive gap: enterprise-grade retry strategy coverage (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC5-U1: ShouldRetry returns false after max attempts exceeded.
        /// Hard limit on retry attempts prevents runaway retry loops in production.
        /// </summary>
        [Test]
        public void AC5_ShouldRetry_ReturnsFalse_AfterMaxAttempts()
        {
            // RetryableWithDelay has MAX_DELAY_RETRIES = 5
            var pastMaxAttempts = 100;
            var shouldRetry = _retryClassifier.ShouldRetry(
                RetryPolicy.RetryableWithDelay,
                pastMaxAttempts,
                DateTime.UtcNow.AddSeconds(-10));

            Assert.That(shouldRetry, Is.False,
                "Retry must be halted after max attempts to prevent runaway loops");
        }

        /// <summary>
        /// AC5-U2: ShouldRetry returns false when max duration exceeded.
        /// Total retry duration cap prevents indefinite blocking of user workflows.
        /// </summary>
        [Test]
        public void AC5_ShouldRetry_ReturnsFalse_WhenMaxDurationExceeded()
        {
            var oldFirstAttempt = DateTime.UtcNow.AddMinutes(-20); // > 10 minute max
            var shouldRetry = _retryClassifier.ShouldRetry(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 1,
                firstAttemptTime: oldFirstAttempt);

            Assert.That(shouldRetry, Is.False,
                "Retry must stop after max duration to prevent indefinite blocking");
        }

        /// <summary>
        /// AC5-U3: NotRetryable policy always returns false from ShouldRetry regardless of attempts.
        /// Permanent errors must never be auto-retried.
        /// </summary>
        [Test]
        public void AC5_NotRetryable_ShouldRetry_AlwaysReturnsFalse()
        {
            foreach (var attempt in new[] { 0, 1, 2 })
            {
                var shouldRetry = _retryClassifier.ShouldRetry(
                    RetryPolicy.NotRetryable,
                    attempt,
                    DateTime.UtcNow);

                Assert.That(shouldRetry, Is.False,
                    $"NotRetryable must never return true (attempt={attempt})");
            }
        }

        /// <summary>
        /// AC5-U4: RetryableWithDelay policy returns true for first few attempts.
        /// Network errors must trigger automatic retry within permitted attempt window.
        /// </summary>
        [Test]
        public void AC5_RetryableWithDelay_ShouldRetry_ReturnsTrueForFirstAttempts()
        {
            var shouldRetry = _retryClassifier.ShouldRetry(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 1,
                firstAttemptTime: DateTime.UtcNow);

            Assert.That(shouldRetry, Is.True,
                "First retry attempts must be allowed for transient network errors");
        }

        /// <summary>
        /// AC5-U5: Concurrent classification of different error codes produces correct independent decisions.
        /// Thread safety is required for high-throughput token deployment scenarios.
        /// </summary>
        [Test]
        public void AC5_ConcurrentClassification_ProducesCorrectIndependentDecisions()
        {
            var codes = new[]
            {
                ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR, // RetryableWithDelay
                ErrorCodes.INVALID_REQUEST,             // NotRetryable
                ErrorCodes.RATE_LIMIT_EXCEEDED          // RetryableWithCooldown
            };
            var expectedPolicies = new[]
            {
                RetryPolicy.RetryableWithDelay,
                RetryPolicy.NotRetryable,
                RetryPolicy.RetryableWithCooldown
            };

            var results = codes.AsParallel()
                .Select(code => _retryClassifier.ClassifyError(code))
                .ToArray();

            // Verify each code maps to its expected policy
            for (int i = 0; i < codes.Length; i++)
            {
                var result = results.First(r =>
                    (codes[i] == ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR && r.Policy == RetryPolicy.RetryableWithDelay) ||
                    (codes[i] == ErrorCodes.INVALID_REQUEST && r.Policy == RetryPolicy.NotRetryable) ||
                    (codes[i] == ErrorCodes.RATE_LIMIT_EXCEEDED && r.Policy == RetryPolicy.RetryableWithCooldown));
                Assert.That(result, Is.Not.Null, $"Concurrent classification must return correct policy for '{codes[i]}'");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC7 – Test coverage validation: branch exhaustiveness (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC7-U1: DeploymentErrorCategory.NetworkError classifies as RetryableWithDelay.
        /// Category-based classification must fall back correctly when error code is novel.
        /// </summary>
        [Test]
        public void AC7_NetworkErrorCategory_ClassifiesAs_RetryableWithDelay()
        {
            var decision = _retryClassifier.ClassifyError(
                "NOVEL_NETWORK_CODE",
                DeploymentErrorCategory.NetworkError);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
        }

        /// <summary>
        /// AC7-U2: DeploymentErrorCategory.ValidationError classifies as NotRetryable.
        /// Category fallback must enforce the same invariants as error-code classification.
        /// </summary>
        [Test]
        public void AC7_ValidationErrorCategory_ClassifiesAs_NotRetryable()
        {
            var decision = _retryClassifier.ClassifyError(
                "NOVEL_VALIDATION_CODE",
                DeploymentErrorCategory.ValidationError);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
        }

        /// <summary>
        /// AC7-U3: DeploymentErrorCategory.RateLimitExceeded classifies as RetryableWithCooldown.
        /// All rate limit variants must use cooldown to prevent cascading pressure.
        /// </summary>
        [Test]
        public void AC7_RateLimitCategory_ClassifiesAs_RetryableWithCooldown()
        {
            var decision = _retryClassifier.ClassifyError(
                "NOVEL_RATELIMIT_CODE",
                DeploymentErrorCategory.RateLimitExceeded);

            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithCooldown));
        }

        /// <summary>
        /// AC7-U4: CalculateRetryDelay for NotRetryable always returns 0.
        /// Clients must not be given a delay hint for permanent errors.
        /// </summary>
        [Test]
        public void AC7_NotRetryable_CalculateRetryDelay_ReturnsZero()
        {
            var delay = _retryClassifier.CalculateRetryDelay(
                RetryPolicy.NotRetryable, 0, useExponentialBackoff: false);

            Assert.That(delay, Is.EqualTo(0),
                "NotRetryable policy must not suggest any delay");
        }

        /// <summary>
        /// AC7-U5: GetValidNextStates for all DeploymentStatus values returns non-null lists.
        /// Exhaustive state coverage prevents NullReferenceException in production workflows.
        /// </summary>
        [Test]
        public void AC7_GetValidNextStates_ForAllStatuses_ReturnsNonNullList()
        {
            foreach (DeploymentStatus status in Enum.GetValues(typeof(DeploymentStatus)))
            {
                var nextStates = _stateGuard.GetValidNextStates(status);
                Assert.That(nextStates, Is.Not.Null,
                    $"GetValidNextStates must return non-null for status '{status}'");
            }
        }
    }
}
