using BiatecTokensApi.Models;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for classifying errors into retry policies
    /// </summary>
    public interface IRetryPolicyClassifier
    {
        /// <summary>
        /// Classifies an error code into a retry policy
        /// </summary>
        /// <param name="errorCode">The error code to classify</param>
        /// <param name="errorCategory">The error category (optional)</param>
        /// <param name="context">Additional context about the error (optional)</param>
        /// <returns>A retry policy decision with guidance</returns>
        RetryPolicyDecision ClassifyError(
            string errorCode,
            DeploymentErrorCategory? errorCategory = null,
            Dictionary<string, object>? context = null);

        /// <summary>
        /// Determines if a retry should be attempted based on attempt history
        /// </summary>
        /// <param name="policy">The retry policy</param>
        /// <param name="attemptCount">Number of retry attempts already made</param>
        /// <param name="firstAttemptTime">Timestamp of first attempt</param>
        /// <returns>True if retry should be attempted, false otherwise</returns>
        bool ShouldRetry(RetryPolicy policy, int attemptCount, DateTime firstAttemptTime);

        /// <summary>
        /// Calculates the recommended delay before next retry
        /// </summary>
        /// <param name="policy">The retry policy</param>
        /// <param name="attemptCount">Number of retry attempts already made</param>
        /// <param name="useExponentialBackoff">Whether to use exponential backoff</param>
        /// <returns>Delay in seconds before next retry</returns>
        int CalculateRetryDelay(RetryPolicy policy, int attemptCount, bool useExponentialBackoff);
    }
}
