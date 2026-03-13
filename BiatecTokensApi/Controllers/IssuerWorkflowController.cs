using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiatecTokensApi.Controllers
{
    /// <summary>
    /// Issuer workflow controller providing team role management and approval-state APIs
    /// for enterprise collaboration in regulated token operations.
    ///
    /// All endpoints are issuer-scoped. Tenant isolation is enforced by the service layer:
    /// one issuer cannot read or mutate another issuer's team members or workflow items.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/issuer-workflow")]
    public class IssuerWorkflowController : ControllerBase
    {
        private readonly IIssuerWorkflowService _workflowService;
        private readonly ILogger<IssuerWorkflowController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="IssuerWorkflowController"/>.
        /// </summary>
        public IssuerWorkflowController(IIssuerWorkflowService workflowService, ILogger<IssuerWorkflowController> logger)
        {
            _workflowService = workflowService;
            _logger          = logger;
        }

        // ── Team Membership ────────────────────────────────────────────────────

        /// <summary>
        /// Adds a new member to an issuer team with the specified role.
        /// Only Admins of the issuer should call this endpoint.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="request">Member details and role.</param>
        [HttpPost("{issuerId}/members")]
        [ProducesResponseType(typeof(IssuerTeamMemberResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IssuerTeamMemberResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddMember(string issuerId, [FromBody] AddIssuerTeamMemberRequest request)
        {
            var actorId = GetActorId();
            _logger.LogInformation(
                "AddMember. IssuerId={IssuerId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(issuerId),
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _workflowService.AddMemberAsync(issuerId, request, actorId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Updates an existing team member's role or display name.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="memberId">Member record identifier.</param>
        /// <param name="request">Updated role / display name.</param>
        [HttpPut("{issuerId}/members/{memberId}")]
        [ProducesResponseType(typeof(IssuerTeamMemberResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IssuerTeamMemberResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(IssuerTeamMemberResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMember(string issuerId, string memberId, [FromBody] UpdateIssuerTeamMemberRequest request)
        {
            var actorId = GetActorId();
            var result  = await _workflowService.UpdateMemberAsync(issuerId, memberId, request, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Removes (deactivates) a team member. The record is soft-deleted and retained for audit purposes.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="memberId">Member record identifier.</param>
        [HttpDelete("{issuerId}/members/{memberId}")]
        [ProducesResponseType(typeof(IssuerTeamMemberResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IssuerTeamMemberResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveMember(string issuerId, string memberId)
        {
            var actorId = GetActorId();
            var result  = await _workflowService.RemoveMemberAsync(issuerId, memberId, actorId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Gets a single team member by their member ID, scoped to the issuer.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="memberId">Member record identifier.</param>
        [HttpGet("{issuerId}/members/{memberId}")]
        [ProducesResponseType(typeof(IssuerTeamMemberResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IssuerTeamMemberResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMember(string issuerId, string memberId)
        {
            var result = await _workflowService.GetMemberAsync(issuerId, memberId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Lists all active team members for the given issuer.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        [HttpGet("{issuerId}/members")]
        [ProducesResponseType(typeof(IssuerTeamMembersResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListMembers(string issuerId)
        {
            var result = await _workflowService.ListMembersAsync(issuerId);
            return Ok(result);
        }

        // ── Workflow Items ─────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new workflow item in Prepared state for the given issuer.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="request">Workflow item details.</param>
        [HttpPost("{issuerId}/workflows")]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateWorkflowItem(string issuerId, [FromBody] CreateWorkflowItemRequest request)
        {
            var actorId = GetActorId();
            _logger.LogInformation(
                "CreateWorkflowItem. IssuerId={IssuerId} ItemType={ItemType} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(issuerId),
                request?.ItemType,
                LoggingHelper.SanitizeLogInput(actorId));

            var result = await _workflowService.CreateWorkflowItemAsync(issuerId, request!, actorId);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Gets a single workflow item by its ID, scoped to the issuer.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item identifier.</param>
        [HttpGet("{issuerId}/workflows/{workflowId}")]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetWorkflowItem(string issuerId, string workflowId)
        {
            var result = await _workflowService.GetWorkflowItemAsync(issuerId, workflowId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND)
                return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Lists workflow items for an issuer, with optional filtering by state or assignee.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="state">Optional state filter.</param>
        /// <param name="assignedTo">Optional assignee filter.</param>
        [HttpGet("{issuerId}/workflows")]
        [ProducesResponseType(typeof(WorkflowItemListResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListWorkflowItems(string issuerId, [FromQuery] WorkflowApprovalState? state = null, [FromQuery] string? assignedTo = null)
        {
            var result = await _workflowService.ListWorkflowItemsAsync(issuerId, state, assignedTo);
            return Ok(result);
        }

        // ── Workflow Transitions ───────────────────────────────────────────────

        /// <summary>
        /// Submits a Prepared workflow item for review (Prepared → PendingReview).
        /// Fails with 400 if the item is not in Prepared state.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item identifier.</param>
        /// <param name="request">Optional submission note.</param>
        [HttpPost("{issuerId}/workflows/{workflowId}/submit")]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SubmitForReview(string issuerId, string workflowId, [FromBody] SubmitWorkflowItemRequest? request)
        {
            var actorId       = GetActorId();
            var correlationId = HttpContext.TraceIdentifier;
            var result = await _workflowService.SubmitForReviewAsync(issuerId, workflowId, request ?? new SubmitWorkflowItemRequest(), actorId, correlationId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Approves a PendingReview workflow item (PendingReview → Approved).
        /// Fails with 400 if the item is not in PendingReview state.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item identifier.</param>
        /// <param name="request">Optional approval note.</param>
        [HttpPost("{issuerId}/workflows/{workflowId}/approve")]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Approve(string issuerId, string workflowId, [FromBody] ApproveWorkflowItemRequest? request)
        {
            var actorId       = GetActorId();
            var correlationId = HttpContext.TraceIdentifier;
            var result = await _workflowService.ApproveAsync(issuerId, workflowId, request ?? new ApproveWorkflowItemRequest(), actorId, correlationId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Rejects a PendingReview workflow item (PendingReview → Rejected).
        /// Fails with 400 if the item is not in PendingReview state or if RejectionReason is missing.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item identifier.</param>
        /// <param name="request">Rejection reason (required).</param>
        [HttpPost("{issuerId}/workflows/{workflowId}/reject")]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Reject(string issuerId, string workflowId, [FromBody] RejectWorkflowItemRequest request)
        {
            var actorId       = GetActorId();
            var correlationId = HttpContext.TraceIdentifier;
            var result = await _workflowService.RejectAsync(issuerId, workflowId, request, actorId, correlationId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Returns a PendingReview item to the creator with change requests (PendingReview → NeedsChanges).
        /// Fails with 400 if the item is not in PendingReview state or if ChangeDescription is missing.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item identifier.</param>
        /// <param name="request">Change description (required).</param>
        [HttpPost("{issuerId}/workflows/{workflowId}/request-changes")]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RequestChanges(string issuerId, string workflowId, [FromBody] RequestChangesRequest request)
        {
            var actorId       = GetActorId();
            var correlationId = HttpContext.TraceIdentifier;
            var result = await _workflowService.RequestChangesAsync(issuerId, workflowId, request, actorId, correlationId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Resubmits a NeedsChanges item back for review (NeedsChanges → PendingReview).
        /// Fails with 400 if the item is not in NeedsChanges state.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item identifier.</param>
        /// <param name="request">Optional resubmission note.</param>
        [HttpPost("{issuerId}/workflows/{workflowId}/resubmit")]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Resubmit(string issuerId, string workflowId, [FromBody] SubmitWorkflowItemRequest? request)
        {
            var actorId       = GetActorId();
            var correlationId = HttpContext.TraceIdentifier;
            var result = await _workflowService.ResubmitAsync(issuerId, workflowId, request ?? new SubmitWorkflowItemRequest(), actorId, correlationId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Reassigns a workflow item to a different active team member.
        /// The new assignee must be a member of the same issuer.
        /// Fails-closed on completed or rejected items.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item identifier.</param>
        /// <param name="request">New assignee and optional note.</param>
        [HttpPost("{issuerId}/workflows/{workflowId}/reassign")]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Reassign(string issuerId, string workflowId, [FromBody] ReassignWorkflowItemRequest request)
        {
            var actorId       = GetActorId();
            var correlationId = HttpContext.TraceIdentifier;
            var result = await _workflowService.ReassignAsync(issuerId, workflowId, request, actorId, correlationId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Marks an Approved workflow item as Completed (Approved → Completed).
        /// Fails with 400 if the item is not in Approved state.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="workflowId">Workflow item identifier.</param>
        /// <param name="request">Optional completion note.</param>
        [HttpPost("{issuerId}/workflows/{workflowId}/complete")]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(WorkflowItemResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Complete(string issuerId, string workflowId, [FromBody] CompleteWorkflowItemRequest? request)
        {
            var actorId       = GetActorId();
            var correlationId = HttpContext.TraceIdentifier;
            var result = await _workflowService.CompleteAsync(issuerId, workflowId, request ?? new CompleteWorkflowItemRequest(), actorId, correlationId);
            if (!result.Success && result.ErrorCode == ErrorCodes.NOT_FOUND) return NotFound(result);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        // ── Queries ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns an approval summary for the issuer suitable for dashboard rendering.
        /// Includes counts per state, active team member count, and the five most recent pending items.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        [HttpGet("{issuerId}/summary")]
        [ProducesResponseType(typeof(WorkflowApprovalSummaryResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetApprovalSummary(string issuerId)
        {
            var result = await _workflowService.GetApprovalSummaryAsync(issuerId);
            return Ok(result);
        }

        /// <summary>
        /// Returns all workflow items currently assigned to a specific team member within the issuer.
        /// </summary>
        /// <param name="issuerId">Issuer identifier scope.</param>
        /// <param name="assigneeId">User ID of the assignee to query.</param>
        [HttpGet("{issuerId}/queue/{assigneeId}")]
        [ProducesResponseType(typeof(WorkflowItemListResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAssignedQueue(string issuerId, string assigneeId)
        {
            var result = await _workflowService.GetAssignedQueueAsync(issuerId, assigneeId);
            return Ok(result);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private string GetActorId() =>
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("nameid")?.Value
                ?? "anonymous";
    }
}
