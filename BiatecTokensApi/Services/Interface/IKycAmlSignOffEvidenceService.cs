using BiatecTokensApi.Models.KycAmlSignOff;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Orchestrates provider-backed KYC/AML sign-off evidence flows.
    /// Tracks execution mode (live, protected sandbox, or simulated), generates durable
    /// evidence artifacts, provides plain-language explanations for enterprise frontend
    /// workflows, and enforces fail-closed behaviour for all adverse or degraded conditions.
    /// </summary>
    public interface IKycAmlSignOffEvidenceService
    {
        /// <summary>
        /// Initiates a new KYC/AML sign-off evidence flow for a subject.
        /// If an idempotency key is supplied and an existing record matches, the existing
        /// record is returned without re-initiating the provider check.
        /// Fails closed when a live-provider execution is requested but the provider is
        /// not configured.
        /// </summary>
        Task<InitiateKycAmlSignOffResponse> InitiateSignOffAsync(
            InitiateKycAmlSignOffRequest request,
            string actorId,
            string correlationId);

        /// <summary>
        /// Processes an inbound provider callback and updates the matching sign-off
        /// record.  Validates the callback payload (including HMAC signature when
        /// configured) before applying state transitions. Fails closed when the
        /// payload is malformed, the provider reference is unknown, or the signature
        /// is invalid.
        /// </summary>
        Task<ProcessKycAmlSignOffCallbackResponse> ProcessCallbackAsync(
            string recordId,
            ProcessKycAmlSignOffCallbackRequest request,
            string correlationId);

        /// <summary>
        /// Retrieves the full sign-off record including audit trail and evidence artifacts.
        /// </summary>
        Task<GetKycAmlSignOffRecordResponse> GetRecordAsync(string recordId);

        /// <summary>
        /// Evaluates the readiness state of a sign-off record for approval gating.
        /// Only returns <see cref="KycAmlSignOffReadinessState.Ready"/> when
        /// provider-backed evidence is present, unexpired, and all checks passed.
        /// </summary>
        Task<KycAmlSignOffReadinessResponse> GetReadinessAsync(string recordId);

        /// <summary>
        /// Returns the evidence artifacts associated with a sign-off record.
        /// </summary>
        Task<GetKycAmlSignOffArtifactsResponse> GetArtifactsAsync(string recordId);

        /// <summary>
        /// Lists all sign-off records for a subject, ordered by creation time descending.
        /// </summary>
        Task<ListKycAmlSignOffRecordsResponse> ListRecordsForSubjectAsync(string subjectId);

        /// <summary>
        /// Polls the provider for the current status of an in-progress check and
        /// updates the sign-off record if the outcome has changed.
        /// Fails closed when the provider is unavailable.
        /// </summary>
        Task<PollKycAmlSignOffStatusResponse> PollProviderStatusAsync(
            string recordId,
            string correlationId);
    }
}
