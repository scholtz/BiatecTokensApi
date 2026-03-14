using BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for compliance evidence and launch decision evaluations.
    /// </summary>
    /// <remarks>
    /// Provides deterministic, idempotent evaluation of all compliance prerequisites
    /// required before a token launch is permitted.  Every evaluation produces a
    /// structured decision record with blockers, warnings, and a full decision trace
    /// that can be used for audit and regulatory review.
    /// </remarks>
    public interface IComplianceEvidenceLaunchDecisionService
    {
        /// <summary>
        /// Evaluates whether a token launch is permitted, producing a structured decision.
        /// </summary>
        /// <param name="request">Launch decision request.</param>
        /// <returns>Comprehensive decision response with status, blockers, warnings, and evidence.</returns>
        Task<LaunchDecisionResponse> EvaluateLaunchDecisionAsync(LaunchDecisionRequest request);

        /// <summary>
        /// Retrieves a previously computed launch decision by ID.
        /// </summary>
        /// <param name="decisionId">Decision identifier.</param>
        /// <param name="correlationId">Optional correlation ID for tracing.</param>
        /// <returns>Decision response if found; null otherwise.</returns>
        Task<LaunchDecisionResponse?> GetDecisionAsync(string decisionId, string? correlationId = null);

        /// <summary>
        /// Retrieves a compliance evidence bundle for an owner.
        /// </summary>
        /// <param name="request">Evidence bundle request.</param>
        /// <returns>Bundle of compliance evidence items with provenance metadata.</returns>
        Task<EvidenceBundleResponse> GetEvidenceBundleAsync(EvidenceBundleRequest request);

        /// <summary>
        /// Retrieves the decision trace for a specific evaluation.
        /// </summary>
        /// <param name="request">Decision trace request.</param>
        /// <returns>Structured decision trace with per-rule evaluation records.</returns>
        Task<DecisionTraceResponse> GetDecisionTraceAsync(DecisionTraceRequest request);

        /// <summary>
        /// Lists launch decisions for an owner, most recent first.
        /// </summary>
        /// <param name="ownerId">Owner identifier.</param>
        /// <param name="limit">Maximum number of results (1–100).</param>
        /// <param name="correlationId">Optional correlation ID.</param>
        /// <returns>List of decision responses ordered by <see cref="LaunchDecisionResponse.DecidedAt"/> descending.</returns>
        Task<List<LaunchDecisionResponse>> ListDecisionsAsync(
            string ownerId,
            int limit = 20,
            string? correlationId = null);

        /// <summary>
        /// Exports a compliance evidence bundle as a downloadable JSON artifact.
        /// </summary>
        /// <param name="request">Export request with optional filters.</param>
        /// <returns>
        /// Export result containing UTF-8 JSON bytes, suggested filename,
        /// and an export manifest with integrity hash.
        /// </returns>
        Task<EvidenceExportResult> ExportEvidenceBundleAsJsonAsync(EvidenceExportRequest request);

        /// <summary>
        /// Exports a compliance evidence bundle as a downloadable CSV artifact.
        /// </summary>
        /// <param name="request">Export request with optional filters.</param>
        /// <returns>
        /// Export result containing UTF-8 CSV bytes, suggested filename,
        /// and an export manifest describing the tabular evidence summary.
        /// </returns>
        Task<EvidenceExportResult> ExportEvidenceBundleAsCsvAsync(EvidenceExportRequest request);
    }
}
