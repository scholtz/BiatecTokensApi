using BiatecTokensApi.Models.Whitelist;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory implementation of the whitelist rules repository
    /// </summary>
    /// <remarks>
    /// Thread-safe in-memory storage for whitelisting rules and audit logs.
    /// Can be replaced with database backend without changing the interface.
    /// </remarks>
    public class WhitelistRulesRepository : IWhitelistRulesRepository
    {
        // Thread-safe storage for rules
        private readonly ConcurrentDictionary<string, WhitelistRule> _rules = new();
        
        // Thread-safe storage for audit logs
        private readonly ConcurrentDictionary<string, WhitelistRuleAuditLog> _auditLogs = new();

        /// <summary>
        /// Adds a new whitelisting rule
        /// </summary>
        /// <param name="rule">The rule to add</param>
        /// <returns>True if the rule was added successfully</returns>
        public Task<bool> AddRuleAsync(WhitelistRule rule)
        {
            var added = _rules.TryAdd(rule.Id, rule);
            return Task.FromResult(added);
        }

        /// <summary>
        /// Gets a specific rule by ID
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <returns>The rule if found, null otherwise</returns>
        public Task<WhitelistRule?> GetRuleAsync(string ruleId)
        {
            _rules.TryGetValue(ruleId, out var rule);
            return Task.FromResult(rule);
        }

        /// <summary>
        /// Gets all rules for a specific asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="ruleType">Optional rule type filter</param>
        /// <param name="isActive">Optional active status filter</param>
        /// <param name="network">Optional network filter</param>
        /// <returns>List of rules</returns>
        public Task<List<WhitelistRule>> GetRulesByAssetIdAsync(
            ulong assetId,
            WhitelistRuleType? ruleType = null,
            bool? isActive = null,
            string? network = null)
        {
            var query = _rules.Values.Where(r => r.AssetId == assetId);

            if (ruleType.HasValue)
            {
                query = query.Where(r => r.RuleType == ruleType.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(r => r.IsActive == isActive.Value);
            }

            if (!string.IsNullOrEmpty(network))
            {
                query = query.Where(r => r.Network == null || r.Network == network);
            }

            // Sort by priority (lower numbers first)
            var results = query.OrderBy(r => r.Priority).ThenBy(r => r.CreatedAt).ToList();
            
            return Task.FromResult(results);
        }

        /// <summary>
        /// Updates an existing rule
        /// </summary>
        /// <param name="rule">The rule to update</param>
        /// <returns>True if the rule was updated successfully</returns>
        public Task<bool> UpdateRuleAsync(WhitelistRule rule)
        {
            if (_rules.ContainsKey(rule.Id))
            {
                _rules[rule.Id] = rule;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// Deletes a rule
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <returns>True if the rule was deleted successfully</returns>
        public Task<bool> DeleteRuleAsync(string ruleId)
        {
            var removed = _rules.TryRemove(ruleId, out _);
            return Task.FromResult(removed);
        }

        /// <summary>
        /// Records application of a rule
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <param name="appliedAt">Timestamp when applied</param>
        /// <returns>True if updated successfully</returns>
        public Task<bool> RecordRuleApplicationAsync(string ruleId, DateTime appliedAt)
        {
            if (_rules.TryGetValue(ruleId, out var rule))
            {
                rule.ApplicationCount++;
                rule.LastAppliedAt = appliedAt;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// Adds an audit log entry for a rule action
        /// </summary>
        /// <param name="auditLog">The audit log entry</param>
        /// <returns>True if added successfully</returns>
        public Task<bool> AddAuditLogAsync(WhitelistRuleAuditLog auditLog)
        {
            var added = _auditLogs.TryAdd(auditLog.Id, auditLog);
            return Task.FromResult(added);
        }

        /// <summary>
        /// Gets audit log entries for a specific asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="ruleId">Optional rule ID filter</param>
        /// <param name="actionType">Optional action type filter</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>List of audit log entries</returns>
        public Task<List<WhitelistRuleAuditLog>> GetAuditLogsAsync(
            ulong assetId,
            string? ruleId = null,
            RuleAuditActionType? actionType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var query = _auditLogs.Values.Where(a => a.AssetId == assetId);

            if (!string.IsNullOrEmpty(ruleId))
            {
                query = query.Where(a => a.RuleId == ruleId);
            }

            if (actionType.HasValue)
            {
                query = query.Where(a => a.ActionType == actionType.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.PerformedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(a => a.PerformedAt <= toDate.Value);
            }

            // Sort by most recent first
            var results = query.OrderByDescending(a => a.PerformedAt).ToList();
            
            return Task.FromResult(results);
        }
    }
}
