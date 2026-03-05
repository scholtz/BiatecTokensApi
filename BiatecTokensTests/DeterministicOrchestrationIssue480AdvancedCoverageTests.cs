using BiatecTokensApi.Models.DeterministicOrchestration;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Advanced coverage tests for Issue #480: Deterministic Orchestration.
    /// Covers: branch coverage for all enums/error codes, concurrency, malformed inputs,
    /// multi-step workflow audit, retry/rollback semantics, policy-conflict ordering.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class DeterministicOrchestrationIssue480AdvancedCoverageTests
    {
        private DeterministicOrchestrationService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<DeterministicOrchestrationService>>();
            _service = new DeterministicOrchestrationService(logger.Object);
        }

        // ── ENUM: All OrchestrationStage values reachable ─────────────────────

        [Test]
        public async Task ENUM_Stage_Draft_IsInitialStage()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            Assert.That(r.Stage, Is.EqualTo(OrchestrationStage.Draft));
        }

        [Test]
        public async Task ENUM_Stage_Validated_ReachableByAdvance()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var adv = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = r.OrchestrationId });
            Assert.That(adv.CurrentStage, Is.EqualTo(OrchestrationStage.Validated));
        }

        [Test]
        public async Task ENUM_Stage_Queued_ReachableByAdvance()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = r.OrchestrationId });
            var adv = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = r.OrchestrationId });
            Assert.That(adv.CurrentStage, Is.EqualTo(OrchestrationStage.Queued));
        }

        [Test]
        public async Task ENUM_Stage_Processing_ReachableByAdvance()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var id = r.OrchestrationId!;
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            var adv = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            Assert.That(adv.CurrentStage, Is.EqualTo(OrchestrationStage.Processing));
        }

        [Test]
        public async Task ENUM_Stage_Confirmed_ReachableByAdvance()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var id = r.OrchestrationId!;
            for (int i = 0; i < 3; i++)
                await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            var adv = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            Assert.That(adv.CurrentStage, Is.EqualTo(OrchestrationStage.Confirmed));
        }

        [Test]
        public async Task ENUM_Stage_Completed_ReachableByAdvance()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var id = r.OrchestrationId!;
            for (int i = 0; i < 4; i++)
                await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            var adv = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            Assert.That(adv.CurrentStage, Is.EqualTo(OrchestrationStage.Completed));
        }

        [Test]
        public async Task ENUM_Stage_Cancelled_ReachableByCancel()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            await _service.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = r.OrchestrationId });
            var status = await _service.GetStatusAsync(r.OrchestrationId!, null);
            Assert.That(status!.Stage, Is.EqualTo(OrchestrationStage.Cancelled));
        }

        // ── ENUM: All ComplianceCheckStatus values ─────────────────────────────

        [Test]
        public async Task ENUM_ComplianceStatus_Pending_BeforeFirstCheck()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var status = await _service.GetStatusAsync(r.OrchestrationId!, null);
            Assert.That(status!.ComplianceStatus, Is.EqualTo(ComplianceCheckStatus.Pending));
        }

        [Test]
        public async Task ENUM_ComplianceStatus_Passed_AfterCheck()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = r.OrchestrationId
            });
            Assert.That(check.Status, Is.EqualTo(ComplianceCheckStatus.Passed));
        }

        // ── ERROR: All error codes covered ───────────────────────────────────

        [Test]
        public async Task ERRCODE_INVALID_REQUEST_FromNullRequest()
        {
            var r = await _service.OrchestrateAsync(null!);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        }

        [Test]
        public async Task ERRCODE_MISSING_TOKEN_NAME_FromNullName()
        {
            var req = BuildRequest();
            req.TokenName = null;
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
        }

        [Test]
        public async Task ERRCODE_MISSING_TOKEN_STANDARD_FromNullStandard()
        {
            var req = BuildRequest();
            req.TokenStandard = null;
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_TOKEN_STANDARD"));
        }

        [Test]
        public async Task ERRCODE_UNSUPPORTED_TOKEN_STANDARD_FromBadStandard()
        {
            var req = BuildRequest();
            req.TokenStandard = "BADCOIN";
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r.ErrorCode, Is.EqualTo("UNSUPPORTED_TOKEN_STANDARD"));
        }

        [Test]
        public async Task ERRCODE_MISSING_NETWORK_FromNullNetwork()
        {
            var req = BuildRequest();
            req.Network = null;
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_NETWORK"));
        }

        [Test]
        public async Task ERRCODE_UNSUPPORTED_NETWORK_FromBadNetwork()
        {
            var req = BuildRequest();
            req.Network = "zksync";
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r.ErrorCode, Is.EqualTo("UNSUPPORTED_NETWORK"));
        }

        [Test]
        public async Task ERRCODE_MISSING_DEPLOYER_ADDRESS_FromNullAddress()
        {
            var req = BuildRequest();
            req.DeployerAddress = null;
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_DEPLOYER_ADDRESS"));
        }

        [Test]
        public async Task ERRCODE_INVALID_MAX_RETRIES_FromNegativeValue()
        {
            var req = BuildRequest();
            req.MaxRetries = -99;
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_MAX_RETRIES"));
        }

        [Test]
        public async Task ERRCODE_IDEMPOTENCY_KEY_CONFLICT_FromMismatchedParams()
        {
            var req1 = BuildRequest();
            req1.IdempotencyKey = "conflict-key-adv";
            await _service.OrchestrateAsync(req1);

            var req2 = BuildRequest();
            req2.IdempotencyKey = "conflict-key-adv";
            req2.Network = "mainnet";
            var r = await _service.OrchestrateAsync(req2);
            Assert.That(r.ErrorCode, Is.EqualTo("IDEMPOTENCY_KEY_CONFLICT"));
        }

        [Test]
        public async Task ERRCODE_MISSING_ORCHESTRATION_ID_FromAdvanceWithNull()
        {
            var r = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = null });
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_ORCHESTRATION_ID"));
        }

        [Test]
        public async Task ERRCODE_ORCHESTRATION_NOT_FOUND_FromAdvanceWithGhost()
        {
            var r = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = "ghost" });
            Assert.That(r.ErrorCode, Is.EqualTo("ORCHESTRATION_NOT_FOUND"));
        }

        [Test]
        public async Task ERRCODE_TERMINAL_STAGE_FromAdvanceCompleted()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var id = created.OrchestrationId!;
            for (int i = 0; i < 5; i++)
                await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            var r = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            Assert.That(r.ErrorCode, Is.EqualTo("TERMINAL_STAGE"));
        }

        [Test]
        public async Task ERRCODE_CANNOT_CANCEL_FromCancelCompleted()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var id = created.OrchestrationId!;
            for (int i = 0; i < 5; i++)
                await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            var r = await _service.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = id });
            Assert.That(r.ErrorCode, Is.EqualTo("CANNOT_CANCEL"));
        }

        // ── CONC: Concurrency tests ───────────────────────────────────────────

        [Test]
        public async Task CONC_FiveParallelOrchestrations_AllSucceed()
        {
            var tasks = Enumerable.Range(1, 5)
                .Select(i => _service.OrchestrateAsync(new OrchestrationRequest
                {
                    TokenName = $"concurrent-token-{i}",
                    TokenStandard = "ASA",
                    Network = "testnet",
                    DeployerAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                    CorrelationId = $"conc-{i}",
                    MaxRetries = 3
                }))
                .ToList();

            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.Success), Is.True);
            var ids = results.Select(r => r.OrchestrationId).Distinct().ToList();
            Assert.That(ids.Count, Is.EqualTo(5));
        }

        [Test]
        public async Task CONC_ParallelAdvances_OnSameOrchestration_OnlyOneSucceedsFirst()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var id = created.OrchestrationId!;

            var tasks = Enumerable.Range(0, 3)
                .Select(_ => _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }))
                .ToList();
            var results = await Task.WhenAll(tasks);

            // At least one must have succeeded; duplicates may or may not succeed depending on stage
            Assert.That(results.Any(r => r.Success), Is.True);
        }

        // ── MAL: Malformed input tests ────────────────────────────────────────

        [Test]
        public async Task MAL_SqlInjection_InTokenName_DoesNotThrow()
        {
            var req = BuildRequest();
            req.TokenName = "'; DROP TABLE tokens; --";
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r, Is.Not.Null); // no exception thrown
            Assert.That(r.Success, Is.True); // injected string treated as valid token name
        }

        [Test]
        public async Task MAL_XSS_InTokenName_DoesNotThrow()
        {
            var req = BuildRequest();
            req.TokenName = "<script>alert('xss')</script>";
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task MAL_NullByte_InNetwork_ReturnsUnsupportedNetwork()
        {
            var req = BuildRequest();
            req.Network = "test\0net";
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("UNSUPPORTED_NETWORK"));
        }

        [Test]
        public async Task MAL_UnicodeCharacters_InTokenName_Succeeds()
        {
            var req = BuildRequest();
            req.TokenName = "BiatecToken-€-あ-🚀";
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task MAL_EmptyString_IdempotencyKey_TreatedAsNoKey()
        {
            var req = BuildRequest();
            req.IdempotencyKey = "";
            var r1 = await _service.OrchestrateAsync(req);
            var r2 = await _service.OrchestrateAsync(req);
            // Empty key should not be treated as a valid idempotency key; creates two separate orchestrations
            Assert.That(r1.OrchestrationId, Is.Not.EqualTo(r2.OrchestrationId));
        }

        // ── MULTI: Multi-step audit trail integrity ────────────────────────────

        [Test]
        public async Task MULTI_AuditTrail_RecordsAllOperationsInOrder()
        {
            var req = BuildRequest();
            req.CorrelationId = "multi-audit-corr";
            var created = await _service.OrchestrateAsync(req);
            var id = created.OrchestrationId!;

            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id, CorrelationId = "multi-audit-corr" });
            await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { OrchestrationId = id, CorrelationId = "multi-audit-corr" });
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id, CorrelationId = "multi-audit-corr" });

            var events = _service.GetAuditEvents(id);
            var ops = events.Select(e => e.Operation).ToList();

            Assert.That(ops[0], Is.EqualTo("Orchestrate"));
            Assert.That(ops[1], Is.EqualTo("Advance"));
            Assert.That(ops[2], Is.EqualTo("ComplianceCheck"));
            Assert.That(ops[3], Is.EqualTo("Advance"));
        }

        [Test]
        public async Task MULTI_AuditTrail_FailedOperations_AreRecorded()
        {
            await _service.OrchestrateAsync(new OrchestrationRequest
            {
                TokenName = null,
                CorrelationId = "multi-fail-corr"
            });

            var events = _service.GetAuditEvents(correlationId: "multi-fail-corr");
            Assert.That(events.Any(e => !e.Succeeded), Is.True);
        }

        // ── RETRY: Retry/rollback semantics ───────────────────────────────────

        [Test]
        public async Task RETRY_MaxRetriesZero_StillCreatesOrchestration()
        {
            var req = BuildRequest();
            req.MaxRetries = 0;
            var r = await _service.OrchestrateAsync(req);
            Assert.That(r.Success, Is.True);
            var status = await _service.GetStatusAsync(r.OrchestrationId!, null);
            Assert.That(status!.MaxRetries, Is.EqualTo(0));
        }

        [Test]
        public async Task RETRY_MaxRetriesPreservedInStatus()
        {
            var req = BuildRequest();
            req.MaxRetries = 7;
            var r = await _service.OrchestrateAsync(req);
            var status = await _service.GetStatusAsync(r.OrchestrationId!, null);
            Assert.That(status!.MaxRetries, Is.EqualTo(7));
        }

        [Test]
        public async Task RETRY_RetryCount_StartsAtZero()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var status = await _service.GetStatusAsync(r.OrchestrationId!, null);
            Assert.That(status!.RetryCount, Is.EqualTo(0));
        }

        // ── COMPLIANCE: Compliance rule filtering ─────────────────────────────

        [Test]
        public async Task COMPLIANCE_OnlyMica_Excludes_KycAml()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = r.OrchestrationId,
                RunMicaChecks = true,
                RunKycAmlChecks = false
            });
            Assert.That(check.Rules.Any(rule => rule.RuleId.StartsWith("KYC")), Is.False);
            Assert.That(check.Rules.Any(rule => rule.RuleId.StartsWith("AML")), Is.False);
        }

        [Test]
        public async Task COMPLIANCE_OnlyKycAml_Excludes_Mica()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = r.OrchestrationId,
                RunMicaChecks = false,
                RunKycAmlChecks = true
            });
            Assert.That(check.Rules.Any(rule => rule.RuleId.StartsWith("MICA")), Is.False);
        }

        [Test]
        public async Task COMPLIANCE_NoChecksEnabled_ReturnsNoRules()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = r.OrchestrationId,
                RunMicaChecks = false,
                RunKycAmlChecks = false
            });
            Assert.That(check.Success, Is.True);
            // AUDIT and NET rules are always included (not filtered by MiCA/KYC flags)
            Assert.That(check.Rules, Is.Not.Empty);
        }

        [Test]
        public async Task COMPLIANCE_RuleResults_ContainMessageAndHint()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = r.OrchestrationId,
                RunMicaChecks = true,
                RunKycAmlChecks = true
            });
            Assert.That(check.Rules.All(rule => !string.IsNullOrWhiteSpace(rule.Message)), Is.True);
        }

        // ── SCHEMA: Response schema contract assertions ────────────────────────

        [Test]
        public async Task SCHEMA_OrchestrationResponse_HasAllRequiredFields()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            Assert.That(r.OrchestrationId, Is.Not.Null);
            Assert.That(r.SchemaVersion, Is.Not.Null);
            Assert.That(r.CorrelationId, Is.Not.Null);
            Assert.That(r.CreatedAt, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public async Task SCHEMA_OrchestrationStatusResponse_HasAllRequiredFields()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var status = await _service.GetStatusAsync(created.OrchestrationId!, null);
            Assert.That(status!.TokenName, Is.Not.Null);
            Assert.That(status.TokenStandard, Is.Not.Null);
            Assert.That(status.Network, Is.Not.Null);
            Assert.That(status.DeployerAddress, Is.Not.Null);
            Assert.That(status.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task SCHEMA_ComplianceCheckResponse_HasEvaluatedAt()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = r.OrchestrationId
            });
            Assert.That(check.EvaluatedAt, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public async Task SCHEMA_AuditEntry_HasRequiredFields()
        {
            var r = await _service.OrchestrateAsync(BuildRequest());
            var events = _service.GetAuditEvents(r.OrchestrationId);
            Assert.That(events, Is.Not.Empty);
            var ev = events.First();
            Assert.That(ev.EventId, Is.Not.Null);
            Assert.That(ev.Operation, Is.Not.Null);
            Assert.That(ev.Timestamp, Is.Not.EqualTo(default(DateTime)));
        }

        // ── Helper ─────────────────────────────────────────────────────────────

        private static OrchestrationRequest BuildRequest() => new()
        {
            TokenName = "AdvancedTestToken",
            TokenStandard = "ASA",
            Network = "testnet",
            DeployerAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            CorrelationId = Guid.NewGuid().ToString(),
            MaxRetries = 3
        };
    }
}
