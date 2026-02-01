using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for managing RWA token whitelists
    /// </summary>
    /// <remarks>
    /// This controller manages whitelist operations for RWA tokens including adding, removing,
    /// listing, and bulk uploading addresses. All endpoints require ARC-0014 authentication.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/whitelist")]
    public class WhitelistController : ControllerBase
    {
        private readonly IWhitelistService _whitelistService;
        private readonly ILogger<WhitelistController> _logger;
        
        /// <summary>
        /// Maximum number of records to export in a single request
        /// </summary>
        private const int MaxExportRecords = 10000;

        /// <summary>
        /// Maximum page size for pagination
        /// </summary>
        private const int MaxPageSize = 100;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhitelistController"/> class.
        /// </summary>
        /// <param name="whitelistService">The whitelist service</param>
        /// <param name="logger">The logger instance</param>
        public WhitelistController(
            IWhitelistService whitelistService,
            ILogger<WhitelistController> logger)
        {
            _whitelistService = whitelistService;
            _logger = logger;
        }

        /// <summary>
        /// Lists whitelist entries for a specific token
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <param name="status">Optional status filter</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20, max: 100)</param>
        /// <returns>List of whitelist entries with pagination</returns>
        [HttpGet("{assetId}")]
        [ProducesResponseType(typeof(WhitelistListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListWhitelist(
            [FromRoute] ulong assetId,
            [FromQuery] WhitelistStatus? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var request = new ListWhitelistRequest
                {
                    AssetId = assetId,
                    Status = status,
                    Page = page,
                    PageSize = Math.Min(pageSize, MaxPageSize) // Cap at 100
                };

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _whitelistService.ListEntriesAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Listed {Count} whitelist entries for asset {AssetId}", 
                        result.Entries.Count, assetId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to list whitelist entries for asset {AssetId}: {Error}", 
                        assetId, result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing whitelist entries for asset {AssetId}", assetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistListResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Adds a single address to the whitelist
        /// </summary>
        /// <param name="request">The add whitelist entry request</param>
        /// <returns>The created whitelist entry</returns>
        /// <remarks>
        /// This operation emits a metering event for billing analytics with the following details:
        /// - Category: Whitelist
        /// - OperationType: Add (for new entries) or Update (for existing entries)
        /// - Network: Not available
        /// - PerformedBy: Authenticated user
        /// - ItemCount: 1
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(WhitelistResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddWhitelistEntry([FromBody] AddWhitelistEntryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdBy = GetUserAddress();
                
                if (string.IsNullOrEmpty(createdBy))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new WhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _whitelistService.AddEntryAsync(request, createdBy);

                if (result.Success)
                {
                    _logger.LogInformation("Added whitelist entry for address {Address} on asset {AssetId} by {CreatedBy}", 
                        request.Address, request.AssetId, createdBy);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to add whitelist entry for address {Address} on asset {AssetId}: {Error}", 
                        request.Address, request.AssetId, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception adding whitelist entry for address {Address} on asset {AssetId}", 
                    request.Address, request.AssetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Removes an address from the whitelist
        /// </summary>
        /// <param name="request">The remove whitelist entry request</param>
        /// <returns>The result of the removal operation</returns>
        /// <remarks>
        /// This operation emits a metering event for billing analytics with the following details:
        /// - Category: Whitelist
        /// - OperationType: Remove
        /// - Network: Not available
        /// - PerformedBy: User who last modified the entry
        /// - ItemCount: 1
        /// </remarks>
        [HttpDelete]
        [ProducesResponseType(typeof(WhitelistResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RemoveWhitelistEntry([FromBody] RemoveWhitelistEntryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userAddress = GetUserAddress();
                
                if (string.IsNullOrEmpty(userAddress))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new WhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _whitelistService.RemoveEntryAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Removed whitelist entry for address {Address} on asset {AssetId} by {UserAddress}", 
                        request.Address, request.AssetId, userAddress);
                    return Ok(result);
                }
                else if (result.ErrorMessage == "Whitelist entry not found")
                {
                    _logger.LogWarning("Whitelist entry not found for address {Address} on asset {AssetId}", 
                        request.Address, request.AssetId);
                    return NotFound(result);
                }
                else
                {
                    _logger.LogError("Failed to remove whitelist entry for address {Address} on asset {AssetId}: {Error}", 
                        request.Address, request.AssetId, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception removing whitelist entry for address {Address} on asset {AssetId}", 
                    request.Address, request.AssetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Bulk adds addresses to the whitelist
        /// </summary>
        /// <param name="request">The bulk add whitelist request</param>
        /// <returns>The result of the bulk operation including success and failure counts</returns>
        /// <remarks>
        /// This operation emits a metering event for billing analytics with the following details:
        /// - Category: Whitelist
        /// - OperationType: BulkAdd
        /// - Network: Not available
        /// - PerformedBy: Authenticated user
        /// - ItemCount: Number of successfully added/updated entries (not failed ones)
        /// 
        /// Note: Metering event is only emitted if at least one entry succeeds.
        /// </remarks>
        [HttpPost("bulk")]
        [ProducesResponseType(typeof(BulkWhitelistResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> BulkAddWhitelistEntries([FromBody] BulkAddWhitelistRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdBy = GetUserAddress();
                
                if (string.IsNullOrEmpty(createdBy))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new BulkWhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _whitelistService.BulkAddEntriesAsync(request, createdBy);

                _logger.LogInformation("Bulk add completed for asset {AssetId}: {SuccessCount} succeeded, {FailedCount} failed by {CreatedBy}", 
                    request.AssetId, result.SuccessCount, result.FailedCount, createdBy);

                // Return 200 even if partially successful - client should check SuccessCount and FailedCount
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during bulk add for asset {AssetId}", request.AssetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new BulkWhitelistResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets the audit log for a specific token's whitelist
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <param name="address">Optional filter by address</param>
        /// <param name="actionType">Optional filter by action type</param>
        /// <param name="performedBy">Optional filter by user who performed the action</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 50, max: 100)</param>
        /// <returns>Audit log entries with pagination</returns>
        [HttpGet("{assetId}/audit-log")]
        [ProducesResponseType(typeof(WhitelistAuditLogResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAuditLog(
            [FromRoute] ulong assetId,
            [FromQuery] string? address = null,
            [FromQuery] WhitelistActionType? actionType = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var request = new GetWhitelistAuditLogRequest
                {
                    AssetId = assetId,
                    Address = address,
                    ActionType = actionType,
                    PerformedBy = performedBy,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = Math.Min(pageSize, MaxPageSize) // Cap at 100
                };

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _whitelistService.GetAuditLogAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved {Count} audit log entries for asset {AssetId}", 
                        result.Entries.Count, assetId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve audit log for asset {AssetId}: {Error}", 
                        assetId, result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving audit log for asset {AssetId}", assetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets audit log for whitelist operations across all assets with optional filtering
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="address">Optional filter by address</param>
        /// <param name="actionType">Optional filter by action type</param>
        /// <param name="performedBy">Optional filter by user who performed the action</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 50, max: 100)</param>
        /// <returns>Audit log entries with pagination and retention policy metadata</returns>
        /// <remarks>
        /// This endpoint provides access to immutable audit logs for compliance reporting and incident investigations
        /// across all whitelist operations. Unlike the asset-specific endpoint, this allows querying across all assets
        /// and filtering by network for MICA/RWA compliance dashboards.
        /// 
        /// **Retention Policy**: Audit logs are retained for a minimum of 7 years to comply with MICA regulations.
        /// All entries are immutable and cannot be modified or deleted.
        /// 
        /// **Use Cases**:
        /// - Enterprise-wide compliance dashboards
        /// - Network-specific audit reports (VOI, Aramid)
        /// - Cross-asset incident investigations
        /// - Regulatory compliance reporting
        /// - Actor-based activity tracking
        /// 
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("audit-log")]
        [ProducesResponseType(typeof(WhitelistAuditLogResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllAuditLogs(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? address = null,
            [FromQuery] WhitelistActionType? actionType = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] string? network = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var request = new GetWhitelistAuditLogRequest
                {
                    AssetId = assetId,
                    Address = address,
                    ActionType = actionType,
                    PerformedBy = performedBy,
                    Network = network,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = Math.Min(pageSize, MaxPageSize) // Cap at 100
                };

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _whitelistService.GetAuditLogAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved {Count} audit log entries (page {Page} of {TotalPages})", 
                        result.Entries.Count, result.Page, result.TotalPages);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve audit logs: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving audit logs");
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports whitelist audit log as CSV for compliance reporting
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="address">Optional filter by address</param>
        /// <param name="actionType">Optional filter by action type</param>
        /// <param name="performedBy">Optional filter by user who performed the action</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>CSV file with audit log entries</returns>
        /// <remarks>
        /// Exports audit log entries matching the filter criteria as a CSV file for regulatory compliance reporting.
        /// The CSV includes all audit fields: timestamp, asset ID, address, action type, actor, network, status changes, and notes.
        /// 
        /// **Format**: Standard CSV with headers
        /// **Encoding**: UTF-8
        /// **Max Records**: 10,000 per export (use pagination for larger datasets)
        /// 
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("audit-log/export/csv")]
        [Produces("text/csv")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportAuditLogCsv(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? address = null,
            [FromQuery] WhitelistActionType? actionType = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] string? network = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var request = new GetWhitelistAuditLogRequest
                {
                    AssetId = assetId,
                    Address = address,
                    ActionType = actionType,
                    PerformedBy = performedBy,
                    Network = network,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = 1,
                    PageSize = MaxExportRecords
                };

                var result = await _whitelistService.GetAuditLogAsync(request);

                if (!result.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessage);
                }

                // Build CSV content
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Id,AssetId,Address,ActionType,PerformedBy,PerformedAt,OldStatus,NewStatus,Notes,ToAddress,TransferAllowed,DenialReason,Amount,Network,Role");

                foreach (var entry in result.Entries)
                {
                    csv.AppendLine($"\"{entry.Id}\"," +
                        $"{entry.AssetId}," +
                        $"\"{entry.Address}\"," +
                        $"\"{entry.ActionType}\"," +
                        $"\"{entry.PerformedBy}\"," +
                        $"\"{entry.PerformedAt:O}\"," +
                        $"\"{entry.OldStatus?.ToString() ?? ""}\"," +
                        $"\"{entry.NewStatus?.ToString() ?? ""}\"," +
                        $"\"{EscapeCsv(entry.Notes)}\"," +
                        $"\"{EscapeCsv(entry.ToAddress)}\"," +
                        $"\"{entry.TransferAllowed?.ToString() ?? ""}\"," +
                        $"\"{EscapeCsv(entry.DenialReason)}\"," +
                        $"\"{entry.Amount?.ToString() ?? ""}\"," +
                        $"\"{entry.Network ?? ""}\"," +
                        $"\"{entry.Role}\"");
                }

                var fileName = $"whitelist-audit-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                
                _logger.LogInformation("Exported {Count} whitelist audit log entries to CSV", result.Entries.Count);

                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting whitelist audit log as CSV");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Internal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Exports whitelist audit log as JSON for compliance reporting
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="address">Optional filter by address</param>
        /// <param name="actionType">Optional filter by action type</param>
        /// <param name="performedBy">Optional filter by user who performed the action</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>JSON file with audit log entries</returns>
        /// <remarks>
        /// Exports audit log entries matching the filter criteria as a JSON file for regulatory compliance reporting.
        /// The JSON includes all audit fields and retention policy metadata.
        /// 
        /// **Max Records**: 10,000 per export (use pagination for larger datasets)
        /// 
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("audit-log/export/json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportAuditLogJson(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? address = null,
            [FromQuery] WhitelistActionType? actionType = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] string? network = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var request = new GetWhitelistAuditLogRequest
                {
                    AssetId = assetId,
                    Address = address,
                    ActionType = actionType,
                    PerformedBy = performedBy,
                    Network = network,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = 1,
                    PageSize = MaxExportRecords
                };

                var result = await _whitelistService.GetAuditLogAsync(request);

                if (!result.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }

                _logger.LogInformation("Exported {Count} whitelist audit log entries to JSON", result.Entries.Count);

                // Serialize to JSON with pretty printing
                var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var fileName = $"whitelist-audit-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

                return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting whitelist audit log as JSON");
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets the whitelist audit log retention policy metadata
        /// </summary>
        /// <returns>Retention policy information</returns>
        /// <remarks>
        /// Returns metadata about the audit log retention policy including minimum retention period,
        /// regulatory framework, and immutability guarantees.
        /// </remarks>
        [HttpGet("audit-log/retention-policy")]
        [ProducesResponseType(typeof(BiatecTokensApi.Models.Compliance.AuditRetentionPolicy), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetAuditLogRetentionPolicy()
        {
            var policy = new BiatecTokensApi.Models.Compliance.AuditRetentionPolicy
            {
                MinimumRetentionYears = 7,
                RegulatoryFramework = "MICA",
                ImmutableEntries = true,
                Description = "Audit logs are retained for a minimum of 7 years to comply with MICA and other regulatory requirements. All entries are immutable and cannot be modified or deleted."
            };

            return Ok(policy);
        }

        /// <summary>
        /// Gets whitelist enforcement audit report focused on transfer validation events
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="fromAddress">Optional filter by sender address</param>
        /// <param name="toAddress">Optional filter by receiver address</param>
        /// <param name="performedBy">Optional filter by user who performed the validation</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="transferAllowed">Optional filter by transfer result (true=allowed, false=denied)</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 50, max: 100)</param>
        /// <returns>Enforcement audit report with entries and summary statistics</returns>
        /// <remarks>
        /// This endpoint provides a focused view of whitelist enforcement events (transfer validations only)
        /// for enterprise compliance dashboards and MICA/RWA regulatory reporting.
        /// 
        /// **Key Features:**
        /// - Filters specifically for TransferValidation actions (enforcement events)
        /// - Includes comprehensive summary statistics (allowed/denied counts, percentages)
        /// - Top denial reasons for compliance analysis
        /// - Date range and network tracking
        /// - Supports filtering by transfer result (allowed/denied)
        /// 
        /// **Use Cases:**
        /// - Enterprise compliance dashboards showing enforcement effectiveness
        /// - Regulatory audit trails for MICA compliance
        /// - Analysis of denied transfer patterns
        /// - Network-specific enforcement monitoring (VOI, Aramid)
        /// - Investigating specific transfer validation incidents
        /// 
        /// **Business Value:**
        /// - Demonstrates enforcement effectiveness to regulators
        /// - Identifies compliance gaps and patterns
        /// - Supports evidence-based policy adjustments
        /// - Enables proactive risk management
        /// 
        /// **Retention Policy**: 7-year minimum retention for MICA compliance.
        /// All entries are immutable and cannot be modified or deleted.
        /// 
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("enforcement-report")]
        [ProducesResponseType(typeof(WhitelistEnforcementReportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetEnforcementReport(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? fromAddress = null,
            [FromQuery] string? toAddress = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] string? network = null,
            [FromQuery] bool? transferAllowed = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Enforcement report requested by {UserAddress}: AssetId={AssetId}, Network={Network}",
                    userAddress, assetId, network);

                var request = new GetWhitelistEnforcementReportRequest
                {
                    AssetId = assetId,
                    FromAddress = fromAddress,
                    ToAddress = toAddress,
                    PerformedBy = performedBy,
                    Network = network,
                    TransferAllowed = transferAllowed,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = Math.Min(pageSize, MaxPageSize)
                };

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _whitelistService.GetEnforcementReportAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved {Count} enforcement entries for user {UserAddress}",
                        result.Entries.Count, userAddress);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve enforcement report: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving enforcement report");
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistEnforcementReportResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports whitelist enforcement audit report as CSV
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID</param>
        /// <param name="fromAddress">Optional filter by sender address</param>
        /// <param name="toAddress">Optional filter by receiver address</param>
        /// <param name="performedBy">Optional filter by validator</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="transferAllowed">Optional filter by result</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>CSV file with enforcement audit entries</returns>
        /// <remarks>
        /// Exports up to 10,000 enforcement audit entries in CSV format for compliance reporting.
        /// 
        /// **CSV Format:**
        /// - UTF-8 encoding with proper CSV escaping
        /// - Header row with all enforcement-relevant fields
        /// - One row per transfer validation event
        /// - Includes sender, receiver, result, denial reason, timestamp
        /// 
        /// **Use Cases:**
        /// - MICA compliance submissions to regulators
        /// - Excel analysis of enforcement patterns
        /// - Integration with enterprise compliance systems
        /// - Long-term archival of enforcement records
        /// 
        /// **Business Value:**
        /// - Simplifies regulatory reporting workflows
        /// - Enables offline analysis and auditing
        /// - Supports evidence-based compliance decisions
        /// 
        /// Requires ARC-0014 authentication.
        /// </remarks>
        [HttpGet("enforcement-report/export/csv")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportEnforcementReportCsv(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? fromAddress = null,
            [FromQuery] string? toAddress = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] string? network = null,
            [FromQuery] bool? transferAllowed = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Enforcement report CSV export requested by {UserAddress}: AssetId={AssetId}",
                    userAddress, assetId);

                var request = new GetWhitelistEnforcementReportRequest
                {
                    AssetId = assetId,
                    FromAddress = fromAddress,
                    ToAddress = toAddress,
                    PerformedBy = performedBy,
                    Network = network,
                    TransferAllowed = transferAllowed,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = 1,
                    PageSize = MaxExportRecords
                };

                var result = await _whitelistService.GetEnforcementReportAsync(request);

                if (!result.Success)
                {
                    _logger.LogError("Failed to export enforcement report as CSV: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }

                // Build CSV
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Id,AssetId,FromAddress,ToAddress,PerformedBy,PerformedAt,TransferAllowed,DenialReason,Amount,Network,Role,Notes");

                foreach (var entry in result.Entries)
                {
                    csv.AppendLine(
                        $"\"{EscapeCsv(entry.Id)}\"," +
                        $"{entry.AssetId}," +
                        $"\"{EscapeCsv(entry.Address)}\"," +
                        $"\"{EscapeCsv(entry.ToAddress)}\"," +
                        $"\"{EscapeCsv(entry.PerformedBy)}\"," +
                        $"\"{entry.PerformedAt:o}\"," +
                        $"{entry.TransferAllowed}," +
                        $"\"{EscapeCsv(entry.DenialReason)}\"," +
                        $"{entry.Amount}," +
                        $"\"{EscapeCsv(entry.Network)}\"," +
                        $"\"{entry.Role}\"," +
                        $"\"{EscapeCsv(entry.Notes)}\""
                    );
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                var fileName = $"whitelist-enforcement-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

                _logger.LogInformation("Exported {Count} enforcement entries as CSV for user {UserAddress}",
                    result.Entries.Count, userAddress);

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting enforcement report as CSV");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports whitelist enforcement audit report as JSON
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID</param>
        /// <param name="fromAddress">Optional filter by sender address</param>
        /// <param name="toAddress">Optional filter by receiver address</param>
        /// <param name="performedBy">Optional filter by validator</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="transferAllowed">Optional filter by result</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>JSON file with enforcement audit entries and statistics</returns>
        /// <remarks>
        /// Exports up to 10,000 enforcement audit entries in JSON format with summary statistics.
        /// 
        /// **JSON Format:**
        /// - Pretty-printed JSON with camelCase property names
        /// - Includes full response structure with summary statistics
        /// - Contains retention policy metadata
        /// - Includes enforcement metrics (allowed/denied percentages, denial reasons)
        /// 
        /// **Use Cases:**
        /// - Programmatic analysis of enforcement patterns
        /// - Integration with compliance management systems
        /// - Dashboard data feeds
        /// - Long-term archival with metadata
        /// 
        /// **Business Value:**
        /// - Enables automated compliance monitoring
        /// - Supports data-driven policy decisions
        /// - Facilitates integration with enterprise systems
        /// 
        /// Requires ARC-0014 authentication.
        /// </remarks>
        [HttpGet("enforcement-report/export/json")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportEnforcementReportJson(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? fromAddress = null,
            [FromQuery] string? toAddress = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] string? network = null,
            [FromQuery] bool? transferAllowed = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Enforcement report JSON export requested by {UserAddress}: AssetId={AssetId}",
                    userAddress, assetId);

                var request = new GetWhitelistEnforcementReportRequest
                {
                    AssetId = assetId,
                    FromAddress = fromAddress,
                    ToAddress = toAddress,
                    PerformedBy = performedBy,
                    Network = network,
                    TransferAllowed = transferAllowed,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = 1,
                    PageSize = MaxExportRecords
                };

                var result = await _whitelistService.GetEnforcementReportAsync(request);

                if (!result.Success)
                {
                    _logger.LogError("Failed to export enforcement report as JSON: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }

                var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });

                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                var fileName = $"whitelist-enforcement-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

                _logger.LogInformation("Exported {Count} enforcement entries as JSON for user {UserAddress}",
                    result.Entries.Count, userAddress);

                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting enforcement report as JSON");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Validates if a transfer between two addresses is allowed based on whitelist rules
        /// </summary>
        /// <param name="request">The transfer validation request</param>
        /// <returns>Validation response indicating if the transfer is allowed</returns>
        /// <remarks>
        /// This endpoint validates whether a token transfer is permitted based on whitelist compliance rules.
        /// Both sender and receiver must be actively whitelisted (status=Active) with non-expired entries.
        /// 
        /// Use this endpoint before executing transfers to ensure compliance with MICA regulations
        /// and other regulatory requirements for RWA tokens.
        /// 
        /// The response includes detailed status information for both sender and receiver,
        /// including whitelist status, expiration dates, and specific denial reasons if applicable.
        /// 
        /// **Audit Logging**: All transfer validation attempts are recorded in the audit log with:
        /// - Who performed the validation (authenticated user)
        /// - When the validation occurred (timestamp)
        /// - Transfer details (from/to addresses, amount)
        /// - Validation result (allowed/denied with reason)
        /// </remarks>
        [HttpPost("validate-transfer")]
        [ProducesResponseType(typeof(ValidateTransferResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateTransfer([FromBody] ValidateTransferRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var performedBy = GetUserAddress();
                
                if (string.IsNullOrEmpty(performedBy))
                {
                    _logger.LogWarning("Failed to get user address from authentication context for transfer validation");
                    return Unauthorized(new ValidateTransferResponse
                    {
                        Success = false,
                        IsAllowed = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _whitelistService.ValidateTransferAsync(request, performedBy);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Validated transfer for asset {AssetId} from {From} to {To} by {PerformedBy}: {Result}",
                        request.AssetId, request.FromAddress, request.ToAddress, performedBy,
                        result.IsAllowed ? "ALLOWED" : "DENIED");
                    return Ok(result);
                }
                else
                {
                    _logger.LogError(
                        "Failed to validate transfer for asset {AssetId} from {From} to {To}: {Error}",
                        request.AssetId, request.FromAddress, request.ToAddress, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Exception validating transfer for asset {AssetId} from {From} to {To}",
                    request.AssetId, request.FromAddress, request.ToAddress);
                return StatusCode(StatusCodes.Status500InternalServerError, new ValidateTransferResponse
                {
                    Success = false,
                    IsAllowed = false,
                    ErrorMessage = "An error occurred while validating the transfer. Please try again or contact support.",
                    DenialReason = "Internal validation error"
                });
            }
        }

        /// <summary>
        /// Gets the authenticated user's Algorand address from the claims
        /// </summary>
        /// <returns>The user's Algorand address or empty string if not found</returns>
        private string GetUserAddress()
        {
            // ARC-0014 authentication stores the address in the "sub" claim
            var address = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value 
                ?? string.Empty;
            
            return address;
        }

        /// <summary>
        /// Escapes special characters in CSV fields
        /// </summary>
        /// <param name="value">The value to escape</param>
        /// <returns>The escaped value</returns>
        private string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // Escape double quotes by doubling them
            return value.Replace("\"", "\"\"");
        }

        /// <summary>
        /// Exports whitelist entries for a specific token as CSV
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <param name="status">Optional status filter</param>
        /// <returns>CSV file with whitelist entries</returns>
        /// <remarks>
        /// Exports up to 10,000 whitelist entries in CSV format for the specified token.
        /// 
        /// **CSV Format:**
        /// - UTF-8 encoding with proper CSV escaping
        /// - Header row included
        /// - Columns: Id, AssetId, Address, Status, CreatedBy, CreatedAt, UpdatedAt, UpdatedBy, Reason, ExpirationDate, KycVerified, KycVerificationDate, KycProvider, Network, Role
        /// 
        /// **Use Cases:**
        /// - Backup whitelist data for disaster recovery
        /// - Export for offline review or external processing
        /// - Create templates for bulk import
        /// 
        /// **Max Records**: 10,000 per export
        /// </remarks>
        [HttpGet("{assetId}/export/csv")]
        [Produces("text/csv")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportWhitelistCsv(
            [FromRoute] ulong assetId,
            [FromQuery] WhitelistStatus? status = null)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Whitelist CSV export requested by {UserAddress} for asset {AssetId}",
                    userAddress, assetId);

                var request = new ListWhitelistRequest
                {
                    AssetId = assetId,
                    Status = status,
                    Page = 1,
                    PageSize = MaxExportRecords
                };

                var result = await _whitelistService.ListEntriesAsync(request);

                if (!result.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }

                // Build CSV content
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Id,AssetId,Address,Status,CreatedBy,CreatedAt,UpdatedAt,UpdatedBy,Reason,ExpirationDate,KycVerified,KycVerificationDate,KycProvider,Network,Role");

                foreach (var entry in result.Entries)
                {
                    csv.AppendLine($"\"{entry.Id}\"," +
                        $"{entry.AssetId}," +
                        $"\"{EscapeCsv(entry.Address)}\"," +
                        $"{entry.Status}," +
                        $"\"{EscapeCsv(entry.CreatedBy)}\"," +
                        $"{entry.CreatedAt:O}," +
                        $"{(entry.UpdatedAt.HasValue ? entry.UpdatedAt.Value.ToString("O") : "")}," +
                        $"\"{EscapeCsv(entry.UpdatedBy)}\"," +
                        $"\"{EscapeCsv(entry.Reason)}\"," +
                        $"{(entry.ExpirationDate.HasValue ? entry.ExpirationDate.Value.ToString("O") : "")}," +
                        $"{entry.KycVerified}," +
                        $"{(entry.KycVerificationDate.HasValue ? entry.KycVerificationDate.Value.ToString("O") : "")}," +
                        $"\"{EscapeCsv(entry.KycProvider)}\"," +
                        $"\"{EscapeCsv(entry.Network)}\"," +
                        $"{entry.Role}");
                }

                var fileName = $"whitelist-{assetId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

                _logger.LogInformation("Exported {Count} whitelist entries to CSV for asset {AssetId}", 
                    result.Entries.Count, assetId);

                return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting whitelist as CSV for asset {AssetId}", assetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistListResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to export whitelist: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Imports whitelist entries from a CSV file
        /// </summary>
        /// <param name="assetId">The asset ID (token ID) for which to import entries</param>
        /// <param name="file">The CSV file to import</param>
        /// <returns>The result of the bulk import operation</returns>
        /// <remarks>
        /// Imports whitelist entries from a CSV file for the specified token.
        /// 
        /// **CSV Format Requirements:**
        /// - UTF-8 encoding
        /// - Header row required with column: Address (required), Status (optional), Reason (optional), KycVerified (optional), Network (optional)
        /// - Minimal format: just "Address" column with one address per line
        /// - Maximum 1000 addresses per file
        /// - File size limit: 1 MB
        /// 
        /// **Supported Columns:**
        /// - **Address** (required): Algorand address to whitelist
        /// - **Status** (optional): Active, Inactive, or Revoked (defaults to Active)
        /// - **Reason** (optional): Reason for whitelisting
        /// - **KycVerified** (optional): true or false (defaults to false)
        /// - **KycProvider** (optional): Name of KYC provider
        /// - **Network** (optional): voimain-v1.0, aramidmain-v1.0, etc.
        /// - **ExpirationDate** (optional): ISO 8601 date format
        /// 
        /// **Important Note on Metadata:**
        /// When importing, the metadata fields (Status, Reason, KycVerified, etc.) from the first data row
        /// will be applied to ALL addresses in the CSV file. If you need to import addresses with different
        /// metadata values, use the single address endpoint (`POST /api/v1/whitelist`) or make multiple
        /// CSV import calls with grouped addresses.
        /// 
        /// **Example CSV:**
        /// ```
        /// Address,Status,Reason,KycVerified,Network
        /// VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA,Active,Accredited Investor,true,voimain-v1.0
        /// AAAA...AAA,Active,Accredited Investor,true,voimain-v1.0
        /// ```
        /// (Note: Both addresses will use the metadata from the first row)
        /// 
        /// **Validation:**
        /// - All addresses are validated for correct Algorand format
        /// - Network-specific rules are enforced (VOI/Aramid requirements)
        /// - Invalid rows are skipped and reported in the response
        /// 
        /// **Response:**
        /// - Returns count of successful and failed imports
        /// - Lists failed addresses with reasons
        /// 
        /// This operation emits a metering event for billing analytics.
        /// </remarks>
        [HttpPost("{assetId}/import/csv")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(BulkWhitelistResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImportWhitelistCsv(
            [FromRoute] ulong assetId,
            IFormFile file)
        {
            try
            {
                var createdBy = GetUserAddress();
                
                if (string.IsNullOrEmpty(createdBy))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new BulkWhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                // Validate file
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new BulkWhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = "No file uploaded or file is empty"
                    });
                }

                // Check file size (1 MB limit)
                const long maxFileSize = 1024 * 1024; // 1 MB
                if (file.Length > maxFileSize)
                {
                    return BadRequest(new BulkWhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = $"File size exceeds maximum allowed size of {maxFileSize / 1024 / 1024} MB"
                    });
                }

                // Check file extension
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (fileExtension != ".csv")
                {
                    return BadRequest(new BulkWhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = "File must be a CSV file with .csv extension"
                    });
                }

                _logger.LogInformation("Processing CSV import for asset {AssetId} by {CreatedBy}, file: {FileName}", 
                    assetId, createdBy, file.FileName);

                // Parse CSV file
                var addresses = new List<string>();
                var addressToMetadata = new Dictionary<string, CsvRowMetadata>();
                var errors = new List<string>();
                const int maxAddresses = 1000;

                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    var lineNumber = 0;
                    string? line;
                    string[]? headers = null;
                    int addressColumnIndex = -1;
                    int statusColumnIndex = -1;
                    int reasonColumnIndex = -1;
                    int kycVerifiedColumnIndex = -1;
                    int kycProviderColumnIndex = -1;
                    int networkColumnIndex = -1;
                    int expirationDateColumnIndex = -1;

                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        lineNumber++;

                        // Skip empty lines
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        // Parse header row
                        if (headers == null)
                        {
                            headers = ParseCsvLine(line);
                            
                            // Find column indices (case-insensitive)
                            for (int i = 0; i < headers.Length; i++)
                            {
                                var header = headers[i].Trim().ToLowerInvariant();
                                if (header == "address") addressColumnIndex = i;
                                else if (header == "status") statusColumnIndex = i;
                                else if (header == "reason") reasonColumnIndex = i;
                                else if (header == "kycverified") kycVerifiedColumnIndex = i;
                                else if (header == "kycprovider") kycProviderColumnIndex = i;
                                else if (header == "network") networkColumnIndex = i;
                                else if (header == "expirationdate") expirationDateColumnIndex = i;
                            }

                            if (addressColumnIndex == -1)
                            {
                                return BadRequest(new BulkWhitelistResponse
                                {
                                    Success = false,
                                    ErrorMessage = "CSV file must contain an 'Address' column"
                                });
                            }

                            continue;
                        }

                        // Parse data rows
                        var columns = ParseCsvLine(line);

                        if (columns.Length <= addressColumnIndex)
                        {
                            errors.Add($"Line {lineNumber}: Missing address column");
                            continue;
                        }

                        var address = columns[addressColumnIndex].Trim();

                        if (string.IsNullOrWhiteSpace(address))
                        {
                            errors.Add($"Line {lineNumber}: Empty address");
                            continue;
                        }

                        if (addresses.Count >= maxAddresses)
                        {
                            errors.Add($"Line {lineNumber}: Maximum number of addresses ({maxAddresses}) exceeded");
                            break;
                        }

                        addresses.Add(address);

                        // Parse optional metadata
                        var metadata = new CsvRowMetadata();
                        
                        if (statusColumnIndex >= 0 && columns.Length > statusColumnIndex)
                        {
                            var statusStr = columns[statusColumnIndex].Trim();
                            if (!string.IsNullOrEmpty(statusStr) && 
                                Enum.TryParse<WhitelistStatus>(statusStr, true, out var status))
                            {
                                metadata.Status = status;
                            }
                        }

                        if (reasonColumnIndex >= 0 && columns.Length > reasonColumnIndex)
                        {
                            metadata.Reason = columns[reasonColumnIndex].Trim();
                        }

                        if (kycVerifiedColumnIndex >= 0 && columns.Length > kycVerifiedColumnIndex)
                        {
                            var kycStr = columns[kycVerifiedColumnIndex].Trim();
                            if (bool.TryParse(kycStr, out var kycVerified))
                            {
                                metadata.KycVerified = kycVerified;
                            }
                        }

                        if (kycProviderColumnIndex >= 0 && columns.Length > kycProviderColumnIndex)
                        {
                            metadata.KycProvider = columns[kycProviderColumnIndex].Trim();
                        }

                        if (networkColumnIndex >= 0 && columns.Length > networkColumnIndex)
                        {
                            metadata.Network = columns[networkColumnIndex].Trim();
                        }

                        if (expirationDateColumnIndex >= 0 && columns.Length > expirationDateColumnIndex)
                        {
                            var dateStr = columns[expirationDateColumnIndex].Trim();
                            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var expirationDate))
                            {
                                metadata.ExpirationDate = expirationDate;
                            }
                        }

                        addressToMetadata[address] = metadata;
                    }
                }

                if (addresses.Count == 0)
                {
                    return BadRequest(new BulkWhitelistResponse
                    {
                        Success = false,
                        ErrorMessage = "No valid addresses found in CSV file",
                        ValidationErrors = errors
                    });
                }

                _logger.LogInformation("Parsed {Count} addresses from CSV for asset {AssetId}", 
                    addresses.Count, assetId);

                // Create bulk request with first address metadata (for now, use same metadata for all)
                // In a future enhancement, we could process each row individually with its metadata
                var firstMetadata = addressToMetadata.Values.FirstOrDefault() ?? new CsvRowMetadata();
                
                var bulkRequest = new BulkAddWhitelistRequest
                {
                    AssetId = assetId,
                    Addresses = addresses,
                    Status = firstMetadata.Status ?? WhitelistStatus.Active,
                    Reason = firstMetadata.Reason,
                    KycVerified = firstMetadata.KycVerified ?? false,
                    KycProvider = firstMetadata.KycProvider,
                    Network = firstMetadata.Network,
                    ExpirationDate = firstMetadata.ExpirationDate,
                    Role = WhitelistRole.Admin
                };

                var result = await _whitelistService.BulkAddEntriesAsync(bulkRequest, createdBy);

                // Add CSV parsing errors to validation errors
                result.ValidationErrors.AddRange(errors);

                _logger.LogInformation("CSV import completed for asset {AssetId}: {SuccessCount} succeeded, {FailedCount} failed by {CreatedBy}", 
                    assetId, result.SuccessCount, result.FailedCount, createdBy);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during CSV import for asset {AssetId}", assetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new BulkWhitelistResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Helper method to parse a CSV line handling quoted values
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            var columns = new List<string>();
            var currentColumn = new System.Text.StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        currentColumn.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        // Toggle quote mode
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // End of column
                    columns.Add(currentColumn.ToString());
                    currentColumn.Clear();
                }
                else
                {
                    currentColumn.Append(c);
                }
            }

            // Add last column
            columns.Add(currentColumn.ToString());

            return columns.ToArray();
        }

        /// <summary>
        /// Helper class to store CSV row metadata
        /// </summary>
        private class CsvRowMetadata
        {
            public WhitelistStatus? Status { get; set; }
            public string? Reason { get; set; }
            public bool? KycVerified { get; set; }
            public string? KycProvider { get; set; }
            public string? Network { get; set; }
            public DateTime? ExpirationDate { get; set; }
        }
    }
}
