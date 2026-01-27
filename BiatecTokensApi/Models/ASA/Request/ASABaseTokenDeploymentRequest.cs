using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.Filters;
using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ASA.Request
{
    /// <summary>
    /// Request model for creating an ASA Token on Algorand
    /// </summary>
    public class ASABaseTokenDeploymentRequest
    {
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
        /// Algorand network to deploy to (mainnet-v1.0, testnet-v1.0, betanet-v1.0, voimain-v1.0, aramidmain-v1.0)
        /// </summary>
        [Required]
        

        public required string Network { get; set; } = "testnet-v1.0";

        /// <summary>
        /// Optional compliance metadata for MICA/RWA tokens
        /// </summary>
        public TokenDeploymentComplianceMetadata? ComplianceMetadata { get; set; }
    }
}