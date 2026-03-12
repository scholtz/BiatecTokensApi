using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for <see cref="TokenDeploymentLifecycleService"/>.
    ///
    /// These tests validate:
    /// - Deployment initiation with valid and invalid inputs
    /// - Idempotency: repeated requests return cached results
    /// - State machine: stage transitions follow the expected pipeline
    /// - Validation: required fields, supported standards, network constraints
    /// - Reliability guardrails: node reachability, retry limits, timeout, in-flight duplicates
    /// - Status queries: found, not-found, empty ID
    /// - Retry semantics: transient retry, limit exceeded, already-succeeded replay
    /// - Telemetry: events emitted throughout the lifecycle
    /// - Error taxonomy: deterministic, machine-readable error codes
    /// </summary>
    [TestFixture]
    public class TokenDeploymentLifecycleServiceTests
    {
        private Mock<ILogger<TokenDeploymentLifecycleService>> _loggerMock = null!;
        private TokenDeploymentLifecycleService _service = null!;

        // Valid test addresses
        private const string ValidAlgorandAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string ValidEvmAddress      = "0x1234567890123456789012345678901234567890";

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<TokenDeploymentLifecycleService>>();
            _service    = new TokenDeploymentLifecycleService(_loggerMock.Object);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static TokenDeploymentLifecycleRequest BuildValidAlgorandRequest(
            string? idempotencyKey = null,
            string tokenName       = "Test Token",
            string tokenSymbol     = "TST",
            string standard        = "ASA",
            string network         = "algorand-testnet",
            ulong  totalSupply     = 1_000_000,
            int    decimals        = 6)
        {
            return new TokenDeploymentLifecycleRequest
            {
                IdempotencyKey  = idempotencyKey,
                CorrelationId   = Guid.NewGuid().ToString(),
                TokenStandard   = standard,
                TokenName       = tokenName,
                TokenSymbol     = tokenSymbol,
                Network         = network,
                TotalSupply     = totalSupply,
                Decimals        = decimals,
                CreatorAddress  = ValidAlgorandAddress,
                MaxRetryAttempts = 3,
                TimeoutSeconds   = 120,
            };
        }

        private static TokenDeploymentLifecycleRequest BuildValidEvmRequest(
            string? idempotencyKey = null)
        {
            return new TokenDeploymentLifecycleRequest
            {
                IdempotencyKey  = idempotencyKey,
                CorrelationId   = Guid.NewGuid().ToString(),
                TokenStandard   = "ERC20",
                TokenName       = "EVM Test Token",
                TokenSymbol     = "ETT",
                Network         = "base-mainnet",
                TotalSupply     = 1_000_000,
                Decimals        = 18,
                CreatorAddress  = ValidEvmAddress,
                MaxRetryAttempts = 3,
                TimeoutSeconds   = 120,
            };
        }

        // ── InitiateDeploymentAsync: happy path ────────────────────────────────

        [Test]
        public async Task Initiate_ValidAlgorandASA_ReturnsSuccess()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_ReturnsSuccessOutcome()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.Success));
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_ReturnsDurableDeploymentId()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.DeploymentId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_ReturnsIdempotencyKey()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.IdempotencyKey, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_ReturnsCorrelationId()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_ReturnsNonZeroAssetId()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.AssetId, Is.Not.Null);
            Assert.That(result.AssetId!.Value, Is.GreaterThan(0UL));
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_ReturnsTransactionId()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.TransactionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_ReturnsConfirmedRound()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.ConfirmedRound, Is.Not.Null);
            Assert.That(result.ConfirmedRound!.Value, Is.GreaterThan(0UL));
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_HasTelemetryEvents()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.TelemetryEvents, Is.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_HasValidationResults()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.ValidationResults, Is.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_HasInitiatedAtTimestamp()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.InitiatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_HasLastUpdatedAtTimestamp()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.LastUpdatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_HasProgress()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Progress, Is.Not.Null);
            Assert.That(result.Progress.PercentComplete, Is.EqualTo(100));
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_SchemaVersionPresent()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_IdempotencyStatusIsNew()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.IdempotencyStatus, Is.EqualTo(IdempotencyStatus.New));
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_IsNotIdempotentReplay()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.IsIdempotentReplay, Is.False);
        }

        [Test]
        public async Task Initiate_ValidAlgorandASA_ProgressHasStages()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Progress.Stages, Is.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidEvmERC20_ReturnsSuccess()
        {
            var req = BuildValidEvmRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.Success));
        }

        [Test]
        public async Task Initiate_AllSupportedAlgorandStandards_SucceedWithValidAddress()
        {
            foreach (var standard in new[] { "ASA", "ARC3", "ARC200", "ARC1400" })
            {
                var req = BuildValidAlgorandRequest(standard: standard);
                var result = await _service.InitiateDeploymentAsync(req);
                Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed),
                    $"Standard '{standard}' should complete successfully.");
            }
        }

        // ── InitiateDeploymentAsync: null / missing request ───────────────────

        [Test]
        public async Task Initiate_NullRequest_ReturnsFailed()
        {
            var result = await _service.InitiateDeploymentAsync(null!);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_NullRequest_ReturnsErrorResponse()
        {
            var result = await _service.InitiateDeploymentAsync(null!);
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
        }

        // ── InitiateDeploymentAsync: validation failures ──────────────────────

        [Test]
        public async Task Initiate_MissingTokenStandard_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest();
            req.TokenStandard = string.Empty;
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_UnsupportedTokenStandard_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest();
            req.TokenStandard = "UNKNOWN_STANDARD";
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_UnsupportedTokenStandard_HasValidationError()
        {
            var req = BuildValidAlgorandRequest();
            req.TokenStandard = "UNKNOWN_STANDARD";
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.ValidationResults.Any(v => !v.IsValid && v.FieldName == "TokenStandard"),
                Is.True, "Should have TokenStandard validation error.");
        }

        [Test]
        public async Task Initiate_MissingTokenName_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest();
            req.TokenName = string.Empty;
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_TokenNameTooLong_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest(tokenName: new string('X', 65));
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_TokenNameTooLong_HasValidationErrorCode()
        {
            var req = BuildValidAlgorandRequest(tokenName: new string('X', 65));
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.ValidationResults.Any(v =>
                v.FieldName == "TokenName" && v.ErrorCode == "TOKEN_NAME_TOO_LONG"),
                Is.True);
        }

        [Test]
        public async Task Initiate_MissingTokenSymbol_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest();
            req.TokenSymbol = string.Empty;
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_TokenSymbolTooLong_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest(tokenSymbol: "TOOLONGSY");
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_MissingNetwork_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest();
            req.Network = string.Empty;
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_UnsupportedNetwork_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest(network: "unknown-chain-xyz");
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_ZeroTotalSupply_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest(totalSupply: 0);
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_ZeroTotalSupply_HasSupplyZeroErrorCode()
        {
            var req = BuildValidAlgorandRequest(totalSupply: 0);
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.ValidationResults.Any(v => v.ErrorCode == "SUPPLY_ZERO"),
                Is.True, "Should have SUPPLY_ZERO error code.");
        }

        [Test]
        public async Task Initiate_NegativeDecimals_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest(decimals: -1);
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_DecimalsTooHigh_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest(decimals: 20);
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_DecimalsTooHigh_HasDecimalsOutOfRangeErrorCode()
        {
            var req = BuildValidAlgorandRequest(decimals: 20);
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.ValidationResults.Any(v => v.ErrorCode == "DECIMALS_OUT_OF_RANGE"),
                Is.True, "Should have DECIMALS_OUT_OF_RANGE error code.");
        }

        [Test]
        public async Task Initiate_MissingCreatorAddress_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest();
            req.CreatorAddress = string.Empty;
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_InvalidCreatorAddressForNetwork_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest();
            req.CreatorAddress = "not-a-valid-address";
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_AlgorandStandardOnEvmNetwork_ReturnsFailed()
        {
            var req = BuildValidEvmRequest();
            req.TokenStandard = "ASA"; // Algorand standard on EVM network
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_AlgorandStandardOnEvmNetwork_HasStandardNetworkMismatchError()
        {
            var req = BuildValidEvmRequest();
            req.TokenStandard = "ASA";
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.ValidationResults.Any(v => v.ErrorCode == "STANDARD_NETWORK_MISMATCH"),
                Is.True, "Should have STANDARD_NETWORK_MISMATCH error.");
        }

        [Test]
        public async Task Initiate_EvmStandardOnAlgorandNetwork_ReturnsFailed()
        {
            var req = BuildValidAlgorandRequest();
            req.TokenStandard = "ERC20"; // EVM standard on Algorand network
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_ValidationFailed_RemediationHintPresent()
        {
            var req = BuildValidAlgorandRequest(totalSupply: 0);
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidationFailed_OutcomeIsTerminalFailure()
        {
            var req = BuildValidAlgorandRequest(totalSupply: 0);
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
        }

        // ── InitiateDeploymentAsync: idempotency ──────────────────────────────

        [Test]
        public async Task Initiate_SameIdempotencyKey_ReturnsCachedResult()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildValidAlgorandRequest(idempotencyKey: key);
            var first  = await _service.InitiateDeploymentAsync(req);
            var second = await _service.InitiateDeploymentAsync(req);
            Assert.That(second.DeploymentId, Is.EqualTo(first.DeploymentId));
        }

        [Test]
        public async Task Initiate_SameIdempotencyKey_SecondCallIsIdempotentReplay()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildValidAlgorandRequest(idempotencyKey: key);
            await _service.InitiateDeploymentAsync(req);
            var second = await _service.InitiateDeploymentAsync(req);
            Assert.That(second.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task Initiate_SameIdempotencyKey_SecondCallHasDuplicateStatus()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildValidAlgorandRequest(idempotencyKey: key);
            await _service.InitiateDeploymentAsync(req);
            var second = await _service.InitiateDeploymentAsync(req);
            Assert.That(second.IdempotencyStatus, Is.EqualTo(IdempotencyStatus.Duplicate));
        }

        [Test]
        public async Task Initiate_SameIdempotencyKey_ThreeTimesDeterministic()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildValidAlgorandRequest(idempotencyKey: key);
            var first  = await _service.InitiateDeploymentAsync(req);
            var second = await _service.InitiateDeploymentAsync(req);
            var third  = await _service.InitiateDeploymentAsync(req);
            Assert.That(second.AssetId,     Is.EqualTo(first.AssetId));
            Assert.That(third.AssetId,      Is.EqualTo(first.AssetId));
            Assert.That(second.TransactionId, Is.EqualTo(first.TransactionId));
            Assert.That(third.TransactionId,  Is.EqualTo(first.TransactionId));
        }

        [Test]
        public async Task Initiate_DifferentIdempotencyKeys_CreateDifferentDeploymentIds()
        {
            var req1 = BuildValidAlgorandRequest(idempotencyKey: Guid.NewGuid().ToString());
            var req2 = BuildValidAlgorandRequest(idempotencyKey: Guid.NewGuid().ToString());
            var r1 = await _service.InitiateDeploymentAsync(req1);
            var r2 = await _service.InitiateDeploymentAsync(req2);
            Assert.That(r2.DeploymentId, Is.Not.EqualTo(r1.DeploymentId));
        }

        [Test]
        public async Task Initiate_FirstCall_IsNotIdempotentReplay()
        {
            var req = BuildValidAlgorandRequest(idempotencyKey: Guid.NewGuid().ToString());
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.IsIdempotentReplay, Is.False);
        }

        [Test]
        public async Task Initiate_DeterministicDeploymentId_SameKeyProducesSameId()
        {
            var key = "fixed-idempotency-key-abc";
            var req1 = BuildValidAlgorandRequest(idempotencyKey: key);
            var req2 = BuildValidAlgorandRequest(idempotencyKey: key);
            // Use a fresh service for each to test derivation, not caching
            var svc1 = new TokenDeploymentLifecycleService(
                new Mock<ILogger<TokenDeploymentLifecycleService>>().Object);
            var svc2 = new TokenDeploymentLifecycleService(
                new Mock<ILogger<TokenDeploymentLifecycleService>>().Object);
            var r1 = await svc1.InitiateDeploymentAsync(req1);
            var r2 = await svc2.InitiateDeploymentAsync(req2);
            Assert.That(r2.DeploymentId, Is.EqualTo(r1.DeploymentId));
        }

        // ── InitiateDeploymentAsync: telemetry evidence ────────────────────────

        [Test]
        public async Task Initiate_SuccessfulDeployment_TelemetryContainsStageTransitions()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            var stageTransitions = result.TelemetryEvents
                .Where(e => e.EventType == TelemetryEventType.StageTransition)
                .ToList();
            Assert.That(stageTransitions, Is.Not.Empty);
        }

        [Test]
        public async Task Initiate_SuccessfulDeployment_TelemetryContainsCompletionEvent()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            var completionEvent = result.TelemetryEvents
                .FirstOrDefault(e => e.EventType == TelemetryEventType.CompletionSuccess);
            Assert.That(completionEvent, Is.Not.Null);
        }

        [Test]
        public async Task Initiate_SuccessfulDeployment_TelemetryHasCorrelationId()
        {
            var correlationId = Guid.NewGuid().ToString();
            var req = BuildValidAlgorandRequest();
            req.CorrelationId = correlationId;
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.TelemetryEvents.All(e => !string.IsNullOrEmpty(e.CorrelationId)),
                Is.True, "All telemetry events should have a correlation ID.");
        }

        [Test]
        public async Task Initiate_SuccessfulDeployment_TelemetryEventsHaveDeploymentId()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.TelemetryEvents.All(e => e.DeploymentId == result.DeploymentId),
                Is.True, "All telemetry events should have the correct deployment ID.");
        }

        [Test]
        public async Task Initiate_SuccessfulDeployment_TelemetryEventsHaveTimestamps()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.TelemetryEvents.All(e => e.OccurredAt != default),
                Is.True, "All telemetry events should have a timestamp.");
        }

        [Test]
        public async Task Initiate_ValidationFailure_TelemetryContainsValidationErrorEvent()
        {
            var req = BuildValidAlgorandRequest(totalSupply: 0);
            var result = await _service.InitiateDeploymentAsync(req);
            var validationErrorEvent = result.TelemetryEvents
                .FirstOrDefault(e => e.EventType == TelemetryEventType.ValidationError);
            Assert.That(validationErrorEvent, Is.Not.Null);
        }

        [Test]
        public async Task Initiate_ValidationFailure_TelemetryContainsTerminalFailureEvent()
        {
            var req = BuildValidAlgorandRequest(totalSupply: 0);
            var result = await _service.InitiateDeploymentAsync(req);
            var terminalEvent = result.TelemetryEvents
                .FirstOrDefault(e => e.EventType == TelemetryEventType.TerminalFailure);
            Assert.That(terminalEvent, Is.Not.Null);
        }

        // ── GetDeploymentStatusAsync ───────────────────────────────────────────

        [Test]
        public async Task GetStatus_AfterInitiation_ReturnsDeployment()
        {
            var req = BuildValidAlgorandRequest();
            var initiated = await _service.InitiateDeploymentAsync(req);
            var status = await _service.GetDeploymentStatusAsync(initiated.DeploymentId);
            Assert.That(status.DeploymentId, Is.EqualTo(initiated.DeploymentId));
        }

        [Test]
        public async Task GetStatus_AfterInitiation_ReturnsSameStage()
        {
            var req = BuildValidAlgorandRequest();
            var initiated = await _service.InitiateDeploymentAsync(req);
            var status = await _service.GetDeploymentStatusAsync(initiated.DeploymentId);
            Assert.That(status.Stage, Is.EqualTo(initiated.Stage));
        }

        [Test]
        public async Task GetStatus_AfterInitiation_ReturnsSameAssetId()
        {
            var req = BuildValidAlgorandRequest();
            var initiated = await _service.InitiateDeploymentAsync(req);
            var status = await _service.GetDeploymentStatusAsync(initiated.DeploymentId);
            Assert.That(status.AssetId, Is.EqualTo(initiated.AssetId));
        }

        [Test]
        public async Task GetStatus_AfterInitiation_IsNotIdempotentReplay()
        {
            var req = BuildValidAlgorandRequest();
            var initiated = await _service.InitiateDeploymentAsync(req);
            var status = await _service.GetDeploymentStatusAsync(initiated.DeploymentId);
            Assert.That(status.IsIdempotentReplay, Is.False);
        }

        [Test]
        public async Task GetStatus_NonExistentDeployment_ReturnsFailed()
        {
            var status = await _service.GetDeploymentStatusAsync("nonexistent-deployment-id-xyz");
            Assert.That(status.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task GetStatus_NonExistentDeployment_MessageContainsNotFound()
        {
            var status = await _service.GetDeploymentStatusAsync("nonexistent-deployment-id-xyz");
            Assert.That(status.Message, Does.Contain("not found").IgnoreCase
                .Or.Contain("not found").IgnoreCase);
        }

        [Test]
        public async Task GetStatus_EmptyDeploymentId_ReturnsFailed()
        {
            var status = await _service.GetDeploymentStatusAsync(string.Empty);
            Assert.That(status.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task GetStatus_WithCorrelationId_ReturnsCorrelationId()
        {
            var req = BuildValidAlgorandRequest();
            var initiated = await _service.InitiateDeploymentAsync(req);
            var correlationId = Guid.NewGuid().ToString();
            var status = await _service.GetDeploymentStatusAsync(initiated.DeploymentId, correlationId);
            Assert.That(status.CorrelationId, Is.EqualTo(correlationId));
        }

        [Test]
        public async Task GetStatus_StableAcrossMultiplePolls_StateDoesNotRegress()
        {
            var req = BuildValidAlgorandRequest();
            var initiated = await _service.InitiateDeploymentAsync(req);
            for (int i = 0; i < 3; i++)
            {
                var status = await _service.GetDeploymentStatusAsync(initiated.DeploymentId);
                Assert.That(status.Stage, Is.EqualTo(DeploymentStage.Completed),
                    $"Poll #{i + 1}: state should not regress.");
            }
        }

        // ── RetryDeploymentAsync ───────────────────────────────────────────────

        [Test]
        public async Task Retry_NullRequest_ReturnsFailed()
        {
            var result = await _service.RetryDeploymentAsync(null!);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Retry_MissingIdempotencyKey_ReturnsFailed()
        {
            var result = await _service.RetryDeploymentAsync(new DeploymentRetryRequest
            {
                IdempotencyKey = string.Empty
            });
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Retry_UnknownIdempotencyKey_ReturnsFailed()
        {
            var result = await _service.RetryDeploymentAsync(new DeploymentRetryRequest
            {
                IdempotencyKey = "nonexistent-key-xyz"
            });
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Retry_AlreadySucceeded_ReturnsIdempotentReplay()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildValidAlgorandRequest(idempotencyKey: key);
            var initiated = await _service.InitiateDeploymentAsync(req);
            Assert.That(initiated.Stage, Is.EqualTo(DeploymentStage.Completed));

            var retryResult = await _service.RetryDeploymentAsync(new DeploymentRetryRequest
            {
                IdempotencyKey = key
            });
            Assert.That(retryResult.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task Retry_AlreadySucceeded_PreservesAssetId()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildValidAlgorandRequest(idempotencyKey: key);
            var initiated = await _service.InitiateDeploymentAsync(req);

            var retryResult = await _service.RetryDeploymentAsync(new DeploymentRetryRequest
            {
                IdempotencyKey = key
            });
            Assert.That(retryResult.AssetId, Is.EqualTo(initiated.AssetId));
        }

        [Test]
        public async Task Retry_ExceedsMaxAttempts_ReturnsTerminalFailure()
        {
            // Use a deployment that failed (invalid input), so retries are not short-circuited
            // by the "already succeeded" idempotent-replay branch.
            var key = Guid.NewGuid().ToString();
            var failedReq = BuildValidAlgorandRequest(idempotencyKey: key, totalSupply: 0); // will fail validation
            var initiated = await _service.InitiateDeploymentAsync(failedReq);
            Assert.That(initiated.Stage, Is.EqualTo(DeploymentStage.Failed), "Precondition: deployment must fail.");

            // Retry 4 times – the 4th retry increments RetryCount to 4 which exceeds the default limit of 3.
            DeploymentRetryRequest retryReq = new() { IdempotencyKey = key, ForceRetry = false };
            TokenDeploymentLifecycleResponse? result = null;
            for (int i = 0; i < 4; i++)
            {
                result = await _service.RetryDeploymentAsync(retryReq);
            }
            Assert.That(result!.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
        }

        [Test]
        public async Task Retry_ExceedsMaxAttempts_RemediationHintPresent()
        {
            // Use a deployment that failed (invalid input), so retries are not short-circuited.
            var key = Guid.NewGuid().ToString();
            var failedReq = BuildValidAlgorandRequest(idempotencyKey: key, totalSupply: 0);
            await _service.InitiateDeploymentAsync(failedReq);

            // Retry 4 times to exceed the default limit of 3.
            DeploymentRetryRequest retryReq = new() { IdempotencyKey = key, ForceRetry = false };
            TokenDeploymentLifecycleResponse? result = null;
            for (int i = 0; i < 4; i++)
            {
                result = await _service.RetryDeploymentAsync(retryReq);
            }
            Assert.That(result!.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        // ── GetTelemetryEventsAsync ────────────────────────────────────────────

        [Test]
        public async Task GetTelemetry_AfterInitiation_ReturnsEvents()
        {
            var req = BuildValidAlgorandRequest();
            var initiated = await _service.InitiateDeploymentAsync(req);
            var events = await _service.GetTelemetryEventsAsync(initiated.DeploymentId);
            Assert.That(events, Is.Not.Empty);
        }

        [Test]
        public async Task GetTelemetry_AfterInitiation_EventsHaveCorrectDeploymentId()
        {
            var req = BuildValidAlgorandRequest();
            var initiated = await _service.InitiateDeploymentAsync(req);
            var events = await _service.GetTelemetryEventsAsync(initiated.DeploymentId);
            Assert.That(events.All(e => e.DeploymentId == initiated.DeploymentId), Is.True);
        }

        [Test]
        public async Task GetTelemetry_NonExistentDeployment_ReturnsEmptyList()
        {
            var events = await _service.GetTelemetryEventsAsync("nonexistent-xyz");
            Assert.That(events, Is.Empty);
        }

        [Test]
        public async Task GetTelemetry_SuccessfulDeployment_ContainsCompletionSuccessEvent()
        {
            var req = BuildValidAlgorandRequest();
            var initiated = await _service.InitiateDeploymentAsync(req);
            var events = await _service.GetTelemetryEventsAsync(initiated.DeploymentId);
            Assert.That(events.Any(e => e.EventType == TelemetryEventType.CompletionSuccess), Is.True);
        }

        [Test]
        public async Task GetTelemetry_SuccessfulDeployment_ContainsStageTransitionEvents()
        {
            var req = BuildValidAlgorandRequest();
            var initiated = await _service.InitiateDeploymentAsync(req);
            var events = await _service.GetTelemetryEventsAsync(initiated.DeploymentId);
            Assert.That(events.Any(e => e.EventType == TelemetryEventType.StageTransition), Is.True);
        }

        // ── ValidateDeploymentInputsAsync ─────────────────────────────────────

        [Test]
        public async Task Validate_ValidAlgorandRequest_IsValid()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Validation Test Token",
                TokenSymbol    = "VTT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public async Task Validate_ValidAlgorandRequest_AllResultsPass()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Validation Test Token",
                TokenSymbol    = "VTT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.Results.All(r => r.IsValid), Is.True);
        }

        [Test]
        public async Task Validate_ValidAlgorandRequest_SummaryIndicatesPass()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.Summary, Does.Contain("All validation checks passed")
                .Or.Contain("passed").IgnoreCase);
        }

        [Test]
        public async Task Validate_ValidAlgorandRequest_HasSchemaVersion()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Validate_NullRequest_IsInvalid()
        {
            var result = await _service.ValidateDeploymentInputsAsync(null!);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public async Task Validate_MissingTokenStandard_IsInvalid()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = string.Empty,
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public async Task Validate_MissingTokenStandard_HasTokenStandardRequiredError()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = string.Empty,
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.Results.Any(r => r.ErrorCode == "TOKEN_STANDARD_REQUIRED"), Is.True);
        }

        [Test]
        public async Task Validate_ZeroSupply_IsInvalid()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 0,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public async Task Validate_ZeroSupply_HasSupplyZeroError()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 0,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.Results.Any(r => r.ErrorCode == "SUPPLY_ZERO"), Is.True);
        }

        [Test]
        public async Task Validate_UnsupportedStandard_HasUnsupportedErrorCode()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "UNKNOWN_STANDARD_XYZ",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.Results.Any(r => r.ErrorCode == "TOKEN_STANDARD_UNSUPPORTED"), Is.True);
        }

        [Test]
        public async Task Validate_InvalidNetwork_IsInvalid()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "invalid-network-xyz",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public async Task Validate_InvalidNetwork_HasNetworkUnsupportedError()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "invalid-network-xyz",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.Results.Any(r => r.ErrorCode == "NETWORK_UNSUPPORTED"), Is.True);
        }

        [Test]
        public async Task Validate_MissingCreatorAddress_HasCreatorAddressRequiredError()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = string.Empty,
            });
            Assert.That(result.Results.Any(r => r.ErrorCode == "CREATOR_ADDRESS_REQUIRED"), Is.True);
        }

        [Test]
        public async Task Validate_InvalidCreatorAddress_HasInvalidAddressError()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = "not-a-valid-address",
            });
            Assert.That(result.Results.Any(r => r.ErrorCode == "CREATOR_ADDRESS_INVALID"), Is.True);
        }

        [Test]
        public async Task Validate_CorrelationIdEchoed()
        {
            var correlationId = Guid.NewGuid().ToString();
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                CorrelationId  = correlationId,
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.CorrelationId, Is.EqualTo(correlationId));
        }

        [Test]
        public async Task Validate_ValidARC3Request_HasIpfsGuardrailFinding()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ARC3",
                TokenName      = "ARC3 Token",
                TokenSymbol    = "A3T",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.IsValid, Is.True);
            // ARC3 should trigger IPFS guardrail info
            Assert.That(result.GuardrailFindings.Any(g => g.GuardrailId == "GR-007"), Is.True);
        }

        [Test]
        public async Task Validate_StandardNetworkMismatch_HasMismatchError()
        {
            var result = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ERC20",
                TokenName      = "EVM Token",
                TokenSymbol    = "ET",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 18,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(result.Results.Any(r => r.ErrorCode == "STANDARD_NETWORK_MISMATCH"), Is.True);
        }

        // ── EvaluateReliabilityGuardrails ──────────────────────────────────────

        [Test]
        public void Guardrails_NodeUnreachable_ReturnsBlockingGr001()
        {
            var context = new GuardrailEvaluationContext
            {
                TokenStandard        = "ASA",
                Network              = "algorand-testnet",
                NodeReachable        = false,
                CreatorAddressValid  = true,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            Assert.That(findings.Any(g => g.GuardrailId == "GR-001" && g.IsBlocking), Is.True);
        }

        [Test]
        public void Guardrails_NodeUnreachable_SeverityIsError()
        {
            var context = new GuardrailEvaluationContext
            {
                NodeReachable       = false,
                CreatorAddressValid = true,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            var gr001 = findings.FirstOrDefault(g => g.GuardrailId == "GR-001");
            Assert.That(gr001, Is.Not.Null);
            Assert.That(gr001!.Severity, Is.EqualTo(GuardrailSeverity.Error));
        }

        [Test]
        public void Guardrails_NodeUnreachable_HasRemediationHint()
        {
            var context = new GuardrailEvaluationContext
            {
                NodeReachable       = false,
                CreatorAddressValid = true,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            var gr001 = findings.First(g => g.GuardrailId == "GR-001");
            Assert.That(gr001.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void Guardrails_RetryLimitExceeded_ReturnsBlockingGr002()
        {
            var context = new GuardrailEvaluationContext
            {
                NodeReachable        = true,
                CreatorAddressValid  = true,
                RetryCount           = 3,
                MaxRetryAttempts     = 3,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            Assert.That(findings.Any(g => g.GuardrailId == "GR-002" && g.IsBlocking), Is.True);
        }

        [Test]
        public void Guardrails_RetryNotExceeded_DoesNotReturnGr002()
        {
            var context = new GuardrailEvaluationContext
            {
                NodeReachable        = true,
                CreatorAddressValid  = true,
                RetryCount           = 2,
                MaxRetryAttempts     = 3,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            Assert.That(findings.Any(g => g.GuardrailId == "GR-002"), Is.False);
        }

        [Test]
        public void Guardrails_TimedOut_ReturnsBlockingGr003()
        {
            var context = new GuardrailEvaluationContext
            {
                NodeReachable        = true,
                CreatorAddressValid  = true,
                IsTimedOut           = true,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            Assert.That(findings.Any(g => g.GuardrailId == "GR-003" && g.IsBlocking), Is.True);
        }

        [Test]
        public void Guardrails_InFlightDuplicate_ReturnsNonBlockingWarningGr004()
        {
            var context = new GuardrailEvaluationContext
            {
                NodeReachable        = true,
                CreatorAddressValid  = true,
                HasInFlightDuplicate = true,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            var gr004 = findings.FirstOrDefault(g => g.GuardrailId == "GR-004");
            Assert.That(gr004, Is.Not.Null);
            Assert.That(gr004!.IsBlocking, Is.False);
            Assert.That(gr004.Severity, Is.EqualTo(GuardrailSeverity.Warning));
        }

        [Test]
        public void Guardrails_InvalidCreatorAddress_ReturnsBlockingGr005()
        {
            var context = new GuardrailEvaluationContext
            {
                NodeReachable        = true,
                CreatorAddressValid  = false,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            Assert.That(findings.Any(g => g.GuardrailId == "GR-005" && g.IsBlocking), Is.True);
        }

        [Test]
        public void Guardrails_ConflictingDeployment_ReturnsNonBlockingWarningGr006()
        {
            var context = new GuardrailEvaluationContext
            {
                NodeReachable                = true,
                CreatorAddressValid          = true,
                ConflictingDeploymentDetected = true,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            var gr006 = findings.FirstOrDefault(g => g.GuardrailId == "GR-006");
            Assert.That(gr006, Is.Not.Null);
            Assert.That(gr006!.IsBlocking, Is.False);
            Assert.That(gr006.Severity, Is.EqualTo(GuardrailSeverity.Warning));
        }

        [Test]
        public void Guardrails_ARC3RequiresIpfs_ReturnsInfoGr007()
        {
            var context = new GuardrailEvaluationContext
            {
                TokenStandard        = "ARC3",
                NodeReachable        = true,
                CreatorAddressValid  = true,
                RequiresIpfs         = true,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            var gr007 = findings.FirstOrDefault(g => g.GuardrailId == "GR-007");
            Assert.That(gr007, Is.Not.Null);
            Assert.That(gr007!.Severity, Is.EqualTo(GuardrailSeverity.Info));
            Assert.That(gr007.IsBlocking, Is.False);
        }

        [Test]
        public void Guardrails_AllClear_ReturnsNoFindings()
        {
            var context = new GuardrailEvaluationContext
            {
                NodeReachable        = true,
                CreatorAddressValid  = true,
                RequiresIpfs         = false,
                IsTimedOut           = false,
                HasInFlightDuplicate = false,
                ConflictingDeploymentDetected = false,
                RetryCount           = 0,
                MaxRetryAttempts     = 3,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            Assert.That(findings, Is.Empty);
        }

        [Test]
        public void Guardrails_NullContext_ReturnsEmptyList()
        {
            var findings = _service.EvaluateReliabilityGuardrails(null!);
            Assert.That(findings, Is.Empty);
        }

        [Test]
        public void Guardrails_MultipleIssues_ReturnsMultipleFindings()
        {
            var context = new GuardrailEvaluationContext
            {
                NodeReachable        = false,
                CreatorAddressValid  = false,
                IsTimedOut           = true,
                HasInFlightDuplicate = true,
            };
            var findings = _service.EvaluateReliabilityGuardrails(context);
            Assert.That(findings.Count, Is.GreaterThanOrEqualTo(3));
        }

        // ── DeriveDeploymentId ─────────────────────────────────────────────────

        [Test]
        public void DeriveDeploymentId_SameKeyAndNetwork_ReturnsSameId()
        {
            var id1 = _service.DeriveDeploymentId("key-abc", "algorand-testnet");
            var id2 = _service.DeriveDeploymentId("key-abc", "algorand-testnet");
            Assert.That(id2, Is.EqualTo(id1));
        }

        [Test]
        public void DeriveDeploymentId_DifferentKeys_ReturnsDifferentIds()
        {
            var id1 = _service.DeriveDeploymentId("key-abc", "algorand-testnet");
            var id2 = _service.DeriveDeploymentId("key-xyz", "algorand-testnet");
            Assert.That(id2, Is.Not.EqualTo(id1));
        }

        [Test]
        public void DeriveDeploymentId_DifferentNetworks_ReturnsDifferentIds()
        {
            var id1 = _service.DeriveDeploymentId("key-abc", "algorand-testnet");
            var id2 = _service.DeriveDeploymentId("key-abc", "algorand-mainnet");
            Assert.That(id2, Is.Not.EqualTo(id1));
        }

        [Test]
        public void DeriveDeploymentId_EmptyKey_ReturnsNonEmptyId()
        {
            var id = _service.DeriveDeploymentId(string.Empty, "algorand-testnet");
            Assert.That(id, Is.Not.Null.And.Not.Empty);
        }

        // ── BuildProgress ─────────────────────────────────────────────────────

        [Test]
        public void BuildProgress_CompletedStage_Returns100Percent()
        {
            var progress = _service.BuildProgress(DeploymentStage.Completed, 0);
            Assert.That(progress.PercentComplete, Is.EqualTo(100));
        }

        [Test]
        public void BuildProgress_FailedStage_Returns0Percent()
        {
            var progress = _service.BuildProgress(DeploymentStage.Failed, 0);
            Assert.That(progress.PercentComplete, Is.EqualTo(0));
        }

        [Test]
        public void BuildProgress_InitialisingStage_Returns5Percent()
        {
            var progress = _service.BuildProgress(DeploymentStage.Initialising, 0);
            Assert.That(progress.PercentComplete, Is.EqualTo(5));
        }

        [Test]
        public void BuildProgress_HasStages()
        {
            var progress = _service.BuildProgress(DeploymentStage.Completed, 0);
            Assert.That(progress.Stages, Is.Not.Empty);
        }

        [Test]
        public void BuildProgress_CompletedStage_HasSummary()
        {
            var progress = _service.BuildProgress(DeploymentStage.Completed, 0);
            Assert.That(progress.Summary, Is.Not.Null.And.Not.Empty);
        }

        // ── Error response contract ────────────────────────────────────────────

        [Test]
        public async Task Initiate_ValidationFailure_HasMachineReadableErrorCodes()
        {
            var req = BuildValidAlgorandRequest(totalSupply: 0);
            var result = await _service.InitiateDeploymentAsync(req);
            var failedValidation = result.ValidationResults.Where(v => !v.IsValid).ToList();
            Assert.That(failedValidation.All(v => !string.IsNullOrEmpty(v.ErrorCode)), Is.True,
                "All failed validations must have machine-readable error codes.");
        }

        [Test]
        public async Task Initiate_ValidationFailure_HasHumanReadableMessages()
        {
            var req = BuildValidAlgorandRequest(totalSupply: 0);
            var result = await _service.InitiateDeploymentAsync(req);
            var failedValidation = result.ValidationResults.Where(v => !v.IsValid).ToList();
            Assert.That(failedValidation.All(v => !string.IsNullOrEmpty(v.Message)), Is.True,
                "All failed validations must have human-readable messages.");
        }

        [Test]
        public async Task Initiate_ValidationFailure_HasFieldName()
        {
            var req = BuildValidAlgorandRequest(totalSupply: 0);
            var result = await _service.InitiateDeploymentAsync(req);
            var failedValidation = result.ValidationResults.Where(v => !v.IsValid).ToList();
            Assert.That(failedValidation.All(v => !string.IsNullOrEmpty(v.FieldName)), Is.True,
                "All failed validations must identify the failing field.");
        }

        // ── E2E lifecycle proof ────────────────────────────────────────────────

        [Test]
        public async Task E2E_FullLifecyclePath_ValidateInitiateStatusTelemetry()
        {
            // 1. Validate inputs
            var validationResp = await _service.ValidateDeploymentInputsAsync(new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "E2E Token",
                TokenSymbol    = "E2E",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            });
            Assert.That(validationResp.IsValid, Is.True, "Step 1: validation should pass.");

            // 2. Initiate deployment
            var key = Guid.NewGuid().ToString();
            var req = BuildValidAlgorandRequest(idempotencyKey: key);
            var initiated = await _service.InitiateDeploymentAsync(req);
            Assert.That(initiated.Stage, Is.EqualTo(DeploymentStage.Completed), "Step 2: deployment should complete.");
            Assert.That(initiated.DeploymentId, Is.Not.Null.And.Not.Empty, "Step 2: deployment ID required.");

            // 3. Query status
            var status = await _service.GetDeploymentStatusAsync(initiated.DeploymentId);
            Assert.That(status.Stage, Is.EqualTo(DeploymentStage.Completed), "Step 3: status should match.");
            Assert.That(status.AssetId, Is.EqualTo(initiated.AssetId), "Step 3: asset ID should match.");

            // 4. Retrieve telemetry
            var events = await _service.GetTelemetryEventsAsync(initiated.DeploymentId);
            Assert.That(events, Is.Not.Empty, "Step 4: telemetry events required.");
            Assert.That(events.Any(e => e.EventType == TelemetryEventType.CompletionSuccess),
                Is.True, "Step 4: completion event required.");
        }

        [Test]
        public async Task E2E_IdempotencyReplay_ThreeRunsIdentical()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildValidAlgorandRequest(idempotencyKey: key);
            var r1 = await _service.InitiateDeploymentAsync(req);
            var r2 = await _service.InitiateDeploymentAsync(req);
            var r3 = await _service.InitiateDeploymentAsync(req);
            // All three must produce identical evidence
            Assert.That(r2.AssetId, Is.EqualTo(r1.AssetId));
            Assert.That(r3.AssetId, Is.EqualTo(r1.AssetId));
            Assert.That(r2.TransactionId, Is.EqualTo(r1.TransactionId));
            Assert.That(r3.TransactionId, Is.EqualTo(r1.TransactionId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
            Assert.That(r3.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task E2E_InvalidInputPath_ExplicitFailureNotPermissiveSuccess()
        {
            // Multiple invalid fields - must NOT return success-shaped response
            var req = new TokenDeploymentLifecycleRequest
            {
                TokenStandard  = string.Empty,
                TokenName      = string.Empty,
                TokenSymbol    = string.Empty,
                Network        = string.Empty,
                TotalSupply    = 0,
                Decimals       = -1,
                CreatorAddress = string.Empty,
            };
            var result = await _service.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.Not.EqualTo(DeploymentStage.Completed),
                "Completely invalid request must not succeed.");
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
            Assert.That(result.ValidationResults.Any(v => !v.IsValid), Is.True,
                "Must expose specific validation failures.");
        }

        [Test]
        public async Task E2E_SchemaContractAssertions_AllRequiredFieldsPresent()
        {
            var req = BuildValidAlgorandRequest();
            var result = await _service.InitiateDeploymentAsync(req);
            // Contract assertions on required response fields
            Assert.That(result.DeploymentId,   Is.Not.Null.And.Not.Empty, "DeploymentId required.");
            Assert.That(result.IdempotencyKey, Is.Not.Null.And.Not.Empty, "IdempotencyKey required.");
            Assert.That(result.CorrelationId,  Is.Not.Null.And.Not.Empty, "CorrelationId required.");
            Assert.That(result.SchemaVersion,  Is.Not.Null.And.Not.Empty, "SchemaVersion required.");
            Assert.That(result.InitiatedAt,    Is.Not.EqualTo(default(DateTimeOffset)), "InitiatedAt required.");
            Assert.That(result.LastUpdatedAt,  Is.Not.EqualTo(default(DateTimeOffset)), "LastUpdatedAt required.");
            Assert.That(result.Progress,       Is.Not.Null, "Progress required.");
            Assert.That(result.TelemetryEvents, Is.Not.Null, "TelemetryEvents required.");
            Assert.That(result.ValidationResults, Is.Not.Null, "ValidationResults required.");
            Assert.That(result.GuardrailFindings, Is.Not.Null, "GuardrailFindings required.");
        }
    }
}
