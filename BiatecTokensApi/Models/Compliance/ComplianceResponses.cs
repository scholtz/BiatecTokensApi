namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Response for compliance metadata operations
    /// </summary>
    public class ComplianceMetadataResponse : BaseResponse
    {
        /// <summary>
        /// The compliance metadata that was created, retrieved, or modified
        /// </summary>
        public ComplianceMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Response for listing compliance metadata
    /// </summary>
    public class ComplianceMetadataListResponse : BaseResponse
    {
        /// <summary>
        /// List of compliance metadata entries
        /// </summary>
        public List<ComplianceMetadata> Metadata { get; set; } = new();

        /// <summary>
        /// Total number of entries matching the filter
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
    /// Response for token preset validation
    /// </summary>
    public class ValidateTokenPresetResponse : BaseResponse
    {
        /// <summary>
        /// Whether the token configuration is valid for MICA/RWA compliance
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// List of validation errors that must be fixed
        /// </summary>
        public List<ValidationIssue> Errors { get; set; } = new();

        /// <summary>
        /// List of validation warnings that should be reviewed
        /// </summary>
        public List<ValidationIssue> Warnings { get; set; } = new();

        /// <summary>
        /// Summary of validation results
        /// </summary>
        public string? Summary { get; set; }
    }

    /// <summary>
    /// Represents a validation issue (error or warning)
    /// </summary>
    public class ValidationIssue
    {
        /// <summary>
        /// Severity of the issue
        /// </summary>
        public ValidationSeverity Severity { get; set; }

        /// <summary>
        /// Field or area that has the issue
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// Description of the issue
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Suggested action to resolve the issue
        /// </summary>
        public string? Recommendation { get; set; }

        /// <summary>
        /// Applicable regulatory framework or standard
        /// </summary>
        public string? RegulatoryContext { get; set; }
    }

    /// <summary>
    /// Severity level for validation issues
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// Critical error that must be fixed
        /// </summary>
        Error,

        /// <summary>
        /// Warning that should be reviewed
        /// </summary>
        Warning,

        /// <summary>
        /// Informational message
        /// </summary>
        Info
    }
}
