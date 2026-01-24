namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Response for whitelisting rule operations
    /// </summary>
    public class WhitelistRuleResponse : BaseResponse
    {
        /// <summary>
        /// The rule that was created or modified
        /// </summary>
        public WhitelistRule? Rule { get; set; }
    }

    /// <summary>
    /// Response for listing whitelisting rules
    /// </summary>
    public class WhitelistRulesListResponse : BaseResponse
    {
        /// <summary>
        /// List of whitelisting rules
        /// </summary>
        public List<WhitelistRule> Rules { get; set; } = new();

        /// <summary>
        /// Total number of rules matching the filter
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

    /// <summary>
    /// Response for applying a whitelisting rule
    /// </summary>
    public class ApplyWhitelistRuleResponse : BaseResponse
    {
        /// <summary>
        /// Result of the rule application
        /// </summary>
        public RuleApplicationResult? Result { get; set; }
    }

    /// <summary>
    /// Response for deleting a whitelisting rule
    /// </summary>
    public class DeleteWhitelistRuleResponse : BaseResponse
    {
        /// <summary>
        /// The ID of the deleted rule
        /// </summary>
        public string? RuleId { get; set; }
    }
}
