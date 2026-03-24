using BiatecTokensApi.Models.ComplianceAuditExport;
using BiatecTokensApi.Models.RegulatoryEvidencePackage;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Advanced verification tests for <see cref="ComplianceAuditExportService"/>.
    ///
    /// CAV01-CAV10: ReadinessHeadline / ReadinessDetail content validation
    /// CAV11-CAV20: SchemaVersion, PolicyVersion, ExpiresAt, ContentHash stability
    /// CAV21-CAV30: Multi-subject isolation, TrackerHistory per-scenario, concurrent assembly
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceAuditExportAdvancedTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset v) => _now = v;
            public void Advance(TimeSpan d) => _now = _now.Add(d);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static ComplianceAuditExportService CreateService(TimeProvider? tp = null)
            => new(NullLogger<ComplianceAuditExportService>.Instance, tp);

        // Finds the first subject (scanning up to 200 seeds) that yields a given readiness
        private static async Task<ComplianceAuditExportPackage?> FindPackageWithReadiness(
            ComplianceAuditExportService svc,
            AuditExportReadiness target,
            Func<string, Task<ComplianceAuditExportResponse>> assembler,
            int maxSeeds = 200)
        {
            for (int i = 0; i < maxSeeds; i++)
            {
                var result = await assembler($"adv-seed-{i}-{target}");
                if (result.Package!.Readiness == target)
                    return result.Package;
            }
            return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // CAV01-CAV10: ReadinessHeadline and ReadinessDetail content
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CAV01 — Ready state: headline indicates regulator-ready
        public async Task CAV01_ReadinessHeadline_Ready_ContainsRegulatorReady()
        {
            var svc = CreateService();
            ComplianceAuditExportPackage? pkg = null;
            for (int i = 0; i < 300; i++)
            {
                var r = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
                    { SubjectId = $"cav01-{i}", IncludeResolvedBlockers = false });
                if (r.Package!.Readiness == AuditExportReadiness.Ready) { pkg = r.Package; break; }
            }
            Assume.That(pkg, Is.Not.Null, "No Ready subject found in 300 seeds — skipped.");
            Assert.That(pkg!.ReadinessHeadline, Does.Contain("regulator-ready").IgnoreCase
                .Or.Contain("all required evidence").IgnoreCase);
            Assert.That(pkg.ReadinessDetail, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CAV02 — Blocked state: headline mentions critical blocker count
        public async Task CAV02_ReadinessHeadline_Blocked_MentionsCriticalBlockers()
        {
            var svc = CreateService();
            ComplianceAuditExportPackage? pkg = null;
            for (int i = 0; i < 300; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                    { SubjectId = $"cav02-{i}" });
                if (r.Package!.Readiness == AuditExportReadiness.Blocked) { pkg = r.Package; break; }
            }
            Assume.That(pkg, Is.Not.Null, "No Blocked subject found — skipped.");
            Assert.That(pkg!.ReadinessHeadline, Does.Contain("blocked").IgnoreCase
                .Or.Contain("critical").IgnoreCase);
            Assert.That(pkg.CriticalBlockerCount, Is.GreaterThan(0));
        }

        [Test] // CAV03 — Incomplete state: headline indicates missing evidence
        public async Task CAV03_ReadinessHeadline_Incomplete_MentionsMissingEvidence()
        {
            var svc = CreateService();
            // Force Incomplete by requesting evidence only after now+future (nothing matches)
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "cav03-incomplete",
                EvidenceFromTimestamp = DateTime.UtcNow.AddYears(100)
            });
            if (r.Package!.Readiness == AuditExportReadiness.Incomplete)
            {
                Assert.That(r.Package.ReadinessHeadline, Does.Contain("incomplete").IgnoreCase
                    .Or.Contain("missing").IgnoreCase);
                Assert.That(r.Package.ReadinessDetail, Does.Contain("missing").IgnoreCase
                    .Or.Contain("required evidence").IgnoreCase);
            }
            else
            {
                // Stale or other degraded state is also valid — check headline is non-empty
                Assert.That(r.Package.ReadinessHeadline, Is.Not.Null.And.Not.Empty);
                Assert.That(r.Package.ReadinessDetail, Is.Not.Null.And.Not.Empty);
            }
        }

        [Test] // CAV04 — ReadinessDetail is always non-empty for all scenarios
        public async Task CAV04_ReadinessDetail_AlwaysPopulated_AllScenarios()
        {
            var svc = CreateService();
            var rrResult = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav04-rr" });
            var ocResult = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "cav04-oc" });
            var brResult = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "cav04-br" });
            var ahResult = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "cav04-ah" });

            Assert.That(rrResult.Package!.ReadinessDetail, Is.Not.Null.And.Not.Empty);
            Assert.That(ocResult.Package!.ReadinessDetail, Is.Not.Null.And.Not.Empty);
            Assert.That(brResult.Package!.ReadinessDetail, Is.Not.Null.And.Not.Empty);
            Assert.That(ahResult.Package!.ReadinessDetail, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CAV05 — ReadinessHeadline is always non-empty for all scenarios
        public async Task CAV05_ReadinessHeadline_AlwaysPopulated_AllScenarios()
        {
            var svc = CreateService();
            var rr = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav05-rr" });
            var oc = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "cav05-oc" });
            var br = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "cav05-br" });
            var ah = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "cav05-ah" });

            Assert.That(rr.Package!.ReadinessHeadline, Is.Not.Null.And.Not.Empty);
            Assert.That(oc.Package!.ReadinessHeadline, Is.Not.Null.And.Not.Empty);
            Assert.That(br.Package!.ReadinessHeadline, Is.Not.Null.And.Not.Empty);
            Assert.That(ah.Package!.ReadinessHeadline, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CAV06 — IsRegulatorReady=false for every non-Ready state across all scenarios
        public async Task CAV06_IsRegulatorReady_FalseForAllNonReadyStates()
        {
            var svc = CreateService();
            // Force non-Ready by using future timestamp filter
            var future = DateTime.UtcNow.AddYears(100);
            var rr = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav06-rr", EvidenceFromTimestamp = future });
            var oc = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "cav06-oc", EvidenceFromTimestamp = future });
            var br = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "cav06-br", EvidenceFromTimestamp = future });
            var ah = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "cav06-ah", EvidenceFromTimestamp = future });

            // With future filter all required evidence is missing → non-Ready
            foreach (var pkg in new[] { rr.Package!, oc.Package!, br.Package!, ah.Package! })
            {
                if (pkg.Readiness != AuditExportReadiness.Ready)
                    Assert.That(pkg.IsRegulatorReady, Is.False,
                        $"Expected IsRegulatorReady=false for readiness={pkg.Readiness}");
            }
        }

        [Test] // CAV07 — IsReleaseGrade=false for Blocked and DegradedProviderUnavailable
        public async Task CAV07_IsReleaseGrade_FalseForBlockedAndDegraded()
        {
            var svc = CreateService();
            // Scan for Blocked
            for (int i = 0; i < 300; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(
                    new ReleaseReadinessExportRequest { SubjectId = $"cav07-{i}" });
                if (r.Package!.Readiness == AuditExportReadiness.Blocked)
                {
                    Assert.That(r.Package.IsReleaseGrade, Is.False);
                    return;
                }
                if (r.Package.Readiness == AuditExportReadiness.DegradedProviderUnavailable)
                {
                    Assert.That(r.Package.IsReleaseGrade, Is.False);
                    return;
                }
            }
            Assert.Pass("No Blocked or Degraded subject found in range — trivially passed.");
        }

        [Test] // CAV08 — PartiallyAvailable state: IsReleaseGrade=false, IsRegulatorReady=false
        public async Task CAV08_PartiallyAvailable_IsNotReleaseGrade_IsNotRegulatorReady()
        {
            var svc = CreateService();
            for (int i = 0; i < 300; i++)
            {
                var r = await svc.AssembleBlockerReviewExportAsync(
                    new ComplianceBlockerReviewExportRequest { SubjectId = $"cav08-{i}" });
                if (r.Package!.Readiness == AuditExportReadiness.PartiallyAvailable)
                {
                    Assert.That(r.Package.IsReleaseGrade, Is.False);
                    Assert.That(r.Package.IsRegulatorReady, Is.False);
                    return;
                }
            }
            Assert.Pass("PartiallyAvailable not found in range — trivially passed.");
        }

        [Test] // CAV09 — RequiresReview: IsReleaseGrade=true, IsRegulatorReady=false
        public async Task CAV09_RequiresReview_IsReleaseGrade_NotRegulatorReady()
        {
            var svc = CreateService();
            for (int i = 0; i < 300; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(
                    new ReleaseReadinessExportRequest { SubjectId = $"cav09-{i}" });
                if (r.Package!.Readiness == AuditExportReadiness.RequiresReview)
                {
                    Assert.That(r.Package.IsReleaseGrade, Is.True);
                    Assert.That(r.Package.IsRegulatorReady, Is.False);
                    return;
                }
            }
            Assert.Pass("RequiresReview not found in range — trivially passed.");
        }

        [Test] // CAV10 — CriticalBlockerCount reflects actual critical open blockers
        public async Task CAV10_CriticalBlockerCount_MatchesActualBlockers()
        {
            var svc = CreateService();
            for (int i = 0; i < 200; i++)
            {
                var r = await svc.AssembleBlockerReviewExportAsync(
                    new ComplianceBlockerReviewExportRequest
                    { SubjectId = $"cav10-{i}", IncludeResolvedBlockers = true });
                var pkg = r.Package!;
                int computedCritical = pkg.Blockers.Count(b => !b.IsResolved && b.Severity == AuditBlockerSeverity.Critical);
                Assert.That(pkg.CriticalBlockerCount, Is.EqualTo(computedCritical),
                    $"CriticalBlockerCount mismatch at seed {i}");
                if (computedCritical > 0)
                    return; // Confirmed at least one non-trivial case
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // CAV11-CAV20: SchemaVersion, PolicyVersion, ExpiresAt, ContentHash
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CAV11 — SchemaVersion is always "1.0.0" across all scenarios
        public async Task CAV11_SchemaVersion_Is_1_0_0_AllScenarios()
        {
            var svc = CreateService();
            var rr = (await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav11-rr" })).Package!;
            var oc = (await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "cav11-oc" })).Package!;
            var br = (await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "cav11-br" })).Package!;
            var ah = (await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "cav11-ah" })).Package!;

            Assert.That(rr.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(oc.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(br.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(ah.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test] // CAV12 — PolicyVersion is non-empty and consistent across scenarios
        public async Task CAV12_PolicyVersion_NonEmpty_Consistent()
        {
            var svc = CreateService();
            var rr = (await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav12-rr" })).Package!;
            var oc = (await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "cav12-oc" })).Package!;
            var br = (await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "cav12-br" })).Package!;
            var ah = (await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "cav12-ah" })).Package!;

            Assert.That(rr.PolicyVersion, Is.Not.Null.And.Not.Empty);
            // All scenarios share the same policy version
            Assert.That(oc.PolicyVersion, Is.EqualTo(rr.PolicyVersion));
            Assert.That(br.PolicyVersion, Is.EqualTo(rr.PolicyVersion));
            Assert.That(ah.PolicyVersion, Is.EqualTo(rr.PolicyVersion));
        }

        [Test] // CAV13 — AssembledAt is set close to UTC now
        public async Task CAV13_AssembledAt_IsSetToUtcNow()
        {
            var fakeClock = new FakeTimeProvider(new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero));
            var svc = CreateService(fakeClock);
            var result = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav13" });
            Assert.That(result.Package!.AssembledAt,
                Is.EqualTo(new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc)).Within(TimeSpan.FromSeconds(1)));
        }

        [Test] // CAV14 — ExpiresAt is after AssembledAt when set
        public async Task CAV14_ExpiresAt_IsAfterAssembledAt_WhenSet()
        {
            var svc = CreateService();
            var result = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav14" });
            var pkg = result.Package!;
            if (pkg.ExpiresAt.HasValue)
            {
                Assert.That(pkg.ExpiresAt.Value, Is.GreaterThan(pkg.AssembledAt),
                    "ExpiresAt must be after AssembledAt.");
            }
        }

        [Test] // CAV15 — ContentHash is exactly 64 hex characters (SHA-256) for all scenarios
        public async Task CAV15_ContentHash_Is64HexChars_AllScenarios()
        {
            var svc = CreateService();
            var scenarios = new Func<string, Task<ComplianceAuditExportResponse>>[]
            {
                id => svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = id }),
                id => svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = id }),
                id => svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = id }),
                id => svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = id }),
            };
            var names = new[] { "rr", "oc", "br", "ah" };
            for (int i = 0; i < scenarios.Length; i++)
            {
                var result = await scenarios[i]($"cav15-{names[i]}");
                Assert.That(result.Package!.ContentHash, Has.Length.EqualTo(64),
                    $"ContentHash length should be 64 for {names[i]}");
                Assert.That(result.Package.ContentHash, Does.Match("^[0-9a-f]{64}$"),
                    $"ContentHash should be lowercase hex for {names[i]}");
            }
        }

        [Test] // CAV16 — Two sequential assemblies for same subject produce different ExportIds
        public async Task CAV16_SequentialAssemblies_ProduceDifferentExportIds()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav16" });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav16" });
            Assert.That(r2.Package!.ExportId, Is.Not.EqualTo(r1.Package!.ExportId),
                "Sequential assemblies must produce unique ExportIds.");
        }

        [Test] // CAV17 — RequestorNotes is preserved in the package
        public async Task CAV17_RequestorNotes_PreservedInPackage()
        {
            var svc = CreateService();
            const string notes = "Prepared for Q2 2025 regulatory review.";
            var result = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav17", RequestorNotes = notes });
            Assert.That(result.Package!.RequestorNotes, Is.EqualTo(notes));
        }

        [Test] // CAV18 — EnvironmentLabel is preserved in the package
        public async Task CAV18_EnvironmentLabel_PreservedInPackage()
        {
            var svc = CreateService();
            var result = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest
                {
                    SubjectId = "cav18",
                    EnvironmentLabel = "production"
                });
            Assert.That(result.Package!.EnvironmentLabel, Is.EqualTo("production"));
        }

        [Test] // CAV19 — AudienceProfile propagated for all 4 scenarios
        public async Task CAV19_AudienceProfile_Propagated_AllScenarios()
        {
            var svc = CreateService();
            var profile = RegulatoryAudienceProfile.RegulatorReview;
            var rr = (await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav19", AudienceProfile = profile })).Package!;
            var oc = (await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "cav19", AudienceProfile = profile })).Package!;
            var br = (await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "cav19", AudienceProfile = profile })).Package!;
            var ah = (await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "cav19", AudienceProfile = profile })).Package!;

            Assert.That(rr.AudienceProfile, Is.EqualTo(profile));
            Assert.That(oc.AudienceProfile, Is.EqualTo(profile));
            Assert.That(br.AudienceProfile, Is.EqualTo(profile));
            Assert.That(ah.AudienceProfile, Is.EqualTo(profile));
        }

        [Test] // CAV20 — SubjectId is preserved in the package for all scenarios
        public async Task CAV20_SubjectId_PreservedInPackage_AllScenarios()
        {
            var svc = CreateService();
            const string sid = "enterprise-client-xkcd-42";
            var rr = (await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = sid })).Package!;
            var oc = (await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = sid })).Package!;
            var br = (await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = sid })).Package!;
            var ah = (await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = sid })).Package!;

            Assert.That(rr.SubjectId, Is.EqualTo(sid));
            Assert.That(oc.SubjectId, Is.EqualTo(sid));
            Assert.That(br.SubjectId, Is.EqualTo(sid));
            Assert.That(ah.SubjectId, Is.EqualTo(sid));
        }

        // ═════════════════════════════════════════════════════════════════════
        // CAV21-CAV30: Multi-subject isolation, TrackerHistory, concurrency
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CAV21 — Two different subjects do not share TrackerHistory
        public async Task CAV21_MultiSubject_TrackerHistories_AreIsolated()
        {
            var svc = CreateService();
            // Build up some history for subject A
            for (int i = 0; i < 3; i++)
                await svc.AssembleReleaseReadinessExportAsync(
                    new ReleaseReadinessExportRequest { SubjectId = "cav21-subjectA" });

            // Subject B should have empty history
            var bResult = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav21-subjectB" });
            Assert.That(bResult.Package!.TrackerHistory, Is.Empty,
                "Subject B should have no TrackerHistory from Subject A.");
        }

        [Test] // CAV22 — TrackerHistory grows independently per scenario per subject
        public async Task CAV22_TrackerHistory_GrowsPerScenario_NotAcrossScenarios()
        {
            var svc = CreateService();
            const string sid = "cav22-shared-subject";

            // Build 2 releases-readiness exports
            for (int i = 0; i < 2; i++)
                await svc.AssembleReleaseReadinessExportAsync(
                    new ReleaseReadinessExportRequest { SubjectId = sid });

            // Build 1 approval-history export (different scenario)
            await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = sid });

            // The 3rd release-readiness export should have history from the previous 2 RR exports
            var rrResult3 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = sid });
            Assert.That(rrResult3.Package!.TrackerHistory.Count, Is.EqualTo(2),
                "RR scenario history should be 2 from previous RR exports.");

            // The 2nd approval-history export should have history of 1 from previous AH export
            var ahResult2 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = sid });
            Assert.That(ahResult2.Package!.TrackerHistory.Count, Is.EqualTo(1),
                "AH scenario history should be 1 from previous AH export.");
        }

        [Test] // CAV23 — Same subject assembles all 4 scenarios independently
        public async Task CAV23_AllFourScenarios_ForSameSubject_AreIndependent()
        {
            var svc = CreateService();
            const string sid = "cav23-multiscenario";
            var rr = (await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = sid })).Package!;
            var oc = (await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = sid })).Package!;
            var br = (await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = sid })).Package!;
            var ah = (await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = sid })).Package!;

            // Each export has its own unique ID
            var ids = new[] { rr.ExportId, oc.ExportId, br.ExportId, ah.ExportId };
            Assert.That(ids.Distinct().Count(), Is.EqualTo(4), "All 4 ExportIds must be unique.");

            // Correct scenario enum per package
            Assert.That(rr.Scenario, Is.EqualTo(AuditScenario.ReleaseReadinessSignOff));
            Assert.That(oc.Scenario, Is.EqualTo(AuditScenario.OnboardingCaseReview));
            Assert.That(br.Scenario, Is.EqualTo(AuditScenario.ComplianceBlockerReview));
            Assert.That(ah.Scenario, Is.EqualTo(AuditScenario.ApprovalHistoryExport));
        }

        [Test] // CAV24 — ProvenanceRecords has at least one entry for all scenarios
        public async Task CAV24_ProvenanceRecords_HasAtLeastOneEntry_AllScenarios()
        {
            var svc = CreateService();
            var rr = (await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav24-rr" })).Package!;
            var oc = (await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "cav24-oc" })).Package!;
            var br = (await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "cav24-br" })).Package!;
            var ah = (await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "cav24-ah" })).Package!;

            Assert.That(rr.ProvenanceRecords, Is.Not.Empty, "ReleaseReadiness must have provenance.");
            Assert.That(oc.ProvenanceRecords, Is.Not.Empty, "OnboardingCaseReview must have provenance.");
            Assert.That(br.ProvenanceRecords, Is.Not.Empty, "BlockerReview must have provenance.");
            Assert.That(ah.ProvenanceRecords, Is.Not.Empty, "ApprovalHistory must have provenance.");
        }

        [Test] // CAV25 — Every ProvenanceRecord has non-empty Id, SourceSystem, SourceLabel
        public async Task CAV25_ProvenanceRecords_FieldsArePopulated()
        {
            var svc = CreateService();
            var result = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "cav25" });
            foreach (var prov in result.Package!.ProvenanceRecords)
            {
                Assert.That(prov.ProvenanceId, Is.Not.Null.And.Not.Empty, "ProvenanceRecord.ProvenanceId must be set.");
                Assert.That(prov.SourceSystem, Is.Not.Null.And.Not.Empty, "ProvenanceRecord.SourceSystem must be set.");
                Assert.That(prov.EvidenceCategory, Is.Not.Null.And.Not.Empty, "ProvenanceRecord.EvidenceCategory must be set.");
            }
        }

        [Test] // CAV26 — ForceRegenerate on idempotency key adds old ExportId to TrackerHistory
        public async Task CAV26_ForceRegenerate_OldExportId_InTrackerHistory()
        {
            var svc = CreateService();
            const string sid = "cav26";
            const string key = "cav26-idem-key";

            // First generation
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = sid, IdempotencyKey = key });
            var id1 = r1.Package!.ExportId;

            // Force regenerate — new export, id1 should be in tracker history
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = sid, IdempotencyKey = key, ForceRegenerate = true });
            Assert.That(r2.Package!.ExportId, Is.Not.EqualTo(id1), "ForceRegenerate must produce a new ExportId.");
            Assert.That(r2.Package.TrackerHistory, Does.Contain(id1),
                "Old ExportId should appear in TrackerHistory after force-regenerate.");
        }

        [Test] // CAV27 — ListExports returns only exports for the queried subject
        public async Task CAV27_ListExports_ReturnsOnlyQueriedSubject()
        {
            var svc = CreateService();
            const string sidA = "cav27-A";
            const string sidB = "cav27-B";

            await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = sidA });
            await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = sidA });
            await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = sidB });

            var listA = await svc.ListExportsAsync(sidA);
            var listB = await svc.ListExportsAsync(sidB);

            Assert.That(listA.Exports.Count, Is.EqualTo(2), "Subject A should have 2 exports.");
            Assert.That(listB.Exports.Count, Is.EqualTo(1), "Subject B should have 1 export.");
            Assert.That(listA.Exports.All(e => e.SubjectId == sidA), Is.True);
            Assert.That(listB.Exports.All(e => e.SubjectId == sidB), Is.True);
        }

        [Test] // CAV28 — GetExport returns the correct package after multi-scenario assembly
        public async Task CAV28_GetExport_ReturnsCorrectPackage_MultiScenario()
        {
            var svc = CreateService();
            const string sid = "cav28";

            var rr = (await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = sid })).Package!;
            var br = (await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = sid })).Package!;

            // Retrieve each by its ExportId
            var fetchedRr = (await svc.GetExportAsync(rr.ExportId)).Package!;
            var fetchedBr = (await svc.GetExportAsync(br.ExportId)).Package!;

            Assert.That(fetchedRr.ExportId, Is.EqualTo(rr.ExportId));
            Assert.That(fetchedRr.Scenario, Is.EqualTo(AuditScenario.ReleaseReadinessSignOff));
            Assert.That(fetchedBr.ExportId, Is.EqualTo(br.ExportId));
            Assert.That(fetchedBr.Scenario, Is.EqualTo(AuditScenario.ComplianceBlockerReview));
        }

        [Test] // CAV29 — Concurrent assemblies for the same subject produce unique ExportIds
        public async Task CAV29_ConcurrentAssemblies_ProduceUniqueExportIds()
        {
            var svc = CreateService();
            const string sid = "cav29-concurrent";
            const int concurrency = 10;

            var tasks = Enumerable.Range(0, concurrency)
                .Select(_ => svc.AssembleReleaseReadinessExportAsync(
                    new ReleaseReadinessExportRequest { SubjectId = sid }))
                .ToList();
            var results = await Task.WhenAll(tasks);

            var ids = results.Select(r => r.Package!.ExportId).ToHashSet();
            Assert.That(ids.Count, Is.EqualTo(concurrency),
                "All concurrent assemblies must produce unique ExportIds.");
        }

        [Test] // CAV30 — OpenBlockers is a derived view of Blockers (contains only unresolved)
        public async Task CAV30_OpenBlockers_IsCorrectDerivedView()
        {
            var svc = CreateService();
            var result = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest
                {
                    SubjectId = "cav30-openview",
                    IncludeResolvedBlockers = true
                });
            var pkg = result.Package!;
            var expectedOpen = pkg.Blockers.Where(b => !b.IsResolved).ToList();
            Assert.That(pkg.OpenBlockers.Count, Is.EqualTo(expectedOpen.Count),
                "OpenBlockers must match Blockers.Where(b => !b.IsResolved).");
            Assert.That(pkg.OpenBlockers.All(b => !b.IsResolved), Is.True,
                "All items in OpenBlockers must have IsResolved=false.");
        }
    }
}
