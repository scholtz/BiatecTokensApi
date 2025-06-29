using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Request model for creating an ARC3 Fungible Token on Algorand
    /// </summary>
    public class ARC3FungibleTokenDeploymentRequest
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
        /// Mnemonic phrase for the creator account
        /// </summary>
        [Required]
        public required string CreatorMnemonic { get; set; }

        /// <summary>
        /// Algorand network to deploy to (mainnet, testnet, betanet)
        /// </summary>
        [Required]
        public required string Network { get; set; } = "testnet";

        /// <summary>
        /// ARC3 compliant metadata for the token
        /// </summary>
        public ARC3TokenMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// ARC3 compliant token metadata structure
    /// </summary>
    public class ARC3TokenMetadata
    {
        /// <summary>
        /// Identifies the asset to which this token represents
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The number of decimal places that the token amount should display
        /// </summary>
        public int? Decimals { get; set; }

        /// <summary>
        /// Describes the asset to which this token represents
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// A URI pointing to a file with MIME type image/* representing the asset
        /// </summary>
        public string? Image { get; set; }

        /// <summary>
        /// The SHA-256 digest of the file pointed by the URI image
        /// </summary>
        public string? ImageIntegrity { get; set; }

        /// <summary>
        /// The MIME type of the file pointed by the URI image. MUST be of the form 'image/*'
        /// </summary>
        public string? ImageMimetype { get; set; }

        /// <summary>
        /// Background color to display the asset. MUST be a six-character hexadecimal without a pre-pended #
        /// </summary>
        [RegularExpression(@"^[0-9A-Fa-f]{6}$", ErrorMessage = "Background color must be a six-character hexadecimal without #")]
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// A URI pointing to an external website presenting the asset
        /// </summary>
        public string? ExternalUrl { get; set; }

        /// <summary>
        /// The SHA-256 digest of the file pointed by the URI external_url
        /// </summary>
        public string? ExternalUrlIntegrity { get; set; }

        /// <summary>
        /// The MIME type of the file pointed by the URI external_url
        /// </summary>
        public string? ExternalUrlMimetype { get; set; }

        /// <summary>
        /// A URI pointing to a multi-media file representing the asset
        /// </summary>
        public string? AnimationUrl { get; set; }

        /// <summary>
        /// The SHA-256 digest of the file pointed by the URI animation_url
        /// </summary>
        public string? AnimationUrlIntegrity { get; set; }

        /// <summary>
        /// The MIME type of the file pointed by the URI animation_url
        /// </summary>
        public string? AnimationUrlMimetype { get; set; }

        /// <summary>
        /// Arbitrary properties (also called attributes). Values may be strings, numbers, object or arrays
        /// </summary>
        public Dictionary<string, object>? Properties { get; set; }

        /// <summary>
        /// Extra metadata in base64
        /// </summary>
        public string? ExtraMetadata { get; set; }

        /// <summary>
        /// Localization information for the metadata
        /// </summary>
        public ARC3TokenLocalization? Localization { get; set; }
    }

    /// <summary>
    /// Localization information for ARC3 token metadata
    /// </summary>
    public class ARC3TokenLocalization
    {
        /// <summary>
        /// The URI pattern to fetch localized data from. This URI should contain the substring `{locale}`
        /// </summary>
        [Required]
        public required string Uri { get; set; }

        /// <summary>
        /// The locale of the default data within the base JSON
        /// </summary>
        [Required]
        public required string Default { get; set; }

        /// <summary>
        /// The list of locales for which data is available
        /// </summary>
        [Required]
        public required List<string> Locales { get; set; }

        /// <summary>
        /// The SHA-256 digests of the localized JSON files (except the default one)
        /// </summary>
        public Dictionary<string, string>? Integrity { get; set; }
    }

    /// <summary>
    /// Response model for ARC3 token deployment
    /// </summary>
    public class ARC3TokenDeploymentResponse
    {
        /// <summary>
        /// Transaction ID of the asset creation
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// Asset ID of the created token
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Creator account address
        /// </summary>
        public string? CreatorAddress { get; set; }

        /// <summary>
        /// Error message if deployment failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Status of the deployment
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Round number when the transaction was confirmed
        /// </summary>
        public ulong? ConfirmedRound { get; set; }

        /// <summary>
        /// Token configuration details
        /// </summary>
        public ARC3TokenInfo? TokenInfo { get; set; }

        /// <summary>
        /// Generated metadata URL if metadata was uploaded
        /// </summary>
        public string? MetadataUrl { get; set; }

        /// <summary>
        /// Hash of the uploaded metadata
        /// </summary>
        public string? MetadataHash { get; set; }
    }

    /// <summary>
    /// Information about the created ARC3 token
    /// </summary>
    public class ARC3TokenInfo
    {
        public string Name { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public ulong TotalSupply { get; set; }
        public uint Decimals { get; set; }
        public string? Url { get; set; }
        public byte[]? MetadataHash { get; set; }
        public bool DefaultFrozen { get; set; }
        public string? ManagerAddress { get; set; }
        public string? ReserveAddress { get; set; }
        public string? FreezeAddress { get; set; }
        public string? ClawbackAddress { get; set; }
        public ARC3TokenMetadata? Metadata { get; set; }
    }
}