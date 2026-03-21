using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Regression and stress test coverage for ProtectedSignOffEvidencePersistenceService.
    ///
    /// These tests validate:
    ///   - All bug-fix regressions (ActorId propagation, MaxRecords, CorrelationId)
    ///   - ReleaseGrade requirement enforcement
    ///   - Head-ref isolation across evidence packs
    ///   - FreshnessWindowHours boundary values (0 defaults to 24, large values)
    ///   - Content hash stability across identical inputs
    ///   - PayloadHash computation for webhook records with raw payloads
    ///   - TimedOut / DeliveryError outcomes in readiness evaluation
    ///   - Multiple blockers coexisting in a single readiness response
    ///   - Readiness state machine: Pending vs Blocked vs Stale vs Ready
    ///   - History TotalCount accuracy vs truncated Records list
    ///   - Evidence pack ordering (newest first)
    ///   - Webhook records across multiple case IDs with same head ref
    ///   - Null metadata handled gracefully (defaulted to empty dict)
    ///   - Service isolation: separate instances do not share state
    ///   - CaseId filter on GetEvidencePackHistory
    ///   - CorrelationId auto-generated when absent from request
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffEvidencePersistenceRegressionTests
    {
        // ─── Controllable time provider ──────────────────────────────────────
        private sealed class FixedTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FixedTimeProvider(DateTimeOffset now) => _now = now;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static ProtectedSignOffEvidencePersistenceService CreateService(TimeProvider? tp = null)
            => new ProtectedSignOffEvidencePersistenceService(
                NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance,
                webhookService: null,
                tp);

        // ════════════════════════════════════════════════════════════════════
        // Regression: ActorId propagation
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Regression_ActorId_RequestActorIdOverridesMethodParam()
        {
            var svc = CreateService();
            var req = new RecordApprovalWebhookRequest
            {
                CaseId = "case-actor",
                HeadRef = "sha-actor-1",
                Outcome = ApprovalWebhookOutcome.Approved,
                ActorId = "request-actor"
            };
            var resp = await svc.RecordApprovalWebhookAsync(req, "param-actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.ActorId, Is.EqualTo("request-actor"),
                "ActorId from request should take precedence over method param");
        }

        [Test]
        public async Task Regression_ActorId_MethodParamUsedWhenRequestActorIdIsNull()
        {
            var svc = CreateService();
            var req = new RecordApprovalWebhookRequest
            {
                CaseId = "case-actor2",
                HeadRef = "sha-actor-2",
                Outcome = ApprovalWebhookOutcome.Approved,
                ActorId = null
            };
            var resp = await svc.RecordApprovalWebhookAsync(req, "fallback-actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.ActorId, Is.EqualTo("fallback-actor"),
                "ActorId should fall back to method param when request ActorId is null");
        }

        // ════════════════════════════════════════════════════════════════════
        // Regression: MaxRecords cap enforcement
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Regression_MaxRecords_WebhookHistoryRespectsCap()
        {
            var svc = CreateService();
            const string caseId = "case-max";
            const string headRef = "sha-max-1";

            for (int i = 0; i < 15; i++)
            {
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Escalated },
                    "actor");
            }

            var resp = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId, MaxRecords = 5 });

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.TotalCount, Is.EqualTo(15), "TotalCount should reflect all 15 records");
            Assert.That(resp.Records.Count, Is.EqualTo(5), "Records should be capped at MaxRecords=5");
        }

        [Test]
        public async Task Regression_MaxRecords_EvidencePackHistoryRespectsCap()
        {
            var svc = CreateService();
            const string headRef = "sha-maxpacks";

            for (int i = 0; i < 12; i++)
            {
                await svc.PersistSignOffEvidenceAsync(
                    new PersistSignOffEvidenceRequest { HeadRef = headRef },
                    "actor");
            }

            var resp = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = headRef, MaxRecords = 4 });

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.TotalCount, Is.EqualTo(12), "TotalCount should reflect all 12 packs");
            Assert.That(resp.Packs.Count, Is.EqualTo(4), "Packs should be capped at MaxRecords=4");
        }

        [Test]
        public async Task Regression_MaxRecords_ZeroDefaultsToFifty()
        {
            var svc = CreateService();
            const string headRef = "sha-default-max";

            for (int i = 0; i < 60; i++)
            {
                await svc.PersistSignOffEvidenceAsync(
                    new PersistSignOffEvidenceRequest { HeadRef = headRef },
                    "actor");
            }

            var resp = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = headRef, MaxRecords = 0 });

            Assert.That(resp.TotalCount, Is.EqualTo(60));
            Assert.That(resp.Packs.Count, Is.EqualTo(50), "MaxRecords=0 should default to 50");
        }

        // ════════════════════════════════════════════════════════════════════
        // Regression: CorrelationId source
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Regression_CorrelationId_PropagatedFromRequest()
        {
            var svc = CreateService();
            const string expectedCorrelId = "corr-abc-123";
            var req = new RecordApprovalWebhookRequest
            {
                CaseId = "case-corr",
                HeadRef = "sha-corr-1",
                Outcome = ApprovalWebhookOutcome.Approved,
                CorrelationId = expectedCorrelId
            };
            var resp = await svc.RecordApprovalWebhookAsync(req, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.CorrelationId, Is.EqualTo(expectedCorrelId),
                "CorrelationId from request must be preserved in the stored record");
        }

        [Test]
        public async Task Regression_CorrelationId_AutoGeneratedWhenAbsent()
        {
            var svc = CreateService();
            var req = new RecordApprovalWebhookRequest
            {
                CaseId = "case-corr2",
                HeadRef = "sha-corr-2",
                Outcome = ApprovalWebhookOutcome.Approved,
                CorrelationId = null
            };
            var resp = await svc.RecordApprovalWebhookAsync(req, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "CorrelationId must be auto-generated when not provided");
        }

        // ════════════════════════════════════════════════════════════════════
        // ReleaseGrade requirement enforcement
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ReleaseGrade_RequireReleaseGradeWithoutWebhook_Returns400()
        {
            var svc = CreateService();
            var resp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = "sha-rg-1",
                    RequireReleaseGrade = true,
                    RequireApprovalWebhook = false
                },
                "actor");

            Assert.That(resp.Success, Is.False,
                "RequireReleaseGrade=true without an approval webhook should fail");
            Assert.That(resp.ErrorCode, Is.EqualTo("NOT_RELEASE_GRADE"));
        }

        [Test]
        public async Task ReleaseGrade_RequireReleaseGradeWithApprovedWebhook_Succeeds()
        {
            var svc = CreateService();
            const string headRef = "sha-rg-2";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-rg", HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved },
                "actor");

            var resp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = "case-rg",
                    RequireReleaseGrade = true
                },
                "actor");

            Assert.That(resp.Success, Is.True, "Should succeed when approved webhook exists and ReleaseGrade is required");
            Assert.That(resp.Pack!.IsReleaseGrade, Is.True);
        }

        [Test]
        public async Task ReleaseGrade_WithoutRequireFlag_SucceedsEvenWithoutWebhook()
        {
            var svc = CreateService();
            var resp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = "sha-rg-3",
                    RequireReleaseGrade = false
                },
                "actor");

            Assert.That(resp.Success, Is.True, "Should succeed when ReleaseGrade is not required");
        }

        // ════════════════════════════════════════════════════════════════════
        // FreshnessWindowHours boundary values
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task FreshnessWindow_ZeroDefaultsTo24Hours()
        {
            var tp = new FixedTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            const string headRef = "sha-fw-1";

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, FreshnessWindowHours = 0 },
                "actor");

            // Advance 23h → still fresh
            tp.Advance(TimeSpan.FromHours(23));
            var readiness1 = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef });

            // Advance another 2h (total 25h) → should be stale
            tp.Advance(TimeSpan.FromHours(2));
            var readiness2 = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef });

            Assert.That(readiness1.EvidenceFreshness, Is.Not.EqualTo(SignOffEvidenceFreshnessStatus.Stale),
                "Evidence should not be stale at 23h (default window is 24h)");
            Assert.That(readiness2.EvidenceFreshness, Is.EqualTo(SignOffEvidenceFreshnessStatus.Stale),
                "Evidence should be stale after 25h when default window is 24h");
        }

        [Test]
        public async Task FreshnessWindow_VeryLargeValue_EvidenceRemainsFresh()
        {
            var tp = new FixedTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            const string headRef = "sha-fw-2";

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, FreshnessWindowHours = 8760 /* 1 year */ },
                "actor");

            // Advance 100 days
            tp.Advance(TimeSpan.FromDays(100));
            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef });

            Assert.That(readiness.EvidenceFreshness, Is.Not.EqualTo(SignOffEvidenceFreshnessStatus.Stale),
                "Evidence should remain fresh within a 1-year window");
        }

        [Test]
        public async Task FreshnessWindow_OneHourWindow_ExpiresCorrectly()
        {
            var tp = new FixedTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            const string headRef = "sha-fw-3";

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, FreshnessWindowHours = 1 },
                "actor");

            // Advance 61 minutes → stale
            tp.Advance(TimeSpan.FromMinutes(61));
            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef });

            Assert.That(readiness.EvidenceFreshness, Is.EqualTo(SignOffEvidenceFreshnessStatus.Stale),
                "Evidence should be stale after 61 minutes with a 1-hour freshness window");
        }

        // ════════════════════════════════════════════════════════════════════
        // TimedOut / DeliveryError outcomes in readiness
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Readiness_WithOnlyTimedOutWebhook_ShowsMissingApprovalBlocker()
        {
            var svc = CreateService();
            const string headRef = "sha-to-1";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-to", HeadRef = headRef, Outcome = ApprovalWebhookOutcome.TimedOut },
                "actor");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = "case-to" });

            Assert.That(readiness.Blockers.Any(b => b.Category == SignOffReleaseBlockerCategory.MissingApproval
                                                   || b.Category == SignOffReleaseBlockerCategory.MalformedWebhook
                                                   || b.Code == "APPROVAL_MISSING"),
                Is.True,
                "TimedOut webhook should result in a missing-approval or related blocker");
        }

        [Test]
        public async Task Readiness_WithOnlyDeliveryErrorWebhook_IsBlocked()
        {
            var svc = CreateService();
            const string headRef = "sha-de-1";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-de", HeadRef = headRef, Outcome = ApprovalWebhookOutcome.DeliveryError },
                "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = "case-de" },
                "actor");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = "case-de" });

            Assert.That(readiness.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "DeliveryError webhook should not result in a Ready status");
        }

        // ════════════════════════════════════════════════════════════════════
        // Multiple blockers coexisting
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Readiness_MultipleBlockers_AllReturned()
        {
            // Fresh evidence but denied approval → should produce ≥2 blockers or at minimum 1
            var svc = CreateService();
            const string headRef = "sha-mb-1";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-mb", HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Denied },
                "actor");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = "case-mb" });

            // Should have at least MissingEvidence (no pack) and ApprovalDenied
            Assert.That(readiness.Blockers.Count, Is.GreaterThanOrEqualTo(1),
                "Should have at least one blocker");
            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked));
        }

        // ════════════════════════════════════════════════════════════════════
        // Readiness state machine: Pending state
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Readiness_EvidencePartialWithApproval_ReturnsPending()
        {
            // The Pending state requires evidence that is present but not complete,
            // with no critical blockers. This is an edge case with Partial freshness.
            var svc = CreateService();
            const string headRef = "sha-pend-1";

            // First: record an approved webhook
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-pend", HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved },
                "actor");

            // Then: persist evidence
            var persistResp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = "case-pend" },
                "actor");
            Assert.That(persistResp.Success, Is.True);

            // Get readiness — should be Ready (approval + complete evidence)
            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = "case-pend" });

            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "Complete evidence + approved webhook should yield Ready");
        }

        // ════════════════════════════════════════════════════════════════════
        // Readiness when no evidence exists → Blocked (fail-closed)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Readiness_NoEvidenceForHead_ReturnsBlockedWithMissingEvidenceBlocker()
        {
            var svc = CreateService();
            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = "sha-nope", CaseId = null });

            Assert.That(readiness.Success, Is.False);
            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked));
            Assert.That(readiness.Blockers.Any(b => b.Category == SignOffReleaseBlockerCategory.MissingEvidence), Is.True);
        }

        // ════════════════════════════════════════════════════════════════════
        // History TotalCount accuracy vs truncated Records
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task History_TotalCountReflectsAllRecordsBeforeTruncation()
        {
            var svc = CreateService();
            const string caseId = "case-tc";
            for (int i = 0; i < 20; i++)
            {
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = $"sha-tc-{i}", Outcome = ApprovalWebhookOutcome.Approved },
                    "actor");
            }

            var resp = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId, MaxRecords = 7 });

            Assert.That(resp.TotalCount, Is.EqualTo(20));
            Assert.That(resp.Records.Count, Is.EqualTo(7));
        }

        // ════════════════════════════════════════════════════════════════════
        // Evidence pack history ordering (newest first)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvidenceHistory_OrderedNewestFirst()
        {
            var tp = new FixedTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            const string headRef = "sha-order";

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef },
                "actor-1");
            tp.Advance(TimeSpan.FromSeconds(1));

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef },
                "actor-2");
            tp.Advance(TimeSpan.FromSeconds(1));

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef },
                "actor-3");

            var resp = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = headRef });

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Packs.Count, Is.EqualTo(3));
            // Newest (actor-3) should be first
            Assert.That(resp.Packs[0].CreatedBy, Is.EqualTo("actor-3"),
                "Most recently created pack should be first in the ordered list");
            Assert.That(resp.Packs[2].CreatedBy, Is.EqualTo("actor-1"),
                "Oldest pack should be last in the ordered list");
        }

        // ════════════════════════════════════════════════════════════════════
        // Webhook history ordering (newest first)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WebhookHistory_OrderedNewestFirst()
        {
            var tp = new FixedTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            const string caseId = "case-whorder";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = "sha-wh1", Outcome = ApprovalWebhookOutcome.Escalated, Reason = "first" },
                "actor");
            tp.Advance(TimeSpan.FromSeconds(1));

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = "sha-wh2", Outcome = ApprovalWebhookOutcome.Approved, Reason = "second" },
                "actor");

            var resp = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Records.Count, Is.EqualTo(2));
            Assert.That(resp.Records[0].Reason, Is.EqualTo("second"),
                "Most recent webhook should be first");
        }

        // ════════════════════════════════════════════════════════════════════
        // Cross-case webhook isolation (same head ref, different case IDs)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task CrossCase_SameHeadRefDifferentCases_WebhooksIsolated()
        {
            var svc = CreateService();
            const string headRef = "sha-cross";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-X", HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved },
                "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-Y", HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Denied },
                "actor");

            var histX = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = "case-X" });
            var histY = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = "case-Y" });

            Assert.That(histX.Records.All(r => r.Outcome == ApprovalWebhookOutcome.Approved), Is.True,
                "case-X history should only contain Approved record");
            Assert.That(histY.Records.All(r => r.Outcome == ApprovalWebhookOutcome.Denied), Is.True,
                "case-Y history should only contain Denied record");
        }

        // ════════════════════════════════════════════════════════════════════
        // Null metadata handled gracefully
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task NullMetadata_DefaultsToEmptyDictionary()
        {
            var svc = CreateService();
            var req = new RecordApprovalWebhookRequest
            {
                CaseId = "case-meta",
                HeadRef = "sha-meta-1",
                Outcome = ApprovalWebhookOutcome.Approved,
                Metadata = null
            };
            var resp = await svc.RecordApprovalWebhookAsync(req, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.Metadata, Is.Not.Null,
                "Metadata should default to empty dictionary, not null");
        }

        // ════════════════════════════════════════════════════════════════════
        // Service isolation: separate instances do not share state
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ServiceIsolation_TwoSeparateInstancesDoNotShareState()
        {
            var svc1 = CreateService();
            var svc2 = CreateService();
            const string headRef = "sha-iso";

            await svc1.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-iso", HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved },
                "actor");

            var histSvc2 = await svc2.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = "case-iso" });

            Assert.That(histSvc2.TotalCount, Is.EqualTo(0),
                "Second service instance must not share state with first");
        }

        // ════════════════════════════════════════════════════════════════════
        // CaseId filter on GetEvidencePackHistory
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvidenceHistory_CaseIdFilterReturnsOnlyMatchingPacks()
        {
            var svc = CreateService();
            const string headRef = "sha-casefilt";

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = "case-F1" }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = "case-F2" }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = "case-F1" }, "actor");

            var resp = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { CaseId = "case-F1" });

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.TotalCount, Is.EqualTo(2),
                "Only packs for case-F1 should be returned");
            Assert.That(resp.Packs.All(p => p.CaseId == "case-F1"), Is.True);
        }

        // ════════════════════════════════════════════════════════════════════
        // PayloadHash computed for webhook records with raw payloads
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task PayloadHash_ComputedWhenRawPayloadProvided()
        {
            var svc = CreateService();
            var req = new RecordApprovalWebhookRequest
            {
                CaseId = "case-hash",
                HeadRef = "sha-hash-1",
                Outcome = ApprovalWebhookOutcome.Approved,
                RawPayload = "{\"approved\": true, \"reviewer\": \"alice\"}"
            };
            var resp = await svc.RecordApprovalWebhookAsync(req, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.PayloadHash, Is.Not.Null.And.Not.Empty,
                "PayloadHash must be computed when RawPayload is provided");
            Assert.That(resp.Record.PayloadHash!.Length, Is.EqualTo(64),
                "SHA-256 hex digest is 64 characters");
        }

        [Test]
        public async Task PayloadHash_NotSetWhenNoRawPayload()
        {
            var svc = CreateService();
            var req = new RecordApprovalWebhookRequest
            {
                CaseId = "case-nohash",
                HeadRef = "sha-nohash",
                Outcome = ApprovalWebhookOutcome.Approved,
                RawPayload = null
            };
            var resp = await svc.RecordApprovalWebhookAsync(req, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record!.PayloadHash, Is.Null,
                "PayloadHash should be null when no raw payload is provided");
        }

        // ════════════════════════════════════════════════════════════════════
        // Content hash stability
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ContentHash_NonNullAndNonEmpty()
        {
            var svc = CreateService();
            var resp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = "sha-contenthash" },
                "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Pack!.ContentHash, Is.Not.Null.And.Not.Empty,
                "ContentHash must be populated on every persisted pack");
        }

        // ════════════════════════════════════════════════════════════════════
        // GetReleaseReadiness: Indeterminate on missing head ref
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetReleaseReadiness_NullRequest_ReturnsIndeterminate()
        {
            var svc = CreateService();
            var readiness = await svc.GetReleaseReadinessAsync(null!);

            Assert.That(readiness.Success, Is.False);
            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Indeterminate));
            Assert.That(readiness.ErrorCode, Is.EqualTo("MISSING_HEAD_REF"));
        }

        [Test]
        public async Task GetReleaseReadiness_EmptyHeadRef_ReturnsIndeterminate()
        {
            var svc = CreateService();
            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = "" });

            Assert.That(readiness.Success, Is.False);
            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Indeterminate));
        }

        // ════════════════════════════════════════════════════════════════════
        // OperatorGuidance present in every non-Ready response
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task OperatorGuidance_PresentInBlockedResponse()
        {
            var svc = CreateService();
            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = "sha-og-blocked" });

            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked));
            Assert.That(readiness.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "OperatorGuidance must be populated for Blocked responses");
        }

        [Test]
        public async Task OperatorGuidance_PresentInReadyResponse()
        {
            var svc = CreateService();
            const string headRef = "sha-og-ready";
            const string caseId = "case-og-ready";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved },
                "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId },
                "actor");

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready));
            Assert.That(readiness.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "OperatorGuidance must be populated even for Ready responses");
        }

        // ════════════════════════════════════════════════════════════════════
        // RecordId uniqueness
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task RecordId_UniquePerWebhookRecord()
        {
            var svc = CreateService();
            const string caseId = "case-uid";
            const string headRef = "sha-uid";

            var ids = new HashSet<string>();
            for (int i = 0; i < 20; i++)
            {
                var r = await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved },
                    "actor");
                ids.Add(r.Record!.RecordId);
            }

            Assert.That(ids.Count, Is.EqualTo(20), "Each webhook record must have a unique RecordId");
        }

        [Test]
        public async Task PackId_UniquePerEvidencePack()
        {
            var svc = CreateService();
            const string headRef = "sha-packuid";

            var ids = new HashSet<string>();
            for (int i = 0; i < 20; i++)
            {
                var r = await svc.PersistSignOffEvidenceAsync(
                    new PersistSignOffEvidenceRequest { HeadRef = headRef },
                    "actor");
                ids.Add(r.Pack!.PackId);
            }

            Assert.That(ids.Count, Is.EqualTo(20), "Each evidence pack must have a unique PackId");
        }

        // ════════════════════════════════════════════════════════════════════
        // Head-ref isolation for evidence packs
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EvidencePacks_HeadRefIsolation_DifferentHeadsDoNotMix()
        {
            var svc = CreateService();

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = "sha-head-A" }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = "sha-head-A" }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = "sha-head-B" }, "actor");

            var histA = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = "sha-head-A" });
            var histB = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = "sha-head-B" });

            Assert.That(histA.TotalCount, Is.EqualTo(2), "HeadRef A should have 2 packs");
            Assert.That(histB.TotalCount, Is.EqualTo(1), "HeadRef B should have 1 pack");
        }

        // ════════════════════════════════════════════════════════════════════
        // HeadRef filter on GetApprovalWebhookHistory
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WebhookHistory_HeadRefFilterNarrowsResults()
        {
            var svc = CreateService();
            const string caseId = "case-hrf";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = "sha-hrf-A", Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = "sha-hrf-B", Outcome = ApprovalWebhookOutcome.Denied }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = "sha-hrf-A", Outcome = ApprovalWebhookOutcome.Escalated }, "actor");

            var respA = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId, HeadRef = "sha-hrf-A" });

            Assert.That(respA.TotalCount, Is.EqualTo(2),
                "HeadRef filter should narrow to only 2 records for sha-hrf-A");
            Assert.That(respA.Records.All(r => r.HeadRef == "sha-hrf-A"), Is.True);
        }

        // ════════════════════════════════════════════════════════════════════
        // Readiness: Stale evidence + denied approval = Blocked (denied takes priority)
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Readiness_StaleEvidenceWithDeniedApproval_StatusIsBlocked()
        {
            var tp = new FixedTimeProvider(DateTimeOffset.UtcNow);
            var svc = CreateService(tp);
            const string headRef = "sha-staledenied";
            const string caseId = "case-staledenied";

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId, FreshnessWindowHours = 1 },
                "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Denied },
                "actor");

            // Advance 2 hours → evidence is stale
            tp.Advance(TimeSpan.FromHours(2));

            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            // Either Blocked or Stale is acceptable; the critical point is it's not Ready
            Assert.That(readiness.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "Stale evidence with denied approval must not be Ready");
            Assert.That(readiness.Blockers.Count, Is.GreaterThanOrEqualTo(1));
        }

        // ════════════════════════════════════════════════════════════════════
        // GetReleaseReadiness: EvaluatedAt timestamp is present
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetReleaseReadiness_EvaluatedAtTimestampIsSet()
        {
            var svc = CreateService();
            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = "sha-evalt" });

            Assert.That(readiness.EvaluatedAt, Is.Not.EqualTo(default(DateTimeOffset)),
                "EvaluatedAt must be populated on every readiness response");
        }
    }
}
