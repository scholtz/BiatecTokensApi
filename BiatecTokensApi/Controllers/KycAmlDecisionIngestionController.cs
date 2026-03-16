using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.KycAmlDecisionIngestion;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provider-agnostic KYC/AML decision ingestion and evidence retention API.
    /// </summary>
    /// <remarks>
    /// Implements the provider-agnostic compliance decision backbone:
    ///
    /// - **Normalised ingestion**: Accepts decisions from any KYC/AML provider or manual review
    ///   flow using a consistent normalised model.  Provider-specific payload details are
    ///   retained for audit/support, but business-rule logic operates exclusively on the
    ///   normalised representation.
    ///
    /// - **Evidence retention**: Every ingested decision retains provenance metadata
    ///   (provider, timestamps, reference IDs, reviewer identity), evidence artefacts, and an
    ///   immutable timeline so auditors can reconstruct each eligibility determination.
    ///
    /// - **Fail-closed readiness**: Missing evidence, expired evidence, provider-unavailable,
    ///   contradictory or rejected decisions always yield a non-Ready state.  The system
    ///   never silently advances a launch when compliance posture cannot be confirmed.
    ///
    /// - **Explicit blockers**: Blockers and advisories are returned in a structured,
    ///   frontend-consumable form so the UI never needs to infer business logic from raw fields.
    ///
    /// - **Cohort readiness**: Multiple subjects can be grouped into a cohort and their
    ///   readiness aggregated into a single launch-readiness summary.
    ///
    /// Endpoints:
    /// - `POST   /decisions`                          – Ingest a normalised compliance decision.
    /// - `GET    /decisions/{decisionId}`             – Retrieve a specific decision.
    /// - `GET    /subjects/{subjectId}/decisions`     – List all decisions for a subject.
    /// - `GET    /subjects/{subjectId}/timeline`      – Merged event timeline for a subject.
    /// - `GET    /subjects/{subjectId}/blockers`      – Current blockers and advisories.
    /// - `GET    /subjects/{subjectId}/readiness`     – Aggregated launch readiness.
    /// - `POST   /cohorts`                            – Create or update a cohort.
    /// - `GET    /cohorts/{cohortId}/readiness`       – Cohort-level launch readiness.
    /// - `POST   /decisions/{decisionId}/notes`       – Append a reviewer note.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/kyc-aml-ingestion")]
    [Produces("application/json")]
    public class KycAmlDecisionIngestionController : ControllerBase
    {
        private readonly IKycAmlDecisionIngestionService _service;
        private readonly ILogger<KycAmlDecisionIngestionController> _logger;

        /// <summary>
        /// Initialises a new instance of <see cref="KycAmlDecisionIngestionController"/>.
        /// </summary>
        public KycAmlDecisionIngestionController(
            IKycAmlDecisionIngestionService service,
            ILogger<KycAmlDecisionIngestionController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ── Helper ────────────────────────────────────────────────────────────

        private string ActorId =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "unknown";

        private string CorrelationId =>
            HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        // ── Decision ingestion ────────────────────────────────────────────────

        /// <summary>
        /// Ingest a normalised compliance decision from any provider or manual review flow.
        /// </summary>
        /// <param name="request">The normalised decision request.</param>
        /// <returns>
        /// The created (or idempotent-replayed) decision record, including its stable
        /// <c>decisionId</c>, normalised status, evidence artefacts, and timeline.
        /// </returns>
        /// <remarks>
        /// The ingestion boundary translates provider-specific payload fields into the
        /// normalised <see cref="NormalizedIngestionStatus"/> model.  The caller must
        /// perform this mapping before calling this endpoint.  Provider-specific raw fields
        /// (e.g., <c>providerRawStatus</c>, <c>providerReferenceId</c>) are retained
        /// for audit and support purposes but are never used in business-rule logic.
        ///
        /// **Idempotency**: Supply an <c>idempotencyKey</c> to ensure repeated calls with
        /// the same key return the original record without creating duplicates.  The default
        /// key is derived from <c>subjectId + contextId + kind + provider + providerReferenceId</c>.
        ///
        /// **Fail-closed**: Ingesting a decision with status
        /// <c>ProviderUnavailable</c>, <c>Error</c>, or <c>Rejected</c> will immediately
        /// block the subject's readiness state.
        /// </remarks>
        [HttpPost("decisions")]
        [ProducesResponseType(typeof(IngestProviderDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> IngestDecision([FromBody] IngestProviderDecisionRequest request)
        {
            if (request == null)
                return BadRequest(new ApiErrorResponse { ErrorMessage = "Request body is required.", ErrorCode = "MISSING_REQUEST_BODY" });

            var correlationId = CorrelationId;
            _logger.LogInformation(
                "Decision ingestion requested. SubjectId={SubjectId} Kind={Kind} Provider={Provider} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                request.Kind,
                request.Provider,
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.IngestDecisionAsync(request, ActorId, correlationId);

            if (!response.Success)
                return BadRequest(new ApiErrorResponse { ErrorMessage = response.ErrorMessage ?? string.Empty, ErrorCode = response.ErrorCode ?? string.Empty });

            return Ok(response);
        }

        // ── Decision retrieval ────────────────────────────────────────────────

        /// <summary>
        /// Retrieve a specific ingested decision record by its stable decision ID.
        /// </summary>
        /// <param name="decisionId">The decision identifier returned at ingestion time.</param>
        /// <returns>The full decision record including evidence artefacts and timeline.</returns>
        [HttpGet("decisions/{decisionId}")]
        [ProducesResponseType(typeof(GetIngestionDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDecision([FromRoute] string decisionId)
        {
            var correlationId = CorrelationId;
            _logger.LogInformation(
                "Decision retrieval requested. DecisionId={DecisionId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(decisionId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.GetDecisionAsync(decisionId, correlationId);

            if (!response.Success)
                return NotFound(new ApiErrorResponse { ErrorMessage = response.ErrorMessage ?? string.Empty, ErrorCode = response.ErrorCode ?? string.Empty });

            return Ok(response);
        }

        // ── Subject decisions ─────────────────────────────────────────────────

        /// <summary>
        /// List all ingested compliance decisions for a subject, most recent first.
        /// </summary>
        /// <param name="subjectId">The subject identifier (investor, entity, address).</param>
        /// <returns>Ordered list of all decision records for the subject.</returns>
        [HttpGet("subjects/{subjectId}/decisions")]
        [ProducesResponseType(typeof(ListSubjectDecisionsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ListSubjectDecisions([FromRoute] string subjectId)
        {
            var correlationId = CorrelationId;
            _logger.LogInformation(
                "Subject decisions list requested. SubjectId={SubjectId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(subjectId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.ListSubjectDecisionsAsync(subjectId, correlationId);
            return Ok(response);
        }

        // ── Subject timeline ──────────────────────────────────────────────────

        /// <summary>
        /// Get the merged compliance event timeline for a subject across all their decisions.
        /// </summary>
        /// <param name="subjectId">The subject identifier.</param>
        /// <returns>
        /// Chronological (most-recent first) list of events including ingestion, status changes,
        /// evidence expiry, and reviewer notes.
        /// </returns>
        /// <remarks>
        /// The timeline merges events from all ingested decisions for the subject into a
        /// single ordered view suitable for audit trail and approval handoff reporting.
        /// </remarks>
        [HttpGet("subjects/{subjectId}/timeline")]
        [ProducesResponseType(typeof(GetSubjectTimelineResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSubjectTimeline([FromRoute] string subjectId)
        {
            var correlationId = CorrelationId;
            _logger.LogInformation(
                "Subject timeline requested. SubjectId={SubjectId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(subjectId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.GetSubjectTimelineAsync(subjectId, correlationId);
            return Ok(response);
        }

        // ── Subject blockers ──────────────────────────────────────────────────

        /// <summary>
        /// Get the current explicit blockers and advisories for a subject.
        /// </summary>
        /// <param name="subjectId">The subject identifier.</param>
        /// <returns>
        /// Structured list of hard blockers (preventing launch) and advisory issues, together
        /// with the aggregated readiness state.
        /// </returns>
        /// <remarks>
        /// Blockers are derived by applying fail-closed readiness rules to the subject's
        /// ingested decisions.  The frontend should use this endpoint to display meaningful
        /// compliance status badges and remediation guidance without reconstructing business
        /// logic from raw decision fields.
        /// </remarks>
        [HttpGet("subjects/{subjectId}/blockers")]
        [ProducesResponseType(typeof(GetSubjectBlockersResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSubjectBlockers([FromRoute] string subjectId)
        {
            var correlationId = CorrelationId;
            _logger.LogInformation(
                "Subject blockers requested. SubjectId={SubjectId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(subjectId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.GetSubjectBlockersAsync(subjectId, correlationId);
            return Ok(response);
        }

        // ── Subject readiness ─────────────────────────────────────────────────

        /// <summary>
        /// Compute aggregated launch readiness for a subject from their ingested compliance decisions.
        /// </summary>
        /// <param name="subjectId">The subject identifier.</param>
        /// <returns>
        /// Full readiness summary including state, blockers, advisories, per-kind check summary,
        /// evidence expiry, and computation timestamp.
        /// </returns>
        /// <remarks>
        /// **Readiness states**:
        /// - `Ready` – All required checks approved; launch is permitted.
        /// - `Blocked` – Hard blockers are present; launch is prohibited.
        /// - `PendingReview` – Manual review is in progress; readiness not yet determinable.
        /// - `AtRisk` – Non-critical issues; launch may proceed with acknowledged risk.
        /// - `Stale` – Evidence has expired; checks must be renewed.
        /// - `EvidenceMissing` – No decisions have been ingested; fail-closed.
        /// - `Unknown` – Readiness has not been evaluated.
        ///
        /// The evaluation is **fail-closed**: the system returns a non-Ready state whenever
        /// evidence is missing, expired, contradictory, or from an unavailable provider.
        /// </remarks>
        [HttpGet("subjects/{subjectId}/readiness")]
        [ProducesResponseType(typeof(GetSubjectReadinessResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSubjectReadiness([FromRoute] string subjectId)
        {
            var correlationId = CorrelationId;
            _logger.LogInformation(
                "Subject readiness requested. SubjectId={SubjectId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(subjectId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.GetSubjectReadinessAsync(subjectId, correlationId);
            return Ok(response);
        }

        // ── Cohort management ─────────────────────────────────────────────────

        /// <summary>
        /// Create or update a compliance cohort (a named group of subjects).
        /// </summary>
        /// <param name="request">Cohort upsert request including subject IDs to add.</param>
        /// <returns>Confirmation of the upsert with the total subject count.</returns>
        /// <remarks>
        /// Cohorts allow cohort-level launch readiness to be computed across a group of
        /// investors or entities associated with a token launch.  Calling this endpoint
        /// with an existing <c>cohortId</c> adds the specified subjects to the cohort
        /// without removing existing members.
        /// </remarks>
        [HttpPost("cohorts")]
        [ProducesResponseType(typeof(UpsertCohortResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpsertCohort([FromBody] UpsertCohortRequest request)
        {
            if (request == null)
                return BadRequest(new ApiErrorResponse { ErrorMessage = "Request body is required.", ErrorCode = "MISSING_REQUEST_BODY" });

            var correlationId = CorrelationId;
            _logger.LogInformation(
                "Cohort upsert requested. CohortId={CohortId} SubjectCount={SubjectCount} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.CohortId),
                request.SubjectIds.Count,
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.UpsertCohortAsync(request, correlationId);

            if (!response.Success)
                return BadRequest(new ApiErrorResponse { ErrorMessage = response.ErrorMessage ?? string.Empty, ErrorCode = response.ErrorCode ?? string.Empty });

            return Ok(response);
        }

        // ── Cohort readiness ──────────────────────────────────────────────────

        /// <summary>
        /// Compute cohort-level launch readiness by aggregating readiness for all subjects in the cohort.
        /// </summary>
        /// <param name="cohortId">The cohort identifier.</param>
        /// <returns>
        /// Per-subject and aggregate cohort readiness including overall state, subject counts,
        /// and cohort-level blockers.
        /// </returns>
        /// <remarks>
        /// The cohort readiness state is the most severe state across all members (fail-closed).
        /// A single blocked subject prevents the cohort from being Ready.
        ///
        /// **Cohort states** follow the same semantics as subject readiness states.
        /// </remarks>
        [HttpGet("cohorts/{cohortId}/readiness")]
        [ProducesResponseType(typeof(GetCohortReadinessResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCohortReadiness([FromRoute] string cohortId)
        {
            var correlationId = CorrelationId;
            _logger.LogInformation(
                "Cohort readiness requested. CohortId={CohortId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(cohortId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.GetCohortReadinessAsync(cohortId, correlationId);

            if (!response.Success)
                return NotFound(new ApiErrorResponse { ErrorMessage = response.ErrorMessage ?? string.Empty, ErrorCode = response.ErrorCode ?? string.Empty });

            return Ok(response);
        }

        // ── Reviewer notes ────────────────────────────────────────────────────

        /// <summary>
        /// Append a reviewer note to an existing ingested decision record.
        /// </summary>
        /// <param name="decisionId">The decision to annotate.</param>
        /// <param name="request">The note content and optional evidence references.</param>
        /// <returns>The created reviewer note.</returns>
        /// <remarks>
        /// Reviewer notes allow human operators to annotate compliance decisions with
        /// free-text observations and structured evidence references.  Notes are appended
        /// to the decision's immutable timeline so they appear in audit exports and
        /// approval handoff reports.
        /// </remarks>
        [HttpPost("decisions/{decisionId}/notes")]
        [ProducesResponseType(typeof(AppendIngestionReviewerNoteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AppendReviewerNote(
            [FromRoute] string decisionId,
            [FromBody] AppendIngestionReviewerNoteRequest request)
        {
            if (request == null)
                return BadRequest(new ApiErrorResponse { ErrorMessage = "Request body is required.", ErrorCode = "MISSING_REQUEST_BODY" });

            var correlationId = CorrelationId;
            _logger.LogInformation(
                "Reviewer note append requested. DecisionId={DecisionId} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(decisionId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.AppendReviewerNoteAsync(decisionId, request, ActorId, correlationId);

            if (!response.Success)
            {
                if (response.ErrorCode == "INGESTION_DECISION_NOT_FOUND")
                    return NotFound(new ApiErrorResponse { ErrorMessage = response.ErrorMessage ?? string.Empty, ErrorCode = response.ErrorCode ?? string.Empty });
                return BadRequest(new ApiErrorResponse { ErrorMessage = response.ErrorMessage ?? string.Empty, ErrorCode = response.ErrorCode ?? string.Empty });
            }

            return Ok(response);
        }
    }
}
