using BiatecTokensApi.Models.Whitelist;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// Interface for whitelist repository operations
    /// </summary>
    public interface IWhitelistRepository
    {
        /// <summary>
        /// Adds a new whitelist entry
        /// </summary>
        /// <param name="entry">The whitelist entry to add</param>
        /// <returns>True if the entry was added successfully</returns>
        Task<bool> AddEntryAsync(WhitelistEntry entry);

        /// <summary>
        /// Gets all whitelist entries for a specific asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="status">Optional status filter</param>
        /// <returns>List of whitelist entries</returns>
        Task<List<WhitelistEntry>> GetEntriesByAssetIdAsync(ulong assetId, WhitelistStatus? status = null);

        /// <summary>
        /// Gets a specific whitelist entry by asset ID and address
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="address">The Algorand address</param>
        /// <returns>The whitelist entry if found, null otherwise</returns>
        Task<WhitelistEntry?> GetEntryAsync(ulong assetId, string address);

        /// <summary>
        /// Updates an existing whitelist entry
        /// </summary>
        /// <param name="entry">The whitelist entry to update</param>
        /// <returns>True if the entry was updated successfully</returns>
        Task<bool> UpdateEntryAsync(WhitelistEntry entry);

        /// <summary>
        /// Removes a whitelist entry
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="address">The Algorand address</param>
        /// <returns>True if the entry was removed successfully</returns>
        Task<bool> RemoveEntryAsync(ulong assetId, string address);

        /// <summary>
        /// Checks if an address is whitelisted for a specific asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="address">The Algorand address</param>
        /// <returns>True if the address is actively whitelisted</returns>
        Task<bool> IsWhitelistedAsync(ulong assetId, string address);

        /// <summary>
        /// Adds an audit log entry for a whitelist change
        /// </summary>
        /// <param name="auditEntry">The audit log entry to add</param>
        /// <returns>True if the audit entry was added successfully</returns>
        Task<bool> AddAuditLogEntryAsync(WhitelistAuditLogEntry auditEntry);

        /// <summary>
        /// Gets audit log entries for a specific asset with optional filters
        /// </summary>
        /// <param name="request">The audit log request with filters and pagination</param>
        /// <returns>List of audit log entries</returns>
        Task<List<WhitelistAuditLogEntry>> GetAuditLogAsync(GetWhitelistAuditLogRequest request);

        /// <summary>
        /// Gets the count of whitelist entries for a specific asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <returns>Count of whitelist entries</returns>
        Task<int> GetEntriesCountAsync(ulong assetId);
    }
}
