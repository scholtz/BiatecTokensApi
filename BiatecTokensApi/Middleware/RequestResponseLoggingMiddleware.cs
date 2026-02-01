using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace BiatecTokensApi.Middleware
{
    /// <summary>
    /// Middleware for logging HTTP requests and responses for debugging and monitoring
    /// </summary>
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestResponseLoggingMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline</param>
        /// <param name="logger">Logger instance</param>
        public RequestResponseLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Invokes the middleware
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <returns>Task representing the async operation</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var correlationId = context.TraceIdentifier;
            
            // Sanitize path to remove query parameters and potential injection attempts
            var sanitizedPath = SanitizePath(context.Request.Path);
            var sanitizedMethod = SanitizeLogInput(context.Request.Method);

            // Log request
            _logger.LogInformation(
                "HTTP Request {Method} {Path} started. CorrelationId: {CorrelationId}",
                sanitizedMethod,
                sanitizedPath,
                correlationId);

            // Capture the original response body stream
            var originalBodyStream = context.Response.Body;

            try
            {
                // Use a memory stream to capture the response
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                await _next(context);

                stopwatch.Stop();

                // Log response
                _logger.LogInformation(
                    "HTTP Response {Method} {Path} completed with status {StatusCode} in {ElapsedMs}ms. CorrelationId: {CorrelationId}",
                    sanitizedMethod,
                    sanitizedPath,
                    context.Response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);

                // Copy response back to original stream
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                _logger.LogError(
                    ex,
                    "HTTP Request {Method} {Path} failed after {ElapsedMs}ms. CorrelationId: {CorrelationId}",
                    sanitizedMethod,
                    sanitizedPath,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
                
                throw;
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }

        /// <summary>
        /// Sanitizes the request path by removing query parameters and limiting length
        /// </summary>
        /// <param name="path">The request path to sanitize</param>
        /// <returns>Sanitized path safe for logging</returns>
        private static string SanitizePath(PathString path)
        {
            if (!path.HasValue)
            {
                return "/";
            }

            var pathValue = path.Value ?? "/";
            
            // Remove query string if present (should be handled by PathString but extra safety)
            var queryIndex = pathValue.IndexOf('?');
            if (queryIndex >= 0)
            {
                pathValue = pathValue.Substring(0, queryIndex);
            }

            // Limit length to prevent log injection
            if (pathValue.Length > 200)
            {
                pathValue = pathValue.Substring(0, 200) + "...";
            }

            // Replace any control characters or newlines that could be used for log injection
            pathValue = Regex.Replace(pathValue, @"[\r\n\t\x00-\x1F\x7F]", "");

            return pathValue;
        }

        /// <summary>
        /// Sanitizes a string input by removing control characters that could be used for log injection
        /// </summary>
        /// <param name="input">The string to sanitize</param>
        /// <returns>Sanitized string safe for logging</returns>
        private static string SanitizeLogInput(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Remove any control characters (including newlines) that could be used for log injection
            var builder = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                // Keep only non-control characters (ASCII 0x20–0x7E and all non-ASCII)
                if (ch >= 0x20 && ch != 0x7F)
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Extension methods for registering the request/response logging middleware
    /// </summary>
    public static class RequestResponseLoggingMiddlewareExtensions
    {
        /// <summary>
        /// Adds the request/response logging middleware to the application pipeline
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <returns>The application builder for method chaining</returns>
        public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<RequestResponseLoggingMiddleware>();
        }
    }
}
