using BiatecTokensApi.Models.Metering;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for emitting subscription metering events for billing and analytics
    /// </summary>
    public interface ISubscriptionMeteringService
    {
        /// <summary>
        /// Emits a metering event for a compliance or whitelist operation
        /// </summary>
        /// <param name="meteringEvent">The metering event to emit</param>
        void EmitMeteringEvent(SubscriptionMeteringEvent meteringEvent);
    }
}
