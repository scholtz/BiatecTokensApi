namespace BiatecTokensApi.Models.KycAmlOnboarding
{
    // ── Enums ─────────────────────────────────────────────────────────────────────

    /// <summary>State machine for a KYC/AML onboarding case lifecycle.</summary>
    public enum KycAmlOnboardingCaseState
    {
        /// <summary>Case has been created but provider checks have not started.</summary>
        Initiated,
        /// <summary>Provider checks have been initiated successfully.</summary>
        ProviderChecksStarted,
        /// <summary>Checks complete; case is awaiting human review.</summary>
        PendingReview,
        /// <summary>A reviewer is actively working the case.</summary>
        UnderReview,
        /// <summary>Additional documentation or information was requested from the subject.</summary>
        RequiresAdditionalInfo,
        /// <summary>Case has been escalated for senior review.</summary>
        Escalated,
        /// <summary>Case has been approved.</summary>
        Approved,
        /// <summary>Case has been rejected.</summary>
        Rejected,
        /// <summary>Case has expired without a final decision.</summary>
        Expired,
        /// <summary>Provider was unreachable when checks were attempted.</summary>
        ProviderUnavailable,
        /// <summary>Required provider configuration is absent.</summary>
        ConfigurationMissing
    }

    /// <summary>The kind of subject being onboarded.</summary>
    public enum KycAmlOnboardingSubjectKind
    {
        /// <summary>Individual natural person.</summary>
        Individual,
        /// <summary>Legal entity / business.</summary>
        Business,
        /// <summary>Trust or similar structure.</summary>
        Trust,
        /// <summary>Unknown or unspecified.</summary>
        Unknown
    }

    /// <summary>Kinds of reviewer actions that can be recorded on a case.</summary>
    public enum KycAmlOnboardingActionKind
    {
        /// <summary>Approve the case.</summary>
        Approve,
        /// <summary>Reject the case.</summary>
        Reject,
        /// <summary>Escalate the case.</summary>
        Escalate,
        /// <summary>Request additional information from the subject.</summary>
        RequestAdditionalInfo,
        /// <summary>Add an informational note without changing state.</summary>
        AddNote
    }

    /// <summary>Evidence quality/state for an onboarding case.</summary>
    public enum KycAmlOnboardingEvidenceState
    {
        /// <summary>Evidence is provider-backed and suitable for release-grade decisions.</summary>
        AuthoritativeProviderBacked,
        /// <summary>Evidence is present but degraded or only partially provider-backed.</summary>
        DegradedPartialEvidence,
        /// <summary>Evidence exists but is stale and should be refreshed.</summary>
        StaleEvidence,
        /// <summary>Provider configuration is missing; no evidence can be obtained.</summary>
        MissingConfiguration,
        /// <summary>Provider is configured but currently unavailable.</summary>
        ProviderUnavailable,
        /// <summary>Checks are pending or in progress; evidence not yet available.</summary>
        PendingVerification
    }

    /// <summary>Execution mode when initiating provider checks.</summary>
    public enum KycAmlOnboardingExecutionMode
    {
        /// <summary>Use the live production provider.</summary>
        LiveProvider,
        /// <summary>Use a protected sandbox environment.</summary>
        ProtectedSandbox,
        /// <summary>Simulate checks without calling a real provider.</summary>
        Simulated
    }

    /// <summary>Typed onboarding timeline events used for operator-facing audit trails.</summary>
    public enum KycAmlOnboardingTimelineEventType
    {
        /// <summary>The onboarding case was created.</summary>
        CaseCreated,
        /// <summary>Provider checks were initiated successfully.</summary>
        ProviderChecksInitiated,
        /// <summary>Provider checks could not start because configuration is missing.</summary>
        ProviderConfigurationMissing,
        /// <summary>Provider checks could not complete because the provider is unavailable or degraded.</summary>
        ProviderUnavailable,
        /// <summary>A reviewer action or note was recorded.</summary>
        ReviewerActionRecorded
    }

    // ── Core records ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents the full lifecycle record of a KYC/AML onboarding case.
    /// </summary>
    public record KycAmlOnboardingCase
    {
        /// <summary>Unique case identifier.</summary>
        public string CaseId { get; init; } = string.Empty;

        /// <summary>Identifier of the subject being onboarded.</summary>
        public string SubjectId { get; init; } = string.Empty;

        /// <summary>Kind of subject (individual, business, etc.).</summary>
        public KycAmlOnboardingSubjectKind SubjectKind { get; init; } = KycAmlOnboardingSubjectKind.Unknown;

        /// <summary>Current state of the case.</summary>
        public KycAmlOnboardingCaseState State { get; set; } = KycAmlOnboardingCaseState.Initiated;

        /// <summary>ID of the associated live-provider verification journey, if one was initiated.</summary>
        public string? VerificationJourneyId { get; set; }

        /// <summary>UTC timestamp when the case was created.</summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>UTC timestamp of the most recent update.</summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Ordered list of reviewer actions taken on this case.</summary>
        public List<KycAmlOnboardingActorAction> Actions { get; init; } = new();

        /// <summary>Chronological onboarding timeline entries for this case.</summary>
        public List<KycAmlOnboardingTimelineEvent> Timeline { get; init; } = new();

        /// <summary>Current evidence state for the case.</summary>
        public KycAmlOnboardingEvidenceState EvidenceState { get; set; } = KycAmlOnboardingEvidenceState.PendingVerification;

        /// <summary>Whether the required provider is configured.</summary>
        public bool IsProviderConfigured { get; set; }

        /// <summary>Optional correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; init; }

        /// <summary>Free-form metadata about the subject.</summary>
        public Dictionary<string, string> SubjectMetadata { get; init; } = new();

        /// <summary>Active blockers preventing case progression.</summary>
        public List<string> Blockers { get; init; } = new();

        /// <summary>Optional organisation name for business subjects.</summary>
        public string? OrganizationName { get; init; }
    }

    /// <summary>
    /// Immutable onboarding timeline entry capturing a business-significant lifecycle change.
    /// </summary>
    public record KycAmlOnboardingTimelineEvent
    {
        /// <summary>Unique identifier for this timeline event.</summary>
        public string EventId { get; init; } = string.Empty;

        /// <summary>Type of business event recorded.</summary>
        public KycAmlOnboardingTimelineEventType EventType { get; init; }

        /// <summary>UTC timestamp when the event occurred.</summary>
        public DateTimeOffset OccurredAt { get; init; }

        /// <summary>Actor or subsystem that caused the event.</summary>
        public string ActorId { get; init; } = string.Empty;

        /// <summary>Plain-language summary suitable for operator timelines.</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>Case state before the event, when applicable.</summary>
        public KycAmlOnboardingCaseState? FromState { get; init; }

        /// <summary>Case state after the event, when applicable.</summary>
        public KycAmlOnboardingCaseState? ToState { get; init; }

        /// <summary>Correlation ID propagated from the originating workflow, if available.</summary>
        public string? CorrelationId { get; init; }

        /// <summary>Structured metadata describing the event in more detail.</summary>
        public Dictionary<string, string> Metadata { get; init; } = new();
    }

    /// <summary>
    /// A single action recorded by a reviewer on an onboarding case.
    /// </summary>
    public record KycAmlOnboardingActorAction
    {
        /// <summary>Unique action identifier.</summary>
        public string ActionId { get; init; } = string.Empty;

        /// <summary>Kind of reviewer action.</summary>
        public KycAmlOnboardingActionKind Kind { get; init; }

        /// <summary>Actor who performed the action.</summary>
        public string ActorId { get; init; } = string.Empty;

        /// <summary>UTC timestamp when the action was taken.</summary>
        public DateTimeOffset Timestamp { get; init; }

        /// <summary>Rationale for the action.</summary>
        public string? Rationale { get; init; }

        /// <summary>Optional free-form notes.</summary>
        public string? Notes { get; init; }
    }

    /// <summary>
    /// Evidence summary for an onboarding case.
    /// </summary>
    public record KycAmlOnboardingEvidenceSummary
    {
        /// <summary>Case identifier this summary relates to.</summary>
        public string CaseId { get; init; } = string.Empty;

        /// <summary>Current evidence quality state.</summary>
        public KycAmlOnboardingEvidenceState EvidenceState { get; init; }

        /// <summary>Whether evidence is backed by a live provider.</summary>
        public bool IsProviderBacked { get; init; }

        /// <summary>Whether evidence is considered release-grade.</summary>
        public bool IsReleaseGrade { get; init; }

        /// <summary>Number of checks completed.</summary>
        public int ChecksCompleted { get; init; }

        /// <summary>UTC timestamp of the most recent provider check.</summary>
        public DateTimeOffset? LastCheckedAt { get; init; }

        /// <summary>Names of the providers consulted.</summary>
        public List<string> ProviderNames { get; init; } = new();

        /// <summary>Active blockers preventing evidence from being release-grade.</summary>
        public List<string> Blockers { get; init; } = new();

        /// <summary>Actionable guidance for the operator.</summary>
        public string ActionableGuidance { get; init; } = string.Empty;

        /// <summary>Whether the required provider is configured.</summary>
        public bool IsProviderConfigured { get; init; }
    }

    /// <summary>
    /// Readiness state of the KYC/AML provider configuration.
    /// </summary>
    public record KycAmlOnboardingConfigReadiness
    {
        /// <summary>Whether a KYC provider is configured.</summary>
        public bool IsKycProviderConfigured { get; init; }

        /// <summary>Whether an AML provider is configured.</summary>
        public bool IsAmlProviderConfigured { get; init; }

        /// <summary>True only when all required providers are configured.</summary>
        public bool OverallReady { get; init; }

        /// <summary>Names of missing providers.</summary>
        public List<string> MissingProviders { get; init; } = new();

        /// <summary>Actionable guidance for the operator.</summary>
        public string ActionableGuidance { get; init; } = string.Empty;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────────

    /// <summary>Request to create a new KYC/AML onboarding case.</summary>
    public record CreateOnboardingCaseRequest
    {
        /// <summary>Identifier of the subject to onboard. Required.</summary>
        public string SubjectId { get; init; } = string.Empty;

        /// <summary>Kind of subject being onboarded.</summary>
        public KycAmlOnboardingSubjectKind SubjectKind { get; init; } = KycAmlOnboardingSubjectKind.Unknown;

        /// <summary>Optional organisation name for business subjects.</summary>
        public string? OrganizationName { get; init; }

        /// <summary>Client-supplied idempotency key. If set and a case already exists for the
        /// same SubjectId + key, the existing case is returned.</summary>
        public string? IdempotencyKey { get; init; }

        /// <summary>Free-form metadata about the subject.</summary>
        public Dictionary<string, string>? SubjectMetadata { get; init; }

        /// <summary>Optional correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; init; }
    }

    /// <summary>Response for case creation.</summary>
    public record CreateOnboardingCaseResponse
    {
        /// <summary>Whether the call succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>The created (or idempotently returned) case.</summary>
        public KycAmlOnboardingCase? Case { get; init; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; init; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; init; }
    }

    /// <summary>Response for retrieving a single case.</summary>
    public record GetOnboardingCaseResponse
    {
        /// <summary>Whether the call succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>The retrieved case.</summary>
        public KycAmlOnboardingCase? Case { get; init; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; init; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; init; }
    }

    /// <summary>Request to initiate provider checks for an onboarding case.</summary>
    public record InitiateProviderChecksRequest
    {
        /// <summary>Which execution mode to use when calling the provider.</summary>
        public KycAmlOnboardingExecutionMode ExecutionMode { get; init; } = KycAmlOnboardingExecutionMode.LiveProvider;

        /// <summary>Client-supplied idempotency key.</summary>
        public string? IdempotencyKey { get; init; }

        /// <summary>Optional correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; init; }
    }

    /// <summary>Response for initiating provider checks.</summary>
    public record InitiateProviderChecksResponse
    {
        /// <summary>Whether the call succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>Updated case record.</summary>
        public KycAmlOnboardingCase? Case { get; init; }

        /// <summary>ID of the verification journey created by the provider.</summary>
        public string? VerificationJourneyId { get; init; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; init; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; init; }
    }

    /// <summary>Request to record a reviewer action on an onboarding case.</summary>
    public record RecordReviewerActionRequest
    {
        /// <summary>Kind of action being recorded.</summary>
        public KycAmlOnboardingActionKind Kind { get; init; }

        /// <summary>Rationale for the action.</summary>
        public string? Rationale { get; init; }

        /// <summary>Optional free-form notes.</summary>
        public string? Notes { get; init; }

        /// <summary>Optional correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; init; }
    }

    /// <summary>Response for recording a reviewer action.</summary>
    public record RecordReviewerActionResponse
    {
        /// <summary>Whether the call succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>The recorded action.</summary>
        public KycAmlOnboardingActorAction? Action { get; init; }

        /// <summary>Updated case record.</summary>
        public KycAmlOnboardingCase? Case { get; init; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; init; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; init; }
    }

    /// <summary>Response for evidence summary queries.</summary>
    public record GetOnboardingEvidenceSummaryResponse
    {
        /// <summary>Whether the call succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>Evidence summary for the case.</summary>
        public KycAmlOnboardingEvidenceSummary? Summary { get; init; }

        /// <summary>Machine-readable error code on failure.</summary>
        public string? ErrorCode { get; init; }

        /// <summary>Human-readable error message on failure.</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; init; }
    }

    /// <summary>Request parameters for listing onboarding cases.</summary>
    public record ListOnboardingCasesRequest
    {
        /// <summary>Filter by subject ID.</summary>
        public string? SubjectId { get; init; }

        /// <summary>Filter by case state.</summary>
        public KycAmlOnboardingCaseState? State { get; init; }

        /// <summary>Maximum number of results per page.</summary>
        public int PageSize { get; init; } = 50;

        /// <summary>Pagination token from a previous response.</summary>
        public string? PageToken { get; init; }
    }

    /// <summary>Response for listing onboarding cases.</summary>
    public record ListOnboardingCasesResponse
    {
        /// <summary>Whether the call succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>Page of matching cases.</summary>
        public List<KycAmlOnboardingCase> Cases { get; init; } = new();

        /// <summary>Total number of matching cases (before pagination).</summary>
        public int TotalCount { get; init; }

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; init; }
    }
}
