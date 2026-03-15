using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Compliance case management controller providing full lifecycle management of
    /// compliance cases: creation, evidence collection, escalation handling, remediation
    /// tracking, state transitions, timeline auditing, and readiness evaluation.
    ///
    /// All endpoints are fail-closed: missing data returns errors rather than partial results.
    /// Unauthenticated requests return 401; resource-not-found returns 404.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance-cases")]
    [Produces("application/json")]
    public class ComplianceCaseManagementController : ControllerBase
    {
        private readonly IComplianceCaseManagementService _service;
        private readonly ILogger<ComplianceCaseManagementController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ComplianceCaseManagementController"/>.
        /// </summary>
        public ComplianceCaseManagementController(
            IComplianceCaseManagementService service,
            ILogger<ComplianceCaseManagementController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ── Create ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new compliance case. Idempotent: if an active case for the same
        /// (issuerId, subjectId, type) already exists, returns that case.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(CreateComplianceCaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CreateComplianceCaseResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateCase([FromBody] CreateComplianceCaseRequest request)
        {
            var actorId       = GetActorId();
            var correlationId = GetCorrelationId();
            request.CorrelationId ??= correlationId;

            _logger.LogInformation(
                "CreateCase. IssuerId={IssuerId} SubjectId={SubjectId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.IssuerId),
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var result = await _service.CreateCaseAsync(request, actorId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Get ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves a compliance case by ID.
        /// Automatically checks evidence freshness and transitions to Stale if expired.
        /// </summary>
        /// <param name="caseId">Unique case identifier.</param>
        [HttpGet("{caseId}")]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCase(string caseId)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "GetCase. CaseId={CaseId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.GetCaseAsync(caseId, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── List ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Lists compliance cases with optional filters. Uses POST for filter body.
        /// </summary>
        [HttpPost("list")]
        [ProducesResponseType(typeof(ListComplianceCasesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ListComplianceCasesResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ListCases([FromBody] ListComplianceCasesRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "ListCases. Actor={Actor} State={State} IssuerId={IssuerId}",
                LoggingHelper.SanitizeLogInput(actorId),
                request.State?.ToString() ?? "all",
                LoggingHelper.SanitizeLogInput(request.IssuerId ?? "all"));

            var result = await _service.ListCasesAsync(request, actorId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Update ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates mutable fields on a compliance case (priority, reviewer, jurisdiction, etc.).
        /// </summary>
        /// <param name="caseId">Unique case identifier.</param>
        [HttpPatch("{caseId}")]
        [ProducesResponseType(typeof(UpdateComplianceCaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(UpdateComplianceCaseResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(UpdateComplianceCaseResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateCase(string caseId, [FromBody] UpdateComplianceCaseRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "UpdateCase. CaseId={CaseId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.UpdateCaseAsync(caseId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Transition ─────────────────────────────────────────────────────────

        /// <summary>
        /// Transitions a compliance case to a new lifecycle state.
        /// Only valid transitions per the state machine are accepted.
        /// </summary>
        /// <param name="caseId">Unique case identifier.</param>
        [HttpPost("{caseId}/transition")]
        [ProducesResponseType(typeof(UpdateComplianceCaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(UpdateComplianceCaseResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(UpdateComplianceCaseResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> TransitionState(string caseId, [FromBody] TransitionCaseStateRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "TransitionState. CaseId={CaseId} NewState={NewState} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                request.NewState.ToString(),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.TransitionStateAsync(caseId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Evidence ───────────────────────────────────────────────────────────

        /// <summary>
        /// Adds a normalized evidence summary to a compliance case.
        /// </summary>
        /// <param name="caseId">Unique case identifier.</param>
        [HttpPost("{caseId}/evidence")]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddEvidence(string caseId, [FromBody] AddEvidenceRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "AddEvidence. CaseId={CaseId} EvidenceType={EvidenceType} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(request.EvidenceType),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.AddEvidenceAsync(caseId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Remediation Tasks ──────────────────────────────────────────────────

        /// <summary>Adds a remediation task to a compliance case.</summary>
        /// <param name="caseId">Unique case identifier.</param>
        [HttpPost("{caseId}/remediation-tasks")]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddRemediationTask(string caseId, [FromBody] AddRemediationTaskRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "AddRemediationTask. CaseId={CaseId} Title={Title} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(request.Title),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.AddRemediationTaskAsync(caseId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Resolves or dismisses a remediation task on a compliance case.</summary>
        /// <param name="caseId">Unique case identifier.</param>
        /// <param name="taskId">Remediation task identifier.</param>
        [HttpPost("{caseId}/remediation-tasks/{taskId}/resolve")]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ResolveRemediationTask(
            string caseId, string taskId, [FromBody] ResolveRemediationTaskRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "ResolveRemediationTask. CaseId={CaseId} TaskId={TaskId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(taskId),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.ResolveRemediationTaskAsync(caseId, taskId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Escalations ────────────────────────────────────────────────────────

        /// <summary>Raises an escalation on a compliance case.</summary>
        /// <param name="caseId">Unique case identifier.</param>
        [HttpPost("{caseId}/escalations")]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AddEscalation(string caseId, [FromBody] AddEscalationRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "AddEscalation. CaseId={CaseId} Type={Type} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                request.Type.ToString(),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.AddEscalationAsync(caseId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Resolves an escalation on a compliance case.</summary>
        /// <param name="caseId">Unique case identifier.</param>
        /// <param name="escalationId">Escalation identifier.</param>
        [HttpPost("{caseId}/escalations/{escalationId}/resolve")]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(GetComplianceCaseResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ResolveEscalation(
            string caseId, string escalationId, [FromBody] ResolveEscalationRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "ResolveEscalation. CaseId={CaseId} EscalationId={EscalationId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(escalationId),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.ResolveEscalationAsync(caseId, escalationId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Timeline ───────────────────────────────────────────────────────────

        /// <summary>Returns the chronological audit trail for a compliance case.</summary>
        /// <param name="caseId">Unique case identifier.</param>
        [HttpGet("{caseId}/timeline")]
        [ProducesResponseType(typeof(CaseTimelineResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CaseTimelineResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetTimeline(string caseId)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "GetTimeline. CaseId={CaseId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.GetTimelineAsync(caseId, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Readiness ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the fail-closed readiness summary for a compliance case.
        /// Automatically evaluates evidence freshness before computing readiness.
        /// </summary>
        /// <param name="caseId">Unique case identifier.</param>
        [HttpGet("{caseId}/readiness")]
        [ProducesResponseType(typeof(CaseReadinessSummaryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CaseReadinessSummaryResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetReadiness(string caseId)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "GetReadiness. CaseId={CaseId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.GetReadinessSummaryAsync(caseId, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Private Helpers ────────────────────────────────────────────────────

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
