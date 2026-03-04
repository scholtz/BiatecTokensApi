using BiatecTokensApi.Models.GuidedLaunchReliability;
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
    /// Contract/integration tests for Issue #478: Guided Launch Reliability.
    /// Uses WebApplicationFactory for DI resolution and HTTP-level assertions.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class GuidedLaunchReliabilityIssue478ContractTests
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
            ["JwtConfig:SecretKey"] = "guided-launch-issue-478-test-key-32chars-minimum!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "GuidedLaunchIssue478TestKey32CharsRequired!!"
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

        // ── DI / application wiring ────────────────────────────────────────

        [Test]
        public void DI_GuidedLaunchReliabilityService_Resolves()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetService<IGuidedLaunchReliabilityService>();
            Assert.That(svc, Is.Not.Null);
        }

        // ── Unauthenticated HTTP access ────────────────────────────────────

        [Test]
        public async Task POST_Initiate_Unauthenticated_Returns401()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/guided-launch/initiate",
                new GuidedLaunchInitiateRequest { TokenName = "T", TokenStandard = "ASA", OwnerId = "o1" });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GET_Status_Unauthenticated_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/guided-launch/status/some-id");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task POST_Advance_Unauthenticated_Returns401()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/guided-launch/advance",
                new GuidedLaunchAdvanceRequest { LaunchId = "some-id" });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task POST_ValidateStep_Unauthenticated_Returns401()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/guided-launch/validate-step",
                new GuidedLaunchValidateStepRequest { LaunchId = "x", StepName = "token-details" });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task POST_Cancel_Unauthenticated_Returns401()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/guided-launch/cancel",
                new GuidedLaunchCancelRequest { LaunchId = "some-id" });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── Service-layer contract assertions ──────────────────────────────

        [Test]
        public async Task Initiate_ValidRequest_ReturnsSuccessWithLaunchId()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var result = await svc.InitiateLaunchAsync(BuildInitiateRequest());
            Assert.That(result.Success, Is.True);
            Assert.That(result.LaunchId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Stage, Is.EqualTo(LaunchStage.TokenDetails));
        }

        [Test]
        public async Task Initiate_MissingTokenName_Returns_MISSING_TOKEN_NAME()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var req = BuildInitiateRequest();
            req.TokenName = null;
            var result = await svc.InitiateLaunchAsync(req);
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
            Assert.That(result.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_InvalidStandard_Returns_INVALID_TOKEN_STANDARD()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var req = BuildInitiateRequest();
            req.TokenStandard = "BOGUS";
            var result = await svc.InitiateLaunchAsync(req);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_TOKEN_STANDARD"));
        }

        [Test]
        public async Task GetStatus_AfterInitiate_ReturnsCorrectStage()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var r = await svc.InitiateLaunchAsync(BuildInitiateRequest());
            var status = await svc.GetLaunchStatusAsync(r.LaunchId!, null);
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.Stage, Is.EqualTo(LaunchStage.TokenDetails));
        }

        [Test]
        public async Task GetStatus_UnknownId_ReturnsNull()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var status = await svc.GetLaunchStatusAsync("ghost", null);
            Assert.That(status, Is.Null);
        }

        [Test]
        public async Task Advance_ValidLaunch_AdvancesStage()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var r = await svc.InitiateLaunchAsync(BuildInitiateRequest());
            var adv = await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = r.LaunchId });
            Assert.That(adv.Success, Is.True);
            Assert.That(adv.CurrentStage, Is.EqualTo(LaunchStage.ComplianceSetup));
        }

        [Test]
        public async Task Advance_UnknownId_Returns_LAUNCH_NOT_FOUND()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var adv = await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = "ghost" });
            Assert.That(adv.ErrorCode, Is.EqualTo("LAUNCH_NOT_FOUND"));
        }

        [Test]
        public async Task ValidateStep_TokenDetails_WithName_IsValid()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var r = await svc.InitiateLaunchAsync(BuildInitiateRequest());
            var validate = await svc.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = r.LaunchId,
                StepName = "token-details",
                StepInputs = new Dictionary<string, string> { ["tokenName"] = "MyToken" }
            });
            Assert.That(validate.IsValid, Is.True);
        }

        [Test]
        public async Task ValidateStep_InvalidStep_Returns_INVALID_STEP_NAME()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var r = await svc.InitiateLaunchAsync(BuildInitiateRequest());
            var validate = await svc.ValidateStepAsync(new GuidedLaunchValidateStepRequest
            {
                LaunchId = r.LaunchId,
                StepName = "bad-step"
            });
            Assert.That(validate.ErrorCode, Is.EqualTo("INVALID_STEP_NAME"));
        }

        [Test]
        public async Task Cancel_ActiveLaunch_SetsCancelledStage()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var r = await svc.InitiateLaunchAsync(BuildInitiateRequest());
            var cancel = await svc.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = r.LaunchId });
            Assert.That(cancel.Success, Is.True);
            Assert.That(cancel.FinalStage, Is.EqualTo(LaunchStage.Cancelled));
        }

        [Test]
        public async Task Cancel_UnknownId_Returns_LAUNCH_NOT_FOUND()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var cancel = await svc.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = "ghost" });
            Assert.That(cancel.ErrorCode, Is.EqualTo("LAUNCH_NOT_FOUND"));
        }

        [Test]
        public async Task IdempotentReplay_ReturnsSameLaunchId()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var req = BuildInitiateRequest("idem-contract-key");
            var r1 = await svc.InitiateLaunchAsync(req);
            var r2 = await svc.InitiateLaunchAsync(req);
            Assert.That(r2.LaunchId, Is.EqualTo(r1.LaunchId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task AuditEvents_AfterInitiate_CorrelationMatches()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var cid = Guid.NewGuid().ToString();
            var req = BuildInitiateRequest();
            req.CorrelationId = cid;
            await svc.InitiateLaunchAsync(req);
            var events = svc.GetAuditEvents(cid);
            Assert.That(events, Is.Not.Empty);
        }

        [Test]
        public async Task ComplianceMessages_HaveWhatWhyHow()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var r = await svc.InitiateLaunchAsync(BuildInitiateRequest());
            var adv = await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = r.LaunchId });
            foreach (var msg in adv.ComplianceMessages)
            {
                Assert.That(msg.What, Is.Not.Null.And.Not.Empty, "What");
                Assert.That(msg.Why, Is.Not.Null.And.Not.Empty, "Why");
                Assert.That(msg.How, Is.Not.Null.And.Not.Empty, "How");
            }
        }

        [Test]
        public async Task SchemaVersion_IsAlways_1_0_0()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var r = await svc.InitiateLaunchAsync(BuildInitiateRequest());
            Assert.That(r.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task HealthEndpoint_Returns200Or503()
        {
            var response = await _client.GetAsync("/health");
            Assert.That((int)response.StatusCode, Is.EqualTo(200).Or.EqualTo(503));
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static GuidedLaunchInitiateRequest BuildInitiateRequest(string? idempotencyKey = null) =>
            new()
            {
                TokenName = "ContractToken",
                TokenStandard = "ASA",
                OwnerId = "contract-owner-001",
                IdempotencyKey = idempotencyKey
            };
    }
}
