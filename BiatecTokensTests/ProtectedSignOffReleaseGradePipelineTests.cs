using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Pipeline integration tests for the release-grade sign-off pathway and
    /// <see cref="SignOffReleaseReadinessStatus.NotReleaseEvidence"/> state transitions.
    ///
    /// These tests prove the full chain: record approval webhook → persist evidence →
    /// evaluate readiness → observe <c>Mode=ReadyReleaseGrade</c> or
    /// <c>Mode=Configured/NotReleaseEvidence</c> depending on conditions.
    ///
    /// Test groups:
    ///   RGP01–RGP10  – Full release-grade pipeline (webhook + evidence → ReadyReleaseGrade)
    ///   RGP11–RGP20  – NotReleaseEvidence state conditions and boundary detection
    ///   RGP21–RGP30  – Mode transitions, freshness, history integration, cross-cutting
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffReleaseGradePipelineTests
    {
        // ─── Fakes ──────────────────────────────────────────────────────────

        private sealed class CapturingWebhook : IWebhookService
        {
            public List<WebhookEvent> Events { get; } = new();
            public Task EmitEventAsync(WebhookEvent evt) { lock (Events) Events.Add(evt); return Task.CompletedTask; }
            public Task<WebhookSubscriptionResponse> CreateSubscriptionAsync(CreateWebhookSubscriptionRequest r, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> GetSubscriptionAsync(string id, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = false });
            public Task<WebhookSubscriptionListResponse> ListSubscriptionsAsync(string u) => Task.FromResult(new WebhookSubscriptionListResponse { Success = true });
            public Task<WebhookSubscriptionResponse> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest r, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookSubscriptionResponse> DeleteSubscriptionAsync(string id, string u) => Task.FromResult(new WebhookSubscriptionResponse { Success = true });
            public Task<WebhookDeliveryHistoryResponse> GetDeliveryHistoryAsync(GetWebhookDeliveryHistoryRequest r, string u) => Task.FromResult(new WebhookDeliveryHistoryResponse { Success = true });
        }

        private sealed class FakeTime : TimeProvider
        {
            private DateTimeOffset _now;
            public FakeTime(DateTimeOffset t) => _now = t;
            public void Advance(TimeSpan d) => _now += d;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        // ─── Factories ──────────────────────────────────────────────────────

        private static ProtectedSignOffEvidencePersistenceService Svc(
            CapturingWebhook? wh = null, TimeProvider? tp = null)
            => new(NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance, wh ?? new CapturingWebhook(), tp);

        private static RecordApprovalWebhookRequest Approval(string head, string caseId = "rgp-case",
            ApprovalWebhookOutcome outcome = ApprovalWebhookOutcome.Approved)
            => new() { HeadRef = head, CaseId = caseId, Outcome = outcome, ActorId = "rgp-reviewer", Reason = "RGP test" };

        private static PersistSignOffEvidenceRequest Persist(string head, string caseId = "rgp-case",
            bool releaseGrade = true, bool requireApprovalWebhook = true, string? label = null)
            => new() { HeadRef = head, CaseId = caseId, RequireReleaseGrade = releaseGrade, RequireApprovalWebhook = requireApprovalWebhook, EnvironmentLabel = label };

        private static GetSignOffReleaseReadinessRequest Readiness(string head, string caseId = "rgp-case",
            int windowHours = 24)
            => new() { HeadRef = head, CaseId = caseId, FreshnessWindowHours = windowHours };

        private static async Task PollAsync(CapturingWebhook wh, WebhookEventType type, int maxMs = 2000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
            while (DateTime.UtcNow < deadline)
            {
                lock (wh.Events) { if (wh.Events.Any(e => e.EventType == type)) return; }
                await Task.Delay(20);
            }
            Assert.Fail($"Webhook event {type} was not emitted within {maxMs}ms.");
        }

        // ═══════════════════════════════════════════════════════════════════
        // RGP01–RGP10  Full release-grade pipeline
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>RGP01: Full pipeline (webhook + release-grade evidence) yields Status=Ready, Mode=ReadyReleaseGrade.</summary>
        [Test]
        public async Task RGP01_FullPipeline_YieldsReadyReleaseGrade()
        {
            var svc = Svc();
            const string head = "rgp01-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head), "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.Status, Is.EqualTo(SignOffReleaseReadinessStatus.Ready), "RGP01: Status must be Ready.");
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade), "RGP01: Mode must be ReadyReleaseGrade.");
        }

        /// <summary>RGP02: IsReleaseEvidence is true only when Mode=ReadyReleaseGrade.</summary>
        [Test]
        public async Task RGP02_IsReleaseEvidence_TrueOnlyForReadyReleaseGrade()
        {
            var svc = Svc();
            const string head = "rgp02-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head), "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.IsReleaseEvidence, Is.True, "RGP02: IsReleaseEvidence must be true for ReadyReleaseGrade.");
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade));
        }

        /// <summary>RGP03: Without approval webhook, even release-grade evidence cannot reach ReadyReleaseGrade.</summary>
        [Test]
        public async Task RGP03_NoWebhook_ReleaseGradeEvidence_NeverReadyReleaseGrade()
        {
            var svc = Svc();
            const string head = "rgp03-sha";
            // Persist release-grade evidence WITHOUT prior webhook (requireApprovalWebhook=false here)
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case", RequireReleaseGrade = false },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGP03: No approval webhook → cannot be ReadyReleaseGrade.");
        }

        /// <summary>RGP04: Without a prior approval webhook, the persisted pack has IsReleaseGrade=false → Mode never ReadyReleaseGrade.</summary>
        [Test]
        public async Task RGP04_NonReleaseGradePack_ModeNeverReadyReleaseGrade()
        {
            var svc = Svc();
            const string head = "rgp04-sha";
            // No webhook recorded → approvalWebhook=null when persisting → IsReleaseGrade=false on the pack
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case", RequireReleaseGrade = false, RequireApprovalWebhook = false },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGP04: No approval webhook → isReleaseGrade=false → Mode must not be ReadyReleaseGrade.");
            Assert.That(r.IsReleaseEvidence, Is.False, "RGP04: IsReleaseEvidence must be false.");
        }

        /// <summary>RGP05: ReadyReleaseGrade with custom 48h window remains valid within window.</summary>
        [Test]
        public async Task RGP05_CustomWindow48h_ReadyReleaseGrade_WithinWindow()
        {
            var tp = new FakeTime(DateTimeOffset.UtcNow);
            var svc = Svc(tp: tp);
            const string head = "rgp05-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            // Persist with 48h freshness window so ExpiresAt = now + 48h
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case", RequireReleaseGrade = true, RequireApprovalWebhook = true, FreshnessWindowHours = 48 },
                "actor");
            tp.Advance(TimeSpan.FromHours(47)); // 47h into 48h window

            var r = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = "rgp-case", FreshnessWindowHours = 48 });

            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGP05: 47h into 48h window → still ReadyReleaseGrade.");
        }

        /// <summary>RGP06: ReadyReleaseGrade degrades to StaleEvidence after 48h window expires.</summary>
        [Test]
        public async Task RGP06_CustomWindow48h_ExpiresAfter48h_BecomesStalevidence()
        {
            var tp = new FakeTime(DateTimeOffset.UtcNow);
            var svc = Svc(tp: tp);
            const string head = "rgp06-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            // Persist with 48h freshness window so ExpiresAt = now + 48h
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case", RequireReleaseGrade = true, RequireApprovalWebhook = true, FreshnessWindowHours = 48 },
                "actor");
            tp.Advance(TimeSpan.FromHours(49)); // past 48h window

            var r = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = "rgp-case", FreshnessWindowHours = 48 });

            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.StaleEvidence),
                "RGP06: 49h into 48h window → StaleEvidence.");
            Assert.That(r.IsReleaseEvidence, Is.False, "RGP06: Stale evidence is not release evidence.");
        }

        /// <summary>RGP07: EnvironmentLabel flows from pack to ReadyReleaseGrade response.</summary>
        [Test]
        public async Task RGP07_EnvironmentLabel_FlowsInto_ReadyReleaseGradeResponse()
        {
            var svc = Svc();
            const string head = "rgp07-sha";
            const string label = "protected-ci-staging";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head, label: label), "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade));
            Assert.That(r.EnvironmentLabel, Is.EqualTo(label),
                "RGP07: EnvironmentLabel must propagate from pack to ReadyReleaseGrade response.");
        }

        /// <summary>RGP08: Multiple packs — latest pack determines EnvironmentLabel in ReadyReleaseGrade response.</summary>
        [Test]
        public async Task RGP08_LatestPack_DeterminesEnvironmentLabel()
        {
            var svc = Svc();
            const string head = "rgp08-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head, label: "env-v1"), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head, label: "env-v2"), "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.EnvironmentLabel, Is.EqualTo("env-v2"),
                "RGP08: Latest pack's EnvironmentLabel must win.");
        }

        /// <summary>RGP09: ReadyReleaseGrade emits ProtectedSignOffReleaseReadySignaled webhook event.</summary>
        [Test]
        public async Task RGP09_ReadyReleaseGrade_EmitsReleaseReadySignaledEvent()
        {
            var wh = new CapturingWebhook();
            var svc = Svc(wh);
            const string head = "rgp09-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head), "actor");
            await svc.GetReleaseReadinessAsync(Readiness(head));

            await PollAsync(wh, WebhookEventType.ProtectedSignOffReleaseReadySignaled);

            lock (wh.Events)
            {
                Assert.That(
                    wh.Events.Any(e => e.EventType == WebhookEventType.ProtectedSignOffReleaseReadySignaled),
                    Is.True, "RGP09: Ready signal must be emitted when Mode=ReadyReleaseGrade.");
            }
        }

        /// <summary>RGP10: Two different heads are independently tracked — ReadyReleaseGrade on one does not affect the other.</summary>
        [Test]
        public async Task RGP10_TwoHeads_ReadyReleaseGrade_DoesNotCrossContaminate()
        {
            var svc = Svc();
            const string head1 = "rgp10-sha-a";
            const string head2 = "rgp10-sha-b";

            // head1: full pipeline
            await svc.RecordApprovalWebhookAsync(Approval(head1), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head1), "actor");

            // head2: no evidence at all
            var r1 = await svc.GetReleaseReadinessAsync(Readiness(head1));
            var r2 = await svc.GetReleaseReadinessAsync(Readiness(head2));

            Assert.That(r1.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade), "RGP10: head1 should be ReadyReleaseGrade.");
            Assert.That(r2.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade), "RGP10: head2 must not inherit head1's state.");
        }

        // ═══════════════════════════════════════════════════════════════════
        // RGP11–RGP20  NotReleaseEvidence state
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>RGP11: NotReleaseEvidence when evidence is not release-grade AND no webhook received.</summary>
        [Test]
        public async Task RGP11_NotReleaseEvidence_WhenIsReleaseGradeFalse_AndNoWebhook()
        {
            var svc = Svc();
            const string head = "rgp11-sha";
            // Persist without release-grade, without requiring webhook
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case", RequireReleaseGrade = false, RequireApprovalWebhook = false },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.Status, Is.EqualTo(SignOffReleaseReadinessStatus.NotReleaseEvidence),
                "RGP11: No webhook + non-release-grade pack → NotReleaseEvidence.");
        }

        /// <summary>RGP12: NotReleaseEvidence Mode maps to Configured (not NotConfigured).</summary>
        [Test]
        public async Task RGP12_NotReleaseEvidence_ModeIsConfigured()
        {
            var svc = Svc();
            const string head = "rgp12-sha";
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case", RequireReleaseGrade = false, RequireApprovalWebhook = false },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.Status, Is.EqualTo(SignOffReleaseReadinessStatus.NotReleaseEvidence));
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.Configured),
                "RGP12: NotReleaseEvidence → Mode must be Configured (not NotConfigured).");
        }

        /// <summary>RGP13: NotReleaseEvidence yields IsReleaseEvidence=false.</summary>
        [Test]
        public async Task RGP13_NotReleaseEvidence_IsReleaseEvidence_False()
        {
            var svc = Svc();
            const string head = "rgp13-sha";
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case" },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.IsReleaseEvidence, Is.False, "RGP13: NotReleaseEvidence → IsReleaseEvidence must be false.");
        }

        /// <summary>RGP14: NotReleaseEvidence has non-null, non-empty OperatorGuidance.</summary>
        [Test]
        public async Task RGP14_NotReleaseEvidence_OperatorGuidance_IsNonEmpty()
        {
            var svc = Svc();
            const string head = "rgp14-sha";
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case" },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.Status, Is.EqualTo(SignOffReleaseReadinessStatus.NotReleaseEvidence));
            Assert.That(r.OperatorGuidance, Is.Not.Null.And.Not.Empty,
                "RGP14: NotReleaseEvidence must carry non-empty OperatorGuidance.");
        }

        /// <summary>RGP15: IsReleaseGrade=false + Approved webhook → Blocked (not NotReleaseEvidence).</summary>
        [Test]
        public async Task RGP15_NonReleaseGrade_WithApprovedWebhook_IsBlocked_NotNotReleaseEvidence()
        {
            var svc = Svc();
            const string head = "rgp15-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case", RequireReleaseGrade = false, RequireApprovalWebhook = false },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            // When an approved webhook exists, hasApprovalWebhook=true → NotReleaseEvidence condition fails
            // → falls through to Blocked because pack is not release-grade
            Assert.That(r.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.NotReleaseEvidence),
                "RGP15: Any approval webhook prevents NotReleaseEvidence (webhook changes the branch taken).");
        }

        /// <summary>RGP16: IsReleaseGrade=false + Denied webhook → Blocked (ApprovalDenied), not NotReleaseEvidence.</summary>
        [Test]
        public async Task RGP16_NonReleaseGrade_WithDeniedWebhook_IsBlocked_NotNotReleaseEvidence()
        {
            var svc = Svc();
            const string head = "rgp16-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head, outcome: ApprovalWebhookOutcome.Denied), "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case", RequireReleaseGrade = false, RequireApprovalWebhook = false },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.Status, Is.Not.EqualTo(SignOffReleaseReadinessStatus.NotReleaseEvidence),
                "RGP16: Denied webhook → hasApprovalWebhook=true → NotReleaseEvidence condition fails.");
            Assert.That(r.Blockers.Any(b => b.Category == SignOffReleaseBlockerCategory.ApprovalDenied),
                Is.True, "RGP16: Denied webhook → ApprovalDenied blocker present.");
        }

        /// <summary>RGP17: NotReleaseEvidence transitions to ReadyReleaseGrade after adding approval + release-grade evidence.</summary>
        [Test]
        public async Task RGP17_NotReleaseEvidence_TransitionsTo_ReadyReleaseGrade()
        {
            var svc = Svc();
            const string head = "rgp17-sha";

            // First: no webhook, non-release-grade evidence → NotReleaseEvidence
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case" },
                "actor");
            var r1 = await svc.GetReleaseReadinessAsync(Readiness(head));
            Assert.That(r1.Status, Is.EqualTo(SignOffReleaseReadinessStatus.NotReleaseEvidence));

            // Now: add webhook + release-grade evidence
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head), "actor");
            var r2 = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r2.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGP17: After adding webhook + release-grade pack, Mode must upgrade to ReadyReleaseGrade.");
        }

        /// <summary>RGP18: Two different case IDs are isolated — NotReleaseEvidence on one does not affect the other.</summary>
        [Test]
        public async Task RGP18_DifferentCaseIds_NotReleaseEvidence_Isolated()
        {
            var svc = Svc();
            const string head = "rgp18-sha";

            // caseA: non-release-grade, no webhook
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp18-caseA" },
                "actor");

            // caseB: full pipeline
            await svc.RecordApprovalWebhookAsync(
                new RecordApprovalWebhookRequest { HeadRef = head, CaseId = "rgp18-caseB", Outcome = ApprovalWebhookOutcome.Approved, ActorId = "a", Reason = "ok" },
                "actor");
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp18-caseB", RequireReleaseGrade = true, RequireApprovalWebhook = true },
                "actor");

            var rA = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = "rgp18-caseA", FreshnessWindowHours = 24 });
            var rB = await svc.GetReleaseReadinessAsync(new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = "rgp18-caseB", FreshnessWindowHours = 24 });

            Assert.That(rA.Status, Is.EqualTo(SignOffReleaseReadinessStatus.NotReleaseEvidence), "RGP18: caseA must be NotReleaseEvidence.");
            Assert.That(rB.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade), "RGP18: caseB must be ReadyReleaseGrade.");
        }

        /// <summary>RGP19: NotReleaseEvidence Success field is false.</summary>
        [Test]
        public async Task RGP19_NotReleaseEvidence_SuccessIsFalse()
        {
            var svc = Svc();
            const string head = "rgp19-sha";
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case" },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.Status, Is.EqualTo(SignOffReleaseReadinessStatus.NotReleaseEvidence));
            Assert.That(r.Success, Is.False, "RGP19: NotReleaseEvidence → Success must be false.");
        }

        /// <summary>RGP20: NotReleaseEvidence HasApprovalWebhook is false (no webhook received).</summary>
        [Test]
        public async Task RGP20_NotReleaseEvidence_HasApprovalWebhook_False()
        {
            var svc = Svc();
            const string head = "rgp20-sha";
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case" },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            Assert.That(r.Status, Is.EqualTo(SignOffReleaseReadinessStatus.NotReleaseEvidence));
            Assert.That(r.HasApprovalWebhook, Is.False,
                "RGP20: NotReleaseEvidence condition requires no webhook → HasApprovalWebhook must be false.");
        }

        // ═══════════════════════════════════════════════════════════════════
        // RGP21–RGP30  Mode transitions, freshness, history integration
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>RGP21: Mode progresses Configured → ReadyReleaseGrade after completing pipeline.</summary>
        [Test]
        public async Task RGP21_ModeProgresses_Configured_To_ReadyReleaseGrade()
        {
            var svc = Svc();
            const string head = "rgp21-sha";

            var r1 = await svc.GetReleaseReadinessAsync(Readiness(head));
            Assert.That(r1.Mode, Is.EqualTo(StrictArtifactMode.Configured), "RGP21: Before evidence → Configured.");

            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head), "actor");

            var r2 = await svc.GetReleaseReadinessAsync(Readiness(head));
            Assert.That(r2.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGP21: After full pipeline → ReadyReleaseGrade.");
        }

        /// <summary>RGP22: Mode degrades from ReadyReleaseGrade to StaleEvidence when the freshness window expires.</summary>
        [Test]
        public async Task RGP22_ReadyReleaseGrade_DegradeToStale_AfterWindowExpiry()
        {
            var tp = new FakeTime(DateTimeOffset.UtcNow);
            var svc = Svc(tp: tp);
            const string head = "rgp22-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head), "actor");

            var r1 = await svc.GetReleaseReadinessAsync(Readiness(head));
            Assert.That(r1.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade), "RGP22: Before expiry → ReadyReleaseGrade.");

            tp.Advance(TimeSpan.FromHours(25)); // past default 24h window

            var r2 = await svc.GetReleaseReadinessAsync(Readiness(head));
            Assert.That(r2.Mode, Is.EqualTo(StrictArtifactMode.StaleEvidence),
                "RGP22: After 24h window expiry → StaleEvidence.");
        }

        /// <summary>RGP23: Three consecutive reads with same state return identical Mode.</summary>
        [Test]
        public async Task RGP23_RepeatedReads_ReturnIdenticalMode()
        {
            var svc = Svc();
            const string head = "rgp23-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head), "actor");

            var modes = new List<StrictArtifactMode>();
            for (int i = 0; i < 3; i++)
                modes.Add((await svc.GetReleaseReadinessAsync(Readiness(head))).Mode);

            Assert.That(modes, Is.All.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGP23: Mode must be idempotent across repeated reads.");
        }

        /// <summary>RGP24: GetEvidencePackHistory contains the latest pack with correct EnvironmentLabel.</summary>
        [Test]
        public async Task RGP24_GetEvidencePackHistory_Contains_CorrectEnvironmentLabel()
        {
            var svc = Svc();
            const string head = "rgp24-sha";
            const string label = "rgp-release-env";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head, label: label), "actor");

            var history = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = head, CaseId = "rgp-case" });

            Assert.That(history.Success, Is.True, "RGP24: GetEvidencePackHistory must succeed.");
            Assert.That(history.Packs, Is.Not.Empty, "RGP24: Pack must be present in history.");
            Assert.That(history.Packs.First().EnvironmentLabel, Is.EqualTo(label),
                "RGP24: EnvironmentLabel must be stored in the history pack.");
        }

        /// <summary>RGP25: GetEvidencePackHistory returns packs in newest-first order.</summary>
        [Test]
        public async Task RGP25_GetEvidencePackHistory_NewestFirst()
        {
            var svc = Svc();
            const string head = "rgp25-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head, label: "env-first"), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head, label: "env-second"), "actor");

            var history = await svc.GetEvidencePackHistoryAsync(
                new GetEvidencePackHistoryRequest { HeadRef = head, CaseId = "rgp-case" });

            Assert.That(history.Packs.Count, Is.GreaterThanOrEqualTo(2), "RGP25: Both packs must be in history.");
            Assert.That(history.Packs.First().EnvironmentLabel, Is.EqualTo("env-second"),
                "RGP25: Newest pack must be listed first.");
        }

        /// <summary>RGP26: GetApprovalWebhookHistory reflects the Approved webhook after recording.</summary>
        [Test]
        public async Task RGP26_GetApprovalWebhookHistory_ReflectsRecordedWebhook()
        {
            var svc = Svc();
            const string head = "rgp26-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { HeadRef = head, CaseId = "rgp-case" });

            Assert.That(history.Success, Is.True, "RGP26: GetApprovalWebhookHistory must succeed.");
            Assert.That(history.Records, Is.Not.Empty, "RGP26: Webhook must be present in history.");
            Assert.That(history.Records.First().Outcome, Is.EqualTo(ApprovalWebhookOutcome.Approved),
                "RGP26: Recorded Approved webhook must appear in history.");
        }

        /// <summary>RGP27: GetApprovalWebhookHistory shows both Approved and Denied webhooks when both recorded.</summary>
        [Test]
        public async Task RGP27_GetApprovalWebhookHistory_ShowsApprovedAndDenied()
        {
            var svc = Svc();
            const string head = "rgp27-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head, outcome: ApprovalWebhookOutcome.Denied), "actor");
            await svc.RecordApprovalWebhookAsync(Approval(head, outcome: ApprovalWebhookOutcome.Approved), "actor");

            var history = await svc.GetApprovalWebhookHistoryAsync(
                new GetApprovalWebhookHistoryRequest { HeadRef = head, CaseId = "rgp-case" });

            var outcomes = history.Records.Select(r => r.Outcome).ToList();
            Assert.That(outcomes, Does.Contain(ApprovalWebhookOutcome.Approved), "RGP27: Approved must appear.");
            Assert.That(outcomes, Does.Contain(ApprovalWebhookOutcome.Denied), "RGP27: Denied must appear.");
        }

        /// <summary>RGP28: FreshnessWindowHours=0 defaults to 24h (the service default behaviour).</summary>
        [Test]
        public async Task RGP28_FreshnessWindowHoursZero_DefaultsTo24h()
        {
            var tp = new FakeTime(DateTimeOffset.UtcNow);
            var svc = Svc(tp: tp);
            const string head = "rgp28-sha";
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head), "actor");
            tp.Advance(TimeSpan.FromHours(23)); // 23h, still fresh under default 24h

            // FreshnessWindowHours=0 → service defaults to 24h
            var r = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head, CaseId = "rgp-case", FreshnessWindowHours = 0 });

            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "RGP28: 0-hour window defaults to 24h → 23h-old evidence is still fresh.");
        }

        /// <summary>RGP29: Mode and EnvironmentLabel are independent — Mode=Configured does not suppress EnvironmentLabel.</summary>
        [Test]
        public async Task RGP29_ModeConfigured_EnvironmentLabel_StillPresent()
        {
            var svc = Svc();
            const string head = "rgp29-sha";
            const string label = "rgp29-env";

            // Persist without approval webhook so pack exists but can't reach ReadyReleaseGrade
            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest { HeadRef = head, CaseId = "rgp-case", RequireReleaseGrade = false, RequireApprovalWebhook = false, EnvironmentLabel = label },
                "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            // Mode should be NotReleaseEvidence (no webhook), not ReadyReleaseGrade
            Assert.That(r.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade));
            Assert.That(r.EnvironmentLabel, Is.EqualTo(label),
                "RGP29: EnvironmentLabel must still be echoed even when Mode is not ReadyReleaseGrade.");
        }

        /// <summary>RGP30: IsReleaseEvidence invariant — only true when Status=Ready AND IsReleaseGrade=true.</summary>
        [Test]
        public async Task RGP30_IsReleaseEvidence_Invariant_OnlyTrueWhenReadyAndReleaseGrade()
        {
            var svc = Svc();
            const string head = "rgp30-sha";

            // Set up full pipeline
            await svc.RecordApprovalWebhookAsync(Approval(head), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(head), "actor");

            var r = await svc.GetReleaseReadinessAsync(Readiness(head));

            // The invariant: IsReleaseEvidence == (Status==Ready && IsReleaseGrade==true)
            bool packIsReleaseGrade = r.LatestEvidencePack?.IsReleaseGrade ?? false;
            bool expectedIsReleaseEvidence = r.Status == SignOffReleaseReadinessStatus.Ready && packIsReleaseGrade;

            Assert.That(r.IsReleaseEvidence, Is.EqualTo(expectedIsReleaseEvidence),
                "RGP30: IsReleaseEvidence must equal (Status==Ready AND pack.IsReleaseGrade==true).");
        }
    }
}
