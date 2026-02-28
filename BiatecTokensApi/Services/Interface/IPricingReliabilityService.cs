using BiatecTokensApi.Models.PricingReliability;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Pricing reliability orchestrator with deterministic precedence rules.
    /// </summary>
    public interface IPricingReliabilityService
    {
        /// <summary>
        /// Returns deterministic quote payload with source provenance and freshness markers.
        /// Fallback is traceable; when all sources fail, returns typed error.
        /// </summary>
        Task<PricingReliabilityResponse> GetReliableQuoteAsync(PricingReliabilityRequest request);

        /// <summary>
        /// Returns health status of the pricing service including source availability.
        /// </summary>
        Task<PricingSourceHealthSummary> GetSourceHealthAsync();
    }
}
