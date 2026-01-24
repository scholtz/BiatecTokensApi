using BiatecTokensApi.Models.Webhook;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for webhook operations
    /// </summary>
    public interface IWebhookService
    {
        /// <summary>
        /// Creates a new webhook subscription
        /// </summary>
        /// <param name="request">The subscription creation request</param>
        /// <param name="createdBy">The address of the user creating the subscription</param>
        /// <returns>The created subscription response</returns>
        Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest request, string createdBy);

        /// <summary>
        /// Gets a webhook subscription by ID
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <param name="userId">The user requesting the subscription</param>
        /// <returns>The subscription response</returns>
        Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string subscriptionId, string userId);

        /// <summary>
        /// Lists webhook subscriptions for the current user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>The list of subscriptions</returns>
        Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string userId);

        /// <summary>
        /// Updates an existing webhook subscription
        /// </summary>
        /// <param name="request">The subscription update request</param>
        /// <param name="userId">The user updating the subscription</param>
        /// <returns>The updated subscription response</returns>
        Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest request, string userId);

        /// <summary>
        /// Deletes a webhook subscription
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to delete</param>
        /// <param name="userId">The user deleting the subscription</param>
        /// <returns>The response indicating success or failure</returns>
        Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string subscriptionId, string userId);

        /// <summary>
        /// Emits a webhook event to all subscribed endpoints
        /// </summary>
        /// <param name="webhookEvent">The event to emit</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task EmitEventAsync(WebhookEvent webhookEvent);

        /// <summary>
        /// Gets webhook delivery history with filtering
        /// </summary>
        /// <param name="request">The request containing filter criteria</param>
        /// <param name="userId">The user requesting the history</param>
        /// <returns>The delivery history response</returns>
        Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest request, string userId);
    }
}
