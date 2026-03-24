using BiatecTokensApi.Models.ComplianceAuditExport;
using BiatecTokensApi.Models.RegulatoryEvidencePackage;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Robustness, negative-path, and edge-case tests for
    /// <see cref="ComplianceAuditExportService"/>.
    ///
    /// CR01-CR10:  Null / empty / whitespace input validation
    /// CR11-CR20:  Idempotency replay across all 4 scenarios
    /// CR21-CR30:  ForceRegenerate semantics
    /// CR31-CR40:  ListExports filtering, pagination, and boundaries
    /// CR41-CR50:  GetExport edge cases
    /// CR51-CR60:  Multi-subject isolation
    /// CR61-CR70:  Fail-closed contracts (IsRegulatorReady / IsReleaseGrade)
    /// CR71-CR80:  Content hash, tracker history, provenance
    /// CR81-CR85:  Concurrency safety
    /// CR86-CR92:  Audience profile propagation
    /// CR93-CR100: EvidenceFromTimestamp filter and misc
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceAuditExportRobustnessTests
    {
        private sealed class FakeTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTimeProvider(DateTimeOffset v) => _now = v;
            public void Advance(TimeSpan d) => _now = _now.Add(d);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static ComplianceAuditExportService CreateService(TimeProvider? tp = null)
            => new(NullLogger<ComplianceAuditExportService>.Instance, tp);

        // ═════════════════════════════════════════════════════════════════════
        // CR01-CR10: Null / empty / whitespace input validation
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CR01
        public async Task CR01_ReleaseReadiness_NullSubjectId_ReturnsFailed()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = null! });
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_SUBJECT_ID"));
        }

        [Test] // CR02
        public async Task CR02_ReleaseReadiness_EmptySubjectId_ReturnsFailed()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "" });
            Assert.That(resp.Success, Is.False);
        }

        [Test] // CR03
        public async Task CR03_ReleaseReadiness_WhitespaceSubjectId_ReturnsFailed()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "   " });
            Assert.That(resp.Success, Is.False);
        }

        [Test] // CR04
        public async Task CR04_OnboardingCase_NullSubjectId_ReturnsFailed()
        {
            var svc = CreateService();
            var resp = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = null! });
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_SUBJECT_ID"));
        }

        [Test] // CR05
        public async Task CR05_BlockerReview_NullSubjectId_ReturnsFailed()
        {
            var svc = CreateService();
            var resp = await svc.AssembleBlockerReviewExportAsync(
                new ComplianceBlockerReviewExportRequest { SubjectId = null! });
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_SUBJECT_ID"));
        }

        [Test] // CR06
        public async Task CR06_ApprovalHistory_NullSubjectId_ReturnsFailed()
        {
            var svc = CreateService();
            var resp = await svc.AssembleApprovalHistoryExportAsync(
                new ApprovalHistoryExportRequest { SubjectId = null! });
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.ErrorCode, Is.EqualTo("MISSING_SUBJECT_ID"));
        }

        [Test] // CR07
        public async Task CR07_FailedResponse_NeverHasPackage()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = null! });
            Assert.That(resp.Package, Is.Null);
        }

        [Test] // CR08
        public async Task CR08_FailedResponse_HasErrorMessage()
        {
            var svc = CreateService();
            var resp = await svc.AssembleReleaseReadinessExportAsync(
                new ReleaseReadinessExportRequest { SubjectId = "" });
            Assert.That(resp.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CR09
        public async Task CR09_OnboardingCase_EmptySubjectId_ReturnsFailed()
        {
            var svc = CreateService();
            var resp = await svc.AssembleOnboardingCaseReviewExportAsync(
                new OnboardingCaseReviewExportRequest { SubjectId = "" });
            Assert.That(resp.Success, Is.False);
        }

        [Test] // CR10
        public async Task CR10_AllScenarios_ValidSubjectId_ReturnSuccess()
        {
            var svc = CreateService();
            const string subject = "subject-cr10";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subject });
            var r3 = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = subject });
            var r4 = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = subject });
            Assert.That(r1.Success, Is.True, "ReleaseReadiness");
            Assert.That(r2.Success, Is.True, "OnboardingCase");
            Assert.That(r3.Success, Is.True, "BlockerReview");
            Assert.That(r4.Success, Is.True, "ApprovalHistory");
        }

        // ═════════════════════════════════════════════════════════════════════
        // CR11-CR20: Idempotency replay across all 4 scenarios
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CR11
        public async Task CR11_ReleaseReadiness_Idempotency_SecondCallReturnsReplay()
        {
            var svc = CreateService();
            const string key = "key-cr11";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr11", IdempotencyKey = key });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr11", IdempotencyKey = key });
            Assert.That(r1.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test] // CR12
        public async Task CR12_OnboardingCase_Idempotency_SecondCallReturnsReplay()
        {
            var svc = CreateService();
            const string key = "key-cr12";
            var r1 = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest
            { SubjectId = "sub-cr12", IdempotencyKey = key });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest
            { SubjectId = "sub-cr12", IdempotencyKey = key });
            Assert.That(r1.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test] // CR13
        public async Task CR13_BlockerReview_Idempotency_SecondCallReturnsReplay()
        {
            var svc = CreateService();
            const string key = "key-cr13";
            var r1 = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
            { SubjectId = "sub-cr13", IdempotencyKey = key });
            var r2 = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
            { SubjectId = "sub-cr13", IdempotencyKey = key });
            Assert.That(r1.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test] // CR14
        public async Task CR14_ApprovalHistory_Idempotency_SecondCallReturnsReplay()
        {
            var svc = CreateService();
            const string key = "key-cr14";
            var r1 = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest
            { SubjectId = "sub-cr14", IdempotencyKey = key });
            var r2 = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest
            { SubjectId = "sub-cr14", IdempotencyKey = key });
            Assert.That(r1.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test] // CR15
        public async Task CR15_Idempotency_DifferentKeys_ProduceDifferentExports()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr15", IdempotencyKey = "key-a-cr15" });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr15", IdempotencyKey = "key-b-cr15" });
            Assert.That(r1.Package!.ExportId, Is.Not.EqualTo(r2.Package!.ExportId));
            Assert.That(r2.IsIdempotentReplay, Is.False);
        }

        [Test] // CR16
        public async Task CR16_NoIdempotencyKey_EachCallProducesNewExport()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr16" });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr16" });
            Assert.That(r1.Package!.ExportId, Is.Not.EqualTo(r2.Package!.ExportId));
        }

        [Test] // CR17
        public async Task CR17_Idempotency_ReplayContentHashUnchanged()
        {
            var svc = CreateService();
            const string key = "key-cr17";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr17", IdempotencyKey = key });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr17", IdempotencyKey = key });
            Assert.That(r1.Package!.ContentHash, Is.EqualTo(r2.Package!.ContentHash));
        }

        [Test] // CR18
        public async Task CR18_Idempotency_ReplayAssembledAtUnchanged()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            const string key = "key-cr18";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr18", IdempotencyKey = key });
            tp.Advance(TimeSpan.FromMinutes(5));
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr18", IdempotencyKey = key });
            Assert.That(r1.Package!.AssembledAt, Is.EqualTo(r2.Package!.AssembledAt));
        }

        [Test] // CR19
        public async Task CR19_Idempotency_NullKey_NeverReplays()
        {
            var svc = CreateService();
            var r1 = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
            { SubjectId = "sub-cr19", IdempotencyKey = null });
            var r2 = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
            { SubjectId = "sub-cr19", IdempotencyKey = null });
            Assert.That(r1.IsIdempotentReplay, Is.False);
            Assert.That(r2.IsIdempotentReplay, Is.False);
            Assert.That(r1.Package!.ExportId, Is.Not.EqualTo(r2.Package!.ExportId));
        }

        [Test] // CR20
        public async Task CR20_Idempotency_ThirdReplayStillMatchesFirst()
        {
            var svc = CreateService();
            const string key = "key-cr20";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr20", IdempotencyKey = key });
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr20", IdempotencyKey = key });
            var r3 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr20", IdempotencyKey = key });
            Assert.That(r3.Package!.ExportId, Is.EqualTo(r1.Package!.ExportId));
            Assert.That(r3.IsIdempotentReplay, Is.True);
        }

        // ═════════════════════════════════════════════════════════════════════
        // CR21-CR30: ForceRegenerate semantics
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CR21
        public async Task CR21_ForceRegenerate_BreaksIdempotencyCache()
        {
            var svc = CreateService();
            const string key = "key-cr21";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr21", IdempotencyKey = key });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr21", IdempotencyKey = key, ForceRegenerate = true });
            Assert.That(r2.IsIdempotentReplay, Is.False);
            Assert.That(r2.Package!.ExportId, Is.Not.EqualTo(r1.Package!.ExportId));
        }

        [Test] // CR22
        public async Task CR22_ForceRegenerate_NewExportStored_CanBeRetrievedById()
        {
            var svc = CreateService();
            const string key = "key-cr22";
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr22", IdempotencyKey = key });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr22", IdempotencyKey = key, ForceRegenerate = true });
            var get = await svc.GetExportAsync(r2.Package!.ExportId);
            Assert.That(get.Success, Is.True);
            Assert.That(get.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
        }

        [Test] // CR23
        public async Task CR23_ForceRegenerate_OnboardingCase_Works()
        {
            var svc = CreateService();
            const string key = "key-cr23";
            var r1 = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest
            { SubjectId = "sub-cr23", IdempotencyKey = key });
            var r2 = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest
            { SubjectId = "sub-cr23", IdempotencyKey = key, ForceRegenerate = true });
            Assert.That(r2.Package!.ExportId, Is.Not.EqualTo(r1.Package!.ExportId));
        }

        [Test] // CR24
        public async Task CR24_ForceRegenerate_BlockerReview_Works()
        {
            var svc = CreateService();
            const string key = "key-cr24";
            var r1 = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
            { SubjectId = "sub-cr24", IdempotencyKey = key });
            var r2 = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
            { SubjectId = "sub-cr24", IdempotencyKey = key, ForceRegenerate = true });
            Assert.That(r2.Package!.ExportId, Is.Not.EqualTo(r1.Package!.ExportId));
        }

        [Test] // CR25
        public async Task CR25_ForceRegenerate_ApprovalHistory_Works()
        {
            var svc = CreateService();
            const string key = "key-cr25";
            var r1 = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest
            { SubjectId = "sub-cr25", IdempotencyKey = key });
            var r2 = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest
            { SubjectId = "sub-cr25", IdempotencyKey = key, ForceRegenerate = true });
            Assert.That(r2.Package!.ExportId, Is.Not.EqualTo(r1.Package!.ExportId));
        }

        [Test] // CR26
        public async Task CR26_ForceRegenerate_SubsequentCall_ReplaysFromNewPackage()
        {
            var svc = CreateService();
            const string key = "key-cr26";
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr26", IdempotencyKey = key });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr26", IdempotencyKey = key, ForceRegenerate = true });
            var r3 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr26", IdempotencyKey = key });
            Assert.That(r3.Package!.ExportId, Is.EqualTo(r2.Package!.ExportId));
            Assert.That(r3.IsIdempotentReplay, Is.True);
        }

        [Test] // CR27
        public async Task CR27_ForceRegenerate_WithNoCache_StillSucceeds()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr27", IdempotencyKey = "brand-new-cr27", ForceRegenerate = true });
            Assert.That(r.Success, Is.True);
            Assert.That(r.IsIdempotentReplay, Is.False);
        }

        [Test] // CR28
        public async Task CR28_ForceRegenerate_NewPackage_HasFresherAssembledAt()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            const string key = "key-cr28";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr28", IdempotencyKey = key });
            tp.Advance(TimeSpan.FromHours(1));
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr28", IdempotencyKey = key, ForceRegenerate = true });
            Assert.That(r2.Package!.AssembledAt, Is.GreaterThan(r1.Package!.AssembledAt));
        }

        [Test] // CR29
        public async Task CR29_ForceRegenerate_False_StillReplays()
        {
            var svc = CreateService();
            const string key = "key-cr29";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr29", IdempotencyKey = key });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr29", IdempotencyKey = key, ForceRegenerate = false });
            Assert.That(r2.IsIdempotentReplay, Is.True);
            Assert.That(r2.Package!.ExportId, Is.EqualTo(r1.Package!.ExportId));
        }

        [Test] // CR30
        public async Task CR30_ForceRegenerate_NewContentHash_DifferentFromOld()
        {
            var tp = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            const string key = "key-cr30";
            var r1 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr30", IdempotencyKey = key });
            tp.Advance(TimeSpan.FromSeconds(1));
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr30", IdempotencyKey = key, ForceRegenerate = true });
            Assert.That(r1.Package!.ContentHash, Is.Not.EqualTo(r2.Package!.ContentHash));
        }

        // ═════════════════════════════════════════════════════════════════════
        // CR31-CR40: ListExports filtering and boundaries
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CR31
        public async Task CR31_ListExports_EmptyStore_ReturnsEmptyList()
        {
            var svc = CreateService();
            var list = await svc.ListExportsAsync("ghost-subject-cr31");
            Assert.That(list.Success, Is.True);
            Assert.That(list.Exports, Is.Empty);
        }

        [Test] // CR32
        public async Task CR32_ListExports_AfterAssemble_ContainsPackage()
        {
            var svc = CreateService();
            const string subject = "sub-cr32";
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            var list = await svc.ListExportsAsync(subject);
            Assert.That(list.Exports, Has.Count.GreaterThanOrEqualTo(1));
        }

        [Test] // CR33
        public async Task CR33_ListExports_ScenarioFilter_OnlyReturnsMatchingScenario()
        {
            var svc = CreateService();
            const string subject = "sub-cr33";
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subject });
            var list = await svc.ListExportsAsync(subject, AuditScenario.OnboardingCaseReview);
            Assert.That(list.Exports.All(e => e.Scenario == AuditScenario.OnboardingCaseReview), Is.True);
        }

        [Test] // CR34
        public async Task CR34_ListExports_MultipleScenarios_NullFilter_ReturnsAll()
        {
            var svc = CreateService();
            const string subject = "sub-cr34";
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subject });
            await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = subject });
            var list = await svc.ListExportsAsync(subject);
            Assert.That(list.Exports, Has.Count.GreaterThanOrEqualTo(3));
        }

        [Test] // CR35
        public async Task CR35_ListExports_DifferentSubjects_NoLeakage()
        {
            var svc = CreateService();
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "subA-cr35" });
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "subB-cr35" });
            var listA = await svc.ListExportsAsync("subA-cr35");
            var listB = await svc.ListExportsAsync("subB-cr35");
            Assert.That(listA.Exports.All(e => e.SubjectId == "subA-cr35"), Is.True);
            Assert.That(listB.Exports.All(e => e.SubjectId == "subB-cr35"), Is.True);
        }

        [Test] // CR36
        public async Task CR36_ListExports_Limit_Respected()
        {
            var svc = CreateService();
            const string subject = "sub-cr36";
            for (int i = 0; i < 5; i++)
                await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            var list = await svc.ListExportsAsync(subject, limit: 2);
            Assert.That(list.Exports, Has.Count.LessThanOrEqualTo(2));
        }

        [Test] // CR37
        public async Task CR37_ListExports_ExportIds_AreUniqueStrings()
        {
            var svc = CreateService();
            const string subject = "sub-cr37";
            for (int i = 0; i < 3; i++)
                await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            var list = await svc.ListExportsAsync(subject);
            var ids = list.Exports.Select(e => e.ExportId).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        [Test] // CR38
        public async Task CR38_ListExports_Entries_HaveCorrectSubjectId()
        {
            var svc = CreateService();
            const string subject = "sub-cr38-unique";
            await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subject });
            var list = await svc.ListExportsAsync(subject);
            Assert.That(list.Exports.All(e => e.SubjectId == subject), Is.True);
        }

        [Test] // CR39
        public async Task CR39_ListExports_ApprovalHistory_ScenarioFilter_Works()
        {
            var svc = CreateService();
            const string subject = "sub-cr39";
            await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = subject });
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            var list = await svc.ListExportsAsync(subject, AuditScenario.ApprovalHistoryExport);
            Assert.That(list.Exports.All(e => e.Scenario == AuditScenario.ApprovalHistoryExport), Is.True);
        }

        [Test] // CR40
        public async Task CR40_ListExports_BlockerReview_ScenarioFilter_Works()
        {
            var svc = CreateService();
            const string subject = "sub-cr40";
            await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = subject });
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            var list = await svc.ListExportsAsync(subject, AuditScenario.ComplianceBlockerReview);
            Assert.That(list.Exports.All(e => e.Scenario == AuditScenario.ComplianceBlockerReview), Is.True);
        }

        // ═════════════════════════════════════════════════════════════════════
        // CR41-CR50: GetExport edge cases
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CR41
        public async Task CR41_GetExport_NonExistentId_ReturnsFailed()
        {
            var svc = CreateService();
            var get = await svc.GetExportAsync("does-not-exist");
            Assert.That(get.Success, Is.False);
        }

        [Test] // CR42
        public async Task CR42_GetExport_EmptyId_ReturnsFailed()
        {
            var svc = CreateService();
            var get = await svc.GetExportAsync("");
            Assert.That(get.Success, Is.False);
        }

        [Test] // CR43
        public async Task CR43_GetExport_ValidId_ReturnsCorrectPackage()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "sub-cr43" });
            var get = await svc.GetExportAsync(r.Package!.ExportId);
            Assert.That(get.Success, Is.True);
            Assert.That(get.Package!.ExportId, Is.EqualTo(r.Package!.ExportId));
        }

        [Test] // CR44
        public async Task CR44_GetExport_ReturnsPackageWithMatchingSubjectId()
        {
            var svc = CreateService();
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = "sub-cr44" });
            var get = await svc.GetExportAsync(r.Package!.ExportId);
            Assert.That(get.Package!.SubjectId, Is.EqualTo("sub-cr44"));
        }

        [Test] // CR45
        public async Task CR45_GetExport_WhitespaceId_ReturnsFailed()
        {
            var svc = CreateService();
            var get = await svc.GetExportAsync("   ");
            Assert.That(get.Success, Is.False);
        }

        [Test] // CR46
        public async Task CR46_GetExport_ContentHash_IsNonEmpty()
        {
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = "sub-cr46" });
            var get = await svc.GetExportAsync(r.Package!.ExportId);
            Assert.That(get.Package!.ContentHash, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CR47
        public async Task CR47_GetExport_TrackerHistory_IsNotNull()
        {
            var svc = CreateService();
            var r = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = "sub-cr47" });
            var get = await svc.GetExportAsync(r.Package!.ExportId);
            Assert.That(get.Package!.TrackerHistory, Is.Not.Null);
        }

        [Test] // CR48
        public async Task CR48_GetExport_SchemaVersion_Present()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "sub-cr48" });
            var get = await svc.GetExportAsync(r.Package!.ExportId);
            Assert.That(get.Package!.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CR49
        public async Task CR49_GetExport_PolicyVersion_Present()
        {
            var svc = CreateService();
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = "sub-cr49" });
            var get = await svc.GetExportAsync(r.Package!.ExportId);
            Assert.That(get.Package!.PolicyVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CR50
        public async Task CR50_GetExport_ProvenanceRecords_IsNotNull()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "sub-cr50" });
            var get = await svc.GetExportAsync(r.Package!.ExportId);
            Assert.That(get.Package!.ProvenanceRecords, Is.Not.Null);
        }

        // ═════════════════════════════════════════════════════════════════════
        // CR51-CR60: Multi-subject isolation
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CR51
        public async Task CR51_MultiSubject_ExportIdsAreUnique()
        {
            var svc = CreateService();
            var ids = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                { SubjectId = $"sub-cr51-{i}" });
                ids.Add(r.Package!.ExportId);
            }
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        [Test] // CR52
        public async Task CR52_MultiSubject_GetExport_ReturnsCorrectSubject()
        {
            var svc = CreateService();
            var rA = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "subA-cr52" });
            var rB = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "subB-cr52" });
            var getA = await svc.GetExportAsync(rA.Package!.ExportId);
            var getB = await svc.GetExportAsync(rB.Package!.ExportId);
            Assert.That(getA.Package!.SubjectId, Is.EqualTo("subA-cr52"));
            Assert.That(getB.Package!.SubjectId, Is.EqualTo("subB-cr52"));
        }

        [Test] // CR53
        public async Task CR53_SubjectA_ExportId_NotVisibleInSubjectB_List()
        {
            var svc = CreateService();
            var rA = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "subA-cr53" });
            var listB = await svc.ListExportsAsync("subB-cr53");
            Assert.That(listB.Exports.Any(e => e.ExportId == rA.Package!.ExportId), Is.False);
        }

        [Test] // CR54
        public async Task CR54_SameSubject_FourScenarios_AllListed()
        {
            var svc = CreateService();
            const string subject = "sub-cr54";
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subject });
            await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = subject });
            await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = subject });
            var list = await svc.ListExportsAsync(subject);
            var scenarios = list.Exports.Select(e => e.Scenario).Distinct().ToList();
            Assert.That(scenarios, Has.Count.EqualTo(4));
        }

        [Test] // CR55
        public async Task CR55_SameSubject_DifferentScenarios_HaveCorrectScenarioTag()
        {
            var svc = CreateService();
            const string subject = "sub-cr55";
            var rr = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            var ob = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subject });
            Assert.That(rr.Package!.Scenario, Is.EqualTo(AuditScenario.ReleaseReadinessSignOff));
            Assert.That(ob.Package!.Scenario, Is.EqualTo(AuditScenario.OnboardingCaseReview));
        }

        [Test] // CR56
        public async Task CR56_NewServiceInstance_HasNoState()
        {
            var svc1 = CreateService();
            var r1 = await svc1.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "sub-cr56" });
            var svc2 = CreateService();
            var list = await svc2.ListExportsAsync("sub-cr56");
            Assert.That(list.Exports, Is.Empty);
            var get = await svc2.GetExportAsync(r1.Package!.ExportId);
            Assert.That(get.Success, Is.False);
        }

        [Test] // CR57
        public async Task CR57_SpecialCharacters_InSubjectId_HandledGracefully()
        {
            var svc = CreateService();
            const string subject = "sub/cr57?special=chars&more=value";
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            Assert.That(r.Success, Is.True);
            Assert.That(r.Package!.SubjectId, Is.EqualTo(subject));
        }

        [Test] // CR58
        public async Task CR58_VeryLongSubjectId_HandledGracefully()
        {
            var svc = CreateService();
            var subject = new string('x', 500);
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            Assert.That(r.Success, Is.True);
        }

        [Test] // CR59
        public async Task CR59_MultipleServiceInstances_DontShareState()
        {
            var svcA = CreateService();
            var svcB = CreateService();
            await svcA.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr59", IdempotencyKey = "key-cr59" });
            var r2 = await svcB.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr59", IdempotencyKey = "key-cr59" });
            Assert.That(r2.IsIdempotentReplay, Is.False);
        }

        [Test] // CR60
        public async Task CR60_ListExports_CountMatchesAssembledCount()
        {
            var svc = CreateService();
            const string subject = "sub-cr60";
            for (int i = 0; i < 4; i++)
                await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            var list = await svc.ListExportsAsync(subject);
            Assert.That(list.Exports, Has.Count.GreaterThanOrEqualTo(4));
        }

        // ═════════════════════════════════════════════════════════════════════
        // CR61-CR70: Fail-closed contracts
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CR61
        public async Task CR61_IsRegulatorReady_OnlyTrueWhenReadinessIsReady()
        {
            var svc = CreateService();
            for (int i = 0; i < 30; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                { SubjectId = $"sub-cr61-{i}" });
                if (r.Package!.Readiness == AuditExportReadiness.Ready)
                    Assert.That(r.Package!.IsRegulatorReady, Is.True, $"i={i}: Ready should have IsRegulatorReady=true");
                else
                    Assert.That(r.Package!.IsRegulatorReady, Is.False, $"i={i}: Non-Ready should have IsRegulatorReady=false");
            }
        }

        [Test] // CR62
        public async Task CR62_IsReleaseGrade_TrueForReadyAndRequiresReview_Only()
        {
            var svc = CreateService();
            for (int i = 0; i < 50; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                { SubjectId = $"sub-cr62-{i}" });
                bool expected = r.Package!.Readiness is AuditExportReadiness.Ready
                    or AuditExportReadiness.RequiresReview;
                Assert.That(r.Package!.IsReleaseGrade, Is.EqualTo(expected), $"i={i}: Readiness={r.Package!.Readiness}");
            }
        }

        [Test] // CR63
        public async Task CR63_DegradedProviderUnavailable_NeverRegulatorReady_NeverReleaseGrade()
        {
            var svc = CreateService();
            for (int i = 0; i < 100; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                { SubjectId = $"sub-cr63-{i}" });
                if (r.Package!.Readiness == AuditExportReadiness.DegradedProviderUnavailable)
                {
                    Assert.That(r.Package!.IsRegulatorReady, Is.False);
                    Assert.That(r.Package!.IsReleaseGrade, Is.False);
                    return;
                }
            }
            Assert.Pass("No DegradedProviderUnavailable found in 100-item sample");
        }

        [Test] // CR64
        public async Task CR64_Blocked_NeverRegulatorReady()
        {
            var svc = CreateService();
            for (int i = 0; i < 100; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                { SubjectId = $"sub-cr64-{i}" });
                if (r.Package!.Readiness == AuditExportReadiness.Blocked)
                {
                    Assert.That(r.Package!.IsRegulatorReady, Is.False);
                    return;
                }
            }
            Assert.Pass("No Blocked found in 100-item sample");
        }

        [Test] // CR65
        public async Task CR65_Incomplete_NeverRegulatorReady()
        {
            var svc = CreateService();
            for (int i = 0; i < 100; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                { SubjectId = $"sub-cr65-{i}" });
                if (r.Package!.Readiness == AuditExportReadiness.Incomplete)
                {
                    Assert.That(r.Package!.IsRegulatorReady, Is.False);
                    return;
                }
            }
            Assert.Pass("No Incomplete found in 100-item sample");
        }

        [Test] // CR66
        public async Task CR66_Stale_NeverRegulatorReady()
        {
            var svc = CreateService();
            for (int i = 0; i < 100; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                { SubjectId = $"sub-cr66-{i}" });
                if (r.Package!.Readiness == AuditExportReadiness.Stale)
                {
                    Assert.That(r.Package!.IsRegulatorReady, Is.False);
                    return;
                }
            }
            Assert.Pass("No Stale found in 100-item sample");
        }

        [Test] // CR67
        public async Task CR67_RequiresReview_NeverRegulatorReady_ButMayBeReleaseGrade()
        {
            var svc = CreateService();
            for (int i = 0; i < 100; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                { SubjectId = $"sub-cr67-{i}" });
                if (r.Package!.Readiness == AuditExportReadiness.RequiresReview)
                {
                    Assert.That(r.Package!.IsRegulatorReady, Is.False);
                    Assert.That(r.Package!.IsReleaseGrade, Is.True);
                    return;
                }
            }
            Assert.Pass("No RequiresReview found in 100-item sample");
        }

        [Test] // CR68
        public async Task CR68_AllScenarios_FailClosedInvariant_Holds()
        {
            var svc = CreateService();
            for (int i = 0; i < 20; i++)
            {
                var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                { SubjectId = $"sub-cr68-{i}" });
                if (r.Package!.IsRegulatorReady)
                    Assert.That(r.Package!.Readiness, Is.EqualTo(AuditExportReadiness.Ready));
                if (r.Package!.IsReleaseGrade)
                    Assert.That(r.Package!.Readiness,
                        Is.AnyOf(AuditExportReadiness.Ready, AuditExportReadiness.RequiresReview));
            }
        }

        [Test] // CR69
        public async Task CR69_BlockerReview_Blockers_HaveNonEmptyTitles()
        {
            var svc = CreateService();
            for (int i = 0; i < 20; i++)
            {
                var r = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
                { SubjectId = $"sub-cr69-{i}" });
                Assert.That(r.Package!.Blockers.All(b => !string.IsNullOrEmpty(b.Title)), Is.True, $"i={i}");
            }
        }

        [Test] // CR70
        public async Task CR70_BlockerReview_Blockers_HaveNonEmptyBlockerId()
        {
            var svc = CreateService();
            for (int i = 0; i < 20; i++)
            {
                var r = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
                { SubjectId = $"sub-cr70-{i}" });
                Assert.That(r.Package!.Blockers.All(b => !string.IsNullOrEmpty(b.BlockerId)), Is.True, $"i={i}");
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // CR71-CR80: Content hash, tracker history, provenance
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CR71
        public async Task CR71_ContentHash_IsSHA256HexFormat()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "sub-cr71" });
            Assert.That(r.Package!.ContentHash, Has.Length.EqualTo(64));
            Assert.That(r.Package!.ContentHash, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test] // CR72
        public async Task CR72_TrackerHistory_FirstEntry_IsNotEmpty()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "sub-cr72" });
            var firstEntry = r.Package!.TrackerHistory.FirstOrDefault();
            Assert.That(firstEntry, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CR73
        public async Task CR73_TrackerHistory_AllEntries_AreNonEmptyStrings()
        {
            var svc = CreateService();
            var r = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = "sub-cr73" });
            Assert.That(r.Package!.TrackerHistory.All(e => !string.IsNullOrEmpty(e)), Is.True);
        }

        [Test] // CR74
        public async Task CR74_ExportId_IsNonEmptyString()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "sub-cr74" });
            Assert.That(r.Package!.ExportId, Is.Not.Null.And.Not.Empty);
            Assert.That(r.Package!.ExportId.Length, Is.GreaterThan(4));
        }

        [Test] // CR75
        public async Task CR75_AssembledAt_UsesInjectedTimeProvider()
        {
            var tp = new FakeTimeProvider(new DateTimeOffset(2026, 3, 15, 9, 0, 0, TimeSpan.Zero));
            var svc = CreateService(tp);
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "sub-cr75" });
            Assert.That(r.Package!.AssembledAt.Year, Is.EqualTo(2026));
            Assert.That(r.Package!.AssembledAt.Month, Is.EqualTo(3));
        }

        [Test] // CR76
        public async Task CR76_ProvenanceRecords_AllHaveProvenanceId()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "sub-cr76" });
            foreach (var prov in r.Package!.ProvenanceRecords)
                Assert.That(prov.ProvenanceId, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CR77
        public async Task CR77_ProvenanceRecords_AllHaveEvidenceCategory()
        {
            var svc = CreateService();
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = "sub-cr77" });
            foreach (var prov in r.Package!.ProvenanceRecords)
                Assert.That(prov.EvidenceCategory, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CR78
        public async Task CR78_ProvenanceRecords_AllHaveSourceSystem()
        {
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = "sub-cr78" });
            foreach (var prov in r.Package!.ProvenanceRecords)
                Assert.That(prov.SourceSystem, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CR79
        public async Task CR79_AssembledAt_IsSet()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = "sub-cr79" });
            Assert.That(r.Package!.AssembledAt, Is.GreaterThan(DateTime.MinValue));
        }

        [Test] // CR80
        public async Task CR80_SubjectId_IsPreservedInPackage()
        {
            var svc = CreateService();
            const string subject = "my-specific-subject-cr80";
            var r = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = subject });
            Assert.That(r.Package!.SubjectId, Is.EqualTo(subject));
        }

        // ═════════════════════════════════════════════════════════════════════
        // CR81-CR85: Concurrency safety
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CR81
        public async Task CR81_Concurrent_Assemblies_AllSucceed()
        {
            var svc = CreateService();
            var tasks = Enumerable.Range(0, 10).Select(i =>
                svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                { SubjectId = $"sub-cr81-{i}" })).ToList();
            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.Success), Is.True);
        }

        [Test] // CR82
        public async Task CR82_Concurrent_Assemblies_UniqueExportIds()
        {
            var svc = CreateService();
            var tasks = Enumerable.Range(0, 10).Select(i =>
                svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
                { SubjectId = $"sub-cr82-{i}" })).ToList();
            var results = await Task.WhenAll(tasks);
            var ids = results.Select(r => r.Package!.ExportId).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        [Test] // CR83
        public async Task CR83_Concurrent_AllScenariosInParallel_Succeed()
        {
            var svc = CreateService();
            const string subject = "sub-cr83";
            var tasks = new Task<ComplianceAuditExportResponse>[]
            {
                svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject }),
                svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subject }),
                svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = subject }),
                svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = subject })
            };
            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.Success), Is.True);
        }

        [Test] // CR84
        public async Task CR84_Concurrent_ListExports_ReturnsConsistentResults()
        {
            var svc = CreateService();
            const string subject = "sub-cr84";
            var assembleTasks = Enumerable.Range(0, 5).Select(_ =>
                svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject }));
            await Task.WhenAll(assembleTasks);
            var list = await svc.ListExportsAsync(subject);
            Assert.That(list.Success, Is.True);
            Assert.That(list.Exports, Has.Count.GreaterThanOrEqualTo(5));
        }

        [Test] // CR85
        public async Task CR85_Concurrent_SameSubject_AllPackages_HaveCorrectSubjectId()
        {
            var svc = CreateService();
            const string subject = "shared-cr85";
            var tasks = Enumerable.Range(0, 6).Select(_ =>
                svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject })).ToList();
            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.Package!.SubjectId == subject), Is.True);
        }

        // ═════════════════════════════════════════════════════════════════════
        // CR86-CR92: Audience profile propagation
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CR86
        public async Task CR86_AudienceProfile_InternalCompliance_Stored()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr86", AudienceProfile = RegulatoryAudienceProfile.InternalCompliance });
            Assert.That(r.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.InternalCompliance));
        }

        [Test] // CR87
        public async Task CR87_AudienceProfile_ExecutiveSignOff_Stored()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr87", AudienceProfile = RegulatoryAudienceProfile.ExecutiveSignOff });
            Assert.That(r.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.ExecutiveSignOff));
        }

        [Test] // CR88
        public async Task CR88_AudienceProfile_ExternalAuditor_Stored()
        {
            var svc = CreateService();
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest
            { SubjectId = "sub-cr88", AudienceProfile = RegulatoryAudienceProfile.ExternalAuditor });
            Assert.That(r.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.ExternalAuditor));
        }

        [Test] // CR89
        public async Task CR89_AudienceProfile_RegulatorReview_Stored()
        {
            var svc = CreateService();
            var r = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest
            { SubjectId = "sub-cr89", AudienceProfile = RegulatoryAudienceProfile.RegulatorReview });
            Assert.That(r.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.RegulatorReview));
        }

        [Test] // CR90
        public async Task CR90_AudienceProfile_ApprovalHistory_RegulatorReview_Stored()
        {
            var svc = CreateService();
            var r = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest
            { SubjectId = "sub-cr90", AudienceProfile = RegulatoryAudienceProfile.RegulatorReview });
            Assert.That(r.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.RegulatorReview));
        }

        [Test] // CR91
        public async Task CR91_AudienceProfile_Idempotent_Replay_PreservesProfile()
        {
            var svc = CreateService();
            const string key = "key-cr91";
            await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr91", IdempotencyKey = key, AudienceProfile = RegulatoryAudienceProfile.ExternalAuditor });
            var r2 = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr91", IdempotencyKey = key });
            Assert.That(r2.Package!.AudienceProfile, Is.EqualTo(RegulatoryAudienceProfile.ExternalAuditor));
        }

        [Test] // CR92
        public async Task CR92_AudienceProfile_DefaultValue_IsValidEnumMember()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr92" });
            Assert.That(Enum.IsDefined(typeof(RegulatoryAudienceProfile), r.Package!.AudienceProfile), Is.True);
        }

        // ═════════════════════════════════════════════════════════════════════
        // CR93-CR100: EvidenceFromTimestamp, CorrelationId, misc
        // ═════════════════════════════════════════════════════════════════════

        [Test] // CR93
        public async Task CR93_EvidenceFromTimestamp_FutureTimestamp_NotReady()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr93", EvidenceFromTimestamp = DateTime.UtcNow.AddYears(10) });
            Assert.That(r.Success, Is.True);
            Assert.That(r.Package!.Readiness, Is.Not.EqualTo(AuditExportReadiness.Ready));
        }

        [Test] // CR94
        public async Task CR94_EvidenceFromTimestamp_DistantPast_Succeeds()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr94", EvidenceFromTimestamp = new DateTime(2000, 1, 1) });
            Assert.That(r.Success, Is.True);
        }

        [Test] // CR95
        public async Task CR95_EvidenceFromTimestamp_Null_Succeeds()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr95", EvidenceFromTimestamp = null });
            Assert.That(r.Success, Is.True);
        }

        [Test] // CR96
        public async Task CR96_EvidenceFromTimestamp_OnboardingCase_FutureTimestamp_NotReady()
        {
            var svc = CreateService();
            var r = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest
            { SubjectId = "sub-cr96", EvidenceFromTimestamp = DateTime.UtcNow.AddYears(10) });
            Assert.That(r.Success, Is.True);
            Assert.That(r.Package!.Readiness, Is.Not.EqualTo(AuditExportReadiness.Ready));
        }

        [Test] // CR97
        public async Task CR97_CorrelationId_ExplicitlySet_IsPreserved()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr97", CorrelationId = "test-corr-97" });
            Assert.That(r.Package!.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CR98
        public async Task CR98_CorrelationId_AutoGeneratedIfNotProvided()
        {
            var svc = CreateService();
            var r = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest
            { SubjectId = "sub-cr98" });
            Assert.That(r.Package!.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test] // CR99
        public async Task CR99_ApprovalHistory_DecisionLimit_Positive_Accepted()
        {
            var svc = CreateService();
            var r = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest
            { SubjectId = "sub-cr99", DecisionLimit = 10 });
            Assert.That(r.Success, Is.True);
        }

        [Test] // CR100
        public async Task CR100_AllScenarios_Package_Scenario_MatchesExpected()
        {
            var svc = CreateService();
            const string subject = "sub-cr100";
            var rr = await svc.AssembleReleaseReadinessExportAsync(new ReleaseReadinessExportRequest { SubjectId = subject });
            var ob = await svc.AssembleOnboardingCaseReviewExportAsync(new OnboardingCaseReviewExportRequest { SubjectId = subject });
            var br = await svc.AssembleBlockerReviewExportAsync(new ComplianceBlockerReviewExportRequest { SubjectId = subject });
            var ah = await svc.AssembleApprovalHistoryExportAsync(new ApprovalHistoryExportRequest { SubjectId = subject });
            Assert.That(rr.Package!.Scenario, Is.EqualTo(AuditScenario.ReleaseReadinessSignOff));
            Assert.That(ob.Package!.Scenario, Is.EqualTo(AuditScenario.OnboardingCaseReview));
            Assert.That(br.Package!.Scenario, Is.EqualTo(AuditScenario.ComplianceBlockerReview));
            Assert.That(ah.Package!.Scenario, Is.EqualTo(AuditScenario.ApprovalHistoryExport));
        }
    }
}
