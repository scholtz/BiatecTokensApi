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
    [Authorize]
    [ApiController]
    [Route("api/v1/operator-notifications")]
    [Produces("application/json")]
    public class OperatorNotificationCenterController : ControllerBase
    {
        private readonly IOperatorNotificationCenterService _service;
        private readonly INotificationPreferenceService _preferenceService;
        private readonly ILogger<OperatorNotificationCenterController> _logger;

        /// <summary>Initializes the controller.</summary>
        public OperatorNotificationCenterController(
            IOperatorNotificationCenterService service,
            INotificationPreferenceService preferenceService,
            ILogger<OperatorNotificationCenterController> logger)
        {
            _service = service;
            _preferenceService = preferenceService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves the operator's notification inbox with per-operator lifecycle state,
        /// escalation metadata, and role-aware filtering.
        /// </summary>
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
            [FromQuery] OperatorRole? role = null,
            [FromQuery] NotificationWorkflowArea? workflowArea = null,
            [FromQuery] bool? agedOnly = null,
            [FromQuery] DateTimeOffset? fromDate = null,
            [FromQuery] DateTimeOffset? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var operatorId = GetOperatorId();

            _logger.LogInformation(
                "OperatorNotificationCenter.GetNotifications Operator={OperatorId} Page={Page} PageSize={PageSize}",
                LoggingHelper.SanitizeLogInput(operatorId), page, pageSize);

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
                Role = role,
                WorkflowArea = workflowArea,
                AgedOnly = agedOnly,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = Math.Min(pageSize, 100)
            };

            return Ok(await _service.GetNotificationsAsync(request, operatorId));
        }

        /// <summary>
        /// Returns the unread notification count for badge rendering.
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
        /// </summary>
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
                LoggingHelper.SanitizeLogInput(operatorId), request.NotificationIds.Count);

            return Ok(await _service.MarkAsReadAsync(request, operatorId));
        }

        /// <summary>
        /// Acknowledges one or more notifications, recording an optional note as audit evidence.
        /// </summary>
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
                LoggingHelper.SanitizeLogInput(operatorId), request.NotificationIds.Count);

            return Ok(await _service.AcknowledgeAsync(request, operatorId));
        }

        /// <summary>
        /// Dismisses one or more notifications from the operator's active queue.
        /// </summary>
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
                LoggingHelper.SanitizeLogInput(operatorId), request.NotificationIds.Count);

            return Ok(await _service.DismissAsync(request, operatorId));
        }

        /// <summary>
        /// Resolves one or more notifications, marking the underlying workflow item as complete.
        /// Resolved notifications are retained for audit but excluded from the active queue by default.
        /// </summary>
        [HttpPost("resolve")]
        [ProducesResponseType(typeof(NotificationLifecycleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Resolve([FromBody] ResolveNotificationsRequest? request = null)
        {
            var operatorId = GetOperatorId();
            request ??= new ResolveNotificationsRequest();

            _logger.LogInformation(
                "OperatorNotificationCenter.Resolve Operator={OperatorId} IdsCount={Count}",
                LoggingHelper.SanitizeLogInput(operatorId), request.NotificationIds.Count);

            return Ok(await _service.ResolveAsync(request, operatorId));
        }

        /// <summary>
        /// Reopens one or more previously resolved or dismissed notifications.
        /// </summary>
        [HttpPost("reopen")]
        [ProducesResponseType(typeof(NotificationLifecycleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Reopen([FromBody] ReopenNotificationsRequest? request = null)
        {
            var operatorId = GetOperatorId();
            request ??= new ReopenNotificationsRequest();

            _logger.LogInformation(
                "OperatorNotificationCenter.Reopen Operator={OperatorId} IdsCount={Count}",
                LoggingHelper.SanitizeLogInput(operatorId), request.NotificationIds.Count);

            return Ok(await _service.ReopenAsync(request, operatorId));
        }

        /// <summary>
        /// Returns a digest-grouped summary of notifications aggregated by workflow area.
        /// </summary>
        [HttpGet("digest")]
        [ProducesResponseType(typeof(NotificationDigestResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDigestSummary(
            [FromQuery] NotificationWorkflowArea? workflowArea = null,
            [FromQuery] OperatorRole? role = null,
            [FromQuery] DateTimeOffset? fromDate = null,
            [FromQuery] DateTimeOffset? toDate = null,
            [FromQuery] bool? agedOnly = null)
        {
            var operatorId = GetOperatorId();

            _logger.LogInformation(
                "OperatorNotificationCenter.GetDigestSummary Operator={OperatorId} WorkflowArea={WorkflowArea} Role={Role}",
                LoggingHelper.SanitizeLogInput(operatorId), workflowArea, role);

            var request = new NotificationDigestRequest
            {
                WorkflowArea = workflowArea,
                Role = role,
                FromDate = fromDate,
                ToDate = toDate,
                AgedOnly = agedOnly
            };

            return Ok(await _service.GetDigestSummaryAsync(request, operatorId));
        }

        /// <summary>
        /// Returns the authenticated operator's notification preferences.
        /// A default preference record is returned when no explicit configuration exists.
        /// </summary>
        [HttpGet("preferences")]
        [ProducesResponseType(typeof(NotificationPreferenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPreferences()
        {
            var operatorId = GetOperatorId();

            _logger.LogInformation(
                "OperatorNotificationCenter.GetPreferences Operator={OperatorId}",
                LoggingHelper.SanitizeLogInput(operatorId));

            return Ok(await _preferenceService.GetPreferencesAsync(operatorId));
        }

        /// <summary>
        /// Updates the authenticated operator's notification preferences.
        /// Only non-null fields in the request body are applied; omitted fields retain their current values.
        /// Every change is appended to the preference audit trail.
        /// </summary>
        [HttpPut("preferences")]
        [ProducesResponseType(typeof(NotificationPreferenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdatePreferences(
            [FromBody] UpdateNotificationPreferenceRequest? request = null)
        {
            var operatorId = GetOperatorId();
            request ??= new UpdateNotificationPreferenceRequest();

            _logger.LogInformation(
                "OperatorNotificationCenter.UpdatePreferences Operator={OperatorId}",
                LoggingHelper.SanitizeLogInput(operatorId));

            return Ok(await _preferenceService.UpdatePreferencesAsync(request, operatorId));
        }

        private string GetOperatorId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "anonymous";
    }
}
