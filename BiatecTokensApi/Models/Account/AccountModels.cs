namespace BiatecTokensApi.Models.Account
{
    /// <summary>
    /// Response model for retrieving the user's ARC76-derived Algorand address
    /// </summary>
    public class AccountAddressResponse
    {
        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The ARC76-derived Algorand address for the authenticated user
        /// </summary>
        public string? AlgorandAddress { get; set; }

        /// <summary>
        /// The derivation standard used (ARC76)
        /// </summary>
        public string DerivationStandard { get; set; } = "ARC76";

        /// <summary>
        /// Error message if operation failed
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
    /// Request model for testnet funding
    /// </summary>
    public class AccountFundRequest
    {
        /// <summary>
        /// Target network for funding (e.g., "algorand-testnet")
        /// </summary>
        public string Network { get; set; } = "algorand-testnet";
    }

    /// <summary>
    /// Response model for testnet funding request
    /// </summary>
    public class AccountFundResponse
    {
        /// <summary>
        /// Indicates if the funding request was submitted
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The Algorand address that was funded
        /// </summary>
        public string? AlgorandAddress { get; set; }

        /// <summary>
        /// The network on which funding was requested
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Transaction ID if the funding was processed
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// Human-readable message about the funding status
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Error message if the request failed
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
    /// Response model for account balance query
    /// </summary>
    public class AccountBalanceResponse
    {
        /// <summary>
        /// Indicates if the balance query was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The Algorand address for which balances are returned
        /// </summary>
        public string? AlgorandAddress { get; set; }

        /// <summary>
        /// ALGO balance in microAlgos
        /// </summary>
        public long AlgoBalanceMicroAlgos { get; set; }

        /// <summary>
        /// ALGO balance in whole ALGOs (for display)
        /// </summary>
        public decimal AlgoBalance { get; set; }

        /// <summary>
        /// List of token balances held by the account
        /// </summary>
        public List<TokenBalance> TokenBalances { get; set; } = new();

        /// <summary>
        /// Network the balance was queried from
        /// </summary>
        public string? Network { get; set; }

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
    /// Represents the balance of a single token asset
    /// </summary>
    public class TokenBalance
    {
        /// <summary>
        /// Asset ID on Algorand
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Token name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Token unit name / symbol
        /// </summary>
        public string? UnitName { get; set; }

        /// <summary>
        /// Raw balance amount
        /// </summary>
        public ulong Amount { get; set; }

        /// <summary>
        /// Number of decimals for display
        /// </summary>
        public int Decimals { get; set; }
    }
}
