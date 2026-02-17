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
    public class AuthApiContractTests
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
        public async Task RegisterSuccess_ReturnsRequiredFields()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = $"contract-test-{Guid.NewGuid()}@example.com",
                Password = "StrongPass123!",
                ConfirmPassword = "StrongPass123!",
                FullName = "Contract Test User"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();

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

            // Assert - Algorand address format (exactly 58 characters, base32 alphabet)
            Assert.That(result.AlgorandAddress.Length, Is.EqualTo(58), "Algorand address must be 58 characters");
            Assert.That(result.AlgorandAddress, Does.Match("^[A-Z2-7]{58}$"), "Algorand address must use base32 alphabet");

            // Assert - Error fields null for success
            Assert.That(result.ErrorCode, Is.Null, "ErrorCode should be null for successful response");
            Assert.That(result.ErrorMessage, Is.Null, "ErrorMessage should be null for successful response");
        }

        [Test]
        public async Task RegisterWeakPassword_ReturnsBadRequest()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = $"contract-test-{Guid.NewGuid()}@example.com",
                Password = "weak", // Intentionally weak password (fails model validation)
                ConfirmPassword = "weak",
                FullName = "Test User"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            // Assert - Model validation returns 400 Bad Request for weak password (< 8 chars)
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), 
                "Weak password should fail ASP.NET model validation and return 400");
        }

        [Test]
        public async Task RegisterExistingUser_ReturnsConflictErrorCode()
        {
            // Arrange - Register user first
            var email = $"contract-test-{Guid.NewGuid()}@example.com";
            var firstRequest = new RegisterRequest
            {
                Email = email,
                Password = "StrongPass123!",
                ConfirmPassword = "StrongPass123!",
                FullName = "Test User"
            };
            await _client.PostAsJsonAsync("/api/v1/auth/register", firstRequest);

            // Act - Try to register same email again
            var secondRequest = new RegisterRequest
            {
                Email = email,
                Password = "DifferentPass456!",
                ConfirmPassword = "DifferentPass456!",
                FullName = "Test User 2"
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", secondRequest);
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();

            // Assert - Error contract
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("USER_ALREADY_EXISTS"), "ErrorCode must match documented contract");
        }

        #endregion

        #region Login Contract Tests

        [Test]
        public async Task LoginSuccess_ReturnsRequiredFields()
        {
            // Arrange - Register user first
            var email = $"contract-test-{Guid.NewGuid()}@example.com";
            var password = "StrongPass123!";
            await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                FullName = "Test User"
            });

            // Act - Login
            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

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

            // Assert - Algorand address format
            Assert.That(result.AlgorandAddress.Length, Is.EqualTo(58));
            Assert.That(result.Email, Is.EqualTo(email.ToLowerInvariant()), "Email should be normalized to lowercase");
        }

        [Test]
        public async Task LoginInvalidCredentials_ReturnsTypedErrorCode()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Email = "nonexistent@example.com",
                Password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

            // Assert - Error contract
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_CREDENTIALS"), "ErrorCode must match documented contract");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }

        #endregion

        #region Deterministic Behavior Validation

        [Test]
        public async Task LoginMultipleTimes_ReturnsSameAlgorandAddress()
        {
            // Arrange - Register user
            var email = $"contract-test-{Guid.NewGuid()}@example.com";
            var password = "StrongPass123!";
            await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                FullName = "Test User"
            });

            // Act - Login 3 times
            var addresses = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Email = email, Password = password });
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                addresses.Add(result!.AlgorandAddress!);
            }

            // Assert - Deterministic account derivation: same address every time
            Assert.That(addresses.Distinct().Count(), Is.EqualTo(1), 
                "Same user must receive same Algorand address across multiple logins (deterministic derivation)");
        }

        [Test]
        public async Task RegisterDifferentUsers_ReturnUniqueAlgorandAddresses()
        {
            // Arrange & Act - Register 3 different users
            var addresses = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var request = new RegisterRequest
                {
                    Email = $"contract-test-{Guid.NewGuid()}@example.com",
                    Password = "StrongPass123!",
                    ConfirmPassword = "StrongPass123!",
                    FullName = $"Test User {i}"
                };
                var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
                var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
                addresses.Add(result!.AlgorandAddress!);
            }

            // Assert - Each user gets unique address
            Assert.That(addresses.Distinct().Count(), Is.EqualTo(3),
                "Each user must receive unique Algorand address (no collisions)");
        }

        #endregion
    }
}
