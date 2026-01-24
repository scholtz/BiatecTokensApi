using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for managing RWA token whitelisting rules aligned with MICA requirements
    /// </summary>
    /// <remarks>
    /// This controller manages whitelisting rules that define validation and compliance requirements
    /// for RWA tokens. Rules can enforce KYC requirements, role-based access, network-specific
    /// constraints, expiration policies, and other compliance requirements.
    /// All endpoints require ARC-0014 authentication.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/whitelist/rules")]
    public class WhitelistRulesController : ControllerBase
    {
        private readonly IWhitelistRulesService _rulesService;
        private readonly ILogger<WhitelistRulesController> _logger;

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
        /// <param name="request">The rule creation request</param>
        /// <returns>The created rule</returns>
        /// <remarks>
        /// Creates a new rule that will be applied to whitelist entries for the specified asset.
        /// Rules can enforce various compliance requirements such as KYC verification,
        /// role-based access control, network-specific constraints, and expiration policies.
        /// 
        /// Example for Aramid network KYC requirement:
        /// ```json
        /// {
        ///   "assetId": 12345,
        ///   "name": "KYC Required for Aramid",
        ///   "description": "All Aramid network entries must have KYC verification",
        ///   "ruleType": "KycRequired",
        ///   "priority": 100,
        ///   "isEnabled": true,
        ///   "network": "aramidmain-v1.0",
        ///   "configuration": {
        ///     "kycMandatory": true,
        ///     "approvedKycProviders": ["Sumsub", "Onfido"],
        ///     "validationMessage": "KYC verification is mandatory for Aramid network"
        ///   }
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdBy = GetUserAddress();
                if (string.IsNullOrEmpty(createdBy))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _rulesService.CreateRuleAsync(request, createdBy);

                if (result.Success)
                {
                    _logger.LogInformation("Created whitelist rule for asset {AssetId} by {CreatedBy}",
                        request.AssetId, createdBy);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to create whitelist rule for asset {AssetId}: {Error}",
                        request.AssetId, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating whitelist rule for asset {AssetId}", request.AssetId);
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
        /// <param name="ruleId">The rule ID to update</param>
        /// <param name="request">The rule update request</param>
        /// <returns>The updated rule</returns>
        /// <remarks>
        /// Updates an existing rule. Only provided fields will be updated.
        /// The rule type cannot be changed after creation.
        /// </remarks>
        [HttpPut("{ruleId}")]
        [ProducesResponseType(typeof(WhitelistRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateRule(
            [FromRoute] string ruleId,
            [FromBody] UpdateWhitelistRuleRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Ensure ruleId in route matches request
            request.RuleId = ruleId;

            try
            {
                var updatedBy = GetUserAddress();
                if (string.IsNullOrEmpty(updatedBy))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _rulesService.UpdateRuleAsync(request, updatedBy);

                if (result.Success)
                {
                    _logger.LogInformation("Updated whitelist rule {RuleId} by {UpdatedBy}",
                        ruleId, updatedBy);
                    return Ok(result);
                }
                else
                {
                    if (result.ErrorMessage?.Contains("not found") == true)
                    {
                        return NotFound(result);
                    }
                    _logger.LogError("Failed to update whitelist rule {RuleId}: {Error}",
                        ruleId, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception updating whitelist rule {RuleId}", ruleId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets a specific whitelisting rule by ID
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <returns>The rule details</returns>
        [HttpGet("{ruleId}")]
        [ProducesResponseType(typeof(WhitelistRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRule([FromRoute] string ruleId)
        {
            try
            {
                var result = await _rulesService.GetRuleAsync(ruleId);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return NotFound(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting whitelist rule {RuleId}", ruleId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Deletes a whitelisting rule
        /// </summary>
        /// <param name="ruleId">The rule ID to delete</param>
        /// <returns>The result of the deletion</returns>
        /// <remarks>
        /// Deletes a rule. This does not affect existing whitelist entries,
        /// but the rule will no longer be applied to new entries or validations.
        /// </remarks>
        [HttpDelete("{ruleId}")]
        [ProducesResponseType(typeof(WhitelistRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteRule([FromRoute] string ruleId)
        {
            try
            {
                var deletedBy = GetUserAddress();
                if (string.IsNullOrEmpty(deletedBy))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new WhitelistRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _rulesService.DeleteRuleAsync(ruleId, deletedBy);

                if (result.Success)
                {
                    _logger.LogInformation("Deleted whitelist rule {RuleId} by {DeletedBy}",
                        ruleId, deletedBy);
                    return Ok(result);
                }
                else
                {
                    return NotFound(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception deleting whitelist rule {RuleId}", ruleId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lists whitelisting rules for an asset
        /// </summary>
        /// <param name="assetId">The asset ID</param>
        /// <param name="ruleType">Optional filter by rule type</param>
        /// <param name="network">Optional filter by network</param>
        /// <param name="isEnabled">Optional filter by enabled status</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20, max: 100)</param>
        /// <returns>List of rules with pagination</returns>
        /// <remarks>
        /// Returns all rules for the specified asset, sorted by priority (highest first).
        /// Filters can be applied to narrow down the results.
        /// </remarks>
        [HttpGet("asset/{assetId}")]
        [ProducesResponseType(typeof(WhitelistRulesListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListRules(
            [FromRoute] ulong assetId,
            [FromQuery] WhitelistRuleType? ruleType = null,
            [FromQuery] string? network = null,
            [FromQuery] bool? isEnabled = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var request = new ListWhitelistRulesRequest
                {
                    AssetId = assetId,
                    RuleType = ruleType,
                    Network = network,
                    IsEnabled = isEnabled,
                    Page = page,
                    PageSize = Math.Min(pageSize, 100)
                };

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _rulesService.ListRulesAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation("Listed {Count} whitelist rules for asset {AssetId}",
                        result.Rules.Count, assetId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to list whitelist rules for asset {AssetId}: {Error}",
                        assetId, result.ErrorMessage);
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing whitelist rules for asset {AssetId}", assetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new WhitelistRulesListResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Applies a rule to existing whitelist entries
        /// </summary>
        /// <param name="ruleId">The rule ID to apply</param>
        /// <param name="request">The apply rule request</param>
        /// <returns>The application results with validation details</returns>
        /// <remarks>
        /// Applies a rule to all existing whitelist entries for the rule's asset.
        /// This validates existing entries against the rule and returns which entries
        /// pass or fail validation. Optionally can fail on first error or continue
        /// to validate all entries.
        /// 
        /// This is useful when:
        /// - A new rule is created and you want to check existing entries
        /// - Rule configuration is updated and you need to re-validate
        /// - Compliance audit requires validation report
        /// </remarks>
        [HttpPost("{ruleId}/apply")]
        [ProducesResponseType(typeof(ApplyRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ApplyRule(
            [FromRoute] string ruleId,
            [FromBody] ApplyWhitelistRuleRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Ensure ruleId in route matches request
            request.RuleId = ruleId;

            try
            {
                var performedBy = GetUserAddress();
                if (string.IsNullOrEmpty(performedBy))
                {
                    _logger.LogWarning("Failed to get user address from authentication context");
                    return Unauthorized(new ApplyRuleResponse
                    {
                        Success = false,
                        ErrorMessage = "User address not found in authentication context"
                    });
                }

                var result = await _rulesService.ApplyRuleAsync(request, performedBy);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Applied rule {RuleId} by {PerformedBy}. Evaluated: {Evaluated}, Passed: {Passed}, Failed: {Failed}",
                        ruleId, performedBy, result.EntriesEvaluated, result.EntriesPassed, result.EntriesFailed);
                    return Ok(result);
                }
                else
                {
                    if (result.ErrorMessage?.Contains("not found") == true)
                    {
                        return NotFound(result);
                    }
                    _logger.LogError("Failed to apply rule {RuleId}: {Error}",
                        ruleId, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception applying rule {RuleId}", ruleId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApplyRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Validates whitelist entries against rules
        /// </summary>
        /// <param name="request">The validation request</param>
        /// <returns>The validation results</returns>
        /// <remarks>
        /// Validates whitelist entries against all enabled rules for an asset.
        /// Can validate all entries or a specific address.
        /// Can validate against all rules or a specific rule.
        /// 
        /// This is useful for:
        /// - Pre-flight validation before adding an entry
        /// - Compliance checks and audits
        /// - Troubleshooting why an entry might be invalid
        /// </remarks>
        [HttpPost("validate")]
        [ProducesResponseType(typeof(ValidateAgainstRulesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateAgainstRules([FromBody] ValidateAgainstRulesRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _rulesService.ValidateAgainstRulesAsync(request);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Validated rules for asset {AssetId}. Rules: {RulesEvaluated}, Passed: {Passed}, Failed: {Failed}",
                        request.AssetId, result.RulesEvaluated, result.RulesPassed, result.RulesFailed);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("Failed to validate rules for asset {AssetId}: {Error}",
                        request.AssetId, result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception validating rules for asset {AssetId}", request.AssetId);
                return StatusCode(StatusCodes.Status500InternalServerError, new ValidateAgainstRulesResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Extracts the user's Algorand address from the authentication claims
        /// </summary>
        private string? GetUserAddress()
        {
            // ARC-0014 authentication stores the address in the NameIdentifier claim
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
