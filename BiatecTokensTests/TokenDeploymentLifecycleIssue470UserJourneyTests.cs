using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// User journey tests for Issue #470: Vision Milestone – Deterministic Token
    /// Deployment API Lifecycle with Idempotency, Telemetry, and Reliability Guardrails.
    ///
    /// Journey categories:
    ///   HP  – Happy path: creator deploys a token successfully
    ///   II  – Invalid input: creator submits bad data
    ///   BD  – Boundary conditions: edge values at valid/invalid thresholds
    ///   FR  – Failure/recovery: node down, timeout, retry limit
    ///   NX  – Non-expert UX: clear messages with no internal jargon
    ///
    /// These tests directly exercise the service layer without HTTP overhead,
    /// validating user-visible outcomes and message quality.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TokenDeploymentLifecycleIssue470UserJourneyTests
    {
        private TokenDeploymentLifecycleService _service = null!;
        private Mock<ILogger<TokenDeploymentLifecycleService>> _loggerMock = null!;

        private const string AlgorandAddress =
            "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        private const string EvmAddress =
            "0x0000000000000000000000000000000000000001";

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<TokenDeploymentLifecycleService>>();
            _service = new TokenDeploymentLifecycleService(_loggerMock.Object);
        }

        // ════════════════════════════════════════════════════════════════════════
        // HP – Happy path journeys
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task HP1_Creator_DeploysAsaToken_SeesCompletedStage()
        {
            // A creator deploys a simple ASA token and sees a Completed stage.
            var request = BuildAlgorandRequest("ASA", "MyToken", "MTK");
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.Success));
            Assert.That(result.Message, Does.Contain("MyToken"));
        }

        [Test]
        public async Task HP2_Creator_DeploysArc3Token_GetsLiveAssetId()
        {
            // A creator deploys an ARC3 token with metadata and receives an on-chain asset ID.
            var request = BuildAlgorandRequest("ARC3", "MetaToken", "MTA");
            request.MetadataUri = "ipfs://bafybeig37ioir";
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.AssetId, Is.GreaterThan(0UL));
            Assert.That(result.TransactionId, Is.Not.Empty);
        }

        [Test]
        public async Task HP3_Creator_DeploysArc200Token_SuccessfulDeployment()
        {
            // ARC200 deployment completes successfully.
            var request = BuildAlgorandRequest("ARC200", "SmartToken", "SMT");
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
            Assert.That(result.ConfirmedRound, Is.GreaterThan(0UL));
        }

        [Test]
        public async Task HP4_Creator_DeploysErc20Token_SuccessfulDeployment()
        {
            // EVM ERC20 deployment on base-mainnet.
            var request = BuildEvmRequest("ERC20", "EvmToken", "EVT");
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
            Assert.That(result.AssetId, Is.GreaterThan(0UL));
        }

        [Test]
        public async Task HP5_Creator_SubmitsSameRequestTwice_NoDuplicateCreated()
        {
            // Idempotency: two identical requests produce one deployment, not two.
            var request = BuildAlgorandRequest("ASA", "IdemToken", "IDM");
            request.IdempotencyKey = "hp5-idem-key";

            var first  = await _service.InitiateDeploymentAsync(request);
            var second = await _service.InitiateDeploymentAsync(request);

            Assert.That(first.DeploymentId, Is.EqualTo(second.DeploymentId));
            Assert.That(second.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task HP6_Operator_QueriesDeploymentStatus_ReceivesStageAndOutcome()
        {
            // An operator queries deployment status after it completes.
            var request  = BuildAlgorandRequest("ASA", "OpToken", "OPT");
            var deployed = await _service.InitiateDeploymentAsync(request);
            var status   = await _service.GetDeploymentStatusAsync(deployed.DeploymentId);

            Assert.That(status.Stage, Is.EqualTo(DeploymentStage.Completed));
            Assert.That(status.AssetId, Is.EqualTo(deployed.AssetId));
        }

        [Test]
        public async Task HP7_Operator_RetrievesTelemetry_SeeAllStages()
        {
            // An operator pulls telemetry for a completed deployment.
            var request  = BuildAlgorandRequest("ASA", "TelToken", "TEL");
            var deployed = await _service.InitiateDeploymentAsync(request);
            var events   = await _service.GetTelemetryEventsAsync(deployed.DeploymentId);

            var stageTypes = events.Where(e => e.EventType == TelemetryEventType.StageTransition)
                                   .Select(e => e.Stage)
                                   .ToHashSet();

            Assert.That(stageTypes, Does.Contain(DeploymentStage.Validating));
            Assert.That(stageTypes, Does.Contain(DeploymentStage.Submitting));
            Assert.That(stageTypes, Does.Contain(DeploymentStage.Confirming));
        }

        // ════════════════════════════════════════════════════════════════════════
        // II – Invalid input journeys
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task II1_Creator_SubmitsEmptyTokenName_SeesValidationError()
        {
            var request = BuildAlgorandRequest("ASA", "", "TST");
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.ValidationResults.Any(r => r.ErrorCode == "TOKEN_NAME_REQUIRED"), Is.True);
        }

        [Test]
        public async Task II2_Creator_SubmitsUnsupportedStandard_SeesValidationError()
        {
            var request = BuildAlgorandRequest("UNKNOWN", "Token", "TST");
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.ValidationResults.Any(r => r.ErrorCode == "TOKEN_STANDARD_UNSUPPORTED"), Is.True);
        }

        [Test]
        public async Task II3_Creator_SubmitsZeroSupply_SeesValidationError()
        {
            var request = BuildAlgorandRequest("ASA", "Token", "TST");
            request.TotalSupply = 0;
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.ValidationResults.Any(r => r.ErrorCode == "SUPPLY_ZERO"), Is.True);
        }

        [Test]
        public async Task II4_Creator_SubmitsMismatchedStandardAndNetwork_SeesError()
        {
            // Creator accidentally tries to deploy ERC20 on Algorand mainnet.
            var request = BuildAlgorandRequest("ERC20", "Token", "TST");
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.ValidationResults.Any(r => r.ErrorCode == "STANDARD_NETWORK_MISMATCH"
                || r.ErrorCode == "CREATOR_ADDRESS_INVALID"), Is.True);
        }

        [Test]
        public async Task II5_Creator_SubmitsNullRequest_SeesTerminalFailure()
        {
            var result = await _service.InitiateDeploymentAsync(null!);

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
        }

        [Test]
        public async Task II6_Creator_SubmitsInvalidAddress_SeesCreatorAddressInvalid()
        {
            var request = BuildAlgorandRequest("ASA", "Token", "TST");
            request.CreatorAddress = "NOT-AN-ALGORAND-ADDRESS";
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.ValidationResults.Any(r => r.ErrorCode == "CREATOR_ADDRESS_INVALID"), Is.True);
        }

        [Test]
        public async Task II7_Creator_SubmitsSymbolTooLong_SeesValidationError()
        {
            var request = BuildAlgorandRequest("ASA", "Token", "TOOLONGSYM");
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.ValidationResults.Any(r => r.ErrorCode == "TOKEN_SYMBOL_TOO_LONG"), Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // BD – Boundary conditions
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task BD1_Creator_SubmitsMaxLengthTokenName_Passes()
        {
            var request = BuildAlgorandRequest("ASA", new string('A', 64), "TST");
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task BD2_Creator_SubmitsTokenNameExactlyTooLong_Fails()
        {
            var request = BuildAlgorandRequest("ASA", new string('A', 65), "TST");
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task BD3_Creator_SubmitsZeroDecimals_Passes()
        {
            var request = BuildAlgorandRequest("ASA", "Token", "TST");
            request.Decimals = 0;
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task BD4_Creator_SubmitsMaxDecimals_Passes()
        {
            var request = BuildAlgorandRequest("ASA", "Token", "TST");
            request.Decimals = 19;
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task BD5_Creator_SubmitsDecimalsOutOfRange_Fails()
        {
            var request = BuildAlgorandRequest("ASA", "Token", "TST");
            request.Decimals = 20;
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        // ════════════════════════════════════════════════════════════════════════
        // FR – Failure and recovery
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task FR1_Operator_RetriesSucceededDeployment_GetsIdempotentReplay()
        {
            var request = BuildAlgorandRequest("ASA", "RetryToken", "RTK");
            request.IdempotencyKey = "fr1-retry-success";
            await _service.InitiateDeploymentAsync(request);

            var retry = await _service.RetryDeploymentAsync(new DeploymentRetryRequest
            {
                IdempotencyKey = "fr1-retry-success"
            });

            Assert.That(retry.IsIdempotentReplay, Is.True);
            Assert.That(retry.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task FR2_Operator_RetriesWithUnknownKey_SeesNotFound()
        {
            var result = await _service.RetryDeploymentAsync(new DeploymentRetryRequest
            {
                IdempotencyKey = "unknown-key-fr2"
            });

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.Message, Does.Contain("found").IgnoreCase);
        }

        [Test]
        public async Task FR3_RetryWithNullRequest_SeesTerminalFailure()
        {
            var result = await _service.RetryDeploymentAsync(null!);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task FR4_GuardrailBlocksDeployment_RemediationHintIsProvided()
        {
            // Node unreachable → guardrail GR-001 provides a hint
            var ctx = new GuardrailEvaluationContext
            {
                TokenStandard       = "ASA",
                Network             = "algorand-mainnet",
                NodeReachable       = false,
                CreatorAddressValid = true,
                MaxRetryAttempts    = 3,
            };
            var findings = _service.EvaluateReliabilityGuardrails(ctx);
            var gr001    = findings.First(f => f.GuardrailId == "GR-001");

            Assert.That(gr001.RemediationHint, Is.Not.Empty);
        }

        // ════════════════════════════════════════════════════════════════════════
        // NX – Non-expert UX: messages must be clear, no jargon
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task NX1_Creator_SeesSuccessMessage_ContainsTokenName()
        {
            var request = BuildAlgorandRequest("ASA", "ClearToken", "CLR");
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.Message, Does.Contain("ClearToken"));
        }

        [Test]
        public async Task NX2_Creator_SeesValidationError_MessageIsHumanReadable()
        {
            var request = BuildAlgorandRequest("ASA", "", "TST");
            var result = await _service.InitiateDeploymentAsync(request);

            var nameError = result.ValidationResults.FirstOrDefault(r => r.ErrorCode == "TOKEN_NAME_REQUIRED");
            Assert.That(nameError, Is.Not.Null);
            Assert.That(nameError!.Message, Is.Not.Empty);
            // Should NOT contain stack trace or internal class names
            Assert.That(nameError.Message, Does.Not.Contain("Exception"));
            Assert.That(nameError.Message, Does.Not.Contain("NullRef"));
        }

        [Test]
        public async Task NX3_Creator_SeesTerminalFailure_RemediationHintIsPresent()
        {
            var request = BuildAlgorandRequest("ASA", "", "TST");
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.RemediationHint, Is.Not.Null);
            Assert.That(result.RemediationHint, Is.Not.Empty);
        }

        [Test]
        public async Task NX4_Creator_DeploymentProgress_SummaryIsHumanReadable()
        {
            var request = BuildAlgorandRequest("ASA", "Token", "TST");
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.Progress.Summary, Is.Not.Empty);
            Assert.That(result.Progress.Summary, Does.Not.Contain("null"));
        }

        [Test]
        public async Task NX5_Operator_ReceivesCorrelationId_InTelemetryEvents()
        {
            var request = BuildAlgorandRequest("ASA", "CorrelToken", "CRT");
            request.CorrelationId = "nxtest-correlation-id";
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.TelemetryEvents.All(e => e.CorrelationId == "nxtest-correlation-id"),
                Is.True, "All telemetry events must carry the correlation ID.");
        }

        [Test]
        public async Task NX6_Creator_AllGuardrailFindings_HaveNonEmptyRemediationHints()
        {
            // Every guardrail finding with severity Warning or Error should provide guidance.
            var ctx = new GuardrailEvaluationContext
            {
                TokenStandard               = "ASA",
                Network                     = "algorand-mainnet",
                NodeReachable               = false,
                IsTimedOut                  = true,
                HasInFlightDuplicate        = true,
                CreatorAddressValid         = false,
                ConflictingDeploymentDetected = true,
                RetryCount                  = 5,
                MaxRetryAttempts            = 3,
            };
            var findings = _service.EvaluateReliabilityGuardrails(ctx);
            var nonInfo  = findings.Where(f => f.Severity != GuardrailSeverity.Info);

            Assert.That(nonInfo.All(f => !string.IsNullOrEmpty(f.RemediationHint)), Is.True,
                "Every non-info guardrail finding must provide a remediation hint.");
        }

        // ── Builders ────────────────────────────────────────────────────────────

        private static TokenDeploymentLifecycleRequest BuildAlgorandRequest(
            string standard, string name, string symbol) => new()
        {
            TokenStandard    = standard,
            TokenName        = name,
            TokenSymbol      = symbol,
            Network          = "algorand-mainnet",
            TotalSupply      = 1_000_000,
            Decimals         = 6,
            CreatorAddress   = AlgorandAddress,
            MaxRetryAttempts = 3,
            TimeoutSeconds   = 120,
        };

        private static TokenDeploymentLifecycleRequest BuildEvmRequest(
            string standard, string name, string symbol) => new()
        {
            TokenStandard    = standard,
            TokenName        = name,
            TokenSymbol      = symbol,
            Network          = "base-mainnet",
            TotalSupply      = 1_000_000,
            Decimals         = 18,
            CreatorAddress   = EvmAddress,
            MaxRetryAttempts = 3,
            TimeoutSeconds   = 120,
        };
    }
}
