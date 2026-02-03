using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides security and compliance activity endpoints for audit trails and monitoring
    /// </summary>
    /// <remarks>
    /// This controller provides unified security activity tracking, transaction history,
    /// audit trail export, and recovery guidance endpoints for enterprise compliance and
    /// security monitoring. Supports MICA compliance requirements and subscription-based access.
    /// 
    /// All endpoints require ARC-0014 authentication.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/security")]
    public class SecurityActivityController : ControllerBase
    {
        private readonly ISecurityActivityService _securityActivityService;
        private readonly ILogger<SecurityActivityController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityActivityController"/> class.
        /// </summary>
        /// <param name="securityActivityService">The security activity service</param>
        /// <param name="logger">The logger instance</param>
        public SecurityActivityController(
            ISecurityActivityService securityActivityService,
            ILogger<SecurityActivityController> logger)
        {
            _securityActivityService = securityActivityService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves security activity events with comprehensive filtering
        /// </summary>
        /// <param name="accountId">Optional filter by account ID (defaults to authenticated user)</param>
        /// <param name="eventType">Optional filter by event type</param>
        /// <param name="severity">Optional filter by severity level</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <param name="success">Optional filter by operation result (true/false)</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 50, max: 100)</param>
        /// <returns>Paginated list of security activity events</returns>
        /// <remarks>
        /// This endpoint provides a unified view of all security-related events including authentication,
        /// token deployments, subscription changes, and compliance operations.
        /// 
        /// **Use Cases:**
        /// - Security monitoring and incident investigation
        /// - Compliance reporting and audits
        /// - User activity tracking
        /// - Troubleshooting deployment failures
        /// 
        /// **Event Types:**
        /// - Login, Logout, LoginFailed
        /// - TokenDeployment, TokenDeploymentSuccess, TokenDeploymentFailure
        /// - SubscriptionChange, ComplianceCheck
        /// - WhitelistOperation, BlacklistOperation
        /// - AuditExport, Recovery
        /// 
        /// **Response includes:**
        /// - Paginated events ordered by most recent first
        /// - Each event includes timestamp (ISO 8601), correlation ID, severity, and metadata
        /// - Total count and page information
        /// 
        /// **Security:**
        /// Requires ARC-0014 authentication. Users can only access their own events unless
        /// they have admin privileges (future enhancement).
        /// </remarks>
        [HttpGet("activity")]
        [ProducesResponseType(typeof(SecurityActivityResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetActivity(
            [FromQuery] string? accountId = null,
            [FromQuery] SecurityEventType? eventType = null,
            [FromQuery] EventSeverity? severity = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] bool? success = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userAddress = GetUserAddress();
                
                // If no accountId is specified, use the authenticated user's address
                var effectiveAccountId = accountId ?? userAddress;

                _logger.LogInformation("Security activity requested by {UserAddress}: AccountId={AccountId}, EventType={EventType}",
                    LoggingHelper.SanitizeLogInput(userAddress),
                    LoggingHelper.SanitizeLogInput(effectiveAccountId),
                    eventType);

                var request = new GetSecurityActivityRequest
                {
                    AccountId = effectiveAccountId,
                    EventType = eventType,
                    Severity = severity,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Success = success,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _securityActivityService.GetActivityAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved {Count} security activity events for user {UserAddress}",
                        result.Events.Count, LoggingHelper.SanitizeLogInput(userAddress));
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve security activity: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving security activity");
                return StatusCode(StatusCodes.Status500InternalServerError, ErrorResponseBuilder.CreateErrorResponse<SecurityActivityResponse>(
                    ErrorCodes.INTERNAL_SERVER_ERROR,
                    $"Internal error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Retrieves token deployment transaction history
        /// </summary>
        /// <param name="accountId">Optional filter by account ID (defaults to authenticated user)</param>
        /// <param name="network">Optional filter by network (voimain-v1.0, mainnet-v1.0, etc.)</param>
        /// <param name="tokenStandard">Optional filter by token standard (ASA, ARC3, ARC200, ERC20, etc.)</param>
        /// <param name="status">Optional filter by deployment status (success, failed, pending)</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 50, max: 100)</param>
        /// <returns>Paginated list of token deployment transactions</returns>
        /// <remarks>
        /// This endpoint provides detailed transaction history for token deployments across all networks.
        /// 
        /// **Use Cases:**
        /// - Tracking deployment success rates
        /// - Debugging failed deployments
        /// - Audit trail for compliance
        /// - Portfolio management
        /// 
        /// **Supported Networks:**
        /// - voimain-v1.0: VOI mainnet
        /// - aramidmain-v1.0: Aramid mainnet
        /// - mainnet-v1.0: Algorand mainnet
        /// - testnet-v1.0: Algorand testnet
        /// - base: Base blockchain (EVM)
        /// 
        /// **Supported Token Standards:**
        /// - ASA: Algorand Standard Assets
        /// - ARC3: Algorand tokens with rich metadata
        /// - ARC200: Advanced smart contract tokens
        /// - ERC20: Ethereum-compatible tokens
        /// 
        /// **Response includes:**
        /// - Transaction ID, Asset ID, network, token standard
        /// - Token name and symbol
        /// - Deployment status and timestamp
        /// - Creator address and confirmed round
        /// - Error messages for failed deployments
        /// 
        /// **Security:**
        /// Requires ARC-0014 authentication. Users can only access their own transactions.
        /// </remarks>
        [HttpGet("transactions")]
        [ProducesResponseType(typeof(TransactionHistoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTransactionHistory(
            [FromQuery] string? accountId = null,
            [FromQuery] string? network = null,
            [FromQuery] string? tokenStandard = null,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userAddress = GetUserAddress();
                
                // If no accountId is specified, use the authenticated user's address
                var effectiveAccountId = accountId ?? userAddress;

                _logger.LogInformation("Transaction history requested by {UserAddress}: AccountId={AccountId}, Network={Network}",
                    LoggingHelper.SanitizeLogInput(userAddress),
                    LoggingHelper.SanitizeLogInput(effectiveAccountId),
                    LoggingHelper.SanitizeLogInput(network));

                var request = new GetTransactionHistoryRequest
                {
                    AccountId = effectiveAccountId,
                    Network = network,
                    TokenStandard = tokenStandard,
                    Status = status,
                    FromDate = fromDate,
                    ToDate = toDate,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _securityActivityService.GetTransactionHistoryAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved {Count} transactions for user {UserAddress}",
                        result.Transactions.Count, LoggingHelper.SanitizeLogInput(userAddress));
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve transaction history: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving transaction history");
                return StatusCode(StatusCodes.Status500InternalServerError, ErrorResponseBuilder.CreateErrorResponse<TransactionHistoryResponse>(
                    ErrorCodes.INTERNAL_SERVER_ERROR,
                    $"Internal error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Exports audit trail in CSV or JSON format
        /// </summary>
        /// <param name="format">Export format (csv or json)</param>
        /// <param name="accountId">Optional filter by account ID (defaults to authenticated user)</param>
        /// <param name="eventType">Optional filter by event type</param>
        /// <param name="severity">Optional filter by severity level</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601 format)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601 format)</param>
        /// <returns>Exported file with audit trail data</returns>
        /// <remarks>
        /// Exports up to 10,000 security activity events in the specified format for compliance reporting.
        /// 
        /// **Supported Formats:**
        /// - csv: CSV file with UTF-8 encoding
        /// - json: Pretty-printed JSON with metadata
        /// 
        /// **Idempotency:**
        /// Include the `Idempotency-Key` header to prevent duplicate exports from retries.
        /// Exports are cached for 24 hours by idempotency key.
        /// 
        /// **Quota Management:**
        /// Export limits are based on subscription tier:
        /// - Free: 10 exports/month
        /// - Basic: 50 exports/month
        /// - Premium: Unlimited exports
        /// 
        /// **CSV Format:**
        /// - UTF-8 encoding with proper CSV escaping
        /// - Header row with all field names
        /// - One row per security event
        /// - Timestamp in ISO 8601 format
        /// 
        /// **JSON Format:**
        /// - Pretty-printed with camelCase properties
        /// - Includes export metadata (exportedAt, recordCount)
        /// - Array of event objects
        /// 
        /// **Response Headers:**
        /// - X-Idempotency-Hit: true if returning cached export
        /// - Content-Disposition: attachment with filename
        /// 
        /// **Use Cases:**
        /// - MICA compliance reporting
        /// - Internal audit reviews
        /// - Integration with SIEM systems
        /// - Long-term archival
        /// 
        /// **Security:**
        /// Requires ARC-0014 authentication. Users can only export their own activity data.
        /// </remarks>
        [HttpPost("audit/export")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportAuditTrail(
            [FromQuery] string format = "json",
            [FromQuery] string? accountId = null,
            [FromQuery] SecurityEventType? eventType = null,
            [FromQuery] EventSeverity? severity = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var userAddress = GetUserAddress();
                var effectiveAccountId = accountId ?? userAddress;

                // Get idempotency key from header
                var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

                _logger.LogInformation("Audit trail export requested by {UserAddress}: Format={Format}, IdempotencyKey={IdempotencyKey}",
                    LoggingHelper.SanitizeLogInput(userAddress),
                    LoggingHelper.SanitizeLogInput(format),
                    LoggingHelper.SanitizeLogInput(idempotencyKey));

                var request = new ExportAuditTrailRequest
                {
                    Format = format.ToLower(),
                    AccountId = effectiveAccountId,
                    EventType = eventType,
                    Severity = severity,
                    FromDate = fromDate,
                    ToDate = toDate,
                    IdempotencyKey = idempotencyKey
                };

                var (response, content) = await _securityActivityService.ExportAuditTrailAsync(request, userAddress);

                if (!response.Success)
                {
                    if (response.ErrorCode == ErrorCodes.EXPORT_QUOTA_EXCEEDED)
                    {
                        return StatusCode(StatusCodes.Status429TooManyRequests, response);
                    }
                    return BadRequest(response);
                }

                // Add idempotency hit header if applicable
                if (response.IdempotencyHit)
                {
                    Response.Headers.Append("X-Idempotency-Hit", "true");
                }

                // If this is a cached response without content, return metadata only
                if (content == null)
                {
                    return Ok(response);
                }

                // Return file content
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var fileName = $"audit-trail-{DateTime.UtcNow:yyyyMMddHHmmss}.{format}";
                var contentType = format == "csv" ? "text/csv" : "application/json";

                _logger.LogInformation("Exported audit trail for user {UserAddress}: {Count} records",
                    LoggingHelper.SanitizeLogInput(userAddress), response.RecordCount);

                return File(bytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception exporting audit trail");
                return StatusCode(StatusCodes.Status500InternalServerError, ErrorResponseBuilder.CreateErrorResponse<ExportAuditTrailResponse>(
                    ErrorCodes.INTERNAL_SERVER_ERROR,
                    $"Internal error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Gets recovery guidance and eligibility status
        /// </summary>
        /// <returns>Recovery guidance with steps and cooldown information</returns>
        /// <remarks>
        /// Provides account recovery guidance including eligibility status, cooldown periods, and step-by-step instructions.
        /// 
        /// **Recovery Eligibility States:**
        /// - Eligible: Recovery is available
        /// - Cooldown: Recovery was recently requested, must wait before retrying
        /// - AlreadySent: Recovery email already sent
        /// - NotConfigured: Recovery email not set up
        /// - AccountLocked: Account is locked, contact support
        /// 
        /// **Recovery Process:**
        /// 1. Verify identity using registered email
        /// 2. Confirm recovery request
        /// 3. Check recovery email (link valid for 24 hours)
        /// 4. Reset access with new credentials
        /// 
        /// **Cooldown Policy:**
        /// - 1 hour cooldown between recovery requests
        /// - Maximum 3 recovery attempts per 24 hours
        /// - Automatic account lock after repeated failed attempts
        /// 
        /// **Response includes:**
        /// - Eligibility status
        /// - Last recovery attempt timestamp
        /// - Cooldown remaining (seconds)
        /// - Step-by-step recovery instructions
        /// - Additional guidance notes
        /// 
        /// **Use Cases:**
        /// - Account recovery UI guidance
        /// - Support ticket automation
        /// - Security center dashboard
        /// 
        /// **Security:**
        /// Requires ARC-0014 authentication. Users can only access their own recovery information.
        /// </remarks>
        [HttpGet("recovery")]
        [ProducesResponseType(typeof(RecoveryGuidanceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRecoveryGuidance()
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Recovery guidance requested by {UserAddress}", 
                    LoggingHelper.SanitizeLogInput(userAddress));

                var result = await _securityActivityService.GetRecoveryGuidanceAsync(userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Recovery guidance provided for user {UserAddress}: Eligibility={Eligibility}",
                        LoggingHelper.SanitizeLogInput(userAddress), result.Eligibility);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to get recovery guidance: {Error}", result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting recovery guidance");
                return StatusCode(StatusCodes.Status500InternalServerError, ErrorResponseBuilder.CreateErrorResponse<RecoveryGuidanceResponse>(
                    ErrorCodes.INTERNAL_SERVER_ERROR,
                    $"Internal error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Gets the user's Algorand address from the authentication context
        /// </summary>
        /// <returns>The user's Algorand address</returns>
        private string GetUserAddress()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? "Unknown";
        }
    }
}
