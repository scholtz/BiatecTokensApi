using BiatecTokensApi.Models.Auth;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Interface for user repository operations
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Creates a new user
        /// </summary>
        Task<User> CreateUserAsync(User user);

        /// <summary>
        /// Gets a user by email
        /// </summary>
        Task<User?> GetUserByEmailAsync(string email);

        /// <summary>
        /// Gets a user by ID
        /// </summary>
        Task<User?> GetUserByIdAsync(string userId);

        /// <summary>
        /// Gets a user by Algorand address
        /// </summary>
        Task<User?> GetUserByAlgorandAddressAsync(string algorandAddress);

        /// <summary>
        /// Updates an existing user
        /// </summary>
        Task UpdateUserAsync(User user);

        /// <summary>
        /// Deletes a user
        /// </summary>
        Task DeleteUserAsync(string userId);

        /// <summary>
        /// Checks if a user with the given email exists
        /// </summary>
        Task<bool> UserExistsAsync(string email);

        /// <summary>
        /// Stores a refresh token
        /// </summary>
        Task StoreRefreshTokenAsync(RefreshToken token);

        /// <summary>
        /// Gets a refresh token
        /// </summary>
        Task<RefreshToken?> GetRefreshTokenAsync(string tokenValue);

        /// <summary>
        /// Revokes a refresh token
        /// </summary>
        Task RevokeRefreshTokenAsync(string tokenValue);

        /// <summary>
        /// Gets all refresh tokens for a user
        /// </summary>
        Task<List<RefreshToken>> GetUserRefreshTokensAsync(string userId);

        /// <summary>
        /// Revokes all refresh tokens for a user
        /// </summary>
        Task RevokeAllUserRefreshTokensAsync(string userId);
    }
}
