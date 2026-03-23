using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.KycAmlOnboarding;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provider-backed KYC/AML onboarding case lifecycle API.
    /// </summary>
    /// <remarks>
    /// Manages the full lifecycle of KYC/AML onboarding cases — from creation through
    /// provider checks, reviewer decisions, and evidence summarisation.
    ///
    /// Key capabilities:
    /// - **Case creation**: Create a case for a subject with optional idempotency key.
    ///   Fail-closed: when provider is not configured, <c>IsProviderConfigured = false</c>.
    /// - **Provider checks**: Initiate live/sandbox/simulated KYC/AML checks.
    ///   Fail-closed when provider is unconfigured or unreachable.
    /// - **Reviewer actions**: Record Approve, Reject, Escalate, RequestAdditionalInfo,
    ///   or AddNote with actor ID and rationale.
    /// - **Evidence summary**: Structured evidence state, provider backing, and actionable
    ///   guidance for every case state.
    /// - **Case listing**: Filter by subject ID and state with pagination.
    ///
    /// Endpoints:
    /// - <c>POST  /cases</c>                              – Create a new onboarding case.
    /// - <c>GET   /cases/{caseId}</c>                     – Get a case by ID.
    /// - <c>POST  /cases/{caseId}/initiate-checks</c>     – Initiate provider checks.
    /// - <c>POST  /cases/{caseId}/reviewer-actions</c>    – Record a reviewer action.
    /// - <c>GET   /cases/{caseId}/evidence</c>            – Get evidence summary.
    /// - <c>GET   /cases</c>                              – List cases with filters.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/kyc-aml-onboarding")]
    [Produces("application/json")]
    public class KycAmlOnboardingController : ControllerBase
    {
        private readonly IKycAmlOnboardingCaseService _service;
        private readonly ILogger<KycAmlOnboardingController> _logger;

        /// <summary>Initialises the controller.</summary>
        public KycAmlOnboardingController(
            IKycAmlOnboardingCaseService service,
            ILogger<KycAmlOnboardingController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ── Create Case ───────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new KYC/AML onboarding case for a subject.
        /// </summary>
        /// <remarks>
        /// When an idempotency key is supplied and a case already exists for the same
        /// SubjectId + key, the existing case is returned without modification.
        ///
        /// The response always includes <c>IsProviderConfigured</c>. When false, no
        /// provider checks can be initiated until configuration is supplied.
        /// </remarks>
        [HttpPost("cases")]
        [ProducesResponseType(typeof(CreateOnboardingCaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(CreateOnboardingCaseResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateCase([FromBody] CreateOnboardingCaseRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "CreateCase. SubjectId={SubjectId} SubjectKind={SubjectKind} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                request.SubjectKind,
                LoggingHelper.SanitizeLogInput(actorId));

            var response = await _service.CreateCaseAsync(request, actorId);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        // ── Get Case ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current state of an onboarding case.
        /// </summary>
        [HttpGet("cases/{caseId}")]
        [ProducesResponseType(typeof(GetOnboardingCaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetOnboardingCaseResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetCase(string caseId)
        {
            _logger.LogInformation(
                "GetCase. CaseId={CaseId}",
                LoggingHelper.SanitizeLogInput(caseId));

            var response = await _service.GetCaseAsync(caseId);

            if (!response.Success)
                return NotFound(response);

            return Ok(response);
        }

        // ── Initiate Provider Checks ───────────────────────────────────────────────

        /// <summary>
        /// Initiates provider KYC/AML checks for an onboarding case.
        /// </summary>
        /// <remarks>
        /// The case must be in the <c>Initiated</c> state. When the provider service is
        /// not configured, the response carries <c>ErrorCode = "PROVIDER_NOT_CONFIGURED"</c>
        /// and the case state is set to <c>ConfigurationMissing</c>.
        /// </remarks>
        [HttpPost("cases/{caseId}/initiate-checks")]
        [ProducesResponseType(typeof(InitiateProviderChecksResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(InitiateProviderChecksResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(InitiateProviderChecksResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> InitiateProviderChecks(
            string caseId,
            [FromBody] InitiateProviderChecksRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "InitiateProviderChecks. CaseId={CaseId} Mode={Mode} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                request.ExecutionMode,
                LoggingHelper.SanitizeLogInput(actorId));

            var response = await _service.InitiateProviderChecksAsync(caseId, request, actorId);

            if (!response.Success)
            {
                if (response.ErrorCode == "CASE_NOT_FOUND")
                    return NotFound(response);
                return BadRequest(response);
            }

            return Ok(response);
        }

        // ── Record Reviewer Action ────────────────────────────────────────────────

        /// <summary>
        /// Records a reviewer action on an onboarding case and applies the state transition.
        /// </summary>
        /// <remarks>
        /// Valid actions: Approve, Reject, Escalate, RequestAdditionalInfo, AddNote.
        /// Invalid state transitions return <c>ErrorCode = "INVALID_STATE_TRANSITION"</c>.
        /// </remarks>
        [HttpPost("cases/{caseId}/reviewer-actions")]
        [ProducesResponseType(typeof(RecordReviewerActionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RecordReviewerActionResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(RecordReviewerActionResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RecordReviewerAction(
            string caseId,
            [FromBody] RecordReviewerActionRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "RecordReviewerAction. CaseId={CaseId} Kind={Kind} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                request.Kind,
                LoggingHelper.SanitizeLogInput(actorId));

            var response = await _service.RecordReviewerActionAsync(caseId, request, actorId);

            if (!response.Success)
            {
                if (response.ErrorCode == "CASE_NOT_FOUND")
                    return NotFound(response);
                return BadRequest(response);
            }

            return Ok(response);
        }

        // ── Get Evidence Summary ──────────────────────────────────────────────────

        /// <summary>
        /// Returns a structured evidence summary for an onboarding case.
        /// </summary>
        /// <remarks>
        /// Reflects provider backing status, checks completed, and actionable guidance.
        /// When provider configuration is absent, <c>EvidenceState = MissingConfiguration</c>
        /// and <c>IsProviderBacked = false</c> — there is no silent green fallback.
        /// </remarks>
        [HttpGet("cases/{caseId}/evidence")]
        [ProducesResponseType(typeof(GetOnboardingEvidenceSummaryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetOnboardingEvidenceSummaryResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetEvidenceSummary(string caseId)
        {
            _logger.LogInformation(
                "GetEvidenceSummary. CaseId={CaseId}",
                LoggingHelper.SanitizeLogInput(caseId));

            var response = await _service.GetEvidenceSummaryAsync(caseId);

            if (!response.Success)
                return NotFound(response);

            return Ok(response);
        }

        // ── List Cases ────────────────────────────────────────────────────────────

        /// <summary>
        /// Lists onboarding cases, optionally filtered by subject ID and state.
        /// </summary>
        [HttpGet("cases")]
        [ProducesResponseType(typeof(ListOnboardingCasesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ListCases([FromQuery] ListOnboardingCasesRequest? request)
        {
            _logger.LogInformation(
                "ListCases. SubjectId={SubjectId} State={State}",
                LoggingHelper.SanitizeLogInput(request?.SubjectId),
                request?.State?.ToString() ?? "(any)");

            var response = await _service.ListCasesAsync(request);
            return Ok(response);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private string GetActorId()
        {
            return User?.Identity?.Name ?? "anonymous";
        }
    }
}
