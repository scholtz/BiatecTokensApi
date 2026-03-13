using BiatecTokensApi.Models.EnterpriseComplianceReview;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for durable enterprise compliance review persistence.
    ///
    /// Stores decision metadata and diagnostics events so that review history
    /// can be reconstructed after application restart or redeployment.
    /// </summary>
    public interface IComplianceReviewRepository
    {
        // ── Decision Metadata ──────────────────────────────────────────────────

        /// <summary>
        /// Persists structured review decision metadata. Keyed by decisionId.
        /// Each call creates a new immutable record.
        /// </summary>
        Task SaveDecisionAsync(PersistedReviewDecision decision);

        /// <summary>Retrieves a single decision record by its unique ID.</summary>
        Task<PersistedReviewDecision?> GetDecisionByIdAsync(string decisionId);

        /// <summary>
        /// Returns all decisions for a workflow item ordered by timestamp ascending.
        /// Used to reconstruct enriched audit history.
        /// </summary>
        Task<List<PersistedReviewDecision>> GetDecisionsForWorkflowAsync(string issuerId, string workflowId);

        /// <summary>
        /// Returns all decisions for an issuer within the supplied time range.
        /// Supports audit export and reporting queries.
        /// </summary>
        Task<List<PersistedReviewDecision>> QueryDecisionsAsync(
            string issuerId,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            ReviewDecisionType? decisionType = null,
            string? actorId = null);

        // ── Diagnostics Events ─────────────────────────────────────────────────

        /// <summary>
        /// Appends a diagnostics event for the given issuer.
        /// Bounded to a per-issuer maximum; oldest events are evicted when the limit is reached.
        /// </summary>
        Task AppendDiagnosticsEventAsync(string issuerId, ReviewDiagnosticsEvent ev);

        /// <summary>
        /// Returns the most recent diagnostics events for an issuer, newest first.
        /// </summary>
        Task<List<ReviewDiagnosticsEvent>> GetRecentDiagnosticsEventsAsync(
            string issuerId, int maxCount = 50);
    }
}
