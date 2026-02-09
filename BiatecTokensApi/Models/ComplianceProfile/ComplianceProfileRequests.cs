using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.ComplianceProfile
{
    /// <summary>
    /// Request to create or update a compliance profile
    /// </summary>
    public class UpsertComplianceProfileRequest
    {
        /// <summary>
        /// Legal name of the issuing entity
        /// </summary>
        [Required(ErrorMessage = "Issuing entity name is required")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Issuing entity name must be between 2 and 200 characters")]
        public string IssuingEntityName { get; set; } = string.Empty;

        /// <summary>
        /// Jurisdiction/country code (ISO 3166-1 alpha-2, e.g., "US", "GB", "DE")
        /// </summary>
        [Required(ErrorMessage = "Jurisdiction is required")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "Jurisdiction must be a 2-character ISO country code")]
        [RegularExpression("^[A-Z]{2}$", ErrorMessage = "Jurisdiction must be a valid 2-letter uppercase ISO country code")]
        public string Jurisdiction { get; set; } = string.Empty;

        /// <summary>
        /// Issuance intent category
        /// </summary>
        [Required(ErrorMessage = "Issuance intent is required")]
        public string IssuanceIntent { get; set; } = string.Empty;

        /// <summary>
        /// Readiness status
        /// </summary>
        public ReadinessStatus ReadinessStatus { get; set; } = ReadinessStatus.InProgress;

        /// <summary>
        /// Optional additional metadata
        /// </summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Request to get compliance profile
    /// </summary>
    public class GetComplianceProfileRequest
    {
        /// <summary>
        /// User ID to get profile for (optional, defaults to authenticated user)
        /// </summary>
        public string? UserId { get; set; }
    }
}
