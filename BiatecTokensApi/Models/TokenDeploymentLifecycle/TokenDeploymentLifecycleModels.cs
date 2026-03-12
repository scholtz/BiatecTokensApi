using System.Text.Json.Serialization;

namespace BiatecTokensApi.Models.TokenDeploymentLifecycle
{
    // ── Enumerations ─────────────────────────────────────────────────────────────

    /// <summary>Current stage in the token deployment lifecycle.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DeploymentStage
    {
        /// <summary>Deployment is being initialised; inputs are being validated.</summary>
        Initialising,
        /// <summary>Input validation is running; guardrails are being evaluated.</summary>
        Validating,
        /// <summary>Deployment transaction has been submitted to the network.</summary>
        Submitting,
        /// <summary>Waiting for on-chain confirmation of the deployment transaction.</summary>
        Confirming,
        /// <summary>Deployment completed successfully; asset is live on-chain.</summary>
        Completed,
        /// <summary>Deployment failed; see error details for remediation guidance.</summary>
        Failed,
        /// <summary>Deployment was cancelled by the caller before submission.</summary>
        Cancelled
    }

    /// <summary>Idempotency state of a deployment request.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum IdempotencyStatus
    {
        /// <summary>First-time request; no prior record found.</summary>
        New,
        /// <summary>Duplicate request; an identical prior request exists and is being returned.</summary>
        Duplicate,
        /// <summary>Conflicting request; idempotency key reused with different parameters.</summary>
        Conflict,
        /// <summary>Prior request is still in progress; this call is attached to the in-flight operation.</summary>
        InProgress
    }

    /// <summary>Category of a telemetry lifecycle event.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TelemetryEventType
    {
        /// <summary>A deployment stage transition occurred.</summary>
        StageTransition,
        /// <summary>A validation error was detected.</summary>
        ValidationError,
        /// <summary>A guardrail check produced a warning or failure.</summary>
        GuardrailTriggered,
        /// <summary>An idempotency cache hit was detected.</summary>
        IdempotencyHit,
        /// <summary>A retry was attempted for a previously failed deployment.</summary>
        RetryAttempt,
        /// <summary>A dependency (IPFS, blockchain node) responded with a transient error.</summary>
        DependencyFailure,
        /// <summary>Deployment completed successfully.</summary>
        CompletionSuccess,
        /// <summary>Deployment terminated with a terminal failure.</summary>
        TerminalFailure
    }

    /// <summary>Severity level of a reliability guardrail finding.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GuardrailSeverity
    {
        /// <summary>Informational note; no action required.</summary>
        Info,
        /// <summary>Warning: potential issue detected; deployment can still proceed.</summary>
        Warning,
        /// <summary>Error: invariant violated; deployment must not proceed.</summary>
        Error
    }

    /// <summary>Overall outcome category of a deployment attempt.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DeploymentOutcome
    {
        /// <summary>Deployment succeeded; asset is live.</summary>
        Success,
        /// <summary>Deployment partially completed; some steps succeeded.</summary>
        PartialSuccess,
        /// <summary>Deployment failed due to a transient error; retry is advised.</summary>
        TransientFailure,
        /// <summary>Deployment failed due to a terminal error; retry will not help without remediation.</summary>
        TerminalFailure,
        /// <summary>Outcome is not yet known; deployment is still in progress.</summary>
        Unknown
    }

    /// <summary>
    /// Controls whether the deployment lifecycle service requires authoritative blockchain
    /// evidence or accepts deterministic simulation evidence.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DeploymentExecutionMode
    {
        /// <summary>
        /// The service requires authoritative blockchain evidence. If the evidence provider
        /// cannot supply confirmed on-chain data (e.g. no live node available), the deployment
        /// transitions to <see cref="DeploymentStage.Failed"/> with
        /// <see cref="DeploymentOutcome.TerminalFailure"/> and error code
        /// <c>BLOCKCHAIN_EVIDENCE_UNAVAILABLE</c>. This mode is required for production-grade
        /// sign-off and enterprise regulated issuance.
        /// </summary>
        Authoritative,

        /// <summary>
        /// The service accepts deterministic simulation evidence when authoritative data is
        /// unavailable. Blockchain fields (<c>AssetId</c>, <c>TransactionId</c>,
        /// <c>ConfirmedRound</c>) are derived from the deployment ID hash.
        /// <see cref="TokenDeploymentLifecycleResponse.IsSimulatedEvidence"/> is set to
        /// <c>true</c> on all simulated completions. This mode is acceptable for contract-shape
        /// validation, development, and test environments.
        /// </summary>
        Simulation
    }

    /// <summary>
    /// Granular status of deployment evidence availability.
    /// Used by downstream consumers (frontend, compliance dashboards, audit tools) to
    /// determine the trustworthiness of reported blockchain values.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LifecycleEvidenceStatus
    {
        /// <summary>
        /// Evidence is confirmed authoritative on-chain data retrieved from a live blockchain
        /// node or indexer. <c>AssetId</c>, <c>TransactionId</c>, and <c>ConfirmedRound</c>
        /// can be relied upon as production-grade proof.
        /// </summary>
        Confirmed,

        /// <summary>
        /// Evidence is pending; the blockchain indexer or node has not yet reported the
        /// transaction. This is a transient state during indexer lag. Callers should poll
        /// the status endpoint and retry after a short delay.
        /// </summary>
        Pending,

        /// <summary>
        /// Evidence is deterministically simulated from the deployment ID hash. Not
        /// blockchain-confirmed. Suitable for development and contract-shape testing only.
        /// </summary>
        Simulated,

        /// <summary>
        /// Evidence is stale; it was retrieved previously but the indexer has since reported
        /// a newer state (e.g. the asset was destroyed after creation). Callers should
        /// re-query for current status.
        /// </summary>
        Stale,

        /// <summary>
        /// Evidence is unavailable. The provider could not be reached or returned no data.
        /// In <see cref="DeploymentExecutionMode.Authoritative"/> mode this causes a
        /// terminal failure with error code <c>BLOCKCHAIN_EVIDENCE_UNAVAILABLE</c>.
        /// </summary>
        Unavailable,

        /// <summary>
        /// Evidence was returned by the provider but could not be validated or parsed.
        /// Indicates a malformed response from the upstream blockchain node or indexer.
        /// </summary>
        Invalid
    }

    /// <summary>
    /// Structured error category that classifies the root cause of a deployment failure.
    /// Used together with error codes and remediation hints so downstream consumers can
    /// decide whether to retry, escalate, or remediate.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LifecycleErrorCategory
    {
        /// <summary>Input validation failed; the request contains invalid or missing fields.</summary>
        ValidationFailure,

        /// <summary>
        /// Authoritative blockchain evidence could not be obtained. The provider returned
        /// null or threw a non-transient exception.
        /// </summary>
        EvidenceUnavailable,

        /// <summary>
        /// The evidence provider request timed out. This is a transient failure; the
        /// deployment may be retried after a suitable delay.
        /// </summary>
        NetworkTimeout,

        /// <summary>
        /// The evidence provider returned a response that could not be parsed or validated.
        /// Indicates a malformed upstream response.
        /// </summary>
        MalformedProviderResponse,

        /// <summary>
        /// The target network is not supported by the configured evidence provider.
        /// Requires operator configuration changes before retry.
        /// </summary>
        UnsupportedNetwork,

        /// <summary>
        /// A reliability guardrail check blocked the deployment from proceeding.
        /// The guardrail findings in the response contain remediation guidance.
        /// </summary>
        GuardrailViolation,

        /// <summary>
        /// The evidence provider threw an unexpected exception that is not classified as a
        /// timeout or malformed-response error. Requires investigation.
        /// </summary>
        ProviderException,

        /// <summary>
        /// An idempotency key was reused with different deployment parameters (conflict).
        /// </summary>
        IdempotencyConflict,

        /// <summary>
        /// An unexpected internal error occurred. Not categorised into a more specific class.
        /// </summary>
        InternalError
    }

    // ── Request / Response models ─────────────────────────────────────────────

    /// <summary>
    /// Request to initiate or resume a deterministic token deployment lifecycle.
    /// All fields participate in idempotency key generation; identical inputs
    /// always produce the same lifecycle result.
    /// </summary>
    public class TokenDeploymentLifecycleRequest
    {
        /// <summary>
        /// Idempotency key supplied by the caller.
        /// If omitted, a key is derived from the deployment parameters.
        /// Resubmitting the same key returns the cached result without re-deploying.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>Caller-supplied correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Token standard to deploy (e.g., "ASA", "ARC3", "ARC200", "ERC20").
        /// Required.
        /// </summary>
        public string TokenStandard { get; set; } = string.Empty;

        /// <summary>Token name (1–64 characters).</summary>
        public string TokenName { get; set; } = string.Empty;

        /// <summary>Token symbol / unit name (1–8 characters).</summary>
        public string TokenSymbol { get; set; } = string.Empty;

        /// <summary>Target network identifier (e.g., "algorand-mainnet", "base-mainnet").</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Total token supply. Must be greater than zero.</summary>
        public ulong TotalSupply { get; set; }

        /// <summary>Number of decimal places (0–19).</summary>
        public int Decimals { get; set; }

        /// <summary>Address of the token creator / manager.</summary>
        public string CreatorAddress { get; set; } = string.Empty;

        /// <summary>Optional metadata URI (IPFS, HTTP).</summary>
        public string? MetadataUri { get; set; }

        /// <summary>Whether to enable freeze authority on the token.</summary>
        public bool EnableFreeze { get; set; }

        /// <summary>Whether to enable clawback authority on the token.</summary>
        public bool EnableClawback { get; set; }

        /// <summary>Maximum number of retry attempts before the deployment is considered terminal.</summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>Timeout for the entire deployment pipeline in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 120;

        /// <summary>
        /// Controls whether the deployment requires authoritative blockchain evidence or accepts
        /// deterministic simulation evidence. Defaults to <see cref="DeploymentExecutionMode.Authoritative"/>.
        ///
        /// In <see cref="DeploymentExecutionMode.Authoritative"/> mode the service fails with
        /// <c>BLOCKCHAIN_EVIDENCE_UNAVAILABLE</c> if confirmed on-chain data cannot be obtained,
        /// making it suitable for production regulated issuance sign-off.
        ///
        /// In <see cref="DeploymentExecutionMode.Simulation"/> mode the service uses
        /// deterministic hash-derived values and sets
        /// <see cref="TokenDeploymentLifecycleResponse.IsSimulatedEvidence"/> = <c>true</c>;
        /// suitable for development, contract-shape testing, and non-production environments.
        /// </summary>
        public DeploymentExecutionMode ExecutionMode { get; set; } = DeploymentExecutionMode.Authoritative;
    }

    /// <summary>
    /// Response from a token deployment lifecycle initiation or status query.
    /// Contains the current stage, idempotency metadata, telemetry, and reliability signals.
    /// </summary>
    public class TokenDeploymentLifecycleResponse
    {
        /// <summary>Unique identifier for this deployment attempt.</summary>
        public string DeploymentId { get; set; } = string.Empty;

        /// <summary>Idempotency key associated with this deployment.</summary>
        public string IdempotencyKey { get; set; } = string.Empty;

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Current stage of the deployment lifecycle.</summary>
        public DeploymentStage Stage { get; set; }

        /// <summary>Overall outcome (only meaningful when Stage is Completed or Failed).</summary>
        public DeploymentOutcome Outcome { get; set; }

        /// <summary>Whether the response was served from an idempotency cache.</summary>
        public bool IsIdempotentReplay { get; set; }

        /// <summary>Idempotency status for this request.</summary>
        public IdempotencyStatus IdempotencyStatus { get; set; }

        /// <summary>Human-readable message describing the current state.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>On-chain asset ID (populated when Stage is Completed).</summary>
        public ulong? AssetId { get; set; }

        /// <summary>On-chain transaction ID (populated when Stage is Submitting or later).</summary>
        public string? TransactionId { get; set; }

        /// <summary>Confirmed round / block number (populated when Stage is Confirming or Completed).</summary>
        public ulong? ConfirmedRound { get; set; }

        /// <summary>Number of retry attempts consumed so far.</summary>
        public int RetryCount { get; set; }

        /// <summary>Whether the deployment is in a degraded state (some checks may be incomplete).</summary>
        public bool IsDegraded { get; set; }

        /// <summary>Validation results from pre-deployment input checks.</summary>
        public List<DeploymentValidationResult> ValidationResults { get; set; } = new();

        /// <summary>Reliability guardrail findings from pre-deployment safety checks.</summary>
        public List<ReliabilityGuardrail> GuardrailFindings { get; set; } = new();

        /// <summary>Telemetry events emitted during this deployment lifecycle.</summary>
        public List<DeploymentTelemetryEvent> TelemetryEvents { get; set; } = new();

        /// <summary>Progress snapshot for the current deployment attempt.</summary>
        public DeploymentProgress Progress { get; set; } = new();

        /// <summary>Schema version for forward compatibility.</summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>Timestamp when the deployment was initiated.</summary>
        public DateTimeOffset InitiatedAt { get; set; }

        /// <summary>Timestamp of the most recent state transition.</summary>
        public DateTimeOffset LastUpdatedAt { get; set; }

        /// <summary>Human-readable remediation hint when Outcome is a failure category.</summary>
        public string? RemediationHint { get; set; }

        /// <summary>
        /// Indicates that the blockchain-side evidence fields (<see cref="AssetId"/>,
        /// <see cref="TransactionId"/>, <see cref="ConfirmedRound"/>) are derived
        /// deterministically from the deployment ID rather than obtained from a live blockchain
        /// node. When <c>true</c>, these values are deterministic placeholders suitable for
        /// contract-shape validation and sign-off workflow testing, but they do NOT represent
        /// confirmed on-chain state.
        ///
        /// A value of <c>false</c> means the evidence was obtained from a real blockchain
        /// confirmation and can be used as authoritative proof of deployment.
        ///
        /// Sign-off environments and frontend consumers MUST check this flag before treating
        /// <see cref="AssetId"/> or <see cref="TransactionId"/> as production-valid evidence.
        /// </summary>
        public bool IsSimulatedEvidence { get; set; }

        /// <summary>
        /// Human-readable description of where the deployment evidence originated.
        /// Provides traceability for operators, audit tools, and downstream dashboards.
        ///
        /// Representative values:
        /// <list type="bullet">
        ///   <item><c>"algorand-indexer"</c> — evidence retrieved from a live Algorand
        ///     indexer node; authoritative on-chain state.</item>
        ///   <item><c>"simulation"</c> — evidence derived deterministically from the
        ///     deployment ID hash; not blockchain-confirmed.</item>
        ///   <item><c>"unavailable"</c> — no evidence could be obtained; set on failure
        ///     responses when authoritative mode is requested.</item>
        /// </list>
        /// </summary>
        public string? EvidenceProvenance { get; set; }

        /// <summary>
        /// Structured evidence availability detail. Provides fine-grained status of the
        /// deployment evidence so downstream consumers can distinguish pending, confirmed,
        /// simulated, stale, unavailable, and invalid states without parsing
        /// <see cref="IsSimulatedEvidence"/> and <see cref="EvidenceProvenance"/> heuristically.
        ///
        /// <para>This field is always populated on responses from the lifecycle service.</para>
        /// </summary>
        public EvidenceAvailabilityDetail EvidenceAvailability { get; set; } = new();

        /// <summary>
        /// Structured error detail when <see cref="Outcome"/> is a failure category.
        /// Null on successful responses.
        /// </summary>
        public LifecycleErrorDetail? ErrorDetail { get; set; }

        /// <summary>
        /// Context describing the evidence provider that was used for this deployment.
        /// Supports multi-chain extensibility: consumers can determine which provider
        /// (Algorand, EVM, Simulation, etc.) produced the evidence without inspecting
        /// provider-specific fields.
        /// </summary>
        public LifecycleProviderContext ProviderContext { get; set; } = new();
    }

    /// <summary>
    /// Structured description of deployment evidence availability.
    /// Enables downstream consumers (frontend, compliance dashboards, audit tools) to
    /// reliably distinguish pending, confirmed, simulated, stale, unavailable, and invalid
    /// evidence without heuristic string parsing.
    /// </summary>
    public class EvidenceAvailabilityDetail
    {
        /// <summary>
        /// Machine-readable evidence status. See <see cref="LifecycleEvidenceStatus"/> for
        /// semantics.
        /// </summary>
        public LifecycleEvidenceStatus Status { get; set; } = LifecycleEvidenceStatus.Unavailable;

        /// <summary>
        /// Machine-readable reason code for the current status. Examples:
        /// <c>CONFIRMED_AUTHORITATIVE</c>, <c>SIMULATED_HASH_DERIVED</c>,
        /// <c>PROVIDER_NULL_RETURN</c>, <c>PROVIDER_TIMEOUT</c>,
        /// <c>PROVIDER_EXCEPTION</c>, <c>MALFORMED_RESPONSE</c>.
        /// </summary>
        public string ReasonCode { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable explanation of the evidence status. Suitable for audit logs and
        /// operator dashboards.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Whether the evidence failure is transient and the deployment may be retried.
        /// <c>false</c> means the failure is terminal and requires corrective action.
        /// Only meaningful when <see cref="Status"/> is <see cref="LifecycleEvidenceStatus.Unavailable"/>
        /// or <see cref="LifecycleEvidenceStatus.Invalid"/>.
        /// </summary>
        public bool IsRetriable { get; set; }

        /// <summary>
        /// Timestamp when the evidence was retrieved from the provider (UTC).
        /// Null if evidence could not be obtained.
        /// </summary>
        public DateTimeOffset? RetrievedAt { get; set; }

        /// <summary>
        /// Sanitised diagnostic detail from the provider. Empty on success.
        /// Useful for operational debugging without exposing raw exception messages.
        /// </summary>
        public string DiagnosticDetail { get; set; } = string.Empty;
    }

    /// <summary>
    /// Structured error detail for failed lifecycle operations.
    /// Provides machine-readable error classification plus human-readable remediation
    /// so frontend and compliance tooling can surface actionable messages.
    /// </summary>
    public class LifecycleErrorDetail
    {
        /// <summary>Machine-readable error code (e.g. <c>BLOCKCHAIN_EVIDENCE_UNAVAILABLE</c>).</summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>Structured error category for programmatic handling.</summary>
        public LifecycleErrorCategory ErrorCategory { get; set; }

        /// <summary>
        /// Whether the failure is transient and the deployment may be retried after a delay.
        /// <c>false</c> means the error requires corrective action before retry.
        /// </summary>
        public bool IsRetriable { get; set; }

        /// <summary>Human-readable remediation guidance for operators and enterprise issuers.</summary>
        public string RemediationHint { get; set; } = string.Empty;

        /// <summary>
        /// Sanitised diagnostic context (key-value pairs) for operational debugging.
        /// Values are sanitised to prevent log injection.
        /// </summary>
        public Dictionary<string, string> DiagnosticContext { get; set; } = new();
    }

    /// <summary>
    /// Context about the evidence provider used for a deployment.
    /// Enables provider-neutral lifecycle contracts so future EVM or other network
    /// evidence providers plug into the same contract without breaking fail-closed semantics.
    /// </summary>
    public class LifecycleProviderContext
    {
        /// <summary>
        /// Name of the active evidence provider family.
        /// Representative values: <c>"Algorand"</c>, <c>"EVM"</c>, <c>"Simulation"</c>,
        /// <c>"Unavailable"</c>.
        /// </summary>
        public string ProviderFamily { get; set; } = string.Empty;

        /// <summary>
        /// Provider-specific source identifier (e.g. indexer endpoint, network name).
        /// May be empty if the provider did not supply one.
        /// </summary>
        public string ProviderSource { get; set; } = string.Empty;

        /// <summary>
        /// Whether the provider is operating in a simulation (non-blockchain) mode.
        /// Mirrors <see cref="TokenDeploymentLifecycleResponse.IsSimulatedEvidence"/> for
        /// provider-oriented consumers.
        /// </summary>
        public bool IsSimulated { get; set; }

        /// <summary>
        /// Whether the provider is configured and reachable.
        /// <c>false</c> means no live provider is available; failures will be terminal in
        /// <see cref="DeploymentExecutionMode.Authoritative"/> mode.
        /// </summary>
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// Result of a single pre-deployment input validation rule.
    /// </summary>
    public class DeploymentValidationResult
    {
        /// <summary>Machine-readable field or rule name being validated.</summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>Whether this validation check passed.</summary>
        public bool IsValid { get; set; }

        /// <summary>Machine-readable error code (empty when IsValid is true).</summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>Human-readable description of the validation finding.</summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Reliability guardrail finding emitted before or during a deployment.
    /// </summary>
    public class ReliabilityGuardrail
    {
        /// <summary>Machine-readable identifier for this guardrail rule.</summary>
        public string GuardrailId { get; set; } = string.Empty;

        /// <summary>Severity of the finding.</summary>
        public GuardrailSeverity Severity { get; set; }

        /// <summary>Human-readable description of the finding.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Whether this finding blocks deployment from proceeding.</summary>
        public bool IsBlocking { get; set; }

        /// <summary>Suggested remediation action.</summary>
        public string RemediationHint { get; set; } = string.Empty;
    }

    /// <summary>
    /// A single telemetry event emitted at a specific point in the deployment lifecycle.
    /// Carries correlation ID and contextual metadata for operational diagnostics.
    /// </summary>
    public class DeploymentTelemetryEvent
    {
        /// <summary>Unique event ID.</summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>Deployment ID this event belongs to.</summary>
        public string DeploymentId { get; set; } = string.Empty;

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Type of lifecycle event.</summary>
        public TelemetryEventType EventType { get; set; }

        /// <summary>Deployment stage at the time the event was emitted.</summary>
        public DeploymentStage Stage { get; set; }

        /// <summary>Human-readable description of the event.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Structured metadata attached to the event (key-value pairs).</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>Timestamp when the event was emitted.</summary>
        public DateTimeOffset OccurredAt { get; set; }
    }

    /// <summary>
    /// Progress snapshot for a deployment attempt.
    /// </summary>
    public class DeploymentProgress
    {
        /// <summary>Current completion percentage (0–100).</summary>
        public int PercentComplete { get; set; }

        /// <summary>Ordered list of all pipeline stages and their completion status.</summary>
        public List<DeploymentStageStatus> Stages { get; set; } = new();

        /// <summary>Human-readable summary of the current progress state.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Estimated seconds until completion (null when unknown).</summary>
        public int? EstimatedSecondsRemaining { get; set; }
    }

    /// <summary>
    /// Status of a single stage in the deployment pipeline.
    /// </summary>
    public class DeploymentStageStatus
    {
        /// <summary>Stage this status entry describes.</summary>
        public DeploymentStage Stage { get; set; }

        /// <summary>Whether this stage has been completed.</summary>
        public bool IsCompleted { get; set; }

        /// <summary>Whether this stage is currently active.</summary>
        public bool IsActive { get; set; }

        /// <summary>Whether this stage failed.</summary>
        public bool HasFailed { get; set; }

        /// <summary>Human-readable label for display.</summary>
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request for a retry of a previously failed or in-progress deployment.
    /// </summary>
    public class DeploymentRetryRequest
    {
        /// <summary>Idempotency key of the deployment to retry.</summary>
        public string IdempotencyKey { get; set; } = string.Empty;

        /// <summary>Caller-supplied correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>If true, forces a fresh retry even if the prior attempt is cached as failed.</summary>
        public bool ForceRetry { get; set; }
    }

    /// <summary>
    /// Request for pre-deployment input validation.
    /// </summary>
    public class DeploymentValidationRequest
    {
        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Token standard to validate (e.g., "ASA", "ARC3", "ARC200", "ERC20").</summary>
        public string TokenStandard { get; set; } = string.Empty;

        /// <summary>Token name.</summary>
        public string TokenName { get; set; } = string.Empty;

        /// <summary>Token symbol.</summary>
        public string TokenSymbol { get; set; } = string.Empty;

        /// <summary>Target network identifier.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Total supply.</summary>
        public ulong TotalSupply { get; set; }

        /// <summary>Number of decimal places.</summary>
        public int Decimals { get; set; }

        /// <summary>Creator address.</summary>
        public string CreatorAddress { get; set; } = string.Empty;

        /// <summary>Optional metadata URI.</summary>
        public string? MetadataUri { get; set; }
    }

    /// <summary>
    /// Response from a pre-deployment validation call.
    /// </summary>
    public class DeploymentValidationResponse
    {
        /// <summary>Whether all required fields pass validation (no blocking errors).</summary>
        public bool IsValid { get; set; }

        /// <summary>Correlation ID echo.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>List of individual validation results.</summary>
        public List<DeploymentValidationResult> Results { get; set; } = new();

        /// <summary>Reliability guardrail findings detected during validation.</summary>
        public List<ReliabilityGuardrail> GuardrailFindings { get; set; } = new();

        /// <summary>Human-readable summary.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>Schema version for forward compatibility.</summary>
        public string SchemaVersion { get; set; } = "1.0.0";
    }

    /// <summary>
    /// Context used to evaluate reliability guardrails before or during deployment.
    /// </summary>
    public class GuardrailEvaluationContext
    {
        /// <summary>Token standard being deployed.</summary>
        public string TokenStandard { get; set; } = string.Empty;

        /// <summary>Target network.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Whether IPFS is needed for this deployment (e.g., ARC3 metadata).</summary>
        public bool RequiresIpfs { get; set; }

        /// <summary>Whether a blockchain RPC node is currently reachable.</summary>
        public bool NodeReachable { get; set; }

        /// <summary>Current retry count for the deployment.</summary>
        public int RetryCount { get; set; }

        /// <summary>Maximum allowed retry count.</summary>
        public int MaxRetryAttempts { get; set; }

        /// <summary>Whether the deployment has been running longer than the configured timeout.</summary>
        public bool IsTimedOut { get; set; }

        /// <summary>Whether a prior attempt with this idempotency key is already in-flight.</summary>
        public bool HasInFlightDuplicate { get; set; }

        /// <summary>Whether the creator address format is valid for the target network.</summary>
        public bool CreatorAddressValid { get; set; }

        /// <summary>Whether a conflicting deployment (same token name+network) is in progress.</summary>
        public bool ConflictingDeploymentDetected { get; set; }
    }

    /// <summary>
    /// On-chain evidence returned by a <c>IDeploymentEvidenceProvider</c> for a completed
    /// deployment transaction.
    ///
    /// When <see cref="IsSimulated"/> is <c>true</c> these values are deterministic hash-derived
    /// placeholders (suitable for contract-shape validation only).
    /// When <see cref="IsSimulated"/> is <c>false</c> these values were obtained from a live
    /// blockchain node and represent confirmed on-chain state.
    /// </summary>
    public class BlockchainDeploymentEvidence
    {
        /// <summary>
        /// On-chain transaction identifier. For Algorand this is the base32-encoded tx ID;
        /// for EVM chains this is the 0x-prefixed transaction hash.
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;

        /// <summary>
        /// On-chain asset / contract identifier. For Algorand this is the ASA ID (uint64);
        /// for EVM chains this is the token contract address (encoded as a uint64 for
        /// contract-address → uint mapping support).
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Block or round number at which the deployment transaction was confirmed.
        /// For Algorand this is the confirmed-round from the transaction confirmation object;
        /// for EVM chains this is the block number.
        /// </summary>
        public ulong ConfirmedRound { get; set; }

        /// <summary>
        /// When <c>true</c>, these values are deterministic simulations derived from a hash of
        /// the deployment ID and do NOT represent confirmed blockchain state.
        /// Sign-off tooling MUST treat <c>IsSimulated = true</c> as non-authoritative evidence.
        /// </summary>
        public bool IsSimulated { get; set; }

        /// <summary>
        /// ISO-8601 timestamp when the evidence was obtained or generated.
        /// </summary>
        public DateTimeOffset ObtainedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Identifies the data source that produced this evidence.
        /// Machine-readable label for audit trails and downstream consumers.
        ///
        /// Representative values:
        /// <list type="bullet">
        ///   <item><c>"algorand-indexer"</c> — obtained from a live Algorand indexer API call;
        ///     represents confirmed on-chain state.</item>
        ///   <item><c>"simulation"</c> — derived deterministically from the deployment ID hash;
        ///     not blockchain-confirmed. <see cref="IsSimulated"/> will also be <c>true</c>.</item>
        /// </list>
        /// </summary>
        public string EvidenceSource { get; set; } = string.Empty;
    }
}
