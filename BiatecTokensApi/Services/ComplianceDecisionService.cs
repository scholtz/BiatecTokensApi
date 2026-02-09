using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.Compliance;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Service for managing compliance decisions
    /// </summary>
    /// <remarks>
    /// Implements business logic for creating, querying, and managing compliance decisions.
    /// Integrates with the policy evaluator to determine decision outcomes.
    /// </remarks>
    public class ComplianceDecisionService : IComplianceDecisionService
    {
        private readonly IComplianceDecisionRepository _repository;
        private readonly IPolicyEvaluator _policyEvaluator;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<ComplianceDecisionService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceDecisionService"/> class.
        /// </summary>
        /// <param name="repository">The compliance decision repository</param>
        /// <param name="policyEvaluator">The policy evaluator service</param>
        /// <param name="metricsService">The metrics service</param>
        /// <param name="logger">The logger instance</param>
        public ComplianceDecisionService(
            IComplianceDecisionRepository repository,
            IPolicyEvaluator policyEvaluator,
            IMetricsService metricsService,
            ILogger<ComplianceDecisionService> logger)
        {
            _repository = repository;
            _policyEvaluator = policyEvaluator;
            _metricsService = metricsService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new compliance decision with policy evaluation
        /// </summary>
        public async Task<ComplianceDecisionResponse> CreateDecisionAsync(CreateComplianceDecisionRequest request, string actorAddress)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation(
                    "Creating compliance decision: OrganizationId={OrganizationId}, Step={Step}, Actor={Actor}",
                    LoggingHelper.SanitizeLogInput(request.OrganizationId),
                    request.Step,
                    LoggingHelper.SanitizeLogInput(actorAddress)
                );

                // Validate request
                if (string.IsNullOrWhiteSpace(request.OrganizationId))
                {
                    return new ComplianceDecisionResponse
                    {
                        Success = false,
                        ErrorMessage = "OrganizationId is required"
                    };
                }

                // Get policy configuration
                var policyConfig = await _policyEvaluator.GetPolicyConfigurationAsync();
                var evidenceRefIds = request.EvidenceReferences.Select(e => e.ReferenceId).ToList();

                // Check for duplicate (idempotency)
                var duplicate = await _repository.FindDuplicateDecisionAsync(
                    request.OrganizationId,
                    request.Step,
                    policyConfig.Version,
                    evidenceRefIds
                );

                if (duplicate != null)
                {
                    _logger.LogInformation(
                        "Returning existing decision (idempotent): DecisionId={DecisionId}",
                        LoggingHelper.SanitizeLogInput(duplicate.Id)
                    );

                    return new ComplianceDecisionResponse
                    {
                        Success = true,
                        ErrorMessage = "Decision already exists (idempotent response)",
                        Decision = duplicate
                    };
                }

                // Build evaluation context
                var context = new PolicyEvaluationContext
                {
                    OrganizationId = request.OrganizationId,
                    OnboardingSessionId = request.OnboardingSessionId,
                    Step = request.Step,
                    Evidence = request.EvidenceReferences,
                    AdditionalData = request.EvaluationContext,
                    Initiator = actorAddress,
                    CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString()
                };

                // Evaluate policies
                var evaluationResult = await _policyEvaluator.EvaluateAsync(context);

                // Create decision
                var decision = new ComplianceDecision
                {
                    Id = Guid.NewGuid().ToString(),
                    OrganizationId = request.OrganizationId,
                    OnboardingSessionId = request.OnboardingSessionId,
                    Step = request.Step,
                    Outcome = evaluationResult.Outcome,
                    PolicyRuleIds = evaluationResult.RuleEvaluations.Select(r => r.RuleId).ToList(),
                    DecisionMaker = actorAddress,
                    DecisionTimestamp = DateTime.UtcNow,
                    EvidenceReferences = request.EvidenceReferences,
                    Reason = evaluationResult.Reason,
                    PolicyVersion = policyConfig.Version,
                    CorrelationId = context.CorrelationId,
                    RequiresReview = request.RequiresReview
                };

                // Set expiration if specified
                if (request.ExpirationDays.HasValue && request.ExpirationDays.Value > 0)
                {
                    decision.ExpiresAt = DateTime.UtcNow.AddDays(request.ExpirationDays.Value);
                }

                // Set review date if required
                if (request.RequiresReview && request.ReviewIntervalDays.HasValue && request.ReviewIntervalDays.Value > 0)
                {
                    decision.NextReviewDate = DateTime.UtcNow.AddDays(request.ReviewIntervalDays.Value);
                }

                // Save decision
                await _repository.CreateDecisionAsync(decision);

                // Emit metrics
                var durationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                await EmitDecisionMetricsAsync(decision, durationMs, "create");

                _logger.LogInformation(
                    "Compliance decision created: DecisionId={DecisionId}, Outcome={Outcome}, Duration={DurationMs}ms",
                    LoggingHelper.SanitizeLogInput(decision.Id),
                    decision.Outcome,
                    durationMs
                );

                return new ComplianceDecisionResponse
                {
                    Success = true,
                    ErrorMessage = null,
                    Decision = decision,
                    EvaluationResult = evaluationResult
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error creating compliance decision: OrganizationId={OrganizationId}, Step={Step}",
                    LoggingHelper.SanitizeLogInput(request.OrganizationId),
                    request.Step
                );

                return new ComplianceDecisionResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to create compliance decision: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets a compliance decision by ID
        /// </summary>
        public async Task<ComplianceDecision?> GetDecisionByIdAsync(string decisionId)
        {
            try
            {
                return await _repository.GetDecisionByIdAsync(decisionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving decision: DecisionId={DecisionId}", LoggingHelper.SanitizeLogInput(decisionId));
                return null;
            }
        }

        /// <summary>
        /// Queries compliance decisions with filtering and pagination
        /// </summary>
        public async Task<QueryComplianceDecisionsResponse> QueryDecisionsAsync(QueryComplianceDecisionsRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "Querying compliance decisions: OrganizationId={OrganizationId}, Step={Step}, Page={Page}",
                    LoggingHelper.SanitizeLogInput(request.OrganizationId),
                    request.Step?.ToString() ?? "All",
                    request.Page
                );

                var (decisions, totalCount) = await _repository.QueryDecisionsAsync(request);

                var pageSize = Math.Min(request.PageSize, 100);
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // Calculate summary statistics
                var summary = new DecisionSummary
                {
                    ApprovedCount = decisions.Count(d => d.Outcome == DecisionOutcome.Approved),
                    RejectedCount = decisions.Count(d => d.Outcome == DecisionOutcome.Rejected),
                    RequiresReviewCount = decisions.Count(d => d.Outcome == DecisionOutcome.RequiresManualReview),
                    PendingCount = decisions.Count(d => d.Outcome == DecisionOutcome.Pending),
                    ConditionalApprovalCount = decisions.Count(d => d.Outcome == DecisionOutcome.ConditionalApproval),
                    ExpiredCount = decisions.Count(d => d.Outcome == DecisionOutcome.Expired)
                };

                // Calculate average decision time if there are multiple decisions
                if (decisions.Count > 1)
                {
                    var times = decisions
                        .Where(d => d.DecisionTimestamp != DateTime.MinValue)
                        .OrderBy(d => d.DecisionTimestamp)
                        .ToList();
                    
                    if (times.Count > 1)
                    {
                        var totalHours = 0.0;
                        for (int i = 1; i < times.Count; i++)
                        {
                            totalHours += (times[i].DecisionTimestamp - times[i - 1].DecisionTimestamp).TotalHours;
                        }
                        summary.AverageDecisionTimeHours = totalHours / (times.Count - 1);
                    }
                }

                // Get common rejection reasons
                summary.CommonRejectionReasons = decisions
                    .Where(d => d.Outcome == DecisionOutcome.Rejected)
                    .GroupBy(d => d.Reason)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList();

                return new QueryComplianceDecisionsResponse
                {
                    Success = true,
                    Decisions = decisions,
                    TotalCount = totalCount,
                    Page = request.Page,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    Summary = summary
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying compliance decisions");
                
                return new QueryComplianceDecisionsResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to query compliance decisions: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets the most recent active decision for an organization and step
        /// </summary>
        public async Task<ComplianceDecision?> GetActiveDecisionAsync(string organizationId, OnboardingStep step)
        {
            try
            {
                return await _repository.GetActiveDecisionAsync(organizationId, step);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving active decision: OrganizationId={OrganizationId}, Step={Step}",
                    LoggingHelper.SanitizeLogInput(organizationId),
                    step
                );
                return null;
            }
        }

        /// <summary>
        /// Updates an existing decision by creating a new one that supersedes it
        /// </summary>
        public async Task<ComplianceDecisionResponse> UpdateDecisionAsync(
            string previousDecisionId,
            CreateComplianceDecisionRequest request,
            string actorAddress)
        {
            try
            {
                _logger.LogInformation(
                    "Updating compliance decision: PreviousDecisionId={PreviousDecisionId}, Actor={Actor}",
                    LoggingHelper.SanitizeLogInput(previousDecisionId),
                    LoggingHelper.SanitizeLogInput(actorAddress)
                );

                // Verify previous decision exists
                var previousDecision = await _repository.GetDecisionByIdAsync(previousDecisionId);
                if (previousDecision == null)
                {
                    return new ComplianceDecisionResponse
                    {
                        Success = false,
                        ErrorMessage = $"Previous decision not found: {previousDecisionId}"
                    };
                }

                // Create new decision
                var response = await CreateDecisionAsync(request, actorAddress);
                
                if (response.Success && response.Decision != null)
                {
                    // Link to previous decision
                    response.Decision.PreviousDecisionId = previousDecisionId;

                    // Mark previous decision as superseded
                    await _repository.SupresedeDecisionAsync(previousDecisionId, response.Decision.Id);

                    _logger.LogInformation(
                        "Decision updated: NewDecisionId={NewDecisionId}, PreviousDecisionId={PreviousDecisionId}",
                        LoggingHelper.SanitizeLogInput(response.Decision.Id),
                        LoggingHelper.SanitizeLogInput(previousDecisionId)
                    );
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error updating compliance decision: PreviousDecisionId={PreviousDecisionId}",
                    LoggingHelper.SanitizeLogInput(previousDecisionId)
                );

                return new ComplianceDecisionResponse
                {
                    Success = false,
                    ErrorMessage = $"Failed to update compliance decision: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets decisions requiring review
        /// </summary>
        public async Task<List<ComplianceDecision>> GetDecisionsRequiringReviewAsync(DateTime? beforeDate = null)
        {
            try
            {
                return await _repository.GetDecisionsRequiringReviewAsync(beforeDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving decisions requiring review");
                return new List<ComplianceDecision>();
            }
        }

        /// <summary>
        /// Gets expired decisions
        /// </summary>
        public async Task<List<ComplianceDecision>> GetExpiredDecisionsAsync()
        {
            try
            {
                return await _repository.GetExpiredDecisionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expired decisions");
                return new List<ComplianceDecision>();
            }
        }

        /// <summary>
        /// Emits metrics for decision operations
        /// </summary>
        private Task EmitDecisionMetricsAsync(ComplianceDecision decision, double durationMs, string operation)
        {
            try
            {
                // Record decision creation as a counter
                _metricsService.IncrementCounter($"compliance_decision_{operation}");
                _metricsService.IncrementCounter($"compliance_decision_{operation}_{decision.Outcome.ToString().ToLower()}");
                
                // Record decision duration
                _metricsService.RecordHistogram($"compliance_decision_{operation}_duration_ms", durationMs);
                
                // Record by step
                _metricsService.IncrementCounter($"compliance_decision_step_{decision.Step.ToString().ToLower()}");
            }
            catch (Exception ex)
            {
                // Don't fail the operation if metrics fail
                _logger.LogWarning(ex, "Failed to emit decision metrics");
            }
            
            return Task.CompletedTask;
        }
    }
}
