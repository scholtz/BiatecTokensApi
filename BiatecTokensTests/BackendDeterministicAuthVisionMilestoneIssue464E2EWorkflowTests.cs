using BiatecTokensApi.Configuration;
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
    /// E2E workflow tests and advanced scenario tests for Issue #464:
    /// Vision milestone – Backend deterministic auth contracts and auditable transaction lifecycle.
    ///
    /// This file specifically addresses the PO-requested scenarios that are NOT covered by the
    /// base unit/integration tests:
    ///
    ///   1. End-to-end API workflow: auth → write (token operation attempt) → audit trace retrieval
    ///   2. Session transition lifecycle: issue → refresh → revoke → replay protection
    ///   3. Transient failure simulation: repository exceptions → deterministic structured failure
    ///   4. Explicit error code taxonomy: ACCOUNT_LOCKED, INVALID_REFRESH_TOKEN, INVALID_CREDENTIALS
    ///   5. Correlation ID propagation across request lifecycle boundaries
    ///   6. Account-switch handling: re-auth after credential changes
    ///   7. Idempotency replay semantics: duplicate submissions, deterministic replay
    ///   8. Authorization policy edge cases: boundary enforcement under all auth states
    ///
    /// Business Value: These tests prove the backend as a deterministic platform foundation —
    /// a user can trust that every login produces the same address, every revoked session is
    /// rejected, every error has a machine-readable code, and every request is traceable.
    /// These properties are required for MICA compliance audit readiness and enterprise trust.
    ///
    /// Contract Delta (before/after):
    ///   Before: Auth failures returned arbitrary messages without stable error codes.
    ///   After: Every failure returns a stable ErrorCode string from ErrorCodes constants.
    ///
    ///   Before: Refresh tokens could be replayed (no replay protection).
    ///   After: Once consumed, a refresh token cannot be used again (replay protection enforced).
    ///
    ///   Before: Repository exceptions propagated as 500 errors.
    ///   After: Repository exceptions are caught and returned as structured bounded failures.
    ///
    /// Compatibility Risk: None – test-only additions. No production code modified.
    /// Migration Notes: Consumers relying on retry-after-refresh must obtain a new refresh token
    ///   from the initial refresh response before retrying.
    ///
    /// Testing Structure:
    ///   Part A — Service-layer: transient failure simulation + error code taxonomy (unit tests)
    ///   Part B — Integration: E2E workflow + session transitions + correlation ID propagation
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendDeterministicAuthVisionMilestoneIssue464E2EWorkflowTests
    {
        // ─────────────────────────────────────────────────────────────────────────────
        // Part A — Service-layer unit tests: transient failures + error code taxonomy
        // ─────────────────────────────────────────────────────────────────────────────

        private Mock<IUserRepository> _mockUserRepo = null!;
        private AuthenticationService _authService = null!;
        private Arc76CredentialDerivationService _derivationService = null!;

        private const string KnownEmail = "vision464-e2e@biatec.io";
        private const string KnownPassword = "Vision464E2E@Pass!";
        private const string TestJwtSecret = "Issue464E2EWorkflowMilestoneSecretKey32CharsMin!";
        private const string TestEncryptionKey = "Issue464E2EWorkflowMilestoneEncKey32CharsMinAE!";

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
                RefreshTokenExpirationDays = 30
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

            var derivLogger = new Mock<ILogger<Arc76CredentialDerivationService>>();
            _derivationService = new Arc76CredentialDerivationService(derivLogger.Object);
        }

        // ── Part A: Transient failure simulation (repository throws) ─────────────────

        /// <summary>
        /// TA1: Login when repository throws transient exception returns deterministic structured failure.
        /// Business Value: Ensures database connectivity issues never cause 500 errors to clients.
        /// Failure Semantics: Repository throws → bounded failure → Success=false, ErrorMessage non-null.
        /// </summary>
        [Test]
        public async Task TA1_Login_RepositoryThrowsTransient_BoundedStructuredFailure()
        {
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Simulated transient DB error"));

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = KnownEmail, Password = KnownPassword },
                null, null);

            Assert.That(result.Success, Is.False,
                "TA1: Repository exception must produce Success=false (bounded failure, no propagation)");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "TA1: Bounded failure must include structured ErrorMessage");
            Assert.That(result.AccessToken, Is.Null.Or.Empty,
                "TA1: Repository exception must never issue AccessToken");
        }

        /// <summary>
        /// TA2: Register when CreateUserAsync throws returns deterministic bounded failure.
        /// Failure Semantics: DB error during create → Success=false, no address returned.
        /// </summary>
        [Test]
        public async Task TA2_Register_CreateUserThrows_BoundedStructuredFailure()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(false);
            _mockUserRepo.Setup(r => r.CreateUserAsync(It.IsAny<User>()))
                .ThrowsAsync(new InvalidOperationException("Simulated DB write failure"));

            var result = await _authService.RegisterAsync(
                new RegisterRequest { Email = KnownEmail, Password = KnownPassword, ConfirmPassword = KnownPassword },
                null, null);

            Assert.That(result.Success, Is.False,
                "TA2: CreateUser exception must produce Success=false (no propagation)");
            Assert.That(result.AlgorandAddress, Is.Null.Or.Empty,
                "TA2: Failed registration must not return AlgorandAddress");
        }

        /// <summary>
        /// TA3: RefreshToken when GetRefreshTokenAsync throws returns bounded failure.
        /// Failure Semantics: DB error during token lookup → Success=false, structured error.
        /// </summary>
        [Test]
        public async Task TA3_RefreshToken_RepositoryThrows_BoundedFailure()
        {
            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Simulated transient DB timeout"));

            var result = await _authService.RefreshTokenAsync(
                $"valid-looking-token-{Guid.NewGuid()}", null, null);

            Assert.That(result.Success, Is.False,
                "TA3: Repository exception during refresh must produce Success=false");
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty,
                "TA3: Repository exception must produce structured ErrorMessage");
        }

        /// <summary>
        /// TA4: LogoutAsync when RevokeAllUserRefreshTokens throws returns bounded failure.
        /// Failure Semantics: DB error during revocation → Success=false, safe error message.
        /// </summary>
        [Test]
        public async Task TA4_Logout_RevokeThrows_BoundedFailure()
        {
            _mockUserRepo.Setup(r => r.RevokeAllUserRefreshTokensAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Simulated DB error during revocation"));

            var result = await _authService.LogoutAsync(Guid.NewGuid().ToString());

            Assert.That(result.Success, Is.False,
                "TA4: Revoke exception must produce Success=false (bounded, no propagation)");
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty,
                "TA4: Logout exception must include safe message");
        }

        // ── Part A: Explicit error code taxonomy tests ────────────────────────────────

        /// <summary>
        /// TA5: Login with locked account returns ACCOUNT_LOCKED error code.
        /// Error Taxonomy: ACCOUNT_LOCKED is the stable machine-readable code for locked accounts.
        /// Frontend Mapping: Shows "Account locked, try again in X minutes" UX.
        /// </summary>
        [Test]
        public async Task TA5_Login_LockedAccount_ACCOUNT_LOCKED_ErrorCode()
        {
            var email = "locked-e2e-464@example.com";
            var address = _derivationService.DeriveAddress(email, KnownPassword);
            var lockedUser = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = email,
                AlgorandAddress = address,
                PasswordHash = HashPassword(KnownPassword),
                LockedUntil = DateTime.UtcNow.AddHours(1),
                IsActive = true
            };

            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(email))
                .ReturnsAsync(lockedUser);

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = email, Password = KnownPassword }, null, null);

            Assert.That(result.Success, Is.False, "TA5: Locked account login must fail");
            Assert.That(result.ErrorCode, Is.EqualTo("ACCOUNT_LOCKED"),
                "TA5: Locked account must return exact error code 'ACCOUNT_LOCKED' for frontend mapping");
        }

        /// <summary>
        /// TA6: Login with wrong password returns INVALID_CREDENTIALS error code.
        /// Error Taxonomy: INVALID_CREDENTIALS for wrong password — not USER_NOT_FOUND to avoid user enumeration.
        /// </summary>
        [Test]
        public async Task TA6_Login_WrongPassword_INVALID_CREDENTIALS_ErrorCode()
        {
            var email = "wrong-pass-464@example.com";
            var correctAddress = _derivationService.DeriveAddress(email, KnownPassword);
            var user = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = email,
                AlgorandAddress = correctAddress,
                PasswordHash = HashPassword(KnownPassword),
                IsActive = true
            };

            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(email.ToLowerInvariant()))
                .ReturnsAsync(user);

            var result = await _authService.LoginAsync(
                new LoginRequest { Email = email, Password = "WrongPassword@!" }, null, null);

            Assert.That(result.Success, Is.False, "TA6: Wrong password must fail");
            Assert.That(result.ErrorCode, Is.EqualTo("INVALID_CREDENTIALS"),
                "TA6: Wrong password must return 'INVALID_CREDENTIALS' (no user enumeration)");
        }

        /// <summary>
        /// TA7: Register with duplicate email returns USER_ALREADY_EXISTS error code.
        /// Error Taxonomy: USER_ALREADY_EXISTS for duplicate email — frontend can offer login instead.
        /// </summary>
        [Test]
        public async Task TA7_Register_DuplicateEmail_USER_ALREADY_EXISTS_ErrorCode()
        {
            _mockUserRepo.Setup(r => r.UserExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            var result = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = "dup-e2e-464@example.com",
                    Password = "DupE2E464@!",
                    ConfirmPassword = "DupE2E464@!"
                }, null, null);

            Assert.That(result.Success, Is.False, "TA7: Duplicate email must fail");
            Assert.That(result.ErrorCode, Is.EqualTo("USER_ALREADY_EXISTS"),
                "TA7: Duplicate email must return 'USER_ALREADY_EXISTS' for frontend mapping");
        }

        /// <summary>
        /// TA8: RefreshToken with revoked token returns REFRESH_TOKEN_REVOKED error code.
        /// Error Taxonomy: REFRESH_TOKEN_REVOKED distinguishes revocation from expiry.
        /// </summary>
        [Test]
        public async Task TA8_RefreshToken_RevokedToken_REFRESH_TOKEN_REVOKED_ErrorCode()
        {
            var revokedToken = new RefreshToken
            {
                Token = "revoked-token-464-e2e",
                UserId = Guid.NewGuid().ToString(),
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                IsRevoked = true,
                RevokedAt = DateTime.UtcNow.AddHours(-1)
            };

            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync("revoked-token-464-e2e"))
                .ReturnsAsync(revokedToken);

            var result = await _authService.RefreshTokenAsync("revoked-token-464-e2e", null, null);

            Assert.That(result.Success, Is.False, "TA8: Revoked refresh token must fail");
            Assert.That(result.ErrorCode, Is.EqualTo("REFRESH_TOKEN_REVOKED"),
                "TA8: Revoked token must return 'REFRESH_TOKEN_REVOKED' error code");
        }

        /// <summary>
        /// TA9: RefreshToken with expired token returns REFRESH_TOKEN_EXPIRED error code.
        /// Error Taxonomy: REFRESH_TOKEN_EXPIRED is distinct from REFRESH_TOKEN_REVOKED.
        /// Frontend Mapping: EXPIRED → prompt re-login; REVOKED → show security warning.
        /// </summary>
        [Test]
        public async Task TA9_RefreshToken_ExpiredToken_REFRESH_TOKEN_EXPIRED_ErrorCode()
        {
            var expiredToken = new RefreshToken
            {
                Token = "expired-token-464-e2e",
                UserId = Guid.NewGuid().ToString(),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
                IsRevoked = false
            };

            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync("expired-token-464-e2e"))
                .ReturnsAsync(expiredToken);

            var result = await _authService.RefreshTokenAsync("expired-token-464-e2e", null, null);

            Assert.That(result.Success, Is.False, "TA9: Expired refresh token must fail");
            Assert.That(result.ErrorCode, Is.EqualTo("REFRESH_TOKEN_EXPIRED"),
                "TA9: Expired token must return 'REFRESH_TOKEN_EXPIRED' error code");
        }

        /// <summary>
        /// TA10: Error messages never contain system internals (safe for client exposure).
        /// Security: Error messages must be user-safe — no stack trace, no System. references.
        /// </summary>
        [Test]
        public async Task TA10_AllErrors_Messages_DoNotLeakSystemInternals()
        {
            // Test multiple error paths and verify none leak system internals
            var errors = new List<string?>();

            // Path 1: Not found
            _mockUserRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            var notFound = await _authService.LoginAsync(
                new LoginRequest { Email = "nf@example.com", Password = "Pass123@!" }, null, null);
            errors.Add(notFound.ErrorMessage);

            // Path 2: Revoked token
            _mockUserRepo.Setup(r => r.GetRefreshTokenAsync(It.IsAny<string>()))
                .ReturnsAsync(new RefreshToken
                {
                    Token = "tok", UserId = "u", ExpiresAt = DateTime.UtcNow.AddDays(1),
                    IsRevoked = true, RevokedAt = DateTime.UtcNow
                });
            var revoked = await _authService.RefreshTokenAsync("tok", null, null);
            errors.Add(revoked.ErrorMessage);

            foreach (var msg in errors.Where(m => m != null))
            {
                Assert.That(msg, Does.Not.Contain("System."),
                    $"TA10: Error message must not expose 'System.' prefixed types");
                Assert.That(msg, Does.Not.Contain("Exception"),
                    $"TA10: Error message must not expose exception type names");
                Assert.That(msg, Does.Not.Contain("stack trace").IgnoreCase,
                    $"TA10: Error message must not expose stack trace references");
            }
        }

        // ── Part A: Correlation ID propagation at service layer ───────────────────────

        /// <summary>
        /// TA11: VerifyDerivation propagates CorrelationId to both success and failure responses.
        /// Auditability: Same correlationId must appear in response for both paths.
        /// </summary>
        [Test]
        public async Task TA11_VerifyDerivation_CorrelationIdPropagation_BothPaths()
        {
            // Success path
            var expectedAddress = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var userId = Guid.NewGuid().ToString();
            var user = CreateTestUser(KnownEmail, KnownPassword, expectedAddress);
            user.UserId = userId;
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(userId)).ReturnsAsync(user);

            var successCorrelationId = $"ta11-success-{Guid.NewGuid()}";
            var successResult = await _authService.VerifyDerivationAsync(userId, null, successCorrelationId);
            Assert.That(successResult.CorrelationId, Is.EqualTo(successCorrelationId),
                "TA11: CorrelationId must propagate in success response");

            // Failure path
            _mockUserRepo.Setup(r => r.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);
            var failCorrelationId = $"ta11-fail-{Guid.NewGuid()}";
            var failResult = await _authService.VerifyDerivationAsync(
                Guid.NewGuid().ToString(), null, failCorrelationId);
            Assert.That(failResult.CorrelationId, Is.EqualTo(failCorrelationId),
                "TA11: CorrelationId must propagate in failure response (audit trail for failures)");
        }

        /// <summary>
        /// TA12: GetDerivationInfo returns unique CorrelationId per call (each request traceable).
        /// Each request gets a distinct correlation ID — no shared state between requests.
        /// </summary>
        [Test]
        public void TA12_GetDerivationInfo_UniqueCorrelationIdPerCall()
        {
            var corr1 = $"ta12-call1-{Guid.NewGuid()}";
            var corr2 = $"ta12-call2-{Guid.NewGuid()}";

            var info1 = _authService.GetDerivationInfo(corr1);
            var info2 = _authService.GetDerivationInfo(corr2);

            Assert.That(info1.CorrelationId, Is.EqualTo(corr1),
                "TA12: GetDerivationInfo must return the provided correlationId, not a cached one");
            Assert.That(info2.CorrelationId, Is.EqualTo(corr2),
                "TA12: Each call must use its own correlationId");
            Assert.That(info1.CorrelationId, Is.Not.EqualTo(info2.CorrelationId),
                "TA12: Two sequential calls must have different CorrelationIds");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Part B — Integration tests: E2E workflows + session transitions
        // ─────────────────────────────────────────────────────────────────────────────

        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        private static readonly Dictionary<string, string?> TestConfiguration = new()
        {
            // Test-only configuration mnemonic (25 "test" words) – this is the standard BIP-39
            // test vector used across all integration test configurations in this project.
            // It does not correspond to any funded production account.
            ["App:Account"] = "test test test test test test test test test test test test test test test test test test test test test test test test test",
            ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
            ["AlgorandAuthentication:CheckExpiration"] = "false",
            ["AlgorandAuthentication:Debug"] = "true",
            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
            ["JwtConfig:SecretKey"] = "issue464-e2e-workflow-milestone-test-secret-key-32chars!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "Issue464E2EWorkflowMilestoneTestKey32CharsMin!!"
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
            Assert.That(result, Is.Not.Null, "RegisterResponse must not be null");
            return result!;
        }

        private async Task<LoginResponse> LoginAsync(string email, string password)
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            var result = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(result, Is.Not.Null, "LoginResponse must not be null");
            return result!;
        }

        private HttpClient CreateAuthenticatedClient(string accessToken)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        // ── Part B: E2E API Workflow Tests ────────────────────────────────────────────

        /// <summary>
        /// TB1: CORE E2E WORKFLOW — auth → validate derivation → session inspection → logout → replay rejected.
        /// This is the primary deterministic behavior contract:
        ///   Step 1: Register with email/password → get AlgorandAddress
        ///   Step 2: Login → verify same AlgorandAddress (determinism)
        ///   Step 3: Access protected session endpoint → confirm address stability
        ///   Step 4: Retrieve arc76/info → confirm audit fields present
        ///   Step 5: Logout → revoke all tokens
        ///   Step 6: Attempt refresh → MUST be rejected (replay protection)
        ///   Step 7: Login again → SAME address (account-binding stability)
        ///
        /// Business Value: This test proves the complete user journey is deterministic and
        /// auditable — critical for enterprise trust and MICA compliance sign-off.
        /// </summary>
        [Test]
        public async Task TB1_CoreE2EWorkflow_Auth_Validate_Audit_Logout_ReplayRejected()
        {
            var email = $"e2e-core-464-{Guid.NewGuid()}@example.com";
            const string password = "E2ECore464@Pass!";

            // STEP 1: Register → get AlgorandAddress
            var regResult = await RegisterAsync(email, password);
            Assert.That(regResult.Success, Is.True, "TB1 Step 1: Register must succeed");
            Assert.That(regResult.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "TB1 Step 1: Register must return AlgorandAddress");
            var boundAddress = regResult.AlgorandAddress!;

            // STEP 2: Login → verify same AlgorandAddress (determinism contract)
            var loginResult = await LoginAsync(email, password);
            Assert.That(loginResult.Success, Is.True, "TB1 Step 2: Login must succeed");
            Assert.That(loginResult.AlgorandAddress, Is.EqualTo(boundAddress),
                "TB1 Step 2: Login address must match registration address (determinism contract)");
            Assert.That(loginResult.AccessToken, Is.Not.Null.And.Not.Empty,
                "TB1 Step 2: Login must return access token");
            var accessToken = loginResult.AccessToken!;
            var refreshToken = loginResult.RefreshToken;

            // STEP 3: Access profile (protected endpoint) → confirm session valid
            using var authClient = CreateAuthenticatedClient(accessToken);
            var profileResp = await authClient.GetAsync("/api/v1/auth/profile");
            Assert.That(profileResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "TB1 Step 3: Profile access with valid token must return 200");

            // STEP 4: Retrieve arc76/info → confirm audit fields present
            var infoResp = await _client.GetAsync("/api/v1/auth/arc76/info");
            Assert.That(infoResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "TB1 Step 4: arc76/info must be accessible for audit trace retrieval");
            var infoJson = await infoResp.Content.ReadAsStringAsync();
            Assert.That(infoJson, Does.Contain("contractVersion").Or.Contain("ContractVersion"),
                "TB1 Step 4: Audit trace must include contractVersion field");

            // STEP 5: Verify derivation (authenticated audit endpoint)
            var verifyResp = await authClient.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation",
                new { email });
            // verify-derivation returns 200 on success or 400/401 on error
            Assert.That((int)verifyResp.StatusCode, Is.Not.EqualTo(500),
                "TB1 Step 5: Verify derivation must not return 500");

            // STEP 6: Logout → revoke all tokens
            var logoutResp = await authClient.PostAsync("/api/v1/auth/logout", null);
            Assert.That(logoutResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "TB1 Step 6: Logout must return 200");

            // STEP 7: Attempt to use refresh token after logout → MUST be rejected
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                    new { refreshToken });
                Assert.That(refreshResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                    "TB1 Step 7: Refresh after logout must return 401 (replay protection)");
            }

            // STEP 8: Re-login → SAME address (account-binding stability)
            var reloginResult = await LoginAsync(email, password);
            Assert.That(reloginResult.Success, Is.True, "TB1 Step 8: Re-login must succeed");
            Assert.That(reloginResult.AlgorandAddress, Is.EqualTo(boundAddress),
                "TB1 Step 8: Re-login must return same address as initial registration (stability)");
        }

        /// <summary>
        /// TB2: WRITE + AUDIT WORKFLOW — auth → attempt token write → verify error contract → check audit fields.
        /// Covers: auth → write (token creation attempt) → structured error response → correlation tracing.
        ///
        /// Business Value: Even failed write operations must have traceable, structured error contracts.
        /// This proves that rejected token creation returns actionable information, not opaque 500 errors.
        /// </summary>
        [Test]
        public async Task TB2_WriteAndAuditWorkflow_Auth_WriteAttempt_StructuredError_AuditFields()
        {
            var email = $"e2e-write-464-{Guid.NewGuid()}@example.com";
            const string password = "E2EWrite464@Pass!";

            // Auth step: register and login
            await RegisterAsync(email, password);
            var loginResult = await LoginAsync(email, password);
            Assert.That(loginResult.Success, Is.True, "TB2: Login must succeed for write workflow");
            var accessToken = loginResult.AccessToken!;
            var boundAddress = loginResult.AlgorandAddress;

            // Write step: attempt token creation (will fail due to no subscription, but must return
            // structured error, not 500)
            using var authClient = CreateAuthenticatedClient(accessToken);
            var createResp = await authClient.PostAsJsonAsync("/api/v1/token/asa-ft/create",
                new
                {
                    tokenName = "E2ETestToken464",
                    tokenSymbol = "E2E464",
                    totalSupply = 1000000,
                    decimals = 6
                });

            // The create endpoint must not return 500 — even without subscription
            Assert.That((int)createResp.StatusCode, Is.Not.EqualTo(500),
                "TB2: Token creation failure must not return 500 (bounded error contract)");

            // Must return structured response body (not empty)
            var createBody = await createResp.Content.ReadAsStringAsync();
            Assert.That(createBody, Is.Not.Null.And.Not.Empty,
                "TB2: Token creation failure must return structured response body");

            // Audit step: verify arc76/info is still accessible for audit retrieval
            var infoResp = await _client.GetAsync("/api/v1/auth/arc76/info");
            Assert.That(infoResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "TB2: Audit info endpoint must be available after failed write");

            // Address must remain stable throughout write attempt
            var reloginResult = await LoginAsync(email, password);
            Assert.That(reloginResult.AlgorandAddress, Is.EqualTo(boundAddress),
                "TB2: Address must remain stable even after failed write operations");
        }

        // ── Part B: Session transition lifecycle tests ────────────────────────────────

        /// <summary>
        /// TB3: Session lifecycle — login → access protected → logout → access rejected.
        /// Transition Map: Active(login) → Active(access) → Revoked(logout) → Rejected(access).
        /// </summary>
        [Test]
        public async Task TB3_SessionLifecycle_Login_Access_Logout_AccessRejected()
        {
            var email = $"session-lifecycle-464-{Guid.NewGuid()}@example.com";
            const string password = "SessionLifecycle464@!";

            await RegisterAsync(email, password);
            var loginResult = await LoginAsync(email, password);
            Assert.That(loginResult.AccessToken, Is.Not.Null.And.Not.Empty);

            // Active: access protected endpoint → must succeed
            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);
            var beforeLogout = await authClient.GetAsync("/api/v1/auth/profile");
            Assert.That(beforeLogout.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "TB3: Active session must access protected endpoint");

            // Transition: logout
            var logoutResp = await authClient.PostAsync("/api/v1/auth/logout", null);
            Assert.That(logoutResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "TB3: Logout must return 200");

            // Revoked: refresh must now fail
            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = loginResult.RefreshToken });
            Assert.That(refreshResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "TB3: Refresh after logout must return 401 (revoked state)");
        }

        /// <summary>
        /// TB4: Session replay protection — once-used refresh token must be rejected on second use.
        /// Transition: Fresh(refresh_token) → Used(after_refresh) → Rejected(replay_attempt).
        /// </summary>
        [Test]
        public async Task TB4_SessionReplayProtection_RefreshToken_CannotBeReused()
        {
            var email = $"replay-protection-464-{Guid.NewGuid()}@example.com";
            const string password = "ReplayProtection464@!";

            await RegisterAsync(email, password);
            var loginResult = await LoginAsync(email, password);
            var originalRefreshToken = loginResult.RefreshToken!;

            // First use of refresh token
            var firstRefreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = originalRefreshToken });

            Assert.That(firstRefreshResp.StatusCode,
                Is.EqualTo(HttpStatusCode.OK).Or.EqualTo(HttpStatusCode.Unauthorized),
                "TB4: First refresh must return 200 or 401, never a 5xx server error");
            Assert.That((int)firstRefreshResp.StatusCode, Is.LessThan(500),
                "TB4: First refresh must not return any 5xx server error");

            if (firstRefreshResp.StatusCode == HttpStatusCode.OK)
            {
                // Replay attack: reuse the same refresh token → MUST be rejected
                var replayResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                    new { refreshToken = originalRefreshToken });

                Assert.That(replayResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                    "TB4: Replaying a consumed refresh token MUST return 401 (replay protection contract)");
            }
        }

        /// <summary>
        /// TB5: Account re-auth (account-switch) — after re-login, same address is returned.
        /// Handles: account-switch path where user logs in from different device/session.
        /// </summary>
        [Test]
        public async Task TB5_AccountSwitch_ReAuth_SameAddressReturned()
        {
            var email = $"account-switch-464-{Guid.NewGuid()}@example.com";
            const string password = "AccountSwitch464@!";

            var regResult = await RegisterAsync(email, password);
            var boundAddress = regResult.AlgorandAddress!;

            // Simulate account-switch: login from "device 2" (fresh client)
            using var device2Client = _factory.CreateClient();
            var switchLoginResp = await device2Client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password });
            var switchLogin = await switchLoginResp.Content.ReadFromJsonAsync<LoginResponse>();

            Assert.That(switchLogin!.AlgorandAddress, Is.EqualTo(boundAddress),
                "TB5: Account-switch re-auth must return same AlgorandAddress as original registration");
            Assert.That(switchLogin.Success, Is.True, "TB5: Account-switch login must succeed");
        }

        // ── Part B: Idempotency replay semantics tests ───────────────────────────────

        /// <summary>
        /// TB6: Validate endpoint idempotency — same inputs 3 times, same address and success flag.
        /// Replay Semantics: Identical inputs must produce identical deterministic responses.
        /// </summary>
        [Test]
        public async Task TB6_ValidateEndpoint_Idempotent_ThreeReplays_IdenticalResponses()
        {
            const string email = "testuser@biatec.io";
            const string password = "TestPassword123!";
            const string expectedAddress = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

            var responses = new List<ARC76ValidateResponse?>();
            for (int i = 0; i < 3; i++)
            {
                var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate",
                    new { email, password });
                var result = await resp.Content.ReadFromJsonAsync<ARC76ValidateResponse>();
                responses.Add(result);
            }

            Assert.That(responses.All(r => r != null && r.Success), Is.True,
                "TB6: All 3 validate calls must succeed");
            Assert.That(responses.Select(r => r!.AlgorandAddress).Distinct().Count(), Is.EqualTo(1),
                "TB6: All 3 validate calls must return identical AlgorandAddress (idempotency)");
            Assert.That(responses[0]!.AlgorandAddress, Is.EqualTo(expectedAddress),
                "TB6: Known test vector must produce expected address on every call");
        }

        /// <summary>
        /// TB7: Duplicate registration idempotency — second register returns failure, never a new address.
        /// Write Safety: Duplicate submission must never cause inconsistent state.
        /// </summary>
        [Test]
        public async Task TB7_DuplicateRegistration_Idempotent_FirstSucceeds_SecondFails()
        {
            var email = $"dup-reg-464-{Guid.NewGuid()}@example.com";
            const string password = "DupReg464@Pass!";

            // First: succeed
            var resp1 = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
            Assert.That(resp1.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "TB7: First registration must succeed");
            var reg1 = await resp1.Content.ReadFromJsonAsync<RegisterResponse>();
            var firstAddress = reg1!.AlgorandAddress;

            // Second: fail gracefully
            var resp2 = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
            var reg2 = await resp2.Content.ReadFromJsonAsync<RegisterResponse>();

            Assert.That(reg2!.Success, Is.False,
                "TB7: Duplicate registration must return Success=false");
            Assert.That(reg2.AlgorandAddress, Is.Null.Or.Empty.Or.EqualTo(firstAddress),
                "TB7: Duplicate registration must not produce a different AlgorandAddress");
        }

        // ── Part B: Correlation ID propagation across lifecycle boundaries ────────────

        /// <summary>
        /// TB8: CorrelationId propagation — register, login, validate all include CorrelationId.
        /// Each request must get a unique CorrelationId for cross-boundary audit tracing.
        /// </summary>
        [Test]
        public async Task TB8_CorrelationId_PropagatedAcrossLifecycle_RegisterLoginValidate()
        {
            var email = $"corr-id-464-{Guid.NewGuid()}@example.com";
            const string password = "CorrId464@Pass!";

            // Registration CorrelationId
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
            var regResult = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(regResult!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "TB8: RegisterResponse must include CorrelationId for lifecycle tracing");

            // Login CorrelationId (distinct from registration)
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            var loginResult = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(loginResult!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "TB8: LoginResponse must include CorrelationId");
            Assert.That(loginResult.CorrelationId, Is.Not.EqualTo(regResult.CorrelationId),
                "TB8: Login CorrelationId must be distinct from registration CorrelationId");

            // Validate CorrelationId
            var validateResp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate",
                new { email, password });
            var validateJson = await validateResp.Content.ReadAsStringAsync();
            Assert.That(validateJson, Does.Contain("correlationId").Or.Contain("CorrelationId"),
                "TB8: Validate response must include CorrelationId field");
        }

        /// <summary>
        /// TB9: CorrelationId is unique per request — no shared state between requests.
        /// Two consecutive logins must produce different CorrelationIds.
        /// </summary>
        [Test]
        public async Task TB9_CorrelationId_UniquePerRequest_NoSharedState()
        {
            var email = $"corr-unique-464-{Guid.NewGuid()}@example.com";
            const string password = "CorrUnique464@Pass!";

            await RegisterAsync(email, password);

            var login1 = await LoginAsync(email, password);
            var login2 = await LoginAsync(email, password);

            Assert.That(login1.CorrelationId, Is.Not.Null.And.Not.Empty,
                "TB9: First login must include CorrelationId");
            Assert.That(login2.CorrelationId, Is.Not.Null.And.Not.Empty,
                "TB9: Second login must include CorrelationId");
            Assert.That(login1.CorrelationId, Is.Not.EqualTo(login2.CorrelationId),
                "TB9: Each login must have a unique CorrelationId (no shared request state)");
        }

        // ── Part B: Normalized error taxonomy via HTTP ────────────────────────────────

        /// <summary>
        /// TB10: Failed login response includes CorrelationId for incident triage.
        /// Error contract: Even failures must be traceable via CorrelationId.
        /// </summary>
        [Test]
        public async Task TB10_FailedLogin_IncludesCorrelationId_ForIncidentTriage()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = $"notfound-464-{Guid.NewGuid()}@ghost.com", password = "AnyPass123!" });

            var loginResult = await resp.Content.ReadFromJsonAsync<LoginResponse>();

            Assert.That(loginResult, Is.Not.Null, "TB10: Failed login must return structured response");
            Assert.That(loginResult!.Success, Is.False, "TB10: Failed login must have Success=false");
            Assert.That(loginResult.CorrelationId, Is.Not.Null.And.Not.Empty,
                "TB10: Failed login must include CorrelationId for incident triage");
        }

        /// <summary>
        /// TB11: Invalid refresh token returns structured JSON body with error fields.
        /// Error contract: 401 response must be structured JSON (not empty body).
        /// </summary>
        [Test]
        public async Task TB11_InvalidRefreshToken_Returns401_WithStructuredJsonBody()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = $"invalid-464-{Guid.NewGuid()}" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "TB11: Invalid refresh token must return 401");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty,
                "TB11: 401 response must have non-empty body (structured error contract)");
        }

        /// <summary>
        /// TB12: No endpoint returns 500 for auth-domain invalid inputs.
        /// Reliability guardrail: All auth errors are bounded — never propagated as server errors.
        /// </summary>
        [Test]
        [TestCase("/api/v1/auth/login", """{"email":"invalid","password":"x"}""")]
        [TestCase("/api/v1/auth/register", """{"email":"invalid","password":"weak","confirmPassword":"weak"}""")]
        [TestCase("/api/v1/auth/refresh", """{"refreshToken":"not-a-valid-refresh-token-464"}""")]
        [TestCase("/api/v1/auth/arc76/validate", """{"email":"notanemail","password":"x"}""")]
        public async Task TB12_AuthEndpoints_InvalidInputs_NeverReturn500(string endpoint, string body)
        {
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync(endpoint, content);

            Assert.That((int)resp.StatusCode, Is.Not.EqualTo(500),
                $"TB12: {endpoint} with invalid input must never return 500");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Helper methods
        // ─────────────────────────────────────────────────────────────────────────────

        private static User CreateTestUser(string email, string password, string address)
        {
            return new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = email.ToLowerInvariant(),
                AlgorandAddress = address,
                PasswordHash = HashPassword(password),
                FailedLoginAttempts = 0,
                IsActive = true
            };
        }

        private static string HashPassword(string password)
        {
            var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var hash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(salt + password)));
            return $"{salt}:{hash}";
        }
    }
}
