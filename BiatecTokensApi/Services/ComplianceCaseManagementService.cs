using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of compliance case management.
    /// Uses <see cref="ConcurrentDictionary{TKey,TValue}"/> for thread-safe storage.
    /// Enforces fail-closed readiness evaluation, valid state-transition matrix,
    /// idempotent case creation, and automatic evidence-freshness transitions.
    /// </summary>
    public class ComplianceCaseManagementService : IComplianceCaseManagementService
    {
        private readonly ILogger<ComplianceCaseManagementService> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _defaultEvidenceValidity;

        // Primary store: caseId → case
        private readonly ConcurrentDictionary<string, ComplianceCase> _cases = new();

        // Idempotency index: "issuerId|subjectId|type" → caseId (only for active cases)
        private readonly ConcurrentDictionary<string, string> _idempotencyIndex = new();

        // ── Valid state-transition matrix ──────────────────────────────────────
        private static readonly IReadOnlyDictionary<ComplianceCaseState, IReadOnlySet<ComplianceCaseState>> _validTransitions =
            new Dictionary<ComplianceCaseState, IReadOnlySet<ComplianceCaseState>>
            {
                [ComplianceCaseState.Intake]          = new HashSet<ComplianceCaseState> { ComplianceCaseState.EvidencePending, ComplianceCaseState.Blocked },
                [ComplianceCaseState.EvidencePending] = new HashSet<ComplianceCaseState> { ComplianceCaseState.UnderReview, ComplianceCaseState.Stale, ComplianceCaseState.Blocked },
                [ComplianceCaseState.UnderReview]     = new HashSet<ComplianceCaseState> { ComplianceCaseState.Approved, ComplianceCaseState.Rejected, ComplianceCaseState.Escalated, ComplianceCaseState.Remediating, ComplianceCaseState.Blocked },
                [ComplianceCaseState.Escalated]       = new HashSet<ComplianceCaseState> { ComplianceCaseState.UnderReview, ComplianceCaseState.Rejected, ComplianceCaseState.Blocked },
                [ComplianceCaseState.Remediating]     = new HashSet<ComplianceCaseState> { ComplianceCaseState.UnderReview, ComplianceCaseState.Approved, ComplianceCaseState.Rejected, ComplianceCaseState.Blocked },
                [ComplianceCaseState.Stale]           = new HashSet<ComplianceCaseState> { ComplianceCaseState.EvidencePending, ComplianceCaseState.Rejected, ComplianceCaseState.Blocked },
                [ComplianceCaseState.Blocked]         = new HashSet<ComplianceCaseState> { ComplianceCaseState.Intake },
                // Terminal states — no outbound transitions
                [ComplianceCaseState.Approved]        = new HashSet<ComplianceCaseState>(),
                [ComplianceCaseState.Rejected]        = new HashSet<ComplianceCaseState>(),
            };

        private static readonly IReadOnlySet<ComplianceCaseState> _terminalStates =
            new HashSet<ComplianceCaseState> { ComplianceCaseState.Approved, ComplianceCaseState.Rejected };

        /// <summary>
        /// Initializes the service.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="timeProvider">Time provider (inject a fake for tests).</param>
        /// <param name="defaultEvidenceValidity">How long evidence is considered fresh (default 90 days).</param>
        public ComplianceCaseManagementService(
            ILogger<ComplianceCaseManagementService> logger,
            TimeProvider? timeProvider = null,
            TimeSpan? defaultEvidenceValidity = null)
        {
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _defaultEvidenceValidity = defaultEvidenceValidity ?? TimeSpan.FromDays(90);
        }

        // ── CreateCaseAsync ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<CreateComplianceCaseResponse> CreateCaseAsync(CreateComplianceCaseRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(request.IssuerId))
                return Task.FromResult(Fail<CreateComplianceCaseResponse>("IssuerId is required", ErrorCodes.MISSING_REQUIRED_FIELD));
            if (string.IsNullOrWhiteSpace(request.SubjectId))
                return Task.FromResult(Fail<CreateComplianceCaseResponse>("SubjectId is required", ErrorCodes.MISSING_REQUIRED_FIELD));

            var idempotencyKey = $"{request.IssuerId}|{request.SubjectId}|{request.Type}";

            // Return existing active case if one exists (idempotent)
            if (_idempotencyIndex.TryGetValue(idempotencyKey, out var existingId) &&
                _cases.TryGetValue(existingId, out var existingCase))
            {
                _logger.LogInformation(
                    "CreateCase idempotent hit. CaseId={CaseId} IssuerId={IssuerId} Actor={Actor}",
                    LoggingHelper.SanitizeLogInput(existingId),
                    LoggingHelper.SanitizeLogInput(request.IssuerId),
                    LoggingHelper.SanitizeLogInput(actorId));

                return Task.FromResult(new CreateComplianceCaseResponse
                {
                    Success = true,
                    Case = existingCase,
                    WasIdempotent = true
                });
            }

            var now = _timeProvider.GetUtcNow();
            var caseId = Guid.NewGuid().ToString("N");

            var newCase = new ComplianceCase
            {
                CaseId = caseId,
                IssuerId = request.IssuerId,
                SubjectId = request.SubjectId,
                Type = request.Type,
                State = ComplianceCaseState.Intake,
                Priority = request.Priority,
                CreatedBy = actorId,
                CreatedAt = now,
                UpdatedAt = now,
                Jurisdiction = request.Jurisdiction,
                ExternalReference = request.ExternalReference,
                CorrelationId = request.CorrelationId,
                LinkedDecisionIds = request.LinkedDecisionIds ?? new List<string>(),
                EvidenceExpiresAt = now.Add(_defaultEvidenceValidity),
            };

            newCase.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.CaseCreated, actorId, now,
                description: $"Case created for subject {request.SubjectId} ({request.Type})",
                correlationId: request.CorrelationId));

            _cases[caseId] = newCase;
            _idempotencyIndex[idempotencyKey] = caseId;

            _logger.LogInformation(
                "CreateCase success. CaseId={CaseId} IssuerId={IssuerId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(request.IssuerId),
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new CreateComplianceCaseResponse
            {
                Success = true,
                Case = newCase,
                WasIdempotent = false
            });
        }

        // ── GetCaseAsync ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetComplianceCaseResponse> GetCaseAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<GetComplianceCaseResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            CheckAndApplyEvidenceFreshness(c);

            return Task.FromResult(new GetComplianceCaseResponse { Success = true, Case = c });
        }

        // ── ListCasesAsync ─────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ListComplianceCasesResponse> ListCasesAsync(ListComplianceCasesRequest request, string actorId)
        {
            var pageSize = Math.Min(Math.Max(request.PageSize, 1), 100);
            var page = Math.Max(request.Page, 1);

            IEnumerable<ComplianceCase> query = _cases.Values;

            if (request.State.HasValue)
                query = query.Where(c => c.State == request.State.Value);
            if (request.Priority.HasValue)
                query = query.Where(c => c.Priority == request.Priority.Value);
            if (!string.IsNullOrWhiteSpace(request.AssignedReviewerId))
                query = query.Where(c => c.AssignedReviewerId == request.AssignedReviewerId);
            if (!string.IsNullOrWhiteSpace(request.IssuerId))
                query = query.Where(c => c.IssuerId == request.IssuerId);
            if (!string.IsNullOrWhiteSpace(request.Jurisdiction))
                query = query.Where(c => c.Jurisdiction == request.Jurisdiction);
            if (request.HasStaleEvidence.HasValue)
                query = query.Where(c => c.IsEvidenceStale == request.HasStaleEvidence.Value);
            if (request.Type.HasValue)
                query = query.Where(c => c.Type == request.Type.Value);

            var ordered = query.OrderByDescending(c => c.UpdatedAt).ToList();
            var total = ordered.Count;
            var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Task.FromResult(new ListComplianceCasesResponse
            {
                Success = true,
                Cases = paged,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            });
        }

        // ── UpdateCaseAsync ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<UpdateComplianceCaseResponse> UpdateCaseAsync(string caseId, UpdateComplianceCaseRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<UpdateComplianceCaseResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            var now = _timeProvider.GetUtcNow();

            if (request.Priority.HasValue)
                c.Priority = request.Priority.Value;
            if (request.AssignedReviewerId != null)
            {
                c.AssignedReviewerId = request.AssignedReviewerId;
                c.Timeline.Add(BuildTimelineEntry(caseId, CaseTimelineEventType.ReviewerAssigned, actorId, now,
                    description: $"Reviewer assigned: {request.AssignedReviewerId}"));
            }
            if (request.Jurisdiction != null)
                c.Jurisdiction = request.Jurisdiction;
            if (request.ExternalReference != null)
                c.ExternalReference = request.ExternalReference;
            if (request.AdditionalLinkedDecisionIds != null)
                c.LinkedDecisionIds.AddRange(request.AdditionalLinkedDecisionIds);

            c.UpdatedAt = now;

            return Task.FromResult(new UpdateComplianceCaseResponse { Success = true, Case = c });
        }

        // ── TransitionStateAsync ───────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<UpdateComplianceCaseResponse> TransitionStateAsync(string caseId, TransitionCaseStateRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<UpdateComplianceCaseResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            if (_terminalStates.Contains(c.State))
                return Task.FromResult(Fail<UpdateComplianceCaseResponse>(
                    $"Case is in terminal state {c.State} and cannot be transitioned",
                    "INVALID_STATE_TRANSITION"));

            if (!_validTransitions.TryGetValue(c.State, out var allowed) || !allowed.Contains(request.NewState))
                return Task.FromResult(Fail<UpdateComplianceCaseResponse>(
                    $"Transition from {c.State} to {request.NewState} is not allowed",
                    "INVALID_STATE_TRANSITION"));

            var now = _timeProvider.GetUtcNow();
            var fromState = c.State;
            c.State = request.NewState;
            c.UpdatedAt = now;

            if (_terminalStates.Contains(request.NewState))
            {
                c.ClosedAt = now;
                c.ClosureReason = request.Reason;
                // Remove from idempotency index so new cases can be created for same subject
                var idempotencyKey = $"{c.IssuerId}|{c.SubjectId}|{c.Type}";
                _idempotencyIndex.TryRemove(idempotencyKey, out _);
            }

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.StateTransition, actorId, now,
                description: request.Reason ?? $"State transitioned from {fromState} to {request.NewState}",
                fromState: fromState, toState: request.NewState));

            _logger.LogInformation(
                "TransitionState. CaseId={CaseId} From={From} To={To} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                fromState.ToString(),
                request.NewState.ToString(),
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new UpdateComplianceCaseResponse { Success = true, Case = c });
        }

        // ── AddEvidenceAsync ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetComplianceCaseResponse> AddEvidenceAsync(string caseId, AddEvidenceRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<GetComplianceCaseResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            if (string.IsNullOrWhiteSpace(request.EvidenceType))
                return Task.FromResult(Fail<GetComplianceCaseResponse>("EvidenceType is required", ErrorCodes.MISSING_REQUIRED_FIELD));

            var now = _timeProvider.GetUtcNow();
            var evidenceId = Guid.NewGuid().ToString("N");

            var evidence = new CaseEvidenceSummary
            {
                EvidenceId = evidenceId,
                CaseId = caseId,
                EvidenceType = request.EvidenceType,
                Status = request.Status,
                ProviderName = request.ProviderName,
                ProviderReference = request.ProviderReference,
                CapturedAt = request.CapturedAt,
                ExpiresAt = request.ExpiresAt,
                IsExpired = request.ExpiresAt.HasValue && request.ExpiresAt.Value < now,
                Summary = request.Summary,
                NormalizedAttributes = request.NormalizedAttributes ?? new Dictionary<string, string>(),
                IsBlockingReadiness = request.IsBlockingReadiness,
                BlockingReason = request.BlockingReason,
                AddedBy = actorId,
                AddedAt = now
            };

            c.EvidenceSummaries.Add(evidence);
            c.UpdatedAt = now;

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.EvidenceAdded, actorId, now,
                description: $"Evidence added: {request.EvidenceType} (status={request.Status})",
                metadata: new Dictionary<string, string> { ["evidenceId"] = evidenceId, ["evidenceType"] = request.EvidenceType }));

            return Task.FromResult(new GetComplianceCaseResponse { Success = true, Case = c });
        }

        // ── AddRemediationTaskAsync ────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetComplianceCaseResponse> AddRemediationTaskAsync(string caseId, AddRemediationTaskRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<GetComplianceCaseResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            if (string.IsNullOrWhiteSpace(request.Title))
                return Task.FromResult(Fail<GetComplianceCaseResponse>("Title is required", ErrorCodes.MISSING_REQUIRED_FIELD));

            var now = _timeProvider.GetUtcNow();
            var taskId = Guid.NewGuid().ToString("N");

            var task = new RemediationTask
            {
                TaskId = taskId,
                CaseId = caseId,
                Title = request.Title,
                Description = request.Description,
                OwnerId = request.OwnerId,
                DueAt = request.DueAt,
                Status = RemediationTaskStatus.Open,
                IsBlockingCase = request.IsBlockingCase,
                BlockerSeverity = request.BlockerSeverity,
                CreatedBy = actorId,
                CreatedAt = now
            };

            c.RemediationTasks.Add(task);
            c.UpdatedAt = now;

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.RemediationTaskAdded, actorId, now,
                description: $"Remediation task added: {request.Title}",
                metadata: new Dictionary<string, string> { ["taskId"] = taskId, ["isBlockingCase"] = request.IsBlockingCase.ToString() }));

            return Task.FromResult(new GetComplianceCaseResponse { Success = true, Case = c });
        }

        // ── ResolveRemediationTaskAsync ────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetComplianceCaseResponse> ResolveRemediationTaskAsync(string caseId, string taskId, ResolveRemediationTaskRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<GetComplianceCaseResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            var task = c.RemediationTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (task == null)
                return Task.FromResult(Fail<GetComplianceCaseResponse>($"Remediation task {taskId} not found on case {caseId}", ErrorCodes.NOT_FOUND));

            var now = _timeProvider.GetUtcNow();
            task.Status = request.Status;
            task.ResolutionNotes = request.ResolutionNotes;
            task.ResolvedAt = now;
            c.UpdatedAt = now;

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.RemediationTaskResolved, actorId, now,
                description: $"Remediation task {request.Status}: {task.Title}",
                metadata: new Dictionary<string, string> { ["taskId"] = taskId, ["status"] = request.Status.ToString() }));

            return Task.FromResult(new GetComplianceCaseResponse { Success = true, Case = c });
        }

        // ── AddEscalationAsync ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetComplianceCaseResponse> AddEscalationAsync(string caseId, AddEscalationRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<GetComplianceCaseResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            if (string.IsNullOrWhiteSpace(request.Description))
                return Task.FromResult(Fail<GetComplianceCaseResponse>("Description is required", ErrorCodes.MISSING_REQUIRED_FIELD));

            var now = _timeProvider.GetUtcNow();
            var escalationId = Guid.NewGuid().ToString("N");

            var escalation = new CaseEscalation
            {
                EscalationId = escalationId,
                CaseId = caseId,
                Type = request.Type,
                Status = EscalationStatus.Open,
                Description = request.Description,
                ScreeningSource = request.ScreeningSource,
                MatchedCategories = request.MatchedCategories ?? new List<string>(),
                ConfidenceScore = request.ConfidenceScore,
                RequiresManualReview = request.RequiresManualReview,
                RaisedBy = actorId,
                RaisedAt = now
            };

            c.Escalations.Add(escalation);
            c.UpdatedAt = now;

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.EscalationRaised, actorId, now,
                description: $"Escalation raised: {request.Type} — {request.Description}",
                metadata: new Dictionary<string, string> { ["escalationId"] = escalationId, ["type"] = request.Type.ToString() }));

            _logger.LogInformation(
                "Escalation raised. CaseId={CaseId} Type={Type} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                request.Type.ToString(),
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new GetComplianceCaseResponse { Success = true, Case = c });
        }

        // ── ResolveEscalationAsync ─────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetComplianceCaseResponse> ResolveEscalationAsync(string caseId, string escalationId, ResolveEscalationRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<GetComplianceCaseResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            var escalation = c.Escalations.FirstOrDefault(e => e.EscalationId == escalationId);
            if (escalation == null)
                return Task.FromResult(Fail<GetComplianceCaseResponse>($"Escalation {escalationId} not found on case {caseId}", ErrorCodes.NOT_FOUND));

            var now = _timeProvider.GetUtcNow();
            escalation.Status = EscalationStatus.Resolved;
            escalation.ReviewedBy = actorId;
            escalation.ReviewedAt = now;
            escalation.ResolutionNotes = request.ResolutionNotes;
            c.UpdatedAt = now;

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.EscalationResolved, actorId, now,
                description: $"Escalation resolved: {escalation.Type}",
                metadata: new Dictionary<string, string> { ["escalationId"] = escalationId }));

            return Task.FromResult(new GetComplianceCaseResponse { Success = true, Case = c });
        }

        // ── GetTimelineAsync ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<CaseTimelineResponse> GetTimelineAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new CaseTimelineResponse
                {
                    Success = false,
                    CaseId = caseId,
                    ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            return Task.FromResult(new CaseTimelineResponse
            {
                Success = true,
                CaseId = caseId,
                Entries = c.Timeline.OrderBy(e => e.OccurredAt).ToList()
            });
        }

        // ── GetReadinessSummaryAsync ───────────────────────────────────────────

        /// <inheritdoc/>
        public Task<CaseReadinessSummaryResponse> GetReadinessSummaryAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new CaseReadinessSummaryResponse
                {
                    Success = false,
                    ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            CheckAndApplyEvidenceFreshness(c);

            var now = _timeProvider.GetUtcNow();
            var summary = EvaluateReadiness(c, now);

            return Task.FromResult(new CaseReadinessSummaryResponse { Success = true, Summary = summary });
        }

        // ── RunEvidenceFreshnessCheckAsync ─────────────────────────────────────

        /// <inheritdoc/>
        public Task<int> RunEvidenceFreshnessCheckAsync()
        {
            int transitioned = 0;
            foreach (var c in _cases.Values)
            {
                if (CheckAndApplyEvidenceFreshness(c))
                    transitioned++;
            }

            _logger.LogInformation(
                "EvidenceFreshnessCheck complete. TransitionedToStale={Count}",
                transitioned);

            return Task.FromResult(transitioned);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether evidence has expired. If it has and the case is not in a terminal state,
        /// transitions to <see cref="ComplianceCaseState.Stale"/> and appends a timeline entry.
        /// Returns true when a transition was applied.
        /// </summary>
        private bool CheckAndApplyEvidenceFreshness(ComplianceCase c)
        {
            if (_terminalStates.Contains(c.State))
                return false;
            if (c.State == ComplianceCaseState.Stale)
                return false;
            if (!c.EvidenceExpiresAt.HasValue)
                return false;

            var now = _timeProvider.GetUtcNow();
            if (c.EvidenceExpiresAt.Value >= now)
                return false;

            c.IsEvidenceStale = true;

            // Only transition to Stale if allowed from the current state
            if (_validTransitions.TryGetValue(c.State, out var allowed) && allowed.Contains(ComplianceCaseState.Stale))
            {
                var from = c.State;
                c.State = ComplianceCaseState.Stale;
                c.UpdatedAt = now;

                c.Timeline.Add(BuildTimelineEntry(
                    c.CaseId, CaseTimelineEventType.EvidenceStale, "system", now,
                    description: $"Evidence expired at {c.EvidenceExpiresAt:O}; case transitioned to Stale",
                    fromState: from, toState: ComplianceCaseState.Stale));

                _logger.LogWarning(
                    "EvidenceStale. CaseId={CaseId} ExpiredAt={Expiry}",
                    LoggingHelper.SanitizeLogInput(c.CaseId),
                    c.EvidenceExpiresAt?.ToString("O"));

                return true;
            }

            // Evidence is stale but can't transition (e.g., Escalated state) — just flag it
            c.UpdatedAt = now;
            c.Timeline.Add(BuildTimelineEntry(
                c.CaseId, CaseTimelineEventType.EvidenceStale, "system", now,
                description: $"Evidence expired at {c.EvidenceExpiresAt:O}; case state remains {c.State}"));

            return false;
        }

        /// <summary>Evaluates readiness using fail-closed semantics.</summary>
        private CaseReadinessSummary EvaluateReadiness(ComplianceCase c, DateTimeOffset now)
        {
            var blocking = new List<string>();
            var warnings = new List<string>();
            var missing = new List<string>();
            bool failedClosed = false;
            int criticalEscalations = 0;

            // Fail-closed: no evidence captured at all
            if (!c.EvidenceSummaries.Any(e => e.CapturedAt.HasValue))
            {
                failedClosed = true;
                blocking.Add("No evidence has been captured");
            }

            // Blocking evidence
            foreach (var ev in c.EvidenceSummaries.Where(e => e.IsBlockingReadiness))
                blocking.Add(ev.BlockingReason ?? $"Evidence '{ev.EvidenceType}' is blocking readiness");

            // Open blocking remediation tasks
            foreach (var rt in c.RemediationTasks.Where(t => t.IsBlockingCase && t.Status == RemediationTaskStatus.Open))
                blocking.Add($"Open blocking remediation task: {rt.Title}");

            int openTaskCount = c.RemediationTasks.Count(t => t.Status == RemediationTaskStatus.Open);

            // Open escalations requiring manual review
            foreach (var esc in c.Escalations.Where(e => e.Status == EscalationStatus.Open && e.RequiresManualReview))
            {
                blocking.Add($"Open escalation requiring manual review: {esc.Type}");
                criticalEscalations++;
            }

            // Stale evidence warning
            if (c.IsEvidenceStale)
                warnings.Add("Case evidence is stale and may need to be refreshed");

            bool isReady = blocking.Count == 0 && !failedClosed;

            string? explanation;
            if (failedClosed)
                explanation = "Case is fail-closed: no evidence has been captured yet.";
            else if (blocking.Count > 0)
                explanation = $"Case has {blocking.Count} blocking issue(s) that must be resolved before approval.";
            else if (warnings.Count > 0)
                explanation = "Case is ready but has non-blocking warnings.";
            else
                explanation = "Case is ready to proceed.";

            return new CaseReadinessSummary
            {
                CaseId = c.CaseId,
                IsReady = isReady,
                FailedClosed = failedClosed,
                BlockingIssues = blocking,
                Warnings = warnings,
                MissingEvidence = missing,
                OpenRemediationTasks = openTaskCount,
                CriticalEscalations = criticalEscalations,
                HasStaleEvidence = c.IsEvidenceStale,
                ReadinessExplanation = explanation,
                EvaluatedAt = now
            };
        }

        private static CaseTimelineEntry BuildTimelineEntry(
            string caseId,
            CaseTimelineEventType eventType,
            string actorId,
            DateTimeOffset occurredAt,
            string description = "",
            string? correlationId = null,
            ComplianceCaseState? fromState = null,
            ComplianceCaseState? toState = null,
            Dictionary<string, string>? metadata = null)
        {
            return new CaseTimelineEntry
            {
                EntryId = Guid.NewGuid().ToString("N"),
                CaseId = caseId,
                EventType = eventType,
                OccurredAt = occurredAt,
                ActorId = actorId,
                Description = description,
                CorrelationId = correlationId,
                FromState = fromState,
                ToState = toState,
                Metadata = metadata ?? new Dictionary<string, string>()
            };
        }

        private static T Fail<T>(string message, string errorCode) where T : class, new()
        {
            // Use reflection-free approach with known response types
            if (typeof(T) == typeof(CreateComplianceCaseResponse))
                return (new CreateComplianceCaseResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(GetComplianceCaseResponse))
                return (new GetComplianceCaseResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(UpdateComplianceCaseResponse))
                return (new UpdateComplianceCaseResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(ListComplianceCasesResponse))
                return (new ListComplianceCasesResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;

            throw new InvalidOperationException($"Unsupported response type {typeof(T).Name}");
        }
    }
}
