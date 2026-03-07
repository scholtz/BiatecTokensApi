using BiatecTokensApi.Models.ComplianceEvidenceLaunchDecision;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Contract tests for ComplianceEvidenceLaunchDecisionService.
    /// Validates that response shapes, field stability, and schema contracts are upheld.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ComplianceEvidenceLaunchDecisionContractTests
    {
        private ComplianceEvidenceLaunchDecisionService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<ComplianceEvidenceLaunchDecisionService>>();
            _service = new ComplianceEvidenceLaunchDecisionService(logger.Object);
        }

        // ── LaunchDecisionResponse schema contract ────────────────────────────

        [Test]
        public async Task LaunchDecision_ResponseHas_DecisionId()
        {
            var r = await EvalAsync();
            Assert.That(r.DecisionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task LaunchDecision_ResponseHas_Status()
        {
            var r = await EvalAsync();
            Assert.That(Enum.IsDefined(typeof(LaunchDecisionStatus), r.Status), Is.True);
        }

        [Test]
        public async Task LaunchDecision_ResponseHas_CanLaunch()
        {
            var r = await EvalAsync();
            // CanLaunch is bool – just assert it is present (no exception)
            Assert.That(r.CanLaunch, Is.InstanceOf<bool>());
        }

        [Test]
        public async Task LaunchDecision_ResponseHas_Summary_NonEmpty()
        {
            var r = await EvalAsync();
            Assert.That(r.Summary, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task LaunchDecision_ResponseHas_PolicyVersion_NonEmpty()
        {
            var r = await EvalAsync();
            Assert.That(r.PolicyVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task LaunchDecision_ResponseHas_SchemaVersion()
        {
            var r = await EvalAsync();
            Assert.That(r.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task LaunchDecision_ResponseHas_DecidedAt()
        {
            var before = DateTime.UtcNow.AddSeconds(-2);
            var r = await EvalAsync();
            Assert.That(r.DecidedAt, Is.GreaterThan(before));
        }

        [Test]
        public async Task LaunchDecision_ResponseHas_EvaluationTimeMs_NonNegative()
        {
            var r = await EvalAsync();
            Assert.That(r.EvaluationTimeMs, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task LaunchDecision_ResponseHas_BlockersList_NotNull()
        {
            var r = await EvalAsync();
            Assert.That(r.Blockers, Is.Not.Null);
        }

        [Test]
        public async Task LaunchDecision_ResponseHas_WarningsList_NotNull()
        {
            var r = await EvalAsync();
            Assert.That(r.Warnings, Is.Not.Null);
        }

        [Test]
        public async Task LaunchDecision_ResponseHas_RecommendedActions_NotNull()
        {
            var r = await EvalAsync();
            Assert.That(r.RecommendedActions, Is.Not.Null);
        }

        [Test]
        public async Task LaunchDecision_ResponseHas_EvidenceSummary_NotNull()
        {
            var r = await EvalAsync();
            Assert.That(r.EvidenceSummary, Is.Not.Null);
        }

        [Test]
        public async Task LaunchDecision_SuccessFlag_TrueOnValidRequest()
        {
            var r = await EvalAsync();
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task LaunchDecision_ErrorFields_NullOnSuccess()
        {
            var r = await EvalAsync();
            Assert.That(r.ErrorCode, Is.Null);
            Assert.That(r.ErrorMessage, Is.Null);
        }

        // ── Blocker schema contract ───────────────────────────────────────────

        [Test]
        public async Task Blocker_Has_BlockerId_WhenPresent()
        {
            // ARC1400 on mainnet triggers blocker + warning
            var r = await EvalAsync(standard: "ARC1400", network: "mainnet");
            foreach (var b in r.Blockers)
                Assert.That(b.BlockerId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Blocker_Has_Title_WhenPresent()
        {
            var r = await EvalAsync(standard: "ARC1400", network: "mainnet");
            foreach (var b in r.Blockers)
                Assert.That(b.Title, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Blocker_Has_Description_WhenPresent()
        {
            var r = await EvalAsync(standard: "ARC1400", network: "mainnet");
            foreach (var b in r.Blockers)
                Assert.That(b.Description, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Blocker_Has_Severity_WhenPresent()
        {
            var r = await EvalAsync(standard: "ARC1400", network: "mainnet");
            foreach (var b in r.Blockers)
                Assert.That(Enum.IsDefined(typeof(LaunchBlockerSeverity), b.Severity), Is.True);
        }

        [Test]
        public async Task Blocker_Has_RemediationSteps_NotNull()
        {
            var r = await EvalAsync(standard: "ARC1400", network: "mainnet");
            foreach (var b in r.Blockers)
                Assert.That(b.RemediationSteps, Is.Not.Null);
        }

        [Test]
        public async Task Blocker_Has_RuleId_WhenPresent()
        {
            var r = await EvalAsync(standard: "ARC1400", network: "mainnet");
            foreach (var b in r.Blockers)
                Assert.That(b.RuleId, Is.Not.Null.And.Not.Empty);
        }

        // ── Warning schema contract ───────────────────────────────────────────

        [Test]
        public async Task Warning_Has_WarningId_WhenPresent()
        {
            var r = await EvalAsync(network: "mainnet");
            foreach (var w in r.Warnings)
                Assert.That(w.WarningId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Warning_Has_Title_WhenPresent()
        {
            var r = await EvalAsync(network: "mainnet");
            foreach (var w in r.Warnings)
                Assert.That(w.Title, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Warning_Has_Description_WhenPresent()
        {
            var r = await EvalAsync(network: "mainnet");
            foreach (var w in r.Warnings)
                Assert.That(w.Description, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Warning_Has_RuleId_WhenPresent()
        {
            var r = await EvalAsync(network: "mainnet");
            foreach (var w in r.Warnings)
                Assert.That(w.RuleId, Is.Not.Null.And.Not.Empty);
        }

        // ── RecommendedAction schema contract ─────────────────────────────────

        [Test]
        public async Task RecommendedAction_Has_ActionId_WhenPresent()
        {
            var r = await EvalAsync(standard: "ARC1400", network: "mainnet");
            foreach (var a in r.RecommendedActions)
                Assert.That(a.ActionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task RecommendedAction_Has_Title_WhenPresent()
        {
            var r = await EvalAsync(standard: "ARC1400", network: "mainnet");
            foreach (var a in r.RecommendedActions)
                Assert.That(a.Title, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task RecommendedAction_Priority_NonNegative()
        {
            var r = await EvalAsync(standard: "ARC1400", network: "mainnet");
            foreach (var a in r.RecommendedActions)
                Assert.That(a.Priority, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task RecommendedActions_OrderedByPriority()
        {
            var r = await EvalAsync(standard: "ARC1400", network: "mainnet");
            if (r.RecommendedActions.Count >= 2)
            {
                var priorities = r.RecommendedActions.Select(a => a.Priority).ToList();
                Assert.That(priorities, Is.Ordered.Ascending);
            }
        }

        // ── EvidenceSummaryItem schema contract ───────────────────────────────

        [Test]
        public async Task EvidenceSummaryItem_Has_EvidenceId()
        {
            var r = await EvalAsync();
            foreach (var e in r.EvidenceSummary)
                Assert.That(e.EvidenceId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvidenceSummaryItem_Has_Category()
        {
            var r = await EvalAsync();
            foreach (var e in r.EvidenceSummary)
                Assert.That(Enum.IsDefined(typeof(EvidenceCategory), e.Category), Is.True);
        }

        [Test]
        public async Task EvidenceSummaryItem_Has_ValidationStatus()
        {
            var r = await EvalAsync();
            foreach (var e in r.EvidenceSummary)
                Assert.That(Enum.IsDefined(typeof(EvidenceValidationStatus), e.ValidationStatus), Is.True);
        }

        [Test]
        public async Task EvidenceSummaryItem_Has_CollectedAt()
        {
            var before = DateTime.UtcNow.AddMinutes(-1);
            var r = await EvalAsync();
            foreach (var e in r.EvidenceSummary)
                Assert.That(e.CollectedAt, Is.GreaterThan(before));
        }

        // ── DecisionTraceResponse schema contract ─────────────────────────────

        [Test]
        public async Task DecisionTrace_Has_DecisionId()
        {
            var d = await EvalAsync();
            var t = await _service.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = d.DecisionId });
            Assert.That(t.DecisionId, Is.EqualTo(d.DecisionId));
        }

        [Test]
        public async Task DecisionTrace_Has_PolicyVersion()
        {
            var d = await EvalAsync();
            var t = await _service.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = d.DecisionId });
            Assert.That(t.PolicyVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task DecisionTrace_Has_Rules_NonEmpty()
        {
            var d = await EvalAsync();
            var t = await _service.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = d.DecisionId });
            Assert.That(t.Rules.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task DecisionTrace_Has_SchemaVersion()
        {
            var d = await EvalAsync();
            var t = await _service.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = d.DecisionId });
            Assert.That(t.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task DecisionTrace_OverallOutcome_MatchesDecisionStatus()
        {
            var d = await EvalAsync();
            var t = await _service.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = d.DecisionId });
            Assert.That(t.OverallOutcome, Is.EqualTo(d.Status));
        }

        // ── RuleEvaluationRecord schema contract ──────────────────────────────

        [Test]
        public async Task RuleRecord_Has_RuleId()
        {
            var d = await EvalAsync();
            var t = await _service.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = d.DecisionId });
            foreach (var rule in t.Rules)
                Assert.That(rule.RuleId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task RuleRecord_Has_RuleName()
        {
            var d = await EvalAsync();
            var t = await _service.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = d.DecisionId });
            foreach (var rule in t.Rules)
                Assert.That(rule.RuleName, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task RuleRecord_Has_Outcome()
        {
            var d = await EvalAsync();
            var t = await _service.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = d.DecisionId });
            foreach (var rule in t.Rules)
                Assert.That(Enum.IsDefined(typeof(RuleOutcome), rule.Outcome), Is.True);
        }

        [Test]
        public async Task RuleRecord_Has_EvaluationOrder_Positive()
        {
            var d = await EvalAsync();
            var t = await _service.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = d.DecisionId });
            foreach (var rule in t.Rules)
                Assert.That(rule.EvaluationOrder, Is.GreaterThan(0));
        }

        [Test]
        public async Task RuleRecord_Has_EvidenceIds_NotNull()
        {
            var d = await EvalAsync();
            var t = await _service.GetDecisionTraceAsync(new DecisionTraceRequest { DecisionId = d.DecisionId });
            foreach (var rule in t.Rules)
                Assert.That(rule.EvidenceIds, Is.Not.Null);
        }

        // ── EvidenceBundleResponse schema contract ────────────────────────────

        [Test]
        public async Task EvidenceBundle_Has_BundleId()
        {
            const string owner = "contract-owner-ev";
            await EvalAsync(ownerId: owner);
            var bundle = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest { OwnerId = owner });
            Assert.That(bundle.BundleId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvidenceBundle_Has_OwnerId()
        {
            const string owner = "contract-owner-ev2";
            await EvalAsync(ownerId: owner);
            var bundle = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest { OwnerId = owner });
            Assert.That(bundle.OwnerId, Is.EqualTo(owner));
        }

        [Test]
        public async Task EvidenceBundle_Has_AssembledAt()
        {
            const string owner = "contract-owner-ev3";
            await EvalAsync(ownerId: owner);
            var before = DateTime.UtcNow.AddSeconds(-2);
            var bundle = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest { OwnerId = owner });
            Assert.That(bundle.AssembledAt, Is.GreaterThan(before));
        }

        [Test]
        public async Task EvidenceBundle_Has_SchemaVersion()
        {
            const string owner = "contract-owner-ev4";
            await EvalAsync(ownerId: owner);
            var bundle = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest { OwnerId = owner });
            Assert.That(bundle.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        // ── ComplianceEvidenceItem schema contract ────────────────────────────

        [Test]
        public async Task EvidenceItem_Has_EvidenceId()
        {
            const string owner = "item-owner-001";
            await EvalAsync(ownerId: owner);
            var bundle = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest { OwnerId = owner });
            foreach (var item in bundle.Items)
                Assert.That(item.EvidenceId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvidenceItem_Has_Category()
        {
            const string owner = "item-owner-002";
            await EvalAsync(ownerId: owner);
            var bundle = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest { OwnerId = owner });
            foreach (var item in bundle.Items)
                Assert.That(Enum.IsDefined(typeof(EvidenceCategory), item.Category), Is.True);
        }

        [Test]
        public async Task EvidenceItem_Has_Source()
        {
            const string owner = "item-owner-003";
            await EvalAsync(ownerId: owner);
            var bundle = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest { OwnerId = owner });
            foreach (var item in bundle.Items)
                Assert.That(item.Source, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvidenceItem_Has_ValidationStatus()
        {
            const string owner = "item-owner-004";
            await EvalAsync(ownerId: owner);
            var bundle = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest { OwnerId = owner });
            foreach (var item in bundle.Items)
                Assert.That(Enum.IsDefined(typeof(EvidenceValidationStatus), item.ValidationStatus), Is.True);
        }

        [Test]
        public async Task EvidenceItem_Has_Rationale()
        {
            const string owner = "item-owner-005";
            await EvalAsync(ownerId: owner);
            var bundle = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest { OwnerId = owner });
            foreach (var item in bundle.Items)
                Assert.That(item.Rationale, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task EvidenceItem_Has_Metadata_NotNull()
        {
            const string owner = "item-owner-006";
            await EvalAsync(ownerId: owner);
            var bundle = await _service.GetEvidenceBundleAsync(new EvidenceBundleRequest { OwnerId = owner });
            foreach (var item in bundle.Items)
                Assert.That(item.Metadata, Is.Not.Null);
        }

        // ── Error response schema contract ────────────────────────────────────

        [Test]
        public async Task ErrorResponse_Has_ErrorCode_WhenFailing()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest { OwnerId = "", TokenStandard = "ASA", Network = "testnet" });
            Assert.That(r.ErrorCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ErrorResponse_Has_ErrorMessage_WhenFailing()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest { OwnerId = "", TokenStandard = "ASA", Network = "testnet" });
            Assert.That(r.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ErrorResponse_DecisionId_StillPresent()
        {
            var r = await _service.EvaluateLaunchDecisionAsync(
                new LaunchDecisionRequest { OwnerId = "", TokenStandard = "ASA", Network = "testnet" });
            Assert.That(r.DecisionId, Is.Not.Null.And.Not.Empty);
        }

        // ── Helper ────────────────────────────────────────────────────────────

        private async Task<LaunchDecisionResponse> EvalAsync(
            string ownerId = "contract-owner",
            string standard = "ASA",
            string network = "testnet") =>
            await _service.EvaluateLaunchDecisionAsync(new LaunchDecisionRequest
            {
                OwnerId = ownerId,
                TokenStandard = standard,
                Network = network
            });
    }
}
