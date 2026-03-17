using System.Text.Json.Serialization;

namespace BiatecTokensApi.Models.ProtectedSignOff
{
    // ── Enumerations ──────────────────────────────────────────────────────────────

    /// <summary>Overall readiness status of the protected sign-off environment.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProtectedEnvironmentStatus
    {
        /// <summary>All required services and configuration are available; the environment is ready for a protected sign-off run.</summary>
        Ready,
        /// <summary>One or more non-critical checks failed; the environment can still attempt a sign-off run with reduced confidence.</summary>
        Degraded,
        /// <summary>One or more critical checks failed; the environment cannot support a protected sign-off run.</summary>
        Unavailable,
        /// <summary>Required configuration values are absent or invalid; the environment must be reconfigured before use.</summary>
        Misconfigured
    }

    /// <summary>Category of an individual environment readiness check.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EnvironmentCheckCategory
    {
        /// <summary>Required configuration key presence and validity (e.g., JWT secret, App:Account mnemonic).</summary>
        Configuration,
        /// <summary>Authentication infrastructure including JWT and ARC76 derivation.</summary>
        Authentication,
        /// <summary>Issuer workflow service and team role infrastructure.</summary>
        IssuerWorkflow,
        /// <summary>Deployment lifecycle and contract services.</summary>
        DeploymentLifecycle,
        /// <summary>Sign-off validation and proof-generation services.</summary>
        SignOffValidation,
        /// <summary>Observability infrastructure including logging and correlation IDs.</summary>
        Observability,
        /// <summary>Enterprise fixture provisioning and seed data.</summary>
        EnterpriseFixtures,
        /// <summary>KYC/AML sign-off evidence and compliance case management services required for the enterprise onboarding workflow.</summary>
        ComplianceWorkflow
    }

    /// <summary>Outcome of a single environment readiness check.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EnvironmentCheckOutcome
    {
        /// <summary>Check passed; this aspect of the environment is operational.</summary>
        Pass,
        /// <summary>Check failed with a critical impact; the environment cannot run a protected sign-off.</summary>
        CriticalFail,
        /// <summary>Check failed with a non-critical impact; degraded operation is possible.</summary>
        DegradedFail,
        /// <summary>Check was skipped because a prerequisite check failed.</summary>
        Skipped
    }

    /// <summary>Stage within the enterprise sign-off lifecycle journey.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SignOffLifecycleStage
    {
        /// <summary>Authentication: verify the sign-off identity can authenticate successfully.</summary>
        Authentication,
        /// <summary>Initiation: verify the backend can accept and record a workflow initiation request.</summary>
        Initiation,
        /// <summary>StatusPolling: verify status polling returns deterministic lifecycle state.</summary>
        StatusPolling,
        /// <summary>TerminalState: verify a terminal state (Completed/Failed) is reached and reported correctly.</summary>
        TerminalState,
        /// <summary>Validation: verify the sign-off validation endpoint returns a structured proof document.</summary>
        Validation,
        /// <summary>Complete: all stages passed; the lifecycle journey is ready for protected sign-off evidence.</summary>
        Complete
    }

    /// <summary>Outcome of a single lifecycle stage verification.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LifecycleStageOutcome
    {
        /// <summary>Stage verified successfully.</summary>
        Verified,
        /// <summary>Stage verification failed; the lifecycle journey cannot proceed.</summary>
        Failed,
        /// <summary>Stage was skipped because an earlier stage failed.</summary>
        Skipped
    }

    // ── Environment Check Models ───────────────────────────────────────────────

    /// <summary>Result of a single protected environment readiness check.</summary>
    public class EnvironmentCheck
    {
        /// <summary>Short machine-readable name for this check (e.g., "AuthServiceAvailable").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Category this check belongs to.</summary>
        public EnvironmentCheckCategory Category { get; set; }

        /// <summary>Outcome of the check.</summary>
        public EnvironmentCheckOutcome Outcome { get; set; }

        /// <summary>Human-readable description of what was checked.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Detail about the outcome.
        /// On pass, describes what was verified.
        /// On fail, describes what is missing or broken.
        /// </summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>
        /// Actionable guidance for operators to resolve this check when it fails.
        /// Present when <see cref="Outcome"/> is not <see cref="EnvironmentCheckOutcome.Pass"/>.
        /// </summary>
        public string? OperatorGuidance { get; set; }

        /// <summary>Whether this check is required for a protected sign-off run.</summary>
        public bool IsRequired { get; set; } = true;
    }

    /// <summary>Request to verify the protected sign-off environment is ready.</summary>
    public class ProtectedSignOffEnvironmentRequest
    {
        /// <summary>Caller-supplied correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Whether to include required configuration guard validation in the environment check.
        /// Defaults to <c>true</c>. When <c>true</c>, the check validates that all required
        /// configuration keys (JWT secret, App mnemonic, etc.) are present and non-empty.
        /// </summary>
        public bool IncludeConfigCheck { get; set; } = true;

        /// <summary>
        /// Whether to include enterprise fixture validation in the environment check.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool IncludeFixtureCheck { get; set; } = true;

        /// <summary>
        /// Whether to include observability checks (logging and correlation ID infrastructure).
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool IncludeObservabilityCheck { get; set; } = true;

        /// <summary>
        /// Whether to include compliance workflow service health checks.
        /// When <c>true</c>, verifies that the KYC/AML sign-off evidence service and
        /// compliance case management service are available and wired up correctly.
        /// This check validates the backend compliance capabilities aligned with the
        /// enterprise onboarding workflow.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool IncludeComplianceWorkflowCheck { get; set; } = true;
    }

    /// <summary>
    /// Response from the protected sign-off environment readiness check.
    ///
    /// <para>
    /// When <see cref="Status"/> is <see cref="ProtectedEnvironmentStatus.Ready"/>,
    /// all required checks passed and the backend is prepared for a protected sign-off run.
    /// Any other status indicates that remediation is needed; consult <see cref="Checks"/>
    /// and <see cref="ActionableGuidance"/> for specifics.
    /// </para>
    /// </summary>
    public class ProtectedSignOffEnvironmentResponse
    {
        /// <summary>Unique identifier for this environment check response.</summary>
        public string CheckId { get; set; } = string.Empty;

        /// <summary>Correlation ID propagated from the request.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Overall environment readiness status.</summary>
        public ProtectedEnvironmentStatus Status { get; set; }

        /// <summary>Whether the environment is fully ready for a protected sign-off run.</summary>
        public bool IsReadyForProtectedRun { get; set; }

        /// <summary>Number of checks that passed.</summary>
        public int PassedCheckCount { get; set; }

        /// <summary>Number of critical checks that failed (blocks the protected run when > 0).</summary>
        public int CriticalFailCount { get; set; }

        /// <summary>Number of non-critical checks that failed (causes degraded mode).</summary>
        public int DegradedFailCount { get; set; }

        /// <summary>Total number of checks performed.</summary>
        public int TotalCheckCount { get; set; }

        /// <summary>Ordered list of individual environment check results.</summary>
        public List<EnvironmentCheck> Checks { get; set; } = new();

        /// <summary>
        /// Concise, non-technical guidance summarising what an operator must do
        /// before running the protected sign-off workflow.
        /// Present when <see cref="IsReadyForProtectedRun"/> is <c>false</c>.
        /// </summary>
        public string? ActionableGuidance { get; set; }

        /// <summary>ISO 8601 timestamp when this check was performed.</summary>
        public string CheckedAt { get; set; } = string.Empty;

        /// <summary>Version of the sign-off environment contract evaluated.</summary>
        public string ContractVersion { get; set; } = "1.0";
    }

    // ── Lifecycle Journey Models ───────────────────────────────────────────────

    /// <summary>Single stage result within the enterprise sign-off lifecycle journey.</summary>
    public class SignOffLifecycleTransition
    {
        /// <summary>Lifecycle stage being verified.</summary>
        public SignOffLifecycleStage Stage { get; set; }

        /// <summary>Outcome of this stage verification.</summary>
        public LifecycleStageOutcome Outcome { get; set; }

        /// <summary>Human-readable description of what this stage verifies.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Detail about the stage outcome.
        /// On success, describes the evidence collected.
        /// On failure, describes what blocked the stage.
        /// </summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>
        /// Actionable guidance for a non-technical operator when this stage fails.
        /// Present when <see cref="Outcome"/> is <see cref="LifecycleStageOutcome.Failed"/>.
        /// </summary>
        public string? UserGuidance { get; set; }

        /// <summary>ISO 8601 timestamp when this stage was verified.</summary>
        public string VerifiedAt { get; set; } = string.Empty;
    }

    /// <summary>Request to execute the enterprise sign-off lifecycle journey verification.</summary>
    public class EnterpriseSignOffLifecycleRequest
    {
        /// <summary>
        /// Issuer identifier to use for the lifecycle journey.
        /// If not supplied, the default protected sign-off issuer is used.
        /// </summary>
        public string? IssuerId { get; set; }

        /// <summary>
        /// Deployment identifier to use for sign-off validation.
        /// If not supplied, a deterministic test deployment fixture is used.
        /// </summary>
        public string? DeploymentId { get; set; }

        /// <summary>Caller-supplied correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Whether to include the fixture provisioning step before executing the lifecycle.
        /// Defaults to <c>false</c>; set to <c>true</c> to ensure fixtures are present.
        /// </summary>
        public bool ProvisionFixturesIfAbsent { get; set; } = false;
    }

    /// <summary>
    /// Response from the enterprise sign-off lifecycle journey verification.
    ///
    /// <para>
    /// When <see cref="IsLifecycleVerified"/> is <c>true</c>, every required stage of the
    /// protected sign-off journey returned deterministic, contract-stable responses.
    /// This response can be cited as evidence that the backend is ready for a protected run.
    /// </para>
    /// </summary>
    public class EnterpriseSignOffLifecycleResponse
    {
        /// <summary>Unique identifier for this lifecycle verification response.</summary>
        public string VerificationId { get; set; } = string.Empty;

        /// <summary>Correlation ID propagated from the request.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Whether all lifecycle stages were verified successfully.
        /// <c>true</c> means the backend is ready to support a protected sign-off run.
        /// </summary>
        public bool IsLifecycleVerified { get; set; }

        /// <summary>The last lifecycle stage that completed verification.</summary>
        public SignOffLifecycleStage ReachedStage { get; set; }

        /// <summary>Number of lifecycle stages verified.</summary>
        public int VerifiedStageCount { get; set; }

        /// <summary>Number of lifecycle stages that failed.</summary>
        public int FailedStageCount { get; set; }

        /// <summary>Ordered list of stage-by-stage verification results.</summary>
        public List<SignOffLifecycleTransition> Stages { get; set; } = new();

        /// <summary>
        /// Actionable guidance for resolving lifecycle verification failures.
        /// Present when <see cref="IsLifecycleVerified"/> is <c>false</c>.
        /// </summary>
        public string? ActionableGuidance { get; set; }

        /// <summary>ISO 8601 timestamp when the lifecycle verification was executed.</summary>
        public string ExecutedAt { get; set; } = string.Empty;

        /// <summary>The issuer ID used during the lifecycle journey.</summary>
        public string? IssuerId { get; set; }

        /// <summary>The deployment ID used for sign-off validation.</summary>
        public string? DeploymentId { get; set; }
    }

    // ── Enterprise Fixture Models ─────────────────────────────────────────────

    /// <summary>Request to provision enterprise sign-off fixtures (issuer and team data).</summary>
    public class EnterpriseFixtureProvisionRequest
    {
        /// <summary>
        /// Issuer ID to use for the provisioned fixtures.
        /// If not supplied, the default protected sign-off issuer ID is used.
        /// </summary>
        public string? IssuerId { get; set; }

        /// <summary>
        /// Sign-off user ID (actor) to provision as the issuer admin.
        /// If not supplied, the default sign-off test identity is used.
        /// </summary>
        public string? SignOffUserId { get; set; }

        /// <summary>Caller-supplied correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Whether to reset (clear and re-provision) fixtures if they already exist.
        /// Defaults to <c>false</c>; existing fixtures are left intact.
        /// </summary>
        public bool ResetIfExists { get; set; } = false;
    }

    /// <summary>Response from enterprise sign-off fixture provisioning.</summary>
    public class EnterpriseFixtureProvisionResponse
    {
        /// <summary>Unique identifier for this provisioning response.</summary>
        public string ProvisioningId { get; set; } = string.Empty;

        /// <summary>Correlation ID propagated from the request.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Whether fixtures were provisioned successfully.</summary>
        public bool IsProvisioned { get; set; }

        /// <summary>Whether fixtures already existed and were left intact (no reset).</summary>
        public bool WasAlreadyProvisioned { get; set; }

        /// <summary>The issuer ID under which fixtures are provisioned.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>The admin user ID provisioned as issuer team admin.</summary>
        public string AdminUserId { get; set; } = string.Empty;

        /// <summary>
        /// Machine-readable error code when provisioning fails.
        /// <c>null</c> on success.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Human-readable error detail when provisioning fails.
        /// <c>null</c> on success.
        /// </summary>
        public string? ErrorDetail { get; set; }

        /// <summary>
        /// Actionable guidance to resolve provisioning failures.
        /// Present when <see cref="IsProvisioned"/> is <c>false</c>.
        /// </summary>
        public string? OperatorGuidance { get; set; }

        /// <summary>ISO 8601 timestamp when provisioning was attempted.</summary>
        public string ProvisionedAt { get; set; } = string.Empty;
    }

    // ── Diagnostics Models ────────────────────────────────────────────────────

    /// <summary>
    /// Backend diagnostics report for a protected sign-off run.
    ///
    /// <para>
    /// Provides enough structured information for a product or operations team to
    /// distinguish configuration failures, authorization failures, contract failures,
    /// and lifecycle failures without exposing secrets.
    /// </para>
    /// </summary>
    public class ProtectedSignOffDiagnosticsResponse
    {
        /// <summary>Unique identifier for this diagnostics report.</summary>
        public string DiagnosticsId { get; set; } = string.Empty;

        /// <summary>Correlation ID propagated from the request or auto-generated.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Whether the backend is in a state that supports protected sign-off runs.</summary>
        public bool IsOperational { get; set; }

        /// <summary>Summary of which failure categories were detected.</summary>
        public FailureCategorySummary FailureCategories { get; set; } = new();

        /// <summary>Service availability diagnostics for each sign-off dependency.</summary>
        public List<ServiceAvailabilityDiagnostic> ServiceAvailability { get; set; } = new();

        /// <summary>
        /// Ordered list of actionable diagnostic notes.
        /// Each note corresponds to a specific finding and includes remediation guidance.
        /// </summary>
        public List<DiagnosticNote> Notes { get; set; } = new();

        /// <summary>ISO 8601 timestamp when diagnostics were gathered.</summary>
        public string GeneratedAt { get; set; } = string.Empty;

        /// <summary>Backend API version used to generate this report.</summary>
        public string ApiVersion { get; set; } = "v1";
    }

    /// <summary>Summary of which failure categories were detected during diagnostics.</summary>
    public class FailureCategorySummary
    {
        /// <summary>Whether any configuration failures were detected (e.g., missing required settings).</summary>
        public bool HasConfigurationFailure { get; set; }

        /// <summary>Whether any authorization failures were detected (e.g., missing issuer role).</summary>
        public bool HasAuthorizationFailure { get; set; }

        /// <summary>Whether any contract failures were detected (e.g., unexpected response shape).</summary>
        public bool HasContractFailure { get; set; }

        /// <summary>Whether any lifecycle failures were detected (e.g., invalid state transition).</summary>
        public bool HasLifecycleFailure { get; set; }

        /// <summary>Whether any service availability failures were detected.</summary>
        public bool HasServiceAvailabilityFailure { get; set; }
    }

    /// <summary>Availability diagnostic for a single backend service dependency.</summary>
    public class ServiceAvailabilityDiagnostic
    {
        /// <summary>Short machine-readable service name (e.g., "IssuerWorkflowService").</summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>Whether the service is available and operational.</summary>
        public bool IsAvailable { get; set; }

        /// <summary>Human-readable status detail.</summary>
        public string StatusDetail { get; set; } = string.Empty;
    }

    /// <summary>A single actionable diagnostic note produced during the environment check.</summary>
    public class DiagnosticNote
    {
        /// <summary>Failure category this note relates to.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Human-readable note describing the finding.</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>
        /// Actionable remediation guidance for operators.
        /// Does not expose secrets or internal implementation details.
        /// </summary>
        public string Remediation { get; set; } = string.Empty;

        /// <summary>Whether this note represents a blocking issue.</summary>
        public bool IsBlocking { get; set; }
    }
}
