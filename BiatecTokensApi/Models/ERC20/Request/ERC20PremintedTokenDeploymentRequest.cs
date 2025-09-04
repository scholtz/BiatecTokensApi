namespace BiatecTokensApi.Models.ERC20.Request
{
    /// <summary>
    /// Represents a request to deploy a non-mintable ERC20 token contract on the blockchain.
    /// </summary>
    public class ERC20PremintedTokenDeploymentRequest : ERC20TokenDeploymentRequest
    {
        /// <summary>
        /// Gets a value indicating whether the item can be minted.
        /// </summary>
        public bool IsMintable { get; } = false;
    }
}
