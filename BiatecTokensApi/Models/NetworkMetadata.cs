namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Represents metadata for a supported blockchain network
    /// </summary>
    public class NetworkMetadata
    {
        /// <summary>
        /// Network identifier (e.g., "mainnet", "testnet", "betanet")
        /// </summary>
        public string NetworkId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable network name (e.g., "Algorand Mainnet", "Base Mainnet")
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Blockchain type (e.g., "algorand", "evm")
        /// </summary>
        public string BlockchainType { get; set; } = string.Empty;

        /// <summary>
        /// Whether this is a production-ready mainnet network
        /// </summary>
        public bool IsMainnet { get; set; }

        /// <summary>
        /// Whether this network is recommended for production use
        /// </summary>
        public bool IsRecommended { get; set; }

        /// <summary>
        /// RPC endpoint URL (for EVM chains) or API server URL (for Algorand)
        /// </summary>
        public string? EndpointUrl { get; set; }

        /// <summary>
        /// Chain ID (for EVM chains only)
        /// </summary>
        public int? ChainId { get; set; }

        /// <summary>
        /// Genesis hash (for Algorand networks only)
        /// </summary>
        public string? GenesisHash { get; set; }

        /// <summary>
        /// Additional network-specific properties
        /// </summary>
        public Dictionary<string, object>? Properties { get; set; }
    }

    /// <summary>
    /// Response containing supported network metadata
    /// </summary>
    public class NetworkMetadataResponse
    {
        /// <summary>
        /// Indicates if the request was successful
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// List of supported blockchain networks
        /// </summary>
        public List<NetworkMetadata> Networks { get; set; } = new();

        /// <summary>
        /// Recommended networks for production deployment (mainnets)
        /// </summary>
        public List<string> RecommendedNetworks { get; set; } = new();

        /// <summary>
        /// Error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Timestamp of the response
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
