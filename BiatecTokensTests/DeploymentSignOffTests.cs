using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using BiatecTokensApi.Models.DeploymentSignOff;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Comprehensive tests for <see cref="DeploymentSignOffService"/>.
    ///
    /// This test suite validates the sign-off hardening requirements from the backend
    /// deployment proof and compliance evidence issue:
    ///
    /// AC1: Sign-off-critical APIs return complete, deterministic contract data.
    /// AC2: Missing/ambiguous lifecycle fields treated as explicit failure conditions.
    /// AC3: Tests cover happy-path and failure-path lifecycle behavior.
    /// AC4: Error payloads provide structured info for frontend rendering.
    /// AC5: Aligned with email/password auth and backend-managed deployment.
    /// AC7: Audit/observability evidence fields captured and verifiable.
    /// </summary>
    [TestFixture]
    public class DeploymentSignOffTests
    {
        private Mock<IBackendDeploymentLifecycleContractService> _contractServiceMock = null!;
        private Mock<ILogger<DeploymentSignOffService>> _loggerMock = null!;
        private DeploymentSignOffService _service = null!;

        private const string TestDeploymentId = "deploy-abc123def456";
        private const string TestAlgorandAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string TestTransactionId = "TX123456789ABCDEF0123456789ABCDEF0123456789ABCDEF01234";
        private const ulong TestAssetId = 123456789UL;
        private const ulong TestConfirmedRound = 42_000_000UL;

        [SetUp]
        public void SetUp()
        {
            _contractServiceMock = new Mock<IBackendDeploymentLifecycleContractService>();
            _loggerMock = new Mock<ILogger<DeploymentSignOffService>>();
            _service = new DeploymentSignOffService(_contractServiceMock.Object, _loggerMock.Object);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>Builds a fully completed deployment response with all fields populated.</summary>
        private static BackendDeploymentContractResponse BuildCompletedResponse(
            string? deployerAddress = TestAlgorandAddress,
            ulong? assetId = TestAssetId,
            string? transactionId = TestTransactionId,
            ulong? confirmedRound = TestConfirmedRound,
            bool isDeterministic = true,
            bool isDegraded = false,
            ContractLifecycleState state = ContractLifecycleState.Completed)
        {
            return new BackendDeploymentContractResponse
            {
                DeploymentId     = TestDeploymentId,
                IdempotencyKey   = "idp-key-123",
                CorrelationId    = Guid.NewGuid().ToString(),
                State            = state,
                DerivationStatus = ARC76DerivationStatus.Derived,
                DeployerAddress  = deployerAddress,
                IsDeterministicAddress = isDeterministic,
                ErrorCode        = state == ContractLifecycleState.Completed ? DeploymentErrorCode.None : DeploymentErrorCode.InternalError,
                Message          = state == ContractLifecycleState.Completed ? "Deployment completed." : "Deployment failed.",
                AssetId          = assetId,
                TransactionId    = transactionId,
                ConfirmedRound   = confirmedRound,
                IsDegraded       = isDegraded,
                InitiatedAt      = DateTime.UtcNow.AddMinutes(-5).ToString("o"),
                LastUpdatedAt    = DateTime.UtcNow.ToString("o"),
                ValidationResults = new List<ContractValidationResult>(),
                AuditEvents      = new List<ComplianceAuditEvent>()
            };
        }

        /// <summary>Builds a non-empty audit trail.</summary>
        private static BackendDeploymentAuditTrail BuildAuditTrail(int eventCount = 3)
        {
            var events = Enumerable.Range(1, eventCount).Select(i => new ComplianceAuditEvent
            {
                EventId      = Guid.NewGuid().ToString(),
                CorrelationId = Guid.NewGuid().ToString(),
                DeploymentId = TestDeploymentId,
                EventKind    = ComplianceAuditEventKind.DeploymentInitiated,
                Timestamp    = DateTime.UtcNow.ToString("o"),
                Actor        = TestAlgorandAddress,
                StateAtEvent = ContractLifecycleState.Completed,
                Outcome      = "Success"
            }).ToList();

            return new BackendDeploymentAuditTrail
            {
                DeploymentId  = TestDeploymentId,
                CorrelationId = Guid.NewGuid().ToString(),
                FinalState    = ContractLifecycleState.Completed,
                DeployerAddress = TestAlgorandAddress,
                TokenStandard = "ASA",
                Network       = "algorand-testnet",
                Events        = events
            };
        }

        /// <summary>Builds an empty audit trail.</summary>
        private static BackendDeploymentAuditTrail BuildEmptyAuditTrail()
        {
            return new BackendDeploymentAuditTrail
            {
                DeploymentId  = TestDeploymentId,
                CorrelationId = Guid.NewGuid().ToString(),
                FinalState    = ContractLifecycleState.Failed,
                Events        = new List<ComplianceAuditEvent>()
            };
        }

        /// <summary>Builds a "not found" error response.</summary>
        private static BackendDeploymentContractResponse BuildNotFoundResponse(string deploymentId)
        {
            return new BackendDeploymentContractResponse
            {
                DeploymentId = deploymentId,
                CorrelationId = Guid.NewGuid().ToString(),
                State = ContractLifecycleState.Failed,
                ErrorCode = DeploymentErrorCode.RequiredFieldMissing,
                Message = $"Deployment '{deploymentId}' was not found.",
            };
        }

        private void SetupSuccessfulDeployment(
            BackendDeploymentContractResponse? status = null,
            BackendDeploymentAuditTrail? audit = null)
        {
            status ??= BuildCompletedResponse();
            audit ??= BuildAuditTrail();

            _contractServiceMock
                .Setup(s => s.GetStatusAsync(TestDeploymentId, It.IsAny<string?>()))
                .ReturnsAsync(status);

            _contractServiceMock
                .Setup(s => s.GetAuditTrailAsync(TestDeploymentId, It.IsAny<string?>()))
                .ReturnsAsync(audit);
        }

        // ── AC1: Happy-path – all criteria pass ───────────────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_CompleteDeployment_IsReadyForSignOff()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.IsReadyForSignOff, Is.True,
                "A fully completed deployment with all fields must be ready for sign-off");
        }

        [Test]
        public async Task ValidateSignOffReadiness_CompleteDeployment_VerdictIsApproved()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.Verdict, Is.EqualTo(SignOffVerdict.Approved));
        }

        [Test]
        public async Task ValidateSignOffReadiness_CompleteDeployment_NoFailedCriteria()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.FailedCriteriaCount, Is.EqualTo(0),
                "Zero criteria should fail for a fully completed deployment");
        }

        [Test]
        public async Task ValidateSignOffReadiness_CompleteDeployment_AllRequiredCriteriaPass()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            var failedRequired = result.Criteria.Where(c => c.IsRequired && c.Outcome == SignOffCriterionOutcome.Fail).ToList();
            Assert.That(failedRequired, Is.Empty,
                "No required criteria should fail for a complete deployment");
        }

        [Test]
        public async Task ValidateSignOffReadiness_CompleteDeployment_ActionableGuidanceIsNull()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.ActionableGuidance, Is.Null,
                "Approved sign-off should not include actionable guidance");
        }

        [Test]
        public async Task ValidateSignOffReadiness_CompleteDeployment_DeploymentEvidencePresent()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.AssetId, Is.EqualTo(TestAssetId));
            Assert.That(result.TransactionId, Is.EqualTo(TestTransactionId));
            Assert.That(result.ConfirmedRound, Is.EqualTo(TestConfirmedRound));
            Assert.That(result.DeployerAddress, Is.EqualTo(TestAlgorandAddress));
        }

        [Test]
        public async Task ValidateSignOffReadiness_CompleteDeployment_AuditTrailPopulated()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.HasAuditTrail, Is.True);
            Assert.That(result.AuditEventCount, Is.EqualTo(3));
        }

        [Test]
        public async Task ValidateSignOffReadiness_CompleteDeployment_ProofIdIsGenerated()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.ProofId, Is.Not.Null.And.Not.Empty,
                "Proof document must have a unique ID");
            Assert.That(result.ProofId, Does.StartWith("proof-"),
                "Proof ID must follow the expected format");
        }

        [Test]
        public async Task ValidateSignOffReadiness_CompleteDeployment_GeneratedAtIsPresent()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.GeneratedAt, Is.Not.Null.And.Not.Empty);
            Assert.That(DateTime.TryParse(result.GeneratedAt, out _), Is.True,
                "GeneratedAt must be a valid ISO 8601 timestamp");
        }

        [Test]
        public async Task ValidateSignOffReadiness_CompleteDeployment_IsDeterministicAddressPropagated()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.IsDeterministicAddress, Is.True,
                "ARC76 deterministic address flag must be propagated to the proof");
        }

        // ── AC2: Missing AssetId → explicit failure ───────────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_MissingAssetId_IsNotReadyForSignOff()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(assetId: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireAssetId = true });

            Assert.That(result.IsReadyForSignOff, Is.False,
                "Missing AssetId must block sign-off");
        }

        [Test]
        public async Task ValidateSignOffReadiness_MissingAssetId_VerdictIsBlocked()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(assetId: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireAssetId = true });

            Assert.That(result.Verdict, Is.EqualTo(SignOffVerdict.Blocked));
        }

        [Test]
        public async Task ValidateSignOffReadiness_MissingAssetId_CriterionHasExplicitFailure()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(assetId: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireAssetId = true });

            var assetIdCriterion = result.Criteria.FirstOrDefault(c => c.Name == "AssetId");
            Assert.That(assetIdCriterion, Is.Not.Null, "AssetId criterion must be present");
            Assert.That(assetIdCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail));
        }

        [Test]
        public async Task ValidateSignOffReadiness_MissingAssetId_CriterionHasUserGuidance()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(assetId: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireAssetId = true });

            var assetIdCriterion = result.Criteria.FirstOrDefault(c => c.Name == "AssetId");
            Assert.That(assetIdCriterion!.UserGuidance, Is.Not.Null.And.Not.Empty,
                "Missing AssetId failure must include actionable user guidance");
        }

        [Test]
        public async Task ValidateSignOffReadiness_ZeroAssetId_TreatedAsMissing()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(assetId: 0));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireAssetId = true });

            var assetIdCriterion = result.Criteria.FirstOrDefault(c => c.Name == "AssetId");
            Assert.That(assetIdCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail),
                "Asset ID of zero must be treated as missing (not a valid on-chain ID)");
        }

        [Test]
        public async Task ValidateSignOffReadiness_AssetIdNotRequired_NotEvaluated()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(assetId: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireAssetId = false });

            var assetIdCriterion = result.Criteria.FirstOrDefault(c => c.Name == "AssetId");
            Assert.That(assetIdCriterion, Is.Null,
                "AssetId criterion must not be evaluated when not required");
        }

        // ── AC2: Missing TransactionId → explicit failure ─────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_MissingTransactionId_IsNotReadyForSignOff()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(transactionId: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireTransactionId = true });

            Assert.That(result.IsReadyForSignOff, Is.False);
        }

        [Test]
        public async Task ValidateSignOffReadiness_MissingTransactionId_CriterionFails()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(transactionId: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireTransactionId = true });

            var txCriterion = result.Criteria.FirstOrDefault(c => c.Name == "TransactionId");
            Assert.That(txCriterion, Is.Not.Null);
            Assert.That(txCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail));
        }

        [Test]
        public async Task ValidateSignOffReadiness_MissingTransactionId_UserGuidancePresent()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(transactionId: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireTransactionId = true });

            var txCriterion = result.Criteria.FirstOrDefault(c => c.Name == "TransactionId");
            Assert.That(txCriterion!.UserGuidance, Is.Not.Null.And.Not.Empty,
                "Missing TransactionId failure must include user guidance");
        }

        [Test]
        public async Task ValidateSignOffReadiness_EmptyTransactionId_TreatedAsMissing()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(transactionId: string.Empty));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireTransactionId = true });

            var txCriterion = result.Criteria.FirstOrDefault(c => c.Name == "TransactionId");
            Assert.That(txCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail));
        }

        // ── AC2: Missing ConfirmedRound → explicit failure ────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_MissingConfirmedRound_IsNotReadyForSignOff()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(confirmedRound: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireConfirmedRound = true });

            Assert.That(result.IsReadyForSignOff, Is.False);
        }

        [Test]
        public async Task ValidateSignOffReadiness_MissingConfirmedRound_CriterionFails()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(confirmedRound: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireConfirmedRound = true });

            var roundCriterion = result.Criteria.FirstOrDefault(c => c.Name == "ConfirmedRound");
            Assert.That(roundCriterion, Is.Not.Null);
            Assert.That(roundCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail));
        }

        [Test]
        public async Task ValidateSignOffReadiness_ZeroConfirmedRound_TreatedAsMissing()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(confirmedRound: 0));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId, RequireConfirmedRound = true });

            var roundCriterion = result.Criteria.FirstOrDefault(c => c.Name == "ConfirmedRound");
            Assert.That(roundCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail),
                "ConfirmedRound of zero must be treated as missing (block 0 is not a valid confirmation)");
        }

        // ── AC2: Missing DeployerAddress → explicit failure ───────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_MissingDeployerAddress_IsNotReadyForSignOff()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(deployerAddress: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.IsReadyForSignOff, Is.False,
                "Missing deployer address must block sign-off");
        }

        [Test]
        public async Task ValidateSignOffReadiness_MissingDeployerAddress_CriterionFails()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(deployerAddress: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            var addrCriterion = result.Criteria.FirstOrDefault(c => c.Name == "DeployerAddress");
            Assert.That(addrCriterion, Is.Not.Null);
            Assert.That(addrCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail));
        }

        [Test]
        public async Task ValidateSignOffReadiness_MissingDeployerAddress_UserGuidancePresent()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(deployerAddress: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            var addrCriterion = result.Criteria.FirstOrDefault(c => c.Name == "DeployerAddress");
            Assert.That(addrCriterion!.UserGuidance, Is.Not.Null.And.Not.Empty,
                "Missing deployer address failure must include user guidance");
        }

        // ── AC2: Non-terminal state → sign-off not ready ─────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_PendingState_VerdictIsInProgress()
        {
            SetupSuccessfulDeployment(
                status: BuildCompletedResponse(state: ContractLifecycleState.Pending),
                audit: BuildAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.Verdict, Is.EqualTo(SignOffVerdict.InProgress));
            Assert.That(result.IsReadyForSignOff, Is.False);
        }

        [Test]
        public async Task ValidateSignOffReadiness_SubmittingState_VerdictIsInProgress()
        {
            SetupSuccessfulDeployment(
                status: BuildCompletedResponse(state: ContractLifecycleState.Submitted),
                audit: BuildAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.Verdict, Is.EqualTo(SignOffVerdict.InProgress));
        }

        [Test]
        public async Task ValidateSignOffReadiness_ConfirmingState_VerdictIsInProgress()
        {
            SetupSuccessfulDeployment(
                status: BuildCompletedResponse(state: ContractLifecycleState.Confirmed),
                audit: BuildAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.Verdict, Is.EqualTo(SignOffVerdict.InProgress));
        }

        [Test]
        public async Task ValidateSignOffReadiness_InProgressState_ProvideGuidanceToWait()
        {
            SetupSuccessfulDeployment(
                status: BuildCompletedResponse(state: ContractLifecycleState.Pending),
                audit: BuildAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.ActionableGuidance, Is.Not.Null.And.Not.Empty,
                "In-progress deployments should include guidance to wait");
            Assert.That(
                result.ActionableGuidance!.ToLowerInvariant().Contains("progress") ||
                result.ActionableGuidance.ToLowerInvariant().Contains("wait") ||
                result.ActionableGuidance.Contains("Sign-off"),
                Is.True,
                "In-progress guidance should mention waiting or progress");
        }

        // ── AC2: Failed state → terminal failure verdict ──────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_FailedState_VerdictIsTerminalFailure()
        {
            SetupSuccessfulDeployment(
                status: BuildCompletedResponse(state: ContractLifecycleState.Failed));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.Verdict, Is.EqualTo(SignOffVerdict.TerminalFailure));
            Assert.That(result.IsReadyForSignOff, Is.False);
        }

        [Test]
        public async Task ValidateSignOffReadiness_FailedState_TerminalFailureCriterionFails()
        {
            SetupSuccessfulDeployment(
                status: BuildCompletedResponse(state: ContractLifecycleState.Failed));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            var stateCriterion = result.Criteria.FirstOrDefault(c => c.Name == "LifecycleTerminalState");
            Assert.That(stateCriterion, Is.Not.Null);
            Assert.That(stateCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail));
        }

        [Test]
        public async Task ValidateSignOffReadiness_FailedState_ActionableGuidancePresent()
        {
            SetupSuccessfulDeployment(
                status: BuildCompletedResponse(state: ContractLifecycleState.Failed));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.ActionableGuidance, Is.Not.Null.And.Not.Empty);
        }

        // ── AC2: Cancelled state ──────────────────────────────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_CancelledState_IsNotReadyForSignOff()
        {
            SetupSuccessfulDeployment(
                status: BuildCompletedResponse(state: ContractLifecycleState.Cancelled));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.IsReadyForSignOff, Is.False);
        }

        [Test]
        public async Task ValidateSignOffReadiness_CancelledState_StateCriterionFails()
        {
            SetupSuccessfulDeployment(
                status: BuildCompletedResponse(state: ContractLifecycleState.Cancelled));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            var stateCriterion = result.Criteria.FirstOrDefault(c => c.Name == "LifecycleTerminalState");
            Assert.That(stateCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail));
        }

        // ── AC2: Deployment not found ─────────────────────────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_DeploymentNotFound_IsNotReadyForSignOff()
        {
            _contractServiceMock
                .Setup(s => s.GetStatusAsync(TestDeploymentId, It.IsAny<string?>()))
                .ReturnsAsync(BuildNotFoundResponse(TestDeploymentId));
            _contractServiceMock
                .Setup(s => s.GetAuditTrailAsync(TestDeploymentId, It.IsAny<string?>()))
                .ReturnsAsync(BuildEmptyAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.IsReadyForSignOff, Is.False);
        }

        [Test]
        public async Task ValidateSignOffReadiness_DeploymentNotFound_VerdictIsBlocked()
        {
            _contractServiceMock
                .Setup(s => s.GetStatusAsync(TestDeploymentId, It.IsAny<string?>()))
                .ReturnsAsync(BuildNotFoundResponse(TestDeploymentId));
            _contractServiceMock
                .Setup(s => s.GetAuditTrailAsync(TestDeploymentId, It.IsAny<string?>()))
                .ReturnsAsync(BuildEmptyAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.Verdict, Is.EqualTo(SignOffVerdict.Blocked));
        }

        [Test]
        public async Task ValidateSignOffReadiness_DeploymentNotFound_CriterionHasUserGuidance()
        {
            _contractServiceMock
                .Setup(s => s.GetStatusAsync(TestDeploymentId, It.IsAny<string?>()))
                .ReturnsAsync(BuildNotFoundResponse(TestDeploymentId));
            _contractServiceMock
                .Setup(s => s.GetAuditTrailAsync(TestDeploymentId, It.IsAny<string?>()))
                .ReturnsAsync(BuildEmptyAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            var firstFailedCriterion = result.Criteria.FirstOrDefault(c => c.Outcome == SignOffCriterionOutcome.Fail);
            Assert.That(firstFailedCriterion?.UserGuidance, Is.Not.Null.And.Not.Empty,
                "Not-found failure must include user guidance");
        }

        // ── AC2: Null/missing DeploymentId ────────────────────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_NullRequest_IsNotReadyForSignOff()
        {
            var result = await _service.ValidateSignOffReadinessAsync(null!);

            Assert.That(result.IsReadyForSignOff, Is.False);
        }

        [Test]
        public async Task ValidateSignOffReadiness_NullRequest_VerdictIsBlocked()
        {
            var result = await _service.ValidateSignOffReadinessAsync(null!);

            Assert.That(result.Verdict, Is.EqualTo(SignOffVerdict.Blocked));
        }

        [Test]
        public async Task ValidateSignOffReadiness_EmptyDeploymentId_IsNotReadyForSignOff()
        {
            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = string.Empty });

            Assert.That(result.IsReadyForSignOff, Is.False);
        }

        [Test]
        public async Task ValidateSignOffReadiness_EmptyDeploymentId_DeploymentIdCriterionFails()
        {
            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = string.Empty });

            var deploymentIdCriterion = result.Criteria.FirstOrDefault(c => c.Name == "DeploymentId");
            Assert.That(deploymentIdCriterion, Is.Not.Null);
            Assert.That(deploymentIdCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail));
        }

        // ── AC2: Degraded mode deployment ─────────────────────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_DegradedMode_IsNotReadyForSignOff()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(isDegraded: true));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.IsReadyForSignOff, Is.False,
                "Degraded-mode deployment must not pass sign-off");
        }

        [Test]
        public async Task ValidateSignOffReadiness_DegradedMode_DegradedCriterionFails()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(isDegraded: true));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            var degradedCriterion = result.Criteria.FirstOrDefault(c => c.Name == "NoDegradedMode");
            Assert.That(degradedCriterion, Is.Not.Null);
            Assert.That(degradedCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail));
        }

        [Test]
        public async Task ValidateSignOffReadiness_DegradedMode_UserGuidancePresent()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(isDegraded: true));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            var degradedCriterion = result.Criteria.FirstOrDefault(c => c.Name == "NoDegradedMode");
            Assert.That(degradedCriterion!.UserGuidance, Is.Not.Null.And.Not.Empty);
        }

        // ── AC3: Audit trail criterion ────────────────────────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_EmptyAuditTrail_AuditCriterionFails()
        {
            SetupSuccessfulDeployment(audit: BuildEmptyAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest
                {
                    DeploymentId = TestDeploymentId,
                    RequireAuditTrail = true
                });

            var auditCriterion = result.Criteria.FirstOrDefault(c => c.Name == "AuditTrail");
            Assert.That(auditCriterion, Is.Not.Null);
            Assert.That(auditCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Fail));
        }

        [Test]
        public async Task ValidateSignOffReadiness_EmptyAuditTrail_IsNotReadyForSignOff()
        {
            SetupSuccessfulDeployment(audit: BuildEmptyAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest
                {
                    DeploymentId = TestDeploymentId,
                    RequireAuditTrail = true
                });

            Assert.That(result.IsReadyForSignOff, Is.False,
                "Missing audit trail must block sign-off");
        }

        [Test]
        public async Task ValidateSignOffReadiness_AuditTrailNotRequired_NotEvaluated()
        {
            SetupSuccessfulDeployment(audit: BuildEmptyAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest
                {
                    DeploymentId = TestDeploymentId,
                    RequireAuditTrail = false
                });

            var auditCriterion = result.Criteria.FirstOrDefault(c => c.Name == "AuditTrail");
            Assert.That(auditCriterion, Is.Null,
                "AuditTrail criterion must not be evaluated when not required");
        }

        [Test]
        public async Task ValidateSignOffReadiness_NonEmptyAuditTrail_AuditCriterionPasses()
        {
            SetupSuccessfulDeployment(audit: BuildAuditTrail(5));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest
                {
                    DeploymentId = TestDeploymentId,
                    RequireAuditTrail = true
                });

            var auditCriterion = result.Criteria.FirstOrDefault(c => c.Name == "AuditTrail");
            Assert.That(auditCriterion!.Outcome, Is.EqualTo(SignOffCriterionOutcome.Pass));
            Assert.That(result.AuditEventCount, Is.EqualTo(5));
        }

        // ── AC4: Structured error payloads and actionable guidance ────────────────

        [Test]
        public async Task ValidateSignOffReadiness_MultipleFailures_ActionableGuidanceListsAll()
        {
            // Missing TransactionId + ConfirmedRound → two failures
            SetupSuccessfulDeployment(status: BuildCompletedResponse(
                transactionId: null, confirmedRound: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest
                {
                    DeploymentId       = TestDeploymentId,
                    RequireTransactionId = true,
                    RequireConfirmedRound = true
                });

            Assert.That(result.ActionableGuidance, Is.Not.Null.And.Not.Empty);
            Assert.That(result.FailedCriteriaCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task ValidateSignOffReadiness_AllCriteria_EachHasDescription()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            foreach (var criterion in result.Criteria)
            {
                Assert.That(criterion.Description, Is.Not.Null.And.Not.Empty,
                    $"Criterion '{criterion.Name}' must have a description");
            }
        }

        [Test]
        public async Task ValidateSignOffReadiness_FailedCriteria_HaveCategories()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(assetId: null, transactionId: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest
                {
                    DeploymentId = TestDeploymentId,
                    RequireAssetId = true,
                    RequireTransactionId = true
                });

            var failedCriteria = result.Criteria.Where(c => c.Outcome == SignOffCriterionOutcome.Fail).ToList();
            foreach (var criterion in failedCriteria)
            {
                Assert.That(Enum.IsDefined(typeof(SignOffCriterionCategory), criterion.Category),
                    $"Failed criterion '{criterion.Name}' must have a valid defined category");
            }
        }

        [Test]
        public async Task ValidateSignOffReadiness_FailedCriteria_HaveDetails()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(assetId: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest
                {
                    DeploymentId = TestDeploymentId,
                    RequireAssetId = true
                });

            var failedCriteria = result.Criteria.Where(c => c.Outcome == SignOffCriterionOutcome.Fail).ToList();
            foreach (var criterion in failedCriteria)
            {
                Assert.That(criterion.Detail, Is.Not.Null.And.Not.Empty,
                    $"Failed criterion '{criterion.Name}' must have a detail explanation");
            }
        }

        // ── AC1: Schema contract – required fields always present ─────────────────

        [Test]
        public async Task ValidateSignOffReadiness_AlwaysReturnsProofId()
        {
            // Test both success and failure paths return ProofId
            SetupSuccessfulDeployment();
            var successResult = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            var failResult = await _service.ValidateSignOffReadinessAsync(null!);

            Assert.That(successResult.ProofId, Is.Not.Null.And.Not.Empty);
            Assert.That(failResult.ProofId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ValidateSignOffReadiness_AlwaysReturnsCorrelationId()
        {
            SetupSuccessfulDeployment();
            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest
                {
                    DeploymentId = TestDeploymentId,
                    CorrelationId = "test-correlation-999"
                });

            Assert.That(result.CorrelationId, Is.EqualTo("test-correlation-999"),
                "CorrelationId must be echoed back for distributed tracing");
        }

        [Test]
        public async Task ValidateSignOffReadiness_AlwaysReturnsGeneratedAt()
        {
            SetupSuccessfulDeployment();
            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.GeneratedAt, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task ValidateSignOffReadiness_AlwaysReturnsTotalCriteriaCount()
        {
            SetupSuccessfulDeployment();
            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.TotalCriteriaCount, Is.GreaterThan(0),
                "TotalCriteriaCount must be positive");
            Assert.That(result.TotalCriteriaCount,
                Is.EqualTo(result.PassedCriteriaCount + result.FailedCriteriaCount),
                "TotalCriteriaCount must equal Passed + Failed (required criteria only)");
        }

        // ── AC5: Email/password auth alignment (ARC76 determinism) ───────────────

        [Test]
        public async Task ValidateSignOffReadiness_DeterministicAddressTrue_PropagatedToProof()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(isDeterministic: true));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.IsDeterministicAddress, Is.True,
                "ARC76 email/password derived address flag must be propagated to sign-off proof");
        }

        [Test]
        public async Task ValidateSignOffReadiness_DeterministicAddressFalse_PropagatedToProof()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(isDeterministic: false));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.IsDeterministicAddress, Is.False,
                "Explicit address (non-ARC76) must be reflected as non-deterministic in proof");
        }

        // ── AC7: Observability – audit trail ──────────────────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_AuditTrailPresent_EventCountAccurate()
        {
            SetupSuccessfulDeployment(audit: BuildAuditTrail(7));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.AuditEventCount, Is.EqualTo(7),
                "AuditEventCount must accurately reflect the number of events in the audit trail");
        }

        [Test]
        public async Task ValidateSignOffReadiness_DeploymentTimestampsPresent()
        {
            var status = BuildCompletedResponse();
            SetupSuccessfulDeployment(status: status);

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.DeploymentInitiatedAt, Is.Not.Null.And.Not.Empty,
                "Deployment initiated-at timestamp must be included in sign-off proof");
            Assert.That(result.DeploymentLastUpdatedAt, Is.Not.Null.And.Not.Empty,
                "Deployment last-updated-at timestamp must be included in sign-off proof");
        }

        // ── GenerateSignOffProofAsync ─────────────────────────────────────────────

        [Test]
        public async Task GenerateSignOffProof_CompleteDeployment_IsReadyForSignOff()
        {
            SetupSuccessfulDeployment();

            var result = await _service.GenerateSignOffProofAsync(TestDeploymentId);

            Assert.That(result.IsReadyForSignOff, Is.True,
                "GenerateSignOffProofAsync must use default (all required) criteria");
        }

        [Test]
        public async Task GenerateSignOffProof_MissingAssetId_IsNotReadyForSignOff()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(assetId: null));

            var result = await _service.GenerateSignOffProofAsync(TestDeploymentId);

            Assert.That(result.IsReadyForSignOff, Is.False,
                "GenerateSignOffProofAsync must treat missing AssetId as a failure with default criteria");
        }

        [Test]
        public async Task GenerateSignOffProof_DeploymentId_PropagatedToProof()
        {
            SetupSuccessfulDeployment();

            var result = await _service.GenerateSignOffProofAsync(TestDeploymentId);

            Assert.That(result.DeploymentId, Is.EqualTo(TestDeploymentId));
        }

        // ── Regression: Criteria ordering and uniqueness ──────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_CriteriaNames_AreUnique()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            var names = result.Criteria.Select(c => c.Name).ToList();
            var uniqueNames = names.Distinct().ToList();
            Assert.That(names.Count, Is.EqualTo(uniqueNames.Count),
                "Each criterion name must appear exactly once in the results");
        }

        [Test]
        public async Task ValidateSignOffReadiness_AllCriteria_AreRequired()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            foreach (var criterion in result.Criteria)
            {
                Assert.That(criterion.IsRequired, Is.True,
                    $"Criterion '{criterion.Name}' must be marked as required in the default configuration");
            }
        }

        [Test]
        public async Task ValidateSignOffReadiness_PassedCriteriaCount_MatchesPassedCriteria()
        {
            SetupSuccessfulDeployment();

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            var actualPassed = result.Criteria.Count(c => c.IsRequired && c.Outcome == SignOffCriterionOutcome.Pass);
            Assert.That(result.PassedCriteriaCount, Is.EqualTo(actualPassed));
        }

        [Test]
        public async Task ValidateSignOffReadiness_FailedCriteriaCount_MatchesFailedCriteria()
        {
            SetupSuccessfulDeployment(status: BuildCompletedResponse(assetId: null, transactionId: null));

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest
                {
                    DeploymentId = TestDeploymentId,
                    RequireAssetId = true,
                    RequireTransactionId = true
                });

            var actualFailed = result.Criteria.Count(c => c.IsRequired && c.Outcome == SignOffCriterionOutcome.Fail);
            Assert.That(result.FailedCriteriaCount, Is.EqualTo(actualFailed));
        }

        // ── Network/token standard fields in proof ────────────────────────────────

        [Test]
        public async Task ValidateSignOffReadiness_TokenStandard_PropagatedFromAuditTrail()
        {
            SetupSuccessfulDeployment(audit: BuildAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.TokenStandard, Is.EqualTo("ASA"),
                "TokenStandard from audit trail must be propagated to the sign-off proof");
        }

        [Test]
        public async Task ValidateSignOffReadiness_Network_PropagatedFromAuditTrail()
        {
            SetupSuccessfulDeployment(audit: BuildAuditTrail());

            var result = await _service.ValidateSignOffReadinessAsync(
                new DeploymentSignOffValidationRequest { DeploymentId = TestDeploymentId });

            Assert.That(result.Network, Is.EqualTo("algorand-testnet"),
                "Network from audit trail must be propagated to the sign-off proof");
        }
    }
}
