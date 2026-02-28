using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.PricingReliability;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Pricing Reliability API providing deterministic quotes with source provenance and fallback tracing.
    ///
    /// All endpoints are:
    /// - **Idempotent** – repeated calls return semantically identical responses.
    /// - **Privacy-safe** – no internal stack traces or secrets are exposed.
    /// - **Correlation-linked** – every response carries a correlation ID for audit tracing.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/pricing-reliability")]
    [Produces("application/json")]
    public class PricingReliabilityController : ControllerBase
    {
        private readonly IPricingReliabilityService _service;
        private readonly ILogger<PricingReliabilityController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="PricingReliabilityController"/>.
        /// </summary>
        public PricingReliabilityController(
            IPricingReliabilityService service,
            ILogger<PricingReliabilityController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /quote
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a deterministic price quote with source provenance and precedence trace.
        /// </summary>
        /// <param name="request">Pricing reliability request including asset ID, network, and base currency.</param>
        /// <returns>Reliable price quote with full fallback chain information when requested.</returns>
        [HttpPost("quote")]
        [ProducesResponseType(typeof(PricingReliabilityResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetQuote([FromBody] PricingReliabilityRequest request)
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
                        RemediationHint = "Provide a valid PricingReliabilityRequest body.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var response = await _service.GetReliableQuoteAsync(request);

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
                _logger.LogError(ex, "Error retrieving reliable quote for AssetId={AssetId}, Network={Network}",
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
        // GET /source-health
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns health status of all configured pricing sources.
        /// </summary>
        /// <returns>Source health summary including available and unavailable source names.</returns>
        [HttpGet("source-health")]
        [ProducesResponseType(typeof(PricingSourceHealthSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSourceHealth()
        {
            try
            {
                var summary = await _service.GetSourceHealthAsync();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pricing source health");
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
        /// Health check for the pricing reliability API.
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
