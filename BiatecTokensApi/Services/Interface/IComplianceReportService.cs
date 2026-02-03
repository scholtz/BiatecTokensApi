using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for compliance report generation and management
    /// </summary>
    public interface IComplianceReportService
    {
        /// <summary>
        /// Creates a new compliance report
        /// </summary>
        /// <param name="request">Report creation request</param>
        /// <param name="issuerId">Issuer ID (from authentication)</param>
        /// <returns>Report creation response with report ID</returns>
        Task<CreateComplianceReportResponse> CreateReportAsync(CreateComplianceReportRequest request, string issuerId);

        /// <summary>
        /// Gets a compliance report by ID
        /// </summary>
        /// <param name="reportId">Report ID</param>
        /// <param name="issuerId">Issuer ID (for access control)</param>
        /// <returns>Report details response</returns>
        Task<GetComplianceReportResponse> GetReportAsync(string reportId, string issuerId);

        /// <summary>
        /// Lists compliance reports for an issuer
        /// </summary>
        /// <param name="request">List request with filters</param>
        /// <param name="issuerId">Issuer ID</param>
        /// <returns>List of reports with pagination</returns>
        Task<ListComplianceReportsResponse> ListReportsAsync(ListComplianceReportsRequest request, string issuerId);

        /// <summary>
        /// Downloads a compliance report in the specified format
        /// </summary>
        /// <param name="reportId">Report ID</param>
        /// <param name="issuerId">Issuer ID (for access control)</param>
        /// <param name="format">Export format (json or csv)</param>
        /// <returns>Report content as string</returns>
        Task<string> DownloadReportAsync(string reportId, string issuerId, string format);
    }
}
