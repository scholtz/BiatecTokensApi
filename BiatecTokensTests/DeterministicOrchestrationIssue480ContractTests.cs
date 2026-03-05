using BiatecTokensApi.Models.DeterministicOrchestration;
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
    /// Contract/integration tests for Issue #480: Deterministic Orchestration.
    /// Uses WebApplicationFactory for DI resolution and HTTP-level 401 assertions.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class DeterministicOrchestrationIssue480ContractTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

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
            ["JwtConfig:SecretKey"] = "deterministic-orchestration-issue-480-test-key-32chars!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "DeterministicOrchestrationIssue480TestKey32Chars!!"
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

        // ── DI wiring ──────────────────────────────────────────────────────────

        [Test]
        public void DI_DeterministicOrchestrationService_Resolves()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetService<IDeterministicOrchestrationService>();
            Assert.That(svc, Is.Not.Null);
        }

        // ── Unauthenticated HTTP access ────────────────────────────────────────

        [Test]
        public async Task POST_Orchestrate_Unauthenticated_Returns401()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/deterministic-orchestration/orchestrate",
                BuildRequest());
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GET_Status_Unauthenticated_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/deterministic-orchestration/status/some-id");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task POST_Advance_Unauthenticated_Returns401()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/deterministic-orchestration/advance",
                new OrchestrationAdvanceRequest { OrchestrationId = "x" });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task POST_ComplianceCheck_Unauthenticated_Returns401()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/deterministic-orchestration/compliance-check",
                new ComplianceCheckRequest { OrchestrationId = "x" });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GET_Audit_Unauthenticated_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/deterministic-orchestration/audit/some-id");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task POST_Cancel_Unauthenticated_Returns401()
        {
            var response = await _client.PostAsJsonAsync(
                "/api/v1/deterministic-orchestration/cancel",
                new OrchestrationCancelRequest { OrchestrationId = "x" });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── Service-layer contract assertions ──────────────────────────────────

        [Test]
        public async Task Orchestrate_ValidRequest_ReturnsOrchestrationId()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var result = await svc.OrchestrateAsync(BuildRequest());
            Assert.That(result.Success, Is.True);
            Assert.That(result.OrchestrationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Orchestrate_InitialStage_IsDraft()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var result = await svc.OrchestrateAsync(BuildRequest());
            Assert.That(result.Stage, Is.EqualTo(OrchestrationStage.Draft));
        }

        [Test]
        public async Task Orchestrate_ResponseSchema_HasSchemaVersion()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var result = await svc.OrchestrateAsync(BuildRequest());
            Assert.That(result.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Orchestrate_MissingTokenName_ErrorHasCode()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var req = BuildRequest();
            req.TokenName = null;
            var result = await svc.OrchestrateAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetStatus_AfterOrchestrate_ReturnsRecord()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var created = await svc.OrchestrateAsync(BuildRequest());
            var status = await svc.GetStatusAsync(created.OrchestrationId!, null);
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.Success, Is.True);
            Assert.That(status.Stage, Is.EqualTo(OrchestrationStage.Draft));
        }

        [Test]
        public async Task GetStatus_UnknownId_ReturnsNull()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var status = await svc.GetStatusAsync("no-such-id", null);
            Assert.That(status, Is.Null);
        }

        [Test]
        public async Task Advance_FromDraft_AdvancesToValidated()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var created = await svc.OrchestrateAsync(BuildRequest());
            var adv = await svc.AdvanceAsync(new OrchestrationAdvanceRequest
            {
                OrchestrationId = created.OrchestrationId
            });
            Assert.That(adv.Success, Is.True);
            Assert.That(adv.CurrentStage, Is.EqualTo(OrchestrationStage.Validated));
        }

        [Test]
        public async Task Advance_ThroughLifecycle_TracksPreviousStage()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var created = await svc.OrchestrateAsync(BuildRequest());
            var adv = await svc.AdvanceAsync(new OrchestrationAdvanceRequest
            {
                OrchestrationId = created.OrchestrationId
            });
            Assert.That(adv.PreviousStage, Is.EqualTo(OrchestrationStage.Draft));
        }

        [Test]
        public async Task ComplianceCheck_WithValidOrchestration_ReturnsRules()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var created = await svc.OrchestrateAsync(BuildRequest());
            var check = await svc.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = created.OrchestrationId,
                RunMicaChecks = true,
                RunKycAmlChecks = true
            });
            Assert.That(check.Success, Is.True);
            Assert.That(check.Rules, Is.Not.Empty);
        }

        [Test]
        public async Task ComplianceCheck_UpdatesStatusOnOrchestration()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var created = await svc.OrchestrateAsync(BuildRequest());
            await svc.RunComplianceCheckAsync(new ComplianceCheckRequest
            {
                OrchestrationId = created.OrchestrationId
            });
            var status = await svc.GetStatusAsync(created.OrchestrationId!, null);
            Assert.That(status!.ComplianceStatus, Is.Not.EqualTo(ComplianceCheckStatus.Pending));
        }

        [Test]
        public async Task Cancel_ValidOrchestration_Succeeds()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var created = await svc.OrchestrateAsync(BuildRequest());
            var cancel = await svc.CancelAsync(new OrchestrationCancelRequest
            {
                OrchestrationId = created.OrchestrationId
            });
            Assert.That(cancel.Success, Is.True);
        }

        [Test]
        public async Task Cancel_UpdatesStage_ToCancelled()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var created = await svc.OrchestrateAsync(BuildRequest());
            await svc.CancelAsync(new OrchestrationCancelRequest { OrchestrationId = created.OrchestrationId });
            var status = await svc.GetStatusAsync(created.OrchestrationId!, null);
            Assert.That(status!.Stage, Is.EqualTo(OrchestrationStage.Cancelled));
        }

        [Test]
        public async Task GetAuditEvents_AfterOperations_ReturnsEvents()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var created = await svc.OrchestrateAsync(BuildRequest());
            await svc.AdvanceAsync(new OrchestrationAdvanceRequest { OrchestrationId = created.OrchestrationId });
            var events = svc.GetAuditEvents(created.OrchestrationId);
            Assert.That(events.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task Idempotency_SameKey_ReturnsSameOrchestrationId()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            var req = BuildRequest();
            req.IdempotencyKey = "contract-test-idem-key";
            var r1 = await svc.OrchestrateAsync(req);
            var r2 = await svc.OrchestrateAsync(req);
            Assert.That(r2.OrchestrationId, Is.EqualTo(r1.OrchestrationId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task Orchestrate_AllSupportedStandards_Succeed()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            foreach (var standard in new[] { "ASA", "ARC3", "ARC200", "ERC20", "ARC1400" })
            {
                var req = BuildRequest();
                req.TokenStandard = standard;
                var result = await svc.OrchestrateAsync(req);
                Assert.That(result.Success, Is.True, $"Expected success for standard {standard}");
            }
        }

        [Test]
        public async Task Orchestrate_AllSupportedNetworks_Succeed()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IDeterministicOrchestrationService>();
            foreach (var network in new[] { "mainnet", "testnet", "betanet", "voimain", "aramidmain", "base", "base-sepolia" })
            {
                var req = BuildRequest();
                req.Network = network;
                var result = await svc.OrchestrateAsync(req);
                Assert.That(result.Success, Is.True, $"Expected success for network {network}");
            }
        }

        // ── Helper ─────────────────────────────────────────────────────────────

        private static OrchestrationRequest BuildRequest() => new()
        {
            TokenName = "ContractTestToken",
            TokenStandard = "ASA",
            Network = "testnet",
            DeployerAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            CorrelationId = Guid.NewGuid().ToString(),
            MaxRetries = 3
        };
    }
}
