using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of compliance case management.
    /// Uses <see cref="ConcurrentDictionary{TKey,TValue}"/> for thread-safe storage.
    /// Enforces fail-closed readiness evaluation, valid state-transition matrix,
    /// idempotent case creation, and automatic evidence-freshness transitions.
    /// Emits compliance lifecycle events via <see cref="IWebhookService"/> when injected,
    /// and mirrors case state to <see cref="IComplianceCaseRepository"/> for durable persistence.
    /// </summary>
    public class ComplianceCaseManagementService : IComplianceCaseManagementService
    {
        private readonly ILogger<ComplianceCaseManagementService> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _defaultEvidenceValidity;
        private readonly IWebhookService? _webhookService;
        private readonly IComplianceCaseRepository? _repository;

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
        /// <param name="webhookService">Optional webhook service for lifecycle event emission.</param>
        /// <param name="repository">Optional repository for durable case persistence.</param>
        public ComplianceCaseManagementService(
            ILogger<ComplianceCaseManagementService> logger,
            TimeProvider? timeProvider = null,
            TimeSpan? defaultEvidenceValidity = null,
            IWebhookService? webhookService = null,
            IComplianceCaseRepository? repository = null)
        {
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _defaultEvidenceValidity = defaultEvidenceValidity ?? TimeSpan.FromDays(90);
            _webhookService = webhookService;
            _repository = repository;
        }

        // ── CreateCaseAsync ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<CreateComplianceCaseResponse> CreateCaseAsync(CreateComplianceCaseRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(request.IssuerId))
                return Fail<CreateComplianceCaseResponse>("IssuerId is required", ErrorCodes.MISSING_REQUIRED_FIELD);
            if (string.IsNullOrWhiteSpace(request.SubjectId))
                return Fail<CreateComplianceCaseResponse>("SubjectId is required", ErrorCodes.MISSING_REQUIRED_FIELD);

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

                return new CreateComplianceCaseResponse
                {
                    Success = true,
                    Case = existingCase,
                    WasIdempotent = true
                };
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

            // Persist to durable repository when available
            if (_repository != null)
            {
                await _repository.SaveCaseAsync(newCase);
                await _repository.AddOrGetIdempotencyKeyAsync(idempotencyKey, caseId);
            }

            _logger.LogInformation(
                "CreateCase success. CaseId={CaseId} IssuerId={IssuerId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(request.IssuerId),
                LoggingHelper.SanitizeLogInput(actorId));

            // Emit lifecycle webhook event (fire-and-forget)
            EmitEventFireAndForget(WebhookEventType.ComplianceCaseCreated, actorId, caseId,
                new { caseId, issuerId = request.IssuerId, subjectId = request.SubjectId, type = request.Type.ToString(), state = ComplianceCaseState.Intake.ToString() });

            return new CreateComplianceCaseResponse
            {
                Success = true,
                Case = newCase,
                WasIdempotent = false
            };
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
            bool assignmentChanged = false;

            if (request.Priority.HasValue)
                c.Priority = request.Priority.Value;
            if (request.AssignedReviewerId != null)
            {
                c.AssignedReviewerId = request.AssignedReviewerId;
                assignmentChanged = true;
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

            _ = PersistCaseAsync(c);

            if (assignmentChanged)
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseAssignmentChanged, actorId, caseId,
                    new { caseId, assignedReviewerId = request.AssignedReviewerId });

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

            _ = PersistCaseAsync(c);

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseStateTransitioned, actorId, caseId,
                new { caseId, fromState = fromState.ToString(), toState = request.NewState.ToString(), reason = request.Reason });

            // Emit ApprovalReady when case transitions to Approved state
            if (request.NewState == ComplianceCaseState.Approved)
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseApprovalReady, actorId, caseId,
                    new { caseId, issuerId = c.IssuerId, subjectId = c.SubjectId });

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

            _ = PersistCaseAsync(c);

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

            _ = PersistCaseAsync(c);

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseRemediationTaskAdded, actorId, caseId,
                new { caseId, taskId, title = request.Title, isBlockingCase = request.IsBlockingCase });

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

            _ = PersistCaseAsync(c);

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseRemediationTaskResolved, actorId, caseId,
                new { caseId, taskId, status = request.Status.ToString() });

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

            _ = PersistCaseAsync(c);

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseEscalationRaised, actorId, caseId,
                new { caseId, escalationId, type = request.Type.ToString() });

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

            _ = PersistCaseAsync(c);

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseEscalationResolved, actorId, caseId,
                new { caseId, escalationId, type = escalation.Type.ToString() });

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

        // ── SetMonitoringScheduleAsync ──────────────────────────────────────────

        /// <inheritdoc/>
        public Task<SetMonitoringScheduleResponse> SetMonitoringScheduleAsync(
            string caseId, SetMonitoringScheduleRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<SetMonitoringScheduleResponse>(
                    $"Case '{caseId}' not found", ErrorCodes.NOT_FOUND));

            if (request.Frequency == MonitoringFrequency.Custom &&
                (request.CustomIntervalDays == null || request.CustomIntervalDays <= 0))
                return Task.FromResult(Fail<SetMonitoringScheduleResponse>(
                    "CustomIntervalDays must be a positive integer when Frequency is Custom",
                    ErrorCodes.MISSING_REQUIRED_FIELD));

            int intervalDays = request.Frequency switch
            {
                MonitoringFrequency.Monthly    => 30,
                MonitoringFrequency.Quarterly  => 90,
                MonitoringFrequency.SemiAnnual => 180,
                MonitoringFrequency.Annual     => 365,
                MonitoringFrequency.Custom     => request.CustomIntervalDays!.Value,
                _                              => 365
            };

            var now      = _timeProvider.GetUtcNow();
            var schedId  = Guid.NewGuid().ToString("N");
            var schedule = new MonitoringSchedule
            {
                ScheduleId      = schedId,
                CaseId          = caseId,
                Frequency        = request.Frequency,
                IntervalDays     = intervalDays,
                NextReviewDueAt  = now.AddDays(intervalDays),
                CreatedBy        = actorId,
                CreatedAt        = now,
                Notes            = request.Notes,
                IsActive         = true
            };

            c.MonitoringSchedule = schedule;
            c.UpdatedAt = now;

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.MonitoringScheduleSet, actorId, now,
                description: $"Monitoring schedule set: {request.Frequency} (every {intervalDays} days). Next review: {schedule.NextReviewDueAt:yyyy-MM-dd}"));

            _logger.LogInformation(
                "MonitoringScheduleSet. CaseId={CaseId} Frequency={Freq} IntervalDays={Days} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                request.Frequency,
                intervalDays,
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new SetMonitoringScheduleResponse
            {
                Success  = true,
                Schedule = schedule,
                Case     = c
            });
        }

        // ── RecordMonitoringReviewAsync ─────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<RecordMonitoringReviewResponse> RecordMonitoringReviewAsync(
            string caseId, RecordMonitoringReviewRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Fail<RecordMonitoringReviewResponse>(
                    $"Case '{caseId}' not found", ErrorCodes.NOT_FOUND);

            if (string.IsNullOrWhiteSpace(request.ReviewNotes))
                return Fail<RecordMonitoringReviewResponse>(
                    "ReviewNotes is required", ErrorCodes.MISSING_REQUIRED_FIELD);

            if (c.MonitoringSchedule == null)
                return Fail<RecordMonitoringReviewResponse>(
                    "No monitoring schedule has been set for this case. Call SetMonitoringSchedule first.",
                    ErrorCodes.PRECONDITION_FAILED);

            var now = _timeProvider.GetUtcNow();
            var reviewId = Guid.NewGuid().ToString("N");

            var review = new MonitoringReview
            {
                ReviewId   = reviewId,
                CaseId     = caseId,
                Outcome    = request.Outcome,
                ReviewNotes= request.ReviewNotes,
                ReviewedBy = actorId,
                ReviewedAt = now,
                Attributes = request.Attributes ?? new Dictionary<string, string>()
            };

            // Update schedule timestamps
            var schedule = c.MonitoringSchedule;
            schedule.LastReviewAt    = now;
            schedule.NextReviewDueAt = now.AddDays(schedule.IntervalDays);
            schedule.IsOverdue       = false;

            c.MonitoringReviews.Add(review);
            c.UpdatedAt = now;

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.MonitoringReviewRecorded, actorId, now,
                description: $"Monitoring review recorded. Outcome: {request.Outcome}. Notes: {request.ReviewNotes}",
                metadata: new Dictionary<string, string>
                {
                    ["reviewId"]  = reviewId,
                    ["outcome"]   = request.Outcome.ToString()
                }));

            _logger.LogInformation(
                "MonitoringReviewRecorded. CaseId={CaseId} ReviewId={ReviewId} Outcome={Outcome} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(reviewId),
                request.Outcome,
                LoggingHelper.SanitizeLogInput(actorId));

            if (_repository != null)
                await _repository.SaveCaseAsync(c);

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseMonitoringReviewRecorded, actorId, caseId,
                new { caseId, reviewId, outcome = request.Outcome.ToString() });

            // Optionally create a follow-up case
            ComplianceCase? followUpCase = null;
            if (request.Outcome == MonitoringReviewOutcome.EscalationRequired && request.CreateFollowUpCase)
            {
                var followUpReq = new CreateComplianceCaseRequest
                {
                    IssuerId    = c.IssuerId,
                    SubjectId   = c.SubjectId,
                    Type        = CaseType.OngoingMonitoring,
                    Priority    = CasePriority.High,
                    Jurisdiction= c.Jurisdiction,
                    ExternalReference = $"follow-up:{caseId}:{reviewId}",
                    CorrelationId = c.CorrelationId
                };

                var followUpResp = await CreateCaseAsync(followUpReq, actorId);
                if (followUpResp.Success)
                {
                    followUpCase = followUpResp.Case;
                    review.FollowUpCaseCreated = true;
                    review.FollowUpCaseId      = followUpCase!.CaseId;

                    c.Timeline.Add(BuildTimelineEntry(
                        caseId, CaseTimelineEventType.MonitoringFollowUpCreated, actorId, now,
                        description: $"Follow-up monitoring case created: {followUpCase.CaseId}",
                        metadata: new Dictionary<string, string>
                        {
                            ["followUpCaseId"] = followUpCase.CaseId,
                            ["reviewId"]        = reviewId
                        }));

                    _logger.LogInformation(
                        "MonitoringFollowUpCaseCreated. SourceCaseId={Source} FollowUpCaseId={FollowUp} Actor={Actor}",
                        LoggingHelper.SanitizeLogInput(caseId),
                        LoggingHelper.SanitizeLogInput(followUpCase.CaseId),
                        LoggingHelper.SanitizeLogInput(actorId));

                    EmitEventFireAndForget(WebhookEventType.ComplianceCaseFollowUpCreated, actorId, caseId,
                        new { sourceCaseId = caseId, followUpCaseId = followUpCase.CaseId, reviewId });
                }
            }

            return new RecordMonitoringReviewResponse
            {
                Success    = true,
                Review     = review,
                Case       = c,
                FollowUpCase = followUpCase
            };
        }

        // ── TriggerPeriodicReviewCheckAsync ────────────────────────────────────

        /// <inheritdoc/>
        public Task<TriggerPeriodicReviewCheckResponse> TriggerPeriodicReviewCheckAsync(string actorId)
        {
            var now = _timeProvider.GetUtcNow();
            var overdueCaseIds = new List<string>();
            int inspected = 0;

            foreach (var (_, c) in _cases)
            {
                var sched = c.MonitoringSchedule;
                if (sched == null || !sched.IsActive)
                    continue;

                inspected++;

                if (now >= sched.NextReviewDueAt)
                {
                    sched.IsOverdue = true;
                    overdueCaseIds.Add(c.CaseId);

                    c.UpdatedAt = now;

                    _ = PersistCaseAsync(c);

                    EmitEventFireAndForget(WebhookEventType.ComplianceCaseOverdueReviewDetected, actorId, c.CaseId,
                        new { caseId = c.CaseId, nextReviewDueAt = sched.NextReviewDueAt, frequency = sched.Frequency.ToString() });

                    _logger.LogWarning(
                        "MonitoringReviewOverdue. CaseId={CaseId} DueAt={Due} Actor={Actor}",
                        LoggingHelper.SanitizeLogInput(c.CaseId),
                        sched.NextReviewDueAt.ToString("O"),
                        LoggingHelper.SanitizeLogInput(actorId));
                }
            }

            _logger.LogInformation(
                "PeriodicReviewCheck. Inspected={Inspected} Overdue={Overdue} Actor={Actor}",
                inspected,
                overdueCaseIds.Count,
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new TriggerPeriodicReviewCheckResponse
            {
                Success           = true,
                CasesInspected    = inspected,
                OverdueCasesFound = overdueCaseIds.Count,
                OverdueCaseIds    = overdueCaseIds,
                CheckedAt         = now
            });
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
            if (typeof(T) == typeof(SetMonitoringScheduleResponse))
                return (new SetMonitoringScheduleResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(RecordMonitoringReviewResponse))
                return (new RecordMonitoringReviewResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(ExportComplianceCaseResponse))
                return (new ExportComplianceCaseResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;

            throw new InvalidOperationException($"Unsupported response type {typeof(T).Name}");
        }

        // ── ExportCaseAsync ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ExportComplianceCaseResponse> ExportCaseAsync(string caseId, ExportComplianceCaseRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Fail<ExportComplianceCaseResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND);

            var now = _timeProvider.GetUtcNow();

            // Build ordered timeline
            var timeline = c.Timeline.OrderBy(e => e.OccurredAt).ToList();

            // Serialise the case snapshot and compute SHA-256 content hash
            string snapshotJson;
            string contentHash;
            try
            {
                snapshotJson = JsonSerializer.Serialize(c, new JsonSerializerOptions { WriteIndented = false });
                byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson));
                contentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExportCase: failed to serialise case. CaseId={CaseId}", LoggingHelper.SanitizeLogInput(caseId));
                return Fail<ExportComplianceCaseResponse>("Export generation failed: unable to serialise case data", "EXPORT_GENERATION_FAILED");
            }

            var exportId = Guid.NewGuid().ToString("N");
            var metadata = new CaseExportMetadata
            {
                ExportId    = exportId,
                ExportedAt  = now,
                ExportedBy  = actorId,
                Format      = request.Format,
                SchemaVersion = "1.0",
                ContentHash = contentHash
            };

            var bundle = new ComplianceCaseEvidenceBundle
            {
                CaseId       = caseId,
                CaseSnapshot = c,
                Timeline     = timeline,
                Metadata     = metadata
            };

            // Append export record to durable repository (fail-closed on error)
            if (_repository != null)
            {
                try
                {
                    await _repository.AppendExportRecordAsync(caseId, metadata);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ExportCase: repository.AppendExportRecord failed. CaseId={CaseId}", LoggingHelper.SanitizeLogInput(caseId));
                    return Fail<ExportComplianceCaseResponse>("Export generation failed: unable to record export in repository", "EXPORT_PERSISTENCE_FAILED");
                }
            }

            // Append timeline entry to the live case
            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.CaseExported, actorId, now,
                description: $"Case evidence bundle exported. ExportId={exportId} Format={request.Format}",
                metadata: new Dictionary<string, string> { ["exportId"] = exportId, ["format"] = request.Format }));

            c.UpdatedAt = now;
            _ = PersistCaseAsync(c);

            _logger.LogInformation(
                "CaseExported. CaseId={CaseId} ExportId={ExportId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(exportId),
                LoggingHelper.SanitizeLogInput(actorId));

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseExported, actorId, caseId,
                new { caseId, exportId, format = request.Format });

            return new ExportComplianceCaseResponse { Success = true, Bundle = bundle };
        }

        // ── Private helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Persists the case snapshot to the repository (if available) without blocking the caller.
        /// Errors are logged but do not propagate.
        /// </summary>
        private Task PersistCaseAsync(ComplianceCase c)
        {
            if (_repository == null)
                return Task.CompletedTask;

            return _repository.SaveCaseAsync(c).ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                    _logger.LogError(t.Exception,
                        "PersistCaseAsync: failed to save case to repository. CaseId={CaseId}",
                        LoggingHelper.SanitizeLogInput(c.CaseId));
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Emits a compliance lifecycle webhook event via <see cref="IWebhookService"/> in a
        /// fire-and-forget fashion.  Errors are caught and logged so they never block callers.
        /// </summary>
        private void EmitEventFireAndForget(WebhookEventType eventType, string actorId, string caseId, object payloadObj)
        {
            if (_webhookService == null)
                return;

            Dictionary<string, object>? data;
            try
            {
                // Serialize to JSON then back to Dictionary so we get a stable key-value map
                string json = JsonSerializer.Serialize(payloadObj);
                data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            }
            catch
            {
                data = new Dictionary<string, object> { ["caseId"] = caseId };
            }

            var ev = new WebhookEvent
            {
                Id        = Guid.NewGuid().ToString("N"),
                EventType = eventType,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                Actor     = actorId,
                Data      = data
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    await _webhookService.EmitEventAsync(ev);
                }
                catch (Exception ex)
                {
                    try
                    {
                        _logger.LogError(ex,
                            "EmitEventFireAndForget: webhook emission failed. EventType={EventType} CaseId={CaseId}",
                            eventType, LoggingHelper.SanitizeLogInput(caseId));
                    }
                    catch
                    {
                        // Swallow any logger exception to prevent the background task from faulting
                    }
                }
            });
        }
    }
}
