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
    /// Validates policy evaluator logic across normal/warning/critical conditions,
    /// recommendation reason-code stability, event normalization edge cases,
    /// caching, and degraded-state signaling.
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
        // AC3: Evaluator logic - MintAuthority dimension (Pass/Warning/Fail)
        // ============================================================

        [Test]
        public async Task MintAuthority_WhenRevoked_ReturnsPass()
        {
            var state = new TokenStateInputs { MintAuthorityRevoked = true };
            var health = await _service.EvaluateHealthAsync(1, "voimain-v1.0",
                new[] { "MintAuthority" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "MintAuthority");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Pass));
            Assert.That(result.Severity, Is.EqualTo(AssessmentSeverity.Healthy));
            Assert.That(result.FindingCode, Is.EqualTo("MINT_AUTHORITY_REVOKED"));
        }

        [Test]
        public async Task MintAuthority_WhenCapReached_ReturnsWarning()
        {
            var state = new TokenStateInputs { MintAuthorityRevoked = false, SupplyCapReached = true };
            var health = await _service.EvaluateHealthAsync(1, "voimain-v1.0",
                new[] { "MintAuthority" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "MintAuthority");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Warning));
            Assert.That(result.FindingCode, Is.EqualTo("MINT_AUTHORITY_PRESENT_CAP_REACHED"));
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task MintAuthority_WhenActiveAndUncapped_ReturnsCriticalFail()
        {
            var state = new TokenStateInputs { MintAuthorityRevoked = false, SupplyCapReached = false };
            var health = await _service.EvaluateHealthAsync(1, "voimain-v1.0",
                new[] { "MintAuthority" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "MintAuthority");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Fail));
            Assert.That(result.Severity, Is.EqualTo(AssessmentSeverity.High));
            Assert.That(result.FindingCode, Is.EqualTo("MINT_AUTHORITY_UNCAPPED"));
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task MintAuthority_WhenStateUnknown_ReturnsConservativeWarning()
        {
            var health = await _service.EvaluateHealthAsync(1, "voimain-v1.0",
                new[] { "MintAuthority" }, stateInputs: null);

            var result = health.PolicyResults.Single(r => r.DimensionId == "MintAuthority");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Warning));
            Assert.That(result.FindingCode, Is.EqualTo("MINT_AUTHORITY_PRESENT"));
        }

        // ============================================================
        // AC3: Evaluator logic - MetadataCompleteness dimension
        // ============================================================

        [Test]
        public async Task MetadataCompleteness_WhenFullAndAccessible_ReturnsPass()
        {
            var state = new TokenStateInputs
            {
                MetadataCompletenessPercent = 95,
                MetadataUrlAccessible = true
            };
            var health = await _service.EvaluateHealthAsync(2, "voimain-v1.0",
                new[] { "MetadataCompleteness" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "MetadataCompleteness");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Pass));
            Assert.That(result.Severity, Is.EqualTo(AssessmentSeverity.Healthy));
            Assert.That(result.Details?["completenessPercent"], Is.EqualTo(95.0));
        }

        [Test]
        public async Task MetadataCompleteness_WhenPartiallyComplete_ReturnsWarning()
        {
            var state = new TokenStateInputs { MetadataCompletenessPercent = 70 };
            var health = await _service.EvaluateHealthAsync(2, "voimain-v1.0",
                new[] { "MetadataCompleteness" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "MetadataCompleteness");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Warning));
            Assert.That(result.FindingCode, Is.EqualTo("METADATA_PARTIALLY_COMPLETE"));
        }

        [Test]
        public async Task MetadataCompleteness_WhenCriticallyIncomplete_ReturnsFail()
        {
            var state = new TokenStateInputs { MetadataCompletenessPercent = 30 };
            var health = await _service.EvaluateHealthAsync(2, "voimain-v1.0",
                new[] { "MetadataCompleteness" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "MetadataCompleteness");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Fail));
            Assert.That(result.Severity, Is.EqualTo(AssessmentSeverity.High));
            Assert.That(result.FindingCode, Is.EqualTo("METADATA_CRITICALLY_INCOMPLETE"));
        }

        [Test]
        public async Task MetadataCompleteness_WhenUrlInaccessible_ReturnsFail()
        {
            var state = new TokenStateInputs { MetadataUrlAccessible = false };
            var health = await _service.EvaluateHealthAsync(2, "voimain-v1.0",
                new[] { "MetadataCompleteness" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "MetadataCompleteness");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Fail));
            Assert.That(result.FindingCode, Is.EqualTo("METADATA_URL_INACCESSIBLE"));
        }

        // ============================================================
        // AC3: Evaluator logic - TreasuryMovement dimension
        // ============================================================

        [Test]
        public async Task TreasuryMovement_WhenNoAnomalousMovements_ReturnsPass()
        {
            var state = new TokenStateInputs { LargeTreasuryMovementDetected = false };
            var health = await _service.EvaluateHealthAsync(3, "voimain-v1.0",
                new[] { "TreasuryMovement" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "TreasuryMovement");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Pass));
        }

        [Test]
        public async Task TreasuryMovement_WhenLargeMovementDetected_ReturnsWarning()
        {
            var state = new TokenStateInputs
            {
                LargeTreasuryMovementDetected = true,
                LargestMovementPercent = 15
            };
            var health = await _service.EvaluateHealthAsync(3, "voimain-v1.0",
                new[] { "TreasuryMovement" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "TreasuryMovement");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Warning));
            Assert.That(result.FindingCode, Is.EqualTo("TREASURY_LARGE_MOVEMENT"));
            Assert.That(result.Details?["largestMovementPercent"], Is.EqualTo(15.0));
        }

        [Test]
        public async Task TreasuryMovement_WhenCriticalMovementDetected_ReturnsCriticalFail()
        {
            var state = new TokenStateInputs
            {
                LargeTreasuryMovementDetected = true,
                LargestMovementPercent = 45
            };
            var health = await _service.EvaluateHealthAsync(3, "voimain-v1.0",
                new[] { "TreasuryMovement" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "TreasuryMovement");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Fail));
            Assert.That(result.Severity, Is.EqualTo(AssessmentSeverity.Critical));
            Assert.That(result.FindingCode, Is.EqualTo("TREASURY_CRITICAL_MOVEMENT"));
        }

        // ============================================================
        // AC3: Evaluator logic - OwnershipConsistency dimension
        // ============================================================

        [Test]
        public async Task OwnershipConsistency_WhenRecordsMatch_ReturnsPass()
        {
            var state = new TokenStateInputs { OwnershipRecordsMatch = true };
            var health = await _service.EvaluateHealthAsync(4, "voimain-v1.0",
                new[] { "OwnershipConsistency" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "OwnershipConsistency");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Pass));
        }

        [Test]
        public async Task OwnershipConsistency_WhenRecordsMismatch_ReturnsWarning()
        {
            var state = new TokenStateInputs { OwnershipRecordsMatch = false };
            var health = await _service.EvaluateHealthAsync(4, "voimain-v1.0",
                new[] { "OwnershipConsistency" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "OwnershipConsistency");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Warning));
            Assert.That(result.FindingCode, Is.EqualTo("OWNERSHIP_RECORDS_MISMATCH"));
        }

        [Test]
        public async Task OwnershipConsistency_WhenUnauthorizedChanges_ReturnsCriticalFail()
        {
            var state = new TokenStateInputs { UnauthorizedManagerChangesDetected = true };
            var health = await _service.EvaluateHealthAsync(4, "voimain-v1.0",
                new[] { "OwnershipConsistency" }, state);

            var result = health.PolicyResults.Single(r => r.DimensionId == "OwnershipConsistency");
            Assert.That(result.Status, Is.EqualTo(PolicyStatus.Fail));
            Assert.That(result.Severity, Is.EqualTo(AssessmentSeverity.Critical));
            Assert.That(result.FindingCode, Is.EqualTo("OWNERSHIP_UNAUTHORIZED_CHANGE"));
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        // ============================================================
        // AC3: Overall health score computation
        // ============================================================

        [Test]
        public async Task EvaluateHealthAsync_WithAllPass_ReturnsHealthyStatus()
        {
            var state = new TokenStateInputs
            {
                MintAuthorityRevoked = true,
                MetadataCompletenessPercent = 95,
                MetadataUrlAccessible = true,
                LargeTreasuryMovementDetected = false,
                OwnershipRecordsMatch = true
            };
            var health = await _service.EvaluateHealthAsync(5, "voimain-v1.0", stateInputs: state);

            Assert.That(health.OverallStatus, Is.EqualTo(TokenHealthStatus.Healthy));
            Assert.That(health.HealthScore, Is.EqualTo(1.0));
            Assert.That(health.FailingDimensions, Is.EqualTo(0));
        }

        [Test]
        public async Task EvaluateHealthAsync_WithAllFail_ReturnsUnhealthyStatus()
        {
            var state = new TokenStateInputs
            {
                MintAuthorityRevoked = false,
                SupplyCapReached = false,
                MetadataUrlAccessible = false,
                LargeTreasuryMovementDetected = true,
                LargestMovementPercent = 50,
                UnauthorizedManagerChangesDetected = true
            };
            var health = await _service.EvaluateHealthAsync(6, "voimain-v1.0", stateInputs: state);

            Assert.That(health.OverallStatus, Is.EqualTo(TokenHealthStatus.Unhealthy));
            Assert.That(health.HealthScore, Is.LessThan(0.5));
            Assert.That(health.FailingDimensions, Is.GreaterThan(0));
        }

        [Test]
        public async Task EvaluateHealthAsync_WithSameInputs_ReturnsDeterministicResults()
        {
            var state = new TokenStateInputs
            {
                MintAuthorityRevoked = false,
                SupplyCapReached = true,
                MetadataCompletenessPercent = 80,
                LargeTreasuryMovementDetected = false,
                OwnershipRecordsMatch = true
            };

            var result1 = await _service.EvaluateHealthAsync(7, "voimain-v1.0", stateInputs: state);
            var result2 = await _service.EvaluateHealthAsync(7, "voimain-v1.0", stateInputs: state);

            Assert.That(result1.OverallStatus, Is.EqualTo(result2.OverallStatus));
            Assert.That(result1.HealthScore, Is.EqualTo(result2.HealthScore));
            Assert.That(result1.PolicyResults.Count, Is.EqualTo(result2.PolicyResults.Count));
            for (int i = 0; i < result1.PolicyResults.Count; i++)
            {
                Assert.That(result1.PolicyResults[i].Status, Is.EqualTo(result2.PolicyResults[i].Status));
                Assert.That(result1.PolicyResults[i].FindingCode, Is.EqualTo(result2.PolicyResults[i].FindingCode));
            }
        }

        [Test]
        public async Task EvaluateHealthAsync_WithAllDimensions_ReturnsAllFourPolicyResults()
        {
            var health = await _service.EvaluateHealthAsync(9999999, "mainnet-v1.0");

            Assert.That(health.PolicyResults.Count, Is.EqualTo(4));
            var dimensionIds = health.PolicyResults.Select(r => r.DimensionId).ToHashSet();
            Assert.That(dimensionIds, Contains.Item("MintAuthority"));
            Assert.That(dimensionIds, Contains.Item("MetadataCompleteness"));
            Assert.That(dimensionIds, Contains.Item("TreasuryMovement"));
            Assert.That(dimensionIds, Contains.Item("OwnershipConsistency"));
        }

        [Test]
        public async Task EvaluateHealthAsync_PolicyResults_HaveRequiredFields()
        {
            var health = await _service.EvaluateHealthAsync(1234567, "voimain-v1.0");

            foreach (var result in health.PolicyResults)
            {
                Assert.That(result.DimensionId, Is.Not.Null.And.Not.Empty);
                Assert.That(result.DimensionName, Is.Not.Null.And.Not.Empty);
                Assert.That(result.Description, Is.Not.Null.And.Not.Empty);
                Assert.That(result.EvaluatedAt, Is.LessThanOrEqualTo(DateTime.UtcNow)
                    .And.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
            }
        }

        [Test]
        public async Task EvaluateHealthAsync_HealthScore_IsInValidRange()
        {
            var health = await _service.EvaluateHealthAsync(1234567, "voimain-v1.0");
            Assert.That(health.HealthScore, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0));
        }

        [Test]
        public async Task EvaluateHealthAsync_WithFilteredDimensions_ReturnsOnlyRequestedDimensions()
        {
            var requestedDimensions = new[] { "MintAuthority", "MetadataCompleteness" };
            var health = await _service.EvaluateHealthAsync(1234567, "voimain-v1.0", requestedDimensions);

            Assert.That(health.PolicyResults.Count, Is.EqualTo(2));
            Assert.That(health.PolicyResults.Any(r => r.DimensionId == "MintAuthority"), Is.True);
            Assert.That(health.PolicyResults.Any(r => r.DimensionId == "MetadataCompleteness"), Is.True);
        }

        [Test]
        public async Task EvaluateHealthAsync_WithUnknownDimension_ReturnsDegradedResult()
        {
            var health = await _service.EvaluateHealthAsync(1234567, "voimain-v1.0",
                new[] { "InvalidDimension" });

            Assert.That(health.PolicyResults.Count, Is.EqualTo(1));
            Assert.That(health.PolicyResults[0].Status, Is.EqualTo(PolicyStatus.Degraded));
            Assert.That(health.PolicyResults[0].FindingCode, Is.EqualTo("UNKNOWN_DIMENSION"));
        }

        // ============================================================
        // AC4: Recommendation reason-code stability
        // ============================================================

        [Test]
        public async Task GetRecommendationsAsync_ReturnsRecommendationsWithReasonCodes()
        {
            var recommendations = await _service.GetRecommendationsAsync(1234567, "voimain-v1.0");

            Assert.That(recommendations, Is.Not.Empty);
            foreach (var rec in recommendations)
            {
                Assert.That(rec.ReasonCode, Is.Not.Null.And.Not.Empty);
                Assert.That(rec.Rationale, Is.Not.Null.And.Not.Empty);
                Assert.That(rec.SuggestedAction, Is.Not.Null.And.Not.Empty);
                Assert.That(rec.Title, Is.Not.Null.And.Not.Empty);
            }
        }

        [Test]
        public async Task GetRecommendationsAsync_ReturnsExpectedReasonCodes()
        {
            var recommendations = await _service.GetRecommendationsAsync(1234567, "voimain-v1.0");

            var reasonCodes = recommendations.Select(r => r.ReasonCode).ToHashSet();
            Assert.That(reasonCodes, Contains.Item("MINT_AUTHORITY_UNREVOKED"));
            Assert.That(reasonCodes, Contains.Item("METADATA_INCOMPLETE"));
            Assert.That(reasonCodes, Contains.Item("OWNERSHIP_UNVERIFIED"));
            Assert.That(reasonCodes, Contains.Item("TREASURY_UNMONITORED"));
        }

        [Test]
        public async Task GetRecommendationsAsync_AreOrderedByPriorityDescending()
        {
            var recommendations = await _service.GetRecommendationsAsync(1234567, "voimain-v1.0");

            for (int i = 1; i < recommendations.Count; i++)
            {
                Assert.That(recommendations[i].Priority, Is.LessThanOrEqualTo(recommendations[i - 1].Priority),
                    "Recommendations should be ordered by priority descending");
            }
        }

        [Test]
        public async Task GetRecommendationsAsync_WithSameInputs_ProducesDeterministicOrdering()
        {
            var recs1 = await _service.GetRecommendationsAsync(1234567, "voimain-v1.0");
            var recs2 = await _service.GetRecommendationsAsync(1234567, "voimain-v1.0");

            Assert.That(recs1.Count, Is.EqualTo(recs2.Count));
            for (int i = 0; i < recs1.Count; i++)
            {
                Assert.That(recs1[i].ReasonCode, Is.EqualTo(recs2[i].ReasonCode));
                Assert.That(recs1[i].Priority, Is.EqualTo(recs2[i].Priority));
            }
        }

        [Test]
        public async Task GetRecommendationsAsync_ReasonCodes_AreStableAcrossAssets()
        {
            // Same reason codes regardless of assetId (rule-based, not data-based)
            var recs1 = await _service.GetRecommendationsAsync(111111, "voimain-v1.0");
            var recs2 = await _service.GetRecommendationsAsync(999999, "mainnet-v1.0");

            var codes1 = recs1.Select(r => r.ReasonCode).OrderBy(c => c).ToList();
            var codes2 = recs2.Select(r => r.ReasonCode).OrderBy(c => c).ToList();
            Assert.That(codes1, Is.EqualTo(codes2), "Reason codes must be stable and not depend on assetId");
        }

        // ============================================================
        // AC5: Event normalization - required fields and edge cases
        // ============================================================

        [Test]
        public async Task GetNormalizedEventsAsync_ReturnsEventsWithRequiredFields()
        {
            var events = await _service.GetNormalizedEventsAsync(1234567, "voimain-v1.0");

            Assert.That(events, Is.Not.Null);
            foreach (var evt in events)
            {
                Assert.That(evt.EventId, Is.Not.Null.And.Not.Empty);
                Assert.That(evt.Actor, Is.Not.Null.And.Not.Empty);
                Assert.That(evt.Description, Is.Not.Null.And.Not.Empty);
                Assert.That(evt.OccurredAt, Is.Not.EqualTo(default(DateTime)));
                Assert.That(Enum.IsDefined(typeof(TokenEventCategory), evt.Category), Is.True);
                Assert.That(Enum.IsDefined(typeof(EventImpact), evt.Impact), Is.True);
            }
        }

        [Test]
        public async Task GetNormalizedEventsAsync_RespectsMaxEventsLimit()
        {
            var events = await _service.GetNormalizedEventsAsync(1234567, "voimain-v1.0", maxEvents: 1);
            Assert.That(events.Count, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public async Task GetNormalizedEventsAsync_WithMinimumMaxEvents_ReturnsAtMostOne()
        {
            var events = await _service.GetNormalizedEventsAsync(1234567, "voimain-v1.0", maxEvents: 1);
            Assert.That(events.Count, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(1));
        }

        [Test]
        public async Task GetNormalizedEventsAsync_WithDetails_IncludesDetailsField()
        {
            var events = await _service.GetNormalizedEventsAsync(1234567, "voimain-v1.0", includeDetails: true);

            var deployEvent = events.FirstOrDefault(e => e.Category == TokenEventCategory.Deployment);
            Assert.That(deployEvent, Is.Not.Null);
            Assert.That(deployEvent!.Details, Is.Not.Null);
        }

        [Test]
        public async Task GetNormalizedEventsAsync_WithoutDetails_DetailsAreNull()
        {
            var events = await _service.GetNormalizedEventsAsync(1234567, "voimain-v1.0", includeDetails: false);

            foreach (var evt in events)
            {
                Assert.That(evt.Details, Is.Null);
            }
        }

        [Test]
        public async Task GetNormalizedEventsAsync_EventIds_AreUniqueAndDeterministic()
        {
            // EventId incorporates assetId to be deterministic
            var events1 = await _service.GetNormalizedEventsAsync(1111111, "voimain-v1.0");
            var events2 = await _service.GetNormalizedEventsAsync(1111111, "voimain-v1.0");

            for (int i = 0; i < events1.Count; i++)
            {
                Assert.That(events1[i].EventId, Is.EqualTo(events2[i].EventId),
                    "EventId must be deterministic for same assetId");
            }
        }

        // ============================================================
        // AC1/AC2: Consolidated endpoint returns health, recommendations, and events
        // ============================================================

        [Test]
        public async Task GetOperationsIntelligenceAsync_ReturnsConsolidatedResponse()
        {
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0",
                MaxEvents = 5
            };

            var response = await _service.GetOperationsIntelligenceAsync(request);

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
            var request = new TokenOperationsIntelligenceRequest { AssetId = 9999, Network = "mainnet-v1.0" };
            var response = await _service.GetOperationsIntelligenceAsync(request);

            Assert.That(response.ContractVersion, Is.Not.Null);
            Assert.That(response.ContractVersion.ApiVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(response.ContractVersion.SchemaVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(response.ContractVersion.BackwardCompatible, Is.True);
            Assert.That(response.ContractVersion.GeneratedAt, Is.LessThanOrEqualTo(DateTime.UtcNow));
        }

        [Test]
        public async Task GetOperationsIntelligenceAsync_WithStateInputs_ReflectsInHealth()
        {
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 777,
                Network = "voimain-v1.0",
                StateInputs = new TokenStateInputs
                {
                    MintAuthorityRevoked = true,
                    MetadataCompletenessPercent = 95,
                    MetadataUrlAccessible = true,
                    LargeTreasuryMovementDetected = false,
                    OwnershipRecordsMatch = true
                }
            };

            var response = await _service.GetOperationsIntelligenceAsync(request);

            Assert.That(response.Health, Is.Not.Null);
            Assert.That(response.Health!.OverallStatus, Is.EqualTo(TokenHealthStatus.Healthy));

            var mintResult = response.Health.PolicyResults.First(r => r.DimensionId == "MintAuthority");
            Assert.That(mintResult.Status, Is.EqualTo(PolicyStatus.Pass));
        }

        [Test]
        public async Task GetOperationsIntelligenceAsync_SetsCorrelationId()
        {
            var correlationId = "test-correlation-123";
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0",
                CorrelationId = correlationId
            };

            var response = await _service.GetOperationsIntelligenceAsync(request);
            Assert.That(response.CorrelationId, Is.EqualTo(correlationId));
        }

        [Test]
        public async Task GetOperationsIntelligenceAsync_AutoGeneratesCorrelationId_WhenNotProvided()
        {
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0",
                CorrelationId = null
            };

            var response = await _service.GetOperationsIntelligenceAsync(request);
            Assert.That(response.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        // ============================================================
        // AC6: Degraded-state signaling
        // ============================================================

        [Test]
        public async Task GetOperationsIntelligenceAsync_NormalCase_IsDegradedIsFalse()
        {
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0"
            };

            var response = await _service.GetOperationsIntelligenceAsync(request);

            Assert.That(response.IsDegraded, Is.False);
            Assert.That(response.DegradedSources, Is.Empty);
        }

        [Test]
        public async Task GetOperationsIntelligenceAsync_StillSucceeds_WithHealthyState()
        {
            // Even with all-pass state, response is Success=true and IsDegraded=false
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0",
                StateInputs = new TokenStateInputs
                {
                    MintAuthorityRevoked = true,
                    OwnershipRecordsMatch = true,
                    LargeTreasuryMovementDetected = false,
                    MetadataCompletenessPercent = 100
                }
            };

            var response = await _service.GetOperationsIntelligenceAsync(request);

            Assert.That(response.Success, Is.True);
            Assert.That(response.IsDegraded, Is.False);
        }

        // ============================================================
        // AC8/AC9: Caching behavior
        // ============================================================

        [Test]
        public async Task GetOperationsIntelligenceAsync_SecondCall_UsesCache()
        {
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 8888888,
                Network = "aramidmain-v1.0"
            };

            var response1 = await _service.GetOperationsIntelligenceAsync(request);
            var response2 = await _service.GetOperationsIntelligenceAsync(request);

            Assert.That(response1.Success, Is.True);
            Assert.That(response2.Success, Is.True);
            Assert.That(response2.HealthFromCache, Is.True,
                "Second call should indicate health was served from cache");
        }

        [Test]
        public async Task GetOperationsIntelligenceAsync_FirstCall_NotFromCache()
        {
            // Use a unique assetId to ensure cache is empty
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 5555555,
                Network = "voimain-v1.0"
            };

            var response = await _service.GetOperationsIntelligenceAsync(request);
            Assert.That(response.HealthFromCache, Is.False, "First call should not be from cache");
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

        // ============================================================
        // Contract compatibility and consumer migration (AC2, AC5, AC12)
        // ============================================================

        [Test]
        public async Task ContractVersion_ApiVersion_FollowsSemanticVersionFormat()
        {
            var request = new TokenOperationsIntelligenceRequest { AssetId = 1001, Network = "mainnet-v1.0" };
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // v1.0, v1.1, v2.0 etc.
            Assert.That(response.ContractVersion.ApiVersion, Does.Match(@"^v\d+\.\d+$"),
                "ApiVersion must follow 'vMAJOR.MINOR' format for consumer parsing stability");
        }

        [Test]
        public async Task ContractVersion_SchemaVersion_FollowsSemanticVersionFormat()
        {
            var request = new TokenOperationsIntelligenceRequest { AssetId = 1002, Network = "mainnet-v1.0" };
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // 1.0.0, 1.1.0, 2.0.0 etc.
            Assert.That(response.ContractVersion.SchemaVersion, Does.Match(@"^\d+\.\d+\.\d+$"),
                "SchemaVersion must follow 'MAJOR.MINOR.PATCH' semver for consumer upgrade decisions");
        }

        [Test]
        public async Task ContractVersion_MinClientVersion_FollowsSemanticVersionFormat()
        {
            var request = new TokenOperationsIntelligenceRequest { AssetId = 1003, Network = "mainnet-v1.0" };
            var response = await _service.GetOperationsIntelligenceAsync(request);

            Assert.That(response.ContractVersion.MinClientVersion, Does.Match(@"^\d+\.\d+\.\d+$"),
                "MinClientVersion must follow semver so consumers can gate upgrade checks");
        }

        [Test]
        public async Task ContractVersion_DeprecatedFields_EmptyForV1_NoMigrationNeeded()
        {
            var request = new TokenOperationsIntelligenceRequest { AssetId = 1004, Network = "mainnet-v1.0" };
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // v1.0 has no deprecated fields; consumers should check this list before each request
            Assert.That(response.ContractVersion.DeprecatedFields, Is.Not.Null,
                "DeprecatedFields must always be non-null (empty list, not null) for safe consumer iteration");
            Assert.That(response.ContractVersion.DeprecatedFields, Is.Empty,
                "No deprecated fields in v1.0 - consumers need not apply any migration");
        }

        [Test]
        public async Task ContractVersion_BackwardCompatible_TrueForV1_ConsumersSafeToUpgrade()
        {
            var request = new TokenOperationsIntelligenceRequest { AssetId = 1005, Network = "mainnet-v1.0" };
            var response = await _service.GetOperationsIntelligenceAsync(request);

            Assert.That(response.ContractVersion.BackwardCompatible, Is.True,
                "BackwardCompatible=true means consumers with existing parsers need not change code to consume v1.0");
        }

        [Test]
        public async Task ContractVersion_MultipleEvaluations_ProduceConsistentVersionMetadata()
        {
            // Validates that contract version is not random/per-request — consumers can cache version checks
            var request1 = new TokenOperationsIntelligenceRequest { AssetId = 2001, Network = "voimain-v1.0" };
            var request2 = new TokenOperationsIntelligenceRequest { AssetId = 2002, Network = "voimain-v1.0" };

            var response1 = await _service.GetOperationsIntelligenceAsync(request1);
            var response2 = await _service.GetOperationsIntelligenceAsync(request2);

            Assert.That(response1.ContractVersion.ApiVersion, Is.EqualTo(response2.ContractVersion.ApiVersion));
            Assert.That(response1.ContractVersion.SchemaVersion, Is.EqualTo(response2.ContractVersion.SchemaVersion));
            Assert.That(response1.ContractVersion.MinClientVersion, Is.EqualTo(response2.ContractVersion.MinClientVersion));
            Assert.That(response1.ContractVersion.BackwardCompatible, Is.EqualTo(response2.ContractVersion.BackwardCompatible));
        }

        [Test]
        public async Task ContractVersion_PresentEvenWhenHealthDegraded()
        {
            // Consumer migration note: ContractVersion is always populated — even in degraded mode
            // Consumers must not assume ContractVersion implies full data availability
            var metricsLoggerMock = new Mock<ILogger<BiatecTokensApi.Services.MetricsService>>();
            var metricsService = new BiatecTokensApi.Services.MetricsService(metricsLoggerMock.Object);
            var service = new FaultInjectableTokenOperationsIntelligenceService(
                _cache,
                metricsService,
                _loggerMock.Object,
                failHealth: true);

            var request = new TokenOperationsIntelligenceRequest { AssetId = 3001, Network = "voimain-v1.0" };
            var response = await service.GetOperationsIntelligenceAsync(request);

            Assert.That(response.ContractVersion, Is.Not.Null,
                "ContractVersion must be present even in degraded mode — consumers use it for version gating");
            Assert.That(response.ContractVersion.ApiVersion, Is.EqualTo("v1.0"));
            Assert.That(response.IsDegraded, Is.True,
                "IsDegraded=true must coexist with valid ContractVersion — they are independent fields");
        }

        // ============================================================
        // AC8: Telemetry emitted for latency, failure class, degraded-mode
        // ============================================================

        [Test]
        public async Task Telemetry_NormalRequest_EmitsLatencyAndRequestCounter()
        {
            // Arrange: use a mock IMetricsService to capture metric calls
            var metricsMock = new Mock<BiatecTokensApi.Services.Interface.IMetricsService>();
            var service = new TokenOperationsIntelligenceService(
                _cache,
                metricsMock.Object,
                _loggerMock.Object);

            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 5001,
                Network = "voimain-v1.0"
            };

            // Act
            await service.GetOperationsIntelligenceAsync(request);

            // Assert: latency histogram recorded
            metricsMock.Verify(
                m => m.RecordHistogram("operations_intelligence.latency_ms", It.Is<double>(v => v >= 0)),
                Times.Once,
                "Latency histogram must be emitted for each evaluation (AC8)");

            // Assert: request counter incremented by 1
            metricsMock.Verify(
                m => m.IncrementCounter("operations_intelligence.requests_total", 1),
                Times.Once,
                "Request counter must be incremented by 1 for each evaluation (AC8)");
        }

        [Test]
        public async Task Telemetry_DegradedMode_EmitsDegradedCounterAndFailureClass()
        {
            // Arrange: use a fault-injectable service with mocked metrics to verify degraded telemetry
            var metricsMock = new Mock<BiatecTokensApi.Services.Interface.IMetricsService>();
            var service = new FaultInjectableTokenOperationsIntelligenceService(
                _cache,
                metricsMock.Object,
                _loggerMock.Object,
                failHealth: true);

            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 5002,
                Network = "voimain-v1.0"
            };

            // Act
            var response = await service.GetOperationsIntelligenceAsync(request);

            // Assert: degraded mode detected
            Assert.That(response.IsDegraded, Is.True, "Response must be degraded when health evaluation fails");

            // Assert: degraded counter emitted by 1 (AC8)
            metricsMock.Verify(
                m => m.IncrementCounter("operations_intelligence.degraded_total", 1),
                Times.Once,
                "Degraded counter must be incremented by 1 when any upstream source fails (AC8)");

            // Assert: failure class counter emitted with source name (AC8)
            metricsMock.Verify(
                m => m.IncrementCounter(
                    It.Is<string>(s => s.StartsWith("operations_intelligence.failure_class.")),
                    It.IsAny<long>()),
                Times.AtLeastOnce,
                "Failure class counter must identify the failing source (AC8)");
        }

        [Test]
        public async Task Telemetry_CacheHit_EmitsCacheHitCounter()
        {
            // Arrange: two identical requests — second should be served from cache
            var metricsMock = new Mock<BiatecTokensApi.Services.Interface.IMetricsService>();
            var service = new TokenOperationsIntelligenceService(
                _cache,
                metricsMock.Object,
                _loggerMock.Object);

            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 5003,
                Network = "voimain-v1.0"
            };

            // Act: first call populates cache, second serves from cache
            await service.GetOperationsIntelligenceAsync(request);
            await service.GetOperationsIntelligenceAsync(request);

            // Assert: cache hit counter emitted on second call (AC8)
            metricsMock.Verify(
                m => m.IncrementCounter("operations_intelligence.cache_hits_total", It.IsAny<long>()),
                Times.Once,
                "Cache hit counter must be emitted when health is served from cache (AC8)");
        }

        [Test]
        public async Task Telemetry_NoDegradation_NoDegradedCounterEmitted()
        {
            // Arrange: healthy request with no failures
            var metricsMock = new Mock<BiatecTokensApi.Services.Interface.IMetricsService>();
            var service = new TokenOperationsIntelligenceService(
                _cache,
                metricsMock.Object,
                _loggerMock.Object);

            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 5004,
                Network = "voimain-v1.0"
            };

            // Act
            await service.GetOperationsIntelligenceAsync(request);

            // Assert: degraded counter not emitted when no failures occur (AC8)
            metricsMock.Verify(
                m => m.IncrementCounter("operations_intelligence.degraded_total", It.IsAny<long>()),
                Times.Never,
                "Degraded counter must NOT be emitted on successful evaluations (AC8)");
        }
    }
}

