namespace BiatecTokensApi.Models.MVPHardening
{
    /// <summary>Request to verify auth contract determinism.</summary>
    public class AuthContractVerifyRequest
    {
        public string? Email { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response from auth contract verification.</summary>
    public class AuthContractVerifyResponse
    {
        public bool Success { get; set; }
        public bool IsDeterministic { get; set; }
        public string? AlgorandAddress { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Status values for a deployment reliability record.</summary>
    public enum DeploymentReliabilityStatus
    {
        Pending, Accepted, Queued, Processing, Completed, Failed, Retrying, Cancelled
    }

    /// <summary>Request to initiate a reliable deployment.</summary>
    public class DeploymentReliabilityRequest
    {
        public string? TokenName { get; set; }
        public string? TokenStandard { get; set; }
        public string? DeployerAddress { get; set; }
        public string? Network { get; set; }
        public string? IdempotencyKey { get; set; }
        public string? CorrelationId { get; set; }
        public int MaxRetries { get; set; } = 3;
    }

    /// <summary>Response from a deployment reliability operation.</summary>
    public class DeploymentReliabilityResponse
    {
        public bool Success { get; set; }
        public string? DeploymentId { get; set; }
        public DeploymentReliabilityStatus Status { get; set; }
        public string? StatusReason { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public bool IsIdempotentReplay { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Request to transition a deployment to a new status.</summary>
    public class DeploymentStatusTransitionRequest
    {
        public string? DeploymentId { get; set; }
        public DeploymentReliabilityStatus TargetStatus { get; set; }
        public string? Reason { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Possible outcomes of a compliance check.</summary>
    public enum ComplianceOutcome { Pass, Fail, Warning, Pending }

    /// <summary>Request to run a compliance check.</summary>
    public class ComplianceCheckRequest
    {
        public string? AssetId { get; set; }
        public string? CheckType { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response from a compliance check.</summary>
    public class ComplianceCheckResponse
    {
        public bool Success { get; set; }
        public ComplianceOutcome Outcome { get; set; }
        public string? CheckType { get; set; }
        public string? AssetId { get; set; }
        public List<ComplianceCheckDetail> Details { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A single compliance rule result.</summary>
    public class ComplianceCheckDetail
    {
        public string Rule { get; set; } = string.Empty;
        public ComplianceOutcome Outcome { get; set; }
        public string? Message { get; set; }
        public string? RemediationHint { get; set; }
    }

    /// <summary>Request to create an observability trace.</summary>
    public class ObservabilityTraceRequest
    {
        public string? OperationName { get; set; }
        public string? CorrelationId { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }

    /// <summary>Response from creating an observability trace.</summary>
    public class ObservabilityTraceResponse
    {
        public bool Success { get; set; }
        public string? TraceId { get; set; }
        public string? CorrelationId { get; set; }
        public string? OperationName { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string SchemaVersion { get; set; } = "1.0.0";
    }

    /// <summary>An audit event emitted by the MVP hardening service.</summary>
    public class MVPHardeningAuditEvent
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string OperationName { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
        public string? UserId { get; set; }
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }
}
