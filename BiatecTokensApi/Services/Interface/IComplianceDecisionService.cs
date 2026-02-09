using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for compliance decision service operations
    /// </summary>
    public interface IComplianceDecisionService
    {
        /// <summary>
        /// Creates a new compliance decision with policy evaluation
        /// </summary>
        /// <param name="request">The decision creation request</param>
        /// <param name="actorAddress">Address of the actor making the decision</param>
        /// <returns>Response containing the created decision and evaluation result</returns>
        Task<ComplianceDecisionResponse> CreateDecisionAsync(CreateComplianceDecisionRequest request, string actorAddress);

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
        /// <returns>Response containing matching decisions and pagination info</returns>
        Task<QueryComplianceDecisionsResponse> QueryDecisionsAsync(QueryComplianceDecisionsRequest request);

        /// <summary>
        /// Gets the most recent active decision for an organization and step
        /// </summary>
        /// <param name="organizationId">The organization ID</param>
        /// <param name="step">The onboarding step</param>
        /// <returns>The most recent active decision, or null if not found</returns>
        Task<ComplianceDecision?> GetActiveDecisionAsync(string organizationId, OnboardingStep step);

        /// <summary>
        /// Updates an existing decision by creating a new one that supersedes it
        /// </summary>
        /// <param name="previousDecisionId">ID of the decision to supersede</param>
        /// <param name="request">The new decision request</param>
        /// <param name="actorAddress">Address of the actor making the update</param>
        /// <returns>Response containing the new decision</returns>
        Task<ComplianceDecisionResponse> UpdateDecisionAsync(string previousDecisionId, CreateComplianceDecisionRequest request, string actorAddress);

        /// <summary>
        /// Gets decisions requiring review
        /// </summary>
        /// <param name="beforeDate">Optional date filter</param>
        /// <returns>List of decisions requiring review</returns>
        Task<List<ComplianceDecision>> GetDecisionsRequiringReviewAsync(DateTime? beforeDate = null);

        /// <summary>
        /// Gets expired decisions
        /// </summary>
        /// <returns>List of expired decisions</returns>
        Task<List<ComplianceDecision>> GetExpiredDecisionsAsync();
    }
}
