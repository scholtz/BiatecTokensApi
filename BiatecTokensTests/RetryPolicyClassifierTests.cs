using BiatecTokensApi.Models;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for RetryPolicyClassifier service
    /// 
    /// Validates that error classification produces deterministic retry policies
    /// with correct guidance for each error scenario.
    /// 
    /// Business Value: Ensures platform provides consistent retry behavior across
    /// all error scenarios, improving user experience and reducing support burden.
    /// </summary>
    [TestFixture]
    public class RetryPolicyClassifierTests
    {
        private RetryPolicyClassifier _classifier = null!;
        private Mock<ILogger<RetryPolicyClassifier>> _loggerMock = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<RetryPolicyClassifier>>();
            _classifier = new RetryPolicyClassifier(_loggerMock.Object);
        }

        #region Error Classification Tests

        [Test]
        public void ClassifyError_ValidationError_ShouldBeNotRetryable()
        {
            // Act
            var decision = _classifier.ClassifyError(ErrorCodes.INVALID_REQUEST);

            // Assert
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
            Assert.That(decision.Explanation, Does.Contain("user must correct"));
        }

        [Test]
        public void ClassifyError_NetworkError_ShouldBeRetryableWithDelay()
        {
            // Act
            var decision = _classifier.ClassifyError(ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR);

            // Assert
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
            Assert.That(decision.SuggestedDelaySeconds, Is.GreaterThan(0));
            Assert.That(decision.UseExponentialBackoff, Is.True);
        }

        [Test]
        public void ClassifyError_RateLimit_ShouldBeRetryableWithCooldown()
        {
            // Act
            var decision = _classifier.ClassifyError(ErrorCodes.RATE_LIMIT_EXCEEDED);

            // Assert
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithCooldown));
            Assert.That(decision.SuggestedDelaySeconds, Is.GreaterThanOrEqualTo(60));
            Assert.That(decision.RemediationGuidance, Is.Not.Null);
        }

        [Test]
        public void ClassifyError_InsufficientFunds_ShouldBeRetryableAfterRemediation()
        {
            // Act
            var decision = _classifier.ClassifyError(ErrorCodes.INSUFFICIENT_FUNDS);

            // Assert
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableAfterRemediation));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
            Assert.That(decision.RemediationGuidance, Does.Contain("Add"));
        }

        [Test]
        public void ClassifyError_ConfigurationError_ShouldBeRetryableAfterConfiguration()
        {
            // Act
            var decision = _classifier.ClassifyError(ErrorCodes.CONFIGURATION_ERROR);

            // Assert
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableAfterConfiguration));
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
            Assert.That(decision.RemediationGuidance, Does.Contain("support"));
        }

        [Test]
        public void ClassifyError_UnknownCode_ShouldDefaultToRetryableWithDelay()
        {
            // Act
            var decision = _classifier.ClassifyError("UNKNOWN_ERROR_CODE_12345");

            // Assert
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.ReasonCode, Is.EqualTo("UNKNOWN_ERROR"));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThan(0));
        }

        [Test]
        public void ClassifyError_WithCategory_ShouldClassifyByCategory()
        {
            // Act
            var decision = _classifier.ClassifyError(
                "CUSTOM_ERROR",
                DeploymentErrorCategory.NetworkError);

            // Assert
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
        }

        [Test]
        public void ClassifyError_IPFSError_ShouldHaveAppropriateDelay()
        {
            // Act
            var decision = _classifier.ClassifyError(ErrorCodes.IPFS_SERVICE_ERROR);

            // Assert
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.SuggestedDelaySeconds, Is.GreaterThan(0));
            Assert.That(decision.MaxRetryAttempts, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void ClassifyError_CircuitBreakerOpen_ShouldHaveLongCooldown()
        {
            // Act
            var decision = _classifier.ClassifyError(ErrorCodes.CIRCUIT_BREAKER_OPEN);

            // Assert
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithCooldown));
            Assert.That(decision.SuggestedDelaySeconds, Is.GreaterThanOrEqualTo(60));
        }

        [Test]
        public void ClassifyError_KYCRequired_ShouldRequireRemediation()
        {
            // Act
            var decision = _classifier.ClassifyError(ErrorCodes.KYC_REQUIRED);

            // Assert
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableAfterRemediation));
            Assert.That(decision.RemediationGuidance, Does.Contain("KYC"));
        }

        #endregion

        #region Retry Decision Tests

        [Test]
        public void ShouldRetry_NotRetryable_ShouldReturnFalse()
        {
            // Act
            var shouldRetry = _classifier.ShouldRetry(
                RetryPolicy.NotRetryable,
                attemptCount: 0,
                firstAttemptTime: DateTime.UtcNow);

            // Assert
            Assert.That(shouldRetry, Is.False);
        }

        [Test]
        public void ShouldRetry_RetryableWithDelay_WithinLimit_ShouldReturnTrue()
        {
            // Act
            var shouldRetry = _classifier.ShouldRetry(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 2,
                firstAttemptTime: DateTime.UtcNow.AddSeconds(-30));

            // Assert
            Assert.That(shouldRetry, Is.True);
        }

        [Test]
        public void ShouldRetry_RetryableWithDelay_ExceedsLimit_ShouldReturnFalse()
        {
            // Act
            var shouldRetry = _classifier.ShouldRetry(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 10,
                firstAttemptTime: DateTime.UtcNow.AddSeconds(-30));

            // Assert
            Assert.That(shouldRetry, Is.False);
        }

        [Test]
        public void ShouldRetry_ExceedsMaxDuration_ShouldReturnFalse()
        {
            // Act
            var shouldRetry = _classifier.ShouldRetry(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 1,
                firstAttemptTime: DateTime.UtcNow.AddSeconds(-700)); // Over 10 minutes

            // Assert
            Assert.That(shouldRetry, Is.False);
        }

        [Test]
        public void ShouldRetry_RetryableAfterRemediation_ShouldReturnFalse()
        {
            // User must take action before retry
            // Act
            var shouldRetry = _classifier.ShouldRetry(
                RetryPolicy.RetryableAfterRemediation,
                attemptCount: 0,
                firstAttemptTime: DateTime.UtcNow);

            // Assert
            Assert.That(shouldRetry, Is.False);
        }

        [Test]
        public void ShouldRetry_RetryableImmediate_WithinLimit_ShouldReturnTrue()
        {
            // Act
            var shouldRetry = _classifier.ShouldRetry(
                RetryPolicy.RetryableImmediate,
                attemptCount: 1,
                firstAttemptTime: DateTime.UtcNow);

            // Assert
            Assert.That(shouldRetry, Is.True);
        }

        #endregion

        #region Retry Delay Calculation Tests

        [Test]
        public void CalculateRetryDelay_RetryableImmediate_ShouldReturnZero()
        {
            // Act
            var delay = _classifier.CalculateRetryDelay(
                RetryPolicy.RetryableImmediate,
                attemptCount: 1,
                useExponentialBackoff: false);

            // Assert
            Assert.That(delay, Is.EqualTo(0));
        }

        [Test]
        public void CalculateRetryDelay_RetryableWithDelay_NoBackoff_ShouldReturnBaseDelay()
        {
            // Act
            var delay = _classifier.CalculateRetryDelay(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 1,
                useExponentialBackoff: false);

            // Assert
            Assert.That(delay, Is.GreaterThan(0));
            Assert.That(delay, Is.LessThan(30)); // Should be base delay
        }

        [Test]
        public void CalculateRetryDelay_WithExponentialBackoff_ShouldIncrease()
        {
            // Act
            var delay1 = _classifier.CalculateRetryDelay(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 1,
                useExponentialBackoff: true);

            var delay2 = _classifier.CalculateRetryDelay(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 2,
                useExponentialBackoff: true);

            var delay3 = _classifier.CalculateRetryDelay(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 3,
                useExponentialBackoff: true);

            // Assert
            Assert.That(delay2, Is.GreaterThan(delay1));
            Assert.That(delay3, Is.GreaterThan(delay2));
        }

        [Test]
        public void CalculateRetryDelay_WithExponentialBackoff_ShouldCap()
        {
            // Act
            var delay = _classifier.CalculateRetryDelay(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 10, // Very high attempt count
                useExponentialBackoff: true);

            // Assert
            Assert.That(delay, Is.LessThanOrEqualTo(300)); // Capped at 5 minutes
        }

        [Test]
        public void CalculateRetryDelay_RetryableWithCooldown_ShouldReturnLongerDelay()
        {
            // Act
            var delayWithDelay = _classifier.CalculateRetryDelay(
                RetryPolicy.RetryableWithDelay,
                attemptCount: 1,
                useExponentialBackoff: false);

            var delayWithCooldown = _classifier.CalculateRetryDelay(
                RetryPolicy.RetryableWithCooldown,
                attemptCount: 1,
                useExponentialBackoff: false);

            // Assert
            Assert.That(delayWithCooldown, Is.GreaterThan(delayWithDelay));
        }

        #endregion

        #region Integration Tests

        [Test]
        public void FullRetryFlow_NetworkError_ShouldFollowExpectedPattern()
        {
            // Arrange
            var firstAttemptTime = DateTime.UtcNow;

            // Act - Classify error
            var decision = _classifier.ClassifyError(ErrorCodes.TIMEOUT);

            // Assert initial classification
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.RetryableWithDelay));
            Assert.That(decision.UseExponentialBackoff, Is.True);

            // Act - Check retry attempts
            for (int attempt = 0; attempt < decision.MaxRetryAttempts!.Value; attempt++)
            {
                var shouldRetry = _classifier.ShouldRetry(decision.Policy, attempt, firstAttemptTime);
                Assert.That(shouldRetry, Is.True, $"Attempt {attempt} should allow retry");

                var delay = _classifier.CalculateRetryDelay(decision.Policy, attempt, decision.UseExponentialBackoff);
                Assert.That(delay, Is.GreaterThanOrEqualTo(0), $"Delay for attempt {attempt} should be non-negative");
            }

            // Act - Check that exceeding max attempts blocks retry
            var shouldRetryAfterMax = _classifier.ShouldRetry(
                decision.Policy,
                decision.MaxRetryAttempts.Value + 1,
                firstAttemptTime);

            Assert.That(shouldRetryAfterMax, Is.False, "Should not retry after max attempts exceeded");
        }

        [Test]
        public void FullRetryFlow_ValidationError_ShouldNeverRetry()
        {
            // Arrange
            var firstAttemptTime = DateTime.UtcNow;

            // Act
            var decision = _classifier.ClassifyError(ErrorCodes.INVALID_REQUEST);
            var shouldRetry = _classifier.ShouldRetry(decision.Policy, 0, firstAttemptTime);

            // Assert
            Assert.That(decision.Policy, Is.EqualTo(RetryPolicy.NotRetryable));
            Assert.That(shouldRetry, Is.False);
            Assert.That(decision.MaxRetryAttempts, Is.EqualTo(0));
        }

        #endregion
    }
}
