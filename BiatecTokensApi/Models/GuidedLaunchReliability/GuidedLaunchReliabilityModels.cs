namespace BiatecTokensApi.Models.GuidedLaunchReliability
{
    /// <summary>Stages of the guided token launch flow.</summary>
    public enum LaunchStage
    {
        NotStarted,
        TokenDetails,
        ComplianceSetup,
        NetworkSelection,
        Review,
        Submitted,
        Completed,
        Cancelled,
        Failed
    }

    /// <summary>Severity of a compliance UX message.</summary>
    public enum ComplianceMessageSeverity
    {
        Info,
        Warning,
        Error,
        Blocker
    }

    /// <summary>Request to initiate a guided token launch.</summary>
    public class GuidedLaunchInitiateRequest
    {
        public string? TokenName { get; set; }
        public string? TokenStandard { get; set; }
        public string? OwnerId { get; set; }
        public string? IdempotencyKey { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response from initiating a guided token launch.</summary>
    public class GuidedLaunchInitiateResponse
    {
        public bool Success { get; set; }
        public string? LaunchId { get; set; }
        public LaunchStage Stage { get; set; }
        public string? NextAction { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public bool IsIdempotentReplay { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Request to get the current status of a guided launch.</summary>
    public class GuidedLaunchStatusRequest
    {
        public string? LaunchId { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Detailed status of a guided launch.</summary>
    public class GuidedLaunchStatusResponse
    {
        public bool Success { get; set; }
        public string? LaunchId { get; set; }
        public string? TokenName { get; set; }
        public string? TokenStandard { get; set; }
        public string? OwnerId { get; set; }
        public LaunchStage Stage { get; set; }
        public string? NextAction { get; set; }
        public List<ComplianceUxMessage> ComplianceMessages { get; set; } = new();
        public List<string> CompletedSteps { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Request to advance a guided launch to the next stage.</summary>
    public class GuidedLaunchAdvanceRequest
    {
        public string? LaunchId { get; set; }
        public string? StepData { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response from advancing a guided launch stage.</summary>
    public class GuidedLaunchAdvanceResponse
    {
        public bool Success { get; set; }
        public string? LaunchId { get; set; }
        public LaunchStage PreviousStage { get; set; }
        public LaunchStage CurrentStage { get; set; }
        public string? NextAction { get; set; }
        public List<ComplianceUxMessage> ComplianceMessages { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Request to validate a specific step in the guided launch.</summary>
    public class GuidedLaunchValidateStepRequest
    {
        public string? LaunchId { get; set; }
        public string? StepName { get; set; }
        public Dictionary<string, string>? StepInputs { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response from validating a guided launch step.</summary>
    public class GuidedLaunchValidateStepResponse
    {
        public bool Success { get; set; }
        public string? LaunchId { get; set; }
        public string? StepName { get; set; }
        public bool IsValid { get; set; }
        public List<ComplianceUxMessage> ValidationMessages { get; set; } = new();
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? RemediationHint { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Request to cancel a guided launch.</summary>
    public class GuidedLaunchCancelRequest
    {
        public string? LaunchId { get; set; }
        public string? Reason { get; set; }
        public string? CorrelationId { get; set; }
    }

    /// <summary>Response from cancelling a guided launch.</summary>
    public class GuidedLaunchCancelResponse
    {
        public bool Success { get; set; }
        public string? LaunchId { get; set; }
        public LaunchStage FinalStage { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CorrelationId { get; set; }
        public string SchemaVersion { get; set; } = "1.0.0";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A compliance UX message providing what/why/how guidance for non-technical users.</summary>
    public class ComplianceUxMessage
    {
        public ComplianceMessageSeverity Severity { get; set; }
        public string What { get; set; } = string.Empty;
        public string Why { get; set; } = string.Empty;
        public string How { get; set; } = string.Empty;
        public string? Code { get; set; }
    }

    /// <summary>An audit event emitted by the guided launch reliability service.</summary>
    public class GuidedLaunchAuditEvent
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string OperationName { get; set; } = string.Empty;
        public string? LaunchId { get; set; }
        public string? CorrelationId { get; set; }
        public LaunchStage Stage { get; set; }
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }
}
