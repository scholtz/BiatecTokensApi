using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Portfolio;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Portfolio Intelligence API v1
    /// </summary>
    /// <remarks>
    /// Provides consolidated token portfolio intelligence for a wallet address including:
    /// - Per-holding risk levels with confidence indicators
    /// - Wallet compatibility status for the requested network
    /// - Action readiness signals before transaction execution
    /// - Discovered portfolio opportunities (governance, staking, compliance actions)
    /// - Degraded-mode responses when upstream sources are unavailable
    ///
    /// All evaluations are deterministic: identical inputs always produce identical outputs.
    /// Partial upstream failures return best-effort intelligence with explicit degraded-state flags.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/portfolio-intelligence")]
    [Produces("application/json")]
    public class PortfolioIntelligenceController : ControllerBase
    {
        private readonly IPortfolioIntelligenceService _portfolioService;
        private readonly ILogger<PortfolioIntelligenceController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="PortfolioIntelligenceController"/>.
        /// </summary>
        public PortfolioIntelligenceController(
            IPortfolioIntelligenceService portfolioService,
            ILogger<PortfolioIntelligenceController> logger)
        {
            _portfolioService = portfolioService;
            _logger = logger;
        }

        /// <summary>
        /// Returns consolidated portfolio intelligence for a wallet
        /// </summary>
        /// <param name="request">Portfolio intelligence request specifying wallet, network, and options.</param>
        /// <returns>Enriched portfolio intelligence with risk levels, confidence, wallet compatibility, and opportunities.</returns>
        /// <remarks>
        /// **Response highlights:**
        /// - `aggregateRiskLevel`: Worst-case risk across all holdings (Low / Medium / High / Unknown)
        /// - `riskConfidence`: How confident the system is in the risk assessment (High / Medium / Low)
        /// - `walletCompatibility`: Whether the wallet address is compatible with the network
        /// - `actionReadiness`: Whether the user can safely proceed with portfolio actions
        /// - `holdings`: Per-holding breakdown with risk signals and confidence indicators
        /// - `opportunities`: Discovered opportunities sorted by priority
        /// - `isDegraded`: True when one or more upstream sources failed
        /// </remarks>
        [HttpPost("evaluate")]
        [ProducesResponseType(typeof(PortfolioIntelligenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EvaluatePortfolio([FromBody] PortfolioIntelligenceRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Request body is required."
                    });
                }

                if (string.IsNullOrWhiteSpace(request.WalletAddress))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "WalletAddress is required."
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Network))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Network is required."
                    });
                }

                _logger.LogInformation(
                    "Portfolio intelligence evaluation requested: Wallet={Wallet}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(request.WalletAddress),
                    LoggingHelper.SanitizeLogInput(request.Network));

                var result = await _portfolioService.GetPortfolioIntelligenceAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in portfolio intelligence evaluation");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred while evaluating portfolio intelligence."
                });
            }
        }

        /// <summary>
        /// Evaluates wallet compatibility with a network and optional token standard
        /// </summary>
        /// <param name="walletAddress">Wallet address to check.</param>
        /// <param name="network">Network identifier (e.g., "algorand-mainnet", "base-mainnet").</param>
        /// <param name="tokenStandard">Optional token standard to validate wallet type support.</param>
        /// <returns>Compatibility status and descriptive message.</returns>
        [HttpGet("wallet-compatibility")]
        [ProducesResponseType(typeof(WalletCompatibilityResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public IActionResult GetWalletCompatibility(
            [FromQuery] string walletAddress,
            [FromQuery] string network,
            [FromQuery] string? tokenStandard = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(walletAddress))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "walletAddress query parameter is required."
                    });
                }

                if (string.IsNullOrWhiteSpace(network))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "network query parameter is required."
                    });
                }

                _logger.LogInformation(
                    "Wallet compatibility check: Wallet={Wallet}, Network={Network}, Standard={Standard}",
                    LoggingHelper.SanitizeLogInput(walletAddress),
                    LoggingHelper.SanitizeLogInput(network),
                    LoggingHelper.SanitizeLogInput(tokenStandard ?? "any"));

                var (status, message) = _portfolioService.EvaluateWalletCompatibility(
                    walletAddress, network, tokenStandard);

                return Ok(new WalletCompatibilityResult
                {
                    WalletAddress = walletAddress,
                    Network = network,
                    TokenStandard = tokenStandard,
                    Status = status,
                    Message = message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in wallet compatibility check");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred while checking wallet compatibility."
                });
            }
        }
    }

    /// <summary>
    /// Result model for a wallet compatibility check.
    /// </summary>
    public class WalletCompatibilityResult
    {
        /// <summary>The wallet address that was evaluated.</summary>
        public string WalletAddress { get; set; } = string.Empty;

        /// <summary>The network that was evaluated against.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Optional token standard that was validated.</summary>
        public string? TokenStandard { get; set; }

        /// <summary>Compatibility status.</summary>
        public WalletCompatibilityStatus Status { get; set; }

        /// <summary>Human-readable description of the compatibility state.</summary>
        public string Message { get; set; } = string.Empty;
    }
}
