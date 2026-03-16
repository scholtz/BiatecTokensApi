using BiatecTokensApi.Helpers;
using BiatecTokensApi.Models.ComplianceOperations;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services.Interface;
using System.Collections.Concurrent;

namespace BiatecTokensApi.Services
{
    /// <summary>
    /// In-memory implementation of the compliance operations orchestration layer.
    ///
    /// Maintains a thread-safe queue of <see cref="ComplianceOpsQueueItem"/> instances
    /// seeded from scheduled-reporting, approval-workflow, compliance-case, and
    /// monitoring signals. Provides fail-closed SLA classification, role-aware
    /// aggregation, and webhook emission for overdue/blocked state transitions.
    ///
    /// Designed so a future persistent back-end can be substituted without changing
    /// the API contract.
    /// </summary>
    public class ComplianceOperationsService : IComplianceOperationsService
    {
        private readonly ILogger<ComplianceOperationsService> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly IWebhookService? _webhookService;

        // ── Warning window ─────────────────────────────────────────────────────
        private static readonly TimeSpan DueSoonWindow = TimeSpan.FromDays(3);

        // ── Priority tiers (base scores per SLA bucket) ────────────────────────
        private const int BlockedBaseScore  = 3000;
        private const int OverdueBaseScore  = 2000;
        private const int DueSoonBaseScore  = 1000;
        private const int OnTrackBaseScore  =    0;
        private const int FailClosedBonus   =  500;

        // ── In-memory store ────────────────────────────────────────────────────
        private readonly ConcurrentDictionary<string, ComplianceOpsQueueItem> _queue = new();

        /// <summary>
        /// Initialises the service.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="timeProvider">Substitutable time provider (inject fake for tests).</param>
        /// <param name="webhookService">Optional webhook service for state-transition events.</param>
        public ComplianceOperationsService(
            ILogger<ComplianceOperationsService> logger,
            TimeProvider? timeProvider = null,
            IWebhookService? webhookService = null)
        {
            _logger = logger;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _webhookService = webhookService;
        }

        // ── GetOverviewAsync ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ComplianceOperationsOverviewResponse> GetOverviewAsync(
            string actorId, string correlationId)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Refresh SLA classification for all items before aggregating
            foreach (var item in _queue.Values)
            {
                RefreshItemSla(item, now);
            }

            var allItems = _queue.Values.ToList();

            var overview = new ComplianceOperationsOverview
            {
                GeneratedAt    = now,
                TotalQueueItems = allItems.Count,
                OnTrackCount   = allItems.Count(i => i.SlaStatus == ComplianceOpsSlaStatus.OnTrack),
                DueSoonCount   = allItems.Count(i => i.SlaStatus == ComplianceOpsSlaStatus.DueSoon),
                OverdueCount   = allItems.Count(i => i.SlaStatus == ComplianceOpsSlaStatus.Overdue),
                BlockedCount   = allItems.Count(i => i.SlaStatus == ComplianceOpsSlaStatus.Blocked),
                FailClosedCount = allItems.Count(i => i.IsFailClosed),
                CountByWorkflowType   = BuildWorkflowTypeCounts(allItems),
                CountByBlockerCategory = BuildBlockerCounts(allItems),
                RoleSummaries  = BuildRoleSummaries(allItems)
            };

            // Derive overall health
            DeriveHealth(overview);

            _logger.LogInformation(
                "ComplianceOperationsOverview computed. Total={Total} Blocked={Blocked} Actor={Actor} CorrelationId={CorrelationId}",
                overview.TotalQueueItems,
                overview.BlockedCount,
                LoggingHelper.SanitizeLogInput(actorId),
                LoggingHelper.SanitizeLogInput(correlationId));

            return Task.FromResult(new ComplianceOperationsOverviewResponse
            {
                Success  = true,
                Overview = overview
            });
        }

        // ── GetQueueAsync ───────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Task<ComplianceOpsQueueResponse> GetQueueAsync(
            ComplianceOpsQueueRequest request, string actorId, string correlationId)
        {
            if (request == null)
            {
                return Task.FromResult(new ComplianceOpsQueueResponse
                {
                    Success      = false,
                    ErrorCode    = "MISSING_REQUIRED_FIELD",
                    ErrorMessage = "Request is required."
                });
            }

            int pageSize = Math.Clamp(request.PageSize, 1, 200);
            int page     = Math.Max(request.Page, 0);

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Refresh SLA for all items first
            foreach (var item in _queue.Values)
            {
                RefreshItemSla(item, now);
            }

            var filtered = _queue.Values.AsEnumerable();

            // Apply role filter
            if (request.Role.HasValue)
                filtered = filtered.Where(i => i.OwnerRole == request.Role.Value);

            // Apply workflow type filter
            if (request.WorkflowType.HasValue)
                filtered = filtered.Where(i => i.WorkflowType == request.WorkflowType.Value);

            // Apply SLA status filter
            if (request.SlaStatus.HasValue)
                filtered = filtered.Where(i => i.SlaStatus == request.SlaStatus.Value);

            // Fail-closed only
            if (request.FailClosedOnly)
                filtered = filtered.Where(i => i.IsFailClosed);

            var matched = filtered.ToList();
            int total   = matched.Count;

            // Sort: highest PriorityScore first; then by CreatedAt (oldest first within tier)
            var sorted = matched
                .OrderByDescending(i => i.PriorityScore)
                .ThenBy(i => i.CreatedAt)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToList();

            _logger.LogInformation(
                "ComplianceOpsQueue retrieved. Total={Total} Returned={Returned} Role={Role} Actor={Actor}",
                total,
                sorted.Count,
                LoggingHelper.SanitizeLogInput(request.Role?.ToString() ?? "All"),
                LoggingHelper.SanitizeLogInput(actorId));

            return Task.FromResult(new ComplianceOpsQueueResponse
            {
                Success           = true,
                Items             = sorted,
                TotalCount        = total,
                AppliedRoleFilter = request.Role,
                GeneratedAt       = now
            });
        }

        // ── UpsertQueueItemAsync ────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task UpsertQueueItemAsync(
            ComplianceOpsQueueItem item, string actorId, string correlationId)
        {
            if (item == null) return;

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Classify SLA based on current time
            if (item.BlockerCategory != ComplianceOpsBlockerCategory.None)
                item.SlaStatus = ComplianceOpsSlaStatus.Blocked;
            else
                item.SlaStatus = ClassifySlaStatus(item.DueAt, now);

            item.HoursUntilDue = item.DueAt.HasValue
                ? (item.DueAt.Value - now).TotalHours
                : null;

            item.PriorityScore   = ComputePriorityScore(item.SlaStatus, item.IsFailClosed, item.CreatedAt, now);
            item.LastUpdatedAt   = now;

            // Detect SLA transition for webhook emission
            ComplianceOpsSlaStatus? previousStatus = null;
            if (_queue.TryGetValue(item.ItemId, out var existing))
                previousStatus = existing.SlaStatus;

            _queue[item.ItemId] = item;

            // Emit webhook on new Overdue or Blocked state
            if (_webhookService != null && previousStatus != item.SlaStatus)
            {
                WebhookEventType? eventType = item.SlaStatus switch
                {
                    ComplianceOpsSlaStatus.Overdue => WebhookEventType.ComplianceOpsItemOverdue,
                    ComplianceOpsSlaStatus.Blocked => WebhookEventType.ComplianceOpsItemBlocked,
                    _                              => null
                };

                if (eventType.HasValue)
                {
                    var payload = new ComplianceOpsStateTransitionEvent
                    {
                        ItemId         = item.ItemId,
                        SourceId       = item.SourceId,
                        WorkflowType   = item.WorkflowType,
                        FromStatus     = previousStatus ?? ComplianceOpsSlaStatus.OnTrack,
                        ToStatus       = item.SlaStatus,
                        BlockerCategory = item.BlockerCategory,
                        ActorId        = actorId,
                        OccurredAt     = now,
                        CorrelationId  = correlationId
                    };

                    await _webhookService.EmitEventAsync(new WebhookEvent
                    {
                        EventType = eventType.Value,
                        Actor     = actorId,
                        Timestamp = now,
                        Data      = new Dictionary<string, object>
                        {
                            ["itemId"]          = payload.ItemId,
                            ["sourceId"]        = payload.SourceId,
                            ["workflowType"]    = payload.WorkflowType.ToString(),
                            ["fromStatus"]      = payload.FromStatus.ToString(),
                            ["toStatus"]        = payload.ToStatus.ToString(),
                            ["blockerCategory"] = payload.BlockerCategory.ToString(),
                            ["correlationId"]   = correlationId
                        }
                    });

                    _logger.LogInformation(
                        "ComplianceOps webhook emitted. Event={Event} ItemId={ItemId} Actor={Actor}",
                        eventType.Value,
                        LoggingHelper.SanitizeLogInput(item.ItemId),
                        LoggingHelper.SanitizeLogInput(actorId));
                }
            }
        }

        // ── ResolveQueueItemAsync ───────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task<bool> ResolveQueueItemAsync(
            string itemId, string actorId, string correlationId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            if (!_queue.TryRemove(itemId, out var removed))
                return false;

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (_webhookService != null)
            {
                await _webhookService.EmitEventAsync(new WebhookEvent
                {
                    EventType = WebhookEventType.ComplianceOpsItemResolved,
                    Actor     = actorId,
                    Timestamp = now,
                    Data      = new Dictionary<string, object>
                    {
                        ["itemId"]        = removed.ItemId,
                        ["sourceId"]      = removed.SourceId,
                        ["workflowType"]  = removed.WorkflowType.ToString(),
                        ["fromStatus"]    = removed.SlaStatus.ToString(),
                        ["correlationId"] = correlationId
                    }
                });
            }

            _logger.LogInformation(
                "ComplianceOps item resolved. ItemId={ItemId} Actor={Actor}",
                LoggingHelper.SanitizeLogInput(itemId),
                LoggingHelper.SanitizeLogInput(actorId));

            return true;
        }

        // ── ClassifySlaStatus ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public ComplianceOpsSlaStatus ClassifySlaStatus(DateTime? dueAt, DateTime now)
        {
            if (!dueAt.HasValue)
                return ComplianceOpsSlaStatus.OnTrack;

            if (now > dueAt.Value)
                return ComplianceOpsSlaStatus.Overdue;

            if (dueAt.Value - now <= DueSoonWindow)
                return ComplianceOpsSlaStatus.DueSoon;

            return ComplianceOpsSlaStatus.OnTrack;
        }

        // ── ComputePriorityScore ────────────────────────────────────────────────

        /// <inheritdoc/>
        public int ComputePriorityScore(
            ComplianceOpsSlaStatus status, bool isFailClosed, DateTime createdAt, DateTime now)
        {
            int baseScore = status switch
            {
                ComplianceOpsSlaStatus.Blocked  => BlockedBaseScore,
                ComplianceOpsSlaStatus.Overdue  => OverdueBaseScore,
                ComplianceOpsSlaStatus.DueSoon  => DueSoonBaseScore,
                _                               => OnTrackBaseScore
            };

            int failClosedBonus = isFailClosed ? FailClosedBonus : 0;

            // Age bonus: up to 499 additional points for items up to 30 days old
            double ageDays  = (now - createdAt).TotalDays;
            int    ageBonus = (int)Math.Min(ageDays / 30.0 * 499, 499);

            return baseScore + failClosedBonus + ageBonus;
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        private void RefreshItemSla(ComplianceOpsQueueItem item, DateTime now)
        {
            if (item.BlockerCategory != ComplianceOpsBlockerCategory.None)
            {
                item.SlaStatus = ComplianceOpsSlaStatus.Blocked;
            }
            else
            {
                item.SlaStatus = ClassifySlaStatus(item.DueAt, now);
            }

            item.HoursUntilDue = item.DueAt.HasValue
                ? (item.DueAt.Value - now).TotalHours
                : null;

            item.PriorityScore = ComputePriorityScore(item.SlaStatus, item.IsFailClosed, item.CreatedAt, now);
        }

        private static Dictionary<ComplianceOpsWorkflowType, int> BuildWorkflowTypeCounts(
            IEnumerable<ComplianceOpsQueueItem> items)
        {
            var result = new Dictionary<ComplianceOpsWorkflowType, int>();
            foreach (var item in items)
            {
                result.TryGetValue(item.WorkflowType, out int current);
                result[item.WorkflowType] = current + 1;
            }
            return result;
        }

        private static Dictionary<ComplianceOpsBlockerCategory, int> BuildBlockerCounts(
            IEnumerable<ComplianceOpsQueueItem> items)
        {
            var result = new Dictionary<ComplianceOpsBlockerCategory, int>();
            foreach (var item in items.Where(i => i.BlockerCategory != ComplianceOpsBlockerCategory.None))
            {
                result.TryGetValue(item.BlockerCategory, out int current);
                result[item.BlockerCategory] = current + 1;
            }
            return result;
        }

        private static List<ComplianceOpsRoleSummary> BuildRoleSummaries(
            IEnumerable<ComplianceOpsQueueItem> items)
        {
            var allItems = items.ToList();
            var summaries = new List<ComplianceOpsRoleSummary>();

            foreach (ComplianceOpsRole role in Enum.GetValues<ComplianceOpsRole>())
            {
                // ExecutiveSummary sees all items; other roles see their own items
                var roleItems = role == ComplianceOpsRole.ExecutiveSummary
                    ? allItems
                    : allItems.Where(i => i.OwnerRole == role).ToList();

                var summary = new ComplianceOpsRoleSummary
                {
                    Role               = role,
                    TotalItems         = roleItems.Count,
                    OnTrackCount       = roleItems.Count(i => i.SlaStatus == ComplianceOpsSlaStatus.OnTrack),
                    DueSoonCount       = roleItems.Count(i => i.SlaStatus == ComplianceOpsSlaStatus.DueSoon),
                    OverdueCount       = roleItems.Count(i => i.SlaStatus == ComplianceOpsSlaStatus.Overdue),
                    BlockedCount       = roleItems.Count(i => i.SlaStatus == ComplianceOpsSlaStatus.Blocked),
                    HasFailClosedBlockers = roleItems.Any(i => i.IsFailClosed && i.SlaStatus == ComplianceOpsSlaStatus.Blocked),
                    OverallStatus      = roleItems.Count > 0
                        ? roleItems.Max(i => i.SlaStatus)
                        : ComplianceOpsSlaStatus.OnTrack
                };

                summaries.Add(summary);
            }

            return summaries;
        }

        private static void DeriveHealth(ComplianceOperationsOverview overview)
        {
            if (overview.BlockedCount > 0 || overview.OverdueCount > 0)
            {
                overview.OverallHealthStatus = "Critical";
                overview.HealthSummaryMessage = overview.BlockedCount > 0
                    ? $"{overview.BlockedCount} item(s) are blocked and cannot progress. Immediate action is required."
                    : $"{overview.OverdueCount} item(s) are overdue. Review and escalation required.";
            }
            else if (overview.DueSoonCount > 0)
            {
                overview.OverallHealthStatus = "AtRisk";
                overview.HealthSummaryMessage =
                    $"{overview.DueSoonCount} item(s) are due within the next 3 days. Review to avoid SLA breach.";
            }
            else
            {
                overview.OverallHealthStatus = "Healthy";
                overview.HealthSummaryMessage = overview.TotalQueueItems > 0
                    ? $"All {overview.TotalQueueItems} queue item(s) are on track."
                    : "No active compliance operations queue items.";
            }
        }
    }
}
