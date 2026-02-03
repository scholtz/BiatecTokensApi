using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Concurrent;
using System.Text.Json;

namespace BiatecTokensApi.Filters
{
    /// <summary>
    /// Attribute that enforces idempotency for API endpoints using the Idempotency-Key header
    /// </summary>
    /// <remarks>
    /// This filter ensures that duplicate requests with the same idempotency key return the same response.
    /// It stores the response in memory for a configurable duration (default 24 hours).
    /// 
    /// Usage:
    /// [IdempotencyKey]
    /// [HttpPost("create")]
    /// public async Task&lt;IActionResult&gt; Create([FromBody] Request request) { ... }
    /// 
    /// Clients should include an Idempotency-Key header:
    /// Idempotency-Key: unique-request-identifier
    /// 
    /// If the key is missing, the request proceeds normally without idempotency protection.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class IdempotencyKeyAttribute : ActionFilterAttribute
    {
        private static readonly ConcurrentDictionary<string, IdempotencyRecord> _cache = new();
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(24);
        private const string IdempotencyKeyHeader = "Idempotency-Key";

        /// <summary>
        /// Cache expiration time for idempotency records
        /// </summary>
        public TimeSpan Expiration { get; set; } = DefaultExpiration;

        /// <summary>
        /// Executes before the action method runs
        /// </summary>
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Check for idempotency key in request headers
            if (!context.HttpContext.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var idempotencyKey) || 
                string.IsNullOrWhiteSpace(idempotencyKey))
            {
                // No idempotency key provided - proceed normally
                await next();
                return;
            }

            var key = idempotencyKey.ToString();

            // Clean up expired entries periodically
            CleanupExpiredEntries();

            // Check if we've seen this key before
            if (_cache.TryGetValue(key, out var record))
            {
                // Check if the record has expired
                if (DateTime.UtcNow - record.Timestamp < Expiration)
                {
                    // Return cached response
                    context.Result = new ObjectResult(record.Response)
                    {
                        StatusCode = record.StatusCode
                    };
                    context.HttpContext.Response.Headers.Add("X-Idempotency-Hit", "true");
                    return;
                }
                else
                {
                    // Expired - remove it and continue
                    _cache.TryRemove(key, out _);
                }
            }

            // Execute the action
            var executedContext = await next();

            // Cache the response if the action was successful
            if (executedContext.Result is ObjectResult objectResult)
            {
                var newRecord = new IdempotencyRecord
                {
                    Key = key,
                    Response = objectResult.Value,
                    StatusCode = objectResult.StatusCode ?? 200,
                    Timestamp = DateTime.UtcNow
                };

                _cache.TryAdd(key, newRecord);
                executedContext.HttpContext.Response.Headers.Add("X-Idempotency-Hit", "false");
            }
        }

        /// <summary>
        /// Removes expired entries from the cache
        /// </summary>
        private void CleanupExpiredEntries()
        {
            // Only cleanup periodically (roughly 1% of requests)
            if (Random.Shared.Next(100) != 0) return;

            var now = DateTime.UtcNow;
            var expiredKeys = _cache
                .Where(kvp => now - kvp.Value.Timestamp >= Expiration)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Internal record structure for storing idempotency information
        /// </summary>
        private class IdempotencyRecord
        {
            public string Key { get; set; } = string.Empty;
            public object? Response { get; set; }
            public int StatusCode { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
