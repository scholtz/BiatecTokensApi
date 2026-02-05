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
    }
}
