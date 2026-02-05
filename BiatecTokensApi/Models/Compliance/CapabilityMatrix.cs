namespace BiatecTokensApi.Models.Compliance
{
    /// <summary>
    /// Represents the complete capability matrix for compliance rules
    /// </summary>
    public class CapabilityMatrix
    {
        /// <summary>
        /// Version of the capability matrix (e.g., "2026-02-05")
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the matrix was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// List of jurisdiction-specific capabilities
        /// </summary>
        public List<JurisdictionCapability> Jurisdictions { get; set; } = new();
    }

    /// <summary>
    /// Represents capabilities for a specific jurisdiction
    /// </summary>
    public class JurisdictionCapability
    {
        /// <summary>
        /// ISO country code (e.g., "US", "CH", "EU")
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable jurisdiction name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// List of wallet type capabilities for this jurisdiction
        /// </summary>
        public List<WalletTypeCapability> WalletTypes { get; set; } = new();
    }

    /// <summary>
    /// Represents capabilities for a specific wallet type
    /// </summary>
    public class WalletTypeCapability
    {
        /// <summary>
        /// Wallet type (e.g., "custodial", "non-custodial", "hardware")
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable wallet type description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// List of KYC tier capabilities for this wallet type
        /// </summary>
        public List<KycTierCapability> KycTiers { get; set; } = new();
    }

    /// <summary>
    /// Represents capabilities for a specific KYC tier
    /// </summary>
    public class KycTierCapability
    {
        /// <summary>
        /// KYC tier level (e.g., "0", "1", "2", "3")
        /// </summary>
        public string Tier { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable tier description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// List of token standard capabilities for this KYC tier
        /// </summary>
        public List<TokenStandardCapability> TokenStandards { get; set; } = new();
    }

    /// <summary>
    /// Represents capabilities for a specific token standard
    /// </summary>
    public class TokenStandardCapability
    {
        /// <summary>
        /// Token standard name (e.g., "ARC-3", "ARC-19", "ARC-200", "ERC-20")
        /// </summary>
        public string Standard { get; set; } = string.Empty;

        /// <summary>
        /// List of allowed actions for this token standard
        /// </summary>
        public List<string> Actions { get; set; } = new();

        /// <summary>
        /// List of required compliance checks
        /// </summary>
        public List<string> Checks { get; set; } = new();

        /// <summary>
        /// Optional regulatory notes or restrictions
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Request model for querying capability matrix
    /// </summary>
    public class GetCapabilityMatrixRequest
    {
        /// <summary>
        /// Optional filter by jurisdiction code
        /// </summary>
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Optional filter by wallet type
        /// </summary>
        public string? WalletType { get; set; }

        /// <summary>
        /// Optional filter by token standard
        /// </summary>
        public string? TokenStandard { get; set; }

        /// <summary>
        /// Optional filter by KYC tier
        /// </summary>
        public string? KycTier { get; set; }
    }

    /// <summary>
    /// Response model for capability matrix queries
    /// </summary>
    public class CapabilityMatrixResponse
    {
        /// <summary>
        /// Indicates if the request was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The capability matrix data (may be filtered based on request)
        /// </summary>
        public CapabilityMatrix? Data { get; set; }

        /// <summary>
        /// Error message if the request failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Detailed error information for unsupported combinations
        /// </summary>
        public CapabilityErrorDetails? ErrorDetails { get; set; }
    }

    /// <summary>
    /// Detailed error information for capability violations
    /// </summary>
    public class CapabilityErrorDetails
    {
        /// <summary>
        /// Error code (e.g., "capability_not_allowed", "invalid_filter")
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Jurisdiction code that was requested
        /// </summary>
        public string? Jurisdiction { get; set; }

        /// <summary>
        /// Wallet type that was requested
        /// </summary>
        public string? WalletType { get; set; }

        /// <summary>
        /// Token standard that was requested
        /// </summary>
        public string? TokenStandard { get; set; }

        /// <summary>
        /// KYC tier that was requested
        /// </summary>
        public string? KycTier { get; set; }

        /// <summary>
        /// Action that was attempted
        /// </summary>
        public string? Action { get; set; }

        /// <summary>
        /// ID of the rule that blocked the action
        /// </summary>
        public string? RuleId { get; set; }
    }

    /// <summary>
    /// Model for capability enforcement check
    /// </summary>
    public class CapabilityCheckRequest
    {
        /// <summary>
        /// Jurisdiction code
        /// </summary>
        public string Jurisdiction { get; set; } = string.Empty;

        /// <summary>
        /// Wallet type
        /// </summary>
        public string WalletType { get; set; } = string.Empty;

        /// <summary>
        /// Token standard
        /// </summary>
        public string TokenStandard { get; set; } = string.Empty;

        /// <summary>
        /// KYC tier
        /// </summary>
        public string KycTier { get; set; } = string.Empty;

        /// <summary>
        /// Action to check (e.g., "mint", "transfer", "burn", "freeze")
        /// </summary>
        public string Action { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for capability enforcement check
    /// </summary>
    public class CapabilityCheckResponse
    {
        /// <summary>
        /// Indicates if the action is allowed
        /// </summary>
        public bool Allowed { get; set; }

        /// <summary>
        /// Reason if the action is not allowed
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Required compliance checks for this action
        /// </summary>
        public List<string>? RequiredChecks { get; set; }

        /// <summary>
        /// Regulatory notes if applicable
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Error details if the check failed
        /// </summary>
        public CapabilityErrorDetails? ErrorDetails { get; set; }
    }
}
