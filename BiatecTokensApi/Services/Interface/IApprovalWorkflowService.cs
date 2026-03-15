using BiatecTokensApi.Models.ApprovalWorkflow;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for enterprise approval workflow and release-evidence APIs.
    ///
    /// Provides deterministic posture calculation, stage decision recording,
    /// evidence readiness evaluation, and tamper-evident audit history for
    /// multi-stage enterprise release approval pipelines.
    /// </summary>
    public interface IApprovalWorkflowService
    {
        /// <summary>
        /// Returns the full approval workflow state for the given release package,
        /// including all stage statuses, active blockers, evidence readiness summary,
        /// active owner domain, and overall release posture with rationale.
        /// </summary>
        /// <param name="releasePackageId">Unique identifier of the release package.</param>
        /// <param name="actorId">Identity of the requesting actor (for audit).</param>
        /// <param name="correlationId">Distributed tracing correlation identifier.</param>
        Task<ApprovalWorkflowStateResponse> GetApprovalWorkflowStateAsync(
            string releasePackageId, string actorId, string correlationId);

        /// <summary>
        /// Submits an approval stage decision (Approved, Rejected, Blocked, NeedsFollowUp)
        /// for a specific stage within the given release package.
        ///
        /// Returns the updated stage record, new release posture, and a DecisionId for
        /// cross-referencing in audit logs.
        /// </summary>
        /// <param name="releasePackageId">Unique identifier of the release package.</param>
        /// <param name="request">Stage, decision, note, and optional evidence acknowledgements.</param>
        /// <param name="actorId">Identity of the actor submitting the decision.</param>
        /// <param name="correlationId">Distributed tracing correlation identifier.</param>
        Task<SubmitStageDecisionResponse> SubmitStageDecisionAsync(
            string releasePackageId, SubmitStageDecisionRequest request,
            string actorId, string correlationId);

        /// <summary>
        /// Returns evidence readiness summary for the given release package,
        /// including per-item freshness, counts by category, and overall readiness.
        /// </summary>
        /// <param name="releasePackageId">Unique identifier of the release package.</param>
        /// <param name="actorId">Identity of the requesting actor (for audit).</param>
        /// <param name="correlationId">Distributed tracing correlation identifier.</param>
        Task<ReleaseEvidenceSummaryResponse> GetReleaseEvidenceSummaryAsync(
            string releasePackageId, string actorId, string correlationId);

        /// <summary>
        /// Returns the audit history for the given release package,
        /// ordered newest-first up to the repository maximum.
        /// </summary>
        /// <param name="releasePackageId">Unique identifier of the release package.</param>
        /// <param name="actorId">Identity of the requesting actor (for audit).</param>
        /// <param name="correlationId">Distributed tracing correlation identifier.</param>
        Task<ApprovalAuditHistoryResponse> GetApprovalAuditHistoryAsync(
            string releasePackageId, string actorId, string correlationId);
    }
}
