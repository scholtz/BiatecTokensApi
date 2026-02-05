using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for managing jurisdiction rules
    /// </summary>
    public interface IJurisdictionRulesRepository
    {
        /// <summary>
        /// Creates a new jurisdiction rule
        /// </summary>
        /// <param name="rule">The jurisdiction rule to create</param>
        /// <returns>The created jurisdiction rule with assigned ID</returns>
        Task<JurisdictionRule> CreateRuleAsync(JurisdictionRule rule);

        /// <summary>
        /// Gets a jurisdiction rule by ID
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <returns>The jurisdiction rule if found, null otherwise</returns>
        Task<JurisdictionRule?> GetRuleByIdAsync(string ruleId);

        /// <summary>
        /// Gets a jurisdiction rule by jurisdiction code
        /// </summary>
        /// <param name="jurisdictionCode">The jurisdiction code</param>
        /// <returns>The jurisdiction rule if found, null otherwise</returns>
        Task<JurisdictionRule?> GetRuleByJurisdictionCodeAsync(string jurisdictionCode);

        /// <summary>
        /// Gets all jurisdiction rules matching the filter criteria
        /// </summary>
        /// <param name="request">The filter request</param>
        /// <returns>List of matching jurisdiction rules</returns>
        Task<(List<JurisdictionRule> Rules, int TotalCount)> ListRulesAsync(ListJurisdictionRulesRequest request);

        /// <summary>
        /// Updates an existing jurisdiction rule
        /// </summary>
        /// <param name="rule">The jurisdiction rule with updated values</param>
        /// <returns>The updated jurisdiction rule</returns>
        Task<JurisdictionRule> UpdateRuleAsync(JurisdictionRule rule);

        /// <summary>
        /// Deletes a jurisdiction rule by ID
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteRuleAsync(string ruleId);

        /// <summary>
        /// Gets all active jurisdiction rules
        /// </summary>
        /// <returns>List of active jurisdiction rules ordered by priority</returns>
        Task<List<JurisdictionRule>> GetActiveRulesAsync();

        /// <summary>
        /// Gets a token's jurisdiction assignments
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="network">The network</param>
        /// <returns>List of jurisdiction assignments for the token</returns>
        Task<List<TokenJurisdiction>> GetTokenJurisdictionsAsync(ulong assetId, string network);

        /// <summary>
        /// Assigns a jurisdiction to a token
        /// </summary>
        /// <param name="tokenJurisdiction">The jurisdiction assignment</param>
        /// <returns>The created jurisdiction assignment</returns>
        Task<TokenJurisdiction> AssignTokenJurisdictionAsync(TokenJurisdiction tokenJurisdiction);

        /// <summary>
        /// Removes a jurisdiction assignment from a token
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="network">The network</param>
        /// <param name="jurisdictionCode">The jurisdiction code</param>
        /// <returns>True if removed, false if not found</returns>
        Task<bool> RemoveTokenJurisdictionAsync(ulong assetId, string network, string jurisdictionCode);
    }
}
