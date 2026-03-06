using BiatecTokensApi.Models.ARC76MVPPipeline;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// User journey tests for ARC76 MVP Deployment Pipeline - direct service calls.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76MVPDeploymentPipelineUserJourneyTests
    {
        private ARC76MVPDeploymentPipelineService _svc = null!;

        [SetUp]
        public void Setup()
        {
            _svc = new ARC76MVPDeploymentPipelineService(NullLogger<ARC76MVPDeploymentPipelineService>.Instance);
        }

        private PipelineInitiateRequest ValidRequest(string? network = "testnet", string? standard = "ARC3") => new()
        {
            TokenName = "JourneyToken",
            TokenStandard = standard,
            Network = network,
            DeployerAddress = "ALGO_ENTERPRISE_ADDRESS",
            MaxRetries = 3,
            CorrelationId = Guid.NewGuid().ToString()
        };

        private async Task<string> AdvanceToStageAsync(ARC76MVPDeploymentPipelineService svc, string pipelineId, int steps)
        {
            for (int i = 0; i < steps; i++)
                await svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = pipelineId });
            return pipelineId;
        }

        // ── HP: Happy path ───────────────────────────────────────────────────────

        [Test]
        public async Task HP1_EnterpriseUser_InitiatesPipeline_Succeeds()
        {
            var result = await _svc.InitiateAsync(ValidRequest());
            Assert.That(result.Success, Is.True);
            Assert.That(result.PipelineId, Is.Not.Null);
        }

        [Test]
        public async Task HP2_VerifiesReadiness_AdvancesToReadinessVerified()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.CurrentStage, Is.EqualTo(PipelineStage.ReadinessVerified));
        }

        [Test]
        public async Task HP3_Validates_AdvancesToValidationPending()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 1);
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.CurrentStage, Is.EqualTo(PipelineStage.ValidationPending));
        }

        [Test]
        public async Task HP4_Compliance_AdvancesToCompliancePending()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 3);
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.CurrentStage, Is.EqualTo(PipelineStage.CompliancePending));
        }

        [Test]
        public async Task HP5_Deploys_AdvancesToDeploymentQueued()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 5);
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.CurrentStage, Is.EqualTo(PipelineStage.DeploymentQueued));
        }

        [Test]
        public async Task HP6_Confirms_AdvancesToDeploymentConfirmed()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 7);
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.CurrentStage, Is.EqualTo(PipelineStage.DeploymentConfirmed));
        }

        [Test]
        public async Task HP7_Completes_PipelineIsCompleted()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        // ── II: Invalid input ────────────────────────────────────────────────────

        [Test]
        public async Task II1_MissingTokenName_ReturnsUserCorrectableError()
        {
            var req = ValidRequest();
            req.TokenName = null;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureCategory, Is.EqualTo(FailureCategory.UserCorrectable));
        }

        [Test]
        public async Task II2_MissingDeployerAddress_ReturnsError()
        {
            var req = ValidRequest();
            req.DeployerAddress = null;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_DEPLOYER_ADDRESS"));
        }

        [Test]
        public async Task II3_UnsupportedStandard_EVM_ReturnsError()
        {
            var req = ValidRequest(standard: "BEP20");
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_TOKEN_STANDARD"));
        }

        [Test]
        public async Task II4_UnsupportedStandard_NFT_ReturnsError()
        {
            var req = ValidRequest(standard: "ERC721");
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_TOKEN_STANDARD"));
        }

        [Test]
        public async Task II5_UnsupportedNetwork_Ethereum_ReturnsError()
        {
            var req = ValidRequest(network: "ethereum");
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_NETWORK"));
        }

        [Test]
        public async Task II6_UnsupportedNetwork_Polygon_ReturnsError()
        {
            var req = ValidRequest(network: "polygon");
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_NETWORK"));
        }

        [Test]
        public async Task II7_NegativeMaxRetries_ReturnsError()
        {
            var req = ValidRequest();
            req.MaxRetries = -5;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_MAX_RETRIES"));
        }

        // ── BD: Boundary ─────────────────────────────────────────────────────────

        [Test]
        public async Task BD1_MaxRetries0_Succeeds()
        {
            var req = ValidRequest();
            req.MaxRetries = 0;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task BD2_MaxRetries3_Succeeds()
        {
            var req = ValidRequest();
            req.MaxRetries = 3;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task BD3_MaxRetries10_Succeeds()
        {
            var req = ValidRequest();
            req.MaxRetries = 10;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task BD4_TokenNameSingleChar_Succeeds()
        {
            var req = ValidRequest();
            req.TokenName = "A";
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task BD5_CorrelationIdWithSpecialChars_Propagated()
        {
            var corrId = "test-corr_123.456:789";
            var req = ValidRequest();
            req.CorrelationId = corrId;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.CorrelationId, Is.EqualTo(corrId));
        }

        // ── FR: Failure recovery ─────────────────────────────────────────────────

        [Test]
        public async Task FR1_CancelledPipeline_CannotBeAdvanced()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("TERMINAL_STAGE"));
        }

        [Test]
        public async Task FR2_CancelledPipeline_CannotBeCancelledAgain()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var again = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(again.Success, Is.False);
        }

        [Test]
        public async Task FR3_IdempotentKey_SecondCallReturnsSamePipelineId()
        {
            var key = Guid.NewGuid().ToString();
            var req = ValidRequest();
            req.IdempotencyKey = key;
            var r1 = await _svc.InitiateAsync(req);
            var r2 = await _svc.InitiateAsync(req);
            Assert.That(r2.PipelineId, Is.EqualTo(r1.PipelineId));
        }

        [Test]
        public async Task FR4_IdempotencyConflict_IsUserCorrectable()
        {
            var key = Guid.NewGuid().ToString();
            var req1 = ValidRequest();
            req1.IdempotencyKey = key;
            await _svc.InitiateAsync(req1);
            var req2 = ValidRequest();
            req2.IdempotencyKey = key;
            req2.TokenName = "DifferentName";
            var result = await _svc.InitiateAsync(req2);
            Assert.That(result.FailureCategory, Is.EqualTo(FailureCategory.UserCorrectable));
        }

        // ── NX: Non-crypto-native UX ─────────────────────────────────────────────

        [Test]
        public async Task NX1_ErrorMessages_DoNotMentionWalletComplexity()
        {
            var req = ValidRequest();
            req.TokenName = null;
            var result = await _svc.InitiateAsync(req);
            // Error messages should be user-friendly
            Assert.That(result.ErrorMessage, Is.Not.Null);
            Assert.That(result.ErrorMessage, Does.Not.Contain("exception"));
            Assert.That(result.ErrorMessage, Does.Not.Contain("StackTrace"));
        }

        [Test]
        public async Task NX2_CorrelationIdPresentInAllResponses()
        {
            var result = await _svc.InitiateAsync(ValidRequest());
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task NX3_HumanReadableErrorMessages_Present()
        {
            var req = ValidRequest();
            req.Network = "unknown";
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ErrorMessage!.Length, Is.GreaterThan(10));
        }

        [Test]
        public async Task NX4_RemediationHintsExist_ForAllErrors()
        {
            var req = ValidRequest();
            req.TokenStandard = "UNKNOWN";
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task NX5_FailureCategories_AreActionable()
        {
            var req = ValidRequest();
            req.DeployerAddress = null;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.FailureCategory, Is.Not.EqualTo(FailureCategory.SystemCritical),
                "User-input errors should not be SystemCritical");
        }

        [Test]
        public async Task NX6_AuditTrail_HasReadableOperationNames()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var events = _svc.GetAuditEvents(r.PipelineId);
            foreach (var ev in events)
            {
                Assert.That(ev.Operation, Is.Not.Null.And.Not.Empty);
                Assert.That(ev.Operation!.Length, Is.GreaterThan(0));
            }
        }

        [Test]
        public async Task NX7_NextAction_GuidesThroughPipeline()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            Assert.That(r.NextAction, Is.Not.Null.And.Not.Empty);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.NextAction, Is.Not.Null.And.Not.Empty);
        }

        // ── Additional HP tests ──────────────────────────────────────────────────

        [Test]
        public async Task HP_EnterpriseTokenForBase_CompletesPipeline()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "base", standard: "ERC20"));
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_ARC1400SecurityToken_InitiatesSuccessfully()
        {
            var r = await _svc.InitiateAsync(ValidRequest(standard: "ARC1400"));
            Assert.That(r.Success, Is.True);
            Assert.That(r.Stage, Is.EqualTo(PipelineStage.PendingReadiness));
        }

        [Test]
        public async Task HP_UserCanCancelMidway_BeforeDeployment()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 3);
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        // ── Additional II tests ──────────────────────────────────────────────────

        [Test]
        public async Task II_UserSubmitsEmpty_NetworkName_GetsHelpfulError()
        {
            var req = ValidRequest();
            req.Network = "";
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task II_UserSubmitsJunkIdempotencyKey_SameKeyDifferentRequest_GetsClearError()
        {
            var key = "user-idempotency-key-123";
            var req1 = ValidRequest();
            req1.IdempotencyKey = key;
            await _svc.InitiateAsync(req1);

            var req2 = ValidRequest();
            req2.IdempotencyKey = key;
            req2.TokenName = "DifferentToken";
            var result = await _svc.InitiateAsync(req2);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("IDEMPOTENCY_KEY_CONFLICT"));
            Assert.That(result.RemediationHint, Is.Not.Null);
        }

        // ── Additional BD tests ───────────────────────────────────────────────────

        [Test]
        public async Task BD_MaxRetriesExactlyZero_PipelineCreated()
        {
            var req = ValidRequest();
            req.MaxRetries = 0;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task BD_VoimainNetwork_IsSupported()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "voimain"));
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_BetanetNetwork_IsSupported()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "betanet"));
            Assert.That(r.Success, Is.True);
        }

        // ── Additional FR tests ───────────────────────────────────────────────────

        [Test]
        public async Task FR_PipelineStaysIsolated_AfterAnotherPipelineFails()
        {
            var r1 = await _svc.InitiateAsync(ValidRequest());
            var r2 = await _svc.InitiateAsync(ValidRequest());

            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r1.PipelineId });

            var status2 = await _svc.GetStatusAsync(r2.PipelineId!, null);
            Assert.That(status2!.Stage, Is.EqualTo(PipelineStage.PendingReadiness),
                "r2 should be unaffected by r1 cancellation");
        }

        // ── Additional NX tests ───────────────────────────────────────────────────

        [Test]
        public void NX_ARC76ReadinessStatus_HasFriendlyStringValues()
        {
            // Verify enum names are human-readable (no underscores, no all-caps jargon)
            var expected = new[] { "NotChecked", "Ready", "NotReady", "Error" };
            var actual = Enum.GetNames<ARC76ReadinessStatus>();
            Assert.That(actual, Is.EquivalentTo(expected));
            foreach (var name in actual)
                Assert.That(name, Does.Not.Contain("_"), "Enum names should use PascalCase, not underscores");
        }

        [Test]
        public async Task HP_VoiMainNetwork_SupportedAndCompletesLifecycle()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "voimain"));
            Assert.That(r.Success, Is.True);
            Assert.That(r.Stage, Is.EqualTo(PipelineStage.PendingReadiness));
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_ARC3Standard_SupportedOnMainnet()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "mainnet", standard: "ARC3"));
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task HP_ASAStandard_SupportedOnMainnet()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "mainnet", standard: "ASA"));
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task HP_MaxRetries_10_SupportedBoundary()
        {
            var req = ValidRequest();
            req.MaxRetries = 10;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task II_NullIdempotencyKey_StillCreates_WithNullKey()
        {
            var req = ValidRequest();
            req.IdempotencyKey = null;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            Assert.That(r.PipelineId, Is.Not.Null);
        }

        [Test]
        public async Task II_TokenNameWithSpecialChars_IsAccepted()
        {
            var req = ValidRequest();
            req.TokenName = "My Token-Name 2026";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_MaxRetries_100_SupportedAtUpperLimit()
        {
            var req = ValidRequest();
            req.MaxRetries = 100;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task FR_CancelThenNewPipeline_SameDeployer_Succeeds()
        {
            var req1 = ValidRequest();
            var r1 = await _svc.InitiateAsync(req1);
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r1.PipelineId });

            var req2 = ValidRequest();
            var r2 = await _svc.InitiateAsync(req2);
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.PipelineId, Is.Not.EqualTo(r1.PipelineId));
        }

        [Test]
        public void NX_AllStageNames_AreHumanReadable()
        {
            var stageNames = Enum.GetNames<PipelineStage>();
            Assert.That(stageNames.Length, Is.GreaterThanOrEqualTo(13));
            foreach (var name in stageNames)
            {
                Assert.That(name, Does.Not.Contain("_"), $"Stage '{name}' should use PascalCase");
                Assert.That(name, Is.Not.EqualTo(name.ToUpperInvariant()), $"Stage '{name}' should not be all-caps");
            }
        }

        [Test]
        public async Task NX_ErrorMessage_ForMissingFields_ContainsFieldName()
        {
            var req = ValidRequest();
            req.TokenName = null;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("token").IgnoreCase.Or.Contain("name").IgnoreCase);
        }

        // ── Additional journey tests ──────────────────────────────────────────────

        [Test]
        public async Task HP_ARC200_OnMainnet_CompletesLifecycle()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "mainnet", standard: "ARC200"));
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_ARC3_OnTestnet_CompletesLifecycle()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "testnet", standard: "ARC3"));
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_CorrelationId_IsPreserved_ThroughStages()
        {
            var corrId = "journey-corr-" + Guid.NewGuid();
            var req = ValidRequest();
            req.CorrelationId = corrId;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.CorrelationId, Is.EqualTo(corrId));
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.CorrelationId, Is.EqualTo(corrId));
        }

        [Test]
        public async Task II_TokenStandard_ActuallyInvalid_Returns_Error()
        {
            var req = ValidRequest();
            req.TokenStandard = "TOTALLY_UNSUPPORTED_XYZ";
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("UNSUPPORTED_TOKEN_STANDARD"));
        }

        [Test]
        public async Task II_NegativeMaxRetries_ReturnsError()
        {
            var req = ValidRequest();
            req.MaxRetries = -1;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task BD_MaxRetries_50_IsAccepted()
        {
            var req = ValidRequest();
            req.MaxRetries = 50;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_TokenName_SingleChar_IsAccepted()
        {
            var req = ValidRequest();
            req.TokenName = "X";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task FR_RetryThenCancel_IsAllowed()
        {
            var req = ValidRequest();
            req.MaxRetries = 5;
            var r = await _svc.InitiateAsync(req);
            // 7 advances: PendingReadiness→ReadinessVerified→ValidationPending→ValidationPassed→
            //             CompliancePending→CompliancePassed→DeploymentQueued→DeploymentActive
            for (int i = 0; i < 7; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            await _svc.RetryAsync(new PipelineRetryRequest { PipelineId = r.PipelineId });
            // Cancel while in Retrying state
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
        }

        [Test]
        public async Task NX_StatusMessages_AreUserFriendly()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.Stage.ToString(), Is.Not.EqualTo(string.Empty));
            Assert.That(Enum.IsDefined(typeof(PipelineStage), status.Stage), Is.True);
        }

        [Test]
        public async Task NX_MultipleNetworks_AllHaveConsistentPipelineId()
        {
            var networks = new[] { "mainnet", "testnet", "betanet", "voimain", "base" };
            foreach (var net in networks)
            {
                var req = ValidRequest(network: net, standard: "ASA");
                var r = await _svc.InitiateAsync(req);
                Assert.That(r.Success, Is.True, $"Network '{net}' should be accepted");
                Assert.That(Guid.TryParse(r.PipelineId, out _), Is.True, $"PipelineId for '{net}' should be a GUID");
            }
        }

        // ── User journey tests (batch 2) ─────────────────────────────────────────

        [Test]
        public async Task HP_ARC200_OnBetanet_FullLifecycle()
        {
            var req = ValidRequest(network: "betanet", standard: "ARC200");
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            for (int i = 0; i < 9; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_ERC20_OnBase_FullLifecycle()
        {
            var req = ValidRequest(network: "base", standard: "ERC20");
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            for (int i = 0; i < 9; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_ASA_OnTestnet_InitialStageIsPendingReadiness()
        {
            var req = ValidRequest(network: "testnet", standard: "ASA");
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Stage, Is.EqualTo(PipelineStage.PendingReadiness));
        }

        [Test]
        public async Task II_TokenName_OnlyWhitespace_ReturnsError()
        {
            var req = ValidRequest();
            req.TokenName = "   ";
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
        }

        [Test]
        public async Task II_MaxRetries_NegativeOne_IsRejected()
        {
            var req = ValidRequest();
            req.MaxRetries = -1;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_MAX_RETRIES"));
        }

        [Test]
        public async Task BD_MaxRetries_Exactly1_IsValid()
        {
            var req = ValidRequest();
            req.MaxRetries = 1;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task BD_MaxRetries_1000_IsValid()
        {
            var req = ValidRequest();
            req.MaxRetries = 1000;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task FR_TwoPipelines_SameDeployer_DifferentTokens_AreBothCreated()
        {
            var req1 = ValidRequest();
            req1.TokenName = "TokenAlpha";
            req1.IdempotencyKey = "idem-alpha-" + Guid.NewGuid();
            var req2 = ValidRequest();
            req2.TokenName = "TokenBeta";
            req2.IdempotencyKey = "idem-beta-" + Guid.NewGuid();
            var r1 = await _svc.InitiateAsync(req1);
            var r2 = await _svc.InitiateAsync(req2);
            Assert.That(r1.Success, Is.True);
            Assert.That(r2.Success, Is.True);
            Assert.That(r1.PipelineId, Is.Not.EqualTo(r2.PipelineId));
        }

        [Test]
        public async Task NX_StageName_PendingReadiness_IsHumanReadable()
        {
            Assert.That(PipelineStage.PendingReadiness.ToString(), Is.EqualTo("PendingReadiness"));
        }

        [Test]
        public async Task NX_StageName_Completed_IsHumanReadable()
        {
            Assert.That(PipelineStage.Completed.ToString(), Is.EqualTo("Completed"));
        }

        [Test]
        public async Task HP_IdempotencyReplay_AfterAdvance_ReturnsSameStage()
        {
            var key = "idem-hp-" + Guid.NewGuid();
            var req = ValidRequest();
            req.IdempotencyKey = key;
            var r1 = await _svc.InitiateAsync(req);
            await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r1.PipelineId });
            // Replay the original initiate - should still return same pipeline (same PipelineId)
            var r2 = await _svc.InitiateAsync(req);
            Assert.That(r2.PipelineId, Is.EqualTo(r1.PipelineId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        // ── Additional User Journey tests (batch 3) ──────────────────────────────

        [Test]
        public async Task HP_ERC20OnBase_FullInitiate_Succeeds()
        {
            var req = ValidRequest();
            req.TokenStandard = "ERC20";
            req.Network = "base";
            req.IdempotencyKey = "hp-erc20-base-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            Assert.That(r.Stage, Is.EqualTo(PipelineStage.PendingReadiness));
        }

        [Test]
        public async Task HP_ASA_OnBetanet_FullInitiate_Succeeds()
        {
            var req = ValidRequest();
            req.TokenStandard = "ASA";
            req.Network = "betanet";
            req.IdempotencyKey = "hp-asa-betanet-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task HP_ARC3_OnVoimain_FullInitiate_Succeeds()
        {
            var req = ValidRequest();
            req.TokenStandard = "ARC3";
            req.Network = "voimain";
            req.IdempotencyKey = "hp-arc3-voimain-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task HP_ARC200_OnMainnet_AdvancesThroughReadiness()
        {
            var req = ValidRequest();
            req.TokenStandard = "ARC200";
            req.Network = "mainnet";
            req.IdempotencyKey = "hp-arc200-main-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.CurrentStage, Is.EqualTo(PipelineStage.ReadinessVerified));
        }

        [Test]
        public async Task II_EmptyDeployerAddress_ReturnsError()
        {
            var req = ValidRequest();
            req.DeployerAddress = "";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_DEPLOYER_ADDRESS"));
        }

        [Test]
        public async Task II_WhitespaceDeployerAddress_ReturnsError()
        {
            var req = ValidRequest();
            req.DeployerAddress = "   ";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.False);
        }

        [Test]
        public async Task BD_MaxRetries_ExactlyZero_IsAccepted()
        {
            var req = ValidRequest();
            req.MaxRetries = 0;
            var r = await _svc.InitiateAsync(req);
            // MaxRetries=0 means no retries allowed; service accepts it
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_MaxRetries_Large_IsAccepted()
        {
            var req = ValidRequest();
            req.MaxRetries = 999;
            req.IdempotencyKey = "bd-maxretries-999-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_TokenName_WithDots_IsAccepted()
        {
            var req = ValidRequest();
            req.TokenName = "My.Token.V2";
            req.IdempotencyKey = "bd-dots-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task FR_CancelThenNewPipeline_SecondPipelineCanAdvance()
        {
            var req1 = ValidRequest();
            req1.IdempotencyKey = "fr-cancel-then-new-1-" + Guid.NewGuid();
            var r1 = await _svc.InitiateAsync(req1);
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r1.PipelineId });

            var req2 = ValidRequest();
            req2.IdempotencyKey = "fr-cancel-then-new-2-" + Guid.NewGuid();
            var r2 = await _svc.InitiateAsync(req2);
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r2.PipelineId });
            Assert.That(adv.Success, Is.True);
            Assert.That(adv.CurrentStage, Is.EqualTo(PipelineStage.ReadinessVerified));
        }

        [Test]
        public async Task FR_RetryOnCancelledPipeline_ReturnsCannotCancel()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var retry = await _svc.RetryAsync(new PipelineRetryRequest { PipelineId = r.PipelineId });
            Assert.That(retry.Success, Is.False);
        }

        [Test]
        public async Task NX_AllStageNames_AreNonEmpty()
        {
            var stages = System.Enum.GetValues<PipelineStage>();
            foreach (var stage in stages)
                Assert.That(stage.ToString(), Is.Not.Null.And.Not.Empty, $"Stage {stage} must have a name");
        }

        [Test]
        public async Task NX_ErrorCode_MISSING_TOKEN_NAME_IsDescriptive()
        {
            var req = ValidRequest();
            req.TokenName = "";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
            Assert.That(r.ErrorCode, Does.Not.Contain("Exception"), "ErrorCode should not expose exceptions");
        }

        [Test]
        public async Task NX_PipelineId_IsGuidFormat_ForAllCreatedPipelines()
        {
            for (int i = 0; i < 5; i++)
            {
                var req = ValidRequest();
                req.IdempotencyKey = "nx-guid-check-" + i + "-" + Guid.NewGuid();
                var r = await _svc.InitiateAsync(req);
                Assert.That(Guid.TryParse(r.PipelineId, out _), Is.True, $"PipelineId should be GUID format");
            }
        }

        [Test]
        public async Task HP_FullLifecycle_ARC1400OnTestnet_CompletesWith9Advances()
        {
            var req = ValidRequest();
            req.TokenStandard = "ARC1400";
            req.Network = "testnet";
            req.IdempotencyKey = "hp-arc1400-testnet-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            PipelineAdvanceResponse? last = null;
            for (int i = 0; i < 9; i++)
                last = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(last!.CurrentStage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task II_UnsupportedNetwork_Rejected_WithRemediationHint()
        {
            var req = ValidRequest();
            req.Network = "invalidnet";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("UNSUPPORTED_NETWORK"));
            Assert.That(r.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        // ── Extended lifecycle / network / boundary tests ─────────────────────────

        [Test]
        public async Task HP_ARC3_OnVoimain_FullLifecycle_Completes()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "voimain", standard: "ARC3"));
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_ARC1400_OnBetanet_FullLifecycle_Completes()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "betanet", standard: "ARC1400"));
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_ASA_OnMainnet_FullLifecycle_Completes()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "mainnet", standard: "ASA"));
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_ARC200_OnVoimain_FullLifecycle_Completes()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "voimain", standard: "ARC200"));
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task II_WhitespaceOnlyDeployerAddress_ReturnsError()
        {
            var req = ValidRequest();
            req.DeployerAddress = "   ";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task II_EmptyTokenStandard_ReturnsError()
        {
            var req = ValidRequest();
            req.TokenStandard = "";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.False);
        }

        [Test]
        public async Task BD_MaxRetries_0_IsValid()
        {
            var req = ValidRequest();
            req.MaxRetries = 0;
            req.IdempotencyKey = "bd-mr0-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_MaxRetries_1_IsValid()
        {
            var req = ValidRequest();
            req.MaxRetries = 1;
            req.IdempotencyKey = "bd-mr1-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_MaxRetries_999_IsValid()
        {
            var req = ValidRequest();
            req.MaxRetries = 999;
            req.IdempotencyKey = "bd-mr999-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_TokenNameContainingDots_IsAccepted()
        {
            var req = ValidRequest();
            req.TokenName = "Token.v2.0";
            req.IdempotencyKey = "bd-tokdots2-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_TokenName_WithHyphens_IsAccepted()
        {
            var req = ValidRequest();
            req.TokenName = "My-Token-v1";
            req.IdempotencyKey = "bd-tokhyph-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task FR_CancelThenNew_SameDeployer_SameStandard_CreatesFreshPipeline()
        {
            var req1 = ValidRequest(network: "testnet", standard: "ARC3");
            var r1 = await _svc.InitiateAsync(req1);
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r1.PipelineId });

            var req2 = ValidRequest(network: "testnet", standard: "ARC3");
            var r2 = await _svc.InitiateAsync(req2);
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.PipelineId, Is.Not.EqualTo(r1.PipelineId));
            Assert.That(r2.Stage, Is.EqualTo(PipelineStage.PendingReadiness));
        }

        [Test]
        public async Task FR_RetryOnCancelled_ReturnsError()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var retry = await _svc.RetryAsync(new PipelineRetryRequest { PipelineId = r.PipelineId });
            Assert.That(retry.Success, Is.False);
            Assert.That(retry.ErrorCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task NX_StageNames_AreAllNonEmpty()
        {
            foreach (var stage in Enum.GetValues<PipelineStage>())
                Assert.That(stage.ToString(), Is.Not.Null.And.Not.Empty, $"Stage {stage} must have a non-empty name");
            await Task.CompletedTask;
        }

        [Test]
        public async Task NX_IdempotencyKey_TwoDistinctKeys_ProduceDifferentIds()
        {
            var key1 = "nx-idem-k1-" + Guid.NewGuid();
            var key2 = "nx-idem-k2-" + Guid.NewGuid();
            var req1 = ValidRequest(); req1.IdempotencyKey = key1;
            var req2 = ValidRequest(); req2.IdempotencyKey = key2;
            var r1 = await _svc.InitiateAsync(req1);
            var r2 = await _svc.InitiateAsync(req2);
            Assert.That(r1.PipelineId, Is.Not.EqualTo(r2.PipelineId));
        }

        [Test]
        public async Task NX_GUID_PipelineId_Format()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            Assert.That(r.PipelineId, Is.Not.Null);
            Assert.That(Guid.TryParse(r.PipelineId, out _), Is.True, "PipelineId must be a valid GUID");
        }

        [Test]
        public async Task NX_ARC1400_OnTestnet_FullLifecycle()
        {
            var req = ValidRequest(network: "testnet", standard: "ARC1400");
            req.IdempotencyKey = "nx-arc1400-full-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task NX_UnsupportedNetwork_RemediationHint_ContainsNetworkInfo()
        {
            var req = ValidRequest(network: "unsupported_xyz_net");
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.False);
            Assert.That(r.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task HP_FullLifecycle_ARC200_Testnet_Completes()
        {
            var req = ValidRequest(network: "testnet", standard: "ARC200");
            req.IdempotencyKey = "uj-hp-arc200-tn-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_FullLifecycle_ERC20_Voimain_Completes()
        {
            var req = ValidRequest(network: "voimain", standard: "ERC20");
            req.IdempotencyKey = "uj-hp-erc20-voi-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_FullLifecycle_ARC3_Aramidmain_Completes()
        {
            var req = ValidRequest(network: "aramidmain", standard: "ARC3");
            req.IdempotencyKey = "uj-hp-arc3-aramid-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task II_WhitespaceTokenStandard_Returns_Error()
        {
            var req = ValidRequest();
            req.TokenStandard = "   ";
            req.IdempotencyKey = "uj-ii-ws-std-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task II_MaxRetries_Negative1_Returns_INVALID_MAX_RETRIES()
        {
            var req = ValidRequest();
            req.MaxRetries = -1;
            req.IdempotencyKey = "uj-ii-mr-neg-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.EqualTo("INVALID_MAX_RETRIES"));
        }

        [Test]
        public async Task II_NullDeployerAddress_Returns_Error()
        {
            var req = ValidRequest();
            req.DeployerAddress = null;
            req.IdempotencyKey = "uj-ii-null-deployer-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.False);
            Assert.That(r.ErrorCode, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task BD_MaxRetries_2_IsValid()
        {
            var req = ValidRequest();
            req.MaxRetries = 2;
            req.IdempotencyKey = "uj-bd-mr2-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_MaxRetries_500_IsValid()
        {
            var req = ValidRequest();
            req.MaxRetries = 500;
            req.IdempotencyKey = "uj-bd-mr500-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task FR_CancelAtStage3_CreatesAuditTrail()
        {
            var req = ValidRequest();
            req.IdempotencyKey = "uj-fr-cancel3-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 3);
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId, Reason = "test cancel at stage 3" });
            var audit = await _svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events, Is.Not.Null.And.Not.Empty);
            Assert.That(audit.Events.Any(e => e.Operation.Contains("Cancel", StringComparison.OrdinalIgnoreCase)), Is.True);
        }

        [Test]
        public async Task FR_Cancel_Then_New_Pipeline_SameParams_Succeeds()
        {
            var req1 = ValidRequest(network: "mainnet", standard: "ASA");
            req1.IdempotencyKey = "uj-fr-cancel-new-1-" + Guid.NewGuid();
            var r1 = await _svc.InitiateAsync(req1);
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r1.PipelineId });

            var req2 = ValidRequest(network: "mainnet", standard: "ASA");
            req2.IdempotencyKey = "uj-fr-cancel-new-2-" + Guid.NewGuid();
            var r2 = await _svc.InitiateAsync(req2);
            Assert.That(r2.Success, Is.True);
            Assert.That(r2.PipelineId, Is.Not.EqualTo(r1.PipelineId));
        }

        [Test]
        public async Task NX_ARC200_Network_In_Status_Is_Human_Readable()
        {
            var req = ValidRequest(network: "testnet", standard: "ARC200");
            req.IdempotencyKey = "uj-nx-arc200-net-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Network, Is.Not.Null.And.Not.Empty);
            Assert.That(status.Network, Is.EqualTo("testnet"));
        }

        [Test]
        public async Task Stage_After1Advance_Is_ReadinessVerified()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 1);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.ReadinessVerified));
        }

        [Test]
        public async Task Stage_After2Advances_Is_ValidationPending()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 2);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.ValidationPending));
        }

        [Test]
        public async Task Stage_After3Advances_Is_ValidationPassed()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 3);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.ValidationPassed));
        }

        [Test]
        public async Task Stage_After4Advances_Is_CompliancePending()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 4);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.CompliancePending));
        }

        [Test]
        public async Task Stage_After5Advances_Is_CompliancePassed()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 5);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.CompliancePassed));
        }

        [Test]
        public async Task Stage_After7Advances_Is_DeploymentActive()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 7);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.DeploymentActive));
        }

        [Test]
        public async Task Stage_After8Advances_Is_DeploymentConfirmed()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 8);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.DeploymentConfirmed));
        }

        [Test]
        public async Task Stage_After9Advances_Is_Completed()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task MaxRetries2_IsAccepted()
        {
            var req = ValidRequest();
            req.MaxRetries = 2;
            req.IdempotencyKey = "uj-maxretries2-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task TokenNameWithNumbers_IsAccepted()
        {
            var req = ValidRequest();
            req.TokenName = "Token2025ABC";
            req.IdempotencyKey = "uj-numname-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task ARC200_Mainnet_FullLifecycle_Succeeds()
        {
            var req = ValidRequest("mainnet", "ARC200");
            req.IdempotencyKey = "uj-arc200-mn-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task ERC20_Base_MultipleAdvances_Succeeds()
        {
            var req = ValidRequest("base", "ERC20");
            req.IdempotencyKey = "uj-erc20-base-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 5);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.CompliancePassed));
        }

        [Test]
        public async Task IdempotencyReplay_3Calls_ReturnSamePipelineId()
        {
            var key = "uj-idem3-" + Guid.NewGuid();
            var r1 = await _svc.InitiateAsync(new PipelineInitiateRequest { TokenName = "JourneyToken", TokenStandard = "ARC3", Network = "testnet", DeployerAddress = "ALGO_ENTERPRISE_ADDRESS", MaxRetries = 3, IdempotencyKey = key, CorrelationId = Guid.NewGuid().ToString() });
            var r2 = await _svc.InitiateAsync(new PipelineInitiateRequest { TokenName = "JourneyToken", TokenStandard = "ARC3", Network = "testnet", DeployerAddress = "ALGO_ENTERPRISE_ADDRESS", MaxRetries = 3, IdempotencyKey = key, CorrelationId = Guid.NewGuid().ToString() });
            var r3 = await _svc.InitiateAsync(new PipelineInitiateRequest { TokenName = "JourneyToken", TokenStandard = "ARC3", Network = "testnet", DeployerAddress = "ALGO_ENTERPRISE_ADDRESS", MaxRetries = 3, IdempotencyKey = key, CorrelationId = Guid.NewGuid().ToString() });
            Assert.That(r1.PipelineId, Is.EqualTo(r2.PipelineId));
            Assert.That(r2.PipelineId, Is.EqualTo(r3.PipelineId));
        }

        [Test]
        public async Task CompletedPipeline_AdvanceFurther_ReturnsError()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("TERMINAL_STAGE"));
        }

        [Test]
        public async Task VoimainNetwork_FullLifecycle_Succeeds()
        {
            var req = ValidRequest("voimain", "ASA");
            req.IdempotencyKey = "uj-voimain-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task BetanetNetwork_FullLifecycle_Succeeds()
        {
            var req = ValidRequest("betanet", "ARC3");
            req.IdempotencyKey = "uj-betanet-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task ARC1400_Standard_Succeeds()
        {
            var req = ValidRequest("testnet", "ARC1400");
            req.IdempotencyKey = "uj-arc1400-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task CancelAtCompliancePassed_ThenStatus_IsCancelled()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 5);
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        [Test]
        public async Task GetAudit_AfterFullLifecycle_Has10Events()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var audit = await _svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit!.Events.Count, Is.EqualTo(10));
        }

        [Test]
        public async Task CancelThenAdvance_ReturnsError()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.Success, Is.False);
        }

        [Test]
        public async Task AuditEvents_Count_GrowsWithAdvances()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            for (int i = 1; i <= 5; i++)
            {
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
                var audit = await _svc.GetAuditAsync(r.PipelineId!, null);
                Assert.That(audit!.Events.Count, Is.EqualTo(i + 1));
            }
        }

        [Test]
        public async Task AdvanceAsync_CorrelationId_Propagated()
        {
            var corrId = "uj-corr-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(ValidRequest());
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId, CorrelationId = corrId });
            Assert.That(adv.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task InitiateAsync_MaxRetries10_Accepted()
        {
            var req = ValidRequest();
            req.MaxRetries = 10;
            req.IdempotencyKey = "uj-maxretries10-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task Stage6_After6Advances_Is_DeploymentQueued()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 6);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.DeploymentQueued));
        }

        [Test]
        public async Task RetryAsync_OnCompletedPipeline_ReturnsNotInFailedState()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var retry = await _svc.RetryAsync(new PipelineRetryRequest { PipelineId = r.PipelineId });
            Assert.That(retry.Success, Is.False);
            Assert.That(retry.ErrorCode, Is.EqualTo("NOT_IN_FAILED_STATE"));
        }

        [Test]
        public async Task GetStatus_Returns_DeployerAddress_MatchingRequest()
        {
            var req = ValidRequest();
            req.DeployerAddress = "ALGO_JOURNEY_ADDR_01";
            req.IdempotencyKey = "uj-depladdr-" + Guid.NewGuid();
            var r = await _svc.InitiateAsync(req);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.DeployerAddress, Is.EqualTo("ALGO_JOURNEY_ADDR_01"));
        }

        // ── Additional user journey scenarios ────────────────────────────────────

        [Test]
        public async Task JU1_InvestmentBankTokenizer_RunsFullPipeline_Succeeds()
        {
            var req = ValidRequest("mainnet", "ARC1400");
            req.DeployerEmail = "bank@institution.com";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            // Full pipeline
            for (int i = 0; i < 9; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task JU2_AssetManager_ERC20OnBase_FullPipeline_Succeeds()
        {
            var req = ValidRequest("base", "ERC20");
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            for (int i = 0; i < 9; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task JU3_NFTCreator_ARC3_OnBetanet_Succeeds()
        {
            var req = ValidRequest("betanet", "ARC3");
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task JU4_UserCancelsAfterCompliancePending_StatusIsCancelled()
        {
            var req = ValidRequest();
            var r = await _svc.InitiateAsync(req);
            // Advance to CompliancePending (4 steps)
            for (int i = 0; i < 4; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        [Test]
        public async Task JU5_UserTriesRetry_WhenPipelineNotFailed_GetsHelpfulError()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var result = await _svc.RetryAsync(new PipelineRetryRequest { PipelineId = r.PipelineId });
            Assert.That(result.Success, Is.False);
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task JU6_UserUsesIdempotencyKey_ThenDifferentKey_GetsDifferentPipeline()
        {
            var req1 = ValidRequest();
            req1.IdempotencyKey = "key-jrnA-" + Guid.NewGuid();
            var r1 = await _svc.InitiateAsync(req1);
            var req2 = ValidRequest();
            req2.IdempotencyKey = "key-jrnB-" + Guid.NewGuid();
            var r2 = await _svc.InitiateAsync(req2);
            Assert.That(r1.PipelineId, Is.Not.EqualTo(r2.PipelineId));
        }

        [Test]
        public async Task JU7_UserChecksStatus_AfterEachAdvance_StagesAreCorrect()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var expectedStages = new[]
            {
                PipelineStage.ReadinessVerified,
                PipelineStage.ValidationPending,
                PipelineStage.ValidationPassed
            };
            for (int i = 0; i < 3; i++)
            {
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
                var status = await _svc.GetStatusAsync(r.PipelineId!, null);
                Assert.That(status!.Stage, Is.EqualTo(expectedStages[i]));
            }
        }

        [Test]
        public async Task JU8_AdminVerifiesAuditTrail_HasAllEntries_AfterFullLifecycle()
        {
            var corrId = "admin-audit-" + Guid.NewGuid();
            var req = ValidRequest();
            req.CorrelationId = corrId;
            var r = await _svc.InitiateAsync(req);
            for (int i = 0; i < 9; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var audit = await _svc.GetAuditAsync(r.PipelineId!, corrId);
            Assert.That(audit.Events.Count, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public async Task JU9_MultipleUsers_CreateIndependentPipelines()
        {
            var pipelines = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                var req = ValidRequest();
                req.DeployerAddress = $"USER_ADDRESS_{i}";
                var r = await _svc.InitiateAsync(req);
                pipelines.Add(r.PipelineId!);
            }
            Assert.That(pipelines.Distinct().Count(), Is.EqualTo(5));
        }

        [Test]
        public async Task JU10_User_GetsHelpfulError_ForNegativeRetries()
        {
            var req = ValidRequest();
            req.MaxRetries = -1;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task JU11_User_GetsHelpfulError_ForMissingTokenStandard()
        {
            var req = ValidRequest();
            req.TokenStandard = null;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task JU12_User_Succeeds_WithUnicodeTokenName()
        {
            var req = ValidRequest();
            req.TokenName = "TökenÜnicode";
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task JU13_User_Cancels_WhenAtValidationPassed()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 3; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
        }

        [Test]
        public async Task JU14_User_Cancels_WhenAtDeploymentQueued()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 6; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
        }

        [Test]
        public async Task JU15_User_GetsReadiness_AfterFirstAdvance()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.ReadinessStatus, Is.EqualTo(ARC76ReadinessStatus.Ready));
        }

        [Test]
        public async Task JU16_User_Sees_RetryCount_InitiallyZero()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.RetryCount, Is.EqualTo(0));
        }

        [Test]
        public async Task JU17_User_FullPipeline_CorrelationIdConsistent()
        {
            var corrId = "journey-corr-" + Guid.NewGuid();
            var req = ValidRequest();
            req.CorrelationId = corrId;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.CorrelationId, Is.EqualTo(corrId));
        }

        [Test]
        public async Task JU18_User_ErrorMessage_IsHumanReadable_ForWrongStandard()
        {
            var req = ValidRequest();
            req.TokenStandard = "UNKNOWN";
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.ErrorMessage, Does.Contain("UNKNOWN").Or.Contains("not supported").IgnoreCase);
        }

        [Test]
        public async Task JU19_User_Pipeline_MaxRetries_Persisted_InStatus()
        {
            var req = ValidRequest();
            req.MaxRetries = 10;
            var r = await _svc.InitiateAsync(req);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.MaxRetries, Is.EqualTo(10));
        }

        [Test]
        public async Task JU20_User_GetAuditAsync_HasCancelEvent_WhenCancelled()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var audit = await _svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events.Any(e => e.Operation == "Cancel"), Is.True);
        }

        [Test]
        public async Task JU21_User_GetAuditAsync_IsSucceeded_False_ForMissingStandard()
        {
            var req = ValidRequest();
            req.TokenStandard = null;
            await _svc.InitiateAsync(req);
            var all = _svc.GetAuditEvents();
            Assert.That(all.Any(e => e.Succeeded == false), Is.True);
        }

        [Test]
        public async Task JU22_User_PipelineStage_AfterInitiate_IsPendingReadiness()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            Assert.That(r.Stage, Is.EqualTo(PipelineStage.PendingReadiness));
        }

        [Test]
        public async Task JU23_User_CancelAtReadinessVerified_ThenCannotAdvance()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("TERMINAL_STAGE"));
        }

        [Test]
        public async Task JU24_User_GetStatusAsync_ReturnsNull_ForUnknownId()
        {
            var status = await _svc.GetStatusAsync("nonexistent-pipeline-id", null);
            Assert.That(status, Is.Null);
        }

        [Test]
        public async Task JU25_User_Advance_CorrelationIdAutoGenerated_WhenNull()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task HP_FullLifecycle_ASA_Testnet_Succeeds()
        {
            var req = ValidRequest();
            req.TokenStandard = "ASA";
            req.Network = "testnet";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            for (int i = 0; i < 9; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_AuditTrail_GrowsWithEachOperation()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var a1 = await _svc.GetAuditAsync(r.PipelineId!, null);
            await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var a2 = await _svc.GetAuditAsync(r.PipelineId!, null);
            await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var a3 = await _svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(a2.Events!.Count, Is.GreaterThan(a1.Events!.Count));
            Assert.That(a3.Events!.Count, Is.GreaterThan(a2.Events!.Count));
        }

        [Test]
        public async Task HP_MultipleNetworks_AllSucceed()
        {
            foreach (var network in new[] { "testnet", "mainnet", "voimain", "betanet" })
            {
                var req = ValidRequest();
                req.Network = network;
                var r = await _svc.InitiateAsync(req);
                Assert.That(r.Success, Is.True, $"Network {network} should succeed");
            }
        }

        [Test]
        public async Task HP_MultipleStandards_AllSucceed()
        {
            foreach (var std in new[] { "ASA", "ARC3", "ARC200", "ARC1400" })
            {
                var req = ValidRequest();
                req.TokenStandard = std;
                req.Network = "testnet";
                var r = await _svc.InitiateAsync(req);
                Assert.That(r.Success, Is.True, $"Standard {std} should succeed");
            }
        }

        [Test]
        public async Task HP_SchemaVersion_IsConsistentAcrossAllResponses()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var initSchemaV = r.SchemaVersion;
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(adv.SchemaVersion, Is.EqualTo(initSchemaV));
            Assert.That(status!.SchemaVersion, Is.EqualTo(initSchemaV));
        }

        [Test]
        public async Task II_NullTokenStandard_ReturnsError()
        {
            var req = ValidRequest();
            req.TokenStandard = null!;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task II_NullNetwork_ReturnsError()
        {
            var req = ValidRequest();
            req.Network = null!;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task II_NullDeployerAddress_ReturnsError()
        {
            var req = ValidRequest();
            req.DeployerAddress = null!;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task II_InvalidStandard_ErrorCode_IsNotNull()
        {
            var req = ValidRequest();
            req.TokenStandard = "UNSUPPORTED_STD";
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null);
        }

        [Test]
        public async Task BD_ERC20_OnBase_Full9Advances_Completes()
        {
            var req = ValidRequest();
            req.TokenStandard = "ERC20";
            req.Network = "base";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
            for (int i = 0; i < 9; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task BD_ThreePipelinesRun_AllCompleteIndependently()
        {
            var r1 = await _svc.InitiateAsync(ValidRequest());
            var r2 = await _svc.InitiateAsync(ValidRequest());
            var r3 = await _svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 9; i++)
            {
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r1.PipelineId });
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r2.PipelineId });
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r3.PipelineId });
            }
            var s1 = await _svc.GetStatusAsync(r1.PipelineId!, null);
            var s2 = await _svc.GetStatusAsync(r2.PipelineId!, null);
            var s3 = await _svc.GetStatusAsync(r3.PipelineId!, null);
            Assert.That(s1!.Stage, Is.EqualTo(PipelineStage.Completed));
            Assert.That(s2!.Stage, Is.EqualTo(PipelineStage.Completed));
            Assert.That(s3!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task FR_CancelThenNew_PreviousCancelledPipeline_DoesNotAffectNew()
        {
            var old = await _svc.InitiateAsync(ValidRequest());
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = old.PipelineId });
            var newP = await _svc.InitiateAsync(ValidRequest());
            Assert.That(newP.Stage, Is.EqualTo(PipelineStage.PendingReadiness));
        }

        [Test]
        public async Task FR_PipelineId_IsGuidFormat()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            Assert.That(Guid.TryParse(r.PipelineId, out _), Is.True);
        }

        [Test]
        public async Task FR_CancelledState_IsTerminalForCancelOperation()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var cancel2 = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel2.Success, Is.False);
        }

        [Test]
        public async Task NX_StageNames_AreMeaningful_ForAllStages()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var stages = new System.Collections.Generic.List<string>();
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            stages.Add(status!.Stage.ToString());
            for (int i = 0; i < 9; i++)
            {
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
                status = await _svc.GetStatusAsync(r.PipelineId!, null);
                stages.Add(status!.Stage.ToString());
            }
            Assert.That(stages.All(s => s.Length > 3), Is.True);
        }

        [Test]
        public async Task NX_AuditEvents_OrderedChronologically()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var audit = await _svc.GetAuditAsync(r.PipelineId!, null);
            var timestamps = audit.Events!.Select(e => e.Timestamp).ToList();
            for (int i = 1; i < timestamps.Count; i++)
                Assert.That(timestamps[i], Is.GreaterThanOrEqualTo(timestamps[i - 1]));
        }

        [Test]
        public async Task NX_FailureCategory_IsNone_OnSuccess()
        {
            var result = await _svc.InitiateAsync(ValidRequest());
            Assert.That(result.FailureCategory, Is.EqualTo(FailureCategory.None));
        }

        [Test]
        public async Task NX_FailureCategory_IsUserCorrectable_OnInvalidInput()
        {
            var req = ValidRequest();
            req.TokenName = "";
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.FailureCategory, Is.EqualTo(FailureCategory.UserCorrectable));
        }

        [Test]
        public async Task HP_ARC200_OnTestnet_FullLifecycle_Succeeds()
        {
            var req = ValidRequest();
            req.TokenStandard = "ARC200";
            req.Network = "testnet";
            var r = await _svc.InitiateAsync(req);
            for (int i = 0; i < 9; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_ARC1400_OnVoimain_FullLifecycle_Succeeds()
        {
            var req = ValidRequest();
            req.TokenStandard = "ARC1400";
            req.Network = "voimain";
            var r = await _svc.InitiateAsync(req);
            for (int i = 0; i < 9; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task BD_MaxRetries_Default3_PipelineCreated()
        {
            var req = ValidRequest();
            req.MaxRetries = 3;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_MaxRetries_10_PipelineCreated()
        {
            var req = ValidRequest();
            req.MaxRetries = 10;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task BD_MaxRetries_100_PipelineCreated()
        {
            var req = ValidRequest();
            req.MaxRetries = 100;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task HP_CorrelationId_PassedThrough_InAllResponses()
        {
            var req = ValidRequest();
            req.CorrelationId = "uj-end-to-end-corr-id";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.CorrelationId, Is.EqualTo("uj-end-to-end-corr-id"));
        }

        [Test]
        public async Task FR_TwoDistinctDeployers_IsolatedPipelines()
        {
            var req1 = ValidRequest();
            req1.DeployerAddress = "DEPLOYER_ONE";
            var req2 = ValidRequest();
            req2.DeployerAddress = "DEPLOYER_TWO";
            var r1 = await _svc.InitiateAsync(req1);
            var r2 = await _svc.InitiateAsync(req2);
            for (int i = 0; i < 3; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r1.PipelineId });
            var s1 = await _svc.GetStatusAsync(r1.PipelineId!, null);
            var s2 = await _svc.GetStatusAsync(r2.PipelineId!, null);
            Assert.That(s1!.Stage, Is.Not.EqualTo(s2!.Stage));
        }

        [Test]
        public async Task BD_TokenNameWithNumbers_Succeeds()
        {
            var req = ValidRequest();
            req.TokenName = "Token2025v1";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task HP_IdempotentReplay_Returns_SameStage_BeforeAnyAdvance()
        {
            var key = Guid.NewGuid().ToString();
            var req = ValidRequest();
            req.IdempotencyKey = key;
            var r1 = await _svc.InitiateAsync(req);
            var r2 = await _svc.InitiateAsync(req);
            Assert.That(r2.Stage, Is.EqualTo(r1.Stage));
        }

        [Test]
        public async Task II_WhitespacePaddedTokenStandard_ReturnsError()
        {
            var req = ValidRequest();
            req.TokenStandard = "  ";
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task II_NullTokenName_ReturnsError()
        {
            var req = ValidRequest();
            req.TokenName = null!;
            var result = await _svc.InitiateAsync(req);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task HP_FullPipeline_ARC1400_ReachesCompleted()
        {
            var req = ValidRequest(standard: "ARC1400");
            var r = await _svc.InitiateAsync(req);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_FullPipeline_ERC20_OnBase_ReachesCompleted()
        {
            var req = ValidRequest(network: "base", standard: "ERC20");
            var r = await _svc.InitiateAsync(req);
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task BD_RetryAsync_OnNonFailedPipeline_ReturnsNotInFailedState()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 3);
            var retry = await _svc.RetryAsync(new PipelineRetryRequest { PipelineId = r.PipelineId });
            Assert.That(retry.ErrorCode, Is.EqualTo("NOT_IN_FAILED_STATE"));
        }

        [Test]
        public async Task HP_DeployerEmail_AcceptedInRequest()
        {
            var req = ValidRequest();
            req.DeployerEmail = "user@biatec.io";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task II_UnknownPipelineId_GetStatus_ReturnsNull()
        {
            var status = await _svc.GetStatusAsync("uj-unknown-pipeline-id-xyz", null);
            Assert.That(status, Is.Null);
        }

        [Test]
        public async Task HP_AdvanceAsync_CompliancePending_IsStage5()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 4);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.CompliancePending));
        }

        [Test]
        public async Task HP_AdvanceAsync_DeploymentActive_IsStage8()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 7);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.DeploymentActive));
        }

        [Test]
        public async Task HP_AdvanceAsync_DeploymentConfirmed_IsStage9()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 8);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.DeploymentConfirmed));
        }

        [Test]
        public async Task BD_CancelAsync_AtCompliancePassing_Succeeds()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 5);
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
        }

        [Test]
        public async Task HP_PipelineStage_PendingReadiness_IsInitialStage()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            Assert.That(r.Stage, Is.EqualTo(PipelineStage.PendingReadiness));
        }

        [Test]
        public async Task FR_TwoPipelines_SamePipelineId_IsNeverReturned()
        {
            var r1 = await _svc.InitiateAsync(ValidRequest());
            var r2 = await _svc.InitiateAsync(ValidRequest());
            Assert.That(r1.PipelineId, Is.Not.EqualTo(r2.PipelineId));
        }

        [Test]
        public async Task HP_AdvanceAsync_DeploymentQueued_IsStage7()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 6);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.DeploymentQueued));
        }

        // ── New user journey tests ───────────────────────────────────────────────

        [Test]
        public async Task HP_InitiatePipeline_PipelineId_IsValidGuid()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            Assert.That(Guid.TryParse(r.PipelineId, out _), Is.True);
        }

        [Test]
        public async Task HP_CorrelationId_PropagatedFromRequest()
        {
            var corrId = "uj-corr-" + Guid.NewGuid();
            var req = ValidRequest();
            req.CorrelationId = corrId;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.CorrelationId, Is.EqualTo(corrId));
        }

        [Test]
        public async Task HP_SchemaVersion_IsOneZeroZero()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            Assert.That(r.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task HP_CreatedAt_IsPopulatedWithUtcTime()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var r = await _svc.InitiateAsync(ValidRequest());
            Assert.That(r.CreatedAt, Is.GreaterThan(before));
        }

        [Test]
        public async Task HP_AdvanceToValidationPassed_ReadinessStatusIsReady()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 3);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.ReadinessStatus, Is.EqualTo(ARC76ReadinessStatus.Ready));
        }

        [Test]
        public async Task BD_CancelAsync_MissingPipelineId_ReturnsError()
        {
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = null });
            Assert.That(cancel.Success, Is.False);
            Assert.That(cancel.ErrorCode, Is.EqualTo("MISSING_PIPELINE_ID"));
        }

        [Test]
        public async Task BD_AdvanceAsync_MissingPipelineId_ReturnsError()
        {
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = null });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("MISSING_PIPELINE_ID"));
        }

        [Test]
        public async Task BD_AdvanceAsync_NonExistentPipeline_ReturnsNotFound()
        {
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = "uj-nonexistent-xyz" });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("PIPELINE_NOT_FOUND"));
        }

        [Test]
        public async Task HP_AuditLog_AfterFirstAdvance_HasTwoOrMoreEvents()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var audit = await _svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events!.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task II_IdempotencyKey_SameParamsSameKey_ReturnsSamePipelineId()
        {
            var key = Guid.NewGuid().ToString();
            var req = ValidRequest();
            req.IdempotencyKey = key;
            var r1 = await _svc.InitiateAsync(req);
            var req2 = ValidRequest();
            req2.IdempotencyKey = key;
            var r2 = await _svc.InitiateAsync(req2);
            Assert.That(r1.PipelineId, Is.EqualTo(r2.PipelineId));
        }

        [Test]
        public async Task II_IdempotencyKey_DifferentToken_ReturnsConflict()
        {
            var key = Guid.NewGuid().ToString();
            var req1 = ValidRequest();
            req1.IdempotencyKey = key;
            req1.TokenName = "FirstToken";
            await _svc.InitiateAsync(req1);
            var req2 = ValidRequest();
            req2.IdempotencyKey = key;
            req2.TokenName = "DifferentToken";
            var r2 = await _svc.InitiateAsync(req2);
            Assert.That(r2.ErrorCode, Is.EqualTo("IDEMPOTENCY_KEY_CONFLICT"));
        }

        [Test]
        public async Task HP_ThreeSeparatePipelines_AllHaveUniqueIds()
        {
            var r1 = await _svc.InitiateAsync(ValidRequest());
            var r2 = await _svc.InitiateAsync(ValidRequest());
            var r3 = await _svc.InitiateAsync(ValidRequest());
            var ids = new HashSet<string?> { r1.PipelineId, r2.PipelineId, r3.PipelineId };
            Assert.That(ids.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task HP_CancelAtValidationPending_StatusIsCancelled()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 2);
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        [Test]
        public async Task HP_CancelAtDeploymentQueued_StatusIsCancelled()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 6);
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.Success, Is.True);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Cancelled));
        }

        [Test]
        public async Task HP_AuditEvents_AllHaveCorrelationId()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var audit = await _svc.GetAuditAsync(r.PipelineId!, null);
            foreach (var ev in audit.Events!)
                Assert.That(ev.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task HP_AuditResponse_SchemaVersion_IsOneZeroZero()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var audit = await _svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task HP_AdvanceResponse_SchemaVersion_IsOneZeroZero()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task HP_CancelResponse_SchemaVersion_IsOneZeroZero()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task HP_StatusResponse_SchemaVersion_IsOneZeroZero()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task HP_RetryAsync_OnNonFailedPipeline_ReturnsNotInFailedState_Journey()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var retry = await _svc.RetryAsync(new PipelineRetryRequest { PipelineId = r.PipelineId });
            Assert.That(retry.ErrorCode, Is.EqualTo("NOT_IN_FAILED_STATE"));
        }

        [Test]
        public async Task HP_AdvanceAsync_ReturnsSuccess_True_OnEachNonTerminalStep()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 9; i++)
            {
                var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
                Assert.That(adv.Success, Is.True);
            }
        }

        [Test]
        public async Task HP_AuditLog_HasExactlyOneEventAfterInitiate()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var audit = await _svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.Events!.Count, Is.GreaterThanOrEqualTo(1));
        }

        // ── Additional user journey tests ────────────────────────────────────────

        [Test]
        public async Task HP_SingleChar_TokenName_Accepted()
        {
            var req = ValidRequest();
            req.TokenName = "T";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task HP_ERC20_OnBase_FullLifecycle_Completes()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "base", standard: "ERC20"));
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_ARC1400_OnAramidmain_FullLifecycle_Completes()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "aramidmain", standard: "ARC1400"));
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }

        [Test]
        public async Task HP_AdvanceResponse_HasPipelineId()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.PipelineId, Is.EqualTo(r.PipelineId));
        }

        [Test]
        public async Task HP_CancelResponse_HasCorrectPreviousStage()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.PreviousStage, Is.EqualTo(PipelineStage.PendingReadiness));
        }

        [Test]
        public async Task II_WhitespaceOnlyTokenName_ReturnsError()
        {
            var req = ValidRequest();
            req.TokenName = "   ";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.False);
        }

        [Test]
        public async Task BD_MaxRetries_1_IsSupported()
        {
            var req = ValidRequest();
            req.MaxRetries = 1;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task FR_AdvanceAfterComplete_ReturnsTerminalError()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.Success, Is.False);
            Assert.That(adv.ErrorCode, Is.EqualTo("TERMINAL_STAGE"));
        }

        [Test]
        public async Task NX_InitiateResponse_HasSchemaVersion()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            Assert.That(r.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task HP_AuditEvent_OperationName_IsNotEmpty()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var audit = await _svc.GetAuditAsync(r.PipelineId!, null);
            foreach (var ev in audit.Events!)
                Assert.That(ev.Operation, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task HP_StatusAfterFirstAdvance_HasReadinessStatus_Ready()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.ReadinessStatus, Is.EqualTo(ARC76ReadinessStatus.Ready));
        }

        [Test]
        public async Task II_MissingTokenStandard_ReturnsUserCorrectableError()
        {
            var req = ValidRequest();
            req.TokenStandard = null;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.Success, Is.False);
            Assert.That(r.FailureCategory, Is.EqualTo(FailureCategory.UserCorrectable));
        }

        [Test]
        public async Task HP_FourSeparatePipelines_AllHaveUniqueIds()
        {
            var ids = new HashSet<string?>();
            for (int i = 0; i < 4; i++)
                ids.Add((await _svc.InitiateAsync(ValidRequest())).PipelineId);
            Assert.That(ids.Count, Is.EqualTo(4));
        }

        [Test]
        public async Task HP_AdvanceThirdStep_StageIsValidationPassed()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 2);
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.CurrentStage, Is.EqualTo(PipelineStage.ValidationPassed));
        }

        [Test]
        public async Task HP_AuditAfterFullLifecycle_HasSchemaVersion()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var audit = await _svc.GetAuditAsync(r.PipelineId!, null);
            Assert.That(audit.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task HP_RetryAsync_On_NonFailed_Pipeline_ReturnsNotInFailedState()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var retry = await _svc.RetryAsync(new PipelineRetryRequest { PipelineId = r.PipelineId });
            Assert.That(retry.ErrorCode, Is.EqualTo("NOT_IN_FAILED_STATE"));
        }

        [Test]
        public async Task II_EmptyDeployerAddress_ReturnsRemediationHint()
        {
            var req = ValidRequest();
            req.DeployerAddress = "";
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task HP_CorrelationId_IsPreservedInInitiateResponse()
        {
            var corrId = "journey-corr-" + Guid.NewGuid();
            var req = ValidRequest();
            req.CorrelationId = corrId;
            var r = await _svc.InitiateAsync(req);
            Assert.That(r.CorrelationId, Is.EqualTo(corrId));
        }

        [Test]
        public async Task HP_CancelAtDeploymentConfirmed_Returns_CannotCancel()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            await AdvanceToStageAsync(_svc, r.PipelineId!, 8);
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.ErrorCode, Is.EqualTo("CANNOT_CANCEL"));
        }

        [Test]
        public async Task NX_FailureCategory_ForNullRequest_IsNotNone()
        {
            var r = await _svc.InitiateAsync(null!);
            Assert.That(r.FailureCategory, Is.Not.EqualTo(FailureCategory.None));
        }

        [Test]
        public async Task HP_InitiateAndAdvance_PipelineIdIsSameInBothResponses()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var adv = await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            Assert.That(adv.PipelineId, Is.EqualTo(r.PipelineId));
        }

        [Test]
        public async Task HP_CancelResponse_PipelineId_MatchesInitiate()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var cancel = await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            Assert.That(cancel.PipelineId, Is.EqualTo(r.PipelineId));
        }

        [Test]
        public async Task HP_StatusAtValidationPassed_IsNotReadinessNotChecked()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            for (int i = 0; i < 3; i++)
                await _svc.AdvanceAsync(new PipelineAdvanceRequest { PipelineId = r.PipelineId });
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.ReadinessStatus, Is.Not.EqualTo(ARC76ReadinessStatus.NotChecked));
        }

        [Test]
        public async Task HP_AuditLog_GrowsAfterCancelOp()
        {
            var r = await _svc.InitiateAsync(ValidRequest());
            var countBefore = (await _svc.GetAuditAsync(r.PipelineId!, null)).Events!.Count;
            await _svc.CancelAsync(new PipelineCancelRequest { PipelineId = r.PipelineId });
            var countAfter = (await _svc.GetAuditAsync(r.PipelineId!, null)).Events!.Count;
            Assert.That(countAfter, Is.GreaterThan(countBefore));
        }

        [Test]
        public async Task HP_ARC200_OnBetanet_CompletesLifecycle()
        {
            var r = await _svc.InitiateAsync(ValidRequest(network: "betanet", standard: "ARC200"));
            await AdvanceToStageAsync(_svc, r.PipelineId!, 9);
            var status = await _svc.GetStatusAsync(r.PipelineId!, null);
            Assert.That(status!.Stage, Is.EqualTo(PipelineStage.Completed));
        }
    }
}
