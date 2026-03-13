using BiatecTokensApi.Models.Whitelist;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for managing and evaluating whitelist policies
    /// </summary>
    public interface IWhitelistPolicyService
    {
        /// <summary>
        /// Creates a new whitelist policy in Draft state
        /// </summary>
        /// <param name="request">The creation request</param>
        /// <param name="createdBy">Identifier of the user creating the policy</param>
        /// <returns>The created policy response</returns>
        Task<WhitelistPolicyResponse> CreatePolicyAsync(CreateWhitelistPolicyRequest request, string createdBy);

        /// <summary>
        /// Retrieves a whitelist policy by its unique ID
        /// </summary>
        /// <param name="policyId">The policy ID</param>
        /// <returns>The policy response, or failure if not found</returns>
        Task<WhitelistPolicyResponse> GetPolicyAsync(string policyId);

        /// <summary>
        /// Retrieves all whitelist policies, optionally filtered by asset ID
        /// </summary>
        /// <param name="assetId">Optional asset ID filter</param>
        /// <returns>List of policies</returns>
        Task<WhitelistPolicyListResponse> GetPoliciesAsync(ulong? assetId = null);

        /// <summary>
        /// Updates an existing whitelist policy
        /// </summary>
        /// <param name="policyId">The policy ID</param>
        /// <param name="request">The update request</param>
        /// <param name="updatedBy">Identifier of the user updating the policy</param>
        /// <returns>The updated policy response</returns>
        Task<WhitelistPolicyResponse> UpdatePolicyAsync(string policyId, UpdateWhitelistPolicyRequest request, string updatedBy);

        /// <summary>
        /// Archives a whitelist policy (soft delete)
        /// </summary>
        /// <param name="policyId">The policy ID</param>
        /// <param name="archivedBy">Identifier of the user archiving the policy</param>
        /// <returns>The archived policy response</returns>
        Task<WhitelistPolicyResponse> ArchivePolicyAsync(string policyId, string archivedBy);

        /// <summary>
        /// Validates a whitelist policy for contradictions and completeness issues
        /// </summary>
        /// <param name="policyId">The policy ID</param>
        /// <returns>Validation result with any issues found</returns>
        Task<WhitelistPolicyValidationResult> ValidatePolicyAsync(string policyId);

        /// <summary>
        /// Evaluates a participant's eligibility against a policy using fail-closed semantics.
        /// Draft policies and ambiguous/empty policies always return Deny.
        /// </summary>
        /// <param name="request">The eligibility evaluation request</param>
        /// <returns>The eligibility result with outcome and reasons</returns>
        Task<WhitelistPolicyEligibilityResult> EvaluateEligibilityAsync(WhitelistPolicyEligibilityRequest request);

        /// <summary>
        /// Retrieves paginated audit history for a whitelist policy
        /// </summary>
        /// <param name="policyId">The policy ID</param>
        /// <param name="request">Pagination and filter parameters</param>
        /// <returns>Paginated list of audit events</returns>
        Task<WhitelistAuditHistoryResponse> GetAuditHistoryAsync(string policyId, WhitelistAuditHistoryRequest request);

        /// <summary>
        /// Generates a compliance evidence report for a whitelist policy
        /// </summary>
        /// <param name="policyId">The policy ID</param>
        /// <param name="request">Report parameters</param>
        /// <param name="requestedBy">Identifier of the actor requesting the report</param>
        /// <returns>The compliance evidence report</returns>
        Task<WhitelistComplianceEvidenceReport> GetComplianceEvidenceAsync(string policyId, WhitelistComplianceEvidenceRequest request, string requestedBy);
    }
}
