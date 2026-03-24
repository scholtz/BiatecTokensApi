using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Advanced test coverage for ProtectedSignOffEvidencePersistenceService:
    ///   - Multi-tenant case isolation
    ///   - Webhook outcome sequences (approved→escalated, denied→approved, etc.)
    ///   - Evidence history ordering and filtering
    ///   - Head ref isolation across multiple cases
    ///   - Actor and correlation ID propagation
    ///   - Idempotency and determinism across repeated calls
    ///   - Boundary freshness window scenarios
    ///   - Schema contract completeness on all response types
    ///   - Service state after many operations
    ///   - History pagination via MaxRecords
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffEvidencePersistenceAdvancedTests
    {
        // ─── Fixed-time provider ─────────────────────────────────────────────
        private sealed class FixedTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FixedTimeProvider(DateTimeOffset now) => _now = now;
            public void Advance(TimeSpan delta) => _now = _now.Add(delta);
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ─── Service factory helpers ─────────────────────────────────────────
        private static ProtectedSignOffEvidencePersistenceService CreateService(
            TimeProvider? timeProvider = null)
            => new ProtectedSignOffEvidencePersistenceService(
                NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance,
                webhookService: null,
                timeProvider);

        // ════════════════════════════════════════════════════════════════════
        // Multi-Case Isolation Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task MultiCase_TwoCaseIdsDoNotShareWebhookHistory()
        {
            var svc = CreateService();
            const string head = "sha-iso-1";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-A", HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-B", HeadRef = head, Outcome = ApprovalWebhookOutcome.Denied }, "actor");

            var histA = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = "case-A" });
            var histB = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = "case-B" });

            Assert.That(histA.Records, Has.Count.EqualTo(1));
            Assert.That(histA.Records[0].Outcome, Is.EqualTo(ApprovalWebhookOutcome.Approved));
            Assert.That(histB.Records, Has.Count.EqualTo(1));
            Assert.That(histB.Records[0].Outcome, Is.EqualTo(ApprovalWebhookOutcome.Denied));
        }

        [Test]
        public async Task MultiCase_ReadinessIsIsolatedPerCaseId()
        {
            var svc = CreateService();
            const string headRef = "sha-multi-case-readiness";
            const string caseA = "case-ready-1";
            const string caseB = "case-blocked-1";

            // Make case A ready: webhook + evidence
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseA, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseA }, "actor");

            // Case B has no evidence
            var readyResult = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseA });
            var blockedResult = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseB });

            Assert.That(readyResult.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready));
            Assert.That(blockedResult.Status, Is.AnyOf(SignOffReleaseReadinessStatus.Blocked, SignOffReleaseReadinessStatus.BlockedMissingEvidence, SignOffReleaseReadinessStatus.BlockedMissingConfiguration, SignOffReleaseReadinessStatus.NotReleaseEvidence));
        }

        [Test]
        public async Task MultiCase_FiveCasesAllIndependent()
        {
            var svc = CreateService();

            for (int i = 1; i <= 5; i++)
            {
                var caseId = $"case-multi-{i}";
                var headRef = $"sha-multi-{i}";
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
                await svc.PersistSignOffEvidenceAsync(
                    new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor");
            }

            for (int i = 1; i <= 5; i++)
            {
                var result = await svc.GetReleaseReadinessAsync(
                    new GetSignOffReleaseReadinessRequest { HeadRef = $"sha-multi-{i}", CaseId = $"case-multi-{i}" });
                Assert.That(result.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                    $"Case {i} should be Ready");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Webhook Outcome Sequence Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WebhookSequence_ApprovedThenEscalated_MostRecentReturned()
        {
            var svc = CreateService();
            const string caseId = "case-seq-1";
            const string head = "sha-seq-1";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Escalated }, "actor");

            var hist = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });

            Assert.That(hist.Records, Has.Count.EqualTo(2));
            // newest first
            Assert.That(hist.Records[0].Outcome, Is.EqualTo(ApprovalWebhookOutcome.Escalated));
            Assert.That(hist.Records[1].Outcome, Is.EqualTo(ApprovalWebhookOutcome.Approved));
        }

        [Test]
        public async Task WebhookSequence_DeniedThenApproved_ServiceConsidersApproved()
        {
            var svc = CreateService();
            const string caseId = "case-seq-2";
            const string head = "sha-seq-2";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Denied }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(result.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "After Denied followed by Approved, status should be Ready (most recent approved).");
        }

        [Test]
        public async Task WebhookSequence_MultipleTimedOut_AllPersistedInHistory()
        {
            var svc = CreateService();
            const string caseId = "case-timeout-seq";
            const string head = "sha-timeout-seq";

            for (int i = 0; i < 3; i++)
            {
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest
                    {
                        CaseId = caseId,
                        HeadRef = head,
                        Outcome = ApprovalWebhookOutcome.TimedOut,
                        Reason = $"Timeout attempt {i + 1}"
                    }, "actor");
            }

            var hist = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });

            Assert.That(hist.Records, Has.Count.EqualTo(3), "All timed-out webhooks must be persisted.");
        }

        [Test]
        public async Task WebhookSequence_AllOutcomeTypes_AllPersistedCorrectly()
        {
            var svc = CreateService();
            var outcomes = Enum.GetValues<ApprovalWebhookOutcome>();
            const string head = "sha-all-outcomes";
            var caseId = $"case-all-outcomes-{Guid.NewGuid():N}";

            foreach (var outcome in outcomes)
            {
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest
                    {
                        CaseId = caseId,
                        HeadRef = head,
                        Outcome = outcome,
                        Reason = $"Test {outcome}"
                    }, "actor");
            }

            var hist = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });

            Assert.That(hist.Records, Has.Count.EqualTo(outcomes.Length),
                "One webhook record per outcome value must be persisted.");

            var recordedOutcomes = hist.Records.Select(r => r.Outcome).ToHashSet();
            foreach (var outcome in outcomes)
            {
                Assert.That(recordedOutcomes.Contains(outcome), Is.True,
                    $"Outcome {outcome} must be present in history.");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // History Ordering and Filtering Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WebhookHistory_MaxRecords_LimitsToRequestedCount()
        {
            var svc = CreateService();
            const string caseId = "case-max-records";
            const string head = "sha-max-records";

            for (int i = 0; i < 10; i++)
            {
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            }

            var histLimited = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId, MaxRecords = 3 });

            Assert.That(histLimited.Records, Has.Count.EqualTo(3), "MaxRecords=3 must return exactly 3 records.");
        }

        [Test]
        public async Task WebhookHistory_FilterByHeadRef_OnlyMatchingRecordsReturned()
        {
            var svc = CreateService();
            const string caseId = "case-headref-filter";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = "sha-head-A", Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = "sha-head-B", Outcome = ApprovalWebhookOutcome.Denied }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = "sha-head-A", Outcome = ApprovalWebhookOutcome.Escalated }, "actor");

            var histA = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId, HeadRef = "sha-head-A" });

            Assert.That(histA.Records, Has.Count.EqualTo(2));
            Assert.That(histA.Records.All(r => r.HeadRef == "sha-head-A"), Is.True);
        }

        [Test]
        public async Task EvidenceHistory_MaxRecords_LimitsToRequestedCount()
        {
            var svc = CreateService();
            const string head = "sha-evhist-max";
            const string caseId = "case-evhist-max";

            for (int i = 0; i < 8; i++)
            {
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
                await svc.PersistSignOffEvidenceAsync(
                    new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");
            }

            var histLimited = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = head, MaxRecords = 4 });

            Assert.That(histLimited.Packs, Has.Count.EqualTo(4));
        }

        [Test]
        public async Task EvidenceHistory_FilterByHeadRef_OnlyMatchingPacksReturned()
        {
            var svc = CreateService();
            const string caseId = "case-evhist-filter";

            for (int i = 1; i <= 3; i++)
            {
                var hr = $"sha-ev-{i}";
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = hr, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
                await svc.PersistSignOffEvidenceAsync(
                    new PersistSignOffEvidenceRequest { HeadRef = hr, CaseId = caseId }, "actor");
            }

            var hist = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = "sha-ev-2" });

            Assert.That(hist.Packs, Has.Count.EqualTo(1));
            Assert.That(hist.Packs[0].HeadRef, Is.EqualTo("sha-ev-2"));
        }

        [Test]
        public async Task EvidenceHistory_NoFilter_ReturnsAllPacksNewestFirst()
        {
            var svc = CreateService();
            const string caseId = "case-evhist-order";

            for (int i = 1; i <= 5; i++)
            {
                var hr = $"sha-order-{i}";
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = hr, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
                await svc.PersistSignOffEvidenceAsync(
                    new PersistSignOffEvidenceRequest { HeadRef = hr, CaseId = caseId }, "actor");
            }

            var hist = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { CaseId = caseId });

            Assert.That(hist.Packs, Has.Count.EqualTo(5));
            // Validate times are decreasing (newest first)
            for (int i = 0; i < hist.Packs.Count - 1; i++)
            {
                Assert.That(hist.Packs[i].CreatedAt, Is.GreaterThanOrEqualTo(hist.Packs[i + 1].CreatedAt),
                    "Packs must be returned newest first.");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Actor and Correlation ID Propagation Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task RecordWebhook_ActorIdPropagatedToRecord()
        {
            var svc = CreateService();
            const string actor = "operator-alice-001";

            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = "case-actor-1",
                    HeadRef = "sha-actor-1",
                    Outcome = ApprovalWebhookOutcome.Approved
                }, actor);

            Assert.That(resp.Record, Is.Not.Null);
            Assert.That(resp.Record!.ActorId, Is.EqualTo(actor));
        }

        [Test]
        public async Task RecordWebhook_CorrelationIdFromRequest_PreservedInRecord()
        {
            var svc = CreateService();
            var correlationId = Guid.NewGuid().ToString("N");

            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = "case-corr-1",
                    HeadRef = "sha-corr-1",
                    Outcome = ApprovalWebhookOutcome.Approved,
                    CorrelationId = correlationId
                }, "actor");

            Assert.That(resp.Record!.CorrelationId, Is.EqualTo(correlationId));
        }

        [Test]
        public async Task RecordWebhook_NoCorrelationId_AutoGeneratedInRecord()
        {
            var svc = CreateService();

            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = "case-corr-2",
                    HeadRef = "sha-corr-2",
                    Outcome = ApprovalWebhookOutcome.Approved,
                    CorrelationId = null
                }, "actor");

            Assert.That(resp.Record!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "A correlation ID must be auto-generated when not provided.");
        }

        [Test]
        public async Task PersistEvidence_ActorIdPropagatedToPack()
        {
            var svc = CreateService();
            const string actor = "operator-bob-002";
            const string head = "sha-actor-pack-1";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-ap-1", HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");

            var resp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "case-ap-1" }, actor);

            Assert.That(resp.Pack, Is.Not.Null);
            Assert.That(resp.Pack!.CreatedBy, Is.EqualTo(actor));
        }

        [Test]
        public async Task PersistEvidence_CorrelationIdFromWebhook_PreservedInPack()
        {
            var svc = CreateService();
            const string head = "sha-corr-pack-1";
            var correlationId = Guid.NewGuid().ToString("N");

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-corr-pack-1", HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved, CorrelationId = correlationId }, "actor");

            var resp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = head,
                    CaseId = "case-corr-pack-1"
                }, "actor");

            Assert.That(resp.Pack!.ApprovalWebhook!.CorrelationId, Is.EqualTo(correlationId));
        }

        // ════════════════════════════════════════════════════════════════════
        // Schema Contract Completeness Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task RecordWebhook_SuccessResponse_AllRequiredFieldsPresent()
        {
            var svc = CreateService();

            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = "case-schema-1",
                    HeadRef = "sha-schema-1",
                    Outcome = ApprovalWebhookOutcome.Approved
                }, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Record, Is.Not.Null, "Record must not be null on success.");
            Assert.That(resp.Record!.RecordId, Is.Not.Null.And.Not.Empty, "RecordId must be set.");
            Assert.That(resp.Record.CaseId, Is.Not.Null.And.Not.Empty, "CaseId must be set.");
            Assert.That(resp.Record.HeadRef, Is.Not.Null.And.Not.Empty, "HeadRef must be set.");
            Assert.That(resp.Record.ReceivedAt, Is.Not.EqualTo(default(DateTimeOffset)), "RecordedAt must be set.");
            Assert.That(resp.Record.CorrelationId, Is.Not.Null.And.Not.Empty, "CorrelationId must be set.");
        }

        [Test]
        public async Task PersistEvidence_SuccessResponse_AllRequiredFieldsPresent()
        {
            var svc = CreateService();
            const string head = "sha-schema-pack-1";
            const string caseId = "case-schema-pack-1";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");

            var resp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Pack, Is.Not.Null, "Pack must not be null on success.");
            Assert.That(resp.Pack!.PackId, Is.Not.Null.And.Not.Empty, "PackId must be set.");
            Assert.That(resp.Pack.HeadRef, Is.Not.Null.And.Not.Empty, "HeadRef must be set.");
            Assert.That(resp.Pack.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)), "PersistedAt must be set.");
            Assert.That(resp.Pack.ContentHash, Is.Not.Null.And.Not.Empty, "ContentHash must be set.");
            Assert.That(resp.Pack!.FreshnessStatus, Is.EqualTo(SignOffEvidenceFreshnessStatus.Complete),
                "FreshnessStatus must be set.");
        }

        [Test]
        public async Task GetReleaseReadiness_SuccessResponse_AllRequiredFieldsPresent()
        {
            var svc = CreateService();
            const string head = "sha-schema-ready-1";
            const string caseId = "case-schema-ready-1";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");

            var resp = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(resp.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready), "Status must be set.");
            Assert.That(resp.EvaluatedAt, Is.Not.EqualTo(default(DateTimeOffset)), "EvaluatedAt must be set.");
            Assert.That(resp.Blockers, Is.Not.Null, "Blockers list must not be null.");
            Assert.That(resp.OperatorGuidance, Is.Not.Null.And.Not.Empty, "Guidance must be set.");
        }

        [Test]
        public async Task GetReleaseReadiness_BlockedResponse_BlockersHaveRequiredFields()
        {
            var svc = CreateService();

            var resp = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = "sha-blocked-schema", CaseId = "case-blocked-schema" });

            Assert.That(resp.Status, Is.AnyOf(SignOffReleaseReadinessStatus.Blocked, SignOffReleaseReadinessStatus.BlockedMissingEvidence, SignOffReleaseReadinessStatus.BlockedMissingConfiguration, SignOffReleaseReadinessStatus.NotReleaseEvidence));
            Assert.That(resp.Blockers, Is.Not.Empty, "Blocked status must have at least one blocker.");

            foreach (var blocker in resp.Blockers)
            {
                Assert.That(blocker.Code, Is.Not.Null.And.Not.Empty, "Blocker.Code must be set.");
                Assert.That(blocker.Category, Is.Not.EqualTo(SignOffReleaseBlockerCategory.Unspecified),
                    "Blocker.Category must not be Unspecified.");
                Assert.That(blocker.RemediationHint, Is.Not.Null.And.Not.Empty, "Blocker.RemediationHint must be set.");
            }
        }

        [Test]
        public async Task WebhookHistory_RecordFields_AllPresent()
        {
            var svc = CreateService();
            const string caseId = "case-record-schema";
            const string head = "sha-record-schema";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = head,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    Reason = "All checks passed"
                }, "actor");

            var hist = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });

            var record = hist.Records[0];
            Assert.That(record.RecordId, Is.Not.Null.And.Not.Empty);
            Assert.That(record.CaseId, Is.EqualTo(caseId));
            Assert.That(record.HeadRef, Is.EqualTo(head));
            Assert.That(record.Outcome, Is.EqualTo(ApprovalWebhookOutcome.Approved));
            Assert.That(record.ReceivedAt, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(record.Reason, Is.EqualTo("All checks passed"));
        }

        // ════════════════════════════════════════════════════════════════════
        // Freshness and Time-Based Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task FreshnessWindow_JustBeforeExpiry_IsStillReady()
        {
            var now = DateTimeOffset.UtcNow;
            var tp = new FixedTimeProvider(now);
            var svc = CreateService(tp);
            const string head = "sha-fresh-boundary";
            const string caseId = "case-fresh-boundary";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");

            // Advance to just before expiry (freshness window is typically 24h)
            tp.Advance(TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59)));

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(result.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "Evidence just before freshness expiry should still be Ready.");
        }

        [Test]
        public async Task FreshnessWindow_AfterExpiry_IsStale()
        {
            var now = DateTimeOffset.UtcNow;
            var tp = new FixedTimeProvider(now);
            var svc = CreateService(tp);
            const string head = "sha-stale-boundary";
            const string caseId = "case-stale-boundary";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");

            // Advance past 24h freshness window
            tp.Advance(TimeSpan.FromHours(25));

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(result.Status, Is.EqualTo(SignOffReleaseReadinessStatus.DegradedStaleEvidence),
                "Evidence older than freshness window should be Stale.");
        }

        [Test]
        public async Task FreshnessWindow_ExpiredEvidence_NewEvidenceRestoresReady()
        {
            var now = DateTimeOffset.UtcNow;
            var tp = new FixedTimeProvider(now);
            var svc = CreateService(tp);
            const string head = "sha-refresh";
            const string caseId = "case-refresh";

            // First evidence + webhook
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");

            // Advance time past expiry
            tp.Advance(TimeSpan.FromHours(25));

            var staleResult = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            Assert.That(staleResult.Status, Is.EqualTo(SignOffReleaseReadinessStatus.DegradedStaleEvidence));

            // Refresh evidence at new time
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");

            var readyResult = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(readyResult.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "New evidence after stale should restore Ready status.");
        }

        // ════════════════════════════════════════════════════════════════════
        // Head Ref Isolation Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task HeadRef_DifferentShas_CompletelyIsolated()
        {
            var svc = CreateService();
            const string caseId = "case-sha-iso";

            // Set up head-A as ready, head-B as not
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = "sha-A", Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = "sha-A", CaseId = caseId }, "actor");

            var resultA = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = "sha-A", CaseId = caseId });
            var resultB = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = "sha-B", CaseId = caseId });

            Assert.That(resultA.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready));
            Assert.That(resultB.Status, Is.AnyOf(SignOffReleaseReadinessStatus.Blocked, SignOffReleaseReadinessStatus.BlockedMissingEvidence, SignOffReleaseReadinessStatus.BlockedMissingConfiguration, SignOffReleaseReadinessStatus.NotReleaseEvidence),
                "sha-B has no evidence so must be blocked.");
        }

        [Test]
        public async Task EvidencePersisted_ForOldHead_DoesNotAffectNewHead()
        {
            var svc = CreateService();
            const string caseId = "case-head-iso";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = "sha-old", Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = "sha-old", CaseId = caseId }, "actor");

            // Query for new head
            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = "sha-new", CaseId = caseId });

            Assert.That(result.Status, Is.AnyOf(SignOffReleaseReadinessStatus.Blocked, SignOffReleaseReadinessStatus.BlockedMissingEvidence, SignOffReleaseReadinessStatus.BlockedMissingConfiguration, SignOffReleaseReadinessStatus.NotReleaseEvidence),
                "Evidence for old head must not satisfy new head query.");
        }

        // ════════════════════════════════════════════════════════════════════
        // Idempotency and Determinism Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task RecordWebhook_SameInputTwice_BothPersistedSeparately()
        {
            var svc = CreateService();
            const string caseId = "case-idem-1";
            const string head = "sha-idem-1";

            var req = new RecordApprovalWebhookRequest
            {
                CaseId = caseId,
                HeadRef = head,
                Outcome = ApprovalWebhookOutcome.Approved,
                Reason = "Test"
            };

            var r1 = await svc.RecordApprovalWebhookAsync(req, "actor");
            var r2 = await svc.RecordApprovalWebhookAsync(req, "actor");

            Assert.That(r1.Record!.RecordId, Is.Not.EqualTo(r2.Record!.RecordId),
                "Each webhook recording must get a unique record ID.");

            var hist = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });
            Assert.That(hist.Records, Has.Count.EqualTo(2), "Both recordings must be persisted.");
        }

        [Test]
        public async Task PersistEvidence_CalledTwice_BothPacksInHistory()
        {
            var svc = CreateService();
            const string head = "sha-idem-pack-1";
            const string caseId = "case-idem-pack-1";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");

            var hist = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = head });

            Assert.That(hist.Packs, Has.Count.EqualTo(2),
                "Each evidence persistence call must produce a separate pack record.");
        }

        [Test]
        public async Task GetReleaseReadiness_CalledThreeTimes_DeterministicResult()
        {
            var svc = CreateService();
            const string head = "sha-deterministic";
            const string caseId = "case-deterministic";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");

            var r1 = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            var r2 = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });
            var r3 = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(r1.Status, Is.EqualTo(r2.Status).And.EqualTo(r3.Status),
                "GetReleaseReadiness must be deterministic across repeated calls.");
            Assert.That(r1.HasApprovalWebhook, Is.EqualTo(r2.HasApprovalWebhook).And.EqualTo(r3.HasApprovalWebhook));
        }

        // ════════════════════════════════════════════════════════════════════
        // Readiness State Machine Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ReadinessState_DeliveryErrorWebhook_EvidencePresent_IsBlocked()
        {
            var svc = CreateService();
            const string head = "sha-delivery-err";
            const string caseId = "case-delivery-err";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.DeliveryError }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            // DeliveryError does not constitute a valid approval
            Assert.That(result.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "DeliveryError webhook must not count as a valid approval.");
        }

        [Test]
        public async Task ReadinessState_EscalatedWebhook_EvidencePresent_IsNotReady()
        {
            var svc = CreateService();
            const string head = "sha-escalated-check";
            const string caseId = "case-escalated-check";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Escalated }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            // Escalated is not the same as Approved — release should not be ready
            Assert.That(result.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "Escalated webhook alone must not mark release as Ready.");
        }

        [Test]
        public async Task ReadinessState_NoEvidenceEverPersisted_HasApprovalWebhookFalse()
        {
            var svc = CreateService();

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest
                {
                    HeadRef = "sha-no-evidence-ever",
                    CaseId = "case-no-evidence-ever"
                });

            Assert.That(result.HasApprovalWebhook, Is.False);
        }

        [Test]
        public async Task ReadinessState_OnlyWebhookNoEvidence_HasApprovalWebhookTrue()
        {
            var svc = CreateService();
            const string head = "sha-webhook-only";
            const string caseId = "case-webhook-only";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(result.HasApprovalWebhook, Is.True,
                "HasApprovalWebhook should be true when an approval webhook was received.");
            Assert.That(result.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "Without evidence, status must not be Ready even with approval webhook.");
        }

        // ════════════════════════════════════════════════════════════════════
        // Concurrency Safety Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Concurrency_ParallelWebhookRecording_AllPersisted()
        {
            var svc = CreateService();
            const string caseId = "case-concurrent-wh";
            const string head = "sha-concurrent-wh";
            const int count = 20;

            var tasks = Enumerable.Range(0, count).Select(i =>
                svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest
                    {
                        CaseId = caseId,
                        HeadRef = head,
                        Outcome = ApprovalWebhookOutcome.Approved,
                        Reason = $"Parallel webhook {i}"
                    }, $"actor-{i}"));

            var results = await Task.WhenAll(tasks);

            Assert.That(results.All(r => r.Success), Is.True, "All parallel recordings must succeed.");

            var hist = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });

            Assert.That(hist.Records, Has.Count.EqualTo(count),
                $"All {count} parallel webhook recordings must be persisted.");
        }

        [Test]
        public async Task Concurrency_ParallelEvidencePersistence_AllPersisted()
        {
            var svc = CreateService();
            const int count = 10;

            var tasks = Enumerable.Range(0, count).Select(async i =>
            {
                var head = $"sha-par-ev-{i}";
                var caseId = $"case-par-ev-{i}";
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
                return await svc.PersistSignOffEvidenceAsync(
                    new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "actor");
            });

            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.Success), Is.True, "All parallel evidence persistence calls must succeed.");
        }

        // ════════════════════════════════════════════════════════════════════
        // Validation and Error Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task RecordWebhook_WhitespaceOnlyCaseId_ReturnsError()
        {
            var svc = CreateService();

            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = "   ",
                    HeadRef = "sha-ws",
                    Outcome = ApprovalWebhookOutcome.Approved
                }, "actor");

            Assert.That(resp.Success, Is.False, "Whitespace-only CaseId must be rejected.");
        }

        [Test]
        public async Task RecordWebhook_WhitespaceOnlyHeadRef_ReturnsError()
        {
            var svc = CreateService();

            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = "case-ws-head",
                    HeadRef = "   ",
                    Outcome = ApprovalWebhookOutcome.Approved
                }, "actor");

            Assert.That(resp.Success, Is.False, "Whitespace-only HeadRef must be rejected.");
        }

        [Test]
        public async Task PersistEvidence_WhitespaceHeadRef_ReturnsError()
        {
            var svc = CreateService();

            var resp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = "\t ", CaseId = "case-ws-ev" }, "actor");

            Assert.That(resp.Success, Is.False, "Whitespace-only HeadRef in PersistEvidence must be rejected.");
        }

        [Test]
        public async Task GetReleaseReadiness_EmptyHeadRef_ReturnsIndeterminate()
        {
            var svc = CreateService();

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = string.Empty, CaseId = "case-empty-head" });

            // Empty head ref should produce non-Ready result (indeterminate or blocked)
            Assert.That(result.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.Ready));
        }

        // ════════════════════════════════════════════════════════════════════
        // Full E2E Workflow Tests
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public async Task E2E_FullLifecycle_NewCase_HappyPath()
        {
            var svc = CreateService();
            var caseId = $"case-e2e-{Guid.NewGuid():N}";
            var headRef = $"sha-e2e-{Guid.NewGuid():N}";

            // Step 1: Record approval webhook
            var webhookResp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = caseId,
                    HeadRef = headRef,
                    Outcome = ApprovalWebhookOutcome.Approved,
                    Reason = "All E2E checks passed"
                }, "release-manager");

            Assert.That(webhookResp.Success, Is.True, "E2E Step 1: RecordWebhook must succeed.");

            // Step 2: Persist evidence pack
            var evidenceResp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = headRef,
                    CaseId = caseId
                }, "release-manager");

            Assert.That(evidenceResp.Success, Is.True, "E2E Step 2: PersistEvidence must succeed.");

            // Step 3: Check readiness — must be Ready
            var readiness = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(readiness.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "E2E Step 3: Status must be Ready after webhook + evidence.");
            Assert.That(readiness.Blockers, Is.Empty, "E2E Step 3: No blockers for Ready status.");

            // Step 4: Verify history
            var wbHist = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });
            Assert.That(wbHist.Records, Has.Count.EqualTo(1), "E2E Step 4: One webhook in history.");

            var evHist = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = headRef });
            Assert.That(evHist.Packs, Has.Count.EqualTo(1), "E2E Step 4: One evidence pack in history.");
        }

        [Test]
        public async Task E2E_EscalationThenApproval_FinalStateReady()
        {
            var svc = CreateService();
            const string caseId = "case-escalation-e2e";
            const string head = "sha-escalation-e2e";

            // Escalation first
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Escalated }, "reviewer");

            // Then final approval
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "senior-reviewer");

            // Persist evidence
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "release-manager");

            // Check readiness
            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(result.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "After escalation followed by approval, final state must be Ready.");
        }

        [Test]
        public async Task E2E_DeniedThenRemediated_FinalStateReady()
        {
            var svc = CreateService();
            const string caseId = "case-remediated-e2e";
            const string head = "sha-remediated-e2e";

            // Initial denial
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Denied }, "reviewer");

            // After remediation, new approval
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = head, Outcome = ApprovalWebhookOutcome.Approved }, "reviewer");

            // Persist evidence
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = caseId }, "release-manager");

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = caseId });

            Assert.That(result.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "After remediation and re-approval, status must be Ready.");
        }

        [Test]
        public async Task E2E_LargeNumberOfOperations_ServiceRemainsConsistent()
        {
            var svc = CreateService();
            const string caseId = "case-large-e2e";
            var headRef = "sha-large-e2e";

            // Record 50 webhooks
            for (int i = 0; i < 50; i++)
            {
                var outcome = (i % 2 == 0) ? ApprovalWebhookOutcome.Approved : ApprovalWebhookOutcome.Escalated;
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = outcome }, "actor");
            }

            // Final approval
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, "actor");

            // Persist evidence
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor");

            // Service must still correctly evaluate readiness
            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(result.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "Service must remain consistent after large number of operations.");

            var hist = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId, MaxRecords = 100 });
            Assert.That(hist.Records, Has.Count.EqualTo(51), "All 51 webhooks must be in history.");
        }
    }
}
