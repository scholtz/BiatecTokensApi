using BiatecTokensApi.Models.Metering;
using BiatecTokensApi.Services.Interface;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for emitting subscription metering events for billing and analytics
    /// </summary>
    /// <remarks>
    /// This service emits structured log events that can be consumed by monitoring
    /// and analytics systems for billing purposes. Events include network, asset ID,
    /// operation type, and item count for accurate usage tracking.
    /// </remarks>
    public class SubscriptionMeteringService : ISubscriptionMeteringService
    {
        private readonly ILogger<SubscriptionMeteringService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionMeteringService"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public SubscriptionMeteringService(ILogger<SubscriptionMeteringService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public void EmitMeteringEvent(SubscriptionMeteringEvent meteringEvent)
        {
            if (meteringEvent == null)
            {
                _logger.LogWarning("Attempted to emit null metering event");
                return;
            }

            // Emit structured log event for analytics systems to consume
            _logger.LogInformation(
                "METERING_EVENT: {EventId} | Category: {Category} | Operation: {OperationType} | " +
                "AssetId: {AssetId} | Network: {Network} | ItemCount: {ItemCount} | PerformedBy: {PerformedBy} | " +
                "Timestamp: {Timestamp} | Metadata: {Metadata}",
                meteringEvent.EventId,
                meteringEvent.Category,
                meteringEvent.OperationType,
                meteringEvent.AssetId,
                meteringEvent.Network ?? "unknown",
                meteringEvent.ItemCount,
                meteringEvent.PerformedBy ?? "unknown",
                meteringEvent.Timestamp,
                meteringEvent.Metadata != null ? JsonSerializer.Serialize(meteringEvent.Metadata) : "{}"
            );
        }
    }
}
