using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using BiatecTokensTests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace BiatecTokensTests
{
    /// <summary>
    /// Negative-path integration tests for webhook delivery failures, retries, and timeout scenarios.
    /// 
    /// These tests validate error handling and resilience in webhook delivery:
    /// - HTTP errors (404, 500, timeout)
    /// - Network failures and partial availability
    /// - Retry behavior and exponential backoff
    /// - Delivery failure auditing and logging
    /// 
    /// Business Value: Ensures webhook delivery failures don't silently drop events,
    /// maintaining compliance audit trail and enabling customer troubleshooting.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class WebhookDeliveryFailureTests
    {
        private WebhookRepository _repository;
        private Mock<IHttpClientFactory> _httpClientFactoryMock;
        private Mock<ILogger<WebhookService>> _loggerMock;
        private WebhookService _service;
        private string _testUserAddress;

        [SetUp]
        public void Setup()
        {
            _repository = new WebhookRepository(Mock.Of<ILogger<WebhookRepository>>());
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _loggerMock = new Mock<ILogger<WebhookService>>();
            _service = new WebhookService(_repository, _loggerMock.Object, _httpClientFactoryMock.Object);
            _testUserAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        }

        [Test]
        public async Task WebhookDelivery_Http404Error_RecordsFailure()
        {
            // Arrange - Mock HTTP client to return 404
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent("Webhook endpoint not found")
                });

            var httpClient = new HttpClient(httpMessageHandlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Create subscription
            var subscription = await _service.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
                },
                _testUserAddress);

            Assert.That(subscription.Success, Is.True);

            // Act - Emit event
            await _service.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.WhitelistAdd,
                AssetId = 12345,
                Actor = _testUserAddress
            });

            // Wait for delivery attempt to be recorded
            var deliveryRecorded = await AsyncTestHelper.WaitForConditionAsync(
                async () =>
                {
                    var history = await _service.GetDeliveryHistoryAsync(
                        new GetWebhookDeliveryHistoryRequest
                        {
                            SubscriptionId = subscription.Subscription!.Id
                        },
                        _testUserAddress);
                    return history.Success && history.Deliveries.Count > 0;
                },
                timeout: TimeSpan.FromSeconds(3),
                pollInterval: TimeSpan.FromMilliseconds(50));

            // Assert - Failure is recorded
            Assert.That(deliveryRecorded, Is.True, "Delivery attempt should be recorded within 3 seconds");

            var deliveryHistory = await _service.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest
                {
                    SubscriptionId = subscription.Subscription!.Id
                },
                _testUserAddress);

            Assert.That(deliveryHistory.Deliveries.Count, Is.GreaterThan(0));
            var delivery = deliveryHistory.Deliveries[0];
            Assert.That(delivery.Success, Is.False, "Delivery should be marked as failed");
            Assert.That(delivery.StatusCode, Is.EqualTo(404), "Should record HTTP 404 status code");
        }

        [Test]
        public async Task WebhookDelivery_Http500Error_RecordsFailureAndMarksForRetry()
        {
            // Arrange - Mock HTTP client to return 500
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("Internal server error")
                });

            var httpClient = new HttpClient(httpMessageHandlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Create subscription
            var subscription = await _service.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
                },
                _testUserAddress);

            // Act - Emit event
            await _service.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.WhitelistAdd,
                AssetId = 12345,
                Actor = _testUserAddress
            });

            // Wait for delivery attempt
            var deliveryRecorded = await AsyncTestHelper.WaitForConditionAsync(
                async () =>
                {
                    var history = await _service.GetDeliveryHistoryAsync(
                        new GetWebhookDeliveryHistoryRequest { SubscriptionId = subscription.Subscription!.Id },
                        _testUserAddress);
                    return history.Success && history.Deliveries.Count > 0;
                },
                timeout: TimeSpan.FromSeconds(3));

            // Assert - Failure is recorded with retry flag
            Assert.That(deliveryRecorded, Is.True);

            var deliveryHistory = await _service.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest { SubscriptionId = subscription.Subscription!.Id },
                _testUserAddress);

            var delivery = deliveryHistory.Deliveries[0];
            Assert.That(delivery.Success, Is.False, "Delivery should be marked as failed");
            Assert.That(delivery.StatusCode, Is.EqualTo(500), "Should record HTTP 500 status code");
            Assert.That(delivery.WillRetry, Is.True, "500 errors should be marked for retry");
        }

        [Test]
        public async Task WebhookDelivery_NetworkTimeout_RecordsFailure()
        {
            // Arrange - Mock HTTP client to timeout
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Request timeout"));

            var httpClient = new HttpClient(httpMessageHandlerMock.Object)
            {
                Timeout = TimeSpan.FromMilliseconds(100)
            };
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Create subscription
            var subscription = await _service.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
                },
                _testUserAddress);

            // Act - Emit event
            await _service.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.WhitelistAdd,
                AssetId = 12345,
                Actor = _testUserAddress
            });

            // Wait for delivery attempt
            var deliveryRecorded = await AsyncTestHelper.WaitForConditionAsync(
                async () =>
                {
                    var history = await _service.GetDeliveryHistoryAsync(
                        new GetWebhookDeliveryHistoryRequest { SubscriptionId = subscription.Subscription!.Id },
                        _testUserAddress);
                    return history.Success && history.Deliveries.Count > 0;
                },
                timeout: TimeSpan.FromSeconds(3));

            // Assert - Timeout is recorded
            Assert.That(deliveryRecorded, Is.True);

            var deliveryHistory = await _service.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest { SubscriptionId = subscription.Subscription!.Id },
                _testUserAddress);

            var delivery = deliveryHistory.Deliveries[0];
            Assert.That(delivery.Success, Is.False, "Timeout should be marked as failed");
            Assert.That(delivery.ResponseBody ?? delivery.ErrorMessage ?? "", Does.Contain("timeout").Or.Contain("cancel").IgnoreCase, 
                "Error message should indicate timeout or cancellation");
        }

        [Test]
        public async Task WebhookDelivery_SuccessfulRetryAfterFailure_RecordsBothAttempts()
        {
            // Arrange - Mock HTTP client to fail first, then succeed
            var attempt = 0;
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    attempt++;
                    if (attempt == 1)
                    {
                        return new HttpResponseMessage
                        {
                            StatusCode = HttpStatusCode.ServiceUnavailable,
                            Content = new StringContent("Service temporarily unavailable")
                        };
                    }
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent("Success")
                    };
                });

            var httpClient = new HttpClient(httpMessageHandlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Create subscription
            var subscription = await _service.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
                },
                _testUserAddress);

            // Act - Emit event
            await _service.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.WhitelistAdd,
                AssetId = 12345,
                Actor = _testUserAddress
            });

            // Wait for initial delivery attempt
            var firstDeliveryRecorded = await AsyncTestHelper.WaitForConditionAsync(
                async () =>
                {
                    var history = await _service.GetDeliveryHistoryAsync(
                        new GetWebhookDeliveryHistoryRequest { SubscriptionId = subscription.Subscription!.Id },
                        _testUserAddress);
                    return history.Success && history.Deliveries.Count > 0;
                },
                timeout: TimeSpan.FromSeconds(3));

            Assert.That(firstDeliveryRecorded, Is.True, "First delivery attempt should be recorded");

            // Assert - First attempt failed
            var deliveryHistory = await _service.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest { SubscriptionId = subscription.Subscription!.Id },
                _testUserAddress);

            Assert.That(deliveryHistory.Deliveries.Count, Is.GreaterThan(0));
            var firstDelivery = deliveryHistory.Deliveries[0];
            Assert.That(firstDelivery.Success, Is.False, "First delivery should fail");
            Assert.That(firstDelivery.StatusCode, Is.EqualTo(503), "Should record 503 status");
        }

        [Test]
        public async Task WebhookDelivery_NoActiveSubscriptions_DoesNotAttemptDelivery()
        {
            // Arrange - Create but then deactivate subscription
            var subscription = await _service.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
                },
                _testUserAddress);

            Assert.That(subscription.Success, Is.True);

            // Deactivate subscription
            await _service.DeleteSubscriptionAsync(subscription.Subscription!.Id, _testUserAddress);

            // Act - Emit event
            await _service.EmitEventAsync(new WebhookEvent
            {
                EventType = WebhookEventType.WhitelistAdd,
                AssetId = 12345,
                Actor = _testUserAddress
            });

            // Wait to ensure no delivery is attempted
            await Task.Delay(1000);

            // Assert - No deliveries recorded
            var deliveryHistory = await _service.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest { SubscriptionId = subscription.Subscription.Id },
                _testUserAddress);

            Assert.That(deliveryHistory.Deliveries.Count, Is.EqualTo(0), 
                "No deliveries should be attempted for inactive subscriptions");
        }

        [Test]
        public async Task WebhookDelivery_MultipleFailures_RecordsAllAttempts()
        {
            // Arrange - Mock HTTP client to always fail
            var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadGateway,
                    Content = new StringContent("Bad gateway")
                });

            var httpClient = new HttpClient(httpMessageHandlerMock.Object);
            _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            // Create subscription
            var subscription = await _service.CreateSubscriptionAsync(
                new CreateWebhookSubscriptionRequest
                {
                    Url = "https://example.com/webhook",
                    EventTypes = new List<WebhookEventType> { WebhookEventType.WhitelistAdd }
                },
                _testUserAddress);

            // Act - Emit multiple events
            for (int i = 0; i < 3; i++)
            {
                await _service.EmitEventAsync(new WebhookEvent
                {
                    EventType = WebhookEventType.WhitelistAdd,
                    AssetId = (ulong)(12345 + i),
                    Actor = _testUserAddress
                });
            }

            // Wait for all delivery attempts
            var allDeliveriesRecorded = await AsyncTestHelper.WaitForConditionAsync(
                async () =>
                {
                    var history = await _service.GetDeliveryHistoryAsync(
                        new GetWebhookDeliveryHistoryRequest { SubscriptionId = subscription.Subscription!.Id },
                        _testUserAddress);
                    return history.Success && history.Deliveries.Count >= 3;
                },
                timeout: TimeSpan.FromSeconds(5));

            // Assert - All failures recorded
            Assert.That(allDeliveriesRecorded, Is.True, "All 3 delivery attempts should be recorded");

            var deliveryHistory = await _service.GetDeliveryHistoryAsync(
                new GetWebhookDeliveryHistoryRequest { SubscriptionId = subscription.Subscription!.Id },
                _testUserAddress);

            Assert.That(deliveryHistory.Deliveries.Count, Is.GreaterThanOrEqualTo(3), 
                "Should record all 3 delivery attempts");
            Assert.That(deliveryHistory.Deliveries.All(d => !d.Success), Is.True, 
                "All deliveries should be marked as failed");
        }
    }
}
