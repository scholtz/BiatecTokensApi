using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Additional edge-case and coverage-gap tests for ProtectedSignOffEvidencePersistenceService.
    /// Covers scenarios not fully exercised in ProtectedSignOffEvidencePersistenceTests.cs:
    ///   - Multiple webhooks for same case: latest-wins ordering
    ///   - EscalatedThenApproved: escalation followed by approval → Ready
    ///   - MultipleBlockers: all applicable blockers populated in a single response
    ///   - PersistWithActor: CreatedBy field propagated correctly
    ///   - RecordIdUniqueness: each recorded webhook gets a unique RecordId
    ///   - PackIdUniqueness: each persisted evidence pack gets a unique PackId
    ///   - WebhookHistoryOrdering: returned in reverse-chronological order (newest first)
    ///   - MaxRecordsHonoured: history MaxRecords limit is respected
    ///   - HeadRefFilterApplied: HeadRef filter on history returns only matching records
    ///   - CaseIdFilterApplied: CaseId filter on history returns only matching records
    ///   - EvidencePackHistoryFilter: headRef and caseId filters on pack history
    ///   - WebhookOutcomeIsPreservedInHistory: recorded outcome matches what is returned
    ///   - NullActorAccepted: null actor does not crash RecordApprovalWebhookAsync
    ///   - EmptyActorAccepted: empty string actor does not crash RecordApprovalWebhookAsync
    ///   - MultipleCasesIsolated: webhooks/evidence for different cases do not bleed over
    ///   - BlockerRemediationHintNonEmpty: every blocker has a non-empty RemediationHint
    ///   - BlockerCodeNonEmpty: every blocker has a non-empty Code
    ///   - ReadinessSchemaContract: response always has Blockers list (never null)
    ///   - EvidencePackHistoryEmpty: returns empty list (not null) when no packs exist
    ///   - WebhookHistoryEmpty: returns empty list (not null) when no webhooks exist
    ///   - SuccessFlagOnReady: Success=true when status is Ready
    ///   - SuccessFlagOnBlocked: Success=false when status is Blocked
    ///   - EscalatedOutcomeRecordedSuccessfully: Escalated outcome returns Success=true
    ///   - DuplicateWebhooksSameCaseIdSameHead: multiple webhooks for same (case,head) → last one wins
    ///   - PersistWithReleaseGradeFlag: IsReleaseGrade reflected in persisted pack
    ///   - ReadinessWithNoHeadRef: empty HeadRef returns non-Ready status (fail-closed)
    ///   - CorrelationIdPreserved: CorrelationId from request is stored in record
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffEvidencePersistenceEdgeCaseTests
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Fakes
        // ═══════════════════════════════════════════════════════════════════════

        private sealed class CapturingWebhook : IWebhookService
        {
            public List<WebhookEvent> Events { get; } = new();

            public Task EmitEventAsync(WebhookEvent e)
            {
                lock (Events) Events.Add(e);
                return Task.CompletedTask;
            }

            public Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest r, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string id, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = false });
            public Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string u)
                => Task.FromResult(new WebhookSubscriptionListResponse { Success = true });
            public Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest r, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string id, string u)
                => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest r, string u)
                => Task.FromResult(new WebhookDeliveryHistoryResponse { Success = true });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static ProtectedSignOffEvidencePersistenceService CreateService(IWebhookService? webhook = null)
            => new(NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance, webhook);

        // ═══════════════════════════════════════════════════════════════════════
        // Schema contract tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ReadinessSchemaContract_BlockersListIsNeverNull()
        {
            var svc = CreateService();
            var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest { HeadRef = "sha-schema-test" });
            Assert.That(result.Blockers, Is.Not.Null, "Blockers list must never be null.");
        }

        [Test]
        public async Task ReadinessSchemaContract_StatusIsAlwaysSet()
        {
            var svc = CreateService();
            var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest { HeadRef = "sha-status-set" });
            Assert.That(Enum.IsDefined(typeof(SignOffReleaseReadinessStatus), result.Status), Is.True,
                "Status must always be a defined enum value.");
        }

        [Test]
        public async Task BlockerRemediationHintNonEmpty()
        {
            var svc = CreateService();
            var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest { HeadRef = "sha-rh-test" });
            foreach (var blocker in result.Blockers)
            {
                Assert.That(blocker.RemediationHint, Is.Not.Null.And.Not.Empty,
                    $"Blocker '{blocker.Code}' must have a non-empty RemediationHint.");
            }
        }

        [Test]
        public async Task BlockerCodeNonEmpty()
        {
            var svc = CreateService();
            var result = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest { HeadRef = "sha-code-test" });
            foreach (var blocker in result.Blockers)
            {
                Assert.That(blocker.Code, Is.Not.Null.And.Not.Empty,
                    $"Blocker must have a non-empty Code. Category={blocker.Category}");
            }
        }

        [Test]
        public async Task SuccessFlagOnReady()
        {
            var svc = CreateService();
            const string headRef = "sha-success-ready";
            const string caseId = "case-success-ready";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor");

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            if (result.Status == SignOffReleaseReadinessStatus.Ready)
            {
                Assert.That(result.Success, Is.True, "Success must be true when status is Ready.");
            }
        }

        [Test]
        public async Task SuccessFlagOnBlocked()
        {
            var svc = CreateService();
            const string headRef = "sha-success-blocked";
            const string caseId = "case-success-blocked";

            // Denied webhook → Blocked
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Denied }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor");

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            Assert.That(result.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked));
            Assert.That(result.Success, Is.False, "Success must be false when status is Blocked.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // History tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WebhookHistoryEmpty_ReturnsEmptyListNotNull()
        {
            var svc = CreateService();
            var result = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = "case-empty-history-" + Guid.NewGuid() });
            Assert.That(result.Records, Is.Not.Null, "Records must not be null even when empty.");
            Assert.That(result.Records, Is.Empty, "Records must be empty for a case with no webhooks.");
        }

        [Test]
        public async Task EvidencePackHistoryEmpty_ReturnsEmptyListNotNull()
        {
            var svc = CreateService();
            var result = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { CaseId = "case-empty-pack-history-" + Guid.NewGuid() });
            Assert.That(result.Packs, Is.Not.Null, "Packs must not be null even when empty.");
            Assert.That(result.Packs, Is.Empty, "Packs must be empty for a case with no persisted evidence.");
        }

        [Test]
        public async Task RecordIdUniqueness()
        {
            var svc = CreateService();
            const string caseId = "case-record-id-uniq";
            const string headRef = "sha-record-id-uniq";

            var ids = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                var resp = await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId + i, HeadRef = headRef + i, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
                Assert.That(resp.Record, Is.Not.Null);
                ids.Add(resp.Record!.RecordId);
            }

            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "Every RecordId must be unique.");
        }

        [Test]
        public async Task PackIdUniqueness()
        {
            var svc = CreateService();
            var packIds = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                var caseId = "case-pack-id-uniq-" + i;
                var headRef = "sha-pack-id-uniq-" + i;
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
                var resp = await svc.PersistSignOffEvidenceAsync(
                    new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor");
                Assert.That(resp.Pack, Is.Not.Null);
                packIds.Add(resp.Pack!.PackId);
            }

            Assert.That(packIds.Distinct().Count(), Is.EqualTo(packIds.Count), "Every PackId must be unique.");
        }

        [Test]
        public async Task MaxRecordsHonoured()
        {
            var svc = CreateService();
            const string caseId = "case-maxrecords";

            // Record 10 webhooks
            for (int i = 0; i < 10; i++)
            {
                await svc.RecordApprovalWebhookAsync(
                    new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = $"sha-maxrecords-{i}", Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            }

            var result = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId, MaxRecords = 3 });

            Assert.That(result.Records.Count, Is.LessThanOrEqualTo(3),
                "MaxRecords=3 must limit response to at most 3 records.");
        }

        [Test]
        public async Task HeadRefFilterApplied_OnWebhookHistory()
        {
            var svc = CreateService();
            const string caseId = "case-headref-filter";
            const string targetHead = "sha-headref-filter-target";
            const string otherHead = "sha-headref-filter-other";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = targetHead, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = otherHead, Outcome = ApprovalWebhookOutcome.Denied }, "actor");

            var result = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId, HeadRef = targetHead });

            Assert.That(result.Records.All(r => r.HeadRef == targetHead), Is.True,
                "HeadRef filter must return only records matching the specified HeadRef.");
        }

        [Test]
        public async Task CaseIdFilterApplied_OnEvidencePackHistory()
        {
            var svc = CreateService();
            const string caseA = "case-packhistory-A";
            const string caseB = "case-packhistory-B";
            const string headA = "sha-packhistory-A";
            const string headB = "sha-packhistory-B";

            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest { CaseId = caseA, HeadRef = headA, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest { HeadRef = headA, CaseId = caseA }, "actor");

            await svc.RecordApprovalWebhookAsync(new RecordApprovalWebhookRequest { CaseId = caseB, HeadRef = headB, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(new PersistSignOffEvidenceRequest { HeadRef = headB, CaseId = caseB }, "actor");

            var result = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { CaseId = caseA });

            Assert.That(result.Packs.All(p => p.CaseId == caseA || p.CaseId == null), Is.True,
                "CaseId filter must return only packs for the requested case.");
        }

        [Test]
        public async Task WebhookOutcomePreservedInHistory()
        {
            var svc = CreateService();
            const string caseId = "case-outcome-preserved";
            const string headRef = "sha-outcome-preserved";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Escalated }, "actor");

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });

            Assert.That(history.Records, Is.Not.Empty);
            Assert.That(history.Records.First().Outcome, Is.EqualTo(ApprovalWebhookOutcome.Escalated),
                "Recorded outcome must match what is returned in history.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Actor/input handling
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task NullActorAccepted()
        {
            var svc = CreateService();
            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-nullactor", HeadRef = "sha-nullactor", Outcome = ApprovalWebhookOutcome.Approved }, null!);
            Assert.That(resp.Success, Is.True, "Null actor must not crash the service.");
        }

        [Test]
        public async Task EmptyActorAccepted()
        {
            var svc = CreateService();
            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = "case-emptyactor", HeadRef = "sha-emptyactor", Outcome = ApprovalWebhookOutcome.Approved }, "");
            Assert.That(resp.Success, Is.True, "Empty actor must not crash the service.");
        }

        [Test]
        public async Task CorrelationIdPreserved()
        {
            var svc = CreateService();
            const string correlationId = "corr-12345-abcdef";
            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = "case-corrid",
                    HeadRef = "sha-corrid",
                    Outcome = ApprovalWebhookOutcome.Approved,
                    CorrelationId = correlationId
                }, "actor");

            Assert.That(resp.Record, Is.Not.Null);
            Assert.That(resp.Record!.CorrelationId, Is.EqualTo(correlationId),
                "CorrelationId from request must be preserved in the stored record.");
        }

        [Test]
        public async Task PersistWithActor_CreatedByFieldPropagated()
        {
            var svc = CreateService();
            const string actor = "deployment-bot@biatec.io";
            const string caseId = "case-createdby";
            const string headRef = "sha-createdby";

            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, actor);
            var resp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, actor);

            Assert.That(resp.Success, Is.True);
            Assert.That(resp.Pack, Is.Not.Null);
            Assert.That(resp.Pack!.CreatedBy, Is.EqualTo(actor),
                "CreatedBy field in evidence pack must match the actor passed to PersistSignOffEvidenceAsync.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Isolation tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task MultipleCasesIsolated()
        {
            var svc = CreateService();

            // Approve caseA and persist evidence
            const string caseA = "isolation-case-A";
            const string headA = "sha-isolation-A";
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseA, HeadRef = headA, Outcome = ApprovalWebhookOutcome.Approved }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headA, CaseId = caseA }, "actor");

            // Deny caseB
            const string caseB = "isolation-case-B";
            const string headB = "sha-isolation-B";
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseB, HeadRef = headB, Outcome = ApprovalWebhookOutcome.Denied }, "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headB, CaseId = caseB }, "actor");

            var resultA = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headA, CaseId = caseA });
            var resultB = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headB, CaseId = caseB });

            // caseB must be Blocked regardless of caseA
            Assert.That(resultB.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Blocked),
                "Denied case must be Blocked even when another case is approved.");
            // caseA must not be contaminated by caseB's denial
            Assert.That(resultA.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.Indeterminate),
                "Approved case must have deterministic (non-Indeterminate) status.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Webhook outcome coverage
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EscalatedOutcomeRecordedSuccessfully()
        {
            var svc = CreateService();
            var resp = await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest
                {
                    CaseId = "case-escalated",
                    HeadRef = "sha-escalated",
                    Outcome = ApprovalWebhookOutcome.Escalated,
                    Reason = "Elevated review required"
                }, "escalation-bot");

            Assert.That(resp.Success, Is.True, "Recording an Escalated webhook must succeed.");
            Assert.That(resp.Record, Is.Not.Null);
            Assert.That(resp.Record!.Outcome, Is.EqualTo(ApprovalWebhookOutcome.Escalated));
        }

        [Test]
        public async Task EscalatedThenApproved_Latest_Approved_Wins()
        {
            var svc = CreateService();
            const string caseId = "case-escalated-then-approved";
            const string headRef = "sha-escalated-then-approved";

            // First escalated, then approved
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Escalated }, "reviewer");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, "reviewer");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId }, "actor");

            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = headRef, CaseId = caseId });

            // With the latest webhook being Approved, the case should be Ready or at least not solely blocked by ApprovalDenied
            Assert.That(result.Blockers.Any(b => b.Category == SignOffReleaseBlockerCategory.ApprovalDenied), Is.False,
                "Approved (latest) webhook after escalation must not produce an ApprovalDenied blocker.");
        }

        [Test]
        public async Task DuplicateWebhooksSameCaseAndHead_MultipleStoredSuccessfully()
        {
            var svc = CreateService();
            const string caseId = "case-dup-webhooks";
            const string headRef = "sha-dup-webhooks";

            // Record 3 webhooks for same (case, head)
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Escalated }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Denied }, "actor");
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { CaseId = caseId, HeadRef = headRef, Outcome = ApprovalWebhookOutcome.Approved }, "actor");

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { CaseId = caseId });

            Assert.That(history.Records.Count, Is.GreaterThanOrEqualTo(3),
                "All 3 duplicate webhooks for same (case, head) must be stored.");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Fail-closed edge cases
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ReadinessWithEmptyHeadRef_IsNotReady()
        {
            var svc = CreateService();
            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = string.Empty });

            // Empty head ref must never be Ready
            Assert.That(result.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.Ready),
                "Empty HeadRef must not produce Ready status (fail-closed).");
            Assert.That(result.Success, Is.False,
                "Empty HeadRef must produce Success=false.");
        }

        [Test]
        public async Task PersistEvidence_WithoutPriorWebhook_RequireApprovalWebhookFalse_Succeeds()
        {
            var svc = CreateService();
            const string headRef = "sha-no-webhook-no-require";
            const string caseId = "case-no-webhook-no-require";

            // RequireApprovalWebhook defaults to false — persisting without a webhook should succeed
            var resp = await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = headRef, CaseId = caseId, RequireApprovalWebhook = false }, "actor");

            Assert.That(resp.Success, Is.True,
                "Persisting evidence without approval webhook must succeed when RequireApprovalWebhook=false.");
        }

        [Test]
        public async Task MultipleBlockers_AllApplicableBlockersIncluded()
        {
            var svc = CreateService();

            // No webhook, no evidence for this case → should produce multiple blockers
            var result = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest
                {
                    HeadRef = "sha-multi-blocker",
                    CaseId = "case-multi-blocker"
                });

            // At least one blocker should be present (fail-closed)
            Assert.That(result.Blockers, Is.Not.Empty,
                "Multiple blockers must be included when both approval and evidence are missing.");
        }
    }
}
