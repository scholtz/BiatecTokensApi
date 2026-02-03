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
            var tierService = context.HttpContext.RequestServices.GetService<ISubscriptionTierService>();
            var logger = context.HttpContext.RequestServices.GetService<ILogger<TokenDeploymentSubscriptionAttribute>>();

            if (tierService == null || logger == null)
            {
                logger?.LogWarning("TokenDeploymentSubscriptionAttribute: Required services not available");
                await next();
                return;
            }

            // Get user address from claims
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

            // Sanitize user address for logging
            var sanitizedUserAddress = LoggingHelper.SanitizeLogInput(userAddress);

            // Check if user can deploy tokens
            var canDeploy = await tierService.CanDeployTokenAsync(userAddress);

            if (!canDeploy)
            {
                // Get tier details for error message
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

            // Execute the action
            var executedContext = await next();

            // If the action was successful (2xx status code), record the deployment
            if (executedContext.Result is ObjectResult objectResult)
            {
                var statusCode = objectResult.StatusCode ?? 200;
                if (statusCode >= 200 && statusCode < 300)
                {
                    // Record the deployment
                    await tierService.RecordTokenDeploymentAsync(userAddress);

                    var newCount = await tierService.GetTokenDeploymentCountAsync(userAddress);
                    logger.LogInformation(
                        "Token deployment recorded for user {UserAddress}. New deployment count: {Count}. CorrelationId: {CorrelationId}",
                        sanitizedUserAddress, newCount, context.HttpContext.TraceIdentifier);
                }
            }
        }
    }
}
