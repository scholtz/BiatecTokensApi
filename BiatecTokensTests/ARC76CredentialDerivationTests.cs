using AlgorandARC76AccountDotNet;
using Algorand;
using BiatecTokensApi.Models.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Unit tests for ARC76 credential derivation and mnemonic management
    /// Tests cover: account generation through public API, deterministic behavior, and account uniqueness
    /// 
    /// Business Value: Ensures zero-wallet friction for non-crypto users by validating that
    /// ARC76 accounts are correctly derived from credentials and securely managed backend-side.
    /// 
    /// Risk Mitigation: Validates deterministic account generation for consistent user experience
    /// and ensures each user gets a unique Algorand address.
    /// </summary>
    [TestFixture]
    public class ARC76CredentialDerivationTests
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
                
                // Key management configuration for tests
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

        #region ARC76 Account Generation Tests

        [Test]
        public async Task Register_ShouldGenerateValidAlgorandAddress()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = $"test-{Guid.NewGuid()}@example.com",
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!",
                FullName = "Test User"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "User should receive ARC76-derived Algorand address");
            Assert.That(result.AlgorandAddress!.Length, Is.EqualTo(58),
                "Algorand address should be 58 characters");
            
            // Validate address format (starts with capital letter, alphanumeric)
            Assert.That(result.AlgorandAddress, Does.Match("^[A-Z2-7]{58}$"),
                "Algorand address should use base32 alphabet");
        }

        [Test]
        public async Task Register_MultipleTimes_ShouldGenerateDifferentAddresses()
        {
            // Arrange & Act - Register 3 different users
            var user1 = await RegisterUserAsync();
            var user2 = await RegisterUserAsync();
            var user3 = await RegisterUserAsync();

            // Assert
            Assert.That(user1.AlgorandAddress, Is.Not.EqualTo(user2.AlgorandAddress),
                "Each user should have unique Algorand address");
            Assert.That(user2.AlgorandAddress, Is.Not.EqualTo(user3.AlgorandAddress),
                "Each user should have unique Algorand address");
            Assert.That(user1.AlgorandAddress, Is.Not.EqualTo(user3.AlgorandAddress),
                "Each user should have unique Algorand address");
        }

        #endregion

        #region Deterministic Account Tests

        [Test]
        public async Task LoginMultipleTimes_ShouldReturnSameAddress()
        {
            // Arrange - Register user first
            var email = $"test-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            await RegisterUserWithCredentialsAsync(email, password);

            // Act - Login 3 times
            var login1 = await LoginAndGetProfileAsync(email, password);
            var login2 = await LoginAndGetProfileAsync(email, password);
            var login3 = await LoginAndGetProfileAsync(email, password);

            // Assert - Same address every time (deterministic)
            Assert.That(login1, Is.EqualTo(login2),
                "Same user should have same address across logins");
            Assert.That(login2, Is.EqualTo(login3),
                "Same user should have same address across logins");
        }

        #endregion

        #region Password Change Tests

        [Test]
        public async Task ChangePassword_ShouldMaintainSameAlgorandAddress()
        {
            // Arrange - Register and get initial address
            var email = $"test-{Guid.NewGuid()}@example.com";
            var oldPassword = "OldPassword123!";
            var newPassword = "NewPassword456!";
            
            // Step 1: Register user
            await RegisterUserWithCredentialsAsync(email, oldPassword);
            
            // Step 2: Login and get initial address
            var addressBeforeChange = await LoginAndGetProfileAsync(email, oldPassword);
            Assert.That(addressBeforeChange, Is.Not.Null.And.Not.Empty,
                "User should have valid address before password change");

            // Step 3: Get access token for authenticated request
            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
            {
                Email = email,
                Password = oldPassword
            });
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginResult?.Success, Is.True);
            Assert.That(loginResult?.AccessToken, Is.Not.Null);

            // Step 4: Change password
            _client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
            
            var changePasswordResponse = await _client.PostAsJsonAsync("/api/v1/auth/change-password", new
            {
                currentPassword = oldPassword,
                newPassword = newPassword
            });

            Assert.That(changePasswordResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Password change should succeed");

            var changeResult = await changePasswordResponse.Content.ReadAsStringAsync();
            Assert.That(changeResult, Does.Contain("success"),
                "Password change response should indicate success");

            // Step 5: Login with new password and verify address is unchanged
            var addressAfterChange = await LoginAndGetProfileAsync(email, newPassword);

            Assert.That(addressAfterChange, Is.Not.Null.And.Not.Empty,
                "User should have valid address after password change");
            Assert.That(addressAfterChange, Is.EqualTo(addressBeforeChange),
                "Algorand address must remain the same after password change");
        }

        #endregion

        #region Business Logic Tests - User Journey

        [Test]
        public async Task UserRegistration_ThreeUsers_ShouldHaveUniqueAddresses()
        {
            // Arrange - Simulate 3 users registering
            var user1 = await RegisterUserAsync();
            var user2 = await RegisterUserAsync();
            var user3 = await RegisterUserAsync();

            // Act - Collect all addresses
            var addresses = new[] { user1.AlgorandAddress, user2.AlgorandAddress, user3.AlgorandAddress };

            // Assert - Each user gets unique Algorand address
            Assert.That(addresses.Distinct().Count(), Is.EqualTo(3),
                "Each user should have unique Algorand address");
        }

        [Test]
        public async Task TokenDeployment_DerivedAccountShouldBeConsistentAcrossRequests()
        {
            // Arrange - Register user and login
            var email = $"test-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            await RegisterUserWithCredentialsAsync(email, password);

            // Act - Get address from profile multiple times (simulating multiple deployment requests)
            var address1 = await LoginAndGetProfileAsync(email, password);
            var address2 = await LoginAndGetProfileAsync(email, password);
            var address3 = await LoginAndGetProfileAsync(email, password);

            // Assert - Same address should be used for all deployments
            Assert.That(address1, Is.EqualTo(address2),
                "Account should be consistent across requests");
            Assert.That(address2, Is.EqualTo(address3),
                "Account should be consistent across requests");
        }

        [Test]
        public async Task ConcurrentRegistrations_ShouldGenerateUniqueAddresses()
        {
            // Arrange - Create 5 concurrent registration requests
            var tasks = new List<Task<RegisterResponse>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(RegisterUserAsync());
            }

            // Act - Execute concurrently
            var results = await Task.WhenAll(tasks);

            // Assert - All addresses should be unique
            var addresses = results.Select(r => r.AlgorandAddress).ToList();
            Assert.That(addresses.Distinct().Count(), Is.EqualTo(5),
                "Concurrent registrations should generate unique addresses");
        }

        #endregion

        #region Algorand Address Validation Tests

        [Test]
        public async Task RegisteredAddress_ShouldBeValidAlgorandAddress()
        {
            // Arrange & Act
            var user = await RegisterUserAsync();

            // Assert - Validate address can be parsed by Algorand SDK
            Assert.DoesNotThrow(() =>
            {
                var address = new Address(user.AlgorandAddress!);
                Assert.That(address.ToString(), Is.EqualTo(user.AlgorandAddress),
                    "Address should be valid Algorand address");
            }, "Generated address should be valid according to Algorand SDK");
        }

        #endregion

        #region Helper Methods

        private async Task<RegisterResponse> RegisterUserAsync()
        {
            var email = $"test-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            return await RegisterUserWithCredentialsAsync(email, password);
        }

        private async Task<RegisterResponse> RegisterUserWithCredentialsAsync(string email, string password)
        {
            var request = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                FullName = "Test User"
            };

            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            
            Assert.That(result?.Success, Is.True, $"Registration should succeed for {email}");
            return result!;
        }

        private async Task<string> LoginAndGetProfileAsync(string email, string password)
        {
            // Login
            var loginRequest = new LoginRequest { Email = email, Password = password };
            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

            Assert.That(loginResult?.Success, Is.True, "Login should succeed");

            // Get profile to retrieve address
            _client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
            var profileResponse = await _client.GetAsync("/api/v1/auth/profile");
            var profileJson = await profileResponse.Content.ReadAsStringAsync();

            _client.DefaultRequestHeaders.Authorization = null; // Clear for next test

            Assert.That(profileJson, Does.Contain("algorandAddress"));
            
            // Parse algorandAddress from JSON (simple extraction)
            var addressMatch = System.Text.RegularExpressions.Regex.Match(profileJson, "\"algorandAddress\"\\s*:\\s*\"([A-Z2-7]{58})\"");
            Assert.That(addressMatch.Success, Is.True, "Profile should contain valid Algorand address");
            
            return addressMatch.Groups[1].Value;
        }

        #endregion
    }
}
