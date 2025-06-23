using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models
{
    public class TokenDeploymentRequest
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
        /// Private key for the wallet deploying the contract
        /// In production, consider more secure ways to handle this
        /// </summary>
        [Required]
        public required string DeployerPrivateKey { get; set; }
    }

    public class TokenDeploymentResponse
    {
        /// <summary>
        /// Transaction hash of the deployment
        /// </summary>
        public string? TransactionHash { get; set; }

        /// <summary>
        /// Address of the deployed token contract
        /// </summary>
        public string? ContractAddress { get; set; }

        /// <summary>
        /// Error message if deployment failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Status of the deployment
        /// </summary>
        public bool Success { get; set; }
    }
}