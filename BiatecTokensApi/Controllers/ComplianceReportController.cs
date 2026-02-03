using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides MICA-ready compliance reporting and audit trail export endpoints
    /// </summary>
    /// <remarks>
    /// This controller enables enterprise-grade compliance reporting with support for:
    /// - MICA readiness assessments (Articles 17-35)
    /// - Audit trail snapshots for regulatory compliance
    /// - Compliance badge evidence collection
    /// - JSON and CSV export formats with tamper-evident checksums
    /// 
    /// All endpoints require ARC-0014 authentication and enforce issuer-level access control.
    /// Reports are scoped to the authenticated user's issued tokens only.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance/reports")]
    public class ComplianceReportController : ControllerBase
    {
        private readonly IComplianceReportService _reportService;
        private readonly ILogger<ComplianceReportController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceReportController"/> class.
        /// </summary>
        /// <param name="reportService">The compliance report service</param>
        /// <param name="logger">The logger instance</param>
        public ComplianceReportController(
            IComplianceReportService reportService,
            ILogger<ComplianceReportController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new compliance report
        /// </summary>
        /// <param name="request">Report creation request specifying type and filters</param>
        /// <returns>Report creation response with report ID and status</returns>
        /// <remarks>
        /// Creates a new compliance report of the specified type with optional filters.
        /// 
        /// **Report Types:**
        /// - **MicaReadiness**: Assesses token compliance against MICA Articles 17-35, providing a readiness score and identifying gaps
        /// - **AuditTrail**: Generates a chronological snapshot of all audit events for the specified scope
        /// - **ComplianceBadge**: Collects evidence items required for compliance certification
        /// 
        /// **Filters:**
        /// - `assetId`: Limit report to a specific token
        /// - `network`: Limit report to a specific network (voimain-v1.0, aramidmain-v1.0, etc.)
        /// - `fromDate` / `toDate`: Limit report to a specific time range
        /// 
        /// **Process:**
        /// 1. Report is created with status "Processing"
        /// 2. Report content is generated based on existing audit data
        /// 3. Report status changes to "Completed" when ready
        /// 4. SHA-256 checksum is calculated for tamper evidence
        /// 
        /// **Response:**
        /// Returns the report ID immediately. Use GET /api/v1/compliance/reports/{reportId} to check status and retrieve results.
        /// 
        /// **Example Request:**
        /// ```json
        /// {
        ///   "reportType": "MicaReadiness",
        ///   "assetId": 12345,
        ///   "network": "voimain-v1.0"
        /// }
        /// ```
        /// 
        /// **Access Control:**
        /// Reports are scoped to the authenticated issuer. You can only create reports for tokens you have issued.
        /// 
        /// **Performance:**
        /// Report generation is synchronous in this MVP implementation. Large datasets (10,000+ events) may take up to 30 seconds.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(CreateComplianceReportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateReport([FromBody] CreateComplianceReportRequest request)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Creating compliance report for user {UserAddress}: Type={ReportType}",
                    userAddress, request.ReportType);

                var result = await _reportService.CreateReportAsync(request, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Compliance report created: ReportId={ReportId}, Status={Status}",
                        result.ReportId, result.Status);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to create compliance report: {Error}", result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating compliance report");
                return StatusCode(StatusCodes.Status500InternalServerError, new CreateComplianceReportResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lists compliance reports for the authenticated user
        /// </summary>
        /// <param name="reportType">Optional filter by report type</param>
        /// <param name="assetId">Optional filter by asset ID</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="status">Optional filter by report status</param>
        /// <param name="fromDate">Optional filter by creation date (from)</param>
        /// <param name="toDate">Optional filter by creation date (to)</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 50, max: 100)</param>
        /// <returns>Paginated list of compliance reports</returns>
        /// <remarks>
        /// Returns a paginated list of compliance reports owned by the authenticated user.
        /// 
        /// **Filters:**
        /// All filters are optional and can be combined:
        /// - `reportType`: Filter by MicaReadiness, AuditTrail, or ComplianceBadge
        /// - `assetId`: Show only reports for a specific token
        /// - `network`: Show only reports for a specific network
        /// - `status`: Filter by Pending, Processing, Completed, or Failed
        /// - `fromDate` / `toDate`: Filter by report creation date range
        /// 
        /// **Pagination:**
        /// - Default page size: 50
        /// - Maximum page size: 100
        /// - Results ordered by creation date (most recent first)
        /// 
        /// **Response:**
        /// Each report summary includes:
        /// - Report ID and type
        /// - Status and event count
        /// - Creation and completion timestamps
        /// - Warning count (for identifying reports with gaps)
        /// 
        /// **Example:**
        /// ```
        /// GET /api/v1/compliance/reports?reportType=MicaReadiness&amp;status=Completed&amp;page=1
        /// ```
        /// 
        /// **Access Control:**
        /// Returns only reports created by the authenticated user. Admin access not implemented in MVP.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(ListComplianceReportsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListReports(
            [FromQuery] ReportType? reportType = null,
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? network = null,
            [FromQuery] ReportStatus? status = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Listing compliance reports for user {UserAddress}: Page={Page}",
                    userAddress, page);

                var request = new ListComplianceReportsRequest
                {
                    ReportType = reportType,
                    AssetId = assetId,
                    Network = network,
                    Status = status,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _reportService.ListReportsAsync(request, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Listed {Count} compliance reports for user {UserAddress}",
                        result.Reports.Count, userAddress);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to list compliance reports: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing compliance reports");
                return StatusCode(StatusCodes.Status500InternalServerError, new ListComplianceReportsResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets detailed information about a specific compliance report
        /// </summary>
        /// <param name="reportId">The report ID</param>
        /// <returns>Full report details including metadata and warnings</returns>
        /// <remarks>
        /// Retrieves complete information about a compliance report, including:
        /// - Report metadata (type, status, timestamps)
        /// - Applied filters (asset ID, network, date range)
        /// - Schema version for compatibility tracking
        /// - Event count and warnings
        /// - SHA-256 checksum for tamper evidence
        /// - Error message if report generation failed
        /// 
        /// **Status Values:**
        /// - **Pending**: Report queued for generation
        /// - **Processing**: Report currently being generated
        /// - **Completed**: Report ready for download
        /// - **Failed**: Report generation failed (check errorMessage)
        /// 
        /// **Warnings:**
        /// Reports may include warnings such as:
        /// - "Report limited to 10,000 events. Use date filters to narrow the scope."
        /// - "No audit events found matching the specified criteria."
        /// - "Missing evidence: [requirement name]"
        /// 
        /// **Access Control:**
        /// You can only retrieve reports you created. Returns 404 if report doesn't exist or access is denied.
        /// 
        /// **Example:**
        /// ```
        /// GET /api/v1/compliance/reports/550e8400-e29b-41d4-a716-446655440000
        /// ```
        /// </remarks>
        [HttpGet("{reportId}")]
        [ProducesResponseType(typeof(GetComplianceReportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetReport(string reportId)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Getting compliance report {ReportId} for user {UserAddress}",
                    reportId, userAddress);

                var result = await _reportService.GetReportAsync(reportId, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved compliance report {ReportId}", reportId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Report not found or access denied: {ReportId}, User: {UserAddress}",
                        reportId, userAddress);
                    return NotFound(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting compliance report {ReportId}", reportId);
                return StatusCode(StatusCodes.Status500InternalServerError, new GetComplianceReportResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Downloads a compliance report in the specified format
        /// </summary>
        /// <param name="reportId">The report ID</param>
        /// <param name="format">Export format: 'json' or 'csv' (default: json)</param>
        /// <returns>Report file in the requested format</returns>
        /// <remarks>
        /// Downloads the full report content as a file in JSON or CSV format.
        /// 
        /// **JSON Format:**
        /// - Pretty-printed JSON with complete report structure
        /// - Includes metadata, schema version, and checksum
        /// - Machine-readable for programmatic processing
        /// - Preserves all data types and nested structures
        /// 
        /// **CSV Format:**
        /// - UTF-8 encoded with proper escaping
        /// - Includes report metadata as comments (# prefix)
        /// - Contains checksum in header for integrity verification
        /// - Table format varies by report type:
        ///   - MicaReadiness: Article, Requirement, Status, Evidence, Recommendation
        ///   - AuditTrail: Timestamp, Category, Action, Performed By, Asset ID, Network, Success, etc.
        ///   - ComplianceBadge: Evidence Type, Description, Source, Timestamp, Status
        /// 
        /// **Checksum:**
        /// Both formats include SHA-256 checksum for tamper evidence:
        /// - JSON: In metadata object
        /// - CSV: In header comment
        /// 
        /// **Filename Format:**
        /// - JSON: `compliance-report-{reportId}.json`
        /// - CSV: `compliance-report-{reportId}.csv`
        /// 
        /// **Requirements:**
        /// - Report must have status "Completed"
        /// - User must own the report (issuer-level access control)
        /// 
        /// **Example:**
        /// ```
        /// GET /api/v1/compliance/reports/550e8400-e29b-41d4-a716-446655440000/download?format=csv
        /// ```
        /// 
        /// **Use Cases:**
        /// - Regulatory audit submissions
        /// - Internal compliance reviews
        /// - Evidence collection for MICA compliance
        /// - Integration with compliance management systems
        /// - Long-term archival with integrity verification
        /// </remarks>
        [HttpGet("{reportId}/download")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadReport(string reportId, [FromQuery] string format = "json")
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Downloading compliance report {ReportId} for user {UserAddress} in format {Format}",
                    reportId, userAddress, format);

                var content = await _reportService.DownloadReportAsync(reportId, userAddress, format);

                var contentType = format.Equals("csv", StringComparison.OrdinalIgnoreCase)
                    ? "text/csv"
                    : "application/json";

                var fileName = $"compliance-report-{reportId}.{format.ToLowerInvariant()}";

                _logger.LogInformation("Report downloaded successfully: {ReportId}, Format: {Format}",
                    reportId, format);

                return File(System.Text.Encoding.UTF8.GetBytes(content), contentType, fileName);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot download report {ReportId}: {Message}", reportId, ex.Message);
                return BadRequest(new { success = false, errorMessage = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid format for report {ReportId}: {Message}", reportId, ex.Message);
                return BadRequest(new { success = false, errorMessage = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception downloading compliance report {ReportId}", reportId);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets the user's Algorand address from the authentication context
        /// </summary>
        /// <returns>The user's Algorand address</returns>
        private string GetUserAddress()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? "Unknown";
        }
    }
}
