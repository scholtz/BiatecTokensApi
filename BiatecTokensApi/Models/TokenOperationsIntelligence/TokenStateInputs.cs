namespace BiatecTokensApi.Models.TokenOperationsIntelligence
{
    /// <summary>
    /// State inputs that drive deterministic policy evaluator outcomes.
    /// When not provided, evaluators use conservative (Warning) defaults.
    /// </summary>
    public class TokenStateInputs
    {
        /// <summary>
        /// Whether mint authority has been explicitly revoked (null = unknown)
        /// </summary>
        public bool? MintAuthorityRevoked { get; set; }

        /// <summary>
        /// Whether the mint supply cap has been reached (null = unknown)
        /// </summary>
        public bool? SupplyCapReached { get; set; }

        /// <summary>
        /// Metadata completeness percentage (0-100). null = unknown.
        /// </summary>
        public double? MetadataCompletenessPercent { get; set; }

        /// <summary>
        /// Whether the metadata URL is accessible and valid (null = unknown)
        /// </summary>
        public bool? MetadataUrlAccessible { get; set; }

        /// <summary>
        /// Whether a large treasury movement has been detected (null = unknown)
        /// </summary>
        public bool? LargeTreasuryMovementDetected { get; set; }

        /// <summary>
        /// Largest single treasury movement as percentage of total supply (null = unknown)
        /// </summary>
        public double? LargestMovementPercent { get; set; }

        /// <summary>
        /// Whether on-chain ownership records match deployment records (null = unknown)
        /// </summary>
        public bool? OwnershipRecordsMatch { get; set; }

        /// <summary>
        /// Whether multiple unauthorized manager changes have been detected (null = unknown)
        /// </summary>
        public bool? UnauthorizedManagerChangesDetected { get; set; }
    }
}
