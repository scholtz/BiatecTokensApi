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
                [ComplianceCaseState.UnderReview]     = new HashSet<ComplianceCaseState> { ComplianceCaseState.Approved, ComplianceCaseState.Rejected, ComplianceCaseState.Escalated, ComplianceCaseState.Remediating, ComplianceCaseState.EvidencePending, ComplianceCaseState.Blocked },
                [ComplianceCaseState.Escalated]       = new HashSet<ComplianceCaseState> { ComplianceCaseState.UnderReview, ComplianceCaseState.Rejected, ComplianceCaseState.EvidencePending, ComplianceCaseState.Blocked },
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

            // Emit targeted semantic events for key state destinations
            if (request.NewState == ComplianceCaseState.EvidencePending)
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseEvidenceRequested, actorId, caseId,
                    new { caseId, issuerId = c.IssuerId, subjectId = c.SubjectId, reason = request.Reason });

            if (request.NewState == ComplianceCaseState.Approved)
            {
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseApprovalReady, actorId, caseId,
                    new { caseId, issuerId = c.IssuerId, subjectId = c.SubjectId });
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseApprovalGranted, actorId, caseId,
                    new { caseId, issuerId = c.IssuerId, subjectId = c.SubjectId, reason = request.Reason, decidedBy = actorId });
            }

            if (request.NewState == ComplianceCaseState.Rejected)
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseApprovalDenied, actorId, caseId,
                    new { caseId, issuerId = c.IssuerId, subjectId = c.SubjectId, reason = request.Reason, decidedBy = actorId });

            if (request.NewState == ComplianceCaseState.Remediating)
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseReworkRequested, actorId, caseId,
                    new { caseId, issuerId = c.IssuerId, subjectId = c.SubjectId, reason = request.Reason, requestedBy = actorId });

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

        /// <summary>Derives an urgency band based on time remaining to the review due date.</summary>
        private static CaseUrgencyBand ComputeUrgencyBand(DateTimeOffset? reviewDueAt, DateTimeOffset now)
        {
            if (!reviewDueAt.HasValue)
                return CaseUrgencyBand.Normal;

            var remaining = reviewDueAt.Value - now;

            if (remaining <= TimeSpan.Zero)
                return CaseUrgencyBand.Critical;          // overdue

            if (remaining <= TimeSpan.FromDays(3))
                return CaseUrgencyBand.Critical;          // due within 3 days

            if (remaining <= TimeSpan.FromDays(7))
                return CaseUrgencyBand.Warning;           // due within 7 days

            return CaseUrgencyBand.Normal;
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
            if (typeof(T) == typeof(AssignCaseResponse))
                return (new AssignCaseResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(GetAssignmentHistoryResponse))
                return (new GetAssignmentHistoryResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(GetEscalationHistoryResponse))
                return (new GetEscalationHistoryResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(SetSlaMetadataResponse))
                return (new SetSlaMetadataResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;
            if (typeof(T) == typeof(GetDeliveryStatusResponse))
                return (new GetDeliveryStatusResponse { Success = false, ErrorCode = errorCode, ErrorMessage = message } as T)!;

            throw new InvalidOperationException($"Unsupported response type {typeof(T).Name}");
        }

        // ── AssignCaseAsync ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<AssignCaseResponse> AssignCaseAsync(string caseId, AssignCaseRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<AssignCaseResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            if (request.ReviewerId == null && request.TeamId == null)
                return Task.FromResult(Fail<AssignCaseResponse>(
                    "At least one of ReviewerId or TeamId must be provided",
                    ErrorCodes.MISSING_REQUIRED_FIELD));

            var now = _timeProvider.GetUtcNow();

            var record = new CaseAssignmentRecord
            {
                AssignmentId       = Guid.NewGuid().ToString("N"),
                CaseId             = caseId,
                PreviousReviewerId = c.AssignedReviewerId,
                NewReviewerId      = request.ReviewerId,
                PreviousTeamId     = c.AssignedTeamId,
                NewTeamId          = request.TeamId,
                Reason             = request.Reason,
                AssignedBy         = actorId,
                AssignedAt         = now
            };

            bool reviewerChanged = request.ReviewerId != c.AssignedReviewerId;
            bool teamChanged     = request.TeamId     != c.AssignedTeamId;

            if (request.ReviewerId != null)
                c.AssignedReviewerId = request.ReviewerId;
            if (request.TeamId != null)
                c.AssignedTeamId = request.TeamId;

            c.AssignmentHistory.Add(record);
            c.UpdatedAt = now;

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.ReviewerAssigned, actorId, now,
                description: $"Case assigned. Reviewer={request.ReviewerId ?? c.AssignedReviewerId ?? "(unchanged)"} Team={request.TeamId ?? c.AssignedTeamId ?? "(unchanged)"}. Reason: {request.Reason ?? "none"}",
                metadata: new Dictionary<string, string>
                {
                    ["assignmentId"]       = record.AssignmentId,
                    ["previousReviewerId"] = record.PreviousReviewerId ?? "",
                    ["newReviewerId"]      = record.NewReviewerId       ?? "",
                    ["previousTeamId"]     = record.PreviousTeamId     ?? "",
                    ["newTeamId"]          = record.NewTeamId           ?? ""
                }));

            _logger.LogInformation(
                "CaseAssigned. CaseId={CaseId} PrevReviewer={Prev} NewReviewer={New} Team={Team} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                LoggingHelper.SanitizeLogInput(record.PreviousReviewerId ?? "(none)"),
                LoggingHelper.SanitizeLogInput(record.NewReviewerId       ?? "(none)"),
                LoggingHelper.SanitizeLogInput(record.NewTeamId           ?? "(none)"),
                LoggingHelper.SanitizeLogInput(actorId));

            _ = PersistCaseAsync(c);

            if (reviewerChanged)
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseAssignmentChanged, actorId, caseId,
                    new
                    {
                        caseId,
                        assignmentId       = record.AssignmentId,
                        previousReviewerId = record.PreviousReviewerId,
                        newReviewerId      = record.NewReviewerId,
                        reason             = request.Reason
                    });

            if (teamChanged)
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseTeamAssigned, actorId, caseId,
                    new
                    {
                        caseId,
                        assignmentId   = record.AssignmentId,
                        previousTeamId = record.PreviousTeamId,
                        newTeamId      = record.NewTeamId,
                        reason         = request.Reason
                    });

            return Task.FromResult(new AssignCaseResponse { Success = true, Case = c, AssignmentRecord = record });
        }

        // ── GetAssignmentHistoryAsync ──────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetAssignmentHistoryResponse> GetAssignmentHistoryAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<GetAssignmentHistoryResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            var history = c.AssignmentHistory.OrderBy(r => r.AssignedAt).ToList();

            return Task.FromResult(new GetAssignmentHistoryResponse
            {
                Success    = true,
                CaseId     = caseId,
                History    = history,
                TotalCount = history.Count
            });
        }

        // ── GetEscalationHistoryAsync ──────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetEscalationHistoryResponse> GetEscalationHistoryAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<GetEscalationHistoryResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            var escalations = c.Escalations.OrderBy(e => e.RaisedAt).ToList();

            return Task.FromResult(new GetEscalationHistoryResponse
            {
                Success       = true,
                CaseId        = caseId,
                Escalations   = escalations,
                OpenCount     = escalations.Count(e => e.Status == EscalationStatus.Open || e.Status == EscalationStatus.UnderReview),
                ResolvedCount = escalations.Count(e => e.Status == EscalationStatus.Resolved)
            });
        }

        // ── SetSlaMetadataAsync ────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<SetSlaMetadataResponse> SetSlaMetadataAsync(string caseId, SetSlaMetadataRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<SetSlaMetadataResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            var now = _timeProvider.GetUtcNow();

            bool isOverdue = request.ReviewDueAt.HasValue && request.ReviewDueAt.Value < now;
            DateTimeOffset? overdueSince = isOverdue ? now : null;
            CaseUrgencyBand urgencyBand  = ComputeUrgencyBand(request.ReviewDueAt, now);

            var sla = new CaseSlaMetadata
            {
                ReviewDueAt     = request.ReviewDueAt,
                EscalationDueAt = request.EscalationDueAt,
                IsOverdue       = isOverdue,
                OverdueSince    = overdueSince,
                UrgencyBand     = urgencyBand,
                SetBy           = actorId,
                SetAt           = now,
                Notes           = request.Notes
            };

            c.SlaMetadata = sla;
            c.UpdatedAt   = now;

            _logger.LogInformation(
                "SlaSet. CaseId={CaseId} ReviewDueAt={Due} Urgency={Urgency} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                request.ReviewDueAt?.ToString("O"),
                urgencyBand,
                LoggingHelper.SanitizeLogInput(actorId));

            _ = PersistCaseAsync(c);

            if (isOverdue)
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseSlaBreached, actorId, caseId,
                    new { caseId, reviewDueAt = request.ReviewDueAt, breachedAt = now });

            return Task.FromResult(new SetSlaMetadataResponse { Success = true, SlaMetadata = sla, Case = c });
        }

        // ── GetDeliveryStatusAsync ─────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetDeliveryStatusResponse> GetDeliveryStatusAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(Fail<GetDeliveryStatusResponse>($"Case {caseId} not found", ErrorCodes.NOT_FOUND));

            var records = c.DeliveryRecords.OrderByDescending(r => r.AttemptedAt).ToList();

            return Task.FromResult(new GetDeliveryStatusResponse
            {
                Success          = true,
                CaseId           = caseId,
                Records          = records,
                TotalCount       = records.Count,
                SuccessCount     = records.Count(r => r.Outcome == CaseDeliveryOutcome.Success),
                FailureCount     = records.Count(r => r.Outcome == CaseDeliveryOutcome.Failure || r.Outcome == CaseDeliveryOutcome.RetryExhausted),
                PendingRetryCount= records.Count(r => r.Outcome == CaseDeliveryOutcome.RetryScheduled)
            });
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

        // ── GetCaseSummaryAsync ────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<CaseSummaryResponse> GetCaseSummaryAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new CaseSummaryResponse
                {
                    Success = false, ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            var now = _timeProvider.GetUtcNow();
            CheckAndApplyEvidenceFreshness(c);
            var blockers = ComputeBlockers(c, now);
            var failClosedBlockers = blockers.Where(b => b.IsFailClosed).ToList();
            var topBlocker = failClosedBlockers.OrderByDescending(b => b.Severity).FirstOrDefault();

            var summary = new CaseSummary
            {
                CaseId              = c.CaseId,
                IssuerId            = c.IssuerId,
                SubjectId           = c.SubjectId,
                Type                = c.Type,
                State               = c.State,
                Priority            = c.Priority,
                AssignedReviewerId  = c.AssignedReviewerId,
                AssignedTeamId      = c.AssignedTeamId,
                UrgencyBand         = c.SlaMetadata?.UrgencyBand ?? CaseUrgencyBand.Normal,
                CreatedAt           = c.CreatedAt,
                UpdatedAt           = c.UpdatedAt,
                BlockerCount        = failClosedBlockers.Count,
                OpenRemediationTasks = c.RemediationTasks.Count(t => t.Status == RemediationTaskStatus.Open),
                OpenEscalations     = c.Escalations.Count(e => e.Status == EscalationStatus.Open),
                HasStaleEvidence    = c.IsEvidenceStale,
                IsHandoffReady      = c.HandoffStatus?.IsHandoffReady ?? true,
                TopBlockerTitle     = topBlocker?.Title,
                NextActionDescription = DeriveNextAction(c, failClosedBlockers),
                Jurisdiction        = c.Jurisdiction,
                DecisionCount       = c.DecisionHistory.Count,
                HandoffStage        = c.HandoffStatus?.Stage ?? CaseHandoffStage.NotStarted
            };

            return Task.FromResult(new CaseSummaryResponse { Success = true, Summary = summary });
        }

        // ── ListCaseSummariesAsync ─────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ListCaseSummariesResponse> ListCaseSummariesAsync(ListComplianceCasesRequest request, string actorId)
        {
            var listResult = await ListCasesAsync(request, actorId);
            if (!listResult.Success)
                return new ListCaseSummariesResponse
                {
                    Success = false,
                    ErrorCode = listResult.ErrorCode,
                    ErrorMessage = listResult.ErrorMessage
                };

            var now = _timeProvider.GetUtcNow();
            var summaries = new List<CaseSummary>(listResult.Cases.Count);
            foreach (var c in listResult.Cases)
            {
                var blockers = ComputeBlockers(c, now);
                var failClosedBlockers = blockers.Where(b => b.IsFailClosed).ToList();
                var topBlocker = failClosedBlockers.OrderByDescending(b => b.Severity).FirstOrDefault();

                summaries.Add(new CaseSummary
                {
                    CaseId              = c.CaseId,
                    IssuerId            = c.IssuerId,
                    SubjectId           = c.SubjectId,
                    Type                = c.Type,
                    State               = c.State,
                    Priority            = c.Priority,
                    AssignedReviewerId  = c.AssignedReviewerId,
                    AssignedTeamId      = c.AssignedTeamId,
                    UrgencyBand         = c.SlaMetadata?.UrgencyBand ?? CaseUrgencyBand.Normal,
                    CreatedAt           = c.CreatedAt,
                    UpdatedAt           = c.UpdatedAt,
                    BlockerCount        = failClosedBlockers.Count,
                    OpenRemediationTasks = c.RemediationTasks.Count(t => t.Status == RemediationTaskStatus.Open),
                    OpenEscalations     = c.Escalations.Count(e => e.Status == EscalationStatus.Open),
                    HasStaleEvidence    = c.IsEvidenceStale,
                    IsHandoffReady      = c.HandoffStatus?.IsHandoffReady ?? true,
                    TopBlockerTitle     = topBlocker?.Title,
                    NextActionDescription = DeriveNextAction(c, failClosedBlockers),
                    Jurisdiction        = c.Jurisdiction,
                    DecisionCount       = c.DecisionHistory.Count,
                    HandoffStage        = c.HandoffStatus?.Stage ?? CaseHandoffStage.NotStarted
                });
            }

            return new ListCaseSummariesResponse
            {
                Success    = true,
                Summaries  = summaries,
                TotalCount = listResult.TotalCount,
                Page       = listResult.Page,
                PageSize   = listResult.PageSize
            };
        }

        // ── EvaluateBlockersAsync ──────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<EvaluateBlockersResponse> EvaluateBlockersAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new EvaluateBlockersResponse
                {
                    Success = false, ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            var now = _timeProvider.GetUtcNow();
            CheckAndApplyEvidenceFreshness(c);
            var blockers = ComputeBlockers(c, now);

            // Snapshot blockers onto the case for future GetCaseAsync visibility
            c.Blockers = blockers;

            var failClosed = blockers.Where(b => b.IsFailClosed).ToList();
            var warnings   = blockers.Where(b => !b.IsFailClosed).ToList();

            return Task.FromResult(new EvaluateBlockersResponse
            {
                Success            = true,
                CaseId             = caseId,
                Blockers           = blockers,
                FailClosedBlockers = failClosed,
                Warnings           = warnings,
                CanProceed         = failClosed.Count == 0,
                EvaluatedAt        = now
            });
        }

        // ── AddDecisionRecordAsync ─────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<AddDecisionRecordResponse> AddDecisionRecordAsync(
            string caseId, AddDecisionRecordRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(request.DecisionSummary))
                return Task.FromResult(new AddDecisionRecordResponse
                {
                    Success = false, ErrorCode = "INVALID_REQUEST",
                    ErrorMessage = "DecisionSummary is required"
                });

            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new AddDecisionRecordResponse
                {
                    Success = false, ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            var now = _timeProvider.GetUtcNow();
            var record = new CaseDecisionRecord
            {
                DecisionId        = Guid.NewGuid().ToString("N"),
                CaseId            = caseId,
                Kind              = request.Kind,
                DecisionSummary   = request.DecisionSummary,
                Outcome           = request.Outcome,
                ProviderName      = request.ProviderName,
                ProviderReference = request.ProviderReference,
                Explanation       = request.Explanation,
                IsAdverse         = request.IsAdverse,
                DecidedBy         = actorId,
                DecidedAt         = now,
                Attributes        = request.Attributes ?? new Dictionary<string, string>()
            };

            c.DecisionHistory.Add(record);
            c.UpdatedAt = now;

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.DecisionRecorded, actorId, now,
                description: $"Decision recorded: {request.Kind} — {request.DecisionSummary}",
                metadata: new Dictionary<string, string>
                {
                    ["decisionId"] = record.DecisionId,
                    ["kind"]       = request.Kind.ToString(),
                    ["outcome"]    = request.Outcome ?? "(none)",
                    ["isAdverse"]  = request.IsAdverse.ToString()
                }));

            _ = PersistCaseAsync(c);

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseDecisionRecorded, actorId, caseId,
                new { caseId, decisionId = record.DecisionId, kind = request.Kind.ToString(),
                      outcome = request.Outcome, isAdverse = request.IsAdverse });

            _logger.LogInformation(
                "AddDecisionRecord. CaseId={CaseId} Kind={Kind} Adverse={Adverse} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                request.Kind.ToString(),
                request.IsAdverse,
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new AddDecisionRecordResponse { Success = true, DecisionRecord = record, Case = c });
        }

        // ── GetDecisionHistoryAsync ────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetDecisionHistoryResponse> GetDecisionHistoryAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new GetDecisionHistoryResponse
                {
                    Success = false, ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            var decisions = c.DecisionHistory.OrderBy(d => d.DecidedAt).ToList();
            return Task.FromResult(new GetDecisionHistoryResponse
            {
                Success      = true,
                CaseId       = caseId,
                Decisions    = decisions,
                TotalCount   = decisions.Count,
                AdverseCount = decisions.Count(d => d.IsAdverse)
            });
        }

        // ── UpdateHandoffStatusAsync ───────────────────────────────────────────

        /// <inheritdoc/>
        public Task<UpdateHandoffStatusResponse> UpdateHandoffStatusAsync(
            string caseId, UpdateHandoffStatusRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new UpdateHandoffStatusResponse
                {
                    Success = false, ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            var now = _timeProvider.GetUtcNow();
            bool isReady = request.Stage == CaseHandoffStage.Completed;

            var handoff = new CaseHandoffStatus
            {
                CaseId                 = caseId,
                Stage                  = request.Stage,
                IsHandoffReady         = isReady,
                BlockingReason         = isReady ? null : request.BlockingReason,
                UnresolvedDependencies = request.UnresolvedDependencies ?? new List<string>(),
                HandoffDueAt           = request.HandoffDueAt,
                HandoffCompletedAt     = isReady ? now : null,
                HandoffNotes           = request.HandoffNotes,
                UpdatedBy              = actorId,
                UpdatedAt              = now
            };

            c.HandoffStatus = handoff;
            c.UpdatedAt = now;

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.HandoffStatusChanged, actorId, now,
                description: $"Handoff status updated: {request.Stage}" +
                             (isReady ? " — handoff complete" : $" — {request.BlockingReason ?? "pending"}"),
                metadata: new Dictionary<string, string>
                {
                    ["stage"]      = request.Stage.ToString(),
                    ["isReady"]    = isReady.ToString(),
                    ["unresolved"] = string.Join(",", handoff.UnresolvedDependencies)
                }));

            _ = PersistCaseAsync(c);

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseHandoffStatusChanged, actorId, caseId,
                new { caseId, stage = request.Stage.ToString(), isHandoffReady = isReady,
                      blockingReason = request.BlockingReason });

            _logger.LogInformation(
                "UpdateHandoffStatus. CaseId={CaseId} Stage={Stage} Ready={Ready} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                request.Stage.ToString(),
                isReady,
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new UpdateHandoffStatusResponse { Success = true, HandoffStatus = handoff, Case = c });
        }

        // ── GetHandoffStatusAsync ──────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetHandoffStatusResponse> GetHandoffStatusAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new GetHandoffStatusResponse
                {
                    Success = false, ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            return Task.FromResult(new GetHandoffStatusResponse { Success = true, HandoffStatus = c.HandoffStatus });
        }

        // ── Blocker computation ────────────────────────────────────────────────

        /// <summary>
        /// Computes all structured blockers for a case using fail-closed semantics.
        /// Returns both fail-closed blockers (prevent readiness) and advisory warnings.
        /// </summary>
        private List<CaseBlocker> ComputeBlockers(ComplianceCase c, DateTimeOffset now)
        {
            var blockers = new List<CaseBlocker>();

            // 1. No evidence at all — fail-closed
            if (!c.EvidenceSummaries.Any(e => e.CapturedAt.HasValue))
                blockers.Add(new CaseBlocker
                {
                    Category        = CaseBlockerCategory.MissingEvidence,
                    Severity        = EvidenceIssueSeverityLevel.Critical,
                    Title           = "No evidence captured",
                    Description     = "The case has no captured evidence. At least one evidence record is required before review can proceed.",
                    RemediationHint = "Add KYC, AML, or identity evidence to the case using the evidence endpoint.",
                    IsFailClosed    = true,
                    DetectedAt      = now
                });

            // 2. Stale evidence on the case as a whole
            if (c.IsEvidenceStale)
                blockers.Add(new CaseBlocker
                {
                    Category        = CaseBlockerCategory.StaleEvidence,
                    Severity        = EvidenceIssueSeverityLevel.High,
                    Title           = "Case evidence is stale",
                    Description     = $"The case evidence bundle expired at {c.EvidenceExpiresAt?.ToString("O") ?? "an unknown time"}. Stale evidence must be refreshed before the case can be approved.",
                    RemediationHint = "Re-submit current evidence and update the case evidence expiry date.",
                    IsFailClosed    = true,
                    DetectedAt      = now
                });

            // 3. Individual stale evidence records (evaluated against current time, not stored flag)
            foreach (var ev in c.EvidenceSummaries.Where(e => e.ExpiresAt.HasValue && e.ExpiresAt.Value < now))
                blockers.Add(new CaseBlocker
                {
                    Category        = CaseBlockerCategory.StaleEvidence,
                    Severity        = EvidenceIssueSeverityLevel.High,
                    Title           = $"Evidence expired: {ev.EvidenceType}",
                    Description     = $"The '{ev.EvidenceType}' evidence record expired at {ev.ExpiresAt:O}.",
                    RemediationHint = "Obtain a fresh evidence record from the provider and update it on the case.",
                    LinkedEntityId  = ev.EvidenceId,
                    IsFailClosed    = true,
                    DetectedAt      = now
                });

            // 4. Evidence blocking readiness (provider-flagged)
            foreach (var ev in c.EvidenceSummaries.Where(e => e.IsBlockingReadiness))
                blockers.Add(new CaseBlocker
                {
                    Category        = CaseBlockerCategory.MissingEvidence,
                    Severity        = EvidenceIssueSeverityLevel.High,
                    Title           = $"Blocking evidence: {ev.EvidenceType}",
                    Description     = ev.BlockingReason ?? $"The '{ev.EvidenceType}' evidence is flagged as blocking case readiness.",
                    RemediationHint = "Resolve the underlying evidence issue or replace with a valid evidence record.",
                    LinkedEntityId  = ev.EvidenceId,
                    IsFailClosed    = true,
                    DetectedAt      = now
                });

            // 5. Open escalations requiring manual review
            foreach (var esc in c.Escalations.Where(e => e.Status == EscalationStatus.Open && e.RequiresManualReview))
            {
                bool isSanctions = esc.Type == EscalationType.SanctionsHit;
                blockers.Add(new CaseBlocker
                {
                    Category        = isSanctions ? CaseBlockerCategory.UnresolvedSanctions : CaseBlockerCategory.OpenEscalation,
                    Severity        = EvidenceIssueSeverityLevel.Critical,
                    Title           = isSanctions ? "Sanctions review pending analyst decision" : $"Open escalation: {esc.Type}",
                    Description     = esc.Description,
                    RemediationHint = "Assign a sanctions analyst to review and resolve the escalation.",
                    LinkedEntityId  = esc.EscalationId,
                    IsFailClosed    = true,
                    DetectedAt      = now
                });
            }

            // 6. Open blocking remediation tasks
            foreach (var rt in c.RemediationTasks.Where(t => t.IsBlockingCase && t.Status == RemediationTaskStatus.Open))
                blockers.Add(new CaseBlocker
                {
                    Category        = CaseBlockerCategory.OpenRemediationTask,
                    Severity        = rt.BlockerSeverity,
                    Title           = $"Open remediation task: {rt.Title}",
                    Description     = rt.Description ?? rt.Title,
                    RemediationHint = "Complete or dismiss the remediation task to unblock the case.",
                    LinkedEntityId  = rt.TaskId,
                    IsFailClosed    = true,
                    DetectedAt      = now
                });

            // 7. SLA breach (advisory — does not block readiness on its own)
            if (c.SlaMetadata?.IsOverdue == true)
                blockers.Add(new CaseBlocker
                {
                    Category        = CaseBlockerCategory.SlaBreached,
                    Severity        = EvidenceIssueSeverityLevel.Critical,
                    Title           = "SLA review deadline breached",
                    Description     = $"The case review was due by {c.SlaMetadata.ReviewDueAt:O} and is now overdue.",
                    RemediationHint = "Escalate to the compliance manager and update the review status immediately.",
                    IsFailClosed    = false,
                    DetectedAt      = now
                });

            // 8. No reviewer assigned (advisory)
            if (string.IsNullOrWhiteSpace(c.AssignedReviewerId) && string.IsNullOrWhiteSpace(c.AssignedTeamId))
                blockers.Add(new CaseBlocker
                {
                    Category        = CaseBlockerCategory.MissingAssignment,
                    Severity        = EvidenceIssueSeverityLevel.Medium,
                    Title           = "No reviewer assigned",
                    Description     = "The case does not have a reviewer or team assigned. This may delay review completion.",
                    RemediationHint = "Assign a reviewer or team using the assignment endpoint.",
                    IsFailClosed    = false,
                    DetectedAt      = now
                });

            // 9. Adverse KYC/AML/Sanctions decisions
            var unresolvedAdverse = c.DecisionHistory
                .Where(d => d.IsAdverse && d.Kind is CaseDecisionKind.KycRejection
                    or CaseDecisionKind.AmlHit or CaseDecisionKind.SanctionsReview)
                .ToList();
            foreach (var dec in unresolvedAdverse)
                blockers.Add(new CaseBlocker
                {
                    Category = dec.Kind == CaseDecisionKind.KycRejection ? CaseBlockerCategory.PendingKycDecision :
                               dec.Kind == CaseDecisionKind.AmlHit ? CaseBlockerCategory.PendingAmlDecision :
                               CaseBlockerCategory.UnresolvedSanctions,
                    Severity        = EvidenceIssueSeverityLevel.High,
                    Title           = $"Adverse decision: {dec.Kind} — {dec.Outcome ?? dec.DecisionSummary}",
                    Description     = dec.Explanation ?? $"An adverse {dec.Kind} decision has been recorded and must be addressed.",
                    RemediationHint = "Review the decision record and either resolve the concern or add a manual override with justification.",
                    LinkedEntityId  = dec.DecisionId,
                    IsFailClosed    = true,
                    DetectedAt      = now
                });

            // 10. Handoff failure
            if (c.HandoffStatus?.Stage == CaseHandoffStage.Failed)
                blockers.Add(new CaseBlocker
                {
                    Category        = CaseBlockerCategory.DownstreamDeliveryFailure,
                    Severity        = EvidenceIssueSeverityLevel.High,
                    Title           = "Downstream handoff failed",
                    Description     = c.HandoffStatus.BlockingReason ?? "A downstream delivery or distribution step has failed.",
                    RemediationHint = "Investigate the downstream failure and re-trigger the handoff after resolution.",
                    IsFailClosed    = true,
                    DetectedAt      = now
                });

            return blockers;
        }

        /// <summary>Derives a plain-language next-action description from case state and active blockers.</summary>
        private static string DeriveNextAction(ComplianceCase c, List<CaseBlocker> failClosedBlockers)
        {
            if (failClosedBlockers.Count > 0)
            {
                var top = failClosedBlockers.OrderByDescending(b => b.Severity).First();
                return top.RemediationHint ?? top.Title;
            }

            return c.State switch
            {
                ComplianceCaseState.Intake          => "Transition the case to EvidencePending to begin evidence collection.",
                ComplianceCaseState.EvidencePending => "Collect and attach required evidence (KYC, AML, identity documents) to the case.",
                ComplianceCaseState.UnderReview     => "Complete the compliance review and transition to Approved or Escalated.",
                ComplianceCaseState.Escalated       => "Assign a senior analyst to review the escalation and resolve or reject the case.",
                ComplianceCaseState.Remediating     => "Complete all open remediation tasks to progress the case to review or approval.",
                ComplianceCaseState.Approved        => "Case is approved. Initiate downstream handoff if not already complete.",
                ComplianceCaseState.Rejected        => "Case has been rejected. No further actions required.",
                ComplianceCaseState.Stale           => "Evidence has expired. Collect fresh evidence and resubmit to EvidencePending.",
                ComplianceCaseState.Blocked         => "The case is blocked. Investigate the blocking reason and transition back to Intake when resolved.",
                _                                   => "Review the case state and determine the appropriate next step."
            };
        }

        // ── ApproveComplianceCaseAsync ─────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ApproveComplianceCaseResponse> ApproveComplianceCaseAsync(
            string caseId, ApproveComplianceCaseRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new ApproveComplianceCaseResponse
                {
                    Success = false, ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            var allowedFromStates = new HashSet<ComplianceCaseState>
                { ComplianceCaseState.UnderReview, ComplianceCaseState.Remediating };

            if (!allowedFromStates.Contains(c.State))
                return Task.FromResult(new ApproveComplianceCaseResponse
                {
                    Success = false, ErrorCode = ErrorCodes.INVALID_STATE_TRANSITION,
                    ErrorMessage = $"Case cannot be approved from state '{c.State}'. " +
                                   "Approval is only valid from UnderReview or Remediating."
                });

            var decidedBy = request.ApprovedBy ?? actorId;
            var now = _timeProvider.GetUtcNow();
            var decisionId = Guid.NewGuid().ToString("N");

            // Record an approval decision entry
            var decision = new CaseDecisionRecord
            {
                DecisionId        = decisionId,
                CaseId            = caseId,
                Kind              = CaseDecisionKind.ApprovalDecision,
                DecisionSummary   = request.Rationale ?? "Case formally approved.",
                Outcome           = "Approved",
                Explanation       = request.ApprovalNotes,
                ProviderReference = request.ExternalApprovalReference,
                IsAdverse         = false,
                DecidedBy         = decidedBy,
                DecidedAt         = now
            };
            c.DecisionHistory.Add(decision);

            // Transition to Approved
            var fromState = c.State;
            c.State         = ComplianceCaseState.Approved;
            c.UpdatedAt     = now;
            c.ClosedAt      = now;
            c.ClosureReason = request.Rationale ?? "Approved";

            var idempotencyKey = $"{c.IssuerId}|{c.SubjectId}|{c.Type}";
            _idempotencyIndex.TryRemove(idempotencyKey, out _);

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.ApprovalDecisionRecorded, decidedBy, now,
                description: $"Case approved. Rationale: {request.Rationale ?? "(none)"}",
                fromState: fromState, toState: ComplianceCaseState.Approved,
                metadata: new Dictionary<string, string>
                {
                    ["decisionId"] = decisionId,
                    ["approvedBy"] = decidedBy,
                    ["externalRef"] = request.ExternalApprovalReference ?? string.Empty
                }));

            _logger.LogInformation(
                "ApproveCase. CaseId={CaseId} DecisionId={DecisionId} DecidedBy={DecidedBy}",
                LoggingHelper.SanitizeLogInput(caseId),
                decisionId,
                LoggingHelper.SanitizeLogInput(decidedBy));

            _ = PersistCaseAsync(c);

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseApprovalReady, decidedBy, caseId,
                new { caseId, issuerId = c.IssuerId, subjectId = c.SubjectId });
            EmitEventFireAndForget(WebhookEventType.ComplianceCaseApprovalGranted, decidedBy, caseId,
                new { caseId, issuerId = c.IssuerId, subjectId = c.SubjectId,
                      reason = request.Rationale, decidedBy, decisionId,
                      externalRef = request.ExternalApprovalReference });
            EmitEventFireAndForget(WebhookEventType.ComplianceCaseDecisionRecorded, decidedBy, caseId,
                new { caseId, decisionId, kind = CaseDecisionKind.ApprovalDecision.ToString(),
                      outcome = "Approved", decidedBy });

            return Task.FromResult(new ApproveComplianceCaseResponse
            {
                Success    = true,
                Case       = c,
                DecisionId = decisionId
            });
        }

        // ── RejectComplianceCaseAsync ──────────────────────────────────────────

        /// <inheritdoc/>
        public Task<RejectComplianceCaseResponse> RejectComplianceCaseAsync(
            string caseId, RejectComplianceCaseRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new RejectComplianceCaseResponse
                {
                    Success = false, ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            if (string.IsNullOrWhiteSpace(request.Reason))
                return Task.FromResult(new RejectComplianceCaseResponse
                {
                    Success = false, ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "Reason is required when rejecting a case."
                });

            var allowedFromStates = new HashSet<ComplianceCaseState>
                { ComplianceCaseState.UnderReview, ComplianceCaseState.Escalated, ComplianceCaseState.Remediating };

            if (!allowedFromStates.Contains(c.State))
                return Task.FromResult(new RejectComplianceCaseResponse
                {
                    Success = false, ErrorCode = ErrorCodes.INVALID_STATE_TRANSITION,
                    ErrorMessage = $"Case cannot be rejected from state '{c.State}'. " +
                                   "Rejection is only valid from UnderReview, Escalated, or Remediating."
                });

            var decidedBy = request.RejectedBy ?? actorId;
            var now = _timeProvider.GetUtcNow();
            var decisionId = Guid.NewGuid().ToString("N");

            // Record a rejection decision entry
            var decision = new CaseDecisionRecord
            {
                DecisionId        = decisionId,
                CaseId            = caseId,
                Kind              = CaseDecisionKind.RejectionDecision,
                DecisionSummary   = request.Reason,
                Outcome           = "Rejected",
                Explanation       = request.RejectionNotes,
                ProviderReference = request.ExternalRejectionReference,
                IsAdverse         = true,
                DecidedBy         = decidedBy,
                DecidedAt         = now
            };
            c.DecisionHistory.Add(decision);

            // Transition to Rejected
            var fromState = c.State;
            c.State         = ComplianceCaseState.Rejected;
            c.UpdatedAt     = now;
            c.ClosedAt      = now;
            c.ClosureReason = request.Reason;

            var idempotencyKey = $"{c.IssuerId}|{c.SubjectId}|{c.Type}";
            _idempotencyIndex.TryRemove(idempotencyKey, out _);

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.RejectionDecisionRecorded, decidedBy, now,
                description: $"Case rejected. Reason: {request.Reason}",
                fromState: fromState, toState: ComplianceCaseState.Rejected,
                metadata: new Dictionary<string, string>
                {
                    ["decisionId"] = decisionId,
                    ["rejectedBy"] = decidedBy,
                    ["externalRef"] = request.ExternalRejectionReference ?? string.Empty
                }));

            _logger.LogInformation(
                "RejectCase. CaseId={CaseId} DecisionId={DecisionId} DecidedBy={DecidedBy}",
                LoggingHelper.SanitizeLogInput(caseId),
                decisionId,
                LoggingHelper.SanitizeLogInput(decidedBy));

            _ = PersistCaseAsync(c);

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseApprovalDenied, decidedBy, caseId,
                new { caseId, issuerId = c.IssuerId, subjectId = c.SubjectId,
                      reason = request.Reason, decidedBy, decisionId,
                      externalRef = request.ExternalRejectionReference });
            EmitEventFireAndForget(WebhookEventType.ComplianceCaseDecisionRecorded, decidedBy, caseId,
                new { caseId, decisionId, kind = CaseDecisionKind.RejectionDecision.ToString(),
                      outcome = "Rejected", decidedBy, isAdverse = true });

            return Task.FromResult(new RejectComplianceCaseResponse
            {
                Success    = true,
                Case       = c,
                DecisionId = decisionId
            });
        }

        // ── ReturnForInformationAsync ──────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ReturnForInformationResponse> ReturnForInformationAsync(
            string caseId, ReturnForInformationRequest request, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new ReturnForInformationResponse
                {
                    Success = false, ErrorCode = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            if (string.IsNullOrWhiteSpace(request.Reason))
                return Task.FromResult(new ReturnForInformationResponse
                {
                    Success = false, ErrorCode = ErrorCodes.MISSING_REQUIRED_FIELD,
                    ErrorMessage = "Reason is required when returning a case for information."
                });

            var allowedFromStates = new HashSet<ComplianceCaseState>
                { ComplianceCaseState.UnderReview, ComplianceCaseState.Escalated };

            if (!allowedFromStates.Contains(c.State))
                return Task.FromResult(new ReturnForInformationResponse
                {
                    Success = false, ErrorCode = ErrorCodes.INVALID_STATE_TRANSITION,
                    ErrorMessage = $"Case cannot be returned for information from state '{c.State}'. " +
                                   "Return-for-information is only valid from UnderReview or Escalated."
                });

            var targetState = request.TargetStage == ReturnForInformationTargetStage.Remediating
                ? ComplianceCaseState.Remediating
                : ComplianceCaseState.EvidencePending;

            var fromState = c.State;
            var now = _timeProvider.GetUtcNow();

            c.State     = targetState;
            c.UpdatedAt = now;

            var itemsText = request.RequestedItems?.Count > 0
                ? string.Join("; ", request.RequestedItems)
                : null;

            c.Timeline.Add(BuildTimelineEntry(
                caseId, CaseTimelineEventType.ReturnedForInformation, actorId, now,
                description: $"Case returned to {targetState} for information. Reason: {request.Reason}",
                fromState: fromState, toState: targetState,
                metadata: new Dictionary<string, string>
                {
                    ["reason"] = request.Reason,
                    ["requestedItems"] = itemsText ?? string.Empty,
                    ["targetStage"] = targetState.ToString()
                }));

            _logger.LogInformation(
                "ReturnForInformation. CaseId={CaseId} TargetState={TargetState} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                targetState.ToString(),
                LoggingHelper.SanitizeLogInput(actorId));

            _ = PersistCaseAsync(c);

            EmitEventFireAndForget(WebhookEventType.ComplianceCaseReturnedForInformation, actorId, caseId,
                new
                {
                    caseId,
                    issuerId        = c.IssuerId,
                    subjectId       = c.SubjectId,
                    reason          = request.Reason,
                    requestedItems  = request.RequestedItems,
                    targetStage     = targetState.ToString(),
                    returnedBy      = actorId,
                    additionalNotes = request.AdditionalNotes
                });

            if (targetState == ComplianceCaseState.EvidencePending)
                EmitEventFireAndForget(WebhookEventType.ComplianceCaseEvidenceRequested, actorId, caseId,
                    new { caseId, issuerId = c.IssuerId, subjectId = c.SubjectId,
                          reason = request.Reason, requestedItems = request.RequestedItems });

            return Task.FromResult(new ReturnForInformationResponse
            {
                Success         = true,
                Case            = c,
                ReturnedToStage = targetState
            });
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
        /// Also appends a <see cref="CaseDeliveryRecord"/> to the case for operational observability.
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

            var eventId = Guid.NewGuid().ToString("N");
            var ev = new WebhookEvent
            {
                Id        = eventId,
                EventType = eventType,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                Actor     = actorId,
                Data      = data
            };

            // Create a pending delivery record on the case before starting delivery
            var deliveryRecord = new CaseDeliveryRecord
            {
                DeliveryId   = Guid.NewGuid().ToString("N"),
                CaseId       = caseId,
                EventId      = eventId,
                EventType    = eventType,
                Outcome      = CaseDeliveryOutcome.Pending,
                AttemptedAt  = _timeProvider.GetUtcNow(),
                AttemptCount = 0
            };

            if (_cases.TryGetValue(caseId, out var c))
                lock (c.DeliveryRecords)
                    c.DeliveryRecords.Add(deliveryRecord);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _webhookService.EmitEventAsync(ev);
                    // Mark delivery as successful
                    deliveryRecord.Outcome      = CaseDeliveryOutcome.Success;
                    deliveryRecord.AttemptCount = 1;
                }
                catch (Exception ex)
                {
                    deliveryRecord.Outcome           = CaseDeliveryOutcome.Failure;
                    deliveryRecord.AttemptCount      = 1;
                    deliveryRecord.IsTransientFailure = true;
                    deliveryRecord.LastErrorSummary  = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message;
                    deliveryRecord.RecommendedAction = "Review webhook endpoint availability and re-trigger the event if necessary.";

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

        // ── GetEvidenceAvailabilityAsync ───────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetEvidenceAvailabilityResponse> GetEvidenceAvailabilityAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new GetEvidenceAvailabilityResponse
                {
                    Success      = false,
                    ErrorCode    = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            CheckAndApplyEvidenceFreshness(c);

            var now          = _timeProvider.GetUtcNow();
            var availability = ComputeEvidenceAvailability(c, now);

            return Task.FromResult(new GetEvidenceAvailabilityResponse { Success = true, Availability = availability });
        }

        // ── GetOrchestrationViewAsync ──────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetOrchestrationViewResponse> GetOrchestrationViewAsync(string caseId, string actorId)
        {
            if (!_cases.TryGetValue(caseId, out var c))
                return Task.FromResult(new GetOrchestrationViewResponse
                {
                    Success      = false,
                    ErrorCode    = ErrorCodes.NOT_FOUND,
                    ErrorMessage = $"Case {caseId} not found"
                });

            CheckAndApplyEvidenceFreshness(c);

            var now          = _timeProvider.GetUtcNow();
            var availability = ComputeEvidenceAvailability(c, now);
            var blockers     = ComputeBlockers(c, now);
            var sla          = c.SlaMetadata;
            var handoff      = c.HandoffStatus;

            var isTerminal = _terminalStates.Contains(c.State);

            // Available transitions from current state
            var transitions = new List<CaseAvailableTransition>();
            if (_validTransitions.TryGetValue(c.State, out var targets))
            {
                foreach (var target in targets)
                {
                    var (label, description) = GetTransitionLabel(c.State, target);
                    bool availableNow       = !isTerminal;
                    string? unavailableReason = null;

                    // Fail-closed: can't approve if there are fail-closed blockers
                    if (target == ComplianceCaseState.Approved)
                    {
                        var failClosedBlockers = blockers.Where(b => b.IsFailClosed).ToList();
                        if (failClosedBlockers.Count > 0)
                        {
                            availableNow      = false;
                            unavailableReason = $"{failClosedBlockers.Count} fail-closed blocker(s) must be resolved first.";
                        }
                    }

                    transitions.Add(new CaseAvailableTransition
                    {
                        ToState           = target,
                        Label             = label,
                        Description       = description,
                        IsAvailableNow    = availableNow,
                        UnavailableReason = unavailableReason
                    });
                }
            }

            // Next actions for the operator
            var nextActions = ComputeNextActions(c, blockers, availability, now);

            var view = new CaseOrchestrationView
            {
                CaseId               = c.CaseId,
                State                = c.State,
                StateDescription     = GetStateDescription(c.State),
                Priority             = c.Priority,
                UrgencyBand          = sla is not null ? ComputeUrgencyBand(sla.ReviewDueAt, now) : CaseUrgencyBand.Normal,
                IsTerminal           = isTerminal,
                IsActive             = !isTerminal,
                EvidenceAvailability = availability,
                ActiveBlockers       = blockers.Where(b => b.IsFailClosed).ToList(),
                CanProceed           = !blockers.Any(b => b.IsFailClosed),
                SlaMetadata          = sla,
                HandoffStatus        = handoff,
                AvailableTransitions = transitions,
                NextActions          = nextActions,
                AssignedReviewerId   = c.AssignedReviewerId,
                AssignedTeamId       = c.AssignedTeamId,
                IssuerId             = c.IssuerId,
                SubjectId            = c.SubjectId,
                CaseType             = c.Type,
                Jurisdiction         = c.Jurisdiction,
                CreatedAt            = c.CreatedAt,
                UpdatedAt            = c.UpdatedAt,
                DecisionCount        = c.DecisionHistory.Count,
                OpenEscalations      = c.Escalations.Count(e => e.Status == EscalationStatus.Open),
                OpenRemediationTasks = c.RemediationTasks.Count(t => t.Status == RemediationTaskStatus.Open),
                ComputedAt           = now
            };

            _logger.LogInformation(
                "GetOrchestrationView. CaseId={CaseId} State={State} EvidenceStatus={EvidenceStatus} Blockers={Blockers} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(caseId),
                c.State.ToString(),
                availability.Status.ToString(),
                blockers.Count(b => b.IsFailClosed),
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new GetOrchestrationViewResponse { Success = true, View = view });
        }

        // ── Private orchestration helpers ──────────────────────────────────────

        /// <summary>Computes the case-level evidence availability summary.</summary>
        private CaseEvidenceAvailabilitySummary ComputeEvidenceAvailability(ComplianceCase c, DateTimeOffset now)
        {
            var summaries     = c.EvidenceSummaries;
            bool bundleExpired = c.EvidenceExpiresAt.HasValue && c.EvidenceExpiresAt.Value < now;

            var validTypes   = new List<string>();
            var staleTypes   = new List<string>();
            var pendingTypes = new List<string>();

            int validCount   = 0;
            int staleCount   = 0;
            int pendingCount = 0;
            int rejectedCount = 0;

            foreach (var ev in summaries)
            {
                switch (ev.Status)
                {
                    case CaseEvidenceStatus.Valid:
                        validCount++;
                        validTypes.Add(ev.EvidenceType);
                        break;
                    case CaseEvidenceStatus.Stale:
                        staleCount++;
                        staleTypes.Add(ev.EvidenceType);
                        break;
                    case CaseEvidenceStatus.Pending:
                    case CaseEvidenceStatus.Missing:
                        pendingCount++;
                        pendingTypes.Add(ev.EvidenceType);
                        break;
                    case CaseEvidenceStatus.Rejected:
                        rejectedCount++;
                        pendingTypes.Add(ev.EvidenceType); // rejected items require re-submission
                        break;
                }
            }

            // Derive overall status
            CaseEvidenceAvailabilityStatus status;
            string operatorSummary;
            string? hint;

            bool hasEvidence = summaries.Count > 0;
            bool allValid    = hasEvidence && staleCount == 0 && pendingCount == 0 && rejectedCount == 0 && !bundleExpired;
            bool allStale    = hasEvidence && staleCount == summaries.Count && !bundleExpired;

            if (!hasEvidence)
            {
                // No evidence captured at all → Unavailable
                status         = CaseEvidenceAvailabilityStatus.Unavailable;
                operatorSummary = "No evidence has been captured for this case. Evidence collection must begin before the case can proceed.";
                hint           = "Attach at least one evidence item to proceed.";
            }
            else if (bundleExpired)
            {
                // Case-level bundle has expired → Stale (entire bundle is stale regardless of item states)
                status         = CaseEvidenceAvailabilityStatus.Stale;
                operatorSummary = $"The evidence bundle expired at {c.EvidenceExpiresAt:O}. All evidence must be refreshed before the case can proceed.";
                hint           = "Refresh the entire evidence bundle. Contact the subject to re-submit up-to-date documentation.";
            }
            else if (allValid)
            {
                status         = CaseEvidenceAvailabilityStatus.Complete;
                operatorSummary = $"All {validCount} evidence item(s) are valid and within their validity period. This case is evidence-complete.";
                hint           = null;
            }
            else if (allStale)
            {
                // All items are individually stale, bundle not yet expired at case level
                status         = CaseEvidenceAvailabilityStatus.Stale;
                operatorSummary = $"All {staleCount} evidence item(s) have expired and must be refreshed before the case can proceed.";
                hint           = $"Refresh or replace the following stale items: {string.Join(", ", staleTypes)}.";
            }
            else
            {
                // Mix of valid + stale/pending/rejected → Partial
                status         = CaseEvidenceAvailabilityStatus.Partial;
                operatorSummary = $"{validCount} valid, {staleCount} stale, {pendingCount} pending/missing, {rejectedCount} rejected evidence item(s).";
                hint           = "Resolve stale, missing, and rejected evidence items to achieve complete evidence coverage.";
            }

            return new CaseEvidenceAvailabilitySummary
            {
                CaseId              = c.CaseId,
                Status              = status,
                TotalEvidenceItems  = summaries.Count,
                ValidItems          = validCount,
                StaleItems          = staleCount,
                PendingItems        = pendingCount,
                RejectedItems       = rejectedCount,
                MissingItems        = 0,  // reserved for future required-evidence-type checking
                IsBundleExpired     = bundleExpired,
                BundleExpiresAt     = c.EvidenceExpiresAt,
                StaleEvidenceTypes  = staleTypes,
                ValidEvidenceTypes  = validTypes,
                PendingOrMissingTypes = pendingTypes,
                OperatorSummary     = operatorSummary,
                RemediationHint     = hint,
                EvaluatedAt         = now
            };
        }

        /// <summary>Returns a human-readable description of a compliance case state.</summary>
        private static string GetStateDescription(ComplianceCaseState state) => state switch
        {
            ComplianceCaseState.Intake         => "Case created and awaiting initial processing.",
            ComplianceCaseState.EvidencePending => "Waiting for required evidence to be submitted.",
            ComplianceCaseState.UnderReview    => "Case is under active compliance review.",
            ComplianceCaseState.Escalated      => "Case has been escalated due to a compliance concern.",
            ComplianceCaseState.Remediating    => "Case has open remediation tasks that must be resolved.",
            ComplianceCaseState.Approved       => "Case has been formally approved — no further action required.",
            ComplianceCaseState.Rejected       => "Case has been formally rejected — closed with rejection outcome.",
            ComplianceCaseState.Stale          => "Case evidence has expired and must be refreshed before review can continue.",
            ComplianceCaseState.Blocked        => "Case is blocked pending manual intervention.",
            _                                  => state.ToString()
        };

        /// <summary>Returns a human-readable label and description for a state transition.</summary>
        private static (string label, string description) GetTransitionLabel(
            ComplianceCaseState from, ComplianceCaseState to) => (from, to) switch
        {
            (_, ComplianceCaseState.EvidencePending) => ("Request Evidence",    "Request additional evidence from the subject."),
            (_, ComplianceCaseState.UnderReview)     => ("Start Review",        "Move the case into active compliance review."),
            (_, ComplianceCaseState.Escalated)       => ("Escalate",            "Escalate the case to senior review."),
            (_, ComplianceCaseState.Remediating)     => ("Request Remediation", "Return the case for remediation tasks."),
            (_, ComplianceCaseState.Approved)        => ("Approve",             "Formally approve the case — terminal action."),
            (_, ComplianceCaseState.Rejected)        => ("Reject",              "Formally reject the case — terminal action."),
            (_, ComplianceCaseState.Blocked)         => ("Block",               "Block the case pending manual intervention."),
            (_, ComplianceCaseState.Stale)           => ("Mark Stale",          "Mark the case as stale due to expired evidence."),
            _                                        => (to.ToString(),          $"Transition to {to}.")
        };

        /// <summary>Computes the ordered list of next recommended actions for an operator.</summary>
        private static List<string> ComputeNextActions(
            ComplianceCase c,
            List<CaseBlocker> blockers,
            CaseEvidenceAvailabilitySummary availability,
            DateTimeOffset now)
        {
            var actions = new List<string>();

            // Assignment
            if (string.IsNullOrWhiteSpace(c.AssignedReviewerId) && string.IsNullOrWhiteSpace(c.AssignedTeamId))
                actions.Add("Assign this case to a reviewer or team.");

            // Evidence
            if (availability.Status == CaseEvidenceAvailabilityStatus.Unavailable)
                actions.Add("Capture at least one evidence item before the case can proceed.");
            else if (availability.Status == CaseEvidenceAvailabilityStatus.Stale)
                actions.Add("Refresh all stale evidence items.");
            else if (availability.Status == CaseEvidenceAvailabilityStatus.Partial)
                actions.Add("Resolve partial evidence: submit missing or re-submit rejected items.");

            // Fail-closed blockers
            foreach (var b in blockers.Where(x => x.IsFailClosed).Take(3))
                if (!string.IsNullOrWhiteSpace(b.RemediationHint))
                    actions.Add(b.RemediationHint!);

            // State-specific guidance
            switch (c.State)
            {
                case ComplianceCaseState.Intake:
                    actions.Add("Transition the case to Evidence Pending or directly to Under Review if evidence is already available.");
                    break;
                case ComplianceCaseState.EvidencePending:
                    if (availability.Status == CaseEvidenceAvailabilityStatus.Complete)
                        actions.Add("Evidence is complete — transition the case to Under Review.");
                    break;
                case ComplianceCaseState.UnderReview:
                    if (!blockers.Any(b => b.IsFailClosed))
                        actions.Add("No blockers found — the case can be approved or escalated.");
                    break;
                case ComplianceCaseState.Escalated:
                    actions.Add("Resolve the open escalation before the case can proceed.");
                    break;
                case ComplianceCaseState.Remediating:
                    int openTasks = c.RemediationTasks.Count(t => t.Status == RemediationTaskStatus.Open);
                    if (openTasks > 0)
                        actions.Add($"Resolve {openTasks} open remediation task(s) to return the case to review.");
                    break;
                case ComplianceCaseState.Approved:
                    if (c.HandoffStatus is null || c.HandoffStatus.Stage == CaseHandoffStage.NotStarted)
                        actions.Add("Initiate the downstream handoff workflow.");
                    break;
                case ComplianceCaseState.Stale:
                    actions.Add("Refresh expired evidence and transition back to Evidence Pending or Under Review.");
                    break;
            }

            return actions;
        }
    }
}
