using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for compliance report persistence and retrieval
    /// </summary>
    public interface IComplianceReportRepository
    {
        /// <summary>
        /// Creates a new compliance report
        /// </summary>
        /// <param name="report">The report to create</param>
        /// <returns>The created report</returns>
        Task<ComplianceReport> CreateReportAsync(ComplianceReport report);

        /// <summary>
        /// Updates an existing compliance report
        /// </summary>
        /// <param name="report">The report to update</param>
        /// <returns>The updated report</returns>
        Task<ComplianceReport> UpdateReportAsync(ComplianceReport report);

        /// <summary>
        /// Gets a compliance report by ID for a specific issuer
        /// </summary>
        /// <param name="reportId">The report ID</param>
        /// <param name="issuerId">The issuer ID (for access control)</param>
        /// <returns>The report, or null if not found or access denied</returns>
        Task<ComplianceReport?> GetReportAsync(string reportId, string issuerId);

        /// <summary>
        /// Lists compliance reports for a specific issuer with filtering
        /// </summary>
        /// <param name="issuerId">The issuer ID</param>
        /// <param name="request">Filter and pagination parameters</param>
        /// <returns>List of reports matching the criteria</returns>
        Task<List<ComplianceReport>> ListReportsAsync(string issuerId, ListComplianceReportsRequest request);

        /// <summary>
        /// Gets the total count of reports for a specific issuer with filtering
        /// </summary>
        /// <param name="issuerId">The issuer ID</param>
        /// <param name="request">Filter parameters</param>
        /// <returns>Total count of matching reports</returns>
        Task<int> GetReportCountAsync(string issuerId, ListComplianceReportsRequest request);
    }
}
