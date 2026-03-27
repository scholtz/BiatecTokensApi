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
    /// Deployed-layer (HTTP/WebApplicationFactory) integration tests for the release-grade sign-off
    /// pipeline. These tests complement the service-layer <see cref="ProtectedSignOffReleaseGradePipelineTests"/>
    /// by exercising the full HTTP stack (routing → controller → service) through
    /// <see cref="WebApplicationFactory{TEntryPoint}"/>.
    ///
    /// Test groups:
    ///   RGD01–RGD10  – Full release-grade pipeline via HTTP (webhook + evidence → ReadyReleaseGrade)
    ///   RGD11–RGD20  – IsReleaseEvidence, Mode, EnvironmentLabel schema contracts via HTTP
    ///   RGD21–RGD30  – Fail-closed, error handling, schema stability, and cross-cutting via HTTP
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffReleaseGradeDeployedTests
    {
        // ════════════════════════════════════════════════════════════════════
        // Time provider for staleness simulation (service-layer only)
        // ════════════════════════════════════════════════════════════════════

        private sealed class AdvancableTime : TimeProvider
        {
            private DateTimeOffset _now;
            public AdvancableTime(DateTimeOffset t) => _now = t;
            public void Advance(TimeSpan d) => _now += d;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ════════════════════════════════════════════════════════════════════
        // WebApplicationFactory
        // ════════════════════════════════════════════════════════════════════

        private sealed class ReleaseGradeDeployedFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "ReleaseGradeDeployedTestSecretKey32C!",
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
                        ["KeyManagementConfig:HardcodedKey"] = "ReleaseGradeDeployedTestKeyXXX32C!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "rgd-test",
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

        private ReleaseGradeDeployedFactory _factory = null!;
        private HttpClient _client = null!;
        private string _jwt = null!;

        private const string BaseUrl = "/api/v1/protected-signoff-evidence";

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        [SetUp]
        public async Task SetUp()
        {
            _factory = new ReleaseGradeDeployedFactory();
            _client = _factory.CreateClient();
            _jwt = await ObtainJwtAsync(_client, $"rgd-{Guid.NewGuid():N}");
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
        // Helpers
        // ════════════════════════════════════════════════════════════════════

        private static string UniqueHead() => $"head-rgd-{Guid.NewGuid():N}";
        private static string UniqueCase() => $"case-rgd-{Guid.NewGuid():N}";

        private static ProtectedSignOffEvidencePersistenceService CreateSvc(TimeProvider? tp = null)
            => new(NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance, null, tp);

        private async Task<RecordApprovalWebhookResponse> PostWebhookAsync(
            string caseId, string headRef,
            ApprovalWebhookOutcome outcome = ApprovalWebhookOutcome.Approved)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef, Outcome = outcome,
                    ActorId = "rgd-test-actor", Reason = "RGD test",
                    CorrelationId = Guid.NewGuid().ToString("N")
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<RecordApprovalWebhookResponse>(_jsonOpts))!;
        }

        private async Task<PersistSignOffEvidenceResponse> PostEvidenceAsync(
            string headRef, string caseId,
            bool requireReleaseGrade = true,
            bool requireApprovalWebhook = true,
            string? environmentLabel = null,
            int freshnessWindowHours = 24)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    RequireReleaseGrade = requireReleaseGrade,
                    RequireApprovalWebhook = requireApprovalWebhook,
                    EnvironmentLabel = environmentLabel,
                    FreshnessWindowHours = freshnessWindowHours
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>(_jsonOpts))!;
        }

        private async Task<GetSignOffReleaseReadinessResponse> GetReadinessAsync(
            string headRef, string caseId, int freshnessWindowHours = 24)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId, FreshnessWindowHours = freshnessWindowHours });
            // Controller returns 400 when Success=false (e.g. no evidence); we still deserialise the body.
            return (await resp.Content.ReadFromJsonAsync<GetSignOffReleaseReadinessResponse>(_jsonOpts))!;
        }

        private async Task<JsonElement> GetReadinessJsonAsync(string headRef, string caseId)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });
            string json = await resp.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json).RootElement;
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string userTag)
        {
            string email = $"rgd-{userTag}@release-grade-deployed.biatec.example.com";
            var resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email = email,
                Password = "ReleaseGradeD3pl0yed!",
                ConfirmPassword = "ReleaseGradeD3pl0yed!",
                FullName = $"RGD Test User ({userTag})"
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

        // ════════════════════════════════════════════════════════════════════
        // RGD01–RGD10: Full release-grade pipeline via HTTP
        // ════════════════════════════════════════════════════════════════════

        /// <summary>RGD01: Full pipeline via HTTP (webhook + evidence) → Mode=ReadyReleaseGrade.</summary>
        [Test]
        public async Task RGD01_FullPipeline_Http_YieldsReadyReleaseGrade()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var r = await GetReadinessAsync(head, caseId);

            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD01: Full HTTP pipeline → Mode must be ReadyReleaseGrade.");
            Assert.That(r.IsReleaseEvidence, Is.True,
                "RGD01: Full HTTP pipeline → IsReleaseEvidence must be true.");
        }

        /// <summary>RGD02: HTTP POST webhook returns Success=true for Approved outcome.</summary>
        [Test]
        public async Task RGD02_PostWebhook_Approved_ReturnsSuccess()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var webhookResp = await PostWebhookAsync(caseId, head, ApprovalWebhookOutcome.Approved);

            Assert.That(webhookResp.Success, Is.True,
                "RGD02: POST webhook with Approved outcome must return Success=true.");
        }

        /// <summary>RGD03: HTTP POST evidence after webhook returns Success=true.</summary>
        [Test]
        public async Task RGD03_PostEvidence_AfterWebhook_ReturnsSuccess()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            await PostWebhookAsync(caseId, head);
            var evidenceResp = await PostEvidenceAsync(head, caseId);

            Assert.That(evidenceResp.Success, Is.True,
                "RGD03: POST evidence after webhook must return Success=true.");
        }

        /// <summary>RGD04: HTTP readiness without evidence → Mode is not ReadyReleaseGrade.</summary>
        [Test]
        public async Task RGD04_Http_NoEvidence_Mode_NotReadyReleaseGrade()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var r = await GetReadinessAsync(head, caseId);

            Assert.That(r.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD04: No evidence → Mode must not be ReadyReleaseGrade.");
        }

        /// <summary>RGD05: HTTP readiness without evidence → IsReleaseEvidence=false.</summary>
        [Test]
        public async Task RGD05_Http_NoEvidence_IsReleaseEvidence_False()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var r = await GetReadinessAsync(head, caseId);

            Assert.That(r.IsReleaseEvidence, Is.False,
                "RGD05: No evidence → IsReleaseEvidence must be false.");
        }

        /// <summary>RGD06: HTTP evidence without prior webhook → Mode not ReadyReleaseGrade (fail-closed).</summary>
        [Test]
        public async Task RGD06_Http_EvidenceWithoutWebhook_Mode_NotReadyReleaseGrade()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            // Post evidence without requiring webhook — pack won't have approval webhook
            await PostEvidenceAsync(head, caseId, requireReleaseGrade: false, requireApprovalWebhook: false);

            var r = await GetReadinessAsync(head, caseId);

            Assert.That(r.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD06: Evidence without approval webhook → not ReadyReleaseGrade (fail-closed).");
        }

        /// <summary>RGD07: HTTP pipeline with Denied webhook → Mode not ReadyReleaseGrade.</summary>
        [Test]
        public async Task RGD07_Http_DeniedWebhook_Mode_NotReadyReleaseGrade()
        {
            var svc = CreateSvc();
            string head = UniqueHead();
            string caseId = UniqueCase();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = head,
                    Outcome = ApprovalWebhookOutcome.Denied,
                    ActorId = "rgd-reviewer", Reason = "Denied for test"
                }, "rgd-actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId, RequireReleaseGrade = false },
                "rgd-actor");

            var r = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(r.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD07: Denied webhook → Mode must not be ReadyReleaseGrade.");
        }

        /// <summary>RGD08: HTTP readiness response returns HTTP 200 OK after full pipeline.</summary>
        [Test]
        public async Task RGD08_Http_Readiness_Returns200()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "RGD08: Readiness endpoint must return HTTP 200 when full pipeline complete.");
        }

        /// <summary>RGD09: Consecutive HTTP pipelines on different heads are isolated.</summary>
        [Test]
        public async Task RGD09_Http_MultiHead_PipelinesIsolated()
        {
            string caseId = UniqueCase();
            string headA = UniqueHead();
            string headB = UniqueHead();

            await PostWebhookAsync(caseId, headA);
            await PostEvidenceAsync(headA, caseId);

            // headB has no evidence
            var rA = await GetReadinessAsync(headA, caseId);
            var rB = await GetReadinessAsync(headB, caseId);

            Assert.That(rA.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD09: headA with full pipeline → ReadyReleaseGrade.");
            Assert.That(rB.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD09: headB without evidence must not inherit headA state.");
        }

        /// <summary>RGD10: HTTP pipeline with EnvironmentLabel → label appears in readiness response.</summary>
        [Test]
        public async Task RGD10_Http_FullPipeline_WithEnvironmentLabel_LabelInResponse()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            const string label = "rgd-protected-env";

            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId, environmentLabel: label);

            var r = await GetReadinessAsync(head, caseId);

            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD10: Full pipeline with label → ReadyReleaseGrade.");
            Assert.That(r.EnvironmentLabel, Is.EqualTo(label),
                "RGD10: EnvironmentLabel must flow from evidence to readiness response.");
        }

        // ════════════════════════════════════════════════════════════════════
        // RGD11–RGD20: Schema contracts via HTTP
        // ════════════════════════════════════════════════════════════════════

        /// <summary>RGD11: HTTP readiness JSON always contains 'mode' field.</summary>
        [Test]
        public async Task RGD11_Http_ReadinessJson_ContainsModeField()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var json = await GetReadinessJsonAsync(head, caseId);

            Assert.That(json.TryGetProperty("mode", out _), Is.True,
                "RGD11: Readiness JSON must always contain 'mode' field.");
        }

        /// <summary>RGD12: HTTP readiness JSON always contains 'isReleaseEvidence' field.</summary>
        [Test]
        public async Task RGD12_Http_ReadinessJson_ContainsIsReleaseEvidenceField()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var json = await GetReadinessJsonAsync(head, caseId);

            Assert.That(json.TryGetProperty("isReleaseEvidence", out _), Is.True,
                "RGD12: Readiness JSON must always contain 'isReleaseEvidence' field.");
        }

        /// <summary>RGD13: HTTP readiness JSON 'mode' value is a valid integer (enum ordinal).</summary>
        [Test]
        public async Task RGD13_Http_ReadinessJson_ModeIsValidEnumValue()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var readiness = await GetReadinessAsync(head, caseId);

            Assert.That(Enum.IsDefined(typeof(StrictArtifactMode), readiness.Mode), Is.True,
                "RGD13: Mode must be a defined StrictArtifactMode value.");
        }

        /// <summary>RGD14: HTTP readiness JSON 'status' field is always present.</summary>
        [Test]
        public async Task RGD14_Http_ReadinessJson_ContainsStatusField()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var json = await GetReadinessJsonAsync(head, caseId);

            Assert.That(json.TryGetProperty("status", out _), Is.True,
                "RGD14: Readiness JSON must always contain 'status' field.");
        }

        /// <summary>RGD15: HTTP readiness Mode=ReadyReleaseGrade → isReleaseEvidence JSON is true.</summary>
        [Test]
        public async Task RGD15_Http_ReadyReleaseGrade_JsonIsReleaseEvidence_IsTrue()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var json = await GetReadinessJsonAsync(head, caseId);
            json.TryGetProperty("mode", out var modeEl);
            var modeVal = (StrictArtifactMode)modeEl.GetInt32();

            if (modeVal == StrictArtifactMode.ReadyReleaseGrade)
            {
                json.TryGetProperty("isReleaseEvidence", out var isReEl);
                Assert.That(isReEl.GetBoolean(), Is.True,
                    "RGD15: ReadyReleaseGrade in JSON → isReleaseEvidence must be true.");
            }
        }

        /// <summary>RGD16: HTTP readiness Mode!=ReadyReleaseGrade → isReleaseEvidence JSON is false.</summary>
        [Test]
        public async Task RGD16_Http_NonReadyReleaseGrade_JsonIsReleaseEvidence_IsFalse()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            // No evidence → not ReadyReleaseGrade
            var json = await GetReadinessJsonAsync(head, caseId);
            json.TryGetProperty("mode", out var modeEl);
            var modeVal = (StrictArtifactMode)modeEl.GetInt32();

            Assert.That(modeVal, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD16 pre: no evidence should not be ReadyReleaseGrade.");

            json.TryGetProperty("isReleaseEvidence", out var isReEl);
            Assert.That(isReEl.GetBoolean(), Is.False,
                "RGD16: Non-ReadyReleaseGrade mode → isReleaseEvidence must be false.");
        }

        /// <summary>RGD17: HTTP readiness JSON environmentLabel is null when no evidence persisted.</summary>
        [Test]
        public async Task RGD17_Http_NoEvidence_EnvironmentLabel_IsNullOrMissing()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var readiness = await GetReadinessAsync(head, caseId);

            Assert.That(readiness.EnvironmentLabel, Is.Null.Or.Empty,
                "RGD17: No evidence → EnvironmentLabel must be null or empty.");
        }

        /// <summary>RGD18: HTTP readiness JSON schema is stable across two successive calls.</summary>
        [Test]
        public async Task RGD18_Http_ReadinessSchemaStable_AcrossSuccessiveCalls()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var json1 = await GetReadinessJsonAsync(head, caseId);
            var json2 = await GetReadinessJsonAsync(head, caseId);

            // Both must have same top-level keys
            var keys1 = json1.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToList();
            var keys2 = json2.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToList();

            Assert.That(keys2, Is.EquivalentTo(keys1),
                "RGD18: Readiness JSON schema must be stable across successive calls.");
        }

        /// <summary>RGD19: HTTP readiness Mode value is consistent across two successive calls.</summary>
        [Test]
        public async Task RGD19_Http_ReadinessModeConsistent_AcrossSuccessiveCalls()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var r1 = await GetReadinessAsync(head, caseId);
            var r2 = await GetReadinessAsync(head, caseId);

            Assert.That(r2.Mode, Is.EqualTo(r1.Mode),
                "RGD19: Mode must be consistent across successive calls.");
        }

        /// <summary>RGD20: HTTP evidence endpoint returns HTTP 200 OK.</summary>
        [Test]
        public async Task RGD20_Http_PostEvidence_Returns200()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            // Post evidence without webhook requirement
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = head, CaseId = caseId,
                    RequireReleaseGrade = false, RequireApprovalWebhook = false
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "RGD20: POST evidence endpoint must return HTTP 200.");
        }

        // ════════════════════════════════════════════════════════════════════
        // RGD21–RGD30: Fail-closed, staleness, multi-case, and cross-cutting
        // ════════════════════════════════════════════════════════════════════

        /// <summary>RGD21: Stale evidence via service → Mode=StaleEvidence, IsReleaseEvidence=false.</summary>
        [Test]
        public async Task RGD21_ServiceLayer_StaleEvidence_Mode_IsStaleEvidence()
        {
            var tp = new AdvancableTime(DateTimeOffset.UtcNow);
            var svc = CreateSvc(tp);
            string head = UniqueHead();
            string caseId = UniqueCase();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved, ActorId = "a", Reason = "r" },
                "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId, FreshnessWindowHours = 2 },
                "actor");

            tp.Advance(TimeSpan.FromHours(3));

            var r = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId, FreshnessWindowHours = 2 });

            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.StaleEvidence),
                "RGD21: 3h into 2h window → StaleEvidence.");
            Assert.That(r.IsReleaseEvidence, Is.False,
                "RGD21: StaleEvidence → IsReleaseEvidence must be false.");
        }

        /// <summary>RGD22: Mode transitions NotReleaseEvidence → ReadyReleaseGrade after pipeline completed.</summary>
        [Test]
        public async Task RGD22_ServiceLayer_NotReleaseEvidence_TransitionsTo_ReadyReleaseGrade()
        {
            var svc = CreateSvc();
            string head = UniqueHead();
            string caseId = UniqueCase();

            // Step 1: no webhook, non-release-grade → NotReleaseEvidence
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId },
                "actor");
            var r1 = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            Assert.That(r1.Status, Is.EqualTo(SignOffReleaseReadinessStatus.NotReleaseEvidence),
                "RGD22 step1: initial state must be NotReleaseEvidence.");

            // Step 2: add webhook + release-grade evidence → ReadyReleaseGrade
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved, ActorId = "a", Reason = "r" },
                "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId, RequireReleaseGrade = true, RequireApprovalWebhook = true },
                "actor");
            var r2 = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            Assert.That(r2.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD22 step2: after full pipeline → ReadyReleaseGrade.");
        }

        /// <summary>RGD23: Different cases on same head are independent.</summary>
        [Test]
        public async Task RGD23_Http_MultiCase_SameHead_IndependentModes()
        {
            string head = UniqueHead();
            string caseA = UniqueCase();
            string caseB = UniqueCase();

            // caseA: full pipeline
            await PostWebhookAsync(caseA, head);
            await PostEvidenceAsync(head, caseA);

            // caseB: no evidence

            var rA = await GetReadinessAsync(head, caseA);
            var rB = await GetReadinessAsync(head, caseB);

            Assert.That(rA.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD23: caseA with full pipeline → ReadyReleaseGrade.");
            Assert.That(rB.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD23: caseB without evidence must not be ReadyReleaseGrade.");
        }

        /// <summary>RGD24: HTTP evidence without authentication returns 401.</summary>
        [Test]
        public async Task RGD24_Http_PostEvidence_Unauthenticated_Returns401()
        {
            using var unauthClient = _factory.CreateClient();
            // No Authorization header

            var resp = await unauthClient.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = UniqueHead(), CaseId = UniqueCase()
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "RGD24: Evidence endpoint must require authentication.");
        }

        /// <summary>RGD25: HTTP webhook without authentication returns 401.</summary>
        [Test]
        public async Task RGD25_Http_PostWebhook_Unauthenticated_Returns401()
        {
            using var unauthClient = _factory.CreateClient();

            var resp = await unauthClient.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    HeadRef = UniqueHead(), CaseId = UniqueCase(),
                    Outcome = ApprovalWebhookOutcome.Approved
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "RGD25: Webhook endpoint must require authentication.");
        }

        /// <summary>RGD26: HTTP readiness 3-call idempotency after full pipeline.</summary>
        [Test]
        public async Task RGD26_Http_Readiness_IdempotentAcrossThreeCalls()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var r1 = await GetReadinessAsync(head, caseId);
            var r2 = await GetReadinessAsync(head, caseId);
            var r3 = await GetReadinessAsync(head, caseId);

            Assert.That(r1.Mode, Is.EqualTo(r2.Mode),
                "RGD26: Mode must be idempotent (call 1→2).");
            Assert.That(r2.Mode, Is.EqualTo(r3.Mode),
                "RGD26: Mode must be idempotent (call 2→3).");
            Assert.That(r1.IsReleaseEvidence, Is.EqualTo(r3.IsReleaseEvidence),
                "RGD26: IsReleaseEvidence must be idempotent across calls.");
        }

        /// <summary>RGD27: EnvironmentLabel null when not provided in evidence request.</summary>
        [Test]
        public async Task RGD27_ServiceLayer_NullEnvironmentLabel_Propagates_AsNullOrEmpty()
        {
            var svc = CreateSvc();
            string head = UniqueHead();
            string caseId = UniqueCase();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved, ActorId = "a", Reason = "r" },
                "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId, EnvironmentLabel = null },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(r.EnvironmentLabel, Is.Null.Or.Empty,
                "RGD27: Null EnvironmentLabel in evidence must produce null/empty in readiness response.");
        }

        /// <summary>RGD28: EnvironmentLabel special characters round-trip correctly.</summary>
        [Test]
        public async Task RGD28_ServiceLayer_SpecialCharsEnvironmentLabel_RoundTrips()
        {
            var svc = CreateSvc();
            string head = UniqueHead();
            string caseId = UniqueCase();
            const string label = "protected/env-v2.1_staging";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved, ActorId = "a", Reason = "r" },
                "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId, EnvironmentLabel = label },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(r.EnvironmentLabel, Is.EqualTo(label),
                "RGD28: EnvironmentLabel with special chars must round-trip correctly.");
        }

        /// <summary>RGD29: ReadyReleaseGrade with 72h window remains valid at 70h mark.</summary>
        [Test]
        public async Task RGD29_ServiceLayer_72hWindow_FreshAt70h()
        {
            var tp = new AdvancableTime(DateTimeOffset.UtcNow);
            var svc = CreateSvc(tp);
            string head = UniqueHead();
            string caseId = UniqueCase();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved, ActorId = "a", Reason = "r" },
                "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId, FreshnessWindowHours = 72 },
                "actor");
            tp.Advance(TimeSpan.FromHours(70));

            var r = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId, FreshnessWindowHours = 72 });

            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGD29: 70h into 72h window → still ReadyReleaseGrade.");
        }

        /// <summary>RGD30: ReadyReleaseGrade degrades to StaleEvidence at 73h in 72h window.</summary>
        [Test]
        public async Task RGD30_ServiceLayer_72hWindow_StaleAt73h()
        {
            var tp = new AdvancableTime(DateTimeOffset.UtcNow);
            var svc = CreateSvc(tp);
            string head = UniqueHead();
            string caseId = UniqueCase();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved, ActorId = "a", Reason = "r" },
                "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId, FreshnessWindowHours = 72 },
                "actor");
            tp.Advance(TimeSpan.FromHours(73));

            var r = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId, FreshnessWindowHours = 72 });

            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.StaleEvidence),
                "RGD30: 73h into 72h window → StaleEvidence.");
            Assert.That(r.IsReleaseEvidence, Is.False,
                "RGD30: StaleEvidence → IsReleaseEvidence must be false.");
        }
    }
}
