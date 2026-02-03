namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Unified security activity event for audit trails and activity timelines
    /// </summary>
    /// <remarks>
    /// This model provides a normalized view of security-related events including authentication,
    /// token deployments, subscription changes, and compliance checks for enterprise audit requirements.
    /// Designed for MICA compliance and enterprise security monitoring.
    /// </remarks>
    public class SecurityActivityEvent
    {
        /// <summary>
        /// Unique identifier for the event
        /// </summary>
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Account/user identifier associated with this event
        /// </summary>
        public string AccountId { get; set; } = string.Empty;

        /// <summary>
        /// Type of security event
        /// </summary>
        public SecurityEventType EventType { get; set; }

        /// <summary>
        /// Severity level of the event
        /// </summary>
        public EventSeverity Severity { get; set; }

        /// <summary>
        /// Timestamp when the event occurred (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Human-readable summary of the event
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Additional metadata about the event
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Correlation ID for related events
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Source IP address of the request
        /// </summary>
        public string? SourceIp { get; set; }

        /// <summary>
        /// User agent string from the request
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Whether the operation completed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Type of security event for activity tracking
    /// </summary>
    public enum SecurityEventType
    {
        /// <summary>
        /// User login event
        /// </summary>
        Login,

        /// <summary>
        /// User logout event
        /// </summary>
        Logout,

        /// <summary>
        /// Failed login attempt
        /// </summary>
        LoginFailed,

        /// <summary>
        /// Password reset requested
        /// </summary>
        PasswordReset,

        /// <summary>
        /// Network switch event
        /// </summary>
        NetworkSwitch,

        /// <summary>
        /// Token deployment initiated
        /// </summary>
        TokenDeployment,

        /// <summary>
        /// Token deployment succeeded
        /// </summary>
        TokenDeploymentSuccess,

        /// <summary>
        /// Token deployment failed
        /// </summary>
        TokenDeploymentFailure,

        /// <summary>
        /// Subscription plan changed
        /// </summary>
        SubscriptionChange,

        /// <summary>
        /// Compliance check performed
        /// </summary>
        ComplianceCheck,

        /// <summary>
        /// Whitelist operation
        /// </summary>
        WhitelistOperation,

        /// <summary>
        /// Blacklist operation
        /// </summary>
        BlacklistOperation,

        /// <summary>
        /// Audit export requested
        /// </summary>
        AuditExport,

        /// <summary>
        /// Recovery operation
        /// </summary>
        Recovery,

        /// <summary>
        /// Account created
        /// </summary>
        AccountCreated,

        /// <summary>
        /// Account suspended
        /// </summary>
        AccountSuspended,

        /// <summary>
        /// Account activated
        /// </summary>
        AccountActivated
    }

    /// <summary>
    /// Severity level of security events
    /// </summary>
    public enum EventSeverity
    {
        /// <summary>
        /// Informational event
        /// </summary>
        Info,

        /// <summary>
        /// Warning event
        /// </summary>
        Warning,

        /// <summary>
        /// Error event
        /// </summary>
        Error,

        /// <summary>
        /// Critical security event
        /// </summary>
        Critical
    }

    /// <summary>
    /// Request to retrieve security activity events
    /// </summary>
    public class GetSecurityActivityRequest
    {
        /// <summary>
        /// Optional filter by account ID
        /// </summary>
        public string? AccountId { get; set; }

        /// <summary>
        /// Optional filter by event type
        /// </summary>
        public SecurityEventType? EventType { get; set; }

        /// <summary>
        /// Optional filter by severity
        /// </summary>
        public EventSeverity? Severity { get; set; }

        /// <summary>
        /// Optional start date filter (ISO 8601)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter (ISO 8601)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Optional filter by success status
        /// </summary>
        public bool? Success { get; set; }

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination (default: 50, max: 100)
        /// </summary>
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Response containing security activity events
    /// </summary>
    public class SecurityActivityResponse : BaseResponse
    {
        /// <summary>
        /// List of security activity events
        /// </summary>
        public List<SecurityActivityEvent> Events { get; set; } = new();

        /// <summary>
        /// Total number of events matching the filter
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Token deployment transaction for history tracking
    /// </summary>
    public class TokenDeploymentTransaction
    {
        /// <summary>
        /// Transaction ID
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;

        /// <summary>
        /// Asset ID (token ID)
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Network on which the token was deployed
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Token standard (ASA, ARC3, ARC200, ERC20, etc.)
        /// </summary>
        public string TokenStandard { get; set; } = string.Empty;

        /// <summary>
        /// Token name
        /// </summary>
        public string? TokenName { get; set; }

        /// <summary>
        /// Token symbol
        /// </summary>
        public string? TokenSymbol { get; set; }

        /// <summary>
        /// Deployment status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when deployed (UTC)
        /// </summary>
        public DateTime DeployedAt { get; set; }

        /// <summary>
        /// Creator/deployer address
        /// </summary>
        public string CreatorAddress { get; set; } = string.Empty;

        /// <summary>
        /// Confirmed round (for blockchain networks)
        /// </summary>
        public ulong? ConfirmedRound { get; set; }

        /// <summary>
        /// Error message if deployment failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request to retrieve token deployment transaction history
    /// </summary>
    public class GetTransactionHistoryRequest
    {
        /// <summary>
        /// Optional filter by account ID
        /// </summary>
        public string? AccountId { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by token standard
        /// </summary>
        public string? TokenStandard { get; set; }

        /// <summary>
        /// Optional filter by status
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Optional start date filter (ISO 8601)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter (ISO 8601)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination (default: 50, max: 100)
        /// </summary>
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Response containing token deployment transaction history
    /// </summary>
    public class TransactionHistoryResponse : BaseResponse
    {
        /// <summary>
        /// List of token deployment transactions
        /// </summary>
        public List<TokenDeploymentTransaction> Transactions { get; set; } = new();

        /// <summary>
        /// Total number of transactions matching the filter
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Request to export audit trail
    /// </summary>
    public class ExportAuditTrailRequest
    {
        /// <summary>
        /// Export format (csv or json)
        /// </summary>
        public string Format { get; set; } = "json";

        /// <summary>
        /// Optional filter by account ID
        /// </summary>
        public string? AccountId { get; set; }

        /// <summary>
        /// Optional filter by event type
        /// </summary>
        public SecurityEventType? EventType { get; set; }

        /// <summary>
        /// Optional filter by severity
        /// </summary>
        public EventSeverity? Severity { get; set; }

        /// <summary>
        /// Optional start date filter (ISO 8601)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter (ISO 8601)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Optional idempotency key for preventing duplicate exports
        /// </summary>
        public string? IdempotencyKey { get; set; }
    }

    /// <summary>
    /// Response for audit trail export request
    /// </summary>
    public class ExportAuditTrailResponse : BaseResponse
    {
        /// <summary>
        /// Unique export ID
        /// </summary>
        public string ExportId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Export status (completed, pending, failed)
        /// </summary>
        public string Status { get; set; } = "completed";

        /// <summary>
        /// Download URL for the exported file (if synchronous)
        /// </summary>
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// Number of records included in the export
        /// </summary>
        public int RecordCount { get; set; }

        /// <summary>
        /// Export format
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Idempotency hit indicator
        /// </summary>
        public bool IdempotencyHit { get; set; }

        /// <summary>
        /// Plan-based quota information
        /// </summary>
        public ExportQuota? Quota { get; set; }
    }

    /// <summary>
    /// Export quota information based on subscription tier
    /// </summary>
    public class ExportQuota
    {
        /// <summary>
        /// Maximum exports allowed per month
        /// </summary>
        public int MaxExportsPerMonth { get; set; }

        /// <summary>
        /// Number of exports used this month
        /// </summary>
        public int ExportsUsed { get; set; }

        /// <summary>
        /// Remaining exports for this month
        /// </summary>
        public int ExportsRemaining { get; set; }

        /// <summary>
        /// Maximum records per export
        /// </summary>
        public int MaxRecordsPerExport { get; set; }
    }

    /// <summary>
    /// Recovery guidance response
    /// </summary>
    public class RecoveryGuidanceResponse : BaseResponse
    {
        /// <summary>
        /// Recovery eligibility status
        /// </summary>
        public RecoveryEligibility Eligibility { get; set; }

        /// <summary>
        /// Timestamp of last recovery attempt
        /// </summary>
        public DateTime? LastRecoveryAttempt { get; set; }

        /// <summary>
        /// Cooldown period remaining in seconds (0 if no cooldown)
        /// </summary>
        public int CooldownRemaining { get; set; }

        /// <summary>
        /// Recovery steps
        /// </summary>
        public List<RecoveryStep> Steps { get; set; } = new();

        /// <summary>
        /// Additional guidance notes
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Recovery eligibility status
    /// </summary>
    public enum RecoveryEligibility
    {
        /// <summary>
        /// Recovery is available
        /// </summary>
        Eligible,

        /// <summary>
        /// Recovery is in cooldown period
        /// </summary>
        Cooldown,

        /// <summary>
        /// Recovery already sent
        /// </summary>
        AlreadySent,

        /// <summary>
        /// Recovery not configured
        /// </summary>
        NotConfigured,

        /// <summary>
        /// Account locked
        /// </summary>
        AccountLocked
    }

    /// <summary>
    /// Recovery step with instructions
    /// </summary>
    public class RecoveryStep
    {
        /// <summary>
        /// Step number
        /// </summary>
        public int StepNumber { get; set; }

        /// <summary>
        /// Step title
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed instructions
        /// </summary>
        public string Instructions { get; set; } = string.Empty;

        /// <summary>
        /// Whether this step is completed
        /// </summary>
        public bool Completed { get; set; }
    }
}
