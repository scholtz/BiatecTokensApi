namespace BiatecTokensApi.Models.LiveProviderVerificationJourney
{
    // ── Enums ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ordered stages in a live-provider KYC/AML verification journey.
    /// Stages advance linearly during successful execution and branch to terminal
    /// states (Degraded, Failed) when providers are unavailable or checks fail.
    /// </summary>
    public enum VerificationJourneyStage
    {
        /// <summary>Journey record created; no provider calls made yet.</summary>
        NotStarted,

        /// <summary>KYC identity check has been dispatched to the provider.</summary>
        KycInitiated,

        /// <summary>KYC check dispatched; awaiting provider callback or polling result.</summary>
        KycPending,

        /// <summary>KYC identity check completed with a provider-confirmed outcome.</summary>
        KycCompleted,

        /// <summary>AML / sanctions screening has been dispatched to the provider.</summary>
        AmlInitiated,

        /// <summary>AML screening dispatched; awaiting provider callback or polling result.</summary>
        AmlPending,

        /// <summary>AML screening completed with a provider-confirmed outcome.</summary>
        AmlCompleted,

        /// <summary>
        /// All automated checks completed; case is queued for analyst review.
        /// Human decision required before advancing.
        /// </summary>
        UnderReview,

        /// <summary>
        /// All checks passed and evidence is not stale. Approval decision may be issued.
        /// </summary>
        ApprovalReady,

        /// <summary>The verification journey ended with an approval decision.</summary>
        Approved,

        /// <summary>The verification journey ended with a rejection decision.</summary>
        Rejected,

        /// <summary>
        /// Subject matched a sanctions watchlist or adverse indicator.
        /// Sanctions-review procedure is active; journey cannot advance until resolved.
        /// </summary>
        SanctionsReview,

        /// <summary>
        /// One or more blockers require additional information or action before the
        /// journey can advance.
        /// </summary>
        RequiresAction,

        /// <summary>Journey has been escalated for senior-analyst review.</summary>
        Escalated,

        /// <summary>
        /// Provider reported a transient error, returned a malformed response, or
        /// required configuration is absent. The journey is in a degraded state and
        /// must not proceed to approval. Actionable diagnostics are included.
        /// </summary>
        Degraded,

        /// <summary>
        /// Journey has entered a terminal failure state. Evidence cannot be used for
        /// approval gating. A new journey must be started after root-cause resolution.
        /// </summary>
        Failed
    }

    /// <summary>
    /// Execution mode resolved for the verification journey.
    /// Determines whether resulting evidence qualifies as release-grade.
    /// </summary>
    public enum VerificationJourneyExecutionMode
    {
        /// <summary>
        /// The journey was executed against a live, production-grade external provider
        /// using real API credentials. Evidence is release-grade.
        /// </summary>
        LiveProvider,

        /// <summary>
        /// The journey was executed against a provider-operated sandbox or protected
        /// test environment using protected (non-production) API keys.
        /// Evidence is production-like but not live. Suitable for sign-off validation.
        /// </summary>
        ProtectedSandbox,

        /// <summary>
        /// The journey was executed using an internal simulation / mock provider.
        /// Evidence is not release-grade. Use for unit and integration tests only.
        /// </summary>
        Simulated,

        /// <summary>
        /// Execution mode could not be resolved because the journey has not yet been
        /// initiated or the provider configuration is absent.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Severity of a degraded-state or blocker condition observed during a journey.
    /// </summary>
    public enum JourneyBlockerSeverity
    {
        /// <summary>A recoverable warning; the journey may continue with operator action.</summary>
        Warning,

        /// <summary>
        /// A blocking condition that prevents the journey from advancing until resolved.
        /// </summary>
        Blocking,

        /// <summary>
        /// A critical failure. The journey must be abandoned and restarted after
        /// root-cause resolution.
        /// </summary>
        Critical
    }

    // ── Step record ──────────────────────────────────────────────────────────────

    /// <summary>
    /// An immutable record of a single step performed during a verification journey.
    /// Steps form an ordered audit trail that operators and compliance leads can review.
    /// </summary>
    public class VerificationJourneyStep
    {
        /// <summary>Unique identifier for this step record.</summary>
        public string StepId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Human-readable name for the step (e.g., "KYC Provider Initiation").</summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>The journey stage reached after this step completed.</summary>
        public VerificationJourneyStage StageAfterStep { get; set; }

        /// <summary>True when this step was executed against a live or sandbox provider.</summary>
        public bool IsProviderBacked { get; set; }

        /// <summary>True when this step's evidence qualifies as release-grade.</summary>
        public bool IsReleaseGrade { get; set; }

        /// <summary>UTC timestamp when this step occurred.</summary>
        public DateTimeOffset OccurredAt { get; set; }

        /// <summary>Plain-language description of what occurred in this step.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Name of the external provider used for this step, if applicable.</summary>
        public string? ProviderName { get; set; }

        /// <summary>Provider-assigned reference ID for this check, if applicable.</summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>True when this step completed without errors.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error or degraded-state message if <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error or degraded-state code if <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Optional key-value diagnostics captured at step execution time.</summary>
        public Dictionary<string, string> Diagnostics { get; set; } = new();
    }

    // ── Diagnostics ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A single blocker condition observed during a verification journey.
    /// Blockers explain why a journey cannot advance and what must be done to resolve it.
    /// </summary>
    public class VerificationJourneyBlocker
    {
        /// <summary>Machine-readable blocker code for programmatic handling.</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Human-readable description of the blocking condition.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Severity of the blocking condition.</summary>
        public JourneyBlockerSeverity Severity { get; set; }

        /// <summary>True when operator action can resolve this blocker.</summary>
        public bool IsRemediable { get; set; }

        /// <summary>Plain-language guidance for resolving this blocker.</summary>
        public string? RemediationGuidance { get; set; }

        /// <summary>UTC timestamp when this blocker was recorded.</summary>
        public DateTimeOffset RecordedAt { get; set; }
    }

    /// <summary>
    /// Structured diagnostics for a live-provider verification journey.
    /// All fields are populated regardless of success or failure to enable
    /// operator-friendly triage without requiring log access.
    /// </summary>
    public class VerificationJourneyDiagnostics
    {
        /// <summary>True when all required provider configuration is present.</summary>
        public bool IsConfigurationValid { get; set; }

        /// <summary>True when the configured external provider responded successfully.</summary>
        public bool IsProviderReachable { get; set; }

        /// <summary>True when KYC identity verification completed with a provider-confirmed outcome.</summary>
        public bool IsKycComplete { get; set; }

        /// <summary>True when AML / sanctions screening completed with a provider-confirmed outcome.</summary>
        public bool IsAmlComplete { get; set; }

        /// <summary>True when this journey was executed against a live or sandbox provider.</summary>
        public bool IsProviderBacked { get; set; }

        /// <summary>
        /// True when all checks passed with provider-backed evidence that is not stale.
        /// This is the release-gate qualification flag.
        /// </summary>
        public bool IsReleaseGrade { get; set; }

        /// <summary>The execution mode resolved for this journey.</summary>
        public VerificationJourneyExecutionMode ExecutionMode { get; set; }

        /// <summary>List of missing or invalid configuration keys that blocked execution.</summary>
        public List<string> MissingConfiguration { get; set; } = new();

        /// <summary>Active blockers preventing the journey from advancing.</summary>
        public List<VerificationJourneyBlocker> ActiveBlockers { get; set; } = new();

        /// <summary>
        /// Plain-language guidance for operators to resolve blocking or degraded conditions.
        /// Non-null when <see cref="IsConfigurationValid"/> or <see cref="IsProviderReachable"/> is false,
        /// or when active blockers exist.
        /// </summary>
        public string? ActionableGuidance { get; set; }

        /// <summary>
        /// Human-readable description of the current degraded-state condition, if any.
        /// Non-null when <see cref="VerificationJourneyStage.Degraded"/> is active.
        /// </summary>
        public string? DegradedStateReason { get; set; }

        /// <summary>UTC timestamp when diagnostics were last evaluated.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }
    }

    // ── Approval decision explanation ─────────────────────────────────────────────

    /// <summary>
    /// Structured explanation of the current approval decision state for a verification journey.
    /// Provides the operator-facing rationale for why a case is approved, rejected,
    /// pending review, blocked, or requiring action.
    /// </summary>
    public class ApprovalDecisionExplanation
    {
        /// <summary>The verification journey ID this explanation applies to.</summary>
        public string JourneyId { get; set; } = string.Empty;

        /// <summary>The current journey stage at evaluation time.</summary>
        public VerificationJourneyStage CurrentStage { get; set; }

        /// <summary>True when the journey has reached <see cref="VerificationJourneyStage.ApprovalReady"/>.</summary>
        public bool IsApprovalReady { get; set; }

        /// <summary>True when evidence produced by this journey qualifies as release-grade.</summary>
        public bool IsReleaseGrade { get; set; }

        /// <summary>True when evidence was produced by a live or sandbox provider.</summary>
        public bool IsProviderBacked { get; set; }

        /// <summary>Plain-language summary of the overall verification outcome.</summary>
        public string OutcomeSummary { get; set; } = string.Empty;

        /// <summary>
        /// Rationale for an approval decision.
        /// Non-null when <see cref="CurrentStage"/> is <see cref="VerificationJourneyStage.ApprovalReady"/>
        /// or <see cref="VerificationJourneyStage.Approved"/>.
        /// </summary>
        public string? ApprovalRationale { get; set; }

        /// <summary>
        /// Reason for a rejection decision.
        /// Non-null when <see cref="CurrentStage"/> is <see cref="VerificationJourneyStage.Rejected"/>.
        /// </summary>
        public string? RejectionReason { get; set; }

        /// <summary>
        /// Plain-language description of what the operator must do next.
        /// Non-null when the journey is not in a terminal state and action is required.
        /// </summary>
        public string? RequiredNextAction { get; set; }

        /// <summary>Ordered list of checks that passed during the journey.</summary>
        public List<string> ChecksPassed { get; set; } = new();

        /// <summary>Ordered list of checks that failed definitively during the journey.</summary>
        public List<string> ChecksFailed { get; set; } = new();

        /// <summary>Ordered list of checks that are still pending provider confirmation.</summary>
        public List<string> ChecksPending { get; set; } = new();

        /// <summary>Structured diagnostics supporting this decision explanation.</summary>
        public VerificationJourneyDiagnostics Diagnostics { get; set; } = new();

        /// <summary>UTC timestamp when this explanation was evaluated.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }
    }

    // ── Release evidence ──────────────────────────────────────────────────────────

    /// <summary>
    /// A durable, content-hashed release evidence artifact for a live-provider
    /// verification journey. This artifact provides artifact-backed proof of a
    /// realistic onboarding or compliance verification path that business owners
    /// and compliance leads can review and connect to the release candidate.
    /// </summary>
    public class VerificationJourneyReleaseEvidence
    {
        /// <summary>Unique identifier for this evidence artifact.</summary>
        public string EvidenceId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>The verification journey ID this evidence covers.</summary>
        public string JourneyId { get; set; } = string.Empty;

        /// <summary>The subject ID associated with the verification journey.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Optional compliance case ID associated with the journey.</summary>
        public string? CaseId { get; set; }

        /// <summary>
        /// Optional release tag or label connecting this evidence to the release candidate
        /// being evaluated (e.g., a Git tag, workflow run ID, or release name).
        /// </summary>
        public string? ReleaseTag { get; set; }

        /// <summary>
        /// Optional CI/CD workflow run reference for connecting this evidence to
        /// the exact release head and workflow run.
        /// </summary>
        public string? WorkflowRunReference { get; set; }

        /// <summary>
        /// SHA-256 content hash of the serialised journey steps and decision explanation,
        /// for integrity verification and tamper-evidence.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>UTC timestamp when this evidence artifact was generated.</summary>
        public DateTimeOffset GeneratedAt { get; set; }

        /// <summary>
        /// True when all journey steps were executed against a live or sandbox provider
        /// and evidence qualifies as release-grade.
        /// </summary>
        public bool IsReleaseGrade { get; set; }

        /// <summary>True when evidence was produced by a live or sandbox provider.</summary>
        public bool IsProviderBacked { get; set; }

        /// <summary>The execution mode used for the journey.</summary>
        public VerificationJourneyExecutionMode ExecutionMode { get; set; }

        /// <summary>The journey stage at evidence generation time.</summary>
        public VerificationJourneyStage JourneyStageAtGeneration { get; set; }

        /// <summary>Ordered journey steps forming the audit trail for this evidence.</summary>
        public List<VerificationJourneyStep> Steps { get; set; } = new();

        /// <summary>The approval decision explanation captured at evidence generation time.</summary>
        public ApprovalDecisionExplanation ApprovalDecision { get; set; } = new();

        /// <summary>
        /// Provider reference IDs by check kind (e.g., "KYC" → "sumsub-ref-123").
        /// Connects this evidence to provider-side records.
        /// </summary>
        public Dictionary<string, string> ProviderReferences { get; set; } = new();

        /// <summary>Diagnostics captured at evidence generation time.</summary>
        public VerificationJourneyDiagnostics Diagnostics { get; set; } = new();
    }

    // ── Journey record ────────────────────────────────────────────────────────────

    /// <summary>
    /// The full record for a live-provider KYC/AML verification journey.
    /// Tracks the journey from initiation through KYC, AML, review, and final decision,
    /// including all provider interactions and evidence artifacts.
    /// </summary>
    public class VerificationJourneyRecord
    {
        /// <summary>Unique identifier for this verification journey.</summary>
        public string JourneyId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>The subject being verified (onboarding applicant, token holder, etc.).</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Optional compliance case ID associated with this journey.</summary>
        public string? CaseId { get; set; }

        /// <summary>Optional context ID for linking to a broader issuer or workflow context.</summary>
        public string? ContextId { get; set; }

        /// <summary>The current stage of the verification journey.</summary>
        public VerificationJourneyStage CurrentStage { get; set; } = VerificationJourneyStage.NotStarted;

        /// <summary>The execution mode resolved for this journey.</summary>
        public VerificationJourneyExecutionMode ExecutionMode { get; set; } = VerificationJourneyExecutionMode.Unknown;

        /// <summary>True when this journey has been executed against a live or sandbox provider.</summary>
        public bool IsProviderBacked { get; set; }

        /// <summary>
        /// True when the journey has produced release-grade evidence (provider-backed,
        /// all checks passed, evidence not stale).
        /// </summary>
        public bool IsReleaseGrade { get; set; }

        /// <summary>
        /// ID of the KYC sign-off record created by this journey, if applicable.
        /// </summary>
        public string? KycSignOffRecordId { get; set; }

        /// <summary>
        /// ID of the AML sign-off record created by this journey, if applicable.
        /// </summary>
        public string? AmlSignOffRecordId { get; set; }

        /// <summary>UTC timestamp when this journey was created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>UTC timestamp when this journey record was last updated.</summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Actor (user/system) that initiated this journey.</summary>
        public string InitiatedBy { get; set; } = string.Empty;

        /// <summary>Correlation ID for distributed tracing across the journey lifecycle.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Ordered immutable audit trail of all steps performed during this journey.
        /// </summary>
        public List<VerificationJourneyStep> Steps { get; set; } = new();

        /// <summary>Structured diagnostics for the current journey state.</summary>
        public VerificationJourneyDiagnostics Diagnostics { get; set; } = new();

        /// <summary>
        /// Human-readable explanation of why the journey is at its current stage.
        /// Always populated and suitable for display in operator interfaces.
        /// </summary>
        public string StageExplanation { get; set; } = string.Empty;

        /// <summary>
        /// The most recent provider-reported error or degraded-state description, if any.
        /// Non-null when <see cref="CurrentStage"/> is <see cref="VerificationJourneyStage.Degraded"/>
        /// or <see cref="VerificationJourneyStage.Failed"/>.
        /// </summary>
        public string? LatestProviderError { get; set; }
    }

    // ── Requests ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to start a new live-provider KYC/AML verification journey for a subject.
    /// </summary>
    public class StartVerificationJourneyRequest
    {
        /// <summary>
        /// The subject being verified (onboarding applicant, token holder, corporate entity, etc.).
        /// </summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Optional compliance case ID to associate with this journey.</summary>
        public string? CaseId { get; set; }

        /// <summary>Optional context ID for linking to an issuer or workflow context.</summary>
        public string? ContextId { get; set; }

        /// <summary>
        /// The requested execution mode. Defaults to <see cref="VerificationJourneyExecutionMode.LiveProvider"/>.
        /// </summary>
        public VerificationJourneyExecutionMode RequestedExecutionMode { get; set; } =
            VerificationJourneyExecutionMode.LiveProvider;

        /// <summary>
        /// When true, the journey will fail if execution mode resolves to
        /// <see cref="VerificationJourneyExecutionMode.Simulated"/>. Use for protected
        /// release paths where only provider-backed evidence is accepted.
        /// </summary>
        public bool RequireProviderBacked { get; set; }

        /// <summary>
        /// Subject metadata required by the KYC/AML provider
        /// (e.g., full_name, date_of_birth, country, address).
        /// </summary>
        public Dictionary<string, string> SubjectMetadata { get; set; } = new();

        /// <summary>
        /// Optional idempotency key. If provided and a journey with this key already exists
        /// for the subject, the existing journey is returned without starting a new one.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>Optional correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Request to generate a release-grade evidence artifact for a verification journey.
    /// </summary>
    public class GenerateVerificationJourneyEvidenceRequest
    {
        /// <summary>
        /// When true, evidence generation fails if the journey execution mode is Simulated.
        /// Use for protected release paths.
        /// </summary>
        public bool RequireProviderBacked { get; set; }

        /// <summary>
        /// Optional release tag or label connecting the evidence to the release candidate
        /// (e.g., a Git tag, PR number, or release name).
        /// </summary>
        public string? ReleaseTag { get; set; }

        /// <summary>
        /// Optional CI/CD workflow run reference for exact release head traceability.
        /// </summary>
        public string? WorkflowRunReference { get; set; }

        /// <summary>Optional correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    // ── Responses ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Response from starting a live-provider KYC/AML verification journey.
    /// </summary>
    public class StartVerificationJourneyResponse
    {
        /// <summary>True when the journey was started or an existing journey was returned.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error message if <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code if <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>The created or existing verification journey record.</summary>
        public VerificationJourneyRecord? Journey { get; set; }

        /// <summary>
        /// Structured diagnostics. Always populated to enable operator-friendly triage.
        /// </summary>
        public VerificationJourneyDiagnostics? Diagnostics { get; set; }

        /// <summary>
        /// Plain-language description of the next action an operator should take.
        /// Non-null when <see cref="Success"/> is false or when the journey requires action.
        /// </summary>
        public string? NextAction { get; set; }

        /// <summary>Correlation ID echoed from the request.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Response for a verification journey status query.
    /// </summary>
    public class GetVerificationJourneyStatusResponse
    {
        /// <summary>True when the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error message if <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The current verification journey record.</summary>
        public VerificationJourneyRecord? Journey { get; set; }

        /// <summary>
        /// The current approval decision explanation.
        /// Always populated when <see cref="Success"/> is true.
        /// </summary>
        public ApprovalDecisionExplanation? ApprovalDecision { get; set; }

        /// <summary>Correlation ID for tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Response from an approval decision evaluation request.
    /// </summary>
    public class EvaluateApprovalDecisionResponse
    {
        /// <summary>True when the evaluation completed without errors.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error message if <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The approval decision explanation for the journey.</summary>
        public ApprovalDecisionExplanation? Decision { get; set; }

        /// <summary>Correlation ID for tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Response from a release evidence generation request.
    /// </summary>
    public class GenerateVerificationJourneyEvidenceResponse
    {
        /// <summary>True when the evidence artifact was generated without errors.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error message if <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code if <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>The generated release evidence artifact, or null if generation failed.</summary>
        public VerificationJourneyReleaseEvidence? Evidence { get; set; }

        /// <summary>Correlation ID echoed from the request.</summary>
        public string? CorrelationId { get; set; }
    }
}
