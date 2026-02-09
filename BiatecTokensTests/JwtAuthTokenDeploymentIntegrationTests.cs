using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Models.ERC20.Request;
using BiatecTokensApi.Models.ERC20.Response;
using BiatecTokensApi.Models.EVM;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for JWT authentication flow combined with token deployment
    /// Tests the complete user journey: register → login → deploy token without wallet
    /// </summary>
    [TestFixture]
    public class JwtAuthTokenDeploymentIntegrationTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        [SetUp]
        public void Setup()
        {
            var configuration = new Dictionary<string, string?>
            {
                // System account for fallback
                ["App:Account"] = "test test test test test test test test test test test test test test test test test test test test test test test test test",
                
                // ARC-0014 authentication config
                ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
                ["AlgorandAuthentication:CheckExpiration"] = "false",
                ["AlgorandAuthentication:Debug"] = "true",
                ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
                ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
                ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
                ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
                
                // JWT configuration for email/password auth
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
                
                // IPFS configuration
                ["IPFSConfig:ApiUrl"] = "https://ipfs-api.biatec.io",
                ["IPFSConfig:GatewayUrl"] = "https://ipfs.biatec.io/ipfs",
                ["IPFSConfig:TimeoutSeconds"] = "30",
                ["IPFSConfig:MaxFileSizeBytes"] = "10485760",
                ["IPFSConfig:ValidateContentHash"] = "true",
                
                // EVM chain configuration (Base testnet for testing)
                ["EVMChains:0:RpcUrl"] = "https://sepolia.base.org",
                ["EVMChains:0:ChainId"] = "84532",
                ["EVMChains:0:GasLimit"] = "4500000",
                
                // Stripe configuration
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

        #region User Registration Tests

        [Test]
        public async Task Register_WithValidCredentials_ShouldSucceed()
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
            Assert.That(result.Email, Is.EqualTo(request.Email));
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty);
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RefreshToken, Is.Not.Null.And.Not.Empty);
            Assert.That(result.UserId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Register_WithWeakPassword_ShouldFail()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = $"test-{Guid.NewGuid()}@example.com",
                Password = "weak",
                ConfirmPassword = "weak"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

            // Assert - Model validation or service validation should return 400
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            
            // Response could be either ModelState errors or RegisterResponse
            var content = await response.Content.ReadAsStringAsync();
            Assert.That(content, Does.Contain("password").IgnoreCase, 
                "Response should mention password validation issue");
        }

        [Test]
        public async Task Register_WithDuplicateEmail_ShouldFail()
        {
            // Arrange
            var email = $"test-{Guid.NewGuid()}@example.com";
            var request1 = new RegisterRequest
            {
                Email = email,
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!"
            };
            var request2 = new RegisterRequest
            {
                Email = email,
                Password = "DifferentPass456!",
                ConfirmPassword = "DifferentPass456!"
            };

            // Act
            await _client.PostAsJsonAsync("/api/v1/auth/register", request1);
            var response2 = await _client.PostAsJsonAsync("/api/v1/auth/register", request2);
            var result2 = await response2.Content.ReadFromJsonAsync<RegisterResponse>();

            // Assert
            Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(result2, Is.Not.Null);
            Assert.That(result2!.Success, Is.False);
            Assert.That(result2.ErrorCode, Is.EqualTo("USER_ALREADY_EXISTS"));
        }

        #endregion

        #region User Login Tests

        [Test]
        public async Task Login_WithValidCredentials_ShouldSucceed()
        {
            // Arrange - Register user first
            var email = $"test-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            };
            await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Email, Is.EqualTo(email));
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty);
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RefreshToken, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Login_WithInvalidCredentials_ShouldFail()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Email = $"nonexistent-{Guid.NewGuid()}@example.com",
                Password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_CREDENTIALS"));
        }

        #endregion

        #region User Profile Tests

        [Test]
        public async Task GetProfile_WithValidToken_ShouldReturnUserInfo()
        {
            // Arrange - Register and login
            var email = $"test-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                FullName = "Test User"
            };
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

            // Add JWT token to request headers
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", registerResult!.AccessToken);

            // Act
            var response = await _client.GetAsync("/api/v1/auth/profile");
            var jsonResponse = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(jsonResponse, Does.Contain(email));
            Assert.That(jsonResponse, Does.Contain("Test User"));
            Assert.That(jsonResponse, Does.Contain("algorandAddress"));
        }

        [Test]
        public async Task GetProfile_WithoutToken_ShouldFail()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/profile");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        #endregion

        #region End-to-End Token Deployment Tests

        [Test]
        [Ignore("Requires Base testnet connection and gas - manual test only")]
        public async Task E2E_RegisterLoginAndDeployToken_WithJwtAuth_ShouldSucceed()
        {
            // Arrange - Register new user
            var email = $"test-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                FullName = "Token Creator"
            };
            
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(registerResult!.Success, Is.True, "Registration should succeed");

            // Add JWT token to client
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", registerResult.AccessToken);

            // Prepare token deployment request
            var deployRequest = new ERC20MintableTokenDeploymentRequest
            {
                Name = $"Test Token {Guid.NewGuid().ToString().Substring(0, 8)}",
                Symbol = "TEST",
                Decimals = 18,
                InitialSupply = 1000000,
                Cap = 10000000,
                ChainId = 84532, // Base Sepolia
                InitialSupplyReceiver = registerResult.AlgorandAddress // Use user's address
            };

            // Act - Deploy token
            var deployResponse = await _client.PostAsJsonAsync("/api/v1/token/erc20-mintable/create", deployRequest);
            var deployResult = await deployResponse.Content.ReadFromJsonAsync<ERC20TokenDeploymentResponse>();

            // Assert
            Assert.That(deployResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(deployResult, Is.Not.Null);
            Assert.That(deployResult!.Success, Is.True, $"Deployment should succeed. Error: {deployResult.ErrorMessage}");
            Assert.That(deployResult.TransactionHash, Is.Not.Null.And.Not.Empty);
            Assert.That(deployResult.ContractAddress, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task DeployToken_WithoutAuthentication_ShouldFail()
        {
            // Arrange - Don't set any authentication
            var deployRequest = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 18,
                InitialSupply = 1000000,
                Cap = 10000000,
                ChainId = 84532
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/erc20-mintable/create", deployRequest);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task DeployToken_WithExpiredToken_ShouldFail()
        {
            // Arrange - Create an expired token (manually craft a JWT that expired)
            // For this test, we'll just use an invalid token
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

            var deployRequest = new ERC20MintableTokenDeploymentRequest
            {
                Name = "Test Token",
                Symbol = "TEST",
                Decimals = 18,
                InitialSupply = 1000000,
                Cap = 10000000,
                ChainId = 84532
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/token/erc20-mintable/create", deployRequest);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        #endregion

        #region Token Refresh Tests

        [Test]
        public async Task RefreshToken_WithValidRefreshToken_ShouldReturnNewTokens()
        {
            // Arrange - Register user
            var email = $"test-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            };
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

            var refreshRequest = new RefreshTokenRequest
            {
                RefreshToken = registerResult!.RefreshToken
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);
            var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.AccessToken, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RefreshToken, Is.Not.Null.And.Not.Empty);
            Assert.That(result.RefreshToken, Is.Not.EqualTo(registerResult.RefreshToken), 
                "New refresh token should be different from old one");
        }

        [Test]
        public async Task RefreshToken_WithInvalidRefreshToken_ShouldFail()
        {
            // Arrange
            var refreshRequest = new RefreshTokenRequest
            {
                RefreshToken = "invalid-refresh-token"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);
            var result = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_REFRESH_TOKEN"));
        }

        #endregion

        #region Logout Tests

        [Test]
        public async Task Logout_WithValidToken_ShouldSucceed()
        {
            // Arrange - Register user
            var email = $"test-{Guid.NewGuid()}@example.com";
            var password = "SecurePass123!";
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password
            };
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();

            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", registerResult!.AccessToken);

            // Act
            var response = await _client.PostAsync("/api/v1/auth/logout", null);
            var result = await response.Content.ReadFromJsonAsync<LogoutResponse>();

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);

            // Verify old refresh token is revoked
            var refreshRequest = new RefreshTokenRequest
            {
                RefreshToken = registerResult.RefreshToken
            };
            var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);
            Assert.That(refreshResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), 
                "Refresh token should be revoked after logout");
        }

        #endregion
    }
}
