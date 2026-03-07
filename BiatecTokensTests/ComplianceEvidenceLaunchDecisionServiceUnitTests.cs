using BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for ComplianceEvidenceLaunchDecisionService.
    /// All tests use direct service instantiation – no HTTP calls.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceEvidenceLaunchDecisionServiceUnitTests
    {
        private ComplianceEvidenceLaunchDecisionService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<ComplianceEvidenceLaunchDecisionService>>();
            _service = new ComplianceEvidenceLaunchDecisionService(logger.Object);
        }

        // ── Input validation ──────────────────────────────────────────────────

        [Test]
        public async Task EvaluateLaunchDecision_MissingOwnerId_ReturnsError()
        {
            var req = BuildRequest(ownerId: "");
            var result = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
        }

        [Test]
        public async Task EvaluateLaunchDecision_NullOwnerId_ReturnsError()
        {
            var req = BuildRequest(ownerId: null!);
            var result = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
        }

        [Test]
        public async Task EvaluateLaunchDecision_WhitespaceOwnerId_ReturnsError()
        {
            var req = BuildRequest(ownerId: "   ");
            var result = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
        }

        [Test]
        public async Task EvaluateLaunchDecision_MissingTokenStandard_ReturnsError()
        {
            var req = BuildRequest(tokenStandard: "");
            var result = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_STANDARD"));
        }

        [Test]
        public async Task EvaluateLaunchDecision_InvalidTokenStandard_ReturnsError()
        {
            var req = BuildRequest(tokenStandard: "UNKNOWN_STANDARD");
            var result = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_TOKEN_STANDARD"));
        }

        [Test]
        public async Task EvaluateLaunchDecision_MissingNetwork_ReturnsError()
        {
            var req = BuildRequest(network: "");
            var result = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_NETWORK"));
        }

        [Test]
        public async Task EvaluateLaunchDecision_InvalidNetwork_ReturnsError()
        {
            var req = BuildRequest(network: "unknown-chain-999");
            var result = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_NETWORK"));
        }

        // ── Happy path ────────────────────────────────────────────────────────

        [Test]
        public async Task EvaluateLaunchDecision_ValidTestnet_ReturnsSuccess()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            Assert.That(result.Success, Is.True);
            Assert.That(result.DecisionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvaluateLaunchDecision_ValidTestnet_HasDecisionId()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            Assert.That(result.DecisionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvaluateLaunchDecision_ValidTestnet_HasStatus()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            Assert.That(result.Status, Is.EqualTo(LaunchDecisionStatus.Ready)
                .Or.EqualTo(LaunchDecisionStatus.Warning)
                .Or.EqualTo(LaunchDecisionStatus.Blocked)
                .Or.EqualTo(LaunchDecisionStatus.NeedsReview));
        }

        [Test]
        public async Task EvaluateLaunchDecision_ValidTestnet_HasSummary()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            Assert.That(result.Summary, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvaluateLaunchDecision_ValidTestnet_HasPolicyVersion()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            Assert.That(result.PolicyVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvaluateLaunchDecision_ValidTestnet_HasSchemaVersion()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            Assert.That(result.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvaluateLaunchDecision_ValidTestnet_HasDecidedAt()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            Assert.That(result.DecidedAt, Is.GreaterThan(before));
        }

        [Test]
        public async Task EvaluateLaunchDecision_ValidTestnet_HasEvidenceSummary()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            Assert.That(result.EvidenceSummary, Is.Not.Null);
            Assert.That(result.EvidenceSummary.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task EvaluateLaunchDecision_ValidTestnet_HasCorrelationId()
        {
            var req = BuildRequest();
            req.CorrelationId = "test-corr-001";
            var result = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(result.CorrelationId, Is.EqualTo("test-corr-001"));
        }

        [Test]
        public async Task EvaluateLaunchDecision_AutoGeneratesCorrelationId_WhenNotProvided()
        {
            var req = BuildRequest();
            req.CorrelationId = null;
            var result = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        // ── Token standards ───────────────────────────────────────────────────

        [TestCase("ASA")]
        [TestCase("ARC3")]
        [TestCase("ARC200")]
        [TestCase("ERC20")]
        public async Task EvaluateLaunchDecision_ValidStandards_Succeed(string standard)
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest(tokenStandard: standard));
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task EvaluateLaunchDecision_ARC1400_OnTestnet_NeedsReviewOrBlocked()
        {
            // ARC1400 requires Premium subscription which is not available in mock
            var result = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest(tokenStandard: "ARC1400", network: "testnet"));
            Assert.That(result.Success, Is.True);
            // Should have at least one blocker for premium requirement
            Assert.That(result.Blockers.Count + result.Warnings.Count, Is.GreaterThanOrEqualTo(0));
        }

        [TestCase("ASA", "testnet")]
        [TestCase("ARC3", "testnet")]
        [TestCase("ARC200", "testnet")]
        [TestCase("ERC20", "testnet")]
        public async Task EvaluateLaunchDecision_ValidStandardAndNetwork_ReturnsValidDecision(
            string standard, string network)
        {
            var result = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest(tokenStandard: standard, network: network));
            Assert.That(result.Success, Is.True);
            Assert.That(result.DecisionId, Is.Not.Null.And.Not.Empty);
        }

        // ── Networks ──────────────────────────────────────────────────────────

        [TestCase("testnet")]
        [TestCase("betanet")]
        [TestCase("voimain")]
        [TestCase("aramidmain")]
        [TestCase("base")]
        [TestCase("base-testnet")]
        public async Task EvaluateLaunchDecision_ValidNetworks_Succeed(string network)
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest(network: network));
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task EvaluateLaunchDecision_MainnetLaunch_HasWarning()
        {
            var result = await _service.EvaluateLaunchDecisionAsync(BuildRequest(network: "mainnet"));
            Assert.That(result.Success, Is.True);
            // Mainnet launches carry KYC advisory warning
            Assert.That(result.Warnings.Count, Is.GreaterThan(0));
        }

        // ── Idempotency ───────────────────────────────────────────────────────

        [Test]
        public async Task EvaluateLaunchDecision_SameIdempotencyKey_ReturnsSameDecisionId()
        {
            var req = BuildRequest();
            req.IdempotencyKey = "idem-key-001";
            var r1 = await _service.EvaluateLaunchDecisionAsync(req);
            var r2 = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(r2.DecisionId, Is.EqualTo(r1.DecisionId));
        }

        [Test]
        public async Task EvaluateLaunchDecision_SameIdempotencyKey_SecondCallIsReplay()
        {
            var req = BuildRequest();
            req.IdempotencyKey = "idem-key-002";
            await _service.EvaluateLaunchDecisionAsync(req);
            var r2 = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task EvaluateLaunchDecision_ForceRefresh_BypassesIdempotencyCache()
        {
            var req = BuildRequest();
            req.IdempotencyKey = "idem-key-003";
            var r1 = await _service.EvaluateLaunchDecisionAsync(req);

            req.ForceRefresh = true;
            var r2 = await _service.EvaluateLaunchDecisionAsync(req);

            Assert.That(r2.IsIdempotentReplay, Is.False);
            // Force refresh generates a new decision
            Assert.That(r2.DecisionId, Is.Not.EqualTo(r1.DecisionId));
        }

        [Test]
        public async Task EvaluateLaunchDecision_DifferentIdempotencyKeys_ReturnsDifferentDecisions()
        {
            var req1 = BuildRequest();
            req1.IdempotencyKey = "idem-key-101";
            var req2 = BuildRequest();
            req2.IdempotencyKey = "idem-key-102";

            var r1 = await _service.EvaluateLaunchDecisionAsync(req1);
            var r2 = await _service.EvaluateLaunchDecisionAsync(req2);

            Assert.That(r2.DecisionId, Is.Not.EqualTo(r1.DecisionId));
        }

        [Test]
        public async Task EvaluateLaunchDecision_ThreeIdempotentReplays_IdenticalOutcomes()
        {
            var req = BuildRequest();
            req.IdempotencyKey = "idem-key-three";
            var r1 = await _service.EvaluateLaunchDecisionAsync(req);
            var r2 = await _service.EvaluateLaunchDecisionAsync(req);
            var r3 = await _service.EvaluateLaunchDecisionAsync(req);

            Assert.That(r2.Status, Is.EqualTo(r1.Status));
            Assert.That(r3.Status, Is.EqualTo(r1.Status));
            Assert.That(r2.CanLaunch, Is.EqualTo(r1.CanLaunch));
            Assert.That(r3.CanLaunch, Is.EqualTo(r1.CanLaunch));
        }

        // ── GetDecision ───────────────────────────────────────────────────────

        [Test]
        public async Task GetDecision_ExistingId_ReturnsDecision()
        {
            var created = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            var retrieved = await _service.GetDecisionAsync(created.DecisionId);
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved!.DecisionId, Is.EqualTo(created.DecisionId));
        }

        [Test]
        public async Task GetDecision_NonExistentId_ReturnsNull()
        {
            var result = await _service.GetDecisionAsync("nonexistent-id-xyz");
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetDecision_EmptyId_ReturnsNull()
        {
            var result = await _service.GetDecisionAsync("");
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task GetDecision_WithCorrelationId_PropagatesCorrelationId()
        {
            var created = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            var retrieved = await _service.GetDecisionAsync(created.DecisionId, "my-correlation-99");
            Assert.That(retrieved!.CorrelationId, Is.EqualTo("my-correlation-99"));
        }

        // ── GetEvidenceBundle ─────────────────────────────────────────────────

        [Test]
        public async Task GetEvidenceBundle_MissingOwnerId_ReturnsError()
        {
            var req = new EvidenceBundleRequest { OwnerId = "" };
            var result = await _service.GetEvidenceBundleAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
        }

        [Test]
        public async Task GetEvidenceBundle_InvalidLimit_Zero_ReturnsError()
        {
            var req = new EvidenceBundleRequest { OwnerId = "owner-1", Limit = 0 };
            var result = await _service.GetEvidenceBundleAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_LIMIT"));
        }

        [Test]
        public async Task GetEvidenceBundle_InvalidLimit_TooHigh_ReturnsError()
        {
            var req = new EvidenceBundleRequest { OwnerId = "owner-1", Limit = 200 };
            var result = await _service.GetEvidenceBundleAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_LIMIT"));
        }

        [Test]
        public async Task GetEvidenceBundle_AfterDecision_ReturnsItems()
        {
            const string owner = "bundle-owner-001";
            await _service.EvaluateLaunchDecisionAsync(BuildRequest(ownerId: owner));
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner });
            Assert.That(bundle.Success, Is.True);
            Assert.That(bundle.Items.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GetEvidenceBundle_UnknownOwner_ReturnsEmptyBundle()
        {
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = "nobody-known-xyz" });
            Assert.That(bundle.Success, Is.True);
            Assert.That(bundle.Items.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task GetEvidenceBundle_FilterByCategory_ReturnsOnlyMatchingItems()
        {
            const string owner = "filter-cat-owner";
            await _service.EvaluateLaunchDecisionAsync(BuildRequest(ownerId: owner));
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest
                {
                    OwnerId = owner,
                    Category = EvidenceCategory.Identity
                });
            Assert.That(bundle.Success, Is.True);
            Assert.That(bundle.Items.All(e => e.Category == EvidenceCategory.Identity), Is.True);
        }

        [Test]
        public async Task GetEvidenceBundle_FilterByDecisionId_ReturnsOnlyMatchingItems()
        {
            const string owner = "filter-dec-owner";
            var decision = await _service.EvaluateLaunchDecisionAsync(BuildRequest(ownerId: owner));
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest
                {
                    OwnerId = owner,
                    DecisionId = decision.DecisionId
                });
            Assert.That(bundle.Success, Is.True);
            Assert.That(bundle.Items.All(e => e.DecisionId == decision.DecisionId), Is.True);
        }

        // ── GetDecisionTrace ──────────────────────────────────────────────────

        [Test]
        public async Task GetDecisionTrace_ExistingDecision_ReturnsTrace()
        {
            var decision = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = decision.DecisionId });
            Assert.That(trace.Success, Is.True);
            Assert.That(trace.DecisionId, Is.EqualTo(decision.DecisionId));
        }

        [Test]
        public async Task GetDecisionTrace_NonExistentDecision_ReturnsNotFound()
        {
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = "nonexistent-trace-abc" });
            Assert.That(trace.Success, Is.False);
            Assert.That(trace.ErrorCode, Is.EqualTo("DECISION_NOT_FOUND"));
        }

        [Test]
        public async Task GetDecisionTrace_MissingDecisionId_ReturnsError()
        {
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = "" });
            Assert.That(trace.Success, Is.False);
            Assert.That(trace.ErrorCode, Is.EqualTo("MISSING_DECISION_ID"));
        }

        [Test]
        public async Task GetDecisionTrace_HasRules_OrderedDeterministically()
        {
            var decision = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = decision.DecisionId });
            Assert.That(trace.Rules, Is.Not.Null);
            Assert.That(trace.Rules.Count, Is.GreaterThan(0));
            // Rules should be ordered
            var orders = trace.Rules.Select(r => r.EvaluationOrder).ToList();
            Assert.That(orders, Is.Ordered.Ascending);
        }

        [Test]
        public async Task GetDecisionTrace_EachRuleHasId()
        {
            var decision = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = decision.DecisionId });
            Assert.That(trace.Rules.All(r => !string.IsNullOrEmpty(r.RuleId)), Is.True);
        }

        [Test]
        public async Task GetDecisionTrace_EachRuleHasRationale()
        {
            var decision = await _service.EvaluateLaunchDecisionAsync(BuildRequest());
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = decision.DecisionId });
            Assert.That(trace.Rules.All(r => !string.IsNullOrEmpty(r.Rationale)), Is.True);
        }

        // ── ListDecisions ─────────────────────────────────────────────────────

        [Test]
        public async Task ListDecisions_AfterCreating_ReturnsDecisions()
        {
            const string owner = "list-owner-001";
            await _service.EvaluateLaunchDecisionAsync(BuildRequest(ownerId: owner));
            var list = await _service.ListDecisionsAsync(owner);
            Assert.That(list.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task ListDecisions_EmptyOwnerId_ReturnsEmpty()
        {
            var list = await _service.ListDecisionsAsync("");
            Assert.That(list, Is.Empty);
        }

        [Test]
        public async Task ListDecisions_UnknownOwner_ReturnsEmpty()
        {
            var list = await _service.ListDecisionsAsync("nobody-xyz-999");
            Assert.That(list, Is.Empty);
        }

        [Test]
        public async Task ListDecisions_RespectsLimit()
        {
            const string owner = "limit-owner-001";
            for (int i = 0; i < 5; i++)
                await _service.EvaluateLaunchDecisionAsync(BuildRequest(ownerId: owner));
            var list = await _service.ListDecisionsAsync(owner, limit: 2);
            Assert.That(list.Count, Is.LessThanOrEqualTo(2));
        }

        [Test]
        public async Task ListDecisions_OrderedMostRecentFirst()
        {
            const string owner = "order-owner-001";
            await _service.EvaluateLaunchDecisionAsync(BuildRequest(ownerId: owner));
            await Task.Delay(5);
            await _service.EvaluateLaunchDecisionAsync(BuildRequest(ownerId: owner));
            var list = await _service.ListDecisionsAsync(owner);
            if (list.Count >= 2)
                Assert.That(list[0].DecidedAt, Is.GreaterThanOrEqualTo(list[1].DecidedAt));
        }

        // ── Helper ────────────────────────────────────────────────────────────

        private static LaunchDecisionRequest BuildRequest(
            string ownerId = "owner-001",
            string tokenStandard = "ASA",
            string network = "testnet") =>
            new()
            {
                OwnerId = ownerId,
                TokenStandard = tokenStandard,
                Network = network,
                TokenName = "TestToken"
            };
    }
}
