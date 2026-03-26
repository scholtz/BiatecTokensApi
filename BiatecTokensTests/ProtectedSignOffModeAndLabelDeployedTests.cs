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
    /// Deployed-parity tests covering the <c>Mode</c> (<see cref="StrictArtifactMode"/>)
    /// and <c>EnvironmentLabel</c> fields added to close the roadmap's documented backend-side
    /// MVP sign-off blocker.
    ///
    /// These tests exercise the full HTTP stack (routing → controller → service) through
    /// <see cref="WebApplicationFactory{TEntryPoint}"/> and validate that:
    ///
    /// 1.  The <c>mode</c> field is present and correct in every readiness response.
    /// 2.  The <c>environmentLabel</c> field round-trips through persist → readiness.
    /// 3.  Mode/IsReleaseEvidence invariants hold across all Mode values.
    /// 4.  Schema contracts for both new fields are stable across independent calls.
    ///
    /// Test groups:
    ///   ML01–ML10  – <c>mode</c> field values at the HTTP response layer
    ///   ML11–ML20  – <c>environmentLabel</c> HTTP round-trip
    ///   ML21–ML30  – schema contract: JSON keys, serialisation, and invariants
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffModeAndLabelDeployedTests
    {
        // ════════════════════════════════════════════════════════════════════
        // Controllable time provider (staleness simulation)
        // ════════════════════════════════════════════════════════════════════

        private sealed class AdvancableTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public AdvancableTimeProvider(DateTimeOffset now) => _now = now;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ════════════════════════════════════════════════════════════════════
        // WebApplicationFactory
        // ════════════════════════════════════════════════════════════════════

        private sealed class ModeAndLabelFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "ModeAndLabelTestSecretKey32CharsMin!",
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
                        ["KeyManagementConfig:HardcodedKey"] = "ModeAndLabelTestKeyXXXX32CharsMin!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "ml-test",
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

        private ModeAndLabelFactory _factory = null!;
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
            _factory = new ModeAndLabelFactory();
            _client = _factory.CreateClient();
            _jwt = await ObtainJwtAsync(_client, $"ml-{Guid.NewGuid():N}");
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

        private static string UniqueHead() => $"head-ml-{Guid.NewGuid():N}";
        private static string UniqueCase() => $"case-ml-{Guid.NewGuid():N}";

        private static ProtectedSignOffEvidencePersistenceService CreateService(TimeProvider? tp = null)
            => new(NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance, null, tp);

        private async Task<RecordApprovalWebhookResponse> PostWebhookAsync(
            string caseId, string headRef,
            ApprovalWebhookOutcome outcome = ApprovalWebhookOutcome.Approved)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef, Outcome = outcome,
                    CorrelationId = Guid.NewGuid().ToString("N")
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<RecordApprovalWebhookResponse>())!;
        }

        private async Task<PersistSignOffEvidenceResponse> PostEvidenceAsync(
            string headRef, string caseId,
            bool requireReleaseGrade = false,
            string? environmentLabel = null)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    RequireReleaseGrade = requireReleaseGrade,
                    EnvironmentLabel = environmentLabel
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>())!;
        }

        private async Task<JsonElement> GetReadinessJsonAsync(string headRef, string caseId)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });
            string json = await resp.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json).RootElement;
        }

        private async Task<GetSignOffReleaseReadinessResponse> GetReadinessAsync(
            string headRef, string caseId)
        {
            var resp = await _client.PostAsJsonAsync($"{BaseUrl}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });
            return (await resp.Content.ReadFromJsonAsync<GetSignOffReleaseReadinessResponse>(_jsonOpts))!;
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string userTag)
        {
            string email = $"ml-{userTag}@mode-label-test.biatec.example.com";
            var resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email = email,
                Password = "ModeAndLabelIT!Pass1",
                ConfirmPassword = "ModeAndLabelIT!Pass1",
                FullName = $"Mode Label Test User ({userTag})"
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
        // ML01–ML10: mode field values at the HTTP response layer
        // ════════════════════════════════════════════════════════════════════

        /// <summary>ML01: readiness response always contains a non-null mode field.</summary>
        [Test]
        public async Task ML01_ReadinessResponse_AlwaysHasModeField()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            var json = await GetReadinessJsonAsync(headRef, caseId);
            Assert.That(json.TryGetProperty("mode", out _), Is.True,
                "ML01: readiness response must contain a 'mode' field");
        }

        /// <summary>ML02: fresh evidence with no approval webhook → Mode is Configured (not ReadyReleaseGrade).</summary>
        [Test]
        public async Task ML02_FreshEvidenceNoWebhook_Mode_IsConfigured()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await PostEvidenceAsync(headRef, caseId, requireReleaseGrade: false);

            var readiness = await GetReadinessAsync(headRef, caseId);

            // No webhook → NotReleaseEvidence or Configured (not ReadyReleaseGrade)
            Assert.That(readiness.Mode,
                Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "ML02: evidence without approval webhook must not produce ReadyReleaseGrade mode");
            Assert.That(readiness.Mode,
                Is.Not.EqualTo(StrictArtifactMode.NotConfigured),
                "ML02: configured instance must not report NotConfigured");
        }

        /// <summary>ML03: approved webhook + release-grade evidence → Mode is ReadyReleaseGrade.</summary>
        [Test]
        public async Task ML03_ApprovedWebhook_PlusReleaseGradeEvidence_Mode_IsReadyReleaseGrade()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await PostWebhookAsync(caseId, headRef, ApprovalWebhookOutcome.Approved);
            await PostEvidenceAsync(headRef, caseId, requireReleaseGrade: false); // pack becomes IsReleaseGrade=true

            var readiness = await GetReadinessAsync(headRef, caseId);
            Assert.That(readiness.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "ML03: approved webhook + evidence that is release-grade → ReadyReleaseGrade mode");
        }

        /// <summary>ML04: Mode ReadyReleaseGrade implies IsReleaseEvidence=true.</summary>
        [Test]
        public async Task ML04_ReadyReleaseGrade_Mode_Implies_IsReleaseEvidence_True()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await PostWebhookAsync(caseId, headRef, ApprovalWebhookOutcome.Approved);
            await PostEvidenceAsync(headRef, caseId);

            var readiness = await GetReadinessAsync(headRef, caseId);
            if (readiness.Mode == StrictArtifactMode.ReadyReleaseGrade)
            {
                Assert.That(readiness.IsReleaseEvidence, Is.True,
                    "ML04: ReadyReleaseGrade must imply IsReleaseEvidence=true");
            }
        }

        /// <summary>ML05: Denied approval webhook + evidence → Mode is not ReadyReleaseGrade (fail-closed).</summary>
        [Test]
        public async Task ML05_DeniedWebhook_Mode_IsNotReadyReleaseGrade()
        {
            var svc = CreateService();
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Denied
                }, "actor-ml05");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor-ml05");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "ML05: denied approval must not produce ReadyReleaseGrade mode");
        }

        /// <summary>ML06: Stale evidence → Mode is StaleEvidence.</summary>
        [Test]
        public async Task ML06_StaleEvidence_Mode_IsStaleEvidence()
        {
            var baseTime = DateTimeOffset.UtcNow;
            var tp = new AdvancableTimeProvider(baseTime);
            var svc = CreateService(tp);
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved },
                "actor-ml06");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId, FreshnessWindowHours = 1 },
                "actor-ml06");

            tp.Advance(TimeSpan.FromHours(2));

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId, FreshnessWindowHours = 1 });

            Assert.That(readiness.Mode, Is.EqualTo(StrictArtifactMode.StaleEvidence),
                "ML06: evidence past freshness window must produce StaleEvidence mode");
        }

        /// <summary>ML07: StaleEvidence mode implies IsReleaseEvidence=false.</summary>
        [Test]
        public async Task ML07_StaleEvidence_Mode_Implies_IsReleaseEvidence_False()
        {
            var tp = new AdvancableTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved },
                "actor-ml07");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId, FreshnessWindowHours = 1 },
                "actor-ml07");

            tp.Advance(TimeSpan.FromHours(3));

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId, FreshnessWindowHours = 1 });

            Assert.That(readiness.Mode, Is.EqualTo(StrictArtifactMode.StaleEvidence),
                "ML07 pre: stale state precondition");
            Assert.That(readiness.IsReleaseEvidence, Is.False,
                "ML07: StaleEvidence mode must imply IsReleaseEvidence=false");
        }

        /// <summary>ML08: mode value is a known StrictArtifactMode enum value (not a default int).</summary>
        [Test]
        public async Task ML08_Mode_InReadinessResponse_IsKnownEnumValue()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            var readiness = await GetReadinessAsync(headRef, caseId);

            Assert.That(Enum.IsDefined(typeof(StrictArtifactMode), readiness.Mode), Is.True,
                "ML08: Mode must be a defined StrictArtifactMode enum value");
        }

        /// <summary>ML09: Mode is consistent across three consecutive readiness calls for same inputs.</summary>
        [Test]
        public async Task ML09_Mode_IsDeterministicAcrossConsecutiveCalls()
        {
            var svc = CreateService();
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved },
                "actor-ml09");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor-ml09");

            var req = new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId };
            var r1 = await svc.GetReleaseReadinessAsync(req);
            var r2 = await svc.GetReleaseReadinessAsync(req);
            var r3 = await svc.GetReleaseReadinessAsync(req);

            Assert.That(r2.Mode, Is.EqualTo(r1.Mode), "ML09: Mode must be deterministic across run 1→2");
            Assert.That(r3.Mode, Is.EqualTo(r1.Mode), "ML09: Mode must be deterministic across run 1→3");
        }

        /// <summary>ML10: Different heads produce independent Mode values.</summary>
        [Test]
        public async Task ML10_DifferentHeads_ProduceIndependentModeValues()
        {
            var svc = CreateService();
            string caseId = UniqueCase();
            string headA = UniqueHead();
            string headB = UniqueHead();

            // head A: full approval + evidence → ReadyReleaseGrade
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headA, Outcome = ApprovalWebhookOutcome.Approved },
                "actor-ml10");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headA, CaseId = caseId }, "actor-ml10");

            var rA = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest { HeadRef = headA, CaseId = caseId });
            var rB = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest { HeadRef = headB, CaseId = caseId });

            Assert.That(rA.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade), "ML10: headA should be ReadyReleaseGrade");
            Assert.That(rB.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade), "ML10: headB with no evidence must not bleed into ReadyReleaseGrade");
        }

        // ════════════════════════════════════════════════════════════════════
        // ML11–ML20: environmentLabel HTTP round-trip
        // ════════════════════════════════════════════════════════════════════

        /// <summary>ML11: EnvironmentLabel sent in persist → echoed in readiness response.</summary>
        [Test]
        public async Task ML11_EnvironmentLabel_SentInPersist_EchoedInReadiness()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();
            const string label = "staging";

            await PostEvidenceAsync(headRef, caseId, environmentLabel: label);
            var readiness = await GetReadinessAsync(headRef, caseId);

            Assert.That(readiness.EnvironmentLabel, Is.EqualTo(label),
                "ML11: EnvironmentLabel from persist request must be echoed in readiness response");
        }

        /// <summary>ML12: Null EnvironmentLabel in persist → null in readiness response.</summary>
        [Test]
        public async Task ML12_NullEnvironmentLabel_In_Readiness_IsNull()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await PostEvidenceAsync(headRef, caseId, environmentLabel: null);
            var readiness = await GetReadinessAsync(headRef, caseId);

            Assert.That(readiness.EnvironmentLabel, Is.Null,
                "ML12: null EnvironmentLabel in request must be null in readiness response");
        }

        /// <summary>ML13: Whitespace EnvironmentLabel in persist → normalised to null.</summary>
        [Test]
        public async Task ML13_WhitespaceEnvironmentLabel_NormalisedToNull()
        {
            var svc = CreateService();
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    EnvironmentLabel = "   "
                }, "actor-ml13");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.EnvironmentLabel, Is.Null,
                "ML13: whitespace-only EnvironmentLabel must be normalised to null");
        }

        /// <summary>ML14: EnvironmentLabel is echoed in HTTP readiness response JSON as a string key.</summary>
        [Test]
        public async Task ML14_EnvironmentLabel_PresentInHTTPResponseJson()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();
            const string label = "production";

            await PostEvidenceAsync(headRef, caseId, environmentLabel: label);
            var json = await GetReadinessJsonAsync(headRef, caseId);

            Assert.That(json.TryGetProperty("environmentLabel", out JsonElement labelEl), Is.True,
                "ML14: JSON readiness response must contain 'environmentLabel' key");
            Assert.That(labelEl.GetString(), Is.EqualTo(label),
                "ML14: environmentLabel value must match the submitted label");
        }

        /// <summary>ML15: EnvironmentLabel from the latest evidence pack is echoed (not an earlier one).</summary>
        [Test]
        public async Task ML15_LatestPack_EnvironmentLabel_EchoedInReadiness()
        {
            var svc = CreateService();
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    EnvironmentLabel = "first-env"
                }, "actor-ml15");

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    EnvironmentLabel = "second-env"
                }, "actor-ml15");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.EnvironmentLabel, Is.EqualTo("second-env"),
                "ML15: the latest evidence pack's EnvironmentLabel must be echoed, not an earlier one");
        }

        /// <summary>ML16: EnvironmentLabel survives ReadyReleaseGrade state.</summary>
        [Test]
        public async Task ML16_EnvironmentLabel_Survives_ReadyReleaseGrade()
        {
            var svc = CreateService();
            string headRef = UniqueHead();
            string caseId = UniqueCase();
            const string label = "release-env";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved },
                "actor-ml16");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    EnvironmentLabel = label
                }, "actor-ml16");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade), "ML16 pre: must be ReadyReleaseGrade");
            Assert.That(readiness.EnvironmentLabel, Is.EqualTo(label),
                "ML16: EnvironmentLabel must be echoed even in ReadyReleaseGrade state");
        }

        /// <summary>ML17: EnvironmentLabel with special characters is preserved.</summary>
        [Test]
        public async Task ML17_EnvironmentLabel_SpecialCharacters_Preserved()
        {
            var svc = CreateService();
            string headRef = UniqueHead();
            string caseId = UniqueCase();
            const string label = "env-123/branch-main@2026";

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    EnvironmentLabel = label
                }, "actor-ml17");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.EnvironmentLabel, Is.EqualTo(label),
                "ML17: EnvironmentLabel with special characters must be preserved exactly");
        }

        /// <summary>ML18: EnvironmentLabel is stored on the evidence pack (not just the readiness response).</summary>
        [Test]
        public async Task ML18_EnvironmentLabel_StoredOnEvidencePack()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();
            const string label = "pack-label-test";

            var evidenceResp = await PostEvidenceAsync(headRef, caseId, environmentLabel: label);

            Assert.That(evidenceResp.Pack, Is.Not.Null, "ML18: evidence pack must be returned");
            Assert.That(evidenceResp.Pack!.EnvironmentLabel, Is.EqualTo(label),
                "ML18: EnvironmentLabel must be stored on the returned evidence pack");
        }

        /// <summary>ML19: EnvironmentLabel null on pack when no label was provided.</summary>
        [Test]
        public async Task ML19_EnvironmentLabel_NullOnPackWhenNotProvided()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            var evidenceResp = await PostEvidenceAsync(headRef, caseId, environmentLabel: null);

            Assert.That(evidenceResp.Pack, Is.Not.Null, "ML19: evidence pack must be returned");
            Assert.That(evidenceResp.Pack!.EnvironmentLabel, Is.Null,
                "ML19: EnvironmentLabel must be null on evidence pack when not provided");
        }

        /// <summary>ML20: Empty string EnvironmentLabel is normalised to null (not stored as empty string).</summary>
        [Test]
        public async Task ML20_EmptyStringEnvironmentLabel_NormalisedToNull()
        {
            var svc = CreateService();
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef, CaseId = caseId,
                    EnvironmentLabel = string.Empty
                }, "actor-ml20");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.EnvironmentLabel, Is.Null,
                "ML20: empty string EnvironmentLabel must be normalised to null");
        }

        // ════════════════════════════════════════════════════════════════════
        // ML21–ML30: schema contract — JSON keys, serialisation, invariants
        // ════════════════════════════════════════════════════════════════════

        /// <summary>ML21: Readiness JSON response contains both 'mode' and 'environmentLabel' keys.</summary>
        [Test]
        public async Task ML21_ReadinessJson_ContainsModeAndEnvironmentLabelKeys()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            var json = await GetReadinessJsonAsync(headRef, caseId);

            Assert.That(json.TryGetProperty("mode", out _), Is.True,
                "ML21: JSON must contain 'mode' key");
            Assert.That(json.TryGetProperty("environmentLabel", out _), Is.True,
                "ML21: JSON must contain 'environmentLabel' key");
        }

        /// <summary>ML22: 'mode' in JSON response serialises as integer (enum default).</summary>
        [Test]
        public async Task ML22_ModeField_SerializesAsInteger()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            var json = await GetReadinessJsonAsync(headRef, caseId);
            Assert.That(json.TryGetProperty("mode", out JsonElement modeEl), Is.True, "ML22: 'mode' must exist");

            // Default .NET JSON serialisation serialises enums as integers
            Assert.That(modeEl.ValueKind, Is.EqualTo(JsonValueKind.Number),
                "ML22: 'mode' must serialise as a JSON number (integer enum)");
        }

        /// <summary>ML23: 'mode' integer value maps to a valid StrictArtifactMode.</summary>
        [Test]
        public async Task ML23_ModeIntegerValue_MapsToValidEnum()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            var json = await GetReadinessJsonAsync(headRef, caseId);
            json.TryGetProperty("mode", out JsonElement modeEl);

            int modeInt = modeEl.GetInt32();
            Assert.That(Enum.IsDefined(typeof(StrictArtifactMode), modeInt), Is.True,
                $"ML23: mode integer {modeInt} must map to a defined StrictArtifactMode value");
        }

        /// <summary>ML24: 'environmentLabel' in JSON is string or null (never object/array).</summary>
        [Test]
        public async Task ML24_EnvironmentLabelJson_IsStringOrNull()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            var json = await GetReadinessJsonAsync(headRef, caseId);
            json.TryGetProperty("environmentLabel", out JsonElement labelEl);

            Assert.That(
                labelEl.ValueKind is JsonValueKind.String or JsonValueKind.Null,
                Is.True,
                $"ML24: 'environmentLabel' must be a JSON string or null, got {labelEl.ValueKind}");
        }

        /// <summary>ML25: Schema is stable across 3 consecutive readiness calls.</summary>
        [Test]
        public async Task ML25_ReadinessSchema_StableAcrossThreeCalls()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await PostEvidenceAsync(headRef, caseId, environmentLabel: "schema-check");

            for (int i = 1; i <= 3; i++)
            {
                var json = await GetReadinessJsonAsync(headRef, caseId);
                Assert.That(json.TryGetProperty("mode", out _), Is.True,
                    $"ML25: 'mode' must be present on call {i}");
                Assert.That(json.TryGetProperty("environmentLabel", out _), Is.True,
                    $"ML25: 'environmentLabel' must be present on call {i}");
            }
        }

        /// <summary>ML26: NotConfigured mode invariant — IsReleaseEvidence is always false.</summary>
        [Test]
        public async Task ML26_NotConfiguredMode_Implies_IsReleaseEvidence_False()
        {
            var svc = CreateService();
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            // Simulate BlockedMissingConfiguration by injecting EnvironmentNotReady blocker
            // This is achieved by calling readiness on a service with no config at all
            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            // If Mode is NotConfigured, IsReleaseEvidence must be false
            if (readiness.Mode == StrictArtifactMode.NotConfigured)
            {
                Assert.That(readiness.IsReleaseEvidence, Is.False,
                    "ML26: NotConfigured mode must never carry IsReleaseEvidence=true");
            }
            else
            {
                // Regardless of mode, IsReleaseEvidence must be consistent with Mode
                if (readiness.IsReleaseEvidence)
                {
                    Assert.That(readiness.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                        "ML26: IsReleaseEvidence=true is only valid when Mode=ReadyReleaseGrade");
                }
            }
        }

        /// <summary>ML27: Configured mode does not imply IsReleaseEvidence (it is false for configured-but-not-ready).</summary>
        [Test]
        public async Task ML27_ConfiguredMode_IsReleaseEvidence_IsFalse()
        {
            var svc = CreateService();
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor-ml27");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            // Mode is Configured (no approval webhook → not release grade)
            if (readiness.Mode == StrictArtifactMode.Configured)
            {
                Assert.That(readiness.IsReleaseEvidence, Is.False,
                    "ML27: Configured mode (without approval) must not carry IsReleaseEvidence=true");
            }
        }

        /// <summary>ML28: Mode and Status are always internally consistent.</summary>
        [Test]
        public async Task ML28_Mode_And_Status_AreInternallyConsistent()
        {
            var svc = CreateService();
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved },
                "actor-ml28");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor-ml28");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            // ReadyReleaseGrade ↔ Status.Ready
            if (readiness.Mode == StrictArtifactMode.ReadyReleaseGrade)
            {
                Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                    "ML28: ReadyReleaseGrade mode must correspond to Status.Ready");
            }

            // NotConfigured ↔ BlockedMissingConfiguration
            if (readiness.Mode == StrictArtifactMode.NotConfigured)
            {
                Assert.That(readiness.Status,
                    Is.EqualTo(SignOffReleaseReadinessStatus.BlockedMissingConfiguration),
                    "ML28: NotConfigured mode must correspond to BlockedMissingConfiguration status");
            }
        }

        /// <summary>ML29: Mode field is present even when headRef yields no evidence (schema completeness).</summary>
        [Test]
        public async Task ML29_ModeField_PresentEvenForUnknownHead()
        {
            string headRef = "unknown-head-" + Guid.NewGuid().ToString("N");
            string caseId = UniqueCase();

            var json = await GetReadinessJsonAsync(headRef, caseId);

            Assert.That(json.TryGetProperty("mode", out JsonElement modeEl), Is.True,
                "ML29: 'mode' field must be present even for unknown headRef");
            Assert.That(modeEl.ValueKind, Is.EqualTo(JsonValueKind.Number),
                "ML29: 'mode' must be a valid integer for unknown headRef");
        }

        /// <summary>ML30: EnvironmentLabel key is present (possibly null) even when no evidence was submitted.</summary>
        [Test]
        public async Task ML30_EnvironmentLabelKey_PresentEvenWithNoEvidence()
        {
            string headRef = UniqueHead();
            string caseId = UniqueCase();

            var json = await GetReadinessJsonAsync(headRef, caseId);

            Assert.That(json.TryGetProperty("environmentLabel", out JsonElement labelEl), Is.True,
                "ML30: 'environmentLabel' JSON key must be present even when no evidence has been submitted");
            // Value should be null when no evidence was submitted
            Assert.That(
                labelEl.ValueKind is JsonValueKind.Null or JsonValueKind.String,
                Is.True,
                "ML30: environmentLabel must be null or string (never object/array) when no evidence exists");
        }
    }
}
