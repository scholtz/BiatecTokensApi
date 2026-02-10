using BiatecTokensApi.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for health check and status monitoring endpoints.
    /// These tests verify that health monitoring endpoints are properly configured
    /// and return expected responses for different system states.
    /// </summary>
    [TestFixture]
    public class HealthCheckIntegrationTests
    {
        private CustomWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new CustomWebApplicationFactory();
            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        /// <summary>
        /// Custom WebApplicationFactory for testing health endpoints
        /// </summary>
        private class CustomWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Use in-memory configuration with valid test settings
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "test mnemonic phrase for testing purposes only not real",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "TestKeyForHealthCheckIntegrationTests32CharMinimum",
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

        [Test]
        public async Task BasicHealthEndpoint_ReturnsOk()
        {
            // Act
            var response = await _client.GetAsync("/health");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), 
                "Basic health endpoint should return 200 OK");
        }

        [Test]
        public async Task ReadinessHealthEndpoint_ReturnsOk()
        {
            // Act
            var response = await _client.GetAsync("/health/ready");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.ServiceUnavailable), 
                "Readiness endpoint should return 200 OK or 503 Service Unavailable depending on dependencies");
        }

        [Test]
        public async Task LivenessHealthEndpoint_ReturnsOk()
        {
            // Act
            var response = await _client.GetAsync("/health/live");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), 
                "Liveness endpoint should always return 200 OK if app is running");
        }

        [Test]
        public async Task StatusEndpoint_ReturnsApiStatusResponse()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.ServiceUnavailable),
                "Status endpoint should return 200 OK or 503 depending on component health");

            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Null.And.Not.Empty, "Status endpoint should return content");

            // Verify JSON structure
            var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();
            Assert.That(statusResponse, Is.Not.Null, "Response should deserialize to ApiStatusResponse");
            Assert.That(statusResponse!.Status, Is.Not.Null.And.Not.Empty, "Status field should not be empty");
            Assert.That(statusResponse.Version, Is.Not.Null.And.Not.Empty, "Version field should not be empty");
            Assert.That(statusResponse.Timestamp, Is.Not.EqualTo(default(DateTime)), "Timestamp should be set");
            Assert.That(statusResponse.Uptime, Is.GreaterThan(TimeSpan.Zero), "Uptime should be greater than zero");
            Assert.That(statusResponse.Environment, Is.Not.Null.And.Not.Empty, "Environment field should not be empty");
        }

        [Test]
        public async Task StatusEndpoint_IncludesComponentHealth()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");
            var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();

            // Assert
            Assert.That(statusResponse, Is.Not.Null, "Response should not be null");
            Assert.That(statusResponse!.Components, Is.Not.Null, "Components should not be null");
            
            // Should have at least IPFS, Algorand, and EVM health checks
            Assert.That(statusResponse.Components.Keys, Does.Contain("ipfs"), 
                "Should include IPFS health check");
            Assert.That(statusResponse.Components.Keys, Does.Contain("algorand"), 
                "Should include Algorand health check");
            Assert.That(statusResponse.Components.Keys, Does.Contain("evm"), 
                "Should include EVM health check");

            // Each component should have status
            foreach (var component in statusResponse.Components)
            {
                Assert.That(component.Value.Status, Is.Not.Null.And.Not.Empty,
                    $"Component {component.Key} should have a status");
            }
        }

        [Test]
        public async Task StatusEndpoint_ReturnsConsistentFormat()
        {
            // Act - Call endpoint multiple times
            var response1 = await _client.GetAsync("/api/v1/status");
            var response2 = await _client.GetAsync("/api/v1/status");

            var status1 = await response1.Content.ReadFromJsonAsync<ApiStatusResponse>();
            var status2 = await response2.Content.ReadFromJsonAsync<ApiStatusResponse>();

            // Assert - Structure should be consistent
            Assert.That(status1, Is.Not.Null);
            Assert.That(status2, Is.Not.Null);
            Assert.That(status1!.Version, Is.EqualTo(status2!.Version), 
                "Version should be consistent");
            Assert.That(status1.Environment, Is.EqualTo(status2.Environment), 
                "Environment should be consistent");
            Assert.That(status1.Components.Keys, Is.EquivalentTo(status2.Components.Keys),
                "Component list should be consistent");
        }

        [Test]
        public async Task HealthEndpoints_AreAccessibleWithoutAuthentication()
        {
            // Arrange - Create client without any authentication headers
            var unauthenticatedClient = _factory.CreateClient();

            // Act & Assert - All health endpoints should be accessible
            var healthResponse = await unauthenticatedClient.GetAsync("/health");
            Assert.That(healthResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), 
                "/health should be accessible without authentication");

            var readyResponse = await unauthenticatedClient.GetAsync("/health/ready");
            Assert.That(readyResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.ServiceUnavailable), 
                "/health/ready should be accessible without authentication");

            var liveResponse = await unauthenticatedClient.GetAsync("/health/live");
            Assert.That(liveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), 
                "/health/live should be accessible without authentication");

            var statusResponse = await unauthenticatedClient.GetAsync("/api/v1/status");
            Assert.That(statusResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.ServiceUnavailable), 
                "/api/v1/status should be accessible without authentication");

            unauthenticatedClient.Dispose();
        }

        [Test]
        public async Task StatusEndpoint_IncludesUptimeMetric()
        {
            // Act
            var response1 = await _client.GetAsync("/api/v1/status");
            await Task.Delay(100); // Wait a bit
            var response2 = await _client.GetAsync("/api/v1/status");

            var status1 = await response1.Content.ReadFromJsonAsync<ApiStatusResponse>();
            var status2 = await response2.Content.ReadFromJsonAsync<ApiStatusResponse>();

            // Assert
            Assert.That(status1, Is.Not.Null);
            Assert.That(status2, Is.Not.Null);
            Assert.That(status1!.Uptime, Is.GreaterThan(TimeSpan.Zero), 
                "First uptime should be greater than zero");
            Assert.That(status2!.Uptime, Is.GreaterThanOrEqualTo(status1.Uptime), 
                "Second uptime should be greater than or equal to first");
        }

        [Test]
        public async Task StatusEndpoint_IncludesStripeHealthCheck()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");
            var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();

            // Assert
            Assert.That(statusResponse, Is.Not.Null, "Response should not be null");
            Assert.That(statusResponse!.Components, Is.Not.Null, "Components should not be null");
            
            // Should include Stripe health check
            Assert.That(statusResponse.Components.Keys, Does.Contain("stripe"), 
                "Should include Stripe health check for payment system monitoring");

            // Stripe component should have status (Healthy, Degraded, or Unhealthy)
            var stripeComponent = statusResponse.Components["stripe"];
            Assert.That(stripeComponent.Status, Is.Not.Null.And.Not.Empty,
                "Stripe component should have a status");
            Assert.That(new[] { "Healthy", "Degraded", "Unhealthy" }, Does.Contain(stripeComponent.Status),
                "Stripe status should be one of the valid health states");
        }
    }
}
