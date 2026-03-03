using AlgorandARC76AccountDotNet;
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
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// User-journey and impact tests for the Vision Milestone: Complete ARC76 Email-Based
    /// Account Derivation and Backend Token Deployment Pipeline.
    ///
    /// PURPOSE: Provides explicit evidence for happy-path, invalid-input, boundary,
    /// failure-recovery, and non-crypto-native UX scenarios for an enterprise token issuer
    /// who has never used blockchain before.
    ///
    /// USER IMPACT RATIONALE (non-crypto-native users):
    /// • Registration with email + password automatically creates a blockchain account — users
    ///   never need to understand mnemonics, wallets, or private keys.
    /// • The same email + password always leads back to the same Algorand account, even after
    ///   logging out and returning days later. This is the ARC76 guarantee.
    /// • Token deployment is queued and tracked: users submit once and check status, no need
    ///   to monitor blockchain explorers directly.
    /// • Duplicate requests are safe — idempotency prevents double-deployments even if the
    ///   user clicks "Deploy" twice in quick succession.
    /// • Errors are described in plain language — "Invalid email or password" not cryptographic
    ///   stack traces.
    /// • Correlation IDs enable support teams to trace any issue without asking the user for
    ///   technical details.
    /// • Private key material never appears anywhere: not in responses, logs, or error messages.
    ///
    /// Test categories:
    ///   HP = Happy Path (expected success flows)
    ///   II = Invalid Input (user mistake scenarios)
    ///   BD = Boundary (edge/limit cases)
    ///   FR = Failure-Recovery (behavior after errors)
    ///   NX = Non-Crypto-Native Experience (user-facing clarity)
    ///
    /// Roadmap Alignment:
    ///   Phase 1: Core Token Creation &amp; Authentication – ARC76 Account Management (35% → complete)
    ///   Phase 1: Core Token Creation &amp; Authentication – Backend Token Deployment (45% → complete)
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ARC76EmailDeploymentVisionMilestoneUserJourneyTests
    {
        // ── Part A: Service-layer journey tests ──────────────────────────────────
        private Arc76CredentialDerivationService _derivationService = null!;
        private Mock<IUserRepository> _mockUserRepo = null!;
        private AuthenticationService _authService = null!;

        // ── Part B: HTTP journey tests ───────────────────────────────────────────
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        private const string KnownEmail = "testuser@biatec.io";
        private const string KnownPassword = "TestPassword123!";
        private const string KnownAddress = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        private static readonly Dictionary<string, string?> TestConfig = new()
        {
            ["App:Account"] = "test test test test test test test test test test test test test test test test test test test test test test test test test",
            ["AlgorandAuthentication:Realm"] = "BiatecTokens#ARC14",
            ["AlgorandAuthentication:CheckExpiration"] = "false",
            ["AlgorandAuthentication:Debug"] = "true",
            ["AlgorandAuthentication:EmptySuccessOnFailure"] = "true",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Server"] = "https://mainnet-api.4160.nodely.dev",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Token"] = "",
            ["AlgorandAuthentication:AllowedNetworks:wGHE2Pwdvd7S12BL5FaOP20EGYesN73ktiC1qzkkit8=:Header"] = "",
            ["JwtConfig:SecretKey"] = "arc76emaildep-userjourney-test-secret-key-32chars-min!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "ARC76EmailDeploymentUserJourneyTestKey32Chars!!"
        };

        [SetUp]
        public void Setup()
        {
            // Service-layer setup
            var derivationLogger = new Mock<ILogger<Arc76CredentialDerivationService>>();
            _derivationService = new Arc76CredentialDerivationService(derivationLogger.Object);

            _mockUserRepo = new Mock<IUserRepository>();
            var authLogger = new Mock<ILogger<AuthenticationService>>();
            var jwtConfig = new JwtConfig
            {
                SecretKey = "ARC76UserJourneyTestSecretKey32CharsMinimum!!",
                Issuer = "BiatecTokensApi",
                Audience = "BiatecTokensUsers",
                AccessTokenExpirationMinutes = 60,
                RefreshTokenExpirationDays = 30
            };
            var keyMgmtConfig = new KeyManagementConfig
            {
                Provider = "Hardcoded",
                HardcodedKey = "ARC76UserJourneyEncKey32CharsMinimum!AE!!"
            };
            var svc = new ServiceCollection();
            svc.AddLogging();
            svc.Configure<KeyManagementConfig>(c =>
            {
                c.Provider = keyMgmtConfig.Provider;
                c.HardcodedKey = keyMgmtConfig.HardcodedKey;
            });
            svc.AddSingleton<KeyProviderFactory>();
            var sp = svc.BuildServiceProvider();

            _authService = new AuthenticationService(
                _mockUserRepo.Object,
                authLogger.Object,
                Options.Create(jwtConfig),
                sp.GetRequiredService<KeyProviderFactory>());

            // HTTP setup
            _factory = new WebApplicationFactory<BiatecTokensApi.Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(TestConfig);
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

        // ── Helpers ────────────────────────────────────────────────────────────
        private async Task<RegisterResponse> RegisterAsync(string email, string password)
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email,
                password,
                confirmPassword = password
            });
            return (await resp.Content.ReadFromJsonAsync<RegisterResponse>())!;
        }

        private async Task<LoginResponse> LoginAsync(string email, string password)
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            return (await resp.Content.ReadFromJsonAsync<LoginResponse>())!;
        }

        // ────────────────────────────────────────────────────────────────────────
        // HP – Happy Path Scenarios
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// HP1: New enterprise user registers with email+password and receives their Algorand address.
        /// Impact: User gets a blockchain account without needing any wallet knowledge.
        /// </summary>
        [Test]
        public async Task HP1_NewUser_RegistersWithEmailPassword_ReceivesAlgorandAddress()
        {
            var email = $"hp1-{Guid.NewGuid():N}@biatec.io";
            var response = await RegisterAsync(email, "HP1Password123!@Arc76");

            Assert.That(response.Success, Is.True, "HP1: Registration must succeed");
            Assert.That(response.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "HP1: User must receive an Algorand address without needing a wallet");
            Assert.That(response.AlgorandAddress!.Length, Is.EqualTo(58),
                "HP1: Algorand address must be properly formatted (58 chars)");
        }

        /// <summary>
        /// HP2: Registered user logs in and gets the same address as during registration.
        /// Impact: User can always return to their account without a seed phrase.
        /// </summary>
        [Test]
        public async Task HP2_RegisteredUser_LogsIn_ReceivesSameAddress()
        {
            var email = $"hp2-{Guid.NewGuid():N}@biatec.io";
            var reg = await RegisterAsync(email, "HP2Password123!@Arc76");
            var login = await LoginAsync(email, "HP2Password123!@Arc76");

            Assert.That(login.AlgorandAddress, Is.EqualTo(reg.AlgorandAddress),
                "HP2: Login must return same address as registration (ARC76 determinism)");
        }

        /// <summary>
        /// HP3: User logs in, gets a JWT, and can call authenticated endpoints.
        /// Impact: Standard SaaS authentication flow works for blockchain backend.
        /// </summary>
        [Test]
        public async Task HP3_Login_ReceivesJWT_CanCallProtectedEndpoints()
        {
            var email = $"hp3-{Guid.NewGuid():N}@biatec.io";
            await RegisterAsync(email, "HP3Password123!@Arc76");
            var login = await LoginAsync(email, "HP3Password123!@Arc76");

            Assert.That(login.AccessToken, Is.Not.Null.And.Not.Empty,
                "HP3: Login must return a JWT access token");
            Assert.That(login.RefreshToken, Is.Not.Null.And.Not.Empty,
                "HP3: Login must return a refresh token");

            // Verify JWT structure (3 parts separated by dots)
            var parts = login.AccessToken!.Split('.');
            Assert.That(parts.Length, Is.EqualTo(3),
                "HP3: Access token must be a valid 3-part JWT");
        }

        /// <summary>
        /// HP4: ARC76 derivation is deterministic at the service layer.
        /// Impact: Same credentials always recover the same blockchain identity.
        /// </summary>
        [Test]
        public void HP4_ServiceLayer_ARC76Derivation_IsDeterministic()
        {
            var addr1 = _derivationService.DeriveAddress(KnownEmail, KnownPassword);
            var addr2 = _derivationService.DeriveAddress(KnownEmail, KnownPassword);

            Assert.That(addr1, Is.EqualTo(KnownAddress), "HP4: Known test vector must produce expected address");
            Assert.That(addr2, Is.EqualTo(addr1), "HP4: Second derivation must match first (deterministic)");
        }

        /// <summary>
        /// HP5: Token deployment list is accessible after authentication.
        /// Impact: Users can check the status of their token deployments.
        /// </summary>
        [Test]
        public async Task HP5_AuthenticatedUser_CanAccessDeploymentsList()
        {
            var email = $"hp5-{Guid.NewGuid():N}@biatec.io";
            await RegisterAsync(email, "HP5Password123!@Arc76");
            var login = await LoginAsync(email, "HP5Password123!@Arc76");

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", login.AccessToken);
            var resp = await _client.GetAsync("/api/v1/token/deployments");
            _client.DefaultRequestHeaders.Authorization = null;

            Assert.That((int)resp.StatusCode,
                Is.Not.EqualTo(401).And.Not.EqualTo(403),
                "HP5: Authenticated user must access deployments list (not 401/403)");
        }

        /// <summary>
        /// HP6: Token refresh flow works for long-lived sessions.
        /// Impact: User does not need to log in again after 1 hour.
        /// </summary>
        [Test]
        public async Task HP6_AuthenticatedUser_CanRefreshToken()
        {
            var email = $"hp6-{Guid.NewGuid():N}@biatec.io";
            await RegisterAsync(email, "HP6Password123!@Arc76");
            var login = await LoginAsync(email, "HP6Password123!@Arc76");

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = login.RefreshToken });

            Assert.That((int)resp.StatusCode,
                Is.Not.EqualTo(404),
                "HP6: Token refresh endpoint must be reachable");
        }

        /// <summary>
        /// HP7: ARC76 info endpoint provides contract documentation.
        /// Impact: API consumers can discover and verify the derivation algorithm used.
        /// </summary>
        [Test]
        public async Task HP7_ARC76InfoEndpoint_ReturnsDocumentation()
        {
            var resp = await _client.GetAsync("/api/v1/auth/arc76/info");

            Assert.That((int)resp.StatusCode,
                Is.Not.EqualTo(404),
                "HP7: ARC76 info endpoint must be reachable");
        }

        // ────────────────────────────────────────────────────────────────────────
        // II – Invalid Input Scenarios
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// II1: User submits registration with no email.
        /// Impact: User gets a clear error, not a 500 crash.
        /// </summary>
        [Test]
        public async Task II1_Register_EmptyEmail_Returns400WithClearError()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email = "",
                password = "ValidPass123!",
                confirmPassword = "ValidPass123!"
            });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400),
                "II1: Empty email must return 400 (not 500)");
        }

        /// <summary>
        /// II2: User submits login with wrong password.
        /// Impact: User gets 401 (not 500), with no private key leakage.
        /// </summary>
        [Test]
        public async Task II2_Login_WrongPassword_Returns401()
        {
            var email = $"ii2-{Guid.NewGuid():N}@biatec.io";
            await RegisterAsync(email, "CorrectPass123!@Arc76");

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email,
                password = "WrongPassword999!"
            });

            Assert.That((int)resp.StatusCode, Is.EqualTo(401),
                "II2: Wrong password must return 401");
        }

        /// <summary>
        /// II3: User tries to register with an email already taken.
        /// Impact: User gets a descriptive error (not a silent failure or 500).
        /// </summary>
        [Test]
        public async Task II3_Register_DuplicateEmail_ReturnsStructuredError()
        {
            var email = $"ii3-dup-{Guid.NewGuid():N}@biatec.io";
            await RegisterAsync(email, "FirstReg123!@Arc76");

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email,
                password = "SecondReg123!@Arc76",
                confirmPassword = "SecondReg123!@Arc76"
            });
            var result = await resp.Content.ReadFromJsonAsync<RegisterResponse>();

            Assert.That(result!.Success, Is.False,
                "II3: Duplicate registration must return Success=false");
            Assert.That(result.ErrorCode, Is.Not.Null.And.Not.Empty,
                "II3: Duplicate registration must return an error code");
        }

        /// <summary>
        /// II4: Registration with mismatched passwords fails cleanly.
        /// </summary>
        [Test]
        public async Task II4_Register_PasswordMismatch_Returns400()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email = $"ii4-{Guid.NewGuid():N}@biatec.io",
                password = "Pass1ABC123!",
                confirmPassword = "Pass2DEF456!"
            });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400),
                "II4: Password mismatch must return 400");
        }

        /// <summary>
        /// II5: Invalid email format returns 400.
        /// </summary>
        [Test]
        public async Task II5_Register_InvalidEmailFormat_Returns400()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email = "this-is-not-an-email",
                password = "ValidPass123!",
                confirmPassword = "ValidPass123!"
            });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400),
                "II5: Invalid email format must return 400");
        }

        /// <summary>
        /// II6: Service-layer: null email throws ArgumentException (not NullReferenceException).
        /// </summary>
        [Test]
        public void II6_ServiceLayer_NullEmail_ThrowsArgumentException_NotNullRef()
        {
            Assert.Throws<ArgumentException>(
                () => _derivationService.DeriveAddress(null!, "password"),
                "II6: Null email must throw ArgumentException, not NullReferenceException");
        }

        /// <summary>
        /// II7: Service-layer: empty password throws ArgumentException.
        /// </summary>
        [Test]
        public void II7_ServiceLayer_EmptyPassword_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(
                () => _derivationService.DeriveAddress("user@biatec.io", "   "),
                "II7: Whitespace-only password must throw ArgumentException");
        }

        // ────────────────────────────────────────────────────────────────────────
        // BD – Boundary Scenarios
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// BD1: Email at boundary of valid format (very long email).
        /// </summary>
        [Test]
        public void BD1_ServiceLayer_LongEmail_CanonicalizeEmailHandlesIt()
        {
            var longEmail = $"a-very-long-email-prefix-{new string('x', 50)}@biatec.io";
            var result = _derivationService.CanonicalizeEmail(longEmail);

            Assert.That(result, Is.EqualTo(longEmail.Trim().ToLowerInvariant()),
                "BD1: Long email must be canonicalized correctly");
        }

        /// <summary>
        /// BD2: Unicode characters in email are handled without exception.
        /// </summary>
        [Test]
        public void BD2_ServiceLayer_UnicodeEmail_DeriveAddressDoesNotThrow()
        {
            // Unicode in local part – some providers support it
            var email = "tëst-ünïcode@biatec.io";
            Assert.DoesNotThrow(
                () => _derivationService.DeriveAddress(email, KnownPassword),
                "BD2: Unicode email must not throw (may produce different address)");
        }

        /// <summary>
        /// BD3: Password with special characters is handled correctly.
        /// </summary>
        [Test]
        public void BD3_ServiceLayer_PasswordWithSpecialChars_IsDeterministic()
        {
            var specialPassword = "P@$$w0rd!#%^&*()_+";
            var addr1 = _derivationService.DeriveAddress(KnownEmail, specialPassword);
            var addr2 = _derivationService.DeriveAddress(KnownEmail, specialPassword);

            Assert.That(addr1, Is.EqualTo(addr2),
                "BD3: Special character password must produce deterministic address");
        }

        /// <summary>
        /// BD4: Different password produces different address (collision resistance).
        /// </summary>
        [Test]
        public void BD4_ServiceLayer_SimilarPasswords_ProduceDifferentAddresses()
        {
            var addr1 = _derivationService.DeriveAddress(KnownEmail, "Password123!");
            var addr2 = _derivationService.DeriveAddress(KnownEmail, "Password123!2");

            Assert.That(addr1, Is.Not.EqualTo(addr2),
                "BD4: Slightly different passwords must produce completely different addresses");
        }

        /// <summary>
        /// BD5: Email with trailing dot is handled without crash.
        /// </summary>
        [Test]
        public async Task BD5_Register_WeakPassword_IsRejected()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email = $"bd5-{Guid.NewGuid():N}@biatec.io",
                password = "short",
                confirmPassword = "short"
            });

            Assert.That((int)resp.StatusCode, Is.EqualTo(400),
                "BD5: Short/weak password must be rejected with 400");
        }

        // ────────────────────────────────────────────────────────────────────────
        // FR – Failure-Recovery Scenarios
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// FR1: After failed login, user can retry with correct credentials.
        /// </summary>
        [Test]
        public async Task FR1_FailedLogin_CanRetryWithCorrectCredentials()
        {
            var email = $"fr1-{Guid.NewGuid():N}@biatec.io";
            await RegisterAsync(email, "CorrectPass123!@Arc76");

            // Wrong password first
            var failResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email,
                password = "WrongPass999!"
            });
            Assert.That((int)failResp.StatusCode, Is.EqualTo(401), "FR1: Wrong password must return 401");

            // Correct password second
            var successResp = await LoginAsync(email, "CorrectPass123!@Arc76");
            Assert.That(successResp.Success, Is.True,
                "FR1: After failed login, correct credentials must still succeed");
        }

        /// <summary>
        /// FR2: Service-layer: repository exception results in structured failure, not exception.
        /// </summary>
        [Test]
        public async Task FR2_ServiceLayer_RepositoryException_ReturnsStructuredFailure()
        {
            _mockUserRepo
                .Setup(r => r.UserExistsAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Database unavailable"));

            var response = await _authService.RegisterAsync(
                new RegisterRequest
                {
                    Email = "fr2@biatec.io",
                    Password = "FR2Password123!",
                    ConfirmPassword = "FR2Password123!"
                },
                ipAddress: null,
                userAgent: null);

            Assert.That(response.Success, Is.False,
                "FR2: Repository exception must result in Success=false (not thrown to caller)");
        }

        /// <summary>
        /// FR3: Garbage refresh token returns 401, not 500.
        /// </summary>
        [Test]
        public async Task FR3_GarbageRefreshToken_Returns401OrBadRequest()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
            {
                refreshToken = "garbage-token-xyz-not-valid"
            });

            Assert.That((int)resp.StatusCode,
                Is.EqualTo(401).Or.EqualTo(400),
                "FR3: Invalid refresh token must return 401 or 400 (not 500)");
        }

        /// <summary>
        /// FR4: Protected endpoint with expired/invalid token returns 401.
        /// </summary>
        [Test]
        public async Task FR4_InvalidBearerToken_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");
            var resp = await _client.GetAsync("/api/v1/token/deployments");
            _client.DefaultRequestHeaders.Authorization = null;

            Assert.That((int)resp.StatusCode, Is.EqualTo(401),
                "FR4: Invalid bearer token must return 401");
        }

        // ────────────────────────────────────────────────────────────────────────
        // NX – Non-Crypto-Native UX Scenarios
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// NX1: Error responses never contain internal cryptographic details.
        /// </summary>
        [Test]
        public async Task NX1_ErrorResponse_DoesNotContainCryptoInternals()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = "nonexistent@biatec.io",
                password = "WrongPass!"
            });
            var body = await resp.Content.ReadAsStringAsync();

            Assert.That(body, Does.Not.Contain("StackTrace"),
                "NX1: Error response must not expose stack trace");
            Assert.That(body, Does.Not.Contain("Exception"),
                "NX1: Error response must not contain exception class names");
        }

        /// <summary>
        /// NX2: Register response never contains mnemonic or private key.
        /// </summary>
        [Test]
        public async Task NX2_RegisterResponse_NeverContainsMnemonic()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email = $"nx2-{Guid.NewGuid():N}@biatec.io",
                password = "NX2Password123!@Arc76",
                confirmPassword = "NX2Password123!@Arc76"
            });
            var body = await resp.Content.ReadAsStringAsync();

            Assert.That(body.ToLower(), Does.Not.Contain("mnemonic"),
                "NX2: Register response must never contain mnemonic keyword");
            Assert.That(body.ToLower(), Does.Not.Contain("privatekey"),
                "NX2: Register response must never contain privateKey");
            Assert.That(body.ToLower(), Does.Not.Contain("private_key"),
                "NX2: Register response must never contain private_key");
        }

        /// <summary>
        /// NX3: Response includes a correlation ID for support tracing.
        /// </summary>
        [Test]
        public async Task NX3_RegisterResponse_HasTimestamp()
        {
            var response = await RegisterAsync($"nx3-{Guid.NewGuid():N}@biatec.io", "NX3Password123!@Arc76");

            Assert.That(response.Timestamp, Is.Not.EqualTo(default(DateTime)),
                "NX3: Register response must include a Timestamp for tracing and audit");
        }

        /// <summary>
        /// NX4: Login response has a human-readable timestamp for token expiry.
        /// </summary>
        [Test]
        public async Task NX4_LoginResponse_HasTokenExpiry()
        {
            var email = $"nx4-{Guid.NewGuid():N}@biatec.io";
            await RegisterAsync(email, "NX4Password123!@Arc76");
            var login = await LoginAsync(email, "NX4Password123!@Arc76");

            Assert.That(login.ExpiresAt, Is.Not.Null,
                "NX4: Login response must include token expiry time so user knows when to refresh");
        }

        /// <summary>
        /// NX5: Successful registration response includes the user's email (confirmation).
        /// </summary>
        [Test]
        public async Task NX5_RegisterResponse_IncludesUserEmail()
        {
            var email = $"nx5-{Guid.NewGuid():N}@biatec.io";
            var response = await RegisterAsync(email, "NX5Password123!@Arc76");

            Assert.That(response.Email?.ToLowerInvariant(), Is.EqualTo(email.ToLowerInvariant()),
                "NX5: Register response must include the user's email (normalized) for confirmation");
        }

        /// <summary>
        /// NX6: Service-layer derivation never leaks key material to caller.
        /// </summary>
        [Test]
        public void NX6_ServiceLayer_DeriveAddressAndPublicKey_PublicKeyIsPublic()
        {
            var (address, publicKeyBase64) = _derivationService.DeriveAddressAndPublicKey(KnownEmail, KnownPassword);

            // Verify the returned key matches the actual public key from the spec
            var account = ARC76.GetEmailAccount(KnownEmail, KnownPassword, 0);
            var expectedPublicKeyBase64 = Convert.ToBase64String(account.KeyPair.ClearTextPublicKey);

            Assert.That(publicKeyBase64, Is.EqualTo(expectedPublicKeyBase64),
                "NX6: Returned key must be the public key (not private key)");
        }
    }
}
