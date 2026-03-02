using System.Text.Json.Serialization;

namespace BiatecTokensApi.Models.TokenLaunch
{
    /// <summary>
    /// Request for previewing a token configuration before deployment
    /// </summary>
    public class TokenConfigPreviewRequest
    {
        /// <summary>
        /// Token type (e.g., ASA, ARC3, ARC200, ERC20)
        /// </summary>
        public string TokenType { get; set; } = string.Empty;

        /// <summary>
        /// Target network (e.g., algorand-mainnet, base-mainnet)
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Token name (max 32 characters for ASA)
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Token symbol/unit name (max 8 characters for ASA)
        /// </summary>
        public string? Symbol { get; set; }

        /// <summary>
        /// Total supply (0 for unlimited/mintable)
        /// </summary>
        public ulong TotalSupply { get; set; }

        /// <summary>
        /// Number of decimal places
        /// </summary>
        public int Decimals { get; set; }

        /// <summary>
        /// Token description (metadata)
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// URL for token icon/image
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Whether the token is mintable after initial supply
        /// </summary>
        public bool IsMintable { get; set; }

        /// <summary>
        /// Whether the token supports freezing
        /// </summary>
        public bool IsFreezable { get; set; }

        /// <summary>
        /// Whether the token supports clawback
        /// </summary>
        public bool IsClawbackEnabled { get; set; }

        /// <summary>
        /// Optional correlation ID for tracing
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// Result of a token configuration preview evaluation
    /// </summary>
    public class TokenConfigPreviewResponse
    {
        /// <summary>
        /// Unique preview identifier
        /// </summary>
        public string PreviewId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Overall configuration completeness score (0-100)
        /// </summary>
        public int CompletenessScore { get; set; }

        /// <summary>
        /// Whether the configuration is valid and ready for deployment
        /// </summary>
        public bool IsDeployable { get; set; }

        /// <summary>
        /// Human-readable summary of the configuration status
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Field-level validation issues
        /// </summary>
        public List<TokenConfigFieldIssue> FieldIssues { get; set; } = new();

        /// <summary>
        /// Ordered list of improvements to increase completeness score
        /// </summary>
        public List<TokenConfigImprovement> Improvements { get; set; } = new();

        /// <summary>
        /// Estimated deployment cost breakdown
        /// </summary>
        public TokenDeploymentCostEstimate CostEstimate { get; set; } = new();

        /// <summary>
        /// Competitive signals: how this configuration compares to successful tokens
        /// </summary>
        public TokenCompetitiveSignals CompetitiveSignals { get; set; } = new();

        /// <summary>
        /// When this preview was generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Correlation ID for tracing
        /// </summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>
    /// A field-level validation issue found during configuration preview
    /// </summary>
    public class TokenConfigFieldIssue
    {
        /// <summary>
        /// Field name that has the issue
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Issue severity
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TokenConfigIssueSeverity Severity { get; set; }

        /// <summary>
        /// Human-readable description of the issue
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Suggested fix for this field
        /// </summary>
        public string? SuggestedFix { get; set; }
    }

    /// <summary>
    /// Severity levels for token configuration issues
    /// </summary>
    public enum TokenConfigIssueSeverity
    {
        /// <summary>
        /// Informational suggestion
        /// </summary>
        Info,

        /// <summary>
        /// Recommended improvement
        /// </summary>
        Warning,

        /// <summary>
        /// Blocks deployment
        /// </summary>
        Error
    }

    /// <summary>
    /// An improvement action to increase the token's completeness score
    /// </summary>
    public class TokenConfigImprovement
    {
        /// <summary>
        /// Score points gained by implementing this improvement
        /// </summary>
        public int ScoreGain { get; set; }

        /// <summary>
        /// Short title for this improvement
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of what to add/change
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether this improvement is required for deployment
        /// </summary>
        public bool IsRequired { get; set; }
    }

    /// <summary>
    /// Estimated costs for deploying this token configuration
    /// </summary>
    public class TokenDeploymentCostEstimate
    {
        /// <summary>
        /// Estimated minimum balance requirement in microAlgo (Algorand) or wei (EVM)
        /// </summary>
        public long EstimatedMinBalance { get; set; }

        /// <summary>
        /// Human-readable cost description
        /// </summary>
        public string CostDescription { get; set; } = string.Empty;

        /// <summary>
        /// Currency/unit for the cost (e.g., ALGO, ETH)
        /// </summary>
        public string CostUnit { get; set; } = string.Empty;

        /// <summary>
        /// Whether IPFS storage is required (increases cost)
        /// </summary>
        public bool RequiresIpfsStorage { get; set; }
    }

    /// <summary>
    /// Competitive signals comparing this token configuration to successful tokens
    /// </summary>
    public class TokenCompetitiveSignals
    {
        /// <summary>
        /// How this configuration compares to typical successful tokens (0-100)
        /// </summary>
        public int ConfigurationQualityScore { get; set; }

        /// <summary>
        /// List of trust-enhancing features that are enabled
        /// </summary>
        public List<string> TrustEnhancingFeatures { get; set; } = new();

        /// <summary>
        /// List of trust-enhancing features that are missing
        /// </summary>
        public List<string> MissingTrustFeatures { get; set; } = new();

        /// <summary>
        /// Estimated buyer confidence category
        /// </summary>
        public string BuyerConfidenceCategory { get; set; } = string.Empty;
    }
}
