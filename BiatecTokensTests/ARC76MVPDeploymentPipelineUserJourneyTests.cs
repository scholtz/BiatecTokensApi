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
    }
}
