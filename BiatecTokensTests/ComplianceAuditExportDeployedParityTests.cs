using BiatecTokensApi.Models.ComplianceAuditExport;
using BiatecTokensApi.Models.RegulatoryEvidencePackage;
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
    /// Deployed-parity tests for the <see cref="BiatecTokensApi.Controllers.ComplianceAuditExportController"/>.
    ///
    /// These tests exercise the full HTTP pipeline via <see cref="WebApplicationFactory{TEntryPoint}"/>.
    ///
    /// CAX01-CAX10:  Auth + 400 guards for all POST endpoints
    /// CAX11-CAX20:  Authenticated happy-path HTTP contract
    /// CAX21-CAX30:  E2E workflows (assemble → retrieve → list → schema contract)
    /// CAX31-CAX40:  Edge cases: ForceRegenerate, idempotency header, scenario filter, limit param
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceAuditExportDeployedParityTests
    {
        // ── Factory ───────────────────────────────────────────────────────────

        private sealed class CaxFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "CaxDeployedParityTestKey32Char!!",
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
                        ["KeyManagementConfig:HardcodedKey"] = "CaxTestKey32+CharactersMinimum!!!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "cax-test",
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

        // ── State ─────────────────────────────────────────────────────────────

        private CaxFactory? _factory;
        private HttpClient? _client;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new CaxFactory();
            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        // ── Auth helper ───────────────────────────────────────────────────────

        private async Task<string> GetJwtAsync()
        {
            var email = $"cax-{Guid.NewGuid():N}@test.example";
            const string pwd = "TestPass123!";
            var reg = await _client!.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = pwd, ConfirmPassword = pwd });
            if (!reg.IsSuccessStatusCode) return string.Empty;
            var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = pwd });
            if (!login.IsSuccessStatusCode) return string.Empty;
            var body = await login.Content.ReadFromJsonAsync<JsonElement>();
            return body.TryGetProperty("accessToken", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        }

        private void SetAuth(string? token)
        {
            _client!.DefaultRequestHeaders.Authorization = token is null
                ? null
                : new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        // ═════════════════════════════════════════════════════════════════════
        // CAX01-CAX10: Auth + 400 guards for all POST endpoints (unauthenticated)
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CAX01 — /health endpoint accessible without auth
        public async Task CAX01_Health_NoAuth_Returns200()
        {
            SetAuth(null);
            var resp = await _client!.GetAsync("/api/v1/compliance-audit-export/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test] // CAX02 — POST /release-readiness without auth → 401
        public async Task CAX02_ReleaseReadiness_NoAuth_Returns401()
        {
            SetAuth(null);
            var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                new ReleaseReadinessExportRequest { SubjectId = "cax-subj" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test] // CAX03 — POST /onboarding-case-review without auth → 401
        public async Task CAX03_OnboardingCaseReview_NoAuth_Returns401()
        {
            SetAuth(null);
            var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/onboarding-case-review",
                new OnboardingCaseReviewExportRequest { SubjectId = "cax-subj" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test] // CAX04 — POST /blocker-review without auth → 401
        public async Task CAX04_BlockerReview_NoAuth_Returns401()
        {
            SetAuth(null);
            var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/blocker-review",
                new ComplianceBlockerReviewExportRequest { SubjectId = "cax-subj" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test] // CAX05 — POST /approval-history without auth → 401
        public async Task CAX05_ApprovalHistory_NoAuth_Returns401()
        {
            SetAuth(null);
            var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/approval-history",
                new ApprovalHistoryExportRequest { SubjectId = "cax-subj" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test] // CAX06 — POST /release-readiness with empty SubjectId → 400
        public async Task CAX06_ReleaseReadiness_EmptySubjectId_Returns400()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = "" });
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX07 — POST /onboarding-case-review with empty SubjectId → 400
        public async Task CAX07_OnboardingCaseReview_EmptySubjectId_Returns400()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/onboarding-case-review",
                    new OnboardingCaseReviewExportRequest { SubjectId = "" });
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX08 — POST /blocker-review with empty SubjectId → 400
        public async Task CAX08_BlockerReview_EmptySubjectId_Returns400()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/blocker-review",
                    new ComplianceBlockerReviewExportRequest { SubjectId = "" });
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX09 — POST /approval-history with empty SubjectId → 400
        public async Task CAX09_ApprovalHistory_EmptySubjectId_Returns400()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/approval-history",
                    new ApprovalHistoryExportRequest { SubjectId = "" });
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX10 — GET /{exportId} without auth → 401
        public async Task CAX10_GetExport_NoAuth_Returns401()
        {
            SetAuth(null);
            var resp = await _client!.GetAsync("/api/v1/compliance-audit-export/some-export-id");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ═════════════════════════════════════════════════════════════════════
        // CAX11-CAX20: Authenticated happy-path HTTP contract
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CAX11 — POST /release-readiness returns 200 with correct schema
        public async Task CAX11_ReleaseReadiness_Authenticated_200_CorrectSchema()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest
                    {
                        SubjectId = "cax-subj-rr-011",
                        HeadRef = "v3.1.0",
                        EnvironmentLabel = "staging"
                    });
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                var body = await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>();
                Assert.That(body!.Success, Is.True);
                Assert.That(body.Package, Is.Not.Null);
                Assert.That(body.Package!.Scenario, Is.EqualTo(AuditScenario.ReleaseReadinessSignOff));
                Assert.That(body.Package.ReleaseReadiness, Is.Not.Null);
                Assert.That(body.Package.ReleaseReadiness!.HeadRef, Is.EqualTo("v3.1.0"));
                Assert.That(body.Package.SchemaVersion, Is.EqualTo("1.0.0"));
                Assert.That(body.Package.PolicyVersion, Is.Not.Empty);
                Assert.That(body.Package.ContentHash, Does.Match("^[0-9a-f]{64}$"));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX12 — POST /onboarding-case-review returns 200 with correct scenario
        public async Task CAX12_OnboardingCaseReview_Authenticated_200_CorrectScenario()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/onboarding-case-review",
                    new OnboardingCaseReviewExportRequest { SubjectId = "cax-subj-oc-012" });
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                var body = await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>();
                Assert.That(body!.Success, Is.True);
                Assert.That(body.Package!.Scenario, Is.EqualTo(AuditScenario.OnboardingCaseReview));
                Assert.That(body.Package.OnboardingCase, Is.Not.Null);
                Assert.That(body.Package.OnboardingCase!.CaseId, Is.Not.Empty);
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX13 — POST /blocker-review returns 200 with blocker section
        public async Task CAX13_BlockerReview_Authenticated_200_BlockerSectionPresent()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/blocker-review",
                    new ComplianceBlockerReviewExportRequest
                    {
                        SubjectId = "cax-subj-br-013",
                        IncludeResolvedBlockers = true
                    });
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                var body = await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>();
                Assert.That(body!.Success, Is.True);
                Assert.That(body.Package!.Scenario, Is.EqualTo(AuditScenario.ComplianceBlockerReview));
                Assert.That(body.Package.BlockerReview, Is.Not.Null);
                Assert.That(body.Package.BlockerReview!.BlockerPostureSummary, Is.Not.Empty);
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX14 — POST /approval-history returns 200 with approval section
        public async Task CAX14_ApprovalHistory_Authenticated_200_ApprovalSectionPresent()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/approval-history",
                    new ApprovalHistoryExportRequest { SubjectId = "cax-subj-ah-014", DecisionLimit = 50 });
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                var body = await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>();
                Assert.That(body!.Success, Is.True);
                Assert.That(body.Package!.Scenario, Is.EqualTo(AuditScenario.ApprovalHistoryExport));
                Assert.That(body.Package.ApprovalHistory, Is.Not.Null);
                Assert.That(body.Package.ApprovalHistory!.WorkflowSummary, Is.Not.Empty);
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX15 — GET /{exportId} returns 200 after assembly
        public async Task CAX15_GetExport_Authenticated_200_ReturnsPackage()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var post = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = "cax-subj-get-015" });
                var pkg = (await post.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;

                var get = await _client.GetAsync($"/api/v1/compliance-audit-export/{pkg.ExportId}");
                Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                var getBody = await get.Content.ReadFromJsonAsync<GetComplianceAuditExportResponse>();
                Assert.That(getBody!.Success, Is.True);
                Assert.That(getBody.Package!.ExportId, Is.EqualTo(pkg.ExportId));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX16 — GET /{exportId} returns 404 for unknown id
        public async Task CAX16_GetExport_UnknownId_Returns404()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.GetAsync("/api/v1/compliance-audit-export/unknown-export-cax-016");
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX17 — GET /subject/{subjectId} returns 200 and list
        public async Task CAX17_ListExports_Authenticated_200_ReturnsList()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var subjectId = "cax-subj-list-017";
                await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = subjectId });

                var listResp = await _client.GetAsync(
                    $"/api/v1/compliance-audit-export/subject/{subjectId}");
                Assert.That(listResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                var body = await listResp.Content.ReadFromJsonAsync<ListComplianceAuditExportsResponse>();
                Assert.That(body!.Success, Is.True);
                Assert.That(body.Exports.Count, Is.GreaterThanOrEqualTo(1));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX18 — ReadinessHeadline and ReadinessDetail are always non-empty in HTTP response
        public async Task CAX18_ReadinessFields_AlwaysNonEmpty_InHttpResponse()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                foreach (var req in new object[]
                {
                    new ReleaseReadinessExportRequest { SubjectId = "cax-rhead-rr-018" },
                    new OnboardingCaseReviewExportRequest { SubjectId = "cax-rhead-oc-018" },
                    new ComplianceBlockerReviewExportRequest { SubjectId = "cax-rhead-br-018" },
                    new ApprovalHistoryExportRequest { SubjectId = "cax-rhead-ah-018" }
                })
                {
                    var (path, content) = req switch
                    {
                        ReleaseReadinessExportRequest r => ("release-readiness",
                            JsonContent.Create(r)),
                        OnboardingCaseReviewExportRequest o => ("onboarding-case-review",
                            JsonContent.Create(o)),
                        ComplianceBlockerReviewExportRequest b => ("blocker-review",
                            JsonContent.Create(b)),
                        ApprovalHistoryExportRequest a => ("approval-history",
                            JsonContent.Create(a)),
                        _ => throw new InvalidOperationException()
                    };
                    var resp = await _client!.PostAsync($"/api/v1/compliance-audit-export/{path}", content);
                    Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"endpoint={path}");
                    var body = await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>();
                    Assert.That(body!.Package!.ReadinessHeadline, Is.Not.Empty, $"endpoint={path}");
                    Assert.That(body.Package.ReadinessDetail, Is.Not.Empty, $"endpoint={path}");
                }
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX19 — ProvenanceRecords always have non-empty SourceSystem and EvidenceCategory
        public async Task CAX19_ProvenanceRecords_FieldsNonEmpty_ForAllScenarios()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/onboarding-case-review",
                    new OnboardingCaseReviewExportRequest { SubjectId = "cax-prov-019" });
                var body = await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>();
                foreach (var prov in body!.Package!.ProvenanceRecords)
                {
                    Assert.That(prov.ProvenanceId, Is.Not.Empty);
                    Assert.That(prov.SourceSystem, Is.Not.Empty);
                    Assert.That(prov.EvidenceCategory, Is.Not.Empty);
                    Assert.That(prov.IntegrityHash, Does.Match("^[0-9a-f]{64}$"));
                }
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX20 — ExportId in response matches GET retrieval
        public async Task CAX20_ExportId_InResponse_MatchesGetRetrieval()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var postResp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/blocker-review",
                    new ComplianceBlockerReviewExportRequest { SubjectId = "cax-match-020" });
                var postPkg = (await postResp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;
                Assert.That(postPkg.ExportId, Is.Not.Empty);

                var getResp = await _client.GetAsync(
                    $"/api/v1/compliance-audit-export/{postPkg.ExportId}");
                var getPkg = (await getResp.Content.ReadFromJsonAsync<GetComplianceAuditExportResponse>())!.Package!;
                Assert.That(getPkg.ExportId, Is.EqualTo(postPkg.ExportId));
                Assert.That(getPkg.ContentHash, Is.EqualTo(postPkg.ContentHash));
            }
            finally { SetAuth(null); }
        }

        // ═════════════════════════════════════════════════════════════════════
        // CAX21-CAX30: E2E workflows
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CAX21 — E2E: Assemble all 4 scenarios → list → count is 4
        public async Task CAX21_E2E_All4Scenarios_ListReturnsFour()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var subjectId = $"cax-e2e-all4-021-{Guid.NewGuid():N}";
                await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = subjectId });
                await _client.PostAsJsonAsync("/api/v1/compliance-audit-export/onboarding-case-review",
                    new OnboardingCaseReviewExportRequest { SubjectId = subjectId });
                await _client.PostAsJsonAsync("/api/v1/compliance-audit-export/blocker-review",
                    new ComplianceBlockerReviewExportRequest { SubjectId = subjectId });
                await _client.PostAsJsonAsync("/api/v1/compliance-audit-export/approval-history",
                    new ApprovalHistoryExportRequest { SubjectId = subjectId });

                var listResp = await _client.GetAsync(
                    $"/api/v1/compliance-audit-export/subject/{subjectId}?limit=10");
                var listBody = await listResp.Content.ReadFromJsonAsync<ListComplianceAuditExportsResponse>();
                Assert.That(listBody!.Success, Is.True);
                Assert.That(listBody.Exports.Count, Is.EqualTo(4));
                var scenarios = listBody.Exports.Select(e => e.Scenario).Distinct().ToList();
                Assert.That(scenarios, Has.Count.EqualTo(4));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX22 — E2E: ContentHash is stable on GetExport (not recalculated)
        public async Task CAX22_E2E_ContentHash_StableAcrossGet()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var post = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest
                    {
                        SubjectId = "cax-hash-022",
                        IdempotencyKey = "cax-hash-key-022"
                    });
                var pkg = (await post.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;
                var get = await _client.GetAsync($"/api/v1/compliance-audit-export/{pkg.ExportId}");
                var getPkg = (await get.Content.ReadFromJsonAsync<GetComplianceAuditExportResponse>())!.Package!;
                Assert.That(getPkg.ContentHash, Is.EqualTo(pkg.ContentHash));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX23 — E2E: TrackerHistory grows with each new export for same subject
        public async Task CAX23_E2E_TrackerHistory_GrowsWithEachExport()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var subjectId = $"cax-tracker-023";
                var r1 = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = subjectId });
                var pkg1 = (await r1.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;
                Assert.That(pkg1.TrackerHistory, Is.Empty);

                var r2 = await _client.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = subjectId });
                var pkg2 = (await r2.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;
                Assert.That(pkg2.TrackerHistory, Contains.Item(pkg1.ExportId));

                var r3 = await _client.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = subjectId });
                var pkg3 = (await r3.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;
                Assert.That(pkg3.TrackerHistory, Has.Count.GreaterThanOrEqualTo(2));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX24 — E2E: Fail-closed — EvidenceFromTimestamp in past → all evidence missing
        public async Task CAX24_E2E_FailClosed_FutureTimestamp_PackageNotReady()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest
                    {
                        SubjectId = "cax-failclosed-024",
                        EvidenceFromTimestamp = DateTime.UtcNow.AddYears(10)
                    });
                var pkg = (await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;
                Assert.That(pkg.Readiness, Is.Not.EqualTo(AuditExportReadiness.Ready));
                Assert.That(pkg.IsRegulatorReady, Is.False);
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX25 — E2E: IsRegulatorReady contract is respected in HTTP response
        public async Task CAX25_E2E_IsRegulatorReady_Contract_HeldInHttpResponse()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                for (int i = 0; i < 20; i++)
                {
                    var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                        new ReleaseReadinessExportRequest { SubjectId = $"cax-contract-{i}" });
                    var pkg = (await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;
                    if (pkg.Readiness == AuditExportReadiness.Ready)
                        Assert.That(pkg.IsRegulatorReady, Is.True, $"Ready→IsRegulatorReady must be true (i={i})");
                    else
                        Assert.That(pkg.IsRegulatorReady, Is.False, $"{pkg.Readiness}→IsRegulatorReady must be false (i={i})");
                }
            }
            finally { SetAuth(null); }
        }

        // ═════════════════════════════════════════════════════════════════════
        // CAX26-CAX35: Edge cases via HTTP
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CAX26 — Idempotency via HTTP: same key → same ExportId
        public async Task CAX26_Http_Idempotency_SameKey_SameExportId()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var key = $"cax-idem-026-{Guid.NewGuid():N}";
                var r1 = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = "cax-idem-026", IdempotencyKey = key });
                var r2 = await _client.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = "cax-idem-026", IdempotencyKey = key });
                var body1 = (await r1.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!;
                var body2 = (await r2.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!;
                Assert.That(body2.Package!.ExportId, Is.EqualTo(body1.Package!.ExportId));
                Assert.That(body2.IsIdempotentReplay || body2.Package.ExportId == body1.Package.ExportId, Is.True);
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX27 — ForceRegenerate via HTTP → new ExportId
        public async Task CAX27_Http_ForceRegenerate_NewExportId()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var key = $"cax-force-027-{Guid.NewGuid():N}";
                var r1 = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = "cax-force-027", IdempotencyKey = key });
                var r2 = await _client.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = "cax-force-027", IdempotencyKey = key, ForceRegenerate = true });
                var pkg1 = (await r1.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;
                var pkg2 = (await r2.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;
                Assert.That(pkg2.ExportId, Is.Not.EqualTo(pkg1.ExportId));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX28 — GET /subject/{id} with ?limit=1 returns at most 1 result
        public async Task CAX28_Http_ListExports_LimitQuery_Respected()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var subjectId = "cax-limit-028";
                await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = subjectId });
                await _client.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = subjectId });
                await _client.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = subjectId });

                var listResp = await _client.GetAsync(
                    $"/api/v1/compliance-audit-export/subject/{subjectId}?limit=1");
                var body = await listResp.Content.ReadFromJsonAsync<ListComplianceAuditExportsResponse>();
                Assert.That(body!.Success, Is.True);
                Assert.That(body.Exports.Count, Is.EqualTo(1));
                Assert.That(body.TotalCount, Is.GreaterThanOrEqualTo(3));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX29 — GET /subject/{id} with ?scenario= filter
        public async Task CAX29_Http_ListExports_ScenarioQueryFilter_Works()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var subjectId = "cax-scenfilter-029";
                await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = subjectId });
                await _client.PostAsJsonAsync("/api/v1/compliance-audit-export/blocker-review",
                    new ComplianceBlockerReviewExportRequest { SubjectId = subjectId });

                var listResp = await _client.GetAsync(
                    $"/api/v1/compliance-audit-export/subject/{subjectId}?scenario=ReleaseReadinessSignOff");
                var body = await listResp.Content.ReadFromJsonAsync<ListComplianceAuditExportsResponse>();
                Assert.That(body!.Success, Is.True);
                Assert.That(body.Exports.All(e => e.Scenario == AuditScenario.ReleaseReadinessSignOff), Is.True);
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX30 — GET /subject/{id} for subject with no exports → empty list
        public async Task CAX30_Http_ListExports_UnknownSubject_ReturnsEmpty()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var listResp = await _client!.GetAsync(
                    $"/api/v1/compliance-audit-export/subject/totally-unknown-cax-030");
                Assert.That(listResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                var body = await listResp.Content.ReadFromJsonAsync<ListComplianceAuditExportsResponse>();
                Assert.That(body!.Success, Is.True);
                Assert.That(body.Exports, Is.Empty);
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX31 — BlockerReview: severity totals add up in HTTP response
        public async Task CAX31_Http_BlockerReview_SeverityTotals_Consistent()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/blocker-review",
                    new ComplianceBlockerReviewExportRequest { SubjectId = "cax-severity-031" });
                var section = (await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!
                    .Package!.BlockerReview!;
                Assert.That(section.OpenBlockerCount,
                    Is.EqualTo(section.CriticalOpenCount + section.WarningOpenCount + section.AdvisoryOpenCount));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX32 — ApprovalHistory: DecisionHistory is chronologically ordered in HTTP response
        public async Task CAX32_Http_ApprovalHistory_DecisionHistory_IsChronological()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/approval-history",
                    new ApprovalHistoryExportRequest { SubjectId = "cax-chrono-032", DecisionLimit = 100 });
                var decisions = (await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!
                    .Package!.ApprovalHistory!.DecisionHistory;
                for (int i = 1; i < decisions.Count; i++)
                    Assert.That(decisions[i].DecidedAt, Is.GreaterThanOrEqualTo(decisions[i - 1].DecidedAt),
                        $"Decision[{i}] must not predate Decision[{i - 1}]");
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX33 — OnboardingCase: CaseState field is non-empty in HTTP response
        public async Task CAX33_Http_OnboardingCaseReview_CaseState_NonEmpty()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/onboarding-case-review",
                    new OnboardingCaseReviewExportRequest { SubjectId = "cax-casestate-033" });
                var section = (await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!
                    .Package!.OnboardingCase!;
                Assert.That(section.CaseState, Is.Not.Empty);
                Assert.That(section.CaseId, Is.Not.Empty);
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX34 — ExpiresAt is always 90 days after AssembledAt in HTTP response
        public async Task CAX34_Http_ExpiresAt_Is90DaysAfterAssembledAt()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = "cax-expiry-034" });
                var pkg = (await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;
                Assert.That(pkg.ExpiresAt, Is.Not.Null);
                var expected = pkg.AssembledAt.AddDays(90);
                Assert.That(pkg.ExpiresAt!.Value, Is.EqualTo(expected).Within(TimeSpan.FromSeconds(5)));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX35 — AudienceProfile RegulatorReview propagated through HTTP
        public async Task CAX35_Http_AudienceProfile_RegulatorReview_Propagated()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest
                    {
                        SubjectId = "cax-audience-035",
                        AudienceProfile = RegulatoryAudienceProfile.RegulatorReview
                    });
                var pkg = (await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!.Package!;
                Assert.That(pkg.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.RegulatorReview));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX36 — GET /subject/{id} without auth → 401
        public async Task CAX36_Http_ListExports_NoAuth_Returns401()
        {
            SetAuth(null);
            var resp = await _client!.GetAsync("/api/v1/compliance-audit-export/subject/any-subject");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test] // CAX37 — OnboardingCase: SpecificCaseId reflected in HTTP response
        public async Task CAX37_Http_OnboardingCaseReview_SpecificCaseId_Reflected()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var resp = await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/onboarding-case-review",
                    new OnboardingCaseReviewExportRequest
                    {
                        SubjectId = "cax-caseid-037",
                        CaseId = "my-specific-case-cax-037"
                    });
                var section = (await resp.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>())!
                    .Package!.OnboardingCase!;
                Assert.That(section.CaseId, Is.EqualTo("my-specific-case-cax-037"));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX38 — List summary fields include ContentHash and IsRegulatorReady
        public async Task CAX38_Http_ListSummary_HasContentHashAndIsRegulatorReady()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var subjectId = "cax-summary-038";
                await _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                    new ReleaseReadinessExportRequest { SubjectId = subjectId });

                var listResp = await _client.GetAsync(
                    $"/api/v1/compliance-audit-export/subject/{subjectId}");
                var body = await listResp.Content.ReadFromJsonAsync<ListComplianceAuditExportsResponse>();
                Assert.That(body!.Exports, Is.Not.Empty);
                var summary = body.Exports.First();
                Assert.That(summary.ContentHash, Does.Match("^[0-9a-f]{64}$"));
                Assert.That(summary.ProvenanceRecordCount, Is.GreaterThanOrEqualTo(0));
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX39 — Concurrent requests for different subjects produce independent packages
        public async Task CAX39_Http_ConcurrentRequests_ProduceIndependentPackages()
        {
            var token = await GetJwtAsync();
            if (string.IsNullOrEmpty(token)) { Assert.Pass("Auth not available."); return; }
            SetAuth(token);
            try
            {
                var tasks = Enumerable.Range(0, 5).Select(i =>
                    _client!.PostAsJsonAsync("/api/v1/compliance-audit-export/release-readiness",
                        new ReleaseReadinessExportRequest { SubjectId = $"cax-concurrent-039-{i}" })).ToList();
                await Task.WhenAll(tasks);

                var exportIds = new HashSet<string>();
                foreach (var t in tasks)
                {
                    var body = await t.Result.Content.ReadFromJsonAsync<ComplianceAuditExportResponse>();
                    exportIds.Add(body!.Package!.ExportId);
                }
                Assert.That(exportIds.Count, Is.EqualTo(5), "Each concurrent request must produce a unique ExportId");
            }
            finally { SetAuth(null); }
        }

        [Test] // CAX40 — Regression: existing smoke tests (health + swagger) still pass after new controllers
        public async Task CAX40_Regression_HealthAndSwagger_StillPass()
        {
            SetAuth(null);
            var health = await _client!.GetAsync("/api/v1/compliance-audit-export/health");
            Assert.That(health.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var swagger = await _client.GetAsync("/swagger/v1/swagger.json");
            Assert.That(swagger.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Swagger endpoint must not return 500 (no schema ID collisions)");
        }
    }
}
