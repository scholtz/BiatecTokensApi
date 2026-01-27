using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents compliance metadata for an RWA token
    /// </summary>
    /// <remarks>
    /// This model stores regulatory and compliance information for Real World Asset tokens,
    /// including KYC/AML verification details, jurisdiction information, and regulatory status.
    /// </remarks>
    public class ComplianceMetadata
    {
        /// <summary>
        /// Unique identifier for the compliance metadata entry
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The asset ID (token ID) for which this compliance metadata applies
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Legal name of the token issuer
        /// </summary>
        public string? IssuerName { get; set; }

        /// <summary>
        /// Name of the KYC/AML provider used for verification
        /// </summary>
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
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Regulatory framework(s) the token complies with (e.g., "SEC Reg D", "MiFID II")
        /// </summary>
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
        public string? AssetType { get; set; }

        /// <summary>
        /// Restrictions on token transfers (if any)
        /// </summary>
        public string? TransferRestrictions { get; set; }

        /// <summary>
        /// Maximum number of token holders allowed
        /// </summary>
        public int? MaxHolders { get; set; }

        /// <summary>
        /// Whether the token requires accredited investors only
        /// </summary>
        public bool RequiresAccreditedInvestors { get; set; }

        /// <summary>
        /// Network on which the token is deployed
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Additional compliance notes
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Address of the user who created this compliance metadata
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the metadata was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the metadata was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Address of the user who last updated this metadata
        /// </summary>
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// KYC/AML verification status
    /// </summary>
    public enum VerificationStatus
    {
        /// <summary>
        /// Verification is pending
        /// </summary>
        Pending,

        /// <summary>
        /// Verification is in progress
        /// </summary>
        InProgress,

        /// <summary>
        /// Verification completed successfully
        /// </summary>
        Verified,

        /// <summary>
        /// Verification failed
        /// </summary>
        Failed,

        /// <summary>
        /// Verification expired and needs renewal
        /// </summary>
        Expired
    }

    /// <summary>
    /// Compliance status of the token
    /// </summary>
    public enum ComplianceStatus
    {
        /// <summary>
        /// Compliance review is under review
        /// </summary>
        UnderReview,

        /// <summary>
        /// Token is compliant with regulations
        /// </summary>
        Compliant,

        /// <summary>
        /// Token is non-compliant
        /// </summary>
        NonCompliant,

        /// <summary>
        /// Compliance status is suspended
        /// </summary>
        Suspended,

        /// <summary>
        /// Token is exempt from certain regulations
        /// </summary>
        Exempt
    }
}
