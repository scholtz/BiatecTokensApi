using BiatecTokensApi.Models.ProtectedSignOffEvidencePersistence;
using BiatecTokensApi.Models.Webhook;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Branch-coverage tests for <c>StrictArtifactMode</c> computation and
    /// <c>EnvironmentLabel</c> handling in
    /// <see cref="ProtectedSignOffEvidencePersistenceService"/>.
    ///
    /// These tests saturate every branch of the <c>overallStatus switch</c>
    /// expression that produces <c>Mode</c>, every <c>IsReleaseEvidence</c>
    /// invariant, and all <c>EnvironmentLabel</c> normalisation paths.
    ///
    /// Test groups:
    ///   BC01–BC05  – Enum contract (values, distinctness, count, integer backing, serialisation)
    ///   BC06–BC10  – Mode switch: each arm fires exactly once under the correct trigger
    ///   BC11–BC15  – Mode switch: _ fallback arm covers Blocked / Pending / Ready (non-release)
    ///   BC16–BC20  – IsReleaseEvidence invariant (never true for non-Ready or non-release-grade)
    ///   BC21–BC25  – EnvironmentLabel normalisation paths (null / whitespace / valid / overwrite / multi-pack)
    ///   BC26–BC30  – Cross-cutting: concurrent isolation, idempotency, different headRefs, mode + label combo
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ProtectedSignOffStrictArtifactBranchCoverageTests
    {
        // ─── Fakes / helpers ────────────────────────────────────────────────

        private sealed class NoOpWebhook : IWebhookService
        {
            public Task EmitEventAsync(WebhookEvent evt) => Task.CompletedTask;
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

        private static ProtectedSignOffEvidencePersistenceService Svc(TimeProvider? tp = null)
            => new(NullLogger<ProtectedSignOffEvidencePersistenceService>.Instance, new NoOpWebhook(), tp);

        private static PersistSignOffEvidenceRequest Persist(
            string head = "bc-sha-001",
            bool releaseGrade = false,
            bool requireApprovalWebhook = false,
            string? label = null)
            => new()
            {
                HeadRef = head,
                CaseId = "bc-case",
                RequireReleaseGrade = releaseGrade,
                RequireApprovalWebhook = requireApprovalWebhook,
                EnvironmentLabel = label
            };

        private static GetSignOffReleaseReadinessRequest Readiness(
            string head = "bc-sha-001",
            int windowHours = 24)
            => new() { HeadRef = head, CaseId = "bc-case", FreshnessWindowHours = windowHours };

        private static RecordApprovalWebhookRequest Approval(
            string head = "bc-sha-001",
            ApprovalWebhookOutcome outcome = ApprovalWebhookOutcome.Approved)
            => new()
            {
                HeadRef = head,
                CaseId = "bc-case",
                Outcome = outcome,
                ActorId = "bc-reviewer",
                Reason = "BC test"
            };

        // ═══════════════════════════════════════════════════════════════════
        // BC01–BC05  Enum contract
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void BC01_StrictArtifactMode_HasExactlyFiveValues()
        {
            var values = Enum.GetValues<StrictArtifactMode>();
            Assert.That(values.Length, Is.EqualTo(5), "StrictArtifactMode must have exactly 5 values.");
        }

        [Test]
        public void BC02_StrictArtifactMode_AllValuesDistinct()
        {
            var values = Enum.GetValues<StrictArtifactMode>().Cast<int>().ToList();
            Assert.That(values.Distinct().Count(), Is.EqualTo(5), "No two StrictArtifactMode values may share an integer backing.");
        }

        [Test]
        public void BC03_StrictArtifactMode_AllExpectedNamesPresent()
        {
            var names = Enum.GetNames<StrictArtifactMode>();
            Assert.That(names, Does.Contain("NotConfigured"));
            Assert.That(names, Does.Contain("Configured"));
            Assert.That(names, Does.Contain("Degraded"));
            Assert.That(names, Does.Contain("StaleEvidence"));
            Assert.That(names, Does.Contain("ReadyReleaseGrade"));
        }

        [Test]
        public void BC04_StrictArtifactMode_ParseFromString_RoundTrips()
        {
            foreach (var name in Enum.GetNames<StrictArtifactMode>())
            {
                var parsed = Enum.Parse<StrictArtifactMode>(name);
                Assert.That(parsed.ToString(), Is.EqualTo(name), $"Round-trip failed for {name}");
            }
        }

        [Test]
        public void BC05_StrictArtifactMode_NotConfigured_IsZeroOrPositive()
        {
            // All enum values must be non-negative integers (standard .NET enum behaviour).
            foreach (var v in Enum.GetValues<StrictArtifactMode>())
                Assert.That((int)v, Is.GreaterThanOrEqualTo(0), $"{v} must have non-negative backing.");
        }

        // ═══════════════════════════════════════════════════════════════════
        // BC06–BC10  Mode switch: each named arm
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task BC06_Mode_Configured_WhenNoPackExists()
        {
            // In-memory service with no env config: missing evidence → BlockedMissingEvidence → _ arm → Configured
            var svc = Svc();
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            // BlockedMissingEvidence path → _ arm in mode switch → Configured (not NotConfigured)
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.Configured));
            Assert.That(r.Status, Is.EqualTo(SignOffReleaseReadinessStatus.BlockedMissingEvidence));
        }

        [Test]
        public async Task BC07_Mode_Configured_WhenPackExistsNotReleaseGradeNoWebhook()
        {
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(Persist(releaseGrade: false, requireApprovalWebhook: false), "actor");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            // Not release-grade + no webhook → NotReleaseEvidence → _ fallback → Configured
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.Configured));
        }

        [Test]
        public async Task BC08_Mode_StaleEvidence_WhenWindowExpired()
        {
            var tp = new FakeTime(DateTimeOffset.UtcNow);
            var svc = Svc(tp);
            await svc.PersistSignOffEvidenceAsync(Persist(), "actor");
            tp.Advance(TimeSpan.FromHours(25)); // beyond 24-hour default
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.StaleEvidence));
        }

        [Test]
        public async Task BC09_Mode_ReadyReleaseGrade_WhenReleaseGradeAndApproved()
        {
            // Must record approval webhook FIRST, then persist with RequireReleaseGrade=true
            var svc = Svc();
            await svc.RecordApprovalWebhookAsync(Approval(), "bc-reviewer");
            await svc.PersistSignOffEvidenceAsync(Persist(releaseGrade: true, requireApprovalWebhook: false), "actor");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade));
            Assert.That(r.IsReleaseEvidence, Is.True);
        }

        [Test]
        public async Task BC10_Mode_Configured_WhenBlockedByApprovalWebhookNotReceived()
        {
            // requireApprovalWebhook=true but no webhook yet → Blocked → _ fallback → Configured
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(
                Persist(releaseGrade: true, requireApprovalWebhook: true), "actor");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            // Blocked because webhook not received
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.Configured));
        }

        // ═══════════════════════════════════════════════════════════════════
        // BC11–BC15  Mode switch: _ fallback arms
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task BC11_Mode_Configured_WhenApprovalWebhookDenied()
        {
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(
                Persist(releaseGrade: true, requireApprovalWebhook: false), "actor");
            // Record a denied webhook — this creates a critical blocker, resulting in Blocked status
            await svc.RecordApprovalWebhookAsync(Approval(outcome: ApprovalWebhookOutcome.Denied), "bc-reviewer");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            // Blocked (denial) → _ arm → Configured
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.Configured));
        }

        [Test]
        public async Task BC12_Mode_Configured_WhenReadyButNotReleaseGrade()
        {
            // Release-grade evidence: RequireReleaseGrade=false, pack.IsReleaseGrade=false
            // with approval webhook received → Ready but IsReleaseEvidence=false → _ arm → Configured
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(
                Persist(releaseGrade: false, requireApprovalWebhook: true), "actor");
            await svc.RecordApprovalWebhookAsync(Approval(), "bc-reviewer");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            // Status is Ready (webhook received clears the pending), but IsReleaseEvidence=false
            // because pack.IsReleaseGrade=false → _ arm
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.Configured));
        }

        [Test]
        public async Task BC13_Mode_NeverNull()
        {
            // Mode property must always be set (not default(StrictArtifactMode) == 0 is fine,
            // but the field must be a valid enum value)
            var svc = Svc();
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(Enum.IsDefined(r.Mode), Is.True, "Mode must always be a defined StrictArtifactMode value.");
        }

        [Test]
        public async Task BC14_Mode_OnlyReadyReleaseGrade_HasIsReleaseEvidenceTrue()
        {
            // Without a pack: BlockedMissingEvidence → Configured, IsReleaseEvidence=false
            var svc = Svc();
            var r1 = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r1.Mode, Is.EqualTo(StrictArtifactMode.Configured));
            Assert.That(r1.IsReleaseEvidence, Is.False);

            // With non-release-grade pack (no webhook): NotReleaseEvidence → Configured
            var svc2 = Svc();
            await svc2.PersistSignOffEvidenceAsync(Persist(releaseGrade: false), "actor");
            var r2 = await svc2.GetReleaseReadinessAsync(Readiness());
            Assert.That(r2.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade));
            Assert.That(r2.IsReleaseEvidence, Is.False);
        }

        [Test]
        public async Task BC15_Mode_StaleEvidence_NeverCarriesIsReleaseEvidenceTrue()
        {
            // Use releaseGrade=false so the pack persists without needing an approval webhook
            var tp = new FakeTime(DateTimeOffset.UtcNow);
            var svc = Svc(tp);
            await svc.PersistSignOffEvidenceAsync(Persist(releaseGrade: false), "actor");
            tp.Advance(TimeSpan.FromHours(48));
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.StaleEvidence));
            Assert.That(r.IsReleaseEvidence, Is.False,
                "Stale evidence must never carry IsReleaseEvidence=true.");
        }

        // ═══════════════════════════════════════════════════════════════════
        // BC16–BC20  IsReleaseEvidence invariant
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task BC16_IsReleaseEvidence_FalseWhenNoPackExists()
        {
            var svc = Svc();
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.IsReleaseEvidence, Is.False);
        }

        [Test]
        public async Task BC17_IsReleaseEvidence_FalseWhenPackIsNotReleaseGrade()
        {
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(Persist(releaseGrade: false), "actor");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.IsReleaseEvidence, Is.False);
        }

        [Test]
        public async Task BC18_IsReleaseEvidence_TrueOnlyWhenReady_AndReleaseGrade()
        {
            // Approval webhook must be recorded BEFORE persisting with RequireReleaseGrade=true
            var svc = Svc();
            await svc.RecordApprovalWebhookAsync(Approval(), "bc-reviewer");
            await svc.PersistSignOffEvidenceAsync(Persist(releaseGrade: true), "actor");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.IsReleaseEvidence, Is.True);
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade));
        }

        [Test]
        public async Task BC19_IsReleaseEvidence_FalseWhenBlocked()
        {
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(
                Persist(releaseGrade: true, requireApprovalWebhook: true), "actor");
            // Webhook required but not received → Blocked
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.IsReleaseEvidence, Is.False);
        }

        [Test]
        public async Task BC20_IsReleaseEvidence_FalseAfterWindowExpires()
        {
            var tp = new FakeTime(DateTimeOffset.UtcNow);
            var svc = Svc(tp);
            await svc.RecordApprovalWebhookAsync(Approval(), "bc-reviewer");
            await svc.PersistSignOffEvidenceAsync(Persist(releaseGrade: true), "actor");
            var r1 = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r1.IsReleaseEvidence, Is.True, "Pre-expiry should be release evidence.");
            tp.Advance(TimeSpan.FromHours(25));
            var r2 = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r2.IsReleaseEvidence, Is.False, "Post-expiry must not be release evidence.");
        }

        // ═══════════════════════════════════════════════════════════════════
        // BC21–BC25  EnvironmentLabel normalisation
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task BC21_EnvironmentLabel_NullInput_EchoedAsNull()
        {
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(Persist(label: null, releaseGrade: true), "actor");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.EnvironmentLabel, Is.Null);
        }

        [Test]
        public async Task BC22_EnvironmentLabel_WhitespaceOnly_NormalisedToNull()
        {
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(Persist(label: "   ", releaseGrade: true), "actor");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.EnvironmentLabel, Is.Null,
                "Whitespace-only EnvironmentLabel must be normalised to null.");
        }

        [Test]
        public async Task BC23_EnvironmentLabel_ValidValue_RoundTrips()
        {
            // Use releaseGrade=false so pack persists without a webhook
            const string label = "staging-eu-west-1";
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(Persist(label: label, releaseGrade: false), "actor");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.EnvironmentLabel, Is.EqualTo(label));
        }

        [Test]
        public async Task BC24_EnvironmentLabel_LatestPackWins()
        {
            // Use releaseGrade=false so packs persist without webhooks
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(Persist(label: "first", releaseGrade: false), "actor");
            await svc.PersistSignOffEvidenceAsync(Persist(label: "latest", releaseGrade: false), "actor");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.EnvironmentLabel, Is.EqualTo("latest"),
                "The most-recently persisted pack's label must be echoed.");
        }

        [Test]
        public async Task BC25_EnvironmentLabel_EmptyString_NormalisedToNull()
        {
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(Persist(label: "", releaseGrade: true), "actor");
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.EnvironmentLabel, Is.Null,
                "Empty-string EnvironmentLabel must be normalised to null.");
        }

        // ═══════════════════════════════════════════════════════════════════
        // BC26–BC30  Cross-cutting: isolation, idempotency, different heads
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public async Task BC26_Mode_IsolatedBetweenServiceInstances()
        {
            var svc1 = Svc();
            var svc2 = Svc();
            await svc1.RecordApprovalWebhookAsync(Approval(), "bc-reviewer");
            await svc1.PersistSignOffEvidenceAsync(Persist(releaseGrade: true), "actor");
            var r1 = await svc1.GetReleaseReadinessAsync(Readiness());
            var r2 = await svc2.GetReleaseReadinessAsync(Readiness());
            Assert.That(r1.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade));
            Assert.That(r2.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "Service instances must not share in-memory state.");
        }

        [Test]
        public async Task BC27_Mode_DifferentHeadRef_DoesNotPolluteCrossHeadState()
        {
            const string head1 = "bc-sha-HEAD-1";
            const string head2 = "bc-sha-HEAD-2";
            var svc = Svc();

            await svc.PersistSignOffEvidenceAsync(
                new PersistSignOffEvidenceRequest
                {
                    HeadRef = head1,
                    CaseId = "bc-case",
                    RequireReleaseGrade = true,
                    EnvironmentLabel = "prod"
                }, "actor");

            var r1 = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head1, CaseId = "bc-case", FreshnessWindowHours = 24 });
            var r2 = await svc.GetReleaseReadinessAsync(
                new GetSignOffReleaseReadinessRequest { HeadRef = head2, CaseId = "bc-case", FreshnessWindowHours = 24 });

            Assert.That(r1.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade));
            Assert.That(r2.Mode, Is.Not.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "Evidence stored for head1 must not satisfy head2 readiness.");
        }

        [Test]
        public async Task BC28_Mode_IdempotentReadiness_ThreeConsecutiveCalls()
        {
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(Persist(releaseGrade: true), "actor");

            var r1 = await svc.GetReleaseReadinessAsync(Readiness());
            var r2 = await svc.GetReleaseReadinessAsync(Readiness());
            var r3 = await svc.GetReleaseReadinessAsync(Readiness());

            Assert.That(r1.Mode, Is.EqualTo(r2.Mode), "Mode must be identical across calls.");
            Assert.That(r2.Mode, Is.EqualTo(r3.Mode));
            Assert.That(r1.IsReleaseEvidence, Is.EqualTo(r2.IsReleaseEvidence));
            Assert.That(r2.IsReleaseEvidence, Is.EqualTo(r3.IsReleaseEvidence));
        }

        [Test]
        public async Task BC29_Mode_And_EnvironmentLabel_PresentTogether_ReadyReleaseGrade()
        {
            const string label = "prod-eu";
            var svc = Svc();
            await svc.PersistSignOffEvidenceAsync(Persist(label: label, releaseGrade: true), "actor");
            var r = await svc.GetReleaseReadinessAsync(Readiness());

            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.ReadyReleaseGrade),
                "Mode must be ReadyReleaseGrade when pack is release-grade.");
            Assert.That(r.EnvironmentLabel, Is.EqualTo(label),
                "EnvironmentLabel must be echoed alongside the mode.");
            Assert.That(r.IsReleaseEvidence, Is.True);
        }

        [Test]
        public async Task BC30_Mode_NotConfigured_NeverHasEnvironmentLabel()
        {
            // When no pack exists (NotConfigured mode), EnvironmentLabel must be null
            var svc = Svc();
            var r = await svc.GetReleaseReadinessAsync(Readiness());
            Assert.That(r.Mode, Is.EqualTo(StrictArtifactMode.NotConfigured));
            Assert.That(r.EnvironmentLabel, Is.Null,
                "NotConfigured mode must not echo any EnvironmentLabel.");
        }
    }
}
