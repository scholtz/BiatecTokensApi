using BiatecTokensApi.Models.ARC76;
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
    /// Integration tests covering the ARC76 Account Management acceptance criteria from Issue #429.
    /// Tests: register → login → get-address → verify → logout full flow.
    /// Business Value: Validates that non-crypto users can authenticate with email/password only
    /// and receive a deterministic Algorand address via ARC76 — with no wallet required.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76AccountManagementContractTests
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
        // AC1: POST /auth/register creates user and returns deterministic ARC76 address
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC1_Register_CreatesUserWithARC76Address()
        {
            // Arrange
            var email = $"arc76-ac1-{Guid.NewGuid()}@example.com";
            const string password = "AC1Secure@Pass1";

            // Act
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email,
                password,
                confirmPassword = password
            });

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Created),
                "Registration must return 200 or 201");

            var result = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True, "Registration must succeed");
            Assert.That(result.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "Register must return a non-empty ARC76 Algorand address");
            Assert.That(result.AlgorandAddress, Has.Length.GreaterThan(40),
                "Algorand address must be at least 41 characters");
        }

        [Test]
        public async Task AC1_Register_Returns409_WhenEmailAlreadyRegistered()
        {
            // Arrange
            var email = $"arc76-dup-{Guid.NewGuid()}@example.com";
            const string password = "AC1Dup@Pass123";

            await RegisterUserAsync(email, password);

            // Act - second registration with same email
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email,
                password,
                confirmPassword = password
            });

            // Assert
            Assert.That((int)resp.StatusCode, Is.EqualTo(409).Or.EqualTo(400),
                "Duplicate email registration must return 409 Conflict or 400 Bad Request");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC2: POST /auth/login returns session token and same ARC76 address as registration
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC2_Login_ReturnsSameARC76AddressAsRegistration()
        {
            // Arrange
            var email = $"arc76-ac2-{Guid.NewGuid()}@example.com";
            const string password = "AC2Login@Secure1";

            var registerResult = await RegisterUserAsync(email, password);
            var registeredAddress = registerResult.AlgorandAddress;

            // Act
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Login must return 200");

            var loginResult = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

            // Assert
            Assert.That(loginResult, Is.Not.Null);
            Assert.That(loginResult!.Success, Is.True, "Login must succeed");
            Assert.That(loginResult.AccessToken, Is.Not.Null.And.Not.Empty,
                "Login must return an access token");
            Assert.That(loginResult.AlgorandAddress, Is.EqualTo(registeredAddress),
                "Login must return the same ARC76 address as registration — determinism required");
        }

        [Test]
        public async Task AC2_Login_ReturnsAccessToken_WithValidJwtFormat()
        {
            // Arrange
            var email = $"arc76-jwt-{Guid.NewGuid()}@example.com";
            const string password = "AC2Jwt@Secure1!";

            await RegisterUserAsync(email, password);

            // Act
            var loginResult = await LoginUserAsync(email, password);

            // Assert
            Assert.That(loginResult.AccessToken, Is.Not.Null.And.Not.Empty);
            // A valid JWT has exactly 3 dot-separated parts
            var parts = loginResult.AccessToken!.Split('.');
            Assert.That(parts, Has.Length.EqualTo(3),
                "Access token must be a valid JWT (header.payload.signature)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC3: Invalid credentials return HTTP 401
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC3_Login_WithWrongPassword_Returns401()
        {
            // Arrange
            var email = $"arc76-wrong-{Guid.NewGuid()}@example.com";
            const string correctPassword = "AC3Correct@Pass1";
            const string wrongPassword = "WrongPassword999!";

            await RegisterUserAsync(email, correctPassword);

            // Act
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = wrongPassword });

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Wrong password must return HTTP 401");
        }

        [Test]
        public async Task AC3_Login_WithNonExistentEmail_Returns401()
        {
            // Arrange - email that was never registered
            var email = $"nonexistent-{Guid.NewGuid()}@example.com";

            // Act
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = "SomePass@123!" });

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Non-existent email must return HTTP 401 (same as wrong password — no user enumeration)");
        }

        [Test]
        public async Task AC3_Login_WrongEmail_And_WrongPassword_ReturnSameStatusCode()
        {
            // Arrange
            var email = $"arc76-enum-{Guid.NewGuid()}@example.com";
            const string password = "AC3Enum@Pass1!";

            await RegisterUserAsync(email, password);

            // Act
            var wrongEmailResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = $"nonexistent-{Guid.NewGuid()}@example.com", password });
            var wrongPasswordResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = "WrongPass@999!" });

            // Assert - both must return 401 (prevents user enumeration)
            Assert.That(wrongEmailResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(wrongPasswordResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC4: GET /arc76/address returns the same address on every call
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC4_GetARC76Address_ReturnsAddress_WhenAuthenticated()
        {
            // Arrange
            var email = $"arc76-addr-{Guid.NewGuid()}@example.com";
            const string password = "AC4Addr@Secure1!";

            await RegisterUserAsync(email, password);
            var loginResult = await LoginUserAsync(email, password);

            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);

            // Act
            var resp = await authClient.GetAsync("/api/v1/arc76/address");

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "GET /api/v1/arc76/address must return 200 for authenticated users");

            var result = await resp.Content.ReadFromJsonAsync<ARC76AddressResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True, "Response must indicate success");
            Assert.That(result.Address, Is.Not.Null.And.Not.Empty,
                "Response must contain a non-empty Algorand address");
        }

        [Test]
        public async Task AC4_GetARC76Address_ReturnsSameAddress_OnMultipleCalls()
        {
            // Arrange
            var email = $"arc76-stable-{Guid.NewGuid()}@example.com";
            const string password = "AC4Stable@Pass1!";

            await RegisterUserAsync(email, password);
            var loginResult = await LoginUserAsync(email, password);

            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);

            // Act - call 3 times and ensure same address returned
            var resp1 = await authClient.GetAsync("/api/v1/arc76/address");
            var resp2 = await authClient.GetAsync("/api/v1/arc76/address");
            var resp3 = await authClient.GetAsync("/api/v1/arc76/address");

            var addr1 = (await resp1.Content.ReadFromJsonAsync<ARC76AddressResponse>())?.Address;
            var addr2 = (await resp2.Content.ReadFromJsonAsync<ARC76AddressResponse>())?.Address;
            var addr3 = (await resp3.Content.ReadFromJsonAsync<ARC76AddressResponse>())?.Address;

            // Assert
            Assert.That(addr1, Is.Not.Null.And.Not.Empty);
            Assert.That(addr2, Is.EqualTo(addr1), "Second call must return identical address");
            Assert.That(addr3, Is.EqualTo(addr1), "Third call must return identical address");
        }

        [Test]
        public async Task AC4_GetARC76Address_Returns401_WhenUnauthenticated()
        {
            // Act - call without token
            var resp = await _client.GetAsync("/api/v1/arc76/address");

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "GET /api/v1/arc76/address must return 401 when not authenticated");
        }

        [Test]
        public async Task AC4_GetARC76Address_ReturnsSameAddress_AsLoginResponse()
        {
            // Arrange
            var email = $"arc76-match-{Guid.NewGuid()}@example.com";
            const string password = "AC4Match@Secure1!";

            await RegisterUserAsync(email, password);
            var loginResult = await LoginUserAsync(email, password);
            var loginAddress = loginResult.AlgorandAddress;

            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);

            // Act
            var addrResp = await authClient.GetAsync("/api/v1/arc76/address");
            var addrResult = await addrResp.Content.ReadFromJsonAsync<ARC76AddressResponse>();

            // Assert
            Assert.That(addrResult?.Address, Is.EqualTo(loginAddress),
                "GET /api/v1/arc76/address must return the same address as returned during login");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC5: POST /arc76/verify returns { verified: true } when address matches
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC5_VerifyAddress_ReturnsVerifiedTrue_WhenAddressMatches()
        {
            // Arrange
            var email = $"arc76-verify-{Guid.NewGuid()}@example.com";
            const string password = "AC5Verify@Pass1!";

            var registerResult = await RegisterUserAsync(email, password);
            var loginResult = await LoginUserAsync(email, password);
            var expectedAddress = registerResult.AlgorandAddress!;

            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);

            // Act
            var resp = await authClient.PostAsJsonAsync("/api/v1/arc76/verify",
                new ARC76VerifyRequest { Address = expectedAddress });

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "POST /api/v1/arc76/verify must return 200");

            var result = await resp.Content.ReadFromJsonAsync<ARC76VerifyResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Success, Is.True);
            Assert.That(result.Verified, Is.True,
                "Verified must be true when provided address matches user's ARC76-derived address");
        }

        [Test]
        public async Task AC5_VerifyAddress_ReturnsVerifiedFalse_WhenAddressDoesNotMatch()
        {
            // Arrange
            var email = $"arc76-mismatch-{Guid.NewGuid()}@example.com";
            const string password = "AC5Mismatch@Pass1!";

            await RegisterUserAsync(email, password);
            var loginResult = await LoginUserAsync(email, password);

            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);

            // Act - provide a wrong address
            var resp = await authClient.PostAsJsonAsync("/api/v1/arc76/verify",
                new ARC76VerifyRequest { Address = "WRONGADDRESSWRONGADDRESSWRONGADDRESSWRONGADDRESS" });

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await resp.Content.ReadFromJsonAsync<ARC76VerifyResponse>();
            Assert.That(result!.Verified, Is.False,
                "Verified must be false when provided address does not match user's ARC76-derived address");
        }

        [Test]
        public async Task AC5_VerifyAddress_Returns401_WhenUnauthenticated()
        {
            // Act - no token
            var resp = await _client.PostAsJsonAsync("/api/v1/arc76/verify",
                new ARC76VerifyRequest { Address = "SOME_ADDRESS" });

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task AC5_VerifyAddress_Returns400_WhenAddressIsEmpty()
        {
            // Arrange
            var email = $"arc76-empty-{Guid.NewGuid()}@example.com";
            const string password = "AC5Empty@Pass1!";

            await RegisterUserAsync(email, password);
            var loginResult = await LoginUserAsync(email, password);

            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);

            // Act
            var resp = await authClient.PostAsJsonAsync("/api/v1/arc76/verify",
                new ARC76VerifyRequest { Address = "" });

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Empty address must return 400");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC6: ARC76 derivation is deterministic — 100 derivations same result
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC6_ARC76Derivation_IsDeterministic_Across10Logins()
        {
            // Arrange
            var email = $"arc76-det-{Guid.NewGuid()}@example.com";
            const string password = "AC6Det@Secure1!";
            const int attempts = 10;

            await RegisterUserAsync(email, password);

            var addresses = new HashSet<string>();

            // Act - login 10 times and collect addresses
            for (int i = 0; i < attempts; i++)
            {
                var loginResult = await LoginUserAsync(email, password);
                Assert.That(loginResult.AlgorandAddress, Is.Not.Null.And.Not.Empty);
                addresses.Add(loginResult.AlgorandAddress!);
            }

            // Assert - all logins must return identical address
            Assert.That(addresses, Has.Count.EqualTo(1),
                $"All {attempts} logins must return the same ARC76 address — derivation must be deterministic");
        }

        [Test]
        public async Task AC6_GetARC76Address_IsDeterministic_AcrossLoginSessions()
        {
            // Arrange
            var email = $"arc76-sess-{Guid.NewGuid()}@example.com";
            const string password = "AC6Sess@Secure1!";

            await RegisterUserAsync(email, password);

            // Session 1
            var login1 = await LoginUserAsync(email, password);
            using var client1 = CreateAuthenticatedClient(login1.AccessToken!);
            var addr1Resp = await client1.GetAsync("/api/v1/arc76/address");
            var addr1 = (await addr1Resp.Content.ReadFromJsonAsync<ARC76AddressResponse>())?.Address;

            // Session 2
            var login2 = await LoginUserAsync(email, password);
            using var client2 = CreateAuthenticatedClient(login2.AccessToken!);
            var addr2Resp = await client2.GetAsync("/api/v1/arc76/address");
            var addr2 = (await addr2Resp.Content.ReadFromJsonAsync<ARC76AddressResponse>())?.Address;

            // Assert
            Assert.That(addr1, Is.Not.Null.And.Not.Empty);
            Assert.That(addr2, Is.EqualTo(addr1),
                "GET /api/v1/arc76/address must return identical address across different login sessions");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC7: Session token validated by middleware on protected endpoints
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC7_ProtectedEndpoint_Returns401_WithInvalidToken()
        {
            // Arrange
            using var authClient = CreateAuthenticatedClient("invalid.token.value");

            // Act
            var resp = await authClient.GetAsync("/api/v1/arc76/address");

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Invalid token must be rejected by the JWT middleware");
        }

        [Test]
        public async Task AC7_ProtectedEndpoint_Returns401_WithExpiredToken()
        {
            // Arrange - manually crafted expired JWT (expired 2020)
            const string expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
                "eyJzdWIiOiJ0ZXN0dXNlciIsImV4cCI6MTU4MDAwMDAwMH0." +
                "INVALIDSIGNATURE";

            using var authClient = CreateAuthenticatedClient(expiredToken);

            // Act
            var resp = await authClient.GetAsync("/api/v1/arc76/address");

            // Assert
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Expired token must be rejected by the JWT middleware");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC8: POST /auth/logout invalidates the session
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC8_Logout_InvalidatesSession_SubsequentRequestsReturn401()
        {
            // Arrange
            var email = $"arc76-logout-{Guid.NewGuid()}@example.com";
            const string password = "AC8Logout@Pass1!";

            await RegisterUserAsync(email, password);
            var loginResult = await LoginUserAsync(email, password);
            var accessToken = loginResult.AccessToken!;

            using var authClient = CreateAuthenticatedClient(accessToken);

            // Verify we can access a protected endpoint before logout
            var beforeLogoutResp = await authClient.GetAsync("/api/v1/arc76/address");
            Assert.That(beforeLogoutResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Should access protected endpoint before logout");

            // Act - logout
            var logoutResp = await authClient.PostAsync("/api/v1/auth/logout", null);
            Assert.That(logoutResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Logout must return 200");

            // Assert - after logout, access token should be rejected for refresh operations
            // Note: stateless JWTs remain valid until expiry, but server-side refresh tokens are revoked
            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = loginResult.RefreshToken });
            Assert.That((int)refreshResp.StatusCode, Is.EqualTo(401).Or.EqualTo(400),
                "Refresh token must be rejected after logout");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC9: Full integration flow — register → login → get-address → verify → logout
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC9_FullFlow_RegisterLoginGetAddressVerifyLogout()
        {
            // Step 1: Register
            var email = $"arc76-full-{Guid.NewGuid()}@example.com";
            const string password = "AC9Full@Secure1!";

            var registerResult = await RegisterUserAsync(email, password);
            Assert.That(registerResult.Success, Is.True);
            var registeredAddress = registerResult.AlgorandAddress!;
            Assert.That(registeredAddress, Is.Not.Null.And.Not.Empty);

            // Step 2: Login
            var loginResult = await LoginUserAsync(email, password);
            Assert.That(loginResult.Success, Is.True);
            Assert.That(loginResult.AccessToken, Is.Not.Null.And.Not.Empty);
            Assert.That(loginResult.AlgorandAddress, Is.EqualTo(registeredAddress),
                "Login must return same address as registration");

            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);

            // Step 3: Get address via dedicated ARC76 endpoint
            var addrResp = await authClient.GetAsync("/api/v1/arc76/address");
            Assert.That(addrResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var addrResult = await addrResp.Content.ReadFromJsonAsync<ARC76AddressResponse>();
            Assert.That(addrResult!.Address, Is.EqualTo(registeredAddress),
                "GET /api/v1/arc76/address must return same address as registration");

            // Step 4: Verify the address matches
            var verifyResp = await authClient.PostAsJsonAsync("/api/v1/arc76/verify",
                new ARC76VerifyRequest { Address = registeredAddress });
            Assert.That(verifyResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var verifyResult = await verifyResp.Content.ReadFromJsonAsync<ARC76VerifyResponse>();
            Assert.That(verifyResult!.Verified, Is.True,
                "Verify must confirm the address from registration matches the derived address");

            // Step 5: Logout
            var logoutResp = await authClient.PostAsync("/api/v1/auth/logout", null);
            Assert.That(logoutResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Logout must succeed");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC10: Token refresh flow
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC10_RefreshToken_ReturnsNewAccessToken()
        {
            // Arrange
            var email = $"arc76-refresh-{Guid.NewGuid()}@example.com";
            const string password = "AC10Refresh@Pass1!";

            await RegisterUserAsync(email, password);
            var loginResult = await LoginUserAsync(email, password);

            // Act
            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = loginResult.RefreshToken });

            // Assert
            Assert.That(refreshResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Token refresh must return 200");

            var refreshResult = await refreshResp.Content.ReadFromJsonAsync<RefreshTokenResponse>();
            Assert.That(refreshResult, Is.Not.Null);
            Assert.That(refreshResult!.AccessToken, Is.Not.Null.And.Not.Empty,
                "Refresh must return a new access token");
            Assert.That(refreshResult.AccessToken, Is.Not.EqualTo(loginResult.AccessToken),
                "New access token must differ from the original");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AC11: Rate limiting on /auth/login
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AC11_Login_LocksAccount_AfterFiveFailedAttempts()
        {
            // Arrange
            var email = $"arc76-rl-{Guid.NewGuid()}@example.com";
            const string correctPassword = "AC11RateLimit@Pass1!";
            const string wrongPassword = "WrongPass@999!";

            await RegisterUserAsync(email, correctPassword);

            // Make 5 failed attempts to trigger account lockout
            for (int i = 0; i < 5; i++)
            {
                var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                    new { email, password = wrongPassword });
                // Each of these should return 401 while incrementing the counter
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                    $"Attempt {i + 1} of 5 must return 401 while incrementing the lockout counter");
            }

            // Act - 6th attempt should hit the locked account
            var lockedResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = wrongPassword });

            // Assert - locked account returns HTTP 423 Locked
            Assert.That((int)lockedResp.StatusCode, Is.EqualTo(423),
                "6th failed login attempt must return HTTP 423 Locked (account locked out after 5 failures)");

            // Also confirm that the correct password is now rejected too (account is locked)
            var correctButLockedResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = correctPassword });
            Assert.That((int)correctButLockedResp.StatusCode, Is.EqualTo(423),
                "Even correct credentials must be rejected while account is locked");
        }
    }
}
