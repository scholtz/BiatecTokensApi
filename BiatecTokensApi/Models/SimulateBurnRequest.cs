using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Request model for simulating token burning with whitelist enforcement
    /// </summary>
    /// <remarks>
    /// This model is used for demonstration purposes to show how whitelist enforcement
    /// blocks burning when the token holder address is not whitelisted.
    /// </remarks>
    public class SimulateBurnRequest
    {
        /// <summary>
        /// The asset ID (token ID) for the burn operation
        /// </summary>
        [Required]
        public required ulong AssetId { get; set; }

        /// <summary>
        /// The token holder's Algorand address
        /// </summary>
        [Required]
        public required string FromAddress { get; set; }

        /// <summary>
        /// The amount to burn (for demonstration purposes)
        /// </summary>
        public decimal Amount { get; set; }
    }
}
