using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.DecisionIntelligence;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Implementation of decision intelligence service
    /// </summary>
    /// <remarks>
    /// Provides token analytics with data quality metadata and benchmark normalization.
    /// Currently uses simulated data - integrate with actual blockchain data sources for production.
    /// </remarks>
    public class DecisionIntelligenceService : IDecisionIntelligenceService
    {
        private readonly ILogger<DecisionIntelligenceService> _logger;
        private readonly IMetricsService _metricsService;
        
        // Cache for insight metrics (24-hour TTL)
        private readonly ConcurrentDictionary<string, (InsightMetricsResponse Response, DateTime ExpiresAt)> _insightCache = new();
        
        // Cache for benchmark comparisons (1-hour TTL)
        private readonly ConcurrentDictionary<string, (BenchmarkComparisonResponse Response, DateTime ExpiresAt)> _benchmarkCache = new();
        
        private const string CALCULATION_VERSION = "v1.0";
        private const int INSIGHT_CACHE_HOURS = 24;
        private const int BENCHMARK_CACHE_HOURS = 1;
        private const double FLOATING_POINT_EPSILON = 0.001; // Precision threshold for floating-point comparisons

        /// <summary>
        /// Initializes a new instance of the <see cref="DecisionIntelligenceService"/> class.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="metricsService">Metrics service for observability</param>
        public DecisionIntelligenceService(
            ILogger<DecisionIntelligenceService> logger,
            IMetricsService metricsService)
        {
            _logger = logger;
            _metricsService = metricsService;
        }

        /// <inheritdoc/>
        public async Task<InsightMetricsResponse> GetInsightMetricsAsync(GetInsightMetricsRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                // Validate request
                ValidateInsightMetricsRequest(request);

                // Check cache
                var cacheKey = GenerateInsightCacheKey(request);
                if (_insightCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
                {
                    _logger.LogInformation("Cache hit for insight metrics: AssetId={AssetId}, Network={Network}",
                        LoggingHelper.SanitizeLogInput(request.AssetId.ToString()),
                        LoggingHelper.SanitizeLogInput(request.Network));
                    
                    return cached.Response;
                }

                // Calculate metrics
                var response = await CalculateInsightMetricsAsync(request);

                // Cache the response
                _insightCache[cacheKey] = (response, DateTime.UtcNow.AddHours(INSIGHT_CACHE_HOURS));

                // Record metrics
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _metricsService.RecordHistogram("decision_intelligence.insight_metrics.duration_ms", duration);
                _metricsService.IncrementCounter("decision_intelligence.insight_metrics.success");

                _logger.LogInformation("Successfully calculated insight metrics: AssetId={AssetId}, Network={Network}, Duration={Duration}ms",
                    LoggingHelper.SanitizeLogInput(request.AssetId.ToString()),
                    LoggingHelper.SanitizeLogInput(request.Network),
                    duration);

                return response;
            }
            catch (Exception ex)
            {
                _metricsService.IncrementCounter("decision_intelligence.insight_metrics.error");
                _logger.LogError(ex, "Error calculating insight metrics for AssetId={AssetId}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(request.AssetId.ToString()),
                    LoggingHelper.SanitizeLogInput(request.Network));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<BenchmarkComparisonResponse> GetBenchmarkComparisonAsync(GetBenchmarkComparisonRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                // Validate request
                ValidateBenchmarkRequest(request);

                // Check cache
                var cacheKey = GenerateBenchmarkCacheKey(request);
                if (_benchmarkCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
                {
                    _logger.LogInformation("Cache hit for benchmark comparison: PrimaryAsset={AssetId}",
                        LoggingHelper.SanitizeLogInput(request.PrimaryAsset.AssetId.ToString()));
                    
                    return cached.Response;
                }

                // Calculate benchmark comparison
                var response = await CalculateBenchmarkComparisonAsync(request);

                // Cache the response
                _benchmarkCache[cacheKey] = (response, DateTime.UtcNow.AddHours(BENCHMARK_CACHE_HOURS));

                // Record metrics
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _metricsService.RecordHistogram("decision_intelligence.benchmark.duration_ms", duration);
                _metricsService.IncrementCounter("decision_intelligence.benchmark.success");

                _logger.LogInformation("Successfully calculated benchmark comparison: PrimaryAsset={AssetId}, ComparisonCount={Count}, Duration={Duration}ms",
                    LoggingHelper.SanitizeLogInput(request.PrimaryAsset.AssetId.ToString()),
                    request.ComparisonAssets.Count,
                    duration);

                return response;
            }
            catch (Exception ex)
            {
                _metricsService.IncrementCounter("decision_intelligence.benchmark.error");
                _logger.LogError(ex, "Error calculating benchmark comparison for PrimaryAsset={AssetId}",
                    LoggingHelper.SanitizeLogInput(request.PrimaryAsset.AssetId.ToString()));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<ScenarioEvaluationResponse> EvaluateScenarioAsync(EvaluateScenarioRequest request)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                // Validate request
                ValidateScenarioRequest(request);

                // Calculate scenario evaluation
                var response = await CalculateScenarioEvaluationAsync(request);

                // Record metrics
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _metricsService.RecordHistogram("decision_intelligence.scenario.duration_ms", duration);
                _metricsService.IncrementCounter("decision_intelligence.scenario.success");

                _logger.LogInformation("Successfully evaluated scenario: AssetId={AssetId}, Network={Network}, ProjectionDays={Days}, Duration={Duration}ms",
                    LoggingHelper.SanitizeLogInput(request.AssetId.ToString()),
                    LoggingHelper.SanitizeLogInput(request.Network),
                    request.ProjectionDays,
                    duration);

                return response;
            }
            catch (Exception ex)
            {
                _metricsService.IncrementCounter("decision_intelligence.scenario.error");
                _logger.LogError(ex, "Error evaluating scenario for AssetId={AssetId}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(request.AssetId.ToString()),
                    LoggingHelper.SanitizeLogInput(request.Network));
                throw;
            }
        }

        #region Private Helper Methods

        private void ValidateInsightMetricsRequest(GetInsightMetricsRequest request)
        {
            if (request.AssetId == 0)
                throw new ArgumentException("AssetId must be greater than 0", nameof(request.AssetId));

            if (string.IsNullOrWhiteSpace(request.Network))
                throw new ArgumentException("Network is required", nameof(request.Network));

            if (request.StartTime.HasValue && request.EndTime.HasValue && request.StartTime.Value >= request.EndTime.Value)
                throw new ArgumentException("StartTime must be before EndTime");

            // Validate requested metrics
            var validMetrics = new HashSet<string> { "Adoption", "Retention", "TransactionQuality", "LiquidityHealth", "ConcentrationRisk" };
            foreach (var metric in request.RequestedMetrics)
            {
                if (!validMetrics.Contains(metric))
                    throw new ArgumentException($"Invalid metric requested: {metric}");
            }
        }

        private void ValidateBenchmarkRequest(GetBenchmarkComparisonRequest request)
        {
            if (request.PrimaryAsset.AssetId == 0)
                throw new ArgumentException("PrimaryAsset.AssetId must be greater than 0");

            if (string.IsNullOrWhiteSpace(request.PrimaryAsset.Network))
                throw new ArgumentException("PrimaryAsset.Network is required");

            if (request.ComparisonAssets == null || request.ComparisonAssets.Count == 0)
                throw new ArgumentException("At least one comparison asset is required");

            if (request.ComparisonAssets.Count > 100)
                throw new ArgumentException("Maximum 100 comparison assets allowed");

            if (request.StartTime.HasValue && request.EndTime.HasValue && request.StartTime.Value >= request.EndTime.Value)
                throw new ArgumentException("StartTime must be before EndTime");
        }

        private void ValidateScenarioRequest(EvaluateScenarioRequest request)
        {
            if (request.AssetId == 0)
                throw new ArgumentException("AssetId must be greater than 0");

            if (string.IsNullOrWhiteSpace(request.Network))
                throw new ArgumentException("Network is required");

            if (request.ProjectionDays < 1 || request.ProjectionDays > 365)
                throw new ArgumentException("ProjectionDays must be between 1 and 365");

            if (request.BaselineInputs.CurrentHolders < 0)
                throw new ArgumentException("CurrentHolders cannot be negative");

            if (request.BaselineInputs.RetentionRate < 0 || request.BaselineInputs.RetentionRate > 100)
                throw new ArgumentException("RetentionRate must be between 0 and 100");

            if (request.BaselineInputs.Top10Concentration < 0 || request.BaselineInputs.Top10Concentration > 100)
                throw new ArgumentException("Top10Concentration must be between 0 and 100");
        }

        private string GenerateInsightCacheKey(GetInsightMetricsRequest request)
        {
            var start = request.StartTime?.ToString("yyyy-MM-dd") ?? "default";
            var end = request.EndTime?.ToString("yyyy-MM-dd") ?? "default";
            var metrics = request.RequestedMetrics.Count == 0 ? "all" : string.Join(",", request.RequestedMetrics.OrderBy(x => x));
            return $"insight_{request.AssetId}_{request.Network}_{start}_{end}_{metrics}";
        }

        private string GenerateBenchmarkCacheKey(GetBenchmarkComparisonRequest request)
        {
            var start = request.StartTime?.ToString("yyyy-MM-dd") ?? "default";
            var end = request.EndTime?.ToString("yyyy-MM-dd") ?? "default";
            var comparisons = string.Join(",", request.ComparisonAssets.Select(x => $"{x.AssetId}_{x.Network}").OrderBy(x => x));
            var metrics = string.Join(",", request.MetricsToCompare.OrderBy(x => x));
            return $"benchmark_{request.PrimaryAsset.AssetId}_{request.PrimaryAsset.Network}_{start}_{end}_{request.NormalizationMethod}_{metrics}_{comparisons}";
        }

        private async Task<InsightMetricsResponse> CalculateInsightMetricsAsync(GetInsightMetricsRequest request)
        {
            // Determine time window
            var endTime = request.EndTime ?? DateTime.UtcNow;
            var startTime = request.StartTime ?? endTime.AddDays(-30);

            // Calculate which metrics to include
            var includeAll = request.RequestedMetrics.Count == 0;
            var includeAdoption = includeAll || request.RequestedMetrics.Contains("Adoption");
            var includeRetention = includeAll || request.RequestedMetrics.Contains("Retention");
            var includeTxQuality = includeAll || request.RequestedMetrics.Contains("TransactionQuality");
            var includeLiquidity = includeAll || request.RequestedMetrics.Contains("LiquidityHealth");
            var includeConcentration = includeAll || request.RequestedMetrics.Contains("ConcentrationRisk");

            // Create response
            var response = new InsightMetricsResponse
            {
                Success = true,
                AssetId = request.AssetId,
                Network = request.Network
            };

            // Simulate async data retrieval
            await Task.Delay(10);

            // Generate simulated metrics (in production, fetch from blockchain/database)
            if (includeAdoption)
                response.Adoption = GenerateAdoptionMetrics(request.AssetId, startTime, endTime);

            if (includeRetention)
                response.Retention = GenerateRetentionMetrics(request.AssetId, startTime, endTime);

            if (includeTxQuality)
                response.TransactionQuality = GenerateTransactionQualityMetrics(request.AssetId, startTime, endTime);

            if (includeLiquidity)
                response.LiquidityHealth = GenerateLiquidityHealthMetrics(request.AssetId, startTime, endTime);

            if (includeConcentration)
                response.ConcentrationRisk = GenerateConcentrationRiskMetrics(request.AssetId, startTime, endTime);

            // Generate metadata
            response.Metadata = GenerateMetadata(startTime, endTime, "InsightMetrics");

            return response;
        }

        private AdoptionMetrics GenerateAdoptionMetrics(ulong assetId, DateTime startTime, DateTime endTime)
        {
            // Simulated data - in production, query blockchain/database
            var random = new Random((int)(assetId % int.MaxValue));
            var durationDays = (endTime - startTime).TotalDays;
            
            var uniqueHolders = random.Next(100, 10000);
            var newHolders = (int)(uniqueHolders * random.Next(5, 20) / 100.0);
            var activeAddresses = (int)(uniqueHolders * random.Next(10, 50) / 100.0);

            return new AdoptionMetrics
            {
                UniqueHolders = uniqueHolders,
                NewHolders = newHolders,
                GrowthRate = (newHolders / (double)uniqueHolders) * 100,
                AverageNewHoldersPerDay = newHolders / durationDays,
                ActiveAddresses = activeAddresses,
                ActivityRate = (activeAddresses / (double)uniqueHolders) * 100,
                Trend = DetermineTrend(newHolders, uniqueHolders / 10)
            };
        }

        private RetentionMetrics GenerateRetentionMetrics(ulong assetId, DateTime startTime, DateTime endTime)
        {
            var random = new Random((int)((assetId + 1) % int.MaxValue));
            var currentHolders = random.Next(100, 10000);
            var initialHolders = currentHolders + random.Next(-50, 100);
            var lostHolders = random.Next(0, initialHolders / 10);

            var retentionRate = ((initialHolders - lostHolders) / (double)initialHolders) * 100;
            var churnRate = (lostHolders / (double)initialHolders) * 100;

            return new RetentionMetrics
            {
                InitialHolders = initialHolders,
                CurrentHolders = currentHolders,
                LostHolders = lostHolders,
                RetentionRate = retentionRate,
                ChurnRate = churnRate,
                AverageHoldingPeriodDays = random.Next(30, 365),
                MedianHoldingPeriodDays = random.Next(15, 180),
                Trend = DetermineTrend((int)retentionRate, 75)
            };
        }

        private TransactionQualityMetrics GenerateTransactionQualityMetrics(ulong assetId, DateTime startTime, DateTime endTime)
        {
            var random = new Random((int)assetId + 2);
            var durationDays = (endTime - startTime).TotalDays;
            
            var totalTx = random.Next(1000, 100000);
            var successfulTx = (int)(totalTx * random.Next(95, 100) / 100.0);
            var failedTx = totalTx - successfulTx;

            return new TransactionQualityMetrics
            {
                TotalTransactions = totalTx,
                SuccessfulTransactions = successfulTx,
                FailedTransactions = failedTx,
                SuccessRate = (successfulTx / (double)totalTx) * 100,
                AverageTransactionValue = random.Next(10, 1000) + random.NextDouble(),
                MedianTransactionValue = random.Next(5, 500) + random.NextDouble(),
                TotalVolume = totalTx * (random.Next(10, 1000) + random.NextDouble()),
                AverageTransactionsPerDay = totalTx / durationDays,
                Trend = TrendDirection.Stable
            };
        }

        private LiquidityHealthMetrics GenerateLiquidityHealthMetrics(ulong assetId, DateTime startTime, DateTime endTime)
        {
            var random = new Random((int)assetId + 3);
            
            var totalSupply = random.Next(1000000, 100000000);
            var circulatingSupply = totalSupply * random.Next(60, 95) / 100.0;
            var lockedSupply = totalSupply - circulatingSupply;
            var tradingVolume = circulatingSupply * random.Next(1, 20) / 100.0;

            var liquidityScore = CalculateLiquidityScore(circulatingSupply / totalSupply, tradingVolume / circulatingSupply);

            return new LiquidityHealthMetrics
            {
                TotalSupply = totalSupply,
                CirculatingSupply = circulatingSupply,
                LockedSupply = lockedSupply,
                CirculatingSupplyPercentage = (circulatingSupply / totalSupply) * 100,
                TradingVolume = tradingVolume,
                VolumeToCirculatingRatio = tradingVolume / circulatingSupply,
                LiquidityScore = liquidityScore,
                Status = DetermineLiquidityStatus(liquidityScore)
            };
        }

        private ConcentrationRiskMetrics GenerateConcentrationRiskMetrics(ulong assetId, DateTime startTime, DateTime endTime)
        {
            var random = new Random((int)assetId + 4);
            
            var top10Pct = random.Next(20, 70);
            var top50Pct = top10Pct + random.Next(10, 20);
            var top100Pct = top50Pct + random.Next(5, 15);

            var gini = top10Pct / 100.0 * 0.8; // Simplified Gini coefficient
            var hhi = CalculateHHI(top10Pct / 100.0);
            var whaleCount = random.Next(1, 15);

            return new ConcentrationRiskMetrics
            {
                Top10HoldersPercentage = top10Pct,
                Top50HoldersPercentage = top50Pct,
                Top100HoldersPercentage = top100Pct,
                GiniCoefficient = gini,
                HerfindahlIndex = hhi,
                WhaleCount = whaleCount,
                RiskLevel = DetermineConcentrationRisk(top10Pct, hhi),
                Trend = DetermineTrend(50 - top10Pct, 20) // Lower concentration is better
            };
        }

        private async Task<BenchmarkComparisonResponse> CalculateBenchmarkComparisonAsync(GetBenchmarkComparisonRequest request)
        {
            // Determine time window
            var endTime = request.EndTime ?? DateTime.UtcNow;
            var startTime = request.StartTime ?? endTime.AddDays(-30);

            var response = new BenchmarkComparisonResponse
            {
                Success = true,
                PrimaryAsset = request.PrimaryAsset
            };

            // Simulate async data retrieval
            await Task.Delay(10);

            // Get all assets to compare (primary + comparisons)
            var allAssets = new List<AssetIdentifier> { request.PrimaryAsset };
            allAssets.AddRange(request.ComparisonAssets);

            // Determine metrics to compare
            var metricsToCompare = request.MetricsToCompare.Count > 0 
                ? request.MetricsToCompare 
                : new List<string> { "Adoption", "Retention", "TransactionQuality", "LiquidityHealth", "ConcentrationRisk" };

            // Calculate benchmarks for each metric
            foreach (var metricName in metricsToCompare)
            {
                var benchmark = CalculateMetricBenchmark(metricName, allAssets, request.PrimaryAsset, request.NormalizationMethod);
                response.Benchmarks.Add(benchmark);
            }

            // Generate normalization context
            response.NormalizationContext = new NormalizationContext
            {
                Method = request.NormalizationMethod,
                AlignedWindow = new DataWindow
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    Description = $"Last {(int)(endTime - startTime).TotalDays} days"
                },
                AssetCount = allAssets.Count,
                AlignmentCaveats = GenerateAlignmentCaveats(allAssets)
            };

            // Generate summary
            response.Summary = GenerateBenchmarkSummary(response.Benchmarks, request.PrimaryAsset);

            // Generate metadata
            response.Metadata = GenerateMetadata(startTime, endTime, "BenchmarkComparison");

            return response;
        }

        private MetricBenchmark CalculateMetricBenchmark(
            string metricName,
            List<AssetIdentifier> assets,
            AssetIdentifier primaryAsset,
            NormalizationMethod method)
        {
            var benchmark = new MetricBenchmark
            {
                MetricName = metricName,
                DataPoints = new List<BenchmarkDataPoint>()
            };

            // Generate simulated values for each asset
            var values = new List<double>();
            foreach (var asset in assets)
            {
                var random = new Random((int)(asset.AssetId + (ulong)metricName.GetHashCode()));
                var rawValue = GenerateMetricValue(metricName, random);
                values.Add(rawValue);
            }

            // Calculate statistics
            benchmark.Statistics = CalculateStatistics(values);

            // Normalize and create data points
            var normalizedValues = NormalizeValues(values, method);
            for (int i = 0; i < assets.Count; i++)
            {
                var isPrimary = assets[i].AssetId == primaryAsset.AssetId && assets[i].Network == primaryAsset.Network;
                var percentile = CalculatePercentileRank(values[i], values);
                
                var dataPoint = new BenchmarkDataPoint
                {
                    Asset = assets[i],
                    RawValue = values[i],
                    NormalizedValue = normalizedValues[i],
                    PercentileRank = percentile,
                    IsPrimary = isPrimary,
                    Category = DeterminePerformanceCategory(percentile)
                };

                if (!isPrimary)
                {
                    var primaryIdx = assets.FindIndex(a => a.AssetId == primaryAsset.AssetId && a.Network == primaryAsset.Network);
                    dataPoint.DeltaFromPrimary = values[i] - values[primaryIdx];
                }

                benchmark.DataPoints.Add(dataPoint);
            }

            return benchmark;
        }

        private double GenerateMetricValue(string metricName, Random random)
        {
            return metricName switch
            {
                "Adoption" => random.Next(100, 10000),
                "Retention" => random.Next(50, 95) + random.NextDouble(),
                "TransactionQuality" => random.Next(85, 100) + random.NextDouble(),
                "LiquidityHealth" => random.Next(40, 95) + random.NextDouble(),
                "ConcentrationRisk" => random.Next(20, 70) + random.NextDouble(),
                _ => random.NextDouble() * 100
            };
        }

        private BenchmarkStatistics CalculateStatistics(List<double> values)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            
            return new BenchmarkStatistics
            {
                Mean = values.Average(),
                Median = CalculatePercentile(sorted, 0.50),
                StandardDeviation = CalculateStandardDeviation(values),
                Min = values.Min(),
                Max = values.Max(),
                P25 = CalculatePercentile(sorted, 0.25),
                P75 = CalculatePercentile(sorted, 0.75),
                Count = values.Count
            };
        }

        private List<double> NormalizeValues(List<double> values, NormalizationMethod method)
        {
            return method switch
            {
                NormalizationMethod.ZScore => NormalizeZScore(values),
                NormalizationMethod.MinMax => NormalizeMinMax(values),
                NormalizationMethod.Percentile => NormalizePercentile(values),
                _ => values.ToList()
            };
        }

        private List<double> NormalizeZScore(List<double> values)
        {
            var mean = values.Average();
            var stdDev = CalculateStandardDeviation(values);
            
            if (stdDev == 0) return values.Select(_ => 0.0).ToList();
            
            return values.Select(v => (v - mean) / stdDev).ToList();
        }

        private List<double> NormalizeMinMax(List<double> values)
        {
            var min = values.Min();
            var max = values.Max();
            var range = max - min;
            
            if (range == 0) return values.Select(_ => 50.0).ToList();
            
            return values.Select(v => ((v - min) / range) * 100).ToList();
        }

        private List<double> NormalizePercentile(List<double> values)
        {
            return values.Select(v => CalculatePercentileRank(v, values)).ToList();
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            var mean = values.Average();
            var sumSquaredDiffs = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquaredDiffs / values.Count);
        }

        private double CalculatePercentile(double[] sortedValues, double percentile)
        {
            if (sortedValues.Length == 0) return 0;
            if (sortedValues.Length == 1) return sortedValues[0];

            var index = percentile * (sortedValues.Length - 1);
            var lowerIndex = (int)Math.Floor(index);
            var upperIndex = (int)Math.Ceiling(index);
            
            if (lowerIndex == upperIndex) return sortedValues[lowerIndex];
            
            var weight = index - lowerIndex;
            return sortedValues[lowerIndex] * (1 - weight) + sortedValues[upperIndex] * weight;
        }

        private double CalculatePercentileRank(double value, List<double> allValues)
        {
            var countBelow = allValues.Count(v => v < value);
            var countEqual = allValues.Count(v => Math.Abs(v - value) < FLOATING_POINT_EPSILON);
            
            return ((countBelow + (countEqual / 2.0)) / allValues.Count) * 100;
        }

        private PerformanceCategory DeterminePerformanceCategory(double percentile)
        {
            return percentile switch
            {
                >= 75 => PerformanceCategory.TopPerformer,
                >= 50 => PerformanceCategory.AboveAverage,
                >= 25 => PerformanceCategory.Average,
                >= 10 => PerformanceCategory.BelowAverage,
                _ => PerformanceCategory.Underperformer
            };
        }

        private BenchmarkSummary GenerateBenchmarkSummary(List<MetricBenchmark> benchmarks, AssetIdentifier primaryAsset)
        {
            var primaryDataPoints = benchmarks.SelectMany(b => b.DataPoints.Where(dp => dp.IsPrimary)).ToList();
            
            var avgPercentile = primaryDataPoints.Any() ? primaryDataPoints.Average(dp => dp.PercentileRank) : 50.0;
            
            var strengths = benchmarks
                .Where(b => b.DataPoints.Any(dp => dp.IsPrimary && dp.PercentileRank >= 75))
                .Select(b => b.MetricName)
                .ToList();
            
            var weaknesses = benchmarks
                .Where(b => b.DataPoints.Any(dp => dp.IsPrimary && dp.PercentileRank < 25))
                .Select(b => b.MetricName)
                .ToList();

            return new BenchmarkSummary
            {
                OverallRank = 1, // Simplified - would need to calculate across all metrics
                TotalAssetsCompared = benchmarks.FirstOrDefault()?.DataPoints.Count ?? 0,
                AveragePercentile = avgPercentile,
                StrengthMetrics = strengths,
                WeaknessMetrics = weaknesses,
                CompetitivePosition = DetermineCompetitivePosition(avgPercentile)
            };
        }

        private string DetermineCompetitivePosition(double avgPercentile)
        {
            return avgPercentile switch
            {
                >= 75 => "Strong - Outperforming most peers",
                >= 50 => "Competitive - Above average performance",
                >= 25 => "Challenged - Below average performance",
                _ => "Weak - Underperforming relative to peers"
            };
        }

        private List<string> GenerateAlignmentCaveats(List<AssetIdentifier> assets)
        {
            var caveats = new List<string>();
            
            // Check if assets are from different networks
            var distinctNetworks = assets.Select(a => a.Network).Distinct().Count();
            if (distinctNetworks > 1)
            {
                caveats.Add($"Comparison includes assets from {distinctNetworks} different networks. Cross-network comparisons may have inherent differences.");
            }

            return caveats;
        }

        private async Task<ScenarioEvaluationResponse> CalculateScenarioEvaluationAsync(EvaluateScenarioRequest request)
        {
            var response = new ScenarioEvaluationResponse
            {
                Success = true,
                AssetId = request.AssetId,
                Network = request.Network,
                BaselineInputs = request.BaselineInputs,
                AppliedAdjustments = request.Adjustments
            };

            // Simulate async calculation
            await Task.Delay(10);

            // Calculate projections
            response.Projections = CalculateProjections(request);

            // Calculate outcome ranges
            response.Ranges = CalculateOutcomeRanges(request);

            // Generate insights
            response.KeyInsights = GenerateScenarioInsights(request, response.Projections);

            // Generate caveats
            response.Caveats = GenerateScenarioCaveats(request);

            // Generate metadata
            var endTime = DateTime.UtcNow.AddDays(request.ProjectionDays);
            response.Metadata = GenerateMetadata(DateTime.UtcNow, endTime, "ScenarioEvaluation");

            return response;
        }

        private ProjectedOutcomes CalculateProjections(EvaluateScenarioRequest request)
        {
            var baseline = request.BaselineInputs;
            var adjustments = request.Adjustments;
            var projectionDays = request.ProjectionDays;

            // Apply growth rate adjustments
            var effectiveGrowthRate = baseline.HistoricalGrowthRate + (adjustments.HolderGrowthRateDelta ?? 0);
            var growthFactor = 1 + (effectiveGrowthRate / 100.0 * projectionDays / 30.0);
            
            var projectedHolders = (int)(baseline.CurrentHolders * growthFactor);
            var holderGrowthPercent = ((projectedHolders - baseline.CurrentHolders) / (double)baseline.CurrentHolders) * 100;

            // Apply retention rate adjustments
            var projectedRetentionRate = baseline.RetentionRate + (adjustments.RetentionRateDelta ?? 0);
            projectedRetentionRate = Math.Max(0, Math.Min(100, projectedRetentionRate));

            // Apply volume adjustments
            var volumeChangeFactor = 1 + ((adjustments.TransactionVolumeChangePercent ?? 0) / 100.0);
            var projectedDailyVolume = baseline.DailyTransactionVolume * volumeChangeFactor;
            var volumeGrowthPercent = ((projectedDailyVolume - baseline.DailyTransactionVolume) / baseline.DailyTransactionVolume) * 100;

            // Apply supply adjustments
            var projectedCirculating = baseline.CirculatingSupply + (adjustments.SupplyChangeDelta ?? 0);

            // Apply concentration adjustments
            var projectedConcentration = baseline.Top10Concentration;
            if (adjustments.WhaleDistributionEvent == true)
            {
                projectedConcentration *= 0.8; // 20% reduction
            }

            // Calculate composite scores
            var projectedLiquidityScore = CalculateLiquidityScore(
                projectedCirculating / (projectedCirculating + 1000000),
                projectedDailyVolume / projectedCirculating
            );

            var projectedHealthScore = CalculateHealthScore(
                projectedRetentionRate / 100.0,
                projectedLiquidityScore / 100.0,
                1 - (projectedConcentration / 100.0)
            );

            var baselineHealthScore = CalculateHealthScore(
                baseline.RetentionRate / 100.0,
                CalculateLiquidityScore(0.8, 0.1) / 100.0,
                1 - (baseline.Top10Concentration / 100.0)
            );

            return new ProjectedOutcomes
            {
                ProjectedHolders = projectedHolders,
                HolderGrowthPercent = holderGrowthPercent,
                ProjectedRetentionRate = projectedRetentionRate,
                ProjectedDailyVolume = projectedDailyVolume,
                VolumeGrowthPercent = volumeGrowthPercent,
                ProjectedCirculatingSupply = projectedCirculating,
                ProjectedTop10Concentration = projectedConcentration,
                ProjectedLiquidityScore = projectedLiquidityScore,
                ProjectedHealthScore = projectedHealthScore,
                HealthScoreDelta = projectedHealthScore - baselineHealthScore
            };
        }

        private OutcomeRanges CalculateOutcomeRanges(EvaluateScenarioRequest request)
        {
            var realistic = CalculateProjections(request);

            // Optimistic: +20% on positive metrics, -20% on concentration
            var optimistic = new RangeOutcome
            {
                Holders = (int)(realistic.ProjectedHolders * 1.2),
                RetentionRate = Math.Min(100, realistic.ProjectedRetentionRate * 1.1),
                DailyVolume = realistic.ProjectedDailyVolume * 1.3,
                LiquidityScore = Math.Min(100, realistic.ProjectedLiquidityScore * 1.15),
                HealthScore = Math.Min(100, realistic.ProjectedHealthScore * 1.2)
            };

            // Pessimistic: -20% on positive metrics, +20% on concentration
            var pessimistic = new RangeOutcome
            {
                Holders = (int)(realistic.ProjectedHolders * 0.8),
                RetentionRate = realistic.ProjectedRetentionRate * 0.9,
                DailyVolume = realistic.ProjectedDailyVolume * 0.7,
                LiquidityScore = realistic.ProjectedLiquidityScore * 0.85,
                HealthScore = realistic.ProjectedHealthScore * 0.8
            };

            var realisticOutcome = new RangeOutcome
            {
                Holders = realistic.ProjectedHolders,
                RetentionRate = realistic.ProjectedRetentionRate,
                DailyVolume = realistic.ProjectedDailyVolume,
                LiquidityScore = realistic.ProjectedLiquidityScore,
                HealthScore = realistic.ProjectedHealthScore
            };

            return new OutcomeRanges
            {
                Optimistic = optimistic,
                Realistic = realisticOutcome,
                Pessimistic = pessimistic,
                ConfidenceIntervalWidth = (optimistic.HealthScore - pessimistic.HealthScore) / realistic.ProjectedHealthScore
            };
        }

        private List<string> GenerateScenarioInsights(EvaluateScenarioRequest request, ProjectedOutcomes projections)
        {
            var insights = new List<string>();

            if (projections.HolderGrowthPercent > 20)
                insights.Add($"Strong projected holder growth of {projections.HolderGrowthPercent:F1}% indicates expanding user base");
            
            if (projections.ProjectedRetentionRate > 80)
                insights.Add($"High projected retention rate of {projections.ProjectedRetentionRate:F1}% suggests strong user loyalty");
            
            if (projections.HealthScoreDelta > 10)
                insights.Add($"Overall health score projected to improve by {projections.HealthScoreDelta:F1} points");
            else if (projections.HealthScoreDelta < -10)
                insights.Add($"Overall health score projected to decline by {Math.Abs(projections.HealthScoreDelta):F1} points - consider mitigation strategies");

            if (projections.VolumeGrowthPercent > 50)
                insights.Add($"Transaction volume projected to grow {projections.VolumeGrowthPercent:F1}% - indicates increasing utility");

            if (request.Adjustments.WhaleDistributionEvent == true)
                insights.Add("Whale distribution event would reduce concentration risk and improve decentralization");

            return insights;
        }

        private List<string> GenerateScenarioCaveats(EvaluateScenarioRequest request)
        {
            var caveats = new List<string>
            {
                "Projections are based on deterministic modeling and historical trends",
                "Actual outcomes may vary significantly due to market conditions and external events",
                $"Model assumes linear growth over {request.ProjectionDays} day projection period"
            };

            if (request.Adjustments.ExternalEvents != null && request.Adjustments.ExternalEvents.Any())
            {
                caveats.Add($"External events considered: {string.Join(", ", request.Adjustments.ExternalEvents)}");
            }

            return caveats;
        }

        private double CalculateLiquidityScore(double circulatingRatio, double volumeRatio)
        {
            // Composite score: 50% circulating ratio, 50% volume ratio (normalized)
            var circulatingScore = circulatingRatio * 50;
            var volumeScore = Math.Min(volumeRatio * 10, 1.0) * 50;
            return circulatingScore + volumeScore;
        }

        private LiquidityStatus DetermineLiquidityStatus(double score)
        {
            return score switch
            {
                >= 80 => LiquidityStatus.Excellent,
                >= 60 => LiquidityStatus.Good,
                >= 40 => LiquidityStatus.Fair,
                >= 20 => LiquidityStatus.Poor,
                _ => LiquidityStatus.Insufficient
            };
        }

        private double CalculateHHI(double top10Percentage)
        {
            // Simplified HHI calculation assuming even distribution within top 10
            var individualShare = top10Percentage / 10.0;
            return 10 * Math.Pow(individualShare, 2) * 100; // Scale to 0-10000
        }

        private ConcentrationRisk DetermineConcentrationRisk(double top10Pct, double hhi)
        {
            if (top10Pct > 70 || hhi > 2500) return ConcentrationRisk.Extreme;
            if (top10Pct > 50 || hhi > 1500) return ConcentrationRisk.High;
            if (top10Pct > 30 || hhi > 800) return ConcentrationRisk.Moderate;
            return ConcentrationRisk.Low;
        }

        private double CalculateHealthScore(double retention, double liquidity, double decentralization)
        {
            // Weighted composite: 40% retention, 35% liquidity, 25% decentralization
            return (retention * 0.40 + liquidity * 0.35 + decentralization * 0.25) * 100;
        }

        private TrendDirection DetermineTrend(double currentValue, double threshold)
        {
            if (currentValue > threshold * 1.1) return TrendDirection.Improving;
            if (currentValue < threshold * 0.9) return TrendDirection.Declining;
            return TrendDirection.Stable;
        }

        private MetricMetadata GenerateMetadata(DateTime startTime, DateTime endTime, string calculationType)
        {
            var now = DateTime.UtcNow;
            var dataAge = (now - endTime).TotalHours;
            
            var freshness = dataAge switch
            {
                < 1 => FreshnessIndicator.Fresh,
                < 24 => FreshnessIndicator.Delayed,
                _ => FreshnessIndicator.Stale
            };

            var confidence = calculationType == "ScenarioEvaluation" ? 0.75 : 0.95; // Scenarios inherently less certain

            var caveats = new List<string>();
            if (calculationType == "ScenarioEvaluation")
            {
                caveats.Add("Scenario projections are estimates based on deterministic models");
            }
            if (dataAge > 24)
            {
                caveats.Add($"Data is {(int)dataAge} hours old - may not reflect latest conditions");
            }

            return new MetricMetadata
            {
                GeneratedAt = now,
                DataWindow = new DataWindow
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    Description = $"{(int)(endTime - startTime).TotalDays} days: {startTime:yyyy-MM-dd} to {endTime:yyyy-MM-dd}"
                },
                FreshnessIndicator = freshness,
                ConfidenceHint = confidence,
                CalculationVersion = CALCULATION_VERSION,
                Caveats = caveats,
                DataPointCount = 100, // Simulated
                IsDataComplete = true,
                DataCompleteness = 100.0
            };
        }

        #endregion
    }
}
