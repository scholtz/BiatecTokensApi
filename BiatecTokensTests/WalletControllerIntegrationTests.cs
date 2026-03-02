using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models.Wallet;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for WalletController verifying DI wiring, endpoint availability,
    /// and HTTP response contracts via WebApplicationFactory.
    /// Tests that endpoints return correct status codes and response shapes
    /// without needing real blockchain connections.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class WalletControllerIntegrationTests
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
            ["JwtConfig:SecretKey"] = "wallet-integration-test-secret-key-32chars-min",
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
            ["KeyManagementConfig:HardcodedKey"] = "WalletIntegrationTestKey32CharactersMinimumRequired"
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
            _client?.Dispose();
            _factory?.Dispose();
        }

        // ── DI registration ───────────────────────────────────────────────────

        /// <summary>
        /// IWalletConnectionService must be resolvable from the production DI container
        /// </summary>
        [Test]
        public async Task DI_ResolvesIWalletConnectionService_FromProductionContainer()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetService<IWalletConnectionService>();
            Assert.That(service, Is.Not.Null, "IWalletConnectionService must be registered in DI");
            await Task.CompletedTask;
        }

        /// <summary>
        /// WalletController must be available via DI (controllers are registered automatically)
        /// </summary>
        [Test]
        public async Task DI_WalletControllerEndpoints_AreReachable()
        {
            // WalletController endpoints require auth; unauthenticated request returns 401
            var response = await _client.GetAsync("/api/v1/wallet/connection?address=ALGO");
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "WalletController GET connection must be secured (not 404 = not found)");
        }

        // ── Endpoint security: all endpoints require authentication ───────────

        /// <summary>
        /// GET /api/v1/wallet/connection must require authentication
        /// </summary>
        [Test]
        public async Task GetConnection_WithoutAuth_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/wallet/connection?address=ALGO&network=algorand-mainnet");
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "Wallet connection endpoint must require authentication");
        }

        /// <summary>
        /// POST /api/v1/wallet/connect must require authentication
        /// </summary>
        [Test]
        public async Task PostConnect_WithoutAuth_Returns401()
        {
            var body = new { address = "ALGO", actualNetwork = "algorand-mainnet" };
            var response = await _client.PostAsJsonAsync("/api/v1/wallet/connect", body);
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "Wallet connect endpoint must require authentication");
        }

        /// <summary>
        /// POST /api/v1/wallet/disconnect must require authentication
        /// </summary>
        [Test]
        public async Task PostDisconnect_WithoutAuth_Returns401()
        {
            var body = new { address = "ALGO" };
            var response = await _client.PostAsJsonAsync("/api/v1/wallet/disconnect", body);
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "Wallet disconnect endpoint must require authentication");
        }

        /// <summary>
        /// POST /api/v1/wallet/reconnect must require authentication
        /// </summary>
        [Test]
        public async Task PostReconnect_WithoutAuth_Returns401()
        {
            var body = new { address = "ALGO", actualNetwork = "algorand-mainnet" };
            var response = await _client.PostAsJsonAsync("/api/v1/wallet/reconnect", body);
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "Wallet reconnect endpoint must require authentication");
        }

        /// <summary>
        /// GET /api/v1/wallet/reconnect-guidance must require authentication
        /// </summary>
        [Test]
        public async Task GetReconnectGuidance_WithoutAuth_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/wallet/reconnect-guidance");
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "Reconnect guidance endpoint must require authentication");
        }

        /// <summary>
        /// GET /api/v1/wallet/networks must require authentication
        /// </summary>
        [Test]
        public async Task GetNetworks_WithoutAuth_Returns401()
        {
            var response = await _client.GetAsync("/api/v1/wallet/networks");
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "Networks endpoint must require authentication");
        }

        /// <summary>
        /// POST /api/v1/wallet/validate-address must require authentication
        /// </summary>
        [Test]
        public async Task PostValidateAddress_WithoutAuth_Returns401()
        {
            var body = new { address = "ALGO", network = "algorand-mainnet" };
            var response = await _client.PostAsJsonAsync("/api/v1/wallet/validate-address", body);
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "Validate address endpoint must require authentication");
        }

        // ── Endpoint routing: routes must resolve (not 404) ───────────────────

        /// <summary>
        /// All 7 wallet endpoints must route to WalletController (not return 404)
        /// </summary>
        [Test]
        public async Task AllWalletEndpoints_RouteToController_NotReturn404()
        {
            var getEndpoints = new[]
            {
                "/api/v1/wallet/connection?address=ALGO",
                "/api/v1/wallet/reconnect-guidance",
                "/api/v1/wallet/networks"
            };

            foreach (var endpoint in getEndpoints)
            {
                var response = await _client.GetAsync(endpoint);
                Assert.That((int)response.StatusCode, Is.Not.EqualTo(404),
                    $"Endpoint {endpoint} must route to WalletController (not 404)");
            }

            var postEndpoints = new[]
            {
                ("/api/v1/wallet/connect", (object)new { address = "ALGO", actualNetwork = "algorand-mainnet" }),
                ("/api/v1/wallet/disconnect", (object)new { address = "ALGO" }),
                ("/api/v1/wallet/reconnect", (object)new { address = "ALGO", actualNetwork = "algorand-mainnet" }),
                ("/api/v1/wallet/validate-address", (object)new { address = "ALGO", network = "algorand-mainnet" })
            };

            foreach (var (endpoint, body) in postEndpoints)
            {
                var response = await _client.PostAsJsonAsync(endpoint, body);
                Assert.That((int)response.StatusCode, Is.Not.EqualTo(404),
                    $"Endpoint {endpoint} must route to WalletController (not 404)");
            }
        }

        // ── Regression: existing endpoints unaffected ─────────────────────────

        /// <summary>
        /// Health endpoint is unaffected by WalletController addition
        /// </summary>
        [Test]
        public async Task HealthEndpoint_StillAccessible_AfterWalletControllerAdded()
        {
            var response = await _client.GetAsync("/health");
            Assert.That((int)response.StatusCode, Is.EqualTo(200),
                "Health endpoint must still be accessible after adding WalletController");
        }

        /// <summary>
        /// Standards endpoint is unaffected by WalletController addition
        /// </summary>
        [Test]
        public async Task StandardsEndpoint_StillAccessible_AfterWalletControllerAdded()
        {
            var response = await _client.GetAsync("/api/v1/standards");
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "Standards endpoint must still be secured after adding WalletController");
        }

        /// <summary>
        /// Auth register endpoint is unaffected by WalletController addition
        /// </summary>
        [Test]
        public async Task AuthRegisterEndpoint_StillAccessible_AfterWalletControllerAdded()
        {
            // Use a deliberately malformed request (missing required fields) to get a 400 response
            var body = new { email = "not-an-email", password = "" };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", body);
            Assert.That((int)response.StatusCode, Is.InRange(400, 499),
                "Auth register endpoint must still return 4xx for invalid request after adding WalletController");
        }
    }
}
