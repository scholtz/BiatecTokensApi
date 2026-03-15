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
    /// Integration tests for the Compliance Orchestration rescreen and provider-callback HTTP endpoints.
    ///
    /// Tests:
    ///   POST /api/v1/compliance-orchestration/rescreen/{decisionId}
    ///   POST /api/v1/compliance-orchestration/provider-callback
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceOrchestrationRescreenIntegrationTests
    {
        private RescreenWebAppFactory _factory = null!;
        private HttpClient _client = null!;
        private HttpClient _unauthClient = null!;

        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new RescreenWebAppFactory();
            _unauthClient = _factory.CreateClient();

            var email = $"rescreen-test-{Guid.NewGuid():N}@biatec-test.example.com";
            var regReq = new RegisterRequest
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName = "Rescreen Integration Test User"
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

        private sealed class RescreenWebAppFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForRescreenIntegrationTests32C!",
                        ["JwtConfig:SecretKey"] = "RescreenIntegTestSecretKey32CharsRequired!",
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
                        ["KycConfig:WebhookSecret"] = ""  // Disable signature validation for integration tests
                    });
                });
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private async Task<string> InitiateCheckAndGetDecisionId(string subjectId, string contextId)
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance-orchestration/initiate", new
            {
                subjectId,
                contextId,
                checkType = 2 // Combined
            });
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<ComplianceCheckResponse>(_jsonOptions);
            return body!.DecisionId!;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // POST /rescreen/{decisionId}
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task Rescreen_ExistingDecision_Returns200WithNewDecision()
        {
            var decisionId = await InitiateCheckAndGetDecisionId(
                $"rescreen-subj-{Guid.NewGuid():N}",
                $"rescreen-ctx-{Guid.NewGuid():N}");

            var rescreenResp = await _client.PostAsJsonAsync(
                $"/api/v1/compliance-orchestration/rescreen/{decisionId}",
                new { reason = "IntegrationTest" });

            Assert.That(rescreenResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var body = await rescreenResp.Content.ReadFromJsonAsync<RescreenResponse>(_jsonOptions);
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.PreviousDecisionId, Is.EqualTo(decisionId));
            Assert.That(body.NewDecision, Is.Not.Null);
            Assert.That(body.NewDecision!.DecisionId, Is.Not.EqualTo(decisionId));
        }

        [Test]
        public async Task Rescreen_UnknownDecisionId_Returns404()
        {
            var resp = await _client.PostAsJsonAsync(
                "/api/v1/compliance-orchestration/rescreen/non-existent-decision-id",
                new { });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task Rescreen_Unauthenticated_Returns401()
        {
            var resp = await _unauthClient.PostAsJsonAsync(
                "/api/v1/compliance-orchestration/rescreen/some-decision-id",
                new { });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Rescreen_WithEmptyBody_Returns200()
        {
            var decisionId = await InitiateCheckAndGetDecisionId(
                $"rescreen-empty-body-{Guid.NewGuid():N}",
                $"ctx-empty-body-{Guid.NewGuid():N}");

            // Empty JSON body should be treated as default RescreenRequest
            var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            var rescreenResp = await _client.PostAsync(
                $"/api/v1/compliance-orchestration/rescreen/{decisionId}", content);

            Assert.That(rescreenResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task Rescreen_NewDecisionIsApproved_WhenMockAutoApprove()
        {
            var decisionId = await InitiateCheckAndGetDecisionId(
                $"rescreen-approved-{Guid.NewGuid():N}",
                $"ctx-approved-{Guid.NewGuid():N}");

            var rescreenResp = await _client.PostAsJsonAsync(
                $"/api/v1/compliance-orchestration/rescreen/{decisionId}",
                new { reason = "TestRescreen" });

            Assert.That(rescreenResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await rescreenResp.Content.ReadFromJsonAsync<RescreenResponse>(_jsonOptions);
            Assert.That(body!.NewDecision!.State, Is.EqualTo(ComplianceDecisionState.Approved));
        }

        [Test]
        public async Task Rescreen_ReturnsNewDecisionWithAuditTrail()
        {
            var decisionId = await InitiateCheckAndGetDecisionId(
                $"rescreen-audit-{Guid.NewGuid():N}",
                $"ctx-audit-{Guid.NewGuid():N}");

            var rescreenResp = await _client.PostAsJsonAsync(
                $"/api/v1/compliance-orchestration/rescreen/{decisionId}",
                new { reason = "TestAuditTrail" });

            var body = await rescreenResp.Content.ReadFromJsonAsync<RescreenResponse>(_jsonOptions);
            Assert.That(body!.NewDecision!.AuditTrail, Is.Not.Null);
            Assert.That(body.NewDecision.AuditTrail.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task Rescreen_OriginalDecisionStatusNotChanged()
        {
            var decisionId = await InitiateCheckAndGetDecisionId(
                $"rescreen-orig-{Guid.NewGuid():N}",
                $"ctx-orig-{Guid.NewGuid():N}");

            // Get original status
            var origResp = await _client.GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");
            var origBody = await origResp.Content.ReadFromJsonAsync<ComplianceCheckResponse>(_jsonOptions);
            var origState = origBody!.State;

            // Rescreen
            await _client.PostAsJsonAsync(
                $"/api/v1/compliance-orchestration/rescreen/{decisionId}",
                new { });

            // Original decision state should still be the same
            var afterResp = await _client.GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");
            var afterBody = await afterResp.Content.ReadFromJsonAsync<ComplianceCheckResponse>(_jsonOptions);

            // Original state is preserved; only an audit event was added
            Assert.That(afterBody!.State, Is.EqualTo(origState));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // POST /provider-callback
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task ProviderCallback_NullBody_Returns400()
        {
            var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");
            var resp = await _unauthClient.PostAsync(
                "/api/v1/compliance-orchestration/provider-callback", content);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task ProviderCallback_MissingProviderReferenceId_Returns400()
        {
            var resp = await _unauthClient.PostAsJsonAsync(
                "/api/v1/compliance-orchestration/provider-callback",
                new { providerName = "Mock", outcomeStatus = "approved" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task ProviderCallback_UnknownProviderReferenceId_Returns404()
        {
            var resp = await _unauthClient.PostAsJsonAsync(
                "/api/v1/compliance-orchestration/provider-callback",
                new
                {
                    providerName = "Mock",
                    providerReferenceId = "ref-completely-unknown-12345",
                    eventType = "test.event",
                    outcomeStatus = "approved"
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task ProviderCallback_EndpointIsAnonymous_NoAuthRequired()
        {
            // The endpoint should be reachable without authentication
            // (it validates the payload itself, not via JWT)
            var resp = await _unauthClient.PostAsJsonAsync(
                "/api/v1/compliance-orchestration/provider-callback",
                new
                {
                    providerName = "Mock",
                    providerReferenceId = "ref-anon-test",
                    eventType = "test.event",
                    outcomeStatus = "approved"
                });

            // Should NOT be 401 — the endpoint is anonymous
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ProviderCallback_IdempotentReplay_Returns200WithReplayFlag()
        {
            // First initiate a check to create a real decision with a provider reference
            var decisionId = await InitiateCheckAndGetDecisionId(
                $"cb-idem-{Guid.NewGuid():N}",
                $"cb-ctx-idem-{Guid.NewGuid():N}");

            // Get the status to find the provider reference (if populated in audit trail)
            var statusResp = await _client.GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");
            var statusBody = await statusResp.Content.ReadFromJsonAsync<ComplianceCheckResponse>(_jsonOptions);
            var providerRef = statusBody?.AuditTrail?
                .Where(e => e.ProviderReferenceId != null)
                .Select(e => e.ProviderReferenceId!)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(providerRef))
            {
                Assert.Ignore("No provider reference in audit trail — skipping idempotency integration test.");
                return;
            }

            var callbackPayload = new
            {
                providerName = "Mock",
                providerReferenceId = providerRef,
                eventType = "test.event",
                outcomeStatus = "approved",
                idempotencyKey = $"idem-key-{Guid.NewGuid():N}"
            };

            // First call
            var first = await _unauthClient.PostAsJsonAsync(
                "/api/v1/compliance-orchestration/provider-callback", callbackPayload);
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var firstBody = await first.Content.ReadFromJsonAsync<ProviderCallbackResponse>(_jsonOptions);
            Assert.That(firstBody!.IsIdempotentReplay, Is.False);

            // Second call — same idempotency key
            var second = await _unauthClient.PostAsJsonAsync(
                "/api/v1/compliance-orchestration/provider-callback", callbackPayload);
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var secondBody = await second.Content.ReadFromJsonAsync<ProviderCallbackResponse>(_jsonOptions);
            Assert.That(secondBody!.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task ProviderCallback_ValidPayload_Returns200WithDecisionId()
        {
            var decisionId = await InitiateCheckAndGetDecisionId(
                $"cb-valid-{Guid.NewGuid():N}",
                $"cb-ctx-valid-{Guid.NewGuid():N}");

            var statusResp = await _client.GetAsync($"/api/v1/compliance-orchestration/status/{decisionId}");
            var statusBody = await statusResp.Content.ReadFromJsonAsync<ComplianceCheckResponse>(_jsonOptions);
            var providerRef = statusBody?.AuditTrail?
                .Where(e => e.ProviderReferenceId != null)
                .Select(e => e.ProviderReferenceId!)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(providerRef))
            {
                Assert.Ignore("No provider reference in audit trail.");
                return;
            }

            var resp = await _unauthClient.PostAsJsonAsync(
                "/api/v1/compliance-orchestration/provider-callback",
                new
                {
                    providerName = "Mock",
                    providerReferenceId = providerRef,
                    eventType = "verification.verified",
                    outcomeStatus = "approved"
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<ProviderCallbackResponse>(_jsonOptions);
            Assert.That(body!.Success, Is.True);
            Assert.That(body.DecisionId, Is.EqualTo(decisionId));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Schema contract assertions
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task Rescreen_ResponseShape_ContainsExpectedFields()
        {
            var decisionId = await InitiateCheckAndGetDecisionId(
                $"schema-subj-{Guid.NewGuid():N}",
                $"schema-ctx-{Guid.NewGuid():N}");

            var resp = await _client.PostAsJsonAsync(
                $"/api/v1/compliance-orchestration/rescreen/{decisionId}",
                new { });
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.That(root.TryGetProperty("success", out _), Is.True, "response must contain 'success'");
            Assert.That(root.TryGetProperty("previousDecisionId", out _), Is.True, "response must contain 'previousDecisionId'");
            Assert.That(root.TryGetProperty("newDecision", out _), Is.True, "response must contain 'newDecision'");
        }

        [Test]
        public async Task ProviderCallback_ResponseShape_ContainsExpectedFields()
        {
            var resp = await _unauthClient.PostAsJsonAsync(
                "/api/v1/compliance-orchestration/provider-callback",
                new
                {
                    providerName = "Mock",
                    providerReferenceId = "schema-check-ref",
                    outcomeStatus = "approved"
                });

            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Even a failed response should have 'success' and 'errorCode'
            Assert.That(root.TryGetProperty("success", out _), Is.True, "response must contain 'success'");
        }
    }
}
