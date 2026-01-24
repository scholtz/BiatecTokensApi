using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing webhook subscriptions and event delivery
    /// </summary>
    public class WebhookService : IWebhookService
    {
        private readonly IWebhookRepository _repository;
        private readonly ILogger<WebhookService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        // Retry configuration
        private const int MaxRetries = 3;
        private static readonly TimeSpan[] RetryDelays = new[]
        {
            TimeSpan.FromMinutes(1),   // First retry after 1 minute
            TimeSpan.FromMinutes(5),   // Second retry after 5 minutes
            TimeSpan.FromMinutes(15)   // Third retry after 15 minutes
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="WebhookService"/> class.
        /// </summary>
        /// <param name="repository">The webhook repository</param>
        /// <param name="logger">The logger instance</param>
        /// <param name="httpClientFactory">The HTTP client factory</param>
        public WebhookService(
            IWebhookRepository repository,
            ILogger<WebhookService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _repository = repository;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc/>
        public async Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest request, string createdBy)
        {
            try
            {
                // Validate URL
                if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "Invalid webhook URL. Must be a valid HTTP or HTTPS URL."
                    };
                }

                // Validate event types
                if (request.EventTypes == null || request.EventTypes.Count == 0)
                {
                    return new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "At least one event type must be specified."
                    };
                }

                // Generate signing secret
                var signingSecret = GenerateSigningSecret();

                var subscription = new WebhookSubscription
                {
                    Url = request.Url,
                    SigningSecret = signingSecret,
                    EventTypes = request.EventTypes,
                    Description = request.Description,
                    AssetIdFilter = request.AssetIdFilter,
                    NetworkFilter = request.NetworkFilter,
                    CreatedBy = createdBy,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                var created = await _repository.CreateSubscriptionAsync(subscription);

                _logger.LogInformation("Created webhook subscription {SubscriptionId} for user {UserId} with {EventCount} event types",
                    created.Id, createdBy, request.EventTypes.Count);

                return new WebhookSubscriptionResponse
                {
                    Success = true,
                    Subscription = created
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating webhook subscription for user {UserId}", createdBy);
                return new WebhookSubscriptionResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create webhook subscription: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string subscriptionId, string userId)
        {
            try
            {
                var subscription = await _repository.GetSubscriptionAsync(subscriptionId);

                if (subscription == null)
                {
                    return new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "Webhook subscription not found."
                    };
                }

                // Verify ownership
                if (subscription.CreatedBy != userId)
                {
                    return new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "You do not have permission to access this webhook subscription."
                    };
                }

                return new WebhookSubscriptionResponse
                {
                    Success = true,
                    Subscription = subscription
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting webhook subscription {SubscriptionId}", subscriptionId);
                return new WebhookSubscriptionResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to get webhook subscription: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string userId)
        {
            try
            {
                var subscriptions = await _repository.ListSubscriptionsAsync(userId);

                return new WebhookSubscriptionListResponse
                {
                    Success = true,
                    Subscriptions = subscriptions,
                    TotalCount = subscriptions.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing webhook subscriptions for user {UserId}", userId);
                return new WebhookSubscriptionListResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to list webhook subscriptions: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest request, string userId)
        {
            try
            {
                var subscription = await _repository.GetSubscriptionAsync(request.SubscriptionId);

                if (subscription == null)
                {
                    return new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "Webhook subscription not found."
                    };
                }

                // Verify ownership
                if (subscription.CreatedBy != userId)
                {
                    return new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "You do not have permission to update this webhook subscription."
                    };
                }

                // Update fields
                if (request.IsActive.HasValue)
                {
                    subscription.IsActive = request.IsActive.Value;
                }

                if (request.EventTypes != null && request.EventTypes.Count > 0)
                {
                    subscription.EventTypes = request.EventTypes;
                }

                if (request.Description != null)
                {
                    subscription.Description = request.Description;
                }

                subscription.UpdatedAt = DateTime.UtcNow;

                var updated = await _repository.UpdateSubscriptionAsync(subscription);

                if (!updated)
                {
                    return new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to update webhook subscription."
                    };
                }

                _logger.LogInformation("Updated webhook subscription {SubscriptionId} for user {UserId}",
                    subscription.Id, userId);

                return new WebhookSubscriptionResponse
                {
                    Success = true,
                    Subscription = subscription
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating webhook subscription {SubscriptionId}", request.SubscriptionId);
                return new WebhookSubscriptionResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to update webhook subscription: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string subscriptionId, string userId)
        {
            try
            {
                var subscription = await _repository.GetSubscriptionAsync(subscriptionId);

                if (subscription == null)
                {
                    return new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "Webhook subscription not found."
                    };
                }

                // Verify ownership
                if (subscription.CreatedBy != userId)
                {
                    return new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "You do not have permission to delete this webhook subscription."
                    };
                }

                var deleted = await _repository.DeleteSubscriptionAsync(subscriptionId);

                if (!deleted)
                {
                    return new WebhookSubscriptionResponse
                    {
                        Success = false,
                        ErrorMessage = "Failed to delete webhook subscription."
                    };
                }

                _logger.LogInformation("Deleted webhook subscription {SubscriptionId} for user {UserId}",
                    subscriptionId, userId);

                return new WebhookSubscriptionResponse
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting webhook subscription {SubscriptionId}", subscriptionId);
                return new WebhookSubscriptionResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to delete webhook subscription: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task EmitEventAsync(WebhookEvent webhookEvent)
        {
            try
            {
                // Get all active subscriptions
                var subscriptions = await _repository.ListActiveSubscriptionsAsync();

                // Filter subscriptions based on event type and filters
                var relevantSubscriptions = subscriptions.Where(s =>
                    s.EventTypes.Contains(webhookEvent.EventType) &&
                    (!s.AssetIdFilter.HasValue || s.AssetIdFilter.Value == webhookEvent.AssetId) &&
                    (string.IsNullOrEmpty(s.NetworkFilter) || s.NetworkFilter == webhookEvent.Network)
                ).ToList();

                _logger.LogInformation("Emitting webhook event {EventId} of type {EventType} to {SubscriptionCount} subscriptions",
                    webhookEvent.Id, webhookEvent.EventType, relevantSubscriptions.Count);

                // Deliver to each subscription asynchronously (fire and forget)
                foreach (var subscription in relevantSubscriptions)
                {
                    // Capture subscription in local variable to avoid closure issues
                    var sub = subscription;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await DeliverWebhookAsync(sub, webhookEvent, 0);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unhandled exception in webhook delivery for subscription {SubscriptionId}, event {EventId}",
                                sub.Id, webhookEvent.Id);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error emitting webhook event {EventId}", webhookEvent.Id);
            }
        }

        /// <inheritdoc/>
        public async Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest request, string userId)
        {
            try
            {
                // If subscription ID is provided, verify ownership
                if (!string.IsNullOrEmpty(request.SubscriptionId))
                {
                    var subscription = await _repository.GetSubscriptionAsync(request.SubscriptionId);
                    if (subscription == null || subscription.CreatedBy != userId)
                    {
                        return new WebhookDeliveryHistoryResponse
                        {
                            Success = false,
                            ErrorMessage = "You do not have permission to access this webhook delivery history."
                        };
                    }
                }

                var deliveries = await _repository.GetDeliveryHistoryAsync(request);
                var totalCount = await _repository.GetDeliveryHistoryCountAsync(request);

                var successCount = deliveries.Count(d => d.Success);
                var failedCount = deliveries.Count(d => !d.Success);
                var pendingRetries = deliveries.Count(d => d.WillRetry);

                return new WebhookDeliveryHistoryResponse
                {
                    Success = true,
                    Deliveries = deliveries,
                    TotalCount = totalCount,
                    SuccessCount = successCount,
                    FailedCount = failedCount,
                    PendingRetries = pendingRetries
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting webhook delivery history");
                return new WebhookDeliveryHistoryResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to get webhook delivery history: {ex.Message}"
                };
            }
        }

        private async Task DeliverWebhookAsync(WebhookSubscription subscription, WebhookEvent webhookEvent, int retryCount)
        {
            var deliveryResult = new WebhookDeliveryResult
            {
                SubscriptionId = subscription.Id,
                EventId = webhookEvent.Id,
                RetryCount = retryCount,
                AttemptedAt = DateTime.UtcNow
            };

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                // Serialize event payload
                var payload = JsonSerializer.Serialize(webhookEvent);

                // Generate signature
                var signature = GenerateSignature(payload, subscription.SigningSecret);

                // Create request
                var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                // Add headers
                request.Headers.Add("X-Webhook-Signature", signature);
                request.Headers.Add("X-Webhook-Event-Id", webhookEvent.Id);
                request.Headers.Add("X-Webhook-Event-Type", webhookEvent.EventType.ToString());

                // Send request
                var response = await client.SendAsync(request);

                deliveryResult.StatusCode = (int)response.StatusCode;
                deliveryResult.ResponseBody = await response.Content.ReadAsStringAsync();
                deliveryResult.Success = response.IsSuccessStatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    deliveryResult.ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";

                    // Determine if we should retry
                    if (retryCount < MaxRetries && ShouldRetry(response.StatusCode))
                    {
                        deliveryResult.WillRetry = true;
                        deliveryResult.NextRetryAt = DateTime.UtcNow.Add(RetryDelays[retryCount]);

                        _logger.LogWarning("Webhook delivery failed for subscription {SubscriptionId}, event {EventId}. " +
                            "Will retry in {RetryDelay} (attempt {RetryCount}/{MaxRetries})",
                            subscription.Id, webhookEvent.Id, RetryDelays[retryCount], retryCount + 1, MaxRetries);

                        // Schedule retry with proper exception handling
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(RetryDelays[retryCount]);
                                await DeliverWebhookAsync(subscription, webhookEvent, retryCount + 1);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Unhandled exception during webhook retry for subscription {SubscriptionId}, event {EventId}",
                                    subscription.Id, webhookEvent.Id);
                            }
                        });
                    }
                    else
                    {
                        _logger.LogError("Webhook delivery failed permanently for subscription {SubscriptionId}, event {EventId}. " +
                            "Max retries exhausted.",
                            subscription.Id, webhookEvent.Id);
                    }
                }
                else
                {
                    _logger.LogInformation("Successfully delivered webhook event {EventId} to subscription {SubscriptionId}",
                        webhookEvent.Id, subscription.Id);
                }
            }
            catch (TaskCanceledException)
            {
                deliveryResult.Success = false;
                deliveryResult.ErrorMessage = "Request timeout";

                // Retry on timeout
                if (retryCount < MaxRetries)
                {
                    deliveryResult.WillRetry = true;
                    deliveryResult.NextRetryAt = DateTime.UtcNow.Add(RetryDelays[retryCount]);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(RetryDelays[retryCount]);
                            await DeliverWebhookAsync(subscription, webhookEvent, retryCount + 1);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unhandled exception during webhook retry after timeout for subscription {SubscriptionId}, event {EventId}",
                                subscription.Id, webhookEvent.Id);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                deliveryResult.Success = false;
                deliveryResult.ErrorMessage = ex.Message;

                _logger.LogError(ex, "Exception delivering webhook to subscription {SubscriptionId}, event {EventId}",
                    subscription.Id, webhookEvent.Id);
            }

            // Store delivery result
            await _repository.StoreDeliveryResultAsync(deliveryResult);
        }

        private static bool ShouldRetry(System.Net.HttpStatusCode statusCode)
        {
            // Retry on 5xx server errors and 429 rate limiting
            return ((int)statusCode >= 500 && (int)statusCode < 600) || statusCode == System.Net.HttpStatusCode.TooManyRequests;
        }

        private static string GenerateSigningSecret()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string GenerateSignature(string payload, string secret)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var messageBytes = Encoding.UTF8.GetBytes(payload);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(messageBytes);
            return Convert.ToBase64String(hashBytes);
        }
    }
}
