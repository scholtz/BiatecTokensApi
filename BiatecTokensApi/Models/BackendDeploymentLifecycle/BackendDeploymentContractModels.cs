using System.Text.Json.Serialization;

namespace BiatecTokensApi.Models.BackendDeploymentLifecycle
{
    // ── Enumerations ──────────────────────────────────────────────────────────────

    /// <summary>ARC76 derivation validation result.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ARC76DerivationStatus
    {
        /// <summary>ARC76 account was successfully derived from credentials.</summary>
        Derived,
        /// <summary>ARC76 derivation failed due to invalid credentials.</summary>
        InvalidCredentials,
        /// <summary>ARC76 derivation skipped because an explicit address was supplied.</summary>
        AddressProvided,
        /// <summary>ARC76 derivation failed due to an internal error.</summary>
        Error
    }

    /// <summary>Lifecycle contract state for a backend deployment.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ContractLifecycleState
    {
        /// <summary>Deployment contract created and pending validation.</summary>
        Pending,
        /// <summary>Inputs validated; deployer account confirmed.</summary>
        Validated,
        /// <summary>Deployment submitted to the target network.</summary>
        Submitted,
        /// <summary>Deployment confirmed on-chain.</summary>
        Confirmed,
        /// <summary>Deployment completed and indexed.</summary>
        Completed,
        /// <summary>Deployment failed; see error taxonomy for details.</summary>
        Failed,
        /// <summary>Deployment cancelled before submission.</summary>
        Cancelled
    }

    /// <summary>Machine-readable error taxonomy for backend deployment contract failures.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DeploymentErrorCode
    {
        /// <summary>No error.</summary>
        None,
        /// <summary>Authentication or session context is invalid.</summary>
        AuthenticationFailed,
        /// <summary>Provided credentials are invalid or expired.</summary>
        InvalidCredentials,
        /// <summary>ARC76 account derivation produced an unexpected or mismatched address.</summary>
        DeriveAddressMismatch,
        /// <summary>Required input field is missing or empty.</summary>
        RequiredFieldMissing,
        /// <summary>Input value is outside the allowed range.</summary>
        ValidationRangeFault,
        /// <summary>The token standard is not supported for the requested network.</summary>
        UnsupportedStandard,
        /// <summary>The target network is not configured or unreachable.</summary>
        NetworkUnavailable,
        /// <summary>An idempotency key conflict was detected (different params, same key).</summary>
        IdempotencyConflict,
        /// <summary>Compliance check blocked the deployment.</summary>
        ComplianceBlocked,
        /// <summary>An upstream dependency (IPFS, node) failed transiently.</summary>
        DependencyTransientFailure,
        /// <summary>An unexpected internal error occurred.</summary>
        InternalError
    }

    /// <summary>Category of a compliance audit event.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ComplianceAuditEventKind
    {
        /// <summary>Deployment was initiated by an authenticated user.</summary>
        DeploymentInitiated,
        /// <summary>ARC76 account derivation occurred.</summary>
        AccountDerived,
        /// <summary>Deployment inputs were validated.</summary>
        InputsValidated,
        /// <summary>Compliance policy was evaluated.</summary>
        PolicyEvaluated,
        /// <summary>Deployment was submitted to the network.</summary>
        TransactionSubmitted,
        /// <summary>Deployment was confirmed on-chain.</summary>
        TransactionConfirmed,
        /// <summary>Deployment failed.</summary>
        DeploymentFailed,
        /// <summary>Deployment was completed.</summary>
        DeploymentCompleted
    }

    // ── Request models ────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to initiate a deterministic backend deployment contract.
    /// Supports both ARC76 credential-based account derivation and explicit address supply.
    /// </summary>
    public class BackendDeploymentContractRequest
    {
        /// <summary>
        /// Idempotency key. When omitted, derived from all deployment parameters.
        /// Replaying with the same key returns cached result without duplicate side effects.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>Caller-supplied correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Deployer email for ARC76 account derivation.
        /// When supplied together with <see cref="DeployerPassword"/>, the deployer account
        /// is derived deterministically using ARC76.
        /// </summary>
        public string? DeployerEmail { get; set; }

        /// <summary>
        /// Deployer password for ARC76 account derivation.
        /// Never stored or logged.
        /// </summary>
        public string? DeployerPassword { get; set; }

        /// <summary>
        /// Explicit deployer address.
        /// When provided, ARC76 derivation is skipped and this address is used directly.
        /// Must be a valid Algorand (58-char base32) or EVM (0x…) address.
        /// </summary>
        public string? ExplicitDeployerAddress { get; set; }

        /// <summary>Token standard to deploy (e.g., "ASA", "ARC3", "ARC200", "ERC20"). Required.</summary>
        public string TokenStandard { get; set; } = string.Empty;

        /// <summary>Token name (1–64 characters). Required.</summary>
        public string TokenName { get; set; } = string.Empty;

        /// <summary>Token symbol / unit name (1–8 characters). Required.</summary>
        public string TokenSymbol { get; set; } = string.Empty;

        /// <summary>Target network identifier (e.g., "algorand-mainnet", "base-mainnet"). Required.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Total token supply. Must be greater than zero.</summary>
        public ulong TotalSupply { get; set; }

        /// <summary>Number of decimal places (0–19).</summary>
        public int Decimals { get; set; }

        /// <summary>Optional IPFS metadata URI.</summary>
        public string? MetadataUri { get; set; }

        /// <summary>Whether to enable freeze authority.</summary>
        public bool EnableFreeze { get; set; }

        /// <summary>Whether to enable clawback authority.</summary>
        public bool EnableClawback { get; set; }

        /// <summary>Maximum retry attempts before the deployment is considered terminal.</summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>Total deployment timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 120;
    }

    /// <summary>
    /// Validation-only request. Returns validation results without side effects.
    /// </summary>
    public class BackendDeploymentContractValidationRequest
    {
        /// <summary>Correlation ID for tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Deployer email for ARC76 derivation (optional).</summary>
        public string? DeployerEmail { get; set; }

        /// <summary>Deployer password for ARC76 derivation (optional).</summary>
        public string? DeployerPassword { get; set; }

        /// <summary>Explicit deployer address (optional).</summary>
        public string? ExplicitDeployerAddress { get; set; }

        /// <summary>Token standard to validate.</summary>
        public string TokenStandard { get; set; } = string.Empty;

        /// <summary>Token name.</summary>
        public string TokenName { get; set; } = string.Empty;

        /// <summary>Token symbol.</summary>
        public string TokenSymbol { get; set; } = string.Empty;

        /// <summary>Target network.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Total supply.</summary>
        public ulong TotalSupply { get; set; }

        /// <summary>Decimals.</summary>
        public int Decimals { get; set; }
    }

    // ── Response models ───────────────────────────────────────────────────────────

    /// <summary>
    /// Response from a backend deployment contract initiation or status query.
    /// Contains full lifecycle state, ARC76 derivation evidence, compliance audit fields,
    /// and structured error taxonomy.
    /// </summary>
    public class BackendDeploymentContractResponse
    {
        /// <summary>Unique deployment contract identifier.</summary>
        public string DeploymentId { get; set; } = string.Empty;

        /// <summary>Idempotency key associated with this contract.</summary>
        public string IdempotencyKey { get; set; } = string.Empty;

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Current lifecycle state.</summary>
        public ContractLifecycleState State { get; set; }

        /// <summary>Whether this response was served from an idempotency cache (replay).</summary>
        public bool IsIdempotentReplay { get; set; }

        /// <summary>ARC76 account derivation result.</summary>
        public ARC76DerivationStatus DerivationStatus { get; set; }

        /// <summary>Derived or supplied deployer address (Algorand or EVM).</summary>
        public string? DeployerAddress { get; set; }

        /// <summary>
        /// ARC76 derivation is deterministic: same email+password always produces this address.
        /// True when the address was produced by ARC76 derivation (not explicitly supplied).
        /// </summary>
        public bool IsDeterministicAddress { get; set; }

        /// <summary>Machine-readable error code (None when successful).</summary>
        public DeploymentErrorCode ErrorCode { get; set; }

        /// <summary>Human-readable message describing the current state or error.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Safe user-facing guidance (no internal details).</summary>
        public string? UserGuidance { get; set; }

        /// <summary>On-chain asset ID (populated when State is Completed).</summary>
        public ulong? AssetId { get; set; }

        /// <summary>On-chain transaction ID.</summary>
        public string? TransactionId { get; set; }

        /// <summary>Confirmed on-chain round/block.</summary>
        public ulong? ConfirmedRound { get; set; }

        /// <summary>Number of retry attempts consumed.</summary>
        public int RetryCount { get; set; }

        /// <summary>Whether the deployment is running in degraded mode.</summary>
        public bool IsDegraded { get; set; }

        /// <summary>Timestamp when the deployment was initiated (ISO 8601).</summary>
        public string InitiatedAt { get; set; } = string.Empty;

        /// <summary>Timestamp of the last state change (ISO 8601).</summary>
        public string LastUpdatedAt { get; set; } = string.Empty;

        /// <summary>Validation findings from pre-deployment checks.</summary>
        public List<ContractValidationResult> ValidationResults { get; set; } = new();

        /// <summary>Compliance audit events emitted during lifecycle.</summary>
        public List<ComplianceAuditEvent> AuditEvents { get; set; } = new();
    }

    /// <summary>
    /// Result of a validation-only request.
    /// </summary>
    public class BackendDeploymentContractValidationResponse
    {
        /// <summary>Correlation ID for tracing.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Whether all validations passed.</summary>
        public bool IsValid { get; set; }

        /// <summary>ARC76 derivation status.</summary>
        public ARC76DerivationStatus DerivationStatus { get; set; }

        /// <summary>Derived or supplied deployer address.</summary>
        public string? DeployerAddress { get; set; }

        /// <summary>Whether the address is deterministic (ARC76-derived).</summary>
        public bool IsDeterministicAddress { get; set; }

        /// <summary>Field-level validation findings.</summary>
        public List<ContractValidationResult> ValidationResults { get; set; } = new();

        /// <summary>Machine-readable error code for the first blocking error (None if valid).</summary>
        public DeploymentErrorCode FirstErrorCode { get; set; }

        /// <summary>Human-readable summary message.</summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Individual field-level validation result.</summary>
    public class ContractValidationResult
    {
        /// <summary>Field name that was validated.</summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>Whether this field passed validation.</summary>
        public bool IsValid { get; set; }

        /// <summary>Machine-readable error code (None when valid).</summary>
        public DeploymentErrorCode ErrorCode { get; set; }

        /// <summary>Human-readable validation message.</summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Compliance audit event emitted during deployment lifecycle.</summary>
    public class ComplianceAuditEvent
    {
        /// <summary>Unique event identifier.</summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>Correlation ID linking this event to the deployment.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Deployment contract ID.</summary>
        public string DeploymentId { get; set; } = string.Empty;

        /// <summary>Kind of compliance event.</summary>
        public ComplianceAuditEventKind EventKind { get; set; }

        /// <summary>ISO 8601 timestamp of the event.</summary>
        public string Timestamp { get; set; } = string.Empty;

        /// <summary>Who initiated the action (deployer address or system).</summary>
        public string Actor { get; set; } = string.Empty;

        /// <summary>Lifecycle state at the time of the event.</summary>
        public ContractLifecycleState StateAtEvent { get; set; }

        /// <summary>Event outcome (Success/Failure/Blocked).</summary>
        public string Outcome { get; set; } = string.Empty;

        /// <summary>Supplementary data for the compliance record (no secrets).</summary>
        public Dictionary<string, string> Details { get; set; } = new();
    }

    /// <summary>Compliance audit trail for a deployment contract.</summary>
    public class BackendDeploymentAuditTrail
    {
        /// <summary>Deployment contract ID.</summary>
        public string DeploymentId { get; set; } = string.Empty;

        /// <summary>Correlation ID for the deployment.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Final state of the deployment.</summary>
        public ContractLifecycleState FinalState { get; set; }

        /// <summary>Deployer address (Algorand or EVM).</summary>
        public string? DeployerAddress { get; set; }

        /// <summary>Token standard deployed.</summary>
        public string TokenStandard { get; set; } = string.Empty;

        /// <summary>Target network.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>On-chain asset ID (when completed).</summary>
        public ulong? AssetId { get; set; }

        /// <summary>Ordered compliance audit events.</summary>
        public List<ComplianceAuditEvent> Events { get; set; } = new();
    }
}
