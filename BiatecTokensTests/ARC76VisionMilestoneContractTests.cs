using BiatecTokensApi.Models.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration/contract tests for the ARC76 Vision Milestone (Issue #458):
    /// Complete ARC76 Account Management and Backend-Verified Email/Password Authentication.
    ///
    /// Tests cover acceptance criteria:
    /// - POST /api/v1/auth/arc76/validate returns same address for same credentials (determinism)
    /// - POST /api/v1/auth/arc76/verify-session returns session-bound address
    /// - Full login flow: register → login → verify-session → address matches
    /// - Error handling: missing/invalid credentials return typed errors
    /// - Never returns private key material in any response
    ///
    /// Known Test Vector:
    ///   email = "testuser@biatec.io"
    ///   password = "TestPassword123!"
    ///   address = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI"
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76VisionMilestoneContractTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        // Known test vector
        private const string KnownEmail = "testuser@biatec.io";
        private const string KnownPassword = "TestPassword123!";
        private const string KnownAddress = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

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

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private async Task<RegisterResponse> RegisterUserAsync(string email, string password)
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email,
                password,
                confirmPassword = password
            });
            var result = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(result, Is.Not.Null, "RegisterResponse must not be null");
            return result!;
        }

        private async Task<LoginResponse> LoginUserAsync(string email, string password)
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            var result = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(result, Is.Not.Null, "LoginResponse must not be null");
            return result!;
        }

        private HttpClient CreateAuthenticatedClient(string accessToken)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC1: POST /auth/arc76/validate returns deterministic address
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC1_Validate_ReturnsExpectedAddressForKnownTestVector()
        {
            // Act
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate", new
            {
                email = KnownEmail,
                password = KnownPassword
            });

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Validate must return 200 for valid credentials");

            var result = await resp.Content.ReadFromJsonAsync<ARC76ValidateResponse>();
            Assert.That(result, Is.Not.Null, "Validate response must not be null");
            Assert.That(result!.Success, Is.True, "Validate must succeed");
            Assert.That(result.AlgorandAddress, Is.EqualTo(KnownAddress),
                $"Known test vector must produce expected address. Got: {result.AlgorandAddress}");
            Assert.That(result.PublicKeyBase64, Is.Not.Null.And.Not.Empty,
                "PublicKey must be returned");
        }

        [Test]
        public async Task AC1_Validate_Returns100IdenticalAddresses_ForSameCredentials()
        {
            // Arrange
            const int iterations = 100;
            var firstAddress = (string?)null;

            // Act + Assert
            for (int i = 0; i < iterations; i++)
            {
                var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate", new
                {
                    email = KnownEmail,
                    password = KnownPassword
                });
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                var result = await resp.Content.ReadFromJsonAsync<ARC76ValidateResponse>();
                Assert.That(result!.Success, Is.True, $"Iteration {i + 1} must succeed");

                if (firstAddress == null)
                    firstAddress = result.AlgorandAddress;
                else
                    Assert.That(result.AlgorandAddress, Is.EqualTo(firstAddress),
                        $"Iteration {i + 1}: address diverged from expected. AC1 determinism violated.");
            }
        }

        [Test]
        public async Task AC1_Validate_IsAnonymous_NoAuthRequired()
        {
            // Validate endpoint must work WITHOUT authentication header
            var anonClient = _factory.CreateClient(); // No auth header
            var resp = await anonClient.PostAsJsonAsync("/api/v1/auth/arc76/validate", new
            {
                email = KnownEmail,
                password = KnownPassword
            });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Validate must not require authentication");
        }

        [Test]
        public async Task AC1_Validate_EmptyEmail_Returns400()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate", new
            {
                email = "",
                password = KnownPassword
            });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400),
                "Empty email must return 400 Bad Request");
        }

        [Test]
        public async Task AC1_Validate_EmptyPassword_Returns400()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate", new
            {
                email = KnownEmail,
                password = ""
            });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400),
                "Empty password must return 400 Bad Request");
        }

        [Test]
        public async Task AC1_Validate_NullBody_Returns400()
        {
            var resp = await _client.PostAsJsonAsync<object?>("/api/v1/auth/arc76/validate", null);

            Assert.That((int)resp.StatusCode, Is.EqualTo(400).Or.EqualTo(415).Or.EqualTo(422),
                "Null body must return a 4xx error");
        }

        [Test]
        public async Task AC1_Validate_ResponseNeverContainsPrivateKey()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate", new
            {
                email = KnownEmail,
                password = KnownPassword
            });
            var body = await resp.Content.ReadAsStringAsync();

            // The 32-byte private key base64 for the known test vector
            const string knownPrivateKeyBase64 = "U23OZLAs/ZlYuxusrcx8QCk9ln0yp2OOTfqZ/sdj3bY=";
            Assert.That(body, Does.Not.Contain(knownPrivateKeyBase64),
                "Response body must never contain the private key");
            Assert.That(body, Does.Not.Contain("privateKey"),
                "Response body must not expose any field named 'privateKey'");
            Assert.That(body, Does.Not.Contain("mnemonic"),
                "Response body must not expose mnemonic");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC2: POST /auth/arc76/verify-session returns session-bound address
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC2_VerifySession_WithValidToken_ReturnsAlgorandAddress()
        {
            // Arrange - register and login to get access token
            var email = $"arc76-vs-{Guid.NewGuid()}@example.com";
            const string password = "AC2VSession@Pass1!";

            var registerResult = await RegisterUserAsync(email, password);
            var loginResult = await LoginUserAsync(email, password);
            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);

            // Act
            var resp = await authClient.PostAsync("/api/v1/auth/arc76/verify-session", null);

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "verify-session with valid token must return 200");

            var result = await resp.Content.ReadFromJsonAsync<ARC76VerifySessionResponse>();
            Assert.That(result, Is.Not.Null, "verify-session response must not be null");
            Assert.That(result!.Success, Is.True, "verify-session must succeed");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "verify-session must return the Algorand address");
            Assert.That(result.AlgorandAddress, Is.EqualTo(registerResult.AlgorandAddress),
                "verify-session must return the same address as registration");
        }

        [Test]
        public async Task AC2_VerifySession_WithoutToken_Returns401()
        {
            // No Authorization header
            var resp = await _client.PostAsync("/api/v1/auth/arc76/verify-session", null);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "verify-session without token must return 401");
        }

        [Test]
        public async Task AC2_VerifySession_WithInvalidToken_Returns401()
        {
            using var authClient = CreateAuthenticatedClient("invalid.token.value");
            var resp = await authClient.PostAsync("/api/v1/auth/arc76/verify-session", null);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "verify-session with invalid token must return 401");
        }

        [Test]
        public async Task AC2_VerifySession_AddressBindingIsDeterministic()
        {
            // Same credentials → same session-bound address across multiple logins
            var email = $"arc76-det-{Guid.NewGuid()}@example.com";
            const string password = "AC2Det@Pass123!";

            await RegisterUserAsync(email, password);

            // Session 1
            var login1 = await LoginUserAsync(email, password);
            using var client1 = CreateAuthenticatedClient(login1.AccessToken!);
            var resp1 = await client1.PostAsync("/api/v1/auth/arc76/verify-session", null);
            var result1 = await resp1.Content.ReadFromJsonAsync<ARC76VerifySessionResponse>();

            // Session 2
            var login2 = await LoginUserAsync(email, password);
            using var client2 = CreateAuthenticatedClient(login2.AccessToken!);
            var resp2 = await client2.PostAsync("/api/v1/auth/arc76/verify-session", null);
            var result2 = await resp2.Content.ReadFromJsonAsync<ARC76VerifySessionResponse>();

            // Assert both sessions return same address
            Assert.That(result1!.AlgorandAddress, Is.Not.Null.And.Not.Empty);
            Assert.That(result2!.AlgorandAddress, Is.EqualTo(result1.AlgorandAddress),
                "verify-session must return the same address across different login sessions");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC3: Registration uses ARC76 credential derivation (validate matches stored)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC3_Registration_UsesARC76Derivation_ValidateMatchesStoredAddress()
        {
            // Register a new user with known credentials
            var email = $"arc76-reg-{Guid.NewGuid()}@example.com";
            const string password = "AC3Register@1!";

            var registerResult = await RegisterUserAsync(email, password);
            Assert.That(registerResult.Success, Is.True, "Registration must succeed");
            Assert.That(registerResult.AlgorandAddress, Is.Not.Null.And.Not.Empty);

            // Get the validate-derived address
            var validateResp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate", new
            {
                email,
                password
            });
            var validateResult = await validateResp.Content.ReadFromJsonAsync<ARC76ValidateResponse>();

            // The derived address MUST match the stored address (since registration uses credential derivation)
            Assert.That(validateResult!.AlgorandAddress, Is.EqualTo(registerResult.AlgorandAddress),
                "ARC76 validate-derived address must match the address assigned at registration");
            Assert.That(validateResult.AddressMatchesStoredAccount, Is.True,
                "AddressMatchesStoredAccount must be true for a user registered with credential derivation");
        }

        [Test]
        public async Task AC3_Registration_DeterministicAddress_SameEmailPasswordAlwaysSameAddress()
        {
            // If we derive the address without registration, it should match what registration would produce
            var email = $"arc76-new-{Guid.NewGuid()}@example.com";
            const string password = "AC3Deriv@Test1!";

            // First, derive the expected address via validate (no registration needed)
            var prederiveResp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate", new { email, password });
            var prederiveResult = await prederiveResp.Content.ReadFromJsonAsync<ARC76ValidateResponse>();
            var expectedAddress = prederiveResult!.AlgorandAddress;

            // Now register the user
            var registerResult = await RegisterUserAsync(email, password);

            // The registration-assigned address must match the pre-derived address
            Assert.That(registerResult.AlgorandAddress, Is.EqualTo(expectedAddress),
                "Registration must use ARC76 credential derivation — same as validate endpoint produces");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC4: Full integration flow
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC4_FullFlow_Register_Login_Validate_VerifySession_AllReturnSameAddress()
        {
            // Arrange
            var email = $"arc76-full-{Guid.NewGuid()}@example.com";
            const string password = "AC4Full@Flow1!";

            // Step 1: Register
            var registerResult = await RegisterUserAsync(email, password);
            Assert.That(registerResult.Success, Is.True, "Step 1: Registration must succeed");
            var registeredAddress = registerResult.AlgorandAddress!;

            // Step 2: Login
            var loginResult = await LoginUserAsync(email, password);
            Assert.That(loginResult.Success, Is.True, "Step 2: Login must succeed");
            Assert.That(loginResult.AlgorandAddress, Is.EqualTo(registeredAddress),
                "Step 2: Login must return same address as registration");

            // Step 3: Validate
            var validateResp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate", new { email, password });
            var validateResult = await validateResp.Content.ReadFromJsonAsync<ARC76ValidateResponse>();
            Assert.That(validateResult!.AlgorandAddress, Is.EqualTo(registeredAddress),
                "Step 3: Validate must return same address");

            // Step 4: Verify-session
            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);
            var verifyResp = await authClient.PostAsync("/api/v1/auth/arc76/verify-session", null);
            var verifyResult = await verifyResp.Content.ReadFromJsonAsync<ARC76VerifySessionResponse>();
            Assert.That(verifyResult!.AlgorandAddress, Is.EqualTo(registeredAddress),
                "Step 4: verify-session must return same address");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC5: Schema contract assertions
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC5_Validate_ResponseSchema_HasRequiredFields()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate", new
            {
                email = KnownEmail,
                password = KnownPassword
            });
            var json = await resp.Content.ReadAsStringAsync();

            // Required schema fields
            Assert.That(json, Does.Contain("\"success\""), "Response must contain 'success' field");
            Assert.That(json, Does.Contain("\"algorandAddress\""), "Response must contain 'algorandAddress' field");
            Assert.That(json, Does.Contain("\"publicKeyBase64\""), "Response must contain 'publicKeyBase64' field");
        }

        [Test]
        public async Task AC5_VerifySession_ResponseSchema_HasRequiredFields()
        {
            var email = $"arc76-schema-{Guid.NewGuid()}@example.com";
            const string password = "AC5Schema@Pass1!";
            await RegisterUserAsync(email, password);
            var loginResult = await LoginUserAsync(email, password);

            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);
            var resp = await authClient.PostAsync("/api/v1/auth/arc76/verify-session", null);
            var json = await resp.Content.ReadAsStringAsync();

            Assert.That(json, Does.Contain("\"success\""), "Response must contain 'success' field");
            Assert.That(json, Does.Contain("\"algorandAddress\""), "Response must contain 'algorandAddress' field");
            Assert.That(json, Does.Contain("\"userId\""), "Response must contain 'userId' field");
        }

        [Test]
        public async Task AC5_Validate_DifferentUsers_DifferentAddresses()
        {
            // Two different users must have different derived addresses
            var resp1 = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate", new
            {
                email = "user1@example.com",
                password = "SamePassword123!"
            });
            var result1 = await resp1.Content.ReadFromJsonAsync<ARC76ValidateResponse>();

            var resp2 = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate", new
            {
                email = "user2@example.com",
                password = "SamePassword123!"
            });
            var result2 = await resp2.Content.ReadFromJsonAsync<ARC76ValidateResponse>();

            Assert.That(result1!.AlgorandAddress, Is.Not.EqualTo(result2!.AlgorandAddress),
                "Different email addresses with same password must produce different Algorand addresses");
        }
    }
}
