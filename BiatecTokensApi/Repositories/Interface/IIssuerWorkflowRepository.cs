using BiatecTokensApi.Models.IssuerWorkflow;

namespace BiatecTokensApi.Repositories.Interface
{
    /// <summary>
    /// Repository interface for durable issuer workflow and team-membership persistence.
    ///
    /// All write operations must be idempotent and tenant-isolated. Data stored here
    /// is the authoritative record of workflow state that must survive application
    /// restarts and redeployments.
    /// </summary>
    public interface IIssuerWorkflowRepository
    {
        // ── Team Membership ────────────────────────────────────────────────────

        /// <summary>Persists a new or updated team member record.</summary>
        Task UpsertMemberAsync(IssuerTeamMember member);

        /// <summary>Retrieves a single active team member by (issuerId, userId).</summary>
        Task<IssuerTeamMember?> GetMemberByUserIdAsync(string issuerId, string userId);

        /// <summary>Retrieves a team member by composite key (issuerId + memberId).</summary>
        Task<IssuerTeamMember?> GetMemberByIdAsync(string issuerId, string memberId);

        /// <summary>Lists all members for the given issuer (active and inactive).</summary>
        Task<List<IssuerTeamMember>> ListMembersAsync(string issuerId);

        /// <summary>Returns true if the issuer has any active members.</summary>
        Task<bool> HasActiveMembersAsync(string issuerId);

        /// <summary>Returns true if the user is a member of the issuer.</summary>
        Task<bool> IsMemberAsync(string issuerId, string userId);

        // ── Workflow Items ─────────────────────────────────────────────────────

        /// <summary>Persists a new or updated workflow item (full replace).</summary>
        Task UpsertWorkflowItemAsync(WorkflowItem item);

        /// <summary>Retrieves a workflow item by (issuerId, workflowId).</summary>
        Task<WorkflowItem?> GetWorkflowItemAsync(string issuerId, string workflowId);

        /// <summary>Lists workflow items for an issuer with optional state and assignee filters.</summary>
        Task<List<WorkflowItem>> ListWorkflowItemsAsync(
            string issuerId,
            WorkflowApprovalState? stateFilter = null,
            string? assignedTo = null);

        /// <summary>
        /// Appends an audit entry to the workflow item's immutable audit trail.
        /// The entry is written to durable storage immediately.
        /// </summary>
        Task AppendAuditEntryAsync(string issuerId, string workflowId, WorkflowAuditEntry entry);
    }
}
