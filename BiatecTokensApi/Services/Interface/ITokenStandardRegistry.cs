using BiatecTokensApi.Models.TokenStandards;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service for managing and retrieving token standard profiles
    /// </summary>
    public interface ITokenStandardRegistry
    {
        /// <summary>
        /// Gets all available token standard profiles
        /// </summary>
        /// <param name="activeOnly">If true, only returns active profiles</param>
        /// <returns>List of token standard profiles</returns>
        Task<List<TokenStandardProfile>> GetAllStandardsAsync(bool activeOnly = true);

        /// <summary>
        /// Gets a specific token standard profile by standard type
        /// </summary>
        /// <param name="standard">The token standard to retrieve</param>
        /// <returns>Token standard profile or null if not found</returns>
        Task<TokenStandardProfile?> GetStandardProfileAsync(TokenStandard standard);

        /// <summary>
        /// Gets the default token standard for backward compatibility
        /// </summary>
        /// <returns>Default token standard profile</returns>
        Task<TokenStandardProfile> GetDefaultStandardAsync();

        /// <summary>
        /// Checks if a token standard is supported
        /// </summary>
        /// <param name="standard">The token standard to check</param>
        /// <returns>True if supported, false otherwise</returns>
        Task<bool> IsStandardSupportedAsync(TokenStandard standard);
    }
}
