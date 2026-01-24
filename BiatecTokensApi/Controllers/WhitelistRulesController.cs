using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for managing RWA token whitelisting rules (MICA-compliant)
    /// </summary>
    /// <remarks>
    /// This controller manages whitelisting rules operations for RWA tokens including creating, updating,
    /// listing, applying, and deleting rules. All endpoints require ARC-0014 authentication.
    /// Rules enable automated compliance policies aligned with MICA (Markets in Crypto-Assets) regulation.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/whitelist-rules")]
    public class WhitelistRulesController : ControllerBase
    {
        private readonly IWhitelistRulesService _rulesService;
        private readonly ILogger<WhitelistRulesController> _logger;

        /// <summary>
        /// Maximum number of results to return in a single request
        /// </summary>
        private const int MaxPageSize = 100;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhitelistRulesController"/> class.
        /// </summary>
        /// <param name="rulesService">The whitelist rules service</param>
        /// <param name="logger">The logger instance</param>
        public WhitelistRulesController(
            IWhitelistRulesService rulesService,
            ILogger<WhitelistRulesController> logger)
        {
            _rulesService = rulesService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new whitelisting rule for an RWA token
        /// </summary>
        /// <param name="request">The create rule request</param>
        /// <returns>The created rule</returns>
        /// <remarks>
        /// Creates a new automated compliance rule for managing whitelist entries.
        /// Rules can enforce KYC requirements, handle expiration, and implement network-specific policies.
        /// 
        /// Example request:
        /// ```json
        /// {
        ///   "assetId": 12345,
        ///   "name": "Auto-Revoke Expired Entries",
        ///   "description": "Automatically revokes whitelist entries that have passed their expiration date",
        ///   "ruleType": "AutoRevokeExpired",
        ///   "isActive": true,
        ///   "priority": 100,
        ///   "network": "voimain-v1.0"
        /// }
        /// ```
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(WhitelistRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateRule([FromBody] CreateWhitelistRuleRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userAddress = GetUserAddress();
                if (string.IsNullOrEmpty(userAddress))
                {
                    _logger.LogWarning("User address not found in claims for rule creation");
                    return Unauthorized(new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "User authentication required"
                    });
                }

                var result = await _rulesService.CreateRuleAsync(request, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Created whitelisting rule for asset {AssetId} by user {UserAddress}",
                        request.AssetId, userAddress);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Failed to create whitelisting rule for asset {AssetId}: {Error}",
                        request.AssetId, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating whitelisting rule for asset {AssetId}", request.AssetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Updates an existing whitelisting rule
        /// </summary>
        /// <param name="request">The update rule request</param>
        /// <returns>The updated rule</returns>
        /// <remarks>
        /// Updates properties of an existing rule. Only provided fields will be updated.
        /// 
        /// Example request:
        /// ```json
        /// {
        ///   "ruleId": "550e8400-e29b-41d4-a716-446655440000",
        ///   "isActive": false,
        ///   "priority": 50
        /// }
        /// ```
        /// </remarks>
        [HttpPut]
        [ProducesResponseType(typeof(WhitelistRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateRule([FromBody] UpdateWhitelistRuleRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userAddress = GetUserAddress();
                if (string.IsNullOrEmpty(userAddress))
                {
                    _logger.LogWarning("User address not found in claims for rule update");
                    return Unauthorized(new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "User authentication required"
                    });
                }

                var result = await _rulesService.UpdateRuleAsync(request, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Updated whitelisting rule {RuleId} by user {UserAddress}",
                        request.RuleId, userAddress);
                    return Ok(result);
                }
                else
                {
                    if (result.ErrorMessage?.Contains("not found") == true)
                    {
                        return NotFound(result);
                    }
                    _logger.LogWarning("Failed to update whitelisting rule {RuleId}: {Error}",
                        request.RuleId, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception updating whitelisting rule {RuleId}", request.RuleId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lists whitelisting rules for a specific token
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <param name="ruleType">Optional rule type filter</param>
        /// <param name="isActive">Optional active status filter</param>
        /// <param name="network">Optional network filter</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20, max: 100)</param>
        /// <returns>List of whitelisting rules with pagination</returns>
        /// <remarks>
        /// Retrieves all rules for a token with optional filtering by type, status, and network.
        /// Results are ordered by priority (ascending) and creation date.
        /// </remarks>
        [HttpGet("{assetId}")]
        [ProducesResponseType(typeof(WhitelistRulesListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListRules(
            [FromRoute] ulong assetId,
            [FromQuery] WhitelistRuleType? ruleType = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? network = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var request = new ListWhitelistRulesRequest
                {
                    AssetId = assetId,
                    RuleType = ruleType,
                    IsActive = isActive,
                    Network = network,
                    Page = page,
                    PageSize = Math.Min(pageSize, MaxPageSize)
                };

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _rulesService.ListRulesAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Listed {Count} whitelisting rules for asset {AssetId}",
                        result.Rules.Count, assetId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to list whitelisting rules for asset {AssetId}: {Error}",
                        assetId, result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing whitelisting rules for asset {AssetId}", assetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistRulesListResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Applies a whitelisting rule to matching whitelist entries
        /// </summary>
        /// <param name="request">The apply rule request</param>
        /// <returns>The result of rule application</returns>
        /// <remarks>
        /// Applies a rule's logic to whitelist entries, potentially modifying their status.
        /// Supports dry-run mode to preview changes without committing them.
        /// 
        /// Example request:
        /// ```json
        /// {
        ///   "ruleId": "550e8400-e29b-41d4-a716-446655440000",
        ///   "dryRun": true
        /// }
        /// ```
        /// 
        /// Example with target addresses:
        /// ```json
        /// {
        ///   "ruleId": "550e8400-e29b-41d4-a716-446655440000",
        ///   "targetAddresses": [
        ///     "ALGORAND_ADDRESS_1...",
        ///     "ALGORAND_ADDRESS_2..."
        ///   ],
        ///   "dryRun": false
        /// }
        /// ```
        /// </remarks>
        [HttpPost("apply")]
        [ProducesResponseType(typeof(ApplyWhitelistRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ApplyRule([FromBody] ApplyWhitelistRuleRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userAddress = GetUserAddress();
                if (string.IsNullOrEmpty(userAddress))
                {
                    _logger.LogWarning("User address not found in claims for rule application");
                    return Unauthorized(new ApplyWhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "User authentication required"
                    });
                }

                var result = await _rulesService.ApplyRuleAsync(request, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Applied whitelisting rule {RuleId} by user {UserAddress} (DryRun: {DryRun}, Affected: {Count})",
                        request.RuleId, userAddress, request.DryRun, result.Result?.AffectedEntriesCount ?? 0);
                    return Ok(result);
                }
                else
                {
                    if (result.ErrorMessage?.Contains("not found") == true)
                    {
                        return NotFound(result);
                    }
                    _logger.LogWarning("Failed to apply whitelisting rule {RuleId}: {Error}",
                        request.RuleId, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception applying whitelisting rule {RuleId}", request.RuleId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApplyWhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Deletes a whitelisting rule
        /// </summary>
        /// <param name="ruleId">The ID of the rule to delete</param>
        /// <returns>Confirmation of deletion</returns>
        /// <remarks>
        /// Permanently deletes a rule. This action is logged in the audit trail.
        /// </remarks>
        [HttpDelete("{ruleId}")]
        [ProducesResponseType(typeof(DeleteWhitelistRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteRule([FromRoute] string ruleId)
        {
            try
            {
                var userAddress = GetUserAddress();
                if (string.IsNullOrEmpty(userAddress))
                {
                    _logger.LogWarning("User address not found in claims for rule deletion");
                    return Unauthorized(new DeleteWhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "User authentication required"
                    });
                }

                var request = new DeleteWhitelistRuleRequest { RuleId = ruleId };
                var result = await _rulesService.DeleteRuleAsync(request, userAddress);

                if (result.Success)
                {
                    _logger.LogInformation("Deleted whitelisting rule {RuleId} by user {UserAddress}",
                        ruleId, userAddress);
                    return Ok(result);
                }
                else
                {
                    if (result.ErrorMessage?.Contains("not found") == true)
                    {
                        return NotFound(result);
                    }
                    _logger.LogWarning("Failed to delete whitelisting rule {RuleId}: {Error}",
                        ruleId, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception deleting whitelisting rule {RuleId}", ruleId);
                return StatusCode(StatusCodes.Status500InternalServerError, new DeleteWhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets audit log entries for whitelisting rules
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <param name="ruleId">Optional rule ID filter</param>
        /// <param name="actionType">Optional action type filter</param>
        /// <param name="fromDate">Optional start date filter (ISO 8601)</param>
        /// <param name="toDate">Optional end date filter (ISO 8601)</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 50, max: 100)</param>
        /// <returns>List of audit log entries</returns>
        /// <remarks>
        /// Retrieves audit trail of all rule-related actions for compliance reporting.
        /// Supports filtering by rule, action type, and date range.
        /// 
        /// Example usage:
        /// ```
        /// GET /api/v1/whitelist-rules/12345/audit-log?actionType=Apply&amp;fromDate=2026-01-01T00:00:00Z
        /// ```
        /// </remarks>
        [HttpGet("{assetId}/audit-log")]
        [ProducesResponseType(typeof(WhitelistRuleAuditLogResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAuditLog(
            [FromRoute] ulong assetId,
            [FromQuery] string? ruleId = null,
            [FromQuery] RuleAuditActionType? actionType = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var result = await _rulesService.GetAuditLogsAsync(
                    assetId,
                    ruleId,
                    actionType,
                    fromDate,
                    toDate,
                    page,
                    Math.Min(pageSize, MaxPageSize));

                if (result.Success)
                {
                    _logger.LogInformation("Retrieved {Count} audit log entries for asset {AssetId}",
                        result.Entries.Count, assetId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to retrieve audit log for asset {AssetId}: {Error}",
                        assetId, result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception retrieving audit log for asset {AssetId}", assetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistRuleAuditLogResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Extracts the user address from the authentication claims
        /// </summary>
        /// <returns>The user's Algorand address or empty string if not found</returns>
        private string GetUserAddress()
        {
            var addressClaim = User.FindFirst("address") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            return addressClaim?.Value ?? string.Empty;
        }
    }
}
