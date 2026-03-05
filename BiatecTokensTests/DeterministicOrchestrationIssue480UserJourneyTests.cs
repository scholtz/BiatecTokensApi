using BiatecTokensApi.Models.DeterministicOrchestration;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// User-journey tests for Issue #480: Deterministic Orchestration.
    /// Scenarios cover: happy path (HP), invalid input (II), boundary (BD),
    /// failure recovery (FR), and non-crypto-native UX (NX).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class DeterministicOrchestrationIssue480UserJourneyTests
    {
        private DeterministicOrchestrationService _service = null!;

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<DeterministicOrchestrationService>>();
            _service = new DeterministicOrchestrationService(logger.Object);
        }

        // ── HP: Happy-path user journeys ──────────────────────────────────────

        [Test]
        public async Task HP1_User_Starts_Orchestration_Sees_Draft_Stage()
        {
            var result = await _service.OrchestrateAsync(BuildRequest("hp1-token"));
            Assert.That(result.Success, Is.True);
            Assert.That(result.Stage, Is.EqualTo(OrchestrationStage.Draft));
            Assert.That(result.OrchestrationId, Is.Not.Null);
        }

        [Test]
        public async Task HP2_User_Checks_Status_Matches_Initial_Stage()
        {
            var created = await _service.OrchestrateAsync(BuildRequest("hp2-token"));
            var status = await _service.GetStatusAsync(created.OrchestrationId!, null);
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.Stage, Is.EqualTo(OrchestrationStage.Draft));
            Assert.That(status.TokenName, Is.EqualTo("hp2-token"));
        }

        [Test]
        public async Task HP3_User_Advances_Through_Draft_To_Validated()
        {
            var created = await _service.OrchestrateAsync(BuildRequest("hp3-token"));
            var adv = await _service.AdvanceAsync(new OrchestrationAdvanceRequest
            {
                OrchestrationId = created.OrchestrationId,
                CorrelationId = "hp3-corr"
            });
            Assert.That(adv.Success, Is.True);
            Assert.That(adv.CurrentStage, Is.EqualTo(OrchestrationStage.Validated));
            Assert.That(adv.NextAction, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task HP4_User_Runs_Compliance_Check_Sees_Passed()
        {
            var created = await _service.OrchestrateAsync(BuildRequest("hp4-token"));
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = created.OrchestrationId,
                RunMicaChecks = true,
                RunKycAmlChecks = true,
                CorrelationId = "hp4-corr"
            });
            Assert.That(check.Success, Is.True);
            Assert.That(check.Status, Is.EqualTo(ComplianceCheckStatus.Passed));
            Assert.That(check.Rules, Is.Not.Empty);
        }

        [Test]
        public async Task HP5_User_Advances_Full_Pipeline_To_Confirmed()
        {
            var created = await _service.OrchestrateAsync(BuildRequest("hp5-token"));
            var id = created.OrchestrationId!;
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Validated
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Queued
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Processing
            var confirmed = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Confirmed
            Assert.That(confirmed.Success, Is.True);
            Assert.That(confirmed.CurrentStage, Is.EqualTo(OrchestrationStage.Confirmed));
        }

        [Test]
        public async Task HP6_User_Completes_Orchestration()
        {
            var created = await _service.OrchestrateAsync(BuildRequest("hp6-token"));
            var id = created.OrchestrationId!;
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Validated
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Queued
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Processing
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Confirmed
            var completed = await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Completed
            Assert.That(completed.Success, Is.True);
            Assert.That(completed.CurrentStage, Is.EqualTo(OrchestrationStage.Completed));
        }

        [Test]
        public async Task HP7_User_Cancels_In_Draft_Sees_Cancelled_Stage()
        {
            var created = await _service.OrchestrateAsync(BuildRequest("hp7-token"));
            var cancel = await _service.CancelAsync(new OrchestrationCancelRequest
            {
                OrchestrationId = created.OrchestrationId,
                Reason = "User changed mind"
            });
            Assert.That(cancel.Success, Is.True);
            var status = await _service.GetStatusAsync(created.OrchestrationId!, null);
            Assert.That(status!.Stage, Is.EqualTo(OrchestrationStage.Cancelled));
        }

        // ── II: Invalid input journeys ────────────────────────────────────────

        [Test]
        public async Task II1_Missing_TokenName_Returns_Actionable_Error()
        {
            var req = BuildRequest("II1-token");
            req.TokenName = null;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task II2_Unsupported_Standard_Returns_Actionable_Error()
        {
            var req = BuildRequest("II2-token");
            req.TokenStandard = "BTC20";
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_TOKEN_STANDARD"));
            Assert.That(result.RemediationHint, Does.Contain("ASA").Or.Contain("ERC20"));
        }

        [Test]
        public async Task II3_Unsupported_Network_Returns_Actionable_Error()
        {
            var req = BuildRequest("II3-token");
            req.Network = "solana";
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_NETWORK"));
        }

        [Test]
        public async Task II4_Missing_DeployerAddress_Returns_Actionable_Error()
        {
            var req = BuildRequest("II4-token");
            req.DeployerAddress = null;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_DEPLOYER_ADDRESS"));
        }

        [Test]
        public async Task II5_Advance_Unknown_Orchestration_Returns_NotFound()
        {
            var adv = await _service.AdvanceAsync(new OrchestrationAdvanceRequest
            {
                OrchestrationId = "totally-wrong"
            });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("ORCHESTRATION_NOT_FOUND"));
        }

        [Test]
        public async Task II6_ComplianceCheck_Unknown_Orchestration_Returns_Error()
        {
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = "ghost-id"
            });
            Assert.That(check.Success, Is.False);
            Assert.That(check.ErrorCode, Is.EqualTo("ORCHESTRATION_NOT_FOUND"));
        }

        [Test]
        public async Task II7_Cancel_Terminal_Orchestration_Returns_Cannot_Cancel()
        {
            var created = await _service.OrchestrateAsync(BuildRequest("II7-token"));
            var id = created.OrchestrationId!;
            // Advance to Completed (terminal)
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            var cancel = await _service.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = id });
            Assert.That(cancel.Success, Is.False);
        }

        // ── BD: Boundary-condition journeys ───────────────────────────────────

        [Test]
        public async Task BD1_MaxRetries_Zero_IsValid()
        {
            var req = BuildRequest("BD1-token");
            req.MaxRetries = 0;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task BD2_MaxRetries_Negative_Is_Invalid()
        {
            var req = BuildRequest("BD2-token");
            req.MaxRetries = -5;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_MAX_RETRIES"));
        }

        [Test]
        public async Task BD3_EmptyNetwork_Returns_Missing_Network_Error()
        {
            var req = BuildRequest("BD3-token");
            req.Network = "   ";
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_NETWORK"));
        }

        [Test]
        public async Task BD4_LongTokenName_Succeeds()
        {
            var req = BuildRequest(new string('A', 200));
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task BD5_Idempotency_ThreeRepeatedCalls_AllReturnSameId()
        {
            var req = BuildRequest("BD5-token");
            req.IdempotencyKey = "bd5-idem-key";
            var r1 = await _service.OrchestrateAsync(req);
            var r2 = await _service.OrchestrateAsync(req);
            var r3 = await _service.OrchestrateAsync(req);
            Assert.That(r2.OrchestrationId, Is.EqualTo(r1.OrchestrationId));
            Assert.That(r3.OrchestrationId, Is.EqualTo(r1.OrchestrationId));
        }

        // ── FR: Failure-recovery journeys ─────────────────────────────────────

        [Test]
        public async Task FR1_After_Error_New_Orchestration_Can_Be_Started()
        {
            var bad = BuildRequest("FR1-bad");
            bad.TokenName = null;
            var errResult = await _service.OrchestrateAsync(bad);
            Assert.That(errResult.Success, Is.False);

            var good = await _service.OrchestrateAsync(BuildRequest("FR1-good"));
            Assert.That(good.Success, Is.True);
        }

        [Test]
        public async Task FR2_Cancel_Allows_New_Orchestration_With_Same_Token()
        {
            var created = await _service.OrchestrateAsync(BuildRequest("FR2-token"));
            await _service.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = created.OrchestrationId });

            // Start a fresh orchestration for same token (without idempotency key conflict)
            var retry = await _service.OrchestrateAsync(BuildRequest("FR2-token"));
            Assert.That(retry.Success, Is.True);
            Assert.That(retry.OrchestrationId, Is.Not.EqualTo(created.OrchestrationId));
        }

        [Test]
        public async Task FR3_Compliance_Check_After_Advance_Updates_Status()
        {
            var created = await _service.OrchestrateAsync(BuildRequest("FR3-token"));
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = created.OrchestrationId });
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = created.OrchestrationId
            });
            Assert.That(check.Success, Is.True);
            var status = await _service.GetStatusAsync(created.OrchestrationId!, null);
            Assert.That(status!.ComplianceStatus, Is.Not.EqualTo(ComplianceCheckStatus.Pending));
        }

        [Test]
        public async Task FR4_Advance_Provides_Next_Action_Guidance()
        {
            var created = await _service.OrchestrateAsync(BuildRequest("FR4-token"));
            var adv = await _service.AdvanceAsync(new OrchestrationAdvanceRequest
            {
                OrchestrationId = created.OrchestrationId
            });
            Assert.That(adv.NextAction, Is.Not.Null.And.Not.Empty);
        }

        // ── NX: Non-crypto-native UX journeys ────────────────────────────────

        [Test]
        public async Task NX1_Error_Messages_Are_Human_Readable()
        {
            var req = BuildRequest("NX1-token");
            req.TokenStandard = null;
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.ErrorMessage, Is.Not.Null);
            Assert.That(result.ErrorMessage, Does.Not.Contain("Exception"));
            Assert.That(result.ErrorMessage, Does.Not.Contain("null reference"));
        }

        [Test]
        public async Task NX2_No_Internal_Details_Leaked_In_Errors()
        {
            var req = BuildRequest("NX2-token");
            req.Network = "invalid-net";
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.ErrorMessage, Does.Not.Contain("StackTrace"));
            Assert.That(result.ErrorMessage, Does.Not.Contain("System."));
        }

        [Test]
        public async Task NX3_CorrelationId_Present_In_All_Responses()
        {
            var req = BuildRequest("NX3-token");
            req.CorrelationId = "nx3-correlation";
            var result = await _service.OrchestrateAsync(req);
            Assert.That(result.CorrelationId, Is.EqualTo("nx3-correlation"));
        }

        [Test]
        public async Task NX4_Status_Response_Shows_All_Relevant_Fields()
        {
            var req = BuildRequest("NX4-token");
            req.CorrelationId = "nx4-corr";
            var created = await _service.OrchestrateAsync(req);
            var status = await _service.GetStatusAsync(created.OrchestrationId!, null);
            Assert.That(status!.TokenName, Is.EqualTo("NX4-token"));
            Assert.That(status.Network, Is.EqualTo("testnet"));
            Assert.That(status.Stage, Is.AnyOf(Enum.GetValues<OrchestrationStage>()));
        }

        [Test]
        public async Task NX5_Audit_Trail_Readable_After_Workflow()
        {
            var req = BuildRequest("NX5-token");
            req.CorrelationId = "nx5-corr";
            var created = await _service.OrchestrateAsync(req);
            await _service.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = created.OrchestrationId });
            var events = _service.GetAuditEvents(created.OrchestrationId);
            Assert.That(events, Is.Not.Empty);
            Assert.That(events.All(e => e.Operation != null), Is.True);
        }

        [Test]
        public async Task NX6_Compliance_Rules_Have_Human_Readable_Names()
        {
            var created = await _service.OrchestrateAsync(BuildRequest("NX6-token"));
            var check = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = created.OrchestrationId,
                RunMicaChecks = true
            });
            Assert.That(check.Rules.All(r => !string.IsNullOrWhiteSpace(r.RuleName)), Is.True);
        }

        // ── Helper ─────────────────────────────────────────────────────────────

        private static OrchestrationRequest BuildRequest(string tokenName) => new()
        {
            TokenName = tokenName,
            TokenStandard = "ASA",
            Network = "testnet",
            DeployerAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            CorrelationId = Guid.NewGuid().ToString(),
            MaxRetries = 3
        };
    }
}
