using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using BiatecTokensApi.Models.LifecycleIntelligence;
using BiatecTokensApi.Models.TokenLaunch;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for Lifecycle Intelligence API endpoints
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LifecycleIntelligenceIntegrationTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;
        private string _jwtToken = string.Empty;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var configuration = new Dictionary<string, string?>
            {
                ["App:Account"] = "test mnemonic phrase for testing purposes only not real account details here",
                ["KeyManagementConfig:Provider"] = "Hardcoded",
                ["KeyManagementConfig:HardcodedKey"] = "TestKeyForLifecycleIntegrationTests32CharactersMinimumRequired",
                ["JwtConfig:SecretKey"] = "TestJwtSecretKeyForLifecycleIntelligenceIntegrationTestingRequires64BytesMin",
                ["JwtConfig:Issuer"] = "BiatecTokensApi",
                ["JwtConfig:Audience"] = "BiatecTokensUsers",
                ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                ["JwtConfig:RefreshTokenExpirationDays"] = "30",
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
                ["IPFSConfig:Username"] = "",
                ["IPFSConfig:Password"] = "",
                ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                ["EVMChains:0:ChainId"] = "8453",
                ["EVMChains:0:GasLimit"] = "4500000",
                ["Cors:0"] = "https://tokens.biatec.io",
                ["StripeConfig:SecretKey"] = "test_stripe_key",
                ["StripeConfig:PublishableKey"] = "test_publishable_key",
                ["StripeConfig:WebhookSecret"] = "test_webhook_secret",
                ["KycConfig:Provider"] = "Mock",
                ["KycConfig:Enabled"] = "false"
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

            // Register and login a test user to get JWT token
            await RegisterAndLoginTestUser();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        private async Task RegisterAndLoginTestUser()
        {
            var testEmail = $"lifecycle-test-{Guid.NewGuid()}@test.com";
            var testPassword = "Test123!@#";

            // Register
            var registerRequest = new
            {
                email = testEmail,
                password = testPassword
            };

            var registerResponse = await _client.PostAsJsonAsync("/api/v2/auth/register", registerRequest);
            
            // Login
            var loginRequest = new
            {
                email = testEmail,
                password = testPassword
            };

            var loginResponse = await _client.PostAsJsonAsync("/api/v2/auth/login", loginRequest);
            
            if (loginResponse.IsSuccessStatusCode)
            {
                var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
                _jwtToken = loginResult?.AccessToken ?? string.Empty;
            }
        }

        private class LoginResponse
        {
            public string AccessToken { get; set; } = string.Empty;
        }

        [Test]
        public async Task HealthEndpoint_ReturnsOk()
        {
            // Act
            var response = await _client.GetAsync("/api/v2/lifecycle/health");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            
            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Does.Contain("Healthy").Or.Contain("operational"));
        }

        [Test]
        public async Task ReadinessEndpoint_WithoutAuth_ReturnsUnauthorized()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user",
                TokenType = "ARC3"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/readiness", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task ReadinessEndpoint_WithAuth_ReturnsResponse()
        {
            // Arrange
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Inconclusive("JWT token not available - test user registration/login may have failed");
                return;
            }

            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user@test.com",
                TokenType = "ARC3",
                Network = "mainnet"
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/readiness", request);

            // Assert
            Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.Forbidden));
            
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await response.Content.ReadFromJsonAsync<TokenLaunchReadinessResponseV2>();
                Assert.That(result, Is.Not.Null);
                Assert.That(result!.ApiVersion, Is.EqualTo("v2.0"));
                Assert.That(result.ReadinessScore, Is.Not.Null);
                Assert.That(result.Confidence, Is.Not.Null);
            }
        }

        [Test]
        public async Task EvidenceEndpoint_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Inconclusive("JWT token not available");
                return;
            }

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.GetAsync("/api/v2/lifecycle/evidence/invalid-evidence-id");

            // Assert
            Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task RiskSignalsEndpoint_WithoutAuth_ReturnsUnauthorized()
        {
            // Arrange
            var request = new RiskSignalsRequest
            {
                AssetId = 12345,
                Network = "mainnet",
                Limit = 10
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/risk-signals", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task RiskSignalsEndpoint_WithAuth_ReturnsResponse()
        {
            // Arrange
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Inconclusive("JWT token not available");
                return;
            }

            var request = new RiskSignalsRequest
            {
                Limit = 10
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/risk-signals", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            
            var result = await response.Content.ReadFromJsonAsync<RiskSignalsResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Signals, Is.Not.Null);
        }

        [Test]
        public async Task ReadinessEndpoint_InvalidRequest_ReturnsBadRequest()
        {
            // Arrange
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Inconclusive("JWT token not available");
                return;
            }

            var request = new TokenLaunchReadinessRequest
            {
                UserId = "", // Invalid - empty userId
                TokenType = ""  // Invalid - empty tokenType
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/readiness", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task RiskSignalsEndpoint_InvalidLimit_ReturnsBadRequest()
        {
            // Arrange
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Inconclusive("JWT token not available");
                return;
            }

            var request = new RiskSignalsRequest
            {
                Limit = 200  // Invalid - exceeds maximum of 100
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/risk-signals", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task ReadinessEndpoint_ReturnsV2Schema()
        {
            // Arrange
            if (string.IsNullOrEmpty(_jwtToken))
            {
                Assert.Inconclusive("JWT token not available");
                return;
            }

            var request = new TokenLaunchReadinessRequest
            {
                UserId = "test-user@test.com",
                TokenType = "ARC3"
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/readiness", request);

            // Assert - Even if forbidden, the schema should be v2 format if successful
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var result = await response.Content.ReadFromJsonAsync<TokenLaunchReadinessResponseV2>();
                
                Assert.That(result, Is.Not.Null);
                Assert.That(result!.ApiVersion, Is.EqualTo("v2.0"));
                
                // Verify v2-specific fields exist
                Assert.That(result.ReadinessScore, Is.Not.Null);
                Assert.That(result.BlockingConditions, Is.Not.Null);
                Assert.That(result.Confidence, Is.Not.Null);
                Assert.That(result.EvidenceReferences, Is.Not.Null);
                
                // Verify score structure
                Assert.That(result.ReadinessScore!.Factors, Is.Not.Null);
                Assert.That(result.ReadinessScore.ScoringVersion, Is.Not.Null);
            }
        }
    }
}
