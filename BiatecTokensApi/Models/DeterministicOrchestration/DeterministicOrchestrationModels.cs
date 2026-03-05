namespace BiatecTokensApi.Models.DeterministicOrchestration
{
    // ── Enumerations ────────────────────────────────────────────────────────────

    /// <summary>Lifecycle stages of a deterministic token deployment orchestration.</summary>
    public enum OrchestrationStage
    {
        /// <summary>Initial state; inputs have been accepted but not yet validated.</summary>
        Draft,
        /// <summary>All inputs are structurally valid; ready to be enqueued.</summary>
        Validated,
        /// <summary>Enqueued for processing; awaiting capacity on the deployment chain.</summary>
        Queued,
        /// <summary>Actively being processed by the deployment engine.</summary>
        Processing,
        /// <summary>Deployment transaction confirmed on-chain.</summary>
        Confirmed,
        /// <summary>Orchestration completed successfully; all post-deployment checks passed.</summary>
        Completed,
        /// <summary>Terminal failure; manual intervention or re-initiation required.</summary>
        Failed,
        /// <summary>Operator or user cancelled the orchestration before completion.</summary>
        Cancelled,
        /// <summary>Transient failure encountered; automatic retry in progress.</summary>
        Retrying
    }

    /// <summary>Outcome of a compliance-check step.</summary>
    public enum ComplianceCheckStatus
    {
        /// <summary>Compliance evaluation has not yet run.</summary>
        Pending,
        /// <summary>All compliance rules passed.</summary>
        Passed,
        /// <summary>One or more compliance rules failed.</summary>
        Failed,
        /// <summary>Manual review required before proceeding.</summary>
        NeedsReview
    }

    /// <summary>Severity level for compliance messages surfaced to the operator/frontend.</summary>
    public enum OrchestrationMessageSeverity
    {
        Info,
        Warning,
        Error,
        Blocker
    }

    // ── Request / Response models ───────────────────────────────────────────────

    /// <summary>Request to start a new deterministic deployment orchestration.</summary>
    public class OrchestrationRequest
    {
        /// <summary>Human-readable token name (required).</summary>
        public string? TokenName { get; set; }

        /// <summary>Token standard: ASA, ARC3, ARC200, ERC20, ARC1400 (required).</summary>
        public string? TokenStandard { get; set; }

        /// <summary>Target blockchain network (required).</summary>
        public string? Network { get; set; }

        /// <summary>Deployer wallet address (required).</summary>
        public string? DeployerAddress { get; set; }

        /// <summary>
        /// Caller-supplied idempotency key.  Repeated requests with the same key
        /// and identical parameters return the cached response without side-effects.
        /// </summary>
        public string? IdempotencyKey { get; set; }

        /// <summary>Correlation ID propagated through all audit and log entries.</summary>
        public string? CorrelationId { get; set; }

        /// <summary>Maximum automated retry attempts before marking the orchestration Failed.</summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>Optional additional metadata for compliance or audit purposes.</summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>Response from starting or replaying an orchestration.</summary>
    public class OrchestrationResponse
    {
        public bool Success { get; set; }
        public string? OrchestrationId { get; set; }
        public OrchestrationStage Stage { get; set; }
        public string? NextAction { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public bool IsIdempotentReplay { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Request to retrieve the current status of an orchestration.</summary>
    public class OrchestrationStatusRequest
    {
        public string? OrchestrationId { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Full status detail for an in-progress or completed orchestration.</summary>
    public class OrchestrationStatusResponse
    {
        public bool Success { get; set; }
        public string? OrchestrationId { get; set; }
        public string? TokenName { get; set; }
        public string? TokenStandard { get; set; }
        public string? Network { get; set; }
        public string? DeployerAddress { get; set; }
        public OrchestrationStage Stage { get; set; }
        public ComplianceCheckStatus ComplianceStatus { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public string? NextAction { get; set; }
        public List<OrchestrationMessage> Messages { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Request to advance an orchestration to its next valid lifecycle stage.</summary>
    public class OrchestrationAdvanceRequest
    {
        public string? OrchestrationId { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response from a lifecycle stage advance.</summary>
    public class OrchestrationAdvanceResponse
    {
        public bool Success { get; set; }
        public string? OrchestrationId { get; set; }
        public OrchestrationStage PreviousStage { get; set; }
        public OrchestrationStage CurrentStage { get; set; }
        public string? NextAction { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
    }

    /// <summary>Request to run compliance checks against an orchestration.</summary>
    public class ComplianceCheckRequest
    {
        public string? OrchestrationId { get; set; }
        public string? JurisdictionCode { get; set; }
        public string? CorrelationId { get; set; }
        public bool RunMicaChecks { get; set; } = true;
        public bool RunKycAmlChecks { get; set; } = true;
    }

    /// <summary>Result of a compliance evaluation run.</summary>
    public class ComplianceCheckResponse
    {
        public bool Success { get; set; }
        public string? OrchestrationId { get; set; }
        public ComplianceCheckStatus Status { get; set; }
        public List<ComplianceRule> Rules { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Individual compliance rule result.</summary>
    public class ComplianceRule
    {
        public string RuleId { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public OrchestrationMessageSeverity Severity { get; set; }
        public string? Message { get; set; }
        public string? RemediationHint { get; set; }
    }

    /// <summary>Request to cancel an in-progress orchestration.</summary>
    public class OrchestrationCancelRequest
    {
        public string? OrchestrationId { get; set; }
        public string? Reason { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response from cancelling an orchestration.</summary>
    public class OrchestrationCancelResponse
    {
        public bool Success { get; set; }
        public string? OrchestrationId { get; set; }
        public OrchestrationStage PreviousStage { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
    }

    /// <summary>Single audit event recorded during orchestration processing.</summary>
    public class OrchestrationAuditEntry
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string? OrchestrationId { get; set; }
        public string? CorrelationId { get; set; }
        public string Operation { get; set; } = string.Empty;
        public bool Succeeded { get; set; }
        public string? ErrorCode { get; set; }
        public OrchestrationStage? Stage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Operator / frontend message produced during orchestration processing.</summary>
    public class OrchestrationMessage
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RemediationHint { get; set; }
        public OrchestrationMessageSeverity Severity { get; set; }
    }

    /// <summary>Response wrapping the audit trail for an orchestration.</summary>
    public class OrchestrationAuditResponse
    {
        public bool Success { get; set; }
        public string? OrchestrationId { get; set; }
        public List<OrchestrationAuditEntry> Events { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
    }
}
