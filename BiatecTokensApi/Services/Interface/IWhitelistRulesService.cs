using BiatecTokensApi.Models.Whitelist;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for managing whitelist rules
    /// </summary>
    public interface IWhitelistRulesService
    {
        /// <summary>
        /// Creates a new whitelist rule
        /// </summary>
        /// <param name="request">The rule creation request</param>
        /// <param name="createdBy">The address of the user creating the rule</param>
        /// <returns>The rule response</returns>
        Task<WhitelistRuleResponse> CreateRuleAsync(CreateWhitelistRuleRequest request, string createdBy);

        /// <summary>
        /// Updates an existing whitelist rule
        /// </summary>
        /// <param name="request">The rule update request</param>
        /// <param name="updatedBy">The address of the user updating the rule</param>
        /// <returns>The rule response</returns>
        Task<WhitelistRuleResponse> UpdateRuleAsync(UpdateWhitelistRuleRequest request, string updatedBy);

        /// <summary>
        /// Gets a rule by ID
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <returns>The rule response</returns>
        Task<WhitelistRuleResponse> GetRuleAsync(string ruleId);

        /// <summary>
        /// Deletes a rule
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <param name="deletedBy">The address of the user deleting the rule</param>
        /// <returns>The response indicating success or failure</returns>
        Task<WhitelistRuleResponse> DeleteRuleAsync(string ruleId, string deletedBy);

        /// <summary>
        /// Lists rules for an asset
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>The list response with rules and pagination</returns>
        Task<WhitelistRulesListResponse> ListRulesAsync(ListWhitelistRulesRequest request);

        /// <summary>
        /// Applies a rule to existing whitelist entries
        /// </summary>
        /// <param name="request">The apply rule request</param>
        /// <param name="performedBy">The address of the user applying the rule</param>
        /// <returns>The apply rule response with validation results</returns>
        Task<ApplyRuleResponse> ApplyRuleAsync(ApplyWhitelistRuleRequest request, string performedBy);

        /// <summary>
        /// Validates whitelist entries against rules
        /// </summary>
        /// <param name="request">The validation request</param>
        /// <returns>The validation response</returns>
        Task<ValidateAgainstRulesResponse> ValidateAgainstRulesAsync(ValidateAgainstRulesRequest request);

        /// <summary>
        /// Validates a single whitelist entry against a specific rule
        /// </summary>
        /// <param name="entry">The whitelist entry to validate</param>
        /// <param name="rule">The rule to validate against</param>
        /// <returns>Validation error if failed, null if passed</returns>
        Task<RuleValidationError?> ValidateEntryAgainstRuleAsync(WhitelistEntry entry, WhitelistRule rule);
    }
}
