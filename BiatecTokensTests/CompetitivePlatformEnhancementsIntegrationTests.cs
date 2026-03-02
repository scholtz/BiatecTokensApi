using BiatecTokensApi.Models.TokenLaunch;
using BiatecTokensApi.Models.Wallet;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the three competitive platform enhancements (Issue #447):
    /// 1. POST /api/v1/token-launch/preview-config – guided configuration validation
    /// 2. GET  /api/v1/token-launch/trust-score/{tokenId} – trust signal for buyers
    /// 3. POST /api/v1/wallet/routing-options – wallet routing optimization
    ///
    /// Uses WebApplicationFactory to exercise the full DI container and HTTP pipeline.
    /// Verifies API contract shapes, status codes, and response field presence.
    ///
    /// Business Value: Proves competitive endpoints are wired correctly in the DI container,
    /// return deterministic HTTP responses, and handle validation failure paths.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class CompetitivePlatformEnhancementsIntegrationTests
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
            ["JwtConfig:SecretKey"] = "competitive-enhancement-test-secret-32chars",
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
            ["KeyManagementConfig:HardcodedKey"] = "CompetitiveEnhancementsTestKey32CharsRequired"
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

        // ══════════════════════════════════════════════════════════════════════
        // AC1 Integration: POST /api/v1/token-launch/preview-config
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task PreviewConfig_ValidFullRequest_Returns200WithScore()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "Integration Test Token",
                Symbol = "ITT",
                TotalSupply = 1_000_000,
                Decimals = 6,
                Description = "A token for integration testing",
                ImageUrl = "https://example.com/logo.png"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token-launch/preview-config", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<TokenConfigPreviewResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.CompletenessScore, Is.GreaterThan(0));
            Assert.That(result.IsDeployable, Is.True);
            Assert.That(result.PreviewId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task PreviewConfig_MissingTokenType_Returns400()
        {
            // Arrange
            var request = new { Network = "algorand-mainnet", Name = "Token", Symbol = "TK" };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token-launch/preview-config", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task PreviewConfig_InvalidConfiguration_Returns200WithErrors()
        {
            // Arrange - invalid config (name too long for Algorand)
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = new string('A', 33), // 33 chars > 32 limit
                Symbol = "TK"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token-launch/preview-config", request);

            // Assert - still 200 but with error flags
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<TokenConfigPreviewResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.IsDeployable, Is.False);
        }

        [Test]
        public async Task PreviewConfig_ERC20OnBaseMainnet_Returns200WithEthCost()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ERC20",
                Network = "base-mainnet",
                Name = "Base Integration Token",
                Symbol = "BIT",
                TotalSupply = 1_000_000_000,
                Decimals = 18
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token-launch/preview-config", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<TokenConfigPreviewResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.CostEstimate.CostUnit, Is.EqualTo("ETH (gas)"));
        }

        [Test]
        public async Task PreviewConfig_Deterministic_ThreeRunsIdenticalResult()
        {
            // Arrange
            var request = new TokenConfigPreviewRequest
            {
                TokenType = "ASA",
                Network = "algorand-mainnet",
                Name = "Determinism Test Token",
                Symbol = "DTT",
                TotalSupply = 500_000,
                Decimals = 2
            };

            // Act - 3 runs
            var r1 = await (await _client.PostAsJsonAsync("/api/v1/token-launch/preview-config", request))
                .Content.ReadFromJsonAsync<TokenConfigPreviewResponse>();
            var r2 = await (await _client.PostAsJsonAsync("/api/v1/token-launch/preview-config", request))
                .Content.ReadFromJsonAsync<TokenConfigPreviewResponse>();
            var r3 = await (await _client.PostAsJsonAsync("/api/v1/token-launch/preview-config", request))
                .Content.ReadFromJsonAsync<TokenConfigPreviewResponse>();

            // Assert - scores must be identical
            Assert.That(r1!.CompletenessScore, Is.EqualTo(r2!.CompletenessScore));
            Assert.That(r2.CompletenessScore, Is.EqualTo(r3!.CompletenessScore));
            Assert.That(r1.IsDeployable, Is.EqualTo(r2.IsDeployable));
        }

        // ══════════════════════════════════════════════════════════════════════
        // AC2 Integration: GET /api/v1/token-launch/trust-score/{tokenId}
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TrustScore_ValidAlgorandAssetId_Returns200WithScore()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/token-launch/trust-score/12345678?network=algorand-mainnet");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<TokenTrustScoreResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.TrustScore, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.TrustScore, Is.LessThanOrEqualTo(100));
            Assert.That(result.TrustSummary, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task TrustScore_EvmContractAddress_Returns200WithScore()
        {
            // Act
            var response = await _client.GetAsync(
                "/api/v1/token-launch/trust-score/0xAbCdEf1234567890abcdef1234567890AbCdEf12?network=base-mainnet");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<TokenTrustScoreResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.PositiveSignals.Any(s => s.Label.Contains("Smart Contract")), Is.True);
        }

        [Test]
        public async Task TrustScore_DefaultNetwork_UsesAlgorandMainnet()
        {
            // Act - no network query param, should default to algorand-mainnet
            var response = await _client.GetAsync("/api/v1/token-launch/trust-score/99999999");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<TokenTrustScoreResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.TrustScore, Is.GreaterThan(0));
        }

        [Test]
        public async Task TrustScore_ResponseContainsBreakdown()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/token-launch/trust-score/456789?network=algorand-mainnet");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<TokenTrustScoreResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Breakdown, Is.Not.Null);
        }

        [Test]
        public async Task TrustScore_Deterministic_ThreeRunsIdenticalScore()
        {
            // Act - 3 runs
            var r1 = await (await _client.GetAsync("/api/v1/token-launch/trust-score/77777?network=algorand-mainnet"))
                .Content.ReadFromJsonAsync<TokenTrustScoreResponse>();
            var r2 = await (await _client.GetAsync("/api/v1/token-launch/trust-score/77777?network=algorand-mainnet"))
                .Content.ReadFromJsonAsync<TokenTrustScoreResponse>();
            var r3 = await (await _client.GetAsync("/api/v1/token-launch/trust-score/77777?network=algorand-mainnet"))
                .Content.ReadFromJsonAsync<TokenTrustScoreResponse>();

            // Assert - must be deterministic
            Assert.That(r1!.TrustScore, Is.EqualTo(r2!.TrustScore));
            Assert.That(r2.TrustScore, Is.EqualTo(r3!.TrustScore));
        }

        // ══════════════════════════════════════════════════════════════════════
        // AC3 Integration: POST /api/v1/wallet/routing-options
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task RoutingOptions_UnauthenticatedRequest_Returns401()
        {
            // Arrange - routing endpoint requires auth (same as all WalletController endpoints)
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "algorand-mainnet",
                TargetNetwork = "algorand-mainnet",
                OperationType = WalletOperationType.TokenPurchase
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/wallet/routing-options", request);

            // Assert - [Authorize] requires authentication
            Assert.That((int)response.StatusCode, Is.EqualTo(401).Or.EqualTo(403),
                "Routing endpoint requires authentication");
        }

        [Test]
        public async Task RoutingOptions_EndpointExists_NotReturnNotFound()
        {
            // Arrange - empty body: endpoint must be registered (not 404), even if auth fails
            var request = new { };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/wallet/routing-options", request);

            // Assert - endpoint exists (not 404) and requires auth
            Assert.That((int)response.StatusCode, Is.Not.EqualTo(404),
                "Routing endpoint must be registered in DI/routing");
        }

        [Test]
        public async Task RoutingOptions_ServiceRegisteredInDi_ResolveSuccessfully()
        {
            // Assert - IWalletRoutingService registered in DI
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetService<BiatecTokensApi.Services.Interface.IWalletRoutingService>();
            Assert.That(service, Is.Not.Null, "IWalletRoutingService must be registered in DI");
        }

        [Test]
        public async Task RoutingOptions_SameNetwork_ServiceReturnsSameNetworkFlag()
        {
            // Arrange - test service directly (bypassing auth)
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BiatecTokensApi.Services.Interface.IWalletRoutingService>();
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "algorand-mainnet",
                TargetNetwork = "algorand-mainnet",
                OperationType = WalletOperationType.TokenPurchase
            };

            // Act
            var result = await service.GetRoutingOptionsAsync(request);

            // Assert
            Assert.That(result.IsSameNetwork, Is.True);
            Assert.That(result.HasDirectRoute, Is.True);
        }

        [Test]
        public async Task RoutingOptions_CrossChain_ServiceReturnsCexRoute()
        {
            // Arrange - test service directly (bypassing auth)
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BiatecTokensApi.Services.Interface.IWalletRoutingService>();
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "algorand-mainnet",
                TargetNetwork = "base-mainnet",
                OperationType = WalletOperationType.CrossChainBridge
            };

            // Act
            var result = await service.GetRoutingOptionsAsync(request);

            // Assert
            Assert.That(result.AvailableRoutes, Is.Not.Empty);
            Assert.That(result.AvailableRoutes.Any(r =>
                r.RouteType == WalletRouteType.CentralizedExchange), Is.True);
        }

        [Test]
        public async Task RoutingOptions_Deterministic_ThreeRunsIdenticalRouteCount()
        {
            // Arrange - test service directly
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BiatecTokensApi.Services.Interface.IWalletRoutingService>();
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "algorand-mainnet",
                TargetNetwork = "base-mainnet",
                OperationType = WalletOperationType.TokenPurchase
            };

            // Act - 3 runs
            var r1 = await service.GetRoutingOptionsAsync(request);
            var r2 = await service.GetRoutingOptionsAsync(request);
            var r3 = await service.GetRoutingOptionsAsync(request);

            // Assert - deterministic
            Assert.That(r1.AvailableRoutes.Count, Is.EqualTo(r2.AvailableRoutes.Count));
            Assert.That(r2.AvailableRoutes.Count, Is.EqualTo(r3.AvailableRoutes.Count));
        }

        [Test]
        public async Task RoutingOptions_ResponseContainsGeneratedAt()
        {
            // Arrange - test service directly
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BiatecTokensApi.Services.Interface.IWalletRoutingService>();
            var request = new WalletRoutingRequest
            {
                SourceNetwork = "algorand-mainnet",
                TargetNetwork = "algorand-testnet"
            };

            // Act
            var result = await service.GetRoutingOptionsAsync(request);

            // Assert
            Assert.That(result.GeneratedAt, Is.Not.EqualTo(default(DateTime)));
        }
    }
}
