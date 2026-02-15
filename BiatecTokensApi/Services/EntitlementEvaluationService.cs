using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Entitlement;
using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for evaluating subscription entitlements and enforcing policy rules
    /// </summary>
    public class EntitlementEvaluationService : IEntitlementEvaluationService
    {
        private readonly ISubscriptionTierService _subscriptionTierService;
        private readonly ILogger<EntitlementEvaluationService> _logger;

        // Current active policy version
        private const string CURRENT_POLICY_VERSION = "2026.02.15.1";

        // In-memory storage for entitlement audit logs
        private readonly ConcurrentBag<EntitlementAuditEntry> _auditLog;

        public EntitlementEvaluationService(
            ISubscriptionTierService subscriptionTierService,
            ILogger<EntitlementEvaluationService> logger)
        {
            _subscriptionTierService = subscriptionTierService;
            _logger = logger;
            _auditLog = new ConcurrentBag<EntitlementAuditEntry>();
        }

        /// <inheritdoc/>
        public async Task<EntitlementCheckResult> CheckEntitlementAsync(EntitlementCheckRequest request)
        {
            try
            {
                var sanitizedUserId = LoggingHelper.SanitizeLogInput(request.UserId);
                _logger.LogDebug("Checking entitlement for user {UserId}, operation {Operation}. CorrelationId: {CorrelationId}",
                    sanitizedUserId, request.Operation, request.CorrelationId ?? "N/A");

                // Get user's subscription tier
                var userTier = await _subscriptionTierService.GetUserTierAsync(request.UserId);
                var tierConfig = GetTierConfiguration(userTier);

                // Evaluate entitlement based on operation type
                var result = await EvaluateOperationAsync(request, userTier, tierConfig);

                // Add policy version and correlation ID
                result.PolicyVersion = CURRENT_POLICY_VERSION;
                result.CorrelationId = request.CorrelationId;

                // Log the decision
                _logger.LogInformation(
                    "Entitlement check result for user {UserId}, operation {Operation}: {IsAllowed}. " +
                    "Tier: {Tier}, PolicyVersion: {PolicyVersion}, CorrelationId: {CorrelationId}",
                    sanitizedUserId, request.Operation, result.IsAllowed, userTier, CURRENT_POLICY_VERSION, request.CorrelationId ?? "N/A");

                // Record for audit
                await RecordEntitlementDecisionAsync(result, request.CorrelationId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking entitlement for user {UserId}, operation {Operation}. CorrelationId: {CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.UserId), request.Operation, request.CorrelationId ?? "N/A");

                return new EntitlementCheckResult
                {
                    IsAllowed = false,
                    SubscriptionTier = "Unknown",
                    PolicyVersion = CURRENT_POLICY_VERSION,
                    DenialReason = "Error evaluating entitlement",
                    DenialCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    CorrelationId = request.CorrelationId
                };
            }
        }

        /// <inheritdoc/>
        public Task<EntitlementPolicyVersion> GetActivePolicyVersionAsync()
        {
            var policy = new EntitlementPolicyVersion
            {
                Version = CURRENT_POLICY_VERSION,
                EffectiveDate = new DateTime(2026, 2, 15),
                ChangeDescription = "Initial entitlement policy with Basic/Professional/Enterprise tiers",
                IsActive = true,
                TierConfigurations = new Dictionary<string, TierPolicyConfiguration>
                {
                    { "Free", GetFreeTierPolicy() },
                    { "Basic", GetBasicTierPolicy() },
                    { "Premium", GetPremiumTierPolicy() },
                    { "Enterprise", GetEnterpriseTierPolicy() }
                }
            };

            return Task.FromResult(policy);
        }

        /// <inheritdoc/>
        public async Task<UpgradeRecommendation> GetUpgradeRecommendationAsync(SubscriptionTier currentTier, EntitlementOperation operation)
        {
            _logger.LogDebug("Generating upgrade recommendation for tier {Tier}, operation {Operation}", currentTier, operation);

            return currentTier switch
            {
                SubscriptionTier.Free => await Task.FromResult(GetRecommendationForFree(operation)),
                SubscriptionTier.Basic => await Task.FromResult(GetRecommendationForBasic(operation)),
                SubscriptionTier.Premium => await Task.FromResult(GetRecommendationForPremium(operation)),
                SubscriptionTier.Enterprise => await Task.FromResult(new UpgradeRecommendation
                {
                    CurrentTier = "Enterprise",
                    RecommendedTier = "Enterprise",
                    Message = "You are already on the highest tier",
                    UnlockedFeatures = new List<string>()
                }),
                _ => await Task.FromResult(GetRecommendationForFree(operation))
            };
        }

        /// <inheritdoc/>
        public Task RecordEntitlementDecisionAsync(EntitlementCheckResult result, string? correlationId)
        {
            var auditEntry = new EntitlementAuditEntry
            {
                Timestamp = DateTime.UtcNow,
                SubscriptionTier = result.SubscriptionTier,
                IsAllowed = result.IsAllowed,
                DenialCode = result.DenialCode,
                PolicyVersion = result.PolicyVersion,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString()
            };

            _auditLog.Add(auditEntry);

            _logger.LogDebug("Recorded entitlement decision audit entry. CorrelationId: {CorrelationId}", auditEntry.CorrelationId);

            return Task.CompletedTask;
        }

        #region Private Helper Methods

        private async Task<EntitlementCheckResult> EvaluateOperationAsync(
            EntitlementCheckRequest request,
            SubscriptionTier userTier,
            TierPolicyConfiguration tierConfig)
        {
            var result = new EntitlementCheckResult
            {
                SubscriptionTier = userTier.ToString(),
                Timestamp = DateTime.UtcNow
            };

            switch (request.Operation)
            {
                case EntitlementOperation.TokenDeployment:
                    return await EvaluateTokenDeploymentAsync(request.UserId, userTier, tierConfig);

                case EntitlementOperation.WhitelistAddition:
                    return await EvaluateWhitelistAdditionAsync(request, userTier, tierConfig);

                case EntitlementOperation.ComplianceReport:
                    return await EvaluateComplianceReportAsync(request.UserId, userTier, tierConfig);

                case EntitlementOperation.AuditExport:
                    return await EvaluateAuditExportAsync(request.UserId, userTier, tierConfig);

                case EntitlementOperation.AdvancedCompliance:
                    return EvaluateFeatureToggle(userTier, tierConfig.Features.AdvancedComplianceEnabled, "Advanced Compliance", tierConfig);

                case EntitlementOperation.MultiJurisdiction:
                    return EvaluateFeatureToggle(userTier, tierConfig.Features.MultiJurisdictionEnabled, "Multi-Jurisdiction Support", tierConfig);

                case EntitlementOperation.CustomBranding:
                    return EvaluateFeatureToggle(userTier, tierConfig.Features.CustomBrandingEnabled, "Custom Branding", tierConfig);

                case EntitlementOperation.ApiAccess:
                    return EvaluateFeatureToggle(userTier, tierConfig.Features.ApiAccessEnabled, "API Access", tierConfig);

                case EntitlementOperation.WebhookAccess:
                    return EvaluateFeatureToggle(userTier, tierConfig.Features.WebhooksEnabled, "Webhook Support", tierConfig);

                case EntitlementOperation.BulkOperation:
                    return EvaluateFeatureToggle(userTier, tierConfig.Features.BulkOperationsEnabled, "Bulk Operations", tierConfig);

                default:
                    result.IsAllowed = false;
                    result.DenialReason = $"Unknown operation: {request.Operation}";
                    result.DenialCode = ErrorCodes.INVALID_REQUEST;
                    return result;
            }
        }

        private async Task<EntitlementCheckResult> EvaluateTokenDeploymentAsync(
            string userId,
            SubscriptionTier userTier,
            TierPolicyConfiguration tierConfig)
        {
            var currentCount = await _subscriptionTierService.GetTokenDeploymentCountAsync(userId);
            var maxAllowed = tierConfig.MonthlyTokenDeployments;

            var result = new EntitlementCheckResult
            {
                SubscriptionTier = userTier.ToString(),
                CurrentUsage = new Dictionary<string, object>
                {
                    { "currentDeployments", currentCount }
                },
                MaxAllowed = new Dictionary<string, object>
                {
                    { "maxDeployments", maxAllowed }
                }
            };

            if (maxAllowed == -1)
            {
                result.IsAllowed = true;
                return result;
            }

            if (currentCount >= maxAllowed)
            {
                result.IsAllowed = false;
                result.DenialReason = $"Token deployment limit reached for {tierConfig.TierName} tier. " +
                                     $"You have deployed {currentCount} of {maxAllowed} allowed tokens this month.";
                result.DenialCode = ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED;
                result.UpgradeRecommendation = await GetUpgradeRecommendationAsync(userTier, EntitlementOperation.TokenDeployment);
            }
            else
            {
                result.IsAllowed = true;
            }

            return result;
        }

        private async Task<EntitlementCheckResult> EvaluateWhitelistAdditionAsync(
            EntitlementCheckRequest request,
            SubscriptionTier userTier,
            TierPolicyConfiguration tierConfig)
        {
            var result = new EntitlementCheckResult
            {
                SubscriptionTier = userTier.ToString()
            };

            // Extract context
            var currentCount = 0;
            var additionalCount = 1;

            if (request.OperationContext != null)
            {
                if (request.OperationContext.TryGetValue("currentCount", out var currentObj))
                {
                    currentCount = Convert.ToInt32(currentObj);
                }
                if (request.OperationContext.TryGetValue("additionalCount", out var additionalObj))
                {
                    additionalCount = Convert.ToInt32(additionalObj);
                }
            }

            var maxAllowed = tierConfig.WhitelistedAddressesPerAsset;

            result.CurrentUsage = new Dictionary<string, object>
            {
                { "currentAddresses", currentCount },
                { "requestedAdditions", additionalCount }
            };
            result.MaxAllowed = new Dictionary<string, object>
            {
                { "maxAddresses", maxAllowed }
            };

            if (maxAllowed == -1)
            {
                result.IsAllowed = true;
                return result;
            }

            if ((currentCount + additionalCount) > maxAllowed)
            {
                result.IsAllowed = false;
                result.DenialReason = $"Whitelist limit exceeded for {tierConfig.TierName} tier. " +
                                     $"Current: {currentCount}, Attempting to add: {additionalCount}, Max allowed: {maxAllowed}.";
                result.DenialCode = ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED;
                result.UpgradeRecommendation = await GetUpgradeRecommendationAsync(userTier, EntitlementOperation.WhitelistAddition);
            }
            else
            {
                result.IsAllowed = true;
            }

            return result;
        }

        private Task<EntitlementCheckResult> EvaluateComplianceReportAsync(
            string userId,
            SubscriptionTier userTier,
            TierPolicyConfiguration tierConfig)
        {
            // For now, compliance reports are allowed for all tiers with usage limits
            // In production, you would track monthly usage here
            var result = new EntitlementCheckResult
            {
                IsAllowed = true,
                SubscriptionTier = userTier.ToString(),
                CurrentUsage = new Dictionary<string, object>
                {
                    { "monthlyReports", 0 } // Would query actual usage
                },
                MaxAllowed = new Dictionary<string, object>
                {
                    { "maxMonthlyReports", tierConfig.MonthlyComplianceReports }
                }
            };

            return Task.FromResult(result);
        }

        private Task<EntitlementCheckResult> EvaluateAuditExportAsync(
            string userId,
            SubscriptionTier userTier,
            TierPolicyConfiguration tierConfig)
        {
            if (!tierConfig.Features.AuditLogEnabled)
            {
                var result = new EntitlementCheckResult
                {
                    IsAllowed = false,
                    SubscriptionTier = userTier.ToString(),
                    DenialReason = $"Audit exports are not available in the {tierConfig.TierName} tier",
                    DenialCode = ErrorCodes.FEATURE_NOT_INCLUDED
                };
                result.UpgradeRecommendation = GetUpgradeRecommendationAsync(userTier, EntitlementOperation.AuditExport).Result;
                return Task.FromResult(result);
            }

            // Check monthly limit
            var result2 = new EntitlementCheckResult
            {
                IsAllowed = true,
                SubscriptionTier = userTier.ToString(),
                CurrentUsage = new Dictionary<string, object>
                {
                    { "monthlyExports", 0 } // Would query actual usage
                },
                MaxAllowed = new Dictionary<string, object>
                {
                    { "maxMonthlyExports", tierConfig.MonthlyAuditExports }
                }
            };

            return Task.FromResult(result2);
        }

        private EntitlementCheckResult EvaluateFeatureToggle(
            SubscriptionTier userTier,
            bool isEnabled,
            string featureName,
            TierPolicyConfiguration tierConfig)
        {
            var result = new EntitlementCheckResult
            {
                SubscriptionTier = userTier.ToString()
            };

            if (isEnabled)
            {
                result.IsAllowed = true;
            }
            else
            {
                result.IsAllowed = false;
                result.DenialReason = $"{featureName} is not available in the {tierConfig.TierName} tier";
                result.DenialCode = ErrorCodes.FEATURE_NOT_INCLUDED;
                result.UpgradeRecommendation = GetUpgradeRecommendationAsync(userTier, EntitlementOperation.AdvancedCompliance).Result;
            }

            return result;
        }

        private TierPolicyConfiguration GetTierConfiguration(SubscriptionTier tier)
        {
            return tier switch
            {
                SubscriptionTier.Free => GetFreeTierPolicy(),
                SubscriptionTier.Basic => GetBasicTierPolicy(),
                SubscriptionTier.Premium => GetPremiumTierPolicy(),
                SubscriptionTier.Enterprise => GetEnterpriseTierPolicy(),
                _ => GetFreeTierPolicy()
            };
        }

        private TierPolicyConfiguration GetFreeTierPolicy()
        {
            return new TierPolicyConfiguration
            {
                TierName = "Free",
                MonthlyTokenDeployments = 3,
                ConcurrentDrafts = 2,
                MonthlyComplianceReports = 5,
                MonthlyAuditExports = 0,
                WhitelistedAddressesPerAsset = 10,
                Features = new TierFeatureToggles
                {
                    AdvancedComplianceEnabled = false,
                    MultiJurisdictionEnabled = false,
                    CustomBrandingEnabled = false,
                    ApiAccessEnabled = false,
                    WebhooksEnabled = false,
                    PrioritySupportEnabled = false,
                    SlaEnabled = false,
                    BulkOperationsEnabled = false,
                    AuditLogEnabled = false
                }
            };
        }

        private TierPolicyConfiguration GetBasicTierPolicy()
        {
            return new TierPolicyConfiguration
            {
                TierName = "Basic",
                MonthlyTokenDeployments = 10,
                ConcurrentDrafts = 5,
                MonthlyComplianceReports = 20,
                MonthlyAuditExports = 5,
                WhitelistedAddressesPerAsset = 100,
                Features = new TierFeatureToggles
                {
                    AdvancedComplianceEnabled = false,
                    MultiJurisdictionEnabled = false,
                    CustomBrandingEnabled = false,
                    ApiAccessEnabled = true,
                    WebhooksEnabled = false,
                    PrioritySupportEnabled = false,
                    SlaEnabled = false,
                    BulkOperationsEnabled = false,
                    AuditLogEnabled = true
                }
            };
        }

        private TierPolicyConfiguration GetPremiumTierPolicy()
        {
            return new TierPolicyConfiguration
            {
                TierName = "Premium",
                MonthlyTokenDeployments = 50,
                ConcurrentDrafts = 20,
                MonthlyComplianceReports = 100,
                MonthlyAuditExports = 20,
                WhitelistedAddressesPerAsset = 1000,
                Features = new TierFeatureToggles
                {
                    AdvancedComplianceEnabled = true,
                    MultiJurisdictionEnabled = true,
                    CustomBrandingEnabled = false,
                    ApiAccessEnabled = true,
                    WebhooksEnabled = true,
                    PrioritySupportEnabled = true,
                    SlaEnabled = false,
                    BulkOperationsEnabled = true,
                    AuditLogEnabled = true
                }
            };
        }

        private TierPolicyConfiguration GetEnterpriseTierPolicy()
        {
            return new TierPolicyConfiguration
            {
                TierName = "Enterprise",
                MonthlyTokenDeployments = -1, // Unlimited
                ConcurrentDrafts = -1, // Unlimited
                MonthlyComplianceReports = -1, // Unlimited
                MonthlyAuditExports = -1, // Unlimited
                WhitelistedAddressesPerAsset = -1, // Unlimited
                Features = new TierFeatureToggles
                {
                    AdvancedComplianceEnabled = true,
                    MultiJurisdictionEnabled = true,
                    CustomBrandingEnabled = true,
                    ApiAccessEnabled = true,
                    WebhooksEnabled = true,
                    PrioritySupportEnabled = true,
                    SlaEnabled = true,
                    BulkOperationsEnabled = true,
                    AuditLogEnabled = true
                }
            };
        }

        private UpgradeRecommendation GetRecommendationForFree(EntitlementOperation operation)
        {
            return new UpgradeRecommendation
            {
                CurrentTier = "Free",
                RecommendedTier = "Basic",
                Message = "Upgrade to Basic tier to unlock more token deployments, API access, and audit logs",
                UnlockedFeatures = new List<string>
                {
                    "10 token deployments per month (vs 3)",
                    "100 whitelisted addresses per asset (vs 10)",
                    "API access",
                    "Audit log exports",
                    "20 compliance reports per month (vs 5)"
                },
                LimitIncreases = new Dictionary<string, string>
                {
                    { "Token Deployments", "3 → 10 per month" },
                    { "Whitelisted Addresses", "10 → 100 per asset" },
                    { "Compliance Reports", "5 → 20 per month" }
                },
                UpgradeUrl = "/billing/upgrade?target=basic",
                CostIncrease = "$29/month"
            };
        }

        private UpgradeRecommendation GetRecommendationForBasic(EntitlementOperation operation)
        {
            return new UpgradeRecommendation
            {
                CurrentTier = "Basic",
                RecommendedTier = "Premium",
                Message = "Upgrade to Premium tier to unlock advanced compliance, multi-jurisdiction support, webhooks, and bulk operations",
                UnlockedFeatures = new List<string>
                {
                    "50 token deployments per month (vs 10)",
                    "1,000 whitelisted addresses per asset (vs 100)",
                    "Advanced compliance features",
                    "Multi-jurisdiction support",
                    "Webhook integration",
                    "Bulk operations",
                    "Priority support"
                },
                LimitIncreases = new Dictionary<string, string>
                {
                    { "Token Deployments", "10 → 50 per month" },
                    { "Whitelisted Addresses", "100 → 1,000 per asset" },
                    { "Compliance Reports", "20 → 100 per month" },
                    { "Audit Exports", "5 → 20 per month" }
                },
                UpgradeUrl = "/billing/upgrade?target=premium",
                CostIncrease = "$70/month (total $99/month)"
            };
        }

        private UpgradeRecommendation GetRecommendationForPremium(EntitlementOperation operation)
        {
            return new UpgradeRecommendation
            {
                CurrentTier = "Premium",
                RecommendedTier = "Enterprise",
                Message = "Upgrade to Enterprise tier for unlimited deployments, custom branding, and SLA guarantees",
                UnlockedFeatures = new List<string>
                {
                    "Unlimited token deployments",
                    "Unlimited whitelisted addresses",
                    "Unlimited compliance reports and audit exports",
                    "Custom branding",
                    "SLA guarantees with 99.9% uptime",
                    "Dedicated support team",
                    "Custom integration options"
                },
                LimitIncreases = new Dictionary<string, string>
                {
                    { "Token Deployments", "50 → Unlimited" },
                    { "Whitelisted Addresses", "1,000 → Unlimited" },
                    { "Compliance Reports", "100 → Unlimited" },
                    { "Audit Exports", "20 → Unlimited" }
                },
                UpgradeUrl = "/billing/upgrade?target=enterprise",
                CostIncrease = "$200/month (total $299/month)"
            };
        }

        #endregion

        #region Audit Entry Model

        private class EntitlementAuditEntry
        {
            public DateTime Timestamp { get; set; }
            public string SubscriptionTier { get; set; } = string.Empty;
            public bool IsAllowed { get; set; }
            public string? DenialCode { get; set; }
            public string PolicyVersion { get; set; } = string.Empty;
            public string CorrelationId { get; set; } = string.Empty;
        }

        #endregion
    }
}
