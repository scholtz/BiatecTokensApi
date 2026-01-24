namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Represents a whitelisting rule for RWA tokens that defines validation and compliance requirements
    /// </summary>
    public class WhitelistRule
    {
        /// <summary>
        /// Unique identifier for the rule
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The asset ID (token ID) for which this rule applies
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Name of the rule (e.g., "KYC Required for Aramid", "Expiration Policy")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what the rule enforces
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Type of the rule
        /// </summary>
        public WhitelistRuleType RuleType { get; set; }

        /// <summary>
        /// Priority of the rule (higher number = higher priority, default: 100)
        /// </summary>
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Whether the rule is currently active
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Network on which this rule applies (voimain-v1.0, aramidmain-v1.0, null for all networks)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Configuration parameters for the rule (JSON serialized)
        /// </summary>
        public WhitelistRuleConfiguration Configuration { get; set; } = new();

        /// <summary>
        /// The address of the user who created this rule
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the rule was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the rule was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// The address of the user who last updated this rule
        /// </summary>
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// Type of whitelisting rule
    /// </summary>
    public enum WhitelistRuleType
    {
        /// <summary>
        /// Requires KYC verification for whitelist entries
        /// </summary>
        KycRequired,

        /// <summary>
        /// Enforces role-based access (Admin vs Operator)
        /// </summary>
        RoleBasedAccess,

        /// <summary>
        /// Enforces network-specific validation rules
        /// </summary>
        NetworkSpecific,

        /// <summary>
        /// Enforces expiration date on whitelist entries
        /// </summary>
        ExpirationRequired,

        /// <summary>
        /// Requires specific status for whitelist entries
        /// </summary>
        StatusValidation,

        /// <summary>
        /// Composite rule combining multiple validations
        /// </summary>
        Composite
    }

    /// <summary>
    /// Configuration parameters for a whitelist rule
    /// </summary>
    public class WhitelistRuleConfiguration
    {
        /// <summary>
        /// For KYC rules: whether KYC verification is mandatory
        /// </summary>
        public bool? KycMandatory { get; set; }

        /// <summary>
        /// For KYC rules: list of approved KYC providers
        /// </summary>
        public List<string>? ApprovedKycProviders { get; set; }

        /// <summary>
        /// For role rules: minimum required role
        /// </summary>
        public WhitelistRole? MinimumRole { get; set; }

        /// <summary>
        /// For expiration rules: whether expiration date is mandatory
        /// </summary>
        public bool? ExpirationMandatory { get; set; }

        /// <summary>
        /// For expiration rules: maximum validity period in days
        /// </summary>
        public int? MaxValidityDays { get; set; }

        /// <summary>
        /// For status rules: required status
        /// </summary>
        public WhitelistStatus? RequiredStatus { get; set; }

        /// <summary>
        /// For network rules: specific network requirements
        /// </summary>
        public string? NetworkRequirement { get; set; }

        /// <summary>
        /// For composite rules: list of rule IDs to combine
        /// </summary>
        public List<string>? CompositeRuleIds { get; set; }

        /// <summary>
        /// Custom validation message when rule fails
        /// </summary>
        public string? ValidationMessage { get; set; }
    }
}
