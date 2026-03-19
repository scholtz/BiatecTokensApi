using BiatecTokensApi.Models.LiveProviderVerificationJourney;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Orchestrates end-to-end live-provider KYC/AML verification journeys for
    /// subject onboarding and compliance case workflows.
    ///
    /// <para>
    /// This service provides a unified, operator-observable view of a subject's
    /// complete verification journey — from KYC initiation through AML screening,
    /// manual review, and final approval decision — with structured diagnostics,
    /// approval-decision observability, and release-grade evidence generation.
    /// </para>
    ///
    /// <para>
    /// All methods fail closed when required provider configuration is absent,
    /// a provider is unreachable, or evidence is stale. There are no silent
    /// success-shaped fallbacks for degraded or misconfigured provider paths.
    /// </para>
    /// </summary>
    public interface ILiveProviderVerificationJourneyService
    {
        /// <summary>
        /// Starts a new live-provider KYC/AML verification journey for a subject.
        ///
        /// <para>
        /// Initiates the configured KYC identity check and (if applicable) AML screening
        /// against the requested execution mode. Creates a durable journey record with
        /// structured diagnostics.
        /// </para>
        ///
        /// <para>
        /// When <see cref="StartVerificationJourneyRequest.RequireProviderBacked"/> is
        /// true, the call fails if execution mode resolves to Simulated.
        /// When provider configuration is missing or the provider is unreachable, the
        /// journey is created in the <see cref="VerificationJourneyStage.Degraded"/> stage
        /// with actionable diagnostics — it does not silently continue.
        /// </para>
        /// </summary>
        /// <param name="request">Journey start parameters including subject ID and metadata.</param>
        /// <param name="actorId">ID of the operator or system initiating the journey.</param>
        /// <returns>The created journey record and diagnostics.</returns>
        Task<StartVerificationJourneyResponse> StartJourneyAsync(
            StartVerificationJourneyRequest request,
            string actorId);

        /// <summary>
        /// Returns the current status of a verification journey, including the full
        /// step audit trail and a computed approval-decision explanation.
        ///
        /// <para>
        /// The approval-decision explanation includes structured rationale for why the
        /// journey is at its current stage, what checks passed or failed, and what
        /// action the operator should take next.
        /// </para>
        /// </summary>
        /// <param name="journeyId">ID of the verification journey to query.</param>
        /// <param name="correlationId">Optional correlation ID for distributed tracing.</param>
        /// <returns>Journey status with approval decision explanation.</returns>
        Task<GetVerificationJourneyStatusResponse> GetJourneyStatusAsync(
            string journeyId,
            string? correlationId = null);

        /// <summary>
        /// Evaluates and returns the current approval-decision explanation for a
        /// verification journey.
        ///
        /// <para>
        /// Provides structured operator-facing rationale for why a journey is approved,
        /// rejected, pending review, blocked, or requiring action. Includes:
        /// - which checks passed, failed, or are pending;
        /// - whether evidence is provider-backed and release-grade;
        /// - actionable guidance for each non-approved state.
        /// </para>
        /// </summary>
        /// <param name="journeyId">ID of the verification journey to evaluate.</param>
        /// <param name="correlationId">Optional correlation ID for distributed tracing.</param>
        /// <returns>Approval decision explanation with structured diagnostics.</returns>
        Task<EvaluateApprovalDecisionResponse> EvaluateApprovalDecisionAsync(
            string journeyId,
            string? correlationId = null);

        /// <summary>
        /// Generates a durable, content-hashed release evidence artifact for a
        /// verification journey.
        ///
        /// <para>
        /// The artifact bundles all journey steps, the current approval-decision
        /// explanation, and provider references into a single tamper-evident record
        /// suitable for business-owner sign-off, audit, and regulator-facing review.
        /// </para>
        ///
        /// <para>
        /// When <see cref="GenerateVerificationJourneyEvidenceRequest.RequireProviderBacked"/>
        /// is true, generation fails if the journey execution mode is Simulated.
        /// This preserves the fail-closed contract for protected release paths.
        /// </para>
        /// </summary>
        /// <param name="journeyId">ID of the verification journey.</param>
        /// <param name="request">Evidence generation parameters including release tag.</param>
        /// <param name="actorId">ID of the requesting operator.</param>
        /// <returns>Release evidence artifact or failure diagnostics.</returns>
        Task<GenerateVerificationJourneyEvidenceResponse> GenerateReleaseEvidenceAsync(
            string journeyId,
            GenerateVerificationJourneyEvidenceRequest request,
            string actorId);
    }
}
