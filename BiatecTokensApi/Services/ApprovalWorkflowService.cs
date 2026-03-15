using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.ApprovalWorkflow;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Repository-backed implementation of the enterprise approval workflow service.
    ///
    /// Provides deterministic posture calculation, stage decision recording,
    /// evidence readiness evaluation synthesised from stage states, and
    /// tamper-evident audit history for multi-stage enterprise release pipelines.
    ///
    /// Posture derivation is deterministic and testable:
    ///   1. Any Rejected stage → BlockedByStageDecision
    ///   2. Any release-blocking Missing evidence → BlockedByMissingEvidence
    ///   3. Any release-blocking Stale evidence → BlockedByStaleEvidence
    ///   4. Any release-blocking ConfigurationBlocked evidence → ConfigurationBlocked
    ///   5. All 5 stages Approved → LaunchReady
    ///   6. Otherwise → BlockedByStageDecision (stages still pending/blocked)
    /// </summary>
    public class ApprovalWorkflowService : IApprovalWorkflowService
    {
        private readonly IApprovalWorkflowRepository _repository;
        private readonly ILogger<ApprovalWorkflowService> _logger;

        // Ordered list of stages used for sequential owner-domain derivation
        private static readonly ApprovalStageType[] _stageOrder =
        {
            ApprovalStageType.Compliance,
            ApprovalStageType.Legal,
            ApprovalStageType.Procurement,
            ApprovalStageType.Executive,
            ApprovalStageType.SharedOperations
        };

        // Evidence freshness window: approved stage evidence expires after 30 days
        private static readonly TimeSpan _evidenceFreshnessWindow = TimeSpan.FromDays(30);

        /// <summary>
        /// Initializes a new instance of <see cref="ApprovalWorkflowService"/>.
        /// </summary>
        public ApprovalWorkflowService(
            IApprovalWorkflowRepository repository,
            ILogger<ApprovalWorkflowService> logger)
        {
            _repository = repository;
            _logger     = logger;
        }

        // ── GetApprovalWorkflowStateAsync ──────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ApprovalWorkflowStateResponse> GetApprovalWorkflowStateAsync(
            string releasePackageId, string actorId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(releasePackageId))
                return FailState(ErrorCodes.MISSING_REQUIRED_FIELD, "ReleasePackageId is required.", correlationId);
            if (string.IsNullOrWhiteSpace(actorId))
                return FailState(ErrorCodes.MISSING_REQUIRED_FIELD, "ActorId is required.", correlationId);

            _logger.LogInformation(
                "ApprovalWorkflowService.GetState. PackageId={PackageId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(releasePackageId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            List<PersistedApprovalStageDecision> decisions =
                await _repository.GetStageDecisionsForPackageAsync(releasePackageId);

            List<ApprovalStageRecord> stages = BuildStageRecords(decisions);
            List<EvidenceReadinessItem> evidence = BuildEvidenceItems(stages);

            (ReleasePosture posture, string rationale) = DerivePosture(stages, evidence);
            ApprovalOwnerDomain owner = DeriveOwnerDomain(stages, evidence);
            List<ApprovalBlocker> blockers = BuildActiveBlockers(stages, evidence, posture);

            return new ApprovalWorkflowStateResponse
            {
                Success              = true,
                ReleasePackageId     = releasePackageId,
                Stages               = stages,
                ReleasePosture       = posture,
                ActiveOwnerDomain    = owner,
                ActiveBlockers       = blockers,
                EvidenceSummary      = evidence,
                PostureCalculatedAt  = DateTime.UtcNow,
                CorrelationId        = correlationId,
                PostureRationale     = rationale
            };
        }

        // ── SubmitStageDecisionAsync ───────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<SubmitStageDecisionResponse> SubmitStageDecisionAsync(
            string releasePackageId, SubmitStageDecisionRequest request,
            string actorId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(releasePackageId))
                return FailDecision(ErrorCodes.MISSING_REQUIRED_FIELD, "ReleasePackageId is required.", correlationId);
            if (string.IsNullOrWhiteSpace(actorId))
                return FailDecision(ErrorCodes.MISSING_REQUIRED_FIELD, "ActorId is required.", correlationId);
            if (request == null)
                return FailDecision(ErrorCodes.MISSING_REQUIRED_FIELD, "Request body is required.", correlationId);

            if (request.Decision == ApprovalDecisionStatus.Pending)
                return FailDecision(
                    ErrorCodes.INVALID_REQUEST,
                    "Decision cannot be Pending. Submit Approved, Rejected, Blocked, or NeedsFollowUp.",
                    correlationId,
                    "Choose a non-Pending decision status.");

            bool requiresNote = request.Decision == ApprovalDecisionStatus.Rejected
                             || request.Decision == ApprovalDecisionStatus.Blocked
                             || request.Decision == ApprovalDecisionStatus.NeedsFollowUp;

            if (requiresNote && string.IsNullOrWhiteSpace(request.Note))
                return FailDecision(
                    ErrorCodes.MISSING_REQUIRED_FIELD,
                    $"A Note is required when Decision is {request.Decision}.",
                    correlationId,
                    $"Provide a rationale in the Note field when submitting a {request.Decision} decision.");

            _logger.LogInformation(
                "ApprovalWorkflowService.SubmitDecision. PackageId={PackageId} Stage={Stage} Decision={Decision} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(releasePackageId),
                request.StageType,
                request.Decision,
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            // Load existing decisions to determine previous status
            List<PersistedApprovalStageDecision> existing =
                await _repository.GetStageDecisionsForPackageAsync(releasePackageId);

            PersistedApprovalStageDecision? prev = existing
                .Where(d => d.StageType == request.StageType)
                .OrderByDescending(d => d.Timestamp)
                .FirstOrDefault();

            string decisionId = Guid.NewGuid().ToString();

            PersistedApprovalStageDecision record = new()
            {
                PackageId     = releasePackageId,
                StageType     = request.StageType,
                Status        = request.Decision,
                ActorId       = actorId,
                Note          = request.Note ?? string.Empty,
                Timestamp     = DateTime.UtcNow,
                CorrelationId = correlationId,
                DecisionId    = decisionId
            };

            await _repository.SaveStageDecisionAsync(record);

            // Append audit event
            await _repository.AppendAuditEventAsync(releasePackageId, new ApprovalAuditEvent
            {
                EventType          = "StageDecisionSubmitted",
                ReleasePackageId   = releasePackageId,
                StageType          = request.StageType,
                ActorId            = actorId,
                ActorDisplayName   = actorId,
                Description        = $"Stage {request.StageType} decision: {request.Decision}",
                PreviousStatus     = prev != null ? prev.Status : ApprovalDecisionStatus.Pending,
                NewStatus          = request.Decision,
                Note               = request.Note ?? string.Empty,
                CorrelationId      = correlationId,
                Metadata           = new Dictionary<string, string> { ["DecisionId"] = decisionId }
            });

            // Rebuild state to return updated stage and posture
            List<PersistedApprovalStageDecision> allDecisions =
                await _repository.GetStageDecisionsForPackageAsync(releasePackageId);

            List<ApprovalStageRecord> stages   = BuildStageRecords(allDecisions);
            List<EvidenceReadinessItem> evidence = BuildEvidenceItems(stages);
            (ReleasePosture posture, _)         = DerivePosture(stages, evidence);

            ApprovalStageRecord updatedStage = stages.First(s => s.StageType == request.StageType);

            return new SubmitStageDecisionResponse
            {
                Success           = true,
                DecisionId        = decisionId,
                UpdatedStage      = updatedStage,
                NewReleasePosture = posture,
                CorrelationId     = correlationId
            };
        }

        // ── GetReleaseEvidenceSummaryAsync ─────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ReleaseEvidenceSummaryResponse> GetReleaseEvidenceSummaryAsync(
            string releasePackageId, string actorId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(releasePackageId))
                return FailEvidence(ErrorCodes.MISSING_REQUIRED_FIELD, "ReleasePackageId is required.", correlationId);
            if (string.IsNullOrWhiteSpace(actorId))
                return FailEvidence(ErrorCodes.MISSING_REQUIRED_FIELD, "ActorId is required.", correlationId);

            _logger.LogInformation(
                "ApprovalWorkflowService.GetEvidenceSummary. PackageId={PackageId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(releasePackageId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            List<PersistedApprovalStageDecision> decisions =
                await _repository.GetStageDecisionsForPackageAsync(releasePackageId);

            List<ApprovalStageRecord> stages   = BuildStageRecords(decisions);
            List<EvidenceReadinessItem> items  = BuildEvidenceItems(stages);

            int freshCount    = items.Count(i => i.ReadinessCategory == EvidenceReadinessCategory.Fresh);
            int staleCount    = items.Count(i => i.ReadinessCategory == EvidenceReadinessCategory.Stale);
            int missingCount  = items.Count(i => i.ReadinessCategory == EvidenceReadinessCategory.Missing);
            int configCount   = items.Count(i => i.ReadinessCategory == EvidenceReadinessCategory.ConfigurationBlocked);

            EvidenceReadinessCategory overall = DeriveOverallReadiness(items);

            return new ReleaseEvidenceSummaryResponse
            {
                Success                  = true,
                ReleasePackageId         = releasePackageId,
                EvidenceItems            = items,
                OverallReadiness         = overall,
                FreshCount               = freshCount,
                StaleCount               = staleCount,
                MissingCount             = missingCount,
                ConfigurationBlockedCount = configCount,
                CorrelationId            = correlationId
            };
        }

        // ── GetApprovalAuditHistoryAsync ───────────────────────────────────────

        /// <inheritdoc/>
        public async Task<ApprovalAuditHistoryResponse> GetApprovalAuditHistoryAsync(
            string releasePackageId, string actorId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(releasePackageId))
                return FailAudit(ErrorCodes.MISSING_REQUIRED_FIELD, "ReleasePackageId is required.", correlationId);
            if (string.IsNullOrWhiteSpace(actorId))
                return FailAudit(ErrorCodes.MISSING_REQUIRED_FIELD, "ActorId is required.", correlationId);

            _logger.LogInformation(
                "ApprovalWorkflowService.GetAuditHistory. PackageId={PackageId} Actor={Actor} CorrelationId={CorrelationId}",
                LoggingHelper.SanitizeLogInput(releasePackageId),
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            List<ApprovalAuditEvent> events =
                await _repository.GetAuditEventsAsync(releasePackageId, maxCount: 100);

            return new ApprovalAuditHistoryResponse
            {
                Success          = true,
                ReleasePackageId = releasePackageId,
                Events           = events,
                TotalCount       = events.Count,
                CorrelationId    = correlationId
            };
        }

        // ── Private Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Builds the canonical list of 5 stage records from persisted decisions.
        /// Only the latest decision per stage is considered the effective status.
        /// </summary>
        private static List<ApprovalStageRecord> BuildStageRecords(
            List<PersistedApprovalStageDecision> decisions)
        {
            var result = new List<ApprovalStageRecord>(5);

            foreach (ApprovalStageType stage in _stageOrder)
            {
                PersistedApprovalStageDecision? latest = decisions
                    .Where(d => d.StageType == stage)
                    .OrderByDescending(d => d.Timestamp)
                    .FirstOrDefault();

                ApprovalOwnerDomain ownerDomain = stage switch
                {
                    ApprovalStageType.Compliance      => ApprovalOwnerDomain.Compliance,
                    ApprovalStageType.Legal           => ApprovalOwnerDomain.Legal,
                    ApprovalStageType.Procurement     => ApprovalOwnerDomain.Procurement,
                    ApprovalStageType.Executive       => ApprovalOwnerDomain.Executive,
                    ApprovalStageType.SharedOperations => ApprovalOwnerDomain.SharedOperations,
                    _                                 => ApprovalOwnerDomain.None
                };

                result.Add(new ApprovalStageRecord
                {
                    StageType    = stage,
                    Status       = latest?.Status ?? ApprovalDecisionStatus.Pending,
                    OwnerDomain  = ownerDomain,
                    DecidedBy    = latest?.ActorId,
                    DecidedAt    = latest?.Timestamp,
                    Note         = latest?.Note,
                    UpdatedAt    = latest?.Timestamp ?? DateTime.UtcNow
                });
            }

            return result;
        }

        /// <summary>
        /// Synthesises evidence readiness items from the current stage records.
        /// Each stage maps to one evidence item; freshness is derived from stage status and age.
        /// </summary>
        private static List<EvidenceReadinessItem> BuildEvidenceItems(List<ApprovalStageRecord> stages)
        {
            var items = new List<EvidenceReadinessItem>(5);
            DateTime now = DateTime.UtcNow;

            foreach (ApprovalStageRecord stage in stages)
            {
                (string name, string category, string description) = stage.StageType switch
                {
                    ApprovalStageType.Compliance =>
                        ("KYC/AML Identity Verification", "Compliance",
                         "Anti-money-laundering and know-your-customer identity check sign-off."),
                    ApprovalStageType.Legal =>
                        ("Legal Review Sign-off", "Legal",
                         "Legal team review of contracts, IP rights, and jurisdictional requirements."),
                    ApprovalStageType.Procurement =>
                        ("Procurement Approval", "Procurement",
                         "Procurement team approval for associated vendor costs and contracts."),
                    ApprovalStageType.Executive =>
                        ("Executive Sponsor Sign-off", "Executive",
                         "Executive sponsor approval authorising the release to proceed."),
                    ApprovalStageType.SharedOperations =>
                        ("Shared Operations Readiness", "Operations",
                         "Shared operations team confirmation of infrastructure, security, and runbook readiness."),
                    _ => ("Unknown Evidence", "Unknown", "Evidence item for unknown stage.")
                };

                string evidenceId = $"evidence-{stage.StageType.ToString().ToLowerInvariant()}";

                EvidenceReadinessCategory readiness;
                DateTime? lastCheckedAt = stage.DecidedAt;
                DateTime? expiresAt     = stage.DecidedAt.HasValue
                    ? stage.DecidedAt.Value.Add(_evidenceFreshnessWindow)
                    : (DateTime?)null;

                if (stage.Status == ApprovalDecisionStatus.Approved)
                {
                    // Fresh if decided within the freshness window; stale if older
                    if (stage.DecidedAt.HasValue && now - stage.DecidedAt.Value <= _evidenceFreshnessWindow)
                        readiness = EvidenceReadinessCategory.Fresh;
                    else
                        readiness = EvidenceReadinessCategory.Stale;
                }
                else if (stage.Status == ApprovalDecisionStatus.Pending)
                {
                    readiness = EvidenceReadinessCategory.Missing;
                }
                else
                {
                    // Rejected, Blocked, NeedsFollowUp — treat as missing (not fresh)
                    readiness = EvidenceReadinessCategory.Missing;
                }

                items.Add(new EvidenceReadinessItem
                {
                    EvidenceId        = evidenceId,
                    Name              = name,
                    Category          = category,
                    ReadinessCategory = readiness,
                    LastCheckedAt     = lastCheckedAt,
                    ExpiresAt         = expiresAt,
                    Description       = description,
                    IsReleaseBlocking = true
                });
            }

            return items;
        }

        /// <summary>
        /// Deterministically derives the release posture from stage records and evidence items.
        /// Returns both the posture enum value and a human-readable rationale.
        /// </summary>
        internal static (ReleasePosture Posture, string Rationale) DerivePosture(
            List<ApprovalStageRecord> stages, List<EvidenceReadinessItem> evidence)
        {
            // Rule 1: Any Rejected stage → BlockedByStageDecision (highest priority)
            ApprovalStageRecord? rejected = stages.FirstOrDefault(s => s.Status == ApprovalDecisionStatus.Rejected);
            if (rejected != null)
                return (ReleasePosture.BlockedByStageDecision,
                    $"Stage '{rejected.StageType}' has been Rejected. Release cannot proceed until this decision is revised.");

            // Rule 1b: Any explicitly Blocked stage → BlockedByStageDecision
            // (Blocked is a deliberate hold by a reviewer, distinct from Missing evidence)
            ApprovalStageRecord? explicitlyBlocked = stages.FirstOrDefault(s => s.Status == ApprovalDecisionStatus.Blocked);
            if (explicitlyBlocked != null)
                return (ReleasePosture.BlockedByStageDecision,
                    $"Stage '{explicitlyBlocked.StageType}' is Blocked pending external resolution.");

            // Rule 1c: Any NeedsFollowUp stage → BlockedByStageDecision
            // (Requestor must act; stage cannot be progressed until follow-up is supplied)
            ApprovalStageRecord? needsFollowUp = stages.FirstOrDefault(s => s.Status == ApprovalDecisionStatus.NeedsFollowUp);
            if (needsFollowUp != null)
                return (ReleasePosture.BlockedByStageDecision,
                    $"Stage '{needsFollowUp.StageType}' requires follow-up from the requestor before it can be progressed.");

            // Rule 2: Any release-blocking Missing evidence (catches Pending stages)
            EvidenceReadinessItem? missing = evidence.FirstOrDefault(
                e => e.IsReleaseBlocking && e.ReadinessCategory == EvidenceReadinessCategory.Missing);
            if (missing != null)
                return (ReleasePosture.BlockedByMissingEvidence,
                    $"Evidence '{missing.Name}' (ID: {missing.EvidenceId}) is Missing. Collect or approve the associated stage to resolve.");

            // Rule 3: Any release-blocking Stale evidence
            EvidenceReadinessItem? stale = evidence.FirstOrDefault(
                e => e.IsReleaseBlocking && e.ReadinessCategory == EvidenceReadinessCategory.Stale);
            if (stale != null)
                return (ReleasePosture.BlockedByStaleEvidence,
                    $"Evidence '{stale.Name}' (ID: {stale.EvidenceId}) is Stale. Re-approve the associated stage to refresh it.");

            // Rule 4: Any release-blocking ConfigurationBlocked evidence
            EvidenceReadinessItem? configBlocked = evidence.FirstOrDefault(
                e => e.IsReleaseBlocking && e.ReadinessCategory == EvidenceReadinessCategory.ConfigurationBlocked);
            if (configBlocked != null)
                return (ReleasePosture.ConfigurationBlocked,
                    $"Evidence '{configBlocked.Name}' is ConfigurationBlocked. Resolve platform configuration to continue.");

            // Rule 5: All 5 stages Approved → LaunchReady
            if (stages.Count == 5 && stages.All(s => s.Status == ApprovalDecisionStatus.Approved))
                return (ReleasePosture.LaunchReady,
                    "All 5 approval stages are Approved and all evidence is Fresh. Release may proceed.");

            // Rule 6: Default — stages still pending or otherwise blocked
            int pendingCount = stages.Count(s => s.Status == ApprovalDecisionStatus.Pending);
            int blockedCount = stages.Count(s => s.Status == ApprovalDecisionStatus.Blocked);
            int nfuCount     = stages.Count(s => s.Status == ApprovalDecisionStatus.NeedsFollowUp);

            return (ReleasePosture.BlockedByStageDecision,
                $"{pendingCount} stage(s) Pending, {blockedCount} Blocked, {nfuCount} NeedsFollowUp. All stages must be Approved before release.");
        }

        /// <summary>
        /// Derives the active owner domain: the domain responsible for the next action.
        /// Evaluated in stage order; the first non-approved stage owns resolution.
        /// NeedsFollowUp overrides to Requestor; ConfigurationBlocked to Platform.
        /// </summary>
        private static ApprovalOwnerDomain DeriveOwnerDomain(
            List<ApprovalStageRecord> stages, List<EvidenceReadinessItem> evidence)
        {
            // ConfigurationBlocked evidence → Platform owns resolution
            if (evidence.Any(e => e.IsReleaseBlocking && e.ReadinessCategory == EvidenceReadinessCategory.ConfigurationBlocked))
                return ApprovalOwnerDomain.Platform;

            // NeedsFollowUp in any stage → Requestor must act
            if (stages.Any(s => s.Status == ApprovalDecisionStatus.NeedsFollowUp))
                return ApprovalOwnerDomain.Requestor;

            // Walk stages in order; first non-approved stage owns it
            foreach (ApprovalStageRecord stage in stages.OrderBy(s => (int)s.StageType))
            {
                if (stage.Status != ApprovalDecisionStatus.Approved)
                    return stage.OwnerDomain;
            }

            return ApprovalOwnerDomain.None;
        }

        /// <summary>
        /// Builds the list of active (unresolved) blockers from stage and evidence state.
        /// </summary>
        private static List<ApprovalBlocker> BuildActiveBlockers(
            List<ApprovalStageRecord> stages,
            List<EvidenceReadinessItem> evidence,
            ReleasePosture posture)
        {
            var blockers = new List<ApprovalBlocker>();

            foreach (ApprovalStageRecord stage in stages)
            {
                if (stage.Status == ApprovalDecisionStatus.Rejected)
                {
                    blockers.Add(new ApprovalBlocker
                    {
                        Reason          = $"Stage '{stage.StageType}' has been Rejected.",
                        Severity        = "Critical",
                        LinkedStageType = stage.StageType,
                        Attribution     = stage.OwnerDomain
                    });
                }
                else if (stage.Status == ApprovalDecisionStatus.Blocked)
                {
                    blockers.Add(new ApprovalBlocker
                    {
                        Reason          = $"Stage '{stage.StageType}' is Blocked.",
                        Severity        = "High",
                        LinkedStageType = stage.StageType,
                        Attribution     = stage.OwnerDomain
                    });
                }
            }

            foreach (EvidenceReadinessItem item in evidence.Where(e => e.IsReleaseBlocking && e.ReadinessCategory != EvidenceReadinessCategory.Fresh))
            {
                string severity = item.ReadinessCategory switch
                {
                    EvidenceReadinessCategory.Missing              => "High",
                    EvidenceReadinessCategory.Stale                => "Medium",
                    EvidenceReadinessCategory.ConfigurationBlocked => "High",
                    _                                              => "Low"
                };

                blockers.Add(new ApprovalBlocker
                {
                    Reason           = $"Evidence '{item.Name}' is {item.ReadinessCategory}.",
                    Severity         = severity,
                    LinkedEvidenceId = item.EvidenceId,
                    Attribution      = item.ReadinessCategory == EvidenceReadinessCategory.ConfigurationBlocked
                        ? ApprovalOwnerDomain.Platform
                        : ApprovalOwnerDomain.None
                });
            }

            return blockers;
        }

        /// <summary>
        /// Derives the worst-case overall readiness from a collection of evidence items.
        /// </summary>
        private static EvidenceReadinessCategory DeriveOverallReadiness(
            List<EvidenceReadinessItem> items)
        {
            if (items.Any(i => i.IsReleaseBlocking && i.ReadinessCategory == EvidenceReadinessCategory.Missing))
                return EvidenceReadinessCategory.Missing;
            if (items.Any(i => i.IsReleaseBlocking && i.ReadinessCategory == EvidenceReadinessCategory.ConfigurationBlocked))
                return EvidenceReadinessCategory.ConfigurationBlocked;
            if (items.Any(i => i.IsReleaseBlocking && i.ReadinessCategory == EvidenceReadinessCategory.Stale))
                return EvidenceReadinessCategory.Stale;
            return EvidenceReadinessCategory.Fresh;
        }

        // ── Error Helpers ──────────────────────────────────────────────────────

        private static ApprovalWorkflowStateResponse FailState(
            string errorCode, string message, string correlationId) =>
            new()
            {
                Success       = false,
                ErrorCode     = errorCode,
                ErrorMessage  = message,
                CorrelationId = correlationId
            };

        private static SubmitStageDecisionResponse FailDecision(
            string errorCode, string message, string correlationId,
            string? guidance = null) =>
            new()
            {
                Success          = false,
                ErrorCode        = errorCode,
                ErrorMessage     = message,
                OperatorGuidance = guidance,
                CorrelationId    = correlationId
            };

        private static ReleaseEvidenceSummaryResponse FailEvidence(
            string errorCode, string message, string correlationId) =>
            new()
            {
                Success       = false,
                ErrorCode     = errorCode,
                ErrorMessage  = message,
                CorrelationId = correlationId
            };

        private static ApprovalAuditHistoryResponse FailAudit(
            string errorCode, string message, string correlationId) =>
            new()
            {
                Success       = false,
                ErrorCode     = errorCode,
                ErrorMessage  = message,
                CorrelationId = correlationId
            };
    }
}
