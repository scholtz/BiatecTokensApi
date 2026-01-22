using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for managing RWA token compliance metadata
    /// </summary>
    /// <remarks>
    /// This controller manages compliance metadata operations for RWA tokens including creating,
    /// updating, retrieving, and deleting compliance information. All endpoints require ARC-0014 authentication.
    /// Enforces network-specific compliance rules for VOI and Aramid networks.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance")]
    public class ComplianceController : ControllerBase
    {
        private readonly IComplianceService _complianceService;
        private readonly ILogger<ComplianceController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceController"/> class.
        /// </summary>
        /// <param name="complianceService">The compliance service</param>
        /// <param name="logger">The logger instance</param>
        public ComplianceController(
            IComplianceService complianceService,
            ILogger<ComplianceController> logger)
        {
            _complianceService = complianceService;
            _logger = logger;
        }

        /// <summary>
        /// Gets compliance metadata for a specific token
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <returns>The compliance metadata</returns>
        [HttpGet("{assetId}")]
        [ProducesResponseType(typeof(ComplianceMetadataResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetComplianceMetadata([FromRoute] ulong assetId)
        {
            try
            {
                var result = await _complianceService.GetMetadataAsync(assetId);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved compliance metadata for asset {AssetId}", assetId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Compliance metadata not found for asset {AssetId}", assetId);
                    return NotFound(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving compliance metadata for asset {AssetId}", assetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceMetadataResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Creates or updates compliance metadata for a token
        /// </summary>
        /// <param name="request">The compliance metadata request</param>
        /// <returns>The created or updated compliance metadata</returns>
        /// <remarks>
        /// This operation emits a metering event for billing analytics with the following details:
        /// - Category: Compliance
        /// - OperationType: Upsert
        /// - Network: From request
        /// - PerformedBy: Authenticated user
        /// - ItemCount: 1
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(ComplianceMetadataResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpsertComplianceMetadata([FromBody] UpsertComplianceMetadataRequest request)
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
                    return Unauthorized(new ComplianceMetadataResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _complianceService.UpsertMetadataAsync(request, createdBy);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Upserted compliance metadata for asset {AssetId} by {CreatedBy}",
                        request.AssetId,
                        createdBy);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError(
                        "Failed to upsert compliance metadata for asset {AssetId}: {Error}",
                        request.AssetId,
                        result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception upserting compliance metadata for asset {AssetId}", request.AssetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceMetadataResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Deletes compliance metadata for a specific token
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <returns>The result of the deletion operation</returns>
        /// <remarks>
        /// This operation emits a metering event for billing analytics with the following details:
        /// - Category: Compliance
        /// - OperationType: Delete
        /// - Network: Not available
        /// - PerformedBy: Not available
        /// - ItemCount: 1
        /// </remarks>
        [HttpDelete("{assetId}")]
        [ProducesResponseType(typeof(ComplianceMetadataResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteComplianceMetadata([FromRoute] ulong assetId)
        {
            try
            {
                var result = await _complianceService.DeleteMetadataAsync(assetId);

                if (result.Success)
                {
                    _logger.LogInformation("Deleted compliance metadata for asset {AssetId}", assetId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Compliance metadata not found for asset {AssetId}", assetId);
                    return NotFound(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception deleting compliance metadata for asset {AssetId}", assetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceMetadataResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lists compliance metadata with optional filtering
        /// </summary>
        /// <param name="complianceStatus">Optional filter by compliance status</param>
        /// <param name="verificationStatus">Optional filter by verification status</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20, max: 100)</param>
        /// <returns>List of compliance metadata entries with pagination</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ComplianceMetadataListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListComplianceMetadata(
            [FromQuery] ComplianceStatus? complianceStatus = null,
            [FromQuery] VerificationStatus? verificationStatus = null,
            [FromQuery] string? network = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var request = new ListComplianceMetadataRequest
                {
                    ComplianceStatus = complianceStatus,
                    VerificationStatus = verificationStatus,
                    Network = network,
                    Page = page,
                    PageSize = Math.Min(pageSize, 100) // Cap at 100
                };

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _complianceService.ListMetadataAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Listed {Count} compliance metadata entries", result.Metadata.Count);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to list compliance metadata: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing compliance metadata");
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceMetadataListResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets audit log for compliance operations with optional filtering
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="actionType">Optional filter by action type</param>
        /// <param name="performedBy">Optional filter by user who performed the action</param>
        /// <param name="success">Optional filter by operation result (success/failure)</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 50, max: 100)</param>
        /// <returns>Audit log entries with pagination and retention policy metadata</returns>
        /// <remarks>
        /// This endpoint provides access to immutable audit logs for compliance reporting and incident investigations.
        /// All compliance operations (create, update, delete, read, list) are logged with timestamps, actors, and results.
        /// 
        /// **Retention Policy**: Audit logs are retained for a minimum of 7 years to comply with MICA regulations.
        /// All entries are immutable and cannot be modified or deleted.
        /// 
        /// **Use Cases**:
        /// - Regulatory compliance reporting
        /// - Incident investigations
        /// - Access audits
        /// - Change tracking
        /// 
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("audit-log")]
        [ProducesResponseType(typeof(ComplianceAuditLogResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAuditLog(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? network = null,
            [FromQuery] ComplianceActionType? actionType = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] bool? success = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var request = new GetComplianceAuditLogRequest
                {
                    AssetId = assetId,
                    Network = network,
                    ActionType = actionType,
                    PerformedBy = performedBy,
                    Success = success,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = Math.Min(pageSize, 100) // Cap at 100
                };

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _complianceService.GetAuditLogAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved {Count} audit log entries", result.Entries.Count);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve audit log: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving compliance audit log");
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports audit log as CSV for compliance reporting
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="actionType">Optional filter by action type</param>
        /// <param name="performedBy">Optional filter by user who performed the action</param>
        /// <param name="success">Optional filter by operation result (success/failure)</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>CSV file with audit log entries</returns>
        /// <remarks>
        /// Exports audit log entries matching the filter criteria as a CSV file for regulatory compliance reporting.
        /// The CSV includes all audit fields: timestamp, actor, action, asset ID, network, result, and notes.
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
            [FromQuery] string? network = null,
            [FromQuery] ComplianceActionType? actionType = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] bool? success = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var request = new GetComplianceAuditLogRequest
                {
                    AssetId = assetId,
                    Network = network,
                    ActionType = actionType,
                    PerformedBy = performedBy,
                    Success = success,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = 1,
                    PageSize = 10000 // Max records per export
                };

                var result = await _complianceService.GetAuditLogAsync(request);

                if (!result.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessage);
                }

                // Build CSV
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Id,AssetId,Network,ActionType,PerformedBy,PerformedAt,Success,ErrorMessage,OldComplianceStatus,NewComplianceStatus,OldVerificationStatus,NewVerificationStatus,ItemCount,FilterCriteria,Notes");

                foreach (var entry in result.Entries)
                {
                    csv.AppendLine($"\"{entry.Id}\",{entry.AssetId},\"{entry.Network}\",\"{entry.ActionType}\",\"{entry.PerformedBy}\",\"{entry.PerformedAt:O}\",{entry.Success},\"{EscapeCsv(entry.ErrorMessage)}\",\"{entry.OldComplianceStatus}\",\"{entry.NewComplianceStatus}\",\"{entry.OldVerificationStatus}\",\"{entry.NewVerificationStatus}\",{entry.ItemCount},\"{EscapeCsv(entry.FilterCriteria)}\",\"{EscapeCsv(entry.Notes)}\"");
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                var fileName = $"compliance-audit-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

                _logger.LogInformation("Exported {Count} audit log entries to CSV", result.Entries.Count);

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting audit log as CSV");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Internal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Exports audit log as JSON for compliance reporting
        /// </summary>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="actionType">Optional filter by action type</param>
        /// <param name="performedBy">Optional filter by user who performed the action</param>
        /// <param name="success">Optional filter by operation result (success/failure)</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>JSON file with audit log entries</returns>
        /// <remarks>
        /// Exports audit log entries matching the filter criteria as a JSON file for regulatory compliance reporting.
        /// The JSON includes all audit fields and retention policy metadata.
        /// 
        /// **Format**: Standard JSON array
        /// **Encoding**: UTF-8
        /// **Max Records**: 10,000 per export (use pagination for larger datasets)
        /// 
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("audit-log/export/json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ComplianceAuditLogResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportAuditLogJson(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? network = null,
            [FromQuery] ComplianceActionType? actionType = null,
            [FromQuery] string? performedBy = null,
            [FromQuery] bool? success = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var request = new GetComplianceAuditLogRequest
                {
                    AssetId = assetId,
                    Network = network,
                    ActionType = actionType,
                    PerformedBy = performedBy,
                    Success = success,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = 1,
                    PageSize = 10000 // Max records per export
                };

                var result = await _complianceService.GetAuditLogAsync(request);

                if (!result.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }

                _logger.LogInformation("Exported {Count} audit log entries to JSON", result.Entries.Count);

                // Return as downloadable JSON file
                var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                var fileName = $"compliance-audit-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting audit log as JSON");
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets the audit log retention policy metadata
        /// </summary>
        /// <returns>Retention policy information</returns>
        /// <remarks>
        /// Returns metadata about the audit log retention policy including minimum retention period,
        /// regulatory framework, and immutability guarantees.
        /// 
        /// This endpoint is useful for compliance teams to understand data retention policies.
        /// </remarks>
        [HttpGet("audit-log/retention-policy")]
        [ProducesResponseType(typeof(AuditRetentionPolicy), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetRetentionPolicy()
        {
            var policy = new AuditRetentionPolicy
            {
                MinimumRetentionYears = 7,
                RegulatoryFramework = "MICA",
                ImmutableEntries = true,
                Description = "Audit logs are retained for a minimum of 7 years to comply with MICA and other regulatory requirements. All entries are immutable and cannot be modified or deleted."
            };

            return Ok(policy);
        }

        /// <summary>
        /// Escapes special characters in CSV values
        /// </summary>
        /// <param name="value">The value to escape</param>
        /// <returns>Escaped value</returns>
        private string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            
            return value.Replace("\"", "\"\"");
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
    }
}
