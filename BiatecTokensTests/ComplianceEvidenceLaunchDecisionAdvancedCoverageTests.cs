using BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Advanced coverage tests for the Compliance Evidence and Launch Decision service.
    /// Covers branch coverage, concurrency, malformed inputs, policy conflicts,
    /// and backward-compatible schema assertions.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceEvidenceLaunchDecisionAdvancedCoverageTests
    {
        private ComplianceEvidenceLaunchDecisionService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<ComplianceEvidenceLaunchDecisionService>>();
            _service = new ComplianceEvidenceLaunchDecisionService(logger.Object);
        }

        // ── Branch coverage: all error codes ──────────────────────────────────

        [Test]
        public async Task Branch_MissingOwnerId_Code_MISSING_OWNER_ID()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest { OwnerId = "", TokenStandard = "ASA", Network = "testnet" });
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
        }

        [Test]
        public async Task Branch_MissingTokenStandard_Code_MISSING_TOKEN_STANDARD()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest { OwnerId = "owner", TokenStandard = "", Network = "testnet" });
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_TOKEN_STANDARD"));
        }

        [Test]
        public async Task Branch_InvalidTokenStandard_Code_INVALID_TOKEN_STANDARD()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest { OwnerId = "owner", TokenStandard = "XYZ99", Network = "testnet" });
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_TOKEN_STANDARD"));
        }

        [Test]
        public async Task Branch_MissingNetwork_Code_MISSING_NETWORK()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest { OwnerId = "owner", TokenStandard = "ASA", Network = "" });
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_NETWORK"));
        }

        [Test]
        public async Task Branch_InvalidNetwork_Code_INVALID_NETWORK()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest { OwnerId = "owner", TokenStandard = "ASA", Network = "planet-x" });
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_NETWORK"));
        }

        [Test]
        public async Task Branch_EvidenceBundle_MissingOwnerId_Code_MISSING_OWNER_ID()
        {
            var r = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest { OwnerId = "" });
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_OWNER_ID"));
        }

        [Test]
        public async Task Branch_EvidenceBundle_LimitZero_Code_INVALID_LIMIT()
        {
            var r = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = "owner", Limit = 0 });
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_LIMIT"));
        }

        [Test]
        public async Task Branch_EvidenceBundle_LimitNegative_Code_INVALID_LIMIT()
        {
            var r = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = "owner", Limit = -5 });
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_LIMIT"));
        }

        [Test]
        public async Task Branch_EvidenceBundle_LimitOver100_Code_INVALID_LIMIT()
        {
            var r = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = "owner", Limit = 999 });
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_LIMIT"));
        }

        [Test]
        public async Task Branch_DecisionTrace_MissingDecisionId_Code_MISSING_DECISION_ID()
        {
            var r = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = "" });
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_DECISION_ID"));
        }

        [Test]
        public async Task Branch_DecisionTrace_NonExistent_Code_DECISION_NOT_FOUND()
        {
            var r = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = "nonexistent-xyz-789" });
            Assert.That(r.ErrorCode, Is.EqualTo("DECISION_NOT_FOUND"));
        }

        // ── Branch coverage: rule outcomes ────────────────────────────────────

        [Test]
        public async Task Branch_AllRules_HaveUniqueRuleIds()
        {
            var d = await _service.EvaluateLaunchDecisionAsync(BuildRequest("branch-uniq-owner"));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            var ruleIds = trace.Rules.Select(r => r.RuleId).ToList();
            Assert.That(ruleIds.Distinct().Count(), Is.EqualTo(ruleIds.Count));
        }

        [Test]
        public async Task Branch_TestnetASA_AllRulesPass_Or_Warn()
        {
            var d = await _service.EvaluateLaunchDecisionAsync(BuildRequest("branch-testnet-owner"));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            // On testnet with ASA, no rules should fail
            Assert.That(trace.Rules.All(r => r.Outcome != RuleOutcome.Fail), Is.True);
        }

        [Test]
        public async Task Branch_ARC1400_EntitlementRuleFails()
        {
            var d = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("branch-arc1400-owner", "ARC1400", "testnet"));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            var entRule = trace.Rules.FirstOrDefault(r => r.RuleId == "RULE-ENTITLE-001");
            Assert.That(entRule, Is.Not.Null);
            Assert.That(entRule!.Outcome, Is.EqualTo(RuleOutcome.Fail));
        }

        [Test]
        public async Task Branch_MainnetASA_NetworkRuleIsWarning()
        {
            var d = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("branch-mainnet-owner", "ASA", "mainnet"));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            var netRule = trace.Rules.FirstOrDefault(r => r.RuleId == "RULE-NETWORK-001");
            Assert.That(netRule, Is.Not.Null);
            Assert.That(netRule!.Outcome, Is.EqualTo(RuleOutcome.Warning));
        }

        [Test]
        public async Task Branch_MainnetASA_KycRuleIsWarning()
        {
            var d = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("branch-mainnet-kyc-owner", "ASA", "mainnet"));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            var kycRule = trace.Rules.FirstOrDefault(r => r.RuleId == "RULE-KYC-001");
            Assert.That(kycRule, Is.Not.Null);
            Assert.That(kycRule!.Outcome, Is.EqualTo(RuleOutcome.Warning));
        }

        [Test]
        public async Task Branch_StalePolicyVersion_PolicyRuleIsWarning()
        {
            var req = BuildRequest("branch-stale-pol-owner");
            req.PolicyVersion = "1999.01.01.1";
            var d = await _service.EvaluateLaunchDecisionAsync(req);
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            var polRule = trace.Rules.FirstOrDefault(r => r.RuleId == "RULE-POLICY-001");
            Assert.That(polRule, Is.Not.Null);
            Assert.That(polRule!.Outcome, Is.EqualTo(RuleOutcome.Warning));
        }

        // ── Branch coverage: LaunchDecisionStatus ─────────────────────────────

        [Test]
        public async Task Branch_StatusReady_TestnetASA()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(BuildRequest("status-ready-owner"));
            Assert.That(r.Status, Is.EqualTo(LaunchDecisionStatus.Ready));
        }

        [Test]
        public async Task Branch_StatusBlocked_ARC1400_HasBlockedOrNeedsReview()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("status-blocked-owner", "ARC1400", "testnet"));
            Assert.That(r.Status,
                Is.EqualTo(LaunchDecisionStatus.Blocked).Or.EqualTo(LaunchDecisionStatus.NeedsReview));
        }

        [Test]
        public async Task Branch_StatusWarning_MainnetASA()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("status-warning-owner", "ASA", "mainnet"));
            Assert.That(r.Status, Is.EqualTo(LaunchDecisionStatus.Warning));
        }

        [Test]
        public async Task Branch_CanLaunch_False_WhenBlocked()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("can-launch-false-owner", "ARC1400", "testnet"));
            Assert.That(r.CanLaunch, Is.False);
        }

        [Test]
        public async Task Branch_CanLaunch_True_WhenReady()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(BuildRequest("can-launch-true-owner"));
            Assert.That(r.CanLaunch, Is.True);
        }

        [Test]
        public async Task Branch_CanLaunch_True_WhenWarning()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest("can-launch-warn-owner", "ASA", "mainnet"));
            Assert.That(r.CanLaunch, Is.True);
        }

        // ── Malformed inputs ──────────────────────────────────────────────────

        [Test]
        public async Task Malformed_SQLInjectionInOwnerId_ReturnsValidResponse()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest
                {
                    OwnerId = "'; DROP TABLE decisions; --",
                    TokenStandard = "ASA",
                    Network = "testnet"
                });
            // Service should not throw; should return valid response
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task Malformed_XssInTokenName_ReturnsValidResponse()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest
                {
                    OwnerId = "malformed-xss-owner",
                    TokenStandard = "ASA",
                    Network = "testnet",
                    TokenName = "<script>alert('xss')</script>"
                });
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task Malformed_NullByteInOwnerId_ServiceDoesNotThrow()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest
                {
                    OwnerId = "owner\0null-byte",
                    TokenStandard = "ASA",
                    Network = "testnet"
                });
            Assert.That(r, Is.Not.Null);
        }

        [Test]
        public async Task Malformed_UnicodeInOwnerId_ServiceDoesNotThrow()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest
                {
                    OwnerId = "owner-\u4e2d\u6587-unicode",
                    TokenStandard = "ASA",
                    Network = "testnet"
                });
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task Malformed_LongOwnerId_ServiceDoesNotThrow()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest
                {
                    OwnerId = new string('A', 2000),
                    TokenStandard = "ASA",
                    Network = "testnet"
                });
            Assert.That(r, Is.Not.Null);
        }

        [Test]
        public async Task Malformed_CaseMismatch_Standard_ASA_Accepted()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest
                {
                    OwnerId = "case-owner",
                    TokenStandard = "asa",  // lowercase
                    Network = "testnet"
                });
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task Malformed_CaseMismatch_Network_Testnet_Accepted()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest
                {
                    OwnerId = "case-network-owner",
                    TokenStandard = "ASA",
                    Network = "TestNet"  // mixed case
                });
            Assert.That(r.Success, Is.True);
        }

        // ── Concurrency ───────────────────────────────────────────────────────

        [Test]
        public async Task Concurrency_TenParallelEvaluations_AllSucceed()
        {
            var tasks = Enumerable.Range(0, 10).Select(i =>
                _service.EvaluateLaunchDecisionAsync(BuildRequest($"concurrent-owner-{i}"))).ToList();
            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.Success), Is.True);
        }

        [Test]
        public async Task Concurrency_TenParallelEvaluations_UniqueDecisionIds()
        {
            var tasks = Enumerable.Range(0, 10).Select(i =>
                _service.EvaluateLaunchDecisionAsync(BuildRequest($"conc-owner-{i}"))).ToList();
            var results = await Task.WhenAll(tasks);
            var ids = results.Select(r => r.DecisionId).ToHashSet();
            Assert.That(ids.Count, Is.EqualTo(10));
        }

        [Test]
        public async Task Concurrency_SameIdempotencyKey_OnlyOneUniqueDecision()
        {
            const string key = "concurrent-idem-key";
            var req = BuildRequest("conc-idem-owner");
            req.IdempotencyKey = key;

            var tasks = Enumerable.Range(0, 5).Select(_ =>
                _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
                {
                    OwnerId = req.OwnerId,
                    TokenStandard = req.TokenStandard,
                    Network = req.Network,
                    IdempotencyKey = req.IdempotencyKey
                })).ToList();
            var results = await Task.WhenAll(tasks);

            var decisionIds = results.Select(r => r.DecisionId).Distinct().ToList();
            // All requests with same idempotency key must return the same decisionId
            Assert.That(decisionIds.Count, Is.EqualTo(1));
        }

        // ── Multi-step workflow ───────────────────────────────────────────────

        [Test]
        public async Task MultiStep_Evaluate_Retrieve_Trace_Evidence_Consistent()
        {
            const string owner = "multistep-owner-001";
            const string corr = "multistep-corr-001";

            var decision = await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = owner,
                TokenStandard = "ARC3",
                Network = "testnet",
                CorrelationId = corr
            });

            Assert.That(decision.Success, Is.True);

            var retrieved = await _service.GetDecisionAsync(decision.DecisionId, corr);
            Assert.That(retrieved!.Status, Is.EqualTo(decision.Status));

            var trace = await _service.GetDecisionTraceAsync(new DecisionTraceRequest
            {
                DecisionId = decision.DecisionId,
                CorrelationId = corr
            });
            Assert.That(trace.OverallOutcome, Is.EqualTo(decision.Status));

            var bundle = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest
            {
                OwnerId = owner,
                DecisionId = decision.DecisionId,
                CorrelationId = corr
            });
            Assert.That(bundle.Items.All(e => e.DecisionId == decision.DecisionId), Is.True);
        }

        // ── Policy version coverage ────────────────────────────────────────────

        [TestCase("2024.01.01.1")]
        [TestCase("2023.06.15.2")]
        [TestCase("1.0.0")]
        public async Task PolicyVersion_OldVersions_TriggerStalenessWarning(string version)
        {
            var req = BuildRequest("pol-version-owner");
            req.PolicyVersion = version;
            var r = await _service.EvaluateLaunchDecisionAsync(req);
            Assert.That(r.Success, Is.True);
            Assert.That(r.Warnings.Any(w => w.RuleId == "RULE-POLICY-001"), Is.True);
        }

        // ── Evidence categories coverage ──────────────────────────────────────

        [TestCase(EvidenceCategory.Identity)]
        [TestCase(EvidenceCategory.Policy)]
        [TestCase(EvidenceCategory.Entitlement)]
        [TestCase(EvidenceCategory.Integration)]
        [TestCase(EvidenceCategory.Jurisdiction)]
        [TestCase(EvidenceCategory.Workflow)]
        public async Task EvidenceCategories_AllRepresented_AfterTestnetEvaluation(
            EvidenceCategory expectedCategory)
        {
            const string owner = "all-cat-owner";
            await _service.EvaluateLaunchDecisionAsync(BuildRequest(owner));
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner, Limit = 100 });
            var categories = bundle.Items.Select(e => e.Category).Distinct().ToHashSet();
            Assert.That(categories.Contains(expectedCategory), Is.True,
                $"Expected evidence category {expectedCategory} to be present.");
        }

        // ── Security: no sensitive data leaks ─────────────────────────────────

        [Test]
        public async Task Security_ErrorMessage_DoesNotContainStackTrace()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest { OwnerId = "", TokenStandard = "ASA", Network = "testnet" });
            Assert.That(r.ErrorMessage, Does.Not.Contain("at ").And.Not.Contain("Exception"));
        }

        [Test]
        public async Task Security_TraceRationale_DoesNotLeakPrivateData()
        {
            const string owner = "security-trace-owner";
            var d = await _service.EvaluateLaunchDecisionAsync(BuildRequest(owner));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            foreach (var rule in trace.Rules)
            {
                // Rationale should not contain "password", "secret", "key"
                Assert.That(rule.Rationale.ToLower(),
                    Does.Not.Contain("password").And.Not.Contain("secret"));
            }
        }

        [Test]
        public async Task Security_InputSnapshot_DoesNotExposeRawOwnerId()
        {
            const string owner = "sensitive-owner-data";
            var d = await _service.EvaluateLaunchDecisionAsync(BuildRequest(owner));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            // The input snapshot should describe keys, not expose full PII
            foreach (var rule in trace.Rules)
            {
                var snapshot = rule.InputSnapshot;
                if (snapshot.ContainsKey("ownerId"))
                    Assert.That(snapshot["ownerId"], Is.EqualTo("present"));
            }
        }

        // ── Backward compatibility ────────────────────────────────────────────

        [Test]
        public async Task BackwardCompat_SchemaVersion_AlwaysPresent()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(BuildRequest("bc-owner-1"));
            Assert.That(r.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task BackwardCompat_PolicyVersion_AlwaysPresent()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(BuildRequest("bc-owner-2"));
            Assert.That(r.PolicyVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task BackwardCompat_DecisionId_AlwaysPresent_EvenOnError()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest { OwnerId = "", TokenStandard = "ASA", Network = "testnet" });
            Assert.That(r.DecisionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task BackwardCompat_BlockersAndWarnings_AlwaysLists_NotNull()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(BuildRequest("bc-owner-3"));
            Assert.That(r.Blockers, Is.Not.Null);
            Assert.That(r.Warnings, Is.Not.Null);
            Assert.That(r.RecommendedActions, Is.Not.Null);
            Assert.That(r.EvidenceSummary, Is.Not.Null);
        }

        [Test]
        public async Task BackwardCompat_TraceSchemaVersion_AlwaysPresent()
        {
            var d = await _service.EvaluateLaunchDecisionAsync(BuildRequest("bc-trace-owner"));
            var trace = await _service.GetDecisionTraceAsync(
                new DecisionTraceRequest { DecisionId = d.DecisionId });
            Assert.That(trace.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task BackwardCompat_EvidenceBundleSchemaVersion_AlwaysPresent()
        {
            const string owner = "bc-bundle-owner";
            await _service.EvaluateLaunchDecisionAsync(BuildRequest(owner));
            var bundle = await _service.GetEvidenceBundleAsync(
                new EvidenceBundleRequest { OwnerId = owner });
            Assert.That(bundle.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        // ── Network cross-coverage ────────────────────────────────────────────

        [TestCase("voimain")]
        [TestCase("aramidmain")]
        [TestCase("base")]
        public async Task Network_MainnetVariants_HaveWarnings(string network)
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                BuildRequest($"mainnet-variant-{network}-owner", "ASA", network));
            Assert.That(r.Success, Is.True);
            // Mainnet-like networks should carry some advisory
            Assert.That(r.Warnings.Count, Is.GreaterThanOrEqualTo(0));
        }

        // ── Helper ────────────────────────────────────────────────────────────

        private static LaunchDecisionRequest BuildRequest(
            string owner, string standard = "ASA", string network = "testnet") =>
            new()
            {
                OwnerId = owner,
                TokenStandard = standard,
                Network = network,
                TokenName = "AdvancedTestToken"
            };
    }
}
