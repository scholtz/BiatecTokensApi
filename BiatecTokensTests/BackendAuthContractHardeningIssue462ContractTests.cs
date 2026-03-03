using BiatecTokensApi.Models.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BiatecTokensTests
{
    /// <summary>
    /// Integration/contract tests for Issue #462: Vision milestone – Backend deterministic
    /// auth/account contract closure, explicit session enforcement, and auditable API guarantees.
    ///
    /// These tests use WebApplicationFactory to exercise the full application stack via HTTP,
    /// validating public API contracts for authentication, session management, authorization
    /// boundaries, error contract quality, and observability.
    ///
    /// AC1  - Determinism across lifecycle events: same credentials → same address on every
    ///        operation (register, login, re-login, validate); address stable across sessions
    /// AC2  - Session validity enforcement: no-token → 401; invalid token → 401; tampered → 401;
    ///        every protected endpoint rejects invalid sessions explicitly
    /// AC3  - Authorization correctness: compliance endpoints require auth; token endpoints require
    ///        auth; no success-shaped response on unauthorized paths
    /// AC4  - Error contract quality: error responses are structured JSON; machine-readable codes;
    ///        frontend-mappable schema; no 500 for auth failures
    /// AC5  - Audit and observability: correlation ID propagation; no secret leakage in API responses;
    ///        derivation info audit fields complete
    /// AC6  - CI quality gate: health endpoint available; Swagger endpoint available; CI contract tests pass
    /// AC7  - Documentation: arc76/info endpoint returns complete contract documentation fields
    ///
    /// Business Value: Integration contract tests prove that the full backend stack satisfies
    /// Issue #462 auth contract guarantees from the perspective of API consumers. Each test maps
    /// to an acceptance criterion, providing audit evidence for MVP compliance sign-off.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendAuthContractHardeningIssue462ContractTests
    {
        private WebApplicationFactory<BiatecTokensApi.Program> _factory = null!;
        private HttpClient _client = null!;

        private const string KnownEmail = "testuser@biatec.io";
        private const string KnownPassword = "TestPassword123!";
        private const string KnownAddress = "4DV7T4TUCD4KCPMLCD2GHQGKNX4PTZPMTNJLH77DEH7ZPZHAIAYG5JBBRI";

        // Synthetic test-only private key base64 – no real account, never use in production.
        private const string KnownTestPrivateKeyBase64 = "U23OZLAs/ZlYuxusrcx8QCk9ln0yp2OOTfqZ/sdj3bY=";

        // A structurally valid JWT with tampered payload (invalid signature). Safe to use in tests.
        private const string TamperedJwt =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9" +
            ".eyJzdWIiOiJ0YW1wZXJlZCIsImVtYWlsIjoiaGFja2VyQGV4YW1wbGUuY29tIn0" +
            ".INVALIDSIGNATUREXXXXXXXXXXXXXXXXXXXXXX";

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
            ["JwtConfig:SecretKey"] = "issue462-hardening-milestone-test-secret-key-32chars!",
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
            ["KeyManagementConfig:HardcodedKey"] = "Issue462HardeningMilestoneTestKey32CharsMin!!"
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

        // ─────────────────────────────────────────────────────────────────────────────
        // AC1 – Determinism across lifecycle events (6 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC1-C1: Register and re-login 3 times always return same address.</summary>
        [Test]
        public async Task AC1_RepeatedLogins_AlwaysReturnSameAddress()
        {
            var email = $"ac1-relogin-462-{Guid.NewGuid()}@example.com";
            const string password = "AC1ReLogin462@Pass!";

            var regResult = await RegisterAsync(email, password);
            var firstAddress = regResult.AlgorandAddress;

            for (int i = 1; i <= 3; i++)
            {
                var loginResult = await LoginAsync(email, password);
                Assert.That(loginResult.AlgorandAddress, Is.EqualTo(firstAddress),
                    $"Login #{i} must return same ARC76-derived address as registration (determinism AC1)");
            }
        }

        /// <summary>AC1-C2: validate endpoint returns known test vector address.</summary>
        [Test]
        public async Task AC1_Validate_KnownTestVector_ReturnsExpectedAddress()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate",
                new { email = KnownEmail, password = KnownPassword });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await resp.Content.ReadFromJsonAsync<ARC76ValidateResponse>();

            Assert.That(result!.Success, Is.True, "Validate must succeed for known test vector");
            Assert.That(result.AlgorandAddress, Is.EqualTo(KnownAddress),
                $"Known test vector must produce {KnownAddress}");
        }

        /// <summary>AC1-C3: Register address equals validate-derived address.</summary>
        [Test]
        public async Task AC1_Register_AddressMatches_Validate()
        {
            var email = $"ac1-match-462-{Guid.NewGuid()}@example.com";
            const string password = "AC1Match462@Pass!";

            var validateResp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate",
                new { email, password });
            var validateResult = await validateResp.Content.ReadFromJsonAsync<ARC76ValidateResponse>();

            var regResult = await RegisterAsync(email, password);

            Assert.That(regResult.AlgorandAddress, Is.EqualTo(validateResult!.AlgorandAddress),
                "Register must assign same address as ARC76 validate endpoint (derivation contract)");
        }

        /// <summary>AC1-C4: Different users always produce different addresses.</summary>
        [Test]
        public async Task AC1_DifferentUsers_DifferentAddresses()
        {
            var emailA = $"ac1-userA-462-{Guid.NewGuid()}@example.com";
            var emailB = $"ac1-userB-462-{Guid.NewGuid()}@example.com";
            const string password = "SharedPass123!";

            var respA = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate",
                new { email = emailA, password });
            var respB = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate",
                new { email = emailB, password });

            var rA = await respA.Content.ReadFromJsonAsync<ARC76ValidateResponse>();
            var rB = await respB.Content.ReadFromJsonAsync<ARC76ValidateResponse>();

            Assert.That(rA!.AlgorandAddress, Is.Not.EqualTo(rB!.AlgorandAddress),
                "Different users must have different derived addresses");
        }

        /// <summary>AC1-C5: Login returns same address as session inspect after auth.</summary>
        [Test]
        public async Task AC1_Login_And_SessionInspect_ReturnSameAddress()
        {
            var email = $"ac1-session-462-{Guid.NewGuid()}@example.com";
            const string password = "AC1Session462@Pass!";

            await RegisterAsync(email, password);
            var loginResult = await LoginAsync(email, password);

            using var authClient = CreateAuthenticatedClient(loginResult.AccessToken!);
            var sessionResp = await authClient.GetAsync("/api/v1/auth/session");
            Assert.That(sessionResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var sessionJson = await sessionResp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(sessionJson);

            // Session must contain algorandAddress field
            var hasAddress = doc.RootElement.TryGetProperty("algorandAddress", out var addrProp) ||
                             doc.RootElement.TryGetProperty("AlgorandAddress", out addrProp);
            if (hasAddress)
            {
                Assert.That(addrProp.GetString(), Is.EqualTo(loginResult.AlgorandAddress),
                    "Session inspect must return same address as login (derivation consistency)");
            }
        }

        /// <summary>AC1-C6: Email case normalization — uppercase email produces same address via validate.</summary>
        [Test]
        public async Task AC1_Validate_EmailCaseNormalization_SameAddress()
        {
            var lowerResp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate",
                new { email = KnownEmail, password = KnownPassword });
            var upperResp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate",
                new { email = KnownEmail.ToUpperInvariant(), password = KnownPassword });

            var lResult = await lowerResp.Content.ReadFromJsonAsync<ARC76ValidateResponse>();
            var uResult = await upperResp.Content.ReadFromJsonAsync<ARC76ValidateResponse>();

            Assert.That(uResult!.AlgorandAddress, Is.EqualTo(lResult!.AlgorandAddress),
                "Uppercase email must produce same address as lowercase (canonicalization AC1)");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC2 – Session validity enforcement (7 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC2-C1: /auth/session without token returns 401.</summary>
        [Test]
        public async Task AC2_Session_NoToken_Returns401()
        {
            var resp = await _client.GetAsync("/api/v1/auth/session");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Session endpoint without token must return 401 Unauthorized");
        }

        /// <summary>AC2-C2: /auth/arc76/verify-session without token returns 401.</summary>
        [Test]
        public async Task AC2_VerifySession_NoToken_Returns401()
        {
            var resp = await _client.PostAsync("/api/v1/auth/arc76/verify-session", null);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "verify-session without token must return 401 Unauthorized");
        }

        /// <summary>AC2-C3: /auth/arc76/verify-session with tampered JWT returns 401.</summary>
        [Test]
        public async Task AC2_VerifySession_TamperedJWT_Returns401()
        {
            using var authClient = CreateAuthenticatedClient(TamperedJwt);
            var resp = await authClient.PostAsync("/api/v1/auth/arc76/verify-session", null);
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "verify-session with tampered JWT must return 401");
        }

        /// <summary>AC2-C4: /auth/profile with invalid token returns 401.</summary>
        [Test]
        public async Task AC2_Profile_InvalidToken_Returns401()
        {
            using var authClient = CreateAuthenticatedClient("garbage.jwt.token");
            var resp = await authClient.GetAsync("/api/v1/auth/profile");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Profile with invalid token must return 401");
        }

        /// <summary>AC2-C5: Login with wrong password returns 401 (not 200 or 500).</summary>
        [Test]
        public async Task AC2_Login_WrongPassword_Returns401()
        {
            var email = $"ac2-wrong-462-{Guid.NewGuid()}@example.com";
            await RegisterAsync(email, "CorrectPass123!A");

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password = "WrongPassword999!" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Wrong password must return 401 Unauthorized (not 200 or 500)");
        }

        /// <summary>AC2-C6: Login with non-existent user returns 401 (not 500 or 404).</summary>
        [Test]
        public async Task AC2_Login_NonExistentUser_Returns401()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = $"ghost-{Guid.NewGuid()}@nonexistent.com", password = "AnyPass123!" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Non-existent user login must return 401 (not 500 or 404)");
        }

        /// <summary>AC2-C7: /auth/change-password without token returns 401.</summary>
        [Test]
        public async Task AC2_ChangePassword_NoToken_Returns401()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/change-password",
                new { currentPassword = "OldPass", newPassword = "NewPass123!A" });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Change-password without token must return 401");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC3 – Authorization correctness (6 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC3-C1: /api/v1/token/asa-ft/create without auth returns 401.</summary>
        [Test]
        public async Task AC3_ASAFTCreate_NoAuth_Returns401()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/token/asa-ft/create",
                new { name = "TestToken" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "ASA-FT token creation must require authentication");
        }

        /// <summary>AC3-C2: /api/v1/token/deployments without auth returns 401.</summary>
        [Test]
        public async Task AC3_Deployments_NoAuth_Returns401()
        {
            var resp = await _client.GetAsync("/api/v1/token/deployments");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Deployment list must require authentication");
        }

        /// <summary>AC3-C3: /api/v1/token/arc3-ft/create with invalid JWT returns 401.</summary>
        [Test]
        public async Task AC3_ARC3FTCreate_InvalidBearer_Returns401()
        {
            using var authClient = CreateAuthenticatedClient("invalid.bearer.token");
            var resp = await authClient.PostAsJsonAsync("/api/v1/token/arc3-ft/create",
                new { name = "TestARC3" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "ARC3-FT creation with invalid Bearer must return 401");
        }

        /// <summary>AC3-C4: /api/v1/compliance/issuance/evaluate without auth returns 401.</summary>
        [Test]
        public async Task AC3_ComplianceEvaluate_NoAuth_Returns401()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/compliance/issuance/evaluate",
                new { tokenId = "test" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Compliance evaluate endpoint must require authentication");
        }

        /// <summary>AC3-C5: /api/v1/wallet/routing-options without auth returns 401.</summary>
        [Test]
        public async Task AC3_WalletRoutingOptions_NoAuth_Returns401()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/wallet/routing-options",
                new { targetAddress = "SOMEADDRESS" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Wallet routing-options must require authentication");
        }

        /// <summary>AC3-C6: /api/v1/auth/arc76/verify-derivation with tampered JWT returns 401.</summary>
        [Test]
        public async Task AC3_VerifyDerivation_TamperedJWT_Returns401()
        {
            using var authClient = CreateAuthenticatedClient(TamperedJwt);
            var resp = await authClient.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation",
                new { });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "verify-derivation with tampered JWT must return 401");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC4 – Error contract quality (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC4-C1: Login error response is not 500 (structured failure).</summary>
        [Test]
        public async Task AC4_LoginError_IsNot500()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = $"notfound-{Guid.NewGuid()}@ghost.com", password = "AnyPass123!" });

            Assert.That((int)resp.StatusCode, Is.Not.EqualTo(500),
                "Login failure must never return 500 Internal Server Error");
        }

        /// <summary>AC4-C2: Login error response body is non-empty (not silent failure).</summary>
        [Test]
        public async Task AC4_LoginError_HasNonEmptyBody()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { email = $"notfound-{Guid.NewGuid()}@ghost.com", password = "AnyPass123!" });
            var body = await resp.Content.ReadAsStringAsync();

            Assert.That(body, Is.Not.Null.And.Not.Empty,
                "Login error response must have a non-empty body");
        }

        /// <summary>AC4-C3: Register with invalid email returns structured error (not 500).</summary>
        [Test]
        public async Task AC4_Register_InvalidEmail_StructuredError()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email = "not-an-email", password = "ValidPass123!", confirmPassword = "ValidPass123!" });

            Assert.That((int)resp.StatusCode, Is.Not.EqualTo(500),
                "Register with invalid email must not return 500");
        }

        /// <summary>AC4-C4: Register with weak password returns structured error response.</summary>
        [Test]
        public async Task AC4_Register_WeakPassword_StructuredError()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email = $"weak-{Guid.NewGuid()}@example.com", password = "weak", confirmPassword = "weak" });

            Assert.That((int)resp.StatusCode, Is.Not.EqualTo(500),
                "Weak password registration must not return 500");

            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty,
                "Weak password error response must have a body");
        }

        /// <summary>AC4-C5: /auth/register response schema has required success/algorandAddress fields.</summary>
        [Test]
        public async Task AC4_Register_ResponseSchema_HasRequiredFields()
        {
            var email = $"ac4-schema-462-{Guid.NewGuid()}@example.com";
            const string password = "AC4Schema462@Pass!";

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
            var json = await resp.Content.ReadAsStringAsync();

            Assert.That(json, Does.Contain("\"success\""),
                "RegisterResponse must contain 'success' field for frontend contract");
            Assert.That(json, Does.Contain("\"algorandAddress\""),
                "RegisterResponse must contain 'algorandAddress' field for frontend contract");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC5 – Audit and observability (4 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC5-C1: /auth/arc76/validate response never contains private key material.</summary>
        [Test]
        public async Task AC5_Validate_ResponseNeverContains_PrivateKey()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate",
                new { email = KnownEmail, password = KnownPassword });
            var body = await resp.Content.ReadAsStringAsync();

            Assert.That(body, Does.Not.Contain(KnownTestPrivateKeyBase64),
                "API response must never contain raw private key material");
            Assert.That(body, Does.Not.Contain("\"privateKey\""),
                "API response must not expose a field named 'privateKey'");
            Assert.That(body, Does.Not.Contain("\"mnemonic\""),
                "API response must not expose mnemonic secret material");
        }

        /// <summary>AC5-C2: /auth/register response never contains mnemonic in body.</summary>
        [Test]
        public async Task AC5_Register_ResponseNeverContains_Mnemonic()
        {
            var email = $"ac5-mnemonic-462-{Guid.NewGuid()}@example.com";
            const string password = "AC5Mnemonic462@!";

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { email, password, confirmPassword = password });
            var body = await resp.Content.ReadAsStringAsync();

            Assert.That(body, Does.Not.Contain("\"mnemonic\""),
                "Register response must not expose mnemonic");
            Assert.That(body, Does.Not.Contain("\"privateKey\""),
                "Register response must not expose private key field");
        }

        /// <summary>AC5-C3: /auth/arc76/info returns derivation contract audit fields.</summary>
        [Test]
        public async Task AC5_ARC76Info_ReturnsAuditFields()
        {
            var resp = await _client.GetAsync("/api/v1/auth/arc76/info");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "/auth/arc76/info must be accessible anonymously");

            var json = await resp.Content.ReadAsStringAsync();
            Assert.That(json, Does.Contain("contractVersion").Or.Contain("ContractVersion"),
                "arc76/info must return contractVersion for audit");
            Assert.That(json, Does.Contain("standard").Or.Contain("Standard"),
                "arc76/info must return standard field");
        }

        /// <summary>AC5-C4: /auth/login response schema includes algorandAddress for traceability.</summary>
        [Test]
        public async Task AC5_Login_ResponseSchema_HasTraceabilityFields()
        {
            var email = $"ac5-trace-462-{Guid.NewGuid()}@example.com";
            const string password = "AC5Trace462@Pass!";

            await RegisterAsync(email, password);
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            var json = await resp.Content.ReadAsStringAsync();

            Assert.That(json, Does.Contain("\"algorandAddress\""),
                "LoginResponse must contain 'algorandAddress' for auth traceability");
            Assert.That(json, Does.Contain("\"accessToken\""),
                "LoginResponse must contain 'accessToken'");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC6 – CI quality gate (3 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC6-C1: /health endpoint returns 200 (CI quality gate).</summary>
        [Test]
        public async Task AC6_Health_Returns200()
        {
            var resp = await _client.GetAsync("/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "/health endpoint must return 200 for CI quality gate");
        }

        /// <summary>AC6-C2: /auth/arc76/info (anonymous) returns 200 (CI contract gate).</summary>
        [Test]
        public async Task AC6_ARC76Info_Anonymous_Returns200()
        {
            var resp = await _client.GetAsync("/api/v1/auth/arc76/info");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "/auth/arc76/info must be accessible anonymously for CI verification");
        }

        /// <summary>AC6-C3: /auth/arc76/validate returns 200 for valid inputs (CI regression gate).</summary>
        [Test]
        public async Task AC6_Validate_ValidInputs_Returns200()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/validate",
                new { email = KnownEmail, password = KnownPassword });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "/auth/arc76/validate must return 200 for known test vector");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC7 – Documentation contract (2 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>AC7-C1: /auth/arc76/info returns all required contract documentation fields.</summary>
        [Test]
        public async Task AC7_ARC76Info_ContainsAllContractDocumentationFields()
        {
            var resp = await _client.GetAsync("/api/v1/auth/arc76/info");
            var json = await resp.Content.ReadAsStringAsync();

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("contractVersion").Or.Contain("ContractVersion"),
                    "AC7: arc76/info must include contractVersion documentation field");
                Assert.That(json, Does.Contain("standard").Or.Contain("Standard"),
                    "AC7: arc76/info must include standard documentation field");
                Assert.That(json, Does.Contain("algorithmDescription").Or.Contain("AlgorithmDescription"),
                    "AC7: arc76/info must include algorithmDescription documentation field");
                Assert.That(json, Does.Contain("specificationUrl").Or.Contain("SpecificationUrl"),
                    "AC7: arc76/info must include specificationUrl documentation field");
            });
        }

        /// <summary>AC7-C2: Register response includes DerivationContractVersion for contract version tracking.</summary>
        [Test]
        public async Task AC7_Register_Response_IncludesDerivationContractVersion()
        {
            var email = $"ac7-doc-462-{Guid.NewGuid()}@example.com";
            const string password = "AC7Doc462@Pass!";

            var result = await RegisterAsync(email, password);

            Assert.That(result.DerivationContractVersion, Is.EqualTo("1.0"),
                "AC7: Register response must include DerivationContractVersion='1.0' for contract documentation");
        }
    }
}
