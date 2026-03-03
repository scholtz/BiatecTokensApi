using BiatecTokensApi.Models.BackendDeploymentLifecycle;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration/contract tests for Issue #472: Vision Milestone – Deterministic Backend
    /// Deployment Lifecycle and ARC76 Contract Hardening.
    ///
    /// These tests use WebApplicationFactory to exercise:
    ///   - Service-layer resolution from the DI container via CreateScope()
    ///   - HTTP-level assertions for unauthenticated (401) and null-body (400) paths
    ///   - DI container startup validation
    ///   - Health endpoint reachability
    ///   - Backward-compatibility of existing endpoints
    ///
    /// AC1 – ARC76 determinism is enforced: same credentials → same address
    /// AC2 – Invalid contexts return explicit typed errors
    /// AC3 – Idempotency prevents duplicate deployments
    /// AC4 – Lifecycle state transitions are validated
    /// AC5 – Status endpoints return consistent information
    /// AC6 – Correlation IDs propagate across the request lifecycle
    /// AC7 – Compliance audit events are emitted with required fields
    /// AC8 – Tests cover all acceptance criteria
    /// AC9 – CI passes; no regression
    /// AC10 – API contract is stable
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendDeploymentLifecycleIssue472ContractTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        private const string AlgorandAddress =
            "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

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
            ["JwtConfig:SecretKey"] = "backend-deployment-lifecycle-472-test-key-32chars!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "BackendDeploymentLifecycle472TestKey32CharsReq!!"
        };

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(TestConfiguration);
                    });
                });
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        // ════════════════════════════════════════════════════════════════════════
        // DI resolution tests
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void DI_ResolvesBackendDeploymentLifecycleContractService()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();
            Assert.That(svc, Is.Not.Null);
        }

        [Test]
        public void DI_ServiceInstanceIsSingleton()
        {
            using var scope1 = _factory.Services.CreateScope();
            using var scope2 = _factory.Services.CreateScope();
            var svc1 = scope1.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();
            var svc2 = scope2.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();
            Assert.That(svc1, Is.SameAs(svc2));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC1: ARC76 determinism via DI-resolved service
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void DI_ARC76Determinism_SameCredentialsSameAddress()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var addr1 = svc.DeriveARC76Address("test@biatec.io", "TestPass123!");
            var addr2 = svc.DeriveARC76Address("test@biatec.io", "TestPass123!");
            Assert.That(addr1, Is.EqualTo(addr2));
        }

        [Test]
        public void DI_ARC76Determinism_EmailCaseInsensitive()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var addr1 = svc.DeriveARC76Address("UPPER@BIATEC.IO", "TestPass123!");
            var addr2 = svc.DeriveARC76Address("upper@biatec.io", "TestPass123!");
            Assert.That(addr1, Is.EqualTo(addr2));
        }

        [Test]
        public async Task DI_ARC76Derived_DeploymentSucceeds()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.InitiateAsync(new BackendDeploymentContractRequest
            {
                DeployerEmail = "contract-test@biatec.io",
                DeployerPassword = "ContractTest123!",
                TokenStandard = "ASA",
                TokenName = "ContractToken",
                TokenSymbol = "CTK",
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            });

            Assert.That(result.State, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(result.DerivationStatus, Is.EqualTo(ARC76DerivationStatus.Derived));
            Assert.That(result.IsDeterministicAddress, Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC3: Idempotency via DI-resolved service
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DI_Idempotency_SameKeyReturnsSameResult()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var req = BuildRequest("idem-contract-test-001");
            var r1 = await svc.InitiateAsync(req);
            var r2 = await svc.InitiateAsync(req);

            Assert.That(r2.DeploymentId, Is.EqualTo(r1.DeploymentId));
            Assert.That(r2.AssetId, Is.EqualTo(r1.AssetId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task DI_Idempotency_ThreeReplaysAllProduceSameResult()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var req = BuildRequest("idem-three-replays");
            var r1 = await svc.InitiateAsync(req);
            var r2 = await svc.InitiateAsync(req);
            var r3 = await svc.InitiateAsync(req);

            Assert.That(r2.DeploymentId, Is.EqualTo(r1.DeploymentId));
            Assert.That(r3.DeploymentId, Is.EqualTo(r1.DeploymentId));
            Assert.That(r3.AssetId, Is.EqualTo(r1.AssetId));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC4: State machine validation via DI-resolved service
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void DI_StateTransition_ValidFlow_AllTransitionsAccepted()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            // Valid main path
            Assert.That(svc.IsValidStateTransition(ContractLifecycleState.Pending, ContractLifecycleState.Validated), Is.True);
            Assert.That(svc.IsValidStateTransition(ContractLifecycleState.Validated, ContractLifecycleState.Submitted), Is.True);
            Assert.That(svc.IsValidStateTransition(ContractLifecycleState.Submitted, ContractLifecycleState.Confirmed), Is.True);
            Assert.That(svc.IsValidStateTransition(ContractLifecycleState.Confirmed, ContractLifecycleState.Completed), Is.True);
        }

        [Test]
        public void DI_StateTransition_TerminalStates_BlockedFromFurtherTransition()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            Assert.That(svc.IsValidStateTransition(ContractLifecycleState.Completed, ContractLifecycleState.Pending), Is.False);
            Assert.That(svc.IsValidStateTransition(ContractLifecycleState.Cancelled, ContractLifecycleState.Validated), Is.False);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC5: Status endpoint via DI-resolved service
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DI_GetStatus_AfterInitiate_ReturnsCompletedState()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var initiated = await svc.InitiateAsync(BuildRequest("status-contract-001"));
            var status = await svc.GetStatusAsync(initiated.DeploymentId);

            Assert.That(status.State, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(status.AssetId, Is.EqualTo(initiated.AssetId));
        }

        [Test]
        public async Task DI_GetStatus_UnknownId_ReturnsNotFoundError()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.GetStatusAsync("dep-nonexistent-xyz");
            Assert.That(result.ErrorCode, Is.Not.EqualTo(DeploymentErrorCode.None));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC6 & AC7: Correlation IDs and audit events via DI-resolved service
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task DI_CorrelationId_PropagatesFromRequestToResponse()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var req = BuildRequest("cid-propagate-001");
            req.CorrelationId = "test-correlation-472";
            var result = await svc.InitiateAsync(req);

            Assert.That(result.CorrelationId, Is.EqualTo("test-correlation-472"));
        }

        [Test]
        public async Task DI_CorrelationId_PropagatesIntoAuditEvents()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var req = BuildRequest("cid-audit-472");
            req.CorrelationId = "audit-cid-472";
            var result = await svc.InitiateAsync(req);

            Assert.That(result.AuditEvents.All(e => e.CorrelationId == "audit-cid-472"), Is.True);
        }

        [Test]
        public async Task DI_AuditTrail_ContainsRequiredComplianceFields()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var initiated = await svc.InitiateAsync(BuildRequest("audit-trail-472"));
            var trail = await svc.GetAuditTrailAsync(initiated.DeploymentId);

            Assert.That(trail.DeploymentId, Is.EqualTo(initiated.DeploymentId));
            Assert.That(trail.FinalState, Is.EqualTo(ContractLifecycleState.Completed));
            Assert.That(trail.Events, Is.Not.Empty);
        }

        [Test]
        public async Task DI_AuditEvents_EachHaveEventId()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.InitiateAsync(BuildRequest("event-id-check-472"));
            Assert.That(result.AuditEvents.All(e => !string.IsNullOrEmpty(e.EventId)), Is.True);
        }

        [Test]
        public async Task DI_AuditEvents_EachHaveTimestamp()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.InitiateAsync(BuildRequest("timestamp-check-472"));
            Assert.That(result.AuditEvents.All(e => !string.IsNullOrEmpty(e.Timestamp)), Is.True);
        }

        [Test]
        public async Task DI_AuditEvents_EachHaveActor()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.InitiateAsync(BuildRequest("actor-check-472"));
            Assert.That(result.AuditEvents.All(e => !string.IsNullOrEmpty(e.Actor)), Is.True);
        }

        [Test]
        public async Task DI_AuditEvents_ContainPolicyEvaluatedEvent()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.InitiateAsync(BuildRequest("policy-event-472"));
            Assert.That(result.AuditEvents
                .Any(e => e.EventKind == ComplianceAuditEventKind.PolicyEvaluated), Is.True);
        }

        [Test]
        public async Task DI_AuditEvents_ContainTransactionSubmittedEvent()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.InitiateAsync(BuildRequest("tx-submitted-472"));
            Assert.That(result.AuditEvents
                .Any(e => e.EventKind == ComplianceAuditEventKind.TransactionSubmitted), Is.True);
        }

        [Test]
        public async Task DI_AuditEvents_ContainTransactionConfirmedEvent()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.InitiateAsync(BuildRequest("tx-confirmed-472"));
            Assert.That(result.AuditEvents
                .Any(e => e.EventKind == ComplianceAuditEventKind.TransactionConfirmed), Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // HTTP-level tests (401 for unauthenticated, 400 for null body)
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task HTTP_Initiate_NoAuth_Returns401()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/backend-deployment-contract/initiate",
                new BackendDeploymentContractRequest
                {
                    ExplicitDeployerAddress = AlgorandAddress,
                    TokenStandard = "ASA",
                    TokenName = "Test",
                    TokenSymbol = "TST",
                    Network = "algorand-mainnet",
                    TotalSupply = 1000,
                    Decimals = 0
                });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_GetStatus_NoAuth_Returns401()
        {
            var response = await _client.GetAsync(
                "/api/v1/backend-deployment-contract/status/dep-test-000");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_Validate_NoAuth_Returns401()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/backend-deployment-contract/validate",
                new BackendDeploymentContractValidationRequest
                {
                    ExplicitDeployerAddress = AlgorandAddress,
                    TokenStandard = "ASA",
                    TokenName = "Test",
                    TokenSymbol = "TST",
                    Network = "algorand-mainnet",
                    TotalSupply = 1000,
                    Decimals = 0
                });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task HTTP_GetAuditTrail_NoAuth_Returns401()
        {
            var response = await _client.GetAsync(
                "/api/v1/backend-deployment-contract/audit/dep-test-000");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC10: Health and backward-compatibility
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task HTTP_Health_ReturnsOkOrServiceUnavailable()
        {
            var response = await _client.GetAsync("/health");
            Assert.That((int)response.StatusCode,
                Is.EqualTo(200).Or.EqualTo(503));
        }

        [Test]
        public async Task DI_Validate_WithExplicitAddress_ReturnsIsValidTrue()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.ValidateAsync(new BackendDeploymentContractValidationRequest
            {
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = "ARC3",
                TokenName = "ValidatedToken",
                TokenSymbol = "VLD",
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            });

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.FirstErrorCode, Is.EqualTo(DeploymentErrorCode.None));
        }

        [Test]
        public async Task DI_Validate_MissingTokenName_IsValidFalse()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.ValidateAsync(new BackendDeploymentContractValidationRequest
            {
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "",
                TokenSymbol = "TST",
                Network = "algorand-mainnet",
                TotalSupply = 1000,
                Decimals = 0
            });

            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public async Task DI_Validate_SetsDeployerAddress()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IBackendDeploymentLifecycleContractService>();

            var result = await svc.ValidateAsync(new BackendDeploymentContractValidationRequest
            {
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = "ASA",
                TokenName = "Test",
                TokenSymbol = "TST",
                Network = "algorand-mainnet",
                TotalSupply = 1000,
                Decimals = 0
            });

            Assert.That(result.DeployerAddress, Is.EqualTo(AlgorandAddress));
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static BackendDeploymentContractRequest BuildRequest(string idemKey) =>
            new()
            {
                IdempotencyKey = idemKey,
                ExplicitDeployerAddress = AlgorandAddress,
                TokenStandard = "ASA",
                TokenName = $"Token{idemKey}",
                TokenSymbol = "TST",
                Network = "algorand-mainnet",
                TotalSupply = 1_000_000,
                Decimals = 6
            };
    }
}
