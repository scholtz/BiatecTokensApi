using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.KycWorkflow;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for the KYC verification workflow foundation layer.
    /// Provides production-grade compliance workflow endpoints including state machine
    /// management, audit history, evidence management, and eligibility evaluation.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/kyc-workflow")]
    public class KycWorkflowController : ControllerBase
    {
        private readonly IKycWorkflowService _kycWorkflowService;
        private readonly ILogger<KycWorkflowController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="KycWorkflowController"/>.
        /// </summary>
        public KycWorkflowController(IKycWorkflowService kycWorkflowService, ILogger<KycWorkflowController> logger)
        {
            _kycWorkflowService = kycWorkflowService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new KYC verification workflow record in Pending state.
        /// </summary>
        /// <param name="request">Verification creation request</param>
        /// <returns>The created KYC workflow record</returns>
        [HttpPost]
        [ProducesResponseType(typeof(KycVerificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(KycVerificationResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateVerification([FromBody] CreateKycVerificationRequest request)
        {
            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            var correlationId = HttpContext.TraceIdentifier;

            _logger.LogInformation(
                "CreateVerification called. ParticipantId={ParticipantId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request?.ParticipantId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            if (request == null || string.IsNullOrWhiteSpace(request.ParticipantId))
            {
                return BadRequest(new KycVerificationResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "ParticipantId is required."
                });
            }

            var result = await _kycWorkflowService.CreateVerificationAsync(request, actorId, correlationId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Gets a KYC verification record by its ID.
        /// </summary>
        /// <param name="kycId">KYC record identifier</param>
        [HttpGet("{kycId}")]
        [ProducesResponseType(typeof(KycVerificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(KycVerificationResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVerification(string kycId)
        {
            var result = await _kycWorkflowService.GetVerificationAsync(kycId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Updates the status of a KYC verification record, enforcing state machine rules.
        /// </summary>
        /// <param name="kycId">KYC record identifier</param>
        /// <param name="request">Status update request</param>
        [HttpPut("{kycId}/status")]
        [ProducesResponseType(typeof(KycVerificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(KycVerificationResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(KycVerificationResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus(string kycId, [FromBody] UpdateKycVerificationStatusRequest request)
        {
            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            var correlationId = HttpContext.TraceIdentifier;

            _logger.LogInformation(
                "UpdateStatus called. KycId={KycId} NewState={NewState} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(kycId),
                request?.NewState,
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            if (request == null)
                return BadRequest(new KycVerificationResponse { Success = false, ErrorCode = ErrorCodes.INVALID_REQUEST, ErrorMessage = "Request body is required." });

            var result = await _kycWorkflowService.UpdateStatusAsync(kycId, request, actorId, correlationId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Returns the chronological audit history for a KYC record.
        /// </summary>
        /// <param name="kycId">KYC record identifier</param>
        [HttpGet("{kycId}/history")]
        [ProducesResponseType(typeof(KycHistoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(KycHistoryResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetHistory(string kycId)
        {
            var result = await _kycWorkflowService.GetHistoryAsync(kycId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Adds an evidence item to a KYC verification record.
        /// </summary>
        /// <param name="kycId">KYC record identifier</param>
        /// <param name="request">Evidence addition request</param>
        [HttpPost("{kycId}/evidence")]
        [ProducesResponseType(typeof(KycEvidenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(KycEvidenceResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddEvidence(string kycId, [FromBody] AddKycEvidenceRequest request)
        {
            var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

            if (request == null)
                return BadRequest(new KycEvidenceResponse { Success = false, KycId = kycId, ErrorCode = ErrorCodes.INVALID_REQUEST, ErrorMessage = "Request body is required." });

            var result = await _kycWorkflowService.AddEvidenceAsync(kycId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Returns all evidence items for a KYC verification record.
        /// </summary>
        /// <param name="kycId">KYC record identifier</param>
        [HttpGet("{kycId}/evidence")]
        [ProducesResponseType(typeof(KycEvidenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(KycEvidenceResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEvidence(string kycId)
        {
            var result = await _kycWorkflowService.GetEvidenceAsync(kycId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Gets the most recent active KYC verification for a participant.
        /// </summary>
        /// <param name="participantId">Participant identifier</param>
        [HttpGet("participant/{participantId}")]
        [ProducesResponseType(typeof(KycVerificationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(KycVerificationResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetActiveVerificationByParticipant(string participantId)
        {
            var result = await _kycWorkflowService.GetActiveVerificationByParticipantAsync(participantId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Evaluates whether a participant has an active, non-expired, approved KYC verification.
        /// </summary>
        /// <param name="request">Eligibility evaluation request</param>
        [HttpPost("eligibility")]
        [ProducesResponseType(typeof(KycEligibilityResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(KycEligibilityResult), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> EvaluateEligibility([FromBody] KycEligibilityRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ParticipantId))
            {
                return BadRequest(new KycEligibilityResult
                {
                    IsEligible = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "ParticipantId is required."
                });
            }

            var result = await _kycWorkflowService.EvaluateEligibilityAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Admin operation: batch-processes all Approved records that have passed their expiry date.
        /// </summary>
        [HttpPost("process-expired")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> ProcessExpired()
        {
            var count = await _kycWorkflowService.ProcessExpiredVerificationsAsync();

            _logger.LogInformation("ProcessExpired completed. ExpiredCount={Count}", count);

            return Ok(new { Success = true, ExpiredCount = count });
        }
    }
}
