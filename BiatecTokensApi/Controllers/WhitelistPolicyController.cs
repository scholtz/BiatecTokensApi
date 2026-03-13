using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Whitelist;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Manages whitelist policies for jurisdiction-aware participant eligibility evaluation
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/whitelist/policies")]
    public class WhitelistPolicyController : ControllerBase
    {
        private readonly IWhitelistPolicyService _policyService;
        private readonly ILogger<WhitelistPolicyController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="WhitelistPolicyController"/>
        /// </summary>
        public WhitelistPolicyController(IWhitelistPolicyService policyService, ILogger<WhitelistPolicyController> logger)
        {
            _policyService = policyService;
            _logger = logger;
        }

        private string CallerIdentity => User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "unknown";

        // ── CREATE ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new whitelist policy in Draft state
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(WhitelistPolicyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreatePolicy([FromBody] CreateWhitelistPolicyRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _policyService.CreatePolicyAsync(request, CallerIdentity);
                return result.Success ? Ok(result) : StatusCode(500, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception creating whitelist policy");
                return StatusCode(500, new WhitelistPolicyResponse { Success = false, ErrorMessage = "Internal error." });
            }
        }

        // ── READ ALL ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Lists all whitelist policies, optionally filtered by asset ID
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(WhitelistPolicyListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPolicies([FromQuery] ulong? assetId = null)
        {
            try
            {
                var result = await _policyService.GetPoliciesAsync(assetId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception listing whitelist policies");
                return StatusCode(500, new WhitelistPolicyListResponse { Success = false, ErrorMessage = "Internal error." });
            }
        }

        // ── READ BY ID ────────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves a whitelist policy by its unique ID
        /// </summary>
        [HttpGet("{policyId}")]
        [ProducesResponseType(typeof(WhitelistPolicyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPolicy([FromRoute] string policyId)
        {
            try
            {
                var result = await _policyService.GetPolicyAsync(policyId);
                if (!result.Success && result.ErrorCode == "POLICY_NOT_FOUND")
                    return NotFound(result);
                return result.Success ? Ok(result) : StatusCode(500, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception getting whitelist policy {PolicyId}",
                    LoggingHelper.SanitizeLogInput(policyId));
                return StatusCode(500, new WhitelistPolicyResponse { Success = false, ErrorMessage = "Internal error." });
            }
        }

        // ── UPDATE ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates a whitelist policy
        /// </summary>
        [HttpPut("{policyId}")]
        [ProducesResponseType(typeof(WhitelistPolicyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdatePolicy([FromRoute] string policyId, [FromBody] UpdateWhitelistPolicyRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var result = await _policyService.UpdatePolicyAsync(policyId, request, CallerIdentity);
                if (!result.Success && result.ErrorCode == "POLICY_NOT_FOUND")
                    return NotFound(result);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception updating whitelist policy {PolicyId}",
                    LoggingHelper.SanitizeLogInput(policyId));
                return StatusCode(500, new WhitelistPolicyResponse { Success = false, ErrorMessage = "Internal error." });
            }
        }

        // ── ARCHIVE (DELETE) ──────────────────────────────────────────────────────

        /// <summary>
        /// Archives a whitelist policy (soft delete)
        /// </summary>
        [HttpDelete("{policyId}")]
        [ProducesResponseType(typeof(WhitelistPolicyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ArchivePolicy([FromRoute] string policyId)
        {
            try
            {
                var result = await _policyService.ArchivePolicyAsync(policyId, CallerIdentity);
                if (!result.Success && result.ErrorCode == "POLICY_NOT_FOUND")
                    return NotFound(result);
                return result.Success ? Ok(result) : StatusCode(500, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception archiving whitelist policy {PolicyId}",
                    LoggingHelper.SanitizeLogInput(policyId));
                return StatusCode(500, new WhitelistPolicyResponse { Success = false, ErrorMessage = "Internal error." });
            }
        }

        // ── VALIDATE ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Validates a whitelist policy for contradictions and completeness issues
        /// </summary>
        [HttpPost("{policyId}/validate")]
        [ProducesResponseType(typeof(WhitelistPolicyValidationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidatePolicy([FromRoute] string policyId)
        {
            try
            {
                var result = await _policyService.ValidatePolicyAsync(policyId);
                if (!result.Success && result.ErrorCode == "POLICY_NOT_FOUND")
                    return NotFound(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception validating whitelist policy {PolicyId}",
                    LoggingHelper.SanitizeLogInput(policyId));
                return StatusCode(500, new WhitelistPolicyValidationResult { Success = false, ErrorMessage = "Internal error." });
            }
        }

        // ── EVALUATE ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates a participant's eligibility against a whitelist policy (fail-closed semantics)
        /// </summary>
        [HttpPost("{policyId}/evaluate")]
        [ProducesResponseType(typeof(WhitelistPolicyEligibilityResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> EvaluateEligibility([FromRoute] string policyId, [FromBody] WhitelistPolicyEligibilityRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Ensure the route policyId is used (body PolicyId is optional override)
            request.PolicyId = policyId;

            try
            {
                var result = await _policyService.EvaluateEligibilityAsync(request);
                if (!result.Success && result.ErrorCode == "POLICY_NOT_FOUND")
                    return NotFound(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception evaluating eligibility for policy {PolicyId}",
                    LoggingHelper.SanitizeLogInput(policyId));
                return StatusCode(500, new WhitelistPolicyEligibilityResult
                {
                    Success = false,
                    ErrorMessage = "Internal error.",
                    Outcome = WhitelistPolicyEligibilityOutcome.Deny,
                    IsFailClosed = true
                });
            }
        }
    }
}
