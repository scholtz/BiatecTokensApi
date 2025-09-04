using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ASA.Request
{
    /// <summary>
    /// Request model for creating a Fractional Non-Fungible Token on Algorand
    /// </summary>
    /// <remarks>
    /// This model extends the base token deployment request with additional properties specific to fractional non-fungible tokens.
    /// </remarks>
    public class ASAFractionalNonFungibleTokenDeploymentRequest : ASABaseTokenDeploymentRequest
    {
        /// <summary>
        /// Total supply of tokens
        /// </summary>
        [Required]
        [Range(1, ulong.MaxValue, ErrorMessage = "Total supply must be equal to 10^decimals.")]
        public required ulong TotalSupply { get; set; }

        /// <summary>
        /// Number of decimal places for the token (0-19)
        /// </summary>
        [Range(0, 19, ErrorMessage = "Decimals must be between 0 and 19")]
        public uint Decimals { get; set; } = 6;

    }
}
