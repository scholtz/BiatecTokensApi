using BiatecTokensApi.Models.DeterministicOrchestration;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for DeterministicOrchestrationService (Issue #480).
    /// All tests use direct service instantiation – no HTTP calls.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class DeterministicOrchestrationIssue480ServiceUnitTests
    {
        private DeterministicOrchestrationService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<DeterministicOrchestrationService>>();
            _service = new DeterministicOrchestrationService(logger.Object);
        }

        // ── OrchestrateAsync – happy paths ────────────────────────────────────

        [Test]
        public async Task OrchestrateAsync_ValidRequest_ReturnsSuccess()
        {
            var result = await _service.OrchestrateAsync(BuildRequest());
            Assert.That(result.Success, Is.True);
            Assert.That(result.OrchestrationId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Stage, Is.EqualTo(OrchestrationStage.Draft));
        }

        [Test]
        public async Task OrchestrateAsync_ValidRequest_CorrelationIdPropagated()
        {
            var req = BuildRequest();
            req.CorrelationId = "corr-abc-123";
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.CorrelationId, Is.EqualTo("corr-abc-123"));
        }

        [Test]
        public async Task OrchestrateAsync_NoCorrelationId_GeneratesOne()
        {
            var req = BuildRequest();
            req.CorrelationId = null;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task OrchestrateAsync_ValidRequest_SetsSchemaVersion()
        {
            var result = await _service.OrchestrateAsync(BuildRequest());
            Assert.That(result.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task OrchestrateAsync_ValidRequest_SetsCreatedAt()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var result = await _service.OrchestrateAsync(BuildRequest());
            Assert.That(result.CreatedAt, Is.GreaterThanOrEqualTo(before));
        }

        [Test]
        public async Task OrchestrateAsync_ValidRequest_RecordsAuditEvent()
        {
            var req = BuildRequest();
            req.CorrelationId = "audit-corr-1";
            var result = await _service.OrchestrateAsync(req);
            var events = _service.GetAuditEvents(result.OrchestrationId);
            Assert.That(events, Is.Not.Empty);
            Assert.That(events.Any(e => e.Operation == "Orchestrate" && e.Succeeded), Is.True);
        }

        // ── OrchestrateAsync – validation errors ──────────────────────────────

        [Test]
        public async Task OrchestrateAsync_NullRequest_ReturnsError()
        {
            var result = await _service.OrchestrateAsync(null!);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        }

        [Test]
        public async Task OrchestrateAsync_MissingTokenName_ReturnsError()
        {
            var req = BuildRequest();
            req.TokenName = null;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
        }

        [Test]
        public async Task OrchestrateAsync_EmptyTokenName_ReturnsError()
        {
            var req = BuildRequest();
            req.TokenName = "   ";
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
        }

        [Test]
        public async Task OrchestrateAsync_MissingTokenStandard_ReturnsError()
        {
            var req = BuildRequest();
            req.TokenStandard = null;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_STANDARD"));
        }

        [Test]
        public async Task OrchestrateAsync_UnsupportedTokenStandard_ReturnsError()
        {
            var req = BuildRequest();
            req.TokenStandard = "UNKNOWN";
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_TOKEN_STANDARD"));
        }

        [Test]
        public async Task OrchestrateAsync_MissingNetwork_ReturnsError()
        {
            var req = BuildRequest();
            req.Network = null;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_NETWORK"));
        }

        [Test]
        public async Task OrchestrateAsync_UnsupportedNetwork_ReturnsError()
        {
            var req = BuildRequest();
            req.Network = "unknownchain";
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_NETWORK"));
        }

        [Test]
        public async Task OrchestrateAsync_MissingDeployerAddress_ReturnsError()
        {
            var req = BuildRequest();
            req.DeployerAddress = null;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_DEPLOYER_ADDRESS"));
        }

        [Test]
        public async Task OrchestrateAsync_NegativeMaxRetries_ReturnsError()
        {
            var req = BuildRequest();
            req.MaxRetries = -1;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_MAX_RETRIES"));
        }

        [Test]
        public async Task OrchestrateAsync_ErrorResponse_HasRemediationHint()
        {
            var req = BuildRequest();
            req.TokenName = null;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        // ── Idempotency ───────────────────────────────────────────────────────

        [Test]
        public async Task OrchestrateAsync_SameIdempotencyKey_ReturnsReplay()
        {
            var req = BuildRequest();
            req.IdempotencyKey = "idem-key-1";
            var first = await _service.OrchestrateAsync(req);
            var second = await _service.OrchestrateAsync(req);
            Assert.That(second.IsIdempotentReplay, Is.True);
            Assert.That(second.OrchestrationId, Is.EqualTo(first.OrchestrationId));
        }

        [Test]
        public async Task OrchestrateAsync_SameKeyDifferentParams_ReturnsConflict()
        {
            var req1 = BuildRequest();
            req1.IdempotencyKey = "idem-key-conflict";
            await _service.OrchestrateAsync(req1);

            var req2 = BuildRequest();
            req2.IdempotencyKey = "idem-key-conflict";
            req2.TokenName = "DifferentToken";
            var result = await _service.OrchestrateAsync(req2);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("IDEMPOTENCY_KEY_CONFLICT"));
        }

        [Test]
        public async Task OrchestrateAsync_NoIdempotencyKey_CreatesSeparateOrchestrations()
        {
            var r1 = await _service.OrchestrateAsync(BuildRequest());
            var r2 = await _service.OrchestrateAsync(BuildRequest());
            Assert.That(r1.OrchestrationId, Is.Not.EqualTo(r2.OrchestrationId));
        }

        // ── GetStatusAsync ────────────────────────────────────────────────────

        [Test]
        public async Task GetStatusAsync_ExistingOrchestration_ReturnsStatus()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var status = await _service.GetStatusAsync(created.OrchestrationId!, null);
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.OrchestrationId, Is.EqualTo(created.OrchestrationId));
        }

        [Test]
        public async Task GetStatusAsync_UnknownId_ReturnsNull()
        {
            var status = await _service.GetStatusAsync("nonexistent-id", null);
            Assert.That(status, Is.Null);
        }

        [Test]
        public async Task GetStatusAsync_ReturnsCorrectStage()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var status = await _service.GetStatusAsync(created.OrchestrationId!, null);
            Assert.That(status!.Stage, Is.EqualTo(OrchestrationStage.Draft));
        }

        // ── AdvanceAsync ──────────────────────────────────────────────────────

        [Test]
        public async Task AdvanceAsync_FromDraft_AdvancesToValidated()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var advance = await _service.AdvanceAsync(new OrchestrationAdvanceRequest
            {
                OrchestrationId = created.OrchestrationId
            });
            Assert.That(advance.Success, Is.True);
            Assert.That(advance.PreviousStage, Is.EqualTo(OrchestrationStage.Draft));
            Assert.That(advance.CurrentStage, Is.EqualTo(OrchestrationStage.Validated));
        }

        [Test]
        public async Task AdvanceAsync_MissingOrchestrationId_ReturnsError()
        {
            var result = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = null });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_ORCHESTRATION_ID"));
        }

        [Test]
        public async Task AdvanceAsync_UnknownId_ReturnsError()
        {
            var result = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = "ghost" });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("ORCHESTRATION_NOT_FOUND"));
        }

        [Test]
        public async Task AdvanceAsync_TerminalStage_ReturnsError()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            // Cancel it to reach terminal stage
            await _service.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = created.OrchestrationId });
            var advance = await _service.AdvanceAsync(new OrchestrationAdvanceRequest
            {
                OrchestrationId = created.OrchestrationId
            });
            Assert.That(advance.Success, Is.False);
            Assert.That(advance.ErrorCode, Is.EqualTo("TERMINAL_STAGE"));
        }

        [Test]
        public async Task AdvanceAsync_ThroughFullLifecycle_ReachesConfirmed()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var id = created.OrchestrationId!;
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // Draft→Validated
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // Validated→Queued
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // Queued→Processing
            var result = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // Processing→Confirmed
            Assert.That(result.Success, Is.True);
            Assert.That(result.CurrentStage, Is.EqualTo(OrchestrationStage.Confirmed));
        }

        // ── ComplianceCheck ───────────────────────────────────────────────────

        [Test]
        public async Task RunComplianceCheckAsync_ValidOrchestration_ReturnsPassed()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = created.OrchestrationId
            });
            Assert.That(check.Success, Is.True);
            Assert.That(check.Status, Is.EqualTo(ComplianceCheckStatus.Passed));
        }

        [Test]
        public async Task RunComplianceCheckAsync_MissingOrchestrationId_ReturnsError()
        {
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = null
            });
            Assert.That(check.Success, Is.False);
            Assert.That(check.ErrorCode, Is.EqualTo("MISSING_ORCHESTRATION_ID"));
        }

        [Test]
        public async Task RunComplianceCheckAsync_UnknownId_ReturnsError()
        {
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = "ghost-id"
            });
            Assert.That(check.Success, Is.False);
            Assert.That(check.ErrorCode, Is.EqualTo("ORCHESTRATION_NOT_FOUND"));
        }

        [Test]
        public async Task RunComplianceCheckAsync_ReturnsRules()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = created.OrchestrationId,
                RunMicaChecks = true,
                RunKycAmlChecks = true
            });
            Assert.That(check.Rules, Is.Not.Empty);
        }

        [Test]
        public async Task RunComplianceCheckAsync_MicaOnlyFlag_ExcludesKycRules()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = created.OrchestrationId,
                RunMicaChecks = true,
                RunKycAmlChecks = false
            });
            Assert.That(check.Rules.Any(r => r.RuleId.StartsWith("KYC") || r.RuleId.StartsWith("AML")), Is.False);
        }

        [Test]
        public async Task RunComplianceCheckAsync_CorrelationIdPropagated()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = created.OrchestrationId,
                CorrelationId = "comp-corr-99"
            });
            Assert.That(check.CorrelationId, Is.EqualTo("comp-corr-99"));
        }

        // ── CancelAsync ───────────────────────────────────────────────────────

        [Test]
        public async Task CancelAsync_DraftStage_Succeeds()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var cancel = await _service.CancelAsync(new OrchestrationCancelRequest
            {
                OrchestrationId = created.OrchestrationId
            });
            Assert.That(cancel.Success, Is.True);
            Assert.That(cancel.PreviousStage, Is.EqualTo(OrchestrationStage.Draft));
        }

        [Test]
        public async Task CancelAsync_MissingId_ReturnsError()
        {
            var result = await _service.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = null });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_ORCHESTRATION_ID"));
        }

        [Test]
        public async Task CancelAsync_UnknownId_ReturnsError()
        {
            var result = await _service.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = "ghost" });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("ORCHESTRATION_NOT_FOUND"));
        }

        [Test]
        public async Task CancelAsync_AlreadyCancelled_ReturnsError()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            await _service.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = created.OrchestrationId });
            var second = await _service.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = created.OrchestrationId });
            Assert.That(second.Success, Is.False);
            Assert.That(second.ErrorCode, Is.EqualTo("CANNOT_CANCEL"));
        }

        // ── GetAuditEvents ────────────────────────────────────────────────────

        [Test]
        public async Task GetAuditEvents_ReturnsEventsForOrchestrationId()
        {
            var created = await _service.OrchestrateAsync(BuildRequest());
            var events = _service.GetAuditEvents(created.OrchestrationId);
            Assert.That(events, Is.Not.Empty);
            Assert.That(events.All(e => e.OrchestrationId == created.OrchestrationId), Is.True);
        }

        [Test]
        public async Task GetAuditEvents_FilterByCorrelationId_ReturnsMatching()
        {
            var req = BuildRequest();
            req.CorrelationId = "filter-corr-test";
            var created = await _service.OrchestrateAsync(req);
            var events = _service.GetAuditEvents(correlationId: "filter-corr-test");
            Assert.That(events, Is.Not.Empty);
        }

        [Test]
        public async Task GetAuditEvents_UnknownId_ReturnsEmpty()
        {
            var events = _service.GetAuditEvents("ghost-id");
            Assert.That(events, Is.Empty);
        }

        // ── Helper ─────────────────────────────────────────────────────────────

        private static OrchestrationRequest BuildRequest() => new()
        {
            TokenName = "TestToken",
            TokenStandard = "ASA",
            Network = "testnet",
            DeployerAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            CorrelationId = Guid.NewGuid().ToString(),
            MaxRetries = 3
        };
    }
}
