using BiatecTokensApi.Models.ComplianceAuditExport;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for assembling and retrieving scenario-specific compliance audit export packages.
    /// </summary>
    /// <remarks>
    /// Each package is assembled for a specific scenario (release-readiness, onboarding case review,
    /// compliance blocker review, or approval-history export) and audience profile.
    ///
    /// Readiness semantics are fail-closed:
    /// <list type="bullet">
    ///   <item><description>Any missing required evidence → Incomplete</description></item>
    ///   <item><description>Any stale required evidence → Stale</description></item>
    ///   <item><description>Any unresolved critical blocker → Blocked</description></item>
    ///   <item><description>Provider unreachable → DegradedProviderUnavailable</description></item>
    ///   <item><description>Manual review stage pending → RequiresReview</description></item>
    ///   <item><description>All checks pass → Ready</description></item>
    /// </list>
    ///
    /// The backend is the authoritative source for readiness determination.
    /// Clients must never synthesise missing evidence or override the readiness status.
    /// </remarks>
    public interface IComplianceAuditExportService
    {
        /// <summary>
        /// Assembles a release-readiness sign-off audit export package.
        /// </summary>
        /// <param name="request">
        /// Request containing the subject ID, audience profile, optional head reference,
        /// environment label, and evidence filter timestamp.
        /// </param>
        /// <returns>
        /// Assembly response containing the canonical package.
        /// If an idempotency key is supplied and a matching package exists,
        /// the cached package is returned with <c>IsIdempotentReplay = true</c>.
        /// </returns>
        Task<ComplianceAuditExportResponse> AssembleReleaseReadinessExportAsync(
            ReleaseReadinessExportRequest request);

        /// <summary>
        /// Assembles an onboarding case review audit export package.
        /// </summary>
        /// <param name="request">
        /// Request containing the subject ID, audience profile, and optional case ID.
        /// If no case ID is specified, the most recent onboarding case for the subject is used.
        /// </param>
        /// <returns>Assembly response containing the canonical package.</returns>
        Task<ComplianceAuditExportResponse> AssembleOnboardingCaseReviewExportAsync(
            OnboardingCaseReviewExportRequest request);

        /// <summary>
        /// Assembles a compliance blocker review audit export package.
        /// </summary>
        /// <param name="request">
        /// Request containing the subject ID, audience profile, and whether to include
        /// recently resolved blockers for historical completeness.
        /// </param>
        /// <returns>Assembly response containing the canonical package.</returns>
        Task<ComplianceAuditExportResponse> AssembleBlockerReviewExportAsync(
            ComplianceBlockerReviewExportRequest request);

        /// <summary>
        /// Assembles an approval-history audit export package.
        /// </summary>
        /// <param name="request">
        /// Request containing the subject ID, audience profile, and optional decision limit.
        /// </param>
        /// <returns>Assembly response containing the canonical package.</returns>
        Task<ComplianceAuditExportResponse> AssembleApprovalHistoryExportAsync(
            ApprovalHistoryExportRequest request);

        /// <summary>
        /// Retrieves a previously assembled compliance audit export package by its export ID.
        /// </summary>
        /// <param name="exportId">Stable export identifier returned from an assembly endpoint.</param>
        /// <param name="correlationId">Optional correlation ID for request tracing.</param>
        /// <returns>
        /// Retrieval response; <see cref="GetComplianceAuditExportResponse.Success"/> is false when not found.
        /// </returns>
        Task<GetComplianceAuditExportResponse> GetExportAsync(
            string exportId,
            string? correlationId = null);

        /// <summary>
        /// Lists compliance audit export packages for a subject, most recently assembled first.
        /// </summary>
        /// <param name="subjectId">Subject identifier.</param>
        /// <param name="scenario">
        /// Optional scenario filter. When null, packages for all scenarios are returned.
        /// </param>
        /// <param name="limit">Maximum number of summaries to return (1–100, default 20).</param>
        /// <param name="correlationId">Optional correlation ID for request tracing.</param>
        /// <returns>Ordered list of export summaries for the subject.</returns>
        Task<ListComplianceAuditExportsResponse> ListExportsAsync(
            string subjectId,
            AuditScenario? scenario = null,
            int limit = 20,
            string? correlationId = null);
    }
}
