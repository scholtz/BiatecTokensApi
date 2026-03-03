using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Service-layer unit tests for Issue #470: Vision Milestone – Deterministic Token
    /// Deployment API Lifecycle with Idempotency, Telemetry, and Reliability Guardrails.
    ///
    /// These are pure unit tests (no HTTP / WebApplicationFactory) that directly exercise
    /// TokenDeploymentLifecycleService business logic.
    ///
    /// AC1 - Primary flow completes end-to-end without manual intervention
    /// AC2 - Every critical state transition is represented explicitly in code and logs
    /// AC3 - Validation errors are deterministic with machine-readable codes
    /// AC4 - Retries are idempotent and do not create duplicate resources
    /// AC5 - Telemetry records lifecycle events with correlation IDs
    /// AC6 - Guardrails enforce invariants and provide remediation guidance
    /// AC7 - CI passes: DI resolves, endpoints respond, no unhandled exceptions
    /// AC8 - PR maps business goals to implementation and tests
    /// AC9 - Security and permission boundaries are preserved
    /// AC10 - Documentation updated; runbooks reference new endpoints
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TokenDeploymentLifecycleIssue470ServiceUnitTests
    {
        private TokenDeploymentLifecycleService _service = null!;
        private Mock<ILogger<TokenDeploymentLifecycleService>> _loggerMock = null!;

        // Valid Algorand test address (not a real funded account)
        private const string AlgorandAddress =
            "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        // Valid EVM test address
        private const string EvmAddress =
            "0x0000000000000000000000000000000000000001";

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<TokenDeploymentLifecycleService>>();
            _service = new TokenDeploymentLifecycleService(_loggerMock.Object);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC1: Primary flow completes end-to-end
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateDeployment_ValidAsaRequest_ReturnsCompleted()
        {
            var request = BuildValidAlgorandRequest("ASA");
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.Success));
        }

        [Test]
        public async Task InitiateDeployment_ValidArc3Request_ReturnsCompleted()
        {
            var request = BuildValidAlgorandRequest("ARC3");
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task InitiateDeployment_ValidArc200Request_ReturnsCompleted()
        {
            var request = BuildValidAlgorandRequest("ARC200");
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task InitiateDeployment_ValidErc20Request_ReturnsCompleted()
        {
            var request = BuildValidEvmRequest("ERC20");
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task InitiateDeployment_Completed_SetsAssetId()
        {
            var request = BuildValidAlgorandRequest("ASA");
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.AssetId, Is.Not.Null);
            Assert.That(result.AssetId, Is.GreaterThan(0UL));
        }

        [Test]
        public async Task InitiateDeployment_Completed_SetsTransactionId()
        {
            var request = BuildValidAlgorandRequest("ASA");
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.TransactionId, Is.Not.Null);
            Assert.That(result.TransactionId, Is.Not.Empty);
        }

        [Test]
        public async Task InitiateDeployment_Completed_SetsConfirmedRound()
        {
            var request = BuildValidAlgorandRequest("ASA");
            var result = await _service.InitiateDeploymentAsync(request);
            Assert.That(result.ConfirmedRound, Is.Not.Null);
            Assert.That(result.ConfirmedRound, Is.GreaterThan(0UL));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC2: State transitions are explicit
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateDeployment_Completed_EmitsTelemetryForEachStage()
        {
            var request = BuildValidAlgorandRequest("ASA");
            var result = await _service.InitiateDeploymentAsync(request);

            var stages = result.TelemetryEvents
                .Where(e => e.EventType == TelemetryEventType.StageTransition)
                .Select(e => e.Stage)
                .ToList();

            Assert.That(stages, Does.Contain(DeploymentStage.Initialising));
            Assert.That(stages, Does.Contain(DeploymentStage.Validating));
            Assert.That(stages, Does.Contain(DeploymentStage.Submitting));
            Assert.That(stages, Does.Contain(DeploymentStage.Confirming));
        }

        [Test]
        public async Task InitiateDeployment_Completed_EmitsCompletionSuccessEvent()
        {
            var request = BuildValidAlgorandRequest("ASA");
            var result = await _service.InitiateDeploymentAsync(request);

            var completion = result.TelemetryEvents
                .FirstOrDefault(e => e.EventType == TelemetryEventType.CompletionSuccess);

            Assert.That(completion, Is.Not.Null);
        }

        [Test]
        public async Task InitiateDeployment_InvalidInput_EmitsTerminalFailureEvent()
        {
            var request = BuildValidAlgorandRequest("ASA");
            request.TokenName = string.Empty; // Invalid
            var result = await _service.InitiateDeploymentAsync(request);

            var terminal = result.TelemetryEvents
                .FirstOrDefault(e => e.EventType == TelemetryEventType.TerminalFailure);

            Assert.That(terminal, Is.Not.Null);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC3: Validation errors are deterministic with machine-readable codes
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ValidateInputs_MissingTokenStandard_ReturnsTokenStandardRequired()
        {
            var request = BuildValidValidationRequest("ASA");
            request.TokenStandard = string.Empty;
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "TOKEN_STANDARD_REQUIRED"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_UnsupportedStandard_ReturnsTokenStandardUnsupported()
        {
            var request = BuildValidValidationRequest("UNKNOWN");
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "TOKEN_STANDARD_UNSUPPORTED"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_MissingTokenName_ReturnsTokenNameRequired()
        {
            var request = BuildValidValidationRequest("ASA");
            request.TokenName = string.Empty;
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "TOKEN_NAME_REQUIRED"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_TokenNameTooLong_ReturnsTokenNameTooLong()
        {
            var request = BuildValidValidationRequest("ASA");
            request.TokenName = new string('A', 65);
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "TOKEN_NAME_TOO_LONG"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_MissingTokenSymbol_ReturnsTokenSymbolRequired()
        {
            var request = BuildValidValidationRequest("ASA");
            request.TokenSymbol = string.Empty;
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "TOKEN_SYMBOL_REQUIRED"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_TokenSymbolTooLong_ReturnsTokenSymbolTooLong()
        {
            var request = BuildValidValidationRequest("ASA");
            request.TokenSymbol = "TOOLONGSYM";
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "TOKEN_SYMBOL_TOO_LONG"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_MissingNetwork_ReturnsNetworkRequired()
        {
            var request = BuildValidValidationRequest("ASA");
            request.Network = string.Empty;
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "NETWORK_REQUIRED"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_UnsupportedNetwork_ReturnsNetworkUnsupported()
        {
            var request = BuildValidValidationRequest("ASA");
            request.Network = "unknown-chain";
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "NETWORK_UNSUPPORTED"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_ZeroSupply_ReturnsSupplyZero()
        {
            var request = BuildValidValidationRequest("ASA");
            request.TotalSupply = 0;
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "SUPPLY_ZERO"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_NegativeDecimals_ReturnsDecimalsOutOfRange()
        {
            var request = BuildValidValidationRequest("ASA");
            request.Decimals = -1;
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "DECIMALS_OUT_OF_RANGE"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_DecimalsTooHigh_ReturnsDecimalsOutOfRange()
        {
            var request = BuildValidValidationRequest("ASA");
            request.Decimals = 20;
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "DECIMALS_OUT_OF_RANGE"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_InvalidAlgorandAddress_ReturnsCreatorAddressInvalid()
        {
            var request = BuildValidValidationRequest("ASA");
            request.CreatorAddress = "invalid-address";
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "CREATOR_ADDRESS_INVALID"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_AlgorandStandardOnEvmNetwork_ReturnsStandardNetworkMismatch()
        {
            var request = BuildValidValidationRequest("ASA");
            request.Network = "base-mainnet";
            request.CreatorAddress = EvmAddress; // provide valid EVM address so only mismatch fires
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.Results.Any(r => r.ErrorCode == "STANDARD_NETWORK_MISMATCH"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_EvmStandardOnAlgorandNetwork_ReturnsStandardNetworkMismatch()
        {
            var request = BuildValidValidationRequest("ERC20");
            request.Network = "algorand-mainnet";
            request.CreatorAddress = AlgorandAddress;
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.Results.Any(r => r.ErrorCode == "STANDARD_NETWORK_MISMATCH"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_ValidRequest_ReturnsIsValidTrue()
        {
            var request = BuildValidValidationRequest("ASA");
            var result = await _service.ValidateDeploymentInputsAsync(request);
            Assert.That(result.IsValid, Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC4: Idempotency – retries are safe and do not create duplicates
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateDeployment_SameRequest_SecondCallIsIdempotentReplay()
        {
            var request = BuildValidAlgorandRequest("ASA");
            request.IdempotencyKey = "idem-test-001";
            var first  = await _service.InitiateDeploymentAsync(request);
            var second = await _service.InitiateDeploymentAsync(request);

            Assert.That(second.IsIdempotentReplay, Is.True);
            Assert.That(second.IdempotencyStatus, Is.EqualTo(IdempotencyStatus.Duplicate));
        }

        [Test]
        public async Task InitiateDeployment_SameIdempotencyKey_ReturnsSameAssetId()
        {
            var request = BuildValidAlgorandRequest("ASA");
            request.IdempotencyKey = "idem-test-002";
            var first  = await _service.InitiateDeploymentAsync(request);
            var second = await _service.InitiateDeploymentAsync(request);

            Assert.That(second.AssetId, Is.EqualTo(first.AssetId));
        }

        [Test]
        public async Task InitiateDeployment_SameIdempotencyKey_ReturnsSameDeploymentId()
        {
            var request = BuildValidAlgorandRequest("ASA");
            request.IdempotencyKey = "idem-test-003";
            var first  = await _service.InitiateDeploymentAsync(request);
            var second = await _service.InitiateDeploymentAsync(request);

            Assert.That(second.DeploymentId, Is.EqualTo(first.DeploymentId));
        }

        [Test]
        public async Task InitiateDeployment_FirstRequest_IsNotIdempotentReplay()
        {
            var request = BuildValidAlgorandRequest("ASA");
            request.IdempotencyKey = "idem-test-004";
            var first = await _service.InitiateDeploymentAsync(request);

            Assert.That(first.IsIdempotentReplay, Is.False);
            Assert.That(first.IdempotencyStatus, Is.EqualTo(IdempotencyStatus.New));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC5: Telemetry with correlation IDs
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateDeployment_Completed_AllTelemetryEventsHaveCorrelationId()
        {
            var request = BuildValidAlgorandRequest("ASA");
            request.CorrelationId = "corr-unit-001";
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.TelemetryEvents, Is.Not.Empty);
            Assert.That(result.TelemetryEvents.All(e => e.CorrelationId == "corr-unit-001"), Is.True);
        }

        [Test]
        public async Task InitiateDeployment_Completed_AllTelemetryEventsHaveDeploymentId()
        {
            var request = BuildValidAlgorandRequest("ASA");
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.TelemetryEvents.All(e => e.DeploymentId == result.DeploymentId), Is.True);
        }

        [Test]
        public async Task GetTelemetryEvents_ExistingDeployment_ReturnsEvents()
        {
            var request = BuildValidAlgorandRequest("ASA");
            var deployed = await _service.InitiateDeploymentAsync(request);
            var events   = await _service.GetTelemetryEventsAsync(deployed.DeploymentId);

            Assert.That(events, Is.Not.Empty);
        }

        [Test]
        public async Task GetTelemetryEvents_UnknownDeployment_ReturnsEmptyList()
        {
            var events = await _service.GetTelemetryEventsAsync("unknown-deployment-id");
            Assert.That(events, Is.Empty);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC6: Reliability guardrails
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void EvaluateGuardrails_NodeUnreachable_ReturnsBlockingError()
        {
            var ctx = BuildValidGuardrailContext();
            ctx.NodeReachable = false;
            var findings = _service.EvaluateReliabilityGuardrails(ctx);

            var gr001 = findings.FirstOrDefault(f => f.GuardrailId == "GR-001");
            Assert.That(gr001, Is.Not.Null);
            Assert.That(gr001!.IsBlocking, Is.True);
            Assert.That(gr001.Severity, Is.EqualTo(GuardrailSeverity.Error));
        }

        [Test]
        public void EvaluateGuardrails_RetryLimitReached_ReturnsBlockingError()
        {
            var ctx = BuildValidGuardrailContext();
            ctx.RetryCount      = 3;
            ctx.MaxRetryAttempts = 3;
            var findings = _service.EvaluateReliabilityGuardrails(ctx);

            var gr002 = findings.FirstOrDefault(f => f.GuardrailId == "GR-002");
            Assert.That(gr002, Is.Not.Null);
            Assert.That(gr002!.IsBlocking, Is.True);
        }

        [Test]
        public void EvaluateGuardrails_TimedOut_ReturnsBlockingError()
        {
            var ctx = BuildValidGuardrailContext();
            ctx.IsTimedOut = true;
            var findings = _service.EvaluateReliabilityGuardrails(ctx);

            var gr003 = findings.FirstOrDefault(f => f.GuardrailId == "GR-003");
            Assert.That(gr003, Is.Not.Null);
            Assert.That(gr003!.IsBlocking, Is.True);
        }

        [Test]
        public void EvaluateGuardrails_InFlightDuplicate_ReturnsNonBlockingWarning()
        {
            var ctx = BuildValidGuardrailContext();
            ctx.HasInFlightDuplicate = true;
            var findings = _service.EvaluateReliabilityGuardrails(ctx);

            var gr004 = findings.FirstOrDefault(f => f.GuardrailId == "GR-004");
            Assert.That(gr004, Is.Not.Null);
            Assert.That(gr004!.IsBlocking, Is.False);
            Assert.That(gr004.Severity, Is.EqualTo(GuardrailSeverity.Warning));
        }

        [Test]
        public void EvaluateGuardrails_InvalidCreatorAddress_ReturnsBlockingError()
        {
            var ctx = BuildValidGuardrailContext();
            ctx.CreatorAddressValid = false;
            var findings = _service.EvaluateReliabilityGuardrails(ctx);

            var gr005 = findings.FirstOrDefault(f => f.GuardrailId == "GR-005");
            Assert.That(gr005, Is.Not.Null);
            Assert.That(gr005!.IsBlocking, Is.True);
        }

        [Test]
        public void EvaluateGuardrails_ConflictingDeployment_ReturnsNonBlockingWarning()
        {
            var ctx = BuildValidGuardrailContext();
            ctx.ConflictingDeploymentDetected = true;
            var findings = _service.EvaluateReliabilityGuardrails(ctx);

            var gr006 = findings.FirstOrDefault(f => f.GuardrailId == "GR-006");
            Assert.That(gr006, Is.Not.Null);
            Assert.That(gr006!.IsBlocking, Is.False);
            Assert.That(gr006.Severity, Is.EqualTo(GuardrailSeverity.Warning));
        }

        [Test]
        public void EvaluateGuardrails_Arc3RequiresIpfs_ReturnsInfoFinding()
        {
            var ctx = BuildValidGuardrailContext();
            ctx.RequiresIpfs = true;
            var findings = _service.EvaluateReliabilityGuardrails(ctx);

            var gr007 = findings.FirstOrDefault(f => f.GuardrailId == "GR-007");
            Assert.That(gr007, Is.Not.Null);
            Assert.That(gr007!.Severity, Is.EqualTo(GuardrailSeverity.Info));
            Assert.That(gr007.IsBlocking, Is.False);
        }

        [Test]
        public void EvaluateGuardrails_AllHealthy_ReturnsNoBlockingFindings()
        {
            var ctx = BuildValidGuardrailContext();
            var findings = _service.EvaluateReliabilityGuardrails(ctx);

            Assert.That(findings.Any(f => f.IsBlocking), Is.False);
        }

        [Test]
        public void EvaluateGuardrails_NullContext_ReturnsEmptyList()
        {
            var findings = _service.EvaluateReliabilityGuardrails(null!);
            Assert.That(findings, Is.Empty);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Additional: DeriveDeploymentId and BuildProgress
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void DeriveDeploymentId_SameInputs_ReturnsSameId()
        {
            var id1 = _service.DeriveDeploymentId("key-abc", "algorand-mainnet");
            var id2 = _service.DeriveDeploymentId("key-abc", "algorand-mainnet");
            Assert.That(id1, Is.EqualTo(id2));
        }

        [Test]
        public void DeriveDeploymentId_DifferentKey_ReturnsDifferentId()
        {
            var id1 = _service.DeriveDeploymentId("key-001", "algorand-mainnet");
            var id2 = _service.DeriveDeploymentId("key-002", "algorand-mainnet");
            Assert.That(id1, Is.Not.EqualTo(id2));
        }

        [Test]
        public void BuildProgress_Completed_Returns100Percent()
        {
            var progress = _service.BuildProgress(DeploymentStage.Completed, 0);
            Assert.That(progress.PercentComplete, Is.EqualTo(100));
        }

        [Test]
        public void BuildProgress_Failed_Returns0Percent()
        {
            var progress = _service.BuildProgress(DeploymentStage.Failed, 0);
            Assert.That(progress.PercentComplete, Is.EqualTo(0));
        }

        [Test]
        public async Task GetDeploymentStatus_ExistingDeployment_ReturnsResponse()
        {
            var request  = BuildValidAlgorandRequest("ASA");
            var deployed = await _service.InitiateDeploymentAsync(request);
            var status   = await _service.GetDeploymentStatusAsync(deployed.DeploymentId);

            Assert.That(status.DeploymentId, Is.EqualTo(deployed.DeploymentId));
            Assert.That(status.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task GetDeploymentStatus_UnknownId_ReturnsFailedResponse()
        {
            var status = await _service.GetDeploymentStatusAsync("nonexistent-deployment-id");
            Assert.That(status.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task RetryDeployment_AfterSuccess_ReturnsIdempotentReplay()
        {
            var request = BuildValidAlgorandRequest("ASA");
            request.IdempotencyKey = "retry-success-001";
            var deployed = await _service.InitiateDeploymentAsync(request);

            var retryResult = await _service.RetryDeploymentAsync(new DeploymentRetryRequest
            {
                IdempotencyKey = "retry-success-001"
            });

            Assert.That(retryResult.IsIdempotentReplay, Is.True);
            Assert.That(retryResult.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task RetryDeployment_UnknownKey_ReturnsNotFound()
        {
            var retryResult = await _service.RetryDeploymentAsync(new DeploymentRetryRequest
            {
                IdempotencyKey = "nonexistent-key-999"
            });

            Assert.That(retryResult.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(retryResult.Message, Does.Contain("found").IgnoreCase);
        }

        // ── Builders ────────────────────────────────────────────────────────────

        private static TokenDeploymentLifecycleRequest BuildValidAlgorandRequest(string standard) => new()
        {
            TokenStandard   = standard,
            TokenName       = "TestToken",
            TokenSymbol     = "TST",
            Network         = "algorand-mainnet",
            TotalSupply     = 1_000_000,
            Decimals        = 6,
            CreatorAddress  = AlgorandAddress,
            MaxRetryAttempts = 3,
            TimeoutSeconds  = 120,
        };

        private static TokenDeploymentLifecycleRequest BuildValidEvmRequest(string standard) => new()
        {
            TokenStandard   = standard,
            TokenName       = "TestToken",
            TokenSymbol     = "TST",
            Network         = "base-mainnet",
            TotalSupply     = 1_000_000,
            Decimals        = 18,
            CreatorAddress  = EvmAddress,
            MaxRetryAttempts = 3,
            TimeoutSeconds  = 120,
        };

        private static DeploymentValidationRequest BuildValidValidationRequest(string standard) => new()
        {
            TokenStandard   = standard,
            TokenName       = "TestToken",
            TokenSymbol     = "TST",
            Network         = "algorand-mainnet",
            TotalSupply     = 1_000_000,
            Decimals        = 6,
            CreatorAddress  = AlgorandAddress,
        };

        private static GuardrailEvaluationContext BuildValidGuardrailContext() => new()
        {
            TokenStandard              = "ASA",
            Network                    = "algorand-mainnet",
            RequiresIpfs               = false,
            NodeReachable              = true,
            RetryCount                 = 0,
            MaxRetryAttempts           = 3,
            IsTimedOut                 = false,
            HasInFlightDuplicate       = false,
            CreatorAddressValid        = true,
            ConflictingDeploymentDetected = false,
        };
    }
}
