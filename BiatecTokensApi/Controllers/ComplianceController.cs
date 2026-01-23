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
    }
}
