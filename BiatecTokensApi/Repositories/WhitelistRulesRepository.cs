using BiatecTokensApi.Models.Whitelist;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository for managing whitelist rules with thread-safe operations
    /// </summary>
    public class WhitelistRulesRepository : IWhitelistRulesRepository
    {
        // Thread-safe storage for rules: Key is RuleId
        private readonly ConcurrentDictionary<string, WhitelistRule> _rules = new();
        
        // Thread-safe storage for audit logs: List per RuleId
        private readonly ConcurrentDictionary<string, ConcurrentBag<WhitelistRuleAuditLog>> _auditLogs = new();
        
        // Index for quick asset-based lookups: AssetId -> List of RuleIds
        private readonly ConcurrentDictionary<ulong, ConcurrentBag<string>> _assetIndex = new();

        /// <summary>
        /// Creates a new whitelist rule
        /// </summary>
        public Task<WhitelistRule> CreateRuleAsync(WhitelistRule rule)
        {
            if (_rules.TryAdd(rule.Id, rule))
            {
                // Add to asset index
                _assetIndex.AddOrUpdate(
                    rule.AssetId,
                    new ConcurrentBag<string> { rule.Id },
                    (key, existing) =>
                    {
                        existing.Add(rule.Id);
                        return existing;
                    });

                return Task.FromResult(rule);
            }

            throw new InvalidOperationException($"Rule with ID {rule.Id} already exists");
        }

        /// <summary>
        /// Gets a rule by ID
        /// </summary>
        public Task<WhitelistRule?> GetRuleAsync(string ruleId)
        {
            _rules.TryGetValue(ruleId, out var rule);
            return Task.FromResult(rule);
        }

        /// <summary>
        /// Updates an existing rule
        /// </summary>
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
        public Task<bool> DeleteRuleAsync(string ruleId)
        {
            if (_rules.TryRemove(ruleId, out var rule))
            {
                // Remove from asset index
                if (_assetIndex.TryGetValue(rule.AssetId, out var ruleIds))
                {
                    // Create new bag without the deleted rule ID
                    var newBag = new ConcurrentBag<string>(ruleIds.Where(id => id != ruleId));
                    _assetIndex.TryUpdate(rule.AssetId, newBag, ruleIds);
                }

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets all rules for an asset
        /// </summary>
        public Task<List<WhitelistRule>> GetRulesForAssetAsync(ulong assetId)
        {
            return GetRulesForAssetAsync(assetId, null, null, null);
        }

        /// <summary>
        /// Gets rules for an asset with filtering
        /// </summary>
        public Task<List<WhitelistRule>> GetRulesForAssetAsync(
            ulong assetId,
            WhitelistRuleType? ruleType = null,
            string? network = null,
            bool? isEnabled = null)
        {
            if (!_assetIndex.TryGetValue(assetId, out var ruleIds))
            {
                return Task.FromResult(new List<WhitelistRule>());
            }

            var rules = ruleIds
                .Select(id => _rules.TryGetValue(id, out var rule) ? rule : null)
                .Where(rule => rule != null)
                .Select(rule => rule!)
                .AsEnumerable();

            // Apply filters
            if (ruleType.HasValue)
            {
                rules = rules.Where(r => r.RuleType == ruleType.Value);
            }

            if (!string.IsNullOrEmpty(network))
            {
                rules = rules.Where(r => r.Network == null || 
                    r.Network.Equals(network, StringComparison.OrdinalIgnoreCase));
            }

            if (isEnabled.HasValue)
            {
                rules = rules.Where(r => r.IsEnabled == isEnabled.Value);
            }

            // Sort by priority (descending)
            var sortedRules = rules.OrderByDescending(r => r.Priority).ToList();

            return Task.FromResult(sortedRules);
        }

        /// <summary>
        /// Adds an audit log entry for rule changes
        /// </summary>
        public Task<WhitelistRuleAuditLog> AddAuditLogAsync(WhitelistRuleAuditLog auditLog)
        {
            _auditLogs.AddOrUpdate(
                auditLog.RuleId,
                new ConcurrentBag<WhitelistRuleAuditLog> { auditLog },
                (key, existing) =>
                {
                    existing.Add(auditLog);
                    return existing;
                });

            return Task.FromResult(auditLog);
        }

        /// <summary>
        /// Gets audit log entries for a rule
        /// </summary>
        public Task<List<WhitelistRuleAuditLog>> GetAuditLogAsync(
            string ruleId,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            if (!_auditLogs.TryGetValue(ruleId, out var logs))
            {
                return Task.FromResult(new List<WhitelistRuleAuditLog>());
            }

            var filteredLogs = logs.AsEnumerable();

            if (fromDate.HasValue)
            {
                filteredLogs = filteredLogs.Where(log => log.PerformedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                filteredLogs = filteredLogs.Where(log => log.PerformedAt <= toDate.Value);
            }

            var sortedLogs = filteredLogs
                .OrderByDescending(log => log.PerformedAt)
                .ToList();

            return Task.FromResult(sortedLogs);
        }

        /// <summary>
        /// Gets audit log entries for an asset
        /// </summary>
        public Task<List<WhitelistRuleAuditLog>> GetAuditLogForAssetAsync(
            ulong assetId,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            // Get all rules for the asset
            if (!_assetIndex.TryGetValue(assetId, out var ruleIds))
            {
                return Task.FromResult(new List<WhitelistRuleAuditLog>());
            }

            // Collect all audit logs for these rules
            var allLogs = new List<WhitelistRuleAuditLog>();
            foreach (var ruleId in ruleIds)
            {
                if (_auditLogs.TryGetValue(ruleId, out var logs))
                {
                    allLogs.AddRange(logs);
                }
            }

            // Apply filters
            var filteredLogs = allLogs.AsEnumerable();

            if (fromDate.HasValue)
            {
                filteredLogs = filteredLogs.Where(log => log.PerformedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                filteredLogs = filteredLogs.Where(log => log.PerformedAt <= toDate.Value);
            }

            var sortedLogs = filteredLogs
                .OrderByDescending(log => log.PerformedAt)
                .ToList();

            return Task.FromResult(sortedLogs);
        }
    }
}
