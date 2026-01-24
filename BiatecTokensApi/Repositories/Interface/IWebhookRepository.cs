using BiatecTokensApi.Models.Webhook;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for webhook operations
    /// </summary>
    public interface IWebhookRepository
    {
        /// <summary>
        /// Creates a new webhook subscription
        /// </summary>
        /// <param name="subscription">The webhook subscription to create</param>
        /// <returns>The created subscription with ID assigned</returns>
        Task<WebhookSubscription> CreateSubscriptionAsync(WebhookSubscription subscription);

        /// <summary>
        /// Retrieves a webhook subscription by ID
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <returns>The webhook subscription or null if not found</returns>
        Task<WebhookSubscription?> GetSubscriptionAsync(string subscriptionId);

        /// <summary>
        /// Lists all webhook subscriptions for a user
        /// </summary>
        /// <param name="createdBy">The user address who created the subscriptions</param>
        /// <returns>List of webhook subscriptions</returns>
        Task<List<WebhookSubscription>> ListSubscriptionsAsync(string createdBy);

        /// <summary>
        /// Lists all active webhook subscriptions
        /// </summary>
        /// <returns>List of active webhook subscriptions</returns>
        Task<List<WebhookSubscription>> ListActiveSubscriptionsAsync();

        /// <summary>
        /// Updates an existing webhook subscription
        /// </summary>
        /// <param name="subscription">The subscription to update</param>
        /// <returns>True if updated successfully, false otherwise</returns>
        Task<bool> UpdateSubscriptionAsync(WebhookSubscription subscription);

        /// <summary>
        /// Deletes a webhook subscription
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to delete</param>
        /// <returns>True if deleted successfully, false otherwise</returns>
        Task<bool> DeleteSubscriptionAsync(string subscriptionId);

        /// <summary>
        /// Stores a webhook delivery result
        /// </summary>
        /// <param name="result">The delivery result to store</param>
        /// <returns>The stored delivery result</returns>
        Task<WebhookDeliveryResult> StoreDeliveryResultAsync(WebhookDeliveryResult result);

        /// <summary>
        /// Retrieves webhook delivery history with filtering
        /// </summary>
        /// <param name="request">The request containing filter criteria</param>
        /// <returns>List of delivery results</returns>
        Task<List<WebhookDeliveryResult>> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest request);

        /// <summary>
        /// Gets the total count of delivery results matching the filter
        /// </summary>
        /// <param name="request">The request containing filter criteria</param>
        /// <returns>Count of matching delivery results</returns>
        Task<int> GetDeliveryHistoryCountAsync(GetWebhookDeliveryHistoryRequest request);

        /// <summary>
        /// Gets pending retry deliveries that are due
        /// </summary>
        /// <returns>List of delivery results that should be retried</returns>
        Task<List<WebhookDeliveryResult>> GetPendingRetriesAsync();
    }
}
