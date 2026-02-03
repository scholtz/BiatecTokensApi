using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository implementation for security activity and audit operations
    /// </summary>
    /// <remarks>
    /// This implementation uses in-memory storage for MVP. For production, this should be
    /// replaced with a persistent storage solution (database, cloud storage, etc.).
    /// </remarks>
    public class SecurityActivityRepository : ISecurityActivityRepository
    {
        private readonly ConcurrentBag<SecurityActivityEvent> _activityEvents = new();
        private readonly ConcurrentBag<TokenDeploymentTransaction> _transactions = new();
        private readonly ConcurrentDictionary<string, (ExportAuditTrailResponse Response, DateTime Expiration)> _exportCache = new();
        private readonly ILogger<SecurityActivityRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityActivityRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public SecurityActivityRepository(ILogger<SecurityActivityRepository> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Logs a security activity event
        /// </summary>
        public Task LogEventAsync(SecurityActivityEvent @event)
        {
            _activityEvents.Add(@event);
            _logger.LogInformation("Security event logged: {EventType} for account {AccountId}", 
                @event.EventType, 
                LoggingHelper.SanitizeLogInput(@event.AccountId));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets security activity events with filtering and pagination
        /// </summary>
        public Task<List<SecurityActivityEvent>> GetActivityEventsAsync(GetSecurityActivityRequest request)
        {
            var query = _activityEvents.AsEnumerable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.AccountId))
            {
                query = query.Where(e => e.AccountId == request.AccountId);
            }

            if (request.EventType.HasValue)
            {
                query = query.Where(e => e.EventType == request.EventType.Value);
            }

            if (request.Severity.HasValue)
            {
                query = query.Where(e => e.Severity == request.Severity.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(e => e.Timestamp >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(e => e.Timestamp <= request.ToDate.Value);
            }

            if (request.Success.HasValue)
            {
                query = query.Where(e => e.Success == request.Success.Value);
            }

            // Order by most recent first
            query = query.OrderByDescending(e => e.Timestamp);

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            var result = query.Skip(skip).Take(request.PageSize).ToList();

            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets count of security activity events matching the filter
        /// </summary>
        public Task<int> GetActivityEventsCountAsync(GetSecurityActivityRequest request)
        {
            var query = _activityEvents.AsEnumerable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.AccountId))
            {
                query = query.Where(e => e.AccountId == request.AccountId);
            }

            if (request.EventType.HasValue)
            {
                query = query.Where(e => e.EventType == request.EventType.Value);
            }

            if (request.Severity.HasValue)
            {
                query = query.Where(e => e.Severity == request.Severity.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(e => e.Timestamp >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(e => e.Timestamp <= request.ToDate.Value);
            }

            if (request.Success.HasValue)
            {
                query = query.Where(e => e.Success == request.Success.Value);
            }

            return Task.FromResult(query.Count());
        }

        /// <summary>
        /// Gets token deployment transaction history
        /// </summary>
        public Task<List<TokenDeploymentTransaction>> GetTransactionHistoryAsync(GetTransactionHistoryRequest request)
        {
            var query = _transactions.AsEnumerable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.AccountId))
            {
                query = query.Where(t => t.CreatorAddress == request.AccountId);
            }

            if (!string.IsNullOrEmpty(request.Network))
            {
                query = query.Where(t => t.Network == request.Network);
            }

            if (!string.IsNullOrEmpty(request.TokenStandard))
            {
                query = query.Where(t => t.TokenStandard == request.TokenStandard);
            }

            if (!string.IsNullOrEmpty(request.Status))
            {
                query = query.Where(t => t.Status == request.Status);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(t => t.DeployedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(t => t.DeployedAt <= request.ToDate.Value);
            }

            // Order by most recent first
            query = query.OrderByDescending(t => t.DeployedAt);

            // Apply pagination
            var skip = (request.Page - 1) * request.PageSize;
            var result = query.Skip(skip).Take(request.PageSize).ToList();

            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets count of token deployment transactions matching the filter
        /// </summary>
        public Task<int> GetTransactionHistoryCountAsync(GetTransactionHistoryRequest request)
        {
            var query = _transactions.AsEnumerable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.AccountId))
            {
                query = query.Where(t => t.CreatorAddress == request.AccountId);
            }

            if (!string.IsNullOrEmpty(request.Network))
            {
                query = query.Where(t => t.Network == request.Network);
            }

            if (!string.IsNullOrEmpty(request.TokenStandard))
            {
                query = query.Where(t => t.TokenStandard == request.TokenStandard);
            }

            if (!string.IsNullOrEmpty(request.Status))
            {
                query = query.Where(t => t.Status == request.Status);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(t => t.DeployedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(t => t.DeployedAt <= request.ToDate.Value);
            }

            return Task.FromResult(query.Count());
        }

        /// <summary>
        /// Checks if an export with the given idempotency key exists
        /// </summary>
        public Task<ExportAuditTrailResponse?> GetCachedExportAsync(string idempotencyKey, string accountId)
        {
            var cacheKey = $"{accountId}:{idempotencyKey}";
            
            if (_exportCache.TryGetValue(cacheKey, out var cached))
            {
                // Check if cache is expired
                if (cached.Expiration > DateTime.UtcNow)
                {
                    _logger.LogInformation("Cache hit for export with idempotency key: {IdempotencyKey}", 
                        LoggingHelper.SanitizeLogInput(idempotencyKey));
                    return Task.FromResult<ExportAuditTrailResponse?>(cached.Response);
                }
                else
                {
                    // Remove expired cache entry
                    _exportCache.TryRemove(cacheKey, out _);
                }
            }

            return Task.FromResult<ExportAuditTrailResponse?>(null);
        }

        /// <summary>
        /// Caches an export response with idempotency key
        /// </summary>
        public Task CacheExportAsync(string idempotencyKey, string accountId, ExportAuditTrailResponse response, int expirationHours = 24)
        {
            var cacheKey = $"{accountId}:{idempotencyKey}";
            var expiration = DateTime.UtcNow.AddHours(expirationHours);
            
            _exportCache.AddOrUpdate(cacheKey, (response, expiration), (key, old) => (response, expiration));
            
            _logger.LogInformation("Cached export with idempotency key: {IdempotencyKey}, expires at: {Expiration}", 
                LoggingHelper.SanitizeLogInput(idempotencyKey), expiration);
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Logs a token deployment transaction (for testing/integration)
        /// </summary>
        /// <param name="transaction">The transaction to log</param>
        public void LogTransaction(TokenDeploymentTransaction transaction)
        {
            _transactions.Add(transaction);
            _logger.LogInformation("Token deployment transaction logged: {TransactionId} for network {Network}", 
                LoggingHelper.SanitizeLogInput(transaction.TransactionId),
                LoggingHelper.SanitizeLogInput(transaction.Network));
        }
    }
}
