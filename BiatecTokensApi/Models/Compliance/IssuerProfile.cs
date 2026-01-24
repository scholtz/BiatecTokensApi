using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents an issuer profile for MICA compliance
    /// </summary>
    public class IssuerProfile
    {
        /// <summary>
        /// Unique identifier (issuer's Algorand address)
        /// </summary>
        [Required]
        public string IssuerAddress { get; set; } = string.Empty;

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
        /// KYB (Know Your Business) verification status
        /// </summary>
        public VerificationStatus KybStatus { get; set; } = VerificationStatus.Pending;

        /// <summary>
        /// KYB provider name
        /// </summary>
        [StringLength(200)]
        public string? KybProvider { get; set; }

        /// <summary>
        /// KYB verification date
        /// </summary>
        public DateTime? KybVerifiedDate { get; set; }

        /// <summary>
        /// MICA license status
        /// </summary>
        public MicaLicenseStatus MicaLicenseStatus { get; set; } = MicaLicenseStatus.None;

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
        /// Date issuer profile was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date issuer profile was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Profile verification status
        /// </summary>
        public IssuerProfileStatus Status { get; set; } = IssuerProfileStatus.Draft;

        /// <summary>
        /// Additional notes
        /// </summary>
        [StringLength(2000)]
        public string? Notes { get; set; }

        /// <summary>
        /// Address of user who created profile
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Address of user who last updated profile
        /// </summary>
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// Issuer address information
    /// </summary>
    public class IssuerAddress
    {
        /// <summary>
        /// Address line 1
        /// </summary>
        [Required]
        [StringLength(200)]
        public string AddressLine1 { get; set; } = string.Empty;

        /// <summary>
        /// Address line 2
        /// </summary>
        [StringLength(200)]
        public string? AddressLine2 { get; set; }

        /// <summary>
        /// City
        /// </summary>
        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// State or province
        /// </summary>
        [StringLength(100)]
        public string? StateOrProvince { get; set; }

        /// <summary>
        /// Postal code
        /// </summary>
        [Required]
        [StringLength(20)]
        public string PostalCode { get; set; } = string.Empty;

        /// <summary>
        /// Country code (ISO 2-letter)
        /// </summary>
        [Required]
        [StringLength(2)]
        public string CountryCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Issuer contact information
    /// </summary>
    public class IssuerContact
    {
        /// <summary>
        /// Contact name
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Email address
        /// </summary>
        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Phone number
        /// </summary>
        [Phone]
        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Job title
        /// </summary>
        [StringLength(100)]
        public string? Title { get; set; }
    }

    /// <summary>
    /// MICA license status
    /// </summary>
    public enum MicaLicenseStatus
    {
        /// <summary>
        /// No MICA license
        /// </summary>
        None,

        /// <summary>
        /// License application submitted
        /// </summary>
        Applied,

        /// <summary>
        /// License under review
        /// </summary>
        UnderReview,

        /// <summary>
        /// License approved
        /// </summary>
        Approved,

        /// <summary>
        /// License denied
        /// </summary>
        Denied,

        /// <summary>
        /// License suspended
        /// </summary>
        Suspended,

        /// <summary>
        /// License revoked
        /// </summary>
        Revoked
    }

    /// <summary>
    /// Issuer profile status
    /// </summary>
    public enum IssuerProfileStatus
    {
        /// <summary>
        /// Profile in draft state
        /// </summary>
        Draft,

        /// <summary>
        /// Profile submitted for review
        /// </summary>
        Submitted,

        /// <summary>
        /// Profile under review
        /// </summary>
        UnderReview,

        /// <summary>
        /// Profile verified
        /// </summary>
        Verified,

        /// <summary>
        /// Profile rejected
        /// </summary>
        Rejected,

        /// <summary>
        /// Profile suspended
        /// </summary>
        Suspended
    }
}
