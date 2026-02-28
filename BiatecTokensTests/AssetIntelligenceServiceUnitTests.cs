using BiatecTokensApi.Models.AssetIntelligence;
using BiatecTokensApi.Models.PricingReliability;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    [TestFixture]
    public class AssetIntelligenceServiceUnitTests
    {
        private AssetIntelligenceService _service = null!;
        private IMemoryCache _cache = null!;
        private MetricsService _metricsService = null!;

        [SetUp]
        public void Setup()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            var metricsLogger = new Mock<ILogger<MetricsService>>();
            _metricsService = new MetricsService(metricsLogger.Object);
            var logger = new Mock<ILogger<AssetIntelligenceService>>();
            _service = new AssetIntelligenceService(_cache, _metricsService, logger.Object);
        }

        [TearDown]
        public void TearDown() => _cache.Dispose();

        [Test]
        public async Task ValidRequest_ReturnsSuccess()
        {
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task ValidRequest_ReturnsNonEmptyNormalizedFields()
        {
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.NormalizedFields, Is.Not.Empty);
        }

        [Test]
        public async Task ZeroAssetId_ReturnsErrorWithUnsupportedAssetCode()
        {
            var request = new AssetIntelligenceRequest { AssetId = 0, Network = "voimain-v1.0" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(AssetIntelligenceErrorCode.UnsupportedAsset));
        }

        [Test]
        public async Task EmptyNetwork_ReturnsErrorWithChainMismatchCode()
        {
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = "" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(AssetIntelligenceErrorCode.ChainMismatch));
        }

        [Test]
        public async Task NullNetwork_ReturnsError()
        {
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = null! };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task ValidRequest_IncludesCorrelationIdInResponse()
        {
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ValidRequest_ReturnsCompletenessScoreGreaterThanZero()
        {
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.CompletenessScore, Is.GreaterThan(0));
        }

        [Test]
        public async Task RepeatedSameRequest_ReturnsSameValidationStatus()
        {
            var request = new AssetIntelligenceRequest { AssetId = 99999, Network = "voimain-v1.0" };
            var r1 = await _service.GetAssetIntelligenceAsync(request);
            var r2 = await _service.GetAssetIntelligenceAsync(request);
            var r3 = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(r1.ValidationStatus, Is.EqualTo(r2.ValidationStatus));
            Assert.That(r2.ValidationStatus, Is.EqualTo(r3.ValidationStatus));
        }

        [Test]
        public async Task ValidateMetadataAsync_WithValidFields_ReturnsNoErrorResults()
        {
            var fields = new Dictionary<string, object?> { ["Name"] = "TestToken", ["Symbol"] = "TST" };
            var result = await _service.ValidateMetadataAsync(12345, "voimain-v1.0", fields);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.All(d => d.Status == AssetValidationStatus.Valid), Is.True);
        }

        [Test]
        public async Task ValidateMetadataAsync_WithNullFields_ReturnsValidationDetails()
        {
            var result = await _service.ValidateMetadataAsync(12345, "voimain-v1.0", new Dictionary<string, object?>());
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
            Assert.That(result[0].Status, Is.EqualTo(AssetValidationStatus.Invalid));
        }

        [Test]
        public async Task GetQualityIndicatorsAsync_ReturnsNonNull()
        {
            var result = await _service.GetQualityIndicatorsAsync(12345, "voimain-v1.0");
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public async Task GetQualityIndicatorsAsync_SourceConfidenceIsNotUnverifiedForValidAsset()
        {
            var result = await _service.GetQualityIndicatorsAsync(12345, "voimain-v1.0");
            Assert.That(result.SourceConfidence, Is.Not.EqualTo(SourceConfidenceLevel.Unverified));
        }

        [Test]
        public async Task IncludeProvenance_ReturnsNonEmptyProvenance()
        {
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = "voimain-v1.0", IncludeProvenance = true };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.Provenance, Is.Not.Empty);
        }

        [Test]
        public async Task Response_HasNonDefaultGeneratedAt()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.GeneratedAt, Is.GreaterThan(before));
        }

        [Test]
        public async Task SuccessResponse_HasErrorCodeNone()
        {
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(AssetIntelligenceErrorCode.None));
        }

        [Test]
        public async Task ThreeRunsSameRequest_ReturnsSameAssetId()
        {
            var request = new AssetIntelligenceRequest { AssetId = 77777, Network = "voimain-v1.0" };
            var r1 = await _service.GetAssetIntelligenceAsync(request);
            var r2 = await _service.GetAssetIntelligenceAsync(request);
            var r3 = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(r1.AssetId, Is.EqualTo(77777UL));
            Assert.That(r2.AssetId, Is.EqualTo(77777UL));
            Assert.That(r3.AssetId, Is.EqualTo(77777UL));
        }

        [Test]
        public async Task ErrorResponse_HasNonEmptyRemediationHint()
        {
            var request = new AssetIntelligenceRequest { AssetId = 0, Network = "voimain-v1.0" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ValidRequest_ReturnsAtLeastThreeNormalizedFields()
        {
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.NormalizedFields.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public async Task AssetId_IsEchoedBackInResponse()
        {
            var request = new AssetIntelligenceRequest { AssetId = 54321, Network = "voimain-v1.0" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.AssetId, Is.EqualTo(54321UL));
        }

        [Test]
        public async Task Network_IsEchoedBackInResponse()
        {
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.Network, Is.EqualTo("voimain-v1.0"));
        }

        [Test]
        public async Task ProvidedCorrelationId_IsUsedInResponse()
        {
            var correlationId = "my-trace-id-123";
            var request = new AssetIntelligenceRequest { AssetId = 12345, Network = "voimain-v1.0", CorrelationId = correlationId };
            var result = await _service.GetAssetIntelligenceAsync(request);
            Assert.That(result.CorrelationId, Is.EqualTo(correlationId));
        }
    }

    [TestFixture]
    public class PricingReliabilityServiceUnitTests
    {
        private PricingReliabilityService _service = null!;
        private IMemoryCache _cache = null!;
        private MetricsService _metricsService = null!;

        [SetUp]
        public void Setup()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
            var metricsLogger = new Mock<ILogger<MetricsService>>();
            _metricsService = new MetricsService(metricsLogger.Object);
            var logger = new Mock<ILogger<PricingReliabilityService>>();
            _service = new PricingReliabilityService(_cache, _metricsService, logger.Object);
        }

        [TearDown]
        public void TearDown() => _cache.Dispose();

        [Test]
        public async Task ValidRequest_ReturnsSuccess()
        {
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task ZeroAssetId_ReturnsErrorWithUnsupportedAssetCode()
        {
            var request = new PricingReliabilityRequest { AssetId = 0, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(PricingErrorCode.UnsupportedAsset));
        }

        [Test]
        public async Task EmptyNetwork_ReturnsErrorWithChainMismatchCode()
        {
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = "" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(PricingErrorCode.ChainMismatch));
        }

        [Test]
        public async Task NullNetwork_ReturnsError()
        {
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = null! };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task ValidRequest_ReturnsPriceGreaterThanZero()
        {
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.Price, Is.GreaterThan(0));
        }

        [Test]
        public async Task RepeatedSameRequest_ReturnsSamePrice()
        {
            var request = new PricingReliabilityRequest { AssetId = 99999, Network = "voimain-v1.0" };
            var r1 = await _service.GetReliableQuoteAsync(request);
            var r2 = await _service.GetReliableQuoteAsync(request);
            Assert.That(r1.Price, Is.EqualTo(r2.Price));
        }

        [Test]
        public async Task Response_HasCorrelationId()
        {
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task SuccessResponse_HasSourceInfoWhenProvenanceRequested()
        {
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = "voimain-v1.0", IncludeProvenance = true };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.Success, Is.True);
            Assert.That(result.SourceInfo, Is.Not.Null);
        }

        [Test]
        public async Task Response_HasNonEmptyPrecedenceTrace()
        {
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.PrecedenceTrace, Is.Not.Empty);
        }

        [Test]
        public async Task ValidRequest_QuoteStatusIsSuccess()
        {
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.QuoteStatus, Is.EqualTo(QuoteStatus.Success));
        }

        [Test]
        public async Task GetSourceHealthAsync_ReturnsIsHealthyTrue()
        {
            var result = await _service.GetSourceHealthAsync();
            Assert.That(result.IsHealthy, Is.True);
        }

        [Test]
        public async Task GetSourceHealthAsync_ReturnsAvailableSourcesGreaterThanZero()
        {
            var result = await _service.GetSourceHealthAsync();
            Assert.That(result.AvailableSources, Is.GreaterThan(0));
        }

        [Test]
        public async Task IncludeFallbackChain_ReturnsTraceWithMoreThanOneEntry()
        {
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = "voimain-v1.0", IncludeFallbackChain = true };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.PrecedenceTrace.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task ThreeRunsSameRequest_ReturnsSamePrice()
        {
            var request = new PricingReliabilityRequest { AssetId = 88888, Network = "voimain-v1.0" };
            var r1 = await _service.GetReliableQuoteAsync(request);
            var r2 = await _service.GetReliableQuoteAsync(request);
            var r3 = await _service.GetReliableQuoteAsync(request);
            Assert.That(r1.Price, Is.EqualTo(r2.Price));
            Assert.That(r2.Price, Is.EqualTo(r3.Price));
        }

        [Test]
        public async Task AssetId_IsEchoedBackInResponse()
        {
            var request = new PricingReliabilityRequest { AssetId = 54321, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.AssetId, Is.EqualTo(54321UL));
        }

        [Test]
        public async Task Network_IsEchoedBackInResponse()
        {
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.Network, Is.EqualTo("voimain-v1.0"));
        }

        [Test]
        public async Task Response_HasGeneratedAtSet()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.GeneratedAt, Is.GreaterThan(before));
        }

        [Test]
        public async Task Response_HasLatencyMsNonNegative()
        {
            var request = new PricingReliabilityRequest { AssetId = 12345, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.LatencyMs, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task ErrorResponse_HasErrorCodeNotNone()
        {
            var request = new PricingReliabilityRequest { AssetId = 0, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.ErrorCode, Is.Not.EqualTo(PricingErrorCode.None));
        }

        [Test]
        public async Task ErrorResponse_HasNonEmptyRemediationHint()
        {
            var request = new PricingReliabilityRequest { AssetId = 0, Network = "voimain-v1.0" };
            var result = await _service.GetReliableQuoteAsync(request);
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }
    }
}
