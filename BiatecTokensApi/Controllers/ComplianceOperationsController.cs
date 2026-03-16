using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ComplianceOperations;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Compliance operations orchestration API.
    ///
    /// Provides a unified SLA-aware queue management and workflow-handoff surface for the
    /// enterprise compliance operations cockpit. Aggregates signals from scheduled reporting,
    /// approval workflows, compliance case management, and ongoing monitoring into a single
    /// prioritised queue with fail-closed risk semantics.
    ///
    /// All endpoints require authentication.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance-operations")]
    [Produces("application/json")]
    public class ComplianceOperationsController : ControllerBase
    {
        private readonly IComplianceOperationsService _service;
        private readonly ILogger<ComplianceOperationsController> _logger;

        /// <summary>
        /// Initialises a new instance of <see cref="ComplianceOperationsController"/>.
        /// </summary>
        public ComplianceOperationsController(
            IComplianceOperationsService service,
            ILogger<ComplianceOperationsController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ── Overview ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a cross-workflow compliance operations overview.
        ///
        /// Aggregates item counts by SLA status and workflow type, per-role summaries,
        /// an overall health indicator (Healthy / AtRisk / Critical), and a plain-language
        /// health summary message. Designed to power cockpit header tiles.
        /// </summary>
        /// <returns>Aggregated compliance operations overview with role summaries and health status.</returns>
        [HttpGet("overview")]
        [ProducesResponseType(typeof(ComplianceOperationsOverviewResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOverview()
        {
            var actorId       = GetActor();
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetOverview called. Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.GetOverviewAsync(actorId, correlationId);
            return response.Success ? Ok(response) : StatusCode(500, response);
        }

        // ── Queue ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a prioritised compliance operations queue with optional role, workflow-type,
        /// SLA-status, and fail-closed filters.
        ///
        /// Items are sorted by priority score descending (blocked &gt; overdue &gt; due-soon &gt; on-track).
        /// Each item contains enough information for deep links, plain-language explanations,
        /// owner routing, and SLA visualisation without further API calls.
        /// </summary>
        /// <param name="request">Filter and pagination parameters.</param>
        /// <returns>Prioritised list of compliance operations queue items.</returns>
        [HttpPost("queue")]
        [ProducesResponseType(typeof(ComplianceOpsQueueResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetQueue([FromBody] ComplianceOpsQueueRequest request)
        {
            var actorId       = GetActor();
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetQueue called. Role={Role} WorkflowType={WorkflowType} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(request?.Role?.ToString() ?? "All"),
                LoggingHelper.SanitizeLogInput(request?.WorkflowType?.ToString() ?? "All"),
                LoggingHelper.SanitizeLogInput(actorId));

            var response = await _service.GetQueueAsync(request ?? new ComplianceOpsQueueRequest(), actorId, correlationId);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        // ── Upsert (internal / service-to-service) ─────────────────────────────

        /// <summary>
        /// Registers or updates a compliance operations queue item.
        ///
        /// Intended for service-to-service calls and integration scenarios where a source
        /// workflow (e.g. a scheduled report run blocked due to missing evidence) needs to
        /// surface a work item in the operations cockpit. Emits webhook events on SLA state
        /// transitions to Overdue or Blocked.
        /// </summary>
        /// <param name="item">Queue item to create or update.</param>
        /// <returns>204 No Content on success.</returns>
        [HttpPut("queue/item")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpsertQueueItem([FromBody] ComplianceOpsQueueItem item)
        {
            if (item == null)
                return BadRequest(new ApiErrorResponse { ErrorCode = "MISSING_REQUIRED_FIELD", ErrorMessage = "Queue item is required." });

            if (string.IsNullOrWhiteSpace(item.Title))
                return BadRequest(new ApiErrorResponse { ErrorCode = "MISSING_REQUIRED_FIELD", ErrorMessage = "Title is required." });

            if (string.IsNullOrWhiteSpace(item.SourceId))
                return BadRequest(new ApiErrorResponse { ErrorCode = "MISSING_REQUIRED_FIELD", ErrorMessage = "SourceId is required." });

            var actorId       = GetActor();
            var correlationId = GetCorrelationId();

            await _service.UpsertQueueItemAsync(item, actorId, correlationId);
            return NoContent();
        }

        // ── Resolve ────────────────────────────────────────────────────────────

        /// <summary>
        /// Marks a compliance operations queue item as resolved and removes it from the active queue.
        ///
        /// Emits a <c>ComplianceOpsItemResolved</c> webhook event so downstream subscribers
        /// can update their views. Returns 404 if the item is not found in the active queue.
        /// </summary>
        /// <param name="itemId">Stable identifier of the queue item to resolve.</param>
        /// <returns>204 No Content on success; 404 if the item was not found.</returns>
        [HttpDelete("queue/item/{itemId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ResolveQueueItem([FromRoute] string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return BadRequest(new ApiErrorResponse { ErrorCode = "MISSING_REQUIRED_FIELD", ErrorMessage = "ItemId is required." });

            var actorId       = GetActor();
            var correlationId = GetCorrelationId();

            var removed = await _service.ResolveQueueItemAsync(itemId, actorId, correlationId);
            return removed ? NoContent() : NotFound();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string GetActor() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "anonymous";

        private string GetCorrelationId() =>
            HttpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var v) && v.Count > 0
                ? v.ToString()
                : Guid.NewGuid().ToString();
    }
}
