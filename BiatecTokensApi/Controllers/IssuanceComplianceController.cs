using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides compliance risk scoring and sanctions/KYC evidence endpoints for token issuance workflows.
    /// </summary>
    /// <remarks>
    /// This controller implements the enterprise compliance decision API for regulated RWA token issuance.
    /// It returns deterministic, auditable compliance decisions with explainable risk outcomes,
    /// structured evidence payloads, and machine-consumable decision metadata.
    ///
    /// All endpoints are secured behind existing authentication requirements.
    /// Correlation identifiers are included in every response for audit tracing.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance/issuance")]
    [Produces("application/json")]
    public class IssuanceComplianceController : ControllerBase
    {
        private readonly IIssuanceRiskScoringService _riskScoringService;
        private readonly ILogger<IssuanceComplianceController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="IssuanceComplianceController"/> class.
        /// </summary>
        /// <param name="riskScoringService">Issuance risk scoring service</param>
        /// <param name="logger">Logger instance</param>
        public IssuanceComplianceController(
            IIssuanceRiskScoringService riskScoringService,
            ILogger<IssuanceComplianceController> logger)
        {
            _riskScoringService = riskScoringService;
            _logger = logger;
        }

        /// <summary>
        /// Evaluates the compliance risk of a token issuance request.
        /// </summary>
        /// <param name="request">Issuance risk evaluation request containing KYC, sanctions, and jurisdiction evidence</param>
        /// <returns>Deterministic compliance decision with aggregate risk score and structured evidence blocks</returns>
        /// <remarks>
        /// Evaluates a token issuance request against compliance policy inputs and returns a
        /// deterministic decision payload. The decision is computed from a normalized aggregate
        /// risk score derived from KYC completeness, sanctions screening, and jurisdiction risk.
        ///
        /// **Decision Values:**
        /// - `allow` (risk score 0–39): All compliance checks passed; issuance may proceed.
        /// - `review` (risk score 40–69): Elevated risk; human review required before issuance.
        /// - `deny` (risk score 70–100): Critical compliance issue; issuance is not permitted.
        ///
        /// **Risk Score Components:**
        /// - KYC (0–40 points): Verification status × completeness percentage
        /// - Sanctions (0–30 points): Screening outcome × confidence level
        /// - Jurisdiction (0–30 points): Jurisdiction risk level
        ///
        /// **Audit Trail:**
        /// Every evaluation emits an auditable log entry including the correlation ID,
        /// actor identity, decision, and aggregate score. No sensitive evidence data is logged.
        ///
        /// **Example – Low-Risk Request (allow):**
        /// ```json
        /// POST /api/v1/compliance/issuance/evaluate
        /// {
        ///   "organizationId": "org-001",
        ///   "issuerId": "user@example.com",
        ///   "correlationId": "req-abc123",
        ///   "kycEvidence": {
        ///     "status": "Verified",
        ///     "completenessPercent": 95,
        ///     "provider": "Sumsub",
        ///     "verificationDate": "2026-01-15T00:00:00Z"
        ///   },
        ///   "sanctionsEvidence": {
        ///     "screened": true,
        ///     "hitDetected": false,
        ///     "hitConfidence": 0.0,
        ///     "screeningProvider": "Chainalysis"
        ///   },
        ///   "jurisdictionEvidence": {
        ///     "jurisdictionCode": "DE",
        ///     "riskLevel": "Low",
        ///     "micaCompliant": true,
        ///     "regulatoryFrameworks": ["MICA", "FATF"]
        ///   }
        /// }
        /// ```
        ///
        /// **Example – High-Risk Response (deny):**
        /// ```json
        /// {
        ///   "success": true,
        ///   "correlationId": "req-abc123",
        ///   "evaluatedAt": "2026-03-01T12:00:00Z",
        ///   "decision": "deny",
        ///   "aggregateRiskScore": 80,
        ///   "riskBand": "High",
        ///   "reasonCodes": ["SANCTIONS_HIT_CONFIRMED", "JURISDICTION_PROHIBITED"],
        ///   "primaryReason": "A confirmed sanctions hit has been detected. Issuance is denied.",
        ///   "policyVersion": "1.0.0",
        ///   "kycEvidence": { ... },
        ///   "sanctionsEvidence": { "screened": true, "hitDetected": true, "hitConfidence": 0.95, "riskPenalty": 30, "issueCodes": ["SANCTIONS_HIT_CONFIRMED"] },
        ///   "jurisdictionEvidence": { "jurisdictionCode": "XX", "riskLevel": "Prohibited", "riskPenalty": 30, "issueCodes": ["JURISDICTION_PROHIBITED"] },
        ///   "componentScores": { "kycScore": 20, "sanctionsScore": 30, "jurisdictionScore": 30, "total": 80 }
        /// }
        /// ```
        ///
        /// **Validation Errors (400):**
        /// - `MISSING_ORGANIZATION_ID` – organizationId is required
        /// - `MISSING_ISSUER_ID` – issuerId is required
        /// - `MISSING_JURISDICTION_CODE` – jurisdictionEvidence.jurisdictionCode is required
        /// - `INVALID_KYC_COMPLETENESS` – completenessPercent must be 0–100
        /// - `INVALID_SANCTIONS_CONFIDENCE` – hitConfidence must be 0.0–1.0
        /// </remarks>
        [HttpPost("evaluate")]
        [ProducesResponseType(typeof(IssuanceRiskEvaluationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EvaluateIssuanceRisk([FromBody] IssuanceRiskEvaluationRequest request)
        {
            var correlationId = HttpContext.TraceIdentifier;

            try
            {
                // Inject authenticated actor identity
                var actorId = User.Identity?.Name ?? User.FindFirst("Address")?.Value;
                if (string.IsNullOrWhiteSpace(actorId))
                {
                    _logger.LogWarning(
                        "Issuance compliance evaluation attempted without authentication. CorrelationId={CorrelationId}",
                        correlationId);

                    return Unauthorized(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.UNAUTHORIZED,
                        ErrorMessage = "Authentication is required to perform a compliance risk evaluation.",
                        RemediationHint = "Provide a valid JWT Bearer token in the Authorization header.",
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = correlationId,
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                // Propagate correlation ID from request if provided, otherwise use trace identifier
                if (string.IsNullOrWhiteSpace(request.CorrelationId))
                    request.CorrelationId = correlationId;

                // Inject actor identity when issuerId is not explicitly provided
                if (string.IsNullOrWhiteSpace(request.IssuerId))
                    request.IssuerId = actorId;

                _logger.LogInformation(
                    "Issuance compliance risk evaluation requested: OrganizationId={OrganizationId}, Actor={Actor}, CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(request.OrganizationId),
                    LoggingHelper.SanitizeLogInput(actorId),
                    LoggingHelper.SanitizeLogInput(request.CorrelationId)
                );

                var result = await _riskScoringService.EvaluateAsync(request);

                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Issuance compliance evaluation failed validation: ErrorCode={ErrorCode}, CorrelationId={CorrelationId}",
                        LoggingHelper.SanitizeLogInput(result.ErrorCode ?? "UNKNOWN"),
                        LoggingHelper.SanitizeLogInput(result.CorrelationId)
                    );

                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = result.ErrorCode ?? ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = result.ErrorMessage ?? "Request validation failed.",
                        Details = result.ReasonCodes.Any()
                            ? new Dictionary<string, object> { ["reasonCodes"] = result.ReasonCodes }
                            : null,
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = result.CorrelationId,
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                _logger.LogInformation(
                    "Issuance compliance decision issued: Decision={Decision}, Score={Score}, CorrelationId={CorrelationId}",
                    result.Decision,
                    result.AggregateRiskScore,
                    LoggingHelper.SanitizeLogInput(result.CorrelationId)
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error during issuance compliance evaluation. CorrelationId={CorrelationId}",
                    correlationId);

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred during compliance risk evaluation.",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = correlationId,
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path),
                    Retryable = true
                });
            }
        }
    }
}
