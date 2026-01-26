using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Request model for simulating token minting with whitelist enforcement
    /// </summary>
    /// <remarks>
    /// This model is used for demonstration purposes to show how whitelist enforcement
    /// blocks minting when the recipient address is not whitelisted.
    /// </remarks>
    public class SimulateMintRequest
    {
        /// <summary>
        /// The asset ID (token ID) for the mint operation
        /// </summary>
        public required ulong AssetId { get; set; }

        /// <summary>
        /// The recipient's Algorand address
        /// </summary>
        public required string ToAddress { get; set; }

        /// <summary>
        /// The amount to mint (for demonstration purposes)
        /// </summary>
        public decimal Amount { get; set; }
    }
}
