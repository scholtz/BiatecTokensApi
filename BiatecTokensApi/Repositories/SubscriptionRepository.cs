using BiatecTokensApi.Models.Subscription;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory implementation of subscription repository
    /// </summary>
    /// <remarks>
    /// This implementation uses in-memory storage consistent with other repositories in the system.
    /// For production, this can be migrated to persistent storage (database, Redis, etc.) without API changes.
    /// </remarks>
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly ConcurrentDictionary<string, SubscriptionState> _subscriptionsByAddress;
        private readonly ConcurrentDictionary<string, string> _addressByCustomerId;
        private readonly ConcurrentDictionary<string, string> _addressBySubscriptionId;
        private readonly ConcurrentDictionary<string, SubscriptionWebhookEvent> _webhookEvents;
        private readonly ILogger<SubscriptionRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionRepository"/> class.
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public SubscriptionRepository(ILogger<SubscriptionRepository> logger)
        {
            _logger = logger;
            _subscriptionsByAddress = new ConcurrentDictionary<string, SubscriptionState>(StringComparer.OrdinalIgnoreCase);
            _addressByCustomerId = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _addressBySubscriptionId = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _webhookEvents = new ConcurrentDictionary<string, SubscriptionWebhookEvent>(StringComparer.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public Task<SubscriptionState?> GetSubscriptionAsync(string userAddress)
        {
            if (string.IsNullOrWhiteSpace(userAddress))
            {
                _logger.LogWarning("GetSubscriptionAsync called with null or empty userAddress");
                return Task.FromResult<SubscriptionState?>(null);
            }

            _subscriptionsByAddress.TryGetValue(userAddress.ToUpperInvariant(), out var subscription);
            return Task.FromResult(subscription);
        }

        /// <inheritdoc/>
        public Task SaveSubscriptionAsync(SubscriptionState subscription)
        {
            if (subscription == null)
            {
                throw new ArgumentNullException(nameof(subscription));
            }

            if (string.IsNullOrWhiteSpace(subscription.UserAddress))
            {
                throw new ArgumentException("UserAddress is required", nameof(subscription));
            }

            subscription.LastUpdated = DateTime.UtcNow;
            var upperAddress = subscription.UserAddress.ToUpperInvariant();

            _subscriptionsByAddress[upperAddress] = subscription;

            // Update lookup indices
            if (!string.IsNullOrWhiteSpace(subscription.StripeCustomerId))
            {
                _addressByCustomerId[subscription.StripeCustomerId] = upperAddress;
            }

            if (!string.IsNullOrWhiteSpace(subscription.StripeSubscriptionId))
            {
                _addressBySubscriptionId[subscription.StripeSubscriptionId] = upperAddress;
            }

            _logger.LogInformation(
                "Saved subscription for user {UserAddress}, Tier: {Tier}, Status: {Status}",
                subscription.UserAddress, subscription.Tier, subscription.Status);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task<SubscriptionState?> GetSubscriptionByCustomerIdAsync(string stripeCustomerId)
        {
            if (string.IsNullOrWhiteSpace(stripeCustomerId))
            {
                _logger.LogWarning("GetSubscriptionByCustomerIdAsync called with null or empty stripeCustomerId");
                return null;
            }

            if (_addressByCustomerId.TryGetValue(stripeCustomerId, out var userAddress))
            {
                return await GetSubscriptionAsync(userAddress);
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task<SubscriptionState?> GetSubscriptionBySubscriptionIdAsync(string stripeSubscriptionId)
        {
            if (string.IsNullOrWhiteSpace(stripeSubscriptionId))
            {
                _logger.LogWarning("GetSubscriptionBySubscriptionIdAsync called with null or empty stripeSubscriptionId");
                return null;
            }

            if (_addressBySubscriptionId.TryGetValue(stripeSubscriptionId, out var userAddress))
            {
                return await GetSubscriptionAsync(userAddress);
            }

            return null;
        }

        /// <inheritdoc/>
        public Task<bool> IsEventProcessedAsync(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                _logger.LogWarning("IsEventProcessedAsync called with null or empty eventId");
                return Task.FromResult(false);
            }

            return Task.FromResult(_webhookEvents.ContainsKey(eventId));
        }

        /// <inheritdoc/>
        public Task MarkEventProcessedAsync(SubscriptionWebhookEvent webhookEvent)
        {
            if (webhookEvent == null)
            {
                throw new ArgumentNullException(nameof(webhookEvent));
            }

            if (string.IsNullOrWhiteSpace(webhookEvent.EventId))
            {
                throw new ArgumentException("EventId is required", nameof(webhookEvent));
            }

            webhookEvent.ProcessedAt = DateTime.UtcNow;
            _webhookEvents[webhookEvent.EventId] = webhookEvent;

            _logger.LogInformation(
                "SUBSCRIPTION_AUDIT: WebhookProcessed | EventId: {EventId} | EventType: {EventType} | " +
                "UserAddress: {UserAddress} | Tier: {Tier} | Status: {Status} | Success: {Success}",
                webhookEvent.EventId, webhookEvent.EventType, webhookEvent.UserAddress,
                webhookEvent.Tier, webhookEvent.Status, webhookEvent.Success);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<List<SubscriptionWebhookEvent>> GetWebhookEventsAsync(string? userAddress = null)
        {
            var events = _webhookEvents.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(userAddress))
            {
                events = events.Where(e => string.Equals(e.UserAddress, userAddress, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult(events.OrderByDescending(e => e.ProcessedAt).ToList());
        }
    }
}
