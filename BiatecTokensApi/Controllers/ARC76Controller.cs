using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ARC76;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// ARC76 address management endpoints for authenticated users.
    /// </summary>
    /// <remarks>
    /// Provides canonical endpoints for retrieving and verifying the ARC76-derived Algorand address
    /// associated with the authenticated user. No wallet connection is required — the address is
    /// derived deterministically from the user's email and password via the ARC76 standard.
    /// </remarks>
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ApiController]
    [Route("api/v1/arc76")]
    public class ARC76Controller : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ILogger<ARC76Controller> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ARC76Controller"/> class.
        /// </summary>
        public ARC76Controller(
            IAccountService accountService,
            ILogger<ARC76Controller> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }

        /// <summary>
        /// Returns the ARC76-derived Algorand address for the authenticated user.
        /// </summary>
        /// <returns>The user's deterministic Algorand address.</returns>
        /// <remarks>
        /// Returns the canonical Algorand address derived from the authenticated user's credentials
        /// via the ARC76 standard. This address is deterministic: the same email and password
        /// always produce the same address, regardless of when or where the derivation runs.
        ///
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "address": "ALGORAND_ADDRESS_HERE",
        ///   "success": true
        /// }
        /// ```
        /// </remarks>
        [HttpGet("address")]
        [ProducesResponseType(typeof(ARC76AddressResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAddress()
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("ARC76 GetAddress called without valid user ID. CorrelationId={CorrelationId}", correlationId);
                return Unauthorized(new ARC76AddressResponse
                {
                    Success = false,
                    ErrorMessage = "User identity could not be determined",
                    CorrelationId = correlationId
                });
            }

            var accountResponse = await _accountService.GetAddressAsync(userId, correlationId);

            if (!accountResponse.Success)
            {
                return NotFound(new ARC76AddressResponse
                {
                    Success = false,
                    ErrorMessage = accountResponse.ErrorMessage,
                    CorrelationId = correlationId
                });
            }

            return Ok(new ARC76AddressResponse
            {
                Success = true,
                Address = accountResponse.AlgorandAddress,
                CorrelationId = correlationId
            });
        }

        /// <summary>
        /// Verifies whether the provided Algorand address matches the authenticated user's ARC76-derived address.
        /// </summary>
        /// <param name="request">Request containing the address to verify.</param>
        /// <returns>Verification result indicating whether the address matches.</returns>
        /// <remarks>
        /// Accepts an Algorand address and returns whether it matches the authenticated user's
        /// deterministically derived ARC76 address. This endpoint is used by frontend E2E tests
        /// and client applications to validate that the correct address is stored or displayed.
        ///
        /// **Sample Request:**
        /// ```json
        /// {
        ///   "address": "ALGORAND_ADDRESS_HERE"
        /// }
        /// ```
        ///
        /// **Sample Response (match):**
        /// ```json
        /// {
        ///   "verified": true,
        ///   "success": true
        /// }
        /// ```
        ///
        /// **Sample Response (no match):**
        /// ```json
        /// {
        ///   "verified": false,
        ///   "success": true
        /// }
        /// ```
        /// </remarks>
        [HttpPost("verify")]
        [ProducesResponseType(typeof(ARC76VerifyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> VerifyAddress([FromBody] ARC76VerifyRequest request)
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("ARC76 VerifyAddress called without valid user ID. CorrelationId={CorrelationId}", correlationId);
                return Unauthorized(new ARC76VerifyResponse
                {
                    Success = false,
                    Verified = false,
                    ErrorMessage = "User identity could not be determined",
                    CorrelationId = correlationId
                });
            }

            if (string.IsNullOrWhiteSpace(request?.Address))
            {
                return BadRequest(new ARC76VerifyResponse
                {
                    Success = false,
                    Verified = false,
                    ErrorMessage = "Address is required",
                    CorrelationId = correlationId
                });
            }

            var accountResponse = await _accountService.GetAddressAsync(userId, correlationId);

            if (!accountResponse.Success)
            {
                return Ok(new ARC76VerifyResponse
                {
                    Success = true,
                    Verified = false,
                    CorrelationId = correlationId
                });
            }

            var userAddress = accountResponse.AlgorandAddress?.Trim() ?? string.Empty;
            var providedAddress = request.Address.Trim();
            var verified = !string.IsNullOrEmpty(userAddress) &&
                string.Equals(userAddress, providedAddress, StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation("ARC76 address verification. UserId={UserId}, Verified={Verified}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(userId), verified, correlationId);

            return Ok(new ARC76VerifyResponse
            {
                Success = true,
                Verified = verified,
                CorrelationId = correlationId
            });
        }
    }
}
