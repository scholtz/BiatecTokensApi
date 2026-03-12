using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for <see cref="BackendDeploymentLifecycleContractService"/>.
    ///
    /// These tests validate:
    /// - Deployment initiation with ARC76 credentials and explicit address
    /// - Idempotency: repeated requests return cached results
    /// - State machine: valid and invalid lifecycle transitions
    /// - Validation: required fields, supported standards, network constraints
    /// - ARC76 address derivation determinism
    /// - Audit trail availability after deployment
    /// - Error taxonomy for all failure cases
    /// </summary>
    [TestFixture]
    public class BackendDeploymentLifecycleContractServiceUnitTests
    {
        private Mock<ILogger<BackendDeploymentLifecycleContractService>> _loggerMock = null!;
        private BackendDeploymentLifecycleContractService _service = null!;

        private const string TestEmail = "unit-test@biatec-test.example.com";
        private const string TestPassword = "SecurePass123!";
        // Valid Algorand test address: 58 uppercase base32 characters
        private const string TestAlgorandAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        // Valid EVM test address: 0x followed by 40 hex characters
        private const string TestEvmAddress = "0x1234567890123456789012345678901234567890";

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger<BackendDeploymentLifecycleContractService>>();
            _service = new BackendDeploymentLifecycleContractService(_loggerMock.Object);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static BackendDeploymentContractRequest BuildARC76Request(
            string? email = null,
            string? idempotencyKey = null,
            string network = "algorand-testnet",
            string standard = "ASA")
        {
            return new BackendDeploymentContractRequest
            {
                DeployerEmail = email ?? TestEmail,
                DeployerPassword = TestPassword,
                TokenStandard = standard,
                TokenName = "Unit Test Token",
                TokenSymbol = "UTT",
                Network = network,
                TotalSupply = 1_000_000,
                Decimals = 6,
                IdempotencyKey = idempotencyKey,
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        private static BackendDeploymentContractRequest BuildExplicitAddressRequest(
            string address = TestAlgorandAddress,
            string? idempotencyKey = null)
        {
            return new BackendDeploymentContractRequest
            {
                ExplicitDeployerAddress = address,
                TokenStandard = "ASA",
                TokenName = "Explicit Address Token",
                TokenSymbol = "EAT",
                Network = "algorand-testnet",
                TotalSupply = 500_000,
                Decimals = 0,
                IdempotencyKey = idempotencyKey,
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        // ── InitiateAsync: ARC76 credential path ─────────────────────────────────

        [Test]
        public async Task InitiateAsync_ARC76Credentials_ReturnsSuccess()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.None),
                "ARC76 deployment must succeed with valid credentials");
        }

        [Test]
        public async Task InitiateAsync_ARC76Credentials_ReturnsDeploymentId()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.DeploymentId, Is.Not.Null.And.Not.Empty,
                "DeploymentId must be present for correlated status polling");
        }

        [Test]
        public async Task InitiateAsync_ARC76Credentials_ReturnsIdempotencyKey()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.IdempotencyKey, Is.Not.Null.And.Not.Empty,
                "IdempotencyKey must be reflected back in the response");
        }

        [Test]
        public async Task InitiateAsync_ARC76Credentials_IsDeterministicAddress()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.IsDeterministicAddress, Is.True,
                "ARC76 derivation must flag IsDeterministicAddress=true");
            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Derived));
        }

        [Test]
        public async Task InitiateAsync_ARC76Credentials_ReturnsDeployerAddress()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.DeployerAddress, Is.Not.Null.And.Not.Empty,
                "Deployer address must be returned for ARC76-derived deployments");
        }

        [Test]
        public async Task InitiateAsync_ARC76Credentials_ReturnsCompletedState()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed),
                "Deterministic deployment should reach Completed state synchronously");
        }

        [Test]
        public async Task InitiateAsync_ARC76Credentials_ReturnsAuditEvents()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.AuditEvents, Is.Not.Null.And.Not.Empty,
                "Audit events must be recorded for compliance tracing");
        }

        [Test]
        public async Task InitiateAsync_ARC76Credentials_ReturnsValidationResults()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ValidationResults, Is.Not.Null.And.Not.Empty,
                "Field-level validation results must be returned");
        }

        [Test]
        public async Task InitiateAsync_ARC76Credentials_ReturnsAssetId()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.AssetId, Is.GreaterThan(0),
                "AssetId must be returned for a completed deployment");
        }

        [Test]
        public async Task InitiateAsync_ARC76Credentials_ReturnsTransactionId()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.TransactionId, Is.Not.Null.And.Not.Empty,
                "TransactionId must be returned for a completed deployment");
        }

        [Test]
        public async Task InitiateAsync_ARC76Credentials_ReturnsInitiatedAt()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.InitiatedAt, Is.Not.Null.And.Not.Empty,
                "InitiatedAt timestamp must be returned");
        }

        [Test]
        public async Task InitiateAsync_ARC76Credentials_ReturnsCorrelationId()
        {
            var correlationId = Guid.NewGuid().ToString();
            var req = BuildARC76Request();
            req.CorrelationId = correlationId;

            var result = await _service.InitiateAsync(req);

            Assert.That(result.CorrelationId, Is.EqualTo(correlationId),
                "CorrelationId must be echoed back for distributed tracing");
        }

        [Test]
        public async Task InitiateAsync_ARC76SameCredentials_SameDeployerAddress()
        {
            var email = "determinism@biatec-test.example.com";
            var req1 = BuildARC76Request(email: email, idempotencyKey: Guid.NewGuid().ToString());
            var req2 = BuildARC76Request(email: email, idempotencyKey: Guid.NewGuid().ToString());

            var result1 = await _service.InitiateAsync(req1);
            var result2 = await _service.InitiateAsync(req2);

            Assert.That(result1.DeployerAddress, Is.EqualTo(result2.DeployerAddress),
                "Same ARC76 credentials must always derive the same address (determinism)");
        }

        // ── InitiateAsync: Explicit address path ─────────────────────────────────

        [Test]
        public async Task InitiateAsync_ExplicitAddress_ReturnsSuccess()
        {
            var req = BuildExplicitAddressRequest();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.None),
                "Explicit address deployment must succeed");
        }

        [Test]
        public async Task InitiateAsync_ExplicitAddress_DerivationStatusIsAddressProvided()
        {
            var req = BuildExplicitAddressRequest();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.AddressProvided));
            Assert.That(result.IsDeterministicAddress, Is.False,
                "Explicit address should not be flagged as deterministic");
        }

        [Test]
        public async Task InitiateAsync_ExplicitAddress_ReturnsDeployerAddress()
        {
            var req = BuildExplicitAddressRequest(TestAlgorandAddress);
            var result = await _service.InitiateAsync(req);

            Assert.That(result.DeployerAddress, Is.EqualTo(TestAlgorandAddress));
        }        // ── InitiateAsync: Null/empty request failures ────────────────────────────

        [Test]
        public async Task InitiateAsync_NullRequest_ReturnsFailed()
        {
            var result = await _service.InitiateAsync(null!);

            Assert.That(result.ErrorCode, Is.Not.EqualTo(DeploymentErrorCode.None),
                "Null request must return an error code");
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Failed));
        }

        [Test]
        public async Task InitiateAsync_NoCredentialsNoAddress_ReturnsFailed()
        {
            var req = new BackendDeploymentContractRequest
            {
                TokenStandard = "ASA",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-testnet",
                TotalSupply = 100
            };
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.Not.EqualTo(DeploymentErrorCode.None),
                "Request without deployer credentials or address must fail");
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Failed));
        }

        [Test]
        public async Task InitiateAsync_MissingTokenStandard_ReturnsFailed()
        {
            var req = BuildARC76Request();
            req.TokenStandard = "";
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing),
                "Missing TokenStandard must return RequiredFieldMissing error");
        }

        [Test]
        public async Task InitiateAsync_UnsupportedStandard_ReturnsFailed()
        {
            var req = BuildARC76Request(standard: "UNSUPPORTED_STANDARD");
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.UnsupportedStandard),
                "Unsupported token standard must return UnsupportedStandard error");
        }

        [Test]
        public async Task InitiateAsync_MissingTokenName_ReturnsFailed()
        {
            var req = BuildARC76Request();
            req.TokenName = "";
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        [Test]
        public async Task InitiateAsync_TokenNameTooLong_ReturnsFailed()
        {
            var req = BuildARC76Request();
            req.TokenName = new string('A', 65);
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault),
                "Token name exceeding 64 chars must fail with ValidationRangeFault");
        }

        [Test]
        public async Task InitiateAsync_MissingTokenSymbol_ReturnsFailed()
        {
            var req = BuildARC76Request();
            req.TokenSymbol = "";
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        [Test]
        public async Task InitiateAsync_TokenSymbolTooLong_ReturnsFailed()
        {
            var req = BuildARC76Request();
            req.TokenSymbol = "TOOLONGSY";
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault),
                "Token symbol exceeding 8 chars must fail with ValidationRangeFault");
        }

        [Test]
        public async Task InitiateAsync_MissingNetwork_ReturnsFailed()
        {
            var req = BuildARC76Request();
            req.Network = "";
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        [Test]
        public async Task InitiateAsync_ZeroSupply_ReturnsFailed()
        {
            var req = BuildARC76Request();
            req.TotalSupply = 0;
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault),
                "Zero total supply must fail with ValidationRangeFault");
        }

        [Test]
        public async Task InitiateAsync_InvalidDecimalsNegative_ReturnsFailed()
        {
            var req = BuildARC76Request();
            req.Decimals = -1;
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault),
                "Negative decimals must fail with ValidationRangeFault");
        }

        [Test]
        public async Task InitiateAsync_DecimalsTooHigh_ReturnsFailed()
        {
            var req = BuildARC76Request();
            req.Decimals = 20;
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault),
                "Decimals > 19 must fail with ValidationRangeFault");
        }

        // ── InitiateAsync: Idempotency ────────────────────────────────────────────

        [Test]
        public async Task InitiateAsync_SameIdempotencyKey_ReturnsIdempotentReplay()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildARC76Request(idempotencyKey: key);

            var first = await _service.InitiateAsync(req);
            var second = await _service.InitiateAsync(req);

            Assert.That(second.IsIdempotentReplay, Is.True,
                "Second request with same idempotency key must be flagged as IsIdempotentReplay=true");
        }

        [Test]
        public async Task InitiateAsync_SameIdempotencyKey_ReturnsSameDeploymentId()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildARC76Request(idempotencyKey: key);

            var first = await _service.InitiateAsync(req);
            var second = await _service.InitiateAsync(req);

            Assert.That(second.DeploymentId, Is.EqualTo(first.DeploymentId),
                "Idempotent replay must return the same DeploymentId");
        }

        [Test]
        public async Task InitiateAsync_DifferentIdempotencyKeys_ReturnsDifferentDeploymentIds()
        {
            var req1 = BuildARC76Request(idempotencyKey: Guid.NewGuid().ToString());
            var req2 = BuildARC76Request(idempotencyKey: Guid.NewGuid().ToString());

            var result1 = await _service.InitiateAsync(req1);
            var result2 = await _service.InitiateAsync(req2);

            Assert.That(result1.DeploymentId, Is.Not.EqualTo(result2.DeploymentId),
                "Different idempotency keys must produce different deployment IDs");
        }

        [Test]
        public async Task InitiateAsync_FirstRequest_IsNotIdempotentReplay()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            Assert.That(result.IsIdempotentReplay, Is.False,
                "First request must NOT be flagged as IsIdempotentReplay");
        }

        // ── InitiateAsync: All supported token standards ──────────────────────────

        [TestCase("ASA")]
        [TestCase("ARC3")]
        [TestCase("ARC200")]
        [TestCase("ARC1400")]
        [TestCase("ERC20")]
        public async Task InitiateAsync_SupportedStandards_AllSucceed(string standard)
        {
            var req = BuildARC76Request(standard: standard);
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.None),
                $"Token standard '{standard}' must be supported");
        }

        // ── InitiateAsync: Multiple networks ─────────────────────────────────────

        [TestCase("algorand-testnet")]
        [TestCase("algorand-mainnet")]
        public async Task InitiateAsync_AlgorandNetworks_AllSucceedWithARC76(string network)
        {
            var req = BuildARC76Request(network: network);
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.None),
                $"Algorand network '{network}' must be accepted with ARC76 credentials");
        }

        [TestCase("base-mainnet")]
        [TestCase("base-sepolia")]
        public async Task InitiateAsync_EvmNetworks_AllSucceedWithExplicitEvmAddress(string network)
        {
            var req = new BackendDeploymentContractRequest
            {
                ExplicitDeployerAddress = TestEvmAddress,
                TokenStandard = "ERC20",
                TokenName = "EVM Test Token",
                TokenSymbol = "ETT",
                Network = network,
                TotalSupply = 1_000_000,
                Decimals = 18,
                CorrelationId = Guid.NewGuid().ToString()
            };
            var result = await _service.InitiateAsync(req);

            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.None),
                $"EVM network '{network}' must be accepted with explicit EVM address");
        }

        // ── GetStatusAsync ────────────────────────────────────────────────────────

        [Test]
        public async Task GetStatusAsync_AfterInitiation_ReturnsStatus()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);

            var status = await _service.GetStatusAsync(initiated.DeploymentId);

            Assert.That(status, Is.Not.Null);
            Assert.That(status.DeploymentId, Is.EqualTo(initiated.DeploymentId));
        }

        [Test]
        public async Task GetStatusAsync_AfterInitiation_ReturnsSameState()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);

            var status = await _service.GetStatusAsync(initiated.DeploymentId);

            Assert.That(status.State, Is.EqualTo(initiated.State),
                "Status query must return the same state as the initiation response");
        }

        [Test]
        public async Task GetStatusAsync_UnknownDeploymentId_ReturnsFailed()
        {
            var status = await _service.GetStatusAsync("nonexistent-deployment-id");

            Assert.That(status.State, Is.EqualTo(ContractLifecycleState.Failed),
                "Status query for unknown ID must return Failed state");
            Assert.That(status.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        [Test]
        public async Task GetStatusAsync_EmptyDeploymentId_ReturnsFailed()
        {
            var status = await _service.GetStatusAsync("");

            Assert.That(status.State, Is.EqualTo(ContractLifecycleState.Failed),
                "Status query with empty ID must return Failed state");
        }

        [Test]
        public async Task GetStatusAsync_WithCorrelationId_ReturnsCorrelationId()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);
            var correlationId = Guid.NewGuid().ToString();

            var status = await _service.GetStatusAsync(initiated.DeploymentId, correlationId);

            Assert.That(status.CorrelationId, Is.EqualTo(correlationId),
                "Provided correlation ID must be echoed back");
        }

        [Test]
        public async Task GetStatusAsync_WithoutCorrelationId_GeneratesCorrelationId()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);

            var status = await _service.GetStatusAsync(initiated.DeploymentId, null);

            Assert.That(status.CorrelationId, Is.Not.Null.And.Not.Empty,
                "A correlation ID must be generated when not provided");
        }

        [Test]
        public async Task GetStatusAsync_StableAcrossPolls_StateDoesNotRegress()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);

            var states = new List<ContractLifecycleState>();
            for (var poll = 0; poll < 3; poll++)
            {
                var status = await _service.GetStatusAsync(initiated.DeploymentId);
                states.Add(status.State);
            }

            for (var i = 1; i < states.Count; i++)
            {
                Assert.That((int)states[i], Is.GreaterThanOrEqualTo((int)states[i - 1]),
                    "State must not regress across repeated polls");
            }
        }

        // ── ValidateAsync ─────────────────────────────────────────────────────────

        [Test]
        public async Task ValidateAsync_ValidARC76Request_ReturnsIsValid()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = TestEmail,
                DeployerPassword = TestPassword,
                TokenStandard = "ASA",
                TokenName = "Validation Test Token",
                TokenSymbol = "VTT",
                Network = "algorand-testnet",
                TotalSupply = 1_000,
                Decimals = 0,
                CorrelationId = Guid.NewGuid().ToString()
            };

            var result = await _service.ValidateAsync(req);

            Assert.That(result.IsValid, Is.True,
                "Valid ARC76 request must pass validation");
        }

        [Test]
        public async Task ValidateAsync_ValidARC76Request_DerivationStatusIsDerived()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = TestEmail,
                DeployerPassword = TestPassword,
                TokenStandard = "ASA",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-testnet",
                TotalSupply = 100
            };

            var result = await _service.ValidateAsync(req);

            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Derived));
        }

        [Test]
        public async Task ValidateAsync_ValidARC76Request_ReturnsDeterministicAddressTrue()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = TestEmail,
                DeployerPassword = TestPassword,
                TokenStandard = "ASA",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-testnet",
                TotalSupply = 100
            };

            var result = await _service.ValidateAsync(req);

            Assert.That(result.IsDeterministicAddress, Is.True);
        }

        [Test]
        public async Task ValidateAsync_NullRequest_ReturnsInvalid()
        {
            var result = await _service.ValidateAsync(null!);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Error));
        }

        [Test]
        public async Task ValidateAsync_NoCredentials_ReturnsInvalid()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                TokenStandard = "ASA",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-testnet",
                TotalSupply = 100
            };

            var result = await _service.ValidateAsync(req);

            Assert.That(result.IsValid, Is.False,
                "Validation without credentials or address must fail");
        }

        [Test]
        public async Task ValidateAsync_ExplicitAddress_ReturnsAddressProvided()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                ExplicitDeployerAddress = TestAlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-testnet",
                TotalSupply = 100
            };

            var result = await _service.ValidateAsync(req);

            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.AddressProvided));
        }

        [Test]
        public async Task ValidateAsync_InvalidStandard_ReturnsInvalid()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = TestEmail,
                DeployerPassword = TestPassword,
                TokenStandard = "UNKNOWN",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-testnet",
                TotalSupply = 100
            };

            var result = await _service.ValidateAsync(req);

            Assert.That(result.IsValid, Is.False,
                "Invalid standard must return IsValid=false");
        }

        [Test]
        public async Task ValidateAsync_ZeroSupply_ReturnsInvalid()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = TestEmail,
                DeployerPassword = TestPassword,
                TokenStandard = "ASA",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-testnet",
                TotalSupply = 0
            };

            var result = await _service.ValidateAsync(req);

            Assert.That(result.IsValid, Is.False,
                "Zero supply must return IsValid=false");
        }

        [Test]
        public async Task ValidateAsync_ReturnsValidationResults()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = TestEmail,
                DeployerPassword = TestPassword,
                TokenStandard = "ASA",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-testnet",
                TotalSupply = 100
            };

            var result = await _service.ValidateAsync(req);

            Assert.That(result.ValidationResults, Is.Not.Null.And.Not.Empty,
                "Field-level validation results must be returned");
        }

        [Test]
        public async Task ValidateAsync_ValidRequest_ReturnsDeployerAddress()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                DeployerEmail = TestEmail,
                DeployerPassword = TestPassword,
                TokenStandard = "ASA",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-testnet",
                TotalSupply = 100
            };

            var result = await _service.ValidateAsync(req);

            Assert.That(result.DeployerAddress, Is.Not.Null.And.Not.Empty,
                "Deployer address must be returned for valid ARC76 validation");
        }

        // ── GetAuditTrailAsync ────────────────────────────────────────────────────

        [Test]
        public async Task GetAuditTrailAsync_AfterDeployment_ReturnsAuditEvents()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);

            var trail = await _service.GetAuditTrailAsync(initiated.DeploymentId);

            Assert.That(trail, Is.Not.Null);
            Assert.That(trail.Events, Is.Not.Null.And.Not.Empty,
                "Audit trail must contain events after deployment");
        }

        [Test]
        public async Task GetAuditTrailAsync_AfterDeployment_ContainsDeploymentInitiatedEvent()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);

            var trail = await _service.GetAuditTrailAsync(initiated.DeploymentId);

            Assert.That(
                trail.Events.Any(e => e.EventKind == ComplianceAuditEventKind.DeploymentInitiated),
                Is.True,
                "Audit trail must contain a DeploymentInitiated event");
        }

        [Test]
        public async Task GetAuditTrailAsync_AfterDeployment_ContainsDeploymentCompletedEvent()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);

            var trail = await _service.GetAuditTrailAsync(initiated.DeploymentId);

            Assert.That(
                trail.Events.Any(e => e.EventKind == ComplianceAuditEventKind.DeploymentCompleted),
                Is.True,
                "Audit trail must contain a DeploymentCompleted event for successful deployments");
        }

        [Test]
        public async Task GetAuditTrailAsync_AfterARC76Deployment_ContainsAccountDerivedEvent()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);

            var trail = await _service.GetAuditTrailAsync(initiated.DeploymentId);

            Assert.That(
                trail.Events.Any(e => e.EventKind == ComplianceAuditEventKind.AccountDerived),
                Is.True,
                "ARC76 deployment audit trail must contain AccountDerived event");
        }

        [Test]
        public async Task GetAuditTrailAsync_AfterDeployment_FinalStateIsCompleted()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);

            var trail = await _service.GetAuditTrailAsync(initiated.DeploymentId);

            Assert.That(trail.FinalState, Is.EqualTo(ContractLifecycleState.Completed));
        }

        [Test]
        public async Task GetAuditTrailAsync_UnknownDeploymentId_ReturnsEmptyTrail()
        {
            var trail = await _service.GetAuditTrailAsync("unknown-deployment-id");

            Assert.That(trail, Is.Not.Null);
            Assert.That(trail.Events, Is.Empty,
                "Audit trail for unknown deployment must return empty event list");
        }

        [Test]
        public async Task GetAuditTrailAsync_AfterDeployment_DeploymentIdMatchesRequest()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);

            var trail = await _service.GetAuditTrailAsync(initiated.DeploymentId);

            Assert.That(trail.DeploymentId, Is.EqualTo(initiated.DeploymentId));
        }

        [Test]
        public async Task GetAuditTrailAsync_WithCorrelationId_EchoesCorrelationId()
        {
            var req = BuildARC76Request();
            var initiated = await _service.InitiateAsync(req);
            var correlationId = Guid.NewGuid().ToString();

            var trail = await _service.GetAuditTrailAsync(initiated.DeploymentId, correlationId);

            Assert.That(trail.CorrelationId, Is.EqualTo(correlationId));
        }

        // ── DeriveARC76Address ────────────────────────────────────────────────────

        [Test]
        public void DeriveARC76Address_ValidCredentials_ReturnsDeterministicAddress()
        {
            var address1 = _service.DeriveARC76Address(TestEmail, TestPassword);
            var address2 = _service.DeriveARC76Address(TestEmail, TestPassword);

            Assert.That(address1, Is.EqualTo(address2),
                "Same credentials must always produce the same Algorand address");
        }

        [Test]
        public void DeriveARC76Address_ValidCredentials_ReturnsAlgorandAddress()
        {
            var address = _service.DeriveARC76Address(TestEmail, TestPassword);

            Assert.That(address, Is.Not.Null.And.Not.Empty);
            Assert.That(address.Length, Is.EqualTo(58),
                "Algorand address must be 58 characters");
        }

        [Test]
        public void DeriveARC76Address_DifferentEmails_ReturnsDifferentAddresses()
        {
            var address1 = _service.DeriveARC76Address("user1@example.com", TestPassword);
            var address2 = _service.DeriveARC76Address("user2@example.com", TestPassword);

            Assert.That(address1, Is.Not.EqualTo(address2),
                "Different email inputs must produce different addresses");
        }

        [Test]
        public void DeriveARC76Address_DifferentPasswords_ReturnsDifferentAddresses()
        {
            var address1 = _service.DeriveARC76Address(TestEmail, "Password1!");
            var address2 = _service.DeriveARC76Address(TestEmail, "Password2!");

            Assert.That(address1, Is.Not.EqualTo(address2),
                "Different password inputs must produce different addresses");
        }

        [Test]
        public void DeriveARC76Address_EmptyEmail_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _service.DeriveARC76Address("", TestPassword),
                "Empty email must throw ArgumentException");
        }

        [Test]
        public void DeriveARC76Address_EmptyPassword_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _service.DeriveARC76Address(TestEmail, ""),
                "Empty password must throw ArgumentException");
        }

        [Test]
        public void DeriveARC76Address_WhitespaceEmail_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _service.DeriveARC76Address("   ", TestPassword),
                "Whitespace-only email must throw ArgumentException");
        }

        [Test]
        public void DeriveARC76Address_CaseInsensitiveEmail_ReturnsSameAddress()
        {
            var address1 = _service.DeriveARC76Address("USER@EXAMPLE.COM", TestPassword);
            var address2 = _service.DeriveARC76Address("user@example.com", TestPassword);

            Assert.That(address1, Is.EqualTo(address2),
                "Email canonicalization must make derivation case-insensitive");
        }

        // ── IsValidStateTransition ────────────────────────────────────────────────

        [Test]
        public void IsValidStateTransition_PendingToValidated_IsValid()
        {
            Assert.That(
                _service.IsValidStateTransition(ContractLifecycleState.Pending, ContractLifecycleState.Validated),
                Is.True);
        }

        [Test]
        public void IsValidStateTransition_PendingToFailed_IsValid()
        {
            Assert.That(
                _service.IsValidStateTransition(ContractLifecycleState.Pending, ContractLifecycleState.Failed),
                Is.True);
        }

        [Test]
        public void IsValidStateTransition_PendingToCancelled_IsValid()
        {
            Assert.That(
                _service.IsValidStateTransition(ContractLifecycleState.Pending, ContractLifecycleState.Cancelled),
                Is.True);
        }

        [Test]
        public void IsValidStateTransition_ValidatedToSubmitted_IsValid()
        {
            Assert.That(
                _service.IsValidStateTransition(ContractLifecycleState.Validated, ContractLifecycleState.Submitted),
                Is.True);
        }

        [Test]
        public void IsValidStateTransition_SubmittedToConfirmed_IsValid()
        {
            Assert.That(
                _service.IsValidStateTransition(ContractLifecycleState.Submitted, ContractLifecycleState.Confirmed),
                Is.True);
        }

        [Test]
        public void IsValidStateTransition_ConfirmedToCompleted_IsValid()
        {
            Assert.That(
                _service.IsValidStateTransition(ContractLifecycleState.Confirmed, ContractLifecycleState.Completed),
                Is.True);
        }

        [Test]
        public void IsValidStateTransition_FailedToPending_IsValid_Retry()
        {
            Assert.That(
                _service.IsValidStateTransition(ContractLifecycleState.Failed, ContractLifecycleState.Pending),
                Is.True,
                "Failed → Pending is valid for retry");
        }

        [Test]
        public void IsValidStateTransition_CompletedToAny_IsInvalid_Terminal()
        {
            foreach (var target in Enum.GetValues<ContractLifecycleState>())
            {
                Assert.That(
                    _service.IsValidStateTransition(ContractLifecycleState.Completed, target),
                    Is.False,
                    $"Completed is a terminal state; Completed → {target} must be invalid");
            }
        }

        [Test]
        public void IsValidStateTransition_CancelledToAny_IsInvalid_Terminal()
        {
            foreach (var target in Enum.GetValues<ContractLifecycleState>())
            {
                Assert.That(
                    _service.IsValidStateTransition(ContractLifecycleState.Cancelled, target),
                    Is.False,
                    $"Cancelled is a terminal state; Cancelled → {target} must be invalid");
            }
        }

        [Test]
        public void IsValidStateTransition_PendingToCompleted_IsInvalid_SkipsStages()
        {
            Assert.That(
                _service.IsValidStateTransition(ContractLifecycleState.Pending, ContractLifecycleState.Completed),
                Is.False,
                "Skipping intermediate states must be invalid");
        }

        [Test]
        public void IsValidStateTransition_PendingToPending_IsInvalid_SameState()
        {
            Assert.That(
                _service.IsValidStateTransition(ContractLifecycleState.Pending, ContractLifecycleState.Pending),
                Is.False,
                "Self-transition to the same state must be invalid");
        }

        // ── ARC76 determinism: 3-run repeatability ────────────────────────────────

        [Test]
        public async Task InitiateAsync_ThreeRunsIdenticalRequest_IdenticalDeployerAddress()
        {
            var email = "repeatability@biatec-test.example.com";
            var addresses = new List<string?>();

            for (var run = 0; run < 3; run++)
            {
                var req = BuildARC76Request(
                    email: email,
                    idempotencyKey: Guid.NewGuid().ToString());
                var result = await _service.InitiateAsync(req);
                addresses.Add(result.DeployerAddress);
            }

            Assert.That(addresses.Distinct().Count(), Is.EqualTo(1),
                "DeployerAddress must be identical across 3 independent runs (ARC76 determinism)");
        }

        // ── Schema contract: required fields must be non-null on success ──────────

        [Test]
        public async Task InitiateAsync_SuccessfulDeployment_SchemaContractNonNullFields()
        {
            var req = BuildARC76Request();
            var result = await _service.InitiateAsync(req);

            // Required fields per frontend contract
            Assert.Multiple(() =>
            {
                Assert.That(result.DeploymentId, Is.Not.Null, "DeploymentId must not be null");
                Assert.That(result.IdempotencyKey, Is.Not.Null, "IdempotencyKey must not be null");
                Assert.That(result.CorrelationId, Is.Not.Null, "CorrelationId must not be null");
                Assert.That(result.InitiatedAt, Is.Not.Null, "InitiatedAt must not be null");
                Assert.That(result.LastUpdatedAt, Is.Not.Null, "LastUpdatedAt must not be null");
                Assert.That(result.DeployerAddress, Is.Not.Null, "DeployerAddress must not be null");
                Assert.That(result.ValidationResults, Is.Not.Null, "ValidationResults must not be null");
                Assert.That(result.AuditEvents, Is.Not.Null, "AuditEvents must not be null");
            });
        }
    }
}
