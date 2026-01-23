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
    }
}
