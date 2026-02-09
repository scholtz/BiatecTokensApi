using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;

namespace BiatecTokensApi.Repositories
{
    /// <summary>
    /// In-memory repository for compliance decisions
    /// </summary>
    /// <remarks>
    /// This implementation uses in-memory storage with immutable event logging.
    /// For production, this should be replaced with a persistent database implementation.
    /// </remarks>
    public class ComplianceDecisionRepository : IComplianceDecisionRepository
    {
        private readonly List<ComplianceDecision> _decisions = new();
        private readonly object _lock = new object();
        private readonly ILogger<ComplianceDecisionRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceDecisionRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public ComplianceDecisionRepository(ILogger<ComplianceDecisionRepository> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Creates a new compliance decision
        /// </summary>
        public Task CreateDecisionAsync(ComplianceDecision decision)
        {
            lock (_lock)
            {
                // Validate decision ID is unique
                if (_decisions.Any(d => d.Id == decision.Id))
                {
                    _logger.LogError("Attempted to create decision with duplicate ID: {DecisionId}", decision.Id);
                    throw new InvalidOperationException($"Decision with ID {decision.Id} already exists");
                }

                // Add the decision (immutable)
                _decisions.Add(decision);
                
                _logger.LogInformation(
                    "Created compliance decision: Id={DecisionId}, OrganizationId={OrganizationId}, Step={Step}, Outcome={Outcome}",
                    decision.Id,
                    decision.OrganizationId,
                    decision.Step,
                    decision.Outcome
                );
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets a compliance decision by ID
        /// </summary>
        public Task<ComplianceDecision?> GetDecisionByIdAsync(string decisionId)
        {
            lock (_lock)
            {
                var decision = _decisions.FirstOrDefault(d => d.Id == decisionId);
                return Task.FromResult(decision);
            }
        }

        /// <summary>
        /// Queries compliance decisions with filtering and pagination
        /// </summary>
        public Task<(List<ComplianceDecision> decisions, int totalCount)> QueryDecisionsAsync(QueryComplianceDecisionsRequest request)
        {
            lock (_lock)
            {
                var query = _decisions.AsEnumerable();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(request.OrganizationId))
                {
                    query = query.Where(d => d.OrganizationId.Equals(request.OrganizationId, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(request.OnboardingSessionId))
                {
                    query = query.Where(d => d.OnboardingSessionId != null && 
                                           d.OnboardingSessionId.Equals(request.OnboardingSessionId, StringComparison.OrdinalIgnoreCase));
                }

                if (request.Step.HasValue)
                {
                    query = query.Where(d => d.Step == request.Step.Value);
                }

                if (request.Outcome.HasValue)
                {
                    query = query.Where(d => d.Outcome == request.Outcome.Value);
                }

                if (!string.IsNullOrWhiteSpace(request.DecisionMaker))
                {
                    query = query.Where(d => d.DecisionMaker.Equals(request.DecisionMaker, StringComparison.OrdinalIgnoreCase));
                }

                if (request.FromDate.HasValue)
                {
                    query = query.Where(d => d.DecisionTimestamp >= request.FromDate.Value);
                }

                if (request.ToDate.HasValue)
                {
                    query = query.Where(d => d.DecisionTimestamp <= request.ToDate.Value);
                }

                // Filter superseded decisions unless explicitly included
                if (!request.IncludeSuperseded)
                {
                    query = query.Where(d => !d.IsSuperseded);
                }

                // Filter expired decisions unless explicitly included
                if (!request.IncludeExpired)
                {
                    var now = DateTime.UtcNow;
                    query = query.Where(d => !d.ExpiresAt.HasValue || d.ExpiresAt.Value > now);
                }

                // Order by decision timestamp descending
                query = query.OrderByDescending(d => d.DecisionTimestamp);

                // Get total count before pagination
                var totalCount = query.Count();

                // Apply pagination
                var pageSize = Math.Min(request.PageSize, 100); // Cap at 100
                var skip = (request.Page - 1) * pageSize;
                var decisions = query.Skip(skip).Take(pageSize).ToList();

                _logger.LogInformation(
                    "Query returned {Count} decisions out of {TotalCount} total (Page {Page}, PageSize {PageSize})",
                    decisions.Count,
                    totalCount,
                    request.Page,
                    pageSize
                );

                return Task.FromResult((decisions, totalCount));
            }
        }

        /// <summary>
        /// Gets the most recent active decision for an organization and step
        /// </summary>
        public Task<ComplianceDecision?> GetActiveDecisionAsync(string organizationId, OnboardingStep step)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var decision = _decisions
                    .Where(d => d.OrganizationId.Equals(organizationId, StringComparison.OrdinalIgnoreCase) &&
                               d.Step == step &&
                               !d.IsSuperseded &&
                               (!d.ExpiresAt.HasValue || d.ExpiresAt.Value > now))
                    .OrderByDescending(d => d.DecisionTimestamp)
                    .FirstOrDefault();

                return Task.FromResult(decision);
            }
        }

        /// <summary>
        /// Marks a decision as superseded
        /// </summary>
        public Task<bool> SupersedeDecisionAsync(string decisionId, string supersededById)
        {
            lock (_lock)
            {
                var decision = _decisions.FirstOrDefault(d => d.Id == decisionId);
                if (decision == null)
                {
                    _logger.LogWarning("Cannot supersede decision - not found: {DecisionId}", decisionId);
                    return Task.FromResult(false);
                }

                // Note: In a real immutable system, we wouldn't modify the decision
                // Instead, we'd create a new record. For this in-memory implementation,
                // we'll modify the existing object.
                decision.IsSuperseded = true;
                decision.SupersededAt = DateTime.UtcNow;
                decision.SupersededById = supersededById;

                _logger.LogInformation(
                    "Superseded decision: Id={DecisionId}, SupersededById={SupersededById}",
                    decisionId,
                    supersededById
                );

                return Task.FromResult(true);
            }
        }

        /// <summary>
        /// Gets decisions requiring review
        /// </summary>
        public Task<List<ComplianceDecision>> GetDecisionsRequiringReviewAsync(DateTime? beforeDate = null)
        {
            lock (_lock)
            {
                var targetDate = beforeDate ?? DateTime.UtcNow;
                var decisions = _decisions
                    .Where(d => d.RequiresReview &&
                               !d.IsSuperseded &&
                               d.NextReviewDate.HasValue &&
                               d.NextReviewDate.Value <= targetDate)
                    .OrderBy(d => d.NextReviewDate)
                    .ToList();

                return Task.FromResult(decisions);
            }
        }

        /// <summary>
        /// Gets expired decisions
        /// </summary>
        public Task<List<ComplianceDecision>> GetExpiredDecisionsAsync()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var decisions = _decisions
                    .Where(d => d.ExpiresAt.HasValue &&
                               d.ExpiresAt.Value <= now &&
                               !d.IsSuperseded)
                    .OrderBy(d => d.ExpiresAt)
                    .ToList();

                return Task.FromResult(decisions);
            }
        }

        /// <summary>
        /// Checks if a decision with the same parameters already exists (for idempotency)
        /// </summary>
        public Task<ComplianceDecision?> FindDuplicateDecisionAsync(
            string organizationId,
            OnboardingStep step,
            string policyVersion,
            List<string> evidenceReferenceIds)
        {
            lock (_lock)
            {
                // Look for a recent decision (within last hour) with matching parameters
                var oneHourAgo = DateTime.UtcNow.AddHours(-1);
                var sortedEvidenceIds = evidenceReferenceIds.OrderBy(id => id).ToList();

                var decision = _decisions
                    .Where(d => d.OrganizationId.Equals(organizationId, StringComparison.OrdinalIgnoreCase) &&
                               d.Step == step &&
                               d.PolicyVersion == policyVersion &&
                               d.DecisionTimestamp >= oneHourAgo &&
                               !d.IsSuperseded)
                    .FirstOrDefault(d =>
                    {
                        // Check if evidence reference IDs match
                        var decisionEvidenceIds = d.EvidenceReferences
                            .Select(e => e.ReferenceId)
                            .OrderBy(id => id)
                            .ToList();

                        return decisionEvidenceIds.SequenceEqual(sortedEvidenceIds);
                    });

                if (decision != null)
                {
                    _logger.LogInformation(
                        "Found duplicate decision: Id={DecisionId}, OrganizationId={OrganizationId}, Step={Step}",
                        decision.Id,
                        organizationId,
                        step
                    );
                }

                return Task.FromResult(decision);
            }
        }
    }
}
