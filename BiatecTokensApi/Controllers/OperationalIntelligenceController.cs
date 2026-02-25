using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.OperationalIntelligence;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Deterministic Operational Intelligence and Audit Evidence API.
    ///
    /// Exposes canonical operation timelines, compliance checkpoint summaries,
    /// standardized risk signals, and stakeholder-report-ready payloads.
    ///
    /// All endpoints are:
    /// - **Idempotent** – repeated calls return semantically identical responses.
    /// - **Privacy-safe** – no internal stack traces or secrets are exposed.
    /// - **Correlation-linked** – every response carries a correlation ID for audit tracing.
    /// - **Backward-compatible** – existing schema fields are never removed or renamed.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/operational-intelligence")]
    [Produces("application/json")]
    public class OperationalIntelligenceController : ControllerBase
    {
        private readonly IOperationalIntelligenceService _service;
        private readonly ILogger<OperationalIntelligenceController> _logger;

        public OperationalIntelligenceController(
            IOperationalIntelligenceService service,
            ILogger<OperationalIntelligenceController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC1 – Timeline API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a deterministically ordered operation timeline for a deployment.
        /// </summary>
        /// <param name="request">Timeline request including deployment ID and optional pagination cursor.</param>
        /// <returns>Ordered list of state-transition events with stable semantics.</returns>
        /// <remarks>
        /// **Contract guarantees:**
        /// - Entries are always returned oldest-first (ascending UTC timestamp).
        /// - Each entry carries a stable `EventCode` and `CorrelationId`.
        /// - Pagination is cursor-based for idempotent retrieval under retries.
        /// - No internal implementation details appear in responses.
        /// </remarks>
        [HttpPost("timeline")]
        [ProducesResponseType(typeof(OperationTimelineResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTimeline([FromBody] OperationTimelineRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.DeploymentId))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success   = false,
                        ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                        ErrorMessage = "DeploymentId is required.",
                        RemediationHint = "Provide a valid deployment identifier.",
                        Path      = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var response = await _service.GetOperationTimelineAsync(request);

                if (!response.Success)
                {
                    if (response.ErrorCode == ErrorCodes.NOT_FOUND)
                        return NotFound(ToApiError(response));
                    return StatusCode(StatusCodes.Status500InternalServerError, ToApiError(response));
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving operation timeline for deployment {DeploymentId}",
                    LoggingHelper.SanitizeLogInput(request?.DeploymentId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success   = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred.",
                    RemediationHint = "Retry the request. Contact support if the error persists.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC2 – Compliance Checkpoints API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a normalized compliance checkpoint summary for a deployment.
        /// </summary>
        /// <param name="request">Checkpoint request including deployment ID.</param>
        /// <returns>Normalized checkpoint states with business-readable guidance.</returns>
        /// <remarks>
        /// **Contract guarantees:**
        /// - Checkpoint states use the stable `ComplianceCheckpointState` enum (Pending / InReview / Satisfied / Failed / Blocked).
        /// - Each checkpoint includes a business-readable `Explanation` and optional `RecommendedAction`.
        /// - `OverallPosture` is a stable human-readable summary derived deterministically from checkpoint states.
        /// - `BlockingCount` counts only checkpoints that block deployment progression.
        /// </remarks>
        [HttpPost("compliance-checkpoints")]
        [ProducesResponseType(typeof(ComplianceCheckpointResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetComplianceCheckpoints([FromBody] ComplianceCheckpointRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.DeploymentId))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success   = false,
                        ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                        ErrorMessage = "DeploymentId is required.",
                        RemediationHint = "Provide a valid deployment identifier.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var response = await _service.GetComplianceCheckpointsAsync(request);

                if (!response.Success)
                {
                    if (response.ErrorCode == ErrorCodes.NOT_FOUND)
                        return NotFound(ToApiError(response));
                    return StatusCode(StatusCodes.Status500InternalServerError, ToApiError(response));
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving compliance checkpoints for deployment {DeploymentId}",
                    LoggingHelper.SanitizeLogInput(request?.DeploymentId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success   = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred.",
                    RemediationHint = "Retry the request. Contact support if the error persists.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC3 – Risk Classification
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Maps an error code to a bounded operational risk signal.
        /// </summary>
        /// <param name="errorCode">Stable error code to classify.</param>
        /// <param name="correlationId">Optional correlation ID for audit linking.</param>
        /// <returns>Operational risk signal with category, severity, and remediation hint.</returns>
        /// <remarks>
        /// **Contract guarantees:**
        /// - Deterministic: same error code always maps to the same `OperationalRiskCategory`.
        /// - Bounded: only the nine documented `OperationalRiskCategory` values are returned.
        /// - Safe: no internal details appear in the signal description or hint.
        /// </remarks>
        [HttpGet("classify-risk")]
        [ProducesResponseType(typeof(OperationalRiskSignal), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public IActionResult ClassifyRisk(
            [FromQuery] string errorCode,
            [FromQuery] string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success   = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "errorCode query parameter is required.",
                    RemediationHint = "Provide a stable error code to classify.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }

            var signal = _service.ClassifyRisk(
                LoggingHelper.SanitizeLogInput(errorCode),
                correlationId ?? Guid.NewGuid().ToString("N")[..16]);

            return Ok(signal);
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC6 – Stakeholder Report
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a privacy-safe stakeholder report payload for a deployment.
        /// </summary>
        /// <param name="request">Report request including deployment ID.</param>
        /// <returns>Stakeholder-facing summary with issuance progress, compliance posture, and blockers.</returns>
        /// <remarks>
        /// **Contract guarantees:**
        /// - Payloads never contain internal addresses, keys, or stack traces.
        /// - `UnresolvedBlockers` counts only error-severity signals requiring action.
        /// - `PrimaryRecommendedAction` is null when no action is needed.
        /// - `RiskSignals` are filtered to signals safe for non-technical stakeholders.
        /// </remarks>
        [HttpPost("stakeholder-report")]
        [ProducesResponseType(typeof(StakeholderReportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStakeholderReport([FromBody] StakeholderReportRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.DeploymentId))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success   = false,
                        ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                        ErrorMessage = "DeploymentId is required.",
                        RemediationHint = "Provide a valid deployment identifier.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var response = await _service.GetStakeholderReportAsync(request);

                if (!response.Success)
                {
                    if (response.ErrorCode == ErrorCodes.NOT_FOUND)
                        return NotFound(ToApiError(response));
                    return StatusCode(StatusCodes.Status500InternalServerError, ToApiError(response));
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating stakeholder report for deployment {DeploymentId}",
                    LoggingHelper.SanitizeLogInput(request?.DeploymentId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success   = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred.",
                    RemediationHint = "Retry the request. Contact support if the error persists.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Health
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Health check for the operational intelligence API.
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiStatusResponse), StatusCodes.Status200OK)]
        public IActionResult Health() =>
            Ok(new ApiStatusResponse
            {
                Status      = "Healthy",
                Version     = "v1.0",
                Timestamp   = DateTime.UtcNow,
                Environment = "Production"
            });

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static ApiErrorResponse ToApiError(OperationTimelineResponse r) =>
            new()
            {
                Success   = false,
                ErrorCode = r.ErrorCode ?? ErrorCodes.INTERNAL_SERVER_ERROR,
                ErrorMessage = r.ErrorMessage ?? "An error occurred.",
                RemediationHint = r.RemediationHint,
                CorrelationId   = r.CorrelationId
            };

        private static ApiErrorResponse ToApiError(ComplianceCheckpointResponse r) =>
            new()
            {
                Success   = false,
                ErrorCode = r.ErrorCode ?? ErrorCodes.INTERNAL_SERVER_ERROR,
                ErrorMessage = r.ErrorMessage ?? "An error occurred.",
                RemediationHint = r.RemediationHint,
                CorrelationId   = r.CorrelationId
            };

        private static ApiErrorResponse ToApiError(StakeholderReportResponse r) =>
            new()
            {
                Success   = false,
                ErrorCode = r.ErrorCode ?? ErrorCodes.INTERNAL_SERVER_ERROR,
                ErrorMessage = r.ErrorMessage ?? "An error occurred.",
                RemediationHint = r.RemediationHint
            };
    }
}
