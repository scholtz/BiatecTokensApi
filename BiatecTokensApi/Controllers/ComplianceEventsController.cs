using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceEvents;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Authenticated API exposing the canonical compliance-event backbone used by operator timelines,
    /// audit-linked workflows, and future downstream webhook consumers.
    /// </summary>
    /// <remarks>
    /// The response is intentionally fail-closed and typed:
    /// <list type="bullet">
    ///   <item><description>Freshness distinguishes current proof, awaiting provider callbacks, partial evidence, stale evidence, unavailable, and not-configured states.</description></item>
    ///   <item><description>Delivery status distinguishes not attempted, waiting, sent, failed, skipped, and not-configured conditions without success-shaped ambiguity.</description></item>
    ///   <item><description>Optional <c>headRef</c> evaluation returns a current release-readiness snapshot for operator workspaces.</description></item>
    /// </list>
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance-events")]
    [Produces("application/json")]
    public class ComplianceEventsController : ControllerBase
    {
        private readonly IComplianceEventBackboneService _service;
        private readonly ILogger<ComplianceEventsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceEventsController"/> class.
        /// </summary>
        public ComplianceEventsController(
            IComplianceEventBackboneService service,
            ILogger<ComplianceEventsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves recent compliance events with filtering and pagination suitable for operator workspaces.
        /// </summary>
        /// <param name="caseId">Optional case ID filter.</param>
        /// <param name="subjectId">Optional subject or investor filter.</param>
        /// <param name="entityId">Optional entity identifier filter.</param>
        /// <param name="headRef">Optional release head ref for current readiness evaluation.</param>
        /// <param name="entityKind">Optional entity-kind filter.</param>
        /// <param name="eventType">Optional event-type filter.</param>
        /// <param name="severity">Optional severity filter.</param>
        /// <param name="source">Optional source filter.</param>
        /// <param name="freshness">Optional freshness filter.</param>
        /// <param name="deliveryStatus">Optional delivery-status filter.</param>
        /// <param name="page">Page number (default 1).</param>
        /// <param name="pageSize">Page size (default 50, max 100).</param>
        [HttpGet]
        [ProducesResponseType(typeof(ComplianceEventListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetEvents(
            [FromQuery] string? caseId = null,
            [FromQuery] string? subjectId = null,
            [FromQuery] string? entityId = null,
            [FromQuery] string? headRef = null,
            [FromQuery] ComplianceEventEntityKind? entityKind = null,
            [FromQuery] ComplianceEventType? eventType = null,
            [FromQuery] ComplianceEventSeverity? severity = null,
            [FromQuery] ComplianceEventSource? source = null,
            [FromQuery] ComplianceEventFreshness? freshness = null,
            [FromQuery] ComplianceEventDeliveryStatus? deliveryStatus = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var actorId = GetActorId();
            var request = new ComplianceEventQueryRequest
            {
                CaseId = caseId,
                SubjectId = subjectId,
                EntityId = entityId,
                HeadRef = headRef,
                EntityKind = entityKind,
                EventType = eventType,
                Severity = severity,
                Source = source,
                Freshness = freshness,
                DeliveryStatus = deliveryStatus,
                Page = page,
                PageSize = Math.Min(pageSize, 100)
            };

            _logger.LogInformation(
                "ComplianceEvents.GetEvents Actor={ActorId} CaseId={CaseId} SubjectId={SubjectId} HeadRef={HeadRef} Page={Page} PageSize={PageSize}",
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(subjectId),
                LoggingHelper.SanitizeLogInput(headRef),
                request.Page,
                request.PageSize);

            return Ok(await _service.GetEventsAsync(request, actorId));
        }

        /// <summary>
        /// Retrieves the operator timeline for a specific case ID.
        /// </summary>
        /// <param name="caseId">Case identifier across onboarding, compliance, and sign-off domains.</param>
        /// <param name="headRef">Optional release head ref to include current release-readiness state.</param>
        /// <param name="page">Page number (default 1).</param>
        /// <param name="pageSize">Page size (default 50, max 100).</param>
        [HttpGet("cases/{caseId}/timeline")]
        [ProducesResponseType(typeof(ComplianceEventListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCaseTimeline(
            [FromRoute] string caseId,
            [FromQuery] string? headRef = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "ComplianceEvents.GetCaseTimeline Actor={ActorId} CaseId={CaseId} HeadRef={HeadRef}",
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(headRef));

            return Ok(await _service.GetEventsAsync(new ComplianceEventQueryRequest
            {
                CaseId = caseId,
                HeadRef = headRef,
                Page = page,
                PageSize = Math.Min(pageSize, 100)
            }, actorId));
        }

        /// <summary>
        /// Retrieves a lightweight operator-dashboard queue summary with counts per category.
        /// </summary>
        /// <remarks>
        /// Returns counts broken down into five operator-facing categories:
        /// <list type="bullet">
        ///   <item><description><b>Blocked</b> — events with Critical severity representing hard blockers or failures.</description></item>
        ///   <item><description><b>ActionNeeded</b> — events with Warning severity requiring operator attention.</description></item>
        ///   <item><description><b>WaitingOnProvider</b> — events with AwaitingProviderCallback freshness indicating upstream delay.</description></item>
        ///   <item><description><b>Stale</b> — events with Stale freshness whose evidence is no longer authoritative.</description></item>
        ///   <item><description><b>Informational</b> — events with Informational severity that require no immediate action.</description></item>
        /// </list>
        /// Counts are independent so a single event can contribute to multiple categories.
        /// The same filter parameters accepted by <c>GET /api/v1/compliance-events</c> can be used to scope the summary.
        /// </remarks>
        /// <param name="caseId">Optional case ID filter.</param>
        /// <param name="subjectId">Optional subject or investor filter.</param>
        /// <param name="entityId">Optional entity identifier filter.</param>
        /// <param name="headRef">Optional release head ref for current readiness evaluation.</param>
        /// <param name="entityKind">Optional entity-kind filter.</param>
        /// <param name="eventType">Optional event-type filter.</param>
        /// <param name="severity">Optional severity filter.</param>
        /// <param name="source">Optional source filter.</param>
        /// <param name="freshness">Optional freshness filter.</param>
        /// <param name="deliveryStatus">Optional delivery-status filter.</param>
        [HttpGet("queue-summary")]
        [ProducesResponseType(typeof(ComplianceEventQueueSummaryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetQueueSummary(
            [FromQuery] string? caseId = null,
            [FromQuery] string? subjectId = null,
            [FromQuery] string? entityId = null,
            [FromQuery] string? headRef = null,
            [FromQuery] ComplianceEventEntityKind? entityKind = null,
            [FromQuery] ComplianceEventType? eventType = null,
            [FromQuery] ComplianceEventSeverity? severity = null,
            [FromQuery] ComplianceEventSource? source = null,
            [FromQuery] ComplianceEventFreshness? freshness = null,
            [FromQuery] ComplianceEventDeliveryStatus? deliveryStatus = null)
        {
            var actorId = GetActorId();
            var request = new ComplianceEventQueryRequest
            {
                CaseId = caseId,
                SubjectId = subjectId,
                EntityId = entityId,
                HeadRef = headRef,
                EntityKind = entityKind,
                EventType = eventType,
                Severity = severity,
                Source = source,
                Freshness = freshness,
                DeliveryStatus = deliveryStatus,
                Page = 1,
                PageSize = 100
            };

            _logger.LogInformation(
                "ComplianceEvents.GetQueueSummary Actor={ActorId} CaseId={CaseId} SubjectId={SubjectId} HeadRef={HeadRef}",
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(subjectId),
                LoggingHelper.SanitizeLogInput(headRef));

            return Ok(await _service.GetQueueSummaryAsync(request, actorId));
        }

        private string GetActorId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "anonymous";
    }
}
