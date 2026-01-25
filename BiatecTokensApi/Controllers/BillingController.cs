using BiatecTokensApi.Models.Billing;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for subscription usage metering and plan limit management
    /// </summary>
    /// <remarks>
    /// This controller implements the subscription-funded platform vision with enterprise governance.
    /// It tracks per-tenant usage across token issuance, transfers, audit exports, and storage,
    /// enforces plan limits with clear error codes, and provides admin endpoints for plan management.
    /// All endpoints require ARC-0014 authentication with role checks for admin operations.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/billing")]
    public class BillingController : ControllerBase
    {
        private readonly IBillingService _billingService;
        private readonly ILogger<BillingController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BillingController"/> class.
        /// </summary>
        /// <param name="billingService">The billing service</param>
        /// <param name="logger">The logger instance</param>
        public BillingController(
            IBillingService billingService,
            ILogger<BillingController> logger)
        {
            _billingService = billingService;
            _logger = logger;
        }

        /// <summary>
        /// Gets usage summary for the authenticated tenant
        /// </summary>
        /// <returns>Usage summary with current plan limits and period statistics</returns>
        /// <remarks>
        /// This endpoint provides a comprehensive view of usage across all metered operations
        /// for the current billing period. It includes:
        /// - Token issuance count
        /// - Transfer validation count
        /// - Audit export count
        /// - Storage items count
        /// - Compliance operation count
        /// - Whitelist operation count
        /// - Current plan limits
        /// - Limit violation warnings
        /// 
        /// **Authentication:**
        /// Requires ARC-0014 authentication. The authenticated user's address is used as the tenant identifier.
        /// 
        /// **Billing Period:**
        /// Usage is tracked per calendar month. The period automatically resets on the first day of each month.
        /// 
        /// **Use Cases:**
        /// - Billing dashboard display
        /// - Usage analytics and forecasting
        /// - Limit monitoring and alerts
        /// - Compliance reporting
        /// </remarks>
        [HttpGet("usage")]
        [ProducesResponseType(typeof(UsageSummaryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUsageSummary()
        {
            try
            {
                var tenantAddress = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(tenantAddress))
                {
                    _logger.LogWarning("GetUsageSummary called without authenticated user");
                    return Unauthorized(new UsageSummaryResponse
                    {
                        Success = false,
                        ErrorMessage = "Authentication required"
                    });
                }

                var summary = await _billingService.GetUsageSummaryAsync(tenantAddress);

                _logger.LogInformation(
                    "Usage summary retrieved for tenant {TenantAddress}",
                    tenantAddress);

                return Ok(new UsageSummaryResponse
                {
                    Success = true,
                    Data = summary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving usage summary");
                return StatusCode(StatusCodes.Status500InternalServerError, new UsageSummaryResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Checks if a planned operation would exceed plan limits (preflight check)
        /// </summary>
        /// <param name="request">The limit check request specifying operation type and count</param>
        /// <returns>Limit check result indicating if operation is allowed</returns>
        /// <remarks>
        /// This endpoint performs a preflight check to determine if a planned operation
        /// would exceed the tenant's plan limits. It does NOT record usage or perform
        /// the actual operation - it only checks if the operation would be allowed.
        /// 
        /// **Authentication:**
        /// Requires ARC-0014 authentication. The authenticated user's address is used as the tenant identifier.
        /// 
        /// **Operation Types:**
        /// - TokenIssuance: Token creation/deployment operations
        /// - TransferValidation: Transfer validation checks
        /// - AuditExport: Audit log export operations
        /// - Storage: Storage item additions (whitelist entries, compliance metadata)
        /// - ComplianceOperation: Compliance metadata operations
        /// - WhitelistOperation: Whitelist management operations
        /// 
        /// **Response:**
        /// - IsAllowed: Whether the operation is permitted
        /// - CurrentUsage: Current usage count for this operation type
        /// - MaxAllowed: Maximum allowed operations (-1 for unlimited)
        /// - RemainingCapacity: Available capacity (-1 for unlimited)
        /// - DenialReason: Explanation if operation is denied
        /// - ErrorCode: "LIMIT_EXCEEDED" if denied due to limit
        /// 
        /// **Error Handling:**
        /// When limits are exceeded, the response includes:
        /// - Clear error message explaining the violation
        /// - Current usage vs limit details
        /// - Suggestion to upgrade subscription
        /// - Audit log entry for compliance review
        /// 
        /// **Use Cases:**
        /// - Pre-flight validation before expensive operations
        /// - UI elements showing available capacity
        /// - Preventing unnecessary API calls that would fail
        /// - Billing reconciliation and limit enforcement
        /// </remarks>
        [HttpPost("limits/check")]
        [ProducesResponseType(typeof(LimitCheckResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CheckLimits([FromBody] LimitCheckRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var tenantAddress = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(tenantAddress))
                {
                    _logger.LogWarning("CheckLimits called without authenticated user");
                    return Unauthorized(new LimitCheckResponse
                    {
                        IsAllowed = false,
                        ErrorCode = "UNAUTHORIZED",
                        DenialReason = "Authentication required"
                    });
                }

                var result = await _billingService.CheckLimitAsync(tenantAddress, request);

                if (!result.IsAllowed)
                {
                    _logger.LogWarning(
                        "Limit check denied for tenant {TenantAddress}: {OperationType}",
                        tenantAddress, request.OperationType);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking limits");
                return StatusCode(StatusCodes.Status500InternalServerError, new LimitCheckResponse
                {
                    IsAllowed = false,
                    ErrorCode = "INTERNAL_ERROR",
                    DenialReason = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Updates plan limits for a specific tenant (admin only)
        /// </summary>
        /// <param name="tenantAddress">The tenant's Algorand address</param>
        /// <param name="request">The update request with new plan limits</param>
        /// <returns>Success status and updated limits</returns>
        /// <remarks>
        /// This endpoint allows administrators to customize plan limits for specific tenants,
        /// overriding the default tier-based limits. Changes are logged to the audit repository
        /// for compliance review and billing reconciliation.
        /// 
        /// **Authorization:**
        /// Requires ARC-0014 authentication AND admin role. Only addresses configured as admins
        /// (currently the app account) can call this endpoint. Non-admin attempts are logged
        /// and return a 403 Forbidden response.
        /// 
        /// **Plan Limits:**
        /// All limits use -1 to indicate unlimited capacity:
        /// - MaxTokenIssuance: Maximum tokens that can be issued per period
        /// - MaxTransferValidations: Maximum transfer validations per period
        /// - MaxAuditExports: Maximum audit exports per period
        /// - MaxStorageItems: Maximum storage items (whitelist entries, etc.)
        /// - MaxComplianceOperations: Maximum compliance operations per period
        /// - MaxWhitelistOperations: Maximum whitelist operations per period
        /// 
        /// **Audit Trail:**
        /// Every plan limit update creates an audit log entry containing:
        /// - Admin who performed the update
        /// - Tenant whose limits were changed
        /// - Timestamp of the change
        /// - Optional notes explaining the change
        /// - All new limit values
        /// 
        /// **Use Cases:**
        /// - Custom enterprise agreements
        /// - Temporary limit increases for special events
        /// - Granular control over tenant capabilities
        /// - Compliance with custom SLAs
        /// - Testing and development environments
        /// 
        /// **Security:**
        /// Failed authorization attempts are logged for security monitoring.
        /// Admin status is determined by comparing authenticated address against
        /// configured admin addresses.
        /// </remarks>
        [HttpPut("limits/{tenantAddress}")]
        [ProducesResponseType(typeof(PlanLimitsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdatePlanLimits(
            [FromRoute] string tenantAddress,
            [FromBody] UpdatePlanLimitsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var performedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(performedBy))
                {
                    _logger.LogWarning("UpdatePlanLimits called without authenticated user");
                    return Unauthorized(new PlanLimitsResponse
                    {
                        Success = false,
                        ErrorMessage = "Authentication required"
                    });
                }

                // Check admin authorization
                if (!_billingService.IsAdmin(performedBy))
                {
                    _logger.LogWarning(
                        "Unauthorized plan limit update attempt by {PerformedBy} for tenant {TenantAddress}",
                        performedBy, tenantAddress);

                    return StatusCode(StatusCodes.Status403Forbidden, new PlanLimitsResponse
                    {
                        Success = false,
                        ErrorMessage = "Forbidden. Only admins can update plan limits."
                    });
                }

                // Ensure tenant address in request matches route parameter
                request.TenantAddress = tenantAddress;

                var result = await _billingService.UpdatePlanLimitsAsync(request, performedBy);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Plan limits updated for tenant {TenantAddress} by admin {PerformedBy}",
                        tenantAddress, performedBy);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating plan limits");
                return StatusCode(StatusCodes.Status500InternalServerError, new PlanLimitsResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets current plan limits for the authenticated tenant
        /// </summary>
        /// <returns>Current plan limits</returns>
        /// <remarks>
        /// This endpoint retrieves the current plan limits for the authenticated tenant.
        /// Limits may be tier-based defaults or custom limits set by an administrator.
        /// 
        /// **Authentication:**
        /// Requires ARC-0014 authentication. The authenticated user's address is used as the tenant identifier.
        /// 
        /// **Limit Sources:**
        /// 1. Custom limits (set via admin endpoint) - highest priority
        /// 2. Tier-based defaults (from subscription tier)
        /// 
        /// **Response:**
        /// Returns PlanLimits object with all limit values. A value of -1 indicates unlimited capacity.
        /// 
        /// **Use Cases:**
        /// - Displaying current plan details in UI
        /// - Determining available features
        /// - Planning resource usage
        /// - Upgrade decision support
        /// </remarks>
        [HttpGet("limits")]
        [ProducesResponseType(typeof(PlanLimitsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPlanLimits()
        {
            try
            {
                var tenantAddress = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(tenantAddress))
                {
                    _logger.LogWarning("GetPlanLimits called without authenticated user");
                    return Unauthorized(new PlanLimitsResponse
                    {
                        Success = false,
                        ErrorMessage = "Authentication required"
                    });
                }

                var limits = await _billingService.GetPlanLimitsAsync(tenantAddress);

                _logger.LogInformation(
                    "Plan limits retrieved for tenant {TenantAddress}",
                    tenantAddress);

                return Ok(new PlanLimitsResponse
                {
                    Success = true,
                    Limits = limits
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving plan limits");
                return StatusCode(StatusCodes.Status500InternalServerError, new PlanLimitsResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Records usage for billing purposes (manual usage tracking)
        /// </summary>
        /// <param name="request">The usage recording request</param>
        /// <returns>Success status with updated usage information</returns>
        /// <remarks>
        /// This endpoint allows manual recording of usage events for billing purposes.
        /// It is useful for tracking operations that are not automatically captured by the platform,
        /// or for external systems that need to report usage.
        /// 
        /// **Authentication:**
        /// Requires ARC-0014 authentication. The authenticated user's address is used as the tenant identifier.
        /// 
        /// **Recording Behavior:**
        /// - Records the specified number of operations for the given operation type
        /// - Returns the updated usage and remaining capacity
        /// - Emits audit events for MICA/RWA auditability
        /// - Provides warnings when approaching limits (80% capacity)
        /// 
        /// **Quota Enforcement:**
        /// - This endpoint does NOT enforce quotas - it records usage regardless of limits
        /// - Use the preflight check endpoint (POST /api/v1/billing/limits/check) to verify limits before operations
        /// - The response includes current usage and remaining capacity for awareness
        /// - A warning message is returned if usage exceeds 80% of the limit
        /// 
        /// **Audit Events:**
        /// All usage recording operations are logged with structured logging for:
        /// - Compliance reporting (MICA, RWA)
        /// - Billing reconciliation
        /// - Usage analytics and forecasting
        /// - Fraud detection and monitoring
        /// 
        /// **Use Cases:**
        /// - Manual correction of usage data
        /// - External system integration (e.g., off-chain operations)
        /// - Batch processing of historical operations
        /// - Custom metering for specialized features
        /// - Testing and development environments
        /// 
        /// **Security:**
        /// - Only the authenticated tenant can record usage for themselves
        /// - All operations are logged for audit purposes
        /// - Rate limiting may apply to prevent abuse
        /// </remarks>
        [HttpPost("usage/record")]
        [ProducesResponseType(typeof(RecordUsageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RecordUsage([FromBody] RecordUsageRequest request)
        {
            string? tenantAddress = null;
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                tenantAddress = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(tenantAddress))
                {
                    _logger.LogWarning("RecordUsage called without authenticated user");
                    return Unauthorized(new RecordUsageResponse
                    {
                        Success = false,
                        ErrorMessage = "Authentication required"
                    });
                }

                // Record the usage
                await _billingService.RecordUsageAsync(
                    tenantAddress, 
                    request.OperationType, 
                    request.OperationCount);

                // Get updated usage information
                var summary = await _billingService.GetUsageSummaryAsync(tenantAddress);
                var limits = await _billingService.GetPlanLimitsAsync(tenantAddress);

                // Determine current usage and max allowed based on operation type
                int currentUsage = 0;
                int maxAllowed = -1;

                switch (request.OperationType)
                {
                    case OperationType.TokenIssuance:
                        currentUsage = summary.TokenIssuanceCount;
                        maxAllowed = limits.MaxTokenIssuance;
                        break;
                    case OperationType.TransferValidation:
                        currentUsage = summary.TransferValidationCount;
                        maxAllowed = limits.MaxTransferValidations;
                        break;
                    case OperationType.AuditExport:
                        currentUsage = summary.AuditExportCount;
                        maxAllowed = limits.MaxAuditExports;
                        break;
                    case OperationType.Storage:
                        currentUsage = summary.StorageItemsCount;
                        maxAllowed = limits.MaxStorageItems;
                        break;
                    case OperationType.ComplianceOperation:
                        currentUsage = summary.ComplianceOperationCount;
                        maxAllowed = limits.MaxComplianceOperations;
                        break;
                    case OperationType.WhitelistOperation:
                        currentUsage = summary.WhitelistOperationCount;
                        maxAllowed = limits.MaxWhitelistOperations;
                        break;
                }

                int remainingCapacity = maxAllowed == -1 ? -1 : Math.Max(0, maxAllowed - currentUsage);

                // Check if approaching limits (80% threshold)
                string? warningMessage = null;
                if (maxAllowed > 0 && currentUsage >= (maxAllowed * 0.8))
                {
                    double percentUsed = (double)currentUsage / maxAllowed * 100;
                    warningMessage = 
                        $"Warning: You have used {percentUsed:F1}% of your {request.OperationType} quota. " +
                        $"Current usage: {currentUsage}/{maxAllowed}. Consider upgrading your subscription.";
                }

                // Emit audit event
                _logger.LogInformation(
                    "BILLING_AUDIT: UsageRecorded | Tenant: {TenantAddress} | OperationType: {OperationType} | " +
                    "Count: {Count} | AssetId: {AssetId} | Network: {Network} | Notes: {Notes} | " +
                    "CurrentUsage: {CurrentUsage} | MaxAllowed: {MaxAllowed}",
                    tenantAddress, request.OperationType, request.OperationCount,
                    request.AssetId, request.Network ?? "N/A", request.Notes ?? "N/A",
                    currentUsage, maxAllowed);

                return Ok(new RecordUsageResponse
                {
                    Success = true,
                    RecordedCount = request.OperationCount,
                    CurrentUsage = currentUsage,
                    MaxAllowed = maxAllowed,
                    RemainingCapacity = remainingCapacity,
                    WarningMessage = warningMessage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording usage for tenant {TenantAddress}", tenantAddress);
                return StatusCode(StatusCodes.Status500InternalServerError, new RecordUsageResponse
                {
                    Success = false,
                    ErrorMessage = "An error occurred while recording usage. Please try again later."
                });
            }
        }
    }
}
