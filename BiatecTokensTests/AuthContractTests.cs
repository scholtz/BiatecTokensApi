using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BiatecTokensApi.Models.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace BiatecTokensTests
{
    /// <summary>
    /// Contract tests for authentication API endpoints
    /// Validates response schemas, error codes, and API contracts per verification strategy
    /// 
    /// Business Value: Ensures API stability and backward compatibility for frontend consumers
    /// Risk Mitigation: Prevents breaking changes to authentication contracts
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class AuthContractTests
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
                ["JwtConfig:SecretKey"] = "test-secret-key-at-least-32-characters-long-for-hs256",
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
                ["EVMChains:Chains:0:RpcUrl"] = "https://sepolia.base.org",
                ["EVMChains:Chains:0:ChainId"] = "84532",
                ["EVMChains:Chains:0:GasLimit"] = "4500000",
                ["StripeConfig:SecretKey"] = "test_key",
                ["StripeConfig:PublishableKey"] = "test_key",
                ["StripeConfig:WebhookSecret"] = "test_secret",
                ["StripeConfig:BasicPriceId"] = "price_test_basic",
                ["StripeConfig:ProPriceId"] = "price_test_pro",
                ["StripeConfig:EnterprisePriceId"] = "price_test_enterprise",
                ["KeyManagementConfig:Provider"] = "Hardcoded",
                ["KeyManagementConfig:HardcodedKey"] = "TestKeyForAuthContractTests32CharactersMinimumRequired"
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

        #region Registration Contract Tests

        [Test]
        public async Task RegisterSuccess_ShouldMatchContractSchema()
        {
            // Arrange
            var request = new
            {
                email = $"contract-test-{Guid.NewGuid()}@example.com",
                password = "StrongPass123!",
                confirmPassword = "StrongPass123!",
                fullName = "Contract Test User"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RegisterResponse>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert - HTTP Status
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Registration should return 200 OK");

            // Assert - Required fields presence
            Assert.That(result, Is.Not.Null, "Response body should not be null");
            Assert.That(result!.Success, Is.True, "Success field must be true for successful registration");
            Assert.That(result.UserId, Is.Not.Null.And.Not.Empty, "UserId is required");
            Assert.That(result.Email, Is.Not.Null.And.Not.Empty, "Email is required");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AlgorandAddress is required");
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty, "AccessToken is required");
            Assert.That(result.RefreshToken, Is.Not.Null.And.Not.Empty, "RefreshToken is required");
            Assert.That(result.ExpiresIn, Is.GreaterThan(0), "ExpiresIn must be positive");

            // Assert - Field formats
            Assert.That(result.AlgorandAddress.Length, Is.EqualTo(58), "Algorand address must be 58 characters");
            Assert.That(result.AlgorandAddress, Does.Match("^[A-Z2-7]{58}$"), "Algorand address must use base32 alphabet");
            Assert.That(Guid.TryParse(result.UserId, out _), Is.True, "UserId must be valid GUID");

            // Assert - Error fields null for success
            Assert.That(result.ErrorCode, Is.Null, "ErrorCode should be null for successful response");
            Assert.That(result.ErrorMessage, Is.Null, "ErrorMessage should be null for successful response");
        }

        [Test]
        public async Task RegisterWeakPassword_ShouldMatchErrorContract()
        {
            // Arrange
            var request = new
            {
                email = $"contract-test-{Guid.NewGuid()}@example.com",
                password = "weak", // Intentionally weak password
                confirmPassword = "weak",
                fullName = "Test User"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RegisterResponse>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert - HTTP Status
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "API returns 200 with error details in body");

            // Assert - Error contract
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.False, "Success must be false for error response");
            Assert.That(result.ErrorCode, Is.EqualTo("WEAK_PASSWORD"), "ErrorCode must match documented contract");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty, "ErrorMessage must be present");
            Assert.That(result.ErrorMessage, Does.Contain("Password"), "Error message should explain password requirements");

            // Assert - Null fields for error response
            Assert.That(result.UserId, Is.Null, "UserId should be null for error response");
            Assert.That(result.AlgorandAddress, Is.Null, "AlgorandAddress should be null for error response");
            Assert.That(result.AccessToken, Is.Null, "AccessToken should be null for error response");
            Assert.That(result.RefreshToken, Is.Null, "RefreshToken should be null for error response");
        }

        [Test]
        public async Task RegisterExistingUser_ShouldMatchErrorContract()
        {
            // Arrange - Register user first
            var email = $"contract-test-{Guid.NewGuid()}@example.com";
            var firstRequest = new
            {
                email = email,
                password = "StrongPass123!",
                confirmPassword = "StrongPass123!",
                fullName = "Test User"
            };
            await _client.PostAsJsonAsync("/api/v1/auth/register", firstRequest);

            // Act - Try to register same email again
            var secondRequest = new
            {
                email = email,
                password = "DifferentPass456!",
                confirmPassword = "DifferentPass456!",
                fullName = "Test User 2"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", secondRequest);
            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RegisterResponse>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert - Error contract
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("USER_ALREADY_EXISTS"), "ErrorCode must match documented contract");
            Assert.That(result.ErrorMessage, Does.Contain("already exists"), "Error message should indicate user exists");
        }

        [Test]
        public async Task RegisterMissingField_ShouldReturnValidationError()
        {
            // Arrange - Missing confirmPassword
            var request = new
            {
                email = $"contract-test-{Guid.NewGuid()}@example.com",
                password = "StrongPass123!"
                // confirmPassword intentionally missing
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), 
                "Missing required field should return 400 Bad Request");
        }

        #endregion

        #region Login Contract Tests

        [Test]
        public async Task LoginSuccess_ShouldMatchContractSchema()
        {
            // Arrange - Register user first
            var email = $"contract-test-{Guid.NewGuid()}@example.com";
            var password = "StrongPass123!";
            var registerRequest = new
            {
                email = email,
                password = password,
                confirmPassword = password,
                fullName = "Test User"
            };
            await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

            // Act - Login
            var loginRequest = new
            {
                email = email,
                password = password
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LoginResponse>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert - HTTP Status
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Assert - Required fields
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.UserId, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Email, Is.Not.Null.And.Not.Empty);
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty);
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RefreshToken, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ExpiresIn, Is.GreaterThan(0));

            // Assert - Field formats
            Assert.That(result.AlgorandAddress.Length, Is.EqualTo(58));
            Assert.That(result.Email, Is.EqualTo(email.ToLowerInvariant()), "Email should be returned in lowercase");

            // Assert - Error fields null
            Assert.That(result.ErrorCode, Is.Null);
            Assert.That(result.ErrorMessage, Is.Null);
        }

        [Test]
        public async Task LoginInvalidCredentials_ShouldMatchErrorContract()
        {
            // Arrange
            var loginRequest = new
            {
                email = "nonexistent@example.com",
                password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LoginResponse>(jsonContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert - HTTP Status
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "API returns 200 with error in body");

            // Assert - Error contract
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_CREDENTIALS"), "ErrorCode must match documented contract");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);

            // Assert - Null fields
            Assert.That(result.UserId, Is.Null);
            Assert.That(result.AlgorandAddress, Is.Null);
            Assert.That(result.AccessToken, Is.Null);
            Assert.That(result.RefreshToken, Is.Null);
        }

        #endregion

        #region Deterministic Behavior Tests

        [Test]
        public async Task LoginMultipleTimes_ShouldReturnConsistentAlgorandAddress()
        {
            // Arrange - Register user
            var email = $"contract-test-{Guid.NewGuid()}@example.com";
            var password = "StrongPass123!";
            var registerRequest = new
            {
                email = email,
                password = password,
                confirmPassword = password,
                fullName = "Test User"
            };
            await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

            // Act - Login 3 times
            var addresses = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var loginRequest = new { email = email, password = password };
                var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                addresses.Add(result!.AlgorandAddress!);
            }

            // Assert - All addresses should be identical (deterministic)
            Assert.That(addresses.Distinct().Count(), Is.EqualTo(1), 
                "Same user must receive same Algorand address across multiple logins (deterministic derivation)");
            Assert.That(addresses[0], Is.EqualTo(addresses[1]));
            Assert.That(addresses[1], Is.EqualTo(addresses[2]));
        }

        [Test]
        public async Task RegisterDifferentUsers_ShouldReturnUniqueAlgorandAddresses()
        {
            // Arrange & Act - Register 3 different users
            var addresses = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var request = new
                {
                    email = $"contract-test-{Guid.NewGuid()}@example.com",
                    password = "StrongPass123!",
                    confirmPassword = "StrongPass123!",
                    fullName = $"Test User {i}"
                };
                var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
                var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
                addresses.Add(result!.AlgorandAddress!);
            }

            // Assert - All addresses should be unique
            Assert.That(addresses.Distinct().Count(), Is.EqualTo(3),
                "Each user must receive unique Algorand address");
        }

        #endregion

        #region Response Type Validation

        [Test]
        public async Task RegisterResponse_ShouldDeserializeWithoutErrors()
        {
            // Arrange
            var request = new
            {
                email = $"contract-test-{Guid.NewGuid()}@example.com",
                password = "StrongPass123!",
                confirmPassword = "StrongPass123!",
                fullName = "Test User"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            var jsonContent = await response.Content.ReadAsStringAsync();
            
            // Assert - Should deserialize without exceptions
            Assert.DoesNotThrow(() =>
            {
                var result = JsonSerializer.Deserialize<RegisterResponse>(jsonContent, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                Assert.That(result, Is.Not.Null);
            });
        }

        [Test]
        public async Task LoginResponse_ShouldDeserializeWithoutErrors()
        {
            // Arrange - Register and login
            var email = $"contract-test-{Guid.NewGuid()}@example.com";
            var password = "StrongPass123!";
            await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email = email,
                password = password,
                confirmPassword = password,
                fullName = "Test User"
            });

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = email,
                password = password
            });
            var jsonContent = await response.Content.ReadAsStringAsync();

            // Assert - Should deserialize without exceptions
            Assert.DoesNotThrow(() =>
            {
                var result = JsonSerializer.Deserialize<LoginResponse>(jsonContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                Assert.That(result, Is.Not.Null);
            });
        }

        #endregion
    }
}
