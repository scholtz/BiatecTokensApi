namespace BiatecTokensApi.Configuration
{
    /// <summary>
    /// Represents a collection of blockchain configurations for supported Ethereum Virtual Machine (EVM) chains.
    /// </summary>
    /// <remarks>This class provides a centralized way to manage and access configurations for multiple
    /// EVM-compatible blockchains. Each configuration in the <see cref="Chains"/> property defines the settings and
    /// parameters for a specific chain.</remarks>
    public class EVMChains
    {
        /// <summary>
        /// Gets or sets the collection of blockchain configurations for supported EVM chains.
        /// </summary>
        public List<EVMBlockchainConfig> Chains { get; set; } = new List<EVMBlockchainConfig>();
    }
}
