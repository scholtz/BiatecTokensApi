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

        /// <summary>
        /// Gets audit log for compliance operations with optional filtering
        /// </summary>
        /// <param name="request">The audit log request with filters</param>
        /// <returns>Response with list of audit log entries</returns>
        Task<ComplianceAuditLogResponse> GetAuditLogAsync(GetComplianceAuditLogRequest request);

        /// <summary>
        /// Validates a token configuration against MICA/RWA compliance rules
        /// </summary>
        /// <param name="request">The validation request containing token configuration</param>
        /// <returns>Response with validation errors and warnings</returns>
        Task<ValidateTokenPresetResponse> ValidateTokenPresetAsync(ValidateTokenPresetRequest request);

        /// <summary>
        /// Creates a new compliance attestation
        /// </summary>
        /// <param name="request">The attestation creation request</param>
        /// <param name="createdBy">The address of the user creating the attestation</param>
        /// <returns>Response with the created attestation</returns>
        Task<ComplianceAttestationResponse> CreateAttestationAsync(CreateComplianceAttestationRequest request, string createdBy);

        /// <summary>
        /// Gets a compliance attestation by ID
        /// </summary>
        /// <param name="id">The attestation ID</param>
        /// <returns>Response with the attestation</returns>
        Task<ComplianceAttestationResponse> GetAttestationAsync(string id);

        /// <summary>
        /// Lists compliance attestations with optional filtering
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>Response with list of attestations</returns>
        Task<ComplianceAttestationListResponse> ListAttestationsAsync(ListComplianceAttestationsRequest request);

        /// <summary>
        /// Generates a comprehensive compliance report for VOI/Aramid tokens
        /// </summary>
        /// <param name="request">The compliance report request with filtering options</param>
        /// <param name="requestedBy">The address of the user requesting the report</param>
        /// <returns>Response with consolidated compliance status including metadata, whitelist, and audit logs</returns>
        /// <remarks>
        /// This method aggregates compliance metadata, whitelist statistics, and audit logs
        /// to provide enterprise-grade compliance reporting for VOI/Aramid networks.
        /// Supports MICA dashboard requirements and subscription-based access control.
        /// </remarks>
        Task<TokenComplianceReportResponse> GetComplianceReportAsync(GetTokenComplianceReportRequest request, string requestedBy);

        /// <summary>
        /// Generates a signed compliance attestation package for MICA audits
        /// </summary>
        /// <param name="request">The attestation package request containing token ID, date range, and format</param>
        /// <param name="requestedBy">The address of the user requesting the package</param>
        /// <returns>Response with signed attestation package including issuer metadata, token details, whitelist policy, and compliance status</returns>
        /// <remarks>
        /// This method generates a verifiable audit artifact for regulators and enterprise issuers.
        /// The package includes deterministic hash and signature metadata for audit verification.
        /// Supports MICA reporting requirements and subscription-based access control.
        /// </remarks>
        Task<AttestationPackageResponse> GenerateAttestationPackageAsync(GenerateAttestationPackageRequest request, string requestedBy);

        /// <summary>
        /// Gets compliance indicators for a token, providing a simplified view for frontend display
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <returns>Response with compliance indicators including MICA readiness, whitelisting status, and transfer restrictions</returns>
        /// <remarks>
        /// This method aggregates compliance metadata and whitelist information to provide
        /// enterprise readiness indicators and subscription value flags for the frontend.
        /// </remarks>
        Task<TokenComplianceIndicatorsResponse> GetComplianceIndicatorsAsync(ulong assetId);

        /// <summary>
        /// Gets per-network compliance metadata for all supported blockchain networks
        /// </summary>
        /// <returns>Response with list of networks and their compliance requirements</returns>
        /// <remarks>
        /// This method returns compliance flags and requirements for each supported blockchain network,
        /// enabling the frontend to display network-specific compliance indicators.
        /// Results include MICA readiness, whitelisting requirements, and source metadata.
        /// </remarks>
        Task<NetworkComplianceMetadataResponse> GetNetworkComplianceMetadataAsync();

        // Phase 2: Issuer Profile Management

        /// <summary>
        /// Creates or updates an issuer profile
        /// </summary>
        /// <param name="request">The issuer profile request</param>
        /// <param name="issuerAddress">The issuer's Algorand address</param>
        /// <returns>Response with the created/updated profile</returns>
        Task<IssuerProfileResponse> UpsertIssuerProfileAsync(UpsertIssuerProfileRequest request, string issuerAddress);

        /// <summary>
        /// Gets an issuer profile
        /// </summary>
        /// <param name="issuerAddress">The issuer's Algorand address</param>
        /// <returns>Response with the issuer profile</returns>
        Task<IssuerProfileResponse> GetIssuerProfileAsync(string issuerAddress);

        /// <summary>
        /// Gets issuer verification status
        /// </summary>
        /// <param name="issuerAddress">The issuer's Algorand address</param>
        /// <returns>Response with verification status and score</returns>
        Task<IssuerVerificationResponse> GetIssuerVerificationAsync(string issuerAddress);

        /// <summary>
        /// Lists assets for an issuer
        /// </summary>
        /// <param name="issuerAddress">The issuer's Algorand address</param>
        /// <param name="request">The list request with filters</param>
        /// <returns>Response with list of asset IDs</returns>
        Task<IssuerAssetsResponse> ListIssuerAssetsAsync(string issuerAddress, ListIssuerAssetsRequest request);

        // Phase 3: Blacklist Management

        /// <summary>
        /// Adds an address to the blacklist
        /// </summary>
        /// <param name="request">The blacklist entry request</param>
        /// <param name="createdBy">The address of the user creating the entry</param>
        /// <returns>Response with the created blacklist entry</returns>
        Task<BlacklistResponse> AddBlacklistEntryAsync(AddBlacklistEntryRequest request, string createdBy);

        /// <summary>
        /// Checks if an address is blacklisted
        /// </summary>
        /// <param name="request">The blacklist check request</param>
        /// <returns>Response with blacklist status and entries</returns>
        Task<BlacklistCheckResponse> CheckBlacklistAsync(CheckBlacklistRequest request);

        /// <summary>
        /// Lists blacklist entries
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>Response with list of blacklist entries</returns>
        Task<BlacklistListResponse> ListBlacklistEntriesAsync(ListBlacklistEntriesRequest request);

        /// <summary>
        /// Removes a blacklist entry
        /// </summary>
        /// <param name="id">The entry ID</param>
        /// <returns>Response indicating success or failure</returns>
        Task<BlacklistResponse> DeleteBlacklistEntryAsync(string id);

        /// <summary>
        /// Validates a proposed transfer against compliance rules
        /// </summary>
        /// <param name="request">The transfer validation request</param>
        /// <returns>Response with validation result and violations</returns>
        Task<TransferValidationResponse> ValidateTransferAsync(ValidateComplianceTransferRequest request);

        // Phase 4: MICA Checklist and Health

        /// <summary>
        /// Gets MICA compliance checklist for a token
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <returns>Response with MICA compliance checklist</returns>
        Task<MicaComplianceChecklistResponse> GetMicaComplianceChecklistAsync(ulong assetId);

        /// <summary>
        /// Gets aggregate compliance health for an issuer
        /// </summary>
        /// <param name="issuerAddress">The issuer's Algorand address</param>
        /// <param name="network">Optional network filter</param>
        /// <returns>Response with compliance health metrics</returns>
        Task<ComplianceHealthResponse> GetComplianceHealthAsync(string issuerAddress, string? network);

        /// <summary>
        /// Generates a signed compliance evidence bundle (ZIP) for auditors
        /// </summary>
        /// <param name="request">The evidence bundle request containing asset ID and date range</param>
        /// <param name="requestedBy">The address of the user requesting the bundle</param>
        /// <returns>Response with the ZIP bundle content and manifest metadata</returns>
        /// <remarks>
        /// This method generates a comprehensive ZIP archive containing:
        /// - Audit logs (whitelist, blacklist, compliance operations)
        /// - Whitelist history and current entries
        /// - Transfer approval records
        /// - Token metadata and compliance policies
        /// - Manifest file with SHA256 checksums and timestamps
        /// 
        /// The bundle is designed for MICA/RWA compliance audits and supports
        /// external verification with cryptographic checksums.
        /// </remarks>
        Task<ComplianceEvidenceBundleResponse> GenerateComplianceEvidenceBundleAsync(GenerateComplianceEvidenceBundleRequest request, string requestedBy);
    }
}
