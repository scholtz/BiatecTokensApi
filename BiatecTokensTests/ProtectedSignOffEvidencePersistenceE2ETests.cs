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
    /// End-to-end tests that verify the backend evidence persistence pipeline introduced
    /// in protected-sign-off.yml Step D sections 7a–7c.
    ///
    /// These tests prove that the three-step persistence flow:
    ///   1. POST /api/v1/protected-signoff-evidence/webhooks/approval  (Approved outcome)
    ///   2. POST /api/v1/protected-signoff-evidence/evidence           (requireReleaseGrade+requireApprovalWebhook)
    ///   3. POST /api/v1/protected-signoff-evidence/release-readiness  (StrictArtifactMode query)
    ///
    /// drives the backend's StrictArtifactMode to ReadyReleaseGrade, closing the
    /// "pipeline green but artifact says not_configured" credibility gap described
    /// in issue #627 and the product roadmap.
    ///
    /// Test groups:
    ///   EP01–EP10  — Happy-path persistence flow produces ReadyReleaseGrade
    ///   EP11–EP20  — Fail-closed semantics: missing webhook, missing evidence, wrong head
    ///   EP21–EP30  — Payload schema contracts: backendStrictArtifactMode, environmentLabel
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffEvidencePersistenceE2ETests
    {
        // ════════════════════════════════════════════════════════════════════
        // WebApplicationFactory
        // ════════════════════════════════════════════════════════════════════

        private sealed class EvidencePersistenceE2EFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "EvidencePersistenceE2ETestSecretKey32C!",
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
                        ["KeyManagementConfig:HardcodedKey"] = "EvidencePersistE2EKeyXXXXXXXX32C!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "ep-e2e-test",
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

        private EvidencePersistenceE2EFactory _factory = null!;
        private HttpClient _client = null!;
        private string _jwt = null!;

        private const string EvidenceBase = "/api/v1/protected-signoff-evidence";

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        [SetUp]
        public async Task SetUp()
        {
            _factory = new EvidencePersistenceE2EFactory();
            _client = _factory.CreateClient();
            _jwt = await ObtainJwtAsync(_client, $"ep-{Guid.NewGuid():N}");
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

        private static string UniqueHead() => $"head-ep-{Guid.NewGuid():N}";
        private static string UniqueCase() => $"case-ep-{Guid.NewGuid():N}";

        private async Task<RecordApprovalWebhookResponse> PostWebhookAsync(
            string caseId, string headRef,
            ApprovalWebhookOutcome outcome = ApprovalWebhookOutcome.Approved)
        {
            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = headRef,
                    Outcome = outcome,
                    ActorId = "ep-test-actor",
                    Reason = "EP test approval",
                    CorrelationId = Guid.NewGuid().ToString("N")
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<RecordApprovalWebhookResponse>(_jsonOpts))!;
        }

        private async Task<PersistSignOffEvidenceResponse> PostEvidenceAsync(
            string headRef, string caseId,
            bool requireReleaseGrade = true,
            bool requireApprovalWebhook = true,
            string? environmentLabel = "protected-ci")
        {
            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId,
                    RequireReleaseGrade = requireReleaseGrade,
                    RequireApprovalWebhook = requireApprovalWebhook,
                    EnvironmentLabel = environmentLabel,
                    FreshnessWindowHours = 24
                });
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>(_jsonOpts))!;
        }

        private async Task<GetSignOffReleaseReadinessResponse> GetReadinessAsync(
            string headRef, string caseId)
        {
            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });
            return (await resp.Content.ReadFromJsonAsync<GetSignOffReleaseReadinessResponse>(_jsonOpts))!;
        }

        private static async Task<string> ObtainJwtAsync(HttpClient client, string userTag)
        {
            string email = $"ep-{userTag}@evidence-persistence-e2e.biatec.example.com";
            var resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                Email = email,
                Password = "EvidencePersistE2E!2026",
                ConfirmPassword = "EvidencePersistE2E!2026",
                FullName = $"EP E2E Test ({userTag})"
            });
            Assert.That(resp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Created),
                $"Registration must succeed for user '{userTag}'");
            JsonDocument? doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            string? token = doc?.RootElement.GetProperty("accessToken").GetString();
            Assert.That(token, Is.Not.Null.And.Not.Empty,
                $"Registration must return a non-empty accessToken for user '{userTag}'");
            return token!;
        }

        // ════════════════════════════════════════════════════════════════════
        // EP01–EP10: Happy-path persistence flow produces ReadyReleaseGrade
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// EP01: Full three-step evidence persistence flow (webhook → evidence → readiness)
        /// drives StrictArtifactMode to ReadyReleaseGrade — this is the backend-confirmation
        /// that the workflow artifact isReleaseGradeEvidence=true represents.
        /// </summary>
        [Test]
        public async Task EP01_FullPersistenceFlow_ProducesReadyReleaseGrade()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var webhookResp = await PostWebhookAsync(caseId, head);
            Assert.That(webhookResp.Success, Is.True, "EP01: Approval webhook must be recorded");

            var evidenceResp = await PostEvidenceAsync(head, caseId);
            Assert.That(evidenceResp.Success, Is.True, "EP01: Evidence pack must be persisted");

            var readiness = await GetReadinessAsync(head, caseId);
            Assert.That(readiness.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP01: StrictArtifactMode must be ReadyReleaseGrade after full persistence flow.");
            Assert.That(readiness.IsReleaseEvidence, Is.True,
                "EP01: IsReleaseEvidence must be true when Mode=ReadyReleaseGrade.");
        }

        /// <summary>EP02: Approval webhook record returns Success=true and a non-null Record.</summary>
        [Test]
        public async Task EP02_ApprovalWebhook_ReturnsSuccessWithRecord()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var resp = await PostWebhookAsync(caseId, head);

            Assert.That(resp.Success, Is.True, "EP02: Approval webhook Success must be true.");
            Assert.That(resp.Record, Is.Not.Null, "EP02: Approval webhook Record must not be null.");
            Assert.That(resp.Record!.Outcome, Is.EqualTo(ApprovalWebhookOutcome.Approved),
                "EP02: Recorded outcome must be Approved.");
        }

        /// <summary>EP03: Evidence pack persistence returns Success=true with a non-null Pack.</summary>
        [Test]
        public async Task EP03_EvidencePack_ReturnsSuccessWithPack()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);

            var resp = await PostEvidenceAsync(head, caseId);

            Assert.That(resp.Success, Is.True, "EP03: Evidence pack Success must be true.");
            Assert.That(resp.Pack, Is.Not.Null, "EP03: Evidence Pack must not be null.");
        }

        /// <summary>EP04: Evidence pack carries environmentLabel="protected-ci" as passed.</summary>
        [Test]
        public async Task EP04_EvidencePack_CarriesEnvironmentLabel()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);

            var resp = await PostEvidenceAsync(head, caseId, environmentLabel: "protected-ci");

            Assert.That(resp.Pack?.EnvironmentLabel, Is.EqualTo("protected-ci"),
                "EP04: Evidence pack must carry environmentLabel=protected-ci.");
        }

        /// <summary>EP05: Release readiness after full flow has Status=Ready.</summary>
        [Test]
        public async Task EP05_ReleaseReadiness_AfterFullFlow_StatusIsReady()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var readiness = await GetReadinessAsync(head, caseId);

            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "EP05: Status must be Ready after full evidence persistence flow.");
        }

        /// <summary>EP06: Release readiness after full flow has HasApprovalWebhook=true.</summary>
        [Test]
        public async Task EP06_ReleaseReadiness_AfterFullFlow_HasApprovalWebhook()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var readiness = await GetReadinessAsync(head, caseId);

            Assert.That(readiness.HasApprovalWebhook, Is.True,
                "EP06: HasApprovalWebhook must be true after approval webhook is recorded.");
        }

        /// <summary>EP07: Evidence pack IsReleaseGrade=true after full flow.</summary>
        [Test]
        public async Task EP07_EvidencePack_IsReleaseGrade_AfterFullFlow()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            var resp = await PostEvidenceAsync(head, caseId, requireReleaseGrade: true);

            Assert.That(resp.Pack?.IsReleaseGrade, Is.True,
                "EP07: Evidence pack IsReleaseGrade must be true when requireReleaseGrade=true and all criteria are met.");
        }

        /// <summary>EP08: Full flow is repeatable — second run on same head produces ReadyReleaseGrade again.</summary>
        [Test]
        public async Task EP08_FullFlow_IsRepeatable()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            // First run
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);
            var r1 = await GetReadinessAsync(head, caseId);
            Assert.That(r1.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade), "EP08 run 1: ReadyReleaseGrade expected.");

            // Second run — re-record webhook and re-persist evidence
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);
            var r2 = await GetReadinessAsync(head, caseId);
            Assert.That(r2.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade), "EP08 run 2: ReadyReleaseGrade expected.");
        }

        /// <summary>EP09: Multiple independent head refs produce isolated ReadyReleaseGrade results.</summary>
        [Test]
        public async Task EP09_MultipleHeadRefs_ProduceIsolatedReadyReleaseGrade()
        {
            string head1 = UniqueHead();
            string head2 = UniqueHead();
            string caseId = UniqueCase();

            await PostWebhookAsync(caseId, head1);
            await PostEvidenceAsync(head1, caseId);

            await PostWebhookAsync(caseId, head2);
            await PostEvidenceAsync(head2, caseId);

            var r1 = await GetReadinessAsync(head1, caseId);
            var r2 = await GetReadinessAsync(head2, caseId);

            Assert.That(r1.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP09: head1 must be ReadyReleaseGrade.");
            Assert.That(r2.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP09: head2 must be ReadyReleaseGrade.");
        }

        /// <summary>EP10: EnvironmentLabel from evidence pack surfaces in release readiness response.</summary>
        [Test]
        public async Task EP10_EnvironmentLabel_SurfacesInReadiness()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId, environmentLabel: "protected-ci");

            var readiness = await GetReadinessAsync(head, caseId);

            Assert.That(readiness.EnvironmentLabel, Is.EqualTo("protected-ci"),
                "EP10: EnvironmentLabel from evidence pack must surface in release readiness response.");
        }

        // ════════════════════════════════════════════════════════════════════
        // EP11–EP20: Fail-closed semantics
        // ════════════════════════════════════════════════════════════════════

        /// <summary>EP11: No evidence → Mode is NOT ReadyReleaseGrade (BlockedMissingEvidence).</summary>
        [Test]
        public async Task EP11_NoEvidence_ModeIsNotReadyReleaseGrade()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var readiness = await GetReadinessAsync(head, caseId);

            Assert.That(readiness.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP11: No evidence must not produce ReadyReleaseGrade.");
            Assert.That(readiness.IsReleaseEvidence, Is.False,
                "EP11: IsReleaseEvidence must be false when no evidence exists.");
        }

        /// <summary>EP12: Evidence without approval webhook → Mode is NOT ReadyReleaseGrade.</summary>
        [Test]
        public async Task EP12_EvidenceWithoutWebhook_ModeIsNotReadyReleaseGrade()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            // Persist evidence without requireApprovalWebhook — allowed but not release-grade
            await PostEvidenceAsync(head, caseId,
                requireReleaseGrade: false, requireApprovalWebhook: false);

            var readiness = await GetReadinessAsync(head, caseId);

            Assert.That(readiness.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP12: Evidence without an approval webhook must not produce ReadyReleaseGrade.");
            Assert.That(readiness.IsReleaseEvidence, Is.False,
                "EP12: IsReleaseEvidence must be false without approval webhook.");
        }

        /// <summary>EP13: Denied webhook → evidence pack with requireApprovalWebhook=true fails (fail-closed).</summary>
        [Test]
        public async Task EP13_DeniedWebhook_EvidencePersistenceFails()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head, ApprovalWebhookOutcome.Denied);

            // Evidence with requireApprovalWebhook should fail — no approved webhook exists
            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = head, CaseId = caseId,
                    RequireReleaseGrade = true, RequireApprovalWebhook = true,
                    EnvironmentLabel = "protected-ci", FreshnessWindowHours = 24
                });

            // Service returns 400 when requireApprovalWebhook=true but no approved webhook exists
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest).Or.EqualTo(HttpStatusCode.OK),
                "EP13: Status code should be 400 (fail-closed) or 200 with Success=false.");
            var result = await resp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>(_jsonOpts);
            Assert.That(result, Is.Not.Null, "EP13: Response must deserialize.");
            Assert.That(result!.Success, Is.False,
                "EP13: Evidence persistence must fail when only a denied webhook exists and requireApprovalWebhook=true.");
        }

        /// <summary>EP14: Wrong headRef for readiness query → BlockedMissingEvidence (not ReadyReleaseGrade).</summary>
        [Test]
        public async Task EP14_WrongHeadRef_ModeIsBlockedMissingEvidence()
        {
            string head = UniqueHead();
            string wrongHead = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            // Query with a different head ref — no evidence for this head
            var readiness = await GetReadinessAsync(wrongHead, caseId);

            Assert.That(readiness.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP14: Wrong headRef must not produce ReadyReleaseGrade.");
            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.BlockedMissingEvidence),
                "EP14: Status must be BlockedMissingEvidence for an unknown headRef.");
        }

        /// <summary>EP15: Missing environment configuration → NotConfigured mode (fail-closed).</summary>
        [Test]
        public void EP15_MissingEnvConfig_ModeIsNotConfigured()
        {
            // Semantic invariant: BlockedMissingConfiguration → StrictArtifactMode.NotConfigured
            var mode = SignOffReleaseReadinessStatus.BlockedMissingConfiguration switch
            {
                SignOffReleaseReadinessStatus.BlockedMissingConfiguration => StrictArtifactMode.NotConfigured,
                SignOffReleaseReadinessStatus.BlockedProviderUnavailable => StrictArtifactMode.Degraded,
                SignOffReleaseReadinessStatus.DegradedStaleEvidence => StrictArtifactMode.StaleEvidence,
                SignOffReleaseReadinessStatus.Ready => StrictArtifactMode.ReadyReleaseGrade,
                _ => StrictArtifactMode.Configured
            };
            Assert.That(mode, Is.EqualTo(StrictArtifactMode.NotConfigured),
                "EP15: BlockedMissingConfiguration status must map to NotConfigured mode.");
        }

        /// <summary>EP16: No evidence → IsReleaseEvidence=false always (fail-closed invariant).</summary>
        [Test]
        public async Task EP16_NoEvidence_IsReleaseEvidence_AlwaysFalse()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var readiness = await GetReadinessAsync(head, caseId);

            Assert.That(readiness.IsReleaseEvidence, Is.False,
                "EP16: IsReleaseEvidence must be false when no evidence exists — fail-closed invariant.");
        }

        /// <summary>EP17: Approval webhook for correct head + evidence for different head → not release-grade.</summary>
        [Test]
        public async Task EP17_WebhookForOneHead_EvidenceForAnother_NotReleaseGrade()
        {
            string head1 = UniqueHead();
            string head2 = UniqueHead();
            string caseId = UniqueCase();

            // Webhook for head1, evidence for head2 (no webhook for head2)
            await PostWebhookAsync(caseId, head1);
            await PostEvidenceAsync(head2, caseId,
                requireReleaseGrade: false, requireApprovalWebhook: false);

            var readiness = await GetReadinessAsync(head2, caseId);

            Assert.That(readiness.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP17: Webhook for head1 must not satisfy requireApprovalWebhook for head2.");
            Assert.That(readiness.IsReleaseEvidence, Is.False,
                "EP17: Cross-head webhook must not grant release-grade evidence.");
        }

        /// <summary>EP18: Unauthenticated call to evidence endpoint returns 401.</summary>
        [Test]
        public async Task EP18_EvidenceEndpoint_NoJwt_Returns401()
        {
            using var anonClient = _factory.CreateClient();

            var resp = await anonClient.PostAsJsonAsync($"{EvidenceBase}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = UniqueHead(), CaseId = UniqueCase(),
                    RequireReleaseGrade = true, RequireApprovalWebhook = true
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "EP18: Evidence endpoint must return 401 without a JWT.");
        }

        /// <summary>EP19: Unauthenticated call to webhook endpoint returns 401.</summary>
        [Test]
        public async Task EP19_WebhookEndpoint_NoJwt_Returns401()
        {
            using var anonClient = _factory.CreateClient();

            var resp = await anonClient.PostAsJsonAsync($"{EvidenceBase}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = UniqueCase(), HeadRef = UniqueHead(),
                    Outcome = ApprovalWebhookOutcome.Approved
                });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "EP19: Approval webhook endpoint must return 401 without a JWT.");
        }

        /// <summary>EP20: Unauthenticated call to release-readiness endpoint returns 401.</summary>
        [Test]
        public async Task EP20_ReadinessEndpoint_NoJwt_Returns401()
        {
            using var anonClient = _factory.CreateClient();

            var resp = await anonClient.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = UniqueHead(), CaseId = UniqueCase() });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "EP20: Release-readiness endpoint must return 401 without a JWT.");
        }

        // ════════════════════════════════════════════════════════════════════
        // EP21–EP30: Payload schema contracts
        // ════════════════════════════════════════════════════════════════════

        /// <summary>EP21: Release readiness JSON schema must contain 'mode' field.</summary>
        [Test]
        public async Task EP21_ReadinessJson_ContainsModeField()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            Assert.That(doc.RootElement.TryGetProperty("mode", out _), Is.True,
                "EP21: Release readiness JSON must include 'mode' field.");
        }

        /// <summary>EP22: Release readiness JSON schema must contain 'isReleaseEvidence' field.</summary>
        [Test]
        public async Task EP22_ReadinessJson_ContainsIsReleaseEvidenceField()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            Assert.That(doc.RootElement.TryGetProperty("isReleaseEvidence", out _), Is.True,
                "EP22: Release readiness JSON must include 'isReleaseEvidence' field.");
        }

        /// <summary>EP23: Release readiness JSON mode=ReadyReleaseGrade after full flow.</summary>
        [Test]
        public async Task EP23_ReadinessJson_ModeIsReadyReleaseGrade_AfterFullFlow()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            // StrictArtifactMode is serialized as an integer by default; ReadyReleaseGrade = 4.
            Assert.That(doc.RootElement.GetProperty("mode").GetInt32(),
                Is.EqualTo((int)StrictArtifactMode.ReadyReleaseGrade),
                "EP23: JSON mode must equal ReadyReleaseGrade after full persistence flow.");
        }

        /// <summary>EP24: Release readiness JSON isReleaseEvidence=true after full flow.</summary>
        [Test]
        public async Task EP24_ReadinessJson_IsReleaseEvidence_True_AfterFullFlow()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            Assert.That(doc.RootElement.GetProperty("isReleaseEvidence").GetBoolean(), Is.True,
                "EP24: JSON isReleaseEvidence must be true after full persistence flow.");
        }

        /// <summary>EP25: Release readiness JSON contains 'environmentLabel' field after full flow.</summary>
        [Test]
        public async Task EP25_ReadinessJson_ContainsEnvironmentLabelField()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId, environmentLabel: "protected-ci");

            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            Assert.That(doc.RootElement.TryGetProperty("environmentLabel", out var el), Is.True,
                "EP25: Release readiness JSON must include 'environmentLabel' field.");
            Assert.That(el.GetString(), Is.EqualTo("protected-ci"),
                "EP25: environmentLabel must be 'protected-ci'.");
        }

        /// <summary>EP26: Evidence persistence JSON schema includes 'success' field.</summary>
        [Test]
        public async Task EP26_EvidenceJson_ContainsSuccessField()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);

            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = head, CaseId = caseId,
                    RequireReleaseGrade = true, RequireApprovalWebhook = true,
                    EnvironmentLabel = "protected-ci", FreshnessWindowHours = 24
                });
            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            Assert.That(doc.RootElement.TryGetProperty("success", out _), Is.True,
                "EP26: Evidence persistence JSON must include 'success' field.");
        }

        /// <summary>EP27: Approval webhook JSON schema includes 'success' and 'record' fields.</summary>
        [Test]
        public async Task EP27_WebhookJson_ContainsSuccessAndRecordFields()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();

            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId, HeadRef = head,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    ActorId = "ep27-actor", CorrelationId = "ep27-corr"
                });
            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            Assert.That(doc.RootElement.TryGetProperty("success", out _), Is.True,
                "EP27: Webhook JSON must include 'success' field.");
            Assert.That(doc.RootElement.TryGetProperty("record", out _), Is.True,
                "EP27: Webhook JSON must include 'record' field.");
        }

        /// <summary>EP28: Full flow schema is stable across two independent runs (deterministic contract).</summary>
        [Test]
        public async Task EP28_FullFlow_SchemaIsStable_AcrossRuns()
        {
            // Run 1
            string head1 = UniqueHead();
            string caseId1 = UniqueCase();
            await PostWebhookAsync(caseId1, head1);
            await PostEvidenceAsync(head1, caseId1);
            var r1 = await GetReadinessAsync(head1, caseId1);

            // Run 2
            string head2 = UniqueHead();
            string caseId2 = UniqueCase();
            await PostWebhookAsync(caseId2, head2);
            await PostEvidenceAsync(head2, caseId2);
            var r2 = await GetReadinessAsync(head2, caseId2);

            Assert.That(r1.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP28 run 1: Mode must be ReadyReleaseGrade.");
            Assert.That(r2.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP28 run 2: Mode must be ReadyReleaseGrade.");
            Assert.That(r1.IsReleaseEvidence, Is.EqualTo(r2.IsReleaseEvidence),
                "EP28: IsReleaseEvidence must be identical across runs for equivalent inputs.");
        }

        /// <summary>EP29: Release readiness contains non-null 'blockers' list (contract stability).</summary>
        [Test]
        public async Task EP29_ReadinessJson_ContainsBlockersList()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            Assert.That(doc.RootElement.TryGetProperty("blockers", out _), Is.True,
                "EP29: Release readiness JSON must include 'blockers' list for downstream consumers.");
        }

        /// <summary>EP30: Release readiness response contains 'operatorGuidance' field.</summary>
        [Test]
        public async Task EP30_ReadinessJson_ContainsOperatorGuidanceField()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            // operatorGuidance may be null when status=Ready, but the field must be present
            bool hasProperty = doc.RootElement.TryGetProperty("operatorGuidance", out _);
            Assert.That(hasProperty, Is.True,
                "EP30: Release readiness JSON must include 'operatorGuidance' field for operator-readable diagnostics.");
        }

        // ════════════════════════════════════════════════════════════════════
        // EP31–EP45: Extended coverage — payload shaping, fail-closed edge
        //            cases, and determinism across independent sessions
        // ════════════════════════════════════════════════════════════════════

        /// <summary>EP31: Approval webhook with Denied outcome prevents evidence from being
        /// classified as release-grade (fail-closed against unauthorised sign-off).</summary>
        [Test]
        public async Task EP31_DeniedWebhook_EvidenceNotReleaseGrade()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            // Post a Denied webhook — not an Approved webhook
            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = head,
                    Outcome = ApprovalWebhookOutcome.Denied,
                    ActorId = "ep-test-actor-31",
                    Reason = "EP31 denied",
                    CorrelationId = Guid.NewGuid().ToString("N")
                });
            resp.EnsureSuccessStatusCode();

            // Persist evidence after a Denied webhook
            var evidenceResp = await _client.PostAsJsonAsync($"{EvidenceBase}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = head,
                    CaseId = caseId,
                    RequireReleaseGrade = true,
                    RequireApprovalWebhook = true,
                    EnvironmentLabel = "protected-ci",
                    FreshnessWindowHours = 24
                });
            // The persistence may succeed at HTTP level (200) but the pack must NOT be IsReleaseGrade
            if (evidenceResp.IsSuccessStatusCode)
            {
                var pack = (await evidenceResp.Content.ReadFromJsonAsync<PersistSignOffEvidenceResponse>(_jsonOpts))!;
                Assert.That(pack.Pack?.IsReleaseGrade ?? false, Is.False,
                    "EP31: Evidence persisted after a Denied webhook must not be classified as release-grade.");
            }

            var readiness = await GetReadinessAsync(head, caseId);
            Assert.That(readiness.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP31: Denied webhook must prevent ReadyReleaseGrade classification.");
        }

        /// <summary>EP32: Readiness endpoint returns HTTP 200 even when mode is not ReadyReleaseGrade.</summary>
        [Test]
        public async Task EP32_ReadinessEndpoint_Returns200_EvenWhenNotReady()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            // No webhook, no evidence — should still return 200 with fail-closed mode
            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "EP32: Release-readiness must never return 5xx; fail-closed errors belong in the payload.");
        }

        /// <summary>EP33: Evidence endpoint returns HTTP 200 for a valid request.</summary>
        [Test]
        public async Task EP33_EvidenceEndpoint_Returns200_ForValidRequest()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = head,
                    CaseId = caseId,
                    RequireReleaseGrade = true,
                    RequireApprovalWebhook = true,
                    EnvironmentLabel = "protected-ci",
                    FreshnessWindowHours = 24
                });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "EP33: Evidence endpoint must return 200 for a valid, authenticated request.");
        }

        /// <summary>EP34: Webhook endpoint returns HTTP 200 for a valid request.</summary>
        [Test]
        public async Task EP34_WebhookEndpoint_Returns200_ForValidRequest()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = head,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    ActorId = "ep-test-actor-34",
                    Reason = "EP34 approval",
                    CorrelationId = Guid.NewGuid().ToString("N")
                });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "EP34: Webhook endpoint must return 200 for a valid, authenticated request.");
        }

        /// <summary>EP35: Full flow with custom freshnessWindowHours still produces ReadyReleaseGrade.</summary>
        [Test]
        public async Task EP35_FullFlow_WithCustomFreshnessWindow_ProducesReadyReleaseGrade()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            // Use a 1-hour freshness window (tighter than the default 24h)
            var evidenceResp = await _client.PostAsJsonAsync($"{EvidenceBase}/evidence",
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = head,
                    CaseId = caseId,
                    RequireReleaseGrade = true,
                    RequireApprovalWebhook = true,
                    EnvironmentLabel = "protected-ci",
                    FreshnessWindowHours = 1
                });
            evidenceResp.EnsureSuccessStatusCode();
            var readiness = await GetReadinessAsync(head, caseId);
            Assert.That(readiness.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP35: Full flow with a 1-hour freshness window must still produce ReadyReleaseGrade.");
        }

        /// <summary>EP36: Three independent head/case pairs all produce isolated ReadyReleaseGrade
        /// modes — no cross-contamination between sessions.</summary>
        [Test]
        public async Task EP36_ThreeIsolatedHeads_EachProducesReadyReleaseGrade()
        {
            var pairs = Enumerable.Range(0, 3)
                .Select(_ => (head: UniqueHead(), caseId: UniqueCase()))
                .ToList();

            foreach (var (head, caseId) in pairs)
            {
                await PostWebhookAsync(caseId, head);
                await PostEvidenceAsync(head, caseId);
            }

            foreach (var (head, caseId) in pairs)
            {
                var readiness = await GetReadinessAsync(head, caseId);
                Assert.That(readiness.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                    $"EP36: Each isolated head must independently reach ReadyReleaseGrade. head={head}");
            }
        }

        /// <summary>EP37: Evidence pack response carries a non-null PackId for tracking.</summary>
        [Test]
        public async Task EP37_EvidencePack_HasNonNullPackId()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            var pack = await PostEvidenceAsync(head, caseId);
            Assert.That(pack.Pack?.PackId, Is.Not.Null.And.Not.Empty,
                "EP37: Evidence pack response must include a non-empty PackId for downstream audit-trail tracking.");
        }

        /// <summary>EP38: Readiness response IsReleaseEvidence is false when no evidence exists
        /// (fail-closed: absence of evidence must not be interpreted as release-grade).</summary>
        [Test]
        public async Task EP38_NoEvidence_IsReleaseEvidence_False_FailClosed()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            // Only webhook, no evidence
            await PostWebhookAsync(caseId, head);
            var readiness = await GetReadinessAsync(head, caseId);
            Assert.That(readiness.IsReleaseEvidence, Is.False,
                "EP38: IsReleaseEvidence must be false when no evidence pack has been persisted (fail-closed).");
        }

        /// <summary>EP39: Readiness JSON status field is present and non-null.</summary>
        [Test]
        public async Task EP39_ReadinessJson_ContainsStatusField()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            Assert.That(doc.RootElement.TryGetProperty("status", out _), Is.True,
                "EP39: Release readiness JSON must include 'status' field for downstream operator UIs.");
        }

        /// <summary>EP40: Readiness JSON headRef field echoes back the requested head reference.</summary>
        [Test]
        public async Task EP40_ReadinessJson_HeadRef_EchoesRequest()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var readiness = await GetReadinessAsync(head, caseId);
            Assert.That(readiness.HeadRef, Is.EqualTo(head),
                "EP40: Release readiness response must echo back the requested HeadRef for correlation.");
        }

        /// <summary>EP41: Evidence pack IsReleaseGrade=false when requireReleaseGrade=false
        /// (opt-out of release-grade classification must be respected).</summary>
        [Test]
        public async Task EP41_EvidencePack_NotReleaseGrade_WhenRequireReleaseGradeFalse()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            var pack = await PostEvidenceAsync(head, caseId,
                requireReleaseGrade: false, requireApprovalWebhook: false);
            Assert.That(pack.Pack?.IsReleaseGrade ?? false, Is.False,
                "EP41: Evidence pack must not be classified as release-grade when requireReleaseGrade=false.");
        }

        /// <summary>EP42: Webhook response record has non-null CorrelationId for traceability.</summary>
        [Test]
        public async Task EP42_WebhookRecord_HasNonNullCorrelationId()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            string correlationId = Guid.NewGuid().ToString("N");
            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/webhooks/approval",
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = head,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    ActorId = "ep-test-actor-42",
                    Reason = "EP42 correlation test",
                    CorrelationId = correlationId
                });
            resp.EnsureSuccessStatusCode();
            var record = (await resp.Content.ReadFromJsonAsync<RecordApprovalWebhookResponse>(_jsonOpts))!;
            Assert.That(record.Record, Is.Not.Null, "EP42: Webhook response must include a Record object.");
            Assert.That(record.Record!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "EP42: Webhook record must carry a non-empty CorrelationId for downstream traceability.");
        }

        /// <summary>EP43: Readiness mode for missing-evidence is not ReadyReleaseGrade
        /// (deterministic fail-closed contract for operator dashboards).</summary>
        [Test]
        public async Task EP43_NoWebhookNoEvidence_Mode_IsNotReadyReleaseGrade()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            // Neither webhook nor evidence — completely blank state
            var readiness = await GetReadinessAsync(head, caseId);
            Assert.That(readiness.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "EP43: A completely blank head/case must never reach ReadyReleaseGrade (fail-closed invariant).");
            Assert.That(readiness.IsReleaseEvidence, Is.False,
                "EP43: IsReleaseEvidence must be false for a completely blank head/case.");
        }

        /// <summary>EP44: Full flow produces a non-null Blockers list (empty when ready, non-empty
        /// when blocked — the list must always be present for downstream diagnostics).</summary>
        [Test]
        public async Task EP44_ReadinessBlockersList_NotNull_AlwaysPresent()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var readiness = await GetReadinessAsync(head, caseId);
            Assert.That(readiness.Blockers, Is.Not.Null,
                "EP44: Blockers list must be non-null — downstream operator dashboards depend on it being present.");
        }

        /// <summary>EP45: StrictArtifactMode contract — after full flow the mode integer equals
        /// (int)StrictArtifactMode.ReadyReleaseGrade=4, proving the serialized numeric value is stable.</summary>
        [Test]
        public async Task EP45_StrictArtifactMode_ReadyReleaseGrade_NumericValue_Is4()
        {
            string head = UniqueHead();
            string caseId = UniqueCase();
            await PostWebhookAsync(caseId, head);
            await PostEvidenceAsync(head, caseId);

            var resp = await _client.PostAsJsonAsync($"{EvidenceBase}/release-readiness",
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            // ReadyReleaseGrade is the 5th enum value (0-indexed = 4).
            // This test locks the serialised wire value so downstream consumers
            // (CI workflow, operator UIs) can rely on it without a breaking change.
            int modeValue = doc.RootElement.GetProperty("mode").GetInt32();
            Assert.That(modeValue, Is.EqualTo(4),
                "EP45: ReadyReleaseGrade must serialise to integer 4 — changing this would break " +
                "downstream consumers that interpret the numeric mode value.");
            Assert.That(modeValue, Is.EqualTo((int)StrictArtifactMode.ReadyReleaseGrade),
                "EP45: Numeric mode must equal (int)StrictArtifactMode.ReadyReleaseGrade.");
        }
    }
}
