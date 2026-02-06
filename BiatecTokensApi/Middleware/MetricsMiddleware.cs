using BiatecTokensApi.Services.Interface;
using System.Diagnostics;

namespace BiatecTokensApi.Middleware
{
    /// <summary>
    /// Middleware that tracks metrics for all HTTP requests
    /// </summary>
    public class MetricsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<MetricsMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MetricsMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline</param>
        /// <param name="logger">Logger instance</param>
        public MetricsMiddleware(RequestDelegate next, ILogger<MetricsMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Invokes the middleware
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <param name="metricsService">Metrics service (injected per request)</param>
        /// <returns>Task representing the async operation</returns>
        public async Task InvokeAsync(HttpContext context, IMetricsService metricsService)
        {
            var stopwatch = Stopwatch.StartNew();
            var path = context.Request.Path.Value ?? "/";
            var method = context.Request.Method;

            try
            {
                await _next(context);
                stopwatch.Stop();

                // Record successful request
                metricsService.RecordRequest(path, method, stopwatch.Elapsed.TotalMilliseconds);

                // Record errors based on status code
                if (context.Response.StatusCode >= 400)
                {
                    var errorCode = DetermineErrorCode(context.Response.StatusCode);
                    metricsService.RecordError(path, method, errorCode);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                // Record error
                metricsService.RecordError(path, method, "UNHANDLED_EXCEPTION");
                
                _logger.LogError(ex, "Unhandled exception in request {Method} {Path}", method, path);
                throw;
            }
        }

        private static string DetermineErrorCode(int statusCode)
        {
            return statusCode switch
            {
                400 => "BAD_REQUEST",
                401 => "UNAUTHORIZED",
                403 => "FORBIDDEN",
                404 => "NOT_FOUND",
                408 => "TIMEOUT",
                422 => "UNPROCESSABLE_ENTITY",
                429 => "RATE_LIMIT_EXCEEDED",
                500 => "INTERNAL_SERVER_ERROR",
                502 => "BAD_GATEWAY",
                503 => "SERVICE_UNAVAILABLE",
                504 => "GATEWAY_TIMEOUT",
                _ => $"HTTP_{statusCode}"
            };
        }
    }

    /// <summary>
    /// Extension methods for registering the metrics middleware
    /// </summary>
    public static class MetricsMiddlewareExtensions
    {
        /// <summary>
        /// Adds the metrics middleware to the application pipeline
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <returns>The application builder for method chaining</returns>
        public static IApplicationBuilder UseMetrics(this IApplicationBuilder app)
        {
            return app.UseMiddleware<MetricsMiddleware>();
        }
    }
}
