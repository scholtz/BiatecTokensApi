using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Kyc;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for KYC (Know Your Customer) verification operations
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/kyc")]
    public class KycController : ControllerBase
    {
        private readonly IKycService _kycService;
        private readonly ILogger<KycController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="KycController"/> class
        /// </summary>
        /// <param name="kycService">The KYC service</param>
        /// <param name="logger">The logger instance</param>
        public KycController(IKycService kycService, ILogger<KycController> logger)
        {
            _kycService = kycService;
            _logger = logger;
        }

        /// <summary>
        /// Starts KYC verification for the authenticated user
        /// </summary>
        /// <param name="request">The verification request</param>
        /// <returns>The verification start response</returns>
        /// <response code="200">KYC verification started successfully</response>
        /// <response code="400">Invalid request or verification already pending</response>
        /// <response code="401">Unauthorized - authentication required</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("start")]
        [ProducesResponseType(typeof(StartKycVerificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StartVerification([FromBody] StartKycVerificationRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Get user ID from JWT claims
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("KYC start verification attempted without user ID");
                    return Unauthorized(new { ErrorCode = ErrorCodes.UNAUTHORIZED, ErrorMessage = "User ID not found in authentication token" });
                }

                var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

                _logger.LogInformation("Starting KYC verification for user {UserId}. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                var response = await _kycService.StartVerificationAsync(userId, request, correlationId);

                if (!response.Success)
                {
                    return BadRequest(response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting KYC verification");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An internal error occurred while starting KYC verification"
                });
            }
        }

        /// <summary>
        /// Gets the current KYC verification status for the authenticated user
        /// </summary>
        /// <returns>The KYC status response</returns>
        /// <response code="200">KYC status retrieved successfully</response>
        /// <response code="401">Unauthorized - authentication required</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("status")]
        [ProducesResponseType(typeof(KycStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                // Get user ID from JWT claims
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("KYC status check attempted without user ID");
                    return Unauthorized(new { ErrorCode = ErrorCodes.UNAUTHORIZED, ErrorMessage = "User ID not found in authentication token" });
                }

                _logger.LogInformation("Getting KYC status for user {UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                var response = await _kycService.GetStatusAsync(userId);

                if (!response.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting KYC status");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An internal error occurred while retrieving KYC status"
                });
            }
        }

        /// <summary>
        /// Webhook endpoint for receiving KYC status updates from provider
        /// </summary>
        /// <param name="payload">The webhook payload</param>
        /// <returns>Status of webhook processing</returns>
        /// <response code="200">Webhook processed successfully</response>
        /// <response code="400">Invalid webhook payload or signature</response>
        /// <response code="500">Internal server error</response>
        [AllowAnonymous]
        [HttpPost("webhook")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Webhook([FromBody] KycWebhookPayload payload)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Get signature from header
                var signature = Request.Headers["X-KYC-Signature"].FirstOrDefault();

                _logger.LogInformation("Received KYC webhook. ProviderRefId={ProviderRefId}, EventType={EventType}",
                    LoggingHelper.SanitizeLogInput(payload.ProviderReferenceId),
                    LoggingHelper.SanitizeLogInput(payload.EventType));

                var success = await _kycService.HandleWebhookAsync(payload, signature);

                if (!success)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.WEBHOOK_SIGNATURE_INVALID,
                        ErrorMessage = "Webhook processing failed - invalid signature or record not found"
                    });
                }

                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing KYC webhook");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An internal error occurred while processing webhook"
                });
            }
        }
    }
}
