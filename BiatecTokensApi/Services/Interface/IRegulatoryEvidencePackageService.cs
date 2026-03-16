using BiatecTokensApi.Models.RegulatoryEvidencePackage;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for assembling and retrieving regulator-facing evidence packages.
    /// </summary>
    /// <remarks>
    /// Each package has a stable identifier, an audience profile, and a manifest that enumerates
    /// every source record considered during assembly — including those that are missing or stale.
    /// The backend is the authoritative source for readiness determination; clients must never
    /// synthesise missing evidence or override the readiness status.
    /// </remarks>
    public interface IRegulatoryEvidencePackageService
    {
        /// <summary>
        /// Assembles a new regulatory evidence package for the specified subject and audience profile.
        /// </summary>
        /// <param name="request">Package creation request including subject ID, audience profile, and optional filters.</param>
        /// <returns>
        /// Package creation response containing the summary of the assembled package.
        /// If an idempotency key is supplied and a matching package already exists, the cached
        /// summary is returned with <c>IsIdempotentReplay = true</c>.
        /// </returns>
        Task<CreateRegulatoryEvidencePackageResponse> CreatePackageAsync(CreateRegulatoryEvidencePackageRequest request);

        /// <summary>
        /// Retrieves the lightweight summary for an existing evidence package.
        /// </summary>
        /// <param name="packageId">Stable package identifier returned from <see cref="CreatePackageAsync"/>.</param>
        /// <param name="correlationId">Optional correlation ID for request tracing.</param>
        /// <returns>Package summary response; <see cref="GetPackageSummaryResponse.Success"/> is false when not found.</returns>
        Task<GetPackageSummaryResponse> GetPackageSummaryAsync(string packageId, string? correlationId = null);

        /// <summary>
        /// Retrieves the canonical detail payload for an existing evidence package.
        /// </summary>
        /// <param name="packageId">Stable package identifier.</param>
        /// <param name="correlationId">Optional correlation ID for request tracing.</param>
        /// <returns>
        /// Full canonical package detail including manifest, KYC/AML summary, contradictions,
        /// remediation items, approval history, posture transitions, and readiness rationale.
        /// </returns>
        Task<GetPackageDetailResponse> GetPackageDetailAsync(string packageId, string? correlationId = null);

        /// <summary>
        /// Lists evidence packages for a subject, most recently generated first.
        /// </summary>
        /// <param name="subjectId">Subject identifier.</param>
        /// <param name="limit">Maximum number of summaries to return (1–100, default 20).</param>
        /// <param name="correlationId">Optional correlation ID for request tracing.</param>
        /// <returns>Ordered list of package summaries for the subject.</returns>
        Task<ListEvidencePackagesResponse> ListPackagesAsync(
            string subjectId,
            int limit = 20,
            string? correlationId = null);
    }
}
