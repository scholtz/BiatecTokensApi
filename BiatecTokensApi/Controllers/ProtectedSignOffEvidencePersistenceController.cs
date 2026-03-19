using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Protected sign-off evidence persistence and approval-webhook parity API.
    /// </summary>
    /// <remarks>
    /// This API provides the authoritative backend store for approval webhook outcomes,
    /// protected sign-off evidence packs, and aggregated release-readiness status for
    /// the current release head. It allows the frontend and product owner to rely on a
    /// single source of truth for protected sign-off governance instead of stitching
    /// together raw CI metadata.
    ///
    /// Key capabilities:
    /// - **Approval webhook persistence**: Record incoming approval and escalation webhooks
    ///   with full audit trail (payload hash, actor, correlation ID, validity flag).
    /// - **Evidence pack persistence**: Capture and store sign-off evidence packs for
    ///   specific head refs with freshness tracking, provider-backed flags, and content hash.
    /// - **Release readiness**: Single authoritative endpoint returning complete, stale,
    ///   missing, or head-mismatch evidence state with ordered blockers and operator guidance.
    /// - **History queries**: Queryable history for approval webhooks and evidence packs.
    /// - **Fail-closed semantics**: RequireApprovalWebhook and RequireReleaseGrade guards
    ///   prevent partial evidence from reaching release-grade status.
    ///
    /// Endpoints:
    /// - `POST /webhooks/approval`              – Record an incoming approval or escalation webhook.
    /// - `POST /evidence`                        – Persist a sign-off evidence pack for a head ref.
    /// - `POST /release-readiness`               – Evaluate and return aggregated release readiness.
    /// - `GET  /webhooks/approval/history`       – Query approval webhook history.
    /// - `GET  /evidence/history`                – Query evidence pack history.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/protected-signoff-evidence")]
    [Produces("application/json")]
    public class ProtectedSignOffEvidencePersistenceController : ControllerBase
    {
        private readonly IProtectedSignOffEvidencePersistenceService _service;
        private readonly ILogger<ProtectedSignOffEvidencePersistenceController> _logger;

        /// <summary>
        /// Initialises the controller.
        /// </summary>
        public ProtectedSignOffEvidencePersistenceController(
            IProtectedSignOffEvidencePersistenceService service,
            ILogger<ProtectedSignOffEvidencePersistenceController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ── Record Approval Webhook ───────────────────────────────────────────

        /// <summary>
        /// Records an incoming approval or escalation webhook outcome and persists it
        /// against the specified compliance case and head ref.
        /// </summary>
        /// <remarks>
        /// Malformed payloads are recorded with <c>Malformed</c> outcome rather than silently
        /// dropped, enabling operators to diagnose webhook delivery failures. The response
        /// includes a payload hash and validity flag for integrity verification.
        ///
        /// This endpoint is the primary ingestion point for approval and escalation
        /// webhook events from CI/CD systems, GitHub Actions protected environments, and
        /// compliance review portals.
        /// </remarks>
        [HttpPost("webhooks/approval")]
        [ProducesResponseType(typeof(RecordApprovalWebhookResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RecordApprovalWebhookResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RecordApprovalWebhook([FromBody] RecordApprovalWebhookRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "RecordApprovalWebhook. CaseId={CaseId} HeadRef={HeadRef} Outcome={Outcome} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(request?.CaseId),
                LoggingHelper.SanitizeLogInput(request?.HeadRef),
                request?.Outcome.ToString() ?? "null",
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.RecordApprovalWebhookAsync(request!, actorId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Persist Sign-Off Evidence ─────────────────────────────────────────

        /// <summary>
        /// Persists a protected sign-off evidence pack for the specified head ref,
        /// capturing the current state of all required evidence items.
        /// </summary>
        /// <remarks>
        /// When <c>RequireReleaseGrade</c> is true, the call fails if the evidence pack
        /// cannot be marked as release-grade. When <c>RequireApprovalWebhook</c> is true,
        /// the call fails if no approved webhook has been received for this head ref.
        ///
        /// The resulting pack includes a content hash, freshness expiry, provider-backed
        /// flag, and a link to the associated approval webhook record.
        /// </remarks>
        [HttpPost("evidence")]
        [ProducesResponseType(typeof(PersistSignOffEvidenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(PersistSignOffEvidenceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> PersistSignOffEvidence([FromBody] PersistSignOffEvidenceRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "PersistSignOffEvidence. HeadRef={HeadRef} CaseId={CaseId} RequireReleaseGrade={RequireReleaseGrade} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(request?.HeadRef),
                LoggingHelper.SanitizeLogInput(request?.CaseId),
                request?.RequireReleaseGrade.ToString() ?? "null",
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _service.PersistSignOffEvidenceAsync(request!, actorId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Get Release Readiness ─────────────────────────────────────────────

        /// <summary>
        /// Evaluates and returns the aggregated release-readiness status for the
        /// specified head ref.
        /// </summary>
        /// <remarks>
        /// This is the primary endpoint for the release evidence center. It returns a
        /// single authoritative object that the frontend can consume without stitching
        /// together multiple weak signals. The response includes:
        /// <list type="bullet">
        ///   <item>Aggregated readiness status (Ready / Pending / Blocked / Stale / Indeterminate).</item>
        ///   <item>Evidence freshness classification (Complete / Partial / Stale / Unavailable / HeadMismatch).</item>
        ///   <item>Approval webhook presence and latest record.</item>
        ///   <item>Ordered blockers with category, machine-readable code, and remediation hint.</item>
        ///   <item>Top-level operator guidance for next actions.</item>
        /// </list>
        ///
        /// The method is fail-closed: it reports Blocked when required approvals or evidence
        /// are absent, and Stale when evidence has expired or was captured against a different head.
        /// </remarks>
        [HttpPost("release-readiness")]
        [ProducesResponseType(typeof(GetSignOffReleaseReadinessResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetSignOffReleaseReadinessResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetReleaseReadiness([FromBody] GetSignOffReleaseReadinessRequest request)
        {
            _logger.LogInformation(
                "GetReleaseReadiness. HeadRef={HeadRef} CaseId={CaseId}",
                LoggingHelper.SanitizeLogInput(request?.HeadRef),
                LoggingHelper.SanitizeLogInput(request?.CaseId));

            var result = await _service.GetReleaseReadinessAsync(request!);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Approval Webhook History ──────────────────────────────────────────

        /// <summary>
        /// Queries the approval webhook history filtered by optional case ID and head ref.
        /// </summary>
        /// <remarks>
        /// Returns records ordered newest-first. Use <c>MaxRecords</c> to limit the result
        /// set. All historical records including malformed, timed-out, and denied outcomes
        /// are returned for a complete audit trail.
        /// </remarks>
        [HttpGet("webhooks/approval/history")]
        [ProducesResponseType(typeof(GetApprovalWebhookHistoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetApprovalWebhookHistory(
            [FromQuery] string? caseId,
            [FromQuery] string? headRef,
            [FromQuery] int maxRecords = 50)
        {
            _logger.LogInformation(
                "GetApprovalWebhookHistory. CaseId={CaseId} HeadRef={HeadRef} MaxRecords={MaxRecords}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(headRef),
                maxRecords);

            var result = await _service.GetApprovalWebhookHistoryAsync(new GetApprovalWebhookHistoryRequest
            {
                CaseId = caseId,
                HeadRef = headRef,
                MaxRecords = maxRecords
            });

            return Ok(result);
        }

        // ── Evidence Pack History ─────────────────────────────────────────────

        /// <summary>
        /// Queries the evidence pack history filtered by optional head ref and case ID.
        /// </summary>
        /// <remarks>
        /// Returns packs ordered newest-first. Use <c>MaxRecords</c> to limit the result
        /// set. Each pack includes freshness status, content hash, and approval webhook
        /// reference.
        /// </remarks>
        [HttpGet("evidence/history")]
        [ProducesResponseType(typeof(GetEvidencePackHistoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetEvidencePackHistory(
            [FromQuery] string? headRef,
            [FromQuery] string? caseId,
            [FromQuery] int maxRecords = 50)
        {
            _logger.LogInformation(
                "GetEvidencePackHistory. HeadRef={HeadRef} CaseId={CaseId} MaxRecords={MaxRecords}",
                LoggingHelper.SanitizeLogInput(headRef),
                LoggingHelper.SanitizeLogInput(caseId),
                maxRecords);

            var result = await _service.GetEvidencePackHistoryAsync(new GetEvidencePackHistoryRequest
            {
                HeadRef = headRef,
                CaseId = caseId,
                MaxRecords = maxRecords
            });

            return Ok(result);
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        private string GetActorId() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "anonymous";
    }
}
