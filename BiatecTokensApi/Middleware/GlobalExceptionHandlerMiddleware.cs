using BiatecTokensApi.Models;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BiatecTokensApi.Middleware
{
    /// <summary>
    /// Global exception handling middleware that catches unhandled exceptions and returns standardized error responses
    /// </summary>
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
        private readonly IHostEnvironment _env;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalExceptionHandlerMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="env">Host environment</param>
        public GlobalExceptionHandlerMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlerMiddleware> logger,
            IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Invokes the middleware
        /// </summary>
        /// <param name="context">HTTP context</param>
        /// <returns>Task representing the async operation</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // Sanitize path to remove query parameters and potential injection attempts
                var sanitizedPath = SanitizePath(context.Request.Path);
                
                _logger.LogError(ex, "Unhandled exception occurred. Path: {Path}, Method: {Method}", 
                    sanitizedPath, context.Request.Method);
                
                await HandleExceptionAsync(context, ex);
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

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            
            var correlationId = context.TraceIdentifier;
            var sanitizedPath = SanitizePath(context.Request.Path);
            
            var response = new ApiErrorResponse
            {
                Success = false,
                Timestamp = DateTime.UtcNow,
                Path = sanitizedPath,
                CorrelationId = correlationId
            };

            // Determine error code and status code based on exception type
            switch (exception)
            {
                case ArgumentNullException:
                case ArgumentException:
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    response.ErrorCode = "BAD_REQUEST";
                    response.ErrorMessage = "Invalid request parameters";
                    if (_env.IsDevelopment())
                    {
                        response.Details = new Dictionary<string, object>
                        {
                            { "exceptionType", exception.GetType().Name },
                            { "exceptionMessage", exception.Message }
                        };
                    }
                    break;

                case UnauthorizedAccessException:
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    response.ErrorCode = "UNAUTHORIZED";
                    response.ErrorMessage = "Authentication is required to access this resource";
                    break;

                case InvalidOperationException:
                    context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                    response.ErrorCode = "INVALID_OPERATION";
                    response.ErrorMessage = "The requested operation is not valid in the current state";
                    if (_env.IsDevelopment())
                    {
                        response.Details = new Dictionary<string, object>
                        {
                            { "exceptionMessage", exception.Message }
                        };
                    }
                    break;

                case TimeoutException:
                    context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    response.ErrorCode = "TIMEOUT";
                    response.ErrorMessage = "The request timed out. Please try again later";
                    break;

                case HttpRequestException httpEx:
                    context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                    response.ErrorCode = "EXTERNAL_SERVICE_ERROR";
                    response.ErrorMessage = "An error occurred while communicating with an external service";
                    if (_env.IsDevelopment())
                    {
                        response.Details = new Dictionary<string, object>
                        {
                            { "exceptionMessage", httpEx.Message }
                        };
                    }
                    break;

                default:
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response.ErrorCode = "INTERNAL_SERVER_ERROR";
                    response.ErrorMessage = "An unexpected error occurred while processing your request";
                    
                    // Only include detailed error information in development
                    if (_env.IsDevelopment())
                    {
                        response.Details = new Dictionary<string, object>
                        {
                            { "exceptionType", exception.GetType().Name },
                            { "exceptionMessage", exception.Message },
                            { "stackTrace", exception.StackTrace ?? "No stack trace available" }
                        };
                    }
                    break;
            }

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }

    /// <summary>
    /// Extension methods for registering the global exception handler middleware
    /// </summary>
    public static class GlobalExceptionHandlerMiddlewareExtensions
    {
        /// <summary>
        /// Adds the global exception handler middleware to the application pipeline
        /// </summary>
        /// <param name="app">The application builder</param>
        /// <returns>The application builder for method chaining</returns>
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        {
            return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
        }
    }
}
