using BiatecTokensApi.Models.TokenOperationsIntelligence;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for token operations intelligence
    /// </summary>
    public interface ITokenOperationsIntelligenceService
    {
        /// <summary>
        /// Returns a consolidated operations intelligence response including health assessment,
        /// lifecycle recommendations, and normalized recent events.
        /// Partial upstream failures produce a degraded-mode response rather than a hard error.
        /// </summary>
        /// <param name="request">Request specifying which token to evaluate and options</param>
        /// <returns>Consolidated operations intelligence response</returns>
        Task<TokenOperationsIntelligenceResponse> GetOperationsIntelligenceAsync(
            TokenOperationsIntelligenceRequest request);

        /// <summary>
        /// Evaluates token health across all (or specified) policy dimensions.
        /// Returns deterministic results for identical inputs.
        /// </summary>
        /// <param name="assetId">Token asset ID</param>
        /// <param name="network">Network identifier</param>
        /// <param name="dimensions">Optional filter for specific dimensions</param>
        /// <param name="stateInputs">Optional token state that drives evaluator conditions</param>
        /// <returns>Aggregated health assessment</returns>
        Task<TokenHealthAssessment> EvaluateHealthAsync(
            ulong assetId,
            string network,
            IEnumerable<string>? dimensions = null,
            TokenStateInputs? stateInputs = null);

        /// <summary>
        /// Generates deterministic lifecycle recommendations for a token based on its current state.
        /// </summary>
        /// <param name="assetId">Token asset ID</param>
        /// <param name="network">Network identifier</param>
        /// <returns>Ordered list of lifecycle recommendations (highest priority first)</returns>
        Task<List<LifecycleRecommendation>> GetRecommendationsAsync(ulong assetId, string network);

        /// <summary>
        /// Returns recent normalized token-affecting events with actor attribution and impact categorization.
        /// </summary>
        /// <param name="assetId">Token asset ID</param>
        /// <param name="network">Network identifier</param>
        /// <param name="maxEvents">Maximum number of events to return</param>
        /// <param name="includeDetails">Whether to include full event details</param>
        /// <returns>List of normalized events</returns>
        Task<List<NormalizedTokenEvent>> GetNormalizedEventsAsync(
            ulong assetId,
            string network,
            int maxEvents = 10,
            bool includeDetails = false);
    }
}
