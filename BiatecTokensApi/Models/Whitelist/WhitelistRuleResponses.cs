namespace BiatecTokensApi.Models.Whitelist
{
    /// <summary>
    /// Response for whitelist rule operations
    /// </summary>
    public class WhitelistRuleResponse : BaseResponse
    {
        /// <summary>
        /// The whitelist rule that was created or modified
        /// </summary>
        public WhitelistRule? Rule { get; set; }
    }

    /// <summary>
    /// Response for listing whitelist rules
    /// </summary>
    public class WhitelistRulesListResponse : BaseResponse
    {
        /// <summary>
        /// List of whitelist rules
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
    /// Response for applying a rule
    /// </summary>
    public class ApplyRuleResponse : BaseResponse
    {
        /// <summary>
        /// Number of whitelist entries evaluated
        /// </summary>
        public int EntriesEvaluated { get; set; }

        /// <summary>
        /// Number of entries that passed validation
        /// </summary>
        public int EntriesPassed { get; set; }

        /// <summary>
        /// Number of entries that failed validation
        /// </summary>
        public int EntriesFailed { get; set; }

        /// <summary>
        /// List of addresses that failed validation
        /// </summary>
        public List<string> FailedAddresses { get; set; } = new();

        /// <summary>
        /// List of validation errors
        /// </summary>
        public List<RuleValidationError> ValidationErrors { get; set; } = new();
    }

    /// <summary>
    /// Response for validating entries against rules
    /// </summary>
    public class ValidateAgainstRulesResponse : BaseResponse
    {
        /// <summary>
        /// Whether all validations passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Number of rules evaluated
        /// </summary>
        public int RulesEvaluated { get; set; }

        /// <summary>
        /// Number of rules passed
        /// </summary>
        public int RulesPassed { get; set; }

        /// <summary>
        /// Number of rules failed
        /// </summary>
        public int RulesFailed { get; set; }

        /// <summary>
        /// List of validation errors
        /// </summary>
        public List<RuleValidationError> ValidationErrors { get; set; } = new();
    }

    /// <summary>
    /// Represents a validation error from a rule
    /// </summary>
    public class RuleValidationError
    {
        /// <summary>
        /// The rule ID that generated this error
        /// </summary>
        public string RuleId { get; set; } = string.Empty;

        /// <summary>
        /// The rule name
        /// </summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>
        /// The address that failed validation (if applicable)
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// The error message
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// The field that failed validation
        /// </summary>
        public string? FieldName { get; set; }
    }

    /// <summary>
    /// Audit log entry for rule changes
    /// </summary>
    public class WhitelistRuleAuditLog
    {
        /// <summary>
        /// Unique identifier for the audit log entry
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The rule ID
        /// </summary>
        public string RuleId { get; set; } = string.Empty;

        /// <summary>
        /// The asset ID
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Action performed (Create, Update, Delete, Apply)
        /// </summary>
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// The address of the user who performed the action
        /// </summary>
        public string PerformedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the action was performed
        /// </summary>
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Old rule state (for updates)
        /// </summary>
        public string? OldState { get; set; }

        /// <summary>
        /// New rule state (for updates)
        /// </summary>
        public string? NewState { get; set; }

        /// <summary>
        /// Additional notes about the action
        /// </summary>
        public string? Notes { get; set; }
    }
}
