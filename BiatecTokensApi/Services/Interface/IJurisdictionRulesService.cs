using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for managing and evaluating jurisdiction rules
    /// </summary>
    public interface IJurisdictionRulesService
    {
        /// <summary>
        /// Creates a new jurisdiction rule
        /// </summary>
        /// <param name="request">The rule creation request</param>
        /// <param name="createdBy">The user creating the rule</param>
        /// <returns>Response containing the created rule</returns>
        Task<JurisdictionRuleResponse> CreateRuleAsync(CreateJurisdictionRuleRequest request, string createdBy);

        /// <summary>
        /// Gets a jurisdiction rule by ID
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <returns>Response containing the rule if found</returns>
        Task<JurisdictionRuleResponse> GetRuleByIdAsync(string ruleId);

        /// <summary>
        /// Lists jurisdiction rules with optional filtering
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>Response containing the list of rules</returns>
        Task<ListJurisdictionRulesResponse> ListRulesAsync(ListJurisdictionRulesRequest request);

        /// <summary>
        /// Updates an existing jurisdiction rule
        /// </summary>
        /// <param name="ruleId">The rule ID to update</param>
        /// <param name="request">The updated rule data</param>
        /// <param name="updatedBy">The user updating the rule</param>
        /// <returns>Response containing the updated rule</returns>
        Task<JurisdictionRuleResponse> UpdateRuleAsync(string ruleId, CreateJurisdictionRuleRequest request, string updatedBy);

        /// <summary>
        /// Deletes a jurisdiction rule
        /// </summary>
        /// <param name="ruleId">The rule ID to delete</param>
        /// <returns>Response indicating success or failure</returns>
        Task<BaseResponse> DeleteRuleAsync(string ruleId);

        /// <summary>
        /// Evaluates jurisdiction rules for a token and returns compliance status
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="network">The network</param>
        /// <param name="issuerId">The issuer address</param>
        /// <returns>Evaluation result with compliance status and check results</returns>
        Task<JurisdictionEvaluationResult> EvaluateTokenComplianceAsync(ulong assetId, string network, string issuerId);

        /// <summary>
        /// Assigns a jurisdiction to a token
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="network">The network</param>
        /// <param name="jurisdictionCode">The jurisdiction code</param>
        /// <param name="isPrimary">Whether this is the primary jurisdiction</param>
        /// <param name="assignedBy">The user assigning the jurisdiction</param>
        /// <param name="notes">Optional notes</param>
        /// <returns>Response indicating success or failure</returns>
        Task<BaseResponse> AssignTokenJurisdictionAsync(ulong assetId, string network, string jurisdictionCode, 
            bool isPrimary, string assignedBy, string? notes = null);

        /// <summary>
        /// Gets jurisdiction assignments for a token
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="network">The network</param>
        /// <returns>List of jurisdiction assignments</returns>
        Task<List<TokenJurisdiction>> GetTokenJurisdictionsAsync(ulong assetId, string network);

        /// <summary>
        /// Removes a jurisdiction assignment from a token
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="network">The network</param>
        /// <param name="jurisdictionCode">The jurisdiction code</param>
        /// <returns>Response indicating success or failure</returns>
        Task<BaseResponse> RemoveTokenJurisdictionAsync(ulong assetId, string network, string jurisdictionCode);
    }
}
