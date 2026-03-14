using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.ComplianceOrchestration;
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
    /// Integration tests for the Compliance Orchestration HTTP API endpoints.
    /// Tests the full HTTP pipeline: authentication, controller, service, providers.
    /// Covers KYC-only, AML-only, Combined checks, idempotency, status retrieval,
    /// decision history, fail-closed states, and API contract validation.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceOrchestrationIntegrationTests
    {
        private CustomWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;
        private HttpClient _unauthClient = null!;

        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new CustomWebApplicationFactory();
            _unauthClient = _factory.CreateClient();

            // Register and login to get a JWT for authenticated endpoint tests
            var email = $"compliance-orch-test-{Guid.NewGuid():N}@biatec-test.example.com";
            var regReq = new RegisterRequest
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName = "Compliance Orchestration Test User"
            };
            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            var jwtToken = regBody?.AccessToken ?? string.Empty;

            _client = _factory.CreateClient();
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _unauthClient?.Dispose();
            _factory?.Dispose();
        }

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
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForComplianceOrchestrationTests32C",
                        ["JwtConfig:SecretKey"] = "ComplianceOrchTestSecretKey32CharsRequired!",
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
                        ["KycConfig:MockAutoApprove"] = "true"
                    });
                });
            }
        }

        // ── Helper methods ─────────────────────────────────────────────────────

        private async Task<(HttpResponseMessage Response, JsonDocument? Body)> PostJsonAsync(string url, object body)
        {
            var response = await _client.PostAsJsonAsync(url, body);
            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); } catch { }
            return (response, doc);
        }

        private async Task<(HttpResponseMessage Response, JsonDocument? Body)> GetAsync(string url)
        {
            var response = await _client.GetAsync(url);
            JsonDocument? doc = null;
            try { doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); } catch { }
            return (response, doc);
        }

        private async Task<string> InitiateCheckAndGetDecisionId(
            string subjectId,
            string contextId,
            int checkType = 2,
            Dictionary<string, string>? metadata = null,
            string? idempotencyKey = null)
        {
            var body = new
            {
                subjectId,
                contextId,
                checkType,
                subjectMetadata = metadata ?? new Dictionary<string, string>(),
                idempotencyKey
            };
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate", body);
            resp.EnsureSuccessStatusCode();
            return doc!.RootElement.GetProperty("decisionId").GetString()!;
        }

        // ── Authentication ─────────────────────────────────────────────────────

        [Test]
        public async Task Initiate_Unauthenticated_Returns401()
        {
            var response = await _unauthClient.PostAsJsonAsync(
                "/api/v1/compliance-orchestration/initiate",
                new { subjectId = "user-001", contextId = "ctx-001", checkType = 2 });

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetStatus_Unauthenticated_Returns401()
        {
            var response = await _unauthClient.GetAsync(
                "/api/v1/compliance-orchestration/status/some-id");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetHistory_Unauthenticated_Returns401()
        {
            var response = await _unauthClient.GetAsync(
                "/api/v1/compliance-orchestration/history/some-subject");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── POST /initiate ─────────────────────────────────────────────────────

        [Test]
        public async Task Initiate_ValidCombinedCheck_Returns200()
        {
            var (resp, _) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "api-user-001", contextId = "ctx-api-001", checkType = 2 });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task Initiate_ValidCombinedCheck_SuccessIsTrue()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "api-user-002", contextId = "ctx-api-002", checkType = 2 });

            resp.EnsureSuccessStatusCode();
            Assert.That(doc!.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }

        [Test]
        public async Task Initiate_ValidCombinedCheck_HasDecisionId()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "api-user-003", contextId = "ctx-api-003", checkType = 2 });

            resp.EnsureSuccessStatusCode();
            Assert.That(doc!.RootElement.TryGetProperty("decisionId", out var decisionId), Is.True);
            Assert.That(decisionId.GetString(), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_KycOnlyCheck_Returns200()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "api-kyc-001", contextId = "ctx-kyc-001", checkType = 0 });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(doc!.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }

        [Test]
        public async Task Initiate_AmlOnlyCheck_Returns200()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "api-aml-001", contextId = "ctx-aml-001", checkType = 1 });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(doc!.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }

        [Test]
        public async Task Initiate_AmlSanctionsHit_Returns200WithRejectedState()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new
                {
                    subjectId = "api-sanctions-user",
                    contextId = "ctx-sanctions-001",
                    checkType = 1,
                    subjectMetadata = new Dictionary<string, string> { ["sanctions_flag"] = "true" }
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var state = doc!.RootElement.GetProperty("state").GetInt32();
            Assert.That(state, Is.EqualTo((int)ComplianceDecisionState.Rejected));
        }

        [Test]
        public async Task Initiate_AmlReviewRequired_Returns200WithNeedsReviewState()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new
                {
                    subjectId = "api-review-user",
                    contextId = "ctx-review-001",
                    checkType = 1,
                    subjectMetadata = new Dictionary<string, string> { ["review_flag"] = "true" }
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var state = doc!.RootElement.GetProperty("state").GetInt32();
            Assert.That(state, Is.EqualTo((int)ComplianceDecisionState.NeedsReview));
        }

        [Test]
        public async Task Initiate_AmlTimeout_Returns200WithErrorState()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new
                {
                    subjectId = "api-timeout-user",
                    contextId = "ctx-timeout-001",
                    checkType = 1,
                    subjectMetadata = new Dictionary<string, string> { ["simulate_timeout"] = "true" }
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var state = doc!.RootElement.GetProperty("state").GetInt32();
            Assert.That(state, Is.EqualTo((int)ComplianceDecisionState.Error));
        }

        [Test]
        public async Task Initiate_MissingSubjectId_Returns400()
        {
            var (resp, _) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "", contextId = "ctx-001", checkType = 2 });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Initiate_MissingContextId_Returns400()
        {
            var (resp, _) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "user-001", contextId = "", checkType = 2 });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Initiate_NullBody_Returns400()
        {
            var response = await _client.PostAsync(
                "/api/v1/compliance-orchestration/initiate",
                new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Initiate_HasAuditTrailInResponse()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "audit-trail-user", contextId = "ctx-audit-001", checkType = 2 });

            resp.EnsureSuccessStatusCode();
            Assert.That(doc!.RootElement.TryGetProperty("auditTrail", out var trail), Is.True);
            Assert.That(trail.GetArrayLength(), Is.GreaterThan(0));
        }

        [Test]
        public async Task Initiate_AuditTrailFirstEventIsCheckInitiated()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "audit-event-user", contextId = "ctx-audit-002", checkType = 1 });

            resp.EnsureSuccessStatusCode();
            var trail = doc!.RootElement.GetProperty("auditTrail");
            var firstEvent = trail[0].GetProperty("eventType").GetString();
            Assert.That(firstEvent, Is.EqualTo("CheckInitiated"));
        }

        // ── Idempotency ───────────────────────────────────────────────────────

        [Test]
        public async Task Initiate_SameIdempotencyKey_ReturnsSameDecisionId()
        {
            var idemKey = $"api-idem-{Guid.NewGuid():N}";
            var body = new
            {
                subjectId = "idem-api-user",
                contextId = "ctx-idem-api",
                checkType = 2,
                idempotencyKey = idemKey
            };

            var (_, doc1) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate", body);
            var (_, doc2) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate", body);

            var id1 = doc1!.RootElement.GetProperty("decisionId").GetString();
            var id2 = doc2!.RootElement.GetProperty("decisionId").GetString();
            Assert.That(id1, Is.EqualTo(id2));
        }

        [Test]
        public async Task Initiate_SameIdempotencyKey_SecondCallIsReplay()
        {
            var idemKey = $"api-idem-replay-{Guid.NewGuid():N}";
            var body = new
            {
                subjectId = "idem-replay-user",
                contextId = "ctx-idem-replay",
                checkType = 2,
                idempotencyKey = idemKey
            };

            await PostJsonAsync("/api/v1/compliance-orchestration/initiate", body);
            var (_, doc2) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate", body);

            var isReplay = doc2!.RootElement.GetProperty("isIdempotentReplay").GetBoolean();
            Assert.That(isReplay, Is.True);
        }

        [Test]
        public async Task Initiate_ThreeReplays_AllReturnSameDecisionId()
        {
            var idemKey = $"api-3-replay-{Guid.NewGuid():N}";
            var body = new
            {
                subjectId = "three-replay-user",
                contextId = "ctx-3-replay",
                checkType = 1,
                idempotencyKey = idemKey
            };

            var (_, doc1) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate", body);
            var (_, doc2) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate", body);
            var (_, doc3) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate", body);

            var id1 = doc1!.RootElement.GetProperty("decisionId").GetString();
            var id2 = doc2!.RootElement.GetProperty("decisionId").GetString();
            var id3 = doc3!.RootElement.GetProperty("decisionId").GetString();

            Assert.That(id1, Is.EqualTo(id2));
            Assert.That(id2, Is.EqualTo(id3));
        }

        // ── GET /status/{decisionId} ───────────────────────────────────────────

        [Test]
        public async Task GetStatus_ExistingDecision_Returns200()
        {
            var decisionId = await InitiateCheckAndGetDecisionId("status-user-001", "ctx-status-001");
            var (resp, _) = await GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GetStatus_ExistingDecision_SuccessIsTrue()
        {
            var decisionId = await InitiateCheckAndGetDecisionId("status-user-002", "ctx-status-002");
            var (_, doc) = await GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");

            Assert.That(doc!.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }

        [Test]
        public async Task GetStatus_ExistingDecision_MatchesInitialState()
        {
            var (_, initDoc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "status-match-user", contextId = "ctx-status-match", checkType = 1 });
            var decisionId = initDoc!.RootElement.GetProperty("decisionId").GetString()!;
            var expectedState = initDoc.RootElement.GetProperty("state").GetInt32();

            var (_, statusDoc) = await GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");
            var actualState = statusDoc!.RootElement.GetProperty("state").GetInt32();

            Assert.That(actualState, Is.EqualTo(expectedState));
        }

        [Test]
        public async Task GetStatus_NonExistentDecision_Returns404()
        {
            var (resp, _) = await GetAsync("/api/v1/compliance-orchestration/status/does-not-exist-xyz");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task GetStatus_RepeatedReads_ProduceConsistentState()
        {
            var decisionId = await InitiateCheckAndGetDecisionId("consistent-user", "ctx-consistent");

            var (_, doc1) = await GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");
            var (_, doc2) = await GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");
            var (_, doc3) = await GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");

            var s1 = doc1!.RootElement.GetProperty("state").GetInt32();
            var s2 = doc2!.RootElement.GetProperty("state").GetInt32();
            var s3 = doc3!.RootElement.GetProperty("state").GetInt32();

            Assert.That(s1, Is.EqualTo(s2));
            Assert.That(s2, Is.EqualTo(s3));
        }

        // ── GET /history/{subjectId} ───────────────────────────────────────────

        [Test]
        public async Task GetHistory_NoDecisions_ReturnsEmptyList()
        {
            var (resp, doc) = await GetAsync($"/api/v1/compliance-orchestration/history/no-history-user-{Guid.NewGuid():N}");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(doc!.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(0));
        }

        [Test]
        public async Task GetHistory_AfterOneDecision_ReturnsSingleEntry()
        {
            var subjectId = $"hist-api-user-{Guid.NewGuid():N}";
            await InitiateCheckAndGetDecisionId(subjectId, "ctx-hist-1");

            var (_, doc) = await GetAsync($"/api/v1/compliance-orchestration/history/{subjectId}");

            Assert.That(doc!.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
        }

        [Test]
        public async Task GetHistory_AfterThreeDecisions_ReturnsAll()
        {
            var subjectId = $"hist-3-api-user-{Guid.NewGuid():N}";
            await InitiateCheckAndGetDecisionId(subjectId, "ctx-h1", idempotencyKey: $"h1-{subjectId}");
            await InitiateCheckAndGetDecisionId(subjectId, "ctx-h2", idempotencyKey: $"h2-{subjectId}");
            await InitiateCheckAndGetDecisionId(subjectId, "ctx-h3", idempotencyKey: $"h3-{subjectId}");

            var (_, doc) = await GetAsync($"/api/v1/compliance-orchestration/history/{subjectId}");

            Assert.That(doc!.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
        }

        [Test]
        public async Task GetHistory_SubjectIdInResponse_MatchesRequest()
        {
            var subjectId = $"hist-subj-{Guid.NewGuid():N}";
            await InitiateCheckAndGetDecisionId(subjectId, "ctx-subj-check");

            var (_, doc) = await GetAsync($"/api/v1/compliance-orchestration/history/{subjectId}");

            Assert.That(doc!.RootElement.GetProperty("subjectId").GetString(), Is.EqualTo(subjectId));
        }

        // ── API contract (schema) assertions ──────────────────────────────────

        [Test]
        public async Task InitiateResponse_HasAllSchemaFields()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "schema-user", contextId = "ctx-schema", checkType = 2 });

            resp.EnsureSuccessStatusCode();
            var root = doc!.RootElement;

            Assert.That(root.TryGetProperty("success", out _), Is.True, "Missing 'success'");
            Assert.That(root.TryGetProperty("decisionId", out _), Is.True, "Missing 'decisionId'");
            Assert.That(root.TryGetProperty("state", out _), Is.True, "Missing 'state'");
            Assert.That(root.TryGetProperty("correlationId", out _), Is.True, "Missing 'correlationId'");
            Assert.That(root.TryGetProperty("isIdempotentReplay", out _), Is.True, "Missing 'isIdempotentReplay'");
            Assert.That(root.TryGetProperty("initiatedAt", out _), Is.True, "Missing 'initiatedAt'");
            Assert.That(root.TryGetProperty("auditTrail", out _), Is.True, "Missing 'auditTrail'");
        }

        [Test]
        public async Task InitiateResponse_AuditTrail_HasEventTypeAndOccurredAt()
        {
            var (resp, doc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "audit-schema-user", contextId = "ctx-audit-schema", checkType = 1 });

            resp.EnsureSuccessStatusCode();
            var trail = doc!.RootElement.GetProperty("auditTrail");
            Assert.That(trail.GetArrayLength(), Is.GreaterThan(0));
            foreach (var evt in trail.EnumerateArray())
            {
                Assert.That(evt.TryGetProperty("eventType", out _), Is.True, "Audit event missing 'eventType'");
                Assert.That(evt.TryGetProperty("occurredAt", out _), Is.True, "Audit event missing 'occurredAt'");
                Assert.That(evt.TryGetProperty("correlationId", out _), Is.True, "Audit event missing 'correlationId'");
            }
        }

        [Test]
        public async Task InitiateResponse_CorrelationIdPropagatedFromHeader()
        {
            var correlationId = $"test-corr-{Guid.NewGuid():N}";
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/compliance-orchestration/initiate")
            {
                Content = JsonContent.Create(new { subjectId = "corr-id-user", contextId = "ctx-corr", checkType = 2 })
            };
            request.Headers.Add("X-Correlation-ID", correlationId);

            var response = await _client.SendAsync(request);
            var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            response.EnsureSuccessStatusCode();
            Assert.That(doc.RootElement.GetProperty("correlationId").GetString(), Is.EqualTo(correlationId));
        }

        // ── End-to-end workflow tests ─────────────────────────────────────────

        [Test]
        public async Task E2E_KycApproved_ThenGetStatus_ShowsApproved()
        {
            // Step 1: Initiate KYC check (MockAutoApprove=true means Approved)
            var (initResp, initDoc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new { subjectId = "e2e-kyc-user", contextId = "ctx-e2e-kyc", checkType = 0 });
            initResp.EnsureSuccessStatusCode();
            var decisionId = initDoc!.RootElement.GetProperty("decisionId").GetString()!;

            // Step 2: Fetch status
            var (statusResp, statusDoc) = await GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");
            statusResp.EnsureSuccessStatusCode();

            Assert.That(statusDoc!.RootElement.GetProperty("state").GetInt32(),
                Is.EqualTo((int)ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task E2E_AmlSanctionsHit_ThenGetStatus_ShowsRejected()
        {
            // Step 1: Initiate AML check with sanctions flag
            var (initResp, initDoc) = await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new
                {
                    subjectId = "e2e-sanctions-user",
                    contextId = "ctx-e2e-sanctions",
                    checkType = 1,
                    subjectMetadata = new Dictionary<string, string> { ["sanctions_flag"] = "true" }
                });
            initResp.EnsureSuccessStatusCode();
            var decisionId = initDoc!.RootElement.GetProperty("decisionId").GetString()!;

            // Step 2: Fetch status
            var (_, statusDoc) = await GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");

            Assert.That(statusDoc!.RootElement.GetProperty("state").GetInt32(),
                Is.EqualTo((int)ComplianceDecisionState.Rejected));
        }

        [Test]
        public async Task E2E_CombinedApproved_HistoryHasEntry_WithAuditTrail()
        {
            var subjectId = $"e2e-combined-{Guid.NewGuid():N}";

            // Step 1: Run combined check
            await InitiateCheckAndGetDecisionId(subjectId, "ctx-e2e-combined");

            // Step 2: Verify history
            var (histResp, histDoc) = await GetAsync($"/api/v1/compliance-orchestration/history/{subjectId}");
            histResp.EnsureSuccessStatusCode();

            Assert.That(histDoc!.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
            var decisions = histDoc.RootElement.GetProperty("decisions");
            Assert.That(decisions.GetArrayLength(), Is.EqualTo(1));

            var decision = decisions[0];
            Assert.That(decision.GetProperty("auditTrail").GetArrayLength(), Is.GreaterThan(0));
        }

        [Test]
        public async Task E2E_ProviderError_HistoryRecordsPersisted()
        {
            var subjectId = $"e2e-error-{Guid.NewGuid():N}";

            // Step 1: Run AML check that errors
            await PostJsonAsync("/api/v1/compliance-orchestration/initiate",
                new
                {
                    subjectId,
                    contextId = "ctx-e2e-error",
                    checkType = 1,
                    subjectMetadata = new Dictionary<string, string> { ["simulate_timeout"] = "true" }
                });

            // Step 2: History should still record the decision
            var (_, histDoc) = await GetAsync($"/api/v1/compliance-orchestration/history/{subjectId}");

            Assert.That(histDoc!.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
            var state = histDoc.RootElement.GetProperty("decisions")[0].GetProperty("state").GetInt32();
            Assert.That(state, Is.EqualTo((int)ComplianceDecisionState.Error));
        }

        [Test]
        public async Task E2E_MultipleSubjects_HistoryIsolated()
        {
            var subjectA = $"isolation-a-{Guid.NewGuid():N}";
            var subjectB = $"isolation-b-{Guid.NewGuid():N}";

            await InitiateCheckAndGetDecisionId(subjectA, "ctx-iso-a");
            await InitiateCheckAndGetDecisionId(subjectB, "ctx-iso-b");

            var (_, histA) = await GetAsync($"/api/v1/compliance-orchestration/history/{subjectA}");
            var (_, histB) = await GetAsync($"/api/v1/compliance-orchestration/history/{subjectB}");

            Assert.That(histA!.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
            Assert.That(histB!.RootElement.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
        }
    }
}
