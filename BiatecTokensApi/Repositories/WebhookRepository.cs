using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory implementation of webhook repository
    /// </summary>
    /// <remarks>
    /// This is a simple in-memory implementation for webhook storage.
    /// In production, this should be replaced with a persistent storage solution.
    /// </remarks>
    public class WebhookRepository : IWebhookRepository
    {
        private readonly ConcurrentDictionary<string, WebhookSubscription> _subscriptions = new();
        private readonly ConcurrentDictionary<string, WebhookDeliveryResult> _deliveryResults = new();
        private readonly ILogger<WebhookRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebhookRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public WebhookRepository(ILogger<WebhookRepository> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task<WebhookSubscription> CreateSubscriptionAsync(WebhookSubscription subscription)
        {
            if (string.IsNullOrEmpty(subscription.Id))
            {
                subscription.Id = Guid.NewGuid().ToString();
            }

            if (_subscriptions.TryAdd(subscription.Id, subscription))
            {
                _logger.LogInformation("Created webhook subscription {SubscriptionId} for URL {Url}",
                    subscription.Id, subscription.Url);
                return Task.FromResult(subscription);
            }

            throw new InvalidOperationException($"Subscription with ID {subscription.Id} already exists");
        }

        /// <inheritdoc/>
        public Task<WebhookSubscription?> GetSubscriptionAsync(string subscriptionId)
        {
            _subscriptions.TryGetValue(subscriptionId, out var subscription);
            return Task.FromResult(subscription);
        }

        /// <inheritdoc/>
        public Task<List<WebhookSubscription>> ListSubscriptionsAsync(string createdBy)
        {
            var subscriptions = _subscriptions.Values
                .Where(s => s.CreatedBy == createdBy)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            return Task.FromResult(subscriptions);
        }

        /// <inheritdoc/>
        public Task<List<WebhookSubscription>> ListActiveSubscriptionsAsync()
        {
            var subscriptions = _subscriptions.Values
                .Where(s => s.IsActive)
                .ToList();

            return Task.FromResult(subscriptions);
        }

        /// <inheritdoc/>
        public Task<bool> UpdateSubscriptionAsync(WebhookSubscription subscription)
        {
            if (_subscriptions.TryGetValue(subscription.Id, out var existing))
            {
                subscription.UpdatedAt = DateTime.UtcNow;
                _subscriptions[subscription.Id] = subscription;
                _logger.LogInformation("Updated webhook subscription {SubscriptionId}", subscription.Id);
                return Task.FromResult(true);
            }

            _logger.LogWarning("Webhook subscription {SubscriptionId} not found for update", subscription.Id);
            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task<bool> DeleteSubscriptionAsync(string subscriptionId)
        {
            var removed = _subscriptions.TryRemove(subscriptionId, out _);
            if (removed)
            {
                _logger.LogInformation("Deleted webhook subscription {SubscriptionId}", subscriptionId);
            }
            else
            {
                _logger.LogWarning("Webhook subscription {SubscriptionId} not found for deletion", subscriptionId);
            }

            return Task.FromResult(removed);
        }

        /// <inheritdoc/>
        public Task<WebhookDeliveryResult> StoreDeliveryResultAsync(WebhookDeliveryResult result)
        {
            if (string.IsNullOrEmpty(result.Id))
            {
                result.Id = Guid.NewGuid().ToString();
            }

            _deliveryResults[result.Id] = result;
            _logger.LogInformation("Stored webhook delivery result {DeliveryId} for event {EventId} (Success: {Success})",
                result.Id, result.EventId, result.Success);

            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<List<WebhookDeliveryResult>> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest request)
        {
            var query = _deliveryResults.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(request.SubscriptionId))
            {
                query = query.Where(d => d.SubscriptionId == request.SubscriptionId);
            }

            if (!string.IsNullOrEmpty(request.EventId))
            {
                query = query.Where(d => d.EventId == request.EventId);
            }

            if (request.Success.HasValue)
            {
                query = query.Where(d => d.Success == request.Success.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(d => d.AttemptedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(d => d.AttemptedAt <= request.ToDate.Value);
            }

            var results = query
                .OrderByDescending(d => d.AttemptedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            return Task.FromResult(results);
        }

        /// <inheritdoc/>
        public Task<int> GetDeliveryHistoryCountAsync(GetWebhookDeliveryHistoryRequest request)
        {
            var query = _deliveryResults.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(request.SubscriptionId))
            {
                query = query.Where(d => d.SubscriptionId == request.SubscriptionId);
            }

            if (!string.IsNullOrEmpty(request.EventId))
            {
                query = query.Where(d => d.EventId == request.EventId);
            }

            if (request.Success.HasValue)
            {
                query = query.Where(d => d.Success == request.Success.Value);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(d => d.AttemptedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(d => d.AttemptedAt <= request.ToDate.Value);
            }

            return Task.FromResult(query.Count());
        }

        /// <inheritdoc/>
        public Task<List<WebhookDeliveryResult>> GetPendingRetriesAsync()
        {
            var now = DateTime.UtcNow;
            var pendingRetries = _deliveryResults.Values
                .Where(d => d.WillRetry && d.NextRetryAt.HasValue && d.NextRetryAt.Value <= now)
                .OrderBy(d => d.NextRetryAt)
                .ToList();

            return Task.FromResult(pendingRetries);
        }
    }
}
