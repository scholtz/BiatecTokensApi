using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ARC1400.Request
{
    /// <summary>
    /// Represents a request to deploy an ARC1400 token contract on the blockchain.
    /// </summary>
    /// <remarks>This class encapsulates the necessary parameters for deploying an ARC1400 token, including the
    /// token's name, symbol, initial supply, and other optional configuration details. Ensure all required properties
    /// are set before using this request.</remarks>
    public class ARC1400TokenDeploymentRequest
    {
        /// <summary>
        /// The name of the ARC1400 token
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// The symbol of the ARC1400 token (ticker)
        /// </summary>
        [Required]
        public required string Symbol { get; set; }

        /// <summary>
        /// Initial supply of tokens (will be multiplied by decimals)
        /// </summary>
        [Required]
        public required decimal InitialSupply { get; set; }

        /// <summary>
        /// Number of decimals for the token (typically 18)
        /// </summary>
        public int Decimals { get; set; } = 18;

        /// <summary>
        /// Address that will receive the initial token supply. 
        /// If not specified, the deployer address will be used.
        /// </summary>
        public string? InitialSupplyReceiver { get; set; }

        /// <summary>
        /// Algorand network to deploy to (mainnet-v1.0, testnet-v1.0, betanet-v1.0, voimain-v1.0, aramidmain-v1.0)
        /// </summary>
        [Required]
        public required string Network { get; set; } = "testnet-v1.0";

    }
}