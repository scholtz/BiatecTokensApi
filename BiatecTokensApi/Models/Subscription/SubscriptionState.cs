namespace BiatecTokensApi.Models.Subscription
{
    /// <summary>
    /// Represents the subscription state for a user
    /// </summary>
    public class SubscriptionState
    {
        /// <summary>
        /// User's Algorand address
        /// </summary>
        public string UserAddress { get; set; } = string.Empty;

        /// <summary>
        /// Stripe customer ID
        /// </summary>
        public string? StripeCustomerId { get; set; }

        /// <summary>
        /// Stripe subscription ID
        /// </summary>
        public string? StripeSubscriptionId { get; set; }

        /// <summary>
        /// Current subscription tier
        /// </summary>
        public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

        /// <summary>
        /// Current subscription status
        /// </summary>
        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.None;

        /// <summary>
        /// Subscription start date
        /// </summary>
        public DateTime? SubscriptionStartDate { get; set; }

        /// <summary>
        /// Subscription end date (for cancellations)
        /// </summary>
        public DateTime? SubscriptionEndDate { get; set; }

        /// <summary>
        /// Current billing period start
        /// </summary>
        public DateTime? CurrentPeriodStart { get; set; }

        /// <summary>
        /// Current billing period end
        /// </summary>
        public DateTime? CurrentPeriodEnd { get; set; }

        /// <summary>
        /// Whether subscription will cancel at period end
        /// </summary>
        public bool CancelAtPeriodEnd { get; set; }

        /// <summary>
        /// Last update timestamp
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Subscription status values
    /// </summary>
    public enum SubscriptionStatus
    {
        /// <summary>
        /// No active subscription
        /// </summary>
        None = 0,

        /// <summary>
        /// Subscription is active
        /// </summary>
        Active = 1,

        /// <summary>
        /// Subscription is past due
        /// </summary>
        PastDue = 2,

        /// <summary>
        /// Subscription is unpaid
        /// </summary>
        Unpaid = 3,

        /// <summary>
        /// Subscription is canceled
        /// </summary>
        Canceled = 4,

        /// <summary>
        /// Subscription is incomplete
        /// </summary>
        Incomplete = 5,

        /// <summary>
        /// Subscription is incomplete and payment expired
        /// </summary>
        IncompleteExpired = 6,

        /// <summary>
        /// Subscription is in trial period
        /// </summary>
        Trialing = 7,

        /// <summary>
        /// Subscription is paused
        /// </summary>
        Paused = 8
    }

    /// <summary>
    /// Request to create a checkout session
    /// </summary>
    public class CreateCheckoutSessionRequest
    {
        /// <summary>
        /// Subscription tier to purchase
        /// </summary>
        public SubscriptionTier Tier { get; set; }
    }

    /// <summary>
    /// Response with checkout session details
    /// </summary>
    public class CreateCheckoutSessionResponse
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Stripe checkout session ID
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Checkout URL for the user to complete payment
        /// </summary>
        public string? CheckoutUrl { get; set; }

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request to create a billing portal session
    /// </summary>
    public class CreateBillingPortalSessionRequest
    {
        /// <summary>
        /// Return URL after customer leaves the portal
        /// </summary>
        public string? ReturnUrl { get; set; }
    }

    /// <summary>
    /// Response with billing portal session details
    /// </summary>
    public class CreateBillingPortalSessionResponse
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Billing portal URL
        /// </summary>
        public string? PortalUrl { get; set; }

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Response with subscription status
    /// </summary>
    public class SubscriptionStatusResponse
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Subscription state details
        /// </summary>
        public SubscriptionState? Subscription { get; set; }

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Webhook event for audit logging
    /// </summary>
    public class SubscriptionWebhookEvent
    {
        /// <summary>
        /// Webhook event ID (for idempotency)
        /// </summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>
        /// Event type (e.g., customer.subscription.created)
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// User address associated with the event
        /// </summary>
        public string UserAddress { get; set; } = string.Empty;

        /// <summary>
        /// Stripe subscription ID
        /// </summary>
        public string? StripeSubscriptionId { get; set; }

        /// <summary>
        /// Subscription tier after the event
        /// </summary>
        public SubscriptionTier Tier { get; set; }

        /// <summary>
        /// Subscription status after the event
        /// </summary>
        public SubscriptionStatus Status { get; set; }

        /// <summary>
        /// Event processing timestamp
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the event was processed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if processing failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
