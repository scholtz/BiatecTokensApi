using BiatecTokensApi.Models.Aml;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for GDPR right-to-erasure operations.
    /// Anonymizes PII while retaining compliance audit references as required by AMLD5 (5-year retention).
    /// </summary>
    public interface IGdprErasureService
    {
        /// <summary>
        /// Anonymizes all PII for the specified user while retaining audit trail references.
        /// KYC and AML records are anonymized — names, documents, and personal identifiers are
        /// replaced with a pseudonymous reference, but record IDs and timestamps are preserved
        /// for regulatory audit compliance.
        /// </summary>
        /// <param name="request">Erasure request including user ID and reason</param>
        /// <param name="correlationId">Correlation ID for tracing</param>
        /// <returns>Erasure response with anonymization reference and record counts</returns>
        Task<GdprErasureResponse> EraseUserDataAsync(
            GdprErasureRequest request,
            string correlationId);
    }
}
