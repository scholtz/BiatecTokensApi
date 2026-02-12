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

        // Phase 2: Issuer Profile Management
        
        /// <summary>
        /// Creates or updates an issuer profile
        /// </summary>
        /// <param name="profile">The issuer profile</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpsertIssuerProfileAsync(IssuerProfile profile);

        /// <summary>
        /// Gets an issuer profile by address
        /// </summary>
        /// <param name="issuerAddress">The issuer address</param>
        /// <returns>The issuer profile or null if not found</returns>
        Task<IssuerProfile?> GetIssuerProfileAsync(string issuerAddress);

        /// <summary>
        /// Lists asset IDs for a specific issuer
        /// </summary>
        /// <param name="issuerAddress">The issuer address</param>
        /// <param name="request">The list request with filters</param>
        /// <returns>List of asset IDs</returns>
        Task<List<ulong>> ListIssuerAssetsAsync(string issuerAddress, ListIssuerAssetsRequest request);

        /// <summary>
        /// Gets the total count of assets for an issuer
        /// </summary>
        /// <param name="issuerAddress">The issuer address</param>
        /// <param name="request">The list request with filters</param>
        /// <returns>Total count</returns>
        Task<int> GetIssuerAssetCountAsync(string issuerAddress, ListIssuerAssetsRequest request);

        // Phase 3: Blacklist Management

        /// <summary>
        /// Creates a blacklist entry
        /// </summary>
        /// <param name="entry">The blacklist entry</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> CreateBlacklistEntryAsync(BlacklistEntry entry);

        /// <summary>
        /// Gets a blacklist entry by ID
        /// </summary>
        /// <param name="id">The entry ID</param>
        /// <returns>The blacklist entry or null if not found</returns>
        Task<BlacklistEntry?> GetBlacklistEntryAsync(string id);

        /// <summary>
        /// Updates a blacklist entry
        /// </summary>
        /// <param name="entry">The blacklist entry</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> UpdateBlacklistEntryAsync(BlacklistEntry entry);

        /// <summary>
        /// Deletes a blacklist entry
        /// </summary>
        /// <param name="id">The entry ID</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DeleteBlacklistEntryAsync(string id);

        /// <summary>
        /// Lists blacklist entries with filtering
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>List of blacklist entries</returns>
        Task<List<BlacklistEntry>> ListBlacklistEntriesAsync(ListBlacklistEntriesRequest request);

        /// <summary>
        /// Gets the total count of blacklist entries matching the filter
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>Total count</returns>
        Task<int> GetBlacklistEntryCountAsync(ListBlacklistEntriesRequest request);

        /// <summary>
        /// Checks if an address is blacklisted
        /// </summary>
        /// <param name="address">The address to check</param>
        /// <param name="assetId">Optional asset ID for asset-specific check</param>
        /// <param name="network">Optional network filter</param>
        /// <returns>List of active blacklist entries for the address</returns>
        Task<List<BlacklistEntry>> CheckBlacklistAsync(string address, ulong? assetId, string? network);

        // Validation Evidence Management

        /// <summary>
        /// Stores validation evidence for audit trails
        /// </summary>
        /// <param name="evidence">The validation evidence to store</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> StoreValidationEvidenceAsync(ValidationEvidence evidence);

        /// <summary>
        /// Gets validation evidence by evidence ID
        /// </summary>
        /// <param name="evidenceId">The evidence identifier</param>
        /// <returns>The validation evidence or null if not found</returns>
        Task<ValidationEvidence?> GetValidationEvidenceByIdAsync(string evidenceId);

        /// <summary>
        /// Lists validation evidence with optional filtering
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>List of validation evidence entries</returns>
        Task<List<ValidationEvidence>> ListValidationEvidenceAsync(ListValidationEvidenceRequest request);

        /// <summary>
        /// Gets the total count of validation evidence entries matching the filter
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>Total count</returns>
        Task<int> GetValidationEvidenceCountAsync(ListValidationEvidenceRequest request);

        /// <summary>
        /// Gets the most recent passing validation for a token or pre-issuance ID
        /// </summary>
        /// <param name="tokenId">The token ID</param>
        /// <param name="preIssuanceId">Optional pre-issuance identifier</param>
        /// <returns>The most recent passing validation evidence or null</returns>
        Task<ValidationEvidence?> GetMostRecentPassingValidationAsync(ulong? tokenId, string? preIssuanceId);
    }
}
