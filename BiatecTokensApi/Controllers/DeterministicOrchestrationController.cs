using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.DeterministicOrchestration;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Deterministic deployment orchestration API: lifecycle management, idempotency,
    /// compliance pipeline, audit trail, and structured error payloads (Issue #480).
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/deterministic-orchestration")]
    [Produces("application/json")]
    public class DeterministicOrchestrationController : ControllerBase
    {
        private readonly IDeterministicOrchestrationService _service;
        private readonly ILogger<DeterministicOrchestrationController> _logger;

        /// <summary>Initialises a new instance of <see cref="DeterministicOrchestrationController"/>.</summary>
        public DeterministicOrchestrationController(
            IDeterministicOrchestrationService service,
            ILogger<DeterministicOrchestrationController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>Starts a new deterministic deployment orchestration (idempotent).</summary>
        [HttpPost("orchestrate")]
        [ProducesResponseType(typeof(OrchestrationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Orchestrate([FromBody] OrchestrationRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.OrchestrateAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Returns the current status of a deployment orchestration.</summary>
        [HttpGet("status/{orchestrationId}")]
        [ProducesResponseType(typeof(OrchestrationStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStatus(
            [FromRoute] string orchestrationId,
            [FromQuery] string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(orchestrationId))
                return BadRequest(new { error = "Orchestration ID is required." });

            _logger.LogInformation("Fetching orchestration status for {OrchestrationId}",
                LoggingHelper.SanitizeLogInput(orchestrationId));

            var result = await _service.GetStatusAsync(orchestrationId, correlationId);
            if (result == null)
                return NotFound(new { error = $"Orchestration '{orchestrationId}' was not found." });

            return Ok(result);
        }

        /// <summary>Advances the orchestration to its next lifecycle stage.</summary>
        [HttpPost("advance")]
        [ProducesResponseType(typeof(OrchestrationAdvanceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Advance([FromBody] OrchestrationAdvanceRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.AdvanceAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Runs the compliance-check pipeline for the specified orchestration.</summary>
        [HttpPost("compliance-check")]
        [ProducesResponseType(typeof(ComplianceCheckResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ComplianceCheck([FromBody] ComplianceCheckRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.RunComplianceCheckAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Returns the audit trail for an orchestration.</summary>
        [HttpGet("audit/{orchestrationId}")]
        [ProducesResponseType(typeof(OrchestrationAuditResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetAudit(
            [FromRoute] string orchestrationId,
            [FromQuery] string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(orchestrationId))
                return BadRequest(new { error = "Orchestration ID is required." });

            _logger.LogInformation("Fetching audit trail for {OrchestrationId}",
                LoggingHelper.SanitizeLogInput(orchestrationId));

            var events = _service.GetAuditEvents(orchestrationId, correlationId);
            return Ok(new OrchestrationAuditResponse
            {
                Success = true,
                OrchestrationId = orchestrationId,
                Events = events.ToList(),
                CorrelationId = correlationId
            });
        }

        /// <summary>Cancels an in-progress orchestration.</summary>
        [HttpPost("cancel")]
        [ProducesResponseType(typeof(OrchestrationCancelResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Cancel([FromBody] OrchestrationCancelRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.CancelAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
