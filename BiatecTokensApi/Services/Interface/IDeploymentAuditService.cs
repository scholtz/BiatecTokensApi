using BiatecTokensApi.Models;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for exporting deployment audit trails
    /// </summary>
    public interface IDeploymentAuditService
    {
        /// <summary>
        /// Exports audit trail for a specific deployment as JSON
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <returns>JSON string containing the audit trail</returns>
        Task<string> ExportAuditTrailAsJsonAsync(string deploymentId);

        /// <summary>
        /// Exports audit trail for a specific deployment as CSV
        /// </summary>
        /// <param name="deploymentId">The deployment ID</param>
        /// <returns>CSV string containing the audit trail</returns>
        Task<string> ExportAuditTrailAsCsvAsync(string deploymentId);

        /// <summary>
        /// Exports audit trails for multiple deployments with idempotency support
        /// </summary>
        /// <param name="request">Export request with filters</param>
        /// <param name="idempotencyKey">Optional idempotency key for large exports</param>
        /// <returns>Export result with data or cache reference</returns>
        Task<AuditExportResult> ExportAuditTrailsAsync(
            AuditExportRequest request,
            string? idempotencyKey = null);
    }
}
