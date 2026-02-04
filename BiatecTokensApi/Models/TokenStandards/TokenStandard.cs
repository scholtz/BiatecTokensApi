namespace BiatecTokensApi.Models.TokenStandards
{
    /// <summary>
    /// Enumeration of supported token standards for validation and compliance
    /// </summary>
    public enum TokenStandard
    {
        /// <summary>
        /// Baseline standard with minimal validation requirements
        /// </summary>
        Baseline,

        /// <summary>
        /// ARC-3 standard for Algorand tokens with rich metadata
        /// </summary>
        ARC3,

        /// <summary>
        /// ARC-19 standard for Algorand tokens with on-chain metadata
        /// </summary>
        ARC19,

        /// <summary>
        /// ARC-69 standard for Algorand tokens with simplified metadata
        /// </summary>
        ARC69,

        /// <summary>
        /// ERC-20 standard for Ethereum-compatible tokens
        /// </summary>
        ERC20
    }

    /// <summary>
    /// Token standard profile defining validation rules and requirements
    /// </summary>
    public class TokenStandardProfile
    {
        /// <summary>
        /// Unique identifier for the standard profile
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the standard
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Version of the standard profile
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Human-readable description of the standard
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Token standard enumeration value
        /// </summary>
        public TokenStandard Standard { get; set; }

        /// <summary>
        /// List of required metadata fields for this standard
        /// </summary>
        public List<StandardFieldDefinition> RequiredFields { get; set; } = new();

        /// <summary>
        /// List of optional metadata fields for this standard
        /// </summary>
        public List<StandardFieldDefinition> OptionalFields { get; set; } = new();

        /// <summary>
        /// Validation rules for this standard
        /// </summary>
        public List<ValidationRule> ValidationRules { get; set; } = new();

        /// <summary>
        /// Whether this profile is currently active and available for use
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Example metadata JSON for this standard
        /// </summary>
        public string? ExampleJson { get; set; }

        /// <summary>
        /// External reference URL for standard specification
        /// </summary>
        public string? SpecificationUrl { get; set; }
    }

    /// <summary>
    /// Definition of a metadata field for a token standard
    /// </summary>
    public class StandardFieldDefinition
    {
        /// <summary>
        /// Field name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Expected data type (string, number, boolean, object, array)
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of the field
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Default value if not provided (for optional fields)
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// Regular expression pattern for validation (for string types)
        /// </summary>
        public string? ValidationPattern { get; set; }

        /// <summary>
        /// Minimum value (for numeric types)
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Maximum value (for numeric types)
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Maximum length (for string types)
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Whether this field is required
        /// </summary>
        public bool IsRequired { get; set; }
    }

    /// <summary>
    /// Validation rule for token standard compliance
    /// </summary>
    public class ValidationRule
    {
        /// <summary>
        /// Unique identifier for the rule
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable name of the rule
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of what this rule validates
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Error message to display when validation fails
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Error code for programmatic handling
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Severity level of the validation rule
        /// </summary>
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
    }

    /// <summary>
    /// Severity levels for validation rules
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// Informational message, does not prevent token creation
        /// </summary>
        Info,

        /// <summary>
        /// Warning message, may indicate potential issues
        /// </summary>
        Warning,

        /// <summary>
        /// Error that must be fixed before token creation
        /// </summary>
        Error
    }
}
