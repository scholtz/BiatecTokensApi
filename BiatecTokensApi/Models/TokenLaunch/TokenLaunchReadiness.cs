namespace BiatecTokensApi.Models.TokenLaunch
{
    /// <summary>
    /// Request to evaluate token launch readiness
    /// </summary>
    public class TokenLaunchReadinessRequest
    {
        /// <summary>
        /// User identifier requesting the token launch
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Type of token being launched
        /// </summary>
        public string TokenType { get; set; } = string.Empty;

        /// <summary>
        /// Target blockchain network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Token deployment parameters for context
        /// </summary>
        public Dictionary<string, object>? DeploymentContext { get; set; }

        /// <summary>
        /// Correlation ID for tracking related operations
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Whether to perform full evaluation (default: true)
        /// </summary>
        public bool FullEvaluation { get; set; } = true;
    }

    /// <summary>
    /// Comprehensive token launch readiness response
    /// </summary>
    public class TokenLaunchReadinessResponse
    {
        /// <summary>
        /// Unique evaluation identifier
        /// </summary>
        public string EvaluationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Overall readiness status
        /// </summary>
        public ReadinessStatus Status { get; set; }

        /// <summary>
        /// User-facing summary of readiness state
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Whether token launch can proceed
        /// </summary>
        public bool CanProceed { get; set; }

        /// <summary>
        /// Ordered list of remediation tasks (if not ready)
        /// </summary>
        public List<RemediationTask> RemediationTasks { get; set; } = new();

        /// <summary>
        /// Detailed evaluation results by category
        /// </summary>
        public ReadinessEvaluationDetails Details { get; set; } = new();

        /// <summary>
        /// Policy version used for evaluation
        /// </summary>
        public string PolicyVersion { get; set; } = string.Empty;

        /// <summary>
        /// Evaluation timestamp (UTC)
        /// </summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Correlation ID for tracking
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Evaluation latency in milliseconds
        /// </summary>
        public long EvaluationTimeMs { get; set; }
    }

    /// <summary>
    /// Overall readiness status
    /// </summary>
    public enum ReadinessStatus
    {
        /// <summary>
        /// All requirements met, launch can proceed
        /// </summary>
        Ready,

        /// <summary>
        /// Critical blockers prevent launch
        /// </summary>
        Blocked,

        /// <summary>
        /// Non-critical warnings, launch can proceed with caution
        /// </summary>
        Warning,

        /// <summary>
        /// Manual review required before launch
        /// </summary>
        NeedsReview
    }

    /// <summary>
    /// Remediation task required to achieve readiness
    /// </summary>
    public class RemediationTask
    {
        /// <summary>
        /// Unique task identifier
        /// </summary>
        public string TaskId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Category of the blocker
        /// </summary>
        public BlockerCategory Category { get; set; }

        /// <summary>
        /// Error code associated with this blocker
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Severity level
        /// </summary>
        public RemediationSeverity Severity { get; set; }

        /// <summary>
        /// Recommended owner/team for resolution
        /// </summary>
        public string OwnerHint { get; set; } = string.Empty;

        /// <summary>
        /// Ordered list of actions to resolve
        /// </summary>
        public List<string> Actions { get; set; } = new();

        /// <summary>
        /// Estimated time to resolve (hours)
        /// </summary>
        public int? EstimatedResolutionHours { get; set; }

        /// <summary>
        /// IDs of tasks that must be completed first
        /// </summary>
        public List<string> DependsOn { get; set; } = new();

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Blocker category
    /// </summary>
    public enum BlockerCategory
    {
        /// <summary>
        /// Subscription entitlement limitation
        /// </summary>
        Entitlement,

        /// <summary>
        /// Feature not included in current plan
        /// </summary>
        FeatureAccess,

        /// <summary>
        /// ARC76 account state issue
        /// </summary>
        AccountState,

        /// <summary>
        /// Compliance decision requirement
        /// </summary>
        ComplianceDecision,

        /// <summary>
        /// KYC/AML verification requirement
        /// </summary>
        KycAml,

        /// <summary>
        /// Jurisdiction constraint
        /// </summary>
        Jurisdiction,

        /// <summary>
        /// Whitelist configuration requirement
        /// </summary>
        Whitelist,

        /// <summary>
        /// Token configuration issue
        /// </summary>
        TokenConfiguration,

        /// <summary>
        /// Network or integration issue
        /// </summary>
        Integration
    }

    /// <summary>
    /// Remediation severity
    /// </summary>
    public enum RemediationSeverity
    {
        /// <summary>
        /// Informational, does not block launch
        /// </summary>
        Info,

        /// <summary>
        /// Warning, should be addressed but not blocking
        /// </summary>
        Low,

        /// <summary>
        /// Important, may cause issues if not resolved
        /// </summary>
        Medium,

        /// <summary>
        /// Critical, blocks launch
        /// </summary>
        High,

        /// <summary>
        /// Urgent, blocks launch and requires immediate attention
        /// </summary>
        Critical
    }

    /// <summary>
    /// Detailed evaluation results by category
    /// </summary>
    public class ReadinessEvaluationDetails
    {
        /// <summary>
        /// Subscription entitlement check result
        /// </summary>
        public CategoryEvaluationResult Entitlement { get; set; } = new();

        /// <summary>
        /// ARC76 account readiness result
        /// </summary>
        public CategoryEvaluationResult AccountReadiness { get; set; } = new();

        /// <summary>
        /// Compliance decisions result
        /// </summary>
        public CategoryEvaluationResult ComplianceDecisions { get; set; } = new();

        /// <summary>
        /// KYC/AML verification result
        /// </summary>
        public CategoryEvaluationResult KycAml { get; set; } = new();

        /// <summary>
        /// Jurisdiction compliance result
        /// </summary>
        public CategoryEvaluationResult Jurisdiction { get; set; } = new();

        /// <summary>
        /// Whitelist configuration result
        /// </summary>
        public CategoryEvaluationResult Whitelist { get; set; } = new();

        /// <summary>
        /// Integration health result
        /// </summary>
        public CategoryEvaluationResult Integration { get; set; } = new();
    }

    /// <summary>
    /// Evaluation result for a single category
    /// </summary>
    public class CategoryEvaluationResult
    {
        /// <summary>
        /// Whether this category passed
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Status message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Specific reason codes if failed
        /// </summary>
        public List<string> ReasonCodes { get; set; } = new();

        /// <summary>
        /// Evaluation details
        /// </summary>
        public Dictionary<string, object>? Details { get; set; }

        /// <summary>
        /// Evaluation timestamp
        /// </summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Evidence snapshot for audit trail
    /// </summary>
    public class TokenLaunchReadinessEvidence
    {
        /// <summary>
        /// Evidence identifier
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Evaluation ID this evidence supports
        /// </summary>
        public string EvaluationId { get; set; } = string.Empty;

        /// <summary>
        /// User identifier
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Request snapshot (JSON)
        /// </summary>
        public string RequestSnapshot { get; set; } = string.Empty;

        /// <summary>
        /// Response snapshot (JSON)
        /// </summary>
        public string ResponseSnapshot { get; set; } = string.Empty;

        /// <summary>
        /// Individual category results (JSON)
        /// </summary>
        public string CategoryResultsSnapshot { get; set; } = string.Empty;

        /// <summary>
        /// Evidence creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Correlation ID
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Hash of evidence data for integrity
        /// </summary>
        public string? DataHash { get; set; }

        /// <summary>
        /// Reference to related token deployment (if any)
        /// </summary>
        public string? TokenDeploymentId { get; set; }
    }
}
