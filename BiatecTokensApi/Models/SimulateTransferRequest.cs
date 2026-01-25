using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Request model for simulating a token transfer with whitelist enforcement
    /// </summary>
    /// <remarks>
    /// This model is used for demonstration purposes to show how whitelist enforcement
    /// blocks transfers when addresses are not whitelisted.
    /// </remarks>
    public class SimulateTransferRequest
    {
        /// <summary>
        /// The asset ID (token ID) for the transfer
        /// </summary>
        [Required]
        public required ulong AssetId { get; set; }

        /// <summary>
        /// The sender's Algorand address
        /// </summary>
        [Required]
        public required string FromAddress { get; set; }

        /// <summary>
        /// The receiver's Algorand address
        /// </summary>
        [Required]
        public required string ToAddress { get; set; }

        /// <summary>
        /// The amount to transfer (for demonstration purposes)
        /// </summary>
        public decimal Amount { get; set; }
    }
}
