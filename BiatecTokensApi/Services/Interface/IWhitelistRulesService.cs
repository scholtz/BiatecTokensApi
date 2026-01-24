using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Models;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for whitelisting rules service operations
    /// </summary>
    public interface IWhitelistRulesService
    {
        /// <summary>
        /// Creates a new whitelisting rule
        /// </summary>
        /// <param name="request">The create rule request</param>
        /// <param name="createdBy">The address of the user creating the rule</param>
        /// <returns>The rule response</returns>
        Task<WhitelistRuleResponse> CreateRuleAsync(CreateWhitelistRuleRequest request, string createdBy);

        /// <summary>
        /// Updates an existing whitelisting rule
        /// </summary>
        /// <param name="request">The update rule request</param>
        /// <param name="updatedBy">The address of the user updating the rule</param>
        /// <returns>The rule response</returns>
        Task<WhitelistRuleResponse> UpdateRuleAsync(UpdateWhitelistRuleRequest request, string updatedBy);

        /// <summary>
        /// Lists whitelisting rules for a specific asset
        /// </summary>
        /// <param name="request">The list rules request</param>
        /// <returns>The list response with rules</returns>
        Task<WhitelistRulesListResponse> ListRulesAsync(ListWhitelistRulesRequest request);

        /// <summary>
        /// Applies a whitelisting rule to matching whitelist entries
        /// </summary>
        /// <param name="request">The apply rule request</param>
        /// <param name="appliedBy">The address of the user applying the rule</param>
        /// <returns>The application result</returns>
        Task<ApplyWhitelistRuleResponse> ApplyRuleAsync(ApplyWhitelistRuleRequest request, string appliedBy);

        /// <summary>
        /// Deletes a whitelisting rule
        /// </summary>
        /// <param name="request">The delete rule request</param>
        /// <param name="deletedBy">The address of the user deleting the rule</param>
        /// <returns>The delete response</returns>
        Task<DeleteWhitelistRuleResponse> DeleteRuleAsync(DeleteWhitelistRuleRequest request, string deletedBy);

        /// <summary>
        /// Gets audit log entries for rules
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="ruleId">Optional rule ID filter</param>
        /// <param name="actionType">Optional action type filter</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>List of audit log entries</returns>
        Task<WhitelistRuleAuditLogResponse> GetAuditLogsAsync(
            ulong assetId,
            string? ruleId = null,
            RuleAuditActionType? actionType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 50);
    }

    /// <summary>
    /// Response for rule audit logs
    /// </summary>
    public class WhitelistRuleAuditLogResponse : BaseResponse
    {
        /// <summary>
        /// List of audit log entries
        /// </summary>
        public List<WhitelistRuleAuditLog> Entries { get; set; } = new();

        /// <summary>
        /// Total number of entries
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Current page number
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }
    }
}
