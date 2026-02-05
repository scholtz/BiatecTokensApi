using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Provides endpoints for managing jurisdiction-specific compliance rules
    /// </summary>
    /// <remarks>
    /// This controller enables configuration-driven jurisdiction rules that map regulatory
    /// requirements to tokens and issuers. Rules can be created, updated, and evaluated
    /// without code changes, supporting MICA, FATF, SEC, and other frameworks.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/v1/compliance/jurisdiction-rules")]
    public class JurisdictionRulesController : ControllerBase
    {
        private readonly IJurisdictionRulesService _rulesService;
        private readonly ILogger<JurisdictionRulesController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="JurisdictionRulesController"/> class.
        /// </summary>
        /// <param name="rulesService">The jurisdiction rules service</param>
        /// <param name="logger">The logger instance</param>
        public JurisdictionRulesController(
            IJurisdictionRulesService rulesService,
            ILogger<JurisdictionRulesController> logger)
        {
            _rulesService = rulesService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new jurisdiction rule
        /// </summary>
        /// <param name="request">The rule creation request</param>
        /// <returns>The created jurisdiction rule</returns>
        /// <remarks>
        /// Creates a new compliance rule for a specific jurisdiction. Each jurisdiction can have
        /// multiple compliance requirements that are evaluated when determining token compliance status.
        /// 
        /// **Jurisdiction Codes:**
        /// - Use ISO 3166-1 alpha-2 country codes (e.g., "US", "DE", "FR")
        /// - Or region identifiers (e.g., "EU" for European Union, "GLOBAL" for worldwide)
        /// 
        /// **Regulatory Frameworks:**
        /// - MICA (EU Markets in Crypto-Assets Regulation)
        /// - FATF (Financial Action Task Force)
        /// - SEC (US Securities and Exchange Commission)
        /// - MiFID II (Markets in Financial Instruments Directive)
        /// 
        /// **Requirements:**
        /// Each rule can have multiple compliance requirements with:
        /// - Unique requirement code (e.g., "MICA_ARTICLE_17")
        /// - Category (KYC, AML, Disclosure, Licensing, etc.)
        /// - Severity (Critical, High, Medium, Low, Info)
        /// - Mandatory flag
        /// - Validation criteria and remediation guidance
        /// 
        /// **Example Request:**
        /// ```json
        /// {
        ///   "jurisdictionCode": "US",
        ///   "jurisdictionName": "United States",
        ///   "regulatoryFramework": "SEC",
        ///   "isActive": true,
        ///   "priority": 100,
        ///   "requirements": [
        ///     {
        ///       "requirementCode": "SEC_ACCREDITED",
        ///       "category": "Disclosure",
        ///       "description": "Accredited investor verification required",
        ///       "isMandatory": true,
        ///       "severity": "Critical",
        ///       "regulatoryReference": "SEC Regulation D"
        ///     }
        ///   ]
        /// }
        /// ```
        /// 
        /// **Access Control:**
        /// Requires authenticated user. Admin role recommended for production use.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(JurisdictionRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateRule([FromBody] CreateJurisdictionRuleRequest request)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Creating jurisdiction rule for {JurisdictionCode} by user {UserAddress}",
                    LoggingHelper.SanitizeLogInput(request.JurisdictionCode), LoggingHelper.SanitizeLogInput(userAddress));

                var result = await _rulesService.CreateRuleAsync(request, userAddress);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating jurisdiction rule");
                return StatusCode(StatusCodes.Status500InternalServerError, new JurisdictionRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lists all jurisdiction rules with optional filtering
        /// </summary>
        /// <param name="jurisdictionCode">Optional filter by jurisdiction code</param>
        /// <param name="regulatoryFramework">Optional filter by regulatory framework</param>
        /// <param name="isActive">Optional filter by active status</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 50, max: 100)</param>
        /// <returns>Paginated list of jurisdiction rules</returns>
        /// <remarks>
        /// Returns a paginated list of jurisdiction rules. Useful for displaying available
        /// jurisdictions in UI dropdowns or for auditing rule configurations.
        /// 
        /// **Filters:**
        /// - `jurisdictionCode`: Show rules for a specific jurisdiction
        /// - `regulatoryFramework`: Show rules for a specific framework (MICA, FATF, SEC, etc.)
        /// - `isActive`: Show only active or inactive rules
        /// 
        /// **Pagination:**
        /// - Default page size: 50
        /// - Maximum page size: 100
        /// - Results ordered by priority (descending), then by jurisdiction code
        /// 
        /// **Response:**
        /// Each rule includes:
        /// - Jurisdiction code and name
        /// - Regulatory framework
        /// - Active status and priority
        /// - List of compliance requirements
        /// - Creation and update metadata
        /// 
        /// **Example:**
        /// ```
        /// GET /api/v1/compliance/jurisdiction-rules?regulatoryFramework=MICA&amp;isActive=true
        /// ```
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(ListJurisdictionRulesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ListRules(
            [FromQuery] string? jurisdictionCode = null,
            [FromQuery] string? regulatoryFramework = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Listing jurisdiction rules for user {UserAddress}", LoggingHelper.SanitizeLogInput(userAddress));

                var request = new ListJurisdictionRulesRequest
                {
                    JurisdictionCode = jurisdictionCode,
                    RegulatoryFramework = regulatoryFramework,
                    IsActive = isActive,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _rulesService.ListRulesAsync(request);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing jurisdiction rules");
                return StatusCode(StatusCodes.Status500InternalServerError, new ListJurisdictionRulesResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets a specific jurisdiction rule by ID
        /// </summary>
        /// <param name="ruleId">The rule ID</param>
        /// <returns>The jurisdiction rule details</returns>
        /// <remarks>
        /// Retrieves complete details of a jurisdiction rule including all requirements.
        /// 
        /// **Use Cases:**
        /// - Display rule details in administrative UI
        /// - Audit rule configuration
        /// - Prepare rule for editing
        /// 
        /// **Example:**
        /// ```
        /// GET /api/v1/compliance/jurisdiction-rules/eu-mica-baseline
        /// ```
        /// </remarks>
        [HttpGet("{ruleId}")]
        [ProducesResponseType(typeof(JurisdictionRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRule(string ruleId)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Getting jurisdiction rule {RuleId} for user {UserAddress}",
                    LoggingHelper.SanitizeLogInput(ruleId), LoggingHelper.SanitizeLogInput(userAddress));

                var result = await _rulesService.GetRuleByIdAsync(ruleId);

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
                _logger.LogError(ex, "Exception getting jurisdiction rule {RuleId}", LoggingHelper.SanitizeLogInput(ruleId));
                return StatusCode(StatusCodes.Status500InternalServerError, new JurisdictionRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Updates an existing jurisdiction rule
        /// </summary>
        /// <param name="ruleId">The rule ID to update</param>
        /// <param name="request">The updated rule data</param>
        /// <returns>The updated jurisdiction rule</returns>
        /// <remarks>
        /// Updates an existing jurisdiction rule. All fields are replaced with the new values.
        /// 
        /// **Note:** Updating a rule may affect existing compliance evaluations for tokens
        /// assigned to this jurisdiction. Consider the impact on existing tokens before making changes.
        /// 
        /// **Example Request:**
        /// ```json
        /// {
        ///   "jurisdictionCode": "EU",
        ///   "jurisdictionName": "European Union",
        ///   "regulatoryFramework": "MICA",
        ///   "isActive": true,
        ///   "priority": 100,
        ///   "requirements": [...]
        /// }
        /// ```
        /// </remarks>
        [HttpPut("{ruleId}")]
        [ProducesResponseType(typeof(JurisdictionRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateRule(string ruleId, [FromBody] CreateJurisdictionRuleRequest request)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Updating jurisdiction rule {RuleId} by user {UserAddress}",
                    LoggingHelper.SanitizeLogInput(ruleId), LoggingHelper.SanitizeLogInput(userAddress));

                var result = await _rulesService.UpdateRuleAsync(ruleId, request, userAddress);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return result.ErrorMessage?.Contains("not found") == true 
                        ? NotFound(result) 
                        : BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception updating jurisdiction rule {RuleId}", LoggingHelper.SanitizeLogInput(ruleId));
                return StatusCode(StatusCodes.Status500InternalServerError, new JurisdictionRuleResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Deletes a jurisdiction rule
        /// </summary>
        /// <param name="ruleId">The rule ID to delete</param>
        /// <returns>Success or failure response</returns>
        /// <remarks>
        /// Deletes a jurisdiction rule. Exercise caution as this may affect existing token compliance evaluations.
        /// 
        /// **Warning:** Deleting a rule that is referenced by token jurisdiction assignments
        /// may cause those tokens to fall back to the GLOBAL baseline rule.
        /// 
        /// **Example:**
        /// ```
        /// DELETE /api/v1/compliance/jurisdiction-rules/custom-rule-id
        /// ```
        /// </remarks>
        [HttpDelete("{ruleId}")]
        [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteRule(string ruleId)
        {
            try
            {
                var userAddress = GetUserAddress();
                _logger.LogInformation("Deleting jurisdiction rule {RuleId} by user {UserAddress}",
                    LoggingHelper.SanitizeLogInput(ruleId), LoggingHelper.SanitizeLogInput(userAddress));

                var result = await _rulesService.DeleteRuleAsync(ruleId);

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
                _logger.LogError(ex, "Exception deleting jurisdiction rule {RuleId}", LoggingHelper.SanitizeLogInput(ruleId));
                return StatusCode(StatusCodes.Status500InternalServerError, new BaseResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Evaluates jurisdiction rules for a token and returns compliance status
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <param name="network">The network on which the token is deployed</param>
        /// <returns>Jurisdiction evaluation result with compliance status</returns>
        /// <remarks>
        /// Evaluates all applicable jurisdiction rules for a token and determines its
        /// overall compliance status. This endpoint aggregates compliance checks across
        /// all assigned jurisdictions and returns a comprehensive evaluation.
        /// 
        /// **Process:**
        /// 1. Retrieves jurisdiction assignments for the token
        /// 2. If no jurisdictions assigned, uses GLOBAL baseline
        /// 3. Evaluates each requirement in applicable jurisdiction rules
        /// 4. Aggregates results into overall compliance status
        /// 5. Provides rationale for status determination
        /// 
        /// **Compliance Status Values:**
        /// - **Compliant**: All mandatory requirements passed
        /// - **PartiallyCompliant**: Some mandatory requirements passed, others failed
        /// - **NonCompliant**: All mandatory requirements failed
        /// - **Unknown**: No requirements evaluated or evaluation error
        /// 
        /// **Check Result Status Values:**
        /// - **Pass**: Requirement met
        /// - **Fail**: Requirement not met
        /// - **Partial**: Partially met
        /// - **NotApplicable**: Cannot be evaluated automatically
        /// 
        /// **Example:**
        /// ```
        /// GET /api/v1/compliance/jurisdiction-rules/evaluate?assetId=12345&amp;network=voimain-v1.0
        /// ```
        /// 
        /// **Response Example:**
        /// ```json
        /// {
        ///   "assetId": 12345,
        ///   "network": "voimain-v1.0",
        ///   "applicableJurisdictions": ["EU"],
        ///   "complianceStatus": "PartiallyCompliant",
        ///   "checkResults": [
        ///     {
        ///       "requirementCode": "MICA_ARTICLE_20",
        ///       "jurisdictionCode": "EU",
        ///       "status": "Pass",
        ///       "evidence": "KYC verified on 2024-01-15"
        ///     }
        ///   ],
        ///   "rationale": [
        ///     "Passed 4 of 6 mandatory requirements",
        ///     "Failed: MICA_ARTICLE_30 - Licensing authorization pending"
        ///   ]
        /// }
        /// ```
        /// 
        /// **Access Control:**
        /// Returns evaluation only for tokens owned by the authenticated user.
        /// </remarks>
        [HttpGet("evaluate")]
        [ProducesResponseType(typeof(JurisdictionEvaluationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EvaluateTokenCompliance(
            [FromQuery] ulong assetId,
            [FromQuery] string network)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(network))
                {
                    return BadRequest(new { success = false, errorMessage = "Network is required" });
                }

                var userAddress = GetUserAddress();
                _logger.LogInformation("Evaluating token compliance for {AssetId} on {Network} by user {UserAddress}",
                    assetId, LoggingHelper.SanitizeLogInput(network), LoggingHelper.SanitizeLogInput(userAddress));

                var result = await _rulesService.EvaluateTokenComplianceAsync(assetId, network, userAddress);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception evaluating token compliance");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Assigns a jurisdiction to a token
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <param name="network">The network on which the token is deployed</param>
        /// <param name="jurisdictionCode">The jurisdiction code to assign</param>
        /// <param name="isPrimary">Whether this is the primary jurisdiction (default: true)</param>
        /// <param name="notes">Optional notes about the assignment</param>
        /// <returns>Success or failure response</returns>
        /// <remarks>
        /// Assigns a jurisdiction to a token. A token can have multiple jurisdictions,
        /// but only one can be marked as primary. The primary jurisdiction typically
        /// represents the token issuer's legal domicile.
        /// 
        /// **Rules:**
        /// - Jurisdiction rule must exist and be active
        /// - If isPrimary is true, other jurisdictions are automatically unmarked as primary
        /// - Assigning a jurisdiction that already exists updates the assignment
        /// 
        /// **Example:**
        /// ```
        /// POST /api/v1/compliance/jurisdiction-rules/assign?assetId=12345&amp;network=voimain-v1.0&amp;jurisdictionCode=EU&amp;isPrimary=true
        /// ```
        /// </remarks>
        [HttpPost("assign")]
        [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AssignTokenJurisdiction(
            [FromQuery] ulong assetId,
            [FromQuery] string network,
            [FromQuery] string jurisdictionCode,
            [FromQuery] bool isPrimary = true,
            [FromQuery] string? notes = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(network) || string.IsNullOrWhiteSpace(jurisdictionCode))
                {
                    return BadRequest(new BaseResponse
                    {
                        Success = false,
                        ErrorMessage = "Network and jurisdiction code are required"
                    });
                }

                var userAddress = GetUserAddress();
                _logger.LogInformation("Assigning jurisdiction {JurisdictionCode} to token {AssetId} on {Network}",
                    LoggingHelper.SanitizeLogInput(jurisdictionCode), assetId, LoggingHelper.SanitizeLogInput(network));

                var result = await _rulesService.AssignTokenJurisdictionAsync(assetId, network, jurisdictionCode, isPrimary, userAddress, notes);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception assigning token jurisdiction");
                return StatusCode(StatusCodes.Status500InternalServerError, new BaseResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Gets jurisdiction assignments for a token
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <param name="network">The network on which the token is deployed</param>
        /// <returns>List of jurisdiction assignments</returns>
        /// <remarks>
        /// Returns all jurisdiction assignments for a token, including primary and secondary jurisdictions.
        /// 
        /// **Example:**
        /// ```
        /// GET /api/v1/compliance/jurisdiction-rules/token-jurisdictions?assetId=12345&amp;network=voimain-v1.0
        /// ```
        /// 
        /// **Response:**
        /// ```json
        /// [
        ///   {
        ///     "assetId": 12345,
        ///     "network": "voimain-v1.0",
        ///     "jurisdictionCode": "EU",
        ///     "isPrimary": true,
        ///     "assignedAt": "2024-01-15T10:30:00Z",
        ///     "assignedBy": "ADDR123..."
        ///   }
        /// ]
        /// ```
        /// </remarks>
        [HttpGet("token-jurisdictions")]
        [ProducesResponseType(typeof(List<TokenJurisdiction>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTokenJurisdictions(
            [FromQuery] ulong assetId,
            [FromQuery] string network)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(network))
                {
                    return BadRequest(new { success = false, errorMessage = "Network is required" });
                }

                var userAddress = GetUserAddress();
                _logger.LogInformation("Getting token jurisdictions for {AssetId} on {Network}",
                    assetId, LoggingHelper.SanitizeLogInput(network));

                var result = await _rulesService.GetTokenJurisdictionsAsync(assetId, network);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting token jurisdictions");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    errorMessage = $"Internal error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Removes a jurisdiction assignment from a token
        /// </summary>
        /// <param name="assetId">The asset ID (token ID)</param>
        /// <param name="network">The network on which the token is deployed</param>
        /// <param name="jurisdictionCode">The jurisdiction code to remove</param>
        /// <returns>Success or failure response</returns>
        /// <remarks>
        /// Removes a jurisdiction assignment from a token. If the removed jurisdiction was the
        /// primary jurisdiction, no other jurisdiction is automatically promoted to primary.
        /// 
        /// **Example:**
        /// ```
        /// DELETE /api/v1/compliance/jurisdiction-rules/token-jurisdictions?assetId=12345&amp;network=voimain-v1.0&amp;jurisdictionCode=EU
        /// ```
        /// </remarks>
        [HttpDelete("token-jurisdictions")]
        [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RemoveTokenJurisdiction(
            [FromQuery] ulong assetId,
            [FromQuery] string network,
            [FromQuery] string jurisdictionCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(network) || string.IsNullOrWhiteSpace(jurisdictionCode))
                {
                    return BadRequest(new BaseResponse
                    {
                        Success = false,
                        ErrorMessage = "Network and jurisdiction code are required"
                    });
                }

                var userAddress = GetUserAddress();
                _logger.LogInformation("Removing jurisdiction {JurisdictionCode} from token {AssetId} on {Network}",
                    LoggingHelper.SanitizeLogInput(jurisdictionCode), assetId, LoggingHelper.SanitizeLogInput(network));

                var result = await _rulesService.RemoveTokenJurisdictionAsync(assetId, network, jurisdictionCode);

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
                _logger.LogError(ex, "Exception removing token jurisdiction");
                return StatusCode(StatusCodes.Status500InternalServerError, new BaseResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                });
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
