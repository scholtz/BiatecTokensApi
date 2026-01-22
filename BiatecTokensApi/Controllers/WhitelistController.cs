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
                    PageSize = Math.Min(pageSize, 100) // Cap at 100
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
                    PageSize = Math.Min(pageSize, 100) // Cap at 100
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
                var result = await _whitelistService.ValidateTransferAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Validated transfer for asset {AssetId} from {From} to {To}: {Result}",
                        request.AssetId, request.FromAddress, request.ToAddress,
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
    }
}
