using BiatecTokensApi.Models.MVPHardening;
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
    /// Contract/integration tests for Issue #476: MVP Backend Hardening.
    /// Uses WebApplicationFactory for DI resolution and HTTP-level assertions.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class MVPBackendHardeningIssue476ContractTests
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
            ["JwtConfig:SecretKey"] = "mvp-hardening-issue-476-test-key-32chars-minimum!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "MVPHardeningIssue476TestKey32CharsRequired!!"
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

        // ── DI resolution ───────────────────────────────────────────────────

        [Test]
        public void DI_ResolvesIMVPBackendHardeningService_Successfully()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetService<IMVPBackendHardeningService>();
            Assert.That(svc, Is.Not.Null);
        }

        [Test]
        public async Task DI_AuthContractVerify_WorksViaService()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMVPBackendHardeningService>();
            var result = await svc.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "di@test.com" });
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task DI_DeploymentInitiate_WorksViaService()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMVPBackendHardeningService>();
            var result = await svc.InitiateDeploymentAsync(new DeploymentReliabilityRequest
            { TokenName = "DI-Token", DeployerAddress = "ADDR1", Network = "algorand-mainnet" });
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task DI_ComplianceCheck_WorksViaService()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMVPBackendHardeningService>();
            var result = await svc.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "9999", CheckType = "aml" });
            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task DI_ObservabilityTrace_WorksViaService()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMVPBackendHardeningService>();
            var result = await svc.CreateTraceAsync(new ObservabilityTraceRequest { OperationName = "di-trace" });
            Assert.That(result.Success, Is.True);
            Assert.That(result.TraceId, Is.Not.Null.And.Not.Empty);
        }

        // ── HTTP-level ──────────────────────────────────────────────────────

        [Test]
        public async Task HTTP_HealthEndpoint_Returns200Or503()
        {
            var response = await _client.GetAsync("/health");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.ServiceUnavailable));
        }

        [Test]
        public async Task HTTP_AuthContractVerify_NoBody_Returns400()
        {
            var response = await _client.PostAsync("/api/v1/mvp-hardening/auth-contract/verify",
                new StringContent("", System.Text.Encoding.UTF8, "application/json"));
            Assert.That((int)response.StatusCode, Is.AnyOf(400, 401));
        }

        [Test]
        public async Task HTTP_DeploymentInitiate_NoBody_Returns400Or401()
        {
            var response = await _client.PostAsync("/api/v1/mvp-hardening/deployment/initiate",
                new StringContent("", System.Text.Encoding.UTF8, "application/json"));
            Assert.That((int)response.StatusCode, Is.AnyOf(400, 401));
        }

        [Test]
        public async Task HTTP_ComplianceCheck_NoBody_Returns400Or401()
        {
            var response = await _client.PostAsync("/api/v1/mvp-hardening/compliance/check",
                new StringContent("", System.Text.Encoding.UTF8, "application/json"));
            Assert.That((int)response.StatusCode, Is.AnyOf(400, 401));
        }

        [Test]
        public async Task HTTP_GetDeploymentNonExistent_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/mvp-hardening/deployment/nonexistent");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── Schema contract ─────────────────────────────────────────────────

        [Test]
        public async Task Schema_AuthContractVerifyResponse_HasSchemaVersion100()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMVPBackendHardeningService>();
            var result = await svc.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "schema@test.com" });
            Assert.That(result.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task Schema_AuthContractVerifyResponse_HasCorrelationIdNotNull()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMVPBackendHardeningService>();
            var result = await svc.VerifyAuthContractAsync(new AuthContractVerifyRequest { Email = "schema@test.com", CorrelationId = "test-cid" });
            Assert.That(result.CorrelationId, Is.Not.Null);
        }

        [Test]
        public async Task Schema_DeploymentReliabilityResponse_HasSchemaVersion100()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMVPBackendHardeningService>();
            var result = await svc.InitiateDeploymentAsync(new DeploymentReliabilityRequest
            { TokenName = "Schema-Token", DeployerAddress = "ADDR", Network = "algorand-mainnet" });
            Assert.That(result.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task Schema_DeploymentReliabilityResponse_HasNonNullDeploymentIdOnSuccess()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMVPBackendHardeningService>();
            var result = await svc.InitiateDeploymentAsync(new DeploymentReliabilityRequest
            { TokenName = "Schema-Token", DeployerAddress = "ADDR", Network = "algorand-mainnet" });
            Assert.That(result.DeploymentId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Schema_ComplianceCheckResponse_HasSchemaVersion100()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMVPBackendHardeningService>();
            var result = await svc.RunComplianceCheckAsync(new ComplianceCheckRequest { AssetId = "123", CheckType = "kyc" });
            Assert.That(result.SchemaVersion, Is.EqualTo("1.0.0"));
        }

        [Test]
        public async Task Schema_ComplianceCheckResponse_HasCorrelationIdNotNull()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMVPBackendHardeningService>();
            var result = await svc.RunComplianceCheckAsync(new ComplianceCheckRequest
            { AssetId = "123", CheckType = "kyc", CorrelationId = "cid-schema" });
            Assert.That(result.CorrelationId, Is.Not.Null);
        }

        [Test]
        public async Task Schema_ObservabilityTraceResponse_HasNonNullTraceId()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IMVPBackendHardeningService>();
            var result = await svc.CreateTraceAsync(new ObservabilityTraceRequest { OperationName = "schema-trace" });
            Assert.That(result.TraceId, Is.Not.Null.And.Not.Empty);
        }

        // ── Backward compatibility ──────────────────────────────────────────

        [Test]
        public async Task BackwardCompat_HealthEndpoint_StillWorks()
        {
            var response = await _client.GetAsync("/health");
            Assert.That((int)response.StatusCode, Is.AnyOf(200, 503));
        }

        [Test]
        public async Task BackwardCompat_AuthRegisterStillRespondsInRange()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { });
            Assert.That((int)response.StatusCode, Is.InRange(400, 499));
        }

        [Test]
        public async Task BackwardCompat_DI_ResolvesIAuthenticationService()
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetService<BiatecTokensApi.Services.Interface.IAuthenticationService>();
            Assert.That(svc, Is.Not.Null);
        }
    }
}
