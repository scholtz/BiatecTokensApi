namespace BiatecTokensApi.Models.TokenOperationsIntelligence
{
    /// <summary>
    /// Request for consolidated token operations intelligence
    /// </summary>
    public class TokenOperationsIntelligenceRequest
    {
        /// <summary>
        /// Asset ID of the token to evaluate
        /// </summary>
        public ulong AssetId { get; set; }

        /// <summary>
        /// Network identifier (e.g., voimain-v1.0, mainnet-v1.0)
        /// </summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Optional client-provided idempotency/correlation key
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Maximum number of events to include in the response (1-50, default 10)
        /// </summary>
        public int MaxEvents { get; set; } = 10;

        /// <summary>
        /// Whether to include full event detail or summaries only
        /// </summary>
        public bool IncludeEventDetails { get; set; } = false;

        /// <summary>
        /// Optional list of policy dimensions to evaluate. If null or empty, all dimensions are evaluated.
        /// Valid values: MintAuthority, MetadataCompleteness, TreasuryMovement, OwnershipConsistency
        /// </summary>
        public List<string>? PolicyDimensions { get; set; }

        /// <summary>
        /// Optional token state inputs that drive deterministic evaluator outcomes.
        /// When not provided, evaluators use conservative (Warning) defaults.
        /// </summary>
        public TokenStateInputs? StateInputs { get; set; }
    }
}
