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
    }
}
