using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ERC20.Request
{
    /// <summary>
    /// Represents a request to deploy an ERC20 token contract on the blockchain.
    /// </summary>
    /// <remarks>This class encapsulates the necessary parameters for deploying an ERC20 token, including the
    /// token's name, symbol, initial supply, and other optional configuration details. Ensure all required properties
    /// are set before using this request.</remarks>
    public class ERC20TokenDeploymentRequest
    {
        /// <summary>
        /// The name of the ERC20 token
        /// </summary>
        [Required]
        public required string Name { get; set; }

        /// <summary>
        /// The symbol of the ERC20 token (ticker)
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
        /// EVM chain id
        /// </summary>
        [Required]
        public required ulong ChainId { get; set; } = 1;

        /// <summary>
        /// Optional compliance metadata for MICA/RWA tokens
        /// </summary>
        public TokenDeploymentComplianceMetadata? ComplianceMetadata { get; set; }

    }
}