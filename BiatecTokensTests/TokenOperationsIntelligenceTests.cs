using BiatecTokensApi.Models.TokenOperationsIntelligence;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for Token Operations Intelligence Service.
    /// Validates policy evaluator logic, recommendation engine, event normalization,
    /// caching, and degraded-mode behavior.
    /// </summary>
    [TestFixture]
    public class TokenOperationsIntelligenceTests
    {
        private TokenOperationsIntelligenceService _service = null!;
        private Mock<ILogger<TokenOperationsIntelligenceService>> _loggerMock = null!;
        private IMemoryCache _cache = null!;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<TokenOperationsIntelligenceService>>();
            _cache = new MemoryCache(new MemoryCacheOptions());

            var metricsLoggerMock = new Mock<ILogger<BiatecTokensApi.Services.MetricsService>>();
            var metricsService = new BiatecTokensApi.Services.MetricsService(metricsLoggerMock.Object);

            _service = new TokenOperationsIntelligenceService(
                _cache,
                metricsService,
                _loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _cache?.Dispose();
        }

        // ============================================================
        // AC3: Policy evaluators produce deterministic results for identical inputs
        // ============================================================

        [Test]
        public async Task EvaluateHealthAsync_WithSameInputs_ReturnsDeterministicResults()
        {
            // Arrange
            ulong assetId = 1234567;
            string network = "voimain-v1.0";

            // Act - call twice with identical inputs
            var result1 = await _service.EvaluateHealthAsync(assetId, network);
            var result2 = await _service.EvaluateHealthAsync(assetId, network);

            // Assert - same inputs produce same outputs
            Assert.That(result1.OverallStatus, Is.EqualTo(result2.OverallStatus));
            Assert.That(result1.HealthScore, Is.EqualTo(result2.HealthScore));
            Assert.That(result1.PolicyResults.Count, Is.EqualTo(result2.PolicyResults.Count));
            for (int i = 0; i < result1.PolicyResults.Count; i++)
            {
                Assert.That(result1.PolicyResults[i].DimensionId, Is.EqualTo(result2.PolicyResults[i].DimensionId));
                Assert.That(result1.PolicyResults[i].Status, Is.EqualTo(result2.PolicyResults[i].Status));
            }
        }

        [Test]
        public async Task EvaluateHealthAsync_WithAllDimensions_ReturnsAllFourPolicyResults()
        {
            // Arrange & Act
            var health = await _service.EvaluateHealthAsync(9999999, "mainnet-v1.0");

            // Assert - all 4 policy dimensions evaluated
            Assert.That(health.PolicyResults.Count, Is.EqualTo(4));
            var dimensionIds = health.PolicyResults.Select(r => r.DimensionId).ToHashSet();
            Assert.That(dimensionIds, Contains.Item("MintAuthority"));
            Assert.That(dimensionIds, Contains.Item("MetadataCompleteness"));
            Assert.That(dimensionIds, Contains.Item("TreasuryMovement"));
            Assert.That(dimensionIds, Contains.Item("OwnershipConsistency"));
        }

        [Test]
        public async Task EvaluateHealthAsync_WithFilteredDimensions_ReturnsOnlyRequestedDimensions()
        {
            // Arrange
            var requestedDimensions = new[] { "MintAuthority", "MetadataCompleteness" };

            // Act
            var health = await _service.EvaluateHealthAsync(1234567, "voimain-v1.0", requestedDimensions);

            // Assert
            Assert.That(health.PolicyResults.Count, Is.EqualTo(2));
            Assert.That(health.PolicyResults.Any(r => r.DimensionId == "MintAuthority"), Is.True);
            Assert.That(health.PolicyResults.Any(r => r.DimensionId == "MetadataCompleteness"), Is.True);
        }

        [Test]
        public async Task EvaluateHealthAsync_PolicyResults_HaveRequiredFields()
        {
            // Act
            var health = await _service.EvaluateHealthAsync(1234567, "voimain-v1.0");

            // Assert - each policy result has required fields
            foreach (var result in health.PolicyResults)
            {
                Assert.That(result.DimensionId, Is.Not.Null.And.Not.Empty,
                    $"DimensionId should not be empty for dimension {result.DimensionId}");
                Assert.That(result.DimensionName, Is.Not.Null.And.Not.Empty,
                    $"DimensionName should not be empty for {result.DimensionId}");
                Assert.That(result.Description, Is.Not.Null.And.Not.Empty,
                    $"Description should not be empty for {result.DimensionId}");
                Assert.That(result.EvaluatedAt, Is.LessThanOrEqualTo(DateTime.UtcNow).And.GreaterThan(DateTime.UtcNow.AddMinutes(-1)),
                    $"EvaluatedAt should be recent for {result.DimensionId}");
            }
        }

        [Test]
        public async Task EvaluateHealthAsync_HealthScore_IsInValidRange()
        {
            // Act
            var health = await _service.EvaluateHealthAsync(1234567, "voimain-v1.0");

            // Assert
            Assert.That(health.HealthScore, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0));
        }

        // ============================================================
        // AC4: Recommendation outputs include reason codes and rationale fields
        // ============================================================

        [Test]
        public async Task GetRecommendationsAsync_ReturnsRecommendationsWithReasonCodes()
        {
            // Act
            var recommendations = await _service.GetRecommendationsAsync(1234567, "voimain-v1.0");

            // Assert
            Assert.That(recommendations, Is.Not.Empty);
            foreach (var rec in recommendations)
            {
                Assert.That(rec.ReasonCode, Is.Not.Null.And.Not.Empty,
                    $"ReasonCode must not be empty for recommendation '{rec.Title}'");
                Assert.That(rec.Rationale, Is.Not.Null.And.Not.Empty,
                    $"Rationale must not be empty for recommendation '{rec.ReasonCode}'");
                Assert.That(rec.SuggestedAction, Is.Not.Null.And.Not.Empty,
                    $"SuggestedAction must not be empty for recommendation '{rec.ReasonCode}'");
                Assert.That(rec.Title, Is.Not.Null.And.Not.Empty,
                    $"Title must not be empty for recommendation '{rec.ReasonCode}'");
            }
        }

        [Test]
        public async Task GetRecommendationsAsync_ReturnsExpectedReasonCodes()
        {
            // Act
            var recommendations = await _service.GetRecommendationsAsync(1234567, "voimain-v1.0");

            // Assert - check expected reason codes are present
            var reasonCodes = recommendations.Select(r => r.ReasonCode).ToHashSet();
            Assert.That(reasonCodes, Contains.Item("MINT_AUTHORITY_UNREVOKED"));
            Assert.That(reasonCodes, Contains.Item("METADATA_INCOMPLETE"));
            Assert.That(reasonCodes, Contains.Item("OWNERSHIP_UNVERIFIED"));
            Assert.That(reasonCodes, Contains.Item("TREASURY_UNMONITORED"));
        }

        [Test]
        public async Task GetRecommendationsAsync_AreOrderedByPriorityDescending()
        {
            // Act
            var recommendations = await _service.GetRecommendationsAsync(1234567, "voimain-v1.0");

            // Assert - highest priority first
            for (int i = 1; i < recommendations.Count; i++)
            {
                Assert.That(recommendations[i].Priority, Is.LessThanOrEqualTo(recommendations[i - 1].Priority),
                    "Recommendations should be ordered by priority descending");
            }
        }

        [Test]
        public async Task GetRecommendationsAsync_WithSameInputs_ProducesDeterministicOrdering()
        {
            // Act - call twice
            var recs1 = await _service.GetRecommendationsAsync(1234567, "voimain-v1.0");
            var recs2 = await _service.GetRecommendationsAsync(1234567, "voimain-v1.0");

            // Assert - same order
            Assert.That(recs1.Count, Is.EqualTo(recs2.Count));
            for (int i = 0; i < recs1.Count; i++)
            {
                Assert.That(recs1[i].ReasonCode, Is.EqualTo(recs2[i].ReasonCode));
                Assert.That(recs1[i].Priority, Is.EqualTo(recs2[i].Priority));
            }
        }

        // ============================================================
        // AC5: Event summaries include actor, timestamp, category, impact
        // ============================================================

        [Test]
        public async Task GetNormalizedEventsAsync_ReturnsEventsWithRequiredFields()
        {
            // Act
            var events = await _service.GetNormalizedEventsAsync(1234567, "voimain-v1.0");

            // Assert
            Assert.That(events, Is.Not.Null);
            foreach (var evt in events)
            {
                Assert.That(evt.EventId, Is.Not.Null.And.Not.Empty, "EventId must not be empty");
                Assert.That(evt.Actor, Is.Not.Null.And.Not.Empty, "Actor must not be empty");
                Assert.That(evt.Description, Is.Not.Null.And.Not.Empty, "Description must not be empty");
                Assert.That(evt.OccurredAt, Is.Not.EqualTo(default(DateTime)), "OccurredAt must be set");
                Assert.That(Enum.IsDefined(typeof(TokenEventCategory), evt.Category), Is.True, "Category must be a valid enum value");
                Assert.That(Enum.IsDefined(typeof(EventImpact), evt.Impact), Is.True, "Impact must be a valid enum value");
            }
        }

        [Test]
        public async Task GetNormalizedEventsAsync_RespectsMaxEventsLimit()
        {
            // Act
            var events = await _service.GetNormalizedEventsAsync(1234567, "voimain-v1.0", maxEvents: 1);

            // Assert
            Assert.That(events.Count, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public async Task GetNormalizedEventsAsync_WithDetails_IncludesDetailsField()
        {
            // Act
            var events = await _service.GetNormalizedEventsAsync(1234567, "voimain-v1.0", includeDetails: true);

            // Assert - deployment event should have details when requested
            var deployEvent = events.FirstOrDefault(e => e.Category == TokenEventCategory.Deployment);
            Assert.That(deployEvent, Is.Not.Null, "Should have at least one deployment event");
            Assert.That(deployEvent!.Details, Is.Not.Null, "Details should be populated when includeDetails=true");
        }

        [Test]
        public async Task GetNormalizedEventsAsync_WithoutDetails_DetailsAreNull()
        {
            // Act
            var events = await _service.GetNormalizedEventsAsync(1234567, "voimain-v1.0", includeDetails: false);

            // Assert
            foreach (var evt in events)
            {
                Assert.That(evt.Details, Is.Null, "Details should be null when includeDetails=false");
            }
        }

        // ============================================================
        // AC1/AC2: Consolidated endpoint returns health, recommendations, and events
        // ============================================================

        [Test]
        public async Task GetOperationsIntelligenceAsync_ReturnsConsolidatedResponse()
        {
            // Arrange
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0",
                MaxEvents = 5
            };

            // Act
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // Assert - consolidated response has all three components
            Assert.That(response.Success, Is.True);
            Assert.That(response.Health, Is.Not.Null);
            Assert.That(response.Recommendations, Is.Not.Null);
            Assert.That(response.Events, Is.Not.Null);
            Assert.That(response.AssetId, Is.EqualTo(request.AssetId));
            Assert.That(response.Network, Is.EqualTo(request.Network));
        }

        [Test]
        public async Task GetOperationsIntelligenceAsync_ReturnsContractVersionMetadata()
        {
            // Arrange
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 9999,
                Network = "mainnet-v1.0"
            };

            // Act
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // Assert - contract version metadata is present (AC5 from issue)
            Assert.That(response.ContractVersion, Is.Not.Null);
            Assert.That(response.ContractVersion.ApiVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(response.ContractVersion.SchemaVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(response.ContractVersion.BackwardCompatible, Is.True);
            Assert.That(response.ContractVersion.GeneratedAt, Is.LessThanOrEqualTo(DateTime.UtcNow));
        }

        [Test]
        public async Task GetOperationsIntelligenceAsync_SetsCorrelationId()
        {
            // Arrange
            var correlationId = "test-correlation-123";
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0",
                CorrelationId = correlationId
            };

            // Act
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // Assert
            Assert.That(response.CorrelationId, Is.EqualTo(correlationId));
        }

        [Test]
        public async Task GetOperationsIntelligenceAsync_AutoGeneratesCorrelationId_WhenNotProvided()
        {
            // Arrange
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0",
                CorrelationId = null
            };

            // Act
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // Assert
            Assert.That(response.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        // ============================================================
        // AC6: Partial upstream failures produce degraded-state indicators
        // ============================================================

        [Test]
        public async Task GetOperationsIntelligenceAsync_NormalCase_IsDegradedIsFalse()
        {
            // Arrange
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0"
            };

            // Act
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // Assert - no degraded state under normal operation
            Assert.That(response.IsDegraded, Is.False);
            Assert.That(response.DegradedSources, Is.Empty);
        }

        // ============================================================
        // AC8/AC9: Caching behavior
        // ============================================================

        [Test]
        public async Task GetOperationsIntelligenceAsync_SecondCall_UsesCache()
        {
            // Arrange
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 8888888,
                Network = "aramidmain-v1.0"
            };

            // Act - first call populates cache
            var response1 = await _service.GetOperationsIntelligenceAsync(request);
            // Second call should use cache
            var response2 = await _service.GetOperationsIntelligenceAsync(request);

            // Assert - second call returns cached health
            Assert.That(response1.Success, Is.True);
            Assert.That(response2.Success, Is.True);
            Assert.That(response2.HealthFromCache, Is.True, "Second call should indicate health was served from cache");
        }

        // ============================================================
        // Enum completeness validation
        // ============================================================

        [Test]
        public void PolicyStatusEnum_HasExpectedValues()
        {
            Assert.That(Enum.IsDefined(typeof(PolicyStatus), "Pass"), Is.True);
            Assert.That(Enum.IsDefined(typeof(PolicyStatus), "Warning"), Is.True);
            Assert.That(Enum.IsDefined(typeof(PolicyStatus), "Fail"), Is.True);
            Assert.That(Enum.IsDefined(typeof(PolicyStatus), "Degraded"), Is.True);
        }

        [Test]
        public void TokenEventCategoryEnum_HasExpectedValues()
        {
            Assert.That(Enum.IsDefined(typeof(TokenEventCategory), "Mint"), Is.True);
            Assert.That(Enum.IsDefined(typeof(TokenEventCategory), "Burn"), Is.True);
            Assert.That(Enum.IsDefined(typeof(TokenEventCategory), "Deployment"), Is.True);
            Assert.That(Enum.IsDefined(typeof(TokenEventCategory), "OwnershipChange"), Is.True);
            Assert.That(Enum.IsDefined(typeof(TokenEventCategory), "TreasuryMovement"), Is.True);
        }

        [Test]
        public void EventImpactEnum_HasExpectedValues()
        {
            Assert.That(Enum.IsDefined(typeof(EventImpact), "Informational"), Is.True);
            Assert.That(Enum.IsDefined(typeof(EventImpact), "Low"), Is.True);
            Assert.That(Enum.IsDefined(typeof(EventImpact), "Moderate"), Is.True);
            Assert.That(Enum.IsDefined(typeof(EventImpact), "High"), Is.True);
            Assert.That(Enum.IsDefined(typeof(EventImpact), "Critical"), Is.True);
        }

        [Test]
        public void TokenHealthStatusEnum_HasExpectedValues()
        {
            Assert.That(Enum.IsDefined(typeof(TokenHealthStatus), "Healthy"), Is.True);
            Assert.That(Enum.IsDefined(typeof(TokenHealthStatus), "Degraded"), Is.True);
            Assert.That(Enum.IsDefined(typeof(TokenHealthStatus), "Unhealthy"), Is.True);
            Assert.That(Enum.IsDefined(typeof(TokenHealthStatus), "Unknown"), Is.True);
        }
    }
}
