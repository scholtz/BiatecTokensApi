using BiatecTokensApi.Models.ComplianceAuditExport;
using BiatecTokensApi.Models.RegulatoryEvidencePackage;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Regulator-grade tests for <see cref="ComplianceAuditExportService"/>.
    ///
    /// These tests focus on scenarios that matter most for enterprise and regulatory use:
    /// determinism, schema contract stability, freshness classifications, audience
    /// profile semantics, blocker severity, provenance integrity, and cross-scenario
    /// consistency checks required for MiCA-aligned audit reporting.
    ///
    /// RG01-RG10:  Schema contract stability (all required fields present and non-null/non-empty)
    /// RG11-RG20:  Audience-profile semantics (all 4 profiles × key assertions)
    /// RG21-RG30:  Determinism and idempotency (same inputs → same output 3×)
    /// RG31-RG40:  Blocker severity classification and remediation hints
    /// RG41-RG50:  Freshness state boundary verification
    /// RG51-RG60:  Provenance integrity and cross-scenario aggregation
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceAuditExportRegulatorGradeTests
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

        // ═══════════════════════════════════════════════════════════════════════
        // RG01-RG10: Schema contract stability
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // RG01 — ExportId is a non-empty GUID string
        public async Task RG01_ExportId_IsNonEmptyGuid()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg01-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(Guid.TryParse(resp.Package!.ExportId, out _), Is.True,
                "ExportId must be a valid GUID");
        }

        [Test] // RG02 — AssembledAt is set and recent (within last 5 seconds)
        public async Task RG02_AssembledAt_IsRecentUtc()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg02-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.AssembledAt, Is.GreaterThanOrEqualTo(before));
            Assert.That(resp.Package.AssembledAt, Is.LessThanOrEqualTo(DateTime.UtcNow.AddSeconds(2)));
        }

        [Test] // RG03 — ContentHash is exactly 64 hex characters (SHA-256)
        public async Task RG03_ContentHash_Is64HexChars()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg03-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.ContentHash, Has.Length.EqualTo(64));
            Assert.That(resp.Package.ContentHash, Does.Match("^[0-9a-f]{64}$"),
                "ContentHash must be lowercase hex");
        }

        [Test] // RG04 — SchemaVersion is set and non-empty
        public async Task RG04_SchemaVersion_IsNonEmpty()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg04-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test] // RG05 — SubjectId is echoed back correctly in the package
        public async Task RG05_SubjectId_IsEchoedInPackage()
        {
            const string subjectId = "rg05-subject-echo-test";
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.SubjectId, Is.EqualTo(subjectId));
        }

        [Test] // RG06 — Readiness headline is non-empty for all 4 scenarios
        public async Task RG06_ReadinessHeadline_IsNonEmptyForAllScenarios()
        {
            var svc = CreateService();

            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg06-s1" });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "rg06-s2" });
            var r3 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "rg06-s3" });
            var r4 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "rg06-s4" });

            foreach (var resp in new[] { r1, r2, r3, r4 })
            {
                Assert.That(resp.Package, Is.Not.Null);
                Assert.That(resp.Package!.ReadinessHeadline, Is.Not.Null.And.Not.Empty,
                    $"ReadinessHeadline must be set for scenario");
            }
        }

        [Test] // RG07 — ProvenanceRecords list is not null (may be empty)
        public async Task RG07_ProvenanceRecords_IsNotNull()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg07-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.ProvenanceRecords, Is.Not.Null);
        }

        [Test] // RG08 — TrackerHistory is not null
        public async Task RG08_TrackerHistory_IsNotNull()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg08-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.TrackerHistory, Is.Not.Null);
        }

        [Test] // RG09 — ExpiresAt is after AssembledAt
        public async Task RG09_ExpiresAt_IsAfterAssembledAt()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg09-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            if (resp.Package!.ExpiresAt.HasValue)
            {
                Assert.That(resp.Package.ExpiresAt.Value, Is.GreaterThan(resp.Package.AssembledAt));
            }
        }

        [Test] // RG10 — PolicyVersion is set and non-empty
        public async Task RG10_PolicyVersion_IsNonEmpty()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg10-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.PolicyVersion, Is.Not.Null.And.Not.Empty);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RG11-RG20: Audience-profile semantics
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // RG11 — InternalCompliance profile is recorded in the package
        public async Task RG11_InternalCompliance_ProfileRecordedInPackage()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "rg11-subject",
                AudienceProfile = RegulatoryAudienceProfile.InternalCompliance
            });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.AudienceProfile,
                Is.EqualTo(RegulatoryAudienceProfile.InternalCompliance));
        }

        [Test] // RG12 — ExecutiveSignOff profile is recorded in the package
        public async Task RG12_ExecutiveSignOff_ProfileRecordedInPackage()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "rg12-subject",
                AudienceProfile = RegulatoryAudienceProfile.ExecutiveSignOff
            });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.AudienceProfile,
                Is.EqualTo(RegulatoryAudienceProfile.ExecutiveSignOff));
        }

        [Test] // RG13 — ExternalAuditor profile is recorded in the package
        public async Task RG13_ExternalAuditor_ProfileRecordedInPackage()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "rg13-subject",
                AudienceProfile = RegulatoryAudienceProfile.ExternalAuditor
            });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.AudienceProfile,
                Is.EqualTo(RegulatoryAudienceProfile.ExternalAuditor));
        }

        [Test] // RG14 — RegulatorReview profile is recorded in the package
        public async Task RG14_RegulatorReview_ProfileRecordedInPackage()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "rg14-subject",
                AudienceProfile = RegulatoryAudienceProfile.RegulatorReview
            });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.AudienceProfile,
                Is.EqualTo(RegulatoryAudienceProfile.RegulatorReview));
        }

        [Test] // RG15 — Different profiles for the same subject produce different ExportIds
        public async Task RG15_DifferentProfiles_ProduceDifferentExportIds()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "rg15-subject",
                AudienceProfile = RegulatoryAudienceProfile.InternalCompliance
            });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "rg15-subject",
                AudienceProfile = RegulatoryAudienceProfile.RegulatorReview
            });
            Assert.That(r1.Package!.ExportId, Is.Not.EqualTo(r2.Package!.ExportId),
                "Different profiles should produce distinct exports");
        }

        [Test] // RG16 — OnboardingCaseReview: AudienceProfile stored correctly
        public async Task RG16_OnboardingCaseReview_AudienceProfileStored()
        {
            var svc = CreateService();
            var resp = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest
                {
                    SubjectId = "rg16-subject",
                    AudienceProfile = RegulatoryAudienceProfile.ExternalAuditor
                });
            Assert.That(resp.Package!.AudienceProfile,
                Is.EqualTo(RegulatoryAudienceProfile.ExternalAuditor));
        }

        [Test] // RG17 — ApprovalHistoryExport with RegulatorReview profile stored correctly
        public async Task RG17_ApprovalHistoryExport_RegulatorReviewProfileStored()
        {
            var svc = CreateService();
            var resp = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest
                {
                    SubjectId = "rg17-subject",
                    AudienceProfile = RegulatoryAudienceProfile.RegulatorReview
                });
            Assert.That(resp.Package!.AudienceProfile,
                Is.EqualTo(RegulatoryAudienceProfile.RegulatorReview));
        }

        [Test] // RG18 — BlockerReview with all 4 audience profiles succeeds
        public async Task RG18_BlockerReview_AllAudienceProfiles_Succeed()
        {
            var svc = CreateService();
            foreach (RegulatoryAudienceProfile profile in Enum.GetValues<RegulatoryAudienceProfile>())
            {
                var resp = await svc.AssembleBlockerReviewExportAsync(
                    new ComplianceBlockerReviewExportRequest
                    {
                        SubjectId = $"rg18-{profile}",
                        AudienceProfile = profile
                    });
                Assert.That(resp.Success, Is.True, $"Profile={profile} should succeed");
                Assert.That(resp.Package, Is.Not.Null, $"Package should be present for profile={profile}");
            }
        }

        [Test] // RG19 — Summary.AudienceProfile matches Package.AudienceProfile
        public async Task RG19_Summary_AudienceProfile_MatchesPackage()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "rg19-subject",
                AudienceProfile = RegulatoryAudienceProfile.ExecutiveSignOff
            });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.Summary.AudienceProfile,
                Is.EqualTo(resp.Package.AudienceProfile));
        }

        [Test] // RG20 — Summary.IsRegulatorReady matches Package.IsRegulatorReady
        public async Task RG20_Summary_IsRegulatorReady_MatchesPackage()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg20-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.Summary.IsRegulatorReady,
                Is.EqualTo(resp.Package.IsRegulatorReady));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RG21-RG30: Determinism and idempotency
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // RG21 — Explicit idempotency key: 3 calls return the same ExportId
        public async Task RG21_ExplicitKey_ThreeCalls_SameExportId()
        {
            var svc = CreateService();
            const string key = "rg21-idempotency-key";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg21-s", IdempotencyKey = key });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg21-s", IdempotencyKey = key });
            var r3 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg21-s", IdempotencyKey = key });

            Assert.That(r1.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
            Assert.That(r2.Package!.ExportId, Is.EqualTo(r3.Package!.ExportId));
        }

        [Test] // RG22 — Explicit idempotency key: 2nd and 3rd calls are marked as idempotent replays
        public async Task RG22_ExplicitKey_SecondCall_IsIdempotentReplay()
        {
            var svc = CreateService();
            const string key = "rg22-idempotency-key";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg22-s", IdempotencyKey = key });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg22-s", IdempotencyKey = key });

            Assert.That(r1.IsIdempotentReplay, Is.False, "First call should NOT be a replay");
            Assert.That(r2.IsIdempotentReplay, Is.True, "Second call MUST be a replay");
        }

        [Test] // RG23 — Null idempotency key: 3 calls produce 3 distinct ExportIds
        public async Task RG23_NullKey_ThreeCalls_DistinctExportIds()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg23-s", IdempotencyKey = null });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg23-s", IdempotencyKey = null });
            var r3 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg23-s", IdempotencyKey = null });

            Assert.That(r1.Package!.ExportId, Is.Not.EqualTo(r2.Package!.ExportId));
            Assert.That(r2.Package!.ExportId, Is.Not.EqualTo(r3.Package!.ExportId));
            Assert.That(r1.Package.ExportId, Is.Not.EqualTo(r3.Package!.ExportId));
        }

        [Test] // RG24 — Null key: none of the 3 calls are marked as idempotent replays
        public async Task RG24_NullKey_NoCalls_AreReplays()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg24-s", IdempotencyKey = null });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg24-s", IdempotencyKey = null });
            var r3 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg24-s", IdempotencyKey = null });

            Assert.That(r1.IsIdempotentReplay, Is.False);
            Assert.That(r2.IsIdempotentReplay, Is.False);
            Assert.That(r3.IsIdempotentReplay, Is.False);
        }

        [Test] // RG25 — ForceRegenerate=true with explicit key produces a new ExportId
        public async Task RG25_ForceRegenerate_WithExplicitKey_ProducesNewExportId()
        {
            var svc = CreateService();
            const string key = "rg25-force-key";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg25-s", IdempotencyKey = key });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg25-s", IdempotencyKey = key, ForceRegenerate = true });

            Assert.That(r2.Package!.ExportId, Is.Not.EqualTo(r1.Package!.ExportId),
                "ForceRegenerate must produce a new export");
        }

        [Test] // RG26 — OnboardingCaseReview: explicit key is idempotent
        public async Task RG26_OnboardingCaseReview_ExplicitKey_Idempotent()
        {
            var svc = CreateService();
            const string key = "rg26-onboarding-key";
            var r1 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "rg26-s", IdempotencyKey = key });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "rg26-s", IdempotencyKey = key });

            Assert.That(r1.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
        }

        [Test] // RG27 — BlockerReview: explicit key is idempotent
        public async Task RG27_BlockerReview_ExplicitKey_Idempotent()
        {
            var svc = CreateService();
            const string key = "rg27-blocker-key";
            var r1 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "rg27-s", IdempotencyKey = key });
            var r2 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "rg27-s", IdempotencyKey = key });

            Assert.That(r1.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
        }

        [Test] // RG28 — ApprovalHistory: explicit key is idempotent
        public async Task RG28_ApprovalHistory_ExplicitKey_Idempotent()
        {
            var svc = CreateService();
            const string key = "rg28-approval-key";
            var r1 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "rg28-s", IdempotencyKey = key });
            var r2 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "rg28-s", IdempotencyKey = key });

            Assert.That(r1.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
        }

        [Test] // RG29 — Two different explicit keys for same subject produce different exports
        public async Task RG29_TwoDifferentKeys_SameSubject_DifferentExports()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg29-s", IdempotencyKey = "key-A" });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg29-s", IdempotencyKey = "key-B" });

            Assert.That(r1.Package!.ExportId, Is.Not.EqualTo(r2.Package!.ExportId));
        }

        [Test] // RG30 — CorrelationId is preserved across idempotent replay
        public async Task RG30_CorrelationId_PreservedInIdempotentReplay()
        {
            var svc = CreateService();
            const string correlationId = "rg30-correlation-abc123";
            const string key = "rg30-key";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "rg30-s",
                IdempotencyKey = key,
                CorrelationId = correlationId
            });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "rg30-s",
                IdempotencyKey = key,
                CorrelationId = correlationId
            });

            Assert.That(r2.Package!.CorrelationId, Is.EqualTo(correlationId));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RG31-RG40: Blocker severity and remediation
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // RG31 — Blockers list is not null for all 4 scenarios
        public async Task RG31_Blockers_IsNotNull_ForAllScenarios()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg31-s1" });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "rg31-s2" });
            var r3 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "rg31-s3" });
            var r4 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "rg31-s4" });

            Assert.That(r1.Package!.Blockers, Is.Not.Null);
            Assert.That(r2.Package!.Blockers, Is.Not.Null);
            Assert.That(r3.Package!.Blockers, Is.Not.Null);
            Assert.That(r4.Package!.Blockers, Is.Not.Null);
        }

        [Test] // RG32 — CriticalBlockerCount equals count of unresolved critical-severity blockers
        public async Task RG32_CriticalBlockerCount_MatchesActualCriticalUnresolvedBlockers()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg32-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            int expected = resp.Package!.Blockers
                .Count(b => !b.IsResolved && b.Severity == AuditBlockerSeverity.Critical);
            Assert.That(resp.Package.CriticalBlockerCount, Is.EqualTo(expected));
        }

        [Test] // RG33 — OpenBlockers returns only unresolved blockers
        public async Task RG33_OpenBlockers_ContainsOnlyUnresolvedBlockers()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg33-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            foreach (var blocker in resp.Package!.OpenBlockers)
            {
                Assert.That(blocker.IsResolved, Is.False,
                    $"OpenBlockers should contain only unresolved items; found resolved: {blocker.BlockerId}");
            }
        }

        [Test] // RG34 — BlockerReview section is populated for BlockerReview scenario
        public async Task RG34_BlockerReviewSection_IsPopulatedForBlockerScenario()
        {
            var svc = CreateService();
            var resp = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "rg34-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.BlockerReview, Is.Not.Null,
                "BlockerReview section must be present for BlockerReview scenario");
        }

        [Test] // RG35 — BlockerReview.OpenBlockerCount is >= 0
        public async Task RG35_BlockerReview_OpenBlockerCount_IsNonNegative()
        {
            var svc = CreateService();
            var resp = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "rg35-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.BlockerReview!.OpenBlockerCount, Is.GreaterThanOrEqualTo(0));
        }

        [Test] // RG36 — BlockerReview.ResolvedBlockerCount is >= 0
        public async Task RG36_BlockerReview_ResolvedBlockerCount_IsNonNegative()
        {
            var svc = CreateService();
            var resp = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "rg36-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.BlockerReview!.ResolvedBlockerCount, Is.GreaterThanOrEqualTo(0));
        }

        [Test] // RG37 — Each blocker has a non-empty BlockerId
        public async Task RG37_EachBlocker_HasNonEmptyBlockerId()
        {
            var svc = CreateService();
            var resp = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "rg37-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            foreach (var b in resp.Package!.Blockers)
            {
                Assert.That(b.BlockerId, Is.Not.Null.And.Not.Empty,
                    "Each blocker must have a non-empty BlockerId");
            }
        }

        [Test] // RG38 — Each blocker has a non-empty Title
        public async Task RG38_EachBlocker_HasNonEmptyTitle()
        {
            var svc = CreateService();
            var resp = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "rg38-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            foreach (var b in resp.Package!.Blockers)
            {
                Assert.That(b.Title, Is.Not.Null.And.Not.Empty,
                    $"Blocker {b.BlockerId} must have a non-empty Title");
            }
        }

        [Test] // RG39 — ReleaseReadiness section is not null for ReleaseReadiness scenario
        public async Task RG39_ReleaseReadinessSection_NotNull_ForReleaseScenario()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg39-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.ReleaseReadiness, Is.Not.Null,
                "ReleaseReadiness section must be present for ReleaseReadiness scenario");
        }

        [Test] // RG40 — ApprovalHistory section is not null for ApprovalHistory scenario
        public async Task RG40_ApprovalHistorySection_NotNull_ForApprovalScenario()
        {
            var svc = CreateService();
            var resp = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "rg40-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.ApprovalHistory, Is.Not.Null,
                "ApprovalHistory section must be present for ApprovalHistory scenario");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RG41-RG50: Freshness state boundary verification
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // RG41 — FreshnessState values only include valid enum members
        public async Task RG41_FreshnessState_ContainsOnlyValidEnumValues()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg41-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            foreach (var prov in resp.Package!.ProvenanceRecords)
            {
                Assert.That(Enum.IsDefined(typeof(AuditEvidenceFreshness), prov.FreshnessState),
                    $"FreshnessState {prov.FreshnessState} is not a valid enum value");
            }
        }

        [Test] // RG42 — Readiness enum values are all defined members
        public async Task RG42_ReadinessEnum_IsDefinedMember()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg42-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(Enum.IsDefined(typeof(AuditExportReadiness), resp.Package!.Readiness),
                $"Readiness {resp.Package.Readiness} is not a valid enum value");
        }

        [Test] // RG43 — Summary.Readiness matches Package.Readiness
        public async Task RG43_Summary_Readiness_MatchesPackage()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg43-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.Summary.Readiness, Is.EqualTo(resp.Package.Readiness));
        }

        [Test] // RG44 — FakeTimeProvider: AssembledAt reflects injected time
        public async Task RG44_FakeTimeProvider_AssembledAt_ReflectsInjectedTime()
        {
            var fakeTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
            var tp = new FakeTimeProvider(fakeTime);
            var svc = CreateService(tp);
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg44-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.AssembledAt.Date,
                Is.EqualTo(fakeTime.UtcDateTime.Date));
        }

        [Test] // RG45 — FakeTimeProvider: ExpiresAt is after injected time
        public async Task RG45_FakeTimeProvider_ExpiresAt_AfterInjectedTime()
        {
            var fakeTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
            var tp = new FakeTimeProvider(fakeTime);
            var svc = CreateService(tp);
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg45-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            if (resp.Package!.ExpiresAt.HasValue)
            {
                Assert.That(resp.Package.ExpiresAt.Value,
                    Is.GreaterThan(resp.Package.AssembledAt));
            }
        }

        [Test] // RG46 — FakeTimeProvider advancing time produces new AssembledAt
        public async Task RG46_FakeTimeProvider_AdvancingTime_ProducesNewAssembledAt()
        {
            var fakeTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
            var tp = new FakeTimeProvider(fakeTime);
            var svc = CreateService(tp);

            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg46-s" });
            tp.Advance(TimeSpan.FromDays(1));
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg46-s" });

            Assert.That(r2.Package!.AssembledAt, Is.GreaterThan(r1.Package!.AssembledAt));
        }

        [Test] // RG47 — All provenance records have a non-empty SourceSystem
        public async Task RG47_AllProvenance_HasNonEmptySourceSystem()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg47-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            foreach (var p in resp.Package!.ProvenanceRecords)
            {
                Assert.That(p.SourceSystem, Is.Not.Null.And.Not.Empty,
                    $"ProvenanceId={p.ProvenanceId} must have a non-empty SourceSystem");
            }
        }

        [Test] // RG48 — All provenance records have a non-empty EvidenceCategory
        public async Task RG48_AllProvenance_HasNonEmptyEvidenceCategory()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg48-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            foreach (var p in resp.Package!.ProvenanceRecords)
            {
                Assert.That(p.EvidenceCategory, Is.Not.Null.And.Not.Empty,
                    $"ProvenanceId={p.ProvenanceId} must have a non-empty EvidenceCategory");
            }
        }

        [Test] // RG49 — All provenance records have a non-empty ProvenanceId
        public async Task RG49_AllProvenance_HasNonEmptyProvenanceId()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg49-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            foreach (var p in resp.Package!.ProvenanceRecords)
            {
                Assert.That(p.ProvenanceId, Is.Not.Null.And.Not.Empty,
                    "Each provenance record must have a non-empty ProvenanceId");
            }
        }

        [Test] // RG50 — All provenance records have a CapturedAt that is not the default (MinValue)
        public async Task RG50_AllProvenance_CapturedAt_IsNotDefault()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg50-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            foreach (var p in resp.Package!.ProvenanceRecords)
            {
                Assert.That(p.CapturedAt, Is.GreaterThan(DateTime.MinValue),
                    $"ProvenanceId={p.ProvenanceId} must have a valid CapturedAt");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RG51-RG60: Provenance integrity and cross-scenario aggregation
        // ═══════════════════════════════════════════════════════════════════════

        [Test] // RG51 — List exports: newly created export appears in list for the same subject
        public async Task RG51_ListExports_NewExportAppearsForSubject()
        {
            var svc = CreateService();
            const string subjectId = "rg51-subject";
            await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId });
            var list = await svc.ListExportsAsync(subjectId);
            Assert.That(list.Success, Is.True);
            Assert.That(list.Exports, Has.Count.GreaterThanOrEqualTo(1));
        }

        [Test] // RG52 — List exports: different subjects do not share exports
        public async Task RG52_ListExports_DifferentSubjects_HaveIsolatedExports()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg52-subjectA" });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg52-subjectB" });

            var listA = await svc.ListExportsAsync("rg52-subjectA");
            var listB = await svc.ListExportsAsync("rg52-subjectB");

            var idsA = listA.Exports.Select(e => e.ExportId).ToHashSet();
            var idsB = listB.Exports.Select(e => e.ExportId).ToHashSet();

            Assert.That(idsA.Contains(r1.Package!.ExportId), Is.True);
            Assert.That(idsB.Contains(r2.Package!.ExportId), Is.True);
            Assert.That(idsA.Overlaps(idsB), Is.False, "Exports from different subjects must not overlap");
        }

        [Test] // RG53 — GetExport returns the correct package by ID
        public async Task RG53_GetExport_ReturnsCorrectPackageById()
        {
            var svc = CreateService();
            var created = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg53-subject" });
            var exportId = created.Package!.ExportId;

            var fetched = await svc.GetExportAsync(exportId);
            Assert.That(fetched.Success, Is.True);
            Assert.That(fetched.Package, Is.Not.Null);
            Assert.That(fetched.Package!.ExportId, Is.EqualTo(exportId));
        }

        [Test] // RG54 — GetExport with unknown ID returns Success=false
        public async Task RG54_GetExport_UnknownId_ReturnsFail()
        {
            var svc = CreateService();
            var fetched = await svc.GetExportAsync("rg54-nonexistent-id-xyz");
            Assert.That(fetched.Success, Is.False);
            Assert.That(fetched.Package, Is.Null);
        }

        [Test] // RG55 — Summary.ExportId matches Package.ExportId
        public async Task RG55_Summary_ExportId_MatchesPackage()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg55-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.Summary.ExportId, Is.EqualTo(resp.Package.ExportId));
        }

        [Test] // RG56 — Summary.SubjectId matches Package.SubjectId
        public async Task RG56_Summary_SubjectId_MatchesPackage()
        {
            var svc = CreateService();
            const string subjectId = "rg56-unique-subject";
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.Summary.SubjectId, Is.EqualTo(subjectId));
        }

        [Test] // RG57 — Four scenarios on the same subject produce 4 distinct exports in the list
        public async Task RG57_FourScenarios_SameSubject_FourDistinctExports()
        {
            var svc = CreateService();
            const string subjectId = "rg57-subject";
            await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = subjectId });
            await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = subjectId });
            await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = subjectId });
            await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = subjectId });

            var list = await svc.ListExportsAsync(subjectId);
            Assert.That(list.Exports, Has.Count.GreaterThanOrEqualTo(4),
                "All 4 scenario exports must appear in the subject's export list");
        }

        [Test] // RG58 — Summary.ContentHash matches Package.ContentHash
        public async Task RG58_Summary_ContentHash_MatchesPackage()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg58-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.Summary.ContentHash, Is.EqualTo(resp.Package.ContentHash));
        }

        [Test] // RG59 — Summary.ProvenanceRecordCount matches actual count
        public async Task RG59_Summary_ProvenanceRecordCount_MatchesActual()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "rg59-subject" });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.Summary.ProvenanceRecordCount,
                Is.EqualTo(resp.Package.ProvenanceRecords.Count));
        }

        [Test] // RG60 — RequestorNotes are propagated to the package
        public async Task RG60_RequestorNotes_ArePropagatedToPackage()
        {
            const string notes = "RG60 — regulator submission batch Q2-2026";
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            {
                SubjectId = "rg60-subject",
                RequestorNotes = notes
            });
            Assert.That(resp.Package, Is.Not.Null);
            Assert.That(resp.Package!.RequestorNotes, Is.EqualTo(notes));
        }
    }
}
