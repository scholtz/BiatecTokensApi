using BiatecTokensApi.Models;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for enterprise audit log operations
    /// </summary>
    /// <remarks>
    /// Provides unified access to audit logs across whitelist/blacklist and compliance systems
    /// for MICA reporting and regulatory compliance.
    /// </remarks>
    public interface IEnterpriseAuditRepository
    {
        /// <summary>
        /// Gets unified audit log entries from all systems with comprehensive filtering
        /// </summary>
        /// <param name="request">The audit log request with filters</param>
        /// <returns>List of unified audit log entries ordered by most recent first</returns>
        Task<List<EnterpriseAuditLogEntry>> GetAuditLogAsync(GetEnterpriseAuditLogRequest request);

        /// <summary>
        /// Gets the total count of audit log entries matching the filter
        /// </summary>
        /// <param name="request">The audit log request with filters</param>
        /// <returns>Total count of matching entries</returns>
        Task<int> GetAuditLogCountAsync(GetEnterpriseAuditLogRequest request);

        /// <summary>
        /// Gets summary statistics for audit log entries matching the filter
        /// </summary>
        /// <param name="request">The audit log request with filters</param>
        /// <returns>Summary statistics including event counts and date ranges</returns>
        Task<AuditLogSummary> GetAuditLogSummaryAsync(GetEnterpriseAuditLogRequest request);
    }
}
