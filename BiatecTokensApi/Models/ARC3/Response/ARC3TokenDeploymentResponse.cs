using BiatecTokensApi.Models.AVM;

namespace BiatecTokensApi.Models.ARC3.Response
{
    /// <summary>
    /// Response model for ARC3 token deployment
    /// </summary>
    public class ARC3TokenDeploymentResponse : AVMTokenDeploymentResponse
    {
        /// <summary>
        /// Token configuration details
        /// </summary>
        public Algorand.Algod.Model.Asset? TokenInfo { get; set; } = new Algorand.Algod.Model.Asset();

        /// <summary>
        /// Generated metadata URL if metadata was uploaded
        /// </summary>
        public string? MetadataUrl { get; set; }

        /// <summary>
        /// Hash of the uploaded metadata
        /// </summary>
        public string? MetadataHash { get; set; }
    }
}
