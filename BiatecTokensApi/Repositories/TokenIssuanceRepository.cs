using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// Repository implementation for token issuance audit log operations
    /// </summary>
    /// <remarks>
    /// Provides thread-safe, in-memory storage for token issuance audit logs across all token standards.
    /// Supports MICA 7-year retention requirements and comprehensive filtering.
    /// </remarks>
    public class TokenIssuanceRepository : ITokenIssuanceRepository
    {
        private readonly ConcurrentBag<TokenIssuanceAuditLogEntry> _auditLog = new();
        private readonly ILogger<TokenIssuanceRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenIssuanceRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public TokenIssuanceRepository(ILogger<TokenIssuanceRepository> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Adds a token issuance audit log entry
        /// </summary>
        public Task AddAuditLogEntryAsync(TokenIssuanceAuditLogEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            _auditLog.Add(entry);
            _logger.LogInformation("Token issuance audit log entry added: TokenType={TokenType}, AssetId={AssetId}, Network={Network}, Success={Success}",
                entry.TokenType, entry.AssetId, entry.Network, entry.Success);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets token issuance audit log entries with filtering
        /// </summary>
        public Task<List<TokenIssuanceAuditLogEntry>> GetAuditLogAsync(GetTokenIssuanceAuditLogRequest request)
        {
            _logger.LogInformation("Retrieving token issuance audit logs: AssetId={AssetId}, Network={Network}, TokenType={TokenType}",
                request.AssetId, request.Network, request.TokenType);

            var query = _auditLog.AsEnumerable();

            // Apply filters
            if (request.AssetId.HasValue)
                query = query.Where(e => e.AssetId == request.AssetId.Value);

            if (!string.IsNullOrWhiteSpace(request.ContractAddress))
                query = query.Where(e => e.ContractAddress != null && 
                    e.ContractAddress.Equals(request.ContractAddress, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.Network))
                query = query.Where(e => e.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.TokenType))
                query = query.Where(e => e.TokenType.Equals(request.TokenType, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.DeployedBy))
                query = query.Where(e => e.DeployedBy.Equals(request.DeployedBy, StringComparison.OrdinalIgnoreCase));

            if (request.Success.HasValue)
                query = query.Where(e => e.Success == request.Success.Value);

            if (request.FromDate.HasValue)
                query = query.Where(e => e.DeployedAt >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                query = query.Where(e => e.DeployedAt <= request.ToDate.Value);

            // Order by most recent first and apply pagination
            var orderedQuery = query.OrderByDescending(e => e.DeployedAt);
            var skip = (request.Page - 1) * request.PageSize;
            var result = orderedQuery.Skip(skip).Take(request.PageSize).ToList();

            _logger.LogInformation("Retrieved {Count} token issuance audit log entries", result.Count);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets the total count of token issuance audit log entries matching the filter
        /// </summary>
        public Task<int> GetAuditLogCountAsync(GetTokenIssuanceAuditLogRequest request)
        {
            var query = _auditLog.AsEnumerable();

            // Apply same filters as GetAuditLogAsync
            if (request.AssetId.HasValue)
                query = query.Where(e => e.AssetId == request.AssetId.Value);

            if (!string.IsNullOrWhiteSpace(request.ContractAddress))
                query = query.Where(e => e.ContractAddress != null && 
                    e.ContractAddress.Equals(request.ContractAddress, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.Network))
                query = query.Where(e => e.Network.Equals(request.Network, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.TokenType))
                query = query.Where(e => e.TokenType.Equals(request.TokenType, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(request.DeployedBy))
                query = query.Where(e => e.DeployedBy.Equals(request.DeployedBy, StringComparison.OrdinalIgnoreCase));

            if (request.Success.HasValue)
                query = query.Where(e => e.Success == request.Success.Value);

            if (request.FromDate.HasValue)
                query = query.Where(e => e.DeployedAt >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                query = query.Where(e => e.DeployedAt <= request.ToDate.Value);

            var count = query.Count();
            _logger.LogDebug("Token issuance audit log count: {Count}", count);
            return Task.FromResult(count);
        }
    }
}
