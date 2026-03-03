using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Service-layer unit tests for Issue #472: Vision Milestone – Deterministic Backend
    /// Deployment Lifecycle and ARC76 Contract Hardening.
    ///
    /// These are pure unit tests (no HTTP / WebApplicationFactory) that directly exercise
    /// BackendDeploymentLifecycleContractService business logic.
    ///
    /// AC1 – Same valid credentials deterministically derive the same ARC76 account.
    /// AC2 – Invalid/expired credentials return explicit typed errors with stable codes.
    /// AC3 – Replayed requests with identical idempotency keys do not create duplicates.
    /// AC4 – Lifecycle state transitions are validated; illegal transitions are blocked.
    /// AC5 – Status endpoints expose consistent, monotonic lifecycle information.
    /// AC6 – Structured logs include correlation IDs across request and orchestration stages.
    /// AC7 – Audit records capture minimum compliance evidence fields.
    /// AC8 – Unit and integration tests cover derivation, idempotency, state machine, taxonomy.
    /// AC9 – CI passes; no regression in existing critical tests.
    /// AC10 – API contract changes documented and linked to frontend implications.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendDeploymentLifecycleIssue472ServiceUnitTests
    {
        private BackendDeploymentLifecycleContractService _service = null!;
        private Mock<ILogger<BackendDeploymentLifecycleContractService>> _loggerMock = null!;

        // Known ARC76 test vector
        private const string KnownEmail = "testuser@biatec.io";
        private const string KnownPassword = "TestPassword123!";
        private const string KnownAlgorandAddress = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        private const string AlgorandAddress =
            "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";
        private const string EvmAddress =
            "0x0000000000000000000000000000000000000001";

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<BackendDeploymentLifecycleContractService>>();
            _service = new BackendDeploymentLifecycleContractService(_loggerMock.Object);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC1: ARC76 determinism – same credentials always derive the same address
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void DeriveARC76Address_SameCredentials_ReturnsSameAddress()
        {
            var addr1 = _service.DeriveARC76Address(KnownEmail, KnownPassword);
            var addr2 = _service.DeriveARC76Address(KnownEmail, KnownPassword);
            Assert.That(addr1, Is.EqualTo(addr2));
        }

        [Test]
        public void DeriveARC76Address_KnownVector_MatchesExpected()
        {
            // Verify the well-known test vector documented in Arc76CredentialDerivationService.
            var addr = _service.DeriveARC76Address(KnownEmail, KnownPassword);
            Assert.That(addr, Is.EqualTo(KnownAlgorandAddress));
        }

        [Test]
        public void DeriveARC76Address_EmailCaseInsensitive_SameAddress()
        {
            // Email canonicalization: "User@Biatec.IO" should produce same address as "user@biatec.io"
            var addr1 = _service.DeriveARC76Address("User@Biatec.IO", KnownPassword);
            var addr2 = _service.DeriveARC76Address("user@biatec.io", KnownPassword);
            Assert.That(addr1, Is.EqualTo(addr2));
        }

        [Test]
        public void DeriveARC76Address_EmailWithLeadingTrailingSpaces_SameAddress()
        {
            var addr1 = _service.DeriveARC76Address("  " + KnownEmail + "  ", KnownPassword);
            var addr2 = _service.DeriveARC76Address(KnownEmail, KnownPassword);
            Assert.That(addr1, Is.EqualTo(addr2));
        }

        [Test]
        public void DeriveARC76Address_DifferentPasswords_DifferentAddresses()
        {
            var addr1 = _service.DeriveARC76Address(KnownEmail, "Password1!");
            var addr2 = _service.DeriveARC76Address(KnownEmail, "Password2!");
            Assert.That(addr1, Is.Not.EqualTo(addr2));
        }

        [Test]
        public void DeriveARC76Address_NullEmail_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _service.DeriveARC76Address(null!, KnownPassword));
        }

        [Test]
        public void DeriveARC76Address_EmptyEmail_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _service.DeriveARC76Address("", KnownPassword));
        }

        [Test]
        public void DeriveARC76Address_NullPassword_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _service.DeriveARC76Address(KnownEmail, null!));
        }

        [Test]
        public void DeriveARC76Address_EmptyPassword_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _service.DeriveARC76Address(KnownEmail, ""));
        }

        [Test]
        public void DeriveARC76Address_ReturnsAlgorandAddressFormat()
        {
            // Algorand address: 58 uppercase base32 chars
            var addr = _service.DeriveARC76Address(KnownEmail, KnownPassword);
            Assert.That(addr, Has.Length.EqualTo(58));
            Assert.That(addr, Does.Match(@"^[A-Z2-7]{58}$"));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC2: Invalid credentials return explicit typed errors with stable codes
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Initiate_NullRequest_ReturnsRequiredFieldMissingError()
        {
            var result = await _service.InitiateAsync(null!);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Failed));
        }

        [Test]
        public async Task Initiate_NoCredentialsOrAddress_ReturnsRequiredFieldMissingError()
        {
            var request = BuildAlgorandRequest("ASA");
            request.DeployerEmail = null;
            request.DeployerPassword = null;
            request.ExplicitDeployerAddress = null;

            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        [Test]
        public async Task Initiate_EmptyTokenStandard_ReturnsRequiredFieldMissingError()
        {
            var request = BuildAlgorandRequest("ASA");
            request.TokenStandard = "";

            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Failed));
        }

        [Test]
        public async Task Initiate_UnsupportedStandard_ReturnsUnsupportedStandardError()
        {
            var request = BuildAlgorandRequest("UNKNOWN");
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.UnsupportedStandard));
        }

        [Test]
        public async Task Initiate_UnsupportedNetwork_ReturnsNetworkUnavailableError()
        {
            var request = BuildAlgorandRequest("ASA");
            request.Network = "unsupported-net";
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.NetworkUnavailable));
        }

        [Test]
        public async Task Initiate_ZeroTotalSupply_ReturnsValidationRangeFaultError()
        {
            var request = BuildAlgorandRequest("ASA");
            request.TotalSupply = 0;
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
        }

        [Test]
        public async Task Initiate_DecimalsOutOfRange_ReturnsValidationRangeFaultError()
        {
            var request = BuildAlgorandRequest("ASA");
            request.Decimals = 20;
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
        }

        [Test]
        public async Task Initiate_TokenNameTooLong_ReturnsValidationRangeFaultError()
        {
            var request = BuildAlgorandRequest("ASA");
            request.TokenName = new string('A', 65);
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
        }

        [Test]
        public async Task Initiate_TokenSymbolTooLong_ReturnsValidationRangeFaultError()
        {
            var request = BuildAlgorandRequest("ASA");
            request.TokenSymbol = "TOOLONGSY";
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
        }

        [Test]
        public async Task Initiate_EvmAddressForAlgorandNetwork_ReturnsDeriveAddressMismatch()
        {
            var request = BuildAlgorandRequest("ASA");
            request.ExplicitDeployerAddress = EvmAddress; // EVM addr on Algorand network
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.DeriveAddressMismatch));
        }

        [Test]
        public async Task Initiate_ErrorCodeIsNone_WhenSuccessful()
        {
            var request = BuildAlgorandRequest("ASA");
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.None));
        }

        [Test]
        public async Task Initiate_UserGuidanceIsNull_WhenSuccessful()
        {
            var request = BuildAlgorandRequest("ASA");
            var result = await _service.InitiateAsync(request);
            Assert.That(result.UserGuidance, Is.Null);
        }

        [Test]
        public async Task Initiate_UserGuidancePresent_WhenError()
        {
            var request = BuildAlgorandRequest("ASA");
            request.TokenStandard = "";
            var result = await _service.InitiateAsync(request);
            Assert.That(result.UserGuidance, Is.Not.Null.And.Not.Empty);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC3: Idempotency – same key returns cached result without duplication
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Initiate_SameIdempotencyKey_ReturnsSameDeploymentId()
        {
            var request = BuildAlgorandRequest("ASA");
            request.IdempotencyKey = "idem-key-same-id";

            var r1 = await _service.InitiateAsync(request);
            var r2 = await _service.InitiateAsync(request);

            Assert.That(r2.DeploymentId, Is.EqualTo(r1.DeploymentId));
        }

        [Test]
        public async Task Initiate_SameIdempotencyKey_SecondResponseIsReplay()
        {
            var request = BuildAlgorandRequest("ASA");
            request.IdempotencyKey = "idem-key-replay";

            await _service.InitiateAsync(request);
            var r2 = await _service.InitiateAsync(request);

            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task Initiate_SameIdempotencyKey_SameAssetId()
        {
            var request = BuildAlgorandRequest("ASA");
            request.IdempotencyKey = "idem-key-asset";

            var r1 = await _service.InitiateAsync(request);
            var r2 = await _service.InitiateAsync(request);

            Assert.That(r2.AssetId, Is.EqualTo(r1.AssetId));
        }

        [Test]
        public async Task Initiate_SameIdempotencyKey_SameTransactionId()
        {
            var request = BuildAlgorandRequest("ASA");
            request.IdempotencyKey = "idem-key-txid";

            var r1 = await _service.InitiateAsync(request);
            var r2 = await _service.InitiateAsync(request);

            Assert.That(r2.TransactionId, Is.EqualTo(r1.TransactionId));
        }

        [Test]
        public async Task Initiate_DifferentIdempotencyKeys_ReturnsDifferentDeploymentIds()
        {
            var r1 = await _service.InitiateAsync(BuildAlgorandRequestWithKey("ASA", "key-001"));
            var r2 = await _service.InitiateAsync(BuildAlgorandRequestWithKey("ASA", "key-002"));
            Assert.That(r2.DeploymentId, Is.Not.EqualTo(r1.DeploymentId));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC4: State machine – valid/invalid transitions
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void IsValidStateTransition_PendingToValidated_IsValid()
        {
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Pending, ContractLifecycleState.Validated), Is.True);
        }

        [Test]
        public void IsValidStateTransition_ValidatedToSubmitted_IsValid()
        {
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Validated, ContractLifecycleState.Submitted), Is.True);
        }

        [Test]
        public void IsValidStateTransition_SubmittedToConfirmed_IsValid()
        {
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Submitted, ContractLifecycleState.Confirmed), Is.True);
        }

        [Test]
        public void IsValidStateTransition_ConfirmedToCompleted_IsValid()
        {
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Confirmed, ContractLifecycleState.Completed), Is.True);
        }

        [Test]
        public void IsValidStateTransition_AnyToFailed_IsValid()
        {
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Pending, ContractLifecycleState.Failed), Is.True);
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Validated, ContractLifecycleState.Failed), Is.True);
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Submitted, ContractLifecycleState.Failed), Is.True);
        }

        [Test]
        public void IsValidStateTransition_FailedToPending_IsValidForRetry()
        {
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Failed, ContractLifecycleState.Pending), Is.True);
        }

        [Test]
        public void IsValidStateTransition_CompletedToAny_IsInvalid()
        {
            // Completed is terminal
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Completed, ContractLifecycleState.Pending), Is.False);
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Completed, ContractLifecycleState.Validated), Is.False);
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Completed, ContractLifecycleState.Failed), Is.False);
        }

        [Test]
        public void IsValidStateTransition_CancelledToAny_IsInvalid()
        {
            // Cancelled is terminal
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Cancelled, ContractLifecycleState.Pending), Is.False);
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Cancelled, ContractLifecycleState.Validated), Is.False);
        }

        [Test]
        public void IsValidStateTransition_PendingToCompleted_IsInvalid()
        {
            // Cannot skip stages
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Pending, ContractLifecycleState.Completed), Is.False);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC5: Status endpoint – consistent lifecycle information
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetStatus_AfterInitiate_ReturnsCompletedState()
        {
            var request = BuildAlgorandRequest("ASA");
            var initiated = await _service.InitiateAsync(request);

            var status = await _service.GetStatusAsync(initiated.DeploymentId);
            Assert.That(status.State, Is.EqualTo(ContractLifecycleState.Completed));
        }

        [Test]
        public async Task GetStatus_AfterInitiate_SameDeploymentId()
        {
            var request = BuildAlgorandRequest("ASA");
            var initiated = await _service.InitiateAsync(request);

            var status = await _service.GetStatusAsync(initiated.DeploymentId);
            Assert.That(status.DeploymentId, Is.EqualTo(initiated.DeploymentId));
        }

        [Test]
        public async Task GetStatus_UnknownId_ReturnsErrorResponse()
        {
            var result = await _service.GetStatusAsync("dep-unknown-12345");
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
            Assert.That(result.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task GetStatus_EmptyId_ReturnsRequiredFieldMissing()
        {
            var result = await _service.GetStatusAsync(string.Empty);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC6 & AC7: Audit telemetry with correlation IDs and compliance evidence
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Initiate_ResponseHasCorrelationId()
        {
            var request = BuildAlgorandRequest("ASA");
            request.CorrelationId = "cid-test-001";
            var result = await _service.InitiateAsync(request);
            Assert.That(result.CorrelationId, Is.EqualTo("cid-test-001"));
        }

        [Test]
        public async Task Initiate_AutoGeneratesCorrelationId_WhenNotSupplied()
        {
            var request = BuildAlgorandRequest("ASA");
            request.CorrelationId = null;
            var result = await _service.InitiateAsync(request);
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_AuditEventsPresent()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.AuditEvents, Is.Not.Empty);
        }

        [Test]
        public async Task Initiate_AuditEventsHaveDeploymentInitiatedEvent()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.AuditEvents
                .Any(e => e.EventKind == ComplianceAuditEventKind.DeploymentInitiated), Is.True);
        }

        [Test]
        public async Task Initiate_AuditEventsHaveDeploymentCompletedEvent()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.AuditEvents
                .Any(e => e.EventKind == ComplianceAuditEventKind.DeploymentCompleted), Is.True);
        }

        [Test]
        public async Task Initiate_ARC76Derived_AuditEventsContainAccountDerivedEvent()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequestWithCredentials("ASA"));
            Assert.That(result.AuditEvents
                .Any(e => e.EventKind == ComplianceAuditEventKind.AccountDerived), Is.True);
        }

        [Test]
        public async Task Initiate_AuditEventsAllHaveCorrelationId()
        {
            var request = BuildAlgorandRequest("ASA");
            request.CorrelationId = "cid-audit-check";
            var result = await _service.InitiateAsync(request);
            Assert.That(result.AuditEvents.All(e => e.CorrelationId == "cid-audit-check"), Is.True);
        }

        [Test]
        public async Task Initiate_AuditEventsAllHaveDeploymentId()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.AuditEvents.All(e => e.DeploymentId == result.DeploymentId), Is.True);
        }

        [Test]
        public async Task Initiate_AuditEventsAllHaveTimestamp()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.AuditEvents.All(e => !string.IsNullOrEmpty(e.Timestamp)), Is.True);
        }

        [Test]
        public async Task GetAuditTrail_AfterInitiate_ContainsAllEvents()
        {
            var initiated = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            var trail = await _service.GetAuditTrailAsync(initiated.DeploymentId);
            Assert.That(trail.Events, Is.Not.Empty);
            Assert.That(trail.FinalState, Is.EqualTo(ContractLifecycleState.Completed));
        }

        [Test]
        public async Task GetAuditTrail_Unknown_ReturnsEmptyTrail()
        {
            var trail = await _service.GetAuditTrailAsync("dep-nonexistent-000");
            Assert.That(trail.Events, Is.Empty);
            Assert.That(trail.FinalState, Is.EqualTo(ContractLifecycleState.Failed));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC1 (cont): ARC76 derivation – response metadata
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Initiate_WithCredentials_IsDeterministicAddressTrue()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequestWithCredentials("ASA"));
            Assert.That(result.IsDeterministicAddress, Is.True);
        }

        [Test]
        public async Task Initiate_WithExplicitAddress_IsDeterministicAddressFalse()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.IsDeterministicAddress, Is.False);
        }

        [Test]
        public async Task Initiate_WithCredentials_DerivationStatusIsDerived()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequestWithCredentials("ASA"));
            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Derived));
        }

        [Test]
        public async Task Initiate_WithExplicitAddress_DerivationStatusIsAddressProvided()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.AddressProvided));
        }

        [Test]
        public async Task Initiate_WithCredentials_DeployerAddressMatchesKnownVector()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequestWithCredentials("ASA"));
            Assert.That(result.DeployerAddress, Is.EqualTo(KnownAlgorandAddress));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Additional completeness tests
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Initiate_Success_ReturnsNonEmptyDeploymentId()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.DeploymentId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_Success_ReturnsNonEmptyIdempotencyKey()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.IdempotencyKey, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_Success_ReturnsAssetId()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.AssetId, Is.Not.Null.And.GreaterThan(0UL));
        }

        [Test]
        public async Task Initiate_Success_ReturnsTransactionId()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.TransactionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_Success_ReturnsConfirmedRound()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA"));
            Assert.That(result.ConfirmedRound, Is.Not.Null.And.GreaterThan(0UL));
        }

        [Test]
        public async Task Initiate_Success_MessageContainsTokenName()
        {
            var request = BuildAlgorandRequest("ASA");
            request.TokenName = "UniqueTestToken";
            var result = await _service.InitiateAsync(request);
            Assert.That(result.Message, Does.Contain("UniqueTestToken"));
        }

        [Test]
        public async Task Validate_ValidRequest_ReturnsIsValidTrue()
        {
            var result = await _service.ValidateAsync(BuildValidationRequest());
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public async Task Validate_MissingTokenStandard_ReturnsIsValidFalse()
        {
            var req = BuildValidationRequest();
            req.TokenStandard = "";
            var result = await _service.ValidateAsync(req);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public async Task Validate_NullRequest_ReturnsIsValidFalse()
        {
            var result = await _service.ValidateAsync(null!);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FirstErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        [Test]
        public async Task Validate_WithCredentials_DerivationStatusIsDerived()
        {
            var req = BuildValidationRequest();
            req.DeployerEmail = KnownEmail;
            req.DeployerPassword = KnownPassword;
            req.ExplicitDeployerAddress = null;
            var result = await _service.ValidateAsync(req);
            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Derived));
        }

        [Test]
        public async Task Validate_ValidationResultsContainDeployerAddressField()
        {
            var result = await _service.ValidateAsync(BuildValidationRequest());
            Assert.That(result.ValidationResults.Any(r => r.Field == "DeployerAddress"), Is.True);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private BackendDeploymentContractRequest BuildAlgorandRequest(string standard) =>
            new()
            {
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = standard,
                TokenName = $"{standard}TestToken",
                TokenSymbol = standard[..Math.Min(standard.Length, 4)],
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            };

        private BackendDeploymentContractRequest BuildAlgorandRequestWithKey(string standard, string key) =>
            new()
            {
                IdempotencyKey = key,
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = standard,
                TokenName = $"{standard}Token{key}",
                TokenSymbol = standard[..Math.Min(standard.Length, 4)],
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            };

        private BackendDeploymentContractRequest BuildAlgorandRequestWithCredentials(string standard) =>
            new()
            {
                DeployerEmail = KnownEmail,
                DeployerPassword = KnownPassword,
                TokenStandard = standard,
                TokenName = $"{standard}CredsToken",
                TokenSymbol = standard[..Math.Min(standard.Length, 4)],
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            };

        private BackendDeploymentContractValidationRequest BuildValidationRequest() =>
            new()
            {
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "ValidateToken",
                TokenSymbol = "VLD",
                Network = "algorand-mainnet",
                TotalSupply = 500_000,
                Decimals = 2
            };
    }
}
