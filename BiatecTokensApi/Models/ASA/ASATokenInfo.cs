using BiatecTokensApi.Models.ARC3;
using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ASA
{
    /// <summary>
    /// Information about the created ARC3 token
    /// </summary>
    public class ASATokenInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the ASA (Algorand Standard Asset).
        /// </summary>
        public required ulong Id { get; set; }
        /// <summary>
        /// The name of the ARC3 token
        /// </summary>
        [Required]
        [StringLength(32, ErrorMessage = "Token name cannot exceed 32 characters")]
        public required string Name { get; set; }

        /// <summary>
        /// The unit name (symbol) of the ARC3 token
        /// </summary>
        [Required]
        [StringLength(8, ErrorMessage = "Unit name cannot exceed 8 characters")]
        public required string UnitName { get; set; }

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
        public uint Decimals { get; set; } = 0;

        /// <summary>
        /// Optional URL for token metadata
        /// </summary>
        [StringLength(96, ErrorMessage = "URL cannot exceed 96 characters")]
        public string? Url { get; set; }

        /// <summary>
        /// Optional metadata hash (32 bytes)
        /// </summary>
        public byte[]? MetadataHash { get; set; }

        /// <summary>
        /// Whether the asset can be frozen by the freeze address
        /// </summary>
        public bool DefaultFrozen { get; set; } = false;

        /// <summary>
        /// Address that can manage the asset configuration (optional)
        /// If not provided, the creator will be the manager
        /// </summary>
        public string? ManagerAddress { get; set; }

        /// <summary>
        /// Address that can reserve tokens (optional)
        /// </summary>
        public string? ReserveAddress { get; set; }

        /// <summary>
        /// Address that can freeze/unfreeze tokens (optional)
        /// </summary>
        public string? FreezeAddress { get; set; }

        /// <summary>
        /// Address that can clawback tokens (optional)
        /// </summary>
        public string? ClawbackAddress { get; set; }
        /// <summary>
        /// Gets or sets the name of the network configuration.
        /// </summary>
        public string Network { get; set; } = "mainnet-v1.0";
    }
}
