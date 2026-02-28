using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.AssetIntelligence;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Asset Intelligence API providing canonical metadata, validation status, and confidence indicators.
    ///
    /// All endpoints are:
    /// - **Idempotent** – repeated calls return semantically identical responses.
    /// - **Privacy-safe** – no internal stack traces or secrets are exposed.
    /// - **Correlation-linked** – every response carries a correlation ID for audit tracing.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/asset-intelligence")]
    [Produces("application/json")]
    public class AssetIntelligenceController : ControllerBase
    {
        private readonly IAssetIntelligenceService _service;
        private readonly ILogger<AssetIntelligenceController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="AssetIntelligenceController"/>.
        /// </summary>
        public AssetIntelligenceController(
            IAssetIntelligenceService service,
            ILogger<AssetIntelligenceController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /evaluate
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns canonical asset metadata with validation status and confidence indicators.
        /// </summary>
        /// <param name="request">Asset intelligence request including asset ID and network.</param>
        /// <returns>Normalized metadata, validation details, and provenance information.</returns>
        [HttpPost("evaluate")]
        [ProducesResponseType(typeof(AssetIntelligenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Evaluate([FromBody] AssetIntelligenceRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                        ErrorMessage = "Request body is required.",
                        RemediationHint = "Provide a valid AssetIntelligenceRequest body.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var response = await _service.GetAssetIntelligenceAsync(request);

                if (!response.Success)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = response.ErrorCode.ToString(),
                        ErrorMessage = response.ErrorMessage,
                        RemediationHint = response.RemediationHint,
                        CorrelationId = response.CorrelationId,
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating asset intelligence for AssetId={AssetId}, Network={Network}",
                    request?.AssetId,
                    LoggingHelper.SanitizeLogInput(request?.Network));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred.",
                    RemediationHint = "Retry the request. Contact support if the error persists.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /quality/{assetId}/{network}
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns quality indicators for the specified asset.
        /// </summary>
        /// <param name="assetId">The on-chain asset identifier.</param>
        /// <param name="network">The blockchain network identifier.</param>
        /// <returns>Asset quality indicators including confidence level and overall score.</returns>
        [HttpGet("quality/{assetId}/{network}")]
        [ProducesResponseType(typeof(AssetQualityIndicators), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetQuality(ulong assetId, string network)
        {
            try
            {
                if (assetId == 0 || string.IsNullOrWhiteSpace(network))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                        ErrorMessage = "AssetId and Network are required.",
                        RemediationHint = "Provide a valid asset ID (>0) and network identifier.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var result = await _service.GetQualityIndicatorsAsync(assetId, network);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving quality indicators for AssetId={AssetId}, Network={Network}",
                    assetId, LoggingHelper.SanitizeLogInput(network));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred.",
                    RemediationHint = "Retry the request. Contact support if the error persists.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /health
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Health check for the asset intelligence API.
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiStatusResponse), StatusCodes.Status200OK)]
        public IActionResult Health() =>
            Ok(new ApiStatusResponse
            {
                Status = "Healthy",
                Version = "v1.0",
                Timestamp = DateTime.UtcNow,
                Environment = "Production"
            });
    }
}
