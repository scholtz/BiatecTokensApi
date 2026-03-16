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

        // ── Ongoing Monitoring ─────────────────────────────────────────────────

        /// <summary>
        /// Configures or updates the ongoing monitoring schedule for a compliance case.
        /// Sets the review frequency and calculates the next review due date.
        /// May be applied to cases in any state to enrol them in periodic monitoring.
        /// </summary>
        /// <param name="caseId">Unique case identifier.</param>
        /// <param name="request">Monitoring schedule configuration.</param>
        [HttpPost("{caseId}/monitoring-schedule")]
        [ProducesResponseType(typeof(SetMonitoringScheduleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SetMonitoringScheduleResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(SetMonitoringScheduleResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SetMonitoringSchedule(string caseId, [FromBody] SetMonitoringScheduleRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "SetMonitoringSchedule. CaseId={CaseId} Frequency={Freq} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(request.Frequency.ToString()),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.SetMonitoringScheduleAsync(caseId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Records the outcome of a periodic monitoring review for a compliance case.
        /// Updates the monitoring schedule timestamps and appends an auditable timeline entry.
        /// When outcome is EscalationRequired and CreateFollowUpCase is true, a new
        /// OngoingMonitoring case is automatically created and linked.
        /// </summary>
        /// <param name="caseId">Unique case identifier.</param>
        /// <param name="request">Monitoring review outcome and notes.</param>
        [HttpPost("{caseId}/monitoring-reviews")]
        [ProducesResponseType(typeof(RecordMonitoringReviewResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RecordMonitoringReviewResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(RecordMonitoringReviewResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RecordMonitoringReview(string caseId, [FromBody] RecordMonitoringReviewRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "RecordMonitoringReview. CaseId={CaseId} Outcome={Outcome} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(request.Outcome.ToString()),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.RecordMonitoringReviewAsync(caseId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Triggers a scan across all cases with active monitoring schedules and marks
        /// any overdue reviews. This endpoint is intended for scheduled invocation (e.g.,
        /// from a cron job or background task) to surface cases requiring operator attention.
        /// Returns the number of cases inspected and the IDs of any overdue cases found.
        /// </summary>
        [HttpPost("periodic-review-check")]
        [ProducesResponseType(typeof(TriggerPeriodicReviewCheckResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> TriggerPeriodicReviewCheck()
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "TriggerPeriodicReviewCheck. Actor={Actor}",
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.TriggerPeriodicReviewCheckAsync(actorId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Export case evidence bundle ────────────────────────────────────────

        /// <summary>
        /// Exports a regulator/audit-ready evidence bundle for the specified compliance case.
        /// The bundle contains a full case snapshot, the chronological timeline, and export metadata
        /// including a SHA-256 content hash. Each export is recorded in the audit log and triggers
        /// a <c>ComplianceCaseExported</c> webhook event.
        /// </summary>
        /// <param name="caseId">The compliance case identifier.</param>
        /// <param name="request">Export options (format, requestedBy).</param>
        /// <returns>The serialised evidence bundle on success.</returns>
        [HttpPost("{caseId}/export")]
        [ProducesResponseType(typeof(ExportComplianceCaseResponse), 200)]
        [ProducesResponseType(typeof(ExportComplianceCaseResponse), 400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ExportCase(string caseId, [FromBody] ExportComplianceCaseRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "ExportCase. CaseId={CaseId} Format={Format} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(request.Format),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.ExportCaseAsync(caseId, request, actorId);

            if (!result.Success && result.ErrorCode == "NOT_FOUND")
                return NotFound(result);

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
