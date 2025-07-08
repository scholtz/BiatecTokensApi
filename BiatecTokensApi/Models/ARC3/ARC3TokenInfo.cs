using BiatecTokensApi.Models.ASA;
using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ARC3
{
    /// <summary>
    /// Information about the created ARC3 token
    /// </summary>
    public class ARC3TokenInfo : ASATokenInfo
    {
        /// <summary>
        /// Gets or sets the ARC3 token metadata associated with this instance.
        /// </summary>
        public required ARC3TokenMetadata Metadata { get; set; }
    }
}
