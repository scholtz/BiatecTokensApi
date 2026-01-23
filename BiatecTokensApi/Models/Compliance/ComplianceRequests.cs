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

    /// <summary>
    /// Request to create or update issuer profile
    /// </summary>
    public class UpsertIssuerProfileRequest
    {
        /// <summary>
        /// Legal entity name
        /// </summary>
        [Required]
        [StringLength(200)]
        public string LegalName { get; set; } = string.Empty;

        /// <summary>
        /// Doing Business As (DBA) name
        /// </summary>
        [StringLength(200)]
        public string? DoingBusinessAs { get; set; }

        /// <summary>
        /// Entity type (Corporation, LLC, DAO, etc.)
        /// </summary>
        [StringLength(100)]
        public string? EntityType { get; set; }

        /// <summary>
        /// Country of incorporation (ISO country code)
        /// </summary>
        [Required]
        [StringLength(2)]
        public string CountryOfIncorporation { get; set; } = string.Empty;

        /// <summary>
        /// Tax identification number
        /// </summary>
        [StringLength(100)]
        public string? TaxIdentificationNumber { get; set; }

        /// <summary>
        /// Business registration number
        /// </summary>
        [StringLength(100)]
        public string? RegistrationNumber { get; set; }

        /// <summary>
        /// Registered business address
        /// </summary>
        public IssuerAddress? RegisteredAddress { get; set; }

        /// <summary>
        /// Operational/mailing address
        /// </summary>
        public IssuerAddress? OperationalAddress { get; set; }

        /// <summary>
        /// Primary contact information
        /// </summary>
        public IssuerContact? PrimaryContact { get; set; }

        /// <summary>
        /// Compliance officer contact
        /// </summary>
        public IssuerContact? ComplianceContact { get; set; }

        /// <summary>
        /// Website URL
        /// </summary>
        [StringLength(500)]
        public string? Website { get; set; }

        /// <summary>
        /// KYB provider name
        /// </summary>
        [StringLength(200)]
        public string? KybProvider { get; set; }

        /// <summary>
        /// MICA license number
        /// </summary>
        [StringLength(100)]
        public string? MicaLicenseNumber { get; set; }

        /// <summary>
        /// MICA competent authority
        /// </summary>
        [StringLength(200)]
        public string? MicaCompetentAuthority { get; set; }

        /// <summary>
        /// Additional notes
        /// </summary>
        [StringLength(2000)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request to add a blacklist entry
    /// </summary>
    public class AddBlacklistEntryRequest
    {
        /// <summary>
        /// Blacklisted address
        /// </summary>
        [Required]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Asset ID (token ID), or null for global blacklist
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Reason for blacklisting
        /// </summary>
        [Required]
        [StringLength(1000)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Blacklist category
        /// </summary>
        public BlacklistCategory Category { get; set; }

        /// <summary>
        /// Network where blacklist applies
        /// </summary>
        [StringLength(50)]
        public string? Network { get; set; }

        /// <summary>
        /// Jurisdiction that issued blacklist
        /// </summary>
        [StringLength(200)]
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Source of blacklist (OFAC, FinCEN, Chainalysis, etc.)
        /// </summary>
        [StringLength(200)]
        public string? Source { get; set; }

        /// <summary>
        /// Reference number or case ID
        /// </summary>
        [StringLength(200)]
        public string? ReferenceId { get; set; }

        /// <summary>
        /// Date blacklist entry becomes effective
        /// </summary>
        public DateTime? EffectiveDate { get; set; }

        /// <summary>
        /// Date blacklist entry expires (null for permanent)
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Additional notes
        /// </summary>
        [StringLength(2000)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request to check blacklist status
    /// </summary>
    public class CheckBlacklistRequest
    {
        /// <summary>
        /// Address to check
        /// </summary>
        [Required]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Optional asset ID for asset-specific blacklist check
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional network filter
        /// </summary>
        [StringLength(50)]
        public string? Network { get; set; }
    }

    /// <summary>
    /// Request to list blacklist entries
    /// </summary>
    public class ListBlacklistEntriesRequest
    {
        /// <summary>
        /// Optional address filter
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Optional asset ID filter
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional category filter
        /// </summary>
        public BlacklistCategory? Category { get; set; }

        /// <summary>
        /// Optional status filter
        /// </summary>
        public BlacklistStatus? Status { get; set; }

        /// <summary>
        /// Optional network filter
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Page number (default: 1)
        /// </summary>
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size (default: 20, max: 100)
        /// </summary>
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Request to validate a transfer
    /// </summary>
    public class ValidateTransferRequest
    {
        /// <summary>
        /// Asset ID
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// From address
        /// </summary>
        [Required]
        public string FromAddress { get; set; } = string.Empty;

        /// <summary>
        /// To address
        /// </summary>
        [Required]
        public string ToAddress { get; set; } = string.Empty;

        /// <summary>
        /// Amount to transfer
        /// </summary>
        [Required]
        [Range(1, long.MaxValue)]
        public long Amount { get; set; }

        /// <summary>
        /// Network
        /// </summary>
        [StringLength(50)]
        public string? Network { get; set; }
    }

    /// <summary>
    /// Request to list issuer assets
    /// </summary>
    public class ListIssuerAssetsRequest
    {
        /// <summary>
        /// Optional network filter
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional compliance status filter
        /// </summary>
        public ComplianceStatus? ComplianceStatus { get; set; }

        /// <summary>
        /// Page number (default: 1)
        /// </summary>
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size (default: 20, max: 100)
        /// </summary>
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;
    }
}
