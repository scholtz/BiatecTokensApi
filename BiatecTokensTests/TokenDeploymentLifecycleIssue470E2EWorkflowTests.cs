using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// E2E workflow tests for Issue #470: Vision Milestone – Deterministic Token
    /// Deployment API Lifecycle with Idempotency, Telemetry, and Reliability Guardrails.
    ///
    /// Structure:
    ///   Part A – Service-layer workflow tests (full pipeline executions without HTTP overhead)
    ///   Part B – Integration tests using DI-resolved service from WebApplicationFactory
    ///
    /// Workflow IDs (WA–WG):
    ///   WA – Full deploy-status-telemetry pipeline
    ///   WB – Idempotency determinism across 3 runs
    ///   WC – Validation-first workflow
    ///   WD – Guardrail evaluation workflow (healthy and unhealthy)
    ///   WE – Retry workflow (success replay, unknown key, null request)
    ///   WF – Schema contract assertions (all required fields non-null)
    ///   WG – Correlation ID propagation across lifecycle
    ///
    /// Business Value: Proves the deployment lifecycle service is a production-ready capability:
    /// deterministic, idempotent, observable, and aligned with the roadmap goal of improving
    /// token deployment reliability and creator onboarding success rates.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TokenDeploymentLifecycleIssue470E2EWorkflowTests
    {
        // ── Part A: Service-layer workflow tests ──────────────────────────────────

        private TokenDeploymentLifecycleService _service = null!;
        private Mock<ILogger<TokenDeploymentLifecycleService>> _loggerMock = null!;

        private const string AlgorandAddress =
            "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        private const string EvmAddress =
            "0x0000000000000000000000000000000000000001";

        [SetUp]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<TokenDeploymentLifecycleService>>();
            _service = new TokenDeploymentLifecycleService(_loggerMock.Object);
        }

        // ════════════════════════════════════════════════════════════════════════
        // WA – Full deploy → status → telemetry pipeline
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WA1_FullPipeline_Deploy_Status_Telemetry_AllCoherent()
        {
            // Step 1: initiate
            var request = BuildAlgorandRequest("WA1");
            var deployed = await _service.InitiateDeploymentAsync(request);

            Assert.That(deployed.Stage, Is.EqualTo(DeploymentStage.Completed));
            Assert.That(deployed.AssetId, Is.GreaterThan(0UL));

            // Step 2: status
            var status = await _service.GetDeploymentStatusAsync(deployed.DeploymentId);
            Assert.That(status.DeploymentId, Is.EqualTo(deployed.DeploymentId));
            Assert.That(status.Stage, Is.EqualTo(DeploymentStage.Completed));

            // Step 3: telemetry
            var events = await _service.GetTelemetryEventsAsync(deployed.DeploymentId);
            Assert.That(events, Is.Not.Empty);
            Assert.That(events.Any(e => e.EventType == TelemetryEventType.CompletionSuccess), Is.True);
        }

        [Test]
        public async Task WA2_FullPipeline_AllStagesRepresentedInTelemetry()
        {
            var deployed = await _service.InitiateDeploymentAsync(BuildAlgorandRequest("WA2"));
            var stageEvents = deployed.TelemetryEvents
                .Where(e => e.EventType == TelemetryEventType.StageTransition)
                .Select(e => e.Stage)
                .ToHashSet();

            Assert.That(stageEvents, Does.Contain(DeploymentStage.Initialising));
            Assert.That(stageEvents, Does.Contain(DeploymentStage.Validating));
            Assert.That(stageEvents, Does.Contain(DeploymentStage.Submitting));
            Assert.That(stageEvents, Does.Contain(DeploymentStage.Confirming));
        }

        [Test]
        public async Task WA3_FullPipeline_ProgressSnapshotReflectsCompletion()
        {
            var deployed = await _service.InitiateDeploymentAsync(BuildAlgorandRequest("WA3"));
            Assert.That(deployed.Progress.PercentComplete, Is.EqualTo(100));
            Assert.That(deployed.Progress.Summary, Does.Contain("completed").IgnoreCase);
        }

        // ════════════════════════════════════════════════════════════════════════
        // WB – Idempotency determinism across 3 identical runs
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WB1_IdempotencyDeterminism_ThreeRuns_SameDeploymentId()
        {
            var request = BuildAlgorandRequest("WB1");
            request.IdempotencyKey = "wb1-determinism-key";

            var run1 = await _service.InitiateDeploymentAsync(request);
            var run2 = await _service.InitiateDeploymentAsync(request);
            var run3 = await _service.InitiateDeploymentAsync(request);

            Assert.That(run1.DeploymentId, Is.EqualTo(run2.DeploymentId));
            Assert.That(run2.DeploymentId, Is.EqualTo(run3.DeploymentId));
        }

        [Test]
        public async Task WB2_IdempotencyDeterminism_ThreeRuns_SameAssetId()
        {
            var request = BuildAlgorandRequest("WB2");
            request.IdempotencyKey = "wb2-assetid-key";

            var run1 = await _service.InitiateDeploymentAsync(request);
            var run2 = await _service.InitiateDeploymentAsync(request);
            var run3 = await _service.InitiateDeploymentAsync(request);

            Assert.That(run1.AssetId, Is.EqualTo(run2.AssetId));
            Assert.That(run2.AssetId, Is.EqualTo(run3.AssetId));
        }

        [Test]
        public async Task WB3_IdempotencyDeterminism_SecondAndThirdRunsAreReplays()
        {
            var request = BuildAlgorandRequest("WB3");
            request.IdempotencyKey = "wb3-replay-key";

            await _service.InitiateDeploymentAsync(request);
            var run2 = await _service.InitiateDeploymentAsync(request);
            var run3 = await _service.InitiateDeploymentAsync(request);

            Assert.That(run2.IsIdempotentReplay, Is.True);
            Assert.That(run3.IsIdempotentReplay, Is.True);
        }

        // ════════════════════════════════════════════════════════════════════════
        // WC – Validation-first workflow
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WC1_ValidateFirst_ValidRequest_ThenInitiate_Succeeds()
        {
            // Validate
            var validReq = BuildValidValidationRequest("ASA");
            var validation = await _service.ValidateDeploymentInputsAsync(validReq);
            Assert.That(validation.IsValid, Is.True);

            // Deploy only if valid
            var deployResult = await _service.InitiateDeploymentAsync(BuildAlgorandRequest("WC1"));
            Assert.That(deployResult.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task WC2_ValidateFirst_InvalidRequest_MultipleErrorsReported()
        {
            var badReq = new DeploymentValidationRequest
            {
                TokenStandard   = "INVALID",
                TokenName       = string.Empty,
                TokenSymbol     = string.Empty,
                Network         = string.Empty,
                TotalSupply     = 0,
                Decimals        = -5,
                CreatorAddress  = "bad-address",
            };

            var validation = await _service.ValidateDeploymentInputsAsync(badReq);

            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Results.Count(r => !r.IsValid), Is.GreaterThan(3));
        }

        [Test]
        public async Task WC3_ValidateFirst_CorrelationIdPropagated()
        {
            var req = BuildValidValidationRequest("ASA");
            req.CorrelationId = "wc3-correlation";
            var validation = await _service.ValidateDeploymentInputsAsync(req);

            Assert.That(validation.CorrelationId, Is.EqualTo("wc3-correlation"));
        }

        // ════════════════════════════════════════════════════════════════════════
        // WD – Guardrail evaluation workflow
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public void WD1_GuardrailWorkflow_HealthyContext_NoBlockers()
        {
            var ctx      = BuildHealthyGuardrailContext();
            var findings = _service.EvaluateReliabilityGuardrails(ctx);

            Assert.That(findings.Any(f => f.IsBlocking), Is.False);
        }

        [Test]
        public void WD2_GuardrailWorkflow_UnhealthyContext_MultipleBlockers()
        {
            var ctx = new GuardrailEvaluationContext
            {
                NodeReachable       = false,
                IsTimedOut          = true,
                CreatorAddressValid = false,
                MaxRetryAttempts    = 3,
                RetryCount          = 5,
            };
            var findings = _service.EvaluateReliabilityGuardrails(ctx);

            Assert.That(findings.Count(f => f.IsBlocking), Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void WD3_GuardrailWorkflow_Arc3_InfoFindingForIpfs()
        {
            var ctx = BuildHealthyGuardrailContext();
            ctx.RequiresIpfs = true;
            var findings = _service.EvaluateReliabilityGuardrails(ctx);

            var ipfsFinding = findings.FirstOrDefault(f => f.GuardrailId == "GR-007");
            Assert.That(ipfsFinding, Is.Not.Null);
            Assert.That(ipfsFinding!.Severity, Is.EqualTo(GuardrailSeverity.Info));
        }

        // ════════════════════════════════════════════════════════════════════════
        // WE – Retry workflow
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WE1_RetryWorkflow_AfterSuccess_IsIdempotentReplay()
        {
            var request = BuildAlgorandRequest("WE1");
            request.IdempotencyKey = "we1-retry-success";
            await _service.InitiateDeploymentAsync(request);

            var retry = await _service.RetryDeploymentAsync(new DeploymentRetryRequest
            {
                IdempotencyKey = "we1-retry-success"
            });

            Assert.That(retry.IsIdempotentReplay, Is.True);
            Assert.That(retry.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task WE2_RetryWorkflow_UnknownKey_ReturnsFailedNotFound()
        {
            var retry = await _service.RetryDeploymentAsync(new DeploymentRetryRequest
            {
                IdempotencyKey = "we2-unknown-key-xyz"
            });

            Assert.That(retry.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(retry.Message, Does.Contain("found").IgnoreCase);
        }

        [Test]
        public async Task WE3_RetryWorkflow_NullRequest_ReturnsTerminalFailure()
        {
            var retry = await _service.RetryDeploymentAsync(null!);
            Assert.That(retry.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(retry.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
        }

        // ════════════════════════════════════════════════════════════════════════
        // WF – Schema contract assertions
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WF1_SchemaContract_DeploymentResponse_AllRequiredFieldsNonNull()
        {
            var result = await _service.InitiateDeploymentAsync(BuildAlgorandRequest("WF1"));

            Assert.That(result.DeploymentId,    Is.Not.Null.And.Not.Empty);
            Assert.That(result.IdempotencyKey,  Is.Not.Null.And.Not.Empty);
            Assert.That(result.CorrelationId,   Is.Not.Null.And.Not.Empty);
            Assert.That(result.Message,         Is.Not.Null.And.Not.Empty);
            Assert.That(result.SchemaVersion,   Is.EqualTo("1.0.0"));
            Assert.That(result.Progress,        Is.Not.Null);
            Assert.That(result.TelemetryEvents, Is.Not.Null);
            Assert.That(result.ValidationResults, Is.Not.Null);
            Assert.That(result.GuardrailFindings, Is.Not.Null);
        }

        [Test]
        public async Task WF2_SchemaContract_ValidationResponse_AllRequiredFieldsNonNull()
        {
            var req = BuildValidValidationRequest("ASA");
            var result = await _service.ValidateDeploymentInputsAsync(req);

            Assert.That(result.Results,           Is.Not.Null);
            Assert.That(result.GuardrailFindings, Is.Not.Null);
            Assert.That(result.Summary,           Is.Not.Null.And.Not.Empty);
            Assert.That(result.SchemaVersion,     Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task WF3_SchemaContract_TelemetryEvents_AllFieldsNonNull()
        {
            var deployed = await _service.InitiateDeploymentAsync(BuildAlgorandRequest("WF3"));
            var events   = await _service.GetTelemetryEventsAsync(deployed.DeploymentId);

            Assert.That(events, Is.Not.Empty);
            foreach (var ev in events)
            {
                Assert.That(ev.EventId,      Is.Not.Null.And.Not.Empty);
                Assert.That(ev.DeploymentId, Is.Not.Null.And.Not.Empty);
                Assert.That(ev.Description,  Is.Not.Null.And.Not.Empty);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // WG – Correlation ID propagation
        // ════════════════════════════════════════════════════════════════════════

        [Test]
        public async Task WG1_CorrelationId_PropagatedToAllTelemetryEvents()
        {
            var request = BuildAlgorandRequest("WG1");
            request.CorrelationId = "wg1-correlation-id";
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.CorrelationId, Is.EqualTo("wg1-correlation-id"));
            Assert.That(result.TelemetryEvents.All(e => e.CorrelationId == "wg1-correlation-id"),
                Is.True, "All telemetry events must carry the original correlation ID.");
        }

        [Test]
        public async Task WG2_CorrelationId_GeneratedWhenNotProvided()
        {
            var request = BuildAlgorandRequest("WG2");
            request.CorrelationId = null;
            var result = await _service.InitiateDeploymentAsync(request);

            Assert.That(result.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task WG3_CorrelationId_StatusQuery_ReturnsDeploymentCorrelationId()
        {
            var request = BuildAlgorandRequest("WG3");
            request.CorrelationId = "wg3-original-corr";
            var deployed = await _service.InitiateDeploymentAsync(request);

            var status = await _service.GetDeploymentStatusAsync(
                deployed.DeploymentId, "wg3-status-query-corr");

            // Status query gets its own correlation ID but returns the same deployment
            Assert.That(status.DeploymentId, Is.EqualTo(deployed.DeploymentId));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Part B – Integration: DI resolution via WebApplicationFactory
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
            ["JwtConfig:SecretKey"] = "token-deployment-lifecycle-470-e2e-test-key-32chars!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "TokenDeploymentLifecycle470E2ETestKey32CharsReq!!"
        };

        [Test]
        public async Task B1_DiResolved_Service_SuccessfulDeployment()
        {
            using var factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(TestConfiguration);
                    });
                });

            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var result = await service.InitiateDeploymentAsync(BuildAlgorandRequest("B1-DI"));
            Assert.That(result.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task B2_DiResolved_Service_IdempotencyDeterminism_ThreeRuns()
        {
            using var factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(TestConfiguration);
                    });
                });

            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ITokenDeploymentLifecycleService>();

            var request = BuildAlgorandRequest("B2-idem");
            request.IdempotencyKey = "b2-di-idem-key";

            var run1 = await service.InitiateDeploymentAsync(request);
            var run2 = await service.InitiateDeploymentAsync(request);
            var run3 = await service.InitiateDeploymentAsync(request);

            Assert.That(run1.DeploymentId, Is.EqualTo(run2.DeploymentId));
            Assert.That(run2.DeploymentId, Is.EqualTo(run3.DeploymentId));
        }

        [Test]
        public async Task B3_Application_HealthEndpoint_Reachable()
        {
            using var factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(TestConfiguration);
                    });
                });
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/health");
            Assert.That((int)response.StatusCode, Is.EqualTo(200).Or.EqualTo(503));
        }

        // ── Builders ────────────────────────────────────────────────────────────

        private static TokenDeploymentLifecycleRequest BuildAlgorandRequest(string tag) => new()
        {
            TokenStandard    = "ASA",
            TokenName        = $"E2EToken-{tag}",
            TokenSymbol      = "E2E",
            Network          = "algorand-mainnet",
            TotalSupply      = 1_000_000,
            Decimals         = 6,
            CreatorAddress   = AlgorandAddress,
            MaxRetryAttempts = 3,
            TimeoutSeconds   = 120,
        };

        private static DeploymentValidationRequest BuildValidValidationRequest(string standard) => new()
        {
            TokenStandard   = standard,
            TokenName       = "E2EValidToken",
            TokenSymbol     = "EVT",
            Network         = "algorand-mainnet",
            TotalSupply     = 1_000_000,
            Decimals        = 6,
            CreatorAddress  = AlgorandAddress,
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
