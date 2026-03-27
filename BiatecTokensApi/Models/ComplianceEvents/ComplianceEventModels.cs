using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;

namespace BiatecTokensApi.Models.ComplianceEvents
{
    /// <summary>Business-readable type of entity that emitted a compliance event.</summary>
    public enum ComplianceEventEntityKind
    {
        /// <summary>KYC/AML onboarding case lifecycle event.</summary>
        OnboardingCase,
        /// <summary>Compliance case lifecycle or evidence event.</summary>
        ComplianceCase,
        /// <summary>Compliance webhook delivery event.</summary>
        NotificationDelivery,
        /// <summary>Protected sign-off approval webhook event.</summary>
        ProtectedSignOffWebhook,
        /// <summary>Protected sign-off evidence-pack event.</summary>
        ProtectedSignOffEvidence,
        /// <summary>Release-readiness evaluation event for a specific head.</summary>
        ReleaseReadiness,
        /// <summary>Compliance audit export package assembly event.</summary>
        ComplianceAuditExport
    }

    /// <summary>Canonical typed compliance event kinds exposed to operator timelines.</summary>
    public enum ComplianceEventType
    {
        /// <summary>An onboarding case was created.</summary>
        OnboardingCaseCreated,
        /// <summary>Onboarding provider checks were initiated.</summary>
        OnboardingProviderChecksInitiated,
        /// <summary>An onboarding reviewer action was recorded.</summary>
        OnboardingReviewerActionRecorded,
        /// <summary>Onboarding could not proceed because configuration is missing.</summary>
        OnboardingNotConfigured,
        /// <summary>Onboarding could not proceed because the provider is unavailable.</summary>
        OnboardingProviderUnavailable,
        /// <summary>A compliance case was created.</summary>
        ComplianceCaseCreated,
        /// <summary>A compliance case state or readiness transition was recorded.</summary>
        ComplianceCaseStateChanged,
        /// <summary>Evidence was added or refreshed for a compliance case.</summary>
        ComplianceCaseEvidenceUpdated,
        /// <summary>Evidence on a compliance case became stale.</summary>
        ComplianceCaseEvidenceStale,
        /// <summary>A compliance escalation was raised.</summary>
        ComplianceEscalationRaised,
        /// <summary>A compliance escalation was resolved.</summary>
        ComplianceEscalationResolved,
        /// <summary>A compliance decision was recorded.</summary>
        ComplianceDecisionRecorded,
        /// <summary>A case-level evidence or export package was generated.</summary>
        ComplianceEvidenceGenerated,
        /// <summary>A webhook or notification delivery attempt changed status.</summary>
        NotificationDeliveryUpdated,
        /// <summary>A protected sign-off approval webhook was recorded.</summary>
        ProtectedSignOffApprovalWebhookRecorded,
        /// <summary>A protected sign-off evidence pack was captured.</summary>
        ProtectedSignOffEvidenceCaptured,
        /// <summary>Release readiness was evaluated for a head ref.</summary>
        ReleaseReadinessEvaluated,
        /// <summary>A compliance audit export package was assembled.</summary>
        ComplianceAuditExportGenerated
    }

    /// <summary>Finite severity scale for operator-facing event prioritisation.</summary>
    public enum ComplianceEventSeverity
    {
        /// <summary>Informational event with no immediate operator action required.</summary>
        Informational,
        /// <summary>Warning event requiring operator attention but not immediate blocking action.</summary>
        Warning,
        /// <summary>Critical event representing a blocker, failure, or fail-closed degradation.</summary>
        Critical
    }

    /// <summary>Source attribution for a compliance event.</summary>
    public enum ComplianceEventSource
    {
        /// <summary>Operator-triggered action.</summary>
        Operator,
        /// <summary>System-generated or background evaluation.</summary>
        System,
        /// <summary>Provider or upstream service transition.</summary>
        Provider,
        /// <summary>Webhook callback or webhook delivery lifecycle.</summary>
        Webhook,
        /// <summary>Evidence or export generation subsystem.</summary>
        Evidence,
        /// <summary>Protected environment or release-readiness evaluator.</summary>
        ProtectedEnvironment
    }

    /// <summary>Explicit freshness/degradation classification for operator timelines.</summary>
    public enum ComplianceEventFreshness
    {
        /// <summary>Fresh confirmed proof is available.</summary>
        Current,
        /// <summary>The system is still waiting on a provider callback or provider-backed result.</summary>
        AwaitingProviderCallback,
        /// <summary>Evidence exists but is incomplete or only partially authoritative.</summary>
        PartialEvidence,
        /// <summary>Evidence exists but is stale relative to the required freshness window.</summary>
        Stale,
        /// <summary>Required upstream configuration is absent.</summary>
        NotConfigured,
        /// <summary>Upstream system is unavailable or unreachable.</summary>
        Unavailable,
        /// <summary>Historical event that does not describe current freshness.</summary>
        Historical
    }

    /// <summary>Webhook-ready delivery state for outbound compliance notifications.</summary>
    public enum ComplianceEventDeliveryStatus
    {
        /// <summary>No delivery attempt has been made.</summary>
        NotAttempted,
        /// <summary>Delivery is pending or scheduled for retry.</summary>
        Waiting,
        /// <summary>Delivery completed successfully.</summary>
        Sent,
        /// <summary>Delivery failed.</summary>
        Failed,
        /// <summary>Delivery was intentionally skipped.</summary>
        Skipped,
        /// <summary>Delivery could not start because required configuration is missing.</summary>
        NotConfigured
    }

    /// <summary>Canonical event envelope used by the compliance-event backbone API.</summary>
    public class ComplianceEventEnvelope
    {
        /// <summary>Stable identifier for this event.</summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>Typed business event kind.</summary>
        public ComplianceEventType EventType { get; set; }

        /// <summary>Kind of entity that originated the event.</summary>
        public ComplianceEventEntityKind EntityKind { get; set; }

        /// <summary>Identifier of the entity that originated the event.</summary>
        public string EntityId { get; set; } = string.Empty;

        /// <summary>Compliance or onboarding case ID linked to this event, when applicable.</summary>
        public string? CaseId { get; set; }

        /// <summary>Subject or investor identifier linked to this event, when applicable.</summary>
        public string? SubjectId { get; set; }

        /// <summary>Release head or tag linked to this event, when applicable.</summary>
        public string? HeadRef { get; set; }

        /// <summary>UTC timestamp when the event occurred.</summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>Actor or subsystem that caused the event.</summary>
        public string? ActorId { get; set; }

        /// <summary>Correlation ID joining this event to related workflow transitions.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Business-readable label for timeline rendering.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Plain-language event summary suitable for operator UI display.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Suggested next action when operator intervention is required.</summary>
        public string? RecommendedAction { get; set; }

        /// <summary>Finite severity classification.</summary>
        public ComplianceEventSeverity Severity { get; set; }

        /// <summary>Source attribution.</summary>
        public ComplianceEventSource Source { get; set; }

        /// <summary>Freshness or degradation classification for this event.</summary>
        public ComplianceEventFreshness Freshness { get; set; } = ComplianceEventFreshness.Historical;

        /// <summary>Webhook/delivery status associated with this event.</summary>
        public ComplianceEventDeliveryStatus DeliveryStatus { get; set; } = ComplianceEventDeliveryStatus.NotAttempted;

        /// <summary>Structured payload fields for UI, export, and future webhook consumers.</summary>
        public Dictionary<string, string> Payload { get; set; } = new();
    }

    /// <summary>Query parameters for the compliance-event backbone.</summary>
    public class ComplianceEventQueryRequest
    {
        /// <summary>Filter by case ID across onboarding, compliance, and sign-off domains.</summary>
        public string? CaseId { get; set; }

        /// <summary>Filter by subject or investor identifier.</summary>
        public string? SubjectId { get; set; }

        /// <summary>Filter by a specific entity identifier.</summary>
        public string? EntityId { get; set; }

        /// <summary>Filter by release head ref.</summary>
        public string? HeadRef { get; set; }

        /// <summary>Filter by entity kind.</summary>
        public ComplianceEventEntityKind? EntityKind { get; set; }

        /// <summary>Filter by event type.</summary>
        public ComplianceEventType? EventType { get; set; }

        /// <summary>Filter by severity.</summary>
        public ComplianceEventSeverity? Severity { get; set; }

        /// <summary>Filter by source.</summary>
        public ComplianceEventSource? Source { get; set; }

        /// <summary>Filter by freshness classification.</summary>
        public ComplianceEventFreshness? Freshness { get; set; }

        /// <summary>Filter by delivery status.</summary>
        public ComplianceEventDeliveryStatus? DeliveryStatus { get; set; }

        /// <summary>Page number (1-based).</summary>
        public int Page { get; set; } = 1;

        /// <summary>Page size capped at 100.</summary>
        public int PageSize { get; set; } = 50;
    }

    /// <summary>Current delivery/degradation state derived from a filtered event set.</summary>
    public class ComplianceEventStateSummary
    {
        /// <summary>Total filtered events considered for this summary.</summary>
        public int TotalEvents { get; set; }

        /// <summary>UTC timestamp of the newest event in the filtered set.</summary>
        public DateTimeOffset? LatestEventAt { get; set; }

        /// <summary>Highest severity present in the filtered event set.</summary>
        public ComplianceEventSeverity HighestSeverity { get; set; }

        /// <summary>Current delivery status summarised across matching events.</summary>
        public ComplianceEventDeliveryStatus CurrentDeliveryStatus { get; set; }

        /// <summary>Current freshness summarised across matching events.</summary>
        public ComplianceEventFreshness CurrentFreshness { get; set; }

        /// <summary>True when the filtered set contains any failed deliveries.</summary>
        public bool HasFailedDelivery { get; set; }

        /// <summary>True when the filtered set contains any pending or retrying deliveries.</summary>
        public bool HasPendingDelivery { get; set; }

        /// <summary>True when the filtered set contains both successful and degraded delivery outcomes.</summary>
        public bool HasPartialDelivery { get; set; }

        /// <summary>True when any event reflects missing configuration.</summary>
        public bool HasNotConfigured { get; set; }

        /// <summary>True when any event reflects stale evidence.</summary>
        public bool HasStaleEvidence { get; set; }

        /// <summary>True when any event is still waiting on an upstream provider callback.</summary>
        public bool HasAwaitingProviderCallback { get; set; }

        /// <summary>Top-level operator guidance derived from the most severe current condition.</summary>
        public string? RecommendedAction { get; set; }

        /// <summary>Optional current-head release-readiness snapshot when a head ref was requested.</summary>
        public GetSignOffReleaseReadinessResponse? ReleaseReadiness { get; set; }
    }

    /// <summary>
    /// Operator-dashboard queue summary counts for the compliance-event backbone.
    /// Each category is counted independently so operators can understand multiple dimensions at once.
    /// </summary>
    public class ComplianceEventQueueSummary
    {
        /// <summary>Events with Critical severity representing hard blockers or failures.</summary>
        public int BlockedCount { get; set; }

        /// <summary>Events with Warning severity requiring operator attention.</summary>
        public int ActionNeededCount { get; set; }

        /// <summary>Events whose freshness is AwaitingProviderCallback, indicating upstream delay.</summary>
        public int WaitingOnProviderCount { get; set; }

        /// <summary>Events with Stale freshness whose evidence is no longer authoritative.</summary>
        public int StaleCount { get; set; }

        /// <summary>Events with Informational severity that require no immediate action.</summary>
        public int InformationalCount { get; set; }

        /// <summary>Events with NotConfigured freshness or delivery status representing missing setup.</summary>
        public int NotConfiguredCount { get; set; }

        /// <summary>Events with Unavailable freshness representing upstream unavailability.</summary>
        public int UnavailableCount { get; set; }

        /// <summary>Events with Failed delivery status.</summary>
        public int FailedDeliveryCount { get; set; }

        /// <summary>Events with Waiting delivery status pending retry or initial attempt.</summary>
        public int PendingDeliveryCount { get; set; }

        /// <summary>Total events considered when computing the summary.</summary>
        public int TotalCount { get; set; }

        /// <summary>Top-level operator guidance derived from the most severe current condition.</summary>
        public string? RecommendedAction { get; set; }

        /// <summary>UTC timestamp when the summary was computed.</summary>
        public DateTimeOffset ComputedAt { get; set; }
    }

    /// <summary>Response from the compliance-event queue summary endpoint.</summary>
    public class ComplianceEventQueueSummaryResponse
    {
        /// <summary>True when the summary was computed successfully.</summary>
        public bool Success { get; set; }

        /// <summary>Queue summary counts broken down by operator-dashboard categories.</summary>
        public ComplianceEventQueueSummary Summary { get; set; } = new();

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Paginated response from the compliance-event backbone API.</summary>
    public class ComplianceEventListResponse
    {
        /// <summary>True when the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Paginated list of matching compliance events.</summary>
        public List<ComplianceEventEnvelope> Events { get; set; } = new();

        /// <summary>Total number of matching events before pagination.</summary>
        public int TotalCount { get; set; }

        /// <summary>Requested page number.</summary>
        public int Page { get; set; }

        /// <summary>Requested page size after server-side clamping.</summary>
        public int PageSize { get; set; }

        /// <summary>Derived current delivery/degradation state for the filtered event set.</summary>
        public ComplianceEventStateSummary CurrentState { get; set; } = new();

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Business-readable error message on failure.</summary>
        public string? ErrorMessage { get; set; }
    }
}
