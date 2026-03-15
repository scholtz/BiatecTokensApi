using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceOrchestration;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for enterprise compliance orchestration (KYC + AML).
    /// All endpoints require authentication.
    /// </summary>
    [ApiController]
    [Route("api/v1/compliance-orchestration")]
    [Authorize]
    public class ComplianceOrchestrationController : ControllerBase
    {
        private readonly IComplianceOrchestrationService _orchestrationService;
        private readonly ILogger<ComplianceOrchestrationController> _logger;

        public ComplianceOrchestrationController(
            IComplianceOrchestrationService orchestrationService,
            ILogger<ComplianceOrchestrationController> logger)
        {
            _orchestrationService = orchestrationService;
            _logger = logger;
        }

        /// <summary>
        /// Initiates a new compliance check (or returns the cached result for the same idempotency key).
        /// </summary>
        /// <param name="request">Compliance check request.</param>
        /// <returns>The compliance check response.</returns>
        [HttpPost("initiate")]
        [ProducesResponseType(typeof(ComplianceCheckResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> InitiateCheck([FromBody] InitiateComplianceCheckRequest request)
        {
            if (request == null)
                return BadRequest("Request body is required.");

            var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? "unknown";

            var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                             ?? Guid.NewGuid().ToString("N");

            _logger.LogInformation(
                "Compliance check initiation requested. SubjectId={SubjectId}, CheckType={CheckType}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                request.CheckType,
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _orchestrationService.InitiateCheckAsync(request, actorId, correlationId);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Gets the current status of a compliance check by its decision ID.
        /// </summary>
        /// <param name="decisionId">The decision ID.</param>
        /// <returns>Current status of the compliance decision.</returns>
        [HttpGet("status/{decisionId}")]
        [ProducesResponseType(typeof(ComplianceCheckResponse), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetStatus([FromRoute] string decisionId)
        {
            _logger.LogInformation(
                "Compliance check status requested. DecisionId={DecisionId}",
                LoggingHelper.SanitizeLogInput(decisionId));

            var response = await _orchestrationService.GetCheckStatusAsync(decisionId);

            if (!response.Success)
                return NotFound(response);

            return Ok(response);
        }

        /// <summary>
        /// Gets the full compliance decision history for a given subject.
        /// </summary>
        /// <param name="subjectId">The subject identifier.</param>
        /// <returns>All compliance decisions recorded for the subject.</returns>
        [HttpGet("history/{subjectId}")]
        [ProducesResponseType(typeof(ComplianceDecisionHistoryResponse), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetHistory([FromRoute] string subjectId)
        {
            _logger.LogInformation(
                "Compliance decision history requested. SubjectId={SubjectId}",
                LoggingHelper.SanitizeLogInput(subjectId));

            var response = await _orchestrationService.GetDecisionHistoryAsync(subjectId);
            return Ok(response);
        }

        /// <summary>
        /// Appends a reviewer note or evidence reference to an existing compliance decision.
        /// Notes allow operators to attach human-readable context, document references,
        /// or evidence metadata to a decision for audit and review purposes.
        /// </summary>
        /// <param name="decisionId">The compliance decision to annotate.</param>
        /// <param name="request">The note content and optional evidence references.</param>
        /// <returns>The created reviewer note on success.</returns>
        [HttpPost("notes/{decisionId}")]
        [ProducesResponseType(typeof(AppendReviewerNoteResponse), 200)]
        [ProducesResponseType(typeof(AppendReviewerNoteResponse), 400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> AppendNote(
            [FromRoute] string decisionId,
            [FromBody] AppendReviewerNoteRequest request)
        {
            if (request == null)
                return BadRequest(new AppendReviewerNoteResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_REQUEST_BODY",
                    ErrorMessage = "Request body is required."
                });

            var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(actorId))
                return Unauthorized();

            var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                             ?? Guid.NewGuid().ToString("N");

            _logger.LogInformation(
                "Reviewer note append requested. DecisionId={DecisionId}, Actor={Actor}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(decisionId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _orchestrationService.AppendReviewerNoteAsync(
                decisionId, request, actorId, correlationId);

            if (!response.Success)
            {
                if (response.ErrorCode == "COMPLIANCE_CHECK_NOT_FOUND")
                    return NotFound(response);
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Initiates a rescreen for a subject whose evidence is stale or expired.
        /// A new compliance decision is created using the same subject and context as the
        /// original decision, optionally with updated parameters.
        /// </summary>
        /// <param name="decisionId">The original decision ID to rescreen.</param>
        /// <param name="request">Optional override parameters for the rescreen.</param>
        /// <returns>The new compliance check response on success.</returns>
        [HttpPost("rescreen/{decisionId}")]
        [ProducesResponseType(typeof(RescreenResponse), 200)]
        [ProducesResponseType(typeof(RescreenResponse), 400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Rescreen(
            [FromRoute] string decisionId,
            [FromBody] RescreenRequest? request)
        {
            var actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value
                       ?? "unknown";

            var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                             ?? Guid.NewGuid().ToString("N");

            _logger.LogInformation(
                "Rescreen requested. DecisionId={DecisionId}, Actor={Actor}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(decisionId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _orchestrationService.RescreenAsync(
                decisionId, request ?? new RescreenRequest(), actorId, correlationId);

            if (!response.Success)
            {
                if (response.ErrorCode == "COMPLIANCE_CHECK_NOT_FOUND")
                    return NotFound(response);
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Processes an inbound provider webhook/callback event and updates the corresponding
        /// compliance decision. This endpoint is anonymous but payload-authenticated via the
        /// <c>Signature</c> field in the request body.
        /// </summary>
        /// <remarks>
        /// Providers should POST to this endpoint when screening outcomes change asynchronously.
        /// The <c>ProviderReferenceId</c> links the callback to an existing compliance decision.
        /// Duplicate callbacks with the same <c>IdempotencyKey</c> are accepted without
        /// re-processing.
        /// </remarks>
        /// <param name="request">The normalised provider callback payload.</param>
        /// <returns>200 OK when processed (or idempotent replay); 400 for invalid payloads.</returns>
        [AllowAnonymous]
        [HttpPost("provider-callback")]
        [ProducesResponseType(typeof(ProviderCallbackResponse), 200)]
        [ProducesResponseType(typeof(ProviderCallbackResponse), 400)]
        public async Task<IActionResult> ProviderCallback([FromBody] ProviderCallbackRequest request)
        {
            if (request == null)
                return BadRequest(new ProviderCallbackResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_REQUEST_BODY",
                    ErrorMessage = "Request body is required."
                });

            var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                             ?? Guid.NewGuid().ToString("N");

            _logger.LogInformation(
                "Provider callback received. Provider={Provider}, ProviderRefId={RefId}, EventType={EventType}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.ProviderName),
                LoggingHelper.SanitizeLogInput(request.ProviderReferenceId),
                LoggingHelper.SanitizeLogInput(request.EventType),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _orchestrationService.ProcessProviderCallbackAsync(request, correlationId);

            if (!response.Success)
            {
                if (response.ErrorCode == "COMPLIANCE_CHECK_NOT_FOUND")
                    return NotFound(response);
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
}
