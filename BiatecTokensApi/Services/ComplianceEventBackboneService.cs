using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceAuditExport;
using BiatecTokensApi.Models.ComplianceCaseManagement;
using BiatecTokensApi.Models.ComplianceEvents;
using BiatecTokensApi.Models.KycAmlOnboarding;
using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services.Interface;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// Aggregates persisted compliance workflow records into a canonical, webhook-ready event stream.
    /// </summary>
    public class ComplianceEventBackboneService : IComplianceEventBackboneService
    {
        private readonly IComplianceCaseRepository _complianceCaseRepository;
        private readonly IKycAmlOnboardingCaseService _onboardingCaseService;
        private readonly IProtectedSignOffEvidencePersistenceService _protectedSignOffEvidenceService;
        private readonly IComplianceAuditExportService _complianceAuditExportService;
        private readonly ILogger<ComplianceEventBackboneService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComplianceEventBackboneService"/> class.
        /// </summary>
        public ComplianceEventBackboneService(
            IComplianceCaseRepository complianceCaseRepository,
            IKycAmlOnboardingCaseService onboardingCaseService,
            IProtectedSignOffEvidencePersistenceService protectedSignOffEvidenceService,
            IComplianceAuditExportService complianceAuditExportService,
            ILogger<ComplianceEventBackboneService> logger)
        {
            _complianceCaseRepository = complianceCaseRepository;
            _onboardingCaseService = onboardingCaseService;
            _protectedSignOffEvidenceService = protectedSignOffEvidenceService;
            _complianceAuditExportService = complianceAuditExportService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ComplianceEventListResponse> GetEventsAsync(ComplianceEventQueryRequest request, string actorId)
        {
            ArgumentNullException.ThrowIfNull(request);

            var page = Math.Max(request.Page, 1);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var events = new List<ComplianceEventEnvelope>();

            await AppendComplianceCaseEventsAsync(events, request);
            await AppendOnboardingEventsAsync(events, request);
            await AppendProtectedSignOffEventsAsync(events, request);
            await AppendComplianceAuditExportEventsAsync(events, request);

            GetSignOffReleaseReadinessResponse? releaseReadiness = null;
            if (!string.IsNullOrWhiteSpace(request.HeadRef))
            {
                releaseReadiness = await _protectedSignOffEvidenceService.GetReleaseReadinessAsync(
                    new GetSignOffReleaseReadinessRequest
                    {
                        HeadRef = request.HeadRef,
                        CaseId = request.CaseId
                    });

                string? sourceCaseId = releaseReadiness.LatestEvidencePack?.CaseId ?? releaseReadiness.LatestApprovalWebhook?.CaseId;
                if (!string.IsNullOrWhiteSpace(request.CaseId) && string.IsNullOrWhiteSpace(sourceCaseId))
                {
                    _logger.LogInformation(
                        "ComplianceEvents.GetEvents binding release-readiness event to requested case because no source case was present. RequestedCaseId={RequestedCaseId} HeadRef={HeadRef}",
                        LoggingHelper.SanitizeLogInput(request.CaseId),
                        LoggingHelper.SanitizeLogInput(request.HeadRef));
                }
                else if (!string.IsNullOrWhiteSpace(request.CaseId) &&
                    !string.IsNullOrWhiteSpace(sourceCaseId) &&
                    !string.Equals(request.CaseId, sourceCaseId, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "ComplianceEvents.GetEvents overriding release-readiness case binding. RequestedCaseId={RequestedCaseId} SourceCaseId={SourceCaseId} HeadRef={HeadRef}",
                        LoggingHelper.SanitizeLogInput(request.CaseId),
                        LoggingHelper.SanitizeLogInput(sourceCaseId),
                        LoggingHelper.SanitizeLogInput(request.HeadRef));
                }

                events.Add(MapReleaseReadinessEvent(releaseReadiness, request.CaseId, request.SubjectId));
            }

            IEnumerable<ComplianceEventEnvelope> filtered = events;

            if (!string.IsNullOrWhiteSpace(request.CaseId))
                filtered = filtered.Where(evt => string.Equals(evt.CaseId, request.CaseId, StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(request.SubjectId))
                filtered = filtered.Where(evt => string.Equals(evt.SubjectId, request.SubjectId, StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(request.EntityId))
                filtered = filtered.Where(evt => string.Equals(evt.EntityId, request.EntityId, StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(request.HeadRef))
                filtered = filtered.Where(evt => string.Equals(evt.HeadRef, request.HeadRef, StringComparison.Ordinal));

            if (request.EntityKind.HasValue)
                filtered = filtered.Where(evt => evt.EntityKind == request.EntityKind.Value);

            if (request.EventType.HasValue)
                filtered = filtered.Where(evt => evt.EventType == request.EventType.Value);

            if (request.Severity.HasValue)
                filtered = filtered.Where(evt => evt.Severity == request.Severity.Value);

            if (request.Source.HasValue)
                filtered = filtered.Where(evt => evt.Source == request.Source.Value);

            if (request.Freshness.HasValue)
                filtered = filtered.Where(evt => evt.Freshness == request.Freshness.Value);

            if (request.DeliveryStatus.HasValue)
                filtered = filtered.Where(evt => evt.DeliveryStatus == request.DeliveryStatus.Value);

            var ordered = filtered
                .OrderByDescending(evt => evt.Timestamp)
                .ThenByDescending(evt => evt.EventId, StringComparer.Ordinal)
                .ToList();

            var currentState = BuildCurrentStateSummary(ordered);

            bool hasVisibleReleaseReadinessEvent = ordered.Any(evt => evt.EventType == ComplianceEventType.ReleaseReadinessEvaluated);
            if (releaseReadiness != null && hasVisibleReleaseReadinessEvent)
            {
                currentState.ReleaseReadiness = releaseReadiness;
                currentState.CurrentFreshness = MapFreshness(releaseReadiness);
                currentState.CurrentDeliveryStatus = releaseReadiness.Mode == StrictArtifactMode.NotConfigured
                    ? ComplianceEventDeliveryStatus.NotConfigured
                    : currentState.CurrentDeliveryStatus;
                currentState.HasNotConfigured |= releaseReadiness.Mode == StrictArtifactMode.NotConfigured;
                currentState.HasStaleEvidence |= releaseReadiness.EvidenceFreshness == SignOffEvidenceFreshnessStatus.Stale;
                currentState.RecommendedAction ??= releaseReadiness.OperatorGuidance;
            }

            _logger.LogInformation(
                "ComplianceEvents.GetEvents actor={ActorId} total={TotalCount} page={Page} pageSize={PageSize}",
                actorId,
                ordered.Count,
                page,
                pageSize);

            return new ComplianceEventListResponse
            {
                Success = true,
                Events = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                TotalCount = ordered.Count,
                Page = page,
                PageSize = pageSize,
                CurrentState = currentState
            };
        }

        /// <inheritdoc/>
        public async Task<ComplianceEventQueueSummaryResponse> GetQueueSummaryAsync(ComplianceEventQueryRequest request, string actorId)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Reuse GetEventsAsync with the maximum page size to collect all matching events for counting.
            ComplianceEventQueryRequest summaryRequest = new()
            {
                CaseId = request.CaseId,
                SubjectId = request.SubjectId,
                EntityId = request.EntityId,
                HeadRef = request.HeadRef,
                EntityKind = request.EntityKind,
                EventType = request.EventType,
                Severity = request.Severity,
                Source = request.Source,
                Freshness = request.Freshness,
                DeliveryStatus = request.DeliveryStatus,
                Page = 1,
                PageSize = 100
            };

            ComplianceEventListResponse firstPage = await GetEventsAsync(summaryRequest, actorId);
            List<ComplianceEventEnvelope> all = firstPage.Events;

            // Paginate through remaining pages if the total exceeds the first page.
            int totalPages = (int)Math.Ceiling((double)firstPage.TotalCount / 100);
            for (int p = 2; p <= totalPages; p++)
            {
                summaryRequest.Page = p;
                ComplianceEventListResponse page = await GetEventsAsync(summaryRequest, actorId);
                all.AddRange(page.Events);
            }

            ComplianceEventQueueSummary summary = new()
            {
                BlockedCount = all.Count(evt => evt.Severity == ComplianceEventSeverity.Critical),
                ActionNeededCount = all.Count(evt => evt.Severity == ComplianceEventSeverity.Warning),
                InformationalCount = all.Count(evt => evt.Severity == ComplianceEventSeverity.Informational),
                WaitingOnProviderCount = all.Count(evt => evt.Freshness == ComplianceEventFreshness.AwaitingProviderCallback),
                StaleCount = all.Count(evt => evt.Freshness == ComplianceEventFreshness.Stale),
                NotConfiguredCount = all.Count(evt =>
                    evt.Freshness == ComplianceEventFreshness.NotConfigured ||
                    evt.DeliveryStatus == ComplianceEventDeliveryStatus.NotConfigured),
                UnavailableCount = all.Count(evt => evt.Freshness == ComplianceEventFreshness.Unavailable),
                FailedDeliveryCount = all.Count(evt => evt.DeliveryStatus == ComplianceEventDeliveryStatus.Failed),
                PendingDeliveryCount = all.Count(evt => evt.DeliveryStatus == ComplianceEventDeliveryStatus.Waiting),
                TotalCount = firstPage.TotalCount,
                RecommendedAction = firstPage.CurrentState.RecommendedAction,
                ComputedAt = DateTimeOffset.UtcNow
            };

            _logger.LogInformation(
                "ComplianceEvents.GetQueueSummary actor={ActorId} total={TotalCount} blocked={BlockedCount} actionNeeded={ActionNeededCount} waitingOnProvider={WaitingOnProviderCount} stale={StaleCount}",
                actorId,
                summary.TotalCount,
                summary.BlockedCount,
                summary.ActionNeededCount,
                summary.WaitingOnProviderCount,
                summary.StaleCount);

            return new ComplianceEventQueueSummaryResponse
            {
                Success = true,
                Summary = summary
            };
        }

        private async Task AppendComplianceCaseEventsAsync(List<ComplianceEventEnvelope> events, ComplianceEventQueryRequest request)
        {
            List<ComplianceCase> cases = await _complianceCaseRepository.QueryCasesAsync(caseRecord =>
                (string.IsNullOrWhiteSpace(request.CaseId) || caseRecord.CaseId == request.CaseId) &&
                (string.IsNullOrWhiteSpace(request.SubjectId) || caseRecord.SubjectId == request.SubjectId));

            foreach (ComplianceCase complianceCase in cases)
            {
                foreach (CaseTimelineEntry entry in complianceCase.Timeline)
                {
                    events.Add(MapComplianceCaseTimelineEvent(complianceCase, entry));
                }

                foreach (CaseDeliveryRecord record in complianceCase.DeliveryRecords)
                {
                    events.Add(MapComplianceDeliveryEvent(complianceCase, record));
                }

                if (request.CaseId == complianceCase.CaseId || string.IsNullOrWhiteSpace(request.CaseId))
                {
                    List<CaseExportMetadata> exports = await _complianceCaseRepository.GetExportRecordsAsync(complianceCase.CaseId);
                    foreach (CaseExportMetadata export in exports)
                    {
                        events.Add(new ComplianceEventEnvelope
                        {
                            EventId = $"case-export-{export.ExportId}",
                            EventType = ComplianceEventType.ComplianceEvidenceGenerated,
                            EntityKind = ComplianceEventEntityKind.ComplianceCase,
                            EntityId = export.ExportId,
                            CaseId = complianceCase.CaseId,
                            SubjectId = complianceCase.SubjectId,
                            Timestamp = export.ExportedAt,
                            ActorId = export.ExportedBy,
                            Label = "Case evidence exported",
                            Summary = $"Case evidence bundle exported in {export.Format} format.",
                            Severity = ComplianceEventSeverity.Informational,
                            Source = ComplianceEventSource.Evidence,
                            Freshness = ComplianceEventFreshness.Historical,
                            DeliveryStatus = ComplianceEventDeliveryStatus.NotAttempted,
                            Payload = new Dictionary<string, string>
                            {
                                ["exportId"] = export.ExportId,
                                ["format"] = export.Format,
                                ["contentHash"] = export.ContentHash ?? string.Empty
                            }
                        });
                    }
                }
            }
        }

        private async Task AppendOnboardingEventsAsync(List<ComplianceEventEnvelope> events, ComplianceEventQueryRequest request)
        {
            ListOnboardingCasesResponse response = await _onboardingCaseService.ListCasesAsync(new ListOnboardingCasesRequest
            {
                SubjectId = request.SubjectId,
                PageSize = 500
            });

            if (!response.Success)
            {
                return;
            }

            IEnumerable<KycAmlOnboardingCase> cases = response.Cases;
            if (!string.IsNullOrWhiteSpace(request.CaseId))
            {
                cases = cases.Where(onboardingCase => onboardingCase.CaseId == request.CaseId);
            }

            foreach (KycAmlOnboardingCase onboardingCase in cases)
            {
                foreach (KycAmlOnboardingTimelineEvent entry in onboardingCase.Timeline)
                {
                    events.Add(MapOnboardingEvent(onboardingCase, entry));
                }
            }
        }

        private async Task AppendProtectedSignOffEventsAsync(List<ComplianceEventEnvelope> events, ComplianceEventQueryRequest request)
        {
            GetApprovalWebhookHistoryResponse webhookHistory = await _protectedSignOffEvidenceService.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest
                {
                    CaseId = request.CaseId,
                    HeadRef = request.HeadRef,
                    MaxRecords = 200
                });

            if (webhookHistory.Success)
            {
                foreach (ApprovalWebhookRecord record in webhookHistory.Records)
                {
                    events.Add(MapApprovalWebhookEvent(record));
                }
            }

            GetEvidencePackHistoryResponse evidenceHistory = await _protectedSignOffEvidenceService.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest
                {
                    CaseId = request.CaseId,
                    HeadRef = request.HeadRef,
                    MaxRecords = 200
                });

            if (evidenceHistory.Success)
            {
                foreach (ProtectedSignOffEvidencePack pack in evidenceHistory.Packs)
                {
                    events.Add(MapEvidencePackEvent(pack));
                }
            }
        }

        private async Task AppendComplianceAuditExportEventsAsync(List<ComplianceEventEnvelope> events, ComplianceEventQueryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SubjectId))
            {
                return;
            }

            ListComplianceAuditExportsResponse exports = await _complianceAuditExportService.ListExportsAsync(request.SubjectId, limit: 100);
            if (!exports.Success)
            {
                return;
            }

            foreach (ComplianceAuditExportSummary export in exports.Exports)
            {
                events.Add(new ComplianceEventEnvelope
                {
                    EventId = $"audit-export-{export.ExportId}",
                    EventType = ComplianceEventType.ComplianceAuditExportGenerated,
                    EntityKind = ComplianceEventEntityKind.ComplianceAuditExport,
                    EntityId = export.ExportId,
                    SubjectId = export.SubjectId,
                    Timestamp = new DateTimeOffset(DateTime.SpecifyKind(export.AssembledAt, DateTimeKind.Utc)),
                    Label = "Compliance audit export assembled",
                    Summary = $"Audit export assembled for scenario {export.Scenario}.",
                    Severity = MapSeverity(export.Readiness),
                    Source = ComplianceEventSource.Evidence,
                    Freshness = MapFreshness(export.Readiness),
                    DeliveryStatus = ComplianceEventDeliveryStatus.NotAttempted,
                    Payload = new Dictionary<string, string>
                    {
                        ["scenario"] = export.Scenario.ToString(),
                        ["readiness"] = export.Readiness.ToString(),
                        ["contentHash"] = export.ContentHash
                    }
                });
            }
        }

        private static ComplianceEventEnvelope MapComplianceCaseTimelineEvent(ComplianceCase complianceCase, CaseTimelineEntry entry)
        {
            (ComplianceEventType eventType, string label) = entry.EventType switch
            {
                CaseTimelineEventType.CaseCreated => (ComplianceEventType.ComplianceCaseCreated, "Compliance case created"),
                CaseTimelineEventType.EvidenceAdded => (ComplianceEventType.ComplianceCaseEvidenceUpdated, "Compliance evidence updated"),
                CaseTimelineEventType.EvidenceStale => (ComplianceEventType.ComplianceCaseEvidenceStale, "Compliance evidence stale"),
                CaseTimelineEventType.EscalationRaised => (ComplianceEventType.ComplianceEscalationRaised, "Compliance escalation raised"),
                CaseTimelineEventType.EscalationResolved => (ComplianceEventType.ComplianceEscalationResolved, "Compliance escalation resolved"),
                CaseTimelineEventType.CaseExported => (ComplianceEventType.ComplianceEvidenceGenerated, "Compliance evidence exported"),
                CaseTimelineEventType.DecisionRecorded or CaseTimelineEventType.ApprovalDecisionRecorded or CaseTimelineEventType.RejectionDecisionRecorded
                    => (ComplianceEventType.ComplianceDecisionRecorded, "Compliance decision recorded"),
                _ => (ComplianceEventType.ComplianceCaseStateChanged, "Compliance case updated")
            };

            return new ComplianceEventEnvelope
            {
                EventId = entry.EntryId,
                EventType = eventType,
                EntityKind = ComplianceEventEntityKind.ComplianceCase,
                EntityId = complianceCase.CaseId,
                CaseId = complianceCase.CaseId,
                SubjectId = complianceCase.SubjectId,
                Timestamp = entry.OccurredAt,
                ActorId = entry.ActorId,
                CorrelationId = entry.CorrelationId,
                Label = label,
                Summary = entry.Description,
                Severity = MapSeverity(entry.EventType, entry.ToState ?? complianceCase.State),
                Source = MapSource(entry.EventType),
                Freshness = MapFreshness(entry.EventType),
                DeliveryStatus = ComplianceEventDeliveryStatus.NotAttempted,
                Payload = BuildCaseTimelinePayload(entry)
            };
        }

        private static ComplianceEventEnvelope MapComplianceDeliveryEvent(ComplianceCase complianceCase, CaseDeliveryRecord record)
        {
            return new ComplianceEventEnvelope
            {
                EventId = record.DeliveryId,
                EventType = ComplianceEventType.NotificationDeliveryUpdated,
                EntityKind = ComplianceEventEntityKind.NotificationDelivery,
                EntityId = record.DeliveryId,
                CaseId = record.CaseId,
                SubjectId = complianceCase.SubjectId,
                Timestamp = record.AttemptedAt,
                Label = "Notification delivery updated",
                Summary = $"Delivery outcome for {record.EventType}: {record.Outcome}.",
                RecommendedAction = record.RecommendedAction,
                Severity = MapSeverity(record.Outcome),
                Source = ComplianceEventSource.Webhook,
                Freshness = ComplianceEventFreshness.Historical,
                DeliveryStatus = MapDeliveryStatus(record.Outcome),
                Payload = new Dictionary<string, string>
                {
                    ["eventId"] = record.EventId,
                    ["eventType"] = record.EventType.ToString(),
                    ["httpStatusCode"] = record.HttpStatusCode?.ToString() ?? string.Empty,
                    ["attemptCount"] = record.AttemptCount.ToString(),
                    ["lastError"] = record.LastErrorSummary ?? string.Empty
                }
            };
        }

        private static ComplianceEventEnvelope MapOnboardingEvent(
            KycAmlOnboardingCase onboardingCase,
            KycAmlOnboardingTimelineEvent entry)
        {
            (ComplianceEventType eventType, string label, ComplianceEventSeverity severity, ComplianceEventSource source, ComplianceEventFreshness freshness) = entry.EventType switch
            {
                KycAmlOnboardingTimelineEventType.CaseCreated => (
                    ComplianceEventType.OnboardingCaseCreated,
                    "Onboarding case created",
                    ComplianceEventSeverity.Informational,
                    ComplianceEventSource.Operator,
                    ComplianceEventFreshness.Historical),
                KycAmlOnboardingTimelineEventType.ProviderChecksInitiated => (
                    ComplianceEventType.OnboardingProviderChecksInitiated,
                    "Onboarding provider checks initiated",
                    ComplianceEventSeverity.Warning,
                    ComplianceEventSource.Provider,
                    ComplianceEventFreshness.AwaitingProviderCallback),
                KycAmlOnboardingTimelineEventType.ProviderConfigurationMissing => (
                    ComplianceEventType.OnboardingNotConfigured,
                    "Onboarding provider not configured",
                    ComplianceEventSeverity.Critical,
                    ComplianceEventSource.System,
                    ComplianceEventFreshness.NotConfigured),
                KycAmlOnboardingTimelineEventType.ProviderUnavailable => (
                    ComplianceEventType.OnboardingProviderUnavailable,
                    "Onboarding provider unavailable",
                    ComplianceEventSeverity.Critical,
                    ComplianceEventSource.Provider,
                    ComplianceEventFreshness.Unavailable),
                _ => (
                    ComplianceEventType.OnboardingReviewerActionRecorded,
                    "Onboarding reviewer action recorded",
                    MapSeverityFromOnboardingState(entry.ToState ?? onboardingCase.State),
                    ComplianceEventSource.Operator,
                    MapFreshness(onboardingCase.EvidenceState))
            };

            return new ComplianceEventEnvelope
            {
                EventId = entry.EventId,
                EventType = eventType,
                EntityKind = ComplianceEventEntityKind.OnboardingCase,
                EntityId = onboardingCase.CaseId,
                CaseId = onboardingCase.CaseId,
                SubjectId = onboardingCase.SubjectId,
                Timestamp = entry.OccurredAt,
                ActorId = entry.ActorId,
                CorrelationId = entry.CorrelationId ?? onboardingCase.CorrelationId,
                Label = label,
                Summary = entry.Summary,
                Severity = severity,
                Source = source,
                Freshness = freshness,
                DeliveryStatus = ComplianceEventDeliveryStatus.NotAttempted,
                Payload = BuildOnboardingPayload(entry, onboardingCase)
            };
        }

        private static ComplianceEventEnvelope MapApprovalWebhookEvent(ApprovalWebhookRecord record)
        {
            return new ComplianceEventEnvelope
            {
                EventId = record.RecordId,
                EventType = ComplianceEventType.ProtectedSignOffApprovalWebhookRecorded,
                EntityKind = ComplianceEventEntityKind.ProtectedSignOffWebhook,
                EntityId = record.RecordId,
                CaseId = record.CaseId,
                HeadRef = record.HeadRef,
                Timestamp = record.ReceivedAt,
                ActorId = record.ActorId,
                CorrelationId = record.CorrelationId,
                Label = "Protected sign-off webhook recorded",
                Summary = $"Protected sign-off webhook outcome recorded: {record.Outcome}.",
                RecommendedAction = record.IsValid ? null : record.ValidationError,
                Severity = MapSeverity(record.Outcome),
                Source = ComplianceEventSource.Webhook,
                Freshness = record.IsValid ? ComplianceEventFreshness.Current : ComplianceEventFreshness.PartialEvidence,
                DeliveryStatus = ComplianceEventDeliveryStatus.Sent,
                Payload = new Dictionary<string, string>(record.Metadata)
                {
                    ["outcome"] = record.Outcome.ToString(),
                    ["isValid"] = record.IsValid.ToString(),
                    ["reason"] = record.Reason ?? string.Empty
                }
            };
        }

        private static ComplianceEventEnvelope MapEvidencePackEvent(ProtectedSignOffEvidencePack pack)
        {
            return new ComplianceEventEnvelope
            {
                EventId = pack.PackId,
                EventType = ComplianceEventType.ProtectedSignOffEvidenceCaptured,
                EntityKind = ComplianceEventEntityKind.ProtectedSignOffEvidence,
                EntityId = pack.PackId,
                CaseId = pack.CaseId,
                HeadRef = pack.HeadRef,
                Timestamp = pack.CreatedAt,
                ActorId = pack.CreatedBy,
                CorrelationId = pack.ApprovalWebhook?.CorrelationId,
                Label = "Protected sign-off evidence captured",
                Summary = $"Protected sign-off evidence pack captured for head {LoggingHelper.SanitizeLogInput(pack.HeadRef)}.",
                Severity = pack.IsReleaseGrade && pack.FreshnessStatus == SignOffEvidenceFreshnessStatus.Complete
                    ? ComplianceEventSeverity.Informational
                    : ComplianceEventSeverity.Warning,
                Source = ComplianceEventSource.ProtectedEnvironment,
                Freshness = MapFreshness(pack.FreshnessStatus),
                DeliveryStatus = pack.ApprovalWebhook == null
                    ? ComplianceEventDeliveryStatus.Waiting
                    : ComplianceEventDeliveryStatus.Sent,
                Payload = new Dictionary<string, string>
                {
                    ["headRef"] = pack.HeadRef,
                    ["freshnessStatus"] = pack.FreshnessStatus.ToString(),
                    ["isReleaseGrade"] = pack.IsReleaseGrade.ToString(),
                    ["isProviderBacked"] = pack.IsProviderBacked.ToString(),
                    ["environmentLabel"] = pack.EnvironmentLabel ?? string.Empty,
                    ["contentHash"] = pack.ContentHash ?? string.Empty
                }
            };
        }

        private static ComplianceEventEnvelope MapReleaseReadinessEvent(
            GetSignOffReleaseReadinessResponse readiness,
            string? requestedCaseId = null,
            string? requestedSubjectId = null)
        {
            return new ComplianceEventEnvelope
            {
                EventId = $"release-readiness-{readiness.HeadRef}-{readiness.EvaluatedAt:O}",
                EventType = ComplianceEventType.ReleaseReadinessEvaluated,
                EntityKind = ComplianceEventEntityKind.ReleaseReadiness,
                EntityId = readiness.HeadRef,
                CaseId = string.IsNullOrWhiteSpace(requestedCaseId)
                    ? readiness.LatestEvidencePack?.CaseId ?? readiness.LatestApprovalWebhook?.CaseId
                    : requestedCaseId,
                SubjectId = requestedSubjectId,
                HeadRef = readiness.HeadRef,
                Timestamp = readiness.EvaluatedAt,
                ActorId = readiness.LatestApprovalWebhook?.ActorId ?? readiness.LatestEvidencePack?.CreatedBy,
                CorrelationId = readiness.LatestApprovalWebhook?.CorrelationId,
                Label = "Release readiness evaluated",
                Summary = $"Release readiness evaluated for head {LoggingHelper.SanitizeLogInput(readiness.HeadRef)}: {readiness.Status}.",
                RecommendedAction = readiness.OperatorGuidance,
                Severity = readiness.Status == SignOffReleaseReadinessStatus.Ready
                    ? ComplianceEventSeverity.Informational
                    : ComplianceEventSeverity.Critical,
                Source = ComplianceEventSource.ProtectedEnvironment,
                Freshness = MapFreshness(readiness),
                DeliveryStatus = readiness.Mode == StrictArtifactMode.NotConfigured
                    ? ComplianceEventDeliveryStatus.NotConfigured
                    : readiness.HasApprovalWebhook
                        ? ComplianceEventDeliveryStatus.Sent
                        : ComplianceEventDeliveryStatus.Waiting,
                Payload = new Dictionary<string, string>
                {
                    ["status"] = readiness.Status.ToString(),
                    ["mode"] = readiness.Mode.ToString(),
                    ["evidenceFreshness"] = readiness.EvidenceFreshness.ToString(),
                    ["hasApprovalWebhook"] = readiness.HasApprovalWebhook.ToString(),
                    ["environmentLabel"] = readiness.EnvironmentLabel ?? string.Empty,
                    ["operatorGuidance"] = readiness.OperatorGuidance ?? string.Empty
                }
            };
        }

        private static ComplianceEventStateSummary BuildCurrentStateSummary(List<ComplianceEventEnvelope> events)
        {
            ComplianceEventEnvelope? mostSevere = events
                .OrderByDescending(evt => evt.Severity)
                .ThenByDescending(evt => evt.Timestamp)
                .FirstOrDefault();

            bool hasSuccessfulDelivery = events.Any(evt => evt.DeliveryStatus == ComplianceEventDeliveryStatus.Sent);
            bool hasDegradedDelivery = events.Any(evt =>
                evt.DeliveryStatus is ComplianceEventDeliveryStatus.Failed
                or ComplianceEventDeliveryStatus.Waiting
                or ComplianceEventDeliveryStatus.NotConfigured);

            return new ComplianceEventStateSummary
            {
                TotalEvents = events.Count,
                LatestEventAt = events.FirstOrDefault()?.Timestamp,
                HighestSeverity = mostSevere?.Severity ?? ComplianceEventSeverity.Informational,
                CurrentDeliveryStatus = ResolveDeliveryStatus(events),
                CurrentFreshness = ResolveFreshness(events),
                HasFailedDelivery = events.Any(evt => evt.DeliveryStatus == ComplianceEventDeliveryStatus.Failed),
                HasPendingDelivery = events.Any(evt => evt.DeliveryStatus == ComplianceEventDeliveryStatus.Waiting),
                HasPartialDelivery = hasSuccessfulDelivery && hasDegradedDelivery,
                HasNotConfigured = events.Any(evt => evt.Freshness == ComplianceEventFreshness.NotConfigured || evt.DeliveryStatus == ComplianceEventDeliveryStatus.NotConfigured),
                HasStaleEvidence = events.Any(evt => evt.Freshness == ComplianceEventFreshness.Stale),
                HasAwaitingProviderCallback = events.Any(evt => evt.Freshness == ComplianceEventFreshness.AwaitingProviderCallback),
                RecommendedAction = mostSevere?.RecommendedAction ?? mostSevere?.Summary
            };
        }

        private static ComplianceEventDeliveryStatus ResolveDeliveryStatus(IEnumerable<ComplianceEventEnvelope> events)
        {
            if (events.Any(evt => evt.DeliveryStatus == ComplianceEventDeliveryStatus.NotConfigured))
                return ComplianceEventDeliveryStatus.NotConfigured;
            if (events.Any(evt => evt.DeliveryStatus == ComplianceEventDeliveryStatus.Failed))
                return ComplianceEventDeliveryStatus.Failed;
            if (events.Any(evt => evt.DeliveryStatus == ComplianceEventDeliveryStatus.Waiting))
                return ComplianceEventDeliveryStatus.Waiting;
            if (events.Any(evt => evt.DeliveryStatus == ComplianceEventDeliveryStatus.Sent))
                return ComplianceEventDeliveryStatus.Sent;
            if (events.Any(evt => evt.DeliveryStatus == ComplianceEventDeliveryStatus.Skipped))
                return ComplianceEventDeliveryStatus.Skipped;
            return ComplianceEventDeliveryStatus.NotAttempted;
        }

        private static ComplianceEventFreshness ResolveFreshness(IEnumerable<ComplianceEventEnvelope> events)
        {
            if (events.Any(evt => evt.Freshness == ComplianceEventFreshness.NotConfigured))
                return ComplianceEventFreshness.NotConfigured;
            if (events.Any(evt => evt.Freshness == ComplianceEventFreshness.Stale))
                return ComplianceEventFreshness.Stale;
            if (events.Any(evt => evt.Freshness == ComplianceEventFreshness.Unavailable))
                return ComplianceEventFreshness.Unavailable;
            if (events.Any(evt => evt.Freshness == ComplianceEventFreshness.PartialEvidence))
                return ComplianceEventFreshness.PartialEvidence;
            if (events.Any(evt => evt.Freshness == ComplianceEventFreshness.AwaitingProviderCallback))
                return ComplianceEventFreshness.AwaitingProviderCallback;
            if (events.Any(evt => evt.Freshness == ComplianceEventFreshness.Current))
                return ComplianceEventFreshness.Current;
            return ComplianceEventFreshness.Historical;
        }

        private static ComplianceEventSeverity MapSeverity(CaseTimelineEventType eventType, ComplianceCaseState currentState)
        {
            return eventType switch
            {
                CaseTimelineEventType.EvidenceStale => ComplianceEventSeverity.Warning,
                CaseTimelineEventType.EscalationRaised => ComplianceEventSeverity.Critical,
                CaseTimelineEventType.RejectionDecisionRecorded => ComplianceEventSeverity.Critical,
                CaseTimelineEventType.ReturnedForInformation => ComplianceEventSeverity.Warning,
                CaseTimelineEventType.StateTransition when currentState is ComplianceCaseState.Blocked or ComplianceCaseState.Rejected => ComplianceEventSeverity.Critical,
                CaseTimelineEventType.StateTransition when currentState is ComplianceCaseState.Escalated or ComplianceCaseState.Remediating or ComplianceCaseState.Stale => ComplianceEventSeverity.Warning,
                _ => ComplianceEventSeverity.Informational
            };
        }

        private static ComplianceEventSeverity MapSeverity(CaseDeliveryOutcome outcome)
        {
            return outcome switch
            {
                CaseDeliveryOutcome.Failure or CaseDeliveryOutcome.RetryExhausted => ComplianceEventSeverity.Critical,
                CaseDeliveryOutcome.RetryScheduled or CaseDeliveryOutcome.Pending => ComplianceEventSeverity.Warning,
                _ => ComplianceEventSeverity.Informational
            };
        }

        private static ComplianceEventSeverity MapSeverity(ApprovalWebhookOutcome outcome)
        {
            return outcome switch
            {
                ApprovalWebhookOutcome.Approved => ComplianceEventSeverity.Informational,
                ApprovalWebhookOutcome.Escalated or ApprovalWebhookOutcome.Denied => ComplianceEventSeverity.Critical,
                _ => ComplianceEventSeverity.Warning
            };
        }

        private static ComplianceEventSeverity MapSeverity(AuditExportReadiness readiness)
        {
            return readiness switch
            {
                AuditExportReadiness.Ready => ComplianceEventSeverity.Informational,
                AuditExportReadiness.RequiresReview or AuditExportReadiness.PartiallyAvailable => ComplianceEventSeverity.Warning,
                _ => ComplianceEventSeverity.Critical
            };
        }

        private static ComplianceEventSeverity MapSeverityFromOnboardingState(KycAmlOnboardingCaseState state)
        {
            return state switch
            {
                KycAmlOnboardingCaseState.Approved => ComplianceEventSeverity.Informational,
                KycAmlOnboardingCaseState.PendingReview or KycAmlOnboardingCaseState.UnderReview or KycAmlOnboardingCaseState.RequiresAdditionalInfo => ComplianceEventSeverity.Warning,
                KycAmlOnboardingCaseState.Rejected or KycAmlOnboardingCaseState.Escalated or KycAmlOnboardingCaseState.ProviderUnavailable or KycAmlOnboardingCaseState.ConfigurationMissing => ComplianceEventSeverity.Critical,
                _ => ComplianceEventSeverity.Informational
            };
        }

        private static ComplianceEventSource MapSource(CaseTimelineEventType eventType)
        {
            return eventType switch
            {
                CaseTimelineEventType.CaseExported => ComplianceEventSource.Evidence,
                CaseTimelineEventType.EvidenceStale => ComplianceEventSource.System,
                CaseTimelineEventType.EscalationRaised or CaseTimelineEventType.EscalationResolved => ComplianceEventSource.Operator,
                _ => ComplianceEventSource.Operator
            };
        }

        private static ComplianceEventFreshness MapFreshness(CaseTimelineEventType eventType)
        {
            return eventType switch
            {
                CaseTimelineEventType.EvidenceStale => ComplianceEventFreshness.Stale,
                CaseTimelineEventType.ReadinessChanged => ComplianceEventFreshness.PartialEvidence,
                _ => ComplianceEventFreshness.Historical
            };
        }

        private static ComplianceEventFreshness MapFreshness(KycAmlOnboardingEvidenceState state)
        {
            return state switch
            {
                KycAmlOnboardingEvidenceState.AuthoritativeProviderBacked => ComplianceEventFreshness.Current,
                KycAmlOnboardingEvidenceState.PendingVerification => ComplianceEventFreshness.AwaitingProviderCallback,
                KycAmlOnboardingEvidenceState.DegradedPartialEvidence => ComplianceEventFreshness.PartialEvidence,
                KycAmlOnboardingEvidenceState.StaleEvidence => ComplianceEventFreshness.Stale,
                KycAmlOnboardingEvidenceState.MissingConfiguration => ComplianceEventFreshness.NotConfigured,
                _ => ComplianceEventFreshness.Unavailable
            };
        }

        private static ComplianceEventFreshness MapFreshness(SignOffEvidenceFreshnessStatus freshnessStatus)
        {
            return freshnessStatus switch
            {
                SignOffEvidenceFreshnessStatus.Complete => ComplianceEventFreshness.Current,
                SignOffEvidenceFreshnessStatus.Partial => ComplianceEventFreshness.PartialEvidence,
                SignOffEvidenceFreshnessStatus.Stale => ComplianceEventFreshness.Stale,
                SignOffEvidenceFreshnessStatus.Unavailable => ComplianceEventFreshness.Unavailable,
                _ => ComplianceEventFreshness.PartialEvidence
            };
        }

        private static ComplianceEventFreshness MapFreshness(AuditExportReadiness readiness)
        {
            return readiness switch
            {
                AuditExportReadiness.Ready => ComplianceEventFreshness.Current,
                AuditExportReadiness.Stale => ComplianceEventFreshness.Stale,
                AuditExportReadiness.DegradedProviderUnavailable => ComplianceEventFreshness.Unavailable,
                AuditExportReadiness.PartiallyAvailable or AuditExportReadiness.RequiresReview => ComplianceEventFreshness.PartialEvidence,
                _ => ComplianceEventFreshness.NotConfigured
            };
        }

        private static ComplianceEventFreshness MapFreshness(GetSignOffReleaseReadinessResponse readiness)
            => readiness.Mode == StrictArtifactMode.NotConfigured
                ? ComplianceEventFreshness.NotConfigured
                : MapFreshness(readiness.EvidenceFreshness);

        private static ComplianceEventDeliveryStatus MapDeliveryStatus(CaseDeliveryOutcome outcome)
        {
            return outcome switch
            {
                CaseDeliveryOutcome.Success => ComplianceEventDeliveryStatus.Sent,
                CaseDeliveryOutcome.Pending or CaseDeliveryOutcome.RetryScheduled => ComplianceEventDeliveryStatus.Waiting,
                CaseDeliveryOutcome.Failure or CaseDeliveryOutcome.RetryExhausted => ComplianceEventDeliveryStatus.Failed,
                _ => ComplianceEventDeliveryStatus.NotAttempted
            };
        }

        private static Dictionary<string, string> BuildCaseTimelinePayload(CaseTimelineEntry entry)
        {
            Dictionary<string, string> payload = new(entry.Metadata);
            if (entry.FromState.HasValue)
            {
                payload["fromState"] = entry.FromState.Value.ToString();
            }

            if (entry.ToState.HasValue)
            {
                payload["toState"] = entry.ToState.Value.ToString();
            }

            return payload;
        }

        private static Dictionary<string, string> BuildOnboardingPayload(
            KycAmlOnboardingTimelineEvent entry,
            KycAmlOnboardingCase onboardingCase)
        {
            Dictionary<string, string> payload = new(entry.Metadata)
            {
                ["subjectId"] = onboardingCase.SubjectId,
                ["evidenceState"] = onboardingCase.EvidenceState.ToString()
            };

            if (entry.FromState.HasValue)
            {
                payload["fromState"] = entry.FromState.Value.ToString();
            }

            if (entry.ToState.HasValue)
            {
                payload["toState"] = entry.ToState.Value.ToString();
            }

            return payload;
        }
    }
}
