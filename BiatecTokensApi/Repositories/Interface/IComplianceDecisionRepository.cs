using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Interface for compliance decision repository operations
    /// </summary>
    public interface IComplianceDecisionRepository
    {
        /// <summary>
        /// Creates a new compliance decision
        /// </summary>
        /// <param name="decision">The decision to create</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task CreateDecisionAsync(ComplianceDecision decision);

        /// <summary>
        /// Gets a compliance decision by ID
        /// </summary>
        /// <param name="decisionId">The decision ID</param>
        /// <returns>The compliance decision, or null if not found</returns>
        Task<ComplianceDecision?> GetDecisionByIdAsync(string decisionId);

        /// <summary>
        /// Queries compliance decisions with filtering and pagination
        /// </summary>
        /// <param name="request">The query request</param>
        /// <returns>List of matching decisions and total count</returns>
        Task<(List<ComplianceDecision> decisions, int totalCount)> QueryDecisionsAsync(QueryComplianceDecisionsRequest request);

        /// <summary>
        /// Gets the most recent active decision for an organization and step
        /// </summary>
        /// <param name="organizationId">The organization ID</param>
        /// <param name="step">The onboarding step</param>
        /// <returns>The most recent active decision, or null if not found</returns>
        Task<ComplianceDecision?> GetActiveDecisionAsync(string organizationId, OnboardingStep step);

        /// <summary>
        /// Marks a decision as superseded
        /// </summary>
        /// <param name="decisionId">The decision ID to supersede</param>
        /// <param name="supersededById">The ID of the new decision</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> SupersedeDecisionAsync(string decisionId, string supersededById);

        /// <summary>
        /// Gets decisions requiring review
        /// </summary>
        /// <param name="beforeDate">Optional date filter - decisions with review date before this</param>
        /// <returns>List of decisions requiring review</returns>
        Task<List<ComplianceDecision>> GetDecisionsRequiringReviewAsync(DateTime? beforeDate = null);

        /// <summary>
        /// Gets expired decisions
        /// </summary>
        /// <returns>List of expired decisions</returns>
        Task<List<ComplianceDecision>> GetExpiredDecisionsAsync();

        /// <summary>
        /// Checks if a decision with the same parameters already exists (for idempotency)
        /// </summary>
        /// <param name="organizationId">Organization ID</param>
        /// <param name="step">Onboarding step</param>
        /// <param name="policyVersion">Policy version</param>
        /// <param name="evidenceReferenceIds">List of evidence reference IDs</param>
        /// <returns>Existing decision if found, null otherwise</returns>
        Task<ComplianceDecision?> FindDuplicateDecisionAsync(
            string organizationId, 
            OnboardingStep step, 
            string policyVersion,
            List<string> evidenceReferenceIds);
    }
}
