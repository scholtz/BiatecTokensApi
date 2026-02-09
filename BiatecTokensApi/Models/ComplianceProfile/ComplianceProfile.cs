using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ComplianceProfile
{
    /// <summary>
    /// Represents a user's compliance profile for wallet-free enterprise onboarding
    /// </summary>
    /// <remarks>
    /// This model stores regulatory and compliance metadata associated with a user account,
    /// supporting MICA readiness and enterprise-grade compliance for regulated token issuance.
    /// Uses SaaS terminology (account-based) instead of wallet terminology.
    /// </remarks>
    public class ComplianceProfile
    {
        /// <summary>
        /// Unique identifier for the compliance profile
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// User ID this profile belongs to
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Legal name of the issuing entity
        /// </summary>
        [Required]
        public string IssuingEntityName { get; set; } = string.Empty;

        /// <summary>
        /// Jurisdiction/country code (ISO 3166-1 alpha-2)
        /// </summary>
        [Required]
        [StringLength(2, MinimumLength = 2)]
        public string Jurisdiction { get; set; } = string.Empty;

        /// <summary>
        /// Issuance intent category (e.g., "equity", "debt", "asset-backed", "utility")
        /// </summary>
        [Required]
        public string IssuanceIntent { get; set; } = string.Empty;

        /// <summary>
        /// Onboarding readiness status
        /// </summary>
        public ReadinessStatus ReadinessStatus { get; set; } = ReadinessStatus.NotStarted;

        /// <summary>
        /// Timestamp when the profile was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the profile was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User ID of who last updated the profile
        /// </summary>
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Optional additional metadata for extensibility
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Determines if onboarding is complete based on profile completeness
        /// </summary>
        /// <returns>True if all required fields are filled and status is Complete or Active</returns>
        public bool IsOnboardingComplete()
        {
            return !string.IsNullOrWhiteSpace(IssuingEntityName) &&
                   !string.IsNullOrWhiteSpace(Jurisdiction) &&
                   !string.IsNullOrWhiteSpace(IssuanceIntent) &&
                   (ReadinessStatus == ReadinessStatus.Complete || ReadinessStatus == ReadinessStatus.Active);
        }
    }

    /// <summary>
    /// Readiness status for compliance profile
    /// </summary>
    public enum ReadinessStatus
    {
        /// <summary>
        /// Profile has not been started
        /// </summary>
        NotStarted = 0,

        /// <summary>
        /// Profile is in progress
        /// </summary>
        InProgress = 1,

        /// <summary>
        /// Profile needs review
        /// </summary>
        PendingReview = 2,

        /// <summary>
        /// Profile is complete but not yet active
        /// </summary>
        Complete = 3,

        /// <summary>
        /// Profile is active and ready for token issuance
        /// </summary>
        Active = 4,

        /// <summary>
        /// Profile has been suspended
        /// </summary>
        Suspended = 5
    }
}
