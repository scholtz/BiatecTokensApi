using BiatecTokensApi.Models.Aml;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for AML screening record persistence
    /// </summary>
    public interface IAmlRepository
    {
        /// <summary>Creates a new AML record</summary>
        Task<AmlRecord> CreateAmlRecordAsync(AmlRecord record);

        /// <summary>Gets an AML record by ID</summary>
        Task<AmlRecord?> GetAmlRecordAsync(string amlId);

        /// <summary>Gets the most recent AML record for a user</summary>
        Task<AmlRecord?> GetAmlRecordByUserIdAsync(string userId);

        /// <summary>Gets an AML record by provider reference ID</summary>
        Task<AmlRecord?> GetAmlRecordByProviderReferenceIdAsync(string providerReferenceId);

        /// <summary>Updates an existing AML record</summary>
        Task<AmlRecord> UpdateAmlRecordAsync(AmlRecord record);

        /// <summary>Gets all AML records for a user</summary>
        Task<List<AmlRecord>> GetAmlRecordsByUserIdAsync(string userId);

        /// <summary>Gets all AML records due for re-screening by the given cutoff date</summary>
        Task<List<AmlRecord>> GetRecordsDueForRescreeningAsync(DateTime cutoff);
    }
}
