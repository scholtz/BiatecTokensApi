using BiatecTokensApi.Models.Aml;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for AML (Anti-Money Laundering) screening operations
    /// </summary>
    public interface IAmlService
    {
        /// <summary>
        /// Screens a user against sanctions, PEP, and AML lists.
        /// Creates or updates the AML record for the user.
        /// </summary>
        /// <param name="userId">User ID to screen</param>
        /// <param name="metadata">Additional metadata to pass to the provider</param>
        /// <param name="correlationId">Correlation ID for tracing</param>
        /// <returns>The screening response</returns>
        Task<AmlScreenResponse> ScreenUserAsync(
            string userId,
            Dictionary<string, string> metadata,
            string correlationId);

        /// <summary>
        /// Returns the current AML screening status for a user.
        /// </summary>
        /// <param name="userId">User ID to query</param>
        /// <returns>The AML status response</returns>
        Task<AmlStatusResponse> GetStatusAsync(string userId);

        /// <summary>
        /// Handles an inbound webhook from the AML continuous-monitoring provider.
        /// </summary>
        /// <param name="payload">The parsed webhook payload</param>
        /// <param name="signature">HMAC signature from the provider header (may be null)</param>
        /// <returns>True if the webhook was processed successfully</returns>
        Task<bool> HandleWebhookAsync(AmlWebhookPayload payload, string? signature);

        /// <summary>
        /// Generates a full AML compliance report for a user, including all historical records.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>The AML compliance report</returns>
        Task<AmlReportResponse> GenerateReportAsync(string userId);

        /// <summary>
        /// Checks whether a user has passed AML screening (cleared, low risk).
        /// </summary>
        /// <param name="userId">User ID to check</param>
        /// <returns>True if the user is AML-cleared</returns>
        Task<bool> IsUserClearedAsync(string userId);

        /// <summary>
        /// Anonymizes AML PII for a user as part of GDPR erasure, retaining the audit reference.
        /// </summary>
        /// <param name="userId">User ID to anonymize</param>
        /// <returns>Number of records anonymized</returns>
        Task<int> AnonymizeUserAmlDataAsync(string userId);
    }
}
