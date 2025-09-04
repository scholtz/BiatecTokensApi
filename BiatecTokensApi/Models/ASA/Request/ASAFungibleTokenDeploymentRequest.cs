using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ASA.Request
{
    /// <summary>
    /// Request model for creating an ARC3 Fungible Token on Algorand
    /// </summary>
    public class ASAFungibleTokenDeploymentRequest : ASABaseTokenDeploymentRequest
    {
        /// <summary>
        /// Total supply of tokens
        /// </summary>
        [Required]
        [Range(1, ulong.MaxValue, ErrorMessage = "Total supply must be greater than 0")]
        public required ulong TotalSupply { get; set; }

        /// <summary>
        /// Number of decimal places for the token (0-19)
        /// </summary>
        [Range(0, 19, ErrorMessage = "Decimals must be between 0 and 19")]
        public uint Decimals { get; set; } = 6;

    }
}