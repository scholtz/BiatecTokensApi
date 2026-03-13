using System.Text.Json.Serialization;

namespace BiatecTokensApi.Models.DeploymentSignOff
{
    // ── Enumerations ──────────────────────────────────────────────────────────────

    /// <summary>Outcome of an individual sign-off criterion check.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SignOffCriterionOutcome
    {
        /// <summary>Criterion is satisfied; no issues found.</summary>
        Pass,
        /// <summary>Criterion is not satisfied; sign-off is blocked until resolved.</summary>
        Fail,
        /// <summary>Criterion does not apply to this deployment configuration.</summary>
        NotApplicable
    }

    /// <summary>Overall sign-off readiness verdict.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SignOffVerdict
    {
        /// <summary>All required criteria pass; deployment is ready for enterprise sign-off.</summary>
        Approved,
        /// <summary>One or more required criteria failed; sign-off is blocked.</summary>
        Blocked,
        /// <summary>Deployment is still in progress; sign-off cannot be evaluated yet.</summary>
        InProgress,
        /// <summary>Deployment ended in a terminal failure; sign-off is not applicable.</summary>
        TerminalFailure
    }

    /// <summary>Category of a sign-off criterion, aligned with enterprise compliance needs.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SignOffCriterionCategory
    {
        /// <summary>On-chain identity and asset creation proof.</summary>
        BlockchainEvidence,
        /// <summary>Deployer identity and authentication proof.</summary>
        DeployerIdentity,
        /// <summary>Compliance and policy conformance.</summary>
        CompliancePolicy,
        /// <summary>Lifecycle completeness and terminal state.</summary>
        LifecycleIntegrity,
        /// <summary>Audit trail availability and completeness.</summary>
        AuditTrail
    }

    // ── Request models ────────────────────────────────────────────────────────────

    /// <summary>
    /// Request to validate sign-off readiness of a completed deployment.
    /// </summary>
    public class DeploymentSignOffValidationRequest
    {
        /// <summary>
        /// Deployment contract ID to validate.
        /// Returned by the deployment initiation endpoint.
        /// </summary>
        public string DeploymentId { get; set; } = string.Empty;

        /// <summary>Caller-supplied correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Whether an on-chain asset ID is required for sign-off.
        /// Defaults to <c>true</c>.
        /// For non-fungible or contract-based deployments that do not generate
        /// a numeric asset ID, set to <c>false</c>.
        /// </summary>
        public bool RequireAssetId { get; set; } = true;

        /// <summary>
        /// Whether a confirmed on-chain round/block is required for sign-off.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool RequireConfirmedRound { get; set; } = true;

        /// <summary>
        /// Whether a transaction ID is required for sign-off.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool RequireTransactionId { get; set; } = true;

        /// <summary>
        /// Whether a non-empty audit trail is required for sign-off.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool RequireAuditTrail { get; set; } = true;
    }

    // ── Response models ───────────────────────────────────────────────────────────

    /// <summary>
    /// Result of an individual sign-off criterion check.
    /// </summary>
    public class SignOffCriterion
    {
        /// <summary>Short machine-readable criterion name (e.g., "AssetId", "DeployerAddress").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Human-readable description of what this criterion verifies.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Category this criterion belongs to.</summary>
        public SignOffCriterionCategory Category { get; set; }

        /// <summary>Outcome of the criterion check.</summary>
        public SignOffCriterionOutcome Outcome { get; set; }

        /// <summary>
        /// Human-readable explanation of the outcome.
        /// On failure, explains what is missing or wrong.
        /// On pass, confirms what was verified.
        /// </summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>
        /// Actionable, non-technical guidance for a non-crypto-native operator.
        /// Present when <see cref="Outcome"/> is <see cref="SignOffCriterionOutcome.Fail"/>.
        /// </summary>
        public string? UserGuidance { get; set; }

        /// <summary>
        /// Whether this criterion is required for sign-off (as opposed to recommended).
        /// Sign-off is blocked if any required criterion fails.
        /// </summary>
        public bool IsRequired { get; set; } = true;
    }

    /// <summary>
    /// Structured deployment sign-off proof document.
    ///
    /// <para>
    /// This document is the backend's authoritative statement that a deployment lifecycle
    /// has completed with all required evidence fields present and all compliance criteria
    /// satisfied. It is intended to be cited in enterprise due-diligence, audit submissions,
    /// or product sign-off artefacts.
    /// </para>
    ///
    /// <para>
    /// If <see cref="IsReadyForSignOff"/> is <c>true</c>, all required criteria passed and
    /// the deployment evidence is complete. If <c>false</c>, inspect <see cref="Criteria"/>
    /// for specific failures and use <see cref="ActionableGuidance"/> to guide remediation.
    /// </para>
    /// </summary>
    public class DeploymentSignOffProof
    {
        /// <summary>Unique identifier for this sign-off proof document.</summary>
        public string ProofId { get; set; } = string.Empty;

        /// <summary>Deployment contract ID this proof is for.</summary>
        public string DeploymentId { get; set; } = string.Empty;

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Whether the deployment is ready for enterprise sign-off.
        /// <c>true</c> only when <see cref="Verdict"/> is <see cref="SignOffVerdict.Approved"/>
        /// and all required criteria passed.
        /// </summary>
        public bool IsReadyForSignOff { get; set; }

        /// <summary>Overall sign-off readiness verdict.</summary>
        public SignOffVerdict Verdict { get; set; }

        /// <summary>Number of required criteria that passed.</summary>
        public int PassedCriteriaCount { get; set; }

        /// <summary>Number of required criteria that failed (blocks sign-off when > 0).</summary>
        public int FailedCriteriaCount { get; set; }

        /// <summary>Total number of criteria evaluated.</summary>
        public int TotalCriteriaCount { get; set; }

        /// <summary>
        /// Ordered list of all evaluated sign-off criteria with individual outcomes.
        /// </summary>
        public List<SignOffCriterion> Criteria { get; set; } = new();

        /// <summary>
        /// Concise, non-technical guidance for a business operator explaining what to do next.
        /// Present when <see cref="IsReadyForSignOff"/> is <c>false</c>.
        /// </summary>
        public string? ActionableGuidance { get; set; }

        /// <summary>ISO 8601 timestamp when this proof was generated.</summary>
        public string GeneratedAt { get; set; } = string.Empty;

        // ── Deployment evidence fields (populated from the deployment record) ──

        /// <summary>Token standard deployed (e.g., "ASA", "ARC3", "ERC20").</summary>
        public string? TokenStandard { get; set; }

        /// <summary>Target network identifier (e.g., "algorand-testnet", "base-mainnet").</summary>
        public string? Network { get; set; }

        /// <summary>On-chain asset ID. Populated when deployment completed with blockchain confirmation.</summary>
        public ulong? AssetId { get; set; }

        /// <summary>On-chain transaction ID. Populated on completion.</summary>
        public string? TransactionId { get; set; }

        /// <summary>Confirmed on-chain round/block. Populated on completion.</summary>
        public ulong? ConfirmedRound { get; set; }

        /// <summary>Deployer address (Algorand base32 or EVM hex).</summary>
        public string? DeployerAddress { get; set; }

        /// <summary>Whether the deployer address was derived deterministically using ARC76.</summary>
        public bool IsDeterministicAddress { get; set; }

        /// <summary>Whether the deployment audit trail is present and non-empty.</summary>
        public bool HasAuditTrail { get; set; }

        /// <summary>Number of audit events in the deployment audit trail.</summary>
        public int AuditEventCount { get; set; }

        /// <summary>Timestamp when the deployment was initiated (ISO 8601).</summary>
        public string? DeploymentInitiatedAt { get; set; }

        /// <summary>Timestamp of the last deployment state change (ISO 8601).</summary>
        public string? DeploymentLastUpdatedAt { get; set; }
    }
}
