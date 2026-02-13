using BiatecTokensApi.Models.Kyc;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for KYC record persistence
    /// </summary>
    public interface IKycRepository
    {
        /// <summary>
        /// Creates a new KYC record
        /// </summary>
        /// <param name="record">The KYC record to create</param>
        /// <returns>The created record</returns>
        Task<KycRecord> CreateKycRecordAsync(KycRecord record);

        /// <summary>
        /// Gets a KYC record by ID
        /// </summary>
        /// <param name="kycId">The KYC record ID</param>
        /// <returns>The KYC record if found, null otherwise</returns>
        Task<KycRecord?> GetKycRecordAsync(string kycId);

        /// <summary>
        /// Gets a KYC record by user ID
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>The most recent KYC record for the user, null if none exists</returns>
        Task<KycRecord?> GetKycRecordByUserIdAsync(string userId);

        /// <summary>
        /// Gets a KYC record by provider reference ID
        /// </summary>
        /// <param name="providerReferenceId">The provider reference ID</param>
        /// <returns>The KYC record if found, null otherwise</returns>
        Task<KycRecord?> GetKycRecordByProviderReferenceIdAsync(string providerReferenceId);

        /// <summary>
        /// Updates an existing KYC record
        /// </summary>
        /// <param name="record">The updated KYC record</param>
        /// <returns>The updated record</returns>
        Task<KycRecord> UpdateKycRecordAsync(KycRecord record);

        /// <summary>
        /// Gets all KYC records for a user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>List of KYC records for the user</returns>
        Task<List<KycRecord>> GetKycRecordsByUserIdAsync(string userId);

        /// <summary>
        /// Gets all KYC records with a specific status
        /// </summary>
        /// <param name="status">The status to filter by</param>
        /// <returns>List of KYC records with the specified status</returns>
        Task<List<KycRecord>> GetKycRecordsByStatusAsync(KycStatus status);
    }
}
