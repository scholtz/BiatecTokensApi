namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Audit log entry for whitelisting rule changes (MICA compliance)
    /// </summary>
    /// <remarks>
    /// Provides complete audit trail of rule lifecycle events for regulatory reporting
    /// and compliance verification as required by MICA regulation.
    /// </remarks>
    public class WhitelistRuleAuditLog
    {
        /// <summary>
        /// Unique identifier for the audit log entry
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The asset ID (token ID) affected by this rule change
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// The rule ID that was modified
        /// </summary>
        public string RuleId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the rule at the time of the action
        /// </summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>
        /// Type of action performed
        /// </summary>
        public RuleAuditActionType ActionType { get; set; }

        /// <summary>
        /// The address of the user who performed the action
        /// </summary>
        public string PerformedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the action was performed
        /// </summary>
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Previous state of the rule (JSON serialized, null for Create actions)
        /// </summary>
        public string? OldState { get; set; }

        /// <summary>
        /// New state of the rule (JSON serialized, null for Delete actions)
        /// </summary>
        public string? NewState { get; set; }

        /// <summary>
        /// Optional notes or reason for the change
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Network on which the rule applies (for filtering)
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Number of entries affected when rule was applied (for Apply actions)
        /// </summary>
        public int? AffectedEntriesCount { get; set; }
    }

    /// <summary>
    /// Types of audit actions for whitelisting rules
    /// </summary>
    public enum RuleAuditActionType
    {
        /// <summary>
        /// Rule was created
        /// </summary>
        Create,

        /// <summary>
        /// Rule was updated/modified
        /// </summary>
        Update,

        /// <summary>
        /// Rule was deleted
        /// </summary>
        Delete,

        /// <summary>
        /// Rule was applied to whitelist entries
        /// </summary>
        Apply,

        /// <summary>
        /// Rule was activated
        /// </summary>
        Activate,

        /// <summary>
        /// Rule was deactivated
        /// </summary>
        Deactivate
    }
}
