using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Comprehensive token metadata model for consistent token information across all token types
    /// </summary>
    /// <remarks>
    /// This model provides a canonical schema for token metadata that can be used by wallet integrations,
    /// frontends, and other consumers. It includes all essential fields needed for proper token display
    /// and user experience, with validation and fallback support.
    /// </remarks>
    public class EnrichedTokenMetadata
    {
        /// <summary>
        /// Token name (e.g., "Biatec Token", "USD Coin")
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public required string Name { get; set; }

        /// <summary>
        /// Token symbol/ticker (e.g., "BIAT", "USDC")
        /// </summary>
        [Required]
        [StringLength(20, MinimumLength = 1)]
        public required string Symbol { get; set; }

        /// <summary>
        /// Number of decimal places for the token (typically 18 for ERC20, 6 for ASA)
        /// </summary>
        [Range(0, 18)]
        public int Decimals { get; set; }

        /// <summary>
        /// Human-readable description of the token and its purpose
        /// </summary>
        [StringLength(5000)]
        public string? Description { get; set; }

        /// <summary>
        /// URL to token logo image (PNG, JPG, SVG)
        /// </summary>
        [Url]
        [StringLength(500)]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Official token website URL
        /// </summary>
        [Url]
        [StringLength(500)]
        public string? WebsiteUrl { get; set; }

        /// <summary>
        /// Blockchain explorer URL for this specific token
        /// </summary>
        [Url]
        [StringLength(500)]
        public string? ExplorerUrl { get; set; }

        /// <summary>
        /// Documentation or whitepaper URL
        /// </summary>
        [Url]
        [StringLength(500)]
        public string? DocumentationUrl { get; set; }

        /// <summary>
        /// Additional metadata URLs (social media, GitHub, etc.)
        /// </summary>
        public Dictionary<string, string>? AdditionalLinks { get; set; }

        /// <summary>
        /// Tags for categorization (e.g., "DeFi", "RWA", "Stablecoin")
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Token identifier on the blockchain (asset ID for Algorand, contract address for EVM)
        /// </summary>
        [Required]
        public required string TokenIdentifier { get; set; }

        /// <summary>
        /// Blockchain network identifier (e.g., "algorand-mainnet", "base-mainnet")
        /// </summary>
        [Required]
        public required string Chain { get; set; }

        /// <summary>
        /// Token standard(s) supported (e.g., "ERC20", "ASA", "ARC3", "ARC200")
        /// </summary>
        public List<string> Standards { get; set; } = new();

        /// <summary>
        /// Total supply of tokens (as string to support large numbers)
        /// </summary>
        public string? TotalSupply { get; set; }

        /// <summary>
        /// Metadata completeness score (0-100)
        /// </summary>
        /// <remarks>
        /// Calculated based on presence and validity of optional fields.
        /// 100 = all recommended fields present, 0 = only required fields.
        /// </remarks>
        [Range(0, 100)]
        public int CompletenessScore { get; set; }

        /// <summary>
        /// Timestamp when metadata was last updated
        /// </summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when metadata was first created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Validation status of this metadata
        /// </summary>
        public TokenMetadataValidationStatus ValidationStatus { get; set; } = TokenMetadataValidationStatus.NotValidated;

        /// <summary>
        /// List of validation warnings or issues
        /// </summary>
        public List<TokenMetadataValidationIssue> ValidationIssues { get; set; } = new();

        /// <summary>
        /// Whether this metadata has been verified by the platform
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// Source of this metadata (e.g., "platform", "user-submitted", "ipfs", "chain")
        /// </summary>
        public string DataSource { get; set; } = "platform";
    }

    /// <summary>
    /// Validation status for token metadata
    /// </summary>
    public enum TokenMetadataValidationStatus
    {
        /// <summary>
        /// Metadata has not been validated
        /// </summary>
        NotValidated,

        /// <summary>
        /// Validation is in progress
        /// </summary>
        Validating,

        /// <summary>
        /// Metadata is valid and complete
        /// </summary>
        Valid,

        /// <summary>
        /// Metadata is valid but has some missing optional fields
        /// </summary>
        ValidWithWarnings,

        /// <summary>
        /// Metadata has validation errors
        /// </summary>
        Invalid
    }

    /// <summary>
    /// Individual validation issue for token metadata
    /// </summary>
    public class TokenMetadataValidationIssue
    {
        /// <summary>
        /// Error code for programmatic handling
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Field name that has an issue
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable message describing the issue
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Severity of the issue
        /// </summary>
        public TokenMetadataIssueSeverity Severity { get; set; } = TokenMetadataIssueSeverity.Warning;

        /// <summary>
        /// Suggested remediation or fix for the issue
        /// </summary>
        public string? Remediation { get; set; }
    }

    /// <summary>
    /// Severity levels for metadata validation issues
    /// </summary>
    public enum TokenMetadataIssueSeverity
    {
        /// <summary>
        /// Informational message
        /// </summary>
        Info,

        /// <summary>
        /// Warning that should be addressed but doesn't block usage
        /// </summary>
        Warning,

        /// <summary>
        /// Error that should be fixed for proper functionality
        /// </summary>
        Error
    }

    /// <summary>
    /// Request to retrieve or update token metadata
    /// </summary>
    public class GetTokenMetadataRequest
    {
        /// <summary>
        /// Token identifier (asset ID or contract address)
        /// </summary>
        [Required]
        public required string TokenIdentifier { get; set; }

        /// <summary>
        /// Blockchain network identifier
        /// </summary>
        [Required]
        public required string Chain { get; set; }

        /// <summary>
        /// Whether to include validation details in the response
        /// </summary>
        public bool IncludeValidation { get; set; } = true;
    }

    /// <summary>
    /// Response containing token metadata
    /// </summary>
    public class GetTokenMetadataResponse : BaseResponse
    {
        /// <summary>
        /// Token metadata, or null if not found
        /// </summary>
        public EnrichedTokenMetadata? Metadata { get; set; }

        /// <summary>
        /// Whether the token was found
        /// </summary>
        public bool Found { get; set; }
    }

    /// <summary>
    /// Request to update token metadata
    /// </summary>
    public class UpdateTokenMetadataRequest
    {
        /// <summary>
        /// Token identifier
        /// </summary>
        [Required]
        public required string TokenIdentifier { get; set; }

        /// <summary>
        /// Blockchain network identifier
        /// </summary>
        [Required]
        public required string Chain { get; set; }

        /// <summary>
        /// Token description
        /// </summary>
        [StringLength(5000)]
        public string? Description { get; set; }

        /// <summary>
        /// Token logo image URL
        /// </summary>
        [Url]
        [StringLength(500)]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Official website URL
        /// </summary>
        [Url]
        [StringLength(500)]
        public string? WebsiteUrl { get; set; }

        /// <summary>
        /// Documentation URL
        /// </summary>
        [Url]
        [StringLength(500)]
        public string? DocumentationUrl { get; set; }

        /// <summary>
        /// Additional metadata links
        /// </summary>
        public Dictionary<string, string>? AdditionalLinks { get; set; }

        /// <summary>
        /// Tags for categorization
        /// </summary>
        public List<string>? Tags { get; set; }
    }

    /// <summary>
    /// Response from metadata update operation
    /// </summary>
    public class UpdateTokenMetadataResponse : BaseResponse
    {
        /// <summary>
        /// Updated token metadata
        /// </summary>
        public EnrichedTokenMetadata? Metadata { get; set; }

        /// <summary>
        /// Whether the update was successful
        /// </summary>
        public bool Updated { get; set; }
    }
}
