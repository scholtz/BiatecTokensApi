using BiatecTokensApi.Models.EnterpriseComplianceReview;
using BiatecTokensApi.Repositories.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory singleton repository for enterprise compliance review decision records
    /// and diagnostics events.
    ///
    /// Registered as a singleton so that review decisions and diagnostics survive
    /// individual HTTP requests and DI scope recycling within a single application process.
    ///
    /// For persistent-across-restart storage, replace this implementation with a
    /// database-backed version while keeping the same interface contract.
    /// </summary>
    public class ComplianceReviewRepository : IComplianceReviewRepository
    {
        private readonly ILogger<ComplianceReviewRepository> _logger;

        // Key: decisionId
        private readonly ConcurrentDictionary<string, PersistedReviewDecision> _decisions = new();

        // Key: issuerId → bounded list of events (newest at the end)
        private readonly ConcurrentDictionary<string, List<ReviewDiagnosticsEvent>> _diagnosticsLog = new();

        private const int MaxDiagnosticsEventsPerIssuer = 200;

        /// <summary>
        /// Initializes a new instance of <see cref="ComplianceReviewRepository"/>.
        /// </summary>
        public ComplianceReviewRepository(ILogger<ComplianceReviewRepository> logger)
        {
            _logger = logger;
        }

        // ── Decision Metadata ──────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task SaveDecisionAsync(PersistedReviewDecision decision)
        {
            if (_decisions.TryAdd(decision.DecisionId, decision))
            {
                _logger.LogInformation(
                    "ComplianceReviewRepository: saved decision. DecisionId={DecisionId} IssuerId={IssuerId} WorkflowId={WorkflowId} Type={Type} Actor={Actor}",
                    decision.DecisionId, decision.IssuerId, decision.WorkflowId, decision.DecisionType, decision.ActorId);
            }
            else
            {
                _logger.LogWarning(
                    "ComplianceReviewRepository: decision already exists, skipping save. DecisionId={DecisionId}",
                    decision.DecisionId);
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<PersistedReviewDecision?> GetDecisionByIdAsync(string decisionId)
        {
            _decisions.TryGetValue(decisionId, out var decision);
            return Task.FromResult(decision);
        }

        /// <inheritdoc/>
        public Task<List<PersistedReviewDecision>> GetDecisionsForWorkflowAsync(
            string issuerId, string workflowId)
        {
            var result = _decisions.Values
                .Where(d => d.IssuerId == issuerId && d.WorkflowId == workflowId)
                .OrderBy(d => d.Timestamp)
                .ToList();
            return Task.FromResult(result);
        }

        /// <inheritdoc/>
        public Task<List<PersistedReviewDecision>> QueryDecisionsAsync(
            string issuerId,
            DateTime? fromUtc = null,
            DateTime? toUtc = null,
            ReviewDecisionType? decisionType = null,
            string? actorId = null)
        {
            var query = _decisions.Values.Where(d => d.IssuerId == issuerId);

            if (fromUtc.HasValue)
                query = query.Where(d => d.Timestamp >= fromUtc.Value);
            if (toUtc.HasValue)
                query = query.Where(d => d.Timestamp <= toUtc.Value);
            if (decisionType.HasValue)
                query = query.Where(d => d.DecisionType == decisionType.Value);
            if (!string.IsNullOrWhiteSpace(actorId))
                query = query.Where(d => d.ActorId == actorId);

            var result = query.OrderByDescending(d => d.Timestamp).ToList();
            return Task.FromResult(result);
        }

        // ── Diagnostics Events ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task AppendDiagnosticsEventAsync(string issuerId, ReviewDiagnosticsEvent ev)
        {
            var log = _diagnosticsLog.GetOrAdd(issuerId, _ => new List<ReviewDiagnosticsEvent>());
            lock (log)
            {
                log.Add(ev);
                while (log.Count > MaxDiagnosticsEventsPerIssuer)
                    log.RemoveAt(0);
            }

            _logger.LogDebug(
                "ComplianceReviewRepository: appended diagnostics event. IssuerId={IssuerId} Category={Category} Severity={Severity}",
                issuerId, ev.Category, ev.Severity);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<List<ReviewDiagnosticsEvent>> GetRecentDiagnosticsEventsAsync(
            string issuerId, int maxCount = 50)
        {
            if (!_diagnosticsLog.TryGetValue(issuerId, out var log))
                return Task.FromResult(new List<ReviewDiagnosticsEvent>());

            List<ReviewDiagnosticsEvent> result;
            lock (log)
            {
                result = log
                    .OrderByDescending(e => e.Timestamp)
                    .Take(maxCount)
                    .ToList();
            }
            return Task.FromResult(result);
        }
    }
}
