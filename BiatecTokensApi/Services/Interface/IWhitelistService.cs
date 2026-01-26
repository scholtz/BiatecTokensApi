using BiatecTokensApi.Models.Whitelist;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Interface for whitelist service operations
    /// </summary>
    public interface IWhitelistService
    {
        /// <summary>
        /// Adds a single address to the whitelist
        /// </summary>
        /// <param name="request">The add whitelist entry request</param>
        /// <param name="createdBy">The address of the user creating the entry</param>
        /// <returns>The whitelist response</returns>
        Task<WhitelistResponse> AddEntryAsync(AddWhitelistEntryRequest request, string createdBy);

        /// <summary>
        /// Removes an address from the whitelist
        /// </summary>
        /// <param name="request">The remove whitelist entry request</param>
        /// <returns>The whitelist response</returns>
        Task<WhitelistResponse> RemoveEntryAsync(RemoveWhitelistEntryRequest request);

        /// <summary>
        /// Bulk adds addresses to the whitelist
        /// </summary>
        /// <param name="request">The bulk add whitelist request</param>
        /// <param name="createdBy">The address of the user creating the entries</param>
        /// <returns>The bulk whitelist response</returns>
        Task<BulkWhitelistResponse> BulkAddEntriesAsync(BulkAddWhitelistRequest request, string createdBy);

        /// <summary>
        /// Lists whitelist entries for a token
        /// </summary>
        /// <param name="request">The list whitelist request</param>
        /// <returns>The whitelist list response</returns>
        Task<WhitelistListResponse> ListEntriesAsync(ListWhitelistRequest request);

        /// <summary>
        /// Validates an Algorand address format
        /// </summary>
        /// <param name="address">The address to validate</param>
        /// <returns>True if the address is valid</returns>
        bool IsValidAlgorandAddress(string address);

        /// <summary>
        /// Gets audit log entries for a token's whitelist
        /// </summary>
        /// <param name="request">The audit log request with filters and pagination</param>
        /// <returns>The audit log response with entries and pagination info</returns>
        Task<WhitelistAuditLogResponse> GetAuditLogAsync(GetWhitelistAuditLogRequest request);

        /// <summary>
        /// Validates if a transfer between two addresses is allowed based on whitelist rules
        /// </summary>
        /// <param name="request">The transfer validation request</param>
        /// <param name="performedBy">The address of the user performing the validation (for audit logging)</param>
        /// <returns>The validation response indicating if the transfer is allowed</returns>
        Task<ValidateTransferResponse> ValidateTransferAsync(ValidateTransferRequest request, string performedBy);

        /// <summary>
        /// Gets whitelist enforcement audit report (transfer validation events only) with summary statistics
        /// </summary>
        /// <param name="request">The enforcement report request with filters and pagination</param>
        /// <returns>The enforcement report response with entries and summary statistics</returns>
        Task<WhitelistEnforcementReportResponse> GetEnforcementReportAsync(GetWhitelistEnforcementReportRequest request);
    }
}
