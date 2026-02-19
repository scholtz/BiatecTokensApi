using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenOperationsIntelligence;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Token Operations Intelligence API v1
    /// </summary>
    /// <remarks>
    /// Provides consolidated operational intelligence for deployed tokens including:
    /// - Token health assessments from deterministic policy evaluators
    /// - Lifecycle recommendations with reason codes and rationale
    /// - Normalized token-affecting event summaries with actor attribution
    /// - Contract version metadata for schema evolution tracking
    /// - Graceful degraded-mode responses when upstream sources are unavailable
    ///
    /// All evaluations are deterministic: identical inputs always produce identical outputs.
    /// Partial upstream failures return best-effort intelligence with explicit degraded-state flags.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/operations-intelligence")]
    [Produces("application/json")]
    public class TokenOperationsIntelligenceController : ControllerBase
    {
        private readonly ITokenOperationsIntelligenceService _intelligenceService;
        private readonly ILogger<TokenOperationsIntelligenceController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="TokenOperationsIntelligenceController"/>.
        /// </summary>
        public TokenOperationsIntelligenceController(
            ITokenOperationsIntelligenceService intelligenceService,
            ILogger<TokenOperationsIntelligenceController> logger)
        {
            _intelligenceService = intelligenceService;
            _logger = logger;
        }

        /// <summary>
        /// Returns consolidated operations intelligence for a token
        /// </summary>
        /// <param name="request">Intelligence request specifying the token and evaluation options</param>
        /// <returns>Consolidated response with health, recommendations, events, and contract version</returns>
        /// <remarks>
        /// **Response Structure:**
        /// - `health`: Aggregated token health from all policy dimensions
        /// - `recommendations`: Ordered lifecycle recommendations with reason codes (highest priority first)
        /// - `events`: Recent normalized token-affecting events with actor, timestamp, category, and impact
        /// - `contractVersion`: API schema version and backward-compatibility metadata
        /// - `isDegraded`: True when one or more upstream sources failed but partial intelligence is available
        /// - `degradedSources`: Which upstream sources failed (for operator visibility)
        ///
        /// **Policy Dimensions Evaluated:**
        /// - `MintAuthority`: Mint authority posture and supply control
        /// - `MetadataCompleteness`: Token metadata presence and validity
        /// - `TreasuryMovement`: Anomalous treasury movement detection
        /// - `OwnershipConsistency`: Deployment and ownership record consistency
        ///
        /// **Caching:**
        /// Health assessments are cached for 5 minutes per token+network combination.
        /// The `healthFromCache` flag indicates when cached data is returned.
        ///
        /// **Degraded Mode:**
        /// If any upstream source fails, the endpoint returns 200 with `isDegraded=true`
        /// and `degradedSources` listing the failed sources, rather than returning an error.
        /// This ensures consumers always receive best-effort intelligence.
        ///
        /// **Example Request:**
        /// ```json
        /// POST /api/v1/operations-intelligence/evaluate
        /// {
        ///   "assetId": 1234567,
        ///   "network": "voimain-v1.0",
        ///   "maxEvents": 10,
        ///   "includeEventDetails": false
        /// }
        /// ```
        /// </remarks>
        [HttpPost("evaluate")]
        [ProducesResponseType(typeof(TokenOperationsIntelligenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Evaluate([FromBody] TokenOperationsIntelligenceRequest request)
        {
            try
            {
                if (request.AssetId == 0)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "AssetId must be a positive integer.",
                        RemediationHint = "Provide a valid token asset ID.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Network))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Network is required.",
                        RemediationHint = "Provide a valid network identifier (e.g., voimain-v1.0, mainnet-v1.0).",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                if (request.MaxEvents < 1 || request.MaxEvents > 50)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "MaxEvents must be between 1 and 50.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var response = await _intelligenceService.GetOperationsIntelligenceAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error in operations intelligence: AssetId={AssetId}, Network={Network}",
                    request.AssetId,
                    LoggingHelper.SanitizeLogInput(request.Network ?? string.Empty));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred during operations intelligence evaluation.",
                    RemediationHint = "Retry the request. If the problem persists, contact support.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        /// <summary>
        /// Returns the health assessment for a token across all policy dimensions
        /// </summary>
        /// <param name="assetId">Token asset ID</param>
        /// <param name="network">Network identifier</param>
        /// <param name="dimensions">Optional comma-separated policy dimensions to evaluate</param>
        /// <returns>Token health assessment</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(TokenHealthAssessment), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetHealth(
            [FromQuery] ulong assetId,
            [FromQuery] string network,
            [FromQuery] string? dimensions = null)
        {
            try
            {
                if (assetId == 0)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "AssetId must be a positive integer.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                if (string.IsNullOrWhiteSpace(network))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Network is required.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var dimensionList = string.IsNullOrWhiteSpace(dimensions)
                    ? null
                    : dimensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var health = await _intelligenceService.EvaluateHealthAsync(assetId, network, dimensionList);
                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error evaluating token health: AssetId={AssetId}, Network={Network}",
                    assetId,
                    LoggingHelper.SanitizeLogInput(network ?? string.Empty));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while evaluating token health.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        /// <summary>
        /// Returns lifecycle recommendations for a token
        /// </summary>
        /// <param name="assetId">Token asset ID</param>
        /// <param name="network">Network identifier</param>
        /// <returns>Ordered list of recommendations (highest priority first)</returns>
        [HttpGet("recommendations")]
        [ProducesResponseType(typeof(List<LifecycleRecommendation>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRecommendations(
            [FromQuery] ulong assetId,
            [FromQuery] string network)
        {
            try
            {
                if (assetId == 0)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "AssetId must be a positive integer.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                if (string.IsNullOrWhiteSpace(network))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Network is required.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var recommendations = await _intelligenceService.GetRecommendationsAsync(assetId, network);
                return Ok(recommendations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error generating recommendations: AssetId={AssetId}, Network={Network}",
                    assetId,
                    LoggingHelper.SanitizeLogInput(network ?? string.Empty));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while generating recommendations.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        /// <summary>
        /// Health check for the operations intelligence API
        /// </summary>
        [HttpGet("health-check")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiStatusResponse), StatusCodes.Status200OK)]
        public IActionResult HealthCheck()
        {
            return Ok(new ApiStatusResponse
            {
                Status = "Healthy",
                Version = "v1.0",
                Timestamp = DateTime.UtcNow,
                Environment = "Production"
            });
        }
    }
}
