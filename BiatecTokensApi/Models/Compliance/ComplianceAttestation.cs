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
}
