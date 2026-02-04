namespace BiatecTokensApi.Models.TokenStandards
{
    /// <summary>
    /// Result of token metadata validation against a standard profile
    /// </summary>
    public class TokenValidationResult
    {
        /// <summary>
        /// Whether the validation passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Token standard that was validated against
        /// </summary>
        public TokenStandard Standard { get; set; }

        /// <summary>
        /// Version of the standard profile used for validation
        /// </summary>
        public string StandardVersion { get; set; } = string.Empty;

        /// <summary>
        /// List of validation errors encountered
        /// </summary>
        public List<ValidationError> Errors { get; set; } = new();

        /// <summary>
        /// List of validation warnings
        /// </summary>
        public List<ValidationError> Warnings { get; set; } = new();

        /// <summary>
        /// Timestamp when validation was performed
        /// </summary>
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Summary message describing validation status
        /// </summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// Individual validation error or warning
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// Error code for programmatic handling
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Field name that failed validation
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable error message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Severity level
        /// </summary>
        public TokenValidationSeverity Severity { get; set; } = TokenValidationSeverity.Error;

        /// <summary>
        /// Additional context about the error
        /// </summary>
        public string? Details { get; set; }
    }

    /// <summary>
    /// Token metadata with validation status
    /// </summary>
    public class ValidatedTokenMetadata
    {
        /// <summary>
        /// Selected token standard profile
        /// </summary>
        public TokenStandard Standard { get; set; } = TokenStandard.Baseline;

        /// <summary>
        /// Version of the standard profile
        /// </summary>
        public string StandardVersion { get; set; } = "1.0.0";

        /// <summary>
        /// Current validation status
        /// </summary>
        public ValidationStatus Status { get; set; } = ValidationStatus.NotValidated;

        /// <summary>
        /// Last validation timestamp
        /// </summary>
        public DateTime? LastValidatedAt { get; set; }

        /// <summary>
        /// Validation result message
        /// </summary>
        public string? ValidationMessage { get; set; }

        /// <summary>
        /// Correlation ID for tracking validation through logs
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Validation status enumeration
    /// </summary>
    public enum ValidationStatus
    {
        /// <summary>
        /// Not yet validated
        /// </summary>
        NotValidated,

        /// <summary>
        /// Validation in progress
        /// </summary>
        Validating,

        /// <summary>
        /// Validation passed successfully
        /// </summary>
        Valid,

        /// <summary>
        /// Validation failed with errors
        /// </summary>
        Invalid,

        /// <summary>
        /// Validation passed with warnings
        /// </summary>
        ValidWithWarnings
    }
}
