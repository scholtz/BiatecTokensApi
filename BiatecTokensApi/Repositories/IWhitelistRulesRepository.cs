using BiatecTokensApi.Models.Whitelist;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// Repository interface for managing whitelist rules
    /// </summary>
    public interface IWhitelistRulesRepository
    {
        /// <summary>
        /// Creates a new whitelist rule
        /// </summary>
        /// <param name="rule">The rule to create</param>
        /// <returns>The created rule</returns>
        Task<WhitelistRule> CreateRuleAsync(WhitelistRule rule);

        /// <summary>
        /// Gets a rule by ID
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <returns>The rule if found, null otherwise</returns>
        Task<WhitelistRule?> GetRuleAsync(string ruleId);

        /// <summary>
        /// Updates an existing rule
        /// </summary>
        /// <param name="rule">The rule to update</param>
        /// <returns>True if updated successfully</returns>
        Task<bool> UpdateRuleAsync(WhitelistRule rule);

        /// <summary>
        /// Deletes a rule
        /// </summary>
        /// <param name="ruleId">The rule ID to delete</param>
        /// <returns>True if deleted successfully</returns>
        Task<bool> DeleteRuleAsync(string ruleId);

        /// <summary>
        /// Gets all rules for an asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <returns>List of rules</returns>
        Task<List<WhitelistRule>> GetRulesForAssetAsync(ulong assetId);

        /// <summary>
        /// Gets rules for an asset with filtering
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="ruleType">Optional rule type filter</param>
        /// <param name="network">Optional network filter</param>
        /// <param name="isEnabled">Optional enabled status filter</param>
        /// <returns>List of rules</returns>
        Task<List<WhitelistRule>> GetRulesForAssetAsync(
            ulong assetId, 
            WhitelistRuleType? ruleType = null, 
            string? network = null, 
            bool? isEnabled = null);

        /// <summary>
        /// Adds an audit log entry for rule changes
        /// </summary>
        /// <param name="auditLog">The audit log entry</param>
        /// <returns>The created audit log entry</returns>
        Task<WhitelistRuleAuditLog> AddAuditLogAsync(WhitelistRuleAuditLog auditLog);

        /// <summary>
        /// Gets audit log entries for a rule
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>List of audit log entries</returns>
        Task<List<WhitelistRuleAuditLog>> GetAuditLogAsync(
            string ruleId, 
            DateTime? fromDate = null, 
            DateTime? toDate = null);

        /// <summary>
        /// Gets audit log entries for an asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>List of audit log entries</returns>
        Task<List<WhitelistRuleAuditLog>> GetAuditLogForAssetAsync(
            ulong assetId, 
            DateTime? fromDate = null, 
            DateTime? toDate = null);
    }
}
