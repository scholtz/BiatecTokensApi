using BiatecTokensApi.Models.Portfolio;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for token portfolio intelligence.
    /// Aggregates token metadata, wallet holdings context, and user-action affordances
    /// into a single enriched portfolio response.
    /// All operations are deterministic: identical inputs produce identical outputs.
    /// </summary>
    public interface IPortfolioIntelligenceService
    {
        /// <summary>
        /// Evaluates portfolio intelligence for a given wallet address and network.
        /// Returns per-holding risk levels, confidence indicators, wallet compatibility,
        /// action readiness, and discovered opportunities.
        /// Partial upstream failures produce a degraded-mode response rather than a hard error.
        /// </summary>
        /// <param name="request">Portfolio intelligence request.</param>
        /// <returns>Enriched portfolio intelligence response.</returns>
        Task<PortfolioIntelligenceResponse> GetPortfolioIntelligenceAsync(PortfolioIntelligenceRequest request);

        /// <summary>
        /// Evaluates the wallet compatibility status for a given address and network.
        /// Returns a status and human-readable message suitable for surfacing in UI loading states.
        /// </summary>
        /// <param name="walletAddress">Wallet address to check.</param>
        /// <param name="network">Network identifier.</param>
        /// <param name="tokenStandard">Optional token standard to check wallet type support.</param>
        /// <returns>Compatibility status and descriptive message.</returns>
        (WalletCompatibilityStatus Status, string Message) EvaluateWalletCompatibility(
            string walletAddress,
            string network,
            string? tokenStandard = null);

        /// <summary>
        /// Computes the aggregate risk level across multiple individual holding risk levels.
        /// Uses a deterministic worst-case aggregation strategy.
        /// </summary>
        /// <param name="holdingRisks">Individual holding risk levels.</param>
        /// <returns>Aggregate risk level.</returns>
        HoldingRiskLevel AggregateRisk(IEnumerable<HoldingRiskLevel> holdingRisks);

        /// <summary>
        /// Evaluates the risk level of a single holding based on available signals.
        /// </summary>
        /// <param name="assetId">Asset ID.</param>
        /// <param name="network">Network identifier.</param>
        /// <param name="hasMintAuthority">Whether mint authority is still active.</param>
        /// <param name="metadataComplete">Whether token metadata is complete.</param>
        /// <param name="isVerified">Whether token has been externally verified.</param>
        /// <returns>Risk level and associated signals.</returns>
        (HoldingRiskLevel Risk, ConfidenceLevel Confidence, List<RiskSignal> Signals, List<ConfidenceIndicator> Indicators)
            EvaluateHoldingRisk(ulong assetId, string network, bool hasMintAuthority, bool metadataComplete, bool isVerified);
    }
}
