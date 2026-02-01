using System.Diagnostics;
using System.Text;

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

            // Log request
            _logger.LogInformation(
                "HTTP Request {Method} {Path} started. CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
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
                    context.Request.Method,
                    context.Request.Path,
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
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    correlationId);
                
                throw;
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
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
