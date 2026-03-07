namespace BiatecTokensApi.Models.ComplianceHardening
{
    // ── Enumerations ──────────────────────────────────────────────────────────

    /// <summary>
    /// Categorizes the type of error that occurred during compliance hardening evaluation.
    /// Enables callers to distinguish root cause and apply appropriate remediation.
    /// </summary>
    public enum ComplianceErrorCategory
    {
        /// <summary>No error.</summary>
        None = 0,
        /// <summary>The caller supplied invalid or incomplete input data.</summary>
        InvalidInput = 1,
        /// <summary>An upstream compliance provider is unreachable or degraded.</summary>
        DependencyOutage = 2,
        /// <summary>A compliance policy rule evaluated to a blocking or conditional outcome.</summary>
        PolicyFailure = 3,
        /// <summary>An unexpected internal exception occurred in the service.</summary>
        InternalException = 4
    }

    /// <summary>
    /// Status outcome of a jurisdiction constraint evaluation.
    /// </summary>
    public enum JurisdictionStatus
    {
        /// <summary>Token launch is permitted in the requested jurisdiction.</summary>
        Permitted = 0,
        /// <summary>Token launch is blocked in the requested jurisdiction.</summary>
        Blocked = 1,
        /// <summary>Launch is allowed but subject to specific conditions or disclosures.</summary>
        RestrictedWithConditions = 2,
        /// <summary>Jurisdiction rules engine is not yet configured for this jurisdiction.</summary>
        NotConfigured = 3
    }

    /// <summary>
    /// Status outcome of a sanctions / KYC readiness evaluation.
    /// </summary>
    public enum SanctionsStatus
    {
        /// <summary>Subject passed sanctions screening.</summary>
        Clear = 0,
        /// <summary>Subject has a sanctions or watchlist flag requiring human review.</summary>
        Flagged = 1,
        /// <summary>Sanctions check has been submitted and is awaiting provider result.</summary>
        PendingReview = 2,
        /// <summary>Sanctions provider is not yet integrated; check was not performed.</summary>
        NotConfigured = 3,
        /// <summary>Sanctions provider is temporarily unavailable; result is degraded.</summary>
        ProviderUnavailable = 4
    }

    /// <summary>
    /// Overall launch gate decision status.
    /// </summary>
    public enum LaunchGateStatus
    {
        /// <summary>All compliance prerequisites are satisfied; launch may proceed.</summary>
        Permitted = 0,
        /// <summary>One or more blocking compliance reasons prevent launch.</summary>
        BlockedByCompliance = 1,
        /// <summary>Launch requires human review before it can proceed.</summary>
        PendingReview = 2,
        /// <summary>Prerequisite compliance checks have not yet been completed.</summary>
        NotReady = 3
    }

    /// <summary>
    /// Integration and health status of a compliance provider.
    /// </summary>
    public enum ProviderIntegrationStatus
    {
        /// <summary>Provider is integrated and responding normally.</summary>
        Active = 0,
        /// <summary>Provider endpoint is configured but not yet integrated.</summary>
        NotIntegrated = 1,
        /// <summary>Provider is responding but with degraded latency or partial results.</summary>
        Degraded = 2,
        /// <summary>Provider is unreachable or returning errors.</summary>
        Unavailable = 3
    }

    // ── Request models ─────────────────────────────────────────────────────────

    /// <summary>
    /// Request to perform a full compliance hardening evaluation for a pending token launch.
    /// </summary>
    public class ComplianceHardeningRequest
    {
        /// <summary>
        /// Identifier of the subject (issuer / beneficial owner) being evaluated.
        /// Required.
        /// </summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>
        /// Identifier for the token being launched (used for idempotency and audit trail).
        /// Required.
        /// </summary>
        public string TokenId { get; set; } = string.Empty;

        /// <summary>
        /// ISO 3166-1 alpha-2 country code of the target jurisdiction.
        /// When omitted the jurisdiction check is skipped.
        /// </summary>
        public string? JurisdictionCode { get; set; }

        /// <summary>
        /// Optional tenant / organisation context for multi-tenant deployments.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Optional supplemental metadata forwarded to downstream providers.
        /// </summary>
        public Dictionary<string, string> SubjectMetadata { get; set; } = new();

        /// <summary>
        /// Caller-supplied idempotency key.  When omitted a key is derived from
        /// SubjectId + TokenId.
        /// </summary>
        public string? IdempotencyKey { get; set; }
    }

    /// <summary>
    /// Request to evaluate a specific jurisdiction constraint.
    /// </summary>
    public class JurisdictionConstraintRequest
    {
        /// <summary>Subject identifier.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>ISO 3166-1 alpha-2 jurisdiction code (e.g. "US", "DE").</summary>
        public string JurisdictionCode { get; set; } = string.Empty;

        /// <summary>Optional token standard being considered (e.g. "ARC200", "ERC20").</summary>
        public string? TokenStandard { get; set; }
    }

    /// <summary>
    /// Request to evaluate sanctions readiness for a subject.
    /// </summary>
    public class SanctionsReadinessRequest
    {
        /// <summary>Subject identifier.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Optional supplemental metadata for provider.</summary>
        public Dictionary<string, string> SubjectMetadata { get; set; } = new();
    }

    /// <summary>
    /// Request to enforce the launch gate for a specific token.
    /// </summary>
    public class LaunchGateRequest
    {
        /// <summary>Token identifier.</summary>
        public string TokenId { get; set; } = string.Empty;

        /// <summary>Subject (issuer) identifier.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>
        /// Reference to an existing compliance hardening decision ID.
        /// When provided the gate reuses the cached evaluation.
        /// </summary>
        public string? ComplianceDecisionId { get; set; }
    }

    // ── Response / result models ───────────────────────────────────────────────

    /// <summary>
    /// Result of a jurisdiction constraint evaluation.
    /// </summary>
    public class JurisdictionConstraintResult
    {
        /// <summary>The jurisdiction code that was evaluated.</summary>
        public string JurisdictionCode { get; set; } = string.Empty;

        /// <summary>Evaluation outcome.</summary>
        public JurisdictionStatus Status { get; set; }

        /// <summary>
        /// Stable machine-readable reason code (e.g. "JURISDICTION_BLOCKED_OFAC",
        /// "JURISDICTION_PERMITTED", "JURISDICTION_NOT_CONFIGURED").
        /// </summary>
        public string ReasonCode { get; set; } = string.Empty;

        /// <summary>Human-readable explanation of the decision.</summary>
        public string? Description { get; set; }

        /// <summary>Conditions that must be met when status is RestrictedWithConditions.</summary>
        public List<string> Conditions { get; set; } = new();

        /// <summary>Actionable remediation hints for blocked or conditional outcomes.</summary>
        public List<string> RemediationHints { get; set; } = new();

        /// <summary>UTC timestamp of this evaluation.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }
    }

    /// <summary>
    /// Result of a sanctions / KYC readiness evaluation.
    /// </summary>
    public class SanctionsReadinessResult
    {
        /// <summary>Subject identifier.</summary>
        public string SubjectId { get; set; } = string.Empty;

        /// <summary>Evaluation outcome.</summary>
        public SanctionsStatus Status { get; set; }

        /// <summary>Stable machine-readable reason code.</summary>
        public string ReasonCode { get; set; } = string.Empty;

        /// <summary>Provider reference ID if a check was performed.</summary>
        public string? ProviderReferenceId { get; set; }

        /// <summary>Integration status of the underlying sanctions provider.</summary>
        public ProviderIntegrationStatus ProviderStatus { get; set; }

        /// <summary>Actionable remediation hints for flagged or unavailable outcomes.</summary>
        public List<string> RemediationHints { get; set; } = new();

        /// <summary>UTC timestamp of this evaluation.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }
    }

    /// <summary>
    /// Report on the integration and health status of a compliance provider.
    /// </summary>
    public class ProviderStatusReport
    {
        /// <summary>Provider name.</summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>Provider type (e.g. "KYC", "AML", "Sanctions", "Jurisdiction").</summary>
        public string ProviderType { get; set; } = string.Empty;

        /// <summary>Integration and health status.</summary>
        public ProviderIntegrationStatus Status { get; set; }

        /// <summary>Brief human-readable status description.</summary>
        public string? StatusMessage { get; set; }

        /// <summary>When the status was last checked.</summary>
        public DateTimeOffset LastCheckedAt { get; set; }
    }

    /// <summary>
    /// Comprehensive compliance hardening evaluation response.
    /// </summary>
    public class ComplianceHardeningResponse
    {
        /// <summary>Whether the operation itself succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Unique identifier for this hardening evaluation.</summary>
        public string? EvaluationId { get; set; }

        /// <summary>Overall launch gate status derived from all compliance signals.</summary>
        public LaunchGateStatus LaunchGate { get; set; }

        /// <summary>Jurisdiction constraint evaluation result (null when jurisdiction not evaluated).</summary>
        public JurisdictionConstraintResult? JurisdictionResult { get; set; }

        /// <summary>Sanctions readiness evaluation result.</summary>
        public SanctionsReadinessResult? SanctionsResult { get; set; }

        /// <summary>Stable reason code for the overall outcome.</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Error category when Success is false or the evaluation encountered errors.</summary>
        public ComplianceErrorCategory ErrorCategory { get; set; }

        /// <summary>Human-readable error message when Success is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Correlation identifier propagated from the request.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Whether this response was served from the idempotency cache.</summary>
        public bool IsIdempotentReplay { get; set; }

        /// <summary>Ordered list of remediation hints for the caller to present to the user.</summary>
        public List<string> RemediationHints { get; set; } = new();

        /// <summary>Provider status reports included for observability.</summary>
        public List<ProviderStatusReport> ProviderStatuses { get; set; } = new();

        /// <summary>Schema version of this response contract.</summary>
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>UTC timestamp when the evaluation was performed.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }
    }

    /// <summary>
    /// Response from a launch gate enforcement operation.
    /// </summary>
    public class LaunchGateResponse
    {
        /// <summary>Whether the gate operation itself succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Launch gate status result.</summary>
        public LaunchGateStatus GateStatus { get; set; }

        /// <summary>Whether the launch is permitted to proceed.</summary>
        public bool IsLaunchPermitted { get; set; }

        /// <summary>Human-readable explanation.</summary>
        public string? Message { get; set; }

        /// <summary>Stable reason code.</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Error category when the gate operation failed.</summary>
        public ComplianceErrorCategory ErrorCategory { get; set; }

        /// <summary>Error message when Success is false.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Ordered blocking reasons preventing launch.</summary>
        public List<string> BlockingReasons { get; set; } = new();

        /// <summary>Actionable remediation hints.</summary>
        public List<string> RemediationHints { get; set; } = new();

        /// <summary>Correlation identifier.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>UTC timestamp of this gate evaluation.</summary>
        public DateTimeOffset EvaluatedAt { get; set; }
    }

    /// <summary>
    /// Response listing the current status of all registered compliance providers.
    /// </summary>
    public class ProviderStatusListResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Provider status reports.</summary>
        public List<ProviderStatusReport> Providers { get; set; } = new();

        /// <summary>Correlation identifier.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>UTC timestamp.</summary>
        public DateTimeOffset ReportedAt { get; set; }
    }
}
