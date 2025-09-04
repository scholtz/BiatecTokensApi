using BiatecTokensApi.Models.ASA;
using BiatecTokensApi.Models.AVM;

namespace BiatecTokensApi.Models.ARC3.Response
{
    /// <summary>
    /// Response model for ARC3 token deployment
    /// </summary>
    public class ASATokenDeploymentResponse : AVMTokenDeploymentResponse
    {

        /// <summary>
        /// Token configuration details
        /// </summary>
        public Algorand.Algod.Model.Asset? TokenInfo { get; set; }

    }
}
