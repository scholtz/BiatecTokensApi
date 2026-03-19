using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ProviderBackedCompliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provider-backed compliance case execution API.
    /// </summary>
    /// <remarks>
    /// This API exposes execution, status query, and sign-off evidence bundle operations
    /// for the provider-backed compliance execution path. It bridges the compliance case
    /// lifecycle with external provider validation and enforces fail-closed behaviour for
    /// protected paths.
    ///
    /// Key capabilities:
    /// - **Decision execution**: Approve, reject, return-for-information, sanctions review,
    ///   and escalation decisions executed through real or sandbox providers with durable
    ///   evidence artifacts.
    /// - **Execution status**: Full history of provider-backed executions for a case,
    ///   including diagnostics from the most recent attempt.
    /// - **Sign-off evidence**: Content-hashed, release-grade sign-off bundles that
    ///   business owners and compliance leads can review. Fail-closed: rejects simulated
    ///   evidence in protected paths.
    /// - **Fail-closed semantics**: RequireProviderBacked and RequireKycAmlSignOff guards
    ///   prevent silent simulated approvals from reaching protected sign-off bundles.
    ///
    /// Endpoints:
    /// - `POST  /{caseId}/execute`       – Execute a provider-backed compliance decision.
    /// - `GET   /{caseId}/status`        – Get execution status and history for a case.
    /// - `POST  /{caseId}/sign-off`      – Build a release-grade sign-off evidence bundle.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/provider-compliance")]
    [Produces("application/json")]
    public class ProviderBackedComplianceController : ControllerBase
    {
        private readonly IProviderBackedComplianceExecutionService _service;
        private readonly ILogger<ProviderBackedComplianceController> _logger;

        /// <summary>
        /// Initialises the controller.
        /// </summary>
        public ProviderBackedComplianceController(
            IProviderBackedComplianceExecutionService service,
            ILogger<ProviderBackedComplianceController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ── Execute Decision ──────────────────────────────────────────────────

        /// <summary>
        /// Executes a provider-backed compliance decision (approve, reject,
        /// return-for-information, sanctions review, or escalation) on the specified case.
        /// </summary>
        /// <remarks>
        /// When <c>RequireProviderBacked</c> is true, the call fails if the configured
        /// execution mode is Simulated. When <c>RequireKycAmlSignOff</c> is true and
        /// execution mode is LiveProvider or ProtectedSandbox, the call fails unless
        /// valid KYC/AML sign-off evidence exists for the case subject.
        ///
        /// On success, a durable evidence artifact is created and the underlying compliance
        /// case state is transitioned. On failure, actionable diagnostics are always returned.
        /// </remarks>
        [HttpPost("{caseId}/execute")]
        [ProducesResponseType(typeof(ExecuteProviderBackedDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ExecuteProviderBackedDecisionResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ExecuteProviderBackedDecisionResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ExecuteDecision(string caseId, [FromBody] ExecuteProviderBackedDecisionRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "ExecuteDecision. CaseId={CaseId} DecisionKind={DecisionKind} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                request?.DecisionKind.ToString() ?? "null",
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.ExecuteDecisionAsync(caseId, request!, actorId);
            if (!result.Success && result.ErrorCode == "CASE_NOT_FOUND") return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Get Status ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current execution status and full execution history for the
        /// specified compliance case.
        /// </summary>
        /// <remarks>
        /// Includes diagnostics from the most recent execution attempt and flags
        /// whether any history entry qualifies as release-grade evidence.
        /// </remarks>
        [HttpGet("{caseId}/status")]
        [ProducesResponseType(typeof(GetProviderBackedExecutionStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetProviderBackedExecutionStatusResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetExecutionStatus(string caseId)
        {
            var actorId       = GetActorId();
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetExecutionStatus. CaseId={CaseId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var result = await _service.GetExecutionStatusAsync(caseId, actorId, correlationId);
            return result.Success ? Ok(result) : NotFound(result);
        }

        // ── Build Sign-Off Evidence ───────────────────────────────────────────

        /// <summary>
        /// Builds a durable, content-hashed sign-off evidence bundle for the specified
        /// compliance case.
        /// </summary>
        /// <remarks>
        /// When <c>RequireProviderBackedEvidence</c> is true, the build fails if the
        /// execution history contains any simulated executions. The resulting bundle
        /// includes a SHA-256 content hash for integrity verification and an
        /// <c>IsReleaseGradeEvidence</c> flag that is true only when all history entries
        /// are LiveProvider or ProtectedSandbox executions that completed successfully.
        /// </remarks>
        [HttpPost("{caseId}/sign-off")]
        [ProducesResponseType(typeof(BuildProviderBackedSignOffEvidenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BuildProviderBackedSignOffEvidenceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BuildProviderBackedSignOffEvidenceResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> BuildSignOffEvidence(string caseId, [FromBody] BuildProviderBackedSignOffEvidenceRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "BuildSignOffEvidence. CaseId={CaseId} RequireProviderBacked={RequireProviderBacked} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                request?.RequireProviderBackedEvidence.ToString() ?? "null",
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.BuildSignOffEvidenceAsync(caseId, request!, actorId);
            if (!result.Success && result.ErrorCode == "CASE_NOT_FOUND") return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private string GetActorId() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "anonymous";

        private string GetCorrelationId() =>
            HttpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var val) && !string.IsNullOrWhiteSpace(val)
                ? val.ToString()
                : Guid.NewGuid().ToString();
    }
}
