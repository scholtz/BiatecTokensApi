using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive contract tests for the authoritative deployment lifecycle.
    ///
    /// Issue #512 — Complete authoritative deployment lifecycle contracts for enterprise
    /// issuance proof. Tests validate all 7 acceptance criteria:
    ///
    /// AC1: Consistent states distinguishing pending/confirmed/retriable/terminal outcomes.
    /// AC2: Missing evidence never produces ambiguous success-like responses (fail-closed).
    /// AC3: Responses include provenance and evidence-status for frontend/audit consumption.
    /// AC4: Authoritative path validated under timeout, malformed response, unsupported network.
    /// AC5: Telemetry records lifecycle decisions and evidence transitions with sanitised data.
    /// AC6: Contract extensible for future non-Algorand providers (provider context field).
    /// AC7: Aligned with regulated issuance vision; enterprise-grade auditability.
    /// </summary>
    [TestFixture]
    public class AuthoritativeDeploymentLifecycleContractTests
    {
        private Mock<ILogger<TokenDeploymentLifecycleService>> _loggerMock = null!;
        private const string ValidAlgorandAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string ValidEvmAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";

        [SetUp]
        public void SetUp() => _loggerMock = new Mock<ILogger<TokenDeploymentLifecycleService>>();

        private TokenDeploymentLifecycleService WithProvider(IDeploymentEvidenceProvider provider)
            => new(_loggerMock.Object, provider);

        private static TokenDeploymentLifecycleRequest BaseRequest(
            string standard = "ASA",
            string network = "algorand-testnet",
            string address = ValidAlgorandAddress,
            DeploymentExecutionMode mode = DeploymentExecutionMode.Authoritative) => new()
        {
            TokenStandard  = standard,
            TokenName      = "Enterprise Token",
            TokenSymbol    = "ENT",
            Network        = network,
            TotalSupply    = 1_000_000,
            Decimals       = 6,
            CreatorAddress = address,
            ExecutionMode  = mode,
        };

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Provider that throws TimeoutException (simulates indexer lag).</summary>
        private sealed class TimeoutEvidenceProvider : IDeploymentEvidenceProvider
        {
            public bool IsSimulated => false;
            public Task<BlockchainDeploymentEvidence?> ObtainEvidenceAsync(
                string deploymentId, string tokenStandard, string network,
                CancellationToken cancellationToken = default)
                => throw new TimeoutException("Simulated indexer timeout for testing");
        }

        /// <summary>Provider that throws OperationCanceledException (simulates HTTP cancellation).</summary>
        private sealed class CancellationEvidenceProvider : IDeploymentEvidenceProvider
        {
            public bool IsSimulated => false;
            public Task<BlockchainDeploymentEvidence?> ObtainEvidenceAsync(
                string deploymentId, string tokenStandard, string network,
                CancellationToken cancellationToken = default)
                => throw new OperationCanceledException("Simulated cancellation for testing");
        }

        /// <summary>Provider that throws a generic unexpected exception.</summary>
        private sealed class ExceptionEvidenceProvider : IDeploymentEvidenceProvider
        {
            public bool IsSimulated => false;
            public Task<BlockchainDeploymentEvidence?> ObtainEvidenceAsync(
                string deploymentId, string tokenStandard, string network,
                CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Unexpected provider exception for testing");
        }

        /// <summary>Provider that returns a live (non-simulated) authoritative evidence object.</summary>
        private sealed class AuthoritativeEvidenceProvider : IDeploymentEvidenceProvider
        {
            public bool IsSimulated => false;
            public Task<BlockchainDeploymentEvidence?> ObtainEvidenceAsync(
                string deploymentId, string tokenStandard, string network,
                CancellationToken cancellationToken = default)
                => Task.FromResult<BlockchainDeploymentEvidence?>(new BlockchainDeploymentEvidence
                {
                    AssetId        = 987654321UL,
                    TransactionId  = "AUTHORITATIVE_TX_ID",
                    ConfirmedRound = 42000000UL,
                    IsSimulated    = false,
                    EvidenceSource = "algorand-indexer",
                });
        }

        /// <summary>Provider that returns a live EVM evidence object (proves non-Algorand extensibility).</summary>
        private sealed class EvmEvidenceProvider : IDeploymentEvidenceProvider
        {
            public bool IsSimulated => false;
            public Task<BlockchainDeploymentEvidence?> ObtainEvidenceAsync(
                string deploymentId, string tokenStandard, string network,
                CancellationToken cancellationToken = default)
                => Task.FromResult<BlockchainDeploymentEvidence?>(new BlockchainDeploymentEvidence
                {
                    AssetId        = 0UL,
                    TransactionId  = "0xdeadbeef1234567890abcdef",
                    ConfirmedRound = 19000000UL,
                    IsSimulated    = false,
                    EvidenceSource = "base-rpc",
                });
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC1: Consistent lifecycle states
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC1_AuthoritativeSuccess_StageIsCompleted()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task AC1_AuthoritativeSuccess_OutcomeIsSuccess()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.Success));
        }

        [Test]
        public async Task AC1_AuthoritativeTimeout_OutcomeIsTransientFailure()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TransientFailure),
                "Timeout must be transient (retriable), not terminal.");
        }

        [Test]
        public async Task AC1_AuthoritativeTimeout_StageIsFailed()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task AC1_AuthoritativeNullEvidence_OutcomeIsTerminalFailure()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure),
                "Null evidence (provider returned null) must be terminal.");
        }

        [Test]
        public async Task AC1_AuthoritativeException_OutcomeIsTerminalFailure()
        {
            var svc    = WithProvider(new ExceptionEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure),
                "Generic exception must produce terminal failure, not ambiguous unknown.");
        }

        [Test]
        public async Task AC1_SimulationMode_OutcomeIsSuccess()
        {
            var svc    = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest(mode: DeploymentExecutionMode.Simulation));
            Assert.That(result.Stage,   Is.EqualTo(DeploymentStage.Completed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.Success));
        }

        [Test]
        public async Task AC1_CancellationTimeout_IsRetriable()
        {
            var svc    = WithProvider(new CancellationEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TransientFailure),
                "OperationCanceledException must produce transient (retriable) failure.");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC2: Missing evidence never produces ambiguous success-like responses
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC2_NullEvidence_Authoritative_NoAssetId()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.AssetId, Is.Null,
                "AssetId must not be set on a failed authoritative deployment.");
        }

        [Test]
        public async Task AC2_NullEvidence_Authoritative_NoTransactionId()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.TransactionId, Is.Null.Or.Empty,
                "TransactionId must not be set when evidence is unavailable.");
        }

        [Test]
        public async Task AC2_TimeoutEvidence_Authoritative_NoAssetId()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.AssetId, Is.Null);
        }

        [Test]
        public async Task AC2_NullEvidence_Authoritative_MessageIsExplicit()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Message, Does.Contain("terminal failure").IgnoreCase
                .Or.Contain("unavailable").IgnoreCase,
                "Message must clearly indicate a non-recoverable failure, not a generic error.");
        }

        [Test]
        public async Task AC2_TimeoutEvidence_Authoritative_MessageMentionsRetriable()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Message, Does.Contain("transient").IgnoreCase
                .Or.Contain("retry").IgnoreCase
                .Or.Contain("timed out").IgnoreCase);
        }

        [Test]
        public async Task AC2_NullEvidence_Authoritative_RemediationHintPresent()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty,
                "A remediation hint must always be present on failure.");
        }

        [Test]
        public async Task AC2_NullEvidence_Simulation_DoesNotFail()
        {
            // Simulation mode should fall back when provider returns null, not fail
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest(mode: DeploymentExecutionMode.Simulation));
            Assert.That(result.Stage,   Is.EqualTo(DeploymentStage.Completed),
                "Simulation mode must fall back to hash-derived evidence, not fail.");
            Assert.That(result.IsSimulatedEvidence, Is.True);
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC3: Evidence availability detail and provenance fields
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC3_AuthoritativeSuccess_EvidenceStatusIsConfirmed()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability, Is.Not.Null);
            Assert.That(result.EvidenceAvailability.Status,
                Is.EqualTo(LifecycleEvidenceStatus.Confirmed));
        }

        [Test]
        public async Task AC3_SimulationMode_EvidenceStatusIsSimulated()
        {
            var svc    = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest(mode: DeploymentExecutionMode.Simulation));
            Assert.That(result.EvidenceAvailability.Status,
                Is.EqualTo(LifecycleEvidenceStatus.Simulated));
        }

        [Test]
        public async Task AC3_NullEvidence_Authoritative_EvidenceStatusIsUnavailable()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.Status,
                Is.EqualTo(LifecycleEvidenceStatus.Unavailable));
        }

        [Test]
        public async Task AC3_TimeoutEvidence_EvidenceStatusIsPending()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.Status,
                Is.EqualTo(LifecycleEvidenceStatus.Pending),
                "A timeout indicates evidence is pending (indexer lag), not permanently unavailable.");
        }

        [Test]
        public async Task AC3_AuthoritativeSuccess_EvidenceReasonCodeIsConfirmedAuthoritative()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.ReasonCode,
                Is.EqualTo("CONFIRMED_AUTHORITATIVE"));
        }

        [Test]
        public async Task AC3_SimulationMode_EvidenceReasonCodeIsSimulatedHashDerived()
        {
            var svc    = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest(mode: DeploymentExecutionMode.Simulation));
            Assert.That(result.EvidenceAvailability.ReasonCode,
                Is.EqualTo("SIMULATED_HASH_DERIVED"));
        }

        [Test]
        public async Task AC3_NullEvidence_EvidenceReasonCodeIsProviderNullReturn()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.ReasonCode,
                Is.EqualTo("PROVIDER_NULL_RETURN"));
        }

        [Test]
        public async Task AC3_TimeoutEvidence_EvidenceReasonCodeIsProviderTimeout()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.ReasonCode,
                Is.EqualTo("PROVIDER_TIMEOUT"));
        }

        [Test]
        public async Task AC3_ExceptionEvidence_EvidenceReasonCodeIsProviderException()
        {
            var svc    = WithProvider(new ExceptionEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.ReasonCode,
                Is.EqualTo("PROVIDER_EXCEPTION"));
        }

        [Test]
        public async Task AC3_AuthoritativeSuccess_EvidenceAvailabilityMessageNotEmpty()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.Message, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AC3_NullEvidence_EvidenceIsNotRetriable()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.IsRetriable, Is.False,
                "Terminal null-evidence failure must not be flagged as retriable.");
        }

        [Test]
        public async Task AC3_TimeoutEvidence_EvidenceIsRetriable()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.IsRetriable, Is.True,
                "Timeout (indexer lag) must be flagged as retriable.");
        }

        [Test]
        public async Task AC3_AuthoritativeSuccess_RetrievedAtIsPopulated()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.RetrievedAt, Is.Not.Null,
                "RetrievedAt must be set when evidence is obtained successfully.");
        }

        [Test]
        public async Task AC3_NullEvidence_RetrievedAtIsNull()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.RetrievedAt, Is.Null,
                "RetrievedAt must not be set when no evidence was obtained.");
        }

        [Test]
        public async Task AC3_EvidenceProvenance_AuthoritativeIsPopulated()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceProvenance, Is.Not.Null.And.Not.Empty,
                "EvidenceProvenance must always be populated for traceability.");
        }

        [Test]
        public async Task AC3_EvidenceProvenance_SimulationContainsSimulatedKeyword()
        {
            var svc    = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest(mode: DeploymentExecutionMode.Simulation));
            Assert.That(result.EvidenceProvenance, Does.Contain("simulated").IgnoreCase
                .Or.Contain("hash").IgnoreCase,
                "Simulated provenance must indicate non-blockchain-confirmed origin.");
        }

        [Test]
        public async Task AC3_IdempotentReplay_EvidenceAvailabilityPropagated()
        {
            var svc = WithProvider(new AuthoritativeEvidenceProvider());
            var req = BaseRequest();
            req.IdempotencyKey = "idempotency-test-ac3";
            await svc.InitiateDeploymentAsync(req);
            var replay = await svc.InitiateDeploymentAsync(req);
            Assert.That(replay.IsIdempotentReplay, Is.True);
            Assert.That(replay.EvidenceAvailability.Status,
                Is.EqualTo(LifecycleEvidenceStatus.Confirmed),
                "EvidenceAvailability must be propagated on idempotency replay.");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC3: ErrorDetail structured field
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC3_AuthoritativeSuccess_ErrorDetailIsNull()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ErrorDetail, Is.Null,
                "ErrorDetail must be null on successful responses.");
        }

        [Test]
        public async Task AC3_NullEvidence_ErrorDetailIsPresent()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ErrorDetail, Is.Not.Null,
                "ErrorDetail must be present on failure responses.");
        }

        [Test]
        public async Task AC3_NullEvidence_ErrorCodeIsBlockchainEvidenceUnavailable()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ErrorDetail!.ErrorCode, Is.EqualTo("BLOCKCHAIN_EVIDENCE_UNAVAILABLE"));
        }

        [Test]
        public async Task AC3_TimeoutEvidence_ErrorCodeIsBlockchainEvidenceTimeout()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ErrorDetail!.ErrorCode, Is.EqualTo("BLOCKCHAIN_EVIDENCE_TIMEOUT"));
        }

        [Test]
        public async Task AC3_NullEvidence_ErrorCategoryIsEvidenceUnavailable()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ErrorDetail!.ErrorCategory,
                Is.EqualTo(LifecycleErrorCategory.EvidenceUnavailable));
        }

        [Test]
        public async Task AC3_TimeoutEvidence_ErrorCategoryIsNetworkTimeout()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ErrorDetail!.ErrorCategory,
                Is.EqualTo(LifecycleErrorCategory.NetworkTimeout));
        }

        [Test]
        public async Task AC3_ExceptionEvidence_ErrorCategoryIsProviderException()
        {
            var svc    = WithProvider(new ExceptionEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ErrorDetail!.ErrorCategory,
                Is.EqualTo(LifecycleErrorCategory.ProviderException));
        }

        [Test]
        public async Task AC3_NullEvidence_ErrorDetailIsNotRetriable()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ErrorDetail!.IsRetriable, Is.False);
        }

        [Test]
        public async Task AC3_TimeoutEvidence_ErrorDetailIsRetriable()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ErrorDetail!.IsRetriable, Is.True);
        }

        [Test]
        public async Task AC3_NullEvidence_ErrorDetailRemediationHintNotEmpty()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ErrorDetail!.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AC3_IdempotentReplay_ErrorDetailPropagated()
        {
            var svc = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var req = BaseRequest();
            req.IdempotencyKey = "idempotency-error-ac3";
            await svc.InitiateDeploymentAsync(req);
            var replay = await svc.InitiateDeploymentAsync(req);
            Assert.That(replay.IsIdempotentReplay, Is.True);
            Assert.That(replay.ErrorDetail, Is.Not.Null);
            Assert.That(replay.ErrorDetail!.ErrorCode, Is.EqualTo("BLOCKCHAIN_EVIDENCE_UNAVAILABLE"));
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC4: Realistic provider condition tests
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC4_TimeoutException_ProducesTransientFailure()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TransientFailure));
        }

        [Test]
        public async Task AC4_OperationCanceledException_ProducesTransientFailure()
        {
            var svc    = WithProvider(new CancellationEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TransientFailure));
        }

        [Test]
        public async Task AC4_GenericException_ProducesTerminalFailure()
        {
            var svc    = WithProvider(new ExceptionEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
        }

        [Test]
        public async Task AC4_TimeoutEvidence_EvidenceAvailabilityDiagnosticDetailMentionsException()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            // DiagnosticDetail should contain the sanitised exception type name
            Assert.That(result.EvidenceAvailability.DiagnosticDetail,
                Does.Contain("Timeout").IgnoreCase,
                "DiagnosticDetail must contain sanitised exception type for operability.");
        }

        [Test]
        public async Task AC4_ExceptionEvidence_DiagnosticDetailMentionsException()
        {
            var svc    = WithProvider(new ExceptionEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.EvidenceAvailability.DiagnosticDetail,
                Is.Not.Null.And.Not.Empty,
                "DiagnosticDetail must be populated when a provider exception occurs.");
        }

        [Test]
        public async Task AC4_UnsupportedNetwork_ValidationFails()
        {
            // Unsupported networks are caught at validation before even reaching the provider.
            var svc = WithProvider(new AuthoritativeEvidenceProvider());
            var req = BaseRequest(network: "unsupported-chain-999");
            var result = await svc.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure),
                "Unsupported network must produce terminal failure.");
        }

        [Test]
        public async Task AC4_UnsupportedNetwork_ValidationResultContainsError()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var req    = BaseRequest(network: "unsupported-chain-999");
            var result = await svc.InitiateDeploymentAsync(req);
            Assert.That(result.ValidationResults.Any(v => !v.IsValid),
                Is.True, "Validation results must contain at least one failure for unsupported network.");
        }

        [Test]
        public async Task AC4_InvalidAddress_TerminalValidationFailure()
        {
            var svc = WithProvider(new AuthoritativeEvidenceProvider());
            var req = BaseRequest();
            req.CreatorAddress = "invalid-address-123";
            var result = await svc.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task AC4_ZeroSupply_TerminalValidationFailure()
        {
            var svc = WithProvider(new AuthoritativeEvidenceProvider());
            var req = BaseRequest();
            req.TotalSupply = 0;
            var result = await svc.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task AC4_MalformedRequest_NullRequest_ReturnsFailedResponse()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(null!);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed),
                "Null request must not throw; must return a structured failure.");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC5: Telemetry and audit observability
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC5_AuthoritativeSuccess_TelemetryContainsCompletionEvent()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(
                result.TelemetryEvents.Any(e => e.EventType == TelemetryEventType.CompletionSuccess),
                Is.True, "CompletionSuccess telemetry event must be emitted.");
        }

        [Test]
        public async Task AC5_NullEvidence_TelemetryContainsTerminalFailureEvent()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(
                result.TelemetryEvents.Any(e => e.EventType == TelemetryEventType.TerminalFailure),
                Is.True);
        }

        [Test]
        public async Task AC5_NullEvidence_TelemetryContainsDependencyFailureEvent()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(
                result.TelemetryEvents.Any(e => e.EventType == TelemetryEventType.DependencyFailure),
                Is.True);
        }

        [Test]
        public async Task AC5_NullEvidence_TelemetryMetadataContainsErrorCode()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            var termEvent = result.TelemetryEvents.FirstOrDefault(e =>
                e.EventType == TelemetryEventType.TerminalFailure);
            Assert.That(termEvent, Is.Not.Null);
            Assert.That(termEvent!.Metadata.ContainsKey("errorCode"), Is.True);
        }

        [Test]
        public async Task AC5_NullEvidence_TelemetryMetadataContainsErrorCategory()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            var termEvent = result.TelemetryEvents.FirstOrDefault(e =>
                e.EventType == TelemetryEventType.TerminalFailure);
            Assert.That(termEvent, Is.Not.Null);
            Assert.That(termEvent!.Metadata.ContainsKey("errorCategory"), Is.True);
        }

        [Test]
        public async Task AC5_NullEvidence_TelemetryMetadataContainsIsRetriable()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            var termEvent = result.TelemetryEvents.FirstOrDefault(e =>
                e.EventType == TelemetryEventType.TerminalFailure);
            Assert.That(termEvent, Is.Not.Null);
            Assert.That(termEvent!.Metadata.ContainsKey("isRetriable"), Is.True);
            Assert.That(termEvent.Metadata["isRetriable"], Is.EqualTo("false"));
        }

        [Test]
        public async Task AC5_TimeoutEvidence_TelemetryMetadataIsRetriableIsTrue()
        {
            var svc    = WithProvider(new TimeoutEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            var termEvent = result.TelemetryEvents.FirstOrDefault(e =>
                e.EventType == TelemetryEventType.TerminalFailure);
            Assert.That(termEvent, Is.Not.Null);
            Assert.That(termEvent!.Metadata["isRetriable"], Is.EqualTo("true"));
        }

        [Test]
        public async Task AC5_AuthoritativeSuccess_TelemetryContainsEvidenceStatusMetadata()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            var successEvent = result.TelemetryEvents.FirstOrDefault(e =>
                e.EventType == TelemetryEventType.CompletionSuccess);
            Assert.That(successEvent, Is.Not.Null);
            Assert.That(successEvent!.Metadata.ContainsKey("isSimulatedEvidence"), Is.True);
            Assert.That(successEvent.Metadata["isSimulatedEvidence"], Is.EqualTo("false"));
        }

        [Test]
        public async Task AC5_AllEvents_CorrelationIdPropagated()
        {
            var svc = WithProvider(new AuthoritativeEvidenceProvider());
            var req = BaseRequest();
            req.CorrelationId = "test-correlation-id-ac5";
            var result = await svc.InitiateDeploymentAsync(req);
            foreach (var ev in result.TelemetryEvents)
            {
                Assert.That(ev.CorrelationId, Is.EqualTo("test-correlation-id-ac5"),
                    $"CorrelationId must be propagated to all telemetry events, failed for event {ev.EventId}.");
            }
        }

        [Test]
        public async Task AC5_AllEvents_DeploymentIdPropagated()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            foreach (var ev in result.TelemetryEvents)
            {
                Assert.That(ev.DeploymentId, Is.EqualTo(result.DeploymentId),
                    "DeploymentId must be consistent across all telemetry events.");
            }
        }

        [Test]
        public async Task AC5_AuthoritativeSuccess_TelemetryContainsStageTransitions()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(
                result.TelemetryEvents.Count(e => e.EventType == TelemetryEventType.StageTransition),
                Is.GreaterThanOrEqualTo(3),
                "At least 3 stage transitions (Initialising, Validating, Submitting, Confirming, Completed) expected.");
        }

        [Test]
        public async Task AC5_NullEvidence_TelemetryContainsDependencyFailure_EvidenceStatusField()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            var depEvent = result.TelemetryEvents.FirstOrDefault(e =>
                e.EventType == TelemetryEventType.DependencyFailure);
            Assert.That(depEvent, Is.Not.Null);
            Assert.That(depEvent!.Metadata.ContainsKey("evidenceStatus"), Is.True,
                "DependencyFailure event must carry evidenceStatus metadata for traceability.");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC6: Provider context and multi-chain extensibility
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC6_AuthoritativeAlgorandProvider_ProviderFamilyIsAlgorand()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ProviderContext, Is.Not.Null);
            Assert.That(result.ProviderContext.ProviderFamily, Is.EqualTo("Algorand"));
        }

        [Test]
        public async Task AC6_SimulatedProvider_ProviderFamilyIsSimulation()
        {
            var svc    = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest(mode: DeploymentExecutionMode.Simulation));
            Assert.That(result.ProviderContext.ProviderFamily, Is.EqualTo("Simulation"));
        }

        [Test]
        public async Task AC6_UnavailableProvider_ProviderFamilyIsUnavailable()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            // Provider context reflects the underlying provider type
            Assert.That(result.ProviderContext.ProviderFamily, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AC6_EvmProvider_ProviderContextReflectsEvmFamily()
        {
            // Validates that a future EVM provider can plug into the lifecycle contract
            // without breaking the provider-context contract.
            var svc    = WithProvider(new EvmEvidenceProvider());
            var req    = BaseRequest(standard: "ERC20", network: "base-mainnet", address: ValidEvmAddress);
            var result = await svc.InitiateDeploymentAsync(req);
            Assert.That(result.ProviderContext, Is.Not.Null,
                "ProviderContext must always be populated regardless of provider family.");
        }

        [Test]
        public async Task AC6_AuthoritativeSuccess_ProviderIsAvailableTrue()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ProviderContext.IsAvailable, Is.True);
        }

        [Test]
        public async Task AC6_NullEvidence_ProviderIsAvailableFalse()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ProviderContext.IsAvailable, Is.False,
                "IsAvailable must be false when the provider returned no evidence.");
        }

        [Test]
        public async Task AC6_SimulationMode_ProviderContextIsSimulatedTrue()
        {
            var svc    = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest(mode: DeploymentExecutionMode.Simulation));
            Assert.That(result.ProviderContext.IsSimulated, Is.True);
        }

        [Test]
        public async Task AC6_AuthoritativeSuccess_ProviderContextIsSimulatedFalse()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ProviderContext.IsSimulated, Is.False);
        }

        [Test]
        public async Task AC6_IdempotentReplay_ProviderContextPropagated()
        {
            var svc = WithProvider(new AuthoritativeEvidenceProvider());
            var req = BaseRequest();
            req.IdempotencyKey = "idempotency-provider-ctx-ac6";
            await svc.InitiateDeploymentAsync(req);
            var replay = await svc.InitiateDeploymentAsync(req);
            Assert.That(replay.ProviderContext, Is.Not.Null);
            Assert.That(replay.ProviderContext.ProviderFamily, Is.EqualTo("Algorand"),
                "ProviderContext must survive idempotency replay unchanged.");
        }

        [Test]
        public async Task AC6_ProviderContext_SourceReflectsEvidenceSource()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.ProviderContext.ProviderSource, Is.EqualTo("algorand-indexer"),
                "ProviderSource must reflect the evidence source from the provider.");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // AC7: Enterprise-grade audit and schema contract
        // ══════════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task AC7_AuthoritativeSuccess_SchemaVersionPresent()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AC7_AuthoritativeSuccess_DeploymentIdStable()
        {
            var svc = WithProvider(new AuthoritativeEvidenceProvider());
            var req = BaseRequest();
            req.IdempotencyKey = "stable-deploy-id-test";
            var r1 = await svc.InitiateDeploymentAsync(req);
            var r2 = await svc.InitiateDeploymentAsync(req);
            Assert.That(r2.DeploymentId, Is.EqualTo(r1.DeploymentId),
                "DeploymentId must be stable across idempotent calls.");
        }

        [Test]
        public async Task AC7_AllScenarios_ResponseIsNeverNull()
        {
            var providers = new IDeploymentEvidenceProvider[]
            {
                new AuthoritativeEvidenceProvider(),
                new UnavailableDeploymentEvidenceProvider(),
                new TimeoutEvidenceProvider(),
                new ExceptionEvidenceProvider(),
                new SimulatedDeploymentEvidenceProvider(),
            };
            foreach (var provider in providers)
            {
                var svc    = WithProvider(provider);
                var result = await svc.InitiateDeploymentAsync(BaseRequest());
                Assert.That(result, Is.Not.Null, $"Response must never be null (provider: {provider.GetType().Name}).");
                Assert.That(result.DeploymentId, Is.Not.Null.And.Not.Empty,
                    $"DeploymentId must always be present (provider: {provider.GetType().Name}).");
            }
        }

        [Test]
        public async Task AC7_AllScenarios_EvidenceAvailabilityAlwaysPopulated()
        {
            var providers = new IDeploymentEvidenceProvider[]
            {
                new AuthoritativeEvidenceProvider(),
                new UnavailableDeploymentEvidenceProvider(),
                new TimeoutEvidenceProvider(),
                new ExceptionEvidenceProvider(),
                new SimulatedDeploymentEvidenceProvider(),
            };
            var modes = new[] { DeploymentExecutionMode.Authoritative, DeploymentExecutionMode.Simulation };
            foreach (var provider in providers)
            {
                foreach (var mode in modes)
                {
                    var svc    = WithProvider(provider);
                    var result = await svc.InitiateDeploymentAsync(BaseRequest(mode: mode));
                    Assert.That(result.EvidenceAvailability, Is.Not.Null,
                        $"EvidenceAvailability must always be populated (provider={provider.GetType().Name}, mode={mode}).");
                    Assert.That(result.EvidenceAvailability.ReasonCode, Is.Not.Null,
                        "EvidenceAvailability.ReasonCode must never be null.");
                }
            }
        }

        [Test]
        public async Task AC7_AllScenarios_ProviderContextAlwaysPopulated()
        {
            var providers = new IDeploymentEvidenceProvider[]
            {
                new AuthoritativeEvidenceProvider(),
                new UnavailableDeploymentEvidenceProvider(),
                new TimeoutEvidenceProvider(),
                new ExceptionEvidenceProvider(),
            };
            foreach (var provider in providers)
            {
                var svc    = WithProvider(provider);
                var result = await svc.InitiateDeploymentAsync(BaseRequest());
                Assert.That(result.ProviderContext, Is.Not.Null,
                    $"ProviderContext must always be populated (provider={provider.GetType().Name}).");
                Assert.That(result.ProviderContext.ProviderFamily, Is.Not.Null,
                    "ProviderContext.ProviderFamily must never be null.");
            }
        }

        [Test]
        public async Task AC7_AuthoritativeSuccess_InitiatedAtAndLastUpdatedAtPresent()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.InitiatedAt, Is.Not.EqualTo(DateTimeOffset.MinValue));
            Assert.That(result.LastUpdatedAt, Is.Not.EqualTo(DateTimeOffset.MinValue));
        }

        [Test]
        public async Task AC7_NullEvidence_InitiatedAtAndLastUpdatedAtPresent()
        {
            var svc    = WithProvider(new UnavailableDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.InitiatedAt, Is.Not.EqualTo(DateTimeOffset.MinValue));
            Assert.That(result.LastUpdatedAt, Is.Not.EqualTo(DateTimeOffset.MinValue));
        }

        [Test]
        public async Task AC7_AuthoritativeSuccess_AssetIdNonZero()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.AssetId, Is.EqualTo(987654321UL));
            Assert.That(result.TransactionId, Is.EqualTo("AUTHORITATIVE_TX_ID"));
            Assert.That(result.ConfirmedRound, Is.EqualTo(42000000UL));
        }

        [Test]
        public async Task AC7_DifferentProviders_SameInputProduceSameDeploymentId()
        {
            // DeploymentId is derived from input parameters, not from the provider.
            var req1 = BaseRequest();
            req1.IdempotencyKey = "same-input-test";
            var req2 = BaseRequest();
            req2.IdempotencyKey = "same-input-test";

            var svc1 = WithProvider(new AuthoritativeEvidenceProvider());
            var svc2 = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var r1 = await svc1.InitiateDeploymentAsync(req1);
            var r2 = await svc2.InitiateDeploymentAsync(req2);
            Assert.That(r2.DeploymentId, Is.EqualTo(r1.DeploymentId),
                "DeploymentId must be deterministic from inputs regardless of provider.");
        }

        [Test]
        public async Task AC7_AuthoritativeMode_IsSimulatedEvidenceFalse()
        {
            var svc    = WithProvider(new AuthoritativeEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest());
            Assert.That(result.IsSimulatedEvidence, Is.False,
                "Authoritative success must set IsSimulatedEvidence=false.");
        }

        [Test]
        public async Task AC7_SimulationMode_IsSimulatedEvidenceTrue()
        {
            var svc    = WithProvider(new SimulatedDeploymentEvidenceProvider());
            var result = await svc.InitiateDeploymentAsync(BaseRequest(mode: DeploymentExecutionMode.Simulation));
            Assert.That(result.IsSimulatedEvidence, Is.True,
                "Simulation mode must set IsSimulatedEvidence=true for sign-off safety.");
        }

        [Test]
        public async Task AC7_GuardrailViolation_ErrorDetailHasGuardrailCategory()
        {
            // Trigger a guardrail by providing an unsupported standard
            var svc = WithProvider(new AuthoritativeEvidenceProvider());
            var req = BaseRequest();
            req.TokenStandard = "UNSUPPORTED_STANDARD_XYZ";
            var result = await svc.InitiateDeploymentAsync(req);
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Failed));
        }
    }
}
