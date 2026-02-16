using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ARC76;
using BiatecTokensApi.Models.Entitlement;
using BiatecTokensApi.Models.Preflight;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for preflight readiness checks before token issuance operations
    /// </summary>
    /// <remarks>
    /// Provides deterministic preflight evaluation combining subscription entitlement checks
    /// and ARC76 account readiness validation. Returns comprehensive readiness assessment
    /// with actionable guidance for blocked operations.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/preflight")]
    public class PreflightController : ControllerBase
    {
        private readonly IEntitlementEvaluationService _entitlementService;
        private readonly IARC76AccountReadinessService _arc76ReadinessService;
        private readonly ILogger<PreflightController> _logger;

        public PreflightController(
            IEntitlementEvaluationService entitlementService,
            IARC76AccountReadinessService arc76ReadinessService,
            ILogger<PreflightController> logger)
        {
            _entitlementService = entitlementService;
            _arc76ReadinessService = arc76ReadinessService;
            _logger = logger;
        }

        /// <summary>
        /// Performs a preflight readiness check for a specific operation
        /// </summary>
        /// <param name="request">Preflight check request</param>
        /// <returns>Comprehensive readiness assessment</returns>
        /// <remarks>
        /// This endpoint evaluates both subscription entitlements and ARC76 account readiness
        /// to provide a complete picture of whether an operation can proceed. It returns:
        /// - Entitlement status (subscription limits, feature access)
        /// - ARC76 account readiness (initialization, key accessibility, metadata validity)
        /// - Specific blockers if operation cannot proceed
        /// - Upgrade recommendations if denial is due to subscription limits
        /// - Remediation steps for account issues
        /// 
        /// **Performance Target:** &lt;300ms for standard load profile
        /// 
        /// **Example Request:**
        /// ```json
        /// {
        ///   "operation": "TokenDeployment",
        ///   "operationContext": {
        ///     "tokenType": "ARC3"
        ///   }
        /// }
        /// ```
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(PreflightCheckResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CheckReadiness([FromBody] PreflightCheckRequest request)
        {
            var sw = Stopwatch.StartNew();
            var correlationId = HttpContext.TraceIdentifier;

            try
            {
                // Get user identifier from claims
                var userId = User.Identity?.Name;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Preflight check attempted without authentication. CorrelationId: {CorrelationId}",
                        correlationId);

                    return Unauthorized(new ApiErrorResponse
                    {
                        Success = false,
                        ErrorCode = ErrorCodes.UNAUTHORIZED,
                        ErrorMessage = "Authentication required",
                        RemediationHint = "Provide a valid authentication token in the Authorization header",
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = correlationId,
                        Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                    });
                }

                var sanitizedUserId = LoggingHelper.SanitizeLogInput(userId);

                _logger.LogInformation(
                    "Preflight check requested by user {UserId} for operation {Operation}. CorrelationId: {CorrelationId}",
                    sanitizedUserId, request.Operation, correlationId);

                // Perform entitlement check
                var entitlementRequest = new EntitlementCheckRequest
                {
                    UserId = userId,
                    Operation = request.Operation,
                    OperationContext = request.OperationContext,
                    CorrelationId = correlationId
                };

                var entitlementResult = await _entitlementService.CheckEntitlementAsync(entitlementRequest);

                // Perform ARC76 account readiness check
                var accountReadiness = await _arc76ReadinessService.CheckAccountReadinessAsync(userId, correlationId);

                // Build response
                var response = new PreflightCheckResponse
                {
                    SubscriptionTier = entitlementResult.SubscriptionTier,
                    EntitlementCheck = entitlementResult,
                    AccountReadiness = accountReadiness,
                    PolicyVersion = entitlementResult.PolicyVersion,
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = correlationId
                };

                // Determine overall readiness
                response.IsReady = entitlementResult.IsAllowed && accountReadiness.IsReady;

                // Build blockers list
                if (!entitlementResult.IsAllowed)
                {
                    response.Blockers.Add(new ReadinessBlocker
                    {
                        Type = entitlementResult.DenialCode == ErrorCodes.FEATURE_NOT_INCLUDED
                            ? BlockerType.FeatureAccess
                            : BlockerType.Entitlement,
                        ErrorCode = entitlementResult.DenialCode ?? ErrorCodes.ENTITLEMENT_LIMIT_EXCEEDED,
                        Description = entitlementResult.DenialReason ?? "Entitlement check failed",
                        RemediationSteps = new List<string> { "Upgrade your subscription to access this feature" },
                        Severity = BlockerSeverity.High
                    });

                    response.UpgradeRecommendation = entitlementResult.UpgradeRecommendation;
                }

                if (!accountReadiness.IsReady)
                {
                    var blocker = new ReadinessBlocker
                    {
                        Type = BlockerType.AccountState,
                        ErrorCode = GetAccountReadinessErrorCode(accountReadiness.State),
                        Description = accountReadiness.NotReadyReason ?? "Account is not ready",
                        RemediationSteps = accountReadiness.RemediationSteps ?? new List<string>(),
                        Severity = GetBlockerSeverity(accountReadiness.State)
                    };

                    response.Blockers.Add(blocker);
                }

                sw.Stop();
                response.ResponseTimeMs = sw.ElapsedMilliseconds;

                _logger.LogInformation(
                    "Preflight check completed for user {UserId}: IsReady={IsReady}, " +
                    "EntitlementAllowed={EntitlementAllowed}, AccountReady={AccountReady}, " +
                    "ResponseTime={ResponseTimeMs}ms, CorrelationId: {CorrelationId}",
                    sanitizedUserId, response.IsReady, entitlementResult.IsAllowed, 
                    accountReadiness.IsReady, response.ResponseTimeMs, correlationId);

                // Log warning if response time exceeds target
                if (response.ResponseTimeMs > 300)
                {
                    _logger.LogWarning(
                        "Preflight check exceeded 300ms target: {ResponseTimeMs}ms. User: {UserId}, CorrelationId: {CorrelationId}",
                        response.ResponseTimeMs, sanitizedUserId, correlationId);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Error during preflight check. CorrelationId: {CorrelationId}", correlationId);

                return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR,
                    ErrorMessage = "Error performing preflight check",
                    RemediationHint = "Retry the operation. Contact support if issue persists.",
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = correlationId,
                    Path = LoggingHelper.SanitizeLogInput(HttpContext.Request.Path)
                });
            }
        }

        #region Private Helper Methods

        private string GetAccountReadinessErrorCode(ARC76ReadinessState state)
        {
            return state switch
            {
                ARC76ReadinessState.NotInitialized => ErrorCodes.ACCOUNT_NOT_READY,
                ARC76ReadinessState.Initializing => ErrorCodes.ACCOUNT_INITIALIZING,
                ARC76ReadinessState.Degraded => ErrorCodes.ACCOUNT_DEGRADED,
                ARC76ReadinessState.Failed => ErrorCodes.ACCOUNT_INITIALIZATION_FAILED,
                _ => ErrorCodes.ACCOUNT_NOT_READY
            };
        }

        private BlockerSeverity GetBlockerSeverity(ARC76ReadinessState state)
        {
            return state switch
            {
                ARC76ReadinessState.NotInitialized => BlockerSeverity.High,
                ARC76ReadinessState.Initializing => BlockerSeverity.Medium,
                ARC76ReadinessState.Degraded => BlockerSeverity.Medium,
                ARC76ReadinessState.Failed => BlockerSeverity.Critical,
                _ => BlockerSeverity.High
            };
        }

        #endregion
    }
}
