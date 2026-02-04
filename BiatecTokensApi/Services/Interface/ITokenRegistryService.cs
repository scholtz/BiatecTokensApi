using BiatecTokensApi.Models.TokenRegistry;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for token registry operations
    /// </summary>
    /// <remarks>
    /// Provides business logic for token registry management, including validation,
    /// normalization, and integration with external data sources.
    /// </remarks>
    public interface ITokenRegistryService
    {
        /// <summary>
        /// Lists tokens in the registry with filtering and pagination
        /// </summary>
        /// <param name="request">Filter and pagination parameters</param>
        /// <returns>Paginated list of tokens</returns>
        Task<ListTokenRegistryResponse> ListTokensAsync(ListTokenRegistryRequest request);

        /// <summary>
        /// Gets detailed information for a single token
        /// </summary>
        /// <param name="identifier">Token identifier (ID, asset ID, contract address, or symbol)</param>
        /// <param name="chain">Optional chain filter</param>
        /// <returns>Token details or error response</returns>
        Task<GetTokenRegistryResponse> GetTokenAsync(string identifier, string? chain = null);

        /// <summary>
        /// Creates or updates a token in the registry
        /// </summary>
        /// <param name="request">Token data</param>
        /// <param name="createdBy">User address</param>
        /// <returns>Upsert result</returns>
        Task<UpsertTokenRegistryResponse> UpsertTokenAsync(UpsertTokenRegistryRequest request, string? createdBy = null);

        /// <summary>
        /// Searches for tokens by name or symbol
        /// </summary>
        /// <param name="searchTerm">Search term</param>
        /// <param name="limit">Maximum results</param>
        /// <returns>List of matching tokens</returns>
        Task<List<TokenRegistryEntry>> SearchTokensAsync(string searchTerm, int limit = 10);

        /// <summary>
        /// Validates a token registry entry
        /// </summary>
        /// <param name="entry">The entry to validate</param>
        /// <returns>Validation result with any errors or warnings</returns>
        Task<RegistryValidationResult> ValidateTokenAsync(TokenRegistryEntry entry);
    }

    /// <summary>
    /// Service interface for ingesting token data from external sources
    /// </summary>
    /// <remarks>
    /// Handles data normalization, validation, and idempotent processing of token data
    /// from internal and external registry sources.
    /// </remarks>
    public interface IRegistryIngestionService
    {
        /// <summary>
        /// Ingests token data from specified sources
        /// </summary>
        /// <param name="request">Ingestion parameters</param>
        /// <returns>Ingestion result with statistics and anomalies</returns>
        Task<IngestRegistryResponse> IngestAsync(IngestRegistryRequest request);

        /// <summary>
        /// Ingests tokens from internal token deployment records
        /// </summary>
        /// <param name="chain">Optional chain filter</param>
        /// <param name="limit">Maximum number of tokens to ingest</param>
        /// <returns>Number of tokens ingested</returns>
        Task<int> IngestInternalTokensAsync(string? chain = null, int? limit = null);

        /// <summary>
        /// Normalizes token data from an external source
        /// </summary>
        /// <param name="rawData">Raw token data</param>
        /// <param name="source">Data source identifier</param>
        /// <returns>Normalized token registry entry</returns>
        Task<TokenRegistryEntry?> NormalizeTokenDataAsync(object rawData, string source);

        /// <summary>
        /// Validates and logs anomalies in token data
        /// </summary>
        /// <param name="entry">Token entry to validate</param>
        /// <returns>List of anomalies found</returns>
        Task<List<string>> ValidateAndLogAnomaliesAsync(TokenRegistryEntry entry);
    }

    /// <summary>
    /// Validation result for token registry entries
    /// </summary>
    public class RegistryValidationResult
    {
        /// <summary>
        /// Whether the entry is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Validation errors (critical issues)
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Validation warnings (non-critical issues)
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Informational messages
        /// </summary>
        public List<string> Info { get; set; } = new();
    }
}
