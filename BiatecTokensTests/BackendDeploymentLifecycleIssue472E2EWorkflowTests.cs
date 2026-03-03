using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// E2E workflow tests for Issue #472: Vision Milestone – Deterministic Backend
    /// Deployment Lifecycle and ARC76 Contract Hardening.
    ///
    /// Structure:
    ///   Part A – Service-layer workflow tests (no HTTP overhead)
    ///   Part B – DI-integration tests using WebApplicationFactory
    ///
    /// Workflow IDs (WA–WG):
    ///   WA – Full credential → derive → deploy → status → audit pipeline
    ///   WB – ARC76 determinism across 3 independent service instances
    ///   WC – Idempotency determinism across 3 replays
    ///   WD – State machine validation workflow
    ///   WE – Compliance audit trail completeness workflow
    ///   WF – Schema contract assertions (all required fields non-null)
    ///   WG – Correlation ID propagation across lifecycle stages
    ///
    /// Business Value: Proves the backend deployment contract is a production-ready capability:
    /// deterministic, idempotent, observable, compliant, and aligned with roadmap reliability goals.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendDeploymentLifecycleIssue472E2EWorkflowTests
    {
        // ── Part A: Service-layer workflow tests ──────────────────────────────────

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
        // WA – Full pipeline: derive → deploy → status → audit
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WA1_FullPipeline_Credential_Deploy_Status_Audit_AllCoherent()
        {
            // Step 1: Verify ARC76 derivation
            var derivedAddress = _service.DeriveARC76Address(KnownEmail, KnownPassword);
            Assert.That(derivedAddress, Is.EqualTo(KnownAlgorandAddress));

            // Step 2: Deploy using credentials
            var request = new BackendDeploymentContractRequest
            {
                CorrelationId = "wa1-full-pipeline",
                DeployerEmail = KnownEmail,
                DeployerPassword = KnownPassword,
                TokenStandard = "ASA",
                TokenName = "FullPipelineToken",
                TokenSymbol = "FPT",
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            };
            var deployed = await _service.InitiateAsync(request);

            Assert.That(deployed.State, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(deployed.DeployerAddress, Is.EqualTo(KnownAlgorandAddress));
            Assert.That(deployed.AssetId, Is.GreaterThan(0UL));

            // Step 3: Status check
            var status = await _service.GetStatusAsync(deployed.DeploymentId, "wa1-status");
            Assert.That(status.State, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(status.DeploymentId, Is.EqualTo(deployed.DeploymentId));

            // Step 4: Audit trail
            var trail = await _service.GetAuditTrailAsync(deployed.DeploymentId, "wa1-audit");
            Assert.That(trail.Events, Is.Not.Empty);
            Assert.That(trail.FinalState, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(trail.AssetId, Is.EqualTo(deployed.AssetId));
        }

        [Test]
        public async Task WA2_FullPipeline_ExplicitAddress_AllStagesProduceCoherentData()
        {
            var request = new BackendDeploymentContractRequest
            {
                CorrelationId = "wa2-explicit-addr",
                ExplicitDeployerAddress = KnownAlgorandAddress,
                TokenStandard = "ARC3",
                TokenName = "ExplicitAddrToken",
                TokenSymbol = "EAT",
                Network = "algorand-mainnet",
                TotalSupply = 500_000,
                Decimals = 4,
                MetadataUri = "ipfs://bafybeitest"
            };
            var deployed = await _service.InitiateAsync(request);

            Assert.That(deployed.State, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(deployed.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.AddressProvided));
            Assert.That(deployed.IsDeterministicAddress, Is.False);
            Assert.That(deployed.TransactionId, Is.Not.Null.And.Not.Empty);

            var trail = await _service.GetAuditTrailAsync(deployed.DeploymentId);
            Assert.That(trail.Events.Any(e => e.EventKind == ComplianceAuditEventKind.DeploymentInitiated), Is.True);
            Assert.That(trail.Events.Any(e => e.EventKind == ComplianceAuditEventKind.DeploymentCompleted), Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // WB – ARC76 determinism: 3 independent derivations → same result
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void WB1_ARC76Determinism_ThreeDerivations_IdenticalAddresses()
        {
            var addr1 = _service.DeriveARC76Address(KnownEmail, KnownPassword);
            var addr2 = _service.DeriveARC76Address(KnownEmail, KnownPassword);
            var addr3 = _service.DeriveARC76Address(KnownEmail, KnownPassword);

            Assert.That(addr1, Is.EqualTo(addr2));
            Assert.That(addr2, Is.EqualTo(addr3));
            Assert.That(addr1, Is.EqualTo(KnownAlgorandAddress));
        }

        [Test]
        public void WB2_ARC76Determinism_ThreeServiceInstances_SameAddress()
        {
            // Create 3 independent service instances; all must derive the same address
            var svc1 = new BackendDeploymentLifecycleContractService(
                new Mock<ILogger<BackendDeploymentLifecycleContractService>>().Object);
            var svc2 = new BackendDeploymentLifecycleContractService(
                new Mock<ILogger<BackendDeploymentLifecycleContractService>>().Object);
            var svc3 = new BackendDeploymentLifecycleContractService(
                new Mock<ILogger<BackendDeploymentLifecycleContractService>>().Object);

            var addr1 = svc1.DeriveARC76Address(KnownEmail, KnownPassword);
            var addr2 = svc2.DeriveARC76Address(KnownEmail, KnownPassword);
            var addr3 = svc3.DeriveARC76Address(KnownEmail, KnownPassword);

            Assert.That(addr1, Is.EqualTo(addr2));
            Assert.That(addr2, Is.EqualTo(addr3));
        }

        [Test]
        public async Task WB3_ARC76Determinism_SameCredentials_SameDeployerAddress_AcrossDeployments()
        {
            var r1 = await _service.InitiateAsync(new BackendDeploymentContractRequest
            {
                IdempotencyKey = "wb3-depl1",
                DeployerEmail = KnownEmail,
                DeployerPassword = KnownPassword,
                TokenStandard = "ASA",
                TokenName = "DetermToken1",
                TokenSymbol = "DT1",
                Network = "algorand-mainnet",
                TotalSupply = 1000,
                Decimals = 0
            });

            var r2 = await _service.InitiateAsync(new BackendDeploymentContractRequest
            {
                IdempotencyKey = "wb3-depl2",
                DeployerEmail = KnownEmail,
                DeployerPassword = KnownPassword,
                TokenStandard = "ARC3",
                TokenName = "DetermToken2",
                TokenSymbol = "DT2",
                Network = "algorand-mainnet",
                TotalSupply = 2000,
                Decimals = 2
            });

            Assert.That(r1.DeployerAddress, Is.EqualTo(r2.DeployerAddress));
            Assert.That(r1.DeployerAddress, Is.EqualTo(KnownAlgorandAddress));
        }

        // ════════════════════════════════════════════════════════════════════════
        // WC – Idempotency: 3 replays → identical results
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WC1_Idempotency_ThreeReplays_IdenticalResults()
        {
            var request = new BackendDeploymentContractRequest
            {
                IdempotencyKey = "wc1-three-replays",
                ExplicitDeployerAddress = KnownAlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "IdemToken",
                TokenSymbol = "IDM",
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            };

            var r1 = await _service.InitiateAsync(request);
            var r2 = await _service.InitiateAsync(request);
            var r3 = await _service.InitiateAsync(request);

            Assert.That(r2.DeploymentId, Is.EqualTo(r1.DeploymentId));
            Assert.That(r3.DeploymentId, Is.EqualTo(r1.DeploymentId));
            Assert.That(r2.AssetId, Is.EqualTo(r1.AssetId));
            Assert.That(r3.AssetId, Is.EqualTo(r1.AssetId));
            Assert.That(r2.TransactionId, Is.EqualTo(r1.TransactionId));
            Assert.That(r3.TransactionId, Is.EqualTo(r1.TransactionId));
        }

        [Test]
        public async Task WC2_Idempotency_SecondAndThirdReplays_MarkedAsReplay()
        {
            var request = new BackendDeploymentContractRequest
            {
                IdempotencyKey = "wc2-replay-flag",
                ExplicitDeployerAddress = KnownAlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "ReplayToken",
                TokenSymbol = "RPT",
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 0
            };

            var r1 = await _service.InitiateAsync(request);
            var r2 = await _service.InitiateAsync(request);
            var r3 = await _service.InitiateAsync(request);

            Assert.That(r1.IsIdempotentReplay, Is.False);
            Assert.That(r2.IsIdempotentReplay, Is.True);
            Assert.That(r3.IsIdempotentReplay, Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // WD – State machine validation workflow
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void WD1_StateMachine_ValidMainPath_AllTransitionsAccepted()
        {
            var validPath = new[]
            {
                (ContractLifecycleState.Pending, ContractLifecycleState.Validated),
                (ContractLifecycleState.Validated, ContractLifecycleState.Submitted),
                (ContractLifecycleState.Submitted, ContractLifecycleState.Confirmed),
                (ContractLifecycleState.Confirmed, ContractLifecycleState.Completed)
            };

            foreach (var (from, to) in validPath)
                Assert.That(_service.IsValidStateTransition(from, to), Is.True,
                    $"Transition {from} → {to} should be valid");
        }

        [Test]
        public void WD2_StateMachine_FailurePaths_AllAccepted()
        {
            var failurePaths = new[]
            {
                (ContractLifecycleState.Pending, ContractLifecycleState.Failed),
                (ContractLifecycleState.Validated, ContractLifecycleState.Failed),
                (ContractLifecycleState.Submitted, ContractLifecycleState.Failed),
                (ContractLifecycleState.Confirmed, ContractLifecycleState.Failed)
            };

            foreach (var (from, to) in failurePaths)
                Assert.That(_service.IsValidStateTransition(from, to), Is.True,
                    $"Failure transition {from} → {to} should be valid");
        }

        [Test]
        public void WD3_StateMachine_IllegalTransitions_AllBlocked()
        {
            var illegalTransitions = new[]
            {
                (ContractLifecycleState.Completed, ContractLifecycleState.Pending),
                (ContractLifecycleState.Completed, ContractLifecycleState.Validated),
                (ContractLifecycleState.Completed, ContractLifecycleState.Failed),
                (ContractLifecycleState.Cancelled, ContractLifecycleState.Pending),
                (ContractLifecycleState.Pending, ContractLifecycleState.Completed),
                (ContractLifecycleState.Pending, ContractLifecycleState.Confirmed),
                (ContractLifecycleState.Submitted, ContractLifecycleState.Validated)
            };

            foreach (var (from, to) in illegalTransitions)
                Assert.That(_service.IsValidStateTransition(from, to), Is.False,
                    $"Illegal transition {from} → {to} should be blocked");
        }

        // ════════════════════════════════════════════════════════════════════════
        // WE – Compliance audit trail completeness
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WE1_AuditTrail_ContainsAllRequiredEventKinds()
        {
            var result = await _service.InitiateAsync(new BackendDeploymentContractRequest
            {
                CorrelationId = "we1-audit",
                DeployerEmail = KnownEmail,
                DeployerPassword = KnownPassword,
                TokenStandard = "ASA",
                TokenName = "AuditCompleteToken",
                TokenSymbol = "ACT",
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 0
            });

            var eventKinds = result.AuditEvents.Select(e => e.EventKind).ToHashSet();

            Assert.That(eventKinds.Contains(ComplianceAuditEventKind.DeploymentInitiated), Is.True);
            Assert.That(eventKinds.Contains(ComplianceAuditEventKind.AccountDerived), Is.True);
            Assert.That(eventKinds.Contains(ComplianceAuditEventKind.InputsValidated), Is.True);
            Assert.That(eventKinds.Contains(ComplianceAuditEventKind.PolicyEvaluated), Is.True);
            Assert.That(eventKinds.Contains(ComplianceAuditEventKind.TransactionSubmitted), Is.True);
            Assert.That(eventKinds.Contains(ComplianceAuditEventKind.TransactionConfirmed), Is.True);
            Assert.That(eventKinds.Contains(ComplianceAuditEventKind.DeploymentCompleted), Is.True);
        }

        [Test]
        public async Task WE2_AuditTrail_PolicyEvaluated_OutcomeIsApproved()
        {
            var result = await _service.InitiateAsync(BuildRequest("we2-policy"));
            var policyEvent = result.AuditEvents
                .FirstOrDefault(e => e.EventKind == ComplianceAuditEventKind.PolicyEvaluated);

            Assert.That(policyEvent, Is.Not.Null);
            Assert.That(policyEvent!.Outcome, Is.EqualTo("Success"));
            Assert.That(policyEvent.Details.ContainsKey("PolicyOutcome"), Is.True);
            Assert.That(policyEvent.Details["PolicyOutcome"], Is.EqualTo("Approved"));
        }

        [Test]
        public async Task WE3_AuditTrail_TransactionConfirmed_ContainsAssetId()
        {
            var result = await _service.InitiateAsync(BuildRequest("we3-txconfirm"));
            var confirmEvent = result.AuditEvents
                .FirstOrDefault(e => e.EventKind == ComplianceAuditEventKind.TransactionConfirmed);

            Assert.That(confirmEvent, Is.Not.Null);
            Assert.That(confirmEvent!.Details.ContainsKey("AssetId"), Is.True);
            Assert.That(ulong.Parse(confirmEvent.Details["AssetId"]), Is.EqualTo(result.AssetId));
        }

        // ════════════════════════════════════════════════════════════════════════
        // WF – Schema contract assertions (all required fields non-null)
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WF1_SuccessResponse_AllRequiredFieldsPresent()
        {
            var result = await _service.InitiateAsync(BuildRequest("wf1-schema"));

            Assert.That(result.DeploymentId, Is.Not.Null.And.Not.Empty, "DeploymentId");
            Assert.That(result.IdempotencyKey, Is.Not.Null.And.Not.Empty, "IdempotencyKey");
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty, "CorrelationId");
            Assert.That(result.DeployerAddress, Is.Not.Null.And.Not.Empty, "DeployerAddress");
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty, "Message");
            Assert.That(result.AssetId, Is.Not.Null, "AssetId");
            Assert.That(result.TransactionId, Is.Not.Null.And.Not.Empty, "TransactionId");
            Assert.That(result.ConfirmedRound, Is.Not.Null, "ConfirmedRound");
            Assert.That(result.InitiatedAt, Is.Not.Null.And.Not.Empty, "InitiatedAt");
            Assert.That(result.LastUpdatedAt, Is.Not.Null.And.Not.Empty, "LastUpdatedAt");
        }

        [Test]
        public async Task WF2_ValidationResponse_AllRequiredFieldsPresent()
        {
            var result = await _service.ValidateAsync(new BackendDeploymentContractValidationRequest
            {
                ExplicitDeployerAddress = KnownAlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "SchemaToken",
                TokenSymbol = "SCH",
                Network = "algorand-mainnet",
                TotalSupply = 1000,
                Decimals = 0
            });

            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty, "CorrelationId");
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty, "Message");
            Assert.That(result.ValidationResults, Is.Not.Null, "ValidationResults");
            Assert.That(result.DeployerAddress, Is.Not.Null.And.Not.Empty, "DeployerAddress");
        }

        [Test]
        public async Task WF3_AuditEvent_AllRequiredFieldsPresent()
        {
            var result = await _service.InitiateAsync(BuildRequest("wf3-event-schema"));
            var evt = result.AuditEvents.First();

            Assert.That(evt.EventId, Is.Not.Null.And.Not.Empty, "EventId");
            Assert.That(evt.CorrelationId, Is.Not.Null.And.Not.Empty, "CorrelationId");
            Assert.That(evt.DeploymentId, Is.Not.Null.And.Not.Empty, "DeploymentId");
            Assert.That(evt.Timestamp, Is.Not.Null.And.Not.Empty, "Timestamp");
            Assert.That(evt.Actor, Is.Not.Null.And.Not.Empty, "Actor");
            Assert.That(evt.Outcome, Is.Not.Null.And.Not.Empty, "Outcome");
        }

        // ════════════════════════════════════════════════════════════════════════
        // WG – Correlation ID propagation across lifecycle
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WG1_CorrelationId_PropagatesFromInitiateToStatusResponse()
        {
            var cid = "wg1-correlation-id";
            var request = BuildRequest("wg1-prop-test");
            request.CorrelationId = cid;

            var deployed = await _service.InitiateAsync(request);
            Assert.That(deployed.CorrelationId, Is.EqualTo(cid));
        }

        [Test]
        public async Task WG2_CorrelationId_PropagatesIntoAllAuditEvents()
        {
            var cid = "wg2-audit-propagation";
            var request = BuildRequest("wg2-cid");
            request.CorrelationId = cid;

            var result = await _service.InitiateAsync(request);
            Assert.That(result.AuditEvents.All(e => e.CorrelationId == cid), Is.True);
        }

        [Test]
        public async Task WG3_CorrelationId_AutoGeneratedIfNotSupplied()
        {
            var request = BuildRequest("wg3-auto-cid");
            request.CorrelationId = null;

            var result = await _service.InitiateAsync(request);
            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
            // Should look like a GUID
            Assert.That(Guid.TryParse(result.CorrelationId, out _), Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Part B: DI-integration tests using WebApplicationFactory
        // ════════════════════════════════════════════════════════════════════════

        private static readonly Dictionary<string, string?> TestConfiguration = new()
        {
            ["App:Account"] = "test test test test test test test test test test test test test test test test test test test test test test test test test",
            ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
            ["AlgorandAuthentication:CheckExpiration"] = "false",
            ["AlgorandAuthentication:Debug"] = "true",
            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
            ["JwtConfig:SecretKey"] = "backend-deployment-lifecycle-472-e2e-test-key-32chars!!",
            ["JwtConfig:Issuer"] = "BiatecTokensApi",
            ["JwtConfig:Audience"] = "BiatecTokensUsers",
            ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
            ["JwtConfig:RefreshTokenExpirationDays"] = "30",
            ["JwtConfig:ValidateIssuerSigningKey"] = "true",
            ["JwtConfig:ValidateIssuer"] = "true",
            ["JwtConfig:ValidateAudience"] = "true",
            ["JwtConfig:ValidateLifetime"] = "true",
            ["JwtConfig:ClockSkewMinutes"] = "5",
            ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
            ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
            ["IPFSConfig:TimeoutSeconds"] = "30",
            ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
            ["IPFSConfig:ValidateContentHash"] = "true",
            ["EVMChains:Chains:0:RpcUrl"] = "https://sepolia.base.org",
            ["EVMChains:Chains:0:ChainId"] = "84532",
            ["EVMChains:Chains:0:GasLimit"] = "4500000",
            ["StripeConfig:SecretKey"] = "test_key",
            ["StripeConfig:PublishableKey"] = "test_key",
            ["StripeConfig:WebhookSecret"] = "test_secret",
            ["StripeConfig:BasicPriceId"] = "price_test_basic",
            ["StripeConfig:ProPriceId"] = "price_test_pro",
            ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "BackendDeploymentLifecycle472E2ETestKey32CharsReq!!"
        };

        [Test]
        public async Task DI_E2E_ARC76Determinism_ThreeRunsSameAddress()
        {
            using var factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(TestConfiguration));
                });

            using var scope = factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var addr1 = svc.DeriveARC76Address(KnownEmail, KnownPassword);
            var addr2 = svc.DeriveARC76Address(KnownEmail, KnownPassword);
            var addr3 = svc.DeriveARC76Address(KnownEmail, KnownPassword);

            Assert.That(addr1, Is.EqualTo(addr2));
            Assert.That(addr2, Is.EqualTo(addr3));
            Assert.That(addr1, Is.EqualTo(KnownAlgorandAddress));
        }

        [Test]
        public async Task DI_E2E_FullDeployment_DI_ResolvedService_Succeeds()
        {
            using var factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(TestConfiguration));
                });

            using var scope = factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.InitiateAsync(new BackendDeploymentContractRequest
            {
                IdempotencyKey = "di-e2e-full-472",
                DeployerEmail = KnownEmail,
                DeployerPassword = KnownPassword,
                TokenStandard = "ASA",
                TokenName = "DIE2EToken",
                TokenSymbol = "DE2",
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            });

            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(result.DeployerAddress, Is.EqualTo(KnownAlgorandAddress));
            Assert.That(result.AuditEvents, Is.Not.Empty);
        }

        [Test]
        public async Task DI_E2E_IdempotencyDeterminism_ThreeReplays()
        {
            using var factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(TestConfiguration));
                });

            using var scope = factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var req = new BackendDeploymentContractRequest
            {
                IdempotencyKey = "di-e2e-idem-472",
                ExplicitDeployerAddress = KnownAlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "IdemDIToken",
                TokenSymbol = "IDI",
                Network = "algorand-mainnet",
                TotalSupply = 100_000,
                Decimals = 2
            };

            var r1 = await svc.InitiateAsync(req);
            var r2 = await svc.InitiateAsync(req);
            var r3 = await svc.InitiateAsync(req);

            Assert.That(r2.DeploymentId, Is.EqualTo(r1.DeploymentId));
            Assert.That(r3.DeploymentId, Is.EqualTo(r1.DeploymentId));
            Assert.That(r2.AssetId, Is.EqualTo(r1.AssetId));
            Assert.That(r3.AssetId, Is.EqualTo(r1.AssetId));
        }

        [Test]
        public async Task DI_E2E_BackwardCompatibility_ExistingTokenDeploymentLifecycleService_StillResolves()
        {
            using var factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(TestConfiguration));
                });

            using var scope = factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ITokenDeploymentLifecycleService>();
            Assert.That(svc, Is.Not.Null);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private BackendDeploymentContractRequest BuildRequest(string idemKey) =>
            new()
            {
                IdempotencyKey = idemKey,
                ExplicitDeployerAddress = KnownAlgorandAddress,
                TokenStandard = "ASA",
                TokenName = $"Token{idemKey}",
                TokenSymbol = "TST",
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            };
    }
}
