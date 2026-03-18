using BiatecTokensApi.Models.KycAmlSignOff;

namespace BiatecTokensApi.Models.ProviderBackedCompliance
{
    // ── Enums ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Distinguishes how a compliance case decision was executed:
    /// via a live external provider, a protected sandbox, or a local simulation.
    /// Determines whether sign-off evidence qualifies as release-grade.
    /// </summary>
    public enum ProviderBackedCaseExecutionMode
    {
        /// <summary>
        /// The decision was executed against a live, production-grade external provider
        /// using real API credentials. Evidence produced here is release-grade.
        /// </summary>
        LiveProvider,

        /// <summary>
        /// The decision was executed against a provider-operated sandbox or protected
        /// test environment using protected (non-production) API keys. Evidence is
        /// production-like but not live. Suitable for sign-off validation.
        /// </summary>
        ProtectedSandbox,

        /// <summary>
        /// The decision was executed using an internal simulation / mock provider.
        /// Evidence is not release-grade. Suitable for unit and integration tests only.
        /// </summary>
        Simulated
    }

    /// <summary>
    /// The kind of compliance decision being executed through the provider-backed path.
    /// </summary>
    public enum ProviderBackedCaseDecisionKind
    {
        /// <summary>All compliance checks passed; case is being approved.</summary>
        Approve,

        /// <summary>One or more checks failed definitively; case is being rejected.</summary>
        Reject,

        /// <summary>Reviewer requests additional information before proceeding.</summary>
        ReturnForInformation,

        /// <summary>Case entered sanctions review — subject matched a watchlist or adverse indicator.</summary>
        SanctionsReview,

        /// <summary>Case is being escalated for senior review.</summary>
        Escalate
    }

    /// <summary>
    /// Status of the compliance case execution against the provider-backed path.
    /// </summary>
    public enum ProviderBackedCaseExecutionStatus
    {
        /// <summary>No execution has been initiated for this case.</summary>
        NotStarted,

        /// <summary>Execution is in progress (async provider validation running).</summary>
        InProgress,

        /// <summary>Execution completed successfully; case state was transitioned.</summary>
        Completed,

        /// <summary>Execution failed; case state was NOT changed.</summary>
        Failed,

        /// <summary>
        /// Execution blocked because required provider configuration is missing.
        /// Actionable diagnostics are included in the response.
        /// </summary>
        ConfigurationMissing,

        /// <summary>
        /// Execution blocked because the current case state does not permit the
        /// requested decision transition.
        /// </summary>
        InvalidState,

        /// <summary>
        /// Execution blocked because mandatory provider-backed KYC/AML sign-off
        /// evidence is absent or stale.
        /// </summary>
        InsufficientEvidence
    }

    // ── Diagnostics ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Structured diagnostics for a provider-backed compliance case execution attempt.
    /// All fields are populated regardless of success or failure to enable
    /// operator-friendly triage without log access.
    /// </summary>
    public class ProviderBackedCaseExecutionDiagnostics
    {
        /// <summary>True when all required provider configuration keys are present.</summary>
        public bool IsConfigurationPresent { get; set; }

        /// <summary>True when the configured external provider responded to a health check.</summary>
        public bool IsProviderAvailable { get; set; }

        /// <summary>True when the KYC sign-off record for this case's subject is complete and not stale.</summary>
        public bool IsKycSignOffComplete { get; set; }

        /// <summary>True when the AML sign-off record for this case's subject is complete and not stale.</summary>
        public bool IsAmlSignOffComplete { get; set; }

        /// <summary>List of specific configuration keys that are absent or invalid.</summary>
        public List<string> ConfigurationFailures { get; set; } = new();

        /// <summary>List of provider-reported failures (timeout, error codes, etc.).</summary>
        public List<string> ProviderFailures { get; set; } = new();

        /// <summary>
        /// Plain-language guidance for operators to resolve blocking conditions.
        /// This field is always populated when <see cref="IsConfigurationPresent"/> or
        /// <see cref="IsProviderAvailable"/> is false.
        /// </summary>
        public string? ActionableGuidance { get; set; }

        /// <summary>
        /// The execution mode that was used or attempted during this execution.
        /// </summary>
        public ProviderBackedCaseExecutionMode ExecutionMode { get; set; }
    }

    // ── Evidence artifact ─────────────────────────────────────────────────────────

    /// <summary>
    /// Immutable execution evidence artifact produced after a provider-backed
    /// compliance case decision is executed. This record serves as the durable
    /// audit trail entry for the decision and its provider validation outcome.
    /// </summary>
    public class ProviderBackedCaseExecutionEvidence
    {
        /// <summary>Unique identifier for this execution record.</summary>
        public string ExecutionId { get; set; } = string.Empty;

        /// <summary>UTC timestamp of when execution was completed.</summary>
        public DateTimeOffset ExecutedAt { get; set; }

        /// <summary>ID of the compliance case on which the decision was executed.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>The execution mode used for this decision.</summary>
        public ProviderBackedCaseExecutionMode ExecutionMode { get; set; }

        /// <summary>
        /// True when execution was performed against a live or protected-sandbox provider.
        /// False for simulated execution. This flag determines release-grade qualification.
        /// </summary>
        public bool IsProviderBacked => ExecutionMode != ProviderBackedCaseExecutionMode.Simulated;

        /// <summary>The kind of decision that was executed.</summary>
        public ProviderBackedCaseDecisionKind DecisionKind { get; set; }

        /// <summary>The case state after this decision was applied.</summary>
        public string TargetState { get; set; } = string.Empty;

        /// <summary>The case state before this decision was applied.</summary>
        public string? PreviousState { get; set; }

        /// <summary>
        /// True when this evidence was produced by a live or protected-sandbox provider
        /// with all required checks passing. This is the release-gate qualification flag.
        /// </summary>
        public bool IsReleaseGradeEvidence { get; set; }

        /// <summary>Diagnostics at the time of execution.</summary>
        public ProviderBackedCaseExecutionDiagnostics Diagnostics { get; set; } = new();

        /// <summary>The reason recorded for rejection, RFI, or sanctions review.</summary>
        public string? DecisionReason { get; set; }

        /// <summary>Actor (user/system) that initiated the execution.</summary>
        public string? ActorId { get; set; }

        /// <summary>Ordered list of audit steps performed during execution.</summary>
        public List<string> AuditSteps { get; set; } = new();

        /// <summary>Correlation ID passed in the originating request.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// KYC/AML sign-off readiness state observed at time of execution.
        /// Populated when <see cref="ExecutionMode"/> is LiveProvider or ProtectedSandbox.
        /// </summary>
        public string? KycAmlReadinessState { get; set; }
    }

    // ── Sign-off evidence bundle ──────────────────────────────────────────────────

    /// <summary>
    /// A durable, release-backed sign-off evidence bundle for a compliance case.
    /// This bundle aggregates all provider-backed execution evidence for the case
    /// into a single artifact suitable for business-owner review, audit, and
    /// regulator-facing compliance demonstrations.
    /// </summary>
    public class ProviderBackedCaseSignOffEvidenceBundle
    {
        /// <summary>Unique identifier for this bundle.</summary>
        public string BundleId { get; set; } = string.Empty;

        /// <summary>The compliance case this bundle covers.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>UTC timestamp when the bundle was assembled.</summary>
        public DateTimeOffset BundledAt { get; set; }

        /// <summary>
        /// True when all included execution evidence was produced by a live or
        /// protected-sandbox provider. False if any evidence is simulated.
        /// </summary>
        public bool IsProviderBackedEvidence { get; set; }

        /// <summary>
        /// True when the bundle qualifies as release-grade evidence for product-owner sign-off.
        /// Requires: IsProviderBackedEvidence=true AND at least one Completed execution present.
        /// </summary>
        public bool IsReleaseGrade { get; set; }

        /// <summary>Optional release tag or label for connecting to the release head.</summary>
        public string? ReleaseTag { get; set; }

        /// <summary>
        /// SHA-256 content hash of the serialised execution history, for integrity verification.
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>Complete ordered execution history for this case.</summary>
        public List<ProviderBackedCaseExecutionEvidence> ExecutionHistory { get; set; } = new();

        /// <summary>The current state of the compliance case at bundle time.</summary>
        public string CurrentCaseState { get; set; } = string.Empty;

        /// <summary>The issuer ID associated with this compliance case.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>The subject ID associated with this compliance case.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>The type of compliance case (e.g., KYC, AML, KycAml).</summary>
        public string CaseType { get; set; } = string.Empty;

        /// <summary>
        /// Summary diagnostics at bundle time — non-null when configuration or provider
        /// issues were observed during the most recent execution.
        /// </summary>
        public ProviderBackedCaseExecutionDiagnostics? LatestDiagnostics { get; set; }
    }

    // ── Requests ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to execute a compliance case decision through the provider-backed execution path.
    /// </summary>
    public class ExecuteProviderBackedDecisionRequest
    {
        /// <summary>
        /// The kind of decision to execute (Approve, Reject, ReturnForInformation,
        /// SanctionsReview, or Escalate).
        /// </summary>
        public ProviderBackedCaseDecisionKind DecisionKind { get; set; }

        /// <summary>
        /// The execution mode to use. If omitted, defaults to <see cref="ProviderBackedCaseExecutionMode.Simulated"/>.
        /// </summary>
        public ProviderBackedCaseExecutionMode ExecutionMode { get; set; } = ProviderBackedCaseExecutionMode.Simulated;

        /// <summary>
        /// When true, execution will fail if the execution mode is Simulated.
        /// Use this for protected release paths where only provider-backed evidence
        /// is accepted.
        /// </summary>
        public bool RequireProviderBacked { get; set; }

        /// <summary>
        /// When true and execution mode is LiveProvider or ProtectedSandbox,
        /// execution will fail if KYC/AML sign-off evidence for the case subject
        /// is absent, stale, or not provider-backed.
        /// </summary>
        public bool RequireKycAmlSignOff { get; set; }

        /// <summary>
        /// Plain-text reason for the decision. Required for Reject, ReturnForInformation,
        /// and SanctionsReview decision kinds.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>Optional notes or reviewer context for the decision.</summary>
        public string? Notes { get; set; }

        /// <summary>Optional correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Optional additional metadata for the decision record.</summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Request to build a sign-off evidence bundle for a compliance case.
    /// </summary>
    public class BuildProviderBackedSignOffEvidenceRequest
    {
        /// <summary>
        /// When true, the bundle is rejected if any execution evidence in the history
        /// is from a simulated provider. Use for protected release paths.
        /// </summary>
        public bool RequireProviderBackedEvidence { get; set; }

        /// <summary>
        /// Optional release tag or label to embed in the bundle metadata,
        /// connecting the evidence to the release candidate being evaluated.
        /// </summary>
        public string? ReleaseTag { get; set; }

        /// <summary>Optional correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    // ── Responses ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Response from a provider-backed compliance case decision execution.
    /// </summary>
    public class ExecuteProviderBackedDecisionResponse
    {
        /// <summary>True when execution completed without errors.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error message if <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code if <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>The status of the execution attempt.</summary>
        public ProviderBackedCaseExecutionStatus Status { get; set; }

        /// <summary>The evidence artifact produced if execution was successful.</summary>
        public ProviderBackedCaseExecutionEvidence? Evidence { get; set; }

        /// <summary>
        /// Structured diagnostics. Populated regardless of success or failure.
        /// Use this to triage configuration or provider issues without log access.
        /// </summary>
        public ProviderBackedCaseExecutionDiagnostics? Diagnostics { get; set; }

        /// <summary>
        /// Plain-language description of the next action an operator should take.
        /// Non-null when <see cref="Success"/> is false.
        /// </summary>
        public string? NextAction { get; set; }

        /// <summary>Correlation ID echoed from the request.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Unique identifier for this execution attempt.</summary>
        public string? ExecutionId { get; set; }
    }

    /// <summary>
    /// Response for the execution status query of a compliance case.
    /// </summary>
    public class GetProviderBackedExecutionStatusResponse
    {
        /// <summary>True when the query succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error message if <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The compliance case ID being queried.</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>
        /// The aggregated execution status derived from the execution history.
        /// <see cref="ProviderBackedCaseExecutionStatus.NotStarted"/> if no executions exist.
        /// </summary>
        public ProviderBackedCaseExecutionStatus Status { get; set; }

        /// <summary>Most recent diagnostics, or null if no execution has been attempted.</summary>
        public ProviderBackedCaseExecutionDiagnostics? LatestDiagnostics { get; set; }

        /// <summary>Complete ordered execution history for this case.</summary>
        public List<ProviderBackedCaseExecutionEvidence> ExecutionHistory { get; set; } = new();

        /// <summary>
        /// True when the most recent completed execution produced release-grade evidence.
        /// </summary>
        public bool HasReleaseGradeEvidence { get; set; }

        /// <summary>Correlation ID for tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Response from a sign-off evidence bundle build request.
    /// </summary>
    public class BuildProviderBackedSignOffEvidenceResponse
    {
        /// <summary>True when the bundle was built without errors.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable error message if <see cref="Success"/> is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Machine-readable error code if <see cref="Success"/> is false.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>The assembled evidence bundle, or null if the build failed.</summary>
        public ProviderBackedCaseSignOffEvidenceBundle? Bundle { get; set; }

        /// <summary>Correlation ID echoed from the request.</summary>
        public string? CorrelationId { get; set; }
    }
}
