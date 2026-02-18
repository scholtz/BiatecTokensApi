namespace BiatecTokensApi.Models.Wallet
{
    /// <summary>
    /// User-friendly transaction summary with explicit retry and terminal state guidance
    /// </summary>
    /// <remarks>
    /// Provides frontend-consumable transaction status information with clear
    /// actionability signals and progress indicators.
    /// </remarks>
    public class TransactionSummary
    {
        /// <summary>
        /// Unique transaction identifier
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;

        /// <summary>
        /// Deployment ID if this is a token deployment transaction
        /// </summary>
        public string? DeploymentId { get; set; }

        /// <summary>
        /// Transaction hash on blockchain
        /// </summary>
        public string? TransactionHash { get; set; }

        /// <summary>
        /// User-friendly transaction type (e.g., "Token Deployment", "Transfer", "Opt-In")
        /// </summary>
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>
        /// Current status with user-friendly display text
        /// </summary>
        public TransactionStatus Status { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// User-friendly status message
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of current state
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Network where transaction was submitted
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Whether this transaction can be retried
        /// </summary>
        public bool IsRetryable { get; set; }

        /// <summary>
        /// Whether this is a terminal state (completed or permanently failed)
        /// </summary>
        public bool IsTerminal { get; set; }

        /// <summary>
        /// Recommended action for the user
        /// </summary>
        public string? RecommendedAction { get; set; }

        /// <summary>
        /// Expected time to completion (if pending)
        /// </summary>
        public int? EstimatedSecondsToCompletion { get; set; }

        /// <summary>
        /// Link to transaction on block explorer
        /// </summary>
        public string? ExplorerUrl { get; set; }

        /// <summary>
        /// Timestamp when transaction was initiated
        /// </summary>
        public DateTime InitiatedAt { get; set; }

        /// <summary>
        /// Timestamp when status was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when transaction completed (if completed)
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Total elapsed time in seconds
        /// </summary>
        public long ElapsedSeconds { get; set; }

        /// <summary>
        /// Error information if transaction failed
        /// </summary>
        public TransactionError? Error { get; set; }

        /// <summary>
        /// Token details if this is a token-related transaction
        /// </summary>
        public TransactionTokenDetails? TokenDetails { get; set; }

        /// <summary>
        /// Gas/fee information
        /// </summary>
        public TransactionFeeInfo? FeeInfo { get; set; }

        /// <summary>
        /// Addresses involved in the transaction
        /// </summary>
        public TransactionParties? Parties { get; set; }

        /// <summary>
        /// Confirmation progress (blocks/rounds confirmed)
        /// </summary>
        public ConfirmationProgress? Confirmations { get; set; }

        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// User-friendly transaction status enumeration
    /// </summary>
    public enum TransactionStatus
    {
        /// <summary>
        /// Transaction is being prepared
        /// </summary>
        Preparing = 0,

        /// <summary>
        /// Transaction is queued for submission
        /// </summary>
        Queued = 1,

        /// <summary>
        /// Transaction is being submitted to the network
        /// </summary>
        Submitting = 2,

        /// <summary>
        /// Transaction has been submitted and is pending confirmation
        /// </summary>
        Pending = 3,

        /// <summary>
        /// Transaction is being confirmed by the network
        /// </summary>
        Confirming = 4,

        /// <summary>
        /// Transaction is confirmed and being indexed
        /// </summary>
        Indexing = 5,

        /// <summary>
        /// Transaction completed successfully
        /// </summary>
        Completed = 6,

        /// <summary>
        /// Transaction failed with recoverable error (can be retried)
        /// </summary>
        Failed = 7,

        /// <summary>
        /// Transaction failed permanently (cannot be retried)
        /// </summary>
        PermanentlyFailed = 8,

        /// <summary>
        /// Transaction was cancelled by user
        /// </summary>
        Cancelled = 9,

        /// <summary>
        /// Transaction timed out waiting for confirmation
        /// </summary>
        TimedOut = 10
    }

    /// <summary>
    /// Error information for failed transactions
    /// </summary>
    public class TransactionError
    {
        /// <summary>
        /// Error code for programmatic handling
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// User-friendly error message
        /// </summary>
        public string UserMessage { get; set; } = string.Empty;

        /// <summary>
        /// Technical error details
        /// </summary>
        public string? TechnicalDetails { get; set; }

        /// <summary>
        /// Error category for classification
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Whether this error is retryable
        /// </summary>
        public bool IsRetryable { get; set; }

        /// <summary>
        /// Suggested fix or remediation steps
        /// </summary>
        public string? SuggestedFix { get; set; }

        /// <summary>
        /// Link to documentation for this error
        /// </summary>
        public string? DocumentationUrl { get; set; }

        /// <summary>
        /// Support correlation ID for customer service
        /// </summary>
        public string? SupportCorrelationId { get; set; }
    }

    /// <summary>
    /// Token-specific transaction details
    /// </summary>
    public class TransactionTokenDetails
    {
        /// <summary>
        /// Token symbol
        /// </summary>
        public string? Symbol { get; set; }

        /// <summary>
        /// Token name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Asset ID or contract address
        /// </summary>
        public string? AssetId { get; set; }

        /// <summary>
        /// Token standard
        /// </summary>
        public string? Standard { get; set; }

        /// <summary>
        /// Amount of tokens involved
        /// </summary>
        public decimal? Amount { get; set; }

        /// <summary>
        /// Number of decimal places
        /// </summary>
        public int? Decimals { get; set; }
    }

    /// <summary>
    /// Transaction fee information
    /// </summary>
    public class TransactionFeeInfo
    {
        /// <summary>
        /// Estimated fee in native currency
        /// </summary>
        public decimal? EstimatedFee { get; set; }

        /// <summary>
        /// Actual fee paid in native currency
        /// </summary>
        public decimal? ActualFee { get; set; }

        /// <summary>
        /// Fee in USD equivalent
        /// </summary>
        public decimal? FeeUsd { get; set; }

        /// <summary>
        /// Native currency symbol (ALGO, ETH, etc.)
        /// </summary>
        public string? CurrencySymbol { get; set; }

        /// <summary>
        /// Gas limit (EVM chains)
        /// </summary>
        public ulong? GasLimit { get; set; }

        /// <summary>
        /// Gas price (EVM chains)
        /// </summary>
        public decimal? GasPrice { get; set; }

        /// <summary>
        /// Gas used (EVM chains)
        /// </summary>
        public ulong? GasUsed { get; set; }
    }

    /// <summary>
    /// Parties involved in the transaction
    /// </summary>
    public class TransactionParties
    {
        /// <summary>
        /// Sender address
        /// </summary>
        public string? From { get; set; }

        /// <summary>
        /// Recipient address
        /// </summary>
        public string? To { get; set; }

        /// <summary>
        /// Contract address (for smart contract interactions)
        /// </summary>
        public string? Contract { get; set; }
    }

    /// <summary>
    /// Confirmation progress tracking
    /// </summary>
    public class ConfirmationProgress
    {
        /// <summary>
        /// Current number of confirmations
        /// </summary>
        public int Current { get; set; }

        /// <summary>
        /// Required number of confirmations for finality
        /// </summary>
        public int Required { get; set; }

        /// <summary>
        /// Block or round number where transaction was included
        /// </summary>
        public ulong? BlockNumber { get; set; }

        /// <summary>
        /// Latest block or round number on the network
        /// </summary>
        public ulong? LatestBlockNumber { get; set; }

        /// <summary>
        /// Whether transaction has reached finality
        /// </summary>
        public bool IsFinal { get; set; }
    }

    /// <summary>
    /// Request to get transaction summary
    /// </summary>
    public class GetTransactionSummaryRequest
    {
        /// <summary>
        /// Transaction ID or deployment ID to query
        /// </summary>
        public string TransactionId { get; set; } = string.Empty;

        /// <summary>
        /// Whether to include detailed metadata
        /// </summary>
        public bool IncludeMetadata { get; set; } = true;
    }

    /// <summary>
    /// Response containing transaction summary
    /// </summary>
    public class TransactionSummaryResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Transaction summary data
        /// </summary>
        public TransactionSummary? Transaction { get; set; }

        /// <summary>
        /// Error message if request failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Correlation ID for tracking
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Request to list recent transactions
    /// </summary>
    public class ListTransactionsRequest
    {
        /// <summary>
        /// Optional filter by address
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by transaction type
        /// </summary>
        public string? TransactionType { get; set; }

        /// <summary>
        /// Optional filter by status
        /// </summary>
        public TransactionStatus? Status { get; set; }

        /// <summary>
        /// Start date filter
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// End date filter
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Page number (1-based)
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size (max 100)
        /// </summary>
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Response containing transaction list
    /// </summary>
    public class TransactionListResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// List of transaction summaries
        /// </summary>
        public List<TransactionSummary> Transactions { get; set; } = new();

        /// <summary>
        /// Total count of matching transactions
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
        /// Error message if request failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Correlation ID for tracking
        /// </summary>
        public string? CorrelationId { get; set; }
    }
}
