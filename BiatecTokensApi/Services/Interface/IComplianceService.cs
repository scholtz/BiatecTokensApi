using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for managing compliance metadata
    /// </summary>
    public interface IComplianceService
    {
        /// <summary>
        /// Creates or updates compliance metadata for a token
        /// </summary>
        /// <param name="request">The compliance metadata request</param>
        /// <param name="createdBy">The address of the user creating/updating the metadata</param>
        /// <returns>Response with the created/updated metadata</returns>
        Task<ComplianceMetadataResponse> UpsertMetadataAsync(UpsertComplianceMetadataRequest request, string createdBy);

        /// <summary>
        /// Gets compliance metadata for a specific token
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <returns>Response with the compliance metadata</returns>
        Task<ComplianceMetadataResponse> GetMetadataAsync(ulong assetId);

        /// <summary>
        /// Deletes compliance metadata for a specific token
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <returns>Response indicating success or failure</returns>
        Task<ComplianceMetadataResponse> DeleteMetadataAsync(ulong assetId);

        /// <summary>
        /// Lists compliance metadata with optional filtering
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>Response with list of compliance metadata</returns>
        Task<ComplianceMetadataListResponse> ListMetadataAsync(ListComplianceMetadataRequest request);

        /// <summary>
        /// Validates network-specific compliance rules
        /// </summary>
        /// <param name="network">The network name</param>
        /// <param name="metadata">The compliance metadata to validate</param>
        /// <returns>Validation error message, or null if valid</returns>
        string? ValidateNetworkRules(string? network, UpsertComplianceMetadataRequest metadata);
    }
}
