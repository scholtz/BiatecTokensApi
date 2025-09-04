using BiatecTokensApi.Models.AVM;

namespace BiatecTokensApi.Models.ARC1400.Response
{
    /// <summary>
    /// Represents the response received after deploying an ERC-20 token contract.
    /// </summary>
    /// <remarks>This response includes details about the deployed token contract, such as its
    /// address.</remarks>
    public class ARC1400TokenDeploymentResponse : AVMTokenDeploymentResponse
    {

        /// <summary>
        /// Deployed token contract app id
        /// </summary>
        public required ulong AppId { get; set; }
    }
}
