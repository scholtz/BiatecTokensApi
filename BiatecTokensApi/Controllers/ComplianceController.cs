using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Metering;
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
        private readonly ISubscriptionMeteringService _meteringService;
        
        /// <summary>
        /// Maximum number of records to export in a single request
        /// </summary>
        private const int MaxExportRecords = 10000;

        /// <summary>
        /// Maximum number of audit entries per category in compliance reports
        /// </summary>
        private const int MaxAuditEntriesPerCategory = 1000;

        /// <summary>
        /// Maximum page size for compliance reports
        /// </summary>
        private const int MaxReportPageSize = 100;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceController"/> class.
        /// </summary>
        /// <param name="complianceService">The compliance service</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="meteringService">The subscription metering service</param>
        public ComplianceController(
            IComplianceService complianceService,
            ILogger<ComplianceController> logger,
            ISubscriptionMeteringService meteringService)
        {
            _complianceService = complianceService;
            _logger = logger;
            _meteringService = meteringService;
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
        /// **Metering**: This operation emits a subscription metering event for billing analytics with:
        /// - Category: Compliance
        /// - OperationType: Export
        /// - Metadata: export format (csv), export type (auditLog), row count, time range
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
                    PageSize = MaxExportRecords // Max records per export
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

                // Emit metering event for export
                _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                {
                    Category = MeteringCategory.Compliance,
                    OperationType = MeteringOperationType.Export,
                    AssetId = assetId ?? 0,
                    Network = network ?? "all",
                    PerformedBy = GetActorId(),
                    ItemCount = result.Entries.Count,
                    Metadata = CreateAuditLogExportMetadata("csv", result.Entries.Count, fromDate, toDate, actionType, performedBy)
                });

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
        /// **Metering**: This operation emits a subscription metering event for billing analytics with:
        /// - Category: Compliance
        /// - OperationType: Export
        /// - Metadata: export format (json), export type (auditLog), row count, time range
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
                    PageSize = MaxExportRecords // Max records per export
                };

                var result = await _complianceService.GetAuditLogAsync(request);

                if (!result.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }

                _logger.LogInformation("Exported {Count} audit log entries to JSON", result.Entries.Count);

                // Emit metering event for export
                _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                {
                    Category = MeteringCategory.Compliance,
                    OperationType = MeteringOperationType.Export,
                    AssetId = assetId ?? 0,
                    Network = network ?? "all",
                    PerformedBy = GetActorId(),
                    ItemCount = result.Entries.Count,
                    Metadata = CreateAuditLogExportMetadata("json", result.Entries.Count, fromDate, toDate, actionType, performedBy)
                });

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
        /// Validates token configuration against MICA/RWA compliance rules
        /// </summary>
        /// <param name="request">The validation request containing token configuration</param>
        /// <returns>Validation result with errors and warnings</returns>
        /// <remarks>
        /// This endpoint validates token configuration against MICA/RWA compliance rules used by frontend presets.
        /// It checks for:
        /// - Missing whitelist or issuer controls
        /// - KYC verification requirements
        /// - Jurisdiction and regulatory framework requirements
        /// - Network-specific compliance rules (VOI, Aramid)
        /// - Security token specific requirements
        /// 
        /// The endpoint returns actionable validation errors that must be fixed and warnings that should be reviewed.
        /// Use this endpoint before token deployment to ensure compliance with applicable regulations.
        /// </remarks>
        [HttpPost("validate-preset")]
        [ProducesResponseType(typeof(ValidateTokenPresetResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateTokenPreset([FromBody] ValidateTokenPresetRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _complianceService.ValidateTokenPresetAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Validated token preset: IsValid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}",
                        result.IsValid,
                        result.Errors.Count,
                        result.Warnings.Count);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to validate token preset: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception validating token preset");
                return StatusCode(StatusCodes.Status500InternalServerError, new ValidateTokenPresetResponse
                {
                    Success = false,
                    IsValid = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Creates a new compliance attestation
        /// </summary>
        /// <param name="request">The attestation creation request</param>
        /// <returns>The created attestation</returns>
        /// <remarks>
        /// This operation creates a wallet-level compliance attestation that provides cryptographic proof 
        /// of compliance verification. Attestations are used for MICA/RWA workflows to maintain persistent 
        /// compliance audit trails for issuers.
        /// 
        /// **Key Features**:
        /// - Links attestation to specific wallet and token
        /// - Stores cryptographic proof hash (IPFS CID, SHA-256, etc.)
        /// - Supports multiple attestation types (KYC, AML, Accreditation)
        /// - Optional expiration dates for time-limited attestations
        /// 
        /// This operation emits a metering event for billing analytics.
        /// 
        /// Requires ARC-0014 authentication.
        /// </remarks>
        [HttpPost("attestations")]
        [ProducesResponseType(typeof(ComplianceAttestationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateAttestation([FromBody] CreateComplianceAttestationRequest request)
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
                    return Unauthorized(new ComplianceAttestationResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _complianceService.CreateAttestationAsync(request, createdBy);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Created attestation for wallet {WalletAddress} and asset {AssetId} by {CreatedBy}",
                        request.WalletAddress,
                        request.AssetId,
                        createdBy);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError(
                        "Failed to create attestation for wallet {WalletAddress}: {Error}",
                        request.WalletAddress,
                        result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating attestation for wallet {WalletAddress}", request.WalletAddress);
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceAttestationResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets a compliance attestation by ID
        /// </summary>
        /// <param name="id">The attestation ID</param>
        /// <returns>The attestation details</returns>
        /// <remarks>
        /// Retrieves a specific compliance attestation by its unique identifier.
        /// The attestation includes wallet address, issuer, proof hash, verification status, and metadata.
        /// 
        /// Expired attestations are automatically marked with Expired status when retrieved.
        /// 
        /// Requires ARC-0014 authentication.
        /// </remarks>
        [HttpGet("attestations/{id}")]
        [ProducesResponseType(typeof(ComplianceAttestationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAttestation([FromRoute] string id)
        {
            try
            {
                var result = await _complianceService.GetAttestationAsync(id);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved attestation {Id}", id);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Attestation {Id} not found", id);
                    return NotFound(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving attestation {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceAttestationResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lists compliance attestations with optional filtering
        /// </summary>
        /// <param name="walletAddress">Optional filter by wallet address</param>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="issuerAddress">Optional filter by issuer address</param>
        /// <param name="verificationStatus">Optional filter by verification status</param>
        /// <param name="attestationType">Optional filter by attestation type</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="excludeExpired">Optional filter to exclude expired attestations</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20, max: 100)</param>
        /// <returns>List of compliance attestations with pagination</returns>
        /// <remarks>
        /// Lists compliance attestations with flexible filtering options.
        /// 
        /// **Common Use Cases**:
        /// - List all attestations for a specific wallet
        /// - List all attestations for a specific token
        /// - List attestations by issuer
        /// - Filter by verification status (Pending, Verified, Failed, Expired, Revoked)
        /// - Filter by attestation type (KYC, AML, Accreditation, etc.)
        /// 
        /// Note: This is a read operation and does not emit metering events.
        /// 
        /// Requires ARC-0014 authentication.
        /// </remarks>
        [HttpGet("attestations")]
        [ProducesResponseType(typeof(ComplianceAttestationListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListAttestations(
            [FromQuery] string? walletAddress = null,
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? issuerAddress = null,
            [FromQuery] AttestationVerificationStatus? verificationStatus = null,
            [FromQuery] string? attestationType = null,
            [FromQuery] string? network = null,
            [FromQuery] bool? excludeExpired = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var request = new ListComplianceAttestationsRequest
                {
                    WalletAddress = walletAddress,
                    AssetId = assetId,
                    IssuerAddress = issuerAddress,
                    VerificationStatus = verificationStatus,
                    AttestationType = attestationType,
                    Network = network,
                    ExcludeExpired = excludeExpired,
                    Page = page,
                    PageSize = Math.Min(pageSize, 100) // Cap at 100
                };

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _complianceService.ListAttestationsAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Listed {Count} attestations", result.Attestations.Count);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to list attestations: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing attestations");
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceAttestationListResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports attestation audit history as JSON for compliance reporting
        /// </summary>
        /// <param name="walletAddress">Optional filter by wallet address</param>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="issuerAddress">Optional filter by issuer address</param>
        /// <param name="verificationStatus">Optional filter by verification status</param>
        /// <param name="attestationType">Optional filter by attestation type</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="excludeExpired">Optional filter to exclude expired attestations</param>
        /// <param name="fromDate">Optional start date filter (filter by IssuedAt)</param>
        /// <param name="toDate">Optional end date filter (filter by IssuedAt)</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 100, max: 10000)</param>
        /// <returns>JSON file with attestation history</returns>
        /// <remarks>
        /// Exports attestation history matching the filter criteria as a JSON file for regulatory compliance reporting.
        /// The JSON includes all attestation fields: ID, wallet address, asset ID, issuer, proof hash, verification status,
        /// attestation type, network, jurisdiction, regulatory framework, issued date, expiration date, and metadata.
        /// 
        /// **Format**: Standard JSON array of attestation objects
        /// 
        /// **Use Cases**:
        /// - Enterprise audit trail export
        /// - Regulator reporting and disclosure
        /// - Compliance verification for token holders
        /// - Historical attestation analysis
        /// 
        /// **Pagination**: Maximum 10,000 records per export for performance. Use pagination for larger datasets.
        /// 
        /// **Metering**: This operation emits a subscription metering event for billing analytics with:
        /// - Category: Compliance
        /// - OperationType: Export
        /// - Metadata: export format (json), export type (attestations), row count, time range
        /// 
        /// **Filters**: Combine multiple filters to narrow down results (e.g., specific token + date range + wallet).
        /// </remarks>
        [HttpGet("attestations/export/json")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(List<ComplianceAttestation>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportAttestationsJson(
            [FromQuery] string? walletAddress = null,
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? issuerAddress = null,
            [FromQuery] AttestationVerificationStatus? verificationStatus = null,
            [FromQuery] string? attestationType = null,
            [FromQuery] string? network = null,
            [FromQuery] bool? excludeExpired = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            try
            {
                // Limit page size for export
                if (pageSize > MaxExportRecords)
                {
                    pageSize = MaxExportRecords;
                }

                var request = new ListComplianceAttestationsRequest
                {
                    WalletAddress = walletAddress,
                    AssetId = assetId,
                    IssuerAddress = issuerAddress,
                    VerificationStatus = verificationStatus,
                    AttestationType = attestationType,
                    Network = network,
                    ExcludeExpired = excludeExpired,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _complianceService.ListAttestationsAsync(request);

                if (!result.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessage);
                }

                var fileName = $"attestations-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

                _logger.LogInformation("Exported {Count} attestations as JSON", result.Attestations.Count);

                // Emit metering event for export
                _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                {
                    Category = MeteringCategory.Compliance,
                    OperationType = MeteringOperationType.Export,
                    AssetId = assetId ?? 0,
                    Network = network ?? "all",
                    PerformedBy = GetActorId(),
                    ItemCount = result.Attestations.Count,
                    Metadata = CreateAttestationExportMetadata("json", result.Attestations.Count, fromDate, toDate, walletAddress, verificationStatus, attestationType)
                });

                return File(
                    System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(result.Attestations, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })),
                    "application/json",
                    fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting attestations as JSON");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Internal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Exports attestation audit history as CSV for compliance reporting
        /// </summary>
        /// <param name="walletAddress">Optional filter by wallet address</param>
        /// <param name="assetId">Optional filter by asset ID (token ID)</param>
        /// <param name="issuerAddress">Optional filter by issuer address</param>
        /// <param name="verificationStatus">Optional filter by verification status</param>
        /// <param name="attestationType">Optional filter by attestation type</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="excludeExpired">Optional filter to exclude expired attestations</param>
        /// <param name="fromDate">Optional start date filter (filter by IssuedAt)</param>
        /// <param name="toDate">Optional end date filter (filter by IssuedAt)</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 100, max: 10000)</param>
        /// <returns>CSV file with attestation history</returns>
        /// <remarks>
        /// Exports attestation history matching the filter criteria as a CSV file for regulatory compliance reporting.
        /// The CSV includes all attestation fields: ID, wallet address, asset ID, issuer, proof hash, verification status,
        /// attestation type, network, jurisdiction, regulatory framework, issued date, expiration date, and metadata.
        /// 
        /// **Format**: Standard CSV with headers
        /// 
        /// **Use Cases**:
        /// - Enterprise audit trail export
        /// - Regulator reporting and disclosure
        /// - Compliance verification for token holders
        /// - Historical attestation analysis
        /// - Import into Excel or other tools
        /// 
        /// **Pagination**: Maximum 10,000 records per export for performance. Use pagination for larger datasets.
        /// 
        /// **Metering**: This operation emits a subscription metering event for billing analytics with:
        /// - Category: Compliance
        /// - OperationType: Export
        /// - Metadata: export format (csv), export type (attestations), row count, time range
        /// 
        /// **Filters**: Combine multiple filters to narrow down results (e.g., specific token + date range + wallet).
        /// </remarks>
        [HttpGet("attestations/export/csv")]
        [Produces("text/csv")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportAttestationsCsv(
            [FromQuery] string? walletAddress = null,
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? issuerAddress = null,
            [FromQuery] AttestationVerificationStatus? verificationStatus = null,
            [FromQuery] string? attestationType = null,
            [FromQuery] string? network = null,
            [FromQuery] bool? excludeExpired = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            try
            {
                // Limit page size for export
                if (pageSize > MaxExportRecords)
                {
                    pageSize = MaxExportRecords;
                }

                var request = new ListComplianceAttestationsRequest
                {
                    WalletAddress = walletAddress,
                    AssetId = assetId,
                    IssuerAddress = issuerAddress,
                    VerificationStatus = verificationStatus,
                    AttestationType = attestationType,
                    Network = network,
                    ExcludeExpired = excludeExpired,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _complianceService.ListAttestationsAsync(request);

                if (!result.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, result.ErrorMessage);
                }

                // Build CSV
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Id,WalletAddress,AssetId,IssuerAddress,ProofHash,ProofType,VerificationStatus,AttestationType,Network,Jurisdiction,RegulatoryFramework,IssuedAt,ExpiresAt,VerifiedAt,VerifierAddress,Notes,CreatedAt,UpdatedAt,CreatedBy,UpdatedBy");

                foreach (var attestation in result.Attestations)
                {
                    // Format CSV line with proper escaping
                    var line = string.Format(
                        "\"{0}\",\"{1}\",{2},\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\",\"{8}\",\"{9}\",\"{10}\",\"{11}\",\"{12}\",\"{13}\",\"{14}\",\"{15}\",\"{16}\",\"{17}\",\"{18}\",\"{19}\"",
                        attestation.Id,
                        attestation.WalletAddress,
                        attestation.AssetId,
                        attestation.IssuerAddress,
                        EscapeCsv(attestation.ProofHash),
                        EscapeCsv(attestation.ProofType),
                        attestation.VerificationStatus,
                        EscapeCsv(attestation.AttestationType),
                        EscapeCsv(attestation.Network),
                        EscapeCsv(attestation.Jurisdiction),
                        EscapeCsv(attestation.RegulatoryFramework),
                        attestation.IssuedAt.ToString("O"),
                        attestation.ExpiresAt?.ToString("O") ?? "",
                        attestation.VerifiedAt?.ToString("O") ?? "",
                        EscapeCsv(attestation.VerifierAddress),
                        EscapeCsv(attestation.Notes),
                        attestation.CreatedAt.ToString("O"),
                        attestation.UpdatedAt?.ToString("O") ?? "",
                        attestation.CreatedBy,
                        EscapeCsv(attestation.UpdatedBy)
                    );
                    csv.AppendLine(line);
                }

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                var fileName = $"attestations-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

                _logger.LogInformation("Exported {Count} attestations as CSV", result.Attestations.Count);

                // Emit metering event for export
                _meteringService.EmitMeteringEvent(new SubscriptionMeteringEvent
                {
                    Category = MeteringCategory.Compliance,
                    OperationType = MeteringOperationType.Export,
                    AssetId = assetId ?? 0,
                    Network = network ?? "all",
                    PerformedBy = GetActorId(),
                    ItemCount = result.Attestations.Count,
                    Metadata = CreateAttestationExportMetadata("csv", result.Attestations.Count, fromDate, toDate, walletAddress, verificationStatus, attestationType)
                });

                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting attestations as CSV");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Internal error: {ex.Message}");
            }
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
        /// Gets the actor ID from the current authenticated user
        /// </summary>
        /// <returns>Actor ID or "unknown" if not available</returns>
        private string GetActorId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        }

        /// <summary>
        /// Creates metering metadata for audit log exports
        /// </summary>
        private Dictionary<string, string> CreateAuditLogExportMetadata(
            string exportFormat,
            int rowCount,
            DateTime? fromDate,
            DateTime? toDate,
            ComplianceActionType? actionType,
            string? performedByFilter)
        {
            return new Dictionary<string, string>
            {
                { "exportFormat", exportFormat },
                { "exportType", "auditLog" },
                { "rowCount", rowCount.ToString() },
                { "fromDate", fromDate?.ToString("O") ?? "none" },
                { "toDate", toDate?.ToString("O") ?? "none" },
                { "actionType", actionType?.ToString() ?? "all" },
                { "performedByFilter", performedByFilter ?? "all" }
            };
        }

        /// <summary>
        /// Creates metering metadata for attestation exports
        /// </summary>
        private Dictionary<string, string> CreateAttestationExportMetadata(
            string exportFormat,
            int rowCount,
            DateTime? fromDate,
            DateTime? toDate,
            string? walletAddress,
            AttestationVerificationStatus? verificationStatus,
            string? attestationType)
        {
            return new Dictionary<string, string>
            {
                { "exportFormat", exportFormat },
                { "exportType", "attestations" },
                { "rowCount", rowCount.ToString() },
                { "fromDate", fromDate?.ToString("O") ?? "none" },
                { "toDate", toDate?.ToString("O") ?? "none" },
                { "walletAddress", walletAddress ?? "all" },
                { "verificationStatus", verificationStatus?.ToString() ?? "all" },
                { "attestationType", attestationType ?? "all" }
            };
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
        /// Generates a signed compliance attestation package for MICA audits
        /// </summary>
        /// <param name="request">The attestation package request</param>
        /// <returns>Signed attestation package with issuer metadata, token details, and compliance status</returns>
        /// <remarks>
        /// This endpoint generates verifiable audit artifacts for regulators and enterprise issuers.
        /// The attestation package includes:
        /// - Issuer metadata (creator address, network information)
        /// - Token details (asset ID, compliance metadata)
        /// - Whitelist policy information (if applicable)
        /// - Latest compliance status and verification status
        /// - All attestations within the specified date range
        /// - Deterministic content hash for verification
        /// - Signature metadata for audit trail
        /// 
        /// **Supported Formats**:
        /// - json: Structured JSON package (default)
        /// - pdf: PDF document (future enhancement)
        /// 
        /// **MICA Compliance**: This endpoint aligns with MICA reporting requirements by providing:
        /// - Complete audit trail of compliance attestations
        /// - Verifiable cryptographic signatures
        /// - Regulatory framework and jurisdiction information
        /// - KYC/AML verification status
        /// 
        /// **Use Cases**:
        /// - Regulatory audit submissions
        /// - Enterprise compliance reporting
        /// - Investor disclosure packages
        /// - Quarterly/Annual compliance reviews
        /// 
        /// This operation emits a metering event for billing analytics.
        /// 
        /// Requires ARC-0014 authentication.
        /// </remarks>
        [HttpPost("attestation")]
        [ProducesResponseType(typeof(AttestationPackageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateAttestationPackage([FromBody] GenerateAttestationPackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var requestedBy = GetUserAddress();

                if (string.IsNullOrEmpty(requestedBy))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new AttestationPackageResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _complianceService.GenerateAttestationPackageAsync(request, requestedBy);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Generated attestation package for token {TokenId} by {RequestedBy}, format: {Format}",
                        request.TokenId,
                        requestedBy,
                        request.Format);

                    // If PDF format is requested, return as file
                    if (string.Equals(request.Format, "pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        // PDF generation would be implemented here
                        // For now, return error indicating PDF is not yet supported
                        return StatusCode(StatusCodes.Status501NotImplemented, new AttestationPackageResponse
                        {
                            Success = false,
                            ErrorMessage = "PDF format is not yet implemented. Please use 'json' format."
                        });
                    }

                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to generate attestation package: {Error}", result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception generating attestation package");
                return StatusCode(StatusCodes.Status500InternalServerError, new AttestationPackageResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Generates a comprehensive compliance report for VOI/Aramid tokens
        /// </summary>
        /// <param name="assetId">Optional filter by specific asset ID</param>
        /// <param name="network">Optional filter by network (voimain-v1.0, aramidmain-v1.0)</param>
        /// <param name="fromDate">Optional start date for audit events</param>
        /// <param name="toDate">Optional end date for audit events</param>
        /// <param name="includeWhitelistDetails">Include detailed whitelist information</param>
        /// <param name="includeTransferAudits">Include recent transfer validation audit events</param>
        /// <param name="includeComplianceAudits">Include compliance metadata changes audit log</param>
        /// <param name="maxAuditEntriesPerCategory">Maximum number of audit entries per category</param>
        /// <param name="page">Page number for pagination</param>
        /// <param name="pageSize">Page size for pagination</param>
        /// <returns>Comprehensive compliance report with subscription information</returns>
        /// <remarks>
        /// This endpoint provides enterprise-grade compliance reporting for VOI/Aramid networks.
        /// It aggregates compliance metadata, whitelist statistics, and audit logs to support
        /// MICA dashboard requirements and regulatory reporting.
        /// 
        /// The report includes:
        /// - Compliance metadata (KYC, verification status, regulatory framework)
        /// - Whitelist summary statistics (active/revoked addresses, KYC counts)
        /// - Compliance audit log (metadata changes)
        /// - Whitelist audit log (address additions/removals)
        /// - Transfer validation audit log (allowed/denied transfers)
        /// - Compliance health score (0-100)
        /// - VOI/Aramid specific compliance status
        /// - Warnings and recommendations
        /// - Subscription tier information
        /// 
        /// This operation emits a metering event for billing analytics.
        /// 
        /// Example usage:
        /// - VOI network report: GET /api/v1/compliance/report?network=voimain-v1.0
        /// - Aramid network report: GET /api/v1/compliance/report?network=aramidmain-v1.0
        /// - Specific token: GET /api/v1/compliance/report?assetId=12345
        /// - Date range: GET /api/v1/compliance/report?fromDate=2026-01-01&amp;toDate=2026-01-31
        /// </remarks>
        [HttpGet("report")]
        [ProducesResponseType(typeof(TokenComplianceReportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetComplianceReport(
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? network = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] bool includeWhitelistDetails = true,
            [FromQuery] bool includeTransferAudits = true,
            [FromQuery] bool includeComplianceAudits = true,
            [FromQuery] int maxAuditEntriesPerCategory = 100,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var requestedBy = GetUserAddress();

                if (string.IsNullOrEmpty(requestedBy))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new TokenComplianceReportResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var request = new GetTokenComplianceReportRequest
                {
                    AssetId = assetId,
                    Network = network,
                    FromDate = fromDate,
                    ToDate = toDate,
                    IncludeWhitelistDetails = includeWhitelistDetails,
                    IncludeTransferAudits = includeTransferAudits,
                    IncludeComplianceAudits = includeComplianceAudits,
                    MaxAuditEntriesPerCategory = Math.Min(maxAuditEntriesPerCategory, MaxAuditEntriesPerCategory),
                    Page = page,
                    PageSize = Math.Min(pageSize, MaxReportPageSize)
                };

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _complianceService.GetComplianceReportAsync(request, requestedBy);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Generated compliance report with {Count} tokens for {RequestedBy}, network: {Network}",
                        result.Tokens.Count, requestedBy, network ?? "All");
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to generate compliance report: {Error}", result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception generating compliance report");
                return StatusCode(StatusCodes.Status500InternalServerError, new TokenComplianceReportResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        // Phase 3: Blacklist Management Endpoints

        /// <summary>
        /// Adds an address to the blacklist
        /// </summary>
        /// <param name="request">The blacklist entry request</param>
        /// <returns>The created blacklist entry</returns>
        [HttpPost("blacklist")]
        [ProducesResponseType(typeof(BlacklistResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddBlacklistEntry([FromBody] AddBlacklistEntryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdBy = GetUserAddress();
                var result = await _complianceService.AddBlacklistEntryAsync(request, createdBy);

                if (result.Success)
                {
                    _logger.LogInformation("Added blacklist entry for address {Address}", request.Address);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to add blacklist entry: {Error}", result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception adding blacklist entry");
                return StatusCode(StatusCodes.Status500InternalServerError, new BlacklistResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Checks if an address is blacklisted
        /// </summary>
        /// <param name="address">The address to check</param>
        /// <param name="assetId">Optional asset ID</param>
        /// <param name="network">Optional network filter</param>
        /// <returns>Blacklist check result</returns>
        [HttpGet("blacklist/check")]
        [ProducesResponseType(typeof(BlacklistCheckResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CheckBlacklist(
            [FromQuery] string address,
            [FromQuery] ulong? assetId = null,
            [FromQuery] string? network = null)
        {
            try
            {
                var request = new CheckBlacklistRequest
                {
                    Address = address,
                    AssetId = assetId,
                    Network = network
                };

                var result = await _complianceService.CheckBlacklistAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Checked blacklist for address {Address}: {IsBlacklisted}", address, result.IsBlacklisted);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to check blacklist: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception checking blacklist");
                return StatusCode(StatusCodes.Status500InternalServerError, new BlacklistCheckResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lists blacklist entries with filtering
        /// </summary>
        /// <param name="address">Optional address filter</param>
        /// <param name="assetId">Optional asset ID filter</param>
        /// <param name="category">Optional category filter</param>
        /// <param name="status">Optional status filter</param>
        /// <param name="network">Optional network filter</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20, max: 100)</param>
        /// <returns>List of blacklist entries</returns>
        [HttpGet("blacklist")]
        [ProducesResponseType(typeof(BlacklistListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListBlacklistEntries(
            [FromQuery] string? address = null,
            [FromQuery] ulong? assetId = null,
            [FromQuery] BlacklistCategory? category = null,
            [FromQuery] BlacklistStatus? status = null,
            [FromQuery] string? network = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var request = new ListBlacklistEntriesRequest
                {
                    Address = address,
                    AssetId = assetId,
                    Category = category,
                    Status = status,
                    Network = network,
                    Page = page,
                    PageSize = Math.Min(pageSize, 100)
                };

                var result = await _complianceService.ListBlacklistEntriesAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Listed {Count} blacklist entries", result.Entries.Count);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to list blacklist entries: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing blacklist entries");
                return StatusCode(StatusCodes.Status500InternalServerError, new BlacklistListResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Removes a blacklist entry
        /// </summary>
        /// <param name="id">The entry ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("blacklist/{id}")]
        [ProducesResponseType(typeof(BlacklistResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteBlacklistEntry([FromRoute] string id)
        {
            try
            {
                var result = await _complianceService.DeleteBlacklistEntryAsync(id);

                if (result.Success)
                {
                    _logger.LogInformation("Deleted blacklist entry {Id}", id);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Blacklist entry {Id} not found", id);
                    return NotFound(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception deleting blacklist entry {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new BlacklistResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Validates a proposed transfer against compliance rules
        /// </summary>
        /// <param name="request">The transfer validation request</param>
        /// <returns>Validation result</returns>
        [HttpPost("validate-transfer")]
        [ProducesResponseType(typeof(TransferValidationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateTransfer([FromBody] ValidateComplianceTransferRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _complianceService.ValidateTransferAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Validated transfer for asset {AssetId}: {CanTransfer}", request.AssetId, result.CanTransfer);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to validate transfer: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception validating transfer");
                return StatusCode(StatusCodes.Status500InternalServerError, new TransferValidationResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        // Phase 4: MICA Checklist and Health Endpoints

        /// <summary>
        /// Gets MICA compliance checklist for a token
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <returns>MICA compliance checklist</returns>
        [HttpGet("{assetId}/mica-checklist")]
        [ProducesResponseType(typeof(MicaComplianceChecklistResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMicaComplianceChecklist([FromRoute] ulong assetId)
        {
            try
            {
                var result = await _complianceService.GetMicaComplianceChecklistAsync(assetId);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved MICA checklist for asset {AssetId}", assetId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to get MICA checklist: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting MICA checklist for asset {AssetId}", assetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new MicaComplianceChecklistResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets aggregate compliance health for an issuer
        /// </summary>
        /// <param name="issuerAddress">Optional issuer address filter (if not provided, uses authenticated user)</param>
        /// <param name="network">Optional network filter</param>
        /// <returns>Compliance health metrics</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(ComplianceHealthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetComplianceHealth(
            [FromQuery] string? issuerAddress = null,
            [FromQuery] string? network = null)
        {
            try
            {
                // Use authenticated user if issuerAddress not provided
                var targetAddress = issuerAddress ?? GetUserAddress();

                if (string.IsNullOrEmpty(targetAddress))
                {
                    return Unauthorized(new ComplianceHealthResponse
                    {
                        Success = false,
                        ErrorMessage = "Issuer address required"
                    });
                }

                var result = await _complianceService.GetComplianceHealthAsync(targetAddress, network);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved compliance health for issuer {IssuerAddress}", targetAddress);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to get compliance health: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting compliance health");
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceHealthResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Generates a signed compliance evidence bundle (ZIP) for auditors
        /// </summary>
        /// <param name="request">The evidence bundle request containing asset ID and filtering options</param>
        /// <returns>ZIP file containing audit logs, whitelist history, compliance metadata, and manifest with checksums</returns>
        /// <remarks>
        /// Generates a comprehensive compliance evidence bundle for MICA/RWA audit purposes.
        /// 
        /// **Bundle Contents:**
        /// - **manifest.json**: Complete manifest with SHA256 checksums of all files
        /// - **README.txt**: Human-readable documentation of bundle contents
        /// - **metadata/compliance_metadata.json**: Token compliance metadata (KYC, jurisdiction, regulatory framework)
        /// - **whitelist/current_entries.json**: Current whitelist entries
        /// - **whitelist/audit_log.json**: Complete whitelist operation history
        /// - **audit_logs/compliance_operations.json**: Compliance metadata operation logs
        /// - **audit_logs/transfer_validations.json**: Transfer validation records
        /// - **policy/retention_policy.json**: 7-year MICA retention policy details
        /// 
        /// **Manifest Features:**
        /// - Bundle ID for tracking and audit trail
        /// - Generation timestamp (UTC)
        /// - Generator's Algorand address
        /// - SHA256 checksums for all included files
        /// - SHA256 checksum for entire bundle
        /// - Summary statistics (record counts, date ranges, categories)
        /// - Network information (VOI, Aramid, etc.)
        /// 
        /// **Use Cases:**
        /// - MICA compliance audits
        /// - Regulatory investigations
        /// - External auditor submissions
        /// - Procurement compliance evidence
        /// - Long-term archival and retention
        /// 
        /// **Access Control:**
        /// - Requires ARC-0014 authentication
        /// - Recommended for compliance officers and auditors only
        /// - Export event is logged in audit trail
        /// - Metering event emitted for subscription tracking
        /// 
        /// **Filtering:**
        /// - Optional date range filtering (fromDate, toDate)
        /// - Selective inclusion of evidence types
        /// - Asset-specific data only
        /// 
        /// **Security:**
        /// - All files include SHA256 checksums for integrity verification
        /// - Bundle includes overall SHA256 checksum
        /// - Timestamp ensures temporal ordering
        /// - Immutable source data (append-only logs)
        /// 
        /// **File Format:**
        /// - Filename: compliance-evidence-{assetId}-{timestamp}.zip
        /// - Content-Type: application/zip
        /// - All JSON files are UTF-8 encoded with pretty-printing
        /// </remarks>
        [HttpPost("evidence-bundle")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateComplianceEvidenceBundle(
            [FromBody] GenerateComplianceEvidenceBundleRequest request)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Compliance evidence bundle requested for asset {AssetId} by {UserAddress}",
                    request.AssetId, userAddress);

                // Validate request
                if (request.AssetId == 0)
                {
                    return BadRequest(new ComplianceEvidenceBundleResponse
                    {
                        Success = false,
                        ErrorMessage = "Asset ID is required"
                    });
                }

                if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate > request.ToDate)
                {
                    return BadRequest(new ComplianceEvidenceBundleResponse
                    {
                        Success = false,
                        ErrorMessage = "FromDate must be before ToDate"
                    });
                }

                // Generate the bundle
                var result = await _complianceService.GenerateComplianceEvidenceBundleAsync(request, userAddress);

                if (result.Success && result.ZipContent != null && result.FileName != null)
                {
                    _logger.LogInformation(
                        "Generated compliance evidence bundle for asset {AssetId} ({Size} bytes, {FileCount} files)",
                        request.AssetId, result.ZipContent.Length, result.BundleMetadata?.Files.Count ?? 0);

                    return File(result.ZipContent, "application/zip", result.FileName);
                }
                else
                {
                    _logger.LogError("Failed to generate compliance evidence bundle: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception generating compliance evidence bundle");
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceEvidenceBundleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets per-network compliance metadata for all supported blockchain networks
        /// </summary>
        /// <returns>List of networks with their compliance requirements and flags</returns>
        /// <remarks>
        /// This endpoint returns compliance metadata for each supported blockchain network,
        /// including MICA readiness status, whitelisting requirements, and regulatory specifications.
        /// 
        /// Use this to display network-specific compliance indicators in the frontend and help
        /// users understand compliance requirements when deploying tokens to different networks.
        /// 
        /// Response includes cache headers (CacheDurationSeconds) to optimize frontend performance.
        /// 
        /// Networks included:
        /// - VOI Mainnet (voimain-v1.0): MICA-ready, requires jurisdiction
        /// - Aramid Mainnet (aramidmain-v1.0): MICA-ready, requires regulatory framework
        /// - Algorand Mainnet (mainnet-v1.0): MICA-ready, optional compliance
        /// - Algorand Testnet (testnet-v1.0): Development only
        /// - Algorand Betanet (betanet-v1.0): Testing only
        /// </remarks>
        [HttpGet("networks")]
        [ProducesResponseType(typeof(NetworkComplianceMetadataResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetNetworkComplianceMetadata()
        {
            try
            {
                var result = await _complianceService.GetNetworkComplianceMetadataAsync();

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved network compliance metadata for {Count} networks", result.Networks.Count);
                    
                    // Add cache control header as requested in acceptance criteria
                    Response.Headers.Append("Cache-Control", $"public, max-age={result.CacheDurationSeconds}");
                    
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve network compliance metadata: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving network compliance metadata");
                return StatusCode(StatusCodes.Status500InternalServerError, new NetworkComplianceMetadataResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets aggregated compliance dashboard metrics for enterprise reporting
        /// </summary>
        /// <param name="network">Optional filter by network (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, etc.)</param>
        /// <param name="tokenStandard">Optional filter by token standard (ASA, ARC3, ARC200, ARC1400, ERC20)</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <param name="includeAssetBreakdown">Include detailed asset breakdown (default: false)</param>
        /// <param name="topRestrictionsCount">Maximum number of top restriction reasons to include (default: 10)</param>
        /// <returns>Aggregated compliance dashboard metrics</returns>
        /// <remarks>
        /// This endpoint provides enterprise-grade compliance dashboard aggregations for scheduled exports
        /// and compliance posture tracking. Supports filtering by network, token standard, and date range.
        /// 
        /// **Use Cases:**
        /// - Enterprise compliance dashboards
        /// - Scheduled compliance exports
        /// - Compliance posture tracking across assets
        /// - Regulatory reporting automation
        /// - MICA readiness assessment
        /// 
        /// **Aggregations Provided:**
        /// - MICA readiness statistics (compliant, nearly compliant, in progress, non-compliant)
        /// - Whitelist status metrics (enabled/disabled, active/revoked/suspended addresses)
        /// - Jurisdiction coverage (distribution, unique jurisdictions)
        /// - Compliant vs restricted asset counts
        /// - Top restriction reasons with occurrence counts
        /// - Token standard and network distribution
        /// 
        /// **Filters:**
        /// All filters are optional and can be combined for precise queries.
        /// 
        /// **Asset Breakdown:**
        /// Set includeAssetBreakdown=true to get detailed per-asset compliance information.
        /// This is useful for drill-down analysis but increases response size.
        /// 
        /// **Security:**
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("dashboard-aggregation")]
        [ProducesResponseType(typeof(ComplianceDashboardAggregationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDashboardAggregation(
            [FromQuery] string? network = null,
            [FromQuery] string? tokenStandard = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] bool includeAssetBreakdown = false,
            [FromQuery] int topRestrictionsCount = 10)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Dashboard aggregation requested by {UserAddress}: Network={Network}, TokenStandard={TokenStandard}",
                    userAddress, network, tokenStandard);

                var request = new GetComplianceDashboardAggregationRequest
                {
                    Network = network,
                    TokenStandard = tokenStandard,
                    FromDate = fromDate,
                    ToDate = toDate,
                    IncludeAssetBreakdown = includeAssetBreakdown,
                    TopRestrictionsCount = topRestrictionsCount
                };

                var result = await _complianceService.GetDashboardAggregationAsync(request, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved dashboard aggregation for user {UserAddress}: {TotalAssets} assets",
                        userAddress, result.Metrics.TotalAssets);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve dashboard aggregation: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving dashboard aggregation");
                return StatusCode(StatusCodes.Status500InternalServerError, new ComplianceDashboardAggregationResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports compliance dashboard aggregation data as CSV
        /// </summary>
        /// <param name="network">Optional filter by network (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, etc.)</param>
        /// <param name="tokenStandard">Optional filter by token standard (ASA, ARC3, ARC200, ARC1400, ERC20)</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <param name="includeAssetBreakdown">Include detailed asset breakdown (default: false)</param>
        /// <param name="topRestrictionsCount">Maximum number of top restriction reasons to include (default: 10)</param>
        /// <returns>CSV file with compliance dashboard aggregation data</returns>
        /// <remarks>
        /// Exports aggregated compliance metrics in CSV format for enterprise reporting and scheduled exports.
        /// 
        /// **CSV Format:**
        /// - UTF-8 encoding with proper CSV escaping
        /// - Summary metrics section with all aggregations
        /// - MICA readiness section
        /// - Whitelist status section
        /// - Jurisdiction coverage section
        /// - Compliance counts section
        /// - Top restriction reasons section
        /// - Token standard distribution section
        /// - Network distribution section
        /// - Optional detailed asset breakdown section
        /// 
        /// **Use Cases:**
        /// - Scheduled compliance exports for enterprise systems
        /// - Executive reporting (spreadsheet analysis)
        /// - Compliance posture tracking over time
        /// - Cross-asset compliance analysis
        /// - Regulatory audit preparation
        /// 
        /// **Filename:**
        /// - Format: compliance-dashboard-{timestamp}.csv
        /// - Timestamp in yyyyMMddHHmmss format
        /// 
        /// **Security:**
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("dashboard-aggregation/csv")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportDashboardAggregationCsv(
            [FromQuery] string? network = null,
            [FromQuery] string? tokenStandard = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] bool includeAssetBreakdown = false,
            [FromQuery] int topRestrictionsCount = 10)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Dashboard aggregation CSV export requested by {UserAddress}: Network={Network}, TokenStandard={TokenStandard}",
                    userAddress, network, tokenStandard);

                var request = new GetComplianceDashboardAggregationRequest
                {
                    Network = network,
                    TokenStandard = tokenStandard,
                    FromDate = fromDate,
                    ToDate = toDate,
                    IncludeAssetBreakdown = includeAssetBreakdown,
                    TopRestrictionsCount = topRestrictionsCount
                };

                var csv = await _complianceService.ExportDashboardAggregationCsvAsync(request, userAddress);
                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                var fileName = $"compliance-dashboard-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

                _logger.LogInformation("Exported dashboard aggregation as CSV for user {UserAddress}", userAddress);
                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting dashboard aggregation as CSV");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports compliance dashboard aggregation data as JSON
        /// </summary>
        /// <param name="network">Optional filter by network (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, etc.)</param>
        /// <param name="tokenStandard">Optional filter by token standard (ASA, ARC3, ARC200, ARC1400, ERC20)</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <param name="includeAssetBreakdown">Include detailed asset breakdown (default: false)</param>
        /// <param name="topRestrictionsCount">Maximum number of top restriction reasons to include (default: 10)</param>
        /// <returns>JSON file with compliance dashboard aggregation data</returns>
        /// <remarks>
        /// Exports aggregated compliance metrics in JSON format for programmatic access and API integrations.
        /// 
        /// **JSON Format:**
        /// - Pretty-printed JSON with camelCase property names
        /// - Includes full aggregation response structure with metadata
        /// - Complete metrics with all distribution data
        /// - Optional detailed asset breakdown
        /// 
        /// **Use Cases:**
        /// - Programmatic dashboard data feeds
        /// - API integrations with compliance management systems
        /// - Automated compliance monitoring
        /// - Data archival for long-term tracking
        /// - Real-time compliance posture analysis
        /// 
        /// **Filename:**
        /// - Format: compliance-dashboard-{timestamp}.json
        /// - Timestamp in yyyyMMddHHmmss format
        /// 
        /// **Security:**
        /// Requires ARC-0014 authentication. Recommended for compliance and admin roles only.
        /// </remarks>
        [HttpGet("dashboard-aggregation/json")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportDashboardAggregationJson(
            [FromQuery] string? network = null,
            [FromQuery] string? tokenStandard = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] bool includeAssetBreakdown = false,
            [FromQuery] int topRestrictionsCount = 10)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Dashboard aggregation JSON export requested by {UserAddress}: Network={Network}, TokenStandard={TokenStandard}",
                    userAddress, network, tokenStandard);

                var request = new GetComplianceDashboardAggregationRequest
                {
                    Network = network,
                    TokenStandard = tokenStandard,
                    FromDate = fromDate,
                    ToDate = toDate,
                    IncludeAssetBreakdown = includeAssetBreakdown,
                    TopRestrictionsCount = topRestrictionsCount
                };

                var json = await _complianceService.ExportDashboardAggregationJsonAsync(request, userAddress);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                var fileName = $"compliance-dashboard-{DateTime.UtcNow:yyyyMMddHHmmss}.json";

                _logger.LogInformation("Exported dashboard aggregation as JSON for user {UserAddress}", userAddress);
                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting dashboard aggregation as JSON");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = $"Internal error: {ex.Message}"
                });
            }
        }
    }
}
