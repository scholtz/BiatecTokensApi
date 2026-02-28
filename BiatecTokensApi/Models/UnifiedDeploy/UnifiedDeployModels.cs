using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.UnifiedDeploy
{
    /// <summary>
    /// Unified token deployment request supporting multiple chains and token standards
    /// </summary>
    public class UnifiedDeployRequest
    {
        /// <summary>
        /// Target blockchain network (e.g., "algorand-mainnet", "algorand-testnet", "base", "ethereum")
        /// </summary>
        [Required(ErrorMessage = "Chain is required")]
        public string Chain { get; set; } = string.Empty;

        /// <summary>
        /// Token standard to deploy (e.g., "ASA", "ARC3", "ARC200", "ERC20", "ERC721")
        /// </summary>
        [Required(ErrorMessage = "Standard is required")]
        public string Standard { get; set; } = string.Empty;

        /// <summary>
        /// Token deployment parameters (name, symbol, total supply, decimals, etc.)
        /// Varies by chain/standard combination
        /// </summary>
        [Required(ErrorMessage = "Params is required")]
        public Dictionary<string, object> Params { get; set; } = new();

        /// <summary>
        /// Optional idempotency key to prevent duplicate deployments
        /// </summary>
        public string? IdempotencyKey { get; set; }
    }

    /// <summary>
    /// Response for a unified token deployment request
    /// </summary>
    public class UnifiedDeployResponse
    {
        /// <summary>
        /// Indicates if the deployment was successfully queued
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Job ID for polling deployment status
        /// </summary>
        public string? JobId { get; set; }

        /// <summary>
        /// Current status of the deployment job
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Human-readable message about the deployment
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Error message if the deployment could not be queued
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error code for client handling
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Response timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for polling deployment status by job ID
    /// </summary>
    public class DeploymentStatusPollResponse
    {
        /// <summary>
        /// Indicates if the query was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The deployment job ID
        /// </summary>
        public string? JobId { get; set; }

        /// <summary>
        /// Current status of the deployment
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Token type being deployed (e.g., "ASA", "ARC200", "ERC20")
        /// </summary>
        public string? TokenType { get; set; }

        /// <summary>
        /// Network where the token is being deployed
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Token name
        /// </summary>
        public string? TokenName { get; set; }

        /// <summary>
        /// Token symbol
        /// </summary>
        public string? TokenSymbol { get; set; }

        /// <summary>
        /// Asset ID (for Algorand tokens) or contract address (for EVM tokens), available once confirmed
        /// </summary>
        public string? AssetIdentifier { get; set; }

        /// <summary>
        /// Transaction hash if deployment has been submitted
        /// </summary>
        public string? TransactionHash { get; set; }

        /// <summary>
        /// Status history showing all transitions
        /// </summary>
        public List<StatusHistoryEntry> StatusHistory { get; set; } = new();

        /// <summary>
        /// Error message if the deployment failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Timestamp when the deployment was created
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// Timestamp when the deployment was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Response timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a single status history entry
    /// </summary>
    public class StatusHistoryEntry
    {
        /// <summary>
        /// Status at this point in time
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// When this status was set
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Optional message providing context
        /// </summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// Response for listing user deployments
    /// </summary>
    public class UserDeploymentsResponse
    {
        /// <summary>
        /// Indicates if the query was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// List of deployments for the user
        /// </summary>
        public List<DeploymentSummary> Deployments { get; set; } = new();

        /// <summary>
        /// Total count of deployments
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Error message if the query failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Correlation ID for request tracing
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Response timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Summary of a single deployment
    /// </summary>
    public class DeploymentSummary
    {
        /// <summary>
        /// Deployment job ID
        /// </summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// Current status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Token type (ASA, ARC200, ERC20, etc.)
        /// </summary>
        public string TokenType { get; set; } = string.Empty;

        /// <summary>
        /// Network
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Token name
        /// </summary>
        public string? TokenName { get; set; }

        /// <summary>
        /// Token symbol
        /// </summary>
        public string? TokenSymbol { get; set; }

        /// <summary>
        /// Asset ID or contract address (once confirmed)
        /// </summary>
        public string? AssetIdentifier { get; set; }

        /// <summary>
        /// When the deployment was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the deployment was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
