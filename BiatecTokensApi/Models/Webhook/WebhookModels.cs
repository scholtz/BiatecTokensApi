namespace BiatecTokensApi.Models.Webhook
{
    /// <summary>
    /// Webhook subscription configuration
    /// </summary>
    /// <remarks>
    /// Represents a webhook subscription with URL, signing secret, and event type filters.
    /// Subscriptions are used to deliver compliance events to external systems.
    /// </remarks>
    public class WebhookSubscription
    {
        /// <summary>
        /// Unique identifier for the webhook subscription
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// URL to deliver webhook events to
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Signing secret for webhook signature verification
        /// </summary>
        public string SigningSecret { get; set; } = string.Empty;

        /// <summary>
        /// Event types this subscription is interested in
        /// </summary>
        public List<WebhookEventType> EventTypes { get; set; } = new();

        /// <summary>
        /// Whether the subscription is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Address of the user who created this subscription
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the subscription was created (UTC)
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the subscription was last updated (UTC)
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Optional description for the subscription
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Optional filter by asset ID
        /// </summary>
        public ulong? AssetIdFilter { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? NetworkFilter { get; set; }
    }

    /// <summary>
    /// Types of webhook events that can be subscribed to
    /// </summary>
    public enum WebhookEventType
    {
        /// <summary>
        /// Address added to whitelist
        /// </summary>
        WhitelistAdd,

        /// <summary>
        /// Address removed from whitelist
        /// </summary>
        WhitelistRemove,

        /// <summary>
        /// Transfer denied by whitelist rules
        /// </summary>
        TransferDeny,

        /// <summary>
        /// Audit export created
        /// </summary>
        AuditExportCreated
    }

    /// <summary>
    /// Webhook event payload
    /// </summary>
    /// <remarks>
    /// Contains all details about a compliance event including actor, timestamp, asset, and network.
    /// Events are signed and delivered to subscribed webhook endpoints.
    /// </remarks>
    public class WebhookEvent
    {
        /// <summary>
        /// Unique identifier for the event
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of event
        /// </summary>
        public WebhookEventType EventType { get; set; }

        /// <summary>
        /// Timestamp when the event occurred (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Asset ID associated with the event
        /// </summary>
        public ulong? AssetId { get; set; }

        /// <summary>
        /// Network on which the event occurred
        /// </summary>
        public string? Network { get; set; }

        /// <summary>
        /// Address of the actor who triggered the event
        /// </summary>
        public string Actor { get; set; } = string.Empty;

        /// <summary>
        /// Address affected by the event (for whitelist operations)
        /// </summary>
        public string? AffectedAddress { get; set; }

        /// <summary>
        /// Additional event-specific data as JSON
        /// </summary>
        public Dictionary<string, object>? Data { get; set; }
    }

    /// <summary>
    /// Result of a webhook delivery attempt
    /// </summary>
    public class WebhookDeliveryResult
    {
        /// <summary>
        /// Unique identifier for the delivery attempt
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// ID of the webhook subscription
        /// </summary>
        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>
        /// ID of the event being delivered
        /// </summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>
        /// Whether the delivery was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// HTTP status code from the webhook endpoint
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Timestamp of the delivery attempt (UTC)
        /// </summary>
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of retry attempts made
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Error message if delivery failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Response body from the webhook endpoint
        /// </summary>
        public string? ResponseBody { get; set; }

        /// <summary>
        /// Whether this delivery will be retried
        /// </summary>
        public bool WillRetry { get; set; }

        /// <summary>
        /// Timestamp when the next retry will occur (UTC)
        /// </summary>
        public DateTime? NextRetryAt { get; set; }
    }

    /// <summary>
    /// Request to create a webhook subscription
    /// </summary>
    public class CreateWebhookSubscriptionRequest
    {
        /// <summary>
        /// URL to deliver webhook events to
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Event types to subscribe to
        /// </summary>
        public List<WebhookEventType> EventTypes { get; set; } = new();

        /// <summary>
        /// Optional description for the subscription
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Optional filter by asset ID
        /// </summary>
        public ulong? AssetIdFilter { get; set; }

        /// <summary>
        /// Optional filter by network
        /// </summary>
        public string? NetworkFilter { get; set; }
    }

    /// <summary>
    /// Response containing webhook subscription details
    /// </summary>
    public class WebhookSubscriptionResponse : BaseResponse
    {
        /// <summary>
        /// The webhook subscription
        /// </summary>
        public WebhookSubscription? Subscription { get; set; }
    }

    /// <summary>
    /// Response containing a list of webhook subscriptions
    /// </summary>
    public class WebhookSubscriptionListResponse : BaseResponse
    {
        /// <summary>
        /// List of webhook subscriptions
        /// </summary>
        public List<WebhookSubscription> Subscriptions { get; set; } = new();

        /// <summary>
        /// Total number of subscriptions
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Request to update a webhook subscription
    /// </summary>
    public class UpdateWebhookSubscriptionRequest
    {
        /// <summary>
        /// ID of the subscription to update
        /// </summary>
        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>
        /// Whether the subscription is active
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Event types to subscribe to
        /// </summary>
        public List<WebhookEventType>? EventTypes { get; set; }

        /// <summary>
        /// Optional description for the subscription
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// Response containing webhook delivery history
    /// </summary>
    public class WebhookDeliveryHistoryResponse : BaseResponse
    {
        /// <summary>
        /// List of delivery results
        /// </summary>
        public List<WebhookDeliveryResult> Deliveries { get; set; } = new();

        /// <summary>
        /// Total number of delivery attempts
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Number of successful deliveries
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Number of failed deliveries
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Number of pending retries
        /// </summary>
        public int PendingRetries { get; set; }
    }

    /// <summary>
    /// Request to retrieve webhook delivery history
    /// </summary>
    public class GetWebhookDeliveryHistoryRequest
    {
        /// <summary>
        /// Optional filter by subscription ID
        /// </summary>
        public string? SubscriptionId { get; set; }

        /// <summary>
        /// Optional filter by event ID
        /// </summary>
        public string? EventId { get; set; }

        /// <summary>
        /// Optional filter by success status
        /// </summary>
        public bool? Success { get; set; }

        /// <summary>
        /// Optional start date filter (ISO 8601)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional end date filter (ISO 8601)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination (default: 50, max: 100)
        /// </summary>
        public int PageSize { get; set; } = 50;
    }
}
