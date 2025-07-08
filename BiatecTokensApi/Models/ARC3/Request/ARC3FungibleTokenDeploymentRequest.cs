using BiatecTokensApi.Models.ASA.Request;
using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ARC3.Request
{
    /// <summary>
    /// Request model for creating an ARC3 Fungible Token on Algorand
    /// </summary>
    public class ARC3FungibleTokenDeploymentRequest : ASAFungibleTokenDeploymentRequest, IARC3TokenDeploymentRequest
    {
        /// <summary>
        /// ARC3 compliant metadata for the token
        /// </summary>
        public required ARC3TokenMetadata Metadata { get; set; }
    }
}