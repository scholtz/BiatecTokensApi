using BiatecTokensApi.Models.IssuancePolicy;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for managing issuance compliance policies and evaluating participant eligibility
    /// </summary>
    public interface IIssuancePolicyService
    {
        /// <summary>
        /// Creates a new issuance compliance policy for a token
        /// </summary>
        /// <param name="request">The policy configuration</param>
        /// <param name="issuerId">Address of the issuer creating the policy</param>
        /// <returns>Response containing the created policy</returns>
        Task<IssuancePolicyResponse> CreatePolicyAsync(CreateIssuancePolicyRequest request, string issuerId);

        /// <summary>
        /// Gets a policy by its unique ID
        /// </summary>
        /// <param name="policyId">The policy's unique identifier</param>
        /// <param name="requesterId">Address of the requesting user (must be the policy owner)</param>
        /// <returns>Response containing the policy, or error if not found / not authorized</returns>
        Task<IssuancePolicyResponse> GetPolicyAsync(string policyId, string requesterId);

        /// <summary>
        /// Gets the active policy for a specific asset
        /// </summary>
        /// <param name="assetId">The asset ID to look up</param>
        /// <param name="requesterId">Address of the requesting user (must be the policy owner)</param>
        /// <returns>Response containing the policy, or error if not found</returns>
        Task<IssuancePolicyResponse> GetPolicyByAssetAsync(ulong assetId, string requesterId);

        /// <summary>
        /// Updates an existing policy (only the issuer who created it may update)
        /// </summary>
        /// <param name="policyId">The policy's unique identifier</param>
        /// <param name="request">Fields to update (only non-null fields are applied)</param>
        /// <param name="requesterId">Address of the requesting user (must be the policy owner)</param>
        /// <returns>Response containing the updated policy</returns>
        Task<IssuancePolicyResponse> UpdatePolicyAsync(string policyId, UpdateIssuancePolicyRequest request, string requesterId);

        /// <summary>
        /// Deletes a policy (only the issuer who created it may delete)
        /// </summary>
        /// <param name="policyId">The policy's unique identifier</param>
        /// <param name="requesterId">Address of the requesting user (must be the policy owner)</param>
        /// <returns>Response indicating success or failure</returns>
        Task<IssuancePolicyResponse> DeletePolicyAsync(string policyId, string requesterId);

        /// <summary>
        /// Lists all policies owned by the requesting issuer
        /// </summary>
        /// <param name="issuerId">Address of the issuer</param>
        /// <returns>List response containing all policies for the issuer</returns>
        Task<IssuancePolicyListResponse> ListPoliciesAsync(string issuerId);

        /// <summary>
        /// Evaluates whether a participant is eligible for a token issuance under a given policy
        /// </summary>
        /// <param name="policyId">The policy to evaluate against</param>
        /// <param name="request">Participant details and context</param>
        /// <param name="evaluatorId">Address of the entity requesting the evaluation</param>
        /// <returns>Decision result with outcome, matched rules, and audit metadata</returns>
        Task<IssuancePolicyDecisionResult> EvaluateParticipantAsync(string policyId, EvaluateParticipantRequest request, string evaluatorId);
    }
}
