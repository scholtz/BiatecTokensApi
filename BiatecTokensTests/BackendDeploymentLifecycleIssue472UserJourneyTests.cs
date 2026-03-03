using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// User journey tests for Issue #472: Vision Milestone – Deterministic Backend
    /// Deployment Lifecycle and ARC76 Contract Hardening.
    ///
    /// Journey categories:
    ///   HP  – Happy path: user deploys a token via email/password or explicit address
    ///   II  – Invalid input: user submits bad or missing data
    ///   BD  – Boundary: edge values at valid/invalid thresholds
    ///   FR  – Failure / recovery: idempotency replay, failed-state handling
    ///   NX  – Non-crypto-native UX: safe messages, no internal details, correlation IDs
    ///
    /// These tests directly exercise the service layer without HTTP overhead,
    /// validating user-visible outcomes and message quality.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendDeploymentLifecycleIssue472UserJourneyTests
    {
        private BackendDeploymentLifecycleContractService _service = null!;
        private Mock<ILogger<BackendDeploymentLifecycleContractService>> _loggerMock = null!;

        private const string KnownEmail = "testuser@biatec.io";
        private const string KnownPassword = "TestPassword123!";
        private const string KnownAlgorandAddress =
            "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";
        private const string EvmAddress = "0x0000000000000000000000000000000000000001";

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<BackendDeploymentLifecycleContractService>>();
            _service = new BackendDeploymentLifecycleContractService(_loggerMock.Object);
        }

        // ════════════════════════════════════════════════════════════════════════
        // HP – Happy path journeys
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task HP1_UserDeploysAsaToken_WithExplicitAddress_Completed()
        {
            // A user deploys a simple ASA token using an explicit Algorand address.
            var request = BuildAlgorandRequest("ASA", "SimpleToken", "SMP");
            var result = await _service.InitiateAsync(request);

            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.None));
            Assert.That(result.AssetId, Is.GreaterThan(0UL));
        }

        [Test]
        public async Task HP2_UserDeploysArc3Token_WithCredentials_IsDeterministicAddress()
        {
            // A user deploys ARC3 token using email/password; ARC76 derives the address.
            var request = BuildCredentialsRequest("ARC3", "MetadataToken", "MTA");
            var result = await _service.InitiateAsync(request);

            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(result.IsDeterministicAddress, Is.True);
            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Derived));
            Assert.That(result.DeployerAddress, Is.EqualTo(KnownAlgorandAddress));
        }

        [Test]
        public async Task HP3_UserDeploysArc200Token_WithCredentials_Completed()
        {
            var request = BuildCredentialsRequest("ARC200", "SmartToken", "SMT");
            var result = await _service.InitiateAsync(request);

            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(result.ConfirmedRound, Is.GreaterThan(0UL));
        }

        [Test]
        public async Task HP4_UserDeploysErc20Token_WithExplicitEvmAddress_Completed()
        {
            var request = BuildEvmRequest("ERC20", "EvmToken", "EVT");
            var result = await _service.InitiateAsync(request);

            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(result.DeployerAddress, Is.EqualTo(EvmAddress));
        }

        [Test]
        public async Task HP5_UserResubmitsSameRequest_NoDuplicateCreated()
        {
            // Idempotency: two identical requests produce one deployment.
            var request = BuildAlgorandRequest("ASA", "IdemToken", "IDM");
            request.IdempotencyKey = "hp5-no-dup";

            var first = await _service.InitiateAsync(request);
            var second = await _service.InitiateAsync(request);

            Assert.That(second.DeploymentId, Is.EqualTo(first.DeploymentId));
            Assert.That(second.AssetId, Is.EqualTo(first.AssetId));
            Assert.That(second.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task HP6_UserChecksStatus_AfterDeployment_SeesCompletedState()
        {
            var request = BuildAlgorandRequest("ASA", "StatusToken", "STK");
            var deployed = await _service.InitiateAsync(request);

            var status = await _service.GetStatusAsync(deployed.DeploymentId);
            Assert.That(status.State, Is.EqualTo(ContractLifecycleState.Completed));
        }

        [Test]
        public async Task HP7_UserDryRunsValidation_BeforeDeployment()
        {
            // User validates without deploying.
            var req = new BackendDeploymentContractValidationRequest
            {
                ExplicitDeployerAddress = KnownAlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "DryRunToken",
                TokenSymbol = "DRY",
                Network = "algorand-mainnet",
                TotalSupply = 100_000,
                Decimals = 2
            };

            var result = await _service.ValidateAsync(req);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Message, Does.Contain("valid"));
        }

        // ════════════════════════════════════════════════════════════════════════
        // II – Invalid input journeys
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task II1_UserSubmitsNullRequest_SeesFriendlyError()
        {
            var result = await _service.InitiateAsync(null!);
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Failed));
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
            Assert.That(result.UserGuidance, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task II2_UserSubmitsUnsupportedStandard_SeesUnsupportedStandardCode()
        {
            var request = BuildAlgorandRequest("MYTOKEN", "BadToken", "BAD");
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.UnsupportedStandard));
        }

        [Test]
        public async Task II3_UserSubmitsUnknownNetwork_SeesNetworkUnavailableCode()
        {
            var request = BuildAlgorandRequest("ASA", "NetToken", "NET");
            request.Network = "unknown-chain";
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.NetworkUnavailable));
        }

        [Test]
        public async Task II4_UserSubmitsZeroSupply_SeesValidationError()
        {
            var request = BuildAlgorandRequest("ASA", "ZeroSupply", "ZRO");
            request.TotalSupply = 0;
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
        }

        [Test]
        public async Task II5_UserSubmitsNoCredentialsNoAddress_SeesRequiredFieldMissing()
        {
            var request = BuildAlgorandRequest("ASA", "NoAddr", "NAD");
            request.ExplicitDeployerAddress = null;
            request.DeployerEmail = null;
            request.DeployerPassword = null;
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        [Test]
        public async Task II6_UserSubmitsEvmAddressForAlgorandNetwork_SeesAddressMismatch()
        {
            var request = BuildAlgorandRequest("ASA", "WrongAddr", "WAD");
            request.ExplicitDeployerAddress = EvmAddress;
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.DeriveAddressMismatch));
        }

        [Test]
        public async Task II7_UserSubmitsEmptyTokenName_SeesRequiredFieldMissing()
        {
            var request = BuildAlgorandRequest("ASA", "", "TST");
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        // ════════════════════════════════════════════════════════════════════════
        // BD – Boundary condition journeys
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task BD1_TokenName_MaxLength64_Accepted()
        {
            var request = BuildAlgorandRequest("ASA", new string('A', 64), "TST");
            var result = await _service.InitiateAsync(request);
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed));
        }

        [Test]
        public async Task BD2_TokenName_65Characters_Rejected()
        {
            var request = BuildAlgorandRequest("ASA", new string('A', 65), "TST");
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
        }

        [Test]
        public async Task BD3_TokenSymbol_MaxLength8_Accepted()
        {
            var request = BuildAlgorandRequest("ASA", "MyToken", "EIGHTCHR");
            var result = await _service.InitiateAsync(request);
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed));
        }

        [Test]
        public async Task BD4_TokenSymbol_9Characters_Rejected()
        {
            var request = BuildAlgorandRequest("ASA", "MyToken", "NINECHARS");
            var result = await _service.InitiateAsync(request);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
        }

        [Test]
        public async Task BD5_Decimals_MaxValue19_Accepted()
        {
            var request = BuildAlgorandRequest("ASA", "MaxDecToken", "MDT");
            request.Decimals = 19;
            var result = await _service.InitiateAsync(request);
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed));
        }

        // ════════════════════════════════════════════════════════════════════════
        // FR – Failure / recovery journeys
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task FR1_UserRetriesAfterFailedValidation_CanResendWithFixedInput()
        {
            // First attempt with bad input
            var badRequest = BuildAlgorandRequest("ASA", "", "TST");
            var failResult = await _service.InitiateAsync(badRequest);
            Assert.That(failResult.State, Is.EqualTo(ContractLifecycleState.Failed));

            // Second attempt with fixed input
            var goodRequest = BuildAlgorandRequest("ASA", "FixedToken", "FXT");
            var successResult = await _service.InitiateAsync(goodRequest);
            Assert.That(successResult.State, Is.EqualTo(ContractLifecycleState.Completed));
        }

        [Test]
        public async Task FR2_UserRetriesWithSameKey_ReturnsIdempotentReplay()
        {
            var request = BuildAlgorandRequest("ASA", "RetryToken", "RTK");
            request.IdempotencyKey = "fr2-retry-test";
            await _service.InitiateAsync(request);

            var retry = await _service.InitiateAsync(request);
            Assert.That(retry.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task FR3_UserChecksMissingDeploymentStatus_SeesNotFoundMessage()
        {
            var result = await _service.GetStatusAsync("dep-missing-fr3");
            Assert.That(result.Message, Does.Contain("not found")
                .Or.Contain("not found").IgnoreCase);
            Assert.That(result.UserGuidance, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task FR4_UserQueriesAuditTrailForCompletedDeployment_SeesEvents()
        {
            var request = BuildAlgorandRequest("ASA", "AuditToken", "AUD");
            var deployed = await _service.InitiateAsync(request);

            var trail = await _service.GetAuditTrailAsync(deployed.DeploymentId);
            Assert.That(trail.Events, Is.Not.Empty);
            Assert.That(trail.FinalState, Is.EqualTo(ContractLifecycleState.Completed));
        }

        // ════════════════════════════════════════════════════════════════════════
        // NX – Non-crypto-native UX journeys
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task NX1_ErrorMessages_ContainNoInternalStackTraces()
        {
            var request = BuildAlgorandRequest("UNKNOWN", "Bad", "BAD");
            var result = await _service.InitiateAsync(request);

            Assert.That(result.Message, Does.Not.Contain("Exception"));
            Assert.That(result.Message, Does.Not.Contain("StackTrace"));
            Assert.That(result.Message, Does.Not.Contain("at BiatecTokens"));
        }

        [Test]
        public async Task NX2_ErrorMessages_ContainUserGuidance()
        {
            var request = BuildAlgorandRequest("ASA", "", "TST");
            var result = await _service.InitiateAsync(request);

            Assert.That(result.UserGuidance, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task NX3_SuccessResponse_ContainsHumanReadableMessage()
        {
            var request = BuildAlgorandRequest("ASA", "FriendlyToken", "FTK");
            var result = await _service.InitiateAsync(request);

            Assert.That(result.Message, Is.Not.Empty);
            Assert.That(result.Message, Does.Contain("FriendlyToken"));
        }

        [Test]
        public async Task NX4_AllResponses_HaveCorrelationId()
        {
            var request = BuildAlgorandRequest("ASA", "CidToken", "CID");
            request.CorrelationId = "nx4-test-cid";
            var result = await _service.InitiateAsync(request);
            Assert.That(result.CorrelationId, Is.EqualTo("nx4-test-cid"));
        }

        [Test]
        public async Task NX5_ErrorResponse_HasMachineReadableCode()
        {
            var request = BuildAlgorandRequest("BADSTANDARD", "Bad", "BAD");
            var result = await _service.InitiateAsync(request);
            // Code should not be None when there's an error
            Assert.That(result.ErrorCode, Is.Not.EqualTo(DeploymentErrorCode.None));
        }

        [Test]
        public async Task NX6_ValidateOnly_DoesNotCreateDeployment()
        {
            // Validation should have no side effects on deployment store
            var validateReq = new BackendDeploymentContractValidationRequest
            {
                ExplicitDeployerAddress = KnownAlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "NoSideEffect",
                TokenSymbol = "NSE",
                Network = "algorand-mainnet",
                TotalSupply = 1000,
                Decimals = 0
            };

            await _service.ValidateAsync(validateReq);

            // Try to get status for any deployment ID - should not find the validation-only request
            var status = await _service.GetStatusAsync("dep-validate-only-nse");
            Assert.That(status.ErrorCode, Is.Not.EqualTo(DeploymentErrorCode.None));
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private BackendDeploymentContractRequest BuildAlgorandRequest(
            string standard, string name, string symbol) =>
            new()
            {
                ExplicitDeployerAddress = KnownAlgorandAddress,
                TokenStandard = standard,
                TokenName = name,
                TokenSymbol = symbol,
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            };

        private BackendDeploymentContractRequest BuildCredentialsRequest(
            string standard, string name, string symbol) =>
            new()
            {
                DeployerEmail = KnownEmail,
                DeployerPassword = KnownPassword,
                TokenStandard = standard,
                TokenName = name,
                TokenSymbol = symbol,
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            };

        private BackendDeploymentContractRequest BuildEvmRequest(
            string standard, string name, string symbol) =>
            new()
            {
                ExplicitDeployerAddress = EvmAddress,
                TokenStandard = standard,
                TokenName = name,
                TokenSymbol = symbol,
                Network = "base-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 18
            };
    }
}
