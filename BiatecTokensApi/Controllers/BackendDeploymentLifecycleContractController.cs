using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Exposes the deterministic backend deployment lifecycle contract with ARC76 hardening.
    ///
    /// Provides:
    /// - POST /initiate  – idempotent deployment initiation with ARC76 derivation or explicit address
    /// - GET  /status/{id}  – deployment lifecycle status
    /// - POST /validate  – dry-run validation without side effects
    /// - GET  /audit/{id}   – compliance audit trail
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/backend-deployment-contract")]
    [Produces("application/json")]
    public class BackendDeploymentLifecycleContractController : ControllerBase
    {
        private readonly IBackendDeploymentLifecycleContractService _service;
        private readonly ILogger<BackendDeploymentLifecycleContractController> _logger;

        /// <summary>
        /// Initialises a new instance of <see cref="BackendDeploymentLifecycleContractController"/>.
        /// </summary>
        public BackendDeploymentLifecycleContractController(
            IBackendDeploymentLifecycleContractService service,
            ILogger<BackendDeploymentLifecycleContractController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Initiates or replays a deterministic backend deployment contract.
        /// </summary>
        /// <param name="request">
        /// Deployment parameters. Supply either <c>DeployerEmail</c>+<c>DeployerPassword</c> for ARC76
        /// account derivation or <c>ExplicitDeployerAddress</c> for a pre-derived address.
        /// </param>
        /// <returns>
        /// Deployment contract response with lifecycle state, ARC76 derivation evidence,
        /// compliance audit events, and structured error taxonomy.
        /// </returns>
        /// <remarks>
        /// **Idempotency**: Replaying with the same idempotency key returns the cached result
        /// with <c>isIdempotentReplay=true</c> and no duplicate deployment.
        ///
        /// **ARC76 determinism**: The same email+password always produces the same Algorand address.
        /// The response includes <c>isDeterministicAddress=true</c> when ARC76 was used.
        /// </remarks>
        [HttpPost("initiate")]
        [ProducesResponseType(typeof(BackendDeploymentContractResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Initiate([FromBody] BackendDeploymentContractRequest? request)
        {
            if (request == null)
                return BadRequest("Request body is required.");

            var result = await _service.InitiateAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Returns the current lifecycle state of a deployment contract.
        /// </summary>
        /// <param name="id">Deployment contract ID returned by <c>/initiate</c>.</param>
        /// <returns>Current deployment contract response.</returns>
        [HttpGet("status/{id}")]
        [ProducesResponseType(typeof(BackendDeploymentContractResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStatus([FromRoute] string id)
        {
            var result = await _service.GetStatusAsync(id);
            if (result.ErrorCode == DeploymentErrorCode.RequiredFieldMissing &&
                result.Message.Contains("not found"))
                return NotFound(result);
            return Ok(result);
        }

        /// <summary>
        /// Validates deployment inputs without creating any deployment.
        /// </summary>
        /// <param name="request">Validation request with credentials and deployment parameters.</param>
        /// <returns>Validation response with field-level results and ARC76 derivation status.</returns>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(BackendDeploymentContractValidationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Validate(
            [FromBody] BackendDeploymentContractValidationRequest? request)
        {
            if (request == null)
                return BadRequest("Request body is required.");

            var result = await _service.ValidateAsync(request);
            return Ok(result);
        }

        /// <summary>
        /// Returns the compliance audit trail for a deployment contract.
        /// </summary>
        /// <param name="id">Deployment contract ID.</param>
        /// <returns>Compliance audit trail with ordered lifecycle events.</returns>
        [HttpGet("audit/{id}")]
        [ProducesResponseType(typeof(BackendDeploymentAuditTrail), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAuditTrail([FromRoute] string id)
        {
            var result = await _service.GetAuditTrailAsync(id);
            if (!result.Events.Any() && string.IsNullOrEmpty(result.DeployerAddress))
                return NotFound(result);
            return Ok(result);
        }
    }
}
