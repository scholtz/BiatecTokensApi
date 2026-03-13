using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.EnterpriseComplianceReview;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Enterprise compliance review controller providing role-aware reviewer queues,
    /// compliance evidence bundles, structured review decision APIs, audit history,
    /// audit export, and operational diagnostics for enterprise team operations.
    ///
    /// All endpoints are issuer-scoped. Tenant isolation is enforced by the service layer.
    ///
    /// Authorization is role-aware and fail-closed:
    ///   - All operations require the actor to be an active member of the issuer team.
    ///   - Review decisions require ComplianceReviewer, FinanceReviewer, or Admin role.
    ///   - Diagnostics requires ComplianceReviewer or Admin role.
    ///   - Unauthenticated requests return 401; role violations return 403 with operator guidance.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/enterprise-compliance-review")]
    [Produces("application/json")]
    public class EnterpriseComplianceReviewController : ControllerBase
    {
        private readonly IEnterpriseComplianceReviewService _reviewService;
        private readonly ILogger<EnterpriseComplianceReviewController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="EnterpriseComplianceReviewController"/>.
        /// </summary>
        public EnterpriseComplianceReviewController(
            IEnterpriseComplianceReviewService reviewService,
            ILogger<EnterpriseComplianceReviewController> logger)
        {
            _reviewService = reviewService;
            _logger        = logger;
        }

        // ── Review Queue ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the role-aware reviewer queue for the requesting actor.
        ///
        /// Each item in the queue is enriched with:
        ///   - Available capabilities for the actor (Approve, Reject, RequestChanges, etc.)
        ///   - Evidence readiness summary (critical issues, warnings)
        ///   - Evidence-blocked indicator with human-readable explanation
        ///
        /// Queue filtering by role:
        ///   - Operators and Admins see items they created in Prepared or NeedsChanges state.
        ///   - Reviewers, Approvers, and Admins see items in PendingReview.
        ///   - ReadOnlyObservers see all items.
        ///
        /// The frontend should use AvailableCapabilities to determine which action buttons to enable.
        /// IsEvidenceBlocked and EvidenceBlockReason should surface as a warning before allowing approval.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        [HttpGet("{issuerId}/review-queue")]
        [ProducesResponseType(typeof(ReviewQueueResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ReviewQueueResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ReviewQueueResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ReviewQueueResponse), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetReviewQueue(string issuerId)
        {
            var actorId       = GetActorId();
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetReviewQueue. IssuerId={IssuerId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var result = await _reviewService.GetReviewQueueAsync(issuerId, actorId, correlationId);
            if (!result.Success && IsAuthError(result.ErrorCode)) return StatusCode(StatusCodes.Status403Forbidden, result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Evidence Bundle ────────────────────────────────────────────────────

        /// <summary>
        /// Returns a structured compliance evidence bundle for a specific workflow review item.
        ///
        /// The bundle contains:
        ///   - Supporting evidence items with category, source, validation status, and rationale.
        ///   - Detected contradictions between evidence sources, with severity and recommended actions.
        ///   - Missing evidence indicators (blocking and non-blocking).
        ///   - Overall review readiness assessment and summary.
        ///
        /// If IsReviewReady is false, the ReviewReadinessSummary explains what must be resolved.
        /// The frontend should surface this summary to reviewers before enabling the approval action.
        ///
        /// Evidence bundles are generated on demand and reflect the current workflow item state.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item identifier.</param>
        [HttpGet("{issuerId}/review-queue/{workflowId}/evidence")]
        [ProducesResponseType(typeof(ReviewEvidenceBundleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ReviewEvidenceBundleResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ReviewEvidenceBundleResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ReviewEvidenceBundleResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ReviewEvidenceBundleResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEvidenceBundle(string issuerId, string workflowId)
        {
            var actorId       = GetActorId();
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetEvidenceBundle. IssuerId={IssuerId} WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(workflowId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var result = await _reviewService.GetEvidenceBundleAsync(issuerId, workflowId, actorId, correlationId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            if (!result.Success && IsAuthError(result.ErrorCode)) return StatusCode(StatusCodes.Status403Forbidden, result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Review Decisions ───────────────────────────────────────────────────

        /// <summary>
        /// Submits a structured compliance review decision for a workflow item.
        ///
        /// Supported decision types:
        ///   - Approve: records approval with optional rationale. Requires ComplianceReviewer, FinanceReviewer, or Admin role.
        ///   - Reject: records rejection with mandatory rationale. Requires same roles.
        ///   - RequestChanges: returns item to NeedsChanges with mandatory change description.
        ///
        /// Authorization:
        ///   - Role check: ComplianceReviewer, FinanceReviewer, or Admin required.
        ///   - State check: item must be in PendingReview; other states fail with INVALID_STATE_TRANSITION.
        ///   - Evidence check: Approve with unacknowledged critical issues requires AcknowledgesOpenIssues=true.
        ///
        /// All decisions are persisted in the workflow audit trail with the actor's identity,
        /// rationale, evidence references, and correlation ID for audit reconstruction.
        ///
        /// On success, returns the updated workflow item and a DecisionId for linkage in audit records.
        /// On failure, returns a structured error payload with OperatorGuidance context.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item to decide on.</param>
        /// <param name="request">Decision type, rationale, evidence references, and acknowledgement.</param>
        [HttpPost("{issuerId}/review-queue/{workflowId}/decision")]
        [ProducesResponseType(typeof(SubmitReviewDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(SubmitReviewDecisionResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(SubmitReviewDecisionResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(SubmitReviewDecisionResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(SubmitReviewDecisionResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SubmitReviewDecision(
            string issuerId, string workflowId, [FromBody] SubmitReviewDecisionRequest request)
        {
            var actorId       = GetActorId();
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "SubmitReviewDecision. IssuerId={IssuerId} WorkflowId={WorkflowId} Actor={Actor} Decision={Decision} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(workflowId),
                LoggingHelper.SanitizeLogInput(actorId),
                request?.DecisionType,
                LoggingHelper.SanitizeLogInput(correlationId));

            var result = await _reviewService.SubmitReviewDecisionAsync(issuerId, workflowId, request!, actorId, correlationId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            if (!result.Success && IsAuthError(result.ErrorCode)) return StatusCode(StatusCodes.Status403Forbidden, result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Audit History ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns the enriched audit history for a specific workflow item.
        ///
        /// Each entry includes:
        ///   - State transition (from/to)
        ///   - Actor identity, display name, and role at time of action
        ///   - Human-readable action description
        ///   - Structured rationale and evidence references
        ///   - Correlation ID for distributed tracing
        ///
        /// Suitable for compliance audit reconstruction, regulatory review, and internal
        /// investigations. Entries are in chronological order.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item identifier.</param>
        [HttpGet("{issuerId}/review-queue/{workflowId}/audit-history")]
        [ProducesResponseType(typeof(ReviewAuditHistoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ReviewAuditHistoryResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ReviewAuditHistoryResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ReviewAuditHistoryResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ReviewAuditHistoryResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAuditHistory(string issuerId, string workflowId)
        {
            var actorId       = GetActorId();
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetAuditHistory. IssuerId={IssuerId} WorkflowId={WorkflowId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(workflowId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var result = await _reviewService.GetAuditHistoryAsync(issuerId, workflowId, actorId, correlationId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            if (!result.Success && IsAuthError(result.ErrorCode)) return StatusCode(StatusCodes.Status403Forbidden, result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Audit Export ───────────────────────────────────────────────────────

        /// <summary>
        /// Exports workflow review decisions for an issuer in a structured format.
        ///
        /// Output is designed for:
        ///   - Regulators and auditors (structured timestamps, actor metadata, decision rationale)
        ///   - CSV/JSON export by the frontend
        ///   - Internal compliance review and evidence bundles
        ///
        /// Supports filtering by time range, workflow state, item type, and actor.
        /// Maximum 500 records per export (configurable via MaxItems in the request).
        ///
        /// Each record includes:
        ///   - Workflow item metadata (ID, type, title, state, external reference)
        ///   - Creator, approver, and rejection actor with timestamps
        ///   - Rejection/change reasons for accountability
        ///   - Audit entry count for reconstruction depth indication
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="request">Export criteria including time range, state, type, and actor filters.</param>
        [HttpPost("{issuerId}/audit-export")]
        [ProducesResponseType(typeof(ReviewAuditExportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ReviewAuditExportResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ReviewAuditExportResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ReviewAuditExportResponse), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ExportAudit(string issuerId, [FromBody] ReviewAuditExportRequest request)
        {
            var actorId       = GetActorId();
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "ExportAudit. IssuerId={IssuerId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var result = await _reviewService.ExportAuditAsync(issuerId, request ?? new ReviewAuditExportRequest(), actorId, correlationId);
            if (!result.Success && IsAuthError(result.ErrorCode)) return StatusCode(StatusCodes.Status403Forbidden, result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Diagnostics ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns structured operational diagnostics for an issuer's compliance review workflows.
        ///
        /// Surfaces the following event categories for operator visibility:
        ///   - Authorization denials (which actors are being blocked and why)
        ///   - Invalid workflow state transitions (attempted)
        ///   - Missing evidence situations (blocking or non-blocking)
        ///   - Evidence contradictions detected during review bundle assembly
        ///   - Stale items that have not progressed in an extended period
        ///
        /// Each event includes operator guidance to resolve the issue.
        ///
        /// Authorization: ComplianceReviewer or Admin role required.
        /// Returns 403 with INSUFFICIENT_ROLE error code and role guidance on denial.
        ///
        /// Not intended for end-user display — designed for support staff and platform operators.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        [HttpGet("{issuerId}/diagnostics")]
        [ProducesResponseType(typeof(ReviewDiagnosticsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ReviewDiagnosticsResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ReviewDiagnosticsResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ReviewDiagnosticsResponse), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetDiagnostics(string issuerId)
        {
            var actorId       = GetActorId();
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetDiagnostics. IssuerId={IssuerId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var result = await _reviewService.GetDiagnosticsAsync(issuerId, actorId, correlationId);
            if (!result.Success && IsAuthError(result.ErrorCode)) return StatusCode(StatusCodes.Status403Forbidden, result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Private Helpers ────────────────────────────────────────────────────

        private string GetActorId() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "anonymous";

        private string GetCorrelationId() =>
            HttpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var val) && !string.IsNullOrWhiteSpace(val)
                ? val.ToString()
                : Guid.NewGuid().ToString();

        private static bool IsAuthError(string? errorCode) =>
            errorCode == ErrorCodes.UNAUTHORIZED || errorCode == "INSUFFICIENT_ROLE";
    }
}
