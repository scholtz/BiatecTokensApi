using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceEvents;
using BiatecTokensApi.Models.OperatorNotification;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Authenticated API exposing the operator notification center for enterprise compliance workflows.
    /// </summary>
    /// <remarks>
    /// The notification center provides each operator with a personalised, managed inbox derived from
    /// the compliance-event backbone. Unlike the raw event feed, notifications carry per-operator
    /// lifecycle state (Unread → Read → Acknowledged → Dismissed) with full audit timestamps.
    ///
    /// Intended frontend usage:
    /// <list type="bullet">
    ///   <item><description>Poll <c>GET /unread-count</c> for badge updates.</description></item>
    ///   <item><description>Load <c>GET /</c> with <c>excludeDismissed=true</c> for the active inbox.</description></item>
    ///   <item><description>Call <c>POST /mark-read</c> when an operator opens a notification.</description></item>
    ///   <item><description>Call <c>POST /acknowledge</c> when an operator explicitly confirms awareness.</description></item>
    ///   <item><description>Call <c>POST /dismiss</c> when an operator archives a resolved item.</description></item>
    /// </list>
    ///
    /// All state changes are scoped to the authenticated operator and do not affect other operators' views.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/operator-notifications")]
    [Produces("application/json")]
    public class OperatorNotificationCenterController : ControllerBase
    {
        private readonly IOperatorNotificationCenterService _service;
        private readonly ILogger<OperatorNotificationCenterController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="OperatorNotificationCenterController"/> class.
        /// </summary>
        public OperatorNotificationCenterController(
            IOperatorNotificationCenterService service,
            ILogger<OperatorNotificationCenterController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the operator's notification inbox with per-operator lifecycle state.
        /// </summary>
        /// <param name="caseId">Optional case ID filter.</param>
        /// <param name="subjectId">Optional subject or investor filter.</param>
        /// <param name="entityId">Optional entity identifier filter.</param>
        /// <param name="headRef">Optional release head ref filter.</param>
        /// <param name="severity">Optional severity filter.</param>
        /// <param name="eventType">Optional event-type filter.</param>
        /// <param name="entityKind">Optional entity-kind filter.</param>
        /// <param name="lifecycleState">Optional lifecycle-state filter.</param>
        /// <param name="excludeDismissed">When true, dismissed notifications are excluded.</param>
        /// <param name="unreadOnly">When true, only unread notifications are returned.</param>
        /// <param name="fromDate">Optional earliest creation date filter (UTC).</param>
        /// <param name="toDate">Optional latest creation date filter (UTC).</param>
        /// <param name="page">Page number (default 1).</param>
        /// <param name="pageSize">Page size (default 50, max 100).</param>
        [HttpGet]
        [ProducesResponseType(typeof(OperatorNotificationListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] string? caseId = null,
            [FromQuery] string? subjectId = null,
            [FromQuery] string? entityId = null,
            [FromQuery] string? headRef = null,
            [FromQuery] ComplianceEventSeverity? severity = null,
            [FromQuery] ComplianceEventType? eventType = null,
            [FromQuery] ComplianceEventEntityKind? entityKind = null,
            [FromQuery] NotificationLifecycleState? lifecycleState = null,
            [FromQuery] bool? excludeDismissed = null,
            [FromQuery] bool? unreadOnly = null,
            [FromQuery] DateTimeOffset? fromDate = null,
            [FromQuery] DateTimeOffset? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var operatorId = GetOperatorId();

            _logger.LogInformation(
                "OperatorNotificationCenter.GetNotifications Operator={OperatorId} Page={Page} PageSize={PageSize} UnreadOnly={UnreadOnly}",
                LoggingHelper.SanitizeLogInput(operatorId),
                page,
                pageSize,
                unreadOnly);

            var request = new OperatorNotificationQueryRequest
            {
                CaseId = caseId,
                SubjectId = subjectId,
                EntityId = entityId,
                HeadRef = headRef,
                Severity = severity,
                EventType = eventType,
                EntityKind = entityKind,
                LifecycleState = lifecycleState,
                ExcludeDismissed = excludeDismissed,
                UnreadOnly = unreadOnly,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = Math.Min(pageSize, 100)
            };

            return Ok(await _service.GetNotificationsAsync(request, operatorId));
        }

        /// <summary>
        /// Returns the unread notification count for badge rendering.
        /// Optimised for lightweight polling by notification-center UIs.
        /// </summary>
        [HttpGet("unread-count")]
        [ProducesResponseType(typeof(NotificationUnreadCountResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetUnreadCount()
        {
            var operatorId = GetOperatorId();

            _logger.LogInformation(
                "OperatorNotificationCenter.GetUnreadCount Operator={OperatorId}",
                LoggingHelper.SanitizeLogInput(operatorId));

            return Ok(await _service.GetUnreadCountAsync(operatorId));
        }

        /// <summary>
        /// Marks one or more notifications as read for the authenticated operator.
        /// When <c>notificationIds</c> is omitted or empty, all unread notifications are marked as read.
        /// </summary>
        /// <param name="request">IDs to mark as read plus optional scoping filter.</param>
        [HttpPost("mark-read")]
        [ProducesResponseType(typeof(NotificationLifecycleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> MarkAsRead([FromBody] MarkNotificationsReadRequest? request = null)
        {
            var operatorId = GetOperatorId();
            request ??= new MarkNotificationsReadRequest();

            _logger.LogInformation(
                "OperatorNotificationCenter.MarkAsRead Operator={OperatorId} IdsCount={Count}",
                LoggingHelper.SanitizeLogInput(operatorId),
                request.NotificationIds.Count);

            return Ok(await _service.MarkAsReadAsync(request, operatorId));
        }

        /// <summary>
        /// Acknowledges one or more notifications for the authenticated operator,
        /// recording an optional note as audit evidence.
        /// When <c>notificationIds</c> is omitted or empty, all unread or read notifications are acknowledged.
        /// </summary>
        /// <param name="request">IDs to acknowledge, optional note, and optional scoping filter.</param>
        [HttpPost("acknowledge")]
        [ProducesResponseType(typeof(NotificationLifecycleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Acknowledge([FromBody] AcknowledgeNotificationsRequest? request = null)
        {
            var operatorId = GetOperatorId();
            request ??= new AcknowledgeNotificationsRequest();

            _logger.LogInformation(
                "OperatorNotificationCenter.Acknowledge Operator={OperatorId} IdsCount={Count}",
                LoggingHelper.SanitizeLogInput(operatorId),
                request.NotificationIds.Count);

            return Ok(await _service.AcknowledgeAsync(request, operatorId));
        }

        /// <summary>
        /// Dismisses one or more notifications from the operator's active queue.
        /// Dismissed notifications are retained for audit purposes but excluded from active views.
        /// When <c>notificationIds</c> is omitted or empty, all non-dismissed notifications are dismissed.
        /// </summary>
        /// <param name="request">IDs to dismiss, optional note, and optional scoping filter.</param>
        [HttpPost("dismiss")]
        [ProducesResponseType(typeof(NotificationLifecycleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Dismiss([FromBody] DismissNotificationsRequest? request = null)
        {
            var operatorId = GetOperatorId();
            request ??= new DismissNotificationsRequest();

            _logger.LogInformation(
                "OperatorNotificationCenter.Dismiss Operator={OperatorId} IdsCount={Count}",
                LoggingHelper.SanitizeLogInput(operatorId),
                request.NotificationIds.Count);

            return Ok(await _service.DismissAsync(request, operatorId));
        }

        private string GetOperatorId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "anonymous";
    }
}
