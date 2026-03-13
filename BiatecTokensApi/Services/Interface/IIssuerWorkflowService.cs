using BiatecTokensApi.Models.IssuerWorkflow;

namespace BiatecTokensApi.Services.Interface
{
    /// <summary>
    /// Service interface for issuer-scoped team roles, membership management,
    /// and workflow approval-state operations.
    /// Tenant isolation is enforced: all methods scope results to the provided issuerId.
    /// </summary>
    public interface IIssuerWorkflowService
    {
        // ── Transition Validation ─────────────────────────────────────────────

        /// <summary>
        /// Validates whether a workflow state transition is permitted by the state machine rules.
        /// Returns a detailed result indicating validity and the human-readable reason.
        /// </summary>
        WorkflowTransitionValidationResult ValidateTransition(WorkflowApprovalState from, WorkflowApprovalState to);

        /// <summary>
        /// Returns the set of states reachable from the given state.
        /// </summary>
        IReadOnlySet<WorkflowApprovalState> GetAllowedTransitions(WorkflowApprovalState from);

        // ── Team Membership ────────────────────────────────────────────────────

        /// <summary>Adds a new member to the issuer team.</summary>
        Task<IssuerTeamMemberResponse> AddMemberAsync(string issuerId, AddIssuerTeamMemberRequest request, string actorId);

        /// <summary>Updates the role or display name of an existing team member.</summary>
        Task<IssuerTeamMemberResponse> UpdateMemberAsync(string issuerId, string memberId, UpdateIssuerTeamMemberRequest request, string actorId);

        /// <summary>Deactivates a team member (soft-delete). Fails-closed if member is not found or belongs to a different issuer.</summary>
        Task<IssuerTeamMemberResponse> RemoveMemberAsync(string issuerId, string memberId, string actorId);

        /// <summary>Gets a single team member by memberId, scoped to the issuer.</summary>
        Task<IssuerTeamMemberResponse> GetMemberAsync(string issuerId, string memberId);

        /// <summary>Lists all active team members for the given issuer.</summary>
        Task<IssuerTeamMembersResponse> ListMembersAsync(string issuerId);

        // ── Workflow Items ─────────────────────────────────────────────────────

        /// <summary>Creates a new workflow item in Prepared state.</summary>
        Task<WorkflowItemResponse> CreateWorkflowItemAsync(string issuerId, CreateWorkflowItemRequest request, string actorId);

        /// <summary>Gets a single workflow item by workflowId, scoped to the issuer.</summary>
        Task<WorkflowItemResponse> GetWorkflowItemAsync(string issuerId, string workflowId);

        /// <summary>Lists workflow items for the issuer, optionally filtered by state or assignee.</summary>
        Task<WorkflowItemListResponse> ListWorkflowItemsAsync(string issuerId, WorkflowApprovalState? stateFilter = null, string? assignedTo = null);

        // ── Workflow Transitions ───────────────────────────────────────────────

        /// <summary>
        /// Submits a Prepared item for review (Prepared → PendingReview).
        /// Fails-closed if the item is not in Prepared state.
        /// </summary>
        Task<WorkflowItemResponse> SubmitForReviewAsync(string issuerId, string workflowId, SubmitWorkflowItemRequest request, string actorId, string correlationId);

        /// <summary>
        /// Approves a PendingReview item (PendingReview → Approved).
        /// Requires the actor to hold ComplianceReviewer, FinanceReviewer, or Admin role.
        /// Fails-closed if the item is not in PendingReview state.
        /// </summary>
        Task<WorkflowItemResponse> ApproveAsync(string issuerId, string workflowId, ApproveWorkflowItemRequest request, string actorId, string correlationId);

        /// <summary>
        /// Rejects a PendingReview item (PendingReview → Rejected).
        /// Requires the actor to hold ComplianceReviewer, FinanceReviewer, or Admin role.
        /// Fails-closed if the item is not in PendingReview state.
        /// </summary>
        Task<WorkflowItemResponse> RejectAsync(string issuerId, string workflowId, RejectWorkflowItemRequest request, string actorId, string correlationId);

        /// <summary>
        /// Returns a PendingReview item to NeedsChanges state so the creator can revise it.
        /// Fails-closed if the item is not in PendingReview state.
        /// </summary>
        Task<WorkflowItemResponse> RequestChangesAsync(string issuerId, string workflowId, RequestChangesRequest request, string actorId, string correlationId);

        /// <summary>
        /// Resubmits a NeedsChanges item for review (NeedsChanges → PendingReview).
        /// Typically performed by the original creator after addressing feedback.
        /// Fails-closed if the item is not in NeedsChanges state.
        /// </summary>
        Task<WorkflowItemResponse> ResubmitAsync(string issuerId, string workflowId, SubmitWorkflowItemRequest request, string actorId, string correlationId);

        /// <summary>
        /// Reassigns the workflow item to a different team member.
        /// The new assignee must be an active member of the same issuer.
        /// </summary>
        Task<WorkflowItemResponse> ReassignAsync(string issuerId, string workflowId, ReassignWorkflowItemRequest request, string actorId, string correlationId);

        /// <summary>
        /// Marks an Approved item as Completed (Approved → Completed).
        /// Fails-closed if the item is not in Approved state.
        /// </summary>
        Task<WorkflowItemResponse> CompleteAsync(string issuerId, string workflowId, CompleteWorkflowItemRequest request, string actorId, string correlationId);

        // ── Queries ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a summary of pending approvals and workflow activity for the issuer's dashboard.
        /// </summary>
        Task<WorkflowApprovalSummaryResponse> GetApprovalSummaryAsync(string issuerId);

        /// <summary>
        /// Returns workflow items assigned to a specific actor within the issuer.
        /// </summary>
        Task<WorkflowItemListResponse> GetAssignedQueueAsync(string issuerId, string assigneeActorId);
    }
}
