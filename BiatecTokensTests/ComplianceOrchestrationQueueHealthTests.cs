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
    /// Tests proving that the compliance operations queue-health signals are truthful,
    /// deterministic, and correctly reflect SLA status, blocker severity, and fail-closed
    /// semantics across all operator-facing scenarios.
    ///
    /// Coverage:
    ///
    /// QH01: Empty queue → Healthy status, zero counts
    /// QH02: All OnTrack items → Healthy status
    /// QH03: Any DueSoon item → AtRisk status
    /// QH04: Any Overdue item → Critical status (overrides AtRisk)
    /// QH05: Any Blocked item → Critical status (overrides AtRisk)
    /// QH06: Blocked + Overdue → Critical (message references blocked, not overdue)
    /// QH07: SLA boundary: 4 days until due → OnTrack (not DueSoon)
    /// QH08: SLA boundary: 2 days until due → DueSoon
    /// QH09: SLA boundary: past due → Overdue
    /// QH10: No deadline → OnTrack always
    /// QH11: Fail-closed item scores higher than non-fail-closed in same SLA bucket
    /// QH12: Blocked score > Overdue score > DueSoon score > OnTrack score
    /// QH13: Older items outrank newer items within same SLA bucket
    /// QH14: Upsert updates existing item (same ItemId)
    /// QH15: Resolve removes item from queue
    /// QH16: Overview counts by WorkflowType correct after mixed upserts
    /// QH17: Queue filtered by SlaStatus returns only matching items
    /// QH18: Queue filtered by WorkflowType returns only matching items
    /// QH19: Queue filtered by FailClosedOnly returns only fail-closed items
    /// QH20: Health summary message is non-null and non-empty for all health states
    /// QH21: HTTP GET /api/v1/compliance-operations/overview returns 200
    /// QH22: HTTP POST /api/v1/compliance-operations/queue returns 200 with items
    /// QH23: HTTP unauthenticated overview → 401
    /// QH24: HealthSummaryMessage reflects blocked count when Blocked+Overdue
    /// QH25: Overview deterministic across 3 independent calls
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceOrchestrationQueueHealthTests
    {
        // ════════════════════════════════════════════════════════════════════
        // Controllable time provider
        // ════════════════════════════════════════════════════════════════════

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset initial) => _now = initial;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public void SetUtcNow(DateTimeOffset value) => _now = value;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ════════════════════════════════════════════════════════════════════
        // Fake webhook service
        // ════════════════════════════════════════════════════════════════════

        private sealed class FakeWebhookService : IWebhookService
        {
            public List<WebhookEvent> Emitted { get; } = new();
            public Task EmitEventAsync(WebhookEvent ev) { Emitted.Add(ev); return Task.CompletedTask; }
            public Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest r, string a) => Task.FromResult(new WebhookSubscriptionResponse());
            public Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string id, string a) => Task.FromResult(new WebhookSubscriptionResponse());
            public Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string id, string a) => Task.FromResult(new WebhookSubscriptionResponse());
            public Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string a) => Task.FromResult(new WebhookSubscriptionListResponse());
            public Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest r, string a) => Task.FromResult(new WebhookSubscriptionResponse());
            public Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest r, string a) => Task.FromResult(new WebhookDeliveryHistoryResponse());
        }

        // ════════════════════════════════════════════════════════════════════
        // WebApplicationFactory for HTTP tests
        // ════════════════════════════════════════════════════════════════════

        private sealed class QueueHealthFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "QueueHealthTestSecretKey32CharsMinimum!!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "QueueHealthTestKey32+CharMinimum!!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "queue-health-test",
                        ["ProtectedSignOff:EnforceConfigGuards"] = "true",
                        ["WorkflowGovernanceConfig:Enabled"] = "true",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io"
                    });
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Shared test state
        // ════════════════════════════════════════════════════════════════════

        private QueueHealthFactory _factory = null!;
        private HttpClient _client = null!;
        private string _jwt = null!;

        private static readonly DateTimeOffset BaseTime = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        private const string OpsBase = "/api/v1/compliance-operations";

        [SetUp]
        public async Task SetUp()
        {
            _factory = new QueueHealthFactory();
            _client  = _factory.CreateClient();
            _jwt     = await ObtainJwtAsync(_client, $"qh-{Guid.NewGuid():N}");
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwt);
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        // ════════════════════════════════════════════════════════════════════
        // Service-level unit tests
        // ════════════════════════════════════════════════════════════════════

        // ── QH01: Empty queue → Healthy ────────────────────────────────────

        [Test]
        public async Task QH01_EmptyQueue_HealthStatusIsHealthy_AllCountsZero()
        {
            var (svc, _) = CreateService();

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Success, Is.True, "QH01: GetOverview must succeed");
            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("Healthy"),
                "QH01: empty queue must be Healthy");
            Assert.That(result.Overview.TotalQueueItems, Is.EqualTo(0), "QH01: TotalQueueItems must be 0");
            Assert.That(result.Overview.BlockedCount,   Is.EqualTo(0), "QH01: BlockedCount must be 0");
            Assert.That(result.Overview.OverdueCount,   Is.EqualTo(0), "QH01: OverdueCount must be 0");
            Assert.That(result.Overview.DueSoonCount,   Is.EqualTo(0), "QH01: DueSoonCount must be 0");
        }

        // ── QH02: All OnTrack → Healthy ────────────────────────────────────

        [Test]
        public async Task QH02_AllOnTrackItems_HealthStatusIsHealthy()
        {
            var (svc, tp) = CreateService();
            var now = tp.GetUtcNow().DateTime;

            await AddItem(svc, tp, dueAt: now.AddDays(10), blocked: false);
            await AddItem(svc, tp, dueAt: now.AddDays(15), blocked: false);

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("Healthy"),
                "QH02: all OnTrack items must produce Healthy status");
            Assert.That(result.Overview.OnTrackCount, Is.EqualTo(2), "QH02: OnTrackCount must be 2");
        }

        // ── QH03: Any DueSoon → AtRisk ─────────────────────────────────────

        [Test]
        public async Task QH03_AnyDueSoonItem_HealthStatusIsAtRisk()
        {
            var (svc, tp) = CreateService();
            var now = tp.GetUtcNow().DateTime;

            await AddItem(svc, tp, dueAt: now.AddDays(10), blocked: false);  // OnTrack
            await AddItem(svc, tp, dueAt: now.AddDays(2),  blocked: false);  // DueSoon

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("AtRisk"),
                "QH03: DueSoon item must push status to AtRisk");
            Assert.That(result.Overview.DueSoonCount, Is.EqualTo(1), "QH03: DueSoonCount must be 1");
        }

        // ── QH04: Any Overdue → Critical ───────────────────────────────────

        [Test]
        public async Task QH04_AnyOverdueItem_HealthStatusIsCritical()
        {
            var (svc, tp) = CreateService();
            var now = tp.GetUtcNow().DateTime;

            await AddItem(svc, tp, dueAt: now.AddDays(2),  blocked: false);  // DueSoon
            await AddItem(svc, tp, dueAt: now.AddDays(-1), blocked: false);  // Overdue

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("Critical"),
                "QH04: Overdue item must push status to Critical");
            Assert.That(result.Overview.OverdueCount, Is.EqualTo(1), "QH04: OverdueCount must be 1");
        }

        // ── QH05: Any Blocked → Critical ──────────────────────────────────

        [Test]
        public async Task QH05_AnyBlockedItem_HealthStatusIsCritical()
        {
            var (svc, tp) = CreateService();
            var now = tp.GetUtcNow().DateTime;

            await AddItem(svc, tp, dueAt: now.AddDays(5), blocked: false);  // OnTrack
            await AddItem(svc, tp, dueAt: null,            blocked: true);   // Blocked

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("Critical"),
                "QH05: Blocked item must push status to Critical");
            Assert.That(result.Overview.BlockedCount, Is.EqualTo(1), "QH05: BlockedCount must be 1");
        }

        // ── QH06: Blocked + Overdue → Critical; message references blocked ─

        [Test]
        public async Task QH06_BlockedAndOverdue_Critical_MessageReferencesBlocked()
        {
            var (svc, tp) = CreateService();
            var now = tp.GetUtcNow().DateTime;

            await AddItem(svc, tp, dueAt: now.AddDays(-2), blocked: false);  // Overdue
            await AddItem(svc, tp, dueAt: null,             blocked: true);   // Blocked

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("Critical"),
                "QH06: Blocked+Overdue must be Critical");
            Assert.That(result.Overview.HealthSummaryMessage, Does.Contain("blocked").IgnoreCase,
                "QH06: health summary must reference blocked items when both blocked and overdue");
        }

        // ── QH07: 4 days → OnTrack ────────────────────────────────────────

        [Test]
        public async Task QH07_DueIn4Days_SlaBoundary_IsOnTrack()
        {
            var (svc, tp) = CreateService();
            var now = tp.GetUtcNow().DateTime;
            var dueAt = now.AddDays(4);

            var status = svc.ClassifySlaStatus(dueAt, now);

            Assert.That(status, Is.EqualTo(ComplianceOpsSlaStatus.OnTrack),
                "QH07: 4 days until due must be OnTrack (warning window is ≤ 3 days)");
        }

        // ── QH08: 2 days → DueSoon ────────────────────────────────────────

        [Test]
        public async Task QH08_DueIn2Days_SlaBoundary_IsDueSoon()
        {
            await Task.CompletedTask;
            var (svc, _) = CreateService();
            var now   = BaseTime.DateTime;
            var dueAt = now.AddDays(2);

            var status = svc.ClassifySlaStatus(dueAt, now);

            Assert.That(status, Is.EqualTo(ComplianceOpsSlaStatus.DueSoon),
                "QH08: 2 days until due must be DueSoon");
        }

        // ── QH09: Past due → Overdue ──────────────────────────────────────

        [Test]
        public async Task QH09_PastDue_SlaBoundary_IsOverdue()
        {
            await Task.CompletedTask;
            var (svc, _) = CreateService();
            var now   = BaseTime.DateTime;
            var dueAt = now.AddDays(-1);

            var status = svc.ClassifySlaStatus(dueAt, now);

            Assert.That(status, Is.EqualTo(ComplianceOpsSlaStatus.Overdue),
                "QH09: past-due item must be Overdue");
        }

        // ── QH10: No deadline → OnTrack ───────────────────────────────────

        [Test]
        public async Task QH10_NoDeadline_SlaStatus_IsOnTrack()
        {
            await Task.CompletedTask;
            var (svc, _) = CreateService();

            var status = svc.ClassifySlaStatus(null, BaseTime.DateTime);

            Assert.That(status, Is.EqualTo(ComplianceOpsSlaStatus.OnTrack),
                "QH10: no deadline must always be OnTrack");
        }

        // ── QH11: Fail-closed scores higher within same SLA bucket ─────────

        [Test]
        public async Task QH11_FailClosedScoresHigher_WithinSameSLABucket()
        {
            await Task.CompletedTask;
            var (svc, _) = CreateService();
            var now = BaseTime.DateTime;

            int failClosedScore   = svc.ComputePriorityScore(ComplianceOpsSlaStatus.OnTrack, true,  now.AddDays(-1), now);
            int nonFailClosedScore = svc.ComputePriorityScore(ComplianceOpsSlaStatus.OnTrack, false, now.AddDays(-1), now);

            Assert.That(failClosedScore, Is.GreaterThan(nonFailClosedScore),
                "QH11: fail-closed items must score higher than non-fail-closed in the same SLA bucket");
        }

        // ── QH12: Blocked > Overdue > DueSoon > OnTrack (priority ordering) ─

        [Test]
        public async Task QH12_PriorityOrdering_BlockedOverduedueSoonOnTrack()
        {
            await Task.CompletedTask;
            var (svc, _) = CreateService();
            var now = BaseTime.DateTime;

            int blocked  = svc.ComputePriorityScore(ComplianceOpsSlaStatus.Blocked,  false, now, now);
            int overdue  = svc.ComputePriorityScore(ComplianceOpsSlaStatus.Overdue,  false, now, now);
            int dueSoon  = svc.ComputePriorityScore(ComplianceOpsSlaStatus.DueSoon,  false, now, now);
            int onTrack  = svc.ComputePriorityScore(ComplianceOpsSlaStatus.OnTrack,  false, now, now);

            Assert.That(blocked,  Is.GreaterThan(overdue),  "QH12: Blocked must outscore Overdue");
            Assert.That(overdue,  Is.GreaterThan(dueSoon),  "QH12: Overdue must outscore DueSoon");
            Assert.That(dueSoon,  Is.GreaterThan(onTrack),  "QH12: DueSoon must outscore OnTrack");
        }

        // ── QH13: Older items outrank newer items within same SLA bucket ───

        [Test]
        public async Task QH13_OlderItemsOutrank_NewerItemsInSameBucket()
        {
            await Task.CompletedTask;
            var (svc, _) = CreateService();
            var now = BaseTime.DateTime;

            int olderScore = svc.ComputePriorityScore(ComplianceOpsSlaStatus.DueSoon, false, now.AddDays(-5), now);
            int newerScore = svc.ComputePriorityScore(ComplianceOpsSlaStatus.DueSoon, false, now.AddDays(-1), now);

            Assert.That(olderScore, Is.GreaterThan(newerScore),
                "QH13: older items must outscore newer items within the same SLA bucket");
        }

        // ── QH14: Upsert updates existing item ────────────────────────────

        [Test]
        public async Task QH14_Upsert_UpdatesExistingItem_SameItemId()
        {
            var (svc, tp) = CreateService();
            string itemId = "item-qh14";

            var item = MakeItem(itemId: itemId, title: "Original Title");
            await svc.UpsertQueueItemAsync(item, "actor", "corr");

            var updated = MakeItem(itemId: itemId, title: "Updated Title");
            await svc.UpsertQueueItemAsync(updated, "actor", "corr");

            var result = await svc.GetQueueAsync(
                new ComplianceOpsQueueRequest { PageSize = 50 }, "actor", "corr");

            var items = result.Items.Where(i => i.ItemId == itemId).ToList();
            Assert.That(items.Count, Is.EqualTo(1), "QH14: upsert must not duplicate items");
            Assert.That(items[0].Title, Is.EqualTo("Updated Title"), "QH14: title must be updated");
        }

        // ── QH15: Resolve removes item from queue ─────────────────────────

        [Test]
        public async Task QH15_Resolve_RemovesItemFromQueue()
        {
            var (svc, tp) = CreateService();
            string itemId = "item-qh15";

            await svc.UpsertQueueItemAsync(MakeItem(itemId: itemId, title: "To Be Resolved"), "actor", "corr");
            bool resolved = await svc.ResolveQueueItemAsync(itemId, "actor", "corr");

            var result = await svc.GetQueueAsync(
                new ComplianceOpsQueueRequest { PageSize = 50 }, "actor", "corr");

            Assert.That(resolved, Is.True, "QH15: resolve must return true");
            Assert.That(result.Items.Any(i => i.ItemId == itemId), Is.False,
                "QH15: resolved item must not appear in queue");
        }

        // ── QH16: CountByWorkflowType correct ─────────────────────────────

        [Test]
        public async Task QH16_CountByWorkflowType_Correct_AfterMixedUpserts()
        {
            var (svc, tp) = CreateService();

            await svc.UpsertQueueItemAsync(MakeItem(workflowType: ComplianceOpsWorkflowType.ComplianceCase), "actor", "corr");
            await svc.UpsertQueueItemAsync(MakeItem(workflowType: ComplianceOpsWorkflowType.ComplianceCase), "actor", "corr");
            await svc.UpsertQueueItemAsync(MakeItem(workflowType: ComplianceOpsWorkflowType.ScheduledReporting), "actor", "corr");

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.CountByWorkflowType.ContainsKey(ComplianceOpsWorkflowType.ComplianceCase),
                Is.True, "QH16: CountByWorkflowType must include ComplianceCase");
            Assert.That(result.Overview.CountByWorkflowType[ComplianceOpsWorkflowType.ComplianceCase],
                Is.EqualTo(2), "QH16: ComplianceCase count must be 2");
            Assert.That(result.Overview.CountByWorkflowType[ComplianceOpsWorkflowType.ScheduledReporting],
                Is.EqualTo(1), "QH16: ScheduledReporting count must be 1");
        }

        // ── QH17: Filter by SlaStatus ─────────────────────────────────────

        [Test]
        public async Task QH17_FilterBySlaStatus_ReturnsOnlyMatchingItems()
        {
            var (svc, tp) = CreateService();
            var now = tp.GetUtcNow().DateTime;

            await AddItem(svc, tp, dueAt: now.AddDays(10), blocked: false); // OnTrack
            await AddItem(svc, tp, dueAt: now.AddDays(2),  blocked: false); // DueSoon
            await AddItem(svc, tp, dueAt: now.AddDays(-1), blocked: false); // Overdue

            var result = await svc.GetQueueAsync(
                new ComplianceOpsQueueRequest
                {
                    SlaStatus = ComplianceOpsSlaStatus.Overdue,
                    PageSize  = 50
                }, "actor", "corr");

            Assert.That(result.Items.All(i => i.SlaStatus == ComplianceOpsSlaStatus.Overdue),
                Is.True, "QH17: all returned items must have Overdue status");
            Assert.That(result.Items.Count, Is.EqualTo(1), "QH17: exactly 1 overdue item");
        }

        // ── QH18: Filter by WorkflowType ──────────────────────────────────

        [Test]
        public async Task QH18_FilterByWorkflowType_ReturnsOnlyMatchingItems()
        {
            var (svc, tp) = CreateService();

            await svc.UpsertQueueItemAsync(MakeItem(workflowType: ComplianceOpsWorkflowType.ComplianceCase,   title: "Case A"), "actor", "corr");
            await svc.UpsertQueueItemAsync(MakeItem(workflowType: ComplianceOpsWorkflowType.ApprovalWorkflow, title: "Approval B"), "actor", "corr");

            var result = await svc.GetQueueAsync(
                new ComplianceOpsQueueRequest
                {
                    WorkflowType = ComplianceOpsWorkflowType.ComplianceCase,
                    PageSize     = 50
                }, "actor", "corr");

            Assert.That(result.Items.All(i => i.WorkflowType == ComplianceOpsWorkflowType.ComplianceCase),
                Is.True, "QH18: all returned items must be ComplianceCase type");
        }

        // ── QH19: Filter FailClosedOnly ───────────────────────────────────

        [Test]
        public async Task QH19_FilterFailClosedOnly_ReturnsOnlyFailClosedItems()
        {
            var (svc, tp) = CreateService();

            var failClosed = MakeItem(title: "Fail Closed Item");
            failClosed.IsFailClosed = true;
            await svc.UpsertQueueItemAsync(failClosed, "actor", "corr");

            var notFailClosed = MakeItem(title: "Non-Fail-Closed Item");
            notFailClosed.IsFailClosed = false;
            await svc.UpsertQueueItemAsync(notFailClosed, "actor", "corr");

            var result = await svc.GetQueueAsync(
                new ComplianceOpsQueueRequest { FailClosedOnly = true, PageSize = 50 },
                "actor", "corr");

            Assert.That(result.Items.All(i => i.IsFailClosed), Is.True,
                "QH19: FailClosedOnly filter must return only fail-closed items");
            Assert.That(result.Items.Count, Is.EqualTo(1), "QH19: exactly 1 fail-closed item");
        }

        // ── QH20: HealthSummaryMessage non-null for all health states ──────

        [Test]
        public async Task QH20_HealthSummaryMessage_NonNullForAllHealthStates()
        {
            // Empty → Healthy
            var (svc1, _) = CreateService();
            var r1 = await svc1.GetOverviewAsync("actor", "corr");
            Assert.That(r1.Overview!.HealthSummaryMessage, Is.Not.Null.And.Not.Empty,
                "QH20: HealthSummaryMessage must be populated for Healthy state");

            // DueSoon → AtRisk
            var (svc2, tp2) = CreateService();
            var now2 = tp2.GetUtcNow().DateTime;
            await AddItem(svc2, tp2, dueAt: now2.AddDays(1), blocked: false);
            var r2 = await svc2.GetOverviewAsync("actor", "corr");
            Assert.That(r2.Overview!.HealthSummaryMessage, Is.Not.Null.And.Not.Empty,
                "QH20: HealthSummaryMessage must be populated for AtRisk state");

            // Blocked → Critical
            var (svc3, tp3) = CreateService();
            await AddItem(svc3, tp3, dueAt: null, blocked: true);
            var r3 = await svc3.GetOverviewAsync("actor", "corr");
            Assert.That(r3.Overview!.HealthSummaryMessage, Is.Not.Null.And.Not.Empty,
                "QH20: HealthSummaryMessage must be populated for Critical state");
        }

        // ════════════════════════════════════════════════════════════════════
        // HTTP integration tests
        // ════════════════════════════════════════════════════════════════════

        // ── QH21: HTTP GET overview → 200 ─────────────────────────────────

        [Test]
        public async Task QH21_HttpOverview_Returns200_WithStructuredResponse()
        {
            var resp   = await _client.GetAsync($"{OpsBase}/overview");
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<ComplianceOperationsOverviewResponse>();
            Assert.That(result, Is.Not.Null, "QH21: response must not be null");
            Assert.That(result!.Success, Is.True, "QH21: Success must be true");
            Assert.That(result.Overview, Is.Not.Null, "QH21: Overview must not be null");
            Assert.That(result.Overview!.OverallHealthStatus, Is.Not.Null.And.Not.Empty,
                "QH21: OverallHealthStatus must be populated");
            Assert.That(result.Overview.HealthSummaryMessage, Is.Not.Null.And.Not.Empty,
                "QH21: HealthSummaryMessage must be populated");
        }

        // ── QH22: HTTP POST queue → 200 with items list ───────────────────

        [Test]
        public async Task QH22_HttpQueue_Returns200_WithItemsList()
        {
            var resp   = await _client.PostAsJsonAsync($"{OpsBase}/queue",
                new ComplianceOpsQueueRequest { PageSize = 10, Page = 0 });
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<ComplianceOpsQueueResponse>();
            Assert.That(result, Is.Not.Null, "QH22: response must not be null");
            Assert.That(result!.Success, Is.True, "QH22: Success must be true");
            Assert.That(result.Items, Is.Not.Null, "QH22: Items list must not be null");
        }

        // ── QH23: Unauthenticated overview → 401 ─────────────────────────

        [Test]
        public async Task QH23_UnauthenticatedOverview_Returns401()
        {
            using var anonClient = _factory.CreateClient();
            var resp = await anonClient.GetAsync($"{OpsBase}/overview");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "QH23: unauthenticated request must return 401");
        }

        // ── QH24: HealthSummaryMessage references blocked count when mixed ─

        [Test]
        public async Task QH24_HealthSummaryMessage_ReferencesBlockedCount_WhenBlockedAndOverdue()
        {
            var (svc, tp) = CreateService();
            var now = tp.GetUtcNow().DateTime;

            await AddItem(svc, tp, dueAt: now.AddDays(-3), blocked: false);  // Overdue
            await AddItem(svc, tp, dueAt: null,             blocked: true);   // Blocked

            var result = await svc.GetOverviewAsync("actor", "corr");

            Assert.That(result.Overview!.OverallHealthStatus, Is.EqualTo("Critical"),
                "QH24: blocked+overdue must be Critical");
            Assert.That(result.Overview.HealthSummaryMessage, Does.Contain("1").Or.Contain("blocked").IgnoreCase,
                "QH24: health summary must reference blocked count when blocking items exist");
        }

        // ── QH25: Overview deterministic across 3 calls ───────────────────

        [Test]
        public async Task QH25_Overview_Deterministic_Across3Calls()
        {
            var r1 = await _client.GetAsync($"{OpsBase}/overview");
            var r2 = await _client.GetAsync($"{OpsBase}/overview");
            var r3 = await _client.GetAsync($"{OpsBase}/overview");

            var res1 = await r1.Content.ReadFromJsonAsync<ComplianceOperationsOverviewResponse>();
            var res2 = await r2.Content.ReadFromJsonAsync<ComplianceOperationsOverviewResponse>();
            var res3 = await r3.Content.ReadFromJsonAsync<ComplianceOperationsOverviewResponse>();

            Assert.That(res1!.Overview!.OverallHealthStatus,
                Is.EqualTo(res2!.Overview!.OverallHealthStatus), "QH25: health status stable run 1 vs 2");
            Assert.That(res2.Overview.OverallHealthStatus,
                Is.EqualTo(res3!.Overview!.OverallHealthStatus), "QH25: health status stable run 2 vs 3");
            Assert.That(res1.Overview.TotalQueueItems,
                Is.EqualTo(res2.Overview.TotalQueueItems), "QH25: TotalQueueItems stable");
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        private static (ComplianceOperationsService svc, FakeTimeProvider tp)
            CreateService()
        {
            var tp  = new FakeTimeProvider(BaseTime);
            var svc = new ComplianceOperationsService(
                NullLogger<ComplianceOperationsService>.Instance,
                tp);
            return (svc, tp);
        }

        private static async Task AddItem(
            ComplianceOperationsService svc,
            FakeTimeProvider tp,
            DateTime? dueAt,
            bool blocked)
        {
            var item = MakeItem();
            item.DueAt           = dueAt;
            item.IsFailClosed    = blocked;
            item.BlockerCategory = blocked
                ? ComplianceOpsBlockerCategory.MissingEvidence
                : ComplianceOpsBlockerCategory.None;
            // Note: SlaStatus is re-computed by UpsertQueueItemAsync based on BlockerCategory + DueAt
            await svc.UpsertQueueItemAsync(item, "actor", "corr");
        }

        private static ComplianceOpsQueueItem MakeItem(
            string? itemId       = null,
            string  title        = "Test Queue Item",
            ComplianceOpsWorkflowType workflowType = ComplianceOpsWorkflowType.ComplianceCase)
        {
            return new ComplianceOpsQueueItem
            {
                ItemId       = itemId ?? Guid.NewGuid().ToString(),
                Title        = title,
                WorkflowType = workflowType,
                SourceId     = Guid.NewGuid().ToString(),
                CreatedAt    = BaseTime.DateTime.AddDays(-1),
                LastUpdatedAt = BaseTime.DateTime,
                SlaStatus    = ComplianceOpsSlaStatus.OnTrack
            };
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string userTag)
        {
            string email = $"qhealth-{userTag}@queue-health-test.biatec.example.com";
            HttpResponseMessage resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email           = email,
                Password        = "QueueHealthIT!Pass1",
                ConfirmPassword = "QueueHealthIT!Pass1",
                FullName        = $"Queue Health Test User ({userTag})"
            });

            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Created),
                $"Registration must succeed for user '{userTag}'");

            var doc   = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
            string? token = doc?.RootElement.GetProperty("accessToken").GetString();
            Assert.That(token, Is.Not.Null.And.Not.Empty);
            return token!;
        }
    }
}
