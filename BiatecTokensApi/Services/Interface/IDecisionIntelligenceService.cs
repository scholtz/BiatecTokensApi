using BiatecTokensApi.Models.DecisionIntelligence;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for decision intelligence analytics and insights
    /// </summary>
    /// <remarks>
    /// Provides token-level metrics, benchmark comparisons, and scenario evaluations
    /// with data quality metadata for trustworthy decision support.
    /// </remarks>
    public interface IDecisionIntelligenceService
    {
        /// <summary>
        /// Get insight metrics for a token
        /// </summary>
        /// <param name="request">Metrics request parameters</param>
        /// <returns>Insight metrics response with quality metadata</returns>
        Task<InsightMetricsResponse> GetInsightMetricsAsync(GetInsightMetricsRequest request);

        /// <summary>
        /// Get benchmark comparison between assets
        /// </summary>
        /// <param name="request">Benchmark comparison request</param>
        /// <returns>Normalized benchmark comparison response</returns>
        Task<BenchmarkComparisonResponse> GetBenchmarkComparisonAsync(GetBenchmarkComparisonRequest request);

        /// <summary>
        /// Evaluate scenario projections for a token
        /// </summary>
        /// <param name="request">Scenario evaluation request</param>
        /// <returns>Scenario evaluation response with modeled ranges</returns>
        Task<ScenarioEvaluationResponse> EvaluateScenarioAsync(EvaluateScenarioRequest request);
    }
}
