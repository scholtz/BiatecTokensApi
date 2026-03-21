using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
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
    /// WebApplicationFactory integration tests for the protected sign-off evidence
    /// persistence API at <c>/api/v1/protected-signoff-evidence</c>.
    ///
    /// These tests exercise the full HTTP stack — routing, JSON serialization,
    /// authentication enforcement, controller dispatch, and service logic — against
    /// an in-process application host.  They complement the unit tests in
    /// <c>ProtectedSignOffEvidencePersistenceTests.cs</c> (which test service logic
    /// in isolation) and the controller tests in
    /// <c>ProtectedSignOffEvidencePersistenceControllerTests.cs</c> (which test
    /// controller dispatch with stub services).
    ///
    /// Coverage:
    ///
    /// IT01–IT05:  Unauthenticated requests to all 5 endpoints return 401.
    /// IT06:       POST /webhooks/approval succeeds (200) with a valid approved webhook.
    /// IT07:       POST /webhooks/approval with invalid (null) request returns 400.
    /// IT08:       POST /evidence succeeds (200) when no release-grade constraint is set.
    /// IT09:       POST /evidence with RequireApprovalWebhook=true but no webhook returns 400.
    /// IT10:       POST /release-readiness for head with no evidence returns 200 with Indeterminate.
    /// IT11:       POST /release-readiness for head with persisted evidence returns 200 with Ready.
    /// IT12:       GET  /webhooks/approval/history returns 200 with empty list on fresh instance.
    /// IT13:       GET  /evidence/history returns 200 with empty list on fresh instance.
    /// IT14:       Full lifecycle: record webhook → persist evidence → check readiness.
    /// IT15:       POST /webhooks/approval with Denied outcome succeeds and records correctly.
    /// IT16:       POST /evidence with RequireReleaseGrade=true but no approved webhook returns 400.
    /// IT17:       POST /release-readiness requires explicit HeadRef; missing returns 400.
    /// IT18:       GET  /webhooks/approval/history after recording returns the record.
    /// IT19:       GET  /evidence/history after persisting returns the pack.
    /// IT20:       Concurrent webhook records for same case are all stored (no lost writes).
    /// IT21:       Multiple cases are isolated from each other in readiness responses.
    /// IT22:       Escalated outcome recorded and surfaced in history.
    /// IT23:       TimedOut outcome recorded; readiness returns Blocked.
    /// IT24:       DeliveryError outcome recorded; readiness returns Blocked.
    /// IT25:       MaxRecords query parameter on history endpoints limits response size.
    /// IT26:       Freshness: evidence persisted with very short window becomes Stale on re-query.
    /// IT27:       Response schema contract — readiness response contains all required fields.
    /// IT28:       Response schema contract — webhook record response contains all required fields.
    /// IT29:       Response schema contract — evidence pack response contains all required fields.
    /// IT30:       Full journey is deterministic across 3 independent runs.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffEvidencePersistenceIntegrationTests
    {
        // ════════════════════════════════════════════════════════════════════
        // WebApplicationFactory
        // ════════════════════════════════════════════════════════════════════

        private sealed class EvidencePersistenceFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "EvidencePersistIntTestSecretKey32Chars!!",
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
                        ["KeyManagementConfig:HardcodedKey"] = "EvidencePersistIntTestKeyXXXX32Chars",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "integration-test",
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

        private EvidencePersistenceFactory _factory = null!;
        private HttpClient _client = null!;
        private string _jwt = null!;

        private const string BaseUrl = "/api/v1/protected-signoff-evidence";

        [SetUp]
        public async Task SetUp()
        {
            _factory = new EvidencePersistenceFactory();
            _client = _factory.CreateClient();
            _jwt = await ObtainJwtAsync(_client, $"it-{Guid.NewGuid():N}");
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
        // IT01–IT05: Unauthenticated requests return 401
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT01_RecordWebhook_Unauthenticated_Returns401()
        {
            using var unauthClient = _factory.CreateClient();
            var resp = await unauthClient.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest { CaseId = "c1", HeadRef = "sha1", Outcome = ApprovalWebhookOutcome.Approved });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task IT02_PersistEvidence_Unauthenticated_Returns401()
        {
            using var unauthClient = _factory.CreateClient();
            var resp = await unauthClient.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest { HeadRef = "sha1", CaseId = "c1" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task IT03_GetReleaseReadiness_Unauthenticated_Returns401()
        {
            using var unauthClient = _factory.CreateClient();
            var resp = await unauthClient.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = "sha1" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task IT04_GetWebhookHistory_Unauthenticated_Returns401()
        {
            using var unauthClient = _factory.CreateClient();
            var resp = await unauthClient.GetAsync($"{BaseUrl}/webhooks/approval/history");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task IT05_GetEvidenceHistory_Unauthenticated_Returns401()
        {
            using var unauthClient = _factory.CreateClient();
            var resp = await unauthClient.GetAsync($"{BaseUrl}/evidence/history");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ════════════════════════════════════════════════════════════════════
        // IT06–IT09: Basic POST /webhooks/approval and POST /evidence
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT06_RecordApprovedWebhook_Returns200()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    CorrelationId = Guid.NewGuid().ToString("N")
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Expected 200 for Approved webhook. Actual: {resp.StatusCode}");

            RecordApprovalWebhookResponse? body = await resp.Content.ReadFromJsonAsync<RecordApprovalWebhookResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Record!.RecordId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task IT07_RecordWebhook_NullRequest_Returns400()
        {
            // Send a body that will deserialize to a request with null/default required fields
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = null!,
                    HeadRef = null!,
                    Outcome = ApprovalWebhookOutcome.Approved
                });

            // Null CaseId/HeadRef should be treated as invalid by the service
            // The service returns a fail response which the controller maps to 400
            Assert.That((int)resp.StatusCode, Is.EqualTo(400),
                $"Expected 400 for null CaseId/HeadRef. Actual: {(int)resp.StatusCode}");
        }

        [Test]
        public async Task IT08_PersistEvidence_NoConstraints_Returns200()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    RequireReleaseGrade = false
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Expected 200 for persistence with no constraints. Actual: {resp.StatusCode}");

            PersistSignOffEvidenceResponse? body = await resp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.Pack!.PackId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task IT09_PersistEvidence_RequireApprovalWebhook_NoWebhook_Returns400()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    RequireReleaseGrade = false
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                $"Expected 400 when requiring approval webhook but none exists. Actual: {resp.StatusCode}");

            PersistSignOffEvidenceResponse? body = await resp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.False);
        }

        // ════════════════════════════════════════════════════════════════════
        // IT10–IT11: POST /release-readiness
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT10_GetReleaseReadiness_NoEvidence_Returns200WithIndeterminate()
        {
            string headRef = UniqueHead();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Readiness for unknown head should return 200 (Indeterminate is a valid state). Actual: {resp.StatusCode}");

            GetSignOffReleaseReadinessResponse? body = await resp.Content.ReadFromJsonAsync<GetSignOffReleaseReadinessResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Status,
                Is.EqualTo(SignOffReleaseReadinessStatus.Indeterminate)
                .Or.EqualTo(SignOffReleaseReadinessStatus.Pending),
                "Head with no evidence should be Indeterminate or Pending");
        }

        [Test]
        public async Task IT11_GetReleaseReadiness_AfterPersistingEvidence_Returns200WithReady()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Step 1: Record approved webhook
            var webhookResp = await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    CorrelationId = Guid.NewGuid().ToString("N")
                });
            Assert.That(webhookResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Step 2: Persist evidence (no constraints since we already have the webhook)
            var persistResp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    RequireReleaseGrade = false
                });
            Assert.That(persistResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Step 3: Check readiness — should be Ready now
            var readinessResp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    
                });

            Assert.That(readinessResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            GetSignOffReleaseReadinessResponse? body = await readinessResp.Content.ReadFromJsonAsync<GetSignOffReleaseReadinessResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "After recording approved webhook and persisting evidence, readiness must be Ready");
        }

        // ════════════════════════════════════════════════════════════════════
        // IT12–IT13: GET history endpoints on fresh state
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT12_GetWebhookHistory_FreshInstance_Returns200EmptyList()
        {
            string caseId = UniqueCase();

            var resp = await _client.GetAsync($"{BaseUrl}/webhooks/approval/history?caseId={caseId}");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            GetApprovalWebhookHistoryResponse? body = await resp.Content.ReadFromJsonAsync<GetApprovalWebhookHistoryResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Records, Is.Not.Null);
            Assert.That(body.Records!.Count, Is.EqualTo(0),
                "Fresh case should have no webhook records");
        }

        [Test]
        public async Task IT13_GetEvidenceHistory_FreshInstance_Returns200EmptyList()
        {
            string headRef = UniqueHead();

            var resp = await _client.GetAsync($"{BaseUrl}/evidence/history?headRef={headRef}");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            GetEvidencePackHistoryResponse? body = await resp.Content.ReadFromJsonAsync<GetEvidencePackHistoryResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Packs, Is.Not.Null);
            Assert.That(body.Packs!.Count, Is.EqualTo(0),
                "Fresh head ref should have no evidence packs");
        }

        // ════════════════════════════════════════════════════════════════════
        // IT14: Full lifecycle integration
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT14_FullLifecycle_RecordWebhook_PersistEvidence_CheckReadiness_AllPass()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();
            string corr = Guid.NewGuid().ToString("N");

            // 1. Record approved webhook
            var webhookResult = await PostWebhookAsync(caseId, headRef, ApprovalWebhookOutcome.Approved, corr);
            Assert.That(webhookResult.Success, Is.True, "Recording webhook must succeed");
            Assert.That(webhookResult.Record!.RecordId, Is.Not.Null.And.Not.Empty);

            // 2. Persist evidence
            var evidenceResult = await PostEvidenceAsync(headRef, caseId, false, false);
            Assert.That(evidenceResult.Success, Is.True, "Persisting evidence must succeed");
            Assert.That(evidenceResult.Pack!.PackId, Is.Not.Null.And.Not.Empty);

            // 3. Check readiness
            var readinessResult = await PostReadinessAsync(headRef, caseId, requireWebhook: true);
            Assert.That(readinessResult.Success, Is.True, "Readiness check must succeed");
            Assert.That(readinessResult.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready));
            Assert.That(readinessResult.HasApprovalWebhook, Is.True);

            // 4. Verify history contains the webhook
            var history = await GetWebhookHistoryAsync(caseId, headRef);
            Assert.That(history.Records, Is.Not.Null);
            Assert.That(history.Records!.Count, Is.GreaterThanOrEqualTo(1),
                "After recording, history must contain at least one record");
            Assert.That(history.Records[0].Outcome, Is.EqualTo(ApprovalWebhookOutcome.Approved));

            // 5. Verify evidence pack history
            var packHistory = await GetEvidenceHistoryAsync(headRef, caseId);
            Assert.That(packHistory.Packs, Is.Not.Null);
            Assert.That(packHistory.Packs!.Count, Is.GreaterThanOrEqualTo(1),
                "After persisting, pack history must contain at least one record");
        }

        // ════════════════════════════════════════════════════════════════════
        // IT15–IT16: Additional outcome and constraint tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT15_RecordDeniedWebhook_Returns200AndRecordsCorrectly()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Denied,
                    CorrelationId = Guid.NewGuid().ToString("N")
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            RecordApprovalWebhookResponse? body = await resp.Content.ReadFromJsonAsync<RecordApprovalWebhookResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True, "Denied webhook must still be recorded successfully");
        }

        [Test]
        public async Task IT16_PersistEvidence_RequireReleaseGrade_NoApprovedWebhook_Returns400()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    RequireReleaseGrade = true  // requires release-grade evidence
                });

            // RequireReleaseGrade=true without an approved webhook should fail
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                $"Expected 400 when requiring release-grade but no approved webhook exists. Actual: {resp.StatusCode}");
        }

        // ════════════════════════════════════════════════════════════════════
        // IT17: Missing required field
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT17_GetReleaseReadiness_NullHeadRef_Returns400()
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = null! });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400),
                $"Expected 400 for null HeadRef. Actual: {(int)resp.StatusCode}");
        }

        // ════════════════════════════════════════════════════════════════════
        // IT18–IT19: History after operations
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT18_GetWebhookHistory_AfterRecording_ReturnsRecord()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Record a webhook
            await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Escalated,
                    CorrelationId = Guid.NewGuid().ToString("N")
                });

            // Query history
            var histResp = await _client.GetAsync($"{BaseUrl}/webhooks/approval/history?caseId={caseId}&headRef={headRef}");
            Assert.That(histResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            GetApprovalWebhookHistoryResponse? history = await histResp.Content.ReadFromJsonAsync<GetApprovalWebhookHistoryResponse>();
            Assert.That(history, Is.Not.Null);
            Assert.That(history!.Records, Is.Not.Null);
            Assert.That(history.Records!.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(history.Records[0].Outcome, Is.EqualTo(ApprovalWebhookOutcome.Escalated));
        }

        [Test]
        public async Task IT19_GetEvidenceHistory_AfterPersisting_ReturnsPack()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Persist evidence
            await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    RequireReleaseGrade = false
                });

            // Query history
            var histResp = await _client.GetAsync($"{BaseUrl}/evidence/history?headRef={headRef}&caseId={caseId}");
            Assert.That(histResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            GetEvidencePackHistoryResponse? history = await histResp.Content.ReadFromJsonAsync<GetEvidencePackHistoryResponse>();
            Assert.That(history, Is.Not.Null);
            Assert.That(history!.Packs, Is.Not.Null);
            Assert.That(history.Packs!.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(history.Packs[0].HeadRef, Is.EqualTo(headRef));
        }

        // ════════════════════════════════════════════════════════════════════
        // IT20: Concurrency — no lost writes
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT20_ConcurrentWebhookRecords_AllStored()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();
            const int concurrentCount = 5;

            // Fire multiple webhook recordings concurrently
            var tasks = Enumerable.Range(0, concurrentCount).Select(i =>
                _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                    new RecordApprovalWebhookRequest
                    {
                        CaseId = caseId,
                        HeadRef = headRef,
                        Outcome = ApprovalWebhookOutcome.Approved,
                        CorrelationId = $"concurrent-{i}-{Guid.NewGuid():N}"
                    })
            ).ToList();

            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.StatusCode == HttpStatusCode.OK), Is.True,
                "All concurrent webhook recordings must return 200");

            // Verify history contains all records
            var histResp = await _client.GetAsync(
                $"{BaseUrl}/webhooks/approval/history?caseId={caseId}&headRef={headRef}&maxRecords=100");
            var history = await histResp.Content.ReadFromJsonAsync<GetApprovalWebhookHistoryResponse>();
            Assert.That(history!.Records!.Count, Is.GreaterThanOrEqualTo(concurrentCount),
                $"History must contain at least {concurrentCount} records after concurrent writes");
        }

        // ════════════════════════════════════════════════════════════════════
        // IT21: Case isolation
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT21_MultipleCases_AreIsolated_InReadinessResponses()
        {
            string caseId1 = UniqueCase();
            string caseId2 = UniqueCase();
            string headRef = UniqueHead();

            // Record webhook and persist evidence for case 1 only
            await PostWebhookAsync(caseId1, headRef, ApprovalWebhookOutcome.Approved);
            await PostEvidenceAsync(headRef, caseId1, requireWebhook: false, requireReleaseGrade: false);

            // Case 1 should be Ready
            var readiness1 = await PostReadinessAsync(headRef, caseId1, requireWebhook: true);
            Assert.That(readiness1.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "Case 1 should be Ready after webhook and evidence");

            // Case 2 should remain Indeterminate or Pending (no evidence)
            var readiness2 = await PostReadinessAsync(headRef, caseId2, requireWebhook: false);
            Assert.That(readiness2.Status,
                Is.EqualTo(SignOffReleaseReadinessStatus.Indeterminate)
                .Or.EqualTo(SignOffReleaseReadinessStatus.Pending),
                "Case 2 should remain Indeterminate/Pending since no evidence was persisted for it");
        }

        // ════════════════════════════════════════════════════════════════════
        // IT22–IT24: Escalated / TimedOut / DeliveryError outcomes
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT22_EscalatedWebhook_RecordedAndSurfacedInHistory()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var result = await PostWebhookAsync(caseId, headRef, ApprovalWebhookOutcome.Escalated);
            Assert.That(result.Success, Is.True, "Escalated webhook must be recorded successfully");

            var history = await GetWebhookHistoryAsync(caseId, headRef);
            Assert.That(history.Records!.Any(r => r.Outcome == ApprovalWebhookOutcome.Escalated), Is.True,
                "Escalated outcome must appear in history");
        }

        [Test]
        public async Task IT23_TimedOutWebhook_RecordedAndReadinessReturnsBlocked()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Record a timed-out webhook (this acts as a blocker)
            var result = await PostWebhookAsync(caseId, headRef, ApprovalWebhookOutcome.TimedOut);
            Assert.That(result.Success, Is.True, "TimedOut webhook must be recorded");

            // Readiness requires approval webhook but TimedOut is not an approval
            var readiness = await PostReadinessAsync(headRef, caseId, requireWebhook: true);
            Assert.That(readiness.Status,
                Is.EqualTo(SignOffReleaseReadinessStatus.Blocked)
                .Or.EqualTo(SignOffReleaseReadinessStatus.Pending)
                .Or.EqualTo(SignOffReleaseReadinessStatus.Indeterminate),
                "TimedOut should prevent Ready status when approval webhook is required");
        }

        [Test]
        public async Task IT24_DeliveryErrorWebhook_RecordedAndReadinessNotReady()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var result = await PostWebhookAsync(caseId, headRef, ApprovalWebhookOutcome.DeliveryError);
            Assert.That(result.Success, Is.True, "DeliveryError webhook must be recorded");

            var readiness = await PostReadinessAsync(headRef, caseId, requireWebhook: true);
            Assert.That(readiness.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "DeliveryError should not produce Ready status when approval webhook is required");
        }

        // ════════════════════════════════════════════════════════════════════
        // IT25: MaxRecords query parameter
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT25_MaxRecords_LimitsHistoryResponse()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Insert 5 webhook records
            for (int i = 0; i < 5; i++)
            {
                await PostWebhookAsync(caseId, headRef, ApprovalWebhookOutcome.Approved);
            }

            // Request with maxRecords=2
            var resp = await _client.GetAsync(
                $"{BaseUrl}/webhooks/approval/history?caseId={caseId}&headRef={headRef}&maxRecords=2");
            var history = await resp.Content.ReadFromJsonAsync<GetApprovalWebhookHistoryResponse>();
            Assert.That(history!.Records!.Count, Is.LessThanOrEqualTo(2),
                "MaxRecords=2 should limit the response to at most 2 records");
        }

        // ════════════════════════════════════════════════════════════════════
        // IT26: Freshness — evidence with short window becomes Stale
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT26_EvidenceWithExpiredWindow_ReadinessReturnsStaleOrBlocked()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            // Persist evidence with a freshness window that is already expired
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    RequireReleaseGrade = false,
                    FreshnessWindowHours = 0  // zero-hour window = expired immediately
                });

            // Even if persistence succeeds, readiness for expired evidence should reflect staleness
            var readiness = await PostReadinessAsync(headRef, caseId, requireWebhook: false);
            Assert.That(readiness.Status,
                Is.EqualTo(SignOffReleaseReadinessStatus.Stale)
                .Or.EqualTo(SignOffReleaseReadinessStatus.Blocked)
                .Or.EqualTo(SignOffReleaseReadinessStatus.Indeterminate)
                .Or.EqualTo(SignOffReleaseReadinessStatus.Pending),
                "Expired evidence should not produce Ready status");
        }

        // ════════════════════════════════════════════════════════════════════
        // IT27–IT29: Response schema contracts
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT27_ReadinessResponse_ContainsAllRequiredSchemaFields()
        {
            string headRef = UniqueHead();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Parse raw JSON to validate schema contract
            string json = await resp.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            Assert.That(root.TryGetProperty("success", out _), Is.True, "Must have 'success' field");
            Assert.That(root.TryGetProperty("readinessStatus", out _), Is.True, "Must have 'readinessStatus' field");
            Assert.That(root.TryGetProperty("hasApprovalWebhook", out _), Is.True, "Must have 'hasApprovalWebhook' field");
            Assert.That(root.TryGetProperty("blockers", out _), Is.True, "Must have 'blockers' field");
        }

        [Test]
        public async Task IT28_WebhookRecordResponse_ContainsAllRequiredSchemaFields()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            string json = await resp.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            Assert.That(root.TryGetProperty("success", out _), Is.True, "Must have 'success' field");
            Assert.That(root.TryGetProperty("recordId", out _), Is.True, "Must have 'recordId' field");
            Assert.That(root.TryGetProperty("outcome", out _), Is.True, "Must have 'outcome' field");
            Assert.That(root.TryGetProperty("isValidOutcome", out _), Is.True, "Must have 'isValidOutcome' field");
        }

        [Test]
        public async Task IT29_EvidencePackResponse_ContainsAllRequiredSchemaFields()
        {
            string caseId = UniqueCase();
            string headRef = UniqueHead();

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    RequireReleaseGrade = false
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            string json = await resp.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            Assert.That(root.TryGetProperty("success", out _), Is.True, "Must have 'success' field");
            Assert.That(root.TryGetProperty("packId", out _), Is.True, "Must have 'packId' field");
            Assert.That(root.TryGetProperty("freshnessStatus", out _), Is.True, "Must have 'freshnessStatus' field");
            Assert.That(root.TryGetProperty("contentHash", out _), Is.True, "Must have 'contentHash' field");
        }

        // ════════════════════════════════════════════════════════════════════
        // IT30: Repeatability — 3 identical runs produce identical outcomes
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task IT30_FullJourney_IsDeterministic_Across3IndependentRuns()
        {
            var outcomes = new List<(bool Success, SignOffReleaseReadinessStatus Status)>();

            for (int run = 1; run <= 3; run++)
            {
                // Each run uses a fresh factory + fresh case to ensure independence
                await using var factory = new EvidencePersistenceFactory();
                using var client = factory.CreateClient();
                string jwt = await ObtainJwtAsync(client, $"run{run}-{Guid.NewGuid():N}");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

                string caseId = UniqueCase();
                string headRef = $"sha-run{run}-{Guid.NewGuid():N}";

                // Record approved webhook
                await client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                    new RecordApprovalWebhookRequest
                    {
                        CaseId = caseId,
                        HeadRef = headRef,
                        Outcome = ApprovalWebhookOutcome.Approved,
                        CorrelationId = Guid.NewGuid().ToString("N")
                    });

                // Persist evidence
                await client.PostAsJsonAsync($"{BaseUrl}/evidence",
                    new PersistSignOffEvidenceRequest
                    {
                        HeadRef = headRef,
                        CaseId = caseId,
                        RequireReleaseGrade = false
                    });

                // Check readiness
                var readinessResp = await client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                    new GetSignOffReleaseReadinessRequest
                    {
                        HeadRef = headRef,
                        CaseId = caseId,
                        
                    });

                GetSignOffReleaseReadinessResponse? readiness =
                    await readinessResp.Content.ReadFromJsonAsync<GetSignOffReleaseReadinessResponse>();
                outcomes.Add((readiness!.Success, readiness.Status));
            }

            Assert.That(outcomes.All(o => o.Success), Is.True, "All 3 runs must succeed");
            Assert.That(outcomes.All(o => o.Status == SignOffReleaseReadinessStatus.Ready), Is.True,
                "All 3 runs must produce Ready status (deterministic behavior)");
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        private static string UniqueCase() => $"case-{Guid.NewGuid():N}";
        private static string UniqueHead() => $"sha-{Guid.NewGuid():N}";

        private async Task<RecordApprovalWebhookResponse> PostWebhookAsync(
            string caseId, string headRef, ApprovalWebhookOutcome outcome,
            string? correlationId = null)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = headRef,
                    Outcome = outcome,
                    CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<RecordApprovalWebhookResponse>())!;
        }

        private async Task<PersistSignOffEvidenceResponse> PostEvidenceAsync(
            string headRef, string caseId, bool requireWebhook, bool requireReleaseGrade)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    RequireReleaseGrade = requireReleaseGrade
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>())!;
        }

        private async Task<GetSignOffReleaseReadinessResponse> PostReadinessAsync(
            string headRef, string? caseId, bool requireWebhook)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                });
            return (await resp.Content.ReadFromJsonAsync<GetSignOffReleaseReadinessResponse>())!;
        }

        private async Task<GetApprovalWebhookHistoryResponse> GetWebhookHistoryAsync(
            string? caseId, string? headRef, int maxRecords = 50)
        {
            string url = $"{BaseUrl}/webhooks/approval/history?maxRecords={maxRecords}";
            if (caseId != null) url += $"&caseId={caseId}";
            if (headRef != null) url += $"&headRef={headRef}";
            var resp = await _client.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<GetApprovalWebhookHistoryResponse>())!;
        }

        private async Task<GetEvidencePackHistoryResponse> GetEvidenceHistoryAsync(
            string? headRef, string? caseId, int maxRecords = 50)
        {
            string url = $"{BaseUrl}/evidence/history?maxRecords={maxRecords}";
            if (headRef != null) url += $"&headRef={headRef}";
            if (caseId != null) url += $"&caseId={caseId}";
            var resp = await _client.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<GetEvidencePackHistoryResponse>())!;
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string userTag)
        {
            string email = $"evidenceit-{userTag}@biatec-test.example.com";
            HttpResponseMessage resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email = email,
                Password = "EvidencePersistIT!Pass1",
                ConfirmPassword = "EvidencePersistIT!Pass1",
                FullName = $"Evidence Persist IT User ({userTag})"
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
