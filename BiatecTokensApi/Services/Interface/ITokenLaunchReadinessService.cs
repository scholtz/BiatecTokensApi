using BiatecTokensApi.Models.TokenLaunch;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for evaluating token launch readiness
    /// </summary>
    /// <remarks>
    /// Orchestrates all compliance checks and integrations to provide a deterministic
    /// assessment of whether a token launch can proceed. Aggregates entitlements,
    /// account state, compliance decisions, KYC/AML status, jurisdiction constraints,
    /// and integration health into a unified readiness model.
    /// </remarks>
    public interface ITokenLaunchReadinessService
    {
        /// <summary>
        /// Evaluates comprehensive token launch readiness
        /// </summary>
        /// <param name="request">Readiness evaluation request</param>
        /// <returns>Comprehensive readiness assessment with remediation tasks</returns>
        Task<TokenLaunchReadinessResponse> EvaluateReadinessAsync(TokenLaunchReadinessRequest request);

        /// <summary>
        /// Retrieves a previous readiness evaluation
        /// </summary>
        /// <param name="evaluationId">Evaluation identifier</param>
        /// <returns>Token launch readiness response if found</returns>
        Task<TokenLaunchReadinessResponse?> GetEvaluationAsync(string evaluationId);

        /// <summary>
        /// Retrieves readiness evaluation history for a user
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="limit">Maximum number of results</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <returns>List of historical evaluations</returns>
        Task<List<TokenLaunchReadinessResponse>> GetEvaluationHistoryAsync(
            string userId,
            int limit = 50,
            DateTime? fromDate = null);
    }
}
