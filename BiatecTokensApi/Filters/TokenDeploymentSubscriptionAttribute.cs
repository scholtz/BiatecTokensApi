using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Entitlement;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BiatecTokensApi.Filters
{
    /// <summary>
    /// Action filter that validates subscription tier entitlements for token deployment operations
    /// </summary>
    /// <remarks>
    /// This filter enforces subscription-based access control by validating that users have
    /// appropriate tier permissions and available deployment quota before proceeding with token deployment.
    /// When validation fails, it returns a clear error response that prompts users to upgrade their subscription.
    /// 
    /// **Enforcement Policy:**
    /// - Free tier: Limited token deployments (3)
    /// - Basic tier: Moderate token deployments (10)
    /// - Premium tier: Generous token deployments (50)
    /// - Enterprise tier: Unlimited token deployments
    /// 
    /// **Usage:**
    /// Apply this attribute to token deployment endpoints:
    /// [TokenDeploymentSubscription]
    /// [HttpPost("create")]
    /// public async Task&lt;IActionResult&gt; CreateToken(...) { ... }
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class TokenDeploymentSubscriptionAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// Called before the action executes to validate subscription tier and deployment quota
        /// </summary>
        /// <param name="context">The action executing context</param>
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Get required services from DI
            var entitlementService = context.HttpContext.RequestServices.GetService<IEntitlementEvaluationService>();
            var arc76ReadinessService = context.HttpContext.RequestServices.GetService<IARC76AccountReadinessService>();
            var tierService = context.HttpContext.RequestServices.GetService<ISubscriptionTierService>();
            var logger = context.HttpContext.RequestServices.GetService<ILogger<TokenDeploymentSubscriptionAttribute>>();

            var correlationId = context.HttpContext.TraceIdentifier;

            // Fallback to legacy tier service if new services not available (for backward compatibility)
            if (entitlementService == null || arc76ReadinessService == null)
            {
                logger?.LogWarning("New entitlement services not available, using legacy tier service. CorrelationId: {CorrelationId}", correlationId);
                await LegacyValidationAsync(context, next, tierService, logger);
                return;
            }

            // Get user address from claims
            var userId = context.HttpContext.User.Identity?.Name;
            if (string.IsNullOrEmpty(userId))
            {
                logger?.LogWarning("TokenDeploymentSubscriptionAttribute: No user identity found. CorrelationId: {CorrelationId}", correlationId);
                context.Result = new UnauthorizedObjectResult(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.UNAUTHORIZED,
                    ErrorMessage = "Authentication required",
                    RemediationHint = "Provide a valid authentication token in the Authorization header.",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = correlationId,
                    Path = LoggingHelper.SanitizeLogInput(context.HttpContext.Request.Path)
                });
                return;
            }

            var sanitizedUserId = LoggingHelper.SanitizeLogInput(userId);

            // Check entitlement
            var entitlementRequest = new EntitlementCheckRequest
            {
                UserId = userId,
                Operation = EntitlementOperation.TokenDeployment,
                CorrelationId = correlationId
            };

            var entitlementResult = await entitlementService.CheckEntitlementAsync(entitlementRequest);

            // Check ARC76 account readiness
            var accountReadiness = await arc76ReadinessService.CheckAccountReadinessAsync(userId, correlationId);

            // If entitlement denied, return error with upgrade recommendation
            if (!entitlementResult.IsAllowed)
            {
                logger?.LogWarning(
                    "Token deployment denied for user {UserId}: Entitlement check failed. " +
                    "Tier: {Tier}, Reason: {Reason}, CorrelationId: {CorrelationId}",
                    sanitizedUserId, entitlementResult.SubscriptionTier, entitlementResult.DenialReason, correlationId);

                var errorResponse = new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = entitlementResult.DenialCode ?? ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED,
                    ErrorMessage = entitlementResult.DenialReason ?? "Token deployment not allowed",
                    RemediationHint = BuildRemediationHint(entitlementResult),
                    Details = new Dictionary<string, object>
                    {
                        { "subscriptionTier", entitlementResult.SubscriptionTier },
                        { "policyVersion", entitlementResult.PolicyVersion }
                    },
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = correlationId,
                    Path = LoggingHelper.SanitizeLogInput(context.HttpContext.Request.Path)
                };

                // Add upgrade recommendation if available
                if (entitlementResult.UpgradeRecommendation != null)
                {
                    errorResponse.Details["upgradeRecommendation"] = entitlementResult.UpgradeRecommendation;
                }

                // Add usage information if available
                if (entitlementResult.CurrentUsage != null)
                {
                    errorResponse.Details["currentUsage"] = entitlementResult.CurrentUsage;
                }

                if (entitlementResult.MaxAllowed != null)
                {
                    errorResponse.Details["maxAllowed"] = entitlementResult.MaxAllowed;
                }

                context.Result = new ObjectResult(errorResponse)
                {
                    StatusCode = StatusCodes.Status402PaymentRequired
                };
                return;
            }

            // If account not ready, return error with remediation steps
            if (!accountReadiness.IsReady)
            {
                logger?.LogWarning(
                    "Token deployment denied for user {UserId}: Account not ready. " +
                    "State: {State}, Reason: {Reason}, CorrelationId: {CorrelationId}",
                    sanitizedUserId, accountReadiness.State, accountReadiness.NotReadyReason, correlationId);

                var errorResponse = new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = GetAccountReadinessErrorCode(accountReadiness.State),
                    ErrorMessage = accountReadiness.NotReadyReason ?? "Account is not ready for token deployment",
                    RemediationHint = BuildAccountRemediationHint(accountReadiness),
                    Details = new Dictionary<string, object>
                    {
                        { "accountState", accountReadiness.State.ToString() },
                        { "accountAddress", accountReadiness.AccountAddress ?? "Not available" }
                    },
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = correlationId,
                    Path = LoggingHelper.SanitizeLogInput(context.HttpContext.Request.Path)
                };

                if (accountReadiness.RemediationSteps != null && accountReadiness.RemediationSteps.Any())
                {
                    errorResponse.Details["remediationSteps"] = accountReadiness.RemediationSteps;
                }

                context.Result = new ObjectResult(errorResponse)
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable
                };
                return;
            }

            logger?.LogDebug(
                "Token deployment authorization passed for user {UserId}. " +
                "Tier: {Tier}, AccountState: {AccountState}, CorrelationId: {CorrelationId}",
                sanitizedUserId, entitlementResult.SubscriptionTier, accountReadiness.State, correlationId);

            // Execute the action
            var executedContext = await next();

            // If the action was successful (2xx status code), record the deployment
            var statusCode = executedContext.HttpContext.Response.StatusCode;
            if (statusCode >= 200 && statusCode < 300)
            {
                // Record the deployment through tier service (for backward compatibility)
                if (tierService != null)
                {
                    await tierService.RecordTokenDeploymentAsync(userId);
                    logger?.LogInformation(
                        "Token deployment recorded for user {UserId}. CorrelationId: {CorrelationId}",
                        sanitizedUserId, correlationId);
                }
            }
        }

        #region Private Helper Methods

        private string GetAccountReadinessErrorCode(Models.ARC76.ARC76ReadinessState state)
        {
            return state switch
            {
                Models.ARC76.ARC76ReadinessState.NotInitialized => ErrorCodes.ACCOUNT_NOT_READY,
                Models.ARC76.ARC76ReadinessState.Initializing => ErrorCodes.ACCOUNT_INITIALIZING,
                Models.ARC76.ARC76ReadinessState.Degraded => ErrorCodes.ACCOUNT_DEGRADED,
                Models.ARC76.ARC76ReadinessState.Failed => ErrorCodes.ACCOUNT_INITIALIZATION_FAILED,
                _ => ErrorCodes.ACCOUNT_NOT_READY
            };
        }

        private string BuildRemediationHint(EntitlementCheckResult result)
        {
            if (result.UpgradeRecommendation != null)
            {
                return $"{result.DenialReason} {result.UpgradeRecommendation.Message}";
            }

            return result.DenialReason ?? "Contact support for assistance.";
        }

        private string BuildAccountRemediationHint(Models.ARC76.ARC76AccountReadinessResult result)
        {
            if (result.RemediationSteps != null && result.RemediationSteps.Any())
            {
                return $"{result.NotReadyReason} Steps to resolve: {string.Join("; ", result.RemediationSteps)}";
            }

            return result.NotReadyReason ?? "Account is not ready. Contact support for assistance.";
        }

        /// <summary>
        /// Legacy validation using tier service (for backward compatibility)
        /// </summary>
        private async Task LegacyValidationAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next,
            ISubscriptionTierService? tierService,
            ILogger<TokenDeploymentSubscriptionAttribute>? logger)
        {
            if (tierService == null || logger == null)
            {
                logger?.LogWarning("TokenDeploymentSubscriptionAttribute: Required services not available");
                await next();
                return;
            }

            var userAddress = context.HttpContext.User.Identity?.Name;
            if (string.IsNullOrEmpty(userAddress))
            {
                logger.LogWarning("TokenDeploymentSubscriptionAttribute: No user identity found");
                context.Result = new UnauthorizedObjectResult(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.UNAUTHORIZED,
                    ErrorMessage = "Authentication required",
                    RemediationHint = "Provide a valid ARC-0014 authentication token in the Authorization header.",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = context.HttpContext.TraceIdentifier,
                    Path = LoggingHelper.SanitizeLogInput(context.HttpContext.Request.Path)
                });
                return;
            }

            var sanitizedUserAddress = LoggingHelper.SanitizeLogInput(userAddress);
            var canDeploy = await tierService.CanDeployTokenAsync(userAddress);

            if (!canDeploy)
            {
                var currentTier = await tierService.GetUserTierAsync(userAddress);
                var tierLimits = tierService.GetTierLimits(currentTier);
                var currentCount = await tierService.GetTokenDeploymentCountAsync(userAddress);

                logger.LogWarning(
                    "Token deployment denied for user {UserAddress}: Deployment limit reached. Current tier: {Tier}, Current count: {Count}, Max: {Max}. CorrelationId: {CorrelationId}",
                    sanitizedUserAddress, currentTier, currentCount, tierLimits.MaxTokenDeployments, context.HttpContext.TraceIdentifier);

                context.Result = new ObjectResult(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.SUBSCRIPTION_LIMIT_REACHED,
                    ErrorMessage = $"Token deployment limit reached for {tierLimits.TierName} tier. You have deployed {currentCount} of {tierLimits.MaxTokenDeployments} allowed tokens.",
                    RemediationHint = $"Upgrade to a higher tier to deploy more tokens. Current tier: {tierLimits.TierName}. Visit the billing page to upgrade your subscription.",
                    Details = new Dictionary<string, object>
                    {
                        { "currentTier", currentTier.ToString() },
                        { "currentDeployments", currentCount },
                        { "maxDeployments", tierLimits.MaxTokenDeployments },
                        { "tierDescription", tierLimits.Description }
                    },
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = context.HttpContext.TraceIdentifier,
                    Path = LoggingHelper.SanitizeLogInput(context.HttpContext.Request.Path)
                })
                {
                    StatusCode = StatusCodes.Status402PaymentRequired
                };
                return;
            }

            var executedContext = await next();
            var statusCode = executedContext.HttpContext.Response.StatusCode;
            if (statusCode >= 200 && statusCode < 300)
            {
                await tierService.RecordTokenDeploymentAsync(userAddress);
                var newCount = await tierService.GetTokenDeploymentCountAsync(userAddress);
                logger.LogInformation(
                    "Token deployment recorded for user {UserAddress}. New deployment count: {Count}. CorrelationId: {CorrelationId}",
                    sanitizedUserAddress, newCount, context.HttpContext.TraceIdentifier);
            }
        }

        #endregion
    }
}
