using BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// E2E workflow tests for the Compliance Evidence and Launch Decision service.
    /// Covers full lifecycle: evaluate → trace → evidence → re-evaluate.
    /// Tests idempotency determinism, observability metadata, and multi-step audit trails.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceEvidenceLaunchDecisionE2EWorkflowTests
    {
        private ComplianceEvidenceLaunchDecisionService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<ComplianceEvidenceLaunchDecisionService>>();
            _service = new ComplianceEvidenceLaunchDecisionService(logger.Object);
        }

        // ── E2E 1: Full evaluation lifecycle ─────────────────────────────────

        [Test]
        public async Task E2E_FullLifecycle_EvaluateTraceEvidence()
        {
            const string owner = "e2e-owner-001";
            // Step 1: Evaluate
            var decision = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = owner,
                TokenStandard = "ASA",
                Network = "testnet",
                CorrelationId = "e2e-corr-001"
            });
            Assert.That(decision.Success, Is.True);
            Assert.That(decision.DecisionId, Is.Not.Null.And.Not.Empty);

            // Step 2: Retrieve decision
            var retrieved = await _service.GetDecisionAsync(decision.DecisionId);
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.DecisionId, Is.EqualTo(decision.DecisionId));

            // Step 3: Get trace
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = decision.DecisionId });
            Assert.That(trace.Success, Is.True);
            Assert.That(trace.Rules.Count, Is.GreaterThan(0));

            // Step 4: Get evidence bundle
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner });
            Assert.That(bundle.Success, Is.True);
            Assert.That(bundle.Items.Count, Is.GreaterThan(0));
        }

        // ── E2E 2: Idempotent evaluation determinism (3 runs) ─────────────────

        [Test]
        public async Task E2E_IdempotentEvaluation_ThreeRuns_IdenticalOutcomes()
        {
            var req = new LaunchDecisionRequest
            {
                OwnerId = "e2e-idem-owner",
                TokenStandard = "ARC3",
                Network = "testnet",
                IdempotencyKey = "e2e-idem-key-three-runs"
            };
            var r1 = await _service.EvaluateLaunchDecisionAsync(req);
            var r2 = await _service.EvaluateLaunchDecisionAsync(req);
            var r3 = await _service.EvaluateLaunchDecisionAsync(req);

            Assert.That(r1.DecisionId, Is.EqualTo(r2.DecisionId));
            Assert.That(r1.DecisionId, Is.EqualTo(r3.DecisionId));
            Assert.That(r1.Status, Is.EqualTo(r2.Status));
            Assert.That(r1.Status, Is.EqualTo(r3.Status));
            Assert.That(r1.CanLaunch, Is.EqualTo(r2.CanLaunch));
            Assert.That(r1.CanLaunch, Is.EqualTo(r3.CanLaunch));
            Assert.That(r2.IsIdempotentReplay, Is.True);
            Assert.That(r3.IsIdempotentReplay, Is.True);
        }

        // ── E2E 3: Correlation ID propagation ────────────────────────────────

        [Test]
        public async Task E2E_CorrelationId_PropagatedThroughDecision()
        {
            const string corrId = "e2e-correlation-propagation-test";
            var decision = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = "e2e-corr-owner",
                TokenStandard = "ASA",
                Network = "testnet",
                CorrelationId = corrId
            });
            Assert.That(decision.CorrelationId, Is.EqualTo(corrId));
        }

        [Test]
        public async Task E2E_CorrelationId_PropagatedToTrace()
        {
            const string corrId = "e2e-corr-trace-test";
            var decision = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = "e2e-corr-trace-owner",
                TokenStandard = "ASA",
                Network = "testnet",
                CorrelationId = corrId
            });
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest
                {
                    DecisionId = decision.DecisionId,
                    CorrelationId = corrId
                });
            Assert.That(trace.CorrelationId, Is.EqualTo(corrId));
        }

        [Test]
        public async Task E2E_CorrelationId_PropagatedToEvidenceBundle()
        {
            const string corrId = "e2e-corr-bundle-test";
            const string owner = "e2e-corr-bundle-owner";
            await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = owner,
                TokenStandard = "ASA",
                Network = "testnet",
                CorrelationId = corrId
            });
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner, CorrelationId = corrId });
            Assert.That(bundle.CorrelationId, Is.EqualTo(corrId));
        }

        // ── E2E 4: Multi-step audit trail ─────────────────────────────────────

        [Test]
        public async Task E2E_MultiStep_AuditTrail_EvidenceAccumulates()
        {
            const string owner = "e2e-audit-owner";
            // Simulate three separate evaluations (e.g., day 1, day 7, day 14 checks)
            await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = owner, TokenStandard = "ASA", Network = "testnet"
            });
            await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = owner, TokenStandard = "ARC3", Network = "testnet"
            });
            await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = owner, TokenStandard = "ARC200", Network = "testnet"
            });

            var list = await _service.ListDecisionsAsync(owner);
            Assert.That(list.Count, Is.EqualTo(3));

            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner, Limit = 100 });
            Assert.That(bundle.TotalCount, Is.GreaterThan(3));
        }

        // ── E2E 5: Blocked launch trace analysis ──────────────────────────────

        [Test]
        public async Task E2E_BlockedLaunch_TraceShowsFailedRule()
        {
            var decision = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = "e2e-blocked-owner",
                TokenStandard = "ARC1400",
                Network = "testnet"
            });

            Assert.That(decision.Blockers.Count, Is.GreaterThan(0));

            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = decision.DecisionId });

            var failedRules = trace.Rules.Where(r => r.Outcome == RuleOutcome.Fail).ToList();
            Assert.That(failedRules.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task E2E_BlockedLaunch_BlockerCanBeTracedToRule()
        {
            var decision = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = "e2e-blocked-trace-owner",
                TokenStandard = "ARC1400",
                Network = "testnet"
            });

            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = decision.DecisionId });

            // Each blocker's RuleId should appear as a failed rule in the trace
            foreach (var blocker in decision.Blockers)
            {
                var matchingRule = trace.Rules.FirstOrDefault(r => r.RuleId == blocker.RuleId);
                Assert.That(matchingRule, Is.Not.Null,
                    $"Blocker {blocker.BlockerId} references RuleId {blocker.RuleId} but no matching trace rule found.");
                Assert.That(matchingRule!.Outcome, Is.EqualTo(RuleOutcome.Fail));
            }
        }

        // ── E2E 6: Warning-only launch trace analysis ─────────────────────────

        [Test]
        public async Task E2E_WarningLaunch_TraceShowsWarningRules()
        {
            var decision = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = "e2e-warning-owner",
                TokenStandard = "ASA",
                Network = "mainnet"
            });

            Assert.That(decision.Warnings.Count, Is.GreaterThan(0));

            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = decision.DecisionId });

            var warningRules = trace.Rules.Where(r => r.Outcome == RuleOutcome.Warning).ToList();
            Assert.That(warningRules.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task E2E_WarningLaunch_WarningCanBeTracedToRule()
        {
            var decision = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = "e2e-warning-trace-owner",
                TokenStandard = "ASA",
                Network = "mainnet"
            });

            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = decision.DecisionId });

            foreach (var warning in decision.Warnings)
            {
                var matchingRule = trace.Rules.FirstOrDefault(r => r.RuleId == warning.RuleId);
                Assert.That(matchingRule, Is.Not.Null,
                    $"Warning {warning.WarningId} references RuleId {warning.RuleId} but no matching trace rule found.");
                Assert.That(matchingRule!.Outcome, Is.EqualTo(RuleOutcome.Warning));
            }
        }

        // ── E2E 7: Evidence integrity ─────────────────────────────────────────

        [Test]
        public async Task E2E_EvidenceItems_AllHaveTimestamps()
        {
            const string owner = "e2e-evidence-ts-owner";
            await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = owner, TokenStandard = "ASA", Network = "testnet"
            });
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner });
            Assert.That(bundle.Items.All(e => e.Timestamp > DateTime.MinValue), Is.True);
        }

        [Test]
        public async Task E2E_EvidenceItems_AllHaveSource()
        {
            const string owner = "e2e-evidence-src-owner";
            await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = owner, TokenStandard = "ASA", Network = "testnet"
            });
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner });
            Assert.That(bundle.Items.All(e => !string.IsNullOrEmpty(e.Source)), Is.True);
        }

        [Test]
        public async Task E2E_EvidenceItems_OwnerIdentityItemHasDataHash()
        {
            const string owner = "e2e-evidence-hash-owner";
            await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = owner, TokenStandard = "ASA", Network = "testnet"
            });
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner, Category = EvidenceCategory.Identity });
            // The owner identity item should have a data hash; KYC item may not
            var identityItemWithHash = bundle.Items.FirstOrDefault(e =>
                e.Category == EvidenceCategory.Identity && e.DataHash != null);
            Assert.That(identityItemWithHash, Is.Not.Null,
                "At least one Identity evidence item should have a DataHash.");
        }

        // ── E2E 8: Observability metadata ────────────────────────────────────

        [Test]
        public async Task E2E_Decision_HasEvaluationTimeMs()
        {
            var decision = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = "e2e-obs-owner",
                TokenStandard = "ASA",
                Network = "testnet"
            });
            Assert.That(decision.EvaluationTimeMs, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task E2E_Trace_HasEvaluationTimeMs()
        {
            var decision = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = "e2e-obs-trace-owner",
                TokenStandard = "ASA",
                Network = "testnet"
            });
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = decision.DecisionId });
            Assert.That(trace.EvaluationTimeMs, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task E2E_Trace_EachRuleHasDurationMs()
        {
            var decision = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = "e2e-obs-rule-owner",
                TokenStandard = "ASA",
                Network = "testnet"
            });
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = decision.DecisionId });
            Assert.That(trace.Rules.All(r => r.DurationMs >= 0), Is.True);
        }

        // ── E2E 9: Decision list as history ───────────────────────────────────

        [Test]
        public async Task E2E_DecisionHistory_AllDecisionsListedForOwner()
        {
            const string owner = "e2e-hist-owner";
            var ids = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var d = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
                {
                    OwnerId = owner,
                    TokenStandard = "ASA",
                    Network = "testnet"
                });
                ids.Add(d.DecisionId);
            }

            var list = await _service.ListDecisionsAsync(owner);
            var listedIds = list.Select(d => d.DecisionId).ToHashSet();
            foreach (var id in ids)
                Assert.That(listedIds.Contains(id), Is.True);
        }

        [Test]
        public async Task E2E_DecisionHistory_DoesNotCrossContaminateOwners()
        {
            const string owner1 = "e2e-cross-owner-1";
            const string owner2 = "e2e-cross-owner-2";

            var d1 = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = owner1, TokenStandard = "ASA", Network = "testnet"
            });
            var d2 = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = owner2, TokenStandard = "ARC3", Network = "testnet"
            });

            var list1 = await _service.ListDecisionsAsync(owner1);
            var list2 = await _service.ListDecisionsAsync(owner2);

            Assert.That(list1.Any(d => d.DecisionId == d2.DecisionId), Is.False);
            Assert.That(list2.Any(d => d.DecisionId == d1.DecisionId), Is.False);
        }
    }
}
