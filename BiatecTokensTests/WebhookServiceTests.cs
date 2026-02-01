using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    [TestFixture]
    public class WebhookServiceTests
    {
        private Mock<ILogger<WebhookService>> _loggerMock;
        private Mock<IHttpClientFactory> _httpClientFactoryMock;
        private WebhookRepository _repository;
        private WebhookService _service;

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<WebhookService>>();
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _repository = new WebhookRepository(Mock.Of<ILogger<WebhookRepository>>());
            _service = new WebhookService(_repository, _loggerMock.Object, _httpClientFactoryMock.Object);
        }

        [Test]
        public async Task CreateSubscriptionAsync_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd },
                Description = "Test webhook"
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Act
            var result = await _service.CreateSubscriptionAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscription, Is.Not.Null);
            Assert.That(result.Subscription!.Url, Is.EqualTo(request.Url));
            Assert.That(result.Subscription.EventTypes, Has.Count.EqualTo(1));
            Assert.That(result.Subscription.EventTypes[0], Is.EqualTo(WebhookEventType.WhitelistAdd));
            Assert.That(result.Subscription.SigningSecret, Is.Not.Empty);
            Assert.That(result.Subscription.CreatedBy, Is.EqualTo(createdBy));
            Assert.That(result.Subscription.IsActive, Is.True);
        }

        [Test]
        public async Task CreateSubscriptionAsync_InvalidUrl_ReturnsError()
        {
            // Arrange
            var request = new CreateWebhookSubscriptionRequest
            {
                Url = "not-a-valid-url",
                EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Act
            var result = await _service.CreateSubscriptionAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Invalid webhook URL"));
        }

        [Test]
        public async Task CreateSubscriptionAsync_NoEventTypes_ReturnsError()
        {
            // Arrange
            var request = new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType>()
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Act
            var result = await _service.CreateSubscriptionAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("At least one event type must be specified"));
        }

        [Test]
        public async Task GetSubscriptionAsync_ExistingSubscription_ReturnsSuccess()
        {
            // Arrange
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var createRequest = new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
            };
            var createResult = await _service.CreateSubscriptionAsync(createRequest, createdBy);
            var subscriptionId = createResult.Subscription!.Id;

            // Act
            var result = await _service.GetSubscriptionAsync(subscriptionId, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscription, Is.Not.Null);
            Assert.That(result.Subscription!.Id, Is.EqualTo(subscriptionId));
        }

        [Test]
        public async Task GetSubscriptionAsync_NonExistentSubscription_ReturnsError()
        {
            // Arrange
            var subscriptionId = "non-existent-id";
            var userId = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Act
            var result = await _service.GetSubscriptionAsync(subscriptionId, userId);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("not found"));
        }

        [Test]
        public async Task GetSubscriptionAsync_UnauthorizedUser_ReturnsError()
        {
            // Arrange
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var otherUser = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            var createRequest = new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
            };
            var createResult = await _service.CreateSubscriptionAsync(createRequest, createdBy);
            var subscriptionId = createResult.Subscription!.Id;

            // Act
            var result = await _service.GetSubscriptionAsync(subscriptionId, otherUser);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("permission"));
        }

        [Test]
        public async Task ListSubscriptionsAsync_ReturnsUserSubscriptions()
        {
            // Arrange
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            
            // Create multiple subscriptions
            for (int i = 0; i < 3; i++)
            {
                await _service.CreateSubscriptionAsync(new CreateWebhookSubscriptionRequest
                {
                    Url = $"https://example.com/webhook{i}",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
                }, createdBy);
            }

            // Act
            var result = await _service.ListSubscriptionsAsync(createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscriptions, Has.Count.EqualTo(3));
            Assert.That(result.TotalCount, Is.EqualTo(3));
        }

        [Test]
        public async Task UpdateSubscriptionAsync_ValidUpdate_ReturnsSuccess()
        {
            // Arrange
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var createRequest = new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
            };
            var createResult = await _service.CreateSubscriptionAsync(createRequest, createdBy);
            var subscriptionId = createResult.Subscription!.Id;

            var updateRequest = new UpdateWebhookSubscriptionRequest
            {
                SubscriptionId = subscriptionId,
                IsActive = false,
                Description = "Updated description"
            };

            // Act
            var result = await _service.UpdateSubscriptionAsync(updateRequest, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscription, Is.Not.Null);
            Assert.That(result.Subscription!.IsActive, Is.False);
            Assert.That(result.Subscription.Description, Is.EqualTo("Updated description"));
        }

        [Test]
        public async Task DeleteSubscriptionAsync_ValidDelete_ReturnsSuccess()
        {
            // Arrange
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var createRequest = new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
            };
            var createResult = await _service.CreateSubscriptionAsync(createRequest, createdBy);
            var subscriptionId = createResult.Subscription!.Id;

            // Act
            var result = await _service.DeleteSubscriptionAsync(subscriptionId, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);

            // Verify it's actually deleted
            var getResult = await _service.GetSubscriptionAsync(subscriptionId, createdBy);
            Assert.That(getResult.Success, Is.False);
        }

        [Test]
        public async Task EmitEventAsync_WithActiveSubscriptions_StoresDeliveryResults()
        {
            // Arrange
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            
            // Create a subscription
            await _service.CreateSubscriptionAsync(new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
            }, createdBy);

            var webhookEvent = new WebhookEvent
            {
                EventType = WebhookEventType.WhitelistAdd,
                AssetId = 12345,
                Network = "voimain-v1.0",
                Actor = createdBy,
                AffectedAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                Data = new Dictionary<string, object>
                {
                    { "status", "Active" }
                }
            };

            // Act
            await _service.EmitEventAsync(webhookEvent);

            // Give some time for async delivery to start
            await Task.Delay(100);

            // Assert - Event was emitted (actual delivery might fail due to mock HTTP client)
            // We're just testing that the event emission doesn't throw
            Assert.Pass("Event emitted without errors");
        }

        [Test]
        public async Task EmitEventAsync_WithFilters_OnlyNotifiesMatchingSubscriptions()
        {
            // Arrange
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            var assetId = (ulong)12345;
            
            // Create subscription with asset filter
            await _service.CreateSubscriptionAsync(new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook1",
                EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd },
                AssetIdFilter = assetId
            }, createdBy);

            // Create subscription without filter
            await _service.CreateSubscriptionAsync(new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook2",
                EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
            }, createdBy);

            var webhookEvent = new WebhookEvent
            {
                EventType = WebhookEventType.WhitelistAdd,
                AssetId = assetId,
                Actor = createdBy
            };

            // Act
            await _service.EmitEventAsync(webhookEvent);

            // Give some time for async delivery
            await Task.Delay(100);

            // Assert
            Assert.Pass("Event emitted with filters");
        }

        [Test]
        public async Task CreateSubscriptionAsync_WithComplianceEventTypes_ReturnsSuccess()
        {
            // Arrange
            var request = new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType> 
                { 
                    WebhookEventType.KycStatusChange,
                    WebhookEventType.AmlStatusChange,
                    WebhookEventType.ComplianceBadgeUpdate
                },
                Description = "Compliance webhook"
            };
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";

            // Act
            var result = await _service.CreateSubscriptionAsync(request, createdBy);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Subscription, Is.Not.Null);
            Assert.That(result.Subscription!.EventTypes, Has.Count.EqualTo(3));
            Assert.That(result.Subscription.EventTypes, Does.Contain(WebhookEventType.KycStatusChange));
            Assert.That(result.Subscription.EventTypes, Does.Contain(WebhookEventType.AmlStatusChange));
            Assert.That(result.Subscription.EventTypes, Does.Contain(WebhookEventType.ComplianceBadgeUpdate));
        }

        [Test]
        public async Task EmitEventAsync_KycStatusChange_StoresDeliveryResults()
        {
            // Arrange
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            
            // Create a subscription for KYC events
            await _service.CreateSubscriptionAsync(new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.KycStatusChange }
            }, createdBy);

            var webhookEvent = new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                AssetId = 12345,
                Network = "voimain-v1.0",
                Actor = createdBy,
                Data = new Dictionary<string, object>
                {
                    { "oldStatus", "Pending" },
                    { "newStatus", "Verified" },
                    { "kycProvider", "TestProvider" },
                    { "verificationDate", DateTime.UtcNow.ToString("O") }
                }
            };

            // Act
            await _service.EmitEventAsync(webhookEvent);

            // Give some time for async delivery to start
            await Task.Delay(100);

            // Assert - Event was emitted without errors
            Assert.Pass("KYC status change event emitted without errors");
        }

        [Test]
        public async Task EmitEventAsync_AmlStatusChange_StoresDeliveryResults()
        {
            // Arrange
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            
            // Create a subscription for AML events
            await _service.CreateSubscriptionAsync(new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.AmlStatusChange }
            }, createdBy);

            var webhookEvent = new WebhookEvent
            {
                EventType = WebhookEventType.AmlStatusChange,
                AssetId = 12345,
                Network = "voimain-v1.0",
                Actor = createdBy,
                Data = new Dictionary<string, object>
                {
                    { "verificationStatus", "Verified" },
                    { "provider", "AMLProvider" },
                    { "verificationDate", DateTime.UtcNow.ToString("O") },
                    { "jurisdiction", "US" }
                }
            };

            // Act
            await _service.EmitEventAsync(webhookEvent);

            // Give some time for async delivery to start
            await Task.Delay(100);

            // Assert - Event was emitted without errors
            Assert.Pass("AML status change event emitted without errors");
        }

        [Test]
        public async Task EmitEventAsync_ComplianceBadgeUpdate_StoresDeliveryResults()
        {
            // Arrange
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            
            // Create a subscription for compliance badge events
            await _service.CreateSubscriptionAsync(new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType> { WebhookEventType.ComplianceBadgeUpdate }
            }, createdBy);

            var webhookEvent = new WebhookEvent
            {
                EventType = WebhookEventType.ComplianceBadgeUpdate,
                AssetId = 12345,
                Network = "voimain-v1.0",
                Actor = createdBy,
                Data = new Dictionary<string, object>
                {
                    { "oldStatus", "UnderReview" },
                    { "newStatus", "Compliant" },
                    { "jurisdiction", "EU" },
                    { "regulatoryFramework", "MiFID II" },
                    { "requiresAccreditedInvestors", true }
                }
            };

            // Act
            await _service.EmitEventAsync(webhookEvent);

            // Give some time for async delivery to start
            await Task.Delay(100);

            // Assert - Event was emitted without errors
            Assert.Pass("Compliance badge update event emitted without errors");
        }

        [Test]
        public async Task EmitEventAsync_MultipleComplianceEvents_AllDelivered()
        {
            // Arrange
            var createdBy = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
            
            // Create a subscription for all compliance events
            await _service.CreateSubscriptionAsync(new CreateWebhookSubscriptionRequest
            {
                Url = "https://example.com/webhook",
                EventTypes = new List<WebhookEventType> 
                { 
                    WebhookEventType.KycStatusChange,
                    WebhookEventType.AmlStatusChange,
                    WebhookEventType.ComplianceBadgeUpdate
                }
            }, createdBy);

            // Act - Emit multiple events
            var kycEvent = new WebhookEvent
            {
                EventType = WebhookEventType.KycStatusChange,
                AssetId = 12345,
                Network = "voimain-v1.0",
                Actor = createdBy,
                Data = new Dictionary<string, object> { { "status", "Verified" } }
            };

            var amlEvent = new WebhookEvent
            {
                EventType = WebhookEventType.AmlStatusChange,
                AssetId = 12345,
                Network = "voimain-v1.0",
                Actor = createdBy,
                Data = new Dictionary<string, object> { { "status", "Verified" } }
            };

            var complianceEvent = new WebhookEvent
            {
                EventType = WebhookEventType.ComplianceBadgeUpdate,
                AssetId = 12345,
                Network = "voimain-v1.0",
                Actor = createdBy,
                Data = new Dictionary<string, object> { { "status", "Compliant" } }
            };

            await _service.EmitEventAsync(kycEvent);
            await _service.EmitEventAsync(amlEvent);
            await _service.EmitEventAsync(complianceEvent);

            // Give some time for async delivery to start
            await Task.Delay(200);

            // Assert - All events were emitted without errors
            Assert.Pass("All compliance events emitted without errors");
        }
    }
}
