using BiatecTokensApi.Models.DeterministicOrchestration;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// E2E workflow tests for Issue #480: Deterministic Orchestration.
    /// Tests WA-WJ: full lifecycle, idempotency determinism, compliance integration,
    /// audit trail completeness, error schema stability, and backward compatibility.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class DeterministicOrchestrationIssue480E2EWorkflowTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;

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
            ["JwtConfig:SecretKey"] = "deterministic-orchestration-e2e-480-test-key-32chars!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "DeterministicOrchestrationE2E480TestKey32Chars!!"
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
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
        }

        // ── WA: Full lifecycle E2E ────────────────────────────────────────────

        [Test]
        public async Task WA_FullLifecycle_StartToCompleted()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();

            // Step 1: Start
            var created = await svc.OrchestrateAsync(BuildRequest("WA-token", "wa-corr-01"));
            Assert.That(created.Success, Is.True);
            Assert.That(created.Stage, Is.EqualTo(OrchestrationStage.Draft));

            // Step 2: Advance through pipeline
            var id = created.OrchestrationId!;
            var adv1 = await svc.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Validated
            Assert.That(adv1.CurrentStage, Is.EqualTo(OrchestrationStage.Validated));

            var adv2 = await svc.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Queued
            Assert.That(adv2.CurrentStage, Is.EqualTo(OrchestrationStage.Queued));

            var adv3 = await svc.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Processing
            Assert.That(adv3.CurrentStage, Is.EqualTo(OrchestrationStage.Processing));

            var adv4 = await svc.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Confirmed
            Assert.That(adv4.CurrentStage, Is.EqualTo(OrchestrationStage.Confirmed));

            var adv5 = await svc.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id }); // →Completed
            Assert.That(adv5.CurrentStage, Is.EqualTo(OrchestrationStage.Completed));

            // Step 3: Verify final status
            var status = await svc.GetStatusAsync(id, null);
            Assert.That(status!.Stage, Is.EqualTo(OrchestrationStage.Completed));
        }

        // ── WB: Idempotency determinism E2E ──────────────────────────────────

        [Test]
        public async Task WB_Idempotency_ThreeRuns_IdenticalOutcomes()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();

            var req = BuildRequest("WB-token", "wb-corr");
            req.IdempotencyKey = "wb-idem-e2e-key";

            var r1 = await svc.OrchestrateAsync(req);
            var r2 = await svc.OrchestrateAsync(req);
            var r3 = await svc.OrchestrateAsync(req);

            Assert.That(r1.OrchestrationId, Is.EqualTo(r2.OrchestrationId));
            Assert.That(r2.OrchestrationId, Is.EqualTo(r3.OrchestrationId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
            Assert.That(r3.IsIdempotentReplay, Is.True);
        }

        // ── WC: Compliance pipeline E2E ───────────────────────────────────────

        [Test]
        public async Task WC_CompliancePipeline_RunsMica_And_KycAml_Checks()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();

            var created = await svc.OrchestrateAsync(BuildRequest("WC-token", "wc-corr"));
            var check = await svc.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = created.OrchestrationId,
                RunMicaChecks = true,
                RunKycAmlChecks = true,
                CorrelationId = "wc-comp-corr"
            });

            Assert.That(check.Success, Is.True);
            Assert.That(check.Rules.Any(r => r.RuleId.StartsWith("MICA")), Is.True);
            Assert.That(check.Rules.Any(r => r.RuleId.StartsWith("KYC") || r.RuleId.StartsWith("AML")), Is.True);
        }

        // ── WD: Audit trail completeness E2E ─────────────────────────────────

        [Test]
        public async Task WD_AuditTrail_ContainsAllOperations()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();

            var req = BuildRequest("WD-token", "wd-corr");
            var created = await svc.OrchestrateAsync(req);
            var id = created.OrchestrationId!;

            await svc.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = id });
            await svc.RunComplianceCheckAsync(new ComplianceCheckRequest { OrchestrationId = id });
            await svc.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = id });

            var events = svc.GetAuditEvents(id);
            var ops = events.Select(e => e.Operation).ToList();
            Assert.That(ops, Does.Contain("Orchestrate"));
            Assert.That(ops, Does.Contain("Advance"));
            Assert.That(ops, Does.Contain("ComplianceCheck"));
            Assert.That(ops, Does.Contain("Cancel"));
        }

        // ── WE: Error schema stability E2E ───────────────────────────────────

        [Test]
        public async Task WE_ErrorResponse_HasConsistentSchema()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();

            var req = BuildRequest("WE-token", "we-corr");
            req.TokenName = null;
            var result = await svc.OrchestrateAsync(req);

            Assert.That(result.Success, Is.False);
            // Machine-readable code
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty);
            // Human-safe message
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            // Client action hint
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
            // Stable schema version
            Assert.That(result.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        // ── WF: Correlation ID propagation E2E ───────────────────────────────

        [Test]
        public async Task WF_CorrelationId_PropagatedThroughAllOperations()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();

            const string correlationId = "wf-correlation-id-e2e";
            var req = BuildRequest("WF-token", correlationId);
            var created = await svc.OrchestrateAsync(req);
            Assert.That(created.CorrelationId, Is.EqualTo(correlationId));

            var adv = await svc.AdvanceAsync(new OrchestrationAdvanceRequest
            {
                OrchestrationId = created.OrchestrationId,
                CorrelationId = correlationId
            });
            Assert.That(adv.CorrelationId, Is.EqualTo(correlationId));
        }

        // ── WG: DI resolution E2E ─────────────────────────────────────────────

        [Test]
        public void WG_DI_ServiceResolvesCorrectly()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetService<IDeterministicOrchestrationService>();
            Assert.That(svc, Is.Not.Null);
            Assert.That(svc, Is.InstanceOf<DeterministicOrchestrationService>());
        }

        // ── WH: Backward compat – existing routes unaffected ─────────────────

        [Test]
        public async Task WH_ExistingGuidedLaunchService_StillResolvesAfterIssue480()
        {
            using var scope = _factory.Services.CreateScope();
            var guidedSvc = scope.ServiceProvider.GetService<IGuidedLaunchReliabilityService>();
            Assert.That(guidedSvc, Is.Not.Null);
        }

        [Test]
        public async Task WH_ExistingMVPHardeningService_StillResolvesAfterIssue480()
        {
            using var scope = _factory.Services.CreateScope();
            var mvpSvc = scope.ServiceProvider.GetService<IMVPBackendHardeningService>();
            Assert.That(mvpSvc, Is.Not.Null);
        }

        // ── WI: Independent service instances hold independent state ─────────

        [Test]
        public async Task WI_TwoOrchestrations_HaveIndependentState()
        {
            var logger = new Mock<ILogger<DeterministicOrchestrationService>>();
            var svc = new DeterministicOrchestrationService(logger.Object);

            var r1 = await svc.OrchestrateAsync(BuildRequest("WI-token-1", "wi-corr-1"));
            var r2 = await svc.OrchestrateAsync(BuildRequest("WI-token-2", "wi-corr-2"));

            // Advance only r1
            await svc.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = r1.OrchestrationId });

            var s1 = await svc.GetStatusAsync(r1.OrchestrationId!, null);
            var s2 = await svc.GetStatusAsync(r2.OrchestrationId!, null);

            Assert.That(s1!.Stage, Is.EqualTo(OrchestrationStage.Validated));
            Assert.That(s2!.Stage, Is.EqualTo(OrchestrationStage.Draft));
        }

        // ── WJ: Cancel-then-restart workflow ──────────────────────────────────

        [Test]
        public async Task WJ_CancelThenRestart_ProducesNewOrchestration()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();

            var first = await svc.OrchestrateAsync(BuildRequest("WJ-token", "wj-corr-1"));
            await svc.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = first.OrchestrationId });

            var second = await svc.OrchestrateAsync(BuildRequest("WJ-token", "wj-corr-2"));
            Assert.That(second.Success, Is.True);
            Assert.That(second.OrchestrationId, Is.Not.EqualTo(first.OrchestrationId));
            Assert.That(second.Stage, Is.EqualTo(OrchestrationStage.Draft));
        }

        // ── Helper ─────────────────────────────────────────────────────────────

        private static OrchestrationRequest BuildRequest(string tokenName, string correlationId) => new()
        {
            TokenName = tokenName,
            TokenStandard = "ASA",
            Network = "testnet",
            DeployerAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            CorrelationId = correlationId,
            MaxRetries = 3
        };
    }
}
