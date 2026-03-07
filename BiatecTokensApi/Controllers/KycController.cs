using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Aml;
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
        /// Initiates KYC verification for the authenticated user and returns an SDK token for the frontend widget.
        /// Use this endpoint to start the verification flow — the returned SdkToken is passed to the
        /// frontend Sumsub/Onfido widget to launch document capture and liveness detection.
        /// Estimated processing time: typically 2-5 minutes.
        /// </summary>
        /// <param name="request">The initiation request</param>
        /// <returns>Initiation response including the SDK token for the frontend widget</returns>
        /// <response code="200">KYC initiation successful — SDK token returned</response>
        /// <response code="400">Invalid request or verification already pending</response>
        /// <response code="401">Unauthorized — authentication required</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("initiate")]
        [ProducesResponseType(typeof(InitiateKycResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> InitiateVerification([FromBody] InitiateKycRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("KYC initiate attempted without user ID");
                    return Unauthorized(new { ErrorCode = ErrorCodes.UNAUTHORIZED, ErrorMessage = "User ID not found in authentication token" });
                }

                var correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

                _logger.LogInformation(
                    "Initiating KYC verification for user {UserId}. CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(userId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                // Map InitiateKycRequest to the existing StartKycVerificationRequest
                var startRequest = new StartKycVerificationRequest
                {
                    FullName = request.FullName,
                    DateOfBirth = request.DateOfBirth,
                    Country = request.Country,
                    DocumentType = request.DocumentType,
                    Metadata = request.Metadata
                };

                var startResponse = await _kycService.StartVerificationAsync(userId, startRequest, correlationId);

                if (!startResponse.Success)
                {
                    return BadRequest(new InitiateKycResponse
                    {
                        Success = false,
                        ErrorCode = startResponse.ErrorCode,
                        ErrorMessage = startResponse.ErrorMessage,
                        CorrelationId = correlationId
                    });
                }

                // Build and return the initiation response with SDK token
                // In a real Sumsub integration the SDK token is fetched from the provider API.
                // For the mock provider, we return the providerReferenceId as the token so the
                // frontend can identify the session.
                return Ok(new InitiateKycResponse
                {
                    Success = true,
                    KycId = startResponse.KycId,
                    SdkToken = startResponse.ProviderReferenceId,
                    ProviderReferenceId = startResponse.ProviderReferenceId,
                    VerificationUrl = null, // Populated by real provider
                    EstimatedProcessingTime = "Typically 2-5 minutes",
                    CorrelationId = correlationId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating KYC verification");
                return StatusCode(StatusCodes.Status500InternalServerError, new InitiateKycResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An internal error occurred while initiating KYC verification"
                });
            }
        }

        /// <summary>
        /// Returns detailed KYC information for a specific user (admin endpoint).
        /// Includes provider reference ID, all status history, and metadata.
        /// </summary>
        /// <param name="userId">The user ID to look up</param>
        /// <returns>Admin KYC response with full record details</returns>
        /// <response code="200">KYC details retrieved successfully</response>
        /// <response code="401">Unauthorized — authentication required</response>
        /// <response code="404">No KYC record found for user</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("admin/{userId}")]
        [ProducesResponseType(typeof(KycAdminResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAdminDetails([FromRoute] string userId)
        {
            try
            {
                _logger.LogInformation("Admin KYC details request. UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(userId));

                var response = await _kycService.GetStatusAsync(userId);

                if (!response.Success)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new KycAdminResponse
                    {
                        Success = false,
                        ErrorCode = response.ErrorCode,
                        ErrorMessage = response.ErrorMessage
                    });
                }

                if (response.KycId == null)
                {
                    return NotFound(new KycAdminResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.KYC_NOT_STARTED,
                        ErrorMessage = $"No KYC record found for user {LoggingHelper.SanitizeLogInput(userId)}"
                    });
                }

                return Ok(new KycAdminResponse
                {
                    Success = true,
                    KycId = response.KycId,
                    UserId = userId,
                    Status = response.Status,
                    Provider = response.Provider,
                    Reason = response.Reason,
                    CreatedAt = response.CreatedAt,
                    UpdatedAt = response.UpdatedAt,
                    CompletedAt = response.CompletedAt,
                    ExpiresAt = response.ExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin KYC details. UserId={UserId}",
                    LoggingHelper.SanitizeLogInput(userId));
                return StatusCode(StatusCodes.Status500InternalServerError, new KycAdminResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An internal error occurred while retrieving KYC details"
                });
            }
        }

        /// <summary>
        /// Webhook endpoint for receiving KYC status updates from provider.
        /// Signature is validated via HMAC-SHA256 in the X-KYC-Signature header.
        /// </summary>
        /// <param name="payload">The KYC webhook payload from the provider, containing the provider reference ID, event type, and new status</param>
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
