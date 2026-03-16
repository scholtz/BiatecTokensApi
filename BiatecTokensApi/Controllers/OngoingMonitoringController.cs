using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.OngoingMonitoring;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Ongoing compliance monitoring controller providing lifecycle management of
    /// monitoring tasks linked to compliance cases.
    ///
    /// Monitoring tasks represent discrete review obligations for subjects that have
    /// progressed past initial onboarding. Each task has its own state machine,
    /// evidence requirements, due date, and auditable event trail.
    ///
    /// All endpoints are fail-closed: missing or invalid inputs return explicit errors
    /// rather than optimistic success. Unauthenticated requests return 401.
    /// Resource-not-found returns 404.
    ///
    /// Base route: <c>/api/v1/ongoing-monitoring</c>
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/ongoing-monitoring")]
    [Produces("application/json")]
    public class OngoingMonitoringController : ControllerBase
    {
        private readonly IOngoingMonitoringService _service;
        private readonly ILogger<OngoingMonitoringController> _logger;

        /// <summary>
        /// Initialises a new instance of <see cref="OngoingMonitoringController"/>.
        /// </summary>
        public OngoingMonitoringController(
            IOngoingMonitoringService service,
            ILogger<OngoingMonitoringController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ── Create ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new ongoing monitoring task for a subject linked to a compliance case.
        /// Requires <c>CaseId</c>, <c>IssuerId</c>, <c>SubjectId</c>, and a future <c>DueAt</c>.
        /// The initial task status is automatically derived from the <c>DueAt</c> proximity.
        /// </summary>
        /// <param name="request">Monitoring task creation parameters.</param>
        [HttpPost]
        [ProducesResponseType(typeof(CreateMonitoringTaskResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CreateMonitoringTaskResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateTask([FromBody] CreateMonitoringTaskRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "CreateMonitoringTask. CaseId={CaseId} Reason={Reason} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(request.CaseId),
                request.Reason,
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.CreateTaskAsync(request, actorId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── List ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a paginated, filtered list of monitoring tasks.
        /// Filters: <c>issuerId</c>, <c>subjectId</c>, <c>caseId</c>, <c>status</c>,
        /// <c>reason</c>, <c>severity</c>, <c>overdueOnly</c>.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ListMonitoringTasksResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ListTasks([FromQuery] ListMonitoringTasksRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "ListMonitoringTasks. IssuerId={IssuerId} Status={Status} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(request.IssuerId ?? string.Empty),
                request.Status?.ToString() ?? "any",
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.ListTasksAsync(request, actorId);
            return Ok(result);
        }

        // ── Get single ─────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves a single monitoring task by its unique identifier.
        /// </summary>
        /// <param name="taskId">Monitoring task identifier.</param>
        [HttpGet("{taskId}")]
        [ProducesResponseType(typeof(GetMonitoringTaskResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetMonitoringTaskResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetTask(string taskId)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "GetMonitoringTask. TaskId={TaskId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(taskId),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.GetTaskAsync(taskId, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Start Reassessment ─────────────────────────────────────────────────

        /// <summary>
        /// Starts a reassessment on the specified monitoring task.
        /// Transitions the task status to <c>InProgress</c>.
        /// Fails if the task is already in a terminal state (Resolved, Suspended, Restricted)
        /// or if a reassessment is already in progress.
        /// </summary>
        /// <param name="taskId">Monitoring task identifier.</param>
        /// <param name="request">Reassessment start parameters (optional notes, assignee, evidence list).</param>
        [HttpPost("{taskId}/start-reassessment")]
        [ProducesResponseType(typeof(StartReassessmentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(StartReassessmentResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(StartReassessmentResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> StartReassessment(string taskId, [FromBody] StartReassessmentRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "StartReassessment. TaskId={TaskId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(taskId),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.StartReassessmentAsync(taskId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Defer ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Defers a monitoring task to a future date with a recorded rationale.
        /// The task must not be in a terminal state.
        /// <c>DeferUntil</c> must be in the future and <c>Rationale</c> must be non-empty.
        /// </summary>
        /// <param name="taskId">Monitoring task identifier.</param>
        /// <param name="request">Deferral parameters (DeferUntil date and rationale).</param>
        [HttpPost("{taskId}/defer")]
        [ProducesResponseType(typeof(DeferMonitoringTaskResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(DeferMonitoringTaskResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(DeferMonitoringTaskResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeferTask(string taskId, [FromBody] DeferMonitoringTaskRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "DeferMonitoringTask. TaskId={TaskId} DeferUntil={DeferUntil} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(taskId),
                request.DeferUntil.ToString("O"),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.DeferTaskAsync(taskId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Escalate ───────────────────────────────────────────────────────────

        /// <summary>
        /// Escalates a monitoring task for senior compliance review.
        /// Can optionally raise the task severity.
        /// <c>EscalationReason</c> must be non-empty.
        /// Fails if the task is already in a terminal state.
        /// </summary>
        /// <param name="taskId">Monitoring task identifier.</param>
        /// <param name="request">Escalation parameters (reason and optional severity override).</param>
        [HttpPost("{taskId}/escalate")]
        [ProducesResponseType(typeof(EscalateMonitoringTaskResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(EscalateMonitoringTaskResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(EscalateMonitoringTaskResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> EscalateTask(string taskId, [FromBody] EscalateMonitoringTaskRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "EscalateMonitoringTask. TaskId={TaskId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(taskId),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.EscalateTaskAsync(taskId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Close ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Closes a monitoring task with an auditable resolution outcome.
        /// <c>ResolutionNotes</c> must be non-empty.
        /// Terminal tasks (already Resolved, Suspended, or Restricted) cannot be closed again.
        /// Closing with <c>SubjectSuspended</c> or <c>SubjectRestricted</c> transitions the task
        /// to the corresponding elevated-risk terminal state and emits a webhook event.
        /// </summary>
        /// <param name="taskId">Monitoring task identifier.</param>
        /// <param name="request">Close parameters (resolution outcome and notes).</param>
        [HttpPost("{taskId}/close")]
        [ProducesResponseType(typeof(CloseMonitoringTaskResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CloseMonitoringTaskResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(CloseMonitoringTaskResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CloseTask(string taskId, [FromBody] CloseMonitoringTaskRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "CloseMonitoringTask. TaskId={TaskId} Resolution={Resolution} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(taskId),
                request.Resolution,
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.CloseTaskAsync(taskId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Due-date check ─────────────────────────────────────────────────────

        /// <summary>
        /// Triggers a due-date check across all open monitoring tasks.
        /// Tasks whose <c>DueAt</c> is past are advanced to <c>Overdue</c>.
        /// Tasks within the 7-day lead-time window are advanced to <c>DueSoon</c>.
        /// Deferred tasks whose deferral period has elapsed are re-evaluated.
        /// Intended for scheduled invocation (e.g., from a cron job or background task).
        /// Returns the number of tasks whose status was updated.
        /// </summary>
        [HttpPost("due-date-check")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RunDueDateCheck()
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "RunMonitoringDueDateCheck. Actor={Actor}",
                LoggingHelper.SanitizeLogInput(actorId));

            var count = await _service.RunDueDateCheckAsync();
            return Ok(new { tasksUpdated = count });
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private string GetActorId() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "anonymous";
    }
}
