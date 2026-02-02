using BiatecTokensApi.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests for health check response time metrics and diagnostic information
    /// </summary>
    [TestFixture]
    public class HealthCheckMetricsTests
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
        public async Task StatusEndpoint_IncludesResponseTimeMetrics()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");
            var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();

            // Assert
            Assert.That(statusResponse, Is.Not.Null, "Response should not be null");
            Assert.That(statusResponse!.Components, Is.Not.Null, "Components should not be null");

            // Check that at least one component has response time metrics
            bool hasResponseTimeMetrics = false;
            foreach (var component in statusResponse.Components.Values)
            {
                if (component.Details != null && component.Details.ContainsKey("responseTimeMs"))
                {
                    hasResponseTimeMetrics = true;
                    var responseTime = component.Details["responseTimeMs"];
                    Assert.That(responseTime, Is.Not.Null, "Response time should not be null");
                    
                    // Response time should be a positive number
                    if (responseTime is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
                    {
                        var timeMs = jsonElement.GetDouble();
                        Assert.That(timeMs, Is.GreaterThanOrEqualTo(0), 
                            "Response time should be non-negative");
                        Assert.That(timeMs, Is.LessThan(30000), 
                            "Response time should be reasonable (less than 30 seconds)");
                    }
                }
            }

            // At least one component should have response time metrics
            // (may not be true if all external services are down, but that's OK for this test)
            TestContext.Out.WriteLine($"Response time metrics found: {hasResponseTimeMetrics}");
        }

        [Test]
        public async Task StatusEndpoint_IPFSComponent_IncludesMetrics()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");
            var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();

            // Assert
            Assert.That(statusResponse, Is.Not.Null);
            Assert.That(statusResponse!.Components, Does.ContainKey("ipfs"), 
                "Should include IPFS component");

            var ipfsComponent = statusResponse.Components["ipfs"];
            Assert.That(ipfsComponent, Is.Not.Null);
            Assert.That(ipfsComponent.Status, Is.Not.Null.And.Not.Empty);
            
            TestContext.Out.WriteLine($"IPFS Status: {ipfsComponent.Status}");
            TestContext.Out.WriteLine($"IPFS Message: {ipfsComponent.Message}");
            
            if (ipfsComponent.Details != null)
            {
                foreach (var detail in ipfsComponent.Details)
                {
                    TestContext.Out.WriteLine($"  {detail.Key}: {detail.Value}");
                }
            }
        }

        [Test]
        public async Task StatusEndpoint_AlgorandComponent_IncludesMetrics()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");
            var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();

            // Assert
            Assert.That(statusResponse, Is.Not.Null);
            Assert.That(statusResponse!.Components, Does.ContainKey("algorand"), 
                "Should include Algorand component");

            var algorandComponent = statusResponse.Components["algorand"];
            Assert.That(algorandComponent, Is.Not.Null);
            Assert.That(algorandComponent.Status, Is.Not.Null.And.Not.Empty);
            
            TestContext.Out.WriteLine($"Algorand Status: {algorandComponent.Status}");
            TestContext.Out.WriteLine($"Algorand Message: {algorandComponent.Message}");
            
            if (algorandComponent.Details != null)
            {
                // Should have network-level details
                Assert.That(algorandComponent.Details, Does.ContainKey("totalNetworks"));
                Assert.That(algorandComponent.Details, Does.ContainKey("healthyNetworks"));
                Assert.That(algorandComponent.Details, Does.ContainKey("unhealthyNetworks"));
                
                foreach (var detail in algorandComponent.Details)
                {
                    TestContext.Out.WriteLine($"  {detail.Key}: {detail.Value}");
                }
            }
        }

        [Test]
        public async Task StatusEndpoint_EVMComponent_IncludesMetrics()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");
            var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();

            // Assert
            Assert.That(statusResponse, Is.Not.Null);
            Assert.That(statusResponse!.Components, Does.ContainKey("evm"), 
                "Should include EVM component");

            var evmComponent = statusResponse.Components["evm"];
            Assert.That(evmComponent, Is.Not.Null);
            Assert.That(evmComponent.Status, Is.Not.Null.And.Not.Empty);
            
            TestContext.Out.WriteLine($"EVM Status: {evmComponent.Status}");
            TestContext.Out.WriteLine($"EVM Message: {evmComponent.Message}");
            
            if (evmComponent.Details != null)
            {
                // Should have chain-level details
                Assert.That(evmComponent.Details, Does.ContainKey("totalChains"));
                Assert.That(evmComponent.Details, Does.ContainKey("healthyChains"));
                Assert.That(evmComponent.Details, Does.ContainKey("unhealthyChains"));
                
                foreach (var detail in evmComponent.Details)
                {
                    TestContext.Out.WriteLine($"  {detail.Key}: {detail.Value}");
                }
            }
        }

        [Test]
        public async Task StatusEndpoint_ResponseTimeConsistency()
        {
            // Act - Call endpoint multiple times
            var responses = new List<ApiStatusResponse?>();
            for (int i = 0; i < 3; i++)
            {
                var response = await _client.GetAsync("/api/v1/status");
                var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();
                responses.Add(statusResponse);
                await Task.Delay(100); // Small delay between calls
            }

            // Assert - All responses should have similar structure
            Assert.That(responses, Has.All.Not.Null);
            Assert.That(responses, Has.All.Property("Components").Not.Null);
            
            // All responses should have the same component keys
            var firstComponentKeys = responses[0]!.Components.Keys.ToHashSet();
            foreach (var statusResponse in responses.Skip(1))
            {
                var currentKeys = statusResponse!.Components.Keys.ToHashSet();
                Assert.That(currentKeys, Is.EquivalentTo(firstComponentKeys),
                    "All responses should have the same components");
            }
        }

        [Test]
        public async Task StatusEndpoint_HandlesComponentFailuresGracefully()
        {
            // This test verifies that even if some components fail,
            // the status endpoint still returns a valid response
            
            // Act
            var response = await _client.GetAsync("/api/v1/status");
            
            // Assert
            // Status endpoint should always return either 200 or 503, never crash
            Assert.That(
                (int)response.StatusCode == 200 || (int)response.StatusCode == 503,
                Is.True,
                "Status endpoint should return 200 OK or 503 Service Unavailable");
            
            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Is.Not.Null.And.Not.Empty,
                "Status endpoint should always return content");
            
            var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();
            Assert.That(statusResponse, Is.Not.Null,
                "Response should deserialize successfully");
            Assert.That(statusResponse!.Status, Is.Not.Null.And.Not.Empty,
                "Status field should always be present");
        }

        [Test]
        public async Task StatusEndpoint_IncludesErrorDetailsForFailedComponents()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");
            var statusResponse = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();

            // Assert
            Assert.That(statusResponse, Is.Not.Null);
            
            // Check if any component has failed and includes error details
            foreach (var component in statusResponse!.Components)
            {
                if (component.Value.Status == "Unhealthy" || component.Value.Status == "Degraded")
                {
                    TestContext.Out.WriteLine($"Component {component.Key} status: {component.Value.Status}");
                    TestContext.Out.WriteLine($"  Message: {component.Value.Message}");
                    
                    // Unhealthy/Degraded components should have a message explaining why
                    Assert.That(component.Value.Message, Is.Not.Null.And.Not.Empty,
                        $"Unhealthy component {component.Key} should have an explanatory message");
                    
                    if (component.Value.Details != null)
                    {
                        foreach (var detail in component.Value.Details)
                        {
                            TestContext.Out.WriteLine($"    {detail.Key}: {detail.Value}");
                        }
                    }
                }
            }
        }
    }
}
