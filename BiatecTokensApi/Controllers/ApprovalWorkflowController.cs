using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ApprovalWorkflow;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Enterprise approval workflow controller providing multi-stage release approval,
    /// release evidence readiness, and tamper-evident audit history APIs.
    ///
    /// All endpoints are actor-scoped. The actorId is resolved from the JWT bearer token.
    ///
    /// Authorization is fail-closed:
    ///   - All data endpoints require a valid JWT bearer token ([Authorize]).
    ///   - The health endpoint is anonymous for infrastructure monitoring.
    ///   - Unauthenticated requests return 401.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/approval-workflow")]
    [Produces("application/json")]
    public class ApprovalWorkflowController : ControllerBase
    {
        private readonly IApprovalWorkflowService _service;
        private readonly ILogger<ApprovalWorkflowController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ApprovalWorkflowController"/>.
        /// </summary>
        public ApprovalWorkflowController(
            IApprovalWorkflowService service,
            ILogger<ApprovalWorkflowController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ── Workflow State ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns the full approval workflow state for the given release package.
        ///
        /// The response includes:
        ///   - All 5 approval stage statuses (Compliance, Legal, Procurement, Executive, SharedOperations)
        ///   - Active blockers with severity and attribution
        ///   - Evidence readiness summary for each stage
        ///   - Current release posture (LaunchReady, BlockedByStageDecision, etc.)
        ///   - Active owner domain for frontend routing
        ///   - Human-readable posture rationale
        /// </summary>
        /// <param name="releasePackageId">Unique release package identifier.</param>
        [HttpGet("{releasePackageId}")]
        [ProducesResponseType(typeof(ApprovalWorkflowStateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApprovalWorkflowStateResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetApprovalWorkflowState(string releasePackageId)
        {
            string actorId       = GetActorId();
            string correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetApprovalWorkflowState. PackageId={PackageId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(releasePackageId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            ApprovalWorkflowStateResponse result =
                await _service.GetApprovalWorkflowStateAsync(releasePackageId, actorId, correlationId);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Stage Decision ─────────────────────────────────────────────────────

        /// <summary>
        /// Submits an approval stage decision for the given release package.
        ///
        /// The decision must not be Pending. Rejected, Blocked, and NeedsFollowUp
        /// decisions require a non-empty Note.
        ///
        /// The response includes the updated stage record, new release posture,
        /// and a DecisionId for audit cross-referencing.
        /// </summary>
        /// <param name="releasePackageId">Unique release package identifier.</param>
        /// <param name="request">Stage, decision, note, and optional evidence acknowledgements.</param>
        [HttpPost("{releasePackageId}/stages/decision")]
        [ProducesResponseType(typeof(SubmitStageDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SubmitStageDecisionResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SubmitStageDecision(
            string releasePackageId, [FromBody] SubmitStageDecisionRequest request)
        {
            string actorId       = GetActorId();
            string correlationId = GetCorrelationId();

            _logger.LogInformation(
                "SubmitStageDecision. PackageId={PackageId} Stage={Stage} Decision={Decision} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(releasePackageId),
                request?.StageType,
                request?.Decision,
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            SubmitStageDecisionResponse result =
                await _service.SubmitStageDecisionAsync(
                    releasePackageId, request!, actorId, correlationId);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Evidence Summary ───────────────────────────────────────────────────

        /// <summary>
        /// Returns the release evidence readiness summary for the given release package.
        ///
        /// Each of the 5 stage-linked evidence items is returned with its readiness category
        /// (Fresh, Stale, Missing, ConfigurationBlocked), freshness timestamps, and release-blocking flag.
        ///
        /// Use OverallReadiness for a single-field posture indicator.
        /// </summary>
        /// <param name="releasePackageId">Unique release package identifier.</param>
        [HttpGet("{releasePackageId}/evidence-summary")]
        [ProducesResponseType(typeof(ReleaseEvidenceSummaryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ReleaseEvidenceSummaryResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetReleaseEvidenceSummary(string releasePackageId)
        {
            string actorId       = GetActorId();
            string correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetReleaseEvidenceSummary. PackageId={PackageId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(releasePackageId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            ReleaseEvidenceSummaryResponse result =
                await _service.GetReleaseEvidenceSummaryAsync(releasePackageId, actorId, correlationId);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Audit History ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns the approval workflow audit history for the given release package.
        ///
        /// Events are returned newest-first (up to 100 events).
        /// Each event includes actor identity, state transition, note, and correlation ID.
        /// </summary>
        /// <param name="releasePackageId">Unique release package identifier.</param>
        [HttpGet("{releasePackageId}/audit-history")]
        [ProducesResponseType(typeof(ApprovalAuditHistoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApprovalAuditHistoryResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetApprovalAuditHistory(string releasePackageId)
        {
            string actorId       = GetActorId();
            string correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetApprovalAuditHistory. PackageId={PackageId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(releasePackageId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            ApprovalAuditHistoryResponse result =
                await _service.GetApprovalAuditHistoryAsync(releasePackageId, actorId, correlationId);

            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Health ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Anonymous health check endpoint for infrastructure monitoring.
        /// Returns 200 OK when the approval workflow service is reachable.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Health()
        {
            return Ok(new { Status = "Healthy", Service = "ApprovalWorkflow", Timestamp = DateTime.UtcNow });
        }

        // ── Private Helpers ────────────────────────────────────────────────────

        private string GetActorId() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "anonymous";

        private string GetCorrelationId() =>
            HttpContext.Request.Headers.TryGetValue("X-Correlation-Id", out Microsoft.Extensions.Primitives.StringValues val)
            && !string.IsNullOrWhiteSpace(val)
                ? val.ToString()
                : Guid.NewGuid().ToString();
    }
}
