using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenLaunch;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for token launch readiness evaluation
    /// </summary>
    /// <remarks>
    /// Provides comprehensive compliance decisioning and evidence orchestration for regulated token issuance.
    /// Aggregates subscription entitlements, account readiness, compliance decisions, KYC/AML status,
    /// jurisdiction constraints, and integration health into deterministic launch readiness assessment.
    /// All evaluations are auditable with timestamped evidence snapshots.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/token-launch")]
    [ApiExplorerSettings(IgnoreApi = true)] // TODO: Fix JsonElement schema generation for Swagger - see https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/2479
    public class TokenLaunchReadinessController : ControllerBase
    {
        private readonly ITokenLaunchReadinessService _readinessService;
        private readonly ILogger<TokenLaunchReadinessController> _logger;

        public TokenLaunchReadinessController(
            ITokenLaunchReadinessService readinessService,
            ILogger<TokenLaunchReadinessController> logger)
        {
            _readinessService = readinessService;
            _logger = logger;
        }

        /// <summary>
        /// Evaluates token launch readiness with comprehensive compliance checks
        /// </summary>
        /// <param name="request">Readiness evaluation request</param>
        /// <returns>Comprehensive readiness assessment with remediation guidance</returns>
        /// <remarks>
        /// Performs deterministic evaluation of all compliance requirements for token launch:
        /// 
        /// **Evaluation Categories:**
        /// - **Subscription Entitlement**: Tier limits, feature access, deployment quotas
        /// - **Account Readiness**: ARC76 initialization, key accessibility, metadata validity
        /// - **Compliance Decisions**: Policy-driven approval/rejection based on evidence
        /// - **KYC/AML**: Identity verification status (advisory)
        /// - **Jurisdiction**: Geographic constraints and regulatory requirements
        /// - **Whitelist**: Token transfer restrictions and investor eligibility
        /// - **Integration Health**: Blockchain connectivity and service availability
        /// 
        /// **Response Structure:**
        /// - `status`: Overall readiness (Ready, Blocked, Warning, NeedsReview)
        /// - `canProceed`: Boolean indicating if launch is allowed
        /// - `summary`: Human-readable status description
        /// - `remediationTasks`: Ordered list of actions to resolve blockers
        /// - `details`: Category-by-category evaluation results
        /// 
        /// **Remediation Tasks:**
        /// Each task includes:
        /// - Category and error code
        /// - Severity level (Critical, High, Medium, Low, Info)
        /// - Owner hint (Account Owner, User, Compliance Team, Technical Support)
        /// - Ordered action steps
        /// - Estimated resolution time
        /// - Dependency relationships
        /// 
        /// **Evidence Persistence:**
        /// All evaluations are stored with immutable audit trail including:
        /// - Request and response snapshots
        /// - Timestamp and correlation ID
        /// - Category-level results
        /// - Data integrity hash
        /// 
        /// **Example Request:**
        /// ```json
        /// {
        ///   "userId": "user-123",
        ///   "tokenType": "ARC3",
        ///   "network": "mainnet",
        ///   "deploymentContext": {
        ///     "name": "MyToken",
        ///     "symbol": "MTK"
        ///   }
        /// }
        /// ```
        /// 
        /// **Example Response (Ready):**
        /// ```json
        /// {
        ///   "evaluationId": "eval-456",
        ///   "status": "Ready",
        ///   "canProceed": true,
        ///   "summary": "All requirements met. Token launch can proceed.",
        ///   "remediationTasks": [],
        ///   "details": {
        ///     "entitlement": { "passed": true, "message": "Premium tier allows deployment" },
        ///     "accountReadiness": { "passed": true, "message": "Account ready" },
        ///     "kycAml": { "passed": true, "message": "Verification complete" }
        ///   },
        ///   "policyVersion": "2026.02.16.1",
        ///   "evaluatedAt": "2026-02-16T10:00:00Z",
        ///   "evaluationTimeMs": 245
        /// }
        /// ```
        /// 
        /// **Example Response (Blocked):**
        /// ```json
        /// {
        ///   "evaluationId": "eval-789",
        ///   "status": "Blocked",
        ///   "canProceed": false,
        ///   "summary": "Token launch blocked by 2 critical issue(s)",
        ///   "remediationTasks": [
        ///     {
        ///       "category": "Entitlement",
        ///       "errorCode": "ENTITLEMENT_LIMIT_EXCEEDED",
        ///       "description": "Free tier deployment limit reached (3/3)",
        ///       "severity": "Critical",
        ///       "ownerHint": "Account Owner",
        ///       "actions": [
        ///         "Upgrade to Basic tier for 10 deployments",
        ///         "Contact sales for enterprise options"
        ///       ],
        ///       "estimatedResolutionHours": 1
        ///     }
        ///   ]
        /// }
        /// ```
        /// </remarks>
        [HttpPost("readiness")]
        [ProducesResponseType(typeof(TokenLaunchReadinessResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EvaluateReadiness([FromBody] TokenLaunchReadinessRequest request)
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

                var response = await _readinessService.EvaluateReadinessAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error evaluating token launch readiness");
                
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
        /// Retrieves a specific readiness evaluation by ID
        /// </summary>
        /// <param name="evaluationId">Evaluation identifier</param>
        /// <returns>Token launch readiness evaluation</returns>
        /// <remarks>
        /// Returns a previously conducted readiness evaluation including all assessment details,
        /// remediation tasks, and evidence snapshots. Useful for audit trails and tracking
        /// compliance status over time.
        /// </remarks>
        [HttpGet("readiness/{evaluationId}")]
        [ProducesResponseType(typeof(TokenLaunchReadinessResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetEvaluation([FromRoute] string evaluationId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(evaluationId))
                {
                    return BadRequest(new { error = "Evaluation ID is required" });
                }

                var evaluation = await _readinessService.GetEvaluationAsync(evaluationId);
                
                if (evaluation == null)
                {
                    _logger.LogWarning("Evaluation not found: {EvaluationId}",
                        LoggingHelper.SanitizeLogInput(evaluationId));
                    return NotFound(new { error = $"Evaluation not found: {evaluationId}" });
                }

                return Ok(evaluation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation: {EvaluationId}",
                    LoggingHelper.SanitizeLogInput(evaluationId));
                
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An error occurred while retrieving the evaluation" });
            }
        }

        /// <summary>
        /// Retrieves evaluation history for the authenticated user
        /// </summary>
        /// <param name="limit">Maximum number of results (default: 50, max: 100)</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601)</param>
        /// <returns>List of historical readiness evaluations</returns>
        /// <remarks>
        /// Returns historical readiness evaluations for audit and trend analysis.
        /// Evaluations are ordered by most recent first.
        /// 
        /// **Use Cases:**
        /// - Track compliance status over time
        /// - Identify recurring blockers
        /// - Generate audit reports
        /// - Monitor remediation progress
        /// </remarks>
        [HttpGet("readiness/history")]
        [ProducesResponseType(typeof(List<TokenLaunchReadinessResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetEvaluationHistory(
            [FromQuery] int limit = 50,
            [FromQuery] DateTime? fromDate = null)
        {
            try
            {
                // Validate limit
                if (limit < 1 || limit > 100)
                {
                    return BadRequest(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.INVALID_REQUEST,
                        ErrorMessage = "Limit must be between 1 and 100",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                // Get authenticated user
                var userId = User.Identity?.Name;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.UNAUTHORIZED,
                        ErrorMessage = "Authentication required",
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var history = await _readinessService.GetEvaluationHistoryAsync(userId, limit, fromDate);
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation history");
                
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "An error occurred while retrieving evaluation history",
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }
    }
}
