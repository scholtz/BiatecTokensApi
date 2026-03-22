using BiatecTokensApi.Models.Compliance;
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
    /// Compliance Decision Deployed-Parity Tests — prove that the MICA-ready
    /// compliance decision governance pipeline at <c>/api/v1/compliance/decisions</c>
    /// is authoritative and ready for live operator workflows.
    ///
    /// These tests directly address roadmap gaps:
    ///   - EU MICA Full Compliance (26%): Decision governance and policy evaluation
    ///   - Regulatory Integration (24%): Auditable, immutable compliance decisions
    ///   - Compliance Monitoring (92%): Review-required and expired decision pipelines
    ///
    /// Key behavioral fact:
    ///   POST /api/v1/compliance/decisions requires an Algorand "Address" claim (ARC-0014).
    ///   JWT-authenticated users without the "Address" claim receive HTTP 401 from the controller
    ///   (not HTTP 403), which is the correct fail-closed behavior for this endpoint.
    ///   Read-only endpoints (query, policy-rules, etc.) work with standard JWT auth.
    ///
    /// Coverage:
    ///
    /// CD01: Query decisions (no filter) → Success=true
    /// CD02: Unauthenticated query → 401 fail-closed
    /// CD03: Query with orgId filter → Success=true
    /// CD04: Query with step=KycKybVerification filter → Success=true
    /// CD05: Active decision for unknown org → 404 (no decision found)
    /// CD06: Unauthenticated active decision check → 401 fail-closed
    /// CD07: POST create decision (JWT user without Address claim) → 401 fail-closed
    /// CD08: Unauthenticated create decision → 401 fail-closed
    /// CD09: GET review-required decisions → 200
    /// CD10: Unauthenticated review-required → 401 fail-closed
    /// CD11: GET expired decisions → 200
    /// CD12: Unauthenticated expired decisions → 401 fail-closed
    /// CD13: GET policy-rules/{step} → 200 with policy rules
    /// CD14: Unauthenticated policy-rules → 401 fail-closed
    /// CD15: Three consecutive query calls → deterministic Success=true
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceDecisionDeployedParityTests
    {
        // ════════════════════════════════════════════════════════════════════
        // Factory
        // ════════════════════════════════════════════════════════════════════

        private sealed class CdFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "ComplianceDecisionDeployedParityTestKey!32",
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
                        ["KeyManagementConfig:HardcodedKey"] = "ComplianceDecisionTestKey32Chars!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "cd-dp-test",
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

        private CdFactory  _factory = null!;
        private HttpClient _client  = null!;
        private const string CdBase = "/api/v1/compliance/decisions";

        [SetUp]
        public async Task SetUp()
        {
            _factory = new CdFactory();
            _client  = _factory.CreateClient();
            string jwt = await ObtainJwtAsync(_client, $"cd-{Guid.NewGuid():N}");
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
        // CD01: Query decisions (no filter) → Success=true
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD01_QueryDecisions_NoFilter_ReturnsSuccess()
        {
            var resp = await _client.GetAsync($"{CdBase}/query");
            resp.EnsureSuccessStatusCode();

            var result = await resp.Content.ReadFromJsonAsync<QueryComplianceDecisionsResponse>();
            Assert.That(result!.Success, Is.True, "CD01: unfiltered query must return Success=true");
            Assert.That(result.Decisions, Is.Not.Null, "CD01: Decisions must not be null");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD02: Unauthenticated query → 401 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD02_QueryDecisions_Unauthenticated_Returns401()
        {
            using var anon = _factory.CreateClient();
            var resp = await anon.GetAsync($"{CdBase}/query");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "CD02: unauthenticated query must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD03: Query with orgId filter → Success=true
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD03_QueryDecisions_WithOrgIdFilter_ReturnsSuccess()
        {
            string orgId = $"org-cd03-{Guid.NewGuid():N}";
            var resp = await _client.GetAsync(
                $"{CdBase}/query?organizationId={Uri.EscapeDataString(orgId)}");
            resp.EnsureSuccessStatusCode();

            var result = await resp.Content.ReadFromJsonAsync<QueryComplianceDecisionsResponse>();
            Assert.That(result!.Success, Is.True, "CD03: query with orgId filter must succeed");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD04: Query with step=KycKybVerification filter → Success=true
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD04_QueryDecisions_WithStepFilter_ReturnsSuccess()
        {
            // OnboardingStep.KycKybVerification = 3
            var resp = await _client.GetAsync($"{CdBase}/query?step=3");
            resp.EnsureSuccessStatusCode();

            var result = await resp.Content.ReadFromJsonAsync<QueryComplianceDecisionsResponse>();
            Assert.That(result!.Success, Is.True,
                "CD04: query with step filter must return Success=true");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD05: Active decision for unknown org → 404
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD05_GetActiveDecision_UnknownOrg_Returns404()
        {
            string orgId = $"org-cd05-unknown-{Guid.NewGuid():N}";
            var resp = await _client.GetAsync(
                $"{CdBase}/active/{Uri.EscapeDataString(orgId)}/KycKybVerification");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "CD05: active decision for unknown org must return 404");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD06: Unauthenticated active decision check → 401 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD06_GetActiveDecision_Unauthenticated_Returns401()
        {
            using var anon = _factory.CreateClient();
            var resp = await anon.GetAsync($"{CdBase}/active/any-org/KycKybVerification");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "CD06: unauthenticated active decision check must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD07: POST create decision (JWT user without Algorand Address claim) → 401
        //       ComplianceDecisionController requires the ARC-0014 "Address" claim.
        //       JWT email/password users don't have this claim → fail-closed 401.
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD07_CreateDecision_JwtUserWithoutAddressClaim_Returns401()
        {
            var req = new CreateComplianceDecisionRequest
            {
                OrganizationId = $"org-cd07-{Guid.NewGuid():N}",
                Step           = OnboardingStep.KycKybVerification
            };
            var resp = await _client.PostAsJsonAsync(CdBase, req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "CD07: JWT user without Algorand Address claim must get 401 fail-closed on create");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD08: Unauthenticated create decision → 401 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD08_CreateDecision_Unauthenticated_Returns401()
        {
            using var anon = _factory.CreateClient();
            var req = new CreateComplianceDecisionRequest
            {
                OrganizationId = $"org-cd08-{Guid.NewGuid():N}",
                Step           = OnboardingStep.KycKybVerification
            };
            var resp = await anon.PostAsJsonAsync(CdBase, req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "CD08: unauthenticated create must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD09: GET review-required decisions → 200
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD09_GetReviewRequired_ReturnsOk()
        {
            var resp = await _client.GetAsync($"{CdBase}/review-required");
            resp.EnsureSuccessStatusCode();

            var content = await resp.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Null.And.Not.Empty,
                "CD09: review-required response must not be empty");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD10: Unauthenticated review-required → 401 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD10_GetReviewRequired_Unauthenticated_Returns401()
        {
            using var anon = _factory.CreateClient();
            var resp = await anon.GetAsync($"{CdBase}/review-required");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "CD10: unauthenticated review-required must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD11: GET expired decisions → 200
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD11_GetExpiredDecisions_ReturnsOk()
        {
            var resp = await _client.GetAsync($"{CdBase}/expired");
            resp.EnsureSuccessStatusCode();

            var content = await resp.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Null.And.Not.Empty,
                "CD11: expired decisions response must not be empty");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD12: Unauthenticated expired decisions → 401 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD12_GetExpiredDecisions_Unauthenticated_Returns401()
        {
            using var anon = _factory.CreateClient();
            var resp = await anon.GetAsync($"{CdBase}/expired");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "CD12: unauthenticated expired decisions must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD13: GET policy-rules/{step} → 200 with policy rules content
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD13_GetPolicyRules_KycStep_ReturnsOk()
        {
            var resp = await _client.GetAsync($"{CdBase}/policy-rules/KycKybVerification");
            resp.EnsureSuccessStatusCode();

            var content = await resp.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Null.And.Not.Empty,
                "CD13: policy-rules response must not be empty");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD14: Unauthenticated policy-rules → 401 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD14_GetPolicyRules_Unauthenticated_Returns401()
        {
            using var anon = _factory.CreateClient();
            var resp = await anon.GetAsync($"{CdBase}/policy-rules/KycKybVerification");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "CD14: unauthenticated policy-rules must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // CD15: Three consecutive query calls → deterministic Success=true
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CD15_QueryDecisions_ThreeConsecutiveCalls_Deterministic()
        {
            bool[] results = new bool[3];
            for (int i = 0; i < 3; i++)
            {
                var resp = await _client.GetAsync($"{CdBase}/query");
                resp.EnsureSuccessStatusCode();
                var result = await resp.Content.ReadFromJsonAsync<QueryComplianceDecisionsResponse>();
                results[i] = result!.Success;
            }

            Assert.That(results, Is.All.True,
                "CD15: three consecutive query calls must return deterministic Success=true");
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        private static async Task<string> ObtainJwtAsync(HttpClient client, string tag)
        {
            var resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email           = $"cd-dp-{tag}@cd-dp.biatec.example.com",
                Password        = "CdDpIT!Pass1",
                ConfirmPassword = "CdDpIT!Pass1",
                FullName        = $"CD DP Test ({tag})"
            });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Created));
            var doc   = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            string? t = doc?.RootElement.GetProperty("accessToken").GetString();
            Assert.That(t, Is.Not.Null.And.Not.Empty);
            return t!;
        }
    }
}
