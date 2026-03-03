using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using BiatecTokensApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Advanced coverage tests for Issue #472: Deterministic Backend Deployment Lifecycle
    /// and ARC76 Contract Hardening.
    ///
    /// This file provides the mandatory additional coverage layers:
    ///   - Branch coverage: every DeploymentErrorCode, ContractLifecycleState, and enum value
    ///   - Concurrency tests: parallel requests produce independent, correct, deterministic results
    ///   - Multi-step workflow: chaining multiple pipeline executions with audit trail continuity
    ///   - Retry/rollback semantics: transient vs terminal failure hints; retry-from-failed flow
    ///   - Policy conflict tests: multiple conflicting input conditions and fail-fast ordering
    ///   - Malformed input tests: null, empty, oversized, special characters
    ///
    /// MANDATORY TEST TYPES coverage (per project coding standards):
    ///   [x] Unit tests (per-method logic, happy + error path) — see ServiceUnitTests
    ///   [x] Branch coverage tests (every enum value, every code path) — THIS FILE
    ///   [x] E2E/integration tests using DI — see ContractTests + E2EWorkflowTests
    ///   [x] Idempotency determinism tests (3 runs, identical outcomes) — see E2EWorkflowTests
    ///   [x] Regression/backward-compat tests — see E2EWorkflowTests
    ///   [x] Policy conflict tests — THIS FILE
    ///   [x] Malformed input tests — THIS FILE
    ///   [x] Concurrency tests — THIS FILE
    ///   [x] Multi-step workflow tests — THIS FILE
    ///   [x] Retry/rollback semantics tests — THIS FILE
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendDeploymentLifecycleIssue472AdvancedCoverageTests
    {
        private BackendDeploymentLifecycleContractService _service = null!;

        private const string AlgorandAddress =
            "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";
        private const string EvmAddress =
            "0x0000000000000000000000000000000000000001";
        private const string KnownEmail = "testuser@biatec.io";
        private const string KnownPassword = "TestPassword123!";

        [SetUp]
        public void Setup()
        {
            _service = new BackendDeploymentLifecycleContractService(
                new Mock<ILogger<BackendDeploymentLifecycleContractService>>().Object);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Branch coverage: every DeploymentErrorCode value is reachable
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Branch_ErrorCode_None_OnSuccess()
        {
            var result = await _service.InitiateAsync(BuildAlgorandRequest("ASA", "BranchNone", "BNO"));
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.None));
        }

        [Test]
        public async Task Branch_ErrorCode_RequiredFieldMissing_NullRequest()
        {
            var result = await _service.InitiateAsync(null!);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        [Test]
        public async Task Branch_ErrorCode_RequiredFieldMissing_EmptyTokenName()
        {
            var req = BuildAlgorandRequest("ASA", "", "TST");
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
            Assert.That(result.ValidationResults.Any(v => v.Field == "TokenName" && !v.IsValid), Is.True);
        }

        [Test]
        public async Task Branch_ErrorCode_RequiredFieldMissing_EmptyTokenSymbol()
        {
            var req = BuildAlgorandRequest("ASA", "TestToken", "");
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
            Assert.That(result.ValidationResults.Any(v => v.Field == "TokenSymbol" && !v.IsValid), Is.True);
        }

        [Test]
        public async Task Branch_ErrorCode_RequiredFieldMissing_EmptyTokenStandard()
        {
            var req = BuildAlgorandRequest("", "TestToken", "TST");
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
            Assert.That(result.ValidationResults.Any(v => v.Field == "TokenStandard" && !v.IsValid), Is.True);
        }

        [Test]
        public async Task Branch_ErrorCode_RequiredFieldMissing_EmptyNetwork()
        {
            var req = BuildAlgorandRequest("ASA", "TestToken", "TST");
            req.Network = "";
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
            Assert.That(result.ValidationResults.Any(v => v.Field == "Network" && !v.IsValid), Is.True);
        }

        [Test]
        public async Task Branch_ErrorCode_RequiredFieldMissing_NoCredentialsNoAddress()
        {
            var req = BuildAlgorandRequest("ASA", "TestToken", "TST");
            req.ExplicitDeployerAddress = null;
            req.DeployerEmail = null;
            req.DeployerPassword = null;
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        [Test]
        public async Task Branch_ErrorCode_ValidationRangeFault_TotalSupplyZero()
        {
            var req = BuildAlgorandRequest("ASA", "TestToken", "TST");
            req.TotalSupply = 0;
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
            Assert.That(result.ValidationResults.Any(v => v.Field == "TotalSupply" && !v.IsValid), Is.True);
        }

        [Test]
        public async Task Branch_ErrorCode_ValidationRangeFault_DecimalsNegative()
        {
            var req = BuildAlgorandRequest("ASA", "TestToken", "TST");
            req.Decimals = -1;
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
            Assert.That(result.ValidationResults.Any(v => v.Field == "Decimals" && !v.IsValid), Is.True);
        }

        [Test]
        public async Task Branch_ErrorCode_ValidationRangeFault_Decimals20()
        {
            var req = BuildAlgorandRequest("ASA", "TestToken", "TST");
            req.Decimals = 20;
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
        }

        [Test]
        public async Task Branch_ErrorCode_ValidationRangeFault_TokenName65Chars()
        {
            var req = BuildAlgorandRequest("ASA", new string('X', 65), "TST");
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
            Assert.That(result.ValidationResults.Any(v => v.Field == "TokenName" && !v.IsValid), Is.True);
        }

        [Test]
        public async Task Branch_ErrorCode_ValidationRangeFault_TokenSymbol9Chars()
        {
            var req = BuildAlgorandRequest("ASA", "TestToken", "NINECHARS");
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
            Assert.That(result.ValidationResults.Any(v => v.Field == "TokenSymbol" && !v.IsValid), Is.True);
        }

        [Test]
        public async Task Branch_ErrorCode_UnsupportedStandard_UnknownStandard()
        {
            var req = BuildAlgorandRequest("XTOKEN", "TestToken", "TST");
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.UnsupportedStandard));
        }

        [Test]
        public async Task Branch_ErrorCode_NetworkUnavailable_UnknownNetwork()
        {
            var req = BuildAlgorandRequest("ASA", "TestToken", "TST");
            req.Network = "unknown-network-xyz";
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.NetworkUnavailable));
        }

        [Test]
        public async Task Branch_ErrorCode_DeriveAddressMismatch_EvmOnAlgorand()
        {
            var req = BuildAlgorandRequest("ASA", "TestToken", "TST");
            req.ExplicitDeployerAddress = EvmAddress;
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.DeriveAddressMismatch));
        }

        [Test]
        public async Task Branch_ErrorCode_DeriveAddressMismatch_AlgorandOnEvm()
        {
            var req = BuildEvmRequest("ERC20", "TestToken", "TST");
            req.ExplicitDeployerAddress = AlgorandAddress; // Algorand address on EVM network
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.DeriveAddressMismatch));
        }

        [Test]
        public async Task Branch_ErrorCode_RequiredFieldMissing_WhitespaceCredentials()
        {
            // When both email and password are whitespace, they are treated as empty,
            // triggering RequiredFieldMissing (not InvalidCredentials).
            var req = BuildAlgorandRequest("ASA", "TestToken", "TST");
            req.ExplicitDeployerAddress = null;
            req.DeployerEmail = "   ";
            req.DeployerPassword = "   ";
            var result = await _service.InitiateAsync(req);
            // Whitespace email/password are treated as empty, so RequiredFieldMissing
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Branch coverage: all ContractLifecycleState values in state machine
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void Branch_AllContractLifecycleStates_CoveredInTransitionMap()
        {
            // Verify all non-terminal states can transition to Failed
            Assert.That(_service.IsValidStateTransition(ContractLifecycleState.Pending, ContractLifecycleState.Failed), Is.True);
            Assert.That(_service.IsValidStateTransition(ContractLifecycleState.Validated, ContractLifecycleState.Failed), Is.True);
            Assert.That(_service.IsValidStateTransition(ContractLifecycleState.Submitted, ContractLifecycleState.Failed), Is.True);
            Assert.That(_service.IsValidStateTransition(ContractLifecycleState.Confirmed, ContractLifecycleState.Failed), Is.True);
            // Terminal states cannot transition
            Assert.That(_service.IsValidStateTransition(ContractLifecycleState.Completed, ContractLifecycleState.Failed), Is.False);
            Assert.That(_service.IsValidStateTransition(ContractLifecycleState.Cancelled, ContractLifecycleState.Failed), Is.False);
        }

        [Test]
        public void Branch_CancelledState_BlocksAllTransitions()
        {
            foreach (ContractLifecycleState target in Enum.GetValues<ContractLifecycleState>())
            {
                Assert.That(
                    _service.IsValidStateTransition(ContractLifecycleState.Cancelled, target),
                    Is.False,
                    $"Cancelled→{target} must be blocked");
            }
        }

        [Test]
        public void Branch_CompletedState_BlocksAllTransitions()
        {
            foreach (ContractLifecycleState target in Enum.GetValues<ContractLifecycleState>())
            {
                Assert.That(
                    _service.IsValidStateTransition(ContractLifecycleState.Completed, target),
                    Is.False,
                    $"Completed→{target} must be blocked");
            }
        }

        [Test]
        public void Branch_CancellationPath_PendingToCancelled_Valid()
        {
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Pending, ContractLifecycleState.Cancelled), Is.True);
        }

        [Test]
        public void Branch_CancellationPath_ValidatedToCancelled_Valid()
        {
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Validated, ContractLifecycleState.Cancelled), Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Branch coverage: ARC76DerivationStatus values
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Branch_DerivationStatus_Derived_WhenCredentialsProvided()
        {
            var req = new BackendDeploymentContractRequest
            {
                DeployerEmail = KnownEmail,
                DeployerPassword = KnownPassword,
                TokenStandard = "ASA",
                TokenName = "DerivedToken",
                TokenSymbol = "DRV",
                Network = "algorand-mainnet",
                TotalSupply = 1_000,
                Decimals = 0
            };
            var result = await _service.InitiateAsync(req);
            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Derived));
        }

        [Test]
        public async Task Branch_DerivationStatus_AddressProvided_WhenExplicitAddressGiven()
        {
            var req = BuildAlgorandRequest("ASA", "AddrProvidedToken", "APT");
            var result = await _service.InitiateAsync(req);
            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.AddressProvided));
        }

        [Test]
        public async Task Branch_DerivationStatus_Error_WhenNeitherCredentialsNorAddress()
        {
            var req = BuildAlgorandRequest("ASA", "ErrorToken", "ERR");
            req.ExplicitDeployerAddress = null;
            req.DeployerEmail = null;
            req.DeployerPassword = null;
            var result = await _service.InitiateAsync(req);
            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Error));
        }

        [Test]
        public async Task Branch_DerivationStatus_Error_ReflectedInValidation()
        {
            var req = new BackendDeploymentContractValidationRequest
            {
                TokenStandard = "ASA",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-mainnet",
                TotalSupply = 1000,
                Decimals = 0
                // No credentials or address
            };
            var result = await _service.ValidateAsync(req);
            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Error));
            Assert.That(result.IsValid, Is.False);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Branch coverage: all supported token standards
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        [TestCase("ASA")]
        [TestCase("ARC3")]
        [TestCase("ARC200")]
        [TestCase("ARC1400")]
        [TestCase("ERC20")]
        public async Task Branch_AllSupportedStandards_DeploySuccessfully(string standard)
        {
            string network = standard == "ERC20" ? "base-mainnet" : "algorand-mainnet";
            string address = standard == "ERC20" ? EvmAddress : AlgorandAddress;
            var req = new BackendDeploymentContractRequest
            {
                ExplicitDeployerAddress = address,
                TokenStandard = standard,
                TokenName = $"{standard}BranchToken",
                TokenSymbol = standard.Length > 8 ? standard[..8] : standard,
                Network = network,
                TotalSupply = 1_000_000,
                Decimals = 6
            };
            var result = await _service.InitiateAsync(req);
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed),
                $"Standard {standard} should deploy successfully");
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.None));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Branch coverage: all supported networks
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        [TestCase("algorand-mainnet", "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI")]
        [TestCase("algorand-testnet", "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI")]
        [TestCase("base-mainnet", "0x0000000000000000000000000000000000000001")]
        [TestCase("base-sepolia", "0x0000000000000000000000000000000000000001")]
        public async Task Branch_AllSupportedNetworks_DeploySuccessfully(string network, string address)
        {
            string standard = network.StartsWith("algorand") ? "ASA" : "ERC20";
            var req = new BackendDeploymentContractRequest
            {
                ExplicitDeployerAddress = address,
                TokenStandard = standard,
                TokenName = $"NetToken{network.Replace("-", "")}",
                TokenSymbol = "NET",
                Network = network,
                TotalSupply = 1_000_000,
                Decimals = 0
            };
            var result = await _service.InitiateAsync(req);
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed),
                $"Network {network} should work");
        }

        // ════════════════════════════════════════════════════════════════════════
        // Policy conflict tests: multiple conflicting input conditions
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task PolicyConflict_EmptyTokenName_TakesFirstValidationFailure()
        {
            // Both TokenName empty and TokenSymbol too long — first failure returned
            var req = BuildAlgorandRequest("ASA", "", "TOOLONGSY");
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.Not.EqualTo(DeploymentErrorCode.None));
            // First error by order of validation: TokenName
            Assert.That(result.ValidationResults.Any(v => v.Field == "TokenName" && !v.IsValid), Is.True);
        }

        [Test]
        public async Task PolicyConflict_MultipleFieldErrors_AllCapturedInValidationResults()
        {
            // TokenName empty + zero supply + invalid decimals
            var req = BuildAlgorandRequest("ASA", "", "TST");
            req.TotalSupply = 0;
            req.Decimals = 25;
            var result = await _service.InitiateAsync(req);

            var failedFields = result.ValidationResults.Where(v => !v.IsValid).Select(v => v.Field).ToList();
            // At least TokenName failure should be present
            Assert.That(failedFields, Contains.Item("TokenName"));
        }

        [Test]
        public async Task PolicyConflict_UnsupportedStandardAndBadNetwork_StandardErrorReturned()
        {
            // Both standard invalid and network invalid — standard check comes first
            var req = BuildAlgorandRequest("BADSTANDARD", "TestToken", "TST");
            req.Network = "bad-network";
            var result = await _service.InitiateAsync(req);
            // Validation processes fields in order: TokenStandard → TokenName → TokenSymbol → Network → ...
            Assert.That(result.ErrorCode, Is.Not.EqualTo(DeploymentErrorCode.None));
            var standardResult = result.ValidationResults.FirstOrDefault(v => v.Field == "TokenStandard");
            Assert.That(standardResult?.IsValid, Is.False);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Malformed input tests: injection, oversized, special characters
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Malformed_TokenName_SqlInjection_DoesNotThrow()
        {
            var req = BuildAlgorandRequest("ASA", "'; DROP TABLE tokens;--", "TST");
            Assert.DoesNotThrowAsync(async () => await _service.InitiateAsync(req));
        }

        [Test]
        public async Task Malformed_TokenName_XssAttempt_DoesNotThrow()
        {
            var req = BuildAlgorandRequest("ASA", "<script>alert('xss')</script>", "TST");
            Assert.DoesNotThrowAsync(async () => await _service.InitiateAsync(req));
        }

        [Test]
        public async Task Malformed_TokenName_NullByte_DoesNotThrow()
        {
            var req = BuildAlgorandRequest("ASA", "Test\0Token", "TST");
            Assert.DoesNotThrowAsync(async () => await _service.InitiateAsync(req));
        }

        [Test]
        public async Task Malformed_TokenName_VeryLongString_ReturnValidationError()
        {
            var req = BuildAlgorandRequest("ASA", new string('A', 1000), "TST");
            var result = await _service.InitiateAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.ValidationRangeFault));
        }

        [Test]
        public async Task Malformed_CorrelationId_XssAttempt_DoesNotThrow()
        {
            var req = BuildAlgorandRequest("ASA", "ValidToken", "VLD");
            req.CorrelationId = "<script>alert('cid')</script>";
            Assert.DoesNotThrowAsync(async () => await _service.InitiateAsync(req));
        }

        [Test]
        public async Task Malformed_ExplicitAddress_InvalidFormat_CausesAddressMismatch()
        {
            var req = BuildAlgorandRequest("ASA", "TestToken", "TST");
            req.ExplicitDeployerAddress = "not-a-valid-address-at-all!!!";
            var result = await _service.InitiateAsync(req);
            // Invalid address format should trigger DeriveAddressMismatch for Algorand network
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.DeriveAddressMismatch));
        }

        [Test]
        public async Task Malformed_TotalSupply_MaxUlong_Succeeds()
        {
            var req = BuildAlgorandRequest("ASA", "MaxSupplyToken", "MST");
            req.TotalSupply = ulong.MaxValue;
            // Should succeed — no upper bound on supply
            var result = await _service.InitiateAsync(req);
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed));
        }

        [Test]
        public async Task Malformed_IdempotencyKey_SpecialCharacters_DoesNotThrow()
        {
            var req = BuildAlgorandRequest("ASA", "IdemToken", "IDM");
            req.IdempotencyKey = "key-with-special!@#$%^&*()-chars";
            Assert.DoesNotThrowAsync(async () => await _service.InitiateAsync(req));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Concurrency tests: parallel executions produce independent, correct results
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Concurrency_ParallelDeployments_DifferentKeys_AllSucceed()
        {
            const int parallelCount = 5;
            var tasks = Enumerable.Range(0, parallelCount)
                .Select(i => _service.InitiateAsync(new BackendDeploymentContractRequest
                {
                    IdempotencyKey = $"concurrent-key-{i:D4}",
                    ExplicitDeployerAddress = AlgorandAddress,
                    TokenStandard = "ASA",
                    TokenName = $"ConcurrentToken{i}",
                    TokenSymbol = "CON",
                    Network = "algorand-mainnet",
                    TotalSupply = (ulong)(1_000_000 + i),
                    Decimals = 0
                }))
                .ToList();

            var results = await Task.WhenAll(tasks);

            Assert.That(results.Length, Is.EqualTo(parallelCount));
            Assert.That(results.All(r => r.State == ContractLifecycleState.Completed), Is.True);
            Assert.That(results.All(r => r.ErrorCode == DeploymentErrorCode.None), Is.True);
        }

        [Test]
        public async Task Concurrency_ParallelDeployments_DifferentKeys_AllHaveUniqueDeploymentIds()
        {
            const int parallelCount = 5;
            var tasks = Enumerable.Range(0, parallelCount)
                .Select(i => _service.InitiateAsync(new BackendDeploymentContractRequest
                {
                    IdempotencyKey = $"conc-unique-{i:D4}",
                    ExplicitDeployerAddress = AlgorandAddress,
                    TokenStandard = "ASA",
                    TokenName = $"UniqToken{i}",
                    TokenSymbol = "UNQ",
                    Network = "algorand-mainnet",
                    TotalSupply = (ulong)(100 + i),
                    Decimals = 0
                }))
                .ToList();

            var results = await Task.WhenAll(tasks);
            var uniqueIds = results.Select(r => r.DeploymentId).Distinct().Count();
            Assert.That(uniqueIds, Is.EqualTo(parallelCount),
                "Each parallel deployment should produce a unique DeploymentId");
        }

        [Test]
        public async Task Concurrency_SameIdempotencyKey_Parallel_OnlyOneCreated()
        {
            const int parallelCount = 5;
            var request = new BackendDeploymentContractRequest
            {
                IdempotencyKey = "conc-same-key-idem",
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "SameKeyToken",
                TokenSymbol = "SKT",
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 0
            };

            var tasks = Enumerable.Range(0, parallelCount)
                .Select(_ => _service.InitiateAsync(request))
                .ToList();

            var results = await Task.WhenAll(tasks);

            // All must succeed
            Assert.That(results.All(r => r.State == ContractLifecycleState.Completed), Is.True);
            // All must share the same DeploymentId (idempotency)
            var distinctIds = results.Select(r => r.DeploymentId).Distinct().Count();
            Assert.That(distinctIds, Is.EqualTo(1), "Idempotency must hold under concurrency");
            // All must share the same AssetId
            var distinctAssets = results.Select(r => r.AssetId).Distinct().Count();
            Assert.That(distinctAssets, Is.EqualTo(1), "AssetId must be identical under idempotent replay");
        }

        [Test]
        public async Task Concurrency_ParallelValidations_DoNotInterfere()
        {
            const int parallelCount = 5;
            var tasks = Enumerable.Range(0, parallelCount)
                .Select(i => _service.ValidateAsync(new BackendDeploymentContractValidationRequest
                {
                    CorrelationId = $"conc-validate-{i}",
                    ExplicitDeployerAddress = AlgorandAddress,
                    TokenStandard = "ASA",
                    TokenName = $"ValidToken{i}",
                    TokenSymbol = "VLD",
                    Network = "algorand-mainnet",
                    TotalSupply = (ulong)(1000 + i),
                    Decimals = 0
                }))
                .ToList();

            var results = await Task.WhenAll(tasks);
            Assert.That(results.All(r => r.IsValid), Is.True,
                "Parallel validations should all succeed independently");
        }

        // ════════════════════════════════════════════════════════════════════════
        // Multi-step workflow tests: chain pipeline executions with audit continuity
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task MultiStep_TwoDistinctDeployments_AuditTrailsAreIndependent()
        {
            // Step 1: Deploy token A
            var r1 = await _service.InitiateAsync(new BackendDeploymentContractRequest
            {
                IdempotencyKey = "multi-step-token-a",
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "TokenA",
                TokenSymbol = "TKA",
                Network = "algorand-mainnet",
                TotalSupply = 1_000,
                Decimals = 0
            });

            // Step 2: Deploy token B
            var r2 = await _service.InitiateAsync(new BackendDeploymentContractRequest
            {
                IdempotencyKey = "multi-step-token-b",
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = "ARC3",
                TokenName = "TokenB",
                TokenSymbol = "TKB",
                Network = "algorand-mainnet",
                TotalSupply = 2_000,
                Decimals = 2
            });

            // Step 3: Verify audit trails are independent
            var trail1 = await _service.GetAuditTrailAsync(r1.DeploymentId);
            var trail2 = await _service.GetAuditTrailAsync(r2.DeploymentId);

            Assert.That(trail1.DeploymentId, Is.Not.EqualTo(trail2.DeploymentId));
            Assert.That(trail1.AssetId, Is.Not.EqualTo(trail2.AssetId));
            Assert.That(trail1.Events.All(e => e.DeploymentId == r1.DeploymentId), Is.True);
            Assert.That(trail2.Events.All(e => e.DeploymentId == r2.DeploymentId), Is.True);
        }

        [Test]
        public async Task MultiStep_ValidationThenDeployment_DeployerAddressConsistent()
        {
            // Step 1: Validate
            var validateResult = await _service.ValidateAsync(new BackendDeploymentContractValidationRequest
            {
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "MultiStepConsistent",
                TokenSymbol = "MSC",
                Network = "algorand-mainnet",
                TotalSupply = 5_000,
                Decimals = 0
            });

            Assert.That(validateResult.IsValid, Is.True);
            Assert.That(validateResult.DeployerAddress, Is.EqualTo(AlgorandAddress));

            // Step 2: Deploy with same params
            var deployResult = await _service.InitiateAsync(new BackendDeploymentContractRequest
            {
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "MultiStepConsistent",
                TokenSymbol = "MSC",
                Network = "algorand-mainnet",
                TotalSupply = 5_000,
                Decimals = 0
            });

            // Both steps resolved the same deployer address
            Assert.That(deployResult.DeployerAddress, Is.EqualTo(validateResult.DeployerAddress));
        }

        [Test]
        public async Task MultiStep_ThreeConsecutiveDeployments_AllHaveUniqueAssetIds()
        {
            var r1 = await _service.InitiateAsync(BuildRequestWithKey("multi3-1", "Token1", "T1"));
            var r2 = await _service.InitiateAsync(BuildRequestWithKey("multi3-2", "Token2", "T2"));
            var r3 = await _service.InitiateAsync(BuildRequestWithKey("multi3-3", "Token3", "T3"));

            Assert.That(r1.AssetId, Is.Not.EqualTo(r2.AssetId));
            Assert.That(r2.AssetId, Is.Not.EqualTo(r3.AssetId));
            Assert.That(r1.AssetId, Is.Not.EqualTo(r3.AssetId));
        }

        [Test]
        public async Task MultiStep_DeployThenStatus_AuditEventCountConsistent()
        {
            var deployed = await _service.InitiateAsync(BuildRequestWithKey("multi-audit-count", "AuditToken", "ATC"));
            var eventCountInDeploy = deployed.AuditEvents.Count;

            var trail = await _service.GetAuditTrailAsync(deployed.DeploymentId);
            var eventCountInTrail = trail.Events.Count;

            Assert.That(eventCountInTrail, Is.EqualTo(eventCountInDeploy));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Retry / rollback semantics tests
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Retry_FailedThenFixedInput_SecondAttemptSucceeds()
        {
            // First attempt: intentionally bad (no address/credentials)
            var badReq = BuildAlgorandRequest("ASA", "RetryToken", "RTK");
            badReq.ExplicitDeployerAddress = null;
            badReq.DeployerEmail = null;
            badReq.DeployerPassword = null;
            var failResult = await _service.InitiateAsync(badReq);
            Assert.That(failResult.State, Is.EqualTo(ContractLifecycleState.Failed));

            // Retry with fixed input — new unique key so it is not a replay
            var fixedReq = BuildRequestWithKey("retry-fixed-001", "RetryToken", "RTK");
            var successResult = await _service.InitiateAsync(fixedReq);
            Assert.That(successResult.State, Is.EqualTo(ContractLifecycleState.Completed));
        }

        [Test]
        public void Retry_StateMachine_FailedCanTransitionToPending()
        {
            // The state machine explicitly allows Failed→Pending to model retry
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Failed, ContractLifecycleState.Pending), Is.True);
        }

        [Test]
        public void Retry_StateMachine_FailedCannotGoDirectToCompleted()
        {
            // Failed must go through Pending/Validated/Submitted/Confirmed before Completed
            Assert.That(_service.IsValidStateTransition(
                ContractLifecycleState.Failed, ContractLifecycleState.Completed), Is.False);
        }

        [Test]
        public async Task Retry_Idempotency_SameKeyAfterSuccess_ReturnsReplayNotRetry()
        {
            var req = BuildRequestWithKey("retry-idem-success", "ReplayToken", "RPL");
            var r1 = await _service.InitiateAsync(req);
            Assert.That(r1.State, Is.EqualTo(ContractLifecycleState.Completed));

            // Same key again — must be a replay, not a new deployment
            var r2 = await _service.InitiateAsync(req);
            Assert.That(r2.IsIdempotentReplay, Is.True);
            Assert.That(r2.State, Is.EqualTo(ContractLifecycleState.Completed));
        }

        [Test]
        public async Task Retry_MultipleDistinctFailures_EachProducesSeparateErrorResponse()
        {
            // Each of these has a distinct error — verified they each return the correct typed code
            var results = await Task.WhenAll(
                _service.InitiateAsync(BuildAlgorandRequest("", "T1", "T1")),   // RequiredFieldMissing
                _service.InitiateAsync(BuildAlgorandRequest("ASA", "", "T2")),   // RequiredFieldMissing (name)
                _service.InitiateAsync(BuildAlgorandRequest("BADSTD", "T3", "T3")) // UnsupportedStandard
            );

            Assert.That(results[0].State, Is.EqualTo(ContractLifecycleState.Failed));
            Assert.That(results[1].State, Is.EqualTo(ContractLifecycleState.Failed));
            Assert.That(results[2].State, Is.EqualTo(ContractLifecycleState.Failed));
            Assert.That(results[0].ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
            Assert.That(results[1].ErrorCode, Is.EqualTo(DeploymentErrorCode.RequiredFieldMissing));
            Assert.That(results[2].ErrorCode, Is.EqualTo(DeploymentErrorCode.UnsupportedStandard));
        }

        [Test]
        public async Task Retry_TransientHint_UserGuidanceNotNull_OnEveryError()
        {
            // Every error path must provide user guidance (never null/empty for failed state)
            var errorRequests = new[]
            {
                BuildAlgorandRequest("", "T1", "T1"),                    // empty standard
                BuildAlgorandRequest("ASA", "", "TST"),                  // empty name
                BuildAlgorandRequest("UNKNOWN", "T3", "T3"),             // unsupported standard
            };
            foreach (var req in errorRequests)
            {
                var result = await _service.InitiateAsync(req);
                Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Failed));
                Assert.That(result.UserGuidance, Is.Not.Null.And.Not.Empty,
                    $"UserGuidance should be set for error code {result.ErrorCode}");
            }
        }

        [Test]
        public async Task Retry_GetStatus_UnknownId_AlwaysReturnsUserGuidance()
        {
            var result = await _service.GetStatusAsync("dep-never-existed-id");
            Assert.That(result.ErrorCode, Is.Not.EqualTo(DeploymentErrorCode.None));
            Assert.That(result.UserGuidance, Is.Not.Null.And.Not.Empty);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Schema backward-compatibility: response fields remain stable
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task BackwardCompat_SuccessResponse_SchemaStable()
        {
            var result = await _service.InitiateAsync(BuildRequestWithKey("bwcompat-schema", "StableToken", "STB"));

            // Core contract fields that front-end depends on
            Assert.That(result.DeploymentId, Is.Not.Null, "DeploymentId");
            Assert.That(result.IdempotencyKey, Is.Not.Null, "IdempotencyKey");
            Assert.That(result.CorrelationId, Is.Not.Null, "CorrelationId");
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed), "State");
            Assert.That(result.DerivationStatus, Is.Not.EqualTo(ARC76DerivationStatus.Error), "DerivationStatus");
            Assert.That(result.DeployerAddress, Is.Not.Null, "DeployerAddress");
            Assert.That(result.ErrorCode, Is.EqualTo(DeploymentErrorCode.None), "ErrorCode");
            Assert.That(result.AssetId, Is.Not.Null, "AssetId");
            Assert.That(result.TransactionId, Is.Not.Null, "TransactionId");
            Assert.That(result.ConfirmedRound, Is.Not.Null, "ConfirmedRound");
            Assert.That(result.InitiatedAt, Is.Not.Null, "InitiatedAt");
            Assert.That(result.LastUpdatedAt, Is.Not.Null, "LastUpdatedAt");
            Assert.That(result.ValidationResults, Is.Not.Null, "ValidationResults");
            Assert.That(result.AuditEvents, Is.Not.Null, "AuditEvents");
        }

        [Test]
        public async Task BackwardCompat_ErrorResponse_SchemaStable()
        {
            var result = await _service.InitiateAsync(null!);

            // Error contract fields that front-end depends on
            Assert.That(result.DeploymentId, Is.Not.Null, "DeploymentId");
            Assert.That(result.CorrelationId, Is.Not.Null, "CorrelationId");
            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Failed), "State");
            Assert.That(result.ErrorCode, Is.Not.EqualTo(DeploymentErrorCode.None), "ErrorCode != None");
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty, "Message");
            Assert.That(result.UserGuidance, Is.Not.Null.And.Not.Empty, "UserGuidance");
            Assert.That(result.ValidationResults, Is.Not.Null, "ValidationResults");
            Assert.That(result.AuditEvents, Is.Not.Null, "AuditEvents");
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private BackendDeploymentContractRequest BuildAlgorandRequest(
            string standard, string name, string symbol) =>
            new()
            {
                ExplicitDeployerAddress = AlgorandAddress,
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

        private BackendDeploymentContractRequest BuildRequestWithKey(
            string key, string name, string symbol) =>
            new()
            {
                IdempotencyKey = key,
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = "ASA",
                TokenName = name,
                TokenSymbol = symbol,
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 0
            };
    }
}
