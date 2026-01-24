using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for managing issuer profiles for MICA compliance
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/issuer")]
    public class IssuerController : ControllerBase
    {
        private readonly IComplianceService _complianceService;
        private readonly ILogger<IssuerController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="IssuerController"/> class.
        /// </summary>
        /// <param name="complianceService">The compliance service</param>
        /// <param name="logger">The logger instance</param>
        public IssuerController(
            IComplianceService complianceService,
            ILogger<IssuerController> logger)
        {
            _complianceService = complianceService;
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
