using BiatecTokensApi.Models.Whitelist;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory implementation of whitelist repository
    /// </summary>
    /// <remarks>
    /// This implementation uses concurrent collections for thread-safe operations.
    /// In a production environment, this should be replaced with a proper database implementation.
    /// </remarks>
    public class WhitelistRepository : IWhitelistRepository
    {
        private readonly ConcurrentDictionary<string, WhitelistEntry> _whitelist;
        private readonly ConcurrentBag<WhitelistAuditLogEntry> _auditLog;
        private readonly ILogger<WhitelistRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhitelistRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public WhitelistRepository(ILogger<WhitelistRepository> logger)
        {
            _whitelist = new ConcurrentDictionary<string, WhitelistEntry>();
            _auditLog = new ConcurrentBag<WhitelistAuditLogEntry>();
            _logger = logger;
        }

        /// <summary>
        /// Adds a new whitelist entry
        /// </summary>
        /// <param name="entry">The whitelist entry to add</param>
        /// <returns>True if the entry was added successfully</returns>
        public Task<bool> AddEntryAsync(WhitelistEntry entry)
        {
            var key = GetKey(entry.AssetId, entry.Address);
            var added = _whitelist.TryAdd(key, entry);
            
            if (added)
            {
                _logger.LogInformation("Added whitelist entry for address {Address} on asset {AssetId}", 
                    entry.Address, entry.AssetId);
            }
            else
            {
                _logger.LogWarning("Failed to add whitelist entry for address {Address} on asset {AssetId} - entry already exists", 
                    entry.Address, entry.AssetId);
            }
            
            return Task.FromResult(added);
        }

        /// <summary>
        /// Gets all whitelist entries for a specific asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="status">Optional status filter</param>
        /// <returns>List of whitelist entries</returns>
        public Task<List<WhitelistEntry>> GetEntriesByAssetIdAsync(ulong assetId, WhitelistStatus? status = null)
        {
            var entries = _whitelist.Values
                .Where(e => e.AssetId == assetId)
                .ToList();

            if (status.HasValue)
            {
                entries = entries.Where(e => e.Status == status.Value).ToList();
            }

            _logger.LogDebug("Retrieved {Count} whitelist entries for asset {AssetId}", entries.Count, assetId);
            
            return Task.FromResult(entries);
        }

        /// <summary>
        /// Gets a specific whitelist entry by asset ID and address
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="address">The Algorand address</param>
        /// <returns>The whitelist entry if found, null otherwise</returns>
        public Task<WhitelistEntry?> GetEntryAsync(ulong assetId, string address)
        {
            var key = GetKey(assetId, address);
            _whitelist.TryGetValue(key, out var entry);
            
            return Task.FromResult(entry);
        }

        /// <summary>
        /// Updates an existing whitelist entry
        /// </summary>
        /// <param name="entry">The whitelist entry to update</param>
        /// <returns>True if the entry was updated successfully</returns>
        public Task<bool> UpdateEntryAsync(WhitelistEntry entry)
        {
            var key = GetKey(entry.AssetId, entry.Address);
            
            if (_whitelist.TryGetValue(key, out var existingEntry))
            {
                entry.Id = existingEntry.Id;
                entry.CreatedAt = existingEntry.CreatedAt;
                entry.CreatedBy = existingEntry.CreatedBy;
                entry.UpdatedAt = DateTime.UtcNow;
                
                var updated = _whitelist.TryUpdate(key, entry, existingEntry);
                
                if (updated)
                {
                    _logger.LogInformation("Updated whitelist entry for address {Address} on asset {AssetId}", 
                        entry.Address, entry.AssetId);
                }
                
                return Task.FromResult(updated);
            }
            
            _logger.LogWarning("Failed to update whitelist entry for address {Address} on asset {AssetId} - entry not found", 
                entry.Address, entry.AssetId);
            
            return Task.FromResult(false);
        }

        /// <summary>
        /// Removes a whitelist entry
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="address">The Algorand address</param>
        /// <returns>True if the entry was removed successfully</returns>
        public Task<bool> RemoveEntryAsync(ulong assetId, string address)
        {
            var key = GetKey(assetId, address);
            var removed = _whitelist.TryRemove(key, out _);
            
            if (removed)
            {
                _logger.LogInformation("Removed whitelist entry for address {Address} on asset {AssetId}", 
                    address, assetId);
            }
            else
            {
                _logger.LogWarning("Failed to remove whitelist entry for address {Address} on asset {AssetId} - entry not found", 
                    address, assetId);
            }
            
            return Task.FromResult(removed);
        }

        /// <summary>
        /// Checks if an address is whitelisted for a specific asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="address">The Algorand address</param>
        /// <returns>True if the address is actively whitelisted</returns>
        public Task<bool> IsWhitelistedAsync(ulong assetId, string address)
        {
            var key = GetKey(assetId, address);
            
            if (_whitelist.TryGetValue(key, out var entry))
            {
                return Task.FromResult(entry.Status == WhitelistStatus.Active);
            }
            
            return Task.FromResult(false);
        }

        /// <summary>
        /// Adds an audit log entry for a whitelist change
        /// </summary>
        /// <param name="auditEntry">The audit log entry to add</param>
        /// <returns>True if the audit entry was added successfully</returns>
        public Task<bool> AddAuditLogEntryAsync(WhitelistAuditLogEntry auditEntry)
        {
            try
            {
                _auditLog.Add(auditEntry);
                _logger.LogDebug("Added audit log entry for {ActionType} on address {Address} for asset {AssetId}",
                    auditEntry.ActionType, auditEntry.Address, auditEntry.AssetId);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add audit log entry");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Gets audit log entries for a specific asset with optional filters
        /// </summary>
        /// <param name="request">The audit log request with filters and pagination</param>
        /// <returns>List of audit log entries</returns>
        public Task<List<WhitelistAuditLogEntry>> GetAuditLogAsync(GetWhitelistAuditLogRequest request)
        {
            var query = _auditLog.Where(e => e.AssetId == request.AssetId);

            // Apply filters
            if (!string.IsNullOrEmpty(request.Address))
            {
                query = query.Where(e => e.Address.Equals(request.Address, StringComparison.OrdinalIgnoreCase));
            }

            if (request.ActionType.HasValue)
            {
                query = query.Where(e => e.ActionType == request.ActionType.Value);
            }

            if (!string.IsNullOrEmpty(request.PerformedBy))
            {
                query = query.Where(e => e.PerformedBy.Equals(request.PerformedBy, StringComparison.OrdinalIgnoreCase));
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(e => e.PerformedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(e => e.PerformedAt <= request.ToDate.Value);
            }

            // Sort by most recent first
            var entries = query
                .OrderByDescending(e => e.PerformedAt)
                .ToList();

            _logger.LogDebug("Retrieved {Count} audit log entries for asset {AssetId}", entries.Count, request.AssetId);

            return Task.FromResult(entries);
        }

        private static string GetKey(ulong assetId, string address)
        {
            return $"{assetId}:{address.ToUpperInvariant()}";
        }
    }
}
