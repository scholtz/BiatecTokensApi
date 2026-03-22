using BiatecTokensApi.Models.IssuancePolicy;
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
    /// Issuance Policy Deployed-Parity Tests — prove that the MICA-ready
    /// issuance compliance policy pipeline at <c>/api/v1/compliance/issuance-policies</c>
    /// is authoritative and ready for live operator workflows.
    ///
    /// Roadmap gaps addressed:
    ///   - KYC Integration (48%): Prove KYC-enforced policy lifecycle is deployed-parity
    ///   - EU MICA Full Compliance (26%): Jurisdiction-aware policy governance
    ///   - Whitelist Management (80%): Policy-driven whitelist enforcement lifecycle
    ///
    /// Coverage:
    /// IP01: Authenticated GET /issuance-policies → Success=true
    /// IP02: Unauthenticated GET → 401 fail-closed
    /// IP03: List policies response has Policies and TotalCount fields
    /// IP04: Create policy with valid request → Success=true
    /// IP05: Unauthenticated create policy → 401 fail-closed
    /// IP06: Create policy with empty PolicyName → 400 fail-closed
    /// IP07: Get policy by unknown id → 400 fail-closed
    /// IP08: Three consecutive list calls → deterministic Success=true
    /// IP09: Create policy with KYC required flag → Success=true
    /// IP10: Create policy with jurisdiction rules → Success=true
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class IssuancePolicyDeployedParityTests
    {
        private sealed class IpFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "IssuancePolicyDeployedParityTestKey!32",
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
                        ["KeyManagementConfig:HardcodedKey"] = "IssuancePolicyTestKey32Chars!!!!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "ip-dp-test",
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

        private IpFactory    _factory = null!;
        private HttpClient   _client  = null!;
        private const string IpBase = "/api/v1/compliance/issuance-policies";

        [SetUp]
        public async Task SetUp()
        {
            _factory = new IpFactory();
            _client  = _factory.CreateClient();
            string jwt = await ObtainJwtAsync(_client, $"ip-{Guid.NewGuid():N}");
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
        }

        [TearDown]
        public void TearDown() { _client.Dispose(); _factory.Dispose(); }

        [Test]
        public async Task IP01_ListPolicies_Authenticated_ReturnsSuccess()
        {
            var resp = await _client.GetAsync(IpBase);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<IssuancePolicyListResponse>();
            Assert.That(result!.Success, Is.True, "IP01: list policies must return Success=true");
        }

        [Test]
        public async Task IP02_ListPolicies_Unauthenticated_Returns401()
        {
            using var anon = _factory.CreateClient();
            var resp = await anon.GetAsync(IpBase);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "IP02: unauthenticated list must return 401");
        }

        [Test]
        public async Task IP03_ListPolicies_ResponseHasPoliciesAndTotalCount()
        {
            var resp = await _client.GetAsync(IpBase);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<IssuancePolicyListResponse>();
            Assert.That(result!.Policies, Is.Not.Null, "IP03: Policies must not be null");
            Assert.That(result.TotalCount, Is.GreaterThanOrEqualTo(0), "IP03: TotalCount must be >= 0");
        }

        [Test]
        public async Task IP04_CreatePolicy_ValidRequest_ReturnsSuccess()
        {
            var req = new CreateIssuancePolicyRequest
            {
                AssetId    = 12345678,
                PolicyName = $"IP04-Test-{Guid.NewGuid():N}",
                KycRequired = false,
                IsActive   = true
            };
            var resp = await _client.PostAsJsonAsync(IpBase, req);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            Assert.That(result!.Success, Is.True, "IP04: create policy must return Success=true");
        }

        [Test]
        public async Task IP05_CreatePolicy_Unauthenticated_Returns401()
        {
            using var anon = _factory.CreateClient();
            var req = new CreateIssuancePolicyRequest { AssetId = 1, PolicyName = "unauth" };
            var resp = await anon.PostAsJsonAsync(IpBase, req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "IP05: unauthenticated create must return 401");
        }

        [Test]
        public async Task IP06_CreatePolicy_EmptyPolicyName_Returns400()
        {
            var req = new CreateIssuancePolicyRequest { AssetId = 1, PolicyName = string.Empty };
            var resp = await _client.PostAsJsonAsync(IpBase, req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "IP06: empty PolicyName must return 400 fail-closed");
        }

        [Test]
        public async Task IP07_GetPolicy_UnknownId_Returns400()
        {
            var resp = await _client.GetAsync($"{IpBase}/unknown-policy-{Guid.NewGuid():N}");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "IP07: unknown policyId must return 400");
        }

        [Test]
        public async Task IP08_ListPolicies_ThreeConsecutiveCalls_Deterministic()
        {
            bool[] results = new bool[3];
            for (int i = 0; i < 3; i++)
            {
                var resp = await _client.GetAsync(IpBase);
                resp.EnsureSuccessStatusCode();
                results[i] = (await resp.Content.ReadFromJsonAsync<IssuancePolicyListResponse>())!.Success;
            }
            Assert.That(results, Is.All.True, "IP08: three consecutive calls must return Success=true");
        }

        [Test]
        public async Task IP09_CreatePolicy_KycRequired_ReturnsSuccess()
        {
            var req = new CreateIssuancePolicyRequest
            {
                AssetId     = 99991234,
                PolicyName  = $"IP09-KYC-{Guid.NewGuid():N}",
                KycRequired = true,
                IsActive    = true
            };
            var resp = await _client.PostAsJsonAsync(IpBase, req);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            Assert.That(result!.Success, Is.True, "IP09: create KYC policy must succeed");
        }

        [Test]
        public async Task IP10_CreatePolicy_JurisdictionRules_ReturnsSuccess()
        {
            var req = new CreateIssuancePolicyRequest
            {
                AssetId              = 88887777,
                PolicyName           = $"IP10-Jurisdiction-{Guid.NewGuid():N}",
                AllowedJurisdictions = new List<string> { "DE", "AT", "CH" },
                BlockedJurisdictions = new List<string> { "US", "KP" },
                KycRequired          = true,
                IsActive             = true
            };
            var resp = await _client.PostAsJsonAsync(IpBase, req);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<IssuancePolicyResponse>();
            Assert.That(result!.Success, Is.True, "IP10: create jurisdiction-aware policy must succeed");
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string tag)
        {
            var resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email = $"ip-dp-{tag}@ip-dp.biatec.example.com",
                Password = "IpDpIT!Pass1", ConfirmPassword = "IpDpIT!Pass1",
                FullName = $"IP DP Test ({tag})"
            });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Created));
            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            string? t = doc?.RootElement.GetProperty("accessToken").GetString();
            Assert.That(t, Is.Not.Null.And.Not.Empty);
            return t!;
        }
    }
}
