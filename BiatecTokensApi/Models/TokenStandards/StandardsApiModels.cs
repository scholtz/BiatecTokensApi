namespace BiatecTokensApi.Models.TokenStandards
{
    /// <summary>
    /// Request for standards discovery endpoint
    /// </summary>
    public class GetTokenStandardsRequest
    {
        /// <summary>
        /// Optional filter to only return active standards
        /// </summary>
        public bool? ActiveOnly { get; set; }

        /// <summary>
        /// Optional filter by specific standard
        /// </summary>
        public TokenStandard? Standard { get; set; }
    }

    /// <summary>
    /// Response containing list of supported token standards
    /// </summary>
    public class GetTokenStandardsResponse
    {
        /// <summary>
        /// List of available token standard profiles
        /// </summary>
        public List<TokenStandardProfile> Standards { get; set; } = new();

        /// <summary>
        /// Total number of standards available
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Request to validate token metadata against a standard
    /// </summary>
    public class ValidateTokenMetadataRequest
    {
        /// <summary>
        /// Token standard to validate against
        /// </summary>
        public TokenStandard Standard { get; set; } = TokenStandard.Baseline;

        /// <summary>
        /// Token metadata as JSON object
        /// </summary>
        public object? Metadata { get; set; }

        /// <summary>
        /// Token type (optional, for context)
        /// </summary>
        public string? TokenType { get; set; }

        /// <summary>
        /// Token name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Token symbol
        /// </summary>
        public string? Symbol { get; set; }

        /// <summary>
        /// Number of decimals
        /// </summary>
        public int? Decimals { get; set; }

        /// <summary>
        /// Total supply
        /// </summary>
        public string? TotalSupply { get; set; }
    }

    /// <summary>
    /// Response from metadata validation
    /// </summary>
    public class ValidationResponse
    {
        /// <summary>
        /// Whether validation passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Validation result details
        /// </summary>
        public TokenValidationResult? ValidationResult { get; set; }

        /// <summary>
        /// Error code if validation failed
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Human-readable message
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Correlation ID for tracking
        /// </summary>
        public string? CorrelationId { get; set; }
    }
}
