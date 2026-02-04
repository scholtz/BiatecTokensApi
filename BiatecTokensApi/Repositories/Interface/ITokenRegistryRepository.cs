using BiatecTokensApi.Models.TokenRegistry;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for token registry operations
    /// </summary>
    /// <remarks>
    /// Provides data access methods for the token registry with filtering, pagination, and CRUD operations.
    /// Designed to be implementation-agnostic (in-memory, database, etc.).
    /// </remarks>
    public interface ITokenRegistryRepository
    {
        /// <summary>
        /// Lists tokens in the registry with optional filtering and pagination
        /// </summary>
        /// <param name="request">Filter and pagination parameters</param>
        /// <returns>List of matching token registry entries</returns>
        Task<ListTokenRegistryResponse> ListTokensAsync(ListTokenRegistryRequest request);

        /// <summary>
        /// Gets a single token by its unique identifier
        /// </summary>
        /// <param name="id">Registry ID, token identifier, or symbol</param>
        /// <param name="chain">Optional chain filter for disambiguation</param>
        /// <returns>The token registry entry if found, null otherwise</returns>
        Task<TokenRegistryEntry?> GetTokenByIdAsync(string id, string? chain = null);

        /// <summary>
        /// Gets a token by its blockchain token identifier and chain
        /// </summary>
        /// <param name="tokenIdentifier">The blockchain token identifier (asset ID or contract address)</param>
        /// <param name="chain">The blockchain network</param>
        /// <returns>The token registry entry if found, null otherwise</returns>
        Task<TokenRegistryEntry?> GetTokenByIdentifierAsync(string tokenIdentifier, string chain);

        /// <summary>
        /// Creates or updates a token registry entry
        /// </summary>
        /// <param name="request">The token data to upsert</param>
        /// <param name="createdBy">Address of the user creating/updating the entry</param>
        /// <returns>Response indicating success and whether a new entry was created</returns>
        Task<UpsertTokenRegistryResponse> UpsertTokenAsync(UpsertTokenRegistryRequest request, string? createdBy = null);

        /// <summary>
        /// Deletes a token registry entry
        /// </summary>
        /// <param name="id">The registry ID to delete</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteTokenAsync(string id);

        /// <summary>
        /// Searches for tokens by name or symbol
        /// </summary>
        /// <param name="searchTerm">The search term</param>
        /// <param name="limit">Maximum number of results</param>
        /// <returns>List of matching tokens</returns>
        Task<List<TokenRegistryEntry>> SearchTokensAsync(string searchTerm, int limit = 10);

        /// <summary>
        /// Gets the total count of tokens matching the filter criteria
        /// </summary>
        /// <param name="request">Filter parameters</param>
        /// <returns>Total count of matching tokens</returns>
        Task<int> GetTokenCountAsync(ListTokenRegistryRequest request);
    }
}
