using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ARC200.Request
{
    /// <summary>
    /// Represents a request to deploy an ERC-20 token with mintable functionality.
    /// </summary>
    /// <remarks>This class extends <see cref="ARC200TokenDeploymentRequest"/> to include the mintable
    /// property, indicating that the deployed token will support minting of additional tokens after
    /// deployment.</remarks>
    public class ARC200MintableTokenDeploymentRequest : ARC200TokenDeploymentRequest
    {
        /// <summary>
        /// Gets a value indicating whether the item can be minted.
        /// </summary>
        public bool IsMintable { get; } = true;

        /// <summary>
        /// Cap of tokens (will be multiplied by decimals)
        /// </summary>
        [Required]
        public required decimal Cap { get; set; }
    }
}
