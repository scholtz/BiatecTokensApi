using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ScheduledReporting;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides APIs for scheduled compliance report templates, report run management,
    /// schedule definitions, delivery tracking, and approval workflows.
    ///
    /// This controller supports the enterprise reporting command center by exposing:
    ///  - Template CRUD: create, read, update, archive, and list reporting templates
    ///  - Run management: trigger manual runs, inspect run history, and retrieve run details
    ///  - Schedule management: define recurring execution schedules (monthly, quarterly, etc.)
    ///  - Approval gates: reviewer sign-off and formal approval flows
    ///
    /// Evidence freshness is evaluated fail-closed: if required evidence is missing or stale,
    /// runs are created with Blocked status and actionable blocker details for remediation.
    /// Delivery outcome records are stored per destination without embedding secret credentials.
    /// Webhook events are emitted for significant run lifecycle transitions.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/scheduled-reporting")]
    [Produces("application/json")]
    public class ScheduledReportingController : ControllerBase
    {
        private readonly IScheduledReportingService _service;
        private readonly ILogger<ScheduledReportingController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ScheduledReportingController"/>.
        /// </summary>
        public ScheduledReportingController(
            IScheduledReportingService service,
            ILogger<ScheduledReportingController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ── Template CRUD ──────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new reporting template with audience type, cadence, evidence domains,
        /// delivery preferences, and review/approval requirements.
        /// </summary>
        /// <param name="request">Template creation parameters.</param>
        /// <returns>The created template record.</returns>
        [HttpPost("templates")]
        [ProducesResponseType(typeof(ReportingTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateTemplate([FromBody] CreateReportingTemplateRequest request)
        {
            var actor = GetActor();
            _logger.LogInformation(
                "CreateTemplate. Name={Name} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(request.Name),
                LoggingHelper.SanitizeLogInput(actor));
            try
            {
                var result = await _service.CreateTemplateAsync(request, actor);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateTemplate failed. Actor={Actor}",
                    LoggingHelper.SanitizeLogInput(actor));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Retrieves a reporting template by its ID.
        /// </summary>
        /// <param name="templateId">Template identifier.</param>
        /// <returns>The template record.</returns>
        [HttpGet("templates/{templateId}")]
        [ProducesResponseType(typeof(ReportingTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTemplate(string templateId)
        {
            _logger.LogInformation("GetTemplate. TemplateId={TemplateId}",
                LoggingHelper.SanitizeLogInput(templateId));
            try
            {
                var result = await _service.GetTemplateAsync(templateId);
                if (!result.Success && result.ErrorCode == "TEMPLATE_NOT_FOUND")
                    return NotFound(new ApiErrorResponse { ErrorMessage = result.ErrorMessage ?? "Not found." });
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTemplate failed. TemplateId={TemplateId}",
                    LoggingHelper.SanitizeLogInput(templateId));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Updates an existing reporting template. Only provided fields are modified.
        /// </summary>
        /// <param name="templateId">Template identifier.</param>
        /// <param name="request">Fields to update.</param>
        /// <returns>The updated template record.</returns>
        [HttpPut("templates/{templateId}")]
        [ProducesResponseType(typeof(ReportingTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateTemplate(
            string templateId, [FromBody] UpdateReportingTemplateRequest request)
        {
            var actor = GetActor();
            _logger.LogInformation(
                "UpdateTemplate. TemplateId={TemplateId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(templateId),
                LoggingHelper.SanitizeLogInput(actor));
            try
            {
                var result = await _service.UpdateTemplateAsync(templateId, request, actor);
                if (!result.Success && result.ErrorCode == "TEMPLATE_NOT_FOUND")
                    return NotFound(new ApiErrorResponse { ErrorMessage = result.ErrorMessage ?? "Not found." });
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateTemplate failed. TemplateId={TemplateId}",
                    LoggingHelper.SanitizeLogInput(templateId));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Archives a reporting template. Archived templates are retained for audit purposes
        /// but will no longer trigger scheduled runs.
        /// </summary>
        /// <param name="templateId">Template identifier.</param>
        /// <returns>The archived template record.</returns>
        [HttpPost("templates/{templateId}/archive")]
        [ProducesResponseType(typeof(ReportingTemplateResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ArchiveTemplate(string templateId)
        {
            var actor = GetActor();
            _logger.LogInformation(
                "ArchiveTemplate. TemplateId={TemplateId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(templateId),
                LoggingHelper.SanitizeLogInput(actor));
            try
            {
                var result = await _service.ArchiveTemplateAsync(templateId, actor);
                if (!result.Success && result.ErrorCode == "TEMPLATE_NOT_FOUND")
                    return NotFound(new ApiErrorResponse { ErrorMessage = result.ErrorMessage ?? "Not found." });
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ArchiveTemplate failed. TemplateId={TemplateId}",
                    LoggingHelper.SanitizeLogInput(templateId));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Lists reporting templates with optional filters and pagination.
        /// </summary>
        /// <param name="includeArchived">Whether to include archived templates (default: false).</param>
        /// <param name="audienceFilter">Optional filter by audience type.</param>
        /// <param name="page">Page number (default: 1).</param>
        /// <param name="pageSize">Page size (default: 20, max: 100).</param>
        /// <returns>Paginated list of template records.</returns>
        [HttpGet("templates")]
        [ProducesResponseType(typeof(ListReportingTemplatesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListTemplates(
            [FromQuery] bool includeArchived = false,
            [FromQuery] ReportingAudienceType? audienceFilter = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            _logger.LogInformation(
                "ListTemplates. IncludeArchived={IncludeArchived} Page={Page} PageSize={PageSize}",
                includeArchived, page, pageSize);
            try
            {
                var result = await _service.ListTemplatesAsync(includeArchived, audienceFilter, page, pageSize);
                return result.Success ? Ok(result) : StatusCode(500, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ListTemplates failed.");
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        // ── Report Run operations ──────────────────────────────────────────────

        /// <summary>
        /// Manually triggers a report run for the specified template.
        /// Evidence freshness is evaluated fail-closed: if required evidence is missing or stale,
        /// the run is created with Blocked status and actionable blocker details.
        /// </summary>
        /// <param name="templateId">Template identifier.</param>
        /// <param name="request">Optional run trigger parameters.</param>
        /// <returns>The created report run record.</returns>
        [HttpPost("templates/{templateId}/runs")]
        [ProducesResponseType(typeof(ReportRunResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TriggerRun(
            string templateId, [FromBody] TriggerReportRunRequest? request = null)
        {
            var actor = GetActor();
            _logger.LogInformation(
                "TriggerRun. TemplateId={TemplateId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(templateId),
                LoggingHelper.SanitizeLogInput(actor));
            try
            {
                var result = await _service.TriggerRunAsync(
                    templateId, request ?? new TriggerReportRunRequest(), actor);
                if (!result.Success && result.ErrorCode == "TEMPLATE_NOT_FOUND")
                    return NotFound(new ApiErrorResponse { ErrorMessage = result.ErrorMessage ?? "Not found." });
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TriggerRun failed. TemplateId={TemplateId}",
                    LoggingHelper.SanitizeLogInput(templateId));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Retrieves a specific report run by its ID.
        /// </summary>
        /// <param name="runId">Report run identifier.</param>
        /// <returns>The report run record with evidence lineage, blockers, delivery outcomes,
        /// and comparison to prior run.</returns>
        [HttpGet("runs/{runId}")]
        [ProducesResponseType(typeof(ReportRunResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRun(string runId)
        {
            _logger.LogInformation("GetRun. RunId={RunId}", LoggingHelper.SanitizeLogInput(runId));
            try
            {
                var result = await _service.GetRunAsync(runId);
                if (!result.Success && result.ErrorCode == "RUN_NOT_FOUND")
                    return NotFound(new ApiErrorResponse { ErrorMessage = result.ErrorMessage ?? "Not found." });
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRun failed. RunId={RunId}",
                    LoggingHelper.SanitizeLogInput(runId));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Lists report runs for a template, ordered by creation date descending.
        /// </summary>
        /// <param name="templateId">Template identifier.</param>
        /// <param name="statusFilter">Optional filter by run status.</param>
        /// <param name="page">Page number (default: 1).</param>
        /// <param name="pageSize">Page size (default: 20, max: 100).</param>
        /// <returns>Paginated list of report run records.</returns>
        [HttpGet("templates/{templateId}/runs")]
        [ProducesResponseType(typeof(ListReportRunsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListRuns(
            string templateId,
            [FromQuery] ReportRunStatus? statusFilter = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            _logger.LogInformation(
                "ListRuns. TemplateId={TemplateId} Page={Page}",
                LoggingHelper.SanitizeLogInput(templateId), page);
            try
            {
                var result = await _service.ListRunsAsync(templateId, statusFilter, page, pageSize);
                if (!result.Success && result.ErrorCode == "TEMPLATE_NOT_FOUND")
                    return NotFound(new ApiErrorResponse { ErrorMessage = result.ErrorMessage ?? "Not found." });
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ListRuns failed. TemplateId={TemplateId}",
                    LoggingHelper.SanitizeLogInput(templateId));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        // ── Approval and review ────────────────────────────────────────────────

        /// <summary>
        /// Records a reviewer sign-off decision on a report run.
        /// Run must be in AwaitingReview status.
        /// If approved and no further approval is required, the run transitions to Exported.
        /// If rejected, the run transitions to Failed.
        /// </summary>
        /// <param name="runId">Report run identifier.</param>
        /// <param name="request">Review decision and notes.</param>
        /// <returns>Updated report run record.</returns>
        [HttpPost("runs/{runId}/review")]
        [ProducesResponseType(typeof(ReportRunResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReviewRun(string runId, [FromBody] ReviewReportRunRequest request)
        {
            var actor = GetActor();
            _logger.LogInformation(
                "ReviewRun. RunId={RunId} Approve={Approve} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(runId),
                request.Approve,
                LoggingHelper.SanitizeLogInput(actor));
            try
            {
                var result = await _service.ReviewRunAsync(runId, request, actor);
                if (!result.Success && result.ErrorCode == "RUN_NOT_FOUND")
                    return NotFound(new ApiErrorResponse { ErrorMessage = result.ErrorMessage ?? "Not found." });
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReviewRun failed. RunId={RunId}",
                    LoggingHelper.SanitizeLogInput(runId));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Records a formal approval decision on a reviewed report run.
        /// Run must be in AwaitingApproval status.
        /// Approved runs are exported and delivery is initiated to all configured destinations.
        /// Rejected runs transition to Failed.
        /// </summary>
        /// <param name="runId">Report run identifier.</param>
        /// <param name="request">Approval decision and notes.</param>
        /// <returns>Updated report run record with delivery outcomes.</returns>
        [HttpPost("runs/{runId}/approve")]
        [ProducesResponseType(typeof(ReportRunResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ApproveRun(string runId, [FromBody] ApproveReportRunRequest request)
        {
            var actor = GetActor();
            _logger.LogInformation(
                "ApproveRun. RunId={RunId} Approve={Approve} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(runId),
                request.Approve,
                LoggingHelper.SanitizeLogInput(actor));
            try
            {
                var result = await _service.ApproveRunAsync(runId, request, actor);
                if (!result.Success && result.ErrorCode == "RUN_NOT_FOUND")
                    return NotFound(new ApiErrorResponse { ErrorMessage = result.ErrorMessage ?? "Not found." });
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApproveRun failed. RunId={RunId}",
                    LoggingHelper.SanitizeLogInput(runId));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        // ── Schedule management ────────────────────────────────────────────────

        /// <summary>
        /// Creates or replaces the schedule definition for a reporting template.
        /// Supports monthly, quarterly, semi-annual, annual, and event-driven cadences.
        /// </summary>
        /// <param name="templateId">Template identifier.</param>
        /// <param name="request">Schedule parameters including cadence, timing, and active status.</param>
        /// <returns>The schedule definition.</returns>
        [HttpPut("templates/{templateId}/schedule")]
        [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpsertSchedule(
            string templateId, [FromBody] UpsertScheduleRequest request)
        {
            var actor = GetActor();
            _logger.LogInformation(
                "UpsertSchedule. TemplateId={TemplateId} Cadence={Cadence} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(templateId),
                request.Cadence,
                LoggingHelper.SanitizeLogInput(actor));
            try
            {
                var result = await _service.UpsertScheduleAsync(templateId, request, actor);
                if (!result.Success && result.ErrorCode == "TEMPLATE_NOT_FOUND")
                    return NotFound(new ApiErrorResponse { ErrorMessage = result.ErrorMessage ?? "Not found." });
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpsertSchedule failed. TemplateId={TemplateId}",
                    LoggingHelper.SanitizeLogInput(templateId));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Retrieves the schedule definition for a reporting template.
        /// </summary>
        /// <param name="templateId">Template identifier.</param>
        /// <returns>The schedule definition with next trigger date.</returns>
        [HttpGet("templates/{templateId}/schedule")]
        [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSchedule(string templateId)
        {
            _logger.LogInformation("GetSchedule. TemplateId={TemplateId}",
                LoggingHelper.SanitizeLogInput(templateId));
            try
            {
                var result = await _service.GetScheduleAsync(templateId);
                if (!result.Success)
                    return NotFound(new ApiErrorResponse { ErrorMessage = result.ErrorMessage ?? "Not found." });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSchedule failed. TemplateId={TemplateId}",
                    LoggingHelper.SanitizeLogInput(templateId));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        /// <summary>
        /// Deactivates the schedule for a reporting template without deleting the definition.
        /// Deactivated schedules retain their configuration and can be reactivated via UpsertSchedule.
        /// </summary>
        /// <param name="templateId">Template identifier.</param>
        /// <returns>The updated (deactivated) schedule definition.</returns>
        [HttpPost("templates/{templateId}/schedule/deactivate")]
        [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeactivateSchedule(string templateId)
        {
            var actor = GetActor();
            _logger.LogInformation(
                "DeactivateSchedule. TemplateId={TemplateId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(templateId),
                LoggingHelper.SanitizeLogInput(actor));
            try
            {
                var result = await _service.DeactivateScheduleAsync(templateId, actor);
                if (!result.Success && result.ErrorCode is "TEMPLATE_NOT_FOUND" or "SCHEDULE_NOT_FOUND")
                    return NotFound(new ApiErrorResponse { ErrorMessage = result.ErrorMessage ?? "Not found." });
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeactivateSchedule failed. TemplateId={TemplateId}",
                    LoggingHelper.SanitizeLogInput(templateId));
                return StatusCode(500, new ApiErrorResponse { ErrorMessage = "An unexpected error occurred." });
            }
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private string GetActor() =>
            User?.Identity?.Name
            ?? User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User?.FindFirst("sub")?.Value
            ?? "unknown";
    }
}
