namespace BiatecTokensApi.Models.ARC76
{
    /// <summary>
    /// Represents the readiness state of an ARC76-derived account
    /// </summary>
    public enum ARC76ReadinessState
    {
        /// <summary>
        /// Account has not been initialized
        /// </summary>
        NotInitialized = 0,

        /// <summary>
        /// Account is currently being initialized
        /// </summary>
        Initializing = 1,

        /// <summary>
        /// Account is fully ready for operations
        /// </summary>
        Ready = 2,

        /// <summary>
        /// Account is in degraded state (e.g., key rotation needed, metadata issues)
        /// </summary>
        Degraded = 3,

        /// <summary>
        /// Account initialization or operation has failed
        /// </summary>
        Failed = 4
    }

    /// <summary>
    /// Result of an ARC76 account readiness check
    /// </summary>
    public class ARC76AccountReadinessResult
    {
        /// <summary>
        /// Current readiness state
        /// </summary>
        public ARC76ReadinessState State { get; set; }

        /// <summary>
        /// Whether the account is ready for operations
        /// </summary>
        public bool IsReady => State == ARC76ReadinessState.Ready;

        /// <summary>
        /// Account address (if available)
        /// </summary>
        public string? AccountAddress { get; set; }

        /// <summary>
        /// Reason if not ready
        /// </summary>
        public string? NotReadyReason { get; set; }

        /// <summary>
        /// Remediation steps if not ready
        /// </summary>
        public List<string>? RemediationSteps { get; set; }

        /// <summary>
        /// Account metadata validation results
        /// </summary>
        public AccountMetadataValidation? MetadataValidation { get; set; }

        /// <summary>
        /// Key status information
        /// </summary>
        public KeyStatusInfo? KeyStatus { get; set; }

        /// <summary>
        /// Last state transition timestamp
        /// </summary>
        public DateTime? LastStateTransition { get; set; }

        /// <summary>
        /// Correlation ID for tracking
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Validation results for account metadata
    /// </summary>
    public class AccountMetadataValidation
    {
        /// <summary>
        /// Whether metadata is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// List of validation errors
        /// </summary>
        public List<string> ValidationErrors { get; set; } = new();

        /// <summary>
        /// Whether metadata needs update
        /// </summary>
        public bool NeedsUpdate { get; set; }
    }

    /// <summary>
    /// Information about account key status
    /// </summary>
    public class KeyStatusInfo
    {
        /// <summary>
        /// Whether key is accessible
        /// </summary>
        public bool IsAccessible { get; set; }

        /// <summary>
        /// Whether key rotation is required
        /// </summary>
        public bool RotationRequired { get; set; }

        /// <summary>
        /// Last key rotation timestamp (if applicable)
        /// </summary>
        public DateTime? LastRotation { get; set; }

        /// <summary>
        /// Next recommended rotation timestamp (if applicable)
        /// </summary>
        public DateTime? NextRecommendedRotation { get; set; }

        /// <summary>
        /// Key provider information
        /// </summary>
        public string? KeyProvider { get; set; }
    }
}
