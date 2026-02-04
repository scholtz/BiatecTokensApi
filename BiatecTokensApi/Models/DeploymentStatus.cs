namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Represents the status of a token deployment operation
    /// </summary>
    /// <remarks>
    /// This enum defines the complete lifecycle of a token deployment from initial request
    /// through to completion or failure. Each status transition is tracked in the audit trail.
    /// 
    /// State Machine Flow:
    /// Queued → Submitted → Pending → Confirmed → Indexed → Completed
    ///   ↓         ↓          ↓          ↓          ↓
    /// Failed ← ← ← ← ← ← ← ← ← ← ← ← ← ← (from any state)
    ///   ↓
    /// Queued (retry)
    /// 
    /// Queued → Cancelled (user-initiated)
    /// </remarks>
    public enum DeploymentStatus
    {
        /// <summary>
        /// Deployment request has been received and queued for processing
        /// </summary>
        Queued = 0,

        /// <summary>
        /// Deployment transaction has been submitted to the blockchain network
        /// </summary>
        Submitted = 1,

        /// <summary>
        /// Transaction is pending confirmation on the blockchain
        /// </summary>
        Pending = 2,

        /// <summary>
        /// Transaction has been confirmed by the blockchain (included in a block)
        /// </summary>
        Confirmed = 3,

        /// <summary>
        /// Deployment completed successfully with all post-deployment operations finished
        /// </summary>
        Completed = 4,

        /// <summary>
        /// Deployment failed at any stage of the process
        /// </summary>
        Failed = 5,

        /// <summary>
        /// Transaction has been indexed by blockchain explorers and is fully propagated
        /// </summary>
        /// <remarks>
        /// This intermediate state between Confirmed and Completed indicates that the
        /// transaction is now visible in block explorers and can be queried by external systems.
        /// Useful for SLA tracking and reconciliation.
        /// </remarks>
        Indexed = 6,

        /// <summary>
        /// Deployment was cancelled by the user before completion
        /// </summary>
        /// <remarks>
        /// User-initiated cancellation. Only valid from Queued state before submission.
        /// </remarks>
        Cancelled = 7
    }

    /// <summary>
    /// Represents a deployment status entry tracking a single status transition
    /// </summary>
    /// <remarks>
    /// Each status change during token deployment creates a new entry, forming an
    /// append-only audit trail of the deployment lifecycle.
    /// </remarks>
    public class DeploymentStatusEntry
    {
        /// <summary>
        /// Unique identifier for this status entry
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Unique identifier for the deployment operation
        /// </summary>
        /// <remarks>
        /// This ID is generated when the deployment is initiated and is used to track
        /// all status transitions for a single deployment operation.
        /// </remarks>
        public string DeploymentId { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the deployment
        /// </summary>
        public DeploymentStatus Status { get; set; }

        /// <summary>
        /// Timestamp when this status was recorded (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional message providing additional context about this status
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Transaction hash if the deployment has been submitted to the blockchain
        /// </summary>
        public string? TransactionHash { get; set; }

        /// <summary>
        /// Block number or round when the transaction was confirmed
        /// </summary>
        public ulong? ConfirmedRound { get; set; }

        /// <summary>
        /// Error message if the deployment failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Structured error details if the deployment failed
        /// </summary>
        public DeploymentError? ErrorDetails { get; set; }

        /// <summary>
        /// Reason code for this status transition (e.g., "USER_CANCELLED", "NETWORK_TIMEOUT")
        /// </summary>
        public string? ReasonCode { get; set; }

        /// <summary>
        /// Wallet address that initiated this transition (if applicable)
        /// </summary>
        public string? ActorAddress { get; set; }

        /// <summary>
        /// Compliance checks performed at this stage (if applicable)
        /// </summary>
        public List<ComplianceCheckResult>? ComplianceChecks { get; set; }

        /// <summary>
        /// Duration in milliseconds from previous status (if applicable)
        /// </summary>
        public long? DurationFromPreviousStatusMs { get; set; }

        /// <summary>
        /// Additional metadata about this status entry
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Result of a compliance check performed during deployment
    /// </summary>
    public class ComplianceCheckResult
    {
        /// <summary>
        /// Name of the compliance check (e.g., "KYC_VERIFICATION", "WHITELIST_CHECK")
        /// </summary>
        public string CheckName { get; set; } = string.Empty;

        /// <summary>
        /// Whether the check passed
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Detailed result message
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Timestamp when the check was performed (UTC)
        /// </summary>
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional context for the check result
        /// </summary>
        public Dictionary<string, object>? Context { get; set; }
    }

    /// <summary>
    /// Represents the complete deployment information including all status transitions
    /// </summary>
    public class TokenDeployment
    {
        /// <summary>
        /// Unique identifier for the deployment
        /// </summary>
        public string DeploymentId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Current status of the deployment
        /// </summary>
        public DeploymentStatus CurrentStatus { get; set; }

        /// <summary>
        /// Token type being deployed
        /// </summary>
        public string TokenType { get; set; } = string.Empty;

        /// <summary>
        /// Network where the token is being deployed
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Address of the user who initiated the deployment
        /// </summary>
        public string DeployedBy { get; set; } = string.Empty;

        /// <summary>
        /// Token name
        /// </summary>
        public string? TokenName { get; set; }

        /// <summary>
        /// Token symbol
        /// </summary>
        public string? TokenSymbol { get; set; }

        /// <summary>
        /// Asset ID (for Algorand tokens) or contract address (for EVM tokens)
        /// </summary>
        public string? AssetIdentifier { get; set; }

        /// <summary>
        /// Transaction hash of the deployment transaction
        /// </summary>
        public string? TransactionHash { get; set; }

        /// <summary>
        /// Timestamp when the deployment was initiated (UTC)
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the deployment was last updated (UTC)
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Complete history of status transitions for this deployment
        /// </summary>
        public List<DeploymentStatusEntry> StatusHistory { get; set; } = new();

        /// <summary>
        /// Error message if the deployment failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Correlation ID for tracking related events
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Request to query deployment status
    /// </summary>
    public class GetDeploymentStatusRequest
    {
        /// <summary>
        /// Deployment ID to query
        /// </summary>
        public string DeploymentId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response containing deployment status information
    /// </summary>
    public class DeploymentStatusResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The deployment information if found
        /// </summary>
        public TokenDeployment? Deployment { get; set; }

        /// <summary>
        /// Error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request to list deployments with filtering
    /// </summary>
    public class ListDeploymentsRequest
    {
        /// <summary>
        /// Optional filter by deployed by address
        /// </summary>
        public string? DeployedBy { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by token type
        /// </summary>
        public string? TokenType { get; set; }

        /// <summary>
        /// Optional filter by current status
        /// </summary>
        public DeploymentStatus? Status { get; set; }

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
    /// Response containing a list of deployments
    /// </summary>
    public class ListDeploymentsResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// List of deployments
        /// </summary>
        public List<TokenDeployment> Deployments { get; set; } = new();

        /// <summary>
        /// Total count of deployments matching the filter
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

        /// <summary>
        /// Error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request to cancel a deployment
    /// </summary>
    public class CancelDeploymentRequest
    {
        /// <summary>
        /// Reason for cancellation
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }
}
