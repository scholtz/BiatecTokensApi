using BiatecTokensApi.Models.ComplianceProfile;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Interface for compliance profile repository operations
    /// </summary>
    public interface IComplianceProfileRepository
    {
        /// <summary>
        /// Creates a new compliance profile
        /// </summary>
        Task<ComplianceProfile> CreateProfileAsync(ComplianceProfile profile);

        /// <summary>
        /// Gets a compliance profile by user ID
        /// </summary>
        Task<ComplianceProfile?> GetProfileByUserIdAsync(string userId);

        /// <summary>
        /// Gets a compliance profile by ID
        /// </summary>
        Task<ComplianceProfile?> GetProfileByIdAsync(string id);

        /// <summary>
        /// Updates an existing compliance profile
        /// </summary>
        Task UpdateProfileAsync(ComplianceProfile profile);

        /// <summary>
        /// Deletes a compliance profile
        /// </summary>
        Task DeleteProfileAsync(string id);

        /// <summary>
        /// Checks if a profile exists for a user
        /// </summary>
        Task<bool> ProfileExistsForUserAsync(string userId);
    }
}
