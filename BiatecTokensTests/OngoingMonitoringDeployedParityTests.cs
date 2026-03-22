using BiatecTokensApi.Models.OngoingMonitoring;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Ongoing-Monitoring Deployed-Parity Tests — prove that the post-approval
    /// compliance monitoring pipeline at <c>/api/v1/ongoing-monitoring</c> is
    /// authoritative and ready for live operator workflows.
    ///
    /// These tests address roadmap gaps:
    ///   - Compliance Monitoring (92%): Post-approval ongoing monitoring pipeline
    ///   - Risk Assessment (70%): Severity-driven task escalation and live risk signals
    ///   - FATF Guidelines (18%): Post-onboarding continuous monitoring (AML/FATF Rec 10)
    ///   - Multi-User Access (80%): Reviewer assignment and handoff in monitoring tasks
    ///
    /// Coverage:
    ///
    /// OM01: Create monitoring task → Success=true, TaskId populated
    /// OM02: Create with empty CaseId → 400 fail-closed
    /// OM03: Unauthenticated create → 401 fail-closed
    /// OM04: Created task has expected fields: TaskId, CaseId, IssuerId, SubjectId
    /// OM05: List tasks for known IssuerId → includes the created task
    /// OM06: List tasks with no filter → Success=true, Items not null
    /// OM07: Get task by taskId → returns same task
    /// OM08: Get non-existent taskId → 404
    /// OM09: Start reassessment → task status transitions to InProgress
    /// OM10: Start reassessment on non-existent taskId → 404/400 fail-closed
    /// OM11: Defer task → task status transitions to Deferred
    /// OM12: Defer with empty Rationale → 400 fail-closed
    /// OM13: Escalate task → Success=true, EscalationReason recorded
    /// OM14: Escalate with empty EscalationReason → 400 fail-closed
    /// OM15: Close task after reassessment → task status Resolved
    /// OM16: Close with empty ResolutionNotes → 400 fail-closed
    /// OM17: Timeline includes TaskCreated event after creation
    /// OM18: Timeline grows after reassessment start
    /// OM19: Severity Critical creates task with correct severity
    /// OM20: Full monitoring lifecycle: create → start reassessment → escalate → close
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class OngoingMonitoringDeployedParityTests
    {
        // ════════════════════════════════════════════════════════════════════
        // Factory
        // ════════════════════════════════════════════════════════════════════

        private sealed class OmFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "OngoingMonitoringDeployedParityTestKey!32",
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
                        ["KeyManagementConfig:HardcodedKey"] = "OngoingMonitoringTestKey32Chars!!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "ongoing-monitoring-dp-test",
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

        private OmFactory  _factory = null!;
        private HttpClient _client  = null!;
        private const string OmBase = "/api/v1/ongoing-monitoring";

        [SetUp]
        public async Task SetUp()
        {
            _factory = new OmFactory();
            _client  = _factory.CreateClient();
            string jwt = await ObtainJwtAsync(_client, $"om-{Guid.NewGuid():N}");
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        // ════════════════════════════════════════════════════════════════════
        // OM01: Create monitoring task → Success=true, TaskId populated
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM01_Create_ValidRequest_ReturnsSuccessWithTaskId()
        {
            var result = await CreateTaskAsync();

            Assert.That(result.Success, Is.True, "OM01: create must succeed");
            Assert.That(result.Task, Is.Not.Null, "OM01: Task must not be null");
            Assert.That(result.Task!.TaskId, Is.Not.Null.And.Not.Empty, "OM01: TaskId must be populated");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM02: Create with empty CaseId → 400 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM02_Create_EmptyCaseId_Returns400()
        {
            var req = BuildRequest();
            req.CaseId = "";
            var httpResp = await _client.PostAsJsonAsync(OmBase, req);
            Assert.That(httpResp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "OM02: empty CaseId must return 400 fail-closed");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM03: Unauthenticated create → 401 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM03_Unauthenticated_Create_Returns401()
        {
            using var anon = _factory.CreateClient();
            var resp = await anon.PostAsJsonAsync(OmBase, BuildRequest());
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "OM03: unauthenticated create must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM04: Created task has expected fields
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM04_Create_TaskHasExpectedFields()
        {
            var req    = BuildRequest();
            var result = await CreateTaskAsync(req);

            var task = result.Task!;
            Assert.That(task.CaseId,    Is.EqualTo(req.CaseId),    "OM04: CaseId must match request");
            Assert.That(task.IssuerId,  Is.EqualTo(req.IssuerId),  "OM04: IssuerId must match request");
            Assert.That(task.SubjectId, Is.EqualTo(req.SubjectId), "OM04: SubjectId must match request");
            Assert.That(task.CreatedAt, Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1)),
                "OM04: CreatedAt must be recent");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM05: List tasks for known IssuerId → includes the created task
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM05_List_ByIssuerId_IncludesCreatedTask()
        {
            var req    = BuildRequest();
            var result = await CreateTaskAsync(req);
            string taskId = result.Task!.TaskId;

            var listResp = await _client.GetAsync(
                $"{OmBase}?issuerId={Uri.EscapeDataString(req.IssuerId)}");
            listResp.EnsureSuccessStatusCode();

            var list = await listResp.Content.ReadFromJsonAsync<ListMonitoringTasksResponse>();
            Assert.That(list!.Success, Is.True, "OM05: list must succeed");
            Assert.That(list.Tasks.Any(t => t.TaskId == taskId), Is.True,
                "OM05: created task must appear in filtered list");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM06: List tasks with no filter → Success=true, Items not null
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM06_List_NoFilter_ReturnsSuccessWithItems()
        {
            var listResp = await _client.GetAsync(OmBase);
            listResp.EnsureSuccessStatusCode();

            var list = await listResp.Content.ReadFromJsonAsync<ListMonitoringTasksResponse>();
            Assert.That(list!.Success, Is.True, "OM06: unfiltered list must succeed");
            Assert.That(list.Tasks, Is.Not.Null, "OM06: Tasks must not be null");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM07: Get task by taskId → returns same task
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM07_GetTask_ByTaskId_ReturnsSameTask()
        {
            var result = await CreateTaskAsync();
            string taskId = result.Task!.TaskId;

            var getResp = await _client.GetAsync($"{OmBase}/{taskId}");
            getResp.EnsureSuccessStatusCode();

            var task = await getResp.Content.ReadFromJsonAsync<GetMonitoringTaskResponse>();
            Assert.That(task!.Success, Is.True, "OM07: get must succeed");
            Assert.That(task.Task!.TaskId, Is.EqualTo(taskId), "OM07: retrieved taskId must match");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM08: Get non-existent taskId → 404
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM08_GetTask_NonExistent_Returns404()
        {
            var resp = await _client.GetAsync($"{OmBase}/nonexistent-task-{Guid.NewGuid():N}");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "OM08: non-existent taskId must return 404");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM09: Start reassessment → task status transitions to InProgress
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM09_StartReassessment_TransitionsToInProgress()
        {
            var result = await CreateTaskAsync();
            string taskId = result.Task!.TaskId;

            var rsResp = await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/start-reassessment",
                new StartReassessmentRequest { Notes = "OM09 reassessment" });
            rsResp.EnsureSuccessStatusCode();

            var rsResult = await rsResp.Content.ReadFromJsonAsync<StartReassessmentResponse>();
            Assert.That(rsResult!.Success, Is.True, "OM09: start-reassessment must succeed");
            Assert.That(rsResult.Task!.Status, Is.EqualTo(MonitoringTaskStatus.InProgress),
                "OM09: task must transition to InProgress after reassessment starts");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM10: Start reassessment on non-existent taskId → 404/400 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM10_StartReassessment_NonExistentTask_FailsClosed()
        {
            var resp = await _client.PostAsJsonAsync(
                $"{OmBase}/nonexistent-{Guid.NewGuid():N}/start-reassessment",
                new StartReassessmentRequest { Notes = "OM10" });
            Assert.That(resp.IsSuccessStatusCode, Is.False,
                "OM10: start-reassessment on non-existent task must fail");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM11: Defer task → task status transitions to Deferred
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM11_DeferTask_TransitionsToDeferred()
        {
            var result = await CreateTaskAsync();
            string taskId = result.Task!.TaskId;

            var deferResp = await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/defer",
                new DeferMonitoringTaskRequest
                {
                    DeferUntil = DateTimeOffset.UtcNow.AddDays(14),
                    Rationale  = "OM11: operator requested deferral pending additional evidence"
                });
            deferResp.EnsureSuccessStatusCode();

            var deferResult = await deferResp.Content.ReadFromJsonAsync<DeferMonitoringTaskResponse>();
            Assert.That(deferResult!.Success, Is.True, "OM11: defer must succeed");
            Assert.That(deferResult.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Deferred),
                "OM11: task must transition to Deferred");
            Assert.That(deferResult.Task.DeferralRationale, Is.Not.Null.And.Not.Empty,
                "OM11: DeferralRationale must be recorded");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM12: Defer with empty Rationale → 400 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM12_DeferTask_EmptyRationale_Returns400()
        {
            var result = await CreateTaskAsync();
            string taskId = result.Task!.TaskId;

            var resp = await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/defer",
                new DeferMonitoringTaskRequest
                {
                    DeferUntil = DateTimeOffset.UtcNow.AddDays(7),
                    Rationale  = ""   // empty → fail-closed
                });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "OM12: defer with empty Rationale must return 400");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM13: Escalate task → Success=true, EscalationReason recorded
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM13_EscalateTask_RecordsEscalationReason()
        {
            var result = await CreateTaskAsync();
            string taskId = result.Task!.TaskId;

            var escResp = await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/escalate",
                new EscalateMonitoringTaskRequest
                {
                    EscalationReason = "OM13: high-risk transaction detected",
                    Severity         = MonitoringTaskSeverity.High
                });
            escResp.EnsureSuccessStatusCode();

            var escResult = await escResp.Content.ReadFromJsonAsync<EscalateMonitoringTaskResponse>();
            Assert.That(escResult!.Success, Is.True, "OM13: escalate must succeed");
            Assert.That(escResult.Task!.EscalationReason, Is.Not.Null.And.Not.Empty,
                "OM13: EscalationReason must be recorded");
            Assert.That(escResult.Task.Status, Is.EqualTo(MonitoringTaskStatus.Escalated),
                "OM13: task must transition to Escalated status");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM14: Escalate with empty EscalationReason → 400 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM14_EscalateTask_EmptyReason_Returns400()
        {
            var result = await CreateTaskAsync();
            string taskId = result.Task!.TaskId;

            var resp = await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/escalate",
                new EscalateMonitoringTaskRequest { EscalationReason = "" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "OM14: escalate with empty EscalationReason must return 400");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM15: Close task after reassessment → task status Resolved
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM15_CloseTask_AfterReassessment_StatusResolved()
        {
            var result = await CreateTaskAsync();
            string taskId = result.Task!.TaskId;

            // Start reassessment first
            await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/start-reassessment",
                new StartReassessmentRequest { Notes = "OM15 pre-close reassessment" });

            // Close with resolution
            var closeResp = await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/close",
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = "OM15: no adverse findings, subject cleared"
                });
            closeResp.EnsureSuccessStatusCode();

            var closeResult = await closeResp.Content.ReadFromJsonAsync<CloseMonitoringTaskResponse>();
            Assert.That(closeResult!.Success, Is.True, "OM15: close must succeed");
            Assert.That(closeResult.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Resolved),
                "OM15: task must be Resolved after close");
            Assert.That(closeResult.Task.ResolutionNotes, Is.Not.Null.And.Not.Empty,
                "OM15: ResolutionNotes must be recorded");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM16: Close with empty ResolutionNotes → 400 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM16_CloseTask_EmptyResolutionNotes_Returns400()
        {
            var result = await CreateTaskAsync();
            string taskId = result.Task!.TaskId;

            var resp = await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/close",
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.Clear,
                    ResolutionNotes = ""   // empty → fail-closed
                });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "OM16: close with empty ResolutionNotes must return 400");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM17: Timeline includes TaskCreated event after creation
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM17_Timeline_AfterCreate_ContainsTaskCreatedEvent()
        {
            var result = await CreateTaskAsync();
            var task   = result.Task!;

            Assert.That(task.Timeline, Is.Not.Null, "OM17: Timeline must not be null");
            Assert.That(task.Timeline.Count, Is.GreaterThan(0),
                "OM17: Timeline must contain at least one event after creation");
            Assert.That(task.Timeline.Any(e => e.EventType == MonitoringTaskEventType.TaskCreated), Is.True,
                "OM17: Timeline must contain a TaskCreated event");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM18: Timeline grows after reassessment start
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM18_Timeline_GrowsAfterReassessmentStart()
        {
            var result = await CreateTaskAsync();
            string taskId = result.Task!.TaskId;
            int initialEventCount = result.Task.Timeline.Count;

            // Start reassessment
            var rsResp = await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/start-reassessment",
                new StartReassessmentRequest { Notes = "OM18 reassessment" });
            rsResp.EnsureSuccessStatusCode();
            var rsResult = await rsResp.Content.ReadFromJsonAsync<StartReassessmentResponse>();

            Assert.That(rsResult!.Task!.Timeline.Count, Is.GreaterThan(initialEventCount),
                "OM18: timeline must grow after starting reassessment");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM19: Severity Critical creates task with correct severity
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM19_Create_SeverityCritical_TaskHasCriticalSeverity()
        {
            var req      = BuildRequest();
            req.Severity = MonitoringTaskSeverity.Critical;
            var result   = await CreateTaskAsync(req);

            Assert.That(result.Task!.Severity, Is.EqualTo(MonitoringTaskSeverity.Critical),
                "OM19: task created with Critical severity must reflect that in the response");
        }

        // ════════════════════════════════════════════════════════════════════
        // OM20: Full lifecycle — create → start reassessment → escalate → close
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OM20_FullMonitoringLifecycle_CreateReassessEscalateClose()
        {
            // 1. Create monitoring task
            var req    = BuildRequest();
            var result = await CreateTaskAsync(req);
            string taskId = result.Task!.TaskId;
            Assert.That(result.Success, Is.True, "OM20: create must succeed");

            // 2. Start reassessment
            var rsResp = await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/start-reassessment",
                new StartReassessmentRequest { Notes = "OM20: beginning review" });
            rsResp.EnsureSuccessStatusCode();
            var rsResult = await rsResp.Content.ReadFromJsonAsync<StartReassessmentResponse>();
            Assert.That(rsResult!.Task!.Status, Is.EqualTo(MonitoringTaskStatus.InProgress),
                "OM20: task must be InProgress after reassessment starts");

            // 3. Escalate
            var escResp = await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/escalate",
                new EscalateMonitoringTaskRequest
                {
                    EscalationReason = "OM20: adverse PEP match found, escalating to compliance officer",
                    Severity         = MonitoringTaskSeverity.High
                });
            escResp.EnsureSuccessStatusCode();
            var escResult = await escResp.Content.ReadFromJsonAsync<EscalateMonitoringTaskResponse>();
            Assert.That(escResult!.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Escalated),
                "OM20: task must be Escalated after escalation");

            // 4. Close with resolution
            var closeResp = await _client.PostAsJsonAsync(
                $"{OmBase}/{taskId}/close",
                new CloseMonitoringTaskRequest
                {
                    Resolution      = MonitoringTaskResolution.ActionTaken,
                    ResolutionNotes = "OM20: manual review completed, PEP match confirmed as false positive"
                });
            closeResp.EnsureSuccessStatusCode();
            var closeResult = await closeResp.Content.ReadFromJsonAsync<CloseMonitoringTaskResponse>();
            Assert.That(closeResult!.Task!.Status, Is.EqualTo(MonitoringTaskStatus.Resolved),
                "OM20: task must be Resolved after close");

            // 5. Verify timeline completeness
            var getResp = await _client.GetAsync($"{OmBase}/{taskId}");
            getResp.EnsureSuccessStatusCode();
            var finalTask = (await getResp.Content.ReadFromJsonAsync<GetMonitoringTaskResponse>())!.Task!;
            Assert.That(finalTask.Timeline.Count, Is.GreaterThanOrEqualTo(4),
                "OM20: timeline must record at least 4 events (create, reassessment, escalate, close)");
            Assert.That(finalTask.CompletedAt.HasValue, Is.True,
                "OM20: CompletedAt must be set after task is closed");
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        private CreateMonitoringTaskRequest BuildRequest() =>
            new CreateMonitoringTaskRequest
            {
                CaseId    = $"case-om-{Guid.NewGuid():N}",
                IssuerId  = $"issuer-om-{Guid.NewGuid():N}",
                SubjectId = $"subject-om-{Guid.NewGuid():N}",
                Reason    = ReassessmentReason.PeriodicSchedule,
                Severity  = MonitoringTaskSeverity.Low,
                DueAt     = DateTimeOffset.UtcNow.AddDays(30),
                Notes     = "Deployed-parity test monitoring task"
            };

        private async Task<CreateMonitoringTaskResponse> CreateTaskAsync(
            CreateMonitoringTaskRequest? req = null)
        {
            req ??= BuildRequest();
            var httpResp = await _client.PostAsJsonAsync(OmBase, req);
            httpResp.EnsureSuccessStatusCode();
            var result = await httpResp.Content.ReadFromJsonAsync<CreateMonitoringTaskResponse>();
            Assert.That(result, Is.Not.Null, "CreateTaskAsync: response must not be null");
            return result!;
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string tag)
        {
            var resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email           = $"om-dp-{tag}@om-dp.biatec.example.com",
                Password        = "OmDpIT!Pass1",
                ConfirmPassword = "OmDpIT!Pass1",
                FullName        = $"OM DP Test ({tag})"
            });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Created));
            var doc   = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            string? t = doc?.RootElement.GetProperty("accessToken").GetString();
            Assert.That(t, Is.Not.Null.And.Not.Empty);
            return t!;
        }
    }
}
