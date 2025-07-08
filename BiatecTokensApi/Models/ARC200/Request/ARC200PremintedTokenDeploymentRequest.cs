namespace BiatecTokensApi.Models.ARC200.Request
{
    /// <summary>
    /// Represents a request to deploy a non-mintable ARC200 token contract on the blockchain.
    /// </summary>
    public class ARC200PremintedTokenDeploymentRequest : ARC200TokenDeploymentRequest
    {
        /// <summary>
        /// Gets a value indicating whether the item can be minted.
        /// </summary>
        public bool IsMintable { get; } = false;
    }
}
