using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.DeploymentSignOff;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Deployment Sign-Off Validation API
    /// </summary>
    /// <remarks>
    /// Provides sign-off readiness validation for completed token deployments.
    /// This endpoint is intended for enterprise sign-off journeys where a business operator
    /// must confirm that all required deployment evidence is present and accounted for.
    ///
    /// Key capabilities:
    /// - **Fail-closed validation**: Missing required fields (asset ID, transaction ID,
    ///   confirmed round, deployer address) are treated as explicit failures, never silently ignored.
    /// - **Structured criterion results**: Each sign-off criterion is evaluated individually,
    ///   reporting its outcome (Pass/Fail/NotApplicable) with a detailed description.
    /// - **Actionable user guidance**: Failure payloads include non-technical instructions
    ///   suitable for a non-crypto-native operator to understand and resolve.
    /// - **Verdict classification**: The overall verdict clearly distinguishes Approved,
    ///   Blocked, InProgress, and TerminalFailure states with no ambiguous intermediary shapes.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/deployment-sign-off")]
    [Produces("application/json")]
    public class DeploymentSignOffController : ControllerBase
    {
        private readonly IDeploymentSignOffService _signOffService;
        private readonly ILogger<DeploymentSignOffController> _logger;

        /// <summary>
        /// Initialises a new instance of <see cref="DeploymentSignOffController"/>.
        /// </summary>
        public DeploymentSignOffController(
            IDeploymentSignOffService signOffService,
            ILogger<DeploymentSignOffController> logger)
        {
            _signOffService = signOffService;
            _logger = logger;
        }

        /// <summary>
        /// Validates whether a deployment is ready for enterprise sign-off.
        /// </summary>
        /// <param name="request">
        /// Validation request specifying the deployment ID and which criteria are required
        /// for sign-off (asset ID, transaction ID, confirmed round, audit trail).
        /// </param>
        /// <returns>
        /// A structured sign-off proof document with per-criterion outcomes, an overall verdict,
        /// and actionable guidance for any failures.
        /// </returns>
        /// <remarks>
        /// **Sign-off is fail-closed**: if any required criterion fails, the verdict is
        /// <c>Blocked</c> and the response explains exactly what is missing and how to resolve it.
        ///
        /// **Verdict values:**
        /// - <c>Approved</c> – all required criteria passed; deployment is ready for sign-off.
        /// - <c>Blocked</c> – one or more required criteria failed.
        /// - <c>InProgress</c> – deployment has not yet reached a terminal state.
        /// - <c>TerminalFailure</c> – deployment failed; sign-off is not applicable.
        ///
        /// **Response guarantees:**
        /// - <c>isReadyForSignOff</c> is only <c>true</c> when verdict is <c>Approved</c>.
        /// - Every criterion that fails includes <c>userGuidance</c> with actionable instructions.
        /// - No ambiguous "partial success" shapes are returned.
        /// </remarks>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(DeploymentSignOffProof), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateSignOff(
            [FromBody] DeploymentSignOffValidationRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new ApiErrorResponse
                {
                    ErrorCode    = ErrorCodes.INVALID_REQUEST,
                    ErrorMessage = "Request body is required."
                });
            }

            try
            {
                _logger.LogInformation(
                    "Sign-off validation: DeploymentId={DeploymentId}",
                    LoggingHelper.SanitizeLogInput(request.DeploymentId));

                var result = await _signOffService.ValidateSignOffReadinessAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during sign-off validation");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode    = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred while validating sign-off readiness."
                });
            }
        }

        /// <summary>
        /// Generates a sign-off proof document for a deployment using default (all required) criteria.
        /// </summary>
        /// <param name="deploymentId">Deployment contract ID to generate proof for.</param>
        /// <returns>
        /// Structured sign-off proof document with per-criterion outcomes and overall verdict.
        /// </returns>
        /// <remarks>
        /// Equivalent to calling <c>POST /validate</c> with all criteria set to required.
        /// Use this endpoint when you want a complete sign-off evaluation with a single GET request.
        /// </remarks>
        [HttpGet("proof/{deploymentId}")]
        [ProducesResponseType(typeof(DeploymentSignOffProof), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSignOffProof([FromRoute] string deploymentId)
        {
            if (string.IsNullOrWhiteSpace(deploymentId))
            {
                return BadRequest(new ApiErrorResponse
                {
                    ErrorCode    = ErrorCodes.INVALID_REQUEST,
                    ErrorMessage = "deploymentId path parameter is required."
                });
            }

            try
            {
                _logger.LogInformation(
                    "Sign-off proof request: DeploymentId={DeploymentId}",
                    LoggingHelper.SanitizeLogInput(deploymentId));

                var result = await _signOffService.GenerateSignOffProofAsync(deploymentId);

                // Return 404 when deployment was not found (Blocked with DeploymentId criterion failure
                // that mentions "not found")
                if (result.Verdict == SignOffVerdict.Blocked &&
                    result.Criteria.Any(c =>
                        c.Name == "DeploymentId" &&
                        c.Outcome == SignOffCriterionOutcome.Fail))
                {
                    return NotFound(new ApiErrorResponse
                    {
                        ErrorCode    = "DEPLOYMENT_NOT_FOUND",
                        ErrorMessage = $"Deployment '{deploymentId}' was not found."
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error generating sign-off proof");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode    = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred while generating the sign-off proof."
                });
            }
        }
    }
}
