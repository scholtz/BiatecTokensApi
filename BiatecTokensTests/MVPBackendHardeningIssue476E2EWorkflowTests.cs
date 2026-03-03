using BiatecTokensApi.Models.MVPHardening;
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
    /// E2E workflow tests for Issue #476: MVP Backend Hardening.
    /// Pure service-layer tests – no HTTP calls except health regression.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class MVPBackendHardeningIssue476E2EWorkflowTests
    {
        private MVPBackendHardeningService _service = null!;
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
            ["JwtConfig:SecretKey"] = "mvp-hardening-e2e-476-test-key-32chars-minimum!!!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "MVPHardeningE2EIssue476TestKey32CharsRequired!!"
        };

        [SetUp]
        public void Setup()
        {
            var logger = new Mock<ILogger<MVPBackendHardeningService>>();
            _service = new MVPBackendHardeningService(logger.Object);

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

        // ── WA: Full auth-to-deployment E2E ────────────────────────────────

        [Test]
        public async Task WA_FullAuthToDeploymentE2E_AuditLogShowsAllEvents()
        {
            var cid = Guid.NewGuid().ToString();
            await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "wa@e2e.com", CorrelationId = cid });
            await _service.InitiateDeploymentAsync(new DeploymentReliabilityRequest
            { TokenName = "WA", DeployerAddress = "ADDR", Network = "algorand-mainnet", CorrelationId = cid });
            await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "1", CheckType = "kyc", CorrelationId = cid });
            await _service.CreateTraceAsync(new ObservabilityTraceRequest { OperationName = "wa", CorrelationId = cid });

            var events = _service.GetAuditEvents(cid);
            Assert.That(events.Count, Is.GreaterThanOrEqualTo(4));
        }

        // ── WB: Idempotency E2E ─────────────────────────────────────────────

        [Test]
        public async Task WB_IdempotencyE2E_SameDeploymentThreeTimes()
        {
            var req = new DeploymentReliabilityRequest
            { TokenName = "WB-Token", DeployerAddress = "ADDR", Network = "algorand-mainnet", IdempotencyKey = "wb-idem-e2e" };
            var r1 = await _service.InitiateDeploymentAsync(req);
            var r2 = await _service.InitiateDeploymentAsync(req);
            var r3 = await _service.InitiateDeploymentAsync(req);
            Assert.That(r1.DeploymentId, Is.EqualTo(r2.DeploymentId).And.EqualTo(r3.DeploymentId));
            Assert.That(r2.IsIdempotentReplay, Is.True);
            Assert.That(r3.IsIdempotentReplay, Is.True);
        }

        // ── WC: State machine E2E ───────────────────────────────────────────

        [Test]
        public async Task WC_DeploymentLifecycleStateMachineE2E_PendingToCompleted()
        {
            var r = await _service.InitiateDeploymentAsync(BuildRequest());
            Assert.That(r.Status, Is.EqualTo(DeploymentReliabilityStatus.Pending));
            await T(r.DeploymentId!, DeploymentReliabilityStatus.Accepted);
            await T(r.DeploymentId!, DeploymentReliabilityStatus.Queued);
            await T(r.DeploymentId!, DeploymentReliabilityStatus.Processing);
            var final = await T(r.DeploymentId!, DeploymentReliabilityStatus.Completed);
            Assert.That(final.Status, Is.EqualTo(DeploymentReliabilityStatus.Completed));
        }

        // ── WD: Failure and retry E2E ───────────────────────────────────────

        [Test]
        public async Task WD_DeploymentFailureAndRetryE2E()
        {
            var req = BuildRequest();
            req.MaxRetries = 3;
            var r = await _service.InitiateDeploymentAsync(req);
            await T(r.DeploymentId!, DeploymentReliabilityStatus.Accepted);
            await T(r.DeploymentId!, DeploymentReliabilityStatus.Queued);
            await T(r.DeploymentId!, DeploymentReliabilityStatus.Processing);
            await T(r.DeploymentId!, DeploymentReliabilityStatus.Failed);
            await T(r.DeploymentId!, DeploymentReliabilityStatus.Retrying);
            await T(r.DeploymentId!, DeploymentReliabilityStatus.Queued);
            await T(r.DeploymentId!, DeploymentReliabilityStatus.Processing);
            var final = await T(r.DeploymentId!, DeploymentReliabilityStatus.Completed);
            Assert.That(final.Status, Is.EqualTo(DeploymentReliabilityStatus.Completed));
        }

        // ── WE: Compliance check suite E2E ──────────────────────────────────

        [Test]
        public async Task WE_ComplianceCheckSuiteE2E_AllReturnNormalizedResponses()
        {
            var checkTypes = new[] { "kyc", "aml", "sanctions", "whitelist", "jurisdiction", "ownership" };
            foreach (var ct in checkTypes)
            {
                var result = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest
                { AssetId = "7777", CheckType = ct });
                Assert.That(result.Success, Is.True, $"Failed for checkType={ct}");
                Assert.That(result.SchemaVersion, Is.EqualTo("1.0.0"));
            }
        }

        // ── WF: Correlation ID propagation E2E ─────────────────────────────

        [Test]
        public async Task WF_CorrelationIdPropagationE2E_AllAuditEventsHaveSameCid()
        {
            var cid = Guid.NewGuid().ToString();
            await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "wf@e2e.com", CorrelationId = cid });
            await _service.InitiateDeploymentAsync(new DeploymentReliabilityRequest
            { TokenName = "WF", DeployerAddress = "ADDR", Network = "algorand-mainnet", CorrelationId = cid });
            await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "1", CheckType = "aml", CorrelationId = cid });
            await _service.CreateTraceAsync(new ObservabilityTraceRequest { OperationName = "wf", CorrelationId = cid });

            var events = _service.GetAuditEvents(cid);
            Assert.That(events.All(e => e.CorrelationId == cid), Is.True);
            Assert.That(events.Count, Is.GreaterThanOrEqualTo(4));
        }

        // ── WG: Error taxonomy E2E ──────────────────────────────────────────

        [Test]
        public async Task WG_ErrorTaxonomyE2E_StableErrorCodes()
        {
            var errMissingEmail = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = null });
            var errBadEmail = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "not-email" });
            var errMissingTokenName = await _service.InitiateDeploymentAsync(new DeploymentReliabilityRequest
            { DeployerAddress = "ADDR", Network = "net" });
            var errMissingAsset = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { CheckType = "kyc" });

            Assert.That(errMissingEmail.ErrorCode, Is.EqualTo("MISSING_EMAIL"));
            Assert.That(errBadEmail.ErrorCode, Is.EqualTo("INVALID_EMAIL_FORMAT"));
            Assert.That(errMissingTokenName.ErrorCode, Is.EqualTo("MISSING_TOKEN_NAME"));
            Assert.That(errMissingAsset.ErrorCode, Is.EqualTo("MISSING_ASSET_ID"));

            // All failures have remediation hints
            Assert.That(errMissingEmail.RemediationHint, Is.Not.Null.And.Not.Empty);
            Assert.That(errBadEmail.RemediationHint, Is.Not.Null.And.Not.Empty);
            Assert.That(errMissingTokenName.RemediationHint, Is.Not.Null.And.Not.Empty);
            Assert.That(errMissingAsset.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        // ── Concurrency ─────────────────────────────────────────────────────

        [Test]
        public async Task Concurrency_FiveParallelDeployments_GetUniqueIds()
        {
            var tasks = Enumerable.Range(0, 5).Select(i => _service.InitiateDeploymentAsync(new DeploymentReliabilityRequest
            { TokenName = $"Parallel-{i}", DeployerAddress = "ADDR", Network = "algorand-mainnet" }));
            var results = await Task.WhenAll(tasks);
            var ids = results.Select(r => r.DeploymentId).Distinct().ToList();
            Assert.That(ids.Count, Is.EqualTo(5));
        }

        // ── Schema stability ────────────────────────────────────────────────

        [Test]
        public async Task SchemaStability_SchemaVersionNeverNull_AlwaysV100()
        {
            var auth = await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "schema@e2e.com" });
            var dep = await _service.InitiateDeploymentAsync(BuildRequest());
            var comp = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "1", CheckType = "kyc" });
            var trace = await _service.CreateTraceAsync(new ObservabilityTraceRequest());
            Assert.That(auth.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(dep.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(comp.SchemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(trace.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        // ── Observability ───────────────────────────────────────────────────

        [Test]
        public async Task Observability_AuditLogGrowsWithEachOperation()
        {
            var before = _service.GetAuditEvents().Count;
            await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "obs@e2e.com" });
            await _service.InitiateDeploymentAsync(BuildRequest());
            var after = _service.GetAuditEvents().Count;
            Assert.That(after, Is.GreaterThan(before + 1));
        }

        [Test]
        public async Task Observability_AuditEventsOrderedByOccurredAt()
        {
            await _service.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "order@e2e.com" });
            await _service.InitiateDeploymentAsync(BuildRequest());
            var events = _service.GetAuditEvents();
            var ordered = events.OrderBy(e => e.OccurredAt).ToList();
            Assert.That(events.Select(e => e.EventId), Is.EqualTo(ordered.Select(e => e.EventId)));
        }

        // ── Regression ──────────────────────────────────────────────────────

        [Test]
        public async Task Regression_HealthCheckStillWorks()
        {
            using var client = _factory.CreateClient();
            var response = await client.GetAsync("/health");
            Assert.That((int)response.StatusCode, Is.AnyOf(200, 503));
        }

        // ── Backward compatibility ───────────────────────────────────────────

        [Test]
        public void BackwardCompat_DI_ResolvesIAuthenticationService()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetService<IAuthenticationService>();
            Assert.That(svc, Is.Not.Null);
        }

        // ── Additional ───────────────────────────────────────────────────────

        [Test]
        public async Task InvalidTransitionErrorCode_IsStableEnum()
        {
            var r = await _service.InitiateDeploymentAsync(BuildRequest());
            var t = await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = r.DeploymentId, TargetStatus = DeploymentReliabilityStatus.Completed });
            Assert.That(t.ErrorCode, Is.EqualTo("INVALID_STATE_TRANSITION"));
        }

        [Test]
        public async Task DeploymentNotFound_ReturnsAppropriateError()
        {
            var result = await _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = "does-not-exist", TargetStatus = DeploymentReliabilityStatus.Accepted });
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("DEPLOYMENT_NOT_FOUND"));
        }

        [Test]
        public async Task AllCheckTypesReturnSuccess_True()
        {
            foreach (var ct in new[] { "kyc", "aml", "sanctions" })
            {
                var r = await _service.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "X", CheckType = ct });
                Assert.That(r.Success, Is.True, $"Expected success for checkType={ct}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static DeploymentReliabilityRequest BuildRequest(string? key = null) => new()
        {
            TokenName = "E2EToken",
            TokenStandard = "ASA",
            DeployerAddress = "DEPLOYER",
            Network = "algorand-mainnet",
            IdempotencyKey = key,
            MaxRetries = 3
        };

        private Task<DeploymentReliabilityResponse> T(string id, DeploymentReliabilityStatus status)
            => _service.TransitionDeploymentStatusAsync(new DeploymentStatusTransitionRequest
            { DeploymentId = id, TargetStatus = status });
    }
}
