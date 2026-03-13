using BiatecTokensApi.Models.EnterpriseComplianceReview;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for enterprise compliance review operations.
    ///
    /// Provides role-aware reviewer queues, compliance evidence bundles, structured review
    /// decision recording, audit history, audit export, and operational diagnostics.
    ///
    /// Authorization is enforced at every operation:
    ///   - Actors must be active members of the issuer team.
    ///   - Role-specific capabilities (Approve, Reject, etc.) require the appropriate role.
    ///   - Authorization failures are fail-closed with explicit operator guidance.
    ///
    /// Tenant isolation is enforced at all times: one issuer cannot access another issuer's
    /// review data, evidence bundles, or diagnostics.
    /// </summary>
    public interface IEnterpriseComplianceReviewService
    {
        // ── Review Queue ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the role-filtered reviewer queue for an issuer.
        /// Items are enriched with the actor's available capabilities and evidence readiness summary.
        ///
        /// - Operators/Admins see items they created that need action (NeedsChanges, Prepared).
        /// - Reviewers/Approvers/Admins see items pending their review (PendingReview).
        /// - ReadOnlyObserver/all roles see all items they are permitted to view.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="actorId">Actor requesting the queue.</param>
        /// <param name="correlationId">Correlation ID for tracing.</param>
        Task<ReviewQueueResponse> GetReviewQueueAsync(string issuerId, string actorId, string correlationId);

        // ── Evidence Bundle ────────────────────────────────────────────────────

        /// <summary>
        /// Assembles a compliance evidence bundle for a specific workflow review item.
        ///
        /// The bundle includes:
        ///   - Supporting evidence items with validation status and rationale.
        ///   - Detected contradictions between evidence sources.
        ///   - Missing evidence indicators (blocking and non-blocking).
        ///   - Overall review readiness assessment.
        ///
        /// Returns an explicit "incomplete review" state when blocking issues exist,
        /// rather than implying readiness.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item to assemble evidence for.</param>
        /// <param name="actorId">Actor requesting the evidence bundle.</param>
        /// <param name="correlationId">Correlation ID for tracing.</param>
        Task<ReviewEvidenceBundleResponse> GetEvidenceBundleAsync(string issuerId, string workflowId, string actorId, string correlationId);

        // ── Review Decisions ───────────────────────────────────────────────────

        /// <summary>
        /// Submits a structured review decision (Approve, Reject, or RequestChanges) for a workflow item.
        ///
        /// Enforces role-based authorization:
        ///   - Approve/Reject/RequestChanges: requires ComplianceReviewer, FinanceReviewer, or Admin role.
        ///
        /// Validates state machine rules:
        ///   - The item must be in PendingReview state.
        ///   - Invalid transitions fail loudly with a structured error.
        ///
        /// Approvals with open critical contradictions require explicit acknowledgement.
        /// Records the decision in the workflow audit trail with structured rationale and evidence references.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item to decide on.</param>
        /// <param name="request">The decision with rationale and evidence references.</param>
        /// <param name="actorId">Actor submitting the decision.</param>
        /// <param name="correlationId">Correlation ID for tracing.</param>
        Task<SubmitReviewDecisionResponse> SubmitReviewDecisionAsync(string issuerId, string workflowId, SubmitReviewDecisionRequest request, string actorId, string correlationId);

        // ── Audit History ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns the full enriched audit history for a workflow item.
        ///
        /// Each entry includes the actor's role at the time of the action, structured rationale,
        /// evidence references, and correlation IDs for distributed tracing.
        /// Suitable for audit reconstruction and compliance reporting.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item to retrieve history for.</param>
        /// <param name="actorId">Actor requesting the history.</param>
        /// <param name="correlationId">Correlation ID for tracing.</param>
        Task<ReviewAuditHistoryResponse> GetAuditHistoryAsync(string issuerId, string workflowId, string actorId, string correlationId);

        // ── Audit Export ───────────────────────────────────────────────────────

        /// <summary>
        /// Exports workflow review decisions for an issuer in a structured format.
        ///
        /// Supports filtering by time range, state, item type, and actor.
        /// Output is structured for regulatory review, auditors, or internal compliance reporting.
        /// Suitable as input for CSV or JSON export from the frontend.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="request">Export criteria and filters.</param>
        /// <param name="actorId">Actor requesting the export.</param>
        /// <param name="correlationId">Correlation ID for tracing.</param>
        Task<ReviewAuditExportResponse> ExportAuditAsync(string issuerId, ReviewAuditExportRequest request, string actorId, string correlationId);

        // ── Diagnostics ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns structured diagnostics for an issuer's compliance review operations.
        ///
        /// Surfaces:
        ///   - Authorization denial events.
        ///   - Invalid workflow transition attempts.
        ///   - Missing evidence situations.
        ///   - Evidence contradiction detections.
        ///   - Stale items that have not progressed in an extended period.
        ///
        /// Designed for operator visibility and support investigation, not for end-user display.
        /// Requires Admin or ComplianceReviewer role.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="actorId">Actor requesting diagnostics.</param>
        /// <param name="correlationId">Correlation ID for tracing.</param>
        Task<ReviewDiagnosticsResponse> GetDiagnosticsAsync(string issuerId, string actorId, string correlationId);
    }
}
