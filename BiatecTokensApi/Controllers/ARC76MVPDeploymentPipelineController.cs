using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ARC76MVPPipeline;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// ARC76 MVP deployment pipeline API: lifecycle management, ARC76 readiness enforcement,
    /// idempotency, compliance traceability, and structured error payloads.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/arc76-mvp-pipeline")]
    [Produces("application/json")]
    public class ARC76MVPDeploymentPipelineController : ControllerBase
    {
        private readonly IARC76MVPDeploymentPipelineService _service;
        private readonly ILogger<ARC76MVPDeploymentPipelineController> _logger;

        /// <summary>Initialises a new instance of <see cref="ARC76MVPDeploymentPipelineController"/>.</summary>
        public ARC76MVPDeploymentPipelineController(
            IARC76MVPDeploymentPipelineService service,
            ILogger<ARC76MVPDeploymentPipelineController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>Initiates a new ARC76 MVP deployment pipeline (idempotent).</summary>
        [HttpPost("initiate")]
        [ProducesResponseType(typeof(PipelineInitiateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Initiate([FromBody] PipelineInitiateRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.InitiateAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Returns the current status of a deployment pipeline.</summary>
        [HttpGet("status/{pipelineId}")]
        [ProducesResponseType(typeof(PipelineStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStatus(
            [FromRoute] string pipelineId,
            [FromQuery] string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(pipelineId))
                return BadRequest(new { error = "Pipeline ID is required." });

            _logger.LogInformation("Fetching pipeline status for {PipelineId}",
                LoggingHelper.SanitizeLogInput(pipelineId));

            var result = await _service.GetStatusAsync(pipelineId, correlationId);
            if (result == null)
                return NotFound(new { error = $"Pipeline '{pipelineId}' was not found." });

            return Ok(result);
        }

        /// <summary>Advances the pipeline to its next lifecycle stage.</summary>
        [HttpPost("advance")]
        [ProducesResponseType(typeof(PipelineAdvanceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Advance([FromBody] PipelineAdvanceRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.AdvanceAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Cancels an in-progress pipeline.</summary>
        [HttpPost("cancel")]
        [ProducesResponseType(typeof(PipelineCancelResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Cancel([FromBody] PipelineCancelRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.CancelAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Retries a failed pipeline.</summary>
        [HttpPost("retry")]
        [ProducesResponseType(typeof(PipelineRetryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Retry([FromBody] PipelineRetryRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.RetryAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Returns the audit trail for a pipeline.</summary>
        [HttpGet("audit/{pipelineId}")]
        [ProducesResponseType(typeof(PipelineAuditResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAudit(
            [FromRoute] string pipelineId,
            [FromQuery] string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(pipelineId))
                return BadRequest(new { error = "Pipeline ID is required." });

            _logger.LogInformation("Fetching audit trail for {PipelineId}",
                LoggingHelper.SanitizeLogInput(pipelineId));

            var result = await _service.GetAuditAsync(pipelineId, correlationId);
            return Ok(result);
        }
    }
}
