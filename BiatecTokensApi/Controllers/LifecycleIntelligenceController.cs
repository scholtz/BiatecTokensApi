using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.LifecycleIntelligence;
using BiatecTokensApi.Models.TokenLaunch;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Lifecycle Intelligence API for token readiness and risk signals
    /// </summary>
    /// <remarks>
    /// Provides expanded lifecycle intelligence endpoints including:
    /// - Enhanced readiness scoring with factor breakdowns (v2 API)
    /// - Evidence traceability and retrieval
    /// - Post-launch risk signal aggregation
    /// - Deterministic remediation recommendations
    /// - Future benchmark comparison hooks
    /// 
    /// All endpoints include comprehensive observability, structured errors,
    /// and backward-compatible versioning.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v2/lifecycle")]
    [Produces("application/json")]
    public class LifecycleIntelligenceController : ControllerBase
    {
        private readonly ILifecycleIntelligenceService _lifecycleService;
        private readonly ILogger<LifecycleIntelligenceController> _logger;

        public LifecycleIntelligenceController(
            ILifecycleIntelligenceService lifecycleService,
            ILogger<LifecycleIntelligenceController> logger)
        {
            _lifecycleService = lifecycleService;
            _logger = logger;
        }

        /// <summary>
        /// Evaluates token launch readiness with detailed factor breakdown (v2)
        /// </summary>
        /// <param name="request">Readiness evaluation request</param>
        /// <returns>Enhanced readiness assessment with factor scoring, confidence, and evidence</returns>
        /// <remarks>
        /// **V2 Enhancements:**
        /// - Weighted factor breakdown with individual scores and confidence levels
        /// - Explicit blocking conditions with resolution steps
        /// - Confidence metadata including data completeness and freshness
        /// - Evidence references for audit traceability
        /// - Placeholder for future benchmark comparisons
        /// 
        /// **Response includes:**
        /// - `readinessScore`: Overall score (0.0-1.0) with factor-level breakdown
        /// - `blockingConditions`: Explicit list of blocking factors with mandatory flag
        /// - `confidence`: Data quality, completeness, and freshness indicators
        /// - `evidenceReferences`: Traceable evidence for each evaluation
        /// - `caveats`: Important notes about evaluation limitations
        /// 
        /// **Backward Compatibility:**
        /// This endpoint coexists with v1 `/api/v1/token-launch/readiness`.
        /// Consumers can migrate incrementally.
        /// </remarks>
        [HttpPost("readiness")]
        [ProducesResponseType(typeof(TokenLaunchReadinessResponseV2), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EvaluateReadinessV2([FromBody] TokenLaunchReadinessRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.UserId))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "UserId is required",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                if (string.IsNullOrWhiteSpace(request.TokenType))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "TokenType is required",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                // Get authenticated user from claims
                var authenticatedUser = User.Identity?.Name;
                
                // Verify user is evaluating their own readiness
                if (authenticatedUser != request.UserId)
                {
                    _logger.LogWarning(
                        "User {AuthenticatedUser} attempted to evaluate readiness for {RequestedUser}",
                        LoggingHelper.SanitizeLogInput(authenticatedUser),
                        LoggingHelper.SanitizeLogInput(request.UserId));

                    return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.FORBIDDEN,
                        ErrorMessage = "You can only evaluate readiness for your own account",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var response = await _lifecycleService.EvaluateReadinessV2Async(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error evaluating token launch readiness (v2)");
                
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An unexpected error occurred during readiness evaluation",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        /// <summary>
        /// Retrieves evidence for a specific evaluation
        /// </summary>
        /// <param name="evidenceId">Evidence identifier</param>
        /// <param name="includeContent">Whether to include full evidence content (default: false)</param>
        /// <returns>Evidence reference with optional full content</returns>
        /// <remarks>
        /// Returns traceable evidence supporting evaluation decisions including:
        /// - Evidence metadata (source, collection time, validation status)
        /// - Data integrity hash for verification
        /// - Optional full evidence content (may be large)
        /// - Related evaluation ID
        /// 
        /// **Use Cases:**
        /// - Audit trail verification
        /// - Compliance documentation
        /// - Investigation of evaluation decisions
        /// </remarks>
        [HttpGet("evidence/{evidenceId}")]
        [ProducesResponseType(typeof(EvidenceRetrievalResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetEvidence(
            [FromRoute] string evidenceId,
            [FromQuery] bool includeContent = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(evidenceId))
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Evidence ID is required",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var response = await _lifecycleService.GetEvidenceAsync(evidenceId, includeContent);
                
                if (response == null || !response.Success)
                {
                    return NotFound(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.NOT_FOUND,
                        ErrorMessage = response?.ErrorMessage ?? $"Evidence not found: {evidenceId}",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evidence: {EvidenceId}",
                    LoggingHelper.SanitizeLogInput(evidenceId));
                
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving evidence",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        /// <summary>
        /// Retrieves post-launch risk signals for a token
        /// </summary>
        /// <param name="request">Risk signals request with filters</param>
        /// <returns>List of risk signals with severity, trend, and metadata</returns>
        /// <remarks>
        /// Provides operational risk monitoring for deployed tokens including:
        /// 
        /// **Risk Signal Types:**
        /// - **HolderConcentration**: High concentration of token holdings
        /// - **InactivityRisk**: Low or declining transaction activity
        /// - **AnomalousActivity**: Unusual transaction patterns
        /// - **LiquidityRisk**: Liquidity below healthy thresholds
        /// - **ChurnRisk**: High holder churn rate
        /// - **ComplianceRisk**: Compliance or regulatory concerns
        /// - **SecurityRisk**: Smart contract security concerns
        /// - **VolatilityRisk**: Market volatility or price instability
        /// - **TechnicalRisk**: Integration or technical health issues
        /// - **WhaleMovement**: Unusual whale activity
        /// 
        /// **Signal Attributes:**
        /// - `severity`: Info, Low, Medium, High, Critical
        /// - `trend`: Improving, Stable, Worsening, Unknown
        /// - `lastEvaluatedAt`: When signal was last calculated
        /// - `trendHistory`: Historical data points for trend analysis
        /// - `recommendedActions`: Suggested interventions
        /// 
        /// **Use Cases:**
        /// - Proactive operational monitoring
        /// - Early warning system for token health
        /// - Compliance risk detection
        /// - Portfolio risk assessment
        /// </remarks>
        [HttpPost("risk-signals")]
        [ProducesResponseType(typeof(RiskSignalsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRiskSignals([FromBody] RiskSignalsRequest request)
        {
            try
            {
                // Validate request
                if (request.Limit < 1 || request.Limit > 100)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Limit must be between 1 and 100",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var response = await _lifecycleService.GetRiskSignalsAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving risk signals for AssetId={AssetId}, Network={Network}",
                    request.AssetId,
                    LoggingHelper.SanitizeLogInput(request.Network ?? "unspecified"));
                
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving risk signals",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        /// <summary>
        /// Health check endpoint for lifecycle intelligence API
        /// </summary>
        /// <returns>API health status</returns>
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiStatusResponse), StatusCodes.Status200OK)]
        public IActionResult Health()
        {
            return Ok(new ApiStatusResponse
            {
                Status = "Healthy",
                Version = "v2.0",
                Timestamp = DateTime.UtcNow,
                Environment = "Production"
            });
        }
    }
}
