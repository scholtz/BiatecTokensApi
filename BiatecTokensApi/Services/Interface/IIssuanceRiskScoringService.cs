using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for the issuance compliance risk scoring service.
    /// Provides deterministic, threshold-based risk assessment for token issuance workflows.
    /// </summary>
    public interface IIssuanceRiskScoringService
    {
        /// <summary>
        /// Evaluates the compliance risk of a token issuance request and returns a deterministic decision.
        /// The decision is derived from a normalized aggregate risk score computed across KYC,
        /// sanctions screening, and jurisdiction components.
        /// </summary>
        /// <param name="request">The issuance risk evaluation request containing all evidence inputs</param>
        /// <returns>
        /// A deterministic evaluation response with decision (allow/review/deny),
        /// aggregate risk score, component scores, structured evidence blocks, and reason codes.
        /// </returns>
        Task<IssuanceRiskEvaluationResponse> EvaluateAsync(IssuanceRiskEvaluationRequest request);
    }
}
