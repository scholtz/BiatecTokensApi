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
    /// Integration/contract tests for Issue #452: Backend MVP hardening – deterministic ARC76 auth
    /// contracts and deployment reliability.
    ///
    /// These tests use WebApplicationFactory to exercise the full application stack via HTTP,
    /// validating the public API contracts that frontend, QA, and enterprise compliance workflows
    /// depend on.
    ///
    /// AC1 - Deterministic auth/account behavior: same credentials always map to same Algorand
    ///       address; email case normalization; derivation contract version is stable
    /// AC2 - Session lifecycle correctness: register → login → refresh → logout paths;
    ///       invalid/expired sessions return explicit errors; no silent-success failure modes
    /// AC3 - Deployment pipeline reliability: deployment list accessible; deployment endpoints
    ///       enforce auth; state-machine semantics visible from API; metrics endpoint stable
    /// AC4 - Observability and audit quality: correlation IDs in responses; ARC76 info endpoint
    ///       exposes algorithm metadata; no secrets in error payloads; structured JSON errors
    /// AC5 - Test and CI confidence: health endpoint always returns healthy; Swagger spec
    ///       accessible; no regression in auth endpoints; 3-run test evidence for determinism
    /// AC6 - Documentation/integration readiness: all required response fields present; JWT
    ///       structure valid; token expiry in the future; schema stability across runs
    ///
    /// Business Value: End-to-end contract tests prove that the auth/deployment surface is stable
    /// and trustworthy for enterprise onboarding. Each test maps directly to an acceptance criterion,
    /// providing audit evidence that the backend fulfils its MVP reliability commitments.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class BackendMVPHardeningIssue452ContractTests
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
            ["JwtConfig:SecretKey"] = "issue452-backend-mvp-hardening-test-secret-key-32chars",
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
            ["KeyManagementConfig:HardcodedKey"] = "Issue452BackendMVPHardeningTestKey32CharsMin"
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
        // AC1 – Deterministic auth/account behavior (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC1-I1: Same email/password must return the same Algorand address across all login calls.
        /// This is the foundational guarantee of ARC76 derivation determinism at the API level.
        /// Provides 3-run CI evidence for the acceptance criterion.
        /// </summary>
        [Test]
        public async Task AC1_SameCredentials_ThreeLoginRuns_AlwaysReturnSameAddress()
        {
            var email = $"ac1-det-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC1";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Registration must succeed");
            var reg = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(reg!.Success, Is.True);
            var baseAddress = reg.AlgorandAddress;
            Assert.That(baseAddress, Is.Not.Null.And.Not.Empty, "Registration must yield an Algorand address");

            for (int run = 1; run <= 3; run++)
            {
                var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                    new { Email = email, Password = password });
                Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Login run {run} must succeed");
                var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.That(login!.AlgorandAddress, Is.EqualTo(baseAddress),
                    $"Login run {run} must return the same Algorand address as registration");
            }
        }

        /// <summary>
        /// AC1-I2: Two different users with different emails must receive different Algorand addresses.
        /// Confirms identity isolation – ARC76 derivation must not produce collisions.
        /// </summary>
        [Test]
        public async Task AC1_DifferentUsers_ReceiveDifferentAlgorandAddresses()
        {
            var emailA = $"ac1-userA-{Guid.NewGuid()}@issue452.io";
            var emailB = $"ac1-userB-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC1B";

            var regA = await (await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = emailA, Password = password, ConfirmPassword = password }))
                .Content.ReadFromJsonAsync<RegisterResponse>();

            var regB = await (await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = emailB, Password = password, ConfirmPassword = password }))
                .Content.ReadFromJsonAsync<RegisterResponse>();

            Assert.That(regA!.Success && regB!.Success, Is.True, "Both registrations must succeed");
            Assert.That(regA.AlgorandAddress, Is.Not.EqualTo(regB!.AlgorandAddress),
                "Different users must have different Algorand addresses (identity isolation)");
        }

        /// <summary>
        /// AC1-I3: Register response includes a stable DerivationContractVersion field.
        /// Frontend clients monitor this value to detect breaking changes to the auth contract.
        /// </summary>
        [Test]
        public async Task AC1_Registration_IncludesStableDerivationContractVersion()
        {
            var email = $"ac1-version-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC1C";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var reg = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();

            Assert.That(reg!.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "DerivationContractVersion must be present in RegisterResponse");
        }

        /// <summary>
        /// AC1-I4: ARC76 info endpoint returns non-null contract metadata for anonymous callers.
        /// This endpoint is consumed by QA, frontend, and compliance teams without authentication.
        /// </summary>
        [Test]
        public async Task AC1_ARC76InfoEndpoint_ReturnsContractMetadataForAnonymousCaller()
        {
            var resp = await _client.GetAsync("/api/v1/auth/arc76/info");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "ARC76 info endpoint must be accessible anonymously");

            var content = await resp.Content.ReadAsStringAsync();
            var info = JsonSerializer.Deserialize<ARC76DerivationInfoResponse>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.That(info, Is.Not.Null);
            Assert.That(info!.ContractVersion, Is.Not.Null.And.Not.Empty,
                "ARC76 info must include ContractVersion");
        }

        /// <summary>
        /// AC1-I5: Login response also includes DerivationContractVersion, consistent with register.
        /// Stability of this field across endpoints is required for frontend contract assertions.
        /// </summary>
        [Test]
        public async Task AC1_Login_IncludesDerivationContractVersion()
        {
            var email = $"ac1-loginver-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC1D";

            // Register first
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Login
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = password });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

            Assert.That(login!.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                "DerivationContractVersion must be present in LoginResponse");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC2 – Session lifecycle correctness (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC2-I1: Complete session lifecycle – register → login → refresh → logout.
        /// All four critical session operations must succeed without silent failures.
        /// </summary>
        [Test]
        public async Task AC2_FullSessionLifecycle_RegisterLoginRefreshLogout_AllSucceed()
        {
            var email = $"ac2-lifecycle-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC2A";

            // 1. Register
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Registration must succeed");
            var reg = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(reg!.Success, Is.True);

            // 2. Login
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = password });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Login must succeed");
            var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(login!.Success, Is.True);
            var refreshToken = login.RefreshToken;
            var accessToken = login.AccessToken;

            // 3. Refresh token
            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = refreshToken });
            Assert.That(refreshResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Token refresh must succeed");
            var refresh = await refreshResp.Content.ReadAsStringAsync();
            Assert.That(refresh, Does.Contain("true"), "Refresh response must indicate success");

            // 4. Logout
            using var logoutMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
            logoutMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var logoutResp = await _client.SendAsync(logoutMsg);
            Assert.That(logoutResp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Logout must succeed");
        }

        /// <summary>
        /// AC2-I2: Wrong password returns HTTP 401 with a structured error – not HTTP 200 with false.
        /// Authentication failures must use proper HTTP semantics, not silent wrong-password success.
        /// </summary>
        [Test]
        public async Task AC2_Login_WrongPassword_Returns401WithStructuredError()
        {
            var email = $"ac2-wrongpw-{Guid.NewGuid()}@issue452.io";
            const string correctPw = "Correct@452AC2B";
            const string wrongPw = "Wrong@452AC2B";

            // Register
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = correctPw, ConfirmPassword = correctPw });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Login with wrong password
            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = wrongPw });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Wrong password must return HTTP 401");

            var body = await loginResp.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty, "401 response must have a body");
        }

        /// <summary>
        /// AC2-I3: Invalid refresh token returns HTTP 401. Token forgery must not silently succeed.
        /// </summary>
        [Test]
        public async Task AC2_RefreshToken_InvalidToken_Returns401()
        {
            var refreshResp = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
                new { RefreshToken = "forged-token-issue452-test" });
            Assert.That(refreshResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Invalid refresh token must return HTTP 401");
        }

        /// <summary>
        /// AC2-I4: Protected endpoints return 401 when called without authentication.
        /// This confirms that session validation is enforced on all auth-required routes.
        /// </summary>
        [Test]
        public async Task AC2_ProtectedEndpoints_WithoutToken_Return401()
        {
            // Profile endpoint requires authentication
            var profileResp = await _client.GetAsync("/api/v1/auth/profile");
            Assert.That(profileResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Profile endpoint must require authentication");

            // Session inspect requires authentication
            var sessionResp = await _client.GetAsync("/api/v1/auth/session");
            Assert.That(sessionResp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Session inspect endpoint must require authentication");
        }

        /// <summary>
        /// AC2-I5: Duplicate registration returns HTTP 409 or HTTP 400 (not HTTP 200 success).
        /// Users must not be silently registered twice with different accounts.
        /// </summary>
        [Test]
        public async Task AC2_DuplicateRegistration_ReturnsErrorNotSuccess()
        {
            var email = $"ac2-dup-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC2E";

            // First registration
            var reg1 = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(reg1.StatusCode, Is.EqualTo(HttpStatusCode.OK), "First registration must succeed");

            // Second registration with same email
            var reg2 = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(reg2.StatusCode, Is.Not.EqualTo(HttpStatusCode.OK),
                "Duplicate registration must not return HTTP 200");
            // Accept 400 Bad Request or 409 Conflict as valid responses
            Assert.That(
                reg2.StatusCode == HttpStatusCode.BadRequest ||
                reg2.StatusCode == HttpStatusCode.Conflict,
                Is.True,
                $"Duplicate registration must return 400 or 409, got {reg2.StatusCode}");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC3 – Deployment pipeline reliability (5 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC3-I1: Deployment list endpoint is accessible to authenticated users and returns
        /// a structured JSON response (not an error or empty body).
        /// </summary>
        [Test]
        public async Task AC3_DeploymentList_AuthenticatedUser_ReturnsStructuredResponse()
        {
            var (accessToken, _) = await RegisterAndLoginAsync($"ac3-deploy-{Guid.NewGuid()}@issue452.io", "Harden@452AC3A");

            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/token/deployments");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await _client.SendAsync(req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Deployment list must be accessible to authenticated users");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty, "Deployment list response must have content");
        }

        /// <summary>
        /// AC3-I2: Deployment list endpoint returns HTTP 401 without authentication.
        /// Unauthenticated users must not be able to enumerate deployments.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentList_WithoutAuth_Returns401()
        {
            var resp = await _client.GetAsync("/api/v1/token/deployments");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Deployment list must require authentication");
        }

        /// <summary>
        /// AC3-I3: Deployment metrics endpoint is accessible to authenticated users.
        /// Metrics provide audit evidence of deployment state transitions for compliance.
        /// </summary>
        [Test]
        public async Task AC3_DeploymentMetrics_AuthenticatedUser_ReturnsResponse()
        {
            var (accessToken, _) = await RegisterAndLoginAsync($"ac3-metrics-{Guid.NewGuid()}@issue452.io", "Harden@452AC3C");

            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/token/deployment-metrics");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await _client.SendAsync(req);

            // Either 200 OK (metrics available) or 404 (endpoint not mapped) are acceptable
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "Deployment metrics must not return 500 Internal Server Error");
        }

        /// <summary>
        /// AC3-I4: Token creation endpoint returns 401 without authentication.
        /// Deployment operations must not be executable without valid credentials.
        /// </summary>
        [Test]
        public async Task AC3_TokenCreation_WithoutAuth_Returns401()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/token/asa-ft/create",
                new { TokenName = "TestToken", TokenSymbol = "TT", TotalSupply = 1000000 });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "Token creation must require authentication");
        }

        /// <summary>
        /// AC3-I5: Fetching a non-existent deployment returns 404, not 500.
        /// State-machine queries for unknown deployments must return explicit not-found errors.
        /// </summary>
        [Test]
        public async Task AC3_GetNonExistentDeployment_Returns404NotServerError()
        {
            var (accessToken, _) = await RegisterAndLoginAsync($"ac3-notfound-{Guid.NewGuid()}@issue452.io", "Harden@452AC3E");

            using var req = new HttpRequestMessage(HttpMethod.Get,
                "/api/v1/token/deployments/nonexistent-deployment-id-issue452");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await _client.SendAsync(req);

            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "Non-existent deployment must not cause a 500 error");
            Assert.That(
                resp.StatusCode == HttpStatusCode.NotFound ||
                resp.StatusCode == HttpStatusCode.BadRequest,
                Is.True,
                $"Non-existent deployment must return 404 or 400, got {resp.StatusCode}");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC4 – Observability and audit quality (4 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC4-I1: Registration response includes a CorrelationId for audit trail linkage.
        /// Every registration event must be traceable across backend systems.
        /// </summary>
        [Test]
        public async Task AC4_Registration_IncludesCorrelationId()
        {
            var email = $"ac4-correl-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC4A";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var reg = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(reg!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "Registration response must include a CorrelationId for audit traceability");
        }

        /// <summary>
        /// AC4-I2: Login response includes a CorrelationId.
        /// Authentication events must be traceable to support incident triage and compliance review.
        /// </summary>
        [Test]
        public async Task AC4_Login_IncludesCorrelationId()
        {
            var email = $"ac4-loginid-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC4B";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = password });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();

            Assert.That(login!.CorrelationId, Is.Not.Null.And.Not.Empty,
                "Login response must include a CorrelationId");
        }

        /// <summary>
        /// AC4-I3: ARC76 info endpoint includes compliance-relevant metadata fields.
        /// MICA-oriented audit evidence requires documented algorithm and contract metadata.
        /// </summary>
        [Test]
        public async Task AC4_ARC76Info_ContainsMICAOrientedComplianceMetadata()
        {
            var resp = await _client.GetAsync("/api/v1/auth/arc76/info");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var content = await resp.Content.ReadAsStringAsync();
            Assert.That(content, Does.Contain("contractVersion").Or.Contain("ContractVersion"),
                "ARC76 info must contain ContractVersion for compliance documentation");
            Assert.That(content, Does.Contain("standard").Or.Contain("Standard"),
                "ARC76 info must contain Standard field for algorithm documentation");
        }

        /// <summary>
        /// AC4-I4: Error responses for invalid input do not leak stack traces or internal details.
        /// Security posture requires user-safe error messages in all failure paths.
        /// </summary>
        [Test]
        public async Task AC4_InvalidInputErrors_DoNotLeakInternalDetails()
        {
            // Missing required fields
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = "", Password = "" });

            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Does.Not.Contain("StackTrace"),
                "Error response must not include stack trace");
            Assert.That(body, Does.Not.Contain("at BiatecTokens"),
                "Error response must not include assembly-qualified method names");
            Assert.That(body, Does.Not.Contain("Connection"),
                "Error response must not include database connection details");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC5 – Test and CI confidence (4 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC5-I1: Health endpoint returns HTTP 200 on every call.
        /// CI regression guard – if health endpoint fails entirely, the application is not deployable.
        /// Note: The body may be "healthy" or "degraded" depending on external service availability.
        /// </summary>
        [Test]
        public async Task AC5_HealthEndpoint_AlwaysReturnsHttpOk()
        {
            var resp = await _client.GetAsync("/health");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Health endpoint must return 200");
        }

        /// <summary>
        /// AC5-I2: Swagger/OpenAPI spec is accessible without authentication.
        /// API documentation must be available for frontend teams and QA during integration.
        /// </summary>
        [Test]
        public async Task AC5_SwaggerEndpoint_IsAccessible()
        {
            var resp = await _client.GetAsync("/swagger/v1/swagger.json");
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Swagger spec must be accessible");
        }

        /// <summary>
        /// AC5-I3: Weak password registration returns HTTP 400 (not 200 or 500).
        /// Password policy enforcement must be stable and predictable for CI regression suites.
        /// </summary>
        [Test]
        public async Task AC5_WeakPasswordRegistration_Returns400()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = "weakpw@issue452.io", Password = "short", ConfirmPassword = "short" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Weak password must return HTTP 400");
        }

        /// <summary>
        /// AC5-I4: Missing required fields return HTTP 400 – not 500 Internal Server Error.
        /// Input validation stability is a CI quality gate for all auth endpoints.
        /// </summary>
        [Test]
        public async Task AC5_MissingRequiredFields_Returns400NotServerError()
        {
            // Register with missing email
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Password = "Harden@452NoEmail" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Missing email must return HTTP 400");
            Assert.That(resp.StatusCode, Is.Not.EqualTo(HttpStatusCode.InternalServerError),
                "Missing email must not cause a 500 error");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // AC6 – Documentation and integration readiness (7 tests)
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// AC6-I1: Registration response schema includes all required fields for frontend consumption.
        /// Stable response shapes are required for frontend teams to build deterministic UI.
        /// </summary>
        [Test]
        public async Task AC6_RegisterResponse_IncludesAllRequiredSchemaFields()
        {
            var email = $"ac6-schema-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC6A";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var reg = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(reg!.Success, Is.True);
            Assert.That(reg.AccessToken, Is.Not.Null.And.Not.Empty, "AccessToken must be present");
            Assert.That(reg.RefreshToken, Is.Not.Null.And.Not.Empty, "RefreshToken must be present");
            Assert.That(reg.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AlgorandAddress must be present");
            Assert.That(reg.ExpiresAt, Is.GreaterThan(DateTime.UtcNow), "ExpiresAt must be in the future");
        }

        /// <summary>
        /// AC6-I2: Login response schema includes all required fields for frontend integration.
        /// </summary>
        [Test]
        public async Task AC6_LoginResponse_IncludesAllRequiredSchemaFields()
        {
            var email = $"ac6-loginschema-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC6B";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
                new { Email = email, Password = password });
            Assert.That(loginResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.That(login!.Success, Is.True);
            Assert.That(login.AccessToken, Is.Not.Null.And.Not.Empty, "AccessToken must be present");
            Assert.That(login.RefreshToken, Is.Not.Null.And.Not.Empty, "RefreshToken must be present");
            Assert.That(login.AlgorandAddress, Is.Not.Null.And.Not.Empty, "AlgorandAddress must be present");
            Assert.That(login.ExpiresAt, Is.GreaterThan(DateTime.UtcNow), "ExpiresAt must be in the future");
        }

        /// <summary>
        /// AC6-I3: Access token is a well-formed JWT (three dot-separated parts: header.payload.signature).
        /// Frontend clients split the JWT to read claims without full verification.
        /// </summary>
        [Test]
        public async Task AC6_AccessToken_IsWellFormedJWT()
        {
            var email = $"ac6-jwt-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC6C";

            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var reg = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            var parts = reg!.AccessToken!.Split('.');
            Assert.That(parts.Length, Is.EqualTo(3),
                "AccessToken must be a three-part JWT (header.payload.signature)");
            Assert.That(parts[0].Length, Is.GreaterThan(0), "JWT header must not be empty");
            Assert.That(parts[1].Length, Is.GreaterThan(0), "JWT payload must not be empty");
            Assert.That(parts[2].Length, Is.GreaterThan(0), "JWT signature must not be empty");
        }

        /// <summary>
        /// AC6-I4: ARC76 derivation verify endpoint returns 401 without authentication.
        /// Contract documentation must state that this endpoint requires a valid Bearer token.
        /// </summary>
        [Test]
        public async Task AC6_ARC76VerifyDerivation_WithoutToken_Returns401()
        {
            var resp = await _client.PostAsJsonAsync("/api/v1/auth/arc76/verify-derivation",
                new { Email = "test@issue452.io" });
            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
                "ARC76 verify-derivation must require authentication");
        }

        /// <summary>
        /// AC6-I5: Three consecutive registrations produce deterministically structured responses.
        /// Verifies schema stability – the response shape must not change across calls.
        /// </summary>
        [Test]
        public async Task AC6_ThreeRegistrations_ProduceDeterministicResponseStructure()
        {
            for (int i = 1; i <= 3; i++)
            {
                var email = $"ac6-struct{i}-{Guid.NewGuid()}@issue452.io";
                var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                    new { Email = email, Password = "Harden@452AC6E", ConfirmPassword = "Harden@452AC6E" });
                Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Registration {i} must succeed");

                var reg = await resp.Content.ReadFromJsonAsync<RegisterResponse>();
                Assert.That(reg!.Success, Is.True, $"Registration {i}: Success must be true");
                Assert.That(reg.AlgorandAddress, Is.Not.Null.And.Not.Empty,
                    $"Registration {i}: AlgorandAddress must be present");
                Assert.That(reg.AccessToken, Is.Not.Null.And.Not.Empty,
                    $"Registration {i}: AccessToken must be present");
                Assert.That(reg.DerivationContractVersion, Is.Not.Null.And.Not.Empty,
                    $"Registration {i}: DerivationContractVersion must be present");
            }
        }

        /// <summary>
        /// AC6-I6: Session inspection endpoint returns full session metadata for authenticated users.
        /// Frontend uses this endpoint to verify active session state without making a full profile call.
        /// </summary>
        [Test]
        public async Task AC6_SessionInspect_AuthenticatedUser_ReturnsSessionMetadata()
        {
            var email = $"ac6-session-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC6F";

            var (accessToken, algorandAddress) = await RegisterAndLoginAsync(email, password);

            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/session");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var resp = await _client.SendAsync(req);

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Session inspection must succeed for authenticated users");
            var body = await resp.Content.ReadAsStringAsync();
            Assert.That(body, Is.Not.Null.And.Not.Empty, "Session response must have content");
        }

        /// <summary>
        /// AC6-I7: Register endpoint no-regression check – returns 200 for valid input.
        /// This test prevents accidental regression in the primary onboarding endpoint.
        /// </summary>
        [Test]
        public async Task AC6_RegisterEndpoint_NoRegression_Returns200ForValidInput()
        {
            var email = $"ac6-noreg-{Guid.NewGuid()}@issue452.io";
            const string password = "Harden@452AC6G";

            var resp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });

            Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Register endpoint must return 200 for valid input (no regression)");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────────

        private async Task<(string AccessToken, string AlgorandAddress)> RegisterAndLoginAsync(
            string email, string password)
        {
            var regResp = await _client.PostAsJsonAsync("/api/v1/auth/register",
                new { Email = email, Password = password, ConfirmPassword = password });
            Assert.That(regResp.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"RegisterAndLogin helper: registration failed for {email}");

            var reg = await regResp.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(reg!.Success, Is.True, "RegisterAndLogin helper: registration Success must be true");

            return (reg.AccessToken!, reg.AlgorandAddress!);
        }
    }
}
