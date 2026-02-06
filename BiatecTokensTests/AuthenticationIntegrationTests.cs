using BiatecTokensApi.Controllers;
using BiatecTokensApi.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for authentication endpoints and ARC-0014 authentication flow
    /// Tests authentication diagnostics, error handling, and correlation ID tracking
    /// </summary>
    [TestFixture]
    public class AuthenticationIntegrationTests
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

        #region Authentication Info Endpoint

        [Test]
        public async Task AuthInfo_WithoutAuthentication_ShouldReturnSuccessfully()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/info");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task AuthInfo_ShouldReturnARC0014Details()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/info");
            var authInfo = await response.Content.ReadFromJsonAsync<AuthInfoResponse>();

            // Assert
            Assert.That(authInfo, Is.Not.Null);
            Assert.That(authInfo!.AuthenticationMethod, Is.EqualTo("ARC-0014"));
            Assert.That(authInfo.Realm, Is.EqualTo("BiatecTokens#ARC14"));
            Assert.That(authInfo.Description, Does.Contain("Algorand"));
            Assert.That(authInfo.HeaderFormat, Does.Contain("Authorization"));
            Assert.That(authInfo.Documentation, Does.Contain("github.com"));
        }

        [Test]
        public async Task AuthInfo_ShouldIncludeSupportedNetworks()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/info");
            var authInfo = await response.Content.ReadFromJsonAsync<AuthInfoResponse>();

            // Assert
            Assert.That(authInfo, Is.Not.Null);
            Assert.That(authInfo!.SupportedNetworks, Is.Not.Empty);
            Assert.That(authInfo.SupportedNetworks, Does.Contain("algorand-mainnet"));
            Assert.That(authInfo.SupportedNetworks, Does.Contain("algorand-testnet"));
        }

        [Test]
        public async Task AuthInfo_ShouldIncludeRequirements()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/info");
            var authInfo = await response.Content.ReadFromJsonAsync<AuthInfoResponse>();

            // Assert
            Assert.That(authInfo, Is.Not.Null);
            Assert.That(authInfo!.Requirements, Is.Not.Null);
            Assert.That(authInfo.Requirements!.TransactionFormat, Does.Contain("base64"));
            Assert.That(authInfo.Requirements.ExpirationCheck, Is.True);
            Assert.That(authInfo.Requirements.NetworkValidation, Is.True);
            Assert.That(authInfo.Requirements.MinimumValidityRounds, Is.GreaterThan(0));
        }

        [Test]
        public async Task AuthInfo_ShouldIncludeCorrelationId()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/info");
            var authInfo = await response.Content.ReadFromJsonAsync<AuthInfoResponse>();

            // Assert
            Assert.That(authInfo, Is.Not.Null);
            Assert.That(authInfo!.CorrelationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AuthInfo_ShouldIncludeTimestamp()
        {
            // Arrange
            var beforeRequest = DateTime.UtcNow;

            // Act
            var response = await _client.GetAsync("/api/v1/auth/info");
            var authInfo = await response.Content.ReadFromJsonAsync<AuthInfoResponse>();
            var afterRequest = DateTime.UtcNow;

            // Assert
            Assert.That(authInfo, Is.Not.Null);
            Assert.That(authInfo!.Timestamp, Is.GreaterThanOrEqualTo(beforeRequest).And.LessThanOrEqualTo(afterRequest));
        }

        #endregion

        #region Authentication Verification Endpoint

        [Test]
        public async Task AuthVerify_WithoutAuthentication_ShouldReturnUnauthorized()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/verify");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task AuthVerify_WithInvalidAuthHeader_ShouldReturnUnauthorized()
        {
            // Arrange
            _client.DefaultRequestHeaders.Add("Authorization", "Invalid_Token");

            // Act
            var response = await _client.GetAsync("/api/v1/auth/verify");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        #endregion

        #region Error Response Consistency

        [Test]
        public async Task ProtectedEndpoint_WithoutAuth_ShouldReturnConsistentUnauthorizedResponse()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/verify");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            
            // Verify response headers exist (may or may not have body depending on middleware)
            Assert.That(response.Headers, Is.Not.Null);
        }

        [Test]
        public async Task MultipleProtectedEndpoints_WithoutAuth_ShouldReturnUnauthorized()
        {
            // Arrange
            var endpoints = new[]
            {
                "/api/v1/auth/verify",
                "/api/v1/token/deployments/test-id",
                "/api/v1/subscription/status"
            };

            // Act & Assert
            foreach (var endpoint in endpoints)
            {
                var response = await _client.GetAsync(endpoint);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), 
                    $"Endpoint {endpoint} should return 401");
            }
        }

        #endregion

        #region Correlation ID Tracking

        [Test]
        public async Task AuthInfo_WithCustomCorrelationId_ShouldPreserveIt()
        {
            // Arrange
            var customCorrelationId = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Add("X-Correlation-ID", customCorrelationId);

            // Act
            var response = await _client.GetAsync("/api/v1/auth/info");

            // Assert
            Assert.That(response.Headers.Contains("X-Correlation-ID"), Is.True);
            var returnedCorrelationId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
            Assert.That(returnedCorrelationId, Is.EqualTo(customCorrelationId));
        }

        [Test]
        public async Task AuthInfo_WithoutCorrelationId_ShouldGenerateOne()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/info");

            // Assert
            Assert.That(response.Headers.Contains("X-Correlation-ID"), Is.True);
            var correlationId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
            Assert.That(correlationId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task PublicEndpoint_ShouldIncludeCorrelationIdInResponse()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            Assert.That(response.Headers.Contains("X-Correlation-ID"), Is.True);
        }

        #endregion

        #region Authentication Flow Documentation

        [Test]
        public async Task AuthInfo_DocumentationLink_ShouldBeValid()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/info");
            var authInfo = await response.Content.ReadFromJsonAsync<AuthInfoResponse>();

            // Assert
            Assert.That(authInfo, Is.Not.Null);
            Assert.That(authInfo!.Documentation, Does.StartWith("https://"));
            Assert.That(authInfo.Documentation, Does.Contain("ARC"));
        }

        [Test]
        public async Task AuthInfo_ShouldProvideCompleteAuthenticationGuidance()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/info");
            var authInfo = await response.Content.ReadFromJsonAsync<AuthInfoResponse>();

            // Assert - Verify all necessary information is present
            Assert.That(authInfo, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(authInfo!.AuthenticationMethod, Is.Not.Null.And.Not.Empty, "Authentication method should be specified");
                Assert.That(authInfo.Realm, Is.Not.Null.And.Not.Empty, "Realm should be specified");
                Assert.That(authInfo.Description, Is.Not.Null.And.Not.Empty, "Description should be provided");
                Assert.That(authInfo.HeaderFormat, Is.Not.Null.And.Not.Empty, "Header format should be documented");
                Assert.That(authInfo.Documentation, Is.Not.Null.And.Not.Empty, "Documentation link should be provided");
                Assert.That(authInfo.SupportedNetworks, Is.Not.Null.And.Not.Empty, "Supported networks should be listed");
                Assert.That(authInfo.Requirements, Is.Not.Null, "Requirements should be documented");
            });
        }

        #endregion

        #region API Consistency Tests

        [Test]
        public async Task HealthEndpoint_ShouldBeAccessibleWithoutAuth()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task StatusEndpoint_ShouldBeAccessibleWithoutAuth()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");

            // Assert
            Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable));
        }

        [Test]
        public async Task NetworksEndpoint_ShouldBeAccessibleWithoutAuth()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/networks");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task SwaggerEndpoint_ShouldBeAccessible()
        {
            // Act
            var response = await _client.GetAsync("/swagger/v1/swagger.json");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        #endregion

        #region Regression Tests

        [Test]
        public async Task AuthEndpoints_ShouldNotBreakExistingFunctionality()
        {
            // Test that existing endpoints still work as expected
            var publicEndpoints = new[]
            {
                "/health",
                "/health/live",
                "/health/ready",
                "/api/v1/status",
                "/api/v1/networks",
                "/api/v1/auth/info"
            };

            foreach (var endpoint in publicEndpoints)
            {
                var response = await _client.GetAsync(endpoint);
                Assert.That(response.StatusCode, 
                    Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable), 
                    $"Public endpoint {endpoint} should be accessible");
            }
        }

        #endregion
    }
}
