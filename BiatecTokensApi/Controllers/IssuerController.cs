using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for managing issuer profiles and audit trails for RWA/MICA compliance
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/issuer")]
    public class IssuerController : ControllerBase
    {
        private readonly IComplianceService _complianceService;
        private readonly IEnterpriseAuditService _auditService;
        private readonly ILogger<IssuerController> _logger;

        /// <summary>
        /// Maximum number of records to export in a single request
        /// </summary>
        private const int MaxExportRecords = 10000;

        /// <summary>
        /// Initializes a new instance of the <see cref="IssuerController"/> class.
        /// </summary>
        /// <param name="complianceService">The compliance service</param>
        /// <param name="auditService">The enterprise audit service</param>
        /// <param name="logger">The logger instance</param>
        public IssuerController(
            IComplianceService complianceService,
            IEnterpriseAuditService auditService,
            ILogger<IssuerController> logger)
        {
            _complianceService = complianceService;
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>
        /// Gets an issuer profile
        /// </summary>
        /// <param name="issuerAddress">The issuer's Algorand address</param>
        /// <returns>The issuer profile</returns>
        [HttpGet("profile/{issuerAddress}")]
        [ProducesResponseType(typeof(IssuerProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetIssuerProfile([FromRoute] string issuerAddress)
        {
            try
            {
                var result = await _complianceService.GetIssuerProfileAsync(issuerAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved issuer profile for {IssuerAddress}", issuerAddress);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Issuer profile not found for {IssuerAddress}", issuerAddress);
                    return NotFound(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving issuer profile for {IssuerAddress}", issuerAddress);
                return StatusCode(StatusCodes.Status500InternalServerError, new IssuerProfileResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Creates or updates an issuer profile
        /// </summary>
        /// <param name="request">The issuer profile request</param>
        /// <returns>The created or updated issuer profile</returns>
        [HttpPost("profile")]
        [ProducesResponseType(typeof(IssuerProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpsertIssuerProfile([FromBody] UpsertIssuerProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var issuerAddress = GetUserAddress();

                if (string.IsNullOrEmpty(issuerAddress))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new IssuerProfileResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _complianceService.UpsertIssuerProfileAsync(request, issuerAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Upserted issuer profile for {IssuerAddress}", issuerAddress);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to upsert issuer profile: {Error}", result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception upserting issuer profile");
                return StatusCode(StatusCodes.Status500InternalServerError, new IssuerProfileResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets issuer verification status
        /// </summary>
        /// <param name="issuerAddress">The issuer's Algorand address</param>
        /// <returns>The issuer verification status and score</returns>
        [HttpGet("verification/{issuerAddress}")]
        [ProducesResponseType(typeof(IssuerVerificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetIssuerVerification([FromRoute] string issuerAddress)
        {
            try
            {
                var result = await _complianceService.GetIssuerVerificationAsync(issuerAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved issuer verification for {IssuerAddress}", issuerAddress);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Issuer verification not found for {IssuerAddress}", issuerAddress);
                    return NotFound(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving issuer verification for {IssuerAddress}", issuerAddress);
                return StatusCode(StatusCodes.Status500InternalServerError, new IssuerVerificationResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lists assets for an issuer
        /// </summary>
        /// <param name="issuerAddress">The issuer's Algorand address</param>
        /// <param name="network">Optional network filter</param>
        /// <param name="complianceStatus">Optional compliance status filter</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20, max: 100)</param>
        /// <returns>List of asset IDs for the issuer</returns>
        [HttpGet("{issuerAddress}/assets")]
        [ProducesResponseType(typeof(IssuerAssetsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListIssuerAssets(
            [FromRoute] string issuerAddress,
            [FromQuery] string? network = null,
            [FromQuery] ComplianceStatus? complianceStatus = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var request = new ListIssuerAssetsRequest
                {
                    Network = network,
                    ComplianceStatus = complianceStatus,
                    Page = page,
                    PageSize = Math.Min(pageSize, 100)
                };

                var result = await _complianceService.ListIssuerAssetsAsync(issuerAddress, request);

                if (result.Success)
                {
                    _logger.LogInformation("Listed {Count} assets for issuer {IssuerAddress}", result.AssetIds.Count, issuerAddress);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to list issuer assets: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing issuer assets for {IssuerAddress}", issuerAddress);
                return StatusCode(StatusCodes.Status500InternalServerError, new IssuerAssetsResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports RWA issuer compliance audit trail for authorized issuer's tokens
        /// </summary>
        /// <param name="assetId">Required asset ID (token ID) - issuer must own this token</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <param name="actionType">Optional filter by action type (Add, Update, Remove, TransferValidation, etc.)</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 50, max: 100)</param>
        /// <returns>JSON response with audit trail including whitelist enforcement events</returns>
        /// <remarks>
        /// This endpoint enables RWA issuers to export compliance audit trails for their own tokens.
        /// Includes all audit events including whitelist enforcement blocks for regulatory compliance.
        /// 
        /// **Authorization:**
        /// - Issuer must be authenticated via ARC-0014
        /// - Issuer address must match the token creator/issuer address
        /// 
        /// **Use Cases:**
        /// - RWA issuer compliance workflows
        /// - Regulatory audit preparation
        /// - Whitelist enforcement tracking
        /// - Token operations audit history
        /// 
        /// **Response includes:**
        /// - Asset operations (issuance, transfers, burns, mints)
        /// - Whitelist enforcement events (allowed and denied transfers)
        /// - Compliance status changes
        /// - Timestamp, actor, action, target address, and result for each event
        /// 
        /// **Pagination:**
        /// Use page and pageSize parameters to navigate large result sets.
        /// Maximum pageSize is 100 records per page.
        /// </remarks>
        [HttpGet("audit-trail")]
        [ProducesResponseType(typeof(EnterpriseAuditLogResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetIssuerAuditTrail(
            [FromQuery] ulong? assetId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? actionType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var issuerAddress = GetUserAddress();

                if (string.IsNullOrEmpty(issuerAddress))
                {
                    _logger.LogWarning("Failed to get issuer address from authentication context");
                    return Unauthorized(new EnterpriseAuditLogResponse
                    {
                        Success = false,
                        ErrorMessage = "Issuer address not found in authentication context"
                    });
                }

                // Validate that assetId is provided
                if (!assetId.HasValue)
                {
                    _logger.LogWarning("AssetId is required for issuer audit trail export");
                    return BadRequest(new EnterpriseAuditLogResponse
                    {
                        Success = false,
                        ErrorMessage = "AssetId is required"
                    });
                }

                // Verify issuer ownership of the asset
                var isOwner = await _complianceService.VerifyIssuerOwnsAssetAsync(issuerAddress, assetId.Value);
                if (!isOwner)
                {
                    _logger.LogWarning("Issuer {IssuerAddress} attempted to export audit trail for asset {AssetId} they don't own",
                        issuerAddress, assetId.Value);
                    return StatusCode(StatusCodes.Status403Forbidden, new EnterpriseAuditLogResponse
                    {
                        Success = false,
                        ErrorMessage = $"Access denied: You are not authorized to export audit trail for asset {assetId.Value}"
                    });
                }

                _logger.LogInformation("Issuer audit trail requested by {IssuerAddress} for AssetId={AssetId}",
                    issuerAddress, assetId);

                var request = new GetEnterpriseAuditLogRequest
                {
                    AssetId = assetId,
                    ActionType = actionType,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _auditService.GetAuditLogAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved {Count} audit entries for issuer {IssuerAddress}, asset {AssetId}",
                        result.Entries.Count, issuerAddress, assetId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve issuer audit trail: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving issuer audit trail");
                return StatusCode(StatusCodes.Status500InternalServerError, new EnterpriseAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports RWA issuer compliance audit trail as CSV
        /// </summary>
        /// <param name="assetId">Required asset ID (token ID) - issuer must own this token</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <param name="actionType">Optional filter by action type</param>
        /// <returns>CSV file with audit trail including whitelist enforcement events</returns>
        /// <remarks>
        /// Exports up to 10,000 audit trail entries in CSV format for RWA issuer compliance reporting.
        /// 
        /// **Authorization:**
        /// - Issuer must be authenticated via ARC-0014
        /// - Issuer address must match the token creator/issuer address
        /// 
        /// **CSV Format:**
        /// - UTF-8 encoding with proper CSV escaping
        /// - Header row: Id, AssetId, Network, Category, ActionType, PerformedBy, PerformedAt, Success, 
        ///   ErrorMessage, AffectedAddress, OldStatus, NewStatus, Notes, ToAddress, TransferAllowed, 
        ///   DenialReason, Amount, Role, ItemCount, SourceSystem, CorrelationId
        /// - One row per audit event
        /// - Timestamp in ISO 8601 format
        /// 
        /// **Use Cases:**
        /// - RWA compliance reporting
        /// - Regulatory audit submissions
        /// - Excel/spreadsheet analysis
        /// - Long-term archival
        /// 
        /// **Filename:**
        /// - Format: issuer-audit-trail-{assetId}-{timestamp}.csv
        /// </remarks>
        [HttpGet("audit-trail/csv")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportIssuerAuditTrailCsv(
            [FromQuery] ulong? assetId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? actionType = null)
        {
            try
            {
                var issuerAddress = GetUserAddress();

                if (string.IsNullOrEmpty(issuerAddress))
                {
                    _logger.LogWarning("Failed to get issuer address from authentication context");
                    return Unauthorized(new
                    {
                        success = false,
                        errorMessage = "Issuer address not found in authentication context"
                    });
                }

                // Validate that assetId is provided
                if (!assetId.HasValue)
                {
                    _logger.LogWarning("AssetId is required for issuer audit trail CSV export");
                    return BadRequest(new
                    {
                        success = false,
                        errorMessage = "AssetId is required"
                    });
                }

                // Verify issuer ownership of the asset
                var isOwner = await _complianceService.VerifyIssuerOwnsAssetAsync(issuerAddress, assetId.Value);
                if (!isOwner)
                {
                    _logger.LogWarning("Issuer {IssuerAddress} attempted to export CSV audit trail for asset {AssetId} they don't own",
                        issuerAddress, assetId.Value);
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        errorMessage = $"Access denied: You are not authorized to export audit trail for asset {assetId.Value}"
                    });
                }

                _logger.LogInformation("Issuer audit trail CSV export requested by {IssuerAddress} for AssetId={AssetId}",
                    issuerAddress, assetId);

                var request = new GetEnterpriseAuditLogRequest
                {
                    AssetId = assetId,
                    ActionType = actionType,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = 1,
                    PageSize = MaxExportRecords
                };

                var csv = await _auditService.ExportAuditLogCsvAsync(request, MaxExportRecords);
                var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
                var fileName = $"issuer-audit-trail-{assetId}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

                _logger.LogInformation("Exported issuer audit trail as CSV for {IssuerAddress}, asset {AssetId}",
                    issuerAddress, assetId);
                return File(bytes, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting issuer audit trail as CSV");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Exports RWA issuer compliance audit trail as JSON
        /// </summary>
        /// <param name="assetId">Required asset ID (token ID) - issuer must own this token</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <param name="actionType">Optional filter by action type</param>
        /// <returns>JSON file with audit trail and metadata</returns>
        /// <remarks>
        /// Exports up to 10,000 audit trail entries in JSON format for RWA issuer compliance reporting.
        /// 
        /// **Authorization:**
        /// - Issuer must be authenticated via ARC-0014
        /// - Issuer address must match the token creator/issuer address
        /// 
        /// **JSON Format:**
        /// - Pretty-printed JSON with camelCase property names
        /// - Includes full response structure with metadata
        /// - Contains retention policy and summary statistics
        /// - Timestamp in ISO 8601 format
        /// 
        /// **Use Cases:**
        /// - RWA compliance reporting
        /// - Programmatic audit log analysis
        /// - Integration with compliance management systems
        /// - Data archival for long-term storage
        /// 
        /// **Filename:**
        /// - Format: issuer-audit-trail-{assetId}-{timestamp}.json
        /// </remarks>
        [HttpGet("audit-trail/json")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportIssuerAuditTrailJson(
            [FromQuery] ulong? assetId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? actionType = null)
        {
            try
            {
                var issuerAddress = GetUserAddress();

                if (string.IsNullOrEmpty(issuerAddress))
                {
                    _logger.LogWarning("Failed to get issuer address from authentication context");
                    return Unauthorized(new
                    {
                        success = false,
                        errorMessage = "Issuer address not found in authentication context"
                    });
                }

                // Validate that assetId is provided
                if (!assetId.HasValue)
                {
                    _logger.LogWarning("AssetId is required for issuer audit trail JSON export");
                    return BadRequest(new
                    {
                        success = false,
                        errorMessage = "AssetId is required"
                    });
                }

                // Verify issuer ownership of the asset
                var isOwner = await _complianceService.VerifyIssuerOwnsAssetAsync(issuerAddress, assetId.Value);
                if (!isOwner)
                {
                    _logger.LogWarning("Issuer {IssuerAddress} attempted to export JSON audit trail for asset {AssetId} they don't own",
                        issuerAddress, assetId.Value);
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        success = false,
                        errorMessage = $"Access denied: You are not authorized to export audit trail for asset {assetId.Value}"
                    });
                }

                _logger.LogInformation("Issuer audit trail JSON export requested by {IssuerAddress} for AssetId={AssetId}",
                    issuerAddress, assetId);

                var request = new GetEnterpriseAuditLogRequest
                {
                    AssetId = assetId,
                    ActionType = actionType,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = 1,
                    PageSize = MaxExportRecords
                };

                var json = await _auditService.ExportAuditLogJsonAsync(request, MaxExportRecords);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                var fileName = $"issuer-audit-trail-{assetId}-{DateTime.UtcNow:yyyyMMddHHmmss}.json";

                _logger.LogInformation("Exported issuer audit trail as JSON for {IssuerAddress}, asset {AssetId}",
                    issuerAddress, assetId);
                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting issuer audit trail as JSON");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = $"Internal error: {ex.Message}"
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
    }
}
