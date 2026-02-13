using BiatecTokensApi.Models.Kyc;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for KYC provider implementations
    /// </summary>
    public interface IKycProvider
    {
        /// <summary>
        /// Starts a new KYC verification session with the provider
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="request">The verification request</param>
        /// <param name="correlationId">Correlation ID for tracking</param>
        /// <returns>Provider reference ID and initial status</returns>
        Task<(string providerReferenceId, KycStatus status, string? errorMessage)> StartVerificationAsync(
            string userId,
            StartKycVerificationRequest request,
            string correlationId);

        /// <summary>
        /// Fetches the current status of a verification from the provider
        /// </summary>
        /// <param name="providerReferenceId">The provider reference ID</param>
        /// <returns>Current status and optional reason</returns>
        Task<(KycStatus status, string? reason, string? errorMessage)> FetchStatusAsync(string providerReferenceId);

        /// <summary>
        /// Validates a webhook signature from the provider
        /// </summary>
        /// <param name="payload">The webhook payload</param>
        /// <param name="signature">The signature to validate</param>
        /// <param name="webhookSecret">The webhook secret</param>
        /// <returns>True if signature is valid, false otherwise</returns>
        bool ValidateWebhookSignature(string payload, string signature, string webhookSecret);

        /// <summary>
        /// Parses a webhook payload into a KYC status update
        /// </summary>
        /// <param name="payload">The webhook payload</param>
        /// <returns>Parsed webhook data</returns>
        Task<(string providerReferenceId, KycStatus status, string? reason)> ParseWebhookAsync(KycWebhookPayload payload);
    }
}
