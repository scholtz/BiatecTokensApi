using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents a wallet-level compliance attestation for regulatory audit trails
    /// </summary>
    /// <remarks>
    /// Attestations provide cryptographic proof of compliance verification tied to wallet addresses.
    /// Used for MICA/RWA workflows to maintain persistent compliance audit trails for issuers.
    /// </remarks>
    public class ComplianceAttestation
    {
        /// <summary>
        /// Unique identifier for the attestation entry
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The wallet address for which this attestation applies
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string WalletAddress { get; set; } = string.Empty;

        /// <summary>
        /// The asset ID (token ID) this attestation is linked to
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// The issuer address who created this attestation
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string IssuerAddress { get; set; } = string.Empty;

        /// <summary>
        /// Cryptographic hash of compliance proof document
        /// </summary>
        /// <remarks>
        /// Can be IPFS CID, SHA-256 hash, or other cryptographic proof identifier
        /// </remarks>
        [Required]
        [MaxLength(200)]
        public string ProofHash { get; set; } = string.Empty;

        /// <summary>
        /// Type of proof provided (e.g., "IPFS", "SHA256", "ARC19")
        /// </summary>
        [MaxLength(50)]
        public string? ProofType { get; set; }

        /// <summary>
        /// Verification status of this attestation
        /// </summary>
        public AttestationVerificationStatus VerificationStatus { get; set; } = AttestationVerificationStatus.Pending;

        /// <summary>
        /// Type of attestation (e.g., KYC, AML, Accreditation, License)
        /// </summary>
        [MaxLength(100)]
        public string? AttestationType { get; set; }

        /// <summary>
        /// Network on which the token is deployed
        /// </summary>
        [MaxLength(50)]
        public string? Network { get; set; }

        /// <summary>
        /// Jurisdiction(s) applicable to this attestation
        /// </summary>
        [MaxLength(500)]
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Regulatory framework this attestation complies with
        /// </summary>
        [MaxLength(500)]
        public string? RegulatoryFramework { get; set; }

        /// <summary>
        /// Date when the attestation was issued
        /// </summary>
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date when the attestation expires (if applicable)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Date when the attestation was verified
        /// </summary>
        public DateTime? VerifiedAt { get; set; }

        /// <summary>
        /// Address of the verifier (if different from issuer)
        /// </summary>
        [MaxLength(100)]
        public string? VerifierAddress { get; set; }

        /// <summary>
        /// Additional metadata or notes about the attestation
        /// </summary>
        [MaxLength(2000)]
        public string? Notes { get; set; }

        /// <summary>
        /// Timestamp when the attestation was created in the system
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the attestation was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Address of the user who created this attestation in the system
        /// </summary>
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Address of the user who last updated this attestation
        /// </summary>
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// Verification status for compliance attestations
    /// </summary>
    public enum AttestationVerificationStatus
    {
        /// <summary>
        /// Attestation is pending verification
        /// </summary>
        Pending,

        /// <summary>
        /// Attestation has been verified and is valid
        /// </summary>
        Verified,

        /// <summary>
        /// Attestation verification failed
        /// </summary>
        Failed,

        /// <summary>
        /// Attestation has expired and needs renewal
        /// </summary>
        Expired,

        /// <summary>
        /// Attestation has been revoked
        /// </summary>
        Revoked
    }

    /// <summary>
    /// Request to create a new compliance attestation
    /// </summary>
    public class CreateComplianceAttestationRequest
    {
        /// <summary>
        /// The wallet address for which this attestation applies
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string WalletAddress { get; set; } = string.Empty;

        /// <summary>
        /// The asset ID (token ID) this attestation is linked to
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// The issuer address who creates this attestation
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string IssuerAddress { get; set; } = string.Empty;

        /// <summary>
        /// Cryptographic hash of compliance proof document
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string ProofHash { get; set; } = string.Empty;

        /// <summary>
        /// Type of proof provided (e.g., "IPFS", "SHA256", "ARC19")
        /// </summary>
        [MaxLength(50)]
        public string? ProofType { get; set; }

        /// <summary>
        /// Type of attestation (e.g., KYC, AML, Accreditation, License)
        /// </summary>
        [MaxLength(100)]
        public string? AttestationType { get; set; }

        /// <summary>
        /// Network on which the token is deployed
        /// </summary>
        [MaxLength(50)]
        public string? Network { get; set; }

        /// <summary>
        /// Jurisdiction(s) applicable to this attestation
        /// </summary>
        [MaxLength(500)]
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Regulatory framework this attestation complies with
        /// </summary>
        [MaxLength(500)]
        public string? RegulatoryFramework { get; set; }

        /// <summary>
        /// Date when the attestation expires (if applicable)
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Additional metadata or notes about the attestation
        /// </summary>
        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request to retrieve attestations with filtering
    /// </summary>
    public class ListComplianceAttestationsRequest
    {
        /// <summary>
        /// Optional filter by wallet address
        /// </summary>
        [MaxLength(100)]
        public string? WalletAddress { get; set; }

        /// <summary>
        /// Optional filter by asset ID (token ID)
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Optional filter by issuer address
        /// </summary>
        [MaxLength(100)]
        public string? IssuerAddress { get; set; }

        /// <summary>
        /// Optional filter by verification status
        /// </summary>
        public AttestationVerificationStatus? VerificationStatus { get; set; }

        /// <summary>
        /// Optional filter by attestation type
        /// </summary>
        [MaxLength(100)]
        public string? AttestationType { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        [MaxLength(50)]
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter to include only non-expired attestations
        /// </summary>
        public bool? ExcludeExpired { get; set; }

        /// <summary>
        /// Optional start date filter (filter by IssuedAt)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter (filter by IssuedAt)
        /// </summary>
        public DateTime? ToDate { get; set; }

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
    /// Response for compliance attestation operations
    /// </summary>
    public class ComplianceAttestationResponse : BaseResponse
    {
        /// <summary>
        /// The attestation that was created or retrieved
        /// </summary>
        public ComplianceAttestation? Attestation { get; set; }
    }

    /// <summary>
    /// Response for listing compliance attestations
    /// </summary>
    public class ComplianceAttestationListResponse : BaseResponse
    {
        /// <summary>
        /// List of compliance attestations
        /// </summary>
        public List<ComplianceAttestation> Attestations { get; set; } = new();

        /// <summary>
        /// Total number of attestations matching the filter
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Request to generate a signed compliance attestation package for MICA audits
    /// </summary>
    public class GenerateAttestationPackageRequest
    {
        /// <summary>
        /// The token ID (asset ID) for which to generate the attestation package
        /// </summary>
        [Required]
        public ulong TokenId { get; set; }

        /// <summary>
        /// Start date for the attestation package date range
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// End date for the attestation package date range
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Output format for the attestation package (json or pdf)
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string Format { get; set; } = "json";
    }

    /// <summary>
    /// Signed compliance attestation package for MICA regulatory audits
    /// </summary>
    public class AttestationPackage
    {
        /// <summary>
        /// Unique identifier for this attestation package
        /// </summary>
        public string PackageId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Token ID this package is for
        /// </summary>
        public ulong TokenId { get; set; }

        /// <summary>
        /// Timestamp when this package was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Address of the issuer who requested this package
        /// </summary>
        public string IssuerAddress { get; set; } = string.Empty;

        /// <summary>
        /// Network the token is deployed on
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Token metadata (name, unit name, total supply, etc.)
        /// </summary>
        public TokenMetadata? Token { get; set; }

        /// <summary>
        /// Compliance metadata for the token
        /// </summary>
        public ComplianceMetadata? ComplianceMetadata { get; set; }

        /// <summary>
        /// Whitelist policy information
        /// </summary>
        public WhitelistPolicyInfo? WhitelistPolicy { get; set; }

        /// <summary>
        /// Latest compliance status
        /// </summary>
        public ComplianceStatusInfo? ComplianceStatus { get; set; }

        /// <summary>
        /// List of attestations in the date range
        /// </summary>
        public List<ComplianceAttestation> Attestations { get; set; } = new();

        /// <summary>
        /// Date range for attestations
        /// </summary>
        public DateRangeInfo? DateRange { get; set; }

        /// <summary>
        /// Deterministic hash of the package content for verification
        /// </summary>
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Signature metadata for audit verification
        /// </summary>
        public SignatureMetadata? Signature { get; set; }
    }

    /// <summary>
    /// Token metadata for attestation package
    /// </summary>
    public class TokenMetadata
    {
        /// <summary>
        /// Token ID (asset ID)
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Token name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Token unit name
        /// </summary>
        public string? UnitName { get; set; }

        /// <summary>
        /// Total supply
        /// </summary>
        public ulong? Total { get; set; }

        /// <summary>
        /// Number of decimals
        /// </summary>
        public uint? Decimals { get; set; }

        /// <summary>
        /// Creator address
        /// </summary>
        public string? Creator { get; set; }

        /// <summary>
        /// Manager address
        /// </summary>
        public string? Manager { get; set; }

        /// <summary>
        /// Reserve address
        /// </summary>
        public string? Reserve { get; set; }

        /// <summary>
        /// Freeze address
        /// </summary>
        public string? Freeze { get; set; }

        /// <summary>
        /// Clawback address
        /// </summary>
        public string? Clawback { get; set; }
    }

    /// <summary>
    /// Whitelist policy information
    /// </summary>
    public class WhitelistPolicyInfo
    {
        /// <summary>
        /// Whether whitelist is enabled
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Total number of whitelisted addresses
        /// </summary>
        public int TotalWhitelisted { get; set; }

        /// <summary>
        /// Whitelist enforcement type
        /// </summary>
        public string? EnforcementType { get; set; }
    }

    /// <summary>
    /// Compliance status information
    /// </summary>
    public class ComplianceStatusInfo
    {
        /// <summary>
        /// Current compliance status
        /// </summary>
        public ComplianceStatus Status { get; set; }

        /// <summary>
        /// Current verification status
        /// </summary>
        public VerificationStatus VerificationStatus { get; set; }

        /// <summary>
        /// Last compliance review date
        /// </summary>
        public DateTime? LastReviewDate { get; set; }

        /// <summary>
        /// Next compliance review date
        /// </summary>
        public DateTime? NextReviewDate { get; set; }
    }

    /// <summary>
    /// Date range information
    /// </summary>
    public class DateRangeInfo
    {
        /// <summary>
        /// Start date
        /// </summary>
        public DateTime? From { get; set; }

        /// <summary>
        /// End date
        /// </summary>
        public DateTime? To { get; set; }
    }

    /// <summary>
    /// Signature metadata for audit verification
    /// </summary>
    public class SignatureMetadata
    {
        /// <summary>
        /// Algorithm used for signing (e.g., "ED25519", "SHA256")
        /// </summary>
        public string Algorithm { get; set; } = string.Empty;

        /// <summary>
        /// Public key used for signature verification
        /// </summary>
        public string? PublicKey { get; set; }

        /// <summary>
        /// Signature value (base64 encoded)
        /// </summary>
        public string? SignatureValue { get; set; }

        /// <summary>
        /// Timestamp when signature was created
        /// </summary>
        public DateTime SignedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response for attestation package generation
    /// </summary>
    public class AttestationPackageResponse : BaseResponse
    {
        /// <summary>
        /// The generated attestation package
        /// </summary>
        public AttestationPackage? Package { get; set; }

        /// <summary>
        /// Format of the package (json or pdf)
        /// </summary>
        public string? Format { get; set; }
    }
}
