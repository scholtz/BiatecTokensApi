using BiatecTokensApi.Models.Compliance;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for token metadata validation and evidence management
    /// </summary>
    public interface IValidationService
    {
        /// <summary>
        /// Validates token metadata against standard schemas and network-specific rules
        /// </summary>
        /// <param name="request">The validation request containing metadata and context</param>
        /// <param name="requestedBy">The address of the user requesting validation</param>
        /// <returns>Validation response with evidence and rule evaluations</returns>
        /// <remarks>
        /// This method performs deterministic validation of token metadata against versioned rule sets.
        /// If DryRun is false, validation evidence is persisted for audit trails.
        /// Evidence includes complete rule evaluations, timestamps, and checksums for tamper detection.
        /// </remarks>
        Task<ValidateTokenMetadataResponse> ValidateTokenMetadataAsync(
            ValidateTokenMetadataRequest request, 
            string requestedBy);

        /// <summary>
        /// Retrieves validation evidence by evidence ID
        /// </summary>
        /// <param name="evidenceId">The unique evidence identifier</param>
        /// <returns>Response containing the validation evidence</returns>
        Task<GetValidationEvidenceResponse> GetValidationEvidenceAsync(string evidenceId);

        /// <summary>
        /// Lists validation evidence for a token or pre-issuance identifier
        /// </summary>
        /// <param name="request">The list request with filters</param>
        /// <returns>Response containing list of validation evidence</returns>
        Task<ListValidationEvidenceResponse> ListValidationEvidenceAsync(
            ListValidationEvidenceRequest request);

        /// <summary>
        /// Verifies that a token has passing validation evidence
        /// </summary>
        /// <param name="tokenId">The token ID to verify</param>
        /// <param name="preIssuanceId">Optional pre-issuance identifier</param>
        /// <returns>True if the token has passing validation, false otherwise</returns>
        /// <remarks>
        /// This method is used by token issuance flows to ensure validation has passed
        /// before allowing issuance to proceed.
        /// </remarks>
        Task<bool> VerifyTokenHasPassingValidationAsync(ulong? tokenId, string? preIssuanceId);

        /// <summary>
        /// Computes SHA256 checksum for validation evidence
        /// </summary>
        /// <param name="evidence">The validation evidence</param>
        /// <returns>SHA256 checksum as hex string</returns>
        string ComputeEvidenceChecksum(ValidationEvidence evidence);
    }
}
