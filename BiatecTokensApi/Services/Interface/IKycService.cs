using BiatecTokensApi.Models.Kyc;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for KYC operations
    /// </summary>
    public interface IKycService
    {
        /// <summary>
        /// Starts KYC verification for a user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="request">The verification request</param>
        /// <param name="correlationId">Correlation ID for tracking</param>
        /// <returns>The verification start response</returns>
        Task<StartKycVerificationResponse> StartVerificationAsync(
            string userId,
            StartKycVerificationRequest request,
            string correlationId);

        /// <summary>
        /// Gets the current KYC status for a user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>The KYC status response</returns>
        Task<KycStatusResponse> GetStatusAsync(string userId);

        /// <summary>
        /// Handles a webhook notification from the KYC provider
        /// </summary>
        /// <param name="payload">The webhook payload</param>
        /// <param name="signature">The webhook signature</param>
        /// <returns>True if webhook was processed successfully</returns>
        Task<bool> HandleWebhookAsync(KycWebhookPayload payload, string? signature);

        /// <summary>
        /// Checks if a user is KYC verified and approved
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>True if user is verified and approved, false otherwise</returns>
        Task<bool> IsUserVerifiedAsync(string userId);

        /// <summary>
        /// Checks if KYC enforcement is enabled
        /// </summary>
        /// <returns>True if enforcement is enabled, false otherwise</returns>
        bool IsEnforcementEnabled();
    }
}
