using BiatecTokensApi.Models.TokenLaunch;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository for token launch readiness evaluations and evidence
    /// </summary>
    public interface ITokenLaunchReadinessRepository
    {
        /// <summary>
        /// Stores an evidence snapshot
        /// </summary>
        Task<TokenLaunchReadinessEvidence> StoreEvidenceAsync(TokenLaunchReadinessEvidence evidence);

        /// <summary>
        /// Retrieves evidence by evaluation ID
        /// </summary>
        Task<TokenLaunchReadinessEvidence?> GetEvidenceByEvaluationIdAsync(string evaluationId);

        /// <summary>
        /// Retrieves evidence history for a user
        /// </summary>
        Task<List<TokenLaunchReadinessEvidence>> GetEvidenceHistoryAsync(
            string userId,
            int limit = 50,
            DateTime? fromDate = null);

        /// <summary>
        /// Retrieves evidence related to a token deployment
        /// </summary>
        Task<List<TokenLaunchReadinessEvidence>> GetEvidenceByTokenDeploymentAsync(string tokenDeploymentId);

        /// <summary>
        /// Retrieves the most recent readiness evaluation for a user
        /// </summary>
        Task<TokenLaunchReadinessEvidence?> GetLatestEvaluationAsync(string userId);
    }
}
