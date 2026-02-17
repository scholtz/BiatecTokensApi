using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BiatecTokensApi.Models.LifecycleIntelligence;
using BiatecTokensApi.Models.TokenLaunch;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Contract tests for Lifecycle Intelligence API endpoints
    /// Validates response schemas, scoring determinism, and API contracts per verification strategy
    /// 
    /// Business Value: Ensures lifecycle scoring reliability and contract stability for compliance dashboards
    /// Risk Mitigation: Validates deterministic behavior of readiness scoring algorithm
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LifecycleIntelligenceContractTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;
        private string _jwtToken = string.Empty;
        private string _testUserId = string.Empty;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            var configuration = new Dictionary<string, string?>
            {
                ["App:Account"] = "test mnemonic phrase for testing purposes only not real account details here",
                ["KeyManagementConfig:Provider"] = "Hardcoded",
                ["KeyManagementConfig:HardcodedKey"] = "TestKeyForLifecycleContractTests32CharactersMinimumRequired",
                ["JwtConfig:SecretKey"] = "TestJwtSecretKeyForLifecycleIntelligenceContractTestingRequires64BytesMin",
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
            var testEmail = $"lifecycle-contract-test-{Guid.NewGuid()}@test.com";
            var testPassword = "Test123!@#";

            // Register
            var registerRequest = new
            {
                email = testEmail,
                password = testPassword,
                confirmPassword = testPassword,
                fullName = "Lifecycle Contract Test User"
            };

            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<dynamic>();
            _testUserId = registerResult!.userId.ToString();
            
            // Login
            var loginRequest = new
            {
                email = testEmail,
                password = testPassword
            };

            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<dynamic>();
            _jwtToken = loginResult!.accessToken.ToString();
        }

        #region Readiness V2 Contract Tests

        [Test]
        public async Task ReadinessV2Success_ShouldMatchContractSchema()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = _testUserId,
                TokenType = "ASA",
                CorrelationId = Guid.NewGuid().ToString()
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/evaluate-readiness", request);
            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TokenLaunchReadinessResponseV2>(jsonContent, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert - HTTP Status
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Readiness evaluation should return 200 OK");

            // Assert - Required top-level fields
            Assert.That(result, Is.Not.Null, "Response body should not be null");
            Assert.That(result!.EvaluationId, Is.Not.Null.And.Not.Empty, "EvaluationId is required");
            Assert.That(result.Status, Is.Not.Null, "Status is required");
            Assert.That(result.CanProceed, Is.Not.Null, "CanProceed is required");
            Assert.That(result.CorrelationId, Is.EqualTo(request.CorrelationId), "CorrelationId should be echoed");
            Assert.That(result.PolicyVersion, Is.Not.Null.And.Not.Empty, "PolicyVersion is required");

            // Assert - Readiness Score structure
            Assert.That(result.ReadinessScore, Is.Not.Null, "ReadinessScore is required");
            Assert.That(result.ReadinessScore!.Overall, Is.InRange(0.0, 1.0), "Overall score must be 0.0-1.0");
            Assert.That(result.ReadinessScore.Threshold, Is.EqualTo(0.80), "Threshold must be 0.80 per contract");
            Assert.That(result.ReadinessScore.Version, Is.EqualTo("v2.0"), "Scoring version must be v2.0");

            // Assert - Factor breakdown structure
            Assert.That(result.ReadinessScore.Factors, Is.Not.Null, "Factors breakdown is required");
            Assert.That(result.ReadinessScore.Factors.Count, Is.EqualTo(5), "Must have exactly 5 factors");
            
            var requiredFactors = new[] { "entitlement", "account_readiness", "kyc_aml", "compliance", "integration" };
            foreach (var factorName in requiredFactors)
            {
                Assert.That(result.ReadinessScore.Factors.ContainsKey(factorName), Is.True, 
                    $"Factor '{factorName}' is required");
                
                var factor = result.ReadinessScore.Factors[factorName];
                Assert.That(factor.Score, Is.InRange(0.0, 1.0), $"Factor '{factorName}' score must be 0.0-1.0");
                Assert.That(factor.Weight, Is.InRange(0.0, 1.0), $"Factor '{factorName}' weight must be 0.0-1.0");
                Assert.That(factor.Contribution, Is.InRange(0.0, 1.0), $"Factor '{factorName}' contribution must be 0.0-1.0");
            }

            // Assert - Factor weights sum to 1.0
            var totalWeight = result.ReadinessScore.Factors.Values.Sum(f => f.Weight);
            Assert.That(totalWeight, Is.EqualTo(1.0).Within(0.001), "Factor weights must sum to 1.0");

            // Assert - Confidence metadata
            Assert.That(result.Confidence, Is.Not.Null, "Confidence metadata is required");
            Assert.That(result.Confidence!.Level, Is.Not.Null, "Confidence level is required");
            Assert.That(result.Confidence.DataFreshness, Is.Not.Null.And.Not.Empty, "Data freshness is required");
            Assert.That(result.Confidence.LastUpdated, Is.Not.Null, "LastUpdated timestamp is required");

            // Assert - Evidence references
            Assert.That(result.EvidenceReferences, Is.Not.Null, "EvidenceReferences array is required");
            
            // Assert - Timing metadata
            Assert.That(result.EvaluationTimeMs, Is.GreaterThan(0), "EvaluationTimeMs must be positive");
        }

        [Test]
        public async Task ReadinessV2InvalidTokenType_ShouldMatchErrorContract()
        {
            // Arrange
            var request = new
            {
                userId = _testUserId,
                tokenType = "INVALID_TYPE", // Intentionally invalid
                correlationId = Guid.NewGuid().ToString()
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/evaluate-readiness", request);

            // Assert - Should return 400 Bad Request for invalid token type
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), 
                "Invalid token type should return 400 Bad Request");
        }

        [Test]
        public async Task ReadinessV2Unauthorized_ShouldReturn401()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = _testUserId,
                TokenType = "ASA"
            };

            // Act - No auth header
            _client.DefaultRequestHeaders.Authorization = null;
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/evaluate-readiness", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), 
                "Request without authentication should return 401");
        }

        #endregion

        #region Scoring Determinism Tests

        [Test]
        public async Task ReadinessV2MultipleEvaluations_ShouldReturnConsistentScores()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = _testUserId,
                TokenType = "ASA",
                CorrelationId = Guid.NewGuid().ToString()
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act - Evaluate 3 times in quick succession
            var scores = new List<double>();
            for (int i = 0; i < 3; i++)
            {
                var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/evaluate-readiness", request);
                var result = await response.Content.ReadFromJsonAsync<TokenLaunchReadinessResponseV2>();
                scores.Add(result!.ReadinessScore!.Overall);
            }

            // Assert - All scores should be identical (deterministic scoring)
            Assert.That(scores.Distinct().Count(), Is.EqualTo(1), 
                "Readiness scores must be deterministic for same input");
            Assert.That(scores[0], Is.EqualTo(scores[1]));
            Assert.That(scores[1], Is.EqualTo(scores[2]));
        }

        [Test]
        public async Task ReadinessV2FactorWeights_ShouldMatchSpecification()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = _testUserId,
                TokenType = "ASA"
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/evaluate-readiness", request);
            var result = await response.Content.ReadFromJsonAsync<TokenLaunchReadinessResponseV2>();

            // Assert - Factor weights match specification
            var factors = result!.ReadinessScore!.Factors;
            Assert.That(factors["entitlement"].Weight, Is.EqualTo(0.30), "Entitlement weight must be 30%");
            Assert.That(factors["account_readiness"].Weight, Is.EqualTo(0.30), "Account readiness weight must be 30%");
            Assert.That(factors["kyc_aml"].Weight, Is.EqualTo(0.15), "KYC/AML weight must be 15%");
            Assert.That(factors["compliance"].Weight, Is.EqualTo(0.15), "Compliance weight must be 15%");
            Assert.That(factors["integration"].Weight, Is.EqualTo(0.10), "Integration weight must be 10%");
        }

        [Test]
        public async Task ReadinessV2ContributionCalculation_ShouldBeAccurate()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = _testUserId,
                TokenType = "ASA"
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/evaluate-readiness", request);
            var result = await response.Content.ReadFromJsonAsync<TokenLaunchReadinessResponseV2>();

            // Assert - Contribution = Score × Weight
            var factors = result!.ReadinessScore!.Factors;
            foreach (var (factorName, factorData) in factors)
            {
                var expectedContribution = factorData.Score * factorData.Weight;
                Assert.That(factorData.Contribution, Is.EqualTo(expectedContribution).Within(0.001), 
                    $"Factor '{factorName}' contribution should equal score × weight");
            }

            // Assert - Overall score = Sum of contributions
            var sumOfContributions = factors.Values.Sum(f => f.Contribution);
            Assert.That(result.ReadinessScore.Overall, Is.EqualTo(sumOfContributions).Within(0.001), 
                "Overall score should equal sum of factor contributions");
        }

        #endregion

        #region Status Enum Validation

        [Test]
        public async Task ReadinessV2Status_ShouldBeValidEnum()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = _testUserId,
                TokenType = "ASA"
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/evaluate-readiness", request);
            var result = await response.Content.ReadFromJsonAsync<TokenLaunchReadinessResponseV2>();

            // Assert - Status must be one of 4 valid values
            var validStatuses = new[] { "Ready", "Blocked", "Warning", "NeedsReview" };
            Assert.That(validStatuses, Does.Contain(result!.Status), 
                "Status must be one of: Ready, Blocked, Warning, NeedsReview");
        }

        #endregion

        #region Confidence Level Tests

        [Test]
        public async Task ReadinessV2Confidence_ShouldBeValidEnum()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = _testUserId,
                TokenType = "ASA"
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/evaluate-readiness", request);
            var result = await response.Content.ReadFromJsonAsync<TokenLaunchReadinessResponseV2>();

            // Assert - Confidence level must be valid
            var validLevels = new[] { "High", "Medium", "Low" };
            Assert.That(validLevels, Does.Contain(result!.Confidence!.Level), 
                "Confidence level must be one of: High, Medium, Low");
        }

        #endregion

        #region Evidence References Tests

        [Test]
        public async Task ReadinessV2Evidence_ShouldHaveRequiredFields()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = _testUserId,
                TokenType = "ASA"
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/evaluate-readiness", request);
            var result = await response.Content.ReadFromJsonAsync<TokenLaunchReadinessResponseV2>();

            // Assert - If evidence exists, validate structure
            if (result!.EvidenceReferences != null && result.EvidenceReferences.Count > 0)
            {
                foreach (var evidence in result.EvidenceReferences)
                {
                    Assert.That(evidence.EvidenceId, Is.Not.Null.And.Not.Empty, "EvidenceId is required");
                    Assert.That(evidence.Category, Is.Not.Null.And.Not.Empty, "Evidence category is required");
                    Assert.That(evidence.Timestamp, Is.Not.Null, "Evidence timestamp is required");
                    Assert.That(evidence.Hash, Is.Not.Null.And.Not.Empty, "Evidence hash is required");
                    Assert.That(evidence.Hash, Does.StartWith("sha256-"), "Evidence hash should use SHA-256");
                }
            }
        }

        #endregion

        #region Response Deserialization Tests

        [Test]
        public async Task ReadinessV2Response_ShouldDeserializeWithoutErrors()
        {
            // Arrange
            var request = new TokenLaunchReadinessRequest
            {
                UserId = _testUserId,
                TokenType = "ASA"
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/v2/lifecycle/evaluate-readiness", request);
            var jsonContent = await response.Content.ReadAsStringAsync();

            // Assert - Should deserialize without exceptions
            Assert.DoesNotThrow(() =>
            {
                var result = JsonSerializer.Deserialize<TokenLaunchReadinessResponseV2>(jsonContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                Assert.That(result, Is.Not.Null);
            });
        }

        #endregion
    }
}
