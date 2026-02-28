using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Account;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides account management endpoints for ARC76-derived Algorand accounts
    /// </summary>
    /// <remarks>
    /// All endpoints require JWT Bearer authentication. The user's Algorand account is derived
    /// deterministically from their email and password using the ARC76 standard.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/account")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ILogger<AccountController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AccountController"/> class.
        /// </summary>
        public AccountController(
            IAccountService accountService,
            ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }

        /// <summary>
        /// Returns the ARC76-derived Algorand address for the authenticated user
        /// </summary>
        /// <returns>The user's Algorand address</returns>
        /// <remarks>
        /// Returns the deterministic Algorand address derived from the user's credentials via ARC76.
        /// The same email/password combination always produces the same Algorand address.
        ///
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "algorandAddress": "ALGORAND_ADDRESS_HERE",
        ///   "derivationStandard": "ARC76"
        /// }
        /// ```
        /// </remarks>
        [HttpGet("address")]
        [ProducesResponseType(typeof(AccountAddressResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAddress()
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new AccountAddressResponse
                {
                    Success = false,
                    ErrorMessage = "User identity could not be determined",
                    CorrelationId = correlationId
                });
            }

            var response = await _accountService.GetAddressAsync(userId, correlationId);

            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Requests testnet ALGO funding for the authenticated user's Algorand address
        /// </summary>
        /// <param name="request">Fund request specifying the target testnet network</param>
        /// <returns>Funding request status</returns>
        /// <remarks>
        /// Calls the Algorand testnet faucet on behalf of the authenticated user.
        /// Only available on testnet networks. For mainnet, users must acquire ALGO through exchanges.
        ///
        /// **Sample Request:**
        /// ```json
        /// {
        ///   "network": "algorand-testnet"
        /// }
        /// ```
        ///
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "algorandAddress": "ALGORAND_ADDRESS_HERE",
        ///   "network": "algorand-testnet",
        ///   "message": "Testnet funding request submitted successfully"
        /// }
        /// ```
        /// </remarks>
        [HttpPost("fund")]
        [ProducesResponseType(typeof(AccountFundResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Fund([FromBody] AccountFundRequest request)
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new AccountFundResponse
                {
                    Success = false,
                    ErrorMessage = "User identity could not be determined",
                    CorrelationId = correlationId
                });
            }

            var network = request?.Network ?? "algorand-testnet";
            var response = await _accountService.RequestTestnetFundingAsync(userId, network, correlationId);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Returns ALGO and token balances for the authenticated user's Algorand account
        /// </summary>
        /// <param name="network">Target network to query (default: algorand-mainnet)</param>
        /// <returns>Account balance information</returns>
        /// <remarks>
        /// Returns the current ALGO balance and all token holdings for the authenticated user's
        /// ARC76-derived Algorand address.
        ///
        /// **Sample Response:**
        /// ```json
        /// {
        ///   "success": true,
        ///   "algorandAddress": "ALGORAND_ADDRESS_HERE",
        ///   "algoBalance": 10.5,
        ///   "algoBalanceMicroAlgos": 10500000,
        ///   "tokenBalances": [],
        ///   "network": "algorand-mainnet"
        /// }
        /// ```
        /// </remarks>
        [HttpGet("balance")]
        [ProducesResponseType(typeof(AccountBalanceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBalance([FromQuery] string network = "algorand-mainnet")
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new AccountBalanceResponse
                {
                    Success = false,
                    ErrorMessage = "User identity could not be determined",
                    CorrelationId = correlationId
                });
            }

            var response = await _accountService.GetBalanceAsync(userId, network, correlationId);

            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }
    }
}
