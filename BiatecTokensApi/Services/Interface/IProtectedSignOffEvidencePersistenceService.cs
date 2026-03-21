using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service contract for protected sign-off evidence persistence and
    /// approval-webhook parity.
    ///
    /// <para>
    /// This service provides authoritative backend storage and querying of
    /// approval webhook outcomes, protected sign-off evidence packs, and
    /// aggregated release-readiness status for the current head.
    /// </para>
    ///
    /// <para>
    /// All methods are fail-closed: when required inputs are missing, evidence
    /// is stale, or approval has not been received, the service produces explicit
    /// blocked or unavailable states rather than optimistic partial success.
    /// </para>
    /// </summary>
    public interface IProtectedSignOffEvidencePersistenceService
    {
        /// <summary>
        /// Records an incoming approval or escalation webhook outcome and persists it
        /// against the specified compliance case and head ref.
        ///
        /// <para>
        /// Malformed payloads are recorded with <see cref="ApprovalWebhookOutcome.Malformed"/>
        /// rather than silently dropped, enabling operators to diagnose webhook delivery
        /// failures.
        /// </para>
        /// </summary>
        /// <param name="request">The webhook arrival details including outcome, case ID, and head ref.</param>
        /// <param name="actorId">The ID of the operator or system recording this webhook.</param>
        /// <returns>A persisted <see cref="ApprovalWebhookRecord"/> or a failure response with diagnostics.</returns>
        Task<RecordApprovalWebhookResponse> RecordApprovalWebhookAsync(
            RecordApprovalWebhookRequest request,
            string actorId);

        /// <summary>
        /// Persists a protected sign-off evidence pack for the specified head ref,
        /// capturing the current state of all required evidence items.
        ///
        /// <para>
        /// When <see cref="PersistSignOffEvidenceRequest.RequireReleaseGrade"/> is true,
        /// the call fails if the evidence pack cannot be marked as release-grade.
        /// When <see cref="PersistSignOffEvidenceRequest.RequireApprovalWebhook"/> is true,
        /// the call fails if no approval webhook has been received for this head ref.
        /// </para>
        /// </summary>
        /// <param name="request">Evidence pack persistence parameters.</param>
        /// <param name="actorId">The ID of the operator triggering evidence capture.</param>
        /// <returns>The persisted evidence pack or a failure response with diagnostics.</returns>
        Task<PersistSignOffEvidenceResponse> PersistSignOffEvidenceAsync(
            PersistSignOffEvidenceRequest request,
            string actorId);

        /// <summary>
        /// Evaluates and returns the aggregated release-readiness status for the
        /// specified head ref.
        ///
        /// <para>
        /// The response is a single authoritative object that the frontend can consume
        /// without stitching together multiple weak signals. It includes evidence freshness,
        /// approval webhook presence, ordered blockers with remediation hints, and
        /// top-level operator guidance.
        /// </para>
        ///
        /// <para>
        /// The method is fail-closed: it reports <see cref="SignOffReleaseReadinessStatus.Blocked"/>
        /// when required approvals or evidence are absent, and
        /// <see cref="SignOffReleaseReadinessStatus.Stale"/> when evidence has expired
        /// or was produced for a different head.
        /// </para>
        /// </summary>
        /// <param name="request">Release readiness evaluation parameters.</param>
        /// <returns>Aggregated release readiness status with blockers and operator guidance.</returns>
        Task<GetSignOffReleaseReadinessResponse> GetReleaseReadinessAsync(
            GetSignOffReleaseReadinessRequest request);

        /// <summary>
        /// Retrieves the approval webhook history filtered by optional case ID and head ref.
        /// </summary>
        /// <param name="request">Filter parameters.</param>
        /// <returns>Ordered history of approval webhook records (newest first).</returns>
        Task<GetApprovalWebhookHistoryResponse> GetApprovalWebhookHistoryAsync(
            GetApprovalWebhookHistoryRequest request);

        /// <summary>
        /// Retrieves the evidence pack history filtered by optional head ref and case ID.
        /// </summary>
        /// <param name="request">Filter parameters.</param>
        /// <returns>Ordered history of evidence packs (newest first).</returns>
        Task<GetEvidencePackHistoryResponse> GetEvidencePackHistoryAsync(
            GetEvidencePackHistoryRequest request);
    }
}
