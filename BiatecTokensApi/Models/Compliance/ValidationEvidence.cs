using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents the context in which a validation is performed
    /// </summary>
    public class ValidationContext
    {
        /// <summary>
        /// The blockchain network (e.g., "voimain-v1.0", "aramidmain-v1.0", "mainnet")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// The token standard (e.g., "ASA", "ARC3", "ARC200", "ERC20")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string TokenStandard { get; set; } = string.Empty;

        /// <summary>
        /// The role of the issuer (e.g., "Individual", "Corporation", "GovernmentEntity")
        /// </summary>
        [MaxLength(100)]
        public string? IssuerRole { get; set; }

        /// <summary>
        /// Compliance flags that are active for this validation
        /// </summary>
        public List<ComplianceFlag> ComplianceFlags { get; set; } = new();

        /// <summary>
        /// Jurisdiction-specific toggles and their reasons
        /// </summary>
        public Dictionary<string, string> JurisdictionToggles { get; set; } = new();

        /// <summary>
        /// The validator version used for this validation
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ValidatorVersion { get; set; } = "1.0.0";

        /// <summary>
        /// The rule set version used for this validation
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string RuleSetVersion { get; set; } = "1.0.0";
    }

    /// <summary>
    /// Represents a compliance flag that is active for a validation
    /// </summary>
    public class ComplianceFlag
    {
        /// <summary>
        /// The flag identifier (e.g., "EU_MICA_ENABLED", "RWA_WHITELIST_REQUIRED")
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string FlagId { get; set; } = string.Empty;

        /// <summary>
        /// Description of the flag
        /// </summary>
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Reason why this flag is active
        /// </summary>
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a single rule evaluation in the validation process
    /// </summary>
    public class RuleEvaluation
    {
        /// <summary>
        /// Unique identifier for the rule
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string RuleId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name of the rule
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string RuleName { get; set; } = string.Empty;

        /// <summary>
        /// Description of what the rule checks
        /// </summary>
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether the rule passed
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Whether the rule was skipped
        /// </summary>
        public bool Skipped { get; set; }

        /// <summary>
        /// Reason why the rule was skipped (if applicable)
        /// </summary>
        [MaxLength(500)]
        public string? SkipReason { get; set; }

        /// <summary>
        /// Error message if the rule failed
        /// </summary>
        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Suggested remediation steps if the rule failed
        /// </summary>
        [MaxLength(2000)]
        public string? RemediationSteps { get; set; }

        /// <summary>
        /// Category of the rule (e.g., "Metadata", "Network", "Security", "Compliance")
        /// </summary>
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Severity level if the rule fails
        /// </summary>
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
    }

    /// <summary>
    /// Represents the complete validation evidence for a token issuance
    /// </summary>
    public class ValidationEvidence
    {
        /// <summary>
        /// Unique identifier for this evidence record
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string EvidenceId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The token ID this validation was performed for (null for pre-issuance)
        /// </summary>
        public ulong? TokenId { get; set; }

        /// <summary>
        /// Temporary identifier for pre-issuance validation
        /// </summary>
        [MaxLength(100)]
        public string? PreIssuanceId { get; set; }

        /// <summary>
        /// The validation context used
        /// </summary>
        [Required]
        public ValidationContext Context { get; set; } = new();

        /// <summary>
        /// Overall validation result
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// List of all rule evaluations
        /// </summary>
        public List<RuleEvaluation> RuleEvaluations { get; set; } = new();

        /// <summary>
        /// Timestamp when the validation was performed (UTC)
        /// </summary>
        public DateTime ValidationTimestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Address of the user who requested the validation
        /// </summary>
        [MaxLength(100)]
        public string RequestedBy { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 checksum of the evidence record for tamper detection
        /// </summary>
        [MaxLength(64)]
        public string Checksum { get; set; } = string.Empty;

        /// <summary>
        /// Whether this was a dry-run validation (not persisted)
        /// </summary>
        public bool IsDryRun { get; set; }

        /// <summary>
        /// Summary of the validation outcome
        /// </summary>
        [MaxLength(2000)]
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Total number of rules evaluated
        /// </summary>
        public int TotalRules { get; set; }

        /// <summary>
        /// Number of rules that passed
        /// </summary>
        public int PassedRules { get; set; }

        /// <summary>
        /// Number of rules that failed
        /// </summary>
        public int FailedRules { get; set; }

        /// <summary>
        /// Number of rules that were skipped
        /// </summary>
        public int SkippedRules { get; set; }
    }

    /// <summary>
    /// Request to validate token metadata before issuance
    /// </summary>
    public class ValidateTokenMetadataRequest
    {
        /// <summary>
        /// The validation context
        /// </summary>
        [Required]
        public ValidationContext Context { get; set; } = new();

        /// <summary>
        /// The token metadata to validate
        /// </summary>
        [Required]
        public object TokenMetadata { get; set; } = new();

        /// <summary>
        /// Whether this is a dry-run validation (does not persist evidence)
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// Temporary identifier for tracking pre-issuance validation
        /// </summary>
        [MaxLength(100)]
        public string? PreIssuanceId { get; set; }
    }

    /// <summary>
    /// Response from token metadata validation
    /// </summary>
    public class ValidateTokenMetadataResponse : BaseResponse
    {
        /// <summary>
        /// The validation evidence
        /// </summary>
        public ValidationEvidence? Evidence { get; set; }

        /// <summary>
        /// Whether the validation passed
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Evidence identifier (null for dry-run)
        /// </summary>
        public string? EvidenceId { get; set; }
    }

    /// <summary>
    /// Request to retrieve validation evidence
    /// </summary>
    public class GetValidationEvidenceRequest
    {
        /// <summary>
        /// The evidence ID to retrieve
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string EvidenceId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response containing validation evidence
    /// </summary>
    public class GetValidationEvidenceResponse : BaseResponse
    {
        /// <summary>
        /// The validation evidence
        /// </summary>
        public ValidationEvidence? Evidence { get; set; }
    }

    /// <summary>
    /// Request to list validation evidence for a token
    /// </summary>
    public class ListValidationEvidenceRequest
    {
        /// <summary>
        /// The token ID to list evidence for
        /// </summary>
        public ulong? TokenId { get; set; }

        /// <summary>
        /// Pre-issuance identifier to list evidence for
        /// </summary>
        [MaxLength(100)]
        public string? PreIssuanceId { get; set; }

        /// <summary>
        /// Optional filter by validation result
        /// </summary>
        public bool? Passed { get; set; }

        /// <summary>
        /// Optional start date filter
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter
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
    /// Response containing list of validation evidence
    /// </summary>
    public class ListValidationEvidenceResponse : BaseResponse
    {
        /// <summary>
        /// List of validation evidence entries
        /// </summary>
        public List<ValidationEvidence> Evidence { get; set; } = new();

        /// <summary>
        /// Total number of entries matching the filter
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
