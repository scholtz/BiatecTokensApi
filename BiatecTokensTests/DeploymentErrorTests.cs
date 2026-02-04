using BiatecTokensApi.Models;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for Deployment Error categorization and handling
    /// </summary>
    [TestFixture]
    public class DeploymentErrorTests
    {
        [Test]
        public void NetworkError_ShouldCreateCorrectError()
        {
            // Act
            var error = DeploymentErrorFactory.NetworkError("Connection timeout", "RPC endpoint unavailable");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(error.Category, Is.EqualTo(DeploymentErrorCategory.NetworkError));
                Assert.That(error.ErrorCode, Is.EqualTo(ErrorCodes.BLOCKCHAIN_CONNECTION_ERROR));
                Assert.That(error.IsRetryable, Is.True);
                Assert.That(error.SuggestedRetryDelaySeconds, Is.EqualTo(30));
                Assert.That(error.UserMessage, Does.Contain("blockchain network"));
                Assert.That(error.TechnicalMessage, Does.Contain("Connection timeout"));
            });
        }

        [Test]
        public void ValidationError_ShouldCreateNonRetryableError()
        {
            // Act
            var error = DeploymentErrorFactory.ValidationError(
                "Invalid token supply",
                "Token supply must be greater than zero");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(error.Category, Is.EqualTo(DeploymentErrorCategory.ValidationError));
                Assert.That(error.ErrorCode, Is.EqualTo(ErrorCodes.INVALID_REQUEST));
                Assert.That(error.IsRetryable, Is.False);
                Assert.That(error.UserMessage, Does.Contain("greater than zero"));
            });
        }

        [Test]
        public void ComplianceError_ShouldCreateCorrectError()
        {
            // Act
            var error = DeploymentErrorFactory.ComplianceError(
                "KYC verification failed",
                "Your account requires KYC verification to deploy tokens");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(error.Category, Is.EqualTo(DeploymentErrorCategory.ComplianceError));
                Assert.That(error.ErrorCode, Is.EqualTo(ErrorCodes.FORBIDDEN));
                Assert.That(error.IsRetryable, Is.False);
                Assert.That(error.UserMessage, Does.Contain("KYC verification"));
            });
        }

        [Test]
        public void UserRejection_ShouldCreateRetryableError()
        {
            // Act
            var error = DeploymentErrorFactory.UserRejection("User declined transaction");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(error.Category, Is.EqualTo(DeploymentErrorCategory.UserRejection));
                Assert.That(error.ErrorCode, Is.EqualTo(ErrorCodes.TRANSACTION_REJECTED));
                Assert.That(error.IsRetryable, Is.True);
                Assert.That(error.UserMessage, Does.Contain("cancelled"));
            });
        }

        [Test]
        public void InsufficientFunds_ShouldIncludeAmounts()
        {
            // Act
            var error = DeploymentErrorFactory.InsufficientFunds("100 ALGO", "50 ALGO");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(error.Category, Is.EqualTo(DeploymentErrorCategory.InsufficientFunds));
                Assert.That(error.ErrorCode, Is.EqualTo(ErrorCodes.INSUFFICIENT_FUNDS));
                Assert.That(error.IsRetryable, Is.True);
                Assert.That(error.Context, Is.Not.Null);
                Assert.That(error.Context!.ContainsKey("required"), Is.True);
                Assert.That(error.Context!["required"], Is.EqualTo("100 ALGO"));
                Assert.That(error.Context!["available"], Is.EqualTo("50 ALGO"));
            });
        }

        [Test]
        public void TransactionFailure_ShouldIncludeTransactionHash()
        {
            // Act
            var txHash = "0x123abc";
            var error = DeploymentErrorFactory.TransactionFailure("Transaction reverted", txHash);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(error.Category, Is.EqualTo(DeploymentErrorCategory.TransactionFailure));
                Assert.That(error.ErrorCode, Is.EqualTo(ErrorCodes.TRANSACTION_FAILED));
                Assert.That(error.IsRetryable, Is.True);
                Assert.That(error.Context, Is.Not.Null);
                Assert.That(error.Context!.ContainsKey("transactionHash"), Is.True);
                Assert.That(error.Context!["transactionHash"], Is.EqualTo(txHash));
            });
        }

        [Test]
        public void ConfigurationError_ShouldNotBeRetryable()
        {
            // Act
            var error = DeploymentErrorFactory.ConfigurationError("Missing RPC endpoint configuration");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(error.Category, Is.EqualTo(DeploymentErrorCategory.ConfigurationError));
                Assert.That(error.ErrorCode, Is.EqualTo(ErrorCodes.CONFIGURATION_ERROR));
                Assert.That(error.IsRetryable, Is.False);
                Assert.That(error.UserMessage, Does.Contain("contact support"));
            });
        }

        [Test]
        public void RateLimitError_ShouldIncludeRetryDelay()
        {
            // Act
            var retryAfter = 60;
            var error = DeploymentErrorFactory.RateLimitError(retryAfter);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(error.Category, Is.EqualTo(DeploymentErrorCategory.RateLimitExceeded));
                Assert.That(error.ErrorCode, Is.EqualTo(ErrorCodes.RATE_LIMIT_EXCEEDED));
                Assert.That(error.IsRetryable, Is.True);
                Assert.That(error.SuggestedRetryDelaySeconds, Is.EqualTo(retryAfter));
                Assert.That(error.UserMessage, Does.Contain("60 seconds"));
            });
        }

        [Test]
        public void InternalError_ShouldBeRetryable()
        {
            // Act
            var error = DeploymentErrorFactory.InternalError("Unexpected null reference");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(error.Category, Is.EqualTo(DeploymentErrorCategory.InternalError));
                Assert.That(error.ErrorCode, Is.EqualTo(ErrorCodes.INTERNAL_SERVER_ERROR));
                Assert.That(error.IsRetryable, Is.True);
                Assert.That(error.SuggestedRetryDelaySeconds, Is.EqualTo(120));
                Assert.That(error.UserMessage, Does.Contain("unexpected error"));
            });
        }

        [Test]
        public void AllErrors_ShouldHaveTimestamp()
        {
            // Arrange
            var before = DateTime.UtcNow;

            // Act
            var error = DeploymentErrorFactory.NetworkError("Test error");

            // Assert
            var after = DateTime.UtcNow;
            Assert.That(error.Timestamp, Is.GreaterThanOrEqualTo(before));
            Assert.That(error.Timestamp, Is.LessThanOrEqualTo(after));
        }
    }
}
