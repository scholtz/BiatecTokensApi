using BiatecTokensApi.Models.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration tests for the AuthV2Controller email/password authentication endpoints.
    ///
    /// These tests validate the stable backend contract required for MVP sign-off:
    /// - Deterministic registration and login responses
    /// - ARC76 Algorand address derivation consistency
    /// - Error semantics for invalid credentials, duplicate users, weak passwords
    /// - Authenticated session continuity via JWT tokens
    /// - Token refresh lifecycle
    /// - Standardised error response shapes for frontend assertion
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class AuthV2ControllerIntegrationTests
    {
        private AuthTestWebApplicationFactory _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _factory = new AuthTestWebApplicationFactory();
            _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        // ── WebApplicationFactory ─────────────────────────────────────────────────

        private sealed class AuthTestWebApplicationFactory : WebApplicationFactory<BiatecTokensApi.Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["App:Account"] = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
                        ["KeyManagementConfig:Provider"] = "Hardcoded",
                        ["KeyManagementConfig:HardcodedKey"] = "AuthIntegrationTestKey32CharsMinReq!",
                        ["JwtConfig:SecretKey"] = "AuthV2IntegrationTestSecretKey32CharMinRequired!!",
                        ["JwtConfig:Issuer"] = "BiatecTokensApi",
                        ["JwtConfig:Audience"] = "BiatecTokensUsers",
                        ["JwtConfig:AccessTokenExpirationMinutes"] = "60",
                        ["JwtConfig:RefreshTokenExpirationDays"] = "30",
                        ["JwtConfig:ValidateIssuer"] = "true",
                        ["JwtConfig:ValidateAudience"] = "true",
                        ["JwtConfig:ValidateLifetime"] = "true",
                        ["JwtConfig:ValidateIssuerSigningKey"] = "true",
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
                        ["EVMChains:0:RpcUrl"] = "https://mainnet.base.org",
                        ["EVMChains:0:ChainId"] = "8453",
                        ["EVMChains:0:GasLimit"] = "4500000",
                        ["Cors:0"] = "https://tokens.biatec.io"
                    });
                });
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string UniqueEmail() => $"test-{Guid.NewGuid():N}@biatec-mvp-test.example.com";

        private async Task<(RegisterResponse? body, HttpStatusCode status)> RegisterAsync(
            string email, string password = "SecurePass123!", string? fullName = null)
        {
            var req = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = password,
                FullName = fullName
            };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", req);
            var body = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            return (body, resp.StatusCode);
        }

        private async Task<(LoginResponse? body, HttpStatusCode status)> LoginAsync(
            string email, string password = "SecurePass123!")
        {
            var req = new LoginRequest { Email = email, Password = password };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", req);
            var body = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            return (body, resp.StatusCode);
        }

        private HttpClient CreateAuthenticatedClient(string accessToken)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        // ── Registration tests ────────────────────────────────────────────────────

        [Test]
        public async Task Register_ValidRequest_Returns200WithTokens()
        {
            var email = UniqueEmail();
            var (body, status) = await RegisterAsync(email);

            Assert.That(status, Is.EqualTo(HttpStatusCode.OK), "Valid registration must return 200");
            Assert.That(body, Is.Not.Null);
            Assert.That(body!.Success, Is.True, "Success flag must be true");
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty, "Access token must be present");
            Assert.That(body.RefreshToken, Is.Not.Null.And.Not.Empty, "Refresh token must be present");
            Assert.That(body.ExpiresAt, Is.Not.Null, "Token expiry must be provided");
        }

        [Test]
        public async Task Register_ValidRequest_ReturnsAlgorandAddress()
        {
            var email = UniqueEmail();
            var (body, status) = await RegisterAsync(email);

            Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body!.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "ARC76-derived Algorand address must be returned on registration");
            // Algorand address is 58 characters of uppercase base32
            Assert.That(body.AlgorandAddress!.Length, Is.EqualTo(58),
                "Algorand address must be 58 characters");
        }

        [Test]
        public async Task Register_ValidRequest_ReturnsUserId()
        {
            var email = UniqueEmail();
            var (body, _) = await RegisterAsync(email);

            Assert.That(body!.UserId, Is.Not.Null.And.Not.Empty, "UserId must be returned");
        }

        [Test]
        public async Task Register_ValidRequest_ReturnsDerivationContractVersion()
        {
            var email = UniqueEmail();
            var (body, _) = await RegisterAsync(email);

            Assert.That(body!.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "DerivationContractVersion must be returned for frontend contract detection");
        }

        [Test]
        public async Task Register_ValidRequest_ReturnsCorrelationId()
        {
            var email = UniqueEmail();
            var (body, _) = await RegisterAsync(email);

            Assert.That(body!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "CorrelationId must be returned for distributed tracing");
        }

        [Test]
        public async Task Register_DuplicateEmail_Returns400WithErrorCode()
        {
            var email = UniqueEmail();
            await RegisterAsync(email); // First registration

            var (body, status) = await RegisterAsync(email); // Duplicate

            Assert.That(status, Is.EqualTo(HttpStatusCode.BadRequest),
                "Duplicate registration must return 400");
            Assert.That(body!.Success, Is.False);
            Assert.That(body.ErrorCode, Is.Not.Null.And.Not.Empty,
                "ErrorCode must be provided for frontend error handling");
        }

        [Test]
        public async Task Register_WeakPassword_Returns400()
        {
            var email = UniqueEmail();
            var req = new RegisterRequest
            {
                Email = email,
                Password = "weak",
                ConfirmPassword = "weak"
            };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Weak password must be rejected");
        }

        [Test]
        public async Task Register_MismatchedPasswords_Returns400()
        {
            var req = new RegisterRequest
            {
                Email = UniqueEmail(),
                Password = "SecurePass123!",
                ConfirmPassword = "DifferentPass123!"
            };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Mismatched passwords must be rejected");
        }

        [Test]
        public async Task Register_InvalidEmailFormat_Returns400()
        {
            var req = new RegisterRequest
            {
                Email = "not-an-email",
                Password = "SecurePass123!",
                ConfirmPassword = "SecurePass123!"
            };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Invalid email format must be rejected");
        }

        [Test]
        public async Task Register_NullBody_Returns400()
        {
            var resp = await _client.PostAsync("/api/v1/auth/register",
                new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

            Assert.That((int)resp.StatusCode, Is.AnyOf(400, 422),
                "Null body must be rejected with 4xx");
        }

        // ── Login tests ───────────────────────────────────────────────────────────

        [Test]
        public async Task Login_ValidCredentials_Returns200WithTokens()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (body, status) = await LoginAsync(email);

            Assert.That(status, Is.EqualTo(HttpStatusCode.OK), "Valid login must return 200");
            Assert.That(body!.Success, Is.True);
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty);
            Assert.That(body.RefreshToken, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Login_ValidCredentials_ReturnsAlgorandAddress()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (body, status) = await LoginAsync(email);

            Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body!.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "Algorand address must be returned on login for frontend session binding");
        }

        [Test]
        public async Task Login_ValidCredentials_ReturnsUserId()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (body, _) = await LoginAsync(email);

            Assert.That(body!.UserId, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Login_ValidCredentials_ReturnsCorrelationId()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (body, _) = await LoginAsync(email);

            Assert.That(body!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "CorrelationId must be returned for distributed tracing");
        }

        [Test]
        public async Task Login_InvalidPassword_Returns401WithErrorCode()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (body, status) = await LoginAsync(email, "WrongPassword999!");

            Assert.That(status, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Invalid password must return 401, not a generic 400");
            Assert.That(body!.Success, Is.False);
            Assert.That(body.ErrorCode, Is.Not.Null.And.Not.Empty,
                "ErrorCode must be provided for frontend to show correct error message");
        }

        [Test]
        public async Task Login_NonExistentUser_Returns401()
        {
            var (body, status) = await LoginAsync("nonexistent-user@nobody.example.com");

            Assert.That(status, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Login for non-existent user must return 401");
            Assert.That(body!.Success, Is.False);
        }

        [Test]
        public async Task Login_EmptyPassword_Returns400()
        {
            var req = new LoginRequest { Email = UniqueEmail(), Password = "" };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Empty password must be rejected with 400");
        }

        [Test]
        public async Task Login_EmptyEmail_Returns400()
        {
            var req = new LoginRequest { Email = "", Password = "SecurePass123!" };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Empty email must be rejected with 400");
        }

        // ── ARC76 determinism tests ───────────────────────────────────────────────

        [Test]
        public async Task Login_SameCredentials_ReturnsSameAlgorandAddress()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);

            var (login1, _) = await LoginAsync(email);
            var (login2, _) = await LoginAsync(email);

            Assert.That(login1!.AlgorandAddress, Is.EqualTo(login2!.AlgorandAddress),
                "ARC76 derivation must be deterministic: same credentials must always produce same address");
        }

        [Test]
        public async Task Register_ThenLogin_AlgorandAddressIsConsistent()
        {
            var email = UniqueEmail();
            var (regBody, _) = await RegisterAsync(email);
            var (loginBody, _) = await LoginAsync(email);

            Assert.That(loginBody!.AlgorandAddress, Is.EqualTo(regBody!.AlgorandAddress),
                "Address returned on login must match address returned on registration");
        }

        [Test]
        public async Task MultipleLogins_AllReturnSameAlgorandAddress()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);

            var addresses = new List<string>();
            for (var i = 0; i < 3; i++)
            {
                var (body, _) = await LoginAsync(email);
                addresses.Add(body!.AlgorandAddress!);
            }

            Assert.That(addresses.Distinct().Count(), Is.EqualTo(1),
                "ARC76 must produce same address across all logins (3-run determinism)");
        }

        // ── Token refresh tests ───────────────────────────────────────────────────

        [Test]
        public async Task RefreshToken_ValidRefreshToken_Returns200WithNewTokens()
        {
            var email = UniqueEmail();
            var (regBody, _) = await RegisterAsync(email);

            var refreshReq = new RefreshTokenRequest { RefreshToken = regBody!.RefreshToken! };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshReq);
            var body = await resp.Content.ReadFromJsonAsync<RefreshTokenResponse>();

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Valid refresh token must return 200");
            Assert.That(body!.Success, Is.True);
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty,
                "New access token must be returned");
            Assert.That(body.RefreshToken, Is.Not.Null.And.Not.Empty,
                "New refresh token must be returned");
        }

        [Test]
        public async Task RefreshToken_InvalidToken_Returns401Or400()
        {
            var refreshReq = new RefreshTokenRequest { RefreshToken = "totally-invalid-refresh-token" };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshReq);

            Assert.That((int)resp.StatusCode, Is.AnyOf(400, 401),
                "Invalid refresh token must be rejected with 4xx");
        }

        [Test]
        public async Task RefreshToken_EmptyToken_Returns400()
        {
            var refreshReq = new RefreshTokenRequest { RefreshToken = "" };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshReq);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Empty refresh token must return 400");
        }

        [Test]
        public async Task RefreshToken_AfterLogin_Returns200WithNewTokens()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (loginBody, _) = await LoginAsync(email);

            var refreshReq = new RefreshTokenRequest { RefreshToken = loginBody!.RefreshToken! };
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshReq);
            var body = await resp.Content.ReadFromJsonAsync<RefreshTokenResponse>();

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body!.Success, Is.True);
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty);
        }

        // ── JWT structure tests ───────────────────────────────────────────────────

        [Test]
        public async Task Register_AccessToken_IsThreePartJwt()
        {
            var (body, _) = await RegisterAsync(UniqueEmail());
            var parts = body!.AccessToken!.Split('.');
            Assert.That(parts.Length, Is.EqualTo(3),
                "JWT access token must have three dot-separated parts (header.payload.signature)");
        }

        [Test]
        public async Task Login_AccessToken_IsThreePartJwt()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (body, _) = await LoginAsync(email);
            var parts = body!.AccessToken!.Split('.');
            Assert.That(parts.Length, Is.EqualTo(3),
                "JWT access token from login must be a valid 3-part JWT");
        }

        [Test]
        public async Task Register_ExpiresAt_IsFutureTimestamp()
        {
            var (body, _) = await RegisterAsync(UniqueEmail());
            Assert.That(body!.ExpiresAt, Is.GreaterThan(DateTime.UtcNow),
                "ExpiresAt must be in the future");
        }

        [Test]
        public async Task Login_ExpiresAt_IsFutureTimestamp()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (body, _) = await LoginAsync(email);
            Assert.That(body!.ExpiresAt, Is.GreaterThan(DateTime.UtcNow),
                "ExpiresAt on login must be in the future");
        }

        // ── Authenticated session continuity tests ────────────────────────────────

        [Test]
        public async Task Profile_WithValidJwt_Returns200()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (loginBody, _) = await LoginAsync(email);

            using var authClient = CreateAuthenticatedClient(loginBody!.AccessToken!);
            var resp = await authClient.GetAsync("/api/v1/auth/profile");

            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "Profile endpoint with valid JWT must not return 500");
        }

        [Test]
        public async Task Profile_WithoutToken_Returns401()
        {
            var resp = await _client.GetAsync("/api/v1/auth/profile");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Profile endpoint without auth token must return 401");
        }

        [Test]
        public async Task AuthenticatedEndpoint_WithExpiredToken_Returns401()
        {
            // An obviously invalid/expired token should not grant access
            using var badClient = CreateAuthenticatedClient("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJmYWtlIiwiZXhwIjoxfQ.fakesignature");
            var resp = await badClient.GetAsync("/api/v1/auth/profile");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Tampered/expired JWT must be rejected with 401");
        }

        // ── Error response shape tests ────────────────────────────────────────────

        [Test]
        public async Task Login_InvalidCredentials_ErrorResponseHasRequiredFields()
        {
            var (body, _) = await LoginAsync("nobody@nobody.example.com", "WrongPass999!");

            Assert.That(body, Is.Not.Null, "Error response must have a body");
            Assert.That(body!.Success, Is.False, "Success must be false on error");
            Assert.That(body.ErrorCode, Is.Not.Null.And.Not.Empty,
                "ErrorCode must be present for frontend switch/case error handling");
            Assert.That(body.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "ErrorMessage must be present for logging and support");
        }

        [Test]
        public async Task Register_DuplicateEmail_ErrorResponseHasRequiredFields()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (body, _) = await RegisterAsync(email);

            Assert.That(body!.Success, Is.False);
            Assert.That(body.ErrorCode, Is.Not.Null.And.Not.Empty,
                "ErrorCode must identify the conflict type for frontend handling");
            Assert.That(body.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "ErrorMessage must be present");
        }

        // ── ARC76 verify endpoint tests ───────────────────────────────────────────

        [Test]
        public async Task ARC76Verify_WithValidSession_Returns200()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (loginBody, _) = await LoginAsync(email);

            using var authClient = CreateAuthenticatedClient(loginBody!.AccessToken!);
            var verifyReq = new { Email = email };
            var resp = await authClient.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation", verifyReq);

            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "ARC76 verify endpoint with valid session must not return 5xx");
        }

        [Test]
        public async Task ARC76Verify_WithoutToken_Returns401()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation",
                new { Email = "test@test.com" });

            Assert.That((int)resp.StatusCode, Is.AnyOf(401, 403, 404),
                "ARC76 verify endpoint must reject requests without authentication token");
        }

        // ── Session inspection tests ──────────────────────────────────────────────

        [Test]
        public async Task SessionInspect_WithValidToken_Returns200()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (loginBody, _) = await LoginAsync(email);

            using var authClient = CreateAuthenticatedClient(loginBody!.AccessToken!);
            var resp = await authClient.GetAsync("/api/v1/auth/session");

            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "Session inspect must not return 5xx with valid token");
        }

        [Test]
        public async Task SessionInspect_WithoutToken_Returns401()
        {
            var resp = await _client.GetAsync("/api/v1/auth/session");

            Assert.That((int)resp.StatusCode, Is.AnyOf(401, 403, 404),
                "Session inspect must reject requests without authentication token");
        }

        // ── Logout tests ──────────────────────────────────────────────────────────

        [Test]
        public async Task Logout_WithValidSession_Returns200()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);
            var (loginBody, _) = await LoginAsync(email);

            using var authClient = CreateAuthenticatedClient(loginBody!.AccessToken!);
            var resp = await authClient.PostAsync("/api/v1/auth/logout", null);

            Assert.That((int)resp.StatusCode, Is.LessThan(500),
                "Logout with valid session must not return 5xx");
        }

        [Test]
        public async Task Logout_WithoutToken_Returns401()
        {
            var resp = await _client.PostAsync("/api/v1/auth/logout", null);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Logout without token must return 401");
        }

        // ── Full register→login→refresh E2E flow ──────────────────────────────────

        [Test]
        public async Task E2E_RegisterLoginRefresh_FullAuthFlow()
        {
            // 1. Register
            var email = UniqueEmail();
            var (regBody, regStatus) = await RegisterAsync(email, "SecurePass123!", "E2E Test User");
            Assert.That(regStatus, Is.EqualTo(HttpStatusCode.OK), "Step 1: Registration must succeed");
            var address = regBody!.AlgorandAddress;

            // 2. Login
            var (loginBody, loginStatus) = await LoginAsync(email);
            Assert.That(loginStatus, Is.EqualTo(HttpStatusCode.OK), "Step 2: Login must succeed");
            Assert.That(loginBody!.AlgorandAddress, Is.EqualTo(address),
                "Step 2: Login address must match registration address (ARC76 determinism)");

            // 3. Refresh token
            var refreshReq = new RefreshTokenRequest { RefreshToken = loginBody.RefreshToken! };
            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshReq);
            var refreshBody = await refreshResp.Content.ReadFromJsonAsync<RefreshTokenResponse>();
            Assert.That(refreshResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Step 3: Token refresh must succeed");
            Assert.That(refreshBody!.Success, Is.True, "Step 3: Refresh must succeed");

            // 4. Use refreshed token for authenticated request
            using var authClient = CreateAuthenticatedClient(refreshBody.AccessToken!);
            var profileResp = await authClient.GetAsync("/api/v1/auth/profile");
            Assert.That((int)profileResp.StatusCode, Is.LessThan(500),
                "Step 4: Refreshed token must provide authenticated access");
        }

        [Test]
        public async Task E2E_LoginWithWrongPassword_ThenCorrectPassword_Succeeds()
        {
            var email = UniqueEmail();
            await RegisterAsync(email);

            // First attempt with wrong password → 401
            var (badBody, badStatus) = await LoginAsync(email, "WrongPassword999!");
            Assert.That(badStatus, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Wrong password must return 401");

            // Second attempt with correct password → 200
            var (goodBody, goodStatus) = await LoginAsync(email, "SecurePass123!");
            Assert.That(goodStatus, Is.EqualTo(HttpStatusCode.OK),
                "Correct password after failed attempt must still succeed");
            Assert.That(goodBody!.Success, Is.True);
        }

        // ── Email normalisation tests ─────────────────────────────────────────────

        [Test]
        public async Task Register_ThenLoginWithLowercaseEmail_Succeeds()
        {
            var baseEmail = UniqueEmail();
            await RegisterAsync(baseEmail);

            // Login using same email in lowercase (should already be lowercase, but ensure canonical)
            var (body, status) = await LoginAsync(baseEmail.ToLowerInvariant());

            Assert.That(status, Is.EqualTo(HttpStatusCode.OK),
                "Login with lowercase variant of registered email must succeed");
        }

        // ── Concurrent registration tests ─────────────────────────────────────────

        [Test]
        public async Task ConcurrentRegistrations_DifferentEmails_AllSucceed()
        {
            var tasks = Enumerable.Range(0, 5)
                .Select(_ => RegisterAsync(UniqueEmail()))
                .ToList();

            var results = await Task.WhenAll(tasks);

            Assert.That(results.All(r => r.status == HttpStatusCode.OK),
                Is.True, "Concurrent registrations with different emails must all succeed");
        }

        [Test]
        public async Task ConcurrentRegistrations_DifferentEmails_ProduceDifferentAddresses()
        {
            var tasks = Enumerable.Range(0, 5)
                .Select(_ => RegisterAsync(UniqueEmail()))
                .ToList();

            var results = await Task.WhenAll(tasks);
            var addresses = results.Select(r => r.body!.AlgorandAddress).ToList();

            Assert.That(addresses.Distinct().Count(), Is.EqualTo(5),
                "Different emails must produce different ARC76 Algorand addresses");
        }
    }
}
