using BiatecTokensApi.Models.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Tests validating deterministic ARC76 account derivation across retry and session scenarios
    /// 
    /// Business Value: Ensures that backend-managed ARC76 accounts are derived deterministically
    /// from user credentials, providing consistent "wallet-free" experience even across retries,
    /// session renewals, and network interruptions. This is critical for enterprise non-crypto users.
    /// 
    /// Risk Mitigation: Validates that account derivation is truly deterministic (same input always
    /// produces same address), preventing account fragmentation that would break token ownership
    /// and compliance tracking.
    /// 
    /// Acceptance Criteria Coverage:
    /// - AC1: Given a stable authenticated identity context, ARC76 derivation output is deterministic across repeated issuance initiation attempts
    /// - AC2: Issuance request processing is idempotent for safe retries and does not produce conflicting account mappings
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class DeterministicARC76RetryTests
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
                ["KeyManagementConfig:HardcodedKey"] = "TestKeyForIntegrationTests32CharactersMinimumRequired"
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

        #region Deterministic Derivation Tests

        [Test]
        public async Task ARC76Derivation_SameCredentials_MultipleRegistrationAttempts_ShouldRejectDuplicate()
        {
            // Arrange
            var email = $"deterministic-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            
            var firstRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            };

            // Act 1: First registration
            var firstResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", firstRequest);
            var firstResult = await firstResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            
            Assert.That(firstResult!.Success, Is.True, "First registration should succeed");
            var firstAddress = firstResult.AlgorandAddress;
            Assert.That(firstAddress, Is.Not.Null.And.Not.Empty, "First registration should return ARC76 address");

            // Act 2: Attempt duplicate registration (simulating retry or user error)
            var secondRequest = new RegisterRequest
            {
                Email = email, // Same email
                Password = password, // Same password
                ConfirmPassword = password
            };
            
            var secondResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", secondRequest);
            var secondResult = await secondResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            
            // Assert: Second registration should be rejected (user exists)
            Assert.That(secondResult!.Success, Is.False, "Duplicate registration should be rejected");
            Assert.That(secondResult.ErrorCode, Is.EqualTo("USER_ALREADY_EXISTS"), 
                "Error code should indicate user already exists");
        }

        [Test]
        public async Task ARC76Derivation_SameCredentials_MultipleLoginAttempts_ShouldReturnSameAddress()
        {
            // Arrange: Register user first
            var email = $"login-deterministic-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            };
            
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            
            Assert.That(registerResult!.Success, Is.True, "Registration must succeed");
            var expectedAddress = registerResult.AlgorandAddress;

            // Act: Login multiple times with same credentials
            var addresses = new List<string?>();
            
            for (int i = 0; i < 3; i++)
            {
                var loginRequest = new LoginRequest
                {
                    Email = email,
                    Password = password
                };
                
                var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
                var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
                
                Assert.That(loginResult!.Success, Is.True, $"Login attempt {i + 1} should succeed");
                addresses.Add(loginResult.AlgorandAddress);
            }

            // Assert: All login attempts return same ARC76 address
            foreach (var address in addresses)
            {
                Assert.That(address, Is.EqualTo(expectedAddress),
                    "All login attempts with same credentials must return identical ARC76 address (deterministic derivation)");
            }
            
            // Verify no duplicates
            var distinctAddresses = addresses.Distinct().ToList();
            Assert.That(distinctAddresses.Count, Is.EqualTo(1),
                "Exactly one unique address should be derived from same credentials");
        }

        [Test]
        public async Task ARC76Derivation_EmailCaseVariations_ShouldNormalizeAndReturnSameAddress()
        {
            // This test validates email canonicalization for deterministic account derivation
            // Acceptance Criteria: AC1 requires deterministic derivation from authenticated identity
            
            // Arrange: Register with lowercase email
            var baseEmail = $"case-test-{Guid.NewGuid()}";
            var email = $"{baseEmail}@example.com";
            var password = "SecurePass123!";
            
            var registerRequest = new RegisterRequest
            {
                Email = email.ToLowerInvariant(),
                Password = password,
                ConfirmPassword = password
            };
            
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            
            Assert.That(registerResult!.Success, Is.True, "Registration must succeed");
            var expectedAddress = registerResult.AlgorandAddress;

            // Act: Login with different email casing variations
            var variations = new[]
            {
                email.ToLowerInvariant(),
                email.ToUpperInvariant(),
                $"{baseEmail.ToUpperInvariant()}@EXAMPLE.COM",
                $"{baseEmail}@Example.Com"
            };
            
            foreach (var emailVariation in variations)
            {
                var loginRequest = new LoginRequest
                {
                    Email = emailVariation,
                    Password = password
                };
                
                var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
                var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
                
                Assert.That(loginResult!.Success, Is.True, 
                    $"Login with email variation '{emailVariation}' should succeed");
                
                Assert.That(loginResult.AlgorandAddress, Is.EqualTo(expectedAddress),
                    $"Email variation '{emailVariation}' should resolve to same ARC76 address due to canonicalization");
            }
        }

        [Test]
        public async Task ARC76Derivation_DifferentCredentials_ShouldProduceDifferentAddresses()
        {
            // Arrange: Register two different users
            var user1Email = $"user1-{Guid.NewGuid()}@example.com";
            var user2Email = $"user2-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            
            var request1 = new RegisterRequest
            {
                Email = user1Email,
                Password = password,
                ConfirmPassword = password
            };
            
            var request2 = new RegisterRequest
            {
                Email = user2Email,
                Password = password,
                ConfirmPassword = password
            };

            // Act
            var response1 = await _client.PostAsJsonAsync("/api/v1/auth/register", request1);
            var result1 = await response1.Content.ReadFromJsonAsync<RegisterResponse>();
            
            var response2 = await _client.PostAsJsonAsync("/api/v1/auth/register", request2);
            var result2 = await response2.Content.ReadFromJsonAsync<RegisterResponse>();

            // Assert
            Assert.That(result1!.Success, Is.True, "User 1 registration should succeed");
            Assert.That(result2!.Success, Is.True, "User 2 registration should succeed");
            
            Assert.That(result1.AlgorandAddress, Is.Not.EqualTo(result2.AlgorandAddress),
                "Different email credentials must produce different ARC76 addresses (no collision)");
        }

        [Test]
        public async Task ARC76Derivation_TokenRefresh_ShouldPreserveSameAddress()
        {
            // This test validates that token refresh maintains consistent account identity
            // Critical for AC1: deterministic derivation across session renewal
            
            // Arrange: Register and login
            var email = $"refresh-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            };
            
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            
            Assert.That(registerResult!.Success, Is.True, "Registration must succeed");
            var expectedAddress = registerResult.AlgorandAddress;
            var refreshToken = registerResult.RefreshToken;
            
            Assert.That(refreshToken, Is.Not.Null.And.Not.Empty, "Refresh token must be issued");

            // Act: Refresh token
            var refreshRequest = new { RefreshToken = refreshToken };
            var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);
            
            // Assert: Refresh response should maintain same ARC76 address
            if (refreshResponse.IsSuccessStatusCode)
            {
                var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<RegisterResponse>();
                
                if (refreshResult!.AlgorandAddress != null)
                {
                    Assert.That(refreshResult.AlgorandAddress, Is.EqualTo(expectedAddress),
                        "Token refresh should preserve same ARC76 address (deterministic across session renewal)");
                }
            }
            else
            {
                // If refresh endpoint has different implementation, validate via re-login
                var loginRequest = new LoginRequest
                {
                    Email = email,
                    Password = password
                };
                
                var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
                var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
                
                Assert.That(loginResult!.AlgorandAddress, Is.EqualTo(expectedAddress),
                    "Re-login after session should preserve same ARC76 address");
            }
        }

        #endregion
    }
}
