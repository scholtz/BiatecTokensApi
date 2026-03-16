using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ScheduledReporting;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of <see cref="IScheduledReportingService"/>.
    ///
    /// Manages reporting templates, report runs, schedule definitions, approval metadata,
    /// delivery outcomes, and evidence freshness evaluation for scheduled compliance reporting.
    ///
    /// Evidence freshness is fail-closed: if required evidence is missing or stale, runs are
    /// created with <see cref="ReportRunStatus.Blocked"/> status and actionable blocker details.
    /// </summary>
    public class ScheduledReportingService : IScheduledReportingService
    {
        private readonly ILogger<ScheduledReportingService> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly IWebhookService? _webhookService;

        // In-memory stores (production would use a database)
        private readonly Dictionary<string, ReportingTemplate> _templates = new();
        private readonly Dictionary<string, ReportRunRecord> _runs = new();
        private readonly Dictionary<string, ReportingScheduleDefinition> _schedules = new();

        // templateId → ordered list of runIds (newest first)
        private readonly Dictionary<string, List<string>> _templateRunIndex = new();

        private readonly object _lock = new();

        // Evidence freshness thresholds
        private static readonly TimeSpan FreshnessWindowKycAml = TimeSpan.FromDays(90);
        private static readonly TimeSpan FreshnessWindowCases = TimeSpan.FromDays(30);
        private static readonly TimeSpan FreshnessWindowApprovals = TimeSpan.FromDays(60);
        private static readonly TimeSpan FreshnessWindowAudit = TimeSpan.FromDays(7);
        private static readonly TimeSpan FreshnessWindowMonitoring = TimeSpan.FromDays(14);
        private static readonly TimeSpan FreshnessWindowSignOff = TimeSpan.FromDays(30);
        private static readonly TimeSpan FreshnessWindowRegPackage = TimeSpan.FromDays(90);
        private static readonly TimeSpan NearExpiryWarning = TimeSpan.FromDays(7);

        /// <summary>Initializes a new instance of <see cref="ScheduledReportingService"/>.</summary>
        public ScheduledReportingService(
            ILogger<ScheduledReportingService> logger,
            TimeProvider? timeProvider = null,
            IWebhookService? webhookService = null)
        {
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _webhookService = webhookService;
        }

        // ════════════════════════════════════════════════════════════════════
        // Template CRUD
        // ════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public Task<ReportingTemplateResponse> CreateTemplateAsync(
            CreateReportingTemplateRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Task.FromResult(Fail("MISSING_NAME", "Template name is required."));

            if (string.IsNullOrWhiteSpace(actorId))
                return Task.FromResult(Fail("MISSING_ACTOR", "Actor identity is required."));

            var now = _timeProvider.GetUtcNow();
            var templateId = "tpl-" + Guid.NewGuid().ToString("N")[..16];

            var template = new ReportingTemplate
            {
                TemplateId = templateId,
                Name = request.Name.Trim(),
                Description = request.Description,
                AudienceType = request.AudienceType,
                Cadence = request.Cadence,
                RequiredEvidenceDomains = request.RequiredEvidenceDomains ?? new(),
                DeliveryDestinations = request.DeliveryDestinations ?? new(),
                ReviewRequired = request.ReviewRequired,
                ApprovalRequired = request.ApprovalRequired,
                SubjectScope = request.SubjectScope,
                IsActive = true,
                IsArchived = false,
                CreatedAt = now,
                CreatedBy = actorId,
                UpdatedAt = now,
                UpdatedBy = actorId,
                FreshnessPosture = "Unknown"
            };

            // Compute NextRunAt for scheduled cadences
            if (request.Cadence != ReportingCadence.EventDriven)
                template.NextRunAt = ComputeNextRunAt(request.Cadence, now);

            lock (_lock)
            {
                _templates[templateId] = template;
                _templateRunIndex[templateId] = new List<string>();
            }

            _logger.LogInformation(
                "ReportingTemplate created. TemplateId={TemplateId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(templateId),
                LoggingHelper.SanitizeLogInput(actorId));

            _ = EmitTemplateEventAsync(template, WebhookEventType.ReportTemplateCreated, actorId);

            return Task.FromResult(new ReportingTemplateResponse { Success = true, Template = template });
        }

        /// <inheritdoc/>
        public Task<ReportingTemplateResponse> GetTemplateAsync(string templateId)
        {
            lock (_lock)
            {
                if (!_templates.TryGetValue(templateId, out var template))
                    return Task.FromResult(Fail("TEMPLATE_NOT_FOUND", $"Template '{templateId}' not found."));

                return Task.FromResult(new ReportingTemplateResponse { Success = true, Template = template });
            }
        }

        /// <inheritdoc/>
        public Task<ReportingTemplateResponse> UpdateTemplateAsync(
            string templateId, UpdateReportingTemplateRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                return Task.FromResult(Fail("MISSING_ACTOR", "Actor identity is required."));

            lock (_lock)
            {
                if (!_templates.TryGetValue(templateId, out var template))
                    return Task.FromResult(Fail("TEMPLATE_NOT_FOUND", $"Template '{templateId}' not found."));

                if (template.IsArchived)
                    return Task.FromResult(Fail("TEMPLATE_ARCHIVED", "Cannot update an archived template."));

                var now = _timeProvider.GetUtcNow();

                if (request.Name != null) template.Name = request.Name.Trim();
                if (request.Description != null) template.Description = request.Description;
                if (request.Cadence.HasValue)
                {
                    template.Cadence = request.Cadence.Value;
                    if (template.Cadence != ReportingCadence.EventDriven)
                        template.NextRunAt = ComputeNextRunAt(template.Cadence, now);
                    else
                        template.NextRunAt = null;
                }
                if (request.RequiredEvidenceDomains != null)
                    template.RequiredEvidenceDomains = request.RequiredEvidenceDomains;
                if (request.DeliveryDestinations != null)
                    template.DeliveryDestinations = request.DeliveryDestinations;
                if (request.ReviewRequired.HasValue) template.ReviewRequired = request.ReviewRequired.Value;
                if (request.ApprovalRequired.HasValue) template.ApprovalRequired = request.ApprovalRequired.Value;
                if (request.SubjectScope != null) template.SubjectScope = request.SubjectScope;
                if (request.IsActive.HasValue) template.IsActive = request.IsActive.Value;

                template.UpdatedAt = now;
                template.UpdatedBy = actorId;

                _logger.LogInformation(
                    "ReportingTemplate updated. TemplateId={TemplateId} Actor={Actor}",
                    LoggingHelper.SanitizeLogInput(templateId),
                    LoggingHelper.SanitizeLogInput(actorId));

                _ = EmitTemplateEventAsync(template, WebhookEventType.ReportTemplateUpdated, actorId);

                return Task.FromResult(new ReportingTemplateResponse { Success = true, Template = template });
            }
        }

        /// <inheritdoc/>
        public Task<ReportingTemplateResponse> ArchiveTemplateAsync(string templateId, string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                return Task.FromResult(Fail("MISSING_ACTOR", "Actor identity is required."));

            lock (_lock)
            {
                if (!_templates.TryGetValue(templateId, out var template))
                    return Task.FromResult(Fail("TEMPLATE_NOT_FOUND", $"Template '{templateId}' not found."));

                if (template.IsArchived)
                    return Task.FromResult(new ReportingTemplateResponse { Success = true, Template = template });

                template.IsArchived = true;
                template.IsActive = false;
                template.NextRunAt = null;
                template.UpdatedAt = _timeProvider.GetUtcNow();
                template.UpdatedBy = actorId;

                // Deactivate schedule if present
                if (_schedules.TryGetValue(templateId, out var schedule))
                    schedule.IsActive = false;

                _logger.LogInformation(
                    "ReportingTemplate archived. TemplateId={TemplateId} Actor={Actor}",
                    LoggingHelper.SanitizeLogInput(templateId),
                    LoggingHelper.SanitizeLogInput(actorId));

                _ = EmitTemplateEventAsync(template, WebhookEventType.ReportTemplateArchived, actorId);

                return Task.FromResult(new ReportingTemplateResponse { Success = true, Template = template });
            }
        }

        /// <inheritdoc/>
        public Task<ListReportingTemplatesResponse> ListTemplatesAsync(
            bool includeArchived = false,
            ReportingAudienceType? audienceFilter = null,
            int page = 1,
            int pageSize = 20)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);

            lock (_lock)
            {
                var query = _templates.Values.AsEnumerable();

                if (!includeArchived)
                    query = query.Where(t => !t.IsArchived);

                if (audienceFilter.HasValue)
                    query = query.Where(t => t.AudienceType == audienceFilter.Value);

                var ordered = query.OrderByDescending(t => t.CreatedAt).ToList();
                var total = ordered.Count;
                var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Task.FromResult(new ListReportingTemplatesResponse
                {
                    Success = true,
                    Templates = paged,
                    TotalCount = total
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Report Run operations
        // ════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<ReportRunResponse> TriggerRunAsync(
            string templateId, TriggerReportRunRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                return RunFail("MISSING_ACTOR", "Actor identity is required.");

            ReportingTemplate? template;
            string? priorRunId;
            lock (_lock)
            {
                if (!_templates.TryGetValue(templateId, out template))
                    return RunFail("TEMPLATE_NOT_FOUND", $"Template '{templateId}' not found.");

                if (template.IsArchived)
                    return RunFail("TEMPLATE_ARCHIVED", "Cannot trigger a run for an archived template.");

                priorRunId = _templateRunIndex.TryGetValue(templateId, out var runList) && runList.Count > 0
                    ? runList[0]
                    : null;
            }

            var now = _timeProvider.GetUtcNow();
            var runId = "run-" + Guid.NewGuid().ToString("N")[..16];
            var triggeredBy = string.IsNullOrWhiteSpace(request.TriggeredBy) ? actorId : request.TriggeredBy;

            // Evaluate evidence freshness (fail-closed)
            var (lineage, blockers) = EvaluateEvidence(template.RequiredEvidenceDomains, now);

            var criticalBlockers = blockers.Where(b => b.Severity == ReportBlockerSeverity.Critical).ToList();
            var status = criticalBlockers.Count > 0
                ? ReportRunStatus.Blocked
                : template.ReviewRequired
                    ? ReportRunStatus.AwaitingReview
                    : template.ApprovalRequired
                        ? ReportRunStatus.AwaitingApproval
                        : ReportRunStatus.Exported;

            // Build initial delivery outcomes (all Pending)
            var deliveryOutcomes = template.DeliveryDestinations.Select(d => new DeliveryOutcomeRecord
            {
                DestinationId = d.DestinationId,
                DestinationType = d.DestinationType,
                Label = d.Label,
                Status = DeliveryOutcomeStatus.Pending
            }).ToList();

            // If status is Exported (no blockers, no review/approval needed), simulate delivery
            if (status == ReportRunStatus.Exported)
            {
                deliveryOutcomes = SimulateDelivery(deliveryOutcomes, now);
                var failedRequired = deliveryOutcomes
                    .Where(o => o.Status == DeliveryOutcomeStatus.Failed)
                    .Join(template.DeliveryDestinations,
                        o => o.DestinationId, d => d.DestinationId,
                        (o, d) => d.IsRequired)
                    .Any(isRequired => isRequired);

                status = failedRequired
                    ? ReportRunStatus.PartiallyDelivered
                    : deliveryOutcomes.All(o => o.Status == DeliveryOutcomeStatus.Delivered)
                        ? ReportRunStatus.Delivered
                        : ReportRunStatus.PartiallyDelivered;
            }

            // Build comparison to prior run
            ReportRunComparison? comparison = null;
            ReportRunRecord? priorRun = null;
            lock (_lock)
            {
                if (priorRunId != null && _runs.TryGetValue(priorRunId, out priorRun))
                    comparison = BuildComparison(priorRun, blockers, lineage, now);
            }

            // Build approval metadata
            var approvalMetadata = new ReportRunApprovalMetadata
            {
                ReviewRequired = template.ReviewRequired,
                ApprovalRequired = template.ApprovalRequired
            };

            var run = new ReportRunRecord
            {
                RunId = runId,
                TemplateId = templateId,
                TemplateName = template.Name,
                AudienceType = template.AudienceType,
                Status = status,
                CreatedAt = now,
                CreatedBy = triggeredBy,
                UpdatedAt = now,
                EvidenceLineage = lineage,
                Blockers = blockers,
                DeliveryOutcomes = deliveryOutcomes,
                ApprovalMetadata = approvalMetadata,
                ComparisonToPriorRun = comparison,
                CriticalBlockerCount = criticalBlockers.Count,
                WarningCount = blockers.Count(b => b.Severity == ReportBlockerSeverity.Warning),
                DeliverySuccessCount = deliveryOutcomes.Count(o => o.Status == DeliveryOutcomeStatus.Delivered),
                DeliveryFailureCount = deliveryOutcomes.Count(o => o.Status == DeliveryOutcomeStatus.Failed)
            };

            if (status is ReportRunStatus.Exported or ReportRunStatus.Delivered or ReportRunStatus.PartiallyDelivered)
                run.ExportedAt = now;

            if (status is ReportRunStatus.Delivered)
                run.DeliveredAt = now;

            lock (_lock)
            {
                _runs[runId] = run;
                if (_templateRunIndex.TryGetValue(templateId, out var runList))
                    runList.Insert(0, runId);

                // Update template LastRun metadata
                template.LastRunAt = now;
                template.LastRunStatus = status;
                if (template.Cadence != ReportingCadence.EventDriven)
                    template.NextRunAt = ComputeNextRunAt(template.Cadence, now);
            }

            _logger.LogInformation(
                "ReportRun created. RunId={RunId} TemplateId={TemplateId} Status={Status} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(runId),
                LoggingHelper.SanitizeLogInput(templateId),
                status,
                LoggingHelper.SanitizeLogInput(triggeredBy));

            // Emit webhook events
            await EmitRunEventAsync(run, WebhookEventType.ReportRunCreated, triggeredBy);
            if (status == ReportRunStatus.Blocked)
                await EmitRunEventAsync(run, WebhookEventType.ReportRunBlocked, triggeredBy);
            else if (status is ReportRunStatus.Delivered or ReportRunStatus.PartiallyDelivered)
                await EmitRunEventAsync(run, WebhookEventType.ReportRunDelivered, triggeredBy);
            else if (status == ReportRunStatus.Exported)
                await EmitRunEventAsync(run, WebhookEventType.ReportRunExported, triggeredBy);

            return new ReportRunResponse { Success = true, Run = run };
        }

        /// <inheritdoc/>
        public Task<ReportRunResponse> GetRunAsync(string runId)
        {
            lock (_lock)
            {
                if (!_runs.TryGetValue(runId, out var run))
                    return Task.FromResult(RunFail("RUN_NOT_FOUND", $"Run '{runId}' not found."));

                return Task.FromResult(new ReportRunResponse { Success = true, Run = run });
            }
        }

        /// <inheritdoc/>
        public Task<ListReportRunsResponse> ListRunsAsync(
            string templateId,
            ReportRunStatus? statusFilter = null,
            int page = 1,
            int pageSize = 20)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);

            lock (_lock)
            {
                if (!_templates.ContainsKey(templateId))
                    return Task.FromResult(new ListReportRunsResponse
                    {
                        Success = false,
                        ErrorCode = "TEMPLATE_NOT_FOUND",
                        ErrorMessage = $"Template '{templateId}' not found."
                    });

                if (!_templateRunIndex.TryGetValue(templateId, out var runIds))
                    return Task.FromResult(new ListReportRunsResponse { Success = true, TotalCount = 0 });

                var runs = runIds
                    .Select(id => _runs.TryGetValue(id, out var r) ? r : null)
                    .Where(r => r != null)
                    .Select(r => r!)
                    .Where(r => !statusFilter.HasValue || r.Status == statusFilter.Value)
                    .ToList();

                var total = runs.Count;
                var paged = runs.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Task.FromResult(new ListReportRunsResponse
                {
                    Success = true,
                    Runs = paged,
                    TotalCount = total
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Approval and review
        // ════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<ReportRunResponse> ReviewRunAsync(
            string runId, ReviewReportRunRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                return RunFail("MISSING_ACTOR", "Actor identity is required.");

            ReportRunRecord? run;
            lock (_lock)
            {
                if (!_runs.TryGetValue(runId, out run))
                    return RunFail("RUN_NOT_FOUND", $"Run '{runId}' not found.");

                if (run.Status != ReportRunStatus.AwaitingReview)
                    return RunFail("INVALID_STATE",
                        $"Run must be in AwaitingReview status to record a review. Current status: {run.Status}.");

                if (!request.Approve && string.IsNullOrWhiteSpace(request.RejectionReason))
                    return RunFail("REJECTION_REASON_REQUIRED", "RejectionReason is required when rejecting a run.");

                var now = _timeProvider.GetUtcNow();

                run.ApprovalMetadata ??= new ReportRunApprovalMetadata();
                run.ApprovalMetadata.ReviewedBy = actorId;
                run.ApprovalMetadata.ReviewedAt = now;
                run.ApprovalMetadata.ReviewNotes = request.ReviewNotes;

                if (request.Approve)
                {
                    run.Status = run.ApprovalMetadata.ApprovalRequired
                        ? ReportRunStatus.AwaitingApproval
                        : ReportRunStatus.Exported;

                    if (run.Status == ReportRunStatus.Exported)
                        run.ExportedAt = now;
                }
                else
                {
                    run.Status = ReportRunStatus.Failed;
                    run.ApprovalMetadata.IsRejected = true;
                    run.ApprovalMetadata.RejectionReason = request.RejectionReason;
                }

                run.UpdatedAt = now;
            }

            _logger.LogInformation(
                "ReportRun reviewed. RunId={RunId} Approved={Approved} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(runId),
                request.Approve,
                LoggingHelper.SanitizeLogInput(actorId));

            if (run.Status == ReportRunStatus.Failed)
                await EmitRunEventAsync(run, WebhookEventType.ReportRunFailed, actorId);

            return new ReportRunResponse { Success = true, Run = run };
        }

        /// <inheritdoc/>
        public async Task<ReportRunResponse> ApproveRunAsync(
            string runId, ApproveReportRunRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                return RunFail("MISSING_ACTOR", "Actor identity is required.");

            ReportRunRecord? run;
            lock (_lock)
            {
                if (!_runs.TryGetValue(runId, out run))
                    return RunFail("RUN_NOT_FOUND", $"Run '{runId}' not found.");

                if (run.Status != ReportRunStatus.AwaitingApproval)
                    return RunFail("INVALID_STATE",
                        $"Run must be in AwaitingApproval status to record an approval. Current status: {run.Status}.");

                if (!request.Approve && string.IsNullOrWhiteSpace(request.RejectionReason))
                    return RunFail("REJECTION_REASON_REQUIRED", "RejectionReason is required when rejecting a run.");

                var now = _timeProvider.GetUtcNow();

                run.ApprovalMetadata ??= new ReportRunApprovalMetadata();
                run.ApprovalMetadata.ApprovedBy = actorId;
                run.ApprovalMetadata.ApprovedAt = now;
                run.ApprovalMetadata.ApprovalNotes = request.ApprovalNotes;

                if (request.Approve)
                {
                    // Simulate delivery after approval
                    run.DeliveryOutcomes = SimulateDelivery(run.DeliveryOutcomes, now);
                    run.DeliverySuccessCount = run.DeliveryOutcomes.Count(o => o.Status == DeliveryOutcomeStatus.Delivered);
                    run.DeliveryFailureCount = run.DeliveryOutcomes.Count(o => o.Status == DeliveryOutcomeStatus.Failed);

                    run.ExportedAt = now;
                    run.Status = run.DeliveryFailureCount > 0
                        ? ReportRunStatus.PartiallyDelivered
                        : ReportRunStatus.Delivered;

                    if (run.Status == ReportRunStatus.Delivered)
                        run.DeliveredAt = now;
                }
                else
                {
                    run.Status = ReportRunStatus.Failed;
                    run.ApprovalMetadata.IsRejected = true;
                    run.ApprovalMetadata.RejectionReason = request.RejectionReason;
                }

                run.UpdatedAt = now;
            }

            _logger.LogInformation(
                "ReportRun approved/rejected. RunId={RunId} Approved={Approved} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(runId),
                request.Approve,
                LoggingHelper.SanitizeLogInput(actorId));

            if (request.Approve)
            {
                await EmitRunEventAsync(run, WebhookEventType.ReportRunApproved, actorId);
                await EmitRunEventAsync(run, WebhookEventType.ReportRunExported, actorId);
                if (run.Status is ReportRunStatus.Delivered or ReportRunStatus.PartiallyDelivered)
                    await EmitRunEventAsync(run, WebhookEventType.ReportRunDelivered, actorId);
            }
            else
            {
                await EmitRunEventAsync(run, WebhookEventType.ReportRunFailed, actorId);
            }

            return new ReportRunResponse { Success = true, Run = run };
        }

        // ════════════════════════════════════════════════════════════════════
        // Schedule management
        // ════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public Task<ScheduleResponse> UpsertScheduleAsync(
            string templateId, UpsertScheduleRequest request, string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                return Task.FromResult(ScheduleFail("MISSING_ACTOR", "Actor identity is required."));

            if (request.TriggerHourUtc < 0 || request.TriggerHourUtc > 23)
                return Task.FromResult(ScheduleFail("INVALID_TRIGGER_HOUR", "TriggerHourUtc must be 0-23."));

            if (request.Cadence == ReportingCadence.Monthly &&
                request.DayOfMonth.HasValue &&
                (request.DayOfMonth.Value < 1 || request.DayOfMonth.Value > 28))
                return Task.FromResult(ScheduleFail("INVALID_DAY_OF_MONTH", "DayOfMonth must be 1-28 for monthly cadence."));

            lock (_lock)
            {
                if (!_templates.ContainsKey(templateId))
                    return Task.FromResult(ScheduleFail("TEMPLATE_NOT_FOUND", $"Template '{templateId}' not found."));

                var now = _timeProvider.GetUtcNow();
                var scheduleId = _schedules.TryGetValue(templateId, out var existing)
                    ? existing.ScheduleId
                    : "sch-" + Guid.NewGuid().ToString("N")[..16];

                var schedule = new ReportingScheduleDefinition
                {
                    ScheduleId = scheduleId,
                    TemplateId = templateId,
                    Cadence = request.Cadence,
                    DayOfMonth = request.DayOfMonth,
                    AnchorMonth = request.AnchorMonth,
                    TriggerTimeUtc = TimeSpan.FromHours(request.TriggerHourUtc),
                    IsActive = request.IsActive,
                    CreatedAt = existing?.CreatedAt ?? now,
                    CreatedBy = existing?.CreatedBy ?? actorId,
                    NextTriggerAt = request.IsActive && request.Cadence != ReportingCadence.EventDriven
                        ? ComputeNextRunAt(request.Cadence, now)
                        : null,
                    LastTriggeredAt = existing?.LastTriggeredAt
                };

                _schedules[templateId] = schedule;

                // Update template NextRunAt accordingly
                if (_templates.TryGetValue(templateId, out var template))
                {
                    template.NextRunAt = schedule.NextTriggerAt;
                    template.Cadence = request.Cadence;
                }

                _logger.LogInformation(
                    "Schedule upserted. TemplateId={TemplateId} Cadence={Cadence} Actor={Actor}",
                    LoggingHelper.SanitizeLogInput(templateId),
                    request.Cadence,
                    LoggingHelper.SanitizeLogInput(actorId));

                return Task.FromResult(new ScheduleResponse { Success = true, Schedule = schedule });
            }
        }

        /// <inheritdoc/>
        public Task<ScheduleResponse> GetScheduleAsync(string templateId)
        {
            lock (_lock)
            {
                if (!_templates.ContainsKey(templateId))
                    return Task.FromResult(ScheduleFail("TEMPLATE_NOT_FOUND", $"Template '{templateId}' not found."));

                if (!_schedules.TryGetValue(templateId, out var schedule))
                    return Task.FromResult(ScheduleFail("SCHEDULE_NOT_FOUND",
                        $"No schedule has been defined for template '{templateId}'."));

                return Task.FromResult(new ScheduleResponse { Success = true, Schedule = schedule });
            }
        }

        /// <inheritdoc/>
        public Task<ScheduleResponse> DeactivateScheduleAsync(string templateId, string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId))
                return Task.FromResult(ScheduleFail("MISSING_ACTOR", "Actor identity is required."));

            lock (_lock)
            {
                if (!_templates.ContainsKey(templateId))
                    return Task.FromResult(ScheduleFail("TEMPLATE_NOT_FOUND", $"Template '{templateId}' not found."));

                if (!_schedules.TryGetValue(templateId, out var schedule))
                    return Task.FromResult(ScheduleFail("SCHEDULE_NOT_FOUND",
                        $"No schedule defined for template '{templateId}'."));

                schedule.IsActive = false;
                schedule.NextTriggerAt = null;

                if (_templates.TryGetValue(templateId, out var template))
                    template.NextRunAt = null;

                _logger.LogInformation(
                    "Schedule deactivated. TemplateId={TemplateId} Actor={Actor}",
                    LoggingHelper.SanitizeLogInput(templateId),
                    LoggingHelper.SanitizeLogInput(actorId));

                return Task.FromResult(new ScheduleResponse { Success = true, Schedule = schedule });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        private static ReportingTemplateResponse Fail(string code, string message) =>
            new() { Success = false, ErrorCode = code, ErrorMessage = message };

        private static ReportRunResponse RunFail(string code, string message) =>
            new() { Success = false, ErrorCode = code, ErrorMessage = message };

        private static ScheduleResponse ScheduleFail(string code, string message) =>
            new() { Success = false, ErrorCode = code, ErrorMessage = message };

        /// <summary>
        /// Evaluates evidence freshness for the requested domains. Returns lineage entries and blockers.
        /// In a production system, this would query real services. Here we use simulated metadata.
        /// </summary>
        private (List<EvidenceLineageEntry> lineage, List<ReportRunBlocker> blockers) EvaluateEvidence(
            List<EvidenceDomainKind> domains, DateTimeOffset now)
        {
            var lineage = new List<EvidenceLineageEntry>();
            var blockers = new List<ReportRunBlocker>();

            foreach (var domain in domains)
            {
                var window = GetFreshnessWindow(domain);
                // Simulate: evidence was last updated slightly before the window to be "current"
                // In production, this would query actual service data
                var simulatedLastUpdated = now - (window * 0.5);
                var expiresAt = simulatedLastUpdated + window;

                var freshnessStatus = expiresAt > now + NearExpiryWarning
                    ? ReportEvidenceFreshnessStatus.Current
                    : expiresAt > now
                        ? ReportEvidenceFreshnessStatus.NearingExpiry
                        : ReportEvidenceFreshnessStatus.Stale;

                lineage.Add(new EvidenceLineageEntry
                {
                    Domain = domain,
                    FreshnessStatus = freshnessStatus,
                    LastUpdatedAt = simulatedLastUpdated,
                    ExpiresAt = expiresAt,
                    Note = $"Evidence evaluated at {now:O}"
                });

                if (freshnessStatus == ReportEvidenceFreshnessStatus.Stale)
                {
                    blockers.Add(new ReportRunBlocker
                    {
                        BlockerCode = $"STALE_{domain.ToString().ToUpperInvariant()}",
                        Severity = ReportBlockerSeverity.Critical,
                        Description = $"Evidence for domain '{domain}' is stale and must be refreshed.",
                        RemediationHint = $"Update {domain} records to be within the {window.TotalDays:0}-day freshness window.",
                        AffectedDomain = domain,
                        DetectedAt = now
                    });
                }
                else if (freshnessStatus == ReportEvidenceFreshnessStatus.NearingExpiry)
                {
                    blockers.Add(new ReportRunBlocker
                    {
                        BlockerCode = $"NEARING_EXPIRY_{domain.ToString().ToUpperInvariant()}",
                        Severity = ReportBlockerSeverity.Warning,
                        Description = $"Evidence for domain '{domain}' will expire soon.",
                        RemediationHint = $"Refresh {domain} records within 7 days to avoid blocking future runs.",
                        AffectedDomain = domain,
                        DetectedAt = now
                    });
                }
            }

            return (lineage, blockers);
        }

        private static TimeSpan GetFreshnessWindow(EvidenceDomainKind domain) => domain switch
        {
            EvidenceDomainKind.KycAml => FreshnessWindowKycAml,
            EvidenceDomainKind.ComplianceCases => FreshnessWindowCases,
            EvidenceDomainKind.ApprovalWorkflows => FreshnessWindowApprovals,
            EvidenceDomainKind.AuditExports => FreshnessWindowAudit,
            EvidenceDomainKind.OngoingMonitoring => FreshnessWindowMonitoring,
            EvidenceDomainKind.ProtectedSignOff => FreshnessWindowSignOff,
            EvidenceDomainKind.RegulatoryPackages => FreshnessWindowRegPackage,
            _ => TimeSpan.FromDays(30)
        };

        private static DateTimeOffset ComputeNextRunAt(ReportingCadence cadence, DateTimeOffset from) =>
            cadence switch
            {
                ReportingCadence.Monthly => from.AddMonths(1),
                ReportingCadence.Quarterly => from.AddMonths(3),
                ReportingCadence.SemiAnnual => from.AddMonths(6),
                ReportingCadence.Annual => from.AddYears(1),
                _ => from.AddMonths(1)
            };

        private static List<DeliveryOutcomeRecord> SimulateDelivery(
            List<DeliveryOutcomeRecord> outcomes, DateTimeOffset now)
        {
            foreach (var outcome in outcomes)
            {
                outcome.AttemptedAt = now;
                outcome.AttemptCount = 1;
                // Simulate successful delivery for all destinations
                outcome.Status = DeliveryOutcomeStatus.Delivered;
                outcome.DeliveredAt = now;
            }
            return outcomes;
        }

        private static ReportRunComparison BuildComparison(
            ReportRunRecord priorRun,
            List<ReportRunBlocker> currentBlockers,
            List<EvidenceLineageEntry> currentLineage,
            DateTimeOffset now)
        {
            var priorBlockerCodes = priorRun.Blockers
                .Where(b => !b.IsResolved && b.Severity == ReportBlockerSeverity.Critical)
                .Select(b => b.BlockerCode)
                .ToHashSet();
            var currentBlockerCodes = currentBlockers
                .Where(b => b.Severity == ReportBlockerSeverity.Critical)
                .Select(b => b.BlockerCode)
                .ToHashSet();

            var newBlockers = currentBlockerCodes.Except(priorBlockerCodes).ToList();
            var resolvedBlockers = priorBlockerCodes.Except(currentBlockerCodes).ToList();

            var priorFreshness = priorRun.EvidenceLineage
                .ToDictionary(e => e.Domain, e => e.FreshnessStatus);
            var freshnessChanges = currentLineage
                .Where(e => priorFreshness.TryGetValue(e.Domain, out var prev) && prev != e.FreshnessStatus)
                .Select(e => $"{e.Domain}: {priorFreshness[e.Domain]} → {e.FreshnessStatus}")
                .ToList();

            var postureImproved = resolvedBlockers.Count > newBlockers.Count ||
                                  (resolvedBlockers.Count == newBlockers.Count && resolvedBlockers.Count > 0);

            return new ReportRunComparison
            {
                PriorRunId = priorRun.RunId,
                PriorRunAt = priorRun.CreatedAt,
                PriorRunStatus = priorRun.Status,
                NewBlockers = newBlockers,
                ResolvedBlockers = resolvedBlockers,
                EvidenceFreshnessChanges = freshnessChanges,
                PostureImproved = postureImproved
            };
        }

        private async Task EmitRunEventAsync(ReportRunRecord run, WebhookEventType eventType, string actor)
        {
            if (_webhookService == null) return;
            try
            {
                await _webhookService.EmitEventAsync(new WebhookEvent
                {
                    EventType = eventType,
                    Actor = actor,
                    Data = new Dictionary<string, object>
                    {
                        ["runId"] = run.RunId,
                        ["templateId"] = run.TemplateId,
                        ["status"] = run.Status.ToString(),
                        ["criticalBlockerCount"] = run.CriticalBlockerCount,
                        ["audienceType"] = run.AudienceType.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to emit webhook event {EventType} for run {RunId}",
                    eventType, LoggingHelper.SanitizeLogInput(run.RunId));
            }
        }

        private async Task EmitTemplateEventAsync(ReportingTemplate template, WebhookEventType eventType, string actor)
        {
            if (_webhookService == null) return;
            try
            {
                await _webhookService.EmitEventAsync(new WebhookEvent
                {
                    EventType = eventType,
                    Actor = actor,
                    Data = new Dictionary<string, object>
                    {
                        ["templateId"] = template.TemplateId,
                        ["templateName"] = template.Name,
                        ["audienceType"] = template.AudienceType.ToString(),
                        ["cadence"] = template.Cadence.ToString(),
                        ["isArchived"] = template.IsArchived
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to emit webhook event {EventType} for template {TemplateId}",
                    eventType, LoggingHelper.SanitizeLogInput(template.TemplateId));
            }
        }
    }
}
