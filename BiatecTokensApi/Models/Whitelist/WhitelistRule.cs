namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Represents a whitelisting rule for RWA tokens aligned with MICA requirements
    /// </summary>
    /// <remarks>
    /// Whitelisting rules define automated conditions and policies for managing participant access
    /// to RWA tokens. Rules enable compliance automation, policy enforcement, and audit trails
    /// required by MICA (Markets in Crypto-Assets) regulation.
    /// </remarks>
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
        /// Name of the rule (e.g., "KYC Required for Active Status", "Auto-Revoke Expired Entries")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of what the rule does
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Type of the rule that determines its behavior
        /// </summary>
        public WhitelistRuleType RuleType { get; set; }

        /// <summary>
        /// Whether the rule is currently active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Priority order for rule execution (lower numbers execute first)
        /// </summary>
        public int Priority { get; set; } = 100;

        /// <summary>
        /// Network on which this rule applies (voimain-v1.0, aramidmain-v1.0, null for all networks)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Rule-specific configuration as JSON
        /// </summary>
        public string? Configuration { get; set; }

        /// <summary>
        /// The address of the user who created this rule
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the rule was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The address of the user who last updated this rule
        /// </summary>
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Timestamp when the rule was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Number of times this rule has been applied
        /// </summary>
        public int ApplicationCount { get; set; }

        /// <summary>
        /// Timestamp when the rule was last applied
        /// </summary>
        public DateTime? LastAppliedAt { get; set; }
    }

    /// <summary>
    /// Types of whitelisting rules supported for RWA token compliance
    /// </summary>
    public enum WhitelistRuleType
    {
        /// <summary>
        /// Requires KYC verification for whitelist entry activation
        /// </summary>
        RequireKycForActive,

        /// <summary>
        /// Automatically revokes entries that have expired
        /// </summary>
        AutoRevokeExpired,

        /// <summary>
        /// Enforces network-specific KYC requirements (e.g., mandatory for Aramid)
        /// </summary>
        NetworkKycRequirement,

        /// <summary>
        /// Requires operator role approval for certain status changes
        /// </summary>
        RequireOperatorApproval,

        /// <summary>
        /// Enforces minimum KYC verification age before activation
        /// </summary>
        MinimumKycAge,

        /// <summary>
        /// Prevents whitelist entries without expiration dates
        /// </summary>
        RequireExpirationDate,

        /// <summary>
        /// Auto-notifies when entries are near expiration
        /// </summary>
        ExpirationWarning,

        /// <summary>
        /// Enforces maximum number of active whitelist entries per asset
        /// </summary>
        MaxActiveEntries,

        /// <summary>
        /// Custom rule with user-defined logic
        /// </summary>
        Custom
    }

    /// <summary>
    /// Result of applying a whitelisting rule
    /// </summary>
    public class RuleApplicationResult
    {
        /// <summary>
        /// The rule that was applied
        /// </summary>
        public WhitelistRule Rule { get; set; } = new WhitelistRule();

        /// <summary>
        /// Whether the rule application was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of whitelist entries affected by the rule
        /// </summary>
        public int AffectedEntriesCount { get; set; }

        /// <summary>
        /// List of addresses that were affected
        /// </summary>
        public List<string> AffectedAddresses { get; set; } = new();

        /// <summary>
        /// Actions taken by the rule (e.g., "Revoked expired entry", "Activated entry with KYC")
        /// </summary>
        public List<string> Actions { get; set; } = new();

        /// <summary>
        /// Error message if rule application failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Timestamp when the rule was applied
        /// </summary>
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    }
}
