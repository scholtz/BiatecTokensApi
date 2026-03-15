using BiatecTokensApi.Models.ApprovalWorkflow;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory singleton repository for approval workflow stage decisions and audit events.
    ///
    /// Registered as a singleton so that decisions and audit events survive individual HTTP
    /// requests and DI scope recycling within a single application process.
    ///
    /// For persistent-across-restart storage, replace this implementation with a
    /// database-backed version while keeping the same interface contract.
    /// </summary>
    public class ApprovalWorkflowRepository : IApprovalWorkflowRepository
    {
        private readonly ILogger<ApprovalWorkflowRepository> _logger;

        // Key: releasePackageId → append-only list of stage decisions
        private readonly ConcurrentDictionary<string, List<PersistedApprovalStageDecision>> _decisions = new();

        // Key: releasePackageId → append-only list of audit events
        private readonly ConcurrentDictionary<string, List<ApprovalAuditEvent>> _auditLog = new();

        private const int MaxAuditEventsPerPackage = 500;

        /// <summary>
        /// Initializes a new instance of <see cref="ApprovalWorkflowRepository"/>.
        /// </summary>
        public ApprovalWorkflowRepository(ILogger<ApprovalWorkflowRepository> logger)
        {
            _logger = logger;
        }

        // ── Stage Decisions ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task SaveStageDecisionAsync(PersistedApprovalStageDecision decision)
        {
            List<PersistedApprovalStageDecision> list =
                _decisions.GetOrAdd(decision.PackageId, _ => new List<PersistedApprovalStageDecision>());

            lock (list)
            {
                list.Add(decision);
            }

            _logger.LogInformation(
                "ApprovalWorkflowRepository: saved stage decision. PackageId={PackageId} Stage={Stage} Status={Status} Actor={Actor} DecisionId={DecisionId}",
                decision.PackageId, decision.StageType, decision.Status, decision.ActorId, decision.DecisionId);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<List<PersistedApprovalStageDecision>> GetStageDecisionsForPackageAsync(string releasePackageId)
        {
            if (!_decisions.TryGetValue(releasePackageId, out List<PersistedApprovalStageDecision>? list))
                return Task.FromResult(new List<PersistedApprovalStageDecision>());

            List<PersistedApprovalStageDecision> snapshot;
            lock (list)
            {
                snapshot = list.OrderBy(d => d.Timestamp).ToList();
            }

            return Task.FromResult(snapshot);
        }

        // ── Audit Events ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task AppendAuditEventAsync(string releasePackageId, ApprovalAuditEvent ev)
        {
            List<ApprovalAuditEvent> log =
                _auditLog.GetOrAdd(releasePackageId, _ => new List<ApprovalAuditEvent>());

            lock (log)
            {
                log.Add(ev);

                // Evict oldest events when limit is exceeded
                while (log.Count > MaxAuditEventsPerPackage)
                    log.RemoveAt(0);
            }

            _logger.LogInformation(
                "ApprovalWorkflowRepository: appended audit event. PackageId={PackageId} EventType={EventType} EventId={EventId}",
                releasePackageId, ev.EventType, ev.EventId);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<List<ApprovalAuditEvent>> GetAuditEventsAsync(string releasePackageId, int maxCount = 100)
        {
            if (!_auditLog.TryGetValue(releasePackageId, out List<ApprovalAuditEvent>? log))
                return Task.FromResult(new List<ApprovalAuditEvent>());

            List<ApprovalAuditEvent> snapshot;
            lock (log)
            {
                snapshot = log
                    .OrderByDescending(e => e.Timestamp)
                    .Take(maxCount)
                    .ToList();
            }

            return Task.FromResult(snapshot);
        }
    }
}
