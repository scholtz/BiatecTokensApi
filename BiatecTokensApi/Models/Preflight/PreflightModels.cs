using BiatecTokensApi.Models.ARC76;
using BiatecTokensApi.Models.Entitlement;

namespace BiatecTokensApi.Models.Preflight
{
    /// <summary>
    /// Request for preflight readiness check
    /// </summary>
    public class PreflightCheckRequest
    {
        /// <summary>
        /// The operation to check readiness for
        /// </summary>
        public EntitlementOperation Operation { get; set; }

        /// <summary>
        /// Optional context for the operation
        /// </summary>
        public Dictionary<string, object>? OperationContext { get; set; }
    }

    /// <summary>
    /// Response containing complete preflight readiness assessment
    /// </summary>
    public class PreflightCheckResponse
    {
        /// <summary>
        /// Overall readiness status
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// Subscription tier of the user
        /// </summary>
        public string SubscriptionTier { get; set; } = string.Empty;

        /// <summary>
        /// Entitlement check result
        /// </summary>
        public EntitlementCheckResult EntitlementCheck { get; set; } = new();

        /// <summary>
        /// ARC76 account readiness result
        /// </summary>
        public ARC76AccountReadinessResult AccountReadiness { get; set; } = new();

        /// <summary>
        /// List of blockers preventing operation (if not ready)
        /// </summary>
        public List<ReadinessBlocker> Blockers { get; set; } = new();

        /// <summary>
        /// Upgrade recommendation if entitlement is the blocker
        /// </summary>
        public UpgradeRecommendation? UpgradeRecommendation { get; set; }

        /// <summary>
        /// Policy version used for evaluation
        /// </summary>
        public string PolicyVersion { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp of the check
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Correlation ID for tracking
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Response time in milliseconds
        /// </summary>
        public long ResponseTimeMs { get; set; }
    }

    /// <summary>
    /// Represents a specific blocker preventing operation readiness
    /// </summary>
    public class ReadinessBlocker
    {
        /// <summary>
        /// Type of blocker
        /// </summary>
        public BlockerType Type { get; set; }

        /// <summary>
        /// Error code associated with blocker
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of the blocker
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Remediation steps to resolve the blocker
        /// </summary>
        public List<string> RemediationSteps { get; set; } = new();

        /// <summary>
        /// Severity of the blocker
        /// </summary>
        public BlockerSeverity Severity { get; set; }
    }

    /// <summary>
    /// Type of readiness blocker
    /// </summary>
    public enum BlockerType
    {
        /// <summary>
        /// Entitlement/subscription limit blocker
        /// </summary>
        Entitlement,

        /// <summary>
        /// ARC76 account state blocker
        /// </summary>
        AccountState,

        /// <summary>
        /// Feature not included in current tier
        /// </summary>
        FeatureAccess,

        /// <summary>
        /// System or configuration issue
        /// </summary>
        SystemError
    }

    /// <summary>
    /// Severity level of a blocker
    /// </summary>
    public enum BlockerSeverity
    {
        /// <summary>
        /// Low severity - operation can proceed with limitations
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity - operation may succeed but not recommended
        /// </summary>
        Medium,

        /// <summary>
        /// High severity - operation will fail
        /// </summary>
        High,

        /// <summary>
        /// Critical severity - system issue requires immediate attention
        /// </summary>
        Critical
    }
}
