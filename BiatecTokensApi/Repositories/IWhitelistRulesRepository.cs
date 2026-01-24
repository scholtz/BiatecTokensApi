using BiatecTokensApi.Models.Whitelist;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// Interface for whitelist rules repository operations
    /// </summary>
    public interface IWhitelistRulesRepository
    {
        /// <summary>
        /// Adds a new whitelisting rule
        /// </summary>
        /// <param name="rule">The rule to add</param>
        /// <returns>True if the rule was added successfully</returns>
        Task<bool> AddRuleAsync(WhitelistRule rule);

        /// <summary>
        /// Gets a specific rule by ID
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <returns>The rule if found, null otherwise</returns>
        Task<WhitelistRule?> GetRuleAsync(string ruleId);

        /// <summary>
        /// Gets all rules for a specific asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="ruleType">Optional rule type filter</param>
        /// <param name="isActive">Optional active status filter</param>
        /// <param name="network">Optional network filter</param>
        /// <returns>List of rules</returns>
        Task<List<WhitelistRule>> GetRulesByAssetIdAsync(
            ulong assetId, 
            WhitelistRuleType? ruleType = null, 
            bool? isActive = null,
            string? network = null);

        /// <summary>
        /// Updates an existing rule
        /// </summary>
        /// <param name="rule">The rule to update</param>
        /// <returns>True if the rule was updated successfully</returns>
        Task<bool> UpdateRuleAsync(WhitelistRule rule);

        /// <summary>
        /// Deletes a rule
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <returns>True if the rule was deleted successfully</returns>
        Task<bool> DeleteRuleAsync(string ruleId);

        /// <summary>
        /// Records application of a rule
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <param name="appliedAt">Timestamp when applied</param>
        /// <returns>True if updated successfully</returns>
        Task<bool> RecordRuleApplicationAsync(string ruleId, DateTime appliedAt);

        /// <summary>
        /// Adds an audit log entry for a rule action
        /// </summary>
        /// <param name="auditLog">The audit log entry</param>
        /// <returns>True if added successfully</returns>
        Task<bool> AddAuditLogAsync(WhitelistRuleAuditLog auditLog);

        /// <summary>
        /// Gets audit log entries for a specific asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="ruleId">Optional rule ID filter</param>
        /// <param name="actionType">Optional action type filter</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>List of audit log entries</returns>
        Task<List<WhitelistRuleAuditLog>> GetAuditLogsAsync(
            ulong assetId,
            string? ruleId = null,
            RuleAuditActionType? actionType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null);
    }
}
