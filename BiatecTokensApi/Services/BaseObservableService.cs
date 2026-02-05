using BiatecTokensApi.Services.Interface;
using System.Diagnostics;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Base class for services that provides automatic observability features
    /// </summary>
    /// <remarks>
    /// This base class provides:
    /// - Automatic metrics recording for operations
    /// - Correlation ID access from HTTP context
    /// - Structured logging with correlation IDs
    /// - Standardized error handling patterns
    /// 
    /// Services inheriting from this class get observability features automatically.
    /// </remarks>
    public abstract class BaseObservableService
    {
        /// <summary>
        /// Metrics service for recording operations
        /// </summary>
        protected readonly IMetricsService Metrics;

        /// <summary>
        /// Logger instance
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// HTTP context accessor for correlation ID access
        /// </summary>
        protected readonly IHttpContextAccessor? HttpContextAccessor;

        /// <summary>
        /// Gets the current correlation ID from HTTP context or generates one
        /// </summary>
        protected string CorrelationId =>
            HttpContextAccessor?.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseObservableService"/> class
        /// </summary>
        /// <param name="metrics">Metrics service</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="httpContextAccessor">HTTP context accessor (optional)</param>
        protected BaseObservableService(
            IMetricsService metrics, 
            ILogger logger, 
            IHttpContextAccessor? httpContextAccessor = null)
        {
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            HttpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Executes an operation with automatic metrics and logging
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operationName">Name of the operation for metrics</param>
        /// <param name="operation">The operation to execute</param>
        /// <param name="logParameters">Optional parameters to log</param>
        /// <returns>Result of the operation</returns>
        protected async Task<T> ExecuteWithMetricsAsync<T>(
            string operationName,
            Func<Task<T>> operation,
            Dictionary<string, object>? logParameters = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var correlationId = CorrelationId;

            try
            {
                LogOperationStart(operationName, correlationId, logParameters);
                
                var result = await operation();
                
                stopwatch.Stop();
                Metrics.RecordHistogram($"{operationName}.duration_ms", stopwatch.Elapsed.TotalMilliseconds);
                Metrics.IncrementCounter($"{operationName}.success");
                
                LogOperationSuccess(operationName, correlationId, stopwatch.Elapsed.TotalMilliseconds);
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Metrics.RecordHistogram($"{operationName}.duration_ms", stopwatch.Elapsed.TotalMilliseconds);
                Metrics.IncrementCounter($"{operationName}.failure");
                
                LogOperationFailure(operationName, correlationId, ex, stopwatch.Elapsed.TotalMilliseconds);
                
                throw;
            }
        }

        /// <summary>
        /// Executes an operation with automatic metrics and logging (synchronous version)
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operationName">Name of the operation for metrics</param>
        /// <param name="operation">The operation to execute</param>
        /// <param name="logParameters">Optional parameters to log</param>
        /// <returns>Result of the operation</returns>
        protected T ExecuteWithMetrics<T>(
            string operationName,
            Func<T> operation,
            Dictionary<string, object>? logParameters = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var correlationId = CorrelationId;

            try
            {
                LogOperationStart(operationName, correlationId, logParameters);
                
                var result = operation();
                
                stopwatch.Stop();
                Metrics.RecordHistogram($"{operationName}.duration_ms", stopwatch.Elapsed.TotalMilliseconds);
                Metrics.IncrementCounter($"{operationName}.success");
                
                LogOperationSuccess(operationName, correlationId, stopwatch.Elapsed.TotalMilliseconds);
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Metrics.RecordHistogram($"{operationName}.duration_ms", stopwatch.Elapsed.TotalMilliseconds);
                Metrics.IncrementCounter($"{operationName}.failure");
                
                LogOperationFailure(operationName, correlationId, ex, stopwatch.Elapsed.TotalMilliseconds);
                
                throw;
            }
        }

        private void LogOperationStart(string operationName, string correlationId, Dictionary<string, object>? parameters)
        {
            if (parameters != null && parameters.Count > 0)
            {
                Logger.LogInformation(
                    "Starting operation {OperationName}. CorrelationId: {CorrelationId}, Parameters: {@Parameters}",
                    operationName,
                    correlationId,
                    parameters);
            }
            else
            {
                Logger.LogInformation(
                    "Starting operation {OperationName}. CorrelationId: {CorrelationId}",
                    operationName,
                    correlationId);
            }
        }

        private void LogOperationSuccess(string operationName, string correlationId, double durationMs)
        {
            Logger.LogInformation(
                "Operation {OperationName} completed successfully in {DurationMs}ms. CorrelationId: {CorrelationId}",
                operationName,
                durationMs,
                correlationId);
        }

        private void LogOperationFailure(string operationName, string correlationId, Exception ex, double durationMs)
        {
            Logger.LogError(
                ex,
                "Operation {OperationName} failed after {DurationMs}ms. CorrelationId: {CorrelationId}",
                operationName,
                durationMs,
                correlationId);
        }
    }
}
