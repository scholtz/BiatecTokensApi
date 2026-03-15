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

        /// <summary>
        /// Appends a reviewer note or evidence reference to an existing compliance decision.
        /// </summary>
        /// <param name="decisionId">The decision ID to annotate.</param>
        /// <param name="request">The note content and optional evidence references.</param>
        /// <param name="actorId">The authenticated user/actor submitting the note.</param>
        /// <param name="correlationId">Correlation ID for end-to-end tracing.</param>
        /// <returns>
        /// A <see cref="AppendReviewerNoteResponse"/> containing the created note on success,
        /// or a failed response with error code <c>COMPLIANCE_CHECK_NOT_FOUND</c> or
        /// <c>MISSING_REQUIRED_FIELD</c> when validation fails.
        /// </returns>
        Task<AppendReviewerNoteResponse> AppendReviewerNoteAsync(
            string decisionId,
            AppendReviewerNoteRequest request,
            string actorId,
            string correlationId);

        /// <summary>
        /// Initiates a rescreen for a subject whose evidence is stale or expired.
        /// A new compliance decision is created using the same subject/context as the original,
        /// optionally with updated metadata or check parameters.
        /// </summary>
        /// <param name="decisionId">The original decision ID to rescreen.</param>
        /// <param name="request">Optional override parameters for the rescreen.</param>
        /// <param name="actorId">The authenticated user/actor requesting the rescreen.</param>
        /// <param name="correlationId">Correlation ID for end-to-end tracing.</param>
        /// <returns>
        /// A <see cref="RescreenResponse"/> with the new decision on success,
        /// or a failed response with error code <c>COMPLIANCE_CHECK_NOT_FOUND</c> when
        /// the original decision does not exist.
        /// </returns>
        Task<RescreenResponse> RescreenAsync(
            string decisionId,
            RescreenRequest request,
            string actorId,
            string correlationId);

        /// <summary>
        /// Processes an inbound provider webhook/callback event and updates the corresponding
        /// compliance decision.
        /// </summary>
        /// <param name="request">The normalised provider callback payload.</param>
        /// <param name="correlationId">Correlation ID for end-to-end tracing.</param>
        /// <returns>
        /// A <see cref="ProviderCallbackResponse"/> indicating whether the decision was updated,
        /// an idempotent replay was detected, or the callback was invalid/unknown.
        /// </returns>
        Task<ProviderCallbackResponse> ProcessProviderCallbackAsync(
            ProviderCallbackRequest request,
            string correlationId);
    }
}
