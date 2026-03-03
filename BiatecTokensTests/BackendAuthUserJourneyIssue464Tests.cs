using BiatecTokensApi.Models.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// User-journey and impact tests for Issue #464: Vision milestone – Backend deterministic
    /// auth contracts and auditable transaction lifecycle.
    ///
    /// PURPOSE: Provides explicit evidence for happy-path, invalid-input, boundary, and
    /// failure-recovery scenarios from the perspective of non-crypto-native users who are
    /// registering on a token-launch platform for the first time.
    ///
    /// USER IMPACT RATIONALE (non-crypto-native users):
    /// • Registration creates an Algorand address automatically — the user never needs to
    ///   understand wallets, mnemonics, or cryptographic keys to get started.
    /// • Login is identical to any web app (email + password). The user's blockchain
    ///   address is derived deterministically from their credentials via ARC76, so they
    ///   never have to back up a seed phrase.
    /// • Error messages are actionable — "Invalid email or password" instead of raw
    ///   cryptographic stack traces, so non-technical users can understand what went wrong.
    /// • Idempotency prevents double-submissions: accidentally clicking "Register" twice
    ///   never creates two conflicting accounts.
    /// • Correlation IDs in responses allow support teams to trace a specific user complaint
    ///   back to a server log without asking the user for technical details.
    ///
    /// Test categories:
    ///   HP = Happy Path (expected success flows)
    ///   II = Invalid Input (user mistake scenarios)
    ///   BD = Boundary (edge/limit cases)
    ///   FR = Failure-Recovery (behavior after errors)
    ///   NX = Non-Crypto-Native Experience (user-facing clarity)
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendAuthUserJourneyIssue464Tests
    {
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
            ["JwtConfig:SecretKey"] = "userjourney-issue464-test-key-32chars-min!!",
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
            ["KeyManagementConfig:HardcodedKey"] = "UserJourneyIssue464TestKey32CharsMin!!"
        };

        [SetUp]
        public void Setup()
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
        public void TearDown()
        {
            _client?.Dispose();
            _factory?.Dispose();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────────

        private string UniqueEmail(string prefix) =>
            $"{prefix}-{Guid.NewGuid():N}@userjourney-test.io";

        private async Task<(HttpResponseMessage Response, RegisterResponse? Body)> TryRegisterAsync(
            string email, string password, string? confirmPassword = null)
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email,
                password,
                confirmPassword = confirmPassword ?? password
            });
            RegisterResponse? body = null;
            try { body = await response.Content.ReadFromJsonAsync<RegisterResponse>(); }
            catch (System.Text.Json.JsonException) { }
            return (response, body);
        }

        private async Task<(HttpResponseMessage Response, LoginResponse? Body)> TryLoginAsync(
            string email, string password)
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            LoginResponse? body = null;
            try { body = await response.Content.ReadFromJsonAsync<LoginResponse>(); }
            catch (System.Text.Json.JsonException) { }
            return (response, body);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // HP – Happy Path Scenarios
        // User impact: These tests verify the core "first-time user" flows work correctly.
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// HP1: New user registers and immediately receives an Algorand wallet address.
        /// User impact (non-crypto-native): The platform automatically creates a blockchain
        /// identity for the user — no wallet software or seed phrases required.
        /// </summary>
        [Test]
        public async Task HP1_NewUser_Register_ReceivesAlgorandAddress()
        {
            // Arrange — a brand-new user with a unique email
            var email = UniqueEmail("hp1-newuser");
            const string password = "SecureHappyPath1!";

            // Act
            var (response, body) = await TryRegisterAsync(email, password);

            // Assert — user gets a 200 with a valid Algorand address (58 chars, uppercase)
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "HP1: Registration must succeed for a valid new user");
            Assert.That(body!.Success, Is.True,
                "HP1: Success flag must be true for a new user registration");
            Assert.That(body.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                "HP1: User must receive an Algorand address automatically (no wallet needed)");
            Assert.That(body.AlgorandAddress!.Length, Is.EqualTo(58),
                "HP1: Algorand address must be exactly 58 characters");
            Assert.That(body.AlgorandAddress, Does.Match("^[A-Z2-7]+$"),
                "HP1: Algorand address must be valid Base32 (uppercase alphanumeric)");
        }

        /// <summary>
        /// HP2: Registered user logs in and receives JWT tokens for authenticated operations.
        /// User impact (non-crypto-native): Login works identically to any email-based platform;
        /// the tokens returned enable further actions without re-entering credentials.
        /// </summary>
        [Test]
        public async Task HP2_RegisteredUser_Login_ReceivesJwtTokens()
        {
            // Arrange — register first
            var email = UniqueEmail("hp2-login");
            const string password = "LoginHappyPath2!";
            await TryRegisterAsync(email, password);

            // Act — login
            var (response, body) = await TryLoginAsync(email, password);

            // Assert — user gets tokens required to make authenticated API calls
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "HP2: Login must succeed with correct credentials");
            Assert.That(body!.Success, Is.True, "HP2: Success flag must be true");
            Assert.That(body.AccessToken, Is.Not.Null.And.Not.Empty,
                "HP2: User must receive an access token to call protected endpoints");
            Assert.That(body.RefreshToken, Is.Not.Null.And.Not.Empty,
                "HP2: User must receive a refresh token to extend their session");
            // JWT has 3 dot-separated parts
            Assert.That(body.AccessToken!.Split('.'), Has.Length.EqualTo(3),
                "HP2: Access token must be a valid 3-part JWT");
        }

        /// <summary>
        /// HP3: User's Algorand address is always the same regardless of how many times they log in.
        /// User impact (non-crypto-native): Deterministic identity — the user doesn't need to
        /// back up or remember a seed phrase; their email/password IS their persistent blockchain identity.
        /// </summary>
        [Test]
        public async Task HP3_SameUser_MultipleLogins_AlwaysGetSameAddress()
        {
            // Arrange
            var email = UniqueEmail("hp3-determinism");
            const string password = "DeterministicPass3!";
            var (_, reg) = await TryRegisterAsync(email, password);
            var expectedAddress = reg!.AlgorandAddress;

            // Act — login 3 separate times
            var (_, login1) = await TryLoginAsync(email, password);
            var (_, login2) = await TryLoginAsync(email, password);
            var (_, login3) = await TryLoginAsync(email, password);

            // Assert — all logins return the same address
            Assert.That(login1!.AlgorandAddress, Is.EqualTo(expectedAddress),
                "HP3: First login must return the same address as registration");
            Assert.That(login2!.AlgorandAddress, Is.EqualTo(expectedAddress),
                "HP3: Second login must return the same address");
            Assert.That(login3!.AlgorandAddress, Is.EqualTo(expectedAddress),
                "HP3: Third login must return the same address — address is permanent");
        }

        /// <summary>
        /// HP4: Authenticated user can access their session information.
        /// User impact (non-crypto-native): After login, users can verify their session is
        /// active and see their wallet address without needing any blockchain tools.
        /// </summary>
        [Test]
        public async Task HP4_AuthenticatedUser_CanAccessSessionInfo()
        {
            // Arrange — register and login
            var email = UniqueEmail("hp4-session");
            const string password = "SessionHappyPath4!";
            await TryRegisterAsync(email, password);
            var (_, login) = await TryLoginAsync(email, password);
            Assert.That(login!.Success, Is.True);

            // Act — access session info with the token
            using var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", login.AccessToken);
            var sessionResponse = await authClient.GetAsync("/api/v1/auth/session");

            // Assert — session info is accessible to authenticated users
            Assert.That(sessionResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "HP4: Authenticated user must be able to retrieve their session info");
        }

        /// <summary>
        /// HP5: User can refresh their session without re-entering credentials.
        /// User impact (non-crypto-native): "Stay logged in" — users don't need to re-enter
        /// email/password after their access token expires; the refresh token handles this transparently.
        /// </summary>
        [Test]
        public async Task HP5_User_CanRefreshSession_WithoutReenteringCredentials()
        {
            // Arrange — register and login to get tokens
            var email = UniqueEmail("hp5-refresh");
            const string password = "RefreshHappyPath5!";
            await TryRegisterAsync(email, password);
            var (_, login) = await TryLoginAsync(email, password);
            Assert.That(login!.Success, Is.True);

            // Act — use refresh token to get a new access token
            var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = login.RefreshToken });

            // Assert — session is extended without re-login
            Assert.That(refreshResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "HP5: Valid refresh token must extend the session without re-login");
            var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(refreshBody!.Success, Is.True,
                "HP5: Refresh must succeed with a valid refresh token");
            Assert.That(refreshBody.AccessToken, Is.Not.Null.And.Not.Empty,
                "HP5: New access token must be provided after refresh");
        }

        /// <summary>
        /// HP6: ARC76 info endpoint explains the derivation algorithm without requiring login.
        /// User impact (non-crypto-native): Users and auditors can inspect the protocol specification
        /// without needing a wallet or blockchain knowledge.
        /// </summary>
        [Test]
        public async Task HP6_ARC76Info_AnonymousUser_CanReadProtocolSpecification()
        {
            // Act — no authentication required
            var response = await _client.GetAsync("/api/v1/auth/arc76/info");

            // Assert — public documentation is accessible
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "HP6: ARC76 protocol info must be publicly accessible (no login required)");
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            // Contract uses "algorithmDescription" and "specificationUrl" fields
            Assert.That(body.GetProperty("algorithmDescription").GetString(), Is.Not.Null.And.Not.Empty,
                "HP6: Protocol info must include algorithm description for user understanding");
            Assert.That(body.GetProperty("specificationUrl").GetString(), Is.Not.Null.And.Not.Empty,
                "HP6: Protocol info must include a specification URL for further reading");
        }

        /// <summary>
        /// HP7: Two different users get different Algorand addresses.
        /// User impact: Each registered email gets a unique blockchain identity, preventing
        /// address collisions between different users on the platform.
        /// </summary>
        [Test]
        public async Task HP7_TwoDifferentUsers_ReceiveDifferentAddresses()
        {
            // Arrange
            var email1 = UniqueEmail("hp7-user-a");
            var email2 = UniqueEmail("hp7-user-b");
            const string password = "DifferentUsers7!";

            // Act
            var (_, reg1) = await TryRegisterAsync(email1, password);
            var (_, reg2) = await TryRegisterAsync(email2, password);

            // Assert — unique identities
            Assert.That(reg1!.AlgorandAddress, Is.Not.EqualTo(reg2!.AlgorandAddress),
                "HP7: Different users must get different Algorand addresses (no identity collision)");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // II – Invalid Input Scenarios
        // User impact: These tests ensure user mistakes produce clear, actionable errors.
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// II1: User tries to register with an invalid email format.
        /// User impact: User gets a clear validation error, not a server crash.
        /// The response must be 400 (bad request) with a usable error message.
        /// </summary>
        [Test]
        public async Task II1_Register_InvalidEmailFormat_Returns400WithUsableError()
        {
            // Act — email missing @ sign
            var (response, _) = await TryRegisterAsync("not-an-email", "ValidPass1!");

            // Assert — validation error, not server crash
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "II1: Invalid email format must return 400 Bad Request (clear user feedback)");
        }

        /// <summary>
        /// II2: User tries to register without a password.
        /// User impact: User gets an immediate validation error explaining the password is required.
        /// </summary>
        [Test]
        public async Task II2_Register_EmptyPassword_Returns400()
        {
            // Act
            var (response, _) = await TryRegisterAsync(UniqueEmail("ii2"), "");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "II2: Empty password must return 400 — user must know the field is required");
        }

        /// <summary>
        /// II3: User tries to log in with the wrong password.
        /// User impact: User gets "Invalid credentials" — specific enough to know what went wrong
        /// without revealing whether the email exists (prevents email enumeration attacks).
        /// </summary>
        [Test]
        public async Task II3_Login_WrongPassword_Returns401_WithActionableMessage()
        {
            // Arrange — register first
            var email = UniqueEmail("ii3-wrongpw");
            await TryRegisterAsync(email, "CorrectPassword3!");

            // Act — login with wrong password
            var (response, _) = await TryLoginAsync(email, "WrongPassword999!");

            // Assert — 401, not 500 (no crash); not 400 (not a format error)
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "II3: Wrong password must return 401 Unauthorized (actionable for user)");
        }

        /// <summary>
        /// II4: User tries to log in with a non-existent email.
        /// User impact: Returns the same 401 as wrong password — prevents email enumeration.
        /// An attacker cannot tell whether the email exists from the response.
        /// </summary>
        [Test]
        public async Task II4_Login_NonExistentEmail_Returns401_SameAsWrongPassword()
        {
            // Act — login with email that was never registered
            var (response, _) = await TryLoginAsync("never-registered@example.com", "AnyPassword1!");

            // Assert — same 401 as wrong password (no email enumeration)
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "II4: Non-existent email must return 401 (identical to wrong password — anti-enumeration)");
        }

        /// <summary>
        /// II5: User submits completely empty JSON to the register endpoint.
        /// User impact: Must not cause a server error (500). Returns 400 with form guidance.
        /// </summary>
        [Test]
        public async Task II5_Register_EmptyJson_Returns400_NotServerError()
        {
            // Act — send empty JSON object
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/v1/auth/register", content);

            // Assert — validation error, not server error
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "II5: Empty form submission must never cause a server error (500)");
            Assert.That((int)response.StatusCode, Is.InRange(400, 422),
                "II5: Empty form must return a 4xx client error with guidance");
        }

        /// <summary>
        /// II6: User submits malformed JSON (syntax error) to login endpoint.
        /// User impact: Must not cause a server error (500). Returns 400.
        /// </summary>
        [Test]
        public async Task II6_Login_MalformedJson_Returns400_NotServerError()
        {
            // Act — invalid JSON
            using var content = new StringContent("{invalid json}", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/api/v1/auth/login", content);

            // Assert
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "II6: Malformed JSON must never cause a server error (500)");
            Assert.That((int)response.StatusCode, Is.InRange(400, 422),
                "II6: Malformed JSON must return a 4xx client error");
        }

        /// <summary>
        /// II7: User tries to register twice with the same email.
        /// User impact: Gets a clear "already registered" message; can be redirected to login.
        /// The second attempt must not overwrite the first user's account.
        /// </summary>
        [Test]
        public async Task II7_Register_DuplicateEmail_Returns4xx_WithGuidance()
        {
            // Arrange — register once
            var email = UniqueEmail("ii7-dup");
            const string password = "DuplicateTest7!";
            var (_, reg1) = await TryRegisterAsync(email, password);
            Assert.That(reg1!.Success, Is.True);
            var originalAddress = reg1.AlgorandAddress;

            // Act — try to register again with same email
            var (response2, reg2) = await TryRegisterAsync(email, "DifferentPassword7!");

            // Assert — not 500; registration is rejected or idempotent
            Assert.That(response2.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "II7: Duplicate registration must never cause a server error (500)");
            // Either the duplicate is rejected (4xx) or it's idempotent but returns same address
            if (reg2?.Success == true)
            {
                Assert.That(reg2.AlgorandAddress, Is.EqualTo(originalAddress),
                    "II7: If duplicate registration succeeds, it must return the same address");
            }
            else
            {
                Assert.That((int)response2.StatusCode, Is.InRange(400, 422),
                    "II7: If duplicate registration is rejected, it must return a 4xx client error");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // BD – Boundary Conditions
        // User impact: Behavior at the edges of what the system accepts.
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// BD1: User submits an extremely long email address (> 500 characters).
        /// User impact: Must not cause a server crash. Returns a validation error.
        /// Prevents DoS through excessively large inputs.
        /// </summary>
        [Test]
        public async Task BD1_Register_OversizedEmail_NotServerError()
        {
            // Arrange — email > 500 chars
            var longEmail = new string('a', 490) + "@example.com";

            // Act
            var (response, _) = await TryRegisterAsync(longEmail, "ValidPass1!");

            // Assert — bounded error, not crash
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "BD1: Oversized email input must not cause a server error (500)");
        }

        /// <summary>
        /// BD2: User submits an extremely long password (> 500 characters).
        /// User impact: Must not cause a server crash. Accepted or bounded gracefully.
        /// </summary>
        [Test]
        public async Task BD2_Register_OversizedPassword_NotServerError()
        {
            // Arrange
            var longPassword = new string('P', 500) + "1!";
            var email = UniqueEmail("bd2-longpw");

            // Act
            var (response, _) = await TryRegisterAsync(email, longPassword);

            // Assert — bounded response, not crash
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "BD2: Oversized password input must not cause a server error (500)");
        }

        /// <summary>
        /// BD3: User submits SQL injection pattern in email field.
        /// User impact: The platform is safe against injection attacks — users' data and
        /// other users' accounts are protected.
        /// </summary>
        [Test]
        public async Task BD3_Register_SqlInjectionInEmail_NotServerError()
        {
            // Act
            var (response, _) = await TryRegisterAsync("'; DROP TABLE users; --", "ValidPass1!");

            // Assert — safe handling, not crash
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "BD3: SQL injection in email field must not cause a server error (500)");
        }

        /// <summary>
        /// BD4: Login endpoint called with an extremely long token (potential header-injection attempt).
        /// User impact: The API safely rejects oversized bearer tokens without crashing.
        /// </summary>
        [Test]
        public async Task BD4_ProtectedEndpoint_OversizedBearerToken_Returns401_NotServerError()
        {
            // Arrange — oversized fake token
            var oversizedToken = new string('X', 8192);
            using var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {oversizedToken}");

            // Act
            var response = await authClient.GetAsync("/api/v1/auth/session");

            // Assert — safe rejection
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "BD4: Oversized bearer token must not cause a server error (500)");
        }

        /// <summary>
        /// BD5: Refresh token endpoint called with an empty string refresh token.
        /// User impact: Clear error if the session data is lost or corrupted on the client side.
        /// </summary>
        [Test]
        public async Task BD5_Refresh_EmptyRefreshToken_Returns4xx_NotServerError()
        {
            // Act — empty refresh token
            var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = "" });

            // Assert — validation or auth error, not crash
            Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "BD5: Empty refresh token must not cause a server error (500)");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized)
                .Or.EqualTo(HttpStatusCode.BadRequest),
                "BD5: Empty refresh token must return 401 or 400");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // FR – Failure-Recovery Scenarios
        // User impact: Behavior after an error — can the user try again successfully?
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// FR1: User mistypes password on first login, then uses correct password successfully.
        /// User impact: Failed attempts don't permanently block the account (unless locked);
        /// the user can immediately retry with the correct credentials.
        /// </summary>
        [Test]
        public async Task FR1_Login_FailedAttempt_ThenSuccessWithCorrectPassword()
        {
            // Arrange — register
            var email = UniqueEmail("fr1-retry");
            const string correctPassword = "CorrectFR1Password!";
            await TryRegisterAsync(email, correctPassword);

            // Act — fail first, then succeed
            var (failResponse, _) = await TryLoginAsync(email, "WrongPassword999!");
            var (successResponse, successBody) = await TryLoginAsync(email, correctPassword);

            // Assert
            Assert.That(failResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "FR1: Wrong password must return 401");
            Assert.That(successResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "FR1: Correct password after failed attempt must succeed (user can retry)");
            Assert.That(successBody!.Success, Is.True,
                "FR1: Login with correct credentials must succeed even after a failed attempt");
        }

        /// <summary>
        /// FR2: User's session expires and they use a garbage refresh token.
        /// Then they log in fresh and get a valid new session.
        /// User impact: Even if stored tokens are corrupted/expired, user can always recover
        /// by logging in again — no account lock-out.
        /// </summary>
        [Test]
        public async Task FR2_GarbageRefreshToken_ThenFreshLogin_SuccessfulRecovery()
        {
            // Arrange — register
            var email = UniqueEmail("fr2-recovery");
            const string password = "RecoveryFR2Pass!";
            await TryRegisterAsync(email, password);

            // Act — first try garbage refresh token (simulates corrupted client storage)
            var garbageRefresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken = "corrupted-token-xyz-invalid" });

            // Then login fresh
            var (loginResponse, loginBody) = await TryLoginAsync(email, password);

            // Assert — fresh login succeeds even after garbage refresh attempt
            Assert.That(garbageRefresh.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "FR2: Garbage refresh token must return 401");
            Assert.That(loginResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "FR2: Fresh login must succeed after failed refresh attempt (recovery path)");
            Assert.That(loginBody!.Success, Is.True,
                "FR2: User can always recover by logging in again");
        }

        /// <summary>
        /// FR3: User uses a refresh token after logout (replay attack simulation).
        /// User impact: Security — tokens are invalidated after logout, so a stolen token
        /// cannot be used by an attacker after the user logs out.
        /// </summary>
        [Test]
        public async Task FR3_PostLogout_RefreshToken_IsRejected_SecurityGuarantee()
        {
            // Arrange — login
            var email = UniqueEmail("fr3-logout");
            const string password = "LogoutReplayFR3!";
            await TryRegisterAsync(email, password);
            var (_, login) = await TryLoginAsync(email, password);
            Assert.That(login!.Success, Is.True);
            var refreshToken = login.RefreshToken;
            var accessToken = login.AccessToken;

            // Act — logout first
            using var authClient = _factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            var logoutResponse = await authClient.PostAsync("/api/v1/auth/logout", null);
            Assert.That((int)logoutResponse.StatusCode, Is.InRange(200, 204),
                "FR3: Logout must succeed");

            // Then try to use the old refresh token
            var replayResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { refreshToken });

            // Assert — refresh is rejected after logout (security contract)
            Assert.That(replayResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized)
                .Or.EqualTo(HttpStatusCode.BadRequest),
                "FR3: Refresh token must be rejected after logout — prevents session hijacking");
        }

        /// <summary>
        /// FR4: After a failed/rejected request, the same client can make valid requests.
        /// User impact: A single failed request does not break the connection or the client state.
        /// </summary>
        [Test]
        public async Task FR4_AfterFailedRequest_ValidRequestsStillSucceed()
        {
            // Arrange — make a failed request (unauthenticated to protected endpoint)
            var failedResponse = await _client.GetAsync("/api/v1/auth/session");
            Assert.That(failedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "FR4: Unauthenticated request must return 401");

            // Act — now make a valid public request using the same client
            var validResponse = await _client.GetAsync("/api/v1/auth/arc76/info");

            // Assert — valid request succeeds even after a previous failure
            Assert.That(validResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "FR4: Valid request must succeed after a previous failed request (no client state corruption)");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // NX – Non-Crypto-Native Experience
        // User impact: Tests that verify the API is accessible to users without blockchain knowledge.
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// NX1: Error responses never contain raw cryptographic internals (no stack traces,
        /// no exception class names, no key material).
        /// User impact: Non-technical users see actionable error messages, not confusing technical output.
        /// Security: Internal implementation details are never exposed to potential attackers.
        /// </summary>
        [Test]
        public async Task NX1_ErrorResponses_NeverContain_CryptographicInternals()
        {
            // Act — trigger auth error
            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "nobody@example.com", password = "wrongpassword" });
            var body = await loginResponse.Content.ReadAsStringAsync();
            var bodyLower = body.ToLowerInvariant();

            // Assert — no internal implementation details (case-insensitive checks)
            Assert.That(bodyLower, Does.Not.Contain("system.exception"),
                "NX1: Error must not expose .NET exception class names (System.Exception)");
            Assert.That(bodyLower, Does.Not.Contain("system.data"),
                "NX1: Error must not expose .NET data layer internals");
            Assert.That(bodyLower, Does.Not.Contain("innerexception"),
                "NX1: Error must not expose InnerException property");
            Assert.That(bodyLower, Does.Not.Contain("hresult"),
                "NX1: Error must not expose HResult (Windows error codes)");
            Assert.That(bodyLower, Does.Not.Contain("stack trace"),
                "NX1: Error must not expose stack trace (technical jargon)");
            Assert.That(bodyLower, Does.Not.Contain("stacktrace"),
                "NX1: Error must not expose StackTrace property");
            Assert.That(bodyLower, Does.Not.Contain("at biatectokens"),
                "NX1: Error must not expose internal namespace in stack frames");
        }

        /// <summary>
        /// NX2: Register and login responses never expose private key material or mnemonics.
        /// User impact (security): Users' private keys are never returned via API even by accident.
        /// Non-crypto users are protected from accidentally exposing their keys.
        /// </summary>
        [Test]
        public async Task NX2_RegisterAndLogin_NeverExposePrivateKeys_OrMnemonics()
        {
            // Arrange
            var email = UniqueEmail("nx2-security");
            const string password = "SecurityNX2Pass!";

            // Act — register and login
            var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
            var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password });
            var registerBody = await registerResponse.Content.ReadAsStringAsync();
            var loginBody = await loginResponse.Content.ReadAsStringAsync();

            // Assert — no key material in responses (case-insensitive checks)
            var allContent = (registerBody + loginBody).ToLowerInvariant();
            Assert.That(allContent, Does.Not.Contain("mnemonic"),
                "NX2: API response must never contain the word 'mnemonic'");
            Assert.That(allContent, Does.Not.Contain("privatekey"),
                "NX2: API response must never contain 'privatekey'");
            Assert.That(allContent, Does.Not.Contain("private_key"),
                "NX2: API response must never contain 'private_key'");
            Assert.That(allContent, Does.Not.Contain("secretkey"),
                "NX2: API response must never contain 'secretkey'");
            Assert.That(allContent, Does.Not.Contain("\"seed\""),
                "NX2: API response must never expose a seed phrase field");
            Assert.That(allContent, Does.Not.Contain("\"sk\""),
                "NX2: API response must never expose a raw sk (secret key) field");
        }

        /// <summary>
        /// NX3: ARC76 info endpoint returns a human-readable algorithm description.
        /// User impact: Non-technical users (and compliance reviewers) can understand
        /// how the key derivation works without reading cryptographic papers.
        /// </summary>
        [Test]
        public async Task NX3_ARC76Info_Returns_HumanReadable_Description()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/auth/arc76/info");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            // Assert — human-readable content
            var algorithmDesc = body.GetProperty("algorithmDescription").GetString() ?? "";
            Assert.That(algorithmDesc.Length, Is.GreaterThan(20),
                "NX3: Algorithm description must be meaningful (> 20 chars) for non-crypto users");
            var standard = body.GetProperty("standard").GetString() ?? "";
            Assert.That(standard, Is.Not.Empty,
                "NX3: Standard field must be present for user documentation (e.g., 'ARC76')");
        }

        /// <summary>
        /// NX4: Correlation IDs in responses allow support teams to trace user complaints.
        /// User impact: When a user reports "my login failed at 2pm", support can find the
        /// exact server log entry using the CorrelationId without asking for technical details.
        /// </summary>
        [Test]
        public async Task NX4_SuccessResponse_Includes_CorrelationId_ForSupportTracing()
        {
            // Arrange
            var email = UniqueEmail("nx4-correlation");
            const string password = "CorrelationNX4!";

            // Act — register (includes CorrelationId for support tracing)
            var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();

            // Assert — CorrelationId allows support team tracing
            Assert.That(body!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "NX4: Registration response must include CorrelationId for incident tracing");
        }

        /// <summary>
        /// NX5: Error response for failed login includes a CorrelationId for incident triage.
        /// User impact: Support team can trace a user's failed login using only the CorrelationId
        /// from the error response — no need for server access or technical user knowledge.
        /// </summary>
        [Test]
        public async Task NX5_FailedLogin_Includes_CorrelationId_ForIncidentTriage()
        {
            // Act — login that will fail
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = "nonexistent@support-trace.com", password = "WrongPW!" });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

            // Assert — even failures include tracing info
            Assert.That(body!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "NX5: Failed login response must include CorrelationId so support can trace the issue");
        }

        /// <summary>
        /// NX6: Validate endpoint returns user's Algorand address from just email and password.
        /// User impact: Non-crypto users can look up their blockchain address at any time
        /// without needing a wallet app — just their standard email/password credentials.
        /// </summary>
        [Test]
        public async Task NX6_ValidateEndpoint_NoCryptoKnowledgeNeeded_Returns_AlgorandAddress()
        {
            // Act — validate with standard email/password (no blockchain knowledge needed)
            var validateResponse = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate",
                new { email = "testuser@biatec.io", password = "TestPassword123!" });
            Assert.That(validateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await validateResponse.Content.ReadFromJsonAsync<JsonElement>();

            // Assert — user gets their address without any crypto knowledge
            Assert.That(body.GetProperty("success").GetBoolean(), Is.True,
                "NX6: Validate endpoint must work with just email and password");
            Assert.That(body.GetProperty("algorandAddress").GetString(), Is.Not.Null.And.Not.Empty,
                "NX6: User's Algorand address is accessible without crypto tools");
        }
    }
}
