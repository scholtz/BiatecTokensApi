using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.DecisionIntelligence;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Decision intelligence API for token analytics and insights
    /// </summary>
    /// <remarks>
    /// Provides comprehensive analytics endpoints for token-level metrics,
    /// benchmark comparisons, and scenario evaluations with data quality metadata.
    /// </remarks>
    [ApiController]
    [Route("api/v1/decision-intelligence")]
    [Produces("application/json")]
    public class DecisionIntelligenceController : ControllerBase
    {
        private readonly IDecisionIntelligenceService _decisionIntelligenceService;
        private readonly ILogger<DecisionIntelligenceController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DecisionIntelligenceController"/> class.
        /// </summary>
        /// <param name="decisionIntelligenceService">Decision intelligence service</param>
        /// <param name="logger">Logger instance</param>
        public DecisionIntelligenceController(
            IDecisionIntelligenceService decisionIntelligenceService,
            ILogger<DecisionIntelligenceController> logger)
        {
            _decisionIntelligenceService = decisionIntelligenceService;
            _logger = logger;
        }

        /// <summary>
        /// Get insight metrics for a token
        /// </summary>
        /// <param name="assetId">Asset ID to analyze</param>
        /// <param name="network">Network identifier (voimain-v1.0, aramidmain-v1.0, mainnet-v1.0, etc.)</param>
        /// <param name="startTime">Optional time window start (UTC, ISO 8601). Defaults to 30 days ago.</param>
        /// <param name="endTime">Optional time window end (UTC, ISO 8601). Defaults to now.</param>
        /// <param name="metrics">Optional metrics to include (comma-separated). Valid: Adoption,Retention,TransactionQuality,LiquidityHealth,ConcentrationRisk. If empty, returns all.</param>
        /// <returns>Insight metrics response with quality metadata</returns>
        /// <response code="200">Successfully retrieved insight metrics</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error</response>
        /// <remarks>
        /// Provides comprehensive token-level analytics including:
        /// 
        /// **Adoption Metrics**
        /// - Unique holders count and growth
        /// - New holders and activity rates
        /// - Adoption trend direction
        /// 
        /// **Retention Metrics**
        /// - Retention and churn rates
        /// - Average/median holding periods
        /// - Retention trend analysis
        /// 
        /// **Transaction Quality Metrics**
        /// - Success rates and transaction counts
        /// - Volume and value statistics
        /// - Quality trend indicators
        /// 
        /// **Liquidity Health Metrics**
        /// - Circulating vs locked supply
        /// - Trading volume analysis
        /// - Composite liquidity score (0-100)
        /// 
        /// **Concentration Risk Metrics**
        /// - Top holder percentages (top 10, 50, 100)
        /// - Gini coefficient and HHI
        /// - Risk level assessment
        /// 
        /// **Metadata Fields** (included in all responses)
        /// - `generated_at`: Timestamp of calculation
        /// - `data_window`: Time period analyzed
        /// - `freshness_indicator`: Fresh, Delayed, or Stale
        /// - `confidence_hint`: 0.0-1.0 confidence level
        /// - `calculation_version`: Algorithm version (e.g., "v1.0")
        /// - `caveats`: Data quality warnings
        /// - `data_completeness`: Percentage of expected data available
        /// 
        /// **Example Request:**
        /// <![CDATA[
        /// GET /api/v1/decision-intelligence/metrics?assetId=1234567&network=voimain-v1.0&metrics=Adoption,Retention
        /// ]]>
        /// 
        /// **Cache Behavior:** Responses are cached for 24 hours per asset+network+timeframe combination for performance.
        /// </remarks>
        [HttpGet("metrics")]
        [ProducesResponseType(typeof(InsightMetricsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetInsightMetrics(
            [FromQuery] ulong assetId,
            [FromQuery] string network,
            [FromQuery] DateTime? startTime = null,
            [FromQuery] DateTime? endTime = null,
            [FromQuery] string? metrics = null)
        {
            try
            {
                var requestedMetrics = string.IsNullOrWhiteSpace(metrics)
                    ? new List<string>()
                    : metrics.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

                var request = new GetInsightMetricsRequest
                {
                    AssetId = assetId,
                    Network = network,
                    StartTime = startTime,
                    EndTime = endTime,
                    RequestedMetrics = requestedMetrics
                };

                _logger.LogInformation("Fetching insight metrics: AssetId={AssetId}, Network={Network}, Metrics={Metrics}",
                    LoggingHelper.SanitizeLogInput(assetId.ToString()),
                    LoggingHelper.SanitizeLogInput(network),
                    LoggingHelper.SanitizeLogInput(string.Join(",", requestedMetrics)));

                var response = await _decisionIntelligenceService.GetInsightMetricsAsync(request);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for insight metrics: AssetId={AssetId}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(assetId.ToString()),
                    LoggingHelper.SanitizeLogInput(network));

                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INVALID_REQUEST,
                    ErrorMessage = LoggingHelper.SanitizeLogInput(ex.Message)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching insight metrics: AssetId={AssetId}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(assetId.ToString()),
                    LoggingHelper.SanitizeLogInput(network));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while fetching insight metrics"
                });
            }
        }

        /// <summary>
        /// Get normalized benchmark comparison between assets
        /// </summary>
        /// <param name="request">Benchmark comparison request with primary and comparison assets</param>
        /// <returns>Normalized benchmark comparison response</returns>
        /// <response code="200">Successfully calculated benchmark comparison</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error</response>
        /// <remarks>
        /// Compares a primary asset against competitor/peer assets with normalized metrics
        /// to enable fair comparisons across different scales and distributions.
        /// 
        /// **Normalization Methods:**
        /// 
        /// 1. **ZScore** (Standardization)
        ///    - Converts values to standard deviations from mean: `(value - mean) / stddev`
        ///    - Best for normally distributed data
        ///    - Positive values = above average, negative = below average
        /// 
        /// 2. **MinMax** (0-100 scaling)
        ///    - Scales all values to 0-100 range: `(value - min) / (max - min) * 100`
        ///    - Best for understanding relative position
        ///    - 100 = best performer, 0 = worst performer
        /// 
        /// 3. **Percentile** (Rank-based)
        ///    - Ranks assets by percentile (0-100)
        ///    - Best for understanding relative position regardless of distribution
        ///    - 100 = top performer, 0 = bottom performer
        /// 
        /// **Response Includes:**
        /// - Raw and normalized values for each asset
        /// - Percentile ranks and performance categories
        /// - Statistical summaries (mean, median, std dev, P25/P75)
        /// - Delta from primary asset for each comparison
        /// - Overall competitive position assessment
        /// - Strength and weakness metrics identification
        /// 
        /// **Normalization Context:**
        /// - Time window alignment across all assets
        /// - Sampling assumptions and unit consistency notes
        /// - Caveats about cross-network comparisons
        /// 
        /// **Example Request:**
        /// ```json
        /// POST /api/v1/decision-intelligence/benchmarks
        /// {
        ///   "primaryAsset": {
        ///     "assetId": 1234567,
        ///     "network": "voimain-v1.0",
        ///     "label": "My Token"
        ///   },
        ///   "comparisonAssets": [
        ///     { "assetId": 2345678, "network": "voimain-v1.0", "label": "Competitor A" },
        ///     { "assetId": 3456789, "network": "voimain-v1.0", "label": "Competitor B" }
        ///   ],
        ///   "metricsToCompare": ["Adoption", "Retention", "LiquidityHealth"],
        ///   "normalizationMethod": "MinMax"
        /// }
        /// ```
        /// 
        /// **Cache Behavior:** Responses are cached for 1 hour per asset combination for performance.
        /// </remarks>
        [HttpPost("benchmarks")]
        [ProducesResponseType(typeof(BenchmarkComparisonResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBenchmarkComparison([FromBody] GetBenchmarkComparisonRequest request)
        {
            try
            {
                _logger.LogInformation("Calculating benchmark comparison: PrimaryAsset={AssetId}, ComparisonCount={Count}, Method={Method}",
                    LoggingHelper.SanitizeLogInput(request.PrimaryAsset.AssetId.ToString()),
                    request.ComparisonAssets?.Count ?? 0,
                    LoggingHelper.SanitizeLogInput(request.NormalizationMethod.ToString()));

                var response = await _decisionIntelligenceService.GetBenchmarkComparisonAsync(request);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for benchmark comparison: PrimaryAsset={AssetId}",
                    LoggingHelper.SanitizeLogInput(request.PrimaryAsset?.AssetId.ToString() ?? "null"));

                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INVALID_REQUEST,
                    ErrorMessage = LoggingHelper.SanitizeLogInput(ex.Message)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating benchmark comparison: PrimaryAsset={AssetId}",
                    LoggingHelper.SanitizeLogInput(request.PrimaryAsset?.AssetId.ToString() ?? "null"));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while calculating benchmark comparison"
                });
            }
        }

        /// <summary>
        /// Evaluate scenario projections for a token
        /// </summary>
        /// <param name="request">Scenario evaluation request with baseline inputs and adjustments</param>
        /// <returns>Scenario evaluation response with modeled ranges</returns>
        /// <response code="200">Successfully evaluated scenario</response>
        /// <response code="400">Invalid request parameters</response>
        /// <response code="500">Internal server error</response>
        /// <remarks>
        /// Models future outcomes based on current state and proposed adjustments,
        /// providing optimistic, realistic, and pessimistic projections.
        /// 
        /// **Baseline Inputs** (Current State):
        /// - Current holder count
        /// - Daily transaction volume
        /// - Retention rate (0-100)
        /// - Circulating supply
        /// - Top 10 concentration percentage
        /// - Historical growth rate
        /// 
        /// **Adjustments** (What-If Changes):
        /// - `holderGrowthRateDelta`: Expected change in growth rate (percentage points)
        /// - `retentionRateDelta`: Expected change in retention (percentage points)
        /// - `transactionVolumeChangePercent`: Expected volume change (percentage)
        /// - `supplyChangeDelta`: Expected change in supply (absolute value)
        /// - `whaleDistributionEvent`: Models major holder distribution (boolean)
        /// - `externalEvents`: Contextual event descriptors
        /// 
        /// **Projected Outcomes:**
        /// - Projected holder count and growth percentage
        /// - Projected retention rate
        /// - Projected daily transaction volume
        /// - Projected circulating supply
        /// - Projected concentration levels
        /// - Projected liquidity and health scores
        /// - Health score delta from baseline
        /// 
        /// **Outcome Ranges:**
        /// - **Optimistic**: 75th percentile outcome (+20% on positive metrics)
        /// - **Realistic**: 50th percentile outcome (median projection)
        /// - **Pessimistic**: 25th percentile outcome (-20% on positive metrics)
        /// - Confidence interval width indicator
        /// 
        /// **Key Insights:** Automated analysis highlighting significant changes and recommendations
        /// 
        /// **Caveats:** Assumptions and limitations clearly stated
        /// 
        /// **Example Request:**
        /// ```json
        /// POST /api/v1/decision-intelligence/scenarios
        /// {
        ///   "assetId": 1234567,
        ///   "network": "voimain-v1.0",
        ///   "baselineInputs": {
        ///     "currentHolders": 5000,
        ///     "dailyTransactionVolume": 100000,
        ///     "retentionRate": 85,
        ///     "circulatingSupply": 10000000,
        ///     "top10Concentration": 35,
        ///     "historicalGrowthRate": 10
        ///   },
        ///   "adjustments": {
        ///     "holderGrowthRateDelta": 5,
        ///     "retentionRateDelta": 3,
        ///     "transactionVolumeChangePercent": 20,
        ///     "whaleDistributionEvent": true,
        ///     "externalEvents": ["Major exchange listing", "Partnership announcement"]
        ///   },
        ///   "projectionDays": 90
        /// }
        /// ```
        /// 
        /// **Use Cases:**
        /// - Model impact of marketing campaigns on holder growth
        /// - Evaluate effects of liquidity improvements
        /// - Assess concentration risk mitigation strategies
        /// - Plan for major events (listings, partnerships)
        /// - Scenario planning for governance decisions
        /// 
        /// **Note:** Projections are deterministic estimates based on linear models.
        /// Actual outcomes may vary significantly due to market conditions and external factors.
        /// </remarks>
        [HttpPost("scenarios")]
        [ProducesResponseType(typeof(ScenarioEvaluationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EvaluateScenario([FromBody] EvaluateScenarioRequest request)
        {
            try
            {
                _logger.LogInformation("Evaluating scenario: AssetId={AssetId}, Network={Network}, ProjectionDays={Days}",
                    LoggingHelper.SanitizeLogInput(request.AssetId.ToString()),
                    LoggingHelper.SanitizeLogInput(request.Network),
                    request.ProjectionDays);

                var response = await _decisionIntelligenceService.EvaluateScenarioAsync(request);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid request for scenario evaluation: AssetId={AssetId}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(request.AssetId.ToString()),
                    LoggingHelper.SanitizeLogInput(request.Network));

                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INVALID_REQUEST,
                    ErrorMessage = LoggingHelper.SanitizeLogInput(ex.Message)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating scenario: AssetId={AssetId}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(request.AssetId.ToString()),
                    LoggingHelper.SanitizeLogInput(request.Network));

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while evaluating scenario"
                });
            }
        }
    }
}
