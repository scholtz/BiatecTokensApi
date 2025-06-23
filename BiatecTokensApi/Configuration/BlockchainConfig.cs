namespace BiatecTokensApi.Configuration
{
    public class BlockchainConfig
    {
        /// <summary>
        /// RPC URL for the Base blockchain (can be mainnet or testnet)
        /// </summary>
        public string? BaseRpcUrl { get; set; }
        
        /// <summary>
        /// Chain ID for Base Mainnet (8453) or Base Sepolia Testnet (84532)
        /// </summary>
        public int ChainId { get; set; }
        
        /// <summary>
        /// Gas limit for token deployment
        /// </summary>
        public int GasLimit { get; set; } = 4500000;
    }
}