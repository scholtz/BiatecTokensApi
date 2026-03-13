using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ProtectedSignOff;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Protected Sign-Off Environment API
    /// </summary>
    /// <remarks>
    /// Provides the backend infrastructure for repeatable, enterprise-grade sign-off evidence.
    ///
    /// This API supports the strict sign-off workflow by exposing four capabilities:
    ///
    /// 1. **Environment check** – verifies that all backend services, configuration,
    ///    and infrastructure required for a protected sign-off run are available.
    ///
    /// 2. **Lifecycle verification** – executes a deterministic, stage-by-stage verification
    ///    of the enterprise sign-off journey (authentication → initiation → status polling →
    ///    terminal state → validation) and returns structured evidence for each stage.
    ///
    /// 3. **Fixture provisioning** – seeds the default enterprise issuer and team fixtures
    ///    so the protected sign-off journey operates against realistic authorization state
    ///    without ad-hoc runtime mutation or permissive fallbacks.
    ///
    /// 4. **Diagnostics** – gathers operational diagnostics that distinguish configuration
    ///    failures, authorization failures, contract failures, and lifecycle failures so
    ///    that protected-run problems can be triaged quickly without exposing secrets.
    ///
    /// Every endpoint is fail-closed: no silent success, no fake-pass paths.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/protected-sign-off")]
    [Produces("application/json")]
    public class ProtectedSignOffController : ControllerBase
    {
        private readonly IProtectedSignOffEnvironmentService _protectedSignOffService;
        private readonly ILogger<ProtectedSignOffController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="ProtectedSignOffController"/>.
        /// </summary>
        public ProtectedSignOffController(
            IProtectedSignOffEnvironmentService protectedSignOffService,
            ILogger<ProtectedSignOffController> logger)
        {
            _protectedSignOffService = protectedSignOffService;
            _logger = logger;
        }

        /// <summary>
        /// Checks whether the protected sign-off environment is ready for a protected run.
        /// </summary>
        /// <remarks>
        /// Performs a structured readiness check against every backend component required
        /// for a protected sign-off run:
        ///
        /// - **Authentication** – JWT + ARC76 derivation infrastructure
        /// - **IssuerWorkflow** – team role and approval-state infrastructure
        /// - **DeploymentLifecycle** – backend deployment contract service
        /// - **SignOffValidation** – proof-generation service
        /// - **Observability** – correlation ID propagation (optional)
        /// - **EnterpriseFixtures** – default issuer fixtures (optional)
        ///
        /// When <c>isReadyForProtectedRun</c> is <c>true</c>, all required checks passed
        /// and the backend can accept a protected sign-off run. Any other status means
        /// remediation is needed; inspect the <c>checks</c> array and follow
        /// <c>actionableGuidance</c> for each failed check.
        /// </remarks>
        /// <param name="request">Request controlling which optional checks to include.</param>
        /// <returns>Structured environment readiness response.</returns>
        [HttpPost("environment/check")]
        [ProducesResponseType(typeof(ProtectedSignOffEnvironmentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CheckEnvironmentReadiness(
            [FromBody] ProtectedSignOffEnvironmentRequest request)
        {
            if (request == null)
            {
                return BadRequest(new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "Request body is required."
                });
            }

            string correlationId = LoggingHelper.SanitizeLogInput(request.CorrelationId ?? "NONE");
            _logger.LogInformation(
                "ProtectedSignOff environment check requested. CorrelationId={CorrelationId}",
                correlationId);

            ProtectedSignOffEnvironmentResponse result =
                await _protectedSignOffService.CheckEnvironmentReadinessAsync(request);

            return Ok(result);
        }

        /// <summary>
        /// Executes the enterprise sign-off lifecycle journey verification.
        /// </summary>
        /// <remarks>
        /// Runs a deterministic, stage-by-stage verification of the full enterprise
        /// sign-off lifecycle:
        ///
        /// - **Authentication** – verifies authentication infrastructure is available
        /// - **Initiation** – verifies workflow initiation readiness
        /// - **StatusPolling** – verifies deployment status polling returns stable responses
        /// - **TerminalState** – verifies terminal-state semantics are deterministic
        /// - **Validation** – verifies sign-off proof generation is functioning
        /// - **Complete** – confirms the full lifecycle is ready for protected evidence
        ///
        /// When <c>isLifecycleVerified</c> is <c>true</c>, every stage passed and the
        /// backend is ready to support a protected sign-off run. The response can be cited
        /// as evidence that the backend lifecycle contract is stable.
        ///
        /// Failures stop the journey at the failing stage and mark subsequent stages as
        /// Skipped, providing a clear indication of where remediation is needed.
        /// </remarks>
        /// <param name="request">Request specifying the issuer, deployment, and options.</param>
        /// <returns>Structured lifecycle verification response with per-stage results.</returns>
        [HttpPost("lifecycle/execute")]
        [ProducesResponseType(typeof(EnterpriseSignOffLifecycleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExecuteSignOffLifecycle(
            [FromBody] EnterpriseSignOffLifecycleRequest request)
        {
            if (request == null)
            {
                return BadRequest(new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "Request body is required."
                });
            }

            string correlationId = LoggingHelper.SanitizeLogInput(request.CorrelationId ?? "NONE");
            _logger.LogInformation(
                "ProtectedSignOff lifecycle verification requested. CorrelationId={CorrelationId}",
                correlationId);

            EnterpriseSignOffLifecycleResponse result =
                await _protectedSignOffService.ExecuteSignOffLifecycleAsync(request);

            return Ok(result);
        }

        /// <summary>
        /// Provisions enterprise sign-off fixtures (default issuer and admin team member).
        /// </summary>
        /// <remarks>
        /// Seeds the default enterprise sign-off issuer and its admin team member so the
        /// protected sign-off journey operates against realistic authorization state.
        ///
        /// Default fixtures:
        /// - **Issuer ID**: <c>biatec-protected-sign-off-issuer</c>
        /// - **Admin user**: <c>biatec-sign-off-admin@biatec.io</c>
        ///
        /// If fixtures already exist and <c>resetIfExists</c> is <c>false</c> (default),
        /// the endpoint returns immediately with <c>wasAlreadyProvisioned: true</c>.
        /// Set <c>resetIfExists: true</c> to clear and re-provision.
        ///
        /// This endpoint is safe to call repeatedly; it is idempotent when
        /// <c>resetIfExists</c> is <c>false</c>.
        /// </remarks>
        /// <param name="request">Request specifying issuer, admin user, and reset options.</param>
        /// <returns>Structured provisioning response.</returns>
        [HttpPost("fixtures/provision")]
        [ProducesResponseType(typeof(EnterpriseFixtureProvisionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ProvisionEnterpriseFixtures(
            [FromBody] EnterpriseFixtureProvisionRequest request)
        {
            if (request == null)
            {
                return BadRequest(new ApiErrorResponse
                {
                    ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "Request body is required."
                });
            }

            string correlationId = LoggingHelper.SanitizeLogInput(request.CorrelationId ?? "NONE");
            _logger.LogInformation(
                "ProtectedSignOff fixture provisioning requested. CorrelationId={CorrelationId}",
                correlationId);

            EnterpriseFixtureProvisionResponse result =
                await _protectedSignOffService.ProvisionEnterpriseFixturesAsync(request);

            return Ok(result);
        }

        /// <summary>
        /// Returns operational diagnostics for the protected sign-off backend.
        /// </summary>
        /// <remarks>
        /// Gathers a structured diagnostics report that enables product and operations teams
        /// to triage protected-run failures quickly. The report distinguishes:
        ///
        /// - **Configuration failures** – missing or invalid required settings
        /// - **Authorization failures** – missing role or insufficient permissions
        /// - **Contract failures** – unexpected response shapes from sign-off services
        /// - **Lifecycle failures** – invalid state transitions or lifecycle inconsistencies
        /// - **Service availability failures** – unreachable or unregistered services
        ///
        /// The report does not expose secrets or internal credentials. It provides
        /// service-level availability status and actionable remediation guidance only.
        ///
        /// Supply a <c>correlationId</c> query parameter to correlate this diagnostics
        /// response with backend logs from a specific protected run.
        /// </remarks>
        /// <param name="correlationId">Optional correlation ID for distributed tracing.</param>
        /// <returns>Structured diagnostics response.</returns>
        [HttpGet("diagnostics")]
        [ProducesResponseType(typeof(ProtectedSignOffDiagnosticsResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDiagnostics(
            [FromQuery] string? correlationId = null)
        {
            _logger.LogInformation(
                "ProtectedSignOff diagnostics requested. CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(correlationId ?? "NONE"));

            ProtectedSignOffDiagnosticsResponse result =
                await _protectedSignOffService.GetDiagnosticsAsync(correlationId);

            return Ok(result);
        }
    }
}
