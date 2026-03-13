using System.ComponentModel.DataAnnotations;

namespace BiatecTokensApi.Models.IssuerWorkflow
{
    // ── Enumerations ──────────────────────────────────────────────────────────

    /// <summary>
    /// Roles available to members of an issuer team.
    /// Maps to common enterprise personas for regulated token operations.
    /// </summary>
    public enum IssuerTeamRole
    {
        /// <summary>Prepares and initiates workflow items; executes operational tasks.</summary>
        Operator = 0,

        /// <summary>Reviews compliance aspects and policy changes.</summary>
        ComplianceReviewer = 1,

        /// <summary>Reviews financial and economic implications of token operations.</summary>
        FinanceReviewer = 2,

        /// <summary>Full administrative rights including membership management and final approvals.</summary>
        Admin = 3,

        /// <summary>Read-only access to view workflow state and audit history without acting.</summary>
        ReadOnlyObserver = 4
    }

    /// <summary>
    /// Approval lifecycle states for sensitive workflow items.
    /// Transitions are deterministic and fail-closed for unauthorized or ambiguous moves.
    /// </summary>
    public enum WorkflowApprovalState
    {
        /// <summary>Item has been created and saved but not yet submitted for review.</summary>
        Prepared = 0,

        /// <summary>Item has been submitted and is waiting for a reviewer to act.</summary>
        PendingReview = 1,

        /// <summary>Item has been approved by the required reviewer or approver.</summary>
        Approved = 2,

        /// <summary>Item has been rejected; the creator should address the feedback.</summary>
        Rejected = 3,

        /// <summary>Reviewer returned the item requesting specific changes before re-review.</summary>
        NeedsChanges = 4,

        /// <summary>Item has been fully processed and closed.</summary>
        Completed = 5
    }

    /// <summary>
    /// Categories of workflow items supported by the issuer workflow engine.
    /// </summary>
    public enum WorkflowItemType
    {
        /// <summary>Sign-off required for a token launch readiness checkpoint.</summary>
        LaunchReadinessSignOff = 0,

        /// <summary>Approval required for a whitelist policy update.</summary>
        WhitelistPolicyUpdate = 1,

        /// <summary>Review of compliance evidence before a regulated operation.</summary>
        ComplianceEvidenceReview = 2,

        /// <summary>General issuer-level change requiring approval.</summary>
        GeneralApproval = 3
    }

    // ── Team Membership ───────────────────────────────────────────────────────

    /// <summary>
    /// Represents a single member of an issuer's team with their assigned role.
    /// </summary>
    public class IssuerTeamMember
    {
        /// <summary>Unique identifier for this membership record.</summary>
        public string MemberId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Issuer scope — members from one issuer cannot see another issuer's records.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>User identifier (e.g. email or Algorand address).</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>Display name for the member, used in UI and audit labels.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Role assigned to this member within the issuer team.</summary>
        public IssuerTeamRole Role { get; set; } = IssuerTeamRole.ReadOnlyObserver;

        /// <summary>Whether this membership is currently active.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>When this membership was created.</summary>
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Actor who added this member.</summary>
        public string AddedBy { get; set; } = string.Empty;

        /// <summary>When this membership was last updated.</summary>
        public DateTime? UpdatedAt { get; set; }
    }

    // ── Workflow Items ────────────────────────────────────────────────────────

    /// <summary>
    /// A workflow item representing a piece of work that requires approval or review
    /// within an issuer's team.
    /// </summary>
    public class WorkflowItem
    {
        /// <summary>Unique identifier for this workflow item.</summary>
        public string WorkflowId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Issuer this workflow item belongs to; enforces tenant isolation.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Category of workflow item.</summary>
        public WorkflowItemType ItemType { get; set; }

        /// <summary>Current approval state.</summary>
        public WorkflowApprovalState State { get; set; } = WorkflowApprovalState.Prepared;

        /// <summary>Short human-readable title.</summary>
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>Description of the change or action being approved.</summary>
        [StringLength(2000)]
        public string? Description { get; set; }

        /// <summary>Actor who created this item.</summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>Actor currently responsible for taking action (reviewer or assignee).</summary>
        public string? AssignedTo { get; set; }

        /// <summary>Actor who last reviewed this item.</summary>
        public string? LatestReviewerActorId { get; set; }

        /// <summary>Actor who approved or rejected this item.</summary>
        public string? ApproverActorId { get; set; }

        /// <summary>Optional reference to an external entity (e.g. policyId, assetId).</summary>
        public string? ExternalReference { get; set; }

        /// <summary>When the item was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the item was last updated.</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>When the item was approved (if applicable).</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>When the item was rejected (if applicable).</summary>
        public DateTime? RejectedAt { get; set; }

        /// <summary>When the item was completed (if applicable).</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>Reason provided when rejecting or requesting changes.</summary>
        public string? RejectionOrChangeReason { get; set; }

        /// <summary>Chronological audit log of all transitions for this item.</summary>
        public List<WorkflowAuditEntry> AuditHistory { get; set; } = new();

        /// <summary>Arbitrary key/value metadata for extensibility.</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Audit entry recording a single state transition in a workflow item's history.
    /// </summary>
    public class WorkflowAuditEntry
    {
        /// <summary>Unique identifier for this entry.</summary>
        public string EntryId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Workflow item this entry belongs to.</summary>
        public string WorkflowId { get; set; } = string.Empty;

        /// <summary>State before the transition.</summary>
        public WorkflowApprovalState FromState { get; set; }

        /// <summary>State after the transition.</summary>
        public WorkflowApprovalState ToState { get; set; }

        /// <summary>Actor who caused the transition.</summary>
        public string ActorId { get; set; } = string.Empty;

        /// <summary>Optional note from the actor describing the reason for the transition.</summary>
        public string? Note { get; set; }

        /// <summary>When this entry was recorded.</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Correlation ID for distributed tracing.</summary>
        public string? CorrelationId { get; set; }
    }

    // ── State Transition Validation ───────────────────────────────────────────

    /// <summary>
    /// Result of validating a workflow state transition.
    /// </summary>
    public class WorkflowTransitionValidationResult
    {
        /// <summary>Whether the transition is permitted.</summary>
        public bool IsValid { get; set; }

        /// <summary>Human-readable explanation of why the transition is or is not allowed.</summary>
        public string? Reason { get; set; }
    }

    // ── Request Models ────────────────────────────────────────────────────────

    /// <summary>
    /// Request to add a new member to an issuer team.
    /// </summary>
    public class AddIssuerTeamMemberRequest
    {
        /// <summary>User identifier to add (email, Algorand address, or internal user ID).</summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>Optional display name for the member.</summary>
        [StringLength(200)]
        public string? DisplayName { get; set; }

        /// <summary>Role to assign.</summary>
        [Required]
        public IssuerTeamRole Role { get; set; }
    }

    /// <summary>
    /// Request to update an existing team member's role.
    /// </summary>
    public class UpdateIssuerTeamMemberRequest
    {
        /// <summary>New role for the member.</summary>
        [Required]
        public IssuerTeamRole Role { get; set; }

        /// <summary>Optional updated display name.</summary>
        [StringLength(200)]
        public string? DisplayName { get; set; }
    }

    /// <summary>
    /// Request to create a new workflow item.
    /// </summary>
    public class CreateWorkflowItemRequest
    {
        /// <summary>Category of this workflow item.</summary>
        [Required]
        public WorkflowItemType ItemType { get; set; }

        /// <summary>Short human-readable title.</summary>
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>Description of the change or action.</summary>
        [StringLength(2000)]
        public string? Description { get; set; }

        /// <summary>Optional actor to immediately assign the item to.</summary>
        public string? AssignedTo { get; set; }

        /// <summary>Optional external reference (policyId, assetId, etc.).</summary>
        public string? ExternalReference { get; set; }

        /// <summary>Optional additional metadata.</summary>
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Request to submit a workflow item for review (Prepared → PendingReview).
    /// </summary>
    public class SubmitWorkflowItemRequest
    {
        /// <summary>Optional note from the submitter.</summary>
        [StringLength(1000)]
        public string? SubmissionNote { get; set; }
    }

    /// <summary>
    /// Request to approve a workflow item.
    /// </summary>
    public class ApproveWorkflowItemRequest
    {
        /// <summary>Optional approval note.</summary>
        [StringLength(1000)]
        public string? ApprovalNote { get; set; }
    }

    /// <summary>
    /// Request to reject a workflow item.
    /// </summary>
    public class RejectWorkflowItemRequest
    {
        /// <summary>Reason for the rejection (required).</summary>
        [Required]
        [StringLength(1000)]
        public string RejectionReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to return a workflow item to the creator requesting specific changes.
    /// </summary>
    public class RequestChangesRequest
    {
        /// <summary>Description of the required changes (required).</summary>
        [Required]
        [StringLength(1000)]
        public string ChangeDescription { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to reassign a workflow item to a different team member.
    /// </summary>
    public class ReassignWorkflowItemRequest
    {
        /// <summary>User ID of the new assignee.</summary>
        [Required]
        public string NewAssigneeId { get; set; } = string.Empty;

        /// <summary>Optional note explaining the reassignment.</summary>
        [StringLength(500)]
        public string? ReassignmentNote { get; set; }
    }

    /// <summary>
    /// Request to mark a workflow item as completed.
    /// </summary>
    public class CompleteWorkflowItemRequest
    {
        /// <summary>Optional completion note.</summary>
        [StringLength(1000)]
        public string? CompletionNote { get; set; }
    }

    // ── Response Models ───────────────────────────────────────────────────────

    /// <summary>
    /// Response containing a single team member.
    /// </summary>
    public class IssuerTeamMemberResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error code if unsuccessful.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The team member record.</summary>
        public IssuerTeamMember? Member { get; set; }
    }

    /// <summary>
    /// Response containing a list of team members.
    /// </summary>
    public class IssuerTeamMembersResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error code if unsuccessful.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Team members for the requested issuer.</summary>
        public List<IssuerTeamMember> Members { get; set; } = new();

        /// <summary>Total count of active members.</summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Response containing a single workflow item.
    /// </summary>
    public class WorkflowItemResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error code if unsuccessful.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The workflow item record.</summary>
        public WorkflowItem? WorkflowItem { get; set; }
    }

    /// <summary>
    /// Response containing a list of workflow items (queue or summary view).
    /// </summary>
    public class WorkflowItemListResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error code if unsuccessful.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Workflow items matching the query.</summary>
        public List<WorkflowItem> Items { get; set; } = new();

        /// <summary>Total matching count (before pagination).</summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Summary of workflow approval activity for a given issuer, suitable for dashboard display.
    /// </summary>
    public class WorkflowApprovalSummary
    {
        /// <summary>Issuer this summary belongs to.</summary>
        public string IssuerId { get; set; } = string.Empty;

        /// <summary>Number of items waiting for review.</summary>
        public int PendingReviewCount { get; set; }

        /// <summary>Number of items that were approved.</summary>
        public int ApprovedCount { get; set; }

        /// <summary>Number of items rejected in the period.</summary>
        public int RejectedCount { get; set; }

        /// <summary>Number of items that need changes.</summary>
        public int NeedsChangesCount { get; set; }

        /// <summary>Number of completed items.</summary>
        public int CompletedCount { get; set; }

        /// <summary>Total active team members for this issuer.</summary>
        public int ActiveTeamMemberCount { get; set; }

        /// <summary>Most recent items pending review (up to 5), for at-a-glance dashboard use.</summary>
        public List<WorkflowItem> RecentPendingItems { get; set; } = new();
    }

    /// <summary>
    /// Response wrapping a workflow approval summary.
    /// </summary>
    public class WorkflowApprovalSummaryResponse
    {
        /// <summary>Whether the operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Error code if unsuccessful.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Human-readable error message.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>The approval summary.</summary>
        public WorkflowApprovalSummary? Summary { get; set; }
    }
}
