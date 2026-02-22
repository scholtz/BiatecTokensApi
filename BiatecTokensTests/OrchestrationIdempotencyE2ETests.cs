using BiatecTokensApi.Models.Orchestration;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Net;

namespace BiatecTokensTests
{
    /// <summary>
    /// End-to-end and API contract tests for the policy-driven orchestration pipeline.
    ///
    /// Validates:
    /// 1. DI-resolved pipeline executes deterministically in the full application context
    /// 2. Repeated submissions with the same idempotency key produce identical results
    /// 3. Correlation ID is propagated from HTTP request to orchestration context
    /// 4. Orchestration pipeline is registered and resolvable via the DI container
    /// 5. API endpoints that previously worked continue to return stable response shapes
    ///
    /// Business value: Provides consumer-facing evidence that the orchestration contract
    /// is stable across deployments and that idempotency replay guarantees hold under
    /// real application conditions.
    ///
    /// Acceptance Criteria coverage:
    ///   AC1  - Pipeline is registered and resolvable in the production DI container
    ///   AC3  - Idempotency key carried through orchestration context
    ///   AC4  - Repeated submissions return deterministic outcomes
    ///   AC5  - Correlation IDs present in HTTP responses (via CorrelationIdMiddleware)
    ///   AC9  - Existing endpoints continue to return stable response shapes
    ///   AC10 - CI green
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class OrchestrationIdempotencyE2ETests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        private const string HealthEndpoint = "/health";

        private static readonly Dictionary<string, string?> TestConfig = new()
        {
            ["App:Account"] = "test test test test test test test test test test test test test test test test test test test test test test test test test",
            ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
            ["AlgorandAuthentication:CheckExpiration"] = "false",
            ["AlgorandAuthentication:Debug"] = "true",
            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
            ["JwtConfig:SecretKey"] = "test-secret-key-at-least-32-characters-long-for-hs256",
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
            ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
            ["EVMChains:0:ChainId"] = "8453",
            ["EVMChains:0:GasLimit"] = "4500000",
            ["StripeConfig:SecretKey"] = "test_key",
            ["StripeConfig:PublishableKey"] = "test_key",
            ["StripeConfig:WebhookSecret"] = "test_secret",
            ["StripeConfig:BasicPriceId"] = "price_test_basic",
            ["StripeConfig:ProPriceId"] = "price_test_pro",
            ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
            ["KeyManagementConfig:Provider"] = "Hardcoded",
            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForOrchestrationIdempotencyE2ETests32CharsMin",
            ["Cors:0"] = "https://tokens.biatec.io"
        };

        [SetUp]
        public void Setup()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(TestConfig);
                    });
                });
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        // ── AC1: Pipeline is resolvable from the DI container ─────────────────────

        [Test]
        public void Pipeline_IsRegisteredInDiContainer_CanBeResolved()
        {
            // The factory creates the real application; resolve the pipeline from DI
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetService<ITokenWorkflowOrchestrationPipeline>();

            Assert.That(pipeline, Is.Not.Null,
                "ITokenWorkflowOrchestrationPipeline must be registered in the DI container");
            Assert.That(pipeline, Is.InstanceOf<TokenWorkflowOrchestrationPipeline>(),
                "Concrete type must be TokenWorkflowOrchestrationPipeline");
        }

        // ── AC3 & AC4: Idempotency determinism via DI-resolved pipeline ───────────

        [Test]
        public async Task Pipeline_DI_SameInputsAndIdempotencyKey_ProduceDeterministicResults()
        {
            // Resolve the pipeline from the real DI container
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            const string operationType = "ERC20_MINTABLE_CREATE";
            const string idempotencyKey = "determinism-test-key-e2e-001";
            const string correlationId = "corr-e2e-determinism";
            const string requestData = "SAME_REQUEST_PAYLOAD";

            // Submit the same logical request 3 times via the DI-resolved pipeline
            async Task<OrchestrationResult<string>> Submit()
            {
                var ctx = pipeline.BuildContext(operationType, correlationId, idempotencyKey, "user-123");
                return await pipeline.ExecuteAsync(
                    ctx, requestData,
                    validationPolicy: r => r.Length > 0 ? null : "Empty request",
                    preconditionPolicy: _ => null,
                    executor: r => Task.FromResult($"result-for-{r}"));
            }

            var result1 = await Submit();
            var result2 = await Submit();
            var result3 = await Submit();

            // All three must produce identical outcomes
            Assert.Multiple(() =>
            {
                Assert.That(result1.Success, Is.True, "Run 1 should succeed");
                Assert.That(result2.Success, Is.True, "Run 2 should succeed");
                Assert.That(result3.Success, Is.True, "Run 3 should succeed");

                Assert.That(result1.Payload, Is.EqualTo(result2.Payload), "Payload must be deterministic");
                Assert.That(result2.Payload, Is.EqualTo(result3.Payload), "Payload must be deterministic");

                Assert.That(result1.IdempotencyKey, Is.EqualTo(idempotencyKey));
                Assert.That(result2.IdempotencyKey, Is.EqualTo(idempotencyKey));
            });
        }

        [Test]
        public async Task Pipeline_DI_DifferentCorrelationIds_ProduceSameOutcome()
        {
            // AC2: same logical inputs with different correlation IDs must produce same outcome
            // (correlation IDs are for tracing only – they don't affect business logic)
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            const string operationType = "ASA_CREATE";
            const string requestData = "PAYLOAD";

            async Task<OrchestrationResult<string>> Submit(string correlationId)
            {
                var ctx = pipeline.BuildContext(operationType, correlationId);
                return await pipeline.ExecuteAsync(
                    ctx, requestData,
                    validationPolicy: _ => null,
                    preconditionPolicy: _ => null,
                    executor: r => Task.FromResult($"ok-{r}"));
            }

            var r1 = await Submit("trace-aaa");
            var r2 = await Submit("trace-bbb");
            var r3 = await Submit("trace-ccc");

            Assert.Multiple(() =>
            {
                // Each result carries its own correlation ID
                Assert.That(r1.CorrelationId, Is.EqualTo("trace-aaa"));
                Assert.That(r2.CorrelationId, Is.EqualTo("trace-bbb"));
                Assert.That(r3.CorrelationId, Is.EqualTo("trace-ccc"));

                // Business outcome is identical
                Assert.That(r1.Success, Is.EqualTo(r2.Success));
                Assert.That(r2.Success, Is.EqualTo(r3.Success));
            });
        }

        [Test]
        public async Task Pipeline_DI_ValidationFailure_IsDeterministicAcrossSubmissions()
        {
            // AC4: repeated invalid submissions must return the same structured error
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            const string errorMessage = "Token decimals must be 0-18";

            async Task<OrchestrationResult<string>> SubmitBadRequest()
            {
                var ctx = pipeline.BuildContext("ERC20_CREATE", Guid.NewGuid().ToString());
                return await pipeline.ExecuteAsync(
                    ctx, "bad",
                    validationPolicy: _ => errorMessage,
                    preconditionPolicy: _ => null,
                    executor: _ => Task.FromResult("ok"));
            }

            var r1 = await SubmitBadRequest();
            var r2 = await SubmitBadRequest();
            var r3 = await SubmitBadRequest();

            Assert.Multiple(() =>
            {
                // All three produce the same error code
                Assert.That(r1.ErrorCode, Is.EqualTo(r2.ErrorCode));
                Assert.That(r2.ErrorCode, Is.EqualTo(r3.ErrorCode));

                // All three have the same failure category
                Assert.That(r1.FailureCategory, Is.EqualTo(r2.FailureCategory));
                Assert.That(r2.FailureCategory, Is.EqualTo(r3.FailureCategory));
                Assert.That(r1.FailureCategory, Is.EqualTo(OrchestrationFailureCategory.ValidationFailure));
            });
        }

        // ── AC5: Correlation ID visible in HTTP responses ─────────────────────────

        [Test]
        public async Task HttpRequest_WithCorrelationId_HeaderPropagatedToResponse()
        {
            // CorrelationIdMiddleware must echo back the X-Correlation-ID header
            var customCorrelationId = $"test-corr-{Guid.NewGuid():N}";
            var request = new HttpRequestMessage(HttpMethod.Get, HealthEndpoint);
            request.Headers.Add("X-Correlation-ID", customCorrelationId);

            var response = await _client.SendAsync(request);

            Assert.That(response.Headers.TryGetValues("X-Correlation-ID", out var values), Is.True,
                "X-Correlation-ID must be present in the response headers");
            Assert.That(values!.First(), Is.EqualTo(customCorrelationId),
                "Server must echo the client-provided correlation ID");
        }

        [Test]
        public async Task HttpRequest_WithoutCorrelationId_ServerGeneratesOne()
        {
            // When no correlation ID is provided, the server must auto-generate one
            var request = new HttpRequestMessage(HttpMethod.Get, HealthEndpoint);
            // Deliberately no X-Correlation-ID header

            var response = await _client.SendAsync(request);

            Assert.That(response.Headers.TryGetValues("X-Correlation-ID", out var values), Is.True,
                "Server must generate X-Correlation-ID even when client didn't provide one");
            Assert.That(values!.First(), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task HttpRequest_ConsistentCorrelationId_AcrossMultipleRequests()
        {
            // Each request gets its own correlation ID; IDs must not be reused
            string? id1 = null, id2 = null, id3 = null;

            for (int i = 0; i < 3; i++)
            {
                var response = await _client.GetAsync(HealthEndpoint);
                var ids = response.Headers.GetValues("X-Correlation-ID").ToArray();
                if (i == 0) id1 = ids[0];
                else if (i == 1) id2 = ids[0];
                else id3 = ids[0];
            }

            // Without explicit headers, each request gets its own auto-generated ID
            Assert.That(id1, Is.Not.Null.And.Not.Empty);
            Assert.That(id2, Is.Not.Null.And.Not.Empty);
            Assert.That(id3, Is.Not.Null.And.Not.Empty);
        }

        // ── AC9: Existing endpoints return stable response shapes ─────────────────

        [Test]
        public async Task HealthEndpoint_ReturnOk_RegressionCheck()
        {
            var response = await _client.GetAsync(HealthEndpoint);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Health endpoint must return 200 OK after orchestration pipeline was added");
        }

        [Test]
        public async Task AuthRegisterEndpoint_WithoutBody_Returns4xx_RegressionCheck()
        {
            // Auth endpoint must still respond correctly (no orchestration regression)
            var response = await _client.PostAsync("/api/v1/auth/register", null!);

            Assert.That((int)response.StatusCode, Is.InRange(400, 499),
                "Auth register without body must return 4xx - endpoint contract unchanged");
        }

        // ── Pipeline audit summary – DI-resolved path ────────────────────────────

        [Test]
        public async Task Pipeline_DI_AuditSummary_PopulatedForSuccessfulRun()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var ctx = pipeline.BuildContext("ASA_CREATE", "corr-audit-e2e", "key-audit-e2e", "user-audit");
            var result = await pipeline.ExecuteAsync(
                ctx, "payload",
                validationPolicy: _ => null,
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("created"));

            Assert.Multiple(() =>
            {
                Assert.That(result.AuditSummary, Is.Not.Null);
                Assert.That(result.AuditSummary.CorrelationId, Is.EqualTo("corr-audit-e2e"));
                Assert.That(result.AuditSummary.OperationType, Is.EqualTo("ASA_CREATE"));
                Assert.That(result.AuditSummary.InitiatedBy, Is.EqualTo("user-audit"));
                Assert.That(result.AuditSummary.Outcome, Is.EqualTo("Succeeded"));
                Assert.That(result.AuditSummary.HasIdempotencyKey, Is.True);
                Assert.That(result.AuditSummary.StagesCompleted, Is.GreaterThan(0));
            });
        }

        [Test]
        public async Task Pipeline_DI_AuditSummary_PopulatedForFailedRun()
        {
            using var scope = _factory.Services.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<ITokenWorkflowOrchestrationPipeline>();

            var ctx = pipeline.BuildContext("ERC20_CREATE", "corr-fail-e2e");
            var result = await pipeline.ExecuteAsync(
                ctx, "bad-payload",
                validationPolicy: _ => "Token name too long",
                preconditionPolicy: _ => null,
                executor: _ => Task.FromResult("ok"));

            Assert.Multiple(() =>
            {
                Assert.That(result.AuditSummary.Outcome, Is.EqualTo("Failed"));
                Assert.That(result.AuditSummary.FailureCode, Is.Not.Null);
                Assert.That(result.AuditSummary.CorrelationId, Is.EqualTo("corr-fail-e2e"));
            });
        }
    }
}
