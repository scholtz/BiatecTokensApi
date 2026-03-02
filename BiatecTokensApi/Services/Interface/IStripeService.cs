using BiatecTokensApi.Models.Subscription;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for managing Stripe subscriptions
    /// </summary>
    public interface IStripeService
    {
        /// <summary>
        /// Creates a Stripe checkout session for a subscription
        /// </summary>
        /// <param name="userAddress">User's Algorand address</param>
        /// <param name="tier">Subscription tier to purchase</param>
        /// <returns>Checkout session details</returns>
        Task<CreateCheckoutSessionResponse> CreateCheckoutSessionAsync(string userAddress, SubscriptionTier tier);

        /// <summary>
        /// Creates a Stripe billing portal session
        /// </summary>
        /// <param name="userAddress">User's Algorand address</param>
        /// <param name="returnUrl">Return URL after portal session</param>
        /// <returns>Billing portal session details</returns>
        Task<CreateBillingPortalSessionResponse> CreateBillingPortalSessionAsync(string userAddress, string? returnUrl = null);

        /// <summary>
        /// Gets subscription status for a user
        /// </summary>
        /// <param name="userAddress">User's Algorand address</param>
        /// <returns>Subscription status</returns>
        Task<SubscriptionState> GetSubscriptionStatusAsync(string userAddress);

        /// <summary>
        /// Processes a Stripe webhook event
        /// </summary>
        /// <param name="json">Webhook event JSON payload</param>
        /// <param name="signature">Stripe signature header</param>
        /// <returns>Processing result</returns>
        Task<bool> ProcessWebhookEventAsync(string json, string signature);

        /// <summary>
        /// Gets subscription entitlements for a user
        /// </summary>
        /// <param name="userAddress">User's Algorand address</param>
        /// <returns>Subscription entitlements</returns>
        Task<SubscriptionEntitlements> GetEntitlementsAsync(string userAddress);

        /// <summary>
        /// Provisions a 14-day Professional-tier trial for a newly registered user
        /// </summary>
        /// <param name="userAddress">User's Algorand address</param>
        Task ProvisionTrialAsync(string userAddress);

        /// <summary>
        /// Cancels a user's subscription (at period end by default)
        /// </summary>
        /// <param name="userAddress">User's Algorand address</param>
        /// <param name="cancelImmediately">If true, cancel immediately; otherwise cancel at period end</param>
        /// <returns>Cancellation response</returns>
        Task<CancelSubscriptionResponse> CancelSubscriptionAsync(string userAddress, bool cancelImmediately = false);

        /// <summary>
        /// Overrides a user's subscription tier (admin operation)
        /// </summary>
        /// <param name="userId">User ID (Algorand address)</param>
        /// <param name="tier">Tier to assign</param>
        /// <param name="reason">Reason for the override (for audit log)</param>
        /// <returns>Override response</returns>
        Task<SubscriptionOverrideResponse> OverrideSubscriptionTierAsync(string userId, SubscriptionTier tier, string? reason = null);

        /// <summary>
        /// Gets aggregate subscription metrics for admin dashboard
        /// </summary>
        /// <returns>Subscription metrics</returns>
        Task<SubscriptionMetrics> GetAdminMetricsAsync();
    }
}
