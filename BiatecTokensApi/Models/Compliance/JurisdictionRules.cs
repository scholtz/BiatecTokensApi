namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents jurisdiction-specific compliance rules and requirements
    /// </summary>
    /// <remarks>
    /// This model defines compliance requirements for specific jurisdictions (countries or regions).
    /// Rules can be configured without code changes and are evaluated during compliance checks.
    /// Supports MICA, FATF, SEC, and other regulatory frameworks.
    /// </remarks>
    public class JurisdictionRule
    {
        /// <summary>
        /// Unique identifier for the jurisdiction rule
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Jurisdiction code (ISO 3166-1 alpha-2 country code or region identifier like "EU", "US", "GLOBAL")
        /// </summary>
        public string JurisdictionCode { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name of the jurisdiction
        /// </summary>
        public string JurisdictionName { get; set; } = string.Empty;

        /// <summary>
        /// Regulatory framework applicable in this jurisdiction (e.g., "MICA", "SEC", "FATF", "MiFID II")
        /// </summary>
        public string RegulatoryFramework { get; set; } = string.Empty;

        /// <summary>
        /// Whether this jurisdiction is active and should be evaluated
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Priority for rule evaluation (higher priority evaluated first)
        /// </summary>
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Compliance requirements for this jurisdiction
        /// </summary>
        public List<ComplianceRequirement> Requirements { get; set; } = new();

        /// <summary>
        /// Date when the rule was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date when the rule was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// User who created the rule
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// User who last updated the rule
        /// </summary>
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Additional notes or context about this jurisdiction rule
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Rule version for compatibility tracking
        /// </summary>
        public string Version { get; set; } = "1.0";
    }

    /// <summary>
    /// Represents a specific compliance requirement for a jurisdiction
    /// </summary>
    public class ComplianceRequirement
    {
        /// <summary>
        /// Unique identifier for the requirement
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Requirement code (e.g., "MICA_ARTICLE_17", "SEC_ACCREDITED", "FATF_KYC")
        /// </summary>
        public string RequirementCode { get; set; } = string.Empty;

        /// <summary>
        /// Requirement category (e.g., "KYC", "AML", "Disclosure", "Licensing", "Transfer Restrictions")
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of the requirement
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether this requirement is mandatory or optional
        /// </summary>
        public bool IsMandatory { get; set; } = true;

        /// <summary>
        /// Requirement severity (Critical, High, Medium, Low, Info)
        /// </summary>
        public RequirementSeverity Severity { get; set; } = RequirementSeverity.High;

        /// <summary>
        /// Reference to applicable regulation article or section
        /// </summary>
        public string? RegulatoryReference { get; set; }

        /// <summary>
        /// Validation criteria for this requirement (JSON schema or description)
        /// </summary>
        public string? ValidationCriteria { get; set; }

        /// <summary>
        /// Recommended remediation action if requirement is not met
        /// </summary>
        public string? RemediationGuidance { get; set; }
    }

    /// <summary>
    /// Severity level for compliance requirements
    /// </summary>
    public enum RequirementSeverity
    {
        /// <summary>
        /// Informational requirement (best practice)
        /// </summary>
        Info,

        /// <summary>
        /// Low severity requirement
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity requirement
        /// </summary>
        Medium,

        /// <summary>
        /// High severity requirement
        /// </summary>
        High,

        /// <summary>
        /// Critical requirement (must be met for compliance)
        /// </summary>
        Critical
    }

    /// <summary>
    /// Request to create or update a jurisdiction rule
    /// </summary>
    public class CreateJurisdictionRuleRequest
    {
        /// <summary>
        /// Jurisdiction code (ISO 3166-1 alpha-2 or region identifier)
        /// </summary>
        public string JurisdictionCode { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name of the jurisdiction
        /// </summary>
        public string JurisdictionName { get; set; } = string.Empty;

        /// <summary>
        /// Regulatory framework applicable in this jurisdiction
        /// </summary>
        public string RegulatoryFramework { get; set; } = string.Empty;

        /// <summary>
        /// Whether this jurisdiction rule should be active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Priority for rule evaluation
        /// </summary>
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Compliance requirements for this jurisdiction
        /// </summary>
        public List<ComplianceRequirement> Requirements { get; set; } = new();

        /// <summary>
        /// Additional notes
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Response after creating or updating a jurisdiction rule
    /// </summary>
    public class JurisdictionRuleResponse : BaseResponse
    {
        /// <summary>
        /// The created or updated jurisdiction rule
        /// </summary>
        public JurisdictionRule? Rule { get; set; }
    }

    /// <summary>
    /// Request to list jurisdiction rules
    /// </summary>
    public class ListJurisdictionRulesRequest
    {
        /// <summary>
        /// Optional filter by jurisdiction code
        /// </summary>
        public string? JurisdictionCode { get; set; }

        /// <summary>
        /// Optional filter by regulatory framework
        /// </summary>
        public string? RegulatoryFramework { get; set; }

        /// <summary>
        /// Optional filter by active status
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination (default: 50, max: 100)
        /// </summary>
        public int PageSize { get; set; } = 50;
    }

    /// <summary>
    /// Response containing list of jurisdiction rules
    /// </summary>
    public class ListJurisdictionRulesResponse : BaseResponse
    {
        /// <summary>
        /// List of jurisdiction rules
        /// </summary>
        public List<JurisdictionRule> Rules { get; set; } = new();

        /// <summary>
        /// Total number of rules matching the filter
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
    /// Jurisdiction tagging for tokens
    /// </summary>
    public class TokenJurisdiction
    {
        /// <summary>
        /// Asset ID (token ID)
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Network on which the token is deployed
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Jurisdiction code applicable to this token
        /// </summary>
        public string JurisdictionCode { get; set; } = string.Empty;

        /// <summary>
        /// Whether this jurisdiction is the primary jurisdiction for the token
        /// </summary>
        public bool IsPrimary { get; set; } = true;

        /// <summary>
        /// Date when the jurisdiction was assigned
        /// </summary>
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User who assigned the jurisdiction
        /// </summary>
        public string AssignedBy { get; set; } = string.Empty;

        /// <summary>
        /// Additional notes about the jurisdiction assignment
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Result of evaluating jurisdiction rules for a token
    /// </summary>
    public class JurisdictionEvaluationResult
    {
        /// <summary>
        /// Asset ID that was evaluated
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Network on which the token is deployed
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Jurisdiction codes applicable to this token
        /// </summary>
        public List<string> ApplicableJurisdictions { get; set; } = new();

        /// <summary>
        /// Overall compliance status (Compliant, PartiallyCompliant, NonCompliant, Unknown)
        /// </summary>
        public string ComplianceStatus { get; set; } = "Unknown";

        /// <summary>
        /// List of compliance checks performed
        /// </summary>
        public List<JurisdictionComplianceCheck> CheckResults { get; set; } = new();

        /// <summary>
        /// Rationale for the compliance status
        /// </summary>
        public List<string> Rationale { get; set; } = new();

        /// <summary>
        /// Timestamp when the evaluation was performed
        /// </summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Result of a single jurisdiction compliance check
    /// </summary>
    public class JurisdictionComplianceCheck
    {
        /// <summary>
        /// Requirement code that was checked
        /// </summary>
        public string RequirementCode { get; set; } = string.Empty;

        /// <summary>
        /// Jurisdiction for which this check was performed
        /// </summary>
        public string JurisdictionCode { get; set; } = string.Empty;

        /// <summary>
        /// Check result status (Pass, Fail, Partial, NotApplicable)
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Evidence or details supporting the check result
        /// </summary>
        public string? Evidence { get; set; }

        /// <summary>
        /// Timestamp when the check was performed
        /// </summary>
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }
}
