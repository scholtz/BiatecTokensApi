using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models
{
    /// <summary>
    /// Compliance metadata for token deployment requests (MICA/RWA)
    /// </summary>
    /// <remarks>
    /// This class captures compliance information during token deployment,
    /// including issuer details, jurisdiction, regulatory disclosures, and whitelist policies.
    /// Required for RWA tokens, optional for utility tokens.
    /// </remarks>
    public class TokenDeploymentComplianceMetadata
    {
        /// <summary>
        /// Legal name of the token issuer
        /// </summary>
        [MaxLength(200)]
        public string? IssuerName { get; set; }

        /// <summary>
        /// Jurisdiction(s) where the token is compliant (comma-separated country codes, e.g., "US,EU,GB")
        /// </summary>
        [MaxLength(500)]
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Type of asset being tokenized (e.g., "Security Token", "Real Estate", "Utility Token")
        /// </summary>
        [MaxLength(200)]
        public string? AssetType { get; set; }

        /// <summary>
        /// Regulatory framework(s) the token complies with (e.g., "SEC Reg D", "MiFID II", "MICA")
        /// </summary>
        [MaxLength(500)]
        public string? RegulatoryFramework { get; set; }

        /// <summary>
        /// URL to regulatory disclosure documents
        /// </summary>
        [MaxLength(1000)]
        public string? DisclosureUrl { get; set; }

        /// <summary>
        /// Whether the token requires a whitelist for transfers
        /// </summary>
        public bool RequiresWhitelist { get; set; }

        /// <summary>
        /// Whether the token requires accredited investors only
        /// </summary>
        public bool RequiresAccreditedInvestors { get; set; }

        /// <summary>
        /// Maximum number of token holders allowed (for regulated securities)
        /// </summary>
        [Range(1, int.MaxValue)]
        public int? MaxHolders { get; set; }

        /// <summary>
        /// Restrictions on token transfers (if any)
        /// </summary>
        [MaxLength(1000)]
        public string? TransferRestrictions { get; set; }

        /// <summary>
        /// Name of the KYC/AML provider used for verification
        /// </summary>
        [MaxLength(200)]
        public string? KycProvider { get; set; }

        /// <summary>
        /// Additional compliance notes
        /// </summary>
        [MaxLength(2000)]
        public string? Notes { get; set; }
    }
}
