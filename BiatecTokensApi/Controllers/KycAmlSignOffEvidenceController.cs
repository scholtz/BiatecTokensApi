using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.KycAmlSignOff;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provider-backed KYC/AML sign-off evidence API.
    /// </summary>
    /// <remarks>
    /// This API operationalizes live-provider KYC/AML sign-off evidence for enterprise
    /// investor onboarding. It orchestrates real or production-like provider flows,
    /// records durable evidence artifacts, propagates provider-backed results into
    /// approval readiness, and enforces fail-closed handling for all adverse or degraded
    /// conditions.
    ///
    /// Key capabilities:
    /// - **Execution mode tracking**: Each record exposes whether evidence was produced by
    ///   a live provider, protected sandbox, or simulation. Product leadership can
    ///   distinguish release-grade from permissive test evidence.
    /// - **Plain-language explanations**: Every state carries a human-readable explanation
    ///   suitable for enterprise operator display (e.g., "Sanctions hit requires analyst
    ///   review").
    /// - **Durable artifacts**: Provider initiation records, callback payloads, and
    ///   readiness assessments are retained as named artifacts for audit and sign-off
    ///   conversations.
    /// - **Fail-closed**: Provider unavailability, malformed callbacks, adverse findings,
    ///   stale evidence, and incomplete remediation all produce a blocked readiness state
    ///   — never silent approval.
    ///
    /// Endpoints:
    /// - `POST   /initiate`                          – Initiate a new KYC/AML sign-off flow.
    /// - `POST   /{recordId}/callback`               – Process a provider callback.
    /// - `GET    /{recordId}`                        – Retrieve a sign-off record.
    /// - `GET    /{recordId}/readiness`              – Evaluate readiness for approval gating.
    /// - `GET    /{recordId}/artifacts`              – Retrieve evidence artifacts.
    /// - `POST   /{recordId}/poll`                   – Poll provider for updated status.
    /// - `GET    /subject/{subjectId}`               – List all records for a subject.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/kyc-aml-signoff")]
    [Produces("application/json")]
    public class KycAmlSignOffEvidenceController : ControllerBase
    {
        private readonly IKycAmlSignOffEvidenceService _service;
        private readonly ILogger<KycAmlSignOffEvidenceController> _logger;

        /// <summary>
        /// Initialises the controller.
        /// </summary>
        public KycAmlSignOffEvidenceController(
            IKycAmlSignOffEvidenceService service,
            ILogger<KycAmlSignOffEvidenceController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Initiates a new KYC/AML sign-off evidence flow for a subject.
        /// </summary>
        /// <remarks>
        /// Calls the configured KYC and/or AML provider and creates a durable sign-off
        /// evidence record. When a live provider is requested but not configured, the
        /// call fails closed.
        ///
        /// Supply an <c>IdempotencyKey</c> to ensure a second call with the same key
        /// returns the existing record without re-initiating the provider check.
        /// </remarks>
        /// <param name="request">Sign-off initiation parameters.</param>
        /// <returns>The newly created (or existing) sign-off record.</returns>
        /// <response code="200">Sign-off record created or returned via idempotency.</response>
        /// <response code="400">Request validation failed.</response>
        /// <response code="401">Authentication required.</response>
        [HttpPost("initiate")]
        [ProducesResponseType(typeof(InitiateKycAmlSignOffResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Initiate([FromBody] InitiateKycAmlSignOffRequest request)
        {
            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var correlationId = HttpContext.TraceIdentifier ?? Guid.NewGuid().ToString();

            _logger.LogInformation(
                "KycAmlSignOff initiate. SubjectId={SubjectId}, CheckKind={CheckKind}, Mode={Mode}, Actor={Actor}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request?.SubjectId ?? "null"),
                request?.CheckKind,
                request?.RequestedExecutionMode,
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.InitiateSignOffAsync(request!, actorId, correlationId);

            if (!response.Success)
                return BadRequest(new ApiErrorResponse { ErrorCode = response.ErrorCode, ErrorMessage = response.ErrorMessage });

            return Ok(response);
        }

        /// <summary>
        /// Processes an inbound provider callback for a sign-off record.
        /// </summary>
        /// <remarks>
        /// Validates the callback payload and updates the sign-off record. When a
        /// webhook secret is configured, the HMAC signature is verified before
        /// processing. Fails closed on invalid signature, malformed payload, or
        /// unknown provider reference.
        /// </remarks>
        /// <param name="recordId">The sign-off record ID.</param>
        /// <param name="request">The provider callback payload.</param>
        /// <returns>Updated sign-off record.</returns>
        /// <response code="200">Callback accepted and processed.</response>
        /// <response code="400">Callback rejected (validation failure, unknown reference, etc.).</response>
        /// <response code="401">Authentication required.</response>
        [HttpPost("{recordId}/callback")]
        [ProducesResponseType(typeof(ProcessKycAmlSignOffCallbackResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ProcessCallback(
            [FromRoute] string recordId,
            [FromBody] ProcessKycAmlSignOffCallbackRequest request)
        {
            var correlationId = HttpContext.TraceIdentifier ?? Guid.NewGuid().ToString();

            _logger.LogInformation(
                "KycAmlSignOff callback. RecordId={RecordId}, ProviderRef={ProviderRef}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(recordId),
                LoggingHelper.SanitizeLogInput(request?.ProviderReferenceId ?? "null"),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _service.ProcessCallbackAsync(recordId, request!, correlationId);

            if (!response.Success)
                return BadRequest(new ApiErrorResponse { ErrorCode = response.ErrorCode, ErrorMessage = response.ErrorMessage });

            return Ok(response);
        }

        /// <summary>
        /// Retrieves a sign-off record including audit trail and evidence artifacts.
        /// </summary>
        /// <param name="recordId">The sign-off record ID.</param>
        /// <returns>Sign-off record detail.</returns>
        /// <response code="200">Record found and returned.</response>
        /// <response code="404">Record not found.</response>
        /// <response code="401">Authentication required.</response>
        [HttpGet("{recordId}")]
        [ProducesResponseType(typeof(GetKycAmlSignOffRecordResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetRecord([FromRoute] string recordId)
        {
            var response = await _service.GetRecordAsync(recordId);

            if (!response.Success)
                return NotFound(new ApiErrorResponse { ErrorCode = response.ErrorCode, ErrorMessage = response.ErrorMessage });

            return Ok(response);
        }

        /// <summary>
        /// Evaluates the approval readiness of a sign-off record.
        /// </summary>
        /// <remarks>
        /// Returns the readiness state, blockers, and a plain-language explanation.
        /// <c>IsApprovalReady</c> is only <c>true</c> when the evidence was produced by
        /// a live or protected-sandbox provider and all checks passed.
        ///
        /// This endpoint is the primary signal for approval-gating logic in enterprise
        /// workflows.
        /// </remarks>
        /// <param name="recordId">The sign-off record ID.</param>
        /// <returns>Readiness assessment.</returns>
        /// <response code="200">Readiness evaluated and returned.</response>
        /// <response code="404">Record not found.</response>
        /// <response code="401">Authentication required.</response>
        [HttpGet("{recordId}/readiness")]
        [ProducesResponseType(typeof(KycAmlSignOffReadinessResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetReadiness([FromRoute] string recordId)
        {
            var response = await _service.GetReadinessAsync(recordId);

            // ReadinessState == IncompleteEvidence with a null RecordId indicates not found
            if (string.IsNullOrWhiteSpace(response.RecordId) && response.ReadinessState == KycAmlSignOffReadinessState.IncompleteEvidence)
                return NotFound(new ApiErrorResponse { ErrorCode = "RECORD_NOT_FOUND", ErrorMessage = $"No sign-off record found with ID '{LoggingHelper.SanitizeLogInput(recordId)}'." });

            return Ok(response);
        }

        /// <summary>
        /// Returns the evidence artifacts for a sign-off record.
        /// </summary>
        /// <remarks>
        /// Evidence artifacts include provider initiation records, callback payloads,
        /// state transition records, and readiness assessments. The
        /// <c>IsProviderBacked</c> flag on each artifact indicates whether it was
        /// produced by a live or protected-sandbox provider.
        /// </remarks>
        /// <param name="recordId">The sign-off record ID.</param>
        /// <returns>Evidence artifacts list.</returns>
        /// <response code="200">Artifacts returned.</response>
        /// <response code="404">Record not found.</response>
        /// <response code="401">Authentication required.</response>
        [HttpGet("{recordId}/artifacts")]
        [ProducesResponseType(typeof(GetKycAmlSignOffArtifactsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetArtifacts([FromRoute] string recordId)
        {
            var response = await _service.GetArtifactsAsync(recordId);

            if (!string.IsNullOrWhiteSpace(response.ErrorCode))
                return NotFound(new ApiErrorResponse { ErrorCode = response.ErrorCode, ErrorMessage = response.ErrorMessage });

            return Ok(response);
        }

        /// <summary>
        /// Polls the provider for an updated status and refreshes the sign-off record.
        /// </summary>
        /// <remarks>
        /// Only applicable when the current outcome is <c>Pending</c>. If the provider
        /// is unavailable, the record is updated to a blocked state.
        /// </remarks>
        /// <param name="recordId">The sign-off record ID.</param>
        /// <returns>Poll result with updated record.</returns>
        /// <response code="200">Poll completed (even if no change occurred).</response>
        /// <response code="404">Record not found.</response>
        /// <response code="401">Authentication required.</response>
        [HttpPost("{recordId}/poll")]
        [ProducesResponseType(typeof(PollKycAmlSignOffStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> PollStatus([FromRoute] string recordId)
        {
            var correlationId = HttpContext.TraceIdentifier ?? Guid.NewGuid().ToString();

            var response = await _service.PollProviderStatusAsync(recordId, correlationId);

            if (!response.Success && response.ErrorCode == "RECORD_NOT_FOUND")
                return NotFound(new ApiErrorResponse { ErrorCode = response.ErrorCode, ErrorMessage = response.ErrorMessage });

            return Ok(response);
        }

        /// <summary>
        /// Lists all KYC/AML sign-off records for a subject.
        /// </summary>
        /// <param name="subjectId">The subject ID.</param>
        /// <returns>List of sign-off records ordered by creation time descending.</returns>
        /// <response code="200">Records returned (may be empty list).</response>
        /// <response code="401">Authentication required.</response>
        [HttpGet("subject/{subjectId}")]
        [ProducesResponseType(typeof(ListKycAmlSignOffRecordsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ListForSubject([FromRoute] string subjectId)
        {
            _logger.LogInformation(
                "KycAmlSignOff list for subject. SubjectId={SubjectId}",
                LoggingHelper.SanitizeLogInput(subjectId));

            var response = await _service.ListRecordsForSubjectAsync(subjectId);
            return Ok(response);
        }
    }
}
