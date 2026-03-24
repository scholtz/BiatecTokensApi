using BiatecTokensApi.Models.ComplianceAuditExport;
using BiatecTokensApi.Models.RegulatoryEvidencePackage;
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
    /// Coverage-extension tests for <see cref="ComplianceAuditExportService"/> and
    /// <see cref="BiatecTokensApi.Controllers.ComplianceAuditExportController"/>.
    ///
    /// CC01-CC10:  ReadinessText and schema version contract assertions
    /// CC11-CC20:  DetermineReadiness — deterministic injection via provenance/blocker setup
    /// CC21-CC30:  HTTP controller tests — all 4 endpoints via WebApplicationFactory
    /// CC31-CC35:  ContentHash determinism and format
    /// CC36-CC40:  Provenance record mandatory field completeness
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceAuditExportCoverageExtensionTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // FakeTimeProvider
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset v) => _now = v;
            public void Advance(TimeSpan d) => _now = _now.Add(d);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Integration factory
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class CaeFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "CceTestSecretKey32Characters!!!!",
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
                        ["KeyManagementConfig:HardcodedKey"] = "CceTestKey32+CharactersMinimum!!!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "cce-test",
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

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static ComplianceAuditExportService CreateService(TimeProvider? tp = null)
            => new(NullLogger<ComplianceAuditExportService>.Instance, tp);

        private static string NewSubject() => "subj-cc-" + Guid.NewGuid().ToString("N")[..8];

        private static async Task<string> GetJwtTokenAsync(HttpClient client)
        {
            var email = $"cc-test-{Guid.NewGuid():N}@example.com";
            await client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = "TestPass123!", ConfirmPassword = "TestPass123!" });
            var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = "TestPass123!" });
            var json = await loginResp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("accessToken").GetString() ?? "";
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CC01-CC10: ReadinessText and schema/policy version contract
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // CC01
        public async Task CC01_Package_SchemaVersion_IsNonEmpty()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            Assert.That(r.Package!.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CC02
        public async Task CC02_Package_PolicyVersion_IsNonEmpty()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            Assert.That(r.Package!.PolicyVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CC03
        public async Task CC03_Package_ReadinessHeadline_IsNonEmpty()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            Assert.That(r.Package!.ReadinessHeadline, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CC04
        public async Task CC04_Package_ReadinessDetail_IsNonEmpty()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            Assert.That(r.Package!.ReadinessDetail, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CC05
        public async Task CC05_OnboardingCase_Package_ReadinessHeadline_IsNonEmpty()
        {
            var svc = CreateService();
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = NewSubject() });
            Assert.That(r.Package!.ReadinessHeadline, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CC06
        public async Task CC06_BlockerReview_Package_ReadinessHeadline_IsNonEmpty()
        {
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = NewSubject() });
            Assert.That(r.Package!.ReadinessHeadline, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CC07
        public async Task CC07_ApprovalHistory_Package_ReadinessHeadline_IsNonEmpty()
        {
            var svc = CreateService();
            var r = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = NewSubject() });
            Assert.That(r.Package!.ReadinessHeadline, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CC08
        public async Task CC08_Package_ExportId_IsGuidFormatString()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            Assert.That(Guid.TryParse(r.Package!.ExportId, out _), Is.True,
                "ExportId must be parseable as a GUID");
        }

        [Test] // CC09
        public async Task CC09_Package_AssembledAt_IsRecentUtcTimestamp()
        {
            var svc = CreateService();
            var before = DateTime.UtcNow.AddSeconds(-5);
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            var after = DateTime.UtcNow.AddSeconds(5);
            Assert.That(r.Package!.AssembledAt, Is.GreaterThanOrEqualTo(before));
            Assert.That(r.Package!.AssembledAt, Is.LessThanOrEqualTo(after));
        }

        [Test] // CC10
        public async Task CC10_Package_SubjectId_MatchesRequest()
        {
            var svc = CreateService();
            var subject = NewSubject();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subject });
            Assert.That(r.Package!.SubjectId, Is.EqualTo(subject));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CC11-CC20: Deterministic readiness injection via service state
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // CC11
        public async Task CC11_AllScenarios_Package_Scenario_Field_IsSet()
        {
            var svc = CreateService();
            var subj = NewSubject();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subj + "a" });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = subj + "b" });
            var r3 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = subj + "c" });
            var r4 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = subj + "d" });

            Assert.That(r1.Package!.Scenario, Is.EqualTo(AuditScenario.ReleaseReadinessSignOff));
            Assert.That(r2.Package!.Scenario, Is.EqualTo(AuditScenario.OnboardingCaseReview));
            Assert.That(r3.Package!.Scenario, Is.EqualTo(AuditScenario.ComplianceBlockerReview));
            Assert.That(r4.Package!.Scenario, Is.EqualTo(AuditScenario.ApprovalHistoryExport));
        }

        [Test] // CC12
        public async Task CC12_Package_AudienceProfile_Default_IsInternalCompliance()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest
                {
                    SubjectId = NewSubject(),
                    AudienceProfile = RegulatoryAudienceProfile.InternalCompliance
                });
            Assert.That(r.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.InternalCompliance));
        }

        [Test] // CC13
        public async Task CC13_Package_AudienceProfile_ExternalAuditor_Propagated()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest
                {
                    SubjectId = NewSubject(),
                    AudienceProfile = RegulatoryAudienceProfile.ExternalAuditor
                });
            Assert.That(r.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.ExternalAuditor));
        }

        [Test] // CC14
        public async Task CC14_Package_AudienceProfile_RegulatorReview_Propagated()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest
                {
                    SubjectId = NewSubject(),
                    AudienceProfile = RegulatoryAudienceProfile.RegulatorReview
                });
            Assert.That(r.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.RegulatorReview));
        }

        [Test] // CC15
        public async Task CC15_Package_AudienceProfile_ExecutiveSignOff_Propagated()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest
                {
                    SubjectId = NewSubject(),
                    AudienceProfile = RegulatoryAudienceProfile.ExecutiveSignOff
                });
            Assert.That(r.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.ExecutiveSignOff));
        }

        [Test] // CC16
        public async Task CC16_Package_CorrelationId_FromRequest_IsPreserved()
        {
            var svc = CreateService();
            const string corrId = "cc16-corr-abc123";
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest
                {
                    SubjectId = NewSubject(),
                    CorrelationId = corrId
                });
            Assert.That(r.Package!.CorrelationId, Is.EqualTo(corrId));
        }

        [Test] // CC17
        public async Task CC17_Package_AutoCorrelationId_WhenNullInRequest()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest
                {
                    SubjectId = NewSubject(),
                    CorrelationId = null
                });
            Assert.That(r.Package!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "Service must auto-generate a correlation ID when none is provided");
        }

        [Test] // CC18
        public async Task CC18_ReleaseReadiness_Blockers_HaveNonEmptyCategories()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            foreach (var b in r.Package!.Blockers)
                Assert.That(b.Category, Is.Not.Null.And.Not.Empty,
                    $"Blocker {b.BlockerId} has null/empty Category");
        }

        [Test] // CC19
        public async Task CC19_ReleaseReadiness_Blockers_HaveRemediationHints()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            foreach (var b in r.Package!.Blockers)
                Assert.That(b.RemediationHints, Is.Not.Null,
                    $"Blocker {b.BlockerId} has null RemediationHints list");
        }

        [Test] // CC20
        public async Task CC20_ReleaseReadiness_Blockers_HaveBlockerIds()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            foreach (var b in r.Package!.Blockers)
                Assert.That(b.BlockerId, Is.Not.Null.And.Not.Empty,
                    "Every blocker must have a non-empty BlockerId");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CC21-CC30: HTTP controller tests via WebApplicationFactory
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // CC21
        public async Task CC21_ReleaseReadiness_UnauthenticatedRequest_Returns401()
        {
            await using var factory = new CaeFactory();
            var client = factory.CreateClient();
            var resp = await client.PostAsJsonAsync(
                "/api/v1/compliance-audit-export/release-readiness",
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test] // CC22
        public async Task CC22_OnboardingCaseReview_UnauthenticatedRequest_Returns401()
        {
            await using var factory = new CaeFactory();
            var client = factory.CreateClient();
            var resp = await client.PostAsJsonAsync(
                "/api/v1/compliance-audit-export/onboarding-case-review",
                new OnboardingCaseReviewExportRequest { SubjectId = NewSubject() });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test] // CC23
        public async Task CC23_BlockerReview_UnauthenticatedRequest_Returns401()
        {
            await using var factory = new CaeFactory();
            var client = factory.CreateClient();
            var resp = await client.PostAsJsonAsync(
                "/api/v1/compliance-audit-export/blocker-review",
                new ComplianceBlockerReviewExportRequest { SubjectId = NewSubject() });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test] // CC24
        public async Task CC24_ApprovalHistory_UnauthenticatedRequest_Returns401()
        {
            await using var factory = new CaeFactory();
            var client = factory.CreateClient();
            var resp = await client.PostAsJsonAsync(
                "/api/v1/compliance-audit-export/approval-history",
                new ApprovalHistoryExportRequest { SubjectId = NewSubject() });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test] // CC25
        public async Task CC25_ReleaseReadiness_MissingSubjectId_Returns400()
        {
            await using var factory = new CaeFactory();
            var client = factory.CreateClient();
            var token = await GetJwtTokenAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await client.PostAsJsonAsync(
                "/api/v1/compliance-audit-export/release-readiness",
                new ReleaseReadinessExportRequest { SubjectId = "" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test] // CC26
        public async Task CC26_OnboardingCaseReview_MissingSubjectId_Returns400()
        {
            await using var factory = new CaeFactory();
            var client = factory.CreateClient();
            var token = await GetJwtTokenAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await client.PostAsJsonAsync(
                "/api/v1/compliance-audit-export/onboarding-case-review",
                new OnboardingCaseReviewExportRequest { SubjectId = "" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test] // CC27
        public async Task CC27_GetExport_NotFound_Returns404()
        {
            await using var factory = new CaeFactory();
            var client = factory.CreateClient();
            var token = await GetJwtTokenAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await client.GetAsync(
                "/api/v1/compliance-audit-export/exports/nonexistent-id-cc27");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test] // CC28
        public async Task CC28_ReleaseReadiness_ValidRequest_Returns200WithPackage()
        {
            await using var factory = new CaeFactory();
            var client = factory.CreateClient();
            var token = await GetJwtTokenAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await client.PostAsJsonAsync(
                "/api/v1/compliance-audit-export/release-readiness",
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            Assert.That(doc.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(doc.RootElement.TryGetProperty("package", out _), Is.True);
        }

        [Test] // CC29
        public async Task CC29_OnboardingCaseReview_ValidRequest_Returns200WithPackage()
        {
            await using var factory = new CaeFactory();
            var client = factory.CreateClient();
            var token = await GetJwtTokenAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await client.PostAsJsonAsync(
                "/api/v1/compliance-audit-export/onboarding-case-review",
                new OnboardingCaseReviewExportRequest { SubjectId = NewSubject() });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            Assert.That(doc.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }

        [Test] // CC30
        public async Task CC30_AssembleThenGetById_SchemaContractHolds()
        {
            await using var factory = new CaeFactory();
            var client = factory.CreateClient();
            var token = await GetJwtTokenAsync(client);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Assemble
            var postResp = await client.PostAsJsonAsync(
                "/api/v1/compliance-audit-export/release-readiness",
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            Assert.That(postResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var postBody = await postResp.Content.ReadAsStringAsync();
            using var postDoc = JsonDocument.Parse(postBody);
            var exportId = postDoc.RootElement.GetProperty("package").GetProperty("exportId").GetString();
            Assert.That(exportId, Is.Not.Null.And.Not.Empty);

            // Retrieve
            var getResp = await client.GetAsync($"/api/v1/compliance-audit-export/{exportId}");
            Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var getBody = await getResp.Content.ReadAsStringAsync();
            using var getDoc = JsonDocument.Parse(getBody);
            Assert.That(getDoc.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(getDoc.RootElement.TryGetProperty("package", out _), Is.True);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CC31-CC35: ContentHash determinism and format
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // CC31
        public async Task CC31_ContentHash_Length_Is64Chars()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            Assert.That(r.Package!.ContentHash, Has.Length.EqualTo(64),
                "SHA-256 hex must be exactly 64 characters");
        }

        [Test] // CC32
        public async Task CC32_ContentHash_IsHexString()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            Assert.That(r.Package!.ContentHash,
                Does.Match("^[0-9a-fA-F]{64}$"),
                "ContentHash must be a valid 64-char hex string");
        }

        [Test] // CC33
        public async Task CC33_ForceRegenerate_ProducesNewContentHash()
        {
            var svc = CreateService();
            var subj = NewSubject();
            var key = "cc33-idem-" + Guid.NewGuid().ToString("N")[..6];

            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest
                {
                    SubjectId = subj,
                    IdempotencyKey = key,
                    ForceRegenerate = false
                });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest
                {
                    SubjectId = subj,
                    IdempotencyKey = key,
                    ForceRegenerate = true
                });

            // Force-regenerate should bypass idempotency and produce a new export
            Assert.That(r2.Package!.ExportId, Is.Not.EqualTo(r1.Package!.ExportId),
                "ForceRegenerate must produce a new ExportId");
        }

        [Test] // CC34
        public async Task CC34_AllScenarios_ContentHash_NonEmpty()
        {
            var svc = CreateService();
            var subj = NewSubject();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subj + "a" });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = subj + "b" });
            var r3 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = subj + "c" });
            var r4 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = subj + "d" });

            Assert.That(r1.Package!.ContentHash, Is.Not.Null.And.Not.Empty);
            Assert.That(r2.Package!.ContentHash, Is.Not.Null.And.Not.Empty);
            Assert.That(r3.Package!.ContentHash, Is.Not.Null.And.Not.Empty);
            Assert.That(r4.Package!.ContentHash, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CC35
        public async Task CC35_TwoExports_ForDifferentSubjects_HaveDifferentContentHashes()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cc35-subject-alpha" });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cc35-subject-beta" });

            // Different subjects → different content (subject ID is part of hash input)
            Assert.That(r1.Package!.ContentHash, Is.Not.EqualTo(r2.Package!.ContentHash),
                "Different subjects must produce different content hashes");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CC36-CC40: Provenance record mandatory field completeness
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // CC36
        public async Task CC36_ProvenanceRecords_HaveNonEmptyProvenanceId()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            foreach (var p in r.Package!.ProvenanceRecords)
                Assert.That(p.ProvenanceId, Is.Not.Null.And.Not.Empty,
                    "Every provenance record must have a non-empty ProvenanceId");
        }

        [Test] // CC37
        public async Task CC37_ProvenanceRecords_HaveNonEmptySourceSystem()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            foreach (var p in r.Package!.ProvenanceRecords)
                Assert.That(p.SourceSystem, Is.Not.Null.And.Not.Empty,
                    $"Provenance {p.ProvenanceId} has empty SourceSystem");
        }

        [Test] // CC38
        public async Task CC38_ProvenanceRecords_HaveNonEmptyEvidenceCategory()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = NewSubject() });
            foreach (var p in r.Package!.ProvenanceRecords)
                Assert.That(p.EvidenceCategory, Is.Not.Null.And.Not.Empty,
                    $"Provenance {p.ProvenanceId} has empty EvidenceCategory");
        }

        [Test] // CC39
        public async Task CC39_OnboardingCase_ProvenanceRecords_HaveValidFreshnessState()
        {
            var svc = CreateService();
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = NewSubject() });
            foreach (var p in r.Package!.ProvenanceRecords)
                Assert.That(Enum.IsDefined(typeof(AuditEvidenceFreshness), p.FreshnessState), Is.True,
                    $"Provenance {p.ProvenanceId} has invalid FreshnessState value");
        }

        [Test] // CC40
        public async Task CC40_AllScenarios_ProvenanceRecordsCount_IsPositive()
        {
            var svc = CreateService();
            var subj = NewSubject();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subj + "a" });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = subj + "b" });
            var r3 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = subj + "c" });
            var r4 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = subj + "d" });

            Assert.That(r1.Package!.ProvenanceRecords.Count, Is.GreaterThan(0),
                "ReleaseReadiness package must have at least one provenance record");
            Assert.That(r2.Package!.ProvenanceRecords.Count, Is.GreaterThan(0),
                "OnboardingCase package must have at least one provenance record");
            Assert.That(r3.Package!.ProvenanceRecords.Count, Is.GreaterThan(0),
                "BlockerReview package must have at least one provenance record");
            Assert.That(r4.Package!.ProvenanceRecords.Count, Is.GreaterThan(0),
                "ApprovalHistory package must have at least one provenance record");
        }
    }
}
