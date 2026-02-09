using BiatecTokensApi.Models;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for managing and validating token metadata
    /// </summary>
    public interface ITokenMetadataService
    {
        /// <summary>
        /// Retrieves token metadata for a given token identifier and chain
        /// </summary>
        /// <param name="tokenIdentifier">Token identifier (asset ID or contract address)</param>
        /// <param name="chain">Blockchain network identifier</param>
        /// <param name="includeValidation">Whether to include validation details</param>
        /// <returns>Token metadata or null if not found</returns>
        Task<EnrichedTokenMetadata?> GetMetadataAsync(string tokenIdentifier, string chain, bool includeValidation = true);

        /// <summary>
        /// Creates or updates token metadata
        /// </summary>
        /// <param name="metadata">Token metadata to create or update</param>
        /// <returns>Updated metadata with validation results</returns>
        Task<EnrichedTokenMetadata> UpsertMetadataAsync(EnrichedTokenMetadata metadata);

        /// <summary>
        /// Validates token metadata and calculates completeness score
        /// </summary>
        /// <param name="metadata">Metadata to validate</param>
        /// <returns>Validated metadata with issues and completeness score</returns>
        Task<EnrichedTokenMetadata> ValidateMetadataAsync(EnrichedTokenMetadata metadata);

        /// <summary>
        /// Calculates metadata completeness score (0-100)
        /// </summary>
        /// <param name="metadata">Metadata to score</param>
        /// <returns>Completeness score from 0 to 100</returns>
        int CalculateCompletenessScore(EnrichedTokenMetadata metadata);

        /// <summary>
        /// Generates explorer URL for a token based on chain and identifier
        /// </summary>
        /// <param name="tokenIdentifier">Token identifier</param>
        /// <param name="chain">Blockchain network identifier</param>
        /// <returns>Block explorer URL for the token</returns>
        string? GenerateExplorerUrl(string tokenIdentifier, string chain);

        /// <summary>
        /// Enriches metadata with additional information from external sources
        /// </summary>
        /// <param name="metadata">Base metadata to enrich</param>
        /// <returns>Enriched metadata</returns>
        Task<EnrichedTokenMetadata> EnrichMetadataAsync(EnrichedTokenMetadata metadata);

        /// <summary>
        /// Applies fallback values for missing metadata fields
        /// </summary>
        /// <param name="metadata">Metadata to apply fallbacks to</param>
        /// <returns>Metadata with fallback values applied</returns>
        EnrichedTokenMetadata ApplyFallbacks(EnrichedTokenMetadata metadata);
    }
}
