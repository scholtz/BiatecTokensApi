using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.IssuancePolicy;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Controller for managing issuance compliance policies and evaluating participant eligibility.
    ///
    /// An issuance compliance policy lets a token issuer define eligibility rules for their token issuance:
    /// - Whitelist membership requirements
    /// - Jurisdiction allow/block lists
    /// - KYC verification requirements
    ///
    /// The evaluate endpoint returns a structured <c>IssuancePolicyDecisionResult</c> with
    /// Allow / Deny / ConditionalReview outcome, matched rules, reasons, and full audit metadata.
    /// </summary>
    [ApiController]
    [Route("api/v1/compliance/issuance-policies")]
    [Authorize]
    public class IssuancePolicyController : ControllerBase
    {
        private readonly IIssuancePolicyService _policyService;
        private readonly ILogger<IssuancePolicyController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="IssuancePolicyController"/>
        /// </summary>
        public IssuancePolicyController(
            IIssuancePolicyService policyService,
            ILogger<IssuancePolicyController> logger)
        {
            _policyService = policyService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new issuance compliance policy for a token.
        /// </summary>
        /// <param name="request">Policy configuration (name, asset ID, jurisdiction rules, KYC requirement)</param>
        /// <returns>The created policy</returns>
        /// <response code="200">Policy created successfully</response>
        /// <response code="400">Validation error (e.g., empty name, conflicting jurisdictions)</response>
        /// <response code="401">Authentication required</response>
        [HttpPost]
        [ProducesResponseType(typeof(IssuancePolicyResponse), 200)]
        [ProducesResponseType(typeof(IssuancePolicyResponse), 400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> CreatePolicy([FromBody] CreateIssuancePolicyRequest request)
        {
            var issuerId = GetUserAddress();
            _logger.LogInformation(
                "Creating issuance policy: AssetId={AssetId}, IssuerId={IssuerId}",
                request?.AssetId,
                LoggingHelper.SanitizeLogInput(issuerId));

            var result = await _policyService.CreatePolicyAsync(request!, issuerId);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Failed to create issuance policy: {ErrorMessage}",
                    LoggingHelper.SanitizeLogInput(result.ErrorMessage));
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Lists all issuance compliance policies owned by the authenticated issuer.
        /// </summary>
        /// <returns>List of policies belonging to the authenticated user</returns>
        /// <response code="200">Policies retrieved successfully</response>
        /// <response code="401">Authentication required</response>
        [HttpGet]
        [ProducesResponseType(typeof(IssuancePolicyListResponse), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> ListPolicies()
        {
            var issuerId = GetUserAddress();
            _logger.LogInformation(
                "Listing issuance policies for IssuerId={IssuerId}",
                LoggingHelper.SanitizeLogInput(issuerId));

            var result = await _policyService.ListPoliciesAsync(issuerId);
            return Ok(result);
        }

        /// <summary>
        /// Gets a specific issuance policy by its unique ID.
        /// Only the issuer who created the policy may retrieve it.
        /// </summary>
        /// <param name="policyId">The unique policy identifier</param>
        /// <returns>The policy details</returns>
        /// <response code="200">Policy found</response>
        /// <response code="400">Policy not found or access denied</response>
        /// <response code="401">Authentication required</response>
        [HttpGet("{policyId}")]
        [ProducesResponseType(typeof(IssuancePolicyResponse), 200)]
        [ProducesResponseType(typeof(IssuancePolicyResponse), 400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetPolicy([FromRoute] string policyId)
        {
            var requesterId = GetUserAddress();
            _logger.LogInformation(
                "Getting issuance policy: PolicyId={PolicyId}, RequesterId={RequesterId}",
                LoggingHelper.SanitizeLogInput(policyId),
                LoggingHelper.SanitizeLogInput(requesterId));

            var result = await _policyService.GetPolicyAsync(policyId, requesterId);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Gets the issuance policy for a specific asset ID.
        /// Only the issuer who created the policy may retrieve it.
        /// </summary>
        /// <param name="assetId">The asset (token) ID</param>
        /// <returns>The policy for the specified asset</returns>
        /// <response code="200">Policy found</response>
        /// <response code="400">No policy found for this asset</response>
        /// <response code="401">Authentication required</response>
        [HttpGet("asset/{assetId}")]
        [ProducesResponseType(typeof(IssuancePolicyResponse), 200)]
        [ProducesResponseType(typeof(IssuancePolicyResponse), 400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetPolicyByAsset([FromRoute] ulong assetId)
        {
            var requesterId = GetUserAddress();
            _logger.LogInformation(
                "Getting issuance policy by asset: AssetId={AssetId}, RequesterId={RequesterId}",
                assetId,
                LoggingHelper.SanitizeLogInput(requesterId));

            var result = await _policyService.GetPolicyByAssetAsync(assetId, requesterId);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        /// <summary>
        /// Updates an existing issuance policy. Only the issuer who created the policy may update it.
        /// Only non-null fields in the request body are applied.
        /// </summary>
        /// <param name="policyId">The unique policy identifier</param>
        /// <param name="request">Fields to update</param>
        /// <returns>The updated policy</returns>
        /// <response code="200">Policy updated</response>
        /// <response code="400">Validation error or not authorized</response>
        /// <response code="401">Authentication required</response>
        [HttpPut("{policyId}")]
        [ProducesResponseType(typeof(IssuancePolicyResponse), 200)]
        [ProducesResponseType(typeof(IssuancePolicyResponse), 400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> UpdatePolicy(
            [FromRoute] string policyId,
            [FromBody] UpdateIssuancePolicyRequest request)
        {
            var requesterId = GetUserAddress();
            _logger.LogInformation(
                "Updating issuance policy: PolicyId={PolicyId}, RequesterId={RequesterId}",
                LoggingHelper.SanitizeLogInput(policyId),
                LoggingHelper.SanitizeLogInput(requesterId));

            var result = await _policyService.UpdatePolicyAsync(policyId, request, requesterId);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Failed to update issuance policy: {ErrorMessage}",
                    LoggingHelper.SanitizeLogInput(result.ErrorMessage));
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Deletes an issuance policy. Only the issuer who created the policy may delete it.
        /// </summary>
        /// <param name="policyId">The unique policy identifier</param>
        /// <returns>Success confirmation</returns>
        /// <response code="200">Policy deleted</response>
        /// <response code="400">Policy not found or not authorized</response>
        /// <response code="401">Authentication required</response>
        [HttpDelete("{policyId}")]
        [ProducesResponseType(typeof(IssuancePolicyResponse), 200)]
        [ProducesResponseType(typeof(IssuancePolicyResponse), 400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> DeletePolicy([FromRoute] string policyId)
        {
            var requesterId = GetUserAddress();
            _logger.LogInformation(
                "Deleting issuance policy: PolicyId={PolicyId}, RequesterId={RequesterId}",
                LoggingHelper.SanitizeLogInput(policyId),
                LoggingHelper.SanitizeLogInput(requesterId));

            var result = await _policyService.DeletePolicyAsync(policyId, requesterId);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Failed to delete issuance policy: {ErrorMessage}",
                    LoggingHelper.SanitizeLogInput(result.ErrorMessage));
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Evaluates a participant's eligibility against an issuance compliance policy.
        ///
        /// Returns a <c>IssuancePolicyDecisionResult</c> with:
        /// - <c>Allow</c> — participant meets all policy requirements
        /// - <c>Deny</c> — participant fails one or more required checks
        /// - <c>ConditionalReview</c> — participant may be eligible but additional information is required
        ///
        /// The response includes matched rules, human-readable reasons, required actions (for ConditionalReview),
        /// and full audit metadata (decisionId, evaluatedAt, evaluatedBy).
        /// </summary>
        /// <param name="policyId">The policy to evaluate against</param>
        /// <param name="request">Participant details (address, jurisdiction, KYC status, context)</param>
        /// <returns>The policy decision result</returns>
        /// <response code="200">Evaluation completed</response>
        /// <response code="400">Invalid request</response>
        /// <response code="401">Authentication required</response>
        [HttpPost("{policyId}/evaluate")]
        [ProducesResponseType(typeof(IssuancePolicyDecisionResult), 200)]
        [ProducesResponseType(typeof(IssuancePolicyDecisionResult), 400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> EvaluateParticipant(
            [FromRoute] string policyId,
            [FromBody] EvaluateParticipantRequest request)
        {
            var evaluatorId = GetUserAddress();
            _logger.LogInformation(
                "Evaluating participant for policy: PolicyId={PolicyId}, Participant={Participant}, EvaluatorId={EvaluatorId}",
                LoggingHelper.SanitizeLogInput(policyId),
                LoggingHelper.SanitizeLogInput(request?.ParticipantAddress),
                LoggingHelper.SanitizeLogInput(evaluatorId));

            var result = await _policyService.EvaluateParticipantAsync(policyId, request!, evaluatorId);

            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Gets the authenticated user's address from JWT claims (ARC-0014 / JWT auth)
        /// </summary>
        private string GetUserAddress()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? string.Empty;
        }
    }
}
