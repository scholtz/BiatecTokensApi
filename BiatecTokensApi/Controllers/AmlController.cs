using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Aml;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for AML (Anti-Money Laundering) sanctions screening and PEP checks.
    /// Admin endpoints require authentication. Webhook endpoint is anonymous (HMAC-secured).
    /// </summary>
    [ApiController]
    [Route("api/v1/aml")]
    public class AmlController : ControllerBase
    {
        private readonly IAmlService _amlService;
        private readonly ILogger<AmlController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AmlController"/> class.
        /// </summary>
        /// <param name="amlService">The AML service</param>
        /// <param name="logger">The logger</param>
        public AmlController(IAmlService amlService, ILogger<AmlController> logger)
        {
            _amlService = amlService;
            _logger = logger;
        }

        /// <summary>
        /// Triggers a manual AML screening for a user (admin only).
        /// Screens against UN, EU, OFAC sanctions lists and PEP databases.
        /// </summary>
        /// <param name="request">The screening request</param>
        /// <returns>The screening response including risk level and status</returns>
        /// <response code="200">Screening completed successfully</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="401">Unauthorized — authentication required</response>
        /// <response code="500">Internal server error</response>
        [Authorize]
        [HttpPost("screen")]
        [ProducesResponseType(typeof(AmlScreenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Screen([FromBody] AmlScreenRequest request)
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
                    "AML screen request. UserId={UserId}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.UserId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                var response = await _amlService.ScreenUserAsync(request.UserId, request.Metadata, correlationId);

                if (!response.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AML screen request");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An internal error occurred during AML screening"
                });
            }
        }

        /// <summary>
        /// Returns the current AML screening status and risk score for a user.
        /// </summary>
        /// <param name="userId">The user ID to query</param>
        /// <returns>The AML status response including risk level and next screening date</returns>
        /// <response code="200">Status retrieved successfully</response>
        /// <response code="401">Unauthorized — authentication required</response>
        /// <response code="500">Internal server error</response>
        [Authorize]
        [HttpGet("status/{userId}")]
        [ProducesResponseType(typeof(AmlStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatus([FromRoute] string userId)
        {
            try
            {
                _logger.LogInformation("AML status query. UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                var response = await _amlService.GetStatusAsync(userId);

                if (!response.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting AML status. UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(userId));
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An internal error occurred while retrieving AML status"
                });
            }
        }

        /// <summary>
        /// Receives continuous-monitoring alert webhooks from the AML provider.
        /// Signature is validated via HMAC-SHA256 in the X-AML-Signature header.
        /// </summary>
        /// <param name="payload">The webhook payload from the AML provider</param>
        /// <returns>200 OK on success, 400 on invalid payload or signature</returns>
        /// <response code="200">Webhook processed successfully</response>
        /// <response code="400">Invalid payload or signature</response>
        /// <response code="500">Internal server error</response>
        [AllowAnonymous]
        [HttpPost("webhook")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Webhook([FromBody] AmlWebhookPayload payload)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var signature = Request.Headers["X-AML-Signature"].FirstOrDefault();

                _logger.LogInformation(
                    "Received AML webhook. ProviderRefId={ProviderRefId}, AlertType={AlertType}",
                    LoggingHelper.SanitizeLogInput(payload.ProviderReferenceId),
                    LoggingHelper.SanitizeLogInput(payload.AlertType));

                var success = await _amlService.HandleWebhookAsync(payload, signature);

                if (!success)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.WEBHOOK_SIGNATURE_INVALID,
                        ErrorMessage = "Webhook processing failed — invalid payload or record not found"
                    });
                }

                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AML webhook");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An internal error occurred while processing AML webhook"
                });
            }
        }

        /// <summary>
        /// Generates an AML compliance report for a user, including full screening history.
        /// Suitable for regulatory audit and MICA compliance documentation.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>The AML compliance report</returns>
        /// <response code="200">Report generated successfully</response>
        /// <response code="401">Unauthorized — authentication required</response>
        /// <response code="500">Internal server error</response>
        [Authorize]
        [HttpGet("report/{userId}")]
        [ProducesResponseType(typeof(AmlReportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetReport([FromRoute] string userId)
        {
            try
            {
                _logger.LogInformation("AML report request. UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                var response = await _amlService.GenerateReportAsync(userId);

                if (!response.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AML report. UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(userId));
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An internal error occurred while generating the AML report"
                });
            }
        }
    }
}
