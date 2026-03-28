using BiatecTokensApi.Models.OperatorNotification;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Manages per-operator notification lifecycle state on top of the compliance-event backbone.
    /// Tracks read, acknowledged, and dismissed states so operators can manage their own inbox
    /// without affecting the shared compliance-event record.
    /// </summary>
    /// <remarks>
    /// The notification center is intentionally operator-scoped: lifecycle state (read/acknowledged/dismissed)
    /// is private to each operator and does not modify the underlying compliance event.
    /// All state changes are timestamped to support audit evidence of operator awareness.
    /// </remarks>
    public interface IOperatorNotificationCenterService
    {
        /// <summary>
        /// Retrieves a paginated, filtered list of notifications for the authenticated operator,
        /// enriched with per-operator lifecycle state.
        /// </summary>
        /// <param name="request">Notification filter and pagination options.</param>
        /// <param name="operatorId">Authenticated operator requesting their notification inbox.</param>
        /// <returns>Paginated notification list plus inbox summary counts.</returns>
        Task<OperatorNotificationListResponse> GetNotificationsAsync(
            OperatorNotificationQueryRequest request,
            string operatorId);

        /// <summary>
        /// Marks one or more notifications as read for the authenticated operator.
        /// When <see cref="MarkNotificationsReadRequest.NotificationIds"/> is empty,
        /// all Unread notifications in scope are marked as read.
        /// </summary>
        /// <param name="request">IDs to mark as read, plus optional scoping filter.</param>
        /// <param name="operatorId">Authenticated operator performing the action.</param>
        /// <returns>Lifecycle action result with affected count and audit timestamp.</returns>
        Task<NotificationLifecycleResponse> MarkAsReadAsync(
            MarkNotificationsReadRequest request,
            string operatorId);

        /// <summary>
        /// Acknowledges one or more notifications for the authenticated operator,
        /// recording an optional operator note as audit evidence.
        /// When <see cref="AcknowledgeNotificationsRequest.NotificationIds"/> is empty,
        /// all Unread or Read notifications in scope are acknowledged.
        /// </summary>
        /// <param name="request">IDs to acknowledge, optional note, and optional scoping filter.</param>
        /// <param name="operatorId">Authenticated operator performing the action.</param>
        /// <returns>Lifecycle action result with affected count and audit timestamp.</returns>
        Task<NotificationLifecycleResponse> AcknowledgeAsync(
            AcknowledgeNotificationsRequest request,
            string operatorId);

        /// <summary>
        /// Dismisses one or more notifications from the operator's active queue.
        /// Dismissed notifications are retained but excluded from active views by default.
        /// When <see cref="DismissNotificationsRequest.NotificationIds"/> is empty,
        /// all non-Dismissed notifications in scope are dismissed.
        /// </summary>
        /// <param name="request">IDs to dismiss, optional note, and optional scoping filter.</param>
        /// <param name="operatorId">Authenticated operator performing the action.</param>
        /// <returns>Lifecycle action result with affected count and audit timestamp.</returns>
        Task<NotificationLifecycleResponse> DismissAsync(
            DismissNotificationsRequest request,
            string operatorId);

        /// <summary>
        /// Retrieves the unread notification count for badge rendering.
        /// Optimised for lightweight polling by notification-center UIs.
        /// </summary>
        /// <param name="operatorId">Authenticated operator whose badge count is requested.</param>
        /// <returns>Unread count with critical sub-count and evaluation timestamp.</returns>
        Task<NotificationUnreadCountResponse> GetUnreadCountAsync(string operatorId);
    }
}
