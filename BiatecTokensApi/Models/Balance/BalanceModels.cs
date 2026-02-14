using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Balance
{
    /// <summary>
    /// Request to query token balance for an address
    /// </summary>
    public class BalanceQueryRequest
    {
        /// <summary>
        /// Token identifier (asset ID for Algorand, contract address for EVM)
        /// </summary>
        [Required]
        public string TokenIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Address to query balance for
        /// </summary>
        [Required]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Blockchain network identifier (e.g., "algorand-mainnet", "base-mainnet")
        /// </summary>
        [Required]
        public string Chain { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response containing token balance information
    /// </summary>
    public class BalanceQueryResponse
    {
        /// <summary>
        /// Whether the query was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Token identifier queried
        /// </summary>
        public string? TokenIdentifier { get; set; }

        /// <summary>
        /// Address queried
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Blockchain network
        /// </summary>
        public string? Chain { get; set; }

        /// <summary>
        /// Token balance (as string to handle large numbers)
        /// </summary>
        public string? Balance { get; set; }

        /// <summary>
        /// Token decimals for display purposes
        /// </summary>
        public int? Decimals { get; set; }

        /// <summary>
        /// Formatted balance with decimals applied
        /// </summary>
        public string? FormattedBalance { get; set; }

        /// <summary>
        /// Token symbol (if available)
        /// </summary>
        public string? Symbol { get; set; }

        /// <summary>
        /// Token name (if available)
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Whether the address is opted-in to the token (Algorand only)
        /// </summary>
        public bool? IsOptedIn { get; set; }

        /// <summary>
        /// Whether the address is frozen for this token (if applicable)
        /// </summary>
        public bool? IsFrozen { get; set; }

        /// <summary>
        /// Error message if query failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error code if query failed
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Timestamp when balance was queried
        /// </summary>
        public DateTime? Timestamp { get; set; }
    }

    /// <summary>
    /// Request to query multiple token balances for an address
    /// </summary>
    public class MultiBalanceQueryRequest
    {
        /// <summary>
        /// Address to query balances for
        /// </summary>
        [Required]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Blockchain network identifier
        /// </summary>
        [Required]
        public string Chain { get; set; } = string.Empty;

        /// <summary>
        /// Optional list of token identifiers to query (if empty, returns all tokens)
        /// </summary>
        public List<string>? TokenIdentifiers { get; set; }

        /// <summary>
        /// Include zero balances in response (default: false)
        /// </summary>
        public bool IncludeZeroBalances { get; set; } = false;
    }

    /// <summary>
    /// Response containing multiple token balances
    /// </summary>
    public class MultiBalanceQueryResponse
    {
        /// <summary>
        /// Whether the query was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Address queried
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Blockchain network
        /// </summary>
        public string? Chain { get; set; }

        /// <summary>
        /// List of token balances
        /// </summary>
        public List<TokenBalance>? Balances { get; set; }

        /// <summary>
        /// Total number of tokens found
        /// </summary>
        public int TotalTokens { get; set; }

        /// <summary>
        /// Error message if query failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error code if query failed
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Timestamp when balances were queried
        /// </summary>
        public DateTime? Timestamp { get; set; }
    }

    /// <summary>
    /// Represents a single token balance
    /// </summary>
    public class TokenBalance
    {
        /// <summary>
        /// Token identifier
        /// </summary>
        public string TokenIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Token name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Token symbol
        /// </summary>
        public string? Symbol { get; set; }

        /// <summary>
        /// Token decimals
        /// </summary>
        public int Decimals { get; set; }

        /// <summary>
        /// Raw balance
        /// </summary>
        public string Balance { get; set; } = "0";

        /// <summary>
        /// Formatted balance with decimals
        /// </summary>
        public string FormattedBalance { get; set; } = "0";

        /// <summary>
        /// Whether address is opted-in (Algorand only)
        /// </summary>
        public bool? IsOptedIn { get; set; }

        /// <summary>
        /// Whether address is frozen
        /// </summary>
        public bool? IsFrozen { get; set; }
    }
}
