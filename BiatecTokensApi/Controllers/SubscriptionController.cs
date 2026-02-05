using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for managing Stripe subscriptions
    /// </summary>
    /// <remarks>
    /// This controller implements the subscription system for the RWA tokenization platform.
    /// It manages subscription lifecycle including checkout, billing portal access, status queries,
    /// and webhook processing for Stripe events. All user-facing endpoints require ARC-0014 authentication.
    /// </remarks>
    [ApiController]
    [Route("api/v1/subscription")]
    public class SubscriptionController : ControllerBase
    {
        private readonly IStripeService _stripeService;
        private readonly ILogger<SubscriptionController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionController"/> class.
        /// </summary>
        /// <param name="stripeService">The Stripe service</param>
        /// <param name="logger">The logger instance</param>
        public SubscriptionController(
            IStripeService stripeService,
            ILogger<SubscriptionController> logger)
        {
            _stripeService = stripeService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a Stripe checkout session for subscription purchase
        /// </summary>
        /// <param name="request">Checkout session request with desired tier</param>
        /// <returns>Checkout session with URL to redirect user</returns>
        /// <remarks>
        /// This endpoint creates a Stripe checkout session for the authenticated user to purchase
        /// a subscription. The user will be redirected to the Stripe checkout page to complete payment.
        /// 
        /// **Authentication:**
        /// Requires ARC-0014 authentication. The authenticated user's address is used as the customer identifier.
        /// 
        /// **Supported Tiers:**
        /// - Basic: Entry-level subscription with basic features
        /// - Premium (Pro): Advanced features for larger deployments
        /// - Enterprise: Full feature set with unlimited capacity
        /// 
        /// **Checkout Flow:**
        /// 1. API creates or retrieves Stripe customer for the user
        /// 2. API creates checkout session with selected subscription tier
        /// 3. User is redirected to Stripe checkout page (CheckoutUrl in response)
        /// 4. User completes payment on Stripe
        /// 5. User is redirected to success URL
        /// 6. Webhook updates subscription state in backend
        /// 
        /// **Error Handling:**
        /// - Returns error if tier is Free (cannot purchase Free tier)
        /// - Returns error if price ID not configured for requested tier
        /// - Returns error if Stripe API fails
        /// 
        /// **Use Cases:**
        /// - Initial subscription purchase
        /// - Upgrade from Free to paid tier
        /// - Tier change handled via billing portal (not checkout)
        /// </remarks>
        [Authorize]
        [HttpPost("checkout")]
        [ProducesResponseType(typeof(CreateCheckoutSessionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userAddress = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userAddress))
                {
                    _logger.LogWarning("CreateCheckoutSession called without authenticated user");
                    return Unauthorized(new CreateCheckoutSessionResponse
                    {
                        Success = false,
                        ErrorMessage = "Authentication required"
                    });
                }

                var response = await _stripeService.CreateCheckoutSessionAsync(userAddress, request.Tier);

                if (!response.Success)
                {
                    _logger.LogWarning(
                        "Failed to create checkout session for user {UserAddress}, tier {Tier}: {Error}",
                        userAddress, request.Tier, response.ErrorMessage);
                    return BadRequest(response);
                }

                _logger.LogInformation(
                    "Created checkout session for user {UserAddress}, tier {Tier}",
                    userAddress, request.Tier);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating checkout session");
                return StatusCode(StatusCodes.Status500InternalServerError, new CreateCheckoutSessionResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while creating checkout session"
                });
            }
        }

        /// <summary>
        /// Creates a Stripe billing portal session for subscription management
        /// </summary>
        /// <param name="request">Billing portal request with optional return URL</param>
        /// <returns>Billing portal session with URL to redirect user</returns>
        /// <remarks>
        /// This endpoint creates a Stripe billing portal session where users can manage their subscription,
        /// including viewing invoices, updating payment methods, upgrading/downgrading tiers, and canceling.
        /// 
        /// **Authentication:**
        /// Requires ARC-0014 authentication. The authenticated user's address is used to look up their Stripe customer.
        /// 
        /// **Portal Capabilities:**
        /// - View subscription details and billing history
        /// - Update payment methods
        /// - Upgrade or downgrade subscription tier
        /// - Cancel subscription (effective at period end)
        /// - Download invoices
        /// 
        /// **Prerequisites:**
        /// User must have an active Stripe customer ID (created during first checkout).
        /// Returns error if user has never subscribed.
        /// 
        /// **Return URL:**
        /// Optional parameter specifying where to redirect user after they exit the portal.
        /// If not provided, uses the configured checkout success URL.
        /// 
        /// **Webhook Integration:**
        /// Any changes made in the portal trigger Stripe webhooks that update the backend state.
        /// 
        /// **Use Cases:**
        /// - Manage existing subscription
        /// - Upgrade/downgrade between tiers
        /// - Cancel subscription
        /// - Update payment information
        /// </remarks>
        [Authorize]
        [HttpPost("billing-portal")]
        [ProducesResponseType(typeof(CreateBillingPortalSessionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateBillingPortalSession([FromBody] CreateBillingPortalSessionRequest? request = null)
        {
            try
            {
                var userAddress = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userAddress))
                {
                    _logger.LogWarning("CreateBillingPortalSession called without authenticated user");
                    return Unauthorized(new CreateBillingPortalSessionResponse
                    {
                        Success = false,
                        ErrorMessage = "Authentication required"
                    });
                }

                var response = await _stripeService.CreateBillingPortalSessionAsync(
                    userAddress,
                    request?.ReturnUrl);

                if (!response.Success)
                {
                    _logger.LogWarning(
                        "Failed to create billing portal session for user {UserAddress}: {Error}",
                        userAddress, response.ErrorMessage);
                    return BadRequest(response);
                }

                _logger.LogInformation(
                    "Created billing portal session for user {UserAddress}",
                    userAddress);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating billing portal session");
                return StatusCode(StatusCodes.Status500InternalServerError, new CreateBillingPortalSessionResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while creating billing portal session"
                });
            }
        }

        /// <summary>
        /// Gets the current subscription status for the authenticated user
        /// </summary>
        /// <returns>Subscription status details</returns>
        /// <remarks>
        /// This endpoint returns the current subscription state for the authenticated user,
        /// including tier, status, billing period information, and Stripe IDs.
        /// 
        /// **Authentication:**
        /// Requires ARC-0014 authentication. The authenticated user's address is used to query subscription state.
        /// 
        /// **Response Fields:**
        /// - UserAddress: User's Algorand address
        /// - StripeCustomerId: Stripe customer ID (null if never subscribed)
        /// - StripeSubscriptionId: Active subscription ID (null if no active subscription)
        /// - Tier: Current subscription tier (Free, Basic, Premium, Enterprise)
        /// - Status: Subscription status (None, Active, PastDue, Canceled, etc.)
        /// - SubscriptionStartDate: When subscription started
        /// - CurrentPeriodStart/End: Current billing period
        /// - CancelAtPeriodEnd: Whether subscription will cancel at period end
        /// - LastUpdated: Last state update timestamp
        /// 
        /// **Free Tier:**
        /// Users without active subscriptions return Free tier with None status.
        /// 
        /// **Use Cases:**
        /// - Display current plan in UI
        /// - Check subscription status before restricted operations
        /// - Show billing period information
        /// - Determine if subscription is expiring
        /// </remarks>
        [Authorize]
        [HttpGet("status")]
        [ProducesResponseType(typeof(SubscriptionStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSubscriptionStatus()
        {
            try
            {
                var userAddress = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userAddress))
                {
                    _logger.LogWarning("GetSubscriptionStatus called without authenticated user");
                    return Unauthorized(new SubscriptionStatusResponse
                    {
                        Success = false,
                        ErrorMessage = "Authentication required"
                    });
                }

                var subscription = await _stripeService.GetSubscriptionStatusAsync(userAddress);

                _logger.LogInformation(
                    "Retrieved subscription status for user {UserAddress}, tier {Tier}, status {Status}",
                    userAddress, subscription.Tier, subscription.Status);

                return Ok(new SubscriptionStatusResponse
                {
                    Success = true,
                    Subscription = subscription
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subscription status");
                return StatusCode(StatusCodes.Status500InternalServerError, new SubscriptionStatusResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while retrieving subscription status"
                });
            }
        }

        /// <summary>
        /// Gets subscription entitlements for the authenticated user
        /// </summary>
        /// <returns>Subscription entitlements (feature access)</returns>
        /// <remarks>
        /// This endpoint returns the feature entitlements for the authenticated user's subscription tier.
        /// Entitlements define what features and limits apply to the user based on their subscription.
        /// 
        /// **Authentication:**
        /// Requires ARC-0014 authentication. The authenticated user's address is used to query entitlements.
        /// 
        /// **Entitlement Fields:**
        /// - MaxTokenDeployments: Maximum tokens deployable per month (-1 = unlimited)
        /// - MaxWhitelistedAddresses: Maximum whitelisted addresses per token (-1 = unlimited)
        /// - MaxComplianceReports: Maximum compliance reports per month (-1 = unlimited)
        /// - AdvancedComplianceEnabled: Whether advanced compliance features are available
        /// - MultiJurisdictionEnabled: Whether multi-jurisdiction support is available
        /// - CustomBrandingEnabled: Whether custom branding is available
        /// - PrioritySupportEnabled: Whether priority support is included
        /// - ApiAccessEnabled: Whether API access is enabled
        /// - WebhooksEnabled: Whether webhook subscriptions are allowed
        /// - AuditExportsEnabled: Whether audit exports are available
        /// - MaxAuditExports: Maximum audit exports per month (-1 = unlimited)
        /// - SlaEnabled: Whether SLA guarantees apply
        /// - SlaUptimePercentage: Uptime guarantee percentage (e.g., 99.9)
        /// 
        /// **Tier Mapping:**
        /// - Free: Limited features, 1 token, 10 whitelist addresses
        /// - Basic: Basic features, 10 tokens, 100 whitelist addresses
        /// - Premium: Advanced features, 100 tokens, 1000 whitelist addresses, 99.5% SLA
        /// - Enterprise: All features unlimited, 99.9% SLA, priority support
        /// 
        /// **Use Cases:**
        /// - Feature gating in frontend UI
        /// - Determining available capabilities
        /// - Displaying current plan benefits
        /// - Upgrade decision support
        /// </remarks>
        [Authorize]
        [HttpGet("entitlements")]
        [ProducesResponseType(typeof(SubscriptionEntitlementsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetEntitlements()
        {
            try
            {
                var userAddress = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userAddress))
                {
                    _logger.LogWarning("GetEntitlements called without authenticated user");
                    return Unauthorized(new SubscriptionEntitlementsResponse
                    {
                        Success = false,
                        ErrorMessage = "Authentication required"
                    });
                }

                var entitlements = await _stripeService.GetEntitlementsAsync(userAddress);

                _logger.LogInformation(
                    "Retrieved entitlements for user {UserAddress}, tier {Tier}",
                    userAddress, entitlements.Tier);

                return Ok(new SubscriptionEntitlementsResponse
                {
                    Success = true,
                    Entitlements = entitlements
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entitlements");
                return StatusCode(StatusCodes.Status500InternalServerError, new SubscriptionEntitlementsResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while retrieving entitlements"
                });
            }
        }

        /// <summary>
        /// Webhook endpoint for processing Stripe events
        /// </summary>
        /// <remarks>
        /// This endpoint receives and processes webhook events from Stripe to keep subscription state
        /// synchronized with Stripe. Events are validated using the webhook secret to ensure authenticity.
        /// 
        /// **No Authentication Required:**
        /// This endpoint does NOT require ARC-0014 authentication as it's called by Stripe servers.
        /// Security is provided by webhook signature validation.
        /// 
        /// **Signature Validation:**
        /// All webhook requests must include a valid Stripe-Signature header.
        /// Invalid signatures are rejected to prevent spoofing.
        /// 
        /// **Idempotency:**
        /// Each webhook event is processed exactly once using the event ID for deduplication.
        /// Duplicate events (retries) are safely ignored.
        /// 
        /// **Supported Events:**
        /// - checkout.session.completed: Checkout completed, customer created
        /// - customer.subscription.created: New subscription activated
        /// - customer.subscription.updated: Subscription tier or status changed
        /// - customer.subscription.deleted: Subscription canceled
        /// - invoice.payment_succeeded: Invoice payment successful
        /// - invoice.payment_failed: Invoice payment failed
        /// - charge.dispute.created: Payment dispute initiated
        /// 
        /// **Event Processing:**
        /// 1. Validate webhook signature
        /// 2. Check event idempotency (skip if already processed)
        /// 3. Update subscription state in backend
        /// 4. Update tier in tier service for access control
        /// 5. Log event to audit trail
        /// 
        /// **Audit Logging:**
        /// All webhook events are logged with structured logging for:
        /// - Compliance reporting
        /// - Billing reconciliation
        /// - Debugging and troubleshooting
        /// - Security monitoring
        /// 
        /// **Configuration:**
        /// Configure this webhook URL in Stripe dashboard with these events:
        /// - checkout.session.completed
        /// - customer.subscription.created
        /// - customer.subscription.updated
        /// - customer.subscription.deleted
        /// - invoice.payment_succeeded
        /// - invoice.payment_failed
        /// - charge.dispute.created
        /// 
        /// **Use Cases:**
        /// - Real-time subscription state synchronization
        /// - Automatic tier updates when customer changes plan
        /// - Cancellation handling
        /// - Payment failure notifications
        /// - Dispute tracking and resolution
        /// </remarks>
        [HttpPost("webhook")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> HandleWebhook()
        {
            try
            {
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                var signature = Request.Headers["Stripe-Signature"].ToString();

                if (string.IsNullOrWhiteSpace(signature))
                {
                    _logger.LogWarning("Webhook called without Stripe-Signature header");
                    return BadRequest("Missing signature");
                }

                var processed = await _stripeService.ProcessWebhookEventAsync(json, signature);

                if (!processed)
                {
                    _logger.LogWarning("Webhook processing failed");
                    return BadRequest("Event processing failed");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling webhook");
                return BadRequest(ex.Message);
            }
        }
    }
}
