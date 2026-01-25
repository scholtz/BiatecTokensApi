using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Billing;
using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Repositories;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for billing service functionality
    /// </summary>
    [TestFixture]
    public class BillingServiceIntegrationTests
    {
        private Mock<ILogger<BillingService>> _loggerMock;
        private Mock<ILogger<SubscriptionTierService>> _tierLoggerMock;
        private SubscriptionTierService _tierService;
        private BillingService _billingService;
        private Mock<IOptions<AppConfiguration>> _appConfigMock;

        private const string TestTenantAddress = "VCMJKWOY5P5P7SKMZFFOCEROPJCZOTIJMNIYNUCKH7LRO45JMJP6UYBIJA";
        private const string AdminAddress = "ADMINADDRESS1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<BillingService>>();
            _tierLoggerMock = new Mock<ILogger<SubscriptionTierService>>();
            _tierService = new SubscriptionTierService(_tierLoggerMock.Object);

            var appConfig = new AppConfiguration { Account = AdminAddress };
            _appConfigMock = new Mock<IOptions<AppConfiguration>>();
            _appConfigMock.Setup(x => x.Value).Returns(appConfig);

            _billingService = new BillingService(
                _loggerMock.Object,
                _tierService,
                _appConfigMock.Object);
        }

        #region Usage Summary Tests

        [Test]
        public async Task GetUsageSummary_NewTenant_ReturnsZeroUsage()
        {
            // Act
            var summary = await _billingService.GetUsageSummaryAsync(TestTenantAddress);

            // Assert
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.TenantAddress, Is.EqualTo(TestTenantAddress));
            Assert.That(summary.SubscriptionTier, Is.EqualTo(SubscriptionTier.Free.ToString()));
            Assert.That(summary.TokenIssuanceCount, Is.EqualTo(0));
            Assert.That(summary.TransferValidationCount, Is.EqualTo(0));
            Assert.That(summary.AuditExportCount, Is.EqualTo(0));
            Assert.That(summary.StorageItemsCount, Is.EqualTo(0));
            Assert.That(summary.ComplianceOperationCount, Is.EqualTo(0));
            Assert.That(summary.WhitelistOperationCount, Is.EqualTo(0));
            Assert.That(summary.HasExceededLimits, Is.False);
            Assert.That(summary.LimitViolations, Is.Empty);
        }

        [Test]
        public async Task GetUsageSummary_WithRecordedUsage_ReturnsCorrectCounts()
        {
            // Arrange - record various operations
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.TokenIssuance, 5);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.TransferValidation, 10);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.AuditExport, 2);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.Storage, 15);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.ComplianceOperation, 7);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.WhitelistOperation, 3);

            // Act
            var summary = await _billingService.GetUsageSummaryAsync(TestTenantAddress);

            // Assert
            Assert.That(summary.TokenIssuanceCount, Is.EqualTo(5));
            Assert.That(summary.TransferValidationCount, Is.EqualTo(10));
            Assert.That(summary.AuditExportCount, Is.EqualTo(2));
            Assert.That(summary.StorageItemsCount, Is.EqualTo(15));
            Assert.That(summary.ComplianceOperationCount, Is.EqualTo(7));
            Assert.That(summary.WhitelistOperationCount, Is.EqualTo(3));
        }

        [Test]
        public async Task GetUsageSummary_InvalidAddress_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _billingService.GetUsageSummaryAsync(""));
            
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _billingService.GetUsageSummaryAsync(null!));
        }

        [Test]
        public async Task GetUsageSummary_WithCustomLimits_ShowsCustomLimits()
        {
            // Arrange - set custom limits
            var updateRequest = new UpdatePlanLimitsRequest
            {
                TenantAddress = TestTenantAddress,
                Limits = new PlanLimits
                {
                    MaxTokenIssuance = 100,
                    MaxTransferValidations = 1000,
                    MaxAuditExports = 10,
                    MaxStorageItems = 500,
                    MaxComplianceOperations = 200,
                    MaxWhitelistOperations = 300
                }
            };
            await _billingService.UpdatePlanLimitsAsync(updateRequest, AdminAddress);

            // Act
            var summary = await _billingService.GetUsageSummaryAsync(TestTenantAddress);

            // Assert
            Assert.That(summary.CurrentLimits.MaxTokenIssuance, Is.EqualTo(100));
            Assert.That(summary.CurrentLimits.MaxTransferValidations, Is.EqualTo(1000));
            Assert.That(summary.CurrentLimits.MaxAuditExports, Is.EqualTo(10));
            Assert.That(summary.CurrentLimits.MaxStorageItems, Is.EqualTo(500));
            Assert.That(summary.CurrentLimits.MaxComplianceOperations, Is.EqualTo(200));
            Assert.That(summary.CurrentLimits.MaxWhitelistOperations, Is.EqualTo(300));
        }

        [Test]
        public async Task GetUsageSummary_ExceededLimits_ShowsViolations()
        {
            // Arrange - set low limits
            var updateRequest = new UpdatePlanLimitsRequest
            {
                TenantAddress = TestTenantAddress,
                Limits = new PlanLimits
                {
                    MaxTokenIssuance = 5,
                    MaxTransferValidations = 10
                }
            };
            await _billingService.UpdatePlanLimitsAsync(updateRequest, AdminAddress);

            // Record usage exceeding limits
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.TokenIssuance, 10);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.TransferValidation, 20);

            // Act
            var summary = await _billingService.GetUsageSummaryAsync(TestTenantAddress);

            // Assert
            Assert.That(summary.HasExceededLimits, Is.True);
            Assert.That(summary.LimitViolations, Has.Count.EqualTo(2));
            Assert.That(summary.LimitViolations[0], Does.Contain("Token issuance"));
            Assert.That(summary.LimitViolations[1], Does.Contain("Transfer validation"));
        }

        #endregion

        #region Limit Check Tests

        [Test]
        public async Task CheckLimit_UnlimitedPlan_AlwaysAllows()
        {
            // Arrange - default free tier has unlimited for most operations
            var request = new LimitCheckRequest
            {
                OperationType = OperationType.TokenIssuance,
                OperationCount = 1000
            };

            // Act
            var result = await _billingService.CheckLimitAsync(TestTenantAddress, request);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.MaxAllowed, Is.EqualTo(-1));
            Assert.That(result.RemainingCapacity, Is.EqualTo(-1));
            Assert.That(result.DenialReason, Is.Null);
            Assert.That(result.ErrorCode, Is.Null);
        }

        [Test]
        public async Task CheckLimit_WithinLimit_Allows()
        {
            // Arrange - set limit and use some capacity
            var updateRequest = new UpdatePlanLimitsRequest
            {
                TenantAddress = TestTenantAddress,
                Limits = new PlanLimits { MaxTokenIssuance = 100 }
            };
            await _billingService.UpdatePlanLimitsAsync(updateRequest, AdminAddress);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.TokenIssuance, 50);

            var checkRequest = new LimitCheckRequest
            {
                OperationType = OperationType.TokenIssuance,
                OperationCount = 30
            };

            // Act
            var result = await _billingService.CheckLimitAsync(TestTenantAddress, checkRequest);

            // Assert
            Assert.That(result.IsAllowed, Is.True);
            Assert.That(result.CurrentUsage, Is.EqualTo(50));
            Assert.That(result.MaxAllowed, Is.EqualTo(100));
            Assert.That(result.RemainingCapacity, Is.EqualTo(50));
        }

        [Test]
        public async Task CheckLimit_ExceedsLimit_Denies()
        {
            // Arrange - set limit and use most capacity
            var updateRequest = new UpdatePlanLimitsRequest
            {
                TenantAddress = TestTenantAddress,
                Limits = new PlanLimits { MaxTokenIssuance = 100 }
            };
            await _billingService.UpdatePlanLimitsAsync(updateRequest, AdminAddress);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.TokenIssuance, 95);

            var checkRequest = new LimitCheckRequest
            {
                OperationType = OperationType.TokenIssuance,
                OperationCount = 10
            };

            // Act
            var result = await _billingService.CheckLimitAsync(TestTenantAddress, checkRequest);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.CurrentUsage, Is.EqualTo(95));
            Assert.That(result.MaxAllowed, Is.EqualTo(100));
            Assert.That(result.RemainingCapacity, Is.EqualTo(5));
            Assert.That(result.ErrorCode, Is.EqualTo("LIMIT_EXCEEDED"));
            Assert.That(result.DenialReason, Does.Contain("exceed"));
            Assert.That(result.DenialReason, Does.Contain("95"));
            Assert.That(result.DenialReason, Does.Contain("10"));
            Assert.That(result.DenialReason, Does.Contain("100"));
        }

        [Test]
        public async Task CheckLimit_ExactlyAtLimit_Denies()
        {
            // Arrange
            var updateRequest = new UpdatePlanLimitsRequest
            {
                TenantAddress = TestTenantAddress,
                Limits = new PlanLimits { MaxTokenIssuance = 100 }
            };
            await _billingService.UpdatePlanLimitsAsync(updateRequest, AdminAddress);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.TokenIssuance, 100);

            var checkRequest = new LimitCheckRequest
            {
                OperationType = OperationType.TokenIssuance,
                OperationCount = 1
            };

            // Act
            var result = await _billingService.CheckLimitAsync(TestTenantAddress, checkRequest);

            // Assert
            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.RemainingCapacity, Is.EqualTo(0));
        }

        [Test]
        public async Task CheckLimit_InvalidInput_ThrowsException()
        {
            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _billingService.CheckLimitAsync("", new LimitCheckRequest()));

            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _billingService.CheckLimitAsync(TestTenantAddress, null!));
        }

        [Test]
        public async Task CheckLimit_DenialLogsAuditEntry()
        {
            // Arrange - set very low limit
            var updateRequest = new UpdatePlanLimitsRequest
            {
                TenantAddress = TestTenantAddress,
                Limits = new PlanLimits { MaxTokenIssuance = 1 }
            };
            await _billingService.UpdatePlanLimitsAsync(updateRequest, AdminAddress);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.TokenIssuance, 1);

            var checkRequest = new LimitCheckRequest
            {
                OperationType = OperationType.TokenIssuance,
                OperationCount = 1,
                AssetId = 12345,
                Network = "testnet"
            };

            // Act
            var result = await _billingService.CheckLimitAsync(TestTenantAddress, checkRequest);

            // Assert
            Assert.That(result.IsAllowed, Is.False);

            // Verify audit log entry was logged (check logger was called)
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("BILLING_AUDIT") && v.ToString()!.Contains("LimitCheckDenied")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Plan Limits Management Tests

        [Test]
        public async Task UpdatePlanLimits_AsAdmin_Succeeds()
        {
            // Arrange
            var request = new UpdatePlanLimitsRequest
            {
                TenantAddress = TestTenantAddress,
                Limits = new PlanLimits
                {
                    MaxTokenIssuance = 500,
                    MaxTransferValidations = 5000
                },
                Notes = "Custom enterprise plan"
            };

            // Act
            var result = await _billingService.UpdatePlanLimitsAsync(request, AdminAddress);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Limits, Is.Not.Null);
            Assert.That(result.Limits!.MaxTokenIssuance, Is.EqualTo(500));
            Assert.That(result.Limits.MaxTransferValidations, Is.EqualTo(5000));
        }

        [Test]
        public async Task UpdatePlanLimits_AsNonAdmin_Fails()
        {
            // Arrange
            var request = new UpdatePlanLimitsRequest
            {
                TenantAddress = TestTenantAddress,
                Limits = new PlanLimits { MaxTokenIssuance = 500 }
            };
            var nonAdminAddress = "NONADMIN1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

            // Act
            var result = await _billingService.UpdatePlanLimitsAsync(request, nonAdminAddress);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Unauthorized"));
        }

        [Test]
        public async Task UpdatePlanLimits_LogsAuditEntry()
        {
            // Arrange
            var request = new UpdatePlanLimitsRequest
            {
                TenantAddress = TestTenantAddress,
                Limits = new PlanLimits { MaxTokenIssuance = 500 },
                Notes = "Test update"
            };

            // Act
            await _billingService.UpdatePlanLimitsAsync(request, AdminAddress);

            // Assert - verify audit log entry was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("BILLING_AUDIT") && v.ToString()!.Contains("PlanLimitUpdate")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Test]
        public async Task GetPlanLimits_WithoutCustomLimits_ReturnsTierDefaults()
        {
            // Arrange - set tier to Basic
            _tierService.SetUserTier(TestTenantAddress, SubscriptionTier.Basic);

            // Act
            var limits = await _billingService.GetPlanLimitsAsync(TestTenantAddress);

            // Assert - should have tier-based limits
            Assert.That(limits, Is.Not.Null);
            Assert.That(limits.MaxStorageItems, Is.EqualTo(100)); // Basic tier limit
        }

        [Test]
        public async Task GetPlanLimits_WithCustomLimits_ReturnsCustomLimits()
        {
            // Arrange - set custom limits
            var updateRequest = new UpdatePlanLimitsRequest
            {
                TenantAddress = TestTenantAddress,
                Limits = new PlanLimits
                {
                    MaxTokenIssuance = 999,
                    MaxTransferValidations = 9999
                }
            };
            await _billingService.UpdatePlanLimitsAsync(updateRequest, AdminAddress);

            // Act
            var limits = await _billingService.GetPlanLimitsAsync(TestTenantAddress);

            // Assert - should return custom limits
            Assert.That(limits.MaxTokenIssuance, Is.EqualTo(999));
            Assert.That(limits.MaxTransferValidations, Is.EqualTo(9999));
        }

        #endregion

        #region Admin Authorization Tests

        [Test]
        public void IsAdmin_ConfiguredAdminAddress_ReturnsTrue()
        {
            // Act
            var isAdmin = _billingService.IsAdmin(AdminAddress);

            // Assert
            Assert.That(isAdmin, Is.True);
        }

        [Test]
        public void IsAdmin_NonAdminAddress_ReturnsFalse()
        {
            // Act
            var isAdmin = _billingService.IsAdmin(TestTenantAddress);

            // Assert
            Assert.That(isAdmin, Is.False);
        }

        [Test]
        public void IsAdmin_NullOrEmptyAddress_ReturnsFalse()
        {
            // Act & Assert
            Assert.That(_billingService.IsAdmin(null!), Is.False);
            Assert.That(_billingService.IsAdmin(""), Is.False);
            Assert.That(_billingService.IsAdmin("   "), Is.False);
        }

        #endregion

        #region Usage Recording Tests

        [Test]
        public async Task RecordUsage_ValidOperation_IncrementsCount()
        {
            // Arrange
            var initialSummary = await _billingService.GetUsageSummaryAsync(TestTenantAddress);
            var initialCount = initialSummary.TokenIssuanceCount;

            // Act
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.TokenIssuance, 5);

            // Assert
            var updatedSummary = await _billingService.GetUsageSummaryAsync(TestTenantAddress);
            Assert.That(updatedSummary.TokenIssuanceCount, Is.EqualTo(initialCount + 5));
        }

        [Test]
        public async Task RecordUsage_MultipleOperations_TracksIndependently()
        {
            // Act - record different operation types
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.TokenIssuance, 3);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.TransferValidation, 7);
            await _billingService.RecordUsageAsync(TestTenantAddress, OperationType.ComplianceOperation, 2);

            // Assert - each should be tracked independently
            var summary = await _billingService.GetUsageSummaryAsync(TestTenantAddress);
            Assert.That(summary.TokenIssuanceCount, Is.EqualTo(3));
            Assert.That(summary.TransferValidationCount, Is.EqualTo(7));
            Assert.That(summary.ComplianceOperationCount, Is.EqualTo(2));
            Assert.That(summary.AuditExportCount, Is.EqualTo(0));
        }

        [Test]
        public async Task RecordUsage_NullAddress_DoesNotThrow()
        {
            // Act & Assert - should handle gracefully
            Assert.DoesNotThrowAsync(async () =>
                await _billingService.RecordUsageAsync(null!, OperationType.TokenIssuance));
        }

        #endregion

        #region Multiple Tenants Tests

        [Test]
        public async Task GetUsageSummary_MultipleTenants_TracksSeparately()
        {
            // Arrange
            var tenant1 = TestTenantAddress;
            var tenant2 = "TENANT2XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXYYY";

            // Act - record different usage for each tenant
            await _billingService.RecordUsageAsync(tenant1, OperationType.TokenIssuance, 10);
            await _billingService.RecordUsageAsync(tenant2, OperationType.TokenIssuance, 20);

            // Assert - each tenant has independent usage
            var summary1 = await _billingService.GetUsageSummaryAsync(tenant1);
            var summary2 = await _billingService.GetUsageSummaryAsync(tenant2);

            Assert.That(summary1.TokenIssuanceCount, Is.EqualTo(10));
            Assert.That(summary2.TokenIssuanceCount, Is.EqualTo(20));
        }

        [Test]
        public async Task UpdatePlanLimits_MultipleTenants_AffectsOnlySpecifiedTenant()
        {
            // Arrange
            var tenant1 = TestTenantAddress;
            var tenant2 = "TENANT2XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXYYY";

            // Act - set custom limits only for tenant1
            var updateRequest = new UpdatePlanLimitsRequest
            {
                TenantAddress = tenant1,
                Limits = new PlanLimits { MaxTokenIssuance = 100 }
            };
            await _billingService.UpdatePlanLimitsAsync(updateRequest, AdminAddress);

            // Assert - tenant1 has custom limits, tenant2 has defaults
            var limits1 = await _billingService.GetPlanLimitsAsync(tenant1);
            var limits2 = await _billingService.GetPlanLimitsAsync(tenant2);

            Assert.That(limits1.MaxTokenIssuance, Is.EqualTo(100));
            Assert.That(limits2.MaxTokenIssuance, Is.EqualTo(-1)); // Default unlimited
        }

        #endregion
    }
}
