using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// MICA compliance checklist for a specific token
    /// </summary>
    public class MicaComplianceChecklist
    {
        /// <summary>
        /// Asset ID (token ID)
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Overall MICA compliance status
        /// </summary>
        public MicaComplianceStatus OverallStatus { get; set; }

        /// <summary>
        /// Compliance percentage (0-100)
        /// </summary>
        [Range(0, 100)]
        public int CompliancePercentage { get; set; }

        /// <summary>
        /// List of compliance requirements
        /// </summary>
        public List<MicaRequirement> Requirements { get; set; } = new();

        /// <summary>
        /// Date checklist was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Next required action
        /// </summary>
        [StringLength(500)]
        public string? NextAction { get; set; }

        /// <summary>
        /// Estimated completion date
        /// </summary>
        public DateTime? EstimatedCompletionDate { get; set; }
    }

    /// <summary>
    /// Individual MICA requirement
    /// </summary>
    public class MicaRequirement
    {
        /// <summary>
        /// Requirement ID
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Requirement category
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Requirement description
        /// </summary>
        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether requirement is met
        /// </summary>
        public bool IsMet { get; set; }

        /// <summary>
        /// Requirement priority
        /// </summary>
        public MicaRequirementPriority Priority { get; set; }

        /// <summary>
        /// Evidence or notes
        /// </summary>
        [StringLength(2000)]
        public string? Evidence { get; set; }

        /// <summary>
        /// Date requirement was met
        /// </summary>
        public DateTime? MetDate { get; set; }

        /// <summary>
        /// Recommendations for meeting requirement
        /// </summary>
        [StringLength(1000)]
        public string? Recommendations { get; set; }
    }

    /// <summary>
    /// MICA compliance status
    /// </summary>
    public enum MicaComplianceStatus
    {
        /// <summary>
        /// Not started
        /// </summary>
        NotStarted,

        /// <summary>
        /// In progress
        /// </summary>
        InProgress,

        /// <summary>
        /// Nearly compliant
        /// </summary>
        NearlyCompliant,

        /// <summary>
        /// Fully compliant
        /// </summary>
        FullyCompliant,

        /// <summary>
        /// Non-compliant
        /// </summary>
        NonCompliant
    }

    /// <summary>
    /// MICA requirement priority
    /// </summary>
    public enum MicaRequirementPriority
    {
        /// <summary>
        /// Critical priority
        /// </summary>
        Critical,

        /// <summary>
        /// High priority
        /// </summary>
        High,

        /// <summary>
        /// Medium priority
        /// </summary>
        Medium,

        /// <summary>
        /// Low priority
        /// </summary>
        Low
    }
}
