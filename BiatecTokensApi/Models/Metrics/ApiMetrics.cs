using System.Collections.Concurrent;

namespace BiatecTokensApi.Models.Metrics
{
    /// <summary>
    /// In-memory metrics storage for API operations
    /// </summary>
    /// <remarks>
    /// This class provides thread-safe metrics collection for monitoring API health,
    /// performance, and reliability. Metrics are stored in memory and can be exported
    /// for monitoring dashboards or Prometheus scraping.
    /// </remarks>
    public class ApiMetrics
    {
        private readonly ConcurrentDictionary<string, long> _counters = new();
        private readonly ConcurrentDictionary<string, ConcurrentBag<double>> _histograms = new();
        private readonly ConcurrentDictionary<string, double> _gauges = new();

        /// <summary>
        /// Increments a counter metric
        /// </summary>
        /// <param name="name">Metric name</param>
        /// <param name="increment">Amount to increment (default: 1)</param>
        public void IncrementCounter(string name, long increment = 1)
        {
            _counters.AddOrUpdate(name, increment, (_, current) => current + increment);
        }

        /// <summary>
        /// Records a value in a histogram metric
        /// </summary>
        /// <param name="name">Metric name</param>
        /// <param name="value">Value to record</param>
        public void RecordHistogram(string name, double value)
        {
            var bag = _histograms.GetOrAdd(name, _ => new ConcurrentBag<double>());
            bag.Add(value);
        }

        /// <summary>
        /// Sets a gauge metric to a specific value
        /// </summary>
        /// <param name="name">Metric name</param>
        /// <param name="value">Value to set</param>
        public void SetGauge(string name, double value)
        {
            _gauges.AddOrUpdate(name, value, (_, _) => value);
        }

        /// <summary>
        /// Gets all current counter values
        /// </summary>
        /// <returns>Dictionary of counter names and values</returns>
        public Dictionary<string, long> GetCounters()
        {
            return new Dictionary<string, long>(_counters);
        }

        /// <summary>
        /// Gets histogram statistics
        /// </summary>
        /// <returns>Dictionary of histogram statistics</returns>
        public Dictionary<string, HistogramStats> GetHistograms()
        {
            var result = new Dictionary<string, HistogramStats>();
            foreach (var kvp in _histograms)
            {
                var values = kvp.Value.ToArray();
                if (values.Length > 0)
                {
                    Array.Sort(values);
                    result[kvp.Key] = new HistogramStats
                    {
                        Count = values.Length,
                        Min = values[0],
                        Max = values[^1],
                        Average = values.Average(),
                        P50 = GetPercentile(values, 0.50),
                        P95 = GetPercentile(values, 0.95),
                        P99 = GetPercentile(values, 0.99)
                    };
                }
            }
            return result;
        }

        /// <summary>
        /// Gets all current gauge values
        /// </summary>
        /// <returns>Dictionary of gauge names and values</returns>
        public Dictionary<string, double> GetGauges()
        {
            return new Dictionary<string, double>(_gauges);
        }

        /// <summary>
        /// Resets all metrics (for testing or periodic resets)
        /// </summary>
        public void Reset()
        {
            _counters.Clear();
            _histograms.Clear();
            _gauges.Clear();
        }

        private static double GetPercentile(double[] sortedValues, double percentile)
        {
            if (sortedValues.Length == 0) return 0;
            if (sortedValues.Length == 1) return sortedValues[0];

            var index = (int)Math.Ceiling(sortedValues.Length * percentile) - 1;
            index = Math.Max(0, Math.Min(sortedValues.Length - 1, index));
            return sortedValues[index];
        }
    }

    /// <summary>
    /// Statistics for histogram metrics
    /// </summary>
    public class HistogramStats
    {
        /// <summary>
        /// Number of values recorded
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Minimum value
        /// </summary>
        public double Min { get; set; }

        /// <summary>
        /// Maximum value
        /// </summary>
        public double Max { get; set; }

        /// <summary>
        /// Average value
        /// </summary>
        public double Average { get; set; }

        /// <summary>
        /// 50th percentile (median)
        /// </summary>
        public double P50 { get; set; }

        /// <summary>
        /// 95th percentile
        /// </summary>
        public double P95 { get; set; }

        /// <summary>
        /// 99th percentile
        /// </summary>
        public double P99 { get; set; }
    }
}
