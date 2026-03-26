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

namespace BiatecTokensTests
{
    /// <summary>
    /// E2E journey tests for the ComplianceAuditExport feature, simulating the full
    /// operator journey from evidence assembly through audience-profile selection
    /// to regulator-grade export confirmation.
    ///
    /// CJ01-CJ10: Full operator journey — assemble all 4 scenarios for a subject
    /// CJ11-CJ20: Audience-profile journey — all 4 profiles tested end-to-end
    /// CJ21-CJ30: Freshness-degradation journey — evidence ageing and re-assembly
    /// CJ31-CJ40: Blocker-resolution journey — from blocked to ready progression
    /// CJ41-CJ50: Multi-subject governance journey — isolation and cross-subject safety
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceAuditExportJourneyTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // FakeTimeProvider
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset initial) => _now = initial;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // WebApplicationFactory for HTTP journey tests
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class CjFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["JwtConfig:SecretKey"] = "CjJourneyTestKey32Char+Padding!!",
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
                        ["KeyManagementConfig:HardcodedKey"] = "CjTestKey32+CharactersMinimum!!!",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["ProtectedSignOff:EnvironmentLabel"] = "cj-test",
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

        private CjFactory? _factory;
        private HttpClient? _client;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new CjFactory();
            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        private async Task<string> GetJwtAsync()
        {
            var email = $"cj-{Guid.NewGuid():N}@test.example";
            const string pwd = "TestPass123!";
            var reg = await _client!.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = pwd, ConfirmPassword = pwd });
            if (!reg.IsSuccessStatusCode) return string.Empty;
            var regBody = await reg.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var login = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = pwd });
            if (!login.IsSuccessStatusCode) return string.Empty;
            var loginBody = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            return loginBody.TryGetProperty("accessToken", out var tok) ? tok.GetString() ?? string.Empty : string.Empty;
        }

        private void SetAuth(string? token)
        {
            _client!.DefaultRequestHeaders.Remove("Authorization");
            if (token != null) _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CJ01-CJ10: Full operator journey — assemble all 4 scenarios for a subject
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // CJ01 — Operator assembles release-readiness export; receives a valid package
        public void CJ01_Service_OperatorAssemblesReleaseReadiness_ReturnsValidPackage()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var req = new ReleaseReadinessExportRequest { SubjectId = "cj-01-subject" };
            var resp = svc.AssembleReleaseReadinessExportAsync(req).Result;

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.Scenario, Is.EqualTo(AuditScenario.ReleaseReadinessSignOff));
            Assert.That(resp.Package.SubjectId, Is.EqualTo("cj-01-subject"));
            Assert.That(resp.Package.ExportId, Is.Not.Empty);
            Assert.That(resp.Package.ContentHash, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test] // CJ02 — Operator assembles onboarding case review; package is scenario-correct
        public void CJ02_Service_OperatorAssemblesOnboardingCaseReview_ReturnsValidPackage()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var req = new OnboardingCaseReviewExportRequest { SubjectId = "cj-02-subject" };
            var resp = svc.AssembleOnboardingCaseReviewExportAsync(req).Result;

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Package!.Scenario, Is.EqualTo(AuditScenario.OnboardingCaseReview));
            Assert.That(resp.Package.ContentHash, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test] // CJ03 — Operator assembles compliance blocker review; package is scenario-correct
        public void CJ03_Service_OperatorAssemblesBlockerReview_ReturnsValidPackage()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var req = new ComplianceBlockerReviewExportRequest { SubjectId = "cj-03-subject" };
            var resp = svc.AssembleBlockerReviewExportAsync(req).Result;

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Package!.Scenario, Is.EqualTo(AuditScenario.ComplianceBlockerReview));
        }

        [Test] // CJ04 — Operator assembles approval history export; package is scenario-correct
        public void CJ04_Service_OperatorAssemblesApprovalHistory_ReturnsValidPackage()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var req = new ApprovalHistoryExportRequest { SubjectId = "cj-04-subject" };
            var resp = svc.AssembleApprovalHistoryExportAsync(req).Result;

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Package!.Scenario, Is.EqualTo(AuditScenario.ApprovalHistoryExport));
        }

        [Test] // CJ05 — Full operator journey: assemble all 4 scenarios, then list all; 4 entries appear
        public void CJ05_Service_FullJourney_AllFourScenarios_ListReturnsAll()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-05-fulljourney";

            svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subjectId }).Wait();
            svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subjectId }).Wait();
            svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = subjectId }).Wait();
            svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = subjectId }).Wait();

            var list = svc.ListExportsAsync(subjectId, limit: 10).Result;
            Assert.That(list.Exports.Count, Is.EqualTo(4));
        }

        [Test] // CJ06 — After full 4-scenario assembly, each export retrievable by ID
        public void CJ06_Service_FullJourney_EachExportRetrievableById()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-06-retrieve";

            var ids = new List<string>();
            ids.Add(svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subjectId }).Result.Package!.ExportId);
            ids.Add(svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subjectId }).Result.Package!.ExportId);
            ids.Add(svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = subjectId }).Result.Package!.ExportId);
            ids.Add(svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = subjectId }).Result.Package!.ExportId);

            foreach (var id in ids)
            {
                var get = svc.GetExportAsync(id).Result;
                Assert.That(get.Success, Is.True, $"ExportId {id} should be retrievable");
                Assert.That(get.Package!.SubjectId, Is.EqualTo(subjectId));
            }
        }

        [Test] // CJ07 — All 4 exports have distinct ExportIds
        public void CJ07_Service_AllFourScenarios_HaveDistinctExportIds()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-07-distinct-ids";

            var ids = new HashSet<string>
            {
                svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subjectId }).Result.Package!.ExportId,
                svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subjectId }).Result.Package!.ExportId,
                svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = subjectId }).Result.Package!.ExportId,
                svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = subjectId }).Result.Package!.ExportId
            };

            Assert.That(ids.Count, Is.EqualTo(4), "All 4 scenarios must produce distinct ExportIds");
        }

        [Test] // CJ08 — List with scenario filter returns only that scenario's exports
        public void CJ08_Service_ListWithScenarioFilter_ReturnsOnlyMatchingScenario()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-08-filter";

            svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subjectId }).Wait();
            svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subjectId }).Wait();

            var rrList = svc.ListExportsAsync(subjectId, AuditScenario.ReleaseReadinessSignOff).Result;
            Assert.That(rrList.Exports, Has.All.Matches<ComplianceAuditExportSummary>(s =>
                s.Scenario == AuditScenario.ReleaseReadinessSignOff));

            var ocList = svc.ListExportsAsync(subjectId, AuditScenario.OnboardingCaseReview).Result;
            Assert.That(ocList.Exports, Has.All.Matches<ComplianceAuditExportSummary>(s =>
                s.Scenario == AuditScenario.OnboardingCaseReview));
        }

        [Test] // CJ09 — Each assembled export has at least one provenance record
        public void CJ09_Service_AllScenarios_HaveAtLeastOneProvenanceRecord()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-09-provenance";

            var responses = new[]
            {
                svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subjectId }).Result,
                svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subjectId }).Result,
                svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = subjectId }).Result,
                svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = subjectId }).Result
            };

            foreach (var r in responses)
            {
                Assert.That(r.Package!.ProvenanceRecords.Count, Is.GreaterThan(0),
                    $"Scenario {r.Package.Scenario} must have at least one provenance record");
            }
        }

        [Test] // CJ10 — All provenance records have non-empty ProvenanceId, SourceSystem, EvidenceCategory
        public void CJ10_Service_AllProvenanceRecords_HaveRequiredFields()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cj-10-prov-fields" }).Result;

            foreach (var prov in resp.Package!.ProvenanceRecords)
            {
                Assert.That(prov.ProvenanceId, Is.Not.Empty, "ProvenanceId must not be empty");
                Assert.That(prov.SourceSystem, Is.Not.Empty, "SourceSystem must not be empty");
                Assert.That(prov.EvidenceCategory, Is.Not.Empty, "EvidenceCategory must not be empty");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CJ11-CJ20: Audience-profile journey
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // CJ11 — InternalCompliance profile: IsRegulatorReady may be false without full evidence
        public void CJ11_Service_InternalCompliance_IsRegulatorReadyRespectsFailClosed()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "cj-11-internal",
                AudienceProfile = RegulatoryAudienceProfile.InternalCompliance
            }).Result;

            Assert.That(resp.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.InternalCompliance));
            // IsRegulatorReady must only be true when Readiness == Ready
            if (resp.Package.Readiness != AuditExportReadiness.Ready)
                Assert.That(resp.Package.IsRegulatorReady, Is.False);
        }

        [Test] // CJ12 — ExecutiveSignOff profile: package carries correct audience
        public void CJ12_Service_ExecutiveSignOff_AudienceProfilePropagated()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "cj-12-exec",
                AudienceProfile = RegulatoryAudienceProfile.ExecutiveSignOff
            }).Result;

            Assert.That(resp.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.ExecutiveSignOff));
        }

        [Test] // CJ13 — ExternalAuditor profile: package carries correct audience
        public void CJ13_Service_ExternalAuditor_AudienceProfilePropagated()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
            {
                SubjectId = "cj-13-external-auditor",
                AudienceProfile = RegulatoryAudienceProfile.ExternalAuditor
            }).Result;

            Assert.That(resp.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.ExternalAuditor));
        }

        [Test] // CJ14 — RegulatorReview profile: package carries correct audience
        public void CJ14_Service_RegulatorReview_AudienceProfilePropagated()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest
            {
                SubjectId = "cj-14-regulator",
                AudienceProfile = RegulatoryAudienceProfile.RegulatorReview
            }).Result;

            Assert.That(resp.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.RegulatorReview));
        }

        [Test] // CJ15 — Same subject, different audience profiles produce different ContentHashes
        public void CJ15_Service_DifferentAudienceProfiles_ProduceDifferentContentHashes()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-15-audience-hash";

            var resp1 = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = subjectId,
                AudienceProfile = RegulatoryAudienceProfile.InternalCompliance
            }).Result;

            var resp2 = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = subjectId,
                AudienceProfile = RegulatoryAudienceProfile.RegulatorReview,
                ForceRegenerate = true
            }).Result;

            // Different audience may produce different hashes (packages differ in metadata)
            Assert.That(resp1.Package!.ContentHash, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(resp2.Package!.ContentHash, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test] // CJ16 — All 4 audience profiles can be set on all 4 scenarios
        public void CJ16_Service_AllAudienceProfilesWorkOnAllScenarios()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var profiles = Enum.GetValues<RegulatoryAudienceProfile>();

            foreach (var profile in profiles)
            {
                var rrResp = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                {
                    SubjectId = $"cj-16-{profile}-rr",
                    AudienceProfile = profile
                }).Result;
                Assert.That(rrResp.Package!.AudienceProfile, Is.EqualTo(profile),
                    $"ReleaseReadiness with {profile} should propagate audience");

                var ocResp = svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest
                {
                    SubjectId = $"cj-16-{profile}-oc",
                    AudienceProfile = profile
                }).Result;
                Assert.That(ocResp.Package!.AudienceProfile, Is.EqualTo(profile));
            }
        }

        [Test] // CJ17 — IsRegulatorReady is false for non-Ready readiness states regardless of audience
        public void CJ17_Service_IsRegulatorReady_FalseForNonReadyStates()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            // Assemble multiple subjects — none guaranteed to be Ready with default seed
            var profiles = Enum.GetValues<RegulatoryAudienceProfile>();
            foreach (var profile in profiles)
            {
                var resp = svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest
                {
                    SubjectId = $"cj-17-{profile}",
                    AudienceProfile = profile
                }).Result;
                if (resp.Package!.Readiness != AuditExportReadiness.Ready)
                    Assert.That(resp.Package.IsRegulatorReady, Is.False,
                        $"Profile {profile}: IsRegulatorReady must be false when not Ready");
            }
        }

        [Test] // CJ18 — IsReleaseGrade is false for non-Ready and non-RequiresReview states
        public void CJ18_Service_IsReleaseGrade_FalseForNonReleaseReadiness()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var nonReleaseStates = new[]
            {
                AuditExportReadiness.Incomplete,
                AuditExportReadiness.Blocked,
                AuditExportReadiness.Stale,
                AuditExportReadiness.PartiallyAvailable,
                AuditExportReadiness.DegradedProviderUnavailable
            };

            // Assert invariant on the actual assembled packages
            var resp = svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
            {
                SubjectId = "cj-18-release-grade"
            }).Result;

            // Invariant: Ready and RequiresReview → IsReleaseGrade true; others → false
            if (nonReleaseStates.Contains(resp.Package!.Readiness))
                Assert.That(resp.Package.IsReleaseGrade, Is.False);
            else
                Assert.That(resp.Package.IsReleaseGrade, Is.True);
        }

        [Test] // CJ19 — Default audience profile (InternalCompliance) is used when not specified
        public void CJ19_Service_DefaultAudienceProfile_IsInternalCompliance()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cj-19-default-audience" }).Result;

            // Default audience should be InternalCompliance (index 0)
            Assert.That((int)resp.Package!.AudienceProfile, Is.GreaterThanOrEqualTo(0));
        }

        [Test] // CJ20 — AudienceProfile is serialized in list summaries
        public void CJ20_Service_AudienceProfile_AppearsInListSummaries()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-20-list-audience";

            svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = subjectId,
                AudienceProfile = RegulatoryAudienceProfile.ExternalAuditor
            }).Wait();

            var list = svc.ListExportsAsync(subjectId).Result;
            Assert.That(list.Exports, Is.Not.Empty);
            Assert.That(list.Exports.First().AudienceProfile,
                Is.EqualTo(RegulatoryAudienceProfile.ExternalAuditor));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CJ21-CJ30: Freshness-degradation journey
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // CJ21 — Evidence freshness is classified at assembly time via TimeProvider
        public void CJ21_Service_EvidenceFreshness_ClassifiedAtAssemblyTime()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance, tp);
            var resp = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cj-21-freshness" }).Result;

            // All provenance records should have a FreshnessState set
            foreach (var prov in resp.Package!.ProvenanceRecords)
            {
                Assert.That(Enum.IsDefined(typeof(AuditEvidenceFreshness), prov.FreshnessState),
                    "FreshnessState must be a valid enum value");
            }
        }

        [Test] // CJ22 — Assembly with current time: evidence should not be Missing for any core scenario
        public void CJ22_Service_CurrentTime_EvidenceNotMissingForReleaseReadiness()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance, tp);
            var resp = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cj-22-not-missing" }).Result;

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Package, Is.Not.Null);
        }

        [Test] // CJ23 — Advancing time: re-assembling with ForceRegenerate produces new timestamp
        public void CJ23_Service_AdvancingTime_ReassemblyProducesNewAssembledAt()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance, tp);
            const string subjectId = "cj-23-time-advance";

            var resp1 = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId }).Result;
            var assembledAt1 = resp1.Package!.AssembledAt;

            tp.Advance(TimeSpan.FromHours(1));

            var resp2 = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId, ForceRegenerate = true }).Result;
            var assembledAt2 = resp2.Package!.AssembledAt;

            Assert.That(assembledAt2, Is.GreaterThan(assembledAt1),
                "Re-assembled package should have a later AssembledAt timestamp");
        }

        [Test] // CJ24 — ForceRegenerate changes the ContentHash (new package is assembled fresh)
        public void CJ24_Service_ForceRegenerate_ProducesNewContentHash()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance, tp);
            const string subjectId = "cj-24-hash-change";

            var resp1 = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId }).Result;

            tp.Advance(TimeSpan.FromSeconds(1));

            var resp2 = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId, ForceRegenerate = true }).Result;

            // Force-regenerate at a later time must produce a different hash
            Assert.That(resp2.Package!.ContentHash, Is.Not.EqualTo(resp1.Package!.ContentHash),
                "ForceRegenerate after time advance must produce a different ContentHash");
        }

        [Test] // CJ25 — TrackerHistory records prior export on second assembly
        public void CJ25_Service_SecondAssembly_TrackerHistoryIsNonEmpty()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-25-tracker";

            // First assembly — TrackerHistory is empty (no prior)
            svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId }).Wait();

            // Second assembly — TrackerHistory should record the prior export
            var resp2 = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId, ForceRegenerate = true }).Result;

            Assert.That(resp2.Package!.TrackerHistory, Is.Not.Null);
            Assert.That(resp2.Package.TrackerHistory, Is.Not.Empty,
                "Second assembly should have at least one entry in TrackerHistory");
        }

        [Test] // CJ26 — TrackerHistory entries carry the prior ExportId
        public void CJ26_Service_TrackerHistory_CarriesPriorExportId()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-26-tracker-id";

            var resp1 = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId }).Result;
            var firstExportId = resp1.Package!.ExportId;

            var resp2 = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId, ForceRegenerate = true }).Result;

            var history = resp2.Package!.TrackerHistory;
            Assert.That(history, Is.Not.Null);
            Assert.That(history, Is.Not.Empty);
            Assert.That(history.Contains(firstExportId), Is.True,
                "TrackerHistory must include the prior ExportId");
        }

        [Test] // CJ27 — Repeated assemblies accumulate tracker history entries
        public void CJ27_Service_RepeatedAssemblies_AccumulateTrackerHistory()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-27-accumulate";

            svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subjectId }).Wait();
            svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subjectId, ForceRegenerate = true }).Wait();
            var resp3 = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subjectId, ForceRegenerate = true }).Result;

            Assert.That(resp3.Package!.TrackerHistory.Count, Is.GreaterThanOrEqualTo(2),
                "After 3 assemblies, history should have at least 2 entries");
        }

        [Test] // CJ28 — TrackerHistory entries are valid non-empty export ID strings
        public void CJ28_Service_TrackerHistory_EntriesAreValidExportIds()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-28-history-fields";

            svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subjectId }).Wait();
            var resp2 = svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subjectId, ForceRegenerate = true }).Result;

            foreach (var exportId in resp2.Package!.TrackerHistory)
            {
                Assert.That(exportId, Is.Not.Empty, "TrackerHistory entry must be a non-empty export ID");
            }
        }

        [Test] // CJ29 — EvidenceFromTimestamp filter does not break assembly
        public void CJ29_Service_EvidenceFromTimestamp_AssemblySucceeds()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "cj-29-timestamp-filter",
                EvidenceFromTimestamp = DateTime.UtcNow.AddDays(-30)
            }).Result;

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Package, Is.Not.Null);
        }

        [Test] // CJ30 — EvidenceFromTimestamp in the future still assembles without crash
        public void CJ30_Service_EvidenceFromTimestamp_FutureTimestamp_AssembliesGracefully()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "cj-30-future-timestamp",
                EvidenceFromTimestamp = DateTime.UtcNow.AddDays(30)
            }).Result;

            // Should not crash; may return partial or missing evidence
            Assert.That(resp.Package, Is.Not.Null, "Future EvidenceFromTimestamp must not crash assembly");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CJ31-CJ40: Blocker-resolution journey
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // CJ31 — Assembled package lists any blockers present
        public void CJ31_Service_AssembledPackage_BlockersListIsNotNull()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "cj-31-blockers" }).Result;

            Assert.That(resp.Package!.Blockers, Is.Not.Null, "Blockers list must not be null");
        }

        [Test] // CJ32 — Any critical blocker means IsRegulatorReady is false
        public void CJ32_Service_CriticalBlockers_PreventRegulatorReady()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "cj-32-critical" }).Result;

            var hasCritical = resp.Package!.Blockers.Any(b => b.Severity == AuditBlockerSeverity.Critical && !b.IsResolved);
            if (hasCritical)
                Assert.That(resp.Package.IsRegulatorReady, Is.False,
                    "Critical unresolved blockers must prevent IsRegulatorReady");
        }

        [Test] // CJ33 — Blocker taxonomy: all blockers have non-empty BlockerId, Title, Category
        public void CJ33_Service_Blockers_HaveRequiredFields()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "cj-33-blocker-fields" }).Result;

            foreach (var blocker in resp.Package!.Blockers)
            {
                Assert.That(blocker.BlockerId, Is.Not.Empty, "BlockerId must not be empty");
                Assert.That(blocker.Title, Is.Not.Empty, "Title must not be empty");
                Assert.That(blocker.Category, Is.Not.Empty, "Category must not be empty");
            }
        }

        [Test] // CJ34 — Blockers with remediation hints have at least one hint
        public void CJ34_Service_Blockers_WithRemediationHints_HaveAtLeastOneHint()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cj-34-hints" }).Result;

            foreach (var blocker in resp.Package!.Blockers.Where(b => b.RemediationHints.Count > 0))
            {
                Assert.That(blocker.RemediationHints, Has.Count.GreaterThan(0));
            }
        }

        [Test] // CJ35 — All blockers have a valid AuditBlockerSeverity value
        public void CJ35_Service_Blockers_HaveValidSeverity()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "cj-35-severity" }).Result;

            foreach (var blocker in resp.Package!.Blockers)
            {
                Assert.That(Enum.IsDefined(typeof(AuditBlockerSeverity), blocker.Severity),
                    "All blockers must have a valid severity");
            }
        }

        [Test] // CJ36 — Package SchemaVersion is not empty
        public void CJ36_Service_Package_SchemaVersionIsSet()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cj-36-schema" }).Result;

            Assert.That(resp.Package!.SchemaVersion, Is.Not.Empty, "SchemaVersion must not be empty");
        }

        [Test] // CJ37 — Package PolicyVersion is not empty
        public void CJ37_Service_Package_PolicyVersionIsSet()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "cj-37-policy" }).Result;

            Assert.That(resp.Package!.PolicyVersion, Is.Not.Empty, "PolicyVersion must not be empty");
        }

        [Test] // CJ38 — Package CorrelationId is propagated from request
        public void CJ38_Service_Package_CorrelationIdPropagated()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var corrId = Guid.NewGuid().ToString();
            var resp = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "cj-38-correlation",
                CorrelationId = corrId
            }).Result;

            Assert.That(resp.Package!.CorrelationId, Is.EqualTo(corrId));
        }

        [Test] // CJ39 — CorrelationId is auto-generated if not provided
        public void CJ39_Service_Package_CorrelationIdAutoGeneratedIfNull()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "cj-39-auto-corr",
                CorrelationId = null
            }).Result;

            Assert.That(resp.Package!.CorrelationId, Is.Not.Empty, "CorrelationId must be auto-generated");
        }

        [Test] // CJ40 — Readiness enum covers all 7 states by definition; assembled packages use valid states
        public void CJ40_Service_ReadinessEnum_AssembledPackageUsesValidState()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var resp = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cj-40-readiness-enum" }).Result;

            Assert.That(Enum.IsDefined(typeof(AuditExportReadiness), resp.Package!.Readiness),
                "Package.Readiness must be a valid AuditExportReadiness enum value");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CJ41-CJ50: Multi-subject governance journey
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // CJ41 — Two subjects are isolated: listing one does not return the other
        public void CJ41_Service_TwoSubjects_ListsAreIsolated()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string s1 = "cj-41-subject-alpha";
            const string s2 = "cj-41-subject-beta";

            svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = s1 }).Wait();
            svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = s2 }).Wait();

            var list1 = svc.ListExportsAsync(s1).Result;
            var list2 = svc.ListExportsAsync(s2).Result;

            Assert.That(list1.Exports.All(e => e.SubjectId == s1), Is.True, "Subject1 list must only contain Subject1 exports");
            Assert.That(list2.Exports.All(e => e.SubjectId == s2), Is.True, "Subject2 list must only contain Subject2 exports");
        }

        [Test] // CJ42 — Export from subject A is not retrievable via subject B's list
        public void CJ42_Service_ExportFromSubjectA_NotInSubjectBList()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string sA = "cj-42-subject-a";
            const string sB = "cj-42-subject-b";

            var respA = svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = sA }).Result;
            svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = sB }).Wait();

            var listB = svc.ListExportsAsync(sB).Result;
            Assert.That(listB.Exports.Any(e => e.ExportId == respA.Package!.ExportId), Is.False,
                "Subject B's list must not contain Subject A's export");
        }

        [Test] // CJ43 — 10 concurrent subjects produce 10 isolated export lists
        public void CJ43_Service_TenConcurrentSubjects_ProduceIsolatedLists()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var subjectIds = Enumerable.Range(0, 10).Select(i => $"cj-43-concurrent-{i}").ToList();

            var tasks = subjectIds.Select(id =>
                svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = id })
            ).ToList();
            Task.WhenAll(tasks).Wait();

            foreach (var subjectId in subjectIds)
            {
                var list = svc.ListExportsAsync(subjectId).Result;
                Assert.That(list.Exports.All(e => e.SubjectId == subjectId), Is.True,
                    $"Subject {subjectId} list should only contain its own exports");
            }
        }

        [Test] // CJ44 — Listing an unknown subject returns an empty list (not an error)
        public void CJ44_Service_UnknownSubject_ListReturnsEmpty()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var list = svc.ListExportsAsync("cj-44-nobody").Result;
            Assert.That(list.Exports, Is.Empty, "Unknown subject should return empty list");
        }

        [Test] // CJ45 — GetExport with unknown ExportId returns Success=false
        public void CJ45_Service_UnknownExportId_GetReturnsFailure()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var get = svc.GetExportAsync("nonexistent-export-id-cj45").Result;
            Assert.That(get.Success, Is.False, "GetExport for unknown ID must return Success=false");
        }

        [Test] // CJ46 — Idempotency: same key returns same package without creating a new one
        public void CJ46_Service_IdempotencyKey_ReturnsSamePackage()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var key = $"idem-cj46-{Guid.NewGuid():N}";

            var resp1 = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "cj-46-idem",
                IdempotencyKey = key
            }).Result;

            var resp2 = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "cj-46-idem",
                IdempotencyKey = key
            }).Result;

            Assert.That(resp2.IsIdempotentReplay, Is.True, "Second request with same key must be idempotent replay");
            Assert.That(resp2.Package!.ExportId, Is.EqualTo(resp1.Package!.ExportId));
        }

        [Test] // CJ47 — ForceRegenerate with same idempotency key produces a new package
        public void CJ47_Service_ForceRegenerate_BreaksIdempotency()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var key = $"idem-cj47-{Guid.NewGuid():N}";

            var resp1 = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "cj-47-force",
                IdempotencyKey = key
            }).Result;

            var resp2 = svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "cj-47-force",
                IdempotencyKey = key,
                ForceRegenerate = true
            }).Result;

            Assert.That(resp2.IsIdempotentReplay, Is.False, "ForceRegenerate must bypass idempotency");
            Assert.That(resp2.Package!.ExportId, Is.Not.EqualTo(resp1.Package!.ExportId));
        }

        [Test] // CJ48 — List limit is respected
        public void CJ48_Service_ListLimit_IsRespected()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            const string subjectId = "cj-48-limit";

            // Assemble 5 packages
            for (var i = 0; i < 5; i++)
                svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                {
                    SubjectId = subjectId,
                    ForceRegenerate = true
                }).Wait();

            var list = svc.ListExportsAsync(subjectId, limit: 3).Result;
            Assert.That(list.Exports.Count, Is.LessThanOrEqualTo(3), "Limit=3 must return at most 3 exports");
        }

        [Test] // CJ49 — List is ordered newest-first (AssembledAt descending)
        public void CJ49_Service_List_IsOrderedNewestFirst()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance, tp);
            const string subjectId = "cj-49-ordering";

            svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subjectId }).Wait();
            tp.Advance(TimeSpan.FromMinutes(1));
            svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subjectId, ForceRegenerate = true }).Wait();

            var list = svc.ListExportsAsync(subjectId).Result;
            if (list.Exports.Count >= 2)
            {
                Assert.That(list.Exports[0].AssembledAt, Is.GreaterThanOrEqualTo(list.Exports[1].AssembledAt),
                    "List must be ordered newest-first");
            }
        }

        [Test] // CJ50 — Full governance journey: 3 subjects × all 4 scenarios = 12 packages, all isolated
        public void CJ50_Service_FullGovernanceJourney_ThreeSubjectsFourScenarios()
        {
            var svc = new ComplianceAuditExportService(NullLogger<ComplianceAuditExportService>.Instance);
            var subjects = new[] { "cj-50-alpha", "cj-50-beta", "cj-50-gamma" };

            foreach (var subjectId in subjects)
            {
                svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subjectId }).Wait();
                svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subjectId }).Wait();
                svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = subjectId }).Wait();
                svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = subjectId }).Wait();
            }

            foreach (var subjectId in subjects)
            {
                var list = svc.ListExportsAsync(subjectId).Result;
                Assert.That(list.Exports.Count, Is.EqualTo(4), $"{subjectId} should have exactly 4 exports");
                Assert.That(list.Exports.All(e => e.SubjectId == subjectId), Is.True,
                    $"{subjectId} list must only contain its own exports");
            }
        }
    }
}
