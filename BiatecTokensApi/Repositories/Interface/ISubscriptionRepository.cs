using BiatecTokensApi.Models.Subscription;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository for managing subscription state persistence
    /// </summary>
    public interface ISubscriptionRepository
    {
        /// <summary>
        /// Gets subscription state for a user
        /// </summary>
        /// <param name="userAddress">The user's Algorand address</param>
        /// <returns>Subscription state or null if not found</returns>
        Task<SubscriptionState?> GetSubscriptionAsync(string userAddress);

        /// <summary>
        /// Saves or updates subscription state
        /// </summary>
        /// <param name="subscription">Subscription state to save</param>
        Task SaveSubscriptionAsync(SubscriptionState subscription);

        /// <summary>
        /// Gets subscription state by Stripe customer ID
        /// </summary>
        /// <param name="stripeCustomerId">Stripe customer ID</param>
        /// <returns>Subscription state or null if not found</returns>
        Task<SubscriptionState?> GetSubscriptionByCustomerIdAsync(string stripeCustomerId);

        /// <summary>
        /// Gets subscription state by Stripe subscription ID
        /// </summary>
        /// <param name="stripeSubscriptionId">Stripe subscription ID</param>
        /// <returns>Subscription state or null if not found</returns>
        Task<SubscriptionState?> GetSubscriptionBySubscriptionIdAsync(string stripeSubscriptionId);

        /// <summary>
        /// Checks if a webhook event has been processed (for idempotency)
        /// </summary>
        /// <param name="eventId">Stripe event ID</param>
        /// <returns>True if event has been processed</returns>
        Task<bool> IsEventProcessedAsync(string eventId);

        /// <summary>
        /// Marks a webhook event as processed
        /// </summary>
        /// <param name="webhookEvent">Webhook event details</param>
        Task MarkEventProcessedAsync(SubscriptionWebhookEvent webhookEvent);

        /// <summary>
        /// Gets webhook event audit log
        /// </summary>
        /// <param name="userAddress">Optional user address filter</param>
        /// <returns>List of webhook events</returns>
        Task<List<SubscriptionWebhookEvent>> GetWebhookEventsAsync(string? userAddress = null);
    }
}
