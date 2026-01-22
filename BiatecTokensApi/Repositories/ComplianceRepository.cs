using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository for compliance metadata
    /// </summary>
    /// <remarks>
    /// This implementation uses thread-safe concurrent collections for production-grade concurrency.
    /// Can be replaced with a database-backed implementation without API changes.
    /// </remarks>
    public class ComplianceRepository : IComplianceRepository
    {
        private readonly ConcurrentDictionary<ulong, ComplianceMetadata> _metadata = new();
        private readonly ConcurrentBag<ComplianceAuditLogEntry> _auditLog = new();
        private readonly ILogger<ComplianceRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public ComplianceRepository(ILogger<ComplianceRepository> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<bool> UpsertMetadataAsync(ComplianceMetadata metadata)
        {
            try
            {
                _metadata.AddOrUpdate(
                    metadata.AssetId,
                    metadata,
                    (key, existing) =>
                    {
                        // Preserve creation info when updating
                        metadata.CreatedBy = existing.CreatedBy;
                        metadata.CreatedAt = existing.CreatedAt;
                        metadata.UpdatedAt = DateTime.UtcNow;
                        return metadata;
                    });

                _logger.LogDebug("Upserted compliance metadata for asset {AssetId}", metadata.AssetId);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting compliance metadata for asset {AssetId}", metadata.AssetId);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<ComplianceMetadata?> GetMetadataByAssetIdAsync(ulong assetId)
        {
            _metadata.TryGetValue(assetId, out var metadata);
            return Task.FromResult(metadata);
        }

        /// <inheritdoc/>
        public Task<bool> DeleteMetadataAsync(ulong assetId)
        {
            var result = _metadata.TryRemove(assetId, out _);
            if (result)
            {
                _logger.LogDebug("Deleted compliance metadata for asset {AssetId}", assetId);
            }
            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<List<ComplianceMetadata>> ListMetadataAsync(ListComplianceMetadataRequest request)
        {
            var query = _metadata.Values.AsEnumerable();

            // Apply filters
            if (request.ComplianceStatus.HasValue)
            {
                query = query.Where(m => m.ComplianceStatus == request.ComplianceStatus.Value);
            }

            if (request.VerificationStatus.HasValue)
            {
                query = query.Where(m => m.VerificationStatus == request.VerificationStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Network))
            {
                query = query.Where(m => 
                    !string.IsNullOrEmpty(m.Network) && 
                    m.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));
            }

            // Order by creation date descending
            query = query.OrderByDescending(m => m.CreatedAt);

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            var result = query.Skip(skip).Take(request.PageSize).ToList();

            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<int> GetMetadataCountAsync(ListComplianceMetadataRequest request)
        {
            var query = _metadata.Values.AsEnumerable();

            // Apply same filters as ListMetadataAsync
            if (request.ComplianceStatus.HasValue)
            {
                query = query.Where(m => m.ComplianceStatus == request.ComplianceStatus.Value);
            }

            if (request.VerificationStatus.HasValue)
            {
                query = query.Where(m => m.VerificationStatus == request.VerificationStatus.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Network))
            {
                query = query.Where(m => 
                    !string.IsNullOrEmpty(m.Network) && 
                    m.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult(query.Count());
        }

        /// <inheritdoc/>
        public Task<bool> AddAuditLogEntryAsync(ComplianceAuditLogEntry entry)
        {
            try
            {
                _auditLog.Add(entry);
                _logger.LogDebug("Added audit log entry {Id} for action {ActionType}", entry.Id, entry.ActionType);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding audit log entry for action {ActionType}", entry.ActionType);
                return Task.FromResult(false);
            }
        }

        /// <inheritdoc/>
        public Task<List<ComplianceAuditLogEntry>> GetAuditLogAsync(GetComplianceAuditLogRequest request)
        {
            var query = _auditLog.AsEnumerable();

            // Apply filters
            if (request.AssetId.HasValue)
            {
                query = query.Where(e => e.AssetId == request.AssetId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Network))
            {
                query = query.Where(e => 
                    !string.IsNullOrEmpty(e.Network) && 
                    e.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));
            }

            if (request.ActionType.HasValue)
            {
                query = query.Where(e => e.ActionType == request.ActionType.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.PerformedBy))
            {
                query = query.Where(e => 
                    !string.IsNullOrEmpty(e.PerformedBy) && 
                    e.PerformedBy.Equals(request.PerformedBy, StringComparison.OrdinalIgnoreCase));
            }

            if (request.Success.HasValue)
            {
                query = query.Where(e => e.Success == request.Success.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(e => e.PerformedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(e => e.PerformedAt <= request.ToDate.Value);
            }

            // Order by timestamp descending (most recent first)
            query = query.OrderByDescending(e => e.PerformedAt);

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            var result = query.Skip(skip).Take(request.PageSize).ToList();

            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<int> GetAuditLogCountAsync(GetComplianceAuditLogRequest request)
        {
            var query = _auditLog.AsEnumerable();

            // Apply same filters as GetAuditLogAsync
            if (request.AssetId.HasValue)
            {
                query = query.Where(e => e.AssetId == request.AssetId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Network))
            {
                query = query.Where(e => 
                    !string.IsNullOrEmpty(e.Network) && 
                    e.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));
            }

            if (request.ActionType.HasValue)
            {
                query = query.Where(e => e.ActionType == request.ActionType.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.PerformedBy))
            {
                query = query.Where(e => 
                    !string.IsNullOrEmpty(e.PerformedBy) && 
                    e.PerformedBy.Equals(request.PerformedBy, StringComparison.OrdinalIgnoreCase));
            }

            if (request.Success.HasValue)
            {
                query = query.Where(e => e.Success == request.Success.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(e => e.PerformedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(e => e.PerformedAt <= request.ToDate.Value);
            }

            return Task.FromResult(query.Count());
        }
    }
}
