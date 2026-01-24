using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Request to create a new whitelisting rule
    /// </summary>
    public class CreateWhitelistRuleRequest
    {
        /// <summary>
        /// The asset ID (token ID) for which to create the rule
        /// </summary>
        [Required]
        public ulong AssetId { get; set; }

        /// <summary>
        /// Name of the rule
        /// </summary>
        [Required]
        [StringLength(200, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of what the rule does
        /// </summary>
        [StringLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Type of the rule
        /// </summary>
        [Required]
        public WhitelistRuleType RuleType { get; set; }

        /// <summary>
        /// Whether the rule should be active immediately (defaults to true)
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Priority order for rule execution (lower numbers execute first, defaults to 100)
        /// </summary>
        [Range(1, 1000)]
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Network on which this rule applies (voimain-v1.0, aramidmain-v1.0, null for all networks)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Rule-specific configuration as JSON
        /// </summary>
        public string? Configuration { get; set; }
    }

    /// <summary>
    /// Request to update an existing whitelisting rule
    /// </summary>
    public class UpdateWhitelistRuleRequest
    {
        /// <summary>
        /// The ID of the rule to update
        /// </summary>
        [Required]
        public string RuleId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the rule
        /// </summary>
        [StringLength(200, MinimumLength = 3)]
        public string? Name { get; set; }

        /// <summary>
        /// Detailed description of what the rule does
        /// </summary>
        [StringLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Whether the rule is active
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Priority order for rule execution
        /// </summary>
        [Range(1, 1000)]
        public int? Priority { get; set; }

        /// <summary>
        /// Network on which this rule applies
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Rule-specific configuration as JSON
        /// </summary>
        public string? Configuration { get; set; }
    }

    /// <summary>
    /// Request to list whitelisting rules
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
        /// Optional filter by active status
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? Network { get; set; }

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
    /// Request to apply a whitelisting rule
    /// </summary>
    public class ApplyWhitelistRuleRequest
    {
        /// <summary>
        /// The ID of the rule to apply
        /// </summary>
        [Required]
        public string RuleId { get; set; } = string.Empty;

        /// <summary>
        /// Optional list of specific addresses to apply the rule to (if null, applies to all matching entries)
        /// </summary>
        public List<string>? TargetAddresses { get; set; }

        /// <summary>
        /// Whether to perform a dry run without making actual changes
        /// </summary>
        public bool DryRun { get; set; }
    }

    /// <summary>
    /// Request to delete a whitelisting rule
    /// </summary>
    public class DeleteWhitelistRuleRequest
    {
        /// <summary>
        /// The ID of the rule to delete
        /// </summary>
        [Required]
        public string RuleId { get; set; } = string.Empty;
    }
}
