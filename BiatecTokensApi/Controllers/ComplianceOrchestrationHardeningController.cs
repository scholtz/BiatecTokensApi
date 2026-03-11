using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceHardening;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Compliance orchestration hardening endpoints for enterprise launch decisions.
    /// Provides deterministic, auditable compliance readiness evaluation with explicit
    /// jurisdiction constraints, sanctions readiness, and launch gate enforcement.
    /// </summary>
    [ApiController]
    [Route("api/v1/compliance-hardening")]
    [Authorize]
    public class ComplianceOrchestrationHardeningController : ControllerBase
    {
        private readonly IComplianceOrchestrationHardeningService _hardeningService;
        private readonly ILogger<ComplianceOrchestrationHardeningController> _logger;

        public ComplianceOrchestrationHardeningController(
            IComplianceOrchestrationHardeningService hardeningService,
            ILogger<ComplianceOrchestrationHardeningController> logger)
        {
            _hardeningService = hardeningService;
            _logger = logger;
        }

        /// <summary>
        /// Performs a full compliance hardening evaluation for a pending token launch.
        /// Aggregates jurisdiction constraints, sanctions readiness, and overall launch gate
        /// status into a single deterministic response with remediation hints.
        /// </summary>
        /// <param name="request">Hardening evaluation request.</param>
        /// <returns>Comprehensive hardening response with launch gate status and evidence.</returns>
        [HttpPost("evaluate-readiness")]
        [ProducesResponseType(typeof(ComplianceHardeningResponse), 200)]
        [ProducesResponseType(typeof(ComplianceHardeningResponse), 400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> EvaluateReadiness([FromBody] ComplianceHardeningRequest request)
        {
            if (request == null)
                return BadRequest(new ComplianceHardeningResponse
                {
                    Success = false,
                    ErrorMessage = "Request body is required.",
                    ErrorCategory = ComplianceErrorCategory.InvalidInput,
                    ReasonCode = "MISSING_REQUEST_BODY",
                    LaunchGate = LaunchGateStatus.NotReady
                });

            var correlationId = GetCorrelationId();
            var actorId = GetActorId();

            _logger.LogInformation(
                "Compliance hardening evaluation requested. SubjectId={SubjectId}, TokenId={TokenId}, Actor={Actor}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                LoggingHelper.SanitizeLogInput(request.TokenId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _hardeningService.EvaluateLaunchReadinessAsync(request, correlationId);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Evaluates jurisdiction constraints for a subject in a specific jurisdiction.
        /// Returns an explicit NotConfigured outcome when the jurisdiction rules engine
        /// is not configured — never silently succeeds.
        /// </summary>
        /// <param name="request">Jurisdiction constraint request.</param>
        /// <returns>Jurisdiction constraint evaluation result.</returns>
        [HttpPost("jurisdiction-constraint")]
        [ProducesResponseType(typeof(JurisdictionConstraintResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> EvaluateJurisdiction([FromBody] JurisdictionConstraintRequest request)
        {
            if (request == null)
                return BadRequest("Request body is required.");

            if (string.IsNullOrWhiteSpace(request.SubjectId) || string.IsNullOrWhiteSpace(request.JurisdictionCode))
                return BadRequest("SubjectId and JurisdictionCode are required.");

            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "Jurisdiction constraint check requested. SubjectId={SubjectId}, Jurisdiction={Jurisdiction}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                LoggingHelper.SanitizeLogInput(request.JurisdictionCode),
                LoggingHelper.SanitizeLogInput(correlationId));

            var result = await _hardeningService.GetJurisdictionConstraintAsync(request, correlationId);
            return Ok(result);
        }

        /// <summary>
        /// Evaluates sanctions and KYC readiness for a subject.
        /// Returns an explicit NotConfigured outcome when the provider is not integrated
        /// — never silently succeeds.
        /// </summary>
        /// <param name="request">Sanctions readiness request.</param>
        /// <returns>Sanctions readiness evaluation result.</returns>
        [HttpPost("sanctions-readiness")]
        [ProducesResponseType(typeof(SanctionsReadinessResult), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> CheckSanctionsReadiness([FromBody] SanctionsReadinessRequest request)
        {
            if (request == null)
                return BadRequest("Request body is required.");

            if (string.IsNullOrWhiteSpace(request.SubjectId))
                return BadRequest("SubjectId is required.");

            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "Sanctions readiness check requested. SubjectId={SubjectId}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var result = await _hardeningService.GetSanctionsReadinessAsync(request, correlationId);
            return Ok(result);
        }

        /// <summary>
        /// Enforces the launch gate for a specific token.
        /// Prevents launch execution when blocking compliance reasons exist.
        /// Returns IsLaunchPermitted=false with explicit blocking reasons and remediation hints
        /// when any compliance prerequisite is not satisfied.
        /// </summary>
        /// <param name="request">Launch gate enforcement request.</param>
        /// <returns>Launch gate response with permit/block decision.</returns>
        [HttpPost("launch-gate/enforce")]
        [ProducesResponseType(typeof(LaunchGateResponse), 200)]
        [ProducesResponseType(typeof(LaunchGateResponse), 400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> EnforceLaunchGate([FromBody] LaunchGateRequest request)
        {
            if (request == null)
                return BadRequest(new LaunchGateResponse
                {
                    Success = false,
                    ErrorMessage = "Request body is required.",
                    ErrorCategory = ComplianceErrorCategory.InvalidInput,
                    ReasonCode = "MISSING_REQUEST_BODY",
                    GateStatus = LaunchGateStatus.NotReady
                });

            var correlationId = GetCorrelationId();
            var actorId = GetActorId();

            _logger.LogInformation(
                "Launch gate enforcement requested. TokenId={TokenId}, SubjectId={SubjectId}, Actor={Actor}, CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.TokenId),
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            var response = await _hardeningService.EnforceLaunchGateAsync(request, correlationId);

            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Returns the current integration and health status of all registered compliance providers.
        /// Provides observability into which providers are active, degraded, or not yet integrated.
        /// </summary>
        /// <returns>List of provider status reports.</returns>
        [HttpGet("provider-status")]
        [ProducesResponseType(typeof(ProviderStatusListResponse), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetProviderStatus()
        {
            var correlationId = GetCorrelationId();

            _logger.LogInformation(
                "Provider status requested. CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(correlationId));

            var result = await _hardeningService.GetProviderStatusAsync(correlationId);
            return Ok(result);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private string GetCorrelationId() =>
            HttpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        private string GetActorId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "unknown";
    }
}
