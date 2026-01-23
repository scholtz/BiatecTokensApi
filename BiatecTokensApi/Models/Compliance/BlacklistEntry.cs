using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents a blacklisted address for compliance enforcement
    /// </summary>
    public class BlacklistEntry
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

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
        public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date blacklist entry expires (null for permanent)
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        /// Blacklist status
        /// </summary>
        public BlacklistStatus Status { get; set; } = BlacklistStatus.Active;

        /// <summary>
        /// Address of user who created the entry
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Date entry was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Address of user who last updated the entry
        /// </summary>
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Date entry was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Additional notes
        /// </summary>
        [StringLength(2000)]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Blacklist category
    /// </summary>
    public enum BlacklistCategory
    {
        /// <summary>
        /// Government sanctions (OFAC, UN, EU)
        /// </summary>
        Sanctions,

        /// <summary>
        /// Scams, fraud, phishing
        /// </summary>
        FraudulentActivity,

        /// <summary>
        /// AML concerns
        /// </summary>
        MoneyLaundering,

        /// <summary>
        /// CTF concerns
        /// </summary>
        TerroristFinancing,

        /// <summary>
        /// Regulatory non-compliance
        /// </summary>
        RegulatoryViolation,

        /// <summary>
        /// Other reasons
        /// </summary>
        Other
    }

    /// <summary>
    /// Blacklist status
    /// </summary>
    public enum BlacklistStatus
    {
        /// <summary>
        /// Active blacklist entry
        /// </summary>
        Active,

        /// <summary>
        /// Inactive blacklist entry
        /// </summary>
        Inactive,

        /// <summary>
        /// Under review
        /// </summary>
        UnderReview,

        /// <summary>
        /// Appealed
        /// </summary>
        Appealed,

        /// <summary>
        /// Removed from blacklist
        /// </summary>
        Removed
    }
}
