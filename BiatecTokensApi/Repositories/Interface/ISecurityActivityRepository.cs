using BiatecTokensApi.Models;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for security activity and audit operations
    /// </summary>
    public interface ISecurityActivityRepository
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
        /// <returns>List of security activity events</returns>
        Task<List<SecurityActivityEvent>> GetActivityEventsAsync(GetSecurityActivityRequest request);

        /// <summary>
        /// Gets count of security activity events matching the filter
        /// </summary>
        /// <param name="request">Filter parameters</param>
        /// <returns>Total count of matching events</returns>
        Task<int> GetActivityEventsCountAsync(GetSecurityActivityRequest request);

        /// <summary>
        /// Gets token deployment transaction history
        /// </summary>
        /// <param name="request">Filter and pagination parameters</param>
        /// <returns>List of token deployment transactions</returns>
        Task<List<TokenDeploymentTransaction>> GetTransactionHistoryAsync(GetTransactionHistoryRequest request);

        /// <summary>
        /// Gets count of token deployment transactions matching the filter
        /// </summary>
        /// <param name="request">Filter parameters</param>
        /// <returns>Total count of matching transactions</returns>
        Task<int> GetTransactionHistoryCountAsync(GetTransactionHistoryRequest request);

        /// <summary>
        /// Checks if an export with the given idempotency key exists
        /// </summary>
        /// <param name="idempotencyKey">The idempotency key</param>
        /// <param name="accountId">The account ID</param>
        /// <param name="request">The current export request to validate against cached</param>
        /// <returns>Cached export response if exists and matches, null otherwise</returns>
        Task<ExportAuditTrailResponse?> GetCachedExportAsync(string idempotencyKey, string accountId, ExportAuditTrailRequest request);

        /// <summary>
        /// Caches an export response with idempotency key
        /// </summary>
        /// <param name="idempotencyKey">The idempotency key</param>
        /// <param name="accountId">The account ID</param>
        /// <param name="request">The export request</param>
        /// <param name="response">The export response to cache</param>
        /// <param name="expirationHours">Cache expiration in hours (default: 24)</param>
        /// <returns>Task representing the async operation</returns>
        Task CacheExportAsync(string idempotencyKey, string accountId, ExportAuditTrailRequest request, ExportAuditTrailResponse response, int expirationHours = 24);
    }
}
