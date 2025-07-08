using BiatecTokensApi.Models.EVM;

namespace BiatecTokensApi.Models.ERC20.Response
{
    /// <summary>
    /// Represents the response received after deploying an ERC-20 token contract.
    /// </summary>
    /// <remarks>This response includes details about the deployed token contract, such as its
    /// address.</remarks>
    public class ERC20TokenDeploymentResponse : EVMTokenDeploymentResponse
    {
        /// <summary>
        /// Address of the deployed token contract
        /// </summary>
        public required string ContractAddress { get; set; }
    }
}
