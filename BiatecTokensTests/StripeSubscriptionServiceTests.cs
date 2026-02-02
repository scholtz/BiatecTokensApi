using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for Stripe subscription service
    /// </summary>
    [TestFixture]
    public class StripeSubscriptionServiceTests
    {
        private Mock<IOptions<StripeConfig>> _configMock;
        private Mock<ISubscriptionRepository> _repositoryMock;
        private Mock<ILogger<StripeService>> _loggerMock;
        private Mock<ILogger<SubscriptionTierService>> _tierLoggerMock;
        private SubscriptionTierService _tierService;
        private StripeConfig _config;

        private const string TestUserAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string TestCustomerId = "cus_test123";
        private const string TestSubscriptionId = "sub_test123";

        [SetUp]
        public void Setup()
        {
            _config = new StripeConfig
            {
                SecretKey = "sk_test_51234567890",
                PublishableKey = "pk_test_51234567890",
                WebhookSecret = "whsec_test123",
                BasicPriceId = "price_basic123",
                ProPriceId = "price_pro123",
                EnterprisePriceId = "price_enterprise123",
                CheckoutSuccessUrl = "https://example.com/success",
                CheckoutCancelUrl = "https://example.com/cancel"
            };

            _configMock = new Mock<IOptions<StripeConfig>>();
            _configMock.Setup(x => x.Value).Returns(_config);

            _repositoryMock = new Mock<ISubscriptionRepository>();
            _loggerMock = new Mock<ILogger<StripeService>>();
            _tierLoggerMock = new Mock<ILogger<SubscriptionTierService>>();
            _tierService = new SubscriptionTierService(_tierLoggerMock.Object);
        }

        #region GetSubscriptionStatusAsync Tests

        [Test]
        public async Task GetSubscriptionStatusAsync_NewUser_ReturnsFreeTier()
        {
            // Arrange
            _repositoryMock.Setup(x => x.GetSubscriptionAsync(TestUserAddress))
                .ReturnsAsync((SubscriptionState?)null);

            var service = new StripeService(_configMock.Object, _repositoryMock.Object, _tierService, _loggerMock.Object);

            // Act
            var result = await service.GetSubscriptionStatusAsync(TestUserAddress);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.UserAddress, Is.EqualTo(TestUserAddress));
            Assert.That(result.Tier, Is.EqualTo(SubscriptionTier.Free));
            Assert.That(result.Status, Is.EqualTo(SubscriptionStatus.None));
        }

        [Test]
        public async Task GetSubscriptionStatusAsync_ExistingSubscription_ReturnsCorrectState()
        {
            // Arrange
            var subscription = new SubscriptionState
            {
                UserAddress = TestUserAddress,
                StripeCustomerId = TestCustomerId,
                StripeSubscriptionId = TestSubscriptionId,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active,
                SubscriptionStartDate = DateTime.UtcNow.AddDays(-30),
                CurrentPeriodStart = DateTime.UtcNow.AddDays(-5),
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(25)
            };

            _repositoryMock.Setup(x => x.GetSubscriptionAsync(TestUserAddress))
                .ReturnsAsync(subscription);

            var service = new StripeService(_configMock.Object, _repositoryMock.Object, _tierService, _loggerMock.Object);

            // Act
            var result = await service.GetSubscriptionStatusAsync(TestUserAddress);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.UserAddress, Is.EqualTo(TestUserAddress));
            Assert.That(result.Tier, Is.EqualTo(SubscriptionTier.Basic));
            Assert.That(result.Status, Is.EqualTo(SubscriptionStatus.Active));
            Assert.That(result.StripeCustomerId, Is.EqualTo(TestCustomerId));
            Assert.That(result.StripeSubscriptionId, Is.EqualTo(TestSubscriptionId));
        }

        [Test]
        public void GetSubscriptionStatusAsync_NullAddress_ThrowsException()
        {
            // Arrange
            var service = new StripeService(_configMock.Object, _repositoryMock.Object, _tierService, _loggerMock.Object);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await service.GetSubscriptionStatusAsync(null!));

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await service.GetSubscriptionStatusAsync(""));
        }

        #endregion

        #region Subscription Repository Tests

        [Test]
        public async Task Repository_SaveAndRetrieve_WorksCorrectly()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<SubscriptionRepository>>();
            var repository = new SubscriptionRepository(loggerMock.Object);

            var subscription = new SubscriptionState
            {
                UserAddress = TestUserAddress,
                StripeCustomerId = TestCustomerId,
                StripeSubscriptionId = TestSubscriptionId,
                Tier = SubscriptionTier.Premium,
                Status = SubscriptionStatus.Active
            };

            // Act
            await repository.SaveSubscriptionAsync(subscription);
            var retrieved = await repository.GetSubscriptionAsync(TestUserAddress);

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.UserAddress, Is.EqualTo(TestUserAddress));
            Assert.That(retrieved.StripeCustomerId, Is.EqualTo(TestCustomerId));
            Assert.That(retrieved.Tier, Is.EqualTo(SubscriptionTier.Premium));
        }

        [Test]
        public async Task Repository_GetByCustomerId_WorksCorrectly()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<SubscriptionRepository>>();
            var repository = new SubscriptionRepository(loggerMock.Object);

            var subscription = new SubscriptionState
            {
                UserAddress = TestUserAddress,
                StripeCustomerId = TestCustomerId,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active
            };

            // Act
            await repository.SaveSubscriptionAsync(subscription);
            var retrieved = await repository.GetSubscriptionByCustomerIdAsync(TestCustomerId);

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.StripeCustomerId, Is.EqualTo(TestCustomerId));
            Assert.That(retrieved.UserAddress, Is.EqualTo(TestUserAddress));
        }

        [Test]
        public async Task Repository_GetBySubscriptionId_WorksCorrectly()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<SubscriptionRepository>>();
            var repository = new SubscriptionRepository(loggerMock.Object);

            var subscription = new SubscriptionState
            {
                UserAddress = TestUserAddress,
                StripeSubscriptionId = TestSubscriptionId,
                Tier = SubscriptionTier.Enterprise,
                Status = SubscriptionStatus.Active
            };

            // Act
            await repository.SaveSubscriptionAsync(subscription);
            var retrieved = await repository.GetSubscriptionBySubscriptionIdAsync(TestSubscriptionId);

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.StripeSubscriptionId, Is.EqualTo(TestSubscriptionId));
            Assert.That(retrieved.UserAddress, Is.EqualTo(TestUserAddress));
        }

        [Test]
        public async Task Repository_WebhookIdempotency_WorksCorrectly()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<SubscriptionRepository>>();
            var repository = new SubscriptionRepository(loggerMock.Object);

            var eventId = "evt_test123";
            var webhookEvent = new SubscriptionWebhookEvent
            {
                EventId = eventId,
                EventType = "customer.subscription.created",
                UserAddress = TestUserAddress,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active,
                Success = true
            };

            // Act
            var isProcessedBefore = await repository.IsEventProcessedAsync(eventId);
            await repository.MarkEventProcessedAsync(webhookEvent);
            var isProcessedAfter = await repository.IsEventProcessedAsync(eventId);

            // Assert
            Assert.That(isProcessedBefore, Is.False);
            Assert.That(isProcessedAfter, Is.True);
        }

        [Test]
        public async Task Repository_GetWebhookEvents_ReturnsAllEvents()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<SubscriptionRepository>>();
            var repository = new SubscriptionRepository(loggerMock.Object);

            var event1 = new SubscriptionWebhookEvent
            {
                EventId = "evt_1",
                EventType = "customer.subscription.created",
                UserAddress = TestUserAddress,
                Success = true
            };

            var event2 = new SubscriptionWebhookEvent
            {
                EventId = "evt_2",
                EventType = "customer.subscription.updated",
                UserAddress = TestUserAddress,
                Success = true
            };

            // Act
            await repository.MarkEventProcessedAsync(event1);
            await repository.MarkEventProcessedAsync(event2);
            var events = await repository.GetWebhookEventsAsync();

            // Assert
            Assert.That(events, Has.Count.EqualTo(2));
            Assert.That(events.Any(e => e.EventId == "evt_1"), Is.True);
            Assert.That(events.Any(e => e.EventId == "evt_2"), Is.True);
        }

        [Test]
        public async Task Repository_GetWebhookEvents_FiltersByUserAddress()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<SubscriptionRepository>>();
            var repository = new SubscriptionRepository(loggerMock.Object);

            var user1Address = "USER1ADDRESS";
            var user2Address = "USER2ADDRESS";

            var event1 = new SubscriptionWebhookEvent
            {
                EventId = "evt_1",
                EventType = "customer.subscription.created",
                UserAddress = user1Address,
                Success = true
            };

            var event2 = new SubscriptionWebhookEvent
            {
                EventId = "evt_2",
                EventType = "customer.subscription.created",
                UserAddress = user2Address,
                Success = true
            };

            // Act
            await repository.MarkEventProcessedAsync(event1);
            await repository.MarkEventProcessedAsync(event2);
            var user1Events = await repository.GetWebhookEventsAsync(user1Address);
            var user2Events = await repository.GetWebhookEventsAsync(user2Address);

            // Assert
            Assert.That(user1Events, Has.Count.EqualTo(1));
            Assert.That(user1Events[0].EventId, Is.EqualTo("evt_1"));
            Assert.That(user2Events, Has.Count.EqualTo(1));
            Assert.That(user2Events[0].EventId, Is.EqualTo("evt_2"));
        }

        #endregion

        #region Integration with Tier Service Tests

        [Test]
        public async Task TierService_UpdatesWhenSubscriptionChanges()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<SubscriptionRepository>>();
            var repository = new SubscriptionRepository(loggerMock.Object);

            var subscription = new SubscriptionState
            {
                UserAddress = TestUserAddress,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active
            };

            // Act - save subscription
            await repository.SaveSubscriptionAsync(subscription);
            
            // Manually update tier service (this would be done by StripeService in real scenario)
            _tierService.SetUserTier(TestUserAddress, subscription.Tier);

            // Get tier from tier service
            var tier = await _tierService.GetUserTierAsync(TestUserAddress);

            // Assert
            Assert.That(tier, Is.EqualTo(SubscriptionTier.Basic));
        }

        #endregion
    }
}
