using BiatecTokensApi.Models.OperatorNotification;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Manages per-operator notification lifecycle state on top of the compliance-event backbone.
    /// Tracks read, acknowledged, dismissed, resolved, and reopened states so operators can manage
    /// their own inbox without affecting the shared compliance-event record.
    /// </summary>
    public interface IOperatorNotificationCenterService
    {
        /// <summary>
        /// Retrieves a paginated, filtered list of notifications for the authenticated operator,
        /// enriched with per-operator lifecycle state, escalation metadata, and role-aware targeting.
        /// </summary>
        Task<OperatorNotificationListResponse> GetNotificationsAsync(
            OperatorNotificationQueryRequest request,
            string operatorId);

        /// <summary>
        /// Marks one or more notifications as read for the authenticated operator.
        /// When NotificationIds is empty, all Unread notifications in scope are marked as read.
        /// </summary>
        Task<NotificationLifecycleResponse> MarkAsReadAsync(
            MarkNotificationsReadRequest request,
            string operatorId);

        /// <summary>
        /// Acknowledges one or more notifications, recording an optional note as audit evidence.
        /// When NotificationIds is empty, all Unread or Read notifications in scope are acknowledged.
        /// </summary>
        Task<NotificationLifecycleResponse> AcknowledgeAsync(
            AcknowledgeNotificationsRequest request,
            string operatorId);

        /// <summary>
        /// Dismisses one or more notifications from the operator's active queue.
        /// Dismissed notifications are retained for audit but excluded from active views by default.
        /// </summary>
        Task<NotificationLifecycleResponse> DismissAsync(
            DismissNotificationsRequest request,
            string operatorId);

        /// <summary>
        /// Retrieves the unread notification count for badge rendering.
        /// </summary>
        Task<NotificationUnreadCountResponse> GetUnreadCountAsync(string operatorId);

        /// <summary>
        /// Resolves one or more notifications, marking the underlying workflow item as complete.
        /// Resolved notifications are retained for audit but excluded from the active queue by default.
        /// </summary>
        Task<NotificationLifecycleResponse> ResolveAsync(
            ResolveNotificationsRequest request,
            string operatorId);

        /// <summary>
        /// Reopens one or more previously resolved or dismissed notifications,
        /// returning them to the active queue for further operator attention.
        /// </summary>
        Task<NotificationLifecycleResponse> ReopenAsync(
            ReopenNotificationsRequest request,
            string operatorId);

        /// <summary>
        /// Returns a digest-grouped summary of notifications aggregated by workflow area.
        /// </summary>
        Task<NotificationDigestResponse> GetDigestSummaryAsync(
            NotificationDigestRequest request,
            string operatorId);
    }
}
