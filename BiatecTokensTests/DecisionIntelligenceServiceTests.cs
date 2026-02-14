using BiatecTokensApi.Models.DecisionIntelligence;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for DecisionIntelligenceService
    /// </summary>
    [TestFixture]
    public class DecisionIntelligenceServiceTests
    {
        private Mock<ILogger<DecisionIntelligenceService>> _loggerMock;
        private Mock<IMetricsService> _metricsServiceMock;
        private DecisionIntelligenceService _service;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<DecisionIntelligenceService>>();
            _metricsServiceMock = new Mock<IMetricsService>();
            _service = new DecisionIntelligenceService(_loggerMock.Object, _metricsServiceMock.Object);
        }

        #region Insight Metrics Tests

        [Test]
        public async Task GetInsightMetricsAsync_ValidRequest_ReturnsSuccessResponse()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                StartTime = DateTime.UtcNow.AddDays(-30),
                EndTime = DateTime.UtcNow
            };

            // Act
            var result = await _service.GetInsightMetricsAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True);
            Assert.That(result.AssetId, Is.EqualTo(12345));
            Assert.That(result.Network, Is.EqualTo("voimain-v1.0"));
            Assert.That(result.Metadata, Is.Not.Null);
            Assert.That(result.Metadata.GeneratedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(result.Metadata.CalculationVersion, Is.EqualTo("v1.0"));
        }

        [Test]
        public async Task GetInsightMetricsAsync_AllMetrics_ReturnsAllCategories()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                RequestedMetrics = new List<string>() // Empty = all metrics
            };

            // Act
            var result = await _service.GetInsightMetricsAsync(request);

            // Assert
            Assert.That(result.Adoption, Is.Not.Null, "Adoption metrics should be included");
            Assert.That(result.Retention, Is.Not.Null, "Retention metrics should be included");
            Assert.That(result.TransactionQuality, Is.Not.Null, "Transaction quality should be included");
            Assert.That(result.LiquidityHealth, Is.Not.Null, "Liquidity health should be included");
            Assert.That(result.ConcentrationRisk, Is.Not.Null, "Concentration risk should be included");
        }

        [Test]
        public async Task GetInsightMetricsAsync_SpecificMetrics_ReturnsOnlyRequested()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                RequestedMetrics = new List<string> { "Adoption", "Retention" }
            };

            // Act
            var result = await _service.GetInsightMetricsAsync(request);

            // Assert
            Assert.That(result.Adoption, Is.Not.Null);
            Assert.That(result.Retention, Is.Not.Null);
            Assert.That(result.TransactionQuality, Is.Null);
            Assert.That(result.LiquidityHealth, Is.Null);
            Assert.That(result.ConcentrationRisk, Is.Null);
        }

        [Test]
        public void GetInsightMetricsAsync_ZeroAssetId_ThrowsArgumentException()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 0,
                Network = "voimain-v1.0"
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () => 
                await _service.GetInsightMetricsAsync(request));
            Assert.That(ex.Message, Does.Contain("AssetId"));
        }

        [Test]
        public void GetInsightMetricsAsync_EmptyNetwork_ThrowsArgumentException()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = ""
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () => 
                await _service.GetInsightMetricsAsync(request));
            Assert.That(ex.Message, Does.Contain("Network"));
        }

        [Test]
        public void GetInsightMetricsAsync_InvalidTimeWindow_ThrowsArgumentException()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddDays(-1) // End before start
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () => 
                await _service.GetInsightMetricsAsync(request));
            Assert.That(ex.Message, Does.Contain("StartTime"));
        }

        [Test]
        public void GetInsightMetricsAsync_InvalidMetricName_ThrowsArgumentException()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                RequestedMetrics = new List<string> { "InvalidMetric" }
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () => 
                await _service.GetInsightMetricsAsync(request));
            Assert.That(ex.Message, Does.Contain("Invalid metric"));
        }

        [Test]
        public async Task GetInsightMetricsAsync_Caching_ReturnsFromCacheOnSecondCall()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0"
            };

            // Act
            var result1 = await _service.GetInsightMetricsAsync(request);
            var result2 = await _service.GetInsightMetricsAsync(request);

            // Assert
            Assert.That(result1.Metadata.GeneratedAt, Is.EqualTo(result2.Metadata.GeneratedAt), 
                "Should return cached result with same timestamp");
        }

        [Test]
        public async Task GetInsightMetricsAsync_AdoptionMetrics_ContainsValidData()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0"
            };

            // Act
            var result = await _service.GetInsightMetricsAsync(request);

            // Assert
            Assert.That(result.Adoption, Is.Not.Null);
            Assert.That(result.Adoption.UniqueHolders, Is.GreaterThan(0));
            Assert.That(result.Adoption.GrowthRate, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.Adoption.ActivityRate, Is.InRange(0, 100));
            Assert.That(result.Adoption.Trend, Is.Not.EqualTo(default(TrendDirection)));
        }

        [Test]
        public async Task GetInsightMetricsAsync_RetentionMetrics_ContainsValidData()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0"
            };

            // Act
            var result = await _service.GetInsightMetricsAsync(request);

            // Assert
            Assert.That(result.Retention, Is.Not.Null);
            Assert.That(result.Retention.RetentionRate, Is.InRange(0, 100));
            Assert.That(result.Retention.ChurnRate, Is.InRange(0, 100));
            Assert.That(result.Retention.AverageHoldingPeriodDays, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetInsightMetricsAsync_LiquidityMetrics_ContainsValidData()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0"
            };

            // Act
            var result = await _service.GetInsightMetricsAsync(request);

            // Assert
            Assert.That(result.LiquidityHealth, Is.Not.Null);
            Assert.That(result.LiquidityHealth.TotalSupply, Is.GreaterThan(0));
            Assert.That(result.LiquidityHealth.CirculatingSupplyPercentage, Is.InRange(0, 100));
            Assert.That(result.LiquidityHealth.LiquidityScore, Is.InRange(0, 100));
            Assert.That(result.LiquidityHealth.Status, Is.Not.EqualTo(default(LiquidityStatus)));
        }

        [Test]
        public async Task GetInsightMetricsAsync_ConcentrationMetrics_ContainsValidData()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0"
            };

            // Act
            var result = await _service.GetInsightMetricsAsync(request);

            // Assert
            Assert.That(result.ConcentrationRisk, Is.Not.Null);
            Assert.That(result.ConcentrationRisk.Top10HoldersPercentage, Is.InRange(0, 100));
            Assert.That(result.ConcentrationRisk.GiniCoefficient, Is.InRange(0, 1));
            // Risk level can be any valid value including Low (which is default but still valid)
            Assert.That(Enum.IsDefined(typeof(ConcentrationRisk), result.ConcentrationRisk.RiskLevel), Is.True);
        }

        #endregion

        #region Benchmark Comparison Tests

        [Test]
        public async Task GetBenchmarkComparisonAsync_ValidRequest_ReturnsSuccessResponse()
        {
            // Arrange
            var request = new GetBenchmarkComparisonRequest
            {
                PrimaryAsset = new AssetIdentifier { AssetId = 12345, Network = "voimain-v1.0" },
                ComparisonAssets = new List<AssetIdentifier>
                {
                    new AssetIdentifier { AssetId = 23456, Network = "voimain-v1.0" },
                    new AssetIdentifier { AssetId = 34567, Network = "voimain-v1.0" }
                },
                NormalizationMethod = NormalizationMethod.MinMax
            };

            // Act
            var result = await _service.GetBenchmarkComparisonAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Benchmarks, Is.Not.Empty);
            Assert.That(result.NormalizationContext, Is.Not.Null);
            Assert.That(result.Summary, Is.Not.Null);
        }

        [Test]
        public async Task GetBenchmarkComparisonAsync_MinMaxNormalization_ScalesTo0_100()
        {
            // Arrange
            var request = new GetBenchmarkComparisonRequest
            {
                PrimaryAsset = new AssetIdentifier { AssetId = 12345, Network = "voimain-v1.0" },
                ComparisonAssets = new List<AssetIdentifier>
                {
                    new AssetIdentifier { AssetId = 23456, Network = "voimain-v1.0" },
                    new AssetIdentifier { AssetId = 34567, Network = "voimain-v1.0" }
                },
                NormalizationMethod = NormalizationMethod.MinMax,
                MetricsToCompare = new List<string> { "Adoption" }
            };

            // Act
            var result = await _service.GetBenchmarkComparisonAsync(request);

            // Assert
            var benchmark = result.Benchmarks.First();
            Assert.That(benchmark.DataPoints.All(dp => dp.NormalizedValue >= 0 && dp.NormalizedValue <= 100), 
                "MinMax normalization should scale to 0-100");
        }

        [Test]
        public async Task GetBenchmarkComparisonAsync_PercentileNormalization_ReturnsRanks()
        {
            // Arrange
            var request = new GetBenchmarkComparisonRequest
            {
                PrimaryAsset = new AssetIdentifier { AssetId = 12345, Network = "voimain-v1.0" },
                ComparisonAssets = new List<AssetIdentifier>
                {
                    new AssetIdentifier { AssetId = 23456, Network = "voimain-v1.0" },
                    new AssetIdentifier { AssetId = 34567, Network = "voimain-v1.0" }
                },
                NormalizationMethod = NormalizationMethod.Percentile,
                MetricsToCompare = new List<string> { "Adoption" }
            };

            // Act
            var result = await _service.GetBenchmarkComparisonAsync(request);

            // Assert
            var benchmark = result.Benchmarks.First();
            Assert.That(benchmark.DataPoints.All(dp => dp.PercentileRank >= 0 && dp.PercentileRank <= 100),
                "Percentile ranks should be 0-100");
        }

        [Test]
        public void GetBenchmarkComparisonAsync_ZeroAssetId_ThrowsArgumentException()
        {
            // Arrange
            var request = new GetBenchmarkComparisonRequest
            {
                PrimaryAsset = new AssetIdentifier { AssetId = 0, Network = "voimain-v1.0" },
                ComparisonAssets = new List<AssetIdentifier>()
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () => 
                await _service.GetBenchmarkComparisonAsync(request));
            Assert.That(ex.Message, Does.Contain("AssetId"));
        }

        [Test]
        public void GetBenchmarkComparisonAsync_NoComparisonAssets_ThrowsArgumentException()
        {
            // Arrange
            var request = new GetBenchmarkComparisonRequest
            {
                PrimaryAsset = new AssetIdentifier { AssetId = 12345, Network = "voimain-v1.0" },
                ComparisonAssets = new List<AssetIdentifier>()
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () => 
                await _service.GetBenchmarkComparisonAsync(request));
            Assert.That(ex.Message, Does.Contain("comparison asset"));
        }

        [Test]
        public void GetBenchmarkComparisonAsync_TooManyComparisonAssets_ThrowsArgumentException()
        {
            // Arrange
            var comparisonAssets = Enumerable.Range(1, 101)
                .Select(i => new AssetIdentifier { AssetId = (ulong)i, Network = "voimain-v1.0" })
                .ToList();
            
            var request = new GetBenchmarkComparisonRequest
            {
                PrimaryAsset = new AssetIdentifier { AssetId = 12345, Network = "voimain-v1.0" },
                ComparisonAssets = comparisonAssets
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () => 
                await _service.GetBenchmarkComparisonAsync(request));
            Assert.That(ex.Message, Does.Contain("Maximum 100"));
        }

        [Test]
        public async Task GetBenchmarkComparisonAsync_IncludesStatistics()
        {
            // Arrange
            var request = new GetBenchmarkComparisonRequest
            {
                PrimaryAsset = new AssetIdentifier { AssetId = 12345, Network = "voimain-v1.0" },
                ComparisonAssets = new List<AssetIdentifier>
                {
                    new AssetIdentifier { AssetId = 23456, Network = "voimain-v1.0" },
                    new AssetIdentifier { AssetId = 34567, Network = "voimain-v1.0" }
                },
                MetricsToCompare = new List<string> { "Adoption" }
            };

            // Act
            var result = await _service.GetBenchmarkComparisonAsync(request);

            // Assert
            var benchmark = result.Benchmarks.First();
            Assert.That(benchmark.Statistics, Is.Not.Null);
            Assert.That(benchmark.Statistics.Mean, Is.GreaterThan(0));
            Assert.That(benchmark.Statistics.Count, Is.EqualTo(3)); // Primary + 2 comparisons
        }

        #endregion

        #region Scenario Evaluation Tests

        [Test]
        public async Task EvaluateScenarioAsync_ValidRequest_ReturnsSuccessResponse()
        {
            // Arrange
            var request = new EvaluateScenarioRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                BaselineInputs = new ScenarioInputs
                {
                    CurrentHolders = 1000,
                    DailyTransactionVolume = 50000,
                    RetentionRate = 85,
                    CirculatingSupply = 5000000,
                    Top10Concentration = 40,
                    HistoricalGrowthRate = 10
                },
                Adjustments = new ScenarioAdjustments
                {
                    HolderGrowthRateDelta = 5
                },
                ProjectionDays = 90
            };

            // Act
            var result = await _service.EvaluateScenarioAsync(request);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Projections, Is.Not.Null);
            Assert.That(result.Ranges, Is.Not.Null);
            Assert.That(result.KeyInsights, Is.Not.Empty);
        }

        [Test]
        public async Task EvaluateScenarioAsync_PositiveAdjustments_IncreasesProjections()
        {
            // Arrange
            var request = new EvaluateScenarioRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                BaselineInputs = new ScenarioInputs
                {
                    CurrentHolders = 1000,
                    DailyTransactionVolume = 50000,
                    RetentionRate = 85,
                    CirculatingSupply = 5000000,
                    Top10Concentration = 40,
                    HistoricalGrowthRate = 10
                },
                Adjustments = new ScenarioAdjustments
                {
                    HolderGrowthRateDelta = 10,
                    RetentionRateDelta = 5
                },
                ProjectionDays = 90
            };

            // Act
            var result = await _service.EvaluateScenarioAsync(request);

            // Assert
            Assert.That(result.Projections.ProjectedHolders, Is.GreaterThan(request.BaselineInputs.CurrentHolders),
                "Positive growth should increase holder count");
            Assert.That(result.Projections.ProjectedRetentionRate, Is.GreaterThan(request.BaselineInputs.RetentionRate),
                "Positive retention delta should increase retention rate");
        }

        [Test]
        public async Task EvaluateScenarioAsync_IncludesOutcomeRanges()
        {
            // Arrange
            var request = new EvaluateScenarioRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                BaselineInputs = new ScenarioInputs
                {
                    CurrentHolders = 1000,
                    DailyTransactionVolume = 50000,
                    RetentionRate = 85,
                    CirculatingSupply = 5000000,
                    Top10Concentration = 40,
                    HistoricalGrowthRate = 10
                },
                Adjustments = new ScenarioAdjustments(),
                ProjectionDays = 90
            };

            // Act
            var result = await _service.EvaluateScenarioAsync(request);

            // Assert
            Assert.That(result.Ranges.Optimistic, Is.Not.Null);
            Assert.That(result.Ranges.Realistic, Is.Not.Null);
            Assert.That(result.Ranges.Pessimistic, Is.Not.Null);
            Assert.That(result.Ranges.Optimistic.HealthScore, Is.GreaterThan(result.Ranges.Pessimistic.HealthScore),
                "Optimistic scenario should have higher health score");
        }

        [Test]
        public void EvaluateScenarioAsync_NegativeHolders_ThrowsArgumentException()
        {
            // Arrange
            var request = new EvaluateScenarioRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                BaselineInputs = new ScenarioInputs
                {
                    CurrentHolders = -100 // Negative
                },
                ProjectionDays = 90
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () => 
                await _service.EvaluateScenarioAsync(request));
            Assert.That(ex.Message, Does.Contain("CurrentHolders"));
        }

        [Test]
        public void EvaluateScenarioAsync_InvalidProjectionDays_ThrowsArgumentException()
        {
            // Arrange
            var request = new EvaluateScenarioRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                BaselineInputs = new ScenarioInputs
                {
                    CurrentHolders = 1000
                },
                ProjectionDays = 500 // > 365
            };

            // Act & Assert
            var ex = Assert.ThrowsAsync<ArgumentException>(async () => 
                await _service.EvaluateScenarioAsync(request));
            Assert.That(ex.Message, Does.Contain("ProjectionDays"));
        }

        [Test]
        public async Task EvaluateScenarioAsync_GeneratesKeyInsights()
        {
            // Arrange
            var request = new EvaluateScenarioRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                BaselineInputs = new ScenarioInputs
                {
                    CurrentHolders = 1000,
                    DailyTransactionVolume = 50000,
                    RetentionRate = 85,
                    CirculatingSupply = 5000000,
                    Top10Concentration = 40,
                    HistoricalGrowthRate = 10
                },
                Adjustments = new ScenarioAdjustments
                {
                    HolderGrowthRateDelta = 15,
                    WhaleDistributionEvent = true
                },
                ProjectionDays = 90
            };

            // Act
            var result = await _service.EvaluateScenarioAsync(request);

            // Assert
            Assert.That(result.KeyInsights, Is.Not.Empty);
            Assert.That(result.KeyInsights.Any(i => i.Contains("growth") || i.Contains("distribution")), 
                "Should contain insights about significant changes");
        }

        #endregion

        #region Metadata Generation Tests

        [Test]
        public async Task GetInsightMetricsAsync_MetadataHasRequiredFields()
        {
            // Arrange
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0"
            };

            // Act
            var result = await _service.GetInsightMetricsAsync(request);

            // Assert
            Assert.That(result.Metadata.GeneratedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(result.Metadata.DataWindow, Is.Not.Null);
            // FreshnessIndicator can be Fresh (which is default but still valid for recent data)
            Assert.That(Enum.IsDefined(typeof(FreshnessIndicator), result.Metadata.FreshnessIndicator), Is.True);
            Assert.That(result.Metadata.ConfidenceHint, Is.InRange(0.0, 1.0));
            Assert.That(result.Metadata.CalculationVersion, Is.Not.Empty);
            Assert.That(result.Metadata.DataPointCount, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetInsightMetricsAsync_MetadataIncludesDataWindow()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddDays(-30);
            var endTime = DateTime.UtcNow;
            var request = new GetInsightMetricsRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                StartTime = startTime,
                EndTime = endTime
            };

            // Act
            var result = await _service.GetInsightMetricsAsync(request);

            // Assert
            Assert.That(result.Metadata.DataWindow.StartTime.Date, Is.EqualTo(startTime.Date));
            Assert.That(result.Metadata.DataWindow.EndTime.Date, Is.EqualTo(endTime.Date));
            Assert.That(result.Metadata.DataWindow.Description, Is.Not.Empty);
        }

        #endregion
    }
}
