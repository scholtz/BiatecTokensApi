using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Request to create or update compliance metadata for a token
    /// </summary>
    public class UpsertComplianceMetadataRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which to set compliance metadata
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// Name of the KYC/AML provider used for verification
        /// </summary>
        [MaxLength(200)]
        public string? KycProvider { get; set; }

        /// <summary>
        /// Date when KYC/AML verification was completed
        /// </summary>
        public DateTime? KycVerificationDate { get; set; }

        /// <summary>
        /// KYC/AML verification status
        /// </summary>
        public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;

        /// <summary>
        /// Jurisdiction(s) where the token is compliant (comma-separated country codes)
        /// </summary>
        [MaxLength(500)]
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Regulatory framework(s) the token complies with
        /// </summary>
        [MaxLength(500)]
        public string? RegulatoryFramework { get; set; }

        /// <summary>
        /// Compliance status of the token
        /// </summary>
        public ComplianceStatus ComplianceStatus { get; set; } = ComplianceStatus.UnderReview;

        /// <summary>
        /// Date when compliance review was last performed
        /// </summary>
        public DateTime? LastComplianceReview { get; set; }

        /// <summary>
        /// Date when the next compliance review is due
        /// </summary>
        public DateTime? NextComplianceReview { get; set; }

        /// <summary>
        /// Type of asset being tokenized
        /// </summary>
        [MaxLength(200)]
        public string? AssetType { get; set; }

        /// <summary>
        /// Restrictions on token transfers (if any)
        /// </summary>
        [MaxLength(1000)]
        public string? TransferRestrictions { get; set; }

        /// <summary>
        /// Maximum number of token holders allowed
        /// </summary>
        [Range(1, int.MaxValue)]
        public int? MaxHolders { get; set; }

        /// <summary>
        /// Whether the token requires accredited investors only
        /// </summary>
        public bool RequiresAccreditedInvestors { get; set; }

        /// <summary>
        /// Network on which the token is deployed (voimain-v1.0, aramidmain-v1.0, etc.)
        /// </summary>
        [MaxLength(50)]
        public string? Network { get; set; }

        /// <summary>
        /// Additional compliance notes
        /// </summary>
        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request to get compliance metadata for a token
    /// </summary>
    public class GetComplianceMetadataRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which to retrieve compliance metadata
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }
    }

    /// <summary>
    /// Request to delete compliance metadata for a token
    /// </summary>
    public class DeleteComplianceMetadataRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which to delete compliance metadata
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }
    }

    /// <summary>
    /// Request to list compliance metadata with filtering
    /// </summary>
    public class ListComplianceMetadataRequest
    {
        /// <summary>
        /// Optional filter by compliance status
        /// </summary>
        public ComplianceStatus? ComplianceStatus { get; set; }

        /// <summary>
        /// Optional filter by verification status
        /// </summary>
        public VerificationStatus? VerificationStatus { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        [MaxLength(50)]
        public string? Network { get; set; }

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination
        /// </summary>
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Request to validate token configuration against compliance rules
    /// </summary>
    /// <remarks>
    /// This request validates token configuration against MICA/RWA compliance rules
    /// used by frontend presets. It checks for missing whitelist controls, issuer controls,
    /// and network-specific compliance requirements.
    /// </remarks>
    public class ValidateTokenPresetRequest
    {
        /// <summary>
        /// Type of asset being tokenized (e.g., "Security Token", "Utility Token", "NFT")
        /// </summary>
        [MaxLength(200)]
        public string? AssetType { get; set; }

        /// <summary>
        /// Whether the token requires accredited investors only
        /// </summary>
        public bool RequiresAccreditedInvestors { get; set; }

        /// <summary>
        /// Whether whitelist controls are enabled for the token
        /// </summary>
        public bool HasWhitelistControls { get; set; }

        /// <summary>
        /// Whether issuer controls are enabled for the token (freeze, clawback, etc.)
        /// </summary>
        public bool HasIssuerControls { get; set; }

        /// <summary>
        /// KYC/AML verification status
        /// </summary>
        public VerificationStatus? VerificationStatus { get; set; }

        /// <summary>
        /// Jurisdiction(s) where the token is compliant (comma-separated country codes)
        /// </summary>
        [MaxLength(500)]
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Regulatory framework(s) the token complies with
        /// </summary>
        [MaxLength(500)]
        public string? RegulatoryFramework { get; set; }

        /// <summary>
        /// Compliance status of the token
        /// </summary>
        public ComplianceStatus? ComplianceStatus { get; set; }

        /// <summary>
        /// Maximum number of token holders allowed
        /// </summary>
        [Range(1, int.MaxValue)]
        public int? MaxHolders { get; set; }

        /// <summary>
        /// Network on which the token will be deployed (voimain-v1.0, aramidmain-v1.0, etc.)
        /// </summary>
        [MaxLength(50)]
        public string? Network { get; set; }

        /// <summary>
        /// Whether to include only critical errors (false) or also warnings (true)
        /// </summary>
        public bool IncludeWarnings { get; set; } = true;
    }
}
