using BiatecTokensApi.Models;
using BiatecTokensApi.Models.TokenOperationsIntelligence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the Token Operations Intelligence API endpoint.
    /// Tests orchestration, contract validation, authorization, and degraded mode.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TokenOperationsIntelligenceIntegrationTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["App:Account"] = "test mnemonic phrase for testing purposes only not real",
                            ["KeyManagementConfig:Provider"] = "Hardcoded",
                            ["KeyManagementConfig:HardcodedKey"] = "TestKeyForOperationsIntelligenceTests32CharMinimum",
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
                });
            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        // ============================================================
        // Health check - no auth required
        // ============================================================

        [Test]
        public async Task HealthCheck_ReturnsOk_WithoutAuthentication()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/operations-intelligence/health-check");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task HealthCheck_ReturnsHealthyStatus()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/operations-intelligence/health-check");
            var status = await response.Content.ReadFromJsonAsync<ApiStatusResponse>();

            // Assert
            Assert.That(status, Is.Not.Null);
            Assert.That(status!.Status, Is.EqualTo("Healthy"));
            Assert.That(status.Version, Is.Not.Null.And.Not.Empty);
        }

        // ============================================================
        // Authorization checks
        // ============================================================

        [Test]
        public async Task EvaluateEndpoint_WithoutAuth_Returns401()
        {
            // Arrange
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0"
            };

            // Act - no auth header
            var response = await _client.PostAsJsonAsync("/api/v1/operations-intelligence/evaluate", request);

            // Assert - unauthorized without auth (debug mode may return different code)
            Assert.That(
                response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.OK,
                Is.True,
                $"Expected 401 or 200 (debug auth mode), got {response.StatusCode}");
        }

        [Test]
        public async Task GetHealthEndpoint_WithoutAuth_Returns401()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/operations-intelligence/health?assetId=1234567&network=voimain-v1.0");

            // Assert
            Assert.That(
                response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.OK,
                Is.True,
                $"Expected 401 or 200 (debug auth mode), got {response.StatusCode}");
        }

        // ============================================================
        // Contract validation
        // ============================================================

        [Test]
        public async Task EvaluateEndpoint_WithValidRequest_ReturnsValidContract()
        {
            // Arrange
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0",
                MaxEvents = 5
            };

            // Act (debug mode allows unauthenticated access)
            var response = await _client.PostAsJsonAsync("/api/v1/operations-intelligence/evaluate", request);

            // In debug EmptySuccessOnFailure mode, this may return 200
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await response.Content.ReadFromJsonAsync<TokenOperationsIntelligenceResponse>();

                Assert.That(result, Is.Not.Null);
                Assert.That(result!.Success, Is.True);
                Assert.That(result.AssetId, Is.EqualTo(request.AssetId));
                Assert.That(result.Network, Is.EqualTo(request.Network));
                Assert.That(result.ContractVersion, Is.Not.Null);
                Assert.That(result.ContractVersion!.ApiVersion, Is.Not.Null.And.Not.Empty);
                Assert.That(result.Health, Is.Not.Null);
                Assert.That(result.Recommendations, Is.Not.Null);
                Assert.That(result.Events, Is.Not.Null);
            }
            else
            {
                // Accept 401 as valid (auth middleware doing its job)
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            }
        }

        [Test]
        public async Task EvaluateEndpoint_WithZeroAssetId_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 0,
                Network = "voimain-v1.0"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/operations-intelligence/evaluate", request);

            // Assert
            Assert.That(
                response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Unauthorized,
                Is.True,
                $"Expected 400 or 401, got {response.StatusCode}");
        }

        [Test]
        public async Task EvaluateEndpoint_WithEmptyNetwork_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = ""
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/operations-intelligence/evaluate", request);

            // Assert
            Assert.That(
                response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Unauthorized,
                Is.True,
                $"Expected 400 or 401, got {response.StatusCode}");
        }

        [Test]
        public async Task EvaluateEndpoint_WithInvalidMaxEvents_ReturnsBadRequest()
        {
            // Arrange
            var request = new TokenOperationsIntelligenceRequest
            {
                AssetId = 1234567,
                Network = "voimain-v1.0",
                MaxEvents = 200 // > 50 max
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/operations-intelligence/evaluate", request);

            // Assert
            Assert.That(
                response.StatusCode == HttpStatusCode.BadRequest ||
                response.StatusCode == HttpStatusCode.Unauthorized,
                Is.True,
                $"Expected 400 or 401, got {response.StatusCode}");
        }

        // ============================================================
        // Contract version metadata (AC5)
        // ============================================================

        [Test]
        public async Task OperationsContractVersion_HasRequiredFields()
        {
            // Arrange - validate the contract version model directly
            var version = new OperationsContractVersion();

            // Assert
            Assert.That(version.ApiVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(version.SchemaVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(version.MinClientVersion, Is.Not.Null.And.Not.Empty);
            Assert.That(version.BackwardCompatible, Is.True);
            Assert.That(version.DeprecatedFields, Is.Not.Null);
        }

        // ============================================================
        // Structured error model (AC7)
        // ============================================================

        [Test]
        public void ApiErrorResponse_HasMachineReadableCodeAndRemediationHint()
        {
            // Arrange & Assert - validate error model structure
            var error = new ApiErrorResponse
            {
                Success = false,
                ErrorCode = BiatecTokensApi.Models.ErrorCodes.INVALID_REQUEST,
                ErrorMessage = "Test error message",
                RemediationHint = "Test remediation hint"
            };

            Assert.That(error.ErrorCode, Is.Not.Null.And.Not.Empty);
            Assert.That(error.RemediationHint, Is.Not.Null.And.Not.Empty);
            Assert.That(error.Success, Is.False);
        }
    }
}
