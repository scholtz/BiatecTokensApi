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
