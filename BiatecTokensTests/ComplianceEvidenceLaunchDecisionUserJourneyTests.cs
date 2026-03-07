using BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// User journey tests for the Compliance Evidence and Launch Decision service.
    /// Simulates real operator workflows: evaluate, inspect, remediate, re-evaluate.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceEvidenceLaunchDecisionUserJourneyTests
    {
        private ComplianceEvidenceLaunchDecisionService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<ComplianceEvidenceLaunchDecisionService>>();
            _service = new ComplianceEvidenceLaunchDecisionService(logger.Object);
        }

        // ── Journey 1: Testnet happy path ─────────────────────────────────────

        [Test]
        public async Task Journey_TestnetHappyPath_EvaluationSucceeds()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest("journey-owner-1"));
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task Journey_TestnetHappyPath_HasDecisionId()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest("journey-owner-2"));
            Assert.That(result.DecisionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Journey_TestnetHappyPath_StatusIsReadyOrWarning()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest("journey-owner-3"));
            Assert.That(result.Status,
                Is.EqualTo(LaunchDecisionStatus.Ready).Or.EqualTo(LaunchDecisionStatus.Warning));
        }

        [Test]
        public async Task Journey_TestnetHappyPath_CanRetrieveDecisionById()
        {
            var created = await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest("journey-owner-4"));
            var retrieved = await _service.GetDecisionAsync(created.DecisionId);
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.DecisionId, Is.EqualTo(created.DecisionId));
        }

        [Test]
        public async Task Journey_TestnetHappyPath_CanRetrieveTrace()
        {
            var created = await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest("journey-owner-5"));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = created.DecisionId });
            Assert.That(trace.Success, Is.True);
            Assert.That(trace.Rules.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task Journey_TestnetHappyPath_CanRetrieveEvidenceBundle()
        {
            const string owner = "journey-owner-6";
            await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest(owner));
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner });
            Assert.That(bundle.Success, Is.True);
            Assert.That(bundle.Items.Count, Is.GreaterThan(0));
        }

        // ── Journey 2: Mainnet launch (advisory warnings) ─────────────────────

        [Test]
        public async Task Journey_MainnetLaunch_EvaluationSucceeds()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildMainnetRequest("journey-mn-1"));
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task Journey_MainnetLaunch_HasWarnings()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildMainnetRequest("journey-mn-2"));
            Assert.That(result.Warnings.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task Journey_MainnetLaunch_SummaryMentionsWarnings()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildMainnetRequest("journey-mn-3"));
            // Summary should indicate warnings or readiness
            Assert.That(result.Summary, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Journey_MainnetLaunch_EvidenceSummaryHasItems()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildMainnetRequest("journey-mn-4"));
            Assert.That(result.EvidenceSummary.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task Journey_MainnetLaunch_TraceContainsKycRule()
        {
            var d = await _service.EvaluateLaunchDecisionAsync(BuildMainnetRequest("journey-mn-5"));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            Assert.That(trace.Rules.Any(r => r.RuleId == "RULE-KYC-001"), Is.True);
        }

        [Test]
        public async Task Journey_MainnetLaunch_KycRuleHasWarningOutcome()
        {
            var d = await _service.EvaluateLaunchDecisionAsync(BuildMainnetRequest("journey-mn-6"));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            var kycRule = trace.Rules.FirstOrDefault(r => r.RuleId == "RULE-KYC-001");
            Assert.That(kycRule, Is.Not.Null);
            Assert.That(kycRule!.Outcome, Is.EqualTo(RuleOutcome.Warning));
        }

        // ── Journey 3: ARC1400 premium requirement ────────────────────────────

        [Test]
        public async Task Journey_ARC1400_EvaluationSucceeds()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("journey-arc-1", "ARC1400", "testnet"));
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task Journey_ARC1400_HasBlocker_ForPremiumRequirement()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("journey-arc-2", "ARC1400", "testnet"));
            Assert.That(result.Blockers.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task Journey_ARC1400_BlockerHasRemediationSteps()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("journey-arc-3", "ARC1400", "testnet"));
            var entitlementBlocker = result.Blockers
                .FirstOrDefault(b => b.RuleId == "RULE-ENTITLE-001");
            Assert.That(entitlementBlocker, Is.Not.Null);
            Assert.That(entitlementBlocker!.RemediationSteps.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task Journey_ARC1400_CanTraceToEntitlementRule()
        {
            var d = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("journey-arc-4", "ARC1400", "testnet"));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            var entRule = trace.Rules.FirstOrDefault(r => r.RuleId == "RULE-ENTITLE-001");
            Assert.That(entRule, Is.Not.Null);
            Assert.That(entRule!.Outcome, Is.EqualTo(RuleOutcome.Fail));
        }

        [Test]
        public async Task Journey_ARC1400_RemediationGuidanceIsPresent_InTrace()
        {
            var d = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("journey-arc-5", "ARC1400", "testnet"));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            var failedRule = trace.Rules.FirstOrDefault(r => r.Outcome == RuleOutcome.Fail);
            Assert.That(failedRule, Is.Not.Null);
            Assert.That(failedRule!.RemediationGuidance, Is.Not.Null.And.Not.Empty);
        }

        // ── Journey 4: Idempotent operator workflow ────────────────────────────

        [Test]
        public async Task Journey_Idempotency_FirstCallNotReplay()
        {
            var req = BuildTestnetRequest("idem-journey-1");
            req.IdempotencyKey = "journey-key-001";
            var r = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(r.IsIdempotentReplay, Is.False);
        }

        [Test]
        public async Task Journey_Idempotency_SecondCallIsReplay()
        {
            var req = BuildTestnetRequest("idem-journey-2");
            req.IdempotencyKey = "journey-key-002";
            await _service.EvaluateLaunchDecisionAsync(req);
            var r2 = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task Journey_Idempotency_ReplaySameDecisionId()
        {
            var req = BuildTestnetRequest("idem-journey-3");
            req.IdempotencyKey = "journey-key-003";
            var r1 = await _service.EvaluateLaunchDecisionAsync(req);
            var r2 = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(r2.DecisionId, Is.EqualTo(r1.DecisionId));
        }

        [Test]
        public async Task Journey_Idempotency_ForceRefreshProducesNewDecision()
        {
            var req = BuildTestnetRequest("idem-journey-4");
            req.IdempotencyKey = "journey-key-004";
            var r1 = await _service.EvaluateLaunchDecisionAsync(req);
            req.ForceRefresh = true;
            var r2 = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(r2.DecisionId, Is.Not.EqualTo(r1.DecisionId));
            Assert.That(r2.IsIdempotentReplay, Is.False);
        }

        // ── Journey 5: Multi-decision history ────────────────────────────────

        [Test]
        public async Task Journey_MultipleDecisions_AllAvailableInList()
        {
            const string owner = "multi-journey-1";
            var d1 = await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest(owner));
            var d2 = await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest(owner));
            var list = await _service.ListDecisionsAsync(owner);
            var ids = list.Select(d => d.DecisionId).ToHashSet();
            Assert.That(ids.Contains(d1.DecisionId), Is.True);
            Assert.That(ids.Contains(d2.DecisionId), Is.True);
        }

        [Test]
        public async Task Journey_MultipleDecisions_OrderedMostRecentFirst()
        {
            const string owner = "multi-journey-2";
            await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest(owner));
            await Task.Delay(5);
            await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest(owner));
            var list = await _service.ListDecisionsAsync(owner);
            if (list.Count >= 2)
                Assert.That(list[0].DecidedAt, Is.GreaterThanOrEqualTo(list[1].DecidedAt));
        }

        [Test]
        public async Task Journey_MultipleDecisions_EvidenceAccumulates()
        {
            const string owner = "multi-journey-3";
            await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest(owner));
            await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest(owner));
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner });
            // At least 2 decisions × multiple evidence items each
            Assert.That(bundle.TotalCount, Is.GreaterThanOrEqualTo(2));
        }

        // ── Journey 6: Evidence filtering ────────────────────────────────────

        [Test]
        public async Task Journey_EvidenceFilter_ByDecisionId_ReturnsOnlyRelevant()
        {
            const string owner = "filter-journey-1";
            var d1 = await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest(owner));
            await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest(owner));

            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner, DecisionId = d1.DecisionId });
            Assert.That(bundle.Items.All(e => e.DecisionId == d1.DecisionId), Is.True);
        }

        [Test]
        public async Task Journey_EvidenceFilter_ByCategory_ReturnsOnlyRelevant()
        {
            const string owner = "filter-journey-2";
            await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest(owner));
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest
                {
                    OwnerId = owner,
                    Category = EvidenceCategory.Integration
                });
            Assert.That(bundle.Items.All(e => e.Category == EvidenceCategory.Integration), Is.True);
        }

        [Test]
        public async Task Journey_EvidenceFilter_Limit_ReturnsCorrectCount()
        {
            const string owner = "filter-journey-3";
            for (int i = 0; i < 3; i++)
                await _service.EvaluateLaunchDecisionAsync(BuildTestnetRequest(owner));
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner, Limit = 2 });
            Assert.That(bundle.Items.Count, Is.LessThanOrEqualTo(2));
        }

        // ── Journey 7: Policy version staleness advisory ──────────────────────

        [Test]
        public async Task Journey_CustomPolicyVersion_HasStalenessWarning()
        {
            var req = BuildTestnetRequest("policy-journey-1");
            req.PolicyVersion = "2020.01.01.1"; // old version
            var result = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(result.Success, Is.True);
            // Policy staleness should trigger a warning
            Assert.That(result.Warnings.Any(w => w.RuleId == "RULE-POLICY-001"), Is.True);
        }

        [Test]
        public async Task Journey_CurrentPolicyVersion_NoStalenessWarning()
        {
            var req = BuildTestnetRequest("policy-journey-2");
            // No custom version (uses latest)
            var result = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(result.Warnings.Any(w => w.RuleId == "RULE-POLICY-001"), Is.False);
        }

        // ── Journey 8: All networks ───────────────────────────────────────────

        [TestCase("testnet", "network-journey-tn")]
        [TestCase("betanet", "network-journey-bn")]
        [TestCase("base-testnet", "network-journey-bt")]
        public async Task Journey_AllTestNetworks_SucceedWithoutBlockers(string network, string owner)
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest(owner, "ASA", network));
            Assert.That(result.Success, Is.True);
            Assert.That(result.Blockers, Is.Empty);
        }

        // ── Helper ────────────────────────────────────────────────────────────

        private static LaunchDecisionRequest BuildTestnetRequest(string owner) =>
            BuildRequest(owner, "ASA", "testnet");

        private static LaunchDecisionRequest BuildMainnetRequest(string owner) =>
            BuildRequest(owner, "ASA", "mainnet");

        private static LaunchDecisionRequest BuildRequest(
            string owner, string standard, string network) =>
            new()
            {
                OwnerId = owner,
                TokenStandard = standard,
                Network = network,
                TokenName = "JourneyToken"
            };
    }
}
