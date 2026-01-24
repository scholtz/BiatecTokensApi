using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Request to create a new whitelisting rule
    /// </summary>
    public class CreateWhitelistRuleRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which this rule applies
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// Name of the rule (e.g., "KYC Required for Aramid")
        /// </summary>
        [Required]
        [StringLength(200, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what the rule enforces
        /// </summary>
        [StringLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Type of the rule
        /// </summary>
        [Required]
        public WhitelistRuleType RuleType { get; set; }

        /// <summary>
        /// Priority of the rule (higher number = higher priority, default: 100)
        /// </summary>
        [Range(1, 1000)]
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Whether the rule is currently active (default: true)
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Network on which this rule applies (voimain-v1.0, aramidmain-v1.0, null for all networks)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Configuration parameters for the rule
        /// </summary>
        [Required]
        public WhitelistRuleConfiguration Configuration { get; set; } = new();
    }

    /// <summary>
    /// Request to update an existing whitelisting rule
    /// </summary>
    public class UpdateWhitelistRuleRequest
    {
        /// <summary>
        /// The rule ID to update
        /// </summary>
        [Required]
        public string RuleId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the rule
        /// </summary>
        [StringLength(200, MinimumLength = 3)]
        public string? Name { get; set; }

        /// <summary>
        /// Description of what the rule enforces
        /// </summary>
        [StringLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Priority of the rule (higher number = higher priority)
        /// </summary>
        [Range(1, 1000)]
        public int? Priority { get; set; }

        /// <summary>
        /// Whether the rule is currently active
        /// </summary>
        public bool? IsEnabled { get; set; }

        /// <summary>
        /// Configuration parameters for the rule
        /// </summary>
        public WhitelistRuleConfiguration? Configuration { get; set; }
    }

    /// <summary>
    /// Request to list whitelisting rules for an asset
    /// </summary>
    public class ListWhitelistRulesRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which to list rules
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// Optional filter by rule type
        /// </summary>
        public WhitelistRuleType? RuleType { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Optional filter by enabled status
        /// </summary>
        public bool? IsEnabled { get; set; }

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
    /// Request to apply a rule to existing whitelist entries
    /// </summary>
    public class ApplyWhitelistRuleRequest
    {
        /// <summary>
        /// The rule ID to apply
        /// </summary>
        [Required]
        public string RuleId { get; set; } = string.Empty;

        /// <summary>
        /// Whether to apply the rule retroactively to all existing entries
        /// </summary>
        public bool ApplyToExisting { get; set; } = true;

        /// <summary>
        /// Whether to fail on validation errors or continue
        /// </summary>
        public bool FailOnError { get; set; } = false;
    }

    /// <summary>
    /// Request to validate whitelist entries against rules
    /// </summary>
    public class ValidateAgainstRulesRequest
    {
        /// <summary>
        /// The asset ID (token ID)
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// Optional specific address to validate
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Optional specific rule ID to validate against
        /// </summary>
        public string? RuleId { get; set; }
    }
}
