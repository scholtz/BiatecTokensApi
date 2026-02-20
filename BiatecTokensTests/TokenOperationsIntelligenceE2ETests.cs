using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenOperationsIntelligence;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// End-to-end workflow tests for Token Operations Intelligence API v1.
    /// These tests simulate realistic consumer scenarios: full-success paths,
    /// partial-failure degraded mode, recommendation evolution, and schema contract assertions.
    /// </summary>
    [TestFixture]
    public class TokenOperationsIntelligenceE2ETests
    {
        private TokenOperationsIntelligenceService _service = null!;
        private IMemoryCache _cache = null!;

        [SetUp]
        public void Setup()
        {
            var loggerMock = new Mock<ILogger<TokenOperationsIntelligenceService>>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            var metricsLoggerMock = new Mock<ILogger<BiatecTokensApi.Services.MetricsService>>();
            var metricsService = new BiatecTokensApi.Services.MetricsService(metricsLoggerMock.Object);

            _service = new TokenOperationsIntelligenceService(
                _cache,
                metricsService,
                loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _cache?.Dispose();
        }

        // ============================================================
        // E2E: Full success path - consumer receives full intelligence payload
        // ============================================================

        [Test]
        public async Task FullSuccessPath_HealthyToken_ReturnsCompleteIntelligencePayload()
        {
            // Arrange: a healthy token with all state known
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 9876543,
                Network = "voimain-v1.0",
                CorrelationId = "e2e-test-healthy-token",
                MaxEvents = 5,
                IncludeEventDetails = false,
                StateInputs = new TokenStateInputs
                {
                    MintAuthorityRevoked = true,
                    MetadataCompletenessPercent = 95,
                    MetadataUrlAccessible = true,
                    LargeTreasuryMovementDetected = false,
                    OwnershipRecordsMatch = true
                }
            };

            // Act
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // Assert: consumer receives complete intelligence payload
            Assert.That(response.Success, Is.True, "Success must be true for full-success path");
            Assert.That(response.IsDegraded, Is.False, "IsDegraded must be false for full-success path");
            Assert.That(response.CorrelationId, Is.EqualTo("e2e-test-healthy-token"), "Correlation ID must be preserved");

            // Health assessment
            Assert.That(response.Health, Is.Not.Null, "Health must be populated");
            Assert.That(response.Health!.OverallStatus, Is.EqualTo(TokenHealthStatus.Healthy));
            Assert.That(response.Health.HealthScore, Is.EqualTo(1.0));
            Assert.That(response.Health.PolicyResults.Count, Is.EqualTo(4), "All 4 dimensions must be evaluated");
            Assert.That(response.Health.FailingDimensions, Is.EqualTo(0));

            // Per-dimension results
            var mintResult = response.Health.PolicyResults.Single(r => r.DimensionId == "MintAuthority");
            Assert.That(mintResult.Status, Is.EqualTo(PolicyStatus.Pass));
            Assert.That(mintResult.FindingCode, Is.EqualTo("MINT_AUTHORITY_REVOKED"));

            var metaResult = response.Health.PolicyResults.Single(r => r.DimensionId == "MetadataCompleteness");
            Assert.That(metaResult.Status, Is.EqualTo(PolicyStatus.Pass));

            var treasuryResult = response.Health.PolicyResults.Single(r => r.DimensionId == "TreasuryMovement");
            Assert.That(treasuryResult.Status, Is.EqualTo(PolicyStatus.Pass));

            var ownerResult = response.Health.PolicyResults.Single(r => r.DimensionId == "OwnershipConsistency");
            Assert.That(ownerResult.Status, Is.EqualTo(PolicyStatus.Pass));

            // Recommendations: present with reason codes and rationale
            Assert.That(response.Recommendations, Is.Not.Null.And.Not.Empty);
            Assert.That(response.Recommendations.All(r => !string.IsNullOrEmpty(r.ReasonCode)), Is.True,
                "Every recommendation must have a ReasonCode");
            Assert.That(response.Recommendations.All(r => !string.IsNullOrEmpty(r.Rationale)), Is.True,
                "Every recommendation must have a Rationale");

            // Events: populated
            Assert.That(response.Events, Is.Not.Null);
            Assert.That(response.Events.Count, Is.LessThanOrEqualTo(request.MaxEvents));

            // Contract version: schema metadata present
            Assert.That(response.ContractVersion, Is.Not.Null);
            Assert.That(response.ContractVersion.ApiVersion, Is.EqualTo("v1.0"));
            Assert.That(response.ContractVersion.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(response.ContractVersion.BackwardCompatible, Is.True);
        }

        // ============================================================
        // E2E: At-risk token - consumer receives warnings with actionable metadata
        // ============================================================

        [Test]
        public async Task AtRiskPath_TokenWithIssues_ReturnsActionableErrorAndDegradedMetadata()
        {
            // Arrange: token with active, uncapped mint authority and large treasury movement
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1111111,
                Network = "voimain-v1.0",
                CorrelationId = "e2e-test-at-risk",
                StateInputs = new TokenStateInputs
                {
                    MintAuthorityRevoked = false,
                    SupplyCapReached = false,
                    LargeTreasuryMovementDetected = true,
                    LargestMovementPercent = 40,
                    OwnershipRecordsMatch = true
                }
            };

            // Act
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // Assert: consumer sees issues clearly
            Assert.That(response.Success, Is.True);
            Assert.That(response.Health, Is.Not.Null);
            Assert.That(response.Health!.OverallStatus, Is.Not.EqualTo(TokenHealthStatus.Healthy));
            Assert.That(response.Health.FailingDimensions, Is.GreaterThan(0));

            // MintAuthority is failing
            var mintResult = response.Health.PolicyResults.Single(r => r.DimensionId == "MintAuthority");
            Assert.That(mintResult.Status, Is.EqualTo(PolicyStatus.Fail));
            Assert.That(mintResult.RemediationHint, Is.Not.Null.And.Not.Empty,
                "Operator must receive remediation guidance");

            // TreasuryMovement is critical
            var treasuryResult = response.Health.PolicyResults.Single(r => r.DimensionId == "TreasuryMovement");
            Assert.That(treasuryResult.Status, Is.EqualTo(PolicyStatus.Fail));
            Assert.That(treasuryResult.Severity, Is.EqualTo(AssessmentSeverity.Critical));
            Assert.That(treasuryResult.RemediationHint, Is.Not.Null.And.Not.Empty);

            // Recommendations still present with reason codes
            Assert.That(response.Recommendations, Is.Not.Null.And.Not.Empty);
        }

        // ============================================================
        // E2E: Degraded mode scenario - partial upstream source failure
        // ============================================================

        [Test]
        public async Task DegradedMode_NormalHealthEvaluation_ReturnsBestEffortWithNoDegradedFlag()
        {
            // Arrange: no injected failure (normal path)
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 2222222,
                Network = "mainnet-v1.0",
                CorrelationId = "e2e-test-degraded"
            };

            // Act
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // Assert: no degraded state in normal conditions
            Assert.That(response.Success, Is.True);
            Assert.That(response.IsDegraded, Is.False,
                "Under normal conditions, IsDegraded must be false");
            Assert.That(response.DegradedSources, Is.Empty);
            Assert.That(response.Health, Is.Not.Null, "Health must still be populated");
            Assert.That(response.Recommendations, Is.Not.Null, "Recommendations must still be populated");
            Assert.That(response.Events, Is.Not.Null, "Events must still be populated");
        }

        [Test]
        public async Task DegradedMode_PartialHealthResult_ExplicitlyFlagged()
        {
            // Arrange: token with unknown state dimensions (no state inputs provided)
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 3333333,
                Network = "voimain-v1.0",
                PolicyDimensions = new List<string> { "InvalidDimension" }
            };

            // Act
            var response = await _service.GetOperationsIntelligenceAsync(request);

            // Assert: degraded dimension is explicitly flagged within health
            Assert.That(response.Success, Is.True);
            Assert.That(response.Health, Is.Not.Null);
            var degradedResult = response.Health!.PolicyResults.First(r => r.Status == PolicyStatus.Degraded);
            Assert.That(degradedResult.FindingCode, Is.EqualTo("UNKNOWN_DIMENSION"));
        }

        // ============================================================
        // E2E: Recommendation evolution stability
        // ============================================================

        [Test]
        public async Task RecommendationEvolution_SameAsset_StableOrderingAndCodes()
        {
            // Simulate two calls representing successive evaluations over time
            var request1 = new TokenOperationsIntelligenceRequest
            {
                AssetId = 4444444,
                Network = "voimain-v1.0"
            };
            var request2 = new TokenOperationsIntelligenceRequest
            {
                AssetId = 4444444,
                Network = "voimain-v1.0"
            };

            var response1 = await _service.GetOperationsIntelligenceAsync(request1);
            _cache.Remove($"token_ops_intel_health_4444444_voimain-v1.0"); // force fresh evaluation
            var response2 = await _service.GetOperationsIntelligenceAsync(request2);

            // Assert: same reason codes in same order
            Assert.That(response1.Recommendations.Count, Is.EqualTo(response2.Recommendations.Count));
            for (int i = 0; i < response1.Recommendations.Count; i++)
            {
                Assert.That(response1.Recommendations[i].ReasonCode,
                    Is.EqualTo(response2.Recommendations[i].ReasonCode),
                    $"Recommendation[{i}] ReasonCode must be stable across evaluations");
                Assert.That(response1.Recommendations[i].Priority,
                    Is.EqualTo(response2.Recommendations[i].Priority),
                    $"Recommendation[{i}] Priority must be stable across evaluations");
            }
        }

        // ============================================================
        // E2E: Schema contract assertions
        // ============================================================

        [Test]
        public async Task SchemaContract_AllRequiredFields_PresentInResponse()
        {
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 5555555,
                Network = "voimain-v1.0"
            };

            var response = await _service.GetOperationsIntelligenceAsync(request);

            // Top-level required fields
            Assert.That(response.AssetId, Is.EqualTo(request.AssetId));
            Assert.That(response.Network, Is.EqualTo(request.Network));
            Assert.That(response.CorrelationId, Is.Not.Null.And.Not.Empty);
            Assert.That(response.GeneratedAt, Is.LessThanOrEqualTo(DateTime.UtcNow));

            // ContractVersion required fields
            Assert.That(response.ContractVersion.ApiVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(response.ContractVersion.SchemaVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(response.ContractVersion.MinClientVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(response.ContractVersion.DeprecatedFields, Is.Not.Null);

            // Health required fields
            Assert.That(response.Health, Is.Not.Null);
            Assert.That(response.Health!.PolicyResults, Is.Not.Null);
            Assert.That(response.Health.HealthScore, Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0));

            // Each PolicyAssuranceResult required fields
            foreach (var result in response.Health.PolicyResults)
            {
                Assert.That(result.DimensionId, Is.Not.Null.And.Not.Empty);
                Assert.That(result.DimensionName, Is.Not.Null.And.Not.Empty);
                Assert.That(result.Description, Is.Not.Null.And.Not.Empty);
                Assert.That(result.EvaluatedAt, Is.Not.EqualTo(default(DateTime)));
                Assert.That(Enum.IsDefined(typeof(PolicyStatus), result.Status), Is.True);
                Assert.That(Enum.IsDefined(typeof(AssessmentSeverity), result.Severity), Is.True);
            }

            // Recommendations required fields
            foreach (var rec in response.Recommendations)
            {
                Assert.That(rec.ReasonCode, Is.Not.Null.And.Not.Empty);
                Assert.That(rec.Title, Is.Not.Null.And.Not.Empty);
                Assert.That(rec.Rationale, Is.Not.Null.And.Not.Empty);
                Assert.That(rec.SuggestedAction, Is.Not.Null.And.Not.Empty);
                Assert.That(Enum.IsDefined(typeof(AssessmentSeverity), rec.Severity), Is.True);
            }

            // Events required fields
            foreach (var evt in response.Events)
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
        public async Task SchemaContract_ErrorModel_HasMachineReadableCodeAndRemediationHint()
        {
            // Validate error model structure (from ErrorCodes constants)
            var errorResponse = new ApiErrorResponse
            {
                Success = false,
                ErrorCode = BiatecTokensApi.Models.ErrorCodes.INVALID_REQUEST,
                ErrorMessage = "AssetId must be a positive integer.",
                RemediationHint = "Provide a valid token asset ID.",
                CorrelationId = "e2e-test-error-schema"
            };

            Assert.That(errorResponse.ErrorCode, Is.Not.Null.And.Not.Empty, "Machine-readable error code must be present");
            Assert.That(errorResponse.RemediationHint, Is.Not.Null.And.Not.Empty, "Remediation hint must be present");
            Assert.That(errorResponse.Success, Is.False);
            Assert.That(errorResponse.CorrelationId, Is.Not.Null.And.Not.Empty, "Correlation ID must be present in errors");
        }

        // ============================================================
        // E2E: Policy result details are populated correctly
        // ============================================================

        [Test]
        public async Task PolicyResult_Details_ArePopulatedForQuantitativeDimensions()
        {
            var state = new TokenStateInputs
            {
                MetadataCompletenessPercent = 75,
                LargeTreasuryMovementDetected = true,
                LargestMovementPercent = 20
            };

            var health = await _service.EvaluateHealthAsync(6666666, "voimain-v1.0", stateInputs: state);

            var metaResult = health.PolicyResults.Single(r => r.DimensionId == "MetadataCompleteness");
            Assert.That(metaResult.Details, Is.Not.Null, "Completeness details must be present");
            Assert.That(metaResult.Details!.ContainsKey("completenessPercent"), Is.True);
            Assert.That(metaResult.Details["completenessPercent"], Is.EqualTo(75.0));

            var treasuryResult = health.PolicyResults.Single(r => r.DimensionId == "TreasuryMovement");
            Assert.That(treasuryResult.Details, Is.Not.Null, "Treasury details must be present");
            Assert.That(treasuryResult.Details!.ContainsKey("largestMovementPercent"), Is.True);
            Assert.That(treasuryResult.Details["largestMovementPercent"], Is.EqualTo(20.0));
        }

        // ============================================================
        // E2E: Health computation counters
        // ============================================================

        [Test]
        public async Task TokenHealthAssessment_ComputedCounters_AreConsistentWithPolicyResults()
        {
            var state = new TokenStateInputs
            {
                MintAuthorityRevoked = true,          // Pass
                MetadataCompletenessPercent = 70,      // Warning
                LargeTreasuryMovementDetected = false, // Pass
                OwnershipRecordsMatch = false          // Warning
            };

            var health = await _service.EvaluateHealthAsync(7777777, "voimain-v1.0", stateInputs: state);

            Assert.That(health.PassingDimensions, Is.EqualTo(2), "2 dimensions should pass");
            Assert.That(health.WarningDimensions, Is.EqualTo(2), "2 dimensions should warn");
            Assert.That(health.FailingDimensions, Is.EqualTo(0), "0 dimensions should fail");
            Assert.That(health.PassingDimensions + health.WarningDimensions + health.FailingDimensions + health.DegradedDimensions,
                Is.EqualTo(health.PolicyResults.Count), "Counts must sum to total");
        }

        // ============================================================
        // E2E: Degraded-mode with explicit fault injection
        // These tests use FaultInjectableTokenOperationsIntelligenceService
        // to simulate real upstream source failures and assert IsDegraded=true.
        // ============================================================

        [Test]
        public async Task DegradedMode_HealthEvaluatorFails_ReturnsDegradedWithHealthSource()
        {
            // Arrange: health evaluation throws a simulated upstream exception
            var faultService = new FaultInjectableTokenOperationsIntelligenceService(
                _cache, CreateMetricsService(), CreateLogger(),
                failHealth: true);

            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0",
                CorrelationId = "e2e-test-degraded-health"
            };

            // Act
            var response = await faultService.GetOperationsIntelligenceAsync(request);

            // Assert: IsDegraded=true, HealthEvaluator listed as degraded source
            Assert.That(response.Success, Is.True,
                "Success must be true even when a source fails - best-effort semantics");
            Assert.That(response.IsDegraded, Is.True,
                "IsDegraded must be true when health evaluator throws");
            Assert.That(response.DegradedSources, Contains.Item("HealthEvaluator"),
                "HealthEvaluator must be listed in DegradedSources");

            // Health is replaced with degraded assessment (not null)
            Assert.That(response.Health, Is.Not.Null,
                "Health must not be null even in degraded mode - BuildDegradedHealthAssessment used");
            // BuildDegradedHealthAssessment returns Unknown (not Degraded) as the OverallStatus
            Assert.That(response.Health!.OverallStatus, Is.EqualTo(TokenHealthStatus.Unknown),
                "OverallStatus must be Unknown when health evaluator fails (BuildDegradedHealthAssessment)");
            Assert.That(response.Health.IsPartialResult, Is.True,
                "IsPartialResult must be true for degraded health assessment");
            Assert.That(response.Health.DegradedReason, Is.Not.Null.And.Not.Empty,
                "DegradedReason must explain why health is unavailable");

            // Recommendations and events still returned (unaffected by health failure)
            Assert.That(response.Recommendations, Is.Not.Null.And.Not.Empty,
                "Recommendations should still be returned when only health source fails");
            Assert.That(response.Events, Is.Not.Null,
                "Events should still be returned when only health source fails");
        }

        [Test]
        public async Task DegradedMode_RecommendationEngineFails_ReturnsDegradedWithRecommendationSource()
        {
            // Arrange: recommendation generation throws
            var faultService = new FaultInjectableTokenOperationsIntelligenceService(
                _cache, CreateMetricsService(), CreateLogger(),
                failRecommendations: true);

            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 2222222,
                Network = "mainnet-v1.0",
                CorrelationId = "e2e-test-degraded-recs"
            };

            // Act
            var response = await faultService.GetOperationsIntelligenceAsync(request);

            // Assert: IsDegraded=true, RecommendationEngine listed
            Assert.That(response.Success, Is.True);
            Assert.That(response.IsDegraded, Is.True);
            Assert.That(response.DegradedSources, Contains.Item("RecommendationEngine"),
                "RecommendationEngine must be listed in DegradedSources when it fails");

            // Recommendations are empty (failed gracefully)
            Assert.That(response.Recommendations, Is.Empty,
                "Recommendations must be empty when engine fails");

            // Health was evaluated normally (not from BuildDegradedHealthAssessment)
            Assert.That(response.Health, Is.Not.Null);
            // IsPartialResult=false means health was computed normally (not degraded due to evaluator failure)
            Assert.That(response.Health!.IsPartialResult, Is.False,
                "IsPartialResult must be false when only recommendations fail (health evaluated normally)");
            Assert.That(response.Events, Is.Not.Null);
        }

        [Test]
        public async Task DegradedMode_EventNormalizerFails_ReturnsDegradedWithEventSource()
        {
            // Arrange: event retrieval throws
            var faultService = new FaultInjectableTokenOperationsIntelligenceService(
                _cache, CreateMetricsService(), CreateLogger(),
                failEvents: true);

            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 3333333,
                Network = "voimain-v1.0",
                CorrelationId = "e2e-test-degraded-events"
            };

            // Act
            var response = await faultService.GetOperationsIntelligenceAsync(request);

            // Assert: IsDegraded=true, EventNormalizer listed
            Assert.That(response.Success, Is.True);
            Assert.That(response.IsDegraded, Is.True);
            Assert.That(response.DegradedSources, Contains.Item("EventNormalizer"),
                "EventNormalizer must be listed in DegradedSources when it fails");

            // Events are empty (failed gracefully)
            Assert.That(response.Events, Is.Empty, "Events must be empty when normalizer fails");

            // Health and recommendations still returned
            Assert.That(response.Health, Is.Not.Null);
            Assert.That(response.Health!.IsPartialResult, Is.False,
                "IsPartialResult must be false when only events fail (health evaluated normally)");
            Assert.That(response.Recommendations, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task DegradedMode_AllSourcesFail_ReturnsFullyDegradedResponse()
        {
            // Arrange: all three sources throw
            var faultService = new FaultInjectableTokenOperationsIntelligenceService(
                _cache, CreateMetricsService(), CreateLogger(),
                failHealth: true, failRecommendations: true, failEvents: true);

            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 4444444,
                Network = "voimain-v1.0",
                CorrelationId = "e2e-test-fully-degraded"
            };

            // Act
            var response = await faultService.GetOperationsIntelligenceAsync(request);

            // Assert: all 3 degraded, response still 200 (not 500)
            Assert.That(response.Success, Is.True, "Success must be true even with all sources failing");
            Assert.That(response.IsDegraded, Is.True);
            Assert.That(response.DegradedSources.Count, Is.EqualTo(3),
                "All 3 sources must be listed when all fail");
            Assert.That(response.DegradedSources, Contains.Item("HealthEvaluator"));
            Assert.That(response.DegradedSources, Contains.Item("RecommendationEngine"));
            Assert.That(response.DegradedSources, Contains.Item("EventNormalizer"));

            // Empty collections returned - no silent data loss
            Assert.That(response.Recommendations, Is.Empty, "No silent data loss: empty list returned");
            Assert.That(response.Events, Is.Empty, "No silent data loss: empty list returned");

            // Health degraded assessment used (BuildDegradedHealthAssessment returns Unknown)
            Assert.That(response.Health, Is.Not.Null);
            Assert.That(response.Health!.OverallStatus, Is.EqualTo(TokenHealthStatus.Unknown),
                "OverallStatus must be Unknown when health evaluator fails");
            Assert.That(response.Health.IsPartialResult, Is.True);
        }

        [Test]
        public async Task DegradedMode_PartialFailure_CorrelationIdStillPreserved()
        {
            // Arrange: health fails, but correlation ID must survive
            var faultService = new FaultInjectableTokenOperationsIntelligenceService(
                _cache, CreateMetricsService(), CreateLogger(),
                failHealth: true);

            var correlationId = "e2e-test-correlation-in-degraded";
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 5555555,
                Network = "voimain-v1.0",
                CorrelationId = correlationId
            };

            // Act
            var response = await faultService.GetOperationsIntelligenceAsync(request);

            // Assert: correlation ID preserved even in degraded mode
            Assert.That(response.CorrelationId, Is.EqualTo(correlationId),
                "CorrelationId must be preserved in degraded responses for traceability");
            Assert.That(response.IsDegraded, Is.True);
        }

        [Test]
        public async Task DegradedMode_RemediationHints_PresentInDegradedHealthAssessment()
        {
            // Arrange: health evaluator fails
            var faultService = new FaultInjectableTokenOperationsIntelligenceService(
                _cache, CreateMetricsService(), CreateLogger(),
                failHealth: true);

            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 6666666,
                Network = "voimain-v1.0"
            };

            // Act
            var response = await faultService.GetOperationsIntelligenceAsync(request);

            // Assert: degraded health assessment has a description (not null/empty)
            Assert.That(response.Health, Is.Not.Null);
            Assert.That(response.Health!.PolicyResults, Is.Not.Null.And.Not.Empty,
                "Degraded health assessment must include at least one result explaining the degradation");
            var degradedResult = response.Health.PolicyResults.First();
            Assert.That(degradedResult.Description, Is.Not.Null.And.Not.Empty,
                "Degraded health result must have a description");
        }

        // ============================================================
        // Helpers for fault-injection tests
        // ============================================================

        private static BiatecTokensApi.Services.MetricsService CreateMetricsService()
        {
            var metricsLoggerMock = new Mock<ILogger<BiatecTokensApi.Services.MetricsService>>();
            return new BiatecTokensApi.Services.MetricsService(metricsLoggerMock.Object);
        }

        private static ILogger<TokenOperationsIntelligenceService> CreateLogger()
        {
            return new Mock<ILogger<TokenOperationsIntelligenceService>>().Object;
        }
    }

    /// <summary>
    /// A fault-injectable subclass of TokenOperationsIntelligenceService used in E2E tests
    /// to simulate upstream source failures and verify degraded-mode behavior.
    /// Each source (health, recommendations, events) can be independently configured to throw.
    /// </summary>
    internal sealed class FaultInjectableTokenOperationsIntelligenceService : TokenOperationsIntelligenceService
    {
        private readonly bool _failHealth;
        private readonly bool _failRecommendations;
        private readonly bool _failEvents;

        public FaultInjectableTokenOperationsIntelligenceService(
            IMemoryCache cache,
            BiatecTokensApi.Services.Interface.IMetricsService metricsService,
            ILogger<TokenOperationsIntelligenceService> logger,
            bool failHealth = false,
            bool failRecommendations = false,
            bool failEvents = false)
            : base(cache, metricsService, logger)
        {
            _failHealth = failHealth;
            _failRecommendations = failRecommendations;
            _failEvents = failEvents;
        }

        public override Task<TokenHealthAssessment> EvaluateHealthAsync(
            ulong assetId,
            string network,
            IEnumerable<string>? dimensions = null,
            BiatecTokensApi.Models.TokenOperationsIntelligence.TokenStateInputs? stateInputs = null)
        {
            if (_failHealth)
                throw new InvalidOperationException("Simulated HealthEvaluator upstream failure");
            return base.EvaluateHealthAsync(assetId, network, dimensions, stateInputs);
        }

        public override Task<List<BiatecTokensApi.Models.TokenOperationsIntelligence.LifecycleRecommendation>> GetRecommendationsAsync(
            ulong assetId, string network)
        {
            if (_failRecommendations)
                throw new InvalidOperationException("Simulated RecommendationEngine upstream failure");
            return base.GetRecommendationsAsync(assetId, network);
        }

        public override Task<List<BiatecTokensApi.Models.TokenOperationsIntelligence.NormalizedTokenEvent>> GetNormalizedEventsAsync(
            ulong assetId, string network, int maxEvents = 10, bool includeDetails = false)
        {
            if (_failEvents)
                throw new InvalidOperationException("Simulated EventNormalizer upstream failure");
            return base.GetNormalizedEventsAsync(assetId, network, maxEvents, includeDetails);
        }
    }
}
