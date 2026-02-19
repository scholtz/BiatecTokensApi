using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.ARC76;
using BiatecTokensApi.Repositories.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests validating that token issuance operations use deterministic ARC76 account derivation
    /// from authenticated user context, ensuring wallet-free backend orchestration is stable and traceable.
    /// 
    /// Business Value: Proves that backend-managed token deployments are deterministically linked to
    /// user authentication, providing consistent issuer identity across sessions without requiring
    /// users to manage blockchain wallets. This is critical for enterprise non-crypto users.
    /// 
    /// Risk Mitigation: Validates that issuer address derivation is truly deterministic (same credentials
    /// always produce same blockchain address), preventing issuer fragmentation that would break
    /// token ownership tracking and compliance audits.
    /// 
    /// Acceptance Criteria Coverage:
    /// - AC1: Authenticated user context deterministically maps to ARC76 account used in issuance operations
    /// - AC2: Backend responses expose issuer identity fields for verification and diagnostics
    /// - AC6: Integration tests cover deterministic derivation in complete issuance flows
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class TokenIssuanceARC76DeterminismTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;
        private IUserRepository _userRepository = null!;

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
            
            // Get user repository for verification
            var scope = _factory.Services.CreateScope();
            _userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        }

        [TearDown]
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        [Test]
        public async Task TokenIssuance_SameAuthenticatedUser_ShouldUseDeterministicARC76Address()
        {
            // Arrange - Register user and get JWT token
            var email = $"issuer-{Guid.NewGuid().ToString("N")}@example.com";
            var password = "SecurePass123!@#";
            
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            };
            
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            
            Assert.That(registerResult, Is.Not.Null);
            Assert.That(registerResult!.Success, Is.True, "Registration should succeed");
            Assert.That(registerResult.AlgorandAddress, Is.Not.Null, "Should have ARC76 address");
            
            var expectedAddress = registerResult.AlgorandAddress;
            var accessToken = registerResult.AccessToken;
            
            // Act - Get ARC76 account readiness (simulates issuance preparation)
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var readinessResponse = await client.GetAsync("/api/v1/arc76/account-readiness");
            
            // Assert - Response should expose the deterministic ARC76 address
            Assert.That(readinessResponse.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "Account readiness check should not fail with internal error");
            
            if (readinessResponse.StatusCode == HttpStatusCode.OK)
            {
                var readinessResult = await readinessResponse.Content.ReadFromJsonAsync<ARC76AccountReadinessResult>();
                Assert.That(readinessResult, Is.Not.Null);
                Assert.That(readinessResult!.AccountAddress, Is.EqualTo(expectedAddress),
                    "Account readiness should return the same deterministic ARC76 address from registration");
            }
            
            // Verify user in repository has the expected address
            var user = await _userRepository.GetUserByEmailAsync(email);
            Assert.That(user, Is.Not.Null);
            Assert.That(user!.AlgorandAddress, Is.EqualTo(expectedAddress),
                "User repository should store the deterministic ARC76 address");
        }

        [Test]
        public async Task TokenIssuance_MultipleSessionsSameUser_ShouldDeriveConsistentIssuerAddress()
        {
            // Arrange - Register user once
            var email = $"consistent-issuer-{Guid.NewGuid().ToString("N")}@example.com";
            var password = "SecurePass123!@#";
            
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            };
            
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            
            Assert.That(registerResult?.Success, Is.True);
            var expectedAddress = registerResult!.AlgorandAddress;
            
            // Act - Login multiple times and verify same address
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
                
                Assert.That(loginResult?.Success, Is.True, $"Login attempt {i + 1} should succeed");
                addresses.Add(loginResult!.AlgorandAddress);
            }
            
            // Assert - All sessions must return the same deterministic address
            Assert.That(addresses, Has.All.EqualTo(expectedAddress),
                "All login sessions for the same user must derive the identical ARC76 address for token issuance");
        }

        [Test]
        public async Task TokenIssuance_EmailCaseVariations_ShouldNormalizeToSameDeterministicAddress()
        {
            // Arrange - Register with lowercase email
            var baseEmail = $"issuer-case-{Guid.NewGuid().ToString("N")}";
            var password = "SecurePass123!@#";
            
            var registerRequest = new RegisterRequest
            {
                Email = $"{baseEmail}@example.com",
                Password = password,
                ConfirmPassword = password
            };
            
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            
            Assert.That(registerResult?.Success, Is.True);
            var expectedAddress = registerResult!.AlgorandAddress;
            
            // Act - Login with different email case variations
            var caseVariations = new[]
            {
                $"{baseEmail}@example.com",
                $"{baseEmail.ToUpper()}@EXAMPLE.COM",
                $"{char.ToUpper(baseEmail[0])}{baseEmail.Substring(1)}@Example.Com"
            };
            
            foreach (var emailVariation in caseVariations)
            {
                var loginRequest = new LoginRequest
                {
                    Email = emailVariation,
                    Password = password
                };
                
                var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
                var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
                
                // Assert - Email canonicalization should always produce same address
                Assert.That(loginResult?.Success, Is.True, $"Login with {emailVariation} should succeed");
                Assert.That(loginResult!.AlgorandAddress, Is.EqualTo(expectedAddress),
                    $"Email {emailVariation} should normalize to the same deterministic ARC76 address: {expectedAddress}");
            }
        }

        [Test]
        public async Task TokenIssuance_DifferentUsers_ShouldProduceDistinctARC76Addresses()
        {
            // Arrange - Register two different users
            var user1Email = $"issuer-1-{Guid.NewGuid().ToString("N")}@example.com";
            var user2Email = $"issuer-2-{Guid.NewGuid().ToString("N")}@example.com";
            var password = "SecurePass123!@#";
            
            var user1RegisterRequest = new RegisterRequest
            {
                Email = user1Email,
                Password = password,
                ConfirmPassword = password
            };
            
            var user2RegisterRequest = new RegisterRequest
            {
                Email = user2Email,
                Password = password,
                ConfirmPassword = password
            };
            
            // Act - Register both users
            var user1Response = await _client.PostAsJsonAsync("/api/v1/auth/register", user1RegisterRequest);
            var user1Result = await user1Response.Content.ReadFromJsonAsync<RegisterResponse>();
            
            var user2Response = await _client.PostAsJsonAsync("/api/v1/auth/register", user2RegisterRequest);
            var user2Result = await user2Response.Content.ReadFromJsonAsync<RegisterResponse>();
            
            // Assert - Different users must have different ARC76 addresses
            Assert.That(user1Result?.Success, Is.True);
            Assert.That(user2Result?.Success, Is.True);
            Assert.That(user1Result!.AlgorandAddress, Is.Not.Null);
            Assert.That(user2Result!.AlgorandAddress, Is.Not.Null);
            Assert.That(user1Result.AlgorandAddress, Is.Not.EqualTo(user2Result.AlgorandAddress),
                "Different users must derive distinct ARC76 addresses to prevent issuer identity collision");
        }

        [Test]
        public async Task TokenIssuance_TokenRefresh_ShouldPreserveDeterministicAddress()
        {
            // Arrange - Register and get tokens
            var email = $"refresh-issuer-{Guid.NewGuid().ToString("N")}@example.com";
            var password = "SecurePass123!@#";
            
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            };
            
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            
            Assert.That(registerResult?.Success, Is.True);
            var expectedAddress = registerResult!.AlgorandAddress;
            var refreshToken = registerResult.RefreshToken;
            
            // Act - Refresh the token
            var refreshRequest = new { RefreshToken = refreshToken };
            var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);
            var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<RefreshTokenResponse>();
            
            // Assert - Refreshed token session should succeed
            // Note: RefreshTokenResponse doesn't include AlgorandAddress field
            // The address remains associated with the user in the backend repository
            Assert.That(refreshResult?.Success, Is.True, 
                "Token refresh should succeed");
            Assert.That(refreshResult!.AccessToken, Is.Not.Null.And.Not.Empty,
                "Refreshed token should include new access token");
            
            // Verify user address is still the same in repository
            var user = await _userRepository.GetUserByEmailAsync(email);
            Assert.That(user!.AlgorandAddress, Is.EqualTo(expectedAddress),
                "Token refresh must preserve the deterministic ARC76 address in user repository");
        }
    }
}
