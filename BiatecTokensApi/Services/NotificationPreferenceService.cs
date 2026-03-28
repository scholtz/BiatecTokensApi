using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceEvents;
using BiatecTokensApi.Models.OperatorNotification;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of <see cref="INotificationPreferenceService"/>.
    /// Manages per-operator notification preferences including severity thresholds,
    /// workflow area subscriptions, digest policies, muting, and fail-closed escalation.
    /// </summary>
    /// <remarks>
    /// In a production deployment the in-memory store would be replaced by a durable
    /// persistence layer (e.g. database, distributed cache) while preserving the same interface.
    /// </remarks>
    public class NotificationPreferenceService : INotificationPreferenceService
    {
        private readonly ILogger<NotificationPreferenceService> _logger;
        private readonly TimeProvider _timeProvider;

        // Keyed by operatorId
        private readonly Dictionary<string, NotificationPreference> _preferences = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        /// <summary>Initializes a new instance of <see cref="NotificationPreferenceService"/>.</summary>
        public NotificationPreferenceService(
            ILogger<NotificationPreferenceService> logger,
            TimeProvider? timeProvider = null)
        {
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        // ── GetPreferencesAsync ───────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<NotificationPreferenceResponse> GetPreferencesAsync(string operatorId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);

            _logger.LogInformation(
                "NotificationPreferenceService.GetPreferences OperatorId={OperatorId}",
                LoggingHelper.SanitizeLogInput(operatorId));

            lock (_lock)
            {
                var pref = _preferences.TryGetValue(operatorId, out var existing)
                    ? existing
                    : BuildDefault(operatorId);

                return Task.FromResult(new NotificationPreferenceResponse
                {
                    Success = true,
                    Preference = pref
                });
            }
        }

        // ── UpdatePreferencesAsync ────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<NotificationPreferenceResponse> UpdatePreferencesAsync(
            UpdateNotificationPreferenceRequest request,
            string operatorId)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);

            _logger.LogInformation(
                "NotificationPreferenceService.UpdatePreferences OperatorId={OperatorId}",
                LoggingHelper.SanitizeLogInput(operatorId));

            var note = request.Note;
            var now = _timeProvider.GetUtcNow();

            lock (_lock)
            {
                if (!_preferences.TryGetValue(operatorId, out var pref))
                {
                    pref = BuildDefault(operatorId);
                    pref.CreatedAt = now;
                    _preferences[operatorId] = pref;
                }

                if (request.SeverityThreshold.HasValue)
                {
                    AddAuditEntry(pref, now, operatorId, nameof(pref.SeverityThreshold),
                        pref.SeverityThreshold.ToString(), request.SeverityThreshold.Value.ToString(), note);
                    pref.SeverityThreshold = request.SeverityThreshold.Value;
                }

                if (request.DigestEnabled.HasValue)
                {
                    AddAuditEntry(pref, now, operatorId, nameof(pref.DigestEnabled),
                        pref.DigestEnabled.ToString(), request.DigestEnabled.Value.ToString(), note);
                    pref.DigestEnabled = request.DigestEnabled.Value;
                }

                if (request.DigestPolicy is not null)
                {
                    AddAuditEntry(pref, now, operatorId, nameof(pref.DigestPolicy),
                        $"Frequency={pref.DigestPolicy.Frequency}", $"Frequency={request.DigestPolicy.Frequency}", note);
                    pref.DigestPolicy = request.DigestPolicy;
                }

                if (request.EscalationEnabled.HasValue)
                {
                    AddAuditEntry(pref, now, operatorId, nameof(pref.EscalationEnabled),
                        pref.EscalationEnabled.ToString(), request.EscalationEnabled.Value.ToString(), note);
                    pref.EscalationEnabled = request.EscalationEnabled.Value;
                }

                if (request.WorkflowAreaSubscriptions is not null)
                {
                    var prev = pref.WorkflowAreaSubscriptions is null
                        ? "All"
                        : string.Join(",", pref.WorkflowAreaSubscriptions);
                    AddAuditEntry(pref, now, operatorId, nameof(pref.WorkflowAreaSubscriptions),
                        prev, string.Join(",", request.WorkflowAreaSubscriptions), note);
                    pref.WorkflowAreaSubscriptions = request.WorkflowAreaSubscriptions;
                }

                if (request.MutedWorkflowAreas is not null)
                {
                    var prev = pref.MutedWorkflowAreas is null
                        ? "None"
                        : string.Join(",", pref.MutedWorkflowAreas);
                    AddAuditEntry(pref, now, operatorId, nameof(pref.MutedWorkflowAreas),
                        prev, string.Join(",", request.MutedWorkflowAreas), note);
                    pref.MutedWorkflowAreas = request.MutedWorkflowAreas;
                }

                if (request.AllowBlockerSuppression.HasValue)
                {
                    AddAuditEntry(pref, now, operatorId, nameof(pref.AllowBlockerSuppression),
                        pref.AllowBlockerSuppression.ToString(), request.AllowBlockerSuppression.Value.ToString(), note);
                    pref.AllowBlockerSuppression = request.AllowBlockerSuppression.Value;
                }

                pref.UpdatedAt = now;

                return Task.FromResult(new NotificationPreferenceResponse
                {
                    Success = true,
                    Preference = pref
                });
            }
        }

        // ── GetEffectivePreferencesAsync ──────────────────────────────────────

        /// <inheritdoc/>
        public Task<NotificationPreference> GetEffectivePreferencesAsync(string operatorId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);

            lock (_lock)
            {
                if (_preferences.TryGetValue(operatorId, out var existing))
                    return Task.FromResult(existing);

                var def = BuildDefault(operatorId);
                _preferences[operatorId] = def;
                return Task.FromResult(def);
            }
        }

        // ── IsNotificationAllowed ─────────────────────────────────────────────

        /// <inheritdoc/>
        public bool IsNotificationAllowed(
            ComplianceEventSeverity severity,
            NotificationWorkflowArea workflowArea,
            NotificationPreference? preference)
        {
            // Fail-closed: Critical events are ALWAYS allowed regardless of preference configuration.
            if (severity == ComplianceEventSeverity.Critical)
                return true;

            if (preference is null)
                return true;

            // Check severity threshold
            if (!PassesSeverityFilter(severity, preference.SeverityThreshold))
                return false;

            // Check workflow area subscriptions (null means subscribed to all)
            if (preference.WorkflowAreaSubscriptions is not null
                && !preference.WorkflowAreaSubscriptions.Contains(workflowArea))
            {
                return false;
            }

            return true;
        }

        // ── ComputeRoutingMetadata ────────────────────────────────────────────

        /// <inheritdoc/>
        public NotificationRoutingMetadata ComputeRoutingMetadata(
            ComplianceEventSeverity severity,
            NotificationWorkflowArea workflowArea,
            NotificationPreference? preference)
        {
            bool passesSeverity = preference is null
                || severity == ComplianceEventSeverity.Critical
                || PassesSeverityFilter(severity, preference.SeverityThreshold);

            bool isInSubscribedArea = preference?.WorkflowAreaSubscriptions is null
                || preference.WorkflowAreaSubscriptions.Count == 0
                || preference.WorkflowAreaSubscriptions.Contains(workflowArea);

            bool isMuted = preference?.MutedWorkflowAreas?.Contains(workflowArea) == true;

            // Determine whether immediate delivery applies
            bool immediateDelivery =
                severity == ComplianceEventSeverity.Critical
                || preference is null
                || !preference.DigestEnabled
                || preference.DigestPolicy.Frequency == DigestFrequency.Immediate
                || (preference.DigestPolicy.AlwaysImmediateForCritical && severity == ComplianceEventSeverity.Critical);

            string? suggestedWindow = immediateDelivery
                ? null
                : preference?.DigestPolicy.Frequency.ToString();

            return new NotificationRoutingMetadata
            {
                ImmediateDelivery = immediateDelivery,
                IsMuted = isMuted,
                PassesSeverityThreshold = passesSeverity,
                IsInSubscribedArea = isInSubscribedArea,
                SuggestedDigestWindow = suggestedWindow
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool PassesSeverityFilter(
            ComplianceEventSeverity severity,
            NotificationSeverityThreshold threshold)
        {
            return threshold switch
            {
                NotificationSeverityThreshold.All => true,
                NotificationSeverityThreshold.WarningAndAbove =>
                    severity == ComplianceEventSeverity.Warning
                    || severity == ComplianceEventSeverity.Critical,
                NotificationSeverityThreshold.CriticalOnly =>
                    severity == ComplianceEventSeverity.Critical,
                _ => true
            };
        }

        private NotificationPreference BuildDefault(string operatorId)
        {
            var now = _timeProvider.GetUtcNow();
            return new NotificationPreference
            {
                OperatorId = operatorId,
                SeverityThreshold = NotificationSeverityThreshold.All,
                DigestEnabled = true,
                DigestPolicy = new NotificationDigestPolicy(),
                EscalationEnabled = true,
                AllowBlockerSuppression = false,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        private static void AddAuditEntry(
            NotificationPreference pref,
            DateTimeOffset changedAt,
            string changedBy,
            string field,
            string? previousValue,
            string? newValue,
            string? note)
        {
            pref.AuditTrail.Add(new NotificationPreferenceAuditEntry
            {
                EntryId = Guid.NewGuid().ToString(),
                ChangedAt = changedAt,
                ChangedBy = changedBy,
                FieldChanged = field,
                PreviousValue = previousValue,
                NewValue = newValue,
                Note = note
            });
        }
    }
}
