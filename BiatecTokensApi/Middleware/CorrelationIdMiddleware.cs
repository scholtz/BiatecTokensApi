namespace BiatecTokensApi.Middleware
{
    /// <summary>
    /// Middleware that ensures every request has a correlation ID for distributed tracing
    /// </summary>
    /// <remarks>
    /// This middleware:
    /// - Accepts X-Correlation-ID header from clients or generates a new one
    /// - Adds correlation ID to response headers for client-side tracking
    /// - Makes correlation ID available through HttpContext.TraceIdentifier
    /// - Enables end-to-end request tracing across services and logs
    /// </remarks>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private const string CorrelationIdHeader = "X-Correlation-ID";

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline</param>
        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Invokes the middleware to process the request
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <returns>Task representing the async operation</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            // Try to get correlation ID from request header
            string correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault() 
                ?? context.TraceIdentifier;

            // Ensure we have a valid correlation ID
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
            }

            // Update TraceIdentifier for logging
            context.TraceIdentifier = correlationId;

            // Add correlation ID to response headers for client tracking
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
                {
                    context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);
                }
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }

    /// <summary>
    /// Extension methods for registering the correlation ID middleware
    /// </summary>
    public static class CorrelationIdMiddlewareExtensions
    {
        /// <summary>
        /// Adds the correlation ID middleware to the application pipeline
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <returns>The application builder for method chaining</returns>
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        {
            return app.UseMiddleware<CorrelationIdMiddleware>();
        }
    }
}
