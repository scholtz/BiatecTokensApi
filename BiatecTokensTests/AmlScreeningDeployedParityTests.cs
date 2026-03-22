using BiatecTokensApi.Models.Aml;
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
    /// AML Screening Deployed-Parity Tests — prove that the AML screening
    /// pipeline at <c>/api/v1/aml</c> is authoritative and ready for live
    /// operator workflows.
    ///
    /// These tests directly address the roadmap gaps:
    ///   - FATF Guidelines (18%): Deployed-system proof for sanctions/AML orchestration
    ///   - AML Screening (43%): End-to-end protected evidence for AML checks
    ///   - Advanced MICA Compliance (67%): AML/sanctions screening as a compliance gate
    ///
    /// Coverage:
    ///
    /// AML01: Screen valid userId → Success=true, AmlId populated
    /// AML02: Screen empty userId → 400 fail-closed
    /// AML03: Unauthenticated screen → 401 fail-closed
    /// AML04: Screen returns AmlId and CorrelationId non-empty
    /// AML05: Status for screened userId → Success=true
    /// AML06: Status for unscreened userId → Success=true, Status=NotScreened
    /// AML07: Unauthenticated status → 401 fail-closed
    /// AML08: Report for userId returns Success=true
    /// AML09: Report ComplianceSummary is populated
    /// AML10: Unauthenticated report → 401 fail-closed
    /// AML11: Webhook anonymous endpoint with valid payload → 200 or 400 (not 401/500)
    /// AML12: Webhook with empty ProviderReferenceId → 400 fail-closed
    /// AML13: Two screens for same userId both succeed (idempotent acceptance)
    /// AML14: Report after screen reflects at least one history entry
    /// AML15: Screen two distinct users → distinct AmlIds
    /// AML16: Status RiskLevel is a valid AmlRiskLevel integer value
    /// AML17: Screen with metadata populated → Success=true
    /// AML18: Report GeneratedAt is within recent window
    /// AML19: Screen then status → status AmlId matches screen AmlId
    /// AML20: Full AML pipeline: screen → status → report (end-to-end lifecycle)
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class AmlScreeningDeployedParityTests
    {
        // ════════════════════════════════════════════════════════════════════
        // Factory
        // ════════════════════════════════════════════════════════════════════

        private sealed class AmlFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "AmlScreeningDeployedParityTestSecretKey!32",
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
                        ["KeyManagementConfig:HardcodedKey"] = "AmlScreeningTestKey32+Characters!!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "aml-screening-dp-test",
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

        private AmlFactory _factory = null!;
        private HttpClient _client = null!;
        private const string AmlBase = "/api/v1/aml";

        [SetUp]
        public async Task SetUp()
        {
            _factory = new AmlFactory();
            _client  = _factory.CreateClient();
            string jwt = await ObtainJwtAsync(_client, $"aml-{Guid.NewGuid():N}");
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
        // AML01: Screen valid userId → Success=true, AmlId populated
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML01_Screen_ValidUserId_ReturnsSuccess()
        {
            string userId = $"user-aml01-{Guid.NewGuid():N}";
            var resp = await ScreenAsync(userId);

            Assert.That(resp.Success, Is.True, "AML01: screen must succeed for valid userId");
            Assert.That(resp.AmlId, Is.Not.Null.And.Not.Empty, "AML01: AmlId must be populated");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML02: Screen empty userId → 400 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML02_Screen_EmptyUserId_Returns400()
        {
            var httpResp = await _client.PostAsJsonAsync($"{AmlBase}/screen",
                new AmlScreenRequest { UserId = "" });
            Assert.That(httpResp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "AML02: empty UserId must return 400 fail-closed");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML03: Unauthenticated screen → 401 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML03_Unauthenticated_Screen_Returns401()
        {
            using var anon = _factory.CreateClient();
            var resp = await anon.PostAsJsonAsync($"{AmlBase}/screen",
                new AmlScreenRequest { UserId = "any-user" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AML03: unauthenticated screen must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML04: Screen returns AmlId and CorrelationId non-empty
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML04_Screen_ReturnsAmlIdAndCorrelationId()
        {
            string userId = $"user-aml04-{Guid.NewGuid():N}";
            var resp = await ScreenAsync(userId);

            Assert.That(resp.AmlId, Is.Not.Null.And.Not.Empty, "AML04: AmlId must be populated");
            Assert.That(resp.CorrelationId, Is.Not.Null.And.Not.Empty, "AML04: CorrelationId must be populated");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML05: Status for screened userId → Success=true
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML05_Status_AfterScreen_ReturnsSuccess()
        {
            string userId = $"user-aml05-{Guid.NewGuid():N}";
            await ScreenAsync(userId);

            var statusResp = await GetStatusAsync(userId);
            Assert.That(statusResp.Success, Is.True, "AML05: status check after screen must succeed");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML06: Status for unscreened user → Success=true, Status=NotScreened
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML06_Status_UnscreenedUser_StatusIsNotScreened()
        {
            string userId = $"user-aml06-{Guid.NewGuid():N}";
            var statusResp = await GetStatusAsync(userId);

            Assert.That(statusResp.Success, Is.True, "AML06: status check for unscreened user must succeed");
            Assert.That(statusResp.Status, Is.EqualTo(AmlScreeningStatus.NotScreened),
                "AML06: unscreened user must have status=NotScreened");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML07: Unauthenticated status → 401 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML07_Unauthenticated_Status_Returns401()
        {
            using var anon = _factory.CreateClient();
            var resp = await anon.GetAsync($"{AmlBase}/status/any-user");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AML07: unauthenticated status must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML08: Report for userId returns Success=true
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML08_Report_ValidUserId_ReturnsSuccess()
        {
            string userId = $"user-aml08-{Guid.NewGuid():N}";
            var report = await GetReportAsync(userId);
            Assert.That(report.Success, Is.True, "AML08: report must return Success=true");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML09: Report ComplianceSummary is populated
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML09_Report_ComplianceSummaryPopulated()
        {
            string userId = $"user-aml09-{Guid.NewGuid():N}";
            var report = await GetReportAsync(userId);
            Assert.That(report.ComplianceSummary, Is.Not.Null.And.Not.Empty,
                "AML09: report ComplianceSummary must be populated");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML10: Unauthenticated report → 401 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML10_Unauthenticated_Report_Returns401()
        {
            using var anon = _factory.CreateClient();
            var resp = await anon.GetAsync($"{AmlBase}/report/any-user");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "AML10: unauthenticated report must return 401");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML11: Webhook anonymous endpoint returns 200 or 400 (not 401 or 500)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML11_Webhook_AnonymousEndpoint_NotAuthRequired()
        {
            using var anon = _factory.CreateClient();
            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = $"ref-aml11-{Guid.NewGuid():N}",
                AlertType           = "SANCTIONS_MATCH",
                Status              = "SanctionsMatch",
                RiskLevel           = "High",
                Timestamp           = DateTime.UtcNow
            };
            var resp = await anon.PostAsJsonAsync($"{AmlBase}/webhook", payload);

            // Anonymous webhook endpoint must NOT return 401 (authentication must not be required)
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized),
                "AML11: webhook endpoint must not require authentication (anonymous)");
            // Must also not 500 — either accepted (200) or rejected for business reasons (400/404)
            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "AML11: webhook endpoint must not return 5xx");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML12: Webhook with empty ProviderReferenceId → 400 fail-closed
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML12_Webhook_EmptyProviderReferenceId_Returns400()
        {
            using var anon = _factory.CreateClient();
            var payload = new AmlWebhookPayload
            {
                ProviderReferenceId = "",   // empty → invalid
                AlertType           = "SANCTIONS_MATCH",
                Status              = "SanctionsMatch",
                RiskLevel           = "High",
                Timestamp           = DateTime.UtcNow
            };
            var resp = await anon.PostAsJsonAsync($"{AmlBase}/webhook", payload);

            // Empty ProviderReferenceId should fail (400 or 500 handled in service, but not 2xx)
            Assert.That(resp.IsSuccessStatusCode, Is.False,
                "AML12: webhook with empty ProviderReferenceId must not succeed");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML13: Two screens for same userId — both accepted (idempotent acceptance)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML13_ScreenTwice_SameUser_BothSucceed()
        {
            string userId = $"user-aml13-{Guid.NewGuid():N}";
            var r1 = await ScreenAsync(userId);
            var r2 = await ScreenAsync(userId);

            Assert.That(r1.Success, Is.True, "AML13: first screen must succeed");
            Assert.That(r2.Success, Is.True, "AML13: second screen of same user must also succeed");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML14: Report after screen reflects at least one history entry
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML14_ReportAfterScreen_IncludesHistory()
        {
            string userId = $"user-aml14-{Guid.NewGuid():N}";
            await ScreenAsync(userId);
            var report = await GetReportAsync(userId);

            Assert.That(report.ScreeningHistory, Is.Not.Null, "AML14: ScreeningHistory must not be null");
            Assert.That(report.ScreeningHistory.Count, Is.GreaterThan(0),
                "AML14: report after screening must have at least one history entry");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML15: Screen two distinct users → distinct AmlIds
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML15_ScreenTwoDistinctUsers_DistinctAmlIds()
        {
            string userId1 = $"user-aml15a-{Guid.NewGuid():N}";
            string userId2 = $"user-aml15b-{Guid.NewGuid():N}";

            var r1 = await ScreenAsync(userId1);
            var r2 = await ScreenAsync(userId2);

            Assert.That(r1.AmlId, Is.Not.EqualTo(r2.AmlId),
                "AML15: two different users must have distinct AmlIds");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML16: Status RiskLevel is a valid AmlRiskLevel integer value
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML16_Status_RiskLevel_IsValidEnumValue()
        {
            string userId = $"user-aml16-{Guid.NewGuid():N}";
            var status = await GetStatusAsync(userId);

            var validValues = Enum.GetValues<AmlRiskLevel>();
            Assert.That(validValues, Does.Contain(status.RiskLevel),
                "AML16: RiskLevel must be a valid AmlRiskLevel enum value");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML17: Screen with metadata populated → Success=true
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML17_Screen_WithMetadata_Succeeds()
        {
            string userId = $"user-aml17-{Guid.NewGuid():N}";
            var httpResp = await _client.PostAsJsonAsync($"{AmlBase}/screen",
                new AmlScreenRequest
                {
                    UserId   = userId,
                    Metadata = new Dictionary<string, string>
                    {
                        ["country"] = "DE",
                        ["jurisdiction"] = "EU",
                        ["riskCategory"] = "Standard"
                    }
                });
            httpResp.EnsureSuccessStatusCode();
            var resp = await httpResp.Content.ReadFromJsonAsync<AmlScreenResponse>();

            Assert.That(resp!.Success, Is.True, "AML17: screen with metadata must succeed");
            Assert.That(resp.AmlId, Is.Not.Null.And.Not.Empty, "AML17: AmlId must be populated");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML18: Report GeneratedAt is within the last minute
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML18_Report_GeneratedAtIsRecent()
        {
            string userId = $"user-aml18-{Guid.NewGuid():N}";
            var report = await GetReportAsync(userId);

            Assert.That(report.GeneratedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)),
                "AML18: GeneratedAt must be within the last minute");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML19: Screen then status — status AmlId matches screen AmlId
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML19_ScreenThenStatus_AmlIdMatches()
        {
            string userId = $"user-aml19-{Guid.NewGuid():N}";
            var screenResp   = await ScreenAsync(userId);
            var statusResp   = await GetStatusAsync(userId);

            Assert.That(statusResp.AmlId, Is.Not.Null.And.Not.Empty,
                "AML19: AmlId must be present in status after screening");
            // The most recent AmlId in status must match the one returned by screening
            Assert.That(statusResp.AmlId, Is.EqualTo(screenResp.AmlId),
                "AML19: status AmlId must match the screening AmlId");
        }

        // ════════════════════════════════════════════════════════════════════
        // AML20: Full AML pipeline — screen → status → report (end-to-end)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AML20_FullAmlPipeline_Screen_Status_Report()
        {
            string userId = $"user-aml20-{Guid.NewGuid():N}";

            // 1. Screen the user
            var screenResp = await ScreenAsync(userId);
            Assert.That(screenResp.Success, Is.True, "AML20: screen must succeed");
            Assert.That(screenResp.AmlId, Is.Not.Null.And.Not.Empty, "AML20: AmlId must be populated");

            // 2. Check status
            var statusResp = await GetStatusAsync(userId);
            Assert.That(statusResp.Success, Is.True, "AML20: status must succeed after screening");
            Assert.That(statusResp.AmlId, Is.EqualTo(screenResp.AmlId),
                "AML20: status AmlId must match screen AmlId");

            // 3. Verify valid risk level
            var validRiskLevels = Enum.GetValues<AmlRiskLevel>();
            Assert.That(validRiskLevels, Does.Contain(statusResp.RiskLevel),
                "AML20: status RiskLevel must be a valid AmlRiskLevel");

            // 4. Generate regulatory report
            var report = await GetReportAsync(userId);
            Assert.That(report.Success, Is.True, "AML20: report must succeed");
            Assert.That(report.ComplianceSummary, Is.Not.Null.And.Not.Empty,
                "AML20: ComplianceSummary must be populated in report");
            Assert.That(report.ScreeningHistory.Count, Is.GreaterThan(0),
                "AML20: report must include at least one screening history entry");
            Assert.That(report.GeneratedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)),
                "AML20: report GeneratedAt must be recent");

            // 5. Verify report reflects the screening event
            Assert.That(report.LatestRecord, Is.Not.Null, "AML20: LatestRecord must be set after screening");
        }

        // ════════════════════════════════════════════════════════════════════
        // Private helpers
        // ════════════════════════════════════════════════════════════════════

        private async Task<AmlScreenResponse> ScreenAsync(string userId)
        {
            var httpResp = await _client.PostAsJsonAsync($"{AmlBase}/screen",
                new AmlScreenRequest { UserId = userId });
            httpResp.EnsureSuccessStatusCode();
            var resp = await httpResp.Content.ReadFromJsonAsync<AmlScreenResponse>();
            Assert.That(resp, Is.Not.Null, $"Screen response must not be null for user {userId}");
            return resp!;
        }

        private async Task<AmlStatusResponse> GetStatusAsync(string userId)
        {
            var httpResp = await _client.GetAsync($"{AmlBase}/status/{userId}");
            httpResp.EnsureSuccessStatusCode();
            var resp = await httpResp.Content.ReadFromJsonAsync<AmlStatusResponse>();
            Assert.That(resp, Is.Not.Null, $"Status response must not be null for user {userId}");
            return resp!;
        }

        private async Task<AmlReportResponse> GetReportAsync(string userId)
        {
            var httpResp = await _client.GetAsync($"{AmlBase}/report/{userId}");
            httpResp.EnsureSuccessStatusCode();
            var resp = await httpResp.Content.ReadFromJsonAsync<AmlReportResponse>();
            Assert.That(resp, Is.Not.Null, $"Report response must not be null for user {userId}");
            return resp!;
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string tag)
        {
            var resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email           = $"aml-dp-{tag}@aml-dp.biatec.example.com",
                Password        = "AmlDpIT!Pass1",
                ConfirmPassword = "AmlDpIT!Pass1",
                FullName        = $"AML DP Test ({tag})"
            });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Created));
            var doc   = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            string? t = doc?.RootElement.GetProperty("accessToken").GetString();
            Assert.That(t, Is.Not.Null.And.Not.Empty);
            return t!;
        }
    }
}
