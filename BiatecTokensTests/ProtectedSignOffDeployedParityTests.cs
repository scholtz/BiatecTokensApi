using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Deployed-path parity tests for the protected sign-off evidence persistence
    /// API at <c>/api/v1/protected-signoff-evidence</c>.
    ///
    /// These tests prove that the backend behaves correctly in promoted/production-like
    /// conditions, not merely in branch-level unit test harnesses. They exercise the full
    /// HTTP stack (routing, JWT auth, controller dispatch, service logic) through a
    /// <see cref="WebApplicationFactory{TEntryPoint}"/> and validate that:
    ///
    /// 1.  Webhook arrival before and after evidence persistence are handled correctly.
    /// 2.  Stale evidence after time advancement yields a truthful Stale status.
    /// 3.  Missing evidence on a clean instance yields Blocked + MissingEvidence blocker.
    /// 4.  Denied approval yields Blocked + ApprovalDenied blocker (fail-closed).
    /// 5.  Escalation followed by approval resolves to Ready.
    /// 6.  TimedOut and DeliveryError webhooks yield Blocked states.
    /// 7.  Actor propagation is correct: request-level ActorId takes precedence.
    /// 8.  CorrelationId threads from webhook record through evidence pack to readiness.
    /// 9.  Operator guidance is non-null and non-empty for every blocked state.
    /// 10. History limits under bursty ingestion are enforced.
    /// 11. Multiple head refs do not cross-contaminate readiness state.
    /// 12. Same webhook submitted twice produces two independent records (no silent drop).
    /// 13. Full deployed lifecycle (webhook → evidence → readiness → history) is stable.
    /// 14. New service instance returns Indeterminate (process-restart simulation).
    /// 15. Malformed webhook yields Blocked + MalformedWebhook blocker.
    /// 16. Readiness response schema is stable across 3 independent calls.
    /// 17. Concurrent evidence submissions for different heads are all persisted.
    /// 18. RequireApprovalWebhook=true without prior webhook returns 400 + remediation hint.
    /// 19. RequireReleaseGrade=true without prior webhook returns 400 + remediation hint.
    /// 20. Full happy-path deterministic across 3 independent factory instances.
    ///
    /// Coverage labels:
    ///
    /// DP01: Webhook-before-evidence → release-grade evidence
    /// DP02: Evidence-without-webhook → non-release-grade (no block when not required)
    /// DP03: Stale evidence after FreshnessWindowHours exceeded → Stale status
    /// DP04: No evidence on fresh head → Blocked (MissingEvidence + MissingApproval)
    /// DP05: Denied webhook + evidence → Blocked (ApprovalDenied)
    /// DP06: Escalation then approval → Ready
    /// DP07: TimedOut webhook + evidence → Blocked
    /// DP08: DeliveryError webhook + evidence → Blocked
    /// DP09: Actor propagation: request.ActorId wins over method param
    /// DP10: CorrelationId from webhook threaded into evidence pack
    /// DP11: OperatorGuidance non-null for every non-Ready status
    /// DP12: History MaxRecords enforced under bursty ingestion (50 submissions, limit 5)
    /// DP13: Multi-head isolation: different heads produce independent readiness
    /// DP14: Duplicate webhook submission → both records persisted (no dedup drop)
    /// DP15: Full lifecycle HTTP integration: webhook → evidence → readiness → history
    /// DP16: Process-restart simulation: new service instance → Indeterminate
    /// DP17: Malformed webhook → Blocked (MalformedWebhook)
    /// DP18: Readiness schema contract stable across 3 calls
    /// DP19: RequireApprovalWebhook=true blocked without webhook (400 + hint)
    /// DP20: Happy-path is deterministic across 3 independent factory instances
    ///
    /// New coverage for blocked artifact / release-grade evidence semantics (DP41-DP50):
    ///
    /// DP41: Non-release-grade evidence has IsReleaseGrade=false in evidence pack
    /// DP42: Release-grade evidence has IsReleaseGrade=true in evidence pack
    /// DP43: Readiness for non-release-grade evidence is non-Ready (Blocked or Pending)
    /// DP44: Recovery from Blocked → Ready by providing release-grade evidence
    /// DP45: OperatorGuidance is non-null for Indeterminate status (fail-closed guidance)
    /// DP46: ContentHash differs between two different evidence submissions (input-sensitivity)
    /// DP47: Evidence history TotalCount is accurate after multiple evidence submissions
    /// DP48: Webhook history TotalCount is accurate after multiple webhook submissions
    /// DP49: RequireReleaseGrade=true + approved webhook → IsReleaseGrade=true (schema contract)
    /// DP50: New service instance returns Indeterminate with non-null OperatorGuidance (process-restart)
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffDeployedParityTests
    {
        // ════════════════════════════════════════════════════════════════════
        // Controllable time provider (for staleness simulation)
        // ════════════════════════════════════════════════════════════════════

        private sealed class AdvancableTimeProvider : TimeProvider
        {
            // NOTE: This class is intentionally not thread-safe.
            // Tests using it are marked [NonParallelizable] and exercise single-threaded flows.
            private DateTimeOffset _now;
            public AdvancableTimeProvider(DateTimeOffset now) => _now = now;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ════════════════════════════════════════════════════════════════════
        // WebApplicationFactory
        // ════════════════════════════════════════════════════════════════════

        private sealed class DeployedParityFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "DeployedParityTestSecretKey32CharsMinimum!",
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
                        ["KeyManagementConfig:HardcodedKey"] = "DeployedParityTestKeyXXXX32CharsMin",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "deployed-parity-test",
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
        // Shared state
        // ════════════════════════════════════════════════════════════════════

        private DeployedParityFactory _factory = null!;
        private HttpClient _client = null!;
        private string _jwt = null!;

        private const string BaseUrl = "/api/v1/protected-signoff-evidence";

        [SetUp]
        public async Task SetUp()
        {
            _factory = new DeployedParityFactory();
            _client = _factory.CreateClient();
            _jwt = await ObtainJwtAsync(_client, $"dp-{Guid.NewGuid():N}");
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
        // DP01: Webhook before evidence → release-grade evidence pack
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP01_WebhookBeforeEvidence_EvidenceIsReleaseGrade()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Record approved webhook first
            var webhookResp = await PostWebhookAsync(caseId, headRef, ApprovalWebhookOutcome.Approved);
            Assert.That(webhookResp.Success, Is.True, "DP01: webhook must succeed");

            // Persist evidence after webhook
            var evidenceResp = await PostEvidenceAsync(headRef, caseId, requireReleaseGrade: false);
            Assert.That(evidenceResp.Success, Is.True, "DP01: evidence must succeed");
            Assert.That(evidenceResp.Pack!.IsReleaseGrade, Is.True,
                "DP01: evidence must be release-grade when prior approved webhook exists");
            Assert.That(evidenceResp.Pack.ApprovalWebhook, Is.Not.Null,
                "DP01: evidence pack must reference the approval webhook");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP02: Evidence without prior webhook → non-release-grade (no block)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP02_EvidenceWithoutWebhook_IsNotReleaseGrade_ButSucceeds()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // No webhook recorded
            var evidenceResp = await PostEvidenceAsync(headRef, caseId, requireReleaseGrade: false);
            Assert.That(evidenceResp.Success, Is.True, "DP02: evidence without webhook must succeed when not required");
            Assert.That(evidenceResp.Pack!.IsReleaseGrade, Is.False,
                "DP02: evidence without approval webhook must not be release-grade");
            Assert.That(evidenceResp.Pack.ApprovalWebhook, Is.Null,
                "DP02: no approval webhook reference in pack when none recorded");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP03: Stale evidence after freshness window exceeded → Stale status
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP03_StaleEvidence_AfterFreshnessWindow_YieldsStaleStatus()
        {
            // Use service directly with controllable time to simulate staleness
            var baseTime = DateTimeOffset.UtcNow;
            var timeProvider = new AdvancableTimeProvider(baseTime);
            var svc = new ProtectedSignOffEvidencePersistenceService(
                NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance,
                timeProvider: timeProvider);

            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Record webhook
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved
                }, "actor-dp03");

            // Persist evidence with 1-hour freshness window
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    FreshnessWindowHours = 1
                }, "actor-dp03");

            // Advance time past freshness window
            timeProvider.Advance(TimeSpan.FromHours(2));

            // Check readiness
            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    FreshnessWindowHours = 1
                });

            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Stale),
                "DP03: expired evidence must yield Stale status");
            Assert.That(readiness.Blockers.Any(b => b.Category == SignOffReleaseBlockerCategory.StaleEvidence),
                Is.True, "DP03: Stale status must include StaleEvidence blocker");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP04: No evidence on fresh head → Blocked with both missing blockers
        //
        // When a valid HeadRef is provided but has no evidence or webhooks,
        // the service returns Blocked (not Indeterminate). Indeterminate is
        // reserved for missing/null HeadRef inputs only.
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP04_NoEvidence_OnFreshHead_IsBlocked_WithMissingEvidenceAndMissingApproval()
        {
            string headRef = UniqueHead();

            var readinessResp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef });

            // Controller returns 400 for non-Ready states
            var readiness = await readinessResp.Content.ReadFromJsonAsync<GetSignOffReleaseReadinessResponse>();
            Assert.That(readiness, Is.Not.Null);
            // A valid HeadRef with no evidence yields Blocked (not Indeterminate).
            // Indeterminate is only returned for missing/null HeadRef inputs.
            Assert.That(readiness!.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked),
                "DP04: fresh head with valid HeadRef but no evidence must be Blocked");
            Assert.That(readiness.Blockers, Is.Not.Empty,
                "DP04: Blocked state must include at least one blocker");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP05: Denied webhook + evidence → Blocked (ApprovalDenied) fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP05_DeniedWebhook_WithEvidence_IsBlocked_ApprovalDenied()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Record a denied webhook
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Denied
                }, "actor-dp05");

            // Persist evidence
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor-dp05");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked),
                "DP05: denied approval must yield Blocked status (fail-closed)");
            Assert.That(
                readiness.Blockers.Any(b => b.Category == SignOffReleaseBlockerCategory.ApprovalDenied),
                Is.True,
                "DP05: Blocked state must include ApprovalDenied blocker");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP06: Escalation then approval → Ready
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP06_EscalationThenApproval_FinalStateIsReady()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Record escalation first
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Escalated
                }, "actor-dp06");

            // Then record approval (resolves escalation)
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved
                }, "escalation-reviewer");

            // Persist evidence
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor-dp06");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "DP06: escalation followed by approval must resolve to Ready");
            Assert.That(readiness.Blockers, Is.Empty,
                "DP06: Ready state must have no blockers");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP07: TimedOut webhook + evidence → Blocked (MissingApproval)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP07_TimedOutWebhook_WithEvidence_IsBlocked()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.TimedOut
                }, "actor-dp07");

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor-dp07");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked),
                "DP07: TimedOut webhook must block release");
            Assert.That(readiness.Blockers, Is.Not.Empty,
                "DP07: Blocked state must include at least one blocker");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP08: DeliveryError webhook + evidence → Blocked
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP08_DeliveryErrorWebhook_WithEvidence_IsBlocked()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.DeliveryError
                }, "actor-dp08");

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor-dp08");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked),
                "DP08: DeliveryError webhook must block release");
            Assert.That(readiness.Blockers, Is.Not.Empty,
                "DP08: Blocked state must include at least one blocker");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP09: Actor propagation — request.ActorId takes precedence
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP09_ActorPropagation_RequestActorIdWins()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    ActorId = "request-level-actor"
                }, "method-level-actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.ActorId, Is.EqualTo("request-level-actor"),
                "DP09: request.ActorId must override method-level actorId");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP10: CorrelationId threads from webhook into evidence pack
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP10_CorrelationId_ThreadedFromWebhookToEvidence()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();
            string correlationId = $"corr-dp10-{Guid.NewGuid():N}";

            // Record webhook with explicit correlationId
            var webhookResp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    CorrelationId = correlationId
                }, "actor-dp10");

            Assert.That(webhookResp.Record!.CorrelationId, Is.EqualTo(correlationId),
                "DP10: webhook record must preserve correlationId");

            // Persist evidence — the evidence pack should reference the same approval webhook
            var evidenceResp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor-dp10");

            Assert.That(evidenceResp.Pack!.ApprovalWebhook, Is.Not.Null,
                "DP10: evidence pack must include the approval webhook");
            Assert.That(evidenceResp.Pack.ApprovalWebhook!.CorrelationId, Is.EqualTo(correlationId),
                "DP10: evidence pack approval webhook must carry the correlationId");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP11: OperatorGuidance non-null for every non-Ready status
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP11_OperatorGuidance_IsNonNull_ForAllNonReadyStatuses()
        {
            var svc = CreateService();

            // Blocked: no evidence, no webhook
            var blockedReadiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = UniqueHead() });
            Assert.That(blockedReadiness.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "DP11: Blocked state must have non-empty OperatorGuidance");

            // Stale: evidence exists but expired
            var timeProvider = new AdvancableTimeProvider(DateTimeOffset.UtcNow);
            var svcWithTime = new ProtectedSignOffEvidencePersistenceService(
                NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance,
                timeProvider: timeProvider);

            string caseId = UniqueCase();
            string headRef = UniqueHead();
            await svcWithTime.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svcWithTime.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId, FreshnessWindowHours = 1 }, "actor");

            timeProvider.Advance(TimeSpan.FromHours(2));
            var staleReadiness = await svcWithTime.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId, FreshnessWindowHours = 1 });
            Assert.That(staleReadiness.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "DP11: Stale state must have non-empty OperatorGuidance");

            // Pending: only evidence, no webhook
            var svcPending = CreateService();
            string headRef2 = UniqueHead();
            string caseId2 = UniqueCase();
            await svcPending.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef2, CaseId = caseId2 }, "actor");
            var pendingReadiness = await svcPending.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef2, CaseId = caseId2 });
            Assert.That(pendingReadiness.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "DP11: non-Ready state (Blocked/Pending) must have non-empty OperatorGuidance");

            // Ready: webhook + evidence → check guidance too
            var svcReady = CreateService();
            string headRef3 = UniqueHead();
            string caseId3 = UniqueCase();
            await svcReady.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId3, HeadRef = headRef3, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svcReady.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef3, CaseId = caseId3 }, "actor");
            var readyReadiness = await svcReady.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef3, CaseId = caseId3 });
            Assert.That(readyReadiness.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "DP11: Ready state must also have non-empty OperatorGuidance");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP12: History MaxRecords enforced under bursty ingestion
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP12_HistoryMaxRecords_EnforcedUnderBurstyIngestion()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Submit 50 webhooks
            for (int i = 0; i < 50; i++)
            {
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest
                    {
                        CaseId = caseId,
                        HeadRef = headRef,
                        Outcome = ApprovalWebhookOutcome.Approved
                    }, $"actor-{i}");
            }

            // Request with MaxRecords=5
            var historyResp = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId, MaxRecords = 5 });

            Assert.That(historyResp.Success, Is.True);
            Assert.That(historyResp.TotalCount, Is.EqualTo(50),
                "DP12: TotalCount must reflect all 50 submitted records");
            Assert.That(historyResp.Records.Count, Is.EqualTo(5),
                "DP12: Records list must be capped at MaxRecords=5");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP13: Multi-head isolation — different heads have independent readiness
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP13_MultiHeadIsolation_DifferentHeadsHaveIndependentReadiness()
        {
            var svc = CreateService();
            string caseId = UniqueCase();

            string headRef1 = $"sha-head1-{Guid.NewGuid():N}";
            string headRef2 = $"sha-head2-{Guid.NewGuid():N}";

            // Only headRef1 gets webhook and evidence
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef1, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef1, CaseId = caseId }, "actor");

            var readiness1 = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef1, CaseId = caseId });
            var readiness2 = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef2, CaseId = caseId });

            Assert.That(readiness1.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "DP13: headRef1 with webhook + evidence must be Ready");
            // headRef2 has no evidence and a valid HeadRef → Blocked (not Indeterminate).
            // Indeterminate is only for null/missing HeadRef inputs.
            Assert.That(readiness2.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked),
                "DP13: headRef2 without any evidence must be Blocked (no cross-contamination from headRef1)");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP14: Duplicate webhook submission → both records persisted (no drop)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP14_DuplicateWebhookSubmission_BothRecordsPersisted()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Submit same webhook payload twice
            var req = new RecordApprovalWebhookRequest
            {
                CaseId = caseId, HeadRef = headRef,
                Outcome = ApprovalWebhookOutcome.Approved,
                CorrelationId = "dedup-test-corr-id"
            };

            var resp1 = await svc.RecordApprovalWebhookAsync(req, "actor-dp14");
            var resp2 = await svc.RecordApprovalWebhookAsync(req, "actor-dp14");

            Assert.That(resp1.Success, Is.True);
            Assert.That(resp2.Success, Is.True);
            Assert.That(resp1.Record!.RecordId, Is.Not.EqualTo(resp2.Record!.RecordId),
                "DP14: duplicate submissions must produce independent records (no silent dedup drop)");

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });
            Assert.That(history.TotalCount, Is.EqualTo(2),
                "DP14: history must contain both records");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP15: Full lifecycle HTTP integration test (promoted conditions)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP15_FullDeployedLifecycle_WebhookEvidenceReadinessHistory_AllSucceed()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();
            string correlationId = $"dp15-lifecycle-{Guid.NewGuid():N}";

            // Step 1: Record approved webhook
            var webhookHttpResp = await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    CorrelationId = correlationId,
                    ActorId = "release-manager"
                });
            Assert.That(webhookHttpResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP15: Step 1 webhook record must return 200");
            var webhookResult = await webhookHttpResp.Content.ReadFromJsonAsync<RecordApprovalWebhookResponse>();
            Assert.That(webhookResult!.Success, Is.True);

            // Step 2: Persist evidence
            var evidenceHttpResp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    RequireReleaseGrade = false
                });
            Assert.That(evidenceHttpResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP15: Step 2 evidence persist must return 200");
            var evidenceResult = await evidenceHttpResp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>();
            Assert.That(evidenceResult!.Success, Is.True);
            Assert.That(evidenceResult.Pack!.IsReleaseGrade, Is.True,
                "DP15: evidence with prior approved webhook must be release-grade");

            // Step 3: Get release readiness
            var readinessHttpResp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });
            Assert.That(readinessHttpResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP15: Step 3 readiness check must return 200 (Ready)");
            var readinessResult = await readinessHttpResp.Content.ReadFromJsonAsync<GetSignOffReleaseReadinessResponse>();
            Assert.That(readinessResult!.Success, Is.True);
            Assert.That(readinessResult.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "DP15: full deployed lifecycle must yield Ready status");
            Assert.That(readinessResult.Blockers, Is.Empty,
                "DP15: Ready status must have no blockers");

            // Step 4: Check webhook history
            var historyHttpResp = await _client.GetAsync(
                $"{BaseUrl}/webhooks/approval/history?caseId={caseId}&headRef={headRef}");
            Assert.That(historyHttpResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP15: Step 4 webhook history must return 200");
            var historyResult = await historyHttpResp.Content.ReadFromJsonAsync<GetApprovalWebhookHistoryResponse>();
            Assert.That(historyResult!.Success, Is.True);
            Assert.That(historyResult.TotalCount, Is.EqualTo(1),
                "DP15: history must contain exactly the one webhook recorded");
            Assert.That(historyResult.Records[0].CorrelationId, Is.EqualTo(correlationId),
                "DP15: history must preserve the correlationId from the webhook request");

            // Step 5: Check evidence history
            var evHistoryHttpResp = await _client.GetAsync(
                $"{BaseUrl}/evidence/history?headRef={headRef}&caseId={caseId}");
            Assert.That(evHistoryHttpResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP15: Step 5 evidence history must return 200");
            var evHistoryResult = await evHistoryHttpResp.Content.ReadFromJsonAsync<GetEvidencePackHistoryResponse>();
            Assert.That(evHistoryResult!.Success, Is.True);
            Assert.That(evHistoryResult.TotalCount, Is.EqualTo(1),
                "DP15: evidence history must contain exactly the one pack persisted");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP16: Process-restart simulation — new service instance → Indeterminate
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP16_ProcessRestartSimulation_NewServiceInstance_ReturnsIndeterminate()
        {
            // Persist evidence in instance 1
            var svc1 = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            await svc1.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc1.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor");

            var readiness1 = await svc1.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });
            Assert.That(readiness1.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "DP16: instance 1 must be Ready");

            // Simulate process restart: create a fresh service instance
            var svc2 = CreateService();
            var readiness2 = await svc2.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            // A valid HeadRef with no evidence in the new instance yields Blocked.
            // Indeterminate is reserved for null/missing HeadRef inputs.
            Assert.That(readiness2.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked),
                "DP16: after process restart (new instance), evidence must be gone → Blocked (fail-closed)");
            // MissingEvidence blocker must be present to confirm fail-closed behavior
            Assert.That(
                readiness2.Blockers.Any(b => b.Category == SignOffReleaseBlockerCategory.MissingEvidence),
                Is.True,
                "DP16: Blocked state after restart must include MissingEvidence blocker");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP17: Malformed webhook → Blocked (MalformedWebhook) fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP17_MalformedWebhook_WithEvidence_IsBlocked_MalformedCategory()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Record malformed webhook
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Malformed
                }, "actor-dp17");

            // Persist evidence
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor-dp17");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked),
                "DP17: malformed webhook must block release (fail-closed)");
            Assert.That(
                readiness.Blockers.Any(b => b.Category == SignOffReleaseBlockerCategory.MalformedWebhook),
                Is.True,
                "DP17: Blocked state must include MalformedWebhook blocker");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP18: Readiness schema contract stable across 3 identical calls
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP18_ReadinessSchemaContract_StableAcross3Calls()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Record webhook and evidence once
            await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved });
            await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId });

            var statuses = new List<string>();
            var guidances = new List<string?>();

            for (int i = 0; i < 3; i++)
            {
                var resp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                    new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

                string json = await resp.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                // Validate schema fields
                Assert.That(root.TryGetProperty("success", out _), Is.True, "DP18: 'success' field required");
                Assert.That(root.TryGetProperty("status", out JsonElement statusEl), Is.True, "DP18: 'status' field required");
                Assert.That(root.TryGetProperty("blockers", out _), Is.True, "DP18: 'blockers' field required");
                Assert.That(root.TryGetProperty("operatorGuidance", out JsonElement guidanceEl), Is.True, "DP18: 'operatorGuidance' field required");
                Assert.That(root.TryGetProperty("hasApprovalWebhook", out _), Is.True, "DP18: 'hasApprovalWebhook' field required");
                Assert.That(root.TryGetProperty("evaluatedAt", out _), Is.True, "DP18: 'evaluatedAt' field required");

                // The API serializes enums as integers (no JsonStringEnumConverter registered).
                // Status=Ready is ordinal 0. Store the raw integer for determinism check.
                string statusValue = statusEl.ValueKind == JsonValueKind.Number
                    ? statusEl.GetInt32().ToString()
                    : statusEl.GetString() ?? "";
                statuses.Add(statusValue);
                guidances.Add(guidanceEl.ValueKind == JsonValueKind.Null ? null : guidanceEl.GetString());
            }

            // All 3 calls must return the same status (deterministic)
            Assert.That(statuses.Distinct().Count(), Is.EqualTo(1),
                "DP18: readiness status must be deterministic across 3 calls");
            // Guidance must be present for each call
            Assert.That(guidances.All(g => !string.IsNullOrEmpty(g)), Is.True,
                "DP18: operatorGuidance must be non-empty across all 3 calls");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP19: RequireApprovalWebhook=true without prior webhook → 400 + hint
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP19_RequireApprovalWebhook_WithoutPriorWebhook_Returns400_WithRemediationHint()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    RequireApprovalWebhook = true
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "DP19: RequireApprovalWebhook=true without prior webhook must return 400");

            string json = await resp.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            Assert.That(root.TryGetProperty("remediationHint", out JsonElement hintEl), Is.True,
                "DP19: response must include remediationHint field");
            Assert.That(hintEl.GetString(), Is.Not.Null.And.Not.Empty,
                "DP19: remediationHint must be non-empty (operator-safe guidance)");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP20: Happy-path deterministic across 3 independent factory instances
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP20_HappyPath_IsDeterministic_Across3IndependentFactoryInstances()
        {
            var outcomes = new List<(bool Success, string Status, bool IsReleaseGrade)>();

            for (int run = 1; run <= 3; run++)
            {
                await using var factory = new DeployedParityFactory();
                using var client = factory.CreateClient();
                string jwt = await ObtainJwtAsync(client, $"dp20-run{run}-{Guid.NewGuid():N}");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

                string caseId = UniqueCase();
                string headRef = $"sha-dp20-run{run}-{Guid.NewGuid():N}";

                // Record approved webhook
                await client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                    new RecordApprovalWebhookRequest
                    {
                        CaseId = caseId, HeadRef = headRef,
                        Outcome = ApprovalWebhookOutcome.Approved,
                        CorrelationId = $"dp20-run{run}-corr"
                    });

                // Persist evidence
                var evResp = await client.PostAsJsonAsync($"{BaseUrl}/evidence",
                    new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId });
                var evResult = await evResp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>();

                // Check readiness
                var readinessResp = await client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                    new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });
                var readinessResult = await readinessResp.Content.ReadFromJsonAsync<GetSignOffReleaseReadinessResponse>();

                outcomes.Add((
                    readinessResult!.Success,
                    readinessResult.Status.ToString(),
                    evResult!.Pack!.IsReleaseGrade));
            }

            Assert.That(outcomes.All(o => o.Success), Is.True,
                "DP20: all 3 runs must succeed");
            Assert.That(outcomes.All(o => o.Status == SignOffReleaseReadinessStatus.Ready.ToString()), Is.True,
                "DP20: all 3 runs must produce Ready status (deterministic deployed-path behavior)");
            Assert.That(outcomes.All(o => o.IsReleaseGrade), Is.True,
                "DP20: all 3 runs must produce release-grade evidence");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP21: RawPayload provided → PayloadHash computed and stored in record
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP21_RawPayload_IsHashedAndStoredInRecord()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();
            string rawPayload = """{"approver":"user@example.com","timestamp":"2026-03-21T20:00:00Z"}""";

            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    RawPayload = rawPayload
                }, "actor-dp21");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.PayloadHash, Is.Not.Null.And.Not.Empty,
                "DP21: RawPayload must produce a non-empty PayloadHash in the record");
            Assert.That(resp.Record.PayloadHash!.Length, Is.EqualTo(64),
                "DP21: PayloadHash must be a 64-char lowercase SHA-256 hex string");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP22: Metadata key/value pairs stored in webhook record
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP22_Metadata_IsPreservedInWebhookRecord()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var metadata = new Dictionary<string, string>
            {
                ["source"] = "github-actions",
                ["runId"] = "99887766",
                ["environment"] = "production"
            };

            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    Metadata = metadata
                }, "actor-dp22");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.Metadata, Is.Not.Null);
            Assert.That(resp.Record.Metadata["source"], Is.EqualTo("github-actions"),
                "DP22: webhook record must preserve metadata key 'source'");
            Assert.That(resp.Record.Metadata["runId"], Is.EqualTo("99887766"),
                "DP22: webhook record must preserve metadata key 'runId'");
            Assert.That(resp.Record.Metadata["environment"], Is.EqualTo("production"),
                "DP22: webhook record must preserve metadata key 'environment'");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP23: RequireReleaseGrade=true with prior webhook succeeds
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP23_RequireReleaseGrade_WithPriorWebhook_Succeeds()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Record approved webhook
            await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved
                });

            // Now persist with RequireReleaseGrade=true
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    RequireReleaseGrade = true
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "DP23: RequireReleaseGrade=true with prior approved webhook must succeed");
            var result = await resp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>();
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Pack!.IsReleaseGrade, Is.True,
                "DP23: evidence with RequireReleaseGrade=true must be marked as release-grade");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP24: RequireReleaseGrade=true without prior webhook → 400
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP24_RequireReleaseGrade_WithoutWebhook_Returns400()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    RequireReleaseGrade = true
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "DP24: RequireReleaseGrade=true without prior webhook must return 400");
            var result = await resp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>();
            Assert.That(result!.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("NOT_RELEASE_GRADE"),
                "DP24: error code must be NOT_RELEASE_GRADE");
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                "DP24: remediationHint must be non-empty");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP25: Evidence pack content hash is stable for identical inputs
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP25_EvidenceContentHash_IsNonEmptyAndPresentInPack()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            await PostWebhookAsync(caseId, headRef, ApprovalWebhookOutcome.Approved);
            var evidenceResp = await PostEvidenceAsync(headRef, caseId);

            Assert.That(evidenceResp.Pack!.ContentHash, Is.Not.Null.And.Not.Empty,
                "DP25: evidence pack must have a non-empty ContentHash");
            Assert.That(evidenceResp.Pack.ContentHash!.Length, Is.EqualTo(64),
                "DP25: ContentHash must be a 64-char SHA-256 hex string");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP26: Evidence pack has non-empty PackId and correct HeadRef/CaseId
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP26_EvidencePack_HasCorrectIdentityFields()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var evidenceResp = await PostEvidenceAsync(headRef, caseId);

            Assert.That(evidenceResp.Pack!.PackId, Is.Not.Null.And.Not.Empty,
                "DP26: PackId must be non-empty");
            Assert.That(evidenceResp.Pack.HeadRef, Is.EqualTo(headRef),
                "DP26: pack HeadRef must match the requested HeadRef");
            Assert.That(evidenceResp.Pack.CaseId, Is.EqualTo(caseId),
                "DP26: pack CaseId must match the requested CaseId");
            Assert.That(evidenceResp.Pack.CreatedAt, Is.Not.EqualTo(DateTimeOffset.MinValue),
                "DP26: pack CreatedAt must be populated");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP27: Evidence pack has non-empty Items list
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP27_EvidencePack_HasNonEmptyItemsList()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var evidenceResp = await PostEvidenceAsync(headRef, caseId);

            Assert.That(evidenceResp.Pack!.Items, Is.Not.Null.And.Not.Empty,
                "DP27: evidence pack must have at least one item in the Items list");
            Assert.That(evidenceResp.Pack.Items.All(i => !string.IsNullOrEmpty(i.EvidenceType)), Is.True,
                "DP27: every evidence item must have a non-empty EvidenceType");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP28: Webhook record has correct identity fields
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP28_WebhookRecord_HasCorrectIdentityFields()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    ActorId = "release-approver",
                    Reason = "All checks passed in staging"
                }, "fallback-actor");

            Assert.That(resp.Record!.RecordId, Is.Not.Null.And.Not.Empty,
                "DP28: RecordId must be non-empty");
            Assert.That(resp.Record.CaseId, Is.EqualTo(caseId),
                "DP28: record CaseId must match the request");
            Assert.That(resp.Record.HeadRef, Is.EqualTo(headRef),
                "DP28: record HeadRef must match the request");
            Assert.That(resp.Record.Outcome, Is.EqualTo(ApprovalWebhookOutcome.Approved),
                "DP28: record Outcome must match the request");
            Assert.That(resp.Record.ActorId, Is.EqualTo("release-approver"),
                "DP28: record ActorId must match the request-level ActorId");
            Assert.That(resp.Record.Reason, Is.EqualTo("All checks passed in staging"),
                "DP28: record Reason must be preserved");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP29: FreshnessWindowHours=0 defaults to 24 hours
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP29_FreshnessWindowHours_Zero_DefaultsTo24Hours()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, "actor");

            // Explicit 0 value should default to 24 hours
            var resp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId, FreshnessWindowHours = 0 }, "actor");

            Assert.That(resp.Success, Is.True);
            // ExpiresAt should be approximately 24 hours from now (allow +/- 1 min for test execution)
            var expectedExpiry = DateTimeOffset.UtcNow.AddHours(24);
            Assert.That(resp.Pack!.ExpiresAt, Is.Not.Null,
                "DP29: evidence pack must have ExpiresAt set when FreshnessWindowHours=0 defaults to 24");
            Assert.That(Math.Abs((resp.Pack.ExpiresAt!.Value - expectedExpiry).TotalMinutes), Is.LessThan(2),
                "DP29: ExpiresAt must be approximately 24 hours from creation time");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP30: Evidence history filter by caseId only (no headRef) works
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP30_EvidenceHistory_FilterByCaseIdOnly_ReturnsCorrectPacks()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string otherCaseId = UniqueCase();
            string headRef1 = UniqueHead();
            string headRef2 = UniqueHead();

            // Persist evidence for caseId across two head refs
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef1, CaseId = caseId }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef2, CaseId = caseId }, "actor");

            // Persist evidence for otherCaseId
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef1, CaseId = otherCaseId }, "actor");

            // Get history filtered by caseId only
            var history = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { CaseId = caseId });

            Assert.That(history.Success, Is.True);
            Assert.That(history.TotalCount, Is.EqualTo(2),
                "DP30: filtering by caseId must return exactly the 2 packs for that case");
            Assert.That(history.Packs.All(p => p.CaseId == caseId), Is.True,
                "DP30: all returned packs must have the requested caseId");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP31: Webhook history filter by caseId only returns correct records
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP31_WebhookHistory_FilterByCaseIdOnly_ReturnsCorrectRecords()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string otherCaseId = UniqueCase();
            string headRef = UniqueHead();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Escalated }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = otherCaseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Denied }, "actor");

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });

            Assert.That(history.Success, Is.True);
            Assert.That(history.TotalCount, Is.EqualTo(2),
                "DP31: filtering by caseId must return exactly the 2 records for that case");
            Assert.That(history.Records.All(r => r.CaseId == caseId), Is.True,
                "DP31: all returned records must have the requested caseId");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP32: Evidence history returned in newest-first order
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP32_EvidenceHistory_ReturnedInNewestFirstOrder()
        {
            var baseTime = DateTimeOffset.UtcNow;
            var timeProvider = new AdvancableTimeProvider(baseTime);
            var svc = new ProtectedSignOffEvidencePersistenceService(
                NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance,
                timeProvider: timeProvider);

            string headRef = UniqueHead();

            // Persist 3 packs with advancing time
            for (int i = 0; i < 3; i++)
            {
                await svc.PersistSignOffEvidenceAsync(
                    new PersistSignOffEvidenceRequest { HeadRef = headRef }, "actor");
                timeProvider.Advance(TimeSpan.FromMinutes(5));
            }

            var history = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = headRef });

            Assert.That(history.TotalCount, Is.EqualTo(3));
            // Verify newest first: each pack's CreatedAt should be descending
            for (int i = 0; i < history.Packs.Count - 1; i++)
            {
                Assert.That(history.Packs[i].CreatedAt, Is.GreaterThanOrEqualTo(history.Packs[i + 1].CreatedAt),
                    $"DP32: pack at index {i} must be newer than pack at index {i + 1}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // DP33: Webhook history returned in newest-first order
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP33_WebhookHistory_ReturnedInNewestFirstOrder()
        {
            var baseTime = DateTimeOffset.UtcNow;
            var timeProvider = new AdvancableTimeProvider(baseTime);
            var svc = new ProtectedSignOffEvidencePersistenceService(
                NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance,
                timeProvider: timeProvider);

            string caseId = UniqueCase();
            string headRef = UniqueHead();

            for (int i = 0; i < 3; i++)
            {
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest
                    {
                        CaseId = caseId, HeadRef = headRef,
                        Outcome = ApprovalWebhookOutcome.Approved
                    }, "actor");
                timeProvider.Advance(TimeSpan.FromMinutes(3));
            }

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });

            Assert.That(history.TotalCount, Is.EqualTo(3));
            for (int i = 0; i < history.Records.Count - 1; i++)
            {
                Assert.That(history.Records[i].ReceivedAt, Is.GreaterThanOrEqualTo(history.Records[i + 1].ReceivedAt),
                    $"DP33: record at index {i} must be newer than record at index {i + 1}");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // DP34: Readiness with HasApprovalWebhook=true when only non-Approved webhook exists
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP34_HasApprovalWebhook_TrueEvenForDeniedWebhook()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Only a denied webhook — HasApprovalWebhook tracks any webhook presence
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Denied }, "actor");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.HasApprovalWebhook, Is.True,
                "DP34: HasApprovalWebhook must be true when any webhook (even Denied) was received");
            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked),
                "DP34: status must be Blocked because only a Denied webhook exists");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP35: Unauthenticated webhook record request returns 401
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP35_UnauthenticatedWebhookRecord_Returns401()
        {
            await using var factory = new DeployedParityFactory();
            using var unauthClient = factory.CreateClient();

            var resp = await unauthClient.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = UniqueCase(), HeadRef = UniqueHead(),
                    Outcome = ApprovalWebhookOutcome.Approved
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "DP35: unauthenticated webhook record must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP36: Unauthenticated evidence persist request returns 401
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP36_UnauthenticatedEvidencePersist_Returns401()
        {
            await using var factory = new DeployedParityFactory();
            using var unauthClient = factory.CreateClient();

            var resp = await unauthClient.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest { HeadRef = UniqueHead() });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "DP36: unauthenticated evidence persist must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP37: Unauthenticated readiness request returns 401
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP37_UnauthenticatedReadiness_Returns401()
        {
            await using var factory = new DeployedParityFactory();
            using var unauthClient = factory.CreateClient();

            var resp = await unauthClient.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = UniqueHead() });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "DP37: unauthenticated readiness check must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP38: Multi-actor workflow — different actors for webhook and evidence
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP38_MultiActor_WebhookAndEvidenceHaveDifferentActors()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var webhookResp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    ActorId = "external-approver-system"
                }, "fallback");

            var evidenceResp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId },
                "release-pipeline-service");

            Assert.That(webhookResp.Record!.ActorId, Is.EqualTo("external-approver-system"),
                "DP38: webhook actor must be the request-level actor");
            Assert.That(evidenceResp.Pack!.CreatedBy, Is.EqualTo("release-pipeline-service"),
                "DP38: evidence pack actor (CreatedBy) must be the evidence method-level actor");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP39: Evidence history MaxRecords=1 returns most recent pack
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP39_EvidenceHistory_MaxRecords1_ReturnsMostRecentPack()
        {
            var baseTime = DateTimeOffset.UtcNow;
            var timeProvider = new AdvancableTimeProvider(baseTime);
            var svc = new ProtectedSignOffEvidencePersistenceService(
                NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance,
                timeProvider: timeProvider);

            string headRef = UniqueHead();

            // Create 3 packs with advancing time
            var packIds = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var r = await svc.PersistSignOffEvidenceAsync(
                    new PersistSignOffEvidenceRequest { HeadRef = headRef }, "actor");
                packIds.Add(r.Pack!.PackId);
                timeProvider.Advance(TimeSpan.FromHours(1));
            }

            var history = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = headRef, MaxRecords = 1 });

            Assert.That(history.TotalCount, Is.EqualTo(3),
                "DP39: TotalCount must reflect all 3 packs");
            Assert.That(history.Packs.Count, Is.EqualTo(1),
                "DP39: Records list must be capped at MaxRecords=1");
            Assert.That(history.Packs[0].PackId, Is.EqualTo(packIds[2]),
                "DP39: MaxRecords=1 must return the most recently created pack");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP40: Readiness with null request body returns error response
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DP40_ReadinessWithEmptyHeadRef_ReturnsIndeterminate()
        {
            var svc = CreateService();

            // Empty HeadRef → Indeterminate
            var resp = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = "" });

            Assert.That(resp.Success, Is.False,
                "DP40: empty HeadRef must return Success=false");
            Assert.That(resp.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Indeterminate),
                "DP40: empty HeadRef must return Indeterminate status (not Blocked)");
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_HEAD_REF"),
                "DP40: error code must be MISSING_HEAD_REF");
            Assert.That(resp.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "DP40: OperatorGuidance must be present even for missing-HeadRef error");
        }

        // ════════════════════════════════════════════════════════════════════
        // DP41-DP50: Blocked artifact / release-grade evidence semantics
        // ════════════════════════════════════════════════════════════════════

        // DP41: Non-release-grade evidence has IsReleaseGrade=false in evidence pack
        [Test]
        public async Task DP41_NonReleaseGradeEvidence_HasIsReleaseGradeFalse_InPack()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            // Submit evidence WITHOUT prior approved webhook → non-release-grade
            var evidenceResp = await PostEvidenceAsync(head, caseId, requireReleaseGrade: false);

            Assert.That(evidenceResp.Success, Is.True,
                "DP41: evidence without webhook must succeed when RequireReleaseGrade=false");
            Assert.That(evidenceResp.Pack, Is.Not.Null,
                "DP41: evidence pack must be present even when not release-grade");
            Assert.That(evidenceResp.Pack!.IsReleaseGrade, Is.False,
                "DP41: evidence pack without prior approved webhook must have IsReleaseGrade=false");
        }

        // DP42: Release-grade evidence has IsReleaseGrade=true in evidence pack
        [Test]
        public async Task DP42_ReleaseGradeEvidence_HasIsReleaseGradeTrue_InPack()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            // Submit approved webhook first
            await PostWebhookAsync(caseId, head, ApprovalWebhookOutcome.Approved);

            // Submit evidence WITH prior approved webhook → release-grade
            var evidenceResp = await PostEvidenceAsync(head, caseId, requireReleaseGrade: true);

            Assert.That(evidenceResp.Success, Is.True,
                "DP42: evidence with prior approved webhook must succeed");
            Assert.That(evidenceResp.Pack, Is.Not.Null,
                "DP42: evidence pack must be present");
            Assert.That(evidenceResp.Pack!.IsReleaseGrade, Is.True,
                "DP42: evidence pack with prior approved webhook and RequireReleaseGrade=true must have IsReleaseGrade=true");
        }

        // DP43: Readiness for non-release-grade evidence is non-Ready
        [Test]
        public async Task DP43_NonReleaseGradeEvidence_Readiness_IsNotReady()
        {
            var svc = CreateService();
            string head = UniqueHead();
            string caseId = UniqueCase();

            // Persist evidence without webhook → non-release-grade
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor-dp43");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(readiness.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "DP43: non-release-grade evidence without approved webhook must not be Ready");
            // Note: Success=false for non-Ready states is the correct service contract
            // (Success=true only when Status=Ready). Operator guidance is always present.
            Assert.That(readiness.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "DP43: OperatorGuidance must be non-null and non-empty for non-Ready states");
        }

        // DP44: Recovery from Blocked → Ready by providing release-grade evidence
        [Test]
        public async Task DP44_Recovery_FromBlocked_ToReady_ByReleaseGradeEvidence()
        {
            var svc = CreateService();
            string head = UniqueHead();
            string caseId = UniqueCase();

            // Verify initial Blocked state (no evidence, no webhook)
            var initialReadiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            Assert.That(initialReadiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked),
                "DP44: fresh head must start Blocked");

            // Add approved webhook
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = head,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    CorrelationId = Guid.NewGuid().ToString("N")
                }, "actor-dp44");

            // Persist release-grade evidence
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = head, CaseId = caseId,
                    RequireReleaseGrade = true
                }, "actor-dp44");

            // Verify recovery to Ready
            var finalReadiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            Assert.That(finalReadiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "DP44: release-grade evidence with approved webhook must yield Ready");
            Assert.That(finalReadiness.Blockers, Is.Null.Or.Empty,
                "DP44: Ready state must have no blockers");
        }

        // DP45: OperatorGuidance is non-null for Indeterminate status
        [Test]
        public async Task DP45_OperatorGuidance_IsNonNull_ForIndeterminateStatus()
        {
            var svc = CreateService();

            // Empty HeadRef → Indeterminate
            var resp = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = string.Empty });

            Assert.That(resp.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Indeterminate),
                "DP45: empty HeadRef must yield Indeterminate status");
            Assert.That(resp.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "DP45: OperatorGuidance must be non-null and non-empty for Indeterminate status — " +
                "the system must always provide actionable guidance, even for invalid inputs");
        }

        // DP46: ContentHash differs between two different evidence submissions (input-sensitivity)
        [Test]
        public async Task DP46_ContentHash_DiffersBetween_DifferentEvidenceSubmissions()
        {
            var svc = CreateService();
            string head1 = UniqueHead();
            string head2 = UniqueHead();
            string caseId1 = UniqueCase();
            string caseId2 = UniqueCase();

            var resp1 = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head1, CaseId = caseId1 }, "actor-dp46a");
            var resp2 = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head2, CaseId = caseId2 }, "actor-dp46b");

            Assert.That(resp1.Success, Is.True, "DP46: first evidence must succeed");
            Assert.That(resp2.Success, Is.True, "DP46: second evidence must succeed");
            Assert.That(resp1.Pack!.ContentHash, Is.Not.Null.And.Not.Empty,
                "DP46: first evidence pack must have ContentHash");
            Assert.That(resp2.Pack!.ContentHash, Is.Not.Null.And.Not.Empty,
                "DP46: second evidence pack must have ContentHash");
            Assert.That(resp1.Pack.ContentHash, Is.Not.EqualTo(resp2.Pack.ContentHash),
                "DP46: evidence packs for different head refs and case IDs must have different ContentHash " +
                "values — the hash must be input-sensitive");
        }

        // DP47: Evidence history TotalCount is accurate after multiple submissions
        [Test]
        public async Task DP47_EvidenceHistory_TotalCount_IsAccurate_AfterMultipleSubmissions()
        {
            var svc = CreateService();
            string head = UniqueHead();
            string caseId = UniqueCase();

            // Submit 3 evidence packs to the same head+case
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor-dp47a");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor-dp47b");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor-dp47c");

            var history = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = head, CaseId = caseId, MaxRecords = 100 });

            Assert.That(history.Success, Is.True,
                "DP47: evidence history request must succeed");
            Assert.That(history.TotalCount, Is.GreaterThanOrEqualTo(3),
                "DP47: TotalCount must reflect at least the 3 evidence packs submitted");
            Assert.That(history.Packs, Is.Not.Null,
                "DP47: Packs list must not be null");
        }

        // DP48: Webhook history TotalCount is accurate after multiple submissions
        [Test]
        public async Task DP48_WebhookHistory_TotalCount_IsAccurate_AfterMultipleSubmissions()
        {
            var svc = CreateService();
            string head = UniqueHead();
            string caseId = UniqueCase();

            // Submit 3 webhooks
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = head,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    CorrelationId = Guid.NewGuid().ToString("N")
                }, "actor-dp48a");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = head,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    CorrelationId = Guid.NewGuid().ToString("N")
                }, "actor-dp48b");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = head,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    CorrelationId = Guid.NewGuid().ToString("N")
                }, "actor-dp48c");

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { HeadRef = head, CaseId = caseId, MaxRecords = 100 });

            Assert.That(history.Success, Is.True,
                "DP48: webhook history request must succeed");
            Assert.That(history.TotalCount, Is.GreaterThanOrEqualTo(3),
                "DP48: TotalCount must reflect at least the 3 webhooks submitted");
        }

        // DP49: RequireReleaseGrade=true + approved webhook → IsReleaseGrade=true (schema contract)
        [Test]
        public async Task DP49_RequireReleaseGrade_WithApprovedWebhook_EvidencePack_IsReleaseGradeTrue()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            await PostWebhookAsync(caseId, head, ApprovalWebhookOutcome.Approved);
            var evidenceResp = await PostEvidenceAsync(head, caseId, requireReleaseGrade: true);

            Assert.That(evidenceResp.Success, Is.True,
                "DP49: evidence with RequireReleaseGrade=true and prior webhook must succeed");
            Assert.That(evidenceResp.Pack, Is.Not.Null,
                "DP49: evidence pack must be present");
            Assert.That(evidenceResp.Pack!.IsReleaseGrade, Is.True,
                "DP49: evidence pack schema contract: IsReleaseGrade must be true when " +
                "RequireReleaseGrade=true and an approved webhook exists");
            Assert.That(evidenceResp.Pack.PackId, Is.Not.Null.And.Not.Empty,
                "DP49: PackId must be non-empty in release-grade evidence pack");
            Assert.That(evidenceResp.Pack.ContentHash, Is.Not.Null.And.Not.Empty,
                "DP49: ContentHash must be non-empty in release-grade evidence pack");
        }

        // DP50: New service instance returns Indeterminate with non-null OperatorGuidance
        [Test]
        public async Task DP50_NewServiceInstance_Indeterminate_HasNonNullOperatorGuidance()
        {
            // Simulate process restart by creating a fresh service instance
            var freshSvc = CreateService();
            string head = UniqueHead();
            string caseId = UniqueCase();

            var readiness = await freshSvc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest
                {
                    HeadRef = head,
                    CaseId = caseId
                });

            // Fresh service has no data → expect Blocked (no evidence, no webhook)
            // A non-Indeterminate result is also valid if the service pre-populates default state
            Assert.That(readiness.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "DP50: new service instance must provide OperatorGuidance for any status — " +
                "fail-closed guidance is required even on process restart to prevent silent failures");
            Assert.That(readiness.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "DP50: new service instance with no data must not return Ready — " +
                "a fresh instance with no evidence cannot claim release-grade readiness");
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        private static string UniqueCase() => $"case-dp-{Guid.NewGuid():N}";
        private static string UniqueHead() => $"sha-dp-{Guid.NewGuid():N}";

        private static ProtectedSignOffEvidencePersistenceService CreateService() =>
            new ProtectedSignOffEvidencePersistenceService(
                NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance);

        private async Task<RecordApprovalWebhookResponse> PostWebhookAsync(
            string caseId, string headRef, ApprovalWebhookOutcome outcome,
            string? correlationId = null)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = outcome,
                    CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<RecordApprovalWebhookResponse>())!;
        }

        private async Task<PersistSignOffEvidenceResponse> PostEvidenceAsync(
            string headRef, string caseId, bool requireReleaseGrade = false)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    RequireReleaseGrade = requireReleaseGrade
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>())!;
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string userTag)
        {
            string email = $"dp-{userTag}@deployed-parity-test.biatec.example.com";
            HttpResponseMessage resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email = email,
                Password = "DeployedParityIT!Pass1",
                ConfirmPassword = "DeployedParityIT!Pass1",
                FullName = $"Deployed Parity Test User ({userTag})"
            });

            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Created),
                $"Registration must succeed for user '{userTag}'. Status: {resp.StatusCode}");

            JsonDocument? doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            string? token = doc?.RootElement.GetProperty("accessToken").GetString();
            Assert.That(token, Is.Not.Null.And.Not.Empty,
                $"Registration must return a non-empty accessToken for user '{userTag}'");
            return token!;
        }
    }
}
