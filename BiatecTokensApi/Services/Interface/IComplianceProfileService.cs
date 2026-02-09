using BiatecTokensApi.Models.ComplianceProfile;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for managing compliance profiles
    /// </summary>
    public interface IComplianceProfileService
    {
        /// <summary>
        /// Creates or updates a compliance profile for a user
        /// </summary>
        /// <param name="request">The profile creation/update request</param>
        /// <param name="userId">The user ID</param>
        /// <param name="ipAddress">Optional IP address for audit logging</param>
        /// <param name="userAgent">Optional user agent for audit logging</param>
        /// <returns>Response with the created/updated profile</returns>
        Task<ComplianceProfileResponse> UpsertProfileAsync(
            UpsertComplianceProfileRequest request,
            string userId,
            string? ipAddress = null,
            string? userAgent = null);

        /// <summary>
        /// Gets a compliance profile for a user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>Response with the profile</returns>
        Task<ComplianceProfileResponse> GetProfileAsync(string userId);

        /// <summary>
        /// Gets audit log entries for a compliance profile
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="startDate">Optional start date filter</param>
        /// <param name="endDate">Optional end date filter</param>
        /// <returns>List of audit entries</returns>
        Task<List<ComplianceProfileAuditEntry>> GetAuditLogAsync(
            string userId,
            DateTime? startDate = null,
            DateTime? endDate = null);

        /// <summary>
        /// Exports audit log entries as JSON
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="startDate">Optional start date filter</param>
        /// <param name="endDate">Optional end date filter</param>
        /// <returns>JSON string of audit entries</returns>
        Task<string> ExportAuditLogJsonAsync(
            string userId,
            DateTime? startDate = null,
            DateTime? endDate = null);

        /// <summary>
        /// Exports audit log entries as CSV
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="startDate">Optional start date filter</param>
        /// <param name="endDate">Optional end date filter</param>
        /// <returns>CSV string of audit entries</returns>
        Task<string> ExportAuditLogCsvAsync(
            string userId,
            DateTime? startDate = null,
            DateTime? endDate = null);

        /// <summary>
        /// Validates jurisdiction code against supported ISO codes
        /// </summary>
        /// <param name="jurisdiction">ISO 3166-1 alpha-2 country code</param>
        /// <returns>True if valid, false otherwise</returns>
        bool IsValidJurisdiction(string jurisdiction);

        /// <summary>
        /// Validates issuance intent category
        /// </summary>
        /// <param name="issuanceIntent">Issuance intent category</param>
        /// <returns>True if valid, false otherwise</returns>
        bool IsValidIssuanceIntent(string issuanceIntent);
    }
}
