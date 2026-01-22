using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for subscription tier validation and enforcement
    /// </summary>
    [TestFixture]
    public class SubscriptionTierGatingTests
    {
        private WhitelistRepository _repository;
        private Mock<ILogger<WhitelistService>> _loggerMock;
        private Mock<ILogger<WhitelistRepository>> _repoLoggerMock;
        private Mock<ILogger<SubscriptionTierService>> _tierLoggerMock;
        private Mock<ISubscriptionMeteringService> _meteringServiceMock;
        private SubscriptionTierService _tierService;
        private WhitelistService _whitelistService;

        private const string TestUser = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string TestAddress1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string TestAddress2 = "DN7MBMCL5JQ3PFUQS7TMX5AH4EEKOBJVDUF4TCV6WERATKFLQF4MQUPZUM";
        private const string TestAddress3 = "GD64YIY3TWGDMCNPP553DZPPR6LDUSFQOIJVFDPPXWEG3FVOJCCDBBHU5A";
        private const ulong TestAssetId = 12345;

        [SetUp]
        public void Setup()
        {
            _repoLoggerMock = new Mock<ILogger<WhitelistRepository>>();
            _repository = new WhitelistRepository(_repoLoggerMock.Object);
            _loggerMock = new Mock<ILogger<WhitelistService>>();
            _tierLoggerMock = new Mock<ILogger<SubscriptionTierService>>();
            _meteringServiceMock = new Mock<ISubscriptionMeteringService>();
            
            _tierService = new SubscriptionTierService(_tierLoggerMock.Object);
            _whitelistService = new WhitelistService(_repository, _loggerMock.Object, _meteringServiceMock.Object, _tierService);
        }

        #region Tier Configuration Tests

        [Test]
        public void GetTierLimits_FreeTier_ShouldReturnCorrectLimits()
        {
            // Act
            var limits = SubscriptionTierConfiguration.GetTierLimits(SubscriptionTier.Free);

            // Assert
            Assert.That(limits.MaxAddressesPerAsset, Is.EqualTo(10));
            Assert.That(limits.TierName, Is.EqualTo("Free"));
            Assert.That(limits.BulkOperationsEnabled, Is.False);
            Assert.That(limits.AuditLogEnabled, Is.False);
            Assert.That(limits.TransferValidationEnabled, Is.True);
        }

        [Test]
        public void GetTierLimits_BasicTier_ShouldReturnCorrectLimits()
        {
            // Act
            var limits = SubscriptionTierConfiguration.GetTierLimits(SubscriptionTier.Basic);

            // Assert
            Assert.That(limits.MaxAddressesPerAsset, Is.EqualTo(100));
            Assert.That(limits.TierName, Is.EqualTo("Basic"));
            Assert.That(limits.BulkOperationsEnabled, Is.False);
            Assert.That(limits.AuditLogEnabled, Is.True);
            Assert.That(limits.TransferValidationEnabled, Is.True);
        }

        [Test]
        public void GetTierLimits_PremiumTier_ShouldReturnCorrectLimits()
        {
            // Act
            var limits = SubscriptionTierConfiguration.GetTierLimits(SubscriptionTier.Premium);

            // Assert
            Assert.That(limits.MaxAddressesPerAsset, Is.EqualTo(1000));
            Assert.That(limits.TierName, Is.EqualTo("Premium"));
            Assert.That(limits.BulkOperationsEnabled, Is.True);
            Assert.That(limits.AuditLogEnabled, Is.True);
            Assert.That(limits.TransferValidationEnabled, Is.True);
        }

        [Test]
        public void GetTierLimits_EnterpriseTier_ShouldReturnUnlimited()
        {
            // Act
            var limits = SubscriptionTierConfiguration.GetTierLimits(SubscriptionTier.Enterprise);

            // Assert
            Assert.That(limits.MaxAddressesPerAsset, Is.EqualTo(-1)); // Unlimited
            Assert.That(limits.TierName, Is.EqualTo("Enterprise"));
            Assert.That(limits.BulkOperationsEnabled, Is.True);
            Assert.That(limits.AuditLogEnabled, Is.True);
            Assert.That(limits.TransferValidationEnabled, Is.True);
        }

        #endregion

        #region Subscription Tier Service Tests

        [Test]
        public async Task GetUserTier_DefaultUser_ShouldReturnFreeTier()
        {
            // Act
            var tier = await _tierService.GetUserTierAsync("SOMEADDRESS");

            // Assert
            Assert.That(tier, Is.EqualTo(SubscriptionTier.Free));
        }

        [Test]
        public async Task SetAndGetUserTier_ShouldPersist()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Premium);

            // Act
            var tier = await _tierService.GetUserTierAsync(TestUser);

            // Assert
            Assert.That(tier, Is.EqualTo(SubscriptionTier.Premium));
        }

        [Test]
        public async Task ValidateOperation_FreeTier_UnderLimit_ShouldAllow()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Free);

            // Act
            var result = await _tierService.ValidateOperationAsync(TestUser, TestAssetId, 5, 1);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.Tier, Is.EqualTo(SubscriptionTier.Free));
            Assert.That(result.CurrentCount, Is.EqualTo(5));
            Assert.That(result.MaxAllowed, Is.EqualTo(10));
        }

        [Test]
        public async Task ValidateOperation_FreeTier_AtLimit_ShouldAllow()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Free);

            // Act
            var result = await _tierService.ValidateOperationAsync(TestUser, TestAssetId, 9, 1);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.CurrentCount, Is.EqualTo(9));
            Assert.That(result.MaxAllowed, Is.EqualTo(10));
        }

        [Test]
        public async Task ValidateOperation_FreeTier_OverLimit_ShouldDeny()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Free);

            // Act
            var result = await _tierService.ValidateOperationAsync(TestUser, TestAssetId, 10, 1);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.DenialReason, Does.Contain("Free"));
            Assert.That(result.DenialReason, Does.Contain("limit exceeded"));
            Assert.That(result.DenialReason, Does.Contain("10"));
        }

        [Test]
        public async Task ValidateOperation_EnterpriseTier_LargeNumber_ShouldAllow()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Enterprise);

            // Act
            var result = await _tierService.ValidateOperationAsync(TestUser, TestAssetId, 10000, 1);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.MaxAllowed, Is.EqualTo(-1)); // Unlimited
        }

        [Test]
        public async Task IsBulkOperationEnabled_FreeTier_ShouldReturnFalse()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Free);

            // Act
            var result = await _tierService.IsBulkOperationEnabledAsync(TestUser);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task IsBulkOperationEnabled_PremiumTier_ShouldReturnTrue()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Premium);

            // Act
            var result = await _tierService.IsBulkOperationEnabledAsync(TestUser);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task IsAuditLogEnabled_FreeTier_ShouldReturnFalse()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Free);

            // Act
            var result = await _tierService.IsAuditLogEnabledAsync(TestUser);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task IsAuditLogEnabled_BasicTier_ShouldReturnTrue()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Basic);

            // Act
            var result = await _tierService.IsAuditLogEnabledAsync(TestUser);

            // Assert
            Assert.That(result, Is.True);
        }

        #endregion

        #region Whitelist Service Integration Tests

        [Test]
        public async Task AddEntry_FreeTier_UnderLimit_ShouldSucceed()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Free);

            var request = new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = TestAddress1,
                Status = WhitelistStatus.Active
            };

            // Act
            var result = await _whitelistService.AddEntryAsync(request, TestUser);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entry, Is.Not.Null);
        }

        [Test]
        public async Task AddEntry_FreeTier_OverLimit_ShouldFail()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Free);

            // Add 10 entries to reach the limit
            // Note: Using intentionally invalid address format for counting test only.
            // These are not validated as real Algorand addresses in this test scenario.
            for (int i = 0; i < 10; i++)
            {
                await _repository.AddEntryAsync(new WhitelistEntry
                {
                    AssetId = TestAssetId,
                    Address = $"ADDRESS{i:D50}",  // Mock addresses for counting
                    Status = WhitelistStatus.Active,
                    CreatedBy = TestUser,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Act - Try to add 11th entry
            var request = new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = TestAddress1,
                Status = WhitelistStatus.Active
            };

            var result = await _whitelistService.AddEntryAsync(request, TestUser);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Free"));
            Assert.That(result.ErrorMessage, Does.Contain("limit exceeded"));
        }

        [Test]
        public async Task BulkAdd_FreeTier_ShouldFail()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Free);

            var request = new BulkAddWhitelistRequest
            {
                AssetId = TestAssetId,
                Addresses = new List<string> { TestAddress1, TestAddress2 },
                Status = WhitelistStatus.Active
            };

            // Act
            var result = await _whitelistService.BulkAddEntriesAsync(request, TestUser);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Bulk operations"));
            Assert.That(result.ErrorMessage, Does.Contain("not available"));
        }

        [Test]
        public async Task BulkAdd_PremiumTier_ShouldSucceed()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Premium);

            // Use a single valid address - the main goal is to test that bulk operations are allowed
            var request = new BulkAddWhitelistRequest
            {
                AssetId = TestAssetId,
                Addresses = new List<string> { TestAddress1 },
                Status = WhitelistStatus.Active
            };

            // Act
            var result = await _whitelistService.BulkAddEntriesAsync(request, TestUser);

            // Assert
            // The key assertion is that bulk operations are ENABLED (not rejected due to tier)
            // Individual address validation is a separate concern
            Assert.That(result.Success, Is.True, $"Expected success. Error: {result.ErrorMessage}");
            Assert.That(result.SuccessCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AddEntry_EnterpriseTier_NoLimits_ShouldSucceed()
        {
            // Arrange
            _tierService.SetUserTier(TestUser, SubscriptionTier.Enterprise);

            var request = new AddWhitelistEntryRequest
            {
                AssetId = TestAssetId,
                Address = TestAddress1,
                Status = WhitelistStatus.Active
            };

            // Act
            var result = await _whitelistService.AddEntryAsync(request, TestUser);

            // Assert
            Assert.That(result.Success, Is.True);
        }

        #endregion
    }
}
