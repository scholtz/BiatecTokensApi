using BiatecTokensApi.Models.ASA.Request;

namespace BiatecTokensApi.Models.ARC3.Request
{
    /// <summary>
    /// Represents a request to deploy an ARC3-compliant non-fungible token (NFT).
    /// </summary>
    /// <remarks>This class encapsulates the necessary metadata required for deploying an ARC3-compliant NFT.
    /// The metadata must adhere to the ARC3 standard to ensure compatibility with supported platforms and
    /// tools.</remarks>
    public class ARC3FractionalNonFungibleTokenDeploymentRequest : ASAFractionalNonFungibleTokenDeploymentRequest, IARC3TokenDeploymentRequest
    {
        /// <summary>
        /// ARC3 compliant metadata for the token
        /// </summary>
        public required ARC3TokenMetadata Metadata { get; set; }
    }
}
