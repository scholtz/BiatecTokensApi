using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Aml;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for GDPR compliance operations.
    /// Implements Article 17 right-to-erasure for KYC and AML personal data.
    /// Audit references are retained per AMLD5 5-year retention requirements.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/gdpr")]
    public class GdprController : ControllerBase
    {
        private readonly IGdprErasureService _gdprErasureService;
        private readonly ILogger<GdprController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GdprController"/> class.
        /// </summary>
        /// <param name="gdprErasureService">The GDPR erasure service</param>
        /// <param name="logger">The logger</param>
        public GdprController(
            IGdprErasureService gdprErasureService,
            ILogger<GdprController> logger)
        {
            _gdprErasureService = gdprErasureService;
            _logger = logger;
        }

        /// <summary>
        /// Anonymizes all personal data (PII) for a user in compliance with GDPR Article 17.
        /// KYC and AML record identifiers, timestamps, and compliance audit references are
        /// preserved for the 5-year AMLD5 retention period. Only PII fields are erased.
        /// </summary>
        /// <param name="request">Erasure request with user ID and reason</param>
        /// <returns>Erasure response with anonymization reference and counts</returns>
        /// <response code="200">Erasure completed — anonymization reference returned</response>
        /// <response code="400">Invalid request</response>
        /// <response code="401">Unauthorized — authentication required</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("erase")]
        [ProducesResponseType(typeof(GdprErasureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EraseUserData([FromBody] GdprErasureRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(request.UserId))
                {
                    return BadRequest(new { ErrorCode = ErrorCodes.INVALID_INPUT, ErrorMessage = "UserId is required" });
                }

                var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

                _logger.LogInformation(
                    "GDPR erasure request. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.UserId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                var response = await _gdprErasureService.EraseUserDataAsync(request, correlationId);

                if (!response.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GDPR erasure request");
                return StatusCode(StatusCodes.Status500InternalServerError, new GdprErasureResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An internal error occurred during GDPR erasure"
                });
            }
        }
    }
}
