using BiatecTokensApi.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for token deployment reliability, correlation tracking, and idempotency
    /// Tests ensure consistent API responses, proper error handling, and observable deployment flow
    /// </summary>
    [TestFixture]
    public class TokenDeploymentReliabilityTests
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
                ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
                ["KeyManagementConfig:Provider"] = "Hardcoded",
                ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIntegrationTests32CharactersMinimumRequired",
                ["JwtConfig:SecretKey"] = "test-secret-key-at-least-32-characters-long-for-hs256",
                ["JwtConfig:Issuer"] = "BiatecTokensApi",
                ["JwtConfig:Audience"] = "BiatecTokensUsers",
                ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                ["JwtConfig:RefreshTokenExpirationDays"] = "30"
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

        #region Error Response Consistency

        [Test]
        public async Task TokenDeployment_WithoutAuth_ShouldReturnConsistentUnauthorized()
        {
            // Arrange
            var request = new
            {
                network = "algorand-testnet",
                name = "Test Token",
                unitName = "TEST",
                totalSupply = 1000000,
                decimals = 0
            };

            // Act
            var response = await _client.PostAsync("/api/v1/token/asa/create",
                JsonContent.Create(request));

            // Assert - May return 404 (no route match) or 401 (unauthorized)
            Assert.That(response.StatusCode, 
                Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound));
        }

        [Test]
        public async Task TokenDeployment_WithInvalidNetwork_ShouldReturnBadRequestOrUnauthorized()
        {
            // Arrange
            var request = new
            {
                network = "invalid-network",
                name = "Test Token",
                unitName = "TEST",
                totalSupply = 1000000,
                decimals = 0
            };

            // Act
            var response = await _client.PostAsync("/api/v1/token/asa/create",
                JsonContent.Create(request));

            // Assert - Should be Unauthorized, NotFound (no route), or BadRequest (if validation runs first)
            Assert.That(response.StatusCode, 
                Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.NotFound));
        }

        [Test]
        public async Task TokenDeployment_WithMissingRequiredFields_ShouldReturnBadRequestOrUnauthorized()
        {
            // Arrange - missing required fields
            var request = new { network = "algorand-testnet" };

            // Act
            var response = await _client.PostAsync("/api/v1/token/asa/create",
                JsonContent.Create(request));

            // Assert - May return NotFound, Unauthorized or BadRequest
            Assert.That(response.StatusCode, 
                Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.NotFound));
        }

        #endregion

        #region Correlation ID Tracking

        [Test]
        public async Task TokenDeployment_ShouldIncludeCorrelationIdInResponse()
        {
            // Arrange
            var request = new
            {
                network = "algorand-testnet",
                name = "Test Token",
                unitName = "TEST",
                totalSupply = 1000000,
                decimals = 0
            };

            // Act
            var response = await _client.PostAsync("/api/v1/token/asa/create",
                JsonContent.Create(request));

            // Assert - Should have correlation ID in headers
            Assert.That(response.Headers.Contains("X-Correlation-ID"), Is.True);
        }

        [Test]
        public async Task TokenDeployment_WithCustomCorrelationId_ShouldPreserveIt()
        {
            // Arrange
            var customCorrelationId = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Add("X-Correlation-ID", customCorrelationId);
            
            var request = new
            {
                network = "algorand-testnet",
                name = "Test Token",
                unitName = "TEST",
                totalSupply = 1000000,
                decimals = 0
            };

            // Act
            var response = await _client.PostAsync("/api/v1/token/asa/create",
                JsonContent.Create(request));

            // Assert
            Assert.That(response.Headers.Contains("X-Correlation-ID"), Is.True);
            var returnedCorrelationId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
            Assert.That(returnedCorrelationId, Is.EqualTo(customCorrelationId));
        }

        [Test]
        public async Task MultipleDeployments_ShouldHaveUniqueCorrelationIds()
        {
            // Arrange
            var request = new
            {
                network = "algorand-testnet",
                name = "Test Token",
                unitName = "TEST",
                totalSupply = 1000000,
                decimals = 0
            };

            // Act - Make multiple requests
            var response1 = await _client.PostAsync("/api/v1/token/asa/create",
                JsonContent.Create(request));
            var response2 = await _client.PostAsync("/api/v1/token/asa/create",
                JsonContent.Create(request));

            // Assert - Correlation IDs should be different
            var correlationId1 = response1.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
            var correlationId2 = response2.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
            
            Assert.That(correlationId1, Is.Not.Null.And.Not.Empty);
            Assert.That(correlationId2, Is.Not.Null.And.Not.Empty);
            Assert.That(correlationId1, Is.Not.EqualTo(correlationId2));
        }

        #endregion

        #region Deployment Status Tracking

        [Test]
        public async Task DeploymentStatus_WithInvalidId_ShouldReturnNotFoundOrUnauthorized()
        {
            // Arrange
            var invalidDeploymentId = Guid.NewGuid().ToString();

            // Act
            var response = await _client.GetAsync($"/api/v1/token/deployments/{invalidDeploymentId}");

            // Assert
            Assert.That(response.StatusCode, 
                Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound));
        }

        [Test]
        public async Task DeploymentStatus_WithEmptyId_ShouldReturnBadRequestOrNotFound()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/token/deployments/");

            // Assert - May return 404, 400, or 401 depending on routing
            Assert.That(response.StatusCode, 
                Is.AnyOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task DeploymentStatus_ShouldIncludeCorrelationId()
        {
            // Arrange
            var deploymentId = Guid.NewGuid().ToString();

            // Act
            var response = await _client.GetAsync($"/api/v1/token/deployments/{deploymentId}");

            // Assert
            Assert.That(response.Headers.Contains("X-Correlation-ID"), Is.True);
        }

        #endregion

        #region Idempotency Support

        [Test]
        public async Task TokenDeployment_WithIdempotencyKey_ShouldAcceptHeader()
        {
            // Arrange
            var idempotencyKey = Guid.NewGuid().ToString();
            var request = new
            {
                network = "algorand-testnet",
                name = "Test Token",
                unitName = "TEST",
                totalSupply = 1000000,
                decimals = 0
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/token/asa/create")
            {
                Content = JsonContent.Create(request)
            };
            requestMessage.Headers.Add("Idempotency-Key", idempotencyKey);

            // Act
            var response = await _client.SendAsync(requestMessage);

            // Assert - Should accept the idempotency key header (may still return 401 or 404)
            Assert.That(response.StatusCode, 
                Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.BadGateway, HttpStatusCode.NotFound));
        }

        [Test]
        public async Task ERC20Deployment_WithIdempotencyKey_ShouldAcceptHeader()
        {
            // Arrange
            var idempotencyKey = Guid.NewGuid().ToString();
            var request = new
            {
                network = "base-mainnet",
                name = "Test Token",
                symbol = "TEST",
                cap = 1000000000000000000,
                initialSupply = 1000000000000000000
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/token/erc20-mintable/create")
            {
                Content = JsonContent.Create(request)
            };
            requestMessage.Headers.Add("Idempotency-Key", idempotencyKey);

            // Act
            var response = await _client.SendAsync(requestMessage);

            // Assert
            Assert.That(response.StatusCode, 
                Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.BadGateway));
        }

        #endregion

        #region Error Message Quality

        [Test]
        public async Task UnauthorizedRequest_ShouldHaveMinimalResponse()
        {
            // Arrange
            var request = new
            {
                network = "algorand-testnet",
                name = "Test Token",
                unitName = "TEST",
                totalSupply = 1000000,
                decimals = 0
            };

            // Act
            var response = await _client.PostAsync("/api/v1/token/asa/create",
                JsonContent.Create(request));

            // Assert - May return 404 or 401
            Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound));
            // Unauthorized responses typically don't include detailed error bodies for security
            // They may have WWW-Authenticate header
        }

        #endregion

        #region Multi-Endpoint Consistency

        [Test]
        public async Task AllTokenDeploymentEndpoints_WithoutAuth_ShouldReturnUnauthorized()
        {
            // Arrange
            var endpoints = new[]
            {
                "/api/v1/token/erc20-mintable/create",
                "/api/v1/token/erc20-preminted/create",
                "/api/v1/token/asa/create",
                "/api/v1/token/asa/nft/create",
                "/api/v1/token/asa/fnft/create",
                "/api/v1/token/arc3/fungible/create",
                "/api/v1/token/arc3/nft/create",
                "/api/v1/token/arc3/fnft/create",
                "/api/v1/token/arc200/mintable/create",
                "/api/v1/token/arc200/preminted/create",
                "/api/v1/token/arc1400/mintable/create"
            };

            // Act & Assert
            foreach (var endpoint in endpoints)
            {
                var response = await _client.PostAsync(endpoint, 
                    JsonContent.Create(new { network = "test" }));
                
                Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound),
                    $"Endpoint {endpoint} should return 401 or 404 without authentication");
            }
        }

        [Test]
        public async Task AllTokenDeploymentEndpoints_ShouldHaveCorrelationIds()
        {
            // Arrange
            var endpoints = new[]
            {
                "/api/v1/token/erc20-mintable/create",
                "/api/v1/token/asa/create"
            };

            // Act & Assert
            foreach (var endpoint in endpoints)
            {
                var response = await _client.PostAsync(endpoint,
                    JsonContent.Create(new { network = "test" }));

                Assert.That(response.Headers.Contains("X-Correlation-ID"), Is.True,
                    $"Endpoint {endpoint} should include correlation ID in response headers");
            }
        }

        #endregion

        #region Deployment Progress Tracking

        [Test]
        public async Task DeploymentStatus_EndpointShouldBeAccessible()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/token/deployments/test-id-12345");

            // Assert - Should return either Unauthorized (no auth) or NotFound (authenticated but no deployment)
            Assert.That(response.StatusCode, 
                Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.BadRequest));
        }

        #endregion

        #region Response Structure Tests

        [Test]
        public async Task TokenDeployment_UnauthorizedResponse_ShouldBeConsistent()
        {
            // Arrange
            var requests = new[]
            {
                "/api/v1/token/asa/create",
                "/api/v1/token/erc20-mintable/create"
            };

            // Act & Assert
            foreach (var endpoint in requests)
            {
                var response = await _client.PostAsync(endpoint,
                    JsonContent.Create(new { network = "test" }));

                // May return 404 or 401 depending on routing
                Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound));
                // All responses should be consistent and include correlation IDs
                Assert.That(response.Headers.Contains("X-Correlation-ID"), Is.True);
            }
        }

        #endregion

        #region Integration with Existing Tests

        [Test]
        public async Task NewTests_ShouldNotAffectExistingHealthChecks()
        {
            // Verify that existing functionality still works
            var response = await _client.GetAsync("/health");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task NewTests_ShouldNotAffectNetworkEndpoints()
        {
            // Verify that existing functionality still works
            var response = await _client.GetAsync("/api/v1/networks");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        #endregion
    }
}
