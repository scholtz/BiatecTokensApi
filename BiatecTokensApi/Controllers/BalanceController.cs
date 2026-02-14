using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Balance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for querying token balances across blockchain networks
    /// </summary>
    /// <remarks>
    /// Provides endpoints for querying token balances for addresses on Algorand and EVM chains.
    /// Supports both single token and multi-token balance queries.
    /// </remarks>
    [ApiController]
    [Route("api/v1/balance")]
    [Authorize]
    public class BalanceController : ControllerBase
    {
        private readonly IBalanceService _balanceService;
        private readonly ILogger<BalanceController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BalanceController"/> class
        /// </summary>
        public BalanceController(
            IBalanceService balanceService,
            ILogger<BalanceController> logger)
        {
            _balanceService = balanceService;
            _logger = logger;
        }

        /// <summary>
        /// Query token balance for a specific address
        /// </summary>
        /// <param name="tokenIdentifier">Token identifier (asset ID for Algorand, contract address for EVM)</param>
        /// <param name="address">Address to query balance for</param>
        /// <param name="chain">Blockchain network identifier (e.g., "algorand-mainnet", "base-mainnet")</param>
        /// <returns>Token balance information including formatted balance and metadata</returns>
        /// <remarks>
        /// Example request: GET /api/v1/balance?tokenIdentifier=123456&amp;address=ADDR...&amp;chain=algorand-mainnet
        /// 
        /// Supported chains:
        /// - algorand-mainnet, algorand-testnet, algorand-betanet
        /// - voimain-v1.0, voitest-v1
        /// - aramidmain-v1, aramidtest-v1
        /// - base-mainnet (Chain ID: 8453)
        /// - ethereum-mainnet (Chain ID: 1)
        /// 
        /// The response includes:
        /// - Raw balance (as string to handle large numbers)
        /// - Formatted balance (with decimals applied)
        /// - Token metadata (name, symbol, decimals)
        /// - Opt-in status (Algorand only)
        /// - Frozen status (if applicable)
        /// 
        /// This endpoint is useful for:
        /// - Wallet integrations displaying token holdings
        /// - Compliance checks verifying token balances
        /// - Frontend applications showing user portfolios
        /// - Transaction validation before submitting transfers
        /// </remarks>
        [AllowAnonymous]
        [HttpGet]
        [SwaggerOperation(
            Summary = "Get token balance for address",
            Description = "Queries the blockchain for token balance of a specific address"
        )]
        [SwaggerResponse(200, "Successfully retrieved balance", typeof(BalanceQueryResponse))]
        [SwaggerResponse(400, "Invalid request parameters", typeof(BalanceQueryResponse))]
        [SwaggerResponse(404, "Token or address not found", typeof(BalanceQueryResponse))]
        [SwaggerResponse(502, "Blockchain connection error", typeof(BalanceQueryResponse))]
        public async Task<IActionResult> GetBalance(
            [FromQuery] string tokenIdentifier,
            [FromQuery] string address,
            [FromQuery] string chain)
        {
            try
            {
                _logger.LogInformation(
                    "Balance query request: Token={Token}, Address={Address}, Chain={Chain}",
                    LoggingHelper.SanitizeLogInput(tokenIdentifier),
                    LoggingHelper.SanitizeLogInput(address),
                    LoggingHelper.SanitizeLogInput(chain)
                );

                // Validate input
                if (string.IsNullOrWhiteSpace(tokenIdentifier))
                {
                    return BadRequest(new BalanceQueryResponse
                    {
                        Success = false,
                        ErrorMessage = "Token identifier is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(address))
                {
                    return BadRequest(new BalanceQueryResponse
                    {
                        Success = false,
                        ErrorMessage = "Address is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(chain))
                {
                    return BadRequest(new BalanceQueryResponse
                    {
                        Success = false,
                        ErrorMessage = "Chain is required"
                    });
                }

                var request = new BalanceQueryRequest
                {
                    TokenIdentifier = tokenIdentifier,
                    Address = address,
                    Chain = chain
                };

                var response = await _balanceService.GetBalanceAsync(request);

                if (!response.Success)
                {
                    _logger.LogWarning(
                        "Balance query failed: {ErrorCode} - {ErrorMessage}",
                        LoggingHelper.SanitizeLogInput(response.ErrorCode),
                        LoggingHelper.SanitizeLogInput(response.ErrorMessage)
                    );

                    if (response.ErrorCode == "INVALID_NETWORK" || response.ErrorCode == "INVALID_REQUEST")
                    {
                        return BadRequest(response);
                    }

                    return StatusCode(502, response);
                }

                _logger.LogInformation(
                    "Balance query successful: Balance={Balance}, FormattedBalance={FormattedBalance}",
                    LoggingHelper.SanitizeLogInput(response.Balance),
                    LoggingHelper.SanitizeLogInput(response.FormattedBalance)
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in balance query: {Message}", ex.Message);
                return StatusCode(500, new BalanceQueryResponse
                {
                    Success = false,
                    ErrorMessage = "Internal server error"
                });
            }
        }

        /// <summary>
        /// Query multiple token balances for a single address
        /// </summary>
        /// <param name="request">Multi-balance query request containing address, chain, and optional token list</param>
        /// <returns>List of token balances for the specified address</returns>
        /// <remarks>
        /// Example request:
        /// POST /api/v1/balance/multi
        /// ```json
        /// {
        ///   "address": "ADDR...",
        ///   "chain": "algorand-mainnet",
        ///   "tokenIdentifiers": ["123456", "789012"],
        ///   "includeZeroBalances": false
        /// }
        /// ```
        /// 
        /// If `tokenIdentifiers` is empty or null, returns all tokens held by the address.
        /// Set `includeZeroBalances` to true to include tokens with zero balance in the response.
        /// 
        /// This endpoint is useful for:
        /// - Portfolio dashboards showing all token holdings
        /// - Bulk balance checks for multiple tokens
        /// - Wallet applications displaying user assets
        /// </remarks>
        [HttpPost("multi")]
        [SwaggerOperation(
            Summary = "Get multiple token balances for address",
            Description = "Queries balances for multiple tokens for a single address"
        )]
        [SwaggerResponse(200, "Successfully retrieved balances", typeof(MultiBalanceQueryResponse))]
        [SwaggerResponse(400, "Invalid request parameters", typeof(MultiBalanceQueryResponse))]
        [SwaggerResponse(502, "Blockchain connection error", typeof(MultiBalanceQueryResponse))]
        public async Task<IActionResult> GetMultipleBalances([FromBody] MultiBalanceQueryRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "Multi-balance query request: Address={Address}, Chain={Chain}, TokenCount={Count}",
                    LoggingHelper.SanitizeLogInput(request.Address),
                    LoggingHelper.SanitizeLogInput(request.Chain),
                    request.TokenIdentifiers?.Count ?? 0
                );

                // Validate input
                if (string.IsNullOrWhiteSpace(request.Address))
                {
                    return BadRequest(new MultiBalanceQueryResponse
                    {
                        Success = false,
                        ErrorMessage = "Address is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Chain))
                {
                    return BadRequest(new MultiBalanceQueryResponse
                    {
                        Success = false,
                        ErrorMessage = "Chain is required"
                    });
                }

                var response = await _balanceService.GetMultipleBalancesAsync(request);

                if (!response.Success)
                {
                    _logger.LogWarning(
                        "Multi-balance query failed: {ErrorCode} - {ErrorMessage}",
                        LoggingHelper.SanitizeLogInput(response.ErrorCode),
                        LoggingHelper.SanitizeLogInput(response.ErrorMessage)
                    );
                    return StatusCode(502, response);
                }

                _logger.LogInformation(
                    "Multi-balance query successful: TotalTokens={TotalTokens}",
                    response.TotalTokens
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in multi-balance query: {Message}", ex.Message);
                return StatusCode(500, new MultiBalanceQueryResponse
                {
                    Success = false,
                    ErrorMessage = "Internal server error"
                });
            }
        }
    }
}
