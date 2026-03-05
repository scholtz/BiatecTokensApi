namespace BiatecTokensApi.Models.ARC76MVPPipeline
{
    /// <summary>Lifecycle stage of an ARC76 MVP deployment pipeline.</summary>
    public enum PipelineStage
    {
        PendingReadiness,    // Initial state - checking ARC76 account readiness
        ReadinessVerified,   // ARC76 account ready
        ValidationPending,   // Inputs being validated
        ValidationPassed,    // Input validation complete
        CompliancePending,   // Compliance checks in progress
        CompliancePassed,    // Compliance checks passed
        DeploymentQueued,    // Queued for blockchain deployment
        DeploymentActive,    // Deployment actively executing
        DeploymentConfirmed, // Transaction confirmed on-chain
        Completed,           // Pipeline fully complete
        Failed,              // Terminal failure
        Cancelled,           // Cancelled by user/operator
        Retrying             // Transient failure, retrying
    }

    /// <summary>Failure category for structured error classification.</summary>
    public enum FailureCategory
    {
        None,
        UserCorrectable,    // User made an error, can fix and retry
        Retriable,          // Transient error, system can retry
        SystemCritical      // System-level failure, needs operator intervention
    }

    /// <summary>ARC76 account readiness check status.</summary>
    public enum ARC76ReadinessStatus
    {
        NotChecked,
        Ready,
        NotReady,
        Error
    }

    /// <summary>Request to initiate a new ARC76 MVP deployment pipeline.</summary>
    public class PipelineInitiateRequest
    {
        public string? TokenName { get; set; }
        public string? TokenStandard { get; set; }
        public string? Network { get; set; }
        public string? DeployerEmail { get; set; }
        public string? DeployerAddress { get; set; }
        public string? IdempotencyKey { get; set; }
        public string? CorrelationId { get; set; }
        public int MaxRetries { get; set; } = 3;
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>Response from pipeline initiation.</summary>
    public class PipelineInitiateResponse
    {
        public bool Success { get; set; }
        public string? PipelineId { get; set; }
        public PipelineStage Stage { get; set; }
        public ARC76ReadinessStatus ReadinessStatus { get; set; }
        public string? NextAction { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public FailureCategory FailureCategory { get; set; }
        public bool IsIdempotentReplay { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Response for pipeline status queries.</summary>
    public class PipelineStatusResponse
    {
        public bool Success { get; set; }
        public string? PipelineId { get; set; }
        public string? TokenName { get; set; }
        public string? TokenStandard { get; set; }
        public string? Network { get; set; }
        public string? DeployerAddress { get; set; }
        public PipelineStage Stage { get; set; }
        public ARC76ReadinessStatus ReadinessStatus { get; set; }
        public FailureCategory FailureCategory { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public string? NextAction { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Request to advance a pipeline to its next stage.</summary>
    public class PipelineAdvanceRequest
    {
        public string? PipelineId { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response from a pipeline advance operation.</summary>
    public class PipelineAdvanceResponse
    {
        public bool Success { get; set; }
        public string? PipelineId { get; set; }
        public PipelineStage PreviousStage { get; set; }
        public PipelineStage CurrentStage { get; set; }
        public string? NextAction { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
    }

    /// <summary>Request to cancel a pipeline.</summary>
    public class PipelineCancelRequest
    {
        public string? PipelineId { get; set; }
        public string? Reason { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response from a pipeline cancel operation.</summary>
    public class PipelineCancelResponse
    {
        public bool Success { get; set; }
        public string? PipelineId { get; set; }
        public PipelineStage PreviousStage { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
    }

    /// <summary>Single audit entry in a pipeline's audit trail.</summary>
    public class PipelineAuditEntry
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string? PipelineId { get; set; }
        public string? CorrelationId { get; set; }
        public string Operation { get; set; } = string.Empty;
        public bool Succeeded { get; set; }
        public string? ErrorCode { get; set; }
        public PipelineStage? Stage { get; set; }
        public FailureCategory FailureCategory { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Response containing the audit trail for a pipeline.</summary>
    public class PipelineAuditResponse
    {
        public bool Success { get; set; }
        public string? PipelineId { get; set; }
        public List<PipelineAuditEntry> Events { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
    }

    /// <summary>Request to retry a failed pipeline.</summary>
    public class PipelineRetryRequest
    {
        public string? PipelineId { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response from a pipeline retry operation.</summary>
    public class PipelineRetryResponse
    {
        public bool Success { get; set; }
        public string? PipelineId { get; set; }
        public PipelineStage Stage { get; set; }
        public int RetryCount { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
    }
}
