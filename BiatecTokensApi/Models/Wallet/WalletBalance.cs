namespace BiatecTokensApi.Models.Wallet
{
    /// <summary>
    /// Represents the balance of a single token in a wallet
    /// </summary>
    /// <remarks>
    /// Provides frontend-consumable balance data with proper decimal handling
    /// and chain-specific formatting for display purposes.
    /// </remarks>
    public class WalletBalance
    {
        /// <summary>
        /// Token asset identifier (asset ID for Algorand, contract address for EVM)
        /// </summary>
        public string AssetId { get; set; } = string.Empty;

        /// <summary>
        /// Token symbol (e.g., "USDC", "BTC")
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Token name (e.g., "USD Coin", "Bitcoin")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Network where the token exists (e.g., "algorand-mainnet", "ethereum-mainnet", "base")
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Token standard (e.g., "ASA", "ARC3", "ARC200", "ERC20", "ERC721")
        /// </summary>
        public string Standard { get; set; } = string.Empty;

        /// <summary>
        /// Raw balance in smallest unit (e.g., microAlgos, wei)
        /// </summary>
        public string RawBalance { get; set; } = "0";

        /// <summary>
        /// Human-readable balance with proper decimal places
        /// </summary>
        public decimal DisplayBalance { get; set; }

        /// <summary>
        /// Number of decimal places for this token
        /// </summary>
        public int Decimals { get; set; }

        /// <summary>
        /// Whether the balance data is verified on-chain
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// Timestamp when balance was last updated (UTC)
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional token icon/logo URL
        /// </summary>
        public string? IconUrl { get; set; }

        /// <summary>
        /// Optional USD equivalent value
        /// </summary>
        public decimal? UsdValue { get; set; }

        /// <summary>
        /// Optional price per token in USD
        /// </summary>
        public decimal? UsdPrice { get; set; }

        /// <summary>
        /// Whether this is a frozen asset (Algorand-specific)
        /// </summary>
        public bool? IsFrozen { get; set; }

        /// <summary>
        /// Minimum balance required for this asset (Algorand opt-in requirement)
        /// </summary>
        public decimal? MinimumBalance { get; set; }

        /// <summary>
        /// Additional metadata about the balance
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Represents a detailed token position including transaction history
    /// </summary>
    public class TokenPosition
    {
        /// <summary>
        /// Token asset identifier
        /// </summary>
        public string AssetId { get; set; } = string.Empty;

        /// <summary>
        /// Token symbol
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Token name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Network where the token exists
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Token standard
        /// </summary>
        public string Standard { get; set; } = string.Empty;

        /// <summary>
        /// Current balance
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>
        /// Number of decimal places
        /// </summary>
        public int Decimals { get; set; }

        /// <summary>
        /// Average purchase price (for cost basis calculation)
        /// </summary>
        public decimal? AverageCost { get; set; }

        /// <summary>
        /// Current market value in USD
        /// </summary>
        public decimal? MarketValue { get; set; }

        /// <summary>
        /// Unrealized profit/loss in USD
        /// </summary>
        public decimal? UnrealizedPnL { get; set; }

        /// <summary>
        /// Percentage gain/loss
        /// </summary>
        public decimal? PnLPercentage { get; set; }

        /// <summary>
        /// Total tokens acquired
        /// </summary>
        public decimal? TotalAcquired { get; set; }

        /// <summary>
        /// Total tokens sold
        /// </summary>
        public decimal? TotalSold { get; set; }

        /// <summary>
        /// Number of transactions for this position
        /// </summary>
        public int TransactionCount { get; set; }

        /// <summary>
        /// Date when position was first acquired
        /// </summary>
        public DateTime? FirstAcquired { get; set; }

        /// <summary>
        /// Date of most recent transaction
        /// </summary>
        public DateTime? LastActivity { get; set; }

        /// <summary>
        /// Whether this position is actively traded
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Recent transactions (limited to last N)
        /// </summary>
        public List<PositionTransaction>? RecentTransactions { get; set; }

        /// <summary>
        /// Position allocation as percentage of total portfolio
        /// </summary>
        public decimal? AllocationPercentage { get; set; }
    }

    /// <summary>
    /// Represents a transaction within a token position
    /// </summary>
    public class PositionTransaction
    {
        /// <summary>
        /// Transaction hash
        /// </summary>
        public string TransactionHash { get; set; } = string.Empty;

        /// <summary>
        /// Transaction type (buy, sell, transfer, mint, burn)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Amount of tokens involved
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Price per token at time of transaction
        /// </summary>
        public decimal? Price { get; set; }

        /// <summary>
        /// Total value in USD
        /// </summary>
        public decimal? TotalValue { get; set; }

        /// <summary>
        /// Timestamp of transaction (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Counterparty address (if applicable)
        /// </summary>
        public string? CounterpartyAddress { get; set; }

        /// <summary>
        /// Transaction fee paid
        /// </summary>
        public decimal? Fee { get; set; }

        /// <summary>
        /// Block number or round
        /// </summary>
        public ulong? BlockNumber { get; set; }
    }

    /// <summary>
    /// Aggregated portfolio summary across all chains and tokens
    /// </summary>
    public class PortfolioSummary
    {
        /// <summary>
        /// Wallet address or user identifier
        /// </summary>
        public string WalletAddress { get; set; } = string.Empty;

        /// <summary>
        /// Total portfolio value in USD
        /// </summary>
        public decimal TotalValueUsd { get; set; }

        /// <summary>
        /// Total number of different tokens held
        /// </summary>
        public int TotalTokens { get; set; }

        /// <summary>
        /// Number of networks/chains with balances
        /// </summary>
        public int ActiveNetworks { get; set; }

        /// <summary>
        /// Total unrealized profit/loss in USD
        /// </summary>
        public decimal? TotalUnrealizedPnL { get; set; }

        /// <summary>
        /// Overall portfolio percentage gain/loss
        /// </summary>
        public decimal? TotalPnLPercentage { get; set; }

        /// <summary>
        /// 24-hour portfolio value change in USD
        /// </summary>
        public decimal? Change24hUsd { get; set; }

        /// <summary>
        /// 24-hour portfolio percentage change
        /// </summary>
        public decimal? Change24hPercentage { get; set; }

        /// <summary>
        /// Breakdown of balances by network
        /// </summary>
        public List<NetworkBalance> NetworkBalances { get; set; } = new();

        /// <summary>
        /// Top token positions by value
        /// </summary>
        public List<TokenPosition> TopPositions { get; set; } = new();

        /// <summary>
        /// Portfolio diversification score (0-100)
        /// </summary>
        public decimal? DiversificationScore { get; set; }

        /// <summary>
        /// Timestamp when portfolio was last synced
        /// </summary>
        public DateTime LastSynced { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether portfolio data is completely up-to-date
        /// </summary>
        public bool IsFullySynced { get; set; }

        /// <summary>
        /// Networks that are currently syncing
        /// </summary>
        public List<string>? SyncingNetworks { get; set; }
    }

    /// <summary>
    /// Balance summary for a specific network
    /// </summary>
    public class NetworkBalance
    {
        /// <summary>
        /// Network identifier
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the network
        /// </summary>
        public string NetworkName { get; set; } = string.Empty;

        /// <summary>
        /// Total value on this network in USD
        /// </summary>
        public decimal TotalValueUsd { get; set; }

        /// <summary>
        /// Number of tokens on this network
        /// </summary>
        public int TokenCount { get; set; }

        /// <summary>
        /// Native currency balance (ALGO, ETH, etc.)
        /// </summary>
        public decimal? NativeBalance { get; set; }

        /// <summary>
        /// Native currency symbol
        /// </summary>
        public string? NativeCurrencySymbol { get; set; }

        /// <summary>
        /// Individual token balances on this network
        /// </summary>
        public List<WalletBalance> Balances { get; set; } = new();

        /// <summary>
        /// Whether this network is currently reachable
        /// </summary>
        public bool IsOnline { get; set; } = true;

        /// <summary>
        /// Last successful sync timestamp
        /// </summary>
        public DateTime? LastSynced { get; set; }
    }

    /// <summary>
    /// Request to get wallet balances
    /// </summary>
    public class GetWalletBalancesRequest
    {
        /// <summary>
        /// Wallet address to query
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by token standard
        /// </summary>
        public string? Standard { get; set; }

        /// <summary>
        /// Whether to include zero balances
        /// </summary>
        public bool IncludeZeroBalances { get; set; } = false;

        /// <summary>
        /// Whether to fetch current USD values
        /// </summary>
        public bool IncludeUsdValues { get; set; } = true;
    }

    /// <summary>
    /// Response containing wallet balances
    /// </summary>
    public class WalletBalancesResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Wallet address queried
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// List of token balances
        /// </summary>
        public List<WalletBalance> Balances { get; set; } = new();

        /// <summary>
        /// Total balance count
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Timestamp when balances were fetched
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

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
    /// Request to get portfolio summary
    /// </summary>
    public class GetPortfolioRequest
    {
        /// <summary>
        /// Wallet address to query (optional if using authenticated session)
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Whether to include detailed positions
        /// </summary>
        public bool IncludePositions { get; set; } = true;

        /// <summary>
        /// Maximum number of top positions to return
        /// </summary>
        public int TopPositionsLimit { get; set; } = 10;

        /// <summary>
        /// Whether to force refresh from blockchain
        /// </summary>
        public bool ForceRefresh { get; set; } = false;
    }

    /// <summary>
    /// Response containing portfolio summary
    /// </summary>
    public class PortfolioResponse
    {
        /// <summary>
        /// Whether the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Portfolio summary data
        /// </summary>
        public PortfolioSummary? Portfolio { get; set; }

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
