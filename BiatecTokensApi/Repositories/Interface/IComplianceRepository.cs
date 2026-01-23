using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for managing compliance metadata
    /// </summary>
    public interface IComplianceRepository
    {
        /// <summary>
        /// Creates or updates compliance metadata for a token
        /// </summary>
        /// <param name="metadata">The compliance metadata to save</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpsertMetadataAsync(ComplianceMetadata metadata);

        /// <summary>
        /// Gets compliance metadata for a specific token
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <returns>The compliance metadata or null if not found</returns>
        Task<ComplianceMetadata?> GetMetadataByAssetIdAsync(ulong assetId);

        /// <summary>
        /// Deletes compliance metadata for a specific token
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteMetadataAsync(ulong assetId);

        /// <summary>
        /// Lists all compliance metadata with optional filtering
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>List of compliance metadata entries</returns>
        Task<List<ComplianceMetadata>> ListMetadataAsync(ListComplianceMetadataRequest request);

        /// <summary>
        /// Gets the total count of metadata entries matching the filter
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>Total count</returns>
        Task<int> GetMetadataCountAsync(ListComplianceMetadataRequest request);

        /// <summary>
        /// Adds an audit log entry for compliance operations
        /// </summary>
        /// <param name="entry">The audit log entry to add</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> AddAuditLogEntryAsync(ComplianceAuditLogEntry entry);

        /// <summary>
        /// Gets audit log entries with optional filtering
        /// </summary>
        /// <param name="request">The audit log request with filters</param>
        /// <returns>List of audit log entries</returns>
        Task<List<ComplianceAuditLogEntry>> GetAuditLogAsync(GetComplianceAuditLogRequest request);

        /// <summary>
        /// Gets the total count of audit log entries matching the filter
        /// </summary>
        /// <param name="request">The audit log request with filters</param>
        /// <returns>Total count</returns>
        Task<int> GetAuditLogCountAsync(GetComplianceAuditLogRequest request);

        /// <summary>
        /// Creates a new compliance attestation
        /// </summary>
        /// <param name="attestation">The attestation to create</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> CreateAttestationAsync(ComplianceAttestation attestation);

        /// <summary>
        /// Gets a compliance attestation by ID
        /// </summary>
        /// <param name="id">The attestation ID</param>
        /// <returns>The attestation or null if not found</returns>
        Task<ComplianceAttestation?> GetAttestationByIdAsync(string id);

        /// <summary>
        /// Lists compliance attestations with optional filtering
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>List of attestations</returns>
        Task<List<ComplianceAttestation>> ListAttestationsAsync(ListComplianceAttestationsRequest request);

        /// <summary>
        /// Gets the total count of attestations matching the filter
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>Total count</returns>
        Task<int> GetAttestationCountAsync(ListComplianceAttestationsRequest request);
    }
}
