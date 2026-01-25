using BiatecTokensApi.Models;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for token issuance audit log operations
    /// </summary>
    /// <remarks>
    /// Provides data access for token issuance audit logs across all token standards
    /// (ERC20, ASA, ARC3, ARC200, ARC1400) for MICA reporting and regulatory compliance.
    /// </remarks>
    public interface ITokenIssuanceRepository
    {
        /// <summary>
        /// Adds a token issuance audit log entry
        /// </summary>
        /// <param name="entry">The audit log entry to add</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task AddAuditLogEntryAsync(TokenIssuanceAuditLogEntry entry);

        /// <summary>
        /// Gets token issuance audit log entries with filtering
        /// </summary>
        /// <param name="request">The audit log request with filters</param>
        /// <returns>List of token issuance audit log entries ordered by most recent first</returns>
        Task<List<TokenIssuanceAuditLogEntry>> GetAuditLogAsync(GetTokenIssuanceAuditLogRequest request);

        /// <summary>
        /// Gets the total count of token issuance audit log entries matching the filter
        /// </summary>
        /// <param name="request">The audit log request with filters</param>
        /// <returns>Total count of matching entries</returns>
        Task<int> GetAuditLogCountAsync(GetTokenIssuanceAuditLogRequest request);
    }
}
