using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.LiveProviderVerificationJourney;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Live-provider KYC/AML verification journey API.
    /// </summary>
    /// <remarks>
    /// This API exposes the end-to-end live-provider verification journey for subject
    /// onboarding and compliance case workflows. It provides a unified, operator-observable
    /// view of a subject's complete verification journey — from KYC initiation through
    /// AML screening, manual review, and final approval decision.
    ///
    /// Key capabilities:
    /// - **Journey start**: Initiate a KYC/AML verification journey with configurable
    ///   execution mode (live, sandbox, or simulated). Fails closed when provider
    ///   configuration is absent or providers are unreachable.
    /// - **Journey status**: Full step audit trail and approval-decision explanation,
    ///   suitable for operator cockpit and evidence center views.
    /// - **Approval decision**: Structured explanation of why a journey is approved,
    ///   rejected, pending review, blocked, or requiring action.
    /// - **Release evidence**: Content-hashed, artifact-backed proof connecting a
    ///   verification journey to a specific release candidate.
    ///
    /// Endpoints:
    /// - `POST  /`                              – Start a new verification journey.
    /// - `GET   /{journeyId}`                   – Get journey status and approval decision.
    /// - `POST  /{journeyId}/evaluate-decision` – Evaluate and return approval decision.
    /// - `POST  /{journeyId}/release-evidence`  – Generate release-grade evidence artifact.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/verification-journey")]
    [Produces("application/json")]
    public class LiveProviderVerificationJourneyController : ControllerBase
    {
        private readonly ILiveProviderVerificationJourneyService _service;
        private readonly ILogger<LiveProviderVerificationJourneyController> _logger;

        /// <summary>
        /// Initialises the controller.
        /// </summary>
        public LiveProviderVerificationJourneyController(
            ILiveProviderVerificationJourneyService service,
            ILogger<LiveProviderVerificationJourneyController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ── Start Journey ─────────────────────────────────────────────────────────

        /// <summary>
        /// Starts a new live-provider KYC/AML verification journey for a subject.
        /// </summary>
        /// <remarks>
        /// Initiates KYC identity verification and AML screening against the configured
        /// provider. Returns the created journey record with structured diagnostics.
        ///
        /// When <c>RequireProviderBacked</c> is true, the call fails if execution mode
        /// resolves to Simulated. When the KYC/AML provider is unconfigured or unreachable,
        /// the journey is created in Degraded state with actionable diagnostics — no silent
        /// success-shaped fallback.
        ///
        /// If an idempotency key is supplied and a journey for that key already exists
        /// for the subject, the existing journey is returned.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(StartVerificationJourneyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(StartVerificationJourneyResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> StartJourney([FromBody] StartVerificationJourneyRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "StartJourney. SubjectId={SubjectId} Mode={Mode} RequireProviderBacked={RequireProviderBacked} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                request.RequestedExecutionMode,
                request.RequireProviderBacked,
                LoggingHelper.SanitizeLogInput(actorId));

            var response = await _service.StartJourneyAsync(request, actorId);

            if (!response.Success &&
                response.Journey?.CurrentStage is not VerificationJourneyStage.Degraded)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        // ── Get Journey Status ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the current status of a verification journey with a full audit trail
        /// and approval-decision explanation.
        /// </summary>
        /// <remarks>
        /// The approval-decision explanation includes structured rationale for why the
        /// journey is at its current stage, what checks passed or failed, and what
        /// action the operator should take next.
        /// </remarks>
        [HttpGet("{journeyId}")]
        [ProducesResponseType(typeof(GetVerificationJourneyStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GetVerificationJourneyStatusResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetJourneyStatus(string journeyId)
        {
            _logger.LogInformation(
                "GetJourneyStatus. JourneyId={JourneyId}",
                LoggingHelper.SanitizeLogInput(journeyId));

            var response = await _service.GetJourneyStatusAsync(journeyId);

            if (!response.Success)
                return NotFound(response);

            return Ok(response);
        }

        // ── Evaluate Approval Decision ────────────────────────────────────────────

        /// <summary>
        /// Evaluates and returns the current approval-decision explanation for a journey.
        /// </summary>
        /// <remarks>
        /// Provides operator-facing rationale for why a journey is approved, rejected,
        /// pending review, blocked, or requiring action. Includes which checks passed,
        /// failed, or are pending, whether evidence is provider-backed and release-grade,
        /// and actionable guidance for each non-approved state.
        /// </remarks>
        [HttpPost("{journeyId}/evaluate-decision")]
        [ProducesResponseType(typeof(EvaluateApprovalDecisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(EvaluateApprovalDecisionResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> EvaluateDecision(string journeyId)
        {
            _logger.LogInformation(
                "EvaluateDecision. JourneyId={JourneyId}",
                LoggingHelper.SanitizeLogInput(journeyId));

            var response = await _service.EvaluateApprovalDecisionAsync(journeyId);

            if (!response.Success)
                return NotFound(response);

            return Ok(response);
        }

        // ── Generate Release Evidence ─────────────────────────────────────────────

        /// <summary>
        /// Generates a durable, content-hashed release evidence artifact for a journey.
        /// </summary>
        /// <remarks>
        /// Bundles all journey steps, the current approval-decision explanation, and
        /// provider references into a tamper-evident record suitable for business-owner
        /// sign-off, audit, and regulator-facing review.
        ///
        /// When <c>RequireProviderBacked</c> is true, generation fails if the journey
        /// was executed in Simulated mode. This preserves the fail-closed contract for
        /// protected release paths.
        ///
        /// Supply a <c>ReleaseTag</c> and <c>WorkflowRunReference</c> to connect
        /// the evidence to the exact release candidate and CI workflow run.
        /// </remarks>
        [HttpPost("{journeyId}/release-evidence")]
        [ProducesResponseType(typeof(GenerateVerificationJourneyEvidenceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(GenerateVerificationJourneyEvidenceResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(GenerateVerificationJourneyEvidenceResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GenerateReleaseEvidence(
            string journeyId,
            [FromBody] GenerateVerificationJourneyEvidenceRequest request)
        {
            var actorId = GetActorId();

            _logger.LogInformation(
                "GenerateReleaseEvidence. JourneyId={JourneyId} ReleaseTag={ReleaseTag} " +
                "RequireProviderBacked={RequireProviderBacked} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(journeyId),
                LoggingHelper.SanitizeLogInput(request.ReleaseTag ?? "(none)"),
                request.RequireProviderBacked,
                LoggingHelper.SanitizeLogInput(actorId));

            var response = await _service.GenerateReleaseEvidenceAsync(journeyId, request, actorId);

            if (!response.Success)
            {
                if (response.ErrorCode == "JOURNEY_NOT_FOUND")
                    return NotFound(response);
                return BadRequest(response);
            }

            return Ok(response);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private string GetActorId()
        {
            return User?.Identity?.Name ?? "anonymous";
        }
    }
}
