using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Admin endpoints for subscription management and metrics
    /// </summary>
    /// <remarks>
    /// These endpoints require administrative privileges and are intended for internal operations such as
    /// manual tier overrides (for enterprise contracts, beta invites, support escalations) and platform-level
    /// subscription analytics. No PII is returned in metrics endpoints.
    /// </remarks>
    [ApiController]
    [Route("api/v1/admin/subscription")]
    [Authorize]
    public class AdminSubscriptionController : ControllerBase
    {
        private readonly IStripeService _stripeService;
        private readonly ILogger<AdminSubscriptionController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminSubscriptionController"/> class.
        /// </summary>
        /// <param name="stripeService">The Stripe/subscription service</param>
        /// <param name="logger">The logger instance</param>
        public AdminSubscriptionController(
            IStripeService stripeService,
            ILogger<AdminSubscriptionController> logger)
        {
            _stripeService = stripeService;
            _logger = logger;
        }

        /// <summary>
        /// Manually overrides a user's subscription tier
        /// </summary>
        /// <param name="request">Override request with userId and target tier</param>
        /// <returns>Override result</returns>
        /// <remarks>
        /// This endpoint allows administrators to manually set a user's subscription tier, bypassing normal
        /// payment flows. Useful for enterprise contracts signed outside Stripe, beta invites, white-label deals,
        /// or support escalations.
        ///
        /// **Requires Admin Role:**
        /// This endpoint requires the authenticated user to have admin privileges (currently enforced by [Authorize]).
        /// Future versions will add role-based access control.
        ///
        /// **Audit Trail:**
        /// All overrides are recorded in the subscription audit log with the provided reason.
        ///
        /// **Effect:**
        /// The override takes effect immediately. The user's effective tier is updated in the access control
        /// cache within 5 seconds (cache TTL).
        ///
        /// **Use Cases:**
        /// - Enterprise contracts signed offline
        /// - Beta user onboarding
        /// - Support escalations (temporary upgrades)
        /// - White-label partner provisioning
        /// </remarks>
        [HttpPost("override")]
        [ProducesResponseType(typeof(SubscriptionOverrideResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> OverrideSubscriptionTier([FromBody] SubscriptionOverrideRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(request.UserId))
                {
                    return BadRequest(new SubscriptionOverrideResponse
                    {
                        Success = false,
                        ErrorMessage = "UserId is required"
                    });
                }

                var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown-admin";

                _logger.LogInformation(
                    "Admin {AdminId} overriding subscription tier for user {UserId} to {Tier}",
                    LoggingHelper.SanitizeLogInput(adminId), LoggingHelper.SanitizeLogInput(request.UserId), request.Tier);

                var response = await _stripeService.OverrideSubscriptionTierAsync(
                    request.UserId,
                    request.Tier,
                    request.Reason);

                if (!response.Success)
                {
                    _logger.LogWarning(
                        "Failed to override subscription for user {UserId}: {Error}",
                        LoggingHelper.SanitizeLogInput(request.UserId), response.ErrorMessage);
                    return BadRequest(response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error overriding subscription tier for user {UserId}", LoggingHelper.SanitizeLogInput(request?.UserId ?? "unknown"));
                return StatusCode(StatusCodes.Status500InternalServerError, new SubscriptionOverrideResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while overriding subscription tier"
                });
            }
        }

        /// <summary>
        /// Returns aggregate subscription metrics for the platform
        /// </summary>
        /// <returns>Aggregate metrics (no PII)</returns>
        /// <remarks>
        /// Returns platform-level subscription analytics including MRR, tier distribution, churn rate,
        /// and trial conversion metrics. No personally identifiable information (PII) is returned.
        ///
        /// **Metrics Included:**
        /// - MRR (Monthly Recurring Revenue) in cents to avoid floating-point errors
        /// - Total active subscribers by tier
        /// - Trial-to-paid conversion rate
        /// - Churn count for current 30-day rolling window
        /// - Tier distribution (count per tier)
        ///
        /// **Requires Admin Role:**
        /// This endpoint requires the authenticated user to have admin privileges.
        ///
        /// **Use Cases:**
        /// - Monthly business reporting
        /// - Investor metrics package
        /// - Product team dashboards
        /// - Churn analysis and early warning
        /// </remarks>
        [HttpGet("metrics")]
        [ProducesResponseType(typeof(SubscriptionMetricsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMetrics()
        {
            try
            {
                var metrics = await _stripeService.GetAdminMetricsAsync();

                _logger.LogInformation(
                    "Admin metrics retrieved: MRR={MrrCents} cents, ActiveSubscribers={Active}, Trialing={Trialing}",
                    metrics.MrrCents, metrics.TotalActiveSubscribers, metrics.TotalTrialingUsers);

                return Ok(new SubscriptionMetricsResponse
                {
                    Success = true,
                    Metrics = metrics
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subscription metrics");
                return StatusCode(StatusCodes.Status500InternalServerError, new SubscriptionMetricsResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while retrieving subscription metrics"
                });
            }
        }
    }
}
