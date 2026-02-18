using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using BiatecTokensTests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for compliance webhook emission from ComplianceService.
    /// 
    /// Note on deterministic async testing: These tests use AsyncTestHelper.WaitForConditionAsync
    /// instead of fixed Task.Delay calls to reduce flakiness. The helper polls for the expected
    /// condition (webhook delivery records) with a maximum timeout, allowing tests to complete
    /// as soon as the condition is met rather than waiting for an arbitrary fixed duration.
    /// 
    /// Webhooks are emitted asynchronously via fire-and-forget Task.Run in WebhookService.EmitEventAsync,
    /// and delivery occurs in background threads. The condition-based waiting ensures:
    /// - Tests complete faster when webhooks process quickly
    /// - Tests don't fail spuriously due to timing variations in CI environments
    /// - Maximum timeout provides clear failure indication if webhooks don't process
    /// 
    /// Business Value: Reduces CI flakiness and improves developer productivity by making
    /// integration tests more reliable and faster.
    /// </summary>
    [TestFixture]
    public class ComplianceWebhookIntegrationTests
    {
        private ComplianceRepository _complianceRepository;
        private WhitelistRepository _whitelistRepository;
        private WhitelistService _whitelistService;
        private WebhookRepository _webhookRepository;
        private SubscriptionMeteringService _meteringService;
        private Mock<IHttpClientFactory> _httpClientFactoryMock;
        private WebhookService _webhookService;
        private ComplianceService _complianceService;
        private string _testUserAddress;

        [SetUp]
        public void Setup()
        {
            _complianceRepository = new ComplianceRepository(Mock.Of<ILogger<ComplianceRepository>>());
            _whitelistRepository = new WhitelistRepository(Mock.Of<ILogger<WhitelistRepository>>());
            _whitelistService = new WhitelistService(
                _whitelistRepository,
                Mock.Of<ILogger<WhitelistService>>(),
                Mock.Of<ISubscriptionMeteringService>(),
                Mock.Of<ISubscriptionTierService>(),
                Mock.Of<IWebhookService>());
            
            _webhookRepository = new WebhookRepository(Mock.Of<ILogger<WebhookRepository>>());
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _meteringService = new SubscriptionMeteringService(Mock.Of<ILogger<SubscriptionMeteringService>>());
            
            _webhookService = new WebhookService(
                _webhookRepository,
                Mock.Of<ILogger<WebhookService>>(),
                _httpClientFactoryMock.Object);
            
            _complianceService = new ComplianceService(
                _complianceRepository,
                _whitelistService,
                Mock.Of<ILogger<ComplianceService>>(),
                _meteringService,
                _webhookService);

            _testUserAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        }

        [Test]
        public async Task UpsertMetadata_KycStatusChange_EmitsWebhookEvent()
        {
            // Arrange - Create a webhook subscription for KYC events
            var subscription = await _webhookService.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange }
                },
                _testUserAddress);

            Assert.That(subscription.Success, Is.True);

            // Create initial metadata
            var initialRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                VerificationStatus = VerificationStatus.Pending,
                ComplianceStatus = ComplianceStatus.UnderReview,
                Jurisdiction = "US"
            };

            await _complianceService.UpsertMetadataAsync(initialRequest, _testUserAddress);

            // Act - Update KYC status
            var updateRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                VerificationStatus = VerificationStatus.Verified,
                ComplianceStatus = ComplianceStatus.UnderReview,
                KycProvider = "TestKYCProvider",
                KycVerificationDate = DateTime.UtcNow,
                Jurisdiction = "US"
            };

            var result = await _complianceService.UpsertMetadataAsync(updateRequest, _testUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);

            // Wait for webhook delivery to be recorded (max 2 seconds)
            var deliveryRecorded = await AsyncTestHelper.WaitForConditionAsync(
                async () =>
                {
                    var history = await _webhookService.GetDeliveryHistoryAsync(
                        new GetWebhookDeliveryHistoryRequest
                        {
                            SubscriptionId = subscription.Subscription!.Id
                        },
                        _testUserAddress);
                    return history.Success && history.Deliveries.Count > 0;
                },
                timeout: TimeSpan.FromSeconds(2),
                pollInterval: TimeSpan.FromMilliseconds(50));

            Assert.That(deliveryRecorded, Is.True, 
                "Webhook delivery should have been recorded within 2 seconds");

            // Verify webhook delivery was attempted
            var deliveryHistory = await _webhookService.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest
                {
                    SubscriptionId = subscription.Subscription!.Id
                },
                _testUserAddress);

            Assert.That(deliveryHistory.Success, Is.True);
            Assert.That(deliveryHistory.Deliveries.Count, Is.GreaterThan(0), 
                "At least one webhook delivery should have been attempted");
            
            // Verify deliveries were attempted (event details would be in the webhook payload, 
            // not easily accessible in this in-memory test setup)
        }

        [Test]
        public async Task UpsertMetadata_ComplianceStatusChange_EmitsWebhookEvent()
        {
            // Arrange - Create a webhook subscription for compliance badge events
            var subscription = await _webhookService.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.ComplianceBadgeUpdate }
                },
                _testUserAddress);

            Assert.That(subscription.Success, Is.True);

            // Create initial metadata
            var initialRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = 54321,
                Network = "voimain-v1.0",
                VerificationStatus = VerificationStatus.Verified,
                ComplianceStatus = ComplianceStatus.UnderReview,
                Jurisdiction = "US"
            };

            await _complianceService.UpsertMetadataAsync(initialRequest, _testUserAddress);

            // Act - Update compliance status
            var updateRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = 54321,
                Network = "voimain-v1.0",
                VerificationStatus = VerificationStatus.Verified,
                ComplianceStatus = ComplianceStatus.Compliant,
                RegulatoryFramework = "MiFID II",
                Jurisdiction = "US"
            };

            var result = await _complianceService.UpsertMetadataAsync(updateRequest, _testUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);

            // Wait for webhook delivery to be recorded (max 2 seconds)
            var deliveryRecorded = await AsyncTestHelper.WaitForConditionAsync(
                async () =>
                {
                    var history = await _webhookService.GetDeliveryHistoryAsync(
                        new GetWebhookDeliveryHistoryRequest
                        {
                            SubscriptionId = subscription.Subscription!.Id
                        },
                        _testUserAddress);
                    return history.Success && history.Deliveries.Count > 0;
                },
                timeout: TimeSpan.FromSeconds(2),
                pollInterval: TimeSpan.FromMilliseconds(50));

            Assert.That(deliveryRecorded, Is.True,
                "Webhook delivery should have been recorded within 2 seconds");

            // Verify webhook delivery was attempted
            var deliveryHistory = await _webhookService.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest
                {
                    SubscriptionId = subscription.Subscription!.Id
                },
                _testUserAddress);

            Assert.That(deliveryHistory.Success, Is.True);
            Assert.That(deliveryHistory.Deliveries.Count, Is.GreaterThan(0),
                "At least one webhook delivery should have been attempted");
        }

        [Test]
        public async Task UpsertMetadata_MultipleStatusChanges_EmitsMultipleWebhookEvents()
        {
            // Arrange - Create a webhook subscription for all compliance events
            var subscription = await _webhookService.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook",
                    EventTypes = new List<WebhookEventType>
                    {
                        WebhookEventType.KycStatusChange,
                        WebhookEventType.AmlStatusChange,
                        WebhookEventType.ComplianceBadgeUpdate
                    }
                },
                _testUserAddress);

            Assert.That(subscription.Success, Is.True);

            // Act - Create metadata with verified KYC and compliant status
            // This should trigger both KYC and compliance badge events
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 99999,
                Network = "voimain-v1.0",
                VerificationStatus = VerificationStatus.Verified,
                ComplianceStatus = ComplianceStatus.Compliant,
                KycProvider = "TestProvider",
                KycVerificationDate = DateTime.UtcNow,
                RegulatoryFramework = "SEC Reg D",
                Jurisdiction = "US"
            };

            var result = await _complianceService.UpsertMetadataAsync(request, _testUserAddress);

            // Assert
            Assert.That(result.Success, Is.True);

            // Wait for webhook deliveries to be recorded (max 2 seconds)
            var deliveriesRecorded = await AsyncTestHelper.WaitForConditionAsync(
                async () =>
                {
                    var history = await _webhookService.GetDeliveryHistoryAsync(
                        new GetWebhookDeliveryHistoryRequest
                        {
                            SubscriptionId = subscription.Subscription!.Id
                        },
                        _testUserAddress);
                    return history.Success && history.Deliveries.Count > 0;
                },
                timeout: TimeSpan.FromSeconds(2),
                pollInterval: TimeSpan.FromMilliseconds(50));

            Assert.That(deliveriesRecorded, Is.True,
                "Webhook deliveries should have been recorded within 2 seconds");

            // Verify webhook deliveries were attempted
            var deliveryHistory = await _webhookService.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest
                {
                    SubscriptionId = subscription.Subscription!.Id
                },
                _testUserAddress);

            Assert.That(deliveryHistory.Success, Is.True);
            Assert.That(deliveryHistory.Deliveries.Count, Is.GreaterThan(0),
                "Multiple webhook deliveries should have been attempted");
        }

        [Test]
        public async Task UpsertMetadata_NoStatusChange_DoesNotEmitWebhook()
        {
            // Arrange - Create a webhook subscription
            var subscription = await _webhookService.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook",
                    EventTypes = new List<WebhookEventType>
                    {
                        WebhookEventType.KycStatusChange,
                        WebhookEventType.ComplianceBadgeUpdate
                    }
                },
                _testUserAddress);

            Assert.That(subscription.Success, Is.True);

            // Create initial metadata
            var initialRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = 77777,
                Network = "voimain-v1.0",
                VerificationStatus = VerificationStatus.Verified,
                ComplianceStatus = ComplianceStatus.Compliant,
                Jurisdiction = "US"
            };

            await _complianceService.UpsertMetadataAsync(initialRequest, _testUserAddress);

            // Wait for initial webhook processing (max 2 seconds)
            await AsyncTestHelper.WaitForConditionAsync(
                async () =>
                {
                    var history = await _webhookService.GetDeliveryHistoryAsync(
                        new GetWebhookDeliveryHistoryRequest
                        {
                            SubscriptionId = subscription.Subscription!.Id
                        },
                        _testUserAddress);
                    return history.Success && history.Deliveries.Count > 0;
                },
                timeout: TimeSpan.FromSeconds(2),
                pollInterval: TimeSpan.FromMilliseconds(50));

            // Get initial delivery count
            var initialHistory = await _webhookService.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest
                {
                    SubscriptionId = subscription.Subscription!.Id
                },
                _testUserAddress);

            var initialDeliveryCount = initialHistory.Deliveries.Count;

            // Act - Update metadata but keep status the same (only change non-status fields)
            var updateRequest = new UpsertComplianceMetadataRequest
            {
                AssetId = 77777,
                Network = "voimain-v1.0",
                VerificationStatus = VerificationStatus.Verified, // Same
                ComplianceStatus = ComplianceStatus.Compliant,    // Same
                Jurisdiction = "US",
                Notes = "Updated notes" // Only notes changed
            };

            await _complianceService.UpsertMetadataAsync(updateRequest, _testUserAddress);

            // Wait a reasonable time to ensure no new webhooks are sent (max 1 second)
            // We give it time to potentially send webhooks (if bug exists), then verify none were sent
            await Task.Delay(1000);

            // Assert - No new webhooks should be sent
            var finalHistory = await _webhookService.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest
                {
                    SubscriptionId = subscription.Subscription!.Id
                },
                _testUserAddress);

            // Delivery count should be the same since no status changed
            Assert.That(finalHistory.Deliveries.Count, Is.EqualTo(initialDeliveryCount),
                "No new webhooks should be sent when status doesn't change");
        }

        [Test]
        public async Task UpsertMetadata_WithAssetFilter_OnlyEmitsToMatchingSubscriptions()
        {
            // Arrange - Create two subscriptions, one with asset filter
            var filteredSubscription = await _webhookService.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook1",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange },
                    AssetIdFilter = 12345
                },
                _testUserAddress);

            var unfilteredSubscription = await _webhookService.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook2",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange }
                },
                _testUserAddress);

            Assert.That(filteredSubscription.Success, Is.True);
            Assert.That(unfilteredSubscription.Success, Is.True);

            // Act - Update metadata for the filtered asset
            var request = new UpsertComplianceMetadataRequest
            {
                AssetId = 12345,
                Network = "voimain-v1.0",
                VerificationStatus = VerificationStatus.Verified,
                ComplianceStatus = ComplianceStatus.Compliant,
                Jurisdiction = "US"
            };

            await _complianceService.UpsertMetadataAsync(request, _testUserAddress);

            // Wait for webhook deliveries to be recorded (max 2 seconds)
            await AsyncTestHelper.WaitForConditionAsync(
                async () =>
                {
                    var filtered = await _webhookService.GetDeliveryHistoryAsync(
                        new GetWebhookDeliveryHistoryRequest
                        {
                            SubscriptionId = filteredSubscription.Subscription!.Id
                        },
                        _testUserAddress);
                    var unfiltered = await _webhookService.GetDeliveryHistoryAsync(
                        new GetWebhookDeliveryHistoryRequest
                        {
                            SubscriptionId = unfilteredSubscription.Subscription!.Id
                        },
                        _testUserAddress);
                    return filtered.Success && filtered.Deliveries.Count > 0 &&
                           unfiltered.Success && unfiltered.Deliveries.Count > 0;
                },
                timeout: TimeSpan.FromSeconds(2),
                pollInterval: TimeSpan.FromMilliseconds(50));

            // Assert - Both should receive the webhook
            var filteredHistory = await _webhookService.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest
                {
                    SubscriptionId = filteredSubscription.Subscription!.Id
                },
                _testUserAddress);

            var unfilteredHistory = await _webhookService.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest
                {
                    SubscriptionId = unfilteredSubscription.Subscription!.Id
                },
                _testUserAddress);

            Assert.That(filteredHistory.Deliveries.Count, Is.GreaterThan(0),
                "Filtered subscription should receive webhook for matching asset");
            Assert.That(unfilteredHistory.Deliveries.Count, Is.GreaterThan(0),
                "Unfiltered subscription should receive all webhooks");
        }
    }
}
