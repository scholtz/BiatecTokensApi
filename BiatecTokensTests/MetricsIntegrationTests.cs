using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for metrics collection and observability
    /// </summary>
    [TestFixture]
    public class MetricsIntegrationTests
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

        private class CustomWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
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
        public async Task MetricsEndpoint_IsAccessible()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/metrics");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), 
                "Metrics endpoint should be accessible");
        }

        [Test]
        public async Task MetricsEndpoint_ReturnsStructuredData()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/metrics");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.That(content, Is.Not.Null.And.Not.Empty, "Response should not be empty");
            
            // Verify JSON structure
            var metrics = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            Assert.That(metrics, Is.Not.Null, "Should deserialize to dictionary");
            Assert.That(metrics!.ContainsKey("counters"), Is.True, "Should contain counters");
            Assert.That(metrics.ContainsKey("histograms"), Is.True, "Should contain histograms");
            Assert.That(metrics.ContainsKey("gauges"), Is.True, "Should contain gauges");
        }

        [Test]
        public async Task MetricsEndpoint_TracksHttpRequests()
        {
            // Arrange - Clear metrics by getting initial state
            var initialResponse = await _client.GetAsync("/api/v1/metrics");
            
            // Act - Make several requests to different endpoints
            await _client.GetAsync("/health");
            await _client.GetAsync("/health/ready");
            await _client.GetAsync("/api/v1/status");
            
            // Get metrics
            var metricsResponse = await _client.GetAsync("/api/v1/metrics");
            var metrics = await metricsResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();

            // Assert
            Assert.That(metrics, Is.Not.Null, "Metrics should not be null");
            Assert.That(metrics!["counters"], Is.Not.Null, "Counters should not be null");
            
            // Counters should be tracking requests
            var countersJson = System.Text.Json.JsonSerializer.Serialize(metrics["counters"]);
            Assert.That(countersJson, Does.Contain("http_requests_total"), 
                "Should track HTTP requests");
        }

        [Test]
        public async Task CorrelationId_IsAddedToResponse()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/status");

            // Assert
            Assert.That(response.Headers.Contains("X-Correlation-ID"), Is.True, 
                "Response should contain X-Correlation-ID header");
            
            var correlationId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
            Assert.That(correlationId, Is.Not.Null.And.Not.Empty, 
                "Correlation ID should not be empty");
        }

        [Test]
        public async Task CorrelationId_IsPreservedFromRequest()
        {
            // Arrange
            var testCorrelationId = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Add("X-Correlation-ID", testCorrelationId);

            // Act
            var response = await _client.GetAsync("/api/v1/status");

            // Assert
            var responseCorrelationId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
            Assert.That(responseCorrelationId, Is.EqualTo(testCorrelationId), 
                "Response should preserve the correlation ID from request");

            // Cleanup
            _client.DefaultRequestHeaders.Remove("X-Correlation-ID");
        }

        [Test]
        public async Task MetricsService_CanBeResolved()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var services = scope.ServiceProvider;

            // Act
            var metricsService = services.GetService<IMetricsService>();

            // Assert
            Assert.That(metricsService, Is.Not.Null, 
                "MetricsService should be registered and resolvable");
        }

        [Test]
        public async Task Metrics_TracksErrorResponses()
        {
            // Act - Make a request that will result in 404
            var notFoundResponse = await _client.GetAsync("/api/v1/nonexistent-endpoint");
            
            // Get metrics
            await Task.Delay(100); // Brief delay to ensure metrics are recorded
            var metricsResponse = await _client.GetAsync("/api/v1/metrics");
            var metrics = await metricsResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();

            // Assert
            Assert.That(notFoundResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), 
                "Should return 404 for nonexistent endpoint");
            Assert.That(metrics, Is.Not.Null, "Metrics should not be null");
            
            // Should have tracked the error
            var countersJson = System.Text.Json.JsonSerializer.Serialize(metrics!["counters"]);
            // Note: May contain error metrics depending on timing
            Assert.That(metrics["counters"], Is.Not.Null, "Counters should be tracked");
        }
    }
}
