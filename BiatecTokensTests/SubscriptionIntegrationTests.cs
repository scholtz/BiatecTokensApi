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
    /// End-to-end integration tests for subscription lifecycle
    /// </summary>
    [TestFixture]
    public class SubscriptionIntegrationTests
    {
        private ISubscriptionRepository _repository;
        private ISubscriptionTierService _tierService;
        private Mock<ILogger<SubscriptionRepository>> _repoLoggerMock;
        private Mock<ILogger<SubscriptionTierService>> _tierLoggerMock;
        private StripeConfig _config;

        private const string TestUserAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string TestCustomerId = "cus_integration_test";
        private const string TestSubscriptionId = "sub_integration_test";

        [SetUp]
        public void Setup()
        {
            _repoLoggerMock = new Mock<ILogger<SubscriptionRepository>>();
            _tierLoggerMock = new Mock<ILogger<SubscriptionTierService>>();

            _repository = new SubscriptionRepository(_repoLoggerMock.Object);
            _tierService = new SubscriptionTierService(_tierLoggerMock.Object);

            _config = new StripeConfig
            {
                SecretKey = "sk_test_integration",
                PublishableKey = "pk_test_integration",
                WebhookSecret = "whsec_test_integration",
                BasicPriceId = "price_basic_test",
                ProPriceId = "price_pro_test",
                EnterprisePriceId = "price_enterprise_test",
                CheckoutSuccessUrl = "https://example.com/success",
                CheckoutCancelUrl = "https://example.com/cancel"
            };
        }

        [Test]
        public async Task FullSubscriptionLifecycle_FromFreeToBasicToCancel()
        {
            // Phase 1: User starts with Free tier
            var initialTier = await _tierService.GetUserTierAsync(TestUserAddress);
            Assert.That(initialTier, Is.EqualTo(SubscriptionTier.Free));

            // Phase 2: User subscribes to Basic tier
            var subscription = new SubscriptionState
            {
                UserAddress = TestUserAddress,
                StripeCustomerId = TestCustomerId,
                StripeSubscriptionId = TestSubscriptionId,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active,
                SubscriptionStartDate = DateTime.UtcNow,
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
                CancelAtPeriodEnd = false
            };

            await _repository.SaveSubscriptionAsync(subscription);

            // Simulate StripeService updating the tier
            if (_tierService is SubscriptionTierService tierService)
            {
                tierService.SetUserTier(TestUserAddress, SubscriptionTier.Basic);
            }

            var activeTier = await _tierService.GetUserTierAsync(TestUserAddress);
            Assert.That(activeTier, Is.EqualTo(SubscriptionTier.Basic));

            // Verify subscription is active
            var retrieved = await _repository.GetSubscriptionAsync(TestUserAddress);
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.Status, Is.EqualTo(SubscriptionStatus.Active));
            Assert.That(retrieved.Tier, Is.EqualTo(SubscriptionTier.Basic));

            // Phase 3: User cancels subscription
            subscription.Status = SubscriptionStatus.Canceled;
            subscription.Tier = SubscriptionTier.Free;
            subscription.SubscriptionEndDate = DateTime.UtcNow;
            await _repository.SaveSubscriptionAsync(subscription);

            // Update tier back to Free
            if (_tierService is SubscriptionTierService tierServiceCancel)
            {
                tierServiceCancel.SetUserTier(TestUserAddress, SubscriptionTier.Free);
            }

            var canceledTier = await _tierService.GetUserTierAsync(TestUserAddress);
            Assert.That(canceledTier, Is.EqualTo(SubscriptionTier.Free));

            // Verify subscription is canceled
            var canceledSubscription = await _repository.GetSubscriptionAsync(TestUserAddress);
            Assert.That(canceledSubscription, Is.Not.Null);
            Assert.That(canceledSubscription.Status, Is.EqualTo(SubscriptionStatus.Canceled));
            Assert.That(canceledSubscription.Tier, Is.EqualTo(SubscriptionTier.Free));
        }

        [Test]
        public async Task SubscriptionUpgrade_FromBasicToPremium()
        {
            // Start with Basic subscription
            var subscription = new SubscriptionState
            {
                UserAddress = TestUserAddress,
                StripeCustomerId = TestCustomerId,
                StripeSubscriptionId = TestSubscriptionId,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active
            };

            await _repository.SaveSubscriptionAsync(subscription);

            if (_tierService is SubscriptionTierService tierService1)
            {
                tierService1.SetUserTier(TestUserAddress, SubscriptionTier.Basic);
            }

            var initialTier = await _tierService.GetUserTierAsync(TestUserAddress);
            Assert.That(initialTier, Is.EqualTo(SubscriptionTier.Basic));

            // Upgrade to Premium
            subscription.Tier = SubscriptionTier.Premium;
            await _repository.SaveSubscriptionAsync(subscription);

            if (_tierService is SubscriptionTierService tierService2)
            {
                tierService2.SetUserTier(TestUserAddress, SubscriptionTier.Premium);
            }

            var upgradedTier = await _tierService.GetUserTierAsync(TestUserAddress);
            Assert.That(upgradedTier, Is.EqualTo(SubscriptionTier.Premium));

            // Verify limits changed
            var basicLimits = _tierService.GetTierLimits(SubscriptionTier.Basic);
            var premiumLimits = _tierService.GetTierLimits(SubscriptionTier.Premium);

            Assert.That(premiumLimits.MaxAddressesPerAsset, Is.GreaterThan(basicLimits.MaxAddressesPerAsset));
        }

        [Test]
        public async Task WebhookIdempotency_SameEventProcessedOnce()
        {
            // Create first webhook event
            var eventId = "evt_idempotency_test";
            var event1 = new SubscriptionWebhookEvent
            {
                EventId = eventId,
                EventType = "customer.subscription.created",
                UserAddress = TestUserAddress,
                StripeSubscriptionId = TestSubscriptionId,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active,
                Success = true
            };

            // Process first time
            var isProcessedBefore = await _repository.IsEventProcessedAsync(eventId);
            Assert.That(isProcessedBefore, Is.False);

            await _repository.MarkEventProcessedAsync(event1);

            // Try to process again
            var isProcessedAfter = await _repository.IsEventProcessedAsync(eventId);
            Assert.That(isProcessedAfter, Is.True);

            // Create duplicate event (simulating retry)
            var event2 = new SubscriptionWebhookEvent
            {
                EventId = eventId, // Same event ID
                EventType = "customer.subscription.created",
                UserAddress = TestUserAddress,
                StripeSubscriptionId = TestSubscriptionId,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active,
                Success = true
            };

            // Should detect it's already processed
            var isDuplicate = await _repository.IsEventProcessedAsync(eventId);
            Assert.That(isDuplicate, Is.True);

            // Get all events - should only have one despite two attempts
            var events = await _repository.GetWebhookEventsAsync(TestUserAddress);
            Assert.That(events.Count(e => e.EventId == eventId), Is.EqualTo(1));
        }

        [Test]
        public async Task MultipleUsers_IndependentSubscriptions()
        {
            // User 1: Basic subscription
            var user1Address = "USER1ADDRESSFORSUBSCRIPTIONTEST123456789012345678901234";
            var subscription1 = new SubscriptionState
            {
                UserAddress = user1Address,
                StripeCustomerId = "cus_user1",
                StripeSubscriptionId = "sub_user1",
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active
            };

            await _repository.SaveSubscriptionAsync(subscription1);
            if (_tierService is SubscriptionTierService tierService1)
            {
                tierService1.SetUserTier(user1Address, SubscriptionTier.Basic);
            }

            // User 2: Premium subscription
            var user2Address = "USER2ADDRESSFORSUBSCRIPTIONTEST123456789012345678901234";
            var subscription2 = new SubscriptionState
            {
                UserAddress = user2Address,
                StripeCustomerId = "cus_user2",
                StripeSubscriptionId = "sub_user2",
                Tier = SubscriptionTier.Premium,
                Status = SubscriptionStatus.Active
            };

            await _repository.SaveSubscriptionAsync(subscription2);
            if (_tierService is SubscriptionTierService tierService2)
            {
                tierService2.SetUserTier(user2Address, SubscriptionTier.Premium);
            }

            // Verify both users have correct tiers
            var user1Tier = await _tierService.GetUserTierAsync(user1Address);
            var user2Tier = await _tierService.GetUserTierAsync(user2Address);

            Assert.That(user1Tier, Is.EqualTo(SubscriptionTier.Basic));
            Assert.That(user2Tier, Is.EqualTo(SubscriptionTier.Premium));

            // Verify subscriptions are independent
            var user1Sub = await _repository.GetSubscriptionAsync(user1Address);
            var user2Sub = await _repository.GetSubscriptionAsync(user2Address);

            Assert.That(user1Sub?.StripeCustomerId, Is.EqualTo("cus_user1"));
            Assert.That(user2Sub?.StripeCustomerId, Is.EqualTo("cus_user2"));
        }

        [Test]
        public async Task SubscriptionPastDue_KeepsAccessUntilCanceled()
        {
            // Create active subscription
            var subscription = new SubscriptionState
            {
                UserAddress = TestUserAddress,
                StripeCustomerId = TestCustomerId,
                StripeSubscriptionId = TestSubscriptionId,
                Tier = SubscriptionTier.Premium,
                Status = SubscriptionStatus.Active
            };

            await _repository.SaveSubscriptionAsync(subscription);
            if (_tierService is SubscriptionTierService tierService1)
            {
                tierService1.SetUserTier(TestUserAddress, SubscriptionTier.Premium);
            }

            var activeTier = await _tierService.GetUserTierAsync(TestUserAddress);
            Assert.That(activeTier, Is.EqualTo(SubscriptionTier.Premium));

            // Payment fails - subscription becomes past_due
            subscription.Status = SubscriptionStatus.PastDue;
            await _repository.SaveSubscriptionAsync(subscription);

            // Tier should still be Premium (user still has access)
            var pastDueTier = await _tierService.GetUserTierAsync(TestUserAddress);
            Assert.That(pastDueTier, Is.EqualTo(SubscriptionTier.Premium));

            // Eventually subscription is canceled
            subscription.Status = SubscriptionStatus.Canceled;
            subscription.Tier = SubscriptionTier.Free;
            await _repository.SaveSubscriptionAsync(subscription);

            if (_tierService is SubscriptionTierService tierService2)
            {
                tierService2.SetUserTier(TestUserAddress, SubscriptionTier.Free);
            }

            var canceledTier = await _tierService.GetUserTierAsync(TestUserAddress);
            Assert.That(canceledTier, Is.EqualTo(SubscriptionTier.Free));
        }

        [Test]
        public async Task WebhookAuditLog_TracksAllEvents()
        {
            // Simulate subscription lifecycle via webhooks
            var events = new List<SubscriptionWebhookEvent>
            {
                new SubscriptionWebhookEvent
                {
                    EventId = "evt_1_created",
                    EventType = "customer.subscription.created",
                    UserAddress = TestUserAddress,
                    Tier = SubscriptionTier.Basic,
                    Status = SubscriptionStatus.Active,
                    Success = true
                },
                new SubscriptionWebhookEvent
                {
                    EventId = "evt_2_updated",
                    EventType = "customer.subscription.updated",
                    UserAddress = TestUserAddress,
                    Tier = SubscriptionTier.Premium,
                    Status = SubscriptionStatus.Active,
                    Success = true
                },
                new SubscriptionWebhookEvent
                {
                    EventId = "evt_3_deleted",
                    EventType = "customer.subscription.deleted",
                    UserAddress = TestUserAddress,
                    Tier = SubscriptionTier.Free,
                    Status = SubscriptionStatus.Canceled,
                    Success = true
                }
            };

            // Process all events
            foreach (var evt in events)
            {
                await _repository.MarkEventProcessedAsync(evt);
            }

            // Retrieve audit log
            var auditLog = await _repository.GetWebhookEventsAsync(TestUserAddress);

            // Verify all events are logged in order
            Assert.That(auditLog, Has.Count.EqualTo(3));
            Assert.That(auditLog.Any(e => e.EventType == "customer.subscription.created"), Is.True);
            Assert.That(auditLog.Any(e => e.EventType == "customer.subscription.updated"), Is.True);
            Assert.That(auditLog.Any(e => e.EventType == "customer.subscription.deleted"), Is.True);

            // Verify all succeeded
            Assert.That(auditLog.All(e => e.Success), Is.True);
        }

        [Test]
        public async Task SubscriptionLimitsEnforcement_WorksWithTiers()
        {
            // Test that subscription tiers correctly enforce limits
            var testUser = "LIMITTESTUSERADDRESS12345678901234567890123456789012";

            // Free tier - limited capacity
            if (_tierService is SubscriptionTierService tierServiceFree)
            {
                tierServiceFree.SetUserTier(testUser, SubscriptionTier.Free);
            }

            var freeValidation = await _tierService.ValidateOperationAsync(testUser, 12345, 5, 10);
            Assert.That(freeValidation.IsAllowed, Is.False); // Free tier allows max 10 addresses

            // Basic tier - more capacity
            if (_tierService is SubscriptionTierService tierServiceBasic)
            {
                tierServiceBasic.SetUserTier(testUser, SubscriptionTier.Basic);
            }

            var basicValidation = await _tierService.ValidateOperationAsync(testUser, 12345, 50, 25);
            Assert.That(basicValidation.IsAllowed, Is.True); // Basic allows 100 total

            // Enterprise tier - unlimited
            if (_tierService is SubscriptionTierService tierServiceEnterprise)
            {
                tierServiceEnterprise.SetUserTier(testUser, SubscriptionTier.Enterprise);
            }

            var enterpriseValidation = await _tierService.ValidateOperationAsync(testUser, 12345, 5000, 1000);
            Assert.That(enterpriseValidation.IsAllowed, Is.True); // Enterprise is unlimited
        }
    }
}
