using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace BiatecTokensApi.Filters
{
    /// <summary>
    /// Attribute that enforces idempotency for API endpoints using the Idempotency-Key header
    /// </summary>
    /// <remarks>
    /// This filter ensures that duplicate requests with the same idempotency key return the same response.
    /// It stores the response in memory for a configurable duration (default 24 hours).
    /// 
    /// **Security:** The filter validates that cached requests match current request parameters.
    /// If the same idempotency key is used with different parameters, a warning is logged and
    /// the request is rejected to prevent bypassing business logic.
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
            var logger = context.HttpContext.RequestServices.GetService<ILogger<IdempotencyKeyAttribute>>();

            // Clean up expired entries periodically
            CleanupExpiredEntries();

            // Compute hash of request parameters for validation
            var requestHash = ComputeRequestHash(context, context.ActionArguments);

            // Check if we've seen this key before
            if (_cache.TryGetValue(key, out var record))
            {
                // Check if the record has expired
                if (DateTime.UtcNow - record.Timestamp < Expiration)
                {
                    // Validate that the request parameters match
                    if (record.RequestHash != requestHash)
                    {
                        logger?.LogWarning(
                            "Idempotency key reused with different parameters. Key: {Key}, CorrelationId: {CorrelationId}",
                            key, context.HttpContext.TraceIdentifier);

                        context.Result = new BadRequestObjectResult(new
                        {
                            success = false,
                            errorCode = "IDEMPOTENCY_KEY_MISMATCH",
                            errorMessage = "The provided idempotency key has been used with different request parameters. Please use a unique key for this request or reuse the same parameters.",
                            correlationId = context.HttpContext.TraceIdentifier
                        });
                        return;
                    }

                    // Return cached response
                    context.Result = new ObjectResult(record.Response)
                    {
                        StatusCode = record.StatusCode
                    };
                    context.HttpContext.Response.Headers["X-Idempotency-Hit"] = "true";
                    
                    logger?.LogDebug(
                        "Idempotency cache hit. Key: {Key}, CorrelationId: {CorrelationId}",
                        key, context.HttpContext.TraceIdentifier);
                    
                    return;
                }
                else
                {
                    // Expired - remove it and continue
                    _cache.TryRemove(key, out _);
                    logger?.LogDebug(
                        "Idempotency record expired and removed. Key: {Key}",
                        key);
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
                    Timestamp = DateTime.UtcNow,
                    RequestHash = requestHash
                };

                _cache.TryAdd(key, newRecord);
                executedContext.HttpContext.Response.Headers["X-Idempotency-Hit"] = "false";
                
                logger?.LogDebug(
                    "Idempotency record cached. Key: {Key}, StatusCode: {StatusCode}",
                    key, newRecord.StatusCode);
            }
        }

        /// <summary>
        /// Computes a hash of the request parameters for validation
        /// </summary>
        /// <param name="context">Action executing context</param>
        /// <param name="arguments">Action arguments</param>
        /// <returns>Hash string</returns>
        private string ComputeRequestHash(ActionExecutingContext context, IDictionary<string, object?> arguments)
        {
            try
            {
                // Serialize arguments to JSON for consistent hashing
                var json = JsonSerializer.Serialize(arguments, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
                });

                // Compute SHA256 hash
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
                return Convert.ToBase64String(hashBytes);
            }
            catch (Exception ex)
            {
                // Log serialization failure and return empty hash
                var logger = context.HttpContext?.RequestServices?.GetService<ILogger<IdempotencyKeyAttribute>>();
                logger?.LogWarning(ex, "Failed to compute request hash for idempotency check. Returning empty hash.");
                
                // Return empty hash (will not match cached requests, treating as new request)
                return string.Empty;
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
            public string RequestHash { get; set; } = string.Empty;
        }
    }
}
