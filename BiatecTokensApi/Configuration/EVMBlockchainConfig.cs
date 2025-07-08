namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// Represents the configuration settings for an EVM-compatible blockchain, such as Base Mainnet or Base Sepolia
    /// Testnet.
    /// </summary>
    /// <remarks>This class provides the necessary parameters to interact with an EVM-compatible blockchain,
    /// including the RPC URL, chain ID, and gas limit. It is typically used to configure blockchain-related operations
    /// or services.</remarks>
    public class EVMBlockchainConfig
    {
        /// <summary>
        /// RPC URL for the Base blockchain (can be mainnet or testnet)
        /// </summary>
        public string? RpcUrl { get; set; }
        
        /// <summary>
        /// Chain ID for Base Mainnet (8453) or Base Sepolia Testnet (84532), or others
        /// </summary>
        public int ChainId { get; set; }
        
        /// <summary>
        /// Gas limit for token deployment
        /// </summary>
        public int GasLimit { get; set; } = 4500000;
    }
}