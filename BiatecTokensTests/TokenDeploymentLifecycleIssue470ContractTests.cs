using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration/contract tests for Issue #470: Vision Milestone – Deterministic Token
    /// Deployment API Lifecycle with Idempotency, Telemetry, and Reliability Guardrails.
    ///
    /// These tests use WebApplicationFactory to exercise:
    ///   - Service-layer resolution from the DI container via CreateScope()
    ///   - HTTP-level assertions for unauthenticated (401) and null-body (400) paths
    ///   - DI container startup validation
    ///   - Health endpoint reachability
    ///   - Backward-compatibility of existing endpoints
    ///
    /// AC1 - Primary flow completes end-to-end without manual intervention
    /// AC2 - Every critical state transition is surfaced via the API
    /// AC3 - Validation errors return deterministic machine-readable codes
    /// AC4 - Idempotent operations return stable results
    /// AC5 - Telemetry endpoint returns lifecycle events
    /// AC6 - Guardrail endpoint returns structured findings
    /// AC7 - CI passes: DI resolves, endpoints respond, no unhandled exceptions
    /// AC8 - PR maps business goals to implementation and tests
    /// AC9 - Security: unauthenticated requests are rejected
    /// AC10 - Documentation updated; runbooks reference new endpoints
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TokenDeploymentLifecycleIssue470ContractTests
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
            ["JwtConfig:SecretKey"] = "token-deployment-lifecycle-470-test-key-32chars!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "TokenDeploymentLifecycle470TestKey32CharsReq!!"
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
        // AC7: DI container resolves the service without exceptions
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void TokenDeploymentLifecycleService_DiContainerResolves_NoException()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();
            Assert.That(service, Is.Not.Null);
        }

        [Test]
        public async Task Application_Starts_HealthEndpointResponds()
        {
            var response = await _client.GetAsync("/health");
            Assert.That((int)response.StatusCode, Is.EqualTo(200).Or.EqualTo(503));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC1: Service-layer – successful deployment via DI-resolved service
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateDeployment_Service_ValidAsaRequest_StageCompleted()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var result = await service.InitiateDeploymentAsync(BuildValidRequest("ASA"));

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
            Assert.That(result.Outcome, Is.EqualTo(DeploymentOutcome.Success));
        }

        [Test]
        public async Task InitiateDeployment_Service_ValidArc3Request_StageCompleted()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var result = await service.InitiateDeploymentAsync(BuildValidRequest("ARC3"));

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task InitiateDeployment_Service_ValidErc20Request_StageCompleted()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var result = await service.InitiateDeploymentAsync(BuildValidEvmRequest());

            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC2: All required response fields are non-null
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateDeployment_Service_ResponseHasAllRequiredFields()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var result = await service.InitiateDeploymentAsync(BuildValidRequest("ASA"));

            Assert.That(result.DeploymentId,    Is.Not.Null.And.Not.Empty);
            Assert.That(result.IdempotencyKey,  Is.Not.Null.And.Not.Empty);
            Assert.That(result.CorrelationId,   Is.Not.Null.And.Not.Empty);
            Assert.That(result.Message,         Is.Not.Null.And.Not.Empty);
            Assert.That(result.SchemaVersion,   Is.EqualTo("1.0.0"));
            Assert.That(result.Progress,        Is.Not.Null);
            Assert.That(result.TelemetryEvents, Is.Not.Null);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC3: Validation errors return machine-readable codes
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ValidateInputs_Service_MissingTokenName_ReturnsErrorCode()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var req = BuildValidValidationRequest("ASA");
            req.TokenName = string.Empty;
            var result = await service.ValidateDeploymentInputsAsync(req);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "TOKEN_NAME_REQUIRED"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_Service_UnsupportedNetwork_ReturnsNetworkUnsupported()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var req = BuildValidValidationRequest("ASA");
            req.Network = "unsupported-network";
            var result = await service.ValidateDeploymentInputsAsync(req);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Results.Any(r => r.ErrorCode == "NETWORK_UNSUPPORTED"), Is.True);
        }

        [Test]
        public async Task ValidateInputs_Service_ValidRequest_IsValidTrue()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var result = await service.ValidateDeploymentInputsAsync(BuildValidValidationRequest("ASA"));

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public async Task ValidateInputs_Service_ResponseHasCorrelationId()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var req = BuildValidValidationRequest("ASA");
            req.CorrelationId = "contract-validation-corr";
            var result = await service.ValidateDeploymentInputsAsync(req);

            Assert.That(result.CorrelationId, Is.EqualTo("contract-validation-corr"));
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC4: Idempotency – same key produces stable results
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task InitiateDeployment_Service_SameKeyThreeRuns_DeterministicResults()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var request = BuildValidRequest("ASA");
            request.IdempotencyKey = "contract-idem-key-470";

            var run1 = await service.InitiateDeploymentAsync(request);
            var run2 = await service.InitiateDeploymentAsync(request);
            var run3 = await service.InitiateDeploymentAsync(request);

            Assert.That(run1.DeploymentId, Is.EqualTo(run2.DeploymentId));
            Assert.That(run2.DeploymentId, Is.EqualTo(run3.DeploymentId));
            Assert.That(run1.AssetId,      Is.EqualTo(run2.AssetId));
            Assert.That(run2.AssetId,      Is.EqualTo(run3.AssetId));

            Assert.That(run2.IsIdempotentReplay, Is.True);
            Assert.That(run3.IsIdempotentReplay, Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC5: Telemetry endpoint returns lifecycle events
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task GetTelemetry_Service_AfterDeployment_ReturnsNonEmptyList()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var deployed = await service.InitiateDeploymentAsync(BuildValidRequest("ASA"));
            var events   = await service.GetTelemetryEventsAsync(deployed.DeploymentId);

            Assert.That(events, Is.Not.Empty);
        }

        [Test]
        public async Task GetTelemetry_Service_AfterDeployment_AllEventsHaveDeploymentId()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var deployed = await service.InitiateDeploymentAsync(BuildValidRequest("ASA"));
            var events   = await service.GetTelemetryEventsAsync(deployed.DeploymentId);

            Assert.That(events.All(e => e.DeploymentId == deployed.DeploymentId), Is.True);
        }

        [Test]
        public async Task GetTelemetry_Service_UnknownDeployment_ReturnsEmptyList()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var events = await service.GetTelemetryEventsAsync("unknown-deploy-id-xyz");
            Assert.That(events, Is.Empty);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC6: Guardrails return structured findings
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void EvaluateGuardrails_Service_HealthyContext_NoBlockers()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var ctx = BuildHealthyGuardrailContext();
            var findings = service.EvaluateReliabilityGuardrails(ctx);

            Assert.That(findings.Any(f => f.IsBlocking), Is.False);
        }

        [Test]
        public void EvaluateGuardrails_Service_NodeUnreachable_ReturnGR001()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var ctx = BuildHealthyGuardrailContext();
            ctx.NodeReachable = false;
            var findings = service.EvaluateReliabilityGuardrails(ctx);

            Assert.That(findings.Any(f => f.GuardrailId == "GR-001"), Is.True);
        }

        [Test]
        public void EvaluateGuardrails_Service_AllFindings_HaveNonEmptyDescriptions()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var ctx = new GuardrailEvaluationContext
            {
                NodeReachable       = false,
                IsTimedOut          = true,
                CreatorAddressValid = false,
                MaxRetryAttempts    = 3,
                RetryCount          = 5,
            };
            var findings = service.EvaluateReliabilityGuardrails(ctx);

            Assert.That(findings.All(f => !string.IsNullOrEmpty(f.Description)), Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // AC9: Security – unauthenticated HTTP requests are rejected
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task Initiate_Unauthenticated_Returns401()
        {
            using var strictFactory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        var strictConfig = new Dictionary<string, string?>(TestConfiguration)
                        {
                            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "false",
                            ["AlgorandAuthentication:Debug"] = "false"
                        };
                        config.AddInMemoryCollection(strictConfig);
                    });
                });
            using var strictClient = strictFactory.CreateClient();
            var response = await strictClient.PostAsJsonAsync(
                "/api/v1/token-deployment-lifecycle/initiate", BuildValidRequest("ASA"));
            Assert.That((int)response.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task Validate_Unauthenticated_Returns401()
        {
            using var strictFactory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        var strictConfig = new Dictionary<string, string?>(TestConfiguration)
                        {
                            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "false",
                            ["AlgorandAuthentication:Debug"] = "false"
                        };
                        config.AddInMemoryCollection(strictConfig);
                    });
                });
            using var strictClient = strictFactory.CreateClient();
            var response = await strictClient.PostAsJsonAsync(
                "/api/v1/token-deployment-lifecycle/validate", BuildValidValidationRequest("ASA"));
            Assert.That((int)response.StatusCode, Is.EqualTo(401));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Backward compatibility: existing endpoints still reachable
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ExistingPortfolioIntelligenceEndpoint_StillReachable()
        {
            var response = await _client.PostAsync(
                "/api/v1/portfolio-intelligence/evaluate",
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            // 400 or 401 – not 404 or 500
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(404).And.Not.EqualTo(500));
        }

        [Test]
        public async Task ExistingDeploymentStatusEndpoint_StillReachable()
        {
            var response = await _client.GetAsync("/api/v1/token/deployments/test-id-123");
            // 401 (unauth) or 404 (not found) but never 500
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(500));
        }

        [Test]
        public async Task ExistingLifecycleIntelligenceEndpoint_StillReachable()
        {
            var response = await _client.PostAsync(
                "/api/v2/lifecycle/readiness",
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(404).And.Not.EqualTo(500));
        }

        // ── Builders ────────────────────────────────────────────────────────────

        private static TokenDeploymentLifecycleRequest BuildValidRequest(string standard) => new()
        {
            TokenStandard    = standard,
            TokenName        = "ContractTestToken",
            TokenSymbol      = "CTT",
            Network          = "algorand-mainnet",
            TotalSupply      = 1_000_000,
            Decimals         = 6,
            CreatorAddress   = AlgorandAddress,
            MaxRetryAttempts = 3,
            TimeoutSeconds   = 120,
        };

        private static TokenDeploymentLifecycleRequest BuildValidEvmRequest() => new()
        {
            TokenStandard    = "ERC20",
            TokenName        = "ContractTestEvmToken",
            TokenSymbol      = "CTE",
            Network          = "base-mainnet",
            TotalSupply      = 1_000_000,
            Decimals         = 18,
            CreatorAddress   = "0x0000000000000000000000000000000000000001",
            MaxRetryAttempts = 3,
            TimeoutSeconds   = 120,
        };

        private static DeploymentValidationRequest BuildValidValidationRequest(string standard) => new()
        {
            TokenStandard    = standard,
            TokenName        = "ContractTestToken",
            TokenSymbol      = "CTT",
            Network          = "algorand-mainnet",
            TotalSupply      = 1_000_000,
            Decimals         = 6,
            CreatorAddress   = AlgorandAddress,
        };

        private static GuardrailEvaluationContext BuildHealthyGuardrailContext() => new()
        {
            TokenStandard               = "ASA",
            Network                     = "algorand-mainnet",
            RequiresIpfs                = false,
            NodeReachable               = true,
            RetryCount                  = 0,
            MaxRetryAttempts            = 3,
            IsTimedOut                  = false,
            HasInFlightDuplicate        = false,
            CreatorAddressValid         = true,
            ConflictingDeploymentDetected = false,
        };
    }
}
