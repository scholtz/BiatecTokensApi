using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.TokenRegistry
{
    /// <summary>
    /// Request model for listing tokens in the registry
    /// </summary>
    /// <remarks>
    /// Supports filtering by multiple criteria and pagination.
    /// Example: GET /api/v1/registry/tokens?standard=ARC3&amp;complianceStatus=Compliant&amp;page=1&amp;pageSize=20
    /// </remarks>
    public class ListTokenRegistryRequest
    {
        /// <summary>
        /// Filter by token standard (e.g., "ASA", "ARC3", "ERC20")
        /// </summary>
        public string? Standard { get; set; }

        /// <summary>
        /// Filter by compliance status
        /// </summary>
        public ComplianceState? ComplianceStatus { get; set; }

        /// <summary>
        /// Filter by blockchain network
        /// </summary>
        public string? Chain { get; set; }

        /// <summary>
        /// Filter by issuer address
        /// </summary>
        public string? IssuerAddress { get; set; }

        /// <summary>
        /// Filter by contract verification status
        /// </summary>
        public bool? IsContractVerified { get; set; }

        /// <summary>
        /// Filter by audit status
        /// </summary>
        public bool? IsAudited { get; set; }

        /// <summary>
        /// Filter by whether the token has valid metadata
        /// </summary>
        public bool? HasValidMetadata { get; set; }

        /// <summary>
        /// Search by token name or symbol
        /// </summary>
        public string? Search { get; set; }

        /// <summary>
        /// Filter by tags (comma-separated)
        /// </summary>
        public string? Tags { get; set; }

        /// <summary>
        /// Filter by data source
        /// </summary>
        public string? DataSource { get; set; }

        /// <summary>
        /// Page number for pagination (1-based)
        /// </summary>
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size for pagination
        /// </summary>
        [Range(1, 100)]
        public int PageSize { get; set; } = 20;

        /// <summary>
        /// Sort field (e.g., "name", "createdAt", "updatedAt", "complianceScore")
        /// </summary>
        public string SortBy { get; set; } = "createdAt";

        /// <summary>
        /// Sort direction ("asc" or "desc")
        /// </summary>
        public string SortDirection { get; set; } = "desc";
    }

    /// <summary>
    /// Response model for listing tokens in the registry
    /// </summary>
    /// <remarks>
    /// Returns paginated results with metadata about total count and pages.
    /// Example response structure:
    /// {
    ///   "tokens": [...],
    ///   "totalCount": 150,
    ///   "page": 1,
    ///   "pageSize": 20,
    ///   "totalPages": 8,
    ///   "hasNextPage": true,
    ///   "hasPreviousPage": false
    /// }
    /// </remarks>
    public class ListTokenRegistryResponse
    {
        /// <summary>
        /// List of token registry entries matching the filter criteria
        /// </summary>
        public List<TokenRegistryEntry> Tokens { get; set; } = new();

        /// <summary>
        /// Total number of tokens matching the filter criteria
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

        /// <summary>
        /// Whether there is a next page available
        /// </summary>
        public bool HasNextPage { get; set; }

        /// <summary>
        /// Whether there is a previous page available
        /// </summary>
        public bool HasPreviousPage { get; set; }
    }

    /// <summary>
    /// Request model for retrieving a single token by identifier
    /// </summary>
    /// <remarks>
    /// Used with GET /api/v1/registry/tokens/{identifier}
    /// The identifier can be the registry ID, token identifier (asset ID/contract address), or token symbol
    /// </remarks>
    public class GetTokenRegistryRequest
    {
        /// <summary>
        /// Token identifier (registry ID, asset ID, contract address, or symbol)
        /// </summary>
        [Required]
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// Optional chain filter to disambiguate symbols that exist on multiple chains
        /// </summary>
        public string? Chain { get; set; }
    }

    /// <summary>
    /// Response model for retrieving a single token
    /// </summary>
    /// <remarks>
    /// Returns complete token details including all compliance metadata and operational readiness fields.
    /// Example response structure includes all fields from TokenRegistryEntry with explicit null handling.
    /// </remarks>
    public class GetTokenRegistryResponse
    {
        /// <summary>
        /// The token registry entry, or null if not found
        /// </summary>
        public TokenRegistryEntry? Token { get; set; }

        /// <summary>
        /// Whether the token was found
        /// </summary>
        public bool Found { get; set; }

        /// <summary>
        /// Error message if the token was not found or an error occurred
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Request model for creating or updating a registry entry
    /// </summary>
    /// <remarks>
    /// Used by the ingestion pipeline to add or update tokens in the registry.
    /// Supports idempotent upsert operations based on token identifier and chain.
    /// </remarks>
    public class UpsertTokenRegistryRequest
    {
        /// <summary>
        /// Token identifier on the blockchain
        /// </summary>
        [Required]
        public string TokenIdentifier { get; set; } = string.Empty;

        /// <summary>
        /// Blockchain network identifier
        /// </summary>
        [Required]
        public string Chain { get; set; } = string.Empty;

        /// <summary>
        /// Token name
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Token symbol
        /// </summary>
        [Required]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Number of decimal places
        /// </summary>
        public int Decimals { get; set; }

        /// <summary>
        /// Total supply
        /// </summary>
        public string? TotalSupply { get; set; }

        /// <summary>
        /// Supported standards
        /// </summary>
        public List<string>? SupportedStandards { get; set; }

        /// <summary>
        /// Primary standard
        /// </summary>
        public string? PrimaryStandard { get; set; }

        /// <summary>
        /// Issuer identity
        /// </summary>
        public IssuerIdentity? Issuer { get; set; }

        /// <summary>
        /// Compliance scoring
        /// </summary>
        public ComplianceScoring? Compliance { get; set; }

        /// <summary>
        /// Operational readiness
        /// </summary>
        public OperationalReadiness? Readiness { get; set; }

        /// <summary>
        /// Token description
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Token website
        /// </summary>
        public string? Website { get; set; }

        /// <summary>
        /// Token logo URL
        /// </summary>
        public string? LogoUrl { get; set; }

        /// <summary>
        /// Data source
        /// </summary>
        public string DataSource { get; set; } = "internal";

        /// <summary>
        /// External registries
        /// </summary>
        public List<string>? ExternalRegistries { get; set; }

        /// <summary>
        /// Tags
        /// </summary>
        public List<string>? Tags { get; set; }

        /// <summary>
        /// Deployment date
        /// </summary>
        public DateTime? DeployedAt { get; set; }
    }

    /// <summary>
    /// Response model for upsert operations
    /// </summary>
    public class UpsertTokenRegistryResponse
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The registry entry ID (new or existing)
        /// </summary>
        public string? RegistryId { get; set; }

        /// <summary>
        /// Whether a new entry was created (true) or an existing entry was updated (false)
        /// </summary>
        public bool Created { get; set; }

        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// The complete registry entry after the operation
        /// </summary>
        public TokenRegistryEntry? Token { get; set; }
    }

    /// <summary>
    /// Request model for triggering registry ingestion
    /// </summary>
    /// <remarks>
    /// Used with POST /api/v1/registry/ingest to manually trigger ingestion from external sources.
    /// Supports selective ingestion by source and network.
    /// </remarks>
    public class IngestRegistryRequest
    {
        /// <summary>
        /// Source to ingest from (e.g., "internal", "vestige", "all")
        /// </summary>
        public string Source { get; set; } = "all";

        /// <summary>
        /// Optional chain filter for ingestion
        /// </summary>
        public string? Chain { get; set; }

        /// <summary>
        /// Whether to force re-ingestion of existing entries
        /// </summary>
        public bool Force { get; set; }

        /// <summary>
        /// Maximum number of entries to ingest (for testing/debugging)
        /// </summary>
        [Range(1, 1000)]
        public int? Limit { get; set; }
    }

    /// <summary>
    /// Response model for ingestion operations
    /// </summary>
    public class IngestRegistryResponse
    {
        /// <summary>
        /// Whether the ingestion was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of entries processed
        /// </summary>
        public int ProcessedCount { get; set; }

        /// <summary>
        /// Number of new entries created
        /// </summary>
        public int CreatedCount { get; set; }

        /// <summary>
        /// Number of existing entries updated
        /// </summary>
        public int UpdatedCount { get; set; }

        /// <summary>
        /// Number of entries skipped due to validation failures
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// Number of errors encountered
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Anomalies or warnings encountered during ingestion
        /// </summary>
        public List<string> Anomalies { get; set; } = new();

        /// <summary>
        /// Error messages if any
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Duration of the ingestion process
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Timestamp when ingestion started
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Timestamp when ingestion completed
        /// </summary>
        public DateTime CompletedAt { get; set; }
    }
}
