using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Billing;
using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for billing operations including usage tracking and plan limit enforcement
    /// </summary>
    /// <remarks>
    /// This service tracks per-tenant usage across token issuance, transfers, exports, and storage.
    /// It enforces plan limits with clear error codes and maintains audit logs for compliance.
    /// Current implementation uses in-memory storage for usage tracking and plan limits.
    /// </remarks>
    public class BillingService : IBillingService
    {
        private readonly ILogger<BillingService> _logger;
        private readonly ISubscriptionTierService _tierService;
        private readonly IEnterpriseAuditRepository _auditRepository;
        private readonly AppConfiguration _appConfig;

        // In-memory storage for usage tracking (per billing period)
        // Key: tenantAddress, Value: usage data for current period
        private readonly ConcurrentDictionary<string, UsageData> _usageTracking;

        // In-memory storage for custom plan limits (overrides tier defaults)
        // Key: tenantAddress, Value: custom plan limits
        private readonly ConcurrentDictionary<string, PlanLimits> _customPlanLimits;

        // Billing period start (reset monthly)
        private DateTime _currentPeriodStart;

        /// <summary>
        /// Initializes a new instance of the <see cref="BillingService"/> class.
        /// </summary>
        public BillingService(
            ILogger<BillingService> logger,
            ISubscriptionTierService tierService,
            IEnterpriseAuditRepository auditRepository,
            IOptions<AppConfiguration> appConfig)
        {
            _logger = logger;
            _tierService = tierService;
            _auditRepository = auditRepository;
            _appConfig = appConfig.Value;
            _usageTracking = new ConcurrentDictionary<string, UsageData>(StringComparer.OrdinalIgnoreCase);
            _customPlanLimits = new ConcurrentDictionary<string, PlanLimits>(StringComparer.OrdinalIgnoreCase);
            _currentPeriodStart = GetCurrentPeriodStart();
        }

        /// <inheritdoc/>
        public async Task<UsageSummary> GetUsageSummaryAsync(string tenantAddress)
        {
            if (string.IsNullOrWhiteSpace(tenantAddress))
            {
                throw new ArgumentException("Tenant address cannot be null or empty", nameof(tenantAddress));
            }

            CheckAndResetPeriodIfNeeded();

            var tier = await _tierService.GetUserTierAsync(tenantAddress);
            var usage = GetOrCreateUsageData(tenantAddress);
            var limits = await GetPlanLimitsAsync(tenantAddress);

            var summary = new UsageSummary
            {
                TenantAddress = tenantAddress,
                SubscriptionTier = tier.ToString(),
                PeriodStart = _currentPeriodStart,
                PeriodEnd = GetPeriodEnd(_currentPeriodStart),
                TokenIssuanceCount = usage.TokenIssuanceCount,
                TransferValidationCount = usage.TransferValidationCount,
                AuditExportCount = usage.AuditExportCount,
                StorageItemsCount = usage.StorageItemsCount,
                ComplianceOperationCount = usage.ComplianceOperationCount,
                WhitelistOperationCount = usage.WhitelistOperationCount,
                CurrentLimits = limits,
                HasExceededLimits = false,
                LimitViolations = new List<string>()
            };

            // Check for limit violations
            CheckLimitViolations(summary, limits);

            _logger.LogInformation(
                "Retrieved usage summary for tenant {TenantAddress}: " +
                "TokenIssuance={TokenIssuance}, Transfers={Transfers}, Exports={Exports}, " +
                "Storage={Storage}, Compliance={Compliance}, Whitelist={Whitelist}",
                tenantAddress, summary.TokenIssuanceCount, summary.TransferValidationCount,
                summary.AuditExportCount, summary.StorageItemsCount, 
                summary.ComplianceOperationCount, summary.WhitelistOperationCount);

            return summary;
        }

        /// <inheritdoc/>
        public async Task<LimitCheckResponse> CheckLimitAsync(string tenantAddress, LimitCheckRequest request)
        {
            if (string.IsNullOrWhiteSpace(tenantAddress))
            {
                throw new ArgumentException("Tenant address cannot be null or empty", nameof(tenantAddress));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            CheckAndResetPeriodIfNeeded();

            var tier = await _tierService.GetUserTierAsync(tenantAddress);
            var usage = GetOrCreateUsageData(tenantAddress);
            var limits = await GetPlanLimitsAsync(tenantAddress);

            var (currentUsage, maxAllowed) = GetUsageAndLimit(usage, limits, request.OperationType);
            var isAllowed = maxAllowed == -1 || (currentUsage + request.OperationCount) <= maxAllowed;
            var remainingCapacity = maxAllowed == -1 ? -1 : Math.Max(0, maxAllowed - currentUsage);

            var response = new LimitCheckResponse
            {
                IsAllowed = isAllowed,
                CurrentUsage = currentUsage,
                MaxAllowed = maxAllowed,
                RemainingCapacity = remainingCapacity,
                SubscriptionTier = tier.ToString()
            };

            if (!isAllowed)
            {
                response.ErrorCode = "LIMIT_EXCEEDED";
                response.DenialReason = $"Operation would exceed {request.OperationType} limit. " +
                    $"Current: {currentUsage}, Requested: {request.OperationCount}, " +
                    $"Max allowed: {maxAllowed}. Please upgrade your subscription.";

                _logger.LogWarning(
                    "Limit check failed for tenant {TenantAddress}: {OperationType} - " +
                    "Current={Current}, Requested={Requested}, Max={Max}",
                    tenantAddress, request.OperationType, currentUsage, request.OperationCount, maxAllowed);

                // Log denial to audit repository
                await LogLimitDenialAsync(tenantAddress, request, response.DenialReason);
            }
            else
            {
                _logger.LogDebug(
                    "Limit check passed for tenant {TenantAddress}: {OperationType} - " +
                    "Current={Current}, Requested={Requested}, Max={Max}, Remaining={Remaining}",
                    tenantAddress, request.OperationType, currentUsage, request.OperationCount, 
                    maxAllowed, remainingCapacity);
            }

            return response;
        }

        /// <inheritdoc/>
        public async Task<PlanLimitsResponse> UpdatePlanLimitsAsync(UpdatePlanLimitsRequest request, string performedBy)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.TenantAddress))
            {
                return new PlanLimitsResponse
                {
                    Success = false,
                    ErrorMessage = "Tenant address is required"
                };
            }

            if (!IsAdmin(performedBy))
            {
                _logger.LogWarning(
                    "Unauthorized plan limit update attempt by {PerformedBy} for tenant {TenantAddress}",
                    performedBy, request.TenantAddress);

                return new PlanLimitsResponse
                {
                    Success = false,
                    ErrorMessage = "Unauthorized. Only admins can update plan limits."
                };
            }

            // Store custom limits
            _customPlanLimits[request.TenantAddress.ToUpperInvariant()] = request.Limits;

            _logger.LogInformation(
                "Plan limits updated for tenant {TenantAddress} by admin {PerformedBy}: " +
                "TokenIssuance={TokenIssuance}, Transfers={Transfers}, Exports={Exports}, " +
                "Storage={Storage}, Compliance={Compliance}, Whitelist={Whitelist}",
                request.TenantAddress, performedBy,
                request.Limits.MaxTokenIssuance, request.Limits.MaxTransferValidations,
                request.Limits.MaxAuditExports, request.Limits.MaxStorageItems,
                request.Limits.MaxComplianceOperations, request.Limits.MaxWhitelistOperations);

            // Log to audit repository
            await LogPlanLimitUpdateAsync(request, performedBy);

            return new PlanLimitsResponse
            {
                Success = true,
                Limits = request.Limits
            };
        }

        /// <inheritdoc/>
        public async Task<PlanLimits> GetPlanLimitsAsync(string tenantAddress)
        {
            if (string.IsNullOrWhiteSpace(tenantAddress))
            {
                throw new ArgumentException("Tenant address cannot be null or empty", nameof(tenantAddress));
            }

            // Check for custom limits first
            if (_customPlanLimits.TryGetValue(tenantAddress.ToUpperInvariant(), out var customLimits))
            {
                _logger.LogDebug("Using custom plan limits for tenant {TenantAddress}", tenantAddress);
                return customLimits;
            }

            // Otherwise use tier-based limits
            var tier = await _tierService.GetUserTierAsync(tenantAddress);
            var tierLimits = _tierService.GetTierLimits(tier);

            // Map tier limits to plan limits
            var planLimits = new PlanLimits
            {
                MaxTokenIssuance = -1, // Unlimited by default
                MaxTransferValidations = -1,
                MaxAuditExports = tierLimits.AuditLogEnabled ? -1 : 0,
                MaxStorageItems = tierLimits.MaxAddressesPerAsset,
                MaxComplianceOperations = -1,
                MaxWhitelistOperations = -1
            };

            _logger.LogDebug(
                "Using tier-based plan limits for tenant {TenantAddress} (Tier: {Tier})",
                tenantAddress, tier);

            return planLimits;
        }

        /// <inheritdoc/>
        public bool IsAdmin(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            // For now, use a simple check - in production this would check against a database or config
            // The app account is considered an admin
            var adminAddresses = new[] { _appConfig.Account }.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
            
            return adminAddresses.Any(admin => 
                string.Equals(admin, address, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc/>
        public async Task RecordUsageAsync(string tenantAddress, OperationType operationType, int count = 1)
        {
            if (string.IsNullOrWhiteSpace(tenantAddress))
            {
                _logger.LogWarning("RecordUsageAsync called with null or empty tenantAddress");
                return;
            }

            CheckAndResetPeriodIfNeeded();

            var usage = GetOrCreateUsageData(tenantAddress);

            switch (operationType)
            {
                case OperationType.TokenIssuance:
                    usage.TokenIssuanceCount += count;
                    break;
                case OperationType.TransferValidation:
                    usage.TransferValidationCount += count;
                    break;
                case OperationType.AuditExport:
                    usage.AuditExportCount += count;
                    break;
                case OperationType.Storage:
                    usage.StorageItemsCount += count;
                    break;
                case OperationType.ComplianceOperation:
                    usage.ComplianceOperationCount += count;
                    break;
                case OperationType.WhitelistOperation:
                    usage.WhitelistOperationCount += count;
                    break;
            }

            _logger.LogDebug(
                "Recorded usage for tenant {TenantAddress}: {OperationType} += {Count}",
                tenantAddress, operationType, count);

            await Task.CompletedTask;
        }

        #region Private Helper Methods

        private UsageData GetOrCreateUsageData(string tenantAddress)
        {
            return _usageTracking.GetOrAdd(
                tenantAddress.ToUpperInvariant(),
                _ => new UsageData());
        }

        private (int currentUsage, int maxAllowed) GetUsageAndLimit(
            UsageData usage, PlanLimits limits, OperationType operationType)
        {
            return operationType switch
            {
                OperationType.TokenIssuance => (usage.TokenIssuanceCount, limits.MaxTokenIssuance),
                OperationType.TransferValidation => (usage.TransferValidationCount, limits.MaxTransferValidations),
                OperationType.AuditExport => (usage.AuditExportCount, limits.MaxAuditExports),
                OperationType.Storage => (usage.StorageItemsCount, limits.MaxStorageItems),
                OperationType.ComplianceOperation => (usage.ComplianceOperationCount, limits.MaxComplianceOperations),
                OperationType.WhitelistOperation => (usage.WhitelistOperationCount, limits.MaxWhitelistOperations),
                _ => (0, -1)
            };
        }

        private void CheckLimitViolations(UsageSummary summary, PlanLimits limits)
        {
            if (limits.MaxTokenIssuance != -1 && summary.TokenIssuanceCount > limits.MaxTokenIssuance)
            {
                summary.HasExceededLimits = true;
                summary.LimitViolations.Add($"Token issuance limit exceeded: {summary.TokenIssuanceCount}/{limits.MaxTokenIssuance}");
            }

            if (limits.MaxTransferValidations != -1 && summary.TransferValidationCount > limits.MaxTransferValidations)
            {
                summary.HasExceededLimits = true;
                summary.LimitViolations.Add($"Transfer validation limit exceeded: {summary.TransferValidationCount}/{limits.MaxTransferValidations}");
            }

            if (limits.MaxAuditExports != -1 && summary.AuditExportCount > limits.MaxAuditExports)
            {
                summary.HasExceededLimits = true;
                summary.LimitViolations.Add($"Audit export limit exceeded: {summary.AuditExportCount}/{limits.MaxAuditExports}");
            }

            if (limits.MaxStorageItems != -1 && summary.StorageItemsCount > limits.MaxStorageItems)
            {
                summary.HasExceededLimits = true;
                summary.LimitViolations.Add($"Storage limit exceeded: {summary.StorageItemsCount}/{limits.MaxStorageItems}");
            }

            if (limits.MaxComplianceOperations != -1 && summary.ComplianceOperationCount > limits.MaxComplianceOperations)
            {
                summary.HasExceededLimits = true;
                summary.LimitViolations.Add($"Compliance operation limit exceeded: {summary.ComplianceOperationCount}/{limits.MaxComplianceOperations}");
            }

            if (limits.MaxWhitelistOperations != -1 && summary.WhitelistOperationCount > limits.MaxWhitelistOperations)
            {
                summary.HasExceededLimits = true;
                summary.LimitViolations.Add($"Whitelist operation limit exceeded: {summary.WhitelistOperationCount}/{limits.MaxWhitelistOperations}");
            }
        }

        private void CheckAndResetPeriodIfNeeded()
        {
            var currentPeriodStart = GetCurrentPeriodStart();
            if (currentPeriodStart != _currentPeriodStart)
            {
                _logger.LogInformation(
                    "Billing period reset: {OldPeriod} -> {NewPeriod}",
                    _currentPeriodStart, currentPeriodStart);

                _currentPeriodStart = currentPeriodStart;
                _usageTracking.Clear();
            }
        }

        private static DateTime GetCurrentPeriodStart()
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        private static DateTime GetPeriodEnd(DateTime periodStart)
        {
            return periodStart.AddMonths(1).AddSeconds(-1);
        }

        private async Task LogLimitDenialAsync(string tenantAddress, LimitCheckRequest request, string denialReason)
        {
            try
            {
                var auditEntry = new EnterpriseAuditLogEntry
                {
                    Category = Models.AuditEventCategory.Compliance,
                    ActionType = "LimitCheckDenied",
                    PerformedBy = tenantAddress,
                    PerformedAt = DateTime.UtcNow,
                    Success = false,
                    ErrorMessage = denialReason,
                    AssetId = request.AssetId,
                    Network = request.Network,
                    Notes = $"Operation type: {request.OperationType}, Count: {request.OperationCount}"
                };

                await _auditRepository.AddAuditLogEntryAsync(auditEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log limit denial to audit repository");
            }
        }

        private async Task LogPlanLimitUpdateAsync(UpdatePlanLimitsRequest request, string performedBy)
        {
            try
            {
                var auditEntry = new EnterpriseAuditLogEntry
                {
                    Category = Models.AuditEventCategory.Compliance,
                    ActionType = "PlanLimitUpdate",
                    PerformedBy = performedBy,
                    PerformedAt = DateTime.UtcNow,
                    Success = true,
                    AffectedAddress = request.TenantAddress,
                    Notes = request.Notes ?? "Plan limits updated by admin",
                    Role = "Admin"
                };

                await _auditRepository.AddAuditLogEntryAsync(auditEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log plan limit update to audit repository");
            }
        }

        #endregion

        /// <summary>
        /// Internal class for tracking usage data per tenant
        /// </summary>
        private class UsageData
        {
            public int TokenIssuanceCount { get; set; }
            public int TransferValidationCount { get; set; }
            public int AuditExportCount { get; set; }
            public int StorageItemsCount { get; set; }
            public int ComplianceOperationCount { get; set; }
            public int WhitelistOperationCount { get; set; }
        }
    }
}
