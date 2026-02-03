using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
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
    /// appropriate tier permissions before proceeding with token deployment operations.
    /// When validation fails, it returns a clear error response that can be used to prompt
    /// users to upgrade their subscription.
    /// </remarks>
    public class SubscriptionTierValidationAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// Gets or sets the minimum tier required for the operation
        /// </summary>
        public string MinimumTier { get; set; } = "Free";

        /// <summary>
        /// Gets or sets whether this is a premium feature
        /// </summary>
        public bool RequiresPremium { get; set; } = false;

        /// <summary>
        /// Called before the action executes to validate subscription tier
        /// </summary>
        /// <param name="context">The action executing context</param>
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Get required services from DI
            var tierService = context.HttpContext.RequestServices.GetService<ISubscriptionTierService>();
            var logger = context.HttpContext.RequestServices.GetService<ILogger<SubscriptionTierValidationAttribute>>();

            if (tierService == null || logger == null)
            {
                logger?.LogWarning("SubscriptionTierValidationAttribute: Required services not available");
                await next();
                return;
            }

            // Get user address from claims
            var userAddress = context.HttpContext.User.Identity?.Name;
            if (string.IsNullOrEmpty(userAddress))
            {
                logger.LogWarning("SubscriptionTierValidationAttribute: No user identity found");
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

            // Sanitize user address for logging
            var sanitizedUserAddress = LoggingHelper.SanitizeLogInput(userAddress);

            // Get user's current tier
            var currentTier = await tierService.GetUserTierAsync(userAddress);
            var tierLimits = tierService.GetTierLimits(currentTier);

            // Check if premium feature is required and user doesn't have it
            if (RequiresPremium && !tierLimits.BulkOperationsEnabled)
            {
                logger.LogWarning(
                    "Access denied for user {UserAddress}: Premium feature required. Current tier: {Tier}. CorrelationId: {CorrelationId}",
                    sanitizedUserAddress, currentTier, context.HttpContext.TraceIdentifier);

                context.Result = new ObjectResult(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.SUBSCRIPTION_LIMIT_REACHED,
                    ErrorMessage = $"This feature requires a Premium or Enterprise subscription. Your current tier: {currentTier}.",
                    RemediationHint = $"Upgrade to Premium or Enterprise tier to access this feature. Visit the billing page to upgrade your subscription.",
                    Details = new Dictionary<string, object>
                    {
                        { "currentTier", currentTier.ToString() },
                        { "requiredTier", "Premium" },
                        { "featureType", "premium" }
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

            // Log successful validation
            logger.LogDebug(
                "Subscription tier validation passed for user {UserAddress}. Tier: {Tier}. CorrelationId: {CorrelationId}",
                sanitizedUserAddress, currentTier, context.HttpContext.TraceIdentifier);

            await next();
        }
    }
}
