using BiatecTokensApi.Models.LifecycleIntelligence;
using BiatecTokensApi.Models.TokenLaunch;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for lifecycle intelligence operations
    /// </summary>
    public interface ILifecycleIntelligenceService
    {
        /// <summary>
        /// Evaluates token launch readiness with detailed factor breakdown (v2)
        /// </summary>
        /// <param name="request">Readiness evaluation request</param>
        /// <returns>Enhanced readiness response with scoring, confidence, and evidence</returns>
        Task<TokenLaunchReadinessResponseV2> EvaluateReadinessV2Async(TokenLaunchReadinessRequest request);

        /// <summary>
        /// Retrieves evidence for a specific evaluation
        /// </summary>
        /// <param name="evidenceId">Evidence identifier</param>
        /// <param name="includeContent">Whether to include full evidence content</param>
        /// <returns>Evidence retrieval response</returns>
        Task<EvidenceRetrievalResponse?> GetEvidenceAsync(string evidenceId, bool includeContent = false);

        /// <summary>
        /// Retrieves post-launch risk signals for a token
        /// </summary>
        /// <param name="request">Risk signals request with filters</param>
        /// <returns>Risk signals response with severity and trend data</returns>
        Task<RiskSignalsResponse> GetRiskSignalsAsync(RiskSignalsRequest request);
    }
}
