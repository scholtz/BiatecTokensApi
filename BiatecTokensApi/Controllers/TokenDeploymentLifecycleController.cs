using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Token Deployment Lifecycle API v1
    /// </summary>
    /// <remarks>
    /// Provides a deterministic, idempotent token deployment lifecycle with telemetry and
    /// reliability guardrails. Key capabilities:
    ///
    /// - **Idempotency**: Repeating a request with the same idempotency key returns a
    ///   cached result without creating duplicate resources.
    /// - **Telemetry**: Every stage transition is recorded as a structured telemetry event
    ///   with correlation ID for distributed tracing.
    /// - **Reliability guardrails**: Pre-deployment checks detect node unreachability,
    ///   retry exhaustion, timeouts, address mismatches, and conflicting deployments.
    /// - **Validation**: Strict input validation with deterministic, machine-readable error
    ///   codes and human-readable guidance.
    ///
    /// All evaluations are deterministic: identical inputs always produce identical outputs.
    /// Partial upstream failures return degraded-mode responses rather than hard errors.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/token-deployment-lifecycle")]
    [Produces("application/json")]
    public class TokenDeploymentLifecycleController : ControllerBase
    {
        private readonly ITokenDeploymentLifecycleService _lifecycleService;
        private readonly ILogger<TokenDeploymentLifecycleController> _logger;

        /// <summary>
        /// Initialises a new instance of <see cref="TokenDeploymentLifecycleController"/>.
        /// </summary>
        public TokenDeploymentLifecycleController(
            ITokenDeploymentLifecycleService lifecycleService,
            ILogger<TokenDeploymentLifecycleController> logger)
        {
            _lifecycleService = lifecycleService;
            _logger = logger;
        }

        /// <summary>
        /// Initiates or resumes a deterministic token deployment lifecycle.
        /// </summary>
        /// <param name="request">Deployment parameters including idempotency key, token standard, and network.</param>
        /// <returns>
        /// Deployment lifecycle response with current stage, idempotency status, telemetry events,
        /// guardrail findings, and progress snapshot.
        /// </returns>
        /// <remarks>
        /// **Idempotency behaviour**: If the same idempotency key is submitted again, the cached
        /// result is returned immediately with <c>isIdempotentReplay=true</c> and
        /// <c>idempotencyStatus=Duplicate</c>. No duplicate deployment is created.
        ///
        /// **Response highlights:**
        /// - `stage`: Current pipeline stage (Initialising → Validating → Submitting → Confirming → Completed / Failed)
        /// - `outcome`: Success / PartialSuccess / TransientFailure / TerminalFailure / Unknown
        /// - `idempotencyStatus`: New / Duplicate / Conflict / InProgress
        /// - `validationResults`: Per-field validation outcomes with machine-readable error codes
        /// - `guardrailFindings`: Reliability checks with severity and remediation hints
        /// - `telemetryEvents`: Ordered lifecycle events with correlation ID for distributed tracing
        /// </remarks>
        [HttpPost("initiate")]
        [ProducesResponseType(typeof(TokenDeploymentLifecycleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> InitiateDeployment([FromBody] TokenDeploymentLifecycleRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode    = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Request body is required."
                    });
                }

                _logger.LogInformation(
                    "Deployment initiation: Standard={Standard}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(request.TokenStandard),
                    LoggingHelper.SanitizeLogInput(request.Network));

                var result = await _lifecycleService.InitiateDeploymentAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error initiating deployment lifecycle");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode    = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred while initiating the deployment lifecycle."
                });
            }
        }

        /// <summary>
        /// Returns the current status of a deployment by its deployment ID.
        /// </summary>
        /// <param name="deploymentId">Deployment ID returned by the initiate endpoint.</param>
        /// <returns>Current lifecycle response including stage, outcome, and telemetry.</returns>
        [HttpGet("status/{deploymentId}")]
        [ProducesResponseType(typeof(TokenDeploymentLifecycleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDeploymentStatus([FromRoute] string deploymentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(deploymentId))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode    = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "deploymentId path parameter is required."
                    });
                }

                _logger.LogInformation(
                    "Deployment status query: DeploymentId={DeploymentId}",
                    LoggingHelper.SanitizeLogInput(deploymentId));

                var result = await _lifecycleService.GetDeploymentStatusAsync(deploymentId);

                if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new ApiErrorResponse
                    {
                        ErrorCode    = "DEPLOYMENT_NOT_FOUND",
                        ErrorMessage = result.Message
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving deployment status");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode    = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred while retrieving deployment status."
                });
            }
        }

        /// <summary>
        /// Retries a previously failed or timed-out deployment.
        /// </summary>
        /// <param name="request">Retry request including the original idempotency key.</param>
        /// <returns>Updated deployment lifecycle response with new retry count and telemetry.</returns>
        /// <remarks>
        /// Retries honour the <c>MaxRetryAttempts</c> limit from the original request.
        /// When the limit is exceeded, a terminal failure response is returned with a remediation hint.
        /// Retrying a deployment that already succeeded returns an idempotent replay of the success.
        /// </remarks>
        [HttpPost("retry")]
        [ProducesResponseType(typeof(TokenDeploymentLifecycleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RetryDeployment([FromBody] DeploymentRetryRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.IdempotencyKey))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode    = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "IdempotencyKey is required for retry."
                    });
                }

                _logger.LogInformation(
                    "Deployment retry: IdempotencyKey={Key}",
                    LoggingHelper.SanitizeLogInput(request.IdempotencyKey));

                var result = await _lifecycleService.RetryDeploymentAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrying deployment");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode    = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred while retrying the deployment."
                });
            }
        }

        /// <summary>
        /// Returns the telemetry events emitted during a deployment lifecycle.
        /// </summary>
        /// <param name="deploymentId">Deployment ID to retrieve telemetry for.</param>
        /// <returns>Ordered list of telemetry events with correlation IDs and contextual metadata.</returns>
        [HttpGet("telemetry/{deploymentId}")]
        [ProducesResponseType(typeof(List<DeploymentTelemetryEvent>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTelemetry([FromRoute] string deploymentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(deploymentId))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode    = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "deploymentId path parameter is required."
                    });
                }

                _logger.LogInformation(
                    "Telemetry query: DeploymentId={DeploymentId}",
                    LoggingHelper.SanitizeLogInput(deploymentId));

                var events = await _lifecycleService.GetTelemetryEventsAsync(deploymentId);
                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving deployment telemetry");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode    = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred while retrieving deployment telemetry."
                });
            }
        }

        /// <summary>
        /// Validates deployment inputs without submitting a transaction.
        /// </summary>
        /// <param name="request">Validation-only deployment request.</param>
        /// <returns>
        /// Deterministic validation response with per-field results, machine-readable error codes,
        /// and reliability guardrail findings.
        /// </returns>
        /// <remarks>
        /// This endpoint is idempotent and safe: it reads no state and writes no state.
        /// Identical inputs always produce identical validation results.
        /// </remarks>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(DeploymentValidationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateDeployment([FromBody] DeploymentValidationRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode    = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Request body is required."
                    });
                }

                _logger.LogInformation(
                    "Deployment validation: Standard={Standard}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(request.TokenStandard),
                    LoggingHelper.SanitizeLogInput(request.Network));

                var result = await _lifecycleService.ValidateDeploymentInputsAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error validating deployment inputs");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode    = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred while validating deployment inputs."
                });
            }
        }

        /// <summary>
        /// Evaluates reliability guardrails for a given deployment context.
        /// </summary>
        /// <param name="context">Guardrail evaluation context describing environment and deployment state.</param>
        /// <returns>Ordered list of guardrail findings with severity, blocking status, and remediation hints.</returns>
        [HttpPost("guardrails")]
        [ProducesResponseType(typeof(List<ReliabilityGuardrail>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public IActionResult EvaluateGuardrails([FromBody] GuardrailEvaluationContext context)
        {
            try
            {
                if (context == null)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        ErrorCode    = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Request body is required."
                    });
                }

                _logger.LogInformation(
                    "Guardrail evaluation: Standard={Standard}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(context.TokenStandard),
                    LoggingHelper.SanitizeLogInput(context.Network));

                var findings = _lifecycleService.EvaluateReliabilityGuardrails(context);
                return Ok(findings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error evaluating guardrails");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    ErrorCode    = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred while evaluating guardrails."
                });
            }
        }
    }
}
