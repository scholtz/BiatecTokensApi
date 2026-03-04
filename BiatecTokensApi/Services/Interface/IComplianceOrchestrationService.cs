using BiatecTokensApi.Models.ComplianceOrchestration;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Orchestrates KYC and AML compliance checks, maintains an auditable decision trail,
    /// and enforces idempotency for repeated requests.
    /// </summary>
    public interface IComplianceOrchestrationService
    {
        /// <summary>
        /// Initiates a new compliance check (or returns the cached result for the same idempotency key).
        /// </summary>
        /// <param name="request">The compliance check request.</param>
        /// <param name="actorId">The authenticated user/actor performing this action.</param>
        /// <param name="correlationId">Correlation ID for end-to-end tracing.</param>
        /// <returns>The compliance check response including state and audit trail.</returns>
        Task<ComplianceCheckResponse> InitiateCheckAsync(
            InitiateComplianceCheckRequest request,
            string actorId,
            string correlationId);

        /// <summary>
        /// Retrieves the current state of a compliance check by its decision ID.
        /// </summary>
        /// <param name="decisionId">The decision ID returned by <see cref="InitiateCheckAsync"/>.</param>
        /// <returns>
        /// A <see cref="ComplianceCheckResponse"/> with the current decision state,
        /// or a failed response with error code <c>COMPLIANCE_CHECK_NOT_FOUND</c>.
        /// </returns>
        Task<ComplianceCheckResponse> GetCheckStatusAsync(string decisionId);

        /// <summary>
        /// Retrieves the full decision history for a given subject.
        /// </summary>
        /// <param name="subjectId">The subject identifier to query.</param>
        /// <returns>All decisions recorded for the subject, ordered by initiation time descending.</returns>
        Task<ComplianceDecisionHistoryResponse> GetDecisionHistoryAsync(string subjectId);
    }
}
