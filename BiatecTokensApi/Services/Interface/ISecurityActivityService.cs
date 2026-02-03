using BiatecTokensApi.Models;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for security activity and audit operations
    /// </summary>
    public interface ISecurityActivityService
    {
        /// <summary>
        /// Logs a security activity event
        /// </summary>
        /// <param name="event">The security event to log</param>
        /// <returns>Task representing the async operation</returns>
        Task LogEventAsync(SecurityActivityEvent @event);

        /// <summary>
        /// Gets security activity events with filtering and pagination
        /// </summary>
        /// <param name="request">Filter and pagination parameters</param>
        /// <returns>Response containing security activity events</returns>
        Task<SecurityActivityResponse> GetActivityAsync(GetSecurityActivityRequest request);

        /// <summary>
        /// Gets token deployment transaction history
        /// </summary>
        /// <param name="request">Filter and pagination parameters</param>
        /// <returns>Response containing transaction history</returns>
        Task<TransactionHistoryResponse> GetTransactionHistoryAsync(GetTransactionHistoryRequest request);

        /// <summary>
        /// Exports audit trail in specified format (CSV or JSON)
        /// </summary>
        /// <param name="request">Export parameters including format and filters</param>
        /// <param name="accountId">Account ID requesting the export</param>
        /// <returns>Response containing export details and content</returns>
        Task<(ExportAuditTrailResponse Response, string? Content)> ExportAuditTrailAsync(ExportAuditTrailRequest request, string accountId);

        /// <summary>
        /// Gets recovery guidance for the account
        /// </summary>
        /// <param name="accountId">Account ID requesting recovery guidance</param>
        /// <returns>Response containing recovery guidance and eligibility</returns>
        Task<RecoveryGuidanceResponse> GetRecoveryGuidanceAsync(string accountId);
    }
}
