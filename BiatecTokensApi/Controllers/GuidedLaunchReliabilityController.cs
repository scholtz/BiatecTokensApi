using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.GuidedLaunchReliability;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Exposes guided token launch reliability operations: initiation, status tracking,
    /// step-by-step advancement, step validation, and cancellation.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/guided-launch")]
    [Produces("application/json")]
    public class GuidedLaunchReliabilityController : ControllerBase
    {
        private readonly IGuidedLaunchReliabilityService _service;
        private readonly ILogger<GuidedLaunchReliabilityController> _logger;

        /// <summary>Initialises a new instance of <see cref="GuidedLaunchReliabilityController"/>.</summary>
        public GuidedLaunchReliabilityController(
            IGuidedLaunchReliabilityService service,
            ILogger<GuidedLaunchReliabilityController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>Initiates a new guided token launch with idempotency support.</summary>
        [HttpPost("initiate")]
        [ProducesResponseType(typeof(GuidedLaunchInitiateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Initiate([FromBody] GuidedLaunchInitiateRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.InitiateLaunchAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Returns the current status of a guided launch.</summary>
        [HttpGet("status/{launchId}")]
        [ProducesResponseType(typeof(GuidedLaunchStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStatus(
            [FromRoute] string launchId,
            [FromQuery] string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(launchId))
                return BadRequest(new { error = "Launch ID is required." });

            _logger.LogInformation("Fetching guided launch status for {LaunchId}", LoggingHelper.SanitizeLogInput(launchId));

            var result = await _service.GetLaunchStatusAsync(launchId, correlationId);
            if (result == null)
                return NotFound(new { error = "Launch not found.", launchId });

            return Ok(result);
        }

        /// <summary>Advances the guided launch to the next stage.</summary>
        [HttpPost("advance")]
        [ProducesResponseType(typeof(GuidedLaunchAdvanceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Advance([FromBody] GuidedLaunchAdvanceRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.AdvanceLaunchAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Validates inputs for a specific step without advancing stage.</summary>
        [HttpPost("validate-step")]
        [ProducesResponseType(typeof(GuidedLaunchValidateStepResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ValidateStep([FromBody] GuidedLaunchValidateStepRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.ValidateStepAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Cancels an in-progress guided launch.</summary>
        [HttpPost("cancel")]
        [ProducesResponseType(typeof(GuidedLaunchCancelResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Cancel([FromBody] GuidedLaunchCancelRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.CancelLaunchAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
