using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.KycAmlDecisionIngestion;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of <see cref="IKycAmlDecisionIngestionService"/>.
    ///
    /// Provides provider-agnostic ingestion of KYC/AML compliance decisions,
    /// evidence retention with provenance, and fail-closed readiness aggregation.
    ///
    /// Fail-closed rules:
    /// <list type="bullet">
    ///   <item>ProviderUnavailable, Error, or Contradiction → Blocked (hard).</item>
    ///   <item>Rejected or InsufficientData → Blocked (hard).</item>
    ///   <item>Expired evidence → Stale (hard) if a decision is marked expired.</item>
    ///   <item>NeedsReview → PendingReview (blocks Ready; not blocked yet).</item>
    ///   <item>All decisions Approved and no evidence expired → Ready.</item>
    ///   <item>No decisions present for a subject → EvidenceMissing (hard).</item>
    /// </list>
    /// </summary>
    public class KycAmlDecisionIngestionService : IKycAmlDecisionIngestionService
    {
        private readonly ILogger<KycAmlDecisionIngestionService> _logger;
        private readonly TimeProvider _timeProvider;

        // Primary store: decisionId → record
        private readonly ConcurrentDictionary<string, IngestionDecisionRecord> _decisions = new();
        // Idempotency store: idempotencyKey → decisionId
        private readonly ConcurrentDictionary<string, string> _idempotencyIndex = new();
        // Subject index: subjectId → set of decisionIds
        private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _subjectIndex = new();
        // Cohort store: cohortId → CohortMembership
        private readonly ConcurrentDictionary<string, CohortMembership> _cohorts = new();

        /// <summary>
        /// Initialises the service with an optional time provider (defaults to system time).
        /// Injecting a test-controlled <see cref="TimeProvider"/> allows deterministic
        /// freshness and expiry tests without Thread.Sleep.
        /// </summary>
        public KycAmlDecisionIngestionService(
            ILogger<KycAmlDecisionIngestionService> logger,
            TimeProvider? timeProvider = null)
        {
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        // ── IngestDecisionAsync ───────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<IngestProviderDecisionResponse> IngestDecisionAsync(
            IngestProviderDecisionRequest request,
            string actorId,
            string correlationId)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.SubjectId))
                return Task.FromResult(ErrorIngest("MISSING_REQUIRED_FIELD", "SubjectId is required.", correlationId));
            if (string.IsNullOrWhiteSpace(request.ContextId))
                return Task.FromResult(ErrorIngest("MISSING_REQUIRED_FIELD", "ContextId is required.", correlationId));

            var key = BuildIdempotencyKey(request);

            // Idempotent replay
            if (_idempotencyIndex.TryGetValue(key, out var existingId) &&
                _decisions.TryGetValue(existingId, out var existing))
            {
                _logger.LogInformation(
                    "Idempotent replay for key={Key} DecisionId={DecisionId} CorrelationId={CorrelationId}",
                    LoggingHelper.SanitizeLogInput(key),
                    LoggingHelper.SanitizeLogInput(existingId),
                    LoggingHelper.SanitizeLogInput(correlationId));

                existing.IsIdempotentReplay = true;
                return Task.FromResult(new IngestProviderDecisionResponse
                {
                    Success = true,
                    Decision = existing,
                    WasIdempotentReplay = true,
                    CorrelationId = correlationId
                });
            }

            var now = _timeProvider.GetUtcNow();
            var decisionId = Guid.NewGuid().ToString("N");

            // Build evidence artifacts
            var artifacts = new List<IngestionEvidenceArtifact>();
            if (request.EvidenceArtifacts != null)
            {
                foreach (var a in request.EvidenceArtifacts)
                {
                    artifacts.Add(new IngestionEvidenceArtifact
                    {
                        ArtifactId = Guid.NewGuid().ToString("N"),
                        Kind = a.Kind,
                        Label = a.Label ?? string.Empty,
                        ProviderArtifactId = a.ProviderArtifactId,
                        ContentUri = a.ContentUri,
                        ContentHash = a.ContentHash,
                        ReceivedAt = now,
                        ExpiresAt = a.ExpiresAt,
                        IsExpired = a.ExpiresAt.HasValue && a.ExpiresAt.Value < now,
                        Metadata = a.Metadata ?? new Dictionary<string, string>()
                    });
                }
            }

            // Calculate evidence expiry
            DateTimeOffset? evidenceExpiry = null;
            if (request.EvidenceValidityHours is > 0)
                evidenceExpiry = now.AddHours(request.EvidenceValidityHours.Value);

            var record = new IngestionDecisionRecord
            {
                DecisionId = decisionId,
                SubjectId = request.SubjectId,
                ContextId = request.ContextId,
                Kind = request.Kind,
                Provider = request.Provider,
                ProviderReferenceId = request.ProviderReferenceId,
                ProviderRawStatus = request.ProviderRawStatus,
                Status = request.Status,
                IngestedBy = actorId,
                IngestedAt = now,
                ProviderCompletedAt = request.ProviderCompletedAt,
                EvidenceExpiresAt = evidenceExpiry,
                IsEvidenceExpired = evidenceExpiry.HasValue && evidenceExpiry.Value < now,
                ReasonCode = request.ReasonCode,
                Rationale = request.Rationale,
                ConfidenceScore = request.ConfidenceScore,
                Jurisdiction = request.Jurisdiction,
                EvidenceArtifacts = artifacts,
                ReviewerNotes = new List<IngestionReviewerNote>(),
                CorrelationId = correlationId,
                IsIdempotentReplay = false,
                IdempotencyKey = key,
                Timeline = new List<IngestionTimelineEvent>()
            };

            AppendTimelineEvent(record, "DecisionIngested",
                $"Decision ingested from provider={request.Provider}, kind={request.Kind}, status={request.Status}",
                actorId, correlationId, now);

            // Persist
            _decisions[decisionId] = record;
            _idempotencyIndex.TryAdd(key, decisionId);
            var bag = _subjectIndex.GetOrAdd(request.SubjectId, _ => new ConcurrentBag<string>());
            bag.Add(decisionId);

            _logger.LogInformation(
                "Decision ingested. DecisionId={DecisionId} SubjectId={SubjectId} Kind={Kind} Status={Status} Provider={Provider} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(decisionId),
                LoggingHelper.SanitizeLogInput(request.SubjectId),
                request.Kind,
                request.Status,
                request.Provider,
                LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new IngestProviderDecisionResponse
            {
                Success = true,
                Decision = record,
                WasIdempotentReplay = false,
                CorrelationId = correlationId
            });
        }

        // ── GetDecisionAsync ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetIngestionDecisionResponse> GetDecisionAsync(
            string decisionId,
            string correlationId)
        {
            if (string.IsNullOrWhiteSpace(decisionId))
                return Task.FromResult(new GetIngestionDecisionResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_REQUIRED_FIELD",
                    ErrorMessage = "decisionId is required.",
                    CorrelationId = correlationId
                });

            if (!_decisions.TryGetValue(decisionId, out var record))
                return Task.FromResult(new GetIngestionDecisionResponse
                {
                    Success = false,
                    ErrorCode = "INGESTION_DECISION_NOT_FOUND",
                    ErrorMessage = $"Decision '{LoggingHelper.SanitizeLogInput(decisionId)}' not found.",
                    CorrelationId = correlationId
                });

            // Refresh expiry flag
            var now = _timeProvider.GetUtcNow();
            record.IsEvidenceExpired = record.EvidenceExpiresAt.HasValue && record.EvidenceExpiresAt.Value < now;
            foreach (var a in record.EvidenceArtifacts)
                a.IsExpired = a.ExpiresAt.HasValue && a.ExpiresAt.Value < now;

            return Task.FromResult(new GetIngestionDecisionResponse
            {
                Success = true,
                Decision = record,
                CorrelationId = correlationId
            });
        }

        // ── ListSubjectDecisionsAsync ─────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ListSubjectDecisionsResponse> ListSubjectDecisionsAsync(
            string subjectId,
            string correlationId)
        {
            var decisions = GetDecisionsForSubject(subjectId);

            return Task.FromResult(new ListSubjectDecisionsResponse
            {
                Success = true,
                SubjectId = subjectId,
                Decisions = decisions.OrderByDescending(d => d.IngestedAt).ToList(),
                CorrelationId = correlationId
            });
        }

        // ── GetSubjectTimelineAsync ───────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetSubjectTimelineResponse> GetSubjectTimelineAsync(
            string subjectId,
            string correlationId)
        {
            var decisions = GetDecisionsForSubject(subjectId);

            var merged = decisions
                .SelectMany(d => d.Timeline)
                .OrderByDescending(e => e.OccurredAt)
                .ToList();

            return Task.FromResult(new GetSubjectTimelineResponse
            {
                Success = true,
                SubjectId = subjectId,
                Timeline = merged,
                CorrelationId = correlationId
            });
        }

        // ── GetSubjectBlockersAsync ───────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetSubjectBlockersResponse> GetSubjectBlockersAsync(
            string subjectId,
            string correlationId)
        {
            var decisions = GetDecisionsForSubject(subjectId);
            var readiness = ComputeSubjectReadiness(subjectId, decisions);

            return Task.FromResult(new GetSubjectBlockersResponse
            {
                Success = true,
                SubjectId = subjectId,
                HardBlockers = readiness.Blockers,
                Advisories = readiness.Advisories,
                ReadinessState = readiness.ReadinessState,
                CorrelationId = correlationId
            });
        }

        // ── GetSubjectReadinessAsync ──────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetSubjectReadinessResponse> GetSubjectReadinessAsync(
            string subjectId,
            string correlationId)
        {
            var decisions = GetDecisionsForSubject(subjectId);
            var readiness = ComputeSubjectReadiness(subjectId, decisions);

            return Task.FromResult(new GetSubjectReadinessResponse
            {
                Success = true,
                Readiness = readiness,
                CorrelationId = correlationId
            });
        }

        // ── UpsertCohortAsync ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<UpsertCohortResponse> UpsertCohortAsync(
            UpsertCohortRequest request,
            string correlationId)
        {
            if (string.IsNullOrWhiteSpace(request.CohortId))
                return Task.FromResult(new UpsertCohortResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_REQUIRED_FIELD",
                    ErrorMessage = "CohortId is required.",
                    CorrelationId = correlationId
                });

            var membership = _cohorts.GetOrAdd(request.CohortId, _ => new CohortMembership
            {
                CohortId = request.CohortId,
                CohortName = request.CohortName
            });

            if (request.CohortName != null)
                membership.CohortName = request.CohortName;

            foreach (var sid in request.SubjectIds)
                if (!string.IsNullOrWhiteSpace(sid))
                    membership.SubjectIds.Add(sid);

            _logger.LogInformation(
                "Cohort upserted. CohortId={CohortId} SubjectCount={SubjectCount} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(request.CohortId),
                membership.SubjectIds.Count,
                LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new UpsertCohortResponse
            {
                Success = true,
                CohortId = request.CohortId,
                SubjectCount = membership.SubjectIds.Count,
                CorrelationId = correlationId
            });
        }

        // ── GetCohortReadinessAsync ───────────────────────────────────────────

        /// <inheritdoc/>
        public Task<GetCohortReadinessResponse> GetCohortReadinessAsync(
            string cohortId,
            string correlationId)
        {
            if (!_cohorts.TryGetValue(cohortId, out var membership))
                return Task.FromResult(new GetCohortReadinessResponse
                {
                    Success = false,
                    ErrorCode = "INGESTION_COHORT_NOT_FOUND",
                    ErrorMessage = $"Cohort '{LoggingHelper.SanitizeLogInput(cohortId)}' not found.",
                    CorrelationId = correlationId
                });

            var now = _timeProvider.GetUtcNow();
            var subjectReadinesses = new List<SubjectIngestionReadiness>();

            foreach (var subjectId in membership.SubjectIds)
            {
                var decisions = GetDecisionsForSubject(subjectId);
                var readiness = ComputeSubjectReadiness(subjectId, decisions);
                subjectReadinesses.Add(readiness);
            }

            // Aggregate: most severe state across all subjects (fail-closed)
            var overallState = AggregateReadinessState(subjectReadinesses.Select(r => r.ReadinessState));

            // Collect cohort-level blockers from blocked subjects
            var cohortBlockers = subjectReadinesses
                .SelectMany(r => r.Blockers)
                .GroupBy(b => b.Code)
                .Select(g => g.OrderByDescending(b => (int)b.Severity).First())
                .ToList();

            var countByState = subjectReadinesses
                .GroupBy(r => r.ReadinessState)
                .ToDictionary(g => g.Key, g => g.Count());

            var summary = BuildCohortSummary(overallState, subjectReadinesses.Count, countByState);

            return Task.FromResult(new GetCohortReadinessResponse
            {
                Success = true,
                CohortReadiness = new CohortIngestionReadiness
                {
                    CohortId = cohortId,
                    CohortName = membership.CohortName,
                    OverallReadinessState = overallState,
                    ReadinessSummary = summary,
                    TotalSubjects = subjectReadinesses.Count,
                    SubjectCountByState = countByState,
                    SubjectReadiness = subjectReadinesses,
                    CohortBlockers = cohortBlockers,
                    ComputedAt = now
                },
                CorrelationId = correlationId
            });
        }

        // ── AppendReviewerNoteAsync ───────────────────────────────────────────

        /// <inheritdoc/>
        public Task<AppendIngestionReviewerNoteResponse> AppendReviewerNoteAsync(
            string decisionId,
            AppendIngestionReviewerNoteRequest request,
            string actorId,
            string correlationId)
        {
            if (string.IsNullOrWhiteSpace(decisionId))
                return Task.FromResult(new AppendIngestionReviewerNoteResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_REQUIRED_FIELD",
                    ErrorMessage = "decisionId is required.",
                    CorrelationId = correlationId
                });

            if (string.IsNullOrWhiteSpace(request.Content))
                return Task.FromResult(new AppendIngestionReviewerNoteResponse
                {
                    Success = false,
                    ErrorCode = "MISSING_REQUIRED_FIELD",
                    ErrorMessage = "Note content is required.",
                    CorrelationId = correlationId
                });

            if (!_decisions.TryGetValue(decisionId, out var record))
                return Task.FromResult(new AppendIngestionReviewerNoteResponse
                {
                    Success = false,
                    ErrorCode = "INGESTION_DECISION_NOT_FOUND",
                    ErrorMessage = $"Decision '{LoggingHelper.SanitizeLogInput(decisionId)}' not found.",
                    CorrelationId = correlationId
                });

            var note = new IngestionReviewerNote
            {
                NoteId = Guid.NewGuid().ToString("N"),
                ActorId = actorId,
                Content = request.Content,
                AppendedAt = _timeProvider.GetUtcNow(),
                EvidenceReferences = request.EvidenceReferences ?? new Dictionary<string, string>(),
                CorrelationId = correlationId
            };

            record.ReviewerNotes.Add(note);
            AppendTimelineEvent(record, "ReviewerNoteAdded",
                $"Reviewer note appended by {LoggingHelper.SanitizeLogInput(actorId)}",
                actorId, correlationId, note.AppendedAt);

            return Task.FromResult(new AppendIngestionReviewerNoteResponse
            {
                Success = true,
                Note = note,
                CorrelationId = correlationId
            });
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private List<IngestionDecisionRecord> GetDecisionsForSubject(string subjectId)
        {
            if (!_subjectIndex.TryGetValue(subjectId, out var bag))
                return new List<IngestionDecisionRecord>();

            var now = _timeProvider.GetUtcNow();
            var result = new List<IngestionDecisionRecord>();
            foreach (var id in bag)
            {
                if (!_decisions.TryGetValue(id, out var d))
                    continue;
                // Refresh expiry flags
                d.IsEvidenceExpired = d.EvidenceExpiresAt.HasValue && d.EvidenceExpiresAt.Value < now;
                foreach (var a in d.EvidenceArtifacts)
                    a.IsExpired = a.ExpiresAt.HasValue && a.ExpiresAt.Value < now;
                result.Add(d);
            }
            return result;
        }

        /// <summary>
        /// Applies fail-closed readiness rules to a subject's decision set.
        /// Rules evaluated in priority order (most severe first):
        /// <list type="number">
        ///   <item>No decisions → EvidenceMissing (blocked).</item>
        ///   <item>ProviderUnavailable → Blocked.</item>
        ///   <item>Error → Blocked.</item>
        ///   <item>Contradiction → Blocked.</item>
        ///   <item>Rejected or InsufficientData → Blocked.</item>
        ///   <item>Expired evidence (IsEvidenceExpired) → Stale (blocked).</item>
        ///   <item>NeedsReview → PendingReview.</item>
        ///   <item>All Approved, no expired evidence → Ready.</item>
        /// </list>
        /// </summary>
        private SubjectIngestionReadiness ComputeSubjectReadiness(
            string subjectId,
            List<IngestionDecisionRecord> decisions)
        {
            var now = _timeProvider.GetUtcNow();
            var blockers = new List<IngestionBlocker>();
            var advisories = new List<IngestionBlocker>();

            if (decisions.Count == 0)
            {
                blockers.Add(new IngestionBlocker
                {
                    Code = "EVIDENCE_MISSING",
                    Title = "No compliance decisions on record",
                    Description = "No KYC/AML decisions have been ingested for this subject. Launch is blocked until required checks are completed.",
                    Severity = IngestionBlockerSeverity.Critical,
                    IsHardBlocker = true,
                    RemediationHint = "Ingest at least one identity (KYC) and one AML/sanctions screening decision for this subject."
                });
                return BuildReadinessSummary(subjectId, IngestionReadinessState.EvidenceMissing, blockers, advisories, decisions, now);
            }

            // ── Use only the MOST RECENT decision per Kind ──────────────────
            // Earlier decisions (e.g. NeedsReview superseded by Approved via rescreen) are
            // retained in the full audit trail but do NOT affect current readiness. Only
            // the newest decision per kind governs the compliance posture.
            var latestPerKind = decisions
                .GroupBy(d => d.Kind)
                .Select(g => g.OrderByDescending(d => d.IngestedAt).First())
                .ToList();

            // Check for provider unavailable (fail-closed)
            foreach (var d in latestPerKind.Where(d => d.Status == NormalizedIngestionStatus.ProviderUnavailable))
            {
                blockers.Add(new IngestionBlocker
                {
                    Code = "PROVIDER_UNAVAILABLE",
                    Title = "Compliance provider unavailable",
                    Description = $"The {d.Kind} check via {d.Provider} could not be completed because the provider was unavailable. The system is fail-closed.",
                    Severity = IngestionBlockerSeverity.Critical,
                    SourceDecisionId = d.DecisionId,
                    SourceDecisionKind = d.Kind,
                    IsHardBlocker = true,
                    RemediationHint = "Retry the check once the provider is available, or ingest a decision from an alternative provider."
                });
            }

            // Check for errors
            foreach (var d in latestPerKind.Where(d => d.Status == NormalizedIngestionStatus.Error))
            {
                blockers.Add(new IngestionBlocker
                {
                    Code = "CHECK_ERROR",
                    Title = "Compliance check error",
                    Description = $"An error occurred during the {d.Kind} check via {d.Provider}. Reason: {d.ReasonCode ?? "unknown"}.",
                    Severity = IngestionBlockerSeverity.Critical,
                    SourceDecisionId = d.DecisionId,
                    SourceDecisionKind = d.Kind,
                    IsHardBlocker = true,
                    RemediationHint = "Review the error details and re-run the check or ingest a corrected decision."
                });
            }

            // Check for contradictions within EACH kind: if the latest decision for a kind is
            // Approved but there also exist more-recent Rejected decisions for the same kind
            // (from a different provider or assessment), that is a contradiction. We check whether
            // for the same kind there are BOTH Approved and Rejected decisions when the latest is
            // not a single clear terminal state. Specifically: a contradiction exists when the full
            // history for a kind contains both Approved and Rejected terminal states AND the most
            // recent is one of them (not an intermediate superseding the other).
            // Reuse the pre-computed latestPerKind dictionary for O(1) lookups.
            var latestByKind = latestPerKind.ToDictionary(d => d.Kind);
            var terminalByKind = decisions
                .Where(d => d.Status == NormalizedIngestionStatus.Approved || d.Status == NormalizedIngestionStatus.Rejected)
                .GroupBy(d => d.Kind)
                .Where(g =>
                {
                    // Contradiction: both Approved and Rejected exist in the history for this kind,
                    // AND the most recent decision is also terminal (not a clear resolution).
                    var statuses = g.Select(d => d.Status).Distinct().ToList();
                    if (statuses.Count <= 1) return false; // all same terminal state → no contradiction

                    // Reuse latestByKind lookup instead of re-sorting decisions
                    if (!latestByKind.TryGetValue(g.Key, out var mostRecent)) return false;
                    return mostRecent.Status == NormalizedIngestionStatus.Approved
                           || mostRecent.Status == NormalizedIngestionStatus.Rejected;
                });

            foreach (var group in terminalByKind)
            {
                blockers.Add(new IngestionBlocker
                {
                    Code = "CONTRADICTORY_DECISIONS",
                    Title = "Contradictory compliance decisions",
                    Description = $"Contradictory {group.Key} decisions detected: both Approved and Rejected outcomes exist. Manual review is required.",
                    Severity = IngestionBlockerSeverity.Critical,
                    SourceDecisionKind = group.Key,
                    IsHardBlocker = true,
                    RemediationHint = "Resolve the contradiction by submitting a superseding decision after manual review."
                });
            }

            // Check for rejected or insufficient data (latest decision per kind only)
            foreach (var d in latestPerKind.Where(d =>
                d.Status == NormalizedIngestionStatus.Rejected ||
                d.Status == NormalizedIngestionStatus.InsufficientData))
            {
                var isInsufficient = d.Status == NormalizedIngestionStatus.InsufficientData;
                blockers.Add(new IngestionBlocker
                {
                    Code = isInsufficient ? "INSUFFICIENT_DATA" : "CHECK_REJECTED",
                    Title = isInsufficient ? "Insufficient data for compliance check" : "Compliance check rejected",
                    Description = $"The {d.Kind} check via {d.Provider} returned {d.Status}. Reason: {d.ReasonCode ?? "none provided"}.",
                    Severity = IngestionBlockerSeverity.High,
                    SourceDecisionId = d.DecisionId,
                    SourceDecisionKind = d.Kind,
                    IsHardBlocker = true,
                    RemediationHint = isInsufficient
                        ? "Provide additional subject data and re-run the check."
                        : "Review the rejection reason and determine if remediation or escalation is required."
                });
            }

            // Check for expired evidence (latest per kind)
            foreach (var d in latestPerKind.Where(d => d.IsEvidenceExpired))
            {
                blockers.Add(new IngestionBlocker
                {
                    Code = "EVIDENCE_EXPIRED",
                    Title = "Evidence validity window expired",
                    Description = $"The evidence for the {d.Kind} check (decision {d.DecisionId}) expired on {d.EvidenceExpiresAt:u}.",
                    Severity = IngestionBlockerSeverity.High,
                    SourceDecisionId = d.DecisionId,
                    SourceDecisionKind = d.Kind,
                    IsHardBlocker = true,
                    RemediationHint = "Request a new check from the provider to refresh the evidence validity window."
                });
            }

            // Individual expired artifacts (on otherwise-valid latest decisions)
            foreach (var d in latestPerKind.Where(d => !d.IsEvidenceExpired))
            {
                foreach (var a in d.EvidenceArtifacts.Where(a => a.IsExpired))
                {
                    blockers.Add(new IngestionBlocker
                    {
                        Code = "ARTIFACT_EXPIRED",
                        Title = $"Evidence artefact expired: {a.Label}",
                        Description = $"The artefact '{a.Label}' (kind={a.Kind}) for decision {d.DecisionId} expired on {a.ExpiresAt:u}.",
                        Severity = IngestionBlockerSeverity.High,
                        SourceDecisionId = d.DecisionId,
                        SourceDecisionKind = d.Kind,
                        IsHardBlocker = true,
                        RemediationHint = "Provide a renewed version of this artefact."
                    });
                }
            }

            // Pending checks (latest per kind)
            foreach (var d in latestPerKind.Where(d => d.Status == NormalizedIngestionStatus.Pending))
            {
                advisories.Add(new IngestionBlocker
                {
                    Code = "CHECK_PENDING",
                    Title = "Compliance check in progress",
                    Description = $"The {d.Kind} check via {d.Provider} is still in progress. Readiness cannot be confirmed until it completes.",
                    Severity = IngestionBlockerSeverity.Medium,
                    SourceDecisionId = d.DecisionId,
                    SourceDecisionKind = d.Kind,
                    IsHardBlocker = false,
                    RemediationHint = "Wait for the check to complete or poll the provider for an updated status."
                });
            }

            // NeedsReview checks (latest per kind)
            foreach (var d in latestPerKind.Where(d => d.Status == NormalizedIngestionStatus.NeedsReview))
            {
                advisories.Add(new IngestionBlocker
                {
                    Code = "MANUAL_REVIEW_REQUIRED",
                    Title = "Manual compliance review required",
                    Description = $"The {d.Kind} check via {d.Provider} requires manual review before a final decision can be made.",
                    Severity = IngestionBlockerSeverity.High,
                    SourceDecisionId = d.DecisionId,
                    SourceDecisionKind = d.Kind,
                    IsHardBlocker = false,
                    RemediationHint = "Assign a reviewer and record their decision via the reviewer note endpoint."
                });
            }

            // Derive overall state
            IngestionReadinessState state;

            if (blockers.Any(b => b.Code == "PROVIDER_UNAVAILABLE") ||
                blockers.Any(b => b.Code == "CHECK_ERROR") ||
                blockers.Any(b => b.Code == "CONTRADICTORY_DECISIONS") ||
                blockers.Any(b => b.Code == "CHECK_REJECTED") ||
                blockers.Any(b => b.Code == "INSUFFICIENT_DATA") ||
                blockers.Any(b => b.Code == "EVIDENCE_MISSING"))
            {
                state = IngestionReadinessState.Blocked;
            }
            else if (blockers.Any(b => b.Code == "EVIDENCE_EXPIRED" || b.Code == "ARTIFACT_EXPIRED"))
            {
                state = IngestionReadinessState.Stale;
            }
            else if (advisories.Any(a => a.Code == "MANUAL_REVIEW_REQUIRED" || a.Code == "CHECK_PENDING"))
            {
                state = IngestionReadinessState.PendingReview;
            }
            else if (latestPerKind.All(d => d.Status == NormalizedIngestionStatus.Approved))
            {
                // All latest decisions per kind are Approved → Ready
                state = IngestionReadinessState.Ready;
            }
            else
            {
                // Mixed non-blocking states → AtRisk
                state = IngestionReadinessState.AtRisk;
            }

            return BuildReadinessSummary(subjectId, state, blockers, advisories, decisions, now);
        }

        private SubjectIngestionReadiness BuildReadinessSummary(
            string subjectId,
            IngestionReadinessState state,
            List<IngestionBlocker> blockers,
            List<IngestionBlocker> advisories,
            List<IngestionDecisionRecord> decisions,
            DateTimeOffset now)
        {
            // Latest decision per kind
            var checkSummary = decisions
                .GroupBy(d => d.Kind)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(d => d.IngestedAt).First().Status);

            // Earliest expiry
            var expiries = decisions
                .Where(d => d.EvidenceExpiresAt.HasValue)
                .Select(d => d.EvidenceExpiresAt!.Value)
                .ToList();
            DateTimeOffset? earliest = expiries.Any() ? expiries.Min() : null;

            var summary = state switch
            {
                IngestionReadinessState.Ready => "All compliance checks are approved. Launch readiness confirmed.",
                IngestionReadinessState.Blocked => $"Launch is blocked. {blockers.Count} hard blocker(s) require resolution.",
                IngestionReadinessState.PendingReview => "Manual review is in progress. Readiness pending reviewer outcome.",
                IngestionReadinessState.Stale => "Evidence has expired. Checks must be renewed before launch.",
                IngestionReadinessState.EvidenceMissing => "Required compliance evidence is missing. Complete KYC/AML checks to proceed.",
                IngestionReadinessState.AtRisk => "Non-critical issues are present. Review advisories before proceeding.",
                _ => "Readiness status is unknown."
            };

            return new SubjectIngestionReadiness
            {
                SubjectId = subjectId,
                ReadinessState = state,
                ReadinessSummary = summary,
                Blockers = blockers,
                Advisories = advisories,
                CheckSummary = checkSummary,
                EarliestEvidenceExpiry = earliest,
                HasExpiredEvidence = decisions.Any(d => d.IsEvidenceExpired) ||
                                     decisions.Any(d => d.EvidenceArtifacts.Any(a => a.IsExpired)),
                ComputedAt = now
            };
        }

        private static IngestionReadinessState AggregateReadinessState(
            IEnumerable<IngestionReadinessState> states)
        {
            // Severity order (highest first): Blocked > Stale > EvidenceMissing > PendingReview > AtRisk > Unknown > Ready
            var priority = new[]
            {
                IngestionReadinessState.Blocked,
                IngestionReadinessState.Stale,
                IngestionReadinessState.EvidenceMissing,
                IngestionReadinessState.PendingReview,
                IngestionReadinessState.AtRisk,
                IngestionReadinessState.Unknown,
                IngestionReadinessState.Ready
            };

            var stateSet = new HashSet<IngestionReadinessState>(states);
            foreach (var p in priority)
                if (stateSet.Contains(p))
                    return p;

            return IngestionReadinessState.Ready;
        }

        private static string BuildCohortSummary(
            IngestionReadinessState overall,
            int total,
            Dictionary<IngestionReadinessState, int> counts)
        {
            var ready = counts.GetValueOrDefault(IngestionReadinessState.Ready, 0);
            var blocked = counts.GetValueOrDefault(IngestionReadinessState.Blocked, 0) +
                          counts.GetValueOrDefault(IngestionReadinessState.EvidenceMissing, 0) +
                          counts.GetValueOrDefault(IngestionReadinessState.Stale, 0);
            var pending = counts.GetValueOrDefault(IngestionReadinessState.PendingReview, 0);

            return overall switch
            {
                IngestionReadinessState.Ready =>
                    $"All {total} subject(s) are ready for launch.",
                IngestionReadinessState.Blocked =>
                    $"Cohort blocked: {blocked}/{total} subject(s) have hard blockers. {ready}/{total} ready.",
                IngestionReadinessState.Stale =>
                    $"Cohort stale: evidence has expired for {blocked}/{total} subject(s).",
                IngestionReadinessState.PendingReview =>
                    $"Cohort pending: {pending}/{total} subject(s) require manual review. {ready}/{total} ready.",
                _ =>
                    $"Cohort status: {ready}/{total} ready, {blocked}/{total} blocked, {pending}/{total} pending review."
            };
        }

        private static void AppendTimelineEvent(
            IngestionDecisionRecord record,
            string eventType,
            string description,
            string actor,
            string correlationId,
            DateTimeOffset occurredAt,
            Dictionary<string, string>? metadata = null)
        {
            record.Timeline.Add(new IngestionTimelineEvent
            {
                OccurredAt = occurredAt,
                EventType = eventType,
                Description = description,
                StatusSnapshot = record.Status,
                Actor = actor,
                CorrelationId = correlationId,
                Metadata = metadata ?? new Dictionary<string, string>()
            });
        }

        private static string BuildIdempotencyKey(IngestProviderDecisionRequest request)
        {
            return string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? $"{request.SubjectId}:{request.ContextId}:{request.Kind}:{request.Provider}:{request.ProviderReferenceId}"
                : request.IdempotencyKey;
        }

        private static IngestProviderDecisionResponse ErrorIngest(
            string errorCode,
            string errorMessage,
            string correlationId)
        {
            return new IngestProviderDecisionResponse
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                CorrelationId = correlationId
            };
        }
    }
}
