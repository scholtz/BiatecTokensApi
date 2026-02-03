using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Subscription;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for backend MVP stabilization requirements
    /// Tests critical workflows including authentication, subscription enforcement, 
    /// network validation, and error handling
    /// </summary>
    [TestFixture]
    public class BackendMVPStabilizationTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        [SetUp]
        public void Setup()
        {
            var configuration = new Dictionary<string, string?>
            {
                ["App:Account"] = "test test test test test test test test test test test test test test test test test test test test test test test test test",
                ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                ["AlgorandAuthentication:CheckExpiration"] = "false",
                ["AlgorandAuthentication:Debug"] = "true",
                ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                ["AlgorandAuthentication:AllowedNetworks:SGO1GKSzyE7IEPItTxCByw9x8FmnrCDexi9/cOUJOiI=:Server"] = "https://testnet-api.4160.nodely.dev",
                ["AlgorandAuthentication:AllowedNetworks:SGO1GKSzyE7IEPItTxCByw9x8FmnrCDexi9/cOUJOiI=:Token"] = "",
                ["AlgorandAuthentication:AllowedNetworks:SGO1GKSzyE7IEPItTxCByw9x8FmnrCDexi9/cOUJOiI=:Header"] = "",
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
                ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise"
            };

            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(configuration);
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

        #region Health and Status Endpoints

        [Test]
        public async Task HealthEndpoint_ShouldReturnOk()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task HealthReadyEndpoint_ShouldReturnOkOrServiceUnavailable()
        {
            // Act
            var response = await _client.GetAsync("/health/ready");

            // Assert - Can be OK or Service Unavailable depending on external services
            Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable));
        }

        [Test]
        public async Task HealthLiveEndpoint_ShouldReturnOk()
        {
            // Act
            var response = await _client.GetAsync("/health/live");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task StatusEndpoint_ShouldReturnDetailedStatus()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");

            // Assert
            Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable));
            
            var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();
            
            Assert.That(statusResponse, Is.Not.Null);
            Assert.That(statusResponse!.Version, Is.Not.Null);
            Assert.That(statusResponse.Timestamp, Is.Not.EqualTo(default(DateTime)));
            Assert.That(statusResponse.Environment, Is.Not.Null);
        }

        [Test]
        public async Task StatusEndpoint_ShouldIncludeComponentHealth()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");
            var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();

            // Assert
            Assert.That(statusResponse, Is.Not.Null);
            Assert.That(statusResponse!.Components, Is.Not.Null);
            
            // Should have health checks for critical components
            var componentKeys = statusResponse.Components.Keys;
            Assert.That(componentKeys, Does.Contain("ipfs").Or.Contain("algorand").Or.Contain("evm"));
        }

        #endregion

        #region Network Metadata Endpoint

        [Test]
        public async Task NetworksEndpoint_ShouldReturnNetworkList()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/networks");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            
            var networkResponse = await response.Content.ReadFromJsonAsync<NetworkMetadataResponse>();
            
            Assert.That(networkResponse, Is.Not.Null);
            Assert.That(networkResponse!.Success, Is.True);
            Assert.That(networkResponse.Networks, Is.Not.Empty);
        }

        [Test]
        public async Task NetworksEndpoint_ShouldPrioritizeMainnets()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/networks");
            var networkResponse = await response.Content.ReadFromJsonAsync<NetworkMetadataResponse>();

            // Assert
            Assert.That(networkResponse, Is.Not.Null);
            Assert.That(networkResponse!.RecommendedNetworks, Is.Not.Empty);
            
            // Should have Algorand and Base mainnets as recommended
            var hasAlgorandMainnet = networkResponse.RecommendedNetworks.Any(n => n.Contains("algorand-mainnet"));
            var hasBaseMainnet = networkResponse.RecommendedNetworks.Any(n => n.Contains("base-mainnet"));
            
            Assert.That(hasAlgorandMainnet || hasBaseMainnet, Is.True);
        }

        [Test]
        public async Task NetworksEndpoint_ShouldIncludeNetworkMetadata()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/networks");
            var networkResponse = await response.Content.ReadFromJsonAsync<NetworkMetadataResponse>();

            // Assert
            Assert.That(networkResponse, Is.Not.Null);
            Assert.That(networkResponse!.Networks, Is.Not.Empty);
            
            var firstNetwork = networkResponse.Networks.First();
            Assert.That(firstNetwork.NetworkId, Is.Not.Null.And.Not.Empty);
            Assert.That(firstNetwork.DisplayName, Is.Not.Null.And.Not.Empty);
            Assert.That(firstNetwork.BlockchainType, Is.Not.Null.And.Not.Empty);
            Assert.That(firstNetwork.EndpointUrl, Is.Not.Null.And.Not.Empty);
        }

        #endregion

        #region Error Handling and Correlation IDs

        [Test]
        public async Task UnauthorizedEndpoint_ShouldReturnStandardErrorResponse()
        {
            // Act - Call protected endpoint without authentication
            var response = await _client.PostAsync("/api/v1/token/erc20-mintable/create",
                JsonContent.Create(new { }));

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task InvalidRequest_ShouldReturnBadRequestOrUnauthorized()
        {
            // Act - Send invalid request body
            var response = await _client.PostAsync("/api/v1/subscription/checkout",
                JsonContent.Create(new { tier = "InvalidTier" }));

            // Assert - Should return Unauthorized (requires auth) or BadRequest (if it got past auth)
            Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task NonExistentEndpoint_ShouldReturn404()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/nonexistent");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        #endregion

        #region Subscription System Integration

        [Test]
        public async Task SubscriptionStatus_WithoutAuth_ShouldReturnUnauthorized()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/subscription/status");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task SubscriptionCheckout_WithoutAuth_ShouldReturnUnauthorized()
        {
            // Act
            var checkoutRequest = new { tier = "Basic" };
            var response = await _client.PostAsync("/api/v1/subscription/checkout",
                JsonContent.Create(checkoutRequest));

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task SubscriptionBillingPortal_WithoutAuth_ShouldReturnUnauthorized()
        {
            // Act
            var response = await _client.PostAsync("/api/v1/subscription/billing-portal",
                JsonContent.Create(new { }));

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        #endregion

        #region API Documentation and Consistency

        [Test]
        public async Task SwaggerEndpoint_ShouldBeAccessible()
        {
            // Act
            var response = await _client.GetAsync("/swagger/v1/swagger.json");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task SwaggerUI_ShouldBeAccessible()
        {
            // Act
            var response = await _client.GetAsync("/swagger");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        #endregion
    }
}
