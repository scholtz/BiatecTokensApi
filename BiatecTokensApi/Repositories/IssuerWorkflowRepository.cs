using BiatecTokensApi.Models.IssuerWorkflow;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory singleton repository for issuer workflow team members and workflow items.
    ///
    /// This implementation provides durable-within-process storage: data survives
    /// individual HTTP requests and DI scope changes because the repository is registered
    /// as a singleton. It serves as the authoritative state store for the
    /// <see cref="BiatecTokensApi.Services.IssuerWorkflowService"/>.
    ///
    /// For persistent-across-restart storage, replace this implementation with a
    /// database-backed version while keeping the same interface contract.
    /// </summary>
    public class IssuerWorkflowRepository : IIssuerWorkflowRepository
    {
        private readonly ILogger<IssuerWorkflowRepository> _logger;

        // Key: "{issuerId}:{memberId}"
        private readonly ConcurrentDictionary<string, IssuerTeamMember> _members = new();

        // Key: "{issuerId}:{workflowId}"
        private readonly ConcurrentDictionary<string, WorkflowItem> _workflowItems = new();

        // Key: "{issuerId}:{workflowId}" → list of audit entries (append-only)
        private readonly ConcurrentDictionary<string, List<WorkflowAuditEntry>> _auditEntries = new();

        /// <summary>
        /// Initializes a new instance of <see cref="IssuerWorkflowRepository"/>.
        /// </summary>
        public IssuerWorkflowRepository(ILogger<IssuerWorkflowRepository> logger)
        {
            _logger = logger;
        }

        // ── Team Membership ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task UpsertMemberAsync(IssuerTeamMember member)
        {
            var key = MemberKey(member.IssuerId, member.MemberId);
            _members[key] = member;
            _logger.LogDebug(
                "IssuerWorkflowRepository: upserted member. IssuerId={IssuerId} MemberId={MemberId} UserId={UserId}",
                member.IssuerId, member.MemberId, member.UserId);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<IssuerTeamMember?> GetMemberByIdAsync(string issuerId, string memberId)
        {
            _members.TryGetValue(MemberKey(issuerId, memberId), out var member);
            return Task.FromResult(member);
        }

        /// <inheritdoc/>
        public Task<List<IssuerTeamMember>> ListMembersAsync(string issuerId)
        {
            var result = _members.Values
                .Where(m => m.IssuerId == issuerId)
                .OrderBy(m => m.AddedAt)
                .ToList();
            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<bool> HasActiveMembersAsync(string issuerId)
        {
            bool hasAny = _members.Values.Any(m => m.IssuerId == issuerId && m.IsActive);
            return Task.FromResult(hasAny);
        }

        /// <inheritdoc/>
        public Task<bool> IsMemberAsync(string issuerId, string userId)
        {
            bool isMember = _members.Values.Any(m => m.IssuerId == issuerId && m.UserId == userId && m.IsActive);
            return Task.FromResult(isMember);
        }

        // ── Workflow Items ─────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task UpsertWorkflowItemAsync(WorkflowItem item)
        {
            var key = WorkflowKey(item.IssuerId, item.WorkflowId);
            _workflowItems[key] = item;
            _logger.LogDebug(
                "IssuerWorkflowRepository: upserted workflow item. IssuerId={IssuerId} WorkflowId={WorkflowId} State={State}",
                item.IssuerId, item.WorkflowId, item.State);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<WorkflowItem?> GetWorkflowItemAsync(string issuerId, string workflowId)
        {
            _workflowItems.TryGetValue(WorkflowKey(issuerId, workflowId), out var item);
            return Task.FromResult(item);
        }

        /// <inheritdoc/>
        public Task<List<WorkflowItem>> ListWorkflowItemsAsync(
            string issuerId,
            WorkflowApprovalState? stateFilter = null,
            string? assignedTo = null)
        {
            var query = _workflowItems.Values.Where(i => i.IssuerId == issuerId);

            if (stateFilter.HasValue)
                query = query.Where(i => i.State == stateFilter.Value);

            if (!string.IsNullOrWhiteSpace(assignedTo))
                query = query.Where(i => i.AssignedTo == assignedTo);

            var result = query.OrderByDescending(i => i.UpdatedAt).ToList();
            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task AppendAuditEntryAsync(string issuerId, string workflowId, WorkflowAuditEntry entry)
        {
            var key = WorkflowKey(issuerId, workflowId);
            var list = _auditEntries.GetOrAdd(key, _ => new List<WorkflowAuditEntry>());
            lock (list)
            {
                list.Add(entry);
            }

            // Also append to the workflow item's in-object audit trail for convenience
            if (_workflowItems.TryGetValue(key, out var item))
            {
                lock (item.AuditHistory)
                {
                    // Avoid duplicate if already added by the caller
                    if (!item.AuditHistory.Any(e => e.EntryId == entry.EntryId))
                        item.AuditHistory.Add(entry);
                }
            }

            _logger.LogDebug(
                "IssuerWorkflowRepository: appended audit entry. IssuerId={IssuerId} WorkflowId={WorkflowId} EntryId={EntryId}",
                issuerId, workflowId, entry.EntryId);
            return Task.CompletedTask;
        }

        // ── Key Helpers ────────────────────────────────────────────────────────

        private static string MemberKey(string issuerId, string memberId) =>
            $"{issuerId}:{memberId}";

        private static string WorkflowKey(string issuerId, string workflowId) =>
            $"{issuerId}:{workflowId}";
    }
}
