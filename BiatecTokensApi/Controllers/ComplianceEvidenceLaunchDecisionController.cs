using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Compliance Evidence and Launch Decision API
    /// </summary>
    /// <remarks>
    /// Delivers deterministic, auditable launch readiness for enterprise tokenization workflows.
    ///
    /// This controller answers the following enterprise questions:
    /// - **What checks have passed and which remain?**
    /// - **What risk does each blocker represent?**
    /// - **What must happen before launch is permitted?**
    /// - **What evidence supports this decision?**
    ///
    /// All evaluations are idempotent, produce stable decision IDs, and include full
    /// decision traces for compliance audit and regulatory review.
    ///
    /// Endpoints:
    /// - `POST /decision` – Evaluate and record a launch decision.
    /// - `GET  /decision/{id}` – Retrieve a previously recorded decision.
    /// - `GET  /decision/{id}/trace` – Retrieve the full rule evaluation trace.
    /// - `GET  /decisions/{ownerId}` – List recent decisions for an owner.
    /// - `POST /evidence` – Retrieve a compliance evidence bundle.
    /// - `GET  /health` – API health check.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance-evidence")]
    [Produces("application/json")]
    public class ComplianceEvidenceLaunchDecisionController : ControllerBase
    {
        private readonly IComplianceEvidenceLaunchDecisionService _service;
        private readonly ILogger<ComplianceEvidenceLaunchDecisionController> _logger;

        /// <summary>
        /// Initialises a new instance of
        /// <see cref="ComplianceEvidenceLaunchDecisionController"/>.
        /// </summary>
        public ComplianceEvidenceLaunchDecisionController(
            IComplianceEvidenceLaunchDecisionService service,
            ILogger<ComplianceEvidenceLaunchDecisionController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ── Decision evaluation ───────────────────────────────────────────────

        /// <summary>
        /// Evaluates launch readiness and produces a structured compliance decision
        /// </summary>
        /// <param name="request">Launch decision request</param>
        /// <returns>
        /// Comprehensive decision response with status, blockers, warnings,
        /// recommended actions, evidence summary, and correlation metadata.
        /// </returns>
        /// <remarks>
        /// Performs a deterministic evaluation of all compliance prerequisites for
        /// token launch.  The evaluation engine runs a fixed, ordered rule set so
        /// that traces are stable across replays.
        ///
        /// **Idempotency**: Supply an `IdempotencyKey` to ensure repeated calls with
        /// the same inputs return the same decision without re-running evaluation.
        ///
        /// **Decision status values:**
        /// - `Ready` – All prerequisites met; launch is permitted.
        /// - `Blocked` – One or more hard blockers prevent launch.
        /// - `Warning` – Soft warnings present; launch may proceed with acknowledged risk.
        /// - `NeedsReview` – Manual compliance review required before launch.
        ///
        /// **Response fields:**
        /// - `decisionId` – Stable ID for subsequent trace/evidence retrieval.
        /// - `canLaunch` – Boolean indicating if launch is currently permitted.
        /// - `blockers` – Ordered list of blocking issues with remediation steps.
        /// - `warnings` – Non-critical advisory items.
        /// - `recommendedActions` – Prioritised next steps.
        /// - `evidenceSummary` – Summary of evidence items evaluated.
        /// - `policyVersion` – Policy version used for this evaluation.
        /// - `correlationId` – Request correlation for log tracing.
        /// </remarks>
        [HttpPost("decision")]
        [ProducesResponseType(typeof(LaunchDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EvaluateLaunchDecision([FromBody] LaunchDecisionRequest request)
        {
            try
            {
                var response = await _service.EvaluateLaunchDecisionAsync(request);

                if (!response.Success)
                {
                    _logger.LogWarning(
                        "Launch decision validation failed: Code={Code}, CorrelationId={Corr}",
                        response.ErrorCode,
                        response.CorrelationId);

                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = response.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = response.ErrorMessage ?? "Invalid request.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error evaluating launch decision");

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred during launch decision evaluation.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Decision retrieval ────────────────────────────────────────────────

        /// <summary>
        /// Retrieves a previously recorded launch decision
        /// </summary>
        /// <param name="decisionId">Decision identifier returned by POST /decision</param>
        /// <param name="correlationId">Optional correlation ID for tracing</param>
        /// <returns>Full decision record including status, blockers, warnings, and evidence</returns>
        [HttpGet("decision/{decisionId}")]
        [ProducesResponseType(typeof(LaunchDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDecision(
            [FromRoute] string decisionId,
            [FromQuery] string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(decisionId))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INVALID_REQUEST,
                    ErrorMessage = "Decision ID is required.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }

            try
            {
                var decision = await _service.GetDecisionAsync(decisionId, correlationId);

                if (decision == null)
                {
                    return NotFound(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.NOT_FOUND,
                        ErrorMessage = $"Decision '{LoggingHelper.SanitizeLogInput(decisionId)}' was not found.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(decision);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving decision {DecisionId}",
                    LoggingHelper.SanitizeLogInput(decisionId));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving the decision.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Decision trace ────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves the full rule evaluation trace for a decision
        /// </summary>
        /// <param name="decisionId">Decision identifier</param>
        /// <param name="correlationId">Optional correlation ID for tracing</param>
        /// <returns>
        /// Ordered list of rule evaluations with inputs, outcomes, rationale,
        /// and remediation guidance — enabling traceable root cause analysis.
        /// </returns>
        /// <remarks>
        /// The decision trace provides:
        /// - `ruleId` – Stable rule identifier (e.g., RULE-KYC-001)
        /// - `ruleName` – Human-readable rule name
        /// - `outcome` – Pass / Warning / Fail / Skipped
        /// - `rationale` – Why the rule produced this outcome
        /// - `remediationGuidance` – What to do if outcome is Fail or Warning
        /// - `evaluationOrder` – Deterministic evaluation sequence
        /// - `evidenceIds` – Evidence items referenced by this rule
        /// </remarks>
        [HttpGet("decision/{decisionId}/trace")]
        [ProducesResponseType(typeof(DecisionTraceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDecisionTrace(
            [FromRoute] string decisionId,
            [FromQuery] string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(decisionId))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INVALID_REQUEST,
                    ErrorMessage = "Decision ID is required.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }

            try
            {
                var trace = await _service.GetDecisionTraceAsync(new DecisionTraceRequest
                {
                    DecisionId = decisionId,
                    CorrelationId = correlationId
                });

                if (!trace.Success)
                {
                    if (trace.ErrorCode == "DECISION_NOT_FOUND")
                    {
                        return NotFound(new ApiErrorResponse
                        {
                            Success = false,
                            ErrorCode = ErrorCodes.NOT_FOUND,
                            ErrorMessage = trace.ErrorMessage ?? "Decision not found.",
                            Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                        });
                    }

                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = trace.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = trace.ErrorMessage ?? "Invalid request.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(trace);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving decision trace {DecisionId}",
                    LoggingHelper.SanitizeLogInput(decisionId));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving the decision trace.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Decision listing ─────────────────────────────────────────────────

        /// <summary>
        /// Lists recent launch decisions for an owner
        /// </summary>
        /// <param name="ownerId">Owner identifier</param>
        /// <param name="limit">Maximum results to return (1–100, default 20)</param>
        /// <param name="correlationId">Optional correlation ID for tracing</param>
        /// <returns>Decisions ordered by evaluation timestamp, most recent first</returns>
        [HttpGet("decisions/{ownerId}")]
        [ProducesResponseType(typeof(List<LaunchDecisionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ListDecisions(
            [FromRoute] string ownerId,
            [FromQuery] int limit = 20,
            [FromQuery] string? correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INVALID_REQUEST,
                    ErrorMessage = "Owner ID is required.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }

            if (limit < 1 || limit > 100)
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INVALID_REQUEST,
                    ErrorMessage = "Limit must be between 1 and 100.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }

            try
            {
                var decisions = await _service.ListDecisionsAsync(ownerId, limit, correlationId);
                return Ok(decisions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing decisions for owner {OwnerId}",
                    LoggingHelper.SanitizeLogInput(ownerId));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while listing decisions.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Evidence bundle ───────────────────────────────────────────────────

        /// <summary>
        /// Retrieves a compliance evidence bundle for an owner
        /// </summary>
        /// <param name="request">Evidence bundle request with optional filters</param>
        /// <returns>
        /// Bundle of compliance evidence items with provenance metadata, timestamps,
        /// validation status, data integrity hashes, and source traceability.
        /// </returns>
        /// <remarks>
        /// Evidence items include:
        /// - `evidenceId` – Unique evidence identifier
        /// - `category` – Identity, Policy, Entitlement, Jurisdiction, Integration, etc.
        /// - `source` – Source system that generated the evidence
        /// - `timestamp` – UTC collection time
        /// - `validationStatus` – Valid, Pending, Invalid, Stale, Unavailable
        /// - `rationale` – Human-readable description
        /// - `dataHash` – SHA-256 integrity hash
        ///
        /// Supports filtering by category, decision ID, and timestamp range.
        /// </remarks>
        [HttpPost("evidence")]
        [ProducesResponseType(typeof(EvidenceBundleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetEvidenceBundle([FromBody] EvidenceBundleRequest request)
        {
            try
            {
                var bundle = await _service.GetEvidenceBundleAsync(request);

                if (!bundle.Success)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = bundle.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = bundle.ErrorMessage ?? "Invalid request.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(bundle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evidence bundle for owner {OwnerId}",
                    LoggingHelper.SanitizeLogInput(request.OwnerId));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving the evidence bundle.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Health ─────────────────────────────────────────────────────────

        /// <summary>
        /// Health check for the Compliance Evidence and Launch Decision API
        /// </summary>
        /// <returns>API health status</returns>
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiStatusResponse), StatusCodes.Status200OK)]
        public IActionResult Health()
        {
            return Ok(new ApiStatusResponse
            {
                Status = "Healthy",
                Version = "v1.0",
                Timestamp = DateTime.UtcNow,
                Environment = "Production"
            });
        }
    }
}
