namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for collecting and exposing API metrics
    /// </summary>
    public interface IMetricsService
    {
        /// <summary>
        /// Records a successful API request
        /// </summary>
        /// <param name="endpoint">Endpoint path</param>
        /// <param name="method">HTTP method</param>
        /// <param name="durationMs">Request duration in milliseconds</param>
        void RecordRequest(string endpoint, string method, double durationMs);

        /// <summary>
        /// Records an API error
        /// </summary>
        /// <param name="endpoint">Endpoint path</param>
        /// <param name="method">HTTP method</param>
        /// <param name="errorCode">Error code</param>
        void RecordError(string endpoint, string method, string errorCode);

        /// <summary>
        /// Records a token deployment attempt
        /// </summary>
        /// <param name="tokenType">Type of token</param>
        /// <param name="success">Whether deployment succeeded</param>
        /// <param name="durationMs">Deployment duration in milliseconds</param>
        void RecordDeployment(string tokenType, bool success, double durationMs);

        /// <summary>
        /// Records an RPC call
        /// </summary>
        /// <param name="network">Network name</param>
        /// <param name="operation">Operation type</param>
        /// <param name="success">Whether call succeeded</param>
        /// <param name="durationMs">Call duration in milliseconds</param>
        void RecordRpcCall(string network, string operation, bool success, double durationMs);

        /// <summary>
        /// Records an audit log write
        /// </summary>
        /// <param name="category">Audit category</param>
        /// <param name="success">Whether write succeeded</param>
        void RecordAuditWrite(string category, bool success);

        /// <summary>
        /// Increments a counter metric
        /// </summary>
        /// <param name="name">Metric name</param>
        /// <param name="increment">Amount to increment (default: 1)</param>
        void IncrementCounter(string name, long increment = 1);

        /// <summary>
        /// Records a value in a histogram metric
        /// </summary>
        /// <param name="name">Metric name</param>
        /// <param name="value">Value to record</param>
        void RecordHistogram(string name, double value);

        /// <summary>
        /// Sets a gauge metric to a specific value
        /// </summary>
        /// <param name="name">Metric name</param>
        /// <param name="value">Value to set</param>
        void SetGauge(string name, double value);

        /// <summary>
        /// Gets all current metrics
        /// </summary>
        /// <returns>Dictionary of metric categories and their data</returns>
        Dictionary<string, object> GetMetrics();
    }
}
