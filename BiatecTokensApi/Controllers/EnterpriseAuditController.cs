using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides enterprise-grade audit export endpoints for MICA reporting
    /// </summary>
    /// <remarks>
    /// This controller provides unified audit log access across whitelist/blacklist and compliance
    /// operations for regulatory compliance reporting. Supports 7-year retention requirements,
    /// comprehensive filtering by asset/network, and CSV/JSON export formats.
    /// Designed specifically for VOI/Aramid networks and MICA compliance.
    /// 
    /// All endpoints require ARC-0014 authentication and are recommended for compliance/admin roles only.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/enterprise-audit")]
    public class EnterpriseAuditController : ControllerBase
    {
        private readonly IEnterpriseAuditService _auditService;
        private readonly ILogger<EnterpriseAuditController> _logger;

        /// <summary>
        /// Maximum number of records to export in a single request
        /// </summary>
        private const int MaxExportRecords = 10000;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnterpriseAuditController"/> class.
        /// </summary>
        /// <param name="auditService">The enterprise audit service</param>
        /// <param name="logger">The logger instance</param>
        public EnterpriseAuditController(
            IEnterpriseAuditService auditService,
            ILogger<EnterpriseAuditController> logger)
        {
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves enterprise audit log entries with comprehensive filtering
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="network">Optional filter by network (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, etc.)</param>
        /// <param name="category">Optional filter by event category (Whitelist, Blacklist, Compliance, TransferValidation)</param>
        /// <param name="actionType">Optional filter by action type (Add, Update, Remove, Create, Delete, etc.)</param>
        /// <param name="performedBy">Optional filter by user who performed the action</param>
        /// <param name="affectedAddress">Optional filter by affected address (for whitelist/blacklist operations)</param>
        /// <param name="success">Optional filter by operation result (true/false)</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 50, max: 100)</param>
        /// <returns>Paginated list of audit log entries with summary statistics</returns>
        /// <remarks>
        /// This endpoint provides a unified view of all audit events across whitelist/blacklist and compliance operations.
        /// 
        /// **Use Cases:**
        /// - MICA compliance reporting and audits
        /// - Regulatory investigation support
        /// - Enterprise compliance dashboards
        /// - Network-specific audit trails (VOI, Aramid)
        /// - Cross-asset incident investigations
        /// 
        /// **Filtering:**
        /// All filters are optional and can be combined for precise queries. Date filters support ISO 8601 format.
        /// 
        /// **Networks:**
        /// - voimain-v1.0: VOI mainnet
        /// - aramidmain-v1.0: Aramid mainnet
        /// - mainnet-v1.0: Algorand mainnet
        /// - testnet-v1.0: Algorand testnet
        /// 
        /// **Response includes:**
        /// - Paginated audit entries ordered by most recent first
        /// - Each entry includes a SHA-256 payload hash for integrity verification
        /// - Total count and page information
        /// - 7-year MICA retention policy metadata
        /// - Summary statistics (event counts, date ranges, networks, assets)
        /// 
        /// **Security:**
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("export")]
        [ProducesResponseType(typeof(EnterpriseAuditLogResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAuditLog(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? network = null,
            [FromQuery] AuditEventCategory? category = null,
            [FromQuery] string? actionType = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] string? affectedAddress = null,
            [FromQuery] bool? success = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Enterprise audit log requested by {UserAddress}: AssetId={AssetId}, Network={Network}, Category={Category}",
                    userAddress, assetId, network, category);

                var request = new GetEnterpriseAuditLogRequest
                {
                    AssetId = assetId,
                    Network = network,
                    Category = category,
                    ActionType = actionType,
                    PerformedBy = performedBy,
                    AffectedAddress = affectedAddress,
                    Success = success,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _auditService.GetAuditLogAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved {Count} enterprise audit entries for user {UserAddress}",
                        result.Entries.Count, userAddress);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve enterprise audit log: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving enterprise audit log");
                return StatusCode(StatusCodes.Status500InternalServerError, new EnterpriseAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports enterprise audit log as CSV for MICA compliance reporting
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="network">Optional filter by network (voimain-v1.0, aramidmain-v1.0, etc.)</param>
        /// <param name="category">Optional filter by event category</param>
        /// <param name="actionType">Optional filter by action type</param>
        /// <param name="performedBy">Optional filter by user who performed the action</param>
        /// <param name="affectedAddress">Optional filter by affected address</param>
        /// <param name="success">Optional filter by operation result</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <returns>CSV file with audit log entries</returns>
        /// <remarks>
        /// Exports up to 10,000 audit log entries in CSV format for compliance reporting and analysis.
        /// 
        /// **CSV Format:**
        /// - UTF-8 encoding with proper CSV escaping
        /// - Header row with all field names including PayloadHash
        /// - One row per audit event
        /// - Timestamp in ISO 8601 format
        /// - SHA-256 payload hash for each entry to verify data integrity
        /// 
        /// **Use Cases:**
        /// - MICA compliance reporting
        /// - Regulatory audit submissions
        /// - Enterprise compliance system integration
        /// - Excel/spreadsheet analysis
        /// - Long-term archival with integrity verification
        /// 
        /// **Limits:**
        /// - Maximum 10,000 records per export
        /// - Use pagination parameters to export data in chunks if needed
        /// - Filtered exports may contain fewer records
        /// 
        /// **Filename:**
        /// - Format: enterprise-audit-log-{timestamp}.csv
        /// - Timestamp in yyyyMMddHHmmss format
        /// 
        /// **Security:**
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("export/csv")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportAuditLogCsv(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? network = null,
            [FromQuery] AuditEventCategory? category = null,
            [FromQuery] string? actionType = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] string? affectedAddress = null,
            [FromQuery] bool? success = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Enterprise audit log CSV export requested by {UserAddress}: AssetId={AssetId}, Network={Network}",
                    userAddress, assetId, network);

                var request = new GetEnterpriseAuditLogRequest
                {
                    AssetId = assetId,
                    Network = network,
                    Category = category,
                    ActionType = actionType,
                    PerformedBy = performedBy,
                    AffectedAddress = affectedAddress,
                    Success = success,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = 1,
                    PageSize = MaxExportRecords
                };

                var csv = await _auditService.ExportAuditLogCsvAsync(request, MaxExportRecords);
                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                var fileName = $"enterprise-audit-log-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

                _logger.LogInformation("Exported enterprise audit log as CSV for user {UserAddress}", userAddress);
                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting enterprise audit log as CSV");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports enterprise audit log as JSON for MICA compliance reporting
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="network">Optional filter by network (voimain-v1.0, aramidmain-v1.0, etc.)</param>
        /// <param name="category">Optional filter by event category</param>
        /// <param name="actionType">Optional filter by action type</param>
        /// <param name="performedBy">Optional filter by user who performed the action</param>
        /// <param name="affectedAddress">Optional filter by affected address</param>
        /// <param name="success">Optional filter by operation result</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <returns>JSON file with audit log entries and metadata</returns>
        /// <remarks>
        /// Exports up to 10,000 audit log entries in JSON format for compliance reporting and programmatic analysis.
        /// 
        /// **JSON Format:**
        /// - Pretty-printed JSON with camelCase property names
        /// - Includes full response structure with metadata
        /// - Contains retention policy and summary statistics
        /// - Timestamp in ISO 8601 format
        /// 
        /// **Use Cases:**
        /// - MICA compliance reporting
        /// - Programmatic audit log analysis
        /// - Integration with compliance management systems
        /// - Data archival for long-term storage
        /// - Compliance dashboard data feeds
        /// 
        /// **Limits:**
        /// - Maximum 10,000 records per export
        /// - Use pagination parameters to export data in chunks if needed
        /// - Filtered exports may contain fewer records
        /// 
        /// **Filename:**
        /// - Format: enterprise-audit-log-{timestamp}.json
        /// - Timestamp in yyyyMMddHHmmss format
        /// 
        /// **Security:**
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("export/json")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportAuditLogJson(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? network = null,
            [FromQuery] AuditEventCategory? category = null,
            [FromQuery] string? actionType = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] string? affectedAddress = null,
            [FromQuery] bool? success = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Enterprise audit log JSON export requested by {UserAddress}: AssetId={AssetId}, Network={Network}",
                    userAddress, assetId, network);

                var request = new GetEnterpriseAuditLogRequest
                {
                    AssetId = assetId,
                    Network = network,
                    Category = category,
                    ActionType = actionType,
                    PerformedBy = performedBy,
                    AffectedAddress = affectedAddress,
                    Success = success,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = 1,
                    PageSize = MaxExportRecords
                };

                var json = await _auditService.ExportAuditLogJsonAsync(request, MaxExportRecords);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                var fileName = $"enterprise-audit-log-{DateTime.UtcNow:yyyyMMddHHmmss}.json";

                _logger.LogInformation("Exported enterprise audit log as JSON for user {UserAddress}", userAddress);
                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting enterprise audit log as JSON");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets the 7-year MICA retention policy for enterprise audit logs
        /// </summary>
        /// <returns>Retention policy metadata including minimum retention period and regulatory framework</returns>
        /// <remarks>
        /// Returns metadata about the audit log retention policy for transparency and compliance verification.
        /// 
        /// **Policy Details:**
        /// - Minimum retention: 7 years
        /// - Regulatory framework: MICA (Markets in Crypto-Assets Regulation)
        /// - Immutable entries: Cannot be modified or deleted
        /// - Scope: All whitelist, blacklist, and compliance events
        /// - Networks: All supported networks including VOI and Aramid
        /// 
        /// **Use Cases:**
        /// - Compliance policy verification
        /// - Audit preparation
        /// - Regulatory documentation
        /// - Enterprise policy alignment
        /// 
        /// **Security:**
        /// Requires ARC-0014 authentication for consistency with other endpoints.
        /// </remarks>
        [HttpGet("retention-policy")]
        [ProducesResponseType(typeof(AuditRetentionPolicy), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetRetentionPolicy()
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Enterprise audit retention policy requested by {UserAddress}", userAddress);

                var policy = _auditService.GetRetentionPolicy();
                return Ok(policy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving enterprise audit retention policy");
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
