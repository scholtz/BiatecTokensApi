using BiatecTokensApi.Models.ComplianceEvents;
using BiatecTokensApi.Models.OperatorNotification;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Manages per-operator notification preferences including severity thresholds,
    /// workflow area subscriptions, digest policies, muting semantics, and
    /// fail-closed escalation behaviour for the enterprise notification center.
    /// </summary>
    public interface INotificationPreferenceService
    {
        /// <summary>
        /// Retrieves the notification preferences for the specified operator.
        /// Returns a default preference record if none has been explicitly configured.
        /// </summary>
        /// <param name="operatorId">The operator whose preferences are requested.</param>
        Task<NotificationPreferenceResponse> GetPreferencesAsync(string operatorId);

        /// <summary>
        /// Applies a partial update to the notification preferences of the specified operator.
        /// Only non-null fields in the request are modified; all other fields retain their current values.
        /// Every applied change is recorded in the preference audit trail.
        /// </summary>
        /// <param name="request">The partial update to apply.</param>
        /// <param name="operatorId">The operator whose preferences are being updated.</param>
        Task<NotificationPreferenceResponse> UpdatePreferencesAsync(
            UpdateNotificationPreferenceRequest request,
            string operatorId);

        /// <summary>
        /// Returns the effective (resolved) notification preference for the specified operator,
        /// creating and persisting a default preference if one does not yet exist.
        /// </summary>
        /// <param name="operatorId">The operator whose effective preferences are requested.</param>
        Task<NotificationPreference> GetEffectivePreferencesAsync(string operatorId);

        /// <summary>
        /// Determines whether a notification with the given severity and workflow area
        /// is allowed to surface in the operator's inbox according to their preferences.
        /// </summary>
        /// <remarks>
        /// Fail-closed guarantee: <see cref="ComplianceEventSeverity.Critical"/> notifications
        /// are always allowed regardless of preference configuration.
        /// </remarks>
        /// <param name="severity">The severity of the compliance event.</param>
        /// <param name="workflowArea">The workflow area of the compliance event.</param>
        /// <param name="preference">The operator's current preference record. Null is treated as default (allow all).</param>
        bool IsNotificationAllowed(
            ComplianceEventSeverity severity,
            NotificationWorkflowArea workflowArea,
            NotificationPreference? preference);

        /// <summary>
        /// Computes routing metadata for a notification given the operator's preferences,
        /// indicating whether it should be delivered immediately, is muted, passes the
        /// severity threshold, and what digest window applies.
        /// </summary>
        /// <param name="severity">The severity of the compliance event.</param>
        /// <param name="workflowArea">The workflow area of the compliance event.</param>
        /// <param name="preference">The operator's current preference record. Null is treated as default.</param>
        NotificationRoutingMetadata ComputeRoutingMetadata(
            ComplianceEventSeverity severity,
            NotificationWorkflowArea workflowArea,
            NotificationPreference? preference);
    }
}
