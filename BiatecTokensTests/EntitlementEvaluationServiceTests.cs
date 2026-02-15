using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Entitlement;
using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for EntitlementEvaluationService
    /// </summary>
    [TestFixture]
    public class EntitlementEvaluationServiceTests
    {
        private Mock<ISubscriptionTierService> _mockTierService = null!;
        private Mock<ILogger<EntitlementEvaluationService>> _mockLogger = null!;
        private EntitlementEvaluationService _service = null!;

        [SetUp]
        public void Setup()
        {
            _mockTierService = new Mock<ISubscriptionTierService>();
            _mockLogger = new Mock<ILogger<EntitlementEvaluationService>>();
            _service = new EntitlementEvaluationService(_mockTierService.Object, _mockLogger.Object);
        }

        [Test]
        public async Task CheckEntitlementAsync_FreeTier_TokenDeployment_WithinLimit_ReturnsAllowed()
        {
            // Arrange
            var userId = "test-user@example.com";
            _mockTierService.Setup(x => x.GetUserTierAsync(userId)).ReturnsAsync(SubscriptionTier.Free);
            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(userId)).ReturnsAsync(2);

            var request = new EntitlementCheckRequest
            {
                UserId = userId,
                Operation = EntitlementOperation.TokenDeployment,
                CorrelationId = "test-correlation-id"
            };

            // Act
            var result = await _service.CheckEntitlementAsync(request);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.SubscriptionTier, Is.EqualTo("Free"));
            Assert.That(result.PolicyVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(result.CorrelationId, Is.EqualTo("test-correlation-id"));
            Assert.That(result.DenialReason, Is.Null);
            Assert.That(result.UpgradeRecommendation, Is.Null);
        }

        [Test]
        public async Task CheckEntitlementAsync_FreeTier_TokenDeployment_ExceededLimit_ReturnsDenied()
        {
            // Arrange
            var userId = "test-user@example.com";
            _mockTierService.Setup(x => x.GetUserTierAsync(userId)).ReturnsAsync(SubscriptionTier.Free);
            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(userId)).ReturnsAsync(3);

            var request = new EntitlementCheckRequest
            {
                UserId = userId,
                Operation = EntitlementOperation.TokenDeployment,
                CorrelationId = "test-correlation-id"
            };

            // Act
            var result = await _service.CheckEntitlementAsync(request);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.SubscriptionTier, Is.EqualTo("Free"));
            Assert.That(result.DenialReason, Does.Contain("Token deployment limit reached"));
            Assert.That(result.DenialCode, Is.EqualTo(ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED));
            Assert.That(result.UpgradeRecommendation, Is.Not.Null);
            Assert.That(result.UpgradeRecommendation!.RecommendedTier, Is.EqualTo("Basic"));
        }

        [Test]
        public async Task CheckEntitlementAsync_BasicTier_TokenDeployment_WithinLimit_ReturnsAllowed()
        {
            // Arrange
            var userId = "test-user@example.com";
            _mockTierService.Setup(x => x.GetUserTierAsync(userId)).ReturnsAsync(SubscriptionTier.Basic);
            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(userId)).ReturnsAsync(5);

            var request = new EntitlementCheckRequest
            {
                UserId = userId,
                Operation = EntitlementOperation.TokenDeployment
            };

            // Act
            var result = await _service.CheckEntitlementAsync(request);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.SubscriptionTier, Is.EqualTo("Basic"));
        }

        [Test]
        public async Task CheckEntitlementAsync_EnterpriseTier_TokenDeployment_AlwaysAllowed()
        {
            // Arrange
            var userId = "test-user@example.com";
            _mockTierService.Setup(x => x.GetUserTierAsync(userId)).ReturnsAsync(SubscriptionTier.Enterprise);
            _mockTierService.Setup(x => x.GetTokenDeploymentCountAsync(userId)).ReturnsAsync(1000);

            var request = new EntitlementCheckRequest
            {
                UserId = userId,
                Operation = EntitlementOperation.TokenDeployment
            };

            // Act
            var result = await _service.CheckEntitlementAsync(request);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.SubscriptionTier, Is.EqualTo("Enterprise"));
            Assert.That(result.MaxAllowed!["maxDeployments"], Is.EqualTo(-1)); // Unlimited
        }

        [Test]
        public async Task CheckEntitlementAsync_FreeTier_AdvancedCompliance_ReturnsDenied()
        {
            // Arrange
            var userId = "test-user@example.com";
            _mockTierService.Setup(x => x.GetUserTierAsync(userId)).ReturnsAsync(SubscriptionTier.Free);

            var request = new EntitlementCheckRequest
            {
                UserId = userId,
                Operation = EntitlementOperation.AdvancedCompliance
            };

            // Act
            var result = await _service.CheckEntitlementAsync(request);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialCode, Is.EqualTo(ErrorCodes.FEATURE_NOT_INCLUDED));
            Assert.That(result.DenialReason, Does.Contain("not available in the Free tier"));
            Assert.That(result.UpgradeRecommendation, Is.Not.Null);
        }

        [Test]
        public async Task CheckEntitlementAsync_PremiumTier_AdvancedCompliance_ReturnsAllowed()
        {
            // Arrange
            var userId = "test-user@example.com";
            _mockTierService.Setup(x => x.GetUserTierAsync(userId)).ReturnsAsync(SubscriptionTier.Premium);

            var request = new EntitlementCheckRequest
            {
                UserId = userId,
                Operation = EntitlementOperation.AdvancedCompliance
            };

            // Act
            var result = await _service.CheckEntitlementAsync(request);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.SubscriptionTier, Is.EqualTo("Premium"));
        }

        [Test]
        public async Task CheckEntitlementAsync_WhitelistAddition_WithinLimit_ReturnsAllowed()
        {
            // Arrange
            var userId = "test-user@example.com";
            _mockTierService.Setup(x => x.GetUserTierAsync(userId)).ReturnsAsync(SubscriptionTier.Free);

            var request = new EntitlementCheckRequest
            {
                UserId = userId,
                Operation = EntitlementOperation.WhitelistAddition,
                OperationContext = new Dictionary<string, object>
                {
                    { "currentCount", 5 },
                    { "additionalCount", 2 }
                }
            };

            // Act
            var result = await _service.CheckEntitlementAsync(request);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.CurrentUsage!["currentAddresses"], Is.EqualTo(5));
            Assert.That(result.MaxAllowed!["maxAddresses"], Is.EqualTo(10));
        }

        [Test]
        public async Task CheckEntitlementAsync_WhitelistAddition_ExceedsLimit_ReturnsDenied()
        {
            // Arrange
            var userId = "test-user@example.com";
            _mockTierService.Setup(x => x.GetUserTierAsync(userId)).ReturnsAsync(SubscriptionTier.Free);

            var request = new EntitlementCheckRequest
            {
                UserId = userId,
                Operation = EntitlementOperation.WhitelistAddition,
                OperationContext = new Dictionary<string, object>
                {
                    { "currentCount", 10 },
                    { "additionalCount", 1 }
                }
            };

            // Act
            var result = await _service.CheckEntitlementAsync(request);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialCode, Is.EqualTo(ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED));
            Assert.That(result.DenialReason, Does.Contain("Whitelist limit exceeded"));
            Assert.That(result.UpgradeRecommendation, Is.Not.Null);
        }

        [Test]
        public async Task GetActivePolicyVersionAsync_ReturnsValidPolicy()
        {
            // Act
            var policy = await _service.GetActivePolicyVersionAsync();

            // Assert
            Assert.That(policy, Is.Not.Null);
            Assert.That(policy.Version, Is.Not.Null.And.Not.Empty);
            Assert.That(policy.IsActive, Is.True);
            Assert.That(policy.TierConfigurations, Has.Count.EqualTo(4));
            Assert.That(policy.TierConfigurations.ContainsKey("Free"), Is.True);
            Assert.That(policy.TierConfigurations.ContainsKey("Basic"), Is.True);
            Assert.That(policy.TierConfigurations.ContainsKey("Premium"), Is.True);
            Assert.That(policy.TierConfigurations.ContainsKey("Enterprise"), Is.True);
        }

        [Test]
        public async Task GetUpgradeRecommendationAsync_FreeTier_TokenDeployment_ReturnsBasicRecommendation()
        {
            // Act
            var recommendation = await _service.GetUpgradeRecommendationAsync(
                SubscriptionTier.Free,
                EntitlementOperation.TokenDeployment);

            // Assert
            Assert.That(recommendation, Is.Not.Null);
            Assert.That(recommendation.CurrentTier, Is.EqualTo("Free"));
            Assert.That(recommendation.RecommendedTier, Is.EqualTo("Basic"));
            Assert.That(recommendation.UnlockedFeatures, Is.Not.Empty);
            Assert.That(recommendation.LimitIncreases, Is.Not.Empty);
            Assert.That(recommendation.Message, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetUpgradeRecommendationAsync_BasicTier_ReturnsPremiumRecommendation()
        {
            // Act
            var recommendation = await _service.GetUpgradeRecommendationAsync(
                SubscriptionTier.Basic,
                EntitlementOperation.AdvancedCompliance);

            // Assert
            Assert.That(recommendation, Is.Not.Null);
            Assert.That(recommendation.CurrentTier, Is.EqualTo("Basic"));
            Assert.That(recommendation.RecommendedTier, Is.EqualTo("Premium"));
            Assert.That(recommendation.UnlockedFeatures, Contains.Item("Advanced compliance features"));
        }

        [Test]
        public async Task GetUpgradeRecommendationAsync_PremiumTier_ReturnsEnterpriseRecommendation()
        {
            // Act
            var recommendation = await _service.GetUpgradeRecommendationAsync(
                SubscriptionTier.Premium,
                EntitlementOperation.CustomBranding);

            // Assert
            Assert.That(recommendation, Is.Not.Null);
            Assert.That(recommendation.CurrentTier, Is.EqualTo("Premium"));
            Assert.That(recommendation.RecommendedTier, Is.EqualTo("Enterprise"));
            Assert.That(recommendation.UnlockedFeatures, Contains.Item("Custom branding"));
        }

        [Test]
        public async Task GetUpgradeRecommendationAsync_EnterpriseTier_ReturnsNoUpgrade()
        {
            // Act
            var recommendation = await _service.GetUpgradeRecommendationAsync(
                SubscriptionTier.Enterprise,
                EntitlementOperation.TokenDeployment);

            // Assert
            Assert.That(recommendation, Is.Not.Null);
            Assert.That(recommendation.CurrentTier, Is.EqualTo("Enterprise"));
            Assert.That(recommendation.RecommendedTier, Is.EqualTo("Enterprise"));
            Assert.That(recommendation.Message, Does.Contain("highest tier"));
        }

        [Test]
        public async Task RecordEntitlementDecisionAsync_RecordsSuccessfully()
        {
            // Arrange
            var result = new EntitlementCheckResult
            {
                IsAllowed = false,
                SubscriptionTier = "Free",
                PolicyVersion = "2026.02.15.1",
                DenialCode = ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED,
                CorrelationId = "test-correlation-id"
            };

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => 
                await _service.RecordEntitlementDecisionAsync(result, "test-correlation-id"));
        }

        [Test]
        public async Task CheckEntitlementAsync_BasicTier_AuditExport_Allowed()
        {
            // Arrange
            var userId = "test-user@example.com";
            _mockTierService.Setup(x => x.GetUserTierAsync(userId)).ReturnsAsync(SubscriptionTier.Basic);

            var request = new EntitlementCheckRequest
            {
                UserId = userId,
                Operation = EntitlementOperation.AuditExport
            };

            // Act
            var result = await _service.CheckEntitlementAsync(request);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.SubscriptionTier, Is.EqualTo("Basic"));
        }

        [Test]
        public async Task CheckEntitlementAsync_FreeTier_AuditExport_Denied()
        {
            // Arrange
            var userId = "test-user@example.com";
            _mockTierService.Setup(x => x.GetUserTierAsync(userId)).ReturnsAsync(SubscriptionTier.Free);

            var request = new EntitlementCheckRequest
            {
                UserId = userId,
                Operation = EntitlementOperation.AuditExport
            };

            // Act
            var result = await _service.CheckEntitlementAsync(request);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialCode, Is.EqualTo(ErrorCodes.FEATURE_NOT_INCLUDED));
            Assert.That(result.DenialReason, Does.Contain("not available in the Free tier"));
        }
    }
}
