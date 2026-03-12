using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.TokenDeploymentLifecycle;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the Token Deployment Lifecycle endpoints.
    ///
    /// These tests validate the HTTP contract for:
    /// - POST /api/v1/token-deployment-lifecycle/initiate
    /// - GET  /api/v1/token-deployment-lifecycle/status/{deploymentId}
    /// - POST /api/v1/token-deployment-lifecycle/retry
    /// - GET  /api/v1/token-deployment-lifecycle/telemetry/{deploymentId}
    /// - POST /api/v1/token-deployment-lifecycle/validate
    /// - POST /api/v1/token-deployment-lifecycle/guardrails
    ///
    /// Focus: blocker-grade deployment lifecycle evidence including
    /// durable identifiers, state transitions, idempotency, error taxonomy,
    /// and telemetry contracts required for regulated token issuance.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TokenDeploymentLifecycleIntegrationTests
    {
        private LifecycleTestWebApplicationFactory _factory = null!;
        private HttpClient _unauthClient = null!;
        private HttpClient _authClient = null!;

        private const string ValidAlgorandAddress = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAY5HFKQ";
        private const string ValidEvmAddress      = "0x1234567890123456789012345678901234567890";

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            _factory = new LifecycleTestWebApplicationFactory();
            _unauthClient = _factory.CreateClient();

            var email = $"lifecycle-test-{Guid.NewGuid():N}@biatec-lifecycle-test.example.com";
            var regReq = new RegisterRequest
            {
                Email           = email,
                Password        = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName        = "Lifecycle Integration Test User"
            };
            var regResp = await _unauthClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
            var regBody = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();

            _authClient = _factory.CreateClient();
            _authClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", regBody?.AccessToken ?? string.Empty);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _unauthClient?.Dispose();
            _authClient?.Dispose();
            _factory?.Dispose();
        }

        private sealed class LifecycleTestWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "LifecycleTestKey32CharsMinimumRequired!!",
                        ["JwtConfig:SecretKey"] = "LifecycleIntegrationTestSecretKey32CharsRequired!!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
                        ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                        ["AlgorandAuthentication:CheckExpiration"] = "false",
                        ["AlgorandAuthentication:Debug"] = "true",
                        ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                        ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                        ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                        ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                        ["IPFSConfig:TimeoutSeconds"] = "30",
                        ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                        ["IPFSConfig:ValidateContentHash"] = "true",
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io"
                    });
                });
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static TokenDeploymentLifecycleRequest BuildValidRequest(
            string? idempotencyKey = null,
            string tokenName       = "Integration Test Token",
            string tokenSymbol     = "ITT",
            string standard        = "ASA",
            string network         = "algorand-testnet",
            ulong  totalSupply     = 1_000_000,
            int    decimals        = 6,
            string? creatorAddress = null)
        {
            return new TokenDeploymentLifecycleRequest
            {
                IdempotencyKey   = idempotencyKey,
                CorrelationId    = Guid.NewGuid().ToString(),
                TokenStandard    = standard,
                TokenName        = tokenName,
                TokenSymbol      = tokenSymbol,
                Network          = network,
                TotalSupply      = totalSupply,
                Decimals         = decimals,
                CreatorAddress   = creatorAddress ?? ValidAlgorandAddress,
                MaxRetryAttempts = 3,
                TimeoutSeconds   = 120,
            };
        }

        // ── POST /initiate: authentication ─────────────────────────────────────

        [Test]
        public async Task Initiate_WithoutAuth_Returns401()
        {
            var req = BuildValidRequest();
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ── POST /initiate: happy path ─────────────────────────────────────────

        [Test]
        public async Task Initiate_ValidRequest_Returns200()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsDurableDeploymentId()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.DeploymentId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsIdempotencyKey()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.IdempotencyKey, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsCompletedStage()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.Stage, Is.EqualTo(DeploymentStage.Completed));
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsSuccessOutcome()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.Outcome, Is.EqualTo(DeploymentOutcome.Success));
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsNonZeroAssetId()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.AssetId, Is.Not.Null);
            Assert.That(body.AssetId!.Value, Is.GreaterThan(0UL));
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsTransactionId()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.TransactionId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsConfirmedRound()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.ConfirmedRound, Is.Not.Null);
            Assert.That(body.ConfirmedRound!.Value, Is.GreaterThan(0UL));
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsTelemetryEvents()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.TelemetryEvents, Is.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsValidationResults()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.ValidationResults, Is.Not.Empty);
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsProgress()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.Progress, Is.Not.Null);
            Assert.That(body.Progress.PercentComplete, Is.EqualTo(100));
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsCorrelationId()
        {
            var correlationId = Guid.NewGuid().ToString();
            var req = BuildValidRequest();
            req.CorrelationId = correlationId;
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.CorrelationId, Is.EqualTo(correlationId));
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsInitiatedAtTimestamp()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.InitiatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public async Task Initiate_ValidRequest_ReturnsSchemaVersion()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        // ── POST /initiate: null / invalid body ────────────────────────────────

        [Test]
        public async Task Initiate_NullBody_Returns400()
        {
            var resp = await _authClient.PostAsync("/api/v1/token-deployment-lifecycle/initiate",
                new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Initiate_MissingTokenStandard_Returns200WithFailedStage()
        {
            var req = BuildValidRequest();
            req.TokenStandard = string.Empty;
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_UnsupportedTokenStandard_Returns200WithFailedStage()
        {
            var req = BuildValidRequest();
            req.TokenStandard = "UNSUPPORTED_STANDARD_XYZ";
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        [Test]
        public async Task Initiate_UnsupportedTokenStandard_ReturnsValidationErrors()
        {
            var req = BuildValidRequest();
            req.TokenStandard = "UNSUPPORTED_XYZ";
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.ValidationResults.Any(v => !v.IsValid && v.FieldName == "TokenStandard"),
                Is.True, "Should expose TokenStandard validation failure.");
        }

        [Test]
        public async Task Initiate_ZeroTotalSupply_Returns200WithValidationError()
        {
            var req = BuildValidRequest(totalSupply: 0);
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(body.ValidationResults.Any(v => v.ErrorCode == "SUPPLY_ZERO"), Is.True);
        }

        [Test]
        public async Task Initiate_InvalidNetwork_Returns200WithNetworkError()
        {
            var req = BuildValidRequest(network: "unsupported-network-xyz");
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(body.ValidationResults.Any(v => v.ErrorCode == "NETWORK_UNSUPPORTED"), Is.True);
        }

        [Test]
        public async Task Initiate_ValidationFailure_OutcomeIsTerminalFailure()
        {
            var req = BuildValidRequest(totalSupply: 0);
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
        }

        [Test]
        public async Task Initiate_ValidationFailure_RemediationHintPresent()
        {
            var req = BuildValidRequest(totalSupply: 0);
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.RemediationHint, Is.Not.Null.And.Not.Empty);
        }

        // ── POST /initiate: idempotency ────────────────────────────────────────

        [Test]
        public async Task Initiate_SameIdempotencyKey_Returns200WithCachedResult()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildValidRequest(idempotencyKey: key);
            var resp1 = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var resp2 = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body1 = await resp1.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            var body2 = await resp2.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(resp1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(resp2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body2!.DeploymentId, Is.EqualTo(body1!.DeploymentId));
        }

        [Test]
        public async Task Initiate_SameIdempotencyKey_SecondCallIsIdempotentReplay()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildValidRequest(idempotencyKey: key);
            await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var resp2 = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body2 = await resp2.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body2!.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task Initiate_SameIdempotencyKey_ThreeTimesDeterministic()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildValidRequest(idempotencyKey: key);
            var r1 = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            var r2 = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            var r3 = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(r2!.AssetId, Is.EqualTo(r1!.AssetId));
            Assert.That(r3!.AssetId, Is.EqualTo(r1!.AssetId));
            Assert.That(r2.TransactionId, Is.EqualTo(r1.TransactionId));
            Assert.That(r3.TransactionId, Is.EqualTo(r1.TransactionId));
        }

        [Test]
        public async Task Initiate_DifferentIdempotencyKeys_CreateDifferentDeploymentIds()
        {
            var req1 = BuildValidRequest(idempotencyKey: Guid.NewGuid().ToString());
            var req2 = BuildValidRequest(idempotencyKey: Guid.NewGuid().ToString());
            var r1 = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req1))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            var r2 = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req2))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(r2!.DeploymentId, Is.Not.EqualTo(r1!.DeploymentId));
        }

        // ── GET /status/{deploymentId} ─────────────────────────────────────────

        [Test]
        public async Task GetStatus_WithoutAuth_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/token-deployment-lifecycle/status/some-deployment-id");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetStatus_ExistingDeployment_Returns200()
        {
            var req = BuildValidRequest();
            var initiated = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();

            var statusResp = await _authClient.GetAsync($"/api/v1/token-deployment-lifecycle/status/{initiated!.DeploymentId}");
            Assert.That(statusResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GetStatus_ExistingDeployment_ReturnsMatchingDeploymentId()
        {
            var req = BuildValidRequest();
            var initiated = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();

            var status = await (await _authClient.GetAsync($"/api/v1/token-deployment-lifecycle/status/{initiated!.DeploymentId}"))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(status!.DeploymentId, Is.EqualTo(initiated.DeploymentId));
        }

        [Test]
        public async Task GetStatus_ExistingDeployment_ReturnsMatchingStage()
        {
            var req = BuildValidRequest();
            var initiated = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();

            var status = await (await _authClient.GetAsync($"/api/v1/token-deployment-lifecycle/status/{initiated!.DeploymentId}"))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(status!.Stage, Is.EqualTo(initiated.Stage));
        }

        [Test]
        public async Task GetStatus_ExistingDeployment_ReturnsMatchingAssetId()
        {
            var req = BuildValidRequest();
            var initiated = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();

            var status = await (await _authClient.GetAsync($"/api/v1/token-deployment-lifecycle/status/{initiated!.DeploymentId}"))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(status!.AssetId, Is.EqualTo(initiated.AssetId));
        }

        [Test]
        public async Task GetStatus_NonExistentDeployment_Returns404()
        {
            var resp = await _authClient.GetAsync("/api/v1/token-deployment-lifecycle/status/nonexistent-deployment-xyz-999");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task GetStatus_StableAcrossPolls_StateDoesNotRegress()
        {
            var req = BuildValidRequest();
            var initiated = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();

            for (int i = 0; i < 3; i++)
            {
                var status = await (await _authClient.GetAsync($"/api/v1/token-deployment-lifecycle/status/{initiated!.DeploymentId}"))
                    .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
                Assert.That(status!.Stage, Is.EqualTo(DeploymentStage.Completed),
                    $"Poll #{i + 1}: state must not regress.");
            }
        }

        // ── GET /telemetry/{deploymentId} ─────────────────────────────────────

        [Test]
        public async Task GetTelemetry_WithoutAuth_Returns401()
        {
            var resp = await _unauthClient.GetAsync("/api/v1/token-deployment-lifecycle/telemetry/some-id");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task GetTelemetry_ExistingDeployment_Returns200()
        {
            var req = BuildValidRequest();
            var initiated = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();

            var telemetryResp = await _authClient.GetAsync($"/api/v1/token-deployment-lifecycle/telemetry/{initiated!.DeploymentId}");
            Assert.That(telemetryResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task GetTelemetry_ExistingDeployment_ReturnsNonEmptyList()
        {
            var req = BuildValidRequest();
            var initiated = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();

            var events = await (await _authClient.GetAsync($"/api/v1/token-deployment-lifecycle/telemetry/{initiated!.DeploymentId}"))
                .Content.ReadFromJsonAsync<List<DeploymentTelemetryEvent>>();
            Assert.That(events, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task GetTelemetry_ExistingDeployment_ContainsCompletionEvent()
        {
            var req = BuildValidRequest();
            var initiated = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();

            var events = await (await _authClient.GetAsync($"/api/v1/token-deployment-lifecycle/telemetry/{initiated!.DeploymentId}"))
                .Content.ReadFromJsonAsync<List<DeploymentTelemetryEvent>>();
            Assert.That(events!.Any(e => e.EventType == TelemetryEventType.CompletionSuccess), Is.True);
        }

        [Test]
        public async Task GetTelemetry_ExistingDeployment_AllEventsHaveDeploymentId()
        {
            var req = BuildValidRequest();
            var initiated = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();

            var events = await (await _authClient.GetAsync($"/api/v1/token-deployment-lifecycle/telemetry/{initiated!.DeploymentId}"))
                .Content.ReadFromJsonAsync<List<DeploymentTelemetryEvent>>();
            Assert.That(events!.All(e => e.DeploymentId == initiated.DeploymentId), Is.True);
        }

        [Test]
        public async Task GetTelemetry_NonExistentDeployment_Returns200WithEmptyList()
        {
            var events = await (await _authClient.GetAsync("/api/v1/token-deployment-lifecycle/telemetry/nonexistent-id-xyz"))
                .Content.ReadFromJsonAsync<List<DeploymentTelemetryEvent>>();
            Assert.That(events, Is.Not.Null);
            Assert.That(events!, Is.Empty);
        }

        // ── POST /validate ─────────────────────────────────────────────────────

        [Test]
        public async Task Validate_WithoutAuth_Returns401()
        {
            var req = new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Test",
                TokenSymbol    = "TST",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/validate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Validate_ValidRequest_Returns200()
        {
            var req = new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/validate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task Validate_ValidRequest_ReturnsIsValidTrue()
        {
            var req = new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/validate", req);
            var body = await resp.Content.ReadFromJsonAsync<DeploymentValidationResponse>();
            Assert.That(body!.IsValid, Is.True);
        }

        [Test]
        public async Task Validate_ValidRequest_ReturnsResultList()
        {
            var req = new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/validate", req);
            var body = await resp.Content.ReadFromJsonAsync<DeploymentValidationResponse>();
            Assert.That(body!.Results, Is.Not.Empty);
        }

        [Test]
        public async Task Validate_ValidRequest_HasSummary()
        {
            var req = new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/validate", req);
            var body = await resp.Content.ReadFromJsonAsync<DeploymentValidationResponse>();
            Assert.That(body!.Summary, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Validate_ValidRequest_HasSchemaVersion()
        {
            var req = new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Valid Token",
                TokenSymbol    = "VT",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/validate", req);
            var body = await resp.Content.ReadFromJsonAsync<DeploymentValidationResponse>();
            Assert.That(body!.SchemaVersion, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Validate_NullBody_Returns400()
        {
            var resp = await _authClient.PostAsync("/api/v1/token-deployment-lifecycle/validate",
                new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Validate_MissingRequiredFields_Returns200WithErrors()
        {
            var req = new DeploymentValidationRequest
            {
                TokenStandard  = string.Empty,
                TokenName      = string.Empty,
                TokenSymbol    = string.Empty,
                Network        = string.Empty,
                TotalSupply    = 0,
                Decimals       = 0,
                CreatorAddress = string.Empty,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/validate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<DeploymentValidationResponse>();
            Assert.That(body!.IsValid, Is.False);
            Assert.That(body.Results.Any(r => !r.IsValid), Is.True);
        }

        [Test]
        public async Task Validate_ZeroSupply_ReturnsSupplyZeroErrorCode()
        {
            var req = new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Token",
                TokenSymbol    = "TKN",
                Network        = "algorand-testnet",
                TotalSupply    = 0,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/validate", req);
            var body = await resp.Content.ReadFromJsonAsync<DeploymentValidationResponse>();
            Assert.That(body!.Results.Any(r => r.ErrorCode == "SUPPLY_ZERO"), Is.True);
        }

        [Test]
        public async Task Validate_InvalidNetwork_ReturnsNetworkUnsupportedError()
        {
            var req = new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "Token",
                TokenSymbol    = "TKN",
                Network        = "unsupported-network-xyz",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/validate", req);
            var body = await resp.Content.ReadFromJsonAsync<DeploymentValidationResponse>();
            Assert.That(body!.Results.Any(r => r.ErrorCode == "NETWORK_UNSUPPORTED"), Is.True);
        }

        [Test]
        public async Task Validate_CorrelationIdEchoed()
        {
            var correlationId = Guid.NewGuid().ToString();
            var req = new DeploymentValidationRequest
            {
                CorrelationId  = correlationId,
                TokenStandard  = "ASA",
                TokenName      = "Token",
                TokenSymbol    = "TKN",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 0,
                CreatorAddress = ValidAlgorandAddress,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/validate", req);
            var body = await resp.Content.ReadFromJsonAsync<DeploymentValidationResponse>();
            Assert.That(body!.CorrelationId, Is.EqualTo(correlationId));
        }

        // ── POST /guardrails ───────────────────────────────────────────────────

        [Test]
        public async Task Guardrails_WithoutAuth_Returns401()
        {
            var ctx = new GuardrailEvaluationContext
            {
                NodeReachable       = true,
                CreatorAddressValid = true,
            };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/guardrails", ctx);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Guardrails_AllClear_Returns200WithEmptyList()
        {
            var ctx = new GuardrailEvaluationContext
            {
                TokenStandard        = "ASA",
                Network              = "algorand-testnet",
                NodeReachable        = true,
                CreatorAddressValid  = true,
                RequiresIpfs         = false,
                IsTimedOut           = false,
                HasInFlightDuplicate = false,
                ConflictingDeploymentDetected = false,
                RetryCount           = 0,
                MaxRetryAttempts     = 3,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/guardrails", ctx);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var findings = await resp.Content.ReadFromJsonAsync<List<ReliabilityGuardrail>>();
            Assert.That(findings, Is.Not.Null);
            Assert.That(findings!, Is.Empty);
        }

        [Test]
        public async Task Guardrails_NodeUnreachable_ReturnsBlockingGR001()
        {
            var ctx = new GuardrailEvaluationContext
            {
                NodeReachable       = false,
                CreatorAddressValid = true,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/guardrails", ctx);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var findings = await resp.Content.ReadFromJsonAsync<List<ReliabilityGuardrail>>();
            Assert.That(findings!.Any(g => g.GuardrailId == "GR-001" && g.IsBlocking), Is.True);
        }

        [Test]
        public async Task Guardrails_RetryLimitExceeded_ReturnsBlockingGR002()
        {
            var ctx = new GuardrailEvaluationContext
            {
                NodeReachable        = true,
                CreatorAddressValid  = true,
                RetryCount           = 3,
                MaxRetryAttempts     = 3,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/guardrails", ctx);
            var findings = await resp.Content.ReadFromJsonAsync<List<ReliabilityGuardrail>>();
            Assert.That(findings!.Any(g => g.GuardrailId == "GR-002" && g.IsBlocking), Is.True);
        }

        [Test]
        public async Task Guardrails_TimedOut_ReturnsBlockingGR003()
        {
            var ctx = new GuardrailEvaluationContext
            {
                NodeReachable        = true,
                CreatorAddressValid  = true,
                IsTimedOut           = true,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/guardrails", ctx);
            var findings = await resp.Content.ReadFromJsonAsync<List<ReliabilityGuardrail>>();
            Assert.That(findings!.Any(g => g.GuardrailId == "GR-003" && g.IsBlocking), Is.True);
        }

        [Test]
        public async Task Guardrails_InvalidAddress_ReturnsBlockingGR005()
        {
            var ctx = new GuardrailEvaluationContext
            {
                NodeReachable        = true,
                CreatorAddressValid  = false,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/guardrails", ctx);
            var findings = await resp.Content.ReadFromJsonAsync<List<ReliabilityGuardrail>>();
            Assert.That(findings!.Any(g => g.GuardrailId == "GR-005" && g.IsBlocking), Is.True);
        }

        [Test]
        public async Task Guardrails_ARC3_ReturnsIpfsInfoGR007()
        {
            var ctx = new GuardrailEvaluationContext
            {
                TokenStandard        = "ARC3",
                NodeReachable        = true,
                CreatorAddressValid  = true,
                RequiresIpfs         = true,
            };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/guardrails", ctx);
            var findings = await resp.Content.ReadFromJsonAsync<List<ReliabilityGuardrail>>();
            Assert.That(findings!.Any(g => g.GuardrailId == "GR-007"), Is.True);
        }

        [Test]
        public async Task Guardrails_NullBody_Returns400()
        {
            var resp = await _authClient.PostAsync("/api/v1/token-deployment-lifecycle/guardrails",
                new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        // ── POST /retry ────────────────────────────────────────────────────────

        [Test]
        public async Task Retry_WithoutAuth_Returns401()
        {
            var req = new DeploymentRetryRequest { IdempotencyKey = "some-key" };
            var resp = await _unauthClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/retry", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Retry_MissingIdempotencyKey_Returns400()
        {
            var req = new DeploymentRetryRequest { IdempotencyKey = string.Empty };
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/retry", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Retry_NullBody_Returns400()
        {
            var resp = await _authClient.PostAsync("/api/v1/token-deployment-lifecycle/retry",
                new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Retry_ValidKeyForSuccessfulDeployment_Returns200WithIdempotentReplay()
        {
            var key = Guid.NewGuid().ToString();
            var req = BuildValidRequest(idempotencyKey: key);
            var initiated = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(initiated!.Stage, Is.EqualTo(DeploymentStage.Completed));

            var retryResp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/retry",
                new DeploymentRetryRequest { IdempotencyKey = key });
            Assert.That(retryResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var retryBody = await retryResp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(retryBody!.IsIdempotentReplay, Is.True);
        }

        [Test]
        public async Task Retry_UnknownKey_Returns200WithFailedStage()
        {
            var retryResp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/retry",
                new DeploymentRetryRequest { IdempotencyKey = "nonexistent-key-xyz-999" });
            Assert.That(retryResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await retryResp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.Stage, Is.EqualTo(DeploymentStage.Failed));
        }

        // ── E2E: full lifecycle evidence ───────────────────────────────────────

        [Test]
        public async Task E2E_ValidateInitiateStatusTelemetry_FullLifecycleEvidence()
        {
            // Step 1: Pre-deployment validation
            var valReq = new DeploymentValidationRequest
            {
                TokenStandard  = "ASA",
                TokenName      = "E2E Evidence Token",
                TokenSymbol    = "E2E",
                Network        = "algorand-testnet",
                TotalSupply    = 1_000_000,
                Decimals       = 6,
                CreatorAddress = ValidAlgorandAddress,
            };
            var valResp = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/validate", valReq))
                .Content.ReadFromJsonAsync<DeploymentValidationResponse>();
            Assert.That(valResp!.IsValid, Is.True, "Step 1: pre-deployment validation must pass.");

            // Step 2: Initiate deployment
            var key = Guid.NewGuid().ToString();
            var initReq = BuildValidRequest(idempotencyKey: key,
                tokenName: "E2E Evidence Token", tokenSymbol: "E2E");
            var initiated = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", initReq))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(initiated!.Stage, Is.EqualTo(DeploymentStage.Completed),
                "Step 2: deployment must reach Completed stage.");
            Assert.That(initiated.DeploymentId, Is.Not.Null.And.Not.Empty,
                "Step 2: durable deployment ID required.");
            Assert.That(initiated.AssetId!.Value, Is.GreaterThan(0UL),
                "Step 2: asset ID must be returned for signed-off evidence.");

            // Step 3: Query status
            var status = await (await _authClient.GetAsync($"/api/v1/token-deployment-lifecycle/status/{initiated.DeploymentId}"))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(status!.Stage, Is.EqualTo(DeploymentStage.Completed),
                "Step 3: status query must reflect completed stage.");
            Assert.That(status.AssetId, Is.EqualTo(initiated.AssetId),
                "Step 3: asset ID must be stable across status queries.");

            // Step 4: Retrieve telemetry for audit evidence
            var events = await (await _authClient.GetAsync($"/api/v1/token-deployment-lifecycle/telemetry/{initiated.DeploymentId}"))
                .Content.ReadFromJsonAsync<List<DeploymentTelemetryEvent>>();
            Assert.That(events, Is.Not.Empty, "Step 4: telemetry events required for audit trail.");
            Assert.That(events!.Any(e => e.EventType == TelemetryEventType.CompletionSuccess),
                Is.True, "Step 4: completion event required in telemetry.");

            // Step 5: Idempotency replay proves determinism
            var replay = await (await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", initReq))
                .Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(replay!.IsIdempotentReplay, Is.True, "Step 5: idempotency replay expected.");
            Assert.That(replay.AssetId, Is.EqualTo(initiated.AssetId),
                "Step 5: idempotency replay must return identical asset ID.");
        }

        [Test]
        public async Task E2E_InvalidInputPath_NotPermissiveSuccess()
        {
            var req = BuildValidRequest(totalSupply: 0, network: "invalid-xyz");
            req.TokenStandard = string.Empty;
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body!.Stage, Is.Not.EqualTo(DeploymentStage.Completed),
                "Invalid request must NOT succeed.");
            Assert.That(body.Stage, Is.EqualTo(DeploymentStage.Failed));
            Assert.That(body.Outcome, Is.EqualTo(DeploymentOutcome.TerminalFailure));
        }

        [Test]
        public async Task E2E_SchemaContractAssertions_AllRequiredFieldsPresentInResponse()
        {
            var req = BuildValidRequest();
            var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
            var body = await resp.Content.ReadFromJsonAsync<TokenDeploymentLifecycleResponse>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.DeploymentId,    Is.Not.Null.And.Not.Empty, "DeploymentId required.");
            Assert.That(body.IdempotencyKey,   Is.Not.Null.And.Not.Empty, "IdempotencyKey required.");
            Assert.That(body.CorrelationId,    Is.Not.Null.And.Not.Empty, "CorrelationId required.");
            Assert.That(body.SchemaVersion,    Is.Not.Null.And.Not.Empty, "SchemaVersion required.");
            Assert.That(body.InitiatedAt,      Is.Not.EqualTo(default(DateTimeOffset)), "InitiatedAt required.");
            Assert.That(body.LastUpdatedAt,    Is.Not.EqualTo(default(DateTimeOffset)), "LastUpdatedAt required.");
            Assert.That(body.Progress,         Is.Not.Null, "Progress required.");
            Assert.That(body.TelemetryEvents,  Is.Not.Null, "TelemetryEvents required.");
            Assert.That(body.ValidationResults, Is.Not.Null, "ValidationResults required.");
            Assert.That(body.GuardrailFindings, Is.Not.Null, "GuardrailFindings required.");
            // Simulation disclosure: consumers must be able to detect simulated vs real evidence
            Assert.That(body.IsSimulatedEvidence, Is.True,
                "IsSimulatedEvidence must be present and true in the current simulated implementation; " +
                "sign-off tooling must check this field before treating evidence as production-valid.");
        }

        [Test]
        public async Task E2E_AllSupportedAlgorandStandards_DoNotReturn5xx()
        {
            foreach (var standard in new[] { "ASA", "ARC3", "ARC200", "ARC1400" })
            {
                var req = BuildValidRequest(standard: standard, idempotencyKey: Guid.NewGuid().ToString());
                var resp = await _authClient.PostAsJsonAsync("/api/v1/token-deployment-lifecycle/initiate", req);
                Assert.That((int)resp.StatusCode, Is.LessThan(500),
                    $"Standard '{standard}' must not produce a 5xx error.");
            }
        }
    }
}
