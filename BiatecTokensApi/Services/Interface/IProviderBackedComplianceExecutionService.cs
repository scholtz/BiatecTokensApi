using BiatecTokensApi.Models.ProviderBackedCompliance;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service contract for provider-backed compliance case execution.
    ///
    /// <para>
    /// This service bridges the compliance case lifecycle (approve / reject / RFI /
    /// sanctions review) with external provider validation, enforces fail-closed
    /// behaviour for protected paths, and produces durable, release-grade evidence
    /// artifacts that business owners and compliance leads can review.
    /// </para>
    ///
    /// <para>
    /// All methods fail clearly when required configuration is absent or a provider
    /// is unavailable — there are no silent success-shaped fallbacks.
    /// </para>
    /// </summary>
    public interface IProviderBackedComplianceExecutionService
    {
        /// <summary>
        /// Executes a compliance case decision (approve, reject, return-for-information,
        /// sanctions review, or escalation) through the provider-backed execution path.
        ///
        /// <para>
        /// When <see cref="ExecuteProviderBackedDecisionRequest.RequireProviderBacked"/> is
        /// true, the call fails if execution mode is Simulated.
        /// </para>
        /// <para>
        /// When <see cref="ExecuteProviderBackedDecisionRequest.RequireKycAmlSignOff"/> is
        /// true and execution mode is LiveProvider or ProtectedSandbox, the call fails
        /// if KYC/AML sign-off evidence for the case subject is absent, stale, or
        /// not provider-backed.
        /// </para>
        /// </summary>
        /// <param name="caseId">ID of the compliance case to act on.</param>
        /// <param name="request">Decision request parameters.</param>
        /// <param name="actorId">ID of the operator or system initiating the decision.</param>
        /// <returns>Execution result including evidence artifact and diagnostics.</returns>
        Task<ExecuteProviderBackedDecisionResponse> ExecuteDecisionAsync(
            string caseId,
            ExecuteProviderBackedDecisionRequest request,
            string actorId);

        /// <summary>
        /// Returns the current execution status and full execution history for a
        /// compliance case, including diagnostics from the most recent attempt.
        /// </summary>
        /// <param name="caseId">ID of the compliance case.</param>
        /// <param name="actorId">ID of the querying operator.</param>
        /// <param name="correlationId">Optional correlation ID for distributed tracing.</param>
        /// <returns>Execution status and ordered history.</returns>
        Task<GetProviderBackedExecutionStatusResponse> GetExecutionStatusAsync(
            string caseId,
            string actorId,
            string? correlationId = null);

        /// <summary>
        /// Builds a durable, content-hashed sign-off evidence bundle for a compliance case.
        ///
        /// <para>
        /// When <see cref="BuildProviderBackedSignOffEvidenceRequest.RequireProviderBackedEvidence"/>
        /// is true, the build fails if the execution history contains any simulated executions.
        /// </para>
        /// </summary>
        /// <param name="caseId">ID of the compliance case.</param>
        /// <param name="request">Bundle build parameters.</param>
        /// <param name="actorId">ID of the requesting operator.</param>
        /// <returns>Sign-off evidence bundle or failure diagnostics.</returns>
        Task<BuildProviderBackedSignOffEvidenceResponse> BuildSignOffEvidenceAsync(
            string caseId,
            BuildProviderBackedSignOffEvidenceRequest request,
            string actorId);
    }
}
