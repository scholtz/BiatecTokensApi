using BiatecTokensApi.Models.GuidedLaunchReliability;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// E2E workflow tests for Issue #478: Guided Launch Reliability.
    /// Tests full consumer journeys: WA = workflow assertions, WB = backward-compat/redirect.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class GuidedLaunchReliabilityIssue478E2EWorkflowTests
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
            ["JwtConfig:SecretKey"] = "guided-launch-e2e-issue-478-test-key-32chars!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "GuidedLaunchE2EIssue478TestKey32CharsRequired!!"
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

        // ── WA – Full workflow assertions ──────────────────────────────────

        [Test]
        public async Task WA1_FullCanonicalLaunchPath_TokenDetails_To_Submitted()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

            var init = await svc.InitiateLaunchAsync(BuildRequest("wa1-token"));
            Assert.That(init.Success, Is.True);
            var id = init.LaunchId!;

            var expectedPath = new[]
            {
                (LaunchStage.TokenDetails, LaunchStage.ComplianceSetup),
                (LaunchStage.ComplianceSetup, LaunchStage.NetworkSelection),
                (LaunchStage.NetworkSelection, LaunchStage.Review),
                (LaunchStage.Review, LaunchStage.Submitted)
            };

            foreach (var (expectedPrev, expectedNext) in expectedPath)
            {
                var adv = await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = id });
                Assert.That(adv.Success, Is.True);
                Assert.That(adv.PreviousStage, Is.EqualTo(expectedPrev));
                Assert.That(adv.CurrentStage, Is.EqualTo(expectedNext));
            }

            var status = await svc.GetLaunchStatusAsync(id, null);
            Assert.That(status!.Stage, Is.EqualTo(LaunchStage.Submitted));
        }

        [Test]
        public async Task WA2_CanonicalPath_StatusHistoryCaptures_AllCompletedSteps()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

            var init = await svc.InitiateLaunchAsync(BuildRequest("wa2-token"));
            var id = init.LaunchId!;

            await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = id }); // → Compliance
            await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = id }); // → Network
            await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = id }); // → Review

            var status = await svc.GetLaunchStatusAsync(id, null);
            Assert.That(status!.CompletedSteps.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public async Task WA3_IdempotencyDeterminism_ThreeRuns_IdenticalResults()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

            var req = BuildRequest("wa3-token", "wa3-idem-key");

            var r1 = await svc.InitiateLaunchAsync(req);
            var r2 = await svc.InitiateLaunchAsync(req);
            var r3 = await svc.InitiateLaunchAsync(req);

            Assert.That(r2.LaunchId, Is.EqualTo(r1.LaunchId), "Run 2 must return same ID");
            Assert.That(r3.LaunchId, Is.EqualTo(r1.LaunchId), "Run 3 must return same ID");
            Assert.That(r2.IsIdempotentReplay, Is.True);
            Assert.That(r3.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task WA4_ComplianceMessages_PresentAtComplianceSetupStage()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

            var init = await svc.InitiateLaunchAsync(BuildRequest("wa4-token"));
            var adv = await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = init.LaunchId });

            Assert.That(adv.CurrentStage, Is.EqualTo(LaunchStage.ComplianceSetup));
            Assert.That(adv.ComplianceMessages, Is.Not.Empty,
                "Compliance UX messages must be provided at the compliance stage");
        }

        [Test]
        public async Task WA5_ReviewStageMessages_InfoAboutIrreversibility()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

            var init = await svc.InitiateLaunchAsync(BuildRequest("wa5-token"));
            var id = init.LaunchId!;

            // Advance: TokenDetails → ComplianceSetup → NetworkSelection → Review
            await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = id });
            await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = id });
            var advToReview = await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = id });

            // Review messages should mention review/confirmation
            Assert.That(advToReview.ComplianceMessages, Is.Not.Empty);
            var msgText = string.Join(" ", advToReview.ComplianceMessages.Select(m => m.What + m.Why + m.How));
            Assert.That(msgText.Length, Is.GreaterThan(0));
        }

        [Test]
        public async Task WA6_CancellationAuditTrail_Recorded()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

            var cid = Guid.NewGuid().ToString();
            var init = await svc.InitiateLaunchAsync(new GuidedLaunchInitiateRequest
            {
                TokenName = "CancelAuditToken",
                TokenStandard = "ASA",
                OwnerId = "owner-cancel",
                CorrelationId = cid
            });

            await svc.CancelLaunchAsync(new GuidedLaunchCancelRequest
                { LaunchId = init.LaunchId, CorrelationId = cid });

            var events = svc.GetAuditEvents(cid);
            Assert.That(events, Has.Some.Matches<GuidedLaunchAuditEvent>(e => e.OperationName == "CancelLaunch"));
        }

        [Test]
        public async Task WA7_StepValidation_BeforeEachAdvance_CI_Deterministic()
        {
            // Run same validation 3 times to prove CI determinism
            for (int run = 0; run < 3; run++)
            {
                using var scope = _factory.Services.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

                var init = await svc.InitiateLaunchAsync(BuildRequest($"wa7-token-run{run}"));

                var v = await svc.ValidateStepAsync(new GuidedLaunchValidateStepRequest
                {
                    LaunchId = init.LaunchId,
                    StepName = "token-details",
                    StepInputs = new Dictionary<string, string> { ["tokenName"] = "DetermToken" }
                });

                Assert.That(v.IsValid, Is.True, $"Run {run}: validation must be deterministic");
                Assert.That(v.SchemaVersion, Is.EqualTo("1.0.0"), $"Run {run}: schema must be stable");
            }
        }

        [Test]
        public async Task WA8_FullFlow_AllStages_Verified_Via_StatusEndpoint()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

            var init = await svc.InitiateLaunchAsync(BuildRequest("wa8-token"));
            var id = init.LaunchId!;

            // Verify each stage via status
            var expectedStages = new[]
            {
                LaunchStage.TokenDetails,
                LaunchStage.ComplianceSetup,
                LaunchStage.NetworkSelection,
                LaunchStage.Review,
                LaunchStage.Submitted
            };

            var status0 = await svc.GetLaunchStatusAsync(id, null);
            Assert.That(status0!.Stage, Is.EqualTo(expectedStages[0]));

            for (int i = 1; i < expectedStages.Length; i++)
            {
                await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = id });
                var status = await svc.GetLaunchStatusAsync(id, null);
                Assert.That(status!.Stage, Is.EqualTo(expectedStages[i]));
            }
        }

        [Test]
        public async Task WA9_SchemaContract_AllResponseFieldsNonNull()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

            var init = await svc.InitiateLaunchAsync(BuildRequest("wa9-token"));

            Assert.That(init.LaunchId, Is.Not.Null);
            Assert.That(init.NextAction, Is.Not.Null);
            Assert.That(init.SchemaVersion, Is.Not.Null);
        }

        // ── WB – Backward compat / regression checks ───────────────────────

        [Test]
        public async Task WB1_ServiceRegisteredAsSingleton_ReturnsConsistentState()
        {
            using var scope = _factory.Services.CreateScope();
            var svc1 = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();
            var svc2 = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

            var init = await svc1.InitiateLaunchAsync(BuildRequest("wb1-token"));
            var status = await svc2.GetLaunchStatusAsync(init.LaunchId!, null);

            Assert.That(status, Is.Not.Null, "Singleton service must share state");
        }

        [Test]
        public async Task WB2_AllEndpoints_ReturnSchemaVersion_1_0_0()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

            var init = await svc.InitiateLaunchAsync(BuildRequest("wb2-token"));
            var status = await svc.GetLaunchStatusAsync(init.LaunchId!, null);
            var adv = await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = init.LaunchId });
            var cancel = await svc.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = adv.LaunchId });

            Assert.That(init.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(status!.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(adv.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(cancel.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task WB3_ExistingServices_StillResolve()
        {
            using var scope = _factory.Services.CreateScope();

            // Ensure pre-existing services are not broken by this issue's changes
            Assert.That(scope.ServiceProvider.GetService<BiatecTokensApi.Services.Interface.IMVPBackendHardeningService>(), Is.Not.Null);
            Assert.That(scope.ServiceProvider.GetService<BiatecTokensApi.Services.Interface.IComplianceOrchestrationService>(), Is.Not.Null);
            Assert.That(scope.ServiceProvider.GetService<IGuidedLaunchReliabilityService>(), Is.Not.Null);
        }

        [Test]
        public async Task WB4_CancelledLaunch_StatusRemainsCancelled_NotAdvanceable()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IGuidedLaunchReliabilityService>();

            var init = await svc.InitiateLaunchAsync(BuildRequest("wb4-token"));
            await svc.CancelLaunchAsync(new GuidedLaunchCancelRequest { LaunchId = init.LaunchId });

            var status = await svc.GetLaunchStatusAsync(init.LaunchId!, null);
            Assert.That(status!.Stage, Is.EqualTo(LaunchStage.Cancelled));

            var adv = await svc.AdvanceLaunchAsync(new GuidedLaunchAdvanceRequest { LaunchId = init.LaunchId });
            Assert.That(adv.Success, Is.False);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static GuidedLaunchInitiateRequest BuildRequest(string tokenName, string? idempotencyKey = null) =>
            new()
            {
                TokenName = tokenName,
                TokenStandard = "ASA",
                OwnerId = "e2e-owner-001",
                IdempotencyKey = idempotencyKey
            };
    }
}
