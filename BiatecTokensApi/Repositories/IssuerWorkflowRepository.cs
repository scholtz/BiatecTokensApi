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

        // Key: "{issuerId}:{workflowId}" → Value: inner dict keyed by EntryId
        // Using ConcurrentDictionary<entryId, entry> as the authoritative audit store:
        //   - TryAdd is lock-free and idempotent — two concurrent calls with the same EntryId
        //     are harmless; the second TryAdd is simply a no-op.
        //   - Eliminates the check-then-add race that would exist with a plain List<T>.
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WorkflowAuditEntry>> _auditEntries = new();

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
        public Task<IssuerTeamMember?> GetMemberByUserIdAsync(string issuerId, string userId)
        {
            var member = _members.Values.FirstOrDefault(
                m => m.IssuerId == issuerId && m.UserId == userId && m.IsActive);
            return Task.FromResult(member);
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

            // Seed the authoritative audit-entry store from the item's current history.
            // This ensures entries added by service code (item.AuditHistory.Add → Upsert)
            // are visible to AppendAuditEntryAsync callers and to GetWorkflowItemAsync.
            if (item.AuditHistory.Count > 0)
            {
                var entries = _auditEntries.GetOrAdd(key, _ => new ConcurrentDictionary<string, WorkflowAuditEntry>());
                foreach (var entry in item.AuditHistory)
                    entries.TryAdd(entry.EntryId, entry); // idempotent: no-op if already present
            }

            _logger.LogDebug(
                "IssuerWorkflowRepository: upserted workflow item. IssuerId={IssuerId} WorkflowId={WorkflowId} State={State}",
                item.IssuerId, item.WorkflowId, item.State);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<WorkflowItem?> GetWorkflowItemAsync(string issuerId, string workflowId)
        {
            var key = WorkflowKey(issuerId, workflowId);
            if (!_workflowItems.TryGetValue(key, out var item))
                return Task.FromResult<WorkflowItem?>(null);

            // Synthesize a fresh, ordered, deduplicated audit history from the authoritative
            // entry store. This guarantees that readers always observe all entries regardless
            // of whether they arrived via UpsertWorkflowItemAsync or AppendAuditEntryAsync,
            // and without any concurrent List<T> mutation.
            if (_auditEntries.TryGetValue(key, out var entries))
                item.AuditHistory = entries.Values.OrderBy(e => e.Timestamp).ToList();

            return Task.FromResult<WorkflowItem?>(item);
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

            // Synthesize complete audit history for each item from the authoritative entry store.
            foreach (var item in result)
            {
                var key = WorkflowKey(item.IssuerId, item.WorkflowId);
                if (_auditEntries.TryGetValue(key, out var entries))
                    item.AuditHistory = entries.Values.OrderBy(e => e.Timestamp).ToList();
            }

            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task AppendAuditEntryAsync(string issuerId, string workflowId, WorkflowAuditEntry entry)
        {
            var key = WorkflowKey(issuerId, workflowId);
            var entries = _auditEntries.GetOrAdd(key, _ => new ConcurrentDictionary<string, WorkflowAuditEntry>());

            // TryAdd is atomic and idempotent: if two concurrent threads attempt to add the
            // same EntryId, exactly one succeeds and the other is silently ignored.
            // No lock required, no check-then-add race, no List<T> mutation.
            entries.TryAdd(entry.EntryId, entry);

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
