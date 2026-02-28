namespace BiatecTokensApi.Models.AssetIntelligence
{
    /// <summary>Overall validation status of an asset's metadata.</summary>
    public enum AssetValidationStatus
    {
        Valid,
        PartiallyValid,
        Invalid,
        Unknown
    }

    /// <summary>Source type for asset metadata retrieval.</summary>
    public enum MetadataSourceType
    {
        OnChain,
        IPFS,
        Registry,
        Fallback
    }

    /// <summary>Confidence level of a metadata source.</summary>
    public enum SourceConfidenceLevel
    {
        High,
        Medium,
        Low,
        Unverified
    }

    /// <summary>Schema version of the asset metadata standard.</summary>
    public enum AssetSchemaVersion
    {
        V1,
        V2,
        Unknown
    }

    /// <summary>Error codes for asset intelligence operations.</summary>
    public enum AssetIntelligenceErrorCode
    {
        None,
        UnsupportedAsset,
        MalformedSymbol,
        ChainMismatch,
        StaleSource,
        InconsistentMetadata,
        PolicyViolation,
        ProviderTimeout,
        AllSourcesFailed
    }

    /// <summary>Request model for asset intelligence evaluation.</summary>
    public class AssetIntelligenceRequest
    {
        /// <summary>The on-chain asset identifier.</summary>
        public ulong AssetId { get; set; }

        /// <summary>The blockchain network identifier (e.g., "algorand-mainnet").</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Whether to include provenance information in the response.</summary>
        public bool IncludeProvenance { get; set; }

        /// <summary>Optional caller-supplied correlation ID for audit tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    /// <summary>A single normalized metadata field with source and confidence information.</summary>
    public class AssetMetadataField
    {
        /// <summary>The canonical name of the field.</summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>The field value as a string.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>The source type from which this field was retrieved.</summary>
        public MetadataSourceType Source { get; set; }

        /// <summary>Whether the field value has been validated.</summary>
        public bool IsValidated { get; set; }

        /// <summary>Confidence score between 0 and 1 for this field.</summary>
        public decimal ConfidenceScore { get; set; }
    }

    /// <summary>Provenance information for a metadata source.</summary>
    public class AssetProvenanceInfo
    {
        /// <summary>The type of the metadata source.</summary>
        public MetadataSourceType SourceType { get; set; }

        /// <summary>Stable identifier for the source (e.g., URL, chain node ID).</summary>
        public string SourceIdentifier { get; set; } = string.Empty;

        /// <summary>When this source was last queried.</summary>
        public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Number of seconds before this source's data is considered stale.</summary>
        public int FreshnessWindowSeconds { get; set; }

        /// <summary>Whether the data from this source is currently stale.</summary>
        public bool IsStale { get; set; }
    }

    /// <summary>Validation detail for a single metadata field.</summary>
    public class AssetValidationDetail
    {
        /// <summary>The canonical name of the field that was validated.</summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>Validation outcome for this field.</summary>
        public AssetValidationStatus Status { get; set; }

        /// <summary>Human-readable validation message.</summary>
        public string ValidationMessage { get; set; } = string.Empty;

        /// <summary>Stable issue code when validation fails.</summary>
        public string? IssueCode { get; set; }
    }

    /// <summary>Comprehensive quality indicators for an asset.</summary>
    public class AssetQualityIndicators
    {
        /// <summary>Overall confidence level of the metadata source(s).</summary>
        public SourceConfidenceLevel SourceConfidence { get; set; }

        /// <summary>Freshness window in seconds for the primary source.</summary>
        public int FreshnessWindowSeconds { get; set; }

        /// <summary>Current validation status of the asset's metadata.</summary>
        public AssetValidationStatus ValidationStatus { get; set; }

        /// <summary>Timestamp of the last successful verification, if any.</summary>
        public DateTime? LastVerifiedAt { get; set; }

        /// <summary>Overall quality score between 0 and 100.</summary>
        public decimal OverallScore { get; set; }
    }

    /// <summary>
    /// Canonical asset intelligence response with normalized metadata,
    /// validation details, and provenance information.
    /// </summary>
    public class AssetIntelligenceResponse
    {
        /// <summary>Whether the request succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>The on-chain asset identifier.</summary>
        public ulong AssetId { get; set; }

        /// <summary>The blockchain network identifier.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>Correlation ID for audit tracing.</summary>
        public string CorrelationId { get; set; } = string.Empty;

        /// <summary>Overall validation status of the asset metadata.</summary>
        public AssetValidationStatus ValidationStatus { get; set; }

        /// <summary>Schema version detected for this asset.</summary>
        public AssetSchemaVersion SchemaVersion { get; set; }

        /// <summary>Metadata completeness score between 0 and 100.</summary>
        public decimal CompletenessScore { get; set; }

        /// <summary>List of normalized metadata fields with source and confidence info.</summary>
        public List<AssetMetadataField> NormalizedFields { get; set; } = new();

        /// <summary>Provenance information for each metadata source consulted.</summary>
        public List<AssetProvenanceInfo> Provenance { get; set; } = new();

        /// <summary>Per-field validation details.</summary>
        public List<AssetValidationDetail> ValidationDetails { get; set; } = new();

        /// <summary>Aggregated quality indicators for this asset.</summary>
        public AssetQualityIndicators? QualityIndicators { get; set; }

        /// <summary>Bounded error code; None on success.</summary>
        public AssetIntelligenceErrorCode ErrorCode { get; set; } = AssetIntelligenceErrorCode.None;

        /// <summary>Human-readable error message; null on success.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Actionable remediation hint when an error occurs.</summary>
        public string? RemediationHint { get; set; }

        /// <summary>UTC timestamp when this response was generated.</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
