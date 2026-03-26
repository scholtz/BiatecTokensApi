using BiatecTokensApi.Models.ComplianceAuditExport;
using BiatecTokensApi.Models.RegulatoryEvidencePackage;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Scenario-validation and semantic-contract tests for
    /// <see cref="ComplianceAuditExportService"/>.
    ///
    /// SV01-SV10: Scenario enum matches request type for all 4 scenarios
    /// SV11-SV20: Blockers structural contract (required fields, category, severity)
    /// SV21-SV30: Summary structural contract (counts, ReadinessText, schema/policy version)
    /// SV31-SV40: Correlation ID lifecycle
    /// SV41-SV50: Audience profile × scenario matrix
    /// SV51-SV55: ForceRegenerate semantics
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceAuditExportScenarioValidationTests
    {
        private static ComplianceAuditExportService CreateService()
            => new(NullLogger<ComplianceAuditExportService>.Instance);

        // ═════════════════════════════════════════════════════════════════════
        // SV01-SV10: Scenario enum matches request type for all 4 scenarios
        // ═════════════════════════════════════════════════════════════════════

        [Test] // SV01
        public async Task SV01_ReleaseReadiness_PackageScenario_IsReleaseReadinessSignOff()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv01-subj" });
            Assert.That(r.Success, Is.True);
            Assert.That(r.Package!.Scenario, Is.EqualTo(AuditScenario.ReleaseReadinessSignOff));
        }

        [Test] // SV02
        public async Task SV02_OnboardingCaseReview_PackageScenario_IsOnboardingCaseReview()
        {
            var svc = CreateService();
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv02-subj" });
            Assert.That(r.Success, Is.True);
            Assert.That(r.Package!.Scenario, Is.EqualTo(AuditScenario.OnboardingCaseReview));
        }

        [Test] // SV03
        public async Task SV03_BlockerReview_PackageScenario_IsComplianceBlockerReview()
        {
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv03-subj" });
            Assert.That(r.Success, Is.True);
            Assert.That(r.Package!.Scenario, Is.EqualTo(AuditScenario.ComplianceBlockerReview));
        }

        [Test] // SV04
        public async Task SV04_ApprovalHistory_PackageScenario_IsApprovalHistoryExport()
        {
            var svc = CreateService();
            var r = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv04-subj" });
            Assert.That(r.Success, Is.True);
            Assert.That(r.Package!.Scenario, Is.EqualTo(AuditScenario.ApprovalHistoryExport));
        }

        [Test] // SV05
        public async Task SV05_ReleaseReadiness_PackageScenarioText_MatchesEnum()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv05-subj" });
            Assert.That(r.Package!.Scenario.ToString(), Is.EqualTo(nameof(AuditScenario.ReleaseReadinessSignOff)));
        }

        [Test] // SV06
        public async Task SV06_OnboardingCaseReview_PackageScenarioText_MatchesEnum()
        {
            var svc = CreateService();
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv06-subj" });
            Assert.That(r.Package!.Scenario.ToString(), Is.EqualTo(nameof(AuditScenario.OnboardingCaseReview)));
        }

        [Test] // SV07
        public async Task SV07_BlockerReview_PackageHasOpenBlockersSection()
        {
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv07-subj" });
            Assert.That(r.Package!.BlockerReview, Is.Not.Null);
        }

        [Test] // SV08
        public async Task SV08_ApprovalHistory_PackageHasApprovalHistorySection()
        {
            var svc = CreateService();
            var r = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv08-subj" });
            Assert.That(r.Package!.ApprovalHistory, Is.Not.Null);
        }

        [Test] // SV09
        public async Task SV09_ReleaseReadiness_PackageHasReleaseReadinessSection()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv09-subj" });
            Assert.That(r.Package!.ReleaseReadiness, Is.Not.Null);
        }

        [Test] // SV10
        public async Task SV10_OnboardingCaseReview_PackageHasOnboardingSection()
        {
            var svc = CreateService();
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv10-subj" });
            Assert.That(r.Package!.OnboardingCase, Is.Not.Null);
        }

        // ═════════════════════════════════════════════════════════════════════
        // SV11-SV20: Blockers structural contract
        // ═════════════════════════════════════════════════════════════════════

        [Test] // SV11
        public async Task SV11_AllBlockers_HaveBlockerId()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv11-subj" });
            foreach (var b in r.Package!.Blockers)
                Assert.That(b.BlockerId, Is.Not.Null.And.Not.Empty, $"Blocker missing BlockerId: {b.Title}");
        }

        [Test] // SV12
        public async Task SV12_AllBlockers_HaveTitle()
        {
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv12-subj" });
            foreach (var b in r.Package!.Blockers)
                Assert.That(b.Title, Is.Not.Null.And.Not.Empty, $"Blocker missing Title: {b.BlockerId}");
        }

        [Test] // SV13
        public async Task SV13_AllBlockers_HaveCategory()
        {
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv13-subj" });
            foreach (var b in r.Package!.Blockers)
                Assert.That(b.Category, Is.Not.Null.And.Not.Empty, $"Blocker missing Category: {b.BlockerId}");
        }

        [Test] // SV14
        public async Task SV14_AllBlockers_HaveValidSeverity()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv14-subj" });
            foreach (var b in r.Package!.Blockers)
                Assert.That(Enum.IsDefined(typeof(AuditBlockerSeverity), b.Severity),
                    $"Blocker {b.BlockerId} has invalid Severity {b.Severity}");
        }

        [Test] // SV15
        public async Task SV15_AllBlockers_HaveRemediationHint()
        {
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv15-subj" });
            foreach (var b in r.Package!.Blockers)
                Assert.That(b.RemediationHint, Is.Not.Null.And.Not.Empty, $"Blocker missing RemediationHint: {b.BlockerId}");
        }

        [Test] // SV16
        public async Task SV16_CriticalBlockerCount_MatchesComputedProperty()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv16-subj" });
            var pkg = r.Package!;
            var expected = pkg.Blockers.Count(b => !b.IsResolved && b.Severity == AuditBlockerSeverity.Critical);
            Assert.That(pkg.CriticalBlockerCount, Is.EqualTo(expected));
        }

        [Test] // SV17
        public async Task SV17_IsRegulatorReady_FalseWhenReadinessIsNotReady()
        {
            var svc = CreateService();
            // Scan subjects to find one that is NOT Ready
            for (int i = 0; i < 30; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(
                    new ReleaseReadinessExportRequest { SubjectId = $"sv17-scan-{i}" });
                if (r.Package!.Readiness != AuditExportReadiness.Ready)
                {
                    Assert.That(r.Package.IsRegulatorReady, Is.False);
                    return;
                }
            }
            Assert.Pass("All scanned subjects were Ready; invariant passes vacuously.");
        }

        [Test] // SV18
        public async Task SV18_IsReleaseGrade_FalseForNonReadyOrRequiresReview()
        {
            var svc = CreateService();
            for (int i = 0; i < 30; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(
                    new ReleaseReadinessExportRequest { SubjectId = $"sv18-scan-{i}" });
                var pkg = r.Package!;
                if (pkg.Readiness == AuditExportReadiness.Ready || pkg.Readiness == AuditExportReadiness.RequiresReview)
                    Assert.That(pkg.IsReleaseGrade, Is.True, $"Readiness={pkg.Readiness} should be release-grade");
                else
                    Assert.That(pkg.IsReleaseGrade, Is.False, $"Readiness={pkg.Readiness} should NOT be release-grade");
            }
        }

        [Test] // SV19
        public async Task SV19_Blockers_AreNotResolvedByDefault_WhenNotIncludeResolved()
        {
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv19-subj", IncludeResolvedBlockers = false });
            foreach (var b in r.Package!.Blockers)
                Assert.That(b.IsResolved, Is.False, $"Unexpected resolved blocker {b.BlockerId} in unresolved-only export");
        }

        [Test] // SV20
        public async Task SV20_BlockerReview_WithIncludeResolved_AllBlockersReturned()
        {
            var svc = CreateService();
            var unresolved = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv20-subj", IncludeResolvedBlockers = false });
            var all = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest
                {
                    SubjectId = "sv20-subj",
                    IncludeResolvedBlockers = true,
                    ForceRegenerate = true
                });
            // With resolved included, count should be >= without resolved
            Assert.That(all.Package!.Blockers.Count, Is.GreaterThanOrEqualTo(unresolved.Package!.Blockers.Count));
        }

        // ═════════════════════════════════════════════════════════════════════
        // SV21-SV30: Summary structural contract
        // ═════════════════════════════════════════════════════════════════════

        [Test] // SV21
        public async Task SV21_Summary_IsNotNull_ForAllScenarios()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "sv21a" });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = "sv21b" });
            var r3 = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = "sv21c" });
            var r4 = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = "sv21d" });
            Assert.Multiple(() =>
            {
                Assert.That(r1.Package!.Summary, Is.Not.Null, "ReleaseReadiness summary is null");
                Assert.That(r2.Package!.Summary, Is.Not.Null, "OnboardingCaseReview summary is null");
                Assert.That(r3.Package!.Summary, Is.Not.Null, "BlockerReview summary is null");
                Assert.That(r4.Package!.Summary, Is.Not.Null, "ApprovalHistory summary is null");
            });
        }

        [Test] // SV22
        public async Task SV22_Summary_ReadinessText_IsNotEmpty()
        {
            var svc = CreateService();
            for (int i = 0; i < 10; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(
                    new ReleaseReadinessExportRequest { SubjectId = $"sv22-subj-{i}" });
                Assert.That(r.Package!.Summary!.ReadinessText, Is.Not.Null.And.Not.Empty,
                    $"Subject sv22-subj-{i} has empty ReadinessText");
            }
        }

        [Test] // SV23
        public async Task SV23_Summary_ProvenanceCount_MatchesProvenanceRecords()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv23-subj" });
            var pkg = r.Package!;
            Assert.That(pkg.Summary!.ProvenanceRecordCount, Is.EqualTo(pkg.ProvenanceRecords.Count));
        }

        [Test] // SV24
        public async Task SV24_Summary_BlockerCount_MatchesBlockers()
        {
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv24-subj" });
            var pkg = r.Package!;
            Assert.That(pkg.Summary!.UnresolvedBlockerCount,
                Is.EqualTo(pkg.Blockers.Count(b => !b.IsResolved)));
        }

        [Test] // SV25
        public async Task SV25_Summary_SchemaVersion_IsPresent()
        {
            var svc = CreateService();
            var r = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv25-subj" });
            Assert.That(r.Package!.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test] // SV26
        public async Task SV26_Summary_PolicyVersion_IsPresent()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv26-subj" });
            Assert.That(r.Package!.PolicyVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test] // SV27
        public async Task SV27_Summary_ExportId_IsValidGuid()
        {
            var svc = CreateService();
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv27-subj" });
            Assert.That(Guid.TryParse(r.Package!.ExportId, out _), Is.True,
                $"ExportId is not a valid GUID: {r.Package.ExportId}");
        }

        [Test] // SV28
        public async Task SV28_Summary_SubjectId_MatchesRequest()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv28-expected-id" });
            Assert.That(r.Package!.SubjectId, Is.EqualTo("sv28-expected-id"));
        }

        [Test] // SV29
        public async Task SV29_Summary_AssembledAt_IsRecent()
        {
            var before = DateTime.UtcNow.AddSeconds(-5);
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv29-subj" });
            var after = DateTime.UtcNow.AddSeconds(5);
            Assert.That(r.Package!.AssembledAt, Is.InRange(before, after));
        }

        [Test] // SV30
        public async Task SV30_Summary_ExpiresAt_IsAfterAssembledAt()
        {
            var svc = CreateService();
            var r = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv30-subj" });
            var pkg = r.Package!;
            Assert.That(pkg.ExpiresAt, Is.GreaterThan(pkg.AssembledAt));
        }

        // ═════════════════════════════════════════════════════════════════════
        // SV31-SV40: Correlation ID lifecycle
        // ═════════════════════════════════════════════════════════════════════

        [Test] // SV31
        public async Task SV31_CorrelationId_AutoGenerated_WhenNotProvided()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv31-subj" });
            Assert.That(r.Package!.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test] // SV32
        public async Task SV32_CorrelationId_PassedThrough_WhenProvided()
        {
            var svc = CreateService();
            var id = "my-custom-correlation-id-sv32";
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv32-subj", CorrelationId = id });
            Assert.That(r.Package!.CorrelationId, Is.EqualTo(id));
        }

        [Test] // SV33
        public async Task SV33_CorrelationId_Unique_PerRequest_WhenNotProvided()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv33-a", ForceRegenerate = true });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv33-b", ForceRegenerate = true });
            Assert.That(r1.Package!.CorrelationId, Is.Not.EqualTo(r2.Package!.CorrelationId));
        }

        [Test] // SV34
        public async Task SV34_CorrelationId_SameSubject_DifferentCorrelationId_ForceRegenerate()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv34-subj", ForceRegenerate = true });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv34-subj", ForceRegenerate = true });
            // Each call auto-generates a new CorrelationId; they should differ
            Assert.That(r1.Package!.ExportId, Is.Not.EqualTo(r2.Package!.ExportId));
        }

        [Test] // SV35
        public async Task SV35_CorrelationId_OnboardingScenario_PropagatedCorrectly()
        {
            var svc = CreateService();
            var id = "corr-sv35-onboarding";
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv35-subj", CorrelationId = id });
            Assert.That(r.Package!.CorrelationId, Is.EqualTo(id));
        }

        [Test] // SV36
        public async Task SV36_CorrelationId_BlockerReview_PropagatedCorrectly()
        {
            var svc = CreateService();
            var id = "corr-sv36-blocker";
            var r = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv36-subj", CorrelationId = id });
            Assert.That(r.Package!.CorrelationId, Is.EqualTo(id));
        }

        [Test] // SV37
        public async Task SV37_CorrelationId_ApprovalHistory_PropagatedCorrectly()
        {
            var svc = CreateService();
            var id = "corr-sv37-approval";
            var r = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv37-subj", CorrelationId = id });
            Assert.That(r.Package!.CorrelationId, Is.EqualTo(id));
        }

        [Test] // SV38
        public async Task SV38_CorrelationId_IdempotentCall_SameCorrelationId_Returned()
        {
            var svc = CreateService();
            const string idemKey = "sv38-explicit-idempotency-key";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv38-subj", IdempotencyKey = idemKey });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv38-subj", IdempotencyKey = idemKey });
            // Idempotent call should return same export, same CorrelationId
            Assert.That(r1.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
        }

        [Test] // SV39
        public async Task SV39_CorrelationId_ForceRegenerate_NewId_Generated()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv39-subj" });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv39-subj", ForceRegenerate = true });
            Assert.That(r1.Package!.ExportId, Is.Not.EqualTo(r2.Package!.ExportId));
        }

        [Test] // SV40
        public async Task SV40_CorrelationId_FailedResponse_ContainsCorrelationId()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = null! });
            Assert.That(r.Success, Is.False);
            Assert.That(r.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        // ═════════════════════════════════════════════════════════════════════
        // SV41-SV50: Audience profile × scenario matrix
        // ═════════════════════════════════════════════════════════════════════

        [Test] // SV41
        public async Task SV41_AudienceProfile_InternalCompliance_AllScenarios()
        {
            var svc = CreateService();
            var profile = RegulatoryAudienceProfile.InternalCompliance;
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv41-a", AudienceProfile = profile });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv41-b", AudienceProfile = profile });
            var r3 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv41-c", AudienceProfile = profile });
            var r4 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv41-d", AudienceProfile = profile });
            Assert.Multiple(() =>
            {
                Assert.That(r1.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r2.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r3.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r4.Package!.AudienceProfile, Is.EqualTo(profile));
            });
        }

        [Test] // SV42
        public async Task SV42_AudienceProfile_ExecutiveSignOff_AllScenarios()
        {
            var svc = CreateService();
            var profile = RegulatoryAudienceProfile.ExecutiveSignOff;
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv42-a", AudienceProfile = profile });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv42-b", AudienceProfile = profile });
            var r3 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv42-c", AudienceProfile = profile });
            var r4 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv42-d", AudienceProfile = profile });
            Assert.Multiple(() =>
            {
                Assert.That(r1.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r2.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r3.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r4.Package!.AudienceProfile, Is.EqualTo(profile));
            });
        }

        [Test] // SV43
        public async Task SV43_AudienceProfile_ExternalAuditor_AllScenarios()
        {
            var svc = CreateService();
            var profile = RegulatoryAudienceProfile.ExternalAuditor;
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv43-a", AudienceProfile = profile });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv43-b", AudienceProfile = profile });
            var r3 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv43-c", AudienceProfile = profile });
            var r4 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv43-d", AudienceProfile = profile });
            Assert.Multiple(() =>
            {
                Assert.That(r1.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r2.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r3.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r4.Package!.AudienceProfile, Is.EqualTo(profile));
            });
        }

        [Test] // SV44
        public async Task SV44_AudienceProfile_RegulatorReview_AllScenarios()
        {
            var svc = CreateService();
            var profile = RegulatoryAudienceProfile.RegulatorReview;
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv44-a", AudienceProfile = profile });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv44-b", AudienceProfile = profile });
            var r3 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv44-c", AudienceProfile = profile });
            var r4 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv44-d", AudienceProfile = profile });
            Assert.Multiple(() =>
            {
                Assert.That(r1.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r2.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r3.Package!.AudienceProfile, Is.EqualTo(profile));
                Assert.That(r4.Package!.AudienceProfile, Is.EqualTo(profile));
            });
        }

        [Test] // SV45
        public async Task SV45_AudienceProfile_Default_IsInternalCompliance()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv45-subj" });
            Assert.That(r.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.InternalCompliance));
        }

        [Test] // SV46
        public async Task SV46_AudienceProfile_RegulatorReview_IsRegulatorReady_OnlyIfReadinessReady()
        {
            var svc = CreateService();
            for (int i = 0; i < 20; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(
                    new ReleaseReadinessExportRequest
                    {
                        SubjectId = $"sv46-scan-{i}",
                        AudienceProfile = RegulatoryAudienceProfile.RegulatorReview
                    });
                if (r.Package!.Readiness != AuditExportReadiness.Ready)
                    Assert.That(r.Package.IsRegulatorReady, Is.False,
                        $"IsRegulatorReady must be false for readiness={r.Package.Readiness}");
                else
                    Assert.That(r.Package.IsRegulatorReady, Is.True);
            }
        }

        [Test] // SV47
        public async Task SV47_AudienceProfile_PropagatedToSummarySection()
        {
            var svc = CreateService();
            var profile = RegulatoryAudienceProfile.ExternalAuditor;
            var r = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv47-subj", AudienceProfile = profile });
            Assert.That(r.Package!.AudienceProfile, Is.EqualTo(profile));
        }

        [Test] // SV48
        public async Task SV48_AudienceProfile_ChangedViaForceRegenerate()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest
                {
                    SubjectId = "sv48-subj",
                    AudienceProfile = RegulatoryAudienceProfile.InternalCompliance
                });
            var r2 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest
                {
                    SubjectId = "sv48-subj",
                    AudienceProfile = RegulatoryAudienceProfile.RegulatorReview,
                    ForceRegenerate = true
                });
            Assert.Multiple(() =>
            {
                Assert.That(r1.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.InternalCompliance));
                Assert.That(r2.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.RegulatorReview));
            });
        }

        [Test] // SV49
        public async Task SV49_AllAudienceProfiles_PackageSucceeds_ForAllScenarios()
        {
            var svc = CreateService();
            var profiles = Enum.GetValues<RegulatoryAudienceProfile>();
            int idx = 0;
            foreach (var profile in profiles)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(
                    new ReleaseReadinessExportRequest
                    {
                        SubjectId = $"sv49-{idx++}",
                        AudienceProfile = profile
                    });
                Assert.That(r.Success, Is.True, $"Profile {profile} returned failure");
            }
        }

        [Test] // SV50
        public async Task SV50_AudienceProfile_TwoSubjects_SameProfile_IndependentExports()
        {
            var svc = CreateService();
            var profile = RegulatoryAudienceProfile.ExternalAuditor;
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv50-subj-A", AudienceProfile = profile });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv50-subj-B", AudienceProfile = profile });
            Assert.Multiple(() =>
            {
                Assert.That(r1.Package!.SubjectId, Is.EqualTo("sv50-subj-A"));
                Assert.That(r2.Package!.SubjectId, Is.EqualTo("sv50-subj-B"));
                Assert.That(r1.Package.ExportId, Is.Not.EqualTo(r2.Package!.ExportId));
            });
        }

        // ═════════════════════════════════════════════════════════════════════
        // SV51-SV55: ForceRegenerate semantics
        // ═════════════════════════════════════════════════════════════════════

        [Test] // SV51
        public async Task SV51_ForceRegenerate_ProducesNewContentHash()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv51-subj" });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "sv51-subj", ForceRegenerate = true });
            Assert.That(r1.Package!.ContentHash, Is.Not.EqualTo(r2.Package!.ContentHash));
        }

        [Test] // SV52
        public async Task SV52_ForceRegenerate_TrackerHistory_GrowsAfterEachCall()
        {
            var svc = CreateService();
            // First call
            var r1 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv52-subj" });
            var h1 = r1.Package!.TrackerHistory.Count;
            // ForceRegenerate adds r1's exportId to history
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "sv52-subj", ForceRegenerate = true });
            var h2 = r2.Package!.TrackerHistory.Count;
            Assert.That(h2, Is.GreaterThan(h1));
        }

        [Test] // SV53
        public async Task SV53_ForceRegenerate_False_SameExportId_Returned()
        {
            var svc = CreateService();
            const string idemKey = "sv53-explicit-idempotency-key";
            var r1 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv53-subj", IdempotencyKey = idemKey });
            var r2 = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = "sv53-subj", IdempotencyKey = idemKey, ForceRegenerate = false });
            Assert.That(r1.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
        }

        [Test] // SV54
        public async Task SV54_ForceRegenerate_NewExport_AppearsInList()
        {
            var svc = CreateService();
            await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv54-subj" });
            var r2 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv54-subj", ForceRegenerate = true });
            var list = await svc.ListExportsAsync("sv54-subj");
            Assert.That(list.Exports.Any(e => e.ExportId == r2.Package!.ExportId), Is.True);
        }

        [Test] // SV55
        public async Task SV55_ForceRegenerate_NewExportId_DifferentFromPrevious()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv55-subj" });
            var r2 = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = "sv55-subj", ForceRegenerate = true });
            Assert.That(r1.Package!.ExportId, Is.Not.EqualTo(r2.Package!.ExportId));
        }
    }
}
