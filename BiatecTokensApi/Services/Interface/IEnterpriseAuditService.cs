using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for enterprise audit log operations
    /// </summary>
    /// <remarks>
    /// Provides business logic for retrieving and exporting unified audit logs
    /// across whitelist/blacklist and compliance systems for MICA reporting.
    /// </remarks>
    public interface IEnterpriseAuditService
    {
        /// <summary>
        /// Gets unified audit log entries with comprehensive filtering and pagination
        /// </summary>
        /// <param name="request">The audit log request with filters</param>
        /// <returns>Response containing audit log entries with retention policy</returns>
        Task<EnterpriseAuditLogResponse> GetAuditLogAsync(GetEnterpriseAuditLogRequest request);

        /// <summary>
        /// Exports audit log entries as CSV for MICA compliance reporting
        /// </summary>
        /// <param name="request">The audit log request with filters</param>
        /// <param name="maxRecords">Maximum number of records to export (default: 10000)</param>
        /// <returns>CSV content as string</returns>
        Task<string> ExportAuditLogCsvAsync(GetEnterpriseAuditLogRequest request, int maxRecords = 10000);

        /// <summary>
        /// Exports audit log entries as JSON for MICA compliance reporting
        /// </summary>
        /// <param name="request">The audit log request with filters</param>
        /// <param name="maxRecords">Maximum number of records to export (default: 10000)</param>
        /// <returns>JSON content as string</returns>
        Task<string> ExportAuditLogJsonAsync(GetEnterpriseAuditLogRequest request, int maxRecords = 10000);

        /// <summary>
        /// Gets the 7-year MICA retention policy for audit logs
        /// </summary>
        /// <returns>Audit retention policy metadata</returns>
        AuditRetentionPolicy GetRetentionPolicy();
    }
}
