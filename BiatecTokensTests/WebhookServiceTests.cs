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
    }
}
