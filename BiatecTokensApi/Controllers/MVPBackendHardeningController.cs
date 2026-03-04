using BiatecTokensApi.Models.MVPHardening;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Exposes MVP backend hardening operations:
    /// auth contract verification, deployment reliability, compliance checks, and observability tracing.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/mvp-hardening")]
    [Produces("application/json")]
    public class MVPBackendHardeningController : ControllerBase
    {
        private readonly IMVPBackendHardeningService _service;
        private readonly ILogger<MVPBackendHardeningController> _logger;

        /// <summary>Initialises a new instance of <see cref="MVPBackendHardeningController"/>.</summary>
        public MVPBackendHardeningController(
            IMVPBackendHardeningService service,
            ILogger<MVPBackendHardeningController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>Verifies auth contract determinism for the provided email.</summary>
        [HttpPost("auth-contract/verify")]
        [ProducesResponseType(typeof(AuthContractVerifyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> VerifyAuthContract([FromBody] AuthContractVerifyRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.VerifyAuthContractAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Initiates a reliable deployment with idempotency support.</summary>
        [HttpPost("deployment/initiate")]
        [ProducesResponseType(typeof(DeploymentReliabilityResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> InitiateDeployment([FromBody] DeploymentReliabilityRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.InitiateDeploymentAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Returns current status of a deployment.</summary>
        [HttpGet("deployment/{id}")]
        [ProducesResponseType(typeof(DeploymentReliabilityResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDeploymentStatus(string id, [FromQuery] string? correlationId)
        {
            var result = await _service.GetDeploymentStatusAsync(id, correlationId);
            if (result == null)
                return NotFound(new { error = $"Deployment '{id}' not found." });
            return Ok(result);
        }

        /// <summary>Transitions a deployment to the specified status.</summary>
        [HttpPost("deployment/transition")]
        [ProducesResponseType(typeof(DeploymentReliabilityResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> TransitionDeploymentStatus([FromBody] DeploymentStatusTransitionRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.TransitionDeploymentStatusAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Runs a compliance check for the given asset.</summary>
        [HttpPost("compliance/check")]
        [ProducesResponseType(typeof(ComplianceCheckResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RunComplianceCheck([FromBody] ComplianceCheckRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.RunComplianceCheckAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Creates an observability trace event.</summary>
        [HttpPost("observability/trace")]
        [ProducesResponseType(typeof(ObservabilityTraceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateTrace([FromBody] ObservabilityTraceRequest? request)
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required." });

            var result = await _service.CreateTraceAsync(request);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
