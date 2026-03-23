using BiatecTokensApi.Models.KycAmlOnboarding;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Manages the full lifecycle of provider-backed KYC/AML onboarding cases.
    ///
    /// <para>
    /// Each case represents a single subject's onboarding journey from initial
    /// creation through provider checks, reviewer decisions, and a final
    /// approved or rejected outcome. The state machine is fail-closed: if the
    /// required provider is absent, all operations surface that explicitly.
    /// </para>
    ///
    /// <para>
    /// All methods are idempotent-safe where idempotency keys are supplied and
    /// record a full audit trail of reviewer actions with actor IDs and rationale.
    /// </para>
    /// </summary>
    public interface IKycAmlOnboardingCaseService
    {
        /// <summary>
        /// Creates a new KYC/AML onboarding case for the given subject.
        ///
        /// <para>
        /// If an idempotency key is supplied and a case already exists for the same
        /// SubjectId and key, the existing case is returned without modification.
        /// </para>
        /// </summary>
        /// <param name="request">Case creation parameters.</param>
        /// <param name="actorId">ID of the operator creating the case.</param>
        /// <returns>The created (or idempotently returned) case.</returns>
        Task<CreateOnboardingCaseResponse> CreateCaseAsync(
            CreateOnboardingCaseRequest request,
            string actorId);

        /// <summary>
        /// Returns the current state of a single onboarding case.
        /// </summary>
        /// <param name="caseId">ID of the case to retrieve.</param>
        /// <returns>The case record, or a not-found error response.</returns>
        Task<GetOnboardingCaseResponse> GetCaseAsync(string caseId);

        /// <summary>
        /// Initiates provider KYC/AML checks for an onboarding case.
        ///
        /// <para>
        /// When the required provider service is not configured, the response carries
        /// <c>ErrorCode = "PROVIDER_NOT_CONFIGURED"</c> and the case state is set to
        /// <see cref="KycAmlOnboardingCaseState.ConfigurationMissing"/>. This is an
        /// explicit fail-closed outcome — there is no silent success-shaped fallback.
        /// </para>
        ///
        /// <para>
        /// If the case is not in the <see cref="KycAmlOnboardingCaseState.Initiated"/>
        /// state, the response carries <c>ErrorCode = "INVALID_STATE"</c>.
        /// </para>
        /// </summary>
        /// <param name="caseId">ID of the case to advance.</param>
        /// <param name="request">Provider check initiation parameters.</param>
        /// <param name="actorId">ID of the operator initiating checks.</param>
        /// <returns>Updated case record and verification journey ID on success.</returns>
        Task<InitiateProviderChecksResponse> InitiateProviderChecksAsync(
            string caseId,
            InitiateProviderChecksRequest request,
            string actorId);

        /// <summary>
        /// Records a reviewer action on an onboarding case and applies the
        /// corresponding state transition.
        ///
        /// <para>
        /// Valid state transitions:
        /// <list type="bullet">
        ///   <item><see cref="KycAmlOnboardingActionKind.Approve"/>: PendingReview/UnderReview → Approved</item>
        ///   <item><see cref="KycAmlOnboardingActionKind.Reject"/>: any non-terminal → Rejected</item>
        ///   <item><see cref="KycAmlOnboardingActionKind.Escalate"/>: PendingReview/UnderReview → Escalated</item>
        ///   <item><see cref="KycAmlOnboardingActionKind.RequestAdditionalInfo"/>: PendingReview/UnderReview → RequiresAdditionalInfo</item>
        ///   <item><see cref="KycAmlOnboardingActionKind.AddNote"/>: any active state, no state change</item>
        /// </list>
        /// Invalid transitions return <c>ErrorCode = "INVALID_STATE_TRANSITION"</c>.
        /// </para>
        /// </summary>
        /// <param name="caseId">ID of the case to act on.</param>
        /// <param name="request">Action parameters including kind, rationale, and notes.</param>
        /// <param name="actorId">ID of the reviewer taking the action.</param>
        /// <returns>The recorded action and updated case record.</returns>
        Task<RecordReviewerActionResponse> RecordReviewerActionAsync(
            string caseId,
            RecordReviewerActionRequest request,
            string actorId);

        /// <summary>
        /// Returns a structured evidence summary for a case, reflecting provider
        /// backing status, checks completed, and actionable guidance.
        ///
        /// <para>
        /// When provider configuration is absent, the summary carries
        /// <see cref="KycAmlOnboardingEvidenceState.MissingConfiguration"/> and
        /// <c>IsProviderBacked = false</c>. There is no silent "green" fallback.
        /// </para>
        /// </summary>
        /// <param name="caseId">ID of the case to summarise.</param>
        /// <returns>Evidence summary or a not-found error response.</returns>
        Task<GetOnboardingEvidenceSummaryResponse> GetEvidenceSummaryAsync(string caseId);

        /// <summary>
        /// Lists onboarding cases, optionally filtered by subject ID and state.
        /// </summary>
        /// <param name="request">Optional filter and pagination parameters.</param>
        /// <returns>A page of matching cases with a total count.</returns>
        Task<ListOnboardingCasesResponse> ListCasesAsync(ListOnboardingCasesRequest? request = null);
    }
}
