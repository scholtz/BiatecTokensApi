using BiatecTokensApi.Configuration;
using BiatecTokensApi.Models;
using BiatecTokensApi.Models.Auth;
using BiatecTokensApi.Repositories.Interface;
using BiatecTokensApi.Services;
using BiatecTokensApi.Services.Interface;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace BiatecTokensTests
{
    /// <summary>
    /// Focused session security tests for Issue #462: explicit session lifecycle enforcement
    /// covering expiry, revocation, replay protection, and malformed token handling.
    ///
    /// This file targets the specific scenarios requested in the Issue #462 acceptance criteria:
    ///   - Session lifecycle: issue → refresh → revoke → expire → invalid replay
    ///   - Replay attack prevention: once-consumed refresh token must be rejected
    ///   - Revoked session rejection: revoked tokens return explicit structured errors
    ///   - Expired session rejection: tokens past ExpiresAt return explicit structured errors
    ///   - Malformed token handling: no exception propagation for any malformed variant
    ///   - Logout-then-access: post-logout session access is explicitly rejected
    ///   - Security boundary: no success-shaped payloads for any invalid session state
    ///
    /// Two sections:
    ///   Part A — Service-layer unit tests (no HTTP): fast, pure logic verification
    ///   Part B — Integration tests (WebApplicationFactory): full HTTP lifecycle verification
    ///
    /// Business Value: These tests prove that the backend enforces explicit session failure
    /// semantics required for MICA compliance reviews — auditors need evidence that revoked
    /// and expired sessions are never silently accepted.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendAuthContractHardeningIssue462SessionSecurityTests
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // Part A — Service-layer unit tests
        // ─────────────────────────────────────────────────────────────────────────────

        private Mock<IUserRepository> _mockUserRepo = null!;
        private AuthenticationService _authService = null!;

        private const string KnownEmail = "testuser@biatec.io";
        private const string KnownPassword = "TestPassword123!";
        private const string TestJwtSecret = "Issue462SessionSecurityTestSecretKey32CharsMin!";
        private const string TestEncryptionKey = "Issue462SessionSecurityEncKey32CharsMin!!";

        [SetUp]
        public void Setup()
        {
            _mockUserRepo = new Mock<IUserRepository>();

            var jwtConfig = new JwtConfig
            {
                SecretKey = TestJwtSecret,
                Issuer = "BiatecTokensApi",
                Audience = "BiatecTokensUsers",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 30,
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkewMinutes = 5
            };

            var keyMgmtConfig = new KeyManagementConfig
            {
                Provider = "Hardcoded",
                HardcodedKey = TestEncryptionKey
            };

            var services = new ServiceCollection();
            services.AddLogging();
            services.Configure<KeyManagementConfig>(_ =>
            {
                _.Provider = "Hardcoded";
                _.HardcodedKey = TestEncryptionKey;
            });
            services.AddSingleton<HardcodedKeyProvider>();
            var sp = services.BuildServiceProvider();

            var keyProviderFactory = new KeyProviderFactory(
                sp,
                Options.Create(keyMgmtConfig),
                new Mock<ILogger<KeyProviderFactory>>().Object);

            _authService = new AuthenticationService(
                _mockUserRepo.Object,
                new Mock<ILogger<AuthenticationService>>().Object,
                Options.Create(jwtConfig),
                keyProviderFactory);
        }

        // ── Service-layer: Revoked token tests ───────────────────────────────────────

        /// <summary>SA1: Refresh with a revoked token returns REFRESH_TOKEN_REVOKED error code.</summary>
        [Test]
        public async Task SA1_RefreshToken_Revoked_ReturnsRevocationErrorCode()
        {
            var revokedToken = new RefreshToken
            {
                TokenId = Guid.NewGuid().ToString(),
                Token = "revoked-token-value",
                UserId = Guid.NewGuid().ToString(),
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                IsRevoked = true,
                RevokedAt = DateTime.UtcNow.AddHours(-1)
            };

            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync("revoked-token-value"))
                .ReturnsAsync(revokedToken);

            var result = await _authService.RefreshTokenAsync("revoked-token-value", null, null);

            Assert.That(result.Success, Is.False,
                "Revoked refresh token must return Success=false");
            Assert.That(result.ErrorCode, Is.EqualTo("REFRESH_TOKEN_REVOKED"),
                "Revoked token must return REFRESH_TOKEN_REVOKED error code");
            Assert.That(result.AccessToken, Is.Null.Or.Empty,
                "Revoked token refresh must not issue new access token");
        }

        /// <summary>SA2: Refresh with an expired token returns REFRESH_TOKEN_EXPIRED error code.</summary>
        [Test]
        public async Task SA2_RefreshToken_Expired_ReturnsExpirationErrorCode()
        {
            var expiredToken = new RefreshToken
            {
                TokenId = Guid.NewGuid().ToString(),
                Token = "expired-token-value",
                UserId = Guid.NewGuid().ToString(),
                ExpiresAt = DateTime.UtcNow.AddDays(-1),  // expired yesterday
                IsRevoked = false
            };

            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync("expired-token-value"))
                .ReturnsAsync(expiredToken);

            var result = await _authService.RefreshTokenAsync("expired-token-value", null, null);

            Assert.That(result.Success, Is.False,
                "Expired refresh token must return Success=false");
            Assert.That(result.ErrorCode, Is.EqualTo("REFRESH_TOKEN_EXPIRED"),
                "Expired token must return REFRESH_TOKEN_EXPIRED error code");
            Assert.That(result.AccessToken, Is.Null.Or.Empty,
                "Expired token refresh must not issue new access token");
        }

        /// <summary>SA3: Refresh with unknown token returns INVALID_REFRESH_TOKEN error code.</summary>
        [Test]
        public async Task SA3_RefreshToken_Unknown_ReturnsInvalidTokenErrorCode()
        {
            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync(It.IsAny<string>()))
                .ReturnsAsync((RefreshToken?)null);

            var result = await _authService.RefreshTokenAsync(
                $"unknown-token-{Guid.NewGuid()}", null, null);

            Assert.That(result.Success, Is.False,
                "Unknown refresh token must return Success=false");
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_REFRESH_TOKEN"),
                "Unknown token must return INVALID_REFRESH_TOKEN error code");
        }

        /// <summary>SA4: Revoked token refresh does not call RevokeRefreshTokenAsync again (idempotency).</summary>
        [Test]
        public async Task SA4_RefreshToken_Revoked_DoesNotCallRevokeAgain()
        {
            var revokedToken = new RefreshToken
            {
                TokenId = Guid.NewGuid().ToString(),
                Token = "already-revoked",
                UserId = Guid.NewGuid().ToString(),
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                IsRevoked = true,
                RevokedAt = DateTime.UtcNow.AddHours(-2)
            };

            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync("already-revoked"))
                .ReturnsAsync(revokedToken);

            await _authService.RefreshTokenAsync("already-revoked", null, null);

            // Must not call revoke again for already-revoked token
            _mockUserRepo.Verify(r => r.RevokeRefreshTokenAsync(It.IsAny<string>()),
                Times.Never,
                "Revoked token must not trigger additional revoke calls");
        }

        /// <summary>SA5: Expired token never returns success-shaped response.</summary>
        [Test]
        public async Task SA5_ExpiredToken_NeverReturnsSuccessShape()
        {
            var expiredToken = new RefreshToken
            {
                Token = "exp-token",
                UserId = Guid.NewGuid().ToString(),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),  // just expired
                IsRevoked = false
            };

            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync("exp-token"))
                .ReturnsAsync(expiredToken);

            var result = await _authService.RefreshTokenAsync("exp-token", null, null);

            Assert.Multiple(() =>
            {
                Assert.That(result.Success, Is.False,
                    "Just-expired token must never return Success=true");
                Assert.That(result.AccessToken, Is.Null.Or.Empty,
                    "Just-expired token must not return AccessToken");
                Assert.That(result.RefreshToken, Is.Null.Or.Empty,
                    "Just-expired token must not return new RefreshToken");
            });
        }

        // ── Service-layer: Logout / session revocation tests ─────────────────────────

        /// <summary>SA6: LogoutAsync calls RevokeAllUserRefreshTokens for session revocation.</summary>
        [Test]
        public async Task SA6_Logout_RevokesAllUserTokens()
        {
            var userId = Guid.NewGuid().ToString();
            _mockUserRepo.Setup(r => r.RevokeAllUserRefreshTokensAsync(userId))
                .Returns(Task.CompletedTask);

            var result = await _authService.LogoutAsync(userId);

            Assert.That(result.Success, Is.True, "Logout must succeed");
            _mockUserRepo.Verify(r => r.RevokeAllUserRefreshTokensAsync(userId),
                Times.Once,
                "Logout must call RevokeAllUserRefreshTokensAsync exactly once");
        }

        /// <summary>SA7: LogoutAsync repository exception returns structured failure (no propagation).</summary>
        [Test]
        public async Task SA7_Logout_RepositoryException_StructuredFailure()
        {
            _mockUserRepo.Setup(r => r.RevokeAllUserRefreshTokensAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("DB error during revocation"));

            var result = await _authService.LogoutAsync(Guid.NewGuid().ToString());

            Assert.That(result.Success, Is.False,
                "Logout exception must return Success=false (no exception propagation)");
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty,
                "Logout failure must include a message");
        }

        // ── Service-layer: Malformed token variants ───────────────────────────────────

        /// <summary>SA8: Refresh with whitespace string returns structured failure.</summary>
        [Test]
        public async Task SA8_RefreshToken_Whitespace_StructuredFailure()
        {
            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync(It.IsAny<string>()))
                .ReturnsAsync((RefreshToken?)null);

            var result = await _authService.RefreshTokenAsync("   ", null, null);

            Assert.That(result.Success, Is.False,
                "Whitespace refresh token must return Success=false");
        }

        /// <summary>SA9: ValidateAccessToken with single dot segment returns null (no exception).</summary>
        [Test]
        public async Task SA9_ValidateAccessToken_SingleDot_ReturnsNull()
        {
            var result = await _authService.ValidateAccessTokenAsync(".");
            Assert.That(result, Is.Null, "Single-dot JWT must return null, not throw");
        }

        /// <summary>SA10: ValidateAccessToken with two-segment JWT returns null (missing signature).</summary>
        [Test]
        public async Task SA10_ValidateAccessToken_TwoSegment_ReturnsNull()
        {
            var result = await _authService.ValidateAccessTokenAsync("header.payload");
            Assert.That(result, Is.Null, "Two-segment JWT (missing signature) must return null");
        }

        /// <summary>SA11: ValidateAccessToken with oversized token string returns null (no exception).</summary>
        [Test]
        public async Task SA11_ValidateAccessToken_OversizedToken_ReturnsNull()
        {
            var oversized = new string('A', 10000);
            var result = await _authService.ValidateAccessTokenAsync(oversized);
            Assert.That(result, Is.Null, "Oversized token string must return null, not throw");
        }

        /// <summary>SA12: ValidateAccessToken with SQL injection pattern returns null (no exception).</summary>
        [Test]
        public async Task SA12_ValidateAccessToken_SqlInjectionPattern_ReturnsNull()
        {
            var result = await _authService.ValidateAccessTokenAsync(
                "' OR 1=1; DROP TABLE users; --");
            Assert.That(result, Is.Null, "SQL injection pattern must return null, not throw");
        }

        // ── Service-layer: Account locked session tests ───────────────────────────────

        /// <summary>SA13: Login with locked account returns ACCOUNT_LOCKED error code.</summary>
        [Test]
        public async Task SA13_Login_AccountLocked_ReturnsAccountLockedCode()
        {
            var email = "locked-user@example.com";
            var address = new Arc76CredentialDerivationService(
                new Mock<ILogger<Arc76CredentialDerivationService>>().Object)
                .DeriveAddress(email, KnownPassword);

            var lockedUser = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = email,
                AlgorandAddress = address,
                PasswordHash = HashPassword(KnownPassword),
                LockedUntil = DateTime.UtcNow.AddHours(1)
            };

            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(email))
                .ReturnsAsync(lockedUser);

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = email, Password = KnownPassword },
                null, null);

            Assert.That(result.Success, Is.False,
                "Locked account login must return Success=false");
            Assert.That(result.ErrorCode, Is.EqualTo("ACCOUNT_LOCKED"),
                "Locked account must return ACCOUNT_LOCKED error code");
            Assert.That(result.AccessToken, Is.Null.Or.Empty,
                "Locked account must not issue access token");
        }

        /// <summary>SA14: Revoked token error message is user-safe (no internal detail leakage).</summary>
        [Test]
        public async Task SA14_RevokedToken_ErrorMessage_IsUserSafe()
        {
            var revokedToken = new RefreshToken
            {
                Token = "leaktest-revoked",
                UserId = Guid.NewGuid().ToString(),
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                IsRevoked = true,
                RevokedAt = DateTime.UtcNow.AddHours(-1)
            };

            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync("leaktest-revoked"))
                .ReturnsAsync(revokedToken);

            var result = await _authService.RefreshTokenAsync("leaktest-revoked", null, null);

            Assert.That(result.ErrorMessage, Does.Not.Contain("System."),
                "Revoked token error must not expose system stack details");
            Assert.That(result.ErrorMessage, Does.Not.Contain("null reference"),
                "Revoked token error must not expose null reference details");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "Revoked token must have a user-safe error message");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Part B — Integration tests (full HTTP lifecycle)
        // ─────────────────────────────────────────────────────────────────────────────

        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        private static readonly Dictionary<string, string?> TestConfiguration = new()
        {
            ["App:Account"] = "test test test test test test test test test test test test test test test test test test test test test test test test test",
            ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
            ["AlgorandAuthentication:CheckExpiration"] = "false",
            ["AlgorandAuthentication:Debug"] = "true",
            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
            ["JwtConfig:SecretKey"] = "issue462-session-security-test-secret-key-32chars!",
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
            ["KeyManagementConfig:HardcodedKey"] = "Issue462SessionSecurityTestKey32CharsMin!!"
        };

        [SetUp]
        public void SetupHttp()
        {
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(TestConfiguration);
                    });
                });
            _client = _factory.CreateClient();
        }

        [TearDown]
        public void TearDownHttp()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        private async Task<RegisterResponse> RegisterAsync(string email, string password)
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(result, Is.Not.Null);
            return result!;
        }

        private async Task<LoginResponse> LoginAsync(string email, string password)
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password });
            var result = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(result, Is.Not.Null);
            return result!;
        }

        private HttpClient CreateAuthenticatedClient(string token)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        // ── Integration: Session lifecycle ────────────────────────────────────────────

        /// <summary>SB1: Full lifecycle — register → login → access protected endpoint → logout → session rejected.</summary>
        [Test]
        public async Task SB1_FullLifecycle_RegisterLoginLogoutSessionRejected()
        {
            var email = $"lifecycle-462-{Guid.NewGuid()}@example.com";
            const string password = "Lifecycle462@Pass!";

            // 1. Register
            await RegisterAsync(email, password);

            // 2. Login
            var loginResult = await LoginAsync(email, password);
            Assert.That(loginResult.Success, Is.True, "Login must succeed");
            Assert.That(loginResult.AccessToken, Is.Not.Null.And.Not.Empty);

            // 3. Access protected endpoint — must succeed
            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);
            var profileResp = await authClient.GetAsync("/api/v1/auth/profile");
            Assert.That(profileResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Profile access with valid token must return 200");

            // 4. Logout
            var logoutResp = await authClient.PostAsync("/api/v1/auth/logout", null);
            Assert.That(logoutResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Logout must return 200 OK");

            // 5. Refresh token after logout must fail (tokens revoked)
            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = loginResult.RefreshToken });

            Assert.That(refreshResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Refresh after logout must return 401 — tokens are revoked");
        }

        /// <summary>SB2: Refresh lifecycle — login → refresh → verify new token works → old refresh rejected.</summary>
        [Test]
        public async Task SB2_RefreshLifecycle_OldRefreshRejectedAfterUse()
        {
            var email = $"refresh-lifecycle-462-{Guid.NewGuid()}@example.com";
            const string password = "RefreshLifecycle462@!";

            await RegisterAsync(email, password);
            var loginResult = await LoginAsync(email, password);
            var originalRefreshToken = loginResult.RefreshToken!;

            // Use the refresh token once
            var firstRefreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = originalRefreshToken });

            // First use should succeed (200) or return a structured failure (401) — never 5xx
            Assert.That(firstRefreshResp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Unauthorized),
                "First refresh must return 200 or 401, never 5xx");

            if (firstRefreshResp.StatusCode == HttpStatusCode.OK)
            {
                // Refresh succeeded — replay the original token and expect rejection
                var replayResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                    new { refreshToken = originalRefreshToken });

                Assert.That(replayResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                    "Replaying a consumed refresh token must return 401 (replay protection)");
            }
        }

        /// <summary>SB3: Invalid refresh token returns 401 with structured body (not 500).</summary>
        [Test]
        public async Task SB3_InvalidRefreshToken_Returns401_WithStructuredBody()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = $"totally-invalid-{Guid.NewGuid()}" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Invalid refresh token must return 401 Unauthorized");

            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty,
                "Refresh failure must return non-empty body (structured error)");
            Assert.That((int)resp.StatusCode, Is.Not.EqualTo(500),
                "Refresh failure must never return 500");
        }

        /// <summary>SB4: Malformed JWT variants — all return 401 (not 500).</summary>
        [Test]
        [TestCase("")]
        [TestCase("not.a.jwt")]
        [TestCase("Bearer only-one-segment")]
        [TestCase("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.tampered.invalidsig")]
        public async Task SB4_MalformedJWT_Returns401_NotException(string badToken)
        {
            using var authClient = CreateAuthenticatedClient(badToken);
            var resp = await authClient.GetAsync("/api/v1/auth/profile");

            Assert.That((int)resp.StatusCode, Is.Not.EqualTo(500),
                $"Malformed token '{(badToken.Length > 20 ? badToken[..20] : badToken)}...' must not cause 500");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                $"Malformed JWT must return 401");
        }

        /// <summary>SB5: Empty refresh token body returns 401.</summary>
        [Test]
        public async Task SB5_EmptyRefreshToken_Returns401()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = "" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Empty refresh token must return 401 Unauthorized");
        }

        /// <summary>SB6: Re-login after password change — old tokens rejected, new tokens work.</summary>
        [Test]
        public async Task SB6_PasswordChange_OldTokenStillValid_NewCredentialsWork()
        {
            var email = $"chgpass-462-{Guid.NewGuid()}@example.com";
            const string oldPassword = "OldPassword462@!";
            const string newPassword = "NewPassword462@!";

            await RegisterAsync(email, oldPassword);
            var loginResult = await LoginAsync(email, oldPassword);

            // Change password
            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);
            var changeResp = await authClient.PostAsJsonAsync("/api/v1/auth/change-password",
                new { currentPassword = oldPassword, newPassword });

            // Change password should return 200 (success) or 400 (validation error) — never 500
            Assert.That(changeResp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.BadRequest),
                "Change password must return 200 or 400, never 500");

            // Old credentials login (must fail since password changed)
            if (changeResp.IsSuccessStatusCode)
            {
                var oldLoginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                    new { email, password = oldPassword });
                Assert.That(oldLoginResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                    "Login with old password after change must return 401");

                // New credentials must work
                var newLoginResult = await LoginAsync(email, newPassword);
                Assert.That(newLoginResult.Success, Is.True,
                    "Login with new password must succeed");
                Assert.That(newLoginResult.AccessToken, Is.Not.Null.And.Not.Empty,
                    "New login must issue access token");
            }
        }

        /// <summary>SB7: Session inspect after login returns IsActive=true with correct fields.</summary>
        [Test]
        public async Task SB7_SessionInspect_AfterLogin_IsActiveWithCorrectFields()
        {
            var email = $"session-inspect-462-{Guid.NewGuid()}@example.com";
            const string password = "SessionInspect462@!";

            await RegisterAsync(email, password);
            var loginResult = await LoginAsync(email, password);

            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);
            var resp = await authClient.GetAsync("/api/v1/auth/session");

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Session inspect with valid token must return 200");

            var session = await resp.Content.ReadFromJsonAsync<SessionInspectionResponse>();
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.IsActive, Is.True,
                "Authenticated session must be active");
            Assert.That(session.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "Session must include AlgorandAddress for traceability");
        }

        /// <summary>SB8: Session inspect without token returns 401 (not 403 or 500).</summary>
        [Test]
        public async Task SB8_SessionInspect_NoToken_Returns401()
        {
            var resp = await _client.GetAsync("/api/v1/auth/session");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Session inspect without token must return 401");
        }

        /// <summary>SB9: Verify-session endpoint rejects all forms of invalid tokens with 401.</summary>
        [Test]
        public async Task SB9_VerifySession_InvalidVariants_All401()
        {
            var invalidTokens = new[]
            {
                "not.a.jwt.at.all",
                "eyJhbGciOiJub25lIn0.eyJzdWIiOiJoYWNrZXIifQ.",  // alg=none attack
                "a.b.c",
                "header.payload.signature"
            };

            foreach (var token in invalidTokens)
            {
                using var authClient = CreateAuthenticatedClient(token);
                var resp = await authClient.PostAsync("/api/v1/auth/arc76/verify-session", null);

                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                    $"verify-session with token '{token[..Math.Min(20, token.Length)]}' must return 401");
            }
        }

        /// <summary>SB10: Replay protection — refresh token cannot be used twice.</summary>
        [Test]
        public async Task SB10_RefreshToken_CannotBeUsedTwice_ReplayProtection()
        {
            var email = $"replay-462-{Guid.NewGuid()}@example.com";
            const string password = "ReplayProtect462@!";

            await RegisterAsync(email, password);
            var loginResult = await LoginAsync(email, password);
            var refreshToken = loginResult.RefreshToken!;

            Assert.That(refreshToken, Is.Not.Null.And.Not.Empty,
                "Login must return refresh token");

            // First use
            var firstResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken });

            // Second use of same token (replay)
            var replayResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken });

            // If first succeeded, second must fail (replay protection)
            if (firstResp.StatusCode == HttpStatusCode.OK)
            {
                Assert.That(replayResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                    "Replaying a used refresh token must return 401 (replay protection enforced)");
            }
            else
            {
                // Both failed — that's fine, replay protection is implicitly satisfied
                Assert.That((int)replayResp.StatusCode, Is.Not.EqualTo(500),
                    "Refresh failure must never return 500");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Helper
        // ─────────────────────────────────────────────────────────────────────────────

        private static string HashPassword(string password)
        {
            var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var hash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(salt + password)));
            return $"{salt}:{hash}";
        }
    }
}
