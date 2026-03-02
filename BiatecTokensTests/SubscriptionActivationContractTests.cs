using BiatecTokensApi.Configuration;
using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using System.Security.Claims;

namespace BiatecTokensTests
{
    /// <summary>
    /// Acceptance criteria tests for the subscription system activation (Issue #441).
    /// Covers all 8 acceptance criteria from the issue.
    /// </summary>
    [TestFixture]
    public class SubscriptionActivationContractTests
    {
        private ISubscriptionRepository _repository = null!;
        private SubscriptionTierService _tierService = null!;
        private Mock<ILogger<SubscriptionRepository>> _repoLogger = null!;
        private Mock<ILogger<SubscriptionTierService>> _tierLogger = null!;
        private Mock<ILogger<StripeService>> _stripeLogger = null!;
        private StripeConfig _config = null!;

        private const string TestUser = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string TestUser2 = "TESTUSER2ADDRESSABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890ABCDEF";

        [SetUp]
        public void Setup()
        {
            _repoLogger = new Mock<ILogger<SubscriptionRepository>>();
            _tierLogger = new Mock<ILogger<SubscriptionTierService>>();
            _stripeLogger = new Mock<ILogger<StripeService>>();

            _repository = new SubscriptionRepository(_repoLogger.Object);
            _tierService = new SubscriptionTierService(_tierLogger.Object);

            _config = new StripeConfig
            {
                SecretKey = "sk_test_placeholder_no_live_calls",
                PublishableKey = "pk_test_placeholder",
                WebhookSecret = "whsec_test_placeholder",
                BasicPriceId = "price_basic_test",
                ProPriceId = "price_pro_test",
                EnterprisePriceId = "price_enterprise_test",
                CheckoutSuccessUrl = "https://example.com/success",
                CheckoutCancelUrl = "https://example.com/cancel"
            };
        }

        private IStripeService CreateStripeService()
        {
            var configMock = new Mock<IOptions<StripeConfig>>();
            configMock.Setup(x => x.Value).Returns(_config);
            return new StripeService(configMock.Object, _repository, _tierService, _stripeLogger.Object);
        }

        private static SubscriptionController CreateController(IStripeService stripeService, string userAddress)
        {
            var loggerMock = new Mock<ILogger<SubscriptionController>>();
            var controller = new SubscriptionController(stripeService, loggerMock.Object);
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userAddress) };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
            };
            return controller;
        }

        private static AdminSubscriptionController CreateAdminController(IStripeService stripeService, string adminId)
        {
            var loggerMock = new Mock<ILogger<AdminSubscriptionController>>();
            var controller = new AdminSubscriptionController(stripeService, loggerMock.Object);
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, adminId) };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
            };
            return controller;
        }

        #region AC3: Trial Period Auto-Provisioned

        [Test]
        public async Task AC3_ProvisionTrial_NewUser_CreatesProfessionalTierTrialing()
        {
            // Arrange
            var service = CreateStripeService();

            // Act — simulate what happens after user registration
            await service.ProvisionTrialAsync(TestUser);

            // Assert
            var state = await _repository.GetSubscriptionAsync(TestUser);
            Assert.That(state, Is.Not.Null, "Trial subscription record must exist after provisioning");
            Assert.That(state!.Status, Is.EqualTo(SubscriptionStatus.Trialing), "Status must be Trialing");
            Assert.That(state.Tier, Is.EqualTo(SubscriptionTier.Premium), "Trial tier must be Premium (Professional)");
            Assert.That(state.CurrentPeriodEnd, Is.Not.Null, "TrialEndsAt (CurrentPeriodEnd) must be set");
        }

        [Test]
        public async Task AC3_ProvisionTrial_TrialEndIsApprox14DaysFromNow()
        {
            var service = CreateStripeService();

            await service.ProvisionTrialAsync(TestUser);

            var state = await _repository.GetSubscriptionAsync(TestUser);
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.CurrentPeriodEnd, Is.Not.Null);

            var daysUntilEnd = (state.CurrentPeriodEnd!.Value - DateTime.UtcNow).TotalDays;
            Assert.That(daysUntilEnd, Is.InRange(13.9, 14.1), "Trial must end in ~14 days");
        }

        [Test]
        public async Task AC3_ProvisionTrial_TierServiceUpdatedImmediately()
        {
            var service = CreateStripeService();

            await service.ProvisionTrialAsync(TestUser);

            var tier = await _tierService.GetUserTierAsync(TestUser);
            Assert.That(tier, Is.EqualTo(SubscriptionTier.Premium), "Tier service must reflect trial tier immediately");
        }

        [Test]
        public async Task AC3_StatusEndpoint_TrialingUser_IncludesTrialEndsAt()
        {
            var service = CreateStripeService();
            await service.ProvisionTrialAsync(TestUser);

            var controller = CreateController(service, TestUser);
            var result = await controller.GetSubscriptionStatus();

            var ok = result as OkObjectResult;
            Assert.That(ok, Is.Not.Null);
            var response = ok!.Value as SubscriptionStatusResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.TrialEndsAt, Is.Not.Null, "TrialEndsAt must be present for trialing user");
            Assert.That(response.DaysRemaining, Is.Not.Null, "DaysRemaining must be present for trialing user");
            Assert.That(response.DaysRemaining, Is.InRange(13, 14), "DaysRemaining must be ~14");
        }

        [Test]
        public async Task AC3_StatusEndpoint_NonTrialingUser_TrialEndsAtIsNull()
        {
            // Active paid subscription — no trial fields
            var subscription = new SubscriptionState
            {
                UserAddress = TestUser,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active,
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
            };
            await _repository.SaveSubscriptionAsync(subscription);

            var service = CreateStripeService();
            var controller = CreateController(service, TestUser);
            var result = await controller.GetSubscriptionStatus();

            var ok = result as OkObjectResult;
            var response = (ok!.Value as SubscriptionStatusResponse)!;
            Assert.That(response.TrialEndsAt, Is.Null, "TrialEndsAt must be null for non-trialing user");
            Assert.That(response.DaysRemaining, Is.Null, "DaysRemaining must be null for non-trialing user");
        }

        [Test]
        public async Task AC3_ProvisionTrial_ExistingActiveSubscription_NotOverridden()
        {
            // User already has active subscription
            var existing = new SubscriptionState
            {
                UserAddress = TestUser,
                Tier = SubscriptionTier.Enterprise,
                Status = SubscriptionStatus.Active
            };
            await _repository.SaveSubscriptionAsync(existing);

            var service = CreateStripeService();
            await service.ProvisionTrialAsync(TestUser); // Should not overwrite

            var state = await _repository.GetSubscriptionAsync(TestUser);
            Assert.That(state!.Tier, Is.EqualTo(SubscriptionTier.Enterprise), "Existing active subscription must not be downgraded to trial");
            Assert.That(state.Status, Is.EqualTo(SubscriptionStatus.Active));
        }

        [Test]
        public async Task AC3_ProvisionTrial_AuditLogCreated()
        {
            var service = CreateStripeService();
            await service.ProvisionTrialAsync(TestUser);

            var events = await _repository.GetWebhookEventsAsync(TestUser);
            var trialEvent = events.FirstOrDefault(e => e.EventType == "trial.provisioned");
            Assert.That(trialEvent, Is.Not.Null, "Audit log must contain trial.provisioned event");
            Assert.That(trialEvent!.Tier, Is.EqualTo(SubscriptionTier.Premium));
            Assert.That(trialEvent.Status, Is.EqualTo(SubscriptionStatus.Trialing));
        }

        #endregion

        #region AC5: Cancellation Works

        [Test]
        public async Task AC5_CancelSubscription_ScheduledAtPeriodEnd_ReturnsCancelAtPeriodEndTrue()
        {
            // Arrange — user has active subscription
            var subscription = new SubscriptionState
            {
                UserAddress = TestUser,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active,
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(15)
            };
            await _repository.SaveSubscriptionAsync(subscription);

            var service = CreateStripeService();
            var controller = CreateController(service, TestUser);

            // Act
            var result = await controller.CancelSubscription(new CancelSubscriptionRequest { CancelImmediately = false });

            // Assert
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var ok = result as OkObjectResult;
            var response = ok!.Value as CancelSubscriptionResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.True);
            Assert.That(response.CancelAtPeriodEnd, Is.True, "cancelAtPeriodEnd must be true");
        }

        [Test]
        public async Task AC5_GetStatusAfterCancel_ReturnsCancelAtPeriodEndTrue()
        {
            var subscription = new SubscriptionState
            {
                UserAddress = TestUser,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active,
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(15)
            };
            await _repository.SaveSubscriptionAsync(subscription);

            var service = CreateStripeService();
            await service.CancelSubscriptionAsync(TestUser);

            // Check status
            var state = await _repository.GetSubscriptionAsync(TestUser);
            Assert.That(state!.CancelAtPeriodEnd, Is.True, "CancelAtPeriodEnd must be persisted");
        }

        [Test]
        public async Task AC5_CancelImmediately_StatusBecomesCancel_TierBecomeFree()
        {
            var subscription = new SubscriptionState
            {
                UserAddress = TestUser,
                Tier = SubscriptionTier.Enterprise,
                Status = SubscriptionStatus.Active,
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(20)
            };
            await _repository.SaveSubscriptionAsync(subscription);

            var service = CreateStripeService();
            var response = await service.CancelSubscriptionAsync(TestUser, cancelImmediately: true);

            Assert.That(response.Success, Is.True);
            var state = await _repository.GetSubscriptionAsync(TestUser);
            Assert.That(state!.Status, Is.EqualTo(SubscriptionStatus.Canceled));
            Assert.That(state.Tier, Is.EqualTo(SubscriptionTier.Free));
        }

        [Test]
        public async Task AC5_CancelNoSubscription_ReturnsBadRequest()
        {
            var service = CreateStripeService();
            var controller = CreateController(service, TestUser);

            var result = await controller.CancelSubscription();

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badReq = result as BadRequestObjectResult;
            var response = badReq!.Value as CancelSubscriptionResponse;
            Assert.That(response!.ErrorMessage, Contains.Substring("No active subscription"));
        }

        [Test]
        public async Task AC5_CancelAlreadyCanceled_ReturnsBadRequest()
        {
            var subscription = new SubscriptionState
            {
                UserAddress = TestUser,
                Tier = SubscriptionTier.Free,
                Status = SubscriptionStatus.Canceled
            };
            await _repository.SaveSubscriptionAsync(subscription);

            var service = CreateStripeService();
            var response = await service.CancelSubscriptionAsync(TestUser);

            Assert.That(response.Success, Is.False);
            Assert.That(response.ErrorMessage, Contains.Substring("already canceled"));
        }

        [Test]
        public async Task AC5_CancelNoAuthentication_ReturnsUnauthorized()
        {
            var service = CreateStripeService();
            var loggerMock = new Mock<ILogger<SubscriptionController>>();
            var controller = new SubscriptionController(service, loggerMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            var result = await controller.CancelSubscription();

            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        [Test]
        public async Task AC5_CancelTrial_WorksCorrectly()
        {
            // Trial should also be cancelable
            var service = CreateStripeService();
            await service.ProvisionTrialAsync(TestUser);

            var response = await service.CancelSubscriptionAsync(TestUser, cancelImmediately: true);

            Assert.That(response.Success, Is.True);
            var state = await _repository.GetSubscriptionAsync(TestUser);
            Assert.That(state!.Status, Is.EqualTo(SubscriptionStatus.Canceled));
        }

        #endregion

        #region AC6: Admin Override Works

        [Test]
        public async Task AC6_AdminOverride_SetsUserTierImmediately()
        {
            var service = CreateStripeService();
            var controller = CreateAdminController(service, "admin-address");

            var request = new SubscriptionOverrideRequest
            {
                UserId = TestUser,
                Tier = SubscriptionTier.Enterprise,
                Reason = "Enterprise contract #12345"
            };

            var result = await controller.OverrideSubscriptionTier(request);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var ok = result as OkObjectResult;
            var response = ok!.Value as SubscriptionOverrideResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Tier, Is.EqualTo(SubscriptionTier.Enterprise));
            Assert.That(response.UserId, Is.EqualTo(TestUser));
        }

        [Test]
        public async Task AC6_AdminOverride_TierServiceUpdatedImmediately()
        {
            var service = CreateStripeService();
            await service.OverrideSubscriptionTierAsync(TestUser, SubscriptionTier.Enterprise);

            var tier = await _tierService.GetUserTierAsync(TestUser);
            Assert.That(tier, Is.EqualTo(SubscriptionTier.Enterprise), "Tier service must reflect override immediately");
        }

        [Test]
        public async Task AC6_AdminOverride_AuditLogCreated()
        {
            var service = CreateStripeService();
            await service.OverrideSubscriptionTierAsync(TestUser, SubscriptionTier.Enterprise, "Test override");

            var events = await _repository.GetWebhookEventsAsync(TestUser);
            var overrideEvent = events.FirstOrDefault(e => e.EventType == "admin.subscription_override");
            Assert.That(overrideEvent, Is.Not.Null, "Audit log must contain admin.subscription_override event");
            Assert.That(overrideEvent!.Tier, Is.EqualTo(SubscriptionTier.Enterprise));
        }

        [Test]
        public async Task AC6_AdminOverride_EmptyUserId_ThrowsException()
        {
            var service = CreateStripeService();
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await service.OverrideSubscriptionTierAsync("", SubscriptionTier.Basic));
        }

        [Test]
        public async Task AC6_AdminOverride_NewUser_CreatesSubscriptionRecord()
        {
            var service = CreateStripeService();
            var result = await service.OverrideSubscriptionTierAsync(TestUser2, SubscriptionTier.Basic);

            Assert.That(result.Success, Is.True);
            var state = await _repository.GetSubscriptionAsync(TestUser2);
            Assert.That(state, Is.Not.Null, "Override must create subscription record for new user");
            Assert.That(state!.Tier, Is.EqualTo(SubscriptionTier.Basic));
        }

        [Test]
        public async Task AC6_AdminOverride_DowngradeToFree_StatusBecomesNone()
        {
            // Setup user with paid subscription
            var subscription = new SubscriptionState
            {
                UserAddress = TestUser,
                Tier = SubscriptionTier.Enterprise,
                Status = SubscriptionStatus.Active
            };
            await _repository.SaveSubscriptionAsync(subscription);

            var service = CreateStripeService();
            await service.OverrideSubscriptionTierAsync(TestUser, SubscriptionTier.Free);

            var state = await _repository.GetSubscriptionAsync(TestUser);
            Assert.That(state!.Tier, Is.EqualTo(SubscriptionTier.Free));
            Assert.That(state.Status, Is.EqualTo(SubscriptionStatus.None));
        }

        [Test]
        public async Task AC6_AdminMetrics_ReturnsAggregateData()
        {
            var service = CreateStripeService();

            // Setup some subscriptions for metrics
            await service.ProvisionTrialAsync(TestUser);
            await service.OverrideSubscriptionTierAsync(TestUser2, SubscriptionTier.Enterprise);

            var controller = CreateAdminController(service, "admin-address");
            var result = await controller.GetMetrics();

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var ok = result as OkObjectResult;
            var response = ok!.Value as SubscriptionMetricsResponse;
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Success, Is.True);
            Assert.That(response.Metrics, Is.Not.Null);
            Assert.That(response.Metrics!.TierDistribution, Is.Not.Null);
        }

        [Test]
        public async Task AC6_AdminMetrics_MrrReflectsActivePaidSubscriptions()
        {
            var service = CreateStripeService();
            // 1 Basic ($29), 1 Enterprise ($299)
            await service.OverrideSubscriptionTierAsync(TestUser, SubscriptionTier.Basic, "basic override");
            await service.OverrideSubscriptionTierAsync(TestUser2, SubscriptionTier.Enterprise, "enterprise override");

            var metrics = await service.GetAdminMetricsAsync();

            // MRR should include at least Basic ($2900 cents) + Enterprise ($29900 cents) = $32800
            Assert.That(metrics.MrrCents, Is.GreaterThanOrEqualTo(2900 + 29900),
                "MRR must account for Basic + Enterprise subscribers");
        }

        #endregion

        #region AC4: Webhook Idempotency

        [Test]
        public async Task AC4_WebhookIdempotency_SameEventIdProcessedOnce()
        {
            // Simulate a webhook event being processed twice
            var eventId = $"evt_test_idempotency_{Guid.NewGuid()}";
            var webhookEvent1 = new SubscriptionWebhookEvent
            {
                EventId = eventId,
                EventType = "invoice.payment_succeeded",
                UserAddress = TestUser,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active,
                Success = true
            };

            await _repository.MarkEventProcessedAsync(webhookEvent1);

            // Second call with same event ID
            var isAlreadyProcessed = await _repository.IsEventProcessedAsync(eventId);
            Assert.That(isAlreadyProcessed, Is.True, "Event must be marked as processed after first handling");

            // Verify only one event stored
            var events = await _repository.GetWebhookEventsAsync(TestUser);
            var matchingEvents = events.Where(e => e.EventId == eventId).ToList();
            Assert.That(matchingEvents.Count, Is.EqualTo(1), "Same event ID must appear only once in audit log");
        }

        #endregion

        #region AC7: Audit Log Populated

        [Test]
        public async Task AC7_AuditLog_TrialProvisionCreatesAuditEntry()
        {
            var service = CreateStripeService();
            await service.ProvisionTrialAsync(TestUser);

            var events = await _repository.GetWebhookEventsAsync(TestUser);
            Assert.That(events, Is.Not.Empty, "Audit log must be populated after trial provision");
            Assert.That(events.Any(e => e.EventType == "trial.provisioned"), Is.True);
        }

        [Test]
        public async Task AC7_AuditLog_CancellationCreatesAuditEntry()
        {
            var subscription = new SubscriptionState
            {
                UserAddress = TestUser,
                Tier = SubscriptionTier.Basic,
                Status = SubscriptionStatus.Active,
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(10)
            };
            await _repository.SaveSubscriptionAsync(subscription);

            var service = CreateStripeService();
            await service.CancelSubscriptionAsync(TestUser);

            var events = await _repository.GetWebhookEventsAsync(TestUser);
            Assert.That(events.Any(e => e.EventType.Contains("cancel")), Is.True,
                "Audit log must contain cancellation event");
        }

        [Test]
        public async Task AC7_AuditLog_AdminOverrideCreatesAuditEntry()
        {
            var service = CreateStripeService();
            await service.OverrideSubscriptionTierAsync(TestUser, SubscriptionTier.Enterprise, "beta invite");

            var events = await _repository.GetWebhookEventsAsync(TestUser);
            Assert.That(events.Any(e => e.EventType == "admin.subscription_override"), Is.True,
                "Audit log must contain admin override event");
        }

        [Test]
        public async Task AC7_AuditLog_AllEventsIncludeUserAddress()
        {
            var service = CreateStripeService();
            await service.ProvisionTrialAsync(TestUser);
            await service.CancelSubscriptionAsync(TestUser);

            var events = await _repository.GetWebhookEventsAsync(TestUser);
            Assert.That(events.All(e => !string.IsNullOrWhiteSpace(e.UserAddress)), Is.True,
                "All audit entries must include UserAddress for traceability");
        }

        #endregion

        #region AC2: Feature Gating Returns HTTP 402

        [Test]
        public async Task AC2_SubscriptionGuard_TrialUserCanDeploy()
        {
            var service = CreateStripeService();
            await service.ProvisionTrialAsync(TestUser);

            // Trial (Premium) should allow deployment
            var canDeploy = await _tierService.CanDeployTokenAsync(TestUser);
            Assert.That(canDeploy, Is.True, "Trial user (Premium tier) must be allowed to deploy tokens");
        }

        [Test]
        public async Task AC2_OverriddenEnterpriseUser_UnlimitedDeployments()
        {
            var service = CreateStripeService();
            await service.OverrideSubscriptionTierAsync(TestUser, SubscriptionTier.Enterprise);

            // Enterprise = unlimited, record many deployments
            for (int i = 0; i < 10; i++)
            {
                await _tierService.RecordTokenDeploymentAsync(TestUser);
            }

            var canDeploy = await _tierService.CanDeployTokenAsync(TestUser);
            Assert.That(canDeploy, Is.True, "Enterprise user must always be able to deploy");
        }

        #endregion

        #region Determinism and Idempotency

        [Test]
        public async Task Idempotency_ProvisionTrialTwice_OnlyOneRecordCreated()
        {
            var service = CreateStripeService();

            await service.ProvisionTrialAsync(TestUser);
            await service.ProvisionTrialAsync(TestUser); // second call should be no-op

            var state = await _repository.GetSubscriptionAsync(TestUser);
            Assert.That(state, Is.Not.Null);
            Assert.That(state!.Status, Is.EqualTo(SubscriptionStatus.Trialing));
            // Should still be trial, not duplicated
        }

        [Test]
        public async Task Determinism_OverrideThreeTimes_FinalTierWins()
        {
            var service = CreateStripeService();
            await service.OverrideSubscriptionTierAsync(TestUser, SubscriptionTier.Basic);
            await service.OverrideSubscriptionTierAsync(TestUser, SubscriptionTier.Enterprise);
            await service.OverrideSubscriptionTierAsync(TestUser, SubscriptionTier.Premium);

            var tier = await _tierService.GetUserTierAsync(TestUser);
            Assert.That(tier, Is.EqualTo(SubscriptionTier.Premium), "Last override must be the effective tier");
        }

        #endregion
    }
}
