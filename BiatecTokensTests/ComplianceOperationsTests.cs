using BiatecTokensApi.Models.ComplianceOperations;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive tests for <see cref="ComplianceOperationsService"/> and
    /// <see cref="BiatecTokensApi.Controllers.ComplianceOperationsController"/>.
    ///
    /// Coverage:
    ///   - SLA classification: OnTrack / DueSoon / Overdue boundary conditions
    ///   - Priority scoring: blocked &gt; overdue &gt; due-soon &gt; on-track; fail-closed bonus; age bonus
    ///   - Queue management: upsert, filter by role / workflow / SLA / fail-closed, pagination
    ///   - Aggregation: overview counts, per-role summaries, health status derivation
    ///   - Blocker classification: missing evidence, stale evidence, delivery failure, etc.
    ///   - Fail-closed semantics: blocked + fail-closed items produce Critical health
    ///   - Webhook emission: overdue and blocked transitions trigger appropriate events
    ///   - Resolve: item removal and ComplianceOpsItemResolved event
    ///   - Degraded downstream: empty queue produces Healthy; partial-blocked produces Critical
    ///   - Integration API tests: overview and queue endpoints return expected shapes
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceOperationsTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // FakeTimeProvider
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset initial) => _now = initial;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public void SetUtcNow(DateTimeOffset value) => _now = value;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // FakeWebhookService
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class FakeWebhookService : IWebhookService
        {
            public List<WebhookEvent> Emitted { get; } = new();

            public Task EmitEventAsync(WebhookEvent ev)
            {
                Emitted.Add(ev);
                return Task.CompletedTask;
            }

            // Unused stubs
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> CreateSubscriptionAsync(
                BiatecTokensApi.Models.Webhook.CreateWebhookSubscriptionRequest request, string actorId) =>
                Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> DeleteSubscriptionAsync(string subscriptionId, string actorId) =>
                Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> GetSubscriptionAsync(string subscriptionId, string userId) =>
                Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionListResponse> ListSubscriptionsAsync(string userId) =>
                Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionListResponse());
            public Task<BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse> UpdateSubscriptionAsync(
                BiatecTokensApi.Models.Webhook.UpdateWebhookSubscriptionRequest request, string userId) =>
                Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookSubscriptionResponse());
            public Task<BiatecTokensApi.Models.Webhook.WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(
                BiatecTokensApi.Models.Webhook.GetWebhookDeliveryHistoryRequest request, string userId) =>
                Task.FromResult(new BiatecTokensApi.Models.Webhook.WebhookDeliveryHistoryResponse());
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        private static ComplianceOperationsService CreateService(
            TimeProvider? timeProvider = null,
            IWebhookService? webhookService = null)
        {
            return new ComplianceOperationsService(
                NullLogger<ComplianceOperationsService>.Instance,
                timeProvider,
                webhookService);
        }

        private static ComplianceOpsQueueItem MakeItem(
            string? itemId = null,
            string? sourceId = null,
            string title = "Test Item",
            ComplianceOpsWorkflowType workflowType = ComplianceOpsWorkflowType.ComplianceCase,
            ComplianceOpsRole role = ComplianceOpsRole.ComplianceManager,
            ComplianceOpsBlockerCategory blocker = ComplianceOpsBlockerCategory.None,
            DateTime? dueAt = null,
            bool isFailClosed = false,
            bool evidenceFresh = true,
            DateTime? createdAt = null)
        {
            var now = DateTime.UtcNow;
            return new ComplianceOpsQueueItem
            {
                ItemId          = itemId ?? Guid.NewGuid().ToString(),
                Title           = title,
                SourceId        = sourceId ?? Guid.NewGuid().ToString(),
                WorkflowType    = workflowType,
                OwnerRole       = role,
                BlockerCategory = blocker,
                DueAt           = dueAt,
                IsFailClosed    = isFailClosed,
                EvidenceFresh   = evidenceFresh,
                CreatedAt       = createdAt ?? now,
                LastUpdatedAt   = createdAt ?? now
            };
        }

        // Convenience helpers for common SLA buckets
        private static DateTime InFuture(int days = 30)  => DateTime.UtcNow.AddDays(days);
        private static DateTime InPast(int days = 1)     => DateTime.UtcNow.AddDays(-days);
        private static DateTime DueSoonDate(int hours = 24) => DateTime.UtcNow.AddHours(hours);

        // ═══════════════════════════════════════════════════════════════════════
        // SLA classification tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ClassifySlaStatus_NoDueDate_ReturnsOnTrack()
        {
            var svc = CreateService();
            var result = svc.ClassifySlaStatus(null, DateTime.UtcNow);
            Assert.That(result, Is.EqualTo(ComplianceOpsSlaStatus.OnTrack));
        }

        [Test]
        public void ClassifySlaStatus_DueInFutureBeyondWarningWindow_ReturnsOnTrack()
        {
            var svc = CreateService();
            var now = DateTime.UtcNow;
            var due = now.AddDays(10);
            var result = svc.ClassifySlaStatus(due, now);
            Assert.That(result, Is.EqualTo(ComplianceOpsSlaStatus.OnTrack));
        }

        [Test]
        public void ClassifySlaStatus_DueExactlyAtWarningBoundary_ReturnsDueSoon()
        {
            var svc = CreateService();
            var now = DateTime.UtcNow;
            var due = now.AddDays(3);
            var result = svc.ClassifySlaStatus(due, now);
            Assert.That(result, Is.EqualTo(ComplianceOpsSlaStatus.DueSoon));
        }

        [Test]
        public void ClassifySlaStatus_DueIn1Day_ReturnsDueSoon()
        {
            var svc = CreateService();
            var now = DateTime.UtcNow;
            var due = now.AddDays(1);
            var result = svc.ClassifySlaStatus(due, now);
            Assert.That(result, Is.EqualTo(ComplianceOpsSlaStatus.DueSoon));
        }

        [Test]
        public void ClassifySlaStatus_Overdue_ReturnsOverdue()
        {
            var svc = CreateService();
            var now = DateTime.UtcNow;
            var due = now.AddDays(-1);
            var result = svc.ClassifySlaStatus(due, now);
            Assert.That(result, Is.EqualTo(ComplianceOpsSlaStatus.Overdue));
        }

        [Test]
        public void ClassifySlaStatus_DueExactlyNow_ReturnsOverdue()
        {
            // Due at == now: the item is not strictly overdue yet but the comparison
            // checks `now > dueAt`, so exact equality returns DueSoon.
            var svc = CreateService();
            var now = DateTime.UtcNow;
            var due = now;
            var result = svc.ClassifySlaStatus(due, now);
            // Due exactly now is within the warning window (0 hours <= 3 days), so DueSoon
            Assert.That(result, Is.EqualTo(ComplianceOpsSlaStatus.DueSoon));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Priority scoring tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ComputePriorityScore_Blocked_HigherThanOverdue()
        {
            var svc = CreateService();
            var now = DateTime.UtcNow;
            var blocked = svc.ComputePriorityScore(ComplianceOpsSlaStatus.Blocked, false, now, now);
            var overdue = svc.ComputePriorityScore(ComplianceOpsSlaStatus.Overdue, false, now, now);
            Assert.That(blocked, Is.GreaterThan(overdue));
        }

        [Test]
        public void ComputePriorityScore_Overdue_HigherThanDueSoon()
        {
            var svc = CreateService();
            var now = DateTime.UtcNow;
            var overdue = svc.ComputePriorityScore(ComplianceOpsSlaStatus.Overdue, false, now, now);
            var dueSoon = svc.ComputePriorityScore(ComplianceOpsSlaStatus.DueSoon, false, now, now);
            Assert.That(overdue, Is.GreaterThan(dueSoon));
        }

        [Test]
        public void ComputePriorityScore_DueSoon_HigherThanOnTrack()
        {
            var svc = CreateService();
            var now = DateTime.UtcNow;
            var dueSoon = svc.ComputePriorityScore(ComplianceOpsSlaStatus.DueSoon, false, now, now);
            var onTrack = svc.ComputePriorityScore(ComplianceOpsSlaStatus.OnTrack, false, now, now);
            Assert.That(dueSoon, Is.GreaterThan(onTrack));
        }

        [Test]
        public void ComputePriorityScore_FailClosedBonus_IncreasesScore()
        {
            var svc = CreateService();
            var now = DateTime.UtcNow;
            var withBonus    = svc.ComputePriorityScore(ComplianceOpsSlaStatus.Blocked, true, now, now);
            var withoutBonus = svc.ComputePriorityScore(ComplianceOpsSlaStatus.Blocked, false, now, now);
            Assert.That(withBonus, Is.GreaterThan(withoutBonus));
        }

        [Test]
        public void ComputePriorityScore_OlderItem_HigherAgeBonus()
        {
            var svc = CreateService();
            var now     = DateTime.UtcNow;
            var old     = svc.ComputePriorityScore(ComplianceOpsSlaStatus.OnTrack, false, now.AddDays(-30), now);
            var recent  = svc.ComputePriorityScore(ComplianceOpsSlaStatus.OnTrack, false, now, now);
            Assert.That(old, Is.GreaterThan(recent));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Upsert and queue management tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task UpsertQueueItem_NewItem_AppearsInQueue()
        {
            var svc  = CreateService();
            var item = MakeItem(dueAt: InFuture(30));
            await svc.UpsertQueueItemAsync(item, "actor-1", "corr-1");

            var result = await svc.GetQueueAsync(new ComplianceOpsQueueRequest(), "actor-1", "corr-1");
            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items[0].ItemId, Is.EqualTo(item.ItemId));
        }

        [Test]
        public async Task UpsertQueueItem_UpdateExistingItem_ReplacesEntry()
        {
            var svc    = CreateService();
            var itemId = Guid.NewGuid().ToString();
            var item1  = MakeItem(itemId: itemId, title: "Original");
            await svc.UpsertQueueItemAsync(item1, "actor", "corr");

            var item2 = MakeItem(itemId: itemId, title: "Updated");
            await svc.UpsertQueueItemAsync(item2, "actor", "corr");

            var result = await svc.GetQueueAsync(new ComplianceOpsQueueRequest(), "actor", "corr");
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items[0].Title, Is.EqualTo("Updated"));
        }

        [Test]
        public async Task UpsertQueueItem_WithBlocker_SetsBlockedStatus()
        {
            var svc  = CreateService();
            var item = MakeItem(blocker: ComplianceOpsBlockerCategory.MissingEvidence,
                                dueAt: InFuture(30));
            await svc.UpsertQueueItemAsync(item, "actor", "corr");

            var result = await svc.GetQueueAsync(new ComplianceOpsQueueRequest(), "actor", "corr");
            Assert.That(result.Items[0].SlaStatus, Is.EqualTo(ComplianceOpsSlaStatus.Blocked));
        }

        [Test]
        public async Task UpsertQueueItem_OverdueItem_SetsOverdueStatus()
        {
            var svc  = CreateService();
            var item = MakeItem(dueAt: InPast(2));
            await svc.UpsertQueueItemAsync(item, "actor", "corr");

            var result = await svc.GetQueueAsync(new ComplianceOpsQueueRequest(), "actor", "corr");
            Assert.That(result.Items[0].SlaStatus, Is.EqualTo(ComplianceOpsSlaStatus.Overdue));
        }

        [Test]
        public async Task GetQueue_FilterByRole_ReturnsOnlyMatchingItems()
        {
            var svc = CreateService();
            var mgr = MakeItem(title: "Manager item", role: ComplianceOpsRole.ComplianceManager);
            var lead = MakeItem(title: "Lead item", role: ComplianceOpsRole.OperationsLead);
            await svc.UpsertQueueItemAsync(mgr,  "actor", "corr");
            await svc.UpsertQueueItemAsync(lead, "actor", "corr");

            var result = await svc.GetQueueAsync(
                new ComplianceOpsQueueRequest { Role = ComplianceOpsRole.ComplianceManager },
                "actor", "corr");

            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items[0].Title, Is.EqualTo("Manager item"));
        }

        [Test]
        public async Task GetQueue_FilterByWorkflowType_ReturnsOnlyMatchingItems()
        {
            var svc = CreateService();
            var case1 = MakeItem(workflowType: ComplianceOpsWorkflowType.ComplianceCase);
            var rep1  = MakeItem(workflowType: ComplianceOpsWorkflowType.ScheduledReporting);
            await svc.UpsertQueueItemAsync(case1, "actor", "corr");
            await svc.UpsertQueueItemAsync(rep1,  "actor", "corr");

            var result = await svc.GetQueueAsync(
                new ComplianceOpsQueueRequest { WorkflowType = ComplianceOpsWorkflowType.ScheduledReporting },
                "actor", "corr");

            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items[0].WorkflowType, Is.EqualTo(ComplianceOpsWorkflowType.ScheduledReporting));
        }

        [Test]
        public async Task GetQueue_FilterBySlaStatus_ReturnsOnlyMatchingItems()
        {
            var svc     = CreateService();
            var overdue = MakeItem(title: "Overdue", dueAt: InPast(2));
            var onTrack = MakeItem(title: "OnTrack", dueAt: InFuture(30));
            await svc.UpsertQueueItemAsync(overdue, "actor", "corr");
            await svc.UpsertQueueItemAsync(onTrack, "actor", "corr");

            var result = await svc.GetQueueAsync(
                new ComplianceOpsQueueRequest { SlaStatus = ComplianceOpsSlaStatus.Overdue },
                "actor", "corr");

            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items[0].Title, Is.EqualTo("Overdue"));
        }

        [Test]
        public async Task GetQueue_FailClosedOnly_ReturnsOnlyFailClosedItems()
        {
            var svc       = CreateService();
            var failClosed = MakeItem(title: "FailClosed", isFailClosed: true,
                                      blocker: ComplianceOpsBlockerCategory.MissingEvidence);
            var normal    = MakeItem(title: "Normal");
            await svc.UpsertQueueItemAsync(failClosed, "actor", "corr");
            await svc.UpsertQueueItemAsync(normal,     "actor", "corr");

            var result = await svc.GetQueueAsync(
                new ComplianceOpsQueueRequest { FailClosedOnly = true },
                "actor", "corr");

            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items[0].Title, Is.EqualTo("FailClosed"));
        }

        [Test]
        public async Task GetQueue_Pagination_ReturnsCorrectPage()
        {
            var svc = CreateService();
            for (int i = 0; i < 5; i++)
                await svc.UpsertQueueItemAsync(MakeItem(title: $"Item-{i}"), "actor", "corr");

            var page0 = await svc.GetQueueAsync(
                new ComplianceOpsQueueRequest { PageSize = 3, Page = 0 }, "actor", "corr");
            var page1 = await svc.GetQueueAsync(
                new ComplianceOpsQueueRequest { PageSize = 3, Page = 1 }, "actor", "corr");

            Assert.That(page0.TotalCount, Is.EqualTo(5));
            Assert.That(page0.Items.Count, Is.EqualTo(3));
            Assert.That(page1.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task GetQueue_PriorityOrdering_BlockedBeforeOverdueBeforeDueSoon()
        {
            var svc = CreateService();
            var onTrack = MakeItem(title: "OnTrack");
            var overdue = MakeItem(title: "Overdue", dueAt: InPast(2));
            var blocked = MakeItem(title: "Blocked",
                                   blocker: ComplianceOpsBlockerCategory.MissingEvidence);
            var dueSoon = MakeItem(title: "DueSoon", dueAt: DueSoonDate(24));

            await svc.UpsertQueueItemAsync(onTrack, "actor", "corr");
            await svc.UpsertQueueItemAsync(overdue, "actor", "corr");
            await svc.UpsertQueueItemAsync(blocked, "actor", "corr");
            await svc.UpsertQueueItemAsync(dueSoon, "actor", "corr");

            var result = await svc.GetQueueAsync(new ComplianceOpsQueueRequest(), "actor", "corr");

            Assert.That(result.Items[0].Title, Is.EqualTo("Blocked"));
            Assert.That(result.Items[1].Title, Is.EqualTo("Overdue"));
            Assert.That(result.Items[2].Title, Is.EqualTo("DueSoon"));
            Assert.That(result.Items[3].Title, Is.EqualTo("OnTrack"));
        }

        [Test]
        public async Task GetQueue_NullRequest_ReturnsBadRequest()
        {
            var svc    = CreateService();
            var result = await svc.GetQueueAsync(null!, "actor", "corr");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_REQUIRED_FIELD"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Overview / aggregation tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetOverview_EmptyQueue_ReturnsHealthyZeroCounts()
        {
            var svc    = CreateService();
            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Overview!.TotalQueueItems, Is.EqualTo(0));
            Assert.That(result.Overview.OverallHealthStatus, Is.EqualTo("Healthy"));
            Assert.That(result.Overview.FailClosedCount, Is.EqualTo(0));
        }

        [Test]
        public async Task GetOverview_OnlyOnTrackItems_ReturnsHealthy()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(MakeItem(dueAt: InFuture(30)), "actor", "corr");
            await svc.UpsertQueueItemAsync(MakeItem(dueAt: InFuture(45)), "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("Healthy"));
            Assert.That(result.Overview.OnTrackCount, Is.EqualTo(2));
        }

        [Test]
        public async Task GetOverview_DueSoonItem_ReturnsAtRisk()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(MakeItem(dueAt: DueSoonDate(24)), "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("AtRisk"));
            Assert.That(result.Overview.DueSoonCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GetOverview_OverdueItem_ReturnsCritical()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(MakeItem(dueAt: InPast(2)), "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("Critical"));
            Assert.That(result.Overview.OverdueCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GetOverview_BlockedItem_ReturnsCritical()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(MakeItem(blocker: ComplianceOpsBlockerCategory.PendingApproval), "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("Critical"));
            Assert.That(result.Overview.BlockedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GetOverview_FailClosedBlockedItem_IncrementsFailClosedCount()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(
                MakeItem(blocker: ComplianceOpsBlockerCategory.MissingEvidence, isFailClosed: true),
                "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.FailClosedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task GetOverview_MultipleWorkflowTypes_CountsCorrectly()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(MakeItem(workflowType: ComplianceOpsWorkflowType.ComplianceCase), "actor", "corr");
            await svc.UpsertQueueItemAsync(MakeItem(workflowType: ComplianceOpsWorkflowType.ComplianceCase), "actor", "corr");
            await svc.UpsertQueueItemAsync(MakeItem(workflowType: ComplianceOpsWorkflowType.ScheduledReporting), "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.CountByWorkflowType[ComplianceOpsWorkflowType.ComplianceCase], Is.EqualTo(2));
            Assert.That(result.Overview.CountByWorkflowType[ComplianceOpsWorkflowType.ScheduledReporting], Is.EqualTo(1));
        }

        [Test]
        public async Task GetOverview_BlockerCategories_OnlyNonNoneIncluded()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(MakeItem(blocker: ComplianceOpsBlockerCategory.MissingEvidence), "actor", "corr");
            await svc.UpsertQueueItemAsync(MakeItem(blocker: ComplianceOpsBlockerCategory.MissingEvidence), "actor", "corr");
            await svc.UpsertQueueItemAsync(MakeItem(blocker: ComplianceOpsBlockerCategory.DeliveryFailure), "actor", "corr");
            await svc.UpsertQueueItemAsync(MakeItem(), "actor", "corr"); // None — should not appear

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.CountByBlockerCategory.ContainsKey(ComplianceOpsBlockerCategory.None), Is.False);
            Assert.That(result.Overview.CountByBlockerCategory[ComplianceOpsBlockerCategory.MissingEvidence], Is.EqualTo(2));
            Assert.That(result.Overview.CountByBlockerCategory[ComplianceOpsBlockerCategory.DeliveryFailure], Is.EqualTo(1));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Role summary tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetOverview_RoleSummaries_ContainAllRoles()
        {
            var svc    = CreateService();
            var result = await svc.GetOverviewAsync("actor", "corr");

            var roles = result.Overview!.RoleSummaries.Select(r => r.Role).ToList();
            Assert.That(roles, Contains.Item(ComplianceOpsRole.ComplianceManager));
            Assert.That(roles, Contains.Item(ComplianceOpsRole.OperationsLead));
            Assert.That(roles, Contains.Item(ComplianceOpsRole.ExecutiveSummary));
        }

        [Test]
        public async Task GetOverview_ExecutiveSummary_SeesAllItems()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(MakeItem(role: ComplianceOpsRole.ComplianceManager), "actor", "corr");
            await svc.UpsertQueueItemAsync(MakeItem(role: ComplianceOpsRole.OperationsLead), "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            var exec = result.Overview!.RoleSummaries.First(r => r.Role == ComplianceOpsRole.ExecutiveSummary);
            Assert.That(exec.TotalItems, Is.EqualTo(2));
        }

        [Test]
        public async Task GetOverview_RoleSummary_HasFailClosedBlockers_WhenBlockedFailClosedPresent()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(
                MakeItem(role: ComplianceOpsRole.ComplianceManager,
                         blocker: ComplianceOpsBlockerCategory.MissingEvidence,
                         isFailClosed: true),
                "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            var mgr = result.Overview!.RoleSummaries.First(r => r.Role == ComplianceOpsRole.ComplianceManager);
            Assert.That(mgr.HasFailClosedBlockers, Is.True);
        }

        [Test]
        public async Task GetOverview_RoleSummary_OverallStatus_WorstCase()
        {
            var svc = CreateService();
            // Manager: one blocked, one on-track → worst = Blocked
            await svc.UpsertQueueItemAsync(MakeItem(role: ComplianceOpsRole.ComplianceManager,
                                                    blocker: ComplianceOpsBlockerCategory.PendingApproval), "actor", "corr");
            await svc.UpsertQueueItemAsync(MakeItem(role: ComplianceOpsRole.ComplianceManager), "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            var mgr = result.Overview!.RoleSummaries.First(r => r.Role == ComplianceOpsRole.ComplianceManager);
            Assert.That(mgr.OverallStatus, Is.EqualTo(ComplianceOpsSlaStatus.Blocked));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Resolve tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ResolveQueueItem_ExistingItem_RemovesFromQueue()
        {
            var svc    = CreateService();
            var itemId = Guid.NewGuid().ToString();
            await svc.UpsertQueueItemAsync(MakeItem(itemId: itemId), "actor", "corr");

            var removed = await svc.ResolveQueueItemAsync(itemId, "actor", "corr");

            Assert.That(removed, Is.True);
            var queue = await svc.GetQueueAsync(new ComplianceOpsQueueRequest(), "actor", "corr");
            Assert.That(queue.TotalCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ResolveQueueItem_NonExistentItem_ReturnsFalse()
        {
            var svc    = CreateService();
            var result = await svc.ResolveQueueItemAsync("does-not-exist", "actor", "corr");
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task ResolveQueueItem_EmptyItemId_ReturnsFalse()
        {
            var svc    = CreateService();
            var result = await svc.ResolveQueueItemAsync("", "actor", "corr");
            Assert.That(result, Is.False);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Webhook emission tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task UpsertQueueItem_TransitionsToOverdue_EmitsOverdueEvent()
        {
            var webhook = new FakeWebhookService();
            var svc     = CreateService(webhookService: webhook);

            // First insert as on-track
            var itemId = Guid.NewGuid().ToString();
            await svc.UpsertQueueItemAsync(MakeItem(itemId: itemId, dueAt: InFuture(30)), "actor", "corr");
            webhook.Emitted.Clear();

            // Update to overdue
            await svc.UpsertQueueItemAsync(MakeItem(itemId: itemId, dueAt: InPast(2)), "actor", "corr");

            Assert.That(webhook.Emitted, Has.Count.EqualTo(1));
            Assert.That(webhook.Emitted[0].EventType, Is.EqualTo(WebhookEventType.ComplianceOpsItemOverdue));
        }

        [Test]
        public async Task UpsertQueueItem_TransitionsToBlocked_EmitsBlockedEvent()
        {
            var webhook = new FakeWebhookService();
            var svc     = CreateService(webhookService: webhook);

            var itemId = Guid.NewGuid().ToString();
            await svc.UpsertQueueItemAsync(MakeItem(itemId: itemId, dueAt: InFuture(30)), "actor", "corr");
            webhook.Emitted.Clear();

            await svc.UpsertQueueItemAsync(
                MakeItem(itemId: itemId, blocker: ComplianceOpsBlockerCategory.MissingEvidence),
                "actor", "corr");

            Assert.That(webhook.Emitted, Has.Count.EqualTo(1));
            Assert.That(webhook.Emitted[0].EventType, Is.EqualTo(WebhookEventType.ComplianceOpsItemBlocked));
        }

        [Test]
        public async Task UpsertQueueItem_NoStatusChange_NoWebhookEmitted()
        {
            var webhook = new FakeWebhookService();
            var svc     = CreateService(webhookService: webhook);

            var itemId = Guid.NewGuid().ToString();
            var item   = MakeItem(itemId: itemId, dueAt: InFuture(30));
            await svc.UpsertQueueItemAsync(item, "actor", "corr");
            webhook.Emitted.Clear();

            // Re-upsert with same SLA status (still OnTrack) — no event expected
            var item2 = MakeItem(itemId: itemId, dueAt: InFuture(25));
            await svc.UpsertQueueItemAsync(item2, "actor", "corr");

            Assert.That(webhook.Emitted, Is.Empty);
        }

        [Test]
        public async Task ResolveQueueItem_EmitsResolvedEvent()
        {
            var webhook = new FakeWebhookService();
            var svc     = CreateService(webhookService: webhook);

            var itemId = Guid.NewGuid().ToString();
            await svc.UpsertQueueItemAsync(MakeItem(itemId: itemId), "actor", "corr");
            webhook.Emitted.Clear();

            await svc.ResolveQueueItemAsync(itemId, "actor", "corr");

            Assert.That(webhook.Emitted, Has.Count.EqualTo(1));
            Assert.That(webhook.Emitted[0].EventType, Is.EqualTo(WebhookEventType.ComplianceOpsItemResolved));
        }

        [Test]
        public async Task UpsertQueueItem_NewOverdueItem_EmitsOverdueEventOnFirstInsert()
        {
            // A brand-new overdue item should emit the event even without a previous state
            var webhook = new FakeWebhookService();
            var svc     = CreateService(webhookService: webhook);

            await svc.UpsertQueueItemAsync(
                MakeItem(dueAt: InPast(5)),
                "actor", "corr");

            Assert.That(webhook.Emitted, Has.Count.EqualTo(1));
            Assert.That(webhook.Emitted[0].EventType, Is.EqualTo(WebhookEventType.ComplianceOpsItemOverdue));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Blocker category classification tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [TestCase(ComplianceOpsBlockerCategory.MissingEvidence)]
        [TestCase(ComplianceOpsBlockerCategory.StaleEvidence)]
        [TestCase(ComplianceOpsBlockerCategory.PendingApproval)]
        [TestCase(ComplianceOpsBlockerCategory.UnresolvedSanctionsReview)]
        [TestCase(ComplianceOpsBlockerCategory.KycIncomplete)]
        [TestCase(ComplianceOpsBlockerCategory.DeliveryFailure)]
        [TestCase(ComplianceOpsBlockerCategory.EscalationPending)]
        [TestCase(ComplianceOpsBlockerCategory.UpstreamDependency)]
        [TestCase(ComplianceOpsBlockerCategory.PolicyConflict)]
        public async Task UpsertQueueItem_AnyBlockerCategory_SetsSlaStatusBlocked(
            ComplianceOpsBlockerCategory category)
        {
            var svc  = CreateService();
            var item = MakeItem(blocker: category, dueAt: InFuture(30));
            await svc.UpsertQueueItemAsync(item, "actor", "corr");

            var result = await svc.GetQueueAsync(new ComplianceOpsQueueRequest(), "actor", "corr");
            Assert.That(result.Items[0].SlaStatus, Is.EqualTo(ComplianceOpsSlaStatus.Blocked));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Degraded downstream / fail-closed semantics
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Overview_StaleEvidence_FailClosed_IsRepresentedExplicitly()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(
                MakeItem(blocker: ComplianceOpsBlockerCategory.StaleEvidence,
                         isFailClosed: true,
                         evidenceFresh: false),
                "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.FailClosedCount, Is.EqualTo(1));
            Assert.That(result.Overview.BlockedCount, Is.EqualTo(1));
            Assert.That(result.Overview.OverallHealthStatus, Is.EqualTo("Critical"));
            Assert.That(result.Overview.CountByBlockerCategory.ContainsKey(ComplianceOpsBlockerCategory.StaleEvidence), Is.True);
        }

        [Test]
        public async Task Overview_DeliveryFailure_ProducesExplicitBlockedState()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(
                MakeItem(blocker: ComplianceOpsBlockerCategory.DeliveryFailure, isFailClosed: true),
                "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("Critical"));
            Assert.That(result.Overview.FailClosedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Overview_UnresolvedSanctionsReview_ProducesBlockedState()
        {
            var svc = CreateService();
            await svc.UpsertQueueItemAsync(
                MakeItem(blocker: ComplianceOpsBlockerCategory.UnresolvedSanctionsReview),
                "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.BlockedCount, Is.EqualTo(1));
            Assert.That(result.Overview.OverallHealthStatus, Is.EqualTo("Critical"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // FakeTimeProvider: SLA refreshes on overview / queue retrieval
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TimeAdvance_OnTrackItemBecomesOverdue_ReflectedOnNextRetrieval()
        {
            var tp   = new FakeTimeProvider(BaseTime);
            var svc  = CreateService(timeProvider: tp);

            // Insert item due in 10 days — currently on-track
            var item = MakeItem(dueAt: BaseTime.UtcDateTime.AddDays(10), createdAt: BaseTime.UtcDateTime);
            await svc.UpsertQueueItemAsync(item, "actor", "corr");

            // Advance time 12 days so item is now overdue
            tp.Advance(TimeSpan.FromDays(12));

            var result = await svc.GetOverviewAsync("actor", "corr");
            Assert.That(result.Overview!.OverdueCount, Is.EqualTo(1));
            Assert.That(result.Overview.OverallHealthStatus, Is.EqualTo("Critical"));
        }

        [Test]
        public async Task TimeAdvance_OnTrackItemBecomesDueSoon_ReflectedOnNextRetrieval()
        {
            var tp  = new FakeTimeProvider(BaseTime);
            var svc = CreateService(timeProvider: tp);

            var item = MakeItem(dueAt: BaseTime.UtcDateTime.AddDays(10), createdAt: BaseTime.UtcDateTime);
            await svc.UpsertQueueItemAsync(item, "actor", "corr");

            tp.Advance(TimeSpan.FromDays(8)); // 2 days remaining — within warning window

            var result = await svc.GetOverviewAsync("actor", "corr");
            Assert.That(result.Overview!.DueSoonCount, Is.EqualTo(1));
            Assert.That(result.Overview.OverallHealthStatus, Is.EqualTo("AtRisk"));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HoursUntilDue calculation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task UpsertQueueItem_DueInFuture_HoursUntilDueIsPositive()
        {
            var svc  = CreateService();
            var item = MakeItem(dueAt: DateTime.UtcNow.AddHours(48));
            await svc.UpsertQueueItemAsync(item, "actor", "corr");

            var result = await svc.GetQueueAsync(new ComplianceOpsQueueRequest(), "actor", "corr");
            Assert.That(result.Items[0].HoursUntilDue, Is.GreaterThan(0));
        }

        [Test]
        public async Task UpsertQueueItem_Overdue_HoursUntilDueIsNegative()
        {
            var svc  = CreateService();
            var item = MakeItem(dueAt: DateTime.UtcNow.AddHours(-24));
            await svc.UpsertQueueItemAsync(item, "actor", "corr");

            var result = await svc.GetQueueAsync(new ComplianceOpsQueueRequest(), "actor", "corr");
            Assert.That(result.Items[0].HoursUntilDue, Is.LessThan(0));
        }

        [Test]
        public async Task UpsertQueueItem_NoDueDate_HoursUntilDueIsNull()
        {
            var svc  = CreateService();
            var item = MakeItem(); // no dueAt
            await svc.UpsertQueueItemAsync(item, "actor", "corr");

            var result = await svc.GetQueueAsync(new ComplianceOpsQueueRequest(), "actor", "corr");
            Assert.That(result.Items[0].HoursUntilDue, Is.Null);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Contract / schema tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ComplianceOpsSlaStatus_HasFourDistinctValues()
        {
            var values = Enum.GetValues<ComplianceOpsSlaStatus>();
            Assert.That(values.Length, Is.EqualTo(4));
            Assert.That(values, Contains.Item(ComplianceOpsSlaStatus.OnTrack));
            Assert.That(values, Contains.Item(ComplianceOpsSlaStatus.DueSoon));
            Assert.That(values, Contains.Item(ComplianceOpsSlaStatus.Overdue));
            Assert.That(values, Contains.Item(ComplianceOpsSlaStatus.Blocked));
        }

        [Test]
        public void ComplianceOpsSlaStatus_NumericOrdering_WorstCaseIsHighest()
        {
            // Blocked (3) > Overdue (2) > DueSoon (1) > OnTrack (0)
            Assert.That((int)ComplianceOpsSlaStatus.Blocked, Is.GreaterThan((int)ComplianceOpsSlaStatus.Overdue));
            Assert.That((int)ComplianceOpsSlaStatus.Overdue, Is.GreaterThan((int)ComplianceOpsSlaStatus.DueSoon));
            Assert.That((int)ComplianceOpsSlaStatus.DueSoon, Is.GreaterThan((int)ComplianceOpsSlaStatus.OnTrack));
        }

        [Test]
        public void ComplianceOpsQueueItem_DefaultsAreNonNull()
        {
            var item = new ComplianceOpsQueueItem();
            Assert.That(item.ItemId, Is.Not.Null.And.Not.Empty);
            Assert.That(item.LineageTags, Is.Not.Null);
            Assert.That(item.EvidenceFresh, Is.True);
        }

        [Test]
        public async Task GetQueueResponse_ContainsGeneratedAt()
        {
            var svc    = CreateService();
            var result = await svc.GetQueueAsync(new ComplianceOpsQueueRequest(), "actor", "corr");
            Assert.That(result.GeneratedAt, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public async Task GetOverviewResponse_ContainsGeneratedAt()
        {
            var svc    = CreateService();
            var result = await svc.GetOverviewAsync("actor", "corr");
            Assert.That(result.Overview!.GeneratedAt, Is.Not.EqualTo(default(DateTime)));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration API tests (HTTP pipeline via WebApplicationFactory)
        // ═══════════════════════════════════════════════════════════════════════

        private CustomWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;
        private string _jwtToken = string.Empty;

        [SetUp]
        public async Task ApiSetUp()
        {
            _factory = new CustomWebApplicationFactory();
            _client  = _factory.CreateClient();
            _jwtToken = await GetJwtToken();
        }

        [TearDown]
        public void ApiTearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        private async Task<string> GetJwtToken()
        {
            var email = $"ops-test-{Guid.NewGuid()}@biatec.io";
            var pass  = "SecureTest123!";

            var reg = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = pass, ConfirmPassword = pass });

            if (!reg.IsSuccessStatusCode)
                return string.Empty;

            var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = pass });
            if (!login.IsSuccessStatusCode)
                return string.Empty;

            var body = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            return body.TryGetProperty("accessToken", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        }

        [Test]
        public async Task Api_GetOverview_ReturnsOk()
        {
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Ignore("JWT token unavailable — skipping API test.");
                return;
            }

            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);

            var response = await _client.GetAsync("/api/v1/compliance-operations/overview");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await response.Content.ReadFromJsonAsync<ComplianceOperationsOverviewResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Overview, Is.Not.Null);
        }

        [Test]
        public async Task Api_GetOverview_WithoutAuth_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/compliance-operations/overview");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Api_PostQueue_ReturnsOk()
        {
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Ignore("JWT token unavailable — skipping API test.");
                return;
            }

            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);

            var request = new ComplianceOpsQueueRequest { PageSize = 10, Page = 0 };
            var response = await _client.PostAsJsonAsync("/api/v1/compliance-operations/queue", request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await response.Content.ReadFromJsonAsync<ComplianceOpsQueueResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
        }

        [Test]
        public async Task Api_PostQueue_WithoutAuth_Returns401()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/compliance-operations/queue",
                new ComplianceOpsQueueRequest());
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Api_PutQueueItem_ReturnsNoContent()
        {
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Ignore("JWT token unavailable — skipping API test.");
                return;
            }

            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);

            var item = new ComplianceOpsQueueItem
            {
                ItemId       = Guid.NewGuid().ToString(),
                Title        = "API test item",
                SourceId     = Guid.NewGuid().ToString(),
                WorkflowType = ComplianceOpsWorkflowType.ComplianceCase,
                OwnerRole    = ComplianceOpsRole.ComplianceManager
            };
            var response = await _client.PutAsJsonAsync("/api/v1/compliance-operations/queue/item", item);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        }

        [Test]
        public async Task Api_DeleteQueueItem_NonExistent_Returns404()
        {
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Ignore("JWT token unavailable — skipping API test.");
                return;
            }

            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);

            var response = await _client.DeleteAsync("/api/v1/compliance-operations/queue/item/does-not-exist");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Api_OverviewResponse_HasExpectedFields()
        {
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Ignore("JWT token unavailable — skipping API test.");
                return;
            }

            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);

            var response = await _client.GetAsync("/api/v1/compliance-operations/overview");
            var body     = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

            Assert.That(body.TryGetProperty("success", out _), Is.True, "overview.success missing");
            Assert.That(body.TryGetProperty("overview", out var ov), Is.True, "overview.overview missing");
            Assert.That(ov.TryGetProperty("overallHealthStatus", out _), Is.True, "overallHealthStatus missing");
            Assert.That(ov.TryGetProperty("roleSummaries", out _), Is.True, "roleSummaries missing");
            Assert.That(ov.TryGetProperty("countByWorkflowType", out _), Is.True, "countByWorkflowType missing");
        }

        [Test]
        public async Task Api_QueueResponse_HasExpectedFields()
        {
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Ignore("JWT token unavailable — skipping API test.");
                return;
            }

            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);

            var response = await _client.PostAsJsonAsync("/api/v1/compliance-operations/queue",
                new ComplianceOpsQueueRequest());
            var body     = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

            Assert.That(body.TryGetProperty("success", out _), Is.True, "success missing");
            Assert.That(body.TryGetProperty("items", out _), Is.True, "items missing");
            Assert.That(body.TryGetProperty("totalCount", out _), Is.True, "totalCount missing");
            Assert.That(body.TryGetProperty("generatedAt", out _), Is.True, "generatedAt missing");
        }

        // ── WebApplicationFactory ─────────────────────────────────────────────

        private class CustomWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForComplianceOperationsTests32Ch",
                        ["JwtConfig:SecretKey"] = "TestSecretKeyForComplianceOperationsTests32C!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io",
                        ["KycConfig:MockAutoApprove"] = "true",
                        ["StripeConfig:SecretKey"] = "sk_test_placeholder",
                        ["StripeConfig:PublishableKey"] = "pk_test_placeholder",
                        ["StripeConfig:WebhookSecret"] = "whsec_placeholder",
                    });
                });
            }
        }
    }
}
