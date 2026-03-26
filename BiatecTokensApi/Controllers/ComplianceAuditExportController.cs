using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ComplianceAuditExport;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides scenario-specific compliance audit export APIs for internal compliance teams,
    /// enterprise reviewers, and regulator-facing workflows.
    ///
    /// Each endpoint assembles an authoritative, provenance-backed evidence package for a specific
    /// compliance scenario. The backend is the authoritative source for readiness determination;
    /// clients must never suppress, override, or synthesise the package readiness status.
    ///
    /// Supported scenarios:
    /// <list type="bullet">
    ///   <item><description>Release-readiness sign-off: validates protected sign-off evidence and KYC/AML posture.</description></item>
    ///   <item><description>Onboarding case review: exports KYC/AML case lifecycle and provider-check outcomes.</description></item>
    ///   <item><description>Compliance blocker review: surfaces open and resolved blockers with severity distribution.</description></item>
    ///   <item><description>Approval-history export: captures the complete approval workflow audit trail.</description></item>
    /// </list>
    ///
    /// Readiness semantics are fail-closed:
    /// missing, stale, provider-unavailable, or unverified evidence downgrades the readiness status
    /// explicitly rather than silently passing an incomplete package.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance-audit-export")]
    [Produces("application/json")]
    public class ComplianceAuditExportController : ControllerBase
    {
        private readonly IComplianceAuditExportService _service;
        private readonly ILogger<ComplianceAuditExportController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ComplianceAuditExportController"/>.
        /// </summary>
        public ComplianceAuditExportController(
            IComplianceAuditExportService service,
            ILogger<ComplianceAuditExportController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ── Release readiness sign-off ─────────────────────────────────────────

        /// <summary>
        /// Assembles a release-readiness sign-off audit export package.
        /// </summary>
        /// <param name="request">
        /// Request containing the subject ID, audience profile, optional head reference,
        /// environment label, and evidence filter timestamp.
        /// Supply an IdempotencyKey to enable safe retries without regenerating the package.
        /// </param>
        /// <returns>
        /// Assembled package with release-readiness section, provenance records, blockers,
        /// and fail-closed readiness classification.
        /// </returns>
        /// <remarks>
        /// This endpoint focuses on protected sign-off evidence, KYC/AML posture, and
        /// launch-grade evidence suitability. If any required sign-off evidence is missing,
        /// stale, or non-release-grade, the readiness status is downgraded to Blocked or Stale —
        /// never silently passed as Ready.
        ///
        /// Use the <c>IsRegulatorReady</c> flag for authoritative readiness signal.
        /// Never override or suppress blocker items in a downstream UI.
        /// </remarks>
        [HttpPost("release-readiness")]
        [ProducesResponseType(typeof(ComplianceAuditExportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AssembleReleaseReadiness(
            [FromBody] ReleaseReadinessExportRequest request)
        {
            var correlationId = GetCorrelationId();
            request.CorrelationId ??= correlationId;

            _logger.LogInformation(
                "AssembleReleaseReadiness. SubjectId={SubjectId} HeadRef={HeadRef} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                LoggingHelper.SanitizeLogInput(request.HeadRef),
                LoggingHelper.SanitizeLogInput(correlationId));

            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "SubjectId is required.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }

            try
            {
                var result = await _service.AssembleReleaseReadinessExportAsync(request);

                if (!result.Success)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = result.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = result.ErrorMessage ?? "Release-readiness export assembly failed.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error assembling release-readiness export for subject {SubjectId}",
                    LoggingHelper.SanitizeLogInput(request.SubjectId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An unexpected error occurred while assembling the release-readiness export.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Onboarding case review ─────────────────────────────────────────────

        /// <summary>
        /// Assembles an onboarding case review audit export package.
        /// </summary>
        /// <param name="request">
        /// Request containing the subject ID, audience profile, and optional case ID.
        /// If no case ID is supplied, the most recent onboarding case for the subject is used.
        /// </param>
        /// <returns>
        /// Assembled package with onboarding case section, provider-check outcomes,
        /// reviewer actions, and fail-closed readiness classification.
        /// </returns>
        /// <remarks>
        /// This endpoint focuses on KYC/AML case lifecycle, provider availability,
        /// and reviewer decision history. If the provider is unavailable or the case
        /// is in a non-reviewable state, the readiness is downgraded accordingly.
        ///
        /// Evidence completeness is assessed against required evidence sources for the case.
        /// Missing or stale provider results are surfaced as Critical blockers.
        /// </remarks>
        [HttpPost("onboarding-case-review")]
        [ProducesResponseType(typeof(ComplianceAuditExportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AssembleOnboardingCaseReview(
            [FromBody] OnboardingCaseReviewExportRequest request)
        {
            var correlationId = GetCorrelationId();
            request.CorrelationId ??= correlationId;

            _logger.LogInformation(
                "AssembleOnboardingCaseReview. SubjectId={SubjectId} CaseId={CaseId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                LoggingHelper.SanitizeLogInput(request.CaseId),
                LoggingHelper.SanitizeLogInput(correlationId));

            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "SubjectId is required.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }

            try
            {
                var result = await _service.AssembleOnboardingCaseReviewExportAsync(request);

                if (!result.Success)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = result.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = result.ErrorMessage ?? "Onboarding case review export assembly failed.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error assembling onboarding case review export for subject {SubjectId}",
                    LoggingHelper.SanitizeLogInput(request.SubjectId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An unexpected error occurred while assembling the onboarding case review export.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Compliance blocker review ─────────────────────────────────────────

        /// <summary>
        /// Assembles a compliance blocker review audit export package.
        /// </summary>
        /// <param name="request">
        /// Request containing the subject ID, audience profile, and whether to include
        /// recently resolved blockers for historical completeness.
        /// </param>
        /// <returns>
        /// Assembled package with blocker review section showing severity distribution,
        /// open and resolved blockers with remediation hints, and fail-closed readiness.
        /// </returns>
        /// <remarks>
        /// This endpoint surfaces all open compliance blockers with their severity, category,
        /// and remediation hints. Resolved blockers are included for audit trail completeness
        /// when <c>IncludeResolvedBlockers = true</c> (default).
        ///
        /// The readiness status is downgraded to Blocked when any critical blocker is open.
        /// Advisory and warning blockers downgrade to RequiresReview or PartiallyAvailable.
        /// </remarks>
        [HttpPost("blocker-review")]
        [ProducesResponseType(typeof(ComplianceAuditExportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AssembleBlockerReview(
            [FromBody] ComplianceBlockerReviewExportRequest request)
        {
            var correlationId = GetCorrelationId();
            request.CorrelationId ??= correlationId;

            _logger.LogInformation(
                "AssembleBlockerReview. SubjectId={SubjectId} IncludeResolved={IncludeResolved} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                request.IncludeResolvedBlockers,
                LoggingHelper.SanitizeLogInput(correlationId));

            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "SubjectId is required.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }

            try
            {
                var result = await _service.AssembleBlockerReviewExportAsync(request);

                if (!result.Success)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = result.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = result.ErrorMessage ?? "Blocker review export assembly failed.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error assembling blocker review export for subject {SubjectId}",
                    LoggingHelper.SanitizeLogInput(request.SubjectId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An unexpected error occurred while assembling the blocker review export.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Approval history export ────────────────────────────────────────────

        /// <summary>
        /// Assembles an approval-history audit export package.
        /// </summary>
        /// <param name="request">
        /// Request containing the subject ID, audience profile, and optional decision limit (default 100).
        /// </param>
        /// <returns>
        /// Assembled package with approval-history section showing stage decisions, decision actors,
        /// rationale, workflow completion status, and fail-closed readiness.
        /// </returns>
        /// <remarks>
        /// This endpoint captures the complete approval workflow history in chronological order.
        /// A pending review stage (NeedsMoreEvidence) downgrades readiness to Blocked until resolved.
        /// An absent approval workflow history is surfaced as a Critical blocker.
        ///
        /// Use the <c>IsWorkflowCompleted</c> flag to determine whether the full approval chain
        /// has been completed without pending items.
        /// </remarks>
        [HttpPost("approval-history")]
        [ProducesResponseType(typeof(ComplianceAuditExportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AssembleApprovalHistory(
            [FromBody] ApprovalHistoryExportRequest request)
        {
            var correlationId = GetCorrelationId();
            request.CorrelationId ??= correlationId;

            _logger.LogInformation(
                "AssembleApprovalHistory. SubjectId={SubjectId} DecisionLimit={Limit} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                request.DecisionLimit,
                LoggingHelper.SanitizeLogInput(correlationId));

            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "SubjectId is required.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }

            try
            {
                var result = await _service.AssembleApprovalHistoryExportAsync(request);

                if (!result.Success)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = result.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = result.ErrorMessage ?? "Approval-history export assembly failed.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error assembling approval-history export for subject {SubjectId}",
                    LoggingHelper.SanitizeLogInput(request.SubjectId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An unexpected error occurred while assembling the approval-history export.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Retrieve by export ID ─────────────────────────────────────────────

        /// <summary>
        /// Retrieves a previously assembled compliance audit export package by its export ID.
        /// </summary>
        /// <param name="exportId">Stable export identifier returned from an assembly endpoint.</param>
        /// <returns>The canonical package payload for the specified export.</returns>
        /// <remarks>
        /// Use this endpoint to re-identify, compare, or re-download a previously assembled package.
        /// The response includes the full scenario-specific section and provenance records.
        /// </remarks>
        [HttpGet("{exportId}")]
        [ProducesResponseType(typeof(GetComplianceAuditExportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExport(string exportId)
        {
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "GetExport. ExportId={ExportId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(exportId),
                LoggingHelper.SanitizeLogInput(correlationId));

            if (string.IsNullOrWhiteSpace(exportId))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "ExportId is required.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }

            try
            {
                var result = await _service.GetExportAsync(exportId, correlationId);

                if (!result.Success)
                {
                    if (result.ErrorCode == "NOT_FOUND")
                    {
                        return NotFound(new ApiErrorResponse
                        {
                            Success = false,
                            ErrorCode = ErrorCodes.NOT_FOUND,
                            ErrorMessage = result.ErrorMessage ??
                                $"Export '{LoggingHelper.SanitizeLogInput(exportId)}' was not found.",
                            Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                        });
                    }

                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = result.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = result.ErrorMessage ?? "Failed to retrieve export.",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error retrieving export {ExportId}",
                    LoggingHelper.SanitizeLogInput(exportId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An unexpected error occurred while retrieving the export.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── List exports for subject ──────────────────────────────────────────

        /// <summary>
        /// Lists compliance audit export packages for a subject, most recently assembled first.
        /// </summary>
        /// <param name="subjectId">Subject or issuer identifier.</param>
        /// <param name="scenario">
        /// Optional scenario filter. When omitted, packages for all scenarios are returned.
        /// </param>
        /// <param name="limit">Maximum number of summaries to return (1–100, default 20).</param>
        /// <returns>
        /// Ordered list of export package summaries for the subject, newest first.
        /// Use the <c>ExportId</c> from each summary to retrieve the full package.
        /// </returns>
        /// <remarks>
        /// This endpoint supports append-only history views and package comparison.
        /// The <c>TrackerHistory</c> field on each full package shows the ordered chain of prior exports.
        /// </remarks>
        [HttpGet("subject/{subjectId}")]
        [ProducesResponseType(typeof(ListComplianceAuditExportsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListExports(
            string subjectId,
            [FromQuery] AuditScenario? scenario = null,
            [FromQuery] int limit = 20)
        {
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "ListExports. SubjectId={SubjectId} Scenario={Scenario} Limit={Limit} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(subjectId),
                scenario?.ToString() ?? "all",
                limit,
                LoggingHelper.SanitizeLogInput(correlationId));

            if (string.IsNullOrWhiteSpace(subjectId))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "SubjectId is required.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }

            try
            {
                var result = await _service.ListExportsAsync(subjectId, scenario, limit, correlationId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error listing exports for subject {SubjectId}",
                    LoggingHelper.SanitizeLogInput(subjectId));
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.InternalError,
                    ErrorMessage = "An unexpected error occurred while listing exports.",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        // ── Health ────────────────────────────────────────────────────────────

        /// <summary>Health check for the Compliance Audit Export API.</summary>
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiStatusResponse), StatusCodes.Status200OK)]
        public IActionResult Health()
        {
            return Ok(new ApiStatusResponse
            {
                Status = "Healthy",
                Version = "v1.0",
                Timestamp = DateTime.UtcNow
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string GetCorrelationId()
        {
            if (HttpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var header) &&
                !string.IsNullOrWhiteSpace(header))
                return header.ToString();
            return Guid.NewGuid().ToString();
        }
    }
}
